#!/usr/bin/env python3
"""T-MB06: official MotionBricks SOMA runtime behind a localhost UDP bridge."""

from __future__ import annotations

import argparse
from dataclasses import dataclass, replace
import json
import math
from pathlib import Path
import socket
import struct
import sys
import threading
import time
from collections import Counter

import numpy as np


MAGIC = b"MBR1"
VERSION = 1
PACKET_POSE = 1
PACKET_BIND = 2
PACKET_ERROR = 3
FLAG_RESET = 1
FLAG_SHUTDOWN = 2
JOINTS = 77
FLOATS_PER_JOINT = 7
POSE_FLOATS = JOINTS * FLOATS_PER_JOINT
REQUEST = struct.Struct("<4sBBHI13f")
RESPONSE = struct.Struct("<4sBBHIiiI6f")
POSE_BYTES = POSE_FLOATS * 4
CONTACT_BYTES = 4 * 4

PLAYBACK_SOURCE = {"generated": 0, "clip_transition": 1, "clip": 2}


# Движок ведёт модель по **сглаженной копии** нашей скорости, а не по ней самой.
# LocomotionSettings по умолчанию: speed_acceleration_halflife = 0.6 с (полураспад!),
# speed_deceleration 0.4 с, velocity_direction 0.2 с. Через 0.6 с после Shift модель проходит
# только половину пути 5→10 м/с и всё это время показывает промежуточный шаг, в то время
# как CharacterController уже бежит — это и читается как «бег проигрывается как хотьба»
# и «задержка 1+ с».
# Плюс новый чанк запрашивается только на конце чанка, при повороте больше 23°
# (direction_regeneration_dot 0.92) или смене скорости больше 30% диапазона
# (speed_regeneration_fraction 0.3 / minimum 0.1): всё, что мельче, ждёт до 0.9 с.
RESPONSIVE_SETTINGS = {
    "facing_halflife": 0.06,
    "velocity_direction_halflife": 0.06,
    "speed_acceleration_halflife": 0.10,
    "speed_deceleration_halflife": 0.10,
    "speed_regeneration_fraction": 0.05,
    "minimum_speed_regeneration_fraction": 0.02,
    "direction_regeneration_dot": 0.995,
}


def apply_response_settings(character, enabled: bool = True) -> dict:
    """Подтянуть отклик модели к скорости CharacterController.

    `LocomotionSettings` — frozen dataclass, поэтому пересобираем через `replace` и
    кладём обратно в state machine. Возвращает применённое (для лога), чтобы в
    приёмке было видно, с какими числами гонялись.
    """

    if not enabled:
        return {}
    from dataclasses import replace

    # settings живут не на StateMachine, а на каждом LocomotionState
    # (state_machine.standing_locomotion_state / crouching_locomotion_state).
    patched = []
    for attribute in ("standing_locomotion_state", "crouching_locomotion_state"):
        state = getattr(character.state_machine, attribute, None)
        if state is None or not hasattr(state, "settings"):
            continue
        state.settings = replace(state.settings, **RESPONSIVE_SETTINGS)
        patched.append(attribute)
    if not patched:
        raise RuntimeError("LocomotionState not found; response settings were not applied")
    return dict(RESPONSIVE_SETTINGS)


@dataclass(slots=True)
class InputFrame:
    sequence: int = 0
    flags: int = 0
    delta_time: float = 1.0 / 60.0
    walk_speed: float = 5.0
    run_speed: float = 10.0
    position: np.ndarray = None
    rotation: np.ndarray = None
    velocity: np.ndarray = None

    def __post_init__(self) -> None:
        if self.position is None:
            self.position = np.zeros(3, dtype=np.float32)
        if self.rotation is None:
            self.rotation = np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float32)
        if self.velocity is None:
            self.velocity = np.zeros(3, dtype=np.float32)


def _normalize_quaternion(value) -> np.ndarray:
    result = np.asarray(value, dtype=np.float64)
    length = float(np.linalg.norm(result))
    if not math.isfinite(length) or length < 1.0e-8:
        return np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float64)
    return result / length


