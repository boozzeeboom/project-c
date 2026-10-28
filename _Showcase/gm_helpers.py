"""Builder for GeneratedModels_Showcase.blend — see Assets/Generated_Models/README.md.

Usage (via Blender MCP execute_blender_code):
    exec(open(r"C:/Projects/ProjectC_client/_Showcase/gm_helpers.py").read())
then call: setup() / import_batch(n) / layout() / finalize() / save()
"""
import bpy, os, math, json
from mathutils import Vector

BASE = r"C:/Projects/ProjectC_client/Assets/Generated_Models"
OUT_BLEND = r"C:/Projects/ProjectC_client/GeneratedModels_Showcase.blend"

GAP = 0.16          # gap between neighbours inside a row
SHELF_GAP = 0.15    # gap between a shelf of models and the next label band
SECTION_GAP = 2.2   # gap between big sections
BAND_TITLE = 2.10   # reserved band above each section for its title
BAND_LABEL = 1.20   # reserved band above each shelf for pack labels
SHELF_W = 34.0      # max width of one shelf before wrapping
PACK_GAP = 1.1      # gap between packs (armor sets, weapon families) on one shelf
PER_ROW = 1000      # build_plan keeps one row per subgroup; real wrapping happens in layout()

COLL_ORDER = ["Guilds", "Pilots", "Medics", "City", "Winter",
              "CreationChem", "Engineers", "Explorers", "Archetypes"]

SET_ORDER = ["STEEL", "COMPOSITE", "BRONZE", "AGRAV", "MEDICAL", "MEDICAL_FIELD",
             "CHEMICAL", "THOUGHTS_ENGINEER", "RESEARCH", "SUPERHEAVY", "TANK",
             "RANGER", "CHROME", "RUNIC", "KORU"]

SLOT_PREFIXES = ["CL_F_ACC_PAULDRON_", "CL_F_ACC_MANTLE_", "CL_F_AIRTANK_", "CL_F_BACKPACK_",
                 "CL_F_BELT_", "CL_F_CHEST_PLATE_", "CL_F_CHEST_LAMES_", "CL_F_FEET_BOOT_",
                 "CL_F_HANDS_BRACER_", "CL_F_HANDS_GAUNTLET_", "CL_F_HEAD_HELMET_",
                 "CL_F_LEGS_BELT_", "CL_F_LEGS_KNEE_", "CL_F_LEGS_THIGH_"]

SLOT_LABEL = {"ACC_PAULDRON": "плечо", "ACC_MANTLE": "мантия", "AIRTANK": "баллон",
              "BACKPACK": "рюкзак", "BELT": "пояс", "CHEST_PLATE": "нагрудник",
              "CHEST_LAMES": "ламели", "FEET_BOOT": "сапог", "HANDS_BRACER": "наруч",
              "HANDS_GAUNTLET": "рукавица", "HEAD_HELMET": "шлем",
              "LEGS_BELT": "пояс ног", "LEGS_KNEE": "колено", "LEGS_THIGH": "бедро"}

WEAPON_FAMILIES = ["AXE", "BOW", "CANE", "GSWORD", "KNIFE", "PISTOL", "RAPIER", "SWORD", "XBOW"]

# BACKPACK_07_ENGINEER / BELT_07_ENGINEER названы без префикса THOUGHTS, но это тот же сет 07
CODE_ALIAS = {"ENGINEER": "THOUGHTS_ENGINEER"}


# ---------------------------------------------------------------- scanning

def _dirs(p):
    return sorted(d for d in os.listdir(p) if os.path.isdir(os.path.join(p, d)))


def _entry(folder, name):
    fbx = os.path.join(folder, name, name + ".fbx")
    return {"name": name, "fbx": fbx} if os.path.exists(fbx) else None


def _split_set(name):
    rest = name
    slot = ""
    for p in SLOT_PREFIXES:
        if name.startswith(p):
            rest = name[len(p):]
            slot = p[len("CL_F_"):-1]
            break
    tok = rest.split("_")
    num, code = tok[0], "_".join(tok[1:])
    for suf in ("_v2b", "_v3b", "_v2"):
        if code.endswith(suf):
            code = code[: -len(suf)]
    if not code:
        code = "STEEL"          # CL_F_ACC_PAULDRON_01 — единственный без суффикса, сет 01 = STEEL
    code = CODE_ALIAS.get(code, code)
    return slot, num, code


