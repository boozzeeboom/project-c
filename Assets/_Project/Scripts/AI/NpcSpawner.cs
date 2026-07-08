// Project C: Real-Time Combat Engine — T-NPC-02
// NpcSpawner: server-side spawner NPC-врагов вокруг ближайшего игрока.
// Design: docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md §2.1.
//
// Flow:
//   - scene-placed в BootstrapScene как root [NpcSpawner] GameObject.
//   - server-only (enabled=false на клиенте).
//   - каждые spawnCheckInterval сек проверяет:
//       1. count(alive NPCs from this spawner) < maxAliveCount?
//       2. для ближайшего игрока: есть точка на террейне в [spawnRadiusMin, spawnRadiusMax]?
//       3. rate-limit per player?
//       4. Random.value < spawnChance?
//     → spawn prefab через Instantiate + NetworkObject.Spawn().
//
// Анти-рестриктивное:
//   - если npcPrefab = null → no-op (не падает, дизайнер может деактивировать).
//   - если config = null → используется built-in default (rad 30м, max 5).
//
// v0.2 (T-NPC-09): chunk integration.
//   - опционально подписывается на ChunkLoader.OnChunkLoaded/OnChunkUnloaded.
//   - при загрузке чанка → спавнит NPC в радиусе вокруг центра чанка (до maxAlivePerChunk).
//   - spawn всё равно проходит через TrySpawnAtPoint (DRY: surface validation + rate-limit общие).
//   - при выгрузке чанка NGO сам деспавнит NPC (потому что destroyWithScene=true).
// v0.3 (T-NPC-S06): social config override — NpcSocialBrain.ApplySpawnerConfig.

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ProjectC.World.Streaming;

namespace ProjectC.AI
{
    public class NpcSpawner : NetworkBehaviour
    {
        [Header("Config (designer-tunable)")]
        [Tooltip("Опционально. Если не задан — используются дефолты.")]
        [SerializeField] private NpcSpawnerConfig _config;
        [Tooltip("Точка якорь (по умолчанию — transform.position спавнера).")]
        [SerializeField] private Transform _anchor;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        [Header("Activation (when to spawn at all)")]
        [Tooltip("Игрок должен быть в этом радиусе от спавнера чтобы спавнить NPC. " +
                 "Если 0 = всегда спавним (даже когда игрок далеко). " +
                 "Полезно для зонирования — NPC спавнятся только в конкретной области.")]
        [Range(0f, 200f)] public float activationRadius = 80f;

        [Header("Chunk integration (T-NPC-09)")]
        [Tooltip("T-NPC-09: подписаться на ChunkLoader.OnChunkLoaded/OnChunkUnloaded. " +
                 "При загрузке чанка → спавнить NPC в его центре. " +
                 "Анти-рестриктивное: false (default) = старое zone-based поведение.")]
        [SerializeField] private bool _autoPopulateChunks = false;

        [Tooltip("T-NPC-09: радиус спавна вокруг центра чанка (метры). " +
                 "NPC спавнятся в случайных точках в пределах этого радиуса от chunk.WorldBounds.center.")]
        [Range(5f, 100f)] [SerializeField] private float _chunkSpawnRadius = 30f;

        [Tooltip("T-NPC-09: максимум NPC на один чанк (дополнительно к maxAliveCount глобально). " +
                 "Если 0 — chunk-spawn выключен даже при _autoPopulateChunks=true.")]
        [Range(0, 20)] [SerializeField] private int _maxAlivePerChunk = 3;

        // Spawned NPCs (server-only) — для alive-count tracking.
        private readonly List<NetworkObject> _spawned = new List<NetworkObject>();
        private float _nextCheckTime;
        private float _spawnInterval = 4f;
        private int _maxAlive = 5;
        private float _spawnRadiusMin = 5f;
        private float _spawnRadiusMax = 20f;
        private float _spawnChance = 0.5f;
        private LayerMask _groundMask = 1;
        private float _groundRaycastDistance = 30f;
        private float _minDistanceFromOtherNpc = 3f;
        private GameObject _prefab;

