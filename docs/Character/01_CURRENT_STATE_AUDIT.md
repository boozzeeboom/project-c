# Current State Audit — Character Progression

> **Дата:** 2026-06-14
> **Метод:** 3 параллельных сабагента + ручная верификация через `read_file` / `search_files`
> **Скоуп:** что уже есть в проекте, что можно переиспользовать, что нужно создать

---

## 1. Что уже реализовано (готовые точки входа)

### 1.1 CharacterWindow — 6 табов готовы к расширению

**Файлы:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — 2156 строк, 105 KB
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — 177 строк
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — 509 строк
- `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset` — PanelSettings

**Что уже работает:**
- ✅ 6 табов: `character / ship / reputation / contracts / inventory / quests`
- ✅ 4 FIX'а UI Toolkit применены (`pickingMode Ignore/Position`, `ApplyInlineFallbackStyles`, `Cursor.lockState`, `MarkDirtyRepaint + schedule.Execute StartingIn(50)`)
- ✅ 5 state-подписок (ContractClientState, InventoryClientState, ReputationClientState, NpcAttitudeClientState, QuestClientState) — канонический `SubscribeX/UnsubscribeX` + `_isXSubscribed` флаг pattern
- ✅ Tab switching через `SwitchTab(string tab)` — 6 bool-флагов + 6 sections visibility + filters visibility
- ✅ 6 row-factory (MakeXxxRow/BindXxxRow) — pattern для любых списков
- ✅ Реюзные USS-классы: `.stats-grid`, `.stat-row`, `.stat-label`, `.stat-value`, `.reputation-bar`, `.reputation-fill`, `.quest-row`, `.quest-sub`

**Что есть в character-section (стат-лейблы):**

| `name` | Класс | Текущий источник | Что нужно |
|--------|-------|------------------|-----------|
| `stat-name` | `stat-value` | `_localPlayer.IsLocalPlayer` | Оставить |
| `stat-level` | `stat-value` | **hardcoded "1"** | Заменить на stat-tile |
| `stat-xp` | `stat-value` | **hardcoded "0"** | Заменить на stat-tile |
| `stat-credits` | `stat-value` | `MarketClientState.CurrentSnapshot.credits` | Оставить |
| `stat-debt` | `stat-value debt-ok` | `ContractClientState.CurrentSnapshot.debtAmount` | Оставить |
| `stat-active-contracts` | `stat-value` | `ContractClientState.CurrentSnapshot.active.Length` | Оставить |

**Вердикт:** CharacterWindow готов к расширению. Добавляем таб "ПРОГРЕССИЯ" как **вложенный sub-tab родитель** (чтобы избежать 9 top-level кнопок оборачивающихся на 2 ряда).

### 1.2 WorldEventBus — центральная шина событий (готова)

**Файл:** `Assets/_Project/Core/WorldEventBus.cs` (82 строки, T-X0 статический singleton)

**API:**
```csharp
WorldEventBus.Publish<T>(T ev) where T : WorldEvent
WorldEventBus.Subscribe<T>(Action<T> handler) where T : WorldEvent
WorldEventBus.Unsubscribe<T>(Action<T> handler)
WorldEventBus.Reset()  // Editor/EditMode only
```

**Существующие event types:** `WorldEvent.cs` содержит:
- `ItemAddedEvent`, `ItemRemovedEvent` (InventoryWorld publishes)
- `ReputationChangedEvent`, `NpcAttitudeChangedEvent` (QuestWorld publishes)
- `QuestStateChangedEvent` (**класс есть, но НЕ публикуется** — нужно добавить emit)
- `DialogVisitedEvent` (QuestServer publishes, несёт `NpcId` для anti-spam)
- `CustomEvent`, `DayNightPhaseChangedEvent`
- `ContractAcceptedEvent/CompletedEvent/FailedEvent` (ContractServer publishes)

