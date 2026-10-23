# dbg_admin_levels.py — замер УРОВНЕЙ ADMIN-чанка (шаг 1 плана 06_ADMIN_COLLIDERS.md).
#
# Зачем: у ADMIN нет объектов-перекрытий (плит), этаж смоделирован «скорлупой» комнат.
# Значит плиту надо СИНТЕЗИРОВАТЬ по этажу. Этот скрипт только читает RAW и отвечает
# на три вопроса, без которых синтез невозможен:
#
#   1. Какая Z у каждого этажа F1..F4?  Опора — центры оконных лент ADM_F<n>_WINDOW_*.
#   2. Каков габарит в плане у каждого этажа?  Опора — стены (класс WALL) этого этажа.
#   3. Что уже есть по горизонтали (SLAB-класс), маршам (STAIR) и крыльям (ADM_WING_*) —
#      чтобы синтез не наложился на готовое.
#
# Только чтение. В сцену ничего не пишет.
#
# Запуск:  DBG_CHUNK=PRIMUM_ADMIN_S0_ADM_P1_01H.blend  blender -b --factory-startup --python dbg_admin_levels.py
import bpy, os, re, sys
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

FLOOR_RE = re.compile(r"^F([0-9]+)$")

# token -> номер этажа ("F1" -> 1)
def floor_of(toks):
    for i, t in enumerate(toks):
        m = FLOOR_RE.match(t)
        if m:
            return int(m.group(1))
    return None


objs = []
for o in bpy.data.objects:
    if o.type != 'MESH' or o.library is not None:
        continue
    if o.data is None or len(o.data.polygons) == 0:
        continue
    if HX.has_ancestor(o, HX.ANCESTORS):
        continue
    toks = HX.base_tokens(o.name)
    bb = HB.aabb(HB.obox(o))
    dx, dy, dz = HB.dims(bb)
    cls, rule = HB.strict_class(o, toks)
    objs.append({"n": o.name, "t": toks, "bb": bb, "d": (dx, dy, dz),
                 "cls": cls, "rule": rule, "f": floor_of(toks)})

say("=== ADMIN %s  mesh-объектов=%d ===" % (CHUNK, len(objs)))

# ---------------------------------------------------------------- A. окна по этажам
say("")
say("--- A. ОКОННЫЕ ЛЕНТЫ (опора для Z этажа): center_z, z0..z1, план ---")
win = defaultdict(list)
for r in objs:
    if r["f"] is None:
        continue
    if not any(t in ("WINDOW", "WIN") for t in r["t"]):
        continue
    bb = r["bb"]
    win[r["f"]].append((r["n"], 0.5 * (bb[4] + bb[5]), bb[4], bb[5], r["d"]))

for f in sorted(win):
    rows = win[f]
    say("  F%d: %d лент" % (f, len(rows)))
    zs = [r[1] for r in rows]
    say("      center_z: min=%.3f max=%.3f  разброс=%.3f" % (min(zs), max(zs), max(zs) - min(zs)))
    z0s = [r[2] for r in rows]
    z1s = [r[3] for r in rows]
    say("      z0: min=%.3f max=%.3f | z1: min=%.3f max=%.3f" %
        (min(z0s), max(z0s), min(z1s), max(z1s)))
    say("      высоты лент: %s" % (sorted({round(r[3] - r[2], 2) for r in rows}),))
    for n, zc, a, b, d in sorted(rows):
        say("        %-28s z=%7.3f (%.2f..%.2f)  d=(%5.2f %5.2f %5.2f)" %
            (n[:28], zc, a, b, d[0], d[1], d[2]))

# ---------------------------------------------------------------- B. стеновые Z по этажам
say("")
say("--- B. КЛАСС WALL: Z-диапазоны по этажу (опора для габарита этажа) ---")
wall_by_f = defaultdict(list)
for r in objs:
    if r["cls"] != "WALL":
        continue
    wall_by_f[r["f"]].append(r)

