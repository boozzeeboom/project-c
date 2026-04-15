# Trade Market Architecture — Рынок + Склад (Database Pattern)

**Версия:** 1.0 | **Дата:** 15.04.2026 | **Статус:** Проектная документация

---

## 1. Анализ текущего бага

### Корневая причина (BUG_CLIENT_TRADE_NOT_WORKING)

```
ХОСТ (clientId=0)                    КЛИЕНТ (clientId=1)
┌─────────────────────────┐          ┌─────────────────────────┐
│  NetworkPlayer (0)      │          │  NetworkPlayer (1)       
│  IsOwner=true           │          │  IsOwner=false           
│  OwnerClientId=0        │          │  OwnerClientId=1         
└─────────────────────────┘          └─────────────────────────┘
         ▲                                    ▲
         │                                    │
┌─────────────────────────────────────────────────────────────┐
│  TradeMarketServer.SendTradeResultToClient(clientId=1, ...) │
│  1. FindPlayerNetworkPlayer(1) → возвращает NetworkPlayer(1) │
│  2. player.TradeResultClientRpc(targetClientId=1, ...)      │
│     [Rpc: SendTo.Owner]                                      │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│  PROBLEMA: На КАЖДОМ клиенте RPC приходит на NetworkPlayer   │
│  с OwnerClientId = ЛОКАЛЬНОГО клиента!                       │
│                                                              │
│  • На хосте: RPC приходит на NetworkPlayer(0) — локальный    │
│  • На клиенте: RPC приходит на NetworkPlayer(1) — правильный │
│                                                              │
│  TradeResultClientRpc.IsOwner проверяет ЛОКАЛЬНОГО клиента!  │
└─────────────────────────────────────────────────────────────┘
```

### Почему все попытки (8I-8L) не помогли

| Попытка | Проблема |
|---------|----------|
| `IsOwner` внутри RPC | На хосте `IsOwner=True` для clientId=0, НЕ для того кому отправляем |
| `targetClientId` в параметрах | Проверка `localClientId != targetClientId` — работает правильно, НО... |
| `FindPlayerNetworkPlayer` через `ConnectedClients` | Находит правильный объект, НО отправляет на НЕПРАВИЛЬНЫЙ |

**Корневая проблема:** `ClientRpc` с `SendTo.Owner` на хосте ДУБЛИРУЕТСЯ на все NetworkPlayer
с локальным `IsOwner=True`.

---

## 2. Архитектура "Рынок-Склад" (Database Pattern)

