// Project C: Real-Time Combat Engine — T-NPC-01
// NpcBrainEditor: custom editor with foldout groups for NpcBrain.
//
// Groups:
//   1. Debug                 — _debugLog
//   2. Behavior              — behaviorType, passive thresholds
//   3. Quest/Reputation      — npcId, hostilityThreshold
//   4. Respawn               — respawn settings
//   5. Ranges & Movement     — aggroRange, attackRange, leashRange, moveSpeed, angularSpeed, stopping ratio, leash multiplier
//   6. Combat Tuning         — fallback cooldown, attack exit multiplier, throw arc speed
//   7. Social Integration    — socialEnabled, override timeout, force flee distance
//   8. Deck Nav              — warp radii, warn probe radius, warn cooldown
//   9. Platform Carry        — platformCarryEnabled, platformMask, probe settings, carryYaw, etc.
//  10. Tick                  — tickRate

using UnityEditor;
using UnityEngine;
using ProjectC.AI;

[CustomEditor(typeof(NpcBrain))]
public class NpcBrainEditor : Editor
{
    private static bool _foldoutDebug = true;
    private static bool _foldoutBehavior = true;
    private static bool _foldoutQuest = true;
    private static bool _foldoutRespawn = true;
    private static bool _foldoutRanges = true;
    private static bool _foldoutCombat = true;
    private static bool _foldoutSocial = true;
    private static bool _foldoutDeckNav = true;
    private static bool _foldoutPlatform = true;
    private static bool _foldoutTick = true;

    private SerializedProperty _debugLog;
    private SerializedProperty _behaviorType;
    private SerializedProperty _aggroHpThreshold;
    private SerializedProperty _maxHitsPerMinute;
    private SerializedProperty _passiveHitWindowSeconds;
    private SerializedProperty _aggroSearchMultiplier;
    private SerializedProperty _npcId;
    private SerializedProperty _hostilityThreshold;
    private SerializedProperty _respawnEnabled;
    private SerializedProperty _respawnDelaySeconds;
    private SerializedProperty _maxRespawns;
    private SerializedProperty _aggroRange;
    private SerializedProperty _attackRange;
    private SerializedProperty _leashRange;
    private SerializedProperty _moveSpeed;
    private SerializedProperty _angularSpeed;
    private SerializedProperty _stoppingDistanceRatio;
    private SerializedProperty _leashClearMultiplier;
    private SerializedProperty _fallbackAttackCooldown;
    private SerializedProperty _attackExitRangeMultiplier;
    private SerializedProperty _throwArcSpeed;
    private SerializedProperty _socialEnabled;
    private SerializedProperty _socialOverrideTimeout;
    private SerializedProperty _forceFleeDistance;
    private SerializedProperty _deckNavWarpRadii;
    private SerializedProperty _deckNavWarnProbeRadius;
    private SerializedProperty _deckNavWarnCooldown;
    private SerializedProperty _platformCarryEnabled;
    private SerializedProperty _platformMask;
    private SerializedProperty _platformProbeUp;
    private SerializedProperty _platformProbeDistance;
    private SerializedProperty _platformProbeRadius;
    private SerializedProperty _carryYaw;
    private SerializedProperty _platformMissFramesToClear;
    private SerializedProperty _useParentingOnShips;
    private SerializedProperty _tickRate;

