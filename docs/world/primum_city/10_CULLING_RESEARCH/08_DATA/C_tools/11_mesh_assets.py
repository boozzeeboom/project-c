"""Parse Unity mesh .asset collections (YAML) -> per-mesh vert/tri/submesh stats,
then resolve the scene's m_Mesh overrides against them.

Read-only. Streams the YAML line by line.
"""
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

HDR = re.compile(r'^--- !u!43 &(-?\d+)')
NAME = re.compile(r'^  m_Name: (.*)$')
VCNT = re.compile(r'^    m_VertexCount: (\d+)$')
ICNT = re.compile(r'^    indexCount: (\d+)$')
SUBS = re.compile(r'^  m_SubMeshes:$')

REBASED = os.path.join(ASSETS, '_Project/Art/Models/Primum/Rebased')


def parse_mesh_asset(path):
    """Return dict fileID -> {name, verts, idx, tris, submeshes}."""
    meshes = {}
    cur_fid = None
    cur = None
    in_submeshes = False
    with open(path, 'r', encoding='utf-8', errors='replace') as f:
        for line in f:
            m = HDR.match(line)
            if m:
                if cur is not None:
                    meshes[cur_fid] = cur
                cur_fid = int(m.group(1))
                cur = {'name': '', 'verts': 0, 'idx': 0, 'tris': 0, 'submeshes': 0}
                in_submeshes = False
                continue
            if cur is None:
                continue
            if line.startswith('  m_SubMeshes:'):
                in_submeshes = True
                continue
            if in_submeshes:
                mm = ICNT.match(line)
                if mm:
                    n = int(mm.group(1))
                    cur['idx'] += n
                    cur['submeshes'] += 1
                elif line.startswith('  ') and not line.startswith('    ') and line[2:3] not in (' ', '-'):
                    in_submeshes = False
            if not cur['name']:
                mm = NAME.match(line)
                if mm:
                    cur['name'] = mm.group(1).strip()
                    continue
            mm = VCNT.match(line)
            if mm:
                cur['verts'] = int(mm.group(1))
    if cur is not None:
        meshes[cur_fid] = cur
    for m in meshes.values():
        m['tris'] = m['idx'] // 3
    return meshes


def main():
    per_asset = []
    all_meshes = {}   # (guid, fid) -> stats + asset path
    for dirpath, dirnames, filenames in os.walk(REBASED):
        for fn in sorted(filenames):
            if not fn.endswith('.asset'):
                continue
            path = os.path.join(dirpath, fn)
            meta = path + '.meta'
            guid = None
            try:
                with open(meta, 'r', encoding='utf-8', errors='replace') as f:
                    mm = re.search(r'guid: ([0-9a-f]{32})', f.read(200))
                    if mm:
                        guid = mm.group(1)
            except OSError:
                pass
            if not guid:
                print('NO GUID', path)
                continue
            t0 = os.path.getsize(path)
            meshes = parse_mesh_asset(path)
            tot_v = sum(m['verts'] for m in meshes.values())
            tot_t = sum(m['tris'] for m in meshes.values())
            sub_hist = collections.Counter(m['submeshes'] for m in meshes.values())
            per_asset.append({
                'asset': os.path.relpath(path, ROOT),
                'guid': guid,
                'mb': round(t0 / 1048576, 2),
                'meshes': len(meshes),
                'vertices': tot_v,
                'triangles': tot_t,
                'submesh_hist': ';'.join(f'{k}x{v}' for k, v in sorted(sub_hist.items())),
            })
            for fid, m in meshes.items():
                all_meshes[(guid, fid)] = dict(m, asset=os.path.relpath(path, ROOT))
            print(f'{os.path.basename(path):70s} meshes={len(meshes):5d} verts={tot_v:9d} tris={tot_t:8d}')

    print('\ntotal meshes in rebased assets:', len(all_meshes))
    print('total verts:', sum(m['verts'] for m in all_meshes.values()))
    print('total tris:', sum(m['tris'] for m in all_meshes.values()))

    with open(os.path.join(OUT, 'C_rebased_meshes.csv'), 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(per_asset[0].keys()))
        w.writeheader()
        w.writerows(per_asset)

    # ---- resolve scene overrides ----
    SCENE = os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity')
    s = up.Scene(SCENE)
    body = s.docs[1019326845].body
    refs = re.findall(r'propertyPath: m_Mesh\n[ \t]*value: \n[ \t]*objectReference: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: (\d+)\}', body)
    print('\nscene m_Mesh overrides:', len(refs))
    used = collections.Counter((g, int(fid)) for fid, g, t in refs)
    resolved = 0
    missing = 0
    tot_verts_ref = 0
    tot_tris_ref = 0
    rows = []
    sub_hist = collections.Counter()
    for (g, fid), n in used.items():
        m = all_meshes.get((g, fid))
        if m is None:
            missing += 1
            continue
        resolved += 1
        tot_verts_ref += m['verts'] * n
        tot_tris_ref += m['tris'] * n
        sub_hist[m['submeshes']] += n
        rows.append({
            'guid': g, 'fileID': fid, 'name': m['name'], 'asset': m['asset'],
            'verts': m['verts'], 'tris': m['tris'], 'submeshes': m['submeshes'], 'refs': n,
        })
    print('resolved distinct meshes:', resolved, ' missing:', missing)
    print('draw triangles (sum over 16,704 refs):', tot_tris_ref)
    print('draw vertices:', tot_verts_ref)
    print('submesh histogram of referenced meshes (by refs):', dict(sorted(sub_hist.items())))
    rows.sort(key=lambda r: -r['refs'])
    with open(os.path.join(OUT, 'C_city_mesh_refs.csv'), 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['guid', 'fileID', 'name', 'asset', 'verts', 'tris', 'submeshes', 'refs'])
        w.writeheader()
        w.writerows(rows)

    # per-asset summary of referenced geometry
    by_asset = collections.Counter()
    tris_by_asset = collections.Counter()
    for r in rows:
        by_asset[r['asset']] += r['refs']
        tris_by_asset[r['asset']] += r['tris'] * r['refs']
    print('\nreferenced objects / tris by asset:')
    for a, n in by_asset.most_common():
        print(f'  {n:6d} obj {tris_by_asset[a]:9d} tris  {a}')

    json.dump({
        'refs': len(refs),
        'distinct': len(used),
        'resolved': resolved,
        'missing': missing,
        'draw_tris': tot_tris_ref,
        'draw_verts': tot_verts_ref,
        'submesh_hist': {str(k): v for k, v in sub_hist.items()},
    }, open(os.path.join(OUT, 'C_city_mesh_refs_summary.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
