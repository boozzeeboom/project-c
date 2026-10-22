# houses_probe.py — диагностика домов в RAW-блендах PRIMUM (только чтение, ничего не пишет в .blend).
#
# Зачем: перед правкой отбора коллайдеров нужно ответить на вопросы §3.1-3.4 из
# docs/world/primum_city/04_COLLIDERS/04_STATUS_AND_GAPS.md по КОНКРЕТНОМУ дому:
#   1) есть ли в модели геометрия лестницы (и под каким именем/в какой коллекции);
#   2) есть ли задняя стена (и с какой стороны дома её нет);
#   3) какие коллекции несут этажные перекрытия и межкомнатные перегородки;
#   4) сколько этажных уровней реально смоделировано.
#
# Выход:
#   docs/world/primum_city/04_COLLIDERS/houses_probe.csv  — одна строка на дом
#   docs/world/primum_city/04_COLLIDERS/houses_probe.log  — сводка + разбор выбранного дома
#
# Запуск (пример):
#   "W:\SteamLibrary\steamapps\common\Blender\blender.exe" -b --factory-startup ^
#     --python docs\world\primum_city\04_COLLIDERS\tools\houses_probe.py
#   переменные окружения: PROBE_CHUNKS="TRD-S2,TRD-N"  (подстрока имени чанка)
#                         PROBE_HOUSE="RES_TN_01"      (дом для подробного разбора)
import bpy, os, csv, math
from collections import defaultdict
from mathutils import Vector

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW")
OUTDIR = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS"
CSV_OUT = os.path.join(OUTDIR, "houses_probe.csv")
LOG_OUT = os.path.join(OUTDIR, "houses_probe.log")

def list_chunks():
    """Все жилые чанки в RAW (без копий и бэкапов) — список не ведём руками."""
    if not os.path.isdir(RAW):
        return []
    out = []
    for f in sorted(os.listdir(RAW)):
        u = f.upper()
        if not u.startswith("PRIMUM_HOUSES_") or not u.endswith(".BLEND"):
            continue
        if "COPY" in u or "BAK" in u:
            continue
        out.append(f)
    return out


CHUNKS = list_chunks()

# порог площади «горизонтальной грани», чтобы считать деталь полом/перекрытием, м^2
FLOOR_AREA = 0.5
# порог «тонкой вертикальной» детали (перегородка), м
PART_MIN_SIDE = 0.35
SIDE_TOL = 0.9       # допуск привязки куска стены к грани дома, м

logf = None


def log(m):
    print(m, flush=True)
    if logf is not None:
        logf.write(m + "\n")
        logf.flush()


def cpath(o):
    return "/".join([c.name for c in o.users_collection]).upper()


def has_ancestor(o, names):
    p = o.parent
    while p is not None:
        if p.name in names:
            return True
        p = p.parent
    return False


def bbox(o):
    m = o.matrix_world
    pts = [m @ Vector(c) for c in o.bound_box]
    xs = [p.x for p in pts]
    ys = [p.y for p in pts]
    zs = [p.z for p in pts]
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))


def house_of(name):
    parts = name.split("_")
    return "_".join(parts[:3]) if len(parts) >= 3 else name


def flat_area(o):
    """Площадь граней с нормалью вверх (в мире) для детали."""
    a = 0.0
    me = o.data
    if me is None:
        return 0.0
    mw = o.matrix_world
    try:
        me.calc_loop_triangles()
    except Exception:
        return 0.0
    for t in me.loop_triangles:
        v0 = mw @ me.vertices[t.vertices[0]].co
        v1 = mw @ me.vertices[t.vertices[1]].co
        v2 = mw @ me.vertices[t.vertices[2]].co
        n = (v1 - v0).cross(v2 - v0)
        l = n.length
        if l < 1e-9:
            continue
        if abs(n.z / l) > 0.85:
            a += l * 0.5
    return a


