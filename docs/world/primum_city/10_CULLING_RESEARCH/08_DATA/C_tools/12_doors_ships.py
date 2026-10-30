"""Doors + ships + materials + shader inventory for the Project C audit (read-only)."""
import sys, os, re, csv, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unity_parse as up
import fbx_parse


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

SCENE = os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity')
s = up.Scene(SCENE)

FBX_TRI_CACHE = {}


def mesh_source_path(guid):
    return IDX.get(guid)


def fbx_file_for(path):
    """For a mesh inside an .fbx, path is the fbx; for .asset, it's the asset."""
    return path


def mesh_tris(guid, fid):
    """Try to get triangle count for a mesh reference (guid, fileID)."""
    path = IDX.get(guid)
    if not path:
        return None
    if path.lower().endswith('.fbx'):
        if path not in FBX_TRI_CACHE:
            try:
                st = fbx_parse.summarize(path)
                FBX_TRI_CACHE[path] = st
            except Exception:
                FBX_TRI_CACHE[path] = None
        st = FBX_TRI_CACHE[path]
        if not st:
            return None
        # fileID for fbx meshes: 4300000 + internal id? try direct match
        g = st['geometries'].get(fid)
        if g:
            return g['tris']
        return None
    return None


print('=== DOOR PREFABS ===')
DOORS_DIR = os.path.join(ASSETS, '_Project/Prefabs/City/Doors')
door_rows = []
door_prefab_guids = {}
for fn in sorted(os.listdir(DOORS_DIR)):
    if not fn.endswith('.prefab'):
        continue
    p = os.path.join(DOORS_DIR, fn)
    docs = up.load(p)
    st = up.prefab_stats(p)
    # per-renderer detail
    renderers = []
    mesh_guids = []
    for d in docs.values():
        if d.stripped:
            continue
        if d.cls == 23:
            mats = up.get_ref_guid_map(d.body, 'm_Materials')
            renderers.append({
                'slots': len([m for m in mats if m[1]]),
                'castShadows': up.get_int(d.body, 'm_CastShadows', 1),
                'receiveShadows': up.get_int(d.body, 'm_ReceiveShadows', 1),
                'materials': [g for _, g, _ in mats if g],
            })
        elif d.cls == 33:
            fid, guid = up.get_ref(d.body, 'm_Mesh')
            if guid:
                mesh_guids.append((guid, fid))
        elif d.cls == 114:
            # MonoBehaviour script guid
            _, guid = up.get_ref(d.body, 'm_Script')
            renderers.append({'script': guid})
    # meta guid
    guid = None
    try:
        with open(p + '.meta', 'r', encoding='utf-8', errors='replace') as f:
            mm = re.search(r'guid: ([0-9a-f]{32})', f.read(200))
            guid = mm.group(1) if mm else None
    except OSError:
        pass
    door_prefab_guids[guid] = fn
    inst = 0
    for pi in s.prefab_instances.values():
        if pi['source'] and pi['source'][1] == guid:
            inst += 1
    tris = 0
    tris_known = 0
    for g, fid in mesh_guids:
        t = mesh_tris(g, fid)
        if t is not None:
            tris += t
            tris_known += 1
    door_rows.append({
        'prefab': fn,
        'guid': guid,
        'gameobjects': st['gameobjects'],
        'mesh_renderers': st['mesh_renderers'],
        'material_slots': st['material_slots'],
        'unique_materials': len(st['materials']),
        'colliders': st['colliders'],
        'monobehaviours': st['monobehaviours'],
        'meshfilters': st['meshfilters'],
        'unique_meshes': len(st['meshes']),
        'tris_per_instance': tris,
        'tris_resolved_meshes': tris_known,
        'instances_in_scene': inst,
        'renderers_per_slot_detail': ';'.join(
            f"{r['slots']}slots/cast{r['castShadows']}" for r in renderers if 'slots' in r),
        'scripts': ';'.join(r['script'] for r in renderers if 'script' in r),
    })
    print(f"{fn:45s} r={st['mesh_renderers']} slots={st['material_slots']} mats={len(st['materials'])} "
          f"coll={st['colliders']} mb={st['monobehaviours']} tris={tris} inst={inst}")

with open(os.path.join(OUT, 'C_doors.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(door_rows[0].keys()))
    w.writeheader()
    w.writerows(door_rows)

print('\ntotal door instances:', sum(r['instances_in_scene'] for r in door_rows))
print('total door renderers (instances x prefab renderers):',
      sum(r['instances_in_scene'] * r['mesh_renderers'] for r in door_rows))
print('total door material slots (instances x slots):',
      sum(r['instances_in_scene'] * r['material_slots'] for r in door_rows))

print('\n=== SHIP PREFABS ===')
SHIPS_DIR = os.path.join(ASSETS, '_Project/Prefabs/Ships')
ship_rows = []
for fn in sorted(os.listdir(SHIPS_DIR)):
    if not fn.endswith('.prefab'):
        continue
    p = os.path.join(SHIPS_DIR, fn)
    try:
        st = up.prefab_stats(p)
    except Exception as e:
        print('ERR', fn, e)
        continue
    # transform depth / child count
    docs = up.load(p)
    n_tr = sum(1 for d in docs.values() if d.cls == 4 and not d.stripped)
    n_pi = sum(1 for d in docs.values() if d.cls == 1001 and not d.stripped)
    n_mc = sum(1 for d in docs.values() if d.cls == 64 and not d.stripped)
    n_scripts = sum(1 for d in docs.values() if d.cls == 114 and not d.stripped)
    in_scene = sum(1 for pi in s.prefab_instances.values() if pi['source'] and
                   IDX.get(pi['source'][1]) == p)
    ship_rows.append({
        'prefab': fn,
        'gameobjects': st['gameobjects'],
        'mesh_renderers': st['mesh_renderers'],
        'material_slots': st['material_slots'],
        'unique_meshes': len(st['meshes']),
        'unique_materials': len(st['materials']),
        'colliders': st['colliders'],
        'mesh_colliders': n_mc,
        'transforms': n_tr,
        'nested_prefab_instances': n_pi,
        'monobehaviours': n_scripts,
        'kbytes': round(os.path.getsize(p) / 1024, 1),
        'instances_in_scene': in_scene,
    })
    print(f"{fn:45s} go={st['gameobjects']:5d} r={st['mesh_renderers']:5d} slots={st['material_slots']:6d} "
          f"mats={len(st['materials']):3d} coll={st['colliders']:4d} mb={n_scripts:3d} inst={in_scene}")

with open(os.path.join(OUT, 'C_ships.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(ship_rows[0].keys()))
    w.writeheader()
    w.writerows(ship_rows)

# combined assets
print('\n=== *_Combined.asset ===')
comb = []
for dirpath, dirnames, filenames in os.walk(os.path.join(ASSETS, '_Project/Generated')):
    for fn in filenames:
        if fn.endswith('_Combined.asset'):
            p = os.path.join(dirpath, fn)
            comb.append({'asset': os.path.relpath(p, ROOT), 'kbytes': round(os.path.getsize(p) / 1024, 1)})
for c in comb:
    print(f"  {c['kbytes']:9.1f} KB  {c['asset']}")
print('combined assets:', len(comb))
with open(os.path.join(OUT, 'C_combined_assets.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=['asset', 'kbytes'])
    w.writeheader()
    w.writerows(comb)
