# check_admin_portal.py — замурован ли парадный вход, БЕЗ Unity: только JSON боксов.
#
# Объём проёма взят из замера RAW (dbg_admin_portal.py) и переведён в локальные оси
# PRIMUM_COLLIDERS той же формулой, что и боксы. Скрипт печатает, какие боксы его задевают.
# Чистая арифметика, bpy не нужен (но запускается питоном Blender'а — он в проекте гарантирован).
#
# Запуск: blender -b --factory-startup --python check_admin_portal.py
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
DOCS = os.path.dirname(HERE)
JSON = os.path.join(DOCS, "houses_boxes_ADMIN.json")

# Парадный портал ADM_DOOR_PORTAL_FRAME (Blender x -73.45..-73.15, y 4..16, z 24.45..28.45),
# переведённый в локальные оси PRIMUM_COLLIDERS: local = (-s.x, s.z, -s.y), s = C + (p-C)*1.07
K = 1.07
C = (7.5, -148.0, 18.5)


def to_local(p):
    s = [C[i] + (p[i] - C[i]) * K for i in range(3)]
    return (-s[0], s[2], -s[1])


def span(lo, hi, axis):
    a = to_local([0.0, 0.0, 0.0])
    p0 = [0.0, 0.0, 0.0]
    p1 = [0.0, 0.0, 0.0]
    p0[axis] = lo
    p1[axis] = hi
    l0 = to_local(p0)[axis if axis == 0 else (2 if axis == 1 else 1)]
    l1 = to_local(p1)[axis if axis == 0 else (2 if axis == 1 else 1)]
    return (min(l0, l1), max(l0, l1))


PX = span(-73.45, -73.15, 0)     # толщина полотна по X
PY = span(24.45, 28.45, 2)       # высота (Blender Z -> local Y)
PZ = span(4.00, 16.00, 1)        # ширина по фасаду (Blender Y -> local Z)

print("парадный проём, локально:")
print("  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (PX[0], PX[1], PY[0], PY[1], PZ[0], PZ[1]))
print("  размер %.2f x %.2f x %.2f м (x, y, z)" % (PX[1] - PX[0], PY[1] - PY[0], PZ[1] - PZ[0]))

PAD = 0.05
with open(JSON, encoding="utf-8") as fh:
    data = json.load(fh)

hit = []
for chunk in data["chunks"]:
    for grp in ("walls", "floors", "roofs", "stairs"):
        for b in chunk.get(grp, []):
            if abs(b.get("ry", 0.0)) > 0.5 or b.get("q"):
                continue                     # повёрнутые/наклонные пропускаем: стены осевые
            c, s = b["c"], b["s"]
            bx = (c[0] - s[0] * 0.5, c[0] + s[0] * 0.5)
            by = (c[1] - s[1] * 0.5, c[1] + s[1] * 0.5)
            bz = (c[2] - s[2] * 0.5, c[2] + s[2] * 0.5)
            ox = PX[0] - PAD <= bx[1] and bx[0] - PAD <= PX[1]
            oy = PY[0] - PAD <= by[1] and by[0] - PAD <= PY[1]
            oz = PZ[0] - PAD <= bz[1] and bz[0] - PAD <= PZ[1]
            if ox and oy and oz:
                hit.append((grp, b))

print("")
print("боксов, задевающих объём проёма: %d" % len(hit))
for grp, b in hit:
    c, s = b["c"], b["s"]
    print("  %-7s c=(%7.2f %7.2f %7.2f)  s=(%6.2f %5.2f %6.2f)" %
          (grp, c[0], c[1], c[2], s[0], s[1], s[2]))

# Перекрытие оценивается ПО ХОДОВОЙ ПОЛОСЕ: перемычка над проёмом — не помеха.
WALK = 2.20
walk_lo, walk_hi = PY[0], min(PY[1], PY[0] + WALK)
print("")
print("ходовая полоса проёма: y %.2f..%.2f (%.2f м над порогом)" % (walk_lo, walk_hi, WALK))
blockers = 0
for grp, b in hit:
    c, s = b["c"], b["s"]
    by = (c[1] - s[1] * 0.5, c[1] + s[1] * 0.5)
    if by[0] - PAD <= walk_hi and walk_lo - PAD <= by[1]:
        blockers += 1
        print("  ! в ходовой полосе: %s, перекрывает y %.2f..%.2f" %
              (grp, max(by[0], walk_lo), min(by[1], walk_hi)))

print("")
print("ВЕРДИКТ: %s" % ("ПРОЁМ ЗАМУРОВАН" if blockers else "проход свободен"))
