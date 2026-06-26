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

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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
        [SerializeField] private bool _showDebugLogs = true;

        // Spawned NPCs (server-only) — для alive-count tracking.
        private readonly List<NetworkObject> _spawned = new List<NetworkObject>();
        private float _nextCheckTime;
        private float _spawnInterval = 4f;
        private int _maxAlive = 5;
        private float _spawnRadiusMin = 20f;
        private float _spawnRadiusMax = 60f;
        private float _spawnChance = 0.5f;
        private LayerMask _groundMask = 1;
        private float _groundRaycastDistance = 30f;
        private float _minDistanceFromOtherNpc = 5f;
        private GameObject _prefab;

        // Rate-limit per player (spawns per minute).
        private readonly Dictionary<ulong, Queue<float>> _playerSpawnTimestamps = new Dictionary<ulong, Queue<float>>();
        private const float RATE_LIMIT_WINDOW_SEC = 60f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }

            if (_anchor == null) _anchor = transform;
            ApplyConfig();
            _nextCheckTime = Time.unscaledTime + _spawnInterval;
            if (_showDebugLogs) Debug.Log($"[NpcSpawner] Initialized. prefab={(_prefab != null ? _prefab.name : "null")}, max={_maxAlive}, interval={_spawnInterval}s, radius=[{_spawnRadiusMin},{_spawnRadiusMax}]");
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

            // Rate-limit per player.
            if (!CheckRateLimit(clientId)) return;

            if (Random.value > _spawnChance) return;

            // Spawn point: random в кольце [radiusMin, radiusMax] вокруг игрока.
            Vector3 anchorPos = playerObj.transform.position;
            if (!TryFindSpawnPoint(anchorPos, out Vector3 spawnPos)) return;

            // Validate distance от других NPC.
            if (IsTooCloseToOtherNpc(spawnPos)) return;

            // Spawn!
            var go = Instantiate(_prefab, spawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[NpcSpawner] Prefab '{_prefab.name}' missing NetworkObject component!");
                Destroy(go);
                return;
            }
            netObj.Spawn(destroyWithScene: true);
            _spawned.Add(netObj);
            RegisterSpawnTimestamp(clientId);

            if (_showDebugLogs) Debug.Log($"[NpcSpawner] Spawned NPC '{_prefab.name}' at {spawnPos:F1} (anchor={anchorPos:F1}, alive={_spawned.Count}/{_maxAlive})");
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
            var others = Object.FindObjectsByType<NpcBrain>(FindObjectsSortMode.None);
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