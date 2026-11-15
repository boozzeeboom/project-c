"""Извлекает bind-позу SOMA из USD-скелета NVIDIA и пишет .pose-совместимый .f32.

Зачем: файл `soma_rest.pose`, который лежит рядом с инструментами sa-inbetween,
НЕ является bind-позой. `tools/make_pose_library.py` создаёт его копированием
произвольного кадра библиотеки:

    first = next(iter(sorted(out.glob("*_0.pose"))), None)
    (out / "soma_rest.pose").write_bytes(first.read_bytes())

т.е. это первый по алфавиту кадр (`combat_quickstep_1_0.pose`). Как опорная поза
для ретаргета он не годится: руки в нём асимметричны (правая цепочка плечо-локоть-запястье
даёт дельту ~168° против ~88° у левой).

Истинная bind-поза — restTransforms скелета в
`assets/human/soma_base_skel_minimal_no_root.usd`.

Запуск (venv из offline-пака, там есть pxr):

    C:\\Projects\\motion_bricks\\motionbricks\\.venv\\Scripts\\python.exe ^
        docs/world/MOTIONBRICK_animations/tools/extract_soma_bind.py ^
        --out Assets/_Project/Animations/Generated/MotionBricks/walk_to_jog/Male/soma_bind.f32

Формат выхода — как у .pose: 77 джойнтов x (tx,ty,tz, qx,qy,qz,qw), float32 LE,
локальные трансформации, Y-up, метры.
"""

from __future__ import annotations

import argparse
import sys

from pxr import Gf, Usd, UsdSkel


def quat_from_matrix(m: Gf.Matrix4d) -> tuple[float, float, float, float]:
    q = m.ExtractRotationQuat()
    im = q.GetImaginary()
    return (float(im[0]), float(im[1]), float(im[2]), float(q.GetReal()))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--usd", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    stage = Usd.Stage.Open(args.usd)
    if stage is None:
        print("cannot open stage", args.usd, file=sys.stderr)
        return 1

    skel_prim = None
    for prim in stage.Traverse():
        if prim.IsA(UsdSkel.Skeleton):
            skel_prim = prim
            break
    if skel_prim is None:
        print("no UsdSkel.Skeleton prim", file=sys.stderr)
        return 1

    skel = UsdSkel.Skeleton(skel_prim)
    joints = list(skel.GetJointsAttr().Get() or [])
    rest = list(skel.GetRestTransformsAttr().Get() or [])
    print(f"skeleton prim: {skel_prim.GetPath()}")
    print(f"joints={len(joints)} restTransforms={len(rest)}")
    up = stage.GetMetadata("upAxis")
    print(f"upAxis={up} metersPerUnit={stage.GetMetadata('metersPerUnit')}")

    for i, name in enumerate(joints):
        print(f"  [{i:2}] {name}")

    if not joints or len(joints) != len(rest):
        print("joints/restTransforms mismatch", file=sys.stderr)
        return 1

    hips = rest[0].ExtractTranslation()
    print(f"joint0='{joints[0]}' translation=({hips[0]:.4f}, {hips[1]:.4f}, {hips[2]:.4f})")

    # USD здесь в сантиметрах (metersPerUnit=0.01), а .pose — в метрах.
    scale = float(stage.GetMetadata("metersPerUnit") or 1.0)
    print(f"translation scale = {scale}")

    import struct

    buf = bytearray()
    for m in rest:
        t = m.ExtractTranslation()
        q = quat_from_matrix(m)
        buf += struct.pack(
            "<7f",
            float(t[0]) * scale,
            float(t[1]) * scale,
            float(t[2]) * scale,
            *q,
        )

    with open(args.out, "wb") as f:
        f.write(buf)
    print(f"wrote {args.out}: {len(buf)} bytes ({len(rest)} joints)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
