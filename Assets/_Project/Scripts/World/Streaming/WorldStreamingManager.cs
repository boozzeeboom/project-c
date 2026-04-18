using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.World.Core;
using ProjectC.World.Streaming;

namespace ProjectC.World
{
    /// <summary>
    /// Главный менеджер World Streaming системы.
    /// Координирует WorldChunkManager, ChunkLoader, FloatingOriginMP
    /// для обеспечения бесшовного стриминга мира в MMO-стиле.
    /// 
    /// Использование:
    /// 1. Добавить на Empty GameObject в сцене
    /// 2. Назначить ссылки на компоненты (или оставить auto-find)
    /// 3. В Play Mode вызывать LoadChunksAroundPlayer() с позицией игрока
    /// 
    /// Для мультиплеера: управление происходит на сервере,
    /// клиенты получают команды через RPC.
    /// </summary>
    public class WorldStreamingManager : MonoBehaviour
    {
        #region Singleton
        
        private static WorldStreamingManager _instance;
        public static WorldStreamingManager Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("References")]
        [Tooltip("WorldData ScriptableObject с данными о массивах, пиках, фермах")]
        [SerializeField] private WorldData worldData;
        
        [Tooltip("WorldChunkManager - реестр чанков")]
        [SerializeField] private WorldChunkManager chunkManager;
        
        [Tooltip("ProceduralChunkGenerator - генерация содержимого чанков")]
        [SerializeField] private ProceduralChunkGenerator chunkGenerator;
        
        [Tooltip("ChunkLoader - загрузка/выгрузка чанков")]
        [SerializeField] private ChunkLoader chunkLoader;
        
        [Tooltip("FloatingOriginMP - для больших координат")]
        [SerializeField] private FloatingOriginMP floatingOrigin;
        
        [Header("Streaming Settings")]
        [Tooltip("Радиус загрузки чанков вокруг игрока (в чанках)")]
        [SerializeField, Range(1, 5)]
        private int loadRadius = 2;
        
        [Tooltip("Радиус выгрузки чанков (в чанках) - за пределами этого радиуса чанки выгружаются")]
        [SerializeField, Range(2, 10)]
        private int unloadRadius = 3;
        
        [Tooltip("Интервал проверки загрузки/выгрузки чанков (секунды)")]
        [SerializeField, Range(0.1f, 2f)]
        private float updateInterval = 0.5f;
        
        [Tooltip("Глобальный seed мира - влияет на генерацию облаков и форму гор")]
        [SerializeField]
        #pragma warning disable 0414
        private int globalSeed = 42;
        #pragma warning restore 0414
        
        [Header("Debug")]
        [Tooltip("Показывать debug информацию на экране")]
        [SerializeField] private bool showDebugHUD = false;
        
        [Header("Preload Settings")]
        [Tooltip("Preload: количество слоев соседних чанков для загрузки заранее")]
        [SerializeField, Range(0, 3)]
        private int preloadLayers = 1;
        
        [Tooltip("Preload: задержка после входа в новый чанк перед началом preloading (секунды)")]
        [SerializeField, Range(0f, 5f)]
        private float preloadDelay = 1f;
        
        [Tooltip("Preload: интервал между загрузкой каждого preloaded чанка (секунды)")]
        [SerializeField, Range(0.1f, 2f)]
        private float preloadChunkInterval = 0.3f;
        
        [Tooltip("Memory Budget: максимальное количество загруженных чанков (0 = без ограничений)")]
        [SerializeField, Range(0, 50)]
        private int maxLoadedChunks = 0;
        
        #endregion
        
        #region Private State
        
        private HashSet<ChunkId> _loadedChunks = new HashSet<ChunkId>();
        private ChunkId _currentCenterChunk;
        private float _lastUpdateTime = 0f;
        private bool _initialized = false;
        
        // Preload system
        private ChunkId _lastPreloadChunk;
        private float _lastPreloadTime = 0f;
        private List<ChunkId> _preloadQueue = new List<ChunkId>();
        private float _lastPreloadChunkTime = 0f;
        private bool _preloadInProgress = false;
        
        // Memory budget tracking
        private int _peakLoadedChunks = 0;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[WorldStreamingManager] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            
            // Auto-find компоненты если не назначены
            AutoFindComponents();
            
