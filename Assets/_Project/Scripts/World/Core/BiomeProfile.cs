using UnityEngine;

namespace ProjectC.World.Core
{
    /// <summary>
    /// Климатический профиль биома — цвета, атмосфера, освещение.
    /// Создаётся через Unity Editor: Create → Project C → Biome Profile
    /// </summary>
    [CreateAssetMenu(menuName = "Project C/Biome Profile", fileName = "BiomeProfile")]
    public class BiomeProfile : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID биома (himalayan, alpine, african, andean, alaskan)")]
        public string biomeId;

        [Tooltip("Отображаемое имя")]
        public string displayName;

        [Header("Цвет неба")]
        [Tooltip("Цвет неба в этом биоме")]
        public Color skyColor = Color.blue;

        [Tooltip("Цвет атмосферного тумана")]
        public Color atmosphereColor = Color.white;

        [Header("Цвет скал")]
        [Tooltip("Основной цвет скальных пород")]
        public Color rockColor = Color.grey;

        [Tooltip("Цвет снежной шапки")]
        public Color snowColor = new Color(0.94f, 0.94f, 0.96f);

        [Header("Освещение")]
        [Tooltip("Интенсивность освещения (0-1)")]
        [Range(0f, 1f)]
        public float lightIntensity = 1f;

        [Tooltip("Цвет освещения (тёплый/холодный)")]
        public Color lightColor = Color.white;

        [Tooltip("Угол падения света (влияет на тени)")]
        [Range(0f, 90f)]
        public float lightAngle = 45f;

        [Header("Атмосфера")]
        [Tooltip("Общая атмосфера биома (описание)")]
        [TextArea]
        public string atmosphereDescription;
    }
}
