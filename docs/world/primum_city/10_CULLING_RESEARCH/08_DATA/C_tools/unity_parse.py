"""Read-only parser for Unity YAML scene/prefab files (Project C audit).

Usage as module:
    import unity_parse as up
    docs = up.load(path)          # Dict[int fileId] -> Doc
    gos  = up.gameobjects(docs)
    ...
No writes, no Unity.
"""
import re
import os
from collections import defaultdict

HDR = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$')


class Doc:
    __slots__ = ('cls', 'fid', 'stripped', 'body', 'line')

    def __init__(self, cls, fid, stripped, body, line):
        self.cls = cls
        self.fid = fid
        self.stripped = stripped
        self.body = body
        self.line = line

    def __repr__(self):
        return f'<Doc cls={self.cls} fid={self.fid} stripped={self.stripped}>'


def load(path):
    """Return dict fid -> Doc. Memory-heavy but fine for ~63 MB files."""
    docs = {}
    with open(path, 'r', encoding='utf-8', errors='replace') as f:
        cur = None
        buf = []
        for lineno, line in enumerate(f, 1):
            m = HDR.match(line)
            if m:
                if cur is not None:
                    cur.body = ''.join(buf)
                    docs[cur.fid] = cur
                cur = Doc(int(m.group(1)), int(m.group(2)), bool(m.group(3)), None, lineno)
                buf = []
            elif cur is not None:
                buf.append(line)
        if cur is not None:
            cur.body = ''.join(buf)
            docs[cur.fid] = cur
    return docs


# ---------- field helpers ----------

def get_scalar(body, key, default=None):
    """First `key: value` at any indentation, returns trimmed string value."""
    m = re.search(r'^[ \t]*' + re.escape(key) + r':[ \t]*(.*)$', body, re.M)
    if not m:
        return default
    return m.group(1).strip()


def get_int(body, key, default=None):
    v = get_scalar(body, key)
    if v is None:
        return default
    try:
        return int(v)
    except ValueError:
        return default


def get_ref(body, key):
    """Parse `key: {fileID: N, guid: G, type: T}` -> (fid, guid) ; fid may be 0."""
    m = re.search(r'^[ \t]*' + re.escape(key) + r': \{fileID: (-?\d+)(?:, guid: ([0-9a-fA-F]+), type: (\d+))?\}',
                  body, re.M)
    if not m:
        return None, None
    return int(m.group(1)), (m.group(2) or None)


def get_int_list(body, key):
    """Parse YAML block list of scalars after `key:` (e.g. m_Layers: - 0)."""
    m = re.search(r'^[ \t]*' + re.escape(key) + r':[ \t]*\n((?:[ \t]*-[ \t]*[^\n]*\n?)*)', body, re.M)
    if not m:
        return []
    out = []
    for ln in m.group(1).splitlines():
        ln = ln.strip()
        if ln.startswith('-'):
            out.append(ln[1:].strip())
    return out


def get_ref_list(body, key):
    """Parse block list of `{fileID: N}` after key -> list of ints."""
    items = get_int_list(body, key)
    out = []
    for it in items:
        m = re.match(r'\{fileID: (-?\d+)\}', it)
        if m:
            out.append(int(m.group(1)))
    return out


def get_vec(body, key):
    m = re.search(r'^[ \t]*' + re.escape(key) + r': \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}',
                  body, re.M)
    if not m:
        return None
    return tuple(float(m.group(i)) for i in (1, 2, 3))


def get_ref_guid_map(body, key):
    """For lists like m_Materials: - {fileID: 2100000, guid: ..., type: 2} return [(fid, guid, type)]."""
    m = re.search(r'^[ \t]*' + re.escape(key) + r':[ \t]*\n((?:[ \t]*-[ \t]*\{[^\n]*\n?)*)', body, re.M)
    if not m:
        return []
    out = []
    for ln in m.group(1).splitlines():
        mm = re.search(r'\{fileID: (-?\d+), guid: ([0-9a-fA-F]+), type: (\d+)\}', ln)
        if mm:
            out.append((int(mm.group(1)), mm.group(2), int(mm.group(3))))
        else:
            mm = re.search(r'\{fileID: (-?\d+)\}', ln)
            if mm:
                out.append((int(mm.group(1)), None, None))
    return out


# ---------- higher level ----------

