using System.Collections.Generic;
using UnityEngine;
using ProjectC.World.Clouds;

namespace ProjectC.Core
{
    /// <summary>
    /// Менеджер одного облачного слоя
    /// Генерирует и управляет облаками на основе CloudLayerConfig
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class CloudLayer : MonoBehaviour
    {
        [Header("Настройки слоя")]
        [Tooltip("Конфигурация этого облачного слоя")]
        public CloudLayerConfig config;

        [Header("Информация (только чтение)")]
        [SerializeField] private int cloudCount = 0;

        // Список сгенерированных облаков
        private List<GameObject> clouds = new List<GameObject>();
        
        // Таймер для анимации
        private float morphTimer = 0f;

        /// <summary>
        /// Сгенерировать облачный слой
        /// Вызывается автоматически при старте
        /// </summary>
        private void Start()
        {
            if (config == null)
            {
                Debug.LogWarning($"[CloudLayer] Конфигурация не назначена на {gameObject.name}");
                return;
            }

            GenerateLayer();
        }

        /// <summary>
        /// Обновление движения и анимации
        /// </summary>
        private void Update()
        {
            if (clouds.Count == 0) return;

            // Движение слоя
            MoveLayer();

            // Анимация формы
            if (config.animateMorph)
            {
                AnimateMorph();
            }
        }

        /// <summary>
        /// Сгенерировать все облака слоя
        /// </summary>
        public void GenerateLayer()
        {
            // Очистить старые облака
            ClearClouds();

            if (config == null)
            {
                Debug.LogError("[CloudLayer] Конфигурация не назначена!");
                return;
            }

            // Попробовать использовать CloudGhibli шейдер
            TrySetupGhibliShader();

            // Расчёт количества облаков
            int count = CalculateCloudCount();

            // Генерация облаков
            for (int i = 0; i < count; i++)
            {
                CreateCloud();
            }

            cloudCount = clouds.Count;
        }

        /// <summary>
        /// Попытка назначить CloudGhibli шейдер на материал конфига
        /// </summary>
        private void TrySetupGhibliShader()
        {
            if (config.cloudMaterial != null)
            {
                // Проверяем, используется ли старый Standard шейдер
                if (config.cloudMaterial.shader.name == "Standard" ||
                    config.cloudMaterial.shader.name.Contains("Universal Render Pipeline/Unlit"))
                {
                    // Пробуем найти CloudGhibli
                    Shader ghibliShader = Shader.Find("ProjectC/CloudGhibli");
                    if (ghibliShader != null)
                    {
                        // Создаём новый материал с CloudGhibli
                        Material ghibliMat = new Material(ghibliShader);

                        // Копируем базовый цвет из старого материала (URP использует _BaseColor)
                        Color baseColor = config.cloudMaterial.HasProperty("_BaseColor")
                            ? config.cloudMaterial.GetColor("_BaseColor")
                            : Color.white;
                        ghibliMat.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f));

                        // Настраиваем rim glow (Ghibli signature)
                        ghibliMat.SetColor("_RimColor", new Color(1f, 0.85f, 0.6f, 0.6f));
                        ghibliMat.SetFloat("_RimPower", 2.0f);
                        ghibliMat.SetFloat("_AlphaBase", 0.4f);
                        ghibliMat.SetFloat("_Softness", 0.3f);
                        ghibliMat.SetFloat("_VertexDisplacement", 3.0f);

                        // Назначаем noise-текстуры
                        ghibliMat.SetTexture("_NoiseTex", ProceduralNoiseGenerator.GetNoiseTexture1());
                        ghibliMat.SetTexture("_NoiseTex2", ProceduralNoiseGenerator.GetNoiseTexture2());
                        ghibliMat.SetFloat("_NoiseScale", 1.0f);

                        config.cloudMaterial = ghibliMat;
                        Debug.Log($"[CloudLayer] Используется CloudGhibli шейдер для {gameObject.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[CloudLayer] CloudGhibli шейдер не найден, используется fallback материал.");
                    }
                }
            }
        }

        /// <summary>
        /// Расчёт количества облаков на основе плотности
        /// </summary>
        private int CalculateCloudCount()
        {
            // Базовое количество на основе плотности (увеличено в 3 раза)
            float baseCount = 300f * config.density;
            
            // Вариативность
            int variation = Random.Range(-20, 20);
            
            return Mathf.Max(50, Mathf.RoundToInt(baseCount) + variation);
        }

        /// <summary>
        /// Создать одно облако
        /// </summary>
        private void CreateCloud()
        {
            // Примитив: сфера или plane
            GameObject cloud;
            
            if (config.use2DPlanes)
            {
                cloud = GameObject.CreatePrimitive(PrimitiveType.Plane);
            }
            else
            {
                cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            }

            // Позиция в пределах слоя
            Vector3 position = GenerateCloudPosition();
            cloud.transform.position = position;
            cloud.transform.SetParent(transform);

            // Размер с вариативностью
            float sizeVariation = Random.Range(
                1f / config.sizeVariation,
                config.sizeVariation
            );
            float scale = config.cloudSize * sizeVariation;

            if (config.use2DPlanes)
            {
                // Используем Quad вместо Plane - он более предсказуемый
                cloud = GameObject.CreatePrimitive(PrimitiveType.Quad);
                
                // Для quad: горизонтальная плоскость (лежит на XZ)
                cloud.transform.localScale = new Vector3(scale, scale, 1f);
                // Quad по умолчанию смотрит по +Z, кладём на XZ (нормаль по +Y)
                cloud.transform.rotation = Quaternion.Euler(-90f, Random.value * 360f, 0f);
            }
            else
            {
                // Для сферы: объёмная форма
                cloud.transform.localScale = new Vector3(scale, scale * 0.6f, scale);
            }

            // Материал
            if (config.cloudMaterial != null)
            {
                Renderer renderer = cloud.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = config.cloudMaterial;
                }
            }

            // Удалить коллайдер (не нужен для облаков)
            Collider collider = cloud.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Имя
            cloud.name = $"Cloud_{clouds.Count}";

            // Добавить CloudClimateTinter для климатического тинта
            CloudClimateTinter tint = cloud.AddComponent<CloudClimateTinter>();
            // URP использует _BaseColor вместо _Color
            tint.baseColor = config.cloudMaterial != null
                ? (config.cloudMaterial.HasProperty("_BaseColor")
                    ? config.cloudMaterial.GetColor("_BaseColor")
                    : Color.white)
                : Color.white;
            tint.layerType = config.layerType;

            clouds.Add(cloud);
        }

        /// <summary>
        /// Сгенерировать позицию облака в пределах слоя
        /// </summary>
        private Vector3 GenerateCloudPosition()
        {
            // Случайная позиция в круге (радиус 10км)
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Random.value) * 10000f;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // Высота в пределах слоя
            float y = Random.Range(config.minHeight, config.maxHeight);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Движение облачного слоя
        /// </summary>
        private void MoveLayer()
        {
            Vector3 movement = config.moveDirection.normalized * config.moveSpeed * Time.deltaTime;

            foreach (GameObject cloud in clouds)
            {
                if (cloud != null)
                {
                    cloud.transform.position += movement;

                    // Зацикливание: если ушло далеко, вернуть с другой стороны
                    if (cloud.transform.position.magnitude > 15000f)
                    {
                        Vector3 opposite = -cloud.transform.position.normalized * 10000f;
                        cloud.transform.position = opposite;
                    }
                }
            }
        }

        /// <summary>
        /// Анимация формы облаков (плавное изменение масштаба)
        /// </summary>
        private void AnimateMorph()
        {
            morphTimer += Time.deltaTime;
            float morphAmount = Mathf.Sin(morphTimer * config.morphSpeed) * 0.1f;

            foreach (GameObject cloud in clouds)
            {
                if (cloud != null && !config.use2DPlanes)
                {
                    Vector3 scale = cloud.transform.localScale;
                    cloud.transform.localScale = new Vector3(
                        scale.x * (1f + morphAmount * 0.01f),
                        scale.y * (1f - morphAmount * 0.02f),
                        scale.z * (1f + morphAmount * 0.01f)
                    );
                }
            }
        }

        /// <summary>
        /// Очистить все облака
        /// </summary>
        private void ClearClouds()
        {
            foreach (GameObject cloud in clouds)
            {
                if (cloud != null)
                {
                    DestroyImmediate(cloud);
                }
            }
            clouds.Clear();
            cloudCount = 0;
        }

        /// <summary>
        /// Пересгенерировать слой (для редактора)
        /// </summary>
        [ContextMenu("Regenerate Layer")]
        public void RegenerateLayer()
        {
            GenerateLayer();
        }

        /// <summary>
        /// Очистить слой (для редактора)
        /// </summary>
        [ContextMenu("Clear Layer")]
        public void ClearLayer()
        {
            ClearClouds();
        }

        /// <summary>
        /// Получить количество облаков
        /// </summary>
        public int GetCloudCount()
        {
            return clouds.Count;
        }
    }
}
