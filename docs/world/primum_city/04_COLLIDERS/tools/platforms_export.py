# platforms_export.py — выгрузка 9 платформ низа города (BASE/20_PLATFORMS) в OBJ.
# Локальные координаты PRIMUM: local = (-s.x, s.z, -s.y), s = C + (p - C)*1.07.
# Индексы вершин — ГЛОБАЛЬНЫЕ по файлу (as required by OBJ).
#
# ВАЖНО (проёмы лестниц): плиты дек толстые (~1.3 м), а террасные марши входят в их толщу —
# верхние ступени оказываются ПОД плитой, игрок упирается головой (просвет 0.1–1.1 м) и не
# поднимается. Поэтому из плит вырезается «коридор подъёма»: объём высотой 2.2 м над линией
# настила марша, по всей его ширине. Реализация без булевых операций (на грубых плитах они
# врут): каждый коридор задаёт 6 плоскостей, плита рассекается по ним (bisect_plane), затем
# грани, чьи центры попали внутрь коридора, удаляются. Детерминированно, число коллайдеров
# не растёт (те же 9 мешей); OBJ — это физика, визуал не затрагивается.
#
# Оси Blender: x, y — план, z — вверх.
import bpy, bmesh, math, os
from mathutils import Vector

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW", "PRIMUM_BASE_S0_CITY_P1_00H.blend")
OUT = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\platforms.obj"

K = 1.07
C = (7.5, -148.0, 18.5)
HEADROOM = 2.2           # высота коридора над настилом (м)
UNDER = 0.15             # запас под настил (м)
STEP = 0.5               # шаг профиля вдоль марша (м)
SIDE_PAD = 0.25          # запас по ширине марша (м)
END_PAD = 0.30           # запас за торцы марша (м)

NAMES = ["DECK_0", "DECK_6", "DECK_12", "DECK_18",
         "PLATFORM_L00", "PLATFORM_L06", "PLATFORM_L12", "PLATFORM_L18", "PLATFORM_L24"]
SHORT = {"DECK_0": "DECK0", "DECK_6": "DECK6", "DECK_12": "DECK12", "DECK_18": "DECK18",
         "PLATFORM_L00": "PLATL00", "PLATFORM_L06": "PLATL06", "PLATFORM_L12": "PLATL12",
         "PLATFORM_L18": "PLATL18", "PLATFORM_L24": "PLATL24"}

RAIL = ("RAIL", "GUARD", "HAND", "SKIRT", "BALUST", "BAL_", "_BAL", "PARAPET", "_BR", "KERB", "KICK",
        "FURN", "RUG", "CARPET", "COVER")


def ulocal(p):
    s = [C[i] + (p[i] - C[i]) * K for i in range(3)]
    return (-s[0], s[2], -s[1])


def cpath(o):
    return "/".join([c.name for c in o.users_collection]).upper()


class Corridor:
    """Коридор подъёма над маршем. Оси Blender: x,y — план, z — вверх."""

    def __init__(self, x0, y0, z0, x1, y1, z1, w, h):
        self.p0 = Vector((x0, y0, z0))
        self.p1 = Vector((x1, y1, z1))
        self.w = w
        self.h = h
        dx, dy = self.p1.x - self.p0.x, self.p1.y - self.p0.y
        self.run = math.hypot(dx, dy)
        self.ux, self.uy = dx / self.run, dy / self.run
        self.px, self.py = -self.uy, self.ux

    def inside(self, x, y, z):
        dx, dy = x - self.p0.x, y - self.p0.y
        t = (dx * self.ux + dy * self.uy) / self.run
        if t < -1e-4 or t > 1.0 + 1e-4:
            return False
        perp = dx * self.px + dy * self.py
        if abs(perp) > self.w:
            return False
        zsurf = self.p0.z + (self.p1.z - self.p0.z) * t
        return (zsurf - UNDER) <= z <= (zsurf + self.h)

    def planes(self):
        """6 плоскостей (точка, нормаль)."""
        u3 = Vector((self.ux, self.uy, 0.0))
        p3 = Vector((self.px, self.py, 0.0))
        d3 = (self.p1 - self.p0).normalized()
        slope_n = d3.cross(p3)
        if slope_n.length < 1e-6:
            slope_n = Vector((0, 0, 1))
        slope_n.normalize()
        if slope_n.z < 0:
            slope_n = -slope_n
        return [
            (self.p0 - u3 * END_PAD, u3),                 # торец снизу
            (self.p1 + u3 * END_PAD, -u3),                # торец сверху
            (self.p0 + p3 * self.w, p3),                  # бок 1
            (self.p0 - p3 * self.w, -p3),                 # бок 2
            (self.p0 + Vector((0, 0, self.h + UNDER)), slope_n),   # верх (наклонный)
            (self.p0 - Vector((0, 0, UNDER)), -slope_n),           # низ (наклонный)
        ]


