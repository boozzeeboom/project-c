# -*- coding: utf-8 -*-
"""T-CHAR-UVRB v2: адаптивный, недеструктивный Blender headless pipeline.
Нативный результат .blend является сохранным контрактом. FBX/GLB — отдельный opt-in экспорт.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import sys
import tempfile
import time
import traceback

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import bpy
import numpy as np
from uvrb_geometry import Graph, active, array, candidate_specs, chart_copy, metrics, refine_invalid_uv, resolution_for_islands, unwrap_candidate, uv_get, uv_set
from uvrb_materials import bake_channels, preflight_material, principled, slug, source_resolution
from uvrb_qa import snapshot, compare, material_snapshot

VERSION = '2.0.0'
PROJECT = HERE.parents[2]


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--src', required=True)
    p.add_argument('--out-dir', default='')
    p.add_argument('--report-dir', default='')
    p.add_argument('--suffix', default='_Rebaked_v2')
    p.add_argument('--tex', default='', help='Явный BaseColor только для одного mesh/slot')
    p.add_argument('--res', type=int, default=0, help='0 = по исходным текстурам и измеренному UV покрытию')
    p.add_argument('--max-res', type=int, default=4096)
    p.add_argument('--supersample', type=int, default=2)
    p.add_argument('--samples', type=int, default=4)
    p.add_argument('--margin-px', type=int, default=8)
    p.add_argument('--bleed-px', type=int, default=8)
    p.add_argument('--profile', choices=['auto', 'hard', 'organic'], default='auto')
    p.add_argument('--policy', choices=['auto', 'rebuild'], default='auto', help='auto оставляет исходные UV без доказанного улучшения')
    p.add_argument('--smooth-iters', type=int, default=None)
    p.add_argument('--angle', type=float, default=None)
    p.add_argument('--sharp-angle', type=float, default=50.)
    p.add_argument('--connect-splits', choices=['exact','off'], default='exact', help='Совпадающие boundary edges только в аналитической копии')
    p.add_argument('--subpixel-area', type=float, default=.001, help='Допустимая доля поверхности на островах уже 2 px (0 = строго все)')
    p.add_argument('--min-island-frac', type=float, default=.004)
    p.add_argument('--border-smooth', type=int, default=2)
    p.add_argument('--weld', type=float, default=0., help='Совместимость CLI: v2 разрешает только 0')
    p.add_argument('--unwrap', choices=['ANGLE_BASED', 'CONFORMAL', 'MINIMUM_STRETCH'], default='MINIMUM_STRETCH')
    p.add_argument('--pack-shape', choices=['CONCAVE', 'CONVEX', 'AABB'], default='CONCAVE')
    p.add_argument('--gpu', action='store_true')
    p.add_argument('--force', action='store_true')
    p.add_argument('--audit-only', action='store_true')
    p.add_argument('--no-export', action='store_true', help='Измерить кандидаты; не печь и не публиковать assets')
    p.add_argument('--save-blend', default='', help='Совместимость: разрешён только путь основного результата внутри out-dir')
    p.add_argument('--export-format', choices=['native', 'fbx', 'glb'], default='native')
    return p


def outside_assets(path):
    path = Path(path).resolve()
    if path == PROJECT/'Assets' or PROJECT/'Assets' in path.parents:
        raise ValueError('Отчёты/логи нельзя записывать в Assets')
    return path


def fingerprint(src, options):
    h = hashlib.sha256()
    def add(path):
        h.update(str(Path(path).resolve()).encode())
        with open(path, 'rb') as f:
            for block in iter(lambda:f.read(1024*1024), b''):
                h.update(block)
    add(src)
    for path in sorted(HERE.glob('uvrb_*.py')):
        add(path)
    add(HERE/'rebake_model.py')
    args = vars(options).copy()
    for key in ('force', 'report_dir', 'audit_only', 'no_export'):
        args.pop(key, None)
    h.update(json.dumps(args, sort_keys=True).encode())
    h.update(json.dumps(snapshot(), sort_keys=True, default=str).encode())
    h.update(material_snapshot().encode())
    # Дополнительно включаем внешние картинки, реально загруженные импортёром.
    deps = []
    for image in bpy.data.images:
        path = bpy.path.abspath(image.filepath)
        if path and Path(path).is_file():
            deps.append(str(Path(path).resolve()))
    for path in sorted(set(deps)):
        add(path)
    return h.hexdigest(), sorted(set(deps))


def import_model(path):
    ext = Path(path).suffix.lower()
    if ext == '.blend':
        bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False, use_scripts=False)
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if ext == '.fbx':
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif ext in {'.glb', '.gltf'}:
        bpy.ops.import_scene.gltf(filepath=str(path))
    elif ext == '.obj':
        bpy.ops.wm.obj_import(filepath=str(path))
    elif ext == '.ply':
        bpy.ops.wm.ply_import(filepath=str(path))
    elif ext == '.stl':
        bpy.ops.wm.stl_import(filepath=str(path))
    else:
        raise ValueError('Поддерживаемые входы: blend/fbx/glb/gltf/obj/ply/stl')


def prepare_materials(meshes, options):
    if options.tex and (len(meshes) != 1 or len(meshes[0].material_slots) > 1):
        raise ValueError('--tex неоднозначен для нескольких meshes/material slots')
    for obj in meshes:
        if not obj.data.materials:
            mat = bpy.data.materials.new(obj.name+'_Default')
            mat.use_nodes = True
            obj.data.materials.append(mat)
        if options.tex:
            mat = obj.material_slots[0].material.copy()
            bsdf, _ = principled(mat)
            tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(str(Path(options.tex).resolve()), check_existing=True)
            mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
            obj.material_slots[0].material = mat


def preflight(meshes):
    if not meshes:
        raise ValueError('В сцене нет mesh objects')
    for obj in meshes:
        me = obj.data
        if not len(me.polygons):
            raise ValueError(obj.name+': mesh без полигонов')
        co = array(me.vertices, 'co', 3)
        areas = array(me.polygons, 'area')
        if not np.isfinite(co).all() or not np.isfinite(areas).all() or areas.max() <= 0 or np.any(areas <= areas.max()*1e-14):
            raise ValueError(obj.name+': NaN/Inf/вырожденная геометрия; сначала исправить mesh')
        if np.any(np.abs(obj.scale) < 1e-12):
            raise ValueError(obj.name+': нулевой масштаб')
        mods = [m.type for m in obj.modifiers if m.type != 'ARMATURE']
        if mods:
            raise ValueError(obj.name+': modifiers требуют отдельной bake-модели: '+str(mods))
        source_uv = next((u.name for u in me.uv_layers if u.active_render), None)
        names = {u.name for u in me.uv_layers}
        if len(names) >= 8:
            raise ValueError(obj.name+': достигнут лимит UV layers; исходные слои не удаляем')
        for slot in obj.material_slots:
            preflight_material(slot.material, names, source_uv)


def next_power(value):
    return 2**int(math.ceil(math.log2(max(16, value))))


def score(m):
    # Это эвристика предпочтения, а не доказательство художественного улучшения.
    return math.log1p(m['islands']) + 2*m['texel_density_cv'] + (1-m['uv_area_sum'])


def process_object(obj, options, stage, index):
    print('[UVRB] object '+obj.name, flush=True)
    work = obj.copy()
    work.data = obj.data.copy()
    work.name = '__UVRB_work'
    bpy.context.scene.collection.objects.link(work)
    work.animation_data_clear()
    work.modifiers.clear()
    work.hide_render = False
    if work.data.shape_keys:
        work.shape_key_clear()
    row = {'name': obj.name, 'status': 'pending', 'candidates': []}
    source_uv = next((u.name for u in obj.data.uv_layers if u.active_render), None)
    source_mats = [s.material for s in obj.material_slots]
    chart = None
    try:
        chart, corner_ids, connected = chart_copy(work, options.connect_splits == 'exact', options.sharp_angle)
        row['analytical_joined_vertices'] = connected
        g = Graph(chart.data, options.sharp_angle)
        source = metrics(chart, source_uv, g) if source_uv else None
        row['uv_src'] = source
        src_res = source_resolution(source_mats)
        res = options.res or next_power(src_res)
        if res > options.max_res:
            if source_uv and options.policy == 'auto':
                row.update(status='unchanged', reason='source resolution exceeds max-res')
                return row, []
            raise ValueError(obj.name+': source/--res превышает --max-res; разрешение не уменьшаем молча')
        row['source_resolution'] = src_res
        target = chart.data.uv_layers.new(name='UVRB_v2').name
        best = None
        for iterations, angle in candidate_specs(g, options):
            report = {'smooth_iterations': iterations, 'angle': angle, 'resolution': res}
            try:
                seams = unwrap_candidate(chart, target, g, iterations, angle, options, res)
                m = metrics(chart, target, g)
                if not m['valid']:
                    report['metrics_before_refine'] = m
                    report['refinement_faces'] = []
                    for _ in range(2):
                        seams, isolated = refine_invalid_uv(chart, target, g, options, res)
                        report['refinement_faces'].append(isolated)
                        m = metrics(chart, target, g)
                        if m['valid'] or isolated == 0:
                            break
                report['metrics'] = m
                needed = res
                if options.res == 0 and source and source['valid'] and m.get('uv_area_sum', 0) > 0:
                    needed = max(res, next_power(src_res*math.sqrt(source['uv_area_sum']/m['uv_area_sum'])))
                needed, subpixel = resolution_for_islands(chart, target, needed, options.max_res, options.subpixel_area)
                report['required_resolution'] = needed
                report['subpixel_surface_fraction'] = subpixel
                eligible = m['valid'] and needed <= options.max_res and subpixel <= options.subpixel_area
                if not eligible:
                    report['reason'] = 'invalid UV or thin-island/resolution budget'
                elif options.policy == 'auto' and source and source['valid'] and score(m) >= score(source)*.98:
                    eligible = False
                    report['reason'] = 'no measured improvement over source'
                report['accepted'] = eligible
                if eligible and (best is None or score(m) < score(best[0])):
                    best = (m, uv_get(chart.data, target), seams.copy(), max(res, needed), iterations)
            except (RuntimeError, ValueError) as exc:
                report.update(accepted=False, reason=str(exc))
            finally:
                if bpy.context.object and bpy.context.object.mode != 'OBJECT':
                    bpy.ops.object.mode_set(mode='OBJECT')
            row['candidates'].append(report)
        if best is None:
            if source_uv:
                row.update(status='unchanged', reason='no acceptable candidate; original UV/materials retained')
                return row, []
            raise ValueError(obj.name+': нет исходной UV и все кандидаты отклонены')
        m, uv, seams, resolution, iterations = best
        mapped = np.empty_like(uv)
        # Corner IDs не зависят от сварки аналитической копии или перестановки вершин.
        corner_ids = array(chart.data.attributes['__UVRB_corner_id'].data, 'value', dtype=np.int64)
        mapped[corner_ids] = uv
        uv = mapped
        work.data.uv_layers.new(name=target)
        uv_set(work.data, target, uv)
        original_graph = Graph(work.data, options.sharp_angle)
        _, connected_uv = original_graph.uv_labels(uv)
        seams = np.ones(len(work.data.edges), bool)
        seams[original_graph.two] = ~connected_uv | original_graph.locked
        work.data.edges.foreach_set('use_seam', seams)
        row.update(uv_new=m, resolution=resolution, chosen_smooth_iterations=iterations, non_disk_charts_left=0)
        if options.no_export:
            row['status'] = 'measured'
            return row, []
        prefix = f'{index:03d}_'+slug(obj.name)
        mats, files = bake_channels(work, source_mats, source_uv, target, resolution, str(stage), prefix, options)
        # Коммит UV/материалов только после успешной печки. Вершины/shape keys/weights исходника не трогаем.
        obj.data = obj.data.copy()
        layer = obj.data.uv_layers.new(name=target)
        uv_set(obj.data, layer.name, uv)
        obj.data.uv_layers.active = layer
        layer.active_render = True
        obj.data.edges.foreach_set('use_seam', seams)
        for slot, mat in zip(obj.material_slots, mats):
            slot.material = mat
        row.update(status='rebaked', texture_files=[Path(p).name for p in files], target_uv=layer.name)
        return row, files
    finally:
        active(work)
        if chart is not None:
            chart_data = chart.data
            bpy.data.objects.remove(chart, do_unlink=True)
            bpy.data.meshes.remove(chart_data)
        data = work.data
        bpy.data.objects.remove(work, do_unlink=True)
        bpy.data.meshes.remove(data)


def export_optional(path, kind):
    if kind == 'fbx':
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=False,
            object_types={'MESH','ARMATURE','EMPTY','CAMERA','LIGHT'}, use_mesh_modifiers=False,
            add_leaf_bones=False, bake_anim=True, use_custom_props=True,
            path_mode='COPY', embed_textures=True, apply_scale_options='FBX_SCALE_NONE')
    else:
        bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', export_apply=False,
            export_animations=True, export_skins=True, export_morph=True, export_texcoords=True,
            export_materials='EXPORT', export_extras=True)


def execute(options):
    started = time.time()
    src = Path(options.src).resolve()
    output = Path(options.out_dir).resolve() if options.out_dir else src.parent/'Rebaked_v2'
    report_dir = outside_assets(options.report_dir or HERE/'_reports'/src.stem)
    report_dir.mkdir(parents=True, exist_ok=True)
    mode_suffix = '_audit' if options.audit_only else ('_measure' if options.no_export else '')
    report_path = report_dir/(src.stem+options.suffix+mode_suffix+'_report.json')
    report = {'pipeline_version': VERSION, 'status': 'failed', 'src': str(src), 'params': vars(options),
              'objects': [], 'unity_validation': 'NOT RUN', 'visual_validation': 'NOT RUN'}
    stage = None
    try:
        if options.weld != 0:
            raise ValueError('v2: --weld изменяет топологию/weights; разрешено только 0')
        if options.supersample < 1 or options.samples < 1 or options.max_res < 16 or options.res < 0:
            raise ValueError('Недопустимый texture/sample budget')
        if options.margin_px < 0 or options.bleed_px < 0 or options.border_smooth < 0 or not 0 <= options.min_island_frac <= 1:
            raise ValueError('Недопустимые параметры UV')
        if options.smooth_iters is not None and not 0 <= options.smooth_iters <= 600:
            raise ValueError('smooth-iters должен быть 0..600')
        if options.angle is not None and not 1 <= options.angle <= 89:
            raise ValueError('angle должен быть 1..89')
        if not 0 <= options.subpixel_area <= .05 or not 1 <= options.sharp_angle <= 179:
            raise ValueError('subpixel-area: 0..0.05; sharp-angle: 1..179')
        if output == src.parent or output == src or output in src.parents:
            raise ValueError('out-dir не должен совпадать с исходной папкой или содержать её')
        native_name = src.stem+options.suffix+'.blend'
        if options.save_blend and Path(options.save_blend).resolve() != output/native_name:
            raise ValueError('--save-blend: v2 всегда сохраняет native в out-dir; отдельный путь не поддерживается')
        import_model(src)
        meshes = sorted([o for o in bpy.context.scene.objects if o.type == 'MESH'], key=lambda o:o.name)
        prepare_materials(meshes, options)
        preflight(meshes)
        fp, deps = fingerprint(src, options)
        report.update(fingerprint=fp, dependencies=deps, blender=bpy.app.version_string)
        # Проверка кэша выполняется после импорта, чтобы учесть реальные texture dependencies.
        if report_path.is_file() and not options.force and not options.audit_only and not options.no_export:
            previous = json.loads(report_path.read_text(encoding='utf8'))
            if previous.get('status') == 'success' and previous.get('fingerprint') == fp and all(Path(p).is_file() for p in previous.get('outputs', [])) and previous.get('outputs'):
                print('[UVRB] cached verified result', flush=True)
                report = previous
                report['cache_hit'] = True
                return report
        original_uvs = {o.name:[u.name for u in o.data.uv_layers] for o in meshes}
        before = snapshot(original_uvs)
        report['input_meshes'] = len(meshes)
        if options.audit_only:
            report['status'] = 'audited'
            return report
        if output.exists() and not options.force and not options.no_export:
            raise ValueError('out-dir уже существует; новый путь или явный --force')
        # Рабочие файлы и повреждённые результаты не попадают в Assets до QA.
        stage = Path(tempfile.mkdtemp(prefix='uvrb_', dir=report_dir))
        hidden = {o.name:o.hide_render for o in bpy.context.scene.objects}
        try:
            for o in bpy.context.scene.objects:
                o.hide_render = True
            for i, obj in enumerate(meshes):
                row, files = process_object(obj, options, stage, i)
                report['objects'].append(row)
        finally:
            for name, value in hidden.items():
                if name in bpy.data.objects:
                    bpy.data.objects[name].hide_render = value
        report['invariants'] = compare(before, snapshot(original_uvs))
        if not report['invariants']['ok']:
            raise ValueError('Изменены исходные данные: '+str(report['invariants']['errors']))
        if options.no_export:
            report['status'] = 'measured'
            return report
        native = stage/native_name
        all_before = snapshot()
        materials_before = material_snapshot()
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=str(native), check_existing=False, compress=True)
        bpy.ops.wm.open_mainfile(filepath=str(native), load_ui=False, use_scripts=False)
        qa = compare(all_before, snapshot())
        qa['materials_ok'] = materials_before == material_snapshot()
        qa['ok'] &= qa['materials_ok']
        report['native_roundtrip'] = qa
        if not qa['ok']:
            raise ValueError('Native roundtrip failed: '+str(qa))
        report['interchange'] = {'status': 'not_requested'}
        if options.export_format != 'native':
            exchange = stage/(src.stem+options.suffix+'.'+options.export_format)
            export_optional(exchange, options.export_format)
            report['interchange'] = {'status': 'exported_NOT_VERIFIED', 'file': exchange.name,
                'warning': 'FBX/GLB не гарантируют сохранение Blender shader graphs, UV bindings, constraints и animations. Native .blend остаётся master.'}
        # Публикация целой новой папки после QA; предыдущий результат сохраняется резервной копией.
        output.parent.mkdir(parents=True, exist_ok=True)
        backup = None
        if output.exists():
            backup = output.with_name(output.name+'.backup_'+str(time.time_ns()))
            output.rename(backup)
        try:
            shutil.copytree(stage, output)
        except Exception:
            if output.exists():
                shutil.rmtree(output)
            if backup:
                backup.rename(output)
            raise
        report['outputs'] = [str(output/p.name) for p in stage.iterdir() if p.is_file()]
        if backup:
            report['previous_output_backup'] = str(backup)
        report['status'] = 'success'
        return report
    except Exception as exc:
        report['error'] = str(exc)
        report['traceback'] = traceback.format_exc()
        raise
    finally:
        report['total_sec'] = round(time.time()-started, 2)
        if stage:
            report['work_directory'] = str(stage)
        temp = report_path.with_suffix('.tmp')
        temp.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
        os.replace(temp, report_path)
        print('UVRB_REPORT='+str(report_path), flush=True)


def main(argv=None):
    args = argv if argv is not None else (sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    options = parser().parse_args(args)
    report = execute(options)
    print('[UVRB] '+report['status'], flush=True)


if __name__ == '__main__':
    main()
