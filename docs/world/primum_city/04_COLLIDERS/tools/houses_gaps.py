# houses_gaps.py — что ЕСТЬ в RAW-домах, но НЕ попало в коллайдеры (остаток после правки 2).
#
# Только чтение RAW-блендов. Правила отбора берутся из houses_export.py (импорт), чтобы не
# держать второй источник правды: здесь считается именно ОСТАТОК — крупные поверхности,
# которые отброшены, с причиной и площадью. Ответ на «много дыр»: остаток должен быть
# мелким/декоративным, а не стенами и перекрытиями.
#
# Выход:
#   docs/world/primum_city/04_COLLIDERS/houses_gaps.log   — сводка по чанкам
#   docs/world/primum_city/04_COLLIDERS/houses_gaps.csv   — строка на дом
#
# Запуск:
#   "W:\SteamLibrary\steamapps\common\Blender\blender.exe" -b --factory-startup --python houses_gaps.py
#   GAPS_CHUNKS="S5_NE_P2,S6_NQ" — фильтр по подстроке имени чанка
import bpy, os, csv, sys
from collections import defaultdict
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX          # классификатор, словари, грамматика имён

ROOT = HX.ROOT
RAW = HX.RAW
OUTDIR = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS"
LOG_OUT = os.path.join(OUTDIR, "houses_gaps.log")
CSV_OUT = os.path.join(OUTDIR, "houses_gaps.csv")

BIG = 1.5            # м, крупная деталь
HORIZ = 0.85         # |nz| > HORIZ => горизонтальная грань
VERT = 0.30          # |nz| < VERT  => вертикальная грань
LEVEL_EPS = 0.5

logf = None


def log(m):
    print(m, flush=True)
    if logf is not None:
        logf.write(m + "\n")
        logf.flush()


def bbox(o):
    m = o.matrix_world
    pts = [m @ Vector(c) for c in o.bound_box]
    xs = [p.x for p in pts]
    ys = [p.y for p in pts]
    zs = [p.z for p in pts]
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))


def areas(o):
    me = o.data
    mw = o.matrix_world
    try:
        me.calc_loop_triangles()
    except Exception:
        return 0.0, 0.0
    hor = ver = 0.0
    vs = me.vertices
    for t in me.loop_triangles:
        v0 = mw @ vs[t.vertices[0]].co
        v1 = mw @ vs[t.vertices[1]].co
        v2 = mw @ vs[t.vertices[2]].co
        n = (v1 - v0).cross(v2 - v0)
        l = n.length
        if l < 1e-9:
            continue
        nz = abs(n.z / l)
        a = l * 0.5
        if nz > HORIZ:
            hor += a
        elif nz < VERT:
            ver += a
    return hor, ver


def horiz_levels(o):
    me = o.data
    mw = o.matrix_world
    try:
        me.calc_loop_triangles()
    except Exception:
        return []
    out = []
    vs = me.vertices
    for t in me.loop_triangles:
        v0 = mw @ vs[t.vertices[0]].co
        v1 = mw @ vs[t.vertices[1]].co
        v2 = mw @ vs[t.vertices[2]].co
        n = (v1 - v0).cross(v2 - v0)
        l = n.length
        if l < 1e-9 or abs(n.z / l) <= HORIZ or l * 0.5 < 0.5:
            continue
        out.append((v0.z + v1.z + v2.z) / 3.0)
    return out


def collapse(zs, eps=LEVEL_EPS):
    lev = []
    for z in sorted(zs):
        if not lev or z - lev[-1] > eps:
            lev.append(z)
    return lev


def cfg_name(n):
    out = []
    for t in HX.base_tokens(n):
        if t.isdigit():
            out.append("#")
        else:
            out.append(t)
    return "_".join(out)


