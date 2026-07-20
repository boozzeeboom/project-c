// NpcWorldInspectorWindow — unified NPC inspector across all WorldScene_* files.
// Scans scenes additively, finds all 4 NPC types, shows connections (quests, faction, ship, market, spawner).
// Menu: Tools → Project C → NPC World Inspector.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.Quests;
using ProjectC.Factions;
using ProjectC.AI;
using ProjectC.Combat;
using ProjectC.PeacefulShip.Stations;
using ProjectC.PeacefulShip.Core;
using ProjectC.Trade.Network;

namespace ProjectC.Editor.Tools
{
    /// <summary>
    /// Unified NPC inspector: scan all WorldScene_* files, find all NPC types,
    /// show relationships (quests, faction, ship, spawner, market).
    /// </summary>
    public class NpcWorldInspectorWindow : EditorWindow
    {
        private enum Tab { SceneNpcs, QuestDbCrossRef, Factions }
        private Tab _currentTab = Tab.SceneNpcs;

        // ── Scan state ──
        private SceneNpcScanResult _scanResult;
        private bool _scanInProgress;
        private string[] _worldScenePaths;

        // ── Quest DB ──
        private QuestDatabase _questDb;
        private bool _questDbLoaded;

        // ── Faction state ──
        private FactionScanResult _factionResult;
        private int _selectedFactionIndex = -1;
        private Vector2 _scrollFactionList;
        private Vector2 _scrollFactionDetail;
        private bool _factionDirty;
        private readonly Dictionary<string, FactionId> _factionNameToId = new Dictionary<string, FactionId>();
        private readonly Dictionary<FactionId, string> _factionIdToAssetPath = new Dictionary<FactionId, string>();

        // ── UI state ──
        private Vector2 _scrollNpcs;
        private Vector2 _scrollDb;
        private string _filterText = "";
        private NpcEntryType _typeFilter = (NpcEntryType)0; // 0 = all
        private bool _showAllTypes = true;

        // ── Expand state per scene group ──
        private readonly Dictionary<string, bool> _sceneExpanded = new Dictionary<string, bool>();
        private readonly Dictionary<int, bool> _entryExpanded = new Dictionary<int, bool>();

        // ── Column widths ──
        private const float COL_TYPE = 90f;
        private const float COL_NAME = 160f;
        private const float COL_FACTION = 100f;
        private const float COL_BEHAVIOR = 90f;
        private const float COL_CONNECTION = 200f;
        private const float COL_QUESTS = 180f;
        private const float COL_POS = 130f;

        [MenuItem("Tools/Project C/NPC World Inspector", priority = 90)]
        public static void Open()
        {
            var window = GetWindow<NpcWorldInspectorWindow>("NPC World Inspector");
            window.minSize = new Vector2(900, 500);
            window.Show();
        }

        private void OnEnable()
        {
            CacheWorldScenePaths();
            LoadQuestDatabase();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab,
                new[] { "🌍 Scene NPCs", "📋 Quest DB Cross-Ref", "🏛 Factions" },
                GUILayout.Height(24));

            GUILayout.FlexibleSpace();

            // Scan button (always visible)
            EditorGUI.BeginDisabledGroup(_scanInProgress);
            if (GUILayout.Button(_scanInProgress ? "⏳ Scanning..." : "🔍 Scan All World Scenes",
                EditorStyles.toolbarButton, GUILayout.Width(180)))
            {
                ScanAllWorldScenes();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _scanResult = null;
                _sceneExpanded.Clear();
                _entryExpanded.Clear();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            switch (_currentTab)
            {
                case Tab.SceneNpcs: DrawSceneNpcsTab(); break;
                case Tab.QuestDbCrossRef: DrawQuestDbCrossRefTab(); break;
                case Tab.Factions: DrawFactionsTab(); break;
            }
        }

        // ════════════════════════════════════════════════
        //  TAB 1: Scene NPCs
        // ════════════════════════════════════════════════

        private void DrawSceneNpcsTab()
        {
            if (_scanResult == null || _scanResult.entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No scan data. Click 'Scan All World Scenes' to discover NPCs across all WorldScene_* files.\n\n" +
                    "This will additively load each scene, find all NPC-related GameObjects, capture their data, and close the scene.",
                    MessageType.Info);
                return;
            }

