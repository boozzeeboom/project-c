import sys, os, re, csv, json, collections
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
OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IDX = up.build_guid_index(ASSETS)
GUID2PATH = IDX

SCENE = os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity')
s = up.Scene(SCENE)


def mat_info(guid):
    path = GUID2PATH.get(guid)
    if not path:
        return None
    try:
        docs = up.load(path)
    except Exception:
        return None
    shader_name = None
    shader_guid = None
    enabled = True
    for d in docs.values():
        if d.cls == 21:
            enabled = up.get_int(d.body, 'm_Enabled', 1) == 1 or up.get_int(d.body, 'm_Shader', 1) != 0
            sid, sguid = up.get_ref(d.body, 'm_Shader')
            shader_guid = sguid
            shader_name = up.get_scalar(d.body, 'm_ShaderKeywords', '')
            break
    shader_path = GUID2PATH.get(shader_guid) if shader_guid else None
    shader_file_name = None
    if shader_path:
        try:
            with open(shader_path, 'r', encoding='utf-8', errors='replace') as f:
                head = f.read(400)
            m = re.search(r'Shader "([^"]+)"', head)
            shader_file_name = m.group(1) if m else os.path.basename(shader_path)
        except OSError:
            pass
    return {'material': os.path.relpath(path, ROOT), 'shader_guid': shader_guid,
            'shader_path': os.path.relpath(shader_path, ROOT) if shader_path else None,
            'shader_name': shader_file_name}


# ---- city material usage from scene overrides ----
city_body = s.docs[1019326845].body
mat_refs = re.findall(r"propertyPath: 'm_Materials\.Array\.data\[\d\]'\n[ \t]*value: \n[ \t]*objectReference: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: (\d+)\}", city_body)
cnt = collections.Counter(g for _, g, _ in mat_refs if g)
print('=== CITY MATERIALS (scene overrides) ===')
print('slot assignments:', len(mat_refs), 'unique materials:', len(cnt))
rows = []
shaders = collections.Counter()
for guid, n in cnt.most_common():
    mi = mat_info(guid)
    if mi is None:
        rows.append({'guid': guid, 'slots': n, 'material': '??', 'shader': '??'})
        print(f'  {n:6d} {guid} ??')
        continue
    rows.append({'guid': guid, 'slots': n, 'material': mi['material'], 'shader': mi['shader_name'] or '??'})
    shaders[mi['shader_name'] or '??'] += n
    print(f'  {n:6d} {os.path.basename(mi["material"]):32s} {mi["shader_name"]}')
print('shaders used by city slots:', dict(shaders))

# ---- materials across whole scene: collect from scene-local renderers + prefab instances ----
print('\n=== SCENE-LEVEL material refs (scene-local renderers) ===')
allmats = collections.Counter()
for r in s.renderers.values():
    for fid, g, t in r['materials']:
        if g:
            allmats[g] += 1
print('scene-local renderer slots:', sum(allmats.values()), 'unique material assets:', len(allmats))

# ---- door materials ----
print('\n=== DOOR MATERIALS ===')
DOORS_DIR = os.path.join(ASSETS, '_Project/Prefabs/City/Doors')
door_mats = collections.Counter()
for fn in sorted(os.listdir(DOORS_DIR)):
    if not fn.endswith('.prefab'):
        continue
    p = os.path.join(DOORS_DIR, fn)
    docs = up.load(p)
    for d in docs.values():
        if d.cls == 23 and not d.stripped:
            for fid, g, t in up.get_ref_guid_map(d.body, 'm_Materials'):
                if g:
                    door_mats[g] += 1
print('door prefab material assignments (per prefab, not per instance):', sum(door_mats.values()),
      'unique:', len(door_mats))
for guid, n in door_mats.most_common():
    mi = mat_info(guid)
    print(f'  {n:4d}  {os.path.basename(mi["material"]) if mi else "??":40s} {mi["shader_name"] if mi else "??"}')

# ---- shader inventory: DOTS instancing support ----
print('\n=== SHADER INVENTORY (DOTS instancing support) ===')
scan_roots = [os.path.join(ASSETS, '_Project/Shaders'), os.path.join(ASSETS, '_Project/Materials')]
shader_files = []
for r in scan_roots:
    for dp, dn, fns in os.walk(r):
        for fn in fns:
            if fn.endswith(('.shader', '.hlsl', '.cginc')):
                shader_files.append(os.path.join(dp, fn))
print('shader/hlsl files found:', len(shader_files))
srows = []
for p in sorted(shader_files):
    try:
        txt = open(p, 'r', encoding='utf-8', errors='replace').read()
    except OSError:
        continue
    name_m = re.search(r'^\s*Shader\s+"([^"]+)"', txt, re.M)
    srows.append({
        'file': os.path.relpath(p, ROOT),
        'shader_name': name_m.group(1) if name_m else '',
        'dots_instancing_on': int('DOTS_INSTANCING_ON' in txt),
        'unity_dots_instanced_prop': int('UNITY_DOTS_INSTANCED_PROP' in txt),
        'unity_object_to_world': int('unity_ObjectToWorld' in txt),
        'unity_matrix_m': int('UNITY_MATRIX_M' in txt),
        'multi_compile_instancing': int('multi_compile_instancing' in txt),
        'gpu_instancing_support': int('UNITY_GPU_INSTANCING' in txt or 'UNITY_INSTANCING_ENABLED' in txt),
        'bytes': len(txt),
    })
for r in srows:
    print(f"  dots={r['dots_instancing_on']} prop={r['unity_dots_instanced_prop']} "
          f"u2w={r['unity_object_to_world']} inst={r['multi_compile_instancing']} "
          f"{r['shader_name'] or r['file']}")
with open(os.path.join(OUT, 'C_shaders.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(srows[0].keys()))
    w.writeheader()
    w.writerows(srows)

with open(os.path.join(OUT, 'C_city_materials.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=['guid', 'slots', 'material', 'shader'])
    w.writeheader()
    w.writerows(rows)

# ---- URP materials in project: how many URP/Lit vs custom ----
print('\n=== MATERIAL LIBRARY SHADER DISTRIBUTION ===')
libdirs = [os.path.join(ASSETS, '_Project/Materials')]
sh_dist = collections.Counter()
n_mats = 0
for r in libdirs:
    for dp, dn, fns in os.walk(r):
        for fn in fns:
            if not fn.endswith('.mat'):
                continue
            n_mats += 1
            p = os.path.join(dp, fn)
            try:
                with open(p, 'r', encoding='utf-8', errors='replace') as f:
                    head = f.read(1200)
            except OSError:
                continue
            m = re.search(r'm_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})', head)
            guid = m.group(1) if m else None
            sp = GUID2PATH.get(guid)
            nm = '??'
            if sp:
                try:
                    with open(sp, 'r', encoding='utf-8', errors='replace') as f:
                        h = f.read(600)
                    mm = re.search(r'Shader "([^"]+)"', h)
                    nm = mm.group(1) if mm else os.path.basename(sp)
                except OSError:
                    pass
            sh_dist[nm] += 1
print('materials in Assets/_Project/Materials:', n_mats)
for k, v in sh_dist.most_common(20):
    print(f'  {v:4d}  {k}')
