# GDD-11: Inventory & Items — Project C: The Clouds

**Версия:** 1.1 | **Дата:** 10 июня 2026 г. (дизайн-контент без изменений с 6 апреля 2026 г.; добавлена §X «Реализация в коде») | **Статус:** 🟢 Документировано + реализовано (v2)
**Автор:** Qwen Code (Game Studio: @gameplay-programmer + @systems-designer) — дизайн, Mavis 2026-06-10 — раздел реализации

---

## 1. Overview

Система инвентаря Project C: The Clouds включает **подбор предметов**, **сундуки с LootTable**, **круговое колесо инвентаря** и **сетевую синхронизацию** подбора. Предметы сгруппированы по 8 типам.

### Ключевые особенности
- **8 типов предметов** — ItemType enum
- **Круговое колесо** — стиль GTA 5, GL-рендер, 8 секторов
- **LootTable** — ScriptableObject с шансами и min/max count
- **Сетевая синхронизация** — HidePickupRpc, OpenChestRpc (SendTo.Everyone)
- **Сохранение** — PlayerPrefs при Disconnect/Reconnect

---

## 2. Item Types

### 8 типов предметов

| ID | Тип | Название (лор) | Описание | Примеры |
|----|-----|---------------|----------|---------|
| 1 | Type1 | Тестовый предмет | [🔴 Будет заменено] | «Говно» (тест) |
| 2 | Type2 | Ресурсы | Строительные материалы | Металл, доски, тросы |
| 3 | Type3 | Еда | Пища и вода | Консервы, фильтры воды |
| 4 | Type4 | Топливо | Мезий для двигателей | Жидкий мезий, канистры |
| 5 | Type5 | Антигравий | Компоненты двигателей | Антигравиевые кристаллы |
| 6 | Type6 | Мезий | Яд/топливо, ключевой ресурс | Мезий-картриджи |
| 7 | Type7 | МНП | Мезий-антигравиевый препарат | Медикаменты, стимуляторы |
| 8 | Type8 | Латекс | Технический ресурс | Изоляция, уплотнители |

### ItemData (ScriptableObject)

| Поле | Тип | Описание |
|------|-----|----------|
| `itemName` | string | Название предмета |
| `itemType` | ItemType | Тип (Type1-Type8) |
| `description` | string | Описание |
| `icon` | Sprite | Иконка 128x128 [🔴 Запланировано] |
| `stackSize` | int | Максимальный стак [🔴 Запланировано] |
| `weight` | float | Вес предмета [🔴 Запланировано] |

---

## 3. Inventory System

### Хранение по типам

| Параметр | Описание |
|----------|----------|
| Структура | `Dictionary<ItemType, int>` — тип → количество |
| Ячейки | 8 (по одной на тип) |
| Группировка | Автоматическая — одинаковые предметы складываются |
| Лимит | [🔴 Запланировано] Максимальный вес/объём |
| Singleton | Inventory.Instance — глобальный доступ |

### Операции

| Метод | Описание |
|-------|----------|
| `AddItem(ItemData item)` | Добавить 1 предмет |
| `AddMultipleItems(ItemData item, int count)` | Добавить N предметов |
| `RemoveItem(ItemType type, int count)` | Удалить N предметов |
| `GetCount(ItemType type)` | Получить количество |
| `SaveToPrefs()` | Сохранить в PlayerPrefs (для реконнекта) |
| `LoadFromPrefs()` | Загрузить из PlayerPrefs |

---

## 4. Item Pickup

### Подбор предметов

| Параметр | Значение |
|----------|----------|
| Клавиша | E |
| Радиус | 3м |
| Приоритет | Сундуки > предметы |
| Режим | Только пеший режим |
| Реализация | ItemPickupSystem.cs |

### PickupItem (компонент на сцене)

| Поле | Тип | Описание |
|------|-----|----------|
| `itemData` | ItemData | Ссылка на предмет |
| `bobSpeed` | float | Скорость покачивания |
| `Collect()` | method | Подбор предмета |
| `HidePickupRpc()` | Rpc | Скрытие у всех игроков |

### Визуализация

| Элемент | Описание |
|---------|----------|
| Покачивание | Синусоидальное движение вверх-вниз |
| Радиус | Gizmos в редакторе |
| Подсветка | [🔴 Запланировано] При приближении игрока |

---

## 5. Chest / Loot System

