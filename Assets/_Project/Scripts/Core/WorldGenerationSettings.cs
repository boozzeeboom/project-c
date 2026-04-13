using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Настройки генерации мира Project C
    /// Концепция: обширный мир с горными пиками над облаками
    /// </summary>
    [CreateAssetMenu(fileName = "WorldGenerationSettings", menuName = "ProjectC/World Generation Settings")]
    public class WorldGenerationSettings : ScriptableObject
    {
        [Header("🌍 Масштаб мира")]
        [Tooltip("Радиус мира (масштаб ~Земли, до 350,000 units)")]
        [Range(1000f, 500000f)]
        public float worldRadius = 350000f;
        
        [Tooltip("Количество горных пиков (точек интереса)")]
        [Range(5, 50)]
        public int peakCount = 15;

        [Header("🏔️ Горные пики")]
        [Tooltip("Минимальная высота пика (над облаками)")]
        [Range(500f, 3000f)]
        public float minPeakHeight = 1000f;
        
        [Tooltip("Максимальная высота пика")]
        [Range(2000f, 10000f)]
        public float maxPeakHeight = 8000f;
        
        [Tooltip("Минимальный радиус основания пика")]
        [Range(100f, 1000f)]
        public float minPeakRadius = 200f;
        
        [Tooltip("Максимальный радиус основания пика")]
        [Range(500f, 2000f)]
        public float maxPeakRadius = 800f;
        
        [Tooltip("Детализация меша пика (количество сегментов)")]
        [Range(32, 128)]
        public int peakDetail = 64;

        [Header("☁️ Облачный слой (Legacy)")]
        [Tooltip("Высота облачного слоя (уровень 'нижнего мира')")]
        [Range(0f, 2000f)]
        public float cloudLayerHeight = 1500f;

        [Tooltip("Толщина облачного слоя")]
        [Range(50f, 3000f)]
        public float cloudLayerThickness = 2500f;

        [Tooltip("Плотность облаков (0-1)")]
        [Range(0.1f, 1f)]
        public float cloudDensity = 0.7f;

        [Tooltip("Размер одного облака")]
        [Range(20f, 200f)]
        public float cloudSize = 100f;

        [Tooltip("Вариативность размера облаков")]
        [Range(0.5f, 2f)]
        public float cloudSizeVariation = 2f;

        [Header("☁️ Новая система облаков")]
        [Tooltip("Префаб CloudSystem для генерации облаков")]
        public GameObject cloudSystemPrefab;
        
        [Tooltip("Ссылка на CloudSystem (находится автоматически)")]
        public CloudSystem cloudSystem;

        [Header("🎨 Материалы")]
        [Tooltip("Материал для горных пиков")]
        public Material peakMaterial;
        
        [Tooltip("Материал для облаков")]
        public Material cloudMaterial;

        [Header("🌱 Детали")]
        [Tooltip("Добавлять ли мелкие острова между пиками")]
        public bool addMinorIslands = true;
        
        [Tooltip("Количество мелких островов")]
        [Range(10, 100)]
        public int minorIslandCount = 30;
    }
}