        // Rate-limit per player (spawns per minute).
        private readonly Dictionary<ulong, Queue<float>> _playerSpawnTimestamps = new Dictionary<ulong, Queue<float>>();
        private const float RATE_LIMIT_WINDOW_SEC = 60f;

        // T-NPC-09: chunk integration state.
        // ChunkId → число NPC, заспавненных в этом чанке (для лимита _maxAlivePerChunk).
        private readonly Dictionary<ChunkId, int> _chunkAliveCount = new Dictionary<ChunkId, int>();
        // Кэш ChunkLoader (найден в OnNetworkSpawn, используется в handlers).
        private ChunkLoader _chunkLoader;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }

            if (_anchor == null) _anchor = transform;
            ApplyConfig();
            _nextCheckTime = Time.unscaledTime + _spawnInterval;
            if (_showDebugLogs) Debug.Log($"[NpcSpawner] Initialized. prefab={(_prefab != null ? _prefab.name : "null")}, max={_maxAlive}, interval={_spawnInterval}s, radius=[{_spawnRadiusMin},{_spawnRadiusMax}]");

            // T-NPC-09: chunk integration — опциональная подписка.
            if (_autoPopulateChunks && _maxAlivePerChunk > 0)
            {
                _chunkLoader = FindAnyObjectByType<ChunkLoader>();
                if (_chunkLoader != null)
                {
                    _chunkLoader.OnChunkLoaded += OnChunkLoaded_SpawnNpcs;
                    _chunkLoader.OnChunkUnloaded += OnChunkUnloaded_CleanupCount;
                    if (_showDebugLogs) Debug.Log($"[NpcSpawner] Chunk integration ENABLED: perChunk={_maxAlivePerChunk}, radius={_chunkSpawnRadius}m");
                }
                else
                {
                    Debug.LogWarning("[NpcSpawner] _autoPopulateChunks=true but no ChunkLoader found in scene. Chunk spawn disabled.");
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_chunkLoader != null)
            {
                _chunkLoader.OnChunkLoaded -= OnChunkLoaded_SpawnNpcs;
                _chunkLoader.OnChunkUnloaded -= OnChunkUnloaded_CleanupCount;
            }
            base.OnNetworkDespawn();
        }

        private void ApplyConfig()
        {
            if (_config != null)
            {
                _prefab = _config.npcPrefab;
                _spawnRadiusMin = _config.spawnRadiusMin;
                _spawnRadiusMax = _config.spawnRadiusMax;
                _maxAlive = _config.maxAliveCount;
                _spawnInterval = _config.spawnCheckInterval;
                _spawnChance = _config.spawnChance;
                _groundMask = _config.groundMask;
                _groundRaycastDistance = _config.groundRaycastDistance;
                _minDistanceFromOtherNpc = _config.minDistanceFromOtherNpc;
                // T-NPC-08 v0.2: read activationRadius too.
                activationRadius = _config.activationRadius;
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            if (_prefab == null) return;
            if (Time.unscaledTime < _nextCheckTime) return;
            _nextCheckTime = Time.unscaledTime + _spawnInterval;
            TickSpawn();
        }

        private void TickSpawn()
        {
            // Cleanup dead/despawned.
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                var no = _spawned[i];
                if (no == null || !no.IsSpawned) _spawned.RemoveAt(i);
            }

            if (_spawned.Count >= _maxAlive) return;

            // Найти ближайшего игрока.
            var (clientId, playerObj) = FindNearestPlayer();
            if (playerObj == null) return;

            // T-NPC-08 v0.2: активация только если игрок в зоне спавнера (для зонирования).
            Vector3 spawnerPos = _anchor != null ? _anchor.position : transform.position;
            if (activationRadius > 0f)
            {
                float distToPlayer = Vector3.Distance(spawnerPos, playerObj.transform.position);
                if (distToPlayer > activationRadius)
                {
                    // Игрок далеко → no spawn. Существующие NPC не despawn (можно отдельно).
                    return;
                }
            }

            // Rate-limit per player.
            if (!CheckRateLimit(clientId)) return;

            if (Random.value > _spawnChance) return;

            // T-NPC-08 v0.2: Spawn point — вокруг СПАВНЕРА (не вокруг игрока).
            // Это даёт зонирование — NPC спавнятся только в конкретной зоне.
            if (!TryFindSpawnPoint(spawnerPos, out Vector3 spawnPos)) return;

            // Validate distance от других NPC.
            if (IsTooCloseToOtherNpc(spawnPos)) return;

            // Spawn! (DRY: общий путь для zone-spawn и chunk-spawn через TrySpawnAtPoint)
            if (TrySpawnAtPoint(spawnerPos, clientId, out _))
            {
                if (_showDebugLogs) Debug.Log($"[NpcSpawner] Spawned NPC '{_prefab.name}' at {spawnPos:F1} (zone-center={spawnerPos:F1}, distToPlayer={Vector3.Distance(spawnerPos, playerObj.transform.position):F1}, alive={_spawned.Count}/{_maxAlive})");
            }
        }

