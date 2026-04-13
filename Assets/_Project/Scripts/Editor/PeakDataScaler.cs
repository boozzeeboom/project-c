using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ProjectC.World.Core;
using ProjectC.World.Generation;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor utility для обновления PeakData dimensions (V2).
    /// 
    /// Использование: Tools → Project C → Scale Peak Data (V2)
    /// 
    /// Что делает:
    /// 1. Для каждого пика в 5 массивах вычисляет правильные meshHeight и baseRadius
    /// 2. Обновляет PeakData в assets
    /// 3. Сохраняет assets
    /// 
    /// Новая система размеров (ADR-0001):
    /// - Эверест: meshHeight=750, baseRadius=420
    /// - Монблан: meshHeight=650, baseRadius=350
    /// - Кибо: meshHeight=550, baseRadius=400
    /// - Аконкагуа: meshHeight=720, baseRadius=420
    /// - Денали: meshHeight=680, baseRadius=380
    /// - Мелкие пики: 380-500 height
    /// </summary>
    public class PeakDataScaler : EditorWindow
    {
        private Vector2 _scrollPos;
        private bool _showPreview = true;

        [MenuItem("Tools/Project C/Scale Peak Data (V2)")]
        public static void ShowWindow()
        {
            GetWindow<PeakDataScaler>("Peak Data Scaler V2");
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Peak Data Scaler V2 (ADR-0001)", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Обновляет meshHeight и baseRadius для всех 29 пиков.\n\n" +
                "НОВЫЕ РАЗМЕРЫ:\n" +
                "- Эверест: H=750, R=420 (h/r=1.79)\n" +
                "- Монблан: H=650, R=350 (h/r=1.86)\n" +
                "- Кибо: H=550, R=400 (h/r=1.38)\n" +
                "- Аконкагуа: H=720, R=420 (h/r=1.71)\n" +
                "- Денали: H=680, R=380 (h/r=1.79)\n\n" +
                "Это НЕ изменяет позиции пиков — только размеры мешей!",
                MessageType.Info);

            EditorGUILayout.Space();

            _showPreview = EditorGUILayout.Toggle("Показать превью размеров", _showPreview);

            if (_showPreview)
            {
                ShowPreviewTable();
            }

            EditorGUILayout.Space();

            // Кнопка масштабирования ВСЕХ пиков
            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.2f);
            if (GUILayout.Button("Масштабировать ВСЕ пики (29) — V2", GUILayout.Height(50)))
            {
                ScaleAllPeaks();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Кнопки для отдельных массивов
            if (GUILayout.Button("Масштабировать Гималайский массив (8 пиков)"))
                ScaleMassif("HimalayanMassif");

            if (GUILayout.Button("Масштабировать Альпийский массив (6 пиков)"))
                ScaleMassif("AlpineMassif");

            if (GUILayout.Button("Масштабировать Африканский массив (4 пика)"))
                ScaleMassif("AfricanMassif");

            if (GUILayout.Button("Масштабировать Андийский массив (6 пиков)"))
                ScaleMassif("AndeanMassif");

            if (GUILayout.Button("Масштабировать Аляскинский массив (5 пиков)"))
                ScaleMassif("AlaskanMassif");

            EditorGUILayout.EndScrollView();
        }

        private void ShowPreviewTable()
        {
            EditorGUILayout.LabelField("Таблица размеров пиков (V2):", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Заголовок таблицы
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Пик", GUILayout.Width(150));
            GUILayout.Label("Тип", GUILayout.Width(80));
            GUILayout.Label("Role", GUILayout.Width(80));
            GUILayout.Label("meshHeight", GUILayout.Width(100));
            GUILayout.Label("baseRadius", GUILayout.Width(100));
            GUILayout.Label("h/r", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Данные для каждого массива
            string[] massifNames = { "HimalayanMassif", "AlpineMassif", "AfricanMassif", "AndeanMassif", "AlaskanMassif" };

            foreach (var massifName in massifNames)
            {
                var massif = FindMassif(massifName);
                if (massif == null || massif.peaks == null) continue;

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(massif.displayName, EditorStyles.boldLabel);

                foreach (var peak in massif.peaks)
                {
                    if (peak == null) continue;

                    float meshHeight = MountainMeshGenerator.CalculateMeshHeight(peak);
                    float baseRadius = MountainMeshGenerator.CalculateBaseRadius(peak, meshHeight);
                    float hrRatio = meshHeight / baseRadius;

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(peak.displayName, GUILayout.Width(150));
                    GUILayout.Label(peak.shapeType.ToString(), GUILayout.Width(80));
                    GUILayout.Label(peak.role.ToString(), GUILayout.Width(80));
                    GUILayout.Label($"{meshHeight:F0}", GUILayout.Width(100));
                    GUILayout.Label($"{baseRadius:F0}", GUILayout.Width(100));
                    GUILayout.Label($"{hrRatio:F2}", GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void ScaleAllPeaks()
        {
            ScaleMassif("HimalayanMassif");
            ScaleMassif("AlpineMassif");
            ScaleMassif("AfricanMassif");
            ScaleMassif("AndeanMassif");
            ScaleMassif("AlaskanMassif");

            EditorUtility.DisplayDialog("Готово! (V2)",
                "Все 29 пиков масштабированы (V2).\n\n" +
                "Новые размеры:\n" +
                "- Эверест: H=750, R=420\n" +
                "- Монблан: H=650, R=350\n" +
                "- Кибо: H=550, R=400\n" +
                "- Аконкагуа: H=720, R=420\n" +
                "- Денали: H=680, R=380\n\n" +
                "Теперь запустите:\n" +
                "Tools → Project C → Build All Mountain Meshes (V2)",
                "OK");
        }

        private void ScaleMassif(string massifFileName)
        {
            var massif = FindMassif(massifFileName);
            if (massif == null)
            {
                Debug.LogError($"[PeakDataScaler] Massif not found: {massifFileName}");
                return;
            }

            if (massif.peaks == null || massif.peaks.Count == 0)
            {
                Debug.LogWarning($"[PeakDataScaler] {massif.displayName} has no peaks.");
                return;
            }

            int scaledCount = 0;
            foreach (var peak in massif.peaks)
            {
                if (peak == null) continue;

                // Вычислить новые размеры
                float meshHeight = MountainMeshGenerator.CalculateMeshHeight(peak);
                float baseRadius = MountainMeshGenerator.CalculateBaseRadius(peak, meshHeight);

                // Обновить PeakData
                peak.meshHeight = meshHeight;
                peak.baseRadius = baseRadius;

                scaledCount++;

                Debug.Log($"[PeakDataScaler] {peak.displayName}: " +
                          $"meshHeight={meshHeight:F0}, baseRadius={baseRadius:F0}, " +
                          $"h/r={meshHeight / baseRadius:F2}");
            }

            // Сохранить asset
            EditorUtility.SetDirty(massif);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PeakDataScaler] {massif.displayName}: {scaledCount} peaks scaled (V2).");
        }

        #region Helper Methods

        private MountainMassif FindMassif(string fileName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:ScriptableObject {fileName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{fileName}.asset"))
                {
                    return AssetDatabase.LoadAssetAtPath<MountainMassif>(path);
                }
            }

            string standardPath = $"Assets/_Project/Data/World/Massifs/{fileName}.asset";
            var massif = AssetDatabase.LoadAssetAtPath<MountainMassif>(standardPath);
            if (massif != null) return massif;

            Debug.LogError($"[PeakDataScaler] Cannot find MountainMassif asset: {fileName}");
            return null;
        }

        #endregion
    }
}
