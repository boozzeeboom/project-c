import sys, os, json, collections
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
print('root:', ROOT, 'assets:', ASSETS, os.path.isdir(ASSETS))

idx = up.build_guid_index(ASSETS)
print('guids indexed:', len(idx))

SCENE = os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity')
s = up.Scene(SCENE)
cnt = collections.Counter()
for pi in s.prefab_instances.values():
    cnt[pi['source'][1] if pi['source'] else None] += 1

print('\n== prefab instances -> asset path ==')
rows = []
for guid, n in cnt.most_common():
    p = idx.get(guid, '??')
    try:
        rel = os.path.relpath(p, ROOT)
    except Exception:
        rel = p
    rows.append({'guid': guid, 'count': n, 'path': rel})
    print(f'{n:6d}  {guid}  {rel}')

with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '_prefab_instances.json'), 'w', encoding='utf-8') as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)
