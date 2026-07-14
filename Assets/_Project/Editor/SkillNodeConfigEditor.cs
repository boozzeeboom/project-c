// Project C: Skill System Refactor — Phase C (v2)
// SkillNodeConfigEditor: Custom Editor с адаптивными секциями инспектора.
//
// Правила:
//   Social (category=Social):
//     - Только: Identity, Prerequisites, Effects, XP Cost, Tier Req, UI Layout
//     - НЕТ: discipline, subtype, weaponMask, isActive, AOE, Animation, throw/trap
//   Combat (category=Combat):
//     - Дисциплина: dropdown из 4 значений (Melee/Ranged/Defense/Placed)
//     - Подтип: фильтрованный dropdown по дисциплине
//       - Melee → None
//       - Ranged → None, Throwables, Bows, Crossbows
//       - Defense → None
//       - Placed → None, Traps
//     - isActive → AOE/Animation/Debug (только active)
//     - subtype=Throwables → throwRange/throwScatter/throwCount
//     - subtype=Traps → placeholder

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Skills;
using ProjectC.Equipment;

namespace ProjectC.Editor.Skills
{
    [CustomEditor(typeof(SkillNodeConfig))]
    public class SkillNodeConfigEditor : UnityEditor.Editor
    {
        // Identity
        private SerializedProperty _skillId;
        private SerializedProperty _displayName;
        private SerializedProperty _description;
        private SerializedProperty _icon;

        // Category & Discipline
        private SerializedProperty _category;
        private SerializedProperty _discipline;
        private SerializedProperty _subtype;
        private SerializedProperty _requiredWeaponMask;

        // Prerequisites & Effects
        private SerializedProperty _prerequisites;
        private SerializedProperty _effects;

        // XP Cost & Tiers
        private SerializedProperty _learnXpCost;
        private SerializedProperty _requiredStrengthTier;
        private SerializedProperty _requiredDexterityTier;
        private SerializedProperty _requiredIntelligenceTier;

        // UI Layout
        private SerializedProperty _treeX;
        private SerializedProperty _treeY;

        // Active / Passive
        private SerializedProperty _isActive;
        private SerializedProperty _cooldownSeconds;

        // Self Damage
        private SerializedProperty _allowSelfDamage;

        // Animation
        private SerializedProperty _attackClip;
        private SerializedProperty _attackClipSpeed;

        // AOE
        private SerializedProperty _aoeFormula;
        private SerializedProperty _aoeSize;
        private SerializedProperty _aoeConeAngleDeg;
        private SerializedProperty _aoeWidth;

        // Debug
        private SerializedProperty _debugVisualizeAoe;
        private SerializedProperty _debugVisualizeDuration;

        // Throwables
        private SerializedProperty _throwRange;
        private SerializedProperty _throwScatter;
        private SerializedProperty _throwCount;

        // Bows/Crossbows (R5)
        private SerializedProperty _rangedMaxRange;
        private SerializedProperty _rangedHitChance;

        // VFX: Cast
        private SerializedProperty _castVfxPrefab;
        private SerializedProperty _castSpawnPoint;
        private SerializedProperty _castVfxDuration;
        private SerializedProperty _castVfxDelay;

        // VFX: Projectile
        private SerializedProperty _projectileVfxPrefab;
        private SerializedProperty _projectileSpeed;
        private SerializedProperty _projectileArcHeight;
        private SerializedProperty _projectileTrailMaterial;

        // VFX: Impact
        private SerializedProperty _impactVfxPrefab;
        private SerializedProperty _impactScaleByDamage;
        private SerializedProperty _impactColorByDamageType;
        private SerializedProperty _impactVfxDuration;

        // VFX: 2D (future)
        private SerializedProperty _twoDVfxAnimation;
        private SerializedProperty _twoDFps;

        // Cached state
        private SkillCategory _cachedCategory;
        private CombatDiscipline _cachedDiscipline;
        private CombatSubtype _cachedSubtype;
        private bool _cachedIsActive;

        // Discipline dropdown: только 4 боевых значения
        private static readonly string[] DisciplineNames = { "Melee", "Ranged", "Defense", "Placed" };
        private static readonly int[] DisciplineValues = { (int)CombatDiscipline.Melee, (int)CombatDiscipline.Ranged, (int)CombatDiscipline.Defense, (int)CombatDiscipline.Placed };

