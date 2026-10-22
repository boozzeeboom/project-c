# houses_boxes.py — БОКСОВЫЕ коллайдеры домов PRIMUM. ТОЛЬКО конструктив, ровно по схеме:
#
#   ОДНОЭТАЖНЫЙ ДОМ:  3 стены по 1 боксу + крыша 1 бокс; фасад с дверью = 3 бокса (лево/право/над);
#                     внутренняя стена: 1 бокс без двери, 3 бокса с дверью.
#   ДВУХЭТАЖНЫЙ ДОМ:  то же + перекрытие (пол 2-го этажа) = 1 бокс на уровень
#                     + ЛЕСТНИЦА = 1 наклонный (диагональный) бокс от первой ступени до последней.
#
# Мебель, окна и декор в физику НЕ ПОПАДАЮТ ВООБЩЕ. Раньше их тащило геометрическое
# «угадывание» (bbox-plate / bbox-wall) и правило «F<этаж> → перекрытие», из-за которого весь
# реквизит этажа (F0_BILLIARD_BODY, F0_CAU, FURN_2_BED, C1_LIFE_CAB) становился боксом, а игрок
# не мог пройти ни в одном доме. Теперь класс — только по имени из узкого белого списка.
#
# Как собирается стена. Все стены дома раскладываются по ПЛОСКОСТЯМ: у каждой стены берётся
# главная ось (вдоль/поперёк дома) и смещение плоскости. Объекты одной плоскости (кладка,
# простенки, перемычки, заливка окон) объединяются в ОДИН бокс по прогону; разрыв прогона
# больше RUN_GAP — это уже другая стена. Из бокса вычитаются проёмы дверей/арок, и получаются
# боксы лево / право / над дверью.
#
# Выход: docs/world/primum_city/04_COLLIDERS/houses_boxes.json
#   {"chunks":[{"chunk":"PRIMUM_HOUSES_...","walls":[box],"floors":[box],"roofs":[box],
#              "stairs":[box],"doors":[door],"report":{...}}]}
#   box  = {"c":[x,y,z], "s":[sx,sy,sz], "ry":deg}          — поворот вокруг вертикали
#        | {"c":[x,y,z], "s":[sx,sy,sz], "q":[x,y,z,w]}     — полный поворот (наклонный марш)
#   door = {"c":[x,y,z], "t":[tx,tz], "w":м, "h":м}          — для проверки проходимости в Unity
#   Координаты локальные для корня PRIMUM_COLLIDERS: local = (-s.x, s.z, -s.y), s = C + (p-C)*K.
#
# Запуск:
#   "W:\...\blender.exe" -b --factory-startup --python houses_boxes.py
#   BOXES_CHUNKS="S7_SE_P1" — фильтр по подстроке имени чанка
import bpy, os, json, sys, math, time
from collections import defaultdict
from mathutils import Vector, Matrix

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX

RAW = HX.RAW
OUT_JSON = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\houses_boxes.json"

MIN_BOX = 0.10          # м, вырожденные обрезки не выпускаем
CLEAR_H = 2.10          # м, высота ПРОХОДА от порога вверх (не габарит полотна!)
MIN_CLEAR_W = 0.90      # м, минимальная ширина прохода
SNAP_DEG = 0.5          # °, допуск «бокс по осям»
RUN_GAP = 0.40          # м, разрыв по прогону, после которого это ДРУГАЯ стена
PLANE_TOL = 0.28        # м, допуск одной плоскости
LEVEL_Z = 0.35          # м, допуск одного уровня перекрытия
PLATE_SHARE = 0.20      # плита должна занимать >= 20% площади дома
ROOF_T_MIN = 0.25       # м, минимальная толщина бокса крыши

# ---------------------------------------------------------------- словари видов
ARCH_KIND = {"ARCH", "ARCADE", "PASSAGE", "PASS", "PORTAL"}
STAIR_KIND = {"STAIR", "STAIRS", "STAIRCASE", "STEPS", "STAIRTREAD", "FLIGHT", "STFLIGHT", "PORCHST"}
SLAB_KIND = {"SLAB", "FLOOR", "FLOORS", "ATTIC", "PLATE", "MEZZ", "MEZZANINE", "GALLERY",
             "VER", "VERANDA", "PORCH", "PLATFORM", "BASEPLATE"}
ROOF_KIND = {"ROOF", "ROOFS", "PARAPET", "GABLE", "HIP", "HIPGABLE", "MANSARD", "TENT", "CORNICE"}
WALL_KIND = {"WALL", "WALLS", "SHELL", "PART", "PARTITION", "PARTITIONS", "BACKWALL", "WA",
             "INTWA", "W2", "ZX", "MURO", "STENA"}