**Subscribers (на сегодня):** `QuestServer.cs:92-98` подписан на 7 событий (ItemAdded, ItemRemoved, ReputationChanged, NpcAttitudeChanged, CustomEvent, DialogVisited, DayNightPhase). `ContractMetaBridge.cs:48-50` подписан на 3 contract-events.

**Вердикт:** Это **готовая инфраструктура** для подписки `StatsServer`. Не нужно создавать новый bus — добавляем новые event types в `WorldEvent.cs` и публикуем из существующих серверов в success-ветках.

### 1.3 Mining — полностью реализован (точка входа для STR)

**Файлы:** `Assets/_Project/Scripts/ResourceNode/`
- `ResourceNodeConfig.cs` — SO, [CreateAssetMenu]
- `ResourceNode.cs` — NetworkBehaviour, gather state machine
- `GatheringServer.cs` — RPC hub (BootstrapScene)
- `GatheringClientState.cs` — singleton, OnGatherCompleted event
- `GatheringToastController.cs` — UI toast

**Entry-point для STR:** `GatheringClientState.OnGatherCompleted` (line 47) — `event Action<string, int, bool>` — `(itemName, quantity, isDepleted)`. **Но client-only**.

**Server-side точка:** `GatheringServer.cs:159` — `SendGatherResultToClient` в Completed-ветке. Здесь добавляем `WorldEventBus.Publish(new MiningCompletedEvent {...})`.

**Минимальное изменение для подписки:** 1 строка в `GatheringServer.cs:159` (publish) + 1 строка в `WorldEvent.cs` (класс события) + ничего больше.

### 1.4 Crafting — точка входа для INT

**Файлы:** `Assets/_Project/Scripts/Crafting/`
- `CraftingServer.cs` — RPC hub (BootstrapScene)
- `CraftingClientState.cs` — singleton, OnCraftingCompleted event (line 33)

**Entry-point для INT (craft):** `CraftingClientState.OnCraftingCompleted` — `event Action<ulong, string>` — `(stationNetId, resultItemName)`. Client-only.

**Server-side точка:** `CraftingServer.cs:82-103` (`OnCraftingTick`) — детектит `job.State == Completed` transition. Здесь добавляем publish.

### 1.5 Exchange — точка входа для INT (resource conversion)

**Файлы:** `Assets/_Project/Trade/Exchange/`
- `ExchangeServer.cs` — RPC hub (BootstrapScene)

**Entry-point:** `RequestPackRpc` / `RequestUnpackRpc` в `ExchangeServer.cs:154, 214` — success-ветка. **Нет events вообще** — нужно создать `ExchangeCompletedEvent`.

### 1.6 Market — точка входа для INT (торговля)

**Файлы:** `Assets/_Project/Trade/Scripts/Network/MarketServer.cs`

**Entry-point:** `RequestBuyRpc` / `RequestSellRpc` в `MarketServer.cs:134, 150` — success-ветка (`r.IsSuccess`). **Нет events вообще** — нужно создать `MarketTradedEvent`.

### 1.7 Quest / NPC — точка входа для INT (диалоги + квесты)

**Файлы:** `Assets/_Project/Quests/`
- `QuestServer.cs` — RPC hub + dialog flow
- `QuestClientState.cs` — singleton
- `Dialogue/DialogWindow.cs` — UI

**Готовые events:**
- ✅ `DialogVisitedEvent` — публикуется в `QuestServer.cs:612`, несёт `NpcId` (line 117). **Идеально для INT от диалогов + защита от спама с одним NPC.**
- ❌ `QuestStateChangedEvent` — класс есть в `WorldEvent.cs:100`, **но никогда не публикуется**. Нужно добавить emit в `QuestServer.OnWorldStageTransition:455` (Discovered→Active, Active→Completed).

**NPC-spam protection:** `NpcController.NpcId` (`Assets/_Project/Quests/NpcController.cs:52`) — stable string (`"npc_blacksmith_001"`). Используем `Dictionary<ulong, Dictionary<string, float>> _lastXPGainPerNpc` в `StatsServer` — 60 sec cooldown per (playerId, npcId).

