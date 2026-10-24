# -*- coding: utf-8 -*-
"""
T-CHAR-UVRB: переразвёртка UV + перепечка BaseColor для AI-персонажей (Tripo и т.п.).

Запуск (один персонаж, headless):
  blender.exe -b --factory-startup --python-exit-code 1 -P rebake_character.py -- ^
      --src <model.fbx|model.glb> [--tex <BaseColor.jpg>] [--out-dir <dir>] [--res 2048] ...

Алгоритм (подробно — docs/Character/TextureRebake/01_ALGORITHM.md):
  1. Импорт, объединение мешей, сварка дублей вершин (UV хранятся на углах граней — не теряются).
  2. Сегментация на СГЛАЖЕННОМ прокси: Smart UV Project по сглаженной копии (та же топология)
     -> острова следуют форме тела, а не шуму складок/вокселей.
  3. Слияние мелких островов в соседа с самой длинной общей границей.
  4. Границы итоговых островов -> швы на оригинале; Unwrap (ABF) по швам на настоящей геометрии.
  5. Выравнивание масштаба островов + упаковка.
  6. Печка: UV_src -> исходная текстура -> Emission; цель — новая картинка на UV_bake
     (один и тот же меш, без ray-cast => нет артефактов cage/дистанции). Суперсэмплинг + Extend margin.
  7. Удаление UV_src, финальный материал, экспорт (FBX->FBX, GLB->GLB) + JSON-отчёт с метриками.
"""
import bpy, sys, os, math, time, json, argparse, glob
import numpy as np


# ----------------------------------------------------------------------------- args
def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="rebake_character")
    p.add_argument("--src", required=True, help="исходная модель .fbx/.glb")
    p.add_argument("--tex", default="", help="исходная BaseColor (по умолчанию *_BaseColor.* рядом с моделью или из материала)")
    p.add_argument("--out-dir", default="", help="папка результата (по умолчанию <папка модели>/Rebaked)")
    p.add_argument("--suffix", default="_Rebaked")
    p.add_argument("--report-dir", default="", help="папка JSON-отчёта (по умолчанию = out-dir)")
    p.add_argument("--res", type=int, default=2048, help="итоговое разрешение текстуры")
    p.add_argument("--supersample", type=int, default=2, help="печь в res*ss и уменьшить (антиалиасинг)")
    p.add_argument("--samples", type=int, default=4, help="Cycles samples при печке")
    p.add_argument("--margin-px", type=int, default=8, help="зазор между островами при упаковке, px итоговой текстуры")
    p.add_argument("--bleed-px", type=int, default=16, help="размазывание цвета за край острова (Extend), px итоговой текстуры")
    p.add_argument("--border-smooth", type=int, default=12, help="итерации мажоритарного сглаживания границ островов (0 = выкл)")
    p.add_argument("--weld", type=float, default=1e-5, help="порог сварки вершин (0 = не сваривать)")
    p.add_argument("--smooth-iters", type=int, default=600, help="итерации сглаживания прокси для сегментации")
    p.add_argument("--angle", type=float, default=66.0, help="angle limit Smart UV на прокси, градусы")
    p.add_argument("--min-island-frac", type=float, default=0.004, help="острова меньше этой доли площади сливаются с соседом")
    p.add_argument("--unwrap", default="MINIMUM_STRETCH", choices=["ANGLE_BASED", "CONFORMAL", "MINIMUM_STRETCH"])
    p.add_argument("--pack-shape", default="CONCAVE", choices=["CONCAVE", "CONVEX", "AABB"])
    p.add_argument("--gpu", action="store_true", help="печь на GPU (если доступно)")
    p.add_argument("--save-blend", default="", help="сохранить рабочий .blend (для ручной доводки швов)")
    p.add_argument("--no-export", action="store_true", help="только метрики/текстура, без экспорта модели")
    return p.parse_args(argv)


A = parse_args()
T0 = time.time()
TIMES = {}
REPORT = {"ticket": "T-CHAR-UVRB", "src": os.path.abspath(A.src), "params": vars(A)}


