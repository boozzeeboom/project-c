"""T-MB06: численная проверка «куда смотрят руки и ноги» в позе, которую отдаёт sidecar.

Мотив (17.11.2026): приёмка «руки напутаны, ноги ведут не туда» не была ничем измерена.
`verify_pose_stability.py` меряет только МАГНИТУДУ локальных дельт (насколько поза ушла от
bind), а `verify_root_frame.py` — только корень. Ни один харнесс не отвечал на вопрос
«конечность смотрит туда или нет».

Здесь меряется то же, что видит глаз, но в числах: направления сегментов скелета, посчитанные
из пакета в той же системе, в которой их строит C# (`ToUnityPos`/`ToUnityRot`). В этой системе
down = -Y, forward модели ~ +Z (носок стопы в bind), т.е. совпадает с осями Unity — поэтому
числа сравнимы с тем, что видно в сцене.

Показатели на прямом ходе (5 м/с, 4 s = 2 полных цикла):

    arm_down_deg      угол плечо->локоть от -Y   (стоя/на ходу рука должна висеть, не торчать)
    thigh_down_deg    угол бедро->колено от -Y
    thigh_scissor     corr(Z-качание бедра L, Z-качание бедра R): на ходу должно быть ~ -1
                      (ноги в противофазе); если > 0 — «ноги ведут не туда» обе разом
    arm_leg_same      corr(Z-качание arm.L, Z-качание thigh.L): контралатерально ~ -1

    python verify_pose_directions.py
    python verify_pose_directions.py --verbose
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import runtime_sidecar as rs  # noqa: E402
from verify_pose_stability import fk  # noqa: E402

WALK_SPEED = 5.0
WALK_FRAMES = 240  # 4 s при 60 Гц
WARMUP_FRAMES = 60

# Сегменты (SOMA joint индексы): родитель -> ребёнок.
SEG = {
    "spine": (1, 3),
    "chest": (3, 4),
    "head": (4, 6),
    "upperArm.L": (12, 13),
    "forearm.L": (13, 14),
    "upperArm.R": (40, 41),
    "forearm.R": (41, 42),
    "thigh.L": (67, 68),
    "shin.L": (68, 69),
    "thigh.R": (72, 73),
    "shin.R": (73, 74),
    "toe.L": (69, 70),
    "toe.R": (74, 75),
}

# Пороги держим по факту замеренного, а не «как хотелось бы»; см. docstring.
LIMITS = {
    "arm_down_deg": 60.0,
    "thigh_down_deg": 55.0,
    "thigh_scissor_max": -0.30,
}


def segment_directions(positions) -> dict:
    result = {}
    for name, (parent, child) in SEG.items():
        direction = np.asarray(positions[child], dtype=np.float64) - np.asarray(positions[parent], dtype=np.float64)
        length = float(np.linalg.norm(direction))
        result[name] = direction / length if length > 1e-9 else np.zeros(3)
    return result


def down_angle_deg(direction) -> float:
    value = max(-1.0, min(1.0, -float(direction[1])))
    return math.degrees(math.acos(value))


def up_angle_deg(direction) -> float:
    value = max(-1.0, min(1.0, float(direction[1])))
    return math.degrees(math.acos(value))


def _normalize(q) -> np.ndarray:
    q = np.asarray(q, dtype=np.float64)
    return q / max(1e-12, float(np.linalg.norm(q)))


def quat_from_to(a, b) -> np.ndarray:
    a = _normalize(a)
    b = _normalize(b)
    dot = max(-1.0, min(1.0, float(np.dot(a, b))))
    if dot > 1.0 - 1e-9:
        return np.array([0.0, 0.0, 0.0, 1.0])
    if dot < -1.0 + 1e-9:
        axis = np.cross(a, np.array([1.0, 0.0, 0.0]))
        if float(np.linalg.norm(axis)) < 1e-6:
            axis = np.cross(a, np.array([0.0, 1.0, 0.0]))
        axis = _normalize(axis)
        return np.array([axis[0], axis[1], axis[2], 0.0])
    axis = _normalize(np.cross(a, b))
    half = math.acos(dot) * 0.5
    return np.array([axis[0] * math.sin(half), axis[1] * math.sin(half), axis[2] * math.sin(half), math.cos(half)])


def clamp_rotation(q, limit_deg: float) -> np.ndarray:
    """Повторить `Quaternion.Slerp(identity, q, limit/angle)` из C#."""
    q = _normalize(q)
    w = max(-1.0, min(1.0, abs(float(q[3]))))
    angle = math.degrees(2.0 * math.acos(w))
    if angle <= limit_deg or angle < 1e-6:
        return q
    scale = limit_deg / angle
    half = angle * 0.5 * scale
    axis_part = np.array(q[:3], dtype=np.float64)
    norm = float(np.linalg.norm(axis_part))
    if norm < 1e-9:
        return np.array([0.0, 0.0, 0.0, 1.0])
    axis = axis_part / norm
    sign = 1.0 if float(q[3]) >= 0.0 else -1.0
    return np.array([axis[0] * math.sin(half) * sign, axis[1] * math.sin(half) * sign,
                     axis[2] * math.sin(half) * sign, math.cos(half)])


