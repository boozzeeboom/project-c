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


def dump_prefab(path):
    st = up.prefab_stats(path)
    st2 = {k: v for k, v in st.items() if not isinstance(v, set)}
    st2['unique_meshes'] = len(st['meshes'])
    st2['unique_materials'] = len(st['materials'])
    print(f'\n== {os.path.relpath(path, ROOT)} ==')
    print(json.dumps(st2, indent=1, ensure_ascii=False))


for rel in [
    '_Project/Prefabs/City/PRIMUM_CITY.prefab',
    '_Project/Prefabs/City/Doors/PM_DOOR_SINGLE_PLANK_A.prefab',
    '_Project/Prefabs/City/Doors/PM_DOOR_SINGLE_PANEL_B.prefab',
    '_Project/Prefabs/City/Doors/PM_DOOR_SINGLE_ARCH_C.prefab',
    '_Project/Prefabs/City/Doors/PM_DOOR_DOUBLE_A.prefab',
    '_Project/Prefabs/City/Doors/PM_DOOR_PALACE_DOUBLE_A.prefab',
    '_Project/Prefabs/City/Lights/PM_LIGHT_STREET_POST.prefab',
    '_Project/Prefabs/City/Lights/PM_LIGHT_INTERIOR_SCONCE.prefab',
]:
    p = os.path.join(ASSETS, rel)
    if os.path.exists(p):
        dump_prefab(p)
    else:
        print('MISSING', p)