            // Summary bar
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"Scanned {_scanResult.sceneCount} scenes, found {_scanResult.entries.Count} NPCs " +
                $"(🎯{_scanResult.QuestCount} ⚔{_scanResult.AICount} 🔄{_scanResult.SpawnerCount} 🚢{_scanResult.ShipCount})",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Scanned: {_scanResult.scanTime:HH:mm:ss}", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Filter bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
            _filterText = EditorGUILayout.TextField(_filterText, GUILayout.Width(180));
            GUILayout.Space(8);
            _showAllTypes = EditorGUILayout.ToggleLeft("All types", _showAllTypes, GUILayout.Width(80));
            if (!_showAllTypes)
            {
                _typeFilter = (NpcEntryType)EditorGUILayout.EnumPopup(_typeFilter, GUILayout.Width(110));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Column header
            DrawColumnHeader();

            _scrollNpcs = EditorGUILayout.BeginScrollView(_scrollNpcs);

            // Group by scene
            var filtered = GetFilteredEntries();
            var grouped = filtered.GroupBy(e => e.sceneName).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                string sceneName = group.Key;
                var entries = group.ToList();

                // Ensure expanded state
                if (!_sceneExpanded.ContainsKey(sceneName))
                    _sceneExpanded[sceneName] = true;

                // Scene group header
                var bgRect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUI.DrawRect(bgRect, new Color(0.22f, 0.28f, 0.36f, 0.8f));

                bool expanded = _sceneExpanded[sceneName];
                bool newExpanded = EditorGUILayout.Foldout(expanded,
                    $"📍 {sceneName}  —  {entries.Count} NPC(s)", true);
                if (newExpanded != expanded)
                    _sceneExpanded[sceneName] = newExpanded;

                // Type summary pills
                GUILayout.FlexibleSpace();
                int qc = entries.Count(e => e.entryType == NpcEntryType.Quest);
                int ac = entries.Count(e => e.entryType == NpcEntryType.AI);
                int sc = entries.Count(e => e.entryType == NpcEntryType.Spawner);
                int hc = entries.Count(e => e.entryType == NpcEntryType.Ship);
                if (qc > 0) DrawPill($"🎯{qc}", new Color(0.3f, 0.7f, 0.3f));
                if (ac > 0) DrawPill($"⚔{ac}", new Color(0.8f, 0.4f, 0.3f));
                if (sc > 0) DrawPill($"🔄{sc}", new Color(0.5f, 0.5f, 0.8f));
                if (hc > 0) DrawPill($"🚢{hc}", new Color(0.4f, 0.6f, 0.9f));
                EditorGUILayout.EndHorizontal();

                if (expanded)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        DrawNpcRow(entries[i], i);
                    }
                }
            }

            if (!filtered.Any())
            {
                EditorGUILayout.HelpBox("No NPCs match the current filter.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawColumnHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("", GUILayout.Width(18)); // expand
                GUILayout.Label("Type", EditorStyles.toolbarButton, GUILayout.Width(COL_TYPE));
                GUILayout.Label("Name", EditorStyles.toolbarButton, GUILayout.Width(COL_NAME));
                GUILayout.Label("Faction", EditorStyles.toolbarButton, GUILayout.Width(COL_FACTION));
                GUILayout.Label("Behavior", EditorStyles.toolbarButton, GUILayout.Width(COL_BEHAVIOR));
                GUILayout.Label("Connection", EditorStyles.toolbarButton, GUILayout.Width(COL_CONNECTION));
                GUILayout.Label("Quests", EditorStyles.toolbarButton, GUILayout.Width(COL_QUESTS));
                GUILayout.Label("Position", EditorStyles.toolbarButton, GUILayout.Width(COL_POS));
            }
        }

        private void DrawNpcRow(NpcEntry entry, int entryIndex)
        {
            if (!_entryExpanded.ContainsKey(entryIndex))
                _entryExpanded[entryIndex] = false;

            bool isExpanded = _entryExpanded[entryIndex];

            // Row background
            var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(isExpanded ? 140 : 22));
            bool isEven = entryIndex % 2 == 0;
            if (isEven)
            {
                var bg = new Color(0.25f, 0.25f, 0.28f, 0.4f);
                EditorGUI.DrawRect(rowRect, bg);
            }

            // Expand toggle
            bool newExpanded = GUILayout.Toggle(isExpanded, isExpanded ? "▼" : "▶", EditorStyles.label, GUILayout.Width(18));
            if (newExpanded != isExpanded)
                _entryExpanded[entryIndex] = newExpanded;

            // Type with color
            var typeColor = entry.entryType switch
            {
                NpcEntryType.Quest   => new Color(0.3f, 0.8f, 0.3f),
                NpcEntryType.AI      => new Color(0.9f, 0.5f, 0.3f),
                NpcEntryType.Spawner => new Color(0.5f, 0.5f, 0.9f),
                NpcEntryType.Ship    => new Color(0.4f, 0.7f, 1.0f),
                _ => Color.gray
            };
            GUI.color = typeColor;
            GUILayout.Label(entry.TypeLabel, GUILayout.Width(COL_TYPE));
            GUI.color = Color.white;

            // Name (clickable → ping GO in scene)
            if (GUILayout.Button(entry.goName, EditorStyles.linkLabel, GUILayout.Width(COL_NAME)))
            {
                PingSceneObject(entry);
            }

            // Faction
            GUILayout.Label(entry.FactionLabel, GUILayout.Width(COL_FACTION));

            // Behavior
            GUILayout.Label(entry.BehaviorLabel, GUILayout.Width(COL_BEHAVIOR));

            // Connection
            GUILayout.Label(entry.ConnectionLabel, EditorStyles.miniLabel, GUILayout.Width(COL_CONNECTION));

            // Quests
            GUILayout.Label(entry.QuestSummary, EditorStyles.miniLabel, GUILayout.Width(COL_QUESTS));

            // Position
            GUILayout.Label($"{entry.position.x:F0},{entry.position.y:F0},{entry.position.z:F0}",
                EditorStyles.miniLabel, GUILayout.Width(COL_POS));

            EditorGUILayout.EndHorizontal();

            // ── Expanded details ──
            if (isExpanded)
            {
                DrawNpcDetails(entry);
            }
        }

        private void DrawNpcDetails(NpcEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            switch (entry.entryType)
            {
                case NpcEntryType.Quest:
                    DrawQuestNpcDetails(entry);
                    break;
                case NpcEntryType.AI:
                    DrawAiNpcDetails(entry);
                    break;
                case NpcEntryType.Spawner:
                    DrawSpawnerDetails(entry);
                    break;
                case NpcEntryType.Ship:
                    DrawShipDetails(entry);
                    break;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawQuestNpcDetails(NpcEntry e)
        {
            EditorGUILayout.LabelField("Quest NPC Details", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("NPC ID:", GUILayout.Width(100));
                EditorGUILayout.SelectableLabel(e.questNpcId ?? "—", GUILayout.Height(18));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Display:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.questDisplayName ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Faction:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.questFaction ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Services:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.questServices ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Interaction:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{e.questInteractionRadius:F1}m");
            }

            if (!string.IsNullOrEmpty(e.questDialogTreePath))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Dialog:", GUILayout.Width(100));
                    if (GUILayout.Button(e.questDialogTreePath, EditorStyles.linkLabel))
                        PingAsset(e.questDialogTreePath);
                }
            }

            if (!string.IsNullOrEmpty(e.questOffers))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Quest Offers:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(e.questOffers, EditorStyles.wordWrappedLabel);
                }
            }
            if (!string.IsNullOrEmpty(e.questTurnIns))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Turn-Ins:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(e.questTurnIns, EditorStyles.wordWrappedLabel);
                }
            }
            if (!string.IsNullOrEmpty(e.questAttitudeLinks))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Attitude Links:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(e.questAttitudeLinks, EditorStyles.wordWrappedLabel);
                }
            }
            if (!string.IsNullOrEmpty(e.questGreetingText))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Greeting:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(e.questGreetingText, EditorStyles.wordWrappedLabel);
                }
            }

            // Ping buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(100);
                if (!string.IsNullOrEmpty(e.questDefinitionPath))
                {
                    if (GUILayout.Button("📄 Ping NpcDefinition", GUILayout.Width(160)))
                        PingAsset(e.questDefinitionPath);
                }
                if (GUILayout.Button("🎯 Ping in Scene", GUILayout.Width(140)))
                    PingSceneObject(e);
            }
        }

        private void DrawAiNpcDetails(NpcEntry e)
        {
            EditorGUILayout.LabelField("Combat NPC Details", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Behavior:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.aiBehaviorType ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Social:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.aiSocialEnabled ? "✅ Enabled" : "❌ Disabled");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Faction:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.aiFaction ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Aggro Range:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{e.aiAggroRange:F1}m");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Leash Range:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{e.aiLeashRange:F1}m");
            }
            if (!string.IsNullOrEmpty(e.aiIdleActivity))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Idle:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(e.aiIdleActivity);
                }
            }
            if (!string.IsNullOrEmpty(e.aiCombatDataPath))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Combat Data:", GUILayout.Width(100));
                    if (GUILayout.Button(e.aiCombatDataPath, EditorStyles.linkLabel))
                        PingAsset(e.aiCombatDataPath);
                }
            }
            if (!string.IsNullOrEmpty(e.aiMarketConfigPath))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Market:", GUILayout.Width(100));
                    if (GUILayout.Button(e.aiMarketConfigPath, EditorStyles.linkLabel))
                        PingAsset(e.aiMarketConfigPath);
                }
            }
            if (!string.IsNullOrEmpty(e.aiSkillSetPath))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Skill Set:", GUILayout.Width(100));
                    if (GUILayout.Button(e.aiSkillSetPath, EditorStyles.linkLabel))
                        PingAsset(e.aiSkillSetPath);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(100);
                if (GUILayout.Button("🎯 Ping in Scene", GUILayout.Width(140)))
                    PingSceneObject(e);
            }
        }

        private void DrawSpawnerDetails(NpcEntry e)
        {
            EditorGUILayout.LabelField("NPC Spawner Details", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Prefab:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.spawnerPrefabName ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Behavior:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.spawnerBehaviorType ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Social:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.spawnerSocialEnabled ? "✅ Enabled" : "❌ Disabled");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Faction:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.spawnerFaction ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Spawn Mode:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.spawnerSpawnMode ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Limits:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"maxAlive={e.spawnerMaxAlive}, total={e.spawnerTotalLimit}, activation={e.spawnerActivationRadius:F0}m");
            }
            if (e.spawnerPatrolMarkerCount > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Patrol Markers:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(e.spawnerPatrolMarkerCount.ToString());
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(100);
                if (!string.IsNullOrEmpty(e.spawnerConfigPath))
                {
                    if (GUILayout.Button("📄 Ping Config", GUILayout.Width(140)))
                        PingAsset(e.spawnerConfigPath);
                }
                if (!string.IsNullOrEmpty(e.spawnerPrefabPath))
                {
                    if (GUILayout.Button("📦 Ping Prefab", GUILayout.Width(140)))
                        PingAsset(e.spawnerPrefabPath);
                }
                if (GUILayout.Button("🎯 Ping in Scene", GUILayout.Width(140)))
                    PingSceneObject(e);
            }
        }

        private void DrawShipDetails(NpcEntry e)
        {
            EditorGUILayout.LabelField("NPC Ship Details", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Schedule:", GUILayout.Width(100));
                EditorGUILayout.LabelField(string.IsNullOrEmpty(e.shipScheduleId) ? "⚠ NO SCHEDULE" : e.shipScheduleId);
            }
            if (!string.IsNullOrEmpty(e.shipScheduleDisplayName))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Name:", GUILayout.Width(100));
                    EditorGUILayout.LabelField(e.shipScheduleDisplayName);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Type:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.shipScheduleType ?? "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Routes:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.shipRouteCount > 0 ? $"{e.shipRouteCount}: {e.shipRouteSummary}" : "(no routes)");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cargo:", GUILayout.Width(100));
                EditorGUILayout.LabelField(e.shipHasCargo ? $"✅ {e.shipCargoSummary}" : "—");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Flight:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{e.shipFlightClass}, thrust×{e.shipNpcThrustMult}, yaw×{e.shipNpcYawMult}");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(100);
                if (!string.IsNullOrEmpty(e.shipSchedulePath))
                {
                    if (GUILayout.Button("📄 Ping Schedule", GUILayout.Width(140)))
                        PingAsset(e.shipSchedulePath);
                }
                if (GUILayout.Button("🎯 Ping in Scene", GUILayout.Width(140)))
                    PingSceneObject(e);
            }
        }

        private void DrawPill(string label, Color color)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label($" {label} ", EditorStyles.miniButton, GUILayout.Width(50));
            GUI.backgroundColor = oldColor;
        }

        // ════════════════════════════════════════════════
        //  TAB 2: Quest DB Cross-Ref
        // ════════════════════════════════════════════════

        private void DrawQuestDbCrossRefTab()
        {
            if (!_questDbLoaded || _questDb == null)
            {
                EditorGUILayout.HelpBox("QuestDatabase not found. Scan the project first.", MessageType.Warning);
                if (GUILayout.Button("Load Quest Database"))
                    LoadQuestDatabase();
                return;
            }

            var dbNpcs = _questDb.npcs;
            if (dbNpcs == null || dbNpcs.Length == 0)
            {
                EditorGUILayout.HelpBox("QuestDatabase has no NPCs registered.", MessageType.Warning);
                return;
            }

            // Summary
            var placedNpcIds = _scanResult?.ReferencedNpcIds ?? new HashSet<string>();
            int placedCount = dbNpcs.Count(n => placedNpcIds.Contains(n.npcId));
            int unplacedCount = dbNpcs.Length - placedCount;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"Quest DB: {dbNpcs.Length} NPC definitions  |  " +
                $"✅ {placedCount} placed in scenes  |  ⚠ {unplacedCount} not placed",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Scan state warning
            if (_scanResult == null)
            {
                EditorGUILayout.HelpBox(
                    "No scene scan data yet. Click 'Scan All World Scenes' to cross-reference placement status.",
                    MessageType.Info);
            }

            // Column header
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Status", EditorStyles.toolbarButton, GUILayout.Width(60));
                GUILayout.Label("NPC ID", EditorStyles.toolbarButton, GUILayout.Width(120));
                GUILayout.Label("Display Name", EditorStyles.toolbarButton, GUILayout.Width(180));
                GUILayout.Label("Faction", EditorStyles.toolbarButton, GUILayout.Width(130));
                GUILayout.Label("Services", EditorStyles.toolbarButton, GUILayout.Width(110));
                GUILayout.Label("Quests", EditorStyles.toolbarButton, GUILayout.Width(160));
                GUILayout.Label("Scene", EditorStyles.toolbarButton, GUILayout.Width(130));
            }

            _scrollDb = EditorGUILayout.BeginScrollView(_scrollDb);

            for (int i = 0; i < dbNpcs.Length; i++)
            {
                var npc = dbNpcs[i];
                if (npc == null) continue;

                bool isPlaced = placedNpcIds.Contains(npc.npcId);

                // Find scene placement
                string sceneName = "—";
                NpcEntry sceneEntry = null;
                if (isPlaced && _scanResult != null)
                {
                    sceneEntry = _scanResult.entries.FirstOrDefault(
                        e => e.entryType == NpcEntryType.Quest && e.questNpcId == npc.npcId);
                    if (sceneEntry != null)
                        sceneName = sceneEntry.sceneName;
                }

                var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
                if (!isPlaced)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.4f, 0.1f, 0.3f));
                }

                // Status
                GUI.color = isPlaced ? Color.green : new Color(1f, 0.8f, 0.3f);
                GUILayout.Label(isPlaced ? "✅" : "⚠", GUILayout.Width(60));
                GUI.color = Color.white;

                // NPC ID (clickable)
                if (GUILayout.Button(npc.npcId, EditorStyles.linkLabel, GUILayout.Width(120)))
                {
                    PingAsset(AssetDatabase.GetAssetPath(npc));
                }

                GUILayout.Label(npc.displayName, GUILayout.Width(180));
                GUILayout.Label(npc.faction.ToString(), GUILayout.Width(130));
                GUILayout.Label(npc.services != NpcService.None ? npc.services.ToString() : "—", GUILayout.Width(110));

                // Quest summary
                string questInfo = "";
                if (npc.questOffers != null && npc.questOffers.Length > 0)
                    questInfo = $"offers:{npc.questOffers.Length}";
                if (npc.questTurnIns != null && npc.questTurnIns.Length > 0)
                    questInfo += (questInfo.Length > 0 ? " " : "") + $"turnIn:{npc.questTurnIns.Length}";
                GUILayout.Label(string.IsNullOrEmpty(questInfo) ? "—" : questInfo, GUILayout.Width(160));

                // Scene
                GUI.color = isPlaced ? Color.white : new Color(1f, 0.8f, 0.3f);
                GUILayout.Label(sceneName, GUILayout.Width(130));
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // ════════════════════════════════════════════════
        //  Scanning
        // ════════════════════════════════════════════════

        private void CacheWorldScenePaths()
        {
            var guids = AssetDatabase.FindAssets("WorldScene_ t:Scene", new[] { "Assets/_Project/Scenes/World" });
            _worldScenePaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileNameWithoutExtension(p).StartsWith("WorldScene_"))
                .OrderBy(p => p)
                .ToArray();
        }

        private void ScanAllWorldScenes()
        {
            if (_scanInProgress) return;
            _scanInProgress = true;
            _scanResult = new SceneNpcScanResult();
            _sceneExpanded.Clear();
            _entryExpanded.Clear();

            try
            {
                CacheWorldScenePaths();
                string currentScenePath = SceneManager.GetActiveScene().path;

                for (int i = 0; i < _worldScenePaths.Length; i++)
                {
                    string scenePath = _worldScenePaths[i];
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                    EditorUtility.DisplayProgressBar(
                        "Scanning NPCs",
                        $"Scene {i + 1}/{_worldScenePaths.Length}: {sceneName}",
                        (float)i / _worldScenePaths.Length);

                    try
                    {
                        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                        // Collect all NPC types
                        CollectNpcControllers(scene, sceneName, scenePath);
                        CollectNpcBrains(scene, sceneName, scenePath);
                        CollectNpcSpawners(scene, sceneName, scenePath);
                        CollectNpcShips(scene, sceneName, scenePath);

                        EditorSceneManager.CloseScene(scene, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NpcWorldInspector] Failed to scan {sceneName}: {ex.Message}");
                    }
                }

                _scanResult.sceneCount = _worldScenePaths.Length;
                _scanResult.scanTime = DateTime.Now;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _scanInProgress = false;
                Repaint();

                Debug.Log($"[NpcWorldInspector] Scan complete: {_scanResult.entries.Count} NPCs in {_scanResult.sceneCount} scenes " +
                          $"(🎯{_scanResult.QuestCount} ⚔{_scanResult.AICount} 🔄{_scanResult.SpawnerCount} 🚢{_scanResult.ShipCount})");
            }
        }

        private void CollectNpcControllers(Scene scene, string sceneName, string scenePath)
        {
            var controllers = FindObjectsByType<NpcController>(FindObjectsInactive.Include);
            foreach (var ctrl in controllers)
            {
                if (ctrl.gameObject.scene != scene) continue;

                var def = ctrl.Definition;
                var entry = new NpcEntry
                {
                    sceneName = sceneName,
                    scenePath = scenePath,
                    goName = ctrl.gameObject.name,
                    goPath = GetHierarchyPath(ctrl.transform),
                    position = ctrl.transform.position,
                    entryType = NpcEntryType.Quest,
                    questInteractionRadius = ctrl.InteractionDistance,
                };

                if (def != null)
                {
                    entry.questNpcId = def.npcId;
                    entry.questDisplayName = def.displayName;
                    entry.questFaction = def.faction.ToString();
                    entry.questDialogTreePath = def.defaultDialogTree != null
                        ? AssetDatabase.GetAssetPath(def.defaultDialogTree) : null;
                    entry.questOffers = def.questOffers != null && def.questOffers.Length > 0
                        ? string.Join(", ", def.questOffers) : null;
                    entry.questTurnIns = def.questTurnIns != null && def.questTurnIns.Length > 0
                        ? string.Join(", ", def.questTurnIns) : null;
                    entry.questServices = def.services != NpcService.None ? def.services.ToString() : null;
                    entry.questGreetingText = def.greetingText;
                    entry.questVoicePrefix = def.voicePrefix;
                    entry.questDefinitionPath = AssetDatabase.GetAssetPath(def);
                    entry.questAttitudeMin = def.personalAttitudeMin;
                    entry.questAttitudeMax = def.personalAttitudeMax;

                    if (def.attitudeLinks != null && def.attitudeLinks.Length > 0)
                    {
                        entry.questAttitudeLinks = string.Join("; ",
                            def.attitudeLinks.Select(a => $"{a.targetFaction}:{(a.deltaOnLike >= 0 ? "+" : "")}{a.deltaOnLike}/{(a.deltaOnDislike >= 0 ? "+" : "")}{a.deltaOnDislike}"));
                    }
                }

                _scanResult.entries.Add(entry);
            }
        }

        private void CollectNpcBrains(Scene scene, string sceneName, string scenePath)
        {
            var brains = FindObjectsByType<NpcBrain>(FindObjectsInactive.Include);
            foreach (var brain in brains)
            {
                if (brain.gameObject.scene != scene) continue;
                // Skip if already captured as NpcController (quest NPC might have NpcBrain too)
                var npcCtrl = brain.GetComponent<NpcController>();
                if (npcCtrl != null) continue;

                var entry = new NpcEntry
                {
                    sceneName = sceneName,
                    scenePath = scenePath,
                    goName = brain.gameObject.name,
                    goPath = GetHierarchyPath(brain.transform),
                    position = brain.transform.position,
                    entryType = NpcEntryType.AI,
                    aiBehaviorType = brain.CurrentBehavior.ToString(),
                    aiAggroRange = brain.aggroRange,
                    aiLeashRange = brain.leashRange,
                };

                // Check social brain
                var socialBrain = brain.GetComponent<NpcSocialBrain>();
                entry.aiSocialEnabled = socialBrain != null;
                entry.aiHasSocialBrain = socialBrain != null;

                if (socialBrain != null)
                {
                    entry.aiIdleActivity = socialBrain.idleActivity.ToString();
                    entry.aiCanFlee = socialBrain.canFlee;
                    if (socialBrain.faction != null)
                        entry.aiFaction = socialBrain.faction.CombatKey;
                }

                // Combat data
                var attacker = brain.GetComponent<NpcAttacker>();
                if (attacker != null && attacker.Data != null)
                {
                    entry.aiCombatDataPath = AssetDatabase.GetAssetPath(attacker.Data);
                }

                // Skill set
                if (attacker != null && attacker.SkillSet != null)
                {
                    entry.aiSkillSetPath = AssetDatabase.GetAssetPath(attacker.SkillSet);
                }

                // Market zone
                var marketZone = brain.GetComponent<MarketZone>();
                if (marketZone != null && marketZone.Config != null)
                {
                    entry.aiMarketConfigPath = AssetDatabase.GetAssetPath(marketZone.Config);
                }

                _scanResult.entries.Add(entry);
            }
        }

        private void CollectNpcSpawners(Scene scene, string sceneName, string scenePath)
        {
            var spawners = FindObjectsByType<NpcSpawner>(FindObjectsInactive.Include);
            foreach (var sp in spawners)
            {
                if (sp.gameObject.scene != scene) continue;

                var entry = new NpcEntry
                {
                    sceneName = sceneName,
                    scenePath = scenePath,
                    goName = sp.gameObject.name,
                    goPath = GetHierarchyPath(sp.transform),
                    position = sp.transform.position,
                    entryType = NpcEntryType.Spawner,
                    spawnerActivationRadius = sp.activationRadius,
                };

                // NpcSpawner exposes some fields as public — grab what we can via SerializedObject
                var so = new SerializedObject(sp);
                var configProp = so.FindProperty("_config");
                if (configProp != null && configProp.objectReferenceValue != null)
                {
                    var config = configProp.objectReferenceValue as NpcSpawnerConfig;
                    entry.spawnerConfigPath = AssetDatabase.GetAssetPath(config);

                    if (config != null)
                    {
                        entry.spawnerPrefabName = config.npcPrefab != null ? config.npcPrefab.name : null;
                        entry.spawnerPrefabPath = config.npcPrefab != null
                            ? AssetDatabase.GetAssetPath(config.npcPrefab) : null;
                        entry.spawnerBehaviorType = config.behaviorType.ToString();
                        entry.spawnerSocialEnabled = config.socialEnabled;
                        entry.spawnerFaction = config.faction != null ? config.faction.name : null;
                        entry.spawnerSpawnMode = config.spawnMode.ToString();
                        entry.spawnerMaxAlive = config.maxAliveCount;
                        entry.spawnerTotalLimit = config.totalSpawnLimit;
                        entry.spawnerAutoPopulateChunks = config.autoPopulateChunks;
                        entry.spawnerLootTablePath = config.lootTable != null
                            ? AssetDatabase.GetAssetPath(config.lootTable) : null;
                        entry.spawnerLootPrefabPath = config.lootPrefab != null
                            ? AssetDatabase.GetAssetPath(config.lootPrefab) : null;
                        entry.spawnerVisualConfigPath = config.visualConfig != null
                            ? AssetDatabase.GetAssetPath(config.visualConfig) : null;
                    }
                }

                // Patrol markers
                var markersProp = so.FindProperty("patrolWaypointMarkers");
                if (markersProp != null && markersProp.isArray)
                {
                    entry.spawnerPatrolMarkerCount = markersProp.arraySize;
                }

                so.Dispose();
                _scanResult.entries.Add(entry);
            }
        }

        private void CollectNpcShips(Scene scene, string sceneName, string scenePath)
        {
            var ships = FindObjectsByType<NpcShipController>(FindObjectsInactive.Include);
            foreach (var ship in ships)
            {
                if (ship.gameObject.scene != scene) continue;

                var schedule = ship.Schedule;
                var entry = new NpcEntry
                {
                    sceneName = sceneName,
                    scenePath = scenePath,
                    goName = ship.gameObject.name,
                    goPath = GetHierarchyPath(ship.transform),
                    position = ship.transform.position,
                    entryType = NpcEntryType.Ship,
                };

                // Read private fields via SerializedObject
                var shipSo = new SerializedObject(ship);
                entry.shipNpcThrustMult = shipSo.FindProperty("npcThrustMult")?.floatValue ?? 0.6f;
                entry.shipNpcYawMult = shipSo.FindProperty("npcYawMult")?.floatValue ?? 0.4f;
                entry.shipOverrideSpeeds = shipSo.FindProperty("overrideClassSpeeds")?.boolValue ?? false;
                shipSo.Dispose();

                if (schedule != null)
                {
                    entry.shipScheduleId = schedule.scheduleId;
                    entry.shipScheduleDisplayName = schedule.displayName;
                    entry.shipSchedulePath = AssetDatabase.GetAssetPath(schedule);
                    entry.shipScheduleType = schedule.scheduleType.ToString();
                    entry.shipRouteCount = schedule.routes?.Length ?? 0;
                    entry.shipRouteSummary = schedule.routes != null && schedule.routes.Length > 0
                        ? string.Join(", ", schedule.routes.Take(3).Select(r => $"{r.fromLocationId}→{r.toLocationId}"))
                          + (schedule.routes.Length > 3 ? " ..." : "")
                        : "";

                    bool hasCargo = schedule.cargoTrade?.buyItems != null && schedule.cargoTrade.buyItems.Length > 0;
                    entry.shipHasCargo = hasCargo;
                    entry.shipCargoItemCount = hasCargo ? schedule.cargoTrade.buyItems.Length : 0;
                    entry.shipCargoSummary = hasCargo ? $"{schedule.cargoTrade.buyItems.Length} items" : "—";
                }

                _scanResult.entries.Add(entry);
            }
        }

        // ════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════

        private List<NpcEntry> GetFilteredEntries()
        {
            if (_scanResult == null) return new List<NpcEntry>();

            var query = _scanResult.entries.AsEnumerable();

            if (!_showAllTypes)
                query = query.Where(e => e.entryType == _typeFilter);

            if (!string.IsNullOrEmpty(_filterText))
            {
                var lower = _filterText.ToLowerInvariant();
                query = query.Where(e =>
                    e.goName.ToLowerInvariant().Contains(lower) ||
                    (e.questNpcId?.ToLowerInvariant().Contains(lower) ?? false) ||
                    (e.questDisplayName?.ToLowerInvariant().Contains(lower) ?? false) ||
                    (e.FactionLabel.ToLowerInvariant().Contains(lower)) ||
                    (e.shipScheduleId?.ToLowerInvariant().Contains(lower) ?? false) ||
                    (e.spawnerPrefabName?.ToLowerInvariant().Contains(lower) ?? false));
            }

            return query.ToList();
        }

        private static string GetHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }

        private void PingAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        private void PingSceneObject(NpcEntry entry)
        {
            if (string.IsNullOrEmpty(entry.scenePath)) return;

            try
            {
                // Open scene additively
                Scene scene = EditorSceneManager.OpenScene(entry.scenePath, OpenSceneMode.Additive);

                // Find by name (best effort)
                var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                GameObject found = null;
                foreach (var go in allObjects)
                {
                    if (go.scene != scene) continue;
                    if (go.name == entry.goName && Vector3.Distance(go.transform.position, entry.position) < 0.1f)
                    {
                        found = go;
                        break;
                    }
                }

                // Fallback: first match by name only
                if (found == null)
                {
                    foreach (var go in allObjects)
                    {
                        if (go.scene == scene && go.name == entry.goName)
                        {
                            found = go;
                            break;
                        }
                    }
                }

                if (found != null)
                {
                    Selection.activeGameObject = found;
                    EditorGUIUtility.PingObject(found);
                }

                EditorSceneManager.CloseScene(scene, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NpcWorldInspector] Could not ping scene object '{entry.goName}': {ex.Message}");
            }
        }

        private void LoadQuestDatabase()
        {
            const string dbPath = "Assets/_Project/Quests/Data/QuestDatabase.asset";
            _questDb = AssetDatabase.LoadAssetAtPath<QuestDatabase>(dbPath);
            _questDbLoaded = _questDb != null;
        }

        // ════════════════════════════════════════════════
        //  TAB 3: Factions
        // ════════════════════════════════════════════════

        private void DrawFactionsTab()
        {
            // Top bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("🔍 Scan Factions", EditorStyles.toolbarButton, GUILayout.Width(120)))
                ScanFactions();

            if (GUILayout.Button("➕ Create New", EditorStyles.toolbarButton, GUILayout.Width(110)))
                CreateNewFaction();

            GUILayout.FlexibleSpace();

            if (_factionDirty && _selectedFactionIndex >= 0)
            {
                GUI.color = Color.yellow;
                if (GUILayout.Button("💾 Save Changes", EditorStyles.toolbarButton, GUILayout.Width(120)))
                    SaveFactionChanges();
                GUI.color = Color.white;
            }

            if (_factionResult != null)
                EditorGUILayout.LabelField($"{_factionResult.factionCount} factions", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (_factionResult == null || _factionResult.factions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No faction data. Click 'Scan Factions' to load all FactionDefinition assets.\n\n" +
                    "This scans Assets/_Project/Quests/Data/Factions/ for all FactionDefinition ScriptableObjects.",
                    MessageType.Info);
                return;
            }

            // Two-panel layout
            EditorGUILayout.BeginHorizontal();

            // ── Left panel: faction list ──
            EditorGUILayout.BeginVertical(GUILayout.Width(260));
            EditorGUILayout.LabelField("Factions", EditorStyles.boldLabel);
            _scrollFactionList = EditorGUILayout.BeginScrollView(_scrollFactionList);

            for (int i = 0; i < _factionResult.factions.Count; i++)
            {
                var f = _factionResult.factions[i];
                bool isSelected = i == _selectedFactionIndex;

                var rowRect = EditorGUILayout.BeginHorizontal(
                    isSelected ? "ProjectBrowserHeaderBgTop" : "ProjectBrowserHeaderBgMiddle",
                    GUILayout.Height(26));

                if (isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.5f, 0.7f, 0.5f));

                // Color swatch
                var oldColor = GUI.color;
                GUI.color = f.color;
                EditorGUILayout.LabelField("■", GUILayout.Width(16));
                GUI.color = oldColor;

                // Faction name (clickable)
                if (GUILayout.Button(f.displayName, EditorStyles.label, GUILayout.MinWidth(120)))
                {
                    _selectedFactionIndex = i;
                    _factionDirty = false;
                }

                // NPC count pill
                if (f.npcCount > 0)
                {
                    GUI.color = new Color(0.5f, 0.8f, 0.5f);
                    GUILayout.Label($"{f.npcCount}", EditorStyles.miniButton, GUILayout.Width(30));
                    GUI.color = oldColor;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Separator
            EditorGUILayout.LabelField("", GUI.skin.verticalSlider, GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // ── Right panel: faction detail ──
            EditorGUILayout.BeginVertical();
            _scrollFactionDetail = EditorGUILayout.BeginScrollView(_scrollFactionDetail);

            if (_selectedFactionIndex >= 0 && _selectedFactionIndex < _factionResult.factions.Count)
                DrawFactionDetail(_factionResult.factions[_selectedFactionIndex]);
            else
                EditorGUILayout.HelpBox("Select a faction from the list to view and edit details.", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFactionDetail(FactionEntry f)
        {
            // ═══ Header ═══
            EditorGUILayout.BeginHorizontal();
            var oldColor = GUI.color;
            GUI.color = f.color;
            EditorGUILayout.LabelField($"■ {f.displayName} [{f.factionId}]", EditorStyles.whiteLargeLabel);
            GUI.color = oldColor;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("📄 Ping Asset", GUILayout.Width(110)))
                PingAsset(f.assetPath);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // ═══ Identity ═══
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Display Name:", GUILayout.Width(120));
                var newName = EditorGUILayout.TextField(f.displayName);
                if (newName != f.displayName) { f.displayName = newName; _factionDirty = true; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Faction ID:", GUILayout.Width(120));
                EditorGUILayout.LabelField(f.factionId.ToString());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Color:", GUILayout.Width(120));
                var newColor = EditorGUILayout.ColorField(f.color);
                if (newColor != f.color) { f.color = newColor; _factionDirty = true; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Asset Name:", GUILayout.Width(120));
                EditorGUILayout.SelectableLabel(f.assetName, GUILayout.Height(18));
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            // ═══ Lore ═══
            EditorGUILayout.LabelField("Lore", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Description:", GUILayout.Width(120));
            var newLore = EditorGUILayout.TextArea(f.loreDescription ?? "", GUILayout.Height(40));
            if (newLore != f.loreDescription) { f.loreDescription = newLore; _factionDirty = true; }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            // ═══ Defaults ═══
            EditorGUILayout.LabelField("Defaults", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Default Attitude:", GUILayout.Width(130));
                var newAtt = (FactionAttitude)EditorGUILayout.EnumPopup(f.defaultAttitude);
                if (newAtt != f.defaultAttitude) { f.defaultAttitude = newAtt; _factionDirty = true; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Default Combat:", GUILayout.Width(130));
                var newRel = (FactionRelation)EditorGUILayout.EnumPopup(f.defaultCombatRelation);
                if (newRel != f.defaultCombatRelation) { f.defaultCombatRelation = newRel; _factionDirty = true; }
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            // ═══ Reputation Tiers ═══
            EditorGUILayout.LabelField($"Reputation Tiers ({f.reputationThresholds?.Length ?? 0})", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (f.reputationThresholds != null)
            {
                for (int t = 0; t < f.reputationThresholds.Length; t++)
                {
                    var tier = f.reputationThresholds[t];
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    // tier label
                    EditorGUILayout.LabelField("Tier:", GUILayout.Width(35));
                    var newLabel = EditorGUILayout.TextField(tier.tier, GUILayout.Width(100));
                    if (newLabel != tier.tier) { tier.tier = newLabel; _factionDirty = true; }

                    // threshold
                    EditorGUILayout.LabelField("≥", GUILayout.Width(15));
                    var newVal = EditorGUILayout.IntField(tier.value, GUILayout.Width(50));
                    if (newVal != tier.value) { tier.value = newVal; _factionDirty = true; }

                    // color
                    var newCol = EditorGUILayout.ColorField(tier.color, GUILayout.Width(60));
                    if (newCol != tier.color) { tier.color = newCol; _factionDirty = true; }

                    // USS class
                    EditorGUILayout.LabelField("USS:", GUILayout.Width(35));
                    var newUss = EditorGUILayout.TextField(tier.ussClass, GUILayout.Width(110));
                    if (newUss != tier.ussClass) { tier.ussClass = newUss; _factionDirty = true; }

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            // ═══ Combat Relations ═══
            EditorGUILayout.LabelField($"Combat Relations ({f.combatRelations?.Count ?? 0})", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (f.combatRelations != null && f.combatRelations.Count > 0)
            {
                for (int r = 0; r < f.combatRelations.Count; r++)
                {
                    var rel = f.combatRelations[r];
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    EditorGUILayout.LabelField("vs", GUILayout.Width(20));
                    var newTarget = (FactionId)EditorGUILayout.EnumPopup(rel.targetFaction, GUILayout.Width(160));
                    if (newTarget != rel.targetFaction) { rel.targetFaction = newTarget; _factionDirty = true; }

                    var newRel = (FactionRelation)EditorGUILayout.EnumPopup(rel.relation, GUILayout.Width(90));
                    if (newRel != rel.relation) { rel.relation = newRel; _factionDirty = true; }

                    var relColor = rel.relation switch
                    {
                        FactionRelation.Allied => Color.green,
                        FactionRelation.Hostile => Color.red,
                        _ => Color.gray
                    };
                    GUI.color = relColor;
                    EditorGUILayout.LabelField(RelationEmoji(rel.relation), GUILayout.Width(20));
                    GUI.color = Color.white;

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        f.combatRelations.RemoveAt(r);
                        _factionDirty = true;
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            // Add relation button
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20);
                if (GUILayout.Button("➕ Add Combat Relation", GUILayout.Width(180)))
                {
                    f.combatRelations.Add(new FactionCombatRelationEntry
                    {
                        targetFaction = FactionId.None,
                        relation = FactionRelation.Neutral
                    });
                    _factionDirty = true;
                }
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            // ═══ Cross-Reference: Scene NPCs ═══
            EditorGUILayout.LabelField($"Scene NPCs ({f.npcCount})", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (f.npcCount > 0 && _scanResult != null)
            {
                var factionNpcs = _scanResult.entries.FindAll(e =>
                {
                    var fl = e.FactionLabel;
                    return !string.IsNullOrEmpty(fl) &&
                           (fl == f.factionId.ToString() || fl == f.assetName);
                });

                foreach (var npc in factionNpcs)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"{npc.TypeLabel}", GUILayout.Width(110));
                    if (GUILayout.Button(npc.goName, EditorStyles.linkLabel))
                        PingSceneObject(npc);
                    EditorGUILayout.LabelField(npc.sceneName, GUILayout.Width(140));
                    EditorGUILayout.EndHorizontal();
                }

                if (factionNpcs.Count == 0)
                    EditorGUILayout.LabelField("(no scene data — run Scan All World Scenes first)");
            }
            else
            {
                EditorGUILayout.LabelField(f.npcCount == 0
                    ? "(no NPCs reference this faction)"
                    : "(run Scene NPCs scan to see details)");
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);

            // ═══ Action buttons ═══
            EditorGUILayout.BeginHorizontal();
            if (_factionDirty)
            {
                GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
                if (GUILayout.Button("💾 Save Changes", GUILayout.Height(28)))
                    SaveFactionChanges();
                GUI.backgroundColor = Color.white;
                GUILayout.Space(8);
                if (GUILayout.Button("↩ Revert", GUILayout.Height(28)))
                    RevertFactionChanges();
            }
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button("🗑 Delete Faction", GUILayout.Height(28), GUILayout.Width(140)))
            {
                if (EditorUtility.DisplayDialog("Delete Faction",
                    $"Delete '{f.displayName}' ({f.assetName})?\n\nThis will delete the asset file permanently.",
                    "Delete", "Cancel"))
                    DeleteFaction(f);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // ═══ Faction operations ═══

        private void ScanFactions()
        {
            _factionResult = new FactionScanResult();
            _factionNameToId.Clear();
            _factionIdToAssetPath.Clear();

            var guids = AssetDatabase.FindAssets("t:FactionDefinition",
                new[] { "Assets/_Project/Quests/Data/Factions" });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
                if (def == null) continue;

                // Count scene NPCs for this faction
                int npcCount = 0;
                if (_scanResult != null)
                {
                    npcCount = _scanResult.entries.Count(e =>
                    {
                        var fl = e.FactionLabel;
                        return !string.IsNullOrEmpty(fl) &&
                               (fl == def.factionId.ToString() || fl == def.CombatKey);
                    });
                }

                var entry = new FactionEntry
                {
                    assetPath = path,
                    assetName = def.name,
                    factionId = def.factionId,
                    displayName = def.displayName,
                    color = def.color,
                    loreDescription = def.loreDescription,
                    defaultAttitude = def.defaultAttitude,
                    defaultCombatRelation = def.defaultCombatRelation,
                    reputationThresholds = def.reputationThresholds,
                    combatRelations = new List<FactionCombatRelationEntry>(),
                    npcCount = npcCount,
                };

                foreach (var cr in def.combatRelations)
                    entry.combatRelations.Add(new FactionCombatRelationEntry
                    {
                        targetFaction = cr.targetFaction,
                        relation = cr.relation
                    });

                _factionResult.factions.Add(entry);
                _factionNameToId[def.name] = def.factionId;
                _factionNameToId[def.displayName] = def.factionId;
                _factionIdToAssetPath[def.factionId] = path;
            }

            _factionResult.factions.Sort((a, b) => a.factionId.CompareTo(b.factionId));
            _factionResult.scanTime = DateTime.Now;
            _selectedFactionIndex = _factionResult.factions.Count > 0 ? 0 : -1;
            _factionDirty = false;

            Debug.Log($"[NpcWorldInspector] Faction scan complete: {_factionResult.factionCount} factions loaded.");
        }

        private void SaveFactionChanges()
        {
            if (_selectedFactionIndex < 0 || _selectedFactionIndex >= _factionResult.factions.Count) return;

            var entry = _factionResult.factions[_selectedFactionIndex];

            // Check if asset was renamed (assetName vs actual file name)
            var actualPath = entry.assetPath;
            var expectedDir = System.IO.Path.GetDirectoryName(actualPath);
            var expectedPath = System.IO.Path.Combine(expectedDir, entry.assetName + ".asset").Replace("\\", "/");

            if (actualPath != expectedPath)
            {
                var moveResult = AssetDatabase.MoveAsset(actualPath, expectedPath);
                if (!string.IsNullOrEmpty(moveResult))
                {
                    Debug.LogError($"[NpcWorldInspector] Failed to rename asset: {moveResult}");
                    return;
                }
                entry.assetPath = expectedPath;
                actualPath = expectedPath;
            }

            var def = AssetDatabase.LoadAssetAtPath<FactionDefinition>(actualPath);
            if (def == null)
            {
                Debug.LogError($"[NpcWorldInspector] FactionDefinition not found at {actualPath}");
                return;
            }

            var so = new SerializedObject(def);

            // Identity
            so.FindProperty("displayName").stringValue = entry.displayName;
            so.FindProperty("color").colorValue = entry.color;
            so.FindProperty("loreDescription").stringValue = entry.loreDescription ?? "";

            // FactionId — don't change (breaks references)

            // Defaults
            var attProp = so.FindProperty("defaultAttitude");
            if (attProp != null) attProp.enumValueIndex = (int)entry.defaultAttitude;

            var relProp = so.FindProperty("defaultCombatRelation");
            if (relProp != null) relProp.enumValueIndex = (int)entry.defaultCombatRelation;

            // Reputation tiers
            var tiersProp = so.FindProperty("reputationThresholds");
            if (tiersProp != null)
            {
                tiersProp.ClearArray();
                if (entry.reputationThresholds != null)
                {
                    tiersProp.arraySize = entry.reputationThresholds.Length;
                    for (int i = 0; i < entry.reputationThresholds.Length; i++)
                    {
                        var elem = tiersProp.GetArrayElementAtIndex(i);
                        elem.FindPropertyRelative("tier").stringValue = entry.reputationThresholds[i].tier;
                        elem.FindPropertyRelative("value").intValue = entry.reputationThresholds[i].value;
                        elem.FindPropertyRelative("color").colorValue = entry.reputationThresholds[i].color;
                        elem.FindPropertyRelative("ussClass").stringValue = entry.reputationThresholds[i].ussClass;
                    }
                }
            }

            // Combat relations
            var crProp = so.FindProperty("combatRelations");
            if (crProp != null)
            {
                crProp.ClearArray();
                if (entry.combatRelations != null)
                {
                    crProp.arraySize = entry.combatRelations.Count;
                    for (int i = 0; i < entry.combatRelations.Count; i++)
                    {
                        var elem = crProp.GetArrayElementAtIndex(i);
                        elem.FindPropertyRelative("targetFaction").enumValueIndex =
                            (int)entry.combatRelations[i].targetFaction;
                        elem.FindPropertyRelative("relation").enumValueIndex =
                            (int)entry.combatRelations[i].relation;
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssetIfDirty(def);

            _factionDirty = false;
            Debug.Log($"[NpcWorldInspector] Saved changes to {entry.displayName} ({entry.assetName})");
        }

        private void RevertFactionChanges()
        {
            if (_selectedFactionIndex < 0) return;
            ScanFactions(); // re-scan all; selection index preserved
        }

        private void CreateNewFaction()
        {
            // Find next available FactionId
            var usedIds = new HashSet<FactionId>();
            if (_factionResult != null)
                foreach (var f in _factionResult.factions)
                    usedIds.Add(f.factionId);

            // Enum values start at 0, find first unused above None
            FactionId newId = FactionId.None;
            foreach (FactionId val in Enum.GetValues(typeof(FactionId)))
            {
                if (val == FactionId.None) continue;
                if (!usedIds.Contains(val))
                {
                    newId = val;
                    break;
                }
            }

            if (newId == FactionId.None)
            {
                EditorUtility.DisplayDialog("Cannot Create",
                    "All FactionId enum values are already used. Add a new value to FactionId.cs first.",
                    "OK");
                return;
            }

            var folder = "Assets/_Project/Quests/Data/Factions";
            var assetName = $"Faction_{newId}";

            // Create the SO
            var def = ScriptableObject.CreateInstance<FactionDefinition>();
            def.factionId = newId;
            def.displayName = $"New Faction ({newId})";
            def.color = Color.gray;
            def.defaultAttitude = FactionAttitude.Neutral;
            def.defaultCombatRelation = FactionRelation.Neutral;
            def.reputationThresholds = new ReputationTier[]
            {
                new ReputationTier { tier = "Враг", value = -100, color = new Color(1f, 0.2f, 0.2f), ussClass = "rep-negative" },
                new ReputationTier { tier = "Недруг", value = -25, color = new Color(1f, 0.4f, 0.2f), ussClass = "rep-negative" },
                new ReputationTier { tier = "Нейтрален", value = 0, color = new Color(0.6f, 0.6f, 0.7f), ussClass = "rep-neutral" },
                new ReputationTier { tier = "Друг", value = 25, color = new Color(0.4f, 0.7f, 0.4f), ussClass = "rep-positive" },
                new ReputationTier { tier = "Уважаемый", value = 75, color = new Color(0.3f, 0.9f, 0.7f), ussClass = "rep-positive" },
            };

            var fullPath = System.IO.Path.Combine(folder, assetName + ".asset").Replace("\\", "/");
            AssetDatabase.CreateAsset(def, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NpcWorldInspector] Created new faction: {fullPath}");

            // Re-scan
            ScanFactions();

            // Select the new one
            _selectedFactionIndex = _factionResult.factions.FindIndex(f => f.factionId == newId);
            _factionDirty = false;
        }

        private void DeleteFaction(FactionEntry entry)
        {
            if (string.IsNullOrEmpty(entry.assetPath)) return;
            AssetDatabase.DeleteAsset(entry.assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NpcWorldInspector] Deleted faction: {entry.assetName}");
            ScanFactions();
        }

        private static string RelationEmoji(FactionRelation rel) => rel switch
        {
            FactionRelation.Allied => "🤝",
            FactionRelation.Hostile => "⚔",
            _ => "➖"
        };
    }
}
#endif
