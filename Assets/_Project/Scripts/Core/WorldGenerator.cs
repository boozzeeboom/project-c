using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Процедурный генератор мира Project C
    /// Генерирует облачные острова с помощью шума Перлина
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Настройки генерации")]
        [SerializeField] private WorldGenerationSettings settings;
        
        [Header("Ссылки")]
        [SerializeField] private Transform islandsParent;
        [SerializeField] private Transform cloudsParent;

        [Header("Рандомизация")]
        [SerializeField] private int seed = 0;
        [SerializeField] private bool randomizeSeed = true;

        private List<GameObject> generatedIslands = new List<GameObject>();
        private List<GameObject> generatedClouds = new List<GameObject>();

        private void Start()
        {
            if (randomizeSeed)
            {
                seed = Random.Range(int.MinValue, int.MaxValue);
            }
            
            Random.InitState(seed);
            Debug.Log($"[WorldGenerator] Seed: {seed}");

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

            GenerateIslands();
            GenerateCloudLayer();
            
            Debug.Log($"[WorldGenerator] Мир сгенерирован: {generatedIslands.Count} островов, {generatedClouds.Count} облаков");
        }

        /// <summary>
        /// Сгенерировать острова
        /// </summary>
        private void GenerateIslands()
        {
            for (int i = 0; i < settings.islandCount; i++)
            {
                Vector3 position = GenerateIslandPosition(i);
                GameObject island = CreateIsland(position, i);
                generatedIslands.Add(island);
            }
        }

        /// <summary>
        /// Сгенерировать позицию для острова
        /// </summary>
        private Vector3 GenerateIslandPosition(int index)
        {
            float angle = (index / (float)settings.islandCount) * Mathf.PI * 2;
            float radius = settings.minDistanceBetweenIslands * 0.5f + index * 20f;
            
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = 0;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Создать остров
        /// </summary>
        private GameObject CreateIsland(Vector3 position, int index)
        {
            GameObject island = new GameObject($"Island_{index}");
            island.transform.position = position;
            island.transform.SetParent(islandsParent);

            // Добавляем MeshFilter и MeshRenderer
            MeshFilter meshFilter = island.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = island.AddComponent<MeshRenderer>();

            // Генерируем меш острова
            Mesh islandMesh = GenerateIslandMesh();
            meshFilter.mesh = islandMesh;

            // Добавляем материал (будет заменён на реальный)
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.3f, 0.6f, 0.3f, 1f); // Зелёный цвет
            meshRenderer.material = mat;

            // Добавляем коллайдер
            MeshCollider collider = island.AddComponent<MeshCollider>();
            collider.sharedMesh = islandMesh;

            return island;
        }

        /// <summary>
        /// Сгенерировать меш острова
        /// </summary>
        private Mesh GenerateIslandMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "IslandMesh";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // Центр острова
            vertices.Add(new Vector3(0, settings.baseHeight, 0));

            // Вершины по кругу
            float angleStep = (Mathf.PI * 2) / settings.islandVertices;
            for (int i = 0; i < settings.islandVertices; i++)
            {
                float angle = i * angleStep;
                float radius = settings.islandSize * (0.5f + Random.Range(-0.1f, 0.1f));
                
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = Mathf.PerlinNoise(
                    x * settings.shapeNoiseScale, 
                    z * settings.shapeNoiseScale
                ) * (settings.maxHeight - settings.baseHeight);

                vertices.Add(new Vector3(x, y, z));
            }

            // Треугольники от центра к вершинам
            for (int i = 1; i < settings.islandVertices; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }
            triangles.Add(0);
            triangles.Add(settings.islandVertices);
            triangles.Add(1);

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Сгенерировать слой облаков
        /// </summary>
        private void GenerateCloudLayer()
        {
            int cloudCount = Mathf.RoundToInt(settings.islandCount * 3f * settings.cloudDensity);

            for (int i = 0; i < cloudCount; i++)
            {
                Vector3 position = GenerateCloudPosition();
                GameObject cloud = CreateCloud(position);
                generatedClouds.Add(cloud);
            }
        }

        /// <summary>
        /// Сгенерировать позицию облака
        /// </summary>
        private Vector3 GenerateCloudPosition()
        {
            float x = Random.Range(-settings.minDistanceBetweenIslands, settings.minDistanceBetweenIslands);
            float y = settings.cloudLayerHeight + Random.Range(-20f, 20f);
            float z = Random.Range(-settings.minDistanceBetweenIslands, settings.minDistanceBetweenIslands);

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
            cloud.transform.localScale = Vector3.one * settings.cloudSize * Random.Range(0.8f, 1.5f);

            // Материал облака
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 1f, 1f, 0.3f); // Белый полупрозрачный
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            cloud.GetComponent<Renderer>().material = mat;

            return cloud;
        }

        /// <summary>
        /// Очистить сгенерированные объекты
        /// </summary>
        private void ClearGeneratedObjects()
        {
            foreach (var island in generatedIslands)
            {
                if (island != null)
                    DestroyImmediate(island);
            }
            generatedIslands.Clear();

            foreach (var cloud in generatedClouds)
            {
                if (cloud != null)
                    DestroyImmediate(cloud);
            }
            generatedClouds.Clear();
        }

        /// <summary>
        /// Пересгенерировать мир (для редактора)
        /// </summary>
        [ContextMenu("Regenerate World")]
        public void RegenerateWorld()
        {
            GenerateWorld();
        }
    }
}
