# GDD-11: Inventory & Items — Project C: The Clouds

**Версия:** 3.0 | **Дата:** 14 июля 2026 г. | **Статус:** 🟢 Актуализировано по коду — §1-§12 переписаны под Inventory v2 (ItemRegistry, InventoryWorld, NetworkList-синхронизация), §2 (типы предметов обновлены: Type1-Type8 → конкретные), §6 (круговое колесо → CharacterWindow + таб Инвентарь)
**Автор:** Малков Леонид Андреевич

---

## 1. Overview

Система инвентаря Project C: The Clouds включает **сервер-авторитативное хранение предметов**, **подбор/дроп через RPC**, **синхронизацию через NetworkList** и **UI через CharacterWindow**. Предметы имеют конкретные типы (не Type1-Type8) и могут экипироваться в слоты персонажа.

### Ключевые особенности
- **9 типов предметов** — ItemType enum: Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech, Key
- **ItemRegistry** — ScriptableObject, единый источник истины для id↔ItemData mapping (32 registered items)
- **Сервер-авторитативное** — все мутации только на сервере через `InventoryWorld.TryPickup`/`TryDrop`/`AddItemDirect`/`TryRemove`
- **NetworkList синхронизация** — `InventoryServer` → `InventoryClientState.OnSnapshotUpdated`
- **CharacterWindow UI** — P → таб «Инвентарь» (вместо старого кругового колеса GTA-style)
- **JSON persistence** — per-client JSON в `persistentDataPath` через `JsonInventoryRepository`
- **ICombatDamageProvider** — WeaponItemData унифицирован (ThrownItemData удалён)

### История изменений

| Версия | Дата | Изменения |
|--------|------|-----------|
| 1.0 | Апрель 2026 | Первая версия: Dictionary<ItemType,int>, 8 типов, круговое колесо, PlayerPrefs |
| 2.0 | 31 июля 2026 | Инвентарь v2: сервер-авторитативный, JsonInventoryRepository, CharacterWindow таб |
| 3.0 | 14 июля 2026 | Переписаны §1-§12 под актуальную архитектуру v2; Weapon Unification (ThrownItemData → WeaponItemData) |

---

## 2. Item Types

### 9 типов предметов

| ID | Тип | Название (лор) | Описание | Примеры |
|----|-----|---------------|----------|---------|
| 0 | Resources | Ресурсы | Строительные материалы, руда | Металл, доски, тросы |
| 1 | Equipment | Экипировка | Оружие, броня, инструменты | Меч, щит, шлем |
| 2 | Food | Еда | Пища и вода | Консервы, фильтры воды |
| 3 | Fuel | Топливо | Мезий для двигателей | Жидкий мезий, канистры |
| 4 | Antigrav | Антигравий | Компоненты антиграв-двигателей | Антигравиевые кристаллы |
| 5 | Meziy | Мезий | Яд/топливо, ключевой ресурс | Мезий-картриджи |
| 6 | Medical | МНП/Медикаменты | Мезий-антигравиевый препарат | Медикаменты, стимуляторы |
| 7 | Tech | Латекс/Тех. | Технический ресурс | Изоляция, уплотнители |
| 8 | Key | Ключи | Ключ-стержни для кораблей (R2-SHIP-KEY-003) | ShipLight Key, ShipMedium Key |

> **Примечание:** В версии 1.0 (legacy) типы назывались Type1-Type8. При миграции на v2 типы получили осмысленные имена. Тип Key (8) добавлен в R2-SHIP-KEY-003 для отдельного типа ключей кораблей.

### ItemData (ScriptableObject)

| Поле | Тип | Описание | Статус |
|------|-----|----------|--------|
| `itemName` | string | Название предмета | ✅ |
| `itemType` | ItemType | Тип (Resources/Equipment/...) | ✅ |
| `description` | string | Описание предмета | ✅ |
| `icon` | Sprite | Иконка | ✅ (планировалось) |
| `maxStack` | int | Максимальное количество в стаке (1 = non-stackable) | ✅ |
| `weightKg` | float | Вес предмета в кг (для будущей cargo-системы) | ✅ |
| `equipSlot` | EquipSlot | В какой слот экипируется (None = нельзя надеть) | ✅ |
| `visualPrefab` | GameObject | 3D-меш для отображения в мире и на персонаже | ✅ (Phase 1-2) |
| `attachBoneOverride` | HumanBodyBones | Кость для прикрепления (LastBone = default) | ✅ (Phase 2) |
| `attachPositionOffset` | Vector3 | Локальный offset от кости | ✅ |
| `attachRotationOffset` | Vector3 | Локальное вращение | ✅ |
| `attachScale` | Vector3 | Локальный масштаб | ✅ |

