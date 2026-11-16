"""Locomotion transition pack v1: 22 offline transitions через официальный MotionBricks.

Предусловие: Unity Edit Mode menu
  ProjectC/MotionBricks/Official Slice/Export locomotion transition sources (Edit Mode)

Запуск из официального venv, cwd = C:\\Projects\\motion_bricks\\motionbricks:
  .venv\\Scripts\\python.exe <этот файл> generate

Скрипт не запускает Play Mode и не меняет Animator. Он создаёт raw SOMA output и sidecar'ы
для последующего batch bake. NVIDIA sample motions не используются.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import sys

import numpy as np

import official_vertical_slice as ovs

PROJECT_ROOT = ovs.PROJECT_ROOT
MANIFEST_PATH = PROJECT_ROOT / "docs/world/MOTIONBRICK_animations/batches/locomotion_v1.json"
SOURCE_ROOT = PROJECT_ROOT / "Assets/_Project/Animations/Generated/MotionBricks/locomotion_sources"
WORK_ROOT = PROJECT_ROOT / "Assets/_Project/Animations/Generated/MotionBricks/locomotion_pack_work"
PACK_PRIMITIVE = "locomotion_transitions_v1"
BAKE_ROOT = PROJECT_ROOT / "Assets/_Project/Animations/Generated/MotionBricks" / PACK_PRIMITIVE
BIND_PATH = WORK_ROOT / "soma_bind.f32"
FPS = 30.0


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def pascal(value: str) -> str:
    return "".join(part.capitalize() for part in value.replace("-", "_").split("_") if part)


def load_manifest() -> dict:
    data = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    sources = {item["id"]: item for item in data["sources"]}
    if len(sources) != len(data["sources"]):
        raise ValueError("Duplicate source id in locomotion manifest")
    for job in data["jobs"]:
        if job["context"] not in sources or job["target"] not in sources:
            raise ValueError(f"Unknown source in job {job['id']}")
        if len(job["contextFrames"]) != 4 or len(job["targetFrames"]) != 4:
            raise ValueError(f"Job {job['id']} needs exactly 4 context and 4 target frames")
    return data


def load_pack(source_id: str) -> dict:
    path = SOURCE_ROOT / source_id / "Male/unity_pose_pack.json"
    if not path.exists():
        raise FileNotFoundError(f"Нет pose pack: {path}. Сначала выполните Unity export.")
    pack = json.loads(path.read_text(encoding="utf-8"))
    if pack.get("sourceId") != source_id:
        raise ValueError(f"{path}: sourceId={pack.get('sourceId')}, expected {source_id}")
    return pack


def source_to_soma(pack: dict, skeleton, bind: np.ndarray, parents: np.ndarray) -> tuple[np.ndarray, float]:
    """KL pose pack → 77-joint SOMA local frames + max roundtrip error."""
    bind_unity = bind.copy()
    bind_unity[:, :3] = ovs.to_unity_p(bind[:, :3])
    bind_unity[:, 3:7] = ovs.to_unity_q(bind[:, 3:7])
    _, bind_world_q = ovs.fk(bind_unity[None, :, :], parents)

    rest_by_pose = {item["poseIndex"]: item for item in pack["bones"]}
    mapped = sorted(rest_by_pose)
    rest_pack_q = {
        j: ovs.qnormalize(np.array([
            rest_by_pose[j]["qx"], rest_by_pose[j]["qy"],
            rest_by_pose[j]["qz"], rest_by_pose[j]["qw"],
        ], dtype=np.float64))
        for j in mapped
    }

    frame0 = pack["frames"][0]
    pose0 = {item["poseIndex"]: item for item in frame0["bones"]}
    distance = lambda a, b: float(np.linalg.norm(np.array([
        pose0[a]["px"] - pose0[b]["px"],
        pose0[a]["py"] - pose0[b]["py"],
        pose0[a]["pz"] - pose0[b]["pz"],
    ], dtype=np.float64)))
    unity_leg = 0.5 * (distance(67, 68) + distance(68, 69) + distance(72, 73) + distance(73, 74))
    bind_pos, _ = ovs.fk(bind[None, :, :], parents)
    soma_leg = 0.5 * (
        np.linalg.norm(bind_pos[0, 68] - bind_pos[0, 67])
        + np.linalg.norm(bind_pos[0, 69] - bind_pos[0, 68])
        + np.linalg.norm(bind_pos[0, 73] - bind_pos[0, 72])
        + np.linalg.norm(bind_pos[0, 74] - bind_pos[0, 73])
    )
    root_scale = soma_leg / unity_leg
    hip0 = np.array([frame0["hipX"], frame0["hipY"], frame0["hipZ"]], dtype=np.float64)

    source = np.repeat(bind[None, :, :], len(pack["frames"]), axis=0)
    errors = []
    for f, frame in enumerate(pack["frames"]):
        current_pack = {item["poseIndex"]: item for item in frame["bones"]}
        desired_q = {}
        for j in mapped:
            item = current_pack[j]
            current_q = ovs.qnormalize(np.array([item["qx"], item["qy"], item["qz"], item["qw"]]))
            delta = ovs.qmul(current_q, ovs.qconj(rest_pack_q[j]))
            desired_q[j] = ovs.qnormalize(ovs.qmul(delta, bind_world_q[0, j]))

        world_p = np.zeros((77, 3), dtype=np.float64)
        world_q = np.zeros((77, 4), dtype=np.float64)
        local_q = np.zeros((77, 4), dtype=np.float64)
        for j, parent in enumerate(parents):
            if j in desired_q:
                world_q[j] = desired_q[j]
            elif parent < 0:
                world_q[j] = ovs.qnormalize(bind_unity[j, 3:7])
            else:
                world_q[j] = ovs.qnormalize(ovs.qmul(world_q[parent], bind_unity[j, 3:7]))
            local_q[j] = bind_unity[j, 3:7] if parent < 0 else ovs.qnormalize(
                ovs.qmul(ovs.qconj(world_q[parent]), world_q[j])
            )
            world_p[j] = (
                ovs.to_unity_p(bind[j, :3]) if parent < 0
                else world_p[parent] + ovs.qrotate(world_q[parent], ovs.to_unity_p(bind[j, :3]))
            )

        source[f, :, 3:7] = ovs.to_unity_q(ovs.qnormalize(local_q))
        hip = np.array([frame["hipX"], frame["hipY"], frame["hipZ"]], dtype=np.float64)
        source[f, 0, :3] = bind[0, :3] + ovs.to_unity_p((hip - hip0) * root_scale)

        for j in mapped:
            item = current_pack[j]
            current_q = np.array([item["qx"], item["qy"], item["qz"], item["qw"]])
            reconstructed = ovs.qmul(
                ovs.qmul(desired_q[j], ovs.qconj(bind_world_q[0, j])), rest_pack_q[j]
            )
            errors.append(float(ovs.q_angle_degrees(reconstructed, current_q)))

    if not np.isfinite(source).all():
        raise ValueError(f"{pack['sourceId']}: non-finite SOMA source")
    return source, max(errors)


def heading_from_raw(raw: np.ndarray, parents: np.ndarray) -> float:
    positions, _ = ovs.fk(raw, parents)
    across = positions[-1, ovs.RIGHT_LEG] - positions[-1, ovs.LEFT_LEG]
    forward = np.array([across[2], 0.0, -across[0]], dtype=np.float64)
    return float(math.atan2(forward[0], forward[2]))


def rotate_target(raw: np.ndarray, angle: float) -> np.ndarray:
    """Повернуть target pose sequence вокруг Y, сохранив локальные позы кроме root."""
    result = raw.copy()
    qy = np.array([0.0, math.sin(angle * 0.5), 0.0, math.cos(angle * 0.5)], dtype=np.float64)
    c, s = math.cos(angle), math.sin(angle)
    for f in range(result.shape[0]):
        p = result[f, 0, :3].copy()
        result[f, 0, 0] = c * p[0] + s * p[2]
        result[f, 0, 2] = -s * p[0] + c * p[2]
        result[f, 0, 3:7] = ovs.qnormalize(ovs.qmul(qy, result[f, 0, 3:7]))
    return result


def planar_delta(raw: np.ndarray) -> np.ndarray:
    return raw[-1, 0, [0, 2]] - raw[0, 0, [0, 2]]


def place_target(context: np.ndarray, target_source: np.ndarray, target_indices: list[int],
                 lead_seconds: float) -> tuple[np.ndarray, dict]:
    """Align target body heading to context and place its root ahead of the context."""
    target = target_source[target_indices].copy()
    context_heading = heading_from_raw(context, OVS_PARENTS)
    target_start_heading = heading_from_raw(target_source[0:1], OVS_PARENTS)
    correction = math.atan2(
        math.sin(context_heading - target_start_heading),
        math.cos(context_heading - target_start_heading),
    )
    target = rotate_target(target, correction)

    context_delta = planar_delta(context)
    target_delta = planar_delta(target)
    context_speed = float(np.linalg.norm(context_delta)) / (3.0 / FPS)
    target_speed = float(np.linalg.norm(target_delta)) / (3.0 / FPS)

    if target_speed > 0.15:
        direction = target_delta / np.linalg.norm(target_delta)
    elif context_speed > 0.15:
        direction = context_delta / np.linalg.norm(context_delta)
    else:
        direction = np.array([math.sin(context_heading), math.cos(context_heading)], dtype=np.float64)

    desired_speed = max(target_speed, context_speed, 1.0)
    context_root = context[-1, 0, :3].copy()
    offset = np.array([direction[0], 0.0, direction[1]], dtype=np.float64) * desired_speed * lead_seconds
    target_first = target[0, 0, :3].copy()
    shift = context_root + offset - target_first
    target[:, 0, :3] += shift

    return target, {
        "contextHeadingDegrees": math.degrees(context_heading),
        "targetStartHeadingDegrees": math.degrees(target_start_heading),
        "yawCorrectionDegrees": math.degrees(correction),
        "contextSpeedMetersPerSecond": context_speed,
        "targetSpeedMetersPerSecond": target_speed,
        "placementDirectionXZ": direction.tolist(),
        "placementLeadSeconds": lead_seconds,
        "targetRootShift": shift.tolist(),
    }


def write_bind(bind: np.ndarray) -> None:
    BIND_PATH.parent.mkdir(parents=True, exist_ok=True)
    bind.astype("<f4").tofile(BIND_PATH)


def generate() -> dict:
    import warp as wp
    from motionbricks.model.config import MotionBricksInferenceConfig
    from motionbricks.model.soma.contract import SOMA_N1_FP32_ONNX_PATH
    from motionbricks.model.soma.engine import MotionBricksSOMAEngine

    global OVS_PARENTS
    manifest = load_manifest()
    skeleton = ovs.official_skeleton()
    OVS_PARENTS = skeleton.parent_indices.numpy()
    bind = skeleton.reference_local_transforms.numpy().astype(np.float64)
    write_bind(bind)
    WORK_ROOT.mkdir(parents=True, exist_ok=True)
    BAKE_ROOT.mkdir(parents=True, exist_ok=True)

    sources = {}
    source_report = {}
    for source_id in sorted({job["context"] for job in manifest["jobs"]} | {job["target"] for job in manifest["jobs"]}):
        pack = load_pack(source_id)
        raw, roundtrip = source_to_soma(pack, skeleton, bind, OVS_PARENTS)
        sources[source_id] = raw
        source_report[source_id] = {
            "frames": len(raw),
            "fps": pack["frameRate"],
            "clipPath": pack["clipPath"],
            "clipName": pack["clipName"],
            "roundtripMaxDegrees": roundtrip,
        }
        print(f"[source] {source_id}: {len(raw)} frames, roundtrip max {roundtrip:.8f} deg")

    wp.init()
    wp.set_device("cuda:0")
    config = MotionBricksInferenceConfig(allowed_token_counts=(manifest["tokenCount"],))
    jobs_report = []
    with MotionBricksSOMAEngine(SOMA_N1_FP32_ONNX_PATH, model_to_runtime=wp.transform_identity()) as engine:
        context_token = engine.create_input(skeleton)
        target_token = engine.create_input(skeleton)
        output = engine.create_output(skeleton)
        for job in manifest["jobs"]:
            context = sources[job["context"]][job["contextFrames"]]
            target, placement = place_target(
                context, sources[job["target"]], job["targetFrames"], job["leadSeconds"]
            )
            poses = np.concatenate((context, target), axis=0)
            poses = poses.astype(np.float32)
            context_poses = wp.array(poses[:4], dtype=wp.transform, device=engine.device)
            target_poses = wp.array(poses[4:], dtype=wp.transform, device=engine.device)
            with wp.ScopedStream(engine.stream):
                context_token.update_from_poses(context_poses, stream=engine.stream)
                target_token.update_from_poses(target_poses, stream=engine.stream)
                engine.generate_animation(context_token, target_token, output, config=config)
            count = output.num_frames
            generated = output.poses[:count].numpy()
            contacts = output.foot_contacts[:count].numpy()
            if not np.isfinite(generated).all():
                raise ValueError(f"{job['id']}: non-finite official output")

            clip_base = f"MB_{pascal(job['id'])}"
            job_dir = BAKE_ROOT / "Male"
            job_dir.mkdir(parents=True, exist_ok=True)
            raw_path = job_dir / f"{clip_base}_30fps.f32"
            generated.astype("<f4").tofile(raw_path)
            (job_dir / "soma_bind.f32").write_bytes(BIND_PATH.read_bytes())
            sidecar = {
                "groundFeet": True,
                "legIk": True,
                "loop": False,
                "bakeInPlace": True,
                "clipSpeed": placement["targetSpeedMetersPerSecond"],
                "label": f"Locomotion transition {job['id']}",
            }
            (job_dir / f"{clip_base}.bake.config.json").write_text(
                json.dumps(sidecar, indent=2), encoding="utf-8"
            )
            work_path = WORK_ROOT / f"{job['id']}.f32"
            generated.astype("<f4").tofile(work_path)
            np.asarray(contacts).astype("<f4").tofile(WORK_ROOT / f"{job['id']}_contacts.f32")

            trajectory, _ = ovs.fk(generated, OVS_PARENTS)
            travel = float(np.linalg.norm(generated[-1, 0, [0, 2]] - generated[0, 0, [0, 2]]))
            report = {
                "id": job["id"],
                "clipBase": clip_base,
                "source": {"context": job["context"], "target": job["target"],
                           "contextFrames": job["contextFrames"], "targetFrames": job["targetFrames"]},
                "frames": count,
                "tokens": (count + 6) // 4,
                "durationSeconds": (count - 1) / FPS,
                "rootTravelMeters": travel,
                "footHeightMinMeters": float(trajectory[:, [69, 74], 1].min()),
                "footHeightMaxMeters": float(trajectory[:, [69, 74], 1].max()),
                "contactCountsLhLtRhRt": [int(v) for v in (contacts > 0.5).sum(axis=0)],
                "rawSha256": sha256(raw_path),
                "placement": placement,
            }
            jobs_report.append(report)
            print(f"[job] {job['id']}: {count} frames, root {travel:.3f} m, {raw_path}")

    (WORK_ROOT / "batch_metrics.json").write_text(json.dumps({
        "schema": 1,
        "pack": PACK_PRIMITIVE,
        "officialModelSha256": sha256(Path(SOMA_N1_FP32_ONNX_PATH)),
        "manifestSha256": sha256(MANIFEST_PATH),
        "bindSha256": sha256(BIND_PATH),
        "sources": source_report,
        "jobs": jobs_report,
    }, indent=2), encoding="utf-8")
    print(f"[done] {len(jobs_report)} transitions -> {BAKE_ROOT}")
    return {"jobs": len(jobs_report)}


def main() -> int:
    if len(sys.argv) != 2 or sys.argv[1] != "generate":
        print("usage: locomotion_pack.py generate", file=sys.stderr)
        return 2
    generate()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
