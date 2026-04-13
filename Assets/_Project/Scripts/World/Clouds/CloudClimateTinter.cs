using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Применяет цветовой тинт к облаку на основе его позиции
    /// Определяет ближайший горный массив (по XZ) и применяет соответствующий оттенок
    /// Нижние облака (Y < 2000) получают фиолетовый оттенок от завесы
    /// </summary>
    public class CloudClimateTinter : MonoBehaviour
    {
        [Header("Базовые настройки")]
        [Tooltip("Базовый цвет облака из CloudLayerConfig")]
        public Color baseColor = Color.white;

        [Tooltip("Тип слоя для определения логики тинта")]
        public CloudLayerType layerType = CloudLayerType.Middle;

        [Header("Настройки завесы")]
        [Tooltip("Высота завесы (VeilSystem)")]
        [SerializeField] private float veilHeight = 1200f;

        [Tooltip("Цвет завесы для фиолетового оттенка нижних облаков")]
        [SerializeField] private Color purpleVeil = new Color(0.176f, 0.106f, 0.306f); // #2d1b4e

        [Header("Цвета горных массивов")]
        [Tooltip("Himalayan: тёплый оттенок")]
        [SerializeField] private Color himalayanColor = new Color(0.941f, 0.902f, 0.816f); // #f0e6d0

        [Tooltip("Alpine: нейтральный оттенок")]
        [SerializeField] private Color alpineColor = new Color(0.831f, 0.816f, 0.784f); // #d4d0c8

        [Tooltip("African: песочный оттенок")]
        [SerializeField] private Color africanColor = new Color(0.91f, 0.863f, 0.784f); // #e8dcc8

        [Tooltip("Andean: прохладный оттенок")]
        [SerializeField] private Color andeanColor = new Color(0.784f, 0.753f, 0.816f); // #c8c0d0

        [Tooltip("Alaskan: холодный оттенок")]
        [SerializeField] private Color alaskanColor = new Color(0.847f, 0.863f, 0.91f); // #d8dce8

        private Renderer cloudRenderer;
        private Material cloudMaterial;

        /// <summary>
        /// Инициализация при старте
        /// </summary>
        private void Start()
        {
            cloudRenderer = GetComponent<Renderer>();
            if (cloudRenderer == null)
            {
                Debug.LogWarning("[CloudClimateTinter] Renderer не найден на " + gameObject.name);
                enabled = false;
                return;
            }

            // Получить материал (instance, не оригинал)
            cloudMaterial = cloudRenderer.material;

            // Применить тинт
            ApplyTint();
        }

        /// <summary>
        /// Применить цветовой тинт на основе позиции облака
        /// </summary>
        private void ApplyTint()
        {
            Vector3 cloudPosition = transform.position;
            float cloudY = cloudPosition.y;

            // Определить цвет горного массива по XZ
            Color mountainTint = GetMountainTintColor(cloudPosition.x, cloudPosition.z);

            // Смешать базовый цвет с тинтом горного массива
            Color finalColor = Color.Lerp(baseColor, mountainTint, 0.3f); // 30% влияния горного массива

            // Для нижних облаков near завесы — добавить фиолетовый оттенок
            if (cloudY < 2000f && layerType == CloudLayerType.Lower)
            {
                float t = Mathf.InverseLerp(veilHeight, 2000f, cloudY); // 0 у завесы, 1 на 2000
                finalColor = Color.Lerp(purpleVeil, finalColor, t);
            }

            // Применить цвет к материалу
            if (cloudMaterial != null)
            {
                // Попробовать установить _BaseColor (CloudGhibli shader)
                if (cloudMaterial.HasProperty("_BaseColor"))
                {
                    Color existingBase = cloudMaterial.GetColor("_BaseColor");
                    // Сохранить alpha из оригинального материала
                    finalColor.a = existingBase.a;
                    cloudMaterial.SetColor("_BaseColor", finalColor);
                }
                else
                {
                    // Fallback для стандартных шейдеров
                    cloudMaterial.color = finalColor;
                }
            }
        }

        /// <summary>
        /// Определить цвет тинта на основе позиции горного массива
        /// </summary>
        private Color GetMountainTintColor(float x, float z)
        {
            // Himalayan: X: 0-5000, Z: 0-5000
            if (x >= 0f && x <= 5000f && z >= 0f && z <= 5000f)
            {
                return himalayanColor;
            }

            // Alpine: X: -6000 - -2000, Z: 2000-6000
            if (x >= -6000f && x <= -2000f && z >= 2000f && z <= 6000f)
            {
                return alpineColor;
            }

            // African: X: 0-3000, Z: -7000 - -3000
            if (x >= 0f && x <= 3000f && z >= -7000f && z <= -3000f)
            {
                return africanColor;
            }

            // Andean: X: -4000 - -1000, Z: -6000 - -2000
            if (x >= -4000f && x <= -1000f && z >= -6000f && z <= -2000f)
            {
                return andeanColor;
            }

            // Alaskan: X: 2000-6000, Z: 4000-8000
            if (x >= 2000f && x <= 6000f && z >= 4000f && z <= 8000f)
            {
                return alaskanColor;
            }

            // По умолчанию — нейтральный (без тинта)
            return Color.white;
        }

        /// <summary>
        /// Пересчитать тинт (можно вызвать извне при изменении позиции)
        /// </summary>
        public void RecalculateTint()
        {
            ApplyTint();
        }
    }
}