def build_plan():
    """Deterministic plan of sections -> rows -> items. Recomputed on every call."""
    sections = []

    # ---- 01 characters
    cdir = os.path.join(BASE, "01_Characters")
    subs = _dirs(cdir)
    subs = [s for s in COLL_ORDER if s in subs] + [s for s in subs if s not in COLL_ORDER]
    rows = []
    for sub in subs:
        p = os.path.join(cdir, sub)
        items = [e for e in (_entry(p, n) for n in _dirs(p)) if e]
        for it, e in zip(items, items):
            e["coll"] = "01_CHARACTERS/" + sub
        for i in range(0, len(items), PER_ROW):
            rows.append({"label": ("%s  ·  %d" % (sub, len(items))) if i == 0 else "",
                         "items": items[i:i + PER_ROW]})
    n = sum(len(r["items"]) for r in rows)
    sections.append({"key": "01_CHARACTERS", "title": "01 · CHARACTERS   %d" % n, "rows": rows})

    # ---- 02 armor, grouped into sets
    adir = os.path.join(BASE, "02_Armor")
    by_set = {}
    for sub in _dirs(adir):
        p = os.path.join(adir, sub)
        for name in _dirs(p):
            e = _entry(p, name)
            if not e:
                continue
            slot, num, code = _split_set(name)
            e["slot"], e["num"] = slot, num
            by_set.setdefault(code, []).append(e)
    rows = []
    seen = [k for k in SET_ORDER if k in by_set] + [k for k in sorted(by_set) if k not in SET_ORDER]
    for code in seen:
        items = sorted(by_set[code], key=lambda e: (e["slot"], e["num"]))
        for e in items:
            e["coll"] = "02_ARMOR/" + code
        rows.append({"label": "SET %s  ·  %d" % (code.replace("_", " "), len(items)),
                     "items": items})
    n = sum(len(r["items"]) for r in rows)
    sections.append({"key": "02_ARMOR", "title": "02 · ARMOR SETS   %d" % n, "rows": rows})

    # ---- 03 weapons, one row per family
    wdir = os.path.join(BASE, "03_Weapons")
    allw = [e for e in (_entry(wdir, n) for n in _dirs(wdir)) if e]
    rows = []
    for fam in WEAPON_FAMILIES:
        items = [e for e in allw if ("_WPN_" + fam + "_") in e["name"]]
        for e in items:
            e["coll"] = "03_WEAPONS/" + fam
        if items:
            rows.append({"label": "%s  ·  %d" % (fam, len(items)), "items": items})
    n = sum(len(r["items"]) for r in rows)
    sections.append({"key": "03_WEAPONS", "title": "03 · WEAPONS   %d" % n, "rows": rows})

    # ---- 04 gadgets
    gdir = os.path.join(BASE, "04_Gadgets")
    items = [e for e in (_entry(gdir, n) for n in _dirs(gdir)) if e]
    for e in items:
        e["coll"] = "04_GADGETS"
    n = len(items)
    rows = [{"label": "GADGETS", "items": items[i:i + 8]} for i in range(0, n, 8)] if items else []
    sections.append({"key": "04_GADGETS", "title": "04 · GADGETS   %d" % n, "rows": rows})

    # ---- 05 residents (early october)
    rdir = os.path.join(BASE, "05_Residents_EarlyOct")
    items = [e for e in (_entry(rdir, n) for n in _dirs(rdir)) if e]
    for e in items:
        e["coll"] = "05_RESIDENTS"
    n = len(items)
    rows = [{"label": "RESIDENTS  ·  EARLY OCT", "items": items}] if items else []
    sections.append({"key": "05_RESIDENTS", "title": "05 · RESIDENTS (РАННЯЯ ГЕНЕРАЦИЯ, ОКТ)   %d" % n, "rows": rows})

    return sections


