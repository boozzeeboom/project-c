// SpeakerRefDrawer — PropertyDrawer для SpeakerRef с drag-and-drop NpcDefinition.
// T-QUEDIT v2: при speakerKind=Npc показывает ObjectField вместо string refId.
// При Player/Narrator — только лейбл (без поля).

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(SpeakerRef))]
    public class SpeakerRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var kindProp = property.FindPropertyRelative("speakerKind");
            var kind = (SpeakerRef.Kind)kindProp.enumValueIndex;

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;

            // ── Kind dropdown ──
            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), kindProp, new GUIContent("Speaker"));
            y += lineH + 2;

            // ── Context-sensitive: Npc → ObjectField, Player/Narrator → label ──
            switch (kind)
            {
                case SpeakerRef.Kind.Npc:
                    DrawNpcField(property, position, ref y, w, lineH);
                    break;

                case SpeakerRef.Kind.Player:
                    var playerStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.4f, 0.8f, 0.4f) }
                    };
                    EditorGUI.LabelField(new Rect(position.x, y, w, lineH), "👤 Player (auto-detected)", playerStyle);
                    y += lineH + 2;
                    break;

                case SpeakerRef.Kind.Narrator:
                    var narratorStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.7f, 0.7f, 0.5f) }
                    };
                    EditorGUI.LabelField(new Rect(position.x, y, w, lineH), "📖 Narrator (italic, no portrait)", narratorStyle);
                    y += lineH + 2;
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var kindProp = property.FindPropertyRelative("speakerKind");
            var kind = (SpeakerRef.Kind)kindProp.enumValueIndex;
            int lines = 2; // kind + npc field/label
            if (kind == SpeakerRef.Kind.Npc && property.FindPropertyRelative("speakerNpc").objectReferenceValue == null)
                lines += 1; // fallback string field
            return (EditorGUIUtility.singleLineHeight + 2) * lines;
        }

        private static void DrawNpcField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var refProp = property.FindPropertyRelative("speakerNpc");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent("NPC (drag .asset)"));
            y += h + 2;

            if (refProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                    property.FindPropertyRelative("refId"), new GUIContent("  └ NPC ID (string)"));
                y += h + 2;
            }
        }
    }
}
#endif
