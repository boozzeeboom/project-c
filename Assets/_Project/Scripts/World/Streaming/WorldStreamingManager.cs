using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.World.Core;
using ProjectC.World.Streaming;

namespace ProjectC.World
{
    /// <summary>
    /// Main manager of World Streaming system.
    /// Coordinates WorldChunkManager, ChunkLoader, FloatingOriginMP
    /// for seamless MMO-style world streaming.
    /// 
    /// Usage:
    /// 1. Add to Empty GameObject in scene
    /// 2. Assign component references (or leave auto-find)
    /// 3. In Play Mode call LoadChunksAroundPlayer() with player position
    /// 
    /// For multiplayer: management happens on server,
    /// clients receive commands via RPC.
    /// </summary>
    public class WorldStreamingManager : MonoBehaviour
    {
        #region Singleton
        
        private static WorldStreamingManager _instance;
        public static WorldStreamingManager Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("References")]
        [Tooltip("WorldData ScriptableObject with arrays, peaks, farms data")]
        [SerializeField] private WorldData worldData;
        
        [Tooltip("WorldChunkManager - chunk registry")]
        [SerializeField] private WorldChunkManager chunkManager;
        
        [Tooltip("ProceduralChunkGenerator - chunk content generation")]
        [SerializeField] private ProceduralChunkGenerator chunkGenerator;
        
        [Tooltip("ChunkLoader - chunk loading/unloading")]
        [SerializeField] private ChunkLoader chunkLoader;
        
        [Tooltip("FloatingOriginMP - for large coordinates")]
        [SerializeField] private FloatingOriginMP floatingOrigin;
        
        [Header("Streaming Settings")]
        [Tooltip("Chunk loading radius around player (in chunks)")]
        [SerializeField, Range(1, 5)]
        private int loadRadius = 2;
        
        [Tooltip("Chunk unloading radius (in chunks) - outside this radius chunks are unloaded")]
        [SerializeField, Range(2, 10)]
        private int unloadRadius = 3;
        
        [Tooltip("Chunk loading/unloading check interval (seconds)")]
        [SerializeField, Range(0.1f, 2f)]
        private float updateInterval = 0.5f;
        
        [Tooltip("Global world seed - affects cloud generation and mountain shape")]
        [SerializeField]
        #pragma warning disable 0414
        private int globalSeed = 42;
        #pragma warning restore 0414
        
        [Header("Debug")]
        [Tooltip("Show debug info on screen")]
        [SerializeField] private bool showDebugHUD = false;
        
        [Header("Preload Settings")]
        [Tooltip("Preload: number of adjacent chunk layers to load in advance")]
        [SerializeField, Range(0, 3)]
        private int preloadLayers = 1;
        
        [Tooltip("Preload: delay after entering new chunk before starting preload (seconds)")]
        [SerializeField, Range(0f, 5f)]
        private float preloadDelay = 1f;
        
        [Tooltip("Preload: interval between loading each preloaded chunk (seconds)")]
        [SerializeField, Range(0.1f, 2f)]
        private float preloadChunkInterval = 0.3f;
        
        [Tooltip("Memory Budget: max loaded chunks (0 = unlimited)")]
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
        
        // Player tracking for chunk loading (I5-001 Fix)
        private Transform _cachedLocalPlayerTransform;
        private float _lastPlayerSearchTime = 0f;
        private const float PLAYER_SEARCH_INTERVAL = 1f;
        
        #endregion
        
        #region Player Position Methods (I5-001)
        
