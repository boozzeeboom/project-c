using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectC.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM, этап Э3 «Свет»: аудит узлов <c>LAMPS</c>, emissive-материал светящихся частей и
    /// расстановка световых префабов <c>PM_LIGHT_*</c> под <c>WorldRoot_0_0/PRIMUM_LIGHTS</c>.
    ///
    /// Почему префаб — только источник света: корпус и светящиеся части светильников уже есть в
    /// геометрии чанков (M_EMIT_LAMP), заменять их самодельными примитивами = потерять вид квартала
    /// и получить двойную геометрию. Поэтому префаб несёт <c>Light</c> (и больше ничего), а светится
    /// исходный корпус после emissive-прохода. Позиция инстанса = центр светящейся части.
    ///
    /// Куда: <c>WorldRoot_0_0/PRIMUM_LIGHTS/&lt;чанк&gt;/&lt;кластер&gt;</c> — СОСЕД корня города, а не ребёнок
    /// префаба PRIMUM (всё, что добавляется внутрь префаб-инстанса, теряется при пересохранении
    /// префаба). Корень копирует контракт PRIMUM_DOORS: identity-трансформ под WorldRoot_0_0,
    /// поэтому ребейз мира везёт огни вместе с городом. static-флаги НЕ ставим (правило Floating Origin).
    ///
    /// Бюджет источников: у инстансов Light ВЫКЛЮЧЕН, включает ближайшие <see cref="PrimumLightPool"/>.
    /// ADMIN и дворцы (S1_GLD + RES_SE_901/902/911) не трогаем — у них свои светильники.
    ///
    /// Меню: ProjectC → World → PRIMUM → Свет: …
    /// </summary>
    public static class PrimumLampLighting
    {
        // ------------------------------------------------------------------ константы

        public const string CityRootName = "PRIMUM";
        public const string WorldRootFallback = "WorldRoot_0_0";
        public const string LampsNodeName = "LAMPS";
        public const string LightsRootName = "PRIMUM_LIGHTS";

        private const string PrefabDir = "Assets/_Project/Prefabs/City/Lights";
        private const string MatDir = "Assets/_Project/Materials/Primum/Lamps";
        private const string GlowMatName = "PM_LAMP_GLOW_WARM";

        private const string AuditRelativePath = "docs/world/primum_city/08_LIGHTING/lamps_audit.csv";
        private const string ManifestRelativePath = "docs/world/primum_city/08_LIGHTING/placement_manifest.csv";

        private const int CityLayer = 8; // PRIMUM
        private const float MaxClusterExtent = 6f;

        /// <summary>Чанки, которые Э3 не трогает: администрация и дворцовый набор.</summary>
        private static readonly string[] ExcludedChunks =
        {
            "PRIMUM_ADMIN_S0_ADM_P1_01H",
            "PRIMUM_HOUSES_S1_GLD_P1_25H"
        };

        /// <summary>Дома-дворцы внутри S7_SE_P1 — тоже не трогаем.</summary>
        private static readonly string[] ExcludedNamePrefixes = { "RES_SE_901", "RES_SE_902", "RES_SE_911" };

        /// <summary>Материалы города, которые сейчас стоят на светящихся частях, но БЕЗ emission.</summary>
        private static readonly string[] GlowSourceMaterials = { "M_EMIT_LAMP", "M_TQ_LAMP" };

        /// <summary>Части, центр которых считается точкой источника (по приоритету).</summary>
        private static readonly string[] HeadParts = { "GLOW", "BULB", "LAMP", "LANTERN", "HEAD", "GLASS", "SHADE", "BODY" };

        // ------------------------------------------------------------------ семейства

        public enum Family
        {
            None = 0,
            StreetPost,
            StreetWall,
            FacadeWall,
            FacadeSconce,
            InteriorPendant,
            InteriorSconce,
            InteriorTable
        }

        private struct LightSpec
        {
            public string Prefab;
            public string Title;
            public float Range;
            public float Intensity;
            public Color Color;
        }

        /// <summary>Бюджет одного источника (additional lights per-object limit = 4, доп. тени выкл.).</summary>
        private static LightSpec SpecOf(Family f)
        {
            switch (f)
            {
                case Family.StreetPost:
                    return new LightSpec { Prefab = "PM_LIGHT_STREET_POST", Title = "улица: фонарь на столбе", Range = 14f, Intensity = 3.0f, Color = new Color(1.000f, 0.780f, 0.500f) };
                case Family.StreetWall:
                    return new LightSpec { Prefab = "PM_LIGHT_STREET_WALL", Title = "улица: настенный фонарь", Range = 9f, Intensity = 2.2f, Color = new Color(1.000f, 0.760f, 0.470f) };
                case Family.FacadeWall:
                    return new LightSpec { Prefab = "PM_LIGHT_FACADE_WALL", Title = "фасад: пристенный", Range = 6f, Intensity = 1.6f, Color = new Color(1.000f, 0.740f, 0.460f) };
                case Family.FacadeSconce:
                    return new LightSpec { Prefab = "PM_LIGHT_FACADE_SCONCE", Title = "фасад: бра", Range = 5f, Intensity = 1.3f, Color = new Color(1.000f, 0.800f, 0.550f) };
                case Family.InteriorPendant:
                    return new LightSpec { Prefab = "PM_LIGHT_INTERIOR_PENDANT", Title = "интерьер: люстра/подвес", Range = 7f, Intensity = 1.8f, Color = new Color(1.000f, 0.860f, 0.660f) };
                case Family.InteriorSconce:
                    return new LightSpec { Prefab = "PM_LIGHT_INTERIOR_SCONCE", Title = "интерьер: бра/настенный", Range = 4.5f, Intensity = 1.2f, Color = new Color(1.000f, 0.840f, 0.630f) };
                case Family.InteriorTable:
                    return new LightSpec { Prefab = "PM_LIGHT_INTERIOR_TABLE", Title = "интерьер: настольный/торшер", Range = 3.5f, Intensity = 1.0f, Color = new Color(1.000f, 0.830f, 0.600f) };
                default:
                    return new LightSpec { Prefab = string.Empty, Title = "—", Range = 0f, Intensity = 0f, Color = Color.white };
            }
        }

        // ------------------------------------------------------------------ данные кластера

        private class Cluster
        {
            public string Key;
            public string Chunk;
            public readonly List<MeshRenderer> Parts = new List<MeshRenderer>();
            public readonly HashSet<string> PartNames = new HashSet<string>(StringComparer.Ordinal);
            public Bounds Bounds;
            public Vector3 LightPos;
            public bool HasEmit;
            public Family Family = Family.None;
            public string Note = string.Empty;

            public string Signature
            {
                get
                {
                    var list = new List<string>(PartNames);
                    list.Sort(StringComparer.Ordinal);
                    return string.Join("+", list.ToArray());
                }
            }
        }

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/World/PRIMUM/Свет: аудит светильников (CSV)", false, 360)]
        public static void MenuAudit()
        {
            Debug.Log(Audit());
        }

        [MenuItem("ProjectC/World/PRIMUM/Свет: расставить световые префабы", false, 361)]
        public static void MenuPlace()
        {
            Debug.Log(Place());
        }

        [MenuItem("ProjectC/World/PRIMUM/Свет: emissive-материал светящихся частей", false, 362)]
        public static void MenuGlow()
        {
            Debug.Log(ApplyGlowPass(true));
        }

        [MenuItem("ProjectC/World/PRIMUM/Свет: очистить расстановку", false, 363)]
        public static void MenuClear()
        {
            Debug.Log(Clear());
        }

        [MenuItem("ProjectC/World/PRIMUM/Свет: проверить (verify)", false, 364)]
        public static void MenuVerify()
        {
            Debug.Log(Verify());
        }

        [MenuItem("ProjectC/World/PRIMUM/Свет: пересобрать префабы и материал", false, 365)]
        public static void MenuBuildAssets()
        {
            EnsureGlowMaterial();
            int n = EnsurePrefabs();
            Debug.Log("[PRIMUM/Lights] Префабы и материал пересобраны: " + n + " префабов в " + PrefabDir + ", материал " + MatDir + "/" + GlowMatName + ".mat");
        }

        // ------------------------------------------------------------------ аудит

        /// <summary>Классификация всех кластеров светильников + CSV. Ничего в сцене не меняет.</summary>
        public static string Audit()
        {
            var city = GameObject.Find(CityRootName);
            if (city == null) return "[PRIMUM/Lights] Не найден корень " + CityRootName;

            List<Cluster> clusters = CollectClusters(city, out int lampsNodes, out int renderers);
            var counts = new Dictionary<Family, int>();
            var skipReasons = new Dictionary<string, int>();
            int placed = 0, skipped = 0, excluded = 0;

            foreach (Cluster c in clusters)
            {
                if (IsExcluded(c)) { excluded++; Bump(skipReasons, "чанк/дворец исключён"); }
                else if (c.Family == Family.None) { skipped++; Bump(skipReasons, c.Note.Length > 0 ? c.Note : "не классифицирован"); }
                else { placed++; Bump(counts, c.Family); }
            }

            WriteAuditCsv(clusters);

            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Lights] Аудит светильников");
            sb.AppendLine("  узлов LAMPS: " + lampsNodes + ", мешей в них: " + renderers + ", кластеров: " + clusters.Count);
            sb.AppendLine("  к расстановке: " + placed + ", пропущено по геометрии: " + skipped + ", исключённые чанки/дворцы: " + excluded);
            foreach (KeyValuePair<Family, int> kv in counts)
            {
                LightSpec s = SpecOf(kv.Key);
                sb.AppendLine("    " + kv.Key + " (" + s.Title + ", " + s.Prefab + "): " + kv.Value);
            }
            foreach (KeyValuePair<string, int> kv in skipReasons) sb.AppendLine("    skip: " + kv.Key + " = " + kv.Value);
            sb.AppendLine("  CSV: " + AuditRelativePath);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ emissive

        /// <summary>Ставит emissive-материал на светящиеся части (M_EMIT_LAMP / M_TQ_LAMP) вне ADMIN и дворцов.</summary>
        public static string ApplyGlowPass(bool markDirty)
        {
            var city = GameObject.Find(CityRootName);
            if (city == null) return "[PRIMUM/Lights] Не найден корень " + CityRootName;

            Material glow = EnsureGlowMaterial();
            List<Cluster> clusters = CollectClusters(city, out _, out _);

            int changed = 0, already = 0, skippedExcluded = 0;
            var seen = new HashSet<MeshRenderer>();
            foreach (Cluster c in clusters)
            {
                bool excluded = IsExcluded(c);
                foreach (MeshRenderer r in c.Parts)
                {
                    if (r == null || !seen.Add(r)) continue;
                    if (excluded) { skippedExcluded++; continue; }
                    Material m = r.sharedMaterial;
                    if (m == null) continue;
                    if (m == glow) { already++; continue; }
                    if (Array.IndexOf(GlowSourceMaterials, m.name) < 0) continue;
                    r.sharedMaterial = glow;
                    changed++;
                }
            }

            if (changed > 0 && markDirty) EditorSceneManager.MarkSceneDirty(city.scene);

            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Lights] Emissive-проход");
            sb.AppendLine("  материал: " + MatDir + "/" + GlowMatName + ".mat");
            sb.AppendLine("  назначено: " + changed + ", уже стоял: " + already + ", пропущено (ADMIN/дворцы): " + skippedExcluded);
            sb.AppendLine("  Замена материала — оверрайд на объектах FBX-модели: после реимпорта чанка проход надо повторить.");
            if (changed > 0) sb.AppendLine("  Сцена помечена dirty — сохранить (Ctrl+S).");
            return sb.ToString();
        }

        /// <summary>
        /// Материал светящихся частей. Shader — URP Unlit БЕЗ keywords: у URP-Lit вариант с emission
        /// проверен и отсюда: `AssetDatabase.Refresh()` после создания повторно импортирует материал, и
        /// keyword `_EMISSION` слетает (на диске оставался `m_ValidKeywords: []`, все лампы — тёмные).
        /// Unlit с HDR-цветом не имеет keyword-состояния вообще, поэтому персистентен.
        /// </summary>
        private static Material EnsureGlowMaterial()
        {
            EnsureFolder(MatDir);
            string path = MatDir + "/" + GlowMatName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            if (mat.shader != shader) mat.shader = shader;

            // HDR-накал (как у MF_LAMP_BULB в фермах): тономаппинг сожмёт, но ядро читается как огонь.
            var glow = new Color(1f * 2.4f, 0.70f * 2.4f, 0.36f * 2.4f, 1f);
            mat.SetColor("_BaseColor", glow);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", glow);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);   // opaque
            // Если бы шейдер оказался Lit — дублируем и в emission, лишним не будет.
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", glow);
                mat.EnableKeyword("_EMISSION");
                ForceKeyword(mat, "_EMISSION");
            }
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Гарантирует наличие keyword в m_ValidKeywords материала.</summary>
        private static void ForceKeyword(Material mat, string keyword)
        {
            var so = new SerializedObject(mat);
            SerializedProperty list = so.FindProperty("m_ValidKeywords");
            if (list == null) return;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).stringValue == keyword) return;
            }
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = keyword;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ префабы

        private static int EnsurePrefabs()
        {
            EnsureFolder(PrefabDir);
            EnsureGlowMaterial();
            int n = 0;
            foreach (Family f in Enum.GetValues(typeof(Family)))
            {
                if (f == Family.None) continue;
                LightSpec spec = SpecOf(f);
                BuildPrefab(spec);
                n++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return n;
        }

        /// <summary>Наружные приборы (улица/фасад) → группа PRIMUM_OUTLIGHT; интерьерные → PRIMUM_INLIGHT.</summary>
        private static bool IsOutdoor(Family f)
        {
            return f == Family.StreetPost || f == Family.StreetWall || f == Family.FacadeWall || f == Family.FacadeSconce;
        }

        /// <summary>Имя узла = префикс группы день/ночь + исходный ключ кластера.</summary>
        private static string NodeName(Family f, string key)
        {
            return (IsOutdoor(f) ? "PRIMUM_OUTLIGHT_" : "PRIMUM_INLIGHT_") + key;
        }

        private static string BuildPrefab(LightSpec spec)
        {
            var go = new GameObject(spec.Prefab);
            go.layer = CityLayer;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = spec.Range;
            light.intensity = spec.Intensity;
            light.color = spec.Color;
            light.shadows = LightShadows.None;         // доп. тени в URP-ассете выключены — держим паритет
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.renderMode = LightRenderMode.Auto;
            // ВЫКЛЮЧЕН: включает только PrimumLightPool (иначе 1 900 источников в первом кадре).
            light.enabled = false;

            string path = PrefabDir + "/" + spec.Prefab + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return path;
        }

        // ------------------------------------------------------------------ расстановка

        /// <summary>Идемпотентная расстановка световых префабов + emissive-проход + манифест.</summary>
        public static string Place()
        {
            var city = GameObject.Find(CityRootName);
            if (city == null) return "[PRIMUM/Lights] Не найден корень " + CityRootName;

            Transform worldRoot = city.transform.parent != null
                ? city.transform.parent
                : GameObject.Find(WorldRootFallback)?.transform;
            if (worldRoot == null) return "[PRIMUM/Lights] Не найден world root";

            EnsureGlowMaterial();
            EnsurePrefabs();

            List<Cluster> clusters = CollectClusters(city, out int lampsNodes, out int renderers);

            // Чистим прошлую расстановку целиком (идемпотентность).
            Transform old = worldRoot.Find(LightsRootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(LightsRootName);
            root.transform.SetParent(worldRoot, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = CityLayer;
            GameObjectUtility.SetStaticEditorFlags(root, (StaticEditorFlags)0);
            var pool = root.AddComponent<PrimumLightPool>();

            var chunkNodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var counts = new Dictionary<Family, int>();
            var skipReasons = new Dictionary<string, int>();
            int placed = 0, skipped = 0, excluded = 0, duplicates = 0;
            var usedPositions = new HashSet<string>(StringComparer.Ordinal);

            var csv = new StringBuilder();
            csv.AppendLine("chunk,cluster,family,prefab,action,has_emit,parts,x,y,z,size_x,size_y,size_z,note");

            foreach (Cluster c in clusters)
            {
                string action;
                string note = c.Note;
                if (IsExcluded(c))
                {
                    action = "skip";
                    excluded++;
                    note = "исключено: ADMIN/дворец (свои светильники)";
                    Bump(skipReasons, "исключено: ADMIN/дворец");
                }
                else if (c.Family == Family.None)
                {
                    action = "skip";
                    skipped++;
                    if (string.IsNullOrEmpty(note)) note = "не классифицирован";
                    Bump(skipReasons, note);
                }
                else
                {
                    action = "place";
                    placed++;
                    Bump(counts, c.Family);

                    if (!chunkNodes.TryGetValue(c.Chunk, out Transform chunkT))
                    {
                        var chunkGo = new GameObject(c.Chunk);
                        chunkGo.transform.SetParent(root.transform, false);
                        chunkGo.layer = CityLayer;
                        GameObjectUtility.SetStaticEditorFlags(chunkGo, (StaticEditorFlags)0);
                        chunkT = chunkGo.transform;
                        chunkNodes[c.Chunk] = chunkT;
                    }

                    LightSpec spec = SpecOf(c.Family);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + spec.Prefab + ".prefab");
                    if (prefab == null)
                    {
                        action = "skip";
                        placed--;
                        skipped++;
                        note = "префаб не найден: " + spec.Prefab;
                        Bump(skipReasons, "префаб не найден");
                    }
                    else
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, chunkT);
                        inst.name = NodeName(c.Family, c.Key);
                        inst.layer = CityLayer;
                        inst.transform.position = c.LightPos; // корень identity → мировые координаты

                        // одна лампа = одна точка; совпадение позиций — повод посмотреть глазами
                        string keyPos = F(c.LightPos.x, 2) + "|" + F(c.LightPos.y, 2) + "|" + F(c.LightPos.z, 2);
                        if (!usedPositions.Add(keyPos)) { duplicates++; note = "совпадает позиция с другим источником"; }
                    }
                }

                csv.Append(Csv(c.Chunk)).Append(',')
                   .Append(Csv(c.Key)).Append(',')
                   .Append(c.Family).Append(',')
                   .Append(c.Family == Family.None ? string.Empty : SpecOf(c.Family).Prefab).Append(',')
                   .Append(action).Append(',')
                   .Append(c.HasEmit ? "1" : "0").Append(',')
                   .Append(Csv(c.Signature)).Append(',')
                   .Append(F(c.LightPos.x)).Append(',')
                   .Append(F(c.LightPos.y)).Append(',')
                   .Append(F(c.LightPos.z)).Append(',')
                   .Append(F(c.Bounds.size.x)).Append(',')
                   .Append(F(c.Bounds.size.y)).Append(',')
                   .Append(F(c.Bounds.size.z)).Append(',')
                   .Append(Csv(note)).AppendLine();
            }

            WriteText(ManifestRelativePath, csv.ToString());

            // emissive: без него ночью светят только точки, корпуса остаются тёмными
            ApplyGlowPass(false);

            EditorSceneManager.MarkSceneDirty(city.scene);
            Selection.activeGameObject = root;

            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Lights] Расстановка световых префабов");
            sb.AppendLine("  узлов LAMPS: " + lampsNodes + ", мешей: " + renderers + ", кластеров: " + clusters.Count);
            sb.AppendLine("  поставлено источников: " + placed + ", пропущено: " + skipped + " (не светильники/крупные), исключено: " + excluded);
            if (duplicates > 0) sb.AppendLine("  ⚠ совпадают позиции: " + duplicates);
            foreach (KeyValuePair<Family, int> kv in counts) sb.AppendLine("    " + kv.Key + ": " + kv.Value);
            foreach (KeyValuePair<string, int> kv in skipReasons) sb.AppendLine("    skip: " + kv.Key + " = " + kv.Value);
            sb.AppendLine("  бюджет пула: maxActive=" + pool.MaxActive + ", radius=" + pool.ActiveRadius + " м (Forward, per-object limit 4)");
            sb.AppendLine("  манифест: " + ManifestRelativePath);
            sb.AppendLine("  Сцена помечена dirty — сохранить (Ctrl+S).");
            return sb.ToString();
        }

        public static string Clear()
        {
            var city = GameObject.Find(CityRootName);
            Transform worldRoot = city != null && city.transform.parent != null
                ? city.transform.parent
                : GameObject.Find(WorldRootFallback)?.transform;
            if (worldRoot == null) return "[PRIMUM/Lights] Не найден world root";

            Transform old = worldRoot.Find(LightsRootName);
            if (old == null) return "[PRIMUM/Lights] Узел " + LightsRootName + " отсутствует — чистить нечего.";
            int n = old.GetComponentsInChildren<Light>(true).Length;
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (city != null) EditorSceneManager.MarkSceneDirty(city.scene);
            return "[PRIMUM/Lights] Узел " + LightsRootName + " удалён (источников было " + n + ").";
        }

        // ------------------------------------------------------------------ проверка

        public static string Verify()
        {
            var city = GameObject.Find(CityRootName);
            if (city == null) return "[PRIMUM/Lights] Не найден корень " + CityRootName;
            Transform worldRoot = city.transform.parent != null ? city.transform.parent : GameObject.Find(WorldRootFallback)?.transform;
            if (worldRoot == null) return "[PRIMUM/Lights] Не найден world root";

            Transform root = worldRoot.Find(LightsRootName);
            var sb = new StringBuilder();
            sb.AppendLine("[PRIMUM/Lights] Проверка");

            if (root == null)
            {
                sb.AppendLine("  ❌ узла " + LightsRootName + " нет");
                return sb.ToString();
            }

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            int enabledNow = 0;
            var positions = new HashSet<string>(StringComparer.Ordinal);
            int duplicatePos = 0, wrongLayer = 0, staticFlags = 0, shadowed = 0, misplaced = 0;
            var badFamilies = new HashSet<string>(StringComparer.Ordinal);

            foreach (Light l in lights)
            {
                if (l == null) continue;
                if (l.enabled) enabledNow++;
                if (l.shadows != LightShadows.None) shadowed++;
                Transform chunk = l.transform.parent != null ? l.transform.parent.parent : null;
                if (chunk == null) misplaced++;
                if (l.gameObject.layer != CityLayer) wrongLayer++;
                if (GameObjectUtility.GetStaticEditorFlags(l.gameObject) != 0) staticFlags++;
                string pos = F(l.transform.position.x, 2) + "|" + F(l.transform.position.y, 2) + "|" + F(l.transform.position.z, 2);
                if (!positions.Add(pos)) duplicatePos++;

                string prefabName = PrefabUtility.GetCorrespondingObjectFromSource(l.gameObject) != null
                    ? PrefabUtility.GetCorrespondingObjectFromSource(l.gameObject).name
                    : "<не префаб>";
                if (!prefabName.StartsWith("PM_LIGHT_", StringComparison.Ordinal)) badFamilies.Add(prefabName);
            }

            var pool = root.GetComponent<PrimumLightPool>();
            int maxActive = pool != null ? pool.MaxActive : 0;

            sb.AppendLine("  инстансов-источников: " + lights.Length + ", включено сейчас: " + enabledNow);
            sb.AppendLine("  чанк-узлов: " + root.childCount);
            sb.AppendLine("  пул: " + (pool == null ? "❌ PrimumLightPool отсутствует" : "✅ maxActive=" + maxActive + ", radius=" + pool.ActiveRadius + " м"));
            sb.AppendLine("  префаб не PM_LIGHT_*: " + badFamilies.Count);
            sb.AppendLine("  не на слое PRIMUM: " + wrongLayer + ", static-флаги: " + staticFlags + ", с тенями: " + shadowed);
            sb.AppendLine("  совпадающих позиций: " + duplicatePos + ", без чанк-родителя: " + misplaced);
            if (maxActive > 0 && lights.Length > maxActive)
                sb.AppendLine("  ✅ бюджет: " + lights.Length + " источников, одновременно горят ≤ " + maxActive);
            else if (maxActive > 0)
                sb.AppendLine("  ℹ источников меньше лимита (" + lights.Length + " ≤ " + maxActive + ") — пул включит все в радиусе");

            // emissive-статус
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + GlowMatName + ".mat");
            if (glow == null)
            {
                sb.AppendLine("  emissive-материал: ❌ нет " + GlowMatName);
            }
            else
            {
                Color c = glow.HasProperty("_BaseColor") ? glow.GetColor("_BaseColor") : Color.black;
                float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                sb.AppendLine("  emissive-материал: " + (peak > 1.01f ? "✅ " : "❌ ") + GlowMatName + " shader=" + glow.shader.name + " peak=" + peak.ToString("F2", CultureInfo.InvariantCulture));
            }
            int stillDark = 0;
            List<Cluster> clusters = CollectClusters(city, out _, out _);
            foreach (Cluster c in clusters)
            {
                if (IsExcluded(c)) continue;
                foreach (MeshRenderer r in c.Parts)
                {
                    if (r == null || r.sharedMaterial == null || glow == null) continue;
                    if (Array.IndexOf(GlowSourceMaterials, r.sharedMaterial.name) >= 0) stillDark++;
                }
            }
            if (stillDark > 0) sb.AppendLine("  ⚠ без emissive осталось мешей: " + stillDark + " (повторить «Свет: emissive-материал…»)");
            else sb.AppendLine("  ✅ светящиеся части получили emissive");

            return sb.ToString();
        }

        // ------------------------------------------------------------------ сбор кластеров

        private static List<Cluster> CollectClusters(GameObject city, out int lampsNodes, out int renderers)
        {
            var index = new Dictionary<string, Cluster>(StringComparer.Ordinal);
            var seen = new HashSet<MeshRenderer>();
            lampsNodes = 0;
            renderers = 0;

            foreach (Transform chunk in city.transform)
            {
                foreach (Transform node in chunk.GetComponentsInChildren<Transform>(true))
                {
                    if (node.name != LampsNodeName) continue;
                    lampsNodes++;
                    foreach (MeshRenderer r in node.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r == null || !seen.Add(r)) continue;
                        renderers++;
                        string key = KeyOf(r.gameObject.name);
                        string id = chunk.name + "\u0001" + key;
                        if (!index.TryGetValue(id, out Cluster c))
                        {
                            c = new Cluster { Key = key, Chunk = chunk.name };
                            index[id] = c;
                        }
                        c.Parts.Add(r);
                        c.PartNames.Add(PartOf(r.gameObject.name));
                        if (r.sharedMaterial != null && r.sharedMaterial.name.IndexOf("EMIT", StringComparison.Ordinal) >= 0)
                            c.HasEmit = true;
                    }
                }
            }

            var clusters = new List<Cluster>(index.Count);
            foreach (KeyValuePair<string, Cluster> kv in index)
            {
                ComputeGeometry(kv.Value);
                Classify(kv.Value);
                clusters.Add(kv.Value);
            }
            clusters.Sort((a, b) =>
            {
                int cmp = string.Compare(a.Chunk, b.Chunk, StringComparison.Ordinal);
                return cmp != 0 ? cmp : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            });
            return clusters;
        }

        private static void ComputeGeometry(Cluster c)
        {
            bool first = true;
            foreach (MeshRenderer r in c.Parts)
            {
                if (r == null) continue;
                if (first) { c.Bounds = r.bounds; first = false; }
                else c.Bounds.Encapsulate(r.bounds);
            }

            // точка источника — центр «головы» светильника, а не центр габарита (иначе свет
            // окажется на середине столба или на кронштейне).
            foreach (string head in HeadParts)
            {
                foreach (MeshRenderer r in c.Parts)
                {
                    if (r == null) continue;
                    if (!PartOf(r.gameObject.name).Equals(head, StringComparison.Ordinal)) continue;
                    c.LightPos = r.bounds.center;
                    return;
                }
            }

            // Только подвес (шнур/цепь/шток) — источник на его нижнем конце, а не посреди шнура.
            bool onlyHanger = c.PartNames.Count > 0;
            foreach (string p in c.PartNames)
            {
                if (p.Equals("CORD", StringComparison.Ordinal) || p.Equals("CHAIN", StringComparison.Ordinal) || p.Equals("ROD", StringComparison.Ordinal)) continue;
                onlyHanger = false;
                break;
            }
            if (onlyHanger)
            {
                Vector3 center = c.Bounds.center;
                c.LightPos = new Vector3(center.x, c.Bounds.min.y, center.z);
                return;
            }

            c.LightPos = c.Bounds.center;
        }

        private static void Classify(Cluster c)
        {
            c.Note = string.Empty;
            string k = c.Key.ToUpperInvariant();

            bool hasPole = c.PartNames.Contains("POLE") || c.PartNames.Contains("POST")
                           || k.Contains("POLE") || k.EndsWith("_LMP", StringComparison.Ordinal) || k.Contains("_LMP_");

            // --- не светильники: под токен LIGHT/LAMP попали окна и марши
            if (k.Contains("SKYLIGHT") || c.PartNames.Contains("SKYLIGHT")) { c.Note = "не светильник: SKYLIGHT"; c.Family = Family.None; return; }
            if (c.PartNames.SetEquals(new[] { "BAR" })) { c.Note = "не светильник: BAR (перекладина)"; c.Family = Family.None; return; }
            if (c.PartNames.SetEquals(new[] { "FLIGHT" })) { c.Note = "не светильник: FLIGHT (марш)"; c.Family = Family.None; return; }
            if (c.PartNames.SetEquals(new[] { "GLASS" }) && !c.HasEmit) { c.Note = "не светильник: стекло"; c.Family = Family.None; return; }

            // --- крупные сборки: один свет на 380-метровый прогон бессмыслен
            Vector3 size = c.Bounds.size;
            float extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (extent > MaxClusterExtent)
            {
                c.Note = "крупная сборка (" + extent.ToString("F1", CultureInfo.InvariantCulture) + " м) — разбить вручную";
                c.Family = Family.None;
                return;
            }

            bool nameFurniture = k.Contains("FURN") || k.Contains("DESK");
            bool interiorPart = c.PartNames.Contains("STEM") || c.PartNames.Contains("CORD") || c.PartNames.Contains("PAPER");

            if (hasPole) { c.Family = Family.StreetPost; return; }
            if (k.Contains("CHAND")) { c.Family = Family.InteriorPendant; return; }
            if (c.PartNames.Contains("PAPER")) { c.Family = Family.InteriorPendant; return; }
            if (k.Contains("HALL") || k.Contains("HOME")) { c.Family = Family.InteriorPendant; return; }
            if (nameFurniture || interiorPart)
            {
                c.Family = c.PartNames.SetEquals(new[] { "BULB" }) ? Family.InteriorSconce : Family.InteriorTable;
                return;
            }
            if (k.Contains("SCONCE"))
            {
                c.Family = (k.Contains("FAC") || k.Contains("EXT") || k.Contains("OUT") || k.Contains("FW_"))
                    ? Family.FacadeSconce
                    : Family.InteriorSconce;
                return;
            }
            if (k.Contains("FW_LAMP") || k.Contains("WLAMP") || k.Contains("FAC") || k.Contains("EXT") || k.Contains("OUT"))
            {
                c.Family = Family.StreetWall;
                return;
            }
            if (c.PartNames.SetEquals(new[] { "BULB" })) { c.Family = Family.InteriorSconce; return; }
            // LAMPL/LAMPR/LANTERN/... — фасадный прибор без токенов FAC/EXT/ARM
            if (k.Contains("LAMP") || k.Contains("LANTERN")) { c.Family = Family.FacadeWall; return; }
            if (c.PartNames.Contains("ARM")) { c.Family = Family.FacadeWall; return; }
            if (c.HasEmit) { c.Family = Family.FacadeWall; return; }

            c.Note = "не классифицирован";
            c.Family = Family.None;
        }

        private static bool IsExcluded(Cluster c)
        {
            if (Array.IndexOf(ExcludedChunks, c.Chunk) >= 0) return true;
            foreach (string prefix in ExcludedNamePrefixes)
                if (c.Key.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        // ------------------------------------------------------------------ утилиты

        private static string KeyOf(string objectName)
        {
            int i = objectName.LastIndexOf('_');
            return i > 0 ? objectName.Substring(0, i) : objectName;
        }

        private static string PartOf(string objectName)
        {
            int i = objectName.LastIndexOf('_');
            return i >= 0 && i < objectName.Length - 1 ? objectName.Substring(i + 1) : objectName;
        }

        private static void Bump<TKey>(Dictionary<TKey, int> dict, TKey key)
        {
            if (key == null) return;
            dict.TryGetValue(key, out int n);
            dict[key] = n + 1;
        }

        private static string F(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
        private static string F(float v, int digits) => v.ToString("F" + digits, CultureInfo.InvariantCulture);

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void WriteAuditCsv(List<Cluster> clusters)
        {
            var sb = new StringBuilder();
            sb.AppendLine("chunk,cluster,family,signature,has_emit,action,x,y,z,size_x,size_y,size_z,note");
            foreach (Cluster c in clusters)
            {
                string action;
                if (IsExcluded(c)) action = "skip";
                else if (c.Family == Family.None) action = "skip";
                else action = "place";
                string note = c.Note;
                if (IsExcluded(c)) note = "исключено: ADMIN/дворец";
                sb.Append(Csv(c.Chunk)).Append(',')
                  .Append(Csv(c.Key)).Append(',')
                  .Append(c.Family).Append(',')
                  .Append(Csv(c.Signature)).Append(',')
                  .Append(c.HasEmit ? "1" : "0").Append(',')
                  .Append(action).Append(',')
                  .Append(F(c.LightPos.x)).Append(',')
                  .Append(F(c.LightPos.y)).Append(',')
                  .Append(F(c.LightPos.z)).Append(',')
                  .Append(F(c.Bounds.size.x)).Append(',')
                  .Append(F(c.Bounds.size.y)).Append(',')
                  .Append(F(c.Bounds.size.z)).Append(',')
                  .Append(Csv(note)).AppendLine();
            }
            WriteText(AuditRelativePath, sb.ToString());
        }

        private static void WriteText(string relativePath, string content)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }
    }
}
