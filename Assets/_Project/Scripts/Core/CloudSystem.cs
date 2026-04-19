using UnityEngine;
using ProjectC.World.Clouds;

namespace ProjectC.Core
{
    /// <summary>
    /// Главная система облаков
    /// Управляет всеми облачными слоями и циклом дня/ночи
    /// </summary>
    public class CloudSystem : MonoBehaviour
    {
        [Header("Конфигурации слоёв")]
        [Tooltip("Конфигурация верхнего слоя (перистые, 6000-8000м)")]
        public CloudLayerConfig upperLayerConfig;

        [Tooltip("Конфигурация среднего слоя (высоко-кучевые, 3000-5000м)")]
        public CloudLayerConfig middleLayerConfig;

        [Tooltip("Конфигурация нижнего слоя (слоистые, 1500-3000м)")]
        public CloudLayerConfig lowerLayerConfig;

        [Header("Кумуло-дождевые облака")]
        [Tooltip("Включить кумуло-дождевые облака (4-й слой)")]
        public bool enableCumulonimbus = true;

        [Tooltip("Конфигурация кумуло-дождевых облаков (Storm слой)")]
        public CloudLayerConfig cumulonimbusConfig;

        [Header("Ссылки")]
        [Tooltip("Родительский объект для всех облаков")]
        public Transform cloudParent;

        [Header("Компоненты слоёв")]
        [SerializeField] private CloudLayer upperLayer;
        [SerializeField] private CloudLayer middleLayer;
        [SerializeField] private CloudLayer lowerLayer;
        [SerializeField] private CumulonimbusManager cumulonimbusManager;

        [Header("Цикл дня и ночи")]
        [Tooltip("Включить смену дня и ночи")]
        public bool enableDayNightCycle = false;
        
        [Tooltip("Длительность полного цикла в минутах")]
        public float cycleDurationMinutes = 20f;
        
        [Tooltip("Текущее время суток (0-24 часа)")]
        [Range(0f, 24f)]
        public float currentTimeOfDay = 12f;

        [Header("Освещение")]
        public Light sunLight;

        [Header("Информация")]
        [SerializeField] private int totalCloudCount = 0;

        /// <summary>
        /// Инициализация системы
        /// </summary>
        private void Start()
        {
            // Найти или создать родителя
            if (cloudParent == null)
            {
                GameObject cloudRoot = new GameObject("Clouds");
                cloudParent = cloudRoot.transform;
            }

            // Создать слои
            CreateLayers();

            // Запустить цикл дня и ночи
            if (enableDayNightCycle && sunLight != null)
            {
                UpdateDayNightCycle();
            }
        }

        /// <summary>
        /// Обновление цикла дня и ночи
        /// </summary>
        private void Update()
        {
            if (enableDayNightCycle)
            {
                UpdateDayNightCycle();
            }
        }

        /// <summary>
        /// Создать все облачные слои
        /// </summary>
        private void CreateLayers()
        {
            // Верхний слой
            if (upperLayerConfig != null)
            {
                GameObject upperObj = new GameObject("CloudLayer_Upper");
                upperObj.transform.SetParent(cloudParent);
                upperLayer = upperObj.AddComponent<CloudLayer>();
                upperLayer.config = upperLayerConfig;
            }
            else
            {
                Debug.LogWarning("[CloudSystem] Upper слой не создан — не назначен CloudLayerConfig_Upper");
            }

            // Средний слой
            if (middleLayerConfig != null)
            {
                GameObject middleObj = new GameObject("CloudLayer_Middle");
                middleObj.transform.SetParent(cloudParent);
                middleLayer = middleObj.AddComponent<CloudLayer>();
                middleLayer.config = middleLayerConfig;
            }
            else
            {
                Debug.LogWarning("[CloudSystem] Middle слой не создан — не назначен CloudLayerConfig_Middle");
            }

            // Нижний слой
            if (lowerLayerConfig != null)
            {
                GameObject lowerObj = new GameObject("CloudLayer_Lower");
                lowerObj.transform.SetParent(cloudParent);
                lowerLayer = lowerObj.AddComponent<CloudLayer>();
                lowerLayer.config = lowerLayerConfig;
            }
            else
            {
                Debug.LogWarning("[CloudSystem] Lower слой не создан — не назначен CloudLayerConfig_Lower");
            }

            // Проверка: все ли конфиги назначены
            int configsAssigned = 0;
            if (upperLayerConfig != null) configsAssigned++;
            if (middleLayerConfig != null) configsAssigned++;
            if (lowerLayerConfig != null) configsAssigned++;
            
            if (configsAssigned == 3)
            {
                // All layers configured
            }
            else
            {
                Debug.LogWarning($"[CloudSystem] Настроено {configsAssigned}/3 слоёв. Назначьте оставшиеся CloudLayerConfig в Inspector CloudSystem");
            }

            // Кумуло-дождевые облака (4-й слой)
            if (enableCumulonimbus)
            {
                GameObject cumulonimbusObj = new GameObject("CumulonimbusManager");
                cumulonimbusObj.transform.SetParent(cloudParent);
                cumulonimbusManager = cumulonimbusObj.AddComponent<CumulonimbusManager>();

                // Если есть конфиг — применяем настройки
                if (cumulonimbusConfig != null)
                {
                    cumulonimbusManager.veilHeight = cumulonimbusConfig.minHeight;
                    cumulonimbusManager.maxHeight = cumulonimbusConfig.maxHeight;
                    cumulonimbusManager.cloudCount = Mathf.RoundToInt(4f * cumulonimbusConfig.density);
                    cumulonimbusManager.cloudCount = Mathf.Clamp(cumulonimbusManager.cloudCount, 3, 5);
                }

            }

            // Подсчитать облака
            UpdateCloudCount();
        }

