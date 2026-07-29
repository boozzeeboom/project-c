// DialogueActionDrawer — контекстно-зависимый PropertyDrawer для DialogueAction.
// T-QUEDIT v2: questRef, npcRef, dialogTreeRef — drag-and-drop поля.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Dialogue;
using ProjectC.Factions;
using ProjectC.Quests;

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

            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), typeProp, new GUIContent("Action"));
            y += lineH + 2;

            switch (type)
            {
                // ── Quest actions (drag QuestDefinition) ──
                case DialogueActionType.OfferQuest:
                case DialogueActionType.AcceptQuest:
                case DialogueActionType.DiscoverQuest:
                    DrawQuestRefField(property, position, ref y, w, lineH);
                    break;

                case DialogueActionType.CompleteObjective:
                case DialogueActionType.FailQuest:
                    DrawQuestRefField(property, position, ref y, w, lineH);
                    DrawStageIdParam(property, position, ref y, w, lineH);
                    break;

                // ── Inventory ──
                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("itemId"), new GUIContent("Item ID"));
                    y += lineH + 2;
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("itemType"), new GUIContent("Item Type"));
                    y += lineH + 2;
                    DrawIntParam(property, position, ref y, w, lineH, "Count");
                    if (property.FindPropertyRelative("itemId").intValue == 0)
                        DrawStringParam(property, position, ref y, w, lineH, "  └ Item Name (fallback)");
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
                    DrawNpcRefField(property, position, ref y, w, lineH);
                    DrawIntParam(property, position, ref y, w, lineH, "Delta");
                    break;

                // ── Market / Service ──
                case DialogueActionType.OpenMarket:
                    DrawStringParam(property, position, ref y, w, lineH, "Zone ID (optional)");
                    break;

                case DialogueActionType.OpenService:
                    break;

                // ── World state ──
                case DialogueActionType.SetFlag:
                    DrawStringParam(property, position, ref y, w, lineH, "Flag ID");
                    break;

                case DialogueActionType.EmitEvent:
                    DrawStringParam(property, position, ref y, w, lineH, "Event ID");
                    break;

                case DialogueActionType.SwitchDialogTree:
                    DrawDialogTreeRefField(property, position, ref y, w, lineH);
                    break;

                case DialogueActionType.EndConversation:
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            var type = (DialogueActionType)typeProp.enumValueIndex;
            int lines = 1;

            switch (type)
            {
                case DialogueActionType.OfferQuest:
                case DialogueActionType.AcceptQuest:
                case DialogueActionType.DiscoverQuest:
                    lines += 1;
                    if (property.FindPropertyRelative("questRef").objectReferenceValue == null) lines += 1;
                    break;
                case DialogueActionType.CompleteObjective:
                case DialogueActionType.FailQuest:
                    lines += 2;
                    if (property.FindPropertyRelative("questRef").objectReferenceValue == null) lines += 1;
                    break;
                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                    lines += 3;
                    if (property.FindPropertyRelative("itemId").intValue == 0) lines += 1;
                    break;
                case DialogueActionType.GiveCargoItem:
                case DialogueActionType.TakeCargoItem:
                case DialogueActionType.AddReputation:
                case DialogueActionType.AddNpcAttitude:
                    lines += 2;
                    if (type == DialogueActionType.AddNpcAttitude && property.FindPropertyRelative("npcRef").objectReferenceValue == null) lines += 1;
                    break;
                case DialogueActionType.SwitchDialogTree:
                    lines += 1;
                    if (property.FindPropertyRelative("dialogTreeRef").objectReferenceValue == null) lines += 1;
                    break;
                case DialogueActionType.GiveCredits:
                case DialogueActionType.SetFlag:
                case DialogueActionType.EmitEvent:
                case DialogueActionType.OpenMarket:
                    lines += 1;
                    break;
            }

            return (EditorGUIUtility.singleLineHeight + 2) * lines;
        }

        // ── helpers ──

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

        private static void DrawQuestRefField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("questRef");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("Quest (drag .asset)"));
            y += h + 2;

            if (refProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                    property.FindPropertyRelative("stringParam"), new GUIContent("  └ Quest ID (string)"));
                y += h + 2;
            }
        }

        private static void DrawNpcRefField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("npcRef");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("NPC (drag .asset)"));
            y += h + 2;

            if (refProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                    property.FindPropertyRelative("stringParam"), new GUIContent("  └ NPC ID (string)"));
                y += h + 2;
            }
        }

        private static void DrawDialogTreeRefField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("dialogTreeRef");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("Dialog (drag .asset)"));
            y += h + 2;

            if (refProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                    property.FindPropertyRelative("stringParam"), new GUIContent("  └ Tree ID (string)"));
                y += h + 2;
            }
        }
    }
}
#endif
