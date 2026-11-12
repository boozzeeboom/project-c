"""Перенос готового 40k-рига на парный NPC-меш (12k) 1-в-1.

Скелет не перестраивается: берётся armature из готового
<Name>_Rigged_Humanoid.fbx (40k), NPC-меш нормализуется тем же
orient_and_scale, веса переносятся модификатором DATA_TRANSFER
(POLYINTERP_NEAREST — барицентрика по ближайшему треугольнику 40k-меша),
затем кисти переопределяются тем же детерминированным weight_hands
(перчаточный режим берётся из отчёта 40k-прогона). Экспорт через общий
export_fbx рядом с NPC BaseColor.

Использование (внутри Blender):
    blender --factory-startup -b <blend> --python-exit-code 1 \
        -P transfer_npc_rig.py -- \
        --input40k <rigged40k.fbx> --inputnpc <retopoNPC.fbx> \
        --out <out.fbx> --report <report.json> --renders <dir> \
        --blend <work.blend> --player-report <rig_report40k.json>
"""
import bpy
import json
import math
import os
import sys
import numpy as np
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig_character_v2 as V2

H = V2.H
TAGS = V2.TAGS


def find_meshes():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def join_and_apply(meshes):
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def orient_and_scale_forced(obj, report, rot_deg):
    """Нормализация с заданным поворотом вокруг Z (решение 40k-пайплайна).

    Эвристика оси вперёд на разреженном NPC-меше может расходиться с 40k
    (почти равные score_x/score_y): тогда берём rot_deg из отчёта 40k-прогона,
    NPC и 40k — один персонаж, решение переносится 1-в-1.
    """
    theta = math.radians(rot_deg)
    obj.data.transform(Matrix.Rotation(theta, 4, 'Z'))
    co = H["mesh_arrays"](obj)
    lo, hi = co.min(axis=0), co.max(axis=0)
    s = H["H_REF"] / float(hi[2] - lo[2])
    obj.data.transform(Matrix.Scale(s, 4))
    co = H["mesh_arrays"](obj)
    lo, hi = co.min(axis=0), co.max(axis=0)
    band = co[np.abs(co[:, 2] - 0.54 * H["H_REF"]) < 0.02 * H["H_REF"]]
    depth_center = float(np.median(band[:, 1])) if len(band) else 0.0
    lateral_center = 0.5 * (lo[0] + hi[0])
    shift = Vector((-lateral_center, -depth_center, -lo[2]))
    obj.data.transform(Matrix.Translation(shift))
    report["normalize_forced"] = {"rot_deg": rot_deg, "scale": round(s, 6)}
    return H["mesh_arrays"](obj)


def alignment_stats(npc, src_co, src_tris):
    """Расстояния вершин NPC до поверхности 40k-меша (мир, после нормализации)."""
    from mathutils.bvhtree import BVHTree
    verts = [Vector(tuple(x)) for x in src_co]
    faces = [tuple(int(v) for v in t) for t in src_tris]
    bvh = BVHTree.FromPolygons(verts, faces, all_triangles=True)
    npc_co = H["mesh_arrays"](npc)
    d = np.empty(len(npc_co))
    for i, p in enumerate(npc_co):
        hit = bvh.find_nearest(Vector(tuple(p)))
        d[i] = float(hit[3]) if hit[0] is not None else 9.9
    return {"median": float(np.median(d)), "p95": float(np.percentile(d, 95)),
            "max": float(d.max()), "samples": int(len(d))}


