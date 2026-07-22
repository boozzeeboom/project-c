// =====================================================================================
// PlayerPositionServer.cs — persistence позиций игроков (T-PLAYER-PERSIST)
// =====================================================================================
// Документация:
//   • docs/Character/respawn/04_PLAYER_SHIP_PERSISTENCE_FINAL.md
//
// Server-only singleton. DontDestroyOnLoad.
// Save: вызывается из ShipPositionServer (единый write с ships).
// Restore: вызывается из NetworkPlayer.RestorePlayerPositionCoroutine.
// =====================================================================================

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Network;
using ProjectC.Player;

namespace ProjectC.Core.ShipPosition
{
    /// <summary>
    /// T-PLAYER-PERSIST: Server-only persistence позиций игроков.
    /// Save: каждые 5s, данные через ShipPositionServer (единый write).
    /// Restore: при OnNetworkSpawn игрока (5s delay).
    /// </summary>
    public class PlayerPositionServer : MonoBehaviour
    {
        public static PlayerPositionServer Instance { get; private set; }

        [SerializeField] private bool _debugMode = true;

        // ── pending data (забирается ShipPositionServer для единого write) ──
        private List<PlayerPositionSaveData> _pendingPlayers = new();
        private readonly object _pendingLock = new();

        // ── saved data (загружается из ShipPositionServer.RestoreCoroutine) ──
        private List<PlayerPositionSaveData> _savedPlayers = new();
        private bool _dataLoaded;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Вызывается из ShipPositionServer.Update (каждые 5s) для сбора данных.
        /// </summary>
        public void CollectPlayers()
        {
            if (!IsServerSafe()) return;

            var allPlayers = FindObjectsByType<NetworkPlayer>();
            var collected = new List<PlayerPositionSaveData>(allPlayers.Length);

            foreach (var np in allPlayers)
            {
                if (!np.IsSpawned) continue;
                if (np.GetComponent<NetworkPlayerSpawner>() != null) continue; // scene-placed ghost

                bool inShip = np.IsInShip;
                string shipId = "";
                if (inShip && np.CurrentShip != null)
                    shipId = np.CurrentShip.ShipPersistentId;

                Vector3 pos = np.GetEffectivePosition();

                collected.Add(new PlayerPositionSaveData
                {
                    clientId = np.OwnerClientId,
                    px = pos.x, py = pos.y, pz = pos.z,
                    inShip = inShip,
                    shipPersistentId = shipId,
                    savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }

            lock (_pendingLock)
            {
                _pendingPlayers = collected;
            }

            if (_debugMode)
                Debug.Log($"[PlayerPositionServer] Collected {collected.Count} players for save");
        }

        /// <summary>
        /// Вызывается из ShipPositionServer для получения pending players.
        /// </summary>
        public List<PlayerPositionSaveData> GetPendingPlayers()
        {
            lock (_pendingLock)
            {
                return new List<PlayerPositionSaveData>(_pendingPlayers);
            }
        }

        /// <summary>
        /// T-PLAYER-PERSIST (D12): загрузить players из ShipPositionServer.RestoreCoroutine.
        /// Вызывается один раз при старте сервера после загрузки ShipPositions.json.
        /// </summary>
        public void LoadSavedPlayers(List<PlayerPositionSaveData> players)
        {
            _savedPlayers = players ?? new List<PlayerPositionSaveData>();
            _dataLoaded = true;
            if (_debugMode)
                Debug.Log($"[PlayerPositionServer] Loaded {_savedPlayers.Count} saved players from ShipPositions.json");
        }

        /// <summary>
        /// Сигнал: ShipPositionServer.RestoreCoroutine завершил загрузку данных.
        /// NetworkPlayer.RestorePlayerPositionCoroutine ждёт этого флага.
        /// </summary>
        public bool DataLoaded => _dataLoaded;

        /// <summary>
        /// Restore позиции игрока при connect.
        /// </summary>
        /// <returns>true если restore выполнен (teleport).</returns>
        public bool RestorePlayer(NetworkPlayer np)
        {
            if (!IsServerSafe()) return false;

            ulong clientId = np.OwnerClientId;

            // Ищем по clientId среди _savedPlayers
            var match = _savedPlayers.Find(p => p.clientId == clientId);
            if (match == null)
            {
                if (_debugMode)
                    Debug.Log($"[PlayerPositionServer] No save for client={clientId} — standard spawn");
                return false;
            }

            Vector3 targetPos;

            if (match.inShip && !string.IsNullOrEmpty(match.shipPersistentId))
            {
                // Игрок был на корабле — ищем корабль
                var allShips = FindObjectsByType<ShipController>();
                var ship = Array.Find(allShips, s => s.IsSpawned && s.ShipPersistentId == match.shipPersistentId);

                if (ship != null)
                {
                    targetPos = ship.GetExitPosition();
                    TeleportPlayer(np, targetPos);
                    if (_debugMode)
                        Debug.Log($"[PlayerPositionServer] Player {clientId} restored to ship '{match.shipPersistentId}' at {targetPos}");
                    return true;
                }

                if (_debugMode)
                    Debug.Log($"[PlayerPositionServer] Player {clientId} was in ship '{match.shipPersistentId}' but ship not found — fallback to saved pos");
            }

            // Fallback: сохранённая позиция
            targetPos = new Vector3(match.px, match.py, match.pz);
            TeleportPlayer(np, targetPos);
            if (_debugMode)
                Debug.Log($"[PlayerPositionServer] Player {clientId} restored to position {targetPos}");
            return true;
        }

        private void TeleportPlayer(NetworkPlayer np, Vector3 position)
        {
            var controller = np.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            np.transform.position = position;
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static bool IsServerSafe()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }
    }
}
