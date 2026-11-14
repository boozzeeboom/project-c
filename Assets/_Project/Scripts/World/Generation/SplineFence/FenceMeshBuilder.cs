using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Низкоуровневый сборщик меша для забора.
    ///
    /// Договорённости:
    /// * каждая грань — 4 своих вершины с нормалью грани (жёсткие кромки, НИКАКОГО сглаживания);
    /// * UV0 в метрах: U = вдоль ребра a→b, V = вдоль ребра a→d (то есть «1 юнит = 1 м»);
    /// * фронт квада (a,b,c,d) = cross(b-a, c-a) — та же конвенция, что у Mesh.RecalculateNormals;
    /// * сабмеши = материалы (0 — полотно, 1 — столбы).
    ///
    /// Габаритный базис AddBox: az = cross(ax, ay), ay доортонормируется — поэтому наклонные
    /// балки (рельеф) не дают сдвига/скошенных коробок.
    /// </summary>
    public sealed class FenceMeshBuilder
    {
        private readonly string _name;
        private readonly List<Vector3> _verts = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<int>[] _tris;

        public FenceMeshBuilder(string name, int submeshCount)
        {
            _name = name;
            _tris = new List<int>[Mathf.Max(1, submeshCount)];
            for (int i = 0; i < _tris.Length; i++) _tris[i] = new List<int>();
        }

        public int SubmeshCount => _tris.Length;
        public int QuadCount { get; private set; }
        public bool IsEmpty => QuadCount == 0;

        /// <summary>Квад по четырём точкам. Порядок обхода задаёт фронт: cross(b-a, c-a).</summary>
        public void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;

            submesh = Mathf.Clamp(submesh, 0, _tris.Length - 1);

            float u = Vector3.Distance(a, b);
            float v = Vector3.Distance(a, d);

            int i0 = _verts.Count;
            _verts.Add(a);
            _verts.Add(b);
            _verts.Add(c);
            _verts.Add(d);

            Vector3 n = normal.normalized;
            _normals.Add(n);
            _normals.Add(n);
            _normals.Add(n);
            _normals.Add(n);

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(u, 0f));
            _uvs.Add(new Vector2(u, v));
            _uvs.Add(new Vector2(0f, v));

            List<int> t = _tris[submesh];
            t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
            t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);

            QuadCount++;
        }

        /// <summary>Коробка с базисом (axisX, axisY). axisX нормализуется, az = cross(ax, ay), ay доортонормируется.</summary>
        public void AddBox(int submesh, Vector3 center, Vector3 axisX, Vector3 axisY, Vector3 size)
        {
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) return;

            Vector3 ax = axisX.sqrMagnitude > 1e-12f ? axisX.normalized : Vector3.right;

            Vector3 az = Vector3.Cross(ax, axisY);
            if (az.sqrMagnitude < 1e-10f) return;          // axisX параллелен axisY — вырожденный базис
            az.Normalize();

            Vector3 ay = Vector3.Cross(az, ax);
            if (ay.sqrMagnitude < 1e-10f) return;
            ay.Normalize();

            Vector3 cx = ax * (size.x * 0.5f);
            Vector3 cy = ay * (size.y * 0.5f);
            Vector3 cz = az * (size.z * 0.5f);

            // +X / -X
            AddQuad(submesh,
                center + cx - cy - cz, center + cx + cy - cz, center + cx + cy + cz, center + cx - cy + cz);
            AddQuad(submesh,
                center - cx - cy + cz, center - cx + cy + cz, center - cx + cy - cz, center - cx - cy - cz);

            // +Y / -Y
            AddQuad(submesh,
                center - cx - cy - cz, center - cx - cy + cz, center + cx - cy + cz, center + cx - cy - cz);
            AddQuad(submesh,
                center - cx + cy - cz, center + cx + cy - cz, center + cx + cy + cz, center - cx + cy + cz);

            // +Z / -Z
            AddQuad(submesh,
                center - cx - cy + cz, center + cx - cy + cz, center + cx + cy + cz, center - cx + cy + cz);
            AddQuad(submesh,
                center + cx - cy - cz, center - cx - cy - cz, center - cx + cy - cz, center + cx + cy - cz);
        }

        public Mesh Build()
        {
            var mesh = new Mesh { name = _name };
            mesh.indexFormat = _verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);

            mesh.subMeshCount = _tris.Length;
            for (int i = 0; i < _tris.Length; i++)
                mesh.SetTriangles(_tris[i], i, false);

            // только bounds: RecalculateNormals сгладил бы кромки балок
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