### 1.8 Ship — точка входа для INT (пилотирование)

**Файлы:**
- `Assets/_Project/Scripts/Player/ShipController.cs` (piloting, server-authoritative)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `_inShip` поле (line 55, 123, 134)

**Server-side состояние:**
- `ShipController._pilots` (HashSet<ulong>, line 131) — заполняется через `AddPilotRpc/RemovePilotRpc` (line 921, 939)
- `ShipController.FixedUpdate` (line 352-355) — early-out если `_pilots.Count == 0`

**Для "как много перемещается в корабле (пилотирует)":**
- В `ShipController.FixedUpdate` (server) добавляем `Vector3.Distance(_rb.position, _lastPos)` per pilot per tick
- Накапливаем в `StatsServer` через subscribe на новый `ShipPilotTickEvent` (или прямое чтение из StatsServer)

### 1.9 Player movement — точка входа для DEX

**Файлы:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — единственный активный Player-контроллер (PlayerController.cs отключён в `NetworkPlayer.OnNetworkSpawn:188`)
- **Input:** прямой `Keyboard.current.*` (line 396-401) — `PlayerInputReader.cs` существует но не подключён

**Что есть:**
- ✅ `WASD` movement (line 396-399) — пешее движение
- ✅ `Space` jump (line 400)
- ✅ `LeftShift` run/sprint (line 401)
- ✅ `E` interact / `F` action / `P` CharacterWindow / `Tab` InventoryUI
- ❌ **Нет `DistanceTraveled` / `TotalDistance` tracking** — 0 hits по grep в проекте
- ❌ **Нет server-side jump RPC** — `spaceKey.wasPressedThisFrame` только на owner client
- ❌ **Нет server-side run/sprint tracking** — `leftShiftKey.isPressed` только на owner client

**Что нужно создать:**
- В `StatsServer.FixedUpdate` (server) — пройти по всем `NetworkPlayer`, для каждого где `!_inShip`, накопить `Vector3.Distance(transform.position, _lastPos)` → `WalkedDistanceXP[playerId]`
- На `NetworkPlayer` (owner) — добавить `SubmitJumpRpc` (owner→server) для счётчика прыжков
- На `NetworkPlayer` (owner) — добавить `NetworkVariable<bool> IsRunning` (owner-write, server-read)

### 1.10 ItemData — базовый SO для всех предметов

**Файл:** `Assets/_Project/Scripts/Core/ItemType.cs:17-34`

**Поля:** `itemName`, `itemType` (enum), `description`, `icon`, `maxStack`, `weightKg`.

**Enum:** `ItemType { Resources=0, Equipment=1, Food=2, Fuel=3, Antigrav=4, Meziy=5, Medical=6, Tech=7 }`.

**Что есть:**
- 1006 `.asset` файлов в `Assets/_Project/Resources/Items/`
- `ItemRegistry` (preferred) — explicit stable ids
- `InventoryData` (struct, INetworkSerializable) — 8 `List<int>` полей, по одному на каждый ItemType

**Чего НЕТ:**
- ❌ **Нет понятия "equippable" / "wearable"** — `Equipment` это просто 8-й ItemType, без slot binding
- ❌ **Нет "stack count"** — каждый id в list = 1 unit (комментарий в `InventoryWorld.cs:149`)
- ❌ **Нет stat-bonuses на ItemData** — только базовые поля

**Вердикт:** Clothing и Module расширяем `ItemData` через наследование (`ClothingItemData : ItemData`) — переиспользуем `itemName`, `icon`, `weightKg`, `ItemRegistry` registration, добавляем `slot`, `statBonuses`, `requiredSkills`.

### 1.11 Persist / Save — паттерн уже отработан

**Три repository-имплементации:**