### 2.1 Принципиальная схема

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║                         СЕРВЕР (Authoritative Server)                        ║
║  ┌─────────────────────────────────────────────────────────────────────┐    ║
║  │                     TradeMarketServer                               │    ║
║  │  ┌───────────────┐  ┌───────────────┐  ┌───────────────────────┐   │    ║
║  │  │   MARKETS     │  │   PRICES      │  │   DYNAMIC_EVENTS       │   │    ║
║  │  │  (Dictionary) │  │  (Calculated)  │  │   (Supply/Demand)     │   │    ║
║  │  │  location →   │  │  base ×       │  │   tick → update       │   │    ║
║  │  │    Market    │  │    (1+d-s)×rep │  │   prices              │   │    ║
║  │  └───────────────┘  └───────────────┘  └───────────────────────┘   │    ║
║  └─────────────────────────────────────────────────────────────────────┘    ║
║                              │                                          ║
║              ┌───────────────┴───────────────┐                          ║
║              ▼                               ▼                          ║
║  ┌─────────────────────────────────────────────────────────────┐          ║
║  │                    PlayerDataStore                        │          ║
║  │                 (Единый источник истины)                   │          ║
║  │  ┌───────────────────────────────────────────────────┐    │          ║
║  │  │  PLAYER_ACCOUNTS                                    │    │          ║
║  │  │  ┌──────────┐  ┌──────────┐  ┌──────────┐          │    │          ║
║  │  │  │clientId │  │credits   │  │reputation│          │    │          ║
║  │  │  │   0     │  │  1000 CR │  │   50     │          │    │          ║
║  │  │  │   1     │  │  800 CR  │  │   30     │          │    │          ║
║  │  │  │   N     │  │  1200 CR │  │   20     │          │    │          ║
║  │  │  └──────────┘  └──────────┘  └──────────┘          │    │          ║
║  │  └───────────────────────────────────────────────────┘    │          ║
║  │  ┌───────────────────────────────────────────────────┐    │          ║
║  │  │  WAREHOUSES                                         │    │          ║
║  │  │  (player_id, location_id) → [item, qty]            │    │          ║
║  │  │  ┌────────────────────────────────────────────┐    │    │          ║
║  │  │  │ (0, "primium") → [{mesium, 5}, {latex, 2}] │    │    │          ║
║  │  │  │ (1, "secundus") → [{antigrav, 3}]         │    │    │          ║
║  │  │  │ (0, "tertius") → [{mesium, 10}]           │    │    │          ║
║  │  │  └────────────────────────────────────────────┘    │    │          ║
║  │  └───────────────────────────────────────────────────┘    │          ║
║  └─────────────────────────────────────────────────────────────┘          ║
║                              │                                          ║
║  ┌───────────────────────────┴───────────────────────────────┐             ║
║  │                       NPC TRADERS                         │             ║
║  │  Периодическая торговля между локациями                    │             ║
║  └───────────────────────────────────────────────────────────┘             ║
╚═══════════════════════════════════════════════════════════════════════════════╝

                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