### Сундуки

| Параметр | Описание |
|----------|----------|
| Компонент | ChestContainer.cs |
| Открытие | E, радиус 3м |
| Анимация | Поворот + масштаб |
| LootTable | ScriptableObject |
| Синхронизация | OpenChestRpc (SendTo.Everyone) |
| Автоуничтожение | После открытия [🔴 Запланировано] |

### LootTable (ScriptableObject)

| Поле | Тип | Описание |
|------|-----|----------|
| `entries` | List<LootEntry> | Таблица добычи |
| `guaranteedItems` | List<ItemData> | Гарантированные предметы |

### LootEntry

| Поле | Тип | Описание |
|------|-----|----------|
| `item` | ItemData | Предмет |
| `chance` | float (0-1) | Шанс выпадения |
| `minCount` | int | Минимальное количество |
| `maxCount` | int | Максимальное количество |

### Формулы лута

| Формула | Описание |
|---------|----------|
| `roll = Random.Range(0f, 1f)` | Бросок шанса |
| `if (roll <= entry.chance) → add` | Проверка выпадения |
| `count = Random.Range(minCount, maxCount + 1)` | Количество |

---

## 6. Inventory UI

### Круговое колесо (InventoryUI.cs)

| Параметр | Описание |
|----------|----------|
| Активация | Tab — открыть/закрыть |
| Секторы | 8 (по типам предметов) |
| Рендер | GL-линии |
| Цвета | Зелёный = есть предметы, серый = пусто |
| Hover | Подсветка сектора мышью |
| Подсписки | При наведении на сектор с >1 предметом |
| Вспышка | При получении лута |

### Визуальное оформление

| Элемент | Описание |
|---------|----------|
| Радиус колеса | 120px |
| Ширина сектора | 45° (360°/8) |
| Цвет активного | `#4CAF50` (зелёный) |
| Цвет пустого | `#666666` (серый) |
| Текст | Название типа + количество |
| Вспышка | Кратковременная подсветка при AddItem |

### ControlHintsUI

| Подсказка | Расположение | Обновление |
|-----------|-------------|------------|
| E — подобрать | Левый верхний угол | Каждый кадр |
| F — сесть/выйти | Левый верхний угол | Каждый кадр |
| Tab — инвентарь | Левый верхний угол | Каждый кадр |
| Сундук с иконкой | Левый верхний угол | Статический |

---

## 7. Item Persistence

### Сохранение инвентаря

| Метод | Описание |
|-------|----------|
| `SaveToPrefs()` | Сохраняет CSV ID предметов в PlayerPrefs |
| `LoadFromPrefs()` | Загружает из PlayerPrefs при реконнекте |
| Trigger | Disconnect → Save, Reconnect → Load |
| Формат | CSV: `itemID,count;itemID,count;...` |

### ItemDatabaseInitializer

| Функция | Описание |
|---------|----------|
| Авто-регистрация | При старте: Resources/Items/, PickupItem, LootTable |
| ScriptableObject | Загрузка всех ItemData из Resources |
| **Crafting (крафт)** | **✅ Реализован (T-C01–T-C07c). Списание/выдача через `InventoryWorld.RemoveItems`/`AddItemDirect`. См. `docs/Crafting_system/ROADMAP.md`** |
| Сцена | Регистрация PickupItem компонентов |

---

## 8. Network Sync

### Синхронизация подбора

| Метод | Target | Описание |
|-------|--------|----------|
| `HidePickupRpc()` | SendTo.Everyone | Предмет исчезает у всех |
| `OpenChestRpc()` | SendTo.Everyone | Сундук открывается у всех |

### Текущие ограничения

| Ограничение | Описание |
|-------------|----------|
| Инвентарь НЕ синхронизируется | Каждый игрок видит только свои предметы |
| NetworkInventory | [⚠️ Частично] NetworkVariable<string> не работает в NGO |
| Серверная валидация | [🔴 Запланировано] Этап 3 |

---

## 9. Future Features

### [🔴 Запланировано] Этап 3

| Фича | Описание |
|------|----------|
| Слот 9 (центр) | Ключевой предмет/квестовый |
| Полная синхронизация | NetworkVariable/NetworkList для инвентаря |
| Торговля между игроками | UI обмена предметами |
| Крафт предметов | Рецепты, верстак, ресурсы |
| Лимит веса | Максимальный груз |
| Иконки 128x128 | game-icons.net или кастомные |
| «Облачный» дизайн колеса | Ghibli-эстетика |