### EquipSlot (13 слотов)

| Слот | Значение | Описание |
|------|----------|----------|
| None | 0 | Нельзя надеть |
| Head | 1 | Голова |
| Chest | 2 | Грудь |
| Legs | 3 | Ноги |
| Feet | 4 | Ступни |
| Back | 5 | Спина |
| Hands | 6 | Руки |
| Accessory1 | 7 | Аксессуар 1 |
| Accessory2 | 8 | Аксессуар 2 |
| WeaponMain | 9 | Основное оружие |
| WeaponOff | 10 | Доп. оружие |
| Module1 | 20 | Модуль 1 |
| Module2 | 21 | Модуль 2 |
| Module3 | 22 | Модуль 3 |

### WeaponItemData (подкласс ItemData)

| Поле | Тип | Описание |
|------|-----|----------|
| `weaponClass` | WeaponClass | Класс оружия (Sword/Dagger/Spear/Mace/Crossbow/...) |
| `handling` | WeaponHandling | Melee/Ranged/Thrown/Placed |
| `damageDice` | DamageDice | Dice урона (d4-d20) |
| `baseDamage` | int | Flat-бонус к урону |
| `critModifier` | int | Модификатор крита (1d100+crit >= 100 → crit ×2) |
| `range` | float | Дальность (м) |
| `damageType` | DamageType | Physical/Ballistic/Antigrav/Explosive/Mesium |
| `explosionRadius` | float | Радиус AOE (для Thrown/Placed) |
| `throwRange` | float | Макс. дистанция броска (Thrown) |
| `fuseTimeSec` | float | Задержка до взрыва (сек) |
| `requiredProficiency` | SkillNodeConfig | Минимальный навык (gate) |
| `minTier` | int | Минимальный INT tier |

> **Weapon Unification (T-WPN-01-REF-02, июль 2026):** `ThrowableItemData` удалён — функционал перемещён в `WeaponItemData` с полем `WeaponHandling.Thrown`. Теперь 2 иерархии: `ItemData` + `WeaponItemData`.

---

## 3. Inventory System (v2)

### Архитектура

```
SERVER (host):
[InventoryServer] : NetworkBehaviour (BootstrapScene, DontDestroyOnLoad)
    ├── InventoryWorld (POCO singleton) — бизнес-логика
    │     ├── ItemDatabase: Dictionary<int, ItemData> — id → definition
    │     ├── Dictionary<ulong, InventoryData> — per-player state
    │     ├── TryPickup / TryDrop / TryMove / TryUse → InventoryResultDto
    │     ├── BuildSnapshot(clientId) → InventorySnapshotDto → NetworkList-синхронизация
    │     └── 4 extensions: HasAllItems, HasAnyItem, CountOf, GetMissingItems
    └── IInventoryRepository (interface) — abstract storage
          └── JsonInventoryRepository (default, per-client JSON)

CLIENT:
[InventoryClientState] (singleton, RuntimeInitializeOnLoadMethod)
    └── OnSnapshotUpdated event → UI подписано
          └── CharacterWindow → таб «Инвентарь» (sub_inventory-tab)
```

### Хранение

| Параметр | Описание |
|----------|----------|
| Структура | `Dictionary<int, int>` (itemId → count) + `_keySlots` для Key-предметов (itemId + instanceId) |
| Персистентность | JSON per-client (`JsonInventoryRepository`), атомарная запись |
| Singleton | `InventoryWorld.Instance` — глобальный server-side доступ |
| Инициализация | `InventoryWorld.CreateAndInitialize()` в `InventoryServer.OnNetworkSpawn` |
| Регистрация предметов | `ItemRegistry.Instance` (32 entries) + fallback на `Resources.LoadAll` |

### Операции

