// QuestObjectiveDrawer — контекстно-зависимый PropertyDrawer для QuestObjective.
// Показывает только поля, релевантные выбранному objectiveType.
// Паттерн: как DialogueConditionDrawer.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;
using ProjectC.Items;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(QuestObjective))]
    public class QuestObjectiveDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("objectiveType");
            var type = (QuestObjectiveType)typeProp.enumValueIndex;

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;
            float halfW = w * 0.5f;

            // Always: objectiveId + objectiveType на одной строке
            var idProp = property.FindPropertyRelative("objectiveId");
            var idRect = new Rect(position.x, y, halfW - 4, lineH);
            var typeRect = new Rect(position.x + halfW + 4, y, halfW - 4, lineH);
            EditorGUI.PropertyField(idRect, idProp, new GUIContent("ID"));
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
            y += lineH + 2;

            // Description
            var descProp = property.FindPropertyRelative("description");
            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), descProp, new GUIContent("Desc"));
            y += lineH + 2;

            // Context-sensitive fields
            switch (type)
            {
                case QuestObjectiveType.TalkToNpc:
                    DrawNpcField(property, position, ref y, w, lineH);
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.HaveItem:
                    DrawPickupItemField(property, position, ref y, w, lineH);
                    DrawIntField(property, "requiredQuantity", position, ref y, w, lineH, "Qty");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.DeliverItem:
                    DrawPickupItemField(property, position, ref y, w, lineH);
                    DrawNpcField(property, position, ref y, w, lineH);
                    DrawIntField(property, "requiredQuantity", position, ref y, w, lineH, "Qty");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.ReachLocation:
                    DrawStringField(property, "targetSceneId", position, ref y, w, lineH, "Scene ID");
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("targetPosition"), new GUIContent("Position"));
                    y += lineH + 2;
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("targetRadius"), new GUIContent("Radius (m)"));
                    y += lineH + 2;
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.ReputationAtLeast:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("targetFaction"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawIntField(property, "reputationValue", position, ref y, w, lineH, "Min Value");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.EventDriven:
                case QuestObjectiveType.WaitForEvent:
                    DrawStringField(property, "eventId", position, ref y, w, lineH, "Event ID");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.KillEntity:
                    DrawStringField(property, "targetEntityType", position, ref y, w, lineH, "Entity Type");
                    DrawIntField(property, "requiredQuantity", position, ref y, w, lineH, "Qty");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("objectiveType");
            var type = (QuestObjectiveType)typeProp.enumValueIndex;

            // Base: ID row + description = 2 lines
            int lines = 2;

            switch (type)
            {
                case QuestObjectiveType.TalkToNpc:
                    lines += 2; // Npc field + optional/required
                    break;
                case QuestObjectiveType.HaveItem:
                    lines += 3; // pickupItem + qty + optional/required
                    break;
                case QuestObjectiveType.DeliverItem:
                    lines += 4; // pickupItem + Npc + qty + optional/required
                    break;
                case QuestObjectiveType.ReachLocation:
                    lines += 4; // sceneId + position + radius + optional/required
                    break;
                case QuestObjectiveType.ReputationAtLeast:
                    lines += 3; // faction + value + optional/required
                    break;
                case QuestObjectiveType.EventDriven:
                case QuestObjectiveType.WaitForEvent:
                    lines += 2; // eventId + optional/required
                    break;
                case QuestObjectiveType.KillEntity:
                    lines += 3; // entityType + qty + optional/required
                    break;
                default:
                    lines += 1; // optional/required only
                    break;
            }

            return (EditorGUIUtility.singleLineHeight + 2) * lines;
        }

        // ── helpers ──

        private static void DrawStringField(SerializedProperty property, string propName, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative(propName), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawIntField(SerializedProperty property, string propName, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative(propName), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawPickupItemField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var pickupProp = property.FindPropertyRelative("pickupItem");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), pickupProp, new GUIContent("Item (drag .asset)"));
            y += h + 2;

            // Show fallback string only if pickupItem is null
            if (pickupProp.objectReferenceValue == null)
            {
                var fallbackProp = property.FindPropertyRelative("itemTradeItemId");
                EditorGUI.PropertyField(new Rect(position.x, y, w, h), fallbackProp, new GUIContent("  └ Item ID (string)"));
                y += h + 2;
            }
        }

        private static void DrawNpcField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var npcProp = property.FindPropertyRelative("targetNpc");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), npcProp, new GUIContent("NPC (drag .asset)"));
            y += h + 2;

            // Show fallback string only if targetNpc is null
            if (npcProp.objectReferenceValue == null)
            {
                var fallbackProp = property.FindPropertyRelative("targetNpcId");
                EditorGUI.PropertyField(new Rect(position.x, y, w, h), fallbackProp, new GUIContent("  └ NPC ID (string)"));
                y += h + 2;
            }
        }

        private static void DrawOptionalRequired(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            float halfW = w * 0.5f;
            EditorGUI.PropertyField(new Rect(position.x, y, halfW - 4, h),
                property.FindPropertyRelative("required"), new GUIContent("Required"));
            EditorGUI.PropertyField(new Rect(position.x + halfW + 4, y, halfW - 4, h),
                property.FindPropertyRelative("optional"), new GUIContent("Optional"));
            y += h + 2;
        }
    }
}
#endif
