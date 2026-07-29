// QuestRewardDrawer — плоская форма для QuestReward.
// Все секции видны сразу, без свёрнутых вложенных массивов.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(QuestReward))]
    public class QuestRewardDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;

            // ── Section: Credits ──
            DrawSectionHeader(ref y, w, lineH, "💰 Credits");
            var creditsProp = property.FindPropertyRelative("credits");
            EditorGUI.PropertyField(new Rect(position.x + 16, y, w - 16, lineH), creditsProp, new GUIContent("Amount"));
            y += lineH + 4;

            // ── Section: Inventory Items ──
            DrawSectionHeader(ref y, w, lineH, "📦 Inventory Items");
            y += DrawArrayInline(property, "items", position, y, w, lineH,
                (prop, rect, ry, rw, rh) =>
                {
                    float rx = rect.x + 16;
                    EditorGUI.PropertyField(new Rect(rx, ry, rw * 0.45f, rh),
                        prop.FindPropertyRelative("pickupItem"), GUIContent.none);
                    EditorGUI.PropertyField(new Rect(rx + rw * 0.45f + 4, ry, rw * 0.25f, rh),
                        prop.FindPropertyRelative("count"), GUIContent.none);
                    // Fallback string
                    if (prop.FindPropertyRelative("pickupItem").objectReferenceValue == null)
                    {
                        ry += rh + 2;
                        EditorGUI.PropertyField(new Rect(rx, ry, rw * 0.45f, rh),
                            prop.FindPropertyRelative("tradeItemId"), new GUIContent("ID"));
                        return ry + rh + 2;
                    }
                    return ry + rh + 2;
                });
            y += 4;

            // ── Section: Cargo Items ──
            DrawSectionHeader(ref y, w, lineH, "🚢 Cargo Items");
            y += DrawArrayInline(property, "cargoItems", position, y, w, lineH,
                (prop, rect, ry, rw, rh) =>
                {
                    float rx = rect.x + 16;
                    EditorGUI.PropertyField(new Rect(rx, ry, rw * 0.45f, rh),
                        prop.FindPropertyRelative("cargoItem"), GUIContent.none);
                    EditorGUI.PropertyField(new Rect(rx + rw * 0.45f + 4, ry, rw * 0.25f, rh),
                        prop.FindPropertyRelative("count"), GUIContent.none);
                    if (prop.FindPropertyRelative("cargoItem").objectReferenceValue == null)
                    {
                        ry += rh + 2;
                        EditorGUI.PropertyField(new Rect(rx, ry, rw * 0.45f, rh),
                            prop.FindPropertyRelative("tradeItemId"), new GUIContent("ID"));
                        return ry + rh + 2;
                    }
                    return ry + rh + 2;
                });
            y += 4;

            // ── Section: Reputation ──
            DrawSectionHeader(ref y, w, lineH, "📈 Reputation");
            y += DrawArrayInline(property, "reputation", position, y, w, lineH,
                (prop, rect, ry, rw, rh) =>
                {
                    float rx = rect.x + 16;
                    EditorGUI.PropertyField(new Rect(rx, ry, rw * 0.40f, rh),
                        prop.FindPropertyRelative("faction"), GUIContent.none);
                    EditorGUI.PropertyField(new Rect(rx + rw * 0.40f + 4, ry, rw * 0.30f, rh),
                        prop.FindPropertyRelative("value"), GUIContent.none);
                    return ry + rh + 2;
                });
            y += 4;

            // ── Section: Unlocks ──
            DrawSectionHeader(ref y, w, lineH, "🔓 Unlocks");
            y += DrawArrayInline(property, "unlocks", position, y, w, lineH,
                (prop, rect, ry, rw, rh) =>
                {
                    float rx = rect.x + 16;
                    var utProp = prop.FindPropertyRelative("unlockType");
                    EditorGUI.PropertyField(new Rect(rx, ry, rw * 0.30f, rh), utProp, GUIContent.none);
                    var ut = (QuestUnlockType)utProp.enumValueIndex;

                    if (ut == QuestUnlockType.DialogTree)
                    {
                        EditorGUI.PropertyField(new Rect(rx + rw * 0.30f + 4, ry, rw * 0.55f, rh),
                            prop.FindPropertyRelative("unlockDialog"), GUIContent.none);
                    }
                    else
                    {
                        EditorGUI.PropertyField(new Rect(rx + rw * 0.30f + 4, ry, rw * 0.55f, rh),
                            prop.FindPropertyRelative("unlockId"), GUIContent.none);
                    }
                    return ry + rh + 2;
                });

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineH = EditorGUIUtility.singleLineHeight;
            float h = 0;

            // Credits: header + 1 field
            h += (lineH + 2) * 2 + 4;

            // Items
            h += (lineH + 2); // header
            h += ArrayHeight(property, "items", lineH);
            h += 4;

            // Cargo
            h += (lineH + 2);
            h += ArrayHeight(property, "cargoItems", lineH);
            h += 4;

            // Reputation
            h += (lineH + 2);
            h += ArrayHeight(property, "reputation", lineH);
            h += 4;

            // Unlocks
            h += (lineH + 2);
            h += ArrayHeight(property, "unlocks", lineH);

            return h + 6;
        }

        // ── helpers ──

        private static void DrawSectionHeader(ref float y, float w, float h, string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = new Color(0.85f, 0.70f, 0.30f);
            EditorGUI.LabelField(new Rect(0, y, w, h), title, style);
            y += h + 2;
        }

        /// <summary>Returns extra height consumed.</summary>
        private static float DrawArrayInline(SerializedProperty parent, string arrayName,
            Rect position, float y, float w, float lineH,
            System.Func<SerializedProperty, Rect, float, float, float, float> drawElement)
        {
            var arr = parent.FindPropertyRelative(arrayName);
            if (arr == null || !arr.isArray) return 0;

            float consumed = 0;
            float h = lineH;

            for (int i = 0; i < arr.arraySize; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                float endY = drawElement(el, position, y + consumed, w, h);
                consumed = endY - y;
            }

            // Add button
            var btnRect = new Rect(position.x + 16, y + consumed, 24, lineH);
            if (GUI.Button(btnRect, "+"))
            {
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).isExpanded = true;
            }
            consumed += lineH + 2;

            // Remove button (on last element)
            if (arr.arraySize > 0)
            {
                var delRect = new Rect(position.x + 44, y + consumed - lineH - 2, 50, lineH);
                if (GUI.Button(delRect, "× Last"))
                {
                    arr.arraySize--;
                }
            }

            return consumed;
        }

        private static float ArrayHeight(SerializedProperty parent, string arrayName, float lineH)
        {
            var arr = parent.FindPropertyRelative(arrayName);
            if (arr == null || !arr.isArray) return 0;
            // 1 line per element + 1 for the [+] button
            return (lineH + 2) * (arr.arraySize + 1);
        }
    }
}
#endif
