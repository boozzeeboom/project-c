import sys, os, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unity_parse as up

SCENE = sys.argv[1] if len(sys.argv) > 1 else 'Assets/_Project/Scenes/World/WorldScene_0_0.unity'
s = up.Scene(SCENE)

print('== scene ==', SCENE)
print('docs total:', len(s.docs))
print('gameobjects (non-stripped):', len(s.go))
print('transforms:', len(s.tr), ' (stripped:', sum(1 for t in s.tr.values() if t['stripped']), ')')
print('prefab instances:', len(s.prefab_instances))
print('scene-local renderers:', len(s.renderers), collections.Counter(r['cls'] for r in s.renderers.values()))
print('scene-local meshfilters:', len(s.mesh_filters))
print('scene roots:', len(s.roots))

print('\n== roots (top-level names, with children count) ==')
# map transform fid -> name for root co
def tr_names(fid):
    t = s.tr.get(fid)
    if not t:
        return f'<no-transform fid={fid}>'
    g = s.go.get(t['go'])
    if g:
        return g['name']
    pi = s.prefab_instances.get(t.get('prefabInstance') or 0)
    if pi and pi.get('name'):
        return pi['name'] + ' [prefab-inst]'
    return f'<stripped go={t["go"]} pi={t.get("prefabInstance")}>'

root_rows = []
for fid in s.roots:
    t = s.tr.get(fid)
    if t is None:
        root_rows.append((tr_names(fid), 0, 0))
        continue
    # count subtree transforms
    n = 0
    stack = [fid]
    while stack:
        cur = stack.pop()
        tt = s.tr.get(cur)
        if not tt:
            continue
        n += 1
        stack.extend(tt['children'])
    root_rows.append((tr_names(fid), n, len(t['children'])))
for name, nsub, nch in sorted(root_rows, key=lambda r: -r[1]):
    print(f'{nsub:7d} tr  children={nch:4d}  {name}')

print('\n== prefab instances by source guid ==')
cnt = collections.Counter()
for pi in s.prefab_instances.values():
    cnt[pi['source'][1] if pi['source'] else None] += 1
for guid, n in cnt.most_common(60):
    print(f'{n:6d}  {guid}')