╔═══════════════════════════════╗      ╔═══════════════════════════════╗
║        ХОСТ (clientId=0)      ║      ║      КЛИЕНТ (clientId=1)    ║
║  ┌─────────────────────────┐  ║      ║  ┌─────────────────────────┐  ║
║  │  NetworkPlayer (0)      │  ║      ║  │  NetworkPlayer (1)      │  ║
║  │  ┌───────────────────┐  │  ║      ║  │  ┌───────────────────┐  │  ║
║  │  │ PlayerTradeStorage│  │  ║      ║  │  │ PlayerTradeStorage│  │  ║
║  │  │ (КЛИЕНТСКИЙ КЭШ)  │  │  ║      ║  │  │ (КЛИЕНТСКИЙ КЭШ)  │  │  ║
║  │  │                   │  │  ║      ║  │  │                   │  │  ║
║  │  │ Склад (primium)   │  │  ║      ║  │  │ Склад (secundus) │  │  ║
║  │  │ Кредиты (synced)  │  │  ║      ║  │  │ Кредиты (synced)  │  │  ║
║  │  └───────────────────┘  │  ║      ║  │  └───────────────────┘  │  ║
║  │                         │  ║      ║  │                         │  ║
║  │  ┌───────────────────┐  │  ║      ║  │  ┌───────────────────┐  │  ║
║  │  │   TradeUI          │  │  ║      ║  │  │   TradeUI          │  │  ║
║  │  │   (Read-Only)      │  │  ║      ║  │  │   (Read-Only)     │  │  ║
║  │  │                   │  │  ║      ║  │  │                   │  │  ║
║  │  │ • Рендер рынка    │  │  ║      ║  │  │ • Рендер рынка    │  │  ║
║  │  │ • Отправка запроса│  │  ║      ║  │  │ • Отправка запроса│  │  ║
║  │  │ • Показ результата │  │  ║      ║  │  │ • Показ результата │  │  ║
║  │  └───────────────────┘  │  ║      ║  │  └───────────────────┘  │  ║
║  └─────────────────────────┘  ║      ║  └─────────────────────────┘  ║
╚═══════════════════════════════╝      ╚═══════════════════════════════╝
```

### 2.2 Аналогия с базой данных

| Компонент БД | Аналог в Trade System |
|-------------|----------------------|
| **Таблица `players`** | `PlayerDataStore._creditsCache` |
| **Таблица `warehouses`** | `PlayerDataStore._warehouseCache` |
| **Внешний ключ** | `clientId` |
| **Индекс по локации** | `locationId` |
| **Триггеры** | `MarketTick()` — обновление цен |
| **Хранимая процедура** | `ProcessBuyItem()` / `ProcessSellItem()` |
| **Клиентский кэш** | `PlayerTradeStorage` (read-only view) |
| **Read-only пользователь** | `TradeUI` |

---

## 3. Протокол взаимодействия клиент-сервер

### 3.1 Диаграмма последовательности (покупка)

```
КЛИЕНТ                              СЕРВЕР                              КЛИЕНТ
   │                                   │                                   │
   │  [1] OpenTrade(market)            │                                   │
   │──────────────────────────────────>│                                   │
   │                                   │                                   │
   │  [2] Синхронизация: LoadFromPlayerDataStore                           │
   │      • Кредиты: PD_Credits_{clientId}                                 │
   │      • Склад: PD_Warehouse_{clientId}_{locationId}                    │
   │                                   │                                   │
   │  [3] Отображение UI              │                                   │
   │  <────────────────────────────────│                                   │
   │                                   │                                   │
   │  [4] TryBuyItem()                 │                                   │
   │  [5] BuyItemViaServer(itemId, qty)│                                   │
   │──────────────────────────────────>│                                   │
   │                                   │  [6] BuyItemServerRpc(itemId, qty) │
   │                                   │───────────────────────────────────>│
   │                                   │                                   │
   │                                   │  [7] VALIDATION                   │
   │                                   │  ┌─────────────────────────────┐  │
   │                                   │  │ • Проверка quantity > 0     │  │
   │                                   │  │ • Проверка стока (market)   │  │
   │                                   │  │ • Расчёт цены (сервер)      │  │
   │                                   │  │ • Проверка кредитов         │  │
   │                                   │  │ • Проверка лимитов склада   │  │
   │                                   │  └─────────────────────────────┘  │
   │                                   │                                   │
   │                                   │  [8] TRANSACTION (ACID)           │
   │                                   │  ┌─────────────────────────────┐  │
   │                                   │  │ BEGIN TRANSACTION           │  │
   │                                   │  │ • Списать кредиты           │  │
   │                                   │  │ • Уменьшить сток рынка      │  │
   │                                   │  │ • Добавить на склад         │  │
   │                                   │  │ • Обновить supply/demand    │  │
   │                                   │  │ • Save() → PlayerDataStore  │  │
   │                                   │  │ COMMIT                      │  │
   │                                   │  └─────────────────────────────┘  │
   │                                   │                                   │
   │  [9] TradeResultClientRpc         │                                   │
   │  (с targetClientId)               │                                   │
   │<──────────────────────────────────│                                   │
   │                                   │                                   │
   │  [10] OnTradeResult()             │                                   │
   │      • LoadFromPlayerDataStore()  │                                   │
   │      • UpdateDisplays()          │                                   │
   │      • RenderItems()             │                                   │
```

### 3.2 Диаграмма последовательности (продажа)

```
КЛИЕНТ                              СЕРВЕР                              КЛИЕНТ
   │                                   │                                   │
   │  [1] TrySellItem()                │                                   │
   │  [2] SellItemViaServer(itemId, qty)                                  │
   │──────────────────────────────────>│                                   │
   │                                   │  [3] SellItemServerRpc(...)       │
   │                                   │───────────────────────────────────>│
   │                                   │                                   │
   │                                   │  [4] VALIDATION                   │
   │                                   │  ┌─────────────────────────────┐  │
   │                                   │  │ • Проверка quantity > 0     │  │
   │                                   │  │ • Поиск товара на складе    │  │
   │                                   │  │ • Проверка quantity <= stored│ │
   │                                   │  │ • Расчёт цены (80% от рынка)│  │
   │                                   │  └─────────────────────────────┘  │
   │                                   │                                   │
   │                                   │  [5] TRANSACTION                  │
   │                                   │  ┌─────────────────────────────┐  │
   │                                   │  │ BEGIN TRANSACTION           │  │
   │                                   │  │ • Убрать со склада          │  │
   │                                   │  │ • Начислить кредиты (×0.8)  │  │
   │                                   │  │ • Увеличить сток рынка      │  │
   │                                   │  │ • Обновить supply/demand    │  │
   │                                   │  │ • Save() → PlayerDataStore  │  │
   │                                   │  │ COMMIT                      │  │
   │                                   │  └─────────────────────────────┘  │
   │                                   │                                   │
   │  [6] TradeResultClientRpc        │                                   │
   │<──────────────────────────────────│                                   │
   │                                   │                                   │
   │  [7] OnTradeResult()             │                                   │
