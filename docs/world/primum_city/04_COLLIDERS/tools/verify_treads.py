# verify_treads.py — проверка проходимости: каждая ступень должна быть поверхностью
# STAIRS-меша, а над ней должен быть просвет >= 1.9 м до боксовых коллайдеров.
# Вход: stairs.obj (Unity-local PRIMUM, индексы вершин ГЛОБАЛЬНЫЕ). Выход: tread_points.txt
import json, math, os

BASE = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS"
PX, PY, PZ = 39460.0, 2475.67, 40000.0
MAX_SAMPLES = 8


def w(p):
    return (PX - p[0], PY + p[1], PZ - p[2])


def parse_obj(path):
    objs = []
    cur = None
    gv = []                                   # глобальный список вершин
    for line in open(path, "r", encoding="utf-8"):
        if line.startswith("o "):
            cur = {"name": line[2:].strip(), "vstart": len(gv), "faces": []}
            objs.append(cur)
        elif line.startswith("v "):
            gv.append(tuple(float(t) for t in line.split()[1:4]))
        elif line.startswith("f "):
            cur["faces"].append([int(t.split("/")[0]) - 1 for t in line.split()[1:]])
    return objs, gv


out = []
objs, gv = parse_obj(os.path.join(BASE, "stairs.obj"))
for o in objs:
    tris = []
    for idx in o["faces"]:
        if len(idx) != 3:
            continue
        a, b, c = gv[idx[0]], gv[idx[1]], gv[idx[2]]
        u = [b[i] - a[i] for i in range(3)]
        v = [c[i] - a[i] for i in range(3)]
        n = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]]
        ln = math.sqrt(sum(t * t for t in n))
        if ln < 1e-9:
            continue
        if n[1] / ln < 0.7:               # только «полы» ступеней, не подступенки
            continue
        cen = [(a[i] + b[i] + c[i]) / 3.0 for i in range(3)]
        tris.append((cen, ln / 2.0))
    if not tris:
        out.append({"obj": o["name"], "pts": [], "note": "нет горизонтальных граней"})
        continue
    tris.sort(key=lambda t: -t[1])
    step = max(1, len(tris) // MAX_SAMPLES)
    picked = tris[::step][:MAX_SAMPLES]
    pts = []
    for cen, _area in picked:
        wx, wy, wz = w(cen)
        pts.append([round(wx, 3), round(wy, 3), round(wz, 3)])
    out.append({"obj": o["name"], "pts": pts, "treads": len(tris)})

here = os.path.dirname(os.path.abspath(__file__))
json.dump(out, open(os.path.join(here, "tread_points.json"), "w", encoding="utf-8"), ensure_ascii=False)
lines = []
for o in out:
    for p in o["pts"]:
        lines.append("T|%s|%.3f|%.3f|%.3f" % (o["obj"], p[0], p[1], p[2]))
open(os.path.join(here, "tread_points.txt"), "w", encoding="utf-8").write("\n".join(lines))
print("лестниц:", len(out), "точек-ступеней:", len(lines),
      "без горизонтальных граней:", sum(1 for o in out if not o["pts"]))
for o in out:
    if not o["pts"]:
        print("  ", o["obj"], o.get("note"))
