using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Тип облачного слоя по высоте
    /// </summary>
    public enum CloudLayerType
    {
        Upper,    // 6000-8000м: перистые (статичные)
        Middle,   // 3000-5000м: высоко-кучевые (анимированные)
        Lower,    // 1500-3000м: слоистые (плотные)
        Storm     // 2000-8000м: грозовые (молнии)
    }

    /// <summary>
    /// Конфигурация облачного слоя
    /// Создаётся через CreateAssetMenu в Unity
    /// </summary>
    [CreateAssetMenu(fileName = "CloudLayerConfig", menuName = "Project C/Cloud Layer Config")]
    public class CloudLayerConfig : ScriptableObject
    {
        [Header("Тип слоя")]
        [Tooltip("Тип облачного слоя (высота и поведение)")]
        public CloudLayerType layerType = CloudLayerType.Middle;

        [Header("Высота слоя")]
        [Tooltip("Минимальная высота генерации облаков (метры)")]
        public float minHeight = 3000f;
        
        [Tooltip("Максимальная высота генерации облаков (метры)")]
        public float maxHeight = 5000f;

        [Header("Плотность и размер")]
        [Tooltip("Плотность облаков (0-1). 0.3 = разряженные, 0.8 = плотные")]
        [Range(0.1f, 1f)]
        public float density = 0.6f;
        
        [Tooltip("Базовый размер одного облака (метры)")]
        [Range(50f, 200f)]
        public float cloudSize = 100f;
        
        [Tooltip("Вариативность размера (множитель). 2.0 = от 50% до 200%")]
        [Range(1.5f, 3f)]
        public float sizeVariation = 2f;

        [Header("Движение слоя")]
        [Tooltip("Скорость движения облаков (метры в секунду)")]
        [Range(0f, 20f)]
        public float moveSpeed = 5f;
        
        [Tooltip("Направление движения (X, Y, Z)")]
        public Vector3 moveDirection = new Vector3(1f, 0f, 0f);
        
        [Tooltip("Анимировать форму облаков (плавное изменение масштаба)")]
        public bool animateMorph = true;
        
        [Tooltip("Скорость анимации формы")]
        [Range(0.1f, 2f)]
        public float morphSpeed = 0.5f;

        [Header("Визуальные настройки")]
        [Tooltip("Материал для облаков")]
        public Material cloudMaterial;
        
        [Tooltip("Использовать 2D planes вместо сфер (для верхнего слоя)")]
        public bool use2DPlanes = false;
    }
}
