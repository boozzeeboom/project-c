using UnityEngine;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Один кумуло-дождевой (грозовой) облачный столб
    /// Вертикальная структура от Y=1200 до Y=9000 с 3 секциями:
    /// - Base (1200-3000м) - тёмно-серое основание
    /// - Body (3000-7000м) - серое тело облака
    /// - Anvil (7000-9000м) - наковальня вершина
    /// </summary>
    public class CumulonimbusCloud : MonoBehaviour
    {
        [Header("Параметры столба")]
        [Tooltip("Высота основания (м)")]
        [SerializeField] private float veilHeight = 1200f;

        [Tooltip("Максимальная высота (м)")]
        [SerializeField] private float maxHeight = 9000f;

        [Tooltip("Базовый радиус (м)")]
        [SerializeField] private float baseRadius = 800f;

        [Tooltip("Верхний радиус (м, расширение к вершине)")]
        [SerializeField] private float topRadius = 1600f;

        [Header("Материалы")]
        [Tooltip("Материал для всего столба")]
        [SerializeField] private Material cloudMaterial;

        [Header("Молнии")]
        [Tooltip("Включить систему молний")]
        [SerializeField] private bool enableLightning = true;

        [Tooltip("Particle System для молний")]
        [SerializeField] private ParticleSystem lightningParticles;

        // Внутренние компоненты
        private GameObject cloudMeshObject;

        /// <summary>
        /// Инициализация облачного столба
        /// </summary>
        public void Initialize(float xPos, float zPos, float baseRadiusOverride = 0f, float topRadiusOverride = 0f)
        {
            // Позиция по XZ
            transform.position = new Vector3(xPos, veilHeight, zPos);

            // Переопределение радиусов если переданы
            float actualBaseRadius = baseRadiusOverride > 0f ? baseRadiusOverride : baseRadius;
            float actualTopRadius = topRadiusOverride > 0f ? topRadiusOverride : topRadius;

            // Создать меш облака
            CreateCloudMesh(actualBaseRadius, actualTopRadius);

            // Настроить молнии если включены
            if (enableLightning && lightningParticles != null)
            {
                SetupLightning();
            }
        }

        /// <summary>
        /// Создать меш облачного столба
        /// Используем стандартный Cylinder примитив Unity (после BUG-0001 с крашем)
        /// </summary>
        private void CreateCloudMesh(float baseRadius, float topRadius)
        {
            // Создаём объект-цилиндр
            cloudMeshObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cloudMeshObject.name = "CumulonimbusMesh";
            cloudMeshObject.transform.SetParent(transform);
            cloudMeshObject.transform.localPosition = Vector3.zero;

            // Высота столба
            float height = maxHeight - veilHeight;
            float cloudHeight = height / 2f; // Центр цилиндра

            // Масштаб: Cylinder по умолчанию 1x2x1 (высота 2 единицы)
            cloudMeshObject.transform.localScale = new Vector3(
                baseRadius / 0.5f,  // radius -> scale.x (0.5 = default radius)
                cloudHeight / 2f,   // height/2 -> scale.y (2 = default height)
                baseRadius / 0.5f   // radius -> scale.z
            );

            // Позиция: центр столба
            cloudMeshObject.transform.localPosition = new Vector3(0, cloudHeight, 0);

            // Назначить материал
            if (cloudMaterial != null)
            {
                Renderer renderer = cloudMeshObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = cloudMaterial;
                }
            }

            // Убрать Collider (не нужен для облака)
            Collider collider = cloudMeshObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        /// <summary>
        /// Настроить систему молний
        /// </summary>
        private void SetupLightning()
        {
            if (lightningParticles == null) return;

            // Позиция: центр столба
            lightningParticles.transform.SetParent(transform);
            lightningParticles.transform.localPosition = new Vector3(0, (maxHeight - veilHeight) / 2f, 0);
            lightningParticles.transform.localRotation = Quaternion.identity;

            // Масштаб Particle System (уменьшен после BUG-0001 краша)
            lightningParticles.transform.localScale = new Vector3(100f, 100f, 100f);

            // Настроить главную систему
            var main = lightningParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

            // Фиолетовый цвет молний (#b366ff)
            main.startColor = new Color(0.7f, 0.4f, 1f, 0.9f);

            // Интервал вспышек: 30-90 секунд
            main.startDelay = new ParticleSystem.MinMaxCurve(30f, 90f);

            // Burst: 5-10 частиц на вспышку
            var bursts = new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(5f, 10f))
            };

            var emission = lightningParticles.emission;
            emission.SetBursts(bursts);
            emission.rateOverTime = 0f; // Только через bursts

            Debug.Log("[CumulonimbusCloud] Молнии настроены: интервал 30-90с, цвет #b366ff");
        }

        /// <summary>
        /// Уничтожить облачный столб
        /// </summary>
        public void DestroyCloud()
        {
            if (cloudMeshObject != null)
            {
                Destroy(cloudMeshObject);
                cloudMeshObject = null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Получить высоту основания
        /// </summary>
        public float GetVeilHeight() => veilHeight;

        /// <summary>
        /// Получить максимальную высоту
        /// </summary>
        public float GetMaxHeight() => maxHeight;
    }
}
