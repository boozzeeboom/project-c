// Project C: Real-Time Combat Engine — T-NPC-S01
// NpcSocialBrainEditor: custom editor with foldout groups for NpcSocialBrain.
//
// Groups:
//   1. Debug                    — _debugLog
//   2. Faction & Personality    — faction, personalityConfig
//   3. Idle Activities          — idleActivity, patrol*, wander*
//   4. Socialize & Work         — socialize*, work*, sit*, sleep*
//   5. Flee                     — flee params
//   6. Grudge & Vengeance       — grudge, vengeance
//   7. Alarm & Threat           — alarm, threat assessment
//   8. Cover                    — cover seek, auto-detect
//   9. Surrender & Post-Combat  — surrender, post-combat
//  10. Tick & Emotion           — socialTickInterval, victoryEmotionDuration

using UnityEditor;
using UnityEngine;
using ProjectC.AI;

[CustomEditor(typeof(NpcSocialBrain))]
public class NpcSocialBrainEditor : Editor
{
    private static bool _foldoutDebug = true;
    private static bool _foldoutFaction = true;
    private static bool _foldoutIdle = true;
    private static bool _foldoutSocialize = true;
    private static bool _foldoutFlee = true;
    private static bool _foldoutGrudge = true;
    private static bool _foldoutAlarm = true;
    private static bool _foldoutCover = true;
    private static bool _foldoutSurrender = true;
    private static bool _foldoutTick = true;

    private SerializedProperty _debugLog;
    private SerializedProperty _faction;
    private SerializedProperty _personalityConfig;
    private SerializedProperty _idleActivity;
    private SerializedProperty _patrolPattern;
    private SerializedProperty _patrolWaypoints;
    private SerializedProperty _idleAtWaypointSec;
    private SerializedProperty _wanderRadius;
    private SerializedProperty _patrolSpeed;
    private SerializedProperty _patrolArrivalThreshold;
    private SerializedProperty _patrolStuckTimeout;
    private SerializedProperty _wanderCooldownMin;
    private SerializedProperty _wanderCooldownMax;
    private SerializedProperty _socializeSearchRadius;
    private SerializedProperty _socializeApproachThreshold;
    private SerializedProperty _socializeCooldownMin;
    private SerializedProperty _socializeCooldownMax;
    private SerializedProperty _workAnimIntervalMin;
    private SerializedProperty _workAnimIntervalMax;
    private SerializedProperty _sitSearchInterval;
    private SerializedProperty _sitSearchRadius;
    private SerializedProperty _sleepDurationMin;
    private SerializedProperty _sleepDurationMax;
    private SerializedProperty _canFlee;
    private SerializedProperty _fleeHpThreshold;
    private SerializedProperty _fleeAllySeekRadius;
    private SerializedProperty _fleeLeash;
    private SerializedProperty _fleeTimeout;
    private SerializedProperty _fleeStraightDistance;
    private SerializedProperty _fleeNearLeashDistance;
    private SerializedProperty _enableGrudgeMemory;
    private SerializedProperty _grudgeDurationSec;
    private SerializedProperty _enableVengeanceMemory;
    private SerializedProperty _alarmHearingRadius;
    private SerializedProperty _allyDeathRadius;
    private SerializedProperty _isGuard;
    private SerializedProperty _threatEvaluationRange;
    private SerializedProperty _cautiousRecklessnessThreshold;
    private SerializedProperty _afraidRecklessnessThreshold;
    private SerializedProperty _allyKillSearchMultiplier;
    private SerializedProperty _coverSeekRadius;
    private SerializedProperty _coverSwitchInterval;
    private SerializedProperty _coverHpThreshold;
    private SerializedProperty _coverAutoDetectAngles;
    private SerializedProperty _coverRaycastUp;
    private SerializedProperty _coverThreatFwdDistance;
    private SerializedProperty _coverNavSampleRadius;
    private SerializedProperty _canSurrender;
    private SerializedProperty _surrenderHpThreshold;
    private SerializedProperty _surrenderAllyRadius;
    private SerializedProperty _mercySurrenderRequired;
    private SerializedProperty _enablePostCombat;
    private SerializedProperty _woundedDuration;
    private SerializedProperty _healHpThreshold;
    private SerializedProperty _healRegenRate;
    private SerializedProperty _reinforcementSeekRadius;
    private SerializedProperty _postCombatGuardMin;
    private SerializedProperty _postCombatGuardMax;
    private SerializedProperty _woundedHpThreshold;
    private SerializedProperty _healingDurationMultiplier;
    private SerializedProperty _seekingReinforcementMultiplier;
    private SerializedProperty _socialTickInterval;
    private SerializedProperty _victoryEmotionDuration;

