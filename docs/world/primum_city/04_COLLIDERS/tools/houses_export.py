# houses_export.py — точная геометрия домов PRIMUM по классам, для MeshCollider'ов.
#
# Зачем: бокс по габариту не умеет дыр — из-за этого двери/окна заливались, а слэбы с проёмом
# лестницы и крыши «по дому» заваривали марши (три неудачные попытки, см. 04_STATUS_AND_GAPS.md).
# Здесь берётся ИСХОДНАЯ (несклеенная) геометрия RAW-чанков, отбирается по грамматике имён
#   <ДОМ>_<слот/этаж>_<ВИД>_...
# и выгружается в ОДИН OBJ на чанк: одна группа `o` на (класс × дом).
#
# Классы: WALL (стены, с настоящими проёмами), FLOOR (FLOOR + SLAB — этажные перекрытия),
#         ROOF (крыши/парапеты), STAIR (марши, STEPS, PORCH, VER — точные ступени и площадки).
# Не берём: окна (WIN), двери (DOOR), мебель (FURN), декор (DTL/FAC/TQ/PLANT/SIGN).
#
# Выход: docs/world/primum_city/04_COLLIDERS/houses_obj/<CHUNK>.obj
# Координаты — локальные координаты корня PRIMUM_COLLIDERS: local = (-s.x, s.z, -s.y),
# s = C + (p - C)*1.07  (та же формула, что в stairs_export.py / colliders_v3.py).
#
# Запуск:
#   "W:\...\blender.exe" -b --factory-startup --python houses_export.py
#   EXPORT_CHUNKS="S9_TRD-S2,S6_NQ" — фильтр по подстроке имени чанка
import bpy, os, time
from mathutils import Vector
from collections import defaultdict

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW")
OUTDIR = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\houses_obj"

K = 1.07
C = (7.5, -148.0, 18.5)

# виды деталей, которые берём в физику
STAIR_KINDS = {"STAIR", "STAIRS", "STEPS", "STEP", "PORCH", "STAIRCASE"}
FLOOR_KINDS = {"FLOOR", "FLOORS", "SLAB", "SLABS", "VER", "VERANDA", "GALLERY"}
ROOF_KINDS = {"ROOF", "ROOFS", "PARAPET", "CORBEL"}
WALL_KINDS = {"WALL", "WALLS", "SHELL", "PART", "PARTITION", "COLUMN", "COL", "ARCADE", "ARCH", "BEAM"}
# что игнорируем (мебель, окна, двери, декор)
DENY = {"WIN", "WINDOW", "WINBOX", "WINPLANT", "DOOR", "DOORS", "FURN", "FURNITURE",
        "DTL", "DETAIL", "DETAILS", "FAC", "FACADE", "PLANT", "SIGN", "BANNER",
        "RUG", "CARPET", "CLOTH", "LAMP", "LANTERN", "HANG", "DECOR"}


def ulocal(p):
    s = [C[i] + (p[i] - C[i]) * K for i in range(3)]
    return (-s[0], s[2], -s[1])


def has_ancestor(o, names):
    p = o.parent
    while p is not None:
        if p.name in names:
            return True
        p = p.parent
    return False


def klass(tokens):
    """Класс детали по токенам имени, либо None."""
    t = set(tokens)
    if t & DENY:
        return None
    if t & STAIR_KINDS:
        return "STAIR"
    if t & ROOF_KINDS:
        return "ROOF"
    if t & FLOOR_KINDS:
        return "FLOOR"
    if t & WALL_KINDS:
        return "WALL"
    return None


def house_of(tokens):
    """Дом = токены до первого «этажного» (чистое число) или видового токена."""
    kind_all = STAIR_KINDS | FLOOR_KINDS | ROOF_KINDS | WALL_KINDS | DENY
    out = []
    for t in tokens:
        if t in kind_all or (t.isdigit() and len(t) <= 2):
            break
        out.append(t)
    return "_".join(out) if out else "_".join(tokens[:2])


