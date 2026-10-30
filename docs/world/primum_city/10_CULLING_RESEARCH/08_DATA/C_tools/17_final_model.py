"""Final composite numbers for the C audit: per-chunk structure, door model, draw-call scenarios."""
import sys, os, csv, re, json, collections, statistics
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
DATA = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

batches = list(csv.DictReader(open(os.path.join(DATA, 'C_primum_batches.csv'), encoding='utf-8-sig')))
chunks = list(csv.DictReader(open(os.path.join(DATA, 'C_primum_chunks.csv'), encoding='utf-8-sig')))
bounds = list(csv.DictReader(open(os.path.join(DATA, 'C_batch_bounds.csv'), encoding='utf-8-sig')))

BATCH_RE = re.compile(r'_(M|INT)_[A-Za-z0-9_.]+$')
PROPISH = re.compile(r'(DOOR|LAMP|LMP|FURN|WINDOW|FRAME|JAMB|KNOB|PANEL|LEAF|BOOTH|SIGN|DECOR)', re.I)


def is_structural_batch(name):
    base = re.sub(r'\.\d{3}$', '', name)
    return bool(BATCH_RE.search(base)) and not bool(PROPISH.search(base))


# per chunk totals from batches CSV
per = collections.defaultdict(lambda: {'batches': 0, 'batch_tris': 0, 'batch_slots': 0,
                                       'props': 0, 'prop_tris': 0, 'prop_slots': 0,
                                       'objects': 0, 'tris': 0, 'slots': 0, 'mats': set()})
for r in batches:
    c = r['chunk']
    d = per[c]
    d['objects'] += 1
    d['tris'] += int(r['triangles'])
    d['slots'] += int(r['submeshes'])
    if is_structural_batch(r['object']):
        d['batches'] += 1
        d['batch_tris'] += int(r['triangles'])
        d['batch_slots'] += int(r['submeshes'])
    else:
        d['props'] += 1
        d['prop_tris'] += int(r['triangles'])
        d['prop_slots'] += int(r['submeshes'])

bnd = collections.defaultdict(dict)
for b in bounds:
    bnd[b['chunk']].setdefault('batch_ext', []).append(float(b['extent_max']))
    bnd[b['chunk']].setdefault('prop_ext', []).append(float(b['extent_max']))

rows = []
tot = collections.Counter()
for c, d in sorted(per.items()):
    be = bnd[c].get('batch_ext', [])
    pe = bnd[c].get('prop_ext', [])
    rows.append({
        'chunk': c,
        'objects': d['objects'],
        'structural_batches': d['batches'],
        'props': d['props'],
        'tris_total': d['tris'],
        'batch_tris': d['batch_tris'],
        'prop_tris': d['prop_tris'],
        'slots_total': d['slots'],
        'batch_slots': d['batch_slots'],
        'prop_slots': d['prop_slots'],
        'avg_tris_per_object': round(d['tris'] / max(1, d['objects']), 1),
        'batch_median_extent_m': round(statistics.median(be), 1) if be else '',
        'batch_max_extent_m': round(max(be), 1) if be else '',
        'prop_median_extent_m': round(statistics.median(pe), 2) if pe else '',
    })
    for k in ('objects', 'batches', 'props', 'tris', 'batch_tris',
              'prop_tris', 'slots', 'batch_slots', 'prop_slots'):
        tot[k] += d[k]