        /// <summary>
        /// Обновить цикл дня и ночи
        /// </summary>
        private void UpdateDayNightCycle()
        {
            // Расчёт времени
            float hoursPerSecond = 24f / (cycleDurationMinutes * 60f);
            currentTimeOfDay += hoursPerSecond * Time.deltaTime;
            
            if (currentTimeOfDay >= 24f)
            {
                currentTimeOfDay = 0f;
            }

            // Обновление солнца
            if (sunLight != null)
            {
                UpdateSunPosition();
                UpdateSunColor();
            }
        }

        /// <summary>
        /// Обновить позицию солнца
        /// </summary>
        private void UpdateSunPosition()
        {
            // Угол солнца: 6h = восток, 12h = зенит, 18h = запад, 0h = ночь
            float angle = ((currentTimeOfDay - 6f) / 24f) * 360f;
            
            Vector3 sunDirection = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad),
                0
            ).normalized;

            sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection);
        }

        /// <summary>
        /// Обновить цвет солнца
        /// </summary>
        private void UpdateSunColor()
        {
            Color sunColor;
            float intensity;

            // Рассвет (5-7h)
            if (currentTimeOfDay >= 5f && currentTimeOfDay < 7f)
            {
                float t = (currentTimeOfDay - 5f) / 2f;
                sunColor = Color.Lerp(new Color(1f, 0.5f, 0.3f), Color.white, t);
                intensity = Mathf.Lerp(0.2f, 1f, t);
            }
            // День (7-17h)
            else if (currentTimeOfDay >= 7f && currentTimeOfDay < 17f)
            {
                sunColor = Color.white;
                intensity = 1f;
            }
            // Закат (17-19h)
            else if (currentTimeOfDay >= 17f && currentTimeOfDay < 19f)
            {
                float t = (currentTimeOfDay - 17f) / 2f;
                sunColor = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.3f), t);
                intensity = Mathf.Lerp(1f, 0.2f, t);
            }
            // Ночь (19-5h)
            else
            {
                sunColor = new Color(0.1f, 0.1f, 0.2f);
                intensity = 0.2f;
            }

            sunLight.color = sunColor;
            sunLight.intensity = intensity;
        }

        /// <summary>
        /// Подсчитать общее количество облаков
        /// </summary>
        private void UpdateCloudCount()
        {
            totalCloudCount = 0;
            if (upperLayer != null) totalCloudCount += upperLayer.GetCloudCount();
            if (middleLayer != null) totalCloudCount += middleLayer.GetCloudCount();
            if (lowerLayer != null) totalCloudCount += lowerLayer.GetCloudCount();
            
            // Кумуло-дождевые облака
            if (cumulonimbusManager != null)
            {
                var clouds = cumulonimbusManager.GetActiveClouds();
                if (clouds != null)
                {
                    totalCloudCount += clouds.Length;
                }
            }
        }

        /// <summary>
        /// Пересгенерировать все слои
        /// </summary>
        [ContextMenu("Regenerate All Layers")]
        public void RegenerateAllLayers()
        {
            if (upperLayer != null) upperLayer.RegenerateLayer();
            if (middleLayer != null) middleLayer.RegenerateLayer();
            if (lowerLayer != null) lowerLayer.RegenerateLayer();
            if (cumulonimbusManager != null) cumulonimbusManager.RespawnAllClouds();
            UpdateCloudCount();
        }

        /// <summary>
        /// Очистить все слои
        /// </summary>
        [ContextMenu("Clear All Layers")]
        public void ClearAllLayers()
        {
            if (upperLayer != null) upperLayer.ClearLayer();
            if (middleLayer != null) middleLayer.ClearLayer();
            if (lowerLayer != null) lowerLayer.ClearLayer();
            // Кумуло-дождевые не очищаем — они создаются один раз
            UpdateCloudCount();
        }

        /// <summary>
        /// Получить общее количество облаков
        /// </summary>
        public int GetTotalCloudCount()
        {
            return totalCloudCount;
        }
    }
}
