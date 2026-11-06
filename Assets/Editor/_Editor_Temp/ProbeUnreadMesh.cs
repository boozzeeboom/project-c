using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace ProjectC.EditorTools
{
    public static class ProbeUnreadMesh
    {
        static string[] Names =
        {
            "ADM_ROOF_SLAB",
            "ADM_BOOTH_ROOF",
            "ADM_CORR_F1_WALL_BLK_E",
            "ADM_CORR_F1_WALL_N",
            "ADM_CORR_F1_WALL_MID_N",
            "ADM_BOOTH_WALL_E",
            "ADM_BOOTH_DOOR_FRAME",
            "ADM_BOOTH_WIN_N_FRAME",
        };

        static bool RayTri(Vector3 a, Vector3 b, Vector3 c, Vector3 o, Vector3 d, out float t)
        {
            t = 0f;
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (det > -1e-12f && det < 1e-12f) return false;
            float inv = 1f / det;
            Vector3 tv = o - a;
            float u = Vector3.Dot(tv, p) * inv;
            if (u < -1e-6f || u > 1f + 1e-6f) return false;
            Vector3 q = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < -1e-6f || u + v > 1f + 1e-6f) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t > 1e-7f;
        }

        public static void Execute()
        {
            var sb = new StringBuilder();

            Type tool = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("ProjectC.EditorTools.BuildSettlementColliders", false);
                if (t != null) { tool = t; break; }
            }
            if (tool == null) { Debug.Log("[Probe5] tool not found"); return; }
            MethodInfo plan = null;
            foreach (var m in tool.GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
                if (m.Name == "SlicePlan") { plan = m; break; }
            Type boxType = null;
            foreach (var t in tool.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
                if (t.Name == "SliceBox") { boxType = t; break; }
            var fmin = boxType.GetField("min", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fmax = boxType.GetField("max", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (string nm in Names)
            {
                var go = GameObject.Find("WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/" + nm);
                if (go == null) { sb.AppendLine($"[Probe5] {nm}: NOT FOUND"); continue; }
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { sb.AppendLine($"[Probe5] {nm}: no mesh"); continue; }
                Mesh mesh = mf.sharedMesh;

                Vector3[] verts; int[] tris;
                using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var d = data[0];
                    var vb = new NativeArray<Vector3>(d.vertexCount, Allocator.Temp);
                    d.GetVertices(vb); verts = vb.ToArray(); vb.Dispose();
                    int total = 0;
                    for (int s = 0; s < d.subMeshCount; s++) total += (int)d.GetSubMesh(s).indexCount;
                    tris = new int[total];
                    int off = 0;
                    for (int s = 0; s < d.subMeshCount; s++)
                    {
                        int cnt = (int)d.GetSubMesh(s).indexCount;
                        if (cnt <= 0) continue;
                        var ib = new NativeArray<int>(cnt, Allocator.Temp);
                        d.GetIndices(ib, s);
                        Array.Copy(ib.ToArray(), 0, tris, off, cnt);
                        ib.Dispose(); off += cnt;
                    }
                }

                Vector3 wpl = new Vector3(Mathf.Abs(go.transform.lossyScale.x),
                                          Mathf.Abs(go.transform.lossyScale.y),
                                          Mathf.Abs(go.transform.lossyScale.z));
                Vector3 lb = mesh.bounds.size;
                int axis = 0;
                if (lb.y > lb[axis]) axis = 1;
                if (lb.z > lb[axis]) axis = 2;

                object[] args = new object[] { mesh, axis, 1.0f / wpl[axis], 0.05f, wpl, false };
                var list = plan.Invoke(null, args) as IList;
                int boxCount = list == null ? 0 : list.Count;

                if (list == null)
                {
                    sb.AppendLine($"[Probe5] {nm}: verts={verts.Length} tris={tris.Length / 3} axis={axis} => SlicePlan=NULL (один габаритный бокс)");
                    continue;
                }

                var boxes = new List<Vector3[]>();
                for (int i = 0; i < list.Count; i++)
                    boxes.Add(new[] { (Vector3)fmin.GetValue(list[i]), (Vector3)fmax.GetValue(list[i]) });

                int ax = axis, c1 = (axis + 1) % 3, c2 = (axis + 2) % 3;
                float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                float zmin = float.MaxValue, zmax = float.MinValue;
                for (int v = 0; v < verts.Length; v++)
                {
                    x0 = Mathf.Min(x0, verts[v][ax]); x1 = Mathf.Max(x1, verts[v][ax]);
                    y0 = Mathf.Min(y0, verts[v][c1]); y1 = Mathf.Max(y1, verts[v][c1]);
                    zmin = Mathf.Min(zmin, verts[v][c2]); zmax = Mathf.Max(zmax, verts[v][c2]);
                }
                float zmid = (zmin + zmax) * 0.5f;

                var o3 = Vector3.zero; var dir3 = Vector3.zero; dir3[c2] = 1f;
                var cross = new List<Vector2>(32);
                int triN = tris.Length / 3;

                Func<float, float, bool> IsMat = (px, py) =>
                {
                    o3[ax] = px; o3[c1] = py; o3[c2] = zmin - 1f;
                    cross.Clear();
                    for (int t = 0; t < triN; t++)
                    {
                        Vector3 a = verts[tris[t * 3]], b = verts[tris[t * 3 + 1]], c = verts[tris[t * 3 + 2]];
                        float nd = Vector3.Dot(Vector3.Cross(b - a, c - a), dir3);
                        if (nd > -1e-9f && nd < 1e-9f) continue;
                        float dist;
                        if (!RayTri(a, b, c, o3, dir3, out dist)) continue;
                        cross.Add(new Vector2(zmin - 1f + dist, nd < 0f ? 1f : -1f));
                    }
                    if (cross.Count == 0) return false;
                    cross.Sort((p, q) => p.x.CompareTo(q.x));
                    float depth = 0f, spanStart = 0f;
                    for (int k = 0; k < cross.Count; k++)
                    {
                        int nxt = k + 1;
                        if (nxt < cross.Count && Mathf.Abs(cross[nxt].x - cross[k].x) <= 1e-5f)
                        {
                            float sum = cross[k].y;
                            while (nxt < cross.Count && Mathf.Abs(cross[nxt].x - cross[k].x) <= 1e-5f) { sum += cross[nxt].y; nxt++; }
                            if (depth == 0f && sum > 0f) spanStart = cross[k].x;
                            depth += sum;
                            if (depth == 0f && zmid >= spanStart && zmid <= cross[k].x) return true;
                            k = nxt - 1; continue;
                        }
                        if (depth == 0f) spanStart = cross[k].x;
                        depth += cross[k].y;
                        if (depth == 0f && zmid >= spanStart && zmid <= cross[k].x) return true;
                    }
                    return false;
                };

                Func<float, float, bool> InBox = (px, py) =>
                {
                    for (int b = 0; b < boxes.Count; b++)
                    {
                        var mn = boxes[b][0]; var mx = boxes[b][1];
                        if (px >= mn[ax] && px <= mx[ax] && py >= mn[c1] && py <= mx[c1]) return true;
                    }
                    return false;
                };

                if (triN > 4000) { sb.AppendLine($"[Probe5] {nm}: verts={verts.Length} tris={triN} axis={axis} => boxes={boxCount} (raster skipped)"); continue; }

                const int N = 90;
                float dx = (x1 - x0) / N, dy = (y1 - y0) / N;
                long mat = 0, both = 0, fn = 0, fp = 0;
                for (int i = 0; i < N; i++)
                {
                    float px = x0 + (i + 0.5f) * dx;
                    for (int j = 0; j < N; j++)
                    {
                        float py = y0 + (j + 0.5f) * dy;
                        bool m = IsMat(px, py), bx = InBox(px, py);
                        if (m) mat++;
                        if (m && bx) both++;
                        else if (m) fn++;
                        else if (bx) fp++;
                    }
                }
                float cell = dx * dy, area = mat * cell;
                float pct = area > 0f ? 100f * (both + fp) * cell / area : 0f;
                sb.AppendLine($"[Probe5] {nm}: verts={verts.Length} tris={triN} axis={axis} boxes={boxCount} | " +
                              $"материал={area * 10000f:F0} у.е.² покрыто={pct:F1}% FP={fp * cell * 10000f:F2} FN={fn * cell * 10000f:F2}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