with open(os.path.join(DATA, 'C_city_chunks_combined.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader()
    w.writerows(rows)

print('=== CITY STRUCTURE (HEAD, mesh assets) ===')
print(json.dumps(dict(tot), indent=1))
print('chunks:', len(rows))
print('avg objects/chunk: %.0f  median batches/chunk: %.0f  median props/chunk: %.0f' % (
    tot['objects'] / len(rows),
    statistics.median([r['structural_batches'] for r in rows]),
    statistics.median([r['props'] for r in rows])))
print('share of tris in structural batches: %.1f%%' % (100 * tot['batch_tris'] / max(1, tot['tris'])))
print('share of slots in structural batches: %.1f%%' % (100 * tot['batch_slots'] / max(1, tot['slots'])))

# asset-based (authoritative for the scene) totals
ASSET_OBJECTS, ASSET_SLOTS, ASSET_TRIS, ASSET_VERTS = 16704, 20954, 5844674, 11042258
print('\nasset-based: objects=%d slots=%d tris=%d verts=%d' % (ASSET_OBJECTS, ASSET_SLOTS, ASSET_TRIS, ASSET_VERTS))
print('FBX-based  : objects=%d slots=%d tris=%d' % (tot['objects'], tot['slots'], tot['tris']))
print('merged structural batches share of tris (FBX rule): %.1f%%' % (100 * tot['batch_tris'] / max(1, tot['tris'])))
tot['objects'] = ASSET_OBJECTS
tot['slots'] = ASSET_SLOTS
tot['tris'] = ASSET_TRIS

# submesh histogram split
bsub = collections.Counter()
for r in batches:
    bsub[int(r['submeshes'])] += 1
print('submesh histogram (FBX objects):', dict(sorted(bsub.items())))

# ---------- DOOR MODEL ----------
doors = list(csv.DictReader(open(os.path.join(DATA, 'C_doors.csv'), encoding='utf-8-sig')))
inst_total = sum(int(r['instances_in_scene']) for r in doors)
rend_total = sum(int(r['instances_in_scene']) * int(r['mesh_renderers']) for r in doors)
slot_total = sum(int(r['instances_in_scene']) * int(r['material_slots']) for r in doors)
drows = []
for r in doors:
    n = int(r['instances_in_scene'])
    if not n:
        continue
    drows.append({
        'prefab': r['prefab'],
        'instances': n,
        'renderers_per_instance': int(r['mesh_renderers']),
        'slots_per_instance': int(r['material_slots']),
        'renderers_total': n * int(r['mesh_renderers']),
        'slots_total': n * int(r['material_slots']),
        'colliders_per_instance': int(r['colliders']),
        'colliders_total': n * int(r['colliders']),
        'slots_detail': r['renderers_per_slot_detail'],
    })
with open(os.path.join(DATA, 'C_doors_model.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(drows[0].keys()))
    w.writeheader()
    w.writerows(drows)

print('\n=== DOORS (HEAD) ===')
print('instances:', inst_total, 'renderers:', rend_total, 'slots:', slot_total)
print('slots/instance: %.2f  renderers/instance: %.2f' % (slot_total / inst_total, rend_total / inst_total))
# state in which the measurement was made (РОАДМАП §2.1): 2 363 инстанса / 4 949 ренд. / 12 189 слотов
M_INST, M_SLOTS = 2363, 12189
print('\n=== MEASURED-STATE MODEL (2 363 инстанса / 12 189 слотов / 4 949 рендеров) ===')
for name, dc in (('A shadow1000/2', 26981), ('B shadow250/1', 21118)):
    doors_dc = {'A shadow1000/2': 22027, 'B shadow250/1': 17766}[name]
    nodoor_dc = dc - doors_dc
    print(f'{name}: total={dc}  doors={doors_dc} ({100*doors_dc/dc:.0f}%)  no-doors={nodoor_dc}')
    print(f'   doors DC per slot: {doors_dc/M_SLOTS:.2f}   per instance: {doors_dc/M_INST:.2f}   per renderer: {doors_dc/4949:.2f}')

# ---------- SCENARIO MODEL ----------
# measured-state slot inventory (WorldRoot_0_0 after Э8, 30.10): 15 403 slots total, doors 12 189
WR_SLOTS = 15403
CITY_SLOTS_MERGED = 1806
NONDOOR_SLOTS = WR_SLOTS - M_SLOTS
print('\nnon-door slots in WorldRoot (measured state):', NONDOOR_SLOTS, '(city', CITY_SLOTS_MERGED, ')')

scen = []
def add(name, dc, tris_m, note):
    scen.append({'scenario': name, 'draw_calls': dc, 'tris_m': tris_m, 'note': note})

# A/B measured
add('A: as measured (merged chunk x material, doors on, shadow 1000/2)', '26981', '15.86-16.57', 'measured 04_OCCLUSION §3')
add('B: as measured (shadow 250/1, doors on)', '21118', '11.61', 'measured 04_OCCLUSION §3')
add('A: doors off', '4954', '15.68', 'measured')
add('B: doors off', '3352', '10.88', 'measured')

# scenario: HEAD unmerged city
head_city_slots = 20954  # HEAD city slots
extra = head_city_slots - CITY_SLOTS_MERGED
for cfg, base_no_doors, m_nondoor in (('shadow250/1', 3352, 1.0), ('shadow1000/2', 4954, 1.5)):
    dc = base_no_doors + extra * m_nondoor
    add(f'city unmerged = HEAD ({head_city_slots} slots), doors as is, {cfg}',
        str(round(dc)), round(5.84, 2),
        f'existing no-door frame {base_no_doors} + {extra} extra slots x {m_nondoor}')

# scenario: merged per house/cell: same slots as merged but better culling
for cfg, dc in (('shadow250/1', 21118), ('shadow1000/2', 26981)):
    add(f'merge per house/cell (same slots, finer AABB), {cfg}', str(dc), '11.6 / 15.9',
        'draw calls roughly unchanged; gain only from better culling (visible fraction -20..40%)')
    add(f'merge per house/cell + occlusion removes 50% hidden, {cfg}',
        str(round(dc * 0.55 / 1000) * 1000), '~6-8', 'estimate: at 55% of current DC')

# scenario: doors without shadow casting
add('A + doors do not cast shadows', str(26981 - (22027 - M_SLOTS)), '15.6', 'removes door shadow pass')
add('B + doors do not cast shadows', str(21118 - (17766 - M_SLOTS)), '11.4', 'removes door shadow pass')
# scenario: doors merged to 1 material (atlas) -> 1 slot/door
add('B + doors merged to 1 slot each (2 363 slots)', str(round(3352 + M_INST * 1.46)), '11.4',
    'atlas merge, keeps interaction')
add('B + doors 1 slot, no shadows', str(round(3352 + M_INST)), '11.4', 'best door-only win without GPU path')
# scenario: GPU resident drawer
add('GPU Resident Drawer path (city + doors instanced, shaders DOTS-ready)', '150-350', '~10-15',
    'estimate; requires DOTS_INSTANCING_ON in all city/door shaders; blocked today')

with open(os.path.join(DATA, 'C_drawcall_model.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=['scenario', 'draw_calls', 'tris_m', 'note'])
    w.writeheader()
    w.writerows(scen)
print('\n=== SCENARIOS ===')
for r in scen:
    print(f"  {r['draw_calls']:>10s}  {r['scenario']}")


