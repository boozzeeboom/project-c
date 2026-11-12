"""Строгий Humanoid-риг A-pose персонажей с отбраковкой лишних пальцев.

Запуск в Blender 5.x или через официальный MCP. Исходные FBX не изменяются.
Старая версия используется только как библиотека измерений тела и экспорта.
"""
import bpy
import itertools
import json
import math
import os
import sys
import numpy as np
from mathutils import Vector, Matrix, Quaternion, kdtree

ROOT = os.path.dirname(os.path.abspath(__file__))
DEFAULT_INPUT = r"C:\Projects\ProjectC_client\Assets\Generated_Models_Retopo\01_Characters\Archetypes\PC_Char_Adjudicator_Female_APOSE_v01\PC_Char_Adjudicator_Female_APOSE_v01_Retopo.fbx"
DEFAULT_REFERENCE = r"C:\Projects\ProjectC_client\Assets\Generated_Models\_Legacy_PreAugust\Character_17866748877224\BLENDER\1_5.fbx"
DEFAULT_OUTPUT = r"C:\Projects\ProjectC_client\Assets\Generated_Models_Rigged\01_Characters\Archetypes\PC_Char_Adjudicator_Female_APOSE_v01\PC_Char_Adjudicator_Female_APOSE_v01_Rigged_Humanoid.fbx"
DEFAULT_REPORT = os.path.join(ROOT, "_reports", "adjudicator_v2", "rig_report.json")
DEFAULT_RENDERS = os.path.join(ROOT, "_reports", "adjudicator_v2")
DEFAULT_BLEND = os.path.join(ROOT, "_work", "adjudicator_rigged_humanoid.blend")
TAGS = ("B-thumb", "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky")
LONG_TAGS = TAGS[1:]


def load_helpers():
    """Загружает функции прежнего скрипта, не выполняя его main."""
    namespace = {"__name__": "rig_character_helpers"}
    path = os.path.join(ROOT, "rig_character.py")
    with open(path, "r", encoding="utf-8") as handle:
        source = handle.read().rsplit("\nmain()", 1)[0]
    exec(compile(source, path, "exec"), namespace)
    return namespace


H = load_helpers()


def safe_wipe():
    """Очищает рабочую сцену разрешённым в официальном MCP способом."""
    bpy.ops.wm.read_homefile(use_empty=True, use_factory_startup=True)


H["wipe"] = safe_wipe


def safe_import_fbx(path):
    """Даёт FBX-импортёру Blender 5.3 обязательный активный контекст."""
    active = getattr(bpy.context, "object", None)
    if active and active.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    temporary = None
    if bpy.context.view_layer.objects.active is None:
        data = bpy.data.meshes.new("__FBX_IMPORT_CONTEXT__")
        temporary = bpy.data.objects.new("__FBX_IMPORT_CONTEXT__", data)
        bpy.context.scene.collection.objects.link(temporary)
        temporary.select_set(True)
        bpy.context.view_layer.objects.active = temporary
    bpy.ops.import_scene.fbx(filepath=path, use_anim=False)
    if temporary and bpy.data.objects.get(temporary.name):
        bpy.data.objects.remove(temporary, do_unlink=True)


H["import_fbx"] = safe_import_fbx


def shortest_path_data(P, A, W, indptr, nb, ww):
    """Кратчайшие пути по коже от запястья, включая предшественников."""
    import heapq
    count = len(P)
    distance = np.full(count, 1e9)
    previous = np.full(count, -1, dtype=np.int64)
    hand_u = (P - W) @ A
    seeds = np.where(hand_u < hand_u.min() + 0.004)[0]
    queue = []
    for seed in seeds:
        distance[seed] = 0.0
        heapq.heappush(queue, (0.0, int(seed)))
    while queue:
        current_distance, i = heapq.heappop(queue)
        if current_distance > distance[i]:
            continue
        for k in range(int(indptr[i]), int(indptr[i + 1])):
            j = int(nb[k])
            new_distance = current_distance + float(ww[k])
            if new_distance < distance[j] - 1e-12:
                distance[j] = new_distance
                previous[j] = i
                heapq.heappush(queue, (new_distance, j))
    return distance, previous


def path_from_previous(previous, tip):
    path = [int(tip)]
    while previous[path[-1]] >= 0 and len(path) <= len(previous):
        path.append(int(previous[path[-1]]))
    return path


