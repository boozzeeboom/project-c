"""Rig a Tripo A-pose character by fitting the proven legacy humanoid skeleton.

  blender -b --factory-startup --python-exit-code 1 -P Tools/Blender/CharacterRig/rig_character.py -- ^
      --input  <character.fbx> ^
      --reference <rigged_reference.fbx> ^
      --out    <output.fbx> ^
      --report <report.json> ^
      --renders <dir> ^
      --target-faces 12000

Skeleton layout (names/hierarchy) is taken from the reference rig; body part
positions are measured on the target mesh so the A-pose and the stance match it.
"""
import bpy
import bmesh
import sys
import os
import json
import math
import heapq
import shutil
import numpy as np
from mathutils import Vector, Matrix, Quaternion, kdtree
from mathutils.bvhtree import BVHTree

H_REF = 1.7837            # reference character height (m)
FINGER_TAGS = ("B-thumb", "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky")
SKIP_BONES = ("B-jaw", "B-jaw_end")

_TRIS = None                # cached triangle index list of the target mesh
_BVH = None                 # cached whole-body BVH (parity needs a closed surface)
_DBG = False                # --diag-hand: print the digit trace profiles


# ------------------------------------------------------------------ helpers
def args_after_ddash():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(a, name, default=None):
    if name in a:
        i = a.index(name)
        if i + 1 < len(a):
            return a[i + 1]
    return default


def log(*parts):
    print("[rig]", *parts, flush=True)


def wipe():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    bpy.ops.import_scene.fbx(filepath=path, use_anim=False)


def scene_meshes():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def mesh_arrays(obj):
    me = obj.data
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(obj.matrix_world)
    co = co @ mw[:3, :3].T + mw[:3, 3]
    return co