| Метод | Описание | Server-only |
|-------|----------|-------------|
| `TryPickup(clientId, itemId, type, worldPos, instanceId?)` | Подобрать предмет (с проверкой дистанции 5м) | ✅ |
| `TryDrop(clientId, itemId, count, worldPos)` | Выбросить предмет | ✅ |
| `TryMove(clientId, fromSlot, toSlot)` | Переместить в инвентаре | ✅ |
| `TryUse(clientId, itemId)` | Использовать предмет | ✅ |
| `AddItemDirect(clientId, itemId, count)` | Прямая выдача (для крафта/квестов) | ✅ |
| `TryRemove(clientId, itemId, count)` | Удалить предметы | ✅ |
| `HasAllItems(clientId, int[] itemIds)` | AND-логика (MetaRequirement) | ✅ |
| `HasAnyItem(clientId, int[] itemIds)` | OR-логика (MetaRequirement) | ✅ |
| `CountOf(clientId, int itemId)` | Подсчёт предметов | ✅ |
| `GetMissingItems(clientId, int[] itemIds)` | Массив недостающих | ✅ |
| `AddKeyItem(clientId, itemId, instanceId)` | Добавить Key-предмет с instanceId | ✅ |
| `BuildSnapshot(clientId)` | Построить DTO для клиента | ✅ |

### Rate Limiting

- Серверный rate limit в `InventoryServer.CheckRateLimit()` — защита от спама запросами
- Проверка дистанции в `TryPickup`: `Vector3.Distance(worldPos, playerPos) <= PICKUP_RANGE_M (5м)`
- Ownership-проверка в `TryDrop`: предмет должен быть в инвентаре клиента

---

## 4. Item Pickup

### Подбор предметов

| Параметр | Значение |
|----------|----------|
| Клавиша | E |
| Радиус | 5м (серверная валидация) |
| Приоритет | Сундуки > предметы |
| Режим | Только пеший режим |
| Реализация | `InventoryServer.RequestPickupRpc` → `InventoryWorld.TryPickup` |

### PickupItem (компонент на сцене)

| Поле | Тип | Описание |
|------|-----|----------|
| `itemData` | ItemData | Ссылка на предмет |
| `instanceId` | int | ID экземпляра (для KeyRod) |
| `bobSpeed` | float | Скорость покачивания |
| `Collect()` | method | Подбор предмета → `RequestPickupRpc` |

### Поток подбора (v2)

```csharp
1. Player нажимает E
2. NetworkPlayer.RequestPickupRpc(itemId, type, instanceId, pos)
3. InventoryServer на сервере:
   - Валидация дистанции: Distance(worldPos, playerPos) <= 5м
   - Rate limit проверка
   - InventoryWorld.TryPickup(clientId, itemId, type, pos, instanceId)
4. InventoryWorld создаёт/обновляет InventoryData
5. Snapshot отправляется клиенту через NetworkList
6. PickupItem.HidePickupRpc() — предмет исчезает у всех
```

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

---

## 6. Inventory UI

### CharacterWindow → таб «Инвентарь» (sub_inventory-tab)

| Параметр | Описание |
|----------|----------|
| Активация | P — открыть CharacterWindow |
| Таб | «Инвентарь» (левый таб в окне персонажа) |
| Фильтры | По типу предметов (Resources/Equipment/Food/...) |
| Слоты | Визуализация equip-слотов (Head, Chest, WeaponMain, WeaponOff, ...) |
| Детали | Клик по предмету → подробная карточка с описанием, статами, иконкой |
| Дроп | Кнопка «Выбросить» в карточке предмета |

### Circular Wheel (GTA-style) — УДАЛЁН

> **Историческое примечание:** В версии 1.0 использовалось круговое колесо (8 секторов, GL-линии, Tab-активация). Заменено на CharacterWindow с табом «Инвентарь» в Inventory v2 (Phases 0-7, 2026-06-05).

### CharacterEquipmentVisualApplier

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `CharacterEquipmentVisualApplier` | `Scripts/Customisation/` | Bone mapping для визуала экипировки |
| `EquipmentVisualSocket` | `Scripts/Items/` | Определение socket на скелете (HumanBodyBones) |
| `visualPrefab` на ItemData | `Scripts/Items/ItemData.cs` | 3D-модель + параметры прикрепления |
| `EquipmentChangedHandler` | `Scripts/Customisation/` | Instantiate/Destroy визуала при смене экипировки |

---

## 7. Item Persistence

### Сохранение инвентаря (v2)

| Метод | Описание |
|-------|----------|
| `JsonInventoryRepository.Save(clientId, InventoryData)` | Атомарная запись JSON в persistentDataPath |
| `JsonInventoryRepository.Load(clientId)` | Загрузка из JSON при коннекте |
| Trigger | `OnClientConnectedCallback` → Load, `Shutdown` → Save всех dirty игроков |
| Формат | JSON: `{items: [{itemId, count, instanceId?}], keys: [...]}` |

