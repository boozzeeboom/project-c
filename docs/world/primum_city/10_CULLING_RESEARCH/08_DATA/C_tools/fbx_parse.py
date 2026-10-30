"""Minimal FBX binary reader (read-only) for the Project C audit.

Parses the node tree and extracts, per file:
  - list of Model nodes (id, name, type) - mesh objects in the mesh hierarchy
  - list of Geometry nodes with polygon/triangle/material counts
  - OO connections Model -> Geometry

Supports FBX binary 7.x (versions < 7500 use 32-bit offsets; >= 7500 use 64-bit).
Not a full FBX SDK: only what we need for counting.
"""
import struct
import zlib
import sys

MAGIC = b'Kaydara FBX Binary  \x00'


class Node:
    __slots__ = ('name', 'props', 'children')

    def __init__(self, name, props):
        self.name = name
        self.props = props
        self.children = []


def _read_prop(buf, pos):
    t = buf[pos:pos + 1]
    pos += 1
    tc = t.decode('latin-1')
    if tc == 'Y':
        v = struct.unpack_from('<h', buf, pos)[0]; pos += 2
    elif tc == 'C':
        v = buf[pos]; pos += 1
    elif tc == 'I':
        v = struct.unpack_from('<i', buf, pos)[0]; pos += 4
    elif tc == 'F':
        v = struct.unpack_from('<f', buf, pos)[0]; pos += 4
    elif tc == 'D':
        v = struct.unpack_from('<d', buf, pos)[0]; pos += 8
    elif tc == 'L':
        v = struct.unpack_from('<q', buf, pos)[0]; pos += 8
    elif tc in 'fdlib':
        n, enc, clen = struct.unpack_from('<III', buf, pos)
        pos += 12
        raw = buf[pos:pos + clen]
        pos += clen
        if enc == 1:
            data = zlib.decompress(raw)
        else:
            data = raw
        fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'b'}[tc]
        size = {'f': 4, 'd': 8, 'l': 8, 'i': 4, 'b': 1}[tc]
        if tc == 'b':
            v = list(data[:n])
        else:
            v = memoryview(data)
        # keep (count, itemsize, raw) so caller can unpack on demand
        v = ('array', tc, n, data)
    elif tc in 'SR':
        ln = struct.unpack_from('<I', buf, pos)[0]; pos += 4
        v = buf[pos:pos + ln]
        pos += ln
        if tc == 'S':
            v = bytes(v).decode('utf-8', 'replace')
    else:
        raise ValueError(f'unknown FBX property type {tc!r} at {pos}')
    return v, pos


def parse(path, want_geometry_data=True):
    with open(path, 'rb') as f:
        buf = f.read()
    if not buf.startswith(MAGIC):
        raise ValueError('not a binary FBX: ' + path)
    version = struct.unpack_from('<I', buf, 23)[0]
    big = version >= 7500
    if big:
        hdr = struct.Struct('<QQQB')
    else:
        hdr = struct.Struct('<IIIB')

    def read_nodes(pos, end):
        out = []
        while pos < end:
            if big:
                endoff, nprops, plen, namelen = hdr.unpack_from(buf, pos)
            else:
                endoff, nprops, plen, namelen = hdr.unpack_from(buf, pos)
            pos += hdr.size
            if endoff == 0:
                break
            name = buf[pos:pos + namelen].decode('latin-1')
            pos += namelen
            props = []
            for _ in range(nprops):
                v, pos = _read_prop(buf, pos)
                props.append(v)
            node = Node(name, props)
            node.children = read_nodes(pos, endoff)
            out.append(node)
            pos = endoff
        return out

    # after 27-byte header: root nodes until a null record
    if big:
        root_start = 27
    else:
        root_start = 27
    nodes = read_nodes(root_start, len(buf))
    return {'version': version, 'nodes': nodes, 'buf': buf}


def iter_sections(tree, sname):
    for n in tree['nodes']:
        if n.name == sname:
            yield n


def summarize(path):
    tree = parse(path)
    res = {
        'version': tree['version'],
        'models': [],       # (id, name, type)
        'geometries': {},   # id -> dict(name, polys, tris, submeshes, matindices)
        'conns': [],        # (child, parent)
        'materials': [],    # (id, name)
        'deformers': 0,
    }
    for sec in iter_sections(tree, 'Objects'):
        for n in sec.children:
            if n.name == 'Model':
                mid = n.props[0]
                nm = n.props[1] if len(n.props) > 1 else ''
                typ = n.props[2] if len(n.props) > 2 else ''
                res['models'].append((mid, nm, typ))
            elif n.name == 'Geometry':
                gid = n.props[0]
                gname = n.props[1] if len(n.props) > 1 else ''
                d = {'name': gname, 'polys': 0, 'tris': 0, 'idx': 0, 'submeshes': 1,
                     'mat_indices': [], 'vertices': 0}
                for c in n.children:
                    if c.name == 'PolygonVertexIndex':
                        arr = c.props[0]
                        if arr[0] == 'array':
                            data = arr[3]
                            n_i = arr[2]
                            vals = struct.unpack_from(f'<{n_i}i', data, 0)
                            neg = sum(1 for v in vals if v < 0)
                            d['idx'] = n_i
                            d['polys'] = neg
                            d['tris'] = n_i - neg
                    elif c.name == 'Vertices':
                        arr = c.props[0]
                        if arr[0] == 'array':
                            d['vertices'] = arr[2] // 3
                    elif c.name == 'LayerElementMaterial':
                        mats = []
                        mapping = ''
                        for cc in c.children:
                            if cc.name == 'MappingInformationType':
                                mapping = cc.props[0]
                            elif cc.name == 'Materials':
                                a = cc.props[0]
                                if a[0] == 'array':
                                    mats = list(struct.unpack_from(f'<{a[2]}i', a[3], 0))
                        d['mapping'] = mapping
                        if mapping == 'ByPolygon' and mats:
                            d['submeshes'] = len(set(m for m in mats if m >= 0)) or 1
                            d['mat_indices'] = sorted(set(m for m in mats if m >= 0))
                        else:
                            d['submeshes'] = 1
                            d['mat_indices'] = sorted(set(mats)) if mats else [0]
                res['geometries'][gid] = d
            elif n.name == 'Material':
                res['materials'].append((n.props[0], n.props[1] if len(n.props) > 1 else ''))
            elif n.name == 'Deformer':
                res['deformers'] += 1
    for sec in iter_sections(tree, 'Connections'):
        for n in sec.children:
            if n.name == 'C' and len(n.props) >= 3 and n.props[0] == 'OO':
                res['conns'].append((n.props[1], n.props[2]))
    return res


if __name__ == '__main__':
    import json
    for p in sys.argv[1:]:
        s = summarize(p)
        print('==', p, 'v', s['version'], 'models', len(s['models']), 'geoms', len(s['geometries']),
              'mats', len(s['materials']), 'conns', len(s['conns']))
        tg = sum(g['tris'] for g in s['geometries'].values())
        print('   total tris', tg, 'polys', sum(g['polys'] for g in s['geometries'].values()))
        for m in s['models'][:10]:
            print('   model', m)