| Repository | Файл | Что хранит |
|-----------|------|------------|
| `IPlayerDataRepository` + `PlayerPrefsRepository` / `ServerFileRepository` | `Assets/_Project/Trade/Scripts/Repository/` | Credits, warehouse, cargo |
| `IInventoryRepository` + `JsonInventoryRepository` | `Assets/_Project/Core/JsonInventoryRepository.cs` | Inventory per-clientId |
| `IQuestStateRepository` + `JsonQuestStateRepository` | `Assets/_Project/Quests/Persistence/JsonQuestStateRepository.cs` | Quest state per-clientId |

**Паттерн:**
- File per clientId: `{persistentDataPath}/<file>_<clientId>.json`
- `JsonUtility` для сериализации (не работает с `Dictionary<>` — нужны parallel `List<>`)
- Атомарная запись через `tmp` + `Move` (quest pattern, не inventory — последний делает прямо `File.WriteAllText`)

**Вердикт:** Создаём `ICharacterDataRepository` + `JsonCharacterDataRepository` — файл `character_<clientId>.json` для Stats + Equipment + Skills. Reuse паттерна.

### 1.12 SO-паттерн (ResourceNodeConfig как золотой стандарт)

**Файл:** `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs` (146 строк)

**Канонические конвенции:**
- `[CreateAssetMenu(fileName = "...", menuName = "Project C/...", order = N)]`
- `[Header("Group Name")]` для группировки полей в инспекторе
- `[Tooltip("...")]` на каждом поле
- `[SerializeField] private` для инспектор-exposed полей (PascalCase c `_` prefix)
- `[Range(min, max)]` и `[Min(value)]` для ограничений
- Public read-only properties для runtime-доступа
- `OnValidate()` в `#if UNITY_EDITOR` для проверки согласованности
- Default fallback в property (`!string.IsNullOrEmpty(...) ? ... : ...`)

**Вердикт:** Все новые SO (StatsConfig, ClothingItemData, ModuleItemData, SkillNodeConfig) следуют этому паттерну.

---

## 2. Что нужно создать (gaps)

### 2.1 Server-side файлы (новые)

