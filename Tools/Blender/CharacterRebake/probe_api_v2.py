# -*- coding: utf-8 -*-
"""T-CHAR-UVRB: read-only проверка API установленного Blender."""
import bpy, json
ops = [bpy.ops.uv.unwrap, bpy.ops.uv.pack_islands, bpy.ops.uv.select_overlap,
       bpy.ops.object.bake, bpy.ops.export_scene.gltf, bpy.ops.export_scene.fbx]
for op in ops:
    rna = op.get_rna_type()
    print(rna.identifier, json.dumps({p.identifier: ([i.identifier for i in p.enum_items] if p.type == 'ENUM' else p.type)
          for p in rna.properties if p.identifier in ('method','margin_method','shape_method','normal_space','normal_r','normal_g','normal_b','uv_layer','export_animations','export_skins','export_morph','export_apply','export_texcoords','export_materials','use_mesh_modifiers','use_custom_props','bake_anim')}, ensure_ascii=False))
mat = bpy.data.materials.new('Probe'); mat.use_nodes = True
p = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
print('PRINCIPLED', [(s.name, s.type) for s in p.inputs])
print('BLENDER', bpy.app.version_string)
