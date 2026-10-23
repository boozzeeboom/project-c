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
from mathutils.bvhtree import BVHTree

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
PLATE_SHARE = 0.20      # плита должна занимать >= 20% площади дома (только для старого растра)
PLATE_THICK_MAX = 0.90  # м, плита толще — это не перекрытие, а объём
PLATE_MIN_AREA = 4.0    # м², мельче — мебель/полка, а не перекрытие
PLATE_CLUSTER_GAP = 1.20  # м, плиты одного уровня в пределах разрыва = ОДИН бокс
PLATE_FAR = 60.0        # м, плита дальше этого от медианы чанка — мусор модели
STEP_RISE_MAX = 0.30    # м, перепад, выше которого нужна промежуточная ступень
STEP_RISE_MIN = 0.20    # м, ниже — ступень не нужна (это шов/погрешность)
ROOF_T_MIN = 0.25       # м, минимальная толщина бокса крыши

# ---------------------------------------------------------------- словари видов
ARCH_KIND = {"ARCH", "ARCADE", "PASSAGE", "PASS", "PORTAL"}
STAIR_KIND = {"STAIR", "STAIRS", "STAIRCASE", "STEPS", "STAIRTREAD", "FLIGHT", "STFLIGHT", "PORCHST"}
SLAB_KIND = {"SLAB", "FLOOR", "FLOORS", "ATTIC", "PLATE", "MEZZ", "MEZZANINE", "GALLERY",
             "VER", "VERANDA", "PORCH", "PLATFORM", "BASEPLATE", "PLINTH", "PLINTHS"}
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
    # WC здесь НЕ вето: в ADMIN это имя ПОМЕЩЕНИЯ (ADM_WC_F1_WALL_DIV_X — настоящая стена),
    # а сантехника и так отсеивается формой на геометрическом запасе.
    "TOILET", "TUB", "BATH", "SHOWER", "LIFE", "CLT", "CLUTTER", "CUSHION", "MATTRESS",
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

