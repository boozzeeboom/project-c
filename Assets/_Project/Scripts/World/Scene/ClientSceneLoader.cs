using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.World.Scene
{
    public class ClientSceneLoader : MonoBehaviour
    {
        #region Singleton
        private static ClientSceneLoader _instance;
        public static ClientSceneLoader Instance => _instance;

        private void Awake()
        {
            Debug.Log($"[CSL] Awake: START, _instance={(object)(_instance != null ? _instance.gameObject : null)?.GetInstanceID()}, this={gameObject.GetInstanceID()}");

            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[CSL] DUPLICATE! Destroying this={gameObject.GetInstanceID()}, keeping instance={_instance.gameObject.GetInstanceID()}");
                Destroy(gameObject);
                return;
            }

            if (_instance == this)
            {
                Debug.Log($"[CSL] Awake: ALREADY SET, possible duplicate OnDestroy issue! _instance={_instance?.gameObject?.GetInstanceID()}");
            }

            _instance = this;
            Debug.Log($"[CSL] Awake: Singleton set, instanceId={gameObject.GetInstanceID()}");
            DontDestroyOnLoad(gameObject);

            if (sceneRegistry == null)
            {
                sceneRegistry = Resources.Load<SceneRegistry>("Scene/SceneRegistry");
                if (sceneRegistry == null)
                    sceneRegistry = Resources.Load<SceneRegistry>("SceneRegistry");
            }
        }

        private void OnDestroy()
        {
            Debug.Log($"[CSL] OnDestroy: instanceId={gameObject.GetInstanceID()}, _instance==this: {_instance == this}");
            if (_instance == this)
                _instance = null;
        }
        #endregion

        #region Configuration
        [SerializeField] private Transform playerTransform;
        [SerializeField] private SceneRegistry sceneRegistry;
        [SerializeField] private bool loadNeighbors = true;
        [SerializeField] private bool unloadDistantScenes = true;
        [SerializeField] private bool showDebugLogs = false;
        #endregion

        #region Private State
        private readonly HashSet<SceneID> _loadedScenes = new HashSet<SceneID>();
        private readonly HashSet<SceneID> _loadingScenes = new HashSet<SceneID>();
        private SceneID _currentScene = new SceneID(-1, -1);
        private bool _isInitialized = false;
        private bool _isLoadingInitialScene = false;
        private bool _isTransitioning = false;
        private Vector3 _lastPlayerPos;
        private Vector3 _teleportTarget;
        private float _lastTeleportTime;
        private bool _initialLoadHadNoSceneLoadedYet = false;
        #endregion

        #region Events
        public event System.Action<SceneID> OnSceneLoaded;
        public event System.Action<SceneID> OnSceneUnloaded;
        public event System.Action<SceneID, SceneID> OnSceneTransition;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            var nmc = FindAnyObjectByType<ProjectC.Core.NetworkManagerController>();
            if (nmc != null)
            {
                nmc.OnPlayerConnected += OnPlayerConnected;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                FindLocalPlayer();
            }
            else
            {
                StartCoroutine(WaitForNetworkAndPlayer());
            }
        }

        private void Update()
        {
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log($"[CSL] Update: this={gameObject.GetInstanceID()}, _instance={_instance?.gameObject?.GetInstanceID()}, _currentScene={_currentScene}");
            }

if (playerTransform == null)
            {
                if (_isInitialized && Time.frameCount % 120 == 0)
                    Debug.Log("[CSL] Update: playerTransform is NULL!");
                return;
            }

