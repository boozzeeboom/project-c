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
IDX = up.build_guid_index(ASSETS)

SCENE = os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity')
s = up.Scene(SCENE)

print('=== scene roots resolved ===')
for fid in s.roots:
    t = s.tr.get(fid)
    pi = s.prefab_instances.get(fid)
    if t is not None:
        g = s.go.get(t['go'])
        name = g['name'] if g else f'<stripped pi={t["prefabInstance"]}>'
        print(f'TR    {fid:22d} {name}')
    elif pi is not None:
        guid = pi['source'][1] if pi['source'] else None
        p = IDX.get(guid, '??')
        print(f'PREF  {fid:22d} {os.path.relpath(p, ROOT) if p != "??" else "??"}')
    else:
        print(f'???   {fid:22d} (no doc)')

# which fids appear in PrefabInstance parent-less (scene roots)
print('\n=== prefab instances with no parent (scene-root prefabs) ===')
for pi in s.prefab_instances.values():
    if not pi['parent']:
        guid = pi['source'][1] if pi['source'] else None
        p = IDX.get(guid, '??')
        print(f'  {pi["fid"]:22d} mods={pi["mods"]:4d} {os.path.relpath(p, ROOT) if p != "??" else "??"}')