def list_chunks():
    out = []
    for f in sorted(os.listdir(RAW)):
        u = f.upper()
        if not u.startswith("PRIMUM_HOUSES_") or not u.endswith(".BLEND"):
            continue
        if "COPY" in u or "BAK" in u:
            continue
        out.append(f)
    return out


def export_chunk(fname):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(RAW, fname))
    chunk = fname.replace(".blend", "")
    groups = defaultdict(list)
    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None:
            continue
        if o.data is None or len(o.data.polygons) == 0:
            continue
        if has_ancestor(o, ("DOORS", "LAMPS", "OUTLIERS", "DOOR_PARTS_UNASSIGNED")):
            continue
        toks = o.name.upper().split("_")
        cls = klass(toks)
        if cls is None:
            continue
        groups[(cls, house_of(toks))].append(o)

    if not groups:
        return 0, 0, {}

    os.makedirs(OUTDIR, exist_ok=True)
    out = os.path.join(OUTDIR, chunk + ".obj")
    nv = 0
    nt = 0
    bb = [1e9, -1e9, 1e9, -1e9, 1e9, -1e9]
    per_cls = defaultdict(lambda: [0, 0])   # cls -> [objects, tris]
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("# PRIMUM houses exact geometry for MeshCollider: %s\n" % chunk)
        fh.write("# coords = PRIMUM-collider-root local space (-s.x, s.z, -s.y), s = C + (p-C)*1.07\n")
        for (cls, house) in sorted(groups):
            objs = groups[(cls, house)]
            name = ("%s__%s__%s" % (chunk, cls, house))[:120].replace(" ", "_")
            fh.write("o %s\n" % name)
            base = nv + 1
            for o in objs:
                me = o.data
                me.calc_loop_triangles()
                vbase = nv + 1
                mw = o.matrix_world
                for v in me.vertices:
                    p = mw @ v.co
                    ux, uy, uz = ulocal((p.x, p.y, p.z))
                    fh.write("v %.4f %.4f %.4f\n" % (ux, uy, uz))
                    nv += 1
                    if ux < bb[0]: bb[0] = ux
                    if ux > bb[1]: bb[1] = ux
                    if uy < bb[2]: bb[2] = uy
                    if uy > bb[3]: bb[3] = uy
                    if uz < bb[4]: bb[4] = uz
                    if uz > bb[5]: bb[5] = uz
                for lt in me.loop_triangles:
                    a, b, c = lt.vertices
                    fh.write("f %d %d %d\n" % (vbase + c, vbase + b, vbase + a))
                    nt += 1
                per_cls[cls][0] += 1
    print("    bbox local x[%.0f..%.0f] y[%.0f..%.0f] z[%.0f..%.0f]" %
          (bb[0], bb[1], bb[2], bb[3], bb[4], bb[5]), flush=True)
    return nv, nt, per_cls


def main():
    flt = os.environ.get("EXPORT_CHUNKS", "").strip()
    chunks = list_chunks()
    if flt:
        want = [s.strip().upper() for s in flt.split(",") if s.strip()]
        chunks = [c for c in chunks if any(w in c.upper() for w in want)]
    print("=== houses_export: чанков %d ===" % len(chunks), flush=True)
    t0 = time.time()
    tot_v = tot_t = 0
    for f in chunks:
        try:
            nv, nt, per = export_chunk(f)
        except Exception as e:
            print("ERROR %s: %s" % (f, e), flush=True)
            continue
        tot_v += nv
        tot_t += nt
        print("  %-44s verts=%8d tris=%8d  %s" % (
            f.replace("PRIMUM_HOUSES_", "").replace(".blend", ""), nv, nt,
            " ".join("%s=%d" % (k, v[0]) for k, v in sorted(per.items()))), flush=True)
    print("=== ИТОГО verts=%d tris=%d  (%.1f c) ===" % (tot_v, tot_t, time.time() - t0), flush=True)


main()
print("DONE")