def mesh_tris(obj):
    me = obj.data
    me.calc_loop_triangles()
    tri = np.empty(len(me.loop_triangles) * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", tri)
    return tri.reshape(-1, 3)


def body_bvh(co, tris):
    verts = [Vector(tuple(x)) for x in co]
    faces = [tuple(int(v) for v in t) for t in tris]
    return BVHTree.FromPolygons(verts, faces, all_triangles=True)


AXES6 = ((1.0, 0.0, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0),
         (0.0, -1.0, 0.0), (0.0, 0.0, 1.0), (0.0, 0.0, -1.0))


def surface_probe(bvh, p, maxd=0.12):
    """(inside, clearance) by ray parity along the 6 world axes.

    Ray based on purpose: this character's mesh has only ~65% of its faces wound
    outward, so any test that trusts a surface normal is unreliable, while crossing
    parity is not. `clearance` is the distance to the nearest wall over those axes and
    is what makes a point sit on a medial axis instead of on the surface.
    """
    odd, clear = 0, maxd
    for ax in AXES6:
        dirv = Vector(ax)
        start = Vector(tuple(float(x) for x in p))
        n, first, acc = 0, None, 0.0
        while acc < maxd:
            hit = bvh.ray_cast(start, dirv, maxd - acc)
            if hit[0] is None:
                break
            t = float(hit[3])
            if first is None:
                first = acc + t
            n += 1
            step = t + 1e-5
            acc += step
            start = start + dirv * step
        if n % 2 == 1:
            odd += 1
        if first is not None and first < clear:
            clear = first
    return odd >= 4, float(clear)


def probe_dir(bvh, base, d, L, step=0.06):
    """(samples outside, summed clearance) for a straight chain of length L from base.
    Maximising clearance is what puts the chain down the finger's medial axis."""
    out, depth = 0, 0.0
    for f in np.arange(0.10, 1.001, step):
        ins, clear = surface_probe(bvh, base + d * (f * L))
        if not ins:
            out += 1
        else:
            depth += min(clear, 0.015)
    return out, depth


def fit_digit(bvh, base, seed, L, cone_deg):
    """Slide one finger chain onto the mesh: search directions in a cone around the
    mapped seed and keep the one that stays inside the surface while sitting deepest.
    The reference hand's finger fan differs per character, so no single global
    similarity can place an individual digit - the thumb is where it hurts most."""
    s = np.array(seed, dtype=float)
    n = np.linalg.norm(s)
    if n < 1e-6 or L < 1e-5:
        return None
    s /= n
    aux = np.array([0.0, 0.0, 1.0]) if abs(s[2]) < 0.9 else np.array([0.0, 1.0, 0.0])
    e1 = np.cross(s, aux)
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(s, e1)
    best = None
    for polar in np.radians(np.arange(0.0, cone_deg + 1e-9, 5.0)):
        azis = [0.0] if polar < 1e-9 else np.radians(np.arange(0.0, 360.0, 20.0))
        for azi in azis:
            d = (math.cos(polar) * s +
                 math.sin(polar) * (math.cos(azi) * e1 + math.sin(azi) * e2))
            d /= np.linalg.norm(d)
            out, depth = probe_dir(bvh, base, d, L)
            key = (out, -depth)
            if best is None or key < best[0]:
                best = (key, d, out, depth)
    if best is None:
        return None
    key, d, out, depth = best
    return {"dir": d, "out": out, "depth": depth}


def digit_march(hv, base, d, Lr, step=0.003, rtip=0.011, minv=5, latch=None):
    """March a tube into the hand, following the slab centroid. Returns (path, counts).

    Used only to find where a digit's axis dies out; kept out of the chain build
    because the slab centroid is biased at a fingertip cap and the path then grazes
    the skin (clearance fell to 1.3 mm a third of the way along the finger).
    """
    p = np.array(base, dtype=float)
    d = np.array(d, dtype=float)
    d = d / np.linalg.norm(d)
    path, cnts = [p.copy()], []
    for _ in range(int(1.6 * Lr / step)):
        rel = hv - p
        s = rel @ d
        lat = rel - np.outer(s, d)
        latl = np.linalg.norm(lat, axis=1)
        near = (s > -0.25 * step) & (s < step) & (latl < rtip)
        c = int(near.sum())
        if c < minv:
            break
        cnts.append(c)
        off = np.zeros(3)
        if latch is None or c < latch:
            off = lat[near].mean(axis=0)
            nv = float(np.linalg.norm(off))
            if nv > 0.5 * step:
                off *= 0.5 * step / nv
        q = p + d * step + off
        dv = q - p
        nd = float(np.linalg.norm(dv))
        if nd < 1e-9:
            break
        d = dv / nd
        p = q
        path.append(p.copy())
    return path, cnts


def geo_descent(g, indptr, nb, i0, maxn=110):
    """Follow the geodesic field downhill from a fingertip: the surface path inwards."""
    path = [int(i0)]
    i = int(i0)
    while len(path) < maxn:
        best, bg = None, g[i]
        for k in range(int(indptr[i]), int(indptr[i + 1])):
            j = int(nb[k])
            if g[j] < bg - 1e-9:
                best, bg = j, g[j]
        if best is None:
            break
        path.append(best)
        i = best
    return path


def hand_geo(co, mask, A, W):
    """Geodesic distance over the hand skin from the wrist, on the cached triangles.

    The fingertips are the only surface points that are far from the wrist *along the
    skin*, so this finds each digit's tip and its proximal direction with no normals
    and no winding - neither is trustworthy on these Tripo meshes (only 64.5 % of the
    faces are wound outward). Returns (hand points, distance per point) or (None, None).
    """
    tris = _TRIS
    if tris is None:
        return None, None, None, None
    idx = np.where(mask)[0]
    if len(idx) < 200:
        return None, None, None, None
    t = tris[mask[tris].all(axis=1)]
    if len(t) < 100:
        return None, None, None, None
    remap = -np.ones(len(co), dtype=np.int64)
    remap[idx] = np.arange(len(idx))
    P = co[idx]
    e = remap[np.concatenate([t[:, [0, 1]], t[:, [1, 2]], t[:, [2, 0]]], axis=0)]
    a, b = e[:, 0], e[:, 1]
    w = np.linalg.norm(P[a] - P[b], axis=1)
    cnt = np.bincount(a, minlength=len(idx))
    indptr = np.concatenate([[0], np.cumsum(cnt)]).astype(np.int64)
    order = np.argsort(a, kind="stable")
    nb, ww = b[order], w[order]
    u = (P - np.array(W, dtype=float)) @ np.array(A, dtype=float)
    g = geo_field(indptr, nb, ww, np.where(u < u.min() + 0.004)[0])
    return P, g, indptr, nb, ww


def geo_field(indptr, nb, ww, seeds):
    """Geodesic distance over a graph (CSR) from a set of seed vertices."""
    n = len(indptr) - 1
    g = np.full(n, 1e9)
    h = []
    for s in seeds:
        g[int(s)] = 0.0
        heapq.heappush(h, (0.0, int(s)))
    while h:
        dcur, i = heapq.heappop(h)
        if dcur > g[i]:
            continue
        for k in range(int(indptr[i]), int(indptr[i + 1])):
            j = int(nb[k])
            nd = dcur + float(ww[k])
            if nd < g[j] - 1e-12:
                g[j] = nd
                heapq.heappush(h, (nd, j))
    return g


def geo_tips(P, g, n=8, sep=0.014, min_g=0.02):
    """Fingertips: the surface points farthest from the wrist along the skin."""
    tips = []
    for i in np.argsort(-g):
        if g[i] > 1e8 or g[i] < min_g:
            break
        p = P[i]
        if all(float(np.linalg.norm(p - q)) > sep for q, _, _ in tips):
            tips.append((p, float(g[i]), int(i)))
            if len(tips) >= n:
                break
    return tips


def skin_dist(bvh, p):
    """Distance from p to the nearest bit of skin - the maximal inscribed sphere radius there.

    This is the distance-transform value, and it is the one quantity that reads the same
    way every rigging tool that skeletonises a mesh reads it: along a finger it is the
    finger's own radius and stays flat, and where the finger merges into the palm it
    steps up to the palm's half-thickness. It needs no normals (find_nearest does not
    care which way the faces are wound, unlike anything that uses a normal), no sampling
    cap, and it cannot be pulled sideways by a neighbouring finger the way a
    cross-section extent can - a sphere inscribed in the finger simply cannot cross a
    touching finger's wall.
    """
    if bvh is None:
        return 0.0
    hit = bvh.find_nearest(Vector(tuple(float(x) for x in p)))
    return float(hit[3]) if hit[0] is not None else 0.0


def digit_from_tip(P, g, path_idx, fracs, e2, label="", target=0.0):
    """Build one digit's bone chain from its own fingertip back to the knuckle.

    The tip is the digit's own geodesic far point and the path inwards is the surface
    route to the wrist, so the chain's shape is measured, not guessed. The only thing
    left to decide is how far down that path the digit's base joint sits, and that is
    `target`: the length of skin the digit spans, taken from the reference character
    (see ref_digit_skin_len). It has to come from there, because nothing local on this
    mesh marks the knuckle:

      * the cross-section width is unusable - the fingers touch, so on the index and the
        thumb the section merges with the neighbouring digit a few mm past the tip
        (`.L index` reads 30 mm at its own fingertip against a 14 mm middle finger), and
        the palm-side width is clipped by the 30 mm sampling cap;
      * the two fingertips' geodesic fields only diverge where the digits are not
        parallel, so their crossing point exists for the middle finger and not for the
        pinky;
      * the geodesic distance from the wrist cannot separate five fingertips that all
        sit within 2 cm of the same value.

    Measuring the reference is not a compromise either: its rig is the layout Unity's
    Humanoid avatar is built against, so its digits' skin lengths are the proportions
    these characters have to be given.

    `target` <= 0 falls back to catching the section widening past its own median on
    both sides, which only works where the fingers do not touch.

    Returns {"nodes": [knuckle, ..tip], "tip_t", "knuckle_t"} or None.
    """
    n = len(path_idx)
    if n < 16:
        return None
    Q = np.array([P[i] for i in path_idx], dtype=float)
    D = np.zeros_like(Q)
    D[1:-1] = Q[2:] - Q[:-2]
    D[0] = Q[1] - Q[0]
    D[-1] = Q[-1] - Q[-2]
    D /= np.maximum(np.linalg.norm(D, axis=1)[:, None], 1e-9)
    arq = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(Q, axis=0), axis=1))])
    need_ext = not (0.01 < target < arq[-1])
    cen = np.zeros_like(Q)
    ext = np.zeros(n)
    for i in range(n):
        rel = P - Q[i]
        s = rel @ D[i]
        lat = rel - np.outer(s, D[i])
        sel = (np.abs(s) < 0.003) & (np.linalg.norm(lat, axis=1) < 0.030)
        if int(sel.sum()) < 8:
            cen[i] = Q[i]
            continue
        cen[i] = Q[i] + lat[sel].mean(axis=0)
        if need_ext:
            u = np.sort(lat[sel] @ e2)      # contiguous strip of skin across the digit
            j0 = int(np.clip(np.searchsorted(u, 0.0), 1, len(u) - 1))
            lo, hi = float(u[j0 - 1]), float(u[j0])
            j = j0 - 2
            while j >= 0 and lo - float(u[j]) <= 0.004:
                lo = float(u[j])
                j -= 1
            j = j0 + 1
            while j < len(u) and float(u[j]) - hi <= 0.004:
                hi = float(u[j])
                j += 1
            ext[i] = hi - lo
    # smooth the medial line over the whole path, then cut it at the knuckle
    for i in range(1, n - 1):
        cen[i] = (cen[i - 1] + cen[i] + cen[i + 1]) / 3.0
    marc = np.concatenate([[0.0],
                           np.cumsum(np.linalg.norm(np.diff(cen, axis=0), axis=1))])
    if _DBG and _BVH is not None:
        rad = np.array([skin_dist(_BVH, cen[i]) for i in range(n)])
        print("\n".join("      RAD%s i=%d arc=%.3f r=%.4f" % (label, i, marc[i], rad[i])
                        for i in range(0, n, 3)))
    # ---- knuckle: `target` of medial line arc back from the fingertip
    k = int(np.searchsorted(marc, target)) if not need_ext else None
    med = 0.0
    if k is None:
        win = [ext[i] for i in range(n) if 0.015 <= arq[i] <= 0.050]
        med = float(np.median(win)) if win else 0.0
        if med > 0.0:
            for i in range(n):
                if arq[i] > 0.050 and ext[i] > 1.5 * med:
                    k = i
                    break
    if k is None or k < 8 or k >= n - 1:
        if _DBG:
            print("   [trace%s] REJECT k=%s target=%.4f med=%.4f" % (label, k, target, med))
        return None
    A = cen[:k + 1]
    arc = marc[:k + 1]
    total = float(arc[-1])
    if total < 0.03:
        return None

    def at(a):
        a = float(np.clip(a, 0.0, total))
        i = int(np.searchsorted(arc, a))
        if i <= 0:
            return A[0].copy()
        if i >= len(arc):
            return A[-1].copy()
        f = (a - arc[i - 1]) / max(arc[i] - arc[i - 1], 1e-9)
        return A[i - 1] + (A[i] - A[i - 1]) * f

    nodes = [at(total * (1.0 - f)) for f in fracs]
    nodes[-1] = Q[0].copy()                      # the tail sits on the fingertip
    if _DBG:
        print("   [trace%s] finger=%.4f knuckle=%d target=%.4f med=%.4f"
              % (label, total, k, target, med))
    return {"nodes": nodes, "tip_t": total, "knuckle_t": 0.0}


def pct(v, q):
    if len(v) == 0:
        return 0.0
    return float(np.percentile(v, q))


# ------------------------------------------------------------------ template
def ref_digit_skin_len(arm, bones, obj, co):
    """Each digit's skin length on the reference character: base joint -> fingertip.

    The straight bone length cannot be transferred, because a finger's base joint sits
    *inside* the palm - a bone chain measured head-to-tail carries the joint's depth
    under the skin into the digit's length. The geodesic distance from the digit's base
    joint to its fingertip is the length of skin the digit actually spans, and that is
    what a new chain is laid out on.

    The .R hand supplies both sides. On the .L hand the thumb and pinky bone heads sit
    1.2-1.7 cm from any surface vertex, so their geodesic seeds land on the wrong side
    of the hand: it reports the .L pinky (0.0862) longer than the .L ring finger
    (0.0818) and HL .L 0.1952 against .R 0.1763. The .R hand returns an ordered set -
    thumb 0.0925, middle 0.0859, index 0.081, ring 0.0754, pinky 0.0677.
    """
    tris = mesh_tris(obj)
    if len(co) < 500 or len(tris) < 500:
        return {}
    e = np.concatenate([tris[:, [0, 1]], tris[:, [1, 2]], tris[:, [2, 0]]], axis=0)
    a, b = e[:, 0], e[:, 1]
    w = np.linalg.norm(co[a] - co[b], axis=1)
    cnt = np.bincount(a, minlength=len(co))
    indptr = np.concatenate([[0], np.cumsum(cnt)]).astype(np.int64)
    order = np.argsort(a, kind="stable")
    nb, ww = b[order], w[order]
    tree = kdtree.KDTree(len(co))
    for i in range(len(co)):
        tree.insert(Vector(tuple(co[i])), i)
    tree.balance()
    mw = np.array(arm.matrix_world)

    def at(name, tail=False):
        v = bones[name]["tail"] if tail else bones[name]["head"]
        p = np.array(v, dtype=float) @ mw[:3, :3].T + mw[:3, 3]
        _, i, _ = tree.find(Vector(tuple(p)))
        return int(i)

    if "B-hand.R" not in bones:
        return {}
    g = geo_field(indptr, nb, ww, [at("B-hand.R")])
    out = {}
    for tag in FINGER_TAGS:
        dn = sorted(n for n in bones if n.startswith(tag) and n.endswith(".R"))
        if not dn:
            continue
        span = float(g[at(dn[-1], tail=True)]) - float(g[at(dn[0])])
        if 0.005 < span < 0.30:
            out[tag] = round(span, 4)
    return out