def detect_tips(P, g, indptr, nb):
    """Находит кончики с допуском к плато триангуляции; лишние отбраковываются позже."""
    tree = kdtree.KDTree(len(P))
    for i, point in enumerate(P):
        tree.insert(Vector(tuple(point)), i)
    tree.balance()
    maxima = []
    for i in range(len(P)):
        if g[i] < 0.055 or g[i] > 1e8:
            continue
        near = [j for _co, j, _dist in tree.find_range(Vector(tuple(P[i])), 0.008)]
        if near and g[i] >= float(np.max(g[near])) - 0.0025:
            maxima.append(i)
    tips = []
    for i in sorted(maxima, key=lambda j: -g[j]):
        if all(float(np.linalg.norm(P[i] - P[j])) > 0.012 for j in tips):
            tips.append(i)
        if len(tips) >= 10:
            break
    return tips


def reference_patterns(bones):
    """Относительные длины и поперечные позиции кончиков эталонной кисти."""
    frame = H["ref_hand_frame"](bones, ".L")
    if frame is None:
        raise RuntimeError("REVIEW_HANDS: нет эталонной системы кисти")
    O, X, Y, _Z, _length = frame
    spreads, forwards = [], []
    for tag in LONG_TAGS:
        name = tag + "03.L"
        point = Vector(bones[name]["tail"])
        spreads.append((point - O).dot(X))
        forwards.append((point - O).dot(Y))
    spreads = np.array(spreads)
    forwards = np.array(forwards)
    spread_pattern = (spreads - spreads[0]) / (spreads[-1] - spreads[0])
    forward_pattern = (forwards - forwards[0]) / (forwards[0] - forwards[-1])
    return spread_pattern, forward_pattern


def choose_digits(candidates, bones):
    """Большой отдельно; четыре длинных — лучшим поднабором по эталонным паттернам."""
    if len(candidates) < 5:
        raise RuntimeError("REVIEW_HANDS: найдено меньше пяти кандидатов")
    for item in candidates:
        item["thumb_score"] = item["spread"] - 0.25 * item["forward"]
    thumb = min(candidates, key=lambda item: item["thumb_score"])
    remaining = [item for item in candidates if item["id"] != thumb["id"]]
    remaining.sort(key=lambda item: item["spread"])
    if len(remaining) < 4:
        raise RuntimeError("REVIEW_HANDS: недостаточно длинных пальцев")
    ref_spread, ref_forward = reference_patterns(bones)
    choices = []
    for subset in itertools.combinations(remaining, 4):
        subset = sorted(subset, key=lambda item: item["spread"])
        spreads = np.array([item["spread"] for item in subset])
        forwards = np.array([item["forward"] for item in subset])
        if spreads[-1] - spreads[0] < 0.025 or forwards[0] - forwards[-1] < 0.015:
            continue
        spread_norm = (spreads - spreads[0]) / (spreads[-1] - spreads[0])
        forward_norm = (forwards - forwards[0]) / (forwards[0] - forwards[-1])
        spread_cost = float(np.mean((spread_norm - ref_spread) ** 2))
        forward_cost = float(np.mean((forward_norm - ref_forward) ** 2))
        # Длина важнее: поперечный веер у сгенерированных кистей заметно варьируется.
        cost = forward_cost + 0.65 * spread_cost
        choices.append({"ids": [item["id"] for item in subset], "cost": cost,
                        "spread_cost": spread_cost, "forward_cost": forward_cost})
    if not choices:
        raise RuntimeError("REVIEW_HANDS: нет допустимого набора из четырёх пальцев")
    choices.sort(key=lambda item: item["cost"])
    selected = sorted(choices[0]["ids"])
    rejected = [item["id"] for item in remaining if item["id"] not in selected]
    return thumb, selected, rejected, choices


def center_on_surface(bvh, point, tangent, palm_normal):
    """Центрирует сустав между двумя стенками пальца по поперечному лучу."""
    point = np.array(point, dtype=float)
    tangent = np.array(tangent, dtype=float)
    tangent /= max(float(np.linalg.norm(tangent)), 1e-9)
    normal = np.array(palm_normal, dtype=float)
    normal -= tangent * float(normal @ tangent)
    if np.linalg.norm(normal) < 1e-6:
        return point, {"centered": False, "thickness": 0.0}
    normal /= np.linalg.norm(normal)
    nearest = bvh.find_nearest(Vector(tuple(point)))
    if nearest[0] is None:
        return point, {"centered": False, "thickness": 0.0}
    surface = np.array(nearest[0], dtype=float)
    first = bvh.ray_cast(Vector(tuple(surface + normal * 0.04)), Vector(tuple(-normal)), 0.08)
    second = bvh.ray_cast(Vector(tuple(surface - normal * 0.04)), Vector(tuple(normal)), 0.08)
    if first[0] is None or second[0] is None:
        return point, {"centered": False, "thickness": 0.0}
    a, b = np.array(first[0]), np.array(second[0])
    thickness = float(np.linalg.norm(a - b))
    if not 0.002 <= thickness <= 0.055:
        return point, {"centered": False, "thickness": thickness}
    return (a + b) * 0.5, {"centered": True, "thickness": thickness}


