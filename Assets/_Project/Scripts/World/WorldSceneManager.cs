using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.World.Scene;
using ProjectC.World.Streaming;
using ProjectC.Player;

namespace ProjectC.World
{
    /// <summary>
    /// Coordination layer between Scene Layer (80k grid) and Chunk Layer (2k grid).
    /// Ensures scenes load BEFORE chunks, manages FloatingOriginMP state, handles preload triggers.
    /// </summary>
    public class WorldSceneManager : MonoBehaviour
    {
        #region Singleton

        private static WorldSceneManager _instance;
        public static WorldSceneManager Instance => _instance;

        #endregion

        #region Configuration

        [Header("References")]
        [Tooltip("ClientSceneLoader for scene loading (80k grid)")]
        [SerializeField] private ClientSceneLoader clientSceneLoader;

        [Tooltip("WorldStreamingManager for chunk loading (2k grid)")]
        [SerializeField] private WorldStreamingManager worldStreamingManager;

        [Tooltip("FloatingOriginMP for large coordinate handling")]
        [SerializeField] private FloatingOriginMP floatingOriginMP;

        [Tooltip("SceneRegistry for grid definitions")]
        [SerializeField] private SceneRegistry sceneRegistry;

        [Header("Preload Settings")]
        [Tooltip("Distance from scene boundary to trigger preload (units)")]
        [SerializeField] private float preloadTriggerDistance = 10000f;

        [Tooltip("Enable scene-aware chunk loading (chunks only load in loaded scenes)")]
        [SerializeField] private bool enableSceneAwareChunkLoading = true;

        [Header("FloatingOrigin Control")]
        [Tooltip("Enable FloatingOriginMP control based on scene loading")]
        [SerializeField] private bool controlFloatingOrigin = true;

        [Tooltip("Distance threshold to enable FloatingOrigin ( hysteresis )")]
        [SerializeField] private float floatingOriginEnableThreshold = 90000f;

        [Tooltip("Distance threshold to disable FloatingOrigin ( hysteresis )")]
        [SerializeField] private float floatingOriginDisableThreshold = 70000f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        #endregion

        #region Private State

        private readonly HashSet<SceneID> _loadedScenes = new HashSet<SceneID>();
        private SceneID _currentPlayerScene;
        private bool _preloadTriggeredX = false;
        private bool _preloadTriggeredZ = false;
        private Transform _playerTransform;
        private bool _isInitialized = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            AutoFindReferences();
            SubscribeToEvents();
            _isInitialized = true;

            LogDebug("WorldSceneManager initialized.");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            Vector3 playerPos = _playerTransform.position;

            UpdatePreloadTrigger(playerPos);

            if (controlFloatingOrigin)
            {
                UpdateFloatingOriginState(playerPos);
            }
        }

        #endregion

        #region Scene Event Handlers

