// QuestPrerequisiteDrawer — контекстно-зависимый PropertyDrawer для QuestPrerequisite.
// T-QUEDIT v2: requiredNpc для NpcAttitudeAtLeast.

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

            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), typeProp, new GUIContent("Condition"));
            y += lineH + 2;

            switch (type)
            {
                case QuestPrerequisiteType.QuestCompleted:
                case QuestPrerequisiteType.QuestActive:
                    DrawQuestRefField(property, position, ref y, w, lineH);
                    break;

                case QuestPrerequisiteType.ReputationAtLeast:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("factionParam"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawIntParam(property, position, ref y, w, lineH, "Min Value");
                    break;

                case QuestPrerequisiteType.NpcAttitudeAtLeast:
                    DrawNpcRefField(property, position, ref y, w, lineH);
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
            int lines = 1;

            switch (type)
            {
                case QuestPrerequisiteType.QuestCompleted:
                case QuestPrerequisiteType.QuestActive:
                    lines += 1;
                    if (property.FindPropertyRelative("requiredQuest").objectReferenceValue == null) lines += 1;
                    break;
                case QuestPrerequisiteType.ReputationAtLeast:
                    lines += 2; break;
                case QuestPrerequisiteType.NpcAttitudeAtLeast:
                    lines += 2;
                    if (property.FindPropertyRelative("requiredNpc").objectReferenceValue == null) lines += 1;
                    break;
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

        private static void DrawQuestRefField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var questProp = property.FindPropertyRelative("requiredQuest");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), questProp, new GUIContent("Quest (drag .asset)"));
            y += h + 2;

            if (questProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                    property.FindPropertyRelative("stringParam"), new GUIContent("  └ Quest ID (string)"));
                y += h + 2;
            }
        }

        private static void DrawNpcRefField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var npcProp = property.FindPropertyRelative("requiredNpc");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), npcProp, new GUIContent("NPC (drag .asset)"));
            y += h + 2;

            if (npcProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                    property.FindPropertyRelative("stringParam"), new GUIContent("  └ NPC ID (string)"));
                y += h + 2;
            }
        }
    }
}
#endif