def trace_chain(P, previous, tip_index, target_length, fractions, bvh, palm_normal):
    """Медиальная цепочка от реального кончика; никаких эталонных координат."""
    path = path_from_previous(previous, tip_index)
    Q = np.array([P[i] for i in path], dtype=float)
    if len(Q) < 5:
        raise RuntimeError("REVIEW_HANDS: слишком короткая трасса пальца")
    tangent = np.zeros_like(Q)
    tangent[1:-1] = Q[2:] - Q[:-2]
    tangent[0] = Q[1] - Q[0]
    tangent[-1] = Q[-1] - Q[-2]
    tangent /= np.maximum(np.linalg.norm(tangent, axis=1)[:, None], 1e-9)
    centers = np.zeros_like(Q)
    for i in range(len(Q)):
        delta = P - Q[i]
        longitudinal = delta @ tangent[i]
        lateral = delta - np.outer(longitudinal, tangent[i])
        selected = (np.abs(longitudinal) < 0.0025) & (np.linalg.norm(lateral, axis=1) < 0.022)
        centers[i] = Q[i] + lateral[selected].mean(axis=0) if selected.sum() >= 6 else Q[i]
    for _ in range(2):
        centers[1:-1] = (centers[:-2] + centers[1:-1] + centers[2:]) / 3.0
    arc = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(centers, axis=0), axis=1))])
    total = float(arc[-1])
    if not 0.025 < target_length < total:
        raise RuntimeError("REVIEW_HANDS: эталонная длина не помещается в трассу")

    def at_distance(distance):
        distance = float(np.clip(distance, 0.0, total))
        i = int(np.searchsorted(arc, distance))
        if i <= 0:
            return centers[0]
        if i >= len(arc):
            return centers[-1]
        factor = (distance - arc[i - 1]) / max(arc[i] - arc[i - 1], 1e-9)
        return centers[i - 1] + (centers[i] - centers[i - 1]) * factor

    raw_nodes = [at_distance(target_length * (1.0 - fraction)) for fraction in fractions]
    raw_nodes[-1] = Q[0].copy()
    digit_direction = raw_nodes[-1] - raw_nodes[0]
    nodes = []
    quality = []
    for i, point in enumerate(raw_nodes[:-1]):
        centered, info = center_on_surface(bvh, point, digit_direction, palm_normal)
        inside, clearance = H["surface_probe"](bvh, centered, maxd=0.06)
        info.update({"inside": bool(inside), "clearance": float(clearance)})
        nodes.append(centered)
        quality.append(info)
    nodes.append(raw_nodes[-1])
    return nodes, quality, total


def bone_fractions(bones, tag):
    names = sorted(name for name in bones if name.startswith(tag) and name.endswith(".L")
                   and not name.endswith("_end"))
    lengths = []
    for name in names:
        lengths.append((Vector(bones[name]["tail"]) - Vector(bones[name]["head"])).length)
    total = sum(lengths)
    cumulative = np.cumsum(lengths) / total
    return [0.0] + [float(value) for value in cumulative]