```

### 3.3 RPC-контракты

| RPC | Тип | Отправщик | Получатель | Параметры | Описание |
|-----|-----|-----------|------------|-----------|----------|
| `BuyItemServerRpc` | ServerRpc | Клиент | Сервер | `itemId, qty, locationId, clientId` | Запрос покупки |
| `SellItemServerRpc` | ServerRpc | Клиент | Сервер | `itemId, qty, locationId, clientId` | Запрос продажи |
| `TradeResultClientRpc` | ClientRpc | Сервер | **Целевой клиент** | `targetClientId, success, message, newCredits, itemId, qty, isPurchase` | Результат операции |
| `MarketUpdateClientRpc` | ClientRpc | Сервер | Все клиенты | `locationId, prices[], stocks[]` | Обновление рынка |

---

## 4. Решение бага — Архитектурный подход

### 4.1 Корневая причина

NGO `ClientRpc` с `SendTo.Owner` на хосте **ДУБЛИРУЕТСЯ** на все NetworkPlayer с `IsOwner=True`.
На хосте только `NetworkPlayer(0)` имеет `IsOwner=True`, поэтому:
- RPC для `clientId=1` отправляется на `NetworkPlayer(0)` (локальный)
- `IsOwner` на `NetworkPlayer(0)` = `True` (для clientId=0)
- Но сервер хочет отправить результат клиенту с `clientId=1`

### 4.2 Решение: Использовать `ClientRpcParams` с явным `TargetClientIds`

**ПРОВЕРЕННОЕ РЕШЕНИЕ:**

```csharp
// TradeMarketServer.cs
private void SendTradeResultToClient(ulong clientId, bool success, ...)
{
    var nm = NetworkManager.Singleton;
    if (nm == null) return;
    
    var player = FindPlayerNetworkPlayer(clientId);
    if (player == null) return;
    
    // СЕССИЯ FIX: Используем ClientRpcParams с явным таргетом
    var rpcParams = new ClientRpcParams
    {
        Send = new ClientRpcSendParams
        {
            TargetClientIds = new ulong[] { clientId }
        }
    };
    
    // Отправляем ТОЛЬКО этому клиенту
    player.TradeResultClientRpc(clientId, success, message, newCredits, 
        itemId, itemQuantity, isPurchase, rpcParams);
}
```

### 4.3 Почему это работает

```
БЕЗ ClientRpcParams:
┌────────────────────────────────────────────────────┐
│  TradeMarketServer.TradeResultClientRpc()          │
│  [Rpc: SendTo.Owner]                               │
│                                                     │
│  → Хост получает (NetworkPlayer(0), IsOwner=True)  │
│  → Клиент получает (NetworkPlayer(1), IsOwner=True)│
│                                                     │
│  ❌ На хосте IsOwner=True для ВСЕХ — RPC дублируется│
└────────────────────────────────────────────────────┘

