// T-SMITH01: Кастомный инспектор для NpcBlacksmithConfig — настройка доступных тиров кузнеца
// (Бронза, Сталь, Композит, Антигравий), множителей цен и кастомных ресурсов.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectC.Quests.Editor
{
    [CustomEditor(typeof(NpcBlacksmithConfig))]
    public class NpcBlacksmithConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var cfg = (NpcBlacksmithConfig)target;
            if (cfg == null) return;

            serializedObject.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚒ NPC Blacksmith Config (Настройки Кузнеца)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Управляет доступными тирами улучшения (Бронза, Сталь, Композит, Антигравий), ценами работы и требуемыми ресурсами.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("smithyTitle"), new GUIContent("Smithy Title"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowedTiers"), new GUIContent("Allowed Upgrade Tiers"));

            EditorGUILayout.BeginHorizontal();
            DrawTierBadge("Бронза", BlacksmithUpgradeEngine.IsTierAllowed(cfg.allowedTiers, BlacksmithUpgradeTier.Bronze), new Color(0.8f, 0.5f, 0.2f));
            DrawTierBadge("Сталь", BlacksmithUpgradeEngine.IsTierAllowed(cfg.allowedTiers, BlacksmithUpgradeTier.Steel), new Color(0.5f, 0.65f, 0.78f));
            DrawTierBadge("Композит", BlacksmithUpgradeEngine.IsTierAllowed(cfg.allowedTiers, BlacksmithUpgradeTier.Composite), new Color(0.32f, 0.77f, 0.54f));
            DrawTierBadge("Антигравий", BlacksmithUpgradeEngine.IsTierAllowed(cfg.allowedTiers, BlacksmithUpgradeTier.Antigrav), new Color(0.33f, 0.72f, 1.0f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("priceMultiplier"), new GUIContent("Work Price Multiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("creditsOnlyMarkupMultiplier"), new GUIContent("Credits-Only Markup (No Resource)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableReputationDiscount"), new GUIContent("Enable Reputation Discount"));
            if (cfg.enableReputationDiscount)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxReputationDiscountRatio"), new GUIContent("Max Discount Ratio"));
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tierOverrides"), new GUIContent("Per-Tier Custom Overrides"), true);

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed)
                EditorUtility.SetDirty(cfg);
        }

        private static void DrawTierBadge(string label, bool active, Color activeColor)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = active ? activeColor : new Color(0.28f, 0.28f, 0.28f);
            GUILayout.Button(active ? $"✓ {label}" : label, EditorStyles.miniButton);
            GUI.backgroundColor = prev;
        }
    }
}
#endif