def strict_hand_fit(co, wrist0, arm_dir, provisional_length, sgn, suffix, template, report):
    """Измеряет кисть пилота без projection/fallback эталонного скелета."""
    W0 = np.array(wrist0, dtype=float)
    A = np.array(arm_dir, dtype=float)
    A /= np.linalg.norm(A)
    # Сначала находим узкое сечение запястья вдоль предплечья.
    initial_rel = co - W0
    initial_u = initial_rel @ A
    initial_perp = initial_rel - np.outer(initial_u, A)
    initial_distance = np.linalg.norm(initial_perp, axis=1)
    region = (initial_u > -0.60 * provisional_length) & (initial_u < 0.60 * provisional_length) & \
             (initial_distance < 0.60 * provisional_length)
    region_points = initial_perp[region]
    region_u = initial_u[region]
    grid = np.arange(-0.45 * provisional_length, 0.45 * provisional_length, 0.004)
    widths = np.full(len(grid), np.nan)
    for i, position in enumerate(grid):
        selected = np.abs(region_u - position) < 0.002
        if selected.sum() >= 20:
            section = region_points[selected] - region_points[selected].mean(axis=0)
            widths[i] = float(np.percentile(np.linalg.norm(section, axis=1), 90))
    if np.all(np.isnan(widths)):
        raise RuntimeError("REVIEW_HANDS: нет профиля запястья")
    wrist_offset = float(grid[int(np.nanargmin(widths))])
    W0 = W0 + A * wrist_offset
    # Затем центрируем сечение, убирая смещение к краю рукава.
    rel0 = co - W0
    u0 = rel0 @ A
    perp0 = rel0 - np.outer(u0, A)
    slice_mask = (np.abs(u0) < 0.010) & (np.linalg.norm(perp0, axis=1) < 0.100)
    if slice_mask.sum() < 12:
        raise RuntimeError("REVIEW_HANDS: не удалось измерить сечение запястья")
    W = W0 + np.median(perp0[slice_mask], axis=0)
    rel = co - W
    u = rel @ A
    perp = rel - np.outer(u, A)
    distance = np.linalg.norm(perp, axis=1)
    hand_mask = (u > 0.0) & (u < 0.45) & (distance < 0.55 * provisional_length)
    P, g, indptr, nb, ww = H["hand_geo"](co, hand_mask, A, W)
    if P is None:
        raise RuntimeError("REVIEW_HANDS: пустой граф кисти")
    distance, previous = shortest_path_data(P, A, W, indptr, nb, ww)
    g = distance
    tip_indices = detect_tips(P, g, indptr, nb)
    if len(tip_indices) < 5:
        raise RuntimeError("REVIEW_HANDS: детектор нашёл %d концов" % len(tip_indices))
    Hvec = P - W
    D = Hvec[int(np.argmax(g))].copy()
    D /= np.linalg.norm(D)
    covariance = (Hvec - Hvec.mean(axis=0)).T @ (Hvec - Hvec.mean(axis=0))
    _values, vectors = np.linalg.eigh(covariance)
    N = vectors[:, 0] - D * float(vectors[:, 0] @ D)
    N /= np.linalg.norm(N)
    if N @ np.array([-sgn, 0.0, 0.0]) < 0.0:
        N = -N
    S = np.cross(D, N)
    S /= np.linalg.norm(S)
    candidates = []
    for new_id, tip_index in enumerate(tip_indices):
        path = path_from_previous(previous, tip_index)
        if len(path) < 5:
            continue
        candidates.append({
            "id": new_id, "tip_index": int(tip_index),
            "tip": [float(v) for v in P[tip_index]],
            "geodesic": float(g[tip_index]),
            "spread": float((P[tip_index] - W) @ S),
            "forward": float((P[tip_index] - W) @ D),
        })
    thumb, selected_ids, rejected_ids, choices = choose_digits(candidates, template["bones"])
    by_id = {item["id"]: item for item in candidates}
    assignment = {"B-thumb": thumb["id"]}
    assignment.update({tag: candidate_id for tag, candidate_id in zip(LONG_TAGS, selected_ids)})
    digits = {}
    traces = {}
    outside_joints = []
    for tag, candidate_id in assignment.items():
        candidate = by_id[candidate_id]
        target = float(template["digit_len"].get(tag, 0.0))
        if target <= 0.0:
            raise RuntimeError("REVIEW_HANDS: нет эталонной длины для " + tag)
        fractions = bone_fractions(template["bones"], tag)
        nodes, quality, total = trace_chain(P, previous, candidate["tip_index"], target,
                                            fractions, _BVH, N)
        for index, info in enumerate(quality):
            if not info["inside"]:
                outside_joints.append({"candidate": candidate_id, "joint": index})
        names = sorted(name for name in template["bones"]
                       if name.startswith(tag) and name.endswith(suffix) and not name.endswith("_end"))
        lengths = [float(np.linalg.norm(nodes[i + 1] - nodes[i])) for i in range(3)]
        if min(lengths) < 0.004:
            raise RuntimeError("REVIEW_HANDS: вырожденная кость " + tag)
        direction = nodes[-1] - nodes[0]
        direction /= np.linalg.norm(direction)
        digits[tag] = {
            "base": [float(v) for v in nodes[0]],
            "nodes": [[float(v) for v in point] for point in nodes[1:]],
            "dir": [float(v) for v in direction],
            "lens": lengths,
            "source": "geodesic_strict",
            "candidate": candidate_id,
            "target": target,
        }
        traces[tag] = {"candidate": candidate_id, "nodes": [[float(v) for v in p] for p in nodes],
                       "quality": quality, "path_total": total}
    hand_len = float(np.percentile(np.linalg.norm(Hvec, axis=1), 99.5))
    ref_frame = H["ref_hand_frame"](template["bones"], suffix)
    O_ref, X_ref, Y_ref, Z_ref, ref_len = ref_frame
    reference_frame = np.stack([np.array(X_ref), np.array(Y_ref), np.array(Z_ref)], axis=1)
    target_frame = np.stack([S, D, N], axis=1)
    rotation = target_frame @ reference_frame.T
    fit = {
        "wrist": [float(v) for v in W],
        "hand_dir": [float(v) for v in D],
        "palm_normal": [float(v) for v in N],
        "spread_axis": [float(v) for v in S],
        "hand_len": hand_len,
        "hand_rot": [float(v) for v in rotation.reshape(-1)],
        "hand_scale": 1.0,
        "digits": digits,
        "candidates": candidates,
        "selected": assignment,
        "rejected": rejected_ids,
        "subset_scores": choices,
        "traces": traces,
        "outside_joints": outside_joints,
        "status": "OK" if not outside_joints else "REVIEW_HANDS",
    }
    report.setdefault("hand_fit", {})[suffix] = fit
    if outside_joints:
        failure_path = os.path.join(ROOT, "_reports", "adjudicator_v2", "hand_failure" + suffix + ".json")
        os.makedirs(os.path.dirname(failure_path), exist_ok=True)
        with open(failure_path, "w", encoding="utf-8") as handle:
            json.dump(fit, handle, ensure_ascii=False, indent=1)
        raise RuntimeError("REVIEW_HANDS: суставы вне меша " + suffix + ": " + str(outside_joints))
    return fit


