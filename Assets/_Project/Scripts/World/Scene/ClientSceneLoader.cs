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
            if (logSingleton) Debug.Log($"[CSL] Awake: START, _instance={(object)(_instance != null ? _instance.gameObject : null)?.GetEntityId()}, this={gameObject.GetEntityId()}");

            if (_instance != null && _instance != this)
            {
                if (logSingleton) Debug.LogWarning($"[CSL] DUPLICATE! Destroying this={gameObject.GetEntityId()}, keeping instance={_instance.gameObject.GetEntityId()}");
                Destroy(gameObject);
                return;
            }

            if (_instance == this)
            {
                if (logSingleton) Debug.Log($"[CSL] Awake: ALREADY SET, possible duplicate OnDestroy issue! _instance={_instance?.gameObject?.GetEntityId()}");
            }

            _instance = this;
            if (logSingleton) Debug.Log($"[CSL] Awake: Singleton set, instanceId={gameObject.GetEntityId()}");
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
            if (logSingleton) Debug.Log($"[CSL] OnDestroy: instanceId={gameObject.GetEntityId()}, _instance==this: {_instance == this}");
            if (_instance == this)
                _instance = null;
        }
        #endregion

        #region Configuration
        [SerializeField] private Transform playerTransform;
        [SerializeField] private SceneRegistry sceneRegistry;
        [SerializeField] private bool showDebugLogs = false;

        [Header("Debug Logging")]
        [SerializeField] private bool logSingleton = false;
        [SerializeField] private bool logUpdate = false;
        [SerializeField] private bool logPlayerFinding = false;
        [SerializeField] private bool logSceneLoading = false;

        [Header("Boundary-based Loading")]
        [Tooltip("Distance from boundary before preloading next scene")]
        [SerializeField] private float preloadDistance = 10000f;
        [Tooltip("Distance from boundary before unloading distant scenes")]
        [SerializeField] private float unloadDistance = 10000f;
        [Tooltip("Maximum scenes to keep loaded at once")]
        [SerializeField] private int maxLoadedScenes = 4;
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


        private const float SCENE_SIZE = 79999f;
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
            if (Time.frameCount % 120 == 0 && logUpdate)
            {
                Debug.Log($"[CSL] Update: this={gameObject.GetEntityId()}, _instance={_instance?.gameObject?.GetEntityId()}, _currentScene={_currentScene}, loaded={_loadedScenes.Count}");
            }

            if (playerTransform == null)
            {
                if (_isInitialized && Time.frameCount % 120 == 0 && logUpdate)
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
            SceneID playerScene = SceneID.FromWorldPosition(pos);

            if (_currentScene.GridX < 0)
            {
                if (_isLoadingInitialScene)
                {
                    if (Time.frameCount % 120 == 0 && logUpdate)
                        Debug.Log($"[CSL] Update: _currentScene.GridX < 0, waiting...");
                    return;
                }
                if (logSingleton) Debug.LogWarning($"[CSL] Update: _currentScene.GridX < 0! Triggering emergency load...");
                _isLoadingInitialScene = true;
                StartCoroutine(LoadSceneBoundaryBased(playerScene));
                return;
            }

            if (!playerScene.Equals(_currentScene))
            {
                if (logUpdate) Debug.Log($"[CSL] ★ SCENE MISMATCH! {_currentScene} -> {playerScene} at pos {pos}");
                if (sceneRegistry != null && sceneRegistry.IsValid(playerScene))
                {
                    StartCoroutine(LoadSceneBoundaryBased(playerScene));
                    OnSceneTransition?.Invoke(_currentScene, playerScene);
                }
                else
                {
                    if (pos.z >= sceneRegistry.GridRows * SCENE_SIZE || pos.x >= sceneRegistry.GridColumns * SCENE_SIZE)
                    {
                        if (logUpdate) Debug.LogWarning($"[CSL] Player is OUTSIDE world bounds! pos={pos}");
                    }
                    else
                    {
                        if (logUpdate) Debug.LogError($"[CSL] Target scene {playerScene} is INVALID!");
                    }
                }
                return;
            }

            Vector3 localPos = playerScene.ToLocalPosition(pos);
            bool approachingBoundaryX = localPos.x > (SCENE_SIZE - preloadDistance) || localPos.x < preloadDistance;
            bool approachingBoundaryZ = localPos.z > (SCENE_SIZE - preloadDistance) || localPos.z < preloadDistance;

            if (approachingBoundaryX || approachingBoundaryZ)
            {
                SceneID preloadScene = CalculatePreloadScene(playerScene, localPos);
                if (preloadScene.IsValid && !_loadedScenes.Contains(preloadScene) && !_loadingScenes.Contains(preloadScene))
                {
                    if (logUpdate) Debug.Log($"[CSL] Approaching boundary, preloading {preloadScene}...");
                    StartCoroutine(LoadSceneAsync(preloadScene));
                }
            }

ManageLoadedScenesCount();
            CheckDistanceBasedUnload(playerScene, pos);
        }

        private void CheckDistanceBasedUnload(SceneID playerScene, Vector3 playerPos)
        {
            var toUnload = new List<SceneID>();

            foreach (var scene in _loadedScenes)
            {
                if (scene.Equals(playerScene))
                    continue;

                Vector3 sceneCenter = scene.WorldCenter;
                float dist = Vector3.Distance(new Vector3(playerPos.x, 0, playerPos.z), new Vector3(sceneCenter.x, 0, sceneCenter.z));

                if (dist > unloadDistance)
                {
                    toUnload.Add(scene);
                }
            }

            foreach (var scene in toUnload)
            {
                if (logUpdate) Debug.Log($"[CSL] Distance-based unload: {scene} (dist > {unloadDistance})");
                StartCoroutine(UnloadSceneCoroutine(scene));
            }
        }

        private SceneID CalculatePreloadScene(SceneID current, Vector3 localPos)
        {
            if (localPos.x > SCENE_SIZE - preloadDistance && current.GridX < sceneRegistry.GridColumns - 1)
                return current.GetNeighbor(Direction.X_plus);
            if (localPos.x < preloadDistance && current.GridX > 0)
                return current.GetNeighbor(Direction.X_minus);
            if (localPos.z > SCENE_SIZE - preloadDistance && current.GridZ < sceneRegistry.GridRows - 1)
                return current.GetNeighbor(Direction.Z_plus);
            if (localPos.z < preloadDistance && current.GridZ > 0)
                return current.GetNeighbor(Direction.Z_minus);
            return current;
        }

        private void ManageLoadedScenesCount()
        {
            if (_loadedScenes.Count <= maxLoadedScenes)
                return;

            var toUnload = new List<SceneID>();
            foreach (var scene in _loadedScenes)
            {
                if (scene.Equals(_currentScene))
                    continue;

                int distance = Mathf.Abs(scene.GridX - _currentScene.GridX) + Mathf.Abs(scene.GridZ - _currentScene.GridZ);
                if (distance > 2)
                    toUnload.Add(scene);
            }

            while (_loadedScenes.Count > maxLoadedScenes && toUnload.Count > 0)
            {
                var sceneToRemove = toUnload[0];
                toUnload.RemoveAt(0);
                if (_loadedScenes.Contains(sceneToRemove))
                {
                    if (logUpdate) Debug.Log($"[CSL] Unloading {sceneToRemove} (over max {maxLoadedScenes})");
                    StartCoroutine(UnloadSceneCoroutine(sceneToRemove));
                }
            }
        }
        #endregion

        #region Private Methods
        private void OnPlayerConnected(ulong clientId)
        {
            if (logPlayerFinding) Debug.Log($"[CSL] OnPlayerConnected: clientId={clientId}");
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
                    // FIX (2026-06-04, INVESTIGATION_GHOST_PLAYER_CLONE.md): сначала проверяем
                    // НАСТОЯЩЕГО локального игрока (NGO PlayerObject). Иначе tag-based поиск ниже
                    // первым матчит scene-placed PlayerSpawner (тоже имеет tag "Player") →
                    // телепортируем не того → реальный NetworkPlayer(Clone) остаётся на (0,3,0) и падает.
                    var realPlayer = FindRealLocalPlayerGameObject();
                    if (realPlayer != null)
                    {
                        playerTransform = realPlayer.transform;
                        _isInitialized = true;
                        if (logPlayerFinding) Debug.Log($"[CSL] ★ UpdatePlayerTransform SUCCESS (RealPlayer/PlayerObject): {realPlayer.name} at {realPlayer.transform.position}");
                        yield break;
                    }

                    var playerByTag = GameObject.FindGameObjectWithTag("Player");
                    if (playerByTag != null && playerByTag.name.Contains("PlayerSpawner") == false)
                    {
                        // FIX: tag-based поиск может вернуть scene-placed PlayerSpawner-пустышку
                        // (она тоже имеет tag "Player"). Проверяем имя и пропускаем.
                        playerTransform = playerByTag.transform;
                        _isInitialized = true;
                        if (logPlayerFinding) Debug.Log($"[CSL] ★ UpdatePlayerTransform SUCCESS (PlayerTag): {playerByTag.name} at {playerByTag.transform.position}");
                        yield break;
                    }

                    var networkObjects = FindObjectsByType<NetworkObject>();

                    foreach (var netObj in networkObjects)
                    {
                        if (netObj.name.Contains("PlayerSpawner") || netObj.name.Contains("Spawner"))
                            continue;

                        if (netObj.IsOwner && netObj.IsPlayerObject) // FIX: добавил IsPlayerObject, см. doc
                        {
                            var networkPlayer = netObj.GetComponent<ProjectC.Player.NetworkPlayer>();
                            if (networkPlayer != null)
                            {
                                playerTransform = netObj.transform;
                                _isInitialized = true;
                                if (logPlayerFinding) Debug.Log($"[CSL] ★ UpdatePlayerTransform SUCCESS (NetworkPlayer): {netObj.name} at {netObj.transform.position}");
                                yield break;
                            }
                        }
                    }
                }
            }
            if (logPlayerFinding) Debug.LogWarning("[CSL] Could not find player to update playerTransform");
        }

        private IEnumerator WaitForNetworkAndPlayer()
        {
            yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
            FindLocalPlayer();
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            if (logPlayerFinding) Debug.Log($"[CSL] OnClientConnectedCallback: clientId={clientId}");

            if (NetworkManager.Singleton != null &&
                clientId == NetworkManager.Singleton.LocalClientId &&
                _currentScene.GridX < 0 &&
                !_isLoadingInitialScene)
            {
                if (logPlayerFinding) Debug.Log($"[CSL] Triggering auto-load for client {clientId}");
                _isLoadingInitialScene = true;
                StartCoroutine(AutoLoadInitialSceneCoroutine());
            }
        }

        private IEnumerator AutoLoadInitialSceneCoroutine()
        {
            if (logPlayerFinding) Debug.Log("[CSL] AutoLoadInitialSceneCoroutine STARTING...");
            if (logPlayerFinding) Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: _currentScene={_currentScene}, _isLoadingInitialScene={_isLoadingInitialScene}");
            yield return new WaitForSeconds(0.5f);

            if (logPlayerFinding) Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: AFTER WAIT _currentScene={_currentScene}, _isLoadingInitialScene={_isLoadingInitialScene}");

            if (_currentScene.GridX >= 0)
            {
                if (logPlayerFinding) Debug.Log($"[CSL] _currentScene already set to {_currentScene}, skipping");
                _isLoadingInitialScene = false;
                yield break;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                SceneID initialScene = new SceneID(0, 0);
                if (logPlayerFinding) Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: Loading initial scene {initialScene}");

                yield return LoadSceneWithNeighborsCoroutine(initialScene);

                if (logPlayerFinding) Debug.Log($"[CSL] AutoLoadInitialSceneCoroutine: Load complete, _currentScene={_currentScene}");

                if (playerTransform != null)
                {
                    Vector3 worldSpawnPos = initialScene.WorldCenter + new Vector3(0, 3000, 0);
                    _teleportTarget = worldSpawnPos;
                    _lastTeleportTime = Time.time;
                    if (logPlayerFinding) Debug.Log($"[CSL] Teleporting player from {playerTransform.position} to {worldSpawnPos}");

                    if (playerTransform.name.Contains("PlayerSpawner"))
                    {
                        // FIX (2026-06-04, INVESTIGATION_GHOST_PLAYER_CLONE.md):
                        // С этой правкой playerTransform НЕ должен указывать на scene-placed
                        // PlayerSpawner (UpdatePlayerTransformAfterSpawn теперь предпочитает
                        // NGO PlayerObject). Но оставляем branch как defense-in-depth —
                        // с усиленным фильтром IsPlayerObject во внутреннем цикле, чтобы
                        // даже в race condition телепорт уходил на НАСТОЯЩЕГО игрока.
                        if (logPlayerFinding) Debug.Log("[CSL] PlayerSpawner detected - finding actual NetworkPlayer for teleport");
                        var networkPlayers = FindObjectsByType<ProjectC.Player.NetworkPlayer>();
                        foreach (var np in networkPlayers)
                        {
                            if (np.IsOwner && np.GetComponent<NetworkObject>()?.IsPlayerObject == true) // FIX
                            {
                                np.TeleportServerRpc(worldSpawnPos);
                                if (logPlayerFinding) Debug.Log($"[CSL] Teleported via NetworkPlayer: {np.name} to {worldSpawnPos}");
                                break;
                            }
                        }
                    }
                    else
                    {
                        var networkPlayer = playerTransform.GetComponent<ProjectC.Player.NetworkPlayer>();
                        if (networkPlayer != null)
                        {
                            if (logPlayerFinding) Debug.Log("[CSL] Using NetworkPlayer.TeleportServerRpc for proper network sync");
                            networkPlayer.TeleportServerRpc(worldSpawnPos);
                        }
                        else
                        {
                            playerTransform.position = worldSpawnPos;
                        }
                    }

                    _lastPlayerPos = playerTransform.position;
                    if (logPlayerFinding) Debug.Log($"[CSL] AFTER TELEPORT: playerTransform.position.z={playerTransform.position.z}, expected z={worldSpawnPos.z}");
                }
                else
                {
                    if (logPlayerFinding) Debug.LogError("[CSL] CRITICAL: playerTransform is NULL! Cannot teleport!");
                }
            }

            _isLoadingInitialScene = false;
            if (logPlayerFinding) Debug.Log("[CSL] AutoLoadInitialSceneCoroutine COMPLETE");
        }

        private void FindLocalPlayer()
        {
            if (logPlayerFinding) Debug.Log("[CSL] FindLocalPlayer() called");

            // FIX (2026-06-04): сначала пробуем source of truth — NGO PlayerObject.
            // Иначе tag-based поиск ниже первым матчит scene-placed PlayerSpawner
            // (тоже имеет tag "Player") → "фантом-клон" (см. INVESTIGATION_GHOST_PLAYER_CLONE.md).
            var realPlayer = FindRealLocalPlayerGameObject();
            if (realPlayer != null)
            {
                playerTransform = realPlayer.transform;
                _isInitialized = true;
                if (logPlayerFinding) Debug.Log($"[CSL] Found REAL player (NGO PlayerObject): {realPlayer.name} at {realPlayer.transform.position}");
                return;
            }

            var playerByTag = GameObject.FindGameObjectWithTag("Player");
            if (logPlayerFinding) Debug.Log($"[CSL] FindGameObjectWithTag('Player') = {playerByTag?.name ?? "NULL"}");
            if (playerByTag != null && playerByTag.name.Contains("PlayerSpawner") == false)
            {
                // FIX: пропускаем scene-placed PlayerSpawner, у него тоже tag "Player".
                playerTransform = playerByTag.transform;
                _isInitialized = true;
                if (logPlayerFinding) Debug.Log($"[CSL] Found PLAYER by tag: {playerByTag.name} at {playerByTag.transform.position}");
                return;
            }
            else if (playerByTag != null)
            {
                if (logPlayerFinding) Debug.LogWarning($"[CSL] FindGameObjectWithTag('Player') matched scene-placed '{playerByTag.name}' — skipping, NGO PlayerObject not yet available, will retry in WaitForPlayer");
            }
            else
            {
                if (logPlayerFinding) Debug.LogWarning("[CSL] FindGameObjectWithTag('Player') returned NULL! Searching alternatives...");
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var networkPlayers = FindObjectsByType<ProjectC.Player.NetworkPlayer>();
                if (logPlayerFinding) Debug.Log($"[CSL] FindLocalPlayer: searching {networkPlayers.Length} NetworkPlayers");

                foreach (var networkPlayer in networkPlayers)
                {
                    // FIX: фильтр IsPlayerObject обязателен, иначе первым в списке
                    // (по instanceID) идёт scene-placed PlayerSpawner-пустышка.
                    if (networkPlayer.IsOwner && networkPlayer.GetComponent<NetworkObject>()?.IsPlayerObject == true)
                    {
                        playerTransform = networkPlayer.transform;
                        _isInitialized = true;
                        if (logPlayerFinding) Debug.Log($"[CSL] Found OWNER NetworkPlayer: {networkPlayer.name} at {networkPlayer.transform.position}");
                        return;
                    }
                }

                var networkObjects = FindObjectsByType<NetworkObject>();
                if (logPlayerFinding) Debug.Log($"[CSL] FindLocalPlayer: searching {networkObjects.Length} NetworkObjects (Player tag and NetworkPlayer failed)");

                foreach (var netObj in networkObjects)
                {
                    if (netObj.name.Contains("PlayerSpawner") || netObj.name.Contains("Spawner"))
                        continue;

                    if (netObj.IsOwner && netObj.IsPlayerObject) // FIX: добавил IsPlayerObject
                    {
                        var networkPlayer = netObj.GetComponent<ProjectC.Player.NetworkPlayer>();
                        if (networkPlayer != null)
                        {
                            playerTransform = netObj.transform;
                            _isInitialized = true;
                            if (logPlayerFinding) Debug.Log($"[CSL] Found NetworkPlayer (via NetworkObject): {netObj.name} at {netObj.transform.position}");
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

                // FIX: предпочитаем NGO PlayerObject (real player), не scene-placed.
                var realPlayer = FindRealLocalPlayerGameObject();
                if (realPlayer != null)
                {
                    playerTransform = realPlayer.transform;
                    _isInitialized = true;
                    if (logPlayerFinding) Debug.Log($"[CSL] ★ WaitForPlayer SUCCESS (via NGO PlayerObject): {realPlayer.name}");
                    yield break;
                }

                var playerByTag = GameObject.FindGameObjectWithTag("Player");
                if (playerByTag != null && playerByTag.name.Contains("PlayerSpawner") == false)
                {
                    playerTransform = playerByTag.transform;
                    _isInitialized = true;
                    if (logPlayerFinding) Debug.Log($"[CSL] ★ WaitForPlayer SUCCESS (via Player tag): {playerByTag.name}");
                    yield break;
                }

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    var networkPlayers = FindObjectsByType<ProjectC.Player.NetworkPlayer>();
                    foreach (var networkPlayer in networkPlayers)
                    {
                        if (networkPlayer.IsOwner && networkPlayer.GetComponent<NetworkObject>()?.IsPlayerObject == true) // FIX
                        {
                            playerTransform = networkPlayer.transform;
                            _isInitialized = true;
                            if (logPlayerFinding) Debug.Log($"[CSL] ★ WaitForPlayer SUCCESS: {networkPlayer.name}");
                            yield break;
                        }
                    }

                    var networkObjects = FindObjectsByType<NetworkObject>();
                    foreach (var netObj in networkObjects)
                    {
                        if (netObj.name.Contains("PlayerSpawner") || netObj.name.Contains("Spawner"))
                            continue;

                        if (netObj.IsOwner && netObj.IsPlayerObject) // FIX
                        {
                            var networkPlayer = netObj.GetComponent<ProjectC.Player.NetworkPlayer>();
                            if (networkPlayer != null)
                            {
                                playerTransform = netObj.transform;
                                _isInitialized = true;
                                if (logPlayerFinding) Debug.Log($"[CSL] ★ WaitForPlayer SUCCESS (via NetObj): {netObj.name}");
                                yield break;
                            }
                        }
                    }
                }

                if (retry % 4 == 0 && logPlayerFinding)
                    Debug.Log($"[CSL] WaitForPlayer: attempt {retry}/20");
            }

            if (logPlayerFinding) Debug.LogError("[CSL] Failed to find NetworkPlayer after 10 seconds!");
        }
        #endregion

        #region Public API
        /// <summary>
        /// Возвращает GameObject НАСТОЯЩЕГО локального игрока (auto-spawned NetworkPlayer(Clone)
        /// из <c>NetworkConfig.PlayerPrefab</c>), или null если ещё не заспавнен.
        ///
        /// НЕ возвращает scene-placed не-player NetworkObject'ы (например <c>PlayerSpawner</c> в
        /// BootstrapScene), у которых на хосте <c>IsOwner==true</c> (server-owned, OwnerClientId=0
        /// = LocalClientId) — это footgun NGO 2.x, приводящий к "фантом-клонам" и телепорту
        /// не того объекта. Подробнее см. <c>docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md</c>.
        ///
        /// Использует source of truth: <c>NetworkManager.ConnectedClients[LocalClientId].PlayerObject</c>.
        /// </summary>
        private GameObject FindRealLocalPlayerGameObject()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return null;

            var localClientId = NetworkManager.Singleton.LocalClientId;
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(localClientId, out var cc)
                && cc?.PlayerObject != null
                && cc.PlayerObject.IsSpawned
                && cc.PlayerObject.IsPlayerObject)
            {
                return cc.PlayerObject.gameObject;
            }
            return null;
        }

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
            if (logSceneLoading) Debug.Log($"[CSL] LoadSceneCoroutine({targetScene}) called");

            if (_loadedScenes.Contains(targetScene) || _loadingScenes.Contains(targetScene))
            {
                if (logSceneLoading) Debug.Log($"[CSL] Scene {targetScene} already loaded or loading, skipping");
                yield break;
            }

            if (sceneRegistry != null && !sceneRegistry.IsValid(targetScene))
            {
                if (logSceneLoading) Debug.LogWarning($"[CSL] Invalid target scene: {targetScene}, clamping");
                targetScene = new SceneID(
                    Mathf.Clamp(targetScene.GridX, 0, Mathf.Max(0, sceneRegistry.GridColumns - 1)),
                    Mathf.Clamp(targetScene.GridZ, 0, Mathf.Max(0, sceneRegistry.GridRows - 1))
                );
            }

            _loadingScenes.Add(targetScene);

            string sceneName = sceneRegistry != null ? sceneRegistry.GetSceneName(targetScene) : $"WorldScene_{targetScene.GridX}_{targetScene.GridZ}";
            if (logSceneLoading) Debug.Log($"[CSL] LoadSceneCoroutine: Loading {sceneName}");

            yield return LoadSceneAsync(targetScene);

            _currentScene = targetScene;

            if (playerTransform != null)
            {
                Vector3 worldPos = targetScene.ToWorldPosition(localSpawnPos);
                playerTransform.position = worldPos;
            }
        }

        private IEnumerator LoadSceneBoundaryBased(SceneID playerScene)
        {
            _isTransitioning = true;
            if (logSceneLoading) Debug.Log($"[CSL] LoadSceneBoundaryBased START: playerScene={playerScene}, _currentScene={_currentScene}");

            if (!_loadedScenes.Contains(playerScene))
            {
                yield return LoadSceneAsync(playerScene);
            }

            _currentScene = playerScene;
            if (logSceneLoading) Debug.Log($"[CSL] Set _currentScene = {playerScene}");

            UnloadDistantScenes(playerScene);

            _isTransitioning = false;
            _isLoadingInitialScene = false;
            if (logSceneLoading) Debug.Log($"[CSL] LoadSceneBoundaryBased END. loaded={_loadedScenes.Count}");
        }

        private void UnloadDistantScenes(SceneID current)
        {
            var toUnload = new List<SceneID>();
            foreach (var scene in _loadedScenes)
            {
                if (scene.Equals(current))
                    continue;

                int distance = Mathf.Abs(scene.GridX - current.GridX) + Mathf.Abs(scene.GridZ - current.GridZ);
                if (distance > 2)
                    toUnload.Add(scene);
            }

            foreach (var scene in toUnload)
            {
                if (logSceneLoading) Debug.Log($"[CSL] Unloading distant {scene}");
                StartCoroutine(UnloadSceneCoroutine(scene));
            }
        }

        private IEnumerator LoadSceneWithNeighborsCoroutine(SceneID center)
        {
            if (logSceneLoading) Debug.Log($"[CSL] LoadSceneWithNeighborsCoroutine START: center={center}, _isLoadingInitialScene={_isLoadingInitialScene}, _currentScene={_currentScene}");
            _isTransitioning = true;

            if (sceneRegistry == null)
            {
                if (logSceneLoading) Debug.LogError("[CSL] sceneRegistry is NULL!");
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
                        if (logSceneLoading) Debug.Log($"[CSL] Starting LoadSceneAsync for {sceneId}");
                        loadTasks.Add(StartCoroutine(LoadSceneAsync(sceneId)));
                    }
                }
                else
                {
                    if (logSceneLoading) Debug.Log($"[CSL] SKIPPING {sceneId} - already in _loadedScenes or _loadingScenes");
                }
            }

            if (logSceneLoading) Debug.Log($"[CSL] Waiting for {loadTasks.Count} loads to complete...");
            foreach (var task in loadTasks)
                yield return task;

            if (logSceneLoading) Debug.Log($"[CSL] All loads complete. loadedScenes={_loadedScenes.Count}");

            _currentScene = center;
            if (logSceneLoading) Debug.Log($"[CSL] Set _currentScene = {center}");

            _isTransitioning = false;
            _isLoadingInitialScene = false;
            if (logSceneLoading) Debug.Log($"[CSL] LoadSceneWithNeighborsCoroutine END. Final loadedScenes={_loadedScenes.Count}");

            var loadedInSceneManager = UnityEngine.SceneManagement.SceneManager.GetSceneByName("WorldScene_0_0");
            if (loadedInSceneManager.isLoaded)
            {
                var roots = loadedInSceneManager.GetRootGameObjects();
                if (logSceneLoading) Debug.Log($"[CSL] WorldScene_0_0 has {roots.Length} root objects:");
                foreach (var r in roots)
                {
                    if (logSceneLoading) Debug.Log($"[CSL]   - {r.name}, active={r.activeSelf}, pos={r.transform.position}");
                }
            }
            else
            {
                if (logSceneLoading) Debug.LogWarning("[CSL] WorldScene_0_0 is NOT loaded in SceneManager!");
            }
        }

        private IEnumerator UnloadDistantScenesCoroutine(SceneID center)
        {
            var keepScenes = sceneRegistry.GetSceneGrid5x5(center);
            if (logSceneLoading) Debug.Log($"[CSL] UnloadDistantScenesCoroutine: center={center}, keepScenes={keepScenes.Count}, loaded={_loadedScenes.Count}");

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
                    if (logSceneLoading) Debug.Log($"[CSL] Queuing unload for distant scene {loaded}");
                    unloadTasks.Add(StartCoroutine(UnloadSceneCoroutine(loaded)));
                }
                else
                {
                    if (logSceneLoading) Debug.Log($"[CSL] Keeping scene {loaded} (within 5x5)");
                }
            }

            if (logSceneLoading) Debug.Log($"[CSL] UnloadDistantScenesCoroutine: waiting for {unloadTasks.Count} unloads...");
            foreach (var task in unloadTasks)
                yield return task;

            if (logSceneLoading) Debug.Log($"[CSL] All unloads complete. loadedScenes={_loadedScenes.Count}");
        }

        private IEnumerator LoadSceneAsync(SceneID sceneId)
        {
            string sceneName = sceneRegistry != null ? sceneRegistry.GetSceneName(sceneId) : $"WorldScene_{sceneId.GridX}_{sceneId.GridZ}";
            if (logSceneLoading) Debug.Log($"[CSL] LoadSceneAsync START: {sceneName}");

            if (_loadingScenes.Contains(sceneId))
            {
                if (logSceneLoading) Debug.Log($"[CSL] LoadSceneAsync SKIP: {sceneId} already in _loadingScenes");
                yield break;
            }

            _loadingScenes.Add(sceneId);

            var asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (asyncOp == null)
            {
                if (logSceneLoading) Debug.LogError($"[CSL] FAILED to load scene: {sceneName} - NOT in Build Settings!");
                _loadingScenes.Remove(sceneId);
                yield break;
            }

            while (!asyncOp.isDone)
                yield return null;

            var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (loadedScene.isLoaded)
            {
                if (logSceneLoading) Debug.Log($"[CSL] LoadSceneAsync COMPLETE: {sceneName}");
            }
            else
            {
                if (logSceneLoading) Debug.LogError($"[CSL] VERIFY FAILED: Scene {sceneName} isLoaded=false!");
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
            if (logSceneLoading) Debug.Log($"[CSL] UnloadSceneCoroutine START: {sceneName}");

            if (!_loadedScenes.Contains(scene))
            {
                if (logSceneLoading) Debug.Log($"[CSL] UnloadSceneCoroutine SKIP: {scene} not in _loadedScenes");
                yield break;
            }

            _loadedScenes.Remove(scene);

            var asyncOp = SceneManager.UnloadSceneAsync(sceneName);

            if (asyncOp == null)
            {
                if (logSceneLoading) Debug.LogWarning($"[CSL] UnloadSceneAsync returned null for {sceneName}");
            }
            else
            {
                while (!asyncOp.isDone)
                    yield return null;
            }

            if (logSceneLoading) Debug.Log($"[CSL] UnloadSceneCoroutine COMPLETE: {scene}");
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