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

    [Header("Rate Limiting")]
    [Tooltip("Максимум сделок в минуту на игрока")]
    [SerializeField] private int maxTradesPerMinute = 10;

    // Рынки локаций
    private Dictionary<string, LocationMarket> _markets = new Dictionary<string, LocationMarket>();

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
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        _tickTimer += Time.fixedDeltaTime;
        if (_tickTimer >= tickInterval)
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
                Debug.Log($"[TradeMarketServer] Загружен рынок: {market.locationName} ({market.locationId})");
            }
        }
#else
        // В билде — из Resources
        var markets = Resources.LoadAll<LocationMarket>("Trade/Markets");
        foreach (var market in markets)
        {
            if (!_markets.ContainsKey(market.locationId))
                _markets[market.locationId] = market;
        }
#endif

        Debug.Log($"[TradeMarketServer] Всего загружено рынков: {_markets.Count}");
    }

    // ==================== SERVERRPC: ТОРГОВЛЯ ====================

    /// <summary>
    /// Купить товар у NPC рынка
    /// </summary>
    [ServerRpc]
    public void BuyItemServerRpc(string itemId, int quantity, string locationId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
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

        // 3. Проверка стока
        if (marketItem.availableStock < quantity)
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Нет в наличии");
            SendTradeResultToClient(clientId, false, "Нет в наличии!", 0f, 0, 0, 0, 0);
            return;
        }

        // 4. Проверка кредитов игрока (через PlayerCreditsManager)
        var creditsManager = FindPlayerCredits(clientId);
        if (creditsManager == null)
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", "Менеджер кредитов не найден");
            SendTradeResultToClient(clientId, false, "Ошибка сервера!", 0f, 0, 0, 0, 0);
            return;
        }

        float totalCost = marketItem.currentPrice * quantity;
        if (creditsManager.Credits < totalCost)
        {
            LogTransaction(clientId, "BUY", itemId, quantity, "FAIL", $"Нет кредитов! Нужно {totalCost:F0}, есть {creditsManager.Credits:F0}");
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
        creditsManager.Credits -= totalCost;

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
        }

        // Лог
        LogTransaction(clientId, "BUY", itemId, quantity, "SUCCESS", $"За {totalCost:F0} CR");

        // Отправляем результат клиенту
        SendTradeResultToClient(clientId, true, $"Куплено {itemId} x{quantity} за {totalCost:F0} CR",
            creditsManager.Credits, marketItem.availableStock, 0, 0, 0);

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

        // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ — выполняем сделку ===
        // Продажа по 80% от текущей цены (NPC маржа)
        float sellPrice = marketItem.currentPrice * quantity * 0.8f;

        // Убираем товар со склада
        warehouseItem.quantity -= quantity;
        if (warehouseItem.quantity <= 0)
        {
            playerStorage.warehouse.Remove(warehouseItem);
        }

        // Начисляем кредиты
        var creditsManager = FindPlayerCredits(clientId);
        if (creditsManager != null)
        {
            creditsManager.Credits += sellPrice;
        }

        // Обновляем рынок: supply
        market.UpdateSupply(itemId, quantity * 0.02f);

        // Лог
        LogTransaction(clientId, "SELL", itemId, quantity, "SUCCESS", $"За {sellPrice:F0} CR");

        // Отправляем результат
        SendTradeResultToClient(clientId, true, $"Продано {itemId} x{quantity} за {sellPrice:F0} CR",
            creditsManager?.Credits ?? 0f, marketItem.availableStock + quantity, 0, 0, 0);

        // Обновляем рынок у всех
        var sellData = SerializeMarketData(market);
        SendMarketUpdateClientRpc(locationId, sellData.itemIds, sellData.prices, sellData.stocks, sellData.demands, sellData.supplies);
    }

    // ==================== CLIENTRPC: ОБНОВЛЕНИЯ ====================

    /// <summary>
    /// Отправить результат торговли конкретному клиенту
    /// Используем примитивные типы — NGO не умеет сериализовать кастомные структуры
    /// </summary>
    private void SendTradeResultToClient(ulong clientId, bool success, string message,
        float newCredits, int newStock, int newCargoWeight, int newCargoVolume, int newCargoSlots)
    {
        // Находим NetworkPlayer клиента и отправляем результат через него
        var player = FindPlayerNetworkPlayer(clientId);
        if (player != null)
        {
            player.TradeResultClientRpc(success, message, newCredits);
        }
        else
        {
            Debug.LogWarning($"[TradeMarketServer] Не удалось найти NetworkPlayer для клиента {clientId}");
        }
    }

    [ClientRpc]
    public void SendMarketUpdateClientRpc(string locationId, string itemIdsJson, string pricesJson, string stocksJson, string demandJson, string supplyJson)
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

            for (int i = 0; i < itemIds.Length && i < prices.Length; i++)
            {
                var mi = market.GetItem(itemIds[i]);
                if (mi != null)
                {
                    mi.currentPrice = float.Parse(prices[i]);
                    mi.availableStock = int.Parse(stocks[i]);
                    if (i < demands.Length) mi.demandFactor = float.Parse(demands[i]);
                    if (i < supplies.Length) mi.supplyFactor = float.Parse(supplies[i]);
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

    private void MarketTick()
    {
        // 1. Затухание спроса/предложения
        foreach (var market in _markets.Values)
        {
            market.DecaySupplyAndDemand(0.95f);
            market.RecalculatePrices();
        }

        // 2. Отправка обновлений клиентам
        foreach (var market in _markets.Values)
        {
            var data = SerializeMarketData(market);
            SendMarketUpdateClientRpc(market.locationId, data.itemIds, data.prices, data.stocks, data.demands, data.supplies);
        }

        Debug.Log($"[TradeMarketServer] MarketTick выполнен. Рынков: {_markets.Count}");
    }

    // ==================== УТИЛИТЫ ====================

    private bool CheckRateLimit(ulong clientId)
    {
        if (!_tradeTimestamps.ContainsKey(clientId))
        {
            _tradeTimestamps[clientId] = new List<float>();
        }

        var timestamps = _tradeTimestamps[clientId];
        if (timestamps.Count >= maxTradesPerMinute)
        {
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
        string logEntry = $"[{Time.time:F0}] {type} | Client:{clientId} | {itemId} x{quantity} | {status} | {details}";
        _transactionLog.Add(logEntry);
        Debug.Log($"[TradeMarketServer] {logEntry}");

        // Храним последние 1000 записей
        if (_transactionLog.Count > 1000)
        {
            _transactionLog.RemoveRange(0, _transactionLog.Count - 1000);
        }
    }

    private PlayerCreditsManager FindPlayerCredits(ulong clientId)
    {
        // Ищем среди всех NetworkPlayer
        var player = FindPlayerNetworkPlayer(clientId);
        if (player == null) return null;

        var credits = player.GetComponent<PlayerCreditsManager>();
        if (credits == null)
        {
            credits = player.gameObject.AddComponent<PlayerCreditsManager>();
        }
        return credits;
    }

    private PlayerTradeStorage FindPlayerStorage(ulong clientId)
    {
        var player = FindPlayerNetworkPlayer(clientId);
        if (player == null) return null;

        var storage = player.GetComponent<PlayerTradeStorage>();
        if (storage == null)
        {
            storage = player.gameObject.AddComponent<PlayerTradeStorage>();
        }
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
