# colliders_fix.py — пересборка ТОЛЬКО WALK (9 боксов) и STAIR (исправленный наклон по bbox)
# Пишет colliders_fix.csv; основной CSV дополняется отдельно (см. merge в PowerShell).
import bpy, os, csv, math, time
from collections import defaultdict
from mathutils import Vector

ROOT = r"Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder"
RAW = os.path.join(ROOT, "PRIMUM_chunks_RAW")
OUT = r"C:\Projects\ProjectC_client\_analysis\colliders_fix.csv"
LOG = r"C:\Projects\ProjectC_client\_analysis\colliders_fix.log"

K = 1.07
C = (7.5, -148.0, 18.5)
MIN_SIDE = 0.50
STAIR_THICK = 0.35

WALK_NAMES = {
    "DECK_0", "DECK_6", "DECK_12", "DECK_18",
    "PLATFORM_L00", "PLATFORM_L06", "PLATFORM_L12", "PLATFORM_L18", "PLATFORM_L24",
}

# чанки, где в первом прогоне нашлись лестницы
CHUNKS = [
    ("PRIMUM_BASE_S0_CITY_P1_00H.blend", "PRIMUM_BASE_S0_CITY_P1_00H"),
    ("PRIMUM_ADMIN_S0_ADM_P1_01H.blend", "PRIMUM_ADMIN_S0_ADM_P1_01H"),
    ("PRIMUM_HOUSES_S2_TRD_P1_28H.blend", "PRIMUM_HOUSES_S2_TRD_P1_28H"),
    ("PRIMUM_HOUSES_S3_TRD_P2_26H.blend", "PRIMUM_HOUSES_S3_TRD_P2_26H"),
    ("PRIMUM_HOUSES_S3_TRD-ES_P1_37H.blend", "PRIMUM_HOUSES_S3_TRD-ES_P1_37H"),
    ("PRIMUM_HOUSES_S4_TRD_P3_26H.blend", "PRIMUM_HOUSES_S4_TRD_P3_26H"),
    ("PRIMUM_HOUSES_S9_TRD-N_P1_28H.blend", "PRIMUM_HOUSES_S9_TRD-N_P1_28H"),
    ("PRIMUM_HOUSES_S9_TRD-S_P1_16H.blend", "PRIMUM_HOUSES_S9_TRD-S_P1_16H"),
]

def log(m):
    with open(LOG, "a", encoding="utf-8") as f: f.write(m + "\n")
    print(m)

def ulocal(p):
    s = [C[i] + (p[i]-C[i])*K for i in range(3)]
    return (-s[0], s[2], -s[1])

def world_bbox(o):
    m = o.matrix_world
    pts = [m @ Vector(c) for c in o.bound_box]
    xs=[p.x for p in pts]; ys=[p.y for p in pts]; zs=[p.z for p in pts]
    return (min(xs),max(xs),min(ys),max(ys),min(zs),max(zs))

def cpath(o): return "/".join([c.name for c in o.users_collection]).upper()

def has_ancestor(o, names):
    p = o.parent
    while p is not None:
        if p.name in names: return True
        p = p.parent
    return False

RAIL_TOKENS = ("RAIL","GUARD","HAND","SKIRT","BALUST","_BR","KERB","KICK")

def is_stair_part(o):
    p = cpath(o); n = o.name.upper()
    if "STAIR" not in p and "STAIR" not in n and "STEPS" not in p: return False
    return not any(t in n for t in RAIL_TOKENS)

rows = []
stat = defaultdict(int)

def fmt(v): return ("%.3f" % v).replace(",", ".")

def add(chunk, cls, c, s, yaw=0.0, pitch=0.0, name=""):
    x,y,z = c; sx,sy,sz = s
    if max(sx,sy,sz) < MIN_SIDE: return
    rows.append([chunk, cls, fmt(x), fmt(y), fmt(z), fmt(sx), fmt(sy), fmt(sz),
                 "%.2f" % yaw, "%.2f" % pitch, name])
    stat[cls] += 1

# ---------------------------------------------------------------- лестница по bbox

