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
        private const string StairsObjRelativePath = "docs/world/primum_city/04_COLLIDERS/stairs.obj";
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
            public int StairHolesCarved;      // боксов, из которых вырезан проём лестницы
            public int StairHolesPieces;      // сколько частей получилось вместо них
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

            // габариты внутренних лестниц — из них вырезаем проёмы в боксах стен/полов/крыш
            Dictionary<string, List<StairBox>> stairBoxes = LoadStairBoxes(rep);

            foreach (Row src in rows)
            {
                if (!wanted.Contains(src.Cls)) continue;

                foreach (Row r in CarveAgainstStairs(src, stairBoxes, rep))
                {
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
            }

            rep.Groups = groups.Count;
            BuildObjColliders(PlatformsObjRelativePath, "PRIMUM_BASE_S0_CITY_P1_00H", "PLATFORM", "PM_PLATFORM", rep);
            BuildStairsColliders(rep);
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

        // ---------------------------------------------------------------- проёмы внутренних лестниц

        /// <summary>Осевой габарит одной внутренней лестницы (в пространстве PRIMUM).</summary>
        private class StairBox
        {
            public float MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
        }

        /// <summary>Расширение проёма вокруг габарита лестницы (м).</summary>
        private const float StairHoleInflate = 0.08f;

        /// <summary>Минимальное вертикальное перекрытие бокса с лестницей, чтобы считать его «на пути» (м).</summary>
        private const float StairYOverlapMin = 0.02f;

        /// <summary>
        /// Насколько выше верха марша ещё может начинаться перекрытие/крыша, чтобы его надо было прорезать:
        /// верхняя ступень упирается в плиту (выход на этаж/крышу), а не просто потолок над лестницей.
        /// </summary>
        private const float StairTopTolerance = 0.60f;

        /// <summary>
        /// Габариты лестниц по чанкам из stairs.obj. Стены/полы/крыши строятся боксами по bbox объекта,
        /// поэтому слэб с проёмом лестницы превращается в сплошной бокс и перекрывает марш (игрок не может
        /// подняться). Габариты нужны, чтобы вырезать эти проёмы из боксов.
        /// </summary>
        private static Dictionary<string, List<StairBox>> LoadStairBoxes(BuildReport rep)
        {
            var result = new Dictionary<string, List<StairBox>>();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(projectRoot, StairsObjRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ObjData data = ParseObj(full, rep);
            if (data.Objects.Count == 0) return result;

            foreach (string name in data.Objects)
            {
                List<int> tris = data.Faces[name];
                if (tris.Count < 3) continue;

                string rest = name.StartsWith("STAIR_") ? name.Substring(6) : name;
                string chunk = null;
                foreach (var kv in ChunkCodes)
                    if (rest.StartsWith(kv.Key + "_")) { chunk = kv.Value; break; }
                if (chunk == null) continue;

                var sb = new StairBox
                {
                    MinX = float.MaxValue, MaxX = float.MinValue,
                    MinY = float.MaxValue, MaxY = float.MinValue,
                    MinZ = float.MaxValue, MaxZ = float.MinValue
                };
                foreach (int gi in tris)
                {
                    if (gi < 0 || gi >= data.Verts.Count) continue;
                    Vector3 v = data.Verts[gi];
                    sb.MinX = Mathf.Min(sb.MinX, v.x); sb.MaxX = Mathf.Max(sb.MaxX, v.x);
                    sb.MinY = Mathf.Min(sb.MinY, v.y); sb.MaxY = Mathf.Max(sb.MaxY, v.y);
                    sb.MinZ = Mathf.Min(sb.MinZ, v.z); sb.MaxZ = Mathf.Max(sb.MaxZ, v.z);
                }
                if (sb.MinX > sb.MaxX) continue;

                List<StairBox> lst;
                if (!result.TryGetValue(chunk, out lst)) { lst = new List<StairBox>(); result[chunk] = lst; }
                lst.Add(sb);
            }
            return result;
        }

        /// <summary>
        /// Бокс, накрывающий марши лестниц, заменяется на части вокруг их проёмов (габарит лестницы).
        /// Один длинный бокс (слэб/крыша/стена на весь дом) может перекрывать сразу несколько лестниц —
        /// поэтому прогоняем вырезание по ВСЕМ лестницам чанка, а не по первой попавшейся.
        /// </summary>
        private static List<Row> CarveAgainstStairs(Row r, Dictionary<string, List<StairBox>> stairs, BuildReport rep)
        {
            var single = new List<Row> { r };
            if (r.Cls != "WALL" && r.Cls != "FLOOR" && r.Cls != "ROOF") return single;
            if (Mathf.Abs(r.Pitch) > 0.5f) return single;

            bool quarter = Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 90f)) < 0.5f || Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 270f)) < 0.5f;
            bool axis = Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 0f)) < 0.5f || Mathf.Abs(Mathf.DeltaAngle(r.Yaw, 180f)) < 0.5f;
            if (!quarter && !axis) return single;                      // повёрнутые боксы не режем

            List<StairBox> list;
            if (!stairs.TryGetValue(r.Chunk, out list) || list.Count == 0) return single;

            Vector3 size = quarter ? new Vector3(r.Size.z, r.Size.y, r.Size.x) : r.Size;
            var first = new Row
            {
                Chunk = r.Chunk, Cls = r.Cls, Name = r.Name,
                Center = r.Center, Size = size, Yaw = 0f, Pitch = 0f
            };

            List<Row> work = new List<Row> { first };
            bool carved = false;
            foreach (StairBox s in list)
            {
                if (work.Count == 0) break;
                var next = new List<Row>();
                foreach (Row w in work)
                {
                    if (!SplitByStair(w, s, next, ref carved)) next.Add(w);
                }
                work = next;
            }

            if (!carved) return single;
            rep.StairHolesCarved++;
            rep.StairHolesPieces += Mathf.Max(0, work.Count - 1);
            return work.Count == 0 ? new List<Row>() : work;
        }

        /// <summary>Делит бокс на 2–4 части вокруг проёма одной лестницы. false — бокс не задет.</summary>
        private static bool SplitByStair(Row w, StairBox s, List<Row> dst, ref bool carved)
        {
            float bx0 = w.Center.x - w.Size.x * 0.5f, bx1 = w.Center.x + w.Size.x * 0.5f;
            float by0 = w.Center.y - w.Size.y * 0.5f, by1 = w.Center.y + w.Size.y * 0.5f;
            float bz0 = w.Center.z - w.Size.z * 0.5f, bz1 = w.Center.z + w.Size.z * 0.5f;

            if (bx1 <= s.MinX || bx0 >= s.MaxX) return false;
            if (bz1 <= s.MinZ || bz0 >= s.MaxZ) return false;
            if (by1 <= s.MinY + StairYOverlapMin) return false;      // бокс под лестницей (пол/земля)
            if (by0 >= s.MaxY + StairTopTolerance) return false;      // бокс заметно выше верха марша

            float hx0 = Mathf.Max(bx0, s.MinX - StairHoleInflate);
            float hx1 = Mathf.Min(bx1, s.MaxX + StairHoleInflate);
            float hz0 = Mathf.Max(bz0, s.MinZ - StairHoleInflate);
            float hz1 = Mathf.Min(bz1, s.MaxZ + StairHoleInflate);
            if (hx1 - hx0 < 0.05f || hz1 - hz0 < 0.05f) return false;

            carved = true;
            AddCarvedPiece(dst, w, bx0, hx0, bz0, bz1, "L");   // слева от проёма
            AddCarvedPiece(dst, w, hx1, bx1, bz0, bz1, "R");   // справа
            AddCarvedPiece(dst, w, hx0, hx1, bz0, hz0, "D");   // перед проёмом
            AddCarvedPiece(dst, w, hx0, hx1, hz1, bz1, "U");   // за проёмом
            return true;
        }

        private static void AddCarvedPiece(List<Row> dst, Row src, float x0, float x1, float z0, float z1, string suffix)
        {
            float w = x1 - x0, d = z1 - z0;
            if (w < 0.03f || d < 0.03f) return;
            dst.Add(new Row
            {
                Chunk = src.Chunk,
                Cls = src.Cls,
                Name = src.Name + "#" + suffix,
                Center = new Vector3((x0 + x1) * 0.5f, src.Center.y, (z0 + z1) * 0.5f),
                Size = new Vector3(w, src.Size.y, d),
                Yaw = 0f,
                Pitch = 0f
            });
        }

        // ---------------------------------------------------------------- MeshCollider'ы из OBJ (платформы, лестницы)

        private class ObjData
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<string> Objects = new List<string>();
            public readonly Dictionary<string, List<int>> Faces = new Dictionary<string, List<int>>();
            public readonly Dictionary<string, int> VertStart = new Dictionary<string, int>();
        }

        private static ObjData ParseObj(string fullPath, BuildReport rep)
        {
            var data = new ObjData();
            if (!File.Exists(fullPath))
            {
                rep.Warnings.Add("OBJ не найден: " + fullPath);
                return data;
            }
            string current = null;
            foreach (string raw in File.ReadAllLines(fullPath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("o "))
                {
                    current = line.Substring(2).Trim();
                    data.Objects.Add(current);
                    data.Faces[current] = new List<int>();
                    data.VertStart[current] = data.Verts.Count;
                }
                else if (line.StartsWith("v "))
                {
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    data.Verts.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
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
                    var list = data.Faces[current];
                    if (idx.Count == 3) { list.Add(idx[0]); list.Add(idx[1]); list.Add(idx[2]); }
                    else if (idx.Count >= 4)
                        for (int i = 1; i + 1 < idx.Count; i++) { list.Add(idx[0]); list.Add(idx[i]); list.Add(idx[i + 1]); }
                }
            }
            return data;
        }

        /// <summary>Короткие коды чанков в именах объектов OBJ (PLATFORM_DECK0, STAIR_S9TRDN_RES_TN_01).</summary>
        private static readonly Dictionary<string, string> ChunkCodes = new Dictionary<string, string>
        {
            { "BASE",     "PRIMUM_BASE_S0_CITY_P1_00H" },
            { "ADMIN",    "PRIMUM_ADMIN_S0_ADM_P1_01H" },
            { "S2TRDP1",  "PRIMUM_HOUSES_S2_TRD_P1_28H" },
            { "S3TRDP2",  "PRIMUM_HOUSES_S3_TRD_P2_26H" },
            { "S3TRDESP1","PRIMUM_HOUSES_S3_TRD-ES_P1_37H" },
            { "S4TRDP3",  "PRIMUM_HOUSES_S4_TRD_P3_26H" },
            { "S9TRDN",   "PRIMUM_HOUSES_S9_TRD-N_P1_28H" },
            { "S9TRDS",   "PRIMUM_HOUSES_S9_TRD-S_P1_16H" },
        };

        /// <summary>Создать MeshCollider'ы из OBJ (платформы низа города, внутренние лестницы).</summary>
        private static void BuildObjColliders(string relativePath, string defaultChunk, string classLabel,
                                              string meshPrefix, BuildReport rep)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            ObjData data = ParseObj(full, rep);
            if (data.Objects.Count == 0) return;

            GameObject city = GameObject.Find(CityRootName);
            Transform worldRoot = city != null && city.transform.parent != null ? city.transform.parent : null;
            Transform root = worldRoot != null ? worldRoot.Find(RootName) : null;
            if (root == null) { rep.Warnings.Add("сначала создайте узел " + RootName); return; }

            string meshDirFull = Path.Combine(projectRoot, PlatformMeshDir);
            if (!Directory.Exists(meshDirFull)) Directory.CreateDirectory(meshDirFull);

            foreach (string name in data.Objects)
            {
                List<int> tris = data.Faces[name];
                if (tris.Count < 3) continue;
                int vstart = data.VertStart[name];
                int vend = data.Verts.Count;
                bool bad = false;
                foreach (int gi in tris) if (gi < vstart || gi >= vend) { bad = true; break; }
                if (bad)
                {
                    rep.Warnings.Add(relativePath + " битый: объект '" + name + "' ссылается на чужие вершины — пропущен");
                    Debug.LogError("[PRIMUM/Colliders] битый OBJ: " + name);
                    continue;
                }

                // чанк: по коду в имени (STAIR_S9TRDN_...), иначе defaultChunk
                string chunk = defaultChunk;
                if (classLabel != "PLATFORM")
                {
                    string rest = name.StartsWith(classLabel + "_") ? name.Substring(classLabel.Length + 1) : name;
                    foreach (var kv in ChunkCodes)
                        if (rest.StartsWith(kv.Key + "_")) { chunk = kv.Value; break; }
                }

                var map = new Dictionary<int, int>();
                var vlist = new List<Vector3>();
                var tlist = new List<int>();
                foreach (int gi in tris)
                {
                    int li;
                    if (!map.TryGetValue(gi, out li)) { li = vlist.Count; vlist.Add(data.Verts[gi]); map[gi] = li; }
                    tlist.Add(li);
                }

                var mesh = new Mesh { name = meshPrefix + "_" + name };
                mesh.SetVertices(vlist);
                mesh.SetTriangles(tlist, 0);
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, PlatformMeshDir + "/" + meshPrefix + "_" + name + ".asset");

                Transform chunkT = root.Find(chunk);
                if (chunkT == null)
                {
                    var chunkGo = new GameObject(chunk);
                    chunkGo.transform.SetParent(root, false);
                    chunkGo.layer = CityLayer;
                    GameObjectUtility.SetStaticEditorFlags(chunkGo, (StaticEditorFlags)0);
                    chunkT = chunkGo.transform;
                }
                var go = new GameObject(name);
                go.layer = CityLayer;
                go.transform.SetParent(chunkT, false);
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                                  | MeshColliderCookingOptions.EnableMeshCleaning
                                  | MeshColliderCookingOptions.WeldColocatedVertices
                                  | MeshColliderCookingOptions.UseFastMidphase;
                ByClassInc(rep.CollidersByClass, classLabel);
            }
        }

        /// <summary>
        /// Внутренние лестницы: точная геометрия из stairs.obj, ОДИН общий MeshCollider на чанк
        /// (110 отдельных объектов-лестниц давали 110 меш-ассетов и тормозили сборку).
        /// </summary>
        private static void BuildStairsColliders(BuildReport rep)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(projectRoot, StairsObjRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ObjData data = ParseObj(full, rep);
            if (data.Objects.Count == 0) return;

            GameObject city = GameObject.Find(CityRootName);
            Transform worldRoot = city != null && city.transform.parent != null ? city.transform.parent : null;
            Transform root = worldRoot != null ? worldRoot.Find(RootName) : null;
            if (root == null) { rep.Warnings.Add("сначала создайте узел " + RootName); return; }

            string meshDirFull = Path.Combine(projectRoot, PlatformMeshDir);
            if (!Directory.Exists(meshDirFull)) Directory.CreateDirectory(meshDirFull);

            var perChunkVerts = new Dictionary<string, List<Vector3>>();
            var perChunkTris = new Dictionary<string, List<int>>();

            foreach (string name in data.Objects)
            {
                List<int> tris = data.Faces[name];
                if (tris.Count < 3) continue;
                int vstart = data.VertStart[name];
                int vend = data.Verts.Count;
                bool bad = false;
                foreach (int gi in tris) if (gi < vstart || gi >= vend) { bad = true; break; }
                if (bad)
                {
                    rep.Warnings.Add("stairs.obj битый: '" + name + "' — пропущен");
                    continue;
                }

                string rest = name.StartsWith("STAIR_") ? name.Substring(6) : name;
                string chunk = null;
                foreach (var kv in ChunkCodes)
                    if (rest.StartsWith(kv.Key + "_")) { chunk = kv.Value; break; }
                if (chunk == null) chunk = "PRIMUM_HOUSES_S9_TRD-N_P1_28H";

                List<Vector3> vl; if (!perChunkVerts.TryGetValue(chunk, out vl)) { vl = new List<Vector3>(); perChunkVerts[chunk] = vl; perChunkTris[chunk] = new List<int>(); }
                List<int> tl = perChunkTris[chunk];
                var map = new Dictionary<int, int>();
                foreach (int gi in tris)
                {
                    int li;
                    if (!map.TryGetValue(gi, out li)) { li = vl.Count; vl.Add(data.Verts[gi]); map[gi] = li; }
                    tl.Add(li);
                }
            }

            foreach (var kv in perChunkVerts)
            {
                string chunk = kv.Key;
                var vlist = kv.Value;
                var tlist = perChunkTris[chunk];
                if (tlist.Count < 3) continue;

                var mesh = new Mesh { name = "PM_STAIRS_" + chunk };
                mesh.indexFormat = vlist.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(vlist);
                mesh.SetTriangles(tlist, 0);
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, PlatformMeshDir + "/PM_STAIRS_" + chunk + ".asset");

                Transform chunkT = root.Find(chunk);
                if (chunkT == null)
                {
                    var chunkGo = new GameObject(chunk);
                    chunkGo.transform.SetParent(root, false);
                    chunkGo.layer = CityLayer;
                    GameObjectUtility.SetStaticEditorFlags(chunkGo, (StaticEditorFlags)0);
                    chunkT = chunkGo.transform;
                }
                var go = new GameObject("STAIRS");
                go.layer = CityLayer;
                go.transform.SetParent(chunkT, false);
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                                  | MeshColliderCookingOptions.EnableMeshCleaning
                                  | MeshColliderCookingOptions.WeldColocatedVertices
                                  | MeshColliderCookingOptions.UseFastMidphase;
                ByClassInc(rep.CollidersByClass, "STAIR_MESH");
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
            if (rep.StairHolesCarved > 0)
                sb.AppendLine("    проёмов лестниц вырезано в боксах: " + rep.StairHolesCarved +
                              " (добавлено частей: " + rep.StairHolesPieces + ")");
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