DOOR_VETO = {"FURN", "CAB", "SHUT", "SHELF", "WARDROBE", "WIN", "WINH", "DF", "DFI", "BAL", "RAIL",
             "FRAME", "SILL", "TRANSOM"}


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
# Порог «скорлупы помещения». Раньше стояло жёстко 0.60 — ровно по толщине типовой стены
# ADMIN, и стена толщиной 0.60 м отбрасывалась из-за погрешности float при сравнении «> 0.60».
# Замер 22.10: из 44 отброшенных `wall-blob` 26 — настоящие стены (наружные N/W всех четырёх
# этажей, внутренние стены башен, стены крыльев), остальные 18 — реальные блоки и кольца
# толщиной от 1.0 м (ADM_OFF_*_WALL_SEP_*, ADM_ROT_WALL, ADM_BOOTH_WALL_*), их отбрасывать верно.
WALL_BLOB_T = 0.75      # м, толще — это уже блок/скорлупа, а не стена
WALL_BLOB_L = 1.20      # м, и длиннее этого


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
        # Перила и ограждения марша лежат в STAIR_KIND по имени (ADM_STAIR_M1_PARAPET_S)
        # и при этом торчат на 2.85 м выше ступеней: в МНК они убивают уклон,
        # а в физике это не проход. Отсеиваем их до лестничной геометрии.
        if "PARAPET" in a:
            return None, "stair-parapet"
        return "STAIR", "name"
    if any(t in SLAB_KIND for t in a) or any(t.startswith("SLAB") for t in toks):
        return "SLAB", "name"
    if any(t in ROOF_KIND for t in a):
        return "ROOF", "name"
    if any(t in WALL_KIND for t in a):
        # Не пластина — значит это «скорлупа» комнаты (один объект = три стены вокруг
        # помещения). Бокс по её габариту заливает комнату целиком — такие не берём.
        dx, dy, dz = dims(aabb(obox(o)))
        if min(dx, dy) > WALL_BLOB_T and max(dx, dy) > WALL_BLOB_L:
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

        if any(t in ("DOOR", "DOORS", "LEAF") for t in al):
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
        # Парадный портал — проём на ПОЛНУЮ высоту объекта (4.00 м), а не на CLEAR_H:
        # ADM_DOOR_PORTAL_GLASS 11.80 x 3.20 при пороге 24.55 давал окно только от верха
        # полотна вниз на 2.10 и оставлял 12-метровую полосу стены внизу (замер 22.10:
        # единственный бокс 0.60 x 0.75 x 12.00 в объёме проёма).
        if "PORTAL" in _nm.upper():
            z0, z1 = bb[4], bb[5]
        elif is_arch:
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
            if (p[0] == w[0] and "PORTAL" not in p[7].upper() and "PORTAL" not in w[7].upper()
                    and w[3] <= p[4] + 1e-6 and w[4] >= p[3] - 1e-6
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
    """Крыши объектом нет — верх дома всё равно закрываем ОДНИМ боксом по габариту стен.

    Замер 24.10: синтетическая крыша брала ВСЕ полосы высот облака стен, и на 4-этажный
    ADMIN это дало по одному боксу 68x64 м на КАЖДОМ этаже (24.73 / 29.01 / 33.29 /
    37.57 / 41.85). Бокс 29.01 попадает в проём шахты настоящей плиты F2 и обрывает
    марш библиотеки за 0.4 м до площадки. Синтетическая крыша — это только КРЫША,
    поэтому берётся одна, самая верхняя полоса."""
    puts = []
    for o, pts, bb in walls:
        puts.extend(pts)
    if not puts:
        return []
    return _roof_box(puts, top_only=True)


def _roof_box(puts, top_only=False):
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
    if top_only and merged:
        merged = [merged[-1]]
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


# ---------------------------------------------------------------- вырезание проёмов плиты
# Замер 22.10 по ADMIN: плиты — НЕ сплошные пластины, в них есть настоящие проёмы.
#   ADM_F2_FLOOR   — 26.8% пусто (витраж/атриум)
#   ADM_ROOF_SLAB  — 23.0% пусто (кольцо барабна, углы террасы)
#   ADM_LIB_GALLERY— 70.1% пусто (обход вокруг витража библиотеки в два света)
#   ADM_F3/F4_FLOOR—  1.4% пусто (шахта лестницы, ровно там, где стоит марш)
# Бокс по габариту заваривает все эти проёмы, и игрок упирается в перекрытие
# на лестнице и теряет библиотеку. Поэтому одиночная плита с дырами режется по растру.
PLATE_STEP = 0.40        # м, шаг растра по плите
PLATE_HOLE_SHARE = 0.002 # доля пустых клеток, выше которой плита режется
PLATE_ERODE = 0          # клеток эрозии: ошибка только в сторону меньшей плиты


def plate_boxes(o, bb, rep):
    """УСТАРЕЛО (25.10): растр плиты по дыркам. Оставлено только как справка.

    Растр резал перекрытие на десятки боксов с щелями по краям проёмов — заказчик
    назвал это «пол с дырками». Сейчас плита — крупный бокс (build_slabs), а дыры
    вырезает только carve_plates_by_stairs по пятну марша. Функция не вызывается.
    """
    x0, x1, y0, y1 = bb[0], bb[1], bb[2], bb[3]
    span_x, span_y = x1 - x0, y1 - y0
    if span_x < MIN_BOX or span_y < MIN_BOX:
        return None

    nx = max(1, int(math.ceil(span_x / PLATE_STEP)))
    ny = max(1, int(math.ceil(span_y / PLATE_STEP)))
    cx = span_x / nx
    cy = span_y / ny

    inv = o.matrix_world.inverted()
    dloc = (inv.to_3x3() @ Vector((0.0, 0.0, -1.0))).normalized()
    ztop = bb[5] + 0.50

    mask = [[False] * nx for _ in range(ny)]
    for j in range(ny):
        wy = y0 + (j + 0.5) * cy
        for i in range(nx):
            wx = x0 + (i + 0.5) * cx
            mask[j][i] = o.ray_cast(inv @ Vector((wx, wy, ztop)), dloc, distance=1.5)[0]

    empty = sum(1 for row in mask for v in row if not v)
    if empty < PLATE_HOLE_SHARE * nx * ny:
        return None

    if PLATE_ERODE <= 0:
        er = [row[:] for row in mask]
    else:
        er = [[False] * nx for _ in range(ny)]
        for j in range(PLATE_ERODE, ny - PLATE_ERODE):
            for i in range(PLATE_ERODE, nx - PLATE_ERODE):
                if (mask[j][i] and mask[j][i - 1] and mask[j][i + 1]
                        and mask[j - 1][i] and mask[j + 1][i]):
                    er[j][i] = True

    rects = greedy_rects(er, nx, ny)
    if not rects:
        rep["slab_carved_empty"] += 1
        return []

    z_lo, z_hi = bb[4], bb[5]
    h = max(z_hi - z_lo, MIN_BOX)
    out = []
    for (i0, i1, j0, j1) in rects:
        ax0, ax1 = x0 + i0 * cx, x0 + i1 * cx
        ay0, ay1 = y0 + j0 * cy, y0 + j1 * cy
        if ax1 - ax0 < MIN_BOX or ay1 - ay0 < MIN_BOX:
            continue
        # th = 0: плиты ADMIN осевые (ry = 0 у всех 14 боксов)
        out.append(make_box((0.5 * (ax0 + ax1), 0.5 * (ay0 + ay1), 0.5 * (z_lo + z_hi)),
                            ax1 - ax0, ay1 - ay0, h, 0.0))
    rep["slab_carved"] += 1
    rep["slab_carved_boxes"] += len(out)
    return out


def greedy_rects(mask, nx, ny):
    """Покрыть занятые клетки максимальными прямоугольниками (строчный жадный проход)."""
    used = [[False] * nx for _ in range(ny)]
    out = []
    for j in range(ny):
        i = 0
        while i < nx:
            if not mask[j][i] or used[j][i]:
                i += 1
                continue
            i2 = i
            while i2 + 1 < nx and mask[j][i2 + 1] and not used[j][i2 + 1]:
                i2 += 1
            j2 = j
            while j2 + 1 < ny and all(mask[j2 + 1][k] and not used[j2 + 1][k] for k in range(i, i2 + 1)):
                j2 += 1
            for jj in range(j, j2 + 1):
                for k in range(i, i2 + 1):
                    used[jj][k] = True
            out.append((i, i2 + 1, j, j2 + 1))
            i = i2 + 1
    return out


def build_slabs(name, entries, walls, rep):
    """Перекрытие = КРУПНЫЙ бокс на уровень и КОРПУС, а не растр по дыркам.

    Замер 25.10 (жалоба заказчика): пол 2/3/4 этажа и кровля были нарезаны 10..80 боксами
    с настоящими дырками (F3 6, F4 4, кровля 80 боксов при 71% заполнения) — игрок
    проваливался в щели галерей и балконов. Мелкие плиты при этом отсеивались
    ОТНОСИТЕЛЬНОЙ площадью (>20% самой крупной) — вместе с ними пропадала плита
    входного крыльца, и в проёме 76.5..79.0 оставалась дыра до деки -0.45 м.

    Новая схема (по замеру, а не по догадке):
      1. плита — всё тоньше PLATE_THICK_MAX, площадь >= PLATE_MIN_AREA (абсолютный порог);
      2. плиты одного уровня (LEVEL_Z) кластеризуются в плане по смыканию
         с разрывом PLATE_CLUSTER_GAP -> ОДИН бокс на кластер (корпус / крыло);
      3. дырки не режутся растром вообще: их вырезает только carve_plates_by_stairs
         по пятну маршей, то есть дыра есть только там, где правда идёт лестница.
    """
    if not entries:
        return []
    wpts = []
    for o, pts, bb in walls:
        wpts.extend(pts)
    ref = wpts if wpts else [p for o, pts, bb in entries for p in pts]
    th = principal_theta(ref)

    good = []
    for o, pts, bb in entries:
        if (bb[5] - bb[4]) > PLATE_THICK_MAX:   # не плита
            rep["slab_thick"] += 1
            continue
        a0, a1, b0, b1 = project(pts, th)
        area = (a1 - a0) * (b1 - b0)
        if area < PLATE_MIN_AREA:               # полка/мебель
            rep["slab_small"] += 1
            continue
        good.append((o, pts, bb, (a0, a1, b0, b1)))
    if not good:
        return []
    rep["slab_keep"] += len(good)

    # мусор модели: плиты, брошенные далеко от самого чанка (в ADMIN — 4 шт. на y=-78..-80)
    cs = sorted(g[3][0] + 0.5 * (g[3][1] - g[3][0]) for g in good)
    cz = sorted(g[3][2] + 0.5 * (g[3][3] - g[3][2]) for g in good)
    mu, mv = cs[len(cs) // 2], cz[len(cz) // 2]
    keep = []
    for g in good:
        cu = g[3][0] + 0.5 * (g[3][1] - g[3][0])
        cv = g[3][2] + 0.5 * (g[3][3] - g[3][2])
        if math.hypot(cu - mu, cv - mv) > PLATE_FAR:
            rep["slab_far"] += 1
            continue
        keep.append(g)
    good = keep
    if not good:
        return []

    levels = defaultdict(list)
    for g in good:
        levels[round(g[2][5], 1)].append(g)
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
        rects = [g[3] for g in grp]
        n = len(grp)
        par = list(range(n))

        def find(i):
            while par[i] != i:
                par[i] = par[par[i]]
                i = par[i]
            return i

        for i in range(n):
            for j in range(i + 1, n):
                if plan_ov(rects[i], rects[j], PLATE_CLUSTER_GAP):
                    ri, rj = find(i), find(j)
                    if ri != rj:
                        par[ri] = rj
        clusters = defaultdict(list)
        for i in range(n):
            clusters[find(i)].append(i)

        for _root, idx in sorted(clusters.items()):
            u0 = min(rects[i][0] for i in idx)
            u1 = max(rects[i][1] for i in idx)
            v0 = min(rects[i][2] for i in idx)
            v1 = max(rects[i][3] for i in idx)
            if u1 - u0 < MIN_BOX or v1 - v0 < MIN_BOX:
                continue
            z0 = min(grp[i][2][4] for i in idx)
            z1 = max(grp[i][2][5] for i in idx)
            um, vm = 0.5 * (u0 + u1), 0.5 * (v0 + v1)
            wx, wy = word((ca, sa), um, vm)
            out.append(make_box((wx, wy, 0.5 * (z0 + z1)), u1 - u0, v1 - v0,
                                max(z1 - z0, MIN_BOX), th))
            rep["slab_cluster"] += 1
    return out


# ---------------------------------------------------------------- марши
def stair_entries_verts(entries):
    pts = []
    for o, _p, _b in entries:
        mw = o.matrix_world
        for v in o.data.vertices:
            pts.append(mw @ v.co)
    return pts


STAIR_PROBE_STEP = 0.25     # м, шаг сетки замеров верха марша
STAIR_PLANE_MIN = 0.10      # tan уклона верха; ниже — плоская площадка/подиум/ступень
STAIR_PLANE_ERR = 0.25      # м, СКО невязки плоскости; больше — объект НЕ один марш


def stair_top_cells(o, step=None):
    """Клетки ВЕРХА объекта марша: луч сверху вниз по сетке над его габаритом.

    Замер 25.10 (ADMIN, 28 объектов): марши — настоящие ступени (104 вершины), верх
    ложится в плоскость z = A*x + B*y + C со СКО 0.036..0.047 м и уклоном 27..33°.
    Главный марш 3.60x4.00 идёт вдоль 3.60, то есть ДЛИННАЯ сторона габарита
    поперечная ходу — поэтому любое направление «по длинной стороне» поворачивает
    диагональ на 90° (это и было видно как «перепутаны оси»). Здесь направление
    измеряется по самим ступеням.
    """
    me = o.data
    mw = o.matrix_world
    vs = [mw @ v.co for v in me.vertices]
    ps = [list(p.vertices) for p in me.polygons]
    if not vs or not ps:
        return []
    bvh = BVHTree.FromPolygons(vs, ps, all_triangles=False)
    x0 = min(v.x for v in vs)
    x1 = max(v.x for v in vs)
    y0 = min(v.y for v in vs)
    y1 = max(v.y for v in vs)
    ztop = max(v.z for v in vs)
    if step is None:
        step = max(STAIR_PROBE_STEP, max(x1 - x0, y1 - y0) / 64.0)
    nx = max(1, int((x1 - x0) / step))
    ny = max(1, int((y1 - y0) / step))
    cells = []
    for i in range(nx):
        x = x0 + (i + 0.5) * (x1 - x0) / nx
        for j in range(ny):
            y = y0 + (j + 0.5) * (y1 - y0) / ny
            hit = bvh.ray_cast(Vector((x, y, ztop + 1.0)), Vector((0.0, 0.0, -1.0)))
            if hit[0] is not None:
                cells.append((x, y, hit[0].z))
    return cells


def stair_plane(cells):
    """МНК-плоскость верха z = A*x + B*y + C: (A, B, se) или None.

    (A, B) — градиент верха: направление подъёма atan2(B, A), уклон hypot(A, B).
    se — СКО невязки, признак «это один марш, а не поворот/два марша в одном объекте».
    """
    n = len(cells)
    if n < 6:
        return None
    sx = sum(c[0] for c in cells)
    sy = sum(c[1] for c in cells)
    sz = sum(c[2] for c in cells)
    mx, my, mz = sx / n, sy / n, sz / n
    sxx = sxy = syy = sxz = syz = 0.0
    for x, y, z in cells:
        dx, dy, dz = x - mx, y - my, z - mz
        sxx += dx * dx
        sxy += dx * dy
        syy += dy * dy
        sxz += dx * dz
        syz += dy * dz
    det = sxx * syy - sxy * sxy
    if abs(det) < 1e-9:
        return None
    A = (sxz * syy - syz * sxy) / det
    B = (syz * sxx - sxz * sxy) / det
    C = mz - A * mx - B * my
    se = math.sqrt(sum((z - (A * x + B * y + C)) ** 2 for x, y, z in cells) / n)
    return A, B, se


ENV_BIN = 0.25          # м, ширина полосы верхней огибающей
STAIR_FLIGHT_SLOPE = 0.15   # tan уклона; ниже — это площадка/подиум, а не марш
STAIR_Z_JOIN = 0.50     # м, марши склеиваются только при перехлёсте ПО ВЫСОТЕ


def top_line(us, zs):
    """МНК по ВЕРХНЕЙ огибающей облака: в каждой полосе u берётся максимальный z.

    Марш смоделирован СПЛОШНЫМ блоком: в облаке есть и нижняя грань, и подступенки.
    МНК по всем точкам даёт вдвое меньший уклон (замер 22.10: 12.6° вместо 26.6°,
    подъём 1.86 м вместо 4.00 м) — бокс обрывался посередине этажа. Верхняя
    огибающая — это линии проступей, то есть настоящий уклон марша.
    """
    bins = {}
    for u, z in zip(us, zs):
        k = int(math.floor(u / ENV_BIN))
        if k not in bins or z > bins[k]:
            bins[k] = z
    ks = sorted(bins)
    if len(ks) < 3:
        return 0.0, 0.5 * (max(zs) + min(zs))
    pu = [(k + 0.5) * ENV_BIN for k in ks]
    pz = [bins[k] for k in ks]
    n = len(pu)
    Su = sum(pu)
    Sz = sum(pz)
    Suu = sum(u * u for u in pu)
    Suz = sum(u * z for u, z in zip(pu, pz))
    den = n * Suu - Su * Su
    a = 0.0 if abs(den) < 1e-6 else (n * Suz - Su * Sz) / den
    return a, (Sz - a * Su) / n


def run_axis(pts):
    """(a, b, th) — ось ХОДА марша и МНК по верхней огибающей вдоль неё.

    Ось берётся НЕ по главной оси облака: сплошной марш в плане шире своей длины
    (ADM_STAIR_M1_A — 3.60 x 4.00), главная ось ложится ПОПЕРЁК хода, огибающая
    вдоль неё плоская — и марш превращается в сплошной бокс на всю высоту (28 боксов,
    ни одного склона). Поэтому ось = более длинная горизонтальная сторона, а при
    квадратном плане — та, где уклон больше.
    """
    flat = [Vector((p.x, p.y, 0.0)) for p in pts]
    th = principal_theta(flat)
    zs = [p.z for p in pts]
    span = []
    for t in (th, th + math.pi / 2):
        ca, sa = math.cos(t), math.sin(t)
        us = [uv(p, ca, sa)[0] for p in pts]
        span.append((max(us) - min(us), t, us))
    span.sort(key=lambda s: -s[0])
    if abs(span[0][0] - span[1][0]) > 0.30:
        _len, t, us = span[0]
    else:
        best = None
        for s in span:
            a0, _b0 = top_line(s[2], zs)
            if best is None or abs(a0) > abs(best[0]):
                best = (abs(a0), s)
        _len, t, us = best[1]
    a, b = top_line(us, zs)
    return a, b, t


def stair_kind(pts):
    """(уклон a, ось хода th) одного лестничного объекта."""
    if len(pts) < 12:
        return None, None
    a, _b, th = run_axis(pts)
    return a, th


def ramp_from_points(pts, rep):
    """Один наклонный бокс по облаку вершин марша: МНК-прямая z = a*u + b."""
    if len(pts) < 12:
        return None
    flat = [Vector((p.x, p.y, 0.0)) for p in pts]
    a, b, th = run_axis(pts)
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


def plan_ov(a, b, pad=0.0):
    """Пересечение в ПЛАНЕ (x, y): pad > 0 расширяет, pad < 0 требует настоящего перехлёста."""
    return not (a[1] <= b[0] - pad or a[0] >= b[1] + pad or
                a[3] <= b[2] - pad or a[2] >= b[3] + pad)


def ramp_between(ca, sa, vc, u_low, z_low, u_high, z_high, width, thick):
    """Наклонный бокс от (u_low, z_low) до (u_high, z_high) шириной width."""
    run = u_high - u_low
    rise = z_high - z_low
    if abs(run) < 0.20 or abs(rise) < 0.05:
        return None
    um = 0.5 * (u_low + u_high)
    zm = 0.5 * (z_low + z_high)
    length = math.hypot(run, rise) + 0.30
    slope = Vector((ca, sa, 0.0)) * run + Vector((0.0, 0.0, rise))
    wx, wy = word((ca, sa), um, vc)
    return make_ramp_box(Vector((wx, wy, zm - 0.5 * thick)), slope, width, length, thick)


def build_stairs(entries, rep):
    """Лет — НАКЛОННЫЙ бокс от своего низа до своего верха; площадка/подиум — сплошной бокс.

    Замер 25.10 по ADMIN (28 объектов): марш описывается ИЗМЕРЕННОЙ плоскостью верха
    (уклон 27..33°, СКО 0.036..0.047 м), а не габаритом и не соседней полосой —
    главный марш 3.60x4.00 идёт вдоль 3.60, и «длинная сторона» поворачивала
    диагональ на 90° (игрок шёл по ней поперёк хода). Высота берётся из габарита:
    bb[4] и bb[5] совпадают с плитой и площадкой (25.29 -> 27.43 -> ... -> 42.71).
    """
    if not entries:
        return []
    out = []

    info = []
    for e in entries:
        o, pts, bb = e
        vpts = stair_entries_verts([e])
        src = vpts if vpts else pts
        a, th = stair_kind(vpts)                 # легаси-оценка по огибающей вершин
        plane = stair_plane(stair_top_cells(o))  # ЗАМЕР верха ступеней
        if plane is not None:
            A, B, se = plane
            g = math.hypot(A, B)
            if g >= STAIR_PLANE_MIN and se <= STAIR_PLANE_ERR:
                th = math.atan2(B, A)            # ось ХОДА; a > 0 в этих осях
                a = g
                rep["stair_measured"] += 1
            else:
                rep["stair_unmeasured"] += 1
        if th is None:
            th = principal_theta(src)
        u0, u1, v0, v1 = project(src, th)
        info.append({"bb": bb, "a": a, "th": th, "u0": u0, "u1": u1, "v0": v0, "v1": v1,
                     "nv": len(vpts)})

    def box_of(it):
        bb = it["bb"]
        cw = (0.5 * (bb[0] + bb[1]), 0.5 * (bb[2] + bb[3]), 0.5 * (bb[4] + bb[5]))
        return make_box(cw, it["u1"] - it["u0"], it["v1"] - it["v0"],
                        max(bb[5] - bb[4], MIN_BOX), it["th"])

    flights = []
    for it in info:
        bb = it["bb"]
        dz = bb[5] - bb[4]
        plan_min = min(bb[1] - bb[0], bb[3] - bb[2])
        if dz < 0.60 or plan_min <= 1.60 or it["nv"] < 12:
            # ступень / узкая тетива / подиум-платформа / ПРОСТОЙ БОКС (у него нет
            # вершин, по которым можно понять направление хода — гадать нечем:
            # ADM_WING_N_STAIR_LANDING 3.4x4.6x2.0 иначе становится наклонным боксом
            # поперёк стены 25.01..29.01)
            out.append(box_of(it))
            rep["stair_flat"] += 1
            continue
        under = False
        for other in info:
            ob = other["bb"]
            if other is it or ob[5] <= bb[4] + 0.30:
                continue
            if ob[4] >= bb[5] - 0.30 and plan_ov(bb, ob, -0.10):
                under = True
                break
        if under:
            # масса ПОД летом: в физике это опора, а не второй марш
            out.append(box_of(it))
            rep["stair_flat"] += 1
            continue
        flights.append(it)

    for it in flights:
        bb = it["bb"]
        ca, sa = math.cos(it["th"]), math.sin(it["th"])
        low_u, high_u = it["u0"], it["u1"]
        if it["a"] is not None and abs(it["a"]) >= STAIR_FLIGHT_SLOPE:
            if it["a"] < 0:
                low_u, high_u = it["u1"], it["u0"]
        else:
            up = None
            dn = None
            for other in flights:
                ob = other["bb"]
                if other is it or not plan_ov(bb, ob, 0.45):
                    continue
                if abs(ob[4] - bb[5]) <= 0.30:
                    up = other
                if abs(ob[5] - bb[4]) <= 0.30:
                    dn = other
            ref = up if up is not None else dn
            if ref is not None:
                rc = 0.5 * (ref["u0"] + ref["u1"])
                uc = 0.5 * (it["u0"] + it["u1"])
                far = it["u1"] if rc > uc else it["u0"]
                near = it["u0"] if rc > uc else it["u1"]
                if up is not None:
                    high_u, low_u = far, near
                else:
                    high_u, low_u = near, far
        vc = 0.5 * (it["v0"] + it["v1"])
        rc = ramp_between(ca, sa, vc, low_u, bb[4], high_u, bb[5],
                          max(it["v1"] - it["v0"], MIN_BOX), 0.30)
        if rc is None:
            out.append(box_of(it))
            rep["stair_flat"] += 1
            continue
        out.append(rc)
        rep["flights"] += 1
    return out


def _quat_mat(q):
    x, y, z, w = q
    return [[1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y - z * w), 2.0 * (x * z + y * w)],
            [2.0 * (x * y + z * w), 1.0 - 2.0 * (x * x + z * z), 2.0 * (y * z - x * w)],
            [2.0 * (x * z - y * w), 2.0 * (y * z + x * w), 1.0 - 2.0 * (x * x + y * y)]]


def _rect_sub(b, a):
    """Прямоугольник b минус прямоугольник a (оба x0,x1,z0,z1) -> до 4 прямоугольников."""
    ix0, ix1 = max(b[0], a[0]), min(b[1], a[1])
    iz0, iz1 = max(b[2], a[2]), min(b[3], a[3])
    if ix1 <= ix0 or iz1 <= iz0:
        return [b]
    out = []
    if ix0 > b[0]:
        out.append((b[0], ix0, b[2], b[3]))
    if ix1 < b[1]:
        out.append((ix1, b[1], b[2], b[3]))
    if iz0 > b[2]:
        out.append((ix0, ix1, b[2], iz0))
    if iz1 < b[3]:
        out.append((ix0, ix1, iz1, b[3]))
    return out


STAIR_CARVE_PAD = 0.12   # м, запас вокруг пятна марша
STAIR_CARVE_DOWN = 0.60  # м, насколько верх марша может быть НИЖЕ низа плиты
STAIR_CARVE_UP = 1.80    # м, насколько верх марша может быть ВЫШЕ низа плиты


def carve_plates_by_stairs(plates, stair_boxes, rep, key):
    """Вырезать пятно марша из плиты/крыши, сквозь которую он выходит.

    Замер 24.10: крыло — марш 27.40..29.55 упирается в плиту F2 (низ 29.16), а проёма в
    плите нет: 1.3 м марша остаются под плитой и игрок встаёт за 0.4 м до площадки.
    Главная лестница — последний марш 40.25..42.70 проходит сквозь синтетическую
    крышу 41.73..41.98 на кровлю. Растр плиты вырезает только НАСТОЯЩИЕ дыры меша, а
    там, где дыры нет или она смещена, плиту надо резать по пятну марша.
    """
    if not plates or not stair_boxes:
        return plates
    ramps = []
    for b in stair_boxes:
        if not b.get("q"):
            continue
        m = _quat_mat(b["q"])
        s = b["s"]
        hx = 0.5 * (abs(m[0][0]) * s[0] + abs(m[0][1]) * s[1] + abs(m[0][2]) * s[2])
        hz = 0.5 * (abs(m[2][0]) * s[0] + abs(m[2][1]) * s[1] + abs(m[2][2]) * s[2])
        cx, cy, cz = b["c"]
        top = cy + 0.5 * s[2] * abs(m[1][2]) + 0.5 * s[1] * abs(m[1][1])
        ramps.append((cx - hx - STAIR_CARVE_PAD, cx + hx + STAIR_CARVE_PAD,
                      cz - hz - STAIR_CARVE_PAD, cz + hz + STAIR_CARVE_PAD, top))
    if not ramps:
        return plates

    out = []
    n_cut = 0
    for b in plates:
        if abs(b.get("ry", 0.0)) > 0.6:
            out.append(b)
            continue
        cx, cy, cz = b["c"]
        sx, sy, sz = b["s"]
        bot = cy - 0.5 * sy
        top = cy + 0.5 * sy
        rect = (cx - 0.5 * sx, cx + 0.5 * sx, cz - 0.5 * sz, cz + 0.5 * sz)
        cur = [rect]
        for (rx0, rx1, rz0, rz1, rtop) in ramps:
            if rtop < bot - STAIR_CARVE_DOWN or rtop > bot + STAIR_CARVE_UP:
                continue
            if rx1 <= rect[0] or rx0 >= rect[1] or rz1 <= rect[2] or rz0 >= rect[3]:
                continue
            nxt = []
            for r in cur:
                nxt.extend(_rect_sub(r, (rx0, rx1, rz0, rz1)))
            cur = nxt
            if not cur:
                break
        if len(cur) == 1 and cur[0] == rect:
            out.append(b)
            continue
        n_cut += 1
        for r in cur:
            if r[1] - r[0] < MIN_BOX or r[3] - r[2] < MIN_BOX:
                continue
            # ВАЖНО: rect и c/s плиты УЖЕ в локальных координатах чанка, а make_box ждёт
            # мировые координаты модели и сам зовёт to_local(). Заворачивать фрагмент
            # в make_box нельзя — получается ДВОЙНОЕ преобразование (K/C + смена осей),
            # и порезанная плита уезжает в мусорные координаты (замер 25.10: отрезки
            # F2/F3/F4/крыши получали верх -80.64 / -23.63 / 14.90 при верных c и s).
            out.append({"c": [round(0.5 * (r[0] + r[1]), 4), round(cy, 4),
                              round(0.5 * (r[2] + r[3]), 4)],
                        "s": [round(max(r[1] - r[0], MIN_BOX), 4),
                              round(max(sy, MIN_BOX), 4),
                              round(max(r[3] - r[2], MIN_BOX), 4)],
                        "ry": 0.0})
    rep[key] = n_cut
    return out


# ---------------------------------------------------------------- порог перед полом
STEP_DEPTH_MIN = 1.00   # м, глубина промежуточной ступени
STEP_DEPTH_MAX = 1.80   # м
STEP_SIDE_MIN = 0.80    # м, насколько нижняя плита должна выступать за верхнюю


def _aabb(b):
    """Габарит бокса в плане с учётом ry (боксы с q — наклонные марши, не сюда)."""
    if "q" in b:
        return None
    r = math.radians(b.get("ry", 0.0))
    ca, sa = abs(math.cos(r)), abs(math.sin(r))
    sx, sz = b["s"][0], b["s"][2]
    hx = 0.5 * (ca * sx + sa * sz)
    hz = 0.5 * (sa * sx + ca * sz)
    cx, cy, cz = b["c"]
    return (cx - hx, cx + hx, cz - hz, cz + hz, cy + 0.5 * b["s"][1], cy - 0.5 * b["s"][1])


def add_threshold_steps(floors, stair_boxes, rep, key="threshold_steps"):
    """Одна промежуточная ступень там, где к полу подходят с перепадом 0.20..0.60 м.

    Замер 25.10 (вход ADMIN, ось z=40021): дека 24.40 -> ступени 24.54 / 24.70 / 24.85 ->
    подиум 24.85 -> пол F1 25.28. Последние 0.43 м — вертикальная стенка выше stepOffset
    капсулы (0.30 м): без прыжка не пройти. Правило по замеру, а не по имени объекта:
    если нижняя плита выступает за край верхней не меньше STEP_SIDE_MIN (то есть к верхней
    подходят снаружи), в этой полосе ставится ОДНА ступень на половину перепада —
    подъём делится на два по <= 0.30 м. Глубина — доля перепада, но не мельче 1.0 м.
    """
    boxes = [_aabb(b) for b in floors]
    boxes = [b for b in boxes if b is not None]
    stair_rects = [b for b in (_aabb(s) for s in stair_boxes) if b is not None]
    out = []
    seen = set()
    for up in boxes:
        for lo in boxes:
            dt = up[4] - lo[4]
            # 0.20..0.30 — уже проходимо (stepOffset 0.30), 0.30..0.60 — делим на две
            # ступени ровно пополам; выше 0.60 одной промежуточной ступенью не лечится.
            if dt < STEP_RISE_MIN or dt > 2.0 * STEP_RISE_MAX:
                continue
            ox0, ox1 = max(up[0], lo[0]), min(up[1], lo[1])
            oz0, oz1 = max(up[2], lo[2]), min(up[3], lo[3])
            if ox1 - ox0 < 1.0 or oz1 - oz0 < 1.0:
                continue
            depth = min(STEP_DEPTH_MAX, max(STEP_DEPTH_MIN, 3.0 * dt))
            top = lo[4] + 0.5 * dt
            sides = []
            if lo[0] < up[0] - STEP_SIDE_MIN:
                sides.append((up[0] - depth, up[0], oz0, oz1))
            if lo[1] > up[1] + STEP_SIDE_MIN:
                sides.append((up[1], up[1] + depth, oz0, oz1))
            if lo[2] < up[2] - STEP_SIDE_MIN:
                sides.append((ox0, ox1, up[2] - depth, up[2]))
            if lo[3] > up[3] + STEP_SIDE_MIN:
                sides.append((ox0, ox1, up[3], up[3] + depth))
            for s in sides:
                if s[1] - s[0] < 1.0 or s[3] - s[2] < 1.0:
                    continue
                if any(plan_ov(s, r, -0.05) for r in stair_rects):   # там уже есть ступени/марши
                    continue
                k = (round(0.5 * (s[0] + s[1]), 1), round(0.5 * (s[2] + s[3]), 1), round(top, 2))
                if k in seen:
                    continue
                seen.add(k)
                h = max(0.25, 0.5 * dt)
                # s уже локальные (из _aabb по боксам пола) — make_box здесь дал бы
                # двойное преобразование. Собираем бокс напрямую.
                out.append({"c": [round(0.5 * (s[0] + s[1]), 4), round(top - 0.5 * h, 4),
                                  round(0.5 * (s[2] + s[3]), 4)],
                            "s": [round(max(s[1] - s[0], MIN_BOX), 4),
                                  round(max(h, MIN_BOX), 4),
                                  round(max(s[3] - s[2], MIN_BOX), 4)],
                            "ry": 0.0})
    rep[key] = len(out)
    return out


# ---------------------------------------------------------------- крыльцо: мост через яму
GAP_LEVEL_TOL = 0.10    # м, ступень и плита считаются на одном уровне
GAP_MAX = 3.00          # м, шире — это улица, а не крыльцо
GAP_MIN = 1.00          # м, уже — полосу не мостим
GAP_FILL_T = 0.60       # м, толщина заливки (низ уходит под деку)


def fill_level_gaps(stair_boxes, floors, rep, key="porch_fill"):
    """Замостить яму между ступенью и плитой, стоящими на ОДНОМ уровне.

    Замер 25.10 (вход ADMIN, ось z=40021.06): дека 24.40 -> ступени 24.54 / 24.70 / 24.85
    -> ЯМА 24.40 (x 76.5..79.0, 2.5 м) -> подиум 24.85 -> порог 25.07 -> F1 25.28.
    Верхняя ступень и подиум стоят на одном уровне 24.85, но между ними яма: игрок
    падает на 0.45 и опять лезет на 0.45 — выше stepOffset капсулы (0.30).
    Правило по замеру, а не по имени: если плоская ступень/площадка и плита на одном
    уровне, а в плане между ними разрыв не шире GAP_MAX, полоса разрыва мостится на
    уровне верха. Мостятся только ЯМЫ у ступеней — разрывы между двумя плитами
    (шахта лестницы, атриум) не трогаются: там вторая сторона — ступень.
    """
    steps = [b for b in (_aabb(s) for s in stair_boxes) if b is not None]
    plates = [b for b in (_aabb(f) for f in floors) if b is not None]
    allr = steps + plates
    out = []
    seen = set()
    for st in steps:
        for pl in plates:
            if abs(st[4] - pl[4]) > GAP_LEVEL_TOL:
                continue
            top = 0.5 * (st[4] + pl[4])
            bands = []
            # полоса = (x0, x1, z0, z1, ширина РАЗРЫВА, перехлёст по другой оси)
            if pl[0] - st[1] > 0.05:                      # разрыв по X, плита выше по X
                bands.append((st[1], pl[0], max(st[2], pl[2]), min(st[3], pl[3]),
                              pl[0] - st[1], min(st[3], pl[3]) - max(st[2], pl[2])))
            elif st[0] - pl[1] > 0.05:
                bands.append((pl[1], st[0], max(st[2], pl[2]), min(st[3], pl[3]),
                              st[0] - pl[1], min(st[3], pl[3]) - max(st[2], pl[2])))
            if pl[2] - st[3] > 0.05:                      # разрыв по Z
                bands.append((max(st[0], pl[0]), min(st[1], pl[1]), st[3], pl[2],
                              pl[2] - st[3], min(st[1], pl[1]) - max(st[0], pl[0])))
            elif st[2] - pl[3] > 0.05:
                bands.append((max(st[0], pl[0]), min(st[1], pl[1]), pl[3], st[2],
                              st[2] - pl[3], min(st[1], pl[1]) - max(st[0], pl[0])))
            for bd in bands:
                x0, x1, z0, z1 = bd[0], bd[1], bd[2], bd[3]
                # GAP_MAX — только про ШИРИНУ РАЗРЫВА; перехлёст по другой оси
                # (ширина крыльца вдоль фасада) — это не разрыв и может быть любой.
                if bd[4] > GAP_MAX or bd[5] < GAP_MIN:
                    continue
                r = (x0, x1, z0, z1)
                if any(plan_ov(r, o, -0.05) for o in allr):
                    continue
                k = (round(0.5 * (x0 + x1), 1), round(0.5 * (z0 + z1), 1), round(top, 2))
                if k in seen:
                    continue
                seen.add(k)
                # s локальные — make_box здесь дал бы двойное преобразование
                out.append({"c": [round(0.5 * (x0 + x1), 4), round(top - 0.5 * GAP_FILL_T, 4),
                                  round(0.5 * (z0 + z1), 4)],
                            "s": [round(x1 - x0, 4), round(GAP_FILL_T, 4), round(z1 - z0, 4)],
                            "ry": 0.0})
    rep[key] = len(out)
    return out


# ---------------------------------------------------------------- сборка чанка
def build_chunk(chunk_path, name):
    walls, slabs, roofs, stairs, doors, arches, rep = collect(chunk_path)

    wall_boxes, floor_boxes, roof_boxes, stair_boxes = [], [], [], []
    synth_caps = []
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
            synth_caps.extend(rb)
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

    # Синтетическая крыша строится для КАЖДОЙ группы стен отдельно. Группы ADMIN разбиты
    # по этажам (ADM_F1_WALL, ADM_CORR_F2, ADM_WC_F3 …), и верх группы — это НЕ крыша дома,
    # а потолок этажа: замер 24.10 дал по одному боксу 68x64 на каждом этаже (28.89 / 33.17 /
    # 37.45 / 41.73 локально), которые садились в ПРОЁМЫ настоящих плит (плита 29.16..29.56
    # при боксе 28.89..29.14) и обрывали марши: библиотека не доезжала 0.4 м, последний
    # марш на кровлю не выходил. Настоящая плита — уже крыша группы, и она вырезана по
    # проёмам, поэтому синтетическая крыша тут лишняя: если сверху есть плита, садящаяся
    # низом на её верх, бокс выбрасывается.
    def _plan_half(b):
        r = math.radians(b.get("ry", 0.0))
        ca, sa = abs(math.cos(r)), abs(math.sin(r))
        return (0.5 * (ca * b["s"][0] + sa * b["s"][2]),
                0.5 * (sa * b["s"][0] + ca * b["s"][2]))

    if synth_caps:
        keep = []
        for b in synth_caps:
            cx, cy, cz = b["c"]
            cap_top = cy + 0.5 * b["s"][1]
            covered = False
            for f in floor_boxes:
                fx, fz = _plan_half(f)
                if abs(cx - f["c"][0]) > fx or abs(cz - f["c"][2]) > fz:
                    continue
                f_bot = f["c"][1] - 0.5 * f["s"][1]
                if -0.05 <= f_bot - cap_top <= 0.60:
                    covered = True
                    break
            if covered:
                rep["roof_synth_covered"] += 1
            else:
                keep.append(b)
        drop = set(id(b) for b in synth_caps) - set(id(b) for b in keep)
        if drop:
            roof_boxes = [b for b in roof_boxes if id(b) not in drop]

    # Марши выходят сквозь плиты и кровлю: режем плиту по пятну марша там, где нет дыры.
    floor_boxes = carve_plates_by_stairs(floor_boxes, stair_boxes, rep, "floor_carved_by_stairs")
    roof_boxes = carve_plates_by_stairs(roof_boxes, stair_boxes, rep, "roof_carved_by_stairs")

    # Порог: перепад 0.20..0.60 м перед полом дробится ОДНОЙ промежуточной ступенью, иначе
    # стенка выше stepOffset капсулы и вход/переход этажа без прыжка не пройти (замер входа).
    floor_boxes.extend(add_threshold_steps(floor_boxes, stair_boxes, rep))

    # Крыльцо: яма между верхней ступенью и подиумом мостится вровень с полом.
    floor_boxes.extend(fill_level_gaps(stair_boxes, floor_boxes, rep))

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
