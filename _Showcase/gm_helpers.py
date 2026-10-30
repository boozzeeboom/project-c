"""Builder for GeneratedModels_Showcase.blend — see Assets/Generated_Models/README.md.

Usage (via Blender MCP execute_blender_code):
    exec(open(r"C:/Projects/ProjectC_client/_Showcase/gm_helpers.py").read())
then call: setup() / import_batch(n) / layout() / finalize() / save()
"""
import bpy, os, math, json
from mathutils import Vector, Matrix

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

S_DOWN = -1.0       # sections stack toward -Y, which reads top-to-bottom in frame

# ---- company formation (section 01).  The FBX characters face +X, so a shelf of
# them reads as a row of profiles; turning them by -90 deg and stacking them into
# ranks is what makes a normal perspective camera produce a parade shot.
CO_FILES = 12       # files (шеренга length) per rank
CO_FILE_GAP = 0.10  # clearance between neighbours inside a rank
CO_RANK_GAP = 0.26  # clearance between ranks (front to back)
CO_STAGGER = 0.5    # odd ranks shift by this fraction of a file pitch
CO_FACE_DEG = -90.0 # +X facing -> -Y, i.e. toward the camera side
CO_BACK_GAP = 6.0   # clear strip of ground between the company and the board
CO_CAPTION = 0.34   # size of the ground caption in front of the company
CO_GROUP_LINE = 0.20
COMPANY_LENS = 50.0

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
    cu.align_y = "TOP"      # text hangs below its anchor, so a label never creeps onto the row above
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


def _coll_objs(key):
    """Every object inside a collection and its children."""
    out = []
    c = bpy.data.collections.get(key)
    if c is None:
        return out
    stack = [c]
    while stack:
        cc = stack.pop()
        out.extend(list(cc.objects))
        stack.extend(list(cc.children))
    return out


def _rot_z_world(objs, deg, pivot):
    R = Matrix.Rotation(math.radians(deg), 4, "Z")
    T = Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot)
    for o in objs:
        o.matrix_world = T @ o.matrix_world


CHILD_TOKENS = ("_Boy_", "_Girl_")
CHILD_HEIGHT = 0.72     # fraction of the 1.0 standard figure height


def scale_children(target=CHILD_HEIGHT):
    """Shrink the _Boy_ / _Girl_ models to a child's height, feet on the ground.

    The generated FBX characters all come in at the same ~1.0 unit height, so the
    kids read as adults next to their guild colleagues.  Each child is scaled
    around the centre of its own footprint so it keeps standing on z = 0.
    """
    n = 0
    for o in _coll_objs("01_CHARACTERS"):
        if o.type != "MESH" or o.get("gm_child"):
            continue
        if not any(t in o.name for t in CHILD_TOKENS):
            continue
        objs = _objs_of(o.name)
        mn, mx, hit = _bounds(objs)
        if not hit:
            continue
        h = mx.z - mn.z
        if h <= 1e-4:
            continue
        s = target / h
        if abs(s - 1.0) < 1e-4:
            o["gm_child"] = True
            continue
        pivot = Vector(((mn.x + mx.x) / 2.0, (mn.y + mx.y) / 2.0, mn.z))
        T = Matrix.Translation(pivot) @ Matrix.Scale(s, 4) @ Matrix.Translation(-pivot)
        for ob in objs:
            ob.matrix_world = T @ ob.matrix_world
        o["gm_child"] = True
        n += 1
    bpy.context.view_layer.update()
    print("CHILDREN  scaled=%d  target_height=%.2f" % (n, target))


