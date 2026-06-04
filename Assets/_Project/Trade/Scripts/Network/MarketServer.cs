using System.Collections.Generic;
using ProjectC.Player;
using ProjectC.Trade.Config;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Repository;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Главный сетевой компонент торговли. NetworkBehaviour, ставится
    /// в Bootstrap сцене рядом с NetworkManager. DontDestroyOnLoad.
    ///
    /// Ответственности:
    ///   • При OnNetworkSpawn (на сервере) — инициализировать TradeWorld.
    ///   • Принимать RPC от клиентов (buy/sell/load/unload/subscribe).
    ///   • Валидировать позицию (игрок в MarketZone с нужным locationId).
    ///   • Делегировать операции в TradeWorld.
    ///   • Слать обновления (snapshot + trade result) клиентам.
    ///   • Бродкастить market updates на каждый тик.
    ///
    /// Связи:
    ///   • <see cref="MarketTimeService"/> — вызывает TradeWorld.MarketTick
    ///     и уведомляет MarketServer о необходимости бродкаста.
    ///   • <see cref="MarketZoneRegistry"/> — для проверки позиции игрока
    ///     и получения списка nearby ships.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MarketServer : NetworkBehaviour
    {
        public static MarketServer Instance { get; private set; }

        [Header("Setup")]
        [Tooltip("База данных всех TradeItemDefinition'ов (для резолвера)")]
        [SerializeField] private TradeDatabase tradeDatabase;

        [Tooltip("Все MarketConfig'и, которые нужно подключить к TradeWorld")]
        [SerializeField] private List<MarketConfig> marketConfigs = new List<MarketConfig>();

        [Header("Behavior")]
        [Tooltip("Использовать PlayerPrefsRepository (host) — по умолчанию")]
        [SerializeField] private bool useFileRepository = false;

        [Tooltip("Использовать отдельный root для файлов репозитория (если useFileRepository)")]
        [SerializeField] private string fileRepoRoot = "";

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита)")]
        [SerializeField] private int maxOpsPerMinute = 30;

        // === Runtime ===
        private IPlayerDataRepository _repository;
        private DatabaseResolver _resolver;
        private MarketTimeService _timeService;

        // Per-client rate limiting
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();

        // FIX (2026-06-04): Какой корабль клиент сейчас выбрал в UI.
        // Нужен, чтобы включить cargo этого корабля в MarketSnapshotDto.cargo
        // (см. FIX в MarketSnapshotDto). Без этого UI видел cargo только из
        // TradeResultDto.updatedCargoSnapshot, что давало stale-данные.
        // Хранится по (clientId, locationId), т.к. в разных зонах могут быть
        // разные наборы кораблей и разные выборы.
        private readonly Dictionary<string, ulong> _clientSelectedShip = new Dictionary<string, ulong>();
        private string SelectedShipKey(ulong clientId, string locationId)
            => $"{clientId}:{(locationId ?? "").ToLowerInvariant()}";

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            // 1. Repository
            _repository = useFileRepository
                ? new ServerFileRepository(string.IsNullOrEmpty(fileRepoRoot) ? null : fileRepoRoot)
                : new PlayerPrefsRepository();

            // 2. Resolver
            _resolver = new DatabaseResolver(tradeDatabase);

            // 3. TradeWorld
            TradeWorld.CreateAndInitialize(marketConfigs, _repository, _resolver);

            // 4. TimeService — найти или создать
            _timeService = MarketTimeService.Instance;
            if (_timeService == null)
            {
                var go = new GameObject("[MarketTimeService]");
                _timeService = go.AddComponent<MarketTimeService>();
            }
            _timeService.OnServerStarted();
            _timeService.onMarketTick.AddListener(BroadcastSnapshotsToAll);

            // 5. Force-collect первый снепшот
            BroadcastSnapshotsToAll();
            Debug.Log($"[MarketServer] инициализирован: markets={marketConfigs?.Count ?? 0}, repository={_repository.GetType().Name}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                if (_timeService != null) _timeService.onMarketTick.RemoveListener(BroadcastSnapshotsToAll);
                if (TradeWorld.Instance != null) TradeWorld.Instance.Shutdown();
                MarketZoneRegistry.Clear();
            }
            if (Instance == this) Instance = null;
        }

        // ========================================================
        // CLIENT → SERVER RPCs
        // ========================================================

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestBuyRpc(string locationId, string itemId, int quantity, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (!ValidateInZone(clientId, locationId, out var zone))
            {
                SendTradeResultToOwner(clientId, TradeResultDto_Fail(TradeResultCode.NotInZone, TradeOp.Buy, locationId, itemId, quantity, 0, clientId, 0));
                return;
            }
            var r = TradeWorld.Instance.TryBuy(clientId, locationId, itemId, quantity);
            var dto = BuildTradeResultDto(r, TradeOp.Buy, locationId, itemId, quantity, 0, clientId);
            SendTradeResultToOwner(clientId, dto);
            if (r.IsSuccess) SendSnapshotToClient(clientId, zone);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestSellRpc(string locationId, string itemId, int quantity, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (!ValidateInZone(clientId, locationId, out var zone))
            {
                SendTradeResultToOwner(clientId, TradeResultDto_Fail(TradeResultCode.NotInZone, TradeOp.Sell, locationId, itemId, quantity, 0, clientId, 0));
                return;
            }
            var r = TradeWorld.Instance.TrySell(clientId, locationId, itemId, quantity);
            var dto = BuildTradeResultDto(r, TradeOp.Sell, locationId, itemId, quantity, 0, clientId);
            SendTradeResultToOwner(clientId, dto);
            if (r.IsSuccess) SendSnapshotToClient(clientId, zone);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestLoadToShipRpc(string locationId, string itemId, int quantity, ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (!ValidateInZone(clientId, locationId, out var zone))
            {
                SendTradeResultToOwner(clientId, TradeResultDto_Fail(TradeResultCode.NotInZone, TradeOp.LoadToShip, locationId, itemId, quantity, shipNetworkObjectId, clientId, 0));
                return;
            }
            if (!zone.IsShipInZone(shipNetworkObjectId))
            {
                SendTradeResultToOwner(clientId, TradeResultDto_Fail(TradeResultCode.ShipNotInZone, TradeOp.LoadToShip, locationId, itemId, quantity, shipNetworkObjectId, clientId, 0));
                return;
            }
            var shipClass = ResolveShipClass(shipNetworkObjectId);
            var r = TradeWorld.Instance.TryLoadToShip(clientId, locationId, itemId, quantity, shipNetworkObjectId, shipClass);
            var dto = BuildTradeResultDto(r, TradeOp.LoadToShip, locationId, itemId, quantity, shipNetworkObjectId, clientId);
            SendTradeResultToOwner(clientId, dto);
            if (r.IsSuccess) SendSnapshotToClient(clientId, zone);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestUnloadFromShipRpc(string locationId, string itemId, int quantity, ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (!ValidateInZone(clientId, locationId, out var zone))
            {
                SendTradeResultToOwner(clientId, TradeResultDto_Fail(TradeResultCode.NotInZone, TradeOp.UnloadFromShip, locationId, itemId, quantity, shipNetworkObjectId, clientId, 0));
                return;
            }
            if (!zone.IsShipInZone(shipNetworkObjectId))
            {
                SendTradeResultToOwner(clientId, TradeResultDto_Fail(TradeResultCode.ShipNotInZone, TradeOp.UnloadFromShip, locationId, itemId, quantity, shipNetworkObjectId, clientId, 0));
                return;
            }
            var shipClass = ResolveShipClass(shipNetworkObjectId);
            var r = TradeWorld.Instance.TryUnloadFromShip(clientId, locationId, itemId, quantity, shipNetworkObjectId, shipClass);
            var dto = BuildTradeResultDto(r, TradeOp.UnloadFromShip, locationId, itemId, quantity, shipNetworkObjectId, clientId);
            SendTradeResultToOwner(clientId, dto);
            if (r.IsSuccess) SendSnapshotToClient(clientId, zone);
        }

        // FIX (2026-06-04): Клиент сообщает, какой корабль сейчас выбран в UI
        // (ship-selector или дефолтный). Сервер кладёт это в _clientSelectedShip
        // и включает cargo этого корабля в следующий snapshot. Без этого UI не
        // знал реальный cargo (получал только updatedCargoSnapshot после Load/Unload).
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SetSelectedShipRpc(string locationId, ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (string.IsNullOrEmpty(locationId)) return;
            if (shipNetworkObjectId == 0)
            {
                _clientSelectedShip.Remove(SelectedShipKey(clientId, locationId));
                return;
            }
            // Доп. валидация: корабль должен быть в той же зоне
            var zone = MarketZoneRegistry.Get(locationId);
            if (zone == null || !zone.IsShipInZone(shipNetworkObjectId)) return;
            _clientSelectedShip[SelectedShipKey(clientId, locationId)] = shipNetworkObjectId;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SubscribeMarketRpc(string locationId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            var zone = MarketZoneRegistry.Get(locationId);
            if (zone == null)
            {
                Debug.LogWarning($"[MarketServer] Subscribe from {clientId} rejected: zone '{locationId}' not found in registry");
                return;
            }
            if (!zone.IsPlayerInZone(clientId))
            {
                Debug.LogWarning($"[MarketServer] Subscribe from {clientId} rejected: player not in zone '{locationId}'. PlayersInZone count={zone.PlayersInZone.Count}");
                return;
            }
            Debug.Log($"[MarketServer] Subscribe OK from {clientId} for zone '{locationId}' → sending snapshot");
            SendSnapshotToClient(clientId, zone);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestSetTimeMultiplierRpc(float multiplier, RpcParams rpcParams = default)
        {
            if (_timeService == null) return;
            _timeService.MarketTimeMultiplier = multiplier;
            Debug.Log($"[MarketServer] time multiplier set to {multiplier:F2} by client {rpcParams.Receive.SenderClientId}");
            // Бродкастнуть всем обновлённый time info
            BroadcastSnapshotsToAll();
        }

        // ========================================================
        // SERVER → CLIENT RPCs
        // ========================================================

        [Rpc(SendTo.Owner)]
        private void ReceiveMarketSnapshotClientRpc(MarketSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            // Доставляем в MarketClientState (на каждом клиенте — свой инстанс)
            ProjectC.Trade.Client.MarketClientState.Instance?.OnSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        private void ReceiveTradeResultClientRpc(TradeResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Trade.Client.MarketClientState.Instance?.OnTradeResultReceived(result);
        }

        // ========================================================
        // BROADCAST HELPERS
        // ========================================================

        private void BroadcastSnapshotsToAll()
        {
            if (!IsServer) return;
            var tw = TradeWorld.Instance;
            if (tw == null) return;

            foreach (var kv in MarketZoneRegistry.All)
            {
                var zone = kv.Value;
                foreach (var clientId in zone.PlayersInZone)
                {
                    SendSnapshotToClient(clientId, zone);
                }
            }
        }

        private void SendSnapshotToClient(ulong clientId, MarketZone zone)
        {
            if (TradeWorld.Instance == null) return;

            var market = TradeWorld.Instance.GetMarket(zone.LocationId);
            if (market == null) return;

            var itemDtos = BuildItemPriceDtos(market);
            Debug.Log($"[MarketServer] SendSnapshotToClient: client={clientId} loc={zone.LocationId} items={itemDtos.Length}");

            // FIX (2026-06-04): вычислить cargo выбранного клиентом корабля (см. FIX в DTO).
            // Если клиент ещё не прислал SetSelectedShipRpc, fallback на первый корабль в зоне
            // (старое поведение UI: дефолтный корабль — ships[0]).
            ulong selectedShipId = 0;
            _clientSelectedShip.TryGetValue(SelectedShipKey(clientId, zone.LocationId), out selectedShipId);
            var ships = zone.BuildNearbyShipsDtos();
            if (selectedShipId == 0 && ships.Count > 0) selectedShipId = ships[0].shipNetworkObjectId;
            WarehouseEntryDto[] cargoDtos = null;
            if (selectedShipId != 0)
            {
                var shipClass = ResolveShipClass(selectedShipId);
                var cargo = TradeWorld.Instance.GetOrLoadCargo(selectedShipId, shipClass);
                if (cargo != null) cargoDtos = BuildCargoDtos(cargo.SaveToList());
            }

            var snapshot = new MarketSnapshotDto
            {
                locationId = zone.LocationId,
                displayName = zone.DisplayName,
                items = itemDtos,
                marketVersion = market.ComputeVersion(),
                warehouse = BuildWarehouseDtos(TradeWorld.Instance.GetWarehouseSnapshot(clientId, zone.LocationId)),
                credits = TradeWorld.Instance.Repository.GetCredits(clientId),
                warehouseMaxWeight = Warehouse.DEFAULT_MAX_WEIGHT,
                warehouseMaxVolume = Warehouse.DEFAULT_MAX_VOLUME,
                warehouseMaxTypes = Warehouse.DEFAULT_MAX_ITEM_TYPES,
                nearbyShips = ships.ToArray(),
                cargo = cargoDtos,
                marketTimeMultiplier = _timeService != null ? _timeService.MarketTimeMultiplier : 1f,
                secondsUntilNextTick = _timeService != null ? _timeService.SecondsUntilNextTick : 0f
            };

            // Выбираем целевого NetworkPlayer, чтобы SendTo.Owner попал именно ему
            var target = FindNetworkPlayer(clientId);
            if (target == null) return;
            target.ReceiveMarketSnapshotTargetRpc(snapshot);
        }

        private void SendTradeResultToOwner(ulong clientId, TradeResultDto dto)
        {
            var target = FindNetworkPlayer(clientId);
            if (target == null) return;
            target.ReceiveTradeResultTargetRpc(dto);
        }

        // ========================================================
        // DTO BUILDERS
        // ========================================================

        private ItemPriceDto[] BuildItemPriceDtos(MarketState market)
        {
            var list = new List<ItemPriceDto>(market.Items.Count);
            foreach (var kv in market.Items)
            {
                var s = kv.Value;
                if (s == null || s.config == null) continue;
                list.Add(new ItemPriceDto
                {
                    itemId = kv.Key,
                    displayName = _resolver.GetDisplayName(kv.Key),
                    currentPrice = s.currentPrice,
                    availableStock = s.availableStock,
                    version = s.version
                });
            }
            return list.ToArray();
        }

        private WarehouseEntryDto[] BuildWarehouseDtos(List<WarehouseEntry> entries)
        {
            if (entries == null || entries.Count == 0) return null;
            var list = new WarehouseEntryDto[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                list[i] = new WarehouseEntryDto
                {
                    itemId = entries[i].itemId,
                    displayName = _resolver.GetDisplayName(entries[i].itemId),
                    quantity = entries[i].quantity
                };
            }
            return list;
        }

        private WarehouseEntryDto[] BuildCargoDtos(List<WarehouseEntry> entries)
        {
            return BuildWarehouseDtos(entries);  // identical shape
        }

        private TradeResultDto BuildTradeResultDto(TradeResult r, TradeOp op, string locationId, string itemId, int quantity, ulong shipNetworkObjectId, ulong clientId)
        {
            var dto = new TradeResultDto
            {
                code = r.code,
                op = op,
                locationId = locationId,
                itemId = itemId,
                quantity = quantity,
                newCredits = r.newCredits,
                newStock = r.newMarketStock,
                shipNetworkObjectId = shipNetworkObjectId
            };
            if (r.updatedWarehouse != null)
                dto.updatedWarehouseSnapshot = BuildWarehouseDtos(r.updatedWarehouse.SaveToList());
            if (r.updatedCargo != null)
                dto.updatedCargoSnapshot = BuildCargoDtos(r.updatedCargo.SaveToList());
            return dto;
        }

        private TradeResultDto TradeResultDto_Fail(TradeResultCode code, TradeOp op, string loc, string item, int qty, ulong shipId, ulong clientId, int stock)
        {
            return new TradeResultDto
            {
                code = code,
                op = op,
                locationId = loc,
                itemId = item,
                quantity = qty,
                newCredits = TradeWorld.Instance != null ? TradeWorld.Instance.Repository.GetCredits(clientId) : 0f,
                newStock = stock,
                shipNetworkObjectId = shipId
            };
        }

        // ========================================================
        // UTILS
        // ========================================================

        private bool ValidateInZone(ulong clientId, string locationId, out MarketZone zone)
        {
            zone = MarketZoneRegistry.Get(locationId);
            if (zone == null) return false;
            return zone.IsPlayerInZone(clientId);
        }

        private bool CheckRateLimit(ulong clientId)
        {
            if (maxOpsPerMinute <= 0) return true;
            float now = Time.realtimeSinceStartup;
            if (!_opTimestamps.TryGetValue(clientId, out var list))
            {
                list = new List<float>();
                _opTimestamps[clientId] = list;
            }
            // Очистить старые
            list.RemoveAll(t => (now - t) > 60f);
            if (list.Count >= maxOpsPerMinute) return false;
            list.Add(now);
            return true;
        }

        private static ShipClass ResolveShipClass(ulong shipNetworkObjectId)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return ShipClass.Light;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shipNetworkObjectId, out var no)) return ShipClass.Light;
            var cargo = no.GetComponent<CargoSystem>();
            return cargo != null ? cargo.shipClass : ShipClass.Light;
        }

        private static NetworkPlayer FindNetworkPlayer(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            return client.PlayerObject?.GetComponent<NetworkPlayer>();
        }

        // ========================================================
        // CONTEXT MENU (debug)
        // ========================================================

        [ContextMenu("DEBUG: Force tick")]
        public void DebugForceTick()
        {
            if (!IsServer) return;
            if (TradeWorld.Instance == null) return;
            TradeWorld.Instance.MarketTick(60f);
            BroadcastSnapshotsToAll();
        }

        [ContextMenu("DEBUG: Set multiplier to 10x")]
        public void DebugSetMultiplier10x()
        {
            if (!IsServer) return;
            if (_timeService != null) _timeService.MarketTimeMultiplier = 10f;
        }

        [ContextMenu("DEBUG: Set multiplier to 0.1x")]
        public void DebugSetMultiplier01x()
        {
            if (!IsServer) return;
            if (_timeService != null) _timeService.MarketTimeMultiplier = 0.1f;
        }
    }
}

