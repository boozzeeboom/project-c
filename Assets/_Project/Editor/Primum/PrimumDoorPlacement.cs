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
    /// PRIMUM: массовая расстановка префабов одиночных дверей в проёмы города.
    ///
    /// Источник геометрии — НЕ реестр CSV, а сами выключенные группы <c>PRIMUM/&lt;чанк&gt;/DOORS/&lt;дверь&gt;</c>
    /// в сцене WorldScene_0_0 (они — ground truth: уже с масштабом k=1.07, в мировых координатах).
    /// Реестр <c>docs/.../registries/doors_registry_*.csv</c> остаётся документацией/провенансом.
    ///
    /// Что делает Apply:
    ///   1) удаляет прежнюю расстановку <c>WorldRoot_0_0/PRIMUM_DOORS</c> (идемпотентно) и легаси-узел DOORS_PREFAB_TEST;
    ///   2) для каждой выключенной исходной двери измеряет мировой AABB + считает острова меша;
    ///   3) отбирает ОДНОСТВОРЧАТЫЕ проёмы (см. фильтры ниже) и ставит в них префаб
    ///      PLANK_A / PANEL_B / ARCH_C с масштабом под проём;
    ///   4) пишет отчёт <c>docs/world/primum_city/03_DOORS/placement_manifest.csv</c> (каждая дверь: годна/почему нет).
    ///
    /// ВАЖНО: экземпляры НЕ помечаются static — статика ломается при ребейзе мира (F8).
    /// Исходные группы DOORS/ остаются выключенными как заглушка до полной замены.
    /// </summary>
    public static class PrimumDoorPlacement
    {
        // ------------------------------------------------------------------ константы

        public const string CityRootName = "PRIMUM";
        public const string WorldRootFallback = "WorldRoot_0_0";
        public const string DoorsNodeName = "DOORS";
        public const string PlaceRootName = "PRIMUM_DOORS";
        public const string LegacyTestNodeName = "DOORS_PREFAB_TEST";

        const string PrefabDir = "Assets/_Project/Prefabs/City/Doors";
        const string ReportRelativePath = "docs/world/primum_city/03_DOORS/placement_manifest.csv";

        /// <summary>Габарит (общий, с рамой) и путь префаба. Масштаб считается от ОБЩЕГО габарита.</summary>
        private class Variant
        {
            public string Id;
            public string Path;
            public float W;   // общая ширина
            public float H;   // общая высота
            public float D;   // общая глубина
        }

        private static readonly Variant[] Variants =
        {
            new Variant { Id = "PLANK_A", Path = PrefabDir + "/PM_DOOR_SINGLE_PLANK_A.prefab", W = 0.96f, H = 2.10f, D = 0.22f },
            new Variant { Id = "PANEL_B", Path = PrefabDir + "/PM_DOOR_SINGLE_PANEL_B.prefab", W = 1.06f, H = 2.15f, D = 0.22f },
            new Variant { Id = "ARCH_C",  Path = PrefabDir + "/PM_DOOR_SINGLE_ARCH_C.prefab",  W = 1.20f, H = 2.36f, D = 0.22f },
        };

        // --- фильтры отбора (одиночные створки)
        public const float MinWidth = 0.60f;   // уже — нужен отдельный узкий тип (0.56 м не жмём в 0.96)
        public const float MaxWidth = 1.38f;   // ARCH_C (1.20) тянется максимум на +15 %; шире — двустворчатые/ворота
        public const float MinHeight = 1.80f;
        public const float MaxHeight = 2.90f;
        public const float MaxThicknessRatio = 0.45f; // T / W: толще — это не дверь, а блок стены
        public const float LeafMinHeightRatio = 0.80f; // остров «полотно»: высота >= 0.80 H
        public const float LeafMinWidthRatio = 0.30f;  // ...ширина >= 0.30 W (отсекает косяки/фурнитуру)
        public const float LeafSingleWidthRatio = 0.55f; // одна створка занимает >= 0.55 W
        public const float LeafMinThicknessRatio = 0.30f; // ...и тонкий: T <= 0.30 W

        /// <summary>Имена-исключения: мебель, дверцы шкафов, наличники без полотна.</summary>
        private static readonly string[] SkipNameTokens =
        {
            "FURN", "DOORCASE", "SCHRANK", "WARDROBE", "VITRINE"
        };

        // ------------------------------------------------------------------ модель отчёта

        public class DoorRecord
        {
            public string Chunk;
            public string Name;
            public GameObject Source;
            public Vector3 Pivot;      // низ по центру AABB, мировые координаты
            public float RotY;         // 0 / 90 (определяется осью толщины)
            public float W, H, T;
            public int LeafCount;
            public float LeafRatio;
            public int Verts;
            public string Skip;        // null = годна к расстановке
            public string VariantId;
            public Vector3 Scale;
        }

        /// <summary>Состояние: что было на входе (для сводки).</summary>
        public class AnalysisResult
        {
            public List<DoorRecord> Records = new List<DoorRecord>();
            public int ChunksFound;
            public int ChunksMissing;
            public readonly Dictionary<string, int> SkipReasons = new Dictionary<string, int>();
            public readonly Dictionary<string, int> VariantCount = new Dictionary<string, int>();
        }

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/World/PRIMUM/Двери: аудит проёмов (CSV)", false, 300)]
        public static void MenuAudit()
        {
            AnalysisResult res = Analyze();
            WriteReport(res, true);
            LogSummary(res, "АУДИТ");
        }

        [MenuItem("ProjectC/World/PRIMUM/Двери: расставить одиночные (префабы)", false, 301)]
        public static void MenuApply()
        {
            ApplyMassPlacement(true);
        }

        [MenuItem("ProjectC/World/PRIMUM/Двери: очистить расстановку", false, 302)]
        public static void MenuClear()
        {
            int removed = ClearPlacement(true);
            Debug.Log("[PRIMUM/Doors] Очистка: удалено экземпляров " + removed + ".");
        }

        [MenuItem("ProjectC/World/PRIMUM/Двери: проверить посадку (verify)", false, 303)]
        public static void MenuVerify()
        {
            VerifyPlacement();
        }

        // ------------------------------------------------------------------ публичное API

        /// <summary>Только чтение: собрать и классифицировать все исходные двери.</summary>
        public static AnalysisResult Analyze()
        {
            var res = new AnalysisResult();
            GameObject city = GameObject.Find(CityRootName);
            if (city == null)
            {
                Debug.LogError("[PRIMUM/Doors] Не найден корень '" + CityRootName + "' в активной сцене.");
                return res;
            }

            Transform cityT = city.transform;
            for (int i = 0; i < cityT.childCount; i++)
            {
                Transform chunk = cityT.GetChild(i);
                Transform doors = chunk.Find(DoorsNodeName);
                if (doors == null)
                {
                    res.ChunksMissing++;
                    continue;
                }
                res.ChunksFound++;
                for (int j = 0; j < doors.childCount; j++)
                {
                    Transform d = doors.GetChild(j);
                    DoorRecord rec = AnalyzeDoor(chunk.name, d.gameObject);
                    if (rec == null) continue;
                    res.Records.Add(rec);
                    if (rec.Skip != null)
                    {
                        int c;
                        res.SkipReasons.TryGetValue(rec.Skip, out c);
                        res.SkipReasons[rec.Skip] = c + 1;
                    }
                    else
                    {
                        int c;
                        res.VariantCount.TryGetValue(rec.VariantId, out c);
                        res.VariantCount[rec.VariantId] = c + 1;
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Идемпотентная расстановка: снести старую, поставить заново по текущим правилам.
        /// Возвращает число поставленных префабов.
        /// </summary>
        public static int ApplyMassPlacement(bool saveScene, string chunkFilter = null)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject city = GameObject.Find(CityRootName);
            if (city == null)
            {
                Debug.LogError("[PRIMUM/Doors] Не найден '" + CityRootName + "'.");
                return 0;
            }

            AnalysisResult res = Analyze();
            List<DoorRecord> ready = res.Records.FindAll(r => r.Skip == null);
            if (!string.IsNullOrEmpty(chunkFilter))
            {
                string token = chunkFilter.ToUpperInvariant();
                ready = ready.FindAll(r => r.Chunk.ToUpperInvariant().Contains(token));
            }
            int skippedByFilter = res.Records.FindAll(r => r.Skip == null).Count - ready.Count;
            if (skippedByFilter > 0)
                Debug.Log("[PRIMUM/Doors] Фильтр чанка '" + chunkFilter + "': исключено " + skippedByFilter + " годных дверей.");

            Transform worldRoot = FindWorldRoot(city.transform);
            if (worldRoot == null)
            {
                Debug.LogError("[PRIMUM/Doors] Не найден world root (родитель PRIMUM).");
                return 0;
            }

            bool partial = !string.IsNullOrEmpty(chunkFilter);
            if (!partial)
            {
                // полная пересборка: снести всю прежнюю расстановку + легаси-узел теста
                ClearPlacement(false);
                RemoveLegacyTestNode(city.transform);
            }

            // корень расстановки: переиспользуем, если есть (частичная пересборка)
            Transform placeRootT = worldRoot.Find(PlaceRootName);
            if (placeRootT == null)
            {
                var created = new GameObject(PlaceRootName);
                placeRootT = created.transform;
                placeRootT.SetParent(worldRoot, false);
                placeRootT.localPosition = Vector3.zero;
                placeRootT.localRotation = Quaternion.identity;
                placeRootT.localScale = Vector3.one;
            }
            GameObject placeRoot = placeRootT.gameObject;
            placeRoot.layer = city.layer;
            GameObjectUtility.SetStaticEditorFlags(placeRoot, (StaticEditorFlags)0);

            // частичная пересборка: снять только те чанки, что попали в фильтр
            if (partial)
            {
                var touched = new HashSet<string>();
                foreach (DoorRecord r in ready) touched.Add(r.Chunk);
                foreach (string cn in touched)
                {
                    Transform old = placeRootT.Find(cn);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
            }

            var byChunk = new Dictionary<string, Transform>();
            var prefabs = new Dictionary<string, GameObject>();
            foreach (Variant v in Variants)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(v.Path);
                if (pf == null) Debug.LogError("[PRIMUM/Doors] Не найден префаб: " + v.Path);
                prefabs[v.Id] = pf;
            }

            int placed = 0;
            try
            {
                for (int i = 0; i < ready.Count; i++)
                {
                    DoorRecord r = ready[i];
                    if ((i & 31) == 0)
                    {
                        EditorUtility.DisplayProgressBar("PRIMUM: расстановка дверей",
                            r.Chunk + " — " + (i + 1) + " / " + ready.Count, (float)i / ready.Count);
                    }

                    GameObject pf = prefabs[r.VariantId];
                    if (pf == null) continue;

                    Transform parent;
                    if (!byChunk.TryGetValue(r.Chunk, out parent))
                    {
                        var chunkRoot = new GameObject(r.Chunk);
                        chunkRoot.transform.SetParent(placeRoot.transform, false);
                        chunkRoot.layer = city.layer;
                        GameObjectUtility.SetStaticEditorFlags(chunkRoot, (StaticEditorFlags)0);
                        parent = chunkRoot.transform;
                        byChunk[r.Chunk] = parent;
                    }

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
                    if (inst == null) continue;
                    inst.name = r.Name;
                    inst.layer = parent.gameObject.layer;

                    // Мировая поза: пивот префаба = низ по центру, лицевая сторона +Z.
                    inst.transform.SetPositionAndRotation(r.Pivot, Quaternion.Euler(0f, r.RotY, 0f));
                    inst.transform.localScale = r.Scale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(inst);

                    // КРИТИЧНО: не static (иначе объект отрывается при ребейзе мира, F8).
                    GameObjectUtility.SetStaticEditorFlags(inst, (StaticEditorFlags)0);
                    foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);

                    placed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            WriteReport(res, true);
            LogSummary(res, "РАССТАНОВКА");

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[PRIMUM/Doors] Сцена сохранена: " + scene.path);
            }
            Debug.Log("[PRIMUM/Doors] Поставлено префабов: " + placed + " (чанков: " + byChunk.Count + ").");
            return placed;
        }

        // ------------------------------------------------------------------ проверка посадки

        /// <summary>
        /// Сверяет мировой AABB каждого поставленного экземпляра с AABB исходной двери:
        /// смещение центра по XZ, смещение низа по Y, расхождение ширины/высоты.
        /// Возвращает человекочитаемую сводку (и пишет её в консоль).
        /// </summary>
        public static string VerifyPlacement()
        {
            GameObject city = GameObject.Find(CityRootName);
            Transform worldRoot = city != null ? FindWorldRoot(city.transform) : null;
            Transform placeRoot = worldRoot != null ? worldRoot.Find(PlaceRootName) : null;
            if (placeRoot == null)
                return "[PRIMUM/Doors] Verify: узел " + PlaceRootName + " не найден.";

            AnalysisResult res = Analyze();
            int checkedCount = 0, missing = 0, bad = 0;
            float maxCenter = 0f, maxBottom = 0f, maxW = 0f, maxH = 0f;
            var worst = new StringBuilder();

            foreach (DoorRecord r in res.Records)
            {
                if (r.Skip != null) continue;
                Transform chunkNode = placeRoot.Find(r.Chunk);
                Transform inst = chunkNode != null ? chunkNode.Find(r.Name) : null;
                if (inst == null) { missing++; continue; }

                Bounds src = r.Source.GetComponent<Renderer>().bounds;
                Renderer[] rs = inst.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { missing++; continue; }
                Bounds got = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) got.Encapsulate(rs[i].bounds);

                float dCenter = new Vector2(got.center.x - src.center.x, got.center.z - src.center.z).magnitude;
                float dBottom = got.min.y - src.min.y;
                bool thickX = r.RotY > 45f;
                float srcW = thickX ? src.size.z : src.size.x;
                float gotW = thickX ? got.size.z : got.size.x;
                float dW = Mathf.Abs(gotW - srcW) / Mathf.Max(0.01f, srcW);
                float dH = Mathf.Abs(got.size.y - src.size.y) / Mathf.Max(0.01f, src.size.y);

                checkedCount++;
                if (dCenter > maxCenter) maxCenter = dCenter;
                if (Mathf.Abs(dBottom) > maxBottom) maxBottom = Mathf.Abs(dBottom);
                if (dW > maxW) maxW = dW;
                if (dH > maxH) maxH = dH;

                bool isBad = dCenter > 0.05f || Mathf.Abs(dBottom) > 0.05f || dW > 0.10f || dH > 0.10f;
                if (isBad)
                {
                    bad++;
                    if (worst.Length < 3000)
                        worst.AppendLine("    " + r.Name + " dCenter=" + dCenter.ToString("F3") +
                                         " dBottom=" + dBottom.ToString("F3") +
                                         " dW=" + dW.ToString("P1") + " dH=" + dH.ToString("P1"));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Doors] VERIFY: проверено " + checkedCount + ", нет экземпляра " + missing +
                          ", вне допуска " + bad);
            sb.AppendLine("    макс отклонения: центр XZ " + maxCenter.ToString("F4") + " м, низ Y " +
                          maxBottom.ToString("F4") + " м, ширина " + maxW.ToString("P2") + ", высота " + maxH.ToString("P2"));
            if (worst.Length > 0) sb.Append(worst.ToString());
            string text = sb.ToString();
            Debug.Log(text);
            return text;
        }

        /// <summary>Удалить узел расстановки. Возвращает число удалённых экземпляров.</summary>
        public static int ClearPlacement(bool markDirty)
        {
            GameObject city = GameObject.Find(CityRootName);
            Transform worldRoot = city != null ? FindWorldRoot(city.transform) : null;
            GameObject root = null;
            if (worldRoot != null) root = worldRoot.Find(PlaceRootName)?.gameObject;
            if (root == null) root = GameObject.Find(PlaceRootName);
            if (root == null) return 0;

            int count = root.GetComponentsInChildren<Transform>(true).Length - 1;
            UnityEngine.Object.DestroyImmediate(root);
            if (markDirty) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return Mathf.Max(0, count);
        }

        // ------------------------------------------------------------------ анализ одной двери

        /// <summary>Прочитать геометрию двери в мировых размерах + посчитать створки по островам меша.</summary>
        private static DoorRecord AnalyzeDoor(string chunkName, GameObject go)
        {
            var rec = new DoorRecord { Chunk = chunkName, Name = go.name, Source = go };

            Renderer rd = go.GetComponent<Renderer>();
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (rd == null || mf == null || mf.sharedMesh == null)
            {
                rec.Skip = "нет меша";
                return rec;
            }

            Bounds wb = rd.bounds;               // мировой AABB (осевой, повороты кратны 90°)
            Vector3 size = wb.size;
            float h = size.y;
            bool thickAlongX = size.x <= size.z;
            float w = thickAlongX ? size.z : size.x;
            float t = thickAlongX ? size.x : size.z;

            rec.W = w;
            rec.H = h;
            rec.T = t;
            rec.Pivot = new Vector3(wb.center.x, wb.min.y, wb.center.z);
            rec.RotY = thickAlongX ? 90f : 0f;
            rec.Verts = mf.sharedMesh.vertexCount;

            // --- острова меша (в локальных осях меша: X/Y — горизонт, Z — высота)
            Bounds lb = mf.sharedMesh.bounds;
            float localW = Mathf.Max(lb.size.x, lb.size.y);
            float localH = lb.size.z;
            CountLeaves(mf.sharedMesh, localW, localH, out int leafs, out float leafRatio);
            rec.LeafCount = leafs;
            rec.LeafRatio = leafRatio;

            // --- фильтры
            string upper = go.name.ToUpperInvariant();
            if (upper.Contains("GATE")) { rec.Skip = "ворота (GATE)"; return rec; }
            foreach (string tok in SkipNameTokens)
            {
                if (upper.Contains(tok)) { rec.Skip = "мебель/наличник (" + tok + ")"; return rec; }
            }
            if (w > MaxWidth) { rec.Skip = "широкий проём >" + MaxWidth.ToString("F2", CultureInfo.InvariantCulture) + " (двустворчатые позже)"; return rec; }
            if (w < MinWidth) { rec.Skip = "узкий проём <" + MinWidth.ToString("F2", CultureInfo.InvariantCulture); return rec; }
            if (h > MaxHeight) { rec.Skip = "высокий >" + MaxHeight.ToString("F2", CultureInfo.InvariantCulture); return rec; }
            if (h < MinHeight) { rec.Skip = "низкий <" + MinHeight.ToString("F2", CultureInfo.InvariantCulture); return rec; }
            if (w <= 0f || t / w > MaxThicknessRatio) { rec.Skip = "не дверь (толщина/ширина)"; return rec; }
            if (leafs == 0 || leafRatio < LeafSingleWidthRatio) { rec.Skip = "не одностворчатая (створок " + leafs + ", доля " + leafRatio.ToString("F2", CultureInfo.InvariantCulture) + ")"; return rec; }

            // --- выбор варианта: минимум искажений по ширине+высоте
            Variant best = null;
            float bestScore = float.MaxValue;
            foreach (Variant v in Variants)
            {
                float score = Mathf.Abs(w / v.W - 1f) + Mathf.Abs(h / v.H - 1f);
                if (score < bestScore) { bestScore = score; best = v; }
            }
            rec.VariantId = best.Id;
            // Глубину подгоняем под фактическую толщину исходной двери (рама в откосе), с ограничением.
            float sz = Mathf.Clamp(t / best.D, 0.6f, 1.6f);
            rec.Scale = new Vector3(w / best.W, h / best.H, sz);
            return rec;
        }

        /// <summary>
        /// Считает «полотна» по связным островам меша.
        /// Остров = полотно, если: высота >= LeafMinHeightRatio * H, ширина >= LeafMinWidthRatio * W,
        /// толщина <= LeafMinThicknessRatio * W. Возвращает число таких островов и долю ширины
        /// самого широкого из них (1.0 = полотно на всю ширину проёма = одностворчатая).
        /// </summary>
        private static void CountLeaves(Mesh mesh, float localW, float localH, out int leafCount, out float widestRatio)
        {
            leafCount = 0;
            widestRatio = 0f;
            if (mesh == null || localW <= 0f || localH <= 0f) return;

            Vector3[] verts = mesh.vertices;
            int n = verts.Length;
            if (n == 0) return;

            // сварка по квантованию 1e-6 в локальных единицах (меши дверей мелкомасштабные)
            var index = new Dictionary<long, int>(n);
            int[] rep = new int[n];
            for (int i = 0; i < n; i++)
            {
                long kx = (long)Mathf.Round(verts[i].x * 1000000f);
                long ky = (long)Mathf.Round(verts[i].y * 1000000f);
                long kz = (long)Mathf.Round(verts[i].z * 1000000f);
                long key = ((kx + 100000000L) * 400000003L + (ky + 100000000L)) * 400000003L + (kz + 100000000L);
                int id;
                if (!index.TryGetValue(key, out id))
                {
                    id = index.Count;
                    index[key] = id;
                }
                rep[i] = id;
            }

            int groups = index.Count;
            int[] parent = new int[groups];
            for (int i = 0; i < groups; i++) parent[i] = i;

            int[] tris = mesh.triangles;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = UnionFind(parent, rep[tris[i]]);
                int b = UnionFind(parent, rep[tris[i + 1]]);
                int c = UnionFind(parent, rep[tris[i + 2]]);
                if (a != b) parent[a] = b;
                int rb = UnionFind(parent, b);
                int rc = UnionFind(parent, c);
                if (rb != rc) parent[rb] = rc;
            }

            var boxes = new Dictionary<int, Bounds>(groups);
            for (int i = 0; i < n; i++)
            {
                int root = UnionFind(parent, rep[i]);
                Bounds bb;
                if (!boxes.TryGetValue(root, out bb))
                {
                    bb = new Bounds(verts[i], Vector3.zero);
                    boxes[root] = bb;
                }
                else
                {
                    bb.Encapsulate(verts[i]);
                    boxes[root] = bb;
                }
            }

            foreach (Bounds bb in boxes.Values)
            {
                Vector3 s = bb.size;
                float iw = Mathf.Max(s.x, s.y);
                float it = Mathf.Min(s.x, s.y);
                float ih = s.z;
                if (ih < localH * LeafMinHeightRatio) continue;
                if (iw < localW * LeafMinWidthRatio) continue;
                if (it > localW * LeafMinThicknessRatio) continue;
                leafCount++;
                float ratio = iw / localW;
                if (ratio > widestRatio) widestRatio = ratio;
            }
        }

        private static int UnionFind(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        // ------------------------------------------------------------------ служебное

        private static Transform FindWorldRoot(Transform city)
        {
            if (city.parent != null) return city.parent;
            GameObject wr = GameObject.Find(WorldRootFallback);
            return wr != null ? wr.transform : null;
        }

        /// <summary>Удаляет легаси-узел тестовой расстановки (он внутри инстанса префаба города).</summary>
        private static void RemoveLegacyTestNode(Transform city)
        {
            for (int i = 0; i < city.childCount; i++)
            {
                Transform legacy = city.GetChild(i).Find(LegacyTestNodeName);
                if (legacy != null)
                {
                    Debug.Log("[PRIMUM/Doors] Удалён легаси-узел " + legacy.name + " (" +
                              legacy.childCount + " дверей) из " + city.GetChild(i).name);
                    UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                }
            }
        }

        private static void LogSummary(AnalysisResult res, string stage)
        {
            int ready = res.Records.FindAll(r => r.Skip == null).Count;
            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Doors] " + stage + ": всего исходных групп " + res.Records.Count +
                          ", годно " + ready + ", чанков " + res.ChunksFound +
                          (res.ChunksMissing > 0 ? " (без узла DOORS: " + res.ChunksMissing + ")" : ""));
            foreach (var kv in res.VariantCount)
                sb.AppendLine("    вариант " + kv.Key + ": " + kv.Value);
            foreach (var kv in res.SkipReasons)
                sb.AppendLine("    пропуск — " + kv.Key + ": " + kv.Value);
            Debug.Log(sb.ToString());
        }

        private static void WriteReport(AnalysisResult res, bool verbose)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(projectRoot, ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("chunk,door,action,variant,x,y,z,rot_y,w,h,t,scale_x,scale_y,scale_z,leafs,leaf_ratio,verts,skip");
                foreach (DoorRecord r in res.Records)
                {
                    bool ok = r.Skip == null;
                    sb.Append(r.Chunk).Append(',')
                      .Append(Csv(r.Name)).Append(',')
                      .Append(ok ? "place" : "skip").Append(',')
                      .Append(ok ? r.VariantId : "").Append(',')
                      .Append(F(r.Pivot.x)).Append(',')
                      .Append(F(r.Pivot.y)).Append(',')
                      .Append(F(r.Pivot.z)).Append(',')
                      .Append(F(r.RotY)).Append(',')
                      .Append(F(r.W)).Append(',')
                      .Append(F(r.H)).Append(',')
                      .Append(F(r.T)).Append(',')
                      .Append(ok ? F(r.Scale.x) : "").Append(',')
                      .Append(ok ? F(r.Scale.y) : "").Append(',')
                      .Append(ok ? F(r.Scale.z) : "").Append(',')
                      .Append(r.LeafCount).Append(',')
                      .Append(F(r.LeafRatio)).Append(',')
                      .Append(r.Verts).Append(',')
                      .Append(Csv(r.Skip)).AppendLine();
                }
                File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
                if (verbose) Debug.Log("[PRIMUM/Doors] Отчёт: " + ReportRelativePath + " (" + res.Records.Count + " строк)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PRIMUM/Doors] Не удалось записать отчёт: " + e.Message);
            }
        }

        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