class Scene:
    """Lightweight index over a parsed scene file."""

    def __init__(self, path):
        self.path = path
        self.docs = load(path)
        self.by_cls = defaultdict(list)
        for d in self.docs.values():
            self.by_cls[d.cls].append(d)
        # GameObjects
        self.go = {}          # fid -> dict(name, layer, active, staticFlags, components, prefabInstance, stripped)
        for d in self.by_cls.get(1, []):
            if d.stripped:
                continue
            body = d.body
            self.go[d.fid] = {
                'fid': d.fid,
                'name': get_scalar(body, 'm_Name', ''),
                'layer': get_int(body, 'm_Layer', 0),
                'active': get_int(body, 'm_IsActive', 1),
                'staticFlags': get_int(body, 'm_StaticEditorFlags', 0),
                'tag': get_scalar(body, 'm_TagString', ''),
                'prefabInstance': get_ref(body, 'm_PrefabInstance')[0],
                'components': get_ref_list(body, 'm_Component'),
            }
        # Transforms
        self.tr = {}
        for d in self.by_cls.get(4, []):
            body = d.body
            gofid = get_ref(body, 'm_GameObject')[0]
            father = get_ref(body, 'm_Father')[0]
            children = get_ref_list(body, 'm_Children')
            pos = get_vec(body, 'm_LocalPosition')
            self.tr[d.fid] = {
                'fid': d.fid,
                'go': gofid,
                'father': father,
                'children': children,
                'pos': pos if pos else (0.0, 0.0, 0.0),
                'prefabInstance': get_ref(body, 'm_PrefabInstance')[0],
                'stripped': d.stripped,
            }
        # prefab instances
        self.prefab_instances = {}
        for d in self.by_cls.get(1001, []):
            body = d.body
            parent = None
            m = re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', body)
            if m:
                parent = int(m.group(1))
            src = None
            m = re.search(r'm_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-fA-F]+), type: (\d+)\}', body)
            if m:
                src = (int(m.group(1)), m.group(2), int(m.group(3)))
            # name override + local position override from m_Modifications
            name = None
            m = re.search(r'propertyPath: m_Name\n[ \t]*value: (.+)\n', body)
            if m:
                name = m.group(1).strip()
            pos = None
            m = re.search(r'propertyPath: m_LocalPosition\.x\n[ \t]*value: ([-\d.eE]+)\n', body)
            if m:
                pos = (float(m.group(1)),)
            removals = len(re.findall(r'propertyPath: ', body))
            self.prefab_instances[d.fid] = {
                'fid': d.fid,
                'parent': parent,
                'source': src,
                'name': name,
                'mods': removals,
                'removedComponents': re.search(r'm_RemovedComponents: \[\]', body) is not None,
            }
        # renderers (scene-local, non-stripped)
        self.renderers = {}
        for cls in (23, 137, 199, 120, 212):  # MeshRenderer, SkinnedMeshRenderer, ParticleSystemRenderer, LineRenderer, SpriteRenderer
            for d in self.by_cls.get(cls, []):
                if d.stripped:
                    continue
                body = d.body
                gofid = get_ref(body, 'm_GameObject')[0]
                mats = get_ref_guid_map(body, 'm_Materials')
                self.renderers[d.fid] = {
                    'fid': d.fid,
                    'cls': cls,
                    'go': gofid,
                    'enabled': get_int(body, 'm_Enabled', 1),
                    'castShadows': get_int(body, 'm_CastShadows', 1),
                    'receiveShadows': get_int(body, 'm_ReceiveShadows', 1),
                    'materials': mats,
                    'lightProbeUsage': get_int(body, 'm_LightProbeUsage', 1),
                    'reflectionProbeUsage': get_int(body, 'm_ReflectionProbeUsage', 1),
                    'motionVectors': get_int(body, 'm_MotionVectors', 1),
                    'rendererPriority': get_int(body, 'm_RendererPriority', 0),
                }
        # mesh filters
        self.mesh_filters = {}
        for d in self.by_cls.get(33, []):
            if d.stripped:
                continue
            body = d.body
            gofid = get_ref(body, 'm_GameObject')[0]
            meshfid, meshguid = get_ref(body, 'm_Mesh')
            self.mesh_filters[d.fid] = {'fid': d.fid, 'go': gofid, 'mesh': meshfid,
                                        'guid': meshguid}
        # colliders
        self.colliders = defaultdict(int)
        for cls in (64, 65, 135, 136, 143, 154, 153):
            for d in self.by_cls.get(cls, []):
                if d.stripped:
                    continue
                gofid = get_ref(d.body, 'm_GameObject')[0]
                self.colliders[(gofid, cls)] += 1
        # scene roots
        self.roots = []
        for d in self.by_cls.get(1660057539, []):
            self.roots = get_ref_list(d.body, 'm_Roots')

    # --- hierarchy ---

    def go_name(self, gofid):
        g = self.go.get(gofid)
        if g:
            return g['name']
        tr = self.tr.get(gofid)  # stripped transform: gofid actually points at transform
        return None

    def transform_of_go(self, gofid):
        """Find non-stripped transform whose m_GameObject == gofid."""
        key = ('tr_by_go', gofid)
        cache = getattr(self, '_tr_by_go', None)
        if cache is None:
            cache = {}
            for tfid, t in self.tr.items():
                if t['go'] and not t['stripped']:
                    cache[t['go']] = tfid
            self._tr_by_go = cache
        return cache.get(gofid)

    def path_of_transform(self, tfid, max_depth=64):
        """Return list of names from root to this transform (stripped transforms resolved by name of GO)."""
        names = []
        cur = tfid
        guard = 0
        while cur and guard < max_depth:
            guard += 1
            t = self.tr.get(cur)
            if not t:
                break
            g = self.go.get(t['go'])
            if g:
                names.append(g['name'])
            else:
                # stripped: name from prefab instance / corresponding source
                names.append('<stripped>')
            cur = t['father']
        names.reverse()
        return names

    def root_name_of_transform(self, tfid, max_depth=64):
        names = self.path_of_transform(tfid, max_depth)
        return names[0] if names else '<none>'


