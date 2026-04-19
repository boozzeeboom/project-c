using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.World.Core;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// Идентификатор чанка в grid-системе мира.
    /// Реализует INetworkSerializable для передачи через RPC.
    /// </summary>
    [Serializable]
    public struct ChunkId : System.IEquatable<ChunkId>, INetworkSerializable
    {
        public int GridX;
        public int GridZ;

        public ChunkId(int gridX, int gridZ)
        {
            GridX = gridX;
            GridZ = gridZ;
        }

        public bool Equals(ChunkId other) => GridX == other.GridX && GridZ == other.GridZ;

        public override bool Equals(object obj) => obj is ChunkId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + GridX;
                hash = hash * 31 + GridZ;
                return hash;
            }
        }

        public override string ToString() => $"Chunk({GridX}, {GridZ})";

        /// <summary>
        /// Реализация INetworkSerializable для передачи через RPC.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref GridX);
            serializer.SerializeValue(ref GridZ);
        }
    }

    /// <summary>
    /// Состояние загрузки чанка.
    /// </summary>
    public enum ChunkState
    {
        Unloaded,
        Loading,
        Loaded,
        Unloading
    }

    /// <summary>
    /// Данные одного загруженной ячейки мира.
    /// </summary>
    public class WorldChunk
    {
        public ChunkId Id;
        public Bounds WorldBounds;
        public ChunkState State;
        public List<PeakData> Peaks;
        public List<FarmData> Farms;
        public int CloudSeed;
    }

    /// <summary>
    /// Менеджер чанков мира - строит реестр всех чанков на основе WorldData,
    /// обеспечивает grid-based lookup и определение содержимого каждого чанка.
    /// </summary>
    public class WorldChunkManager : MonoBehaviour
    {
        /// <summary>Размер чанка по X и Z в units.</summary>
        public const int ChunkSize = 2000;

        [SerializeField]
        [Tooltip("ScriptableObject с данными мира (massifs, worldMinX/Z, worldMaxX/Z)")]
        private WorldData worldData;

        /// <summary>Реестр всех чанков мира: ChunkId -> WorldChunk.</summary>
        private readonly Dictionary<ChunkId, WorldChunk> _chunkRegistry = new Dictionary<ChunkId, WorldChunk>();

        /// <summary>Минимальный GridX в мире.</summary>
        private int _minGridX;

        /// <summary>Максимальный GridX в мире.</summary>
        private int _maxGridX;

        /// <summary>Минимальный GridZ в мире.</summary>
        private int _minGridZ;

        /// <summary>Максимальный GridZ в мире.</summary>
        private int _maxGridZ;

        [Header("Debug")]
        [Tooltip("Show debug logs for chunk generation")]
        [SerializeField] private bool showDebugLogs = true;

        /// <summary>Всего чанков в реестре.</summary>
        public int TotalChunkCount => _chunkRegistry.Count;

        /// <summary>
        /// Получить все чанки в мире.
        /// </summary>
        public IReadOnlyCollection<WorldChunk> GetAllChunks() => _chunkRegistry.Values;

        /// <summary>
        /// Получить чанк по его идентификатору.
        /// </summary>
        /// <returns>WorldChunk если найден, null если нет.</returns>
        public WorldChunk GetChunk(ChunkId chunkId)
        {
            _chunkRegistry.TryGetValue(chunkId, out var chunk);
            return chunk;
        }

        /// <summary>
        /// Вычислить ChunkId для данной мировой позиции.
        /// </summary>
        /// <param name="worldPos">Мировая позиция (X, Y, Z).</param>
        /// <returns>ChunkId ячейки, в которой находится позиция.</returns>
        public ChunkId GetChunkAtPosition(Vector3 worldPos)
        {
            int gridX = Mathf.FloorToInt(worldPos.x / ChunkSize);
            int gridZ = Mathf.FloorToInt(worldPos.z / ChunkSize);
            return new ChunkId(gridX, gridZ);
        }

        /// <summary>
        /// Получить список чанков в заданном радиусе (в чанках) от центральной позиции.
        /// I5-001 FIX: Returns chunks even if not in registry (for procedurally generated world).
        /// </summary>
        /// <param name="centerPos">Центральная мировая позиция.</param>
        /// <param name="radiusInChunks">Радиус в чанках (1 = 3x3 область, 2 = 5x5, и т.д.).</param>
        /// <returns>Список ChunkId в пределах радиуса.</returns>
        public List<ChunkId> GetChunksInRadius(Vector3 centerPos, int radiusInChunks)
        {
            var result = new List<ChunkId>();
            ChunkId centerChunk = GetChunkAtPosition(centerPos);

            for (int x = centerChunk.GridX - radiusInChunks; x <= centerChunk.GridX + radiusInChunks; x++)
            {
                for (int z = centerChunk.GridZ - radiusInChunks; z <= centerChunk.GridZ + radiusInChunks; z++)
                {
                    ChunkId candidate = new ChunkId(x, z);

                    // I5-001 FIX: First check if chunk exists in registry
                    if (_chunkRegistry.TryGetValue(candidate, out var existingChunk))
                    {
                        result.Add(candidate);
                    }
                    else
                    {
                        // I5-001 FIX: Create on-demand for procedurally generated world
                        result.Add(candidate);

                        if (showDebugLogs)
                        {
                            Debug.Log($"[WorldChunkManager] Creating on-demand chunk {candidate} (not in registry)");
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Вычислить детерминированный CloudSeed для чанка на основе его координат.
        /// </summary>
        /// <param name="chunkId">Идентификатор чанка.</param>
        /// <returns>Детерминированный seed для генерации облаков.</returns>
        public int GenerateCloudSeed(ChunkId chunkId)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + chunkId.GridX;
                hash = hash * 31 + chunkId.GridZ;
                return hash;
            }
        }

        private void Awake()
        {
            // Auto-find WorldData
            if (worldData == null)
            {
                // Пробуем найти в сцене
                worldData = FindAnyObjectByType<WorldData>();

                #if UNITY_EDITOR
                // Если не нашли - загружаем из Assets
                if (worldData == null)
                {
                    worldData = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldData>(
                        "Assets/_Project/Data/World/WorldData.asset");
                }
                #endif
            }

            if (worldData == null)
            {
                Debug.LogError("[WorldChunkManager] WorldData не найдена! Streaming disabled.");
                return;
            }

            BuildChunkRegistry();
        }

        /// <summary>
        /// Построить реестр всех чанков на основе границ мира из WorldData.
        /// Для каждого чанка определить WorldBounds, пики, фермы и CloudSeed.
        /// </summary>
        private void BuildChunkRegistry()
        {
            _chunkRegistry.Clear();

            // Определяем границы мира
            float minX = worldData.worldMinX;
            float maxX = worldData.worldMaxX;
            float minZ = worldData.worldMinZ;
            float maxZ = worldData.worldMaxZ;

            // Вычисляем диапазон grid-координат
            _minGridX = Mathf.FloorToInt(minX / ChunkSize);
            _maxGridX = Mathf.FloorToInt(maxX / ChunkSize);
            _minGridZ = Mathf.FloorToInt(minZ / ChunkSize);
            _maxGridZ = Mathf.FloorToInt(maxZ / ChunkSize);

            // Собираем все пики и фермы из всех массивов
            var allPeaks = new List<PeakData>();
            var allFarms = new List<FarmData>();

            foreach (var massif in worldData.massifs)
            {
                if (massif == null) continue;

                if (massif.peaks != null)
                    allPeaks.AddRange(massif.peaks);

                if (massif.farms != null)
                    allFarms.AddRange(massif.farms);
            }

            // Создаём чанк для каждой ячейки grid
            for (int gx = _minGridX; gx <= _maxGridX; gx++)
            {
                for (int gz = _minGridZ; gz <= _maxGridZ; gz++)
                {
                    ChunkId chunkId = new ChunkId(gx, gz);
                    WorldChunk chunk = CreateChunk(chunkId, allPeaks, allFarms);
                    _chunkRegistry[chunkId] = chunk;
                }
            }
        }

        /// <summary>
        /// Создать один WorldChunk с вычисленными WorldBounds, пиками, фермами и CloudSeed.
        /// </summary>
        private WorldChunk CreateChunk(ChunkId chunkId, List<PeakData> allPeaks, List<FarmData> allFarms)
        {
            // Вычисляем WorldBounds чанка
            float minX = chunkId.GridX * ChunkSize;
            float maxX = minX + ChunkSize;
            float minZ = chunkId.GridZ * ChunkSize;
            float maxZ = minZ + ChunkSize;

            Vector3 center = new Vector3(
                (minX + maxX) * 0.5f,
                0f,
                (minZ + maxZ) * 0.5f
            );
            Vector3 size = new Vector3(ChunkSize, 1000f, ChunkSize);

            Bounds worldBounds = new Bounds(center, size);

            // Определяем пики в этом чанке
            var peaksInChunk = new List<PeakData>();
            foreach (var peak in allPeaks)
            {
                if (peak == null) continue;

                Vector3 pos = peak.worldPosition;
                if (pos.x >= minX && pos.x < maxX && pos.z >= minZ && pos.z < maxZ)
                {
                    peaksInChunk.Add(peak);
                }
            }

            // Определяем фермы в этом чанке
            var farmsInChunk = new List<FarmData>();
            foreach (var farm in allFarms)
            {
                if (farm == null) continue;

                Vector3 pos = farm.worldPosition;
                if (pos.x >= minX && pos.x < maxX && pos.z >= minZ && pos.z < maxZ)
                {
                    farmsInChunk.Add(farm);
                }
            }

            // Вычисляем CloudSeed
            int cloudSeed = GenerateCloudSeed(chunkId);

            return new WorldChunk
            {
                Id = chunkId,
                WorldBounds = worldBounds,
                State = ChunkState.Unloaded,
                Peaks = peaksInChunk,
                Farms = farmsInChunk,
                CloudSeed = cloudSeed
            };
        }
    }
}