        /// <summary>
        /// I5-001 FIX: Get local player position for chunk streaming.
        /// Uses priority chain:
        /// 1. NetworkPlayer with IsOwner (multiplayer)
        /// 2. Object with "Player" tag
        /// 3. Camera.main (fallback - may be near origin!)
        /// 
        /// IMPORTANT: Camera.main is under TradeZones which is EXCLUDED from world shift.
        /// After FloatingOriginMP shifts the world, Camera.main returns position near (0,0,0).
        /// This is why chunks loaded around spawn instead of around player!
        /// </summary>
        private Vector3 GetLocalPlayerPosition()
        {
            // Try to use cached transform if still valid
            if (_cachedLocalPlayerTransform != null)
            {
                float timeSinceSearch = Time.time - _lastPlayerSearchTime;
                if (timeSinceSearch < PLAYER_SEARCH_INTERVAL)
                {
                    return _cachedLocalPlayerTransform.position;
                }
            }
            
            Vector3 position = Vector3.zero;
            bool found = false;
            
            // Priority 1: NetworkPlayer with IsOwner (multiplayer)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var networkObjects = FindObjectsByType<Unity.Netcode.NetworkObject>();
                foreach (var netObj in networkObjects)
                {
                    if (netObj.IsOwner && netObj.name.Contains("NetworkPlayer"))
                    {
                        _cachedLocalPlayerTransform = netObj.transform;
                        _lastPlayerSearchTime = Time.time;
                        position = netObj.transform.position;
                        found = true;
                        
                        if (showDebugHUD && Time.frameCount % 120 == 0)
                        {
                            Debug.Log($"[WorldStreamingManager] Using NetworkPlayer IsOwner: {netObj.name} at {position:F0}");
                        }
                        break;
                    }
                }
            }
            
            // Priority 2: Object with "Player" tag
            if (!found)
            {
                GameObject playerByTag = GameObject.FindGameObjectWithTag("Player");
                if (playerByTag != null)
                {
                    _cachedLocalPlayerTransform = playerByTag.transform;
                    _lastPlayerSearchTime = Time.time;
                    position = playerByTag.transform.position;
                    found = true;
                    
                    if (showDebugHUD && Time.frameCount % 120 == 0)
                    {
                        Debug.Log($"[WorldStreamingManager] Using Player tag: {playerByTag.name} at {position:F0}");
                    }
                }
            }
            
            // Priority 3: Camera.main fallback (may be wrong!)
            if (!found)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    _cachedLocalPlayerTransform = mainCam.transform;
                    _lastPlayerSearchTime = Time.time;
                    position = mainCam.transform.position;
                    found = true;
                    
