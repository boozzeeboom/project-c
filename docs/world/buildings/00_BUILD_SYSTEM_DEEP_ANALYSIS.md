# Строительство в мире — глубокий анализ подсистем

> **Дата:** 2026-07-04
> **Тип документа:** глубокий анализ — что у нас есть для мира, что не хватает, что **не нужно** строить
> **Контекст:** гипотеза — дать игроку возможность **строить в мире** (не только на корабле): постройка дома, расстановка placeable предметов (стол, стул, фонарь), поднятие/перенос физических объектов, расстановка.
> **Читать вместе с:** `../../Ships/customisation/buildings/00_BUILD_SYSTEM_DEEP_ANALYSIS.md` (билд на корабле — L6), `../../Ships/customisation/00_SUMMARY.md` (общая сводка кастомизации)

---

## TL;DR — короткий ответ

**У нас уже есть 70% нужной инфраструктуры.** Мировое строительство — это **L7 (новый уровень, шире L6 корабля)**:

```
ResourceNode (mining) → ItemData → Inventory → Crafting (recipe)
       ↓
BuildableItem (новая категория: placeable world furniture) → Inventory
       ↓
Player picks up / places / rotates / stacks в designated Build Zone
       ↓
Server RPC → BuildWorld (singleton) → NetworkList → replicated state
       ↓
BuildVisualApplier спавнит physical prefab на всех клиентах
       ↓
JsonBuildWorldRepository (per (worldZone, owner)) → persistence
```

**Но.** Мировое строительство **на порядок сложнее** корабле-строительства, потому что добавляются:
- **Физика** — предметы падают, сталкиваются, могут толкаться
- **Зонирование** — где можно строить, а где нет (земля? в воздухе? на чужой платформе?)
- **Подбор/перенос** — игрок может взять в руки, поставить, передать другому
- **Multi-player construction** — два игрока строят рядом

