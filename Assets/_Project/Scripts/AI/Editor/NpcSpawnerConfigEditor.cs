// Project C: Real-Time Combat Engine — T-NPC-02
// NpcSpawnerConfigEditor: custom editor with foldout groups for NpcSpawnerConfig.
//
// Groups:
//   1. Prefab & Debug       — npcPrefab, showDebugLogs
//   2. Spawn Rules          — radii, activation, limits, interval, chance, rate-limit, cycle
//   3. Ground & Chunk       — surface validation + chunk integration
//   4. Difficulty           — difficultyByDistance curve
//   5. Behavior             — behaviorType, passive thresholds
//   6. Visual & Skills      — visualConfig, npcSkillSet
//   7. Social: General      — socialEnabled, personality, idle, patrol, wander
//   8. Social: Combat       — flee, alarm, guard, threat, cover, surrender, post-combat
//   9. Social: Group & Memory — assignGroupOnSpawn, groupSpawnRadius, grudge, vengeance
//  10. Faction, Role & Loot — socialRole, faction, lootPrefab, lootTable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.AI;
using ProjectC.Factions;

[CustomEditor(typeof(NpcSpawnerConfig))]
public class NpcSpawnerConfigEditor : Editor
{
    private static bool _foldoutPrefab = true;
    private static bool _foldoutSpawn = true;
    private static bool _foldoutGround = true;
    private static bool _foldoutDifficulty = true;
    private static bool _foldoutBehavior = true;
    private static bool _foldoutVisual = true;
    private static bool _foldoutSocialGeneral = true;
    private static bool _foldoutSocialCombat = true;
    private static bool _foldoutSocialGroup = true;
    private static bool _foldoutFactionLoot = true;

    private SerializedProperty _npcPrefab;
    private SerializedProperty _showDebugLogs;
    private SerializedProperty _spawnRadiusMin;
    private SerializedProperty _spawnRadiusMax;
    private SerializedProperty _activationRadius;
    private SerializedProperty _maxAliveCount;
    private SerializedProperty _spawnCheckInterval;
    private SerializedProperty _spawnChance;
    private SerializedProperty _maxSpawnsPerPlayerPerMinute;
    private SerializedProperty _spawnMode;
    private SerializedProperty _totalSpawnLimit;
    private SerializedProperty _groundMask;
    private SerializedProperty _groundRaycastDistance;
    private SerializedProperty _minDistanceFromOtherNpc;
    private SerializedProperty _autoPopulateChunks;
    private SerializedProperty _chunkSpawnRadius;
    private SerializedProperty _maxAlivePerChunk;
    private SerializedProperty _difficultyByDistance;
    private SerializedProperty _behaviorType;
    private SerializedProperty _passiveAggroHpThreshold;
    private SerializedProperty _passiveMaxHitsPerMinute;
    private SerializedProperty _visualConfig;
    private SerializedProperty _npcSkillSet;
    private SerializedProperty _socialEnabled;
    private SerializedProperty _personalityConfig;
    private SerializedProperty _defaultIdleActivity;
    private SerializedProperty _patrolPattern;
    private SerializedProperty _patrolWaypoints;
    private SerializedProperty _idleAtWaypointSec;
    private SerializedProperty _wanderRadius;
    private SerializedProperty _canFlee;
    private SerializedProperty _fleeHpThreshold;
    private SerializedProperty _fleeAllySeekRadius;
    private SerializedProperty _alarmRadius;
    private SerializedProperty _allyDeathRadius;
    private SerializedProperty _isGuard;
    private SerializedProperty _threatEvaluationRange;
    private SerializedProperty _coverSeekRadius;
    private SerializedProperty _coverHpThreshold;
    private SerializedProperty _surrenderHpThreshold;
    private SerializedProperty _canSurrender;
    private SerializedProperty _enablePostCombat;
    private SerializedProperty _woundedDuration;
    private SerializedProperty _healHpThreshold;
    private SerializedProperty _assignGroupOnSpawn;
    private SerializedProperty _groupSpawnRadius;
    private SerializedProperty _enableGrudgeMemory;
    private SerializedProperty _grudgeDurationSec;
    private SerializedProperty _enableVengeanceMemory;
    private SerializedProperty _socialRole;
    private SerializedProperty _faction;
    private SerializedProperty _lootPrefab;
    private SerializedProperty _lootTable;

    // Faction dropdown cache
    private FactionDefinition[] _cachedFactions;
    private string[] _cachedFactionNames;

