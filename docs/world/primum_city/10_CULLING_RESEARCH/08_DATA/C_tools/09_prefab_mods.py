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

# map prefab-instance -> source guid
for d in docs.values():
    if d.cls != 1001:
        continue
    m = re.search(r'm_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-fA-F]+), type: (\d+)\}', d.body)
    guid = m.group(2) if m else '?'
    src = IDX.get(guid, '??')
    props = collections.Counter(re.findall(r'propertyPath: ([A-Za-z0-9_.]+)', d.body))
    print(f'pi={d.fid} src={os.path.basename(src)}')
    print('   total mods:', sum(props.values()), ' distinct targets:', len(set(re.findall(r'target: \{fileID: (-?\d+),', d.body))))
    print('   props:', dict(props.most_common(12)))
    # count isActive=0 mods
    off = len(re.findall(r'propertyPath: m_IsActive\n[ \t]*value: 0', d.body))
    on = len(re.findall(r'propertyPath: m_IsActive\n[ \t]*value: 1', d.body))
    en_off = len(re.findall(r'propertyPath: m_Enabled\n[ \t]*value: 0', d.body))
    print(f'   m_IsActive=0: {off}, m_IsActive=1: {on}, m_Enabled=0: {en_off}')
    print('   removedComponents:', len(re.findall(r'm_RemovedComponents: \[\]', d.body)), 'empty' )
