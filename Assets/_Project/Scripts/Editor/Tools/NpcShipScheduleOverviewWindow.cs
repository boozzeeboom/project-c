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

        // ── Tab 2: Ships in Scenes ──
        private Vector2 _scrollShips;
        private bool _scanInProgress;
        private readonly List<SceneShipEntry> _sceneShipEntries = new List<SceneShipEntry>();
        private string[] _worldScenePaths;

        // ── Tab 3: Cargo Trade ──
        private Vector2 _scrollCargo;

        [MenuItem("Window/ProjectC/NPC Ship Schedule Overview")]
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
                EditorGUILayout.HelpBox("No NpcShipSchedule found in Resources/PeacefulShip/", MessageType.Warning);
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

            foreach (var sch in _allSchedules)
            {
                if (sch == null) continue;

                int routeCount = sch.routes?.Length ?? 0;
                int cargoCount = sch.cargoTrade?.buyItems?.Length ?? 0;
                bool hasCargo = cargoCount > 0;

                var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
                bool isEven = Array.IndexOf(_allSchedules, sch) % 2 == 0;
                if (isEven)
                {
                    var bg = new Color(0.25f, 0.25f, 0.28f, 0.6f);
                    EditorGUI.DrawRect(rowRect, bg);
                }

                if (GUILayout.Button(sch.scheduleId, EditorStyles.linkLabel, GUILayout.Width(180)))
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

                // Mini route summary
                string routeSummary = routeCount > 0
                    ? string.Join(", ", sch.routes.Take(2).Select(r => $"{r.fromLocationId}→{r.toLocationId}"))
                      + (routeCount > 2 ? " ..." : "")
                    : "(empty)";
                GUILayout.Label(routeSummary, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // Refresh button
            EditorGUILayout.Space(8);
            if (GUILayout.Button("🔄 Refresh Schedules", GUILayout.Width(160)))
            {
                RefreshSchedules();
            }
        }

        private void RefreshSchedules()
        {
            _allSchedules = Resources.LoadAll<NpcShipSchedule>("PeacefulShip");
            Array.Sort(_allSchedules, (a, b) => string.CompareOrdinal(a.scheduleId, b.scheduleId));
            Debug.Log($"[NpcShipScheduleOverview] Loaded {_allSchedules.Length} schedules from Resources/PeacefulShip/");
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
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.indentLevel++;

                    bool hasSchedule = !string.IsNullOrEmpty(entry.ScheduleId);
                    var color = hasSchedule ? Color.white : Color.red;
                    GUI.color = color;
                    EditorGUILayout.LabelField("•", GUILayout.Width(14));
                    EditorGUILayout.LabelField(entry.ShipName, GUILayout.Width(180));
                    GUI.color = Color.white;

                    if (hasSchedule)
                    {
                        EditorGUILayout.LabelField(entry.ScheduleId, EditorStyles.boldLabel, GUILayout.Width(160));
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

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
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

                        // Find all NpcShipController in this scene
                        var controllers = FindObjectsByType<NpcShipController>(FindObjectsInactive.Include);

                        // Filter to only this scene's objects
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

                // Re-open original scene
                if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                {
                    // Keep current scene, just ensure we cleaned up additive ones
                }

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
                EditorGUILayout.HelpBox("No schedules loaded. Check Resources/PeacefulShip/.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);

            _scrollCargo = EditorGUILayout.BeginScrollView(_scrollCargo);

            foreach (var sch in _allSchedules)
            {
                if (sch == null) continue;

                var cargo = sch.cargoTrade;
                bool hasCargo = cargo != null && cargo.buyItems != null && cargo.buyItems.Length > 0;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header row
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

                if (hasCargo)
                {
                    EditorGUI.indentLevel++;

                    // Behavior flags
                    EditorGUILayout.BeginHorizontal();
                    DrawFlag(cargo.sellAllOnArrival, "Sell all on arrival");
                    DrawFlag(cargo.buyConfiguredItemsAfterSell, "Buy after sell");
                    DrawFlag(cargo.useUnlimitedCredits, "Unlimited credits");
                    EditorGUILayout.EndHorizontal();

                    // Limits
                    EditorGUILayout.LabelField(
                        $"Max load: {cargo.maxLoadSlots} slots | {cargo.maxLoadWeightKg:F0} kg",
                        EditorStyles.miniLabel);

                    // Buy items table
                    EditorGUILayout.LabelField($"Buy Items ({cargo.buyItems.Length}):", EditorStyles.miniBoldLabel);

                    // Header
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        GUILayout.Label("Item ID", EditorStyles.toolbarButton, GUILayout.Width(220));
                        GUILayout.Label("Qty", EditorStyles.toolbarButton, GUILayout.Width(50));
                        GUILayout.Label("Sell?", EditorStyles.toolbarButton, GUILayout.Width(40));
                        GUILayout.Label("Keep", EditorStyles.toolbarButton, GUILayout.Width(40));
                    }

                    foreach (var item in cargo.buyItems)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(item.itemId ?? "(empty)", GUILayout.Width(220));
                            EditorGUILayout.LabelField(item.desiredQuantity.ToString(), GUILayout.Width(50));
                            GUI.color = item.sellOnArrival ? Color.green : Color.gray;
                            EditorGUILayout.LabelField(item.sellOnArrival ? "✓" : "✗", GUILayout.Width(40));
                            GUI.color = Color.white;
                            EditorGUILayout.LabelField(item.maxKeepQuantity.ToString(), GUILayout.Width(40));
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("🔄 Refresh", GUILayout.Width(120)))
            {
                RefreshSchedules();
            }
        }

        private static void DrawFlag(bool value, string label)
        {
            GUI.color = value ? Color.green : Color.gray;
            EditorGUILayout.LabelField($"{(value ? "✓" : "✗")} {label}", EditorStyles.miniLabel, GUILayout.Width(160));
            GUI.color = Color.white;
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
        }
    }
}
#endif