---

## 10. Formulas

| Формула | Описание |
|---------|----------|
| `lootChance = Random.Range(0f, 1f) <= entry.chance` | Шанс выпадения |
| `lootCount = Random.Range(entry.minCount, entry.maxCount + 1)` | Количество |
| `pickupDistance = Vector3.Distance(player, item) < 3f` | Радиус подбора |
| `chestOpenDistance = Vector3.Distance(player, chest) < 3f` | Радиус сундука |
| `inventoryCount[type] += count` | Добавление в инвентарь |

---

## 11. Edge Cases

| Ситуация | Поведение | Реализация |
|----------|-----------|-----------|
| **Два игрока подбирают одновременно** | Сервер определяет порядок | ✅ HidePickupRpc |
| **Сундук уже открыт** | [🔴 Запланировано] Не открывается повторно | — |
| **Инвентарь полон** | [🔴 Запланировано] Предмет остаётся на земле | — |
| **Дисконнект при подборе** | Инвентарь сохранён в PlayerPrefs | ✅ NetworkManagerController |
| **Предмет застрял в текстуре** | [🔴 Запланировано] Автоудаление через таймаут | — |
| **LootTable пустой** | Ничего не выпадает | ✅ Проверка entries.count |

---

## 12. Tuning Knobs

| Параметр | Мин | Макс | Текущее | Влияние |
|----------|-----|------|---------|---------|
| `pickupRadius` | 1 | 10 | 3 | Радиус подбора |
| `chestOpenRadius` | 1 | 10 | 3 | Радиус сундука |
| `bobSpeed` | 0.5 | 5.0 | 2.0 | Скорость покачивания |
| `wheelRadius` | 80 | 200 | 120 | Радиус колеса UI |
| `flashDuration` | 0.1 | 2.0 | 0.5 | Длительность вспышки |

---

## 13. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | E подбирает предмет (< 3м) | Подойти, нажать E | ✅ |
| 2 | E открывает сундук | Подойти к сундуку, нажать E | ✅ |
| 3 | Tab открывает круговое колесо | Нажать Tab | ✅ |
| 4 | Секторы отображают типы предметов | Проверить 8 секторов | ✅ |
| 5 | Зелёные сектора = есть предметы | Проверить цвета | ✅ |
| 6 | Hover подсвечивает сектор | Навести мышь | ✅ |
| 7 | Подсписки при >1 предмете | Навести на сектор с несколькими | ✅ |
| 8 | Вспышка при получении лута | Подобрать предмет | ✅ |
| 9 | Предмет исчезает у всех после подбора | 2 игрока, проверить | ✅ |
| 10 | Сундук открывается у всех | 2 игрока, проверить | ✅ |
| 11 | Инвентарь сохраняется при дисконнекте | Отключиться, подключиться | ✅ |
| 12 | ItemDatabaseInitializer регистрирует предметы | Проверить при старте | ✅ |
| 13 | Иконки предметов 128x128 | [🔴 Запланировано] | 🔴 |
| 14 | Торговля между игроками | [🔴 Запланировано] | 🔴 |
| 15 | Крафт предметов | [🔴 Запланировано] | 🔴 |
| 16 | **sub_inventory-tab (P → таб «Инвентарь»)** | P → таб в CharacterWindow, фильтры по типу. См. `docs/Character-menu/sub_inventory-tab/` | 🟢 DONE (Phases 0-7, 2026-06-05) |
| 17 | **MetaRequirement extensions** (`HasAllItems` / `HasAnyItem` / `CountOf` / `GetMissingItems`) | см. §X ниже | 🟢 DONE (R2-META-REQ-001, 2026-06-06) |
| 18 | **ItemRegistry** (single source of truth для item IDs) | см. §X ниже | 🟢 DONE (M14, 2026-06-09) |
| 19 | **MetaRequirement v1 (lock-key)** | см. §X ниже | 🟢 DONE (R2-META-REQ-001) |

---

## X. Реализация в коде (v2, 2026-06-05..09)

> **Секция добавлена Mavis 2026-06-10.** Дизайн-контент (типы предметов, колесо, формулы веса/объёма) остаётся в зоне game-designer'а. Здесь — **только статус реализации** и ссылки на актуальные артефакты.

