// Project C: Custom Editor for SkillNodeConfig
// Adaptive inspector: shows/hides field groups based on category (Social/Combat),
// isActive, subtype (Throwables/Bows/Crossbows), and aoeFormula.
// Design: docs/Character/Skills/CUSTOM_EDITOR_DESIGN.md

using UnityEditor;
using UnityEngine;
using ProjectC.Skills;

namespace ProjectC.Editor
{
    [CustomEditor(typeof(SkillNodeConfig))]
    public class SkillNodeConfigEditor : UnityEditor.Editor
    {
        // --- SerializedProperties (cached once per OnEnable) ---
        private SerializedProperty _skillId;
        private SerializedProperty _displayName;
        private SerializedProperty _description;
        private SerializedProperty _icon;

        private SerializedProperty _category;
        private SerializedProperty _discipline;
        private SerializedProperty _subtype;

        private SerializedProperty _requiredWeaponMask;
        private SerializedProperty _prerequisites;
        private SerializedProperty _effects;

        private SerializedProperty _learnXpCost;
        private SerializedProperty _requiredStrengthTier;
        private SerializedProperty _requiredDexterityTier;
        private SerializedProperty _requiredIntelligenceTier;

        private SerializedProperty _knowledgeUnlockType;
        private SerializedProperty _knowledgeUnlockId;
        private SerializedProperty _knowledgeUnlockDescription;

        private SerializedProperty _treeX;
        private SerializedProperty _treeY;

        private SerializedProperty _allowSelfDamage;
        private SerializedProperty _isActive;
        private SerializedProperty _cooldownSeconds;

        private SerializedProperty _attackClip;
        private SerializedProperty _attackClipSpeed;

        private SerializedProperty _aoeFormula;
        private SerializedProperty _aoeSize;
        private SerializedProperty _aoeConeAngleDeg;
        private SerializedProperty _aoeWidth;

        private SerializedProperty _debugVisualizeAoe;
        private SerializedProperty _debugVisualizeDuration;

        private SerializedProperty _throwRange;
        private SerializedProperty _throwScatter;
        private SerializedProperty _throwCount;

        private SerializedProperty _rangedMaxRange;
        private SerializedProperty _rangedHitChance;

        private SerializedProperty _castVfxPrefab;
        private SerializedProperty _castSpawnPoint;
        private SerializedProperty _castVfxDuration;
        private SerializedProperty _castVfxDelay;

        private SerializedProperty _projectileVfxPrefab;
        private SerializedProperty _projectileSpeed;
        private SerializedProperty _projectileArcHeight;
        private SerializedProperty _projectileTrailMaterial;

        private SerializedProperty _impactVfxPrefab;
        private SerializedProperty _impactScaleByDamage;
        private SerializedProperty _impactColorByDamageType;
        private SerializedProperty _impactVfxDuration;

        private SerializedProperty _twoDVfxAnimation;
        private SerializedProperty _twoDFps;

        // --- Foldout state (per-asset, survives re-select) ---
        private static bool _foldoutIdentity = true;
        private static bool _foldoutCategory = true;
        private static bool _foldoutKnowledge = true;
        private static bool _foldoutCost = true;
        private static bool _foldoutLayout;
        private static bool _foldoutCombatCore = true;
        private static bool _foldoutCombatAnimation = true;
        private static bool _foldoutCombatAoe = true;
        private static bool _foldoutCombatThrowables = true;
        private static bool _foldoutCombatRanged = true;
        private static bool _foldoutVfxCast = true;
        private static bool _foldoutVfxProjectile = true;
        private static bool _foldoutVfxImpact = true;
        private static bool _foldoutVfx2D;

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

            _knowledgeUnlockType = serializedObject.FindProperty("knowledgeUnlockType");
            _knowledgeUnlockId = serializedObject.FindProperty("knowledgeUnlockId");
            _knowledgeUnlockDescription = serializedObject.FindProperty("knowledgeUnlockDescription");

            _treeX = serializedObject.FindProperty("treeX");
            _treeY = serializedObject.FindProperty("treeY");

            _allowSelfDamage = serializedObject.FindProperty("_allowSelfDamage");
            _isActive = serializedObject.FindProperty("isActive");
            _cooldownSeconds = serializedObject.FindProperty("cooldownSeconds");

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

            var config = (SkillNodeConfig)target;
            bool isCombat = config.category == SkillCategory.Combat;
            bool isActive = config.isActive;
            bool isThrowables = config.subtype == CombatSubtype.Throwables;
            bool isBowsOrCrossbows = config.subtype == CombatSubtype.Bows || config.subtype == CombatSubtype.Crossbows;
            var aoe = (AoeFormula)_aoeFormula.enumValueIndex;
            bool isAoe = aoe != AoeFormula.SingleTarget;

