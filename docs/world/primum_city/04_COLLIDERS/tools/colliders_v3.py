# colliders_v3.py — бокс-коллайдеры PRIMUM, ревизия 3 (исправление стен и платформ).
#
# Что исправлено относительно v2:
#   1) СТЕНЫ: спаны считались как [проекция_центра, проекция_центра + длина] — каждый бокс съезжал
#      на пол-длины вдоль стены, а слияние склеивало дверные проёмы. Теперь спаны берутся из ГАБАРИТА
#      объекта ([min, max] по оси стены), линия = центр по нормали, слияние — по зазору 0.3 м
#      (окна заливаются, двери остаются проёмами). Группировка по этажу (кластер z0).
#   2) ПЛАТФОРМЫ: 8 больших боксов перекрывали вырезы под лестницы. Теперь геометрия платформ
#      выгружается в platforms.obj и в Unity становится MeshCollider'ом (8 коллайдеров, вырезы точные).
#   3) КАЖДЫЙ бокс проверяется «внутри габарита своего дома + 0.6 м» — счётчик нарушений в лог.
#
# Выход:
#   docs/world/primum_city/04_COLLIDERS/colliders_v3.csv   (chunk,cls,cx,cy,cz,sx,sy,sz,yaw,pitch,name)
#   docs/world/primum_city/04_COLLIDERS/platforms.obj      (9 платформ низа города, локальные коорд.)
import bpy, os, csv, math, time
from collections import defaultdict
from mathutils import Vector

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW")
OUTDIR = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS"
OUT = os.path.join(OUTDIR, "colliders_v3.csv")
OBJ = os.path.join(OUTDIR, "platforms.obj")
LOG = r"C:\Projects\ProjectC_client\_analysis\colliders_v3.log"

K = 1.07
C = (7.5, -148.0, 18.5)
MIN_SIDE = 0.40
MERGE_GAP = 0.30
STAIR_THICK = 0.35
HOUSE_TOL = 0.60

PLATFORM_NAMES = {"DECK_0", "DECK_6", "DECK_12", "DECK_18",
                  "PLATFORM_L00", "PLATFORM_L06", "PLATFORM_L12", "PLATFORM_L18", "PLATFORM_L24"}
PLATFORM_SHORT = {"DECK_0": "DECK0", "DECK_6": "DECK6", "DECK_12": "DECK12", "DECK_18": "DECK18",
                  "PLATFORM_L00": "PLATL00", "PLATFORM_L06": "PLATL06", "PLATFORM_L12": "PLATL12",
                  "PLATFORM_L18": "PLATL18", "PLATFORM_L24": "PLATL24"}