def load_template(path):
    wipe()
    import_fbx(path)
    arm = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
    bones = {}
    for b in arm.data.bones:
        bones[b.name] = {
            "parent": b.parent.name if b.parent else None,
            "head": list(b.head_local),
            "tail": list(b.tail_local),
            "matrix": [list(r) for r in b.matrix_local],
            "z_axis": list(b.matrix_local.to_3x3().col[2]),
        }
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    ms = mesh_arrays(meshes[0]) if meshes else None
    tmpl_height = float(ms[:, 2].max() - ms[:, 2].min()) if ms is not None else H_REF
    names = [n for n in bones if n not in SKIP_BONES]
    dl = ref_digit_skin_len(arm, bones, meshes[0], ms) if ms is not None else {}
    log("template bones:", len(names), "height:", round(tmpl_height, 4),
        "digit skin:", dl)
    return {"bones": bones, "height": tmpl_height, "order": names, "digit_len": dl}


def bmat(bones, name):
    return Matrix(bones[name]["matrix"])


# ------------------------------------------------------------------ target
def prepare_target(path, report):
    wipe()
    import_fbx(path)
    ms = scene_meshes()
    if not ms:
        raise RuntimeError("no mesh in target fbx")
    bpy.ops.object.select_all(action='DESELECT')
    for o in ms:
        o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    if len(ms) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    report["source_verts"] = len(obj.data.vertices)
    report["source_polys"] = len(obj.data.polygons)
    return obj


def orient_and_scale(obj, report):
    co = mesh_arrays(obj)
    lo, hi = co.min(axis=0), co.max(axis=0)
    H = float(hi[2] - lo[2])
    if H <= 0:
        raise RuntimeError("degenerate mesh height")

    # --- forward axis: the horizontal axis with the biggest asymmetry in the
    #     lowest 5% of the body (toes stick out forward)
    low = co[co[:, 2] <= lo[2] + 0.05 * H]
    cx = 0.5 * (lo[0] + hi[0])
    cy = 0.5 * (lo[1] + hi[1])
    score_x = abs(float(low[:, 0].mean()) - cx) / H
    score_y = abs(float(low[:, 1].mean()) - cy) / H
    if score_x >= score_y:
        axis, sign = "x", (1.0 if float(low[:, 0].mean()) > cx else -1.0)
    else:
        axis, sign = "y", (1.0 if float(low[:, 1].mean()) > cy else -1.0)
    fwd = Vector((sign, 0, 0)) if axis == "x" else Vector((0, sign, 0))
    # rotation about Z that maps fwd onto -Y (character front in Blender)
    theta = math.atan2(-fwd.x, -fwd.y)
    obj.data.transform(Matrix.Rotation(theta, 4, 'Z'))
    log("forward axis", axis, "sign", sign, "rotZ", round(math.degrees(theta), 2))
    report["forward"] = {"axis": axis, "sign": sign, "rot_deg": round(math.degrees(theta), 2),
                         "score_x": round(score_x, 4), "score_y": round(score_y, 4)}

    # --- uniform scale to the reference height, feet on z=0, centred
    co = mesh_arrays(obj)
    lo, hi = co.min(axis=0), co.max(axis=0)
    s = H_REF / float(hi[2] - lo[2])
    obj.data.transform(Matrix.Scale(s, 4))
    co = mesh_arrays(obj)
    lo, hi = co.min(axis=0), co.max(axis=0)
    mid_z = 0.5 * (lo[2] + hi[2])
    band = co[np.abs(co[:, 2] - 0.54 * H_REF) < 0.02 * H_REF]
    depth_center = float(np.median(band[:, 1])) if len(band) else 0.0
    lateral_center = 0.5 * (lo[0] + hi[0])
    shift = Vector((-lateral_center, -depth_center, -lo[2]))
    obj.data.transform(Matrix.Translation(shift))
    report["normalize"] = {"scale": round(s, 6), "shift": [round(v, 5) for v in shift],
                           "height_after": H_REF}
    co = mesh_arrays(obj)
    lo, hi = co.min(axis=0), co.max(axis=0)
    report["bbox_after"] = {"min": [round(v, 4) for v in lo], "max": [round(v, 4) for v in hi]}
    return co


# ------------------------------------------------------------------ measuring
def body_depth_at(co, z, half=0.02):
    """depth of the body axis at height z: middle of the front/back silhouette"""
    sel = co[np.abs(co[:, 2] - z) < half]
    if len(sel) < 20:
        return 0.0
    d = sel[:, 1]
    lo_p = float(np.percentile(d, 0.5))
    hi_p = float(np.percentile(d, 99.5))
    return 0.5 * (lo_p + hi_p)


def half_width_at(co, z, half=0.012):
    sel = co[np.abs(co[:, 2] - z) < half]
    if len(sel) == 0:
        return 0.0
    return pct(np.abs(sel[:, 0]), 99.5)


def outer_cluster(values, gap):
    """outermost contiguous cluster of 'values' (array of scalars, any sign)."""
    idx = np.argsort(-values)
    v = values[idx]
    keep = [idx[0]]
    for k in range(1, len(v)):
        if v[k - 1] - v[k] > gap:
            break
        keep.append(idx[k])
    return np.array(keep)


def ref_hand_frame(bones, suffix):
    """anatomical frame of the reference hand: origin at the wrist, Y along the hand, Z = palm normal"""
    hs = "B-hand" + suffix
    if hs not in bones:
        return None
    O = Vector(bones[hs]["head"])
    Y = (Vector(bones[hs]["tail"]) - O).normalized()
    i1 = "B-indexFinger01" + suffix
    p1 = "B-pinky01" + suffix
    if i1 in bones and p1 in bones:
        spread = Vector(bones[p1]["head"]) - Vector(bones[i1]["head"])
    else:
        spread = Vector((0.0, 1.0, 0.0))
    spread = spread - Y * spread.dot(Y)
    if spread.length < 1e-6:
        spread = Vector((0.0, 1.0, 0.0))
        spread = spread - Y * spread.dot(Y)
    Z = Y.cross(spread).normalized()
    X = Y.cross(Z).normalized()
    mid = "B-middleFinger03" + suffix
    ref_len = (Vector(bones[mid]["tail"]) - O).length if mid in bones else 0.1831
    return O, X, Y, Z, ref_len


