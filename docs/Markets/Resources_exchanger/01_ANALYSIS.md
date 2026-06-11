# Resources Exchanger — Анализ и архитектура

> **Дата:** 2026-06-11
> **Статус:** Анализ (код не написан)
> **Задача:** Объединить две системы предметов через обменник/упаковщик, не ломая существующие системы.

---

## TL;DR

Две независимые системы предметов (Inventory — пикабльные, Trade — рыночные ящики) не имеют
моста. Решение: **ResourceExchangeResolver** (слой конфигурации) + **ExchangeWorld** (серверная
логика) + **ExchangerTab** (4-я вкладка в MarketWindow). Ни одна существующая система не
меняется. UI — левая панель (инвентарь), правая панель (склад), кнопки «упаковать / распаковать».

---

## 1. Текущее состояние: две изолированные системы

### 1.1 Система A — Inventory (Pickable Items)

| Аспект | Детали |
|--------|--------|
| **ID** | `int` (1..34 из `ItemRegistry`) |
| **SO** | `ItemData` в `Assets/_Project/Resources/Items/` |
| **Хранение** | `InventoryWorld._playerInventories[clientId]` → `InventoryData` (List<int> per ItemType) |
| **API** | `HasItem(clientId, itemId)`, `HasAllItems`, `HasAnyItem`, `CountOf`, `AddItemDirect`, `TryRemove` |
| **Потребители** | PickupItem (E-клавиша), Crafting (`RecipeData`), Mining (`ResourceNodeConfig`), Quests (`QuestTask.GiveItem/TakeItem`), MetaRequirement, ShipKeys |
| **Масштаб** | 1 шт = 1 единица («кусок руды 1 кг») |

### 1.2 Система B — Trade/Warehouse (Boxed Items)

| Аспект | Детали |
|--------|--------|
| **ID** | `string` ("mesium_canister_v01", "antigrav_ingot_v01") |
| **SO** | `TradeItemDefinition` в `Assets/_Project/Trade/Data/Items/` |
| **Хранение** | `Warehouse` (per-player-per-location) + ship cargo в `TradeWorld` |
| **API** | `Warehouse.TryAdd/Remove`, `TradeWorld.TryBuy/Sell/Load/Unload` |
| **Потребители** | MarketWindow (buy/sell), ContractSystem, CargoSystem |
| **Масштаб** | 1 шт = 1 ящик/канистра/слиток («100 кг») |

### 1.3 Проблема

Моста между системами **нет**:

- Crafting НЕ может использовать предметы со склада
- Mining НЕ может отправить добычу напрямую на склад
- Warehouse НЕ может быть источником для крафта
- Один и тот же ресурс (например, «Медная руда») представлен как `Item_Resources_Медная_руда.asset`
  (int id) в инвентаре и как `TradeItem_Mesium_v01.asset` (string id) на рынке — **без маппинга**

---

## 2. Анализ архитектурных подходов

### 2.1 Подход A — Exchanger как самостоятельная подсистема

**Что:** Новый `ExchangeWorld` POCO + `ExchangeServer` NetworkBehaviour + `ExchangerTab`
в MarketWindow. Данные: `ExchangeRateConfig` ScriptableObject.

```csharp
// ExchangeRateConfig — одна запись
[Serializable]
public class ExchangeRateEntry
{
    public ItemData inventoryItem;           // pickable предмет (int id)
    public TradeItemDefinition tradeItem;    // рыночный товар (string id)
    public int packRate = 100;               // сколько pickable → 1 box
    public int unpackRate = 100;             // 1 box → сколько pickable
}
```

**Поток Pack:**
```
1. Player выбирает pickable предмет в левой панели
2. Нажимает "→ Упаковать"
3. ExchangeServer.RequestPackRpc(clientId, inventoryItemId, quantity, locationId)
4. ExchangeWorld.TryPack:
   a. Проверяет: inventoryItemId маппится? quantity >= packRate? 
   b. InventoryWorld.TryRemove(clientId, inventoryItemId, packRate)
   c. TradeWorld.GetOrCreateWarehouse(clientId, locationId).TryAdd(tradeItemId, 1)
   d. Возвращает результат
```

**Поток Unpack:**
```
1. Player выбирает trade item в правой панели
2. Нажимает "← Распаковать"
3. ExchangeServer.RequestUnpackRpc(clientId, tradeItemId, quantity, locationId)
4. ExchangeWorld.TryUnpack:
   a. Проверяет: tradeItemId маппится? warehouse.GetQuantity >= 1?
   b. TradeWorld.GetWarehouse().TryRemove(tradeItemId, 1)
   c. InventoryWorld.AddItemDirect(clientId, inventoryItemId, unpackRate)
   d. Возвращает результат
```

