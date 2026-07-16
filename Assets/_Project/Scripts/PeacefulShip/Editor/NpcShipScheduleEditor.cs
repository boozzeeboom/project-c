#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ProjectC.Docking.Network; // DockStationController
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Stations; // NpcShipSchedule

namespace ProjectC.PeacefulShip.EditorTools
{
    /// <summary>
    /// Custom Editor for NpcShipSchedule SO.
    /// - Foldouts: Identity, Routes, Traffic Shaping, Behavior, Cargo Trade
    /// - "Scan Scene Stations" to refresh the dropdown lists in route drawers
    /// - Inline validation: warns about missing/empty location IDs
    /// </summary>
    [CustomEditor(typeof(NpcShipSchedule))]
    public class NpcShipScheduleEditor : UnityEditor.Editor
    {
        private bool _foldIdentity = true;
        private bool _foldRoutes = true;
        private bool _foldTraffic = true;
        private bool _foldBehavior = true;
        private bool _foldCargo = true;

        private static List<DockStationController> _sceneStations;

        public override void OnInspectorGUI()
        {
            var schedule = (NpcShipSchedule)target;
            serializedObject.Update();

            // ── Toolbar ──
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField($"📋 {schedule.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔍 Scan Scene", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                RefreshSceneStations();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            int stationCount = (_sceneStations != null) ? _sceneStations.Count : -1;
            if (stationCount <= 0)
            {
                EditorGUILayout.HelpBox(
                    "No DockStationController found in open scenes.\n" +
                    "Click 'Scan Scene' after loading scenes with stations, or type IDs manually.",
                    MessageType.Info);
            }
            else
            {
                var locIds = _sceneStations
                    .Select(s => s.LocationId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct().ToList();
                EditorGUILayout.HelpBox(
                    $"📍 {stationCount} station(s) in scene: {string.Join(", ", locIds)}",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);

            // ── Identity foldout ──
            _foldIdentity = EditorGUILayout.Foldout(_foldIdentity, "🔷 Identity", true, EditorStyles.foldoutHeader);
            if (_foldIdentity)
            {
                EditorGUI.indentLevel++;
                DrawProp("scheduleId");
                DrawProp("displayName");
                DrawProp("scheduleType");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Routes foldout ──
            _foldRoutes = EditorGUILayout.Foldout(_foldRoutes, "🗺 Routes", true, EditorStyles.foldoutHeader);
            if (_foldRoutes)
            {
                EditorGUI.indentLevel++;
                var routesProp = serializedObject.FindProperty("routes");
                EditorGUILayout.PropertyField(routesProp, new GUIContent("Route Legs"), true);

                // Quick validation
                if (routesProp.isArray && routesProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Routes array is empty — NPC will have nowhere to go.", MessageType.Warning);
                }
                else if (routesProp.isArray)
                {
                    var validIds = new HashSet<string>(
                        (_sceneStations ?? new List<DockStationController>())
                            .Select(s => s.LocationId)
                            .Where(id => !string.IsNullOrEmpty(id)));

                    if (validIds.Count > 0)
                    {
                        for (int i = 0; i < routesProp.arraySize; i++)
                        {
                            var el = routesProp.GetArrayElementAtIndex(i);
                            var from = el.FindPropertyRelative("fromLocationId");
                            var to = el.FindPropertyRelative("toLocationId");
                            if (!string.IsNullOrEmpty(from.stringValue) && !validIds.Contains(from.stringValue))
                                EditorGUILayout.HelpBox($"Route[{i}].fromLocationId '{from.stringValue}' — no station in scene", MessageType.Error);
                            if (!string.IsNullOrEmpty(to.stringValue) && !validIds.Contains(to.stringValue))
                                EditorGUILayout.HelpBox($"Route[{i}].toLocationId '{to.stringValue}' — no station in scene", MessageType.Error);
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Traffic Shaping foldout ──
            _foldTraffic = EditorGUILayout.Foldout(_foldTraffic, "📊 Traffic Shaping (Gaussian)", true, EditorStyles.foldoutHeader);
            if (_foldTraffic)
            {
                EditorGUI.indentLevel++;
                DrawProp("meanArrivalIntervalSec");
                DrawProp("arrivalIntervalStdDev");
                DrawProp("minArrivalSpacingSec");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Behavior foldout ──
            _foldBehavior = EditorGUILayout.Foldout(_foldBehavior, "⏱ NPC Behavior", true, EditorStyles.foldoutHeader);
            if (_foldBehavior)
            {
                EditorGUI.indentLevel++;
                var minProp = serializedObject.FindProperty("minDwellTimeSec");
                var maxProp = serializedObject.FindProperty("maxDwellTimeSec");
                EditorGUILayout.PropertyField(minProp, new GUIContent("Min Dwell (s)"));
                EditorGUILayout.PropertyField(maxProp, new GUIContent("Max Dwell (s)"));
                if (maxProp.floatValue < minProp.floatValue)
                    EditorGUILayout.HelpBox("Max Dwell < Min Dwell — values will be ignored at runtime.", MessageType.Warning);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Cargo Trade foldout ──
            _foldCargo = EditorGUILayout.Foldout(_foldCargo, "📦 Cargo Trade (T-CARGO-NPC-01)", true, EditorStyles.foldoutHeader);
            if (_foldCargo)
            {
                EditorGUI.indentLevel++;
                var cargoProp = serializedObject.FindProperty("cargoTrade");
                if (cargoProp != null)
                {
                    EditorGUILayout.PropertyField(cargoProp, new GUIContent("Cargo Trade Config"), true);
                }
                EditorGUI.indentLevel--;
            }

            // ── Apply ──
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProp(string name)
        {
            var prop = serializedObject.FindProperty(name);
            if (prop != null)
                EditorGUILayout.PropertyField(prop);
        }

        private void OnEnable()
        {
            RefreshSceneStations();
        }

        private static void RefreshSceneStations()
        {
            _sceneStations = Object.FindObjectsByType<DockStationController>(
                FindObjectsInactive.Include).ToList();
        }
    }
}
#endif
