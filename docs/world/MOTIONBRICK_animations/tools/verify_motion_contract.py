"""T-MB06: контракт «стенд-ап → ход» по времени: задержка старта, скорость модели, темп.

Мотив (17.11.2026, приёмка после fix 17.11): «после нажатия WASD модель начинает бежать
только через 3+ секунды», «сильно слайдит и дрифтит — нет контакта с поверхностью».

Ни один харнесс этого не измерял: `verify_pose_stability.py` — только амплитуды закрутки,
`verify_root_frame.py` — только согласованность корня, `verify_pose_directions.py` — только
направления на установившемся ходу. Здесь меряются три вещи, которые видит глаз:

    onset_frames / onset_seconds   сколько кадров от первой команды движения до того, как
                                   поза реально пошла (амплитуда качания бедра)
    step_wall_ms / inference       успевает ли модель за 60 Гц реального времени
    model_speed_mps / slide_ratio  сколько модель реально проезжает за секунду против
                                   команды (slide_ratio != 1 — это и есть «слайдит»)

    python verify_motion_contract.py
    python verify_motion_contract.py --verbose
"""

from __future__ import annotations

import json
import math
import sys
import time
from collections import Counter
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import runtime_sidecar as rs  # noqa: E402
from verify_pose_directions import segment_directions  # noqa: E402
from verify_pose_stability import fk  # noqa: E402

WALK_SPEED = 5.0   # как дефолт `MotionBricksRuntimePlayback._walkSpeed`
RUN_SPEED = 10.0   # как дефолт `MotionBricksRuntimePlayback._runSpeed`
IDLE_FRAMES = 90
MOVE_FRAMES = 300
DT = 1.0 / 60.0
ONSET_MARGIN = 0.12
ONSET_SUSTAIN = 5


def thigh_swing_z(packet) -> float:
    _, positions = fk(np.asarray(packet, dtype=np.float64).reshape(-1))
    directions = segment_directions(positions)
    return float(directions["thigh.L"][2])


def main() -> None:
    verbose = "--verbose" in sys.argv
    idle_frames = IDLE_FRAMES
    for index, value in enumerate(sys.argv):
        if value == "--idle-frames" and index + 1 < len(sys.argv):
            idle_frames = int(sys.argv[index + 1])
    repo = Path(r"C:/Projects/motion_bricks/motionbricks")
    runtime = rs.ExternalSOMACharacter(repo, rs.InputFrame(flags=rs.FLAG_RESET), False)

    position = np.zeros(3, dtype=np.float32)
    forward = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    sources = Counter()
    swings: list[float] = []
    step_wall_ms: list[float] = []
    ground: list[np.ndarray] = []
    frames = 0

    def run(speed: float) -> None:
        nonlocal position, frames
        velocity = forward * speed
        if speed > 0.0:
            position = position + velocity * DT
        frame = rs.InputFrame(
            sequence=frames + 1,
            flags=rs.FLAG_RESET if frames == 0 else 0,
            delta_time=DT,
            walk_speed=WALK_SPEED,
            run_speed=RUN_SPEED,
            position=position.copy(),
            rotation=np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float32),
            velocity=velocity,
        )
        started = time.perf_counter()
        packet, _ = runtime.step(frame)
        step_wall_ms.append((time.perf_counter() - started) * 1000.0)
        sources[runtime.character._playback_source.value] += 1
        swings.append(thigh_swing_z(packet))
        ground.append(np.array(runtime.character.character_ground_position, dtype=np.float64).reshape(-1)[:3].copy())
        frames += 1
        if verbose:
            print(f"{frames:4d} speed={speed:4.1f} swing={swings[-1]:+6.3f} wall={step_wall_ms[-1]:6.1f} ms")

    try:
        for _ in range(idle_frames):
            run(0.0)
        idle_end = len(swings)
        for _ in range(MOVE_FRAMES):
            run(WALK_SPEED)
    finally:
        runtime.close()

    idle_baseline = float(np.mean([abs(v) for v in swings[max(0, idle_end - 20):idle_end]]))
    threshold = idle_baseline + ONSET_MARGIN
    onset = None
    for index in range(idle_end, len(swings)):
        window = swings[index:index + ONSET_SUSTAIN]
        if len(window) < ONSET_SUSTAIN:
            break
        if all(abs(v) > threshold for v in window):
            onset = index - idle_end
            break

    # Скорость модели по её собственному корню на установившемся ходу (вторая половина).
    steady_start = len(ground) - MOVE_FRAMES // 2
    displacement = float(np.linalg.norm(ground[-1][:2] - ground[steady_start][:2]))
    steady_seconds = (len(ground) - 1 - steady_start) * DT
    model_speed = displacement / max(1e-6, steady_seconds)

    ramp = {}
    for index in range(idle_end, len(swings), 30):
        window = [abs(v) for v in swings[index:index + 30]]
        ramp[f"+{round((index - idle_end) * DT, 2)}s"] = round(float(np.max(window)), 3) if window else 0.0

    result = {
        "idle_frames": idle_end,
        "move_frames": MOVE_FRAMES,
        "command_speed_mps": WALK_SPEED,
        "idle_swing_baseline": round(idle_baseline, 4),
        "onset_threshold": round(threshold, 4),
        "onset_frames": onset,
        "onset_seconds": None if onset is None else round(onset * DT, 2),
        "swing_ramp_max_abs_z": ramp,
        "model_root_displacement_m": round(displacement, 3),
        "model_speed_mps": round(model_speed, 3),
        "slide_ratio": round(model_speed / WALK_SPEED, 3),
        "step_wall_ms": {
            "mean": round(float(np.mean(step_wall_ms)), 3),
            "p95": round(float(np.percentile(step_wall_ms, 95)), 3),
            "max": round(float(np.max(step_wall_ms)), 3),
        },
        "realtime_budget_ms": round(DT * 1000.0, 3),
        "keeps_up_with_60hz": bool(float(np.percentile(step_wall_ms, 95)) < DT * 1000.0),
        "inference_wall_ms": {
            "count": runtime.character.generation_count,
            "mean": round(float(np.mean(runtime.character.wall_times_ms)), 3) if runtime.character.wall_times_ms else 0.0,
            "max": round(float(np.max(runtime.character.wall_times_ms)), 3) if runtime.character.wall_times_ms else 0.0,
        },
        "playback_sources": dict(sources),
    }
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
