# -*- coding: utf-8 -*-
"""T-CHAR-UVRB v2: инварианты и native round-trip; не подменяет визуальное принятие."""
import hashlib
import json
import bpy
import numpy as np
from uvrb_geometry import array, uv_get


def digest(value):
    if isinstance(value, np.ndarray):
        return hashlib.sha256(value.tobytes()).hexdigest()
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False, default=str).encode('utf8')).hexdigest()


def animation_snapshot():
    records = {}
    for action in bpy.data.actions:
        curves = list(action.fcurves) if hasattr(action, 'fcurves') else []
        if hasattr(action, 'layers'):
            for layer in action.layers:
                for strip in layer.strips:
                    if hasattr(strip, 'channelbags'):
                        for bag in strip.channelbags:
                            curves.extend(bag.fcurves)
        rows = []
        for curve in curves:
            rows.append((curve.data_path, curve.array_index,
                         [(tuple(k.co), tuple(k.handle_left), tuple(k.handle_right), k.interpolation) for k in curve.keyframe_points]))
        records[action.name] = digest(rows)
    return records


def snapshot(original_uvs=None):
    result = {'objects': {}, 'actions': animation_snapshot()}
    for obj in sorted(bpy.context.scene.objects, key=lambda o:o.name):
        row = {'type': obj.type, 'parent': obj.parent.name if obj.parent else None,
               'parent_type': obj.parent_type, 'parent_bone': obj.parent_bone,
               'matrix_basis': [list(r) for r in obj.matrix_basis],
               'matrix_parent_inverse': [list(r) for r in obj.matrix_parent_inverse],
               'visibility': [obj.hide_render, obj.hide_viewport],
               'custom_properties': digest({k: obj[k] for k in obj.keys()}),
               'modifiers': [(m.name, m.type, getattr(getattr(m, 'object', None), 'name', None)) for m in obj.modifiers],
               'constraints': [(c.name, c.type, getattr(getattr(c, 'target', None), 'name', None)) for c in obj.constraints],
               'action': getattr(getattr(getattr(obj, 'animation_data', None), 'action', None), 'name', None)}
        if obj.type == 'MESH':
            me = obj.data
            names = original_uvs.get(obj.name, []) if original_uvs is not None else [u.name for u in me.uv_layers]
            row.update(vertices=digest(array(me.vertices, 'co', 3)),
                       topology=digest(array(me.loops, 'vertex_index', dtype=np.int64)),
                       polygons=digest(array(me.polygons, 'loop_total', dtype=np.int64)),
                       material_indices=digest(array(me.polygons, 'material_index', dtype=np.int64)),
                       slots=len(obj.material_slots),
                       smooth=digest(array(me.polygons, 'use_smooth', dtype=bool)),
                       sharp=digest(array(me.edges, 'use_edge_sharp', dtype=bool)),
                       uv={n: digest(uv_get(me, n)) for n in names},
                       weights=digest([[(g.group, g.weight) for g in v.groups] for v in me.vertices]),
                       groups=[g.name for g in obj.vertex_groups],
                       shape_keys={}, colors={})
            for c in me.color_attributes:
                row['colors'][c.name] = digest(array(c.data, 'color', 4))
            if me.shape_keys:
                row['shape_keys'] = {k.name: [digest(array(k.data, 'co', 3)), k.value, k.slider_min, k.slider_max,
                                               k.relative_key.name, k.vertex_group] for k in me.shape_keys.key_blocks}
                row['shape_action'] = getattr(getattr(getattr(me.shape_keys, 'animation_data', None), 'action', None), 'name', None)
        elif obj.type == 'ARMATURE':
            row['bones'] = [(b.name, b.parent.name if b.parent else None, [list(r) for r in b.matrix_local], b.length, b.use_deform) for b in obj.data.bones]
            row['pose'] = [(b.name, [list(r) for r in b.matrix_basis]) for b in obj.pose.bones]
        result['objects'][obj.name] = row
    return result


def compare(before, after):
    errors = []
    if before['actions'] != after['actions']:
        errors.append('action/keyframe data changed')
    if set(before['objects']) != set(after['objects']):
        errors.append('object names/count changed')
    for name in before['objects'].keys() & after['objects'].keys():
        a, b = before['objects'][name], after['objects'][name]
        for key in a.keys() | b.keys():
            if a.get(key) != b.get(key):
                errors.append(name+': '+key)
    return {'ok': not errors, 'errors': errors}


def material_snapshot():
    rows = {}
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        values = []
        for slot in obj.material_slots:
            mat = slot.material
            if not mat or not mat.node_tree:
                values.append(None)
                continue
            values.append({'name': mat.name,
                'nodes': [(n.name, n.type, getattr(n, 'uv_map', None),
                          [(s.name, tuple(s.default_value) if hasattr(s.default_value, '__len__') else s.default_value)
                           for s in n.inputs if hasattr(s, 'default_value') and not s.is_linked]) for n in mat.node_tree.nodes],
                'links': [(l.from_node.name, l.from_socket.identifier, l.to_node.name, l.to_socket.identifier) for l in mat.node_tree.links],
                'images': [(n.image.name, list(n.image.size), n.image.colorspace_settings.name, n.image.has_data)
                           for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]})
        rows[obj.name] = values
    return digest(rows)
