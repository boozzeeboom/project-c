using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Core
{
    /// <summary>
    /// Роль пика в мире.
    /// </summary>
    public enum PeakRole
    {
        MainCity,        // Главный город массива (Примум, Секунд...)
        Secondary,       // Вторичный пик с ролью (военная вышка, тюрьма...)
        Farm,            // Фермерский пик
        Military,        // Военный пост
        Abandoned,       // Заброшенная платформа
        Navigation       // Навигационный ориентир
    }

    /// <summary>
    /// Тип формы горного пика.
    /// </summary>
    public enum PeakShapeType
    {
        Tectonic,        // Острые, тектонические (Гималаи, Альпы, Анды)
        Volcanic,        // Округлые, вулканические (Килиманджаро)
        Dome,            // Куполообразные (Форакер)
        Isolated         // Одиночные громады (Денали)
    }

    /// <summary>
    /// Keypoint для heightmap — определяет профиль высоты пика.
    /// </summary>
    [System.Serializable]
    public class HeightmapKeypoint
    {
        [Tooltip("Нормализованный радиус (0..1 от центра к краю)")]
        [Range(0f, 1f)]
        public float normalizedRadius;

        [Tooltip("Нормализованная высота (0..1 от основания к вершине)")]
        [Range(0f, 1f)]
        public float normalizedHeight;

        [Tooltip("Вес шума (0 = гладкий, 1 = максимальный шум)")]
        [Range(0f, 1f)]
        public float noiseWeight;
    }

    /// <summary>
    /// Данные пика — позиция, форма, визуальные параметры.
    /// </summary>
    [System.Serializable]
    public class PeakData
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID пика (everest, lhoteze, ...)")]
        public string peakId;

        [Tooltip("Отображаемое имя")]
        public string displayName;

        [Tooltip("Роль пика в мире")]
        public PeakRole role;

        [Header("Позиция")]
        [Tooltip("Мировые координаты (X, Y scaled, Z)")]
        public Vector3 worldPosition;

        [Tooltip("Реальная высота в метрах (для HUD)")]
        public float realHeightMeters;

        [Header("Форма меша")]
        [Tooltip("Тип формы пика")]
        public PeakShapeType shapeType;

        [Tooltip("Высота меша (units) — V2 параметр")]
        public float meshHeight;

        [Tooltip("Радиус основания (units)")]
        public float baseRadius = 100f;

        [Tooltip("Профиль высоты от основания к вершине")]
        public AnimationCurve heightProfile;

        [Header("Heightmap keypoints")]
        [Tooltip("Ключевые точки для формы меша")]
        public List<HeightmapKeypoint> keypoints = new List<HeightmapKeypoint>();

        [Header("Визуальные")]
        [Tooltip("Цвет скал (переопределяет биом)")]
        public Color rockColor = Color.grey;

        [Tooltip("Высота снеговой линии")]
        public float snowLineY = 50f;

        [Tooltip("Есть ли снежная шапка")]
        public bool hasSnowCap;

        [Tooltip("Есть ли вулканический кратер")]
        public bool hasCrater;
    }

    /// <summary>
    /// Данные хребта — соединяет пики, создаёт седловины.
    /// </summary>
    [System.Serializable]
    public class RidgeData
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID хребта")]
        public string ridgeId;

        [Tooltip("Отображаемое имя")]
        public string displayName;

        [Header("Связанные пики")]
        [Tooltip("ID пиков, соединённых хребтом (минимум 2)")]
        public string[] peakIds = new string[0];

        [Header("Параметры хребта")]
        [Tooltip("Средняя высота гребня (units)")]
        public float ridgeHeight = 30f;

        [Tooltip("Ширина хребта (units)")]
        public float ridgeWidth = 25f;

        [Tooltip("Насколько седловины ниже пиков (units)")]
        public float saddleDrop = 15f;

        [Header("Фермы на хребте")]
        [Tooltip("ID ферм, расположенных на этом хребте")]
        public string[] farmIds = new string[0];
    }

    /// <summary>
    /// Данные фермерского угодья — платформа, террасы, здания.
    /// </summary>
    [System.Serializable]
    public class FarmData
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID фермы (everest_010, mb_s010, ...)")]
        public string farmId;

        [Tooltip("Отображаемое имя")]
        public string displayName;

        [Tooltip("Номер по системе лора (10, 20, 110...)")]
        public int farmNumber;

        [Header("Позиция")]
        [Tooltip("Мировые координаты (X, Y, Z)")]
        public Vector3 worldPosition;

        [Tooltip("ID хребта, на котором расположена")]
        public string parentRidgeId;

        [Header("Платформа")]
        [Tooltip("Размер антигравийной плиты по X")]
        public float platformSizeX = 40f;

        [Tooltip("Размер антигравийной плиты по Z")]
        public float platformSizeZ = 20f;

        [Tooltip("Количество террас")]
        public int terraceCount = 3;

        [Tooltip("Расстояние между террасами (units)")]
        public float terraceSpacing = 5f;

        [Header("Геймплей")]
        [Tooltip("Тип продукции (latex, grain, vegetables, ...)")]
        public string productionType;

        [Tooltip("Есть ли теплицы")]
        public bool hasGreenhouse;

        [Tooltip("Есть ли посадочная площадка")]
        public bool hasDockingPad;

        [Tooltip("Есть ли паромная станция (подвесной трос)")]
        public bool hasFerryStation;

        [Header("Визуальные")]
        [Tooltip("Цвет свечения платформы (#4fc3f7)")]
        public Color platformGlowColor = new Color(0.31f, 0.76f, 0.97f);
    }
}
