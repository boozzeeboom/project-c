using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Trims the PRIMUM base paving (PAVING_BRUSCHATKA) so that it no longer runs into buildings.
/// The original surface (its heights and its stair steps) is preserved exactly: every output
/// triangle lies on the plane of the source triangle it came from. Only the parts of the
/// pavement that fall inside building volume are cut away.
///
/// Blocker test: a 0.2 m XZ raster holding, per cell, the lowest and the highest Y of all
/// building geometry above the terrain plateau. A pavement point at height h is blocked when
/// building geometry occupies [h - 0.08, h + 2.2] in that cell - i.e. there is something solid
/// at walking height (a wall) or right under the walking surface (an interior floor).
/// Geometry that only exists well above the pavement (balconies, awnings at 3 m, roofs) does not
/// block, so the pavement may still pass beneath them.
/// </summary>
public static class PavingFix
{
    const string PavingPath =
        "WorldRoot_0_0/PRIMUM/PRIMUM_BASE_S0_CITY_P1_00H/PRIMUM_BASE_S0_CITY_P1_00H_M_M_PAVING_BRUSCHATKA";
    const string BaseRoot = "WorldRoot_0_0/PRIMUM/PRIMUM_BASE_S0_CITY_P1_00H/";
    const string OutFolder = "Assets/_Project/Art/Models/Primum/Generated";
    const string OutMeshName = "PAVING_BRUSCHATKA_FIXED";

    // raster
    const float CELL = 0.2f;
    const float RX0 = 39000f, RZ0 = 39300f, RX1 = 40160f, RZ1 = 40360f;
    const float QSTEP = 0.05f;      // vertical quantization
    const float QORG = 2470f;       // vertical quantization origin

    // clip
    const float LEAF = 0.40f;       // stop subdividing below this world edge length
    const float BAND_UP = 2.20f;    // solid at walking height blocks
    const float BAND_DOWN = 0.08f;  // solid just under the surface (interior floors) blocks
    const int MAX_DEPTH = 9;
    const float MIN_COMPONENT_AREA = 0.5f;

    static ushort[] mn, mx;
    static int NX, NZ;

    public static void Execute()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var log = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        BuildBlocker(log);
        log.AppendLine("raster " + sw.ElapsedMilliseconds + " ms");

        var go = GameObject.Find(PavingPath);
        if (go == null) { Debug.LogError("[PavingFix] paving object not found"); return; }
        var mf = go.GetComponent<MeshFilter>();
        var src = mf.sharedMesh;
        var T = go.transform;

        var sv = src.vertices;
        var sn = src.normals;
        var st = src.triangles;
        bool hasNormals = sn != null && sn.Length == sv.Length;

        var outL = new List<Vector3>(st.Length);
        var outN = new List<Vector3>(st.Length);
        var outI = new List<int>(st.Length);
        int kept = 0, dropped = 0;

        for (int i = 0; i < st.Length; i += 3)
        {
            var s0 = Make(sv[st[i]], hasNormals ? sn[st[i]] : Vector3.up, T);
            var s1 = Make(sv[st[i + 1]], hasNormals ? sn[st[i + 1]] : Vector3.up, T);
            var s2 = Make(sv[st[i + 2]], hasNormals ? sn[st[i + 2]] : Vector3.up, T);
            if (s0.b && s1.b && s2.b) { dropped++; continue; }
            int before = outI.Count;
            Recurse(s0, s1, s2, 0, outL, outN, outI);
            if (outI.Count == before) dropped++; else kept++;
        }
        log.AppendLine("source tris " + (st.Length / 3) + " : fully kept/dropped -> cut " + kept + " , removed " + dropped);
        log.AppendLine("raw out tris " + (outI.Count / 3));

        // drop tiny isolated fragments
        int removedFrag = DropFragments(outL, outN, outI);
        log.AppendLine("fragment tris removed " + removedFrag + " ; out tris " + (outI.Count / 3));

