"""T-MB06: проверка пейсинга UDP-очереди (fix 17.13).

Меряет главное для «анимация отстаёт на секунду»: сколько времени модели реально
прокручивается за секунду реального времени.

    sim_to_wall = сумма dt, которые sidecar ОТДАЛ в модель / длительность окна

Legacy-режим (`LATEST_ONLY_DRAIN = True`) из пачки пришедших запросов обрабатывал только
новейший и выбрасывал остальные вместе с их dt — модель шагала реже, чем приходят кадры,
и её внутреннее время отставало от реального.

Скрипт поднимает настоящий `serve()` в фоновом потоке на тестовом порту (не 49360, чтобы не
мешать редактору), гоняет клиента на 15 Hz — как Unity в логе приёмки (`sent 14,6/s`) — и
сравнивает оба режима на одном и том же движении.

Запуск:
    python docs/world/MOTIONBRICK_animations/tools/verify_udp_pacing.py
"""

from __future__ import annotations

import json
import math
import socket
import sys
import threading
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import runtime_sidecar as rs  # noqa: E402

HOST = "127.0.0.1"
PORT = 49399
CLIENT_HZ = 15.0
WINDOW_SECONDS = 6.0
SPEED_MPS = 5.0
MIN_SIM_TO_WALL = 0.90

processed: list[float] = []


def _counting_step(self, frame):  # noqa: ANN001
    processed.append(float(frame.delta_time))
    return _original_step(self, frame)


_original_step = rs.ExternalSOMACharacter.step


def _drive_client(sock: socket.socket, duration: float) -> tuple[int, float]:
    """Гнать клиента с CLIENT_HZ на заданное время; вернуть число кадров и реальную длительность."""
    dt = 1.0 / CLIENT_HZ
    frames = 0
    position = np.zeros(3, dtype=np.float32)
    direction = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    velocity = direction * SPEED_MPS
    started = time.perf_counter()
    deadline = started + duration
    while time.perf_counter() < deadline:
        target = started + frames * dt
        delay = target - time.perf_counter()
        if delay > 0.0:
            time.sleep(delay)
        flags = rs.FLAG_RESET if frames == 0 else 0
        frame = rs.InputFrame(
            sequence=1000 + frames,
            flags=flags,
            delta_time=dt,
            position=position.copy(),
            rotation=np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float32),
            velocity=velocity,
        )
        sock.sendto(rs.pack_request(frame), (HOST, PORT))
        position = position + velocity * dt
        frames += 1
    return frames, time.perf_counter() - started


def measure(repo: Path, latest_only: bool) -> dict:
    rs.LATEST_ONLY_DRAIN = latest_only
    rs.ExternalSOMACharacter.step = _counting_step
    processed.clear()

    worker = threading.Thread(target=rs.serve, args=(repo, HOST, PORT, False), daemon=True)
    worker.start()
    time.sleep(0.5)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.settimeout(90.0)
    try:
        # Прогрев: первый запрос поднимает ENGINE внутри окна, его ответ ждём явно.
        warm = rs.InputFrame(
            sequence=1,
            flags=rs.FLAG_RESET,
            delta_time=1.0 / CLIENT_HZ,
            position=np.zeros(3, dtype=np.float32),
            rotation=np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float32),
            velocity=np.zeros(3, dtype=np.float32),
        )
        sock.sendto(rs.pack_request(warm), (HOST, PORT))
        warm_started = time.perf_counter()
        sock.recvfrom(4096)
        first_reply_ms = (time.perf_counter() - warm_started) * 1000.0

        processed.clear()
        sent, wall = _drive_client(sock, WINDOW_SECONDS)
        time.sleep(0.5)

        shutdown = rs.InputFrame(sequence=2, flags=rs.FLAG_SHUTDOWN)
        sock.sendto(rs.pack_request(shutdown), (HOST, PORT))
    finally:
        sock.close()
        worker.join(timeout=15.0)

    sim_seconds = float(sum(processed))
    return {
        "drain": "latest-only (legacy)" if latest_only else "in-order, each frame with its dt",
        "first_reply_ms": round(first_reply_ms, 1),
        "sent_frames": sent,
        "processed_frames": len(processed),
        "wall_seconds": round(wall, 3),
        "sim_seconds": round(sim_seconds, 3),
        "sim_to_wall_ratio": round(sim_seconds / wall, 3) if wall > 0 else None,
        "processed_per_second": round(len(processed) / wall, 2) if wall > 0 else None,
    }


def main() -> None:
    repo = Path(r"C:/Projects/motion_bricks/motionbricks")
    legacy = measure(repo, latest_only=True)
    fixed = measure(repo, latest_only=False)
    result = {
        "client_hz": CLIENT_HZ,
        "window_seconds": WINDOW_SECONDS,
        "legacy": legacy,
        "fixed": fixed,
        "threshold_sim_to_wall": MIN_SIM_TO_WALL,
        "status": "PASS" if (fixed["sim_to_wall_ratio"] or 0.0) >= MIN_SIM_TO_WALL else "FAIL",
    }
    print(json.dumps(result, indent=2), flush=True)
    if result["status"] != "PASS":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