if (!_isInitialized)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    FindLocalPlayer();
                }
                return;
            }

            if (_isTransitioning)
            {
                return;
            }

            Vector3 pos = playerTransform.position;
            Vector3 rawPos = pos;

            SceneID playerScene = SceneID.FromWorldPosition(pos);

            if (Time.frameCount % 60 == 0)
            {
                Vector3 posNow = playerTransform.position;
                Vector3 delta = posNow - _lastPlayerPos;
                bool isMoving = delta.magnitude > 0.001f;
                bool isTeleportFrame = Time.time - _lastTeleportTime < 1f;

                if (isMoving && !isTeleportFrame)
                {
                    Debug.LogWarning($"[CSL] DETECTED MOVEMENT: delta.z={delta.z} NOT during teleport! Something is moving player!");
                }

                Debug.Log($"[CSL] Update: rawPos={rawPos}, pos={pos}, playerScene={playerScene}, _currentScene={_currentScene}, playerTransform={playerTransform.name}#{playerTransform.GetInstanceID()}");
                Debug.Log($"[CSL] Update: pos.z={pos.z}, SCENE_SIZE={79999f}, gridZ={Mathf.FloorToInt(pos.z / 79999f)}");
                Debug.Log($"[CSL] Update: isMoving={isMoving}, isTeleportFrame={isTeleportFrame}, delta={delta}");
                Debug.Log($"[CSL] Update: ACTUAL Transform.worldPosition={playerTransform.position}, ACTUAL Transform.worldToLocalMatrix.m03={playerTransform.worldToLocalMatrix.m03}");

                _lastPlayerPos = posNow;
            }
            else if (pos.z > 80000 && _currentScene.GridZ == 0)
            {
                Debug.LogError($"[CSL] ★★★ CRITICAL: pos.z={pos.z} (>80000) but _currentScene.GridZ=0! playerTransform={playerTransform.name}#{playerTransform.GetInstanceID()}");
            }

            if (_currentScene.GridX < 0)
            {
                if (_isLoadingInitialScene)
                {
                    if (Time.frameCount % 120 == 0)
                        Debug.Log($"[CSL] Update: _currentScene.GridX < 0, _isLoadingInitialScene={_isLoadingInitialScene}, waiting...");
                    return;
                }
                Debug.LogWarning($"[CSL] Update: _currentScene.GridX < 0, _isLoadingInitialScene={_isLoadingInitialScene}! Triggering emergency load...");
                _isLoadingInitialScene = true;
                StartCoroutine(LoadSceneWithNeighborsCoroutine(new SceneID(0, 0)));
                return;
            }

            if (!playerScene.Equals(_currentScene))
            {
                Debug.Log($"[CSL] ★ SCENE MISMATCH! {_currentScene} -> {playerScene} at pos {pos}");

                if (sceneRegistry != null && sceneRegistry.IsValid(playerScene))
                {
                    Debug.Log($"[CSL] Loading new scene area around {playerScene}...");
                    StartCoroutine(LoadSceneWithNeighborsCoroutine(playerScene));
                    OnSceneTransition?.Invoke(_currentScene, playerScene);
                }
                else
                {
                    if (sceneRegistry == null)
                        Debug.LogError($"[CSL] Target scene {playerScene} is INVALID because sceneRegistry is NULL!");
                    else
                        Debug.LogError($"[CSL] Target scene {playerScene} is INVALID! GridZ={playerScene.GridZ}, valid range 0-{sceneRegistry.GridRows-1}");
                }
            }
        }
        #endregion

        #region Private Methods
        private void OnPlayerConnected(ulong clientId)
        {
            Debug.Log($"[CSL] OnPlayerConnected: clientId={clientId}");
            StartCoroutine(UpdatePlayerTransformAfterSpawn(clientId));
            OnClientConnectedCallback(clientId);
        }

        private IEnumerator UpdatePlayerTransformAfterSpawn(ulong clientId)
        {
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSeconds(0.5f);

                if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
                {
                    var networkObjects = FindObjectsByType<NetworkObject>();

                    foreach (var netObj in networkObjects)
                    {
                        if (netObj.name.Contains("PlayerSpawner") || netObj.name.Contains("Spawner"))
                            continue;

                        if (netObj.IsOwner)
                        {
                            var networkPlayer = netObj.GetComponent<ProjectC.Player.NetworkPlayer>();
                            if (networkPlayer != null)
                            {
                                playerTransform = netObj.transform;
                                _isInitialized = true;
                                Debug.Log($"[CSL] ★ SUCCESS: playerTransform = {netObj.name} at {netObj.transform.position}");
                                yield break;
                            }
                        }
                    }
                }
            }
            Debug.LogWarning("[CSL] Could not find NetworkPlayer to update playerTransform");
        }

        private IEnumerator WaitForNetworkAndPlayer()
        {
            yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
            FindLocalPlayer();
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            Debug.Log($"[CSL] OnClientConnectedCallback: clientId={clientId}");

            if (NetworkManager.Singleton != null &&
                clientId == NetworkManager.Singleton.LocalClientId &&
                _currentScene.GridX < 0 &&
                !_isLoadingInitialScene)
            {
                Debug.Log($"[CSL] Triggering auto-load for client {clientId}");
                _isLoadingInitialScene = true;
                StartCoroutine(AutoLoadInitialSceneCoroutine());
            }
        }

        private IEnumerator AutoLoadInitialSceneCoroutine()
        {
            Debug.Log("[CSL] AutoLoadInitialSceneCoroutine STARTING...");
            Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: _currentScene={_currentScene}, _isLoadingInitialScene={_isLoadingInitialScene}");
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: AFTER WAIT _currentScene={_currentScene}, _isLoadingInitialScene={_isLoadingInitialScene}");

            if (_currentScene.GridX >= 0)
            {
                Debug.Log($"[CSL] _currentScene already set to {_currentScene}, skipping");
                _isLoadingInitialScene = false;
                yield break;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                SceneID initialScene = new SceneID(0, 0);
                Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: Loading initial scene {initialScene}");

                yield return LoadSceneWithNeighborsCoroutine(initialScene);

                Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: Load complete, _currentScene={_currentScene}");

                if (playerTransform != null)
                {
                    Vector3 worldSpawnPos = initialScene.WorldCenter + new Vector3(0, 3, 0);
                    _teleportTarget = worldSpawnPos;
                    _lastTeleportTime = Time.time;
                    Debug.Log($"[CSL] Teleporting player from {playerTransform.position} to {worldSpawnPos}");

                    var networkPlayer = playerTransform.GetComponent<ProjectC.Player.NetworkPlayer>();
                    if (networkPlayer != null)
                    {
                        Debug.Log("[CSL] Using NetworkPlayer.TeleportServerRpc for proper network sync");
                        networkPlayer.TeleportServerRpc(worldSpawnPos);
                    }
                    else
                    {
                        playerTransform.position = worldSpawnPos;
                    }

                    _lastPlayerPos = playerTransform.position;
                    Debug.Log($"[CSL] AFTER TELEPORT: playerTransform.position.z={playerTransform.position.z}, expected z={worldSpawnPos.z}");
                }
                else
                {
                    Debug.LogError("[CSL] CRITICAL: playerTransform is NULL! Cannot teleport!");
                }
            }

            _isLoadingInitialScene = false;
            Debug.Log("[CSL] AutoLoadInitialSceneCoroutine COMPLETE");
        }

        private void FindLocalPlayer()
        {
            Debug.Log("[CSL] FindLocalPlayer() called");

            var playerByTag = GameObject.FindGameObjectWithTag("Player");
            Debug.Log($"[CSL] FindGameObjectWithTag('Player') = {playerByTag?.name ?? "NULL"}");
            if (playerByTag != null)
            {
                playerTransform = playerByTag.transform;
                _isInitialized = true;
                Debug.Log($"[CSL] Found PLAYER by tag: {playerByTag.name} at {playerByTag.transform.position}");
                return;
            }
            else
            {
                Debug.LogWarning("[CSL] FindGameObjectWithTag('Player') returned NULL! Searching alternatives...");
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var networkPlayers = FindObjectsByType<ProjectC.Player.NetworkPlayer>();
                Debug.Log($"[CSL] FindLocalPlayer: searching {networkPlayers.Length} NetworkPlayers");

                foreach (var networkPlayer in networkPlayers)
                {
                    if (networkPlayer.IsOwner)
                    {
                        playerTransform = networkPlayer.transform;
                        _isInitialized = true;
                        Debug.Log($"[CSL] Found OWNER NetworkPlayer: {networkPlayer.name} at {networkPlayer.transform.position}");
                        return;
                    }
                }

                var networkObjects = FindObjectsByType<NetworkObject>();
                Debug.Log($"[CSL] FindLocalPlayer: searching {networkObjects.Length} NetworkObjects (Player tag and NetworkPlayer failed)");

                foreach (var netObj in networkObjects)
                {
                    if (netObj.name.Contains("PlayerSpawner") || netObj.name.Contains("Spawner"))
                        continue;

                    if (netObj.IsOwner)
                    {
                        var networkPlayer = netObj.GetComponent<ProjectC.Player.NetworkPlayer>();
                        if (networkPlayer != null)
                        {
                            playerTransform = netObj.transform;
                            _isInitialized = true;
                            Debug.Log($"[CSL] Found NetworkPlayer (via NetworkObject): {netObj.name} at {netObj.transform.position}");
                            return;
                        }
                    }
                }
            }

            StartCoroutine(WaitForPlayer());
        }

        private IEnumerator WaitForPlayer()
        {
            for (int retry = 0; retry < 20; retry++)
            {
                yield return new WaitForSeconds(0.5f);

                if (playerTransform != null && _isInitialized)
                    yield break;

                var playerByTag = GameObject.FindGameObjectWithTag("Player");
                if (playerByTag != null)
                {
                    playerTransform = playerByTag.transform;
                    _isInitialized = true;
                    Debug.Log($"[CSL] ★ WaitForPlayer SUCCESS (via Player tag): {playerByTag.name}");
                    yield break;
                }

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    var networkPlayers = FindObjectsByType<ProjectC.Player.NetworkPlayer>();
                    foreach (var networkPlayer in networkPlayers)
                    {
                        if (networkPlayer.IsOwner)
                        {
                            playerTransform = networkPlayer.transform;
                            _isInitialized = true;
                            Debug.Log($"[CSL] ★ WaitForPlayer SUCCESS: {networkPlayer.name}");
                            yield break;
                        }
                    }

                    var networkObjects = FindObjectsByType<NetworkObject>();
                    foreach (var netObj in networkObjects)
                    {
                        if (netObj.name.Contains("PlayerSpawner") || netObj.name.Contains("Spawner"))
                            continue;

                        if (netObj.IsOwner)
                        {
                            var networkPlayer = netObj.GetComponent<ProjectC.Player.NetworkPlayer>();
                            if (networkPlayer != null)
                            {
                                playerTransform = netObj.transform;
                                _isInitialized = true;
                                Debug.Log($"[CSL] ★ WaitForPlayer SUCCESS (via NetObj): {netObj.name}");
                                yield break;
                            }
                        }
                    }
                }

                if (retry % 4 == 0)
                    Debug.Log($"[CSL] WaitForPlayer: attempt {retry}/20");
            }

            Debug.LogError("[CSL] Failed to find NetworkPlayer after 10 seconds!");
        }
        #endregion

        #region Public API
        public void LoadScene(SceneID targetScene, Vector3 localSpawnPos)
        {
            StartCoroutine(LoadSceneCoroutine(targetScene, localSpawnPos));
        }

        public void LoadSceneWithNeighbors(SceneID center)
        {
            StartCoroutine(LoadSceneWithNeighborsCoroutine(center));
        }

        public void UnloadScene(SceneID scene)
        {
            if (!_loadedScenes.Contains(scene)) return;
            StartCoroutine(UnloadSceneCoroutine(scene));
        }

        public void UnloadAllScenesExcept(HashSet<SceneID> except)
        {
            foreach (var scene in _loadedScenes.ToList())
            {
                if (!except.Contains(scene))
                    UnloadScene(scene);
            }
        }

        public SceneID GetCurrentScene() => _currentScene;
        public bool IsSceneLoaded(SceneID scene) => _loadedScenes.Contains(scene);
        public IEnumerable<SceneID> GetLoadedScenes() => _loadedScenes;
        public void LoadSceneOnly(SceneID scene) => StartCoroutine(LoadSceneAsync(scene));
        public void LoadInitialScene(SceneID scene) => StartCoroutine(LoadSceneWithNeighborsCoroutine(scene));
        #endregion

        #region Scene Loading Coroutines
        private IEnumerator LoadSceneCoroutine(SceneID targetScene, Vector3 localSpawnPos)
        {
            Debug.Log($"[CSL] LoadSceneCoroutine({targetScene}) called");

            if (_loadedScenes.Contains(targetScene) || _loadingScenes.Contains(targetScene))
            {
                Debug.Log($"[CSL] Scene {targetScene} already loaded or loading, skipping");
                yield break;
            }

            if (sceneRegistry != null && !sceneRegistry.IsValid(targetScene))
            {
                Debug.LogWarning($"[CSL] Invalid target scene: {targetScene}, clamping");
                targetScene = new SceneID(
                    Mathf.Clamp(targetScene.GridX, 0, Mathf.Max(0, sceneRegistry.GridColumns - 1)),
                    Mathf.Clamp(targetScene.GridZ, 0, Mathf.Max(0, sceneRegistry.GridRows - 1))
                );
            }

            _loadingScenes.Add(targetScene);

            string sceneName = sceneRegistry != null ? sceneRegistry.GetSceneName(targetScene) : $"WorldScene_{targetScene.GridX}_{targetScene.GridZ}";
            Debug.Log($"[CSL] LoadSceneCoroutine: Loading {sceneName}");

            yield return LoadSceneAsync(targetScene);

            if (loadNeighbors)
                yield return LoadSceneWithNeighborsCoroutine(targetScene);

            _currentScene = targetScene;

            if (playerTransform != null)
            {
                Vector3 worldPos = targetScene.ToWorldPosition(localSpawnPos);
                playerTransform.position = worldPos;
            }
        }

        private IEnumerator LoadSceneWithNeighborsCoroutine(SceneID center)
        {
            Debug.Log($"[CSL] LoadSceneWithNeighborsCoroutine START: center={center}, _isLoadingInitialScene={_isLoadingInitialScene}, _currentScene={_currentScene}");
            _isTransitioning = true;

            if (sceneRegistry == null)
            {
                Debug.LogError("[CSL] sceneRegistry is NULL!");
                _isTransitioning = false;
                yield break;
            }

            var scenesToLoad = sceneRegistry.GetSceneGrid3x3(center);
            var loadTasks = new List<Coroutine>();

            foreach (var sceneId in scenesToLoad)
            {
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
                yield return task;

            Debug.Log($"[CSL] All loads complete. loadedScenes={_loadedScenes.Count}");

            _currentScene = center;
            Debug.Log($"[CSL] Set _currentScene = {center}");

            if (unloadDistantScenes)
            {
                yield return StartCoroutine(UnloadDistantScenesCoroutine(center));
            }

_isTransitioning = false;
            _isLoadingInitialScene = false;
            Debug.Log($"[CSL] LoadSceneWithNeighborsCoroutine END. Final loadedScenes={_loadedScenes.Count}");

            var loadedInSceneManager = UnityEngine.SceneManagement.SceneManager.GetSceneByName("WorldScene_0_0");
            if (loadedInSceneManager.isLoaded)
            {
                var roots = loadedInSceneManager.GetRootGameObjects();
                Debug.Log($"[CSL] WorldScene_0_0 has {roots.Length} root objects:");
                foreach (var r in roots)
                {
                    Debug.Log($"[CSL]   - {r.name}, active={r.activeSelf}, pos={r.transform.position}");
                }
            }
            else
            {
                Debug.LogWarning("[CSL] WorldScene_0_0 is NOT loaded in SceneManager!");
            }
        }

        private IEnumerator UnloadDistantScenesCoroutine(SceneID center)
        {
            var keepScenes = sceneRegistry.GetSceneGrid5x5(center);
            Debug.Log($"[CSL] UnloadDistantScenesCoroutine: center={center}, keepScenes={keepScenes.Count}, loaded={_loadedScenes.Count}");

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
                    unloadTasks.Add(StartCoroutine(UnloadSceneCoroutine(loaded)));
                }
                else
                {
                    Debug.Log($"[CSL] Keeping scene {loaded} (within 5x5)");
                }
            }

            Debug.Log($"[CSL] UnloadDistantScenesCoroutine: waiting for {unloadTasks.Count} unloads...");
            foreach (var task in unloadTasks)
                yield return task;

            Debug.Log($"[CSL] All unloads complete. loadedScenes={_loadedScenes.Count}");
        }

        private IEnumerator LoadSceneAsync(SceneID sceneId)
        {
            string sceneName = sceneRegistry != null ? sceneRegistry.GetSceneName(sceneId) : $"WorldScene_{sceneId.GridX}_{sceneId.GridZ}";
            Debug.Log($"[CSL] LoadSceneAsync START: {sceneName}");

            if (_loadingScenes.Contains(sceneId))
            {
                Debug.Log($"[CSL] LoadSceneAsync SKIP: {sceneId} already in _loadingScenes");
                yield break;
            }

            _loadingScenes.Add(sceneId);

            var asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (asyncOp == null)
            {
                Debug.LogError($"[CSL] FAILED to load scene: {sceneName} - NOT in Build Settings!");
                _loadingScenes.Remove(sceneId);
                yield break;
            }

            while (!asyncOp.isDone)
                yield return null;

            var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (loadedScene.isLoaded)
            {
                Debug.Log($"[CSL] LoadSceneAsync COMPLETE: {sceneName}");
            }
            else
            {
                Debug.LogError($"[CSL] VERIFY FAILED: Scene {sceneName} isLoaded=false!");
            }

            if (_loadingScenes.Contains(sceneId))
            {
                _loadedScenes.Add(sceneId);
                _loadingScenes.Remove(sceneId);
                OnSceneLoaded?.Invoke(sceneId);
            }
        }

        private IEnumerator UnloadSceneCoroutine(SceneID scene)
        {
            string sceneName = sceneRegistry != null ? sceneRegistry.GetSceneName(scene) : $"WorldScene_{scene.GridX}_{scene.GridZ}";
            Debug.Log($"[CSL] UnloadSceneCoroutine START: {sceneName}");

            if (!_loadedScenes.Contains(scene))
            {
                Debug.Log($"[CSL] UnloadSceneCoroutine SKIP: {scene} not in _loadedScenes");
                yield break;
            }

            _loadedScenes.Remove(scene);

            var asyncOp = SceneManager.UnloadSceneAsync(sceneName);

            if (asyncOp == null)
            {
                Debug.LogWarning($"[CSL] UnloadSceneAsync returned null for {sceneName}");
            }
            else
            {
                while (!asyncOp.isDone)
                    yield return null;
            }

            Debug.Log($"[CSL] UnloadSceneCoroutine COMPLETE: {scene}");
            OnSceneUnloaded?.Invoke(scene);
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