"""Official MotionBricks vertical slice: собственный KL motion → SOMA → official TensorRT + A/B.

Запускать Python из официального venv, cwd = C:\\Projects\\motion_bricks\\motionbricks:

  python docs/world/MOTIONBRICK_animations/tools/official_vertical_slice.py prepare
  python docs/world/MOTIONBRICK_animations/tools/official_vertical_slice.py official
  python docs/world/MOTIONBRICK_animations/tools/official_vertical_slice.py community
  python docs/world/MOTIONBRICK_animations/tools/official_vertical_slice.py analyze

Unity Edit Mode сначала создаёт unity_pose_pack.json через меню:
  ProjectC/MotionBricks/Official Slice/Export KL walk seed (Edit Mode)

NVIDIA sample motions не загружаются. Play Mode, Unity animator/controllers и shipping build
этот скрипт не затрагивает. Бинарные входы/выходы — только локальные артефакты, не для коммита.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys
import time

import numpy as np

PROJECT_ROOT = Path(__file__).resolve().parents[4]
SLICE_DIR = PROJECT_ROOT / "Assets/_Project/Animations/Generated/MotionBricks/official_vertical_slice/Male"
PACK_PATH = SLICE_DIR / "unity_pose_pack.json"
BIND_PATH = SLICE_DIR / "soma_bind.f32"
SOURCE_PATH = SLICE_DIR / "OwnedKlWalk_Source_30fps.f32"
OFFICIAL_PATH = SLICE_DIR / "OfficialWalk_Male_30fps.f32"
COMMUNITY_PATH = SLICE_DIR / "CommunityWalk_Male_30fps.f32"
COMMUNITY_SAIB = SLICE_DIR / "community.saib"
COMMUNITY_CLI = Path(
    "C:/Projects/motion_bricks/community/sa-inbetween/releases/v0.9.4/win-x64/cli-win-x64/sa-inbetween.exe"
)
COMMUNITY_MODEL = Path(
    "C:/Projects/motion_bricks/community/sa-inbetween/releases/v0.9.4/tools-win-x64/SaInbetween/model/mmm_v6_soma_fp32.onnx"
)
CONTEXT_INDICES = (0, 1, 2, 3)
# Контекст заканчивается на 0.10 s, target начинается на 0.667 s. Минимальные 6 токенов дают
# 18 валидных кадров (0.567 s), поэтому модель не растягивает 2.14 м/с клип в медленную походку.
TARGET_INDICES = (20, 21, 22, 23)
TOKEN_COUNTS = (6,)
LEFT_FOOT, RIGHT_FOOT = 69, 74
LEFT_LEG, RIGHT_LEG = 67, 72


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def qnormalize(q: np.ndarray) -> np.ndarray:
    q = np.asarray(q, dtype=np.float64)
    n = np.linalg.norm(q, axis=-1, keepdims=True)
    if np.any(n < 1e-12):
        raise ValueError("Zero quaternion in motion conversion")
    return q / n


def qmul(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    ax, ay, az, aw = np.moveaxis(np.asarray(a, dtype=np.float64), -1, 0)
    bx, by, bz, bw = np.moveaxis(np.asarray(b, dtype=np.float64), -1, 0)
    return np.stack(
        (aw * bx + ax * bw + ay * bz - az * by,
         aw * by - ax * bz + ay * bw + az * bx,
         aw * bz + ax * by - ay * bx + az * bw,
         aw * bw - ax * bx - ay * by - az * bz),
        axis=-1,
    )


def qconj(q: np.ndarray) -> np.ndarray:
    result = np.array(q, dtype=np.float64, copy=True)
    result[..., :3] *= -1.0
    return result


def qrotate(q: np.ndarray, v: np.ndarray) -> np.ndarray:
    qv = np.concatenate((np.asarray(v, dtype=np.float64), np.zeros(1)), axis=-1)
    return qmul(qmul(q, qv), qconj(q))[..., :3]


def to_unity_q(q: np.ndarray) -> np.ndarray:
    result = np.array(q, dtype=np.float64, copy=True)
    result[..., 1:3] *= -1.0
    return result


def to_unity_p(p: np.ndarray) -> np.ndarray:
    result = np.array(p, dtype=np.float64, copy=True)
    result[..., 0] *= -1.0
    return result


def fk(local_poses: np.ndarray, parents: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Local [frame,joint,7] → world positions/rotations; parents топологически упорядочены."""
    frames = np.asarray(local_poses, dtype=np.float64)
    positions = np.zeros(frames.shape[:2] + (3,), dtype=np.float64)
    rotations = np.zeros(frames.shape[:2] + (4,), dtype=np.float64)
    for f in range(frames.shape[0]):
        for j, parent in enumerate(parents):
            p, q = frames[f, j, :3], qnormalize(frames[f, j, 3:7])
            if parent < 0:
                positions[f, j], rotations[f, j] = p, q
            else:
                positions[f, j] = positions[f, parent] + qrotate(rotations[f, parent], p)
                rotations[f, j] = qnormalize(qmul(rotations[f, parent], q))
    return positions, rotations