### Legacy (v1)

| Метод | Описание | Статус |
|-------|----------|--------|
| `SaveToPrefs()` | Сохранял CSV ID предметов в PlayerPrefs | ❌ Заменён на JSON |
| `LoadFromPrefs()` | Загружал из PlayerPrefs при реконнекте | ❌ Заменён на JSON |

---

## 8. Network Sync

### Синхронизация инвентаря (v2)

| Метод | Target | Описание |
|-------|--------|----------|
| `InventoryServer.OnSnapshotUpdated` | NetworkList | Сервер шлёт снапшот инвентаря клиенту |
| `InventoryClientState.OnSnapshotUpdated` | Event | Клиентский singleton, UI подписаны |
| `RequestPickupRpc` | ServerRpc | Запрос на подбор предмета |
| `RequestDropRpc` | ServerRpc | Запрос на выбрасывание |
| `RequestMoveRpc` | ServerRpc | Перемещение в инвентаре |

### Формат снапшота

```csharp
public struct InventorySnapshotDto : INetworkSerializable
{
    public InventoryItemDto[] items;  // Массив предметов (itemId, count, instanceId?)
    public int totalCount;            // Общее количество
}
```

### Анти-чит / Безопасность

| Механизм | Описание |
|----------|----------|
| Server-authoritative | Все мутации только на сервере |
| Distance check | TryPickup проверяет дистанцию 5м |
| Rate limit | InventoryServer.CheckRateLimit() |
| Ownership check | TryDrop проверяет, что предмет у клиента |
| Item ID validation | Фильтрация невалидных itemId из старых сохранений |

---

## 9. Реализованные функции (бывшие Future Features)

| Функция | Статус | Описание |
|---------|--------|----------|
| ItemRegistry (32 items) | ✅ DONE (M14, 2026-06-09) | Single source of truth id↔ItemData |
| Полная NetworkList синхронизация | ✅ DONE (v2) | InventoryServer → ClientState |
| Серверная валидация | ✅ DONE (v2) | Distance, ownership, rate limit |
| InventoryTab (CharacterWindow) | ✅ DONE (Phases 0-7) | Замена кругового колеса |
| MetaRequirement extensions | ✅ DONE (R2-META-REQ-001) | HasAllItems/HasAnyItem/CountOf/GetMissingItems |
| Ship Key v2 (экземпляры) | ✅ DONE (R2-SHIP-KEY-003) | KeyRodInstanceWorld + itemId↔instanceId |
| WeaponItemData + Equipment Visual | ✅ DONE (T-CB-19 + T-EV) | Weapon stats, bone mapping, visualPrefab |
| Weapon & Item Unification | ✅ DONE (T-WPN-01-REF-02) | ThrowableItemData → WeaponItemData |
| Крафт предметов | ✅ DONE (T-C01–T-C07c) | Списание через InventoryWorld.RemoveItems |
| JSON persistence | ✅ DONE (v2) | JsonInventoryRepository |

### [🔴 Запланировано] Будущее

| Фича | Описание |
|------|----------|
| Лимит веса/объёма | Интеграция с ShipCargoLimitsConfig |
| Иконки 128x128 | game-icons.net или кастомные (Phase 6) |
| Торговля между игроками | UI обмена через TradeWorld |
| «Облачный» дизайн UI | Ghibli-эстетика для окон |

---

## 10. Acceptance Criteria (актуализировано)

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | E подбирает предмет (< 5м) | Подойти, нажать E | ✅ v2 |
| 2 | E открывает сундук | Подойти к сундуку, нажать E | ✅ v2 |
| 3 | P открывает CharacterWindow → таб Инвентарь | Нажать P, проверить таб | ✅ v2 |
| 4 | Список предметов по типам | Отфильтровать в табе | ✅ v2 |
| 5 | Сервер-авторитативный подбор | Два игрока — один подбирает | ✅ v2 |
| 6 | ItemRegistry: 32 registered items | Проверить ItemRegistry.asset | ✅ |
| 7 | JSON persistence при дисконнекте | Отключиться, подключиться, проверить инвентарь | ✅ v2 |
| 8 | Drop в мир (server-spawn PickupItem) | Выбросить предмет, подобрать снова | ✅ (R3-INV-DROP-001) |
| 9 | WeaponItemData: combat stats | Проверить поля damageDice/baseDamage/... | ✅ |
| 10 | Equipment Visual: визуал на персонаже | Экипировать предмет → 3D-модель на кости | ✅ Phase 2 |
| 11 | Key-предметы с instanceId | Подобрать KeyRod, проверить instanceId в инвентаре | ✅ R2-SHIP-KEY-003 |
| 12 | Rate limit на PickupRpc | Спам запросами → сервер отклоняет | ✅ v2 |

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


