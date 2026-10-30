"""Per-batch AABB extents from the Rebased mesh assets + per-chunk structure summary."""
import sys, os, re, csv, statistics, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


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
REBASED = os.path.join(ROOT, 'Assets/_Project/Art/Models/Primum/Rebased')

HDR = re.compile(r'^--- !u!43 &(-?\d+)')
NAME = re.compile(r'^  m_Name: (.*)$')
CENTER = re.compile(r'^      m_Center: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}$')
EXTENT = re.compile(r'^      m_Extent: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}$')

rows = []
per_chunk = collections.defaultdict(lambda: {'batches': [], 'props': [], 'all': []})

for dirpath, dirnames, filenames in os.walk(REBASED):
    for fn in sorted(filenames):
        if not fn.endswith('.asset') or fn.endswith('_LAMPS_meshes.asset'):
            continue
        path = os.path.join(dirpath, fn)
        cur = None
        name = ''
        center = None
        extent = None
        meshes = []
        with open(path, 'r', encoding='utf-8', errors='replace') as f:
            for line in f:
                if HDR.match(line):
                    if cur is not None and extent:
                        meshes.append((name, center, extent))
                    cur = HDR.match(line).group(1)
                    name = ''
                    center = None
                    extent = None
                    continue
                if not name:
                    m = NAME.match(line)
                    if m:
                        name = m.group(1).strip()
                        continue
                m = CENTER.match(line)
                if m:
                    center = tuple(float(m.group(i)) for i in (1, 2, 3))
                    continue
                m = EXTENT.match(line)
                if m:
                    extent = tuple(float(m.group(i)) for i in (1, 2, 3))
        if cur is not None and extent:
            meshes.append((name, center, extent))
        chunk = fn.replace('_YUP_meshes.asset', '')
        for name, center, extent in meshes:
            base = re.sub(r'\.\d{3}$', '', name)
            is_batch = bool(re.search(r'_(M|INT)_[A-Za-z0-9_.]+$', base)) and not re.search(
                r'(DOOR|LAMP|LMP|FURN|WINDOW|FRAME|JAMB|KNOB|PANEL|LEAF)', base)
            size = tuple(2 * e for e in extent)
            budget = {'chunk': chunk, 'name': name, 'is_batch': int(is_batch),
                      'size_x': round(size[0], 2), 'size_y': round(size[1], 2), 'size_z': round(size[2], 2),
                      'extent_max': round(max(size), 2)}
            per_chunk[chunk]['all'].append(budget)
            per_chunk[chunk]['batches' if is_batch else 'props'].append(budget)
            rows.append(budget)
        print(f'{chunk:44s} meshes={len(meshes):5d} batches={sum(1 for m in meshes if re.search(r"_(M|INT)_", m[0]))}')

print('\n=== batch vs prop AABB extents (m) ===')
for key in ('batches', 'props'):
    xs = sorted(r['extent_max'] for r in rows if (r['is_batch'] == (key == 'batches')))
    if not xs:
        continue
    print(f'{key}: n={len(xs)} median={statistics.median(xs):.1f} p90={xs[int(.9*len(xs))]:.1f} '
          f'p99={xs[int(.99*len(xs))]:.1f} max={xs[-1]:.1f}')

print('\n=== per-chunk merged-batch AABB (max extent) ===')
chunk_rows = []
for chunk, d in sorted(per_chunk.items()):
    bs = sorted(x['extent_max'] for x in d['batches'])
    ps = sorted(x['extent_max'] for x in d['props'])
    chunk_rows.append({
        'chunk': chunk,
        'meshes': len(d['all']),
        'batches': len(d['batches']),
        'props': len(d['props']),
        'batch_median_ext_m': round(statistics.median(bs), 1) if bs else '',
        'batch_p90_ext_m': round(bs[int(.9 * len(bs))], 1) if bs else '',
        'batch_max_ext_m': round(bs[-1], 1) if bs else '',
        'prop_median_ext_m': round(statistics.median(ps), 2) if ps else '',
        'prop_p90_ext_m': round(ps[int(.9 * len(ps))], 2) if ps else '',
    })
    print(f"{chunk:44s} batches={len(bs):4d} median_ext={chunk_rows[-1]['batch_median_ext_m']:8.1f} "
          f"max={chunk_rows[-1]['batch_max_ext_m']:8.1f} | props={len(ps):5d} "
          f"median={chunk_rows[-1]['prop_median_ext_m']}")

with open(os.path.join(OUT, 'C_batch_bounds.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=['chunk', 'name', 'is_batch', 'size_x', 'size_y', 'size_z', 'extent_max'])
    w.writeheader()
    w.writerows(rows)
with open(os.path.join(OUT, 'C_chunk_structure.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(chunk_rows[0].keys()))
    w.writeheader()
    w.writerows(chunk_rows)