        // Subtype options per discipline
        private static readonly string[] SubtypesNone = { "None" };
        private static readonly int[] SubtypeValuesNone = { (int)CombatSubtype.None };

        private static readonly string[] SubtypesRanged = { "None", "Throwables", "Bows", "Crossbows" };
        private static readonly int[] SubtypeValuesRanged = { (int)CombatSubtype.None, (int)CombatSubtype.Throwables, (int)CombatSubtype.Bows, (int)CombatSubtype.Crossbows };

        private static readonly string[] SubtypesPlaced = { "None", "Traps" };
        private static readonly int[] SubtypeValuesPlaced = { (int)CombatSubtype.None, (int)CombatSubtype.Traps };

        private void OnEnable()
        {
            _skillId = serializedObject.FindProperty("skillId");
            _displayName = serializedObject.FindProperty("displayName");
            _description = serializedObject.FindProperty("description");
            _icon = serializedObject.FindProperty("icon");

            _category = serializedObject.FindProperty("category");
            _discipline = serializedObject.FindProperty("discipline");
            _subtype = serializedObject.FindProperty("subtype");
            _requiredWeaponMask = serializedObject.FindProperty("requiredWeaponMask");

            _prerequisites = serializedObject.FindProperty("prerequisites");
            _effects = serializedObject.FindProperty("effects");

            _learnXpCost = serializedObject.FindProperty("_learnXpCost");
            _requiredStrengthTier = serializedObject.FindProperty("_requiredStrengthTier");
            _requiredDexterityTier = serializedObject.FindProperty("_requiredDexterityTier");
            _requiredIntelligenceTier = serializedObject.FindProperty("_requiredIntelligenceTier");

            _treeX = serializedObject.FindProperty("treeX");
            _treeY = serializedObject.FindProperty("treeY");

            _isActive = serializedObject.FindProperty("isActive");
            _cooldownSeconds = serializedObject.FindProperty("cooldownSeconds");

            _allowSelfDamage = serializedObject.FindProperty("_allowSelfDamage");

            _attackClip = serializedObject.FindProperty("attackClip");
            _attackClipSpeed = serializedObject.FindProperty("attackClipSpeed");

            _aoeFormula = serializedObject.FindProperty("aoeFormula");
            _aoeSize = serializedObject.FindProperty("aoeSize");
            _aoeConeAngleDeg = serializedObject.FindProperty("aoeConeAngleDeg");
            _aoeWidth = serializedObject.FindProperty("aoeWidth");

            _debugVisualizeAoe = serializedObject.FindProperty("debugVisualizeAoe");
            _debugVisualizeDuration = serializedObject.FindProperty("debugVisualizeDuration");

            _throwRange = serializedObject.FindProperty("throwRange");
            _throwScatter = serializedObject.FindProperty("throwScatter");
            _throwCount = serializedObject.FindProperty("throwCount");

            _rangedMaxRange = serializedObject.FindProperty("rangedMaxRange");
            _rangedHitChance = serializedObject.FindProperty("rangedHitChance");

            _castVfxPrefab = serializedObject.FindProperty("castVfxPrefab");
            _castSpawnPoint = serializedObject.FindProperty("castSpawnPoint");
            _castVfxDuration = serializedObject.FindProperty("castVfxDuration");
            _castVfxDelay = serializedObject.FindProperty("castVfxDelay");

            _projectileVfxPrefab = serializedObject.FindProperty("projectileVfxPrefab");
            _projectileSpeed = serializedObject.FindProperty("projectileSpeed");
            _projectileArcHeight = serializedObject.FindProperty("projectileArcHeight");
            _projectileTrailMaterial = serializedObject.FindProperty("projectileTrailMaterial");

            _impactVfxPrefab = serializedObject.FindProperty("impactVfxPrefab");
            _impactScaleByDamage = serializedObject.FindProperty("impactScaleByDamage");
            _impactColorByDamageType = serializedObject.FindProperty("impactColorByDamageType");
            _impactVfxDuration = serializedObject.FindProperty("impactVfxDuration");

            _twoDVfxAnimation = serializedObject.FindProperty("twoDVfxAnimation");
            _twoDFps = serializedObject.FindProperty("twoDFps");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _cachedCategory = (SkillCategory)_category.enumValueIndex;
            _cachedDiscipline = (CombatDiscipline)_discipline.enumValueIndex;
            _cachedSubtype = (CombatSubtype)_subtype.enumValueIndex;
            _cachedIsActive = _isActive.boolValue;

            bool isCombat = _cachedCategory == SkillCategory.Combat;

            // ===== Identity (always) =====
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_skillId);
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_description);
            EditorGUILayout.PropertyField(_icon);
            EditorGUILayout.Space();

