using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// Server-side компонент для отслеживания позиции каждого игрока в чанках.
    /// Управляет загрузкой/выгрузкой чанков для каждого клиента индивидуально.
    /// 
    /// ВАЖНО: Все решения о загрузке чанков принимаются СЕРВЕРОМ.
    /// Клиенты получают RPC команды и выполняют загрузку.
    /// </summary>
    public class PlayerChunkTracker : NetworkBehaviour
    {
        #region Configuration
        
        [Header("Streaming Settings")]
        [Tooltip("Радиус загрузки чанков для каждого клиента (в чанках)")]
        [SerializeField] private int loadRadius = 2;
        
        [Tooltip("Радиус выгрузки чанков (больше чем loadRadius для hysteresis)")]
        [SerializeField] private int unloadRadius = 3;
        
        [Tooltip("Интервал проверки позиции игрока (секунды)")]
        [SerializeField] private float updateInterval = 0.5f;
        
        [Header("Debug")]
        [Tooltip("Показывать логи")]
        [SerializeField] private bool showDebugLogs = false;
        
        #endregion
        
        #region Private State
        
        /// <summary>
        /// ClientId → текущий чанк игрока
        /// </summary>
        private readonly Dictionary<ulong, ChunkId> _playerChunks = new Dictionary<ulong, ChunkId>();
        
        /// <summary>
        /// ClientId → загруженные для него чанки
        /// </summary>
        private readonly Dictionary<ulong, HashSet<ChunkId>> _clientLoadedChunks = new Dictionary<ulong, HashSet<ChunkId>>();
        
        /// <summary>
        /// Кэш NetworkPlayer компонентов
        /// </summary>
        private readonly Dictionary<ulong, Transform> _playerTransforms = new Dictionary<ulong, Transform>();
        
        /// <summary>
        /// Время последнего обновления для каждого клиента
        /// </summary>
        private readonly Dictionary<ulong, float> _lastUpdateTimes = new Dictionary<ulong, float>();
        
        /// <summary>
        /// Ссылка на WorldChunkManager
        /// </summary>
        private WorldChunkManager _chunkManager;
        
        #endregion
        
        #region Unity Lifecycle
        
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }
            
            // Подписка на события подключения/отключения
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // Найти WorldChunkManager
            _chunkManager = FindAnyObjectByType<WorldChunkManager>();
            if (_chunkManager == null)
            {
                Debug.LogError("[PlayerChunkTracker] WorldChunkManager не найден! Streaming disabled.");
                enabled = false;
                return;
            }
            
            LogDebug("PlayerChunkTracker initialized on server.");
            
            base.OnNetworkSpawn();
        }
        
        public override void OnNetworkDespawn()
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            
            base.OnNetworkDespawn();
        }
        
        private void Update()
        {
            if (!IsServer || _chunkManager == null) return;
            
            float currentTime = Time.time;
            
            // Обновляем позицию каждого игрока
            foreach (var kvp in _playerTransforms)
            {
                ulong clientId = kvp.Key;
                Transform playerTransform = kvp.Value;
                
                // Проверяем интервал обновления
                if (!_lastUpdateTimes.TryGetValue(clientId, out float lastTime) ||
                    currentTime - lastTime < updateInterval)
                {
                    continue;
                }
                
                _lastUpdateTimes[clientId] = currentTime;
                
                // Обновляем чанк игрока
                UpdatePlayerChunk(clientId, playerTransform.position);
            }
        }
        
        #endregion
        
        #region Server Callbacks
        
        private void OnClientConnected(ulong clientId)
        {
            // Найти NetworkPlayer компонент
            StartCoroutine(FindPlayerTransformCoroutine(clientId));
        }
        
        private System.Collections.IEnumerator FindPlayerTransformCoroutine(ulong clientId)
        {
            // Ждём спавна NetworkObject игрока
            yield return new WaitForSeconds(0.5f);
            
            // Ищем объект игрока по OwnerClientId
            var networkObjects = FindObjectsByType<NetworkObject>();
            NetworkObject playerObject = null;
            
            foreach (var netObj in networkObjects)
            {
                if (netObj.OwnerClientId == clientId && 
                    netObj.GetComponent<Player.NetworkPlayer>() != null)
                {
                    playerObject = netObj;
                    break;
                }
            }
            
            if (playerObject != null)
            {
                _playerTransforms[clientId] = playerObject.transform;
                _lastUpdateTimes[clientId] = Time.time;
                
                // Инициализируем загруженные чанки для клиента
                _clientLoadedChunks[clientId] = new HashSet<ChunkId>();
                
                // Загружаем начальные чанки
                Vector3 position = playerObject.transform.position;
                ChunkId initialChunk = _chunkManager.GetChunkAtPosition(position);
                _playerChunks[clientId] = initialChunk;
                
                LoadChunksForClient(clientId, position);
                
                LogDebug($"Client {clientId} connected at chunk {initialChunk}");
            }
            else
            {
                LogDebug($"Client {clientId} NetworkPlayer not found yet, retrying...");
                StartCoroutine(FindPlayerTransformCoroutine(clientId));
            }
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            // Отправляем RPC клиенту выгрузить все чанки
            if (_clientLoadedChunks.TryGetValue(clientId, out var loadedChunks))
            {
                foreach (var chunkId in loadedChunks)
                {
                    // RPC вызывается с сервера — отправляем конкретному клиенту перед отключением
                    UnloadChunkClientRpc(clientId, chunkId);
                }
            }
            
            // Очищаем данные
            _playerChunks.Remove(clientId);
            _clientLoadedChunks.Remove(clientId);
            _playerTransforms.Remove(clientId);
            _lastUpdateTimes.Remove(clientId);
            
            LogDebug($"Client {clientId} disconnected, chunks unloaded.");
        }
        
        #endregion
        
        #region Core Logic

        /// <summary>
        /// Get ChunkId at world position.
        /// Uses WorldChunkManager if available, otherwise calculates manually.
        /// </summary>
        private ChunkId GetChunkAtPosition(Vector3 position)
        {
            if (_chunkManager != null)
            {
                return _chunkManager.GetChunkAtPosition(position);
            }
            
            // Fallback: manual calculation
            int gridX = Mathf.FloorToInt(position.x / 2000f);
            int gridZ = Mathf.FloorToInt(position.z / 2000f);
            return new ChunkId(gridX, gridZ);
        }

        /// <summary>
        /// Обновить чанк игрока при изменении позиции.
        /// </summary>
        private void UpdatePlayerChunk(ulong clientId, Vector3 position)
        {
            if (!_chunkManager) return;
            
            ChunkId newChunk = _chunkManager.GetChunkAtPosition(position);
            
            // Проверяем изменился ли чанк
            if (!_playerChunks.TryGetValue(clientId, out var oldChunk) || !oldChunk.Equals(newChunk))
            {
                _playerChunks[clientId] = newChunk;
                
                LogDebug($"Player {clientId} moved from {oldChunk} to {newChunk}");
                
                // Обновляем загруженные чанки
                LoadChunksForClient(clientId, position);
                UnloadChunksForClient(clientId, position);
            }
        }
        
        /// <summary>
        /// Загрузить чанки вокруг клиента.
        /// </summary>
        private void LoadChunksForClient(ulong clientId, Vector3 position)
        {
            if (!_chunkManager || !_clientLoadedChunks.ContainsKey(clientId))
                return;
            
            // Используем GetChunksInRadius который работает только с зарегистрированными чанками
            List<ChunkId> chunksInRange = _chunkManager.GetChunksInRadius(position, loadRadius);
            
            foreach (var chunkId in chunksInRange)
            {
                if (!_clientLoadedChunks[clientId].Contains(chunkId))
                {
                    _clientLoadedChunks[clientId].Add(chunkId);
                    
                    // Отправляем RPC клиенту загрузить чанк
                    LoadChunkClientRpc(clientId, chunkId);
                }
            }
        }
        
        /// <summary>
        /// Выгрузить дальние чанки для клиента.
        /// </summary>
        private void UnloadChunksForClient(ulong clientId, Vector3 position)
        {
            if (!_chunkManager || !_clientLoadedChunks.ContainsKey(clientId))
                return;
            
            // Используем GetChunksInRadius для определения какие чанки должны оставаться загруженными
            List<ChunkId> chunksInUnloadRadius = _chunkManager.GetChunksInRadius(position, unloadRadius);
            var chunksInRadiusSet = new HashSet<ChunkId>(chunksInUnloadRadius);
            
            var chunksToUnload = new List<ChunkId>();
            
            foreach (var loadedChunk in _clientLoadedChunks[clientId])
            {
                if (!chunksInRadiusSet.Contains(loadedChunk))
                {
                    chunksToUnload.Add(loadedChunk);
                }
            }
            
            foreach (var chunkId in chunksToUnload)
            {
                _clientLoadedChunks[clientId].Remove(chunkId);
                
                // Отправляем RPC клиенту выгрузить чанк
                UnloadChunkClientRpc(clientId, chunkId);
            }
        }
        
        /// <summary>
        /// Вычислить ChunkId для позиции (дублирует WorldChunkManager.GetChunkAtPosition для сервера).
        /// </summary>
        private ChunkId GetChunkIdAtPosition(Vector3 position)
        {
            int gridX = Mathf.FloorToInt(position.x / 2000f);
            int gridZ = Mathf.FloorToInt(position.z / 2000f);
            return new ChunkId(gridX, gridZ);
        }
        
        #endregion
        
        #region RPCs
        
        /// <summary>
        /// RPC: Сервер → Клиент: загрузить чанк.
        /// </summary>
        [ClientRpc]
        private void LoadChunkClientRpc(ulong clientId, ChunkId chunkId, ClientRpcParams rpcParams = default)
        {
            // Этот RPC отправляется ВСЕМ клиентам, но выполняется только целевым
            if (clientId != NetworkManager.Singleton.LocalClientId)
                return;
            
            LogDebug($"[Client] Loading chunk {chunkId} by server command");
            
            // Вызываем WorldStreamingManager для загрузки
            if (WorldStreamingManager.Instance != null)
            {
                WorldStreamingManager.Instance.LoadChunkByServerCommand(chunkId);
            }
            else
            {
                Debug.LogWarning("[PlayerChunkTracker] WorldStreamingManager.Instance == null");
            }
        }
        
        /// <summary>
        /// RPC: Сервер → Клиент: выгрузить чанк.
        /// </summary>
        [ClientRpc]
        private void UnloadChunkClientRpc(ulong clientId, ChunkId chunkId, ClientRpcParams rpcParams = default)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
                return;
            
            LogDebug($"[Client] Unloading chunk {chunkId} by server command");
            
            if (WorldStreamingManager.Instance != null)
            {
                WorldStreamingManager.Instance.UnloadChunkByServerCommand(chunkId);
            }
            else
            {
                Debug.LogWarning("[PlayerChunkTracker] WorldStreamingManager.Instance == null");
            }
        }
        
        #endregion
        
        #region Public API

        /// <summary>
        /// Update player position for chunk tracking.
        /// Called from NetworkPlayer.
        /// </summary>
        public void UpdatePlayerPosition(ulong clientId, Vector3 worldPosition)
        {
            if (!IsServer) return;
            
            var chunkId = GetChunkAtPosition(worldPosition);
            
            if (_playerChunks.TryGetValue(clientId, out var currentChunk))
            {
                if (!currentChunk.Equals(chunkId))
                {
                    Debug.Log($"[PlayerChunkTracker] Player {clientId} moved from {currentChunk} to {chunkId}");
                    UnloadChunkClientRpc(clientId, currentChunk);
                    LoadChunkClientRpc(clientId, chunkId);
                }
            }
            else
            {
                LoadChunkClientRpc(clientId, chunkId);
                _clientLoadedChunks[clientId] = new HashSet<ChunkId>();
            }
            
            _playerChunks[clientId] = chunkId;
        }

        /// <summary>
        /// Принудительно обновить позицию игрока (вызывается из других систем).
        /// </summary>
        public void ForceUpdatePlayerChunk(ulong clientId, Vector3 position)
        {
            UpdatePlayerChunk(clientId, position);
        }
        
        /// <summary>
        /// Получить текущий чанк игрока.
        /// </summary>
        public ChunkId GetPlayerChunk(ulong clientId)
        {
            if (_playerChunks.TryGetValue(clientId, out var chunk))
                return chunk;
            return new ChunkId(0, 0);
        }
        
        /// <summary>
        /// Получить количество загруженных чанков для клиента.
        /// </summary>
        public int GetClientLoadedChunkCount(ulong clientId)
        {
            if (_clientLoadedChunks.TryGetValue(clientId, out var chunks))
                return chunks.Count;
            return 0;
        }
        
        #endregion
        
        #region Debug
        
        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerChunkTracker] {message}");
            }
        }
        
        #endregion
    }
}