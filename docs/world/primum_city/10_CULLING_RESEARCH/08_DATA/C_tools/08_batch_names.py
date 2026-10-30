import sys, os, csv, collections, re, statistics
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
OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHUNKS = os.path.join(ROOT, 'Assets/_Project/Art/Models/Primum/Chunks')

rows = []
for fn in sorted(os.listdir(CHUNKS)):
    if not fn.lower().endswith('.fbx'):
        continue
    p = os.path.join(CHUNKS, fn)
    chunk = os.path.splitext(fn)[0]
    s = fbx_parse.summarize(p)
    geo_by_id = s['geometries']
    geom_to_model = {c: pp for c, pp in s['conns'] if c in geo_by_id}
    model_by_id = {m[0]: m for m in s['models']}
    for gid, g in geo_by_id.items():
        mid = geom_to_model.get(gid)
        mname = model_by_id.get(mid, (None, g['name'], ''))[1].split('\x00')[0]
        rows.append((chunk, mname, g['tris'], g['submeshes'], g['vertices']))

# classify names
pat = re.compile(r'^(?P<chunk>.+?)_(?P<kind>M|INT|BASE|EXT|PROPS|DECOR)_(?P<material>.+)$')
kinds = collections.Counter()
dup = collections.Counter()
base_material = collections.Counter()
examples = {}
for chunk, name, tris, subs, verts in rows:
    n = name
    suffix = ''
    m = re.match(r'^(.*?)(\.\d{3})$', n)
    if m:
        n, suffix = m.group(1), m.group(2)
    if suffix:
        dup[n] += 1
    mm = pat.match(n)
    kind = mm.group('kind') if mm else 'OTHER'
    kinds[kind] += 1
    if mm:
        base_material[mm.group('material')] += 1
print('kind histogram:', dict(kinds))
print('names with .NNN suffix (duplicates):', sum(dup.values()), 'unique bases:', len(dup))
print('top duplicate bases:')
for k, v in dup.most_common(15):
    print(f'   {v:4d}  {k}')
print('\nbase material histogram (before .NNN collapse):')
for k, v in base_material.most_common(30):
    print(f'   {v:5d}  {k}')

# how many distinct chunk_M_material combos
combo = collections.Counter()
for chunk, name, tris, subs, verts in rows:
    n = re.sub(r'\.\d{3}$', '', name)
    combo[n] += 1
print('\ndistinct full names:', len(combo), 'of', len(rows), 'objects')
print('objects per distinct name: mean %.2f' % (len(rows) / len(combo)))

# names not starting with chunk name
odd = [r for r in rows if not r[1].startswith(r[0])]
print('\nobjects whose name does not start with chunk:', len(odd))
for r in odd[:20]:
    print('   ', r)