def probe(path, detail_house):
    bpy.ops.wm.open_mainfile(filepath=path)
    chunk = os.path.basename(path).replace(".blend", "")

    per_house = defaultdict(lambda: {
        "objs": 0, "colls": set(),
        "walls": [], "floors": [], "roofs": [], "stairs": [], "inter": [],
        "zmin": 1e9, "zmax": -1e9})

    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None:
            continue
        if has_ancestor(o, ("DOORS", "LAMPS", "OUTLIERS", "DOOR_PARTS_UNASSIGNED")):
            continue
        h = house_of(o.name)
        d = per_house[h]
        p = cpath(o)
        n = o.name.upper()
        b = bbox(o)
        d["objs"] += 1
        d["colls"].add(p)
        d["zmin"] = min(d["zmin"], b[4])
        d["zmax"] = max(d["zmax"], b[5])
        is_interior = "40_INTERIOR" in p or "FURN" in p or "60_DECOR" in p
        if "STAIR" in n or "STEPS" in n or "STAIR" in p or "STEPS" in p:
            if not any(t in n for t in ("RAIL", "GUARD", "HAND", "BALUST", "SKIRT", "KICK")):
                d["stairs"].append((o.name, b))
                continue
        if "30_WINDOWS" in p or "WINDOW" in n or "20_ENTRANCE" in p:
            continue
        if "50_ROOF" in p or "ROOF" in n or "PARAPET" in n:
            d["roofs"].append((o.name, b))
            continue
        if is_interior:
            d["inter"].append((o.name, b, p))
            continue
        if "10_WALLS" in p or "WALL" in n or "SHELL" in n:
            d["walls"].append((o.name, b, p))
            continue
        if "05_FLOOR" in p or "FLOOR" in n or "SLAB" in n:
            d["floors"].append((o.name, b, p))
    return chunk, per_house