def all_items(sections):
    for s in sections:
        for r in s["rows"]:
            for e in r["items"]:
                yield s, r, e


# ---------------------------------------------------------------- scene setup

def _coll(name, parent):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
    if parent is None:
        if c.name not in [x.name for x in bpy.context.scene.collection.children]:
            bpy.context.scene.collection.children.link(c)
    else:
        if c.name not in [x.name for x in parent.children]:
            parent.children.link(c)
    return c


def setup(cur_undo=False):
    bpy.context.preferences.edit.use_global_undo = cur_undo

    # wipe default scene
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)

    root = _coll("GeneratedModels", None)
    for s in build_plan():
        sec = _coll(s["key"], root)
        _coll(s["key"] + "/_LABELS", sec)
        for r in s["rows"]:
            for e in r["items"]:
                parts = e["coll"].split("/")
                parent = sec
                for p in parts[1:]:
                    parent = _coll(s["key"] + "/" + p, parent)

    # ground
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.002))
    ground = bpy.context.object
    ground.name = "GROUND"
    for c in list(ground.users_collection):
        c.objects.unlink(ground)
    _coll("_STAGE", root).objects.link(ground)
    m = bpy.data.materials.get("GroundMat") or bpy.data.materials.new("GroundMat")
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.62, 0.63, 0.60, 1)
        bsdf.inputs["Roughness"].default_value = 0.85
    ground.data.materials.append(m)

    # world
    w = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    bpy.context.scene.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.55, 0.58, 0.62, 1)
        bg.inputs[1].default_value = 1.1

    # sun
    sd = bpy.data.lights.new("Sun", type="SUN")
    sd.energy = 3.2
    sd.angle = math.radians(12)
    sun = bpy.data.objects.new("SUN", sd)
    sun.rotation_euler = (math.radians(48), 0, math.radians(35))
    _coll("_STAGE", root).objects.link(sun)

    # fill
    fd = bpy.data.lights.new("Fill", type="AREA")
    fd.energy = 9000
    fd.size = 90
    fill = bpy.data.objects.new("FILL", fd)
    fill.location = (0, -70, 55)
    fill.rotation_euler = (math.radians(40), 0, 0)
    _coll("_STAGE", root).objects.link(fill)

    # camera
    cd = bpy.data.cameras.new("CAM_Showcase")
    cd.lens = 50
    cam = bpy.data.objects.new("CAM_Showcase", cd)
    _coll("_STAGE", root).objects.link(cam)
    bpy.context.scene.camera = cam

    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except Exception:
        try:
            sc.render.engine = "BLENDER_EEVEE"
        except Exception:
            pass
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080

    tot = sum(len(r["items"]) for s in build_plan() for r in s["rows"])
    print("SETUP OK  planned items =", tot)


# ---------------------------------------------------------------- import

def _link_only(ob, coll):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)


def _coll_for(s, e):
    parts = e["coll"].split("/")
    path = s["key"] + "/" + parts[1] if len(parts) > 1 else s["key"]
    return bpy.data.collections.get(path) or bpy.data.collections.get(s["key"])


def import_batch(limit=40):
    sections = build_plan()
    colls = {}
    done = skip = 0
    for s, r, e in all_items(sections):
        if done >= limit:
            break
        if e["name"] in bpy.data.objects:
            skip += 1
            continue
        coll = _coll_for(s, e)
        if coll is None:
            print("NO COLLECTION", s["key"], e["coll"])
            continue
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=e["fbx"], use_image_search=True)
        new = [o for o in bpy.data.objects if o not in before]
        roots = [o for o in new if o.parent is None or o.parent not in new]
        if not roots:
            print("EMPTY IMPORT", e["name"])
            continue
        for o in new:
            _link_only(o, coll)
        roots[0].name = e["name"]
        done += 1
        if done % 20 == 0:
            print("  imported", done, "of limit", limit)
    total = len(bpy.data.objects)
    print("IMPORT BATCH done=%d skipped=%d objects_in_file=%d" % (done, skip, total))


