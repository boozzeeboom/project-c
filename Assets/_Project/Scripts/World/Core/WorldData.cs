using System.Collections.Generic;
using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Core
{
    /// <summary>
    /// Главный ScriptableObject мира — содержит все данные о массивах, завесе и облаках.
    /// Создаётся через Unity Editor: Create → Project C → World Data
    /// </summary>
    [CreateAssetMenu(menuName = "Project C/World Data", fileName = "WorldData")]
    public class WorldData : ScriptableObject
    {
        [Header("Масштаб")]
        [Tooltip("Масштаб высот: реальная высота / 100")]
        public float heightScale = 0.01f;

        [Tooltip("Масштаб расстояний: реальное расстояние / 2000")]
        public float distanceScale = 0.0005f;

        [Header("Границы мира")]
        public float worldMinX = -5500f;
        public float worldMaxX = 2500f;
        public float worldMinZ = -3500f;
        public float worldMaxZ = 5500f;

        [Header("Массивы")]
        [Tooltip("Список всех горных массивов мира")]
        public List<MountainMassif> massifs = new List<MountainMassif>();

        [Header("Завеса")]
        [Tooltip("Высота завесы (Y). Должна совпадать с minAltitude глобального коридора")]
        public float veilHeight = 12.0f;

        [Tooltip("Цвет завесы (#2d1b4e)")]
        public Color veilColor = new Color(0.176f, 0.106f, 0.306f, 1f);

        [Tooltip("Плотность тумана завесы")]
        public float veilFogDensity = 0.003f;

        [Header("Облака")]
        [Tooltip("Конфигурация верхнего слоя (70-90)")]
        public CloudLayerConfig upperLayerConfig;

        [Tooltip("Конфигурация среднего слоя (40-70)")]
        public CloudLayerConfig middleLayerConfig;

        [Tooltip("Конфигурация нижнего слоя (15-40)")]
        public CloudLayerConfig lowerLayerConfig;

        /// <summary>
        /// Найти массив по позиции в мире.
        /// </summary>
        public MountainMassif FindMassifAtPosition(Vector3 worldPosition)
        {
            foreach (var massif in massifs)
            {
                if (massif == null) continue;
                float dist = Vector3.Distance(worldPosition, massif.centerPosition);
                if (dist <= massif.massifRadius)
                    return massif;
            }
            return null;
        }
    }
}