            // ===== Category (always) =====
            EditorGUILayout.PropertyField(_category);

            // ===== Combat section (only for Combat) =====
            if (isCombat)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Combat Discipline", EditorStyles.boldLabel);

                // Discipline: custom dropdown — только 4 значения
                int discIdx = System.Array.IndexOf(DisciplineValues, (int)_cachedDiscipline);
                if (discIdx < 0) discIdx = 0;
                int newDiscIdx = EditorGUILayout.Popup("Discipline", discIdx, DisciplineNames);
                if (newDiscIdx != discIdx)
                {
                    _discipline.enumValueIndex = DisciplineValues[newDiscIdx];
                    _cachedDiscipline = (CombatDiscipline)DisciplineValues[newDiscIdx];
                    // При смене дисциплины — сбрасываем subtype на None
                    _subtype.enumValueIndex = (int)CombatSubtype.None;
                    _cachedSubtype = CombatSubtype.None;
                }

                // Subtype: filtered per discipline
                string[] subtypeNames;
                int[] subtypeVals;
                GetSubtypeOptions(_cachedDiscipline, out subtypeNames, out subtypeVals);

                int subIdx = System.Array.IndexOf(subtypeVals, (int)_cachedSubtype);
                if (subIdx < 0) subIdx = 0;
                int newSubIdx = EditorGUILayout.Popup("Subtype", subIdx, subtypeNames);
                if (newSubIdx != subIdx)
                {
                    _subtype.enumValueIndex = subtypeVals[newSubIdx];
                    _cachedSubtype = (CombatSubtype)subtypeVals[newSubIdx];
                }

                // Weapon mask
                EditorGUILayout.PropertyField(_requiredWeaponMask);

                EditorGUILayout.Space();

                // ===== Active / Passive =====
                EditorGUILayout.LabelField("Active vs Passive", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_isActive);
                EditorGUILayout.PropertyField(_cooldownSeconds);

                if (_isActive.boolValue)
                {
                    EditorGUILayout.PropertyField(_allowSelfDamage, new GUIContent("Allow Self Damage",
                        "Если включено — скилл может наносить урон самому кастеру (например, взрывчатка). " +
                        "По умолчанию ВЫКЛ — атакующий исключается из AOE-списка целей."));
                }