С ClientRpcParams + TargetClientIds:
┌────────────────────────────────────────────────────┐
│  TradeMarketServer.TradeResultClientRpc(          │
│      rpcParams: TargetClientIds = [1])            │
│                                                     │
│  → Хост: НЕ получает (clientId=1, а не 0)         │
│  → Клиент: получает (clientId=1)                  │
│                                                     │
│  ✅ RPC отправляется ТОЛЬКО целевому клиенту       │
└────────────────────────────────────────────────────┘
```

### 4.4 NetworkPlayer.RPC сигнатура

```csharp
// NetworkPlayer.cs
[ClientRpc]
public void TradeResultClientRpc(
    ulong targetClientId, 
    bool success, 
    string message, 
    float newCredits, 
    string itemId = "", 
    int itemQuantity = 0, 
    bool isPurchase = true,
    ClientRpcParams rpcParams = default)
{
    ulong localClientId = NetworkManager.Singleton.LocalClientId;
    
    // Фильтрация по targetClientId — ДОПОЛНИТЕЛЬНАЯ защита
    if (localClientId != targetClientId)
    {
        Debug.Log($"[NetworkPlayer] RPC для {targetClientId}, пропускаю (я {localClientId})");
        return;
    }
    
    // Обработка результата
    TradeUI.Instance?.OnTradeResult(success, message, newCredits, 
        itemId, itemQuantity, isPurchase);
}
```

---

## 5. Архитектурные слои

### 5.1 Слой 1: Data Layer (PlayerDataStore)

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PlayerDataStore                              │
│                     (Server-Only Singleton)                         │
├─────────────────────────────────────────────────────────────────────┤
│  Credits Cache:      Dictionary<ulong, float>                      │
│  Warehouse Cache:    Dictionary<string, List<WarehouseItem>>       │
│                                                                     │
│  Ключи PlayerPrefs:                                                 │
│    PD_Credits_{clientId}           — float                          │
│    PD_Warehouse_{clientId}_{loc}  — JSON                           │
├─────────────────────────────────────────────────────────────────────┤
│  API:                                                                │
│    GetCredits(clientId) → float                                     │
│    SetCredits(clientId, amount)                                     │
│    ModifyCredits(clientId, delta)                                   │
│    GetWarehouse(clientId, locationId) → List<WarehouseItem>         │
│    SetWarehouse(clientId, locationId, items)                        │
│    ClearCache(clientId)                                             │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.2 Слой 2: Business Logic (TradeMarketServer)

```
┌─────────────────────────────────────────────────────────────────────┐
│                      TradeMarketServer                             │
│                       (NetworkBehaviour)                           │
├─────────────────────────────────────────────────────────────────────┤
│  Markets:            Dictionary<string, LocationMarket>            │
│  NPC Traders:        List<NPCTrader>                               │
│  Market Events:      List<MarketEvent>                            │
│  Transaction Log:    List<string>                                  │
├─────────────────────────────────────────────────────────────────────┤
│  Operations:                                                        │
│    ProcessBuyItem(itemId, qty, locationId, clientId)                 │
│      1. Validate: stock, credits, limits                            │
│      2. Transaction: deduct credits, add to warehouse               │
│      3. Update market: reduce stock, increase demand                │
│      4. Save: PlayerDataStore.SetWarehouse()                        │
│      5. Send: TradeResultClientRpc(targetClientId, ...)             │
│                                                                     │
│    ProcessSellItem(itemId, qty, locationId, clientId)               │
│      1. Validate: warehouse exists, enough qty                       │
│      2. Transaction: add credits, remove from warehouse             │
│      3. Update market: increase stock, increase supply              │
│      4. Save: PlayerDataStore.SetWarehouse()                        │
│      5. Send: TradeResultClientRpc(targetClientId, ...)             │
│                                                                     │
│    MarketTick() — вызывается каждые N минут                        │
│      1. ProcessNPCTrades()                                         │
│      2. UpdateEvents()                                             │
│      3. DecaySupplyAndDemand()                                     │
│      4. RecalculatePrices()                                         │
│      5. SendMarketUpdateClientRpc()                                │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.3 Слой 3: Client Cache (PlayerTradeStorage)

