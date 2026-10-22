# dbg_classes.py — диагностика: что именно попадает в классы WALL / FLOOR / ROOF / STAIR
# чанка и по какому правилу. Нужно, чтобы увидеть утечку мебели и декора в физику.
#
# Запуск:  DBG_CHUNK=PRIMUM_HOUSES_....blend  blender -b --factory-startup --python dbg_classes.py
import bpy, os, sys
from collections import Counter, defaultdict

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX

CHUNK = os.environ.get("DBG_CHUNK", "PRIMUM_HOUSES_S1_GLD_P1_25H.blend")
bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, CHUNK))

by_rule = Counter()
tok_wall = Counter()
tok_floor = Counter()
examples = defaultdict(list)
houses = defaultdict(Counter)
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
    cls, rule = HX.klass_of(o, toks)
    if cls is None:
        continue
    by_rule[(cls, rule)] += 1
    d = o.dimensions
    houses[HX.house_of(toks)][cls] += 1
    if len(examples[(cls, rule)]) < 30:
        examples[(cls, rule)].append("%-52s d=(%5.2f %5.2f %5.2f)"
                                     % (o.name[:52], d.x, d.y, d.z))
    for t in set(HX.alpha(t) for t in toks):
        if cls == "WALL":
            tok_wall[t] += 1
        elif cls == "FLOOR":
            tok_floor[t] += 1

print("=== CHUNK %s  mesh-объектов=%d ===" % (CHUNK, total), flush=True)
print("--- классов по правилам ---", flush=True)
for (cls, rule), n in sorted(by_rule.items()):
    print("  %-6s %-12s %6d" % (cls, rule, n), flush=True)

print("--- топ токенов у WALL ---", flush=True)
for t, n in tok_wall.most_common(40):
    print("  %-14s %6d" % (t, n), flush=True)

print("--- топ токенов у FLOOR ---", flush=True)
for t, n in tok_floor.most_common(40):
    print("  %-14s %6d" % (t, n), flush=True)

print("--- домов=%d, боксов по домам (топ 20) ---" % len(houses), flush=True)
rank = sorted(houses.items(), key=lambda kv: -sum(kv[1].values()))
for h, c in rank[:20]:
    print("  %-22s %s" % (h, dict(c)), flush=True)

print("--- примеры по правилам ---", flush=True)
for (cls, rule), lst in sorted(examples.items()):
    print("  [%s/%s]" % (cls, rule), flush=True)
    for s in lst:
        print("     " + s, flush=True)

print("DONE", flush=True)