def fit_hand(co, bones, wrist0, arm_dir, hl0, sgn, report, suffix, digit_len=None):
    """Find the real wrist (narrowest section between sleeve and palm) and the hand axis."""
    W0 = np.array(wrist0, dtype=float)
    A = np.array(arm_dir, dtype=float)
    A /= np.linalg.norm(A)
    rel = co - W0
    u = rel @ A
    perp = rel - np.outer(u, A)
    d = np.linalg.norm(perp, axis=1)
    sel = (u > -0.60 * hl0) & (u < 0.60 * hl0) & (d < 0.60 * hl0)
    if int(sel.sum()) < 400:
        return None
    P = perp[sel]
    U = u[sel]

    # half-width profile around the local centroid, along the arm axis
    step = 0.004
    grid = np.arange(-0.45 * hl0, 0.45 * hl0, step)
    widths = np.full(len(grid), np.nan)
    for i, u0 in enumerate(grid):
        k = np.abs(U - u0) < step * 0.5
        if int(k.sum()) < 20:
            continue
        Q = P[k] - P[k].mean(axis=0)
        widths[i] = float(np.percentile(np.linalg.norm(Q, axis=1), 90))
    if np.all(np.isnan(widths)):
        return None
    u_wrist = float(grid[int(np.nanargmin(widths))])
    u_wrist = float(np.clip(u_wrist, -0.5 * hl0, 0.5 * hl0))
    W = W0 + A * u_wrist

    # hand cloud: distal to the refined wrist
    rel2 = co - W
    u2 = rel2 @ A
    perp2 = rel2 - np.outer(u2, A)
    d2 = np.linalg.norm(perp2, axis=1)
    hand = (u2 > 0.0) & (u2 < 0.45) & (d2 < 0.55 * hl0)
    if int(hand.sum()) < 200:
        return None
    H = rel2[hand]

    dist = np.linalg.norm(H, axis=1)
    hand_len = float(np.percentile(dist, 99.5))
    thr = np.percentile(dist, 99.0)
    far = H[dist >= thr]
    D = far.mean(axis=0)
    if np.linalg.norm(D) < 1e-6:
        return None
    D /= np.linalg.norm(D)

    # palm normal = least-variance axis of the hand, flipped to face the body
    C = H - H.mean(axis=0)
    w, V = np.linalg.eigh(C.T @ C)
    N = V[:, 0] - D * (float(V[:, 0] @ D))
    if np.linalg.norm(N) < 1e-6:
        N = np.array([0.0, 1.0, 0.0])
    N /= np.linalg.norm(N)
    if float(N @ np.array([-sgn, 0.0, 0.0])) < 0:
        N = -N

    # ---- orientation: the PCA normal only gives an axis. The two signs are a
    #      180 deg half-turn about the hand axis, so the wrong one throws the thumb to
    #      the ulnar side (middle/ring/pinky barely move - they are nearly symmetric).
    #      Decide by inside/outside against the surface normals, which the thumb alone
    #      settles; a plain distance score cannot see the difference.
    fr = ref_hand_frame(bones, suffix)
    if fr is None:
        return None
    O_ref, Xr, Yr, Zr, ref_len = fr
    Fr = np.stack([np.array(Xr), np.array(Yr), np.array(Zr)], axis=1)
    Habs = co[hand]
    tree = kdtree.KDTree(len(Habs))
    for i in range(len(Habs)):
        tree.insert(Vector(tuple(Habs[i])), i)
    tree.balance()
    pbvh = _BVH
    names = ["B-hand" + suffix] + [n for n in bones
                                   if n.startswith(FINGER_TAGS) and n.endswith(suffix)]
    cands = []
    for s in (1.0, -1.0):
        Zn = N * s
        Xn = np.cross(D, Zn)
        Xn /= np.linalg.norm(Xn)
        Fn = np.stack([Xn, D, Zn], axis=1)
        Rm = Fn @ Fr.T
        sf = hand_len / max(ref_len, 1e-6)
        tot, cnt, outside = 0.0, 0, 0
        per_bone, per_bone_out = {}, {}
        for n in names:
            h = np.array(bones[n]["head"], dtype=float) - np.array(O_ref)
            t = np.array(bones[n]["tail"], dtype=float) - np.array(O_ref)
            ds, ous = [], []
            for a in (0.0, 0.25, 0.5, 0.75, 1.0):
                p = W + sf * (Rm @ (h + (t - h) * a))
                _, _, dist = tree.find(Vector(tuple(p)))
                if pbvh is not None:
                    ins, _ = surface_probe(pbvh, p)
                else:
                    ins = True
                ds.append(round(float(dist), 4))
                ous.append(0 if ins else 1)
                tot += dist
                cnt += 1
                outside += ous[-1]
            per_bone[n] = ds
            per_bone_out[n] = ous
        cands.append({"sign": s, "Rm": Rm, "sf": sf, "outside": outside,
                      "cost": tot / max(cnt, 1), "per_bone": per_bone,
                      "per_bone_out": per_bone_out})
    best = min(cands, key=lambda c: (c["outside"], c["cost"]))
    score, s, Rfit, sfit, per_bone = best["cost"], best["sign"], best["Rm"], best["sf"], best["per_bone"]
    N = N * s

    # ---- per-digit chains. The reference hand's fan cannot place an individual digit
    #      on this mesh (its .L thumb ends 2.7 cm off the skin, its .L index chain runs
    #      0.1059 against 0.0853 on the .R, every joint gap is 0), so each digit is
    #      measured from its own fingertip: the geodesic far points of the hand.
    digits = {}
    chain = {}
    for tag in FINGER_TAGS:
        dn = sorted(n for n in bones if n.startswith(tag) and n.endswith(suffix))
        if not dn:
            continue
        pts = [W + sfit * (Rfit @ (np.array(bones[dn[0]]["head"], dtype=float) - np.array(O_ref)))]
        for n in dn:
            pts.append(W + sfit * (Rfit @ (np.array(bones[n]["tail"], dtype=float) - np.array(O_ref))))
        chain[tag] = (dn, pts,
                      [float(np.linalg.norm(pts[i + 1] - pts[i])) for i in range(len(dn))])
    HP, HG, hind, hnb, hww = hand_geo(co, hand, A, W)
    e2 = np.cross(D, N)
    e2 = e2 / np.linalg.norm(e2) if np.linalg.norm(e2) > 1e-6 else np.array([1.0, 0.0, 0.0])
    cands_t, tips = [], {}
    if HG is not None and chain:
        cands_t = geo_tips(HP, HG, 8, 0.014, 0.35 * hand_len)
        if _DBG:
            print("   [cands%s] %s" % (suffix, [
                [round(float(v), 3) for v in cp] + [round(cg, 4)] for cp, cg, _i in cands_t]))
            print("   [bases%s] %s" % (suffix, dict(
                (tag, [round(float(v), 3) for v in pts[0]])
                for tag, (dn, pts, lens) in chain.items())))
        pairs = []
        for tag, (dn, pts, lens) in chain.items():
            for ci, (cp, cg, _ix) in enumerate(cands_t):
                pairs.append((min(float(np.linalg.norm(cp - q)) for q in pts), tag, ci))
        pairs.sort()
        got_t, got_c = set(), set()
        for c, tag, ci in pairs:
            if tag in got_t or ci in got_c:
                continue
            tips[tag] = ci
            got_t.add(tag)
            got_c.add(ci)
        if _DBG:
            print("   [tips%s] %s" % (suffix, [
                (tag, ci, [round(float(v), 3) for v in cands_t[ci][0]],
                 round(cands_t[ci][1], 4))
                for tag, ci in tips.items()]))

    if pbvh is not None:
        for tag, (dn, pts, lens) in chain.items():
            L = float(sum(lens))
            if L < 1e-5:
                continue
            base = pts[0]
            seed = (pts[-1] - base) / L
            out_b, _ = probe_dir(pbvh, base, seed, L)
            fd = fit_digit(pbvh, base, seed, L, 65.0 if tag == "B-thumb" else 25.0)
            # only take a searched direction when it actually finds an inside path:
            # the reference fan is already right for most digits, and a thin finger
            # whose bones graze the skin scores badly for every candidate, so the
            # tie-break would otherwise walk the chain off the hand
            if fd is not None and fd["out"] <= 2 and fd["out"] < out_b:
                dseed, out_a = np.array(fd["dir"], dtype=float), fd["out"]
            else:
                dseed, out_a = np.array(seed, dtype=float), out_b
            fracs = [0.0]
            acc = 0.0
            for seg in lens:
                acc += seg
                fracs.append(acc / L)
            nodes, traced, src = pts, 0.0, "mapped"
            if tag in tips:
                pidx = geo_descent(HG, hind, hnb, cands_t[tips[tag]][2])
                # the digit's skin length on the reference, scaled to this hand
                tgt = float((digit_len or {}).get(tag) or 0.0) * sfit
                tr = digit_from_tip(HP, HG, pidx, fracs, e2, label=tag + suffix, target=tgt)
                if tr is not None:
                    nodes, traced, src = tr["nodes"], float(tr["tip_t"]), "geo"
            nd = [float(np.linalg.norm(nodes[i + 1] - nodes[i])) for i in range(len(dn))]
            if min(nd) < 1e-4 or len(nodes) != len(dn) + 1:
                nodes, nd, traced, src = pts, lens, 0.0, "mapped"
            digits[tag] = {"base": [round(float(x), 4) for x in nodes[0]],
                           "nodes": [[round(float(x), 4) for x in p] for p in nodes[1:]],
                           "dir": [round(float(x), 5) for x in dseed],
                           "lens": [round(float(x), 4) for x in nd],
                           "out_before": int(out_b), "out_after": int(out_a),
                           "traced": round(traced, 4), "ref_len": round(L, 4),
                           "source": src,
                           "target": round(float((digit_len or {}).get(tag) or 0.0) * sfit, 4),
                           "tip": [round(float(x), 4) for x in cands_t[tips[tag]][0]]
                           if tag in tips else []}

    out = {"wrist": [round(float(x), 4) for x in W],
           "hand_dir": [round(float(x), 5) for x in D],
           "hand_len": round(hand_len, 4),
           "palm_normal": [round(float(x), 4) for x in N],
           "hand_rot": [round(float(x), 6) for x in Rfit.reshape(-1)],
           "hand_scale": round(float(sfit), 6),
           "fit_score": round(float(score), 5),
           "fit_outside": int(best["outside"]),
           "per_bone": per_bone,
           "per_bone_outside": best["per_bone_out"],
           "digits": digits,
           "orientation_candidates": [
               {"sign": c["sign"], "outside_samples": int(c["outside"]),
                "fit_score": round(float(c["cost"]), 5),
                "per_bone": c["per_bone"], "per_bone_outside": c["per_bone_out"]}
               for c in cands],
           "u_wrist": round(u_wrist, 4),
           "width_min": round(float(np.nanmin(widths)), 4),
           "width_max": round(float(np.nanmax(widths)), 4)}
    report.setdefault("hand_fit", {})[suffix] = out
    return out


