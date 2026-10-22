# dbg_admin.py — диагностика ADMIN-чанка: как раскладываются его объекты правилами
# houses_boxes.py и ЧТО остаётся за бортом. Только чтение, в сцену ничего не пишет.
#
# Зачем: у ADMIN другая грамматика имён, чем у домов (ADM_HALL, ADM_PIL, ADM_CORR, ADM_CUP,
# ADM_DOME, ADM_DRUM, ADM_LIB…). Боксовый прогон дал 230 стен / 14 плит / 39 крыш — плит мало
# для здания в 4 этажа + крылья + библиотека в два света, значит часть горизонтали не отбирается.
# Этот скрипт показывает, какие крупные объекты пропущены и какой они формы.
#
# Запуск:  DBG_CHUNK=PRIMUM_ADMIN_S0_ADM_P1_01H.blend  blender -b --factory-startup --python dbg_admin.py
import bpy, os, sys
from collections import Counter, defaultdict

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX
import houses_boxes as HB

out = []


def say(s):
    print(s, flush=True)
    out.append(s)


CHUNK = os.environ.get("DBG_CHUNK", "PRIMUM_ADMIN_S0_ADM_P1_01H.blend")
bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, CHUNK))

by_rule = Counter()
by_family = defaultdict(Counter)
skip_plate = []      # пропущено, но форма — плита
skip_wall = []       # пропущено, но форма — стена
skip_big = []        # пропущено, крупное и непонятное
total = 0

for o in bpy.data.objects:
    if o.type != 'MESH' or o.library is not None:
        continue
    if o.data is None or len(o.data.polygons) == 0:
        continue
    if HX.has_ancestor(o, HX.ANCESTORS):
        continue
    total += 1
    toks = HX.base_tokens(o.name)
    cls, rule = HB.strict_class(o, toks)
    d = o.dimensions
    dx, dy, dz = d.x, d.y, d.z
    fam = "_".join(HX.alpha(t) for t in toks[:3])
    by_rule[(cls or "-", rule)] += 1
    by_family[fam][cls or "skip"] += 1
    if cls is not None:
        continue
    if rule == "wall-blob":
        continue
    rec = "%-46s d=(%5.2f %5.2f %5.2f)" % (o.name[:46], dx, dy, dz)
    if dz <= 0.45 and min(dx, dy) >= 2.0:
        if len(skip_plate) < 60:
            skip_plate.append(rec)
    elif dz >= 2.0 and min(dx, dy) <= 0.60 and max(dx, dy) >= 2.0:
        if len(skip_wall) < 40:
            skip_wall.append(rec)
    elif max(dx, dy, dz) >= 4.0:
        if len(skip_big) < 60:
            skip_big.append(rec)

say("=== ADMIN %s  mesh-объектов=%d ===" % (CHUNK, total))
say("--- классов по правилам (houses_boxes.strict_class) ---")
for (cls, rule), n in sorted(by_rule.items()):
    say("  %-6s %-12s %6d" % (cls, rule, n))

say("--- семейства имён: что в каком классе ---")
rank = sorted(by_family.items(), key=lambda kv: -sum(kv[1].values()))
for fam, c in rank[:35]:
    say("  %-32s %s" % (fam[:32], dict(c)))

say("--- ПРОПУЩЕНО, но форма явно плита (dz<=0.45, стороны >=2.0): %d ---" % len(skip_plate))
for s in skip_plate:
    say("     " + s)
say("--- ПРОПУЩЕНО, но форма явно стена (dz>=2.0, тонкая, длинная): %d ---" % len(skip_wall))
for s in skip_wall:
    say("     " + s)
say("--- ПРОПУЩЕНО, крупное и непонятное (>=4 м по любой оси): %d ---" % len(skip_big))
for s in skip_big:
    say("     " + s)

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "houses_admin_dbg.txt")
with open(os.path.abspath(path), "w", encoding="utf-8") as fh:
    fh.write("\n".join(out) + "\n")
print("DONE", flush=True)
