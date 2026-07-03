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
        // T-X1: renamed NPCTrader → MarketTrader (M9 cleanup). _npcTraders list name kept for source compat.
        private readonly List<MarketTrader> _npcTraders = new List<MarketTrader>();
        private readonly List<MarketEvent> _activeEvents = new List<MarketEvent>();

        public IReadOnlyDictionary<string, MarketState> Markets => _markets;
        public IReadOnlyList<MarketTrader> NpcTraders => _npcTraders;
        public IReadOnlyList<MarketEvent> ActiveEvents => _activeEvents;

        public bool IsInitialized { get; private set; }

        // ========================================================
        // T-CARGO-03: События для подписчиков (ShipController, UI)
        // ========================================================
        // OnCargoChanged вызывается при ЛЮБОЙ мутации cargo корабля:
        //   • TryLoadToShip / TryUnloadFromShip (явные операции игрока)
        //   • GetOrLoadCargo (первая загрузка из репозитория)
        //   • InvalidateCargo (явная инвалидация)
        // Подписчики (ShipController) используют это для обновления NetworkVariable.
        // ========================================================
        public event Action<ulong> OnCargoChanged;

        /// <summary>
        /// T-CARGO-UI-02: Публичный доступ для внешних систем (ShipCargoServer),
        /// которые мутируют CargoData напрямую. Вызывает OnCargoChanged →
        /// ShipController.UpdateTelemetryState → NetworkVariable sync → UI.
        /// </summary>
        public void NotifyCargoChanged(ulong shipNetworkObjectId)
        {
            OnCargoChanged?.Invoke(shipNetworkObjectId);
        }

        // ========================================================
        // T-CARGO-NPC-01: Server-only NPC trader API.
        // ========================================================
        // Отдельные методы (не перегрузки TryBuy/TrySell), потому что:
        //   1. NPC не имеет warehouse на этой станции — товар идёт market.stock ↔ cargo.
        //   2. useUnlimitedCredits=true скипает проверку кредитов (PlayerPrefsRepository
        //      клампит к 0 и не любит Infinity; см. D33 в T_CARGO_NPC_01_DESIGN_2026-07-03.md).
        //   3. Минимальный surface change — существующий TryBuy/TrySell контракт НЕ ломается.
        //   4. Symmetric: TryNpcBuy (market.stock → cargo) + TryNpcSell (cargo → market.stock).
        // ========================================================

        /// <summary>
        /// NPC-курьер покупает qty единиц itemId с рынка locationId в cargo своего корабля.
        /// Товар списывается с market.availableStock и кладётся в cargo напрямую
        /// (минуя warehouse, которого у NPC нет). credits не списываются.
        /// Returns: TradeResult (как TryBuy), но с npcClientId подставленным в result-структуру.
        /// </summary>
        public TradeResult TryNpcBuy(
            ulong npcClientId,
            string locationId,
            string itemId,
            int quantity,
            ulong npcShipNetworkObjectId,
            ShipClass shipClass,
            bool useUnlimitedCredits)
        {
            // 1. Валидация (аналогично TryBuy)
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args", 0f, null, null);
            if (string.IsNullOrEmpty(locationId))
                return TradeResult.Fail(TradeResultCode.NotInZone, "no_location", 0f, null, null);
            if (npcShipNetworkObjectId == 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args_npc_ship_id", 0f, null, null);
            if (!MarketExists(locationId))
                return TradeResult.Fail(TradeResultCode.MarketNotFound, $"market '{locationId}' not found", 0f, null, null);
            if (npcClientId == 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "npcClientId==0 reserved for server", 0f, null, null);

            var market = _markets[locationId];
            var item = market.GetItem(itemId);
            if (item == null || item.config == null)
                return TradeResult.Fail(TradeResultCode.ItemNotInMarket, "item not in market", 0f, null, null);
            if (!item.config.allowBuy)
                return TradeResult.Fail(TradeResultCode.ItemBuyDisabled, "buy disabled", 0f, null, null);

            // 2. Stock check
            item.RecalculatePrice();
            if (item.currentPrice <= 0f)
                return TradeResult.Fail(TradeResultCode.PriceInvalid, "price=0", 0f, null, null);
            if (item.availableStock < quantity)
                return TradeResult.Fail(TradeResultCode.InsufficientStock, "stock", 0f, null, null);

            // 3. Credits check — скипаем если useUnlimitedCredits
            //    Иначе — стандартная проверка через Repository (для будущей экономики NPC).
            float totalCost = item.currentPrice * quantity;
            if (!useUnlimitedCredits)
            {
                float currentCredits = Repository.GetCredits(npcClientId);
                if (currentCredits < totalCost)
                    return TradeResult.Fail(TradeResultCode.InsufficientCredits,
                        $"need {totalCost:F0}, have {currentCredits:F0}", currentCredits, null, null);
                if (!Repository.TryModifyCredits(npcClientId, -totalCost, out _, out var credFail))
                    return TradeResult.Fail(TradeResultCode.InsufficientCredits, credFail, currentCredits, null, null);
            }

            // 4. Cargo NPC-корабля
            var cargo = GetOrLoadCargo(npcShipNetworkObjectId, shipClass);
            if (cargo == null)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "cargo=null (npc ship not registered?)", 0f, null, null);

            // 5. Pre-check cargo limits (force-register через NetworkManager если ещё не в реестре)
            if (TryCheckEffectiveCargoLimits(npcShipNetworkObjectId, cargo, itemId, quantity, out var effFail))
            {
                return TradeResult.Fail(MapCargoFail(effFail), effFail, 0f, null, cargo);
            }

            if (!cargo.TryAdd(itemId, quantity, Resolver, out var cargoFail))
            {
                return TradeResult.Fail(MapCargoFail(cargoFail), cargoFail, 0f, null, cargo);
            }

            // 6. Commit: stock update + price formula + persist cargo
            item.availableStock -= quantity;
            PriceFormula.ApplyBuy(item, quantity);
            Repository.SetCargo(npcShipNetworkObjectId, cargo.SaveToList());

            OnCargoChanged?.Invoke(npcShipNetworkObjectId);
            Debug.Log($"[TradeWorld] NPC_BUY npc={npcClientId:X} loc={locationId} ship={npcShipNetworkObjectId} item={itemId} qty={quantity} cost={totalCost:F0} (unlimited={useUnlimitedCredits})");
            return TradeResult.Ok(useUnlimitedCredits ? float.PositiveInfinity : Repository.GetCredits(npcClientId),
                item.availableStock, null, cargo);
        }

        /// <summary>
        /// NPC-курьер продаёт qty единиц itemId из cargo своего корабля на рынок locationId.
        /// Товар списывается из cargo и добавляется в market.availableStock.
        /// credits НЕ начисляются (useUnlimitedCredits=true → кошелёк бесполезен).
        /// Returns: TradeResult.
        /// </summary>
        public TradeResult TryNpcSell(
            ulong npcClientId,
            string locationId,
            string itemId,
            int quantity,
            ulong npcShipNetworkObjectId,
            ShipClass shipClass,
            bool useUnlimitedCredits)
        {
            // 1. Валидация
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args", 0f, null, null);
            if (string.IsNullOrEmpty(locationId))
                return TradeResult.Fail(TradeResultCode.NotInZone, "no_location", 0f, null, null);
            if (npcShipNetworkObjectId == 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "invalid_args_npc_ship_id", 0f, null, null);
            if (!MarketExists(locationId))
                return TradeResult.Fail(TradeResultCode.MarketNotFound, $"market '{locationId}' not found", 0f, null, null);
            if (npcClientId == 0)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "npcClientId==0 reserved for server", 0f, null, null);

            var market = _markets[locationId];
            var item = market.GetItem(itemId);
            if (item == null || item.config == null)
                return TradeResult.Fail(TradeResultCode.ItemNotInMarket, "item not in market", 0f, null, null);
            if (!item.config.allowSell)
                return TradeResult.Fail(TradeResultCode.ItemSellDisabled, "sell disabled", 0f, null, null);

            // 2. Cargo remove
            var cargo = GetOrLoadCargo(npcShipNetworkObjectId, shipClass);
            if (cargo == null)
                return TradeResult.Fail(TradeResultCode.InvalidArgs, "cargo=null (npc ship not registered?)", 0f, null, null);

            if (!cargo.TryRemove(itemId, quantity, out var cargoFail))
                return TradeResult.Fail(MapCargoFail(cargoFail), cargoFail, 0f, null, cargo);

            // 3. Market update + price formula
            item.RecalculatePrice();
            if (item.currentPrice <= 0f)
            {
                // откат cargo
                cargo.TryAdd(itemId, quantity, Resolver, out _);
                return TradeResult.Fail(TradeResultCode.PriceInvalid, "price=0", 0f, null, cargo);
            }

            // 4. Stock update + price formula + persist
            item.availableStock += quantity;
            PriceFormula.ApplySell(item, quantity);
            Repository.SetCargo(npcShipNetworkObjectId, cargo.SaveToList());

            // 5. Credits — начисляем только если НЕ unlimited (для будущей экономики NPC).
            //    Сейчас не начисляем (у нас useUnlimited=true), но код пишем готовый.
            float revenue = item.currentPrice * quantity * 0.8f; // 80% от цены (NPC-маржа)
            if (!useUnlimitedCredits)
            {
                Repository.TryModifyCredits(npcClientId, revenue, out _, out _);
            }

            OnCargoChanged?.Invoke(npcShipNetworkObjectId);
            Debug.Log($"[TradeWorld] NPC_SELL npc={npcClientId:X} loc={locationId} ship={npcShipNetworkObjectId} item={itemId} qty={quantity} revenue={revenue:F0} (unlimited={useUnlimitedCredits})");
            return TradeResult.Ok(useUnlimitedCredits ? float.PositiveInfinity : Repository.GetCredits(npcClientId),
                item.availableStock, null, cargo);
        }

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
            // T-X1: NPCTrader.CreateDefault → MarketTrader.CreateDefault.
            _npcTraders.Add(MarketTrader.CreateDefault(
                "npc_state_convoy", "ГосКонвой",
                "primium", "tertius", "mesium_canister_v01",
                5, 8, TradeCondition.Always));
            _npcTraders.Add(MarketTrader.CreateDefault(
                "npc_wind_trader", "Ветер",
                "primium", "secundus", "antigrav_ingot_v01",
                3, 5, TradeCondition.Always));
            _npcTraders.Add(MarketTrader.CreateDefault(
                "npc_caravan", "Караванщик",
                "tertius", "quartus", "latex_roll_v01",
                8, 12, TradeCondition.SupplyThreshold, 0.3f));
            _npcTraders.Add(MarketTrader.CreateDefault(
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

            // T-CARGO-06: pre-check через ShipCargoRegistry (per-instance лимиты с модулями).
            // Если корабль не зарегистрирован — fallback на cargo.TryAdd (статический).
            // Pre-check также устанавливает _limitsOverride на cargo, чтобы
            // cargo.TryAdd использовал effective лимиты, а не статический fallback.
            if (TryCheckEffectiveCargoLimits(shipNetworkObjectId, cargo, itemId, quantity, out var effFail))
            {
                // откатить склад
                warehouse.TryAdd(itemId, quantity, Resolver, out _);
                return TradeResult.Fail(MapCargoFail(effFail), effFail, Repository.GetCredits(clientId), warehouse, cargo);
            }

            if (!cargo.TryAdd(itemId, quantity, Resolver, out var cargoFail))
            {
                // откатить склад
                warehouse.TryAdd(itemId, quantity, Resolver, out _);
                return TradeResult.Fail(MapCargoFail(cargoFail), cargoFail, Repository.GetCredits(clientId), warehouse, cargo);
            }

            Repository.SetWarehouse(clientId, locationId, warehouse.SaveToList());
            Repository.SetCargo(shipNetworkObjectId, cargo.SaveToList());

            OnCargoChanged?.Invoke(shipNetworkObjectId);
            Debug.Log($"[TradeWorld] LOAD client={clientId} loc={locationId} ship={shipNetworkObjectId} item={itemId} qty={quantity}");
            return TradeResult.Ok(Repository.GetCredits(clientId), 0, warehouse, cargo);
        }

        /// <summary>
        /// T-CARGO-06: Pre-check через ShipCargoRegistry.
        /// Если корабль зарегистрирован (есть в ShipCargoRegistry) — используем
        /// per-instance лимиты с учётом модулей. Иначе return false (= cargo.TryAdd
        /// сам проверит статический fallback).
        ///
        /// FIX (T-CARGO-06-race): NGO не гарантирует порядок OnNetworkSpawn — клиент
        /// может вызвать TryLoadToShip RPC раньше, чем у сервера отработает
        /// ShipController.OnNetworkSpawn (race). Поэтому перед использованием
        /// registry — пробуем force-register через NetworkManager.SpawnManager.
        /// </summary>
        private bool TryCheckEffectiveCargoLimits(ulong shipNetworkObjectId, CargoData cargo, string itemId, int quantity, out string failReason)
        {
            failReason = null;
            // Force-register корабля если он заспавнен, но ещё не в реестре
            TryForceRegisterFromNetworkManager(shipNetworkObjectId);
            var effLimits = ProjectC.Ship.ShipCargoRegistry.GetEffectiveLimits(shipNetworkObjectId);
            if (effLimits == null) return false; // корабль не зарегистрирован, пусть cargo.TryAdd fallback'нет

            if (Resolver == null) return false; // нечем посчитать вес/объём

            int itemSlots = Resolver.GetSlots(itemId);
            float itemWeight = Resolver.GetWeight(itemId);
            float itemVolume = Resolver.GetVolume(itemId);

            float newWeight = cargo.ComputeTotalWeight(Resolver) + itemWeight * quantity;
            float newVolume = cargo.ComputeTotalVolume(Resolver) + itemVolume * quantity;
            int newSlots = cargo.ComputeTotalSlots(Resolver) + itemSlots * quantity;

            if (newWeight > effLimits.Value.maxWeight) { failReason = "cargo_max_weight"; return true; }
            if (newVolume > effLimits.Value.maxVolume) { failReason = "cargo_max_volume"; return true; }
            if (newSlots > effLimits.Value.maxSlots) { failReason = "cargo_max_slots"; return true; }

            // Check passed — устанавливаем effective лимиты на cargo, чтобы
            // cargo.TryAdd (сразу после pre-check) использовал их вместо статики.
            cargo.SetLimitsOverride(new ShipClassLimits.Limits
            {
                maxSlots = effLimits.Value.maxSlots,
                maxWeight = effLimits.Value.maxWeight,
                maxVolume = effLimits.Value.maxVolume,
                penaltyFactor = effLimits.Value.penaltyFactor,
            });
            return false;
        }

        /// <summary>
        /// T-CARGO-06-race: если корабль уже заспавнен, но ещё не зарегистрирован
        /// в ShipCargoRegistry (race между OnNetworkSpawn и RPC TryLoadToShip) —
        /// находим его через NetworkManager.SpawnManager и регистрируем.
        /// </summary>
        private static void TryForceRegisterFromNetworkManager(ulong shipNetworkObjectId)
        {
            if (ProjectC.Ship.ShipCargoRegistry.Get(shipNetworkObjectId) != null) return;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(shipNetworkObjectId, out var no)) return;

            var sc = no != null ? no.GetComponent<ProjectC.Player.ShipController>() : null;
            if (sc != null)
            {
                ProjectC.Ship.ShipCargoRegistry.Register(sc);
            }
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

            OnCargoChanged?.Invoke(shipNetworkObjectId);
            Debug.Log($"[TradeWorld] UNLOAD client={clientId} loc={locationId} ship={shipNetworkObjectId} item={itemId} qty={quantity}");
            return TradeResult.Ok(Repository.GetCredits(clientId), 0, warehouse, cargo);
        }

        // ========================================================
        // T-CARGO-04: Повреждение груза при столкновении
        // ========================================================
        // Параметры передаются снаружи (из ShipController → ShipCollisionDamageConfig),
        // чтобы TradeWorld не зависел от ProjectC.Ship.
        //   • dangerous: roll leakChance → удалить leakPercentOfStack от количества
        //   • fragile:   roll fragileChance → логировать повреждение (пока без мутации)
        // Возвращает (leaked, damaged) — сколько единиц потеряно / сколько помялось.
        // ========================================================
        public struct CollisionDamageParams
        {
            public float impactEnergy;
            public float impactEnergyThreshold;
            public float leakChancePerDangerous;
            public float leakPercentOfStack;
            public float fragileChancePerItem;
            public bool verboseLogging;
        }

        public void TryDamageCargo(ulong shipNetworkObjectId, ShipClass shipClass, CollisionDamageParams p,
            out int leakedAmount, out int damagedAmount, out bool processed)
        {
            leakedAmount = 0;
            damagedAmount = 0;
            processed = false;

            if (shipNetworkObjectId == 0 || Resolver == null) return;
            if (p.impactEnergy < p.impactEnergyThreshold) return; // мелкое столкновение — игнор

            var cargo = GetOrLoadCargo(shipNetworkObjectId, shipClass);
            if (cargo == null) return;

            processed = true;
            var items = cargo.Items;
            // Снимем копию списка, т.к. TryRemove мутирует _items
            var snapshot = new System.Collections.Generic.List<WarehouseEntry>(items.Count);
            for (int i = 0; i < items.Count; i++) snapshot.Add(items[i]);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                if (string.IsNullOrEmpty(entry.itemId) || entry.quantity <= 0) continue;
                if (!Resolver.TryGet(entry.itemId, out var def) || def == null) continue;

                if (def.isDangerous && UnityEngine.Random.value < p.leakChancePerDangerous)
                {
                    int lost = Mathf.Max(1, Mathf.CeilToInt(entry.quantity * p.leakPercentOfStack));
                    int actual = Mathf.Min(lost, entry.quantity);
                    if (cargo.TryRemove(entry.itemId, actual, out _))
                    {
                        leakedAmount += actual;
                        if (p.verboseLogging)
                            Debug.LogWarning($"[TradeWorld] LEAK shipId={shipNetworkObjectId} item={def.itemId} qty={entry.quantity} lost={actual}");
                    }
                }

                if (def.isFragile && UnityEngine.Random.value < p.fragileChancePerItem)
                {
                    damagedAmount++;
                    if (p.verboseLogging)
                        Debug.LogWarning($"[TradeWorld] FRAGILE shipId={shipNetworkObjectId} item={def.itemId} qty={entry.quantity} (status=damaged TBD)");
                }
            }

            if (leakedAmount > 0)
            {
                Repository.SetCargo(shipNetworkObjectId, cargo.SaveToList());
                OnCargoChanged?.Invoke(shipNetworkObjectId);
                Debug.Log($"[TradeWorld] DAMAGE shipId={shipNetworkObjectId} energy={p.impactEnergy:F1} leaked={leakedAmount} damaged={damagedAmount}");
            }
            else if (damagedAmount > 0 && p.verboseLogging)
            {
                Debug.Log($"[TradeWorld] DAMAGE shipId={shipNetworkObjectId} energy={p.impactEnergy:F1} (no leak, {damagedAmount} fragile marked)");
            }
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
            bool loadedFromRepo = Repository.TryGetCargo(shipNetworkObjectId, out var items);
            if (loadedFromRepo)
                cargo.LoadFrom(items);
            _cargoCache[shipNetworkObjectId] = cargo;

            // T-CARGO-03: уведомляем подписчиков ТОЛЬКО если из репозитория пришли данные.
            // Для нового корабля (items пуст) событие не нужно — penalty и так = 1.0.
            if (loadedFromRepo && cargo.Items.Count > 0)
                OnCargoChanged?.Invoke(shipNetworkObjectId);

            return cargo;
        }

        public void InvalidateCargo(ulong shipNetworkObjectId)
        {
            // T-CARGO-03: уведомляем до удаления, чтобы подписчики успели
            // пересчитать penalty (= 1.0 для несуществующего корабля).
            bool existed = _cargoCache.ContainsKey(shipNetworkObjectId);
            _cargoCache.Remove(shipNetworkObjectId);
            if (existed)
                OnCargoChanged?.Invoke(shipNetworkObjectId);
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

        // ========================================================
        // T-CARGO-03: Штраф скорости от груза (для ShipController)
        // ========================================================
        // Формула перенесена из CargoSystem.GetSpeedPenalty() (удаляется в Этапе 5).
        //   speedMultiplier = 1.0 - (weight/maxWeight) * penaltyFactor
        //   перегруз: -20% за каждые 10% сверх лимита
        //   min = 0
        // penaltyFactor берётся из ShipClassLimits (CargoData.cs:138), а не хардкодится.
        // ========================================================
        public float GetSpeedPenalty(ulong shipNetworkObjectId, ShipClass shipClass)
        {
            var cargo = GetOrLoadCargo(shipNetworkObjectId, shipClass);
            if (cargo == null || Resolver == null) return 1.0f;

            // T-CARGO-06: per-instance лимиты через ShipCargoRegistry.
            // Если корабль зарегистрирован — лимиты с учётом модулей. Иначе fallback на статический.
            // T-CARGO-06-race: force-register если корабль заспавнен но не в registry.
            TryForceRegisterFromNetworkManager(shipNetworkObjectId);
            float maxWeight;
            float penaltyFactor;
            var effLimits = ProjectC.Ship.ShipCargoRegistry.GetEffectiveLimits(shipNetworkObjectId);
            if (effLimits != null)
            {
                maxWeight = effLimits.Value.maxWeight;
                penaltyFactor = effLimits.Value.penaltyFactor;
            }
            else
            {
                var staticLimits = ShipClassLimits.Get(shipClass);
                maxWeight = staticLimits.maxWeight;
                penaltyFactor = staticLimits.penaltyFactor;
            }

            float weight = cargo.ComputeTotalWeight(Resolver);
            float weightRatio = maxWeight > 0f ? weight / maxWeight : 0f;
            float speedMultiplier = 1.0f - weightRatio * penaltyFactor;

            // Перегруз: -20% за каждые полные 10% сверх лимита
            if (weightRatio > 1.0f)
            {
                float overloadPercent = (weightRatio - 1.0f) / 0.1f;
                float overloadPenalty = Mathf.Floor(overloadPercent) * 0.20f;
                speedMultiplier -= overloadPenalty;
            }

            return Mathf.Max(0f, speedMultiplier);
        }
    }
}