def corridors_for(o):
    me = o.data
    m = o.matrix_world
    rot = m.to_3x3()
    tris = []
    for poly in me.polygons:
        n = (rot @ poly.normal).normalized()
        if n.z < 0.6:                     # «пол» ступеней / настил (z — вверх)
            continue
        c = m @ poly.center
        tris.append(Vector((c.x, c.y, c.z)))
    if len(tris) < 3:
        return []
    low = min(tris, key=lambda t: t.z)
    high = max(tris, key=lambda t: t.z)
    dx, dy = high.x - low.x, high.y - low.y
    run = math.hypot(dx, dy)
    if run < 1.5 or (high.z - low.z) < 0.4:
        return []
    ux, uy = dx / run, dy / run
    px, py = -uy, ux
    vw = [m @ v.co for v in me.vertices]
    proj = [p.x * px + p.y * py for p in vw]
    half = (max(proj) - min(proj)) / 2.0 + SIDE_PAD
    mid = (max(proj) + min(proj)) / 2.0
    n = max(4, int(run / STEP))
    pts = []
    for i in range(n + 1):
        t = i / float(n)
        x = low.x + dx * t
        y = low.y + dy * t
        best = min(tris, key=lambda c: (c.x - x) ** 2 + (c.y - y) ** 2)
        pts.append(Vector((x + px * mid, y + py * mid, best.z)))
    return [Corridor(pts[i].x, pts[i].y, pts[i].z, pts[i + 1].x, pts[i + 1].y, pts[i + 1].z, half, HEADROOM)
            for i in range(len(pts) - 1)]


bpy.ops.wm.open_mainfile(filepath=RAW)

stair_objs = []
for o in bpy.data.objects:
    if o.type != 'MESH' or o.library is not None:
        continue
    p, n = cpath(o), o.name.upper()
    if not ("STAIR" in p or "STAIR" in n or "STEPS" in p):
        continue
    if any(t in n for t in RAIL):
        continue
    stair_objs.append(o)

corridors = []
for o in stair_objs:
    corridors.extend(corridors_for(o))
print("коридоров подъёма: %d (лестниц %d)" % (len(corridors), len(stair_objs)))

for name in NAMES:
    o = bpy.data.objects.get(name)
    if o is None:
        continue
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.transform(o.matrix_world)
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
    bb0 = Vector((min(xs), min(ys), min(zs)))
    bb1 = Vector((max(xs), max(ys), max(zs)))
    used = 0
    for cor in corridors:
        c0 = Vector((min(cor.p0.x, cor.p1.x) - cor.w, min(cor.p0.y, cor.p1.y) - cor.w,
                     min(cor.p0.z, cor.p1.z) - UNDER))
        c1 = Vector((max(cor.p0.x, cor.p1.x) + cor.w, max(cor.p0.y, cor.p1.y) + cor.w,
                     max(cor.p0.z, cor.p1.z) + cor.h))
        if (c1.x < bb0.x or c0.x > bb1.x or c1.y < bb0.y or c0.y > bb1.y
                or c1.z < bb0.z or c0.z > bb1.z):
            continue
        used += 1
        for (co, no) in cor.planes():
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no,
                                   clear_inner=False, clear_outer=False)
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        for cor in corridors:
            if cor.inside(c.x, c.y, c.z):
                kill.append(f)
                break
    if kill:
        bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.transform(o.matrix_world.inverted())
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    print("обработано %-11s: коридоров задело %2d, удалено граней %4d, осталось %4d"
          % (name, used, len(kill), len(o.data.polygons)))

n_vert = 0
written = []
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write("# PRIMUM platforms (low level), Unity-local coords, stair corridors cut out\n")
    for name in NAMES:
        o = bpy.data.objects.get(name)
        if o is None:
            continue
        me = o.data
        short = SHORT[name]
        fh.write("o %s\n" % short)
        base = n_vert + 1
        for v in me.vertices:
            p = o.matrix_world @ v.co
            ux, uy, uz = ulocal((p.x, p.y, p.z))
            fh.write("v %.4f %.4f %.4f\n" % (ux, uy, uz))
            n_vert += 1
        tris = 0
        for poly in me.polygons:
            idx = [base + i for i in poly.vertices]
            for i in range(1, len(idx) - 1):
                fh.write("f %d %d %d\n" % (idx[i + 1], idx[i], idx[0]))
                tris += 1
        written.append((short, len(me.vertices), tris))

print("=== platforms.obj ===")
for w in written:
    print("  %-8s verts=%-5d tris=%d" % w)
print("всего объектов: %d, вершин: %d" % (len(written), n_vert))
print("DONE")