# Всё, что НЕ конструктив. Проверяется и по сырым, и по очищенным от цифр токенам.
VETO = {
    # окна и столярка
    "WIN", "WINDOW", "WINBOX", "WINPLANT", "GWIN", "WINH", "SASH", "GLASS", "GLAZE", "FRAME",
    "CASING", "SILL", "TRIM", "TRIMB", "TRIMJ", "SHUT", "FAC", "FACADE", "LATTICE", "GRATE",
    # двери и фурнитура
    "DOOR", "DOORS", "DF", "DFI", "HNG", "HINGE", "KNOB", "HDL", "HANDLE", "LOCK", "LATCH", "KEY",
    # мебель
    "FURN", "FURNITURE", "BED", "CAB", "CABINET", "TABLE", "CHAIR", "STOOL", "SOFA", "DESK",
    "SHELF", "SHELVES", "WARDROBE", "DRESSER", "BENCH", "COUNTER", "ISLAND", "SINK", "BASIN",
    "WC", "TOILET", "TUB", "BATH", "SHOWER", "LIFE", "CLT", "CLUTTER", "CUSHION", "MATTRESS",
    "PILLOW", "MIRROR", "CLOCK", "PAINT", "ART", "BOOK", "BOOKS", "PAPER", "BOARD", "TRAY",
    "DISH", "BOWL", "CUP", "BOTTLE", "BARREL", "CRATE", "SACK", "JAR", "JARL", "BUCKET", "BASKET",
    "VASE", "POT", "PLANT",
    # декор и текстиль
    "DECOR", "DTL", "DETAIL", "DETAILS", "SIGN", "BANNER", "RUG", "CARPET", "CLOTH", "CURTAIN",
    "CURT", "SHADE", "FABRIC", "LAMP", "LANTERN", "CANDLE", "CHANDELIER", "LIGHT",
    # перила и ограждения
    "BAL", "BALS", "BALUST", "BALUSTER", "RAIL", "RAILS", "RAILING", "HANDRAIL", "GUARD", "NEWEL",
    "BALC", "BALCRAIL",
    # инженерия и кровельные причиндалы
    "PIPE", "RISER", "VENT", "ROOFVENT", "DUCT", "CABLE", "WIRE", "GEAR", "TOOL", "FAN", "FANHUB",
    "VALVE", "WHISTLE", "HOOP", "STRUT", "BLADE", "STEAM", "CHIM", "BASE", "BODY", "VBODY",
    "WCAP", "CAP", "CAPS", "ROD", "CHAIN", "HOOK", "ROP", "LADDER",
}

DOOR_VETO = {"FURN", "CAB", "SHUT", "SHELF", "WARDROBE", "WIN", "WINH", "DF", "DFI", "BAL", "RAIL"}


# ---------------------------------------------------------------- утилиты
def house_key(toks):
    """Дом = токены до первого чисто цифрового (номер дома: RES_NE_201, GLD_CREATION_01,
    TER2_03). Так обломки имён вида GLD_CREATION_01_BELT_B_27 не плодят «фальшивые дома»."""
    out = []
    for t in toks:
        out.append(t)
        if t.isdigit() and len(out) >= 2:
            break
    if len(out) > 3:
        out = out[:3]
    return "_".join(out) if out else "_".join(toks[:2])


def obox(o):
    m = o.matrix_world
    return [m @ Vector(c) for c in o.bound_box]


def aabb(pts):
    return (min(p.x for p in pts), max(p.x for p in pts),
            min(p.y for p in pts), max(p.y for p in pts),
            min(p.z for p in pts), max(p.z for p in pts))


def dims(bb):
    return (bb[1] - bb[0], bb[3] - bb[2], bb[5] - bb[4])


def overlap(a, b, gap):
    return (a[0] - gap <= b[1] and b[0] - gap <= a[1] and
            a[2] - gap <= b[3] and b[2] - gap <= a[3] and
            a[4] - gap <= b[5] and b[4] - gap <= a[5])


def principal_theta(pts):
    """Угол главной оси в плане (радианы). Для почти квадратных объектов берём ту ось,
    которая длиннее — иначе проём двери спроецируется на толщину стены."""
    n = len(pts)
    mx = sum(p.x for p in pts) / n
    my = sum(p.y for p in pts) / n
    sxx = sxy = syy = 0.0
    for p in pts:
        dx, dy = p.x - mx, p.y - my
        sxx += dx * dx
        sxy += dx * dy
        syy += dy * dy
    theta = 0.5 * math.atan2(2.0 * sxy, sxx - syy)
    if theta > math.pi / 2:
        theta -= math.pi
    elif theta <= -math.pi / 2:
        theta += math.pi
    axw = (max(p.x for p in pts) - min(p.x for p in pts)) * (max(p.y for p in pts) - min(p.y for p in pts))
    u0, u1, v0, v1 = project(pts, theta)
    if (u1 - u0) * (v1 - v0) >= 0.95 * axw:
        dx = max(p.x for p in pts) - min(p.x for p in pts)
        dy = max(p.y for p in pts) - min(p.y for p in pts)
        return 0.0 if dx >= dy else math.pi / 2.0
    return theta


