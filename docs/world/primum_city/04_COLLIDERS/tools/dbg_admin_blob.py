# dbg_admin_blob.py — почему 44 стены ADMIN отброшены правилом `wall-blob`.
#
# Гипотеза: `strict_class` меряет `dims(aabb(obox(o)))` — ГАБАРИТ В МИРОВЫХ ОСЯХ. У повёрнутой
# на несколько градусов тонкой стены проекция на мировую ось толще самой стены, и правило
# «скорлупы» срабатывает ложно. Скрипт печатает оба числа рядом: локальные габариты объекта
# (`o.dimensions` — истинная толщина) и мировой AABB (то, чем меряет правило), плюс угол.
#
# Только чтение.
# Запуск: DBG_CHUNK=PRIMUM_ADMIN_S0_ADM_P1_01H.blend blender -b --factory-startup --python dbg_admin_blob.py
import bpy, os, sys, math

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX
import houses_boxes as HB

out = []


def say(s):
    print(s, flush=True)
    out.append(s)


CHUNK = os.environ.get("DBG_CHUNK", "PRIMUM_ADMIN_S0_ADM_P1_01H.blend")
bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, CHUNK))

say("=== ADMIN %s : ложный wall-blob ===" % CHUNK)
say("")
say("%-40s %-22s %-22s %-8s %s" % ("объект", "локальные d", "мировой AABB", "угол°", "вердикт"))

n_world = 0
n_local = 0
recovered = []
for o in bpy.data.objects:
    if o.type != 'MESH' or o.library is not None or o.data is None or len(o.data.polygons) == 0:
        continue
    if HX.has_ancestor(o, HX.ANCESTORS):
        continue
    toks = HX.base_tokens(o.name)
    cls, rule = HB.strict_class(o, toks)
    if rule != "wall-blob":
        continue
    n_world += 1

    pts = HB.obox(o)
    bbx = HB.dims(HB.aabb(pts))
    loc = (o.dimensions.x, o.dimensions.y, o.dimensions.z)
    th = math.degrees(HB.principal_theta([__import__("mathutils").Vector((p.x, p.y, 0.0)) for p in pts])) % 180.0

    # вердикт по ЛОКАЛЬНЫМ габаритам — тем же условием
    blob_local = (min(loc[0], loc[1]) > 0.60 and max(loc[0], loc[1]) > 1.2)
    if not blob_local:
        n_local += 1
        recovered.append(o.name)
    say("%-40s (%6.2f %6.2f %5.2f) (%6.2f %6.2f %5.2f) %8.2f  %s" %
        (o.name[:40], loc[0], loc[1], loc[2], bbx[0], bbx[1], bbx[2], th,
         "СТЕНА (правило ошиблось)" if not blob_local else "скорлупа — верно"))

say("")
say("всего wall-blob: %d" % n_world)
say("из них по ЛОКАЛЬНЫМ габаритам НЕ скорлупа (потерянные стены): %d" % n_local)
say("потеряно: %s" % ", ".join(recovered))

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "houses_admin_blob.txt")
with open(os.path.abspath(path), "w", encoding="utf-8") as fh:
    fh.write("\n".join(out) + "\n")
print("DONE", flush=True)
