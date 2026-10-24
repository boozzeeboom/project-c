# Диагностика сгенерированного персонажа (read-only).
# Запуск: blender.exe -b --factory-startup -P probe_mesh.py -- <path.fbx|.glb>
import bpy, bmesh, sys, os, json, time

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
src = argv[0]

bpy.ops.wm.read_factory_settings(use_empty=True)
ext = os.path.splitext(src)[1].lower()
if ext == ".fbx":
    bpy.ops.import_scene.fbx(filepath=src)
else:
    bpy.ops.import_scene.gltf(filepath=src)


def uv_islands(bm, uv):
    # union-find по углам грани: соседние грани в одном острове, если общее ребро совпадает в UV
    parent = list(range(len(bm.faces)))

    def f(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        fa, fb = e.link_faces
        def uvs(face):
            d = {}
            for l in face.loops:
                d[l.vert.index] = l[uv].uv.copy()
            return d
        ua, ub = uvs(fa), uvs(fb)
        v0, v1 = e.verts[0].index, e.verts[1].index
        if (ua[v0] - ub[v0]).length < 1e-6 and (ua[v1] - ub[v1]).length < 1e-6:
            ra, rb = f(fa.index), f(fb.index)
            if ra != rb:
                parent[ra] = rb
    return len({f(i) for i in range(len(bm.faces))})


out = {"src": src, "objects": []}
for o in bpy.data.objects:
    item = {"name": o.name, "type": o.type, "loc": list(o.location), "rot": list(o.rotation_euler),
            "scale": list(o.scale), "parent": o.parent.name if o.parent else None}
    if o.type == "MESH":
        me = o.data
        item.update({"verts": len(me.vertices), "faces": len(me.polygons), "loops": len(me.loops),
                     "uv_layers": [u.name for u in me.uv_layers],
                     "materials": [m.name if m else None for m in me.materials],
                     "dims": list(o.dimensions)})
        bm = bmesh.new(); bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        uv = bm.loops.layers.uv.active
        t = time.time()
        item["uv_islands_src"] = uv_islands(bm, uv) if uv else 0
        # после слияния вершин по расстоянию
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
        item["verts_after_merge"] = len(bm.verts)
        shells = 0
        seen = set()
        for v in bm.verts:
            if v.index in seen:
                continue
            shells += 1
            stack = [v]
            seen.add(v.index)
            while stack:
                x = stack.pop()
                for e in x.link_edges:
                    w = e.other_vert(x)
                    if w.index not in seen:
                        seen.add(w.index); stack.append(w)
        item["shells_after_merge"] = shells
        item["nonmanifold_edges_after_merge"] = sum(1 for e in bm.edges if not e.is_manifold)
        item["probe_sec"] = round(time.time() - t, 2)
        bm.free()
    if o.type == "ARMATURE":
        item["bones"] = len(o.data.bones)
    out["objects"].append(item)

out["images"] = [(i.name, i.filepath, list(i.size)) for i in bpy.data.images]
for m in bpy.data.materials:
    out.setdefault("mat_nodes", {})[m.name] = [n.bl_idname for n in m.node_tree.nodes] if m.use_nodes else []
print("PROBE_JSON=" + json.dumps(out))
