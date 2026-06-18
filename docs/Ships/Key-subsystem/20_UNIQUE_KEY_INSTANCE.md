# Unique Key Instance — Концепция уникального ключа корабля

**Подсистема:** Корабли — уникальная привязка ключа к экземпляру корабля
**Тег:** `key-instance`, `unique-binding`, `ship-ownership`
**Статус:** 📋 Дизайн готов, код НЕ написан
**Дата:** 2026-06-18
**Связанные документы:**
- `00_OVERVIEW.md` — текущая реализация (MVP, ключ = ItemData definition)
- `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — миграция на MetaRequirement
- `21_SHIP_OWNERSHIP_MODEL.md` — server-side реестр владельцев
- `22_SHIP_TELEMETRY_PLAN.md` — UI-проекция состояния корабля по ключу
- `23_ROADMAP.md` — тикеты реализации
- `docs/gdd/GDD_10_Ship_System.md` §5 — целевая модель KeyRodData

---

## 1. Проблема

### 1.1 Симптом (со слов пользователя, 2026-06-18)

> *"сейчас это делает ключи универсальным. любой корабль где сказано что нужен лайт ключ откроется любым лайт ключем. нам нужна система как по гдд - где ключ будет уникальным"*

### 1.2 Что происходит сейчас в коде

```csharp
// Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs:96-142
public bool CanPlayerUse(ulong clientId, out string reason)
{
    int[] ids = ServerItemIds;  // ← int[] itemId, не instance
    return InventoryWorld.Instance.HasAllItems(clientId, ids);
}
```

```csharp
// Assets/_Project/Items/Core/InventoryWorld.cs:184
private readonly Dictionary<ulong, InventoryData> _playerInventories;
```

`InventoryData` хранит `List<int> ids` — список `itemId` (definitions), не уникальных `instanceId`. Два ключа одного типа неразличимы.

### 1.3 Где это сломалось бы прямо сейчас

Уже сейчас в коде есть прямое предупреждение (`docs/Ships/Key-subsystem/00_OVERVIEW.md:191`):

> *"Гарантия 1:1: один keyItemData — один keyItemId. Если два корабля случайно получат один keyItemData, проверка наличия ключа сработает на обоих — это баг дизайнера."*

Это **дисциплина, которой нельзя доверять**. В мире с 200 кораблями (как описал пользователь) один неправильно скопированный SO → 2 корабля под одним ключом.

### 1.4 Что требует GDD

GDD_10 §5.4 (`docs/gdd/GDD_10_Ship_System.md:337-346`):

```csharp
public class KeyRodData : ScriptableObject {
    public string keyRodId;
    public string registeredShipId;   // ← к какому кораблю привязан
    public string ownerPlayerId;      // ← кто владеет
    public bool isDuplicate;          // ← нелегальная копия (фаза 2)
    public KeyRodAccessLevel accessLevel; // Full / Limited / OneTime (фаза 2)
}
```

Декларировано, не реализовано. `KeyRodData` — definition-уровень (как `ItemData` для обычных предметов).

---

## 2. Решение — Unique Key Instance слой

### 2.1 Концепция

**Definition ↔ Instance разделение:**

| Уровень | Сейчас | Цель (T-KEY-01) |
|---|---|---|
| **Definition** | `ItemData` (SO в Resources/Items/) | `ItemData` (SO) + поле `kind: ItemKind` (Key/Resource/...) |
| **Instance** | отсутствует | `KeyRodInstance` (POCO struct/record) |
| **Inventory slot** | `List<int> itemIds` | `List<InventorySlot> { itemId, instanceId }` |

`ItemData` остаётся общей definition ("ключ от корабля типа Light"). При крафте/спавне нового экземпляра сервер создаёт **новый `KeyRodInstance`** с уникальным `instanceId` (int, серверный counter) и привязкой `registeredShipId` (NetworkObjectId корабля).

### 2.2 Что такое KeyRodInstance

```csharp
namespace ProjectC.Ship.Key
{
    /// <summary>Server-only runtime record. Уникальный экземпляр ключа.
    /// Создаётся при крафте/спавне, удаляется при уничтожении предмета.</summary>
    public class KeyRodInstance
    {
        public int    instanceId;          // Server-unique, монотонный counter
        public int    itemId;              // → ItemData definition (LightRod/MediumRod/...)
        public ulong  registeredShipId;    // NetworkObjectId корабля-владельца
        public ulong  ownerPlayerId;       // ClientId текущего владельца (меняется при передаче)
        public ulong  originalOwnerId;     // ClientId первого владельца (для истории)
        public long   createdAtUnix;       // Для отладки/истории
        public KeyRodInstanceState state; // Active / Destroyed / Lost
    }