def weight_report(mesh, arm):
    sums = np.zeros(len(mesh.data.vertices))
    influences = np.zeros(len(mesh.data.vertices), dtype=np.int64)
    used = set()
    for vertex in mesh.data.vertices:
        for group in vertex.groups:
            sums[vertex.index] += group.weight
            if group.weight > 1e-5:
                influences[vertex.index] += 1
                used.add(mesh.vertex_groups[group.group].name)
    return {
        "vertices": len(sums),
        "unweighted": int((sums < 1e-5).sum()),
        "sum_min": float(sums.min()),
        "sum_max": float(sums.max()),
        "max_influences": int(influences.max()),
        "zero_weight_bones": [bone.name for bone in arm.data.bones if bone.name not in used],
    }


def repair_unweighted(mesh, arm):
    """Точечный ремонт непокрытых вершин ближайшим сегментом, без изменения скелета."""
    segments = []
    for bone in arm.data.bones:
        a, b = np.array(bone.head_local), np.array(bone.tail_local)
        direction = b - a
        segments.append((bone.name, a, direction, float(direction @ direction)))
    repaired = 0
    for vertex in mesh.data.vertices:
        if sum(group.weight for group in vertex.groups) >= 1e-5:
            continue
        point = np.array(vertex.co)
        best = None
        for name, a, direction, length2 in segments:
            t = float(np.clip((point - a) @ direction / max(length2, 1e-12), 0.0, 1.0))
            dist = float(np.linalg.norm(point - (a + direction * t)))
            if best is None or dist < best[0]:
                best = (dist, name)
        mesh.vertex_groups[best[1]].add([vertex.index], 1.0, 'REPLACE')
        repaired += 1
    return repaired


def segment_distance(point, a, b):
    """Расстояние до сегмента кости в rest pose."""
    direction = b - a
    factor = float(np.clip((point - a) @ direction / max(float(direction @ direction), 1e-12), 0.0, 1.0))
    return float(np.linalg.norm(point - (a + direction * factor)))