CHUNKS = [
    ("PRIMUM_BASE_S0_CITY_P1_00H.blend", "PRIMUM_BASE_S0_CITY_P1_00H"),
    ("PRIMUM_ADMIN_S0_ADM_P1_01H.blend", "PRIMUM_ADMIN_S0_ADM_P1_01H"),
    ("PRIMUM_HOUSES_S1_GLD_P1_25H.blend", "PRIMUM_HOUSES_S1_GLD_P1_25H"),
    ("PRIMUM_HOUSES_S2_TRD_P1_28H.blend", "PRIMUM_HOUSES_S2_TRD_P1_28H"),
    ("PRIMUM_HOUSES_S3_TRD_P2_26H.blend", "PRIMUM_HOUSES_S3_TRD_P2_26H"),
    ("PRIMUM_HOUSES_S3_TRD-EN_P1_37H.blend", "PRIMUM_HOUSES_S3_TRD-EN_P1_37H"),
    ("PRIMUM_HOUSES_S3_TRD-ES_P1_37H.blend", "PRIMUM_HOUSES_S3_TRD-ES_P1_37H"),
    ("PRIMUM_HOUSES_S4_TRD_P3_26H.blend", "PRIMUM_HOUSES_S4_TRD_P3_26H"),
    ("PRIMUM_HOUSES_S4_TRD-EN_P2_34H.blend", "PRIMUM_HOUSES_S4_TRD-EN_P2_34H"),
    ("PRIMUM_HOUSES_S4_TRD-ES_P2_34H.blend", "PRIMUM_HOUSES_S4_TRD-ES_P2_34H"),
    ("PRIMUM_HOUSES_S5_NE_P1_20H.blend", "PRIMUM_HOUSES_S5_NE_P1_20H"),
    ("PRIMUM_HOUSES_S5_NE_P2_32H.blend", "PRIMUM_HOUSES_S5_NE_P2_32H"),
    ("PRIMUM_HOUSES_S5_NE_P3_22H.blend", "PRIMUM_HOUSES_S5_NE_P3_22H"),
    ("PRIMUM_HOUSES_S6_NQ_P1_29H.blend", "PRIMUM_HOUSES_S6_NQ_P1_29H"),
    ("PRIMUM_HOUSES_S7_BIG_P1_02H.blend", "PRIMUM_HOUSES_S7_BIG_P1_02H"),
    ("PRIMUM_HOUSES_S7_SE_P1_46H.blend", "PRIMUM_HOUSES_S7_SE_P1_46H"),
    ("PRIMUM_HOUSES_S7_SE_P2_19H.blend", "PRIMUM_HOUSES_S7_SE_P2_19H"),
    ("PRIMUM_HOUSES_S8_GW_P1_30H.blend", "PRIMUM_HOUSES_S8_GW_P1_30H"),
    ("PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H.blend", "PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H"),
    ("PRIMUM_HOUSES_S9_RING_P1_22H.blend", "PRIMUM_HOUSES_S9_RING_P1_22H"),
    ("PRIMUM_HOUSES_S9_TRD-N_P1_28H.blend", "PRIMUM_HOUSES_S9_TRD-N_P1_28H"),
    ("PRIMUM_HOUSES_S9_TRD-S_P1_16H.blend", "PRIMUM_HOUSES_S9_TRD-S_P1_16H"),
    ("PRIMUM_HOUSES_S9_TRD-S2_P1_45H.blend", "PRIMUM_HOUSES_S9_TRD-S2_P1_45H"),
]

def log(m):
    with open(LOG, "a", encoding="utf-8") as f: f.write(m + "\n")
    print(m)

def ulocal(p):
    s = [C[i] + (p[i]-C[i])*K for i in range(3)]
    return (-s[0], s[2], -s[1])

def world_bbox(o):
    m = o.matrix_world
    pts = [m @ Vector(c) for c in o.bound_box]
    xs=[p.x for p in pts]; ys=[p.y for p in pts]; zs=[p.z for p in pts]
    return (min(xs),max(xs),min(ys),max(ys),min(zs),max(zs))

def cpath(o): return "/".join([c.name for c in o.users_collection]).upper()

def has_ancestor(o, names):
    p = o.parent
    while p is not None:
        if p.name in names: return True
        p = p.parent
    return False

RAIL = ("RAIL","GUARD","HAND","SKIRT","BALUST","_BR","KERB","KICK","FURN","RUG","CARPET","COVER")

def is_stair(o):
    p = cpath(o); n = o.name.upper()
    if "STAIR" not in p and "STAIR" not in n and "STEPS" not in p: return False
    return not any(t in n for t in RAIL)

def classify(o):
    p = cpath(o); n = o.name.upper()
    if has_ancestor(o, ("DOORS","LAMPS","OUTLIERS","DOOR_PARTS_UNASSIGNED")): return None
    if o.name in PLATFORM_NAMES: return "PLATFORM"
    if "30_WINDOWS" in p or "WINDOW" in p or "20_ENTRANCE" in p: return None
    if "40_INTERIOR" in p or "FURN" in p or "60_DECOR" in p: return None
    if is_stair(o): return "STAIR"
    if "50_ROOF" in p or "ROOF" in p or "PARAPET" in n: return "ROOF"
    if "10_WALLS" in p or "WALL" in p or "SHELL" in p: return "WALL"
    if "05_FLOOR" in p or "FLOOR" in n or "SLAB" in n: return "FLOOR"
    return None

def house_of(name):
    parts = name.split("_")
    return "_".join(parts[:3]) if len(parts) >= 3 else name

rows = []
navr = defaultdict(int)
stats = defaultdict(lambda: defaultdict(int))

def fmt(v): return ("%.3f" % v).replace(",", ".")