        /// <summary>
        /// T-NPC-09: публичный API для внешнего запроса спавна NPC в конкретной точке.
        /// Используется chunk handlers (при загрузке чанка), а также может быть вызван
        /// из других систем (квесты, события, dev-commands). DRY: вся spawn-логика
        /// (instantiate, NetworkObject.Spawn, visual override, alive-tracking) в одном месте.
        /// </summary>
        /// <param name="anchorPos">Точка-центр, вокруг которой ищем место спавна (chunk center или spawner anchor).</param>
        /// <param name="attributedClientId">ClientId, к которому привязать rate-limit. 0 = chunk spawn (не привязан к игроку).</param>
        /// <param name="spawned">Выходной параметр — заспавненный NetworkObject (для трекинга), null при отказе.</param>
        /// <returns>true если NPC заспавнен, false при любом отказе (prefab=null, max alive, no surface, etc).</returns>
        public bool TrySpawnAtPoint(Vector3 anchorPos, ulong attributedClientId, out NetworkObject spawned)
        {
            spawned = null;
            if (!IsServer || _prefab == null) return false;
            if (_spawned.Count >= _maxAlive) return false;

            // Surface validation вокруг anchorPos (тот же TryFindSpawnPoint, что и для zone-spawn).
            if (!TryFindSpawnPoint(anchorPos, out Vector3 spawnPos)) return false;
            // Distance от других NPC.
            if (IsTooCloseToOtherNpc(spawnPos)) return false;

            // Spawn!
            var go = Instantiate(_prefab, spawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[NpcSpawner] Prefab '{_prefab.name}' missing NetworkObject component!");
                Destroy(go);
                return false;
            }
            netObj.Spawn(destroyWithScene: true);
            _spawned.Add(netObj);
            spawned = netObj;

            // Rate-limit tracking (для zone-spawn — per player; для chunk — attributedClientId=0).
            if (attributedClientId != 0) RegisterSpawnTimestamp(attributedClientId);

            // T-NPC-05: visual override (если задан в NpcSpawnerConfig).
            if (_config != null && _config.visualConfig != null)
            {
                var applier = go.GetComponent<NpcVisualApplier>();
                if (applier == null) applier = go.AddComponent<NpcVisualApplier>();
                applier.Apply(_config.visualConfig);
            }

            // T-NPC-14: behavior override из SpawnerConfig (Aggressive/Passive/Neutral).
            // НЕ подтирает поле _behaviorType на префабе — пробрасывает override в рантайме.
            // Это позволяет одному префабу быть Aggressive по умолчанию, но в квестовом
            // лагере (отдельный spawner + config) стать Passive.
            if (_config != null)
            {
                var brain = go.GetComponent<NpcBrain>();
                if (brain != null)
                {
                    // passiveAggroHpThreshold=0 → fallback к префабу; =-1 у maxHitsPerMinute → fallback к префабу.
                    float hpThreshold = _config.passiveAggroHpThreshold > 0f
                        ? _config.passiveAggroHpThreshold
                        : 0f;  // sentinel для brain.ApplySpawnerBehavior
                    int maxHits = _config.passiveMaxHitsPerMinute >= 0
                        ? _config.passiveMaxHitsPerMinute
                        : -1;
                    brain.ApplySpawnerBehavior(_config.behaviorType, hpThreshold, maxHits);
                    if (_showDebugLogs)
                    {
                        Debug.Log($"[NpcSpawner] Applied behavior {_config.behaviorType} to {go.name} (hpThresh={hpThreshold}, maxHits={maxHits})");
                    }
                }

                // T-NPC-S06: social config override (Phase 1: patrol, flee, grudge).
                if (_config.socialEnabled)
                {
                    var socialBrain = go.GetComponent<NpcSocialBrain>();
                    if (socialBrain == null)
                    {
                        socialBrain = go.AddComponent<NpcSocialBrain>();
                    }
                    socialBrain.ApplySpawnerConfig(_config);
                    if (_showDebugLogs)
                    {
                        Debug.Log($"[NpcSpawner] Applied social config to {go.name}: idle={_config.defaultIdleActivity}, flee={_config.canFlee}, grudge={_config.enableGrudgeMemory}");
                    }
                }
            }
            return true;
        }

