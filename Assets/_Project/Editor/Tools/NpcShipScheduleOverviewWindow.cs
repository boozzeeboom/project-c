#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Stations;
using ProjectC.Docking.Network; // DockStationController for location dropdowns

namespace ProjectC.PeacefulShip.EditorTools
{
    /// <summary>
    /// EditorWindow for overview of all NpcShipSchedule SOs, their routes,
    /// ship assignments in WorldScene_* files, and cargo trade configuration.
    /// </summary>
    public class NpcShipScheduleOverviewWindow : EditorWindow
    {
        private enum Tab { Schedules, ShipsInScenes, CargoTrade }
        private Tab _currentTab = Tab.Schedules;

        // ── Tab 1: Schedules ──
        private NpcShipSchedule[] _allSchedules;
        private Vector2 _scrollSchedules;
        private int _expandedScheduleIndex = -1;

        // ── Tab 2: Ships in Scenes ──
        private Vector2 _scrollShips;
        private bool _scanInProgress;
        private readonly List<SceneShipEntry> _sceneShipEntries = new List<SceneShipEntry>();
        private string[] _worldScenePaths;

        // ── Tab 3: Cargo Trade ──
        private Vector2 _scrollCargo;

        [MenuItem("Tools/Project C/NPC Ship Schedule Overview")]
        public static void Open()
        {
            var window = GetWindow<NpcShipScheduleOverviewWindow>("NPC Schedule Overview");
            window.minSize = new Vector2(780, 480);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSchedules();
            CacheWorldScenePaths();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            // Toolbar tabs
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab,
                new[] { "📋 All Schedules", "🚢 Ships in Scenes", "📦 Cargo Trade" },
                GUILayout.Height(28));

            EditorGUILayout.Space(8);

            switch (_currentTab)
            {
                case Tab.Schedules: DrawSchedulesTab(); break;
                case Tab.ShipsInScenes: DrawShipsInScenesTab(); break;
                case Tab.CargoTrade: DrawCargoTradeTab(); break;
            }
        }

        // ════════════════════════════════════════════════
        //  TAB 1: All Schedules
        // ════════════════════════════════════════════════

