using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ProjectC.World.Core;
using ProjectC.World.Generation;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor-скрипт для заполнения PeakData в 5× MountainMassif ассетах.
    /// Данные из WorldLandscape_Design.md §3 (29 пиков с координатами).
    /// 
    /// КРИТИЧНО: Y = реальные метры (1:1), НЕ scaled units!
    /// Использование: Tools → Project C → Fill MountainMassif Peak Data
    /// </summary>
    public class PeakDataFiller : EditorWindow
    {
        private Vector2 _scrollPos;

        [MenuItem("Tools/Project C/Fill MountainMassif Peak Data")]
        public static void ShowWindow()
        {
            GetWindow<PeakDataFiller>("Peak Data Filler");
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Peak Data Filler", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Заполняет 29 пиков в 5× MountainMassif ассетах.\n" +
                "Данные из WorldLandscape_Design.md §3.\n" +
                "Y = реальные метры (1:1), НЕ scaled units!",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Заполнить ВСЕ массивы (29 пиков)", GUILayout.Height(40)))
            {
                FillAllMassifs();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Заполнить Гималайский массив (8 пиков)"))
                FillHimalayanMassif();

            if (GUILayout.Button("Заполнить Альпийский массив (6 пиков)"))
                FillAlpineMassif();

            if (GUILayout.Button("Заполнить Африканский массив (4 пика)"))
                FillAfricanMassif();

            if (GUILayout.Button("Заполнить Андийский массив (6 пиков)"))
                FillAndeanMassif();

            if (GUILayout.Button("Заполнить Аляскинский массив (5 пиков)"))
                FillAlaskanMassif();

            EditorGUILayout.EndScrollView();
        }

        private void FillAllMassifs()
        {
            FillHimalayanMassif();
            FillAlpineMassif();
            FillAfricanMassif();
            FillAndeanMassif();
            FillAlaskanMassif();

            EditorUtility.DisplayDialog("Готово!",
                "Все 5 массивов заполнены (29 пиков).\n" +
                "Проверьте в Inspector каждого MountainMassif ассета.",
                "OK");
        }

        #region Himalayan Massif (8 пиков)

        private void FillHimalayanMassif()
        {
            var massif = FindMassif("HimalayanMassif");
            if (massif == null) { Debug.LogError("HimalayanMassif not found!"); return; }

            massif.peaks.Clear();

            // Эверест (Главный)
            // A3 ФИНАЛ: baseRadius увеличен для нормального h/r
            // meshHeight = baseRadius * 1.5 (Tectonic) * 1.2 (MainCity) = baseRadius * 1.8
            // Для h/r = 1.8: baseRadius = 250 → meshHeight = 450
            // XZ ×50 для масштабирования к реальным расстояниям
            massif.peaks.Add(CreatePeak(
                "everest", "Эверест", PeakRole.MainCity, PeakShapeType.Tectonic,
                new Vector3(0, 88.48f, 0), 8848, 250,
                hasSnowCap: true, snowLineY: 55.00f, hasCrater: false,
                rockColor: new Color(0.35f, 0.35f, 0.35f)
            ));

            // Лхоцзе
            massif.peaks.Add(CreatePeak(
                "lhoteze", "Лхоцзе", PeakRole.Military, PeakShapeType.Tectonic,
                new Vector3(30000, 72.00f, -20000), 8516, 200,
                hasSnowCap: true, snowLineY: 55.00f, hasCrater: false,
                rockColor: new Color(0.33f, 0.33f, 0.33f)
            ));

            // Макалу
            massif.peaks.Add(CreatePeak(
                "makalu", "Макалу", PeakRole.Navigation, PeakShapeType.Tectonic,
                new Vector3(60000, 70.00f, 15000), 8485, 180,
                hasSnowCap: true, snowLineY: 55.00f, hasCrater: false,
                rockColor: new Color(0.34f, 0.34f, 0.34f)
            ));

            // Чо-Ойю
            massif.peaks.Add(CreatePeak(
                "cho_oyu", "Чо-Ойю", PeakRole.Farm, PeakShapeType.Tectonic,
                new Vector3(-40000, 65.00f, 30000), 8188, 170,
                hasSnowCap: true, snowLineY: 55.00f, hasCrater: false,
                rockColor: new Color(0.36f, 0.36f, 0.36f)
            ));

            // Шишапангма
            massif.peaks.Add(CreatePeak(
                "shishapangma", "Шишапангма", PeakRole.Abandoned, PeakShapeType.Tectonic,
                new Vector3(-70000, 62.00f, 45000), 8027, 160,
                hasSnowCap: true, snowLineY: 55.00f, hasCrater: false,
                rockColor: new Color(0.32f, 0.32f, 0.32f)
            ));

            // Пик Северный
            massif.peaks.Add(CreatePeak(
                "peak_north", "Пик Северный", PeakRole.Farm, PeakShapeType.Tectonic,
                new Vector3(10000, 55.00f, 60000), 5500, 130,
                hasSnowCap: false, snowLineY: 50.00f, hasCrater: false,
                rockColor: new Color(0.38f, 0.38f, 0.38f)
            ));

            // Пик Южный
            massif.peaks.Add(CreatePeak(
                "peak_south", "Пик Южный", PeakRole.Farm, PeakShapeType.Tectonic,
                new Vector3(-15000, 50.00f, -50000), 5000, 100,
                hasSnowCap: false, snowLineY: 50.00f, hasCrater: false,
                rockColor: new Color(0.37f, 0.37f, 0.37f)
            ));

            // Пик Восточный
            massif.peaks.Add(CreatePeak(
                "peak_east", "Пик Восточный", PeakRole.Military, PeakShapeType.Tectonic,
                new Vector3(75000, 45.00f, -10000), 4500, 140,
                hasSnowCap: false, snowLineY: 50.00f, hasCrater: false,
                rockColor: new Color(0.36f, 0.36f, 0.36f)
            ));

            massif.massifRadius = 150000; // ×50 от 3000
            EditorUtility.SetDirty(massif);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PeakDataFiller] HimalayanMassif: {massif.peaks.Count} peaks filled, radius=3000");
        }

        #endregion

        #region Alpine Massif (6 пиков)

        private void FillAlpineMassif()
        {
            var massif = FindMassif("AlpineMassif");
            if (massif == null) { Debug.LogError("AlpineMassif not found!"); return; }

            massif.peaks.Clear();

            // Монблан (Главный)
            massif.peaks.Add(CreatePeak(
                "montblanc", "Монблан", PeakRole.MainCity, PeakShapeType.Tectonic,
                new Vector3(-65500, 48.08f, 140500), 4808, 180,
                hasSnowCap: true, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.54f, 0.54f, 0.48f)
            ));

            // Гранд-Жорасс
            massif.peaks.Add(CreatePeak(
                "grandes_jorasses", "Гранд-Жорасс", PeakRole.Military, PeakShapeType.Tectonic,
                new Vector3(-45000, 42.00f, 155000), 4208, 130,
                hasSnowCap: true, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.52f, 0.52f, 0.46f)
            ));

            // Маттерхорн
            massif.peaks.Add(CreatePeak(
                "matterhorn", "Маттерхорн", PeakRole.Military, PeakShapeType.Tectonic,
                new Vector3(-30000, 40.00f, 120000), 4478, 140,
                hasSnowCap: true, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.53f, 0.53f, 0.47f)
            ));

            // Финстераархорн
            massif.peaks.Add(CreatePeak(
                "finsteraarhorn", "Финстераархорн", PeakRole.Abandoned, PeakShapeType.Tectonic,
                new Vector3(-50000, 38.00f, 175000), 4274, 150,
                hasSnowCap: true, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.51f, 0.51f, 0.45f)
            ));

            // Вайсхорн
            massif.peaks.Add(CreatePeak(
                "weisshorn", "Вайсхорн", PeakRole.Farm, PeakShapeType.Tectonic,
                new Vector3(-90000, 40.00f, 125000), 4506, 120,
                hasSnowCap: true, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.55f, 0.55f, 0.49f)
            ));

            // Пик ЮЗ
            massif.peaks.Add(CreatePeak(
                "peak_sw", "Пик ЮЗ", PeakRole.Abandoned, PeakShapeType.Tectonic,
                new Vector3(-85000, 35.00f, 110000), 3500, 90,
                hasSnowCap: false, snowLineY: 30.00f, hasCrater: false,
                rockColor: new Color(0.50f, 0.50f, 0.44f)
            ));

            massif.massifRadius = 75000; // ×50 от 1500
            EditorUtility.SetDirty(massif);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PeakDataFiller] AlpineMassif: {massif.peaks.Count} peaks filled, radius=1500");
        }

        #endregion

        #region African Massif (4 пика)

        private void FillAfricanMassif()
        {
            var massif = FindMassif("AfricanMassif");
            if (massif == null) { Debug.LogError("AfricanMassif not found!"); return; }

            massif.peaks.Clear();

            // Кибо (Главный)
            massif.peaks.Add(CreatePeak(
                "kibo", "Кибо", PeakRole.MainCity, PeakShapeType.Volcanic,
                new Vector3(-94050, 58.95f, -150500), 5895, 220,
                hasSnowCap: true, snowLineY: 45.00f, hasCrater: true,
                rockColor: new Color(0.48f, 0.35f, 0.29f)
            ));

            // Мавензи
            massif.peaks.Add(CreatePeak(
                "mawenzi", "Мавензи", PeakRole.Navigation, PeakShapeType.Volcanic,
                new Vector3(-70000, 48.00f, -140000), 5149, 160,
                hasSnowCap: false, snowLineY: 45.00f, hasCrater: false,
                rockColor: new Color(0.46f, 0.33f, 0.27f)
            ));

            // Шира
            massif.peaks.Add(CreatePeak(
                "shira", "Шира", PeakRole.Farm, PeakShapeType.Volcanic,
                new Vector3(-115000, 35.00f, -160000), 3962, 120,
                hasSnowCap: false, snowLineY: 40.00f, hasCrater: false,
                rockColor: new Color(0.44f, 0.31f, 0.25f)
            ));

            // Пик Восточный
            massif.peaks.Add(CreatePeak(
                "peak_east", "Пик Восточный", PeakRole.Navigation, PeakShapeType.Dome,
                new Vector3(-60000, 30.00f, -130000), 3000, 100,
                hasSnowCap: false, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.42f, 0.29f, 0.23f)
            ));

            massif.massifRadius = 50000; // ×50 от 1000
            EditorUtility.SetDirty(massif);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PeakDataFiller] AfricanMassif: {massif.peaks.Count} peaks filled, radius=1000");
        }

        #endregion

        #region Andean Massif (6 пиков)

        private void FillAndeanMassif()
        {
            var massif = FindMassif("AndeanMassif");
            if (massif == null) { Debug.LogError("AndeanMassif not found!"); return; }

            massif.peaks.Clear();

            // Аконкагуа (Главный)
            massif.peaks.Add(CreatePeak(
                "aconcagua", "Аконкагуа", PeakRole.MainCity, PeakShapeType.Tectonic,
                new Vector3(-208800, 69.62f, -105500), 6962, 280,
                hasSnowCap: true, snowLineY: 50.00f, hasCrater: false,
                rockColor: new Color(0.42f, 0.35f, 0.29f)
            ));

            // Охос-дель-Саладо
            massif.peaks.Add(CreatePeak(
                "ojos_del_salado", "Охос-дель-Саладо", PeakRole.Navigation, PeakShapeType.Tectonic,
                new Vector3(-180000, 58.00f, -70000), 6893, 180,
                hasSnowCap: true, snowLineY: 50.00f, hasCrater: false,
                rockColor: new Color(0.40f, 0.33f, 0.27f)
            ));

            // Невадо-Трес-Крусес
            massif.peaks.Add(CreatePeak(
                "nevado_tres_cruces", "Невадо-Трес-Крусес", PeakRole.Navigation, PeakShapeType.Tectonic,
                new Vector3(-170000, 52.00f, -50000), 6748, 160,
                hasSnowCap: true, snowLineY: 50.00f, hasCrater: false,
                rockColor: new Color(0.41f, 0.34f, 0.28f)
            ));

            // Пик Северный
            massif.peaks.Add(CreatePeak(
                "peak_north", "Пик Северный", PeakRole.Farm, PeakShapeType.Tectonic,
                new Vector3(-225000, 55.00f, -60000), 5500, 140,
                hasSnowCap: false, snowLineY: 45.00f, hasCrater: false,
                rockColor: new Color(0.43f, 0.36f, 0.30f)
            ));

            // Пик Южный
            massif.peaks.Add(CreatePeak(
                "peak_south", "Пик Южный", PeakRole.Navigation, PeakShapeType.Tectonic,
                new Vector3(-200000, 48.00f, -140000), 4800, 120,
                hasSnowCap: false, snowLineY: 45.00f, hasCrater: false,
                rockColor: new Color(0.39f, 0.32f, 0.26f)
            ));

            // Пик Западный
            massif.peaks.Add(CreatePeak(
                "peak_west", "Пик Западный", PeakRole.Abandoned, PeakShapeType.Tectonic,
                new Vector3(-260000, 42.00f, -120000), 4200, 100,
                hasSnowCap: false, snowLineY: 40.00f, hasCrater: false,
                rockColor: new Color(0.38f, 0.31f, 0.25f)
            ));

            massif.massifRadius = 125000; // ×50 от 2500
            EditorUtility.SetDirty(massif);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PeakDataFiller] AndeanMassif: {massif.peaks.Count} peaks filled, radius=2500");
        }

        #endregion

        #region Alaskan Massif (5 пиков)

        private void FillAlaskanMassif()
        {
            var massif = FindMassif("AlaskanMassif");
            if (massif == null) { Debug.LogError("AlaskanMassif not found!"); return; }

            massif.peaks.Clear();

            // Денали (Главный)
            massif.peaks.Add(CreatePeak(
                "denali", "Денали", PeakRole.MainCity, PeakShapeType.Isolated,
                new Vector3(62750, 61.90f, 234250), 6190, 250,
                hasSnowCap: true, snowLineY: 45.00f, hasCrater: false,
                rockColor: new Color(0.29f, 0.29f, 0.29f)
            ));

            // Форакер
            massif.peaks.Add(CreatePeak(
                "foraker", "Форакер", PeakRole.Navigation, PeakShapeType.Dome,
                new Vector3(35000, 52.00f, 210000), 5304, 160,
                hasSnowCap: true, snowLineY: 45.00f, hasCrater: false,
                rockColor: new Color(0.30f, 0.30f, 0.30f)
            ));

            // Хантер
            massif.peaks.Add(CreatePeak(
                "hunter", "Хантер", PeakRole.Navigation, PeakShapeType.Tectonic,
                new Vector3(85000, 42.00f, 225000), 4442, 120,
                hasSnowCap: true, snowLineY: 40.00f, hasCrater: false,
                rockColor: new Color(0.28f, 0.28f, 0.28f)
            ));

            // Пик СЗ
            massif.peaks.Add(CreatePeak(
                "peak_nw", "Пик СЗ", PeakRole.Farm, PeakShapeType.Dome,
                new Vector3(25000, 40.00f, 260000), 4000, 100,
                hasSnowCap: false, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.31f, 0.31f, 0.31f)
            ));

            // Пик Восточный
            massif.peaks.Add(CreatePeak(
                "peak_east", "Пик Восточный", PeakRole.Navigation, PeakShapeType.Tectonic,
                new Vector3(110000, 35.00f, 240000), 3500, 110,
                hasSnowCap: false, snowLineY: 35.00f, hasCrater: false,
                rockColor: new Color(0.27f, 0.27f, 0.27f)
            ));

            massif.massifRadius = 75000; // ×50 от 1500
            EditorUtility.SetDirty(massif);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PeakDataFiller] AlaskanMassif: {massif.peaks.Count} peaks filled, radius=1500");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Создать PeakData с заполненными полями.
        /// </summary>
        private PeakData CreatePeak(
            string peakId,
            string displayName,
            PeakRole role,
            PeakShapeType shapeType,
            Vector3 worldPosition,
            float realHeightMeters,
            float baseRadius,
            bool hasSnowCap,
            float snowLineY,
            bool hasCrater,
            Color rockColor)
        {
            var peak = new PeakData
            {
                peakId = peakId,
                displayName = displayName,
                role = role,
                worldPosition = worldPosition,
                realHeightMeters = realHeightMeters,
                shapeType = shapeType,
                baseRadius = baseRadius,
                hasSnowCap = hasSnowCap,
                snowLineY = snowLineY,
                hasCrater = hasCrater,
                rockColor = rockColor,
                heightProfile = CreateDefaultHeightCurve(shapeType),
                keypoints = CreateDefaultKeypoints(shapeType)
            };

            return peak;
        }

        /// <summary>
        /// Создать AnimationCurve по типу формы.
        /// </summary>
        private AnimationCurve CreateDefaultHeightCurve(PeakShapeType shapeType)
        {
            var curve = new AnimationCurve();

            switch (shapeType)
            {
                case PeakShapeType.Tectonic:
                    // Крутые склоны, острая вершина
                    curve.AddKey(0f, 0f);       // База
                    curve.AddKey(0.2f, 0.15f);
                    curve.AddKey(0.4f, 0.35f);
                    curve.AddKey(0.6f, 0.60f);
                    curve.AddKey(0.8f, 0.85f);
                    curve.AddKey(1f, 1f);       // Вершина
                    break;

                case PeakShapeType.Volcanic:
                    // Пологие склоны, округлая вершина
                    curve.AddKey(0f, 0f);
                    curve.AddKey(0.2f, 0.25f);
                    curve.AddKey(0.4f, 0.50f);
                    curve.AddKey(0.6f, 0.70f);
                    curve.AddKey(0.8f, 0.88f);
                    curve.AddKey(1f, 0.95f);    // Кратер depression
                    break;

                case PeakShapeType.Dome:
                    // Очень пологий купол
                    curve.AddKey(0f, 0f);
                    curve.AddKey(0.2f, 0.30f);
                    curve.AddKey(0.4f, 0.55f);
                    curve.AddKey(0.6f, 0.75f);
                    curve.AddKey(0.8f, 0.90f);
                    curve.AddKey(1f, 0.95f);
                    break;

                case PeakShapeType.Isolated:
                    // Широкая база, крутая вершина (экспоненциальный)
                    curve.AddKey(0f, 0f);
                    curve.AddKey(0.15f, 0.10f);
                    curve.AddKey(0.3f, 0.25f);
                    curve.AddKey(0.5f, 0.50f);
                    curve.AddKey(0.7f, 0.75f);
                    curve.AddKey(0.9f, 0.95f);
                    curve.AddKey(1f, 1f);
                    break;
            }

            return curve;
        }

        /// <summary>
        /// Создать стандартные keypoints для формы.
        /// </summary>
        private List<HeightmapKeypoint> CreateDefaultKeypoints(PeakShapeType shapeType)
        {
            var keypoints = new List<HeightmapKeypoint>();

            // Стандартный профиль (из Landscape_TechnicalDesign.md §1.5)
            keypoints.Add(new HeightmapKeypoint { normalizedRadius = 0.00f, normalizedHeight = 1.00f, noiseWeight = 0.1f });
            keypoints.Add(new HeightmapKeypoint { normalizedRadius = 0.10f, normalizedHeight = 0.95f, noiseWeight = 0.2f });
            keypoints.Add(new HeightmapKeypoint { normalizedRadius = 0.25f, normalizedHeight = 0.80f, noiseWeight = 0.4f });
            keypoints.Add(new HeightmapKeypoint { normalizedRadius = 0.40f, normalizedHeight = 0.60f, noiseWeight = 0.6f });
            keypoints.Add(new HeightmapKeypoint { normalizedRadius = 0.60f, normalizedHeight = 0.35f, noiseWeight = 0.8f });
            keypoints.Add(new HeightmapKeypoint { normalizedRadius = 0.80f, normalizedHeight = 0.15f, noiseWeight = 0.5f });
            keypoints.Add(new HeightmapKeypoint { normalizedRadius = 1.00f, normalizedHeight = 0.00f, noiseWeight = 0.2f });

            return keypoints;
        }

        /// <summary>
        /// Найти MountainMassif ассет по имени файла.
        /// </summary>
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

            // Попробовать найти в стандартном пути
            string standardPath = $"Assets/_Project/Data/World/Massifs/{fileName}.asset";
            var massif = AssetDatabase.LoadAssetAtPath<MountainMassif>(standardPath);
            if (massif != null) return massif;

            Debug.LogError($"[PeakDataFiller] Cannot find MountainMassif asset: {fileName}");
            return null;
        }

        #endregion
    }
}
