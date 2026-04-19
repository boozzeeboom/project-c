using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.World.Core;
using ProjectC.World.Generation;
using ProjectC.World.Clouds;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// Процедурный генератор чанков — по ChunkId генерирует все объекты чанка:
    /// 1. Горы (MountainMeshGenerator для каждого пика)
    /// 2. Облака (детерминированно из CloudSeed + chunkSeed)
    /// 3. Фермы (инстанцирование префабов или placeholder объекты)
    ///
    /// Детерминизм: одинаковый Seed -> одинаковый результат на любом запуске.
    /// Асинхронность: генерация через coroutine (yield return для распределения нагрузки).
    /// </summary>
    public class ProceduralChunkGenerator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Префаб фермы для инстанцирования (опционально)")]
        [SerializeField]
        private GameObject farmPrefab;

        [Tooltip("Материал для placeholder ферм")]
        [SerializeField]
        private Material farmPlaceholderMaterial;

        [Tooltip("Материал для гор (MeshRenderer)")]
        [SerializeField]
        private Material mountainMaterial;

        [Header("Generation Settings")]
        [Tooltip("Глобальный seed мира (передаётся при вызове GenerateChunkAsync)")]
        [SerializeField]
#pragma warning disable 0414
        private int globalSeed = 0;
#pragma warning restore 0414

        [Tooltip("LOD уровень генерации (0 = максимальное качество)")]
        [SerializeField, Range(0, 2)]
        private int lodLevel = 0;

        /// <summary>
        /// Сгенерировать детерминированный seed для чанка.
        /// </summary>
        /// <param name="chunkId">Идентификатор чанка.</param>
        /// <param name="globalSeed">Глобальный seed мира.</param>
        /// <returns>Детерминированный chunkSeed.</returns>
        public int GenerateChunkSeed(ChunkId chunkId, int globalSeed)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + chunkId.GridX;
                hash = hash * 31 + chunkId.GridZ;
                hash = hash * 31 + globalSeed;
                return hash;
            }
        }

        /// <summary>
        /// Асинхронно сгенерировать все объекты чанка.
        /// Распределяет нагрузку через yield return null между этапами генерации.
        /// </summary>
        /// <param name="chunk">WorldChunk с данными для генерации.</param>
        /// <param name="parentTransform">Родительский Transform для сгенерированных объектов.</param>
        /// <param name="globalSeed">Глобальный seed мира.</param>
        /// <returns>IEnumerator для coroutine.</returns>
        public IEnumerator GenerateChunkAsync(WorldChunk chunk, Transform parentTransform, int globalSeed)
        {
            if (chunk == null)
            {
                Debug.LogError("[ProceduralChunkGenerator] chunk is null");
                yield break;
            }

            if (parentTransform == null)
            {
                Debug.LogError("[ProceduralChunkGenerator] parentTransform is null");
                yield break;
            }

            int chunkSeed = GenerateChunkSeed(chunk.Id, globalSeed);

            chunk.State = ChunkState.Loading;

            // Этап 1: Генерация гор
            yield return GenerateMountainsAsync(chunk, parentTransform, chunkSeed);

            // Этап 2: Генерация облаков
            yield return GenerateCloudsAsync(chunk, parentTransform, chunkSeed);

            // Этап 3: Генерация ферм
            yield return GenerateFarmsAsync(chunk, parentTransform, chunkSeed);

            chunk.State = ChunkState.Loaded;
        }

        /// <summary>
        /// Сгенерировать все горы в чанке.
        /// Для каждого пика создаёт меш через MountainMeshGenerator.
        /// </summary>
        private IEnumerator GenerateMountainsAsync(WorldChunk chunk, Transform parentTransform, int chunkSeed)
        {
            if (chunk.Peaks == null || chunk.Peaks.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < chunk.Peaks.Count; i++)
            {
                PeakData peak = chunk.Peaks[i];
                if (peak == null) continue;

                GenerateMountainForPeak(peak, chunkSeed + i, parentTransform);

                // Распределение нагрузки между горами
                if (i < chunk.Peaks.Count - 1)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Сгенерировать одну гору для пика.
        /// </summary>
        private void GenerateMountainForPeak(PeakData peak, int seed, Transform parentTransform)
        {
            // Вычисляем параметры меша
            float meshHeight = MountainMeshGenerator.CalculateMeshHeight(peak);
            float baseRadius = MountainMeshGenerator.CalculateBaseRadius(peak, meshHeight);

            // Определяем LOD параметры
            int segments = lodLevel switch
            {
                0 => 64,
                1 => 32,
                2 => 16,
                _ => 64
            };
            int rings = lodLevel switch
            {
                0 => 24,
                1 => 12,
                2 => 8,
                _ => 24
            };

            // Создаём профиль горы
            MountainProfile profile = MountainProfile.CreatePreset(peak.shapeType);

            // Переопределяем crater если есть
            profile.hasCrater = peak.hasCrater;

            // Генерируем меш
            Mesh mountainMesh = MountainMeshGenerator.GenerateMountainMesh(
                profile,
                meshHeight,
                baseRadius,
                segments,
                rings,
                seed: seed
            );

            // Создаём GameObject для горы
            GameObject mountainObj = new GameObject($"Mountain_{peak.peakId}");
            mountainObj.transform.SetParent(parentTransform);
            mountainObj.transform.position = peak.worldPosition;

            // Добавляем MeshFilter и MeshRenderer
            MeshFilter meshFilter = mountainObj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mountainMesh;

            MeshRenderer meshRenderer = mountainObj.AddComponent<MeshRenderer>();
            if (mountainMaterial != null)
            {
                meshRenderer.material = mountainMaterial;
            }

            // Добавляем MeshCollider для физических взаимодействий
            MeshCollider meshCollider = mountainObj.AddComponent<MeshCollider>();
            Mesh colliderMesh = MountainMeshGenerator.GenerateColliderMesh(profile, meshHeight, baseRadius);
            meshCollider.sharedMesh = colliderMesh;

            Debug.Log($"[ProceduralChunkGenerator] Создана гора {peak.peakId} " +
                      $"(height={meshHeight:F0}, radius={baseRadius:F0}, seed={seed})");
        }

        /// <summary>
        /// Сгенерировать облака в чанке детерминированно из CloudSeed + chunkSeed.
        /// Использует CumulonimbusCloud для создания грозовых столбов.
        /// </summary>
        private IEnumerator GenerateCloudsAsync(WorldChunk chunk, Transform parentTransform, int chunkSeed)
        {
            int cloudSeed = chunk.CloudSeed ^ chunkSeed;
            System.Random rng = new System.Random(cloudSeed);

            // Детерминированное количество облаков: 1-3 на чанк
            int cloudCount = rng.Next(1, 4);

            // Создаём контейнер для облаков чанка
            GameObject cloudContainer = new GameObject($"Clouds_{chunk.Id}");
            cloudContainer.transform.SetParent(parentTransform);
            cloudContainer.transform.position = Vector3.zero;

            for (int i = 0; i < cloudCount; i++)
            {
                // Детерминированная позиция в пределах чанка
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float radius = (float)(rng.NextDouble() * 8000f); // Радиус размещения внутри чанка

                float xPos = Mathf.Cos(angle) * radius;
                float zPos = Mathf.Sin(angle) * radius;

                // Смещаем к центру чанка
                Vector3 chunkCenter = chunk.WorldBounds.center;
                xPos += chunkCenter.x;
                zPos += chunkCenter.z;

                // Создаём облако
                GameObject cloudObj = new GameObject($"Cumulonimbus_{chunk.Id}_{i}");
                cloudObj.transform.SetParent(cloudContainer.transform);

                CumulonimbusCloud cloud = cloudObj.AddComponent<CumulonimbusCloud>();
                cloud.Initialize(
                    xPos: xPos,
                    zPos: zPos,
                    baseRadiusOverride: 600f + (float)rng.NextDouble() * 400f,
                    topRadiusOverride: 1200f + (float)rng.NextDouble() * 800f
                );

                // Распределение нагрузки
                if (i < cloudCount - 1)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Инстанцировать фермы в чанке.
        /// Если есть farmPrefab — использует его, иначе создаёт placeholder объект.
        /// </summary>
        private IEnumerator GenerateFarmsAsync(WorldChunk chunk, Transform parentTransform, int chunkSeed)
        {
            if (chunk.Farms == null || chunk.Farms.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < chunk.Farms.Count; i++)
            {
                FarmData farm = chunk.Farms[i];
                if (farm == null) continue;

                GenerateFarmForObject(farm, chunkSeed + i + 10000, parentTransform);

                // Распределение нагрузки между фермами
                if (i < chunk.Farms.Count - 1)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Создать объект фермы из префаба или placeholder.
        /// </summary>
        private void GenerateFarmForObject(FarmData farm, int seed, Transform parentTransform)
        {
            GameObject farmObj;

            if (farmPrefab != null)
            {
                // Инстанцируем префаб
                farmObj = Instantiate(farmPrefab, parentTransform);
                farmObj.name = $"Farm_{farm.farmId}";
            }
            else
            {
                // Создаём placeholder объект
                farmObj = CreateFarmPlaceholder(farm, seed);
            }

            // Позиционируем ферму
            farmObj.transform.position = farm.worldPosition;

            // Настраиваем платформу фермы (размер антигравийной плиты)
            farmObj.transform.localScale = new Vector3(
                farm.platformSizeX / 40f,  // Нормализация к стандартному размеру
                1f,
                farm.platformSizeZ / 20f
            );

            Debug.Log($"[ProceduralChunkGenerator] Создана ферма {farm.farmId} " +
                      $"(pos={farm.worldPosition}, production={farm.productionType})");
        }

        /// <summary>
        /// Создать placeholder объект для фермы когда нет префаба.
        /// </summary>
        private GameObject CreateFarmPlaceholder(FarmData farm, int seed)
        {
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = $"Farm_{farm.farmId}_Placeholder";

            // Базовый размер платформы
            placeholder.transform.localScale = new Vector3(
                farm.platformSizeX,
                2f,
                farm.platformSizeZ
            );

            // Назначаем материал если есть
            Renderer renderer = placeholder.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (farmPlaceholderMaterial != null)
                {
                    renderer.material = farmPlaceholderMaterial;
                }
                else
                {
                    // Дефолтный цвет с glow эффектом
                    renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    {
                        color = farm.platformGlowColor
                    };
                }
            }

            // Убираем collider если не нужен
            Collider collider = placeholder.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            return placeholder;
        }

        /// <summary>
        /// Очистить все объекты сгенерированные для чанка.
        /// </summary>
        public void ClearChunk(WorldChunk chunk, Transform parentTransform)
        {
            if (parentTransform == null || chunk == null) return;

            // Находим и уничтожаем дочерние объекты чанка
            string[] chunkTags = {
                $"Mountain_{chunk.Id}",
                $"Clouds_{chunk.Id}",
                $"Farm_{chunk.Id}"
            };

            for (int i = parentTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = parentTransform.GetChild(i);
                foreach (string tag in chunkTags)
                {
                    if (child.name.Contains(chunk.Id.ToString()))
                    {
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }

            chunk.State = ChunkState.Unloaded;
            Debug.Log($"[ProceduralChunkGenerator] Очищен {chunk.Id}");
        }
    }
}