def project(pts, theta):
    cu, su = math.cos(theta), math.sin(theta)
    us = [p.x * cu + p.y * su for p in pts]
    vs = [-p.x * su + p.y * cu for p in pts]
    return min(us), max(us), min(vs), max(vs)


def uv(p, ca, sa):
    return (p.x * ca + p.y * sa, -p.x * sa + p.y * ca)


def word(p, u, v):
    """Обратно из (u,v) в мировой XY."""
    cu, su = p
    return (u * cu - v * su, u * su + v * cu)


def rect_subtract(u0, u1, v0, v1, holes):
    """Прямоугольник (прогон × высота) минус проёмы -> список прямоугольников."""
    hs = [(max(h[0], u0), min(h[1], u1), max(h[2], v0), min(h[3], v1)) for h in holes]
    hs = [h for h in hs if h[1] - h[0] > 1e-4 and h[3] - h[2] > 1e-4]
    if not hs:
        return [(u0, u1, v0, v1)]
    ub = sorted({u0, u1} | {h[0] for h in hs} | {h[1] for h in hs})
    out = []
    for i in range(len(ub) - 1):
        a, b = ub[i], ub[i + 1]
        if b - a < 1e-4:
            continue
        mid = 0.5 * (a + b)
        ivs = sorted((h[2], h[3]) for h in hs if h[0] <= mid <= h[1])
        cur = v0
        for iv in ivs:
            if iv[0] - cur > MIN_BOX:
                out.append((a, b, cur, iv[0]))
            cur = max(cur, iv[1])
        if v1 - cur > MIN_BOX:
            out.append((a, b, cur, v1))
    return out


def to_local(p):
    s = [HX.C[i] + (p[i] - HX.C[i]) * HX.K for i in range(3)]
    return (-s[0], s[2], -s[1])


def mdir(v):
    """Линейная часть world -> local: (x,y,z) -> (-x, z, -y)."""
    return (-v.x, v.z, -v.y)


def make_box(cw, u_len, v_len, h, theta):
    """Бокс по осям дома: u_len вдоль U, v_len вдоль V, h по вертикали, поворот вокруг Y."""
    cw = Vector(cw)
    u = Vector((math.cos(theta), math.sin(theta), 0.0))
    vv = Vector((-math.sin(theta), math.cos(theta), 0.0))
    Lu = Vector(mdir(u))
    Lv = Vector(mdir(vv))

    if abs(math.degrees(math.atan2(Lu.z, Lu.x))) <= 45.0 or abs(math.degrees(math.atan2(Lu.z, Lu.x))) >= 135.0:
        phi = math.degrees(math.atan2(-Lu.z, Lu.x))
        size = [u_len, h, v_len]
    else:
        phi = math.degrees(math.atan2(-Lv.z, Lv.x))
        size = [v_len, h, u_len]

    k = round(phi / 90.0)
    if abs(phi - k * 90.0) <= SNAP_DEG:
        phi = k * 90.0
    phi = ((phi + 180.0) % 360.0) - 180.0
    if abs(abs(phi) - 180.0) <= SNAP_DEG:      # 180° — тот же самый бокс
        phi = 0.0
    if abs(abs(phi) - 90.0) <= SNAP_DEG:       # 90/270 = тот же бокс с переставленными X/Z
        phi = 0.0
        size = [size[2], size[1], size[0]]

    return {"c": [round(x, 4) for x in to_local(cw)],
            "s": [round(max(s, MIN_BOX), 4) for s in size],
            "ry": round(phi, 3)}


def make_ramp_box(cw, slope_dir_w, width, length, thick):
    """Наклонный бокс марша: локальный Z бокса идёт вдоль уклона, локальный X — поперёк."""
    z = Vector(mdir(Vector(slope_dir_w))).normalized()
    up = Vector((0.0, 1.0, 0.0))
    x = up.cross(z)
    if x.length < 1e-5:
        x = Vector((1.0, 0.0, 0.0))
    x.normalize()
    y = z.cross(x)
    q = Matrix((x, y, z)).transposed().to_quaternion()
    return {"c": [round(v, 4) for v in to_local(cw)],
            "s": [round(max(width, MIN_BOX), 4), round(max(thick, MIN_BOX), 4), round(max(length, MIN_BOX), 4)],
            "q": [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)]}


