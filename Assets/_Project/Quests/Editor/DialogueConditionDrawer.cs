// T-Q09: PropertyDrawer для DialogueCondition + DialogueAction — context-sensitive fields.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.10-2.3.11 (single-class tag-union).
//
// Рисует только relevant поля в зависимости от type. Намного чище чем "all-purpose" fields.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Dialogue;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(DialogueCondition))]
    public class DialogueConditionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var typeProp = property.FindPropertyRelative("type");
            var type = (DialogueConditionType)typeProp.enumValueIndex;

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;

            // Always show type
            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), typeProp);
            y += lineH + 2;

            // Context-sensitive fields
            switch (type)
            {
                case DialogueConditionType.HasItem:
                case DialogueConditionType.CargoHasItem:
                    DrawStringParam(property, position, ref y, w, lineH, "ItemId (string)");
                    DrawIntParam(property, position, ref y, w, lineH, "Quantity");
                    break;
                case DialogueConditionType.QuestStateEquals:
                    DrawStringParam(property, position, ref y, w, lineH, "QuestId");
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("questStateParam"), new GUIContent("Quest State"));
                    y += lineH + 2;
                    break;
                case DialogueConditionType.QuestStageEquals:
                    DrawStringParam(property, position, ref y, w, lineH, "QuestId");
                    DrawStageIdParam(property, position, ref y, w, lineH);
                    break;
                case DialogueConditionType.QuestCompleted:
                case DialogueConditionType.QuestDiscovered:
                    DrawStringParam(property, position, ref y, w, lineH, "QuestId");
                    break;
                case DialogueConditionType.ReputationAtLeast:
                case DialogueConditionType.ReputationAtMost:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("factionParam"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawIntParam(property, position, ref y, w, lineH, "Value");
                    break;
                case DialogueConditionType.NpcAttitudeAtLeast:
                    DrawStringParam(property, position, ref y, w, lineH, "NpcId");
                    DrawIntParam(property, position, ref y, w, lineH, "Value");
                    break;
                case DialogueConditionType.TimeOfDayIn:
                case DialogueConditionType.PlayerInZone:
                case DialogueConditionType.FlagIsSet:
                    DrawStringParam(property, position, ref y, w, lineH, "Id/Name");
                    break;
                case DialogueConditionType.WasNodeVisited:
                    DrawStringParam(property, position, ref y, w, lineH, "TreeId");
                    DrawStageIdParam(property, position, ref y, w, lineH);
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            var type = (DialogueConditionType)typeProp.enumValueIndex;
            int lines = 1; // type
            switch (type)
            {
                case DialogueConditionType.HasItem:
                case DialogueConditionType.CargoHasItem:
                case DialogueConditionType.QuestCompleted:
                case DialogueConditionType.QuestDiscovered:
                case DialogueConditionType.NpcAttitudeAtLeast:
                case DialogueConditionType.TimeOfDayIn:
                case DialogueConditionType.PlayerInZone:
                case DialogueConditionType.FlagIsSet:
                    lines += 2; break;
                case DialogueConditionType.QuestStateEquals:
                case DialogueConditionType.QuestStageEquals:
                case DialogueConditionType.ReputationAtLeast:
                case DialogueConditionType.ReputationAtMost:
                case DialogueConditionType.WasNodeVisited:
                    lines += 2; break;
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
        private static void DrawStageIdParam(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stageIdParam"), new GUIContent("Stage/Node Id"));
            y += h + 2;
        }
    }
}
#endif
