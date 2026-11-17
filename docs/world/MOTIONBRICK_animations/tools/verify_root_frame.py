"""T-MB06: численная проверка пространства root для runtime-ретаргета.

Считает ровно то, что делает C# `MotionBricksSomaRetarget.Apply()` для корня:

    delta_root = ToUnityRot(packet_root) * inverse(ToUnityRot(bind_root))
    ToUnityRot(x, y, z, w) = (x, -y, -z, w)          // как в retarget
    packet_root            = root из model_pose()    // то, что уходит в UDP

Контракт (как у принятого offline-бейка `MotionBricksSpikeBaker`, `YawOffsetDeg = 0f`):
delta_root — это `frame * bind^-1`, то есть в покое (игрок стоит) он близок к identity.
Мировое рыскание добавляется отдельно в `_apply_external_root`, поэтому дельта не зависит
от сгенерированного движения.

Регрессия, которую ловит скрипт: до fix 17.11 `_capture_reference()` снимал эталон из
клона model-space bind **до** первого `_sample_playback()`, и `delta_root` вырождался в
постоянный `M^-1` (`M = model_to_runtime` = +90 deg вокруг X) — жёсткий завал персонажа
на бок. Скрипт печатает эту контрфактическую дельту рядом с измеренной.

Вторая регрессия (fix 17.12): `_capture_reference()` записывал yaw игрока, а
`_apply_external_root` строил корень как `app * inv(ref) * M * bind`. `model_pose` снимает
`inv(app)`, поэтому в риг уезжало `M^-1 * inv(ref) * M * bind`: RESET на ненулевом yaw
(airborne STOP/START после разворота) давал постоянный поворот корня — персонаж задом
наперёд. Фаза `reset180` ловит это: RESET на yaw 180°, `delta_root` обязан остаться ~0.

Запуск:
    python docs/world/MOTIONBRICK_animations/tools/verify_root_frame.py
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import runtime_sidecar as rs  # noqa: E402

JOINTS = rs.JOINTS
FLOATS_PER_JOINT = rs.FLOATS_PER_JOINT
MAX_IDLE_DELTA_DEG = 30.0


def mirror(q: np.ndarray) -> np.ndarray:
    """ToUnityRot: (x, y, z, w) -> (x, -y, -z, w)."""
    return np.array([q[0], -q[1], -q[2], q[3]], dtype=np.float64)


def delta_root(packet_root_model: np.ndarray, bind_root_model: np.ndarray) -> tuple[float, list[float]]:
    """Угол и ось delta_root так, как их посчитает C# (в Unity-базисе)."""
    frame = mirror(rs._normalize_quaternion(np.asarray(packet_root_model, dtype=np.float64)))
    bind = mirror(rs._normalize_quaternion(np.asarray(bind_root_model, dtype=np.float64)))
    delta = rs._quat_multiply(frame, rs._quat_inverse(bind))
    w = float(np.clip(delta[3], -1.0, 1.0))
    angle = math.degrees(2.0 * math.acos(abs(w)))
    sin_half = math.sqrt(max(0.0, 1.0 - w * w))
    if sin_half < 1.0e-9:
        axis = [0.0, 0.0, 0.0]
    else:
        sign = -1.0 if w < 0.0 else 1.0
        axis = [round(sign * float(value) / sin_half, 4) for value in delta[0:3]]
    return round(angle, 3), axis


