# -*- coding: utf-8 -*-
"""T-CHAR-UVRB v2: явные исходные координаты и channel-aware baking без объединения слотов."""
import os
import re
import bpy
import numpy as np
from uvrb_geometry import active


def slug(name):
    return re.sub(r'[^\w.-]+', '_', name, flags=re.UNICODE).strip('._')[:80] or 'item'


def principled(mat):
    if mat is None or mat.node_tree is None:
        raise ValueError('Пустой material slot: назначьте материал перед перепечкой')
    outputs = [n for n in mat.node_tree.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output]
    if len(outputs) != 1 or not outputs[0].inputs['Surface'].is_linked:
        raise ValueError(f'{mat.name}: отсутствует единственный Surface output')
    out = outputs[0]
    if out.inputs['Volume'].is_linked or out.inputs['Displacement'].is_linked:
        raise ValueError(f'{mat.name}: Volume/Displacement требуют отдельного baking contract')
    bsdf = out.inputs['Surface'].links[0].from_node
    if bsdf.type != 'BSDF_PRINCIPLED':
        raise ValueError(f'{mat.name}: Surface должен быть одним Principled (Mix/другие BSDF не упрощаем молча)')
    return bsdf, out


def reachable_nodes(bsdf):
    seen = set()
    stack = [bsdf]
    while stack:
        node = stack.pop()
        if node in seen:
            continue
        seen.add(node)
        stack.extend(link.from_node for inp in node.inputs for link in inp.links)
    return seen


def preflight_material(mat, uv_names, source_uv):
    bsdf, out = principled(mat)
    if mat.animation_data or mat.node_tree.animation_data:
        raise ValueError(f'{mat.name}: анимированный материал нельзя заменить статическими картами')
    # Нельзя молча зафиксировать в текстуре view-dependent/scene-dependent данные.
    forbidden = {'GROUP', 'SCRIPT', 'LIGHT_PATH', 'CAMERA', 'OBJECT_INFO', 'PARTICLE_INFO',
                 'TEX_ENVIRONMENT', 'TEX_IES', 'AMBIENT_OCCLUSION', 'BEVEL', 'TEX_POINTDENSITY',
                 'SHADER_TO_RGB', 'HAIR_INFO', 'VOLUME_INFO'}
    for n in reachable_nodes(bsdf):
        if n.type in forbidden:
            raise ValueError(f'{mat.name}: неподдерживаемая зависимость {n.type} ({n.name})')
        if n.type == 'NEW_GEOMETRY':
            used = {s.name for s in n.outputs if s.is_linked}
            if used - {'Position', 'Normal', 'True Normal', 'Tangent'}:
                raise ValueError(f'{mat.name}: view-dependent Geometry outputs: {used}')
        if n.type == 'TEX_IMAGE':
            if n.image is None or n.image.source in {'MOVIE', 'SEQUENCE', 'VIEWER'}:
                raise ValueError(f'{mat.name}: отсутствующая/динамическая текстура {n.name}')
            # После открытия .blend packed images загружаются лениво; has_data=False ещё не ошибка.
            if not n.image.has_data:
                _ = n.image.pixels[:4]
            if not n.image.has_data:
                try:
                    n.image.reload()
                    _ = n.image.pixels[:4]
                except Exception:
                    pass
            if not n.image.has_data:
                raise ValueError(f'{mat.name}: текстура не загружена: {n.image.filepath}')
            if not n.inputs['Vector'].is_linked and source_uv is None:
                raise ValueError(f'{mat.name}: Image Texture требует исходные UV')
        if n.type == 'UVMAP' and (n.uv_map or source_uv) not in uv_names:
            raise ValueError(f'{mat.name}: UV map не найдена: {n.uv_map}')
        if n.type == 'TEX_COORD':
            if n.outputs['UV'].is_linked and source_uv is None:
                raise ValueError(f'{mat.name}: Texture Coordinate UV без исходной UV-карты')
            if any(n.outputs[s].is_linked for s in ('Camera', 'Window', 'Reflection')):
                raise ValueError(f'{mat.name}: camera-dependent координаты')
        if n.type == 'NORMAL_MAP' and n.space == 'TANGENT' and (n.uv_map or source_uv) not in uv_names:
            raise ValueError(f'{mat.name}: tangent normal без соответствующей исходной UV')
    for socket in bsdf.inputs:
        if socket.is_linked and socket.type not in {'RGBA', 'VALUE'} and socket.name != 'Normal':
            raise ValueError(f'{mat.name}: linked {socket.name}/{socket.type} не поддерживается; исходник не изменён')
    return [s.name for s in bsdf.inputs if s.is_linked]


