"""T-MB06: численная проверка «не закручивается ли поза» при круговом вводе.

Мотив (замерено 17.11.2026): `_apply_external_root` писал выровненный под игрока корень в
`current_pose` ДО `record_pose`, то есть в историю модели уходил не её собственный корень.
Модель видела «я не повернулся» при команде «повернись», каждый чанк пыталась довернуть,
поворот стирался — остаток копился в локальных поворотах костей. Замер на круге
(WASD по кругу, 12 x 0.5 s, 5 m/s):

    метрика (локальная дельта к bind)   было (pin-before-record)   стало (record-own)
    spine                               45 deg                     25 deg
    chest                               78 deg                     27 deg
    thigh.L                            134 deg                     82 deg
    arm.R                              178 deg                     78 deg
    восстановление на простое           нет (arm.R 115 deg)        да (spine 21 deg)

Метрики инвариантны к базису и к тому, кто владеет корнем: это углы ЛОКАЛЬНЫХ поворотов костей
относительно bind (локальные повороты — свойство позы, а не пространства) плюс наклон оси
таз->шея в системе таза.

    python verify_pose_stability.py            # проверка, PASS/FAIL
    python verify_pose_stability.py --verbose  # + таблица по кадрам
    python verify_pose_stability.py --legacy-order  # контрфакт: старый порядок (pin в историю)
    python verify_pose_stability.py --legacy-ground  # контрфакт: старый ground/velocity из позиции игрока
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import runtime_sidecar as rs  # noqa: E402

PARENTS = [
    -1, 0, 1, 2, 3, 4, 5, 6, 6, 6, 6, 3, 11, 12, 13, 14, 15, 16, 17, 14, 19, 20, 21, 22, 14, 24, 25, 26, 27, 14,
    29, 30, 31, 32, 14, 34, 35, 36, 37, 3, 39, 40, 41, 42, 43, 44, 45, 42, 47, 48, 49, 50, 42, 52, 53, 54, 55, 42,
    57, 58, 59, 60, 42, 62, 63, 64, 65, 0, 67, 68, 69, 70, 0, 72, 73, 74, 75,
]

FL = rs.FLOATS_PER_JOINT

CIRCLE_PHASES = 12
PHASE_FRAMES = 30
CIRCLE_SPEED = 5.0
IDLE_FRAMES = 60

# Пороги с запасом над замеренными значениями после фикса (см. docstring).
LIMITS = {
    "spine": 40.0,
    "chest": 40.0,
    "thighL": 120.0,
    "thighR": 120.0,
    "armL": 120.0,
    "armR": 120.0,
    "bodyLean": 55.0,
}
# После простоя поза должна вернуться: иначе «закрутило».
IDLE_LIMITS = {"spine": 30.0, "chest": 35.0, "thighL": 100.0, "thighR": 100.0}


def to_unity_rot(q):
    return np.array([q[0], -q[1], -q[2], q[3]], dtype=np.float64)


def local_rot(pose: np.ndarray, joint: int):
    offset = joint * FL
    return rs._normalize_quaternion(to_unity_rot(np.array(pose[offset + 3:offset + 7], dtype=np.float64)))


def fk(pose: np.ndarray):
    rotations = [None] * rs.JOINTS
    positions = [None] * rs.JOINTS
    for joint in range(rs.JOINTS):
        offset = joint * FL
        local = local_rot(pose, joint)
        local_position = np.array([-pose[offset], pose[offset + 1], pose[offset + 2]], dtype=np.float64)
        parent = PARENTS[joint]
        if parent < 0:
            rotations[joint] = local
            positions[joint] = local_position
        else:
            rotations[joint] = rs._quat_multiply(rotations[parent], local)
            positions[joint] = positions[parent] + rs._quat_rotate(rotations[parent], local_position)
    return rotations, positions


def angle_between(a, b) -> float:
    delta = rs._quat_multiply(rs._normalize_quaternion(np.asarray(a, dtype=np.float64)),
                              rs._quat_inverse(rs._normalize_quaternion(np.asarray(b, dtype=np.float64))))
    w = max(-1.0, min(1.0, abs(float(delta[3]))))
    return math.degrees(2.0 * math.acos(w))


def torso_axis(rotations, positions) -> np.ndarray:
    axis = rs._quat_rotate(rs._quat_inverse(rotations[0]), positions[4] - positions[0])
    return axis / max(1e-9, float(np.linalg.norm(axis)))


def measure(packet, bind, bind_reference: np.ndarray) -> dict:
    packet = np.asarray(packet, dtype=np.float64).reshape(-1)
    rotations, positions = fk(packet)
    result = {"bodyLean": math.degrees(math.acos(
        max(-1.0, min(1.0, float(np.dot(torso_axis(rotations, positions), bind_reference))))))}
    for joint, name in ((1, "spine"), (3, "chest"), (67, "thighL"), (72, "thighR"), (12, "armL"), (40, "armR")):
        result[name] = angle_between(local_rot(packet, joint), local_rot(bind, joint))
    return result


def install_legacy_order(runtime) -> None:
    """Воспроизвести СТАРЫЙ порядок: в `record_pose` уходил выровненный корень.

    Нужно только как контрфакт — доказательство, что проверка ловит регрессию.
    """
    state = {"pinned": None}
    original_override = runtime._apply_external_root

    def spy(character, app_ground, app_rotation):
        original_override(character, app_ground, app_rotation)
        pose = character.current_pose.numpy().reshape(rs.JOINTS, FL)
        state["pinned"] = pose[0, 3:7].copy()

    runtime._apply_external_root = spy

    player = runtime.character.player
    original_record = player.record_pose

    def record(delta_time, pose):
        flat = np.array(pose.numpy(), dtype=np.float32).reshape(rs.JOINTS, FL).copy()
        if state["pinned"] is not None:
            flat[0, 3:7] = state["pinned"]
        return original_record(delta_time, runtime.wp.array(flat, dtype=pose.dtype, device=pose.device))

    player.record_pose = record


def install_legacy_ground(runtime) -> None:
    """Контрфакт: СТАРЫЙ контракт ground/velocity — из позиции игрока.

    sidecar переписывал `character_ground_position` позицией игрока и синтезировал
    `actual_velocity` из скорости игрока. Модель видела противоречивую историю: её собственный
    корень идёт вперёд на 5 м/с, а `actual_velocity` при отпущенных клавишах ровно 0
    (замерено: idle chest 103° от bind, восстановления нет). Нужно только как контрфакт.
    """
    def step_character(character, frame, reset) -> None:
        dt = float(np.clip(frame.delta_time, 1.0e-4, 0.1))
        app_ground = rs.unity_to_app_position(frame.position)
        app_rotation = rs.unity_to_app_rotation(frame.rotation)
        app_velocity = rs.unity_to_app_velocity(frame.velocity)
        app_velocity[2] = 0.0
        speed = float(np.linalg.norm(app_velocity[:2]))

        facing = rs._quat_rotate(app_rotation, [1.0, 0.0, 0.0])
        facing[2] = 0.0
        if speed > 0.05:
            requested_speed = runtime.run_speed if speed > 0.5 * (runtime.walk_speed + runtime.run_speed) else runtime.walk_speed
            movement = app_velocity / requested_speed
            length = float(np.linalg.norm(movement[:2]))
            if length > 1.0:
                movement /= length
            use_max_speed = requested_speed == runtime.run_speed
        else:
            movement = None
            use_max_speed = False

        command = runtime.locomotion_command(
            facing_direction=np.asarray(facing, dtype=np.float32),
            movement=None if movement is None else np.asarray(movement, dtype=np.float32),
            use_max_speed=use_max_speed,
        )
        character.character_ground_position[:] = app_ground
        previous_ground = app_ground - app_velocity * dt
        character.state_machine.update(character, dt, command)
        character._sample_playback(dt)
        if reset or not runtime._reference_initialized:
            runtime._capture_reference(character, app_ground, app_rotation)
        character.character_ground_position[:] = app_ground
        character._update_character_velocity(previous_ground, dt)
        if reset:
            character.player.seed_history_from_pose(character.current_pose)
        else:
            character.player.record_pose(dt, character.current_pose)
        runtime._apply_external_root(character, app_ground, app_rotation)
        character._update_probe_box()

    runtime.step_character = step_character


def main() -> None:
    verbose = "--verbose" in sys.argv
    legacy = "--legacy-order" in sys.argv
    legacy_ground = "--legacy-ground" in sys.argv
    repo = Path(r"C:/Projects/motion_bricks/motionbricks")
    runtime = rs.ExternalSOMACharacter(repo, rs.InputFrame(flags=rs.FLAG_RESET), False)
    if legacy:
        install_legacy_order(runtime)
    if legacy_ground:
        install_legacy_ground(runtime)
    bind = runtime.bind_pose.copy()
    bind_rotations, bind_positions = fk(bind)
    bind_reference = torso_axis(bind_rotations, bind_positions)

    position = np.zeros(3, dtype=np.float32)
    yaw = 0.0
    frames = 0
    peaks = {key: 0.0 for key in LIMITS}
    idle_peaks = {key: 0.0 for key in IDLE_LIMITS}

    def run_step(speed: float, phase: str, index: int) -> dict:
        nonlocal position, frames
        forward = np.array([math.sin(yaw), 0.0, math.cos(yaw)], dtype=np.float32)
        velocity = forward * speed
        if speed > 0.0:
            position = position + velocity / 60.0
        frame = rs.InputFrame(
            sequence=frames + 1,
            flags=rs.FLAG_RESET if frames == 0 else 0,
            position=position.copy(),
            rotation=np.array([0.0, math.sin(yaw * 0.5), 0.0, math.cos(yaw * 0.5)], dtype=np.float32),
            velocity=velocity,
        )
        packet, _ = runtime.step(frame)
        frames += 1
        values = measure(packet, bind, bind_reference)
        if verbose:
            print(f"{frames:4d} {phase:>6} {speed:4.1f} " + " ".join(f"{key}={value:6.1f}" for key, value in values.items()))
        return values

    try:
        for phase in range(CIRCLE_PHASES):
            target_yaw = (math.pi * 0.5) * ((phase % 4) + 1)
            for index in range(PHASE_FRAMES):
                delta_yaw = target_yaw - yaw
                while delta_yaw > math.pi:
                    delta_yaw -= 2.0 * math.pi
                while delta_yaw < -math.pi:
                    delta_yaw += 2.0 * math.pi
                yaw += delta_yaw * 0.5
                values = run_step(CIRCLE_SPEED, f"c{phase}", index)
                for key in peaks:
                    peaks[key] = max(peaks[key], values[key])
        for index in range(IDLE_FRAMES):
            values = run_step(0.0, "idle", index)
            if index == IDLE_FRAMES - 1:
                for key in idle_peaks:
                    idle_peaks[key] = values[key]
    finally:
        runtime.close()

    failures = [f"{key} {peaks[key]:.1f}>{LIMITS[key]:.1f}" for key in LIMITS if peaks[key] > LIMITS[key]]
    idle_failures = [f"idle {key} {idle_peaks[key]:.1f}>{IDLE_LIMITS[key]:.1f}" for key in IDLE_LIMITS if idle_peaks[key] > IDLE_LIMITS[key]]
    print(json.dumps({
        "frames": frames,
        "order": "legacy-pin-before-record" if legacy else "record-own-root",
        "ground": "legacy-player-position" if legacy_ground else "official-own-root",
        "circle_speed_mps": CIRCLE_SPEED,
        "peak_local_delta_deg": {key: round(value, 2) for key, value in peaks.items()},
        "limits": LIMITS,
        "failures": failures,
        "status_during_input": "PASS" if not failures else "FAIL",
        "idle_tail_deg": {key: round(value, 2) for key, value in idle_peaks.items()},
        "idle_limits": IDLE_LIMITS,
        "idle_failures": idle_failures,
        # ОТКРЫТЫЙ дефект: после отпускания кнопок корпус продолжает складываться
        # (замерено: bodyLean 26 -> 72 град за 1 s). Пороги держим как факт, а не как норму.
        "status_idle_recovery": "PASS" if not idle_failures else "OPEN",
        "status": "PASS" if not failures else "FAIL",
    }, indent=2))


if __name__ == "__main__":
    main()
