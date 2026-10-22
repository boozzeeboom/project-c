"""walktest.py — траектория подъёма по лестницам: точки «шага» вдоль оси марша снизу вверх.
Выход: walk_points.txt — W|<лестница>|<x>|<поверхностьY>|<z>|<направление оси X>|<направление оси Z>
В Unity: капсула 1.7 м ставится на поверхность, проверяется CheckCapsule (нет ли блокировки).
"""
import json, math, os, sys

BASE = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS"
PX, PY, PZ = 39460.0, 2475.67, 40000.0
STEP = 0.25          # шаг вдоль марша, м
SKIN_DOWN = 0.35     # откуда ищем поверхность (над ступенью)


def w(p):
    return (PX - p[0], PY + p[1], PZ - p[2])


def parse_obj(path):
    objs, gv, cur = [], [], None
    for line in open(path, "r", encoding="utf-8"):
        if line.startswith("o "):
            cur = {"name": line[2:].strip(), "faces": []}
            objs.append(cur)
        elif line.startswith("v "):
            gv.append(tuple(float(t) for t in line.split()[1:4]))
        elif line.startswith("f "):
            cur["faces"].append([int(t.split("/")[0]) - 1 for t in line.split()[1:]])
    return objs, gv


def up_tris(o, gv):
    """горизонтальные (проходимые) грани: (центроид, высота)"""
    out = []
    for idx in o["faces"]:
        if len(idx) != 3:
            continue
        a, b, c = gv[idx[0]], gv[idx[1]], gv[idx[2]]
        u = [b[i] - a[i] for i in range(3)]
        v = [c[i] - a[i] for i in range(3)]
        n = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]]
        ln = math.sqrt(sum(t * t for t in n))
        if ln < 1e-9 or n[1] / ln < 0.5:
            continue
        out.append(((a[0] + b[0] + c[0]) / 3.0, (a[1] + b[1] + c[1]) / 3.0, (a[2] + b[2] + c[2]) / 3.0))
    return out


objs, gv = parse_obj(os.path.join(BASE, "stairs.obj"))
lines = []
info = []
for o in objs:
    tris = up_tris(o, gv)
    if len(tris) < 4:
        continue
    low = min(tris, key=lambda t: t[1])
    high = max(tris, key=lambda t: t[1])
    dx, dz = high[0] - low[0], high[2] - low[2]
    run = math.hypot(dx, dz)
    if run < 1.0:
        continue
    ux, uz = dx / run, dz / run
    n = max(4, int(run / STEP))
    surface = {}                                  # позиция вдоль марша -> высота поверхности
    for i in range(n + 1):
        t = i / float(n)
        lx = low[0] + dx * t
        lz = low[2] + dz * t
        # ближайшая к линии грань — оценка высоты поверхности в этой точке
        best = None
        for (tx, ty, tz) in tris:
            d = math.hypot(tx - lx, tz - lz)
            if best is None or d < best[0]:
                best = (d, ty)
        if best is None or best[0] > 1.2:         # линия ушла с марша (Г-образная) — стоп
            break
        surface[i] = best[1]
        wx, wy, wz = w((lx, best[1], lz))
        lines.append("W|%s|%.3f|%.3f|%.3f|%.3f|%.3f" % (o["name"], wx, wy, wz, -(ux), -(uz)))
    info.append((o["name"], n, len(surface), low[1], high[1], run))

dest = os.path.join(os.path.dirname(os.path.abspath(__file__)), "walk_points.txt")
open(dest, "w", encoding="utf-8").write("\n".join(lines))
print("лестниц:", len(info), "точек шага:", len(lines))
for row in info:
    print("  %-34s шагов=%-3d поверхность=%-3d y %.2f → %.2f  run=%.1f" % row)
