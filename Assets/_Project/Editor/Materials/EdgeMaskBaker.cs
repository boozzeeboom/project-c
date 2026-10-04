using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    // EdgeMaskBaker — автобейк масок стык/кромка в vertex colors (R/G) из координат меша.
    // T-STEEL01. Договор каналов: R = вогнутость (стыки), G = выпуклость (кромки).
    // Ширина полосы — мировой радиус размытия (_radiusMeters), задаётся при запечке.
    // Никакого художника, текстур и ручной работы: двугранные углы + Dijkstra-blur.
    public static class EdgeMaskBaker
    {
        private const string OutFolder = "Assets/_Project/Generated/EdgeBaked";

        [MenuItem("Tools/ProjectC/Materials/Check Vertex Colors on Selected")]
        public static void CheckColorsMenu()
        {
            var gos = Selection.gameObjects;
            if (gos == null || gos.Length == 0)
            {
                Debug.LogWarning("[EdgeMaskBaker] Ничего не выбрано.");
                return;
            }
            foreach (var go in gos)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { Debug.Log("[EdgeMaskBaker] " + go.name + ": нет меша."); continue; }
                Mesh m = mf.sharedMesh;
                Color[] c = m.colors;
                if (c == null || c.Length == 0)
                {
                    Debug.Log("[EdgeMaskBaker] " + go.name + ": ЦВЕТОВ НЕТ — шейдер прочтёт как БЕЛЫЕ (1,1,1,1). " +
                        "Либо запеки (Bake Edge Masks), либо держи _UseVertexColors=0.", go);
                    continue;
                }
                float sr = 0f, sg = 0f, sb = 0f;
                foreach (var col in c) { sr += col.r; sg += col.g; sb += col.b; }
                sr /= c.Length; sg /= c.Length; sb /= c.Length;
                bool allWhite = sr > 0.99f && sg > 0.99f && sb > 0.99f;
                Debug.Log("[EdgeMaskBaker] " + go.name + ": цветов=" + c.Length +
                    " средний RGB=(" + sr.ToString("F2") + "," + sg.ToString("F2") + "," + sb.ToString("F2") + ")" +
                    (allWhite ? " — ВСЕ БЕЛЫЕ = незапечённый (ProBuilder-дефолт). Запеки или _UseVertexColors=0." : " — похоже на запечённые, можно _UseVertexColors=1."), go);
            }
        }

        [MenuItem("Tools/ProjectC/Materials/Bake Edge Masks (R/G)...")]
        public static void BakeSelectedMenu()
        {
            var gos = Selection.gameObjects;
            if (gos == null || gos.Length == 0)
            {
                Debug.LogWarning("[EdgeMaskBaker] Ничего не выбрано. Выбери объекты с MeshFilter.");
                return;
            }
            int ok = 0, fail = 0;
            foreach (var go in gos)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null) { Debug.LogWarning("[EdgeMaskBaker] " + go.name + ": нет MeshFilter, пропуск."); fail++; continue; }
                string stats = BakeMesh(mf, 0.15f, 5f, true);
                if (stats.StartsWith("ERR"))
                {
                    Debug.LogError("[EdgeMaskBaker] " + go.name + ": " + stats + " — меш НЕ изменён!", go);
                    fail++;
                    continue;
                }
                Debug.Log("[EdgeMaskBaker] " + go.name + ": " + stats);
                ok++;
            }
            Debug.Log("[EdgeMaskBaker] Готово: успешно " + ok + ", ошибок " + fail + " (из " + gos.Length + ")");
        }

        // Демо/автоматизация без выделения: печёт все MeshFilter на объектах SteelDemo_*.
        [MenuItem("Tools/ProjectC/Materials/Bake SteelDemo Objects...")]
        public static void BakeDemoMenu()
        {
            var all = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
            int ok = 0, fail = 0, skip = 0;
            foreach (var mf in all)
            {
                if (mf == null || mf.gameObject == null) continue;
                if (!mf.gameObject.name.StartsWith("SteelDemo_")) { skip++; continue; }
                string stats = BakeMesh(mf, 0.03f, 5f, true);
                if (stats.StartsWith("ERR")) { Debug.LogError("[EdgeMaskBaker] " + mf.gameObject.name + ": " + stats, mf.gameObject); fail++; continue; }
                Debug.Log("[EdgeMaskBaker] " + mf.gameObject.name + ": " + stats);
                ok++;
            }
            Debug.Log("[EdgeMaskBaker] Демо-бейк: успешно " + ok + ", ошибок " + fail + ", пропущено " + skip);
        }

        // Печёт R/G в дубликат меша. saveAsset=false — только в память (для тестов).
        // Возвращает строку статистики (или ошибку).
        public static string BakeMesh(MeshFilter mf, float radiusMeters, float minAngleDeg, bool saveAsset)
        {
            if (mf == null) return "ERR no MeshFilter";
            Mesh src = mf.sharedMesh;
            if (src == null) return "ERR no mesh";

            if (!src.isReadable)
            {
                string assetPath = AssetDatabase.GetAssetPath(src);
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) return "ERR mesh not readable (procedural/builtin without read flag)";
                importer.isReadable = true;
                importer.SaveAndReimport();
                src = mf.sharedMesh;
                if (src == null || !src.isReadable) return "ERR still not readable after reimport";
            }

            // Ширина в мировых метрах: радиус делим на масштаб объекта.
            float maxS = Mathf.Max(mf.transform.lossyScale.x, Mathf.Max(mf.transform.lossyScale.y, mf.transform.lossyScale.z));
            if (maxS < 1e-4f) maxS = 1f;
            float effRadius = radiusMeters / maxS;
            if (src.name.Contains("_EdgeBaked"))
                Debug.LogWarning("[EdgeMaskBaker] Меш уже запечён (" + src.name + ") — повторный бейк складывается. Для чистого результата верни оригинальный меш.", mf);

            Vector3[] verts = src.vertices;
            int[] tris = src.triangles;
            if (tris.Length < 3) return "ERR no triangles";
            int triCount = tris.Length / 3;

            // 1. Сварка позиций (куб из 24 вершин -> 8 точек).
            const float q = 10000f;
            var posId = new Dictionary<Vector3Int, int>();
            int[] vPid = new int[verts.Length];
            var posList = new List<Vector3>();
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * q), Mathf.RoundToInt(v.y * q), Mathf.RoundToInt(v.z * q));
                int id;
                if (!posId.TryGetValue(key, out id)) { id = posList.Count; posId[key] = id; posList.Add(v); }
                vPid[i] = id;
            }
            int pCount = posList.Count;
            Vector3[] pos = posList.ToArray();

            // 2. Нормали граней.
            Vector3[] faceN = new Vector3[triCount];
            for (int f = 0; f < triCount; f++)
            {
                Vector3 a = verts[tris[f * 3]], b = verts[tris[f * 3 + 1]], c = verts[tris[f * 3 + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                faceN[f] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            }

            // 3. Рёбра: неориентированный ключ -> (нормаль первой грани, направленное ребро первой грани).
            var edgeFaces = new Dictionary<long, List<int>>();
            var edgeDir = new Dictionary<long, Vector3>(); // направление a->b из первой грани
            for (int f = 0; f < triCount; f++)
            {
                int i0 = tris[f * 3], i1 = tris[f * 3 + 1], i2 = tris[f * 3 + 2];
                int[] loop = { i0, i1, i2, i0 };
                for (int e = 0; e < 3; e++)
                {
                    int pa = vPid[loop[e]], pb = vPid[loop[e + 1]];
                    if (pa == pb) continue;
                    long key = ((long)Mathf.Min(pa, pb) << 32) | (uint)Mathf.Max(pa, pb);
                    List<int> list;
                    if (!edgeFaces.TryGetValue(key, out list)) { list = new List<int>(2); edgeFaces[key] = list; }
                    if (list.Count == 0)
                        edgeDir[key] = pos[pb] - pos[pa]; // направление из первой встреченной грани
                    if (list.Count < 2) list.Add(f);
                }
            }

            // 4. Двугранные углы -> convex (G) / concave (R) на точках.
            float minRad = minAngleDeg * Mathf.Deg2Rad;
            float[] convex = new float[pCount];
            float[] concave = new float[pCount];
            int sharpEdges = 0;
            foreach (var kv in edgeFaces)
            {
                var list = kv.Value;
                if (list.Count < 2) continue; // граница/шов — не ребро жёсткости
                Vector3 n0 = faceN[list[0]], n1 = faceN[list[1]];
                float cosA = Mathf.Clamp(Vector3.Dot(n0, n1), -1f, 1f);
                float ang = Mathf.Acos(cosA);
                if (ang < minRad) continue;
                sharpEdges++;
                Vector3 d = edgeDir[kv.Key];
                if (d.sqrMagnitude < 1e-12f) continue;
                d.Normalize();
                float s = Vector3.Dot(Vector3.Cross(n0, n1), d);
                float norm = Mathf.Clamp01((ang * Mathf.Rad2Deg - minAngleDeg) / Mathf.Max(45f - minAngleDeg, 1f));
                // Точки ребра: распаковка ключа.
                int pa = (int)(kv.Key >> 32), pb = (int)(kv.Key & 0xffffffff);
                if (s > 0f) { if (norm > convex[pa]) convex[pa] = norm; if (norm > convex[pb]) convex[pb] = norm; }
                else { if (norm > concave[pa]) concave[pa] = norm; if (norm > concave[pb]) concave[pb] = norm; }
            }

            // 5. Сграф-смежность для blur на мировой радиус.
            var adj = new List<int>[pCount];
            var adjLen = new List<float>[pCount];
            for (int i = 0; i < pCount; i++) { adj[i] = new List<int>(); adjLen[i] = new List<float>(); }
            var adjSet = new HashSet<long>();
            for (int f = 0; f < triCount; f++)
            {
                int a = vPid[tris[f * 3]], b = vPid[tris[f * 3 + 1]], c = vPid[tris[f * 3 + 2]];
                AddLink(adj, adjLen, adjSet, pos, a, b);
                AddLink(adj, adjLen, adjSet, pos, b, c);
                AddLink(adj, adjLen, adjSet, pos, c, a);
            }
            float[] blurG = BlurRadius(convex, adj, adjLen, effRadius);
            float[] blurR = BlurRadius(concave, adj, adjLen, effRadius);

            // 6. Запись цветов (B/A сохраняем, иначе нейтраль).
            Color[] oldC = src.colors;
            bool hasC = oldC != null && oldC.Length == verts.Length;
            var cols = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                int pid = vPid[i];
                cols[i] = new Color(blurR[pid], blurG[pid], hasC ? oldC[i].b : 0.5f, hasC ? oldC[i].a : 1f);
            }

            Mesh baked = Object.Instantiate(src);
            baked.name = src.name + "_EdgeBaked";
            baked.colors = cols;

            if (saveAsset)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Generated");
                if (!AssetDatabase.IsValidFolder(OutFolder))
                    AssetDatabase.CreateFolder("Assets/_Project/Generated", "EdgeBaked");
                string path = AssetDatabase.GenerateUniqueAssetPath(OutFolder + "/" + baked.name + ".asset");
                AssetDatabase.CreateAsset(baked, path);
                AssetDatabase.SaveAssets();
            }

            Undo.RecordObject(mf, "Bake Edge Masks");
            mf.sharedMesh = baked;

            // Подсказка: печёные R/G подхватит только _UseVertexColors=1 на материалах объекта.
            Debug.Log("[EdgeMaskBaker] " + mf.gameObject.name + ": маски записаны. " +
                "Включи _UseVertexColors=1 на материалах ProceduralSteel этого объекта, иначе шейдер их игнорирует.");

            float rMax = 0f, rSum = 0f, gMax = 0f, gSum = 0f;
            for (int i = 0; i < verts.Length; i++)
            {
                if (cols[i].r > rMax) rMax = cols[i].r; rSum += cols[i].r;
                if (cols[i].g > gMax) gMax = cols[i].g; gSum += cols[i].g;
            }
            return "OK verts=" + verts.Length + " pts=" + pCount + " sharpEdges=" + sharpEdges
                + " Rmax=" + rMax.ToString("F2") + " Ravg=" + (rSum / verts.Length).ToString("F3")
                + " Gmax=" + gMax.ToString("F2") + " Gavg=" + (gSum / verts.Length).ToString("F3")
                + (saveAsset ? "" : " (mem-only)");
        }

        private static void AddLink(List<int>[] adj, List<float>[] adjLen, HashSet<long> set, Vector3[] pos, int a, int b)
        {
            if (a == b) return;
            long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            if (!set.Add(key)) return;
            float len = (pos[a] - pos[b]).magnitude;
            adj[a].Add(b); adjLen[a].Add(len);
            adj[b].Add(a); adjLen[b].Add(len);
        }

        // Multi-source Dijkstra: val = max(0, 1 - Wmin/radius), W = pathLen + (1-srcVal)*radius.
        private static float[] BlurRadius(float[] srcVal, List<int>[] adj, List<float>[] adjLen, float radius)
        {
            int n = srcVal.Length;
            var best = new float[n];
            for (int i = 0; i < n; i++) best[i] = float.PositiveInfinity;
            var pq = new SortedSet<BNode>(new BNodeCmp());
            long seq = 0;
            for (int i = 0; i < n; i++)
            {
                if (srcVal[i] > 0.001f)
                {
                    float w = (1f - srcVal[i]) * radius;
                    if (w < best[i]) { best[i] = w; pq.Add(new BNode { w = w, id = i, s = seq++ }); }
                }
            }
            while (pq.Count > 0)
            {
                BNode cur = pq.Min;
                pq.Remove(cur);
                if (cur.w > best[cur.id]) continue;
                var nb = adj[cur.id]; var ln = adjLen[cur.id];
                for (int k = 0; k < nb.Count; k++)
                {
                    float nw = cur.w + ln[k];
                    if (nw < best[nb[k]] && nw <= radius)
                    {
                        best[nb[k]] = nw;
                        pq.Add(new BNode { w = nw, id = nb[k], s = seq++ });
                    }
                }
            }
            var outV = new float[n];
            for (int i = 0; i < n; i++)
                outV[i] = float.IsPositiveInfinity(best[i]) ? 0f : Mathf.Clamp01(1f - best[i] / radius);
            return outV;
        }

        private struct BNode { public float w; public int id; public long s; }
        private class BNodeCmp : IComparer<BNode>
        {
            public int Compare(BNode a, BNode b)
            {
                int c = a.w.CompareTo(b.w);
                if (c != 0) return c;
                return a.s.CompareTo(b.s);
            }
        }
    }
}