def add(chunk, cls, c, s, yaw=0.0, pitch=0.0, name=""):
    x,y,z = c; sx,sy,sz = s
    if max(sx,sy,sz) < MIN_SIDE: return
    rows.append([chunk, cls, fmt(x), fmt(y), fmt(z), fmt(sx), fmt(sy), fmt(sz),
                 "%.2f" % yaw, "%.2f" % pitch, name])
    stats[chunk][cls] += 1

def add_axis(chunk, cls, b, name="", hb=None):
    cx=(b[0]+b[1])/2; cy=(b[2]+b[3])/2; cz=(b[4]+b[5])/2
    if hb is not None and not inside((b[0],b[1],b[4],b[5],b[2],b[3]), hb):
        navr[chunk] += 1
    ux,uy,uz = ulocal((cx,cy,cz))
    add(chunk, cls, (ux,uy,uz), ((b[1]-b[0])*K, (b[5]-b[4])*K, (b[3]-b[2])*K), 0, 0, name)

def inside(b, hb):
    """b и hb — (minx,maxx,miny,maxy,minz,maxz) в Blender-координатах."""
    return (b[0] >= hb[0]-HOUSE_TOL and b[1] <= hb[1]+HOUSE_TOL and
            b[2] >= hb[2]-HOUSE_TOL and b[3] <= hb[3]+HOUSE_TOL and
            b[4] >= hb[4]-HOUSE_TOL and b[5] <= hb[5]+HOUSE_TOL)

# ---------------------------------------------------------------- СТЕНЫ (исправлено)

def add_walls(chunk, items, house_boxes):
    """items: (axis, line, a0, a1, z0, z1, thick, house) — axis 0: нормаль X (стена вдоль Y)."""
    groups = defaultdict(list)
    for it in items:
        # группировка: ось, линия (квант 0.25), этаж (низ стены квант 0.6)
        groups[(it[0], round(it[1]/0.25))].append(it)
    for key, lst in groups.items():
        axis, _lq = key
        line = sum(it[1] for it in lst)/len(lst)
        # ВАЖНО: куски, начинающиеся выше пола+1 м — это перемычки над дверями и надоконные части.
        # Их нельзя учитывать при слиянии вдоль стены: иначе они заливают ДВЕРНОЙ проём.
        floor_z = min(it[4] for it in lst)
        low = [it for it in lst if it[4] <= floor_z + 1.0]
        if not low: low = lst
        spans = sorted((it[2], it[3]) for it in low)
        merged = [list(spans[0])]
        for a0,a1 in spans[1:]:
            if a0 - merged[-1][1] <= MERGE_GAP:
                merged[-1][1] = max(merged[-1][1], a1)
            else:
                merged.append([a0,a1])
        thick = sum(it[6] for it in low)/len(low)
        for a0,a1 in merged:
            length = a1-a0
            if length < MIN_SIDE: continue
            seg = [it for it in low if it[3] > a0 and it[2] < a1]
            if not seg: seg = low
            z0 = min(it[4] for it in seg); z1 = max(it[5] for it in seg)
            mid = (a0+a1)*0.5
            if axis == 0:   cx, cy = line, mid      # стена вдоль Y
            else:           cx, cy = mid, line      # стена вдоль X
            hb = house_boxes.get(lst[0][7])
            if hb is not None:
                bb = (cx-length/2, cx+length/2, cy-thick/2, cy+thick/2, z0, z1)
                if not inside(bb, hb): navr[chunk] += 1
            ux,uy,uz = ulocal((cx,cy,(z0+z1)/2))
            yaw = 90.0 if axis == 0 else 0.0
            add(chunk, "WALL", (ux,uy,uz), (length*K, (z1-z0)*K, thick*K), yaw, 0, lst[0][7])

# ---------------------------------------------------------------- лестницы

def _obb_gap(a, b):
    """Зазор между двумя AABB (0 если пересекаются) в 3D."""
    g = 0.0
    for k in range(6):
        pass
    dx = max(a[0] - b[1], b[0] - a[1], 0.0)
    dy = max(a[2] - b[3], b[2] - a[3], 0.0)
    dz = max(a[4] - b[5], b[4] - a[5], 0.0)
    return math.sqrt(dx*dx + dy*dy + dz*dz)

