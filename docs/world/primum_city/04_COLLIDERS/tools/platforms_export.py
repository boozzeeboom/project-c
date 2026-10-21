# platforms_export.py — выгрузка 9 платформ низа города (BASE/20_PLATFORMS) в OBJ
# Локальные координаты PRIMUM: local = (-s.x, s.z, -s.y), s = C + (p - C)*1.07.
# Индексы вершин — ГЛОБАЛЬНЫЕ по файлу (as required by OBJ).
import bpy, os

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW", "PRIMUM_BASE_S0_CITY_P1_00H.blend")
OUT = r"C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\platforms.obj"

K = 1.07
C = (7.5, -148.0, 18.5)

NAMES = ["DECK_0", "DECK_6", "DECK_12", "DECK_18",
         "PLATFORM_L00", "PLATFORM_L06", "PLATFORM_L12", "PLATFORM_L18", "PLATFORM_L24"]
SHORT = {"DECK_0": "DECK0", "DECK_6": "DECK6", "DECK_12": "DECK12", "DECK_18": "DECK18",
         "PLATFORM_L00": "PLATL00", "PLATFORM_L06": "PLATL06", "PLATFORM_L12": "PLATL12",
         "PLATFORM_L18": "PLATL18", "PLATFORM_L24": "PLATL24"}

def ulocal(p):
    s = [C[i] + (p[i] - C[i]) * K for i in range(3)]
    return (-s[0], s[2], -s[1])

bpy.ops.wm.open_mainfile(filepath=RAW)

n_vert = 0
written = []
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write("# PRIMUM platforms (low level), Unity-local coords\n")
    for o in bpy.data.objects:
        if o.type != 'MESH' or o.name not in NAMES:
            continue
        me = o.data
        name = SHORT[o.name]
        fh.write("o %s\n" % name)
        base = n_vert + 1
        for v in me.vertices:
            p = o.matrix_world @ v.co
            ux, uy, uz = ulocal((p.x, p.y, p.z))
            fh.write("v %.4f %.4f %.4f\n" % (ux, uy, uz))
            n_vert += 1
        tris = 0
        for poly in me.polygons:
            idx = [base + i for i in poly.vertices]
            # веер + обратный порядок (Unity: фронт по часовой)
            for i in range(1, len(idx) - 1):
                fh.write("f %d %d %d\n" % (idx[i + 1], idx[i], idx[0]))
                tris += 1
        written.append((name, len(me.vertices), tris))

print("=== platforms.obj ===")
for w in written:
    print("  %-8s verts=%-5d tris=%d" % w)
print("всего объектов:", len(written), "вершин:", n_vert)
print("DONE")
