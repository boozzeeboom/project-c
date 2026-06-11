# Resources Exchanger — Реализация (актуальная)

> **Дата:** 2026-06-11
> **Статус:** ✅ DONE (T-E01..T-E05)
> **Репозиторий:** ProjectC_client

## TL;DR

Система работает: игрок подходит к рынку → открывает Market Window → вкладка «Обменник».
Левая панель — pickable предметы (группированные по itemId, с количеством).
Правая панель — ящики на складе (группированные).
Pack: 100 осколков → 1 ящик на складе.
Unpack: 1 ящик → 100 осколков в инвентарь.

---

## 1. Архитектура (после рефакторинга)

### 1.1 Отклонения от первоначального плана

| План | Реальность | Причина |
|------|-----------|---------|
| Отдельный `ExchangerTab.cs` | UI-код прямо в `MarketWindow.cs` | 232 символа стаба не хватало, проще дописать методы в MarketWindow |
| `ExchangerTab.uxml` | Нет отдельного UXML — `exchange-section` встроена в `MarketWindow.uxml` | Соответствует паттерну contracts |
| `ExchangeServer` как отдельный NetworkBehaviour | Реализован — но спавнится через `ScenePlacedObjectSpawner`, не через `NetworkSceneManager` | `InScenePlacedSourceGlobalObjectIdHash == 0` |
| `ExchangerTab` подписывается на `ExchangeClientState.OnResultReceived` | `MarketWindow` подписывается напрямую | Проще, меньше файлов |

### 1.2 Полный stack

```
┌──────────────────────────────────────────────────────────┐
│ MarketWindow.Show()                                      │
│  → RefreshExchangeData()                                 │
│    → InventoryClientState.GetItems() (группировка по id) │
│    → MarketClientState.CurrentSnapshot.warehouse         │
│    → ResourceExchangeResolver.FindRateForItemName         │
│    → ResourceExchangeResolver.FindRateForWarehouseItem    │
├──────────────────────────────────────────────────────────┤
│ OnPackClicked()                                          │
│  → ExchangeServer.RequestPackRpc(clientId, itemId, qty)  │
│    → IsReadyOrResult / CheckRateLimitOrResult            │
│    → ResourceExchangeResolver: itemName → rate           │
│    → ExchangeWorld.Pack()                                │
│      → InventoryWorld.RemoveItems()                      │
│      → TradeWorld.GetOrLoadWarehouse().TryAdd()          │
│      → rollback на ошибку                                │
│    → TradeWorld.Repository.SetWarehouse()                │
│    → InventoryServer.PushSnapshot(clientId)              │
│    → MarketServer.PushPlayerSnapshot(clientId)           │
│    → ReceiveExchangeResultTargetRpc(dto)                 │
│      → MarketWindow.HandleExchangeResult()               │
│        → RefreshExchangeData()                           │
├──────────────────────────────────────────────────────────┤
│ OnUnpackClicked() (аналогично)                           │
│  → ExchangeServer.RequestUnpackRpc()                     │
│    → ExchangeWorld.Unpack()                              │
│      → TradeWorld.GetOrLoadWarehouse().TryRemove()       │
│      → InventoryWorld.AddItemDirect() × N               │
│      → rollback на ошибку                                │
│    → InventoryServer.PushSnapshot(clientId)              │
│    → MarketServer.PushPlayerSnapshot(clientId)           │
└──────────────────────────────────────────────────────────┘
```

## 2. Ключевые паттерны

### 2.1 Группировка предметов в UI (T-E04)

InventoryData хранит каждый предмет как отдельную запись в List<int>.
`BuildSnapshot` → каждая запись = `InventoryItemDto { quantity = 1 }`.
`RefreshExchangeData` группирует по itemId:

```csharp
var grouped = new Dictionary<int, int>();
foreach (var inv in invItems) grouped[inv.itemId] = grouped.GetValueOrDefault(inv.itemId) + 1;
// grouped = { itemId: count, ... }
```

