# houses_export.py — точная геометрия домов PRIMUM по классам, для MeshCollider'ов.
#
# Зачем: бокс по габариту не умеет дыр — из-за этого двери/окна заливались, а слэбы с проёмом
# лестницы и крыши «по дому» заваривали марши (три неудачные попытки, см. 04_STATUS_AND_GAPS.md).
# Здесь берётся ИСХОДНАЯ (несклеенная) геометрия RAW-чанков, отбирается по грамматике имён
#   <ДОМ>_<слот/этаж>_<ВИД>_...
# и выгружается в ОДИН OBJ на чанк: одна группа `o` на (класс × дом).
#
# Классы: WALL (стены и перегородки, с настоящими проёмами), FLOOR (FLOOR + SLAB — этажные
#         перекрытия), ROOF (крыши/парапеты), STAIR (марши, ступени, крыльца).
# Не берём: окна (WIN), двери (DOOR), мебель (FURN), декор (DTL/FAC/PLANT/SIGN), перила.
#
# ПРАВКА 2 (по замечаниям после первой сборки — «много дыр» и «нет перекрытий между этажами»):
#   1) плиты этажей в модели названы SLAB1/SLAB2, F2_*/F3_*, ATTIC — прежний словарь их не знал
#      и выбрасывал целиком (см. houses_gaps.log: RES_NE_01_F2_W, TRD_S_17_SLAB2_01 …);
#   2) внутренние стены/перегородки названы INT_WA_0, INT_W_SEG_L, PART2_E, BACKWALL_W, W2_* —
#      по прежним правилам тоже уходили в NO_KIND, из-за чего межкомнатных стен не было вовсе;
#   3) суффикс копий Blender (.001/.002) ломал разбор вида: FLOOR.001 -> не FLOOR;
#   4) марши в S9_TRD-S2 названы ST_FLIGHT — не попадали в STAIR;
#   5) добавлен ГЕОМЕТРИЧЕСКИЙ ЗАПАСНОЙ ВАРИАНТ: крупный объект без видового токена, но явно
#      плоская плита (dz << min(dx,dy)) уходит в FLOOR, а явно тонкая и высокая (min(dx,dy) <<
#      max dim) — в WALL. Двухмысленные «кубы» (памятники, тумбы) не берём: лучше пропуск, чем
#      заваренный проход.
#
# Выход: docs/world/primum_city/04_COLLIDERS/houses_obj/<CHUNK>.obj
# Координаты — локальные координаты корня PRIMUM_COLLIDERS: local = (-s.x, s.z, -s.y),
# s = C + (p - C)*1.07  (та же формула, что в stairs_export.py / colliders_v3.py).
#
# Запуск:
#   "W:\...\blender.exe" -b --factory-startup --python houses_export.py
#   EXPORT_CHUNKS="S9_TRD-S2,S6_NQ" — фильтр по подстроке имени чанка
import bpy, os, time, re
from mathutils import Vector
from collections import defaultdict

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW")
OUTDIR = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\houses_obj"

K = 1.07
C = (7.5, -148.0, 18.5)

# ---------------------------------------------------------------- словари видов
STAIR_NAMES = {"STAIR", "STAIRS", "STAIRCASE", "STEPS", "STEP", "PORCH", "FLIGHT", "ST"}
FLOOR_NAMES = {"FLOOR", "FLOORS", "SLAB", "SLABS", "VER", "VERANDA", "GALLERY",
               "ATTIC", "MEZZ", "MEZZANINE", "PLATFORM"}
ROOF_NAMES = {"ROOF", "ROOFS", "PARAPET", "CORBEL", "GABLE", "CORNICE", "AWNING",
              "TENT", "HIP", "HIPGABLE", "MANSARD", "DORMER"}
WALL_NAMES = {"WALL", "WALLS", "SHELL", "PART", "PARTITION", "PARTITIONS", "COLUMN",
              "COL", "ARCADE", "ARCH", "BEAM", "BACKWALL", "PILASTER", "PILLAR",
              "PANEL", "PANE", "LINTEL", "JAMB"}
