using System.Collections.Generic;
using ProjectC.Player;
using ProjectC.Trade.Core;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Scene-placed маркер зоны рынка. Ставится в WorldScene_X_Z рядом с
    /// городами / платформами / анклавами, где разрешена торговля.
    ///
    /// На сервере:
    ///   • Содержит trigger (sphere), который детектит игроков и корабли
    ///     внутри зоны.
    ///   • Регистрирует себя в <see cref="MarketZoneRegistry"/> по locationId.
    ///   • Хранит список playerId и shipNetworkObjectId, находящихся в зоне
    ///     (для multi-ship UI и проверки позиции при RPC).
    ///
    /// На клиенте:
    ///   • Не выполняет логику, только слушает NetworkPlayer, чтобы пометить
    ///     «рядом рынок» для UI.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class MarketZone : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string locationId = "";
        [SerializeField] private string displayName = "";

        [Header("Zone")]
        [SerializeField, Min(0.1f)] private float tradeRadius = 5f;
        [SerializeField, Min(0.1f)] private float shipDockRadius = 30f;
        [SerializeField] private bool drawGizmos = true;

        public string LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? locationId : displayName;
        public float TradeRadius => tradeRadius;
        public float ShipDockRadius => shipDockRadius;

        // Серверные данные
        private readonly HashSet<ulong> _playersInZone = new HashSet<ulong>();
        private readonly HashSet<ulong> _shipsInZone = new HashSet<ulong>();
        // FIX: debounce counter для PollPlayersInRadius — счётчик подряд
        // пропущенных OverlapSphere-тиков на clientId. Удаляем игрока из
        // _playersInZone только когда счётчик >= MISS_THRESHOLD.
        private readonly Dictionary<ulong, int> _missingTicks = new Dictionary<ulong, int>();

        public IReadOnlyCollection<ulong> PlayersInZone => _playersInZone;
        public IReadOnlyCollection<ulong> ShipsInZone => _shipsInZone;

        private SphereCollider _sphere;
        private bool _isServer;

        private void Awake()
        {
            _sphere = GetComponent<SphereCollider>();
            _sphere.isTrigger = true;
            // FIX: SphereCollider теперь отвечает ТОЛЬКО за player detection (tradeRadius).
            // Раньше был Max(tradeRadius, shipDockRadius) = 591 — это заставляло
            // OnTriggerEnter срабатывать в 171м от зоны, и LocalPlayerZone
            // устанавливался преждевременно (до входа игрока в реальный tradeRadius).
            // Корабли по-прежнему детектятся через PollShipsInRadius (OverlapSphere
            // с shipDockRadius, см. ниже) — для них SphereCollider не нужен.
            _sphere.radius = tradeRadius;
        }

        private void OnEnable()
        {
            // FIX: race condition — MarketZone.OnEnable вызывается при загрузке
            // сцены ДО старта NetworkManager (host стартует позже). Раньше
            // IsServerSafe() возвращал false, и зона НИКОГДА не регистрировалась
            // в MarketZoneRegistry. Клиент потом не находил зону через FindNearestZone,
            // сервер не находил её через MarketZoneRegistry.Get.
            // Теперь: всегда регистрируем + подписываемся на старт NetworkManager.
            MarketZoneRegistry.Register(this);
            if (NetworkingUtils.IsServerSafe())
            {
                _isServer = true;
            }
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted += HandleServerStarted;
                nm.OnClientStarted += HandleClientStarted;
                if (nm.IsListening && !_isServer) _isServer = nm.IsServer;
            }
        }

        // Клиентская регистрация (для LocalPlayerZone — нужно на клиенте тоже).
        private void Start()
        {
            // FIX: Start() может сработать до StartHost, IsClientSafe() = false,
            // и мы теряли регистрацию. Теперь дублирующая регистрация безопасна
            // (Register проверяет _zones[locationId] == this).
            MarketZoneRegistry.Register(this);
        }

        private void HandleServerStarted()
        {
            _isServer = true;
            MarketZoneRegistry.Register(this);
        }

        private void HandleClientStarted()
        {
            MarketZoneRegistry.Register(this);
        }

        private void OnDisable()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted -= HandleServerStarted;
                nm.OnClientStarted -= HandleClientStarted;
            }
            MarketZoneRegistry.Unregister(this);
            _isServer = false;
        }

        // ========================================================
        // SERVER: TRIGGERS (быстрый путь) + UPDATE POLL (fallback)
        // CharacterController + SphereCollider Trigger иногда не дружат
        // (зависит от Layer collision matrix). OverlapSphere — гарантированный путь.
        // ========================================================

        private float _pollTimer;
        private const float POLL_INTERVAL = 0.25f;

        private void Update()
        {
            _pollTimer += Time.deltaTime;
            if (_pollTimer < POLL_INTERVAL) return;
            _pollTimer = 0f;

            if (_isServer)
            {
                PollPlayersInRadius();
                PollShipsInRadius();
            }
            // Клиентская часть — ищем локального игрока для LocalPlayerZone
            PollLocalPlayerZone();
        }

        private int _diagTickCounter;
        private void PollLocalPlayerZone()
        {
            var localPlayer = FindLocalPlayer();
            _diagTickCounter++;
            if (localPlayer == null)
            {
                // DIAG: раз в ~5 сек (20 poll'ов при 0.25с) логируем что localPlayer == null
                if (_diagTickCounter % 20 == 0)
                {
                    var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Include);
                    int total = allPlayers.Length;
                    int spawned = 0, owners = 0;
                    for (int i = 0; i < total; i++)
                    {
                        if (allPlayers[i] == null) continue;
                        if (allPlayers[i].IsSpawned) spawned++;
                        if (allPlayers[i].IsOwner) owners++;
                    }
                    Debug.Log($"[MarketZone:{locationId}] DIAG PollLocalPlayerZone: FindLocalPlayer=null (total NetworkPlayers in scene={total}, IsSpawned={spawned}, IsOwner={owners})");
                }
                return;
            }
            float dist = Vector3.Distance(transform.position, localPlayer.GetEffectivePosition());
            // FIX: раньше guard `if (LocalPlayerZone == this) return;` отключал
            // любую проверку после установки — игрок мог уйти за tradeRadius
            // (например, в 100м от зоны), а LocalPlayerZone оставался this.
            // Теперь poll ВСЕГДА пересчитывает дистанцию: ставит/сбрасывает
            // LocalPlayerZone строго по факту попадания в tradeRadius.
            if (dist <= tradeRadius)
            {
                if (MarketZoneRegistry.LocalPlayerZone != this)
                {
                    MarketZoneRegistry.LocalPlayerZone = this;
                    Debug.Log($"[MarketZone:{locationId}] client: local player entered zone (dist={dist:F1})");
                }
            }
            else
            {
                if (MarketZoneRegistry.LocalPlayerZone == this)
                {
                    MarketZoneRegistry.LocalPlayerZone = null;
                    Debug.Log($"[MarketZone:{locationId}] client: local player left zone (dist={dist:F1})");
                }
                // DIAG: раз в ~5 сек, когда игрок ВНЕ зоны, логируем дистанцию
                if (_diagTickCounter % 20 == 0)
                {
                    Debug.Log($"[MarketZone:{locationId}] DIAG PollLocalPlayerZone: outside zone, dist={dist:F1}, tradeRadius={tradeRadius:F1}, localPlayerPos={localPlayer.transform.position}, zonePos={transform.position}");
                }
            }
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsOwner) return players[i];
            }
            return null;
        }

        private void PollPlayersInRadius()
        {
            // Ищем всех NetworkPlayer в радиусе tradeRadius.
            // Серверная сторона — здесь точно все NetworkObject'ы спавнены.
            var hits = Physics.OverlapSphere(transform.position, tradeRadius, ~0, QueryTriggerInteraction.Ignore);
            var found = new HashSet<ulong>();
            for (int i = 0; i < hits.Length; i++)
            {
                var np = hits[i].GetComponentInParent<NetworkPlayer>();
                if (np == null || !np.IsSpawned) continue;
                found.Add(np.OwnerClientId);
            }
            // Добавляем новых
            foreach (var id in found)
            {
                if (_playersInZone.Add(id))
                {
                    _missingTicks.Remove(id);
                    Debug.Log($"[MarketZone:{locationId}] server detected player in zone: clientId={id}");
                }
            }
            // FIX: debounce на diff-remove. Раньше удаляли за один тик — один
            // "промах" OverlapSphere (CharacterController timing / NetworkTransform
            // interpolation) выкидывал игрока из _playersInZone на 250мс, и за это
            // время RequestBuyRpc получал NotInZone. Теперь удаляем только после
            // MISS_THRESHOLD подряд "пустых" тиков (~0.75с при POLL_INTERVAL=0.25).
            const int MISS_THRESHOLD = 3;
            var toRemove = new List<ulong>();
            foreach (var id in _playersInZone)
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
                _playersInZone.Remove(id);
                _missingTicks.Remove(id);
                Debug.Log($"[MarketZone:{locationId}] server: player {id} left zone (after {MISS_THRESHOLD} missed polls)");
            }
        }

        private void PollShipsInRadius()
        {
            var hits = Physics.OverlapSphere(transform.position, shipDockRadius, ~0, QueryTriggerInteraction.Ignore);
            var found = new HashSet<ulong>();
            for (int i = 0; i < hits.Length; i++)
            {
                var ship = hits[i].GetComponentInParent<ShipController>();
                if (ship == null || !ship.IsSpawned) continue;
                found.Add(ship.NetworkObject.NetworkObjectId);
            }
            foreach (var id in found) _shipsInZone.Add(id);
            var toRemove = new List<ulong>();
            foreach (var id in _shipsInZone)
                if (!found.Contains(id)) toRemove.Add(id);
            foreach (var id in toRemove) _shipsInZone.Remove(id);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Сначала пробуем как игрока
            var np = other.GetComponentInParent<NetworkPlayer>();
            if (np != null)
            {
                // FIX: проверяем дистанцию до центра зоны, а не просто факт
                // попадания в SphereCollider. Без этого при Awake radius = max(...)
                // игрок в 591м от зоны считался "в зоне" и UI открывался за пределами
                // реальной tradeRadius. Теперь radius = tradeRadius (см. Awake), но
                // проверка остаётся как defense in depth на случай если кто-то изменит
                // radius в инспекторе.
                float dist = Vector3.Distance(transform.position, np.GetEffectivePosition());
                if (dist <= tradeRadius)
                {
                    if (_isServer) _playersInZone.Add(np.OwnerClientId);
                    if (np.IsOwner) MarketZoneRegistry.LocalPlayerZone = this;
                }
                return;
            }
            // Потом как корабль
            var ship = other.GetComponentInParent<NetworkBehaviour>();
            if (ship is ShipController sc && sc.NetworkObject != null)
            {
                if (_isServer) _shipsInZone.Add(sc.NetworkObject.NetworkObjectId);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var np = other.GetComponentInParent<NetworkPlayer>();
            if (np != null)
            {
                if (_isServer) _playersInZone.Remove(np.OwnerClientId);
                if (np.IsOwner && MarketZoneRegistry.LocalPlayerZone == this) MarketZoneRegistry.LocalPlayerZone = null;
                return;
            }
            var ship = other.GetComponentInParent<NetworkBehaviour>();
            if (ship is ShipController sc && sc.NetworkObject != null)
            {
                if (_isServer) _shipsInZone.Remove(sc.NetworkObject.NetworkObjectId);
            }
        }

        public bool IsPlayerInZone(ulong clientId) => _playersInZone.Contains(clientId);
        public bool IsShipInZone(ulong shipNetworkObjectId) => _shipsInZone.Contains(shipNetworkObjectId);

        // ========================================================
        // SERVER: SHIP INFO для DTO
        // ========================================================

        public List<Dto.ShipSummaryDto> BuildNearbyShipsDtos()
        {
            var list = new List<Dto.ShipSummaryDto>();
            if (!_isServer) return list;
            if (TradeWorld.Instance == null) return list;

            foreach (var shipId in _shipsInZone)
            {
                if (shipId == 0) continue;
                var no = FindNetworkObject(shipId);
                if (no == null) continue;
                var sc = no.GetComponent<ShipController>();
                if (sc == null) continue;

                var cargoComp = sc.GetComponent<CargoSystem>();
                ShipClass cls = cargoComp != null ? cargoComp.shipClass : ShipClass.Light;
                var limits = ShipClassLimits.Get(cls);

                var cargo = TradeWorld.Instance.GetOrLoadCargo(shipId, cls);
                float w = cargo != null && TradeWorld.Instance.Resolver != null
                    ? cargo.ComputeTotalWeight(TradeWorld.Instance.Resolver) : 0f;
                float v = cargo != null && TradeWorld.Instance.Resolver != null
                    ? cargo.ComputeTotalVolume(TradeWorld.Instance.Resolver) : 0f;
                int s = cargo != null && TradeWorld.Instance.Resolver != null
                    ? cargo.ComputeTotalSlots(TradeWorld.Instance.Resolver) : 0;

                list.Add(new Dto.ShipSummaryDto
                {
                    shipNetworkObjectId = shipId,
                    displayName = string.IsNullOrEmpty(sc.gameObject.name) ? $"Корабль #{shipId}" : sc.gameObject.name,
                    shipClassName = cls.ToString(),
                    currentWeight = w,
                    maxWeight = limits.maxWeight,
                    currentVolume = v,
                    maxVolume = limits.maxVolume,
                    currentSlots = s,
                    maxSlots = limits.maxSlots,
                    uniqueItemCount = cargo?.Items.Count ?? 0
                });
            }
            return list;
        }

        private static NetworkObject FindNetworkObject(ulong networkObjectId)
        {
            if (NetworkManager.Singleton == null) return null;
            if (NetworkManager.Singleton.SpawnManager == null) return null;
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var no) ? no : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, tradeRadius);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, shipDockRadius);
        }
#endif
    }
}