### 2.2 Pack countToRemove

Семантика: `countToRemove` = количество ЕДИНИЦ на отправляющей стороне:
- Pack: `countToRemove = rate.inventoryQty` (100 осколков = 1 коробка)
- Unpack: `countToRemove = rate.warehouseQty` (1 коробка = 100 осколков)

Сервер валидирует: `countToRemove % rate.inventoryQty != 0` → fail.

### 2.3 Push-уведомления после операции

После Pack/Unpack зовутся оба:
- `InventoryServer.PushSnapshot(clientId)` — обновить инвентарь
- `MarketServer.PushPlayerSnapshot(clientId)` — обновить склад в UI

Без вызова MarketServer клиент видит старые данные склада (log "warehouse persisted" есть, snapshot не пришёл).

### 2.4 Серверная инициализация

```csharp
// ExchangeServer.OnNetworkSpawn:
// 1. Validate exchangeRateConfig != null
// 2. StartCoroutine(InitWhenReady())
//    → ждёт TradeWorld.Instance (создаётся MarketServer.OnNetworkSpawn)
//    → timeout 10с → создаёт _resolver + _world
```

### 2.5 Scene-placed NetworkObject

`[ExchangeServer]` в BootstrapScene имеет `InScenePlacedSourceGlobalObjectIdHash == 0`
(добавлен вручную). NGO не спавнит такие объекты. Спавнит `ScenePlacedObjectSpawner`:

```csharp
// ScenePlacedObjectSpawner.Start():
// 1. Подписка на ClientSceneLoader.OnSceneLoaded (для WorldScene)
// 2. Подписка на NetworkManager.OnServerStarted (для BootstrapScene)
// 3. HandleServerStarted → SpawnInAllLoadedScenes()
```

## 3. Edge-cases и их обработка

### 3.1 Inventory full (реализовано)

Было: `MAX_SLOTS = 32` → Unpack 100 осколков = fail с `InventoryFull`.
Фикс: `MAX_SLOTS = 1000` (default), конфигурируется через `InventoryServer.maxSlots` в инспекторе.
TODO: stacked inventory (count per itemId) — сейчас каждый id = 1 отдельная запись.

### 3.2 ExchangeServer не готов (реализовано)

Было: `IsReady()` возвращает false → silent return.
Фикс: `IsReadyOrResult(clientId, op)` шлёт fail-результат клиенту с сообщением.

### 3.3 Rate limit (реализовано)

`CheckRateLimitOrResult(clientId, op)` шлёт fail при превышении (30 ops/min по умолчанию).

### 3.4 Пустой инвентарь при первом открытии (реализовано)

Было: открытие обменника до прихода первого snapshot → пустые списки.
Фикс: `MarketWindow.Show()` → `InventoryClientState.RequestRefresh()` + `RequestSubscribeMarket()`.

### 3.5 Склад не обновлялся после Pack (реализовано)

Было: только `InventoryServer.PushSnapshot` звался.
Фикс: добавлен `MarketServer.PushPlayerSnapshot(clientId)`.

## 4. Файлы реализации

### 4.1 Новые файлы

| Файл | Строк | Назначение |
|------|-------|------------|
| `Assets/_Project/Trade/Exchange/Config/ExchangeRateConfig.cs` | 51 | ScriptableObject |
| `Assets/_Project/Trade/Exchange/Config/ExchangeRateEntry.cs` | 40 | Struct курса |
| `Assets/_Project/Trade/Exchange/Core/ExchangeResult.cs` | 40 | Struct результата |
| `Assets/_Project/Trade/Exchange/Core/ExchangeWorld.cs` | 207 | POCO бизнес-логика |
| `Assets/_Project/Trade/Exchange/Core/ResourceExchangeResolver.cs` | 171 | Lookup-слой |
| `Assets/_Project/Trade/Exchange/Network/ExchangeServer.cs` | 333 | RPC-хаб |
| `Assets/_Project/Trade/Scripts/Client/ExchangeClientState.cs` | 46 | Mono-синглтон приёма результата |
| `Assets/_Project/Trade/Scripts/Dto/ExchangeResultDto.cs` | 40 | DTO |
| `Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset` | — | SO с 4 курсами (IronOre, CopperOre, Wood, Antigrav) |
| `Assets/_Project/Resources/Items/Item_Antigrav_Антигравий_осколок.asset` | — | Pickable предмет |