# ---------------------------------------------------------------- классификация
def strict_class(o, toks):
    """(класс, правило). Только конструктив; мебель/окна/декор не проходят вообще."""
    raw = set(toks)
    al = set(HX.alpha(t) for t in toks) | raw
    if al & VETO:
        return None, "veto"
    a = [HX.alpha(t) for t in toks]
    if any(t in ARCH_KIND for t in a):
        return "ARCH", "name"
    if any(t in STAIR_KIND for t in a):
        return "STAIR", "name"
    if any(t in SLAB_KIND for t in a) or any(t.startswith("SLAB") for t in toks):
        return "SLAB", "name"
    if any(t in ROOF_KIND for t in a):
        return "ROOF", "name"
    if any(t in WALL_KIND for t in a):
        # Не пластина — значит это «скорлупа» комнаты (один объект = три стены вокруг
        # помещения). Бокс по её габариту заливает комнату целиком — такие не берём.
        dx, dy, dz = dims(aabb(obox(o)))
        if min(dx, dy) > 0.60 and max(dx, dy) > 1.2:
            return None, "wall-blob"
        return "WALL", "name"

    # узкий геометрический запас: только однозначные формы и без слов-вето
    dx, dy, dz = dims(aabb(obox(o)))
    if dz >= 2.0 and min(dx, dy) <= 0.40 and max(dx, dy) >= 2.0:
        return "WALL", "bbox-wall"
    if dz <= 0.45 and dx >= 2.0 and dy >= 2.0:
        return "SLAB", "bbox-slab"
    return None, "skip"


# ---------------------------------------------------------------- сбор
def collect(chunk_path):
    bpy.ops.wm.open_mainfile(filepath=chunk_path)
    walls = defaultdict(list)
    slabs = defaultdict(list)
    roofs = defaultdict(list)
    stairs = defaultdict(list)
    doors, arches = [], []
    rep = defaultdict(int)

    anc = [o for o in bpy.data.objects]
    for o in anc:
        if o.type != 'MESH' or o.library is not None:
            continue
        if o.data is None or len(o.data.polygons) == 0:
            continue
        if HX.has_ancestor(o, HX.ANCESTORS):
            continue
        toks = HX.base_tokens(o.name)
        al = [HX.alpha(t) for t in toks]
        pts = obox(o)
        bb = aabb(pts)

        if any(t in ("DOOR", "DOORS") for t in al):
            if (set(al) | set(toks)) & DOOR_VETO:
                rep["doors_skipped"] += 1
                continue
            doors.append((bb, pts, False, o.name))
            rep["doors"] += 1
            continue

        cls, rule = strict_class(o, toks)
        if cls is None:
            continue
        entry = (o, pts, bb)
        h = house_key(toks)
        if cls == "WALL":
            walls[h].append(entry)
            rep["wall_objs"] += 1
        elif cls == "SLAB":
            slabs[h].append(entry)
            rep["slab_objs"] += 1
        elif cls == "ROOF":
            roofs[h].append(entry)
            rep["roof_objs"] += 1
        elif cls == "STAIR":
            stairs[h].append(entry)
            rep["stair_objs"] += 1
        elif cls == "ARCH":
            arches.append((bb, pts, True, o.name))
            rep["arches"] += 1
    return walls, slabs, roofs, stairs, doors, arches, rep


# ---------------------------------------------------------------- плоскости стен
def plane_groups(entries, th):
    """Разложить стены дома по плоскостям: (along_u, [(entry,...)]) -> сгруппировано."""
    ca, sa = math.cos(th), math.sin(th)
    prep = []
    for o, pts, bb in entries:
        pu0, pu1, pv0, pv1 = project(pts, th)
        ot = principal_theta(pts)
        delta = (ot - th) % math.pi
        along_u = delta < math.pi / 4 or delta > 3 * math.pi / 4
        prep.append({"e": (o, pts, bb), "u0": pu0, "u1": pu1, "v0": pv0, "v1": pv1,
                     "uc": 0.5 * (pu0 + pu1), "vc": 0.5 * (pv0 + pv1),
                     "along_u": along_u, "z0": bb[4], "z1": bb[5]})

    out = []
    for along_u in (True, False):
        part = [p for p in prep if p["along_u"] == along_u]
        key = (lambda p: p["vc"]) if along_u else (lambda p: p["uc"])
        part.sort(key=key)
        cur, base = [], None
        for p in part:
            k = key(p)
            if cur and k - base > PLANE_TOL:
                out.append((along_u, cur))
                cur, base = [], None
            if base is None:
                base = k
            cur.append(p)
        if cur:
            out.append((along_u, cur))
    return out


def split_runs(group, along_u):
    """Разбить плоскость на связные прогоны: разрыв > RUN_GAP — это другая стена."""
    key0 = (lambda p: p["u0"]) if along_u else (lambda p: p["v0"])
    key1 = (lambda p: p["u1"]) if along_u else (lambda p: p["v1"])
    group = sorted(group, key=key0)
    runs, cur, hi = [], [], None
    for p in group:
        a, b = key0(p), key1(p)
        if cur and a - hi > RUN_GAP:
            runs.append(cur)
            cur = []
            hi = None
        cur.append(p)
        hi = b if hi is None else max(hi, b)
    if cur:
        runs.append(cur)
    return runs