def q_angle_degrees(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    """Sign-invariant угол между XYZW-кватернионами (0 означает одну и ту же ориентацию)."""
    aa = qnormalize(a)
    bb = qnormalize(b)
    dot = np.abs(np.sum(aa * bb, axis=-1))
    return np.degrees(2.0 * np.arccos(np.clip(dot, 0.0, 1.0)))


def official_skeleton():
    from motionbricks.io.usd import load_skeleton_from_usd
    from motionbricks.io.utils import SOMA_SKELETON_PRIM_PATH, get_asset_file

    return load_skeleton_from_usd(
        get_asset_file("human", "soma_base_skel_minimal_no_root.usd"),
        SOMA_SKELETON_PRIM_PATH,
    )


def prepare() -> dict:
    pack = json.loads(PACK_PATH.read_text(encoding="utf-8"))
    skeleton = official_skeleton()
    bind = skeleton.reference_local_transforms.numpy().astype(np.float64)
    parents = skeleton.parent_indices.numpy()
    if len(parents) != 77:
        raise ValueError("Official USD no longer has 77 joints")

    # Rest source преобразуется в оси Unity один раз до FK: M(q_parent*q_local)M взаимно.
    bind_unity = bind.copy()
    bind_unity[:, :3] = to_unity_p(bind[:, :3])
    bind_unity[:, 3:7] = to_unity_q(bind[:, 3:7])
    _, bind_world_q = fk(bind_unity[None, :, :], parents)

    rest_by_pose = {b["poseIndex"]: b for b in pack["bones"]}
    mapped_indices = sorted(rest_by_pose)
    rest_pack_q = {
        j: qnormalize(np.array([
            rest_by_pose[j]["qx"], rest_by_pose[j]["qy"],
            rest_by_pose[j]["qz"], rest_by_pose[j]["qw"],
        ], dtype=np.float64))
        for j in mapped_indices
    }

    # Скелетные длины инвариантны к позе, поэтому frame0 подходит для масштаба root translation.
    frame0 = pack["frames"][0]
    pose0 = {b["poseIndex"]: b for b in frame0["bones"]}
    distance = lambda a, b: float(np.linalg.norm(np.array([
        pose0[a]["px"] - pose0[b]["px"],
        pose0[a]["py"] - pose0[b]["py"],
        pose0[a]["pz"] - pose0[b]["pz"],
    ], dtype=np.float64)))
    unity_leg = 0.5 * (
        distance(67, 68) + distance(68, 69) + distance(72, 73) + distance(73, 74)
    )
    bind_positions, _ = fk(bind[None, :, :], parents)
    soma_leg = 0.5 * (
        np.linalg.norm(bind_positions[0, 68] - bind_positions[0, 67])
        + np.linalg.norm(bind_positions[0, 69] - bind_positions[0, 68])
        + np.linalg.norm(bind_positions[0, 73] - bind_positions[0, 72])
        + np.linalg.norm(bind_positions[0, 74] - bind_positions[0, 73])
    )
    if unity_leg < 1e-6:
        raise ValueError("Degenerate KL leg length")
    root_scale = float(soma_leg / unity_leg)
    hip0 = np.array([frame0["hipX"], frame0["hipY"], frame0["hipZ"]], dtype=np.float64)

    source = np.repeat(bind[None, :, :], len(pack["frames"]), axis=0)
    roundtrip_deg = np.zeros((len(pack["frames"]), len(mapped_indices)), dtype=np.float64)

    for f, frame in enumerate(pack["frames"]):
        current_pack = {b["poseIndex"]: b for b in frame["bones"]}
        desired_q = {}
        for j in mapped_indices:
            b = current_pack[j]
            current_q = qnormalize(np.array([b["qx"], b["qy"], b["qz"], b["qw"]]))
            # Inverse существующего SOMA→KL: искомый source world = (KL world*KL rest^-1)*SOMA rest.
            delta = qmul(current_q, qconj(rest_pack_q[j]))
            desired_q[j] = qnormalize(qmul(delta, bind_world_q[0, j]))

        world_p = np.zeros((77, 3), dtype=np.float64)
        world_q = np.zeros((77, 4), dtype=np.float64)
        local_q = np.zeros((77, 4), dtype=np.float64)
        for j, parent in enumerate(parents):
            if j in desired_q:
                world_q[j] = desired_q[j]
            elif parent < 0:
                world_q[j] = qnormalize(bind_unity[j, 3:7])
            else:
                world_q[j] = qnormalize(qmul(world_q[parent], bind_unity[j, 3:7]))

            local_q[j] = bind_unity[j, 3:7] if parent < 0 else qnormalize(
                qmul(qconj(world_q[parent]), world_q[j])
            )
            world_p[j] = (
                to_unity_p(bind[j, :3]) if parent < 0
                else world_p[parent] + qrotate(world_q[parent], to_unity_p(bind[j, :3]))
            )

        source[f, :, 3:7] = to_unity_q(qnormalize(local_q))
        hip = np.array([frame["hipX"], frame["hipY"], frame["hipZ"]], dtype=np.float64)
        source[f, 0, :3] = bind[0, :3] + to_unity_p((hip - hip0) * root_scale)

        for k, j in enumerate(mapped_indices):
            b = current_pack[j]
            current_q = np.array([b["qx"], b["qy"], b["qz"], b["qw"]])
            # Полный roundtrip: source pose → source delta к SOMA bind → rest кости KL.
            # Сравнивать source world напрямую с KL world нельзя: у скелетов разные bind-ориентации.
            reconstructed = qmul(
                qmul(desired_q[j], qconj(bind_world_q[0, j])),
                rest_pack_q[j],
            )
            roundtrip_deg[f, k] = q_angle_degrees(reconstructed, current_q)

    if not np.isfinite(source).all():
        raise ValueError("Non-finite SOMA seed")
    max_roundtrip = float(roundtrip_deg.max())
    if max_roundtrip > 0.01:
        raise ValueError(f"Inverse retarget roundtrip failed: {max_roundtrip:.6f} degrees")

    SLICE_DIR.mkdir(parents=True, exist_ok=True)
    bind.astype("<f4").tofile(BIND_PATH)
    source.astype("<f4").tofile(SOURCE_PATH)
    for kind, indices in (("context", CONTEXT_INDICES), ("target", TARGET_INDICES)):
        for i, frame_index in enumerate(indices):
            source[frame_index].astype("<f4").tofile(SLICE_DIR / f"{kind}_{i}.pose")

    metadata = {
        "schema": 1,
        "sourceModel": pack["modelPath"],
        "sourceClip": pack["clipPath"],
        "sourceClipName": pack["clipName"],
        "sourceFrames": len(pack["frames"]),
        "sourceFps": pack["frameRate"],
        "sourcePosePackSha256": sha256(PACK_PATH),
        "ownedSource": True,
        "nvidiaSampleMotionsUsed": False,
        "contextFrames": CONTEXT_INDICES,
        "targetFrames": TARGET_INDICES,
        "contextIsConsecutiveMotion": True,
        "targetIsConsecutiveMotion": True,
        "rootTranslationScaleSomaPerUnity": root_scale,
        "unityLegLengthMeters": unity_leg,
        "somaLegLengthMeters": float(soma_leg),
        "roundtripMappedMaxDegrees": max_roundtrip,
        "roundtripMappedMeanDegrees": float(roundtrip_deg.mean()),
        "sourceSha256": sha256(SOURCE_PATH),
        "bindSha256": sha256(BIND_PATH),
        "backendConfig": {
            "modelSpace": "SOMA Y-up, identity model_to_runtime",
            "allowedTokens": list(TOKEN_COUNTS),
            "endpointCorrection": True,
            "targetPoseCount": 4,
        },
    }
    (SLICE_DIR / "seed_report.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(json.dumps(metadata, indent=2))
    return metadata


def load_raw(path: Path) -> np.ndarray:
    raw = np.fromfile(path, dtype="<f4")
    if raw.size % (77 * 7) != 0:
        raise ValueError(f"{path}: expected N x 77 x 7 float32, got {raw.size} floats")
    result = raw.reshape((-1, 77, 7)).astype(np.float64)
    if not np.isfinite(result).all():
        raise ValueError(f"{path}: non-finite pose")
    return result


def official() -> dict:
    import warp as wp
    from motionbricks.model.config import MotionBricksInferenceConfig
    from motionbricks.model.soma.contract import SOMA_N1_FP32_ONNX_PATH
    from motionbricks.model.soma.engine import MotionBricksSOMAEngine

    source = load_raw(SOURCE_PATH)
    skeleton = official_skeleton()
    wp.init()
    wp.set_device("cuda:0")
    config = MotionBricksInferenceConfig(allowed_token_counts=TOKEN_COUNTS)
    wall_start = time.perf_counter()
    # Identity: вход уже в model space Y-up, как и .pose у community backend. Никакого MuJoCo Z-up.
    with MotionBricksSOMAEngine(SOMA_N1_FP32_ONNX_PATH, model_to_runtime=wp.transform_identity()) as engine:
        context = engine.create_input(skeleton)
        target = engine.create_input(skeleton)
        output = engine.create_output(skeleton)
        context_poses = wp.array(source[list(CONTEXT_INDICES)].astype(np.float32), dtype=wp.transform, device=engine.device)
        target_poses = wp.array(source[list(TARGET_INDICES)].astype(np.float32), dtype=wp.transform, device=engine.device)
        with wp.ScopedStream(engine.stream):
            context.update_from_poses(context_poses, stream=engine.stream)
            target.update_from_poses(target_poses, stream=engine.stream)
            engine.generate_animation(context, target, output, config=config)
        count = output.num_frames
        generated = output.poses[:count].numpy()
        contacts = output.foot_contacts[:count].numpy()
        timings = dict(engine.timer.milliseconds)
    generated.astype("<f4").tofile(OFFICIAL_PATH)
    contacts.astype("<f4").tofile(SLICE_DIR / "official_contacts.f32")
    report = {
        "backend": "official MotionBricksSOMAEngine / TensorRT",
        "modelPath": str(SOMA_N1_FP32_ONNX_PATH),
        "modelSha256": sha256(Path(SOMA_N1_FP32_ONNX_PATH)),
        "inputSha256": sha256(SOURCE_PATH),
        "outputSha256": sha256(OFFICIAL_PATH),
        "frames": count,
        "tokens": (count + 6) // 4,
        "fps": 30,
        "contextPoses": 4,
        "targetPoses": 4,
        "allowedTokens": list(config.allowed_token_counts),
        "endpointCorrection": config.correct_generated_diff,
        "headingAlignment": list(config.heading_alignment),
        "modelToRuntime": "identity",
        "stageMilliseconds": timings,
        "wallMillisecondsIncludingImportsEngineAndIo": (time.perf_counter() - wall_start) * 1000.0,
    }
    (SLICE_DIR / "official_generation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return report


def run_checked(command: list[str]) -> str:
    completed = subprocess.run(command, check=True, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if completed.stdout:
        print(completed.stdout, end="")
    if completed.stderr:
        print(completed.stderr, end="", file=sys.stderr)
    return completed.stdout


def community() -> dict:
    command = [str(COMMUNITY_CLI), "generate", "--model", str(COMMUNITY_MODEL)]
    for i in range(4):
        command += ["--context", str(SLICE_DIR / f"context_{i}.pose")]
    targets = ",".join(str(SLICE_DIR / f"target_{i}.pose") for i in range(4))
    command += [
        "--target", targets,
        "--tokens-min", str(min(TOKEN_COUNTS)), "--tokens-max", str(max(TOKEN_COUNTS)),
        "--override", str(TOKEN_COUNTS[0]),
        "--out", str(COMMUNITY_SAIB),
    ]
    stdout = run_checked(command)
    inspection = run_checked([str(COMMUNITY_CLI), "inspect", str(COMMUNITY_SAIB)])
    first = next(line for line in inspection.splitlines() if line.startswith("frames "))
    count = int(first.split()[1])
    frames_dir = SLICE_DIR / "community_frames"
    frames_dir.mkdir(exist_ok=True)
    raw = []
    for i in range(count):
        pose_path = frames_dir / f"{i:03d}.pose"
        run_checked([str(COMMUNITY_CLI), "pose", str(COMMUNITY_SAIB), "--frame", str(i), "--out", str(pose_path)])
        raw.append(np.fromfile(pose_path, dtype="<f4"))
    np.concatenate(raw).astype("<f4").tofile(COMMUNITY_PATH)
    report = {
        "backend": "sa-inbetween v0.9.4 / ONNX Runtime CPU",
        "modelPath": str(COMMUNITY_MODEL),
        "modelSha256": sha256(COMMUNITY_MODEL),
        "inputSha256": sha256(SOURCE_PATH),
        "outputSha256": sha256(COMMUNITY_PATH),
        "frames": count,
        "tokens": (count + 6) // 4,
        "fps": 30,
        "contextPoses": 4,
        "targetPoses": 4,
        "allowedTokens": list(TOKEN_COUNTS),
        "endpointCorrection": True,
        "generateStdout": stdout,
        "inspectStdout": inspection,
    }
    (SLICE_DIR / "community_generation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return report


def motion_metrics(raw: np.ndarray, parents: np.ndarray, contacts_path: Path | None = None) -> dict:
    positions, _ = fk(raw, parents)
    roots = raw[:, 0, :3]
    delta = roots[-1] - roots[0]
    distance = float(np.linalg.norm(delta[[0, 2]]))
    durations = (len(raw) - 1) / 30.0

    across = positions[:, RIGHT_LEG] - positions[:, LEFT_LEG]
    forward = np.stack((across[:, 2], np.zeros(len(raw)), -across[:, 0]), axis=-1)
    headings = np.unwrap(np.arctan2(forward[:, 0], forward[:, 2]))
    foot_heights = positions[:, [LEFT_FOOT, RIGHT_FOOT], 1]
    report = {
        "frames": len(raw),
        "durationSeconds": durations,
        "rootDisplacementXYZ": delta.tolist(),
        "rootGroundTravelMeters": distance,
        "rootGroundSpeedMetersPerSecond": distance / durations,
        "rootYRangeMeters": float(np.ptp(roots[:, 1])),
        "yawDriftDegrees": float(np.degrees(headings[-1] - headings[0])),
        "maxFootHeightMeters": float(foot_heights.max()),
        "minFootHeightMeters": float(foot_heights.min()),
        "finite": bool(np.isfinite(raw).all()),
    }
    if contacts_path is not None and contacts_path.exists():
        contacts = np.fromfile(contacts_path, dtype="<f4").reshape((-1, 4))
        report["contactCountsLhLtRhRt"] = (contacts > 0.5).sum(axis=0).astype(int).tolist()
        report["contactConfidenceMinMax"] = [float(contacts.min()), float(contacts.max())]
    return report


def analyze() -> dict:
    parents = official_skeleton().parent_indices.numpy()
    source = load_raw(SOURCE_PATH)
    official_raw = load_raw(OFFICIAL_PATH)
    report = {
        "schema": 1,
        "note": "Численный raw-SOMA probe, не визуальный и не production acceptance.",
        "sourceOwnedMotion": motion_metrics(source, parents),
        "official": motion_metrics(official_raw, parents, SLICE_DIR / "official_contacts.f32"),
    }
    if COMMUNITY_PATH.exists():
        community_raw = load_raw(COMMUNITY_PATH)
        report["community"] = motion_metrics(community_raw, parents)
        report["backendComparison"] = {"sameFrameCount": len(official_raw) == len(community_raw)}
        if len(official_raw) == len(community_raw):
            op, oq = fk(official_raw, parents)
            cp, cq = fk(community_raw, parents)
            report["backendComparison"].update({
                "maxWorldPositionDifferenceMeters": float(np.linalg.norm(op - cp, axis=-1).max()),
                "rmsWorldPositionDifferenceMeters": float(np.sqrt(np.mean(np.sum((op - cp) ** 2, axis=-1)))),
                "maxWorldRotationDifferenceDegrees": float(q_angle_degrees(oq, cq).max()),
            })
    (SLICE_DIR / "raw_metrics.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=("prepare", "official", "community", "analyze", "all"))
    args = parser.parse_args()
    for stage in (("prepare", "official", "community", "analyze") if args.stage == "all" else (args.stage,)):
        globals()[stage]()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