def _quat_multiply(left, right) -> np.ndarray:
    lx, ly, lz, lw = _normalize_quaternion(left)
    rx, ry, rz, rw = _normalize_quaternion(right)
    return _normalize_quaternion(
        [
            lw * rx + lx * rw + ly * rz - lz * ry,
            lw * ry - lx * rz + ly * rw + lz * rx,
            lw * rz + lx * ry - ly * rx + lz * rw,
            lw * rw - lx * rx - ly * ry - lz * rz,
        ]
    )


def _quat_inverse(value) -> np.ndarray:
    x, y, z, w = _normalize_quaternion(value)
    return np.array([-x, -y, -z, w], dtype=np.float64)


def _quat_rotate(value, vector) -> np.ndarray:
    q = _normalize_quaternion(value)
    v = np.asarray(vector, dtype=np.float64)
    xyz = q[:3]
    return v + 2.0 * np.cross(xyz, np.cross(xyz, v) + q[3] * v)


def unity_to_app_position(value) -> np.ndarray:
    x, y, z = np.asarray(value, dtype=np.float64)
    return np.array([z, -x, y], dtype=np.float64)


def unity_to_app_rotation(value) -> np.ndarray:
    x, y, z, w = _normalize_quaternion(value)
    mirrored = np.array([x, -y, -z, w], dtype=np.float64)
    basis = np.array([0.5, 0.5, 0.5, 0.5], dtype=np.float64)
    return _quat_multiply(basis, _quat_multiply(mirrored, _quat_inverse(basis)))


def unity_to_app_velocity(value) -> np.ndarray:
    return unity_to_app_position(value)


def response_header(
    packet_type: int,
    sequence: int,
    runtime: "ExternalSOMACharacter | None" = None,
) -> bytes:
    if runtime is None:
        source = 0
        generations = 0
        metrics = (0.0,) * 6
        timeline = 0
    else:
        source = PLAYBACK_SOURCE[runtime.character._playback_source.value]
        generations = runtime.character.generation_count
        stages = runtime.character.last_stages
        metrics = (
            runtime.character.last_wall_ms,
            stages.get("encode", 0.0),
            stages.get("inference", 0.0),
            stages.get("decode", 0.0),
            stages.get("post_process", 0.0),
            runtime.last_step_ms,
        )
        timeline = _timeline_field(runtime.character)
    return RESPONSE.pack(
        MAGIC,
        VERSION,
        packet_type,
        0,
        sequence,
        source,
        generations,
        timeline,
        *metrics,
    )


def _timeline_field(character) -> int:
    """Длина текущего чанка и остаток до точки перехода — в зарезервированном uint32.

    Формат пакета не меняется (поле уже было в структуре и клиент его пропускал):
    младшие 16 бит — `playback_duration` в мс, старшие 16 бит — `time_to_transition` в мс.
    Именно остаток задаёт, через сколько модель сможет отреагировать на новую команду:
    проигрывается уже сгенерированный чанк, и раньше точки перехода генерации не будет.
    """

    try:
        duration = float(character.player.playback_duration)
        remaining = float(character.time_to_transition())
    except Exception:
        return 0
    chunk = int(round(max(0.0, min(duration, 65.5)) * 1000.0)) & 0xFFFF
    left = int(round(max(0.0, min(remaining, 65.5)) * 1000.0)) & 0xFFFF
    return (left << 16) | chunk