    private void OnEnable()
    {
        _debugLog = serializedObject.FindProperty("_debugLog");
        _faction = serializedObject.FindProperty("faction");
        _personalityConfig = serializedObject.FindProperty("personalityConfig");
        _idleActivity = serializedObject.FindProperty("idleActivity");
        _patrolPattern = serializedObject.FindProperty("patrolPattern");
        _patrolWaypoints = serializedObject.FindProperty("patrolWaypoints");
        _idleAtWaypointSec = serializedObject.FindProperty("idleAtWaypointSec");
        _wanderRadius = serializedObject.FindProperty("wanderRadius");
        _patrolSpeed = serializedObject.FindProperty("patrolSpeed");
        _patrolArrivalThreshold = serializedObject.FindProperty("patrolArrivalThreshold");
        _patrolStuckTimeout = serializedObject.FindProperty("patrolStuckTimeout");
        _wanderCooldownMin = serializedObject.FindProperty("wanderCooldownMin");
        _wanderCooldownMax = serializedObject.FindProperty("wanderCooldownMax");
        _socializeSearchRadius = serializedObject.FindProperty("socializeSearchRadius");
        _socializeApproachThreshold = serializedObject.FindProperty("socializeApproachThreshold");
        _socializeCooldownMin = serializedObject.FindProperty("socializeCooldownMin");
        _socializeCooldownMax = serializedObject.FindProperty("socializeCooldownMax");
        _workAnimIntervalMin = serializedObject.FindProperty("workAnimIntervalMin");
        _workAnimIntervalMax = serializedObject.FindProperty("workAnimIntervalMax");
        _sitSearchInterval = serializedObject.FindProperty("sitSearchInterval");
        _sitSearchRadius = serializedObject.FindProperty("sitSearchRadius");
        _sleepDurationMin = serializedObject.FindProperty("sleepDurationMin");
        _sleepDurationMax = serializedObject.FindProperty("sleepDurationMax");
        _canFlee = serializedObject.FindProperty("canFlee");
        _fleeHpThreshold = serializedObject.FindProperty("fleeHpThreshold");
        _fleeAllySeekRadius = serializedObject.FindProperty("fleeAllySeekRadius");
        _fleeLeash = serializedObject.FindProperty("fleeLeash");
        _fleeTimeout = serializedObject.FindProperty("fleeTimeout");
        _fleeStraightDistance = serializedObject.FindProperty("fleeStraightDistance");
        _fleeNearLeashDistance = serializedObject.FindProperty("fleeNearLeashDistance");
        _enableGrudgeMemory = serializedObject.FindProperty("enableGrudgeMemory");
        _grudgeDurationSec = serializedObject.FindProperty("grudgeDurationSec");
        _enableVengeanceMemory = serializedObject.FindProperty("enableVengeanceMemory");
        _alarmHearingRadius = serializedObject.FindProperty("alarmHearingRadius");
        _allyDeathRadius = serializedObject.FindProperty("allyDeathRadius");
        _isGuard = serializedObject.FindProperty("isGuard");
        _threatEvaluationRange = serializedObject.FindProperty("threatEvaluationRange");
        _cautiousRecklessnessThreshold = serializedObject.FindProperty("cautiousRecklessnessThreshold");
        _afraidRecklessnessThreshold = serializedObject.FindProperty("afraidRecklessnessThreshold");
        _allyKillSearchMultiplier = serializedObject.FindProperty("allyKillSearchMultiplier");
        _coverSeekRadius = serializedObject.FindProperty("coverSeekRadius");
        _coverSwitchInterval = serializedObject.FindProperty("coverSwitchInterval");
        _coverHpThreshold = serializedObject.FindProperty("coverHpThreshold");
        _coverAutoDetectAngles = serializedObject.FindProperty("coverAutoDetectAngles");
        _coverRaycastUp = serializedObject.FindProperty("coverRaycastUp");
        _coverThreatFwdDistance = serializedObject.FindProperty("coverThreatFwdDistance");
        _coverNavSampleRadius = serializedObject.FindProperty("coverNavSampleRadius");
        _canSurrender = serializedObject.FindProperty("canSurrender");
        _surrenderHpThreshold = serializedObject.FindProperty("surrenderHpThreshold");
        _surrenderAllyRadius = serializedObject.FindProperty("surrenderAllyRadius");
        _mercySurrenderRequired = serializedObject.FindProperty("mercySurrenderRequired");
        _enablePostCombat = serializedObject.FindProperty("enablePostCombat");
        _woundedDuration = serializedObject.FindProperty("woundedDuration");
        _healHpThreshold = serializedObject.FindProperty("healHpThreshold");
        _healRegenRate = serializedObject.FindProperty("healRegenRate");
        _reinforcementSeekRadius = serializedObject.FindProperty("reinforcementSeekRadius");
        _postCombatGuardMin = serializedObject.FindProperty("postCombatGuardMin");
        _postCombatGuardMax = serializedObject.FindProperty("postCombatGuardMax");
        _woundedHpThreshold = serializedObject.FindProperty("woundedHpThreshold");
        _healingDurationMultiplier = serializedObject.FindProperty("healingDurationMultiplier");
        _seekingReinforcementMultiplier = serializedObject.FindProperty("seekingReinforcementMultiplier");
        _socialTickInterval = serializedObject.FindProperty("socialTickInterval");
        _victoryEmotionDuration = serializedObject.FindProperty("victoryEmotionDuration");
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

        // ── 2. Faction & Personality ──
        _foldoutFaction = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutFaction, "▶ Faction & Personality");
        if (_foldoutFaction)
        {
            EditorGUILayout.PropertyField(_faction);
            EditorGUILayout.PropertyField(_personalityConfig);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 3. Idle Activities ──
        _foldoutIdle = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutIdle, "▶ Idle Activities");
        if (_foldoutIdle)
        {
            EditorGUILayout.PropertyField(_idleActivity);

            NpcIdleActivity currentActivity = (NpcIdleActivity)_idleActivity.enumValueIndex;

            // Patrol fields — always visible (referenced by Patrol, and patrolSpeed used by Wander too)
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Patrol", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_patrolPattern);
            EditorGUILayout.PropertyField(_patrolWaypoints, true);
            EditorGUILayout.PropertyField(_idleAtWaypointSec);
            EditorGUILayout.PropertyField(_patrolSpeed);
            EditorGUILayout.PropertyField(_patrolArrivalThreshold);
            EditorGUILayout.PropertyField(_patrolStuckTimeout);

            // Wander fields
            if (currentActivity == NpcIdleActivity.Wander)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Wander", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_wanderRadius);
                EditorGUILayout.PropertyField(_wanderCooldownMin);
                EditorGUILayout.PropertyField(_wanderCooldownMax);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 4. Socialize & Work ──
        _foldoutSocialize = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSocialize, "▶ Socialize & Work Tuning");
        if (_foldoutSocialize)
        {
            EditorGUILayout.LabelField("Socialize", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_socializeSearchRadius);
            EditorGUILayout.PropertyField(_socializeApproachThreshold);
            EditorGUILayout.PropertyField(_socializeCooldownMin);
            EditorGUILayout.PropertyField(_socializeCooldownMax);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Work", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_workAnimIntervalMin);
            EditorGUILayout.PropertyField(_workAnimIntervalMax);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Sit", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sitSearchInterval);
            EditorGUILayout.PropertyField(_sitSearchRadius);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Sleep", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sleepDurationMin);
            EditorGUILayout.PropertyField(_sleepDurationMax);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 5. Flee ──
        _foldoutFlee = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutFlee, "▶ Flee");
        if (_foldoutFlee)
        {
            EditorGUILayout.PropertyField(_canFlee);
            if (_canFlee.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_fleeHpThreshold);
                EditorGUILayout.PropertyField(_fleeAllySeekRadius);
                EditorGUILayout.PropertyField(_fleeLeash);
                EditorGUILayout.PropertyField(_fleeTimeout);
                EditorGUILayout.PropertyField(_fleeStraightDistance);
                EditorGUILayout.PropertyField(_fleeNearLeashDistance);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 6. Grudge & Vengeance ──
        _foldoutGrudge = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGrudge, "▶ Grudge & Vengeance");
        if (_foldoutGrudge)
        {
            EditorGUILayout.PropertyField(_enableGrudgeMemory);
            if (_enableGrudgeMemory.boolValue)
                EditorGUILayout.PropertyField(_grudgeDurationSec);
            EditorGUILayout.PropertyField(_enableVengeanceMemory);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 7. Alarm & Threat ──
        _foldoutAlarm = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutAlarm, "▶ Alarm & Threat");
        if (_foldoutAlarm)
        {
            EditorGUILayout.LabelField("Alarm", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_alarmHearingRadius);
            EditorGUILayout.PropertyField(_allyDeathRadius);
            EditorGUILayout.PropertyField(_isGuard);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Threat Assessment", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_threatEvaluationRange);
            EditorGUILayout.PropertyField(_cautiousRecklessnessThreshold);
            EditorGUILayout.PropertyField(_afraidRecklessnessThreshold);
            EditorGUILayout.PropertyField(_allyKillSearchMultiplier);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 8. Cover ──
        _foldoutCover = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutCover, "▶ Cover");
        if (_foldoutCover)
        {
            EditorGUILayout.PropertyField(_coverSeekRadius);
            EditorGUILayout.PropertyField(_coverSwitchInterval);
            EditorGUILayout.PropertyField(_coverHpThreshold);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Auto-Detect", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_coverAutoDetectAngles, true);
            EditorGUILayout.PropertyField(_coverRaycastUp);
            EditorGUILayout.PropertyField(_coverThreatFwdDistance);
            EditorGUILayout.PropertyField(_coverNavSampleRadius);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 9. Surrender & Post-Combat ──
        _foldoutSurrender = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSurrender, "▶ Surrender & Post-Combat");
        if (_foldoutSurrender)
        {
            EditorGUILayout.LabelField("Surrender", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_canSurrender);
            if (_canSurrender.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_surrenderHpThreshold);
                EditorGUILayout.PropertyField(_surrenderAllyRadius);
                EditorGUILayout.PropertyField(_mercySurrenderRequired);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Post-Combat", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enablePostCombat);
            if (_enablePostCombat.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_woundedDuration);
                EditorGUILayout.PropertyField(_healHpThreshold);
                EditorGUILayout.PropertyField(_healRegenRate);
                EditorGUILayout.PropertyField(_reinforcementSeekRadius);
                EditorGUILayout.PropertyField(_postCombatGuardMin);
                EditorGUILayout.PropertyField(_postCombatGuardMax);
                EditorGUILayout.PropertyField(_woundedHpThreshold);
                EditorGUILayout.PropertyField(_healingDurationMultiplier);
                EditorGUILayout.PropertyField(_seekingReinforcementMultiplier);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 10. Tick & Emotion ──
        _foldoutTick = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutTick, "▶ Tick & Emotion");
        if (_foldoutTick)
        {
            EditorGUILayout.PropertyField(_socialTickInterval);
            EditorGUILayout.PropertyField(_victoryEmotionDuration);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