PLANE_REACH = 1.00      # м, проём режет все стены в этой полосе от полотна


def door_windows(doors, arches, th):
    """Одно окно проёма на дверь, в осях дома: (along_u, a0, a1, t0, t1, z0, z1).

    Окно ОДНО на все стены, которые его пересекают: стена часто собрана из нескольких
    объектов в разных плоскостях (PLANE_TOL), и проход надо резать во всех сразу —
    иначе одна из плоскостей остаётся сплошной и дверь заперта. Поэтому по толщине
    окно расширено на PLANE_REACH в обе стороны, а по ширине не уже MIN_CLEAR_W
    и всегда по центру полотна.
    """
    raw = []
    for bb, pts, is_arch, _nm in doors + arches:
        pu0, pu1, pv0, pv1 = project(pts, th)
        along_u = (pu1 - pu0) >= (pv1 - pv0)     # полотно тянется вдоль прогона U
        if along_u:
            w = max(pu1 - pu0, MIN_CLEAR_W)
            mid = 0.5 * (pu0 + pu1)
            a0, a1 = mid - 0.5 * w, mid + 0.5 * w
            t0, t1 = pv0 - PLANE_REACH, pv1 + PLANE_REACH
        else:
            w = max(pv1 - pv0, MIN_CLEAR_W)
            mid = 0.5 * (pv0 + pv1)
            a0, a1 = mid - 0.5 * w, mid + 0.5 * w
            t0, t1 = pu0 - PLANE_REACH, pu1 + PLANE_REACH

        # Проход привязываем к ВЕРХУ полотна: низ дверного объекта в модели может уходить
        # в фундамент (ниже пола на метр), и проход «от низа» давал метр высоты — игрок
        # упирался в перекрытие. От верха вниз на CLEAR_H — всегда уровень пола.
        if is_arch:
            z0, z1 = bb[4], bb[4] + CLEAR_H
        else:
            z1 = bb[5]
            z0 = z1 - CLEAR_H
        raw.append((along_u, a0, a1, t0, t1, z0, z1, _nm))

    # Одна дверь в модели — это десяток объектов (полотно, наличники, петли, ручка,
    # панели). Склеиваем их в ОДИН проём: одинаковое направление, общая плоскость,
    # разрыв по прогону не больше DOOR_JOIN.
    DOOR_JOIN = 0.20
    DOOR_MAX = 1.60
    raw.sort(key=lambda w: (w[0], w[3], w[1]))
    wins = []
    for w in raw:
        if wins:
            p = wins[-1]
            if (p[0] == w[0] and w[3] <= p[4] + 1e-6 and w[4] >= p[3] - 1e-6
                    and w[1] <= p[2] + DOOR_JOIN
                    and max(p[2], w[2]) - min(p[1], w[1]) <= DOOR_MAX):
                wins[-1] = (p[0], p[1], max(p[2], w[2]), min(p[3], w[3]),
                            max(p[4], w[4]), min(p[5], w[5]), max(p[6], w[6]), p[7])
                continue
        wins.append(w)
    return wins


def run_openings(windows, along_u, a0, a1, t0, t1, y0, y1):
    """Проёмы, попадающие в этот прогон. Ширина — вдоль прогона (по центру полотна)."""
    holes = []
    used = set()
    for (wi, (w_along_u, wa0, wa1, wt0, wt1, wz0, wz1, _dw)) in enumerate(windows):
        if w_along_u != along_u:                # проём поперёк этой стены — не её
            continue
        if wt1 < t0 - 1e-6 or wt0 > t1 + 1e-6:  # другая плоскость, не достаём
            continue

        a, b = max(wa0, a0), min(wa1, a1)
        if b - a <= 1e-4:                       # прогон кончился ровно в двери
            continue
        py0, py1 = max(wz0, y0), min(wz1, y1)
        if py1 - py0 < 0.60:                    # выше стены проход не нужен
            continue
        holes.append((a, b, py0, py1))
        used.add(wi)

    holes.sort()
    merged = []
    for h in holes:
        if merged and h[0] <= merged[-1][1] + 1e-4:
            m = merged[-1]
            merged[-1] = (m[0], max(m[1], h[1]), min(m[2], h[2]), max(m[3], h[3]))
        else:
            merged.append(h)
    return merged, used


