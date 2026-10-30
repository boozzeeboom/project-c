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

CACHE = {}


def prefab_stats_cached(path):
    if path not in CACHE:
        st = up.prefab_stats(path)
        # nested prefab instances inside
        nested = collections.Counter()
        try:
            docs = up.load(path)
            for d in docs.values():
                if d.cls == 1001 and not d.stripped:
                    m = up.re.search(r'm_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-fA-F]+), type: (\d+)\}', d.body)
                    if m:
                        nested[m.group(2)] += 1
        except Exception as e:
            st['load_error'] = str(e)
        st['nested'] = nested
        CACHE[path] = st
    return CACHE[path]


def prefab_renderer_count(guid, depth=0, _seen=None):
    """Recursively count renderers contributed by a prefab guid (cache by guid)."""
    key = ('rc', guid)
    if key in CACHE:
        return CACHE[key]
    path = IDX.get(guid)
    if not path or depth > 4:
        CACHE[key] = 0
        return 0
    st = prefab_stats_cached(path)
    total = st['mesh_renderers'] + st['skinned'] + st['particle'] + st['line'] + st['sprite']
    for nguid, n in st['nested'].items():
        total += n * prefab_renderer_count(nguid, depth + 1)
    CACHE[key] = total
    return total


def prefab_total_renderers_path(path, depth=0):
    st = prefab_stats_cached(path)
    total = st['mesh_renderers'] + st['skinned'] + st['particle'] + st['line'] + st['sprite']
    for nguid, n in st['nested'].items():
        total += n * prefab_renderer_count(nguid, depth + 1)
    return total


def main():
    scene_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ASSETS, '_Project/Scenes/World/WorldScene_0_0.unity')
    s = up.Scene(scene_path)

    # build go -> transform
    tr_by_go = {}
    for tfid, t in s.tr.items():
        if t['go'] and not t['stripped']:
            tr_by_go[t['go']] = tfid

    # direct renderers per transform (renderer's GO -> transform)
    renderers_by_tr = collections.Counter()
    for r in s.renderers.values():
        tfid = tr_by_go.get(r['go'])
        if tfid:
            renderers_by_tr[tfid] += 1

    # index prefab instance by parent transform
    pi_by_parent = collections.defaultdict(list)
    for pi in s.prefab_instances.values():
        if pi['parent']:
            pi_by_parent[pi['parent']].append(pi)

    def subtree_stats(root_tfid, depth_limit=30):
        """Walk the scene transform subtree, expanding prefab instances one level deep
        (renderer counts from prefab assets)."""
        stats = {
            'transforms': 0, 'direct_renderers': 0, 'prefab_renderers': 0,
            'prefab_instances': 0, 'colliders': 0, 'stripped': 0,
            'prefabs': collections.Counter(), 'names': [],
        }
        stack = [root_tfid]
        while stack:
            cur = stack.pop()
            t = s.tr.get(cur)
            if not t:
                continue
            stats['transforms'] += 1
            if t['stripped']:
                stats['stripped'] += 1
            stats['direct_renderers'] += renderers_by_tr.get(cur, 0)
            for pi in pi_by_parent.get(cur, []):
                stats['prefab_instances'] += 1
                guid = pi['source'][1] if pi['source'] else None
                pth = IDX.get(guid, '??')
                stats['prefabs'][os.path.relpath(pth, ROOT) if pth != '??' else '??'] += 1
                stats['prefab_renderers'] += prefab_renderer_count(guid)
            stack.extend(t['children'])
        return stats

    # locate WorldRoot_0_0
    root_tf = None
    for gofid, g in s.go.items():
        if g['name'] == 'WorldRoot_0_0':
            root_tf = tr_by_go.get(gofid)
            break
    print('WorldRoot_0_0 transform:', root_tf)
    if root_tf is None:
        return

    print('\n=== WorldRoot_0_0 children ===')
    rows = []
    for ch in s.tr[root_tf]['children']:
        t = s.tr.get(ch)
        if not t:
            continue
        g = s.go.get(t['go'])
        name = g['name'] if g else f'<stripped go={t["go"]}>'
        st = subtree_stats(ch)
        rows.append((name, st))
    # sort by transforms
    for name, st in sorted(rows, key=lambda r: -r[1]['transforms']):
        print(f'{name:28s} tr={st["transforms"]:6d} dirR={st["direct_renderers"]:4d} '
              f'prefR={st["prefab_renderers"]:6d} pi={st["prefab_instances"]:5d} '
              f'totalR={st["direct_renderers"] + st["prefab_renderers"]:6d}')
        for p, n in st['prefabs'].most_common(8):
            print(f'      {n:5d} x {p}')

    # totals
    print('\n=== totals under WorldRoot_0_0 ===')
    total = subtree_stats(root_tf)
    print(json.dumps({k: (dict(v) if isinstance(v, collections.Counter) else v) for k, v in total.items()},
                     indent=1, ensure_ascii=False)[:4000])


if __name__ == '__main__':
    main()