def _cluster_flights(parts):
    """Кластеризация деталей лестницы в МАРШИ по касанию (зазор <= 0.45 м)."""
    boxes = [world_bbox(o) for o in parts]
    n = len(parts)
    parent = list(range(n))
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for i in range(n):
        for j in range(i+1, n):
            if _obb_gap(boxes[i], boxes[j]) <= 0.45:
                ri, rj = find(i), find(j)
                if ri != rj: parent[ri] = rj
    groups = defaultdict(list)
    for i in range(n):
        groups[find(i)].append(i)
    return [(g, [parts[i] for i in g], [boxes[i] for i in g]) for g in groups.values()]

def _fit_bbox_flight(chunk, objs, name):
    """Фолбэк для одиночных объектов-маршей (BASE/ADMIN): наклон по габариту."""
    pts = []
    for o in objs:
        for v in o.data.vertices:
            pts.append(o.matrix_world @ v.co)
    if len(pts) < 8:
        return
    xs=[p.x for p in pts]; ys=[p.y for p in pts]; zs=[p.z for p in pts]
    dx=max(xs)-min(xs); dy=max(ys)-min(ys); dz=max(zs)-min(zs)
    if dx < 0.6 or dy < 0.6:
        return

    def grad(axis):
        lo = min(xs) if axis==0 else min(ys)
        hi = max(xs) if axis==0 else max(ys)
        if hi-lo < 0.3: return 0.0
        nb=12; acc=[0.0]*nb; cnt=[0]*nb
        for p in pts:
            v = p.x if axis==0 else p.y
            b=int(min(nb-1,(v-lo)/(hi-lo)*nb)); acc[b]+=p.z; cnt[b]+=1
        m=[acc[i]/cnt[i] for i in range(nb) if cnt[i]>0]
        if len(m)<3: return 0.0
        return (m[-1]-m[0])/(hi-lo)

    gx, gy = grad(0), grad(1)
    if abs(gx) >= abs(gy):
        axis=0; run=dx; width=dy; g=gx
    else:
        axis=1; run=dy; width=dx; g=gy
    cx=(min(xs)+max(xs))/2; cy=(min(ys)+max(ys))/2; cz=(min(zs)+max(zs))/2
    if run < 1.0 or width < 0.5:
        ux,uy,uz = ulocal((cx,cy,cz))
        add(chunk, "STAIR", (ux,uy,uz), (dx*K, dz*K, dy*K), 0, 0, name)
        return
    pitch = math.degrees(math.atan2(dz, run))
    if pitch < 8.0 or pitch > 60.0:
        ux,uy,uz = ulocal((cx,cy,cz))
        add(chunk, "STAIR", (ux,uy,uz), (dx*K, dz*K, dy*K), 0, 0, name)
        return
    dir_b = ((1.0 if g>0 else -1.0), 0.0) if axis==0 else (0.0, (1.0 if g>0 else -1.0))
    dir_u = (-dir_b[0], -dir_b[1])
    yaw = math.degrees(math.atan2(dir_u[0], dir_u[1]))
    ux,uy,uz = ulocal((cx,cy,cz))
    length = math.sqrt(run*run + dz*dz) * K
    add(chunk, "STAIR", (ux,uy,uz), (width*K, STAIR_THICK, length), yaw, pitch, name)