def layout_company(cx=0.0, cy=0.0):
    """Line the characters up as a рота: CO_FILES files wide, ranks stacked away from the camera.

    The FBX characters face +X.  Turning each one by CO_FACE_DEG about its own
    centre makes it face -Y, i.e. the camera side, so a plain perspective camera
    gets a frontal parade shot instead of a row of profiles.  Ranks are staggered
    by half a file so the second and third ranks read as rows and not as a wall.
    """
    s01 = next(s for s in build_plan() if s["key"] == "01_CHARACTERS")
    scale_children()
    groups, order = {}, []
    for r in s01["rows"]:
        for e in r["items"]:
            g = e["coll"].split("/")[-1]
            if g not in groups:
                groups[g] = []
                order.append(g)
            groups[g].append(e)

    dims, items = {}, []
    for g in order:
        for e in groups[g]:
            ob = bpy.data.objects.get(e["name"])
            if ob is None:
                continue
            objs = _objs_of(e["name"])
            mn, mx, hit = _bounds(objs)
            if not hit:
                continue
            if not ob.get("gm_faced"):
                ctr = Vector(((mn.x + mx.x) / 2.0, (mn.y + mx.y) / 2.0, 0.0))
                _rot_z_world(objs, CO_FACE_DEG, ctr)
                ob["gm_faced"] = True
                bpy.context.view_layer.update()
                mn, mx, hit = _bounds(objs)
                if not hit:
                    continue
            dims[e["name"]] = (objs, mn, mx, mx.x - mn.x, mx.y - mn.y)
            items.append((g, e))

    if not items:
        print("COMPANY: no characters found")
        return None

    fp = max(dims[e["name"]][3] for _, e in items) + CO_FILE_GAP
    rp = max(dims[e["name"]][4] for _, e in items) + CO_RANK_GAP
    ranks = [items[i:i + CO_FILES] for i in range(0, len(items), CO_FILES)]
    widest = max(len(r) for r in ranks)
    block_w = widest * fp + CO_STAGGER * fp
    block_d = len(ranks) * rp
    x0 = cx - block_w / 2.0
    y_front = cy - block_d / 2.0

    for ri, rank in enumerate(ranks):
        off = CO_STAGGER * fp if ri % 2 else 0.0
        for ci, (g, e) in enumerate(rank):
            objs, mn, mx, w, d = dims[e["name"]]
            tx = x0 + off + ci * fp + fp / 2.0
            ty = y_front + ri * rp + rp / 2.0
            _move(objs, Vector((tx - (mn.x + mx.x) / 2.0,
                                ty - (mn.y + mx.y) / 2.0,
                                -mn.z)))

    lcoll = bpy.data.collections.get("01_CHARACTERS/_LABELS")
    _label("01 · CHARACTERS   %d   ·   РОТА %d × %d" % (len(items), widest, len(ranks)),
           Vector((x0, y_front - 1.55, 0.012)), CO_CAPTION, lcoll)
    line = "   ·   ".join("%s %d" % (g, len(groups[g])) for g in order)
    gs = min(CO_GROUP_LINE, (block_w * 0.98) / max(len(line) * 0.55, 1.0))
    _label(line, Vector((x0, y_front - 1.02, 0.012)), gs, lcoll)

    print("COMPANY  %d figures  %d x %d  pitch=%.3f/%.3f  block=%.2f x %.2f"
          % (len(items), widest, len(ranks), fp, rp, block_w, block_d))
    return {"x0": x0, "x1": x0 + block_w, "y0": y_front, "y1": y_front + block_d,
            "files": widest, "ranks": len(ranks)}


def _shelf_section(s, M, X0, y):
    """Place one shelf section (armour / weapons / gadgets / residents); returns the y after it."""
    lcoll = bpy.data.collections.get(s["key"] + "/_LABELS")
    _label(s["title"], Vector((X0, y, 0.014)), 0.95, lcoll)
    y += S_DOWN * BAND_TITLE

    multi = s["key"] in ("02_ARMOR", "03_WEAPONS")
    rows = [(r["label"] if (multi or i == 0) else "", r["items"])
            for i, r in enumerate(s["rows"])]

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
        y += S_DOWN * BAND_LABEL
        for lab, entries, rw, xs in shelf:
            x = X0 + xs
            yc = y + S_DOWN * depth / 2.0
            for e in entries:
                if e["name"] not in M:
                    continue
                objs, mn, mx, w, d = M[e["name"]]
                _move(objs, Vector((x + w / 2.0 - (mn.x + mx.x) / 2.0,
                                    yc - (mn.y + mx.y) / 2.0,
                                    -mn.z)))
                x += w + GAP
        y += S_DOWN * (depth + SHELF_GAP)
    return y + S_DOWN * SECTION_GAP


