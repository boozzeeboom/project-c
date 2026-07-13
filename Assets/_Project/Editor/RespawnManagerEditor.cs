using UnityEditor;
using UnityEngine;
using ProjectC.World;

namespace ProjectC.Editor
{
    [CustomEditor(typeof(RespawnManager))]
    public class RespawnManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _respawnPoints;

        private void OnEnable()
        {
            _respawnPoints = serializedObject.FindProperty("_respawnPoints");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Respawn Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Индекс 0 — fallback-точка по умолчанию.\n" +
                "Spawn Point (Transform) приоритетнее Fallback Position.\n" +
                "Trigger Zone — при входе игрока назначает эту точку активной.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            if (_respawnPoints.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Нет точек респавна. Нажми «Add Respawn Point» для добавления.", MessageType.Warning);
            }

            // Рисуем список вручную с компактным лейаутом
            for (int i = 0; i < _respawnPoints.arraySize; i++)
            {
                var element = _respawnPoints.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Заголовок
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Point {i}{(i == 0 ? " (Fallback)" : "")}", EditorStyles.boldLabel);
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(18)))
                {
                    _respawnPoints.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    return; // выходим, список изменился
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;

                var fallbackProp = element.FindPropertyRelative("fallbackPosition");
                var spawnPointProp = element.FindPropertyRelative("spawnPoint");
                var triggerZoneProp = element.FindPropertyRelative("triggerZone");

                EditorGUILayout.PropertyField(fallbackProp, new GUIContent("Fallback Position"));
                EditorGUILayout.PropertyField(spawnPointProp, new GUIContent("Spawn Point"));
                EditorGUILayout.PropertyField(triggerZoneProp, new GUIContent("Trigger Zone"));

                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(8);

            // Кнопка Add
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("+ Add Respawn Point", GUILayout.Height(28)))
            {
                _respawnPoints.InsertArrayElementAtIndex(_respawnPoints.arraySize);
            }
            GUI.backgroundColor = Color.white;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