### X.1 Inventory v2 (Phases 0-7, 2026-06-05)

**Архитектура:**
```
SERVER (host):
[InventoryServer] : NetworkBehaviour (BootstrapScene, DontDestroyOnLoad)
    ├── InventoryWorld (POCO singleton) — бизнес-логика
    │     ├── Dictionary<int, ItemData> — ItemDatabase
    │     ├── Dictionary<ulong, InventoryData> — per-player state
    │     └── 4 extensions: HasAllItems, HasAnyItem, CountOf, GetMissingItems (META-REQ)
    └── IInventoryRepository (interface) — abstract storage
          └── JsonInventoryRepository (default, per-client JSON в persistentDataPath)

CLIENT:
[InventoryClientState] (singleton, RuntimeInitializeOnLoadMethod)
    └── OnSnapshotUpdated event → оба UI подписаны
          ├── InventoryUI (TAB-колесо) — GTA-стиль
          └── CharacterWindow → таб «Инвентарь» (sub_inventory-tab) — детальный список
```

**Ключевые фичи:**
- ✅ Single source of truth: TAB-колесо + P-таб читают **тот же** `InventoryClientState`
- ✅ Server-authoritative: все мутации только на сервере через `TryPickup` / `TryDrop` / `AddItemDirect` / `TryRemove` (T-Q14)
- ✅ Persistence: `JsonInventoryRepository`, atomic write per-client JSON, load on `OnClientConnectedCallback`
- ✅ `ItemRemovedEvent` / `ItemAddedEvent` через `WorldEventBus` (publish/subscribe для quest triggers)
- ✅ Drop в мир: server-spawn `PickupItem` (R3-INV-DROP-001: visual representation теряется, см. `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md`)

**Stats (v2 refactor):**
- Код: `Assets/_Project/Items/Core/InventoryWorld.cs` (~600 строк с extensions), `InventoryServer.cs`, `InventoryClientState.cs`
- UI: `CharacterWindow.cs` (1345+ LOC) + sub_inventory-tab
- Документация: `docs/Character-menu/sub_inventory-tab/{00..80}*.md` (8 файлов, ~150 KB)

### X.2 ItemRegistry (M14, 2026-06-09)

**Проблема:** `InventoryWorld.GetOrRegisterItemId()` и `QuestWorld.ResolveItemId()` использовали **независимые** нумерации, которые **случайно** совпадали (alphabetical order из `Resources.LoadAll`). При добавлении item'а вне `Resources/Items/` — id **молча** разъедутся, квесты перестанут работать.

**Решение:** **`ItemRegistry`** (singleton SO) — single source of truth для `id ↔ ItemData` mapping.

**API:**
```csharp
public class ItemRegistry : ScriptableObject
{
    public void RegisterItem(int id, ItemData item);
    public bool TryGetItem(int id, out ItemData item);
    public bool TryGetId(ItemData item, out int id);
    public IEnumerable<ItemData> GetAllItems();
}
```

**Wiring:**
- ✅ `Assets/_Project/Items/Core/ItemRegistry.cs` (singleton SO, 32 items)
- ✅ `Assets/_Project/Items/Data/ItemRegistry.asset` (auto-populated)
- ✅ `InventoryWorld.RegisterAllItems()` читает из `ItemRegistry.Instance` (fallback на Resources если null)
- ✅ `QuestWorld.ResolveItemId` использует `ItemRegistry.TryGetId(itemName)`, fallback на `Resources` scan
- ✅ `QuestServer.FireDialogAction` (GiveItem/TakeItem) prefer `action.itemId` (T-Q27), fallback на stringParam, fallback на ItemRegistry name lookup
- ✅ `DialogueAction.itemId + itemType` explicit fields (T-Q27)

**Migration (T-Q28):** 2 quest objectives обновлены (`CollectCopperOre.asset`, `StageMultiDemo.asset`): string names → int ids.

**Verify (2026-06-09):** ItemRegistry populated: 32 entries (id 1-32). Round-trip 0 errors.

**Известные ограничения:**
- 🟡 Init order: `InventoryWorld` должен init раньше `QuestWorld` (registry → item IDs). Fallback на `Resources.LoadAll` если null.

### X.3 MetaRequirement v1 (lock-key, R2-META-REQ-001, 2026-06-06)

