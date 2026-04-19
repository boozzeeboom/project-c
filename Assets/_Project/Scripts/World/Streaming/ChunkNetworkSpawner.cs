using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// Server-side компонент для спавна/деспавна NetworkObjects (сундуки, NPC, квесты)
    /// при загрузке/выгрузке чанков.
    /// 
    /// ВАЖНО: Все NetworkObject должны спавниться/деспавниться на СЕРВЕРЕ.
    /// Клиенты получают уже синхронизированные объекты через NGO.
    /// 
    /// Iteration 4: Обновлён для использования NetworkChestContainer.
    /// </summary>
    public class ChunkNetworkSpawner : NetworkBehaviour
    {
        #region Configuration
        
        [Header("Prefabs")]
        [Tooltip("Префаб сундука для спавна в чанках")]
        [SerializeField] private NetworkObject chestPrefab;
        
        [Tooltip("Префаб NPC для спавна в чанках")]
        [SerializeField] private NetworkObject npcPrefab;
        
        [Header("Settings")]
        [Tooltip("Автоматически подписываться на события ChunkLoader")]
        [SerializeField] private bool autoSubscribe = true;
        
        [Header("Debug")]
        [Tooltip("Показывать логи")]
        [SerializeField] private bool showDebugLogs = true;
        
        #endregion
        
        #region Private State
        
        /// <summary>
        /// ChunkId → список NetworkObjectId заспавненных в этом чанке
        /// </summary>
        private readonly Dictionary<ChunkId, List<ulong>> _spawnedObjects = new Dictionary<ChunkId, List<ulong>>();
        
        /// <summary>
        /// Ссылка на ChunkLoader
        /// </summary>
        private ChunkLoader _chunkLoader;
        
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
            
            // Найти компоненты
            _chunkLoader = FindAnyObjectByType<ChunkLoader>();
            _chunkManager = FindAnyObjectByType<WorldChunkManager>();
            
            if (_chunkManager == null)
            {
                Debug.LogError("[ChunkNetworkSpawner] WorldChunkManager не найден!");
                enabled = false;
                return;
            }
            
            // Подписка на события ChunkLoader если включена
            if (autoSubscribe && _chunkLoader != null)
            {
                _chunkLoader.OnChunkLoaded += OnChunkLoaded;
                _chunkLoader.OnChunkUnloaded += OnChunkUnloaded;
            }
            
            LogDebug("ChunkNetworkSpawner initialized on server.");
            
            base.OnNetworkSpawn();
        }
        
        public override void OnNetworkDespawn()
        {
            if (_chunkLoader != null)
            {
                _chunkLoader.OnChunkLoaded -= OnChunkLoaded;
                _chunkLoader.OnChunkUnloaded -= OnChunkUnloaded;
            }
            
            base.OnNetworkDespawn();
        }
        
        #endregion
        
        #region Chunk Events
        
        /// <summary>
        /// Вызывается при загрузке чанка.
        /// </summary>
        private void OnChunkLoaded(ChunkId chunkId)
        {
            if (!IsServer) return;
            
            WorldChunk chunk = _chunkManager.GetChunk(chunkId);
            if (chunk == null)
            {
                LogDebug($"Chunk {chunkId} not found in registry, skipping spawn.");
                return;
            }
            
            LogDebug($"Spawning network objects for chunk {chunkId}");
            SpawnForChunk(chunkId, chunk);
        }
        
        /// <summary>
        /// Вызывается при выгрузке чанка.
        /// </summary>
        private void OnChunkUnloaded(ChunkId chunkId)
        {
            if (!IsServer) return;
            
            LogDebug($"Despawning network objects for chunk {chunkId}");
            DespawnForChunk(chunkId);
        }
        
        #endregion
        
        #region Core Logic
        
        /// <summary>
        /// Заспавнить все NetworkObjects для чанка.
        /// </summary>
        public void SpawnForChunk(ChunkId chunkId, WorldChunk chunk)
        {
            if (!IsServer) return;
            
            if (_spawnedObjects.ContainsKey(chunkId))
            {
                LogDebug($"Chunk {chunkId} already has spawned objects, skipping.");
                return;
            }
            
            var spawnedIds = new List<ulong>();
            _spawnedObjects[chunkId] = spawnedIds;
            
            // Спавн сундуков из данных чанка (фермы содержат позиции сундуков)
            if (chunk.Farms != null && chestPrefab != null)
            {
                foreach (var farm in chunk.Farms)
                {
                    if (farm == null) continue;
                    
                    GameObject chest = InstantiateChest(farm.worldPosition);
                    if (chest != null)
                    {
                        var networkObj = chest.GetComponent<NetworkObject>();
                        if (networkObj != null && networkObj.IsSpawned)
                        {
                            spawnedIds.Add(networkObj.NetworkObjectId);
                        }
                        
                        // Привязываем сундук к чанку (поддержка обоих типов)
                        var networkChest = chest.GetComponent<Chest.NetworkChestContainer>();
                        if (networkChest != null)
                        {
                            networkChest.SetChunk(chunkId);
                        }
                        else
                        {
                            // Fallback для старого типа
                            var chestContainer = chest.GetComponent<Items.ChestContainer>();
                            if (chestContainer != null)
                            {
                                chestContainer.SetChunk(chunkId);
                            }
                        }
                    }
                }
            }
            
            // Спавн NPC (placeholder - можно расширить)
            if (chunk.Peaks != null && npcPrefab != null)
            {
                // Пример: спавн NPC около каждого пика
                foreach (var peak in chunk.Peaks)
                {
                    if (peak == null) continue;
                    
                    // Можно добавить логику спавна NPC около пиков
                    // GameObject npc = InstantiateNPC(peak.worldPosition + Vector3.up * 2f);
                }
            }
            
            LogDebug($"Spawned {spawnedIds.Count} network objects for chunk {chunkId}");
        }
        
        /// <summary>
        /// Деспавнить все NetworkObjects для чанка.
        /// </summary>
        public void DespawnForChunk(ChunkId chunkId)
        {
            if (!IsServer) return;
            
            if (!_spawnedObjects.TryGetValue(chunkId, out var objectIds))
            {
                LogDebug($"Chunk {chunkId} has no spawned objects, skipping.");
                return;
            }
            
            int despawnedCount = 0;
            
            foreach (var objId in objectIds)
            {
                if (NetworkManager.Singleton == null) continue;
                
                var spawnManager = NetworkManager.Singleton.SpawnManager;
                if (spawnManager == null) continue;
                
                // Check if object is spawned using SpawnedObjects dictionary
                if (spawnManager.SpawnedObjects.TryGetValue(objId, out var networkObject))
                {
                    if (networkObject != null)
                    {
                        networkObject.Despawn();
                        despawnedCount++;
                    }
                }
            }
            
            _spawnedObjects.Remove(chunkId);
            
            LogDebug($"Despawned {despawnedCount} network objects for chunk {chunkId}");
        }
        
        #endregion
        
        #region Spawn Helpers
        
        /// <summary>
        /// Создать сундук на позиции.
        /// </summary>
        private GameObject InstantiateChest(Vector3 position)
        {
            if (chestPrefab == null)
            {
                Debug.LogWarning("[ChunkNetworkSpawner] Chest prefab not assigned!");
                return null;
            }
            
            GameObject chest = Instantiate(chestPrefab.gameObject, position, Quaternion.identity);
            var networkObj = chest.GetComponent<NetworkObject>();
            
            if (networkObj != null)
            {
                networkObj.Spawn();
            }
            
            return chest;
        }
        
        /// <summary>
        /// Создать NPC на позиции.
        /// </summary>
        private GameObject InstantiateNPC(Vector3 position)
        {
            if (npcPrefab == null)
            {
                Debug.LogWarning("[ChunkNetworkSpawner] NPC prefab not assigned!");
                return null;
            }
            
            GameObject npc = Instantiate(npcPrefab.gameObject, position, Quaternion.identity);
            var networkObj = npc.GetComponent<NetworkObject>();
            
            if (networkObj != null)
            {
                networkObj.Spawn();
            }
            
            return npc;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Заспавнить объекты для чанка по запросу.
        /// Вызывается из PlayerChunkTracker или другой системы.
        /// </summary>
        public void RequestSpawnForChunk(ChunkId chunkId)
        {
            if (!IsServer) return;
            
            WorldChunk chunk = _chunkManager.GetChunk(chunkId);
            if (chunk != null)
            {
                SpawnForChunk(chunkId, chunk);
            }
        }
        
        /// <summary>
        /// Деспавнить объекты для чанка по запросу.
        /// </summary>
        public void RequestDespawnForChunk(ChunkId chunkId)
        {
            if (!IsServer) return;
            DespawnForChunk(chunkId);
        }
        
        /// <summary>
        /// Получить количество заспавненных объектов в чанке.
        /// </summary>
        public int GetSpawnedCount(ChunkId chunkId)
        {
            if (_spawnedObjects.TryGetValue(chunkId, out var ids))
                return ids.Count;
            return 0;
        }
        
        /// <summary>
        /// Получить общее количество заспавненных объектов.
        /// </summary>
        public int TotalSpawnedCount
        {
            get
            {
                int total = 0;
                foreach (var ids in _spawnedObjects.Values)
                {
                    total += ids.Count;
                }
                return total;
            }
        }
        
        #endregion
        
        #region Debug
        
        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[ChunkNetworkSpawner] {message}");
            }
        }
        
        #endregion
    }
}