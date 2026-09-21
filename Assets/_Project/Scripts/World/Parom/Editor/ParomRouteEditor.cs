// ParomRouteEditor.cs — T-PAROM-01
// Кастомный инспектор: кнопка перестройки тросов + проверки настройки ветки.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectC.World.Parom
{
    [CustomEditor(typeof(ParomRoute))]
    public class ParomRouteEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var route = (ParomRoute)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Действия", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("⟳ Перестроить тросы", GUILayout.Height(28)))
            {
                route.RebuildCables();
                EditorUtility.SetDirty(route);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Проверки", EditorStyles.boldLabel);

            // Корень сцены: FO 🟢 — ветка должна быть под WorldScene_*.
            Transform root = route.transform;
            bool underWorldScene = false;
            Transform p = root.parent;
            while (p != null)
            {
                if (p.name.StartsWith("WorldScene_")) { underWorldScene = true; break; }
                p = p.parent;
            }
            if (!underWorldScene)
                EditorGUILayout.HelpBox(
                    "Ветка НЕ под корнем WorldScene_* — при сдвиге мира (F8) тросы и кабинка НЕ поедут с миром. " +
                    "Перетащите объект под корень сцены (см. docs/world/parom_road/00_PAROM_DESIGN.md §4).",
                    MessageType.Error);

            var so = serializedObject;
            Transform start = so.FindProperty("_startAnchor").objectReferenceValue as Transform;
            Transform end = so.FindProperty("_endAnchor").objectReferenceValue as Transform;
            if (start == null || end == null)
                EditorGUILayout.HelpBox(
                    "Нужны минимум 2 станции: Start Anchor и End Anchor. Без них кабинка и тросы скрыты.",
                    MessageType.Warning);

            Object prefab = so.FindProperty("_trolleyPrefab").objectReferenceValue;
            if (prefab == null)
                EditorGUILayout.HelpBox(
                    "Trolley Prefab пуст — в Play построится примитивная кабинка-заглушка. " +
                    "Назначьте FBX-модель кабинки с полом-коллайдером.",
                    MessageType.Info);

            Object mat = so.FindProperty("_cableMaterial").objectReferenceValue;
            if (mat == null)
                EditorGUILayout.HelpBox(
                    "Cable Material пуст — тросы рисуются дефолтным материалом линий.",
                    MessageType.Info);

            var overrides = so.FindProperty("_stationDwellOverrides");
            var intermediates = so.FindProperty("_intermediateAnchors");
            int expected = 2 + (intermediates != null ? intermediates.arraySize : 0);
            if (overrides != null && overrides.arraySize > 0 && overrides.arraySize != expected)
                EditorGUILayout.HelpBox(
                    $"Station Dwell Overrides: {overrides.arraySize} значений, а станций (старт + промежуточные + конец) — {expected}. " +
                    "Пока длины не совпадут, используется общее Dwell Seconds.",
                    MessageType.Warning);

            EditorGUILayout.HelpBox(
                "Пассажир едет стоя (moving-platform carry): слой кабинки должен входить в _platformMask " +
                "префаба игрока/NPC, иначе carry не подхватит.",
                MessageType.Info);
        }
    }
}
#endif