| Файл | Назначение | Шаблон |
|------|------------|--------|
| `Assets/_Project/Scripts/Stats/StatsServer.cs` | NetworkBehaviour, subscribe to WorldEventBus + distance tracker | Копия `GatheringServer.cs` |
| `Assets/_Project/Scripts/Stats/StatsWorld.cs` | POCO singleton, `Dictionary<ulong, PlayerStats>` | Копия `InventoryWorld.cs:36-77` |
| `Assets/_Project/Scripts/Stats/StatsClientState.cs` | singleton, `OnStatsUpdated` event | Копия `ReputationClientState.cs` |
| `Assets/_Project/Scripts/Stats/Dto/StatsSnapshotDto.cs` | INetworkSerializable struct | Копия `InventorySnapshotDto.cs` |
| `Assets/_Project/Scripts/Stats/Dto/PlayerStatsDto.cs` | INetworkSerializable struct | Mirror Reputation DTO |
| `Assets/_Project/Scripts/Stats/JsonCharacterDataRepository.cs` | файл per clientId | Копия `JsonInventoryRepository.cs` |
| `Assets/_Project/Scripts/Stats/PlayerStats.cs` | server-side state struct (3 floats + level) | Mirror InventoryData |
| `Assets/_Project/Scripts/Stats/StatGrowthConfig.cs` | SO (baseXP, growthRate, globalMultiplier, per-stat) | Mirror ResourceNodeConfig |
| `Assets/_Project/Scripts/Stats/StatsConfig.cs` | SO (формула роста, NPC-spam cooldown, distance XP) | Mirror ResourceNodeConfig |
| `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` | NetworkBehaviour, TryEquip/TryUnequip RPC | Mirror InventoryServer |
| `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | POCO singleton | Mirror InventoryWorld |
| `Assets/_Project/Scripts/Equipment/EquipmentData.cs` | struct, Dictionary<slot, itemId> | Mirror InventoryData |
| `Assets/_Project/Scripts/Equipment/EquipmentClientState.cs` | singleton | Mirror InventoryClientState |
| `Assets/_Project/Scripts/Equipment/Dto/EquipmentSnapshotDto.cs` | INetworkSerializable | Mirror InventorySnapshotDto |
| `Assets/_Project/Scripts/Equipment/Dto/EquipSlot.cs` | enum (Head, Chest, Legs, Feet, ...) | Новый |
| `Assets/_Project/Skills/SkillsServer.cs` | NetworkBehaviour, Learn/Forget RPCs | Mirror QuestServer |
| `Assets/_Project/Skills/SkillsWorld.cs` | POCO singleton | Mirror QuestWorld |
| `Assets/_Project/Skills/SkillsClientState.cs` | singleton | Mirror QuestClientState |
| `Assets/_Project/Skills/Dto/SkillSnapshotDto.cs` | INetworkSerializable | Mirror QuestSnapshotDto |
| `Assets/_Project/Skills/Dto/SkillNodeDto.cs` | struct | Mirror QuestDto |
| `Assets/_Project/Skills/Dto/SkillEffect.cs` | struct + enum SkillEffectType | Mirror DialogueAction |

### 2.2 SO файлы (новые)

| Файл | Назначение |
|------|------------|
| `Assets/_Project/Scripts/Stats/StatGrowthConfig.cs` | геом. формула роста, глобальный множитель, per-stat multipliers |
| `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | extends `ItemData`, fields: slot, statBonuses, requiredSkills |
| `Assets/_Project/Scripts/Equipment/ModuleItemData.cs` | extends `ItemData`, fields: slot (Module1..3), statBonuses, requiredSkills |
| `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` | fields: id, name, category (Social/Combat), prerequisites[], effects[] |
| `Assets/_Project/Resources/Clothing/` (папка) | .asset конфиги (по 2-3 примера) |
| `Assets/_Project/Resources/Modules/` (папка) | .asset конфиги |
| `Assets/_Project/Resources/Skills/` (папка) | .asset конфиги (по 2-3 combat + 2-3 social) |

### 2.3 События WorldEventBus (новые)

| Event | Payload | Publisher | Where |
|-------|---------|-----------|-------|
| `MiningCompletedEvent` | PlayerId, ItemId, ItemName, Quantity, IsDepleted | GatheringServer | GatheringServer.cs:159 (success-ветка) |
| `CraftingCompletedEvent` | PlayerId, StationNetId, RecipeId, ResultItemName | CraftingServer | CraftingServer.cs:86-103 |
| `ExchangeCompletedEvent` | PlayerId, Op, ItemId, Quantity | ExchangeServer | ExchangeServer.cs:154, 214 |
| `MarketTradedEvent` | PlayerId, Op, ItemId, Quantity, NewCredits | MarketServer | MarketServer.cs:134, 150 |
| `QuestAcceptedEvent` | PlayerId, QuestId | QuestServer | QuestServer.cs:455 (Discovered→Active) |
| `QuestCompletedEvent` | PlayerId, QuestId | QuestServer | QuestServer.cs:455 (Active→Completed) |
| `ShipPilotTickEvent` | PlayerId, DeltaDistance | ShipController | ShipController.cs FixedUpdate |
| `PlayerJumpedEvent` | PlayerId | NetworkPlayer | новый SubmitJumpRpc |
| `PlayerRunStateChangedEvent` | PlayerId, IsRunning | NetworkPlayer | новый NetworkVariable<bool> IsRunning |

### 2.4 Минимальные изменения в существующих файлах

