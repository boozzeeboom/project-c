// Project C: AI — T-NPC-AOE-EDITOR
// NpcSkillSetEditor: Custom Editor для NpcSkillSet с preview, override-индикацией и effective-значениями.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/06_NPC_SKILL_ASSIGNMENT_PLAN.md §8.3

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.AI;
using ProjectC.Skills;
using ProjectC.Combat.Core;

namespace ProjectC.Editor.AI
{
    [CustomEditor(typeof(NpcSkillSet))]
    public class NpcSkillSetEditor : UnityEditor.Editor
    {
        private SerializedProperty _selectionMode;
        private SerializedProperty _skills;
        private SerializedProperty _defaultAttack;

        private void OnEnable()
        {
            _selectionMode = serializedObject.FindProperty("selectionMode");
            _skills = serializedObject.FindProperty("skills");
            _defaultAttack = serializedObject.FindProperty("defaultAttack");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_selectionMode);

            int validCount = CountValidSkills();
            EditorGUILayout.LabelField($"  Valid skills: {validCount}", EditorStyles.miniLabel);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Skills", EditorStyles.boldLabel);

            for (int i = 0; i < _skills.arraySize; i++)
            {
                DrawSkillElement(i);
            }

            EditorGUILayout.Space(6);

            // Кнопка добавления
            if (GUILayout.Button("+ Add Skill"))
            {
                _skills.arraySize++;
                var el = _skills.GetArrayElementAtIndex(_skills.arraySize - 1);
                el.FindPropertyRelative("skillConfig").objectReferenceValue = null;
                el.FindPropertyRelative("priority").intValue = 50;
                el.FindPropertyRelative("maxHpPercent").floatValue = 1f;
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.PropertyField(_defaultAttack);

            serializedObject.ApplyModifiedProperties();

            // Валидация
            EditorGUILayout.Space(4);
            if (validCount == 0)
                EditorGUILayout.HelpBox("No valid skills! NPC will fallback to NpcDefaultDamageSource.", MessageType.Warning);
        }

        private void DrawSkillElement(int index)
        {
            var el = _skills.GetArrayElementAtIndex(index);
            var skillConfigProp = el.FindPropertyRelative("skillConfig");
            var config = skillConfigProp.objectReferenceValue as SkillNodeConfig;

            // --- Header строка ---
            string headerLabel;
            Color headerColor = Color.white;

            if (config != null)
            {
                string typeIcon = GetSkillTypeIcon(config);
                string name = string.IsNullOrEmpty(config.displayName) ? config.skillId : config.displayName;
                string effective = GetEffectiveSummary(el, config);
                headerLabel = $"[{index}] {typeIcon} {name}  —  {effective}";
                headerColor = el.FindPropertyRelative("priority").intValue > 0
                    ? new Color(0.85f, 0.85f, 1f)
                    : Color.gray;
            }
            else
            {
                headerLabel = $"[{index}] (empty — skillConfig not set)";
                headerColor = new Color(1f, 0.6f, 0.6f);
            }

            var bgColor = GUI.backgroundColor;
            GUI.backgroundColor = headerColor;
            GUI.backgroundColor = bgColor;

            var foldoutProp = el.FindPropertyRelative("overrideCooldown"); // reuse as dummy
            el.isExpanded = EditorGUILayout.Foldout(el.isExpanded, headerLabel, true);
            if (!el.isExpanded) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(skillConfigProp);

            if (config == null)
            {
                EditorGUI.indentLevel--;
                return;
            }

            // --- Preview (read-only) ---
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Preview (from SkillNodeConfig)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Discipline", config.discipline);
                EditorGUILayout.EnumPopup("Subtype", config.subtype);
                if (config.aoeFormula != AoeFormula.SingleTarget)
                    EditorGUILayout.TextField("AOE", $"{config.aoeFormula}  {config.aoeSize}m" +
                        (config.aoeFormula == AoeFormula.Cone ? $"  {config.aoeConeAngleDeg}°" : ""));
                if (config.subtype == CombatSubtype.Throwables)
                    EditorGUILayout.TextField("Throw", $"range={config.throwRange}m  scatter={config.throwScatter}  count={config.throwCount}");
                if (config.subtype == CombatSubtype.Bows || config.subtype == CombatSubtype.Crossbows)
                    EditorGUILayout.TextField("Ranged", $"maxRange={config.rangedMaxRange}m  hitChance={config.rangedHitChance:F0}%");
            }

            // --- Overrides ---
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Overrides (0/null = use default)", EditorStyles.boldLabel);

            var overrideCd = el.FindPropertyRelative("overrideCooldown");
            var overrideAnim = el.FindPropertyRelative("overrideAnimation");
            var overrideAnimSpd = el.FindPropertyRelative("overrideAnimationSpeed");
            var overrideDice = el.FindPropertyRelative("overrideDamageDice");
            var overrideDmg = el.FindPropertyRelative("overrideBaseDamage");
            var overrideRange = el.FindPropertyRelative("overrideRange");

            DrawOverrideField(overrideCd, config.cooldownSeconds, "Cooldown (s)", "{0:F1}s");
            DrawOverrideField(overrideRange, GetDefaultRange(config), "Range (m)", "{0:F1}m");
            DrawOverrideDiceField(overrideDice, GetDefaultDice(config), "Damage Dice");
            DrawOverrideIntField(overrideDmg, GetDefaultBaseDamage(config), "Base Damage");

            EditorGUILayout.PropertyField(overrideAnim);
            DrawOverrideFloatField(overrideAnimSpd, config.attackClipSpeed, "Anim Speed", "{0:F2}x");

            // --- AI Selection ---
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("AI Selection", EditorStyles.boldLabel);

