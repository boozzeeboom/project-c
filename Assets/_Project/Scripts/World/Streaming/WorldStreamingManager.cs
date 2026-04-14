using System.Collections;
using System.Collections.Generic;
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
        private int globalSeed = 42;
        
        [Header("Debug")]
        [Tooltip("Показывать debug информацию на экране")]
        [SerializeField] private bool showDebugHUD = false;
        
        #endregion
        
        #region Private State
        
        private HashSet<ChunkId> _loadedChunks = new HashSet<ChunkId>();
        private ChunkId _currentCenterChunk;
        private float _lastUpdateTime = 0f;
        private bool _initialized = false;
        
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
            if (_instance == this)
            {
                _instance = null;
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
                if (worldData == null)
                {
                    worldData = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldData>(
                        "Assets/_Project/Data/World/WorldData.asset");
                }
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