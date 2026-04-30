using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.World.Scene
{
    /// <summary>
    /// Client-side компонент для загрузки/выгрузки сцен через additive loading.
    /// Получает команды от ServerSceneManager и выполняет загрузку.
    /// </summary>
    public class ClientSceneLoader : MonoBehaviour
    {
        #region Configuration

        [Header("References")]
        [Tooltip("Transform игрока для определения текущей сцены")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("ScriptableObject с данными о сценах мира")]
        [SerializeField] private SceneRegistry sceneRegistry;

        [Header("Loading Settings")]
        [Tooltip("Загружать ли соседние сцены (3x3 вместо 1x1)")]
        [SerializeField] private bool loadNeighbors = true;

        [Tooltip("Выгружать ли неиспользуемые сцены")]
        [SerializeField] private bool unloadDistantScenes = true;

        [Header("Loading Debug")]
        [SerializeField] private bool showDebugLogs = false;

        #endregion

        #region Private State

        private readonly HashSet<SceneID> _loadedScenes = new HashSet<SceneID>();
        private readonly HashSet<SceneID> _loadingScenes = new HashSet<SceneID>();
        private SceneID _currentScene;
        private bool _isInitialized = false;
        private bool _isLoadingInitialScene = false;
        private bool _isTransitioning = false;

        #endregion

        #region Events

        public event System.Action<SceneID> OnSceneLoaded;
        public event System.Action<SceneID> OnSceneUnloaded;
        public event System.Action<SceneID, SceneID> OnSceneTransition;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (sceneRegistry == null)
            {
                sceneRegistry = Resources.Load<SceneRegistry>("Scene/SceneRegistry");
                if (sceneRegistry == null)
                    sceneRegistry = Resources.Load<SceneRegistry>("SceneRegistry");
            }
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
            }
        }

        private void Start()
        {
            FindLocalPlayer();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            }
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            // FIX-3: Only load if this is local client, no scene loaded, and not already loading
            if (clientId == NetworkManager.Singleton.LocalClientId && 
                _currentScene.Equals(default) &&
                !_isLoadingInitialScene)
            {
                _isLoadingInitialScene = true;
                StartCoroutine(AutoLoadInitialSceneCoroutine());
            }
        }

        private IEnumerator AutoLoadInitialSceneCoroutine()
        {
            yield return new WaitForSeconds(1f);

            if (!_currentScene.Equals(default))
            {
                _isLoadingInitialScene = false;
                yield break;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                SceneID initialScene = new SceneID(0, 0);
                Debug.Log($"[ClientSceneLoader] Auto-loading initial scene for Host: {initialScene}");
                yield return LoadSceneWithNeighborsCoroutine(initialScene);
            }
            
            _isLoadingInitialScene = false;
        }

        private void FindLocalPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var networkObjects = FindObjectsByType<NetworkObject>();
                foreach (var netObj in networkObjects)
                {
                    if (netObj.IsOwner && netObj.GetComponent<ProjectC.Player.NetworkPlayer>() != null)
                    {
                        playerTransform = netObj.transform;
                        _isInitialized = true;
                        LogDebug($"Found local player: {netObj.name}");
                        return;
                    }
                }
            }

            StartCoroutine(WaitForPlayer());
        }

        private IEnumerator WaitForPlayer()
        {
            yield return new WaitForSeconds(1f);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var networkObjects = FindObjectsByType<NetworkObject>();
                foreach (var netObj in networkObjects)
                {
                    if (netObj.IsOwner && netObj.GetComponent<ProjectC.Player.NetworkPlayer>() != null)
                    {
                        playerTransform = netObj.transform;
                        _isInitialized = true;
                        LogDebug($"Found local player after wait: {netObj.name}");
                        yield break;
                    }
                }
            }

            var playerByTag = GameObject.FindGameObjectWithTag("Player");
            if (playerByTag != null)
            {
                playerTransform = playerByTag.transform;
                _isInitialized = true;
                LogDebug($"Found player by tag after wait: {playerByTag.name}");
                yield break;
            }

            Debug.LogWarning("[ClientSceneLoader] NetworkPlayer not found after waiting. Will retry...");
            StartCoroutine(WaitForPlayer());
        }

        private void Update()
        {
            if (!_isInitialized || playerTransform == null) return;

            SceneID playerScene = SceneID.FromWorldPosition(playerTransform.position);

            if (!_currentScene.Equals(default) && !playerScene.Equals(_currentScene))
            {
                LogDebug($"Player crossed scene boundary: {_currentScene} -> {playerScene}");
                OnSceneTransition?.Invoke(_currentScene, playerScene);
                _currentScene = playerScene;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Загрузить сцену по команде сервера.
        /// </summary>
        public void LoadScene(SceneID targetScene, Vector3 localSpawnPos)
        {
            StartCoroutine(LoadSceneCoroutine(targetScene, localSpawnPos));
        }

        /// <summary>
        /// Загрузить сцену и её соседей (3x3 grid).
        /// </summary>
        public void LoadSceneWithNeighbors(SceneID center)
        {
            StartCoroutine(LoadSceneWithNeighborsCoroutine(center));
        }

        /// <summary>
        /// Выгрузить сцену.
        /// </summary>
        public void UnloadScene(SceneID scene)
        {
            if (!_loadedScenes.Contains(scene)) return;

            StartCoroutine(UnloadSceneCoroutine(scene));
        }

        /// <summary>
        /// Выгрузить все сцены кроме указанных.
        /// </summary>
        public void UnloadAllScenesExcept(HashSet<SceneID> except)
        {
            foreach (var scene in _loadedScenes.ToList())
            {
                if (!except.Contains(scene))
                {
                    UnloadScene(scene);
                }
            }
        }

        public SceneID GetCurrentScene() => _currentScene;

        public bool IsSceneLoaded(SceneID scene) => _loadedScenes.Contains(scene);

        public IEnumerable<SceneID> GetLoadedScenes() => _loadedScenes;

        public void LoadSceneOnly(SceneID scene)
        {
            StartCoroutine(LoadSceneAsync(scene));
        }

        public void LoadInitialScene(SceneID scene)
        {
            StartCoroutine(LoadSceneWithNeighborsCoroutine(scene));
        }

        #endregion

        #region Scene Loading Coroutines

        private IEnumerator LoadSceneCoroutine(SceneID targetScene, Vector3 localSpawnPos)
        {
            if (_loadedScenes.Contains(targetScene) || _loadingScenes.Contains(targetScene))
            {
                LogDebug($"Scene {targetScene} already loaded or loading");
                yield break;
            }

            if (!sceneRegistry.IsValid(targetScene))
            {
                Debug.LogWarning($"[ClientSceneLoader] Invalid target scene: {targetScene}, clamping to valid range");
                targetScene = new SceneID(
                    Mathf.Clamp(targetScene.GridX, 0, Mathf.Max(0, sceneRegistry.GridColumns - 1)),
                    Mathf.Clamp(targetScene.GridZ, 0, Mathf.Max(0, sceneRegistry.GridRows - 1))
                );
            }

            _loadingScenes.Add(targetScene);

            string sceneName = sceneRegistry.GetSceneName(targetScene);
            LogDebug($"Loading scene: {sceneName}");

            yield return LoadSceneAsync(targetScene);

            if (loadNeighbors)
            {
                yield return LoadSceneWithNeighborsCoroutine(targetScene);
            }

            _currentScene = targetScene;

            if (playerTransform != null)
            {
                Vector3 worldPos = targetScene.ToWorldPosition(localSpawnPos);
                playerTransform.position = worldPos;
            }
        }

        private IEnumerator LoadSceneWithNeighborsCoroutine(SceneID center)
        {
            _isTransitioning = true;
            Debug.Log($"[CSL] LoadSceneWithNeighborsCoroutine START: center={center}, loadedScenes={_loadedScenes.Count}");
            
            var scenesToLoad = sceneRegistry.GetSceneGrid3x3(center);
            var loadTasks = new List<Coroutine>();

            foreach (var sceneId in scenesToLoad)
            {
                Debug.Log($"[CSL] Queuing load for {sceneId}, _loadedScenes.Contains={_loadedScenes.Contains(sceneId)}, _loadingScenes.Contains={_loadingScenes.Contains(sceneId)}");
                if (!_loadedScenes.Contains(sceneId) && !_loadingScenes.Contains(sceneId))
                {
                    if (sceneRegistry.IsValid(sceneId))
                    {
                        Debug.Log($"[CSL] Starting LoadSceneAsync for {sceneId}");
                        loadTasks.Add(StartCoroutine(LoadSceneAsync(sceneId)));
                    }
                }
                else
                {
                    Debug.Log($"[CSL] SKIPPING {sceneId} - already in _loadedScenes or _loadingScenes");
                }
            }

            Debug.Log($"[CSL] Waiting for {loadTasks.Count} loads to complete...");
            foreach (var task in loadTasks)
            {
                yield return task;
            }
            Debug.Log($"[CSL] All loads complete. loadedScenes now={_loadedScenes.Count}");

            // FIX-1: Unload AFTER all loads complete - use coroutine to ensure sequential execution
            if (unloadDistantScenes)
            {
                Debug.Log($"[CSL] Calling UnloadDistantScenesCoroutine...");
                yield return StartCoroutine(UnloadDistantScenesCoroutine(center));
                Debug.Log($"[CSL] UnloadDistantScenesCoroutine complete. loadedScenes now={_loadedScenes.Count}");
            }

            _isTransitioning = false;
            Debug.Log($"[CSL] LoadSceneWithNeighborsCoroutine END. Final loadedScenes={_loadedScenes.Count}");
        }

        private IEnumerator UnloadDistantScenesCoroutine(SceneID center)
        {
            var keepScenes = sceneRegistry.GetSceneGrid5x5(center);
            Debug.Log($"[CSL] UnloadDistantScenesCoroutine: center={center}, keepScenes={keepScenes.Count}, current loaded={_loadedScenes.Count}");
            
            var unloadTasks = new List<Coroutine>();

            foreach (var loaded in _loadedScenes.ToList())
            {
                bool shouldKeep = false;
                foreach (var keep in keepScenes)
                {
                    if (loaded.Equals(keep))
                    {
                        shouldKeep = true;
                        break;
                    }
                }

                if (!shouldKeep)
                {
                    Debug.Log($"[CSL] Queuing unload for distant scene {loaded}");
                    // Queue unload coroutine - do not start immediately to avoid race with loading
                    unloadTasks.Add(StartCoroutine(UnloadSceneCoroutine(loaded)));
                }
                else
                {
                    Debug.Log($"[CSL] Keeping scene {loaded} (in keepScenes)");
                }
            }

            Debug.Log($"[CSL] Waiting for {unloadTasks.Count} unloads to complete...");
            // Wait for all unloads to complete
            foreach (var task in unloadTasks)
            {
                yield return task;
            }
            Debug.Log($"[CSL] All unloads complete. loadedScenes={_loadedScenes.Count}");
        }

        private IEnumerator LoadSceneAsync(SceneID sceneId)
        {
            string sceneName = sceneRegistry.GetSceneName(sceneId);
            Debug.Log($"[CSL] LoadSceneAsync START: {sceneName} (id={sceneId})");
            
            // CRITICAL FIX: Add to _loadingScenes FIRST to prevent race condition
            if (_loadingScenes.Contains(sceneId))
            {
                Debug.Log($"[CSL] LoadSceneAsync SKIP: {sceneId} already in _loadingScenes");
                yield break;
            }
            
            _loadingScenes.Add(sceneId);
            Debug.Log($"[CSL] Added {sceneId} to _loadingScenes. Count: {_loadingScenes.Count}");

            var asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (asyncOp == null)
            {
                Debug.LogError($"[ClientSceneLoader] Failed to start load for scene: {sceneName}");
                _loadingScenes.Remove(sceneId);
                yield break;
            }

            while (!asyncOp.isDone)
            {
                yield return null;
            }

            Debug.Log($"[CSL] LoadSceneAsync COMPLETE: {sceneName}, _loadedScenes.Contains={_loadedScenes.Contains(sceneId)}");
            
            if (_loadingScenes.Contains(sceneId))
            {
                _loadedScenes.Add(sceneId);
                _loadingScenes.Remove(sceneId);
                Debug.Log($"[CSL] SUCCESS: Added {sceneId} to _loadedScenes. Total loaded: {_loadedScenes.Count}");
                OnSceneLoaded?.Invoke(sceneId);
            }
            else
            {
                Debug.LogWarning($"[ClientSceneLoader] FAILURE: Scene {sceneName} was already unloading during load");
            }
        }

        private IEnumerator UnloadSceneCoroutine(SceneID scene)
        {
            string sceneName = sceneRegistry.GetSceneName(scene);
            Debug.Log($"[CSL] UnloadSceneCoroutine START: {sceneName} (id={scene}), _loadedScenes before={_loadedScenes.Contains(scene)}");

            if (!_loadedScenes.Contains(scene))
            {
                Debug.Log($"[CSL] UnloadSceneCoroutine SKIP: {scene} not in _loadedScenes");
                yield break;
            }

            // IMPORTANT: Remove from _loadedScenes BEFORE starting async op
            // This prevents race condition where LoadSceneAsync checks _loadingScenes
            // but scene was removed from _loadedScenes already
            Debug.Log($"[CSL] Removing {scene} from _loadedScenes BEFORE unload");
            _loadedScenes.Remove(scene);
            Debug.Log($"[CSL] After remove: _loadedScenes.Contains={_loadedScenes.Contains(scene)}");

            var asyncOp = SceneManager.UnloadSceneAsync(sceneName);

            // FIX-1: Check for null asyncOp (scene might not be loaded)
            if (asyncOp == null)
            {
                Debug.LogWarning($"[CSL] UnloadSceneAsync returned null for {sceneName}");
            }
            else
            {
                while (!asyncOp.isDone)
                {
                    yield return null;
                }
            }

            Debug.Log($"[CSL] UnloadSceneCoroutine COMPLETE: {scene}");
            OnSceneUnloaded?.Invoke(scene);
        }

        private void UnloadDistantScenes(SceneID center)
        {
            var keepScenes = sceneRegistry.GetSceneGrid5x5(center);

            foreach (var loaded in _loadedScenes.ToList())
            {
                bool shouldKeep = false;
                foreach (var keep in keepScenes)
                {
                    if (loaded.Equals(keep))
                    {
                        shouldKeep = true;
                        break;
                    }
                }

                if (!shouldKeep)
                {
                    UnloadScene(loaded);
                }
            }
        }

        #endregion

        #region Debug

        private void LogDebug(string message)
        {
            if (showDebugLogs)
                Debug.Log($"[ClientSceneLoader] {message}");
        }

        #endregion
    }
}