def measure(co, report, tmpl=None):
    H = H_REF
    m = {}

    # ---- neck: narrowest lateral half width in the upper body
    zs = np.arange(0.78, 0.93, 0.005) * H
    ws = [(z, half_width_at(co, z)) for z in zs]
    z_neck = min(ws, key=lambda t: t[1])[0]
    z_neck = float(min(max(z_neck, 0.82 * H), 0.90 * H))
    m["z_neck"] = z_neck
    m["neck_half_width"] = half_width_at(co, z_neck)

    # ---- spine landmarks from the reference proportions (same height)
    m["z_hip_joint"] = 0.5404 * H
    m["z_spine"] = 0.5960 * H
    m["z_chest"] = 0.7084 * H
    m["z_shoulder"] = 0.8151 * H
    m["z_head"] = max(0.9360 * H, z_neck + 0.06 * H)
    m["z_head_tip"] = min(0.9866 * H, float(co[:, 2].max()) - 0.005 * H)
    m["z_knee"] = 0.2885 * H
    m["z_ankle"] = 0.0720 * H
    m["z_ball"] = 0.0300 * H
    for k in ("z_spine", "z_chest", "z_shoulder", "z_hip_joint"):
        m[k + "_depth"] = body_depth_at(co, m[k])

    # ---- skirt hem: biggest lateral-width drop between 0.15H and 0.45H
    zs = np.arange(0.15, 0.45, 0.005) * H
    ws = np.array([half_width_at(co, z) for z in zs])
    drops = ws[:-1] - ws[1:]
    z_hem = float(zs[int(np.argmax(drops))]) if len(drops) else 0.30 * H
    if not (0.20 * H <= z_hem <= 0.42 * H):
        z_hem = 0.30 * H
    m["z_hem"] = z_hem
    m["hem_drop"] = float(np.max(drops)) if len(drops) else 0.0

    # ---- hand / fingertip = outermost lateral vertex
    later = co[:, 0]
    for side, sgn, suffix in (("L", 1.0, ".L"), ("R", -1.0, ".R")):
        v = later * sgn
        i_tip = int(np.argmax(v))
        tip = co[i_tip]
        m[f"fingertip{suffix}"] = [round(float(x), 4) for x in tip]

        # arm line: outermost cluster per band between 0.48H and 0.68H
        pts = []
        for z in np.arange(0.48, 0.70, 0.01) * H:
            sel = np.where(np.abs(co[:, 2] - z) < 0.008)[0]
            if len(sel) < 30:
                continue
            vals = co[sel, 0] * sgn
            if vals.max() < 0.06 * H:
                continue
            keep = outer_cluster(vals, 0.018 * H)
            cl = co[sel[keep]]
            pts.append([float(cl[:, 0].mean()), float(cl[:, 1].mean()), float(cl[:, 2].mean())])
        pts = np.array(pts)
        if len(pts) < 3:
            raise RuntimeError(f"cannot trace arm {side}")
        # fit x and y against z (line)
        Az = np.stack([pts[:, 2], np.ones(len(pts))], axis=1)
        kx = np.linalg.lstsq(Az, pts[:, 0], rcond=None)[0]
        ky = np.linalg.lstsq(Az, pts[:, 1], rcond=None)[0]

        def arm_at(z):
            return Vector((kx[0] * z + kx[1], ky[0] * z + ky[1], z))

        shoulder = arm_at(m["z_shoulder"])
        # keep the shoulder inside a sane human range
        shoulder.x = float(np.clip(shoulder.x * sgn, 0.050 * H, 0.100 * H) * sgn)
        shoulder.y = 0.5 * (shoulder.y + m["z_shoulder_depth"])
        hand_len = 0.117 * H                       # provisional hand+fingers length
        arm_dir = Vector((float(kx[0]), float(ky[0]), 1.0)).normalized() * -1.0
        # arm_dir now points down-and-outward along the arm
        tipv = Vector((float(tip[0]), float(tip[1]), float(tip[2])))
        wrist = tipv - arm_dir * hand_len

        # refine wrist + hand frame from the mesh (the provisional wrist sits too high:
        # max-X is the knuckles, not the fingertips, and the sleeve hides the wrist)
        fit = fit_hand(co, (tmpl or {}).get("bones", {}), wrist, arm_dir, hand_len, sgn,
                       report, suffix, (tmpl or {}).get("digit_len") or {})
        if fit is not None:
            wrist = Vector(fit["wrist"])
            hand_len = fit["hand_len"]
            m[f"hand_dir{suffix}"] = fit["hand_dir"]
            m[f"palm_normal{suffix}"] = fit["palm_normal"]
            m[f"hand_rot{suffix}"] = fit["hand_rot"]
            m[f"hand_scale{suffix}"] = fit["hand_scale"]
            m[f"digit_fit{suffix}"] = fit.get("digits") or {}
        sp = (wrist - shoulder).length
        elbow = shoulder + arm_dir * (0.588 * sp)
        m[f"shoulder{suffix}"] = [round(v, 4) for v in shoulder]
        m[f"elbow{suffix}"] = [round(v, 4) for v in elbow]
        m[f"wrist{suffix}"] = [round(v, 4) for v in wrist]
        m[f"arm_dir{suffix}"] = [round(v, 5) for v in arm_dir]
        m[f"arm_len{suffix}"] = round(sp, 4)
        m[f"hand_len{suffix}"] = round(hand_len, 4)

    # ---- legs: two clusters below the hem
    z_lo_leg = m["z_ball"] + 0.035 * H
    z_hi_leg = min(0.25 * H, m["z_hem"] - 0.02 * H)
    z_hi_leg = max(z_hi_leg, z_lo_leg + 0.05 * H)
    for side, sgn, suffix in (("L", 1.0, ".L"), ("R", -1.0, ".R")):
        pts = []
        for z in np.arange(z_lo_leg, z_hi_leg, 0.01 * H):
            sel = np.where(np.abs(co[:, 2] - z) < 0.008)[0]
            if len(sel) < 20:
                continue
            sel = sel[co[sel, 0] * sgn > 0]
            if len(sel) < 20:
                continue
            cl = co[sel]
            pts.append([float(cl[:, 0].mean()), float(cl[:, 1].mean()), float(z)])
        pts = np.array(pts)
        if len(pts) < 3:
            raise RuntimeError(f"cannot trace leg {side}")
        Az = np.stack([pts[:, 2], np.ones(len(pts))], axis=1)
        kx = np.linalg.lstsq(Az, pts[:, 0], rcond=None)[0]
        ky = np.linalg.lstsq(Az, pts[:, 1], rcond=None)[0]

        def leg_at(z):
            return Vector((kx[0] * z + kx[1], ky[0] * z + ky[1], z))

        hip = leg_at(m["z_hip_joint"])
        hip.x = float(np.clip(abs(hip.x), 0.035 * H, 0.080 * H)) * sgn
        hip.y = body_depth_at(co, m["z_hip_joint"])
        knee = leg_at(m["z_knee"])
        if m["z_knee"] > m["z_hem"]:            # knee hidden by the skirt
            knee.x = hip.x + (leg_at(m["z_hem"]).x - hip.x) * \
                (m["z_hip_joint"] - m["z_knee"]) / max(m["z_hip_joint"] - m["z_hem"], 1e-4)
        ankle = leg_at(m["z_ankle"])

        # foot: vertices below 0.05H on this side
        foot = co[(co[:, 2] < 0.05 * H) & (co[:, 0] * sgn > 0)]
        if len(foot) < 10:
            foot = co[co[:, 2] < 0.05 * H]
        toe_y = float(np.percentile(foot[:, 1], 1))
        heel_y = float(np.percentile(foot[:, 1], 99))
        ankle.y = float(np.clip(ankle.y, toe_y + 0.02, heel_y - 0.02))
        ball = Vector((ankle.x, ankle.y - 0.45 * (ankle.y - toe_y), m["z_ball"]))
        toe = Vector((ankle.x, toe_y + 0.01, m["z_ball"]))

        m[f"hip{suffix}"] = [round(v, 4) for v in hip]
        m[f"knee{suffix}"] = [round(v, 4) for v in knee]
        m[f"ankle{suffix}"] = [round(v, 4) for v in ankle]
        m[f"ball{suffix}"] = [round(v, 4) for v in ball]
        m[f"toe{suffix}"] = [round(v, 4) for v in toe]
        m[f"foot_len{suffix}"] = round(heel_y - toe_y, 4)
    m["z_toe"] = m["z_ball"]
    report["measure"] = m
    return m