| Файл | Изменение | Строки |
|------|-----------|--------|
| `Assets/_Project/Core/WorldEvent.cs` | +9 новых event classes | ~150 |
| `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs` | +1 `WorldEventBus.Publish(new MiningCompletedEvent)` в success-ветке | ~5 |
| `Assets/_Project/Scripts/Crafting/CraftingServer.cs` | +1 `WorldEventBus.Publish(new CraftingCompletedEvent)` в success-ветке | ~5 |
| `Assets/_Project/Trade/Exchange/Network/ExchangeServer.cs` | +2 (Pack + Unpack success-ветки) | ~10 |
| `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` | +2 (Buy + Sell success-ветки) | ~10 |
| `Assets/_Project/Quests/Network/QuestServer.cs` | +2 `WorldEventBus.Publish(new QuestAcceptedEvent/CompletedEvent)` | ~10 |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | +1 `SubmitJumpRpc`, +1 `NetworkVariable<bool> IsRunning`, +2 WorldEvent publish | ~30 |
| `Assets/_Project/Scripts/Player/ShipController.cs` | +1 distance accumulator в `FixedUpdate` (server), +1 publish | ~15 |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | +400 строк (sub-tabs + 4 row-factory + SubscribeX/UnsubscribeX) | ~400 |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | +progression-section + 4 sub-sections + 4 sub-tabs + stat-row-progress | ~80 |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | +6 новых классов (`.stat-row-progress`, `.skill-row-*`, `.stat-progress-fill.tier-*`) | ~60 |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | +CreateStatsClientState() + CreateEquipmentClientState() + CreateSkillsClientState() | ~30 |
| `Assets/_Project/Scenes/BootstrapScene.unity` | +3 GameObjects: `[StatsServer]`, `[EquipmentServer]`, `[SkillsServer]` | — |

### 2.5 Что НЕ трогаем (явные запреты)

| Файл | Почему |
|------|--------|
| `Assets/_Project/Scripts/Player/PlayerController.cs` | LEGACY, отключён в `NetworkPlayer.cs:188` |
| `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` | LEGACY, не подключён |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | events объявлены, но никто не подписывается (dead code) |
| `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` | Editor-only (`#if UNITY_EDITOR`), использовать в runtime нельзя |
| `Assets/_Project/Quests/Editor/QuestGraphView.cs` | Editor-only |
| `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset` | уже настроен, не трогаем |
| `Assets/_Project/Scripts/UI/Client/InventoryUI.cs` | GTA-wheel на Tab, не путать с CharacterWindow |

---

## 3. Существующие подписки-паттерны для копирования

### 3.1 Server-side: WorldEventBus subscribe в QuestServer

`Assets/_Project/Quests/Network/QuestServer.cs:92-98`:
```csharp
// В OnNetworkSpawn:
_handleItemAdded = OnItemAdded;
WorldEventBus.Subscribe<ItemAddedEvent>(_handleItemAdded);
_handleItemRemoved = OnItemRemoved;
WorldEventBus.Subscribe<ItemRemovedEvent>(_handleItemRemoved);
// ... ещё 5 событий ...

// В OnNetworkDespawn:
WorldEventBus.Unsubscribe<ItemAddedEvent>(_handleItemAdded);
// ... mirror
```

### 3.2 Client-side: Subscribe/Unsubscribe pattern в CharacterWindow

`Assets/_Project/Scripts/UI/Client/CharacterWindow.cs:268-330`:
```csharp
private bool _isXSubscribed = false;

private void SubscribeX() {
    if (_isXSubscribed) return;
    var state = XClientState.Instance;
    if (state == null) return;
    state.OnXUpdated += HandleXSnapshot;
    _isXSubscribed = true;
}

private void UnsubscribeX() {
    if (!_isXSubscribed) return;
    var state = XClientState.Instance;
    if (state == null) { _isXSubscribed = false; return; }
    state.OnXUpdated -= HandleXSnapshot;
    _isXSubscribed = false;
}

// В EnsureBuilt: SubscribeX()
// В OnDisable: UnsubscribeX()
// В Update: lazy-subscribe if !_isXSubscribed && XClientState.Instance != null
```

### 3.3 EnsureBuilt / SwitchTab pattern в CharacterWindow

