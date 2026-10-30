import sys, os, json, re, collections
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

path = os.path.join(ASSETS, '_Project/Prefabs/City/PRIMUM_CITY.prefab')
docs = up.load(path)
print('docs in PRIMUM_CITY.prefab:', len(docs), collections.Counter(d.cls for d in docs.values()))

# root transform children
root_tr = 7569307815459584645
print('\nchildren of PRIMUM_CITY root transform:')
t = docs[root_tr]
kids = up.get_ref_list(t.body, 'm_Children')
print('n children:', len(kids))
for kid in kids:
    d = docs.get(kid)
    if d is None:
        print(f'  {kid}: MISSING')
        continue
    g = up.get_ref(d.body, 'm_GameObject')[0]
    nm = up.get_scalar(docs[g].body, 'm_Name', '?') if g and g in docs else '?'
    father = up.get_ref(d.body, 'm_Father')[0]
    print(f'  tr={kid} go={g} name={nm!r} father={father}')

print('\nall PrefabInstance docs in prefab:')
seen = collections.Counter()
for d in docs.values():
    if d.cls != 1001:
        continue
    m = re.search(r'm_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-fA-F]+), type: (\d+)\}', d.body)
    pid = up.get_scalar(d.body, 'm_TransformParent') 
    mm = re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', d.body)
    parent = mm.group(1) if mm else '?'
    if m:
        guid = m.group(2)
        p = IDX.get(guid)
        rel = os.path.relpath(p, ROOT) if p else '??'
        nm = None
        nm_m = re.search(r'propertyPath: m_Name\n[ \t]*value: (.+)\n', d.body)
        if nm_m:
            nm = nm_m.group(1).strip()
        print(f'  pi={d.fid} parent={parent} name={nm!r} src={rel}')
        seen[rel] += 1
print('\nby source:')
for k, v in seen.most_common():
    print(f'  {v:4d}  {k}')
