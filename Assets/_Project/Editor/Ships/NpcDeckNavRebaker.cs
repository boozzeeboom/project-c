#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectC.EditorTools.Ships
{
    /// <summary>
    /// Пере-запекает NavMesh палубы у 17 NPC-кораблей, получивших визуал эталона (NpcShipVisualGraft),
    /// и ставит точки экипажа CrewAnchors на палубу в 0.5 м от PilotSeat.
    ///
    /// Зачем бейк заново: после грейфта корпус стал эталонным (17…251 коллайдер), а запечённый навмеш
    /// остался от плоской заглушки Platform — экипаж ходил по несуществующей палубе.
    ///
    /// Что меняется в префабе:
    ///   • NavMeshSurface.m_LayerMask = Default | ShipDeck (65). Корпусные коллайдеры лежат на Default,
    ///     старый mask 64 (только ShipDeck) давал ПУСТОЙ навмеш. Остальные настройки Surface не трогаются
    ///     (CollectObjects.All, UseGeometry.PhysicsColliders, агент Humanoid, minRegionArea 2).
    ///   • DeckNavSurface.localPosition = 0: смещение (0, -0.37, 0) от заглушки ставило навмеш выше корпуса.
    ///   • Бейк выполняется в локальном пространстве корня (корень на время бейка в scale 1) — так же, как
    ///     в ShipPresetCreator. ShipDeckNav переводит deck-local → nav как origin + local, поэтому навмеш
    ///     в мировом масштабе у корней со scale (6, 0.9, 12) не совпал бы с рантаймом.
    ///   • ShipDeckNav._deckNavMeshData и NavMeshSurface.m_NavMeshData → новый ассет корабля.
    ///   • CrewAnchors: узел ставится в точку PilotSeat по XZ на высоту палубы, точки — по кольцу r = 0.5 м
    ///     на той же высоте. Имена точек не меняются (ShipCrewSpawner ищет их по spawnAnchorId).
    /// Корень префаба, его компоненты, коллайдеры, двери, слоты и масштаб не трогаются.
    ///
    /// Меню: ProjectC → Ships → NPC DeckNav.
    /// </summary>
    public static class NpcDeckNavRebaker
    {
        private const string ShipsDir = "Assets/_Project/Prefabs/Ships";
        private const string BackupDir = "_local_backups/ships_decknav_backup_2026-11-04";
        private const string ReportPath = "docs/dev/NPC_DECKNAV_REPORT.md";

        /// <summary>Default (0) | ShipDeck (6): корпус и коллайдеры палубы.</summary>
        private const int DeckLayerMask = (1 << 0) | (1 << 6);

        /// <summary>Радиус кольца точек экипажа вокруг кресла пилота, м (мировые).</summary>
        private const float AnchorRingRadius = 0.5f;

        private static readonly string[] ShipNames =
        {
            "Берег", "Вавилон", "Ветроворот", "Горгона", "Жук", "Летучий", "Мастодонт", "Олимп",
            "Пещера", "Река", "Сильфида", "Скат", "Странник", "Торренс", "Угольщик", "Цитадель", "Шмель"
        };

        private sealed class ShipResult
        {
            public string Name;
            public string ClassName;
            public int Tris;
            public Vector3 AuthoredScale;
            public Bounds NavBoundsLocal;
            public float PilotDeckDelta;      // метры
            public int AnchorCount;
            public float AnchorMaxRadius;     // метры
            public float AnchorMaxSampleDelta; // метры
            public string NavAsset;
            public bool Applied;
            public readonly List<string> Problems = new List<string>();
        }

        private sealed class AnchorPlan
        {
            public Transform CrewAnchors;
            public Vector3 CrewLocal;
            public readonly List<Transform> Kids = new List<Transform>();
            public readonly List<Vector3> KidLocal = new List<Vector3>();
        }

        // ------------------------------------------------------------------ меню

        [MenuItem("ProjectC/Ships/NPC DeckNav/1. Dry Run (план, без записи)", priority = 110)]
        public static void DryRun()
        {
            Run(0, ShipNames.Length, false);
        }

        [MenuItem("ProjectC/Ships/NPC DeckNav/2. Apply (бейк + точки экипажа)", priority = 111)]
        public static void Apply()
        {
            Run(0, ShipNames.Length, true);
        }

        /// <summary>Порционный apply: те же корабли, но по указанному диапазону (для длинных проходов).</summary>
        public static void ApplyRange(int start, int count)
        {
            Run(start, count, true);
        }

        [MenuItem("ProjectC/Ships/NPC DeckNav/3. Verify", priority = 112)]
        public static void Verify()
        {
            var report = new StringBuilder();
            report.AppendLine("# NPC DeckNav — проверка");
            report.AppendLine();
            report.AppendLine("| Корабль | Класс | Mask | Deck y | Surface | Tris | Навмеш Y, м | Δ кресло→палуба, м | Точек | макс. R, м | Δ точки→палуба, м | Ассет |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");

            int bad = 0;
            foreach (var name in ShipNames)
            {
                var path = ShipsDir + "/" + name + ".prefab";
                if (!File.Exists(path))
                {
                    report.AppendLine("| " + name + " | — | — | — | — | — | — | — | — | — | — | **нет префаба** |");
                    bad++;
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                NavMeshDataInstance instance = default;
                var authoredScale = root.transform.localScale;
                try
                {
                    var cls = ReadClassName(root);
                    var deck = root.transform.Find("DeckNavSurface");
                    var nms = deck != null ? deck.GetComponent<NavMeshSurface>() : null;
                    var deckNav = root.GetComponent<ProjectC.Ship.ShipDeckNav>();

                    if (nms == null || deckNav == null)
                    {
                        report.AppendLine("| " + name + " | " + cls + " | — | — | " + (nms == null ? "НЕТ" : "есть") +
                                          " | — | — | — | — | — | — | **нет NavMeshSurface/ShipDeckNav** |");
                        bad++;
                        continue;
                    }

                    var nmsSo = new SerializedObject(nms);
                    int mask = nmsSo.FindProperty("m_LayerMask").intValue;
                    bool surfaceEnabled = nms.enabled;
                    float deckY = deck.localPosition.y;

                    var deckNavSo = new SerializedObject(deckNav);
                    var dataProp = deckNavSo.FindProperty("_deckNavMeshData");
                    var data = dataProp != null ? dataProp.objectReferenceValue as NavMeshData : null;
                    var assetPath = data != null ? AssetDatabase.GetAssetPath(data) : "—";

                    string navY = "—";
                    string pilotDelta = "—";
                    string anchorRadius = "—";
                    string anchorDelta = "—";
                    int anchorCount = 0;
                    int tris = 0;

                    if (data == null)
                    {
                        bad++;
                    }
                    else
                    {
                        // Навмеш запечён в локальном пространстве корня — меряем при scale 1.
                        root.transform.localScale = Vector3.one;
                        try
                        {
                            instance = NavMesh.AddNavMeshData(data, Vector3.zero, Quaternion.identity);

                            var pilot = root.transform.Find("PilotSeat");
                            if (instance.valid && pilot != null)
                            {
                                NavMeshHit hit;
                                // Корень в origin со scale 1 → мировые координаты узла совпадают с локальными в нав-пространстве.
                                var pilotLocal = pilot.position;
                                if (NavMesh.SamplePosition(pilotLocal, out hit, LocalSampleRadius(20f, authoredScale, 0.5f), NavMesh.AllAreas))
                                    pilotDelta = ((hit.position.y - pilotLocal.y) * authoredScale.y)
                                        .ToString("F2", CultureInfo.InvariantCulture);
                                else
                                    pilotDelta = "НЕТ";
                            }

                            var crew = root.transform.Find("CrewAnchors");
                            if (crew != null)
                            {
                                float maxDelta = 0f;
                                for (int i = 0; i < crew.childCount; i++)
                                {
                                    var kid = crew.GetChild(i);
                                    NavMeshHit kh;
                                    // kid.localPosition задан относительно CrewAnchors, поэтому в нав-пространство берём position.
                                    var kidNav = kid.position;
                                    if (NavMesh.SamplePosition(kidNav, out kh, LocalSampleRadius(0.6f, authoredScale, 0.05f), NavMesh.AllAreas))
                                        maxDelta = Mathf.Max(maxDelta, Mathf.Abs(kh.position.y - kidNav.y) * authoredScale.y);
                                    else
                                        maxDelta = 99f;
                                    anchorCount++;
                                }
                                anchorDelta = maxDelta >= 99f ? "вне навмеша" : maxDelta.ToString("F2", CultureInfo.InvariantCulture);
                            }

                            var tri = NavMesh.CalculateTriangulation();
                            tris = tri.indices.Length / 3;
                        }
                        finally
                        {
                            if (instance.valid) instance.Remove();
                            root.transform.localScale = authoredScale;
                        }

                        var bounds = data.sourceBounds;
                        navY = ((bounds.center.y - bounds.extents.y) * authoredScale.y).ToString("F1", CultureInfo.InvariantCulture) + "…" +
                               ((bounds.center.y + bounds.extents.y) * authoredScale.y).ToString("F1", CultureInfo.InvariantCulture);
                    }

                    // Радиус кольца меряется в мире — уже с авторским масштабом корня.
                    var pilotT = root.transform.Find("PilotSeat");
                    var crewT = root.transform.Find("CrewAnchors");
                    if (pilotT != null && crewT != null && crewT.childCount > 0)
                    {
                        var pilotWorld = pilotT.position;
                        float maxR = 0f;
                        for (int i = 0; i < crewT.childCount; i++)
                        {
                            var kidWorld = crewT.GetChild(i).position;
                            maxR = Mathf.Max(maxR, new Vector2(kidWorld.x - pilotWorld.x, kidWorld.z - pilotWorld.z).magnitude);
                        }
                        anchorRadius = maxR.ToString("F2", CultureInfo.InvariantCulture);
                    }

                    if (mask != DeckLayerMask) bad++;
                    if (surfaceEnabled) bad++;
                    if (Mathf.Abs(deckY) > 0.001f) bad++;
                    if (data == null) bad++;
                    if (anchorCount == 0) bad++;

                    report.AppendLine(string.Format(
                        "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} |",
                        name, cls, mask, deckY.ToString("F2", CultureInfo.InvariantCulture),
                        surfaceEnabled ? "**ВКЛ**" : "выкл", tris, navY,
                        pilotDelta, anchorCount, anchorRadius, anchorDelta, assetPath));
                }
                finally
                {
                    root.transform.localScale = authoredScale;
                    if (instance.valid) instance.Remove();
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            report.AppendLine();
            report.AppendLine(bad == 0 ? "Проблем не найдено." : "Проблемных пунктов: " + bad + ".");
            Debug.Log("[NPC DeckNav] Verify\n" + report);
            WriteReport(AppendReport("verify", report.ToString()));
        }

        [MenuItem("ProjectC/Ships/NPC DeckNav/9. Rollback (из бэкапа)", priority = 200)]
        public static void Rollback()
        {
            if (!Directory.Exists(BackupDir))
            {
                Debug.LogError("[NPC DeckNav] Нет бэкапа: " + BackupDir);
                return;
            }

            if (!EditorUtility.DisplayDialog("NPC DeckNav — Rollback",
                    "Вернуть 17 NPC-префабов и их навмеши из бэкапа " + BackupDir + "?", "Вернуть", "Отмена"))
            {
                return;
            }

            int restored = 0;
            foreach (var name in ShipNames)
            {
                var prefabBackup = Path.Combine(BackupDir, name + ".prefab");
                if (!File.Exists(prefabBackup))
                {
                    Debug.LogWarning("[NPC DeckNav] Нет в бэкапе префаба: " + name);
                    continue;
                }

                File.Copy(prefabBackup, ShipsDir + "/" + name + ".prefab", true);
                AssetDatabase.ImportAsset(ShipsDir + "/" + name + ".prefab", ImportAssetOptions.ForceUpdate);

                var navBackupDir = Path.Combine(BackupDir, name);
                if (Directory.Exists(navBackupDir))
                {
                    foreach (var file in Directory.GetFiles(navBackupDir, "*.asset"))
                    {
                        var target = ShipsDir + "/" + name + "/" + Path.GetFileName(file);
                        File.Copy(file, target, true);
                        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                    }
                }

                restored++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[NPC DeckNav] Восстановлено префабов: " + restored);
        }

        // ------------------------------------------------------------------ основной проход

        private static void Run(int start, int count, bool apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[NPC DeckNav] Play Mode активен. Остановите игру и повторите.");
                return;
            }

            int from = Mathf.Clamp(start, 0, ShipNames.Length);
            int to = Mathf.Clamp(start + count, from, ShipNames.Length);

            var report = new StringBuilder();
            report.AppendLine("# NPC DeckNav — " + (apply ? "применение" : "dry-run") + " (" + from + "…" + (to - 1) + ")");
            report.AppendLine();
            report.AppendLine("| Корабль | Класс | Tris | Навмеш Y, м | Δ кресло→палуба, м | Точек | макс. R, м | Δ точки→палуба, м | Ассет |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|");

            int processed = 0;
            for (int i = from; i < to; i++)
            {
                ShipResult result;
                try
                {
                    result = Process(ShipNames[i], apply);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[NPC DeckNav] " + ShipNames[i] + ": " + ex);
                    report.AppendLine("| " + ShipNames[i] + " | — | — | — | — | — | — | — | **ОШИБКА: " + ex.Message + "** |");
                    continue;
                }

                processed++;
                var b = result.NavBoundsLocal;
                var s = result.AuthoredScale;
                report.AppendLine(string.Format(
                    "| {0} | {1} | {2} | {3}…{4} | {5} | {6} | {7} | {8} | {9} |{10}",
                    result.Name, result.ClassName, result.Tris,
                    ((b.center.y - b.extents.y) * s.y).ToString("F1", CultureInfo.InvariantCulture),
                    ((b.center.y + b.extents.y) * s.y).ToString("F1", CultureInfo.InvariantCulture),
                    result.PilotDeckDelta.ToString("F2", CultureInfo.InvariantCulture),
                    result.AnchorCount,
                    result.AnchorMaxRadius.ToString("F2", CultureInfo.InvariantCulture),
                    result.AnchorMaxSampleDelta.ToString("F2", CultureInfo.InvariantCulture),
                    result.NavAsset ?? "—",
                    result.Applied ? "" : " (не записано)"));

                foreach (var p in result.Problems)
                {
                    report.AppendLine("| ⚠ " + result.Name + " | | | | | " + p + " | | | |");
                }
            }

            report.AppendLine();
            report.AppendLine("Обработано кораблей: **" + processed + "**" + (apply ? " (записано)." : " (запись не выполнялась)."));

            Debug.Log("[NPC DeckNav]\n" + report);
            WriteReport(AppendReport(apply ? "apply" : "dry-run", report.ToString()));

            if (apply)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[NPC DeckNav] Apply завершён. Дальше: 3. Verify и визуальная проверка экипажа в Play Mode.");
            }
        }

        private static ShipResult Process(string name, bool apply)
        {
            var result = new ShipResult { Name = name, Applied = false };
            var path = ShipsDir + "/" + name + ".prefab";
            if (!File.Exists(path))
            {
                result.Problems.Add("префаб не найден");
                return result;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            var authoredScale = root.transform.localScale;
            result.AuthoredScale = authoredScale;

            NavMeshData baked = null;
            NavMeshDataInstance instance = default;
            try
            {
                result.ClassName = ReadClassName(root);

                var deck = root.transform.Find("DeckNavSurface");
                if (deck == null)
                {
                    result.Problems.Add("нет узла DeckNavSurface");
                    return result;
                }

                var nms = deck.GetComponent<NavMeshSurface>();
                if (nms == null)
                {
                    result.Problems.Add("нет NavMeshSurface на DeckNavSurface");
                    return result;
                }

                var deckNav = root.GetComponent<ProjectC.Ship.ShipDeckNav>();
                if (deckNav == null)
                {
                    result.Problems.Add("нет ShipDeckNav на корне");
                    return result;
                }

                if (root.transform.localPosition.sqrMagnitude > 0.0001f ||
                    Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > 0.01f)
                {
                    result.Problems.Add("корень префаба не в origin/identity — навмеш уедет");
                }

                int trisBefore = TriangulationCount();

                // 1. Настройки бейка и бейк в локальном пространстве корня.
                //    Корпусные коллайдеры лежат на Default, прежний mask 64 (ShipDeck) давал пустой навмеш.
                //    Смещение узла (0, -0.37, 0) от заглушки Platform ставило навмеш выше корпуса — обнуляем.
                deck.localPosition = Vector3.zero;

                var plan = (AnchorPlan)null;
                root.transform.localScale = Vector3.one;
                try
                {
                    var nmsSo = new SerializedObject(nms);
                    nmsSo.FindProperty("m_LayerMask").intValue = DeckLayerMask;
                    nmsSo.FindProperty("m_CollectObjects").intValue = (int)CollectObjects.All;
                    nmsSo.FindProperty("m_UseGeometry").intValue = (int)NavMeshCollectGeometry.PhysicsColliders;
                    nmsSo.ApplyModifiedPropertiesWithoutUndo();

                    baked = BuildLocalNavMesh(root, nms, authoredScale);
                    if (baked == null)
                    {
                        result.Problems.Add("бейк вернул null (нет коллайдеров под маску)");
                        return result;
                    }

                    result.NavBoundsLocal = baked.sourceBounds;

                    // 2. Временная регистрация — только для замеров и посадки точек экипажа.
                    instance = NavMesh.AddNavMeshData(baked, Vector3.zero, Quaternion.identity);
                    if (!instance.valid)
                    {
                        result.Problems.Add("навмеш не зарегистрировался для замеров");
                        return result;
                    }

                    result.Tris = Mathf.Max(0, TriangulationCount() - trisBefore);

                    plan = PlanAnchors(root, authoredScale, result);
                }
                finally
                {
                    root.transform.localScale = authoredScale;
                }

                if (plan == null) return result;
                if (!apply) return result;

                // 3. Запись: точки экипажа → ассет навмеша → префаб.
                ApplyAnchors(plan);
                SaveNavMeshAsset(baked, nms, deckNav, path, result);
                if (result.Problems.Count > 0) return result;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                result.Applied = true;
            }
            finally
            {
                root.transform.localScale = authoredScale;
                if (instance.valid) instance.Remove();
                if (baked != null && !AssetDatabase.Contains(baked)) UnityEngine.Object.DestroyImmediate(baked, true);
                PrefabUtility.UnloadPrefabContents(root);
            }

            return result;
        }

        /// <summary>Радиус SamplePosition в локальных единицах корня для дистанции в метрах мира.</summary>
        private static float LocalSampleRadius(float worldMeters, Vector3 scale, float min)
        {
            float sy = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
            return Mathf.Max(worldMeters / sy, min);
        }

        // ------------------------------------------------------------------ бейк

        /// <summary>
        /// Бейк в НЕСКАЛИРОВАННЫХ локальных единицах корня — именно так навмеш читает ShipDeckNav
        /// (nav = navFrameOrigin + InverseTransformPoint). Корень на время бейка в scale 1, поэтому
        /// размеры агента пересчитываются в локальные единицы по осям: без этого 0.5-метровый агент
        /// на палубе шириной 1.7 локальных единиц (Скат, Шмель) стирает навмеш целиком, а у Medium/HeavyII
        /// обрезает точки экипажа. agentTypeID не меняется, поэтому пути NPC того же Agent Type валидны.
        /// </summary>
        private static NavMeshData BuildLocalNavMesh(GameObject root, NavMeshSurface nms, Vector3 authoredScale)
        {
            var settings = nms.GetBuildSettings();
            float sx = Mathf.Max(Mathf.Abs(authoredScale.x), 0.0001f);
            float sy = Mathf.Max(Mathf.Abs(authoredScale.y), 0.0001f);
            float sz = Mathf.Max(Mathf.Abs(authoredScale.z), 0.0001f);
            float horizontal = Mathf.Max(sx, sz);

            settings.agentRadius = Mathf.Max(settings.agentRadius / horizontal, 0.01f);
            settings.agentHeight = Mathf.Max(settings.agentHeight / sy, 0.05f);
            settings.agentClimb = Mathf.Max(settings.agentClimb / sy, 0.005f);
            settings.voxelSize = Mathf.Max(settings.voxelSize / horizontal, 0.001f);
            settings.overrideVoxelSize = true;

            var sources = new List<NavMeshBuildSource>();
            Bounds bounds;
            if (!TryCollectSources(root, sources, out bounds)) return null;

            return NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Источники геометрии собираются из иерархии префаба вручную: NavMeshBuilder.CollectSources(Transform, …)
        /// на prefab contents возвращает 0 объектов, а вариант по Bounds сканирует открытую сцену.
        /// Корень во время бейка в origin со scale 1 → localToWorld и есть пространство бейка.
        /// </summary>
        private static bool TryCollectSources(GameObject root, List<NavMeshBuildSource> sources, out Bounds bounds)
        {
            sources.Clear();
            bounds = new Bounds();
            bool hasBounds = false;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || collider.isTrigger) continue;
                if ((DeckLayerMask & (1 << collider.gameObject.layer)) == 0) continue;

                var tr = collider.transform;
                var lossy = tr.lossyScale;
                var absScale = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));

                NavMeshBuildSource source;
                if (collider is BoxCollider box)
                {
                    source = new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Box,
                        size = Vector3.Scale(box.size, absScale),
                        transform = Matrix4x4.TRS(tr.TransformPoint(box.center), tr.rotation, Vector3.one),
                        area = 0,
                        component = box,
                    };
                }
                else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
                {
                    source = new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Mesh,
                        sourceObject = meshCollider.sharedMesh,
                        transform = tr.localToWorldMatrix,
                        area = 0,
                        component = meshCollider,
                    };
                }
                else
                {
                    continue; // sphere/capsule в этих кораблях не встречаются
                }

                sources.Add(source);
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (sources.Count == 0 || !hasBounds) return false;
            bounds.Expand(1f);
            return true;
        }

        // ------------------------------------------------------------------ точки экипажа

        /// <summary>
        /// Узел CrewAnchors — в точку PilotSeat по XZ, на высоту палубы под креслом;
        /// сами точки — по кольцу r = 0.5 м (мировых) на той же высоте. Имена точек не меняются.
        /// Расчёт идёт в локальном пространстве корня (scale 1), поэтому радиус переводится по осям.
        /// </summary>
        private static AnchorPlan PlanAnchors(GameObject root, Vector3 authoredScale, ShipResult result)
        {
            var pilot = root.transform.Find("PilotSeat");
            var crew = root.transform.Find("CrewAnchors");
            if (pilot == null)
            {
                result.Problems.Add("нет узла PilotSeat");
                return null;
            }
            if (crew == null)
            {
                result.Problems.Add("нет узла CrewAnchors (экипаж не заспавнится)");
                return null;
            }
            if (crew.childCount == 0)
            {
                result.Problems.Add("CrewAnchors без точек");
                return null;
            }

            float scaleX = Mathf.Max(Mathf.Abs(authoredScale.x), 0.0001f);
            float scaleZ = Mathf.Max(Mathf.Abs(authoredScale.z), 0.0001f);
            float scaleY = Mathf.Max(Mathf.Abs(authoredScale.y), 0.0001f);

            var pilotLocal = pilot.localPosition;
            NavMeshHit seatHit;
            if (!NavMesh.SamplePosition(pilotLocal, out seatHit, LocalSampleRadius(20f, authoredScale, 0.5f), NavMesh.AllAreas))
            {
                result.Problems.Add("под PilotSeat нет навмеша");
                return null;
            }

            result.PilotDeckDelta = (seatHit.position.y - pilotLocal.y) * scaleY;
            var seatOnDeckLocal = new Vector3(pilotLocal.x, seatHit.position.y, pilotLocal.z);

            var plan = new AnchorPlan { CrewAnchors = crew, CrewLocal = seatOnDeckLocal };

            var savedLocal = crew.localPosition;
            crew.localPosition = plan.CrewLocal;
            try
            {
                int n = crew.childCount;
                float maxRadius = 0f;
                float maxDelta = 0f;
                for (int i = 0; i < n; i++)
                {
                    var kid = crew.GetChild(i);
                    float angle = Mathf.PI * 2f * i / n;
                    var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                    // Кольцо задано в метрах, поэтому по осям корня радиус делится на масштаб.
                    var ringLocal = seatOnDeckLocal + new Vector3(
                        direction.x * AnchorRingRadius / scaleX,
                        0f,
                        direction.z * AnchorRingRadius / scaleZ);

                    var worldLocal = ringLocal;
                    NavMeshHit kidHit;
                    if (NavMesh.SamplePosition(ringLocal, out kidHit, LocalSampleRadius(0.6f, authoredScale, 0.05f), NavMesh.AllAreas))
                    {
                        maxDelta = Mathf.Max(maxDelta, Mathf.Abs(kidHit.position.y - ringLocal.y) * scaleY);
                        worldLocal.y = kidHit.position.y;
                    }
                    else
                    {
                        result.Problems.Add("точка " + kid.name + " не легла на навмеш");
                    }

                    maxRadius = Mathf.Max(maxRadius,
                        new Vector2((worldLocal.x - pilotLocal.x) * scaleX, (worldLocal.z - pilotLocal.z) * scaleZ).magnitude);

                    plan.Kids.Add(kid);
                    plan.KidLocal.Add(crew.InverseTransformPoint(root.transform.TransformPoint(worldLocal)));
                }

                result.AnchorCount = n;
                result.AnchorMaxRadius = maxRadius;
                result.AnchorMaxSampleDelta = maxDelta;
            }
            finally
            {
                crew.localPosition = savedLocal;
            }

            return plan;
        }

        private static void ApplyAnchors(AnchorPlan plan)
        {
            plan.CrewAnchors.localPosition = plan.CrewLocal;
            for (int i = 0; i < plan.Kids.Count; i++)
            {
                var kid = plan.Kids[i];
                if (kid == null) continue;
                kid.localPosition = plan.KidLocal[i];
            }
        }

        // ------------------------------------------------------------------ ассет навмеша

        private static void SaveNavMeshAsset(NavMeshData baked, NavMeshSurface nms,
            Component deckNav, string prefabPath, ShipResult result)
        {
            var assetPath = ResolveNavAssetPath(deckNav, prefabPath);
            result.NavAsset = assetPath;

            EnsureBackup(result.Name, prefabPath, assetPath);

            var folder = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(folder))
            {
                folder = folder.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder)) EnsureFolder(folder);
            }

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            baked.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(baked, assetPath);

            var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (asset == null)
            {
                result.Problems.Add("не удалось создать ассет навмеша: " + assetPath);
                return;
            }

            var deckNavSo = new SerializedObject(deckNav);
            var dataProp = deckNavSo.FindProperty("_deckNavMeshData");
            dataProp.objectReferenceValue = asset;
            deckNavSo.ApplyModifiedPropertiesWithoutUndo();

            var nmsSo = new SerializedObject(nms);
            nmsSo.FindProperty("m_NavMeshData").objectReferenceValue = asset;
            nmsSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ResolveNavAssetPath(Component deckNav, string prefabPath)
        {
            if (deckNav != null)
            {
                var so = new SerializedObject(deckNav);
                var prop = so.FindProperty("_deckNavMeshData");
                var current = prop != null ? prop.objectReferenceValue : null;
                if (current != null)
                {
                    var existing = AssetDatabase.GetAssetPath(current);
                    if (!string.IsNullOrEmpty(existing)) return existing;
                }
            }

            var name = Path.GetFileNameWithoutExtension(prefabPath);
            return ShipsDir + "/" + name + "/NavMesh-DeckNavSurface.asset";
        }

        // ------------------------------------------------------------------ бэкап

        private static void EnsureBackup(string shipName, string prefabPath, string navAssetPath)
        {
            try
            {
                Directory.CreateDirectory(BackupDir);

                var prefabBackup = Path.Combine(BackupDir, shipName + ".prefab");
                if (!File.Exists(prefabBackup)) File.Copy(prefabPath, prefabBackup, true);

                var navBackupDir = Path.Combine(BackupDir, shipName);
                Directory.CreateDirectory(navBackupDir);
                var navBackup = Path.Combine(navBackupDir, Path.GetFileName(navAssetPath));
                if (File.Exists(navAssetPath) && !File.Exists(navBackup)) File.Copy(navAssetPath, navBackup, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NPC DeckNav] Бэкап " + shipName + " не сделан: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ утилиты

        private static int TriangulationCount()
        {
            try
            {
                var tri = NavMesh.CalculateTriangulation();
                return tri.indices.Length / 3;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string ReadClassName(GameObject root)
        {
            var controller = root.GetComponent<ProjectC.Player.ShipController>();
            if (controller == null) return "?";
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("shipFlightClass");
            if (prop == null) return "?";
            if (prop.propertyType == SerializedPropertyType.Enum &&
                prop.enumNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length)
            {
                return prop.enumNames[prop.enumValueIndex];
            }
            return prop.intValue.ToString(CultureInfo.InvariantCulture);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent.Replace('\\', '/'));
            var folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.CreateFolder(parent?.Replace('\\', '/') ?? "Assets", folder);
        }

        private static string AppendReport(string title, string body)
        {
            if (!File.Exists(ReportPath)) return body;

            var existing = File.ReadAllText(ReportPath);
            var marker = "<!-- " + title + " -->";
            var index = existing.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0) existing = existing.Substring(0, index);
            return existing + "\n" + marker + "\n" + body;
        }

        private static void WriteReport(string content)
        {
            try
            {
                var directory = Path.GetDirectoryName(ReportPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(ReportPath, content, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NPC DeckNav] Не удалось записать отчёт: " + ex);
            }
        }
    }
}
#endif