def layout():
    """Build the stage: the character company at the origin, the armour board behind it.

    +Y is away from the camera, so the board lands high in an orthographic frame and
    the company stands in the clear ground in front of it, camera side.
    """
    bpy.context.view_layer.update()
    _clear_labels()
    X0 = -SHELF_W / 2.0

    co = layout_company(0.0, 0.0)
    bpy.context.view_layer.update()

    M = _measure_all()
    y = 0.0
    for s in build_plan():
        if s["key"] == "01_CHARACTERS":
            continue
        y = _shelf_section(s, M, X0, y)

    board = []
    for k in ("02_ARMOR", "03_WEAPONS", "04_GADGETS", "05_RESIDENTS"):
        board.extend(_coll_objs(k))
    bpy.context.view_layer.update()          # matrix_world of the moved board must be current
    mn, mx, hit = _bounds(board)
    dy = ((co["y1"] + CO_BACK_GAP) - mn.y) if hit else 0.0
    _move(board, Vector((0.0, dy, 0.0)))
    bpy.context.view_layer.update()

    allobjs = [o for o in bpy.data.objects if o.type in ("MESH", "FONT", "CURVE")
               and o.name != "GROUND"]
    mn, mx, _ = _bounds(allobjs)
    sc = bpy.context.scene
    sc["gm_company"] = [co["x0"], co["x1"], co["y0"], co["y1"]]
    sc["gm_extent"] = [mn.x, mx.x, mn.y, mx.y]
    print("LAYOUT  company=%.1f x %.1f  board_shift=%.1f  scene=%.1f x %.1f  models=%d"
          % (co["x1"] - co["x0"], co["y1"] - co["y0"], dy,
             mx.x - mn.x, mx.y - mn.y, len(M)))


def _ndc_box(sc, cam, corners):
    from bpy_extras.object_utils import world_to_camera_view
    xs, ys = [], []
    for c in corners:
        co = world_to_camera_view(sc, cam, c)
        xs.append(co.x)
        ys.append(co.y)
    return min(xs), max(xs), min(ys), max(ys)


