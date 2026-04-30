using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.Scene
{
    /// <summary>
    /// Server-side менеджер сцен.
    /// Отслеживает позицию каждого клиента и управляет переходами между сценами.
    /// Не управляет загрузкой/выгрузкой сцен на сервере - только координирует клиентов.
    /// </summary>
    public class ServerSceneManager : NetworkBehaviour
    {
        #region Configuration

        [Header("Scene Registry")]
        [Tooltip("ScriptableObject с данными о сценах мира")]
        [SerializeField] private SceneRegistry sceneRegistry;

        [Header("Scene Loading Settings")]
        [Tooltip("Интервал проверки позиции игрока (секунды)")]
        [SerializeField] private float updateInterval = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        #endregion

        #region Private State

        private readonly Dictionary<ulong, SceneID> _clientSceneMap = new Dictionary<ulong, SceneID>();
        private readonly Dictionary<ulong, HashSet<SceneID>> _clientLoadedScenes = new Dictionary<ulong, HashSet<SceneID>>();
        private readonly Dictionary<ulong, Transform> _playerTransforms = new Dictionary<ulong, Transform>();
        private readonly Dictionary<ulong, float> _lastUpdateTimes = new Dictionary<ulong, float>();

        private readonly Dictionary<SceneID, List<ulong>> _sceneClients = new Dictionary<SceneID, List<ulong>>();

        /// <summary>
        /// Registry: SceneID -> NetworkObjects that belong to that scene.
        /// Used for efficient NetworkHide/NetworkShow during scene transitions.
        /// </summary>
        private readonly Dictionary<SceneID, HashSet<NetworkObject>> _sceneObjectRegistry = new Dictionary<SceneID, HashSet<NetworkObject>>();

        /// <summary>
        /// FIX-2: Track last transition times to prevent rapid re-transitions.
        /// </summary>
        private readonly Dictionary<ulong, float> _lastTransitionTimes = new Dictionary<ulong, float>();
        private const float MIN_TRANSITION_INTERVAL = 1.0f;

        #endregion

        #region Events

        public event System.Action<ulong, SceneID, SceneID> OnClientSceneTransition;

        #endregion

        #region Unity Lifecycle

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            if (sceneRegistry == null)
            {
                sceneRegistry = Resources.Load<SceneRegistry>("Scene/SceneRegistry");
                if (sceneRegistry == null)
                    sceneRegistry = Resources.Load<SceneRegistry>("SceneRegistry");
                if (sceneRegistry == null)
                {
                    Debug.LogError("[ServerSceneManager] SceneRegistry not found! Please assign or create.");
                }
            }

            LogDebug("ServerSceneManager initialized on server.");

            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer) return;

            float currentTime = Time.time;

            foreach (var kvp in _playerTransforms)
            {
                ulong clientId = kvp.Key;
                Transform playerTransform = kvp.Value;

                if (!_lastUpdateTimes.TryGetValue(clientId, out float lastTime) ||
                    currentTime - lastTime < updateInterval)
                {
                    continue;
                }

                _lastUpdateTimes[clientId] = currentTime;
                CheckSceneTransition(clientId, playerTransform.position);
            }
        }

        #endregion

        #region Server Callbacks

        private void HandleClientConnected(ulong clientId)
        {
            LogDebug($"Client connected: {clientId}");
            StartCoroutine(FindPlayerTransformCoroutine(clientId));
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            LogDebug($"Client disconnected: {clientId}");

            if (_clientSceneMap.TryGetValue(clientId, out var scene))
            {
                RemoveClientFromScene(clientId, scene);
            }

            _clientSceneMap.Remove(clientId);
            _clientLoadedScenes.Remove(clientId);
            _playerTransforms.Remove(clientId);
            _lastUpdateTimes.Remove(clientId);
        }

        private System.Collections.IEnumerator FindPlayerTransformCoroutine(ulong clientId)
        {
            yield return new WaitForSeconds(0.5f);

            var networkObjects = FindObjectsByType<NetworkObject>();
            NetworkObject playerObject = null;

            foreach (var netObj in networkObjects)
            {
                if (netObj.OwnerClientId == clientId &&
                    netObj.GetComponent<ProjectC.Player.NetworkPlayer>() != null)
                {
                    playerObject = netObj;
                    break;
                }
            }

            if (playerObject != null)
            {
                _playerTransforms[clientId] = playerObject.transform;
                _lastUpdateTimes[clientId] = Time.time;

                var initialScene = SceneID.FromWorldPosition(playerObject.transform.position);
                _clientSceneMap[clientId] = initialScene;
                _clientLoadedScenes[clientId] = new HashSet<SceneID>();
                AddClientToScene(clientId, initialScene);

                LogDebug($"Client {clientId} assigned to scene {initialScene}");

                SendInitialSceneToClient(clientId, initialScene);
            }
            else
            {
                Debug.LogWarning($"[ServerSceneManager] Could not find NetworkPlayer for client {clientId}");
            }
        }

        #endregion

        #region Scene Transition Logic

        private void CheckSceneTransition(ulong clientId, Vector3 worldPosition)
        {
            if (!_clientSceneMap.TryGetValue(clientId, out var currentScene))
                return;

            var targetScene = SceneID.FromWorldPosition(worldPosition);

            if (sceneRegistry != null && !sceneRegistry.IsValid(targetScene))
            {
                LogDebug($"Client {clientId} world position {worldPosition} maps to invalid scene {targetScene}, clamping to valid range");
                targetScene = new SceneID(
                    Mathf.Clamp(targetScene.GridX, 0, Mathf.Max(0, sceneRegistry.GridColumns - 1)),
                    Mathf.Clamp(targetScene.GridZ, 0, Mathf.Max(0, sceneRegistry.GridRows - 1))
                );
            }

            if (!targetScene.Equals(currentScene))
            {
                TransitionClient(clientId, currentScene, targetScene);
            }
        }

        private void TransitionClient(ulong clientId, SceneID from, SceneID to)
        {
            // FIX-2: Prevent rapid re-transitions
            if (_lastTransitionTimes.TryGetValue(clientId, out float lastTime))
            {
                if (Time.time - lastTime < MIN_TRANSITION_INTERVAL)
                {
                    LogDebug($"Skipping rapid transition for client {clientId} (time since last: {Time.time - lastTime:F2}s)");
                    return;
                }
            }

            LogDebug($"Client {clientId} transitioning from {from} to {to}");

            if (!from.Equals(default))
            {
                HideSceneObjectsFromClient(clientId, from);
            }

            SceneID oldScene = from;
            _clientSceneMap[clientId] = to;
            _lastTransitionTimes[clientId] = Time.time;

            RemoveClientFromScene(clientId, oldScene);
            AddClientToScene(clientId, to);

            if (_playerTransforms.TryGetValue(clientId, out var playerTransform))
            {
                Vector3 localSpawnPos = to.ToLocalPosition(playerTransform.position);
                var transitionData = new SceneTransitionData(to, localSpawnPos);
                SendSceneTransitionToClient(clientId, transitionData);
                StartCoroutine(ShowSceneObjectsAfterLoad(clientId, to));
            }

            OnClientSceneTransition?.Invoke(clientId, oldScene, to);
        }

        private System.Collections.IEnumerator ShowSceneObjectsAfterLoad(ulong clientId, SceneID scene)
        {
            yield return new WaitForSeconds(1f);
            ShowSceneObjectsToClient(clientId, scene);
        }

        private void AddClientToScene(ulong clientId, SceneID scene)
        {
            if (!_sceneClients.ContainsKey(scene))
                _sceneClients[scene] = new List<ulong>();

            if (!_sceneClients[scene].Contains(clientId))
                _sceneClients[scene].Add(clientId);
        }

        private void RemoveClientFromScene(ulong clientId, SceneID scene)
        {
            if (_sceneClients.TryGetValue(scene, out var clients))
            {
                clients.Remove(clientId);
            }
        }

        #endregion

        #region RPCs

        /// <summary>
        /// Инициализация клиента - сообщаем начальную сцену.
        /// </summary>
        private void SendInitialSceneToClient(ulong targetClientId, SceneID scene)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };

            InitializeSceneClientRpc(targetClientId, scene, clientRpcParams);
        }

        [ClientRpc]
        private void InitializeSceneClientRpc(ulong targetClientId, SceneID scene, ClientRpcParams clientRpcParams = default)
        {
            if (targetClientId != NetworkManager.Singleton.LocalClientId)
                return;

            Debug.Log($"[SSM] InitializeSceneClientRpc: targetClientId={targetClientId}, LocalClientId={NetworkManager.Singleton.LocalClientId}, scene={scene}");
            LogDebug($"[Client] Received initial scene: {scene}");

            var loader = FindAnyObjectByType<ClientSceneLoader>();
            if (loader != null)
            {
                Vector3 localSpawn = new Vector3(SceneID.SCENE_SIZE / 2f, 0, SceneID.SCENE_SIZE / 2f);
                Debug.Log($"[SSM] Calling loader.LoadScene({scene}, {localSpawn})");
                loader.LoadScene(scene, localSpawn);
            }
            else
            {
                Debug.LogWarning("[ServerSceneManager] ClientSceneLoader not found!");
            }
        }

        /// <summary>
        /// RPC на клиент для загрузки новой сцены.
        /// </summary>
        private void SendSceneTransitionToClient(ulong targetClientId, SceneTransitionData transitionData)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };

            LoadSceneTransitionClientRpc(targetClientId, transitionData, clientRpcParams);
        }

        [ClientRpc]
        private void LoadSceneTransitionClientRpc(ulong targetClientId, SceneTransitionData transitionData, ClientRpcParams clientRpcParams = default)
        {
            if (targetClientId != NetworkManager.Singleton.LocalClientId)
                return;

            Debug.Log($"[SSM] LoadSceneTransitionClientRpc: scene={transitionData.TargetScene}, localPos={transitionData.LocalPosition}");
            LogDebug($"[Client] LoadSceneTransitionClientRpc received for scene {transitionData.TargetScene}");

            var loader = FindAnyObjectByType<ClientSceneLoader>();
            if (loader != null)
            {
                Debug.Log($"[SSM] Calling loader.LoadScene({transitionData.TargetScene}, {transitionData.LocalPosition})");
                loader.LoadScene(transitionData.TargetScene, transitionData.LocalPosition);
            }
            else
            {
                Debug.LogWarning("[ServerSceneManager] ClientSceneLoader not found!");
            }
        }

        /// <summary>
        /// RPC для выгрузки сцены у клиента.
        /// </summary>
        private void SendSceneUnloadToClient(ulong targetClientId, SceneID scene)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };

            UnloadSceneClientRpc(targetClientId, scene, clientRpcParams);
        }

        [ClientRpc]
        private void UnloadSceneClientRpc(ulong targetClientId, SceneID scene, ClientRpcParams clientRpcParams = default)
        {
            if (targetClientId != NetworkManager.Singleton.LocalClientId)
                return;

            LogDebug($"[Client] UnloadSceneClientRpc for scene {scene}");

            var loader = FindAnyObjectByType<ClientSceneLoader>();
            if (loader != null)
            {
                loader.UnloadScene(scene);
            }
        }

        #endregion

        #region Public API

        public SceneID GetClientScene(ulong clientId)
        {
            return _clientSceneMap.GetValueOrDefault(clientId, default);
        }

        public bool IsClientInScene(ulong clientId, SceneID scene)
        {
            return _clientSceneMap.TryGetValue(clientId, out var clientScene) && clientScene.Equals(scene);
        }

        public IEnumerable<SceneID> GetClientLoadedScenes(ulong clientId)
        {
            if (_clientLoadedScenes.TryGetValue(clientId, out var scenes))
                return scenes;
            return System.Linq.Enumerable.Empty<SceneID>();
        }

        public int GetPlayerCountInScene(SceneID scene)
        {
            if (_sceneClients.TryGetValue(scene, out var clients))
                return clients.Count;
            return 0;
        }

        public SceneRegistry GetSceneRegistry() => sceneRegistry;

        #endregion

        #region Scene Object Registry

        /// <summary>
        /// Register a NetworkObject as belonging to a scene.
        /// Used for efficient NetworkHide/NetworkShow during scene transitions.
        /// </summary>
        public void RegisterSceneObject(SceneID scene, NetworkObject networkObject)
        {
            if (networkObject == null) return;

            if (!_sceneObjectRegistry.ContainsKey(scene))
                _sceneObjectRegistry[scene] = new HashSet<NetworkObject>();

            _sceneObjectRegistry[scene].Add(networkObject);
            LogDebug($"Registered object {networkObject.name} to scene {scene}");
        }

        /// <summary>
        /// Unregister a NetworkObject from its scene.
        /// </summary>
        public void UnregisterSceneObject(SceneID scene, NetworkObject networkObject)
        {
            if (networkObject == null) return;

            if (_sceneObjectRegistry.TryGetValue(scene, out var objects))
            {
                objects.Remove(networkObject);
                LogDebug($"Unregistered object {networkObject.name} from scene {scene}");
            }
        }

        /// <summary>
        /// Hide all scene objects from a specific client.
        /// Called when client leaves a scene.
        /// </summary>
        public void HideSceneObjectsFromClient(ulong clientId, SceneID scene)
        {
            if (!_sceneObjectRegistry.TryGetValue(scene, out var objects))
                return;

            foreach (var obj in objects)
            {
                if (obj != null && obj.IsSpawned)
                {
                    obj.NetworkHide(clientId);
                    LogDebug($"Hidden object {obj.name} from client {clientId}");
                }
            }
        }

        /// <summary>
        /// Show all scene objects to a specific client.
        /// Called when client enters a scene (after scene load confirmation).
        /// </summary>
        public void ShowSceneObjectsToClient(ulong clientId, SceneID scene)
        {
            if (!_sceneObjectRegistry.TryGetValue(scene, out var objects))
                return;

            foreach (var obj in objects)
            {
                if (obj != null && obj.IsSpawned)
                {
                    obj.NetworkShow(clientId);
                    LogDebug($"Shown object {obj.name} to client {clientId}");
                }
            }
        }

        /// <summary>
        /// Get all registered objects in a scene.
        /// </summary>
        public IEnumerable<NetworkObject> GetSceneObjects(SceneID scene)
        {
            if (_sceneObjectRegistry.TryGetValue(scene, out var objects))
                return objects;
            return System.Linq.Enumerable.Empty<NetworkObject>();
        }

        #endregion

        #region Debug

        private void LogDebug(string message)
        {
            if (showDebugLogs)
                Debug.Log($"[ServerSceneManager] {message}");
        }

        #endregion
    }
}