def summarize(chunk, per_house, rows, detail_house):
    log("")
    log("=== %s : домов=%d ===" % (chunk, len(per_house)))
    tot = defaultdict(int)
    for h in sorted(per_house):
        d = per_house[h]
        # грани дома
        hx0 = min([b[0] for b in [x[1] for x in d["walls"]]] or [0])
        hx1 = max([b[1] for b in [x[1] for x in d["walls"]]] or [0])
        hy0 = min([b[2] for b in [x[1] for x in d["walls"]]] or [0])
        hy1 = max([b[3] for b in [x[1] for x in d["walls"]]] or [0])

        sides = defaultdict(int)
        for nm, b, p in d["walls"]:
            cx = (b[0] + b[1]) * 0.5
            cy = (b[2] + b[3]) * 0.5
            cand = [("W", abs(cx - hx0)), ("E", abs(cx - hx1)),
                    ("S", abs(cy - hy0)), ("N", abs(cy - hy1))]
            cand.sort(key=lambda t: t[1])
            if cand[0][1] <= SIDE_TOL:
                sides[cand[0][0]] += 1

        levels = sorted(set(round(b[5], 1) for nm, b, p in d["floors"]))
        # схлопываем уровни ближе 0.4 м
        lev = []
        for z in levels:
            if not lev or z - lev[-1] > 0.4:
                lev.append(z)

        floor_area = 0.0
        vert_part = 0
        for nm, b, p in d["inter"]:
            dx = b[1] - b[0]
            dy = b[3] - b[2]
            if min(dx, dy) <= PART_MIN_SIDE and max(dx, dy) > 1.0:
                vert_part += 1
            floor_area += flat_area(bpy.data.objects.get(nm)) if nm in bpy.data.objects else 0.0

        stairs_z = [(round(b[4], 1), round(b[5], 1)) for nm, b in d["stairs"]]
        sz0 = min([z[0] for z in stairs_z]) if stairs_z else 0.0
        sz1 = max([z[1] for z in stairs_z]) if stairs_z else 0.0

        row = [chunk, h, d["objs"], len(d["colls"]),
               round(d["zmin"], 1), round(d["zmax"], 1),
               len(d["walls"]), sum(sides.values()), sides.get("N", 0), sides.get("S", 0),
               sides.get("E", 0), sides.get("W", 0),
               len(d["floors"]), len(lev), " ".join("%.1f" % z for z in lev),
               len(d["inter"]), round(floor_area, 1), vert_part,
               len(d["stairs"]), sz0, sz1,
               len(d["roofs"]),
               " | ".join(sorted(d["colls"])[:4])]
        rows.append(row)
        tot["objs"] += d["objs"]
        tot["no_stair"] += 1 if not d["stairs"] else 0
        tot["no_floor"] += 1 if not d["floors"] else 0
        tot["no_inter"] += 1 if not d["inter"] else 0
        tot["missing_side"] += 1 if (sides.get("N", 0) == 0 or sides.get("S", 0) == 0
                                    or sides.get("E", 0) == 0 or sides.get("W", 0) == 0) else 0

        if detail_house and h == detail_house:
            log("")
            log("--- разбор дома %s ---" % h)
            log("  объекты=%d  z=%.1f..%.1f  коллекции:" % (d["objs"], d["zmin"], d["zmax"]))
            for c in sorted(d["colls"]):
                log("     %s" % c)
            log("  стены=%d (N=%d S=%d E=%d W=%d)" % (len(d["walls"]), sides.get("N", 0), sides.get("S", 0),
                                                      sides.get("E", 0), sides.get("W", 0)))
            log("  перекрытия=%d  уровни=%s" % (len(d["floors"]), ["%.1f" % z for z in lev]))
            log("  интерьер=%d  гориз.площадь=%.1f м2  вертикальных перегородок=%d" %
                (len(d["inter"]), floor_area, vert_part))
            log("  лестницы=%d  z=%s" % (len(d["stairs"]), ["%s [%.1f..%.1f]" % (nm, b[4], b[5]) for nm, b in d["stairs"][:8]]))
            log("  имена перекрытий: %s" % [nm for nm, b, p in d["floors"][:10]])
            log("  имена интерьера: %s" % [nm for nm, b, p in d["inter"][:12]])
            log("  имена стен: %s" % [nm for nm, b, p in d["walls"][:12]])

    log("  ИТОГО объектов=%d | домов без лестниц=%d | без перекрытий=%d | без интерьера=%d | без одной из 4 стен=%d"
        % (tot["objs"], tot["no_stair"], tot["no_floor"], tot["no_inter"], tot["missing_side"]))


def main():
    global logf
    logf = open(LOG_OUT, "a", encoding="utf-8")
    flt = os.environ.get("PROBE_CHUNKS", "").strip()
    detail = os.environ.get("PROBE_HOUSE", "").strip().upper()
    chunks = CHUNKS
    if flt:
        want = [s.strip().upper() for s in flt.split(",") if s.strip()]
        chunks = [c for c in CHUNKS if any(w in c.upper() for w in want)]
    fresh = not os.path.exists(CSV_OUT)
    rows = []
    log("")
    log("=== houses_probe: чанков %d (фильтр: %s) ===" % (len(chunks), flt or "-"))
    for fname in chunks:
        path = os.path.join(RAW, fname)
        if not os.path.exists(path):
            log("MISSING " + fname)
            continue
        try:
            chunk, per_house = probe(path, detail)
            summarize(chunk, per_house, rows, detail)
        except Exception as e:
            log("ERROR %s: %s" % (fname, e))
    with open(CSV_OUT, "a", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        if fresh:
            w.writerow(["chunk", "house", "objs", "collections", "zmin", "zmax",
                        "walls", "walls_near_side", "sideN", "sideS", "sideE", "sideW",
                        "floors", "floor_levels", "level_z", "interior", "interior_flat_area_m2",
                        "interior_partitions", "stairs", "stair_z0", "stair_z1", "roofs", "coll_sample"])
        w.writerows(rows)
    log("")
    log("CSV: %s строк=%d" % (CSV_OUT, len(rows)))
    if logf is not None:
        logf.close()
        logf = None


main()
print("DONE")