**Главный архитектурный вопрос ДО кода:** это **player expression** (Fortnite Creative, No Man's Sky base building, Valheim) или **functional placement** (ARK, Rust, Eco)?

| Вариант | Трудоёмкость | Когда выбирать |
|---|---|---|
| **A. Placeable furniture** (столы, стулья, фонари) — **только в designated Build Zone** | **4-6 нед** | ✅ По умолчанию — sandbox выражение |
| **B. Строительство домов** (foundation + walls + roof grid) | **12-20 нед** | Если хотите "second gameplay loop" |
| **C. Full sandbox** (No Man's Sky, Space Engineers) | 6+ мес | ❌ Не для stage 2.5 |

**Моя рекомендация:** **вариант A** (placeable furniture в зонах) + мост к **варианту B** через общую систему snap'а. Корабле-строительство (L6) и мировое (L7) используют **один и тот же `BuildWorld`**, `BuildGhostApplier`, `SnapResolver` — только разные `BuildableItem` SO.

---

## 1. Что у нас уже есть (по подсистемам мира)

Полная инвентаризация релевантной инфраструктуры для мирового строительства.

### 1.1 Subsystem inventory

| Подсистема | Файлы | LOC | Что даёт для "строительства в мире" |
|---|---|---|---|
| **PickupItem (физический подбираемый предмет)** | `Assets/_Project/Scripts/Core/PickupItem.cs` | ~270 | Бобаинг, trigger, drop from inventory, visualPrefab support. **Основа для placeable item.** |
| **PickupDeckRide (carry на палубе)** | `Assets/_Project/Scripts/Core/PickupDeckRide.cs` | ~100 | Локальный carry физического предмета на движущейся палубе. **Прямой refference для "carry в руках"!** |
| **LootTable (рандомный дроп)** | `Assets/_Project/Scripts/Core/LootTable.cs` | ~60 | Рандом-таблица для сундуков. **Не нужно для buildable**, но полезно для дропа с ресурсов. |
| **Inventory (per-client state)** | `Assets/_Project/Items/Core/InventoryWorld.cs`, `Items/Network/InventoryServer.cs`, `Items/Client/InventoryClientState.cs` | ~1500 | Хранение placeable items в инвентаре игрока |
| **`RequestDropRpc` (drop item в мир)** | `InventoryServer.cs:115-160` | ~50 | Серверный spawn `PickupItem` при дропе из инвентаря |
| **Crafting (рецепты + станции)** | `Assets/_Project/Scripts/Crafting/CraftingServer.cs`, `CraftingWorld.cs`, `RecipeData.cs`, `CraftingStation.cs` | ~1500 | Рецепт "собрать стол из 4 досок" |
| **ResourceNode (mining узлы)** | `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs` + `ResourceNodeConfig` | ~1500 | Узлы в мире: gathering, cooldown, drops. **Не для buildable, но показывает паттерн "world object с состоянием".** |
| **NetworkChestContainer** | `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs` | ~300 | NetworkBehaviour с инвентарём, persisted. **Образец для мира с состоянием.** |
| **NPC visual config + spawner** | `Assets/_Project/Scripts/Npc/NpcVisualConfig.cs`, `AI/NpcSpawner.cs` | ~600 | Паттерн "spawn from prefab + visualConfig + cleanup" |
| **Customisation (client-side persistence)** | `Assets/_Project/Scripts/Customisation/CustomisationClientState.cs` | ~250 | Локальный JSON persistence по `clientId` |
| **Docking (зоны + trigger boxes)** | `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`, `Stations/DockingPadTriggerBox.cs` | ~1200 | **Прямой refference для Build Zone** |
| **NPC-ship (FSM + scheduled placement)** | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | ~1000 | "Объект в мире с persistent state" — пример как сделать NPC-постройку |
| **MetaRequirement (gate)** | `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` | ~600 | "Можно ли строить здесь?" — проверка зоны, владельца, ключа |

**~7000 LOC готовой инфраструктуры**, релевантной для мирового строительства.

### 1.2 Какие паттерны можно прямо копировать

| Что | Где взять | Что строить |
|---|---|---|
| **Per-client persistence** | `InventoryWorld` (JSON repo) | Хранение placeable items в инвентаре игрока |
| **Replicated state + RPC** | `ShipModuleServer.RequestInstallModuleRpc` | `BuildRequestPlaceRpc(itemId, worldPos, rotation)` |
| **Singleton state on server** | `KeyRodInstanceWorld` (static, server-only) | `BuildWorld` — реестр "что стоит в какой зоне" |
| **Trigger zone** | `OuterCommZone` + `DockingPadTriggerBox` | `BuildZone` — где можно строить |
| **Physical item with state** | `PickupItem` + `NetworkChestContainer` | `BuildableInstance` (Physical world object) |
| **Client-side UI singleton** | `CustomisationClientState` | `BuildClientState` (режим строительства) |
| **NPC FSM** | `NpcBrain` (state-машина с сервера) | FSM для Build Zone (активна/деактивирована) |
| **Static event broadcast** | `ShipModuleServer.OnModuleChanged` | `BuildWorld.OnBuildChanged(zoneId)` |
| **Per-object persisted state** | `JsonKeyRodInstanceRepository` | `JsonBuildWorldRepository` (по zoneId) |
| **Marker pattern** | `ShipRootReference` + `ShipComponentLocator` | `BuildSlotReference` (точка крепления в зоне) |
| **Carry on moving platform** | `PickupDeckRide` | **Прямой refference для "carry в руках"** |

**Прямых аналогов "free placement в мире с snap + physical pickup + place" у нас нет.** Это нужно строить, но ~50% — копипаста существующих паттернов.

---

## 2. Анализ требований — что игрок должен мочь

### 2.1 Что сказал пользователь

> "билд дать не только на корабле а в отведенных зонах... постройка дома, плейсбл предметы... расстановка поднятие перенос и т.д."

**Разбираем:**

| Слово | Что значит в контексте | Что это значит для архитектуры |
|---|---|---|
| "не только на корабле" | Расширяем L6 с корабля на мир | Один общий `BuildWorld` для обеих доменов |
| "в отведенных зонах" | Игрок НЕ может строить везде — есть designated Build Zone | `BuildZone` NetworkBehaviour + UI подсказка "press B to build" |
| "постройка дома" | Возможность собрать структуру (стены + крыша) | Higher-level abstraction над placeable parts (отдельный тикет, см. §3.1) |
| "плейсбл предметы" | Атомарные items, которые можно поставить (стол, стул, фонарь, сундук) | `BuildableItem` SO с типом, размером, визуалом |
| "расстановка" | Свободное размещение в зоне | Placement mode + ghost preview + snap |
| "поднятие" | Взять placeable item с земли и нести | Carry mode (см. §6.2 — как PickupDeckRide) |
| "перенос" | Игрок может переставить поставленный предмет | `BuildRequestMoveRpc(elementId, newPos)` |
| "и т.д." | Подразумеваем: rotate, stack, take down, передать другому | Расширения — см. §10 |

### 2.2 Что **НЕ** входит в MVP (явно за рамками)

| Что | Почему не входит |
|---|---|
| **Физика для NPC** | Построенный дом не должен мешать NPC — collider optional |
| **Электричество / трубы** | Valheim-style — не для stage 2.5 |
| **Терраформинг** | Деформация terrain — отдельная гигантская подсистема |
| **Движущиеся части построек** (двери, лестницы) | Атомарные items без runtime-логики (отдельные модули) |
| **Build undo/redo** | `Ctrl+Z` — отдельная фича |
| **Permissions (allowlist друзей)** | Multi-crew build — см. `Ships/customisation/buildings/00` §6.4 |
| **Damage / destruction** | Строение не ломается (нет боёвки со зданиями) |
| **Build cooldowns** | Нет "вы собирали 5 минут назад, теперь cooldown 10 минут" |
| **Сложные рецепты** (multi-step, station required) | Один рецепт → один item |

### 2.3 Что входит в MVP

| Фича | Описание |
|---|---|
| **Placeable items** | Атомарные предметы: стол, стул, сундук, фонарь, кровать |
| **Designated Build Zones** | NetworkBehaviour в сцене (например, "Platform_Build_01") |
| **Pickup / carry** | Игрок берёт в руки через F (как PickupItem) |
| **Place / rotate** | Build mode: ghost preview, click to place, R to rotate |
| **Persistence** | JSON по zoneId (через `JsonBuildWorldRepository`) |
| **Multi-player visibility** | Replicated через NGO — другие видят |
| **Owner-only modify** | Только owner зоны может строить/сносить (по канону `ShipOwnershipRequirement`) |
| **Remove** | В build mode → click на свой элемент → "Удалить" |
| **Salvage** | При remove → item возвращается в инвентарь (50% материалов) |

---

## 3. Главное архитектурное решение — что мы строим

### 3.1 Три варианта продуктового позиционирования

#### Вариант A — "Placeable Furniture" (Sea of Thieves, Valheim мебель)

```
Свободная расстановка placeable items:
- Игрок крафтит/покупает "стол", "стул", "фонарь", "сундук"
- Подходит к Build Zone (платформа в городе)
- Press B → build mode → ghost preview следует за курсором
- R = rotate, click = place
- Можно переставить (build mode → click на existing → drag)
- Можно поднять (build mode → click на existing → "Поднять" → идёт в инвентарь)

Use case: "хочу обустроить свою платформу — поставить стол, фонарь, сундук"
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Простая логика (item-like placement) | Нет "постройки дома" |
| Высокий player value | Нужно много контента |
| Social (показать друзьям обстановку) | Не меняет gameplay loop |
| Reuse PickupItem + BuildVisualApplier | — |

**Трудоёмкость: 4-6 нед.**

#### Вариант B — "Base Building" (Valheim, Rust, Conan Exiles)

```
Структурное строительство:
- Игрок крафтит "foundation", "wall", "roof", "door frame", "window"
- Размещает на земле (или на floating platform в нашем случае)
- Snap к grid-сетке
- Foundation + walls + roof = готовый дом
- Может содержать furniture внутри
- Crafting stations внутри дают бонусы
- Permissions (кто может войти)

Use case: "хочу построить дом в небесах"
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Big gameplay loop (player progression) | Сложная логика (snap-to-grid, foundation/wall/door placement) |
| Долгосрочный retention | Нужно пересмотреть физику мира |
| Strong social | 12-20 нед |

**Трудоёмкость: 12-20 нед.** ⏳ Вне stage 2.5.

#### Вариант C — "Full Sandbox" (No Man's Sky, Space Engineers)

```
Полная свобода:
- Free placement любых предметов в любой зоне
- Custom terrain deformation
- Logic gates / circuits (Fortnite Creative)
- Multi-crew roles (engineer, builder, programmer)

Use case: "хочу построить небесную базу с добычей и крафтом"
```

**Трудоёмкость: 6+ мес.** ❌ Не для stage 2.5.

### 3.2 Рекомендация: вариант A

**Почему:**

1. **Пользователь сказал "плейсбл предметы"** — это чётко вариант A. B и C подразумевают grid/foundation systems.
2. **Переиспользует 80% L6 (корабле-строительства)** — `BuildWorld`, `BuildVisualApplier`, `SnapResolver` общие.
3. **Строительство дома (B) — это будущее расширение**: добавляем `BuildableItem.type = Foundation/Wall/Roof` → snap-to-grid → та же инфраструктура.
4. **Минимальные изменения в физике**: placeable items имеют **физику** (Rigidbody), но **не interactable** для других (как PickupItem).
5. **Контент дешёвый**: один художник может сделать 20-30 моделей placeable items за неделю.

**Что это НЕ исключает:** вариант B в будущем. L7 → L7+ (base building). L7 = placeable, L7+ = structural.

---

## 4. Архитектура системы (вариант A — placeable furniture в зонах)

### 4.1 Высокоуровневая схема

```
┌─────────────────────────────────────────────────────────────────────┐
│                  WORLD BUILD SYSTEM (L7)                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  [Игрок]                          [Сервер]                          │
│     │                                  │                            │
│  1. Крафтит "стол"               ┌────▼─────────────────┐          │
│     через CraftingStation        │ CraftingServer        │          │
│     → BuildableItem              │ + RecipeData (ext)    │          │
│                                  └────┬─────────────────┘          │
│                                       │                            │
│  2. Подходит к Build Zone        ┌────▼─────────────────┐          │
│     (платформа в городе)         │ BuildZone trigger     │          │
│     → Press B                    │ (radius 5m)           │          │
│     → InteractableManager        └────┬─────────────────┘          │
│                                       │                            │
│  3. Enter Build Mode             ┌────▼─────────────────┐          │
│     → BuildClientState           │ BuildClientState      │          │
│     показывает доступные         │ + BuildGhostApplier   │          │
│     "стол/стул/фонарь"           │ (3D ghost preview)    │          │
│     из инвентаря                 └──────────────────────┘          │
│                                                                     │
│  4. Поднимает existing item      (опц.)                             │
│     → click на placed item       │                                  │
│     → "Поднять" → PickupItem     │                                  │
│       спавнится в инвентаре      │                                  │
│                                  │                                  │
│  5. Тащит ghost preview              [Server]                     │
│     → snap to: ground, existing  ┌────▼─────────────────┐          │
│       element, zone socket,      │ BuildWorld (singleton)│        │
│       free position              │ state per-zone        │         │
│     → R = rotate                 └────┬─────────────────┘          │
│     → click = confirm                │                            │
│                                       │                            │
│  6. Confirm → RPC               ┌──────▼─────────────────┐         │
│     RequestPlaceRpc(             │ WorldBuildServer       │        │
│     itemId, position,            │ (NetworkBehaviour?)    │        │
│     rotation)                    │ или Scene-placed       │        │
│     → server validates           │ NetworkObject on zone  │        │
│     → adds to replicated         └──────┬─────────────────┘         │
│     → ClientRpc broadcasts             │                          │
│                                          │                        │
│  7. All clients see new build       ┌────▼─────────────────┐       │
│     → BuildVisualApplier spawns     │ BuildVisualApplier    │       │
│       Rigidbody+visualPrefab        │ (per-zone, on root)   │       │
│       at world position             └──────────────────────┘       │
│                                                                     │
│  8. Persistence                  ┌──────────────────────────┐       │
│     → JSON file                  │ JsonBuildWorldRepository │       │
│     → per (zoneId)               │ (по zoneId)              │       │
│                                  └──────────────────────────┘       │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.2 Различия с L6 (корабле-строительство)

| Аспект | L6 (корабль) | L7 (мир) |
|---|---|---|
| **Куда parent'ить** | `slot.transform` (child GO корабля) | Мировые координаты (без parent) или parent к `BuildZone.transform` |
| **Физика** | Нет (visual only, по §7 L6) | **ДА — Rigidbody** (предмет стоит, не проваливается) |
| **Carry** | Нет | **ДА — PickupItem-style** (поднял, понёс, поставил) |
| **Snap targets** | HullSocket, BuildElement, ShipRoot | Ground, BuildZone socket, BuildElement, Free |
| **Зонирование** | Только docked | Только в `BuildZone` |
| **Owner** | KeyRodInstance (per-ship) | BuildZone.ownerClientId |
| **Persistence** | Per shipInstanceId | Per zoneId |
| **Visuals** | Следует за движением корабля | Стоит на месте |

**Главное:** **общий `BuildWorld` и `BuildableItem`**, разные `BuildVisualApplier`/`BuildGhostApplier`/`SnapResolver`.

### 4.3 Компоненты (что нужно создать)

| Компонент | Файл | LOC | Назначение |
|---|---|---|---|
| **`BuildableItem` SO** | `Assets/_Project/Scripts/Build/BuildableItem.cs` | ~150 | Данные: type, size, visualPrefab, allowedSnapTargets, rotationSteps, salvageMultiplier |
| **`BuildItemRegistry` SO** | `Assets/_Project/Scripts/Build/BuildItemRegistry.cs` | ~100 | Список всех доступных buildable items |
| **`BuildRecipeData` extension** | расширение `RecipeData` | ~50 | Рецепты для крафта placeable items |
| **`BuildClientState`** | `Assets/_Project/Scripts/Build/BuildClientState.cs` | ~200 | Singleton: build mode state, snapshot per-zone |
| **`BuildGhostApplier`** | `Assets/_Project/Scripts/Build/BuildGhostApplier.cs` | ~250 | Ghost preview + snap highlight |
| **`BuildZone`** | `Assets/_Project/Scripts/Build/BuildZone.cs` | ~150 | NetworkBehaviour в сцене: триггер зоны, owner, capacity |
| **`WorldBuildServer`** | `Assets/_Project/Scripts/Build/WorldBuildServer.cs` | ~350 | RPC place/remove/move, server validation |
| **`BuildWorld`** | `Assets/_Project/Scripts/Build/BuildWorld.cs` | ~300 | Server-only singleton: state per-zone |
| **`BuildVisualApplier`** | `Assets/_Project/Scripts/Build/BuildVisualApplier.cs` | ~250 | Spawn/destroy physical prefab (Rigidbody+visualPrefab) |
| **`BuildSnapResolver`** | `Assets/_Project/Scripts/Build/SnapResolver.cs` | ~350 | Snap к ground, existing element, zone socket |
| **`JsonBuildWorldRepository`** | `Assets/_Project/Scripts/Build/JsonBuildWorldRepository.cs` | ~200 | JSON persistence по zoneId |
| **`BuildModeWindow`** | `Assets/_Project/Ship/UI/Build/BuildModeWindow.cs` | ~400 | UI Toolkit окно |
| **`CarryController`** | `Assets/_Project/Scripts/Player/CarryController.cs` | ~250 | "Игрок несёт предмет" — по канону `PickupDeckRide` |

**Итого: ~13 новых файлов, ~3200 LOC.** ~50% — копипаста L6.

### 4.4 Состояние на сервере (replicated)

**Структура данных** (отдельная от L6, но похожая):

```csharp
public struct WorldBuildElementDto : INetworkSerializable
{
    public int elementInstanceId;   // Уникальный ID (для клиента UI / save)
    public int buildItemId;         // ID в BuildItemRegistry (какой предмет)
    public ulong ownerClientId;     // Кто поставил (для salvage permissions)
    public Vector3 worldPosition;   // Мировая позиция
    public Quaternion rotation;
    public byte snapFace;           // 0-5: +X/-X/+Y/-Y/+Z/-Z
    public ulong parentElementId;   // 0 = placed on ground/zone; иначе = parent element (snap)
}

public struct ZoneBuildSnapshot : INetworkSerializable
{
    public ulong zoneNetId;                  // NetworkObjectId BuildZone
    public ulong ownerClientId;              // Кто владеет зоной
    public WorldBuildElementDto[] elements;  // Все элементы в зоне
}
```

**Хранение:** `NetworkList<WorldBuildElement>` на `WorldBuildServer` (server-authoritative).

**Persistence:** `JsonBuildWorldRepository` хранит `Dictionary<ulong /*zoneNetId*/, List<WorldBuildElement>>`.

**Caveat:** `zoneNetId` — эфемерный. После restart он пересоздаётся. Persistence должен использовать **`BuildZone.zoneId`** (стабильный string ID, как `DockStationDefinition.StationId`).

### 4.5 Состояние на клиенте (проекция)

```csharp
public class BuildClientState : MonoBehaviour
{
    public static BuildClientState Instance { get; private set; }

    // Текущая build mode state (input)
    public bool IsBuildMode { get; private set; }
    public string CurrentZoneId { get; private set; }
    public int SelectedItemId { get; private set; }
    public Vector3 CursorWorldPosition { get; private set; }
    public ulong SnapTargetId { get; private set; }
    public WorldBuildElement HoveredElement { get; private set; } // для поднятия

    // Кэш для UI
    public IReadOnlyDictionary<string /*zoneId*/, ZoneBuildSnapshot> ZoneSnapshots { get; }

    // Events
    public event Action OnBuildModeChanged;
    public event Action<string /*zoneId*/> OnZoneBuildChanged;

    // API
    public void EnterBuildMode(string zoneId);
    public void ExitBuildMode();
    public void RequestPlace(int itemId, Vector3 position, Quaternion rotation);
    public void RequestPickup(int elementInstanceId);  // поднять existing
    public void RequestRemove(int elementInstanceId);
    public void RequestMove(int elementInstanceId, Vector3 newPos);
}
```

---

## 5. Система подъёма/переноса (CarryController)

### 5.1 Зачем это нужно

Если предметы можно **только ставить**, это не sandbox — это decoration. Настоящая мировая стройка требует:
- Игрок может поднять поставленный предмет (click → "Поднять" → идёт в инвентарь).
- Игрок может поставить предмет из инвентаря в мире (place).
- Игрок может переставить поставленный предмет (click → drag → drop).

Это превращает placeable items в **полноценные физические предметы**, как `PickupItem`, но с дополнительным состоянием "placed".

### 5.2 Два подхода к подъёму

#### Подход 1 — Place → Pickup (рекомендую)

```
Place: BuildableInstance (Rigidbody, placed) в мире
       ↓ click on it
BuildClientState.RequestPickup(elementInstanceId)
       ↓
Server: validate owner → remove from BuildWorld → AddItemToInventory
       ↓
Client: place visual disappears → Inventory UI updates
```

**Плюсы:** просто, использует существующий `InventoryServer.AddItem`.

#### Подход 2 — Carry в руках (как в Valheim)

```
Place: BuildableInstance (Rigidbody, placed)
       ↓ click on it + hold F
NetworkPlayer.AttachToHand(buildableInstance)
       ↓
BuildableInstance.parent = player.handBone
       ↓ player walks
BuildableInstance едет с игроком (как PickupDeckRide carry)
       ↓ player presses F again or clicks ground
NetworkPlayer.DetachFromHand() → place at ground
```

**Плюсы:** более immersive, как Valheim.
**Минусы:** сложный (carry через parent swap, физика отключена в carry, новые input events).

### 5.3 Рекомендация: Подход 1 для MVP

**MVP:** подъём через инвентарь. `BuildableInstance` disappear → item in inventory → place from inventory.

**Phase 2 (по запросу):** подход 2 (carry в руках) — добавляет immersive feel, но **большой** effort.

### 5.4 `CarryController` — для будущего (Phase 2)

Если позже захотим подход 2 — **прямой refference**: `PickupDeckRide.cs` (T-PICKUP-RIDE-01, уже сделано). Этот компонент умеет:
- Parent физического предмета к движущейся плалубе.
- Carry в local space (следует за движением parent).
- Возврат в world position при выходе с палубы.

Для carry в руках — расширяем `PickupDeckRide` → `PickupCarryController`:
- `parent = NetworkPlayer.handBone` (или `_carryAnchor` на root персонажа).
- Те же принципы: carry в local space, отключение физики, восстановление.

**Но.** Это Phase 2. В MVP — простой pickup через инвентарь.

---

## 6. BuildZone — где можно строить

### 6.1 Что такое BuildZone

**BuildZone** = `NetworkBehaviour` в сцене, обозначающий "здесь можно строить". Примеры:
- Платформа в городе Примум: "BuildZone_Primium_01"
- Палуба NPC-корабля (если разрешено): "BuildZone_NpcShip_Template"
- Платформа игрока (будущее): "BuildZone_PlayerShip_01"

**Структура:**
```csharp
public class BuildZone : NetworkBehaviour
{
    [SerializeField] private string _zoneId;  // "STN-PRM-01-BUILD-01"
    [SerializeField] private ulong _ownerClientId;
    [SerializeField] private int _maxElements = 50;
    [SerializeField] private float _buildRadius = 20f;  // trigger radius
    [SerializeField] private LayerMask _allowedSurfaces;  // на что можно ставить

    private SphereCollider _trigger;
    private readonly List<ulong> _playersInZone = new();
}
```

### 6.2 Связь с существующими системами

**Аналогия:** `DockStationController` + `OuterCommZone` + `DockingPadTriggerBox` (см. `docs/Docking_stations/02_V2_ARCHITECTURE.md`):
- `DockStationController` = наш `BuildZone` (scene-placed NetworkBehaviour с definition SO).
- `OuterCommZone` = наш `_trigger` (зона входа).
- `DockingPadTriggerBox` = наш snap target (точка для place).

**Прямая копипаста паттерна!** Это **+0 новых архитектурных решений**.

### 6.3 Типы Build Zones

| Тип | Где | Owner | Use case |
|---|---|---|---|
| **City zone** | На платформе города (Примум, Секунд) | Город (= shared для всех) | Общественные постройки (таблички, лавки) |
| **Personal zone** | На личном корабле игрока | Player | Personal база |
| **Guild zone** (будущее) | Гильдейская платформа | Guild | Гильдейские постройки |
| **NpcShip zone** | На NPC-корабле | Npc | NPC decorations (hardcoded в префабе, не runtime) |

**В MVP:** city zones + personal zones на player корабле (через `KeyRodInstance`). Guild — потом.

### 6.4 Валидация "можно ли строить здесь"

**Серверная проверка в `WorldBuildServer.RequestPlaceRpc`:**

1. **Player is in BuildZone?** — `BuildZone._playersInZone.Contains(clientId)` (проверка в триггере).
2. **Player is owner of zone?** — `BuildZone._ownerClientId == clientId` OR (zone type == City && player has "Citizen" privilege).
3. **Item in inventory?** — `InventoryWorld.CountOf(clientId, itemId) >= 1`.
4. **Position valid?** — `Vector3.Distance(position, zone.transform.position) <= _buildRadius`.
5. **Snap target valid?** — если snap = existing element → проверка owner that element.
6. **Capacity not exceeded?** — `elements.Count < _maxElements`.

Если все ОК → `NetworkList.Add` + ClientRpc broadcast.

---

## 7. Placeable items — `BuildableItem` SO

### 7.1 Структура

```csharp
[CreateAssetMenu(menuName = "Project C/Build/Buildable Item", fileName = "BuildItem_")]
public class BuildableItem : ScriptableObject
{
    [Header("Identity")]
    public string buildItemId;       // "TABLE_WOOD", "CHAIR_METAL"
    public string displayName;
    public Sprite icon;
    public BuildableCategory category;  // Furniture / Light / Storage / Decoration / Functional

    [Header("Visual")]
    public GameObject visualPrefab;  // префаб с MeshRenderer + Rigidbody
    public Vector3 attachOffset;
    public Vector3 attachScale = Vector3.one;

    [Header("Physics")]
    public float mass = 5f;
    public bool isKinematic = false; // false = можно толкать, true = стоит намертво

    [Header("Snap rules")]
    public AllowedSnapTarget[] allowedSnapTargets;  // ground, wall, ceiling, other
    public float snapDistance = 1f;
    public bool allowRotation = true;
    public float[] rotationSteps = {0, 90, 180, 270};  // разрешённые углы

    [Header("Salvage")]
    [Range(0f, 1f)] public float salvageMultiplier = 0.5f;  // 50% материалов назад
    public RecipeData[] salvageReturns;  // что вернуть в инвентарь при pickup
}

public enum BuildableCategory
{
    Furniture,    // стол, стул, кровать
    Light,        // фонарь, лампа, свеча
    Storage,      // сундук, шкаф
    Decoration,   // картина, флаг, ваза
    Functional    // верстак, спальник, etc.
}

public enum AllowedSnapTarget
{
    Ground,       // ставить на землю
    Wall,         // к стене
    Ceiling,      // к потолку
    OnElement,    // поверх другого placeable
    Free          // в воздухе (запрещено?)
}
```

### 7.2 Примеры items для MVP

| Item | Category | Visual prefab | Snap rules | Mass |
|---|---|---|---|---|
| `Table_Wood` | Furniture | 1m x 1m x 0.8m wooden table | Ground | 10kg |
| `Chair_Wood` | Furniture | 0.5m x 0.5m x 1m chair | Ground | 5kg |
| `Bed_Simple` | Furniture | 2m x 1m x 0.5m bed | Ground | 20kg |
| `Lamp_Standing` | Light | tall standing lamp | Ground | 3kg |
| `Lamp_Wall` | Light | wall-mounted lamp | Wall | 1kg |
| `Chest_Storage` | Storage | 1m x 0.5m x 0.5m chest | Ground | 15kg |
| `Flag_City` | Decoration | banner / flag | Wall, Ceiling | 0.5kg |
| `Vase_Clay` | Decoration | small vase | Ground, OnElement | 1kg |

**Всего:** ~10-15 items в MVP. Расширяется контентом.

### 7.3 BuildItemRegistry

```csharp
[CreateAssetMenu(menuName = "Project C/Build/Item Registry", fileName = "BuildItemRegistry")]
public class BuildItemRegistry : ScriptableObject
{
    public List<BuildableItem> items;

    public BuildableItem FindById(string id)
    {
        return items.FirstOrDefault(i => i.buildItemId == id);
    }
}
```

**Anti-restrictive:** если item не в реестре → warning, не crash.

---

## 8. Snap-система для мира (расширенная)

### 8.1 Типы snap targets (для L7)

| Тип | Описание | Как найти | Пример |
|---|---|---|---|
| **Ground** | Пол/земля/палуба | `Physics.Raycast(cursor, Vector3.down)` → `hit.point` + `hit.normal` | Стол на палубе |
| **Wall** | Вертикальная поверхность | `Physics.Raycast(cursor, dir)` → `hit.normal.y < 0.5` | Картина на стене |
| **Ceiling** | Горизонтальная поверхность снизу | `Physics.Raycast(cursor, Vector3.up)` снизу | Лампа на потолке |
| **OnElement** | Поверх другого placeable | `hit.collider.GetComponent<WorldBuildElement>()` | Ваза на столе |
| **ZoneSocket** | Предзаданная точка в `BuildZone` | `[SerializeField] Transform[] _zoneSockets` | "Anchor" для флага |
| **Free** | В воздухе (если `allowedSnapTargets` включает Free) | — | Декорация в воздухе |

### 8.2 SnapResolver

```csharp
public class BuildSnapResolver
{
    private const float MAX_SNAP_DISTANCE = 2f;

    public SnapResult Resolve(
        Vector3 cursorPos,
        BuildableItem item,
        BuildZone zone,
        IReadOnlyDictionary<ulong, WorldBuildElementDto> existingElements)
    {
        if (item == null || zone == null)
            return SnapResult.None(cursorPos);

        // 1. Raycast от cursor — попадаем в что-то?
        if (Physics.Raycast(cursorPos, Vector3.down, out RaycastHit hit, MAX_SNAP_DISTANCE))
        {
            // 2. Попали в existing placeable element?
            var elem = hit.collider.GetComponentInParent<WorldBuildElement>();
            if (elem != null && item.allowedSnapTargets.Contains(AllowedSnapTarget.OnElement))
            {
                Vector3 surfaceOffset = hit.normal * 0.05f;  // 5cm над surface
                return SnapResult.SnapToElement(elem.ElementId, hit.point + surfaceOffset, hit.normal);
            }

            // 3. Попали в ground/wall/ceiling?
            if (hit.normal.y > 0.7f && item.allowedSnapTargets.Contains(AllowedSnapTarget.Ground))
                return SnapResult.SnapToGround(hit.point, hit.normal);

            if (Mathf.Abs(hit.normal.y) < 0.3f && item.allowedSnapTargets.Contains(AllowedSnapTarget.Wall))
                return SnapResult.SnapToWall(hit.point, hit.normal);

            // 4. Попали в zone socket?
            var socket = zone.FindNearestSocket(hit.point, MAX_SNAP_DISTANCE);
            if (socket != null && item.allowedSnapTargets.Contains(AllowedSnapTarget.ZoneSocket))
                return SnapResult.SnapToSocket(socket);
        }

        // 5. Fallback: free position (если разрешён) или fail
        if (item.allowedSnapTargets.Contains(AllowedSnapTarget.Free))
            return SnapResult.Free(cursorPos);

        return SnapResult.Invalid(cursorPos, "Нет подходящей поверхности");
    }
}
```

### 8.3 Отличия от L6 (корабле-snap)

| L6 (корабль) | L7 (мир) |
|---|---|
| Нет ground (корабль = сам snap target) | Ground обязателен |
| Snap к `ShipRoot` / `HullSocket` / `BuildElement` | Snap к Ground / Wall / Ceiling / `BuildElement` / `ZoneSocket` |
| Нет physical raycast | `Physics.Raycast` обязателен |
| Player может строить когда docked | Player может строить когда в зоне (любая позиция) |

---

## 9. Физика placeable items

### 9.1 Зачем физика

Если предмет **просто висит в координате** — он выглядит как decoration (как L6 на корабле). Если у него **Rigidbody** — он:
- Падает на пол (если spawned без ground support).
- Может быть толкнут другим player'ом (визуально).
- Становится "материальным" (substance).

**Для sandbox feel** — физика обязательна.

### 9.2 Как делаем физику

**На `BuildVisualApplier` при spawn:**
```csharp
var go = Instantiate(item.visualPrefab, position, rotation);
var rb = go.GetComponent<Rigidbody>();
if (rb == null) rb = go.AddComponent<Rigidbody>();
rb.mass = item.mass;
rb.isKinematic = item.isKinematic;  // true для декораций (флаги, картины)
go.AddComponent<WorldBuildElement>();  // маркер для snap/UI
go.GetComponent<WorldBuildElement>().Initialize(elementDto);
```

**Анти-чит:** клиент НЕ спавнит сам — только через ClientRpc от сервера.

### 9.3 Edge cases

| Случай | Что делать |
|---|---|
| **Предмет проваливается сквозь пол** | Spawn чуть выше ground (5cm offset), Rigidbody.gravity = true |
| **Предмет толкают другие игроки** | Rigidbody не kinematic → другие игроки могут двигать (но owner не меняется) |
| **Предмет улетает за пределы зоны** | На сервере: проверка `position.distance < _buildRadius` каждые N секунд → если вне зоны → teleport обратно |
| **Два предмета пересекаются** | На сервере: `Physics.OverlapBox` после place → если intersect → revert |
| **Player walking** | `CharacterController` уже отталкивает placeable items (как PickupItem) |

### 9.4 Что НЕ делаем (явно)

- ❌ **Gravity scale / физические эффекты** (погода, ветер) — не для MVP
- ❌ **Damage от падения** — предметы не бьются
- ❌ **Физика на NPC-кораблях** — не для нашего L7
- ❌ **Мосты между зонами** — отдельная фича

---

## 10. Persistence (новый слой)

### 10.1 Что персистим

**Per `BuildZone`:**
```json
{
  "zones": {
    "STN-PRM-01-BUILD-01": {
      "zoneNetId": 12345,  // опционально, для отладки
      "ownerClientId": 0,  // 0 = city zone
      "elements": [
        {
          "elementInstanceId": 1,
          "buildItemId": "TABLE_WOOD",
          "ownerClientId": 7,
          "worldPosition": [40010, 2512, 40005],
          "rotationEuler": [0, 90, 0],
          "snapFace": 0,
          "parentElementId": 0
        },
        {
          "elementInstanceId": 2,
          "buildItemId": "LAMP_WALL",
          "ownerClientId": 7,
          "worldPosition": [40012, 2514, 40005],
          "rotationEuler": [0, 0, 0],
          "snapFace": 1,  // wall
          "parentElementId": 0
        }
      ]
    },
    "STN-PRM-02-BUILD-01": {
      ...
    }
  }
}
```

### 10.2 JsonBuildWorldRepository

**Прямая копипаста `JsonKeyRodInstanceRepository`** (см. `docs/Ships/Key-subsystem/22_SHIP_TELEMETRY_PLAN.md`):

```csharp
public interface IBuildWorldRepository
{
    Dictionary<string /*zoneId*/, List<WorldBuildElementDto>> LoadAll();
    void SaveAll(Dictionary<string, List<WorldBuildElementDto>> state);
}

public class JsonBuildWorldRepository : IBuildWorldRepository
{
    private const string FILE_NAME = "build_world.json";
    private string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    public Dictionary<string, List<WorldBuildElementDto>> LoadAll() { /* ... */ }
    public void SaveAll(...) { /* ... */ }
    public void AutoSave() { /* вызывается из BuildWorld */ }
}
```

**Anti-restrictive:** если JSON нет → пустой реестр. Если JSON битый → warning + backup.

---

## 11. Что мы НЕ делаем (явные out-of-scope для MVP)

| Что | Почему не делаем |
|---|---|
| **Grid system (foundation+wall)** | Вариант B — отдельный roadmap |
| **Multi-crew build** | Только owner зоны |
| **Build permissions (allowlist)** | Phase 2 |
| **Build cooldowns / rate limit** | Один элемент — одна RPC, OK |
| **Build undo/redo** | Большой UI effort |
| **Damage / destruction** | Не боёвка со зданиями |
| **Visual variety (текстуры, decals)** | Контент, не код |
| **Animated furniture (вращающийся фонарь)** | Отдельный Animator в prefab'е |
| **Sound on place** | Контент |
| **Build cooldowns (после N размещений)** | Anti-frustration, но не критично |
| **Custom terrain deformation** | Гигантская подсистема |
| **Electricity / pipes** | Valheim-style, не для stage 2.5 |
| **Permissions per element** (кто может двигать конкретный стул) | Owner зоны = owner всех элементов |
| **Stacking** (стол на столе на столе) | Anti-physical (физика не даст) |
| **Save/load presets** ("продай мне этот интерьер") | Phase 2 |

**Главный принцип:** placeable = physical item, который можно поставить, поднять, переставить, удалить. Не дом, не сеть, не экономика.

---

## 12. Caveats и риски

### 12.1 Ghost preview — производительность

**Проблема:** ghost preview обновляется каждый кадр. Если у него `MeshCollider` + shadow casting — тормоза.

**Решение:**
- Ghost preview = **упрощённый меш** (без collider, без shadow).
- Или `MeshRenderer.shadowCastingMode = Off` + transparent material.
- `LODGroup` — none на ghost.

### 12.2 Snap target под курсором — feedback

**Проблема:** игрок не видит, к чему snap. UI overlay ("Snap to: ground" / "Invalid position") помогает.

**Решение:** `BuildGhostApplier.OnSnapResultChanged` → `BuildClientState` → UI показывает текст + цвет ghost'а (зелёный = valid, красный = invalid).

### 12.3 Предмет проваливается сквозь пол

**Проблема:** при spawn с `Rigidbody.gravity = true` + ground slop — может проскочить.

**Решение:**
- Spawn на 5cm выше ground point.
- Или `isKinematic = true` для first 1 second → после того как settled → switch to dynamic.
- Или просто `isKinematic = true` всегда (как L6 — visual only).

**Рекомендация:** для MVP — `isKinematic = true` для большинства items (стол, стул, кровать — стоят намертво). `gravity = true` для специальных items (бочки, ящики — которые можно толкать). Конфигурируется через `BuildableItem.isKinematic`.

### 12.4 Зона удаляется при logout

**Проблема:** player logged out → его elements остаются в зоне (на сервере) → другой player видит их.

**Решение:** по умолчанию элементы **остаются**. Если хотим "private zone" — нужен permission system. Для MVP — все видят, никто кроме owner не может modify.

### 12.5 Два player'а строят одновременно

**Проблема:** два player'а в одной зоне оба press B → оба enter build mode → оба пытаются place в одно место.

**Решение:**
- Build mode **per player** (локальный state).
- Place RPC → сервер проверяет валидность (snap target, position bounds).
- Если conflict (другой player уже там) → reject с сообщением "Position occupied".
- Нет гонки — сервер авторитативен.

### 12.6 Persistent element после restart — невалидный snap

**Проблема:** player placed item → logged out → server restart → loaded from JSON → snap target утерян (другой элемент был удалён).

**Решение:** при load → для каждого element проверить snap target → если invalid → переместить на ground / поставить invalid flag → warning.

### 12.7 Capacity лимит

**Проблема:** игрок строит 200 предметов → лагает.

**Решение:** `BuildZone._maxElements = 50` (per zone). Если > 50 → reject + UI подсказка "Достигнут лимит зоны, постройте в другой".

---

## 13. Roadmap зависимостей (что нужно ДО этого)

### 13.1 Hard dependencies (блокируют)

| Что | Почему | Статус |
|---|---|---|
| **L6 корабле-строительство** | Общий `BuildWorld`, `BuildGhostApplier`, `SnapResolver` | ⏳ В плане (см. `../../Ships/customisation/buildings/00`) |
| **`MyShipsTab` (T-KEY08)** | UI-хаб для кнопки "Build" | ⏳ В roadmap |
| **`PickupItem` extension для visualPrefab** | Уже есть, но нужно проверить что работает для buildable items | ✅ Phase 1 |

### 13.2 Soft dependencies (упростят, но не блокируют)

| Что | Что даст |
|---|---|
| **`PickupDeckRide` (carry pattern)** | Refference для `CarryController` (Phase 2) |
| **ResourceNode (mining)** | Контент: placeable items добываются через mining |
| **Crafting (recipes)** | Расширение: крафт placeable items |

### 13.3 Ничего не блокирует — start anytime после L6 + MyShipsTab

То есть: L1 модулей → MyShipsTab → L6 корабле-строительство → L7 мировое строительство.

---

## 14. Трудоёмкость (конкретные оценки)

### 14.1 MVP — "placeable furniture в designated Build Zones"

| Тикет | Что | LOC | Дни |
|---|---|---|---|
| T-WBLD-01 | `BuildableItem` SO + `BuildItemRegistry` + 10 buildable items | ~400 | 3 |
| T-WBLD-02 | `BuildZone` + scene placement | ~200 | 1 |
| T-WBLD-03 | `BuildWorld` + `WorldBuildServer` + RPC place/remove/move | ~700 | 4 |
| T-WBLD-04 | `BuildVisualApplier` (Rigidbody + visualPrefab) | ~300 | 2 |
| T-WBLD-05 | `BuildSnapResolver` (ground/wall/ceiling/element) | ~400 | 3 |
| T-WBLD-06 | `BuildGhostApplier` (3D preview) | ~300 | 2 |
| T-WBLD-07 | `BuildClientState` + events | ~250 | 2 |
| T-WBLD-08 | `JsonBuildWorldRepository` + persistence | ~250 | 2 |
| T-WBLD-09 | `BuildModeWindow` UI Toolkit | ~400 | 3 |
| T-WBLD-10 | `BuildRecipeData` extension (5 крафт-рецептов) | ~150 | 1 |
| T-WBLD-11 | Pickup existing element (request → inventory) | ~200 | 1 |
| T-WBLD-12 | 2 BuildZones в `WorldScene_0_0` (тестовая зона) | — | 1 |
| T-WBLD-13 | Integration tests + Play Mode | — | 2 |
| **Итого MVP** | | **~3550** | **~27 дней (5-6 нед)** |

### 14.2 Сложные фичи (по запросу)

| Фича | Дни |
|---|---|
| Carry в руках (`CarryController`) — Phase 2 | +5 |
| Grid system (foundation + wall + roof) — L7+ | +12-20 |
| Multi-crew build (permissions) | +3 |
| Undo/redo (последние 10) | +5 |
| Salvage с правильными рецептами | +3 |
| Visual variety (20+ items) | +5-10 |
| Build presets ("продать интерьер") | +5 |
| Animated decorations | +5 |
| **Total с фичами** | **~70 дней (~14 нед)** |

### 14.3 Industrial references

| Игра | Что делали | Время |
|---|---|---|
| **Valheim** (Iron Gate) | Base building с grid | ~6-12 мес, маленькая команда |
| **No Man's Sky** (Hello Games) | Base building + power | ~1+ год, ~30 разработчиков |
| **Fortnite Creative** (Epic) | Sandbox с logic | Годы |
| **Conan Exiles** (Funcom) | Base building + permissions | ~1 год |
| **Rust** (Facepunch) | Base building + decay | Годы (incremental) |

**Наш ~5-6 недель MVP** — адекватно для варианта A (placeable furniture в зонах).

---

## 15. Сводная карта — что копируем, что новое, что не нужно

### Из L6 (корабле-строительство)

| Компонент | Что | Статус |
|---|---|---|
| `BuildableItem` SO | Копируем, расширяем (allowedSnapTargets, isKinematic, salvageMultiplier) | ✅ Reuse + extend |
| `BuildClientState` | Копируем, расширяем (zoneId, hoveredElement) | ✅ Reuse + extend |
| `BuildGhostApplier` | Копируем, расширяем (ground/wall snap) | ✅ Reuse + extend |
| `BuildVisualApplier` | Копируем, расширяем (Rigidbody для мира) | ✅ Reuse + extend |
| `JsonBuildWorldRepository` | Копируем, новая схема (zoneId vs shipInstanceId) | 🆕 На основе L6 |
| `BuildZone` | Копируем, новый (нет в L6) | 🆕 На основе `OuterCommZone` |
| `WorldBuildServer` | Копируем, новая (нет в L6) | 🆕 На основе `ShipModuleServer` |

### Из существующих подсистем (не L6)

| Что | Откуда |
|---|---|
| Inventory integration (item в инвентаре → place в мире) | `InventoryWorld.AddItemDirect` + `InventoryServer.RequestDropRpc` (reversed) |
| Pickup physical item | `PickupItem` + `PickupDeckRide` (T-PICKUP-RIDE-01) |
| Physical drop from inventory | `InventoryServer.RequestDropRpc` |
| Trigger zone | `OuterCommZone` + `DockingPadTriggerBox` |
| Scene-placed NetworkBehaviour | `DockStationController` |
| NetworkList replicated state | `ShipModuleServer._installedModules` (или `EquipmentServer._equipmentList`) |
| Server-only singleton | `KeyRodInstanceWorld` pattern |
| JSON persistence | `JsonKeyRodInstanceRepository` |
| Owner validation | `ShipOwnershipRequirement` (расширяем под zoneOwner) |
| Static event broadcast | `ShipModuleServer.OnModuleChanged` |
| Carry physics pattern (Phase 2) | `PickupDeckRide` (T-PICKUP-RIDE-01) |

### Новое (нет аналогов в проекте)

| Что | Почему новое |
|---|---|
| `BuildSnapResolver` (ground/wall raycast) | L6 не использует ground — другая snap-логика |
| `WorldBuildElement` (markup for snap target) | Marker для placed items |
| `BuildableItem.isKinematic` | Физика для placeable (нет в L6) |
| `BuildZone._maxElements` + capacity check | Нет в L6 (per-ship limit) |
| `BuildZone._ownerClientId` | Per-zone owner (нет в L6) |
| `BuildModeWindow` UI | Отдельный UI (L6 имеет UI внутри RepairManagerWindow) |

**~85% — копипаста из существующих паттернов (L6 + общая инфраструктура).** ~15% — действительно новое (snap-to-ground, физика, capacity).

---

## 16. Сравнение с конкурентами

| Игра | Что делают | Мы делаем по аналогии | Различия |
|---|---|---|---|
| **Valheim** | Grid placement, snap-to-snap, decay | Snap-to-element, NO decay | У нас нет decay (не survival) |
| **Sea of Thieves** | Outfit system (hull/sails/trim) | L6 + L7 (visual only) | У нас есть физика placeable |
| **No Man's Sky** | Free placement anywhere | Designated zones (для L7) | У нас нет global free placement |
| **Fortnite Creative** | Sandbox с logic | Только L7 (visual only) | У нас нет logic gates |
| **Rust** | Full survival building | L7+ (future) | У нас нет permissions/raid |
| **Conan Exiles** | Building + thralls/crafting | L7 (placeable) | У нас нет NPCs внутри зданий |
| **Eco** | Server-wide economy + building | L7 + Trade | У нас нет simulation |

**Ближайший аналог:** Sea of Thieves (outfit + furniture) + Valheim (placeable furniture).

---

## 17. Синергия с существующими подсистемами

### 17.1 Что можно построить в Build Zone, что уже существует

| Buildable | Что это | Существующий аналог |
|---|---|---|
| `Chest_Storage` | Сундук для хранения | `NetworkChestContainer` уже есть! Можно использовать как visualPrefab, добавить `WorldBuildElement` маркер |
| `Table_Wood` | Стол | — |
| `CraftingStation_Basic` (future) | Крафт-станция в доме | `CraftingStation` уже есть, можно spawn в build mode |
| `Bed_Simple` | Кровать (respawn point — future) | — |
| `Lamp_Wall` | Освещение | — |

**Синергия:** `NetworkChestContainer` можно использовать как `BuildableItem.visualPrefab` — игрок может **поставить сундук** в Build Zone. Это даёт **новый use case** для существующего компонента.

### 17.2 NPC decorations (passive)

`BuildableItem` может использоваться не только игроком, но и как **NPC decorations**:
- `Prefab_NpcShop_Stationary.asset` — стол + прилавок + фонарь. Спавнится в сцене как NPC-станция.
- Это не требует L7 — просто prefab со встроенными декорациями.

**Синергия:** арт-команда может создавать NPC-станции через `BuildableItem` для консистентности визуала.

### 17.3 Quest items (future)

`QuestItem` (от `QuestServer`) — квестовый предмет, который можно подобрать. Можно использовать `BuildableItem` как quest reward — игрок крафтит редкий предмет и строит из него что-то. **Но это Phase 2.**

---

## 18. Open questions (на усмотрение пользователя)

1. **Placeable furniture (A) или base building (B)?** По умолчанию — A. Если хотите B — другой scope (12-20 нед вместо 5-6).
2. **Carry в руках (Phase 2) или только через инвентарь?** По умолчанию — только через инвентарь (проще, использует существующий `InventoryServer`).
3. **Designated zones только на city platforms или везде?** По умолчанию — city platforms + player ship (через KeyRod). NPC-корабли — нет.
4. **Multi-crew build — может ли co-pilot строить?** По умолчанию — только owner zone. Упрощает.
5. **Build cooldowns (после N размещений)?** По умолчанию — нет. `BuildZone._maxElements = 50` достаточно.
6. **Visual variety в MVP — 10 items или больше?** По умолчанию — 10-15 items (5 категорий × 2-3 варианта).
7. **Физика — `isKinematic = true` или `gravity = true`?** По умолчанию — `isKinematic = true` для большинства (стол/стул/кровать), `gravity = true` для специальных (бочки/ящики).
8. **Сундук в Build Zone = `NetworkChestContainer` или отдельный `Storage_Build`?** По умолчанию — переиспользуем `NetworkChestContainer` (нужен только маркер `WorldBuildElement`).
9. **Salvage при pickup — какой %?** По умолчанию — 50% (`salvageMultiplier = 0.5f`).
10. **Максимум elements на zone?** По умолчанию — 50 (защита от лагов).

---

## 19. Связанные документы

| Документ | Что показывает |
|---|---|
| `../../Ships/customisation/buildings/00_BUILD_SYSTEM_DEEP_ANALYSIS.md` | L6 корабле-строительство — общая база |
| `../../Ships/customisation/00_SUMMARY.md` | L0-L6 кастомизации |
| `../../Ships/Modul_system/01_ARCHITECTURE.md` | ShipModuleServer + RepairManager — серверный хаб pattern |
| `../../Ships/Key-subsystem/00_OVERVIEW.md` | KeyRodInstanceWorld + JSON persistence |
| `../../Docking_stations/02_V2_ARCHITECTURE.md` | DockStationController + OuterCommZone — zone pattern |
| `../../Crafting_system/00_OVERVIEW.md` | CraftingWorld + RecipeData |
| `../../Mining/00_OVERVIEW.md` | ResourceNode + GatheringServer — physical world object pattern |
| `../../Character/Customisation/00_OVERVIEW.md` | CustomisationClientState — client projection pattern |
| `Assets/_Project/Scripts/Core/PickupItem.cs` | Physical item pickup (бобаинг, trigger, drop) |
| `Assets/_Project/Scripts/Core/PickupDeckRide.cs` | Carry pattern — **refference для carry в руках (Phase 2)** |
| `Assets/_Project/Scripts/Core/LootTable.cs` | LootTable для сундуков |
| `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs` | NetworkBehaviour + inventory — образец |
| `Assets/_Project/Scripts/Ship/ShipModuleServer.cs` | NetworkBehaviour + RPC + event — копируем для WorldBuildServer |
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` | Server-only static singleton — копируем для BuildWorld |
| `Assets/_Project/Items/Network/InventoryServer.cs:115-160` | `RequestDropRpc` — drop item в мир (reversed для pickup) |
| `project-c-composite-object-architecture` skill | Marker pattern для build elements |