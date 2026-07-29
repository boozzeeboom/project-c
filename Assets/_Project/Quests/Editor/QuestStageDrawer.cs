// QuestStageDrawer — PropertyDrawer для QuestStage с карточками objectives/actions.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(QuestStage))]
    public class QuestStageDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;

            var stageIdProp = property.FindPropertyRelative("stageId");
            var descProp = property.FindPropertyRelative("description");
            var nextProp = property.FindPropertyRelative("nextStageId");

            // Stage ID + Next на одной строке
            float halfW = w * 0.5f;
            EditorGUI.PropertyField(new Rect(position.x, y, halfW - 4, lineH), stageIdProp, new GUIContent("Stage ID"));
            EditorGUI.PropertyField(new Rect(position.x + halfW + 4, y, halfW - 4, lineH), nextProp, new GUIContent("→ Next"));
            y += lineH + 2;

            // Description
            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH * 2), descProp, new GUIContent("Desc"));
            y += lineH * 2 + 4;

            // ── Objectives ──
            DrawSubHeader(ref y, w, lineH, "🎯 Objectives");
            y += DrawSectionArray(property, "objectives", position, y, w, lineH, "Objective");
            y += 4;

            // ── onEnterActions ──
            DrawSubHeader(ref y, w, lineH, "▶ onEnter Actions");
            y += DrawSectionArray(property, "onEnterActions", position, y, w, lineH, "Action");
            y += 4;

            // ── onCompleteActions ──
            DrawSubHeader(ref y, w, lineH, "✓ onComplete Actions");
            y += DrawSectionArray(property, "onCompleteActions", position, y, w, lineH, "Action");

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineH = EditorGUIUtility.singleLineHeight;
            float h = 0;

            // Header: ID + Next
            h += lineH + 2;
            // Description: 2 lines
            h += lineH * 2 + 4;

            // Objectives section
            h += lineH + 2; // sub-header
            h += SectionArrayHeight(property, "objectives", lineH);
            h += 4;

            // onEnter section
            h += lineH + 2;
            h += SectionArrayHeight(property, "onEnterActions", lineH);
            h += 4;

            // onComplete section
            h += lineH + 2;
            h += SectionArrayHeight(property, "onCompleteActions", lineH);

            return h + 6;
        }

        private static void DrawSubHeader(ref float y, float w, float h, string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = new Color(0.65f, 0.75f, 0.95f);
            EditorGUI.LabelField(new Rect(8, y, w - 8, h), title, style);
            y += h + 2;
        }

        private static float DrawSectionArray(SerializedProperty parent, string arrayName,
            Rect position, float y, float w, float lineH, string elementLabel)
        {
            var arr = parent.FindPropertyRelative(arrayName);
            if (arr == null || !arr.isArray) return 0;

            float consumed = 0;
            float indent = 16;

            for (int i = 0; i < arr.arraySize; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                float elH = EditorGUI.GetPropertyHeight(el, new GUIContent($"{elementLabel} {i}"));
                EditorGUI.PropertyField(new Rect(position.x + indent, y + consumed, w - indent, elH),
                    el, new GUIContent($"{elementLabel} {i}"), true);
                consumed += elH + 2;
            }

            // Add button
            var btnRect = new Rect(position.x + indent, y + consumed, 24, lineH);
            if (GUI.Button(btnRect, "+"))
            {
                arr.arraySize++;
            }
            consumed += lineH + 2;

            // Remove last button
            if (arr.arraySize > 0)
            {
                var delRect = new Rect(position.x + indent + 28, y + consumed - lineH - 2, 50, lineH);
                if (GUI.Button(delRect, "× Last"))
                {
                    arr.arraySize--;
                }
            }

            return consumed;
        }

        private static float SectionArrayHeight(SerializedProperty parent, string arrayName, float lineH)
        {
            var arr = parent.FindPropertyRelative(arrayName);
            if (arr == null || !arr.isArray) return 0;

            float h = 0;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                h += EditorGUI.GetPropertyHeight(el, new GUIContent($"{i}"), true) + 2;
            }
            h += lineH + 2; // [+] button row
            return h;
        }
    }
}
#endif