### X.4 Ship Key v2 — уникальные экземпляры (R2-SHIP-KEY-003, 2026-06-19)

**Концепция:** в отличие от обычных предметов (itemId достаточно), ключ-стержень — это **пара** `(itemId, instanceId)`:
- `itemId` определяет **тип** ключа (Light/Medium/Heavy) — ItemData SO
- `instanceId` определяет **физический экземпляр** ключа в мире — уникальный серверный ID

Один itemId может иметь несколько экземпляров в игре, каждый — отдельный `KeyRodInstance` в `KeyRodInstanceWorld`. Например, на 3 разных ShipController в сцене — 3 KeyRodInstance с одним itemId=2009 (Key_heavy_ship), но разными instanceId.

**Хранение в инвентаре:**

В `InventoryData` ключи хранятся в **двух параллельных структурах**:
- `_keyIds : List<int>` — для сериализации и быстрого подсчёта
- `_keySlots : List<InventorySlot>` — itemId + instanceId, для server-side логики

Для корректной работы добавлять нужно через `AddKeyItem(itemId, instanceId)`, а не `AddItem(itemType, itemId)`.

**Гарантии уникальности:**

1. **1:1 ship ↔ instanceId**: `_primaryInstanceByShipId[shipId] = instanceId`. На каждый корабль — один экземпляр ключа.
2. **1:1 instanceId ↔ state**: `KeyRodInstanceState` (Active / Lost / Destroyed).
3. **Persistence**: `JsonKeyRodInstanceRepository` хранит ВСЕ instance (Active+Lost) в `KeyRodInstances.json`. На рестарте `KeyRodInstanceWorld.CreateAndInitialize` восстанавливает реестр.
4. **Drop↔pickup не дублирует**: при pickup drop'нутого ключа сервер ищет существующий Lost instance по `(itemId, owner=NONE)`, реактивирует, а не создаёт новый.

**Игровые последствия:**

- **Передача ключа** = передача права собственности на корабль. Игрок A → Игрок B: `TransferInstance(A→B)` + обновление `_instancesByPlayer`.
- **Потеря ключа** (выбросил, уничтожен) = нельзя сесть в корабль. `ShipOwnershipRequirement.IsOwnerOfShip(clientId, shipNetId)` вернёт false.
- **Крафт копий** (Phase 2) — только через специальную рецептуру. Без копий — ключи нельзя "раздобыть задним числом".

**Файлы:**
- ✅ `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` — POCO
- ✅ `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` — server-only static registry
- ✅ `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceBinding.cs` — scene-placed MonoBehaviour
- ✅ `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` — IPlayerDataRepository
- ✅ `Assets/_Project/Scripts/Ship/Key/ShipOwnershipRequirement.cs` — auto-attach
- ✅ `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` — NetworkVariable struct
- ✅ `Assets/_Project/Scripts/Ship/Client/ShipTelemetryClientState.cs` — client cache

**UI:** см. `GDD_10_Ship_System.md` §13.3 + `docs/Ships/Key-subsystem/26_TKEY08_MYSHIPS_TAB_PLAN.md`.

**Changelog:** `docs/Ships/Key-subsystem/99_CHANGELOG.md` (v1–v20).

### X.5 WeaponItemData + Equipment Visual (T-CB-19 + T-EV, 2026-06-27..28)

**Новое:** Подкласс `ItemData` для оружия + визуальное отображение экипированных предметов на персонаже через bone mapping.

#### X.5.1 WeaponItemData

| Поле | Тип | Описание |
|------|-----|----------|
| `damage` | float | Базовый урон оружия |
| `range` | float | Дальность атаки (м) |
| `attackSpeed` | float | Скорость атаки (атак/сек) |
| `skillType` | SkillType | Тип навыка для анимации (Punch/Kick/Block/Sword/...) |
| `weaponVisualPrefab` | GameObject | 3D-модель оружия для отображения на персонаже |

