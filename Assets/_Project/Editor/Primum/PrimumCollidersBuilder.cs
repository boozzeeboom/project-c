using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM: применение бокс-коллайдеров из CSV (сгенерированного в Blender скриптом
    /// <c>_analysis/colliders_v2.py</c> из ИСХОДНОЙ геометрии RAW-блендов).
    ///
    /// Классы: WALK (низ города/улицы), STAIR (наклонные боксы маршей), WALL (стены, окна не дырками),
    /// FLOOR (полы/перекрытия), ROOF (крыша дома).
    ///
    /// Куда: <c>WorldRoot_0_0/PRIMUM_COLLIDERS/&lt;чанк&gt;/&lt;класс&gt;</c> — СОСЕД корня города, а НЕ ребёнок
    /// префаба PRIMUM: всё, что добавлялось внутрь префаб-инстанса, терялось при пересохранении префаба
    /// (так пропали SLAB3 и PRIMUM/COLLIDERS). Корень коллайдеров копирует трансформ PRIMUM и висит под
    /// WorldRoot_0_0, поэтому ребейз мира везёт его вместе с городом.
    ///
    /// static-флаги НЕ ставим (правило Floating Origin). Идемпотентно: перед сборкой старый узел удаляется.
    /// Меню: ProjectC → World → PRIMUM → Коллайдеры.
    /// </summary>
    public static class PrimumCollidersBuilder
    {
        private const string CsvRelativePath = "docs/world/primum_city/04_COLLIDERS/colliders_v3.csv";
        private const string PlatformsObjRelativePath = "docs/world/primum_city/04_COLLIDERS/platforms.obj";
        private const string PlatformMeshDir = "Assets/_Project/Prefabs/City/Colliders";
        private const string RootName = "PRIMUM_COLLIDERS";
        private const string CityRootName = "PRIMUM";
        private const string WorldRootFallback = "WorldRoot_0_0";
        private const int CityLayer = 8; // PRIMUM

        private class Row
        {
            public string Chunk, Cls, Name;
            public Vector3 Center;
            public Vector3 Size;
            public float Yaw, Pitch;
        }

        public class BuildReport
        {
            public int Rows;
            public readonly Dictionary<string, int> ByClass = new Dictionary<string, int>();
            public readonly Dictionary<string, int> CollidersByClass = new Dictionary<string, int>();
            public int Groups;
            public readonly List<string> Warnings = new List<string>();
        }

        // ---------------------------------------------------------------- меню

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры: собрать всё (WALK+STAIR+WALL+FLOOR+ROOF)", false, 340)]
        public static void MenuBuildAll()
        {
            BuildAll("WALK,STAIR,WALL,FLOOR,ROOF", true);
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры: только низ+лестницы (WALK+STAIR)", false, 341)]
        public static void MenuBuildWalkStairs()
        {
            BuildAll("WALK,STAIR", true);
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры: отчёт (без изменений)", false, 342)]
        public static void MenuReport()
        {
            BuildReport rep = BuildAll("WALK,STAIR,WALL,FLOOR,ROOF", false);
            Debug.Log(Describe(rep));
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры: удалить все", false, 343)]
        public static void MenuClear()
        {
            int n = ClearAll(true);
            Debug.Log("[PRIMUM/Colliders] Удалено боксов: " + n);
        }

        [MenuItem("ProjectC/World/PRIMUM/Коллайдеры: проверить покрытие (лучи)", false, 344)]
        public static void MenuVerify()
        {
            Debug.Log(VerifyCoverage());
        }

        // ---------------------------------------------------------------- сборка

        /// <summary>Собирает коллайдеры выбранных классов. apply=false — только отчёт.</summary>
        public static BuildReport BuildAll(string classList, bool apply)
        {
            var rep = new BuildReport();
            var wanted = new HashSet<string>();
            foreach (string s in classList.Split(',')) wanted.Add(s.Trim().ToUpperInvariant());

            List<Row> rows = LoadCsv(rep);
            rep.Rows = rows.Count;
            if (apply == false)
            {
                foreach (Row r in rows)
                {
                    if (!wanted.Contains(r.Cls)) continue;
                    ByClassInc(rep.ByClass, r.Cls);
                }
                return rep;
            }

            GameObject city = GameObject.Find(CityRootName);
            if (city == null) { rep.Warnings.Add("Не найден корень " + CityRootName); return rep; }
            Transform worldRoot = city.transform.parent != null ? city.transform.parent
                                : GameObject.Find(WorldRootFallback)?.transform;
            if (worldRoot == null) { rep.Warnings.Add("Не найден world root"); return rep; }

            ClearAll(false);

            var root = new GameObject(RootName);
            root.transform.SetParent(worldRoot, false);
            // трансформ как у города: координаты в CSV — в пространстве PRIMUM
            root.transform.position = city.transform.position;
            root.transform.rotation = city.transform.rotation;
            root.transform.localScale = Vector3.one;
            root.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(root, (StaticEditorFlags)0);

            // группа = (чанк, класс, ориентация)
            var groups = new Dictionary<string, Transform>();
            var classParent = new Dictionary<string, Transform>();

            foreach (Row r in rows)
            {
                if (!wanted.Contains(r.Cls)) continue;
                ByClassInc(rep.ByClass, r.Cls);

                string chunk = string.IsNullOrEmpty(r.Chunk) ? "BASE" : r.Chunk;
                Transform chunkT;
                if (!classParent.TryGetValue(chunk, out chunkT))
                {
                    var chunkGo = new GameObject(chunk);
                    chunkGo.transform.SetParent(root.transform, false);
                    chunkGo.layer = CityLayer;
                    GameObjectUtility.SetStaticEditorFlags(chunkGo, (StaticEditorFlags)0);
                    chunkT = chunkGo.transform;
                    classParent[chunk] = chunkT;
                }

                // ориентация: если yaw кратен 90 и pitch=0 — бокс осевой (сводим к 0/90 через перестановку)
                bool axisAligned = Mathf.Abs(r.Pitch) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 0f)) < 0.5f
                                || Mathf.Abs(r.Pitch) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 180f)) < 0.5f;
                bool quarter = Mathf.Abs(r.Pitch) < 0.5f &&
                               (Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 90f)) < 0.5f || Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 270f)) < 0.5f);

                string groupKey;
                Transform host;
                Vector3 size = r.Size;
                Vector3 center = r.Center;

                if (axisAligned)
                {
                    groupKey = chunk + "|" + r.Cls + "|axis";
                    host = GetGroup(groupKey, chunkT, r.Cls, Quaternion.identity, groups);
                }
                else if (quarter)
                {
                    // yaw=90: локальная X бокса идёт вдоль мировой Z -> переставляем X/Z и делаем осевым
                    groupKey = chunk + "|" + r.Cls + "|axis";
                    host = GetGroup(groupKey, chunkT, r.Cls, Quaternion.identity, groups);
                    size = new Vector3(r.Size.z, r.Size.y, r.Size.x);
                    center = r.Center;
                }
                else
                {
                    string key = chunk + "|" + r.Cls + "|" + Mathf.Round(r.Yaw) + "|" + Mathf.Round(r.Pitch);
                    Quaternion q = Quaternion.Euler(-r.Pitch, r.Yaw, 0f);
                    host = GetGroup(key, chunkT, r.Cls, q, groups);
                    center = Quaternion.Inverse(q) * r.Center;   // в локальные оси группы
                }

                var col = host.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.center = center;
                col.size = size;
                ByClassInc(rep.CollidersByClass, r.Cls);
            }

            rep.Groups = groups.Count;
            if (wanted.Contains("WALK") || wanted.Contains("PLATFORM"))
                BuildPlatforms(rep);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return rep;
        }

        private static Transform GetGroup(string key, Transform chunk, string cls, Quaternion rot,
                                          Dictionary<string, Transform> groups)
        {
            Transform t;
            if (groups.TryGetValue(key, out t)) return t;
            var go = new GameObject(cls);
            go.transform.SetParent(chunk, false);
            go.transform.localRotation = rot;
            go.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
            groups[key] = go.transform;
            return go.transform;
        }

        private static void ByClassInc(Dictionary<string, int> d, string k)
        {
            int v; d.TryGetValue(k, out v); d[k] = v + 1;
        }

        /// <summary>Удалить узел PRIMUM_COLLIDERS. Возвращает число удалённых коллайдеров.</summary>
        public static int ClearAll(bool markDirty)
        {
            GameObject city = GameObject.Find(CityRootName);
            Transform worldRoot = city != null && city.transform.parent != null ? city.transform.parent : null;
            GameObject root = null;
            if (worldRoot != null) root = worldRoot.Find(RootName)?.gameObject;
            if (root == null) root = GameObject.Find(RootName);
            if (root == null) return 0;
            int n = root.GetComponentsInChildren<Collider>(true).Length;
            UnityEngine.Object.DestroyImmediate(root);
            if (markDirty) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return n;
        }

        // ---------------------------------------------------------------- платформы низа города (MeshCollider)

        /// <summary>
        /// Платформы низа города (9 объектов из BASE/20_PLATFORMS) — точные MeshCollider'ы.
        /// Восемь боксов не работают: в платформах есть ВЫРЕЗЫ под лестницы, бокс их перекрывает.
        /// Геометрия выгружена Blender-скриптом в platforms.obj (локальные координаты PRIMUM).
        /// </summary>
        private static void BuildPlatforms(BuildReport rep)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(projectRoot, PlatformsObjRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) { rep.Warnings.Add("platforms.obj не найден: " + PlatformsObjRelativePath); return; }

            string[] lines = File.ReadAllLines(full, Encoding.UTF8);
            var verts = new List<Vector3>();
            var objects = new List<string>();
            var faces = new Dictionary<string, List<int>>();

            string current = null;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("o "))
                {
                    current = line.Substring(2).Trim();
                    objects.Add(current);
                    faces[current] = new List<int>();
                }
                else if (line.StartsWith("v "))
                {
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    verts.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                }
                else if (line.StartsWith("f ") && current != null)
                {
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var idx = new List<int>();
                    for (int i = 1; i < p.Length; i++)
                    {
                        string token = p[i];
                        int slash = token.IndexOf('/');
                        if (slash > 0) token = token.Substring(0, slash);
                        int vi;
                        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out vi))
                            idx.Add(vi - 1);
                    }
                    var list = faces[current];
                    if (idx.Count == 3) { list.Add(idx[0]); list.Add(idx[1]); list.Add(idx[2]); }
                    else if (idx.Count >= 4)
                    {
                        for (int i = 1; i + 1 < idx.Count; i++)
                        {
                            list.Add(idx[0]); list.Add(idx[i]); list.Add(idx[i + 1]);
                        }
                    }
                }
            }
            if (objects.Count == 0) { rep.Warnings.Add("platforms.obj пуст"); return; }

            GameObject city = GameObject.Find(CityRootName);
            Transform worldRoot = city != null && city.transform.parent != null ? city.transform.parent : null;
            Transform root = worldRoot != null ? worldRoot.Find(RootName) : null;
            if (root == null) { rep.Warnings.Add("сначала создайте узел " + RootName + " (сборка CSV)"); return; }

            string chunkName = "PRIMUM_BASE_S0_CITY_P1_00H";
            Transform chunk = root.Find(chunkName);
            if (chunk == null)
            {
                var chunkGo = new GameObject(chunkName);
                chunkGo.transform.SetParent(root, false);
                chunkGo.layer = CityLayer;
                GameObjectUtility.SetStaticEditorFlags(chunkGo, (StaticEditorFlags)0);
                chunk = chunkGo.transform;
            }

            if (!Directory.Exists(Path.Combine(projectRoot, PlatformMeshDir)))
                Directory.CreateDirectory(Path.Combine(projectRoot, PlatformMeshDir));

            foreach (string name in objects)
            {
                List<int> tris = faces[name];
                if (tris.Count < 3) continue;

                // индексы вершин OBJ глобальные -> локальные для объекта
                var map = new Dictionary<int, int>();
                var vlist = new List<Vector3>();
                var tlist = new List<int>();
                foreach (int gi in tris)
                {
                    int li;
                    if (!map.TryGetValue(gi, out li))
                    {
                        li = vlist.Count;
                        vlist.Add(verts[gi]);
                        map[gi] = li;
                    }
                    tlist.Add(li);
                }

                var mesh = new Mesh { name = "PM_PLATFORM_" + name };
                mesh.SetVertices(vlist);
                mesh.SetTriangles(tlist, 0);
                mesh.RecalculateBounds();
                string assetPath = PlatformMeshDir + "/PM_PLATFORM_" + name + ".asset";
                AssetDatabase.CreateAsset(mesh, assetPath);

                var go = new GameObject("PLATFORM_" + name);
                go.layer = CityLayer;
                go.transform.SetParent(chunk, false);
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                                  | MeshColliderCookingOptions.EnableMeshCleaning
                                  | MeshColliderCookingOptions.WeldColocatedVertices
                                  | MeshColliderCookingOptions.UseFastMidphase;
                ByClassInc(rep.CollidersByClass, "PLATFORM");
            }
        }
        // ---------------------------------------------------------------- проверка лучей

        /// <summary>
        /// Лучи вниз по пятну города: считает, в какую долю точек есть попадание.
        /// Дополнительно ищет «верхний» уровень попадания по классу WALK.
        /// </summary>
        public static string VerifyCoverage()
        {
            GameObject city = GameObject.Find(CityRootName);
            if (city == null) return "[PRIMUM/Colliders] город не найден";
            Bounds cityBounds = new Bounds();
            bool first = true;
            foreach (var r in city.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { cityBounds = r.bounds; first = false; } else cityBounds.Encapsulate(r.bounds);
            }

            Physics.SyncTransforms();
            int gridX = 40, gridZ = 30;
            int hits = 0, total = 0, hitsWalkLevel = 0;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int ix = 0; ix < gridX; ix++)
            {
                for (int iz = 0; iz < gridZ; iz++)
                {
                    float x = Mathf.Lerp(cityBounds.min.x, cityBounds.max.x, (ix + 0.5f) / gridX);
                    float z = Mathf.Lerp(cityBounds.min.z, cityBounds.max.z, (iz + 0.5f) / gridZ);
                    Vector3 from = new Vector3(x, cityBounds.max.y + 60f, z);
                    RaycastHit hit;
                    total++;
                    if (Physics.Raycast(from, Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        hits++;
                        minY = Mathf.Min(minY, hit.point.y);
                        maxY = Mathf.Max(maxY, hit.point.y);
                        if (hit.collider.gameObject.name == "WALK") hitsWalkLevel++;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Colliders] VERIFY: лучей " + total + ", попаданий " + hits +
                          " (" + (100f * hits / Mathf.Max(1, total)).ToString("F0") + "%)");
            sb.AppendLine("    из них в класс WALK (низ города): " + hitsWalkLevel);
            if (hits > 0)
                sb.AppendLine("    уровни попаданий: y " + minY.ToString("F1") + " … " + maxY.ToString("F1"));
            int colliders = 0;
            var byCls = new Dictionary<string, int>();
            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    colliders++;
                    string cls = c.transform.GetComponentInParent<Transform>() != null ? c.gameObject.name : c.gameObject.name;
                    ByClassInc(byCls, c.gameObject.name);
                }
            }
            sb.AppendLine("    коллайдеров в узле: " + colliders);
            foreach (var kv in byCls) sb.AppendLine("      " + kv.Key + ": " + kv.Value);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- CSV

        private static List<Row> LoadCsv(BuildReport rep)
        {
            var list = new List<Row>();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(projectRoot, CsvRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                rep.Warnings.Add("CSV не найден: " + CsvRelativePath);
                return list;
            }
            string[] lines = File.ReadAllLines(full, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] p = line.Split(',');
                if (p.Length < 10) continue;
                for (int k = 0; k < p.Length; k++) p[k] = p[k].Trim().Trim('"');
                var r = new Row
                {
                    Chunk = p[0].Trim('\ufeff'),
                    Cls = p[1].ToUpperInvariant(),
                    Center = new Vector3(F(p[2]), F(p[3]), F(p[4])),
                    Size = new Vector3(F(p[5]), F(p[6]), F(p[7])),
                    Yaw = F(p[8]),
                    Pitch = F(p[9]),
                    Name = p.Length > 10 ? p[10] : ""
                };
                list.Add(r);
            }
            return list;
        }

        private static float F(string s)
        {
            float v;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static string Describe(BuildReport rep)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Colliders] строк в CSV: " + rep.Rows + ", групп: " + rep.Groups);
            if (rep.ByClass.Count > 0)
            {
                sb.Append("    в CSV по классам:");
                foreach (var kv in rep.ByClass) sb.Append(" " + kv.Key + "=" + kv.Value);
                sb.AppendLine();
            }
            if (rep.CollidersByClass.Count > 0)
            {
                int sum = 0;
                sb.Append("    создано коллайдеров:");
                foreach (var kv in rep.CollidersByClass) { sb.Append(" " + kv.Key + "=" + kv.Value); sum += kv.Value; }
                sb.AppendLine("  (всего " + sum + ")");
            }
            foreach (string w in rep.Warnings) sb.AppendLine("    ⚠ " + w);
            return sb.ToString();
        }
    }
}
