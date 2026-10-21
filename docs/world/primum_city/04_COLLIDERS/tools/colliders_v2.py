# colliders_v2.py — бокс-коллайдеры PRIMUM из ИСХОДНОЙ (несклеенной) геометрии RAW-блендов.
#
# Правила (по указанию заказчика, 2026-10-21):
#   WALK  — низ города (улицы/первый уровень): крупные боксы из BASE/20_PLATFORMS
#           (DECK_0/6/12/18 + PLATFORM_L06/12/18/24 = 8; PLATFORM_L00 = 9-й, мелкий).
#   STAIR — лестницы: ОДИН наклонный (диагональный) бокс на марш (pitch), без мельчения на ступени.
#   WALL  — стены: прогон по линии стены; ОКНА НЕ ДЫРКАМИ (заполняются объединением по линии),
#           дверные проёмы СОХРАНЯЮТСЯ (в линии остаётся разрыв > 0.3 м). Поддержаны повёрнутые стены.
#   FLOOR — полы/перекрытия: бокс на плиту.
#   ROOF  — крыша дома: 1 бокс на дом (плита + парапет в один объём).
# Пропуск: 30_WINDOWS, 40_INTERIOR, 60_DECOR, 20_ENTRANCE (двери — свои префаб-коллайдеры), маркеры.
#
# Координаты: RAW — ПРЕ-масштаба (k=1.0) => k=1.07 вокруг C, затем в ЛОКАЛЬНЫЕ координаты
# Unity-корня PRIMUM: local = (-s.x, s.z, -s.y). Пивот бокса = центр.
#
# CSV: chunk,cls,cx,cy,cz,sx,sy,sz,yaw,pitch,name
#   cx..cz — центр (локальные координаты PRIMUM); sx..sz — размеры в локальных осях бокса
#   (x — вдоль yaw, y — вверх, z — поперёк); yaw/pitch — градусы.
import bpy, os, csv, math, time
from collections import defaultdict
from mathutils import Vector

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW")
OUT = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\colliders_v2.csv"
LOG = r"C:\Projects\ProjectC_client\_analysis\colliders_v2.log"

K = 1.07
C = (7.5, -148.0, 18.5)
MIN_SIDE = 0.50
MERGE_GAP = 0.30
LINE_QUANT = 0.25
YAW_QUANT = 5.0
STAIR_THICK = 0.35

# 23 чанка города: имя файла -> имя узла в Unity
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
    with open(LOG, "a", encoding="utf-8") as f:
        f.write(m + "\n")
    print(m)

def ulocal(p):
    s = [C[i] + (p[i] - C[i]) * K for i in range(3)]
    return (-s[0], s[2], -s[1])

def local_bbox(o):
    return [Vector(c) for c in o.bound_box]

def world_bbox(o):
    m = o.matrix_world
    pts = [m @ Vector(c) for c in o.bound_box]
    xs=[p.x for p in pts]; ys=[p.y for p in pts]; zs=[p.z for p in pts]
    return (min(xs),max(xs),min(ys),max(ys),min(zs),max(zs))

def has_ancestor(o, names):
    p = o.parent
    while p is not None:
        if p.name in names:
            return True
        p = p.parent
    return False

def cpath(o):
    return "/".join([c.name for c in o.users_collection]).upper()

RAIL_TOKENS = ("RAIL", "GUARD", "HAND", "SKIRT", "BALUST", "_BR", "KERB", "KICK")

def is_stair_part(o):
    p = cpath(o); n = o.name.upper()
    if "STAIR" not in p and "STAIR" not in n and "STEPS" not in p:
        return False
    for t in RAIL_TOKENS:
        if t in n:
            return False
    return True

def classify(o):
    p = cpath(o); n = o.name.upper()
    if has_ancestor(o, ("DOORS", "LAMPS", "OUTLIERS", "DOOR_PARTS_UNASSIGNED")):
        return None
    if "20_PLATFORMS" in p:
        return "WALK"
    if "30_WINDOWS" in p or "WINDOW" in p or "20_ENTRANCE" in p:
        return None
    if "40_INTERIOR" in p or "FURN" in p or "60_DECOR" in p:
        return None
    if is_stair_part(o):
        return "STAIR"
    if "50_ROOF" in p or "ROOF" in p or "PARAPET" in n:
        return "ROOF"
    if "10_WALLS" in p or "WALL" in p or "SHELL" in p:
        return "WALL"
    if "05_FLOOR" in p or "FLOOR" in n or "SLAB" in n:
        return "FLOOR"
    return None

rows = []
stats = defaultdict(lambda: defaultdict(int))

def fmt(v): return ("%.3f" % v).replace(",", ".")