def angle_deg(a, b) -> float:
    delta = rs._quat_multiply(_normalize(a), rs._quat_inverse(_normalize(b)))
    w = max(-1.0, min(1.0, abs(float(delta[3]))))
    return math.degrees(2.0 * math.acos(w))


def correlation(a, b) -> float:
    a = np.asarray(a, dtype=np.float64)
    b = np.asarray(b, dtype=np.float64)
    if a.size < 3 or a.std() < 1e-6 or b.std() < 1e-6:
        return 0.0
    return float(np.corrcoef(a, b)[0, 1])


def main() -> None:
    verbose = "--verbose" in sys.argv
    repo = Path(r"C:/Projects/motion_bricks/motionbricks")
    runtime = rs.ExternalSOMACharacter(repo, rs.InputFrame(flags=rs.FLAG_RESET), False)
    bind = np.asarray(runtime.bind_pose, dtype=np.float64).reshape(-1)
    bind_rotations, bind_positions = fk(bind)
    bind_directions = segment_directions(bind_positions)
    # Ключицы ровно как в C#: `Clavicles = {(3, 11), (3, 39)}`, направление — chest -> shoulder.
    clav_rest = {
        "L": _normalize(np.asarray(bind_positions[11], dtype=np.float64) - np.asarray(bind_positions[3], dtype=np.float64)),
        "R": _normalize(np.asarray(bind_positions[39], dtype=np.float64) - np.asarray(bind_positions[3], dtype=np.float64)),
    }
    MAX_CLAVICLE_DEG = 45.0  # MotionBricksSomaRetarget.MaxClavicleDeg

    position = np.zeros(3, dtype=np.float32)
    forward = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    samples = {name: [] for name in SEG}
    arm_down = {"L": [], "R": []}
    thigh_down = {"L": [], "R": []}
    clavicle_swing = {"L": [], "R": []}
    arm_extra = {"L": [], "R": []}
    frames = 0

    try:
        for _ in range(WALK_FRAMES):
            velocity = forward * WALK_SPEED
            position = position + velocity / 60.0
            # Строго прямой ход: yaw = 0, направление движения = facing = +Z.
            frame = rs.InputFrame(
                sequence=frames + 1,
                flags=rs.FLAG_RESET if frames == 0 else 0,
                position=position.copy(),
                rotation=np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float32),
                velocity=velocity,
            )
            packet, _ = runtime.step(frame)
            frames += 1
            if frames <= WARMUP_FRAMES:
                continue  # дать модели разогнаться из idle
            rotations, positions = fk(np.asarray(packet, dtype=np.float64).reshape(-1))
            directions = segment_directions(positions)

            # --- проверка ветки ключицы в MotionBricksSomaRetarget.Apply ---
            # C# умножает `swing` (поворот chest->shoulder) НА ДЕЛЬТУ кости руки:
            #     _deltaByPose[12|40] = swing * delta
            # Но `delta = F*B^-1` — уже ПОЛНЫЙ мировой поворот кости, он сам по себе
            # приводит сегмент руки в нужное направление. Значит swing домножается лишний раз.
            for side, arm_joint, arm_child, shoulder in (("L", 12, 13, 11), ("R", 40, 41, 39)):
                chest_dir = np.asarray(positions[shoulder], dtype=np.float64) - np.asarray(positions[3], dtype=np.float64)
                if float(np.linalg.norm(chest_dir)) < 1e-9:
                    continue
                swing_raw = quat_from_to(clav_rest[side], _normalize(chest_dir))
                swing = clamp_rotation(swing_raw, MAX_CLAVICLE_DEG)
                clavicle_swing[side].append(angle_deg(swing, np.array([0.0, 0.0, 0.0, 1.0])))

                delta = rs._quat_multiply(_normalize(rotations[arm_joint]),
                                          rs._quat_inverse(_normalize(bind_rotations[arm_joint])))
                rest_dir = bind_directions["upperArm.L" if side == "L" else "upperArm.R"]
                correct_dir = rs._quat_rotate(delta, rest_dir)
                doubled_dir = rs._quat_rotate(rs._quat_multiply(swing, delta), rest_dir)
                arm_extra[side].append(
                    math.degrees(math.acos(max(-1.0, min(1.0, float(np.dot(correct_dir, doubled_dir)))))))
            for name, direction in directions.items():
                samples[name].append(direction)
            arm_down["L"].append(down_angle_deg(directions["upperArm.L"]))
            arm_down["R"].append(down_angle_deg(directions["upperArm.R"]))
            thigh_down["L"].append(down_angle_deg(directions["thigh.L"]))
            thigh_down["R"].append(down_angle_deg(directions["thigh.R"]))
            if verbose:
                print(
                    f"{frames:4d} armL_down={arm_down['L'][-1]:6.1f} armR_down={arm_down['R'][-1]:6.1f} "
                    f"thighL_down={thigh_down['L'][-1]:6.1f} thighR_down={thigh_down['R'][-1]:6.1f} "
                    f"thighL_z={directions['thigh.L'][2]:+6.2f} thighR_z={directions['thigh.R'][2]:+6.2f} "
                    f"armL_z={directions['upperArm.L'][2]:+6.2f} armR_z={directions['upperArm.R'][2]:+6.2f}"
                )
    finally:
        runtime.close()

    def mean_component(name: str, axis: int) -> float:
        return float(np.mean([d[axis] for d in samples[name]]))

    thigh_l_z = [float(d[2]) for d in samples["thigh.L"]]
    thigh_r_z = [float(d[2]) for d in samples["thigh.R"]]
    arm_l_z = [float(d[2]) for d in samples["upperArm.L"]]
    arm_r_z = [float(d[2]) for d in samples["upperArm.R"]]

    metrics = {
        "arm_down_deg": round(float(np.mean(arm_down["L"] + arm_down["R"])), 2),
        "arm_down_deg_L": round(float(np.mean(arm_down["L"])), 2),
        "arm_down_deg_R": round(float(np.mean(arm_down["R"])), 2),
        "thigh_down_deg": round(float(np.mean(thigh_down["L"] + thigh_down["R"])), 2),
        "thigh_down_deg_L": round(float(np.mean(thigh_down["L"])), 2),
        "thigh_down_deg_R": round(float(np.mean(thigh_down["R"])), 2),
        # Противофаза ног: на ходу Z-качание бёдер противоположно.
        "thigh_scissor": round(correlation(thigh_l_z, thigh_r_z), 3),
        # Контралатеральность «рука против одноимённой ноги».
        "arm_leg_same_L": round(correlation(arm_l_z, thigh_l_z), 3),
        "arm_leg_same_R": round(correlation(arm_r_z, thigh_r_z), 3),
        # Симметрия: правая рука не должна качаться вместе с левой вместо ноги.
        "arm_pair": round(correlation(arm_l_z, arm_r_z), 3),
        # Куда смещены конечности по X: L и R должны быть по разные стороны.
        "upperArm_L_x": round(mean_component("upperArm.L", 0), 3),
        "upperArm_R_x": round(mean_component("upperArm.R", 0), 3),
        "thigh_L_x": round(mean_component("thigh.L", 0), 3),
        "thigh_R_x": round(mean_component("thigh.R", 0), 3),
        # Куда смотрит носок: forward у модели — +Z.
        "toe_L_z": round(mean_component("toe.L", 2), 3),
        "toe_R_z": round(mean_component("toe.R", 2), 3),
        # Наклон корпуса: ось spine на ходу должна быть почти вертикальна.
        "spine_up_deg": round(up_angle_deg(np.mean([d for d in samples["spine"]], axis=0)), 2),
        # Насколько лишний поворот ключицы сдвигает сегмент руки от правильного положения.
        "clavicle_swing_deg": round(float(np.mean(clavicle_swing["L"] + clavicle_swing["R"])), 2),
        "arm_extra_deg": round(float(np.mean(arm_extra["L"] + arm_extra["R"])), 2),
        "arm_extra_deg_L": round(float(np.mean(arm_extra["L"])), 2),
        "arm_extra_deg_R": round(float(np.mean(arm_extra["R"])), 2),
        "bind": {
            "upperArm_L": [round(float(v), 3) for v in bind_directions["upperArm.L"]],
            "upperArm_R": [round(float(v), 3) for v in bind_directions["upperArm.R"]],
            "thigh_L": [round(float(v), 3) for v in bind_directions["thigh.L"]],
            "thigh_R": [round(float(v), 3) for v in bind_directions["thigh.R"]],
            "toe_L": [round(float(v), 3) for v in bind_directions["toe.L"]],
            "toe_R": [round(float(v), 3) for v in bind_directions["toe.R"]],
        },
    }

    failures = []
    if metrics["arm_down_deg"] > LIMITS["arm_down_deg"]:
        failures.append(f"arm_down_deg {metrics['arm_down_deg']}>{LIMITS['arm_down_deg']}")
    if metrics["thigh_down_deg"] > LIMITS["thigh_down_deg"]:
        failures.append(f"thigh_down_deg {metrics['thigh_down_deg']}>{LIMITS['thigh_down_deg']}")
    if metrics["thigh_scissor"] > LIMITS["thigh_scissor_max"]:
        failures.append(f"thigh_scissor {metrics['thigh_scissor']}>{LIMITS['thigh_scissor_max']} (ноги не в противофазе)")
    if metrics["arm_extra_deg"] > 10.0:
        failures.append(f"arm_extra_deg {metrics['arm_extra_deg']}>10 (ключица лишний раз поворачивает руку)")

    print(json.dumps({
        "frames_measured": len(arm_down["L"]),
        "walk_speed_mps": WALK_SPEED,
        "metrics": metrics,
        "limits": LIMITS,
        "failures": failures,
        "status": "PASS" if not failures else "FAIL",
    }, indent=2))


if __name__ == "__main__":
    main()