for f in sorted(wall_by_f, key=lambda v: (v is not None, v)):
    rows = wall_by_f[f]
    tag = "F%d" % f if f is not None else "без этажа"
    z0s = [r["bb"][4] for r in rows]
    z1s = [r["bb"][5] for r in rows]
    x0 = min(r["bb"][0] for r in rows)
    x1 = max(r["bb"][1] for r in rows)
    y0 = min(r["bb"][2] for r in rows)
    y1 = max(r["bb"][3] for r in rows)
    say("  %-10s стен=%3d  z=%.2f..%.2f  план x=%.2f..%.2f (%.2f) y=%.2f..%.2f (%.2f)" %
        (tag, len(rows), min(z0s), max(z1s), x0, x1, x1 - x0, y0, y1, y1 - y0))
    hist = Counter((round(r["bb"][4], 1) for r in rows))
    say("      z0 гистограмма: %s" % dict(sorted(hist.items())))
    hist = Counter((round(r["bb"][5], 1) for r in rows))
    say("      z1 гистограмма: %s" % dict(sorted(hist.items())))
    for r in sorted(rows, key=lambda r: (r["bb"][4], r["n"])):
        bb = r["bb"]
        say("        %-44s z=%7.3f..%7.3f  d=(%5.2f %5.2f %5.2f)" %
            (r["n"][:44], bb[4], bb[5], r["d"][0], r["d"][1], r["d"][2]))

# ---------------------------------------------------------------- C. всё, что по горизонтали
say("")
say("--- C. КЛАСС SLAB (уже отобрано как перекрытие) ---")
for r in sorted([r for r in objs if r["cls"] == "SLAB"], key=lambda r: r["bb"][4]):
    bb = r["bb"]
    say("  %-46s z=%7.3f..%7.3f  d=(%6.2f %6.2f %5.2f)  правило=%s" %
        (r["n"][:46], bb[4], bb[5], r["d"][0], r["d"][1], r["d"][2], r["rule"]))

say("")
say("--- D. КЛАСС ROOF ---")
for r in sorted([r for r in objs if r["cls"] == "ROOF"], key=lambda r: r["bb"][4]):
    bb = r["bb"]
    say("  %-46s z=%7.3f..%7.3f  d=(%6.2f %6.2f %5.2f)" %
        (r["n"][:46], bb[4], bb[5], r["d"][0], r["d"][1], r["d"][2]))

say("")
say("--- E. КЛАСС STAIR ---")
for r in sorted([r for r in objs if r["cls"] == "STAIR"], key=lambda r: r["bb"][4]):
    bb = r["bb"]
    tag = "F%d" % r["f"] if r["f"] is not None else "  -"
    say("  %-4s %-40s z=%7.3f..%7.3f  d=(%5.2f %5.2f %5.2f)" %
        (tag, r["n"][:40], bb[4], bb[5], r["d"][0], r["d"][1], r["d"][2]))

# ---------------------------------------------------------------- F. крылья
say("")
say("--- F. КРЫЛЬЯ ADM_WING_* : всё, что не veto (класс + габарит) ---")
for r in sorted([r for r in objs if r["n"].startswith("ADM_WING_") and r["cls"] is not None],
                key=lambda r: (r["n"], r["bb"][4])):
    bb = r["bb"]
    tag = "F%d" % r["f"] if r["f"] is not None else "  -"
    say("  %-4s %-6s %-40s z=%7.3f..%7.3f  d=(%5.2f %5.2f %5.2f)" %
        (tag, r["cls"], r["n"][:40], bb[4], bb[5], r["d"][0], r["d"][1], r["d"][2]))
say("  (veto-объекты крыльев: %d)" %
    sum(1 for r in objs if r["n"].startswith("ADM_WING_") and r["cls"] is None))