        private void DrawSchedulesTab()
        {
            EditorGUILayout.LabelField($"Schedules ({(_allSchedules?.Length ?? 0)} total)", EditorStyles.boldLabel);

            if (_allSchedules == null || _allSchedules.Length == 0)
            {
                EditorGUILayout.HelpBox("No NpcShipSchedule assets found in the project.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);

            // Header
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Schedule ID", EditorStyles.toolbarButton, GUILayout.Width(180));
                GUILayout.Label("Display Name", EditorStyles.toolbarButton, GUILayout.Width(220));
                GUILayout.Label("Type", EditorStyles.toolbarButton, GUILayout.Width(80));
                GUILayout.Label("Routes", EditorStyles.toolbarButton, GUILayout.Width(60));
                GUILayout.Label("Cargo", EditorStyles.toolbarButton, GUILayout.Width(60));
                GUILayout.Label("Interval (s)", EditorStyles.toolbarButton, GUILayout.Width(90));
                GUILayout.FlexibleSpace();
            }

            _scrollSchedules = EditorGUILayout.BeginScrollView(_scrollSchedules);

            for (int si = 0; si < _allSchedules.Length; si++)
            {
                var sch = _allSchedules[si];
                if (sch == null) continue;

                bool isExpanded = _expandedScheduleIndex == si;
                int routeCount = sch.routes?.Length ?? 0;
                int cargoCount = sch.cargoTrade?.buyItems?.Length ?? 0;
                bool hasCargo = cargoCount > 0;

                // ── Summary row ──
                var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
                bool isEven = si % 2 == 0;
                if (isEven)
                {
                    var bg = new Color(0.25f, 0.25f, 0.28f, 0.6f);
                    EditorGUI.DrawRect(rowRect, bg);
                }

                // Expand/collapse toggle
                bool newExpanded = GUILayout.Toggle(isExpanded, isExpanded ? "▼" : "▶", EditorStyles.label, GUILayout.Width(18));
                if (newExpanded != isExpanded)
                    _expandedScheduleIndex = newExpanded ? si : -1;

                if (GUILayout.Button(sch.scheduleId, EditorStyles.linkLabel, GUILayout.Width(160)))
                {
                    Selection.activeObject = sch;
                    EditorGUIUtility.PingObject(sch);
                }

                GUILayout.Label(sch.displayName, GUILayout.Width(220));
                GUILayout.Label(sch.scheduleType.ToString(), GUILayout.Width(80));

                var routeColor = routeCount > 0 ? GUI.color : Color.yellow;
                GUI.color = routeColor;
                GUILayout.Label(routeCount.ToString(), GUILayout.Width(60));
                GUI.color = Color.white;

                var cargoColor = hasCargo ? Color.green : Color.gray;
                GUI.color = cargoColor;
                GUILayout.Label(hasCargo ? $"{cargoCount} items" : "—", GUILayout.Width(60));
                GUI.color = Color.white;

                GUILayout.Label($"{sch.meanArrivalIntervalSec:F0}±{sch.arrivalIntervalStdDev:F0}", GUILayout.Width(90));

                string routeSummary = routeCount > 0
                    ? string.Join(", ", sch.routes.Take(2).Select(r => $"{r.fromLocationId}→{r.toLocationId}"))
                      + (routeCount > 2 ? " ..." : "")
                    : "(empty)";
                GUILayout.Label(routeSummary, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();

                // ── Expanded inline editor ──
                if (isExpanded)
                {
                    DrawScheduleInlineEditor(sch);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("🔄 Refresh Schedules", GUILayout.Width(160)))
            {
                _expandedScheduleIndex = -1;
                RefreshSchedules();
            }
        }

        private void DrawScheduleInlineEditor(NpcShipSchedule sch)
        {
            var so = new SerializedObject(sch);
            so.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            // ── Toolbar with Scan button (like NpcShipScheduleEditor) ──
            var stations = GetCachedSceneStations();
            int stationCount = stations.Count;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("🔍 Scan Scene Stations", EditorStyles.toolbarButton, GUILayout.Width(140)))
            {
                RefreshSceneStationCache();
                Repaint();
            }
            if (stationCount <= 0)
                EditorGUILayout.LabelField("No stations in open scenes — type IDs manually", EditorStyles.miniLabel);
            else
            {
                var locIds = stations.Select(s => s.LocationId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                EditorGUILayout.LabelField($"📍 {stationCount} stations: {string.Join(", ", locIds.Take(5))}{(locIds.Count > 5 ? " ..." : "")}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Identity ──
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            var propId = so.FindProperty("scheduleId");
            var propName = so.FindProperty("displayName");
            var propType = so.FindProperty("scheduleType");

            EditorGUILayout.PropertyField(propId, new GUIContent("Schedule ID"));
            EditorGUILayout.PropertyField(propName, new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(propType, new GUIContent("Schedule Type"));

            EditorGUILayout.Space(6);

            // ── Traffic Shaping ──
            EditorGUILayout.LabelField("Traffic Shaping (Gaussian)", EditorStyles.boldLabel);
            var propMean = so.FindProperty("meanArrivalIntervalSec");
            var propStdDev = so.FindProperty("arrivalIntervalStdDev");
            var propMinSpacing = so.FindProperty("minArrivalSpacingSec");
            var propMinDwell = so.FindProperty("minDwellTimeSec");
            var propMaxDwell = so.FindProperty("maxDwellTimeSec");

            EditorGUILayout.PropertyField(propMean, new GUIContent("Mean Interval (sec)"));
            EditorGUILayout.PropertyField(propStdDev, new GUIContent("StdDev (sec)"));
            EditorGUILayout.PropertyField(propMinSpacing, new GUIContent("Min Spacing (sec)"));
            EditorGUILayout.PropertyField(propMinDwell, new GUIContent("Min Dwell (sec)"));
            EditorGUILayout.PropertyField(propMaxDwell, new GUIContent("Max Dwell (sec)"));

            EditorGUILayout.Space(6);

            // ── Routes (uses NpcShipRouteDrawer for location dropdowns) ──
            EditorGUILayout.LabelField("Routes", EditorStyles.boldLabel);
            var propRoutes = so.FindProperty("routes");

            if (propRoutes == null || !propRoutes.isArray)
            {
                EditorGUILayout.HelpBox("routes property not found.", MessageType.Error);
            }
            else
            {
                bool routesChanged = false;

                for (int ri = 0; ri < propRoutes.arraySize; ri++)
                {
                    var routeElem = propRoutes.GetArrayElementAtIndex(ri);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Use PropertyField with includeChildren=true → invokes NpcShipRouteDrawer
                    // which provides dropdown popups for fromLocationId/toLocationId from DockStationController
                    EditorGUILayout.PropertyField(routeElem,
                        new GUIContent($"Route [{ri}]"), true);

                    if (routeElem.isExpanded)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Remove Route", GUILayout.Width(110)))
                            {
                                propRoutes.DeleteArrayElementAtIndex(ri);
                                routesChanged = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Show compact summary when collapsed
                        var rFrom = routeElem.FindPropertyRelative("fromLocationId");
                        var rTo = routeElem.FindPropertyRelative("toLocationId");
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(20);
                            EditorGUILayout.LabelField(
                                $"{(string.IsNullOrEmpty(rFrom?.stringValue) ? "?" : rFrom.stringValue)} → {(string.IsNullOrEmpty(rTo?.stringValue) ? "?" : rTo.stringValue)}",
                                EditorStyles.miniLabel);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(56)))
                            {
                                propRoutes.DeleteArrayElementAtIndex(ri);
                                routesChanged = true;
                                break;
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                if (!routesChanged)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("+ Add Route", GUILayout.Width(120)))
                    {
                        propRoutes.InsertArrayElementAtIndex(propRoutes.arraySize);
                        var newElem = propRoutes.GetArrayElementAtIndex(propRoutes.arraySize - 1);
                        newElem.FindPropertyRelative("dwellTimeSec").floatValue = 60f;
                        newElem.FindPropertyRelative("dwellRandomAddMinSec").floatValue = 0f;
                        newElem.FindPropertyRelative("dwellRandomAddMaxSec").floatValue = 0f;
                        newElem.FindPropertyRelative("flightDurationSec").floatValue = 120f;
                        newElem.isExpanded = true;
                    }
                }

                // Route-level validation (reuse logic from NpcShipScheduleEditor)
                if (stationCount > 0 && propRoutes.arraySize > 0)
                {
                    var validIds = new HashSet<string>(
                        stations.Select(s => s.LocationId).Where(id => !string.IsNullOrEmpty(id)));
                    for (int i = 0; i < propRoutes.arraySize; i++)
                    {
                        var el = propRoutes.GetArrayElementAtIndex(i);
                        var from = el.FindPropertyRelative("fromLocationId");
                        var to = el.FindPropertyRelative("toLocationId");
                        if (!string.IsNullOrEmpty(from?.stringValue) && !validIds.Contains(from.stringValue))
                            EditorGUILayout.HelpBox($"Route[{i}].fromLocationId '{from.stringValue}' — no station in scene", MessageType.Warning);
                        if (!string.IsNullOrEmpty(to?.stringValue) && !validIds.Contains(to.stringValue))
                            EditorGUILayout.HelpBox($"Route[{i}].toLocationId '{to.stringValue}' — no station in scene", MessageType.Warning);
                    }
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(sch);
            }

            so.Dispose();
        }

        // ── Scene station cache (shared by inline editor — mirrors NpcShipScheduleEditor + NpcShipRouteDrawer) ──

        private static List<DockStationController> _cachedSceneStations;
        private static double _sceneStationCacheTime;

        private static List<DockStationController> GetCachedSceneStations()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_cachedSceneStations != null && (now - _sceneStationCacheTime) < 3.0)
                return _cachedSceneStations;
            return _cachedSceneStations ?? new List<DockStationController>();
        }

        private static void RefreshSceneStationCache()
        {
            _cachedSceneStations = FindObjectsByType<DockStationController>(FindObjectsInactive.Include).ToList();
            _sceneStationCacheTime = EditorApplication.timeSinceStartup;
        }

        private void RefreshSchedules()
        {
            var guids = AssetDatabase.FindAssets("t:NpcShipSchedule");
            var list = new List<NpcShipSchedule>(guids.Length);

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sch = AssetDatabase.LoadAssetAtPath<NpcShipSchedule>(path);
                if (sch != null)
                    list.Add(sch);
            }

            _allSchedules = list.ToArray();
            Array.Sort(_allSchedules, (a, b) => string.CompareOrdinal(a.scheduleId, b.scheduleId));
            Debug.Log($"[NpcShipScheduleOverview] Loaded {_allSchedules.Length} schedules project-wide");
        }

        // ════════════════════════════════════════════════
        //  TAB 2: Ships in Scenes
        // ════════════════════════════════════════════════

        private void DrawShipsInScenesTab()
        {
            EditorGUILayout.LabelField("Ships in World Scenes", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(_scanInProgress);
                if (GUILayout.Button("🔍 Scan All World Scenes", GUILayout.Width(200), GUILayout.Height(26)))
                {
                    ScanAllWorldScenes();
                }
                EditorGUI.EndDisabledGroup();

                if (_scanInProgress)
                {
                    GUILayout.Label("Scanning...", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear Results", GUILayout.Width(120)))
                {
                    _sceneShipEntries.Clear();
                }
            }

            EditorGUILayout.Space(6);

            if (_sceneShipEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No scan results yet. Click 'Scan All World Scenes' to search NpcShipController in all WorldScene_* files.\n\n" +
                    "Note: scenes will be additively loaded one at a time (may take a few seconds for 25 scenes).",
                    MessageType.Info);
                return;
            }

            // Build schedule name list for popups: [0] = "(none)", rest = schedules
            var scheduleNames = new List<string> { "(none)" };
            if (_allSchedules != null)
            {
                foreach (var s in _allSchedules)
                    if (s != null)
                        scheduleNames.Add($"{s.scheduleId} — {s.displayName}");
            }

            // Group by scene
            var grouped = _sceneShipEntries.GroupBy(e => e.SceneName).OrderBy(g => g.Key);

            EditorGUILayout.LabelField($"Found {_sceneShipEntries.Count} NPC ships in {grouped.Count()} scenes", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            _scrollShips = EditorGUILayout.BeginScrollView(_scrollShips);

            foreach (var group in grouped)
            {
                string sceneName = group.Key;
                var ships = group.ToList();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"📍 {sceneName} — {ships.Count} NPC ship(s)", EditorStyles.boldLabel);

                foreach (var entry in ships)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Row 1: ship name + current schedule
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool hasSchedule = !string.IsNullOrEmpty(entry.ScheduleId);
                        var color = hasSchedule ? Color.white : Color.red;
                        GUI.color = color;
                        EditorGUILayout.LabelField("•", GUILayout.Width(14));
                        EditorGUILayout.LabelField(entry.ShipName, EditorStyles.boldLabel, GUILayout.Width(180));
                        GUI.color = Color.white;

                        if (hasSchedule)
                        {
                            EditorGUILayout.LabelField(entry.ScheduleId, GUILayout.Width(160));
                            EditorGUILayout.LabelField($"\"{entry.ScheduleDisplayName}\"", EditorStyles.miniLabel);

                            int routeCount = entry.RouteCount;
                            string routeInfo = routeCount > 0
                                ? $"{routeCount} route(s): " + entry.RouteSummary
                                : "(no routes)";
                            EditorGUILayout.LabelField(routeInfo, EditorStyles.miniLabel);
                        }
                        else
                        {
                            GUI.color = Color.red;
                            EditorGUILayout.LabelField("⚠ NO SCHEDULE ASSIGNED!", EditorStyles.boldLabel);
                            GUI.color = Color.white;
                        }
                    }

                    // Row 2: assign schedule popup + button
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);

                        // Determine current popup index
                        int currentIdx = 0; // "(none)"
                        if (!string.IsNullOrEmpty(entry.ScheduleId) && _allSchedules != null)
                        {
                            for (int i = 0; i < _allSchedules.Length; i++)
                            {
                                if (_allSchedules[i] != null && _allSchedules[i].scheduleId == entry.ScheduleId)
                                {
                                    currentIdx = i + 1; // +1 because [0] is "(none)"
                                    break;
                                }
                            }
                        }

                        EditorGUILayout.LabelField("Assign:", GUILayout.Width(48));
                        int newIdx = EditorGUILayout.Popup(currentIdx, scheduleNames.ToArray(), GUILayout.Width(320));

                        if (newIdx != currentIdx)
                        {
                            NpcShipSchedule newSch = null;
                            if (newIdx > 0 && _allSchedules != null && newIdx - 1 < _allSchedules.Length)
                                newSch = _allSchedules[newIdx - 1];

                            AssignScheduleToShip(entry, newSch);

                            // Update entry
                            entry.ScheduleId = newSch?.scheduleId;
                            entry.ScheduleDisplayName = newSch?.displayName ?? "";
                            entry.RouteCount = newSch?.routes?.Length ?? 0;
                            entry.RouteSummary = newSch != null && newSch.routes != null && newSch.routes.Length > 0
                                ? string.Join(", ", newSch.routes.Take(2).Select(r => $"{r.fromLocationId}→{r.toLocationId}"))
                                  + (newSch.routes.Length > 2 ? " ..." : "")
                                : "";
                        }

                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(1);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        private void AssignScheduleToShip(SceneShipEntry entry, NpcShipSchedule newSchedule)
        {
            try
            {
                // Open scene additively
                Scene scene = EditorSceneManager.OpenScene(entry.ScenePath, OpenSceneMode.Additive);

                // Find controller by ship name
                var controllers = FindObjectsByType<NpcShipController>(FindObjectsInactive.Include);
                NpcShipController target = null;
                foreach (var ctrl in controllers)
                {
                    if (ctrl.gameObject.scene == scene && ctrl.gameObject.name == entry.ShipName)
                    {
                        target = ctrl;
                        break;
                    }
                }

                if (target == null)
                {
                    Debug.LogWarning($"[NpcShipScheduleOverview] Ship '{entry.ShipName}' not found in scene '{entry.SceneName}'");
                    EditorSceneManager.CloseScene(scene, true);
                    return;
                }

                // Assign schedule via SerializedObject
                var so = new SerializedObject(target);
                so.Update();
                var propSchedule = so.FindProperty("schedule");
                if (propSchedule != null)
                {
                    propSchedule.objectReferenceValue = newSchedule;
                    so.ApplyModifiedProperties();
                }
                so.Dispose();

                // Mark scene dirty
                EditorSceneManager.MarkSceneDirty(scene);

                // Save scene
                EditorSceneManager.SaveScene(scene, entry.ScenePath);

                // Close scene
                EditorSceneManager.CloseScene(scene, true);

                string schName = newSchedule != null ? $"'{newSchedule.scheduleId}'" : "null";
                Debug.Log($"[NpcShipScheduleOverview] Assigned schedule {schName} to '{entry.ShipName}' in '{entry.SceneName}'");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcShipScheduleOverview] Failed to assign schedule to '{entry.ShipName}': {ex.Message}");
            }
        }

        private void CacheWorldScenePaths()
        {
            var guids = AssetDatabase.FindAssets("WorldScene_ t:Scene", new[] { "Assets/_Project/Scenes/World" });
            _worldScenePaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .ToArray();
        }

        private void ScanAllWorldScenes()
        {
            if (_scanInProgress) return;
            _scanInProgress = true;
            _sceneShipEntries.Clear();

            try
            {
                CacheWorldScenePaths();
                string currentScenePath = SceneManager.GetActiveScene().path;

                for (int i = 0; i < _worldScenePaths.Length; i++)
                {
                    string scenePath = _worldScenePaths[i];
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                    EditorUtility.DisplayProgressBar(
                        "Scanning NPC Ships",
                        $"Scene {i + 1}/{_worldScenePaths.Length}: {sceneName}",
                        (float)i / _worldScenePaths.Length);

                    try
                    {
                        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                        var controllers = FindObjectsByType<NpcShipController>(FindObjectsInactive.Include);

                        foreach (var ctrl in controllers)
                        {
                            if (ctrl.gameObject.scene != scene) continue;

                            var schedule = ctrl.Schedule;
                            _sceneShipEntries.Add(new SceneShipEntry
                            {
                                SceneName = sceneName,
                                ScenePath = scenePath,
                                ShipName = ctrl.gameObject.name,
                                ScheduleId = schedule?.scheduleId,
                                ScheduleDisplayName = schedule?.displayName ?? "",
                                RouteCount = schedule?.routes?.Length ?? 0,
                                RouteSummary = schedule != null && schedule.routes != null && schedule.routes.Length > 0
                                    ? string.Join(", ",
                                        schedule.routes.Take(2).Select(r => $"{r.fromLocationId}→{r.toLocationId}"))
                                      + (schedule.routes.Length > 2 ? " ..." : "")
                                    : ""
                            });
                        }

                        EditorSceneManager.CloseScene(scene, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NpcShipScheduleOverview] Failed to scan scene {sceneName}: {ex.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _scanInProgress = false;
                Repaint();

                Debug.Log($"[NpcShipScheduleOverview] Scan complete: {_sceneShipEntries.Count} NPC ships in {_worldScenePaths.Length} scenes");
            }
        }

        // ════════════════════════════════════════════════
        //  TAB 3: Cargo Trade
        // ════════════════════════════════════════════════

        private void DrawCargoTradeTab()
        {
            EditorGUILayout.LabelField("Cargo Trade Configuration", EditorStyles.boldLabel);

            if (_allSchedules == null || _allSchedules.Length == 0)
            {
                EditorGUILayout.HelpBox("No schedules loaded. Click 'Refresh Schedules' in the All Schedules tab.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);

            _scrollCargo = EditorGUILayout.BeginScrollView(_scrollCargo);

            foreach (var sch in _allSchedules)
            {
                if (sch == null) continue;

                var so = new SerializedObject(sch);
                so.Update();

                var propCargo = so.FindProperty("cargoTrade");
                if (propCargo == null)
                {
                    so.Dispose();
                    continue;
                }

                var propBuyItems = propCargo.FindPropertyRelative("buyItems");
                bool hasCargo = propBuyItems != null && propBuyItems.isArray && propBuyItems.arraySize > 0;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"📦 {sch.scheduleId}", EditorStyles.boldLabel, GUILayout.Width(170));
                    EditorGUILayout.LabelField(sch.displayName, EditorStyles.miniLabel, GUILayout.Width(200));

                    if (hasCargo)
                    {
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("ACTIVE", EditorStyles.boldLabel, GUILayout.Width(60));
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("NO CARGO", EditorStyles.miniLabel, GUILayout.Width(70));
                        GUI.color = Color.white;
                    }
                }

                EditorGUI.indentLevel++;

                // Behavior flags (editable toggles)
                var propSellAll = propCargo.FindPropertyRelative("sellAllOnArrival");
                var propBuyAfter = propCargo.FindPropertyRelative("buyConfiguredItemsAfterSell");
                var propUnlimited = propCargo.FindPropertyRelative("useUnlimitedCredits");

                EditorGUILayout.BeginHorizontal();
                if (propSellAll != null) EditorGUILayout.PropertyField(propSellAll, new GUIContent("Sell on arrival"), GUILayout.Width(160));
                if (propBuyAfter != null) EditorGUILayout.PropertyField(propBuyAfter, new GUIContent("Buy after sell"), GUILayout.Width(160));
                if (propUnlimited != null) EditorGUILayout.PropertyField(propUnlimited, new GUIContent("Unlimited credits"), GUILayout.Width(160));
                EditorGUILayout.EndHorizontal();

                // Limits
                var propSlots = propCargo.FindPropertyRelative("maxLoadSlots");
                var propWeight = propCargo.FindPropertyRelative("maxLoadWeightKg");

                EditorGUILayout.BeginHorizontal();
                if (propSlots != null) EditorGUILayout.PropertyField(propSlots, new GUIContent("Max Slots"), GUILayout.Width(120));
                if (propWeight != null) EditorGUILayout.PropertyField(propWeight, new GUIContent("Max Weight (kg)"), GUILayout.Width(160));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Buy items
                if (propBuyItems != null && propBuyItems.isArray)
                {
                    EditorGUILayout.LabelField($"Buy Items ({propBuyItems.arraySize}):", EditorStyles.miniBoldLabel);

                    // Table header
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        GUILayout.Label("Item ID", EditorStyles.toolbarButton, GUILayout.Width(200));
                        GUILayout.Label("Qty", EditorStyles.toolbarButton, GUILayout.Width(50));
                        GUILayout.Label("Sell?", EditorStyles.toolbarButton, GUILayout.Width(50));
                        GUILayout.Label("Keep", EditorStyles.toolbarButton, GUILayout.Width(50));
                        GUILayout.FlexibleSpace();
                    }

                    bool itemsModified = false;
                    for (int i = 0; i < propBuyItems.arraySize; i++)
                    {
                        var itemElem = propBuyItems.GetArrayElementAtIndex(i);
                        var propItemId = itemElem.FindPropertyRelative("itemId");
                        var propQty = itemElem.FindPropertyRelative("desiredQuantity");
                        var propSell = itemElem.FindPropertyRelative("sellOnArrival");
                        var propKeep = itemElem.FindPropertyRelative("maxKeepQuantity");

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(propItemId, GUIContent.none, GUILayout.Width(200));
                            if (propQty != null) EditorGUILayout.PropertyField(propQty, GUIContent.none, GUILayout.Width(50));
                            if (propSell != null) EditorGUILayout.PropertyField(propSell, GUIContent.none, GUILayout.Width(50));
                            if (propKeep != null) EditorGUILayout.PropertyField(propKeep, GUIContent.none, GUILayout.Width(50));

                            if (GUILayout.Button("✕", GUILayout.Width(24)))
                            {
                                propBuyItems.DeleteArrayElementAtIndex(i);
                                itemsModified = true;
                                break;
                            }
                        }
                    }

                    if (!itemsModified)
                    {
                        EditorGUILayout.Space(2);
                        if (GUILayout.Button("+ Add Buy Item", GUILayout.Width(120)))
                        {
                            propBuyItems.InsertArrayElementAtIndex(propBuyItems.arraySize);
                            var newItem = propBuyItems.GetArrayElementAtIndex(propBuyItems.arraySize - 1);
                            var niId = newItem.FindPropertyRelative("itemId");
                            var niQty = newItem.FindPropertyRelative("desiredQuantity");
                            var niSell = newItem.FindPropertyRelative("sellOnArrival");
                            var niKeep = newItem.FindPropertyRelative("maxKeepQuantity");
                            if (niId != null) niId.stringValue = "";
                            if (niQty != null) niQty.intValue = 1;
                            if (niSell != null) niSell.boolValue = true;
                            if (niKeep != null) niKeep.intValue = 0;
                        }
                    }
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);

                if (so.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(sch);
                }

                so.Dispose();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("🔄 Refresh", GUILayout.Width(120)))
            {
                RefreshSchedules();
            }
        }

        // ════════════════════════════════════════════════
        //  Data types
        // ════════════════════════════════════════════════

        [Serializable]
        private class SceneShipEntry
        {
            public string SceneName;
            public string ScenePath;
            public string ShipName;
            public string ScheduleId;
            public string ScheduleDisplayName;
            public int RouteCount;
            public string RouteSummary;
            [NonSerialized] public int SelectedSchedulePopupIndex;
        }
    }
}
#endif