def probe(path, chunk, rows, tot):
    bpy.ops.wm.open_mainfile(filepath=path)

    kept = defaultdict(int)
    kept_area = defaultdict(float)
    residual = defaultdict(lambda: [0, 0.0, 0.0, []])   # (правило|шаблон) -> [n, hor, ver, примеры]
    house_floor_levels = defaultdict(list)
    house_z = defaultdict(lambda: [1e9, -1e9])

    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None:
            continue
        if o.data is None or len(o.data.polygons) == 0:
            continue
        toks = HX.base_tokens(o.name)
        cls, rule = HX.klass_of(o, toks)
        house = HX.house_of(toks)
        bb = bbox(o)
        big = max(bb[1] - bb[0], bb[3] - bb[2], bb[5] - bb[4]) >= BIG

        if cls is not None:
            kept[cls] += 1
            if big:
                house_z[house][0] = min(house_z[house][0], bb[4])
                house_z[house][1] = max(house_z[house][1], bb[5])
                if cls == "FLOOR":
                    house_floor_levels[house].extend(horiz_levels(o))
                hor, ver = areas(o)
                kept_area[cls] += hor + ver
            continue

        if not big:
            continue
        hor, ver = areas(o)
        key = rule + " | " + cfg_name(o.name)
        d = residual[key]
        d[0] += 1
        d[1] += hor
        d[2] += ver
        if len(d[3]) < 3 and o.name not in d[3]:
            d[3].append(o.name)

    log("")
    log("=== %s ===" % chunk.replace("PRIMUM_HOUSES_", "").replace(".blend", ""))
    log("  в коллайдеры: " + "  ".join("%s=%d (%.0f м2)" % (k, kept[k], kept_area[k])
                                       for k in ("WALL", "FLOOR", "ROOF", "STAIR") if kept[k]))
    res_ver = sum(v[2] for v in residual.values())
    res_hor = sum(v[1] for v in residual.values())
    tot["res_ver"] += res_ver
    tot["res_hor"] += res_hor
    tot["res_objs"] += sum(v[0] for v in residual.values())
    for k in ("WALL", "FLOOR", "ROOF", "STAIR"):
        tot["kept_" + k] += kept[k]
        tot["area_" + k] += kept_area[k]
    log("  ОСТАТОК крупного: шаблонов=%d  гор=%.0f м2  верт=%.0f м2"
        % (len(residual), res_hor, res_ver))
    for key in sorted(residual, key=lambda k: -(residual[k][1] + residual[k][2]))[:8]:
        d = residual[key]
        log("     %-60s n=%-4d гор=%-7.0f верт=%-7.0f  %s"
            % (key[:60], d[0], d[1], d[2], d[3][:1]))

    for h in sorted(house_floor_levels):
        levs = collapse(house_floor_levels[h])
        z0, z1 = house_z[h]
        rows.append([chunk.replace("PRIMUM_HOUSES_", "").replace(".blend", ""), h,
                     round(z1 - z0, 1), len(levs),
                     " ".join("%.1f" % z for z in levs)])


def main():
    global logf
    logf = open(LOG_OUT, "w", encoding="utf-8")
    flt = os.environ.get("GAPS_CHUNKS", "").strip()
    chunks = HX.list_chunks()
    if flt:
        want = [s.strip().upper() for s in flt.split(",") if s.strip()]
        chunks = [c for c in chunks if any(w in c.upper() for w in want)]
    rows = []
    tot = defaultdict(float)
    log("=== houses_gaps: чанков %d (фильтр: %s) ===" % (len(chunks), flt or "-"))
    for f in chunks:
        p = os.path.join(RAW, f)
        if not os.path.exists(p):
            log("MISSING " + f)
            continue
        try:
            probe(p, f, rows, tot)
        except Exception as e:
            log("ERROR %s: %s" % (f, e))
    log("")
    log("=== ИТОГО по 25 чанкам ===")
    log("  в коллайдеры: WALL=%d (%.0f м2)  FLOOR=%d (%.0f м2)  ROOF=%d (%.0f м2)  STAIR=%d (%.0f м2)"
        % (tot["kept_WALL"], tot["area_WALL"], tot["kept_FLOOR"], tot["area_FLOOR"],
           tot["kept_ROOF"], tot["area_ROOF"], tot["kept_STAIR"], tot["area_STAIR"]))
    log("  ОСТАТОК (не в физике): объектов=%d  гор=%.0f м2  верт=%.0f м2"
        % (tot["res_objs"], tot["res_hor"], tot["res_ver"]))
    multi = [r for r in rows if r[2] >= 5.0]
    bad = [r for r in multi if r[3] < 2]
    log("  домов всего=%d, из них с коробкой >=5 м: %d, из них с одним уровнем пола: %d"
        % (len(rows), len(multi), len(bad)))
    for r in bad[:10]:
        log("     ! %s/%s h=%.1f ур=%d" % (r[0], r[1], r[2], r[3]))
    with open(CSV_OUT, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["chunk", "house", "height_m", "floor_levels", "levels_z"])
        w.writerows(rows)
    log("")
    log("CSV: %s строк=%d" % (CSV_OUT, len(rows)))
    logf.close()
    logf = None


main()
print("DONE")