    private void OnEnable()
    {
        _npcPrefab = serializedObject.FindProperty("npcPrefab");
        _showDebugLogs = serializedObject.FindProperty("showDebugLogs");
        _spawnRadiusMin = serializedObject.FindProperty("spawnRadiusMin");
        _spawnRadiusMax = serializedObject.FindProperty("spawnRadiusMax");
        _activationRadius = serializedObject.FindProperty("activationRadius");
        _maxAliveCount = serializedObject.FindProperty("maxAliveCount");
        _spawnCheckInterval = serializedObject.FindProperty("spawnCheckInterval");
        _spawnChance = serializedObject.FindProperty("spawnChance");
        _maxSpawnsPerPlayerPerMinute = serializedObject.FindProperty("maxSpawnsPerPlayerPerMinute");
        _spawnMode = serializedObject.FindProperty("spawnMode");
        _totalSpawnLimit = serializedObject.FindProperty("totalSpawnLimit");
        _groundMask = serializedObject.FindProperty("groundMask");
        _groundRaycastDistance = serializedObject.FindProperty("groundRaycastDistance");
        _minDistanceFromOtherNpc = serializedObject.FindProperty("minDistanceFromOtherNpc");
        _autoPopulateChunks = serializedObject.FindProperty("autoPopulateChunks");
        _chunkSpawnRadius = serializedObject.FindProperty("chunkSpawnRadius");
        _maxAlivePerChunk = serializedObject.FindProperty("maxAlivePerChunk");
        _difficultyByDistance = serializedObject.FindProperty("difficultyByDistance");
        _behaviorType = serializedObject.FindProperty("behaviorType");
        _passiveAggroHpThreshold = serializedObject.FindProperty("passiveAggroHpThreshold");
        _passiveMaxHitsPerMinute = serializedObject.FindProperty("passiveMaxHitsPerMinute");
        _visualConfig = serializedObject.FindProperty("visualConfig");
        _npcSkillSet = serializedObject.FindProperty("npcSkillSet");
        _socialEnabled = serializedObject.FindProperty("socialEnabled");
        _personalityConfig = serializedObject.FindProperty("personalityConfig");
        _defaultIdleActivity = serializedObject.FindProperty("defaultIdleActivity");
        _patrolPattern = serializedObject.FindProperty("patrolPattern");
        _patrolWaypoints = serializedObject.FindProperty("patrolWaypoints");
        _idleAtWaypointSec = serializedObject.FindProperty("idleAtWaypointSec");
        _wanderRadius = serializedObject.FindProperty("wanderRadius");
        _canFlee = serializedObject.FindProperty("canFlee");
        _fleeHpThreshold = serializedObject.FindProperty("fleeHpThreshold");
        _fleeAllySeekRadius = serializedObject.FindProperty("fleeAllySeekRadius");
        _alarmRadius = serializedObject.FindProperty("alarmRadius");
        _allyDeathRadius = serializedObject.FindProperty("allyDeathRadius");
        _isGuard = serializedObject.FindProperty("isGuard");
        _threatEvaluationRange = serializedObject.FindProperty("threatEvaluationRange");
        _coverSeekRadius = serializedObject.FindProperty("coverSeekRadius");
        _coverHpThreshold = serializedObject.FindProperty("coverHpThreshold");
        _surrenderHpThreshold = serializedObject.FindProperty("surrenderHpThreshold");
        _canSurrender = serializedObject.FindProperty("canSurrender");
        _enablePostCombat = serializedObject.FindProperty("enablePostCombat");
        _woundedDuration = serializedObject.FindProperty("woundedDuration");
        _healHpThreshold = serializedObject.FindProperty("healHpThreshold");
        _assignGroupOnSpawn = serializedObject.FindProperty("assignGroupOnSpawn");
        _groupSpawnRadius = serializedObject.FindProperty("groupSpawnRadius");
        _enableGrudgeMemory = serializedObject.FindProperty("enableGrudgeMemory");
        _grudgeDurationSec = serializedObject.FindProperty("grudgeDurationSec");
        _enableVengeanceMemory = serializedObject.FindProperty("enableVengeanceMemory");
        _socialRole = serializedObject.FindProperty("socialRole");
        _faction = serializedObject.FindProperty("faction");
        _lootPrefab = serializedObject.FindProperty("lootPrefab");
        _lootTable = serializedObject.FindProperty("lootTable");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── 1. Prefab & Debug ──
        _foldoutPrefab = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPrefab, "▶ Prefab & Debug");
        if (_foldoutPrefab)
        {
            EditorGUILayout.PropertyField(_npcPrefab);
            EditorGUILayout.PropertyField(_showDebugLogs);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 2. Spawn Rules ──
        _foldoutSpawn = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSpawn, "▶ Spawn Rules");
        if (_foldoutSpawn)
        {
            EditorGUILayout.PropertyField(_spawnRadiusMin);
            EditorGUILayout.PropertyField(_spawnRadiusMax);
            EditorGUILayout.PropertyField(_activationRadius);
            EditorGUILayout.PropertyField(_maxAliveCount);
            EditorGUILayout.PropertyField(_spawnCheckInterval);
            EditorGUILayout.PropertyField(_spawnChance);
            EditorGUILayout.PropertyField(_maxSpawnsPerPlayerPerMinute);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_spawnMode);
            if (_spawnMode.enumValueIndex != 0) // not Infinite
                EditorGUILayout.PropertyField(_totalSpawnLimit);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 3. Ground & Chunk ──
        _foldoutGround = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGround, "▶ Ground & Chunk");
        if (_foldoutGround)
        {
            EditorGUILayout.PropertyField(_groundMask);
            EditorGUILayout.PropertyField(_groundRaycastDistance);
            EditorGUILayout.PropertyField(_minDistanceFromOtherNpc);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_autoPopulateChunks);
            if (_autoPopulateChunks.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_chunkSpawnRadius);
                EditorGUILayout.PropertyField(_maxAlivePerChunk);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 4. Difficulty ──
        _foldoutDifficulty = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutDifficulty, "▶ Difficulty Scaling");
        if (_foldoutDifficulty)
        {
            EditorGUILayout.PropertyField(_difficultyByDistance);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 5. Behavior ──
        _foldoutBehavior = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutBehavior, "▶ Behavior");
        if (_foldoutBehavior)
        {
            EditorGUILayout.PropertyField(_behaviorType);
            int behaviorIdx = _behaviorType.enumValueIndex;
            if (behaviorIdx == 1) // Passive
            {
                EditorGUILayout.PropertyField(_passiveAggroHpThreshold);
                EditorGUILayout.PropertyField(_passiveMaxHitsPerMinute);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(_passiveAggroHpThreshold);
                EditorGUILayout.PropertyField(_passiveMaxHitsPerMinute);
                EditorGUI.EndDisabledGroup();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 6. Visual & Skills ──
        _foldoutVisual = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutVisual, "▶ Visual & Skills");
        if (_foldoutVisual)
        {
            EditorGUILayout.PropertyField(_visualConfig);
            EditorGUILayout.PropertyField(_npcSkillSet);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 7. Social: General ──
        _foldoutSocialGeneral = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSocialGeneral, "▶ Social: General");
        if (_foldoutSocialGeneral)
        {
            EditorGUILayout.PropertyField(_socialEnabled);
            if (_socialEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(_personalityConfig);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_defaultIdleActivity);
                EditorGUILayout.PropertyField(_patrolPattern);
                EditorGUILayout.PropertyField(_patrolWaypoints, true);
                EditorGUILayout.PropertyField(_idleAtWaypointSec);
                EditorGUILayout.PropertyField(_wanderRadius);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 8. Social: Combat ──
        _foldoutSocialCombat = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSocialCombat, "▶ Social: Combat");
        if (_foldoutSocialCombat)
        {
            EditorGUILayout.PropertyField(_canFlee);
            if (_canFlee.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_fleeHpThreshold);
                EditorGUILayout.PropertyField(_fleeAllySeekRadius);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_alarmRadius);
            EditorGUILayout.PropertyField(_allyDeathRadius);
            EditorGUILayout.PropertyField(_isGuard);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_threatEvaluationRange);
            EditorGUILayout.PropertyField(_coverSeekRadius);
            EditorGUILayout.PropertyField(_coverHpThreshold);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_canSurrender);
            if (_canSurrender.boolValue)
                EditorGUILayout.PropertyField(_surrenderHpThreshold);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_enablePostCombat);
            if (_enablePostCombat.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_woundedDuration);
                EditorGUILayout.PropertyField(_healHpThreshold);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 9. Social: Group & Memory ──
        _foldoutSocialGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSocialGroup, "▶ Social: Group & Memory");
        if (_foldoutSocialGroup)
        {
            EditorGUILayout.PropertyField(_assignGroupOnSpawn);
            EditorGUILayout.PropertyField(_groupSpawnRadius);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_enableGrudgeMemory);
            if (_enableGrudgeMemory.boolValue)
                EditorGUILayout.PropertyField(_grudgeDurationSec);
            EditorGUILayout.PropertyField(_enableVengeanceMemory);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 10. Faction, Role & Loot ──
        _foldoutFactionLoot = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutFactionLoot, "▶ Faction, Role & Loot");
        if (_foldoutFactionLoot)
        {
            EditorGUILayout.PropertyField(_socialRole);
            DrawFactionPopup();
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_lootPrefab);
            EditorGUILayout.PropertyField(_lootTable);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFactionPopup()
    {
        if (_cachedFactions == null)
        {
            var guids = AssetDatabase.FindAssets("t:FactionDefinition");
            var list = new List<FactionDefinition>();
            var nameList = new List<string> { "(None)" };
            list.Add(null);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fd = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
                if (fd != null && !string.IsNullOrEmpty(fd.displayName))
                {
                    list.Add(fd);
                    nameList.Add($"{fd.displayName}  [{fd.factionId}]");
                }
            }

            _cachedFactions = list.ToArray();
            _cachedFactionNames = nameList.ToArray();
        }

        var currentFaction = _faction.objectReferenceValue as FactionDefinition;
        int currentIndex = 0;
        if (currentFaction != null)
        {
            for (int i = 1; i < _cachedFactions.Length; i++)
            {
                if (_cachedFactions[i] == currentFaction)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Faction");
        int newIndex = EditorGUILayout.Popup(currentIndex, _cachedFactionNames);
        EditorGUILayout.EndHorizontal();

        if (newIndex != currentIndex)
        {
            _faction.objectReferenceValue = _cachedFactions[newIndex];
        }
    }
}
