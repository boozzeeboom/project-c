#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Player.Editor
{
    /// <summary>
    /// Custom Editor for ShipController.
    /// Groups ~40+ serialized fields into logical foldout sections:
    /// Flight, Smoothing, Physics, Stabilization, Wind, Cargo, Modules, Identity.
    /// Adds Runtime Info panel (Play Mode only) for live debugging.
    /// Pattern: NpcShipControllerEditor.
    /// </summary>
    [CustomEditor(typeof(ShipController))]
    public class ShipControllerEditor : UnityEditor.Editor
    {
        // ── Foldout state ──
        private bool _foldFlight      = true;
        private bool _foldSmoothing   = false;
        private bool _foldPhysics     = false;
        private bool _foldStabilize   = false;
        private bool _foldWind        = false;
        private bool _foldCargo       = false;
        private bool _foldModules     = false;
        private bool _foldIdentity    = true;
        private bool _foldRuntime     = true;

        public override void OnInspectorGUI()
        {
            var ship = (ShipController)target;
            serializedObject.Update();

            // ═══════════════════════════════════════════
            // 🚀 Flight & Movement
            // ═══════════════════════════════════════════
            _foldFlight = EditorGUILayout.BeginFoldoutHeaderGroup(_foldFlight, "🚀 Flight & Movement");
            if (_foldFlight)
            {
                EditorGUI.indentLevel++;
                DrawProp("shipFlightClass");
                EditorGUILayout.Space(2);

                EditorGUILayout.LabelField("Thrust", EditorStyles.boldLabel);
                DrawProp("thrustForce");
                DrawProp("maxSpeed");
                EditorGUILayout.Space(2);

                EditorGUILayout.LabelField("Rotation", EditorStyles.boldLabel);
                DrawProp("yawForce");
                DrawProp("pitchForce");
                EditorGUILayout.Space(2);

                EditorGUILayout.LabelField("Vertical", EditorStyles.boldLabel);
                DrawProp("verticalForce");
                EditorGUILayout.Space(2);

                DrawProp("antiGravity");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            // 🔄 Smoothing
            // ═══════════════════════════════════════════
            _foldSmoothing = EditorGUILayout.BeginFoldoutHeaderGroup(_foldSmoothing, "🔄 Smoothing (Lerp/Damp)");
            if (_foldSmoothing)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Smooth Times", EditorStyles.boldLabel);
                DrawProp("yawSmoothTime");
                DrawProp("pitchSmoothTime");
                DrawProp("liftSmoothTime");
                DrawProp("thrustSmoothTime");
                EditorGUILayout.Space(2);

                EditorGUILayout.LabelField("Decay Times", EditorStyles.boldLabel);
                DrawProp("yawDecayTime");
                DrawProp("pitchDecayTime");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            // ⚖️ Physics
            // ═══════════════════════════════════════════
            _foldPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldPhysics, "⚖️ Physics & Mass");
            if (_foldPhysics)
            {
                EditorGUI.indentLevel++;
                DrawProp("massMultiplier");
                EditorGUILayout.LabelField("Base Mass per Class", EditorStyles.boldLabel);
                DrawProp("massLight");
                DrawProp("massMedium");
                DrawProp("massHeavy");
                DrawProp("massHeavyII");
                EditorGUILayout.Space(2);

                DrawProp("shipConstraints");
                EditorGUILayout.Space(2);

                EditorGUILayout.LabelField("Drag", EditorStyles.boldLabel);
                DrawProp("linearDrag");
                DrawProp("angularDrag");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            // 🎯 Stabilization
            // ═══════════════════════════════════════════
            _foldStabilize = EditorGUILayout.BeginFoldoutHeaderGroup(_foldStabilize, "🎯 Stabilization");
            if (_foldStabilize)
            {
                EditorGUI.indentLevel++;
                DrawProp("autoStabilize");
                DrawProp("pitchStabForce");
                DrawProp("rollStabForce");
                DrawProp("maxPitchAngle");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            // 🌬️ Wind & Environment
            // ═══════════════════════════════════════════
            _foldWind = EditorGUILayout.BeginFoldoutHeaderGroup(_foldWind, "🌬️ Wind & Corridors");
            if (_foldWind)
            {
                EditorGUI.indentLevel++;
                DrawProp("corridorSystem");
                EditorGUILayout.Space(2);

                EditorGUILayout.LabelField("Local Wind Zones", EditorStyles.boldLabel);
                DrawProp("windInfluence");
                DrawProp("windExposure");
                DrawProp("windDecayTime");
                EditorGUILayout.Space(2);

                EditorGUILayout.LabelField("Global Wind (WindManager)", EditorStyles.boldLabel);
                DrawProp("_globalWindEnabled");
                DrawProp("_globalWindForceScale");
                DrawProp("_globalWindVerticalFactor");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            // 📦 Cargo
            // ═══════════════════════════════════════════
            _foldCargo = EditorGUILayout.BeginFoldoutHeaderGroup(_foldCargo, "📦 Cargo Limits (base)");
            if (_foldCargo)
            {
                EditorGUI.indentLevel++;
                DrawProp("baseMaxCargoSlots");
                DrawProp("baseMaxCargoWeight");
                DrawProp("baseMaxCargoVolume");
                DrawProp("baseCargoPenaltyFactor");
                EditorGUILayout.HelpBox(
                    "Effective limits = base + module bonuses (ShipModuleManager).\n" +
                    "Resolved via ShipCargoRegistry.GetEffectiveLimits().",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            // 🔧 Modules & Meziy
            // ═══════════════════════════════════════════
            _foldModules = EditorGUILayout.BeginFoldoutHeaderGroup(_foldModules, "🔧 Modules, Meziy & Fuel");
            if (_foldModules)
            {
                EditorGUI.indentLevel++;
                DrawProp("moduleManager");
                DrawProp("meziyActivator");
                DrawProp("fuelSystem");
                DrawProp("meziyVisual");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            // 🔑 Identity & Debug
            // ═══════════════════════════════════════════
            _foldIdentity = EditorGUILayout.BeginFoldoutHeaderGroup(_foldIdentity, "🔑 Identity & Debug");
            if (_foldIdentity)
            {
                EditorGUI.indentLevel++;
                DrawProp("_customDisplayName");
                DrawProp("_keyItemData");
                EditorGUILayout.Space(2);
                DrawProp("_debugLog");
                DrawProp("_showLegacyMeziyHud");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();

            // ═══════════════════════════════════════════
            // 🟢 Runtime Info (Play Mode only)
            // ═══════════════════════════════════════════
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8);
                _foldRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(_foldRuntime, "🟢 Runtime Info");
                if (_foldRuntime)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(true);

                    EditorGUILayout.LabelField("Speed", $"{ship.CurrentSpeed:F1} m/s");
                    EditorGUILayout.LabelField("Forward Speed", $"{ship.ForwardSpeedMps:F1} m/s");
                    EditorGUILayout.LabelField("Vertical Speed", $"{ship.VerticalSpeed:F1} m/s");
                    EditorGUILayout.LabelField("Altitude", $"{ship.transform.position.y:F1} m");
                    EditorGUILayout.Space(2);

                    EditorGUILayout.LabelField("Flight Class", ship.ShipFlightClass.ToString());
                    EditorGUILayout.LabelField("Engine Running", ship.IsEngineRunning.ToString());
                    EditorGUILayout.LabelField("Is Docked", ship.IsDocked.ToString());
                    EditorGUILayout.LabelField("Is Hull Broken", ship.IsHullBroken.ToString());
                    EditorGUILayout.LabelField("Pilots", ship.PilotCount.ToString());
                    EditorGUILayout.Space(2);

                    EditorGUILayout.LabelField("Cargo Penalty", $"{ship.ServerCargoPenalty:F3}");
                    EditorGUILayout.LabelField("Resolved Cargo Class", ship.ResolvedCargoClass.ToString());
                    EditorGUILayout.Space(2);

                    var corridor = ship.ActiveCorridor;
                    if (corridor != null)
                        EditorGUILayout.LabelField("Corridor", $"{corridor.corridorId} — Status: {ship.CurrentAltitudeStatus}");
                    else
                        EditorGUILayout.LabelField("Corridor", "none");
                    EditorGUILayout.Space(2);

                    var angVel = ship.AngularVelocity;
                    EditorGUILayout.LabelField("Angular Velocity", $"({angVel.x:F2}, {angVel.y:F2}, {angVel.z:F2})");
                    EditorGUILayout.LabelField("Pitch Angle", $"{ship.PitchAngleDegrees:F1}°");
                    EditorGUILayout.LabelField("Roll Angle", $"{ship.RollAngleDegrees:F1}°");
                    EditorGUILayout.LabelField("Yaw Angle", $"{ship.YawAngleDegrees:F1}°");

                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        /// <summary>
        /// Draw a serialized property by name. Silently skips if not found
        /// (safe for renamed/removed fields).
        /// </summary>
        private void DrawProp(string name)
        {
            var prop = serializedObject.FindProperty(name);
            if (prop != null)
                EditorGUILayout.PropertyField(prop);
        }
    }
}
#endif