### 4.2 Изменённые файлы

| Файл | Изменение |
|------|-----------|
| `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` | +1627 строк (методы: RefreshExchangeData, OnPackClicked, OnUnpackClicked, HandleExchangeResult, SwitchTab, Show — обновлён) |
| `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` | +`OnServerStarted` подписка + `HandleServerStarted` |
| `Assets/_Project/Items/Core/InventoryWorld.cs` | `MAX_SLOTS` → конфигурируемый `_maxSlots` через `ConfigureMaxSlots(int)` |
| `Assets/_Project/Items/Network/InventoryServer.cs` | +`ConfigureMaxSlots(maxSlots)` вызов в `OnNetworkSpawn` |
| `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` | +`PushPlayerSnapshot(clientId)` public helper |
| `Assets/_Project/Scenes/BootstrapScene.unity` | `maxSlots: 32`→`1000`, `[ExchangeServer]` root GameObject |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | +`ReceiveExchangeResultTargetRpc` (T-E04) — на самом деле был, не меняли... |

## 5. Конфигурация

### 5.1 DefaultExchangeRate.asset

```
Resources/Exchange/DefaultExchangeRate.asset
```

4 записи:

| warehouseItemId | warehouseQty | inventoryItemName | inventoryQty | displayName |
|----------------|:---:|-------------------|:---:|------------|
| resource_iron_box | 1 | Железная руда | 100 | Ящик железной руды |
| resource_copper_box | 1 | Медная руда | 100 | Ящик медной руды |
| resource_wood_box | 1 | Древесина | 100 | Ящик древесины |
| antigrav_ingot_v01 | 1 | Антигравий (осколок) | 100 | Слиток антигравия |

### 5.2 MAX_SLOTS

Значение по умолчанию: 1000.
Меняется в инспекторе: BootstrapScene → `[InventoryServer]` → `maxSlots`.

## 6. Команды для проверки

```bash
# 0. Загрузить всё
git pull
# 1. Открыть в Unity Editor
# 2. Убедиться, что сцена BootstrapScene не изменена (кроме MaxSlots)
# 3. Start Host
# 4. Подойти к рынку → F
# 5. Market Window → tab "Обменник"
# 6. Левая панель показывает pickable предметы (группировка)
# 7. Правая панель показывает warehouse ящики
# 8. Выбрать предмет слева → Упаковать → 100 шт → +1 ящик справа
# 9. Выбрать ящик справа → Распаковать → +100 шт слева
```

## 7. Ограничения (known issues)

1. **Stacking отсутствует** — каждая запись в инвентаре = 1 предмет. 100 осколков = 100 слотов.
   Фикс: `MAX_SLOTS = 1000`. Чистый stacking — отдельный рефакторинг InventoryData.

2. **Только Host mode** — Dedicated server не тестировался. `InvokePermission.Owner` может не работать.

3. **Hard-coded DefaultExchangeRate** — не разделяется по локациям (T-E01 задел: `MarketConfig.locationId → свой ExchangeRateConfig`).

4. **Нет кэша курсов на клиенте** — `ResourceExchangeResolver.Default` грузит `Resources.Load` каждый раз при `RefreshExchangeData`. Для Stage 3 — кэш.

5. **Warehouse snapshot приходит с задержкой** — из-за RPC roundtrip. Для 2026 в норме.