                    if (showDebugHUD && Time.frameCount % 120 == 0)
                    {
                        Debug.LogWarning($"[WorldStreamingManager] WARNING: Using Camera.main fallback at {position:F0}. Chunks may load around origin!");
                    }
                }
            }
            
            return position;
        }
        
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
            
            // Auto-find components if not assigned
            AutoFindComponents();
            
            // FIX I2-001: Subscribe to ChunkLoader events
            SubscribeToChunkLoaderEvents();
        }
        
        /// <summary>
        /// FIX I2-001: Subscribe to ChunkLoader events for feedback.
        /// </summary>
        private void SubscribeToChunkLoaderEvents()
        {
            if (chunkLoader != null)
            {
                chunkLoader.OnChunkLoaded += OnChunkLoadedHandler;
                chunkLoader.OnChunkUnloaded += OnChunkUnloadedHandler;
            }
        }
        
        /// <summary>
        /// FIX I2-001: Chunk load event handler.
        /// Called by ChunkLoader after chunk load completes.
        /// </summary>
        private void OnChunkLoadedHandler(ChunkId chunkId)
        {
            // Update peak statistics
            if (_loadedChunks.Count > _peakLoadedChunks)
            {
                _peakLoadedChunks = _loadedChunks.Count;
            }
        }
        
        /// <summary>
        /// FIX I2-001: Chunk unload event handler.
        /// Called by ChunkLoader after chunk unload completes.
        /// </summary>
        private void OnChunkUnloadedHandler(ChunkId chunkId)
        {
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
            
            // Periodic chunk loading/unloading check
            if (Time.time - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.time;
                UpdateStreaming();
            }
        }
        
        private void OnDestroy()
        {
            // FIX I2-001: Unsubscribe from ChunkLoader events
            UnsubscribeFromChunkLoaderEvents();
            
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        /// <summary>
        /// FIX I2-001: Unsubscribe from ChunkLoader events on destroy.
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
        /// Load chunks around player position.
        /// Called automatically from Update, but can be called manually after teleport.
        /// </summary>
        /// <param name="playerPosition">Player position in world coordinates</param>
        /// <param name="radius">Load radius (default from settings)</param>
        public void LoadChunksAroundPlayer(Vector3 playerPosition, int? radius = null)
        {
            if (chunkManager == null || chunkLoader == null)
            {
                Debug.LogError($"[WorldStreamingManager] Cannot load chunks: chunkManager={chunkManager}, chunkLoader={chunkLoader}");
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
            
            // Get chunks in radius
            List<ChunkId> chunksInRange = chunkManager.GetChunksInRadius(playerPosition, effectiveRadius);
            
            // Load new chunks
            foreach (var chunkId in chunksInRange)
            {
                if (!_loadedChunks.Contains(chunkId))
                {
                    chunkLoader.LoadChunk(chunkId);
                    _loadedChunks.Add(chunkId);
                }
            }
            
            // Unload distant chunks
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
            }
        }
        
        /// <summary>
        /// Load chunk by server command (for multiplayer).
        /// Called from PlayerChunkTracker.LoadChunkClientRpc().
        /// CLIENT SHOULD NOT call this method itself!
        /// </summary>
        /// <param name="chunkId">Chunk ID to load</param>
        public void LoadChunkByServerCommand(ChunkId chunkId)
        {
            if (_loadedChunks.Contains(chunkId))
            {
                return;
            }
            
            if (chunkLoader == null)
            {
                Debug.LogError("[WorldStreamingManager] ChunkLoader not initialized!");
                return;
            }
            
            chunkLoader.LoadChunk(chunkId);
            _loadedChunks.Add(chunkId);
        }
        
        /// <summary>
        /// Unload chunk by server command (for multiplayer).
        /// Called from PlayerChunkTracker.UnloadChunkClientRpc().
        /// CLIENT SHOULD NOT call this method itself!
        /// </summary>
        /// <param name="chunkId">Chunk ID to unload</param>
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
            
            chunkLoader.UnloadChunk(chunkId);
            _loadedChunks.Remove(chunkId);
        }
        
        /// <summary>
        /// Teleport player to peak.
        /// Combines ResetOrigin (to avoid floating point issues)
        /// and chunk loading around new position.
        /// </summary>
        /// <param name="peakPosition">Peak position for teleportation</param>
        public void TeleportToPeak(Vector3 peakPosition)
        {
            if (floatingOrigin != null)
            {
                // Shift world so camera stays close to origin
                floatingOrigin.ResetOrigin();
            }
            
            // Load chunks around new position
            LoadChunksAroundPlayer(peakPosition, loadRadius);
            
            Debug.Log($"[WorldStreamingManager] Teleported to {peakPosition}");
        }
        
        /// <summary>
        /// Get loaded chunk count.
        /// </summary>
        public int LoadedChunkCount => _loadedChunks.Count;
        
        /// <summary>
        /// Get current center chunk.
        /// </summary>
        public ChunkId CurrentCenterChunk => _currentCenterChunk;
        
        /// <summary>
        /// Check if chunk is loaded.
        /// </summary>
        public bool IsChunkLoaded(ChunkId chunkId)
        {
            return _loadedChunks.Contains(chunkId);
        }
        
        /// <summary>
        /// Unload all chunks (e.g., on scene change).
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
        /// Auto-find necessary components in scene.
        /// </summary>
        private void AutoFindComponents()
        {
            if (worldData == null)
            {
                // Try to find in scene
                worldData = FindAnyObjectByType<WorldData>();
                
                // If not found - load from Resources or Assets
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
        /// Validate setup correctness.
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
            
            // FloatingOrigin is optional (may not be in single player mode)
            if (floatingOrigin == null)
            {
                Debug.LogWarning("[WorldStreamingManager] FloatingOriginMP not found. Large world coordinate support disabled.");
            }
            
            return valid;
        }
        
        /// <summary>
        /// Initialize streaming on start.
        /// </summary>
        private void InitializeStreaming()
        {
            _initialized = true;
        }
        
        /// <summary>
        /// Periodic chunk loading/unloading update.
        /// I5-001 FIX: Now uses GetLocalPlayerPosition() instead of Camera.main
        /// </summary>
        private void UpdateStreaming()
        {
            // I5-001 FIX: Use player position, not camera position
            // Camera.main is under TradeZones which is EXCLUDED from world shift
            // After FloatingOriginMP shifts the world, Camera.main returns position near (0,0,0)
            Vector3 playerPosition = GetLocalPlayerPosition();
            
            if (playerPosition != Vector3.zero || _cachedLocalPlayerTransform != null)
            {
                LoadChunksAroundPlayer(playerPosition);
            }
            
            // Update preload system
            UpdatePreload();
        }
        
        #endregion
        
        #region Preload System
        
        /// <summary>
        /// Preload system update - loading adjacent chunks in advance.
        /// I5-001 FIX: Now uses GetLocalPlayerPosition() instead of Camera.main
        /// </summary>
        private void UpdatePreload()
        {
            if (preloadLayers <= 0) return;
            
            // I5-001 FIX: Use player position, not camera position
            Vector3 playerPos = GetLocalPlayerPosition();
            if (playerPos == Vector3.zero || chunkManager == null) return;
            
            ChunkId currentChunk = chunkManager.GetChunkAtPosition(playerPos);
            
            // Check if center chunk changed
            if (!currentChunk.Equals(_lastPreloadChunk))
            {
                _lastPreloadChunk = currentChunk;
                _lastPreloadTime = Time.time;
                
                // Build preload queue
                BuildPreloadQueue(currentChunk);
                _preloadInProgress = true;
            }
            
            // Process preload queue
            if (_preloadInProgress && Time.time - _lastPreloadTime >= preloadDelay)
            {
                ProcessPreloadQueue();
            }
        }
        
        /// <summary>
        /// Build queue of chunks for preloading.
        /// </summary>
        private void BuildPreloadQueue(ChunkId centerChunk)
        {
            _preloadQueue.Clear();
            
            if (chunkManager == null) return;
            
            // Load chunks further than loadRadius but within preloadRadius
            int preloadRadius = loadRadius + preloadLayers;
            List<ChunkId> preloadCandidates = chunkManager.GetChunksInRadius(
                new Vector3(centerChunk.GridX * WorldChunkManager.ChunkSize, 0, 
                           centerChunk.GridZ * WorldChunkManager.ChunkSize), 
                preloadRadius
            );
            
            foreach (var chunkId in preloadCandidates)
            {
                // Add only chunks that are not yet loaded
                if (!_loadedChunks.Contains(chunkId))
                {
                    // Calculate distance to center - load nearest first
                    int distance = Mathf.Abs(chunkId.GridX - centerChunk.GridX) + 
                                   Mathf.Abs(chunkId.GridZ - centerChunk.GridZ);
                    
                    // Insert into queue by priority (distance)
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
        }
        
        /// <summary>
        /// Process preload queue - load one chunk per interval.
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
            
            // Check memory budget
            if (maxLoadedChunks > 0 && _loadedChunks.Count >= maxLoadedChunks)
            {
                if (showDebugHUD)
                {
                    Debug.Log($"[WorldStreamingManager] Memory budget reached: {_loadedChunks.Count}/{maxLoadedChunks}");
                }
                return;
            }
            
            // Load next chunk from queue
            ChunkId chunkToLoad = _preloadQueue[0];
            _preloadQueue.RemoveAt(0);
            _lastPreloadChunkTime = Time.time;
            
            if (!_loadedChunks.Contains(chunkToLoad) && chunkLoader != null)
            {
                chunkLoader.LoadChunk(chunkToLoad);
                _loadedChunks.Add(chunkToLoad);
            }
        }
        
        /// <summary>
        /// Get preload queue count.
        /// </summary>
        public int PreloadQueueCount => _preloadQueue.Count;
        
        /// <summary>
        /// Get peak loaded chunk count.
        /// </summary>
        public int PeakLoadedChunks => _peakLoadedChunks;
        
        /// <summary>
        /// Reset peak loaded chunk value.
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
            
            GUILayout.BeginArea(new Rect(10, 10, 450, 250));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("<b>World Streaming Manager</b>", UnityEditor.EditorStyles.boldLabel);
            GUILayout.Label($"Loaded Chunks: {LoadedChunkCount}");
            GUILayout.Label($"Center Chunk: [{_currentCenterChunk.GridX}, {_currentCenterChunk.GridZ}]");
            GUILayout.Label($"Load Radius: {loadRadius}");
            GUILayout.Label($"Unload Radius: {unloadRadius}");
            GUILayout.Label($"Global Seed: {globalSeed}");
            
            // I5-001: Show player tracking info
            GUILayout.Space(5);
            GUILayout.Label("<b>Player Tracking (I5-001)</b>", UnityEditor.EditorStyles.boldLabel);
            GUILayout.Label($"Cached Player: {(_cachedLocalPlayerTransform != null ? _cachedLocalPlayerTransform.name : "NULL")}");
            GUILayout.Label($"Player Pos: {(_cachedLocalPlayerTransform != null ? _cachedLocalPlayerTransform.position.ToString("F0") : "N/A")}");
            
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