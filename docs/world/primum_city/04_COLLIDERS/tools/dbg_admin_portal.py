# dbg_admin_portal.py — мировой габарит парадного входа и его окружения.
#
# Зачем: `ADM_DOOR_PORTAL_GLASS` (11.80 x 3.20) в записи проёмов НЕ попадает — значит стена
# может быть не разрезана и главный вход окажется замурован. Нужны точные координаты,
# чтобы проверить объём проёма уже в Unity по боксам.
#
# Только чтение.
# Запуск: DBG_CHUNK=PRIMUM_ADMIN_S0_ADM_P1_01H.blend blender -b --factory-startup --python dbg_admin_portal.py
import bpy, os, sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX
import houses_boxes as HB

out = []


def say(s):
    print(s, flush=True)
    out.append(s)


CHUNK = os.environ.get("DBG_CHUNK", "PRIMUM_ADMIN_S0_ADM_P1_01H.blend")
bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, CHUNK))

WANT = ("ADM_DOOR_PORTAL", "ADM_FAC_PORT", "ADM_HALL_THRESHOLD", "ADM_PLINTH",
        "ADM_STAIR_ENTRY", "ADM_HALL_VEST")
say("=== ADMIN %s : парадный вход ===" % CHUNK)
say("%-34s %-42s %s" % ("объект", "мировой габарит x0..x1 y0..y1 z0..z1", "локально (PRIMUM_COLLIDERS)"))

rows = []
for o in bpy.data.objects:
    if o.type != 'MESH' or o.library is not None or o.data is None or len(o.data.polygons) == 0:
        continue
    if HX.has_ancestor(o, HX.ANCESTORS):
        continue
    if not o.name.upper().startswith(WANT):
        continue
    bb = HB.aabb(HB.obox(o))
    c = ((bb[0] + bb[1]) * 0.5, (bb[2] + bb[3]) * 0.5, (bb[4] + bb[5]) * 0.5)
    loc = HB.to_local(c)
    rows.append((o.name, bb, loc))

for nm, bb, loc in sorted(rows):
    say("%-34s %8.2f..%8.2f %8.2f..%8.2f %7.2f..%7.2f   (%.3f, %.3f, %.3f)" %
        (nm[:34], bb[0], bb[1], bb[2], bb[3], bb[4], bb[5], loc[0], loc[1], loc[2]))

say("")
say("--- луч вниз из центра каждого проёма (есть ли пол под порогом) ---")
for nm, bb, loc in sorted(rows):
    if "PORTAL" not in nm:
        continue
    cx = (bb[0] + bb[1]) * 0.5
    cy = (bb[2] + bb[3]) * 0.5
    hit = None
    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None or o.data is None:
            continue
        ok = o.ray_cast(o.matrix_world.inverted() @ __import__("mathutils").Vector((cx, cy, bb[5] + 1.0)),
                        (o.matrix_world.inverted().to_3x3() @ __import__("mathutils").Vector((0.0, 0.0, -1.0))).normalized(),
                        distance=4.0)
        if ok[0]:
            hit = o.name
            break
    say("  %-34s центр (%.2f, %.2f)  пол: %s" % (nm[:34], cx, cy, hit or "НЕТ"))

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "houses_admin_portal.txt")
with open(os.path.abspath(path), "w", encoding="utf-8") as fh:
    fh.write("\n".join(out) + "\n")
print("DONE", flush=True)