class ExternalSOMACharacter:
    def __init__(self, repo: Path, initial: InputFrame, fp16: bool = False, responsive: bool = True) -> None:
        repo = repo.resolve()
        if str(repo) not in sys.path:
            sys.path.insert(0, str(repo))

        import warp as wp
        from motionbricks.inference.builder import TensorRTBuildOptions
        from motionbricks.model.contract import ModelRig
        from motionbricks.model.soma.contract import SOMA_N1_FP16_ONNX_PATH, SOMA_N1_FP32_ONNX_PATH
        from motionbricks.model.soma.engine import MotionBricksSOMAEngine
        from app.runtime.common.io.smart_primitive_document import SmartPrimitiveAssetPool
        from app.runtime.common.motion import LocomotionCommand
        from app.runtime.common.rig import load_rig_assets
        from app.runtime.soma.character import SOMACharacter
        from app.runtime.soma.presets import (
            CROUCH_LOCOMOTION,
            DIRECTIONAL_LOCOMOTION,
            LINEAR_LOCOMOTION,
        )

        self.wp = wp
        self.locomotion_command = LocomotionCommand
        self.repo = repo
        self.walk_speed = initial.walk_speed
        self.run_speed = initial.run_speed

        started = time.perf_counter()
        wp.init()
        wp.set_device("cuda:0")
        assets = load_rig_assets(ModelRig.SOMA)
        self.model_to_runtime = assets.model_to_runtime
        self.runtime_to_model = wp.transform_inverse(self.model_to_runtime)
        self.bind_pose = np.ascontiguousarray(assets.reference_pose.numpy(), dtype=np.float32).reshape(-1)
        if self.bind_pose.size != POSE_FLOATS:
            raise RuntimeError(f"SOMA bind has {self.bind_pose.size} floats, expected {POSE_FLOATS}")

        asset_pool = SmartPrimitiveAssetPool(
            assets.clip_registry,
            assets.reference_pose,
            assets.model_to_runtime,
        )
        engine = MotionBricksSOMAEngine(
            SOMA_N1_FP16_ONNX_PATH if fp16 else SOMA_N1_FP32_ONNX_PATH,
            model_to_runtime=assets.model_to_runtime,
            build_options=TensorRTBuildOptions(enable_fp16=fp16),
            cache_dir=repo / ".trt-cache",
        )
        self.engine = engine
        self.engine_init_ms = (time.perf_counter() - started) * 1000.0

        external = self

        class Character(SOMACharacter):
            def __init__(self, **kwargs) -> None:
                self.generation_count = 0
                self.wall_times_ms = []
                self.last_wall_ms = 0.0
                self.last_stages = {}
                super().__init__(**kwargs)

            def generate_to_target_poses(self, target_poses, inference_params, playback_source=None) -> None:
                generation_started = time.perf_counter()
                if playback_source is None:
                    super().generate_to_target_poses(target_poses, inference_params)
                else:
                    super().generate_to_target_poses(target_poses, inference_params, playback_source)
                self.last_wall_ms = (time.perf_counter() - generation_started) * 1000.0
                self.wall_times_ms.append(self.last_wall_ms)
                self.generation_count += 1
                self.last_stages = dict(self.engine.timer.milliseconds)

            def external_step(self, frame: InputFrame, reset: bool) -> None:
                external.step_character(self, frame, reset)

        linear = replace(
            LINEAR_LOCOMOTION,
            target_values=(
                ("Idle", (0.0, 0.0)),
                ("Walk", (initial.walk_speed / initial.run_speed, 0.0)),
                ("Jog", ((initial.walk_speed + initial.run_speed) * 0.5 / initial.run_speed, 0.0)),
                ("Run", (1.0, 0.0)),
            ),
            default_speed=initial.walk_speed,
            max_speed=initial.run_speed,
            minimum_speed=min(1.0, initial.walk_speed),
        )
        app_position = unity_to_app_position(initial.position)
        app_rotation = unity_to_app_rotation(initial.rotation)
        initial_transform = wp.transform(
            wp.vec3(*app_position),
            wp.quat(*app_rotation),
        )
        self.character = Character(
            engine=engine,
            asset_pool=asset_pool,
            initial_transform=initial_transform,
            modes=(linear, DIRECTIONAL_LOCOMOTION, CROUCH_LOCOMOTION),
        )
        self.last_step_ms = 0.0
        self.response_settings = apply_response_settings(self.character, responsive)
        self._reference_initialized = False
        # Эталон yaw СОЗНАТЕЛЬНО не хранится: он не должен попадать в корень пакета
        # (`_apply_external_root`), иначе RESET на ненулевом yaw заваливает корпус.
        self._reference_pose_root_rotation = self._bind_reference_root_rotation()
        self._reference_sample_root_z = float(app_position[2])
        self._reference_root_height = 0.0

    def _bind_reference_root_rotation(self) -> np.ndarray:
        """Корень bind в runtime space: `model_to_runtime * bind_root`.

        Официальный проигрыватель пишет joint 0 в ДВУХ разных пространствах:
        `clip.animation.sample()` оставляет model space (её позы идут в inference как
        target poses), а декодер пишет уже runtime space
        (`output_local_poses[frame, 0] = mul(model_to_runtime, hips_transform)`).
        `model_pose()` же всегда снимает `runtime_to_model`, поэтому любой сэмплированный
        эталон даёт постоянную ошибку в Unity: bind-клон — −90° вокруг X (завал на бок),
        клип-кадр — ≈+94° вокруг Y (корпус боком). Anchor на bind+M даёт packet_root == bind,
        то есть delta_root == identity: ориентацию тела задаёт трансформ игрока.
        """
        bind_root = np.asarray(self.bind_pose, dtype=np.float64).reshape(JOINTS, FLOATS_PER_JOINT)[0, 3:7]
        return _quat_multiply(np.asarray(self.model_to_runtime.q, dtype=np.float64), bind_root)

    def _capture_reference(self, character, app_ground: np.ndarray, app_rotation: np.ndarray) -> None:
        """Зафиксировать эталон при сбросе: корневая ориентация и высота.

        Корневая ориентация берётся не из сэмплированной позы, а из bind+M
        (`_bind_reference_root_rotation`): только тогда постоянный множитель root'а
        в Unity-дельте равен identity.

        yaw игрока в эталон НЕ входит — ни в поворот корня, ни в высоту. Раньше здесь
        записывался `app_rotation`, и при RESET на развороте (airborne STOP/START) эталон
        уезжал от bind: `model_pose` снимает `inv(app)`, остаётся `M^-1 * inv(ref) * M * bind`,
        т.е. постоянный поворот корня — персонаж задом наперёд.
        """
        pose = character.current_pose.numpy().reshape(JOINTS, FLOATS_PER_JOINT)
        self._reference_pose_root_rotation = self._bind_reference_root_rotation()
        self._reference_sample_root_z = float(pose[0, 2])
        # SOMA current_pose: root XY — app-ground, Z — высота относительно ground.
        self._reference_root_height = float(pose[0, 2])
        self._reference_initialized = True

    def _apply_external_root(self, character, app_ground: np.ndarray, app_rotation: np.ndarray) -> None:
        pose = character.current_pose.numpy().reshape(JOINTS, FLOATS_PER_JOINT).copy()
        sampled_root_z = float(pose[0, 2])
        # Контракт: корень в пакете обязан быть равен `model_to_runtime * bind_root` при ЛЮБОМ
        # yaw игрока. `model_pose` снимает `inv(app_rotation)`, поэтому сюда кладём
        # `app_rotation * M * bind` — на выходе остаётся ровно `M^-1 * M * bind`.
        corrected_rotation = _quat_multiply(app_rotation, self._reference_pose_root_rotation)
        pose[0, 0:2] = app_ground[0:2]
        pose[0, 2] = self._reference_root_height + sampled_root_z - self._reference_sample_root_z
        pose[0, 3:7] = corrected_rotation
        character.current_pose.assign(np.ascontiguousarray(pose, dtype=np.float32))

    def step_character(self, character, frame: InputFrame, reset: bool) -> None:
        started = time.perf_counter()
        dt = float(np.clip(frame.delta_time, 1.0e-4, 0.1))
        app_ground = unity_to_app_position(frame.position)
        app_rotation = unity_to_app_rotation(frame.rotation)
        app_velocity = unity_to_app_velocity(frame.velocity)
        app_velocity[2] = 0.0
        speed = float(np.linalg.norm(app_velocity[:2]))

        capture_reference = reset or not self._reference_initialized

        facing = _quat_rotate(app_rotation, [1.0, 0.0, 0.0])
        facing[2] = 0.0
        if speed > 0.05:
            requested_speed = self.run_speed if speed > 0.5 * (self.walk_speed + self.run_speed) else self.walk_speed
            movement = app_velocity / requested_speed
            length = float(np.linalg.norm(movement[:2]))
            if length > 1.0:
                movement /= length
            use_max_speed = requested_speed == self.run_speed
        else:
            movement = None
            use_max_speed = False

        command = self.locomotion_command(
            facing_direction=np.asarray(facing, dtype=np.float32),
            movement=None if movement is None else np.asarray(movement, dtype=np.float32),
            use_max_speed=use_max_speed,
        )
        character.state_machine.update(character, dt, command)
        # Как в официальном `Character.step()`: previous_ground снимается ДО сэмпла, а
        # `_update_character_velocity` считает скорость по собственному корню модели
        # (её `_update_character_position` внутри сэмпла).
        #
        # Раньше здесь стояло `character_ground_position = app_ground` и velocity, синтезированная
        # из скорости игрока. Модель видела противоречивую историю: собственный корень идёт вперёд
        # на 5 м/с, а `actual_velocity` при отпущенных клавишах — ровно 0. Замер на круге
        # (12 x 0.5 s, 5 м/с) + 1 s простоя: chest 103° от bind и НЕ восстанавливается
        # («отпускаешь кнопки — раскручивает»); с собственным velocity — chest 8.7° и
        # восстановление за те же 1 s. Мир по-прежнему принадлежит игроку: позицию в Unity
        # задаёт только `_apply_external_root`, ниже.
        previous_ground = character.character_ground_position.copy()
        character._sample_playback(dt)

        # Эталон корня фиксирован на bind+M (см. `_bind_reference_root_rotation`), поэтому
        # положение и высоту сброса можно снимать из любой позы: сам root берётся не отсюда.
        if capture_reference:
            self._capture_reference(character, app_ground, app_rotation)

        # Порядок важен: в историю модели (`record_pose`) должен уйти ЕЁ СОБСТВЕННЫЙ корень,
        # как в официальном `Character.step()` (sample → record). Если записать туда корень,
        # выровненный под игрока, модель видит «я не повернулся» при команде «повернись»:
        # каждый чанк она пытается довернуть, поворот стирается, остаток копится в позе
        # (персонаж «закручивается как dummy на нитке», а на простое раскручивается обратно).
        # Выравнивание корня применяем ПОСЛЕ записи — только к позе, которую отдаём в Unity.
        character._update_character_velocity(previous_ground, dt)
        if reset:
            character.player.seed_history_from_pose(character.current_pose)
        else:
            character.player.record_pose(dt, character.current_pose)
        self._apply_external_root(character, app_ground, app_rotation)
        character._update_probe_box()
        self.last_step_ms = (time.perf_counter() - started) * 1000.0

    def step(self, frame: InputFrame):
        reset = bool(frame.flags & FLAG_RESET)
        self.character.external_step(frame, reset)
        return self.model_pose(frame)

    def model_pose(self, frame: InputFrame) -> tuple[np.ndarray, np.ndarray]:
        wp = self.wp
        character = self.character
        values = character.current_pose.numpy().reshape(JOINTS, FLOATS_PER_JOINT).copy()
        app_ground = unity_to_app_position(frame.position)
        app_rotation = unity_to_app_rotation(frame.rotation)
        local_app_root = wp.transform(
            wp.vec3(
                float(values[0, 0] - app_ground[0]),
                float(values[0, 1] - app_ground[1]),
                float(values[0, 2]),
            ),
            wp.quat(*_quat_multiply(_quat_inverse(app_rotation), values[0, 3:7])),
        )
        model_root = wp.mul(self.runtime_to_model, local_app_root)
        values[0, 0:3] = np.asarray(model_root.p, dtype=np.float32)
        values[0, 3:7] = _normalize_quaternion(model_root.q)
        contacts = character.current_foot_contacts.numpy().astype(np.float32)
        return np.ascontiguousarray(values, dtype=np.float32), np.ascontiguousarray(contacts)

    def close(self) -> None:
        self.engine.synchronize()
        self.engine.close()


