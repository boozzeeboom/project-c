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

        // T-FO-SAVE-GUARD: последние валидные позиции (анти-отравление сейва).
        // Мир без граунда (платформы/палубы на 1000–2500): соскользнувший игрок
        // падает километры до deathY, а автосейв каждые 5с пишет точку в падении
        // поверх хорошей — следующий заход спавнит в пустоту. Держим последнюю
        // хорошую точку, пока игрок активно падает.
        private readonly Dictionary<ulong, Vector3> _lastGoodPositions = new();
        // Падение глубже этого за один тик сбора (~5с) считается невалидным.
        // Пешком: прыжки/ступеньки дают метры; 60м/тик = ~12м/с sustained = падение.
        // На корабле (inShip) гард не применяется — вертикальная скорость кораблей
        // законна, позиция вships всё равно резолвится через exit живого корабля.
        private const float MaxFallPerCollectTick = 60f;

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

                // T-PAROM-21: стоял на кабинке парома — ссылка на ветку.
                // Carry owner-side, сервер опору remote-клиентов не знает:
                // определяем сами probe-ом (дешево, раз в 5 с).
                string routeId = "";
                if (!inShip)
                    ProjectC.World.Parom.ParomRoute.TryResolveRouteId(pos, out routeId);

                // T-FO-SAVE-GUARD: пеший игрок в активном падении — пишем последнюю
                // хорошую точку вместо точки в пустоте, хорошую обновляем только
                // валидными сэмплами. Приземлился/телепортировался на твёрдое —
                // падение прекратилось, сейв возобновляется сам.
                ulong cid = np.OwnerClientId;
                if (!inShip && _lastGoodPositions.TryGetValue(cid, out Vector3 good))
                {
                    if (pos.y < good.y - MaxFallPerCollectTick)
                    {
                        Debug.LogWarning($"[PlayerPositionServer] T-FO-SAVE-GUARD falling, keep last good: client={cid} curY={pos.y:F1} goodY={good.y:F1}");
                        pos = good;
                    }
                    else
                    {
                        _lastGoodPositions[cid] = pos;
                    }
                }
                else
                {
                    _lastGoodPositions[cid] = pos;
                }

                collected.Add(new PlayerPositionSaveData
                {
                    clientId = np.OwnerClientId,
                    px = pos.x, py = pos.y, pz = pos.z,
                    inShip = inShip,
                    shipPersistentId = shipId,
                    platformRouteId = routeId,
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

        public void BeginRestore()
        {
            _dataLoaded = false;
            _savedPlayers = new List<PlayerPositionSaveData>();
            lock (_pendingLock)
            {
                _pendingPlayers = new List<PlayerPositionSaveData>();
            }
            _lastGoodPositions.Clear();
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
        /// Возвращает true, если для clientId загружена сохранённая позиция.
        /// Используется ClientSceneLoader, чтобы не перезаписать restore дефолтным spawn.
        /// </summary>
        public bool HasSavedPlayer(ulong clientId)
        {
            return _dataLoaded && _savedPlayers.Exists(p => p.clientId == clientId);
        }

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

            // T-PAROM-21: стоял на кабинке — на ЖИВУЮ позицию (s уже восстановлен
            // T-PAROM-20; при живом сервере сейв вообще не читается). Кэш carry
            // переинициализируем — иначе догоняющий рывок. Нет ветки — фолбэк ниже.
            if (!string.IsNullOrEmpty(match.platformRouteId))
            {
                var allRoutes = FindObjectsByType<ProjectC.World.Parom.ParomRoute>();
                var route = Array.Find(allRoutes, r => r.IsSpawned && r.RouteId == match.platformRouteId);
                if (route != null && route.TrolleyTransform != null)
                {
                    Vector3 boardPos = route.GetBoardingPoint();
                    TeleportPlayer(np, boardPos);
                    np.BindRiddenPlatform(route.TrolleyTransform);
                    if (_debugMode)
                        Debug.Log($"[PlayerPositionServer] Player {clientId} restored to parom '{match.platformRouteId}' at {boardPos}");
                    return true;
                }

                if (_debugMode)
                    Debug.Log($"[PlayerPositionServer] Player {clientId} was on parom '{match.platformRouteId}' but route not found — fallback to saved pos");
            }

            // Fallback: сохранённая позиция
            targetPos = new Vector3(match.px, match.py, match.pz);
            TeleportPlayer(np, targetPos);
            if (_debugMode)
                Debug.Log($"[PlayerPositionServer] Player {clientId} restored to position {targetPos}");
            return true;
        }

        /// <summary>T-ADM-03: сеттер для AdminLogBus (мастер-mute). Поле и if'ы не трогаем.</summary>
        public void SetDebugMode(bool v) => _debugMode = v;

        /// <summary>
        /// T-ADM-05: админ-телепорт игрока по clientId (только сервер).
        /// Возвращает false вне сервера или если игрок не найден.
        /// </summary>
        public bool AdminTeleportPlayer(ulong clientId, Vector3 position)
        {
            if (!IsServerSafe()) return false;
            var all = FindObjectsByType<NetworkPlayer>();
            for (int i = 0; i < all.Length; i++)
            {
                var np = all[i];
                if (np != null && np.IsSpawned && np.OwnerClientId == clientId)
                {
                    TeleportPlayer(np, position);
                    if (_debugMode)
                        Debug.Log($"[PlayerPositionServer] Admin teleport client={clientId} to {position}");
                    return true;
                }
            }
            if (_debugMode)
                Debug.LogWarning($"[PlayerPositionServer] Admin teleport: client={clientId} not found");
            return false;
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
