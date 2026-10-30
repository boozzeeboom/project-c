import sys, os, re, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unity_parse as up


def find_root(start):
    cur = os.path.abspath(start)
    while True:
        if os.path.isdir(os.path.join(cur, 'Assets')):
            return cur
        nxt = os.path.dirname(cur)
        if nxt == cur:
            raise RuntimeError('project root not found')
        cur = nxt


ROOT = find_root(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, 'Assets')
s = up.Scene(os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity'))

print('=== CAMERAS (scene-local) ===')
for d in s.by_cls.get(20, []):
    b = d.body
    go = up.get_ref(b, 'm_GameObject')[0]
    g = s.go.get(go, {})
    print(f"  {g.get('name','?'):22s} enabled={up.get_int(b,'m_Enabled',1)} "
          f"fov={up.get_scalar(b,'field of view')} near={up.get_scalar(b,'near clip plane')} "
          f"far={up.get_scalar(b,'far clip plane')} hdr={up.get_scalar(b,'m_HDR')} "
          f"msaa={up.get_scalar(b,'m_AllowMSAA')} occ={up.get_scalar(b,'m_OcclusionCulling')} "
          f"depthTex={up.get_scalar(b,'m_RequireDepthTexture')} "
          f"cullMask={up.get_scalar(b,'m_CullingMask')}")

print('\n=== LIGHTS (scene-local) ===')
light_types = collections.Counter()
n_lights = 0
for d in s.by_cls.get(108, []):
    b = d.body
    go = up.get_ref(b, 'm_GameObject')[0]
    g = s.go.get(go, {})
    n_lights += 1
    t = up.get_scalar(b, 'm_Type')
    light_types[t] += 1
print('scene-local lights:', n_lights, dict(light_types))

print('\n=== PREFAB-INSTANCE LIGHTS (by prefab) ===')
flat = 0
for pi in s.prefab_instances.values():
    pass
print('(lights inside city/doors prefabs counted separately)')

print('\n=== TERRAIN / PROBES / OCCLUSION ===')
td = s.by_cls.get(218, [])
print('terrain docs:', len(td))
for d in td[:1]:
    b = d.body
    print('  m_TerrainData:', up.get_ref(b, 'm_TerrainData'))
    print('  m_Scale:', up.get_scalar(b, 'm_Scale'), ' heightmap:', up.get_scalar(b, 'm_HeightmapPixelError'))
occl = s.by_cls.get(29, [])
for d in occl:
    print('  OcclusionCullingSettings:', d.body.strip().replace('\n', ' | ')[:400])
print('reflection probe docs:', len(s.by_cls.get(215, [])))
print('RenderSettings: m_Sun =', up.get_ref(s.by_cls[104][0].body, 'm_Sun')
      if 104 in s.by_cls else '?')
rs = s.by_cls[104][0].body
for k in ('m_Fog', 'm_AmbientMode', 'm_DefaultReflectionMode', 'm_DefaultReflectionResolution',
          'm_ReflectionBounces', 'm_ReflectionIntensity', 'm_SkyboxMaterial'):
    print('   ', k, '=', up.get_scalar(rs, k))

print('\n=== GAMEOBJECT static flags / layers (scene-local) ===')
layers = collections.Counter(g['layer'] for g in s.go.values())
static_flags = collections.Counter(g['staticFlags'] for g in s.go.values())
active = collections.Counter(g['active'] for g in s.go.values())
print('layers:', dict(layers))
print('staticFlags:', dict(static_flags))
print('active:', dict(active))
print('names of layers 8..12:', [g['name'] for g in s.go.values() if g['layer'] == 8][:5])

print('\n=== prefab instance m_Layer overrides distribution (city) ===')
body = s.docs[1019326845].body
m_layers = collections.Counter(re.findall(r'propertyPath: m_Layer\n[ \t]*value: (\d+)', body))
print('city layer overrides:', dict(m_layers))

print('\n=== renderer settings in city prefab instance ===')
print('m_Enabled overrides:', collections.Counter(re.findall(r'propertyPath: m_Enabled\n[ \t]*value: (\d+)', body)))
print('m_MotionVectors overrides:', collections.Counter(re.findall(r'propertyPath: m_MotionVectors\n[ \t]*value: (\d+)', body)))
print('m_DynamicOccludee overrides:', collections.Counter(re.findall(r'propertyPath: m_DynamicOccludee\n[ \t]*value: (\d+)', body)))

print('\n=== doors: scene-level overrides on door prefab instances ===')
door_guids = {}
for dp, dn, fns in os.walk(os.path.join(ASSETS, '_Project/Prefabs/City/Doors')):
    for fn in fns:
        if fn.endswith('.prefab.meta'):
            for line in open(os.path.join(dp, fn), 'r', encoding='utf-8', errors='replace'):
                if line.startswith('guid:'):
                    door_guids[fn[:-10]] = line.split(':')[1].strip()
                    break
props = collections.Counter()
n_inst = 0
for pi in s.prefab_instances.values():
    if pi['source'] and pi['source'][1] in door_guids.values():
        n_inst += 1
        d = s.docs[pi['fid']]
        for p in re.findall(r'propertyPath: ([\w\.\[\]\x27]+)', d.body):
            props[p] += 1
print('door instances:', n_inst, 'modification props:', dict(props.most_common(10)))