def weight_hands(mesh, arm, hand_fits):
    """Детерминированный skin кистей; лишний палец следует за ближайшим выбранным."""
    coordinates = np.array([tuple(vertex.co) for vertex in mesh.data.vertices], dtype=float)
    stats = {}
    for suffix in (".L", ".R"):
        fit = hand_fits[suffix]
        wrist = np.array(fit["wrist"], dtype=float)
        direction = np.array(fit["hand_dir"], dtype=float)
        hand_length = float(fit["hand_len"])
        rel = coordinates - wrist
        along = rel @ direction
        radial = np.linalg.norm(rel - np.outer(along, direction), axis=1)
        selected = np.where((along > -0.035) & (along < hand_length * 1.18) &
                            (radial < hand_length * 0.78))[0]
        hand_name = "B-hand" + suffix
        forearm_name = "B-forearm" + suffix
        finger_segments = []
        for tag in TAGS:
            chain = []
            for index in range(1, 4):
                name = "%s%02d%s" % (tag, index, suffix)
                bone = arm.data.bones[name]
                chain.append((name, np.array(bone.head_local), np.array(bone.tail_local)))
            finger_segments.append(chain)
        hand_bone = arm.data.bones[hand_name]
        forearm_bone = arm.data.bones[forearm_name]
        hand_segment = (np.array(hand_bone.head_local), np.array(hand_bone.tail_local))
        forearm_segment = (np.array(forearm_bone.head_local), np.array(forearm_bone.tail_local))
        affected_names = [hand_name, forearm_name] + [item[0] for chain in finger_segments for item in chain]
        additions = {name: [] for name in affected_names}
        for vertex_index in selected:
            point = coordinates[vertex_index]
            chain_distances = [[segment_distance(point, a, b) for _name, a, b in chain]
                               for chain in finger_segments]
            nearest_chain = min(range(len(TAGS)), key=lambda i: min(chain_distances[i]))
            nearest_finger = min(chain_distances[nearest_chain])
            hand_distance = segment_distance(point, *hand_segment)
            forearm_distance = segment_distance(point, *forearm_segment)
            weights = {}
            if nearest_finger < hand_distance * 1.15:
                # Не смешиваем соседние пальцы: это особенно важно при шести digit-кандидатах.
                for (_name, _a, _b), distance in zip(finger_segments[nearest_chain],
                                                     chain_distances[nearest_chain]):
                    weights[_name] = 1.0 / ((distance + 0.0025) ** 4)
                weights[hand_name] = 0.30 / ((hand_distance + 0.0025) ** 4)
            else:
                weights[hand_name] = 1.0 / ((hand_distance + 0.0025) ** 4)
            if along[vertex_index] < 0.025:
                weights[forearm_name] = 1.0 / ((forearm_distance + 0.0025) ** 4)
            strongest = sorted(weights, key=weights.get, reverse=True)[:4]
            total = sum(weights[name] for name in strongest)
            for name in affected_names:
                mesh.vertex_groups[name].remove([int(vertex_index)])
            for name in strongest:
                weight = weights[name] / total
                if weight > 1e-6:
                    mesh.vertex_groups[name].add([int(vertex_index)], weight, 'REPLACE')
                    additions[name].append(int(vertex_index))
        stats[suffix] = {"vertices": int(len(selected)),
                         "weighted_per_bone": {name: len(indices) for name, indices in additions.items()}}
    return stats


def pose_fingers(arm, hand_fits):
    for suffix in (".L", ".R"):
        axis = Vector(hand_fits[suffix]["spread_axis"]).normalized()
        for tag in TAGS:
            for index, angle in enumerate((25.0, 45.0, 35.0), start=1):
                name = "%s%02d%s" % (tag, index, suffix)
                pose_bone = arm.pose.bones[name]
                local_axis = pose_bone.bone.matrix_local.to_3x3().inverted() @ axis
                local_axis.normalize()
                pose_bone.rotation_mode = 'QUATERNION'
                pose_bone.rotation_quaternion = Quaternion(local_axis, math.radians(angle))
    bpy.context.view_layer.update()


def reset_pose_full(arm):
    for pose_bone in arm.pose.bones:
        pose_bone.rotation_mode = 'QUATERNION'
        pose_bone.rotation_quaternion = Quaternion()
        pose_bone.rotation_euler = (0, 0, 0)
        pose_bone.location = (0, 0, 0)
        pose_bone.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def hand_crop(mesh, fit):
    """Изолированная деформируемая копия кисти для читаемых rest/posed рендеров."""
    import bmesh
    duplicate = mesh.copy()
    duplicate.data = mesh.data.copy()
    bpy.context.scene.collection.objects.link(duplicate)
    wrist = np.array(fit["wrist"], dtype=float)
    direction = np.array(fit["hand_dir"], dtype=float)
    hand_length = float(fit["hand_len"])
    bm = bmesh.new()
    bm.from_mesh(duplicate.data)
    remove = []
    for vertex in bm.verts:
        rel = np.array(vertex.co, dtype=float) - wrist
        along = float(rel @ direction)
        radial = float(np.linalg.norm(rel - along * direction))
        if along < -0.035 or along > hand_length * 1.18 or radial > hand_length * 0.78:
            remove.append(vertex)
    bmesh.ops.delete(bm, geom=remove, context='VERTS')
    bm.to_mesh(duplicate.data)
    bm.free()
    duplicate.data.update()
    duplicate.color = mesh.color
    return duplicate


