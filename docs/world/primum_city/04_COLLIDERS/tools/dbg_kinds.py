# dbg_kinds.py — словарь ВИДОВ объектов в RAW-чанках: токен сразу после номера этажа.
# Нужно, чтобы отделить конструктив (стены/плиты/крыши/марши) от мебели и декора.
#
# Запуск: DBG_CHUNKS="S5_NE_P1_20H,S1_GLD_P1_25H" blender -b --factory-startup --python dbg_kinds.py
import bpy, os, sys, re
from collections import Counter, defaultdict

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import houses_export as HX

CHUNKS = [s.strip() for s in os.environ.get("DBG_CHUNKS", "S5_NE_P1_20H").split(",") if s.strip()]
FILES = [f for f in sorted(os.listdir(HX.RAW))
         if f.upper().startswith("PRIMUM_HOUSES_") and f.upper().endswith(".BLEND")
         and "COPY" not in f.upper() and "BAK" not in f.upper()]

for want in CHUNKS:
    fname = next((f for f in FILES if want.upper() in f.upper()), None)
    if fname is None:
        print("MISSING %s" % want, flush=True)
        continue
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HX.RAW, fname))
    kinds = Counter()
    ex = {}
    dims = defaultdict(list)
    nh = Counter()
    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None:
            continue
        if o.data is None or len(o.data.polygons) == 0:
            continue
        toks = HX.base_tokens(o.name)
        idx = next((i for i, t in enumerate(toks) if t.isdigit()), None)
        kind = toks[idx + 1] if idx is not None and idx + 1 < len(toks) else "?"
        kind = HX.alpha(kind) or kind
        kinds[kind] += 1
        d = o.dimensions
        dims[kind].append((max(d), sorted((d.x, d.y, d.z))[1]))
        if kind not in ex:
            ex[kind] = o.name
        nh[HX.house_of(toks)] += 1

    print("", flush=True)
    print("##### %s  mesh=%d  домов(по house_of)=%d" % (fname, sum(kinds.values()), len(nh)), flush=True)
    print("  ВИД (после этажа): количество  maxdim_med  middim_med  пример", flush=True)
    for k, n in kinds.most_common(70):
        ds = dims[k]
        mx = sorted(x for x, _ in ds)[len(ds) // 2]
        md = sorted(y for _, y in ds)[len(ds) // 2]
        print("  %-14s %6d   %5.2f  %5.2f   %s" % (k, n, mx, md, ex[k][:70]), flush=True)
    print("  -- дома (топ 15 по объектам) --", flush=True)
    for h, n in nh.most_common(15):
        print("     %-26s %d" % (h, n), flush=True)
print("DONE", flush=True)
