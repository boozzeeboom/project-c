import sys, os, re, collections
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
IDX = up.build_guid_index(ASSETS)
s = up.Scene(os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity'))

tr_by_go = {}
for tfid, t in s.tr.items():
    if t['go'] and not t['stripped']:
        tr_by_go[t['go']] = tfid

def find_tr(go):
    """walk stripped parents via attached GO references; simplest: use tr_by_go or search stripped tr whose go==go"""
    if go in tr_by_go:
        return tr_by_go[go]
    for tfid, t in s.tr.items():
        if t['go'] == go and t['stripped']:
            return tfid
    return None

# renderer -> parent chain
def path(root_tfid):
    names = []
    cur = root_tfid
    guard = 0
    while cur and guard < 80:
        guard += 1
        t = s.tr.get(cur)
        if not t:
            break
        g = s.go.get(t['go'])
        if g:
            names.append(g['name'])
        else:
            pi = s.prefab_instances.get(t.get('prefabInstance') or 0)
            names.append(f'<{pi["name"] if pi and pi.get("name") else "stripped"}>')
        cur = t['father']
    names.reverse()
    return names

print('=== scene-local MeshRenderers by root (first path element) ===')
cnt = collections.Counter()
for r in s.renderers.values():
    if r['cls'] != 23:
        continue
    tf = find_tr(r['go'])
    p = path(tf) if tf else ['<no tr>']
    cnt[p[0] if p else '?'] += 1
for k, v in cnt.most_common():
    print(f'  {v:5d}  {k}')

print('\n=== scene-local MeshFilters referencing LAMPS meshes ===')
lamps_guids = set()
for dp, dn, fns in os.walk(os.path.join(ASSETS, '_Project/Art/Models/Primum/Rebased')):
    for fn in fns:
        if fn.endswith('_LAMPS_meshes.asset.meta'):
            with open(os.path.join(dp, fn), 'r', encoding='utf-8', errors='replace') as f:
                m = re.search(r'guid: ([0-9a-f]{32})', f.read(200))
                if m:
                    lamps_guids.add(m.group(1))
n = 0
for mf in s.mesh_filters.values():
    if mf['guid'] in lamps_guids:
        n += 1
        tf = find_tr(mf['go'])
        p = path(tf) if tf else []
        if n <= 12:
            print('   ', '/'.join(p))
print('total lamps meshfilters:', n)

print('\n=== all scene-local meshfilter guids by asset (top 15) ===')
g_cnt = collections.Counter()
for mf in s.mesh_filters.values():
    p = IDX.get(mf['guid'], '??')
    g_cnt[os.path.relpath(p, ROOT) if p != '??' else '??'] += 1
for k, v in g_cnt.most_common(15):
    print(f'  {v:5d}  {k}')

print('\n=== scene-local renderers by class and root ===')
for cls in (23, 137, 199, 120):
    c = sum(1 for r in s.renderers.values() if r['cls'] == cls)
    print(f'  cls {cls}: {c}')