    private void OnEnable()
    {
        _debugLog = serializedObject.FindProperty("_debugLog");
        _behaviorType = serializedObject.FindProperty("_behaviorType");
        _aggroHpThreshold = serializedObject.FindProperty("_aggroHpThreshold");
        _maxHitsPerMinute = serializedObject.FindProperty("_maxHitsPerMinute");
        _passiveHitWindowSeconds = serializedObject.FindProperty("_passiveHitWindowSeconds");
        _aggroSearchMultiplier = serializedObject.FindProperty("_aggroSearchMultiplier");
        _npcId = serializedObject.FindProperty("_npcId");
        _hostilityThreshold = serializedObject.FindProperty("_hostilityThreshold");
        _respawnEnabled = serializedObject.FindProperty("_respawnEnabled");
        _respawnDelaySeconds = serializedObject.FindProperty("_respawnDelaySeconds");
        _maxRespawns = serializedObject.FindProperty("_maxRespawns");
        _aggroRange = serializedObject.FindProperty("aggroRange");
        _attackRange = serializedObject.FindProperty("attackRange");
        _leashRange = serializedObject.FindProperty("leashRange");
        _moveSpeed = serializedObject.FindProperty("moveSpeed");
        _angularSpeed = serializedObject.FindProperty("angularSpeed");
        _stoppingDistanceRatio = serializedObject.FindProperty("_stoppingDistanceRatio");
        _leashClearMultiplier = serializedObject.FindProperty("_leashClearMultiplier");
        _fallbackAttackCooldown = serializedObject.FindProperty("_fallbackAttackCooldown");
        _attackExitRangeMultiplier = serializedObject.FindProperty("_attackExitRangeMultiplier");
        _throwArcSpeed = serializedObject.FindProperty("_throwArcSpeed");
        _socialEnabled = serializedObject.FindProperty("_socialEnabled");
        _socialOverrideTimeout = serializedObject.FindProperty("_socialOverrideTimeout");
        _forceFleeDistance = serializedObject.FindProperty("_forceFleeDistance");
        _deckNavWarpRadii = serializedObject.FindProperty("_deckNavWarpRadii");
        _deckNavWarnProbeRadius = serializedObject.FindProperty("_deckNavWarnProbeRadius");
        _deckNavWarnCooldown = serializedObject.FindProperty("_deckNavWarnCooldown");
        _platformCarryEnabled = serializedObject.FindProperty("_platformCarryEnabled");
        _platformMask = serializedObject.FindProperty("_platformMask");
        _platformProbeUp = serializedObject.FindProperty("_platformProbeUp");
        _platformProbeDistance = serializedObject.FindProperty("_platformProbeDistance");
        _platformProbeRadius = serializedObject.FindProperty("_platformProbeRadius");
        _carryYaw = serializedObject.FindProperty("_carryYaw");
        _platformMissFramesToClear = serializedObject.FindProperty("_platformMissFramesToClear");
        _useParentingOnShips = serializedObject.FindProperty("_useParentingOnShips");
        _tickRate = serializedObject.FindProperty("tickRate");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── 1. Debug ──
        _foldoutDebug = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutDebug, "▶ Debug");
        if (_foldoutDebug)
        {
            EditorGUILayout.PropertyField(_debugLog);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 2. Behavior ──
        _foldoutBehavior = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutBehavior, "▶ Behavior");
        if (_foldoutBehavior)
        {
            EditorGUILayout.PropertyField(_behaviorType);
            EditorGUILayout.PropertyField(_aggroHpThreshold);
            EditorGUILayout.PropertyField(_maxHitsPerMinute);
            EditorGUILayout.PropertyField(_passiveHitWindowSeconds);
            EditorGUILayout.PropertyField(_aggroSearchMultiplier);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 3. Quest/Reputation ──
        _foldoutQuest = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutQuest, "▶ Quest & Reputation");
        if (_foldoutQuest)
        {
            EditorGUILayout.PropertyField(_npcId);
            EditorGUILayout.PropertyField(_hostilityThreshold);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 4. Respawn ──
        _foldoutRespawn = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutRespawn, "▶ Respawn");
        if (_foldoutRespawn)
        {
            EditorGUILayout.PropertyField(_respawnEnabled);
            if (_respawnEnabled.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_respawnDelaySeconds);
                EditorGUILayout.PropertyField(_maxRespawns);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 5. Ranges & Movement ──
        _foldoutRanges = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutRanges, "▶ Ranges & Movement");
        if (_foldoutRanges)
        {
            EditorGUILayout.PropertyField(_aggroRange);
            EditorGUILayout.PropertyField(_attackRange);
            EditorGUILayout.PropertyField(_leashRange);
            EditorGUILayout.PropertyField(_moveSpeed);
            EditorGUILayout.PropertyField(_angularSpeed);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tuning", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_stoppingDistanceRatio);
            EditorGUILayout.PropertyField(_leashClearMultiplier);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 6. Combat Tuning ──
        _foldoutCombat = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutCombat, "▶ Combat Tuning");
        if (_foldoutCombat)
        {
            EditorGUILayout.PropertyField(_fallbackAttackCooldown);
            EditorGUILayout.PropertyField(_attackExitRangeMultiplier);
            EditorGUILayout.PropertyField(_throwArcSpeed);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 7. Social Integration ──
        _foldoutSocial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSocial, "▶ Social Integration");
        if (_foldoutSocial)
        {
            EditorGUILayout.PropertyField(_socialEnabled);
            if (_socialEnabled.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_socialOverrideTimeout);
                EditorGUILayout.PropertyField(_forceFleeDistance);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 8. Deck Nav ──
        _foldoutDeckNav = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutDeckNav, "▶ Deck Nav Tuning");
        if (_foldoutDeckNav)
        {
            EditorGUILayout.PropertyField(_deckNavWarpRadii, true);
            EditorGUILayout.PropertyField(_deckNavWarnProbeRadius);
            EditorGUILayout.PropertyField(_deckNavWarnCooldown);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 9. Platform Carry ──
        _foldoutPlatform = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPlatform, "▶ Platform Carry");
        if (_foldoutPlatform)
        {
            EditorGUILayout.PropertyField(_platformCarryEnabled);
            if (_platformCarryEnabled.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_platformMask);
                EditorGUILayout.PropertyField(_platformProbeUp);
                EditorGUILayout.PropertyField(_platformProbeDistance);
                EditorGUILayout.PropertyField(_platformProbeRadius);
                EditorGUILayout.PropertyField(_carryYaw);
                EditorGUILayout.PropertyField(_platformMissFramesToClear);
                EditorGUILayout.PropertyField(_useParentingOnShips);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 10. Tick ──
        _foldoutTick = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutTick, "▶ Tick");
        if (_foldoutTick)
        {
            EditorGUILayout.PropertyField(_tickRate);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
