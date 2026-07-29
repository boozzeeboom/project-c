// QuestPrerequisiteDrawer — контекстно-зависимый PropertyDrawer для QuestPrerequisite.
// Показывает только поля, релевантные выбранному QuestPrerequisiteType.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(QuestPrerequisite))]
    public class QuestPrerequisiteDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("type");
            var type = (QuestPrerequisiteType)typeProp.enumValueIndex;

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;

            // Always show type
            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), typeProp, new GUIContent("Condition"));
            y += lineH + 2;

            // Context-sensitive fields
            switch (type)
            {
                case QuestPrerequisiteType.QuestCompleted:
                case QuestPrerequisiteType.QuestActive:
                    DrawQuestField(property, position, ref y, w, lineH);
                    break;

                case QuestPrerequisiteType.ReputationAtLeast:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("factionParam"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawIntParam(property, position, ref y, w, lineH, "Min Value");
                    break;

                case QuestPrerequisiteType.NpcAttitudeAtLeast:
                    DrawStringParam(property, position, ref y, w, lineH, "NPC ID");
                    DrawIntParam(property, position, ref y, w, lineH, "Min Value");
                    break;

                case QuestPrerequisiteType.HaveItem:
                    DrawStringParam(property, position, ref y, w, lineH, "Item ID / Name");
                    DrawIntParam(property, position, ref y, w, lineH, "Qty");
                    break;

                case QuestPrerequisiteType.FlagIsSet:
                    DrawStringParam(property, position, ref y, w, lineH, "Flag ID");
                    break;

                case QuestPrerequisiteType.PlayerFaction:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("factionParam"), new GUIContent("Faction"));
                    y += lineH + 2;
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            var type = (QuestPrerequisiteType)typeProp.enumValueIndex;
            int lines = 1; // type always

            switch (type)
            {
                case QuestPrerequisiteType.QuestCompleted:
                case QuestPrerequisiteType.QuestActive:
                    lines += 1; // requiredQuest or stringParam
                    break;
                case QuestPrerequisiteType.ReputationAtLeast:
                case QuestPrerequisiteType.NpcAttitudeAtLeast:
                case QuestPrerequisiteType.HaveItem:
                case QuestPrerequisiteType.PlayerFaction:
                    lines += 2; break;
                case QuestPrerequisiteType.FlagIsSet:
                    lines += 1; break;
            }

            return (EditorGUIUtility.singleLineHeight + 2) * lines;
        }

        private static void DrawStringParam(SerializedProperty property, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawIntParam(SerializedProperty property, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("intParam"), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawQuestField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var questProp = property.FindPropertyRelative("requiredQuest");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), questProp, new GUIContent("Quest (drag .asset)"));
            y += h + 2;

            // Show fallback string only if requiredQuest is null
            if (questProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                    property.FindPropertyRelative("stringParam"), new GUIContent("  └ Quest ID (string)"));
                y += h + 2;
            }
        }
    }
}
#endif
