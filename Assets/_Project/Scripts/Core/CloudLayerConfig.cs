using UnityEngine;
using ProjectC.CloudGenerator;

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

        [Header("Generator v7.0 Fields")]
        [Tooltip("Archetype for generator7.0")]
        public CloudArchetype archetype = CloudArchetype.Sphere;
        [Tooltip("Seed for deterministic generation")]
        public int generatorSeed = 42;
        [Tooltip("Jitter for position variation")]
        [Range(0f, 1f)]
        public float jitter = 0.3f;
        [Tooltip("Clustering strength")]
        [Range(0f, 1f)]
        public float clustering = 0.5f;
        [Tooltip("Additional position variation")]
        [Range(0f, 2f)]
        public float positionVariation = 0.5f;

        [Header("Sphere Archetype")]
        public int cascadeDepth = 3;
        public int bumpsPerLevel = 24;
        public float childRatio = 30f;
        public float sizeVariationGen = 1.0f;
        public int parentCount = 1;
        public float ellipsoidY = 50f;
        public float ellipsoidXZ = 100f;
        public int maxSphereCount = 5000;
        public float sphereCountScale = 1f;
        public ProjectC.CloudGenerator.SizeRange sizeRange = new ProjectC.CloudGenerator.SizeRange();

        [Header("Column Archetype")]
        public ProjectC.CloudGenerator.ColumnParams columnParams = new ProjectC.CloudGenerator.ColumnParams();

        [Header("Platform Archetype")]
        public ProjectC.CloudGenerator.PlatformParams platformParams = new ProjectC.CloudGenerator.PlatformParams();

        [Header("Tree Archetype")]
        public ProjectC.CloudGenerator.TreeParams treeParams = new ProjectC.CloudGenerator.TreeParams();

        [Header("Parent Mesh (EventCloud)")]
        public string parentMeshPath = "";
        public float parentMeshScaleX = 1f;
        public float parentMeshScaleY = 1f;
        public float parentMeshScaleZ = 1f;
        public float parentMeshRotX = 0f;
        public float parentMeshRotY = 0f;
        public float parentMeshRotZ = 0f;
        public float parentMeshOffsetX = 0f;
        public float parentMeshOffsetY = 0f;
        public float parentMeshOffsetZ = 0f;
    }
}