def add(chunk, cls, center_u, size_u, yaw=0.0, pitch=0.0, name=""):
    x,y,z = center_u; sx,sy,sz = size_u
    if max(sx, sy, sz) < MIN_SIDE:
        return
    rows.append([chunk, cls, fmt(x), fmt(y), fmt(z), fmt(sx), fmt(sy), fmt(sz),
                 "%.2f" % yaw, "%.2f" % pitch, name])
    stats[cls][chunk] += 1

def add_axis(chunk, cls, b, name=""):
    cx=(b[0]+b[1])/2; cy=(b[2]+b[3])/2; cz=(b[4]+b[5])/2
    ux,uy,uz = ulocal((cx,cy,cz))
    add(chunk, cls, (ux,uy,uz), ((b[1]-b[0])*K, (b[5]-b[4])*K, (b[3]-b[2])*K), 0, 0, name)

# ---------------------------------------------------------------- стены (OBB)

def wall_obb(o):
    """Разложить объект-стену: центр (world), dir (world, вдоль стены), length, height, thick."""
    lb = local_bbox(o)
    xs=[p.x for p in lb]; ys=[p.y for p in lb]; zs=[p.z for p in lb]
    dims = [max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs)]
    center_l = Vector(((max(xs)+min(xs))/2, (max(ys)+min(ys))/2, (max(zs)+min(zs))/2))
    m = o.matrix_world
    m3 = m.to_3x3()
    scale = [Vector((m3.col[0].x, m3.col[0].y, m3.col[0].z)).length,
             Vector((m3.col[1].x, m3.col[1].y, m3.col[1].z)).length,
             Vector((m3.col[2].x, m3.col[2].y, m3.col[2].z)).length]
    # горизонтальные локальные оси = 0,1; вертикаль = 2 (модель Z-up)
    horiz = [0, 1]
    thin = min(horiz, key=lambda i: dims[i]*scale[i])
    run = [i for i in horiz if i != thin][0]
    wd = {}; wl = {}; wt = {}; wh = {}
    for i in (0,1,2):
        v = Vector((0,0,0)); v[i] = 1.0
        wv = (m3 @ v)
        if i == 2:
            wh = wv.normalized()
        elif i == run:
            wd = wv.normalized()
        else:
            wl = wv.normalized()
    center_w = m @ center_l
    return center_w, wd, wl, dims[run]*scale[run], dims[2]*scale[2], dims[thin]*scale[thin]

def add_walls(chunk, items):
    """items: (yaw_key, line, a0, a1, z0, z1, thick, dir_unit(x,y), normal(x,y))"""
    groups = defaultdict(list)
    for it in items:
        groups[(it[0], round(it[1]/LINE_QUANT))].append(it)
    for key, lst in groups.items():
        yawk, _lq = key
        line = sum(it[1] for it in lst)/len(lst)
        nx, ny = lst[0][8]
        dx, dy = lst[0][7]
        spans = []
        for it in lst:
            # проекция bbox-углов на dir — уже посчитана как a0/a1
            spans.append((it[2], it[3]))
        spans.sort()
        merged = [list(spans[0])]
        for a0,a1 in spans[1:]:
            if a0 - merged[-1][1] <= MERGE_GAP:
                merged[-1][1] = max(merged[-1][1], a1)
            else:
                merged.append([a0,a1])
        z0 = min(it[4] for it in lst); z1 = max(it[5] for it in lst)
        thick = max(it[6] for it in lst)
        for a0,a1 in merged:
            length = a1-a0
            if length < MIN_SIDE: continue
            mid = (a0+a1)*0.5
            cx = nx*line + dx*mid
            cy = ny*line + dy*mid
            # yaw в Unity: локальная X бокса вдоль dir; Blender (dx,dy)->Unity (x,z)=(-dx,-dy)
            dux, duz = -dx, -dy
            yaw = math.degrees(math.atan2(-duz, dux))
            ux,uy,uz = ulocal((cx, cy, (z0+z1)/2))
            add(chunk, "WALL", (ux,uy,uz), (length*K, (z1-z0)*K, thick*K), yaw, 0, "")

# ---------------------------------------------------------------- лестницы (группами)

