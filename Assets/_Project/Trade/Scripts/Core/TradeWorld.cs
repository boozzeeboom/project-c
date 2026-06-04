using System;
using System.Collections.Generic;
using ProjectC.Player;
using ProjectC.Trade.Config;
using ProjectC.Trade.Repository;
using ProjectC.Trade.Service;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверный singleton, держащий всё runtime-состояние торговли:
    ///   • Markets (locationId → MarketState)
    ///   • NPC-трейдеры
    ///   • Активные события
    ///   • Связь с IPlayerDataRepository для warehouse/cargo/credits
    ///
    /// Не NetworkBehaviour. Не MonoBehaviour. НЕ сериализуется в сцену.
    /// Создаётся в <see cref="Network.MarketServer.OnNetworkSpawn"/> на сервере.
    ///
    /// Все мутации — здесь. Клиент получает только снепшоты.
    /// </summary>
    public class TradeWorld
    {
        public static TradeWorld Instance { get; private set; }

        public IPlayerDataRepository Repository { get; private set; }
        public TradeItemDefinitionResolver Resolver { get; private set; }

        private readonly Dictionary<string, MarketState> _markets = new Dictionary<string, MarketState>();
        private readonly List<NPCTrader> _npcTraders = new List<NPCTrader>();
        private readonly List<MarketEvent> _activeEvents = new List<MarketEvent>();

        public IReadOnlyDictionary<string, MarketState> Markets => _markets;
        public IReadOnlyList<NPCTrader> NpcTraders => _npcTraders;
        public IReadOnlyList<MarketEvent> ActiveEvents => _activeEvents;

        public bool IsInitialized { get; private set; }

        // ========================================================
        // INITIALIZATION
        // ========================================================

        public static TradeWorld CreateAndInitialize(
            IEnumerable<MarketConfig> configs,
            IPlayerDataRepository repository,
            TradeItemDefinitionResolver resolver,
            bool initDefaultTraders = true,
            bool initDefaultEvents = true)
        {
            var w = new TradeWorld();
            w.Initialize(configs, repository, resolver, initDefaultTraders, initDefaultEvents);
            Instance = w;
            return w;
        }

        public void Initialize(
            IEnumerable<MarketConfig> configs,
            IPlayerDataRepository repository,
            TradeItemDefinitionResolver resolver,
            bool initDefaultTraders = true,
            bool initDefaultEvents = true)
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[TradeWorld] уже инициализирован, повторная инициализация игнорируется");
                return;
            }

            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

            _markets.Clear();
            if (configs != null)
            {
                foreach (var cfg in configs)
                {
                    if (cfg == null || string.IsNullOrEmpty(cfg.locationId)) continue;
                    if (_markets.ContainsKey(cfg.locationId))
                    {
                        Debug.LogWarning($"[TradeWorld] дубликат locationId: {cfg.locationId}, пропускаю");
                        continue;
                    }
                    var state = new MarketState(cfg.locationId, cfg);
                    state.Initialize();
                    _markets[cfg.locationId] = state;
                }
            }

            _npcTraders.Clear();
            if (initDefaultTraders) InitDefaultNPCTraders();

            _activeEvents.Clear();
            if (initDefaultEvents) InitDefaultMarketEvents();

            IsInitialized = true;
            Debug.Log($"[TradeWorld] инициализирован: markets={_markets.Count}, npcTraders={_npcTraders.Count}, events={_activeEvents.Count}");
        }

        public void Shutdown()
        {
            _markets.Clear();
            _npcTraders.Clear();
            _activeEvents.Clear();
            if (Instance == this) Instance = null;
            IsInitialized = false;
        }

        private void InitDefaultNPCTraders()
        {
            _npcTraders.Add(NPCTrader.CreateDefault(
                "npc_state_convoy", "ГосКонвой",
                "primium", "tertius", "mesium_canister_v01",
                5, 8, TradeCondition.Always));
            _npcTraders.Add(NPCTrader.CreateDefault(
                "npc_wind_trader", "Ветер",
                "primium", "secundus", "antigrav_ingot_v01",
                3, 5, TradeCondition.Always));
            _npcTraders.Add(NPCTrader.CreateDefault(
                "npc_caravan", "Караванщик",
                "tertius", "quartus", "latex_roll_v01",
                8, 12, TradeCondition.SupplyThreshold, 0.3f));
            _npcTraders.Add(NPCTrader.CreateDefault(
                "npc_shuttle", "Челнок",
                "secundus", "primium", "mesium_canister_v01",
                2, 4, TradeCondition.PriceThreshold, 1.3f));
        }

        private void InitDefaultMarketEvents()
        {
            _activeEvents.Add(new MarketEvent
            {
                eventId = "mesium_rush_001",
                displayName = "Мезиевая лихорадка",
                displayIcon = "🔥",
                affectedItemId = "mesium_canister_v01",
                affectedLocations = new[] { "ALL" },
                demandMultiplier = 1.4f,
                supplyMultiplier = 1.0f,
                durationSeconds = 1800f,       // 30 мин
                cooldownSeconds = 7200f,        // 2 часа
                triggerType = TriggerType.DemandThreshold,
                triggerValue = 0.8f,
                isActive = false,
                cooldownRemaining = 0
            });
        }

        // ========================================================
        // MARKET ACCESS
        // ========================================================

        public MarketState GetMarket(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            _markets.TryGetValue(locationId, out var m);
            return m;
        }

        public bool MarketExists(string locationId) => !string.IsNullOrEmpty(locationId) && _markets.ContainsKey(locationId);

        // ========================================================
        // TRADE OPERATIONS
        // ========================================================

        /// <summary>
        /// Купить товар у рынка. Товар попадает на склад игрока на этой локации.
        /// </summary>
        public TradeResult TryBuy(ulong clientId, string locationId, string itemId, int quantity)
        {
            // 1. Базовая валидация
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args", Repository.GetCredits(clientId), null, null);
            if (string.IsNullOrEmpty(locationId))
                return TradeResult.Fail(TradeResultCode.NotInZone, "no_location", Repository.GetCredits(clientId), null, null);
            if (!MarketExists(locationId))
                return TradeResult.Fail(TradeResultCode.MarketNotFound, $"market '{locationId}' not found", Repository.GetCredits(clientId), null, null);

            var market = _markets[locationId];
            var item = market.GetItem(itemId);
            if (item == null || item.config == null)
                return TradeResult.Fail(TradeResultCode.ItemNotInMarket, "item not in market", Repository.GetCredits(clientId), null, null);
            if (!item.config.allowBuy)
                return TradeResult.Fail(TradeResultCode.ItemBuyDisabled, "buy disabled", Repository.GetCredits(clientId), null, null);

            // 2. Пересчёт цены и валидация
            item.RecalculatePrice();
            if (item.currentPrice <= 0f)
                return TradeResult.Fail(TradeResultCode.PriceInvalid, "price=0", Repository.GetCredits(clientId), null, null);
            if (item.availableStock < quantity)
                return TradeResult.Fail(TradeResultCode.InsufficientStock, "stock", Repository.GetCredits(clientId), null, null);

            // 3. Проверка и списание кредитов
            float totalCost = item.currentPrice * quantity;
            float currentCredits = Repository.GetCredits(clientId);
            if (currentCredits < totalCost)
                return TradeResult.Fail(TradeResultCode.InsufficientCredits, $"need {totalCost:F0}, have {currentCredits:F0}", currentCredits, null, null);

            // 4. Склад
            var warehouse = GetOrLoadWarehouse(clientId, locationId);
            if (!warehouse.TryAdd(itemId, quantity, Resolver, out var whFail))
            {
                return TradeResult.Fail(MapWarehouseFail(whFail), whFail, currentCredits, warehouse, null);
            }

            // 5. Списание
            if (!Repository.TryModifyCredits(clientId, -totalCost, out var newCredits, out var credFail))
            {
                // откатить склад
                warehouse.TryRemove(itemId, quantity, out _);
                return TradeResult.Fail(TradeResultCode.InsufficientCredits, credFail, currentCredits, warehouse, null);
            }

            // 6. Обновление рынка
            item.availableStock -= quantity;
            PriceFormula.ApplyBuy(item, quantity);
            Repository.SetWarehouse(clientId, locationId, warehouse.SaveToList());

            Debug.Log($"[TradeWorld] BUY client={clientId} loc={locationId} item={itemId} qty={quantity} cost={totalCost:F0} newCredits={newCredits:F0}");
            return TradeResult.Ok(newCredits, item.availableStock, warehouse, null);
        }

        /// <summary>
        /// Продать товар со склада на рынок. Получить кредиты.
        /// </summary>
        public TradeResult TrySell(ulong clientId, string locationId, string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args", Repository.GetCredits(clientId), null, null);
            if (string.IsNullOrEmpty(locationId))
                return TradeResult.Fail(TradeResultCode.NotInZone, "no_location", Repository.GetCredits(clientId), null, null);
            if (!MarketExists(locationId))
                return TradeResult.Fail(TradeResultCode.MarketNotFound, $"market '{locationId}' not found", Repository.GetCredits(clientId), null, null);

            var market = _markets[locationId];
            var item = market.GetItem(itemId);
            if (item == null || item.config == null)
                return TradeResult.Fail(TradeResultCode.ItemNotInMarket, "item not in market", Repository.GetCredits(clientId), null, null);
            if (!item.config.allowSell)
                return TradeResult.Fail(TradeResultCode.ItemSellDisabled, "sell disabled", Repository.GetCredits(clientId), null, null);

            var warehouse = GetOrLoadWarehouse(clientId, locationId);
            if (!warehouse.TryRemove(itemId, quantity, out var whFail))
            {
                return TradeResult.Fail(MapWarehouseFail(whFail), whFail, Repository.GetCredits(clientId), warehouse, null);
            }

            item.RecalculatePrice();
            if (item.currentPrice <= 0f)
            {
                // откат
                warehouse.TryAdd(itemId, quantity, Resolver, out _);
                return TradeResult.Fail(TradeResultCode.PriceInvalid, "price=0", Repository.GetCredits(clientId), warehouse, null);
            }

            // 80% от цены покупки (NPC-маржа) — как в старой системе
            float revenue = item.currentPrice * quantity * 0.8f;
            Repository.TryModifyCredits(clientId, revenue, out var newCredits, out _);

            PriceFormula.ApplySell(item, quantity);
            Repository.SetWarehouse(clientId, locationId, warehouse.SaveToList());

            Debug.Log($"[TradeWorld] SELL client={clientId} loc={locationId} item={itemId} qty={quantity} revenue={revenue:F0} newCredits={newCredits:F0}");
            return TradeResult.Ok(newCredits, item.availableStock, warehouse, null);
        }

        /// <summary>
        /// Переместить товар со склада в трюм корабля.
        /// </summary>
        public TradeResult TryLoadToShip(ulong clientId, string locationId, string itemId, int quantity, ulong shipNetworkObjectId, ShipClass shipClass)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0 || shipNetworkObjectId == 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args", Repository.GetCredits(clientId), null, null);
            if (string.IsNullOrEmpty(locationId))
                return TradeResult.Fail(TradeResultCode.NotInZone, "no_location", Repository.GetCredits(clientId), null, null);

            var warehouse = GetOrLoadWarehouse(clientId, locationId);
            var cargo = GetOrLoadCargo(shipNetworkObjectId, shipClass);

            if (!warehouse.TryRemove(itemId, quantity, out var whFail))
                return TradeResult.Fail(MapWarehouseFail(whFail), whFail, Repository.GetCredits(clientId), warehouse, cargo);

            if (!cargo.TryAdd(itemId, quantity, Resolver, out var cargoFail))
            {
                // откатить склад
                warehouse.TryAdd(itemId, quantity, Resolver, out _);
                return TradeResult.Fail(MapCargoFail(cargoFail), cargoFail, Repository.GetCredits(clientId), warehouse, cargo);
            }

            Repository.SetWarehouse(clientId, locationId, warehouse.SaveToList());
            Repository.SetCargo(shipNetworkObjectId, cargo.SaveToList());

            Debug.Log($"[TradeWorld] LOAD client={clientId} loc={locationId} ship={shipNetworkObjectId} item={itemId} qty={quantity}");
            return TradeResult.Ok(Repository.GetCredits(clientId), 0, warehouse, cargo);
        }

        /// <summary>
        /// Переместить товар из трюма корабля на склад.
        /// </summary>
        public TradeResult TryUnloadFromShip(ulong clientId, string locationId, string itemId, int quantity, ulong shipNetworkObjectId, ShipClass shipClass)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0 || shipNetworkObjectId == 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args", Repository.GetCredits(clientId), null, null);
            if (string.IsNullOrEmpty(locationId))
                return TradeResult.Fail(TradeResultCode.NotInZone, "no_location", Repository.GetCredits(clientId), null, null);

            var warehouse = GetOrLoadWarehouse(clientId, locationId);
            var cargo = GetOrLoadCargo(shipNetworkObjectId, shipClass);

            if (!cargo.TryRemove(itemId, quantity, out var cargoFail))
                return TradeResult.Fail(MapCargoFail(cargoFail), cargoFail, Repository.GetCredits(clientId), warehouse, cargo);

            if (!warehouse.TryAdd(itemId, quantity, Resolver, out var whFail))
            {
                cargo.TryAdd(itemId, quantity, Resolver, out _);
                return TradeResult.Fail(MapWarehouseFail(whFail), whFail, Repository.GetCredits(clientId), warehouse, cargo);
            }

            Repository.SetWarehouse(clientId, locationId, warehouse.SaveToList());
            Repository.SetCargo(shipNetworkObjectId, cargo.SaveToList());

            Debug.Log($"[TradeWorld] UNLOAD client={clientId} loc={locationId} ship={shipNetworkObjectId} item={itemId} qty={quantity}");
            return TradeResult.Ok(Repository.GetCredits(clientId), 0, warehouse, cargo);
        }

        private static TradeResultCode MapWarehouseFail(string s)
        {
            return s switch
            {
                "warehouse_max_weight" => TradeResultCode.WarehouseFullWeight,
                "warehouse_max_volume" => TradeResultCode.WarehouseFullVolume,
                "warehouse_max_types" => TradeResultCode.WarehouseFullTypes,
                "item_not_in_warehouse" => TradeResultCode.ItemNotInWarehouse,
                "insufficient_quantity" => TradeResultCode.ItemNotInWarehouse,
                _ => TradeResultCode.InvalidArgs
            };
        }

        private static TradeResultCode MapCargoFail(string s)
        {
            return s switch
            {
                "cargo_max_weight" => TradeResultCode.CargoFullWeight,
                "cargo_max_volume" => TradeResultCode.CargoFullVolume,
                "cargo_max_slots" => TradeResultCode.CargoFullSlots,
                "item_not_in_cargo" => TradeResultCode.ItemNotInCargo,
                "insufficient_quantity" => TradeResultCode.ItemNotInCargo,
                _ => TradeResultCode.InvalidArgs
            };
        }

        // ========================================================
        // WAREHOUSE / CARGO LOADING
        // ========================================================

        private readonly Dictionary<string, Warehouse> _warehouseCache = new Dictionary<string, Warehouse>();

        public Warehouse GetOrLoadWarehouse(ulong clientId, string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            string key = $"{clientId}:{locationId.ToLowerInvariant()}";
            if (_warehouseCache.TryGetValue(key, out var w)) return w;

            var wh = new Warehouse(clientId, locationId);
            if (Repository.TryGetWarehouse(clientId, locationId, out var items))
                wh.LoadFrom(items);
            _warehouseCache[key] = wh;
            return wh;
        }

        public void InvalidateWarehouse(ulong clientId, string locationId)
        {
            string key = $"{clientId}:{locationId.ToLowerInvariant()}";
            _warehouseCache.Remove(key);
        }

        private readonly Dictionary<ulong, CargoData> _cargoCache = new Dictionary<ulong, CargoData>();

        public CargoData GetOrLoadCargo(ulong shipNetworkObjectId, ShipClass shipClass)
        {
            if (shipNetworkObjectId == 0) return null;
            if (_cargoCache.TryGetValue(shipNetworkObjectId, out var c)) return c;
            var cargo = new CargoData(shipNetworkObjectId, shipClass);
            if (Repository.TryGetCargo(shipNetworkObjectId, out var items))
                cargo.LoadFrom(items);
            _cargoCache[shipNetworkObjectId] = cargo;
            return cargo;
        }

        public void InvalidateCargo(ulong shipNetworkObjectId)
        {
            _cargoCache.Remove(shipNetworkObjectId);
        }

        // ========================================================
        // TICK
        // ========================================================

        /// <summary>
        /// Главный тик рынка. Time-based (dt — секунды), не tick-based.
        /// </summary>
        public void MarketTick(float dtSeconds, float nowUnscaled = -1f)
        {
            if (nowUnscaled < 0f) nowUnscaled = Time.realtimeSinceStartup;

            // 1. NPC-трейдеры
            foreach (var t in _npcTraders)
            {
                var from = GetMarket(t.fromLocationId);
                var to = GetMarket(t.toLocationId);
                if (t.ShouldTrade(from, to)) t.ExecuteTrade(_markets);
            }

            // 2. События: проверка триггеров
            foreach (var evt in _activeEvents)
            {
                if (evt.isActive || evt.cooldownRemaining > 0f) continue;
                if (evt.triggerType == TriggerType.Random)
                {
                    if (evt.ShouldTrigger(_markets, UnityEngine.Random.value)) ActivateEvent(evt, nowUnscaled);
                }
                else
                {
                    if (evt.ShouldTrigger(_markets)) ActivateEvent(evt, nowUnscaled);
                }
            }

            // 3. Затухание demand/supply (time-based) + регенерация стока
            foreach (var market in _markets.Values)
            {
                foreach (var kv in market.Items)
                {
                    var s = kv.Value;
                    if (s == null) continue;
                    s.demandFactor = PriceFormula.DecayFactor(s.demandFactor, dtSeconds);
                    s.supplyFactor = PriceFormula.DecayFactor(s.supplyFactor, dtSeconds);
                    PriceFormula.RegenerateStock(s);
                    s.RecalculatePrice();
                }
            }

            // 4. Тик событий
            foreach (var evt in _activeEvents)
            {
                bool wasActive = evt.isActive;
                evt.Tick(dtSeconds);
                if (wasActive && !evt.isActive)
                {
                    // Снять эффект со всех затронутых рынков
                    foreach (var market in _markets.Values) evt.RemoveFromMarket(market);
                }
            }
        }

        private void ActivateEvent(MarketEvent evt, float nowUnscaled)
        {
            evt.Activate(nowUnscaled);
            foreach (var market in _markets.Values) evt.ApplyToMarket(market);
            Debug.Log($"[TradeWorld] Event started: {evt.eventId} ({evt.displayName})");
        }

        public void StartEvent(MarketEvent newEvent)
        {
            if (newEvent == null) return;
            _activeEvents.Add(newEvent);
            ActivateEvent(newEvent, Time.realtimeSinceStartup);
        }

        // ========================================================
        // SNAPSHOT
        // ========================================================

        public List<WarehouseEntry> GetWarehouseSnapshot(ulong clientId, string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return new List<WarehouseEntry>();
            var wh = GetOrLoadWarehouse(clientId, locationId);
            return wh.SaveToList();
        }

        public List<WarehouseEntry> GetCargoSnapshot(ulong shipNetworkObjectId, ShipClass shipClass)
        {
            var c = GetOrLoadCargo(shipNetworkObjectId, shipClass);
            return c != null ? c.SaveToList() : new List<WarehouseEntry>();
        }
    }
}