def build_walls(name, entries, doors, arches, out_walls, out_doors, rep):
    wpts = []
    for o, pts, bb in entries:
        wpts.extend(pts)
    th = principal_theta(wpts)
    ca, sa = math.cos(th), math.sin(th)
    wins = door_windows(doors, arches, th)

    boxes = 0
    for along_u, grp in plane_groups(entries, th):
        for run in split_runs(grp, along_u):
            if along_u:
                a0 = min(p["u0"] for p in run)
                a1 = max(p["u1"] for p in run)
                t0 = min(p["v0"] for p in run)
                t1 = max(p["v1"] for p in run)
            else:
                a0 = min(p["v0"] for p in run)
                a1 = max(p["v1"] for p in run)
                t0 = min(p["u0"] for p in run)
                t1 = max(p["u1"] for p in run)
            y0 = min(p["z0"] for p in run)
            y1 = max(p["z1"] for p in run)
            if a1 - a0 < MIN_BOX or y1 - y0 < MIN_BOX:
                continue
            if (t1 - t0) > 1.00:                 # слиплись разные плоскости — бокс-блоб не нужен
                rep["run_thick"] += 1
                continue

            holes, used = run_openings(wins, along_u, a0, a1, t0, t1, y0, y1)
            for wi in sorted(used):
                aw0, aw1, tw0, tw1, wz0, wz1 = wins[wi][1:7]
                if wins[wi][0]:
                    cu, cv = 0.5 * (aw0 + aw1), 0.5 * (tw0 + tw1)
                else:
                    cu, cv = 0.5 * (tw0 + tw1), 0.5 * (aw0 + aw1)
                wx, wy = word((ca, sa), cu, cv)
                out_doors.append({"c": [round(v, 3) for v in to_local((wx, wy, 0.5 * (wz0 + wz1)))],
                                  "n": wins[wi][7],
                                  "t": [round(-ca, 4), round(-sa, 4)],
                                  "w": round(aw1 - aw0, 3),
                                  "h": round(wz1 - wz0, 3)})

            for (da0, da1, dy0, dy1) in rect_subtract(a0, a1, y0, y1, holes):
                uu = 0.5 * (da0 + da1)
                vv = 0.5 * (t0 + t1)
                if along_u:
                    wx, wy = word((ca, sa), uu, vv)
                    out_walls.append(make_box((wx, wy, 0.5 * (dy0 + dy1)),
                                              da1 - da0, max(t1 - t0, MIN_BOX), dy1 - dy0, th))
                else:
                    wx, wy = word((ca, sa), vv, uu)
                    out_walls.append(make_box((wx, wy, 0.5 * (dy0 + dy1)),
                                              max(t1 - t0, MIN_BOX), da1 - da0, dy1 - dy0, th))
                boxes += 1
    return boxes


# ---------------------------------------------------------------- крыша и перекрытия
def build_roof(entries, rep):
    puts = []
    for o, pts, bb in entries:
        puts.extend(pts)
    if not puts:
        return []
    return _roof_box(puts)


def synth_roof(walls):
    """Крыши объектом нет — верх дома всё равно закрываем ОДНИМ боксом по габариту стен."""
    puts = []
    for o, pts, bb in walls:
        puts.extend(pts)
    if not puts:
        return []
    return _roof_box(puts)


def _roof_box(puts):
    th = principal_theta(puts)
    ca, sa = math.cos(th), math.sin(th)
    out = []
    bands = defaultdict(list)
    for p in puts:
        bands[round(p.z, 1)].append(p)
    order = sorted(bands)
    merged = []
    for z in order:
        if merged and z - merged[-1][0] <= 1.8:
            merged[-1][1].extend(bands[z])
        else:
            merged.append([z, list(bands[z])])
    for _z, grp in merged:
        u0, u1, v0, v1 = project(grp, th)
        z0 = min(p.z for p in grp)
        z1 = max(p.z for p in grp)
        if u1 - u0 < MIN_BOX or v1 - v0 < MIN_BOX:
            continue
        if z1 - z0 < ROOF_T_MIN:
            z0 = z1 - ROOF_T_MIN
        wx, wy = word((ca, sa), 0.5 * (u0 + u1), 0.5 * (v0 + v1))
        out.append(make_box((wx, wy, 0.5 * (z0 + z1)), u1 - u0, v1 - v0, z1 - z0, th))
    return out


