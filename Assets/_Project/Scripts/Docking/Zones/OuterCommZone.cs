// T-DOCK-05: OuterCommZone — зона связи с диспетчером (sphere trigger).
// Паттерн: см. Assets/_Project/Trade/Scripts/Network/MarketZone.cs.
//
// Серверная сторона:
//   • Содержит SphereCollider trigger, детектит NetworkPlayer и ShipController.
//   • PollPlayersInRadius / PollShipsInRadius с debounce (3 тика).
//   • Регистрирует DockStationController в DockingZoneRegistry.
//
// Клиентская сторона:
//   • PollLocalPlayerZone (шарится через DockingZoneRegistry.LocalPlayerStation).
//   • Для T-key check: if LocalPlayerStation != null → T показывает CommPanel.
//
// Q3 (2026-06-19): Сервер — SOT. Клиент только потребитель.
// Q5 (2026-06-19): commRange настраивается в Inspector (как MarketZone.tradeRadius).

using System.Collections.Generic;
using ProjectC.Docking.Network;  // DockStationController, DockingZoneRegistry
using ProjectC.Docking.Stations; // StationRootReference
using ProjectC.Network;          // NetworkPlayerSpawner, NetworkingUtils
using ProjectC.Player;           // NetworkPlayer, ShipController
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Zones
{
    [RequireComponent(typeof(SphereCollider))]
    public class OuterCommZone : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string stationId = "";

        [Header("Zone")]
        [SerializeField, Min(50f)] private float commRange = 1000f;  // Q5: настраивается
        [SerializeField] private bool drawGizmos = true;

        public string StationId => stationId;
        public float CommRange => commRange;

        // Серверные данные
        private readonly HashSet<ulong> _playersInRange = new HashSet<ulong>();
        private readonly HashSet<ulong> _shipsInRange = new HashSet<ulong>();
        // Debounce для PollPlayersInRange (как MarketZone)
        private readonly Dictionary<ulong, int> _missingTicks = new Dictionary<ulong, int>();

        public IReadOnlyCollection<ulong> PlayersInRange => _playersInRange;
        public IReadOnlyCollection<ulong> ShipsInRange => _shipsInRange;

        private SphereCollider _sphere;
        private DockStationController _stationController;
        private bool _isServer;

        private void Awake()
        {
            _sphere = GetComponent<SphereCollider>();
            _sphere.isTrigger = true;
            _sphere.radius = commRange;  // Q5: настраивается

            _stationController = GetComponentInParent<DockStationController>();
            if (_stationController == null)
                Debug.LogError($"[OuterCommZone:{stationId}] no DockStationController in parent", this);
        }

        private void OnEnable()
        {
            // Регистрируем станцию в DockingZoneRegistry (идетемпотентно)
            if (_stationController != null)
                DockingZoneRegistry.Register(_stationController);

            // Q5: OnEnable race-fix (как MarketZone:82-104)
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted += HandleServerStarted;
                nm.OnClientStarted += HandleClientStarted;
                if (nm.IsListening) _isServer = nm.IsServer;
            }
        }

        private void Start()
        {
            // Duplicate registration safe (DockingZoneRegistry.Register проверяет stationId)
            if (_stationController != null)
                DockingZoneRegistry.Register(_stationController);
        }

        private void OnDisable()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted -= HandleServerStarted;
                nm.OnClientStarted -= HandleClientStarted;
            }
            // Unregister через DockingZoneRegistry.Unregister (внутри проверит owner)
            if (_stationController != null)
                DockingZoneRegistry.Unregister(_stationController);
            _isServer = false;
        }

        private void HandleServerStarted() => _isServer = true;
        private void HandleClientStarted() => _isServer = NetworkManager.Singleton.IsServer;

        // ========================================================
        // SERVER: TRIGGERS + UPDATE POLL (debounced)
        // ========================================================

        private float _pollTimer;
        private const float POLL_INTERVAL = 0.25f;
        private const int MISS_THRESHOLD = 3;

        private void Update()
        {
            _pollTimer += Time.deltaTime;
            if (_pollTimer < POLL_INTERVAL) return;
            _pollTimer = 0f;

            if (_isServer)
            {
                PollPlayersInRange();
                PollShipsInRange();
            }
            // Клиентская часть
            PollLocalPlayerZone();
        }

        // === Client-side: LocalPlayer detection (аналог MarketZone.PollLocalPlayerZone) ===

        /// <summary>
        /// Для T-key check. DockingZoneRegistry.LocalPlayerStation — это зона,
        /// в которой сейчас находится локальный игрок.
        /// LocalPlayerShipStation — это зона, в которой сейчас корабль локального игрока.
        /// </summary>
        private bool _loggedPlayerNull;

        private void PollLocalPlayerZone()
        {
            if (_stationController == null) return;

            // Проверяем игрока (пешком или в кабине)
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                if (!_loggedPlayerNull) { _loggedPlayerNull = true; }
                DockingZoneRegistry.LocalPlayerStation = null;
                return;
            }
            _loggedPlayerNull = false;

            float playerDist = Vector3.Distance(transform.position, localPlayer.GetEffectivePosition());
            // Q5: проверяем дистанцию
            if (playerDist <= commRange)
            {
                if (DockingZoneRegistry.LocalPlayerStation != _stationController)
                {
                    DockingZoneRegistry.LocalPlayerStation = _stationController;
                    Debug.Log($"[OuterCommZone:{stationId}] local player entered zone (dist={playerDist:F1})");
                }
            }
            else
            {
                if (DockingZoneRegistry.LocalPlayerStation == _stationController)
                {
                    DockingZoneRegistry.LocalPlayerStation = null;
                    Debug.Log($"[OuterCommZone:{stationId}] local player left zone (dist={playerDist:F1})");
                }
            }

            // Проверяем корабль (чтобы T-key работал только из пилот-кресла в зоне)
            var localShip = FindLocalShip(localPlayer);
            if (localShip != null)
            {
                float shipDist = Vector3.Distance(transform.position, localShip.transform.position);
                if (shipDist <= commRange)
                {
                    DockingZoneRegistry.LocalPlayerShipStation = _stationController;
                }
                else if (DockingZoneRegistry.LocalPlayerShipStation == _stationController)
                {
                    DockingZoneRegistry.LocalPlayerShipStation = null;
                }
            }
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }

        private static ShipController FindLocalShip(NetworkPlayer localPlayer)
        {
            if (localPlayer != null && localPlayer.IsInShip)
                return localPlayer.CurrentShip;
            return null;
        }

        // === Server-side: Polling (аналог MarketZone.PollPlayersInRadius) ===

        private void PollPlayersInRange()
        {
            var hits = Physics.OverlapSphere(transform.position, commRange, ~0, QueryTriggerInteraction.Ignore);
            var found = new HashSet<ulong>();
            for (int i = 0; i < hits.Length; i++)
            {
                var np = hits[i].GetComponentInParent<NetworkPlayer>();
                if (np == null || !np.IsSpawned) continue;
                found.Add(np.OwnerClientId);
            }
            foreach (var id in found)
            {
                if (_playersInRange.Add(id))
                {
                    _missingTicks.Remove(id);
                    Debug.Log($"[OuterCommZone:{stationId}] server: player {id} entered zone");
                }
            }
            var toRemove = new List<ulong>();
            foreach (var id in _playersInRange)
            {
                if (!found.Contains(id))
                {
                    _missingTicks.TryGetValue(id, out var n);
                    n++;
                    _missingTicks[id] = n;
                    if (n >= MISS_THRESHOLD) toRemove.Add(id);
                }
                else
                {
                    _missingTicks[id] = 0;
                }
            }
            foreach (var id in toRemove)
            {
                _playersInRange.Remove(id);
                _missingTicks.Remove(id);
            }
        }

        private void PollShipsInRange()
        {
            var hits = Physics.OverlapSphere(transform.position, commRange, ~0, QueryTriggerInteraction.Ignore);
            var found = new HashSet<ulong>();
            for (int i = 0; i < hits.Length; i++)
            {
                var ship = hits[i].GetComponentInParent<ShipController>();
                if (ship == null || !ship.IsSpawned) continue;
                found.Add(ship.NetworkObject.NetworkObjectId);
            }
            foreach (var id in found)
            {
                if (_shipsInRange.Add(id))
                    Debug.Log($"[OuterCommZone:{stationId}] server: ship {id} entered zone");
            }
            var toRemove = new List<ulong>();
            foreach (var id in _shipsInRange)
                if (!found.Contains(id)) toRemove.Add(id);
            foreach (var id in toRemove)
                _shipsInRange.Remove(id);
        }

        // ========================================================
        // Trigger callbacks (быстрый путь, как MarketZone:285-327)
        // ========================================================

        private void OnTriggerEnter(Collider other)
        {
            var np = other.GetComponentInParent<NetworkPlayer>();
            if (np != null)
            {
                float dist = Vector3.Distance(transform.position, np.GetEffectivePosition());
                if (dist <= commRange)
                {
                    if (_isServer) _playersInRange.Add(np.OwnerClientId);
                    if (np.IsOwner) DockingZoneRegistry.LocalPlayerStation = _stationController;
                }
                return;
            }
            var ship = other.GetComponentInParent<NetworkBehaviour>();
            if (ship is ShipController sc && sc.NetworkObject != null)
            {
                if (_isServer) _shipsInRange.Add(sc.NetworkObject.NetworkObjectId);
                if (IsOwnedByLocalPlayer(sc)) DockingZoneRegistry.LocalPlayerShipStation = _stationController;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var np = other.GetComponentInParent<NetworkPlayer>();
            if (np != null)
            {
                if (_isServer) _playersInRange.Remove(np.OwnerClientId);
                if (np.IsOwner && DockingZoneRegistry.LocalPlayerStation == _stationController)
                    DockingZoneRegistry.LocalPlayerStation = null;
                return;
            }
            var ship = other.GetComponentInParent<NetworkBehaviour>();
            if (ship is ShipController sc && sc.NetworkObject != null)
            {
                if (_isServer) _shipsInRange.Remove(sc.NetworkObject.NetworkObjectId);
                if (DockingZoneRegistry.LocalPlayerShipStation == _stationController)
                    DockingZoneRegistry.LocalPlayerShipStation = null;
            }
        }

        private static bool IsOwnedByLocalPlayer(ShipController ship)
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null) return false;
            return localPlayer.IsInShip && localPlayer.CurrentShip == ship;
        }

        // === Public API для DockingServer (проверка зоны) ===

        public bool IsPlayerInRange(ulong clientId) => _playersInRange.Contains(clientId);
        public bool IsShipInRange(ulong shipNetworkObjectId) => _shipsInRange.Contains(shipNetworkObjectId);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, commRange);
        }
#endif
    }
}