def pin_source_uv(mat, source_uv):
    """Фиксирует implicit source UV до переключения render UV на target."""
    if source_uv is None:
        return
    nt = mat.node_tree
    uvn = nt.nodes.new('ShaderNodeUVMap')
    uvn.uv_map = source_uv
    uvn.label = 'UVRB: исходные UV'
    for n in list(nt.nodes):
        if n == uvn:
            continue
        if n.type == 'TEX_IMAGE' and not n.inputs['Vector'].is_linked:
            nt.links.new(uvn.outputs['UV'], n.inputs['Vector'])
        elif n.type == 'TEX_COORD':
            for link in list(n.outputs['UV'].links):
                nt.links.new(uvn.outputs['UV'], link.to_socket)
        elif n.type == 'UVMAP' and not n.uv_map:
            n.uv_map = source_uv
        elif n.type in {'NORMAL_MAP', 'TANGENT'} and not n.uv_map:
            n.uv_map = source_uv


def source_resolution(materials):
    dims = []
    for mat in materials:
        bsdf, _ = principled(mat)
        for node in reachable_nodes(bsdf):
            if node.type == 'TEX_IMAGE' and node.image:
                dims.extend(node.image.size[:])
    return max(dims, default=512)


def channels_for(materials):
    channels = set()
    for mat in materials:
        bsdf, _ = principled(mat)
        channels.update(s.name for s in bsdf.inputs if s.is_linked)
    return sorted(channels, key=lambda x: (x != 'Base Color', x))


def set_materials(obj, mats):
    # Слоты и polygon.material_index остаются один-к-одному.
    for slot, mat in zip(obj.material_slots, mats):
        slot.material = mat


