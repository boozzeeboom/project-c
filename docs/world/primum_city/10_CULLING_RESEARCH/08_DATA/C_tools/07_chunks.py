import sys, os, csv, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
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
OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # 08_DATA
CHUNKS = os.path.join(ROOT, 'Assets/_Project/Art/Models/Primum/Chunks')

rows = []
per_chunk = []
mat_names_total = collections.Counter()

for fn in sorted(os.listdir(CHUNKS)):
    if not fn.lower().endswith('.fbx'):
        continue
    p = os.path.join(CHUNKS, fn)
    chunk = os.path.splitext(fn)[0]
    try:
        s = fbx_parse.summarize(p)
    except Exception as e:
        print('ERR', fn, e)
        continue
    geo_by_id = s['geometries']
    geom_to_model = {}
    for child, parent in s['conns']:
        if child in geo_by_id:
            geom_to_model[child] = parent
    model_by_id = {m[0]: m for m in s['models']}
    mesh_models = [m for m in s['models'] if m[2] == 'Mesh']
    null_models = [m for m in s['models'] if m[2] == 'Null']
    chunk_tris = 0
    chunk_verts = 0
    chunk_slots = 0
    for gid, g in geo_by_id.items():
        mid = geom_to_model.get(gid)
        mname = model_by_id.get(mid, (None, g['name'], ''))[1].split('\x00')[0]
        gname = g['name'].split('\x00')[0]
        chunk_tris += g['tris']
        chunk_verts += g['vertices']
        chunk_slots += g['submeshes']
        rows.append({
            'chunk': chunk,
            'object': mname or gname,
            'geometry': gname,
            'vertices': g['vertices'],
            'indices': g['idx'],
            'polygons': g['polys'],
            'triangles': g['tris'],
            'submeshes': g['submeshes'],
            'material_indices': ';'.join(str(x) for x in g['mat_indices']),
            'mapping': g.get('mapping', ''),
        })
    mats = [m[1].split('\x00')[0] for m in s['materials']]
    for m in mats:
        mat_names_total[m] += 1
    per_chunk.append({
        'chunk': chunk,
        'fbx_mb': round(os.path.getsize(p) / 1048576, 2),
        'mesh_models': len(mesh_models),
        'null_models': len(null_models),
        'geometries': len(geo_by_id),
        'fbx_materials': len(mats),
        'vertices': chunk_verts,
        'triangles': chunk_tris,
        'submesh_slots': chunk_slots,
        'materials': ';'.join(sorted(mats)),
    })
    print(f'{chunk:42s} mesh={len(mesh_models):4d} null={len(null_models):2d} geoms={len(geo_by_id):4d} '
          f'tris={chunk_tris:8d} verts={chunk_verts:9d} slots={chunk_slots:4d}')

print('\nTOTAL mesh models:', sum(r['mesh_models'] for r in per_chunk))
print('TOTAL tris:', sum(r['triangles'] for r in per_chunk))
print('TOTAL verts:', sum(r['vertices'] for r in per_chunk))
print('TOTAL slots:', sum(r['submesh_slots'] for r in per_chunk))
print('unique material names:', len(mat_names_total))

with open(os.path.join(OUT, 'C_primum_batches.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader()
    w.writerows(rows)

with open(os.path.join(OUT, 'C_primum_chunks.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(per_chunk[0].keys()))
    w.writeheader()
    w.writerows(per_chunk)

# submesh distribution + tris distribution for batches
sub_hist = collections.Counter(r['submeshes'] for r in rows)
print('\nsubmesh histogram (batches):', dict(sorted(sub_hist.items())))
tris = sorted(r['triangles'] for r in rows)
import statistics
print('tris per batch: n=%d min=%d median=%d p90=%d p99=%d max=%d' % (
    len(tris), tris[0], statistics.median(tris), tris[int(.9 * len(tris))], tris[int(.99 * len(tris))], tris[-1]))
print('top10 batches by tris:')
for r in sorted(rows, key=lambda r: -r['triangles'])[:10]:
    print('   %8d  %-42s %s' % (r['triangles'], r['object'], r['chunk']))
