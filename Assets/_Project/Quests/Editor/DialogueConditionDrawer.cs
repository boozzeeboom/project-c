// T-Q09: PropertyDrawer для DialogueCondition — context-sensitive drag-and-drop поля.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.10 (single-class tag-union).
//
// T-QUEDIT v2: requiredQuest, requiredNpc, requiredItem — ObjectField поверх stringParam.
// Рисует только relevant поля в зависимости от type.

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
            var type = (DialogueConditionType)typeProp.intValue;

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
                    DrawItemField(property, position, ref y, w, lineH);
                    DrawIntParam(property, position, ref y, w, lineH, "Quantity");
                    break;

                case DialogueConditionType.QuestStateEquals:
                    DrawQuestField(property, position, ref y, w, lineH);
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("questStateParam"), new GUIContent("Quest State"));
                    y += lineH + 2;
                    break;

                case DialogueConditionType.QuestStageEquals:
                    DrawQuestField(property, position, ref y, w, lineH);
                    DrawStageIdParam(property, position, ref y, w, lineH);
                    break;

                case DialogueConditionType.QuestCompleted:
                case DialogueConditionType.QuestDiscovered:
                    DrawQuestField(property, position, ref y, w, lineH);
                    break;

                case DialogueConditionType.ReputationAtLeast:
                case DialogueConditionType.ReputationAtMost:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("factionParam"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawIntParam(property, position, ref y, w, lineH, "Value");
                    break;

                case DialogueConditionType.NpcAttitudeAtLeast:
                    DrawNpcField(property, position, ref y, w, lineH);
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
            var type = (DialogueConditionType)typeProp.intValue;
            int lines = 1; // type

            switch (type)
            {
                case DialogueConditionType.HasItem:
                case DialogueConditionType.CargoHasItem:
                    lines += 3; break; // item ObjectField + fallback string + quantity

                case DialogueConditionType.QuestStateEquals:
                    lines += 3; break; // quest ObjectField + fallback string + state

                case DialogueConditionType.QuestStageEquals:
                    lines += 3; break; // quest ObjectField + fallback string + stage

                case DialogueConditionType.QuestCompleted:
                case DialogueConditionType.QuestDiscovered:
                    lines += 2; break; // quest ObjectField + fallback string

                case DialogueConditionType.ReputationAtLeast:
                case DialogueConditionType.ReputationAtMost:
                    lines += 2; break; // faction + value

                case DialogueConditionType.NpcAttitudeAtLeast:
                    lines += 3; break; // npc ObjectField + fallback string + value

                case DialogueConditionType.TimeOfDayIn:
                case DialogueConditionType.PlayerInZone:
                case DialogueConditionType.FlagIsSet:
                    lines += 1; break; // string field

                case DialogueConditionType.WasNodeVisited:
                    lines += 2; break; // treeId string + stageId string

                default:
                    lines += 1; break;
            }
            return (EditorGUIUtility.singleLineHeight + 2) * lines;
        }

        // ── Drag-and-drop helpers ──

        private static void DrawQuestField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("requiredQuest");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("Quest (drag .asset)"));
            y += h + 2;

            // Always show fallback string field (dimmed if object ref is set)
            bool hasRef = refProp.objectReferenceValue != null;
            var oldEnabled = GUI.enabled;
            GUI.enabled = !hasRef;
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"),
                new GUIContent(hasRef ? "  └ (not used — object ref active)" : "  └ Quest ID (string)"));
            GUI.enabled = oldEnabled;
            y += h + 2;
        }

        private static void DrawNpcField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("requiredNpc");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("NPC (drag .asset)"));
            y += h + 2;

            bool hasRef = refProp.objectReferenceValue != null;
            var oldEnabled = GUI.enabled;
            GUI.enabled = !hasRef;
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"),
                new GUIContent(hasRef ? "  └ (not used — object ref active)" : "  └ NPC ID (string)"));
            GUI.enabled = oldEnabled;
            y += h + 2;
        }

        private static void DrawItemField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("requiredItem");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("Item (drag .asset)"));
            y += h + 2;

            bool hasRef = refProp.objectReferenceValue != null;
            var oldEnabled = GUI.enabled;
            GUI.enabled = !hasRef;
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"),
                new GUIContent(hasRef ? "  └ (not used — object ref active)" : "  └ Item ID/Name (string)"));
            GUI.enabled = oldEnabled;
            y += h + 2;
        }

        // ── Basic helpers ──

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