def render_previews(mesh, arm, measurement, renders):
    os.makedirs(renders, exist_ok=True)
    mesh.color = (0.72, 0.74, 0.80, 1.0)
    sticks = H["bone_debug_mesh"](arm)
    for stick in sticks:
        stick.scale.x *= 0.22
        stick.scale.y *= 0.22
    center = Vector((0.0, 0.0, H["H_REF"] * 0.5))
    H["visible"]([mesh], True)
    H["visible"](sticks, False)
    H["shoot"](os.path.join(renders, "rest_body_front.png"), center, H["H_REF"] * 1.10,
               (0, -1, 0), res=(800, 900))
    H["shoot"](os.path.join(renders, "rest_body_side.png"), center, H["H_REF"] * 1.10,
               (1, 0, 0), res=(800, 900))
    copies = {}
    cameras = {}
    for suffix in (".L", ".R"):
        fit = measurement["hand_fit" + suffix]
        hand_center = Vector(fit["wrist"]) + Vector(fit["hand_dir"]) * 0.10
        cameras[suffix] = (hand_center, -Vector(fit["palm_normal"]), -Vector(fit["hand_dir"]))
        copies[suffix] = hand_crop(mesh, fit)
        near = [obj for obj in sticks if (obj.location - hand_center).length < 0.25]
        H["visible"]([mesh], False)
        H["visible"]([copies[suffix]], True)
        H["visible"](near, False)
        H["shoot"](os.path.join(renders, "rest_hand" + suffix + "_mesh.png"), hand_center, 0.235,
                   cameras[suffix][1], res=(900, 900), up=cameras[suffix][2])
        H["visible"]([copies[suffix]], False)
        H["visible"](near, True)
        H["shoot"](os.path.join(renders, "rest_hand" + suffix + "_bones.png"), hand_center, 0.235,
                   cameras[suffix][1], res=(900, 900), up=cameras[suffix][2])
        H["visible"](near, False)
    pose_fingers(arm, measurement["hand_fit"])
    for suffix in (".L", ".R"):
        hand_center, direction, up = cameras[suffix]
        H["visible"]([copies[suffix]], True)
        H["shoot"](os.path.join(renders, "posed_hand" + suffix + ".png"), hand_center, 0.235,
                   direction, res=(900, 900), up=up)
        H["shoot"](os.path.join(renders, "posed_hand" + suffix + "_side.png"), hand_center, 0.235,
                   Vector(measurement["hand_fit" + suffix]["spread_axis"]),
                   res=(900, 900), up=up)
        H["visible"]([copies[suffix]], False)
    reset_pose_full(arm)
    H["clear_objects"](sticks + list(copies.values()))
    H["visible"]([mesh], True)