**✅ Плюсы:**
- Чистое разделение, не трогает InventoryWorld/TradeWorld
- Следует v2-архитектуре (POCO + NetworkBehaviour + tab)
- UI — готовая вкладка MarketWindow (4-я после Рынок/Склад/Контракты)
- Config-driven (новые пары предметов = новая запись в SO)
- Rate симметричный (100:1 в обе стороны)

**❌ Минусы:**
- Новая подсистема (но маленькая — 4-5 файлов)
- Не решает автоматическую интеграцию (Crafting всё ещё не видит склад)

### 2.2 Подход B — Прямой мост в существующих системах

**Что:** Добавить методы в InventoryWorld/TradeWorld:
```csharp
// InventoryWorld
public bool TryMoveToWarehouse(ulong clientId, int itemId, int count, string locationId);
public bool TryTakeFromWarehouse(ulong clientId, int itemId, int count, string locationId);
```

**✅ Плюсы:**
- Меньше кода (без новой подсистемы)
- Одно API для всего

**❌ Минусы:**
- **Циркулярная зависимость**: InventoryWorld → TradeWorld (или нужен callback)
- Инвентарь знает про рынок — нарушение separation of concerns
- InventoryWorld уже 628 строк, TradeWorld — 488 — добавление cross-system coupling
  делает их нечитаемыми
- Конфиг рейтов всё равно нужен — куда его класть?
- Усложняет тестирование обеих систем

### 2.3 Подход C — ResourceExchangeResolver + generic сервис

**Что:** `ResourceExchangeResolver` (переиспользуемый слой конфигурации) + `ExchangeService`
(статичный API-мост). Любая система (Crafting, Quest, Mining) может вызвать
`ExchangeService.TryTakeFromWarehouse(clientId, itemId, count, locationId)` и получить
предметы в инвентарь без UI.

**✅ Плюсы:**
- Максимально гибко — любая система может использовать
- Resolver переиспользуется всеми потребителями
- UI — опциональный слой поверх Resolver

**❌ Минусы:**
- Избыточен для MVP — ни Crafting, ни Mining пока не используют склад напрямую
- «Упаковка/распаковка» — это explicit action игрока, не автоматический конвейер
- Сложнее имплементировать, труднее тестировать
- Over-engineering для Stage 2.5

### 2.4 Подход D — Гибрид (Рекомендуется)

**Слои:**

```
┌───────────────────────────────────────────┐
│           ResourceExchangeConfig          │  SO-конфигурация
├───────────────────────────────────────────┤
│        ResourceExchangeResolver           │  Runtime-кэш + lookup
├───────────────────────────────────────────┤
│            ExchangeWorld                  │  Server POCO (бизнес-логика)
├───────────────────────────────────────────┤
│           ExchangeServer                  │  NetworkBehaviour (RPC-хаб)
├───────────────────────────────────────────┤
│           ExchangerTab                    │  UI-вкладка в MarketWindow
└───────────────────────────────────────────┘
```

**Что это даёт:**
- Resolver можно переиспользовать в будущем (когда Crafting сможет тянуть со склада)
- ExchangeWorld остаётся чистым POCO, тестируется без Unity
- UI — 4-я вкладка MarketWindow, минимальные изменения
- **Ни одна существующая система не меняется**
- Путь миграции: сейчас → только UI-обменник → потом Resolver для автоматических систем

---

## 3. Сравнительная таблица

| Критерий | A (Exchanger) | B (Direct bridge) | C (Resolver+Service) | D (Hybrid ✅) |
|----------|:---:|:---:|:---:|:---:|
| Не ломает существующее | ✅ | ⚠️ (рефакторинг) | ✅ | ✅ |
| Новых файлов | 4-5 | 0-1 | 5-7 | 5-6 |
| Сложность | Средняя | Низкая | Высокая | Средняя |
| Тестируемость | ✅ (POCO) | ⚠️ (связность) | ✅ (POCO) | ✅ (POCO) |
| Расширяемость | ⚠️ | ❌ | ✅ | ✅ |
| UI complexity | Tab | N/A | Tab | Tab |
| Может быть reused крафтом/квестами | Нет | Нет | Да | **Resolver — да** |
| Risk | Низкий | Средний | Высокий | **Низкий** |

---

## 4. Рекомендуемая архитектура (D — Hybrid)