def add_stairs(chunk, stair_parts, house_boxes):
    """Один наклонный бокс НА МАРШ (не на всю лестницу): ступени кластеризуются в марши,
    направление и уклон считаются по линии «низ -> верх», а не по габариту всей лестницы
    (иначе на Г-образных лестницах получались наложенные друг на друга боксы)."""
    # отбираем СТУПЕНИ (коллекция STEPS), если есть; иначе все детали
    treads = [o for o in stair_parts if "STEPS" in cpath(o)]
    pool = treads if treads else stair_parts
    if not pool:
        return
    # группируем по лестнице (ближайший сегмент коллекции со STAIR) и внутри — по маршам
    by_stair = defaultdict(list)
    for o in pool:
        key = None
        for seg in cpath(o).split("/"):
            if "STAIR" in seg:
                key = seg
                break
        by_stair[key or o.name].append(o)

    for stair_name, parts in by_stair.items():
        for _g, objs, boxes in _cluster_flights(parts):
            if len(objs) == 0:
                continue
            # центры и высоты деталей
            items = []
            for o, b in zip(objs, boxes):
                cx = (b[0]+b[1])/2; cy = (b[2]+b[3])/2; cz = (b[4]+b[5])/2
                w = max(b[1]-b[0], b[3]-b[2])
                items.append((cz, cx, cy, w, b))
            if len(items) < 3:
                _fit_bbox_flight(chunk, objs, stair_name)
                continue
            items.sort()
            z_lo, z_hi = items[0][0], items[-1][0]
            rise = z_hi - z_lo
            if rise < 0.30:          # плоская площадка
                _fit_bbox_flight(chunk, objs, stair_name)
                continue
            # направление подъёма: от нижней крайней детали к верхней крайней
            lo = items[0]; hi = items[-1]
            dx = hi[1] - lo[1]; dy = hi[2] - lo[2]
            horiz = math.hypot(dx, dy)
            if horiz < 0.5:          # направление не определить — фолбэк по габариту
                _fit_bbox_flight(chunk, objs, stair_name)
                continue
            pitch = math.degrees(math.atan2(rise, horiz))
            if pitch < 8.0 or pitch > 65.0:
                continue
            # длина по наклону: между крайними центрами + глубина ступени
            tread_depth = max(0.25, max(it[4][1]-it[4][0] if it[4][1]-it[4][0] < 1.5 else 0.3 for it in items))
            length_b = math.hypot(horiz, rise) + min(0.6, tread_depth)
            width_b = max(it[3] for it in items)
            if width_b < 0.5:
                continue
            # центр бокса: середина по линии подъёма
            cx = (lo[1] + hi[1]) / 2; cy = (lo[2] + hi[2]) / 2
            cz = (lo[0] + hi[0]) / 2 + 0.15      # чуть выше центров ступеней
            ux, uy, uz = ulocal((cx, cy, cz))
            # yaw: направление подъёма в Unity (x,z) = (-dx,-dy)
            dux, duz = -dx, -dy
            yaw = math.degrees(math.atan2(dux, duz))   # локальный +Z вдоль марша
            add(chunk, "STAIR", (ux,uy,uz), (width_b*K, STAIR_THICK, length_b*K), yaw, pitch, stair_name)
# ---------------------------------------------------------------- платформы -> OBJ

def write_platforms(objs, fh):
    n = 0
    for o in objs:
        me = o.data
        name = PLATFORM_SHORT.get(o.name, o.name.replace(" ", "_"))
        fh.write("o %s\n" % name)
        base = 1
        for v in me.vertices:
            p = o.matrix_world @ v.co
            ux,uy,uz = ulocal((p.x, p.y, p.z))
            fh.write("v %.4f %.4f %.4f\n" % (ux, uy, uz))
        for poly in me.polygons:
            idx = [i + base for i in poly.vertices]
            # Unity: треугольники по часовой стрелке (front face). OBJ — против часовой.
            if len(idx) == 3:
                fh.write("f %d %d %d\n" % (idx[2], idx[1], idx[0]))
            elif len(idx) == 4:
                fh.write("f %d %d %d\n" % (idx[3], idx[2], idx[1]))
                fh.write("f %d %d %d\n" % (idx[3], idx[1], idx[0]))
        n += 1
    return n

# ---------------------------------------------------------------- чанк

def _dedupe_stairs(chunk):
    """Убрать наложенные марши (центры ближе 0.6 м): на ADMIN/лестничных сборках
    один пролёт мог превратиться в 2-3 бокса."""
    global rows
    kept = []
    for r in rows:
        if r[0] != chunk or r[1] != "STAIR":
            continue
        x, y, z = float(r[2]), float(r[3]), float(r[4])
        dup = False
        for k in kept:
            if (x-k[0])**2 + (y-k[1])**2 + (z-k[2])**2 < 0.36:
                dup = True
                break
        if not dup:
            kept.append((x, y, z))
    if not kept:
        return
    allowed = set((round(a,3), round(b,3), round(c,3)) for a,b,c in kept)
    out = []
    for r in rows:
        if r[0] == chunk and r[1] == "STAIR":
            key = (round(float(r[2]),3), round(float(r[3]),3), round(float(r[4]),3))
            if key not in allowed:
                continue
        out.append(r)
    removed = len(rows) - len(out)
    rows = out
    if removed:
        log("    дедуп маршей: убрано %d" % removed)