            // FIX I2-001: Подписка на ChunkLoader events
            SubscribeToChunkLoaderEvents();
        }
        
        /// <summary>
        /// FIX I2-001: Подписаться на события ChunkLoader для получения обратной связи.
        /// </summary>
        private void SubscribeToChunkLoaderEvents()
        {
            if (chunkLoader != null)
            {
                chunkLoader.OnChunkLoaded += OnChunkLoadedHandler;
                chunkLoader.OnChunkUnloaded += OnChunkUnloadedHandler;
                Debug.Log("[WorldStreamingManager] Subscribed to ChunkLoader events");
            }
        }
        
        /// <summary>
        /// FIX I2-001: Обработчик события загрузки чанка.
        /// Вызывается ChunkLoader после завершения загрузки чанка.
        /// </summary>
        private void OnChunkLoadedHandler(ChunkId chunkId)
        {
            Debug.Log($"[WorldStreamingManager] Chunk loaded: {chunkId.GridX},{chunkId.GridZ}");
            
            // Обновляем пиковую статистику
            if (_loadedChunks.Count > _peakLoadedChunks)
            {
                _peakLoadedChunks = _loadedChunks.Count;
            }
        }
        
        /// <summary>
        /// FIX I2-001: Обработчик события выгрузки чанка.
        /// Вызывается ChunkLoader после завершения выгрузки чанка.
        /// </summary>
        private void OnChunkUnloadedHandler(ChunkId chunkId)
        {
            Debug.Log($"[WorldStreamingManager] Chunk unloaded: {chunkId.GridX},{chunkId.GridZ}");
        }
        
        private void Start()
        {
            if (!ValidateSetup())
            {
                Debug.LogError("[WorldStreamingManager] Validation failed. Streaming disabled.");
                enabled = false;
                return;
            }
            
            InitializeStreaming();
        }
        
        private void Update()
        {
            if (!_initialized) return;
            
            // Периодическая проверка загрузки/выгрузки
            if (Time.time - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.time;
                UpdateStreaming();
            }
        }
        
        private void OnDestroy()
        {
            // FIX I2-001: Отписываемся от событий ChunkLoader
            UnsubscribeFromChunkLoaderEvents();
            
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        /// <summary>
        /// FIX I2-001: Отписаться от событий ChunkLoader при уничтожении.
        /// </summary>
        private void UnsubscribeFromChunkLoaderEvents()
        {
            if (chunkLoader != null)
            {
                chunkLoader.OnChunkLoaded -= OnChunkLoadedHandler;
                chunkLoader.OnChunkUnloaded -= OnChunkUnloadedHandler;
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Загрузить чанки вокруг позиции игрока.
        /// Вызывается автоматически из Update, но можно вызвать вручную после телепортации.
        /// </summary>
        /// <param name="playerPosition">Позиция игрока в мировых координатах</param>
        /// <param name="radius">Радиус загрузки (по умолчанию из настроек)</param>
        public void LoadChunksAroundPlayer(Vector3 playerPosition, int? radius = null)
        {
            if (chunkManager == null || chunkLoader == null)
            {
                Debug.LogError("[WorldStreamingManager] Cannot load chunks: managers not initialized.");
                return;
            }
            
            int effectiveRadius = radius ?? loadRadius;
            ChunkId centerChunk = chunkManager.GetChunkAtPosition(playerPosition);
            
            // Если центр не изменился - пропускаем
            if (centerChunk.Equals(_currentCenterChunk) && _loadedChunks.Count > 0)
            {
                return;
            }
            
            _currentCenterChunk = centerChunk;
            
            // Получаем чанки в радиусе
            List<ChunkId> chunksInRange = chunkManager.GetChunksInRadius(playerPosition, effectiveRadius);
            
            // Загружаем новые чанки
            foreach (var chunkId in chunksInRange)
            {
                if (!_loadedChunks.Contains(chunkId))
                {
                    chunkLoader.LoadChunk(chunkId);
                    _loadedChunks.Add(chunkId);
                    
                    if (showDebugHUD)
                    {
                        Debug.Log($"[WorldStreamingManager] Loading chunk {chunkId}");
                    }
                }
            }
            
            // Выгружаем дальние чанки
            int effectiveUnloadRadius = unloadRadius > effectiveRadius ? unloadRadius : effectiveRadius + 1;
            List<ChunkId> unloadChunks = chunkManager.GetChunksInRadius(playerPosition, effectiveUnloadRadius);
            
            HashSet<ChunkId> chunksToUnloadSet = new HashSet<ChunkId>(unloadChunks);
            
            var chunksToRemove = new List<ChunkId>();
            foreach (var loadedChunkId in _loadedChunks)
            {
                if (!chunksToUnloadSet.Contains(loadedChunkId))
                {
                    chunksToRemove.Add(loadedChunkId);
                }
            }
            
            foreach (var chunkId in chunksToRemove)
            {
                chunkLoader.UnloadChunk(chunkId);
                _loadedChunks.Remove(chunkId);
                
                if (showDebugHUD)
                {
                    Debug.Log($"[WorldStreamingManager] Unloading chunk {chunkId}");
                }
            }
        }
        
        /// <summary>
        /// Загрузить чанк по команде сервера (для multiplayer).
        /// Вызывается из PlayerChunkTracker.LoadChunkClientRpc().
        /// КЛИЕНТ НЕ ДОЛЖЕН вызывать этот метод самостоятельно!
        /// </summary>
        /// <param name="chunkId">ID чанка для загрузки</param>
        public void LoadChunkByServerCommand(ChunkId chunkId)
        {
            if (_loadedChunks.Contains(chunkId))
            {
                Debug.Log($"[WorldStreamingManager] Chunk {chunkId} already loaded, skipping.");
                return;
            }
            
            if (chunkLoader == null)
            {
                Debug.LogError("[WorldStreamingManager] ChunkLoader not initialized!");
                return;
            }
            
            Debug.Log($"[WorldStreamingManager] Loading chunk {chunkId} by server command");
            chunkLoader.LoadChunk(chunkId);
            _loadedChunks.Add(chunkId);
        }
        
        /// <summary>
        /// Выгрузить чанк по команде сервера (для multiplayer).
        /// Вызывается из PlayerChunkTracker.UnloadChunkClientRpc().
        /// КЛИЕНТ НЕ ДОЛЖЕН вызывать этот метод самостоятельно!
        /// </summary>
        /// <param name="chunkId">ID чанка для выгрузки</param>
        public void UnloadChunkByServerCommand(ChunkId chunkId)
        {
            if (!_loadedChunks.Contains(chunkId))
            {
                Debug.Log($"[WorldStreamingManager] Chunk {chunkId} not loaded, skipping unload.");
                return;
            }
            
            if (chunkLoader == null)
            {
                Debug.LogError("[WorldStreamingManager] ChunkLoader not initialized!");
                return;
            }
            
            Debug.Log($"[WorldStreamingManager] Unloading chunk {chunkId} by server command");
            chunkLoader.UnloadChunk(chunkId);
            _loadedChunks.Remove(chunkId);
        }
        
        /// <summary>
        /// Телепортировать игрока к пику.
        /// Комбинирует ResetOrigin (чтобы избежать floating point проблем)
        /// и загрузку чанков вокруг новой позиции.
        /// </summary>
        /// <param name="peakPosition">Позиция пика для телепортации</param>
        public void TeleportToPeak(Vector3 peakPosition)
        {
            if (floatingOrigin != null)
            {
                // Сдвигаем мир так чтобы камера осталась близко к origin
                floatingOrigin.ResetOrigin();
            }
            
            // Загружаем чанки вокруг новой позиции
            LoadChunksAroundPlayer(peakPosition, loadRadius);
            
            Debug.Log($"[WorldStreamingManager] Teleported to {peakPosition}");
        }
        
        /// <summary>
        /// Получить количество загруженных чанков.
        /// </summary>
        public int LoadedChunkCount => _loadedChunks.Count;
        
        /// <summary>
        /// Получить текущий центральный чанк.
        /// </summary>
        public ChunkId CurrentCenterChunk => _currentCenterChunk;
        
        /// <summary>
        /// Проверить загружен ли чанк.
        /// </summary>
        public bool IsChunkLoaded(ChunkId chunkId)
        {
            return _loadedChunks.Contains(chunkId);
        }
        
        /// <summary>
        /// Выгрузить все чанки (например, при смене сцены).
        /// </summary>
        public void UnloadAllChunks()
        {
            if (chunkLoader != null)
            {
                chunkLoader.UnloadAllChunks();
            }
            
            _loadedChunks.Clear();
            _currentCenterChunk = new ChunkId(0, 0);
            
            Debug.Log("[WorldStreamingManager] All chunks unloaded.");
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Автоматически найти необходимые компоненты на сцене.
        /// </summary>
        private void AutoFindComponents()
        {
            if (worldData == null)
            {
                // Пробуем найти в сцене
                worldData = FindAnyObjectByType<WorldData>();
                
                // Если не нашли — загружаем из Resources или Assets
                if (worldData == null)
                {
                    worldData = Resources.Load<WorldData>("Data/World/WorldData");
                }
                #if UNITY_EDITOR
                if (worldData == null)
                {
                    worldData = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldData>(
                        "Assets/_Project/Data/World/WorldData.asset");
                }
                #endif
            }
            
            if (chunkManager == null)
            {
                chunkManager = FindAnyObjectByType<WorldChunkManager>();
            }
            
            if (chunkGenerator == null)
            {
                chunkGenerator = FindAnyObjectByType<ProceduralChunkGenerator>();
                if (chunkGenerator == null)
                {
                    chunkGenerator = GetComponent<ProceduralChunkGenerator>();
                }
            }
            
            if (chunkLoader == null)
            {
                chunkLoader = FindAnyObjectByType<ChunkLoader>();
                if (chunkLoader == null)
                {
                    chunkLoader = GetComponent<ChunkLoader>();
                }
            }
            
            if (floatingOrigin == null)
            {
                floatingOrigin = FindAnyObjectByType<FloatingOriginMP>();
            }
        }
        
        /// <summary>
        /// Проверить корректность настроек.
        /// </summary>
        private bool ValidateSetup()
        {
            bool valid = true;
            
            if (worldData == null)
            {
                Debug.LogError("[WorldStreamingManager] WorldData not assigned!");
                valid = false;
            }
            
            if (chunkManager == null)
            {
                Debug.LogError("[WorldStreamingManager] WorldChunkManager not found!");
                valid = false;
            }
            
            if (chunkGenerator == null)
            {
                Debug.LogError("[WorldStreamingManager] ProceduralChunkGenerator not found!");
                valid = false;
            }
            
            if (chunkLoader == null)
            {
                Debug.LogError("[WorldStreamingManager] ChunkLoader not found!");
                valid = false;
            }
            
            // FloatingOrigin опционально (может не быть в одиночном режиме)
            if (floatingOrigin == null)
            {
                Debug.LogWarning("[WorldStreamingManager] FloatingOriginMP not found. Large world coordinate support disabled.");
            }
            
            return valid;
        }
        
        /// <summary>
        /// Инициализировать стриминг при старте.
        /// </summary>
        private void InitializeStreaming()
        {
            Debug.Log("[WorldStreamingManager] Initializing streaming system...");
            Debug.Log($"[WorldStreamingManager] WorldData: {(worldData != null ? "OK" : "NULL")}");
            Debug.Log($"[WorldStreamingManager] ChunkManager: {(chunkManager != null ? $"OK ({chunkManager.TotalChunkCount} chunks)" : "NULL")}");
            Debug.Log($"[WorldStreamingManager] ChunkGenerator: {(chunkGenerator != null ? "OK" : "NULL")}");
            Debug.Log($"[WorldStreamingManager] ChunkLoader: {(chunkLoader != null ? "OK" : "NULL")}");
            Debug.Log($"[WorldStreamingManager] FloatingOrigin: {(floatingOrigin != null ? "OK" : "NULL")}");
            
            // Настраиваем ChunkLoader с seed
            if (chunkLoader != null && chunkGenerator != null)
            {
                // ChunkLoader должен использовать тот же seed
                // Это настраивается через Inspector или здесь
            }
            
            _initialized = true;
            Debug.Log("[WorldStreamingManager] Streaming system initialized.");
        }
        
        /// <summary>
        /// Периодическое обновление загрузки/выгрузки чанков.
        /// </summary>
        private void UpdateStreaming()
        {
            // Находим позицию камеры или игрока
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                LoadChunksAroundPlayer(mainCamera.transform.position);
            }
            
            // Обновляем preload систему
            UpdatePreload();
        }
        
        #endregion
        
        #region Preload System
        
        /// <summary>
        /// Обновление preload системы — загрузка соседних чанков заранее.
        /// </summary>
        private void UpdatePreload()
        {
            if (preloadLayers <= 0) return;
            
            Camera mainCamera = Camera.main;
            if (mainCamera == null || chunkManager == null) return;
            
            Vector3 playerPos = mainCamera.transform.position;
            ChunkId currentChunk = chunkManager.GetChunkAtPosition(playerPos);
            
            // Проверяем изменился ли центральный чанк
            if (!currentChunk.Equals(_lastPreloadChunk))
            {
                _lastPreloadChunk = currentChunk;
                _lastPreloadTime = Time.time;
                
                // Формируем очередь preload
                BuildPreloadQueue(currentChunk);
                _preloadInProgress = true;
            }
            
            // Обрабатываем preload очередь
            if (_preloadInProgress && Time.time - _lastPreloadTime >= preloadDelay)
            {
                ProcessPreloadQueue();
            }
        }
        
        /// <summary>
        /// Построить очередь чанков для preloading.
        /// </summary>
        private void BuildPreloadQueue(ChunkId centerChunk)
        {
            _preloadQueue.Clear();
            
            if (chunkManager == null) return;
            
            // Загружаем чанки дальше чем loadRadius но в пределах preloadRadius
            int preloadRadius = loadRadius + preloadLayers;
            List<ChunkId> preloadCandidates = chunkManager.GetChunksInRadius(
                new Vector3(centerChunk.GridX * WorldChunkManager.ChunkSize, 0, 
                           centerChunk.GridZ * WorldChunkManager.ChunkSize), 
                preloadRadius
            );
            
            foreach (var chunkId in preloadCandidates)
            {
                // Добавляем только те чанки которые ещё не загружены
                if (!_loadedChunks.Contains(chunkId))
                {
                    // Вычисляем расстояние до центра — ближайшие загружаем первыми
                    int distance = Mathf.Abs(chunkId.GridX - centerChunk.GridX) + 
                                   Mathf.Abs(chunkId.GridZ - centerChunk.GridZ);
                    
                    // Вставляем в очередь по приоритету (расстоянию)
                    int insertIndex = _preloadQueue.Count;
                    for (int i = 0; i < _preloadQueue.Count; i++)
                    {
                        var existingChunk = _preloadQueue[i];
                        int existingDist = Mathf.Abs(existingChunk.GridX - centerChunk.GridX) + 
                                          Mathf.Abs(existingChunk.GridZ - centerChunk.GridZ);
                        if (distance < existingDist)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                    _preloadQueue.Insert(insertIndex, chunkId);
                }
            }
            
            if (showDebugHUD && _preloadQueue.Count > 0)
            {
                Debug.Log($"[WorldStreamingManager] Preload queue built: {_preloadQueue.Count} chunks");
            }
        }
        
        /// <summary>
        /// Обработать очередь preload — загружаем по одному чанку за интервал.
        /// </summary>
        private void ProcessPreloadQueue()
        {
            if (_preloadQueue.Count == 0)
            {
                _preloadInProgress = false;
                return;
            }
            
            if (Time.time - _lastPreloadChunkTime < preloadChunkInterval)
                return;
            
            // Проверяем memory budget
            if (maxLoadedChunks > 0 && _loadedChunks.Count >= maxLoadedChunks)
            {
                if (showDebugHUD)
                {
                    Debug.Log($"[WorldStreamingManager] Memory budget reached: {_loadedChunks.Count}/{maxLoadedChunks}");
                }
                return;
            }
            
            // Загружаем следующий чанк из очереди
            ChunkId chunkToLoad = _preloadQueue[0];
            _preloadQueue.RemoveAt(0);
            _lastPreloadChunkTime = Time.time;
            
            if (!_loadedChunks.Contains(chunkToLoad) && chunkLoader != null)
            {
                chunkLoader.LoadChunk(chunkToLoad);
                _loadedChunks.Add(chunkToLoad);
                
                if (showDebugHUD)
                {
                    Debug.Log($"[WorldStreamingManager] Preloading chunk {chunkToLoad}");
                }
            }
        }
        
        /// <summary>
        /// Получить количество чанков в preload очереди.
        /// </summary>
        public int PreloadQueueCount => _preloadQueue.Count;
        
        /// <summary>
        /// Получить пиковое количество загруженных чанков.
        /// </summary>
        public int PeakLoadedChunks => _peakLoadedChunks;
        
        /// <summary>
        /// Сбросить пиковое значение загруженных чанков.
        /// </summary>
        public void ResetPeakChunks()
        {
            _peakLoadedChunks = _loadedChunks.Count;
        }
        
        #endregion
        
        #region Debug HUD
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugHUD || !_initialized) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("<b>World Streaming Manager</b>", UnityEditor.EditorStyles.boldLabel);
            GUILayout.Label($"Loaded Chunks: {LoadedChunkCount}");
            GUILayout.Label($"Center Chunk: [{_currentCenterChunk.GridX}, {_currentCenterChunk.GridZ}]");
            GUILayout.Label($"Load Radius: {loadRadius}");
            GUILayout.Label($"Unload Radius: {unloadRadius}");
            GUILayout.Label($"Global Seed: {globalSeed}");
            
            if (floatingOrigin != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("<b>FloatingOriginMP</b>", UnityEditor.EditorStyles.boldLabel);
                GUILayout.Label($"Total Offset: {floatingOrigin.TotalOffset}");
                GUILayout.Label($"Shift Count: {floatingOrigin.ShiftCount}");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif
        
        #endregion
    }
}