def add_stair(chunk, objs, key):
    pts = []
    for o in objs:
        for v in o.data.vertices:
            pts.append(o.matrix_world @ v.co)
    if len(pts) < 8: return
    xs=[p.x for p in pts]; ys=[p.y for p in pts]; zs=[p.z for p in pts]
    dx=max(xs)-min(xs); dy=max(ys)-min(ys); dz=max(zs)-min(zs)
    if dx < 0.6 or dy < 0.6: return

    def grad(axis):
        lo = min(xs) if axis==0 else min(ys)
        hi = max(xs) if axis==0 else max(ys)
        if hi-lo < 0.3: return 0.0
        nb=12; acc=[0.0]*nb; cnt=[0]*nb
        for p in pts:
            v = p.x if axis==0 else p.y
            b=int(min(nb-1,(v-lo)/(hi-lo)*nb)); acc[b]+=p.z; cnt[b]+=1
        m=[acc[i]/cnt[i] for i in range(nb) if cnt[i]>0]
        if len(m)<3: return 0.0
        return (m[-1]-m[0])/(hi-lo)

    gx, gy = grad(0), grad(1)
    if abs(gx) >= abs(gy):
        axis=0; run=dx; width=dy; g=gx
    else:
        axis=1; run=dy; width=dx; g=gy
    if run < 1.0 or width < 0.5:
        # мелкий элемент — грубый осевой бокс
        cx=(min(xs)+max(xs))/2; cy=(min(ys)+max(ys))/2; cz=(min(zs)+max(zs))/2
        ux,uy,uz = ulocal((cx,cy,cz))
        add(chunk, "STAIR", (ux,uy,uz), (dx*K, dz*K, dy*K), 0, 0, key+"|flat")
        return
    # наклон — по ГАБАРИТУ (bbox), а не по средним (они занижают из-за площадок/косоуров)
    pitch = math.degrees(math.atan2(dz, run))
    if pitch < 8.0 or pitch > 60.0:
        cx=(min(xs)+max(xs))/2; cy=(min(ys)+max(ys))/2; cz=(min(zs)+max(zs))/2
        ux,uy,uz = ulocal((cx,cy,cz))
        add(chunk, "STAIR", (ux,uy,uz), (dx*K, dz*K, dy*K), 0, 0, key+"|flat")
        return
    dir_b = ((1.0 if g>0 else -1.0), 0.0) if axis==0 else (0.0, (1.0 if g>0 else -1.0))
    dir_u = (-dir_b[0], -dir_b[1])
    yaw = math.degrees(math.atan2(dir_u[0], dir_u[1]))
    cx=(min(xs)+max(xs))/2; cy=(min(ys)+max(ys))/2; cz=(min(zs)+max(zs))/2
    ux,uy,uz = ulocal((cx,cy,cz))
    length = math.sqrt(run*run + dz*dz) * K
    add(chunk, "STAIR", (ux,uy,uz), (width*K, STAIR_THICK, length), yaw, pitch, key)

# ---------------------------------------------------------------- прогон

def process(fname, chunk):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(RAW, fname))
    if "BASE" in chunk:
        for o in bpy.data.objects:
            if o.type == 'MESH' and o.name in WALK_NAMES and not has_ancestor(o, ("DOORS","LAMPS")):
                b = world_bbox(o)
                cx=(b[0]+b[1])/2; cy=(b[2]+b[3])/2; cz=(b[4]+b[5])/2
                ux,uy,uz = ulocal((cx,cy,cz))
                add(chunk, "WALK", (ux,uy,uz), ((b[1]-b[0])*K, (b[5]-b[4])*K, (b[3]-b[2])*K), 0, 0, o.name)
    groups = defaultdict(list)
    for o in bpy.data.objects:
        if o.type != 'MESH' or o.library is not None: continue
        if has_ancestor(o, ("DOORS","LAMPS","OUTLIERS","DOOR_PARTS_UNASSIGNED")): continue
        if not is_stair_part(o): continue
        key = o.name
        for seg in cpath(o).split("/"):
            if "STAIR" in seg: key = seg; break
        groups[key].append(o)
    for key, objs in groups.items():
        add_stair(chunk, objs, key)

def main():
    for f in (OUT, LOG):
        if os.path.exists(f): os.remove(f)
    t0=time.time()
    for fname, chunk in CHUNKS:
        if not os.path.exists(os.path.join(RAW, fname)):
            log("MISSING " + fname); continue
        before = dict(stat)
        try:
            process(fname, chunk)
        except Exception as e:
            log("ERROR %s: %s" % (fname, e))
        delta = {k: stat[k]-before.get(k,0) for k in stat if stat[k]-before.get(k,0)}
        log("  %-42s %s" % (chunk.replace("PRIMUM_",""), delta))
    with open(OUT, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["chunk","cls","cx","cy","cz","sx","sy","sz","yaw","pitch","name"])
        w.writerows(rows)
    log("=== ИТОГО %s (%.1f c) ===" % (dict(stat), time.time()-t0))

main()
print("DONE")