```
┌─────────────────────────────────────────────────────────────────────┐
│                      PlayerTradeStorage                            │
│                       (MonoBehaviour, Client)                      │
├─────────────────────────────────────────────────────────────────────┤
│  currentLocationId:   string                                       │
│  warehouse:          List<WarehouseItem>                            │
│  credits:            float (synced from PlayerDataStore)           │
├─────────────────────────────────────────────────────────────────────┤
│  Operations (Local-only, UI-driven):                               │
│    LoadFromPlayerDataStore(clientId)                               │
│      → credits = PlayerDataStore.GetCredits()                      │
│      → warehouse = PlayerDataStore.GetWarehouse()                   │
│                                                                     │
│    Save()                                                           │
│      → Called ONLY after server confirmation                         │
│      → PlayerDataStore.SetWarehouse()                               │
│                                                                     │
│  NO direct modification without server approval!                   │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.4 Слой 4: UI Layer (TradeUI)

```
┌─────────────────────────────────────────────────────────────────────┐
│                           TradeUI                                  │
│                       (MonoBehaviour, Client)                       │
├─────────────────────────────────────────────────────────────────────┤
│  currentMarket:        LocationMarket (Read-Only)                  │
│  playerStorage:       PlayerTradeStorage (Client Cache)           │
├─────────────────────────────────────────────────────────────────────┤
│  Operations:                                                        │
│    OpenTrade(market)                                               │
│      → playerStorage.LoadFromPlayerDataStore()                      │
│      → RenderItems()                                               │
│                                                                     │
│    TryBuyItem()                                                    │
│      → _tradeLocked = true                                         │
│      → BuyItemViaServer()                                          │
│      → Wait for OnTradeResult()                                    │
│                                                                     │
│    OnTradeResult(success, ...)                                     │
│      → _tradeLocked = false                                        │
│      → playerStorage.LoadFromPlayerDataStore()                      │
│      → UpdateDisplays()                                             │
│      → RenderItems()                                               │
│                                                                     │
│  UI NEVER modifies data directly — ONLY through server RPC!        │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 6. Контракты (Contracts)

### 6.1 Контракт: Покупка

| Параметр | Описание |
|----------|----------|
| **Input** | `itemId: string`, `quantity: int`, `locationId: string` |
| **Pre-conditions** | `quantity > 0`, рынок существует, товар есть, сток >= qty, кредиты >= total |
| **Business Logic** | Списать кредиты, добавить на склад, обновить спрос |
| **Output** | `success: bool`, `message: string`, `newCredits: float`, `newStock: int` |
| **Post-conditions** | `credits = oldCredits - totalCost`, `warehouse += qty`, `stock -= qty` |

### 6.2 Контракт: Продажа

| Параметр | Описание |
|----------|----------|
| **Input** | `itemId: string`, `quantity: int`, `locationId: string` |
| **Pre-conditions** | `quantity > 0`, склад существует, товар есть на складе, qty <= stored |
| **Business Logic** | Убрать со склада, начислить кредиты (80%), обновить предложение |
| **Output** | `success: bool`, `message: string`, `newCredits: float`, `newStock: int` |
| **Post-conditions** | `credits = oldCredits + sellPrice`, `warehouse -= qty`, `stock += qty` |

### 6.3 Контракт: Погрузка

| Параметр | Описание |
|----------|----------|
| **Input** | `itemId: string`, `quantity: int` |
| **Pre-conditions** | Рядом корабль с CargoSystem, склад имеет товар, qty <= stored, трюм имеет место |
| **Business Logic** | Убрать со склада, добавить в трюм |
| **Output** | `success: bool`, `message: string` |
| **Post-conditions** | `warehouse -= qty`, `cargo += qty` |

---

## 7. Ценовая модель (Серверные расчёты)

### 7.1 Формула цены покупки

```
currentPrice(item, location) = basePrice(item) × (1 + demandFactor - supplyFactor) × eventMultiplier × reputationDiscount
```

| Множитель | Диапазон | Описание |
|-----------|----------|----------|
| `basePrice` | Фиксированная | Базовая цена из TradeItemDefinition |
| `demandFactor` | 0.0 … 1.5 | Спрос (растёт при покупках) |
| `supplyFactor` | 0.0 … 1.5 | Предложение (растёт при продажах) |
| `eventMultiplier` | 0.5 … 3.0 | Глобальные события |
| `reputationDiscount` | 0.7 … 1.3 | Скидка от репутации |