# ------------------------------------------------------------------ hand probe
def _intervals(vals, gap):
    v = np.sort(vals)
    if len(v) == 0:
        return []
    cuts = np.where(np.diff(v) > gap)[0]
    out, start = [], 0
    for c in cuts:
        out.append([float(v[start]), float(v[c])])
        start = c + 1
    out.append([float(v[start]), float(v[-1])])
    return out


def hand_probe(co, m, suffix, report):
    """profile the forearm->hand->fingers region along the arm axis"""
    W = np.array(m["wrist" + suffix], dtype=float)
    A = np.array(m["arm_dir" + suffix], dtype=float)
    A /= np.linalg.norm(A)
    hl = float(m["hand_len" + suffix])

    rel = co - W
    u = rel @ A
    perp = rel - np.outer(u, A)
    d = np.linalg.norm(perp, axis=1)
    sel = (u > -0.28 * hl) & (u < 0.32) & (d < 0.34 * hl)
    P = rel[sel]
    uu = u[sel]
    dd = d[sel]
    info = {"wrist": [round(float(x), 4) for x in W],
            "arm_dir": [round(float(x), 5) for x in A],
            "hand_len": round(hl, 4),
            "n": int(len(P))}
    if len(P) < 300:
        info["error"] = "too few hand vertices"
        report.setdefault("hand_probe", {})[suffix] = info
        return info

    C = P - P.mean(axis=0)
    w, V = np.linalg.eigh(C.T @ C)
    N = V[:, 0] - A * (V[:, 0] @ A)
    if np.linalg.norm(N) < 1e-6:
        N = np.array([0.0, 1.0, 0.0])
    N /= np.linalg.norm(N)
    S = np.cross(A, N)
    S /= np.linalg.norm(S)
    s = P @ S

    info["palm_normal"] = [round(float(x), 4) for x in N]
    info["spread_axis"] = [round(float(x), 4) for x in S]
    info["u_max"] = round(float(uu.max()), 4)
    info["s_range"] = [round(float(s.min()), 4), round(float(s.max()), 4)]

    gap = 0.004
    step = 0.004
    slices = []
    u_start = float(np.ceil(uu.min() / step) * step)
    for u0 in np.arange(u_start, min(float(uu.max()), 0.32), step):
        msk = np.abs(uu - u0) < step * 0.5
        if msk.sum() < 6:
            continue
        dmax = float(np.percentile(dd[msk], 99.0))
        cl = _intervals(s[msk], gap)
        cl = [[round(a, 4), round(b, 4)] for a, b in cl if b - a > 0.003]
        slices.append({"u": round(float(u0), 4), "n": int(msk.sum()),
                       "dmax": round(dmax, 4), "ncl": len(cl), "cl": cl})
    info["slices"] = slices
    report.setdefault("hand_probe", {})[suffix] = info
    return info


# ------------------------------------------------------------------ armature
def rot_from_to(a, b):
    a = Vector(a).normalized()
    b = Vector(b).normalized()
    q = a.rotation_difference(b)
    return q.to_matrix()


def build_armature(tmpl, m, report):
    bones = tmpl["bones"]
    arm_data = bpy.data.armatures.new("B-hips")
    arm = bpy.data.objects.new("B-hips", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones

    created = {}

    def place(name, head, tail, zref=None):
        b = eb.new(name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        if zref is not None:
            try:
                b.align_roll(Vector(zref))
            except Exception:
                pass
        b.use_deform = True
        created[name] = b
        return b

    # ---------------- spine chain
    H = H_REF
    y_sp = m["z_spine_depth"]
    y_ch = m["z_chest_depth"]
    y_sh = m["z_shoulder_depth"]
    y_neck = body_depth_at_cache.get("neck", y_ch)
    y_head = body_depth_at_cache.get("head", y_neck)

    place("B-spine", (0, y_sp, m["z_spine"]), (0, y_ch, m["z_chest"]),
          bones["B-spine"]["z_axis"])
    place("B-chest", (0, y_ch, m["z_chest"]), (0, y_sh, m["z_shoulder"]),
          bones["B-chest"]["z_axis"])
    z_neck_head = m["z_shoulder"] + 0.020 * H
    place("B-neck", (0, y_sh, z_neck_head), (0, y_neck, m["z_head"]),
          bones["B-neck"]["z_axis"])
    place("B-head", (0, y_head, m["z_head"]), (0, y_head, m["z_head_tip"]),
          bones["B-head"]["z_axis"])

    # ---------------- arms
    for suffix, sgn in ((".L", 1.0), (".R", -1.0)):
        sh = Vector(m["shoulder" + suffix])
        el = Vector(m["elbow" + suffix])
        wr = Vector(m["wrist" + suffix])
        tip = Vector(m["fingertip" + suffix])
        arm_dir = Vector(m["arm_dir" + suffix])
        hand_len = m["hand_len" + suffix]

        ref_dir = (Vector(bones["B-hand.L" if suffix == ".L" else "B-hand.R"]["tail"]) -
                   Vector(bones["B-shoulder.L" if suffix == ".L" else "B-shoulder.R"]["head"])).normalized()
        R = rot_from_to(ref_dir, (wr - sh).normalized())

        def zb(name):
            return R @ Vector(bones[name]["z_axis"])

        place("B-shoulder" + suffix, (0, y_sh, m["z_shoulder"]), sh, zb("B-shoulder" + suffix))
        place("B-upperArm" + suffix, sh, el, zb("B-upperArm" + suffix))
        place("B-forearm" + suffix, el, wr, zb("B-forearm" + suffix))

        # ---- hand + fingers: similarity fit of the reference hand frame onto the measured one
        hs = "B-hand" + suffix
        O_ref = Vector(bones[hs]["head"])
        O_ref_a = np.array(O_ref, dtype=float)
        O_new = np.array(wr, dtype=float)
        rot = m.get("hand_rot" + suffix)
        if rot:
            Rfit = np.array(rot, dtype=float).reshape(3, 3)
            sfit = float(m.get("hand_scale" + suffix) or 1.0)
        else:
            Rfit = np.eye(3)
            sfit = hand_len / 0.1831
            fr = ref_hand_frame(bones, suffix)
            if fr is not None:
                Rr = rot_from_to(fr[2], (wr - el).normalized())
                Rfit = np.array([[Rr[r][c] for c in range(3)] for r in range(3)], dtype=float)

        def map_pt(p):
            return Vector(tuple(O_new + sfit * (Rfit @ (np.array(p, dtype=float) - O_ref_a))))

        def map_z(z):
            return Vector(tuple(Rfit @ np.array(z, dtype=float)))

        place(hs, map_pt(bones[hs]["head"]), map_pt(bones[hs]["tail"]),
              map_z(bones[hs]["z_axis"]))

        # fingers: per-digit chains fitted to the mesh, falling back to the global map
        digits = m.get("digit_fit" + suffix) or {}
        for tag in FINGER_TAGS:
            dn = sorted(n for n in bones if n.startswith(tag) and n.endswith(suffix))
            if not dn:
                continue
            dfit = digits.get(tag)
            if not dfit:
                for n in dn:
                    place(n, map_pt(bones[n]["head"]), map_pt(bones[n]["tail"]),
                          map_z(bones[n]["z_axis"]))
                continue
            cur = Vector(dfit["base"])
            d = Vector(dfit["dir"]).normalized()
            nds = [cur] + [Vector(p) for p in (dfit.get("nodes") or [])]
            if len(nds) >= len(dn) + 1:
                for i, n in enumerate(dn):
                    place(n, tuple(nds[i]), tuple(nds[i + 1]), map_z(bones[n]["z_axis"]))
                continue
            for i, n in enumerate(dn):
                nxt = cur + d * float(dfit["lens"][i])
                place(n, tuple(cur), tuple(nxt), map_z(bones[n]["z_axis"]))
                cur = nxt

    # ---------------- legs
    for suffix, sgn in ((".L", 1.0), (".R", -1.0)):
        hip = Vector(m["hip" + suffix])
        knee = Vector(m["knee" + suffix])
        ankle = Vector(m["ankle" + suffix])
        ball = Vector(m["ball" + suffix])
        toe = Vector(m["toe" + suffix])
        ref_dir = (Vector(bones["B-shin.L" if suffix == ".L" else "B-shin.R"]["tail"]) -
                   Vector(bones["B-thigh.L" if suffix == ".L" else "B-thigh.R"]["head"])).normalized()
        R = rot_from_to(ref_dir, (ankle - hip).normalized())

        def zb(name):
            return R @ Vector(bones[name]["z_axis"])

        place("B-thigh" + suffix, hip, knee, zb("B-thigh" + suffix))
        place("B-shin" + suffix, knee, ankle, zb("B-shin" + suffix))
        place("B-foot" + suffix, ankle, ball, zb("B-foot" + suffix))
        place("B-toe" + suffix, ball, toe, zb("B-toe" + suffix))

    # ---------------- parents (reference hierarchy, minus skipped bones)
    for name, b in created.items():
        p = bones[name]["parent"]
        while p in SKIP_BONES:
            p = bones[p]["parent"]
        if p and p in created:
            b.parent = created[p]
            b.use_connect = False

    bpy.ops.object.mode_set(mode='OBJECT')
    report["bones_created"] = len(created)
    return arm


body_depth_at_cache = {}


# ------------------------------------------------------------------ binding
def bind(mesh_obj, arm, smooth=2):
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    ok = True
    try:
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    except Exception as e:
        ok = False
        log("auto weights failed:", e)
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    if smooth > 0:
        try:
            bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
            bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.5,
                                              repeat=smooth, expand=0.0)
            bpy.ops.object.mode_set(mode='OBJECT')
            log("weights smoothed, repeat=", smooth)
        except Exception as e:
            log("weight smooth failed", e)
            try:
                bpy.ops.object.mode_set(mode='OBJECT')
            except Exception:
                pass
    for op in ("limit", "normalize"):
        try:
            if op == "limit":
                bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
            else:
                bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',
                                                          lock_active=False)
        except Exception as e:
            log("weight op failed", op, e)
    return ok