def parse_request(data: bytes) -> InputFrame:
    if len(data) != REQUEST.size:
        raise ValueError(f"request is {len(data)} bytes, expected {REQUEST.size}")
    unpacked = REQUEST.unpack(data)
    if unpacked[0] != MAGIC or unpacked[1] != VERSION:
        raise ValueError("request magic/version mismatch")
    values = unpacked[5:]
    return InputFrame(
        sequence=unpacked[4],
        flags=unpacked[2],
        delta_time=values[0],
        walk_speed=values[1],
        run_speed=values[2],
        position=np.asarray(values[3:6], dtype=np.float32),
        rotation=np.asarray(values[6:10], dtype=np.float32),
        velocity=np.asarray(values[10:13], dtype=np.float32),
    )


def pack_request(frame: InputFrame) -> bytes:
    return REQUEST.pack(
        MAGIC,
        VERSION,
        frame.flags,
        0,
        frame.sequence,
        frame.delta_time,
        frame.walk_speed,
        frame.run_speed,
        *frame.position,
        *frame.rotation,
        *frame.velocity,
    )


def percentile(values: list[float], fraction: float) -> float:
    return float(np.percentile(np.asarray(values, dtype=np.float64), fraction * 100.0)) if values else 0.0


def metrics_summary(runtime: ExternalSOMACharacter, frames: int, sources: Counter) -> dict:
    times = runtime.character.wall_times_ms
    return {
        "engine_init_ms": round(runtime.engine_init_ms, 3),
        "frames": frames,
        "generation_count": runtime.character.generation_count,
        "inference_wall_ms": {
            "last": round(times[-1], 3) if times else 0.0,
            "mean": round(float(np.mean(times)), 3) if times else 0.0,
            "p50": round(percentile(times, 0.50), 3),
            "p95": round(percentile(times, 0.95), 3),
            "max": round(max(times), 3) if times else 0.0,
        },
        "last_cuda_stages_ms": {key: round(float(value), 3) for key, value in runtime.character.last_stages.items()},
        "playback_sources": dict(sources),
        "request_bytes": REQUEST.size,
        "pose_packet_bytes": RESPONSE.size + POSE_BYTES + CONTACT_BYTES,
    }