def import_rest(batch=60):
    """Call repeatedly until it reports remaining=0."""
    sections = build_plan()
    remaining = sum(1 for s, r, e in all_items(sections) if e["name"] not in bpy.data.objects)
    if remaining == 0:
        print("ALL IMPORTED")
        return 0
    import_batch(min(batch, remaining))
    remaining = sum(1 for s, r, e in all_items(sections) if e["name"] not in bpy.data.objects)
    print("remaining =", remaining)
    return remaining


# ---------------------------------------------------------------- layout

def _bounds(objs):
    mn = Vector((1e18, 1e18, 1e18))
    mx = Vector((-1e18, -1e18, -1e18))
    hit = False
    for o in objs:
        if o.type not in ("MESH", "FONT", "CURVE"):
            continue
        if o.type == "MESH" and not o.data.vertices:
            continue
        bb = o.bound_box
        if bb is None:
            continue
        hit = True
        mw = o.matrix_world
        for c in bb:
            w = mw @ Vector(c)
            for i in range(3):
                mn[i] = min(mn[i], w[i])
                mx[i] = max(mx[i], w[i])
    return (mn, mx, hit)


def _move(objs, d):
    roots = [o for o in objs if o.parent is None or o.parent not in objs]
    for o in roots:
        o.location = o.location + d


def _objs_of(name):
    ob = bpy.data.objects.get(name)
    if ob is None:
        return []
    out = [ob]
    stack = list(ob.children)
    while stack:
        ch = stack.pop()
        out.append(ch)
        stack.extend(ch.children)
    return out


def _label(text, loc, size, coll):
    cu = bpy.data.curves.new(type="FONT", name="L_" + text[:24])
    cu.body = text
    cu.size = size
    cu.align_x = "LEFT"
    cu.align_y = "BOTTOM"
    ob = bpy.data.objects.new("LBL_" + text[:32], cu)
    ob.location = loc
    mat = bpy.data.materials.get("LabelMat")
    if mat is None:
        mat = bpy.data.materials.new("LabelMat")
        mat.use_nodes = True
        b = mat.node_tree.nodes.get("Principled BSDF")
        if b:
            b.inputs["Base Color"].default_value = (0.07, 0.08, 0.07, 1)
            b.inputs["Roughness"].default_value = 0.7
    cu.materials.append(mat)
    coll.objects.link(ob)
    return ob


def _measure_all():
    M = {}
    for s, r, e in all_items(build_plan()):
        objs = _objs_of(e["name"])
        if not objs:
            continue
        mn, mx, hit = _bounds(objs)
        if not hit:
            continue
        M[e["name"]] = (objs, mn, mx, max(mx.x - mn.x, 0.04), max(mx.y - mn.y, 0.04))
    return M


def _flow_rows(items, M):
    rows, cur, x = [], [], 0.0
    for e in items:
        m = M.get(e["name"])
        if m is None:
            continue
        if cur and x + m[3] > SHELF_W:
            rows.append(cur)
            cur, x = [], 0.0
        cur.append(e)
        x += m[3] + GAP
    if cur:
        rows.append(cur)
    return rows


def _clear_labels():
    for ob in [o for o in bpy.data.objects if o.name.startswith("LBL_")]:
        bpy.data.objects.remove(ob, do_unlink=True)