        // === T-NPC-09: chunk handlers ===

        private void OnChunkLoaded_SpawnNpcs(ChunkId chunkId)
        {
            if (!IsServer || _prefab == null || _maxAlivePerChunk <= 0) return;

            // Инициализировать счётчик для этого чанка.
            _chunkAliveCount.TryGetValue(chunkId, out int existing);
            if (existing >= _maxAlivePerChunk)
            {
                if (_showDebugLogs) Debug.Log($"[NpcSpawner] Chunk {chunkId} already has {existing}/{_maxAlivePerChunk} NPC, skipping.");
                return;
            }

            // Центр чанка — берём из WorldChunkManager.
            var chunkManager = FindAnyObjectByType<WorldChunkManager>();
            if (chunkManager == null)
            {
                if (_showDebugLogs) Debug.LogWarning($"[NpcSpawner] WorldChunkManager not found — cannot resolve chunk center for {chunkId}.");
                return;
            }
            var chunk = chunkManager.GetChunk(chunkId);
            if (chunk == null)
            {
                if (_showDebugLogs) Debug.Log($"[NpcSpawner] Chunk {chunkId} not found in registry, skipping spawn.");
                return;
            }

            Vector3 chunkCenter = chunk.WorldBounds.center;
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = _maxAlivePerChunk * 3; // allow 3× попыток на нужное количество
            while (spawned < _maxAlivePerChunk && attempts < maxAttempts)
            {
                attempts++;
                // Подменим anchor для surface validation на chunk center (TrySpawnAtPoint ищет вокруг anchor).
                // Чтобы не попасть в стену в самом центре — используем chunk center + случайный оффсет внутри _chunkSpawnRadius.
                Vector3 anchor = chunkCenter + new Vector3(
                    Random.Range(-_chunkSpawnRadius, _chunkSpawnRadius), 0,
                    Random.Range(-_chunkSpawnRadius, _chunkSpawnRadius));
                if (TrySpawnAtPoint(anchor, attributedClientId: 0, out var no))
                {
                    spawned++;
                    _chunkAliveCount[chunkId] = existing + spawned;
                }
            }
            if (_showDebugLogs) Debug.Log($"[NpcSpawner] Chunk {chunkId} loaded → spawned {spawned}/{_maxAlivePerChunk} NPC (attempts={attempts}, alive global={_spawned.Count}/{_maxAlive})");
        }