**Цель:** обобщить **Ship Key Subsystem** (1 предмет на 1 корабль) в **универсальную систему требований** для любых Interactable-объектов: корабли, двери, контейнеры, терминалы, квестовые зоны.

**Архитектура (generic, не только корабли):**
- ✅ `MetaRequirementRegistry` (NetworkBehaviour, scene-placed в `BootstrapScene`) — `RegisterMetaRequirement(netId, MetaRequirement)`, `CanPlayerUse(clientId, netId) → bool + reason`, `RequestCanUseRpc(netId) → TargetRpc`
- ✅ `MetaRequirement` (NetworkBehaviour MonoBehaviour, generic) — `_requiredItems: ItemData[]`, `_logic: RequirementLogic { All, Any, AtLeastN }`, `_requiredCount: int`, `_interactableDisplayName: string`, `OnInventoryChanged` event, `CanPlayerUse(clientId, out reason)`, `ProgressInfo` struct
- ✅ `MetaRequirementClientState` (client singleton) — `OnCanUseResponse`, `OnBindingsPushed`, `OnInteractableFound`
- ✅ `MetaRequirementToast` (UIDocument) — generic UI: "X/N собрано" + список недостающих

**Extensions в `InventoryWorld`:**
- ✅ `HasAllItems(ulong clientId, int[] itemIds)` — AND-логика
- ✅ `HasAnyItem(ulong clientId, int[] itemIds)` — OR-логика
- ✅ `CountOf(ulong clientId, int itemId)` — подсчёт (для AT_LEAST_N)
- ✅ `GetMissingItems(ulong clientId, int[] itemIds)` — массив недостающих

**Wiring:**
- ✅ `NetworkManagerController.CreateMetaRequirementClientState()` (auto-spawn root GO в `Awake`)
- ✅ `NetworkPlayer.TryInteractNearestMetaRequirement()` (E-key entry point для НЕ-кораблей)
- ✅ `NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc` + `ReceiveMetaRequirementBindingsTargetRpc`

**Алиасы (backward compat):**
- ⏳ `ShipKeyBinding.cs` — `[Obsolete]` empty subclass → `MetaRequirement`
- ⏳ `ShipKeyServer.cs` / `ShipKeyClientState.cs` — legacy API сохранён, `[Obsolete]`
- ⏳ TODO (через 1-2 релиз-цикла): удалить алиасы после миграции всех сцен

**Тестовые ассеты:**
- ✅ 3 SO `ItemData`: `Item_Key_Blue/Red/Green.asset`
- ✅ 6 URP/Lit материалов: `Key_{Blue,Red,Green}.mat` + `LockBox_{Blue,Red,Green}.mat`
- ✅ `MetaRequirementPanelSettings.asset`
- ✅ `WorldScene_0_0.unity`: `[MetaRequirement_Test]` parent + 3 Pickup + 3 LockBox
- ✅ `BootstrapScene.unity`: `[MetaRequirementRegistry]` + `[MetaRequirementToast]`

**Compile (2026-06-06):** 0 errors, warnings только pre-existing + by-design obsolete-usage в алиасах.

**TODO (Этап 2+):**
- ⏳ `_consumeOnUse` логика + reservation pattern (поле есть, логика — TODO)
- ⏳ `ProgressInfo` UI в `MetaRequirementToast` (multi-item tooltip)
- ⏳ Disconnect → reconnect race fix
- ⏳ Multi-MetaRequirement в одной зоне (сейчас 1→1)
- ⏳ Использование `MetaRequirement` для квестов (T-Q?? когда потребуется)

### X.4 Где смотреть актуальный статус

- **`docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md`** — обзор Inventory v2 + sub_inventory-tab
- **`docs/MetaRequirement/00_OVERVIEW.md`** — дизайн MetaRequirement
- **`docs/MetaRequirement/99_CHANGELOG.md`** — changelog MetaRequirement
- **`docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md`** — миграция Ship Key → MetaRequirement
- **`docs/NPC_quests/old_session_log/M14_DESIGN_NOTE.md`** — ItemRegistry design note
- **`docs/MMO_Development_Plan.md`** §1.6, §1.9 — общий план

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [INVENTORY_SYSTEM.md](../INVENTORY_SYSTEM.md) | [`docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md`](../Character-menu/sub_inventory-tab/00_OVERVIEW.md) | [`docs/MetaRequirement/00_OVERVIEW.md`](../MetaRequirement/00_OVERVIEW.md)