def layout():
    """Stack sections downward in world -Y, which reads top-to-bottom in frame.

    +Y is away from the camera so it lands high in the render; going negative
    puts 01 · CHARACTERS at the top of the image and its title directly above
    its own rows — the same for every pack label.
    """
    bpy.context.view_layer.update()
    M = _measure_all()
    _clear_labels()
    S = -1.0                 # layout direction: down the page
    X0 = -SHELF_W / 2.0
    y = 0.0

    for s in build_plan():
        lcoll = bpy.data.collections.get(s["key"] + "/_LABELS")
        _label(s["title"], Vector((X0, y, 0.014)), 0.95, lcoll)
        y += S * BAND_TITLE

        # ---- build labelled rows / packs
        rows = []
        if s["key"] == "01_CHARACTERS":
            groups, order = {}, []
            for r in s["rows"]:
                for e in r["items"]:
                    if e["coll"] not in groups:
                        groups[e["coll"]] = []
                        order.append(e["coll"])
                    groups[e["coll"]].append(e)
            for cn in order:
                items = groups[cn]
                for i, part in enumerate(_flow_rows(items, M)):
                    lab = "%s  ·  %d" % (cn.split("/")[-1], len(items)) if i == 0 else ""
                    rows.append((lab, part))
        elif s["key"] in ("02_ARMOR", "03_WEAPONS"):
            for r in s["rows"]:
                rows.append((r["label"], r["items"]))
        else:
            for i, r in enumerate(s["rows"]):
                rows.append((r["label"] if i == 0 else "", r["items"]))

        # ---- pack the packs into shelves
        shelves, cur, x = [], [], 0.0
        for lab, entries in rows:
            w = sum(M[e["name"]][3] + GAP for e in entries if e["name"] in M)
            rw = max(w - GAP, 0.1)
            if cur and x + rw > SHELF_W:
                shelves.append(cur)
                cur, x = [], 0.0
            cur.append((lab, entries, rw, x))
            x += rw + PACK_GAP
        if cur:
            shelves.append(cur)

        # ---- place the shelves: label band, then the shelf itself
        for shelf in shelves:
            depth = 0.1
            for lab, entries, rw, xs in shelf:
                for e in entries:
                    if e["name"] in M:
                        depth = max(depth, M[e["name"]][4])
            for lab, entries, rw, xs in shelf:
                if lab:
                    avail = rw + PACK_GAP * 0.8
                    ls = min(0.40, avail / max(len(lab) * 0.55, 1.0))
                    _label(lab, Vector((X0 + xs, y, 0.014)), max(ls, 0.22), lcoll)
            y += S * BAND_LABEL
            for lab, entries, rw, xs in shelf:
                x = X0 + xs
                yc = y + S * depth / 2.0
                for e in entries:
                    if e["name"] not in M:
                        continue
                    objs, mn, mx, w, d = M[e["name"]]
                    _move(objs, Vector((x + w / 2.0 - (mn.x + mx.x) / 2.0,
                                        yc - (mn.y + mx.y) / 2.0,
                                        -mn.z)))
                    x += w + GAP
            y += S * (depth + SHELF_GAP)
        y += S * SECTION_GAP

    # ---- centre everything on the origin
    bpy.context.view_layer.update()
    allroot = [o for o in bpy.data.objects
               if o.parent is None and o.name not in ("GROUND", "SUN", "FILL", "CAM_Showcase")
               and o.type in ("MESH", "EMPTY", "ARMATURE", "FONT", "CURVE")]
    mn, mx, _ = _bounds(allroot)
    d = Vector((-(mn.x + mx.x) / 2.0, -(mn.y + mx.y) / 2.0, 0))
    for o in allroot:
        o.location = o.location + d
    bpy.context.scene["gm_extent"] = [X0 + d.x, X0 + SHELF_W + d.x, 0.0 + d.y, y + d.y]
    size = mx - mn
    print("LAYOUT done  models=%.1f x %.1f  extent=%.1f x %.1f  objects=%d"
          % (size.x, size.y, SHELF_W, y, len(M)))


def _ndc_box(sc, cam, corners):
    from bpy_extras.object_utils import world_to_camera_view
    xs, ys = [], []
    for c in corners:
        co = world_to_camera_view(sc, cam, c)
        xs.append(co.x)
        ys.append(co.y)
    return min(xs), max(xs), min(ys), max(ys)