            // ── Header: Skill name + category toggle ──
            DrawHeader(config, isCombat);

            EditorGUILayout.Space(6);

            // ── Validation warnings ──
            DrawValidationWarnings(config, isCombat, isActive, aoe);

            // ═══════════════════════════════════════════
            //  Group 1: IDENTITY (always visible)
            // ═══════════════════════════════════════════
            _foldoutIdentity = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutIdentity, "📋 Identity");
            if (_foldoutIdentity)
            {
                EditorGUILayout.PropertyField(_skillId);
                EditorGUILayout.PropertyField(_displayName);
                EditorGUILayout.PropertyField(_description);
                EditorGUILayout.PropertyField(_icon);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            //  Group 2: CATEGORY & DISCIPLINE (always visible)
            // ═══════════════════════════════════════════
            _foldoutCategory = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutCategory, "🏷 Category & Discipline");
            if (_foldoutCategory)
            {
                EditorGUILayout.PropertyField(_category);
                EditorGUILayout.PropertyField(_discipline);
                EditorGUILayout.PropertyField(_subtype);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            //  Group 3: PREREQUISITES & EFFECTS (always)
            //  Arrays use their own built-in foldout — no BeginFoldoutHeaderGroup wrapper
            // ═══════════════════════════════════════════
            EditorGUILayout.LabelField("🔗 Prerequisites & Effects", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_prerequisites);
            EditorGUILayout.PropertyField(_effects);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f, 0.5f));

            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            //  Group 3.5: KNOWLEDGE UNLOCK (always, V3)
            // ═══════════════════════════════════════════
            _foldoutKnowledge = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutKnowledge, "🔒 Knowledge Unlock (V3)");
            if (_foldoutKnowledge)
            {
                EditorGUILayout.PropertyField(_knowledgeUnlockType);
                if ((KnowledgeUnlockType)_knowledgeUnlockType.enumValueIndex != KnowledgeUnlockType.AlwaysVisible
                    && (KnowledgeUnlockType)_knowledgeUnlockType.enumValueIndex != KnowledgeUnlockType.Hidden)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_knowledgeUnlockId);
                    EditorGUILayout.PropertyField(_knowledgeUnlockDescription);
                    EditorGUI.indentLevel--;
                }
                // HelpBox hints
                var ut = (KnowledgeUnlockType)_knowledgeUnlockType.enumValueIndex;
                if (ut == KnowledgeUnlockType.Hidden)
                    EditorGUILayout.HelpBox("Скрыт по умолчанию. Игрок увидит навык только после открытия знания (триггер, NPC, квест).", MessageType.Info);
                else if (ut == KnowledgeUnlockType.AlwaysVisible)
                    EditorGUILayout.HelpBox("Виден всегда без открытия. Используйте для базовых/стартовых навыков.", MessageType.Info);
                else if (ut == KnowledgeUnlockType.LearnFirst)
                    EditorGUILayout.HelpBox("Откроется автоматически при изучении любого prerequisite-навыка.", MessageType.Info);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            //  Group 4: COST & REQUIREMENTS (always)
            // ═══════════════════════════════════════════
            _foldoutCost = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutCost, "💰 Cost & Tier Requirements");
            if (_foldoutCost)
            {
                EditorGUILayout.PropertyField(_learnXpCost);
                EditorGUILayout.PropertyField(_requiredStrengthTier);
                EditorGUILayout.PropertyField(_requiredDexterityTier);
                EditorGUILayout.PropertyField(_requiredIntelligenceTier);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(2);

            // ═══════════════════════════════════════════
            //  Group 5: UI LAYOUT (always, collapsed)
            // ═══════════════════════════════════════════
            _foldoutLayout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutLayout, "📍 UI Layout (Skill Tree)");
            if (_foldoutLayout)
            {
                EditorGUILayout.PropertyField(_treeX);
                EditorGUILayout.PropertyField(_treeY);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ═══════════════════════════════════════════
            //  COMBAT-ONLY GROUPS (visible when category == Combat)
            // ═══════════════════════════════════════════
            if (isCombat)
            {
                EditorGUILayout.Space(6);

                // ── Combat Core ──
                _foldoutCombatCore = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutCombatCore, "⚔ Combat: Core");
                if (_foldoutCombatCore)
                {
                    EditorGUILayout.PropertyField(_requiredWeaponMask);
                    EditorGUILayout.PropertyField(_allowSelfDamage);
                    EditorGUILayout.PropertyField(_isActive);
                    EditorGUILayout.PropertyField(_cooldownSeconds);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                // Everything below is conditional on isActive
                if (isActive)
                {
                    EditorGUILayout.Space(2);

                    // ── Animation ──
                    _foldoutCombatAnimation = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutCombatAnimation, "🎬 Animation");
                    if (_foldoutCombatAnimation)
                    {
                        EditorGUILayout.PropertyField(_attackClip);
                        EditorGUILayout.PropertyField(_attackClipSpeed);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();

                    EditorGUILayout.Space(2);

                    // ── AOE ──
                    _foldoutCombatAoe = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutCombatAoe, "🎯 AOE Formula");
                    if (_foldoutCombatAoe)
                    {
                        EditorGUILayout.PropertyField(_aoeFormula);

                        if (isAoe)
                        {
                            DrawAoeFields(aoe);
                        }
                        else
                        {
                            // Show dimmed placeholder for SingleTarget
                            EditorGUI.indentLevel++;
                            EditorGUILayout.LabelField(
                                "─ Single Target (no AOE parameters needed) ─",
                                EditorStyles.miniLabel);
                            EditorGUI.indentLevel--;
                        }

                        EditorGUILayout.Space(2);
                        EditorGUILayout.PropertyField(_debugVisualizeAoe);
                        if (config.debugVisualizeAoe)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(_debugVisualizeDuration);
                            EditorGUI.indentLevel--;
                        }
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();

                    EditorGUILayout.Space(2);

                    // ── Throwables (subtype-conditional) ──
                    if (isThrowables)
                    {
                        _foldoutCombatThrowables = EditorGUILayout.BeginFoldoutHeaderGroup(
                            _foldoutCombatThrowables, "💣 Throwables");
                        if (_foldoutCombatThrowables)
                        {
                            EditorGUILayout.PropertyField(_throwRange);
                            EditorGUILayout.PropertyField(_throwScatter);
                            EditorGUILayout.PropertyField(_throwCount);
                        }
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }

                    // ── Ranged (subtype-conditional) ──
                    if (isBowsOrCrossbows)
                    {
                        _foldoutCombatRanged = EditorGUILayout.BeginFoldoutHeaderGroup(
                            _foldoutCombatRanged,
                            config.subtype == CombatSubtype.Bows ? "🏹 Bows" : "🔩 Crossbows");
                        if (_foldoutCombatRanged)
                        {
                            EditorGUILayout.PropertyField(_rangedMaxRange);
                            EditorGUILayout.PropertyField(_rangedHitChance);
                        }
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }

                    EditorGUILayout.Space(6);

                    // ── VFX: Cast ──
                    _foldoutVfxCast = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutVfxCast, "✨ VFX: Cast");
                    if (_foldoutVfxCast)
                    {
                        EditorGUILayout.PropertyField(_castVfxPrefab);
                        EditorGUILayout.PropertyField(_castSpawnPoint);
                        EditorGUILayout.PropertyField(_castVfxDuration);
                        EditorGUILayout.PropertyField(_castVfxDelay);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();

                    EditorGUILayout.Space(2);

                    // ── VFX: Projectile ──
                    _foldoutVfxProjectile = EditorGUILayout.BeginFoldoutHeaderGroup(
                        _foldoutVfxProjectile, "🚀 VFX: Projectile");
                    if (_foldoutVfxProjectile)
                    {
                        EditorGUILayout.PropertyField(_projectileVfxPrefab);
                        EditorGUILayout.PropertyField(_projectileSpeed);
                        EditorGUILayout.PropertyField(_projectileArcHeight);
                        EditorGUILayout.PropertyField(_projectileTrailMaterial);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();

                    EditorGUILayout.Space(2);

                    // ── VFX: Impact ──
                    _foldoutVfxImpact = EditorGUILayout.BeginFoldoutHeaderGroup(
                        _foldoutVfxImpact, "💥 VFX: Impact");
                    if (_foldoutVfxImpact)
                    {
                        EditorGUILayout.PropertyField(_impactVfxPrefab);
                        EditorGUILayout.PropertyField(_impactScaleByDamage);
                        EditorGUILayout.PropertyField(_impactColorByDamageType);
                        EditorGUILayout.PropertyField(_impactVfxDuration);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();

                    EditorGUILayout.Space(2);

                    // ── VFX: 2D ──
                    _foldoutVfx2D = EditorGUILayout.BeginFoldoutHeaderGroup(
                        _foldoutVfx2D, "🖼 VFX: 2D (Future)");
                    if (_foldoutVfx2D)
                    {
                        EditorGUILayout.PropertyField(_twoDVfxAnimation);
                        EditorGUILayout.PropertyField(_twoDFps);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Header: Skill name + big category toggle button ──
        private void DrawHeader(SkillNodeConfig config, bool isCombat)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Skill ID (bold, read-only style)
            EditorGUILayout.LabelField(
                $"Skill: {config.skillId}",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                });

            EditorGUILayout.Space(4);

            // Category toggle button
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = isCombat
                ? new Color(0.9f, 0.2f, 0.2f)   // red for Combat
                : new Color(0.2f, 0.7f, 0.3f);   // green for Social

            if (GUILayout.Button(
                isCombat ? "⚔  COMBAT" : "🗣  SOCIAL",
                GUILayout.Height(32),
                GUILayout.ExpandWidth(true)))
            {
                // Toggle category
                var newCat = isCombat ? SkillCategory.Social : SkillCategory.Combat;
                _category.enumValueIndex = (int)newCat;
                serializedObject.ApplyModifiedProperties();

                // Auto-reset combat fields when switching to Social
                if (newCat == SkillCategory.Social)
                {
                    ResetCombatFieldsToSocialDefaults();
                }
            }

            GUI.backgroundColor = oldBg;
            EditorGUILayout.EndVertical();
        }

        // ── Validation: real-time warnings for designer ──
        private void DrawValidationWarnings(
            SkillNodeConfig config, bool isCombat, bool isActive, AoeFormula aoe)
        {
            if (string.IsNullOrEmpty(config.skillId))
            {
                EditorGUILayout.HelpBox("⚠ skillId is empty — skill won't be findable at runtime.", MessageType.Warning);
            }

            if (isCombat && isActive && config.attackClip == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠ Active combat skill has no attackClip assigned. Animation won't play unless Animator has default 'Attack' state.",
                    MessageType.Warning);
            }

            if (isCombat && isActive && aoe != AoeFormula.SingleTarget && config.aoeSize <= 0f)
            {
                EditorGUILayout.HelpBox(
                    $"⚠ AOE is {aoe} but aoeSize = 0. No targets will be hit.",
                    MessageType.Warning);
            }
        }

        // ── AOE fields with conditional visibility per formula ──
        private void DrawAoeFields(AoeFormula aoe)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(_aoeSize);

            bool showConeAngle = aoe == AoeFormula.Cone;
            bool showWidth = aoe == AoeFormula.Line || aoe == AoeFormula.Box;

            if (showConeAngle)
            {
                EditorGUILayout.PropertyField(_aoeConeAngleDeg);
            }
            else
            {
                // Show dimmed
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_aoeConeAngleDeg);
                }
            }

            if (showWidth)
            {
                EditorGUILayout.PropertyField(_aoeWidth);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_aoeWidth);
                }
            }

            EditorGUI.indentLevel--;
        }

        // ── Auto-reset combat fields when switching to Social ──
        private void ResetCombatFieldsToSocialDefaults()
        {
            _isActive.boolValue = false;
            _cooldownSeconds.floatValue = 0.5f;
            _requiredWeaponMask.enumValueFlag = 0;
            _allowSelfDamage.boolValue = false;

            _attackClip.objectReferenceValue = null;
            _attackClipSpeed.floatValue = 1f;

            _aoeFormula.enumValueIndex = 0; // SingleTarget
            _aoeSize.floatValue = 0f;
            _aoeConeAngleDeg.floatValue = 60f;
            _aoeWidth.floatValue = 0f;
            _debugVisualizeAoe.boolValue = false;
            _debugVisualizeDuration.floatValue = 0.6f;

            _throwRange.floatValue = 25f;
            _throwScatter.intValue = 3;
            _throwCount.intValue = 1;

            _rangedMaxRange.floatValue = 30f;
            _rangedHitChance.floatValue = 70f;

            // Clear VFX references
            _castVfxPrefab.objectReferenceValue = null;
            _castSpawnPoint.enumValueIndex = 0;
            _castVfxDuration.floatValue = 0.5f;
            _castVfxDelay.floatValue = 0f;

            _projectileVfxPrefab.objectReferenceValue = null;
            _projectileSpeed.floatValue = 30f;
            _projectileArcHeight.floatValue = 0f;
            _projectileTrailMaterial.objectReferenceValue = null;

            _impactVfxPrefab.objectReferenceValue = null;
            _impactScaleByDamage.boolValue = false;
            _impactColorByDamageType.boolValue = true;
            _impactVfxDuration.floatValue = 0.4f;

            _twoDVfxAnimation.objectReferenceValue = null;
            _twoDFps.intValue = 12;

            _subtype.enumValueIndex = 0; // None
            _discipline.enumValueIndex = 0; // None

            serializedObject.ApplyModifiedProperties();
        }
    }
}