        private void SubscribeToEvents()
        {
            if (clientSceneLoader != null)
            {
                clientSceneLoader.OnSceneLoaded += HandleSceneLoaded;
                clientSceneLoader.OnSceneUnloaded += HandleSceneUnloaded;
                clientSceneLoader.OnSceneTransition += HandleSceneTransition;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (clientSceneLoader != null)
            {
                clientSceneLoader.OnSceneLoaded -= HandleSceneLoaded;
                clientSceneLoader.OnSceneUnloaded -= HandleSceneUnloaded;
                clientSceneLoader.OnSceneTransition -= HandleSceneTransition;
            }
        }

        private void HandleSceneLoaded(SceneID sceneId)
        {
            _loadedScenes.Add(sceneId);

            LogDebug($"Scene loaded: {sceneId}. Total loaded: {_loadedScenes.Count}");

            if (worldStreamingManager != null && enableSceneAwareChunkLoading)
            {
                worldStreamingManager.SetLoadedScenesFilter(_loadedScenes);
            }
        }

        private void HandleSceneUnloaded(SceneID sceneId)
        {
            _loadedScenes.Remove(sceneId);

            LogDebug($"Scene unloaded: {sceneId}. Total loaded: {_loadedScenes.Count}");

            if (worldStreamingManager != null && enableSceneAwareChunkLoading)
            {
                worldStreamingManager.SetLoadedScenesFilter(_loadedScenes);
            }
        }

        private void HandleSceneTransition(SceneID from, SceneID to)
        {
            _currentPlayerScene = to;
            _preloadTriggeredX = false;
            _preloadTriggeredZ = false;

            LogDebug($"Scene transition: {from} -> {to}");
        }

        #endregion

        #region Preload System

        private void UpdatePreloadTrigger(Vector3 playerPosition)
        {
            SceneID playerScene = SceneID.FromWorldPosition(playerPosition);

            if (!playerScene.Equals(_currentPlayerScene))
            {
                _currentPlayerScene = playerScene;
                _preloadTriggeredX = false;
                _preloadTriggeredZ = false;
            }

            Vector3 localPos = playerScene.ToLocalPosition(playerPosition);
            float sceneSize = SceneID.SCENE_SIZE;

            bool shouldPreloadX = false;
            bool shouldPreloadZ = false;

            int maxGridX = sceneRegistry != null ? sceneRegistry.GridColumns - 1 : 5;
            int maxGridZ = sceneRegistry != null ? sceneRegistry.GridRows - 1 : 3;

            if (localPos.x > sceneSize - preloadTriggerDistance && playerScene.GridX < maxGridX)
            {
                shouldPreloadX = true;
            }
            else if (localPos.x < preloadTriggerDistance && playerScene.GridX > 0)
            {
                shouldPreloadX = true;
            }

            if (localPos.z > sceneSize - preloadTriggerDistance && playerScene.GridZ < maxGridZ)
            {
                shouldPreloadZ = true;
            }
            else if (localPos.z < preloadTriggerDistance && playerScene.GridZ > 0)
            {
                shouldPreloadZ = true;
            }

            if (shouldPreloadX && !_preloadTriggeredX)
            {
                _preloadTriggeredX = true;
                int targetX = localPos.x > sceneSize - preloadTriggerDistance
                    ? playerScene.GridX + 1
                    : playerScene.GridX - 1;
                var targetScene = new SceneID(targetX, playerScene.GridZ);
                TriggerPreload(targetScene);
            }

            if (shouldPreloadZ && !_preloadTriggeredZ)
            {
                _preloadTriggeredZ = true;
                int targetZ = localPos.z > sceneSize - preloadTriggerDistance
                    ? playerScene.GridZ + 1
                    : playerScene.GridZ - 1;
                var targetScene = new SceneID(playerScene.GridX, targetZ);
                TriggerPreload(targetScene);
            }
        }

        private void TriggerPreload(SceneID targetScene)
        {
            if (sceneRegistry != null && !sceneRegistry.IsValid(targetScene))
            {
                LogDebug($"Preload target invalid: {targetScene}");
                return;
            }

            if (_loadedScenes.Contains(targetScene))
            {
                LogDebug($"Scene {targetScene} already loaded, skipping preload");
                return;
            }

            if (clientSceneLoader != null)
            {
                LogDebug($"Preloading scene: {targetScene}");
                clientSceneLoader.LoadSceneWithNeighbors(targetScene);
            }
        }

        #endregion

        #region FloatingOrigin Control

        private void UpdateFloatingOriginState(Vector3 playerPosition)
        {
            if (floatingOriginMP == null) return;

            float distFromOrigin = playerPosition.magnitude;
            bool currentlyEnabled = floatingOriginMP.enabled;

            bool shouldBeEnabled = distFromOrigin > floatingOriginEnableThreshold;
            bool shouldBeDisabled = distFromOrigin < floatingOriginDisableThreshold;

            if (shouldBeEnabled && !currentlyEnabled)
            {
                floatingOriginMP.enabled = true;
                LogDebug($"FloatingOriginMP ENABLED (dist={distFromOrigin:F0}, threshold={floatingOriginEnableThreshold:F0})");
            }
            else if (shouldBeDisabled && currentlyEnabled)
            {
                floatingOriginMP.enabled = false;
                LogDebug($"FloatingOriginMP DISABLED (dist={distFromOrigin:F0}, threshold={floatingOriginDisableThreshold:F0})");
            }
        }

        #endregion

        #region Helper Methods

        private void AutoFindReferences()
        {
            if (clientSceneLoader == null)
                clientSceneLoader = FindAnyObjectByType<ClientSceneLoader>();

            if (worldStreamingManager == null)
                worldStreamingManager = FindAnyObjectByType<WorldStreamingManager>();

            if (floatingOriginMP == null)
                floatingOriginMP = FindAnyObjectByType<FloatingOriginMP>();

            if (sceneRegistry == null)
            {
                sceneRegistry = Resources.Load<SceneRegistry>("Scene/SceneRegistry");
                if (sceneRegistry == null)
                    sceneRegistry = Resources.Load<SceneRegistry>("SceneRegistry");
            }
        }

        private void FindPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var networkObjects = FindObjectsByType<NetworkObject>();
                foreach (var netObj in networkObjects)
                {
                    if (netObj.IsOwner && netObj.GetComponent<ProjectC.Player.NetworkPlayer>() != null)
                    {
                        _playerTransform = netObj.transform;
                        LogDebug($"Found local player: {netObj.name}");
                        return;
                    }
                }
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                LogDebug($"Found player by tag: {player.name}");
            }
        }

        #endregion

        #region Public API

        public bool IsSceneLoaded(SceneID sceneId) => _loadedScenes.Contains(sceneId);

        public IReadOnlyCollection<SceneID> GetLoadedScenes() => _loadedScenes;

        public SceneID GetCurrentPlayerScene() => _currentPlayerScene;

        public bool HasSceneFilter => _loadedScenes.Count > 0;

        #endregion

        #region Debug

        private void LogDebug(string message)
        {
            if (showDebugLogs)
                Debug.Log($"[WorldSceneManager] {message}");
        }

        #endregion
    }
}