def finalize(target=0.94):
    """Frame the whole board with an orthographic camera, centred on real bounds.

    Orthographic is what makes the stack readable: under perspective at 46 deg the
    last section rendered at a third the size of the first. Elevation 36 deg is
    chosen so the footprint (32.4 x 32.7) projects to roughly 16:9 and the board
    fills the frame instead of leaving side margins.

    The 8 corners of the real content bbox project to the exact NDC bbox of the
    whole arrangement, so fitting to them can neither clip a section nor leave a
    dead band: ortho_scale is solved for the dominant axis, then the camera is
    translated along its own right/up axes until the projected box is centred.
    """
    bpy.context.view_layer.update()
    sc = bpy.context.scene
    cam = bpy.data.objects["CAM_Showcase"]
    sc.camera = cam
    cd = cam.data
    cd.type = "ORTHO"
    cd.clip_start = 0.1
    cd.clip_end = 3000.0
    dirv = Vector((0.03, -1.0, 0.73)).normalized()
    q = (-dirv).to_track_quat("-Z", "Y")
    cam.rotation_euler = q.to_euler()
    right = q @ Vector((1.0, 0.0, 0.0))
    up = q @ Vector((0.0, 1.0, 0.0))
    bpy.context.view_layer.update()

    objs = [o for o in bpy.data.objects
            if o.name not in ("GROUND", "SUN", "FILL", "CAM_Showcase")
            and o.type in ("MESH", "FONT", "CURVE")]
    mn, mx, hit = _bounds(objs)
    if hit:
        pad = 0.25
        x0, x1 = mn.x - pad, mx.x + pad
        y0, y1 = mn.y - pad, mx.y + pad
        z0, z1 = min(mn.z, 0.0), mx.z
    else:
        x0, x1, y0, y1 = list(sc.get("gm_extent", [-1.0, 1.0, -1.0, 1.0]))
        z0, z1 = 0.0, 1.3
    corners = [Vector((x, y, z)) for x in (x0, x1) for y in (y0, y1)
               for z in (z0, z1)]
    center = Vector(((x0 + x1) / 2.0, (y0 + y1) / 2.0, (z0 + z1) / 2.0))

    aspect = sc.render.resolution_y / float(sc.render.resolution_x)
    target_safe = max(min(target, 0.98), 0.20)
    dist = 300.0                                      # irrelevant for ORTHO
    off = Vector((0.0, 0.0, 0.0))
    cd.ortho_scale = max(x1 - x0, (y1 - y0) / aspect) * 1.2
    box = (0.0, 0.0, 0.0, 0.0)

    for _ in range(8):
        cam.location = center + dirv * dist + right * off.x + up * off.y
        bpy.context.view_layer.update()
        box = _ndc_box(sc, cam, corners)
        sx, sy = box[1] - box[0], box[3] - box[2]
        cx = (box[0] + box[1]) / 2.0 - 0.5
        cy = (box[2] + box[3]) / 2.0 - 0.5
        s = max(sx, sy, 1e-6) / target_safe
        done = abs(s - 1.0) < 0.0008 and abs(cx) < 0.0008 and abs(cy) < 0.0008
        if done:
            break
        cd.ortho_scale *= s
        off.x += cx * cd.ortho_scale
        off.y += cy * cd.ortho_scale * aspect

    cam.location = center + dirv * dist + right * off.x + up * off.y
    bpy.context.view_layer.update()
    box = _ndc_box(sc, cam, corners)
    sc.render.filepath = r"C:/Projects/ProjectC_client/_Showcase/showcase.png"
    print("FINALIZE  content=%.1fx%.1fx%.1f  ortho_scale=%.1f  "
          "ndc_x=%.3f..%.3f  ndc_y=%.3f..%.3f"
          % (x1 - x0, y1 - y0, z1 - z0, cd.ortho_scale,
             box[0], box[1], box[2], box[3]))


def save():
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, compress=True)
    print("SAVED", OUT_BLEND, "%.0f MB" % (os.path.getsize(OUT_BLEND) / 1e6))


def report():
    sections = build_plan()
    plan_n = sum(1 for s in sections for r in s["rows"] for e in r["items"])
    have = sum(1 for s, r, e in all_items(sections) if e["name"] in bpy.data.objects)
    print("PLAN=%d  IN_SCENE=%d  OBJECTS=%d" % (plan_n, have, len(bpy.data.objects)))
    for s in sections:
        n = sum(len(r["items"]) for r in s["rows"])
        h = sum(1 for r in s["rows"] for e in r["items"] if e["name"] in bpy.data.objects)
        print("  %-16s %3d/%3d  rows=%d" % (s["key"], h, n, len(s["rows"])))