def transfer_weights(npc, src_mesh, arm, src_co, src_tris):
    """Барицентрический перенос весов с ближайшего треугольника 40k-меша.

    Ручная реализация вместо модификатора DATA_TRANSFER: в Blender 5.x
    API модификатора менялся, а здесь всё детерминировано и проверяемо.
    """
    from mathutils.bvhtree import BVHTree
    verts = [Vector(tuple(x)) for x in src_co]
    faces = [tuple(int(v) for v in t) for t in src_tris]
    bvh = BVHTree.FromPolygons(verts, faces, all_triangles=True)
    src_weights = []
    for v in src_mesh.data.vertices:
        src_weights.append([(g.group, float(g.weight)) for g in v.groups if g.weight > 1e-6])
    group_index = {g.name: g.index for g in npc.vertex_groups}

    npc_co = H["mesh_arrays"](npc)
    accumulator = {}
    for vi, point in enumerate(npc_co):
        hit = bvh.find_nearest(Vector(tuple(point)))
        if hit[0] is None:
            continue
        a, b, c = (np.array(verts[i], dtype=float) for i in faces[int(hit[2])])
        loc = np.array(hit[0], dtype=float)
        # Барицентрические координаты точки в треугольнике.
        v0, v1, v2 = b - a, c - a, loc - a
        d00, d01, d11 = v0 @ v0, v0 @ v1, v1 @ v1
        d20, d21 = v2 @ v0, v2 @ v1
        denom = d00 * d11 - d01 * d01
        if abs(denom) < 1e-12:
            bary = (1.0, 0.0, 0.0)
        else:
            u = (d11 * d20 - d01 * d21) / denom
            v = (d00 * d21 - d01 * d20) / denom
            bary = (1.0 - u - v, u, v)
        merged = {}
        for corner, bi in zip(bary, faces[int(hit[2])]):
            if corner <= 0.0:
                continue
            for gi, w in src_weights[bi]:
                merged[gi] = merged.get(gi, 0.0) + corner * w
        if not merged:
            continue
        total = sum(merged.values())
        row = {group_index[src_mesh.vertex_groups[gi].name]: w / total
               for gi, w in merged.items()
               if src_mesh.vertex_groups[gi].name in group_index and w > 1e-8}
        if row:
            accumulator[vi] = row
    for vi, row in accumulator.items():
        for name, w in row.items():
            npc.vertex_groups[name].add([vi], w, 'REPLACE')
    return {"vertices": len(npc_co), "weighted": len(accumulator)}


def bind_to_armature(npc, arm):
    npc.parent = arm
    npc.matrix_parent_inverse = arm.matrix_world.inverted()
    for m in npc.modifiers:
        if m.type == 'ARMATURE':
            m.object = arm
            return
    mod = npc.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm


def validate_weights(mesh, arm, glove_used):
    weights = V2.weight_report(mesh, arm)
    zero_bones = list(weights["zero_weight_bones"])
    if glove_used:
        zero_bones = [name for name in zero_bones
                      if not any(name.startswith(tag) for tag in TAGS)]
    if (weights["unweighted"] or zero_bones or weights["max_influences"] > 4 or
            abs(weights["sum_min"] - 1.0) > 1e-4 or abs(weights["sum_max"] - 1.0) > 1e-4):
        raise RuntimeError("Некорректные веса NPC: " + str(weights))
    return weights