# что физически не нужно и опасно (двери/окна/мебель/декор/перила/сантехника)
HARD_DENY = {"WIN", "WINDOW", "WINBOX", "WINPLANT", "DOOR", "DOORS", "FURN", "FURNITURE",
             "DTL", "DETAIL", "DETAILS", "SIGN", "BANNER", "RUG", "CARPET", "CLOTH",
             "LAMP", "LANTERN", "HANG", "DECOR", "PLANT", "FAC", "FACADE",
             "BAL", "BALS", "BALUST", "BALUSTER", "RAIL", "RAILS", "RAILING",
             "GUARD", "HANDRAIL", "HAND", "SKIRT", "KICK", "CAP", "CAPS",
             "WC", "BED", "ISLAND", "COUNTER", "SHELF", "SHELVES", "MIRROR",
             "SINK", "TOILET", "TUB", "GUTTER", "PLINTH"}
KIND_ALL = STAIR_NAMES | FLOOR_NAMES | ROOF_NAMES | WALL_NAMES | HARD_DENY
ANCESTORS = ("DOORS", "LAMPS", "OUTLIERS", "DOOR_PARTS_UNASSIGNED")

# геометрический запасной вариант
BIG_MIN = 1.5        # м, минимальный габарит для запасного класса
PLATE_T = 0.30       # dz <= PLATE_T * max  => плита
WALL_T = 0.30        # min(dx,dy) <= WALL_T * max => стена (при достаточной высоте)


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


def base_tokens(name):
    """UPPER, без суффикса копий Blender (.001) — иначе FLOOR.001 перестаёт быть FLOOR."""
    out = []
    for t in name.upper().split("_"):
        t = t.split(".")[0]
        if t:
            out.append(t)
    return out


def alpha(t):
    """Токен без хвостовых цифр: SLAB1 -> SLAB, PART2 -> PART, F2 -> F."""
    return re.sub(r"\d+$", "", t)


def klass_of(o, toks):
    """(класс, правило). Правило нужно для отчёта — видно, чем именно объект попал в физику."""
    if any(t in HARD_DENY for t in toks):
        return None, "deny"
    a = [alpha(t) for t in toks]

    if any(t in STAIR_NAMES for t in a):
        return "STAIR", "name"
    if any(t in FLOOR_NAMES for t in a) or any(t.startswith("SLAB") for t in toks):
        return "FLOOR", "name"
    # F2_*/F3_* — плиты этажей, но только не для внутренних деталей (INT_F2_P — это стена)
    if not any(t.startswith("INT") for t in toks) \
            and any(re.fullmatch(r"F\d+", t) for t in toks):
        return "FLOOR", "name"
    if any(t in ROOF_NAMES for t in a) or any(t.startswith("RF") for t in toks):
        return "ROOF", "name"
    if any(t in WALL_NAMES for t in a) or any(t.startswith("INT") for t in toks) \
            or any(t.startswith("FILL") for t in toks):
        return "WALL", "name"

    # ---- запасной вариант по габаритам: только однозначные формы ----
    dx, dy, dz = o.dimensions
    mx = max(dx, dy, dz)
    if mx < BIG_MIN:
        return None, "small"
    mn_h = min(dx, dy)
    if dz <= PLATE_T * mx and dz <= 0.60 * mn_h:
        return "FLOOR", "bbox-plate"
    if mn_h <= WALL_T * mx and dz >= 0.50 * mx:
        return "WALL", "bbox-wall"
    return None, "ambiguous"


def house_of(tokens):
    """Дом = токены до первого видового токена; последний чисто-цифровой токен — этаж,
    если перед ним тоже стоит цифра (RES_S2_02_0_WALL -> RES_S2_02, но RES_NE_01_F2_W -> RES_NE_01)."""
    out = []
    for t in tokens:
        if t in KIND_ALL or t.startswith("INT") or t.startswith("SLAB") \
                or t.startswith("RF") or re.fullmatch(r"F\d+", t):
            break
        out.append(t)
    if len(out) >= 2 and out[-1].isdigit() and out[-2].isdigit():
        out = out[:-1]
    return "_".join(out) if out else "_".join(tokens[:2])


