# 🔍 Торговая Система — RAG-подобная документация для отладки

**Проект:** Project C: The Clouds  
**Дата создания:** 9 апреля 2026 г.  
**Версия:** 0.2 (Сессия 8D)  
**Ветка:** `qwen-gamestudio-agent-dev`

---

## 📋 Как пользоваться этим документом

Этот документ работает как **RAG (Retrieval-Augmented Generation)** система:
1. **Найдите симптом** в таблице "Быстрая диагностика"
2. **Перейдите к разделу** с деталями для вашего случая
3. **Следуйте чек-листу** для диагностики и исправления

---

## ⚡ Быстрая диагностика (Симптом → Раздел)

| Симптом | Вероятная причина | Раздел | Файл |
|---------|-------------------|--------|------|
| `currentPrice = 0` при покупке/продаже | `MarketItem.item` reference потерян | [Price=0](#price0-ошибка) | `MarketItem.cs` |
| Покупка/продажа вызывается 2 раза | Double RPC, debounce не работает | [Double RPC](#double-rpc) | `TradeUI.cs` |
| Склад показывает неправильное кол-во | Накопилось от двойных покупок | [Склад](#склад) | `PlayerTradeStorage.cs` |
| Tick система не обновляет цены | `MarketTick()` не вызывается | [Tick](#tick-система) | `TradeMarketServer.cs` |
| RPC не доходит до сервера | NetworkPlayer не найден | [RPC](#rpc-цепь) | `NetworkPlayer.cs` |
| Рынок пустой после открытия | ScriptableObject не загружен | [Рынок пуст](#рынок-пуст) | `TradeMarketServer.cs` |

---

## 🔑 Ключевые переменные (Индекс)

### Клиентская часть (TradeUI.cs)

| Переменная | Тип | Назначение | Где используется | Значение по умолчанию |
|------------|-----|------------|------------------|----------------------|
| `_tradeLocked` | `bool` | **КРИТИЧНО** — блокировка повторных RPC до ответа сервера | `TryBuyItem()`, `TrySellItem()`, `OnTradeResult()` | `false` |
| `_lastTradeTime` | `float` | Время последней сделки (debounce 0.5s) | `TryBuyItem()`, `TrySellItem()` | `0` |
| `_selectedIndex` | `int` | Индекс выбранного товара в списке | `HandleInput()`, `OnBuyClicked()` | `-1` |
| `_buyQuantity` | `int` | Количество для покупки/продажи (1-99) | `HandleInput()`, `OnBuyClicked()` | `1` |
| `_showWarehouseTab` | `bool` | Текущая вкладка: true=склад, false=рынок | `HandleInput()`, `RenderItems()` | `false` |
| `playerStorage` | `PlayerTradeStorage` | Ссылка на склад игрока (через NetworkPlayer) | Весь UI торговли | FindObjectOfType |
| `currentMarket` | `LocationMarket` | ScriptableObject текущего рынка | Открытие торговли, RPC | null |

### Серверная часть (TradeMarketServer.cs)

| Переменная | Тип | Назначение | Где используется | Значение по умолчанию |
|------------|-----|------------|------------------|----------------------|
| `_markets` | `Dictionary<string, LocationMarket>` | Все рынки по locationId | `LoadAllMarkets()`, Buy/Sell RPC | empty |
| `testMode` | `bool` | **ОТЛАДКА** — уменьшить tickInterval до 30s | `TickInterval` getter | `false` |
| `tickInterval` | `float` | Интервал тика рынка (секунды) | `FixedUpdate()`, `MarketTick()` | `300` (5 мин) |
| `_tradeTimestamps` | `Dictionary<ulong, List<float>>` | Rate limiting: clientId → timestamps | `CheckRateLimit()` | empty |
| `maxTradesPerMinute` | `int` | Лимит сделок (0 = отключено) | `CheckRateLimit()` | `0` |

### Данные рынка (MarketItem.cs)

| Переменная | Тип | Назначение | Где используется | Значение по умолчанию |
|------------|-----|------------|------------------|----------------------|
| `item` | `TradeItemDefinition` | **ХРУПКОЕ** — ссылка на ScriptableObject товара | Все операции с ценой | null |
| `itemId` | `string` | **НОВОЕ 8D** — ID для восстановления ссылки | `InitFromItem()`, `RecalculatePrice()` | `""` |
| `basePrice` | `float` | Базовая цена (копируется из item.basePrice) | Расчёт цен | `0` |
| `currentPrice` | `float` | **КРИТИЧНО** — текущая цена сделки | Buy/Sell RPC | `0` |
| `demandFactor` | `float` | Фактор спроса (0.0–1.5) | Расчёт цен, decay | `0` |
| `supplyFactor` | `float` | Фактор предложения (0.0–1.5) | Расчёт цен, decay | `0` |
| `eventMultiplier` | `float` | Множитель событий (1.0 = нет событий) | Расчёт цен | `1` |
| `availableStock` | `int` | Доступное количество товара | Проверка стока | `50` |

---

## 🎯 Детальная диагностика

### PRICE=0 ОШИБКА

**Симптом:**  
```
[TradeMarketServer] КРИТИЧНО: currentPrice=0 для mesium_canister_v01 после RecalculatePrice!
```

**Причина:**  
`MarketItem.item` ссылка потерялась при сериализации/десериализации ScriptableObject.

**Диагностика:**
1. Проверьте логи: `[TradeMarketServer] DEBUG BUY:` — что показывает `item=NULL` или `item=TradeItem_Mesium`?
2. Если `item=NULL` но `itemId_field=mesium_canister_v01` — система восстановления работает
3. Если `item=NULL` и `itemId_field=NULL` — MarketItem не инициализирован

**Исправление:**
```csharp
// MarketItem.cs — RecalculatePrice()
// Сессия 8D: Автоматическое восстановление ссылки по itemId
if (item == null && !string.IsNullOrEmpty(itemId))
{
    var db = FindTradeDatabase();
    if (db != null)
    {
        item = db.GetItemById(itemId);
        if (item != null) basePrice = item.basePrice;
    }
}

// Если item всё ещё null, используем сохранённый basePrice
if (item == null && basePrice <= 0f)
{
    currentPrice = 0f; // Нельзя рассчитать без данных
    return;
}
```

**Предотвращение:**
- Убедитесь что `LocationMarket.InitItems()` вызывается при загрузке
- Проверьте что `TradeItemDatabase.asset` существует в `Resources/Trade/`
- В Unity Editor: правый клик на рынке → Reinitialize (через ContextMenu)

**Связанные файлы:**
- `Assets/_Project/Trade/Scripts/MarketItem.cs` — `RecalculatePrice()`
- `Assets/_Project/Trade/Scripts/LocationMarket.cs` — `InitItems()`
- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` — `LoadAllMarkets()`

---

### DOUBLE RPC

**Симптом:**  
Один клик по кнопке вызывает 2 покупки/продажи. В логах:
```
[TradeUI] Покупка: Мезий x1
[TradeUI] Покупка: Мезий x1  ← дубль!
```

**Причина (сессия 8C):**  
Unity UI Button onClick мог сработать дважды в одном кадре через EventSystem. Флаг `_tradePending` ставился ПОЗДНО.

**Решение (сессия 8D):**
```csharp
// TradeUI.cs — _tradeLocked сбрасывается ТОЛЬКО в OnTradeResult()
private bool _tradeLocked = false;

private void TryBuyItem()
{
    if (_tradeLocked) return;  // Блокировка до ответа сервера
    _tradeLocked = true;       // ПЕРЕД отправкой RPC
    BuyItemViaServer(itemId, quantity);
}

public void OnTradeResult(bool success, ...)
{
    _tradeLocked = false;  // Разблокировка ПОСЛЕ ответа
}
```

**Дополнительно:**
- Горячие клавиши `1` (купить) и `2` (продать) — альтернатива клику мышью
- `TRADE_COOLDOWN = 0.5f` — дополнительная защита от спама

**Проверка:**
1. Кликните на кнопку "КУПИТЬ" один раз
2. В логах должно быть: `[TradeUI] Покупка: ...` **один раз**
3. В логах сервера: `DEBUG BUY: ...` **один раз**
4. Если видите 2+ лога — проверьте `_tradeLocked` в инспекторе

**Связанные файлы:**
- `Assets/_Project/Trade/Scripts/TradeUI.cs` — `TryBuyItem()`, `OnTradeResult()`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `TradeBuyServerRpc()`
- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` — `BuyItemServerRpc()`

---

### СКЛАД

**Симптом:**  
На складе 5 предметов вместо 1 после одной покупки.

**Причина:**  
Накопилось от двойных RPC (см. [Double RPC](#double-rpc) выше).

**Исправление:**
1. В игре: нажмите `R` для сброса склада (отладка)
2. Или исправьте Double RPC — проблема уйдёт сама

**Как работает синхронизация:**
```
Сервер покупает → SendTradeResultToClient(itemId, quantity, isPurchase=true)
     ↓
NetworkPlayer.TradeResultClientRpc(...)
     ↓
TradeUI.OnTradeResult() → SyncWarehouseItem(itemId, quantity)
     ↓
playerStorage.warehouse обновляется
```

**Проверка:**
```csharp
// TradeUI.cs — Debug лог в RemoveFromWarehouse
Debug.Log($"[TradeUI] RemoveFromWarehouse: {itemId} x{quantity} (было: {wi.quantity})");
```

**Связанные файлы:**
- `Assets/_Project/Trade/Scripts/PlayerTradeStorage.cs` — `Save()`, `Load()`
- `Assets/_Project/Trade/Scripts/TradeUI.cs` — `SyncWarehouseItem()`, `RemoveFromWarehouse()`

---

### TICK СИСТЕМА

**Симптом:**  
Цены не меняются со временем, NPC-трейдеры не работают.

**Диагностика:**
1. Проверьте `testMode` в инспекторе TradeMarketServer → должно быть `true` для отладки
2. В логах должно быть: `[TradeMarketServer] MarketTick #X | markets=4 ...` каждые 30 сек
3. Если логов нет — `FixedUpdate()` не вызывает `MarketTick()`

**Как работает:**
```csharp
// TradeMarketServer.FixedUpdate()
void FixedUpdate()
{
    _tickTimer += Time.fixedDeltaTime;
    if (_tickTimer >= TickInterval)  // 30s если testMode=true
    {
        MarketTick();  // Вызывается на сервере
        _tickTimer = 0f;
    }
}

void MarketTick()
{
    ProcessNPCTrades();          // NPC торгуют
    DecaySupplyAndDemand();      // Затухание 0.846x
    RecalculatePrices();         // Пересчёт цен
    SendDeltaUpdatesToClients(); // Обновление UI
}
```

**Переменные для отладки:**
| Переменная | Где | Значение | Назначение |
|------------|-----|----------|------------|
| `testMode` | TradeMarketServer inspector | `true` | Уменьшить интервал до 30s |
| `tickInterval` | TradeMarketServer inspector | `300` | Базовый интервал (сек) |
| `_tickTimer` | Код | float | Таймер до следующего тика |

**Ручной вызов тика:**  
В Unity Inspector на TradeMarketServer → правый клик → "Вызвать MarketTick вручную"

**Связанные файлы:**
- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` — `MarketTick()`, `FixedUpdate()`

---

### RPC ЦЕПЬ

**Симптом:**  
Клик по кнопке не вызывает покупку, в логах ошибка.

**Полная цепь RPC:**
```
TradeUI.OnBuyClicked()
    ↓
TradeUI.TryBuyItem() — проверка _tradeLocked
    ↓
TradeUI.BuyItemViaServer(itemId, quantity)
    ↓
NetworkPlayer.TradeBuyServerRpc() — [Rpc(SendTo.Server)]
    ↓
TradeMarketServer.BuyItemServerRpc() — [ServerRpc]
    ↓ (проверки: рынок, сток, кредиты, лимиты)
TradeMarketServer.SendTradeResultToClient()
    ↓
NetworkPlayer.TradeResultClientRpc() — [Rpc(SendTo.Owner)]
    ↓
TradeUI.OnTradeResult() — _tradeLocked = false
```

**Диагностика по шагам:**
1. **TradeUI → NetworkPlayer:** Проверьте `Debug.Log($"[TradeUI] Покупка: ...")`
2. **NetworkPlayer → Server:** Проверьте что TradeMarketServer.Instance != null
3. **Server валидация:** Проверьте `DEBUG BUY:` логи
4. **Server → Client:** Проверьте `OnTradeResult` вызов

**Критичные проверки:**
```csharp
// NetworkPlayer.cs — TradeBuyServerRpc
[Rpc(SendTo.Server)]
public void TradeBuyServerRpc(string itemId, int quantity, string locationId, ...)
{
    if (TradeMarketServer.Instance != null)  // ← Проверить!
    {
        TradeMarketServer.Instance.BuyItemServerRpc(itemId, quantity, locationId);
    }
    else
    {
        Debug.LogWarning("[NetworkPlayer] TradeMarketServer не найден");  ← Если видите это — сервер не создан
    }
}
```

---

### РЫНОК ПУСТ

**Симптом:**  
Открыл торговлю — список товаров пуст.

**Причина:**  
`LoadAllMarkets()` не нашёл ScriptableObject рынков.

**Диагностика:**
```csharp
// TradeMarketServer.LoadAllMarkets()
#if UNITY_EDITOR
    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LocationMarket");
    // Ищет ВСЕ LocationMarket в проекте
#else
    var markets = Resources.LoadAll<LocationMarket>("Trade/Markets");
    // Ищет в Assets/Resources/Trade/Markets/
#endif
```

**Исправление:**
1. **Editor:** Проверьте что `Market_*.asset` файлы существуют в проекте
2. **Build:** Переместите рынки в `Assets/Resources/Trade/Markets/`
3. Проверьте `TradeMarketServer._markets.Count` — должно быть > 0

**Связанные файлы:**
- `Assets/_Project/Trade/Data/Markets/` — ScriptableObject рынков
- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` — `LoadAllMarkets()`

---

## 🔧 Инструменты отладки

### Сброс склада (отладка)
В окне торговли нажмите `R` — сбросит кредиты до 1000 и очистит склад.

### Ручной вызов тика
Inspector → TradeMarketServer → контекстное меню → "Вызвать MarketTick вручную"

### Логи для отладки
Все ключевые точки покрыты `Debug.Log`:
- `[TradeUI] Покупка/Продажа` — клиентский запрос
- `[TradeMarketServer] DEBUG BUY/SELL` — состояние товара на сервере
- `[TradeMarketServer] КРИТИЧНО: currentPrice=0` — ошибка цены
- `[TradeMarketServer] MarketTick #X` — тик системы
- `[TradeUI] RemoveFromWarehouse` — синхронизация склада

### Очистка PlayerPrefs
Если склад "сломан" permanently:
```csharp
// В консоли Unity (Play mode):
PlayerPrefs.DeleteAll();
PlayerPrefs.Save();
```

---

## 📊 Архитектура (обновлено Сессия 8D)

```
┌─────────────────────────────────────────────────────────┐
│                    КЛИЕНТ (TradeUI)                      │
│  - UI торговли (Canvas, кнопки, текст)                   │
│  - _tradeLocked: bool — защита от двойных RPC           │
│  - playerStorage = GetPlayerStorageFromNetworkPlayer()   │
│  - TryBuyItem() → NetworkPlayer.TradeBuyServerRpc()     │
│  - OnTradeResult() → _tradeLocked=false + SyncWarehouse │
└────────────────────────┬────────────────────────────────┘
                         │ RPC [SendTo.Server]
                         ▼
┌─────────────────────────────────────────────────────────┐
│              СЕРВЕР (TradeMarketServer)                   │
│  - LoadAllMarkets() → ScriptableObject LocationMarket    │
│  - MarketItem.itemId: string — восстановление ссылок    │
│  - BuyItemServerRpc():                                   │
│    1. Rate limit (отключён)                              │
│    2. Проверка рынка, товара, стока                      │
│    3. Проверка currentPrice > 0 ← NEW 8D                │
│    4. Проверка кредитов (PlayerCreditsManager)           │
│    5. Проверка лимитов склада                            │
│    6. Списать кредиты, обновить рынок, добавить товар    │
│    7. playerStorage.Save()                               │
│    8. SendTradeResultToClient(itemId, qty, isPurchase)   │
│    9. SendMarketUpdateClientRpc (обновить UI цены)       │
│  - MarketTick() каждые 30s (testMode) / 300s (prod)     │
└─────────────────────────────────────────────────────────┘
```

---

## 📝 История изменений

| Версия | Дата | Изменения |
|--------|------|-----------|
| 0.1 | 6 апр 2026 | Начальная документация |
| 0.2 | 9 апр 2026 | Сессия 8D: itemId восстановление, _tradeLocked, price=0 защита, горячие клавиши 1-2 |
| 0.3 | 9 апр 2026 | Hotfix: сервер всегда Load(), клиент Load() вместо Sync/Remove, известна проблема demandFactor>1.5 |

---

## ⚠️ Известные проблемы (не исправлены)

### demandFactor > 1.5

**Симптом:** В логах `demandFactor=20` → аномальные цены.

**Причина:** В inspector ScriptableObject можно установить любое значение. Clamp работает только при `UpdateDemand()`.

**Временное решение:** Вручную установить все `demandFactor` и `supplyFactor` в 0 в inspector рынков.

**Постоянное решение:** Сессия 8E — clamp при загрузке рынка.

### quantity <= 0

**Симптом:** Теоретически можно отправить quantity=0 или -1.

**Решение:** Сессия 8E — валидация в RPC.

---

## 🔗 Связанные документы

- [QWENTRADING8SESSION.md](QWENTRADING8SESSION.md) — Итоги сессии 8C
- [GDD_22_Economy_Trading.md](gdd/GDD_22_Economy_Trading.md) — GDD экономики
- [GDD_25_Trade_Routes.md](gdd/GDD_25_Trade_Routes.md) — GDD торговых маршрутов
