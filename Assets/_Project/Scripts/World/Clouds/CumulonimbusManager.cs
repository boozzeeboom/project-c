using UnityEngine;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Менеджер кумуло-дождевых (грозовых) облаков
    /// Создаёт 3-5 вертикальных грозовых столбов в радиусе 10000м от центра мира
    /// Каждый столб: Y=1200 (основание) до Y=9000 (наковальня)
    /// </summary>
    public class CumulonimbusManager : MonoBehaviour
    {
        [Header("Параметры размещения")]
        [Tooltip("Высота завесы/основания (м)")]
        [SerializeField] public float veilHeight = 1200f;

        [Tooltip("Максимальная высота (м)")]
        [SerializeField] public float maxHeight = 9000f;

        [Tooltip("Количество грозовых столбов")]
        [SerializeField] public int cloudCount = 4;

        [Tooltip("Радиус размещения от центра мира (м)")]
        [SerializeField] public float spawnRadius = 10000f;

        [Header("Материалы")]
        [Tooltip("Материал для грозовых облаков")]
        [SerializeField] public Material cumulonimbusMaterial;

        [Header("Молнии")]
        [Tooltip("Включить молнии")]
        [SerializeField] public bool enableLightning = true;

        [Tooltip("Префаб Particle System для молний")]
        [SerializeField] public GameObject lightningParticlesPrefab;

        // Активные облака
        private CumulonimbusCloud[] activeClouds;

        /// <summary>
        /// Инициализация менеджера
        /// </summary>
        private void Start()
        {
            SpawnAllClouds();
        }

        /// <summary>
        /// Создать все грозовые облака
        /// </summary>
        public void SpawnAllClouds()
        {
            // Очистить старые если есть
            ClearAllClouds();

            // Создать массив
            activeClouds = new CumulonimbusCloud[cloudCount];


            // Создать каждое облако
            for (int i = 0; i < cloudCount; i++)
            {
                // Случайная позиция в круге
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(0f, spawnRadius);

                float xPos = Mathf.Cos(angle) * radius;
                float zPos = Mathf.Sin(angle) * radius;

                // Создать объект
                GameObject cloudObj = new GameObject($"Cumulonimbus_{i}");
                cloudObj.transform.SetParent(transform);

                // Добавить компонент
                CumulonimbusCloud cloud = cloudObj.AddComponent<CumulonimbusCloud>();

                // Настроить параметры
                cloud.GetComponent<CumulonimbusCloud>().Initialize(
                    xPos: xPos,
                    zPos: zPos,
                    baseRadiusOverride: 800f,
                    topRadiusOverride: 1600f
                );

                // Назначить материал если есть
                // (через публичное поле в CumulonimbusCloud)

                activeClouds[i] = cloud;
            }

        }

        /// <summary>
        /// Пересоздать все облака
        /// </summary>
        public void RespawnAllClouds()
        {
            Debug.Log("[CumulonimbusManager] Респавн всех грозовых облаков");
            SpawnAllClouds();
        }

        /// <summary>
        /// Очистить все облака
        /// </summary>
        public void ClearAllClouds()
        {
            if (activeClouds == null) return;

            foreach (var cloud in activeClouds)
            {
                if (cloud != null)
                {
                    cloud.DestroyCloud();
                }
            }

            activeClouds = null;
        }

        /// <summary>
        /// Получить массив активных облаков
        /// </summary>
        public CumulonimbusCloud[] GetActiveClouds()
        {
            return activeClouds;
        }

        /// <summary>
        /// Очистка при уничтожении
        /// </summary>
        private void OnDestroy()
        {
            ClearAllClouds();
        }
    }
}
