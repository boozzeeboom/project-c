# Эксперимент: число островов Smart UV на исходной геометрии vs на сглаженном прокси.
# Запуск: blender.exe -b --factory-startup -P exp_smart_uv.py -- <fbx> <iters,iters,...> <angle>
import bpy, bmesh, sys, math, time, json

argv = sys.argv[sys.argv.index("--") + 1:]
src, iters_list, angle = argv[0], [int(x) for x in argv[1].split(",")], float(argv[2])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
base = [o for o in bpy.data.objects if o.type == "MESH"][0]


def count_islands(me, uv_name):
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv[uv_name]
    parent = list(range(len(bm.faces)))
    def f(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        fa, fb = e.link_faces
        ok = True
        for v in e.verts:
            la = next(l for l in fa.loops if l.vert is v)
            lb = next(l for l in fb.loops if l.vert is v)
            if (la[uv].uv - lb[uv].uv).length > 1e-6:
                ok = False; break
        if ok:
            ra, rb = f(fa.index), f(fb.index)
            if ra != rb: parent[ra] = rb
    roots = {}
    for i in range(len(bm.faces)):
        r = f(i); roots[r] = roots.get(r, 0) + 1
    bm.free()
    sizes = sorted(roots.values(), reverse=True)
    return {"islands": len(sizes), "tiny_lt50f": sum(1 for s in sizes if s < 50), "top5": sizes[:5]}


res = []
for it in iters_list:
    o = base.copy(); o.data = base.data.copy(); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=1e-5)
    bpy.ops.object.mode_set(mode="OBJECT")
    t = time.time()
    if it > 0:
        m = o.modifiers.new("sm", "SMOOTH"); m.factor = 0.5; m.iterations = it
        bpy.ops.object.modifier_apply(modifier=m.name)
    t_s = time.time() - t
    o.data.uv_layers.new(name="UV_new"); o.data.uv_layers.active = o.data.uv_layers["UV_new"]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    t = time.time()
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=0.0, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    t_uv = time.time() - t
    bpy.ops.object.mode_set(mode="OBJECT")
    r = count_islands(o.data, "UV_new")
    r.update({"smooth_iters": it, "angle": angle, "smooth_sec": round(t_s, 1), "smart_uv_sec": round(t_uv, 1)})
    print("EXP_JSON=" + json.dumps(r), flush=True)
    bpy.data.objects.remove(o)
