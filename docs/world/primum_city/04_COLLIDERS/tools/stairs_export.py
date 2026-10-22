# stairs_export.py — геометрия ВСЕХ лестниц (BASE и внутренние) для MeshCollider'ов.
# Наклонный бокс по маршу неверен: у L-образных лестниц получалось 2–3 наложенных бокса,
# у террасных BASE — плита 27° вместо реальных 21.8°, и её верхняя часть висела над маршем
# прямо под декой (пройти нельзя). Поэтому ВСЕ лестницы — точной геометрией: один меш на чанк.
#
# Выход: docs/world/primum_city/04_COLLIDERS/stairs.obj
# Локальные координаты PRIMUM: local = (-s.x, s.z, -s.y), s = C + (p - C)*1.07.
import bpy, os
from mathutils import Vector
from collections import defaultdict

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW")
OUT = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\stairs.obj"

K = 1.07
C = (7.5, -148.0, 18.5)
RAIL = ("RAIL", "GUARD", "HAND", "SKIRT", "BALUST", "BAL_", "_BAL", "PARAPET", "_BR", "KERB", "KICK", "FURN", "RUG", "CARPET", "COVER")

CHUNKS = [
    ("PRIMUM_BASE_S0_CITY_P1_00H.blend", "BASE"),
    ("PRIMUM_ADMIN_S0_ADM_P1_01H.blend", "ADMIN"),
    ("PRIMUM_HOUSES_S2_TRD_P1_28H.blend", "S2TRDP1"),
    ("PRIMUM_HOUSES_S3_TRD_P2_26H.blend", "S3TRDP2"),
    ("PRIMUM_HOUSES_S3_TRD-ES_P1_37H.blend", "S3TRDESP1"),
    ("PRIMUM_HOUSES_S4_TRD_P3_26H.blend", "S4TRDP3"),
    ("PRIMUM_HOUSES_S9_TRD-N_P1_28H.blend", "S9TRDN"),
    ("PRIMUM_HOUSES_S9_TRD-S_P1_16H.blend", "S9TRDS"),
]

def ulocal(p):
    s = [C[i] + (p[i] - C[i]) * K for i in range(3)]
    return (-s[0], s[2], -s[1])

def cpath(o):
    return "/".join([c.name for c in o.users_collection]).upper()

def is_stair_part(o):
    p = cpath(o); n = o.name.upper()
    if "STAIR" not in p and "STAIR" not in n and "STEPS" not in p:
        return False
    return not any(t in n for t in RAIL)

def has_ancestor(o, names):
    p = o.parent
    while p is not None:
        if p.name in names: return True
        p = p.parent
    return False

n_vert = 0
written = []
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write("# PRIMUM interior stairs (exact geometry for MeshCollider), Unity-local coords\n")
    for fname, short in CHUNKS:
        path = os.path.join(RAW, fname)
        if not os.path.exists(path):
            print("MISSING " + fname); continue
        bpy.ops.wm.open_mainfile(filepath=path)
        # группировка по лестнице: ближайший сегмент коллекции со STAIR, иначе имя объекта
        groups = defaultdict(list)
        for o in bpy.data.objects:
            if o.type != 'MESH' or o.library is not None: continue
            if has_ancestor(o, ("DOORS", "LAMPS", "OUTLIERS")): continue
            if not is_stair_part(o): continue
            parts_name = o.name.split("_")
            key = "_".join(parts_name[:3]) if len(parts_name) >= 3 else o.name
            groups[key].append(o)

        cnt = 0
        for key, objs in groups.items():
            name = ("STAIR_%s_%s" % (short, key)).replace(" ", "_").replace("-", "").replace(".", "")
            name = name[:60]
            fh.write("o %s\n" % name)
            base = n_vert + 1
            tris = 0
            for o in objs:
                me = o.data
                me.calc_loop_triangles()
                vbase = n_vert + 1
                for v in me.vertices:
                    p = o.matrix_world @ v.co
                    ux, uy, uz = ulocal((p.x, p.y, p.z))
                    fh.write("v %.4f %.4f %.4f\n" % (ux, uy, uz))
                    n_vert += 1
                for lt in me.loop_triangles:
                    a, b, c = lt.vertices
                    fh.write("f %d %d %d\n" % (vbase + c, vbase + b, vbase + a))
                    tris += 1
            written.append((name, tris))
            cnt += 1
        print("%-14s лестниц=%d" % (short, cnt))

print("=== stairs.obj ===")
for w in written[:15]:
    print("  %-46s tris=%d" % w)
print("всего лестниц: %d, вершин: %d" % (len(written), n_vert))
print("DONE")
