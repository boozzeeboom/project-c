# dbg_admin_holes.py — есть ли в плитах ADMIN ДЫРЫ (шахты лестниц, витраж библиотеки).
#
# Зачем: плиты у ADMIN есть объектами (ADM_F1..F4_FLOOR и т.д.), но бокс по габариту
# сплошной. Лестница же приходит своим верхом РОВНО на верх плиты (ADM_STAIR_M1_B
# 26.85..28.85 при плите 28.45..28.85) — значит в меше плиты обязан быть проём, иначе
# игрок упирается в перекрытие. Этот скрипт отвечает численно: площадь меша против
# площади габарита + карта попаданий луча сверху вниз (ASCII). Только чтение.
#
# Запуск: DBG_CHUNK=PRIMUM_ADMIN_S0_ADM_P1_01H.blend blender -b --factory-startup --python dbg_admin_holes.py
import bpy, os, sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX
import houses_boxes as HB

out = []


def say(s):
    print(s, flush=True)
    out.append(s)


CHUNK = os.environ.get("DBG_CHUNK", "PRIMUM_ADMIN_S0_ADM_P1_01H.blend")
bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, CHUNK))

TARGETS = [
    "ADM_F1_FLOOR", "ADM_F2_FLOOR", "ADM_F3_FLOOR", "ADM_F4_FLOOR",
    "ADM_ROOF_SLAB", "ADM_SLAB_DOME", "ADM_LIB_GALLERY",
    "ADM_WING_N_F1_FLOOR", "ADM_WING_N_F2_FLOOR", "ADM_WING_N_ROOF_SLAB",
    "ADM_PLINTH",
]

NX, NY = 56, 40
UP = 0.50        # м, откуда бросаем луч над верхом плиты
DOWN = 2.50      # м, длина луча

# все STAIR-объекты — чтобы на карте видеть, где именно лестница
stairs = []
for o in bpy.data.objects:
    if o.type != 'MESH' or o.library is not None or o.data is None or len(o.data.polygons) == 0:
        continue
    if HX.has_ancestor(o, HX.ANCESTORS):
        continue
    toks = HX.base_tokens(o.name)
    if HB.strict_class(o, toks)[0] == "STAIR":
        bb = HB.aabb(HB.obox(o))
        stairs.append((o.name, 0.5 * (bb[0] + bb[1]), 0.5 * (bb[2] + bb[3])))

say("=== ADMIN %s : дыры в плитах ===" % CHUNK)
say("STAIR-объектов: %d" % len(stairs))
say("")
say("--- площадь меша против площади габарита (сплошная плита ~100%) ---")

maps = []
for nm in TARGETS:
    o = bpy.data.objects.get(nm)
    if o is None:
        say("  %-26s ОТСУТСТВУЕТ" % nm)
        continue
    bb = HB.aabb(HB.obox(o))
    dx, dy, dz = HB.dims(bb)
    area = sum(p.area for p in o.data.polygons)
    full = dx * dy
    say("  %-26s d=(%6.2f %6.2f %5.2f) z=%.2f..%.2f  меш=%9.1f  габарит=%9.1f  покрытие=%5.1f%%" %
        (nm, dx, dy, dz, bb[4], bb[5], area, full, 100.0 * area / max(1e-6, 2.0 * full)))

    # карта: луч сверху вниз
    inv = o.matrix_world.inverted()
    grid = []
    for j in range(NY):
        wy = bb[3] - (j + 0.5) * (dy / NY)
        row = []
        for i in range(NX):
            wx = bb[0] + (i + 0.5) * (dx / NX)
            p0 = inv @ Vector((wx, wy, bb[5] + UP))
            d = (inv.to_3x3() @ Vector((0.0, 0.0, -1.0))).normalized()
            hit = o.ray_cast(p0, d, distance=DOWN / max(1e-6, o.matrix_world.to_scale().z))[0]
            row.append("#" if hit else ".")
        grid.append(row)
    for sn, sx, sy in stairs:
        if not (bb[0] <= sx <= bb[1] and bb[2] <= sy <= bb[3]):
            continue
        i = int((sx - bb[0]) / (dx / NX))
        j = int((bb[3] - sy) / (dy / NY))
        if 0 <= i < NX and 0 <= j < NY and grid[j][i] == ".":
            grid[j][i] = "S"
    holes = sum(r.count(".") for r in grid)
    say("      дыр в сетке %dx%d: %d (%.1f%% площади)" % (NX, NY, holes, 100.0 * holes / (NX * NY)))
    maps.append((nm, grid))

say("")
say("--- КАРТЫ: '#'=плита есть, '.'=дыра, 'S'=центр марша над дырой ---")
for nm, grid in maps:
    say("")
    say("  " + nm)
    for row in grid:
        say("    " + "".join(row))

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "houses_admin_holes.txt")
with open(os.path.abspath(path), "w", encoding="utf-8") as fh:
    fh.write("\n".join(out) + "\n")
print("DONE", flush=True)
