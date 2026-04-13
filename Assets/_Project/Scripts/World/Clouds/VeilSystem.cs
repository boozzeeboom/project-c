using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Система завесы — визуальная угроза внизу мира.
    /// Плоскость Y=12.0 + URP Exponential Fog + молнии + триггеры предупреждения.
    /// НЕ зависит от ландшафта и обычных облаков.
    /// </summary>
    public class VeilSystem : MonoBehaviour
    {
        [Header("Параметры завесы")]
        [Tooltip("Высота завесы (Y). Должна совпадать с minAltitude глобального коридора")]
        public float veilHeight = 12.0f;

        [Tooltip("Высота зоны предупреждения (Y)")]
        public float warningHeight = 14.0f;

        [Tooltip("Размер плоскости завесы")]
        public Vector2 veilSize = new Vector2(20000f, 20000f);

        [Header("Материал завесы")]
        [Tooltip("Материал с VeilShader")]
        public Material veilMaterial;

        [Header("Молнии")]
        [Tooltip("Particle System для молний")]
        public ParticleSystem lightningParticles;

        [Tooltip("Минимальный интервал между молниями (секунды)")]
        public float minLightningInterval = 20f;

        [Tooltip("Максимальный интервал между молниями (секунды)")]
        public float maxLightningInterval = 60f;

        [Tooltip("Цвет молний (#b366ff)")]
        public Color lightningColor = new Color(0.7f, 0.4f, 1.0f, 1.0f);

        [Header("Под-завесный туман")]
        [Tooltip("Высота под-завесного тумана (Y)")]
        public float subVeilFogHeight = 8.0f;

        [Tooltip("Плотность под-завесного тумана")]
        public float subVeilFogDensity = 0.01f;

        // Внутренние ссылки
        private GameObject veilPlane;
        private Volume veilVolume;
        private Volume subVeilVolume;
        private BoxCollider warningTrigger;
        private float nextLightningTime;

        /// <summary>
        /// Инициализация завесы.
        /// </summary>
        private void Start()
        {
            CreateVeilPlane();
            CreateWarningTrigger();
            CreateVeilFogVolume();
            CreateSubVeilFogVolume();
            SetupLightningParticles();
        }

        private void Update()
        {
            // Обновляем молнии по таймеру
            if (Time.time >= nextLightningTime)
            {
                TriggerLightning();
                nextLightningTime = Time.time + Random.Range(minLightningInterval, maxLightningInterval);
            }

            // Обновляем интенсивность молний в материале
            UpdateLightningIntensity();
        }

        /// <summary>
        /// Создать плоскость завесы.
        /// </summary>
        private void CreateVeilPlane()
        {
            veilPlane = new GameObject("VeilPlane");
            veilPlane.transform.SetParent(transform);
            veilPlane.transform.position = new Vector3(0, veilHeight, 0);

            // Создаём меш плоскости
            MeshFilter meshFilter = veilPlane.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreatePlaneMesh(1, 1);

            MeshRenderer meshRenderer = veilPlane.AddComponent<MeshRenderer>();

            if (veilMaterial != null)
            {
                meshRenderer.sharedMaterial = veilMaterial;
            }
            else
            {
                // Создаём дефолтный материал если не назначен
                Material defaultMat = new Material(Shader.Find("Project C/Clouds/VeilShader"));
                defaultMat.SetColor("_VeilColor", new Color(0.176f, 0.106f, 0.306f, 0.95f));
                defaultMat.SetFloat("_DepthFadeStart", 100f);
                defaultMat.SetFloat("_DepthFadeEnd", 500f);
                veilMaterial = defaultMat;
                meshRenderer.sharedMaterial = defaultMat;
            }

            // Масштабируем
            veilPlane.transform.localScale = new Vector3(veilSize.x, 1f, veilSize.y);
        }

        /// <summary>
        /// Создать триггер зоны предупреждения.
        /// </summary>
        private void CreateWarningTrigger()
        {
            GameObject triggerObj = new GameObject("VeilWarningTrigger");
            triggerObj.transform.SetParent(transform);
            triggerObj.transform.position = new Vector3(0, warningHeight, 0);

            warningTrigger = triggerObj.AddComponent<BoxCollider>();
            warningTrigger.isTrigger = true;
            // Размер: огромная площадь, толщина 2 units (от veilHeight до warningHeight)
            warningTrigger.size = new Vector3(veilSize.x, warningHeight - veilHeight, veilSize.y);

            // Добавляем скрипт-обработчик (можно вынести в отдельный компонент)
            triggerObj.AddComponent<VeilWarningZone>();
        }

        /// <summary>
        /// Создать Volume с exponential fog для завесы.
        /// </summary>
        private void CreateVeilFogVolume()
        {
            // Примечание: URP Volume с Fog override создаётся через Editor.
            // Здесь создаём базовый Volume, но для полной настройки fog
            // рекомендуется создать Volume Profile вручную в Editor.
            GameObject fogObj = new GameObject("VeilFogVolume");
            fogObj.transform.SetParent(transform);
            fogObj.transform.position = new Vector3(0, veilHeight + 2f, 0);

            veilVolume = fogObj.AddComponent<Volume>();
            veilVolume.isGlobal = false;

            // Примечание: для runtime создания Volume с Fog нужен Volume Profile
            // Это лучше сделать через Editor. Здесь — заглушка.
            Debug.LogWarning("[VeilSystem] Fog Volume создан, но для полной настройки создайте Volume Profile в Editor с Fog override (density=0.003, color=#2d1b4e)");
        }

        /// <summary>
        /// Создать под-завесный туман (более плотный, для Y < 8).
        /// </summary>
        private void CreateSubVeilFogVolume()
        {
            GameObject fogObj = new GameObject("SubVeilFogVolume");
            fogObj.transform.SetParent(transform);
            fogObj.transform.position = new Vector3(0, subVeilFogHeight, 0);

            subVeilVolume = fogObj.AddComponent<Volume>();
            subVeilVolume.isGlobal = false;

            Debug.LogWarning("[VeilSystem] Под-завесный туман создан. Создайте Volume Profile в Editor с Fog override (density=0.01)");
        }

        /// <summary>
        /// Настроить Particle System для молний.
        /// </summary>
        private void SetupLightningParticles()
        {
            if (lightningParticles != null)
            {
                // Настраиваем параметры
                var main = lightningParticles.main;
                main.loop = true;
                main.startSpeed = 0f;
                main.startLifetime = 0.5f;
                main.startColor = lightningColor;
                main.maxParticles = 10;

                var emission = lightningParticles.emission;
                emission.rateOverTime = 0f; // Вручную вызываем молнии

                var shape = lightningParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(veilSize.x * 0.5f, 30f, veilSize.y * 0.5f);
            }
            else
            {
                // Создаём дефолтный Particle System
                GameObject particlesObj = new GameObject("VeilLightning");
                particlesObj.transform.SetParent(transform);
                particlesObj.transform.position = new Vector3(0, veilHeight + 15f, 0);

                lightningParticles = particlesObj.AddComponent<ParticleSystem>();

                var main = lightningParticles.main;
                main.loop = true;
                main.startSpeed = 0f;
                main.startLifetime = 0.3f;
                main.startColor = lightningColor;
                main.maxParticles = 5;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = lightningParticles.emission;
                emission.rateOverTime = 0f;

                var shape = lightningParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(5000f, 50f, 5000f);

                // Renderer для молний — используем простой trail
                var renderer = lightningParticles.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) renderer = lightningParticles.gameObject.AddComponent<ParticleSystemRenderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            // Первое время молнии
            nextLightningTime = Time.time + Random.Range(minLightningInterval, maxLightningInterval);
        }

        /// <summary>
        /// Вызвать вспышку молнии.
        /// </summary>
        private void TriggerLightning()
        {
            if (lightningParticles != null)
            {
                // Выпускаем 1-3 частицы (молнии)
                int count = Random.Range(1, 4);
                lightningParticles.Emit(count);
            }

            // Обновляем интенсивность в материале
            if (veilMaterial != null)
            {
                veilMaterial.SetFloat("_LightningIntensity", 1.0f);
            }
        }

        /// <summary>
        /// Обновить интенсивность молний (затухание).
        /// </summary>
        private void UpdateLightningIntensity()
        {
            if (veilMaterial != null)
            {
                float current = veilMaterial.GetFloat("_LightningIntensity");
                if (current > 0.01f)
                {
                    // Затухание за 0.5 секунды
                    veilMaterial.SetFloat("_LightningIntensity", Mathf.Lerp(current, 0f, Time.deltaTime * 4f));
                }
            }
        }

        /// <summary>
        /// Создать плоский меш (1x1, в XZ плоскости).
        /// </summary>
        private Mesh CreatePlaneMesh(float width, float height)
        {
            Mesh mesh = new Mesh();
            mesh.name = "VeilPlaneMesh";

            Vector3[] vertices = new Vector3[4];
            Vector2[] uv = new Vector2[4];
            int[] triangles = new int[6];

            // Вершины
            vertices[0] = new Vector3(-0.5f, 0, -0.5f);
            vertices[1] = new Vector3(0.5f, 0, -0.5f);
            vertices[2] = new Vector3(0.5f, 0, 0.5f);
            vertices[3] = new Vector3(-0.5f, 0, 0.5f);

            // UV
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(1, 1);
            uv[3] = new Vector2(0, 1);

            // Треугольники (два, CW winding)
            triangles[0] = 0; triangles[1] = 1; triangles[2] = 2;
            triangles[3] = 0; triangles[4] = 2; triangles[5] = 3;

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }
    }

    /// <summary>
    /// Триггерная зона завесы — предупреждает при входе.
    /// </summary>
    public class VeilWarningZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            Debug.LogWarning($"[VeilWarningZone] {other.name} вошёл в зону предупреждения завесы!");
            // Здесь можно вызвать UI предупреждение, звук и т.д.
        }

        private void OnTriggerStay(Collider other)
        {
            // Постоянная тряска/предупреждение
        }

        private void OnTriggerExit(Collider other)
        {
            Debug.Log($"[VeilWarningZone] {other.name} покинул зону предупреждения завесы.");
        }
    }
}