def tick(name, t):
    TIMES[name] = round(time.time() - t, 2)
    print(f"[UVRB] {name}: {TIMES[name]} s", flush=True)


def log(msg):
    print(f"[UVRB] {msg}", flush=True)


def set_active(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def mode(m):
    if bpy.context.object and bpy.context.object.mode != m:
        bpy.ops.object.mode_set(mode=m)


# ----------------------------------------------------------------------------- mesh numpy helpers
def edge_face_pairs(me):
    """Для каждого ребра с ровно двумя гранями: (edge_idx, face_a, face_b)."""
    F, L, E = len(me.polygons), len(me.loops), len(me.edges)
    loop_edge = np.empty(L, np.int64); me.loops.foreach_get("edge_index", loop_edge)
    loop_total = np.empty(F, np.int64); me.polygons.foreach_get("loop_total", loop_total)
    loop_face = np.repeat(np.arange(F, dtype=np.int64), loop_total)
    order = np.argsort(loop_edge, kind="stable")
    f_sorted = loop_face[order]
    counts = np.bincount(loop_edge, minlength=E)
    starts = np.concatenate([[0], np.cumsum(counts)[:-1]])
    two = np.where(counts == 2)[0]
    return two, f_sorted[starts[two]], f_sorted[starts[two] + 1], counts


def edge_lengths(me):
    co = np.empty(len(me.vertices) * 3, np.float64); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    ev = np.empty(len(me.edges) * 2, np.int64); me.edges.foreach_get("vertices", ev); ev = ev.reshape(-1, 2)
    return np.linalg.norm(co[ev[:, 0]] - co[ev[:, 1]], axis=1)


def components(n, a, b):
    """Связные компоненты графа (union-find на numpy: hook-by-min + pointer jumping)."""
    lab = np.arange(n, dtype=np.int64)
    while True:
        la, lb = lab[a], lab[b]
        diff = la != lb
        if not diff.any():
            break
        m = np.minimum(la[diff], lb[diff])
        np.minimum.at(lab, la[diff], m)
        np.minimum.at(lab, lb[diff], m)
        while True:
            nl = lab[lab]
            if np.array_equal(nl, lab):
                break
            lab = nl
    _, lab = np.unique(lab, return_inverse=True)
    return lab


def get_seams(me):
    s = np.zeros(len(me.edges), bool); me.edges.foreach_get("use_seam", s); return s


def set_seams(me, s):
    me.edges.foreach_set("use_seam", s.astype(bool)); me.update()


def seams_from_uv(o, uv_name):
    """Швы = границы UV-островов указанного слоя (C-оператор Blender)."""
    me = o.data
    me.uv_layers.active = me.uv_layers[uv_name]
    set_active(o); mode("EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.mesh.mark_seam(clear=True)
    bpy.ops.uv.seams_from_islands(mark_seams=True, mark_sharp=False)
    mode("OBJECT")
    return get_seams(me)


def uv_metrics(me, uv_name, pairs=None, elen=None):
    """Острова, длина швов, покрытие UV, перевёрнутые треугольники, разброс texel density."""
    uvl = me.uv_layers[uv_name]
    F = len(me.polygons)
    uv = np.empty(len(me.loops) * 2, np.float64); uvl.uv.foreach_get("vector", uv); uv = uv.reshape(-1, 2)
    ls = np.empty(F, np.int64); me.polygons.foreach_get("loop_start", ls)
    lt = np.empty(F, np.int64); me.polygons.foreach_get("loop_total", lt)
    area3 = np.empty(F, np.float64); me.polygons.foreach_get("area", area3)
    # площадь в UV (shoelace по полигону; меши AI-генераторов треугольные, но считаем общо)
    uva = np.zeros(F)
    maxk = int(lt.max())
    for k in range(maxk):
        valid = lt > k
        i0 = ls + k
        i1 = ls + (k + 1) % lt
        p, q = uv[i0[valid]], uv[i1[valid]]
        uva[valid] += p[:, 0] * q[:, 1] - q[:, 0] * p[:, 1]
    uva *= 0.5
    two, fa, fb, counts = pairs if pairs is not None else edge_face_pairs(me)
    if elen is None:
        elen = edge_lengths(me)
    # граница острова: на ребре UV двух граней не совпадают.
    # (face, vert) -> loop, векторно через сортированный ключ
    loop_vert = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", loop_vert)
    fv = np.repeat(np.arange(F), lt) * (len(me.vertices) + 1) + loop_vert
    order = np.argsort(fv)
    fv_sorted = fv[order]
    ev = np.empty(len(me.edges) * 2, np.int64); me.edges.foreach_get("vertices", ev); ev = ev.reshape(-1, 2)
    def loop_of(face, vert):
        k = face * (len(me.vertices) + 1) + vert
        return order[np.searchsorted(fv_sorted, k)]
    v0, v1 = ev[two, 0], ev[two, 1]
    d0 = np.abs(uv[loop_of(fa, v0)] - uv[loop_of(fb, v0)]).max(axis=1)
    d1 = np.abs(uv[loop_of(fa, v1)] - uv[loop_of(fb, v1)]).max(axis=1)
    connected = (d0 < 1e-6) & (d1 < 1e-6)
    lab = components(F, fa[connected], fb[connected])
    n_isl = int(lab.max()) + 1
    isl_faces = np.bincount(lab, minlength=n_isl)
    seam_len = float(elen[two[~connected]].sum())
    # ориентация: доля перевёрнутых граней относительно доминирующего знака острова
    sgn = np.sign(uva)
    isl_sgn = np.sign(np.bincount(lab, weights=uva, minlength=n_isl))
    flipped = int(((sgn != isl_sgn[lab]) & (sgn != 0)).sum())
    # texel density: sqrt(uvA/A3), нормировано на средневзвешенное
    ok = area3 > 1e-14
    td = np.sqrt(np.abs(uva[ok]) / area3[ok])
    w = area3[ok]
    mean = float((td * w).sum() / w.sum())
    cv = float(np.sqrt(((td - mean) ** 2 * w).sum() / w.sum()) / mean) if mean > 0 else 0.0
    return {
        "islands": n_isl,
        "islands_lt_50_faces": int((isl_faces < 50).sum()),
        "largest_island_faces": int(isl_faces.max()),
        "seam_length": round(seam_len, 4),
        "uv_coverage": round(float(np.abs(uva).sum()), 4),
        "flipped_faces": flipped,
        "texel_density_cv": round(cv, 4),
    }


# ----------------------------------------------------------------------------- 1. import
t = time.time()
bpy.ops.wm.read_factory_settings(use_empty=True)
src = os.path.abspath(A.src)
ext = os.path.splitext(src)[1].lower()
if ext == ".fbx":
    bpy.ops.import_scene.fbx(filepath=src)
elif ext in (".glb", ".gltf"):
    bpy.ops.import_scene.gltf(filepath=src)
else:
    raise SystemExit(f"unsupported: {ext}")
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if not meshes:
    raise SystemExit("no mesh objects")
name = os.path.splitext(os.path.basename(src))[0]
out_dir = os.path.abspath(A.out_dir) if A.out_dir else os.path.join(os.path.dirname(src), "Rebaked")
os.makedirs(out_dir, exist_ok=True)
out_name = name + A.suffix

# исходная текстура: явная -> *_BaseColor.* рядом -> из материала
tex_path = A.tex
if not tex_path:
    cand = sorted(glob.glob(os.path.join(os.path.dirname(src), name + "_BaseColor.*")))
    tex_path = cand[0] if cand else ""
REPORT["src_texture"] = tex_path or "<from material>"

# UV-источник: активный render-слой каждого меша -> имя UV_src (для корректного join)
for o in meshes:
    me = o.data
    if not me.uv_layers:
        raise SystemExit(f"{o.name}: no UV layer — нечего перепекать")
    rl = next((u for u in me.uv_layers if u.active_render), me.uv_layers[0])
    rl.name = "UV_src"
if len(meshes) > 1:
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active if len(meshes) > 1 else meshes[0]
me = obj.data
for u in list(me.uv_layers):
    if u.name != "UV_src":
        me.uv_layers.remove(u)
tick("import", t)

# ----------------------------------------------------------------------------- 2. weld
t = time.time()
v_before = len(me.vertices)
if A.weld > 0:
    set_active(obj); mode("EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=A.weld)
    mode("OBJECT")
REPORT["mesh"] = {"verts_before_weld": v_before, "verts": len(me.vertices), "faces": len(me.polygons)}
REPORT["src_dims"] = [round(v, 5) for v in obj.dimensions]
REPORT["src_armatures"] = sum(1 for o in bpy.data.objects if o.type == "ARMATURE")
tick("weld", t)
bpy.context.scene.tool_settings.use_uv_select_sync = True

pairs = edge_face_pairs(me)
elen = edge_lengths(me)
REPORT["uv_src"] = uv_metrics(me, "UV_src", pairs, elen)
log(f"src UV: {REPORT['uv_src']}")

# ----------------------------------------------------------------------------- 3. segmentation on smoothed proxy
t = time.time()
proxy = obj.copy(); proxy.data = me.copy(); proxy.name = "UVRB_proxy"
bpy.context.scene.collection.objects.link(proxy)
proxy.modifiers.clear()
proxy.parent = None
if A.smooth_iters > 0:
    set_active(proxy)
    m = proxy.modifiers.new("uvrb_smooth", "SMOOTH"); m.factor = 0.5; m.iterations = A.smooth_iters
    bpy.ops.object.modifier_apply(modifier=m.name)
pm = proxy.data
pm.uv_layers.new(name="UV_seg")
pm.uv_layers.active = pm.uv_layers["UV_seg"]
set_active(proxy); mode("EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(A.angle), island_margin=0.0, area_weight=0.0,
                         correct_aspect=True, scale_to_bounds=False)
mode("OBJECT")
seg_seam = seams_from_uv(proxy, "UV_seg")
bpy.data.objects.remove(proxy, do_unlink=True)
two, fa, fb, counts = pairs
F = len(me.polygons)
keep = ~seg_seam[two]
lab = components(F, fa[keep], fb[keep])
REPORT["segmentation_raw_islands"] = int(lab.max()) + 1
tick("segment", t)

# ----------------------------------------------------------------------------- 4. merge small islands + smooth borders
import heapq
t = time.time()
area3 = np.empty(F, np.float64); me.polygons.foreach_get("area", area3)
total_area = float(area3.sum())
thr = A.min_island_frac * total_area
NV, NE = len(me.vertices), len(me.edges)
loop_total_ = np.empty(F, np.int64); me.polygons.foreach_get("loop_total", loop_total_)
loop_face_ = np.repeat(np.arange(F, dtype=np.int64), loop_total_)
loop_vert_ = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", loop_vert_)
loop_edge_ = np.empty(len(me.loops), np.int64); me.loops.foreach_get("edge_index", loop_edge_)
cent = np.empty(F * 3, np.float64); me.polygons.foreach_get("center", cent); cent = cent.reshape(-1, 3)


def euler_per_chart(lab):
    """χ = V − E + F для каждого острова (диск = 1, цилиндр = 0, замкнутая оболочка = 2)."""
    C = int(lab.max()) + 1
    cl = lab[loop_face_]
    nv = np.bincount(np.unique(cl * NV + loop_vert_) // NV, minlength=C)
    ne = np.bincount(np.unique(cl * NE + loop_edge_) // NE, minlength=C)
    nf = np.bincount(lab, minlength=C)
    return nv - ne + nf


def chart_boundary_vsets(lab):
    """Для каждого острова — множество вершин, общих с другими островами."""
    C = int(lab.max()) + 1
    keys = np.unique(lab[loop_face_] * NV + loop_vert_)
    c_of, v_of = keys // NV, keys % NV
    m = np.bincount(v_of, minlength=NV)[v_of] > 1
    sets = [set() for _ in range(C)]
    for c, v in zip(c_of[m].tolist(), v_of[m].tolist()):
        sets[c].add(v)
    return sets


def merge_small(lab):
    """Острова площадью < thr сливаются с соседом (по убыванию длины общей границы), НО только если
    результат остаётся топологическим диском: χ(i∪j) = χi + χj − (V∩ − E∩) == 1.
    Иначе слияние двух полос вокруг руки даёт цилиндр (χ=0), который нельзя развернуть без искажений."""
    n = int(lab.max()) + 1
    areas = np.bincount(lab, weights=area3, minlength=n).tolist()
    chi = euler_per_chart(lab).tolist()
    vsets = chart_boundary_vsets(lab)
    la, lb = lab[fa], lab[fb]
    bnd = la != lb
    lo, hi = np.minimum(la[bnd], lb[bnd]), np.maximum(la[bnd], lb[bnd])
    uk, inv = np.unique(lo * n + hi, return_inverse=True)
    wsum = np.bincount(inv, weights=elen[two[bnd]])
    csum = np.bincount(inv)
    adj = [dict() for _ in range(n)]
    for k, w, c in zip(uk.tolist(), wsum.tolist(), csum.tolist()):
        i, j = divmod(k, n)
        e = [w, c]
        adj[i][j] = e
        adj[j][i] = e
    parent = list(range(n))

    def root(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    heap = [(areas[i], i) for i in range(n) if areas[i] < thr]
    heapq.heapify(heap)
    merges = 0
    stuck = set()
    while heap:
        a_, i = heapq.heappop(heap)
        if root(i) != i or areas[i] != a_ or areas[i] >= thr or not adj[i]:
            continue  # устаревшая запись / уже крупный / изолированная оболочка
        chosen = None
        for j, (w, c) in sorted(adj[i].items(), key=lambda kv: -kv[1][0]):
            si, sj = vsets[i], vsets[j]
            small, big = (si, sj) if len(si) < len(sj) else (sj, si)
            vs = sum(1 for v in small if v in big)
            chi_new = chi[i] + chi[j] - (vs - c)
            if chi_new == 1:
                chosen = (j, chi_new); break
        if chosen is None:
            stuck.add(i)
            continue
        j, chi_new = chosen
        parent[i] = j
        areas[j] += areas[i]
        chi[j] = chi_new
        vsets[j] |= vsets[i]; vsets[i] = set()
        for k, (w, c) in adj[i].items():
            if k == j:
                continue
            e = adj[j].get(k)
            if e is None:
                e = [0.0, 0]; adj[j][k] = e
            e[0] += w; e[1] += c
            adj[k][j] = e
            adj[k].pop(i, None)
        adj[j].pop(i, None)
        adj[i] = {}
        merges += 1
        stuck.discard(j)
        if areas[j] < thr:
            heapq.heappush(heap, (areas[j], j))
    out = np.array([root(i) for i in range(n)], np.int64)[lab]
    _, out = np.unique(out, return_inverse=True)
    return out, merges, len(stuck)


def face_neighbors3():
    """F x 3 соседей по рёбрам (для треугольников — все; -1 = нет)."""
    s = np.concatenate([fa, fb]); d = np.concatenate([fb, fa])
    o = np.argsort(s, kind="stable"); s, d = s[o], d[o]
    cnt = np.bincount(s, minlength=F)
    st = np.concatenate([[0], np.cumsum(cnt)[:-1]])
    rank = np.arange(len(s)) - st[s]
    nb = np.full((F, 3), -1, np.int64)
    m = rank < 3
    nb[s[m], rank[m]] = d[m]
    return nb


def smooth_borders(lab, iters):
    """Мажоритарный фильтр: грань переходит к острову, которому принадлежат >=2 её соседа.
    Срезает «пилу» по границам островов -> короче швы, ровнее края, лучше упаковка."""
    nb = face_neighbors3()
    sentinel = -1 - np.arange(3)[None, :]
    for _ in range(iters):
        L = np.where(nb >= 0, lab[np.maximum(nb, 0)], sentinel)
        c01, c02, c12 = L[:, 0] == L[:, 1], L[:, 0] == L[:, 2], L[:, 1] == L[:, 2]
        new = lab.copy()
        m1 = (c01 | c02) & (L[:, 0] != lab)
        new[m1] = L[m1, 0]
        m2 = ~(c01 | c02) & c12 & (L[:, 1] != lab)
        new[m2] = L[m2, 1]
        if np.array_equal(new, lab):
            break
        lab = new
    # фильтр мог разорвать остров — пересобираем связные компоненты внутри одинаковых меток
    same = lab[fa] == lab[fb]
    return components(F, fa[same], fb[same])


lab, m1, st1 = merge_small(lab)
n_after1 = int(lab.max()) + 1
m2 = 0
if A.border_smooth > 0:
    lab = smooth_borders(lab, A.border_smooth)
    lab, m2, st1 = merge_small(lab)

# --- топология диска: Unwrap корректен только для островов с характеристикой Эйлера χ = 1.
# Цилиндр (рукав, штанина, пола куртки) имеет χ = 0 и при fill_holes раскладывается «кругом»
# с радиальным растяжением. Такие острова режем плоскостью, содержащей их главную ось (PCA),
# сглаживаем разрез и снова сливаем крошку (с тем же запретом на не-диск).
splits = 0
for _pass in range(8):
    chi = euler_per_chart(lab)
    bad = np.where(chi != 1)[0]
    if len(bad) == 0:
        break
    nxt = int(lab.max()) + 1
    did = 0
    for c in bad.tolist():
        idx = np.where(lab == c)[0]
        if len(idx) < 8:
            continue
        p = cent[idx]; w = area3[idx]
        mu = (p * w[:, None]).sum(0) / w.sum()
        cov = ((p - mu) * w[:, None]).T @ (p - mu)
        _, vec = np.linalg.eigh(cov)          # собств. векторы по возрастанию
        side = (p - mu) @ vec[:, 1] > 0        # плоскость содержит главную ось vec[:, 2]
        lab[idx[side]] = nxt; nxt += 1
        splits += 1; did += 1
    if did == 0:
        break
    same = lab[fa] == lab[fb]
    lab = components(F, fa[same], fb[same])
    if A.border_smooth > 0:
        lab = smooth_borders(lab, 4)
    lab, mm, st1 = merge_small(lab)
    m2 += mm
chi = euler_per_chart(lab)
REPORT["non_disk_splits"] = splits
REPORT["non_disk_charts_left"] = int((chi != 1).sum())
REPORT["small_islands_unmergeable"] = st1
final = lab
REPORT["segmentation_islands_after_merge"] = n_after1
REPORT["segmentation_islands_final"] = int(final.max()) + 1
REPORT["segmentation_merges"] = m1 + m2
tick("merge", t)
log(f"segmentation: raw {REPORT['segmentation_raw_islands']} -> merge {n_after1} -> final {REPORT['segmentation_islands_final']}"
    f" (non-disk splits {splits}, left {REPORT['non_disk_charts_left']})")

# ----------------------------------------------------------------------------- 5. seams + unwrap + pack on original
t = time.time()
seam = np.ones(len(me.edges), bool)        # рёбра не-manifold/границы — шов
seam[two] = final[fa] != final[fb]
set_seams(me, seam)
uvb = me.uv_layers.new(name="UV_bake")
me.uv_layers.active = uvb
set_active(obj); mode("EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.select_all(action="SELECT")
bpy.ops.uv.unwrap(method=A.unwrap, fill_holes=True, correct_aspect=True, margin=0.0)
tick("unwrap", t)
t = time.time()
bpy.ops.uv.select_all(action="SELECT")
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method="FRACTION",
                        margin=A.margin_px / float(A.res), shape_method=A.pack_shape)
mode("OBJECT")
tick("pack", t)
REPORT["uv_new"] = uv_metrics(me, "UV_bake", pairs, elen)
log(f"new UV: {REPORT['uv_new']}")

# ----------------------------------------------------------------------------- 6. bake
t = time.time()
def find_image(mat):
    if mat is None or mat.node_tree is None:
        return None
    nt = mat.node_tree
    for nd in nt.nodes:
        if nd.bl_idname == "ShaderNodeBsdfPrincipled":
            inp = nd.inputs.get("Base Color")
            if inp and inp.is_linked:
                fn = inp.links[0].from_node
                if fn.bl_idname == "ShaderNodeTexImage" and fn.image:
                    return fn.image
    for nd in nt.nodes:
        if nd.bl_idname == "ShaderNodeTexImage" and nd.image:
            return nd.image
    return None

ss = max(1, A.supersample)
bake_res = A.res * ss
target = bpy.data.images.new(out_name + "_BaseColor", bake_res, bake_res, alpha=False, float_buffer=False)
target.colorspace_settings.name = "sRGB"
ext_img = bpy.data.images.load(tex_path, check_existing=True) if tex_path else None

orig_mats = list(me.materials) or [None]
if not me.materials:
    me.materials.append(None)
bake_mats = []
for si, om in enumerate(orig_mats):
    img = ext_img or find_image(om)
    if img is None:
        raise SystemExit(f"slot {si}: исходная текстура не найдена (--tex)")
    img.colorspace_settings.name = "sRGB"
    bm_ = bpy.data.materials.new(f"UVRB_bake_{si}")
    try:
        bm_.use_nodes = True
    except Exception:
        pass
    nt = bm_.node_tree
    nt.nodes.clear()
    uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = "UV_src"
    tsrc = nt.nodes.new("ShaderNodeTexImage"); tsrc.image = img; tsrc.interpolation = "Cubic"
    em = nt.nodes.new("ShaderNodeEmission")
    outn = nt.nodes.new("ShaderNodeOutputMaterial")
    ttgt = nt.nodes.new("ShaderNodeTexImage"); ttgt.image = target
    nt.links.new(uvn.outputs["UV"], tsrc.inputs["Vector"])
    nt.links.new(tsrc.outputs["Color"], em.inputs["Color"])
    nt.links.new(em.outputs["Emission"], outn.inputs["Surface"])
    for nd in nt.nodes:
        nd.select = False
    ttgt.select = True
    nt.nodes.active = ttgt
    me.materials[si] = bm_
    bake_mats.append(bm_)

sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = A.samples
sc.cycles.use_denoising = False
if A.gpu:
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for dt in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
            try:
                prefs.compute_device_type = dt
                prefs.get_devices()
                if any(d.type == dt for d in prefs.devices):
                    for d in prefs.devices:
                        d.use = True
                    sc.cycles.device = "GPU"
                    break
            except Exception:
                continue
    except Exception as e:
        log(f"GPU unavailable: {e}")
sc.render.bake.margin = A.bleed_px * ss
sc.render.bake.margin_type = "EXTEND"
sc.render.bake.use_clear = True
uvb.active_render = True
set_active(obj); mode("OBJECT")
bpy.ops.object.bake(type="EMIT", uv_layer="UV_bake", margin=A.bleed_px * ss, use_clear=True)
if ss > 1:
    target.scale(A.res, A.res)
tex_out = os.path.join(out_dir, out_name + "_BaseColor.png")
target.filepath_raw = tex_out
target.file_format = "PNG"
target.save()
REPORT["texture_out"] = tex_out
tick("bake", t)

# ----------------------------------------------------------------------------- 7. finalize + export
t = time.time()
final_mat = bpy.data.materials.new(out_name + "_Material")
try:
    final_mat.use_nodes = True
except Exception:
    pass
nt = final_mat.node_tree
bsdf = next(nd for nd in nt.nodes if nd.bl_idname == "ShaderNodeBsdfPrincipled")
timg = nt.nodes.new("ShaderNodeTexImage")
final_img = bpy.data.images.load(tex_out, check_existing=False)
timg.image = final_img
nt.links.new(timg.outputs["Color"], bsdf.inputs["Base Color"])
if "Roughness" in bsdf.inputs:
    bsdf.inputs["Roughness"].default_value = 0.9
# один слот материала (все слоты запечены в одну текстуру)
ma = np.zeros(len(me.polygons), np.int64)
me.polygons.foreach_set("material_index", ma)
while len(me.materials) > 1:
    me.materials.pop(index=len(me.materials) - 1)
me.materials[0] = final_mat
me.uv_layers.remove(me.uv_layers["UV_src"])
me.uv_layers["UV_bake"].name = "UVMap"
me.uv_layers.active = me.uv_layers["UVMap"]
me.uv_layers["UVMap"].active_render = True
set_seams(me, seam)  # швы оставляем — пригодятся при ручной доводке в .blend
for m_ in bake_mats:
    bpy.data.materials.remove(m_)

if not A.no_export:
    if ext == ".fbx":
        model_out = os.path.join(out_dir, out_name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=model_out, use_selection=False,
                                 object_types={"MESH", "ARMATURE", "EMPTY"},
                                 path_mode="STRIP", embed_textures=False,
                                 add_leaf_bones=False, bake_anim=False,
                                 apply_scale_options="FBX_SCALE_NONE")
    else:
        model_out = os.path.join(out_dir, out_name + ".glb")
        bpy.ops.export_scene.gltf(filepath=model_out, export_format="GLB")
    REPORT["model_out"] = model_out
if A.save_blend:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(A.save_blend), compress=True)
    REPORT["blend_out"] = os.path.abspath(A.save_blend)
tick("export", t)

# ----------------------------------------------------------------------------- 8. round-trip QA
# Реимпорт результата в чистую сцену: габариты/грани/UV/текстура должны совпасть с исходником.
if not A.no_export:
    t = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=model_out)
    else:
        bpy.ops.import_scene.gltf(filepath=model_out)
    rms = [o for o in bpy.data.objects if o.type == "MESH"]
    rt = {"meshes": len(rms)}
    if rms:
        r = rms[0]
        rt["dims"] = [round(v, 5) for v in r.dimensions]
        rt["faces"] = len(r.data.polygons)
        rt["uv_layers"] = [u.name for u in r.data.uv_layers]
        rt["armatures"] = sum(1 for o in bpy.data.objects if o.type == "ARMATURE")
        img = None
        for m_ in r.data.materials:
            if m_ and m_.node_tree:
                img = next((nd.image for nd in m_.node_tree.nodes if nd.bl_idname == "ShaderNodeTexImage" and nd.image), None)
        rt["texture"] = os.path.basename(img.filepath) if img else None
        dd = max(abs(a - b) for a, b in zip(rt["dims"], REPORT["src_dims"]))
        rt["ok"] = (len(rms) == 1 and rt["faces"] == REPORT["mesh"]["faces"] and len(rt["uv_layers"]) == 1
                    and dd <= 1e-3 * max(REPORT["src_dims"]) and rt["armatures"] == REPORT["src_armatures"]
                    and rt["texture"] is not None)
    else:
        rt["ok"] = False
    REPORT["roundtrip"] = rt
    log(f"roundtrip: {rt}")
    tick("roundtrip", t)

REPORT["times_sec"] = TIMES
REPORT["total_sec"] = round(time.time() - T0, 1)
rep_dir = os.path.abspath(A.report_dir) if A.report_dir else out_dir
os.makedirs(rep_dir, exist_ok=True)
rep_path = os.path.join(rep_dir, out_name + "_report.json")
with open(rep_path, "w", encoding="utf-8") as f:
    json.dump(REPORT, f, ensure_ascii=False, indent=2)
print("UVRB_REPORT=" + json.dumps({k: REPORT.get(k) for k in ("uv_src", "uv_new", "roundtrip", "times_sec", "total_sec")}), flush=True)