def process(fname, chunk, pfh):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(RAW, fname))
    walls=[]; floors=[]; roofs=[]; stair_parts=[]; platforms=[]
    structural = defaultdict(list)      # house -> list of (bbox)

    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None: continue
        cls = classify(o)
        if cls is None: continue
        b = world_bbox(o)
        if cls == "PLATFORM":
            platforms.append(o)
            continue
        h = house_of(o.name)
        structural[h].append(b)
        if cls == "STAIR":
            stair_parts.append(o)
        elif cls == "FLOOR":
            floors.append((b, o.name))
        elif cls == "ROOF":
            roofs.append((b, o.name))
        elif cls == "WALL":
            dx=b[1]-b[0]; dy=b[3]-b[2]
            thin = min(dx, dy); long_ = max(dx, dy)
            # косяки/наличники/столбики (тонкие и короткие) в физику не берём — они стоят в проёмах
            if thin < 0.35 and long_ < 0.85:
                continue
            # ось стены по ГАБАРИТУ: тонкая горизонтальная = нормаль
            if dx <= dy:
                axis = 0; line = (b[0]+b[1])/2; a0, a1 = b[2], b[3]; thick = dx
            else:
                axis = 1; line = (b[2]+b[3])/2; a0, a1 = b[0], b[1]; thick = dy
            walls.append((axis, line, a0, a1, b[4], b[5], thick, h))

    house_boxes = {}
    for h, bs in structural.items():
        house_boxes[h] = (min(x[0] for x in bs), max(x[1] for x in bs),
                          min(x[2] for x in bs), max(x[3] for x in bs),
                          min(x[4] for x in bs), max(x[5] for x in bs))

    if platforms:
        cnt = write_platforms(platforms, pfh)
        log("  платформ в OBJ: %d" % cnt)

    add_walls(chunk, walls, house_boxes)
    add_stairs(chunk, stair_parts, house_boxes)
    _dedupe_stairs(chunk)
    for b,nm in floors:
        add_axis(chunk, "FLOOR", b, nm, house_boxes.get(house_of(nm)))
    by_house = defaultdict(list)
    for b,nm in roofs:
        by_house[house_of(nm)].append((b, nm))
    for house, items in by_house.items():
        # группа только из парапетов (без плиты крыши) — это ограждение деки, в физику не берём
        if all("PARAPET" in nm.upper() for _, nm in items):
            continue
        bs = [b for b, _ in items]
        add_axis(chunk, "ROOF", (min(x[0] for x in bs), max(x[1] for x in bs),
                                min(x[2] for x in bs), max(x[3] for x in bs),
                                min(x[4] for x in bs), max(x[5] for x in bs)), house,
                 house_boxes.get(house))

def main():
    for f in (OUT, OBJ, LOG):
        if os.path.exists(f): os.remove(f)
    flt = os.environ.get("COLL_CHUNKS", "").strip()
    chunks = CHUNKS
    if flt:
        want = [s.strip().upper() for s in flt.split(",") if s.strip()]
        chunks = [c for c in CHUNKS if any(w in c[1].upper() for w in want)]
    log("=== colliders_v3: чанков %d (фильтр: %s) ===" % (len(chunks), flt or "-"))
    t0=time.time()
    with open(OBJ, "w", encoding="utf-8") as pfh:
        for fname, chunk in chunks:
            if not os.path.exists(os.path.join(RAW, fname)):
                log("MISSING " + fname); continue
            try:
                process(fname, chunk, pfh)
            except Exception as e:
                log("ERROR %s: %s" % (fname, e))
            per = dict(stats.get(chunk, {}))
            extra = ("  ВНЕ ГАБАРИТА ДОМА=%d" % navr[chunk]) if navr[chunk] else ""
            log("  %-42s %s%s" % (chunk.replace("PRIMUM_",""), per, extra))
    with open(OUT, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["chunk","cls","cx","cy","cz","sx","sy","sz","yaw","pitch","name"])
        w.writerows(rows)
    total = defaultdict(int)
    for ch in stats:
        for c,n in stats[ch].items(): total[c] += n
    log("=== ИТОГО %s (%.1f c) ===" % (dict(total), time.time()-t0))
    log("CSV: %s строк=%d ; OBJ: %s" % (OUT, len(rows), OBJ))

main()
print("DONE")