def build_slabs(name, entries, walls, rep):
    """Перекрытие = ОДИН бокс на уровень. Плита-мебель и мелкие полки отсеиваются площадью."""
    if not entries:
        return []
    wpts = []
    for o, pts, bb in walls:
        wpts.extend(pts)
    ref = wpts if wpts else [p for o, pts, bb in entries for p in pts]
    th = principal_theta(ref)
    hu0, hu1, hv0, hv1 = project(ref, th)
    house_area = max(1.0, (hu1 - hu0) * (hv1 - hv0))

    good = []
    pu = []
    for o, pts, bb in entries:
        a0, a1, b0, b1 = project(pts, th)
        area = (a1 - a0) * (b1 - b0)
        pu.append(area)
        if (bb[5] - bb[4]) > 0.90:              # не плита
            rep["slab_thick"] += 1
            continue
        good.append((o, pts, bb, area))
    if not good:
        return []
    # порог по самой крупной плите дома: мебель и полочки не проходят, реальные плиты — да
    top = max(g[3] for g in good)
    limit = max(4.0, 0.20 * top)
    keep = []
    for o, pts, bb, area in good:
        if area < limit:
            rep["slab_small"] += 1
            continue
        keep.append((o, pts, bb))
    good = keep
    if not good:
        return []

    levels = defaultdict(list)
    for o, pts, bb in good:
        levels[round(bb[5], 1)].append((o, pts, bb))
    order = sorted(levels)
    merged = []
    for z in order:
        if merged and z - merged[-1][0] <= LEVEL_Z:
            merged[-1][1].extend(levels[z])
        else:
            merged.append([z, list(levels[z])])

    ca, sa = math.cos(th), math.sin(th)
    out = []
    for _z, grp in merged:
        pts_all = []
        for o, pts, bb in grp:
            pts_all.extend(pts)
        u0, u1, v0, v1 = project(pts_all, th)
        z0 = min(p.z for p in pts_all)
        z1 = max(p.z for p in pts_all)
        if u1 - u0 < MIN_BOX or v1 - v0 < MIN_BOX:
            continue
        wx, wy = word((ca, sa), 0.5 * (u0 + u1), 0.5 * (v0 + v1))
        out.append(make_box((wx, wy, 0.5 * (z0 + z1)), u1 - u0, v1 - v0, max(z1 - z0, MIN_BOX), th))
    return out


# ---------------------------------------------------------------- марши
def stair_entries_verts(entries):
    pts = []
    for o, _p, _b in entries:
        mw = o.matrix_world
        for v in o.data.vertices:
            pts.append(mw @ v.co)
    return pts


def ramp_from_points(pts, rep):
    """Один наклонный бокс по облаку вершин марша: МНК-прямая z = a*u + b."""
    if len(pts) < 12:
        return None
    flat = [Vector((p.x, p.y, 0.0)) for p in pts]
    th = principal_theta(flat)
    ca, sa = math.cos(th), math.sin(th)
    us, vs, zs = [], [], []
    for p in pts:
        u, v = uv(p, ca, sa)
        us.append(u)
        vs.append(v)
        zs.append(p.z)
    u0, u1 = min(us), max(us)
    v0, v1 = min(vs), max(vs)
    if u1 - u0 < 0.50 or v1 - v0 < 0.30:
        return None
    n = len(us)
    Su = sum(us)
    Sz = sum(zs)
    Suu = sum(u * u for u in us)
    Suz = sum(u * z for u, z in zip(us, zs))
    den = n * Suu - Su * Su
    a = 0.0 if abs(den) < 1e-6 else (n * Suz - Su * Sz) / den
    b = (Sz - a * Su) / n
    if abs(a) > 3.0:
        return None

    um = 0.5 * (u0 + u1)
    vm = 0.5 * (v0 + v1)
    zm = a * um + b
    pitch = math.atan(a)
    run = (u1 - u0)
    length = run / max(0.5, math.cos(pitch)) + 0.30
    width = max(0.45, v1 - v0)
    thick = 0.30

    wx, wy = word((ca, sa), um, vm)
    slope = Vector((ca, sa, 0.0)).normalized() + Vector((0.0, 0.0, a))
    cw = Vector((wx, wy, zm - 0.5 * thick))
    return make_ramp_box(cw, slope, width, length, thick)


def build_stairs(entries, rep):
    if not entries:
        return []
    # кластеры по касанию габаритов -> отдельные марши
    n = len(entries)
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for i in range(n):
        for j in range(i + 1, n):
            if overlap(entries[i][2], entries[j][2], 0.35):
                ri, rj = find(i), find(j)
                if ri != rj:
                    parent[ri] = rj
    groups = defaultdict(list)
    for i in range(n):
        groups[find(i)].append(entries[i])

    out = []
    for _k, grp in groups.items():
        pts = stair_entries_verts(grp)
        if not pts:
            continue
        box = ramp_from_points(pts, rep)
        if box is None:
            continue
        out.append(box)
        rep["flights"] += 1
    return out