# ---------- prefab stats ----------

def prefab_stats(path):
    """Count renderers/submeshes/colliders inside a prefab asset file."""
    res = {
        'path': path,
        'gameobjects': 0, 'mesh_renderers': 0, 'skinned': 0, 'particle': 0,
        'line': 0, 'sprite': 0, 'meshfilters': 0, 'colliders': 0,
        'material_slots': 0, 'meshes': set(), 'materials': set(),
        'lodgroups': 0, 'materials_list': [],
        'casts_shadows_off': 0, 'static_flags_nonzero': 0,
        'max_submeshes': 0, 'submesh_hist': defaultdict(int),
        'monobehaviours': 0,
    }
    docs = load(path)
    for d in docs.values():
        if d.stripped:
            continue
        b = d.body
        if d.cls == 1:
            res['gameobjects'] += 1
            sf = get_int(b, 'm_StaticEditorFlags', 0)
            if sf:
                res['static_flags_nonzero'] += 1
        elif d.cls == 23:
            res['mesh_renderers'] += 1
            mats = get_ref_guid_map(b, 'm_Materials')
            res['material_slots'] += len(mats)
            res['materials'].update(g for _, g, _ in mats if g)
            res['materials_list'].extend(g for _, g, _ in mats if g)
            if get_int(b, 'm_CastShadows', 1) == 0:
                res['casts_shadows_off'] += 1
        elif d.cls == 137:
            res['skinned'] += 1
        elif d.cls == 199:
            res['particle'] += 1
        elif d.cls == 120:
            res['line'] += 1
        elif d.cls == 212:
            res['sprite'] += 1
        elif d.cls == 33:
            res['meshfilters'] += 1
            meshfid, guid = get_ref(b, 'm_Mesh')
            if guid:
                res['meshes'].add((guid, meshfid))
        elif d.cls in (64, 65, 135, 136, 143):
            res['colliders'] += 1
        elif d.cls == 114:
            res['monobehaviours'] += 1
        elif d.cls == 20:
            res['cameras'] = res.get('cameras', 0) + 1
        elif d.cls == 108:
            res['lights'] = res.get('lights', 0) + 1
        elif d.cls == 218:
            res['terrains'] = res.get('terrains', 0) + 1
        elif d.cls == 215:
            res['reflection_probes'] = res.get('reflection_probes', 0) + 1
    return res


# ---------- guid -> path index ----------

def build_guid_index(assets_root, exts=None):
    """Return dict guid -> relative path by scanning .meta files (fast, no Unity)."""
    idx = {}
    assets_root = os.path.abspath(assets_root)
    for dirpath, dirnames, filenames in os.walk(assets_root):
        for fn in filenames:
            if not fn.endswith('.meta'):
                continue
            if exts and not any(fn[:-5].endswith(e) for e in exts):
                continue
            meta = os.path.join(dirpath, fn)
            try:
                with open(meta, 'r', encoding='utf-8', errors='replace') as f:
                    head = f.read(200)
            except OSError:
                continue
            m = re.search(r'guid: ([0-9a-f]{32})', head)
            if m:
                idx[m.group(1)] = os.path.join(dirpath, fn[:-5])
    return idx