### 4.1 Структура файлов

Все новые файлы — в `Assets/_Project/Trade/Exchange/`, namespace `ProjectC.Trade.Exchange`.

```
Assets/_Project/Trade/Exchange/
├── Data/                           ← SO-конфигурация
│   └── ExchangeRateConfig.asset    ← связки ItemData ↔ TradeItemDefinition
├── Config/
│   └── ExchangeRateConfig.cs       ← SO (список ExchangeRateEntry)
├── Core/
│   ├── ExchangeRateEntry.cs        ← struct: inventoryItem, tradeItem, packRate, unpackRate
│   ├── ExchangeWorld.cs            ← POCO: TryPack / TryUnpack (серверная логика)
│   └── ResourceExchangeResolver.cs ← Runtime-кэш пар + lookup
├── Network/
│   └── ExchangeServer.cs           ← NetworkBehaviour: RPC-хаб
├── Client/
│   └── ExchangerTab.cs             ← Контроллер 4-й вкладки MarketWindow
└── UI/
    └── ExchangerTab.uxml           ← UXML разметка (левая/правая панели + кнопки)
```

### 4.2 Ключевые классы: детали

#### `ExchangeRateEntry`
```csharp
[Serializable]
public struct ExchangeRateEntry
{
    public ItemData inventoryItem;       // pickable → int id
    public TradeItemDefinition tradeItem; // boxed → string id
    public int packRate = 100;           // сколько pickable → 1 box
    public int unpackRate = 100;         // 1 box → сколько pickable
}
```

#### `ExchangeRateConfig` (ScriptableObject)
```csharp
[CreateAssetMenu(menuName = "ProjectC/Trade/Exchange Rate Config")]
public class ExchangeRateConfig : ScriptableObject
{
    public List<ExchangeRateEntry> entries = new();
}
```

#### `ResourceExchangeResolver` (stateless runtime)
```csharp
// Singleton, инициализируется из ExchangeRateConfig при старте.
// Предоставляет lookup в обе стороны.
public class ResourceExchangeResolver
{
    // inventoryItem (int id) → tradeItem (string id) + rates
    public bool TryGetTradeItem(int inventoryItemId, out string tradeItemId, out int packRate, out int unpackRate);
    // tradeItem (string id) → inventoryItem (int id) + rates  
    public bool TryGetInventoryItem(string tradeItemId, out int inventoryItemId, out int packRate, out int unpackRate);
    // Все inventoryItemId, у которых есть trade-пара
    public HashSet<int> GetPackableInventoryItemIds();
    // Все tradeItemId, у которых есть inventory-пара
    public HashSet<string> GetUnpackableTradeItemIds();
}
```

#### `ExchangeWorld` (server POCO, singleton)

```csharp
public class ExchangeWorld
{
    public static ExchangeWorld Instance { get; private set; }
    
    public static ExchangeWorld CreateAndInitialize(ExchangeRateConfig config);
    
    /// <summary>
    /// Упаковать N*packRate pickable предметов → N boxes на склад.
    /// </summary>
    public ExchangeResultDto TryPack(ulong clientId, int inventoryItemId, int requestedQuantity, string locationId);
    
    /// <summary>
    /// Распаковать N boxes → N*unpackRate pickable предметов в инвентарь.
    /// </summary>
    public ExchangeResultDto TryUnpack(ulong clientId, string tradeItemId, int requestedQuantity, string locationId);
}
```

**Логика `TryPack`:**
1. Resolve: `inventoryItemId` → `(tradeItemId, packRate)` через `ResourceExchangeResolver`
2. If not found → `ERROR_NOT_CONFIGURED`
3. If `requestedQuantity < packRate` → `ERROR_INSUFFICIENT_FOR_ONE_BOX`
4. `boxesToPack = requestedQuantity / packRate` (floor)
5. `itemsToConsume = boxesToPack * packRate`
6. `InventoryWorld.Instance.TryRemove(clientId, inventoryItemId, itemsToConsume)` → если не хватает → `ERROR_INSUFFICIENT_ITEMS`
7. `TradeWorld.Instance.GetOrCreateWarehouse(clientId, locationId).TryAdd(tradeItemId, boxesToPack)` → если превышен лимит склада → rollback (вернуть items в инвентарь)
8. Persist: `TradeWorld.Instance.Repository?.Save(clientId, warehouse)`, `InventoryWorld.Instance.SavePlayer(clientId)`
9. Return `SUCCESS(boxesToPack, itemsToConsume)`

