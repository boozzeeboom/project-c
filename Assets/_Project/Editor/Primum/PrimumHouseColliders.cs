using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM, ДОМА: точные коллайдеры из ИСХОДНОЙ (несклеенной) геометрии RAW-чанков.
    ///
    /// Почему не боксы: бокс по габариту не умеет дыр. Из-за этого в трёх предыдущих попытках
    /// заливались дверные и оконные проёмы, слэб с проёмом лестницы и «крыша по дому» заваривали
    /// марши, а задняя стена терялась в слиянии (см. docs/world/primum_city/04_COLLIDERS/
    /// 04_STATUS_AND_GAPS.md, §1 и §9).
    ///
    /// Как: Blender-скрипт <c>tools/houses_export.py</c> берёт RAW-чанки, отбирает объекты по
    /// грамматике имени (&lt;ДОМ&gt;_&lt;слот/этаж&gt;_&lt;ВИД&gt;_…) и пишет ОДИН OBJ на чанк с группами
    /// <c>&lt;чанк&gt;__&lt;КЛАСС&gt;__&lt;дом&gt;</c> в координатах корня PRIMUM_COLLIDERS.
    /// Здесь каждая группа (чанк × класс) склеивается в один испечённый меш-ассет и получает
    /// non-convex <see cref="MeshCollider"/> — ровно так же, как уже работают <c>PM_STAIRS_*</c>
    /// и <c>PM_SURFACE_*</c> для BASE.
    ///
    /// Классы: WALL (стены с настоящими проёмами), FLOOR (полы + SLAB — этажные перекрытия),
    /// ROOF (крыши/парапеты), STAIR (марши, ступени, крыльца).
    ///
    /// Куда: <c>WorldRoot_0_0/PRIMUM_COLLIDERS/&lt;чанк&gt;/HOUSE_&lt;КЛАСС&gt;</c>,
    /// меши — <c>Assets/_Project/Prefabs/City/Colliders/Houses/</c>.
    /// Трансформ объектов — identity: координаты в OBJ уже в пространстве корня коллайдеров
    /// (он копирует трансформ PRIMUM и едет вместе с ребейзом мира). static-флаги НЕ ставим.
    /// Идемпотентно: прежние группы <c>HOUSE_*</c> чанка сносятся перед сборкой.
    ///
    /// Меню: ProjectC → World → PRIMUM → Коллайдеры домов.
    /// </summary>
    public static class PrimumHouseColliders
    {
        private const string ObjFolderRelative = "docs/world/primum_city/04_COLLIDERS/houses_obj";
        private const string MeshAssetFolder = "Assets/_Project/Prefabs/City/Colliders/Houses";
        private const string CollidersRootName = "PRIMUM_COLLIDERS";
        private const string CityRootName = "PRIMUM";
        private const string WorldRootFallback = "WorldRoot_0_0";
        private const string GroupPrefix = "HOUSE_";
        private const int CityLayer = 8;   // PRIMUM

        /// <summary>Старые боксовые группы домов (PrimumCollidersBuilder) — заменены мешами.</summary>
        private static readonly string[] LegacyBoxGroups = { "WALL", "FLOOR", "ROOF" };

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: меши из RAW (пересобрать)", false, 360)]
        public static void MenuBuild()
        {
            Report r = Build(true);
            Debug.Log(Describe(r));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: отчёт (без изменений)", false, 361)]
        public static void MenuReport()
        {
            Debug.Log(Describe(Build(false)));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: снести боксы WALL/FLOOR/ROOF", false, 362)]
        public static void MenuRemoveLegacyBoxes()
        {
            int n = RemoveLegacyBoxes();
            Debug.Log("[PRIMUM/Houses] снесено боксовых групп домов: " + n +
                      " (WALL/FLOOR/ROOF; их заменили MeshCollider'ы)");
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры домов: проверить дома (лучи)", false, 363)]
        public static void MenuVerify()
        {
            Debug.Log(VerifyHouses());
        }

        // ------------------------------------------------------------------ отчёт

        public class Report
        {
            public int Chunks;
            public int Groups;
            public int Meshes;
            public int Colliders;
            public int Vertices;
            public int Triangles;
            public readonly Dictionary<string, int> ByClass = new Dictionary<string, int>();
            public readonly Dictionary<string, string> ChunkSummary = new Dictionary<string, string>();
            public readonly List<string> Warnings = new List<string>();
        }

        private class Group
        {
            public string Chunk;
            public string Class;
            public readonly List<int> Tris = new List<int>();   // индексы в общий массив вершин файла
        }

        // ------------------------------------------------------------------ сборка

        public static Report Build(bool apply)
        {
            var rep = new Report();

            GameObject city = GameObject.Find(CityRootName);
            if (city == null) { rep.Warnings.Add("Не найден корень " + CityRootName); return rep; }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string objDir = Path.Combine(projectRoot, ObjFolderRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(objDir))
            {
                rep.Warnings.Add("Нет папки с OBJ: " + objDir + " — сначала прогоните tools/houses_export.py");
                return rep;
            }

            string[] files = Directory.GetFiles(objDir, "*.obj");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length == 0)
            {
                rep.Warnings.Add("В " + objDir + " нет ни одного .obj");
                return rep;
            }

            Transform worldRoot = city.transform.parent != null ? city.transform.parent
                                : GameObject.Find(WorldRootFallback)?.transform;
            if (worldRoot == null) { rep.Warnings.Add("Не найден world root"); return rep; }

            Transform root = null;
            if (apply)
            {
                GameObject rootGo = GameObject.Find(CollidersRootName);
                if (rootGo == null) rootGo = new GameObject(CollidersRootName);
                rootGo.transform.SetParent(worldRoot, false);
                rootGo.transform.position = city.transform.position;
                rootGo.transform.rotation = city.transform.rotation;
                rootGo.transform.localScale = Vector3.one;
                rootGo.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(rootGo, (StaticEditorFlags)0);
                root = rootGo.transform;
            }

            string meshDirFull = Path.Combine(projectRoot, MeshAssetFolder.Replace('/', Path.DirectorySeparatorChar));
            if (apply && !Directory.Exists(meshDirFull)) Directory.CreateDirectory(meshDirFull);

            foreach (string file in files)
            {
                ParseObj(file, rep, apply, root, meshDirFull);
            }

            if (apply)
            {
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(city.scene);
            }
            return rep;
        }

        private static void ParseObj(string path, Report rep, bool apply, Transform root, string meshDirFull)
        {
            string chunk = Path.GetFileNameWithoutExtension(path);

            var verts = new List<Vector3>();
            var groups = new List<Group>();
            var byClass = new Dictionary<string, Group>();
            Group current = null;

            foreach (string raw in File.ReadLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line.StartsWith("o ", StringComparison.Ordinal))
                {
                    string name = line.Substring(2).Trim();
                    // <чанк>__<КЛАСС>__<дом>
                    string[] p = name.Split(new[] { "__" }, StringSplitOptions.None);
                    string cls = p.Length >= 2 ? p[1] : "OTHER";
                    Group g;
                    if (!byClass.TryGetValue(cls, out g))
                    {
                        g = new Group { Chunk = chunk, Class = cls };
                        byClass[cls] = g;
                        groups.Add(g);
                    }
                    current = g;
                    continue;
                }

                if (line[0] == 'v' && (line.Length == 1 || line[1] == ' '))
                {
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4)
                        verts.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                    continue;
                }

                if (line[0] == 'f' && current != null)
                {
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var idx = new List<int>(p.Length - 1);
                    for (int i = 1; i < p.Length; i++)
                    {
                        string tok = p[i];
                        int slash = tok.IndexOf('/');
                        if (slash > 0) tok = tok.Substring(0, slash);
                        int vi;
                        if (int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out vi))
                            idx.Add(vi - 1);
                    }
                    if (idx.Count == 3) { current.Tris.Add(idx[0]); current.Tris.Add(idx[1]); current.Tris.Add(idx[2]); }
                    else if (idx.Count >= 4)
                        for (int i = 1; i + 1 < idx.Count; i++)
                        {
                            current.Tris.Add(idx[0]); current.Tris.Add(idx[i]); current.Tris.Add(idx[i + 1]);
                        }
                }
            }

            if (verts.Count == 0 || groups.Count == 0)
            {
                rep.Warnings.Add(Path.GetFileName(path) + ": пусто");
                return;
            }

            rep.Chunks++;
            var summary = new StringBuilder();

            foreach (Group g in groups)
            {
                rep.Groups++;
                int gtri = g.Tris.Count / 3;
                summary.Append(g.Class).Append("=").Append(gtri).Append(' ');
                rep.Triangles += gtri;
                int n; rep.ByClass.TryGetValue(g.Class, out n); rep.ByClass[g.Class] = n + gtri;

                if (!apply) continue;
                if (gtri < 8) continue;   // огрызки не нужны

                // общий массив вершин файла → локальный массив меша этого класса
                var map = new Dictionary<int, int>(g.Tris.Count);
                var vl = new List<Vector3>();
                var tl = new List<int>(g.Tris.Count);
                for (int i = 0; i < g.Tris.Count; i++)
                {
                    int gi = g.Tris[i];
                    if (gi < 0 || gi >= verts.Count) continue;
                    int li;
                    if (!map.TryGetValue(gi, out li)) { li = vl.Count; vl.Add(verts[gi]); map[gi] = li; }
                    tl.Add(li);
                }
                if (tl.Count < 3) continue;

                string shortChunk = ShortChunk(chunk);
                string meshName = "PM_HOUSE_" + g.Class + "_" + shortChunk;
                Mesh mesh = BakeMesh(meshName, vl, tl, meshDirFull);
                if (mesh == null) { rep.Warnings.Add(meshName + ": меш не собран"); continue; }
                rep.Meshes++;
                rep.Vertices += vl.Count;

                Transform chunkNode = EnsureChunkNode(root, chunk);
                Transform prev = chunkNode.Find(GroupPrefix + g.Class);
                if (prev != null) UnityEngine.Object.DestroyImmediate(prev.gameObject);

                var go = new GameObject(GroupPrefix + g.Class);
                go.transform.SetParent(chunkNode, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);

                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                                  | MeshColliderCookingOptions.EnableMeshCleaning
                                  | MeshColliderCookingOptions.WeldColocatedVertices
                                  | MeshColliderCookingOptions.UseFastMidphase;
                rep.Colliders++;
            }

            rep.ChunkSummary[chunk] = summary.ToString().TrimEnd();
        }

        private static Transform EnsureChunkNode(Transform root, string chunk)
        {
            Transform t = root.Find(chunk);
            if (t != null) return t;
            var go = new GameObject(chunk);
            go.transform.SetParent(root, false);
            go.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
            return go.transform;
        }

        private static string ShortChunk(string chunk)
        {
            return chunk.StartsWith("PRIMUM_", StringComparison.Ordinal) ? chunk.Substring(7) : chunk;
        }

        private static Mesh BakeMesh(string name, List<Vector3> verts, List<int> tris, string meshDirFull)
        {
            if (verts.Count == 0 || tris.Count < 3) return null;
            string assetPath = MeshAssetFolder + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh(); else mesh.Clear();

            mesh.name = name;
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            if (created) AssetDatabase.CreateAsset(mesh, assetPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ снос боксов

        /// <summary>Удаляет старые боксовые группы домов (WALL/FLOOR/ROOF) во всех чанках узла коллайдеров.</summary>
        public static int RemoveLegacyBoxes()
        {
            GameObject rootGo = GameObject.Find(CollidersRootName);
            if (rootGo == null) return 0;

            int removed = 0;
            var toKill = new List<GameObject>();
            foreach (Transform chunk in rootGo.transform)
            {
                for (int i = chunk.childCount - 1; i >= 0; i--)
                {
                    Transform c = chunk.GetChild(i);
                    foreach (string legacy in LegacyBoxGroups)
                    {
                        if (!string.Equals(c.name, legacy, StringComparison.OrdinalIgnoreCase)) continue;
                        removed += c.GetComponentsInChildren<Collider>(true).Length;
                        toKill.Add(c.gameObject);
                        break;
                    }
                }
            }
            foreach (GameObject go in toKill) UnityEngine.Object.DestroyImmediate(go);

            if (removed > 0)
            {
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            return removed;
        }

        // ------------------------------------------------------------------ проверка

        /// <summary>
        /// Лучи по геометрии домов: берём реальные верхние грани мешей WALL/FLOOR/ROOF/STAIR чанка
        /// и проверяем, что сверху есть коллайдер и что он не выше самой поверхности.
        /// </summary>
        public static string VerifyHouses()
        {
            GameObject city = GameObject.Find(CityRootName);
            if (city == null) return "[PRIMUM/Houses] Не найден корень " + CityRootName;

            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Houses] Проверка точных мешей домов (лучи вниз)");

            int chunkCount = 0, samples = 0, hits = 0, above = 0, below = 0;
            float worstAbove = 0f;
            string worstName = "";

            foreach (Transform chunk in city.transform)
            {
                if (!chunk.name.StartsWith("PRIMUM_HOUSES_", StringComparison.Ordinal)) continue;
                chunkCount++;

                // геометрия домов — по именам объектов (WALL/FLOOR/SLAB/ROOF/STAIR), координаты в корне
                Matrix4x4 toRoot = city.transform.worldToLocalMatrix;
                int chunkSamples = 0, chunkHits = 0, chunkAbove = 0, chunkBelow = 0;

                foreach (MeshFilter mf in chunk.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    string n = mf.gameObject.name.ToUpperInvariant();
                    if (!(n.Contains("_WALL") || n.Contains("_FLOOR") || n.Contains("_SLAB")
                          || n.Contains("_ROOF") || n.Contains("_STAIR") || n.Contains("_STEPS"))) continue;

                    Vector3[] v = mf.sharedMesh.vertices;
                    int[] tr = mf.sharedMesh.triangles;
                    if (tr.Length < 3) continue;
                    Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;

                    // редкая выборка: каждый N-й треугольник, только горизонтальные грани
                    int step = Mathf.Max(1, tr.Length / 3 / 12);
                    for (int t = 0; t < tr.Length; t += 3 * step)
                    {
                        Vector3 a = m.MultiplyPoint3x4(v[tr[t]]);
                        Vector3 b = m.MultiplyPoint3x4(v[tr[t + 1]]);
                        Vector3 c = m.MultiplyPoint3x4(v[tr[t + 2]]);
                        Vector3 cr = Vector3.Cross(b - a, c - a);
                        if (cr.sqrMagnitude < 1e-9f) continue;
                        if (cr.y / cr.magnitude < 0.9f) continue;

                        Vector3 mid = (a + b + c) / 3f;
                        Vector3 world = city.transform.TransformPoint(new Vector3(mid.x, mid.y + 40f, mid.z));
                        samples++; chunkSamples++;

                        RaycastHit[] hh = Physics.RaycastAll(world, Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
                        bool found = false;
                        float best = float.MinValue;
                        foreach (RaycastHit h in hh)
                        {
                            if (!IsUnderColliders(h.collider.transform)) continue;
                            float ly = city.transform.InverseTransformPoint(h.point).y;
                            if (!found || ly > best) { best = ly; found = true; }
                        }
                        if (!found) { below++; chunkBelow++; continue; }
                        hits++; chunkHits++;
                        float d = best - mid.y;
                        if (d > 0.5f)
                        {
                            above++; chunkAbove++;
                            if (d > worstAbove) { worstAbove = d; worstName = chunk.name + "/" + mf.gameObject.name; }
                        }
                    }
                }

                if (chunkSamples > 0)
                    sb.AppendLine("  " + chunk.name.Replace("PRIMUM_HOUSES_", "") + ": точек " + chunkSamples
                                  + ", попало " + chunkHits + ", выше поверхности " + chunkAbove
                                  + ", промах " + chunkBelow);
            }

            sb.AppendLine("  чанков: " + chunkCount);
            sb.AppendLine("  точек-поверхностей: " + samples + ", попало в коллайдер домов: " + hits);
            sb.AppendLine("  коллайдер выше поверхности (>0.5 м): " + above + "  макс " + worstAbove.ToString("F2") + " м " + worstName);
            sb.AppendLine("  промахов в пустоту: " + below);
            return sb.ToString();
        }

        private static bool IsUnderColliders(Transform t)
        {
            Transform p = t;
            while (p != null)
            {
                if (p.name == CollidersRootName) return true;
                p = p.parent;
            }
            return false;
        }

        // ------------------------------------------------------------------ утилиты

        private static float F(string s)
        {
            float v;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static string Describe(Report r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Houses] Точные меши домов из RAW (WALL / FLOOR+SLAB / ROOF / STAIR)");
            sb.AppendLine("  чанков: " + r.Chunks + ", групп (чанк×класс×дом): " + r.Groups);
            sb.AppendLine("  мешей-ассетов: " + r.Meshes + ", MeshCollider'ов: " + r.Colliders);
            sb.AppendLine("  вершин: " + r.Vertices + ", треугольников: " + r.Triangles);
            if (r.ByClass.Count > 0)
            {
                sb.Append("  по классам (треугольников):");
                foreach (var kv in r.ByClass) sb.Append(" " + kv.Key + "=" + kv.Value);
                sb.AppendLine();
            }
            foreach (var kv in r.ChunkSummary) sb.AppendLine("   " + kv.Key.Replace("PRIMUM_", "") + ": " + kv.Value);
            foreach (string w in r.Warnings) sb.AppendLine("  ! " + w);
            return sb.ToString();
        }
    }
}
