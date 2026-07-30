// DialogueActionDrawer — контекстно-зависимый PropertyDrawer для DialogueAction.
// T-QUEDIT v2: questRef, npcRef, dialogTreeRef — drag-and-drop поля.
// T-QUEDIT v3: фикс высоты — всегда max, фолбэк всегда видим (dimmed).

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
            var type = (DialogueActionType)typeProp.intValue;

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;

            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), typeProp, new GUIContent("Action"));
            y += lineH + 2;

            switch (type)
            {
                // ── Quest actions ──
                case DialogueActionType.OfferQuest:
                case DialogueActionType.AcceptQuest:
                case DialogueActionType.DiscoverQuest:
                    DrawQuestRef(property, position, ref y, w, lineH);
                    break;

                case DialogueActionType.CompleteObjective:
                case DialogueActionType.FailQuest:
                    DrawQuestRef(property, position, ref y, w, lineH);
                    DrawStageId(property, position, ref y, w, lineH);
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
                    DrawInt(property, position, ref y, w, lineH, "Count");
                    // Always show fallback — dimmed if itemId is set
                    {
                        bool hasId = property.FindPropertyRelative("itemId").intValue != 0;
                        var oldE = GUI.enabled;
                        GUI.enabled = !hasId;
                        DrawString(property, position, ref y, w, lineH,
                            hasId ? "(not used — Item ID set)" : "Item Name (fallback)");
                        GUI.enabled = oldE;
                    }
                    break;

                case DialogueActionType.GiveCargoItem:
                case DialogueActionType.TakeCargoItem:
                    DrawString(property, position, ref y, w, lineH, "Cargo Item ID");
                    DrawInt(property, position, ref y, w, lineH, "Count");
                    break;

                // ── Currency / Rep / Attitude ──
                case DialogueActionType.GiveCredits:
                    DrawInt(property, position, ref y, w, lineH, "Credits");
                    break;

                case DialogueActionType.AddReputation:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("factionParam"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawInt(property, position, ref y, w, lineH, "Delta");
                    break;

                case DialogueActionType.AddNpcAttitude:
                    DrawNpcRef(property, position, ref y, w, lineH);
                    DrawInt(property, position, ref y, w, lineH, "Delta");
                    break;

                // ── Market / Service ──
                case DialogueActionType.OpenMarket:
                    DrawString(property, position, ref y, w, lineH, "Zone ID (optional)");
                    break;

                case DialogueActionType.OpenService:
                    break;

                // ── World state ──
                case DialogueActionType.SetFlag:
                    DrawString(property, position, ref y, w, lineH, "Flag ID");
                    break;

                case DialogueActionType.EmitEvent:
                    DrawString(property, position, ref y, w, lineH, "Event ID");
                    break;

                case DialogueActionType.SwitchDialogTree:
                    DrawDialogTreeRef(property, position, ref y, w, lineH);
                    break;

                case DialogueActionType.EndConversation:
                    // nothing extra
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            var type = (DialogueActionType)typeProp.intValue;
            int lines = 1; // type dropdown

            switch (type)
            {
                case DialogueActionType.OfferQuest:
                case DialogueActionType.AcceptQuest:
                case DialogueActionType.DiscoverQuest:
                    lines += 2; break; // questRef ObjectField + fallback

                case DialogueActionType.CompleteObjective:
                case DialogueActionType.FailQuest:
                    lines += 3; break; // questRef + fallback + stageId

                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                    lines += 4; break; // itemId + itemType + count + fallback

                case DialogueActionType.GiveCargoItem:
                case DialogueActionType.TakeCargoItem:
                case DialogueActionType.AddReputation:
                    lines += 2; break;

                case DialogueActionType.AddNpcAttitude:
                    lines += 3; break; // npcRef + fallback + delta

                case DialogueActionType.SwitchDialogTree:
                    lines += 2; break; // dialogTreeRef + fallback

                case DialogueActionType.GiveCredits:
                case DialogueActionType.SetFlag:
                case DialogueActionType.EmitEvent:
                case DialogueActionType.OpenMarket:
                    lines += 1; break;

                case DialogueActionType.OpenService:
                case DialogueActionType.EndConversation:
                    break;
            }

            return (EditorGUIUtility.singleLineHeight + 2) * lines;
        }

        // ── Ref fields (always show fallback, dimmed when ref set) ──

        private static void DrawQuestRef(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("questRef");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("Quest (drag .asset)"));
            y += h + 2;

            bool hasRef = refProp.objectReferenceValue != null;
            var oldE = GUI.enabled;
            GUI.enabled = !hasRef;
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"),
                new GUIContent(hasRef ? "  └ (not used — object ref active)" : "  └ Quest ID (string)"));
            GUI.enabled = oldE;
            y += h + 2;
        }

        private static void DrawNpcRef(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("npcRef");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("NPC (drag .asset)"));
            y += h + 2;

            bool hasRef = refProp.objectReferenceValue != null;
            var oldE = GUI.enabled;
            GUI.enabled = !hasRef;
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"),
                new GUIContent(hasRef ? "  └ (not used — object ref active)" : "  └ NPC ID (string)"));
            GUI.enabled = oldE;
            y += h + 2;
        }

        private static void DrawDialogTreeRef(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("dialogTreeRef");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("Dialog (drag .asset)"));
            y += h + 2;

            bool hasRef = refProp.objectReferenceValue != null;
            var oldE = GUI.enabled;
            GUI.enabled = !hasRef;
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"),
                new GUIContent(hasRef ? "  └ (not used — object ref active)" : "  └ Tree ID (string)"));
            GUI.enabled = oldE;
            y += h + 2;
        }

        // ── Simple helpers ──

        private static void DrawString(SerializedProperty property, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stringParam"), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawInt(SerializedProperty property, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("intParam"), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawStageId(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative("stageIdParam"), new GUIContent("Objective / Stage ID"));
            y += h + 2;
        }
    }
}
#endif