def finalize(target=0.94, objs=None):
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

    objs = objs if objs is not None else [o for o in bpy.data.objects
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


# ---------------------------------------------------------------- company camera

BOARD_KEYS = ("02_ARMOR", "03_WEAPONS", "04_GADGETS", "05_RESIDENTS")


def company_cam(lens=None):
    """Perspective camera for the parade shots (created on first use)."""
    root = bpy.data.collections.get("GeneratedModels")
    cam = bpy.data.objects.get("CAM_Company")
    if cam is None:
        cd = bpy.data.cameras.new("CAM_Company")
        cam = bpy.data.objects.new("CAM_Company", cd)
        _coll("_STAGE", root).objects.link(cam)
    cd = cam.data
    cd.type = "PERSP"
    cd.sensor_fit = "HORIZONTAL"
    cd.sensor_width = 36.0
    cd.lens = lens or COMPANY_LENS
    cd.clip_start = 0.05
    cd.clip_end = 3000.0
    return cam


def _fit_persp(cam, pts, dirv, target=0.93):
    """Back the camera off along `dirv` until the point cloud fills `target` of the frame,
    then pan it until that cloud is centred.  Distance and rotation are solved separately
    so the pan never moves the camera."""
    sc = bpy.context.scene
    cd = cam.data
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    ctr = (mn + mx) / 2.0
    q = dirv.normalized()
    base = (-q).to_track_quat("-Z", "Y")
    aspect = sc.render.resolution_y / float(sc.render.resolution_x)
    tan_h = (cd.sensor_width * 0.5) / cd.lens
    tan_v = tan_h * aspect
    dist = max((mx - mn).length, 1.0) * 1.5
    box = (0.0, 0.0, 0.0, 0.0)

    for _ in range(10):
        cam.location = ctr + q * dist
        cam.rotation_euler = base.to_euler()
        bpy.context.view_layer.update()
        box = _ndc_box(sc, cam, pts)
        dist *= max(box[1] - box[0], box[3] - box[2]) / target
        cam.location = ctr + q * dist
        cam.rotation_euler = base.to_euler()
        bpy.context.view_layer.update()

        quat = base.copy()
        for _ in range(6):
            box = _ndc_box(sc, cam, pts)
            cxn = (box[0] + box[1]) / 2.0 - 0.5
            cyn = (box[2] + box[3]) / 2.0 - 0.5
            if abs(cxn) < 0.0015 and abs(cyn) < 0.0015:
                break
            d = Vector((2.0 * cxn * tan_h, 2.0 * cyn * tan_v, -1.0))
            quat = quat @ Vector((0.0, 0.0, -1.0)).rotation_difference(d)
            cam.rotation_euler = quat.to_euler()
            bpy.context.view_layer.update()

        box = _ndc_box(sc, cam, pts)
        cx = (box[0] + box[1]) / 2.0 - 0.5
        cy = (box[2] + box[3]) / 2.0 - 0.5
        if abs(max(box[1] - box[0], box[3] - box[2]) / target - 1.0) < 0.004 \
                and abs(cx) < 0.002 and abs(cy) < 0.002:
            break
    return box


def _pts_box(objs):
    mn, mx, hit = _bounds(objs)
    if not hit:
        return None
    return [Vector((x, y, z)) for x in (mn.x, mx.x) for y in (mn.y, mx.y) for z in (mn.z, mx.z)]


def render_company(target=0.94, out_dir=r"C:/Projects/ProjectC_client/_Showcase"):
    """Render the company: one overview of the whole block plus the two halves close up.

    The armour board is hidden for these frames, so nothing of section 02-05 sits in
    the background behind the ranks.
    """
    sc = bpy.context.scene
    cam = company_cam()
    sc.camera = cam

    chars = [o for o in _coll_objs("01_CHARACTERS") if o.type == "MESH"]
    labels = [o for o in _coll_objs("01_CHARACTERS/_LABELS") if o.type == "FONT"]
    if not chars:
        print("RENDER COMPANY: no characters in scene")
        return

    def cx_of(o):
        mw = o.matrix_world
        return sum((mw @ Vector(c)).x for c in o.bound_box) / 8.0

    mn, mx, _ = _bounds(chars)
    mid = (mn.x + mx.x) / 2.0
    halves = {"left": [o for o in chars if cx_of(o) < mid],
              "right": [o for o in chars if cx_of(o) >= mid]}

    board = []
    for k in BOARD_KEYS:
        board.extend(_coll_objs(k))
    hidden = [(o, o.hide_render) for o in board]
    for o, _ in hidden:
        o.hide_render = True

    shots = [("company_overview", 2560, 1440, Vector((0.16, -1.0, 0.46)), 0.94, "all", True),
             ("company_right", 1920, 1080, Vector((0.10, -1.0, 0.30)), 0.92, "right", False),
             ("company_left", 1920, 1080, Vector((-0.10, -1.0, 0.30)), 0.92, "left", False)]
    try:
        for name, rx, ry, dirv, tgt, which, show_labels in shots:
            sc.render.resolution_x = rx
            sc.render.resolution_y = ry
            for o in labels:
                o.hide_render = not show_labels
            pts = _pts_box(chars + labels) if which == "all" else _pts_box(halves[which])
            if not pts:
                continue
            box = _fit_persp(cam, pts, dirv, tgt)
            sc.render.filepath = os.path.join(out_dir, name + ".png")
            bpy.ops.render.render(write_still=True)
            print("SHOT %-18s %dx%d  ndc_x=%.3f..%.3f  ndc_y=%.3f..%.3f"
                  % (name, rx, ry, box[0], box[1], box[2], box[3]))
    finally:
        for o, h in hidden:
            o.hide_render = h
        for o in labels:
            o.hide_render = False
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080
    sc.camera = cam


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
