// DialogueActionDrawer — контекстно-зависимый PropertyDrawer для DialogueAction.
// Показывает только поля, релевантные выбранному DialogueActionType.
// Паттерн: как DialogueConditionDrawer.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Dialogue;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(DialogueAction))]
    public class DialogueActionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("type");
            var type = (DialogueActionType)typeProp.enumValueIndex;

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;

            // Always show type
            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), typeProp, new GUIContent("Action"));
            y += lineH + 2;

            // Context-sensitive fields
            switch (type)
            {
                // ── Quest actions ──
                case DialogueActionType.OfferQuest:
                case DialogueActionType.AcceptQuest:
                    DrawStringParam(property, position, ref y, w, lineH, "Quest ID");
                    break;

                case DialogueActionType.CompleteObjective:
                    DrawStringParam(property, position, ref y, w, lineH, "Quest ID");
                    DrawStageIdParam(property, position, ref y, w, lineH);
                    break;

                case DialogueActionType.DiscoverQuest:
                    DrawStringParam(property, position, ref y, w, lineH, "Quest ID");
                    break;

                case DialogueActionType.FailQuest:
                    DrawStringParam(property, position, ref y, w, lineH, "Quest ID");
                    DrawStageIdParam(property, position, ref y, w, lineH);
                    break;

                // ── Inventory actions ──
                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("itemId"), new GUIContent("Item ID"));
                    y += lineH + 2;
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("itemType"), new GUIContent("Item Type"));
                    y += lineH + 2;
                    DrawIntParam(property, position, ref y, w, lineH, "Count");
                    // Fallback string
                    if (property.FindPropertyRelative("itemId").intValue == 0)
                    {
                        DrawStringParam(property, position, ref y, w, lineH, "  └ Item Name (fallback)");
                    }
                    break;

                case DialogueActionType.GiveCargoItem:
                case DialogueActionType.TakeCargoItem:
                    DrawStringParam(property, position, ref y, w, lineH, "Cargo Item ID");
                    DrawIntParam(property, position, ref y, w, lineH, "Count");
                    break;

                // ── Currency / Rep / Attitude ──
                case DialogueActionType.GiveCredits:
                    DrawIntParam(property, position, ref y, w, lineH, "Credits");
                    break;

                case DialogueActionType.AddReputation:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("factionParam"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawIntParam(property, position, ref y, w, lineH, "Delta");
                    break;

                case DialogueActionType.AddNpcAttitude:
                    DrawStringParam(property, position, ref y, w, lineH, "NPC ID");
                    DrawIntParam(property, position, ref y, w, lineH, "Delta");
                    break;

                // ── Market / Service ──
                case DialogueActionType.OpenMarket:
                    DrawStringParam(property, position, ref y, w, lineH, "Zone ID (optional)");
                    break;

                case DialogueActionType.OpenService:
                    // No params needed
                    break;

                // ── World state ──
                case DialogueActionType.SetFlag:
                    DrawStringParam(property, position, ref y, w, lineH, "Flag ID");
                    break;

                case DialogueActionType.EmitEvent:
                    DrawStringParam(property, position, ref y, w, lineH, "Event ID");
                    break;

                case DialogueActionType.SwitchDialogTree:
                    DrawStringParam(property, position, ref y, w, lineH, "Tree ID");
                    break;

                case DialogueActionType.EndConversation:
                    // No params
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            var type = (DialogueActionType)typeProp.enumValueIndex;
            int lines = 1; // type always

            switch (type)
            {
                case DialogueActionType.OfferQuest:
                case DialogueActionType.AcceptQuest:
                case DialogueActionType.DiscoverQuest:
                case DialogueActionType.SetFlag:
                case DialogueActionType.EmitEvent:
                case DialogueActionType.SwitchDialogTree:
                case DialogueActionType.OpenMarket:
                    lines += 1; break;

                case DialogueActionType.CompleteObjective:
                case DialogueActionType.FailQuest:
                case DialogueActionType.GiveCargoItem:
                case DialogueActionType.TakeCargoItem:
                case DialogueActionType.AddReputation:
                case DialogueActionType.AddNpcAttitude:
                    lines += 2; break;

                case DialogueActionType.GiveCredits:
                    lines += 1; break;

                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                    lines += 3; // itemId + itemType + count
                    // +1 more if fallback string shown
                    if (property.FindPropertyRelative("itemId").intValue == 0) lines += 1;
                    break;

                case DialogueActionType.EndConversation:
                case DialogueActionType.OpenService:
                    // No extra lines
                    break;
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
                property.FindPropertyRelative("stageIdParam"), new GUIContent("Objective / Stage ID"));
            y += h + 2;
        }
    }
}
#endif