                // AOE + Animation (only for Active skills)
                if (_isActive.boolValue)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_attackClip);
                    EditorGUILayout.PropertyField(_attackClipSpeed);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("AOE Formula", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_aoeFormula);

                    var aoe = (AoeFormula)_aoeFormula.enumValueIndex;
                    if (aoe != AoeFormula.SingleTarget)
                    {
                        EditorGUILayout.PropertyField(_aoeSize);
                        if (aoe == AoeFormula.Cone)
                            EditorGUILayout.PropertyField(_aoeConeAngleDeg);
                        if (aoe == AoeFormula.Line || aoe == AoeFormula.Box)
                            EditorGUILayout.PropertyField(_aoeWidth);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_debugVisualizeAoe);
                    if (_debugVisualizeAoe.boolValue)
                        EditorGUILayout.PropertyField(_debugVisualizeDuration);
                }

                // ===== Type-specific settings =====
                if (_cachedSubtype == CombatSubtype.Throwables)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Throwables Settings", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Дальность, разброс (D6) и кол-во предметов за бросок.", MessageType.Info);
                    EditorGUILayout.PropertyField(_throwRange);
                    EditorGUILayout.PropertyField(_throwScatter);
                    EditorGUILayout.PropertyField(_throwCount);
                }

                if (_cachedSubtype == CombatSubtype.Bows || _cachedSubtype == CombatSubtype.Crossbows)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Bows / Crossbows Settings", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Дальность и шанс попадания D100 для луков/арбалетов.", MessageType.Info);
                    EditorGUILayout.PropertyField(_rangedMaxRange);
                    EditorGUILayout.PropertyField(_rangedHitChance);
                }

                if (_cachedSubtype == CombatSubtype.Traps)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Traps Settings", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Подтип Traps. Поля для ловушек — в будущих итерациях.", MessageType.Info);
                }

                // ===== VFX (Phase 0: data model) =====
                if (_isActive.boolValue)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("VFX: Cast", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Muzzle flash, заряд, свечение — срабатывает в начале каста.", MessageType.None);
                    EditorGUILayout.PropertyField(_castVfxPrefab);
                    if (_castVfxPrefab.objectReferenceValue != null)
                    {
                        EditorGUILayout.PropertyField(_castSpawnPoint);
                        EditorGUILayout.PropertyField(_castVfxDuration);
                        EditorGUILayout.PropertyField(_castVfxDelay);
                    }

                    // Projectile VFX: показываем для Ranged/Throwables, скрываем для Melee/Defense
                    bool showProjectile = _cachedDiscipline == CombatDiscipline.Ranged
                        || _cachedSubtype == CombatSubtype.Throwables;
                    if (showProjectile)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("VFX: Projectile", EditorStyles.boldLabel);
                        EditorGUILayout.HelpBox("Снаряд / граната в полёте. Trail, дуга, скорость.", MessageType.None);
                        EditorGUILayout.PropertyField(_projectileVfxPrefab);
                        EditorGUILayout.PropertyField(_projectileSpeed);
                        if (_cachedSubtype == CombatSubtype.Throwables)
                            EditorGUILayout.PropertyField(_projectileArcHeight);
                        EditorGUILayout.PropertyField(_projectileTrailMaterial);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("VFX: Impact", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Взрыв, искры, blood splat — при попадании в цель.", MessageType.None);
                    EditorGUILayout.PropertyField(_impactVfxPrefab);
                    if (_impactVfxPrefab.objectReferenceValue != null)
                    {
                        EditorGUILayout.PropertyField(_impactScaleByDamage);
                        EditorGUILayout.PropertyField(_impactColorByDamageType);
                        EditorGUILayout.PropertyField(_impactVfxDuration);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("VFX: 2D (Future)", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Покадровая 2D-анимация вместо ParticleSystem. Реализация — Phase 3.", MessageType.None);
                    EditorGUILayout.PropertyField(_twoDVfxAnimation);
                    if (_twoDVfxAnimation.objectReferenceValue != null)
                        EditorGUILayout.PropertyField(_twoDFps);
                }
            }

            EditorGUILayout.Space();

            // ===== Prerequisites & Effects (always) =====
            EditorGUILayout.LabelField("Prerequisites & Effects", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_prerequisites);
            EditorGUILayout.PropertyField(_effects);
            EditorGUILayout.Space();

            // ===== Costs & Tiers (always) =====
            EditorGUILayout.LabelField("Costs & Requirements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_learnXpCost);
            EditorGUILayout.PropertyField(_requiredStrengthTier);
            EditorGUILayout.PropertyField(_requiredDexterityTier);
            EditorGUILayout.PropertyField(_requiredIntelligenceTier);
            EditorGUILayout.Space();

            // ===== UI Layout (always) =====
            EditorGUILayout.LabelField("UI Layout", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_treeX);
            EditorGUILayout.PropertyField(_treeY);

            serializedObject.ApplyModifiedProperties();
        }

        private static void GetSubtypeOptions(CombatDiscipline disc, out string[] names, out int[] values)
        {
            switch (disc)
            {
                case CombatDiscipline.Ranged:
                    names = SubtypesRanged;
                    values = SubtypeValuesRanged;
                    break;
                case CombatDiscipline.Placed:
                    names = SubtypesPlaced;
                    values = SubtypeValuesPlaced;
                    break;
                default: // Melee, Defense
                    names = SubtypesNone;
                    values = SubtypeValuesNone;
                    break;
            }
        }
    }
}
#endif
