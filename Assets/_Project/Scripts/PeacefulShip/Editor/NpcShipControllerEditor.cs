#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Stations; // NpcShipController, NpcShipSchedule

namespace ProjectC.PeacefulShip.EditorTools
{
    /// <summary>
    /// Custom Editor for NpcShipController.
    /// - Foldouts: Schedule & Identity, Movement, Anti-gravity Boost, Avoidance, Debug
    /// - "Create New Schedule" button (creates SO, auto-fills from ship context)
    /// - Inline schedule readout (links to open the SO)
    /// - Quick validation hints
    /// </summary>
    [CustomEditor(typeof(NpcShipController))]
    public class NpcShipControllerEditor : UnityEditor.Editor
    {
        private bool _foldSchedule = true;
        private bool _foldMovement = true;
        private bool _foldAntiGrav = true;
        private bool _foldAvoidance = false;
        private bool _foldDebug = false;

        private SerializedObject _schedSo;
        private bool _schedFoldout;

        private const string DefaultSchedulePath = "Assets/_Project/Resources/PeacefulShip";

        public override void OnInspectorGUI()
        {
            var ctrl = (NpcShipController)target;
            serializedObject.Update();

            // ── Schedule & Identity ──
            _foldSchedule = EditorGUILayout.Foldout(_foldSchedule,
                "🔷 Schedule & Identity", true, EditorStyles.foldoutHeader);
            if (_foldSchedule)
            {
                EditorGUI.indentLevel++;

                // Schedule field + buttons
                var schedProp = serializedObject.FindProperty("schedule");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(schedProp, new GUIContent("Schedule (SO)"));
                if (GUILayout.Button("⊕", GUILayout.Width(26), GUILayout.Height(18)))
                {
                    CreateNewSchedule(ctrl);
                }
                if (GUILayout.Button("◎", GUILayout.Width(26), GUILayout.Height(18)))
                {
                    if (schedProp.objectReferenceValue != null)
                        EditorGUIUtility.PingObject(schedProp.objectReferenceValue);
                }
                EditorGUILayout.EndHorizontal();

                serializedObject.ApplyModifiedProperties();

                // Inline schedule readout
                var schedule = ctrl.Schedule;
                if (schedule != null)
                {
                    if (_schedSo == null || _schedSo.targetObject != schedule)
                        _schedSo = new SerializedObject(schedule);

                    _schedFoldout = EditorGUILayout.Foldout(_schedFoldout,
                        $"📄 {schedule.name} (inline)", true, EditorStyles.foldoutHeader);

                    if (_schedFoldout)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginDisabledGroup(true);

                        _schedSo.Update();
                        EditorGUILayout.LabelField("Schedule ID", schedule.scheduleId);
                        EditorGUILayout.LabelField("Display Name", schedule.displayName);
                        EditorGUILayout.LabelField("Type", schedule.scheduleType.ToString());

                        var routesProp = _schedSo.FindProperty("routes");
                        if (routesProp != null)
                        {
                            EditorGUILayout.LabelField("Routes",
                                routesProp.isArray ? $"{routesProp.arraySize} leg(s)" : "null");
                        }

                        EditorGUILayout.LabelField("Min/Max Dwell",
                            $"{schedule.minDwellTimeSec:F0} / {schedule.maxDwellTimeSec:F0} s");

                        var cargo = schedule.cargoTrade;
                        if (cargo != null && cargo.buyItems != null && cargo.buyItems.Length > 0)
                            EditorGUILayout.LabelField("Cargo Items", $"{cargo.buyItems.Length} item(s)");

                        EditorGUI.EndDisabledGroup();
                        EditorGUI.indentLevel--;

                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("📝 Edit Schedule", GUILayout.Height(22)))
                        {
                            Selection.activeObject = schedule;
                            EditorGUIUtility.PingObject(schedule);
                        }
                        if (GUILayout.Button("📋 Duplicate", GUILayout.Height(22)))
                        {
                            DuplicateSchedule(ctrl, schedule);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "No NpcShipSchedule assigned.\n" +
                        "Click ⊕ to create one, or drag an existing .asset here.",
                        MessageType.Warning);
                }

                // Identity
                DrawProp("npcInstanceId");

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Movement ──
            _foldMovement = EditorGUILayout.Foldout(_foldMovement,
                "🚀 Movement (server-only)", true, EditorStyles.foldoutHeader);
            if (_foldMovement)
            {
                EditorGUI.indentLevel++;

                // M3.2.N: Class-based speed profile with override
                var ship = ctrl.GetComponent<ProjectC.Player.ShipController>();
                var flightClass = ship != null ? ship.ShipFlightClass : ProjectC.Player.ShipFlightClass.Medium;
                var (baseLift, baseCruise, baseApproach, baseYaw) = Stations.NpcShipController.GetClassBaseSpeeds(flightClass);

                var overrideProp = serializedObject.FindProperty("overrideClassSpeeds");
                bool isOverride = overrideProp != null && overrideProp.boolValue;

                EditorGUILayout.LabelField("Ship Class", flightClass.ToString(), EditorStyles.boldLabel);
                if (!isOverride)
                {
                    EditorGUILayout.LabelField("   Class Base Speeds",
                        $"Lift={baseLift:F0}  Cruise={baseCruise:F0}  Approach={baseApproach:F0}  Yaw={baseYaw:F0}°/s",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(2);
                DrawProp("overrideClassSpeeds");

                EditorGUILayout.Space(2);
                if (isOverride)
                {
                    EditorGUILayout.LabelField("   Absolute Speeds (custom)", EditorStyles.boldLabel);
                    DrawProp("customLiftSpeed");
                    DrawProp("customCruiseSpeed");
                    DrawProp("customApproachSpeed");
                    DrawProp("customMaxYawRate");
                }
                else
                {
                    EditorGUILayout.LabelField("   Multipliers (× class base)", EditorStyles.boldLabel);
                    DrawProp("liftSpeedMult");
                    DrawProp("cruiseSpeedMult");
                    DrawProp("approachSpeedMult");
                    DrawProp("maxYawRateMult");
                }

                EditorGUILayout.Space(2);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("   Effective Speeds",
                    $"Lift={ctrl.LiftSpeed:F1}  Cruise={ctrl.CruiseSpeed:F1}  Approach={ctrl.ApproachSpeed:F1}  Yaw={ctrl.MaxYawRate:F0}°/s",
                    EditorStyles.miniLabel);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(4);
                DrawProp("npcThrustMult");
                DrawProp("npcYawMult");
                DrawProp("npcArrivalToleranceMeters");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Anti-gravity Boost ──
            _foldAntiGrav = EditorGUILayout.Foldout(_foldAntiGrav,
                "🪶 Anti-gravity Boost (Q8)", true, EditorStyles.foldoutHeader);
            if (_foldAntiGrav)
            {
                EditorGUI.indentLevel++;
                DrawProp("antiGravityBoostDuration");
                DrawProp("antiGravityBoostValue");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Avoidance ──
            _foldAvoidance = EditorGUILayout.Foldout(_foldAvoidance,
                "⚠ Ship-to-Ship Avoidance (M3.2)", true, EditorStyles.foldoutHeader);
            if (_foldAvoidance)
            {
                EditorGUI.indentLevel++;
                DrawProp("avoidSeparateSpeed");
                DrawProp("avoidSeparateTime");
                DrawProp("avoidStopTime");
                DrawProp("avoidBackOffSpeed");
                DrawProp("avoidBackOffTime");
                DrawProp("avoidTimeout");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Debug ──
            _foldDebug = EditorGUILayout.Foldout(_foldDebug,
                "🐞 Debug", true, EditorStyles.foldoutHeader);
            if (_foldDebug)
            {
                EditorGUI.indentLevel++;
                DrawProp("debugMode");
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProp(string name)
        {
            var prop = serializedObject.FindProperty(name);
            if (prop != null)
                EditorGUILayout.PropertyField(prop);
        }

        // ── Create New Schedule ──

        private void CreateNewSchedule(NpcShipController ctrl)
        {
            string currentPath = DefaultSchedulePath;
            if (ctrl.Schedule != null)
            {
                string existingPath = AssetDatabase.GetAssetPath(ctrl.Schedule);
                if (!string.IsNullOrEmpty(existingPath))
                    currentPath = Path.GetDirectoryName(existingPath);
            }

            if (!Directory.Exists(currentPath))
                Directory.CreateDirectory(currentPath);

            string shipName = ctrl.gameObject.name.Replace(" ", "_").Replace("(", "").Replace(")", "");
            string defaultName = $"NpcShipSchedule_{shipName}";
            string savePath = EditorUtility.SaveFilePanelInProject(
                "Create New NpcShipSchedule",
                defaultName,
                "asset",
                "Choose save location for the new schedule",
                currentPath);

            if (string.IsNullOrEmpty(savePath)) return;

            var newSchedule = CreateInstance<NpcShipSchedule>();
            newSchedule.scheduleId = $"SCH-NPC-{shipName.ToUpperInvariant()}";
            newSchedule.displayName = $"Расписание {ctrl.gameObject.name}";
            newSchedule.scheduleType = NpcShipSchedule.ScheduleType.RoundTrip;
            newSchedule.routes = new NpcShipRoute[1]
            {
                new NpcShipRoute
                {
                    fromLocationId = "",
                    toLocationId = "",
                    dwellTimeSec = 60f,
                    flightDurationSec = 120f
                }
            };
            newSchedule.minDwellTimeSec = 60f;
            newSchedule.maxDwellTimeSec = 90f;

            string finalPath = AssetDatabase.GenerateUniqueAssetPath(savePath);
            AssetDatabase.CreateAsset(newSchedule, finalPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Assign to controller
            var so = new SerializedObject(ctrl);
            so.FindProperty("schedule").objectReferenceValue = newSchedule;
            so.ApplyModifiedProperties();

            _schedSo = new SerializedObject(newSchedule);

            EditorGUIUtility.PingObject(newSchedule);
            Debug.Log($"[NpcShipControllerEditor] Created new schedule: {finalPath}");
        }

        // ── Duplicate Schedule ──

        private void DuplicateSchedule(NpcShipController ctrl, NpcShipSchedule src)
        {
            string srcPath = AssetDatabase.GetAssetPath(src);
            string srcDir = Path.GetDirectoryName(srcPath);
            string srcName = Path.GetFileNameWithoutExtension(srcPath);

            string savePath = EditorUtility.SaveFilePanelInProject(
                "Duplicate NpcShipSchedule",
                $"{srcName}_Copy",
                "asset",
                "Choose save location",
                srcDir);

            if (string.IsNullOrEmpty(savePath)) return;

            string finalPath = AssetDatabase.GenerateUniqueAssetPath(savePath);
            AssetDatabase.CopyAsset(srcPath, finalPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var newSchedule = AssetDatabase.LoadAssetAtPath<NpcShipSchedule>(finalPath);

            var so = new SerializedObject(ctrl);
            so.FindProperty("schedule").objectReferenceValue = newSchedule;
            so.ApplyModifiedProperties();

            _schedSo = new SerializedObject(newSchedule);

            EditorGUIUtility.PingObject(newSchedule);
            Debug.Log($"[NpcShipControllerEditor] Duplicated schedule: {finalPath}");
        }
    }
}
#endif
