# dbg_doors.py — почему INT_DOOR не режется: печатает стены и двери указанного дома.
import bpy, os, sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX

HOUSE = os.environ.get("DBG_HOUSE", "RES_SE_06")
CHUNK = os.environ.get("DBG_CHUNK", "PRIMUM_HOUSES_S7_SE_P1_46H.blend")

bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, CHUNK))


def bb(o):
    m = o.matrix_world
    ps = [m @ Vector(c) for c in o.bound_box]
    return (min(p.x for p in ps), max(p.x for p in ps), min(p.y for p in ps),
            max(p.y for p in ps), min(p.z for p in ps), max(p.z for p in ps))


def show(o, tag):
    b = bb(o)
    d = (b[1] - b[0], b[3] - b[2], b[5] - b[4])
    print("%-26s %-22s c=(%7.2f %7.2f %7.2f) d=(%5.2f %5.2f %5.2f) dims_sorted=%s"
          % (tag, o.name[:22], 0.5 * (b[0] + b[1]), 0.5 * (b[2] + b[3]), 0.5 * (b[4] + b[5]),
             d[0], d[1], d[2], ["%.2f" % x for x in sorted(d)]), flush=True)


print("=== WALL-класс дома %s ===" % HOUSE, flush=True)
for o in bpy.data.objects:
    if o.type != 'MESH' or o.data is None or len(o.data.polygons) == 0:
        continue
    toks = HX.base_tokens(o.name)
    if HX.house_of(toks) != HOUSE:
        continue
    cls, rule = HX.klass_of(o, toks)
    al = [HX.alpha(t) for t in toks]
    if any(t in ("DOOR", "DOORS") for t in al):
        show(o, "DOOR")
    elif cls == "WALL":
        show(o, "WALL/" + rule)

print("=== все DOOR-объекты в радиусе 3 м от дома (по имени) ===", flush=True)
print("DONE", flush=True)
