using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Trade;
using ProjectC.Player;

/// <summary>
/// Серверный менеджер рынка — авторитетный источник всей торговой логики.
/// Сессия 5: Серверная торговля (NGO RPC).
/// 
/// Принцип: Клиент запрашивает → Сервер валидирует → Сервер считает → ClientRpc результат.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class TradeMarketServer : NetworkBehaviour
{
    public static TradeMarketServer Instance { get; private set; }

    [Header("Tick Settings")]
    [Tooltip("Интервал тика рынка (секунды). Host=300, Dedicated=120")]
    [SerializeField] private float tickInterval = 300f;

    [Tooltip("ТЕСТОВЫЙ РЕЖИМ: уменьшить tickInterval до 30 сек для быстрой проверки")]
    [SerializeField] private bool testMode = false;

    // Публичный доступ к tickInterval — защита от 0
    public float TickInterval
    {
        get
        {
            float val = testMode ? 30f : tickInterval;
            return val <= 0f ? 300f : val; // Защита: если 0, используем 300 сек
        }
    }

    [Header("NPC Traders — Session 6")]
    [Tooltip("Список NPC-трейдеров (серверная абстракция)")]
    [SerializeField] private List<NPCTrader> _npcTraders = new List<NPCTrader>();

    [Header("Market Events — Session 6")]
    [Tooltip("Список активных событий")]
    [SerializeField] private List<MarketEvent> _activeEvents = new List<MarketEvent>();

    [Tooltip("Автоматически инициализировать NPC-трейдеров при старте")]
    [SerializeField] private bool autoInitNPCTraders = true;

    [Header("Rate Limiting")]
    [Tooltip("Максимум сделок в минуту на игрока. 0 = без лимита (отключено для отладки)")]
    [SerializeField] private int maxTradesPerMinute = 0;

    // Рынки локаций
    private Dictionary<string, LocationMarket> _markets = new Dictionary<string, LocationMarket>();

    // Сессия 8E: Локальное хранение кредитов — НЕ через NetworkVariable
    // NetworkVariable не работает для компонентов созданных через AddComponent после спавна
    private Dictionary<ulong, float> _playerCredits = new Dictionary<ulong, float>();

    // Логирование транзакций
    private List<string> _transactionLog = new List<string>();

    // Rate limiting: clientId → список timestamps
    private Dictionary<ulong, List<float>> _tradeTimestamps = new Dictionary<ulong, List<float>>();

    private float _tickTimer = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Загружаем все рынки из Resources
        LoadAllMarkets();

        // Fallback: если OnNetworkSpawn не вызвался (объект создан вручную в иерархии),
        // пробуем инициализировать серверную сторону
        if (IsServer && _npcTraders.Count == 0)
        {
            InitServerSide();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        InitServerSide();
    }

    /// <summary>
    /// Инициализация серверной стороны. Вызывается из OnNetworkSpawn() или Start() как fallback.
    /// </summary>
    private void InitServerSide()
    {
        if (!IsServer) return;

        // Инициализируем NPC-трейдеров (Сессия 6)
        if (autoInitNPCTraders && _npcTraders.Count == 0)
        {
            InitDefaultNPCTraders();
        }

        // Инициализируем событие "Мезиевая лихорадка" (Сессия 6)
        if (_activeEvents.Count == 0)
        {
            InitDefaultMarketEvents();
        }
    }

    /// <summary>
    /// Инициализировать событие "Мезиевая лихорадка" (Сессия 6)
    /// Триггер: demandFactor мезия > 0.8 на любом рынке
    /// </summary>
    private void InitDefaultMarketEvents()
    {
        var mesiumRush = new MarketEvent
        {
            eventId = "mesium_rush_001",
            displayName = "Мезиевая лихорадка",
            displayIcon = "🔥",
            affectedItemId = "mesium_canister_v01",
            affectedLocations = new[] { "ALL" },
            demandMultiplier = 1.4f,   // спрос +40%
            supplyMultiplier = 1.0f,    // предложение не меняется
            durationTicks = 6,          // 30 мин (6 × 5 мин)
            cooldownTicks = 24,         // 2 часа кулдаун
            triggerType = TriggerType.DemandThreshold,
            triggerValue = 0.8f,        // demandFactor > 0.8
            isActive = false,
            cooldownRemaining = 0
        };

        _activeEvents.Add(mesiumRush);
    }

    /// <summary>
    /// Инициализировать 4 NPC-трейдеров по умолчанию (Сессия 6)
    /// </summary>
    private void InitDefaultNPCTraders()
    {
        // 1. ГосКонвой: Приму → Тертиус, мезий, 5-8, всегда
        _npcTraders.Add(NPCTrader.CreateDefault(
            "npc_state_convoy", "ГосКонвой",
            "primium", "tertius", "mesium_canister_v01",
            5, 8, TradeCondition.Always));

        // 2. Ветер: Приму → Секунд, антигравий, 3-5, всегда
        _npcTraders.Add(NPCTrader.CreateDefault(
            "npc_wind_trader", "Ветер",
            "primium", "secundus", "antigrav_ingot_v01",
            3, 5, TradeCondition.Always));

        // 3. Караванщик: Тертиус → Квартус, латекс, 8-12, при supplyFactor > 0.3
        _npcTraders.Add(NPCTrader.CreateDefault(
            "npc_caravan", "Караванщик",
            "tertius", "quartus", "latex_roll_v01",
            8, 12, TradeCondition.SupplyThreshold, 0.3f));

        // 4. Челнок: Секунд → Приму, мезий, 2-4, при цене > basePrice × 1.3
        _npcTraders.Add(NPCTrader.CreateDefault(
            "npc_shuttle", "Челнок",
            "secundus", "primium", "mesium_canister_v01",
            2, 4, TradeCondition.PriceThreshold, 1.3f));
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        float interval = TickInterval;
        if (interval <= 0f) interval = 300f; // Защита от нулевого интервала

        _tickTimer += Time.fixedDeltaTime;
        if (_tickTimer >= interval)
        {
            MarketTick();
            _tickTimer = 0f;
        }

        // Очистка старых timestamp (> 60 сек)
        CleanupOldTimestamps();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    // ==================== ИНИЦИАЛИЗАЦИЯ ====================

    private void LoadAllMarkets()
    {
#if UNITY_EDITOR
        // В редакторе загружаем из AssetDatabase
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LocationMarket");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var market = UnityEditor.AssetDatabase.LoadAssetAtPath<LocationMarket>(path);
            if (market != null && !_markets.ContainsKey(market.locationId))
            {
                _markets[market.locationId] = market;
                // Инициализация цен — ScriptableObject.OnEnable() может не вызваться
                market.InitItems();
                market.RecalculatePrices();
            }
        }
#else
        // В билде — из Resources
        var markets = Resources.LoadAll<LocationMarket>("Trade/Markets");
        foreach (var market in markets)
        {
            if (!_markets.ContainsKey(market.locationId))
            {
                _markets[market.locationId] = market;
                market.InitItems();
                market.RecalculatePrices();
            }
        }
#endif
    }

    // ==================== SERVERRPC: ТОРГОВЛЯ ====================

    /// <summary>
    /// Купить товар у NPC рынка
    /// </summary>
    [ServerRpc]
    public void BuyItemServerRpc(string itemId, int quantity, string locationId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // Сессия 8E: Валидация quantity > 0 — защита от эксплойта
        if (quantity <= 0)
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "quantity <= 0");
            SendTradeResultToClient(clientId, false, "Неверное количество!", 0f, 0, 0, 0, 0);
            return;
        }

        // Сессия 8E: Валидация locationId — защита от пустого ID
        if (string.IsNullOrEmpty(locationId))
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "locationId пустой");
            SendTradeResultToClient(clientId, false, "Локация не указана!", 0f, 0, 0, 0, 0);
            return;
        }

        // 1. Rate limiting
        if (!CheckRateLimit(clientId))
        {
            SendTradeResultToClient(clientId, false, "Слишком много запросов! Подождите.", 0f, 0, 0, 0, 0);
            return;
        }

        // 2. Валидация рынка
        if (!_markets.ContainsKey(locationId))
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Рынок не найден");
            SendTradeResultToClient(clientId, false, "Рынок не найден!", 0f, 0, 0, 0, 0);
            return;
        }

        var market = _markets[locationId];
        var marketItem = market.GetItem(itemId);
        if (marketItem == null)
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Товар не найден");
            SendTradeResultToClient(clientId, false, "Товар не найден!", 0f, 0, 0, 0, 0);
            return;
        }
        if (marketItem.item == null)
        {
            Debug.LogError($"[TradeMarketServer] MarketItem.item == null для {itemId}! Проверь ScriptableObject рынка {locationId}");
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "MarketItem.item == null!");
            SendTradeResultToClient(clientId, false, "Ошибка товара! (item=null)", 0f, 0, 0, 0, 0);
            return;
        }

        // 3. Проверка стока
        if (marketItem.availableStock < quantity)
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Нет в наличии");
            SendTradeResultToClient(clientId, false, "Нет в наличии!", 0f, 0, 0, 0, 0);
            return;
        }

        // Принудительный пересчёт цены — защита от stale данных
        marketItem.RecalculatePrice();

        // Сессия 8D: КРИТИЧНО — защита от нулевой цены
        if (marketItem.currentPrice <= 0f)
        {
            Debug.LogError($"[TradeMarketServer] КРИТИЧНО: currentPrice=0 для {itemId}! Отмена транзакции.");
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Цена товара = 0!");
            SendTradeResultToClient(clientId, false, "Ошибка цены товара! (price=0)", 0f, 0, 0, 0, 0);
            return;
        }

        // 4. Проверка кредитов игрока (локальный Dictionary — авторитетный источник)
        float playerCredits = GetPlayerCredits(clientId);

        float totalCost = marketItem.currentPrice * quantity;
        if (playerCredits < totalCost)
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", $"Нет кредитов! Нужно {totalCost:F0}, есть {playerCredits:F0}");
            SendTradeResultToClient(clientId, false, $"Нет кредитов! Нужно {totalCost:F0} CR", 0f, 0, 0, 0, 0);
            return;
        }

        // 5. Проверка лимитов склада игрока (через PlayerTradeStorage)
        var playerStorage = FindPlayerStorage(clientId);
        if (playerStorage != null)
        {
            var itemDef = marketItem.item;
            float newWeight = playerStorage.CurrentWeight + itemDef.weight * quantity;
            float newVolume = playerStorage.CurrentVolume + itemDef.volume * quantity;

            if (newWeight > playerStorage.maxWeight)
            {
                LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Превышен лимит веса склада");
                SendTradeResultToClient(clientId, false, "Превышен лимит веса склада!", 0f, 0, 0, 0, 0);
                return;
            }
            if (newVolume > playerStorage.maxVolume)
            {
                LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Превышен лимит объёма склада");
                SendTradeResultToClient(clientId, false, "Превышен лимит объёма склада!", 0f, 0, 0, 0, 0);
                return;
            }
            if (playerStorage.warehouse.Count >= playerStorage.maxItemTypes &&
                playerStorage.warehouse.Find(w => w.item == itemDef) == null)
            {
                LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Превышен лимит типов предметов");
                SendTradeResultToClient(clientId, false, "Превышен лимит типов предметов!", 0f, 0, 0, 0, 0);
                return;
            }
        }

        // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ — выполняем сделку ===

        // Списываем кредиты
        SetPlayerCredits(clientId, playerCredits - totalCost);
        float newCredits = GetPlayerCredits(clientId);
        Debug.Log($"[TradeMarketServer] BUY: newCredits={newCredits:F0} (was {playerCredits:F0}, cost {totalCost:F0})");

        // Обновляем рынок: сток и demand
        marketItem.availableStock -= quantity;
        market.UpdateDemand(itemId, quantity * 0.02f);

        // Добавляем товар на склад игрока
        if (playerStorage != null)
        {
            var itemDef = marketItem.item;
            var existing = playerStorage.warehouse.Find(w => w.item == itemDef);
            if (existing != null)
            {
                existing.quantity += quantity;
            }
            else
            {
                playerStorage.warehouse.Add(new WarehouseItem { item = itemDef, quantity = quantity });
            }
            // Сессия 8E: Синхронизируем credits с нашим авторитетным источником
            playerStorage.credits = newCredits;
            playerStorage.Save(); // Сессия 8C: сохраняем чтобы данные не терялись
        }

        // Лог
        LogTransaction(clientId, "BUY", itemId, quantity, "SUCCESS", $"За {totalCost:F0} CR");

        // Отправляем результат клиенту (Сессия 8C: itemId + quantity для синхронизации склада)
        SendTradeResultToClient(clientId, true, $"Куплено {itemId} x{quantity} за {totalCost:F0} CR",
            newCredits, marketItem.availableStock, 0, 0, 0,
            itemId, quantity, true);

        // Обновляем рынок у всех клиентов
        var buyData = SerializeMarketData(market);
        SendMarketUpdateClientRpc(locationId, buyData.itemIds, buyData.prices, buyData.stocks, buyData.demands, buyData.supplies);
    }

    /// <summary>
    /// Продать товар NPC рынку
    /// </summary>
    [ServerRpc]
    public void SellItemServerRpc(string itemId, int quantity, string locationId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // Сессия 8E: Валидация quantity > 0 — защита от эксплойта
        if (quantity <= 0)
        {
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "quantity <= 0");
            SendTradeResultToClient(clientId, false, "Неверное количество!", 0f, 0, 0, 0, 0);
            return;
        }

        // Сессия 8E: Валидация locationId — защита от пустого ID
        if (string.IsNullOrEmpty(locationId))
        {
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "locationId пустой");
            SendTradeResultToClient(clientId, false, "Локация не указана!", 0f, 0, 0, 0, 0);
            return;
        }

        // 1. Rate limiting
        if (!CheckRateLimit(clientId))
        {
            SendTradeResultToClient(clientId, false, "Слишком много запросов! Подождите.", 0f, 0, 0, 0, 0);
            return;
        }

        // 2. Валидация рынка
        if (!_markets.ContainsKey(locationId))
        {
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "Рынок не найден");
            SendTradeResultToClient(clientId, false, "Рынок не найден!", 0f, 0, 0, 0, 0);
            return;
        }

        var market = _markets[locationId];
        var marketItem = market.GetItem(itemId);
        if (marketItem == null)
        {
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "Товар не найден");
            SendTradeResultToClient(clientId, false, "Товар не найден!", 0f, 0, 0, 0, 0);
            return;
        }
        if (marketItem.item == null)
        {
            Debug.LogError($"[TradeMarketServer] MarketItem.item == null для {itemId} при продаже! Проверь ScriptableObject рынка {locationId}");
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "MarketItem.item == null!");
            SendTradeResultToClient(clientId, false, "Ошибка товара! (item=null)", 0f, 0, 0, 0, 0);
            return;
        }

        // 3. Проверка наличия у игрока
        var playerStorage = FindPlayerStorage(clientId);
        if (playerStorage == null)
        {
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "Склад игрока не найден");
            SendTradeResultToClient(clientId, false, "Ошибка сервера!", 0f, 0, 0, 0, 0);
            return;
        }

        var warehouseItem = playerStorage.warehouse.Find(w => w.item != null && w.item.itemId == itemId);
        if (warehouseItem == null || warehouseItem.quantity < quantity)
        {
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "Нет товара на складе");
            SendTradeResultToClient(clientId, false, "Нет товара на складе!", 0f, 0, 0, 0, 0);
            return;
        }

        // Принудительный пересчёт цены — защита от stale данных
        marketItem.RecalculatePrice();

        // Сессия 8D: КРИТИЧНО — защита от нулевой цены
        if (marketItem.currentPrice <= 0f)
        {
            Debug.LogError($"[TradeMarketServer] КРИТИЧНО: currentPrice=0 при продаже {itemId}! Отмена транзакции.");
            LogTransaction(clientId, "SELL", itemId, quantity, "FAIL", "Цена товара = 0!");
            SendTradeResultToClient(clientId, false, "Ошибка цены товара! (price=0)", 0f, 0, 0, 0, 0);
            return;
        }

        // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ — выполняем сделку ===
        // Продажа по 80% от текущей цены (NPC маржа)
        float sellPrice = marketItem.currentPrice * quantity * 0.8f;

        // Убираем товар со склада
        warehouseItem.quantity -= quantity;
        if (warehouseItem.quantity <= 0)
        {
            playerStorage.warehouse.Remove(warehouseItem);
        }

        // Начисляем кредиты (локальный Dictionary — авторитетный источник)
        float playerCredits = GetPlayerCredits(clientId);
        SetPlayerCredits(clientId, playerCredits + sellPrice);
        float newCredits = GetPlayerCredits(clientId);
        Debug.Log($"[TradeMarketServer] SELL: newCredits={newCredits:F0} (was {playerCredits:F0}, earned {sellPrice:F0})");

        // Синхронизируем playerStorage с нашим источником
        playerStorage.credits = newCredits;
        playerStorage.Save();

        // Обновляем рынок: supply
        market.UpdateSupply(itemId, quantity * 0.02f);

        // Лог
        LogTransaction(clientId, "SELL", itemId, quantity, "SUCCESS", $"За {sellPrice:F0} CR");

        // Отправляем результат (Сессия 8C: itemId + quantity для синхронизации склада)
        SendTradeResultToClient(clientId, true, $"Продано {itemId} x{quantity} за {sellPrice:F0} CR",
            newCredits, marketItem.availableStock + quantity, 0, 0, 0,
            itemId, quantity, false);

        // Обновляем рынок у всех
        var sellData = SerializeMarketData(market);
        SendMarketUpdateClientRpc(locationId, sellData.itemIds, sellData.prices, sellData.stocks, sellData.demands, sellData.supplies);
    }

    // ==================== CLIENTRPC: ОБНОВЛЕНИЯ ====================

    /// <summary>
    /// Отправить результат торговли конкретному клиенту
    /// Используем примитивные типы — NGO не умеет сериализовать кастомные структуры
    /// Сессия 8C: добавлены itemId и itemQuantity для синхронизации склада на клиенте
    /// </summary>
    private void SendTradeResultToClient(ulong clientId, bool success, string message,
        float newCredits, int newStock, int newCargoWeight, int newCargoVolume, int newCargoSlots,
        string itemId = "", int itemQuantity = 0, bool isPurchase = true)
    {
        Debug.Log($"[TradeMarketServer] SendTradeResultToClient: clientId={clientId}, success={success}, newCredits={newCredits:F0}");

        // Находим NetworkPlayer клиента и отправляем результат через него
        var player = FindPlayerNetworkPlayer(clientId);
        if (player != null)
        {
            player.TradeResultClientRpc(success, message, newCredits, itemId, itemQuantity, isPurchase);
        }
        else
        {
            Debug.LogWarning($"[TradeMarketServer] Не удалось найти NetworkPlayer для клиента {clientId}");
        }
    }

    [ClientRpc]
    public void SendMarketUpdateClientRpc(string locationId, string itemIdsJson, string pricesJson, string stocksJson, string demandJson, string supplyJson, string eventMultipliersJson = "")
    {
        // Обновляем локальный рынок
        if (TradeUI.Instance != null && TradeUI.Instance.currentMarket != null &&
            TradeUI.Instance.currentMarket.locationId == locationId)
        {
            var market = TradeUI.Instance.currentMarket;
            // Парсим JSON и обновляем MarketItem
            var itemIds = itemIdsJson.Split(',');
            var prices = pricesJson.Split(',');
            var stocks = stocksJson.Split(',');
            var demands = demandJson.Split(',');
            var supplies = supplyJson.Split(',');
            var eventMultipliers = string.IsNullOrEmpty(eventMultipliersJson)
                ? null
                : eventMultipliersJson.Split(',');

            for (int i = 0; i < itemIds.Length && i < prices.Length; i++)
            {
                var mi = market.GetItem(itemIds[i]);
                if (mi != null)
                {
                    mi.currentPrice = float.Parse(prices[i]);
                    mi.availableStock = int.Parse(stocks[i]);
                    if (i < demands.Length) mi.demandFactor = float.Parse(demands[i]);
                    if (i < supplies.Length) mi.supplyFactor = float.Parse(supplies[i]);
                    if (eventMultipliers != null && i < eventMultipliers.Length)
                        mi.eventMultiplier = float.Parse(eventMultipliers[i]);
                }
            }

            TradeUI.Instance.RenderItems();
            TradeUI.Instance.UpdateDisplays();
        }
    }

    // ==================== TICK СИСТЕМА ====================

    // ==================== СЕРИАЛИЗАЦИЯ ====================

    /// <summary>
    /// Сериализовать данные рынка в строки CSV для ClientRPC
    /// </summary>
    private (string itemIds, string prices, string stocks, string demands, string supplies) SerializeMarketData(LocationMarket market)
    {
        var itemIds = new System.Text.StringBuilder();
        var prices = new System.Text.StringBuilder();
        var stocks = new System.Text.StringBuilder();
        var demands = new System.Text.StringBuilder();
        var supplies = new System.Text.StringBuilder();

        foreach (var mi in market.items)
        {
            if (mi.item == null) continue;
            if (itemIds.Length > 0)
            {
                itemIds.Append(',');
                prices.Append(',');
                stocks.Append(',');
                demands.Append(',');
                supplies.Append(',');
            }
            itemIds.Append(mi.item.itemId);
            prices.Append(mi.currentPrice.ToString("F2"));
            stocks.Append(mi.availableStock.ToString());
            demands.Append(mi.demandFactor.ToString("F3"));
            supplies.Append(mi.supplyFactor.ToString("F3"));
        }

        return (itemIds.ToString(), prices.ToString(), stocks.ToString(), demands.ToString(), supplies.ToString());
    }

    /// <summary>
    /// Основной тик рынка — вызывается каждые TickInterval секунд (Сессия 6: полный)
    /// [ContextMenu] позволяет вызвать вручную из Inspector в Unity
    /// </summary>
    [ContextMenu("Вызвать MarketTick вручную")]
    private void MarketTick()
    {
        if (!IsServer) return;

        // 1. NPC-трейдеры перемещают товары
        ProcessNPCTrades();

        // 2. Проверка и обновление событий
        UpdateEvents();

        // 3. Затухание спроса/предложения (elastic "качели") + пассивная регенерация стока
        foreach (var market in _markets.Values)
        {
            market.DecaySupplyAndDemand(0.92f, 0.08f);
        }

        // 4. Пересчёт цен (с eventMultiplier)
        foreach (var market in _markets.Values)
        {
            market.RecalculatePrices();
        }

        // 5. Delta-отправка обновлений клиентам (только изменённые предметы)
        SendDeltaUpdatesToClients();

        // 6. Уменьшаем кулдауны событий
        foreach (var evt in _activeEvents)
        {
            if (!evt.isActive)
                evt.TickCooldown();
        }

        int activeEventCount = 0;
        foreach (var evt in _activeEvents)
        {
            if (evt.isActive) activeEventCount++;
        }

        // Информативный лог для отладки tick-системы (только при аномалиях)
        foreach (var market in _markets.Values)
        {
            foreach (var item in market.items)
            {
                if (item != null && item.currentPrice <= 0f && item.item != null)
                {
                    Debug.LogWarning($"[TradeMarketServer] MarketTick: price=0 для {item.item.itemId} на {market.locationId} (basePrice={item.basePrice}, d={item.demandFactor:F2}, s={item.supplyFactor:F2})");
                }
            }
        }
    }

    // ==================== NPC-ТРЕЙДЕРЫ (Сессия 6) ====================

    /// <summary>
    /// Обработать торговлю всех NPC-трейдеров
    /// </summary>
    private void ProcessNPCTrades()
    {
        foreach (var trader in _npcTraders)
        {
            if (!_markets.ContainsKey(trader.fromLocationId) ||
                !_markets.ContainsKey(trader.toLocationId)) continue;

            var fromMarket = _markets[trader.fromLocationId];
            var toMarket = _markets[trader.toLocationId];

            if (trader.ShouldTrade(fromMarket, toMarket))
            {
                trader.ExecuteTrade(_markets);
            }
        }
    }

    // ==================== СОБЫТИЯ РЫНКА (Сессия 6) ====================

    /// <summary>
    /// Обновить активные события: проверка триггеров, таймеры, окончание
    /// </summary>
    private void UpdateEvents()
    {
        // Проверка триггеров для неактивных событий
        foreach (var evt in _activeEvents)
        {
            if (!evt.isActive && evt.cooldownRemaining <= 0 && evt.ShouldTrigger(_markets))
            {
                evt.Activate();

                // Применить эффект ко всем рынкам
                foreach (var market in _markets.Values)
                {
                    evt.ApplyToAllAffectedItems(market);
                }

                BroadcastEventClientRpc(evt.eventId, evt.displayName, evt.displayIcon,
                    evt.durationTicks, evt.affectedItemId);
            }
        }

        // Обновление активных событий
        foreach (var evt in _activeEvents)
        {
            if (evt.isActive && evt.IsExpired())
            {
                // Убрать эффект со всех рынков
                foreach (var market in _markets.Values)
                {
                    evt.RemoveFromAllAffectedItems(market);
                }

                evt.Deactivate();
                RemoveEventClientRpc(evt.eventId);
            }
        }
    }

    /// <summary>
    /// Создать новое событие рынка (вызывается из кода или админ-команды)
    /// </summary>
    public void StartMarketEvent(MarketEvent newEvent)
    {
        _activeEvents.Add(newEvent);
        newEvent.Activate();

        foreach (var market in _markets.Values)
        {
            newEvent.ApplyToAllAffectedItems(market);
        }

        BroadcastEventClientRpc(newEvent.eventId, newEvent.displayName, newEvent.displayIcon,
            newEvent.durationTicks, newEvent.affectedItemId);
    }

    // ==================== DELTA-ОТПРАВКА (Сессия 6) ====================

    /// <summary>
    /// Отправить клиентам только изменённые предметы (isDirty flag)
    /// </summary>
    private void SendDeltaUpdatesToClients()
    {
        foreach (var market in _markets.Values)
        {
            var dirtyItems = market.GetDirtyItems();
            if (dirtyItems.Count == 0) continue;

            var data = SerializeMarketDataDelta(market, dirtyItems);
            SendMarketUpdateClientRpc(market.locationId, data.itemIds, data.prices,
                data.stocks, data.demands, data.supplies, data.eventMultipliers);

            // Сброс isDirty
            market.ClearDirtyFlags();
        }
    }

    /// <summary>
    /// Сериализовать только изменённые предметы
    /// </summary>
    private (string itemIds, string prices, string stocks, string demands, string supplies, string eventMultipliers)
        SerializeMarketDataDelta(LocationMarket market, List<MarketItem> dirtyItems)
    {
        var itemIds = new System.Text.StringBuilder();
        var prices = new System.Text.StringBuilder();
        var stocks = new System.Text.StringBuilder();
        var demands = new System.Text.StringBuilder();
        var supplies = new System.Text.StringBuilder();
        var eventMultipliers = new System.Text.StringBuilder();

        foreach (var mi in dirtyItems)
        {
            if (mi.item == null) continue;
            if (itemIds.Length > 0)
            {
                itemIds.Append(',');
                prices.Append(',');
                stocks.Append(',');
                demands.Append(',');
                supplies.Append(',');
                eventMultipliers.Append(',');
            }
            itemIds.Append(mi.item.itemId);
            prices.Append(mi.currentPrice.ToString("F2"));
            stocks.Append(mi.availableStock.ToString());
            demands.Append(mi.demandFactor.ToString("F3"));
            supplies.Append(mi.supplyFactor.ToString("F3"));
            eventMultipliers.Append(mi.eventMultiplier.ToString("F3"));
        }

        return (itemIds.ToString(), prices.ToString(), stocks.ToString(),
                demands.ToString(), supplies.ToString(), eventMultipliers.ToString());
    }

    // ==================== CLIENTRPC: СОБЫТИЯ (Сессия 6) ====================

    /// <summary>
    /// Уведомить всех клиентов о начале события
    /// </summary>
    [ClientRpc]
    private void BroadcastEventClientRpc(string eventId, string displayName, string displayIcon, int durationTicks, string affectedItemId)
    {
        if (TradeUI.Instance != null)
        {
            TradeUI.Instance.OnMarketEventStarted(eventId, displayName, displayIcon, durationTicks, affectedItemId);
        }
    }

    /// <summary>
    /// Уведомить всех клиентов об окончании события
    /// </summary>
    [ClientRpc]
    private void RemoveEventClientRpc(string eventId)
    {
        if (TradeUI.Instance != null)
        {
            TradeUI.Instance.OnMarketEventEnded(eventId);
        }
    }

    /// <summary>
    /// Отправить все активные события новому клиенту при подключении
    /// </summary>
    [ClientRpc]
    public void SendActiveEventsClientRpc(string eventIdsJson, string displayNamesJson, string displayIconsJson, string durationsJson, string affectedItemIdsJson)
    {
        var eventIds = string.IsNullOrEmpty(eventIdsJson) ? new string[0] : eventIdsJson.Split(',');
        var displayNames = string.IsNullOrEmpty(displayNamesJson) ? new string[0] : displayNamesJson.Split(',');
        var displayIcons = string.IsNullOrEmpty(displayIconsJson) ? new string[0] : displayIconsJson.Split(',');
        var durations = string.IsNullOrEmpty(durationsJson) ? new string[0] : durationsJson.Split(',');
        var affectedItemIds = string.IsNullOrEmpty(affectedItemIdsJson) ? new string[0] : affectedItemIdsJson.Split(',');

        if (TradeUI.Instance != null)
        {
            for (int i = 0; i < eventIds.Length; i++)
            {
                TradeUI.Instance.OnMarketEventStarted(
                    eventIds[i],
                    i < displayNames.Length ? displayNames[i] : "",
                    i < displayIcons.Length ? displayIcons[i] : "",
                    i < durations.Length ? int.Parse(durations[i]) : 0,
                    i < affectedItemIds.Length ? affectedItemIds[i] : "");
            }
        }
    }

    /// <summary>
    /// Публичный метод для отправки активных событий конкретному клиенту
    /// </summary>
    public void SendActiveEventsToClient(ulong clientId)
    {
        var activeEvents = _activeEvents.FindAll(e => e.isActive);
        if (activeEvents.Count == 0) return;

        var ids = new System.Text.StringBuilder();
        var names = new System.Text.StringBuilder();
        var icons = new System.Text.StringBuilder();
        var durations = new System.Text.StringBuilder();
        var itemIds = new System.Text.StringBuilder();

        foreach (var evt in activeEvents)
        {
            if (ids.Length > 0)
            {
                ids.Append(',');
                names.Append(',');
                icons.Append(',');
                durations.Append(',');
                itemIds.Append(',');
            }
            ids.Append(evt.eventId);
            names.Append(evt.displayName);
            icons.Append(evt.displayIcon);
            durations.Append(evt.remainingTicks.ToString());
            itemIds.Append(evt.affectedItemId);
        }

        // ClientRPC шлётся всем, но TradeUI обработает только для себя
        // Для отправки конкретному клиенту нужен отдельный механизм
        // Пока шлём всем — это упрощение
        SendActiveEventsClientRpc(ids.ToString(), names.ToString(), icons.ToString(),
            durations.ToString(), itemIds.ToString());
    }

    // ==================== УТИЛИТЫ ====================

    private bool CheckRateLimit(ulong clientId)
    {
        if (maxTradesPerMinute <= 0) return true; // Лимит отключён

        if (!_tradeTimestamps.ContainsKey(clientId))
        {
            _tradeTimestamps[clientId] = new List<float>();
        }

        var timestamps = _tradeTimestamps[clientId];
        if (timestamps.Count >= maxTradesPerMinute)
        {
            Debug.LogWarning($"[TradeMarketServer] Rate limit! Client:{clientId} сделал {timestamps.Count} запросов (лимит: {maxTradesPerMinute}/мин)");
            return false;
        }

        timestamps.Add(Time.time);
        return true;
    }

    private void CleanupOldTimestamps()
    {
        float cutoff = Time.time - 60f;
        foreach (var kvp in _tradeTimestamps)
        {
            kvp.Value.RemoveAll(t => t < cutoff);
        }
    }

    private void LogTransaction(ulong clientId, string type, string itemId, int quantity, string status, string details)
    {
        string logEntry = $"[TradeMarketServer] {type} | Client:{clientId} | {itemId} x{quantity} | {status} | {details}";
        _transactionLog.Add(logEntry);
        Debug.Log(logEntry);

        // Храним последние 1000 записей
        if (_transactionLog.Count > 1000)
        {
            _transactionLog.RemoveRange(0, _transactionLog.Count - 1000);
        }
    }

    /// <summary>
    /// Получить кредиты игрока (авторитетный источник — локальный Dictionary + PlayerPrefs)
    /// </summary>
    public static float GetPlayerCreditsStatic(ulong clientId)
    {
        if (Instance == null) return 1000f;
        return Instance.GetPlayerCredits(clientId);
    }

    private float GetPlayerCredits(ulong clientId)
    {
        if (_playerCredits.TryGetValue(clientId, out float credits))
            return credits;

        // Загружаем из PlayerPrefs если ещё не в кэше
        credits = PlayerPrefs.GetFloat($"Credits_{clientId}", 1000f);
        _playerCredits[clientId] = credits;
        return credits;
    }

    /// <summary>
    /// Установить кредиты игрока (сохраняет в Dictionary + PlayerPrefs)
    /// </summary>
    public static void SetPlayerCreditsStatic(ulong clientId, float newCredits)
    {
        if (Instance == null) return;
        Instance.SetPlayerCredits(clientId, newCredits);
    }

    private void SetPlayerCredits(ulong clientId, float newCredits)
    {
        newCredits = Mathf.Max(0f, newCredits);
        _playerCredits[clientId] = newCredits;
        PlayerPrefs.SetFloat($"Credits_{clientId}", newCredits);
        PlayerPrefs.Save();
    }

    private PlayerTradeStorage FindPlayerStorage(ulong clientId)
    {
        var player = FindPlayerNetworkPlayer(clientId);
        if (player == null) return null;

        var storage = player.GetComponent<PlayerTradeStorage>();
        bool isNew = (storage == null);
        if (isNew)
        {
            storage = player.gameObject.AddComponent<PlayerTradeStorage>();
        }

        // Сессия 8E: Загружаем склад из PlayerPrefs (items)
        storage.Load();

        // Сессия 8E: Синхронизируем credits с авторитетным Dictionary _playerCredits
        // При первом создании storage.credits = 1000 (default). Берём из Dictionary.
        storage.credits = GetPlayerCredits(clientId);

        return storage;
    }

    private NetworkPlayer FindPlayerNetworkPlayer(ulong clientId)
    {
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
        foreach (var player in players)
        {
            if (player.OwnerClientId == clientId)
                return player;
        }
        return null;
    }
}
