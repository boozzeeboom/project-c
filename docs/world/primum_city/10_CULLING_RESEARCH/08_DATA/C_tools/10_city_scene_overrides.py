import sys, os, re, collections, json
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
OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SCENE = os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity')
s = up.Scene(SCENE)
PI = 1019326845
body = s.docs[PI].body
print('body size MB:', round(len(body) / 1048576, 1))

# --- mesh overrides ---
mesh_refs = re.findall(r'propertyPath: m_Mesh\n[ \t]*value: \n[ \t]*objectReference: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: (\d+)\}', body)
print('m_Mesh overrides:', len(mesh_refs))
by_guid = collections.Counter(g for _, g, _ in mesh_refs)
print('distinct mesh guids:', len(by_guid))
rows = []
for guid, n in by_guid.most_common():
    p = IDX.get(guid, '??')
    rel = os.path.relpath(p, ROOT) if p != '??' else '??'
    rows.append({'guid': guid, 'count': n, 'path': rel})
    print(f'  {n:6d}  {guid}  {rel}')

# --- material overrides per slot ---
mat_slots = collections.Counter(re.findall(r"propertyPath: 'm_Materials\.Array\.data\[(\d)\]'", body))
mats = re.findall(r"propertyPath: 'm_Materials\.Array\.data\[\d\]'\n[ \t]*value: \n[ \t]*objectReference: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: (\d+)\}", body)
print('\nmaterial slot overrides:', dict(sorted(mat_slots.items())), 'total', len(mats))
mat_guids = collections.Counter(g for _, g, _ in mats if g)
print('distinct material guids:', len(mat_guids))
for guid, n in mat_guids.most_common(15):
    p = IDX.get(guid, '??')
    print(f'  {n:6d}  {guid}  {os.path.relpath(p, ROOT) if p != "??" else "??"}')

# --- active overrides ---
off = len(re.findall(r'propertyPath: m_IsActive\n[ \t]*value: 0', body))
on = len(re.findall(r'propertyPath: m_IsActive\n[ \t]*value: 1', body))
print('\nm_IsActive 0:', off, ' 1:', on)
targets = set(re.findall(r'target: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: (\d+)\}', body))
print('distinct targets:', len(targets))
by_type = collections.Counter(t[2] for t in targets)
print('target types:', dict(by_type))

# --- scale overrides ---
scales = re.findall(r'propertyPath: m_LocalScale\.x\n[ \t]*value: ([-\d.eE]+)', body)
sc = collections.Counter(scales)
print('\nlocalScale.x values:', dict(sc.most_common(8)))

with open(os.path.join(OUT, 'C_city_mesh_overrides.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    import csv
    w = csv.DictWriter(f, fieldnames=['guid', 'count', 'path'])
    w.writeheader()
    w.writerows(rows)