        // build mesh (local space -> transform and material stay untouched)
        var mesh = new Mesh();
        mesh.name = OutMeshName;
        mesh.indexFormat = outI.Count / 3 > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(outL);
        if (hasNormals) mesh.SetNormals(outN);
        mesh.SetTriangles(outI, 0);
        mesh.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder(OutFolder))
        {
            System.IO.Directory.CreateDirectory(OutFolder);
            AssetDatabase.Refresh();
        }
        string assetPath = OutFolder + "/" + OutMeshName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, assetPath);
        }
        else
        {
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(outL);
            if (hasNormals) existing.SetNormals(outN);
            existing.SetTriangles(outI, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
        }
        AssetDatabase.SaveAssets();

        var final = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        Undo.RecordObject(mf, "Trim paving");
        mf.sharedMesh = final;
        EditorUtility.SetDirty(mf);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

        // verification against the same raster
        double blockedArea = 0, totalArea = 0;
        var fv = final.vertices; var ft = final.triangles;
        for (int i = 0; i < ft.Length; i += 3)
        {
            Vector3 a = T.TransformPoint(fv[ft[i]]), b = T.TransformPoint(fv[ft[i + 1]]), c = T.TransformPoint(fv[ft[i + 2]]);
            var nn = Vector3.Cross(b - a, c - a);
            double ar = nn.magnitude * 0.5; if (ar < 1e-9) continue;
            nn /= (float)(ar * 2);
            if (nn.y < 0.9f) continue;
            totalArea += ar;
            int cnt = 0, bl = 0;
            foreach (var p in new[] { (a + b + c) / 3f, (a + b) / 2f, (b + c) / 2f, (c + a) / 2f })
            {
                cnt++;
                if (Blocked(p.x, p.y, p.z)) bl++;
            }
            blockedArea += ar * bl / (double)cnt;
        }
        log.AppendLine(string.Format("VERIFY new up-facing area {0:F0} m2, still inside buildings {1:F0} m2", totalArea, blockedArea));
        log.AppendLine("verts " + final.vertexCount + " tris " + (ft.Length / 3) + " asset " + assetPath);
        log.AppendLine("total " + sw.ElapsedMilliseconds + " ms");
        Debug.Log("[PavingFix]\n" + log);
        mn = null; mx = null;
    }

    struct S { public Vector3 l, w, n; public bool b; }

    static S Make(Vector3 local, Vector3 nrm, Transform T)
    {
        var s = new S();
        s.l = local; s.w = T.TransformPoint(local); s.n = nrm;
        s.b = Blocked(s.w.x, s.w.y, s.w.z);
        return s;
    }

    static S Mid(S a, S b)
    {
        var s = new S();
        s.l = (a.l + b.l) * 0.5f;
        s.w = (a.w + b.w) * 0.5f;
        s.n = (a.n + b.n).normalized;
        s.b = Blocked(s.w.x, s.w.y, s.w.z);
        return s;
    }

    static void Emit(S a, S b, S c, List<Vector3> L, List<Vector3> N, List<int> I)
    {
        var cr = Vector3.Cross(b.w - a.w, c.w - a.w);
        if (cr.sqrMagnitude < 1e-10f) return;
        int i0 = L.Count;
        L.Add(a.l); L.Add(b.l); L.Add(c.l);
        N.Add(a.n); N.Add(b.n); N.Add(c.n);
        I.Add(i0); I.Add(i0 + 1); I.Add(i0 + 2);
    }

    static void Recurse(S a, S b, S c, int depth, List<Vector3> L, List<Vector3> N, List<int> I)
    {
        if (a.b && b.b && c.b) return;
        float e = Mathf.Max((a.w - b.w).sqrMagnitude, Mathf.Max((b.w - c.w).sqrMagnitude, (c.w - a.w).sqrMagnitude));
        if (e < LEAF * LEAF)
        {
            var ctr = new Vector3((a.w.x + b.w.x + c.w.x) / 3f, (a.w.y + b.w.y + c.w.y) / 3f, (a.w.z + b.w.z + c.w.z) / 3f);
            if (!Blocked(ctr.x, ctr.y, ctr.z)) Emit(a, b, c, L, N, I);
            return;
        }
        if (depth >= MAX_DEPTH)
        {
            var ctr = new Vector3((a.w.x + b.w.x + c.w.x) / 3f, (a.w.y + b.w.y + c.w.y) / 3f, (a.w.z + b.w.z + c.w.z) / 3f);
            if (!Blocked(ctr.x, ctr.y, ctr.z)) Emit(a, b, c, L, N, I);
            return;
        }
        var ab = Mid(a, b); var bc = Mid(b, c); var ca = Mid(c, a);
        Recurse(a, ab, ca, depth + 1, L, N, I);
        Recurse(ab, b, bc, depth + 1, L, N, I);
        Recurse(ca, bc, c, depth + 1, L, N, I);
        Recurse(ab, bc, ca, depth + 1, L, N, I);
    }

    static bool Blocked(float x, float y, float z)
    {
        int ix = (int)((x - RX0) / CELL);
        int iz = (int)((z - RZ0) / CELL);
        if (ix < 0 || iz < 0 || ix >= NX || iz >= NZ) return false;
        int idx = ix + iz * NX;
        if (mn[idx] == 65535) return false;
        float lo = QORG + mn[idx] * QSTEP;
        float hi = QORG + mx[idx] * QSTEP;
        return lo <= y + BAND_UP && hi >= y - BAND_DOWN;
    }

    static int DropFragments(List<Vector3> L, List<Vector3> N, List<int> I)
    {
        int triCount = I.Count / 3;
        if (triCount == 0) return 0;
        // weld on 1 cm world-space keys of the local position (local scale is uniform)
        var keyOf = new Dictionary<long, int>();
        var vid = new int[L.Count];
        for (int i = 0; i < L.Count; i++)
        {
            long kx = (long)Mathf.Round(L[i].x * 1000f);
            long ky = (long)Mathf.Round(L[i].y * 1000f);
            long kz = (long)Mathf.Round(L[i].z * 1000f);
            long h = (kx * 73856093L) ^ (ky * 19349663L) ^ (kz * 83492791L);
            int id;
            if (!keyOf.TryGetValue(h, out id)) { id = keyOf.Count; keyOf[h] = id; }
            vid[i] = id;
        }
        int nW = keyOf.Count;
        var parent = new int[nW];
        for (int i = 0; i < nW; i++) parent[i] = i;
        Func<int, int> find = null;
        find = a => { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; };
        for (int t = 0; t < triCount; t++)
        {
            int a = vid[I[t * 3]], b = vid[I[t * 3 + 1]], c = vid[I[t * 3 + 2]];
            int ra = find(a), rb = find(b), rc = find(c);
            if (ra != rb) parent[ra] = rb;
            if (find(rb) != rc) parent[find(rb)] = rc;
        }
        var area = new Dictionary<int, double>();
        for (int t = 0; t < triCount; t++)
        {
            Vector3 a = L[I[t * 3]], b = L[I[t * 3 + 1]], c = L[I[t * 3 + 2]];
            double ar = Vector3.Cross(b - a, c - a).magnitude * 0.5;
            int r = find(vid[I[t * 3]]);
            if (!area.ContainsKey(r)) area[r] = 0;
            area[r] += ar;
        }
        var keepTri = new bool[triCount];
        int removed = 0;
        for (int t = 0; t < triCount; t++)
        {
            int r = find(vid[I[t * 3]]);
            bool keep = area[r] >= MIN_COMPONENT_AREA;
            keepTri[t] = keep;
            if (!keep) removed++;
        }
        if (removed == 0) return 0;
        var nL = new List<Vector3>(L.Count); var nN = new List<Vector3>(N.Count); var nI = new List<int>(I.Count);
        for (int t = 0; t < triCount; t++)
        {
            if (!keepTri[t]) continue;
            int i0 = nL.Count;
            nL.Add(L[I[t * 3]]); nL.Add(L[I[t * 3 + 1]]); nL.Add(L[I[t * 3 + 2]]);
            nN.Add(N[I[t * 3]]); nN.Add(N[I[t * 3 + 1]]); nN.Add(N[I[t * 3 + 2]]);
            nI.Add(i0); nI.Add(i0 + 1); nI.Add(i0 + 2);
        }
        L.Clear(); L.AddRange(nL);
        N.Clear(); N.AddRange(nN);
        I.Clear(); I.AddRange(nI);
        return removed;
    }

    static void BuildBlocker(StringBuilder log)
    {
        NX = Mathf.CeilToInt((RX1 - RX0) / CELL);
        NZ = Mathf.CeilToInt((RZ1 - RZ0) / CELL);
        mn = new ushort[NX * NZ];
        mx = new ushort[NX * NZ];
        for (int i = 0; i < mn.Length; i++) mn[i] = 65535;

        var root = GameObject.Find("WorldRoot_0_0/PRIMUM").transform;
        string[] skipGroups = { "DOORS", "LAMPS", "OUTLIERS", "DOOR_PARTS_UNASSIGNED" };
        string[] baseSkip = { "_M_M_DECK_STEEL", "_M_M_PAVING_BRUSCHATKA" };

        int meshes = 0; long tris = 0;
        for (int ci = 0; ci < root.childCount; ci++)
        {
            var ch = root.GetChild(ci);
            bool isBase = ch.name.StartsWith("PRIMUM_BASE_S0");
            var renderers = ch.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                bool skip = false;
                var t = r.transform;
                while (t != null && t != ch)
                {
                    if (Array.IndexOf(skipGroups, t.name) >= 0) { skip = true; break; }
                    t = t.parent;
                }
                if (skip) continue;
                if (isBase)
                {
                    bool bad = false;
                    foreach (var bs in baseSkip) if (r.name.Contains(bs)) { bad = true; break; }
                    if (bad) continue;
                }
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var vv = mesh.vertices; var tt = mesh.triangles; var M = r.transform.localToWorldMatrix;
                meshes++; tris += tt.Length / 3;
                for (int i = 0; i < tt.Length; i += 3)
                {
                    Vector3 a = M.MultiplyPoint3x4(vv[tt[i]]), b = M.MultiplyPoint3x4(vv[tt[i + 1]]), c = M.MultiplyPoint3x4(vv[tt[i + 2]]);
                    float ylo = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), yhi = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    if (yhi < 2472.2f || ylo > 2503f) continue;
                    float bxlo = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), bxhi = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                    float bzlo = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), bzhi = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    int ix0 = Mathf.Max(0, (int)((bxlo - RX0) / CELL)), ix1 = Mathf.Min(NX - 1, (int)((bxhi - RX0) / CELL));
                    int iz0 = Mathf.Max(0, (int)((bzlo - RZ0) / CELL)), iz1 = Mathf.Min(NZ - 1, (int)((bzhi - RZ0) / CELL));
                    ushort elo = (ushort)Mathf.Clamp(Mathf.RoundToInt((ylo - QORG) / QSTEP), 0, 65534);
                    ushort ehi = (ushort)Mathf.Clamp(Mathf.RoundToInt((yhi - QORG) / QSTEP), 0, 65534);
                    float d = (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
                    if (Mathf.Abs(d) > 1e-7f)
                    {
                        float inv = 1f / d;
                        for (int iz = iz0; iz <= iz1; iz++)
                        {
                            float pz = RZ0 + (iz + 0.5f) * CELL;
                            for (int ix = ix0; ix <= ix1; ix++)
                            {
                                float px = RX0 + (ix + 0.5f) * CELL;
                                float wb = ((px - a.x) * (c.z - a.z) - (c.x - a.x) * (pz - a.z)) * inv;
                                float wc = ((b.x - a.x) * (pz - a.z) - (px - a.x) * (b.z - a.z)) * inv;
                                if (1f - wb - wc < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                                int idx = ix + iz * NX;
                                if (elo < mn[idx]) mn[idx] = elo;
                                if (ehi > mx[idx]) mx[idx] = ehi;
                            }
                        }
                    }
                    else
                    {
                        // vertical face: rasterize its edges as thick lines
                        float thr = CELL * 0.72f;
                        Vector3[] p1s = { a, b, c };
                        Vector3[] p2s = { b, c, a };
                        for (int e = 0; e < 3; e++)
                        {
                            Vector3 p1 = p1s[e], p2 = p2s[e];
                            float exlo = Mathf.Min(p1.x, p2.x), exhi = Mathf.Max(p1.x, p2.x);
                            float ezlo = Mathf.Min(p1.z, p2.z), ezhi = Mathf.Max(p1.z, p2.z);
                            int jx0 = Mathf.Max(0, (int)((exlo - thr - RX0) / CELL)), jx1 = Mathf.Min(NX - 1, (int)((exhi + thr - RX0) / CELL));
                            int jz0 = Mathf.Max(0, (int)((ezlo - thr - RZ0) / CELL)), jz1 = Mathf.Min(NZ - 1, (int)((ezhi + thr - RZ0) / CELL));
                            float dx = p2.x - p1.x, dz = p2.z - p1.z;
                            float ll = dx * dx + dz * dz;
                            for (int iz = jz0; iz <= jz1; iz++)
                            {
                                float pz = RZ0 + (iz + 0.5f) * CELL;
                                for (int ix = jx0; ix <= jx1; ix++)
                                {
                                    float px = RX0 + (ix + 0.5f) * CELL;
                                    float tt2 = ll > 1e-9f ? Mathf.Clamp01(((px - p1.x) * dx + (pz - p1.z) * dz) / ll) : 0f;
                                    float qx = p1.x + dx * tt2 - px, qz = p1.z + dz * tt2 - pz;
                                    if (qx * qx + qz * qz > thr * thr) continue;
                                    int idx = ix + iz * NX;
                                    if (elo < mn[idx]) mn[idx] = elo;
                                    if (ehi > mx[idx]) mx[idx] = ehi;
                                }
                            }
                        }
                    }
                }
            }
        }
        log.AppendLine("blocker meshes " + meshes + " tris " + tris + " grid " + NX + "x" + NZ + " cell " + CELL);
    }
}