def weight_stats(mesh_obj):
    me = mesh_obj.data
    total = np.zeros(len(me.vertices))
    for v in me.vertices:
        s = 0.0
        for g in v.groups:
            s += g.weight
        total[v.index] = s
    return {"verts": int(len(total)),
            "unweighted": int((total < 1e-5).sum()),
            "sum_min": round(float(total.min()), 4),
            "sum_max": round(float(total.max()), 4)}


# ------------------------------------------------------------------ render
def setup_render():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 640
    scene.render.resolution_y = 800
    sh = scene.display.shading
    sh.light = 'FLAT'
    sh.color_type = 'OBJECT'
    sh.show_object_outline = True
    try:
        sh.show_xray = False
    except Exception:
        pass
    cam = bpy.data.objects.get("RigCam")
    if cam is None:
        cd = bpy.data.cameras.new("RigCam")
        cd.type = 'ORTHO'
        cam = bpy.data.objects.new("RigCam", cd)
        scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def visible(objs, flag):
    for o in objs:
        o.hide_render = not flag


def shoot(path, center, ortho_scale, direction, res=(640, 800), up=None):
    cam = setup_render()
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = res
    cam.location = Vector(center) + Vector(direction).normalized() * 8.0
    if up is None:
        cam.rotation_euler = (Vector(center) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    else:
        # an explicit up vector: to_track_quat only takes an axis name, and every ortho
        # view of the hand needs its own (the hand is at an arbitrary angle in world space,
        # so a world-Y up turns a hand close-up into a smear)
        fwd = (Vector(center) - cam.location).normalized()
        right = fwd.cross(Vector(up).normalized())
        right.normalize()
        cam.rotation_euler = Matrix((right, right.cross(fwd), -fwd)).transposed().to_euler()
    cam.data.ortho_scale = ortho_scale
    bpy.context.scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)
    log("render", os.path.basename(path))


def body_center():
    return Vector((0, 0, H_REF * 0.5))


def bone_debug_mesh(arm, color=(1.0, 0.1, 0.1, 1.0)):
    """thin octahedron-ish sticks so we can see the skeleton in a render"""
    objs = []
    mat = bpy.data.materials.get("RigDebugMat") or bpy.data.materials.new("RigDebugMat")
    mat.diffuse_color = color
    for b in arm.data.bones:
        h, t = b.head_local, b.tail_local
        v = t - h
        L = v.length
        if L < 1e-5:
            continue
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.012, radius2=0.004,
                                       depth=L, location=(0, 0, 0))
        o = bpy.context.active_object
        o.rotation_mode = 'QUATERNION'
        o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(v.normalized())
        o.location = h + v * 0.5
        o.color = color
        o.data.materials.append(mat)
        objs.append(o)
    return objs


def hand_snapshot(obj, center, radius):
    """A copy of the mesh with everything outside `radius` of `center` moved off-screen.

    The hand is part of the body mesh, so a close-up of it puts the whole torso behind
    it in the same flat object colour and the silhouette tells nothing. Everything
    outside `radius` is collapsed onto `center` - the boundary triangles then fold
    inside the hand and the fingers read against an empty background, which is what
    makes a digit chain that is too short, or sitting too deep in the palm, visible.
    """
    dup = obj.copy()
    dup.data = obj.data.copy()
    bpy.context.scene.collection.objects.link(dup)
    co = np.empty(len(dup.data.vertices) * 3, dtype=np.float64)
    dup.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(dup.matrix_world)
    p = (np.linalg.inv(mw) @ np.append(np.array(center, dtype=float), 1.0))[:3]
    co[np.linalg.norm(co - p, axis=1) > radius] = p
    dup.data.vertices.foreach_set("co", co.reshape(-1))
    dup.data.update()
    dup.color = obj.color
    return dup


def clear_objects(objs):
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)


def pose_world_rot(arm, bone_name, axis, angle_deg, pivot=None):
    pb = arm.pose.bones[bone_name]
    rest = pb.bone.matrix_local.copy()
    p = Vector(pivot) if pivot is not None else rest.translation.copy()
    R = Matrix.Rotation(math.radians(angle_deg), 4, axis)
    pb.matrix = Matrix.Translation(p) @ R @ Matrix.Translation(-p) @ rest
    bpy.context.view_layer.update()


def check_pose(arm):
    """bend the left arm out of the body so finger deformation is visible"""
    for pb in arm.pose.bones:
        pb.rotation_mode = 'XYZ'
    sh = arm.pose.bones["B-upperArm.L"].bone.matrix_local.translation
    pose_world_rot(arm, "B-upperArm.L", 'Y', -45.0, sh)
    el = arm.pose.bones["B-forearm.L"].bone.matrix_local.translation
    # elbow pivot follows the rotated upper arm
    el_now = arm.matrix_world @ arm.pose.bones["B-forearm.L"].head
    pose_world_rot(arm, "B-forearm.L", 'X', 60.0, el)
    pose_world_rot(arm, "B-thigh.L", 'X', 35.0,
                   arm.pose.bones["B-thigh.L"].bone.matrix_local.translation)
    pose_world_rot(arm, "B-shin.L", 'X', -60.0,
                   arm.pose.bones["B-shin.L"].bone.matrix_local.translation)
    for name, pb in arm.pose.bones.items():
        if name.startswith(("B-indexFinger", "B-middleFinger", "B-ringFinger")):
            pb.rotation_euler = (0.9, 0, 0)
    bpy.context.view_layer.update()
    return el_now


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()