**Реализация:** `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` — наследует `ItemData`, добавляет боевые параметры.

#### X.5.2 Equipment Visual System (T-EV Phase 2)

| Компонент | Файл | Назначение | 
|-----------|------|------------|
| `CharacterEquipmentVisualApplier` | `Scripts/Customisation/CharacterEquipmentVisualApplier.cs` | Bone mapping: Weapon/Shield/Helmet/Chest (+7 bone slots) |
| `EquipmentVisualSocket` | `Scripts/Items/EquipmentVisualSocket.cs` | Определение socket на скелете (HumanBodyBones) |
| `visualPrefab` on ItemData | `Scripts/Items/ItemData.cs` | GameObject reference + attach params (position/rotation/scale) |
| `EquipmentChangedHandler` | `Scripts/Customisation/EquipmentChangedHandler.cs` | OnEquipmentChanged → Instantiate/Destroy visual |
| Equip bug fix | `CharacterWindow.ChangeEquipmentSlot` | Rate-limit N callback предотвращает duplicate equip |

**Key decisions:**
- Visual prefab живёт как child анимированного bone — следует за анимацией skeleton
- `CharacterCustomisationApplier` — единая точка входа для customisation + equipment visual
- Socket mapping через `HumanBodyBones` enum (стандарт Unity Avatar): Weapon → RightHand, Shield → LeftHand, Helmet → Head, Chest → Spine
- `attachBoneOverride` + `attachPositionOffset/Rotation/Scale` — per-item настройки позиционирования

**Stats:** +5 C# файлов, ~15 KB кода.

### X.6 Где смотреть актуальный статус (дополнение)

- **`docs/Character/Skills/`** — WeaponItemData + Equipment Visual дизайн
- **`docs/Character/Customisation/`** — Customisation + Equipment Visual implementation

### X.7 Weapon & Item Unification (T-WPN-01-REF-02, июль 2026) ✅

**Контекст:** Три несвязанные иерархии предметов (`ItemData` — 581 шт., `WeaponItemData` — 7 шт., `ThrowableItemData` — 2 шт.) + хардкод `is TypeCheck` в 5+ файлах.

**Решение:**
- `ICombatDamageProvider` — интерфейс боевого предмета (GetDamage/DamageType/WeaponHandling/Range/Cooldown)
- `WeaponItemData + ThrowableItemData → единый класс` (ThrowableItemData удалён)
- `equipSlot` унифицирован в `ItemData` (базовый класс) + OnValidate auto-set
- 2 иерархии вместо 3: `ItemData` + `WeaponItemData`
- InventoryTab: слот читается из `ItemData.equipSlot` (без хардкода)
- Кросс-серверная связность через прямые вызовы (без reflection)

**Фиксы:**
- Клиентский `_itemCache` заполняется из `ItemRegistry`
- ID-коллизия: `while (_itemDatabase.ContainsKey(newId)) newId++`
- `itemName` в снапшоте DTO вместо lookup по кэшу

**Документация:** `docs/Character/Skills/ITERATIONS.md`, `docs/Character/Skills/AUDIT_2026-07-24_ITEM_WEAPON_REFACTOR.md`.

---

**Связанные документы:**

| Документ | Путь | Описание |
|----------|------|----------|
| Ship System | `gdd/GDD_10_Ship_System.md` | Корабли, модули, груз, ключи |
| Crafting System | `docs/Crafting_system/ROADMAP.md` | Крафт предметов (T-C01–T-C07c) |
| MetaRequirement | `../MetaRequirement/00_OVERVIEW.md` | Система требований для Interactable |
| Inventory v2 | `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` | Детальный дизайн инвентаря v2 |
| Combat Engine | `docs/Character/Skills/real-time-combat/` | Боевая система (WeaponItemData) |
| Equipment Visual | `docs/Character/Customisation/` | Визуал экипировки на персонаже |
| MMO Plan | `docs/MMO_Development_Plan.md` §1.6, §1.9 | Общий план |
| Lore Book | RAG БД (лоре) | Лор мира из книги |

---

*Документ создан: Апрель 2026 | Агенты: @game-designer, @lead-programmer, @unity-specialist | Дополнено Mavis 2026-06-10 (раздел X реализации), 2026-06-19 (R2-SHIP-KEY-003 §X.4), 2026-06-27 (X.5 WeaponItemData + Equipment Visual), 2026-07-14 (полная переписка §1-§12 под v2; Weapon Unification §X.7) *
