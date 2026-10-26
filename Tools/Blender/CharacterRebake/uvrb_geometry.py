# -*- coding: utf-8 -*-
"""T-CHAR-UVRB v2: объектонезависимые чарты и измеряемый выбор UV. Оригинальная геометрия не меняется."""
import heapq
import math
import bpy
import bmesh
import numpy as np


def array(collection, prop, width=1, dtype=np.float64):
    a = np.empty(len(collection) * width, dtype=dtype)
    collection.foreach_get(prop, a)
    return a.reshape(-1, width) if width > 1 else a


def active(obj):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def edit_all(obj):
    active(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')


def components(n, a, b):
    lab = np.arange(n, dtype=np.int64)
    while len(a):
        la, lb = lab[a], lab[b]
        d = la != lb
        if not d.any():
            break
        lo = np.minimum(la[d], lb[d])
        np.minimum.at(lab, la[d], lo)
        np.minimum.at(lab, lb[d], lo)
        while not np.array_equal(lab, lab[lab]):
            lab = lab[lab]
    return np.unique(lab, return_inverse=True)[1]


def uv_get(me, name):
    return array(me.uv_layers[name].data, 'uv', 2)


def uv_set(me, name, uv):
    me.uv_layers[name].data.foreach_set('uv', np.asarray(uv).ravel())
    me.update()


def chart_copy(work, connect_splits=True, sharp_degrees=50.):
    """Сшивает только однозначные совпадающие boundary edges в аналитической копии.
    Corner ID обеспечивает обратный перенос UV; ни weld, ни изменённые normals не попадают в результат.
    """
    chart = work.copy()
    chart.data = work.data.copy()
    chart.name = '__UVRB_chart'
    bpy.context.scene.collection.objects.link(chart)
    chart.modifiers.clear()
    chart.hide_render = True
    me = chart.data
    attr = me.attributes.new('__UVRB_corner_id', 'INT', 'CORNER')
    attr.data.foreach_set('value', np.arange(len(me.loops), dtype=np.int32))
    before_loops, before_faces = len(me.loops), len(me.polygons)
    joined = 0
    if connect_splits:
        g = Graph(me, sharp_degrees)
        _, ids = np.unique(g.co, axis=0, return_inverse=True)
        boundary = np.where(g.counts[g.le] == 1)[0]
        nxt = g.ls[g.lf[boundary]] + (boundary-g.ls[g.lf[boundary]]+1) % g.lt[g.lf[boundary]]
        v0, v1 = g.lv[boundary], g.lv[nxt]
        c0, c1 = ids[v0], ids[v1]
        n = len(g.co)
        keys = np.minimum(c0,c1)*n+np.maximum(c0,c1)
        order = np.argsort(keys, kind='stable')
        _, starts, counts = np.unique(keys[order], return_index=True, return_counts=True)
        authored = array(me.edges, 'use_seam', dtype=bool) | array(me.edges, 'use_edge_sharp', dtype=bool)
        materials = array(me.polygons, 'material_index', dtype=np.int64)
        pairs = []
        for start in starts[counts == 2]:
            a,b = order[start:start+2]
            fa,fb = g.lf[boundary[a]],g.lf[boundary[b]]
            if c0[a] != c1[b] or c1[a] != c0[b] or fa == fb:
                continue
            if authored[g.le[boundary[a]]] or authored[g.le[boundary[b]]] or materials[fa] != materials[fb]:
                continue
            if np.dot(g.normals[fa],g.normals[fb]) < math.cos(math.radians(sharp_degrees)):
                continue
            pairs.extend(((int(v0[a]),int(v1[b])),(int(v1[a]),int(v0[b]))))
        if pairs:
            pa,pb = np.asarray(pairs,dtype=np.int64).T
            labels = components(len(g.co),pa,pb)
            roots = np.full(int(labels.max())+1,len(g.co),np.int64)
            np.minimum.at(roots,labels,np.arange(len(g.co)))
            bm = bmesh.new()
            try:
                bm.from_mesh(me)
                bm.verts.ensure_lookup_table()
                targetmap = {bm.verts[i]:bm.verts[int(roots[labels[i]])] for i in range(len(g.co)) if i != roots[labels[i]]}
                joined = len(targetmap)
                bmesh.ops.weld_verts(bm,targetmap=targetmap)
                bm.to_mesh(me)
                me.update()
            finally:
                bm.free()
    corners = array(me.attributes['__UVRB_corner_id'].data, 'value', dtype=np.int64)
    if len(me.loops) != before_loops or len(me.polygons) != before_faces or not np.array_equal(np.sort(corners),np.arange(before_loops)):
        data=chart.data
        bpy.data.objects.remove(chart,do_unlink=True)
        bpy.data.meshes.remove(data)
        raise ValueError('Analytical connectivity changed faces/corners; source untouched')
    return chart, corners, joined


class Graph:
    def __init__(self, me, sharp_degrees=50.0):
        self.me = me
        self.n = len(me.polygons)
        self.lv = array(me.loops, 'vertex_index', dtype=np.int64)
        self.le = array(me.loops, 'edge_index', dtype=np.int64)
        self.lt = array(me.polygons, 'loop_total', dtype=np.int64)
        self.ls = array(me.polygons, 'loop_start', dtype=np.int64)
        self.lf = np.repeat(np.arange(self.n), self.lt)
        self.ev = array(me.edges, 'vertices', 2, np.int64)
        self.co = array(me.vertices, 'co', 3)
        self.areas = array(me.polygons, 'area')
        self.centers = array(me.polygons, 'center', 3)
        self.normals = array(me.polygons, 'normal', 3)
        self.counts = np.bincount(self.le, minlength=len(me.edges))
        self.two = np.where(self.counts == 2)[0]
        order = np.argsort(self.le, kind='stable')
        starts = np.r_[0, np.cumsum(self.counts)[:-1]]
        self.la = order[starts[self.two]]
        self.lb = order[starts[self.two] + 1]
        self.fa, self.fb = self.lf[self.la], self.lf[self.lb]
        self.lengths = np.linalg.norm(self.co[self.ev[:, 0]] - self.co[self.ev[:, 1]], axis=1)
        self.shell = components(self.n, self.fa, self.fb)
        self.shell_area = np.bincount(self.shell, weights=self.areas)
        dot = np.einsum('ij,ij->i', self.normals[self.fa], self.normals[self.fb])
        self.angles = np.degrees(np.arccos(np.clip(dot, -1, 1)))
        mi = array(me.polygons, 'material_index', dtype=np.int64)
        authored = array(me.edges, 'use_seam', dtype=bool)
        sharp = array(me.edges, 'use_edge_sharp', dtype=bool)
        smooth = array(me.polygons, 'use_smooth', dtype=bool)
        # Сильные геометрические рёбра защищены и без explicit sharp-флага.
        self.locked = authored[self.two] | sharp[self.two] | (mi[self.fa] != mi[self.fb]) | (self.angles >= sharp_degrees)
        self.locked |= (~smooth[self.fa] | ~smooth[self.fb]) & (self.angles > 1.0)
        self.allowed = ~self.locked
        self.region = components(self.n, self.fa[self.allowed], self.fb[self.allowed])
        self.frozen = np.zeros(self.n, bool)
        self.frozen[self.fa[self.locked]] = True
        self.frozen[self.fb[self.locked]] = True

    def labels(self, seams):
        use = self.allowed & ~seams[self.two]
        return components(self.n, self.fa[use], self.fb[use])

    def chi(self, labels):
        c = int(labels.max()) + 1
        cl = labels[self.lf]
        nv, ne = len(self.co), len(self.ev)
        v = np.bincount(np.unique(cl * nv + self.lv) // nv, minlength=c)
        e = np.bincount(np.unique(cl * ne + self.le) // ne, minlength=c)
        f = np.bincount(labels, minlength=c)
        return v - e + f

    def disk_errors(self, labels):
        """χ, manifold boundary degree и одна граничная петля; не только χ=1."""
        n = int(labels.max()) + 1
        bad = self.chi(labels) != 1
        keys, counts = np.unique(labels[self.lf] * len(self.ev) + self.le, return_counts=True)
        chart, edges = keys // len(self.ev), keys % len(self.ev)
        bad[chart[counts > 2]] = True
        mask = counts == 1
        bc, be = chart[mask], edges[mask]
        order = np.argsort(bc, kind='stable')
        cs = np.bincount(bc, minlength=n)
        starts = np.r_[0, np.cumsum(cs)]
        for c in range(n):
            if bad[c]:
                continue
            ee = self.ev[be[order[starts[c]:starts[c+1]]]]
            if not len(ee):
                bad[c] = True
                continue
            vs, inv, degrees = np.unique(ee, return_inverse=True, return_counts=True)
            inv = inv.reshape(-1, 2)
            if np.any(degrees != 2) or len(np.unique(components(len(vs), inv[:, 0], inv[:, 1]))) != 1:
                bad[c] = True
        return np.where(bad)[0]

    def merge(self, labels, fraction):
        n = int(labels.max()) + 1
        area = np.bincount(labels, weights=self.areas).tolist()
        chi = self.chi(labels).tolist()
        # Мера мелкости локальна для связной оболочки.
        first = np.full(n, self.n, np.int64)
        np.minimum.at(first, labels, np.arange(self.n))
        thresholds = self.shell_area[self.shell[first]] * fraction
        keys = np.unique(labels[self.lf] * len(self.co) + self.lv)
        vsets = [set() for _ in range(n)]
        for c, v in zip((keys // len(self.co)).tolist(), (keys % len(self.co)).tolist()):
            vsets[c].add(v)
        a, b = labels[self.fa], labels[self.fb]
        d = a != b
        k, inv = np.unique(np.minimum(a[d], b[d]) * n + np.maximum(a[d], b[d]), return_inverse=True)
        lengths = np.bincount(inv, weights=self.lengths[self.two[d]])
        counts = np.bincount(inv)
        locks = np.bincount(inv, weights=self.locked[d])
        adj = [dict() for _ in range(n)]
        for key, length, count, lock in zip(k.tolist(), lengths.tolist(), counts.tolist(), locks.tolist()):
            i, j = divmod(key, n)
            adj[i][j] = [length, count, lock > 0]
            adj[j][i] = adj[i][j]
        parent = list(range(n))
        def root(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i
        heap = [(area[i], i) for i in range(n) if area[i] < thresholds[i]]
        heapq.heapify(heap)
        while heap:
            ar, i = heapq.heappop(heap)
            if root(i) != i or ar != area[i] or ar >= thresholds[i]:
                continue
            j = None
            for dest, (length, count, locked) in sorted(adj[i].items(), key=lambda kv: (-kv[1][0], kv[0])):
                if locked:
                    continue
                new_chi = chi[i] + chi[dest] - (len(vsets[i] & vsets[dest]) - count)
                if new_chi == 1:
                    j = dest
                    break
            if j is None:
                continue
            parent[i] = j
            area[j] += area[i]
            chi[j] = 1
            vsets[j] |= vsets[i]
            vsets[i].clear()
            for dest, values in list(adj[i].items()):
                if dest == j:
                    continue
                value = adj[j].setdefault(dest, [0., 0, False])
                value[0] += values[0]
                value[1] += values[1]
                value[2] |= values[2]
                adj[dest][j] = value
                adj[dest].pop(i, None)
            adj[j].pop(i, None)
            adj[i].clear()
            if area[j] < thresholds[j]:
                heapq.heappush(heap, (area[j], j))
        return np.unique(np.array([root(i) for i in range(n)])[labels], return_inverse=True)[1]

    def smooth_labels(self, labels, iterations):
        """Строгое большинство среди всех edge-neighbours, включая quad/n-gon."""
        src = np.r_[self.fa[self.allowed], self.fb[self.allowed]]
        dst = np.r_[self.fb[self.allowed], self.fa[self.allowed]]
        degree = np.bincount(src, minlength=self.n)
        for _ in range(iterations):
            if not len(src):
                break
            n = int(labels.max()) + 1
            keys, votes = np.unique(src * n + labels[dst], return_counts=True)
            f, c = keys // n, keys % n
            take = (votes > degree[f] / 2) & (votes >= 2) & ~self.frozen[f]
            out = labels.copy()
            out[f[take]] = c[take]
            if np.array_equal(labels, out):
                break
            labels = out
        same = (labels[self.fa] == labels[self.fb]) & self.allowed
        return components(self.n, self.fa[same], self.fb[same])

    def repair(self, labels, fraction, smooth):
        labels = self.merge(labels, fraction)
        labels = self.smooth_labels(labels, smooth)
        for _ in range(8):
            bad = self.disk_errors(labels)
            if not len(bad):
                break
            nxt = int(labels.max()) + 1
            for c in bad:
                idx = np.where(labels == c)[0]
                if len(idx) == 1:
                    continue
                p, w = self.centers[idx], self.areas[idx]
                mu = np.average(p, axis=0, weights=w)
                _, vectors = np.linalg.eigh(((p-mu)*w[:, None]).T @ (p-mu))
                side = (p-mu) @ vectors[:, 1] > 0
                if side.all() or not side.any():
                    side = np.arange(len(idx)) >= len(idx)//2
                labels[idx[side]] = nxt
                nxt += 1
            same = (labels[self.fa] == labels[self.fb]) & self.allowed
            labels = components(self.n, self.fa[same], self.fb[same])
            labels = self.merge(labels, fraction)
        # Предсказуемый последний fallback: отдельные faces вместо ложного успеха.
        bad = self.disk_errors(labels)
        nxt = int(labels.max()) + 1
        for c in bad:
            idx = np.where(labels == c)[0]
            labels[idx] = np.arange(nxt, nxt+len(idx))
            nxt += len(idx)
        labels = np.unique(labels, return_inverse=True)[1]
        if len(self.disk_errors(labels)):
            raise ValueError('Повреждённые полигоны: нельзя построить disk charts')
        seams = np.ones(len(self.ev), bool)
        seams[self.two] = (labels[self.fa] != labels[self.fb]) | self.locked
        return seams, labels

    def uv_labels(self, uv):
        # loop на втором конце ребра — следующий угол данного полигона.
        na = self.ls[self.fa] + (self.la-self.ls[self.fa]+1) % self.lt[self.fa]
        nb = self.ls[self.fb] + (self.lb-self.ls[self.fb]+1) % self.lt[self.fb]
        same_dir = self.lv[self.la] == self.lv[self.lb]
        b0, b1 = np.where(same_dir, self.lb, nb), np.where(same_dir, nb, self.lb)
        same = (np.max(np.abs(uv[self.la]-uv[b0]), axis=1) < 1e-7) & (np.max(np.abs(uv[na]-uv[b1]), axis=1) < 1e-7)
        return components(self.n, self.fa[same], self.fb[same]), same


def overlap_faces(obj, uv_name, return_mask=False):
    """Точная проверка через Blender UV overlap, не сумма площадей атласа."""
    obj.data.uv_layers.active = obj.data.uv_layers[uv_name]
    old = bpy.context.scene.tool_settings.use_uv_select_sync
    bpy.context.scene.tool_settings.use_uv_select_sync = False
    try:
        edit_all(obj)
        bpy.ops.uv.select_all(action='DESELECT')
        bpy.ops.uv.select_overlap(extend=False)
        bm = bmesh.from_edit_mesh(obj.data)
        uv = bm.loops.layers.uv.active
        def selected(loop):
            return loop.uv_select_vert if hasattr(loop, 'uv_select_vert') else loop[uv].select
        mask = np.array([any(selected(loop) for loop in f.loops) for f in bm.faces], dtype=bool)
        return mask if return_mask else int(mask.sum())
    finally:
        bpy.ops.object.mode_set(mode='OBJECT')
        bpy.context.scene.tool_settings.use_uv_select_sync = old


def metrics(obj, name, graph=None, check_overlap=True):
    me = obj.data
    g = graph or Graph(me)
    uv = uv_get(me, name)
    if not np.isfinite(uv).all():
        return {'valid': False, 'reason': 'nonfinite UV'}
    lab, same = g.uv_labels(uv)
    me.calc_loop_triangles()
    loops = array(me.loop_triangles, 'loops', 3, np.int64)
    faces = array(me.loop_triangles, 'polygon_index', dtype=np.int64)
    p, q, r = uv[loops[:, 0]], uv[loops[:, 1]], uv[loops[:, 2]]
    cross = (q[:, 0]-p[:, 0])*(r[:, 1]-p[:, 1]) - (q[:, 1]-p[:, 1])*(r[:, 0]-p[:, 0])
    signed = cross * .5
    ua = np.bincount(faces, weights=np.abs(signed), minlength=g.n)
    sign = np.sign(np.bincount(lab[faces], weights=signed))
    flips = int(((np.sign(signed) != sign[lab[faces]]) & (np.abs(signed) > 1e-14)).sum())
    deg = int((np.abs(signed) <= 1e-14).sum())
    area = np.maximum(g.areas, 1e-30)
    density = np.sqrt(ua/area)
    mean = np.average(density, weights=area)
    cv = np.sqrt(np.average((density-mean)**2, weights=area))/max(mean, 1e-30)
    n = int(lab.max())+1
    lo, hi = np.full((n, 2), np.inf), np.full((n, 2), -np.inf)
    np.minimum.at(lo, lab[g.lf], uv)
    np.maximum.at(hi, lab[g.lf], uv)
    overlap = overlap_faces(obj, name) if check_overlap else None
    bounds = bool((uv >= -1e-6).all() and (uv <= 1+1e-6).all())
    return {'valid': flips == 0 and deg == 0 and overlap == 0 and bounds,
            'islands': n, 'islands_lt_50_faces': int((np.bincount(lab)<50).sum()),
            'flipped_triangles': flips, 'degenerate_uv_triangles': deg, 'overlap_faces': overlap,
            'in_unit_square': bounds, 'uv_area_sum': float(ua.sum()), 'texel_density_cv': float(cv),
            'seam_length': float(g.lengths[g.two[~same]].sum()),
            'min_island_width_uv': float((hi-lo).min())}


def resolution_for_islands(obj, name, resolution, max_resolution, allowance):
    """Бюджет по доле площади поверхности, а не по одному микроскопическому острову.
    allowance=0 возвращает строгую проверку всех островов; default 0.001 = 0.1% площади.
    """
    g = Graph(obj.data)
    uv = uv_get(obj.data,name)
    labels,_ = g.uv_labels(uv)
    n=int(labels.max())+1
    lo,hi=np.full((n,2),np.inf),np.full((n,2),-np.inf)
    np.minimum.at(lo,labels[g.lf],uv)
    np.maximum.at(hi,labels[g.lf],uv)
    widths=(hi-lo).min(axis=1)
    area=np.bincount(labels,weights=g.areas)
    while True:
        fraction=float(area[widths*resolution < 2.].sum()/area.sum())
        if fraction <= allowance or resolution >= max_resolution:
            return resolution,fraction
        resolution=min(max_resolution,resolution*2)



def candidate_specs(g, options):
    # Доля резких рёбер — наблюдаемый признак, а не распознавание типа модели по имени.
    sharp_fraction = float((g.angles >= 35).mean()) if len(g.angles) else 1.
    edge2 = np.median(g.lengths[g.two]**2) if len(g.two) else 1.
    area = np.median(g.shell_area[g.shell])
    # Радиус диффузии порядка 1% sqrt(площади оболочки); это эвристика, QA решает принятие.
    adaptive = int(np.clip(.0002 * area / max(edge2, 1e-30), 1, 600))
    if options.smooth_iters is not None:
        return [(options.smooth_iters, options.angle or 66.)]
    specs = [(0, options.angle or (45. if sharp_fraction > .1 else 66.))]
    if options.profile != 'hard' and (options.profile == 'organic' or sharp_fraction < .1) and adaptive > 1:
        specs.append((adaptive, options.angle or 66.))
    return specs


def unwrap_candidate(obj, target, g, iterations, angle, options, resolution):
    proxy = obj.copy()
    proxy.data = obj.data.copy()
    proxy.name = '__UVRB_proxy'
    bpy.context.scene.collection.objects.link(proxy)
    proxy.modifiers.clear()
    proxy.parent = None
    try:
        if iterations:
            # Замораживаем вершины защищённых границ: smoothing не размывает authored hard edges.
            frozen = set(g.ev[g.two[g.locked]].ravel().tolist())
            group = proxy.vertex_groups.new(name='__UVRB_smooth')
            ids = [i for i in range(len(g.co)) if i not in frozen]
            if ids:
                group.add(ids, 1., 'REPLACE')
                active(proxy)
                m = proxy.modifiers.new('__UVRB_smooth', 'SMOOTH')
                m.factor, m.iterations, m.vertex_group = .5, iterations, group.name
                bpy.ops.object.modifier_apply(modifier=m.name)
        proxy.data.uv_layers.active = proxy.data.uv_layers[target]
        edit_all(proxy)
        bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=0., correct_aspect=False)
        bpy.ops.mesh.mark_seam(clear=True)
        bpy.ops.uv.seams_from_islands(mark_seams=True, mark_sharp=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        seed = array(proxy.data.edges, 'use_seam', dtype=bool)
    finally:
        if bpy.context.object and bpy.context.object.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        data = proxy.data
        bpy.data.objects.remove(proxy, do_unlink=True)
        bpy.data.meshes.remove(data)
    seams, labels = g.repair(g.labels(seed), options.min_island_frac, options.border_smooth)
    obj.data.edges.foreach_set('use_seam', seams)
    obj.data.uv_layers.active = obj.data.uv_layers[target]
    edit_all(obj)
    kwargs = dict(method=options.unwrap, fill_holes=False, correct_aspect=False, margin=0.)
    props = bpy.ops.uv.unwrap.get_rna_type().properties
    for key in ('no_flip', 'use_no_flip'):
        if key in props:
            kwargs[key] = True
    bpy.ops.uv.unwrap(**kwargs)
    bpy.ops.uv.average_islands_scale()
    # Между двумя островами нужен запас для обоих bleed, а не только для одного.
    gap = max(options.margin_px, 2*options.bleed_px+2)
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method='FRACTION',
                           margin=gap/float(resolution), shape_method=options.pack_shape)
    bpy.ops.object.mode_set(mode='OBJECT')
    return seams


def refine_invalid_uv(obj, target, graph, options, resolution):
    """Локальный fallback: изолировать проблемные faces, а не разрешать overlap/flips.
    После разреза весь кандидат вновь проходит те же строгие UV-метрики.
    """
    bad = overlap_faces(obj,target,return_mask=True)
    uv = uv_get(obj.data,target)
    labels,_ = graph.uv_labels(uv)
    obj.data.calc_loop_triangles()
    loops = array(obj.data.loop_triangles,'loops',3,np.int64)
    faces = array(obj.data.loop_triangles,'polygon_index',dtype=np.int64)
    a,b,c = uv[loops[:,0]],uv[loops[:,1]],uv[loops[:,2]]
    signed = (b[:,0]-a[:,0])*(c[:,1]-a[:,1])-(b[:,1]-a[:,1])*(c[:,0]-a[:,0])
    signs = np.sign(np.bincount(labels[faces],weights=signed))
    bad[faces[(np.sign(signed)!=signs[labels[faces]]) | (np.abs(signed)<=2e-14)]] = True
    count=int(bad.sum())
    if not count:
        return array(obj.data.edges,'use_seam',dtype=bool),0
    seams=array(obj.data.edges,'use_seam',dtype=bool)
    seams[graph.le[bad[graph.lf]]] = True
    # Удалённая из диска грань могла оставить отверстие: повторный PCA без обратного merge.
    seams, _ = graph.repair(graph.labels(seams), 0., 0)
    obj.data.edges.foreach_set('use_seam',seams)
    obj.data.uv_layers.active=obj.data.uv_layers[target]
    edit_all(obj)
    kwargs=dict(method=options.unwrap,fill_holes=False,correct_aspect=False,margin=0.)
    if 'no_flip' in bpy.ops.uv.unwrap.get_rna_type().properties:
        kwargs['no_flip']=True
    bpy.ops.uv.unwrap(**kwargs)
    bpy.ops.uv.average_islands_scale()
    gap=max(options.margin_px,2*options.bleed_px+2)
    bpy.ops.uv.pack_islands(rotate=True,scale=True,margin_method='FRACTION',margin=gap/float(resolution),shape_method=options.pack_shape)
    bpy.ops.object.mode_set(mode='OBJECT')
    return seams,count
