using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Настройки генерации мира через ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "WorldGenerationSettings", menuName = "ProjectC/World Generation Settings")]
    public class WorldGenerationSettings : ScriptableObject
    {
        [Header("Основные настройки")]
        [Tooltip("Размер одного острова")]
        public float islandSize = 100f;
        
        [Tooltip("Количество островов")]
        public int islandCount = 10;
        
        [Tooltip("Минимальное расстояние между островами")]
        public float minDistanceBetweenIslands = 200f;

        [Header("Настройки высоты")]
        [Tooltip("Базовая высота островов")]
        public float baseHeight = 50f;
        
        [Tooltip("Максимальная высота")]
        public float maxHeight = 200f;
        
        [Tooltip("Шум для высоты (Perlin Noise Scale)")]
        public float heightNoiseScale = 0.01f;

        [Header("Настройки формы")]
        [Tooltip("Шум для формы острова")]
        public float shapeNoiseScale = 0.02f;
        
        [Tooltip("Количество вершин в острове")]
        public int islandVertices = 20;

        [Header("Облака")]
        [Tooltip("Высота слоя облаков")]
        public float cloudLayerHeight = 300f;
        
        [Tooltip("Плотность облаков")]
        public float cloudDensity = 0.3f;
        
        [Tooltip("Размер одного облака")]
        public float cloudSize = 50f;
    }
}