        private void OnChunkUnloaded_CleanupCount(ChunkId chunkId)
        {
            // NGO сам деспавнит NPC (destroyWithScene=true).
            // Мы только сбрасываем счётчик для повторной загрузки чанка.
            _chunkAliveCount.Remove(chunkId);
            if (_showDebugLogs) Debug.Log($"[NpcSpawner] Chunk {chunkId} unloaded → counter cleared.");
        }

        /// <summary>
        /// T-NPC-09: external query для тестов/дебага.
        /// </summary>
        public int GetChunkAliveCount(ChunkId chunkId)
        {
            return _chunkAliveCount.TryGetValue(chunkId, out int c) ? c : 0;
        }

        private (ulong clientId, NetworkObject playerObj) FindNearestPlayer()
        {
            if (NetworkManager.Singleton == null) return (0, null);
            NetworkObject best = null;
            ulong bestClient = 0;
            float bestDistSq = float.MaxValue;
            Vector3 myPos = _anchor != null ? _anchor.position : transform.position;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var po = client?.PlayerObject;
                if (po == null) continue;
                float d = (po.transform.position - myPos).sqrMagnitude;
                if (d < bestDistSq) { bestDistSq = d; best = po; bestClient = client.ClientId; }
            }
            return (bestClient, best);
        }

        private bool TryFindSpawnPoint(Vector3 anchorPos, out Vector3 result)
        {
            for (int attempt = 0; attempt < 6; attempt++)  // 6 попыток на surface
            {
                Vector2 disc = Random.insideUnitCircle * _spawnRadiusMax;
                if (disc.magnitude < _spawnRadiusMin) disc = disc.normalized * _spawnRadiusMin;
                Vector3 candidate = anchorPos + new Vector3(disc.x, 0, disc.y);
                // Raycast down чтобы найти поверхность.
                if (Physics.Raycast(candidate + Vector3.up * (_groundRaycastDistance * 0.5f), Vector3.down,
                    out RaycastHit hit, _groundRaycastDistance, _groundMask, QueryTriggerInteraction.Ignore))
                {
                    result = hit.point;
                    return true;
                }
            }
            result = anchorPos;
            return false;
        }

        private bool IsTooCloseToOtherNpc(Vector3 pos)
        {
            float minDistSq = _minDistanceFromOtherNpc * _minDistanceFromOtherNpc;
            // 1) наши spawn'ы.
            foreach (var no in _spawned)
            {
                if (no == null) continue;
                if ((no.transform.position - pos).sqrMagnitude < minDistSq) return true;
            }
            // 2) любые другие NPC в сцене (анти-наложение для scene-placed + других spawner'ов).
            var others = Object.FindObjectsByType<NpcBrain>();
            foreach (var n in others)
            {
                if (n == null || !n.isActiveAndEnabled) continue;
                if ((n.transform.position - pos).sqrMagnitude < minDistSq) return true;
            }
            return false;
        }

        private bool CheckRateLimit(ulong clientId)
        {
            if (!_playerSpawnTimestamps.TryGetValue(clientId, out var q))
            {
                q = new Queue<float>();
                _playerSpawnTimestamps[clientId] = q;
            }
            float now = Time.unscaledTime;
            while (q.Count > 0 && now - q.Peek() > RATE_LIMIT_WINDOW_SEC) q.Dequeue();
            int maxPerMin = _config != null ? _config.maxSpawnsPerPlayerPerMinute : 8;
            return q.Count < maxPerMin;
        }

        private void RegisterSpawnTimestamp(ulong clientId)
        {
            if (_playerSpawnTimestamps.TryGetValue(clientId, out var q)) q.Enqueue(Time.unscaledTime);
        }
    }
}