    public enum KeyRodInstanceState
    {
        Active = 0,
        Destroyed = 1,    // удалён из инвентаря навсегда
        Lost = 2,         // дроп на земле, не подобран
    }
}
```

**Важно**: `KeyRodInstance` — **POCO, НЕ MonoBehaviour, НЕ NetworkBehaviour**. Хранится только в `KeyRodInstanceWorld` (server singleton по типу `CraftingWorld`). Клиент получает **только DTO** через RPC (см. `22_SHIP_TELEMETRY_PLAN.md`).

### 2.3 Что меняется в InventoryWorld

**Расширение `InventoryData`** (минимально-инвазивно, additive-only):

```csharp
// Сейчас: List<int> ids (по типу)
// Цель:  List<InventorySlot> slots (по типу), где каждый slot = (itemId, instanceId)

public struct InventorySlot
{
    public int itemId;        // = ItemData._itemDatabase key
    public int instanceId;    // = 0 для non-instance items (Resources, Food), >0 для KeyRods
}

public class InventoryData
{
    private Dictionary<ItemType, List<InventorySlot>> _slotsByType;
    // вместо Dictionary<ItemType, List<int>> _idsByType

    public void AddItem(ItemType type, int itemId, int instanceId = 0) { ... }
    public bool RemoveItem(ItemType type, int itemId, int instanceId = -1) { ... }
    public List<InventorySlot> GetSlotsForType(ItemType type) { ... }
    // + обратная совместимость: GetIdsForType(type) → List<int> (itemId из slot)
}
```

**Backward compat**: существующие методы (`HasItem`, `HasAllItems`, `CountOf`) принимают `int itemId` — они **НЕ трогаются**. Внутри переводятся на новый слот через `GetIdsForType()`.

**Новые методы** (для KeyRod-специфики):

```csharp
public bool HasKeyForShip(ulong clientId, ulong shipNetworkObjectId)
{
    // Пройти по всем слотам Equipment-типа, найти instanceId → KeyRodInstanceWorld
    // → проверить registeredShipId == shipNetworkObjectId
}

public int[] GetKeyInstanceIds(ulong clientId)
{
    // Вернуть все instanceId из Equipment-слотов, которые являются KeyRodInstance
}

