using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.World.Core;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// ChunkLoader управляет загрузкой и выгрузкой чанков мира.
    /// Загружает чанки асинхронно через coroutine, управляет fade-in/fade-out
    /// для облаков и отслеживает статус загрузки каждого чанка.
    /// </summary>
    public class ChunkLoader : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Менеджер чанков мира")]
        [SerializeField]
        private WorldChunkManager chunkManager;

        [Tooltip("Генератор чанков")]
        [SerializeField]
        private ProceduralChunkGenerator chunkGenerator;

        [Tooltip("Transform-контейнер для всех загруженных чанков")]
        [SerializeField]
        private Transform chunksParentTransform;

        [Header("Generation Settings")]
        [Tooltip("Глобальный seed мира для генерации чанков")]
        [SerializeField]
        private int globalSeed = 0;

        [Header("Fade Settings")]
        [Tooltip("Время fade-in/fade-out в секундах")]
        [SerializeField, Range(0.5f, 3f)]
        private float fadeDuration = 1.5f;

        /// <summary>
        /// Событие вызывается когда чанк полностью загружен.
        /// </summary>
        public System.Action<ChunkId> OnChunkLoaded;

        /// <summary>
        /// Событие вызывается когда чанк полностью выгружен.
        /// </summary>
        public System.Action<ChunkId> OnChunkUnloaded;

        /// <summary>
        /// Хранит root-объект каждого загруженного чанка.
        /// </summary>
        private readonly Dictionary<ChunkId, GameObject> loadedChunks = new Dictionary<ChunkId, GameObject>();
        
        [Header("Debug")]
        [Tooltip("Show debug logs for chunk loading")]
        [SerializeField] private bool _showDebugLogs = true;

        /// <summary>
        /// Хранит состояние fade-out для чанков (ChunkId → оставшееся время fade).
        /// </summary>
        private readonly Dictionary<ChunkId, float> chunkFadeTimes = new Dictionary<ChunkId, float>();

        /// <summary>
        /// Coroutine для fade-out каждого чанка.
        /// </summary>
        private readonly Dictionary<ChunkId, Coroutine> fadeOutCoroutines = new Dictionary<ChunkId, Coroutine>();

        private void Awake()
        {
            // Автоматический поиск если ссылки не назначены
            if (chunkManager == null)
            {
                chunkManager = FindAnyObjectByType<WorldChunkManager>();
                if (chunkManager == null)
                {
                    Debug.LogError("[ChunkLoader] WorldChunkManager не найден на сцене!");
                }
            }

            if (chunkGenerator == null)
            {
                chunkGenerator = GetComponent<ProceduralChunkGenerator>();
                if (chunkGenerator == null)
                {
                    chunkGenerator = FindAnyObjectByType<ProceduralChunkGenerator>();
                }
                if (chunkGenerator == null)
                {
                    Debug.LogError("[ChunkLoader] ProceduralChunkGenerator не найден!");
                }
            }

        }

        /// <summary>
        /// Загрузить чанк.
        /// I5-001 FIX: Creates chunk on-demand if not in WorldChunkManager registry.
        /// </summary>
        /// <param name="chunkId">ID чанка для загрузки.</param>
        public void LoadChunk(ChunkId chunkId)
        {
            if (loadedChunks.ContainsKey(chunkId))
            {
                if (_showDebugLogs)
                    Debug.Log($"[ChunkLoader] Chunk {chunkId} already loaded, skipping.");
                return;
            }
            
            if (chunkManager == null)
            {
                Debug.LogError("[ChunkLoader] WorldChunkManager not assigned!");
                return;
            }
            
            // I5-001 FIX: Try to get from registry, create on-demand if not found
            WorldChunk chunk = chunkManager.GetChunk(chunkId);
            if (chunk == null)
            {
                // I5-001 FIX: Create basic chunk data on-demand for procedural world
                if (_showDebugLogs)
                    Debug.Log($"[ChunkLoader] Creating on-demand chunk {chunkId}");
                
                // Create basic chunk with empty peaks/farms
                chunk = new WorldChunk
                {
                    Id = chunkId,
                    State = ChunkState.Unloaded,
                    Peaks = new List<PeakData>(),
                    Farms = new List<FarmData>(),
                    CloudSeed = chunkManager.GenerateCloudSeed(chunkId),
                    WorldBounds = new Bounds(
                        new Vector3(chunkId.GridX * 2000 + 1000, 0, chunkId.GridZ * 2000 + 1000),
                        new Vector3(2000, 1000, 2000)
                    )
                };
            }

            // Создаём root-объект для чанка
            GameObject chunkRoot = CreateChunkRoot(chunkId);
            loadedChunks[chunkId] = chunkRoot;

            // Запускаем асинхронную генерацию
            StartCoroutine(LoadChunkCoroutine(chunkId, chunk, chunkRoot));
        }

        /// <summary>
        /// Выгрузить чанк по его ChunkId.
        /// Запускает fade-out для облаков, затем уничтожает объект чанка.
        /// </summary>
        /// <param name="chunkId">Идентификатор чанка для выгрузки.</param>
        public void UnloadChunk(ChunkId chunkId)
        {
            if (!loadedChunks.ContainsKey(chunkId))
            {
                Debug.LogWarning($"[ChunkLoader] Чанк {chunkId} не загружен, невозможно выгрузить.");
                return;
            }

            // Если уже идёт fade-out — игнорируем повторный вызов
            if (chunkFadeTimes.ContainsKey(chunkId))
            {
                Debug.LogWarning($"[ChunkLoader] Чанк {chunkId} уже в процессе fade-out.");
                return;
            }

            GameObject chunkRoot = loadedChunks[chunkId];
            loadedChunks.Remove(chunkId);

            // Обновляем состояние чанка
            WorldChunk chunk = chunkManager?.GetChunk(chunkId);
            if (chunk != null)
            {
                chunk.State = ChunkState.Unloading;
            }

            // Запускаем fade-out
            fadeOutCoroutines[chunkId] = StartCoroutine(FadeOutCoroutine(chunkId, chunkRoot));
        }

        /// <summary>
        /// Создать root-объект для чанка.
        /// </summary>
        private GameObject CreateChunkRoot(ChunkId chunkId)
        {
            Transform parent = chunksParentTransform;
            if (parent == null)
            {
                // Создаём родительский контейнер автоматически
                GameObject parentObj = new GameObject("ChunksContainer");
                parent = parentObj.transform;
                chunksParentTransform = parent;
            }

            GameObject chunkRoot = new GameObject($"Chunk_{chunkId}");
            chunkRoot.transform.SetParent(parent);
            chunkRoot.transform.position = Vector3.zero;

            return chunkRoot;
        }

        /// <summary>
        /// Coroutine для асинхронной загрузки чанка.
        /// </summary>
        private IEnumerator LoadChunkCoroutine(ChunkId chunkId, WorldChunk chunk, GameObject chunkRoot)
        {
            chunk.State = ChunkState.Loading;

            // Запускаем генерацию через ProceduralChunkGenerator
            IEnumerator generationCoroutine = chunkGenerator.GenerateChunkAsync(chunk, chunkRoot.transform, globalSeed);

            // Ждём завершения генерации
            yield return generationCoroutine;

            // Fade-in для облаков
            yield return FadeInClouds(chunkRoot);

            chunk.State = ChunkState.Loaded;

            // Вызываем событие
            OnChunkLoaded?.Invoke(chunkId);
        }

        /// <summary>
        /// Fade-in для всех renderer'ов в чанке (плавное появление).
        /// </summary>
        private IEnumerator FadeInClouds(GameObject chunkRoot)
        {
            // Находим все renderer'ы в чанке
            var renderers = chunkRoot.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                yield break;
            }

            float elapsed = 0f;

            // Инициализируем alpha = 0
            foreach (var renderer in renderers)
            {
                if (renderer.material != null)
                {
                    Color color = renderer.material.color;
                    color.a = 0f;
                    renderer.material.color = color;
                }
            }

            // Плавное появление
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);

                foreach (var renderer in renderers)
                {
                    if (renderer != null && renderer.gameObject != null)
                    {
                        try
                        {
                            Color color = renderer.material.color;
                            color.a = alpha;
                            renderer.material.color = color;
                        }
                        catch (MissingReferenceException)
                        {
                            // Renderer уничтожен, пропускаем
                        }
                    }
                }

                yield return null;
            }

            // Убеждаемся что alpha = 1
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.gameObject != null)
                {
                    try
                    {
                        Color color = renderer.material.color;
                        color.a = 1f;
                        renderer.material.color = color;
                    }
                    catch (MissingReferenceException)
                    {
                        // Renderer уничтожен, пропускаем
                    }
                }
            }
        }

        /// <summary>
        /// Coroutine для fade-out чанка перед уничтожением.
        /// </summary>
        private IEnumerator FadeOutCoroutine(ChunkId chunkId, GameObject chunkRoot)
        {
            chunkFadeTimes[chunkId] = fadeDuration;

            // Находим все renderer'ы в чанке
            var renderers = chunkRoot.GetComponentsInChildren<Renderer>(true);

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                chunkFadeTimes[chunkId] = fadeDuration - elapsed;

                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        Color color = renderer.material.color;
                        color.a = alpha;
                        renderer.material.color = color;
                    }
                }

                yield return null;
            }

            // Убираем из списка fade
            chunkFadeTimes.Remove(chunkId);
            fadeOutCoroutines.Remove(chunkId);

            // Уничтожаем объект чанка
            Destroy(chunkRoot);

            // Вызываем событие
            OnChunkUnloaded?.Invoke(chunkId);
        }

        /// <summary>
        /// Отменить fade-out для чанка (если загрузка началась во время выгрузки).
        /// </summary>
        private void CancelFadeOut(ChunkId chunkId)
        {
            if (fadeOutCoroutines.TryGetValue(chunkId, out Coroutine coroutine))
            {
                StopCoroutine(coroutine);
                fadeOutCoroutines.Remove(chunkId);
            }

            chunkFadeTimes.Remove(chunkId);
        }

        /// <summary>
        /// Проверить загружен ли чанк.
        /// </summary>
        public bool IsChunkLoaded(ChunkId chunkId)
        {
            return loadedChunks.ContainsKey(chunkId);
        }

        /// <summary>
        /// Получить количество загруженных чанков.
        /// </summary>
        public int LoadedChunkCount => loadedChunks.Count;

        /// <summary>
        /// Получить все загруженные чанки.
        /// </summary>
        public IReadOnlyCollection<ChunkId> GetLoadedChunkIds()
        {
            return loadedChunks.Keys;
        }

        /// <summary>
        /// Выгрузить все загруженные чанки.
        /// </summary>
        public void UnloadAllChunks()
        {
            var chunkIds = new List<ChunkId>(loadedChunks.Keys);
            foreach (var chunkId in chunkIds)
            {
                UnloadChunk(chunkId);
            }
        }

        private void Update()
        {
            // Обновляем таймеры fade-out (для внешнего мониторинга)
            var keysToRemove = new List<ChunkId>();
            foreach (var kvp in chunkFadeTimes)
            {
                if (kvp.Value <= 0f)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                chunkFadeTimes.Remove(key);
            }
        }

        private void OnDestroy()
        {
            // Останавливаем все fade-out coroutine
            foreach (var coroutine in fadeOutCoroutines.Values)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }
            fadeOutCoroutines.Clear();
            chunkFadeTimes.Clear();
        }
    }
}
