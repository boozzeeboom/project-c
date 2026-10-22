# dbg_stairs.py — соотношение плит и маршей для дома: печатает плиты и марши с их Z.
import bpy, os, sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX

HOUSE = os.environ.get("DBG_HOUSE", "RES_E_01")
CHUNK = os.environ.get("DBG_CHUNK", "PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H.blend")

bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, CHUNK))


def bb(o):
    m = o.matrix_world
    ps = [m @ Vector(c) for c in o.bound_box]
    return (min(p.x for p in ps), max(p.x for p in ps), min(p.y for p in ps),
            max(p.y for p in ps), min(p.z for p in ps), max(p.z for p in ps))


print("=== дом %s в %s ===" % (HOUSE, CHUNK), flush=True)
for o in bpy.data.objects:
    if o.type != 'MESH' or o.data is None or len(o.data.polygons) == 0:
        continue
    toks = HX.base_tokens(o.name)
    if HX.house_of(toks) != HOUSE:
        continue
    cls, rule = HX.klass_of(o, toks)
    if cls not in ("FLOOR", "STAIR"):
        continue
    b = bb(o)
    print("%-6s %-28s z %7.2f..%7.2f  x %7.2f..%7.2f  y %7.2f..%7.2f  (%s)"
          % (cls, o.name[:28], b[4], b[5], b[0], b[1], b[2], b[3], rule), flush=True)
print("DONE", flush=True)
