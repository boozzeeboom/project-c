using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Процедурный генератор мира Project C
    /// Концепция: обширный мир с горными пиками над плотным облачным слоем
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Настройки генерации")]
        [SerializeField] private WorldGenerationSettings settings;
        
        [Header("Ссылки")]
        [SerializeField] private Transform peaksParent;
        [SerializeField] private Transform cloudsParent;
        [SerializeField] private Transform minorIslandsParent;

        [Header("Рандомизация")]
        [SerializeField] private int seed = 0;
        [SerializeField] private bool randomizeSeed = true;

        [Header("Информация о мире")]
        [SerializeField] private List<PeakInfo> generatedPeaks = new List<PeakInfo>();

        private List<GameObject> generatedClouds = new List<GameObject>();
        private List<GameObject> generatedMinorIslands = new List<GameObject>();

        /// <summary>
        /// Информация о сгенерированном пике
        /// </summary>
        [System.Serializable]
        public class PeakInfo
        {
            public string name;
            public Vector3 position;
            public float height;
            public float radius;
            public GameObject gameObject;
        }

        private void Start()
        {
            // Пытаемся загрузить настройки автоматически, если не назначены
            if (settings == null)
            {
                settings = Resources.Load<WorldGenerationSettings>("WorldGenerationSettings");
                
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<WorldGenerationSettings>();
                }
            }
            
            if (randomizeSeed)
            {
                seed = Random.Range(int.MinValue, int.MaxValue);
            }
            
            Random.InitState(seed);

            GenerateWorld();
        }

        /// <summary>
        /// Сгенерировать весь мир
        /// </summary>
        public void GenerateWorld()
        {
            ClearGeneratedObjects();

            if (settings == null)
            {
                Debug.LogError("[WorldGenerator] WorldGenerationSettings не назначен!");
                return;
            }

            GeneratePeaks();
            GenerateCloudLayer();

            if (settings.addMinorIslands)
            {
                GenerateMinorIslands();
            }
        }

        /// <summary>
        /// Сгенерировать горные пики (точки интереса)
        /// </summary>
        private void GeneratePeaks()
        {
            for (int i = 0; i < settings.peakCount; i++)
            {
                Vector3 position = GeneratePeakPosition(i);
                float height = Random.Range(settings.minPeakHeight, settings.maxPeakHeight);
                float radius = Random.Range(settings.minPeakRadius, settings.maxPeakRadius);
                
                PeakInfo peak = new PeakInfo
                {
                    name = $"Peak_{i}",
                    position = position,
                    height = height,
                    radius = radius
                };
                
                peak.gameObject = CreatePeak(peak);
                generatedPeaks.Add(peak);
            }
        }

        /// <summary>
        /// Сгенерировать позицию для пика (распределение по кругу)
        /// </summary>
        private Vector3 GeneratePeakPosition(int index)
        {
            // Распределяем пики по спирали для равномерного покрытия
            float angle = index * (Mathf.PI * 2f * 0.382f); // Золотой угол
            float radius = Mathf.Lerp(settings.worldRadius * 0.2f, settings.worldRadius * 0.9f, index / (float)settings.peakCount);
            
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = settings.cloudLayerHeight; // Основание на уровне облаков

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Создать горный пик
        /// </summary>
        private GameObject CreatePeak(PeakInfo peakInfo)
        {
            GameObject peak = new GameObject(peakInfo.name);
            peak.transform.position = peakInfo.position;
            peak.transform.SetParent(peaksParent);

            // MeshFilter и MeshRenderer
            MeshFilter meshFilter = peak.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = peak.AddComponent<MeshRenderer>();

            // Генерируем меш горы (конус с шумом)
            Mesh peakMesh = GeneratePeakMesh(peakInfo.height, peakInfo.radius);
            meshFilter.mesh = peakMesh;

            // Материал
            Material mat = settings.peakMaterial != null 
                ? settings.peakMaterial 
                : CreateDefaultPeakMaterial();
            meshRenderer.material = mat;

            // Отключаем коллайдер для больших пиков (избегание варнов Unity)
            // MeshCollider: varns о треугольниках >500 units
            // Для прототипа коллизии не критичны
            // MeshCollider collider = peak.AddComponent<MeshCollider>();
            // collider.sharedMesh = peakMesh;

            return peak;
        }

        /// <summary>
        /// Сгенерировать меш горы (конус с детализацией)
        /// </summary>
        private Mesh GeneratePeakMesh(float height, float radius)
        {
            Mesh mesh = new Mesh();
            mesh.name = "PeakMesh";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector3> normals = new List<Vector3>();

            int segments = settings.peakDetail;
            float angleStep = (Mathf.PI * 2) / segments;
            
            // Количество колец по высоте (каждые ~100м)
            int rings = Mathf.Max(3, Mathf.CeilToInt(height / 100f));
            
            // Вершина горы
            vertices.Add(new Vector3(0, height, 0));
            normals.Add(Vector3.up);

            // Генерируем кольца вершин по высоте
            for (int ring = 1; ring < rings; ring++)
            {
                float t = ring / (float)rings; // 0..1
                float ringY = height * (1 - t);
                float ringRadius = radius * t;
                
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * angleStep;
                    float x = Mathf.Cos(angle) * ringRadius;
                    float z = Mathf.Sin(angle) * ringRadius;
                    
                    // Шум для неровности
                    float noise = Mathf.PerlinNoise(x * 0.01f + ring * 0.5f, z * 0.01f + ring * 0.5f) * 10f;
                    
                    vertices.Add(new Vector3(x, ringY + noise, z));
                    normals.Add(Vector3.up);
                }
            }

            // Основание горы
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                
                float noise = Mathf.PerlinNoise(x * 0.01f, z * 0.01f) * 20f;
                
                vertices.Add(new Vector3(x, noise, z));
                normals.Add(Vector3.up);
            }

            // Треугольники между кольцами
            int ringVertexCount = segments;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                int currentRingStart = 1 + (ring * segments);
                int nextRingStart = 1 + ((ring + 1) * segments);
                
                for (int i = 0; i < segments; i++)
                {
                    int nextI = (i + 1) % segments;
                    
                    // Первый треугольник
                    triangles.Add(currentRingStart + i);
                    triangles.Add(currentRingStart + nextI);
                    triangles.Add(nextRingStart + i);
                    
                    // Второй треугольник
                    triangles.Add(currentRingStart + nextI);
                    triangles.Add(nextRingStart + nextI);
                    triangles.Add(nextRingStart + i);
                }
            }

            // Соединяем последнее кольцо с основанием
            int lastRingStart = 1 + ((rings - 2) * segments);
            int baseStart = 1 + ((rings - 1) * segments);
            
            for (int i = 0; i < segments; i++)
            {
                int nextI = (i + 1) % segments;
                
                triangles.Add(lastRingStart + i);
                triangles.Add(lastRingStart + nextI);
                triangles.Add(baseStart + i);
                
                triangles.Add(lastRingStart + nextI);
                triangles.Add(baseStart + nextI);
                triangles.Add(baseStart + i);
            }

            // Основание горы (дно)
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0, -50f, 0));
            
            int bottomRingStart = 1 + ((rings - 1) * segments);
            for (int i = 0; i < segments; i++)
            {
                vertices.Add(vertices[bottomRingStart + i]);
            }

            // Треугольники дна
            for (int i = 1; i < segments; i++)
            {
                triangles.Add(bottomCenter);
                triangles.Add(bottomCenter + i + 1);
                triangles.Add(bottomCenter + i);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Создать материал для пика по умолчанию (URP-совместимый)
        /// </summary>
        private Material CreateDefaultPeakMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[WorldGenerator] Не удалось найти URP/Lit шейдер! Проверьте установку URP.");
                shader = Shader.Find("Standard"); // Fallback
            }
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.4f, 0.35f, 0.3f, 1f)); // Коричнево-серый (скалы)
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0.2f);
            return mat;
        }

        /// <summary>
        /// Сгенерировать облачный слой (использует новую систему CloudSystem)
        /// </summary>
        private void GenerateCloudLayer()
        {
            // CloudSystem сгенерирует облака автоматически в своём Start()
        }

        /// <summary>
        /// Сгенерировать плотный облачный слой (legacy метод)
        /// </summary>
        private void GenerateCloudLayerLegacy()
        {
            // Покрываем весь мир облаками
            int cloudCount = Mathf.RoundToInt(
                (Mathf.PI * settings.worldRadius * settings.worldRadius) /
                (settings.cloudSize * settings.cloudSize) *
                settings.cloudDensity
            );

            for (int i = 0; i < cloudCount; i++)
            {
                Vector3 position = GenerateCloudPosition();
                GameObject cloud = CreateCloud(position);
                generatedClouds.Add(cloud);
            }
        }

        /// <summary>
        /// Сгенерировать позицию облака (равномерно по миру)
        /// </summary>
        private Vector3 GenerateCloudPosition()
        {
            // Позиция в пределах радиуса мира
            float angle = Random.value * Mathf.PI * 2;
            float radius = Mathf.Sqrt(Random.value) * settings.worldRadius;
            
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            
            // Высота в пределах облачного слоя
            float y = settings.cloudLayerHeight + 
                      Random.Range(0, settings.cloudLayerThickness);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Создать облако
        /// </summary>
        private GameObject CreateCloud(Vector3 position)
        {
            GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloud.name = "Cloud";
            cloud.transform.position = position;
            cloud.transform.SetParent(cloudsParent);
            
            // Вариативный размер
            float scale = settings.cloudSize * Random.Range(
                1f / settings.cloudSizeVariation, 
                settings.cloudSizeVariation
            );
            cloud.transform.localScale = new Vector3(scale, scale * 0.6f, scale);

            // Материал облака
            Material mat = settings.cloudMaterial != null 
                ? settings.cloudMaterial 
                : CreateDefaultCloudMaterial();
            cloud.GetComponent<Renderer>().material = mat;

            // Удаляем коллайдер (не нужен для облаков)
            DestroyImmediate(cloud.GetComponent<Collider>());

            return cloud;
        }

        /// <summary>
        /// Создать материал для облака по умолчанию (URP-совместимый)
        /// </summary>
        private Material CreateDefaultCloudMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[WorldGenerator] Не удалось найти URP/Unlit шейдер! Проверьте установку URP.");
                shader = Shader.Find("Standard"); // Fallback
            }
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.4f)); // Белый полупрозрачный
            mat.SetInt("_Surface", 1); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.SetFloat("_Smoothness", 0.0f);
            return mat;
        }

        /// <summary>
        /// Сгенерировать мелкие острова между пиками
        /// </summary>
        private void GenerateMinorIslands()
        {
            for (int i = 0; i < settings.minorIslandCount; i++)
            {
                Vector3 position = GenerateMinorIslandPosition();
                GameObject island = CreateMinorIsland(position);
                generatedMinorIslands.Add(island);
            }
        }

        /// <summary>
        /// Позиция для мелкого острова
        /// </summary>
        private Vector3 GenerateMinorIslandPosition()
        {
            float angle = Random.value * Mathf.PI * 2;
            float radius = Random.Range(settings.worldRadius * 0.3f, settings.worldRadius * 0.8f);
            
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = settings.cloudLayerHeight - 50f; // Чуть ниже основных пиков

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Создать мелкий остров
        /// </summary>
        private GameObject CreateMinorIsland(Vector3 position)
        {
            GameObject island = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            island.name = "MinorIsland";
            island.transform.position = position;
            island.transform.SetParent(minorIslandsParent);
            
            float scale = Random.Range(20f, 80f);
            island.transform.localScale = new Vector3(scale, Random.Range(30f, 100f), scale);

            Material mat = CreateDefaultPeakMaterial();
            island.GetComponent<Renderer>().material = mat;

            return island;
        }

        /// <summary>
        /// Очистить сгенерированные объекты
        /// </summary>
        private void ClearGeneratedObjects()
        {
            foreach (var peak in generatedPeaks)
            {
                if (peak.gameObject != null)
                    DestroyImmediate(peak.gameObject);
            }
            generatedPeaks.Clear();

            foreach (var cloud in generatedClouds)
            {
                if (cloud != null)
                    DestroyImmediate(cloud);
            }
            generatedClouds.Clear();

            foreach (var island in generatedMinorIslands)
            {
                if (island != null)
                    DestroyImmediate(island);
            }
            generatedMinorIslands.Clear();
        }

        /// <summary>
        /// Пересгенерировать мир (для редактора)
        /// </summary>
        [ContextMenu("Regenerate World")]
        public void RegenerateWorld()
        {
            // Пытаемся загрузить настройки, если не назначены
            if (settings == null)
            {
                settings = Resources.Load<WorldGenerationSettings>("WorldGenerationSettings");
                
                if (settings == null)
                {
                    Debug.LogWarning("[WorldGenerator] WorldGenerationSettings не найден. Создаются настройки по умолчанию.");
                    settings = ScriptableObject.CreateInstance<WorldGenerationSettings>();
                }
            }
            
            GenerateWorld();
        }

        /// <summary>
        /// Получить информацию о пике по индексу
        /// </summary>
        public PeakInfo GetPeakInfo(int index)
        {
            if (index >= 0 && index < generatedPeaks.Count)
                return generatedPeaks[index];
            return null;
        }

        /// <summary>
        /// Получить все пики
        /// </summary>
        public List<PeakInfo> GetAllPeaks()
        {
            return new List<PeakInfo>(generatedPeaks);
        }
    }
}