def run(repo: Path, fp16: bool = False) -> dict:
    frame = rs.InputFrame(flags=rs.FLAG_RESET)
    runtime = rs.ExternalSOMACharacter(repo, frame, fp16)
    bind = np.asarray(runtime.bind_pose, dtype=np.float32).reshape(JOINTS, FLOATS_PER_JOINT)
    bind_root_model = bind[0, 3:7].astype(np.float64)
    model_to_runtime = np.asarray(runtime.model_to_runtime.q, dtype=np.float64)
    samples: list[dict] = []
    contacts_max: dict[str, float] = {}
    contacts_last: list[float] = []
    position = np.zeros(3, dtype=np.float32)
    frames = 0
    try:
        phases = (
            ("idle", 1.0, 0.0, 0.0),
            ("walk", 1.0, 5.0, 0.0),
            ("turn", 0.5, 5.0, math.pi * 0.5),
            ("turn180", 1.0, 5.0, math.pi),
            # RESET на ненулевом yaw — именно это происходит в игре в airborne STOP/START.
            ("reset180", 1.0, 5.0, math.pi),
        )
        for name, duration, speed, target_yaw in phases:
            for index in range(round(duration * 60.0)):
                yaw = target_yaw if speed == 0.0 else target_yaw * min(1.0, (index + 1) / 30.0)
                direction = np.array([math.sin(yaw), 0.0, math.cos(yaw)], dtype=np.float32)
                velocity = direction * speed
                position = position + velocity / 60.0
                reset_here = frames == 0 or (name == "reset180" and index == 0)
                frame = rs.InputFrame(
                    sequence=frames + 1,
                    flags=rs.FLAG_RESET if reset_here else 0,
                    position=position.copy(),
                    rotation=np.array([0.0, math.sin(yaw * 0.5), 0.0, math.cos(yaw * 0.5)], dtype=np.float32),
                    velocity=velocity,
                )
                pose, contacts = runtime.step(frame)
                if not np.isfinite(pose).all():
                    raise RuntimeError(f"non-finite pose at frame {frames}")
                contacts_flat = np.asarray(contacts, dtype=np.float64).reshape(-1)
                if contacts_flat.size:
                    contacts_last = [round(float(value), 4) for value in contacts_flat]
                    bucket = "idle" if speed == 0.0 else "move"
                    contacts_max[bucket] = max(contacts_max.get(bucket, 0.0), float(contacts_flat.max()))
                sampled = (name == "idle" and index in (0, 15, 29)) \
                    or (name == "walk" and index in (0, 30, 59)) \
                    or (name == "turn" and index == 29) \
                    or (name == "turn180" and index in (29, 59)) \
                    or (name == "reset180" and index in (0, 1, 2, 5, 15, 29, 59))
                if sampled:
                    angle, axis = delta_root(pose[0, 3:7], bind_root_model)
                    samples.append(
                        {
                            "phase": name,
                            "frame": frames,
                            "speed_mps": speed,
                            "player_yaw_deg": round(math.degrees(yaw), 2),
                            "delta_root_deg": angle,
                            "delta_root_axis": axis,
                        }
                    )
                frames += 1
    finally:
        runtime.close()

    # Контрфактическая дельта старого порядка: эталон = model-space bind,
    # packet_root = M^-1 * bind  ->  delta_root = U(M^-1).
    pre_fix_root = rs._quat_multiply(rs._quat_inverse(model_to_runtime), bind_root_model)
    pre_fix_angle, pre_fix_axis = delta_root(pre_fix_root, bind_root_model)

    # Контрфактическая дельта старого yaw-контракта: эталон = yaw игрока на RESET.
    # RESET на развороте 180°  ->  packet_root = M^-1 * inv(Ry(180)) * M * bind.
    yaw180 = np.array([0.0, math.sin(math.pi * 0.5), 0.0, math.cos(math.pi * 0.5)], dtype=np.float64)
    pre_fix_reset_root = rs._quat_multiply(
        rs._quat_inverse(model_to_runtime),
        rs._quat_multiply(
            rs._quat_inverse(rs.unity_to_app_rotation(yaw180)),
            rs._quat_multiply(model_to_runtime, bind_root_model),
        ),
    )
    pre_fix_reset_angle, pre_fix_reset_axis = delta_root(pre_fix_reset_root, bind_root_model)

    idle = [item["delta_root_deg"] for item in samples if item["phase"] == "idle"]
    walk = [item["delta_root_deg"] for item in samples if item["phase"] == "walk"]
    turn = [item["delta_root_deg"] for item in samples if item["phase"] == "turn"]
    after_reset = [item["delta_root_deg"] for item in samples if item["phase"] == "reset180"]
    passed = (
        bool(idle)
        and bool(walk)
        and bool(after_reset)
        and max(idle) < MAX_IDLE_DELTA_DEG
        and max(walk) < MAX_IDLE_DELTA_DEG
        and max(after_reset) < MAX_IDLE_DELTA_DEG
    )
    return {
        "bind_root_model": [round(float(value), 6) for value in bind_root_model],
        "model_to_runtime_axis_angle_deg": round(
            math.degrees(
                2.0 * math.acos(min(1.0, abs(float(model_to_runtime[3]))))
            ),
            3,
        ),
        "samples": samples,
        "max_delta_root_deg_idle": max(idle) if idle else None,
        "max_delta_root_deg_walk_yaw0": max(walk) if walk else None,
        "delta_root_deg_turning": turn,
        "max_delta_root_deg_after_reset_yaw180": max(after_reset) if after_reset else None,
        "pre_fix_counterfactual": {
            "delta_root_deg": pre_fix_angle,
            "delta_root_axis": pre_fix_axis,
        },
        "pre_fix_reset_yaw_counterfactual": {
            "delta_root_deg": pre_fix_reset_angle,
            "delta_root_axis": pre_fix_reset_axis,
        },
        "contacts_max": contacts_max,
        "contacts_last": contacts_last,
        "frames": frames,
        "threshold_deg": MAX_IDLE_DELTA_DEG,
        "status": "PASS" if passed else "FAIL",
    }


def main() -> None:
    result = run(Path(r"C:/Projects/motion_bricks/motionbricks"))
    print(json.dumps(result, indent=2), flush=True)
    if result["status"] != "PASS":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