**Логика `TryUnpack`:**
1. Resolve: `tradeItemId` → `(inventoryItemId, unpackRate)` через resolver
2. If not found → `ERROR_NOT_CONFIGURED`
3. `boxesToUnpack = requestedQuantity` (каждая единица tradeItem = 1 box)
4. `warehouse = TradeWorld.Instance.GetWarehouse(clientId, locationId)`
5. `warehouse.TryRemove(tradeItemId, boxesToUnpack)` → если не хватает → `ERROR_INSUFFICIENT_WAREHOUSE`
6. `itemsToAdd = boxesToUnpack * unpackRate`
7. `InventoryWorld.Instance.TryAddItem(clientId, inventoryItemId, itemsToAdd)` → если инвентарь полон → rollback (вернуть boxes на склад)
8. Persist
9. Return `SUCCESS(itemsToAdd, boxesToUnpack)`

#### `ExchangeServer` (NetworkBehaviour)

```csharp
public class ExchangeServer : NetworkBehaviour
{
    // Привязан к MarketZone GameObject (наследует ZoneValidation)
    
    [Rpc(SendTo.Server)]
    public void RequestPackRpc(ulong clientId, int inventoryItemId, int quantity);
    
    [Rpc(SendTo.Server)]
    public void RequestUnpackRpc(ulong clientId, string tradeItemId, int quantity);
    
    // Ответы клиенту через RPC (или через MarketClientState)
    [Rpc(SendTo.ClientsAndHost)]
    public void ExchangeResultRpc(ulong clientId, ExchangeResultDto result);
}
```

#### `ExchangerTab` (UI контроллер)

**UXML структура:**
```xml
<tab-exchanger>
    <inventory-panel>    ← левая панель (pickable items player)
        <inventory-list> ← ListView, фильтр: только packable
    </inventory-panel>
    <center-buttons>     ← центр
        <pack-btn>  "→ Упаковать на склад"</pack-btn>
        <unpack-btn>"← Распаковать в инвентарь"</unpack-btn>
    </center-buttons>
    <warehouse-panel>    ← правая панель (warehouse items)
        <warehouse-list> ← ListView, фильтр: только unpackable
    </warehouse-panel>
</tab-exchanger>
```

### 4.3 Интеграция с MarketWindow

Pattern — точь-в-точь как Contracts tab (C2-refactor):

```csharp
// В MarketWindow.EnsureBuilt():
// 1. Добавляем 4-ю кнопку таба
var exchangerTabBtn = _root.Q<Button>("tab-exchanger");
exchangerTabBtn.clicked += () => SwitchTab("exchanger");

// 2. Ищем секцию exchanger-section (скрыта по умолчанию, как contracts-section)
_exchangerSection = _root.Q<VisualElement>("exchanger-section");

// 3. В SwitchTab():
case "exchanger":
    _itemSection.style.display = DisplayStyle.None;
    _warehouseSection.style.display = DisplayStyle.None;
    _cargoSection.style.display = DisplayStyle.None;
    _contractsSection.style.display = DisplayStyle.None;
    _exchangerSection.style.display = DisplayStyle.Flex;
    // Подгрузить данные через ExchangerTab
    break;
```

### 4.4 UXML изменения

В `MarketWindow.uxml` добавить:
- `<Button name="tab-exchanger"/>` — кнопка 4-го таба
- `<VisualElement name="exchanger-section">` — контейнер для ExchangerTab (скрыт по умолчанию)

---

## 5. Альтернативы, которые могут быть проще

### 5.1 Альтернатива: один объект-предмет без разделения

**Идея:** Сделать единую систему предметов, где `ItemData.weightKg = 100` для ящика и
`ItemData.weightKg = 1` для куска. Стак = 1 для ящиков, стак = 100 для кусков.

**❌ Почему нет:**
- Ломает всё: Crafting использует `int id`, Market использует `string id`
- Warehouse считает вес/объём через `TradeItemDefinition.weight` — другой формат
- Inventory работал бы, но TradeWorld полностью переписывать
- Risk: поломка 30+ существующих ассетов и кода

### 5.2 Альтернатива: крафт как мост

**Идея:** Ввести рецепты крафта «100 руды → ящик руды» и «ящик руды → 100 руды»
на специальной станции-упаковщике.

**❌ Почему нет:**
- Crafting выдаёт предметы в **инвентарь**, а ящик должен быть на **складе**
- Crafting не умеет класть предметы в Warehouse (и не должен)
- Нужен новый тип станции + new UI = не проще, чем Exchanger
- Крафт — production, упаковка — logistics; смешивать не стоит