def fit_flight(objs):
    pts = []
    for o in objs:
        m = o.matrix_world
        for v in o.data.vertices:
            pts.append(m @ v.co)
    if len(pts) < 12:
        return None
    xs=[p.x for p in pts]; ys=[p.y for p in pts]; zs=[p.z for p in pts]
    dx=max(xs)-min(xs); dy=max(ys)-min(ys)
    if dx < 0.5 and dy < 0.5:
        return None

    def slope(axis):
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

    sx, sy = slope(0), slope(1)
    if abs(sx) >= abs(sy):
        axis=0; run=dx; width=dy; sl=sx
    else:
        axis=1; run=dy; width=dx; sl=sy
    rise = abs(sl)*run
    if run < 1.0 or width < 0.5 or rise < 0.3 or (rise/run) > 1.2:
        return None
    pitch = math.degrees(math.atan2(rise, run))
    dir_b = ((1.0 if sl>0 else -1.0), 0.0) if axis==0 else (0.0, (1.0 if sl>0 else -1.0))
    dir_u = (-dir_b[0], -dir_b[1])
    yaw = math.degrees(math.atan2(dir_u[0], dir_u[1]))
    cx=(min(xs)+max(xs))/2; cy=(min(ys)+max(ys))/2; cz=(min(zs)+max(zs))/2
    return (cx,cy,cz, run, width, rise, yaw, pitch)

def add_stairs(chunk, groups):
    for key, objs in groups.items():
        f = fit_flight(objs)
        if f is None:
            # не распознали — грубый горизонтальный бокс по объединению
            bs = [world_bbox(o) for o in objs]
            b = (min(x[0] for x in bs), max(x[1] for x in bs), min(x[2] for x in bs),
                 max(x[3] for x in bs), min(x[4] for x in bs), max(x[5] for x in bs))
            add_axis(chunk, "STAIR", b, key + "|flat")
            continue
        cx,cy,cz, run, width, rise, yaw, pitch = f
        ux,uy,uz = ulocal((cx,cy,cz))
        length = math.sqrt(run*run + rise*rise) * K
        add(chunk, "STAIR", (ux,uy,uz), (width*K, STAIR_THICK, length), yaw, pitch, key)

# ---------------------------------------------------------------- чанк

def house_of(name):
    parts = name.split("_")
    return "_".join(parts[:3]) if len(parts) >= 3 else name

def process(fname, chunk):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(RAW, fname))
    walls=[]; floors=[]; roofs=[]
    stair_groups = defaultdict(list)

    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None:
            continue
        cls = classify(o)
        if cls is None:
            continue
        b = world_bbox(o)
        if cls == "WALK":
            add_axis(chunk, "WALK", b, o.name)
        elif cls == "STAIR":
            p = cpath(o)
            # группа = ближайшая коллекция со STAIR, иначе — имя объекта (BASE: отдельные марши)
            key = o.name
            for seg in p.split("/"):
                if "STAIR" in seg:
                    key = seg
                    break
            stair_groups[key].append(o)
        elif cls == "WALL":
            cw, d, nl, length, height, thick = wall_obb(o)
            if length < MIN_SIDE or height < MIN_SIDE:
                continue
            a0 = cw.x*d.x + cw.y*d.y
            a1 = a0 + length
            line = cw.x*nl.x + cw.y*nl.y
            yawk = round(math.degrees(math.atan2(d.y, d.x)) / YAW_QUANT)
            walls.append((yawk, line, a0, a1, cw.z-height/2, cw.z+height/2, thick,
                          (d.x, d.y), (nl.x, nl.y)))
        elif cls == "FLOOR":
            floors.append((b, o.name))
        elif cls == "ROOF":
            roofs.append((b, o.name))

    add_walls(chunk, walls)
    add_stairs(chunk, stair_groups)
    for b,nm in floors:
        add_axis(chunk, "FLOOR", b, nm)
    by_house = defaultdict(list)
    for b,nm in roofs:
        by_house[house_of(nm)].append(b)
    for house, bs in by_house.items():
        add_axis(chunk, "ROOF", (min(x[0] for x in bs), max(x[1] for x in bs),
                                min(x[2] for x in bs), max(x[3] for x in bs),
                                min(x[4] for x in bs), max(x[5] for x in bs)), house)

def main():
    for f in (OUT, LOG):
        if os.path.exists(f):
            os.remove(f)
    log("=== colliders_v2: чанков %d ===" % len(CHUNKS))
    t0=time.time()
    for fname, chunk in CHUNKS:
        if not os.path.exists(os.path.join(RAW, fname)):
            log("MISSING " + fname); continue
        try:
            process(fname, chunk)
        except Exception as e:
            log("ERROR %s: %s" % (fname, e))
        per = {}
        for c in stats:
            if chunk in stats[c]:
                per[c] = stats[c][chunk]
        log("  %-42s %s" % (chunk.replace("PRIMUM_", ""), per))
    with open(OUT, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["chunk","cls","cx","cy","cz","sx","sy","sz","yaw","pitch","name"])
        w.writerows(rows)
    total = defaultdict(int)
    for c in stats:
        total[c] = sum(stats[c].values())
    log("=== ИТОГО %s (%.1f c) ===" % (dict(total), time.time()-t0))
    log("CSV: %s строк=%d" % (OUT, len(rows)))

main()
print("DONE")
