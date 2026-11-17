"""Шаг скорости: сколько времени модели нужно, чтобы РЕАЛЬНО поехать быстрее.

Меряется не команда, не длина чанка и не RTT, а путь, который модель проходит за кадр —
то, что видно глазом. Сценарий один и тот же для каждого пресета LocomotionSettings:

    idle 1.5 с → walk 5 м/с 3 с → run 10 м/с 3 с

Для каждого шага снимается t50 / t90 (время до половины и до 90% целевой скорости) и
установившаяся скорость. Так видно, что именно даёт отклик: полураспады команды или
частота регенерации.

    python verify_step_response.py
    python verify_step_response.py --verbose
"""

from __future__ import annotations

import json
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import runtime_sidecar as rs  # noqa: E402

WALK_SPEED = 5.0
RUN_SPEED = 10.0
IDLE_FRAMES = 90
MOVE_FRAMES = 180
DT = 1.0 / 60.0

PRESETS = {
    "engine-defaults": {"settings": {}, "tokens": None, "engine_defaults": True},
    "movement-only": {
        "settings": dict(rs.MOVEMENT_SETTINGS),
        "tokens": rs.RESPONSIVE_TOKEN_COUNTS,
        "position_scale": rs.RESPONSIVE_POSITION_SCALE,
        "early": rs.RESPONSIVE_TRANSITION_EARLY_FRAMES,
        "engine_defaults": False,
    },
    "responsive-full": {
        "settings": dict(rs.RESPONSIVE_SETTINGS),
        "tokens": rs.RESPONSIVE_TOKEN_COUNTS,
        "position_scale": rs.RESPONSIVE_POSITION_SCALE,
        "early": rs.RESPONSIVE_TRANSITION_EARLY_FRAMES,
        "engine_defaults": False,
    },
    "short-chunk-tokens-only": {"settings": {}, "tokens": (6,), "engine_defaults": True},
    "short-chunk": {"settings": dict(rs.RESPONSIVE_SETTINGS), "tokens": (6,), "engine_defaults": True},
}


def ramp_metrics(speeds: list[float], start: int, target: float) -> dict:
    """Время от кадра `start` до 50% и 90% пути от скорости перед шагом до целевой."""

    baseline = float(np.mean(speeds[max(0, start - 10):start])) if start >= 10 else 0.0
    span = target - baseline
    result = {"baseline_mps": round(baseline, 3), "t50_ms": None, "t90_ms": None}
    if abs(span) < 1e-6:
        return result
    for index in range(start, len(speeds)):
        fraction = (speeds[index] - baseline) / span
        if result["t50_ms"] is None and fraction >= 0.5:
            result["t50_ms"] = round((index - start + 1) * DT * 1000.0, 1)
        if fraction >= 0.9:
            result["t90_ms"] = round((index - start + 1) * DT * 1000.0, 1)
            break
    return result


def run_preset(repo: Path, preset: dict, verbose: bool, trace_frames: int = 0) -> dict:
    engine_defaults = bool(preset.get("engine_defaults", True))
    runtime = rs.ExternalSOMACharacter(repo, rs.InputFrame(flags=rs.FLAG_RESET), False, False, engine_defaults)
    applied = rs.apply_response_settings(runtime.character, preset.get("settings"))
    token_patches = rs.apply_token_horizon(
        runtime.character,
        preset.get("tokens"),
        position_scale=preset.get("position_scale"),
        transition_early_by_frames=preset.get("early"),
    )
    position = np.zeros(3, dtype=np.float32)
    forward = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    speeds: list[float] = []
    wall_ms: list[float] = []
    chunks_ms: list[float] = []
    generations = [0]
    previous = [None]

    def step(speed: float) -> None:
        velocity = forward * speed
        if speed > 0.0:
            position[:] = position + velocity * DT
        frame = rs.InputFrame(
            flags=rs.FLAG_RESET if not speeds and previous[0] is None else 0,
            delta_time=DT,
            walk_speed=WALK_SPEED,
            run_speed=RUN_SPEED,
            position=position.copy(),
            rotation=np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float32),
            velocity=velocity,
        )
        started = time.perf_counter()
        runtime.step(frame)
        wall_ms.append((time.perf_counter() - started) * 1000.0)
        try:
            chunks_ms.append(float(runtime.character.player.playback_duration) * 1000.0)
        except Exception:
            pass
        ground = np.asarray(runtime.character.character_ground_position, dtype=np.float64)
        if previous[0] is not None:
            speeds.append(float(np.linalg.norm((ground - previous[0])[:2])) / DT)
        previous[0] = ground
        generations[0] = runtime.character.generation_count
        if verbose and (trace_frames <= 0 or len(speeds) <= trace_frames):
            desired = float(np.linalg.norm(runtime.character.movement.desired_velocity))
            try:
                chunk = float(runtime.character.player.playback_duration) * 1000.0
                remaining = float(runtime.character.time_to_transition()) * 1000.0
                source = runtime.character._playback_source.value
            except Exception:
                chunk = remaining = float("nan")
                source = -1
            print(
                f"  {len(speeds):4d} t={len(speeds) * DT:5.2f} cmd={speed:4.1f} "
                f"desired={desired:5.2f} model={(speeds[-1] if speeds else 0.0):5.2f} "
                f"chunk={chunk:6.0f} rem={remaining:6.0f} src={source}",
                flush=True,
            )

    try:
        for _ in range(IDLE_FRAMES):
            step(0.0)
        walk_start = len(speeds)
        for _ in range(MOVE_FRAMES):
            step(WALK_SPEED)
        run_start = len(speeds)
        for _ in range(MOVE_FRAMES):
            step(RUN_SPEED)
        walk_end = walk_start + MOVE_FRAMES
        steady_walk = float(np.mean(speeds[walk_end - 20:walk_end]))
        steady_run = float(np.mean(speeds[-20:]))
        wall = np.array(wall_ms)
        return {
            "settings": applied or "engine-defaults",
            "tokens": preset.get("tokens"),
            "token_patches": token_patches,
            "chunk_ms_p50": round(float(np.percentile(chunks_ms, 50)), 1) if chunks_ms else None,
            "idle_to_walk": ramp_metrics(speeds, walk_start, WALK_SPEED),
            "walk_to_run": ramp_metrics(speeds, run_start, RUN_SPEED),
            "steady_walk_mps": round(steady_walk, 3),
            "steady_run_mps": round(steady_run, 3),
            "generations": generations[0],
            "generations_per_s": round(generations[0] / (len(speeds) * DT), 2),
            "step_wall_ms_p50": round(float(np.percentile(wall, 50)), 2),
            "step_wall_ms_p95": round(float(np.percentile(wall, 95)), 2),
        }
    finally:
        runtime.close()


def main() -> None:
    verbose = "--verbose" in sys.argv
    only = None
    trace_frames = 0
    for index, value in enumerate(sys.argv):
        if value == "--preset" and index + 1 < len(sys.argv):
            only = sys.argv[index + 1]
        if value == "--trace-frames" and index + 1 < len(sys.argv):
            trace_frames = int(sys.argv[index + 1])
    repo = Path(r"C:/Projects/motion_bricks/motionbricks")
    report = {}
    for name, preset in PRESETS.items():
        if only is not None and name != only:
            continue
        print(f"[{name}]", flush=True)
        report[name] = run_preset(repo, preset, verbose, trace_frames)
        print(json.dumps(report[name], ensure_ascii=False), flush=True)
    print(json.dumps({"status": "OK", "presets": report}, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()