# ------------------------------------------------------------------ export
def export_fbx(path, mesh_obj, arm, src_fbx):
    outdir = os.path.dirname(path)
    os.makedirs(outdir, exist_ok=True)
    base = os.path.splitext(os.path.basename(path))[0]

    # base colour texture next to the new fbx
    src_png = None
    srcdir = os.path.dirname(src_fbx)
    for f in os.listdir(srcdir):
        if f.lower().endswith(".png") and "basecolor" in f.lower():
            src_png = os.path.join(srcdir, f)
            break
    tex_path = None
    if src_png:
        tex_path = os.path.abspath(os.path.join(outdir, base + "_BaseColor.png"))
        shutil.copyfile(src_png, tex_path)
    for img in bpy.data.images:
        if img.source != 'FILE' and img.packed_file is None:
            continue
        try:
            if tex_path:
                img.filepath_raw = tex_path
                img.file_format = 'PNG'
        except Exception:
            pass

    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm

    kwargs = dict(
        filepath=os.path.abspath(path),
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        apply_scale_options='FBX_SCALE_ALL',
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        use_mesh_modifiers=True,
        use_tspace=False,
        path_mode='COPY',
        embed_textures=False,
        bake_anim=False,
        bake_space_transform=True,
    )
    try:
        bpy.ops.export_scene.fbx(**kwargs)
    except TypeError as e:
        log("export fallback:", e)
        for k in ("bake_space_transform", "use_tspace", "mesh_smooth_type",
                  "secondary_bone_axis", "primary_bone_axis"):
            kwargs.pop(k, None)
        bpy.ops.export_scene.fbx(**kwargs)
    return tex_path


# ------------------------------------------------------------------ main
def main():
    a = args_after_ddash()
    src = arg(a, "--input")
    ref = arg(a, "--reference")
    out = arg(a, "--out")
    report_path = arg(a, "--report")
    renders = arg(a, "--renders")
    target_faces = int(arg(a, "--target-faces", "0"))

    report = {"input": src, "reference": ref, "output": out}
    tmpl = load_template(ref)
    report["template"] = {"height": round(tmpl["height"], 4), "bone_count": len(tmpl["bones"])}

    obj = prepare_target(src, report)
    co = orient_and_scale(obj, report)
    tris = mesh_tris(obj)
    globals()["_TRIS"] = tris
    globals()["_BVH"] = body_bvh(co, tris)

    # body depth cache used by the spine
    globals()["_DBG"] = "--diag-hand" in a
    m = measure(co, report, tmpl)
    body_depth_at_cache["neck"] = body_depth_at(co, m["z_neck"])
    body_depth_at_cache["head"] = body_depth_at(co, m["z_head"] + 0.02 * H_REF)
    m["z_neck_depth"] = body_depth_at_cache["neck"]
    m["z_head_depth"] = body_depth_at_cache["head"]

    for sfx in (".L", ".R"):
        fit = (report.get("hand_fit") or {}).get(sfx)
        if fit is not None:
            print("[hand%s] fit_score=%.5f outside=%d palm_normal=%s candidates=%s" %
                  (sfx, fit["fit_score"], fit["fit_outside"], fit["palm_normal"],
                   [(c["sign"], c["outside_samples"], c["fit_score"])
                    for c in fit["orientation_candidates"]]))

    if "--probe" in a:
        for sfx in (".L", ".R"):
            hp = hand_probe(co, m, sfx, report)
            if "slices" in hp:
                print("[probe%s] palm_normal=%s spread=%s u_max=%s s_range=%s" %
                      (sfx, hp["palm_normal"], hp["spread_axis"], hp["u_max"], hp["s_range"]))
                for sl in hp["slices"]:
                    print("    u=%+.3f n=%4d dmax=%.3f ncl=%d %s" %
                          (sl["u"], sl["n"], sl["dmax"], sl["ncl"], sl["cl"]))

    if "--diag-hand" in a:
        dtree = kdtree.KDTree(len(co))
        for i in range(len(co)):
            dtree.insert(Vector(tuple(co[i])), i)
        dtree.balance()
        for sfx in (".L", ".R"):
            fit = (report.get("hand_fit") or {}).get(sfx) or {}
            print("[diag%s] hand_len=%.4f wrist=%s" % (sfx, fit.get("hand_len", 0.0), fit.get("wrist")))
            for tag, dg in (fit.get("digits") or {}).items():
                nds = [dg["base"]] + list(dg.get("nodes") or [])
                ds = []
                for p in nds:
                    _, _, dd = dtree.find(Vector(tuple(p)))
                    ds.append(round(float(dd), 4))
                print("   %-18s %-7s out %s->%s traced=%s ref=%s node_dist=%s len=%s" %
                      (tag, dg.get("source"), dg.get("out_before"), dg.get("out_after"),
                       dg.get("traced"), dg.get("ref_len"), ds, dg.get("lens")))
        sys.exit(0)

    if target_faces > 0 and len(obj.data.polygons) > target_faces:
        rm = len(obj.data.polygons) - target_faces
        mod = obj.modifiers.new("Decimate", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = target_faces / float(len(obj.data.polygons))
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
        report["decimate"] = {"removed": rm, "faces_after": len(obj.data.polygons),
                              "verts_after": len(obj.data.vertices)}
        log("decimated to", len(obj.data.polygons), "faces")

    arm = build_armature(tmpl, m, report)

    if renders:
        os.makedirs(renders, exist_ok=True)
        obj.color = (0.72, 0.74, 0.80, 1.0)
        sticks = bone_debug_mesh(arm)
        c = body_center()
        visible([obj], True)
        visible(sticks, False)
        shoot(os.path.join(renders, "rest_body_front.png"), c, H_REF * 1.10, (0, -1, 0))
        shoot(os.path.join(renders, "rest_body_side.png"), c, H_REF * 1.10, (1, 0, 0))
        visible([obj], False)
        visible(sticks, True)
        shoot(os.path.join(renders, "rest_bones_front.png"), c, H_REF * 1.10, (0, -1, 0))
        shoot(os.path.join(renders, "rest_bones_side.png"), c, H_REF * 1.10, (1, 0, 0))
        shoot(os.path.join(renders, "rest_bones_hand.png"),
              Vector(m["wrist.L"]) * 0.5 + Vector(m["fingertip.L"]) * 0.5, 0.45, (0, -1, 0),
              res=(640, 640))
        hand_c = Vector(m["wrist.L"]) + Vector(m["hand_dir.L"]) * (0.5 * m["hand_len.L"])
        # back-of-hand: the knuckle line spreads across the frame and every finger shows
        # its length, so this is the view in which a digit chain that is too short or
        # sitting too deep in the palm is visible. Seen against an isolated copy of the
        # hand - the palm faces the body, and the torso behind it is the same flat
        # object colour, so a plain close-up has no readable silhouette.
        back = -Vector(m["palm_normal.L"])
        hand_dup = hand_snapshot(obj, hand_c, 0.26)
        near = [s for s in sticks if (s.location - hand_c).length < 0.35]
        visible([s for s in sticks if s not in near], False)
        visible([hand_dup], True)
        down = -Vector(m["hand_dir.L"])
        shoot(os.path.join(renders, "rest_bones_hand_back.png"), hand_c, 0.22, back,
              res=(640, 640), up=down)
        shoot(os.path.join(renders, "rest_bones_hand_palm.png"), hand_c, 0.22,
              Vector(m["palm_normal.L"]), res=(640, 640), up=down)
        clear_objects([hand_dup])
        visible(sticks, True)
        visible([obj], True)
        shoot(os.path.join(renders, "rest_body_hand.png"),
              Vector(m["wrist.L"]) * 0.5 + Vector(m["fingertip.L"]) * 0.5, 0.45, (0, -1, 0),
              res=(640, 640))
        clear_objects(sticks)
        visible([obj], True)

    bind(obj, arm, smooth=int(arg(a, "--smooth-weights", "2")))
    report["weights"] = weight_stats(obj)

    if renders:
        el_now = check_pose(arm)
        c = body_center()
        shoot(os.path.join(renders, "posed_body_front.png"), c, H_REF * 1.10, (0, -1, 0))
        shoot(os.path.join(renders, "posed_body_side.png"), c, H_REF * 1.10, (1, 0, 0))
        hc = arm.matrix_world @ arm.pose.bones["B-hand.L"].head
        shoot(os.path.join(renders, "posed_hand.png"), hc, 0.40, (0, -1, 0), res=(640, 640))
        shoot(os.path.join(renders, "posed_elbow.png"), el_now, 0.55, (0, -1, 0), res=(640, 640))
        reset_pose(arm)

    tex = export_fbx(out, obj, arm, src)
    report["texture"] = tex

    if report_path:
        os.makedirs(os.path.dirname(report_path), exist_ok=True)
        with open(report_path, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=1, ensure_ascii=False)
    print("RIG_DONE", json.dumps({"faces": len(obj.data.polygons),
                                  "weights": report["weights"]}))


main()