# ---------------------------------------------------------------- сборка чанка
def build_chunk(chunk_path, name):
    walls, slabs, roofs, stairs, doors, arches, rep = collect(chunk_path)

    wall_boxes, floor_boxes, roof_boxes, stair_boxes = [], [], [], []
    doors_out = []
    per_house = {}

    houses = set(walls) | set(slabs) | set(roofs) | set(stairs)
    for h in sorted(houses):
        w = walls.get(h, [])
        s = slabs.get(h, [])
        r = roofs.get(h, [])
        st = stairs.get(h, [])
        nw = build_walls(h, w, doors, arches, wall_boxes, doors_out, rep) if w else 0
        fb = build_slabs(h, s, w, rep) if s else []
        floor_boxes.extend(fb)
        nf = len(fb)
        rb = build_roof(r, rep) if r else []
        if not rb and w:
            rb = synth_roof(w)
            rep["roof_synth"] += 1
        roof_boxes.extend(rb)
        nr = len(rb)
        ns = 0
        if st:
            sb = build_stairs(st, rep)
            stair_boxes.extend(sb)
            ns = len(sb)
        if nw or nf or nr or ns:
            per_house[h] = (nw, nf, nr, ns)
            rep["houses"] += 1
            if nw > 60:
                rep["house_heavy"] += 1

    rep["wall_boxes"] = len(wall_boxes)
    rep["floor_boxes"] = len(floor_boxes)
    rep["roof_boxes"] = len(roof_boxes)
    rep["stair_boxes"] = len(stair_boxes)
    uniq = {}
    for d in doors_out:
        uniq[(round(d["c"][0], 1), round(d["c"][1], 1), round(d["c"][2], 1))] = d
    doors_out = list(uniq.values())
    rep["openings"] = len(doors_out)
    rep["small_boxes"] = sum(1 for b in wall_boxes + floor_boxes + roof_boxes + stair_boxes
                             if max(b["s"]) <= 1.2)

    heavy = sorted(per_house.items(), key=lambda kv: -(kv[1][0] + kv[1][1]))[:6]
    samples = ["%s стен=%d плит=%d крыш=%d марш=%d" % (h, v[0], v[1], v[2], v[3]) for h, v in heavy]

    return {"chunk": name, "walls": wall_boxes, "floors": floor_boxes,
            "roofs": roof_boxes, "stairs": stair_boxes, "doors": doors_out,
            "report": dict(rep), "heavy_samples": samples}


def list_chunks():
    out = []
    for f in sorted(os.listdir(RAW)):
        u = f.upper()
        if not u.endswith(".BLEND"):
            continue
        if not (u.startswith("PRIMUM_HOUSES_") or u.startswith("PRIMUM_ADMIN_")):
            continue
        if "COPY" in u or "BAK" in u:
            continue
        out.append(f)
    return out


def main():
    flt = os.environ.get("BOXES_CHUNKS", "").strip()
    chunks = list_chunks()
    if flt:
        want = [s.strip().upper() for s in flt.split(",") if s.strip()]
        chunks = [c for c in chunks if any(w in c.upper() for w in want)]
    print("=== houses_boxes: чанков %d ===" % len(chunks), flush=True)
    t0 = time.time()
    # Фильтрованный прогон пишет ОТДЕЛЬНЫЙ файл: иначе один чанк затрёт общий houses_boxes.json.
    out_json = OUT_JSON
    if flt:
        tag = "_".join(s.strip().upper() for s in flt.split(",") if s.strip())
        tag = "".join(ch if (ch.isalnum() or ch in "-_") else "_" for ch in tag)
        out_json = os.path.join(os.path.dirname(OUT_JSON), "houses_boxes_%s.json" % tag)
    data = []
    tot = defaultdict(int)
    for f in chunks:
        p = os.path.join(RAW, f)
        if not os.path.exists(p):
            print("MISSING " + f, flush=True)
            continue
        try:
            d = build_chunk(p, f.replace(".blend", ""))
        except Exception as e:
            print("ERROR %s: %s" % (f, e), flush=True)
            import traceback
            traceback.print_exc()
            continue
        r = d["report"]
        for k, v in r.items():
            tot[k] += v
        print("  %-34s домов=%-4d стен=%-5d плит=%-4d крыш=%-4d марш=%-3d проёмов=%-4d мелких=%-4d"
              % (f.replace("PRIMUM_HOUSES_", "").replace(".blend", ""),
                 r.get("houses", 0), r.get("wall_boxes", 0), r.get("floor_boxes", 0),
                 r.get("roof_boxes", 0), r.get("stair_boxes", 0), r.get("openings", 0),
                 r.get("small_boxes", 0)), flush=True)
        for s in d.get("heavy_samples", [])[:4]:
            print("      тяжёлый: " + s, flush=True)
        data.append(d)
    with open(out_json, "w", encoding="utf-8") as fh:
        json.dump({"chunks": data}, fh, ensure_ascii=False, separators=(",", ":"))
    print("=== ИТОГО: домов=%d стен=%d плит=%d крыш=%d маршей=%d проёмов=%d (%.1f c) ==="
          % (tot.get("houses", 0), tot.get("wall_boxes", 0), tot.get("floor_boxes", 0),
             tot.get("roof_boxes", 0), tot.get("stair_boxes", 0), tot.get("openings", 0),
             time.time() - t0), flush=True)
    print("JSON: %s" % out_json, flush=True)


if __name__ == "__main__":
    main()
print("DONE")