`CharacterWindow.cs:397-630` (EnsureBuilt) и `:636-706` (SwitchTab) — добавляем новые секции через:
1. Найти элементы в UXML через `_root.Q<VisualElement>("name")`
2. Создать ListView с `makeItem`/`bindItem` callbacks
3. В `SwitchTab`: добавить `bool isX = tab == "x"` + toggle section visibility + actions visibility + filters visibility
4. В `EnsureBuilt`: добавить `MarkDirtyRepaint()` на ListView после `display:flex`

---

## 4. Риски и anti-patterns

1. **Race condition WorldEventBus subscribers** — если `StatsServer` подписан на `ItemAddedEvent` который публикуется до того как `StatsServer.OnNetworkSpawn` отработал → пропускаем события. Решение: реплицируем state при `OnNetworkSpawn` через прямой RPC, не только через bus.
2. **NPC-spam protection — clock skew** — `Time.realtimeSinceStartup` на server может дрейфовать если `NetworkTime` используется. Использовать `NetworkManager.ServerTime.Time` для консистентности.
3. **Дублирование источников истины** — Stats НЕ хранятся как `NetworkVariable` на NetworkPlayer (если бы — отдельные переменные бы рассинхронились). Храним в `StatsWorld` (server) + sync через periodic snapshot RPC.
4. **Skill tree prerequisites циклы** — A requires B, B requires A → бесконечный цикл. Валидировать в `OnValidate` SO (build warning) + в `SkillsServer.TryLearn` (runtime check).
5. **EquipmentData Dictionary vs FixedList** — NGO 2.x не сериализует `Dictionary<,>` через `INetworkSerializable`. Используем `FixedList64Bytes<EquipSlotToItemId>` или `byte[slotCount]` параллельные массивы (как `InventoryData` с 8 `List<int>`).
6. **CharacterWindow 9 tabs wrap** — `flex-wrap:wrap` в `.tabs` уже работает, но 2 ряда табов съедят chrome. Решение: вложенные sub-tabs под "ПРОГРЕССИЯ" (single top-level + 4 sub-tabs внутри секции).
7. **JsonUtility не сериализует Dictionary** — нужен parallel DTO с List полями (как `InventorySaveData` в `JsonInventoryRepository.cs:38-39`).
8. **PlayerInputReader dead code** — не подключён к NetworkPlayer. Не пытаемся использовать — пишем напрямую в `NetworkPlayer.Update`.
9. **Files Modified Risk** — WorldEvent.cs изменения могут повлиять на 6 серверов. Добавляем новые event classes, не трогаем существующие.
10. **Stat formula cap = false** — пользователь явно сказал "без капа по макс". Формула `base * pow(growth, tier)` без cap даёт асимптотический рост к бесконечности — нужно валидировать в `OnValidate` SO и в runtime.

---

## 5. Сводка

| Категория | Готово | Создать | Изменить | Запретить трогать |
|-----------|--------|---------|----------|-------------------|
| Серверные singletons | 0 | 5 (Stats, Equipment, Skills × World/Server/ClientState × 3) | 1 (NetworkManagerController) | — |
| SO | 3+ (ItemData, ResourceNodeConfig, ExchangeRateConfig) | 4 (StatsConfig, Clothing, Module, SkillNode) | 0 | — |
| DTO | 6+ (Inventory, Reputation, Contract, Quest, etc.) | 7+ (PlayerStats, StatsSnapshot, EquipmentSnapshot, SkillSnapshot, etc.) | 0 | — |
| Events (WorldEventBus) | 11 существующих | 9 новых | 1 файл (WorldEvent.cs +5 LOC на класс) | — |
| Существующие серверы | 6+ | 0 | 6 файлов (5 строк каждый, минимальные правки) | — |
| UI | CharacterWindow 6 табов | +1 таб +4 sub-tabs | 3 файла UXML/USS/cs | InventoryUI (Tab) |
| Persist | 3 repository | +1 (JsonCharacterDataRepository) | 0 | — |
| Editor tooling | ResourcesCsvImporter (5 файлов) | +1 CSV-блок для clothing/modules/skills | 1 файл | — |