### 5.3 Альтернатива: упрощённый UI (один список, не два)

**Идея:** Exchanger без двух панелей. Один список: все exchangeable предметы
(объединённые). Рядом поле qty и две кнопки Pack/Unpack.

**⚠️ Почему не рекомендую:**
- Пользователь явно описал двухпанельный UI
- Две панели интуитивно понятнее: «что у меня в карманах» vs «что на складе»
- Не нужно объяснять, в какую сторону идёт операция — направление очевидно из расположения
- MarketWindow уже имеет двухпанельный шаблон (market list + warehouse list)

---

## 6. План имплементации (тикеты)

### T-E01 — ExchangeRateConfig + ResourceExchangeResolver
- SO: `ExchangeRateConfig` + `ExchangeRateEntry`
- POCO: `ResourceExchangeResolver` (lookup cache)
- Тест: через `unity_reflect` проверить загрузку SO

### T-E02 — ExchangeWorld (server POCO)
- `ExchangeWorld.TryPack / TryUnpack`
- Полная бизнес-логика с rollback
- Тест: через `unity_reflect` вызвать методы (in EditMode)

### T-E03 — ExchangeServer (NetworkBehaviour)
- RPC-хаб: `RequestPackRpc / RequestUnpackRpc / ExchangeResultRpc`
- Zone validation (MarketZone)
- Интеграция с MarketClientState для обновления UI

### T-E04 — ExchangerTab (UI)
- UXML + USS для двухпанельного UI
- 4-я вкладка в MarketWindow (SwitchTab/"exchanger")
- Инвентарь ListView + Склад ListView + кнопки Pack/Unpack
- Фильтрация: только packable items в инвентаре

### T-E05 — Scene placement + test items
- Exchanger GameObject в MarketZone (BootstrapScene или WorldScene)
- Test items: pickup на земле рядом с exchanger
- End-to-end проверка

---

## 7. Критические edge-cases

| Scenario | Обработка |
|----------|-----------|
| **Неполный стак:** игрок имеет 50 руды, packRate=100 | `requestedQuantity < packRate` → ERROR_INSUFFICIENT_FOR_ONE_BOX, UI показывает "нужно 100 шт" |
| **Частичная упаковка:** игрок имеет 250 руды, packRate=100 | Упаковывается 200 (2 boxes), 50 остаются в инвентаре (через floor) |
| **Склад полон:** warehouse достиг MaxWeight/MaxVolume | TryAdd возвращает failReason → rollback в InventoryWorld, UI показывает "склад полон" |
| **Ящика нет на складе:** TryUnpack без warehouse entry | ERROR_INSUFFICIENT_WAREHOUSE |
| **Инвентарь полон после распаковки:** unpack добавляет 100 предметов | rollback в Warehouse, UI показывает "инвентарь полон" |
| **Rate mismatch:** packRate=100, unpackRate=50 (разные) | Поддерживается — ящик может распаковываться в другое количество |
| **Double-spend:** два RPC одновременно | Обрабатывается последовательно (NGO 2.x serializes RPC per client) + InventoryWorld проверяет наличие |
| **Вне MarketZone:** игрок нажал F не у ExchangeServer | ZoneValidation → UI не открывается |
| **Нет конфига:** ResourceExchangeResolver не инициализирован | ExchangerTab показывает "обменник недоступен" |

---

## 8. Почему этот подход — лучший

1. **Zero-touch к существующему коду** — ни одна строчка в InventoryWorld, TradeWorld,
   RecipeData, MarketWindow не меняется
2. **Layered architecture** — Config → Resolver → World → Server → Tab, каждый слой
   тестируется независимо
3. **ResourceExchangeResolver — asset for future** — когда понадобится crafting из
   warehouse, Resolver уже есть
4. **UI — 4-я вкладка** — повторяет проверенный шаблон Contracts tab
5. **Минимальный risk** — всё новое, ничего не ломается
6. **Config-driven** — добавить новую пару = запись в SO, не код

---

## 9. Что НЕ входит (границы MVP)

- **Auto-упаковка при сборе** (Mining → сразу на склад) — это Phase 2
- **Crafting из Warehouse** — Phase 2 (использует ResourceExchangeResolver)
- **Drag-and-drop в Exchanger UI** — кнопки Pack/Unpack достаточно
- **Анимация упаковки** (VFX) — после Stage 3
- **Sound** — после Stage 3
- **Множественные MarketZone** — сейчас один Market per location