public List<(int instanceId, ulong shipId)> GetMyShips(ulong clientId)
{
    // Пары (instanceId, registeredShipId) для всех KeyRodInstance в инвентаре клиента
}
```

### 2.4 Создание instance — где и когда

| Событие | Что создаёт instance | Ticket |
|---|---|---|
| **`[KeyRodInstanceBinding]` на pickup (sceneplaced)** | Явный компонент указывает `shipNetId` привязки. Сервер создаёт instance в `OnNetworkSpawn` | T-KEY-04 |
| **Crafting (фаза 2)** | `CraftingWorld.CompleteCraft()` → создаёт instance с привязкой к свежесозданному кораблю | T-KEY-CRAFT (не входит в MVP) |
| **NPC trade (фаза 2)** | Получает готовый instance от NPC vendor | T-KEY-NPC (не входит в MVP) |
| **Drop → Pickup другого игрока** | `instanceId` сохраняется, меняется только `ownerPlayerId` | T-KEY-05 |

**MVP сценарий** (Q11, 2026-06-18): на каждом `[KeyRod_*]` PickupItem в сцене лежит **явный компонент `KeyRodInstanceBinding`** с полем `_shipNetId` (или ссылкой на GameObject корабля). Никакого auto-detect через `FindNearestShip` — Q11 user feedback: *"вообще не понял что это и про что"* → explicit binding component (как сейчас `ShipKeyBinding` на кораблях) более понятен.

### 2.5 Persist между сессиями (Q12, 2026-06-18)

**ДА, persist**. `KeyRodInstance` сохраняется через `IPlayerDataRepository` (по аналогии с `InventoryWorld._repository`, см. `Assets/_Project/Items/Core/InventoryWorld.cs:104-125`).

**Что сохраняется**:
- `instanceId` (server counter — НЕ сохраняется, пересоздаётся)
- `itemId` (definition)
- `registeredShipId` (NetworkObjectId корабля — может измениться между сессиями, см. edge-cases)
- `ownerPlayerId` (original + current)
- `state`
- `createdAtUnix`

**Файл**: `Assets/_Project/Resources/KeyRodInstances/{clientId}.json` (по аналогии с CharacterSaveData).

**Поведение при server restart**:
1. Server стартует → `KeyRodInstanceWorld.CreateAndInitialize(repository)`
2. Bootstrap проходит по всем `[KeyRodInstanceBinding]` в сцене.
3. Для каждого: загружает instance из репозитория по `ownerPlayerId` + `registeredShipId` (если есть).
4. Если instance есть в репо → восстанавливает (с актуальным NetworkObjectId корабля).
5. Если нет → создаёт новый (default state=Active, owner=0).

**T-KEY-PERSIST** (новый ticket, ~1.5h): `KeyRodInstanceRepository` + `KeyRodInstanceSave` + интеграция в T-KEY-01.

### 2.6 Что НЕ входит в MVP

- ❌ **Крафт ключей на верфи** — фаза 2 (T-KEY-CRAFT)
- ❌ **`isDuplicate` поле** — фаза 2 (заметка в дизайне, не в коде)
- ❌ **`KeyRodAccessLevel` (Limited/OneTime)** — фаза 2 (сейчас всегда Full)
- ❌ **NPC-продажа ключей** — фаза 2
- ❌ **Угон** — фаза 2
- ❌ **`keyRodId` string в definition** — GDD использует string, мы используем int `instanceId` (быстрее, совместимо с `NetworkVariable<int>`)

---

## 3. Совместимость с существующим кодом

### 3.1 MetaRequirement

**MetaRequirement остаётся почти без изменений** для блоков/дверей/NPC. Для кораблей добавляется **новый компонент** `ShipOwnershipRequirement` (см. `21_SHIP_OWNERSHIP_MODEL.md`), который работает по `instanceId`, а не по `itemId`.

```
MetaRequirement (existing)        ShipOwnershipRequirement (new)
├── _requiredItems: ItemData[]    ├── _registeredShipId: ulong (или _allowedInstanceIds)
├── Logic: All/Any/AtLeastN       └── (no items, проверяет instance владения)
└── Used by LockBox, etc.          Used by ships (через MetaRequirementRegistry как wrapper)
```

`MetaRequirementRegistry` остаётся общим хабом, но `CanPlayerUse` сначала проверяет `ShipOwnershipRequirement.IsOwner`, и только если false — fallback на старую MetaRequirement-логику (для блоков/дверей).

### 3.2 Crafting (фаза 2, не сейчас)

`CraftingWorld.CompleteCraft` сейчас выдаёт `AddItemDirect(clientId, itemId, itemType)`. В фазе 2 это изменится на `AddItemDirect(clientId, itemId, instanceId, itemType)`. **Сейчас не трогаем** — T-KEY-02 заложит API, но реальный крафт instance'ов появится в T-KEY-CRAFT.

### 3.3 InventoryClientState / InventoryServer

Минимальные изменения:
- `InventoryItemDto` + поле `instanceId` (int, default 0 для non-instance items).
- `InventorySnapshotDto` остаётся массивом `InventoryItemDto[]`, клиент читает `instanceId` где есть.
- `InventoryClientState.OnSnapshotReceived` event не меняется.

### 3.4 WorldScene_0_0 (тестовая сцена)

Сейчас в сцене есть 3 `[KeyRod_*]` PickupItem + 3 корабля с `ShipKeyBinding`. **T-KEY-04** добавит на каждый `[KeyRod_*]` GameObject явный компонент `[KeyRodInstanceBinding]` с полем `_ship` (drag-and-drop ShipController в инспекторе). Никакого auto-detect через `FindNearestShip` (см. Q11). **Additive-only** — никаких удалений существующих объектов.

---

## 4. Точки вставки в существующий код

| Файл | Что меняем | Зачем |
|---|---|---|
| `Assets/_Project/Items/Core/InventoryWorld.cs` | + `KeyRodInstanceWorld.Instance`, + extension `HasKeyForShip/GetKeyInstanceIds/GetMyShips` | Серверный helper для KeyRod |
| `Assets/_Project/Items/Dto/InventoryItemDto.cs` | + поле `int instanceId` | Передача instance-id в snapshot |
| `Assets/_Project/Items/Network/InventoryServer.cs` | + `AddItemDirect(clientId, itemId, instanceId, itemType)` | Создание item с instance |
| `Assets/_Project/Items/Core/InventoryData.cs` | + `List<InventorySlot>` параллельно или вместо `List<int>` | Хранение instance |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` | Без изменений | Для блоков/дверей |
| **Новый** `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` | POCO struct | Runtime record |
| **Новый** `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` | Server-only static facade (по типу `CraftingWorld`) | Реестр всех instances |
| **Новый** `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceBinding.cs` | MonoBehaviour на `[KeyRod_*]` PickupItem | Явная привязка pickup → ship (Q11) |
| **Новый** `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` | `IPlayerDataRepository` имплементация | Persist (Q12, T-KEY-PERSIST) |
| **Новый** `Assets/_Project/Scripts/Ship/Key/ShipOwnershipRequirement.cs` | NetworkBehaviour (по типу `MetaRequirement`) | Проверка владения кораблём |
| **Новый** `Assets/_Project/Scripts/Ship/Network/ShipOwnershipRegistry.cs` | NetworkBehaviour, NetworkList<OwnershipEntry> | Реестр ownership → синхронизация клиентам |
| **Новый** `Assets/_Project/Scripts/Ship/Client/ShipTelemetryClientState.cs` | Singleton, агрегатор NetworkVariable | UI/HUD-проекция (Q4) |
| **Расширение** `Assets/_Project/Scripts/Player/ShipController.cs` | + `NetworkVariable<ShipTelemetryState>`, + `_customDisplayName`, + read-only API | Telemetry + displayName (Q6) |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs` | Без изменений (оставлен как legacy alias) | Backward compat |
| `WorldScene_0_0.unity` | + `[KeyRodInstanceBinding]` на каждом `[KeyRod_*]` PickupItem | Явная привязка (Q11) |

**Что НЕ меняем** (по AGENTS.md «Don't touch»):
- `docs/gdd/`, `docs/WORLD_LORE_BOOK.md` — дизайн-документ держим в `docs/Ships/Key-subsystem/`.
- `Library/`, `Temp/`, `Builds/`, `ProjectSettings/`, `Packages/manifest.json` — без надобности.
- `.meta` и `.asmdef` — НЕ создаём новые.

---

## 5. Сценарии использования

### 5.1 Игрок владеет ключом → видит свой корабль в UI (NetworkVariable-based, Q4)

```
1. Игрок подобрал [KeyRod_ShipLight] → instanceId=42 создан, ownerPlayerId=0
2. ShipController (server) пишет в NetworkVariable<ShipTelemetryState> каждые 0.2s
3. NGO автоматически синхронизирует state клиенту
4. ShipOwnershipRegistry (NetworkList<OwnershipEntry>) обновляется при transfer
5. ShipTelemetryClientState.OnShipTelemetryChanged / OnOwnershipListUpdated → события
6. Игрок открывает TAB-колесо → "Мои корабли" tab (5-й в CharacterWindow, Q5)
7. UI читает ShipTelemetryClientState.MyShips (уже синхронизировано)
8. (HUD на актуальных данных — без ручного refresh)
```

### 5.2 Игрок передаёт ключ другому

```
1. Игрок A: Drop key (slotIndex с instanceId=42) → InventoryServer.TryDrop(slot)
2. InventoryWorld: удаляет slot, спавнит PickupItem в мире с instanceId=42
3. Игрок B: Pickup → TryPickup → InventoryWorld добавляет slot (itemId, instanceId=42)
4. KeyRodInstanceWorld.TransferInstance(42, fromClient=A, toClient=B) → ownerPlayerId=B
5. ShipOwnershipRequirement: теперь IsOwner(client=B, ship) = true
6. Игрок B может сесть в корабль (через ShipKeyBinding legacy alias ИЛИ ShipOwnershipRequirement)
```

### 5.3 Игрок A сел в корабль, ключ у B → доступ запрещён

```
1. Игрок A подходит к Ship_Light, у A instance=42 НЕТ
2. F → RequestCanUseRpc(shipNetId) → MetaRequirementRegistry
3. ShipOwnershipRequirement.CanPlayerUse(A, shipNetId) → IsOwner(A) = false → denied
4. MetaRequirementClientState.OnAccessDenied(reason)
5. Toast "Нет ключа корабля (Корабль Light)"
```

---

## 6. Edge-cases

| Кейс | Решение |
|---|---|
| **Два ключа одного типа в инвентаре** | `GetMyShips()` вернёт обе привязки; `HasKeyForShip(ship)` сматчит любую (не важно, какую) |
| **Ключ утерян (drop в недосягаемом месте)** | `KeyRodInstance.state = Lost`, можно вернуть через salvage-механику (фаза 2) |
| **Корабль уничтожен (крушение, фаза 5)** | `KeyRodInstance.state = Destroyed`, ключ превращается в мусор (TODO) |
| **Корабль спавнится повторно (server restart)** | `registeredShipId` пересоздаётся, **старые instance'ы становятся orphaned** (TODO: salvage/rebind) |
| **Чит: подмена instanceId в RPC** | Серверная валидация: любой `TransferInstance` идёт только через `InventoryServer.TryDrop` / `TryPickup`, клиент не выбирает instanceId руками |
| **NetworkObjectId корабля меняется между запусками** | T-KEY-04: `[KeyRodInstanceBinding]` хранит **ссылку на GameObject** корабля (resolved в `OnNetworkSpawn` через `GetComponent<NetworkObject>().NetworkObjectId`). Persistence: `registeredShipId` сохраняется, при restore — lookup по `(ownerPlayerId, itemId)` если NetID mismatch, новый instance |

---

## 7. Ссылки

- `00_OVERVIEW.md` — текущая реализация (MVP)
- `21_SHIP_OWNERSHIP_MODEL.md` — server-side ownership
- `22_SHIP_TELEMETRY_PLAN.md` — UI-проекция состояния корабля по ключу
- `23_ROADMAP.md` — тикеты T-KEY-01..T-KEY-08
- `24_OPEN_QUESTIONS.md` — открытые вопросы
- `docs/gdd/GDD_10_Ship_System.md` §5 — KeyRodData
- `docs/gdd/GDD_10_Ship_System.md` §13.1 — реализация ShipKey в коде
- `docs/gdd/GDD_10_Ship_System.md` §13.2 — MetaRequirement v1
- `unity-mcp-orchestrator` skill — pitfalls #27 (MetaRequirement-first), #31 (scene-spawn)
- `project-c-netcode-patterns` skill — §26 (scene-placed NetworkObject lifecycle)

---

**Обновлено:** 2026-06-18 — первичный дизайн unique key instance.
**Обновлено:** 2026-06-18 — Q11: explicit `KeyRodInstanceBinding` вместо auto-bootstrap. Q12: persist через `IPlayerDataRepository`. Q4: NetworkVariable-based telemetry (cross-ref §5.1).