def run_self_test(repo: Path, fp16: bool) -> dict:
    frame = InputFrame(flags=FLAG_RESET)
    runtime = ExternalSOMACharacter(repo, frame, fp16)
    frames = 0
    sources = Counter()
    try:
        phases = (
            (1.0, 0.0, 0.0),
            (2.0, 5.0, 0.0),
            (2.0, 10.0, 0.0),
            (1.5, 10.0, math.pi * 0.5),
            (1.5, 0.0, math.pi * 0.5),
        )
        position = np.zeros(3, dtype=np.float32)
        for duration, speed, target_yaw in phases:
            steps = round(duration * 60.0)
            for index in range(steps):
                yaw = target_yaw if speed == 0.0 else target_yaw * min(1.0, (index + 1) / 30.0)
                direction = np.array([math.sin(yaw), 0.0, math.cos(yaw)], dtype=np.float32)
                velocity = direction * speed
                position += velocity / 60.0
                frame = InputFrame(
                    sequence=frames + 1,
                    flags=FLAG_RESET if frames == 0 else 0,
                    position=position.copy(),
                    rotation=np.array([0.0, math.sin(yaw * 0.5), 0.0, math.cos(yaw * 0.5)], dtype=np.float32),
                    velocity=velocity,
                )
                pose, contacts = runtime.step(frame)
                if not np.isfinite(pose).all() or not np.isfinite(contacts).all():
                    raise RuntimeError(f"non-finite output at self-test frame {frames}")
                sources[runtime.character._playback_source.value] += 1
                frames += 1
        result = metrics_summary(runtime, frames, sources)
        if result["generation_count"] <= 0:
            raise RuntimeError("self-test did not invoke TensorRT generation")
        result["status"] = "PASS"
        return result
    finally:
        runtime.close()


