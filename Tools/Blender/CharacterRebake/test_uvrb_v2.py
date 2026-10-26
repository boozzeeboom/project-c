# -*- coding: utf-8 -*-
"""Численные integration fixtures Blender, без Unity/Play Mode/скриншотов.
Все fixture assets и отчёты — Tools/.../_work и _reports, не Assets.
"""
import json
import math
from pathlib import Path
import sys
import time
import traceback
import hashlib

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import bpy
import numpy as np
from mathutils import Vector
from rebake_model import parser, execute
from uvrb_geometry import Graph, active, metrics, uv_get, uv_set, candidate_specs
from uvrb_materials import principled
from batch_rebake import split_extra

ROOT = HERE/'_work'/('tests_v2_'+str(time.time_ns()))
ROOT.mkdir(parents=True)
RESULTS = []


def check(name, test):
    try:
        detail = test()
        RESULTS.append({'name':name, 'ok':True, 'detail':detail})
        print('TEST_PASS '+name, flush=True)
    except Exception as exc:
        RESULTS.append({'name':name, 'ok':False, 'error':str(exc), 'traceback':traceback.format_exc()})
        print('TEST_FAIL '+name+' '+str(exc), flush=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name='Mat', pbr=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf, _ = principled(mat)
    bsdf.inputs['Roughness'].default_value = .23
    bsdf.inputs['Metallic'].default_value = .8
    if pbr:
        nt = mat.node_tree
        tex = nt.nodes.new('ShaderNodeTexChecker')
        tex.inputs['Color1'].default_value = (.4, .1, .02, 1)
        tex.inputs['Color2'].default_value = (.02, .3, .1, 1)
        nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
        for name, val in [('Roughness',.23), ('Metallic',.8), ('Alpha',.37)]:
            node = nt.nodes.new('ShaderNodeValue')
            node.outputs[0].default_value = val
            nt.links.new(node.outputs[0], bsdf.inputs[name])
        node = nt.nodes.new('ShaderNodeRGB')
        node.outputs[0].default_value = (.5, .5, 1., 1.)
        normal = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(node.outputs[0], normal.inputs['Color'])
        nt.links.new(normal.outputs[0], bsdf.inputs['Normal'])
        node = nt.nodes.new('ShaderNodeRGB')
        node.outputs[0].default_value = (2., .25, .125, 1.)
        nt.links.new(node.outputs[0], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = 3.
    return mat


def cube(name='Cube', pbr=False, uv=True):
    bpy.ops.mesh.primitive_cube_add()
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(name+'_Mat', pbr))
    if not uv:
        while obj.data.uv_layers:
            obj.data.uv_layers.remove(obj.data.uv_layers[0])
    return obj


def run(name, extra=()):
    folder = ROOT/name
    folder.mkdir()
    src = folder/(name+'.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(src), compress=True)
    sha = hashlib.sha256(src.read_bytes()).hexdigest()
    args = parser().parse_args(['--src',str(src),'--out-dir',str(folder/'out'),'--report-dir',str(folder/'reports'),
                               '--res','128','--max-res','2048','--supersample','1','--samples','1','--bleed-px','2',
                               '--margin-px','6','--policy','rebuild']+list(extra))
    report = execute(args)
    assert report['status'] == 'success', report
    assert report['invariants']['ok'] and report['native_roundtrip']['ok'], report
    assert hashlib.sha256(src.read_bytes()).hexdigest() == sha
    return report, args


def sample(image, uv):
    w, h = image.size
    x, y = np.clip((np.asarray(uv)*[w,h]).astype(int), [0,0], [w-1,h-1])
    i = (int(y)*w+int(x))*4
    return np.array(image.pixels[i:i+4])


def hard_pbr():
    reset()
    obj = cube('Panel', True)
    obj.data.materials.append(material('Constant_Slot'))
    for f in obj.data.polygons:
        f.material_index = f.index%2
    # Дополнительная UV не должна удаляться.
    obj.data.uv_layers.new(name='Lightmap')
    obj.data.uv_layers[0].active_render = True
    report, args = run('hard_pbr')
    assert report['objects'][0]['status'] == 'rebaked', report['objects']
    obj = bpy.data.objects['Panel']
    assert len(obj.material_slots)==2 and len(obj.data.uv_layers)==3
    assert obj.data.uv_layers.get('Lightmap') is not None
    uvs = uv_get(obj.data, report['objects'][0]['target_uv'])
    bsdf,_ = principled(obj.material_slots[0].material)
    center = np.mean(uvs[list(obj.data.polygons[0].loop_indices)], axis=0)
    for key,value in [('Roughness',.23),('Metallic',.8),('Alpha',.37)]:
        image = bsdf.inputs[key].links[0].from_node.image
        assert abs(sample(image, center)[0]-value)<.005, (key,sample(image,center))
        assert image.colorspace_settings.name == 'Non-Color'
    emission = bsdf.inputs['Emission Color'].links[0].from_node.image
    assert sample(emission,center)[0] > 1.9, sample(emission,center)
    assert bsdf.inputs['Emission Strength'].default_value == 3.
    normal = bsdf.inputs['Normal'].links[0].from_node
    assert normal.type == 'NORMAL_MAP' and normal.uv_map == report['objects'][0]['target_uv']
    rgb = sample(normal.inputs['Color'].links[0].from_node.image, center)[:3]
    assert np.linalg.norm(rgb-[.5,.5,1.])<.03, rgb
    const,_ = principled(obj.material_slots[1].material)
    assert abs(const.inputs['Roughness'].default_value-.23)<1e-6
    args.force=False
    cached=execute(args)
    assert cached.get('cache_hit') and cached['status']=='success',cached
    return {'channels':report['objects'][0]['texture_files'], 'cache':True}


def no_uv():
    reset()
    cube('NoUV', uv=False)
    report,_ = run('no_uv')
    assert report['objects'][0]['status']=='rebaked'
    return report['objects'][0]['uv_new']


def hierarchy_rig():
    reset()
    root = bpy.data.objects.new('Root',None)
    bpy.context.scene.collection.objects.link(root)
    root.location=(2,3,4)
    a=cube('Body')
    a.parent=root
    a.location=(1,0,0)
    a.scale=(1,2,.7)
    a.shape_key_add(name='Basis')
    key=a.shape_key_add(name='Morph')
    key.data[0].co.x+=.2
    key.value=.3
    key.keyframe_insert('value',frame=1)
    key.value=.6
    key.keyframe_insert('value',frame=10)
    bpy.ops.object.armature_add()
    rig=bpy.context.object
    rig.name='Rig'
    rig.parent=root
    bone=rig.data.bones[0].name
    group=a.vertex_groups.new(name=bone)
    group.add(list(range(len(a.data.vertices))),1.,'REPLACE')
    mod=a.modifiers.new('Armature','ARMATURE')
    mod.object=rig
    socket=bpy.data.objects.new('GripSocket',None)
    bpy.context.scene.collection.objects.link(socket)
    socket.parent=a
    socket.location=(.2,0,.5)
    b=cube('MovingPart')
    b.parent=root
    b.location=(0,1,0)
    b.keyframe_insert('location',frame=1)
    b.location=(0,2,0)
    b.keyframe_insert('location',frame=10)
    report,_=run('rig')
    assert len(report['objects'])==2
    assert all(r['status']=='rebaked' for r in report['objects']),report['objects']
    assert bpy.data.objects['GripSocket'].parent.name=='Body'
    return report['native_roundtrip']


def organic():
    reset()
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12)
    obj=bpy.context.object
    obj.name='Organic'
    obj.data.materials.append(material())
    for f in obj.data.polygons:
        f.use_smooth=True
    report,_=run('organic', ['--profile','organic'])
    row=report['objects'][0]
    assert row['status']=='rebaked',row
    return {'candidates':len(row['candidates']), 'metrics':row['uv_new']}


def thin_part():
    reset()
    obj=cube('Thin')
    for v in obj.data.vertices:
        v.co.y*=.02
    obj.data.update()
    report,_=run('thin')
    assert report['objects'][0]['status']=='rebaked',report['objects']
    return report['objects'][0]['resolution']


def topology():
    reset()
    bpy.ops.mesh.primitive_torus_add(major_segments=24,minor_segments=8)
    obj=bpy.context.object
    for f in obj.data.polygons:
        f.use_smooth=True
    g=Graph(obj.data)
    labels=np.zeros(g.n,dtype=np.int64)
    assert g.chi(labels)[0]==0
    seams,labels=g.repair(labels,.004,2)
    assert len(g.disk_errors(labels))==0
    # Quad/n-gon adjacency и инвариант относительно изменения единиц.
    options=parser().parse_args(['--src','unused'])
    spec=candidate_specs(g,options)
    for v in obj.data.vertices:
        v.co*=1000
    obj.data.update()
    assert spec==candidate_specs(Graph(obj.data),options)
    return {'torus_charts':int(labels.max()+1),'scale_invariant':True}


def negative():
    reset()
    obj=cube('Bad')
    nt=obj.data.materials[0].node_tree
    bsdf,out=principled(obj.data.materials[0])
    mix=nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(bsdf.outputs[0],mix.inputs[1])
    nt.links.new(mix.outputs[0],out.inputs['Surface'])
    folder=ROOT/'negative'
    folder.mkdir()
    src=folder/'bad.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(src))
    args=parser().parse_args(['--src',str(src),'--out-dir',str(folder/'out'),'--report-dir',str(folder/'reports')])
    try:
        execute(args)
        raise AssertionError('unsupported shader accepted')
    except ValueError as exc:
        assert 'Principled' in str(exc),str(exc)
    assert not (folder/'out').exists()
    report=json.loads(next((folder/'reports').glob('*report.json')).read_text(encoding='utf8'))
    assert report['status']=='failed'
    assert split_extra('--tex "C:\\has space\\image.png" --res 256') == ['--tex','C:\\has space\\image.png','--res','256']
    return 'shader rejected before output; Windows quoted paths preserved'


def image_and_tangent():
    reset()
    bpy.ops.mesh.primitive_plane_add()
    obj=bpy.context.object
    obj.name='ImagePlane'
    mat=material('ImageMat')
    obj.data.materials.append(mat)
    bsdf,_=principled(mat)
    nt=mat.node_tree
    image=bpy.data.images.new('SourceGradient',64,64,float_buffer=True)
    image.colorspace_settings.name='Non-Color'
    yy,xx=np.mgrid[0:64,0:64]
    pixels=np.stack(((xx+.5)/64,(yy+.5)/64,np.full_like(xx,.2,dtype=float),np.ones_like(xx)),axis=-1).astype(np.float32)
    image.pixels.foreach_set(pixels.ravel())
    image.pack()
    node=nt.nodes.new('ShaderNodeTexImage')
    node.image=image
    nt.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
    normal=nt.nodes.new('ShaderNodeNormalMap')
    source_normal=np.array([.6,.3,math.sqrt(.55)])
    normal.inputs['Color'].default_value=tuple(source_normal*.5+.5)+(1.,)
    nt.links.new(normal.outputs[0],bsdf.inputs['Normal'])
    # Фиксированный поворот source UV гарантирует проверку смены tangent basis.
    original_uv=uv_get(obj.data,'UVMap')
    original_uv=np.column_stack((original_uv[:,1],1-original_uv[:,0]))
    uv_set(obj.data,'UVMap',original_uv)
    obj.data.calc_tangents(uvmap='UVMap')
    loop=obj.data.loops[0]
    n=np.array(obj.data.polygons[0].normal)
    t=np.array(loop.tangent)
    b=np.cross(n,t)*loop.bitangent_sign
    expected=t*source_normal[0]+b*source_normal[1]+n*source_normal[2]
    report,_=run('image_tangent',['--supersample','2'])
    assert report['objects'][0]['status']=='rebaked',report['objects']
    obj=bpy.data.objects['ImagePlane']
    target=report['objects'][0]['target_uv']
    uv=uv_get(obj.data,target)
    bsdf,_=principled(obj.material_slots[0].material)
    # Внутренняя точка с отличающимися U/V, не центр симметричного gradient.
    weights=np.array([.4,.3,.2,.1])
    point=weights@uv
    color=sample(bsdf.inputs['Base Color'].links[0].from_node.image,point)
    expected_uv=weights@original_uv
    assert np.max(np.abs(color[:2]-expected_uv))<.035,(color,expected_uv)
    normnode=bsdf.inputs['Normal'].links[0].from_node
    encoded=sample(normnode.inputs['Color'].links[0].from_node.image,point)[:3]*2-1
    obj.data.calc_tangents(uvmap=target)
    loop=obj.data.loops[0]
    t=np.array(loop.tangent)
    b=np.cross(n,t)*loop.bitangent_sign
    actual=t*encoded[0]+b*encoded[1]+n*encoded[2]
    dot=float(np.dot(expected,actual/np.linalg.norm(actual)))
    assert dot>.995,(expected,actual,dot)
    return {'normal_dot':dot,'basecolor_error':float(np.max(np.abs(color[:2]-expected_uv)))}


def ngon_and_overlap():
    reset()
    bpy.ops.mesh.primitive_circle_add(vertices=7,fill_type='NGON')
    obj=bpy.context.object
    obj.name='Ngon'
    obj.data.materials.append(material())
    report,_=run('ngon')
    assert report['objects'][0]['status']=='rebaked',report['objects']
    reset()
    obj=cube('Overlap')
    uv=uv_get(obj.data,obj.data.uv_layers[0].name)
    for face in obj.data.polygons:
        uv[list(face.loop_indices)]=[[0,0],[1,0],[1,1],[0,1]]
    uv_set(obj.data,obj.data.uv_layers[0].name,uv)
    m=metrics(obj,obj.data.uv_layers[0].name)
    assert m['overlap_faces']>0 and not m['valid'],m
    return {'ngon':True,'overlap_detected':m['overlap_faces']}


def split_connectivity():
    reset()
    me=bpy.data.meshes.new('SplitMesh')
    me.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(3,4,5)])
    me.update()
    obj=bpy.data.objects.new('Split',me)
    bpy.context.scene.collection.objects.link(obj)
    me.materials.append(material())
    uv=me.uv_layers.new(name='Source')
    uv_set(me,uv.name,np.array([(0,0),(1,0),(1,1),(0,0),(1,1),(0,1)]))
    report,_=run('split_connectivity')
    row=report['objects'][0]
    assert row['status']=='rebaked' and row['analytical_joined_vertices']==2,row
    assert len(bpy.data.objects['Split'].data.vertices)==6
    assert row['uv_new']['islands']==1,row
    return {'analytical_joined':2,'original_vertices':6,'new_uv_islands':1}


for name,fn in [('hard_pbr',hard_pbr),('no_uv',no_uv),('hierarchy_rig',hierarchy_rig),('organic',organic),('thin',thin_part),('topology',topology),('negative',negative),('image_tangent',image_and_tangent),('ngon_overlap',ngon_and_overlap),('split_connectivity',split_connectivity)]:
    check(name,fn)
summary={'root':str(ROOT),'tests':RESULTS,'ok':all(r['ok'] for r in RESULTS)}
path=HERE/'_reports'/'v2_test_summary.json'
path.parent.mkdir(exist_ok=True)
path.write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
print('TEST_SUMMARY '+str(path),flush=True)
if not summary['ok']:
    raise RuntimeError('UVRB test failures: '+str([r['name'] for r in RESULTS if not r['ok']]))
