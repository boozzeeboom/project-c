// Project C: Skill System Refactor — Phase C
// SkillNodeConfigEditor: Custom Editor с адаптивными секциями инспектора.
//
// Правила видимости полей:
//   - Всегда: Identity (skillId, displayName, description, icon)
//   - Всегда: Category, Discipline
//   - category == Combat → Subtype, requiredWeaponMask
//   - Всегда: Prerequisites, Effects, XP Cost, Tier Requirements, UI Layout
//   - Всегда: isActive
//   - isActive == true → AOE Formula, AOE Size, Cone Angle, Width, Attack Clip, Debug Viz
//   - subtype == Throwables → throwRange, throwScatter, throwCount
//   - subtype == Traps → (будущие поля — пока placeholder)

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

        // Cached enum values for conditional visibility
        private SkillCategory _cachedCategory;
        private CombatSubtype _cachedSubtype;
        private bool _cachedIsActive;

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
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Refresh cached values
            _cachedCategory = (SkillCategory)_category.enumValueIndex;
            _cachedSubtype = (CombatSubtype)_subtype.enumValueIndex;
            _cachedIsActive = _isActive.boolValue;

            // ===== Identity (always) =====
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_skillId);
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_description);
            EditorGUILayout.PropertyField(_icon);
            EditorGUILayout.Space();

            // ===== Category + Discipline (always) =====
            EditorGUILayout.LabelField("Category & Discipline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_category);
            EditorGUILayout.PropertyField(_discipline);

            // Subtype + Weapon Mask (only for Combat)
            if (_cachedCategory == SkillCategory.Combat)
            {
                EditorGUILayout.PropertyField(_subtype);
                EditorGUILayout.PropertyField(_requiredWeaponMask);
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
            EditorGUILayout.Space();

            // ===== Active vs Passive (always) =====
            EditorGUILayout.LabelField("Active vs Passive", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_isActive);

            // AOE + Animation (only for Active skills)
            if (_cachedIsActive)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_attackClip);
                EditorGUILayout.PropertyField(_attackClipSpeed);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("AOE Formula", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_aoeFormula);

                // Show context-dependent AOE fields
                var aoe = (AoeFormula)_aoeFormula.enumValueIndex;
                if (aoe != AoeFormula.SingleTarget)
                {
                    EditorGUILayout.PropertyField(_aoeSize);

                    if (aoe == AoeFormula.Cone)
                    {
                        EditorGUILayout.PropertyField(_aoeConeAngleDeg);
                    }

                    if (aoe == AoeFormula.Line || aoe == AoeFormula.Box)
                    {
                        EditorGUILayout.PropertyField(_aoeWidth);
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_debugVisualizeAoe);
                if (_debugVisualizeAoe.boolValue)
                {
                    EditorGUILayout.PropertyField(_debugVisualizeDuration);
                }
            }

            // ===== Throwables-specific (subtype == Throwables) =====
            if (_cachedSubtype == CombatSubtype.Throwables)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Throwables Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Эти поля активны только для навыков с подтипом Throwables.\n" +
                    "Дальность, разброс (D6) и кол-во предметов за бросок.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(_throwRange);
                EditorGUILayout.PropertyField(_throwScatter);
                EditorGUILayout.PropertyField(_throwCount);
            }

            // ===== Traps-specific (subtype == Traps) — placeholder =====
            if (_cachedSubtype == CombatSubtype.Traps)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Traps Settings (PLACEHOLDER)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Подтип Traps — пример. Поля для ловушек будут добавлены в будущих итерациях.",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