# ---------------------------------------------------------------- G. общий z-размах
say("")
say("--- G. ОБЩИЙ Z-размах не-veto конструктива ---")
live = [r for r in objs if r["cls"] is not None]
if live:
    say("  объектов=%d  z=%.2f..%.2f  высота=%.2f" %
        (len(live), min(r["bb"][4] for r in live), max(r["bb"][5] for r in live),
         max(r["bb"][5] for r in live) - min(r["bb"][4] for r in live)))
    x0 = min(r["bb"][0] for r in live)
    x1 = max(r["bb"][1] for r in live)
    y0 = min(r["bb"][2] for r in live)
    y1 = max(r["bb"][3] for r in live)
    say("  план x=%.2f..%.2f (%.2f)  y=%.2f..%.2f (%.2f)" % (x0, x1, x1 - x0, y0, y1, y1 - y0))

say("")
say("--- H. ГИСТОГРАММА z0 всех не-veto объектов (шаг 0.25 м) ---")
hist = Counter(round(r["bb"][4] * 4) / 4.0 for r in live)
for z in sorted(hist):
    say("  z0=%8.2f  %4d" % (z, hist[z]))

# ---------------------------------------------------------------- I. стены, НЕ попавшие в WALL
WALLISH = HB.WALL_KIND
say("")
say("--- I. ЕСТЬ СТЕНОВЫЙ ТОКЕН, но в класс WALL НЕ попало (что потеряно) ---")
lost = [r for r in objs if r["cls"] != "WALL"
        and any(HX.alpha(t) in WALLISH for t in r["t"])]
say("  всего=%d" % len(lost))
for r in sorted(lost, key=lambda r: (r["rule"], r["n"])):
    bb = r["bb"]
    tag = "F%d" % r["f"] if r["f"] is not None else "  -"
    say("  %-4s %-11s %-40s z=%7.2f..%7.2f d=(%6.2f %6.2f %5.2f)" %
        (tag, r["rule"], r["n"][:40], bb[4], bb[5], r["d"][0], r["d"][1], r["d"][2]))

# ---------------------------------------------------------------- J. всё, что skip
say("")
say("--- J. ПРАВИЛО skip: что не опознано вообще ---")
for r in sorted([r for r in objs if r["rule"] == "skip"], key=lambda r: r["n"]):
    bb = r["bb"]
    tag = "F%d" % r["f"] if r["f"] is not None else "  -"
    say("  %-4s %-40s z=%7.2f..%7.2f d=(%6.2f %6.2f %5.2f)" %
        (tag, r["n"][:40], bb[4], bb[5], r["d"][0], r["d"][1], r["d"][2]))

# ---------------------------------------------------------------- K. что уйдёт в doors[]
say("")
say("--- K. КАНДИДАТЫ В doors[] (токен DOOR и НЕ под DOOR_VETO) ---")
dc = [r for r in objs if any(t in ("DOOR", "DOORS") for t in r["t"])
      and not ((set(HX.alpha(t) for t in r["t"]) | set(r["t"])) & HB.DOOR_VETO)]
say("  всего=%d" % len(dc))
for r in sorted(dc, key=lambda r: (-max(r["d"][0], r["d"][1]), r["n"]))[:40]:
    bb = r["bb"]
    say("  %-40s w=%6.2f h=%5.2f  d=(%6.2f %6.2f %5.2f)" %
        (r["n"][:40], max(r["d"][0], r["d"][1]), r["d"][2], r["d"][0], r["d"][1], r["d"][2]))
wide = [r for r in dc if max(r["d"][0], r["d"][1]) > 1.60]
say("  из них шире DOOR_MAX=1.60: %d" % len(wide))
for r in sorted(wide, key=lambda r: -max(r["d"][0], r["d"][1]))[:20]:
    say("     %-40s w=%6.2f" % (r["n"][:40], max(r["d"][0], r["d"][1])))

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "houses_admin_levels.txt")
with open(os.path.abspath(path), "w", encoding="utf-8") as fh:
    fh.write("\n".join(out) + "\n")
print("DONE", flush=True)
