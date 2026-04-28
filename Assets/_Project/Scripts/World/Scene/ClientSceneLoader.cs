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

        #endregion

        #region Events

        public event System.Action<SceneID> OnSceneLoaded;
        public event System.Action<SceneID> OnSceneUnloaded;
        public event System.Action<SceneID, SceneID> OnSceneTransition;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (sceneRegistry == null)
            {
                sceneRegistry = Resources.Load<SceneRegistry>("SceneRegistry");
            }
        }

        private void Start()
        {
            FindLocalPlayer();
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

            var playerByTag = GameObject.FindGameObjectWithTag("Player");
            if (playerByTag != null)
            {
                playerTransform = playerByTag.transform;
                _isInitialized = true;
                LogDebug($"Found player by tag: {playerByTag.name}");
                return;
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
                Debug.LogWarning($"[ClientSceneLoader] Invalid target scene: {targetScene}");
                yield break;
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
            var scenesToLoad = sceneRegistry.GetSceneGrid3x3(center);
            var loadTasks = new List<Coroutine>();

            foreach (var sceneId in scenesToLoad)
            {
                if (!_loadedScenes.Contains(sceneId) && !_loadingScenes.Contains(sceneId))
                {
                    if (sceneRegistry.IsValid(sceneId))
                    {
                        loadTasks.Add(StartCoroutine(LoadSceneAsync(sceneId)));
                    }
                }
            }

            foreach (var task in loadTasks)
            {
                yield return task;
            }

            if (unloadDistantScenes)
            {
                UnloadDistantScenes(center);
            }
        }

        private IEnumerator LoadSceneAsync(SceneID sceneId)
        {
            string sceneName = sceneRegistry.GetSceneName(sceneId);

            var asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            while (!asyncOp.isDone)
            {
                yield return null;
            }

            _loadedScenes.Add(sceneId);
            _loadingScenes.Remove(sceneId);

            LogDebug($"Scene loaded: {sceneName}");
            OnSceneLoaded?.Invoke(sceneId);
        }

        private IEnumerator UnloadSceneCoroutine(SceneID scene)
        {
            string sceneName = sceneRegistry.GetSceneName(scene);

            if (!_loadedScenes.Contains(scene))
            {
                yield break;
            }

            LogDebug($"Unloading scene: {sceneName}");

            var asyncOp = SceneManager.UnloadSceneAsync(sceneName);

            while (!asyncOp.isDone)
            {
                yield return null;
            }

            _loadedScenes.Remove(scene);

            LogDebug($"Scene unloaded: {sceneName}");
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