            var priority = el.FindPropertyRelative("priority");
            var minHp = el.FindPropertyRelative("minHpPercent");
            var maxHp = el.FindPropertyRelative("maxHpPercent");

            EditorGUILayout.IntSlider(priority, 0, 100);
            float minHpVal = minHp.floatValue;
            float maxHpVal = maxHp.floatValue;
            EditorGUILayout.MinMaxSlider("HP% Range", ref minHpVal, ref maxHpVal, 0f, 1f);
            minHp.floatValue = minHpVal;
            maxHp.floatValue = maxHpVal;
            EditorGUILayout.LabelField($"  {minHpVal * 100:F0}% – {maxHpVal * 100:F0}%",
                EditorStyles.miniLabel);

            // --- Effective ---
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Effective (computed)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                float effCd = overrideCd.floatValue > 0f ? overrideCd.floatValue : config.cooldownSeconds;
                float effRange = overrideRange.floatValue > 0f ? overrideRange.floatValue : GetDefaultRange(config);
                DamageDice effDice = overrideDice.enumValueIndex > 0 ? (DamageDice)overrideDice.enumValueIndex : GetDefaultDice(config);
                int effDmg = overrideDmg.intValue > 0 ? overrideDmg.intValue : GetDefaultBaseDamage(config);

                string effStr = $"cd={effCd:F1}s  dmg=1{effDice}+{effDmg}  range={effRange:F1}m";
                if (config.subtype == CombatSubtype.Throwables)
                    effStr += $"  throwRange={config.throwRange:F1}m";
                if (config.subtype == CombatSubtype.Bows || config.subtype == CombatSubtype.Crossbows)
                    effStr += $"  hitChance={config.rangedHitChance:F0}%";

                EditorGUILayout.TextField(effStr);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);

            // Delete button
            if (GUILayout.Button($"✕ Remove [{index}]", GUILayout.Width(140)))
            {
                _skills.DeleteArrayElementAtIndex(index);
            }

            EditorGUILayout.Space(4);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static string GetSkillTypeIcon(SkillNodeConfig c)
        {
            if (c.subtype == CombatSubtype.Throwables) return "💣";
            if (c.subtype == CombatSubtype.Bows || c.subtype == CombatSubtype.Crossbows) return "🏹";
            if (c.aoeFormula == AoeFormula.Sphere) return "💥";
            if (c.aoeFormula != AoeFormula.SingleTarget) return "⚔";
            if (c.discipline == CombatDiscipline.Ranged) return "🎯";
            return "🗡";
        }

        private static string GetEffectiveSummary(SerializedProperty el, SkillNodeConfig c)
        {
            float cd = el.FindPropertyRelative("overrideCooldown").floatValue;
            if (cd <= 0) cd = c.cooldownSeconds;
            return $"cd={cd:F1}s";
        }

        private static float GetDefaultRange(SkillNodeConfig c)
        {
            if (c.subtype == CombatSubtype.Throwables && c.throwRange > 0f) return c.throwRange;
            if ((c.subtype == CombatSubtype.Bows || c.subtype == CombatSubtype.Crossbows) && c.rangedMaxRange > 0f) return c.rangedMaxRange;
            return 2f; // melee default
        }

        private static DamageDice GetDefaultDice(SkillNodeConfig c)
        {
            // SkillNodeConfig не хранит damageDice — fallback на d6
            return DamageDice.d6;
        }

        private static int GetDefaultBaseDamage(SkillNodeConfig c)
        {
            return 2; // fallback
        }

        private int CountValidSkills()
        {
            int count = 0;
            for (int i = 0; i < _skills.arraySize; i++)
            {
                var el = _skills.GetArrayElementAtIndex(i);
                var cfg = el.FindPropertyRelative("skillConfig").objectReferenceValue;
                int pri = el.FindPropertyRelative("priority").intValue;
                if (cfg != null && pri > 0) count++;
            }
            return count;
        }

        private static bool IsOverridden(float value) => value > 0f;
        private static bool IsOverridden(int value) => value > 0;
        private static bool IsOverriddenDice(int enumIndex) => enumIndex > 0; // d4 = 0

        private void DrawOverrideField(SerializedProperty prop, float defaultValue, string label, string format)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            bool overridden = IsOverridden(prop.floatValue);
            GUI.color = overridden ? new Color(0.5f, 1f, 0.5f) : Color.gray;
            GUILayout.Label(overridden ? "OVERRIDE" : $"default: {string.Format(format, defaultValue)}",
                EditorStyles.miniLabel, GUILayout.Width(overridden ? 75 : 110));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOverrideFloatField(SerializedProperty prop, float defaultValue, string label, string format)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            bool overridden = IsOverridden(prop.floatValue);
            GUI.color = overridden ? new Color(0.5f, 1f, 0.5f) : Color.gray;
            GUILayout.Label(overridden ? "OVERRIDE" : $"default: {string.Format(format, defaultValue)}",
                EditorStyles.miniLabel, GUILayout.Width(overridden ? 75 : 110));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOverrideIntField(SerializedProperty prop, int defaultValue, string label)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            bool overridden = IsOverridden(prop.intValue);
            GUI.color = overridden ? new Color(0.5f, 1f, 0.5f) : Color.gray;
            GUILayout.Label(overridden ? "OVERRIDE" : $"default: {defaultValue}",
                EditorStyles.miniLabel, GUILayout.Width(overridden ? 75 : 110));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOverrideDiceField(SerializedProperty prop, DamageDice defaultValue, string label)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            bool overridden = IsOverriddenDice(prop.enumValueIndex);
            GUI.color = overridden ? new Color(0.5f, 1f, 0.5f) : Color.gray;
            GUILayout.Label(overridden ? "OVERRIDE" : $"default: {defaultValue}",
                EditorStyles.miniLabel, GUILayout.Width(overridden ? 75 : 110));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