def symmetry_metrics(co):
    """Проверяет, можно ли переносить измеренную кисть на другую сторону."""
    tree = kdtree.KDTree(len(co))
    for i, point in enumerate(co):
        tree.insert(Vector(tuple(point)), i)
    tree.balance()
    sample = co[::max(1, len(co) // 3000)].copy()
    sample[:, 0] *= -1.0
    distances = np.array([tree.find(Vector(tuple(point)))[2] for point in sample])
    return {"median": float(np.median(distances)), "p95": float(np.percentile(distances, 95)),
            "max": float(distances.max()), "samples": len(distances)}


def mirror_hand_fit(left, template):
    """Отражает левую кисть с правильной ориентацией костей правой стороны."""
    import copy
    right = copy.deepcopy(left)
    mirror = np.diag([-1.0, 1.0, 1.0])

    def point(value):
        return [float(v) for v in (mirror @ np.array(value, dtype=float))]

    right["wrist"] = point(left["wrist"])
    right["hand_dir"] = point(left["hand_dir"])
    right["palm_normal"] = point(left["palm_normal"])
    right["spread_axis"] = [float(v) for v in (-mirror @ np.array(left["spread_axis"]))]
    right["mirrored_from"] = ".L"
    for candidate in right["candidates"]:
        candidate["tip"] = point(candidate["tip"])
        tip = np.array(candidate["tip"])
        wrist = np.array(right["wrist"])
        candidate["spread"] = float((tip - wrist) @ np.array(right["spread_axis"]))
        candidate["forward"] = float((tip - wrist) @ np.array(right["hand_dir"]))
        candidate["thumb_score"] = candidate["spread"] - 0.25 * candidate["forward"]
    for digit in right["digits"].values():
        digit["base"] = point(digit["base"])
        digit["nodes"] = [point(value) for value in digit["nodes"]]
        digit["dir"] = point(digit["dir"])
    for trace in right["traces"].values():
        trace["nodes"] = [point(value) for value in trace["nodes"]]
    ref_frame = H["ref_hand_frame"](template["bones"], ".R")
    _O, X_ref, Y_ref, Z_ref, _length = ref_frame
    reference_frame = np.stack([np.array(X_ref), np.array(Y_ref), np.array(Z_ref)], axis=1)
    target_frame = np.stack([np.array(right["spread_axis"]), np.array(right["hand_dir"]),
                             np.array(right["palm_normal"])], axis=1)
    rotation = target_frame @ reference_frame.T
    right["hand_rot"] = [float(v) for v in rotation.reshape(-1)]
    return right


def main():
    args = H["args_after_ddash"]()
    source = H["arg"](args, "--input", DEFAULT_INPUT)
    reference = H["arg"](args, "--reference", DEFAULT_REFERENCE)
    output = H["arg"](args, "--out", DEFAULT_OUTPUT)
    report_path = H["arg"](args, "--report", DEFAULT_REPORT)
    renders = H["arg"](args, "--renders", DEFAULT_RENDERS)
    blend_path = H["arg"](args, "--blend", DEFAULT_BLEND)
    report = {"input": source, "reference": reference, "output": output, "pipeline": "v2_strict"}
    template = H["load_template"](reference)
    report["template"] = {"height": template["height"], "digit_skin_lengths": template["digit_len"]}
    mesh = H["prepare_target"](source, report)
    co = H["orient_and_scale"](mesh, report)
    tris = H["mesh_tris"](mesh)
    H["_TRIS"] = tris
    global _BVH
    _BVH = H["body_bvh"](co, tris)
    # Старая функция используется только для тела; с пустым шаблоном её hand-fit отключён.
    measurement = H["measure"](co, report, {})
    H["body_depth_at_cache"]["neck"] = H["body_depth_at"](co, measurement["z_neck"])
    H["body_depth_at_cache"]["head"] = H["body_depth_at"](co, measurement["z_head"] + 0.02 * H["H_REF"])
    symmetry = symmetry_metrics(co)
    hand_region = co[(np.abs(co[:, 0]) > 0.32) & (co[:, 2] > 0.72) & (co[:, 2] < 1.08)]
    hand_symmetry = symmetry_metrics(hand_region)
    report["symmetry"] = {"body": symmetry, "hands": hand_symmetry}
    use_mirror = hand_symmetry["p95"] < 0.008 and "--no-mirror" not in args
    report["mirror_hand"] = use_mirror
    print("[rig] symmetry", report["symmetry"], "mirror_hand", use_mirror, flush=True)
    for suffix, sgn in ((".L", 1.0), (".R", -1.0)):
        if suffix == ".R" and use_mirror:
            fit = mirror_hand_fit(report["hand_fit"][".L"], template)
            report["hand_fit"][".R"] = fit
        else:
            fit = strict_hand_fit(co, measurement["wrist" + suffix], measurement["arm_dir" + suffix],
                                  0.117 * H["H_REF"], sgn, suffix, template, report)
        measurement["wrist" + suffix] = fit["wrist"]
        measurement["hand_len" + suffix] = fit["hand_len"]
        for key in ("hand_dir", "palm_normal", "hand_rot", "hand_scale"):
            measurement[key + suffix] = fit[key]
        measurement["digit_fit" + suffix] = fit["digits"]
        measurement["hand_fit" + suffix] = fit
    report["measure"] = measurement
    arm = H["build_armature"](template, measurement, report)
    if report["bones_created"] != 50:
        raise RuntimeError("Ожидалось 50 костей, создано %d" % report["bones_created"])
    H["bind"](mesh, arm, smooth=2)
    report["hand_weights"] = weight_hands(mesh, arm, report["hand_fit"])
    report["repaired_unweighted"] = repair_unweighted(mesh, arm)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    report["weights"] = weight_report(mesh, arm)
    weights = report["weights"]
    if (weights["unweighted"] or weights["zero_weight_bones"] or weights["max_influences"] > 4 or
            abs(weights["sum_min"] - 1.0) > 1e-4 or abs(weights["sum_max"] - 1.0) > 1e-4):
        raise RuntimeError("Некорректные веса: " + str(weights))
    measurement["hand_fit"] = report["hand_fit"]
    render_previews(mesh, arm, measurement, renders)
    H["check_pose"](arm)
    H["shoot"](os.path.join(renders, "posed_body_front.png"),
               Vector((0.0, 0.0, H["H_REF"] * 0.5)), H["H_REF"] * 1.10,
               (0, -1, 0), res=(800, 900))
    reset_pose_full(arm)
    mesh.parent = arm
    report["texture"] = H["export_fbx"](output, mesh, arm, source)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    report["blend"] = blend_path
    report["status"] = "OK"
    os.makedirs(os.path.dirname(report_path), exist_ok=True)
    with open(report_path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, ensure_ascii=False, indent=1)
    summary = {"status": report["status"], "output": output, "blend": blend_path,
               "report": report_path, "bones": report["bones_created"],
               "weights": report["weights"],
               "hands": {suffix: {"selected": report["hand_fit"][suffix]["selected"],
                                  "rejected": report["hand_fit"][suffix]["rejected"]}
                         for suffix in (".L", ".R")}}
    print("RIG_V2_DONE", json.dumps(summary, ensure_ascii=False), flush=True)
    return summary


if __name__ == "__main__":
    result = main()