MAX_CATCH_UP_FRAMES = 8
# Legacy-режим (до fix 17.13): из пачки пришедших запросов обрабатывался только новейший,
# остальные ВЫБРАСЫВАЛИСЬ вместе с их dt. Модель шагала несколько раз в секунду, а не каждый
# кадр игрока, поэтому её внутреннее время отставало от реального. Оставлено для A/B в
# `verify_udp_pacing.py`.
LATEST_ONLY_DRAIN = False


def take_burst(sock: socket.socket, data: bytes, address) -> list[tuple[bytes, object]]:
    """Забрать из сокета всё, что уже пришло, в порядке прихода (не больше MAX_CATCH_UP_FRAMES)."""
    pending = [(data, address)]
    sock.setblocking(False)
    try:
        while len(pending) < MAX_CATCH_UP_FRAMES:
            try:
                pending.append(sock.recvfrom(4096))
            except BlockingIOError:
                break
    finally:
        sock.setblocking(True)
        sock.settimeout(1.0)
    if LATEST_ONLY_DRAIN:
        return pending[-1:]
    return pending


def serve(repo: Path, host: str, port: int, fp16: bool, responsive: bool = True) -> None:
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((host, port))
    sock.settimeout(1.0)
    print(f"SIDECAR_LISTENING {host}:{port}", flush=True)
    runtime = None
    client_address = None
    bind_sent = False
    try:
        while True:
            try:
                data, address = sock.recvfrom(4096)
            except socket.timeout:
                continue
            # Очередь — В ПОРЯДКЕ, каждый кадр со своим dt: тогда внутреннее время модели
            # совпадает с реальным (в логе `received` было 8,1/s при `sent` 14,6/s, и время
            # терялось почти вдвое). Бюджет шага (p50 2,3 ms) много меньше кадра, так что
            # догоняющий цикл не растёт; MAX_CATCH_UP_FRAMES ограничивает залповый разбор.
            closing = False
            for data, address in take_burst(sock, data, address):
                client_address = address
                frame = parse_request(data)
                if frame.flags & FLAG_SHUTDOWN:
                    print("SIDECAR_SHUTDOWN", flush=True)
                    closing = True
                    break
                if runtime is None:
                    runtime = ExternalSOMACharacter(repo, frame, fp16, responsive)
                    print(f"ENGINE_READY init_ms={runtime.engine_init_ms:.1f} fp16={fp16}", flush=True)
                    if runtime.response_settings:
                        print("RESPONSE " + " ".join(f"{k}={v}" for k, v in runtime.response_settings.items()), flush=True)
                    else:
                        print("RESPONSE engine-defaults", flush=True)
                if (frame.flags & FLAG_RESET) or not bind_sent:
                    sock.sendto(response_header(PACKET_BIND, frame.sequence, runtime) + runtime.bind_pose.tobytes(), address)
                    bind_sent = True
                pose, contacts = runtime.step(frame)
                packet = (
                    response_header(PACKET_POSE, frame.sequence, runtime)
                    + pose.tobytes()
                    + contacts.tobytes()
                )
                sock.sendto(packet, address)
            if closing:
                break
    except Exception as exc:
        if client_address is not None:
            message = f"{type(exc).__name__}: {exc}".encode("utf-8", errors="replace")[:60000]
            try:
                sock.sendto(response_header(PACKET_ERROR, 0, runtime) + message, client_address)
            except OSError:
                pass
        print(f"SIDECAR_ERROR {type(exc).__name__}: {exc}", file=sys.stderr, flush=True)
        raise
    finally:
        if runtime is not None:
            runtime.close()
        sock.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(r"C:/Projects/motion_bricks/motionbricks"))
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=49360)
    parser.add_argument("--fp16", action="store_true")
    parser.add_argument(
        "--engine-defaults",
        action="store_true",
        help="Оставить LocomotionSettings движка (halflife 0.6 с) — для A/B против --responsive.",
    )
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        print(json.dumps(run_self_test(args.repo, args.fp16), indent=2), flush=True)
    else:
        serve(args.repo, args.host, args.port, args.fp16, not args.engine_defaults)


if __name__ == "__main__":
    main()