def bake_channels(work, source_mats, source_uv, target_uv, resolution, output_dir, prefix, options):
    pinned = []
    for mat in source_mats:
        m = mat.copy()
        pin_source_uv(m, source_uv)
        pinned.append(m)
    channels = channels_for(pinned)
    result_mats = [m.copy() for m in pinned]
    images, files = {}, []
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = options.samples
    scene.cycles.use_denoising = False
    if options.gpu:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        found = False
        for backend in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL'):
            try:
                prefs.compute_device_type = backend
                prefs.get_devices()
                if any(d.type == backend for d in prefs.devices):
                    for d in prefs.devices:
                        d.use = d.type == backend
                    scene.cycles.device = 'GPU'
                    found = True
                    break
            except (TypeError, RuntimeError):
                continue
        if not found:
            raise ValueError('--gpu: доступное GPU-устройство не найдено')
    ss = options.supersample
    res = resolution * ss
    # Float buffer сохраняет HDR и >1 scalar values (IOR/strength), не квантует до 8 bit.
    for channel in channels:
        color = channel == 'Base Color'
        normal = channel == 'Normal'
        image = bpy.data.images.new(prefix+'_'+slug(channel), res, res, alpha=True, float_buffer=True)
        image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
        image.generated_color = (.5, .5, 1., 1.) if normal else (0., 0., 0., 1.)
        temp_mats = []
        try:
            for srcmat in pinned:
                mat = srcmat.copy()
                bsdf, out = principled(mat)
                nt = mat.node_tree
                if not normal:
                    socket = bsdf.inputs[channel]
                    emission = nt.nodes.new('ShaderNodeEmission')
                    if socket.is_linked:
                        nt.links.new(socket.links[0].from_socket, emission.inputs['Color'])
                    else:
                        v = socket.default_value
                        emission.inputs['Color'].default_value = tuple(v) if socket.type == 'RGBA' else (v, v, v, 1.)
                    nt.links.new(emission.outputs[0], out.inputs['Surface'])
                else:
                    # NORMAL должен измерять нормаль, а не смесь opaque/transparent closures.
                    # При Alpha<1 Principled может вернуть нулевой вектор normal pass.
                    diffuse = nt.nodes.new('ShaderNodeBsdfDiffuse')
                    if bsdf.inputs['Normal'].is_linked:
                        nt.links.new(bsdf.inputs['Normal'].links[0].from_socket, diffuse.inputs['Normal'])
                    nt.links.new(diffuse.outputs[0], out.inputs['Surface'])
                target = nt.nodes.new('ShaderNodeTexImage')
                target.image = image
                for node in nt.nodes:
                    node.select = False
                target.select = True
                nt.nodes.active = target
                temp_mats.append(mat)
            set_materials(work, temp_mats)
            work.data.uv_layers.active = work.data.uv_layers[target_uv]
            work.data.uv_layers[target_uv].active_render = True
            active(work)
            scene.render.bake.use_selected_to_active = False
            scene.render.bake.margin_type = 'EXTEND'
            bpy.ops.object.bake(type='NORMAL' if normal else 'EMIT', uv_layer=target_uv,
                                normal_space='TANGENT', normal_r='POS_X', normal_g='POS_Y', normal_b='POS_Z',
                                margin=options.bleed_px*ss, use_clear=True, use_selected_to_active=False)
            if ss > 1:
                image.scale(resolution, resolution)
            if normal:
                pixels = np.empty(len(image.pixels), np.float32)
                image.pixels.foreach_get(pixels)
                pixels = pixels.reshape(-1, 4)
                vec = pixels[:, :3]*2-1
                lengths = np.linalg.norm(vec, axis=1)
                use = lengths > 1e-3
                vec[use] /= lengths[use, None]
                vec[~use] = (0., 0., 1.)
                pixels[:, :3] = vec*.5+.5
                image.pixels.foreach_set(pixels.ravel())
                image.update()
            pixels = np.empty(len(image.pixels), np.float32)
            image.pixels.foreach_get(pixels)
            if not np.isfinite(pixels).all():
                raise ValueError(f'{channel}: bake содержит NaN/Inf')
            # EXR — линейные float-данные; PNG base/normal удобны для импорта.
            fmt = 'PNG' if color or normal else 'OPEN_EXR'
            extension = '.png' if fmt == 'PNG' else '.exr'
            path = os.path.join(output_dir, prefix+'_'+slug(channel)+extension)
            old_format = scene.render.image_settings.file_format
            old_mode = scene.render.image_settings.color_mode
            old_depth = scene.render.image_settings.color_depth
            try:
                # Image.save() использует собственный file_format и не применяет view transform.
                image.file_format = fmt
                image.filepath_raw = path
                image.save()
            finally:
                scene.render.image_settings.file_format = old_format
                scene.render.image_settings.color_mode = old_mode
                scene.render.image_settings.color_depth = old_depth
            images[channel] = image
            files.append(path)
        finally:
            set_materials(work, pinned)
            for mat in temp_mats:
                bpy.data.materials.remove(mat)
    for i, mat in enumerate(result_mats):
        bsdf, _ = principled(mat)
        nt = mat.node_tree
        uvn = nt.nodes.new('ShaderNodeUVMap')
        uvn.uv_map = target_uv
        uvn.label = 'UVRB: новые UV'
        for channel, image in images.items():
            # Константы конкретного слота сохраняются: карта нужна только при исходной связи.
            if not bsdf.inputs[channel].is_linked:
                continue
            tex = nt.nodes.new('ShaderNodeTexImage')
            tex.image = image
            tex.interpolation = 'Linear'
            nt.links.new(uvn.outputs['UV'], tex.inputs['Vector'])
            if channel == 'Normal':
                n = nt.nodes.new('ShaderNodeNormalMap')
                n.space, n.uv_map = 'TANGENT', target_uv
                nt.links.new(tex.outputs['Color'], n.inputs['Color'])
                nt.links.new(n.outputs['Normal'], bsdf.inputs[channel])
            else:
                nt.links.new(tex.outputs['Color'], bsdf.inputs[channel])
        mat.name = prefix+'_slot_'+str(i)+'_'+slug(source_mats[i].name)
    set_materials(work, result_mats)
    for m in pinned:
        bpy.data.materials.remove(m)
    return result_mats, files