def add_level_audit(objs_by_house, house_floors, house_z, chunk, stats):
    """Сколько у дома уровней перекрытий против числа этажей (по высоте коробки)."""
    for house in sorted(house_floors):
        levs = []
        for z in sorted(house_floors[house]):
            if not levs or z - levs[-1] > 0.5:
                levs.append(z)
        z0, z1 = house_z[house]
        h = z1 - z0
        storeys = max(1, int(round(h / 3.2)))
        stats["houses"] += 1
        if storeys >= 2:
            stats["multi"] += 1
            if len(levs) < 2:
                stats["no_upper_floor"] += 1
                if len(stats["examples"]) < 5:
                    stats["examples"].append("%s h=%.1f m уровней=%d" % (house, h, len(levs)))


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
    dropped = defaultdict(lambda: [0, 0.0])      # правило -> [объектов, м3 габарита]
    by_rule = defaultdict(int)
    house_floors = defaultdict(list)             # дом -> z верхних граней плит
    house_z = defaultdict(lambda: [1e9, -1e9])

    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None:
            continue
        if o.data is None or len(o.data.polygons) == 0:
            continue
        if has_ancestor(o, ANCESTORS):
            continue
        toks = base_tokens(o.name)
        cls, rule = klass_of(o, toks)
        if cls is None:
            d = o.dimensions
            if max(d) >= BIG_MIN and rule == "ambiguous":
                dropped[rule][0] += 1
                dropped[rule][1] += d.x * d.y * d.z
            continue
        groups[(cls, house_of(toks))].append(o)
        by_rule[rule] += 1
        h = house_of(toks)
        zt = (o.matrix_world @ Vector(o.bound_box[6]))[2]      # верх габарита объекта
        if cls == "FLOOR":
            house_floors[h].append(zt)
        zlo = min((o.matrix_world @ Vector(b))[2] for b in o.bound_box)
        zhi = max((o.matrix_world @ Vector(b))[2] for b in o.bound_box)
        house_z[h][0] = min(house_z[h][0], zlo)
        house_z[h][1] = max(house_z[h][1], zhi)

    if not groups:
        return 0, 0, {}, by_rule, {}, {}

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
    if dropped:
        print("    отброшено неоднозначных крупных: %d" % dropped["ambiguous"][0], flush=True)
    return nv, nt, per_cls, by_rule, house_floors, house_z


def main():
    flt = os.environ.get("EXPORT_CHUNKS", "").strip()
    chunks = list_chunks()
    if flt:
        want = [s.strip().upper() for s in flt.split(",") if s.strip()]
        chunks = [c for c in chunks if any(w in c.upper() for w in want)]
    print("=== houses_export: чанков %d ===" % len(chunks), flush=True)
    t0 = time.time()
    tot_v = tot_t = 0
    tot_rule = defaultdict(int)
    tot_stats = defaultdict(int)
    tot_stats["examples"] = []
    for f in chunks:
        try:
            nv, nt, per, by_rule, house_floors, house_z = export_chunk(f)
        except Exception as e:
            print("ERROR %s: %s" % (f, e), flush=True)
            continue
        stats = defaultdict(int)
        stats["examples"] = []
        add_level_audit(None, house_floors, house_z, f, stats)
        for k in ("houses", "multi", "no_upper_floor"):
            tot_stats[k] += stats[k]
        tot_stats["examples"].extend(stats["examples"][:2])
        for k, v in by_rule.items():
            tot_rule[k] += v
        tot_v += nv
        tot_t += nt
        print("  %-44s verts=%8d tris=%8d  %s  [%s]" % (
            f.replace("PRIMUM_HOUSES_", "").replace(".blend", ""), nv, nt,
            " ".join("%s=%d" % (k, v[0]) for k, v in sorted(per.items())),
            " ".join("%s=%d" % kv for kv in sorted(tot_rule.items()))), flush=True)
    print("=== ИТОГО verts=%d tris=%d  (%.1f c) ===  правила: %s" % (
        tot_v, tot_t, time.time() - t0,
        " ".join("%s=%d" % kv for kv in sorted(tot_rule.items()))), flush=True)
    print("=== ЭТАЖИ: домов=%d, многоэтажных=%d, из них без верхнего перекрытия=%d ===" % (
        tot_stats["houses"], tot_stats["multi"], tot_stats["no_upper_floor"]), flush=True)
    for ex in tot_stats["examples"][:8]:
        print("     пример: " + ex, flush=True)


if __name__ == "__main__":
    main()
print("DONE")