def main():
    args = H["args_after_ddash"]()
    src40k = H["arg"](args, "--input40k", None)
    srcnpc = H["arg"](args, "--inputnpc", None)
    out = H["arg"](args, "--out", None)
    report_path = H["arg"](args, "--report", None)
    renders = H["arg"](args, "--renders", os.path.join(V2.ROOT, "_reports", "catalog_npc", "pilot"))
    blend_path = H["arg"](args, "--blend", os.path.join(V2.ROOT, "_work", "npc_transfer.blend"))
    player_report_path = H["arg"](args, "--player-report", "")
    if not (src40k and srcnpc and out and report_path):
        raise RuntimeError("нужны --input40k --inputnpc --out --report")

    report = {"input40k": src40k, "inputnpc": srcnpc, "output": out,
              "pipeline": "npc_transfer_v1"}
    glove_used = False
    hand_fits = None
    player_rot_deg = None
    if player_report_path and os.path.exists(player_report_path):
        with open(player_report_path, "r", encoding="utf-8") as fh:
            player = json.load(fh)
        hand_fits = player.get("hand_fit")
        if hand_fits:
            glove_used = any(V2.is_glove_fit(hand_fits[s]) for s in (".L", ".R"))
        player_rot_deg = player.get("forward", {}).get("rot_deg")
    report["glove_mode"] = glove_used

    def build(rot_deg=None):
        """Загрузка пары + нормализация NPC; бросает при плохом совмещении."""
        V2.safe_wipe()
        V2.safe_import_fbx(src40k)
        arms = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
        if len(arms) != 1:
            raise RuntimeError("ожидался 1 armature в %s, найдено %d" % (src40k, len(arms)))
        arm_obj = arms[0]
        src = join_and_apply(find_meshes())
        before = set(o.name for o in find_meshes())
        V2.safe_import_fbx(srcnpc)
        new_meshes = [o for o in find_meshes() if o.name not in before]
        if not new_meshes:
            raise RuntimeError("нет меша в NPC fbx: " + srcnpc)
        npc_obj = join_and_apply(new_meshes)
        if rot_deg is None:
            H["orient_and_scale"](npc_obj, report)
        else:
            orient_and_scale_forced(npc_obj, report, rot_deg)
        src_co = H["mesh_arrays"](src)
        src_tris = H["mesh_tris"](src)
        stats = alignment_stats(npc_obj, src_co, src_tris)
        if stats["p95"] > 0.01:
            raise RuntimeError("NPC не совмещён с 40k: " + str(stats))
        return arm_obj, src, npc_obj, src_co, src_tris, stats

    # 1–3. Готовый 40k-риг + нормализованный NPC, выверенное совмещение.
    # Эвристика оси вперёд на разреженном меше может расходиться с 40k
    # (почти равные score) — тогда повторяем с rot_deg из отчёта 40k.
    try:
        arm, src_mesh, npc, src_co, src_tris, alignment = build(None)
    except RuntimeError:
        if player_rot_deg is None:
            raise
        report["forward_fallback"] = {"rot_deg": player_rot_deg}
        arm, src_mesh, npc, src_co, src_tris, alignment = build(player_rot_deg)
    report["source40k_verts"] = len(src_mesh.data.vertices)
    report["source_npc_verts"] = len(npc.data.vertices)
    report["alignment"] = alignment

    # 4. Перенос весов с поверхности 40k-меша.
    for bone in arm.data.bones:
        if bone.name not in npc.vertex_groups:
            npc.vertex_groups.new(name=bone.name)
    report["weight_transfer"] = transfer_weights(npc, src_mesh, arm, src_co, src_tris)
    bpy.data.objects.remove(src_mesh, do_unlink=True)
    bind_to_armature(npc, arm)

    # 5. Кисти: тот же детерминированный скин, режим (пальцы/варежка) как у 40k.
    if hand_fits:
        report["hand_weights"] = V2.weight_hands(npc, arm, hand_fits)
    report["repaired_unweighted"] = V2.repair_unweighted(npc, arm)
    bpy.context.view_layer.objects.active = npc
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    report["weights"] = validate_weights(npc, arm, glove_used)

    # 6. Рендеры для визуальной проверки + проверочная поза.
    if hand_fits:
        measurement = {"hand_fit": hand_fits,
                       "hand_fit.L": hand_fits[".L"], "hand_fit.R": hand_fits[".R"]}
        V2.render_previews(npc, arm, measurement, renders)
    H["check_pose"](arm)
    os.makedirs(renders, exist_ok=True)
    H["shoot"](os.path.join(renders, "posed_body_front.png"),
               Vector((0.0, 0.0, H["H_REF"] * 0.5)), H["H_REF"] * 1.10,
               (0, -1, 0), res=(800, 900))
    V2.reset_pose_full(arm)

    # 7. Экспорт и отчёт.
    npc.parent = arm
    report["texture"] = H["export_fbx"](out, npc, arm, srcnpc)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    report["blend"] = blend_path
    report["status"] = "OK"
    os.makedirs(os.path.dirname(report_path), exist_ok=True)
    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=1)
    summary = {"status": "OK", "output": out, "blend": blend_path,
               "report": report_path,
               "verts": report["source_npc_verts"],
               "alignment": report["alignment"],
               "weights": report["weights"], "glove_mode": glove_used}
    print("NPC_TRANSFER_DONE", json.dumps(summary, ensure_ascii=False), flush=True)
    return summary


if __name__ == "__main__":
    main()