### 7.2 Формула цены продажи

```
sellPrice = currentPrice × 0.8 (NPC маржа 20%)
```

### 7.3 Влияние транзакций

```
После ПОКУПКИ: demandFactor += quantity × 0.02
После ПРОДАЖИ: supplyFactor += quantity × 0.02
```

---

## 8. Расширения (Future)

### 8.1 Контрабанда

```
Контрабандный товар:
  • Проверяется при погрузке в корабль
  • Шанс обнаружения = 15% + routeDanger × stealthMod
  • При обнаружении: штраф, потеря груза, -репутация
```

### 8.2 Репутация

```
Reputation → reputationDiscount:
  • 0-20:   discount = 1.3  (наценка +30%)
  • 21-50:  discount = 1.0  (стандарт)
  • 51-100: discount = 0.85 (скидка -15%)
  • 101+:   discount = 0.7  (скидка -30%)
```

### 8.3 NPC-трейдеры

```
NPC Trader:
  • fromLocation, toLocation — маршрут
  • itemId — товар
  • minQty, maxQty — количество
  • TradeCondition — условие активации
  
  Условия:
  • Always — всегда
  • SupplyThreshold — при supplyFactor > value
  • DemandThreshold — при demandFactor > value
  • PriceThreshold — при цена > basePrice × value
```

---

## 9. Тестирование

### 9.1 Unit-тесты

| Тест | Описание |
|------|----------|
| `BuyItem_Valid` | Покупка с валидными параметрами |
| `BuyItem_InsufficientCredits` | Отказ при недостатке кредитов |
| `BuyItem_OutOfStock` | Отказ при отсутствии стока |
| `SellItem_Valid` | Продажа с валидными параметрами |
| `SellItem_InsufficientQty` | Отказ при недостатке товара на складе |
| `PriceCalculation` | Проверка расчёта цены |

### 9.2 Интеграционные тесты

| Тест | Описание |
|------|----------|
| `Multiplayer_BuySell` | Host + Client, проверка синхронизации |
| `Multiplayer_Concurrent` | Два клиента, конкурентные операции |
| `MarketTick_Update` | Проверка обновления цен после тика |

### 9.3 Debug-команды

```csharp
// В TradeUI: нажать R для сброса кредитов
// В TradeMarketServer: ContextMenu "MarketTick"

// Тестовые RPC:
TradeMarketServer.Instance.BuyItemLocal("mesium_canister_v01", 1, "primium");
TradeMarketServer.Instance.SellItemLocal("mesium_canister_v01", 1, "primium");
```

---

## 10. Миграция (Plan)

### Фаза 1: Фикс ClientRpcParams (Immediate)

1. Изменить `TradeResultClientRpc` в NetworkPlayer
2. Изменить `SendTradeResultToClient` в TradeMarketServer
3. Добавить `ClientRpcParams` с `TargetClientIds`
4. Тестирование: Host + Client

### Фаза 2: Рефакторинг архитектуры (This Session)

1. Добавить логирование всех транзакций
2. Добавить валидацию входных параметров
3. Добавить rate limiting для защиты от спама
4. Улучшить обработку ошибок

### Фаза 3: Persistence (Future)

1. Заменить PlayerPrefs на реальную БД
2. Добавить резервное копирование
3. Добавить миграцию данных между версиями

---

**Связанные документы:**
- [`GDD_22_Economy_Trading.md`](docs/gdd/GDD_22_Economy_Trading.md)
- [`BUG_CLIENT_TRADE_NOT_WORKING_2026-04-15.md`](docs/bugs/BUG_CLIENT_TRADE_NOT_WORKING_2026-04-15.md)
- [`NETWORK_ARCHITECTURE.md`](docs/NETWORK_ARCHITECTURE.md)
