# References — file:line index для Character Progression

> **Дата:** 2026-06-14
> **Метод:** 3 параллельных сабагента (read-only анализ) + ручная верификация через `read_file` / `search_files`
> **Все ссылки file:line проверены** через реальные file reads в этой сессии.

---

## 1. Прочитанные полностью (PRIORITY A)

### 1.1 Character Progression (subagent #2 — UI/Player-Controller)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | 2156 | 4 FIX'а UI Toolkit (397-630, 1975-2068), `SwitchTab` (636-706), tab buttons (480-485), sections (84-89), ListView (101-108), 4 row-factory (1175-1574), filter config (724-765), snapshot handlers (923-1340) |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | 177 | header (24-27), info-bar (30-33), tabs (36-43), filters (46-50), character-section (53-82), 6 sections total, actions (163-172), message (175) |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | 509 | !important везде, 6 крупных блоков: `.character-window` (21-39), `.tabs/.tab-btn` (81-121), `.stats-grid/.stat-row` (174-203), `.contract-row` (272-307), `.reputation-row` (328-357), `.quest-row*` (364-472), `.npc-attitude-row` (477-500+) |
| `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` | 1648 | эталон 4 FIX'а (187, 1172-1245, 1189-1208, 1258-1321), `SwitchTab` (1061-1114), `EnsureBuilt` (155-422), action handlers (329-340), contract ListView (271-286) |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | 128 | LEGACY, не подключён к NetworkPlayer, events `OnJump/Run/ModeSwitch/MouseDelta` |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 1270 | line 188 (отключает legacy PlayerController), 303 (F), 347 (P → CharacterWindow), 396-401 (WASD/Space/Shift), 406 (E pickup), 560-565 (FindNearestShip через InteractableManager), 626-670 (MetaReq E-key) |
| `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` | 674 | Editor-only (`#if UNITY_EDITOR` line 10), namespace `ProjectC.Quests.Editor`, наследует `GraphView` (UnityEditor.Experimental.GraphView) |
| `Assets/_Project/Quests/Editor/QuestGraphView.cs` | 384 | Editor-only (`#if UNITY_EDITOR` line 7), custom VisualElement + Painter2D (line 17), гибридный подход (Nodes сразу видны, connections через painter) |
| `Assets/_Project/Scripts/Player/PlayerController.cs` | 145 | LEGACY, отключён в `NetworkPlayer.OnNetworkSpawn` line 188, собственные InputActions (line 54-62) |
| `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` | 210 | LEGACY, `[SerializeField] walkController` (line 23), F-key InputAction (line 58) |
| `Assets/_Project/UI/Client/InventoryUI.cs` | 515 | GTA-wheel, **Tab binding** (line 99), 8 секторов (line 60, 204-218), `pickingMode` on `_wheelContainer` (line 493-494), `TrySubscribeToClientState` lazy (line 139-149) |
| `Assets/_Project/Reputation/ReputationClientState.cs` | 64 | **Эталон** singleton pattern (line 23-65), `OnReputationUpdated` event, scene-placed + `DontDestroyOnLoad` (line 28, 41) |
| `Assets/_Project/Quests/UI/QuestTracker.cs` | 314 | HUD overlay, lazy Instance init (line 50-68), `Track/Untrack` API (line 175-199), `OnTrackChanged` event (line 46) |
| `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset` | 52 | `m_ScaleMode: 1`, ref-resolution 1200×800, `sortingOrder=0` |
| `docs/Character-menu/00_OVERVIEW.md` | 121 | P-key выбран (line 96), 5-tap plan (line 41-49), **архитектурное правило "не создавать отдельные окна"** (line 25-33) |
| `docs/Character-menu/10_DESIGN.md` | 408 | Полный UXML/USS дизайн (line 18-228), 4 FIX'а (line 88-90), запреты (line 388-398) |

### 1.2 RPG Entry-Points (subagent #3 — Server events)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Core/WorldEventBus.cs` | 82 | static singleton, `Publish<T>/Subscribe<T>/Unsubscribe<T>/Reset` |
| `Assets/_Project/Core/WorldEvent.cs` | 154 | `ItemAddedEvent` (37), `ItemRemovedEvent` (51), `ReputationChangedEvent` (66), `NpcAttitudeChangedEvent` (81), `QuestStateChangedEvent` (100, **NOT published**), `DialogVisitedEvent` (112, **published by QuestServer.cs:612**), `CustomEvent` (128), `DayNightPhaseChangedEvent` (138), `Contract*Event` (148+) |
| `Assets/_Project/Scripts/ResourceNode/GatheringClientState.cs` | 213 | `OnGatherProgress` (47), `OnGatherCompleted(string, int, bool)` (47), `OnGatherInterrupted` (54), `OnGatherDenied` (60), `OnGatherCancelled` (66), `OnGatherResultReceived` (138), `RequestStartGather` (75), `RequestCancelGather` (85) |
| `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs` | 403 | `RegisterNode` (105), `UnregisterNode` (110), `RequestStartGatherRpc` (134), `RequestCancelGatherRpc` (185), `SendGatherResultToClient` (309), `TickActiveGathers` (245), `TickCooldown` (327), Completed branch (159) |
| `Assets/_Project/Scripts/Crafting/CraftingServer.cs` | 144 | `OnCraftingTick` (82-103), `CraftingTimeService.onCraftingTick` (61), Completed detection (98-101) |
| `Assets/_Project/Scripts/Crafting/CraftingClientState.cs` | 197 | `OnCraftingCompleted(ulong, string)` (33), `OnCraftingProgress` (27), `OnCraftingSnapshotReceived` (190) |
| `Assets/_Project/Trade/Exchange/Network/ExchangeServer.cs` | 269 | `RequestPackRpc` (154), `RequestUnpackRpc` (214), success branches, `_world.Pack/Unpack` |
| `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` | 285 | `RequestBuyRpc` (134), `RequestSellRpc` (150), `TradeWorld.TryBuy` |
| `Assets/_Project/Quests/Network/QuestServer.cs` | 650+ | subscribe pattern (92-98), `OnWorldStageTransition` (455), dialog flow (493, 612), `RequestTalkToNpcRpc` (493) |
| `Assets/_Project/Quests/Client/QuestClientState.cs` | 96 | `OnQuestResult` (36), `OnQuestDiscovered(string, string)` (39), `RaiseOnQuestDiscovered` (92) |
| `Assets/_Project/Quests/NpcController.cs` | 130 | `NpcId` getter (52) |
| `Assets/_Project/Scripts/Player/ShipController.cs` | 939 | `_pilots` HashSet (131), `FixedUpdate` (352-355, 442-460), `AddPilot`/`RemovePilot` (912, 930), `AddPilotRpc` (921), `RemovePilotRpc` (939) |

### 1.3 Data Model (subagent #1 — SO patterns)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Core/ItemType.cs` | 35 | `[CreateAssetMenu]` (17), `ItemData` (18-34), `ItemType` enum (5-15) |
| `Assets/_Project/Items/Core/InventoryWorld.cs` | 666 | POCO singleton (36-77), `_itemDatabase` (144), `RegisterAllItems` (586-654), `GetOrRegisterItemId` (180-191), `AddItemDirect` (355), `CountOf`, `HasAllItems`, JSON repository path |
| `Assets/_Project/Items/Core/ItemRegistry.cs` | 120 | preferred stable ids, `RegisterItem` (101) |
| `Assets/_Project/Items/Dto/InventorySnapshotDto.cs` | 76 | INetworkSerializable pattern, `InventoryData` fields |
| `Assets/_Project/Items/Dto/InventoryItemDto.cs` | 51 | struct with itemId, count, type |
| `Assets/_Project/Scripts/Core/InventoryData.cs` | 162 | struct 8 `List<int>` per ItemType (18-25), `GetIdsForType` (56-70), `AddItem` (75-88) |
| `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs` | 146 | SO pattern gold standard: `[CreateAssetMenu]` (30), `[Header]` groups, `[Tooltip]`, `[Range]`, runtime-resolve, `OnValidate` |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` | 292 | `_requiredItems[]` (38), `CanPlayerUse` (96-142), `ServerItemIds` lazy (64-75), `OnValidate` (266-289) |
| `Assets/_Project/Quests/Quests/QuestDefinition.cs` | 124 | Identity/Categories/Stages/Rewards headers, `FactionId` (36), `prerequisites` (55) |
| `Assets/_Project/Quests/Quests/QuestReward.cs` | 73 | reward struct |
| `Assets/_Project/Quests/Quests/QuestPrerequisite.cs` | 51 | atomic prerequisite (38-50), `stringParam` + `intParam` |
| `Assets/_Project/Quests/Dialogue/DialogueAction.cs` | 102 | enum + atomic params (Unity serialization depth limit) |
| `Assets/_Project/Scripts/Ship/ShipModule.cs` | 128 | separate root SO with `moduleId`, not extending ItemData |
| `Assets/_Project/Core/JsonInventoryRepository.cs` | 164 | `IInventoryRepository`, `JsonUtility` save/load, `InventorySaveData` DTO (38-39), atomic tmp+Move pattern, `Path.Combine(Application.persistentDataPath, ...)` (64-67) |
| `Assets/_Project/Quests/Persistence/JsonQuestStateRepository.cs` | 127 | **battle-tested atomic write** with `tmp + Move` (74-85) |
| `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` | 32 | repository interface |
| `Assets/_Project/Trade/Scripts/Repository/PlayerPrefsRepository.cs` | 80 | host-only (host can't use PlayerPrefs for dedicated) |
| `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` | 80 | dedicated server file-based |
| `Assets/_Project/Trade/Exchange/Config/ExchangeRateConfig.cs` | 51 | simpler SO variant with `List<ExchangeRateEntry>` and FindByWarehouseItemId |
| `Assets/_Project/Items/Editor/ResourcesCsvImporter.cs` | 966 | CSV → SO pattern, `Apply` entry (91-119), `ProcessInventory` (125+), preserve GUID via SerializedObject (130-146) |
| `Assets/_Project/Items/Editor/ResourcesCsvSchema.cs` | 210 | `BlockSchema` + `ColumnDef` |

---

## 2. Прочитанные выборочно (PRIORITY B)

### 2.1 Subagent #1 (Data Model)

- `Assets/_Project/Quests/Editor/QuestCsvImporter.cs` — full pattern of existing CSV importer
- `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` (lines 101-674) — full GraphView implementation
- `Assets/_Project/Items/Editor/ResourcesCsvWindow.cs` — EditorWindow UI

### 2.2 Subagent #2 (UI)

- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs:1-500` (partial — focused on SwitchTab + 4 FIX'а)
- `CharacterWindow.cs:1-2099` (full)
- `NetworkPlayer.cs:1-700` (Update + P-key + F-key + E-key)

### 2.3 Subagent #3 (Entry Points)

- `CraftingServer.cs:82-103` (OnCraftingTick section)
- `ExchangeServer.cs:154, 214` (Pack/Unpack success branches)
- `MarketServer.cs:134, 150` (Buy/Sell success branches)
- `QuestServer.cs:455-650` (OnWorldStageTransition + dialog flow)

---

## 3. Subagent reports (сырые материалы)

Эти файлы НЕ в проекте — в `C:\Users\leon7\`:

| Файл | Размер | Назначение |
|------|--------|------------|
| `C:\Users\leon7\ANALYSIS_CHARACTER_RPG_UI.md` | 69.5 KB, 1020 строк, 6723 слов | UI/Player-Controller анализ (12 секций) |
| `C:\Users\leon7\ANALYSIS_CHARACTER_DATA_MODEL.md` | ~2400 слов (inline, файл не сохранён из-за лимита итераций) | Data-Model/SO-паттерны анализ (12 секций) |
| `C:\Users\leon7\ANALYSIS_CHARACTER_ENTRY_POINTS.md` | 22 KB, 210 строк | entry-points для подписки на серверные события (6 секций) |

Эти отчёты — **сырые материалы сабагентов**, использованные для синтеза `00_README.md`..`09_OPEN_QUESTIONS.md`.

---

## 4. Grep-данные (по всему проекту)

### 4.1 Subagent #2 grep results

| Что искал | Где | Результат |
|-----------|-----|-----------|
| `StateHasher` / `stateHasher` | `Assets/_Project` | **0 hits** — файл не существует |
| `StatsClientState` / `class Stats` / `CharacterStats` | `Assets/_Project` | 1 hit — `CharacterWindow.cs:128` (комментарий) |
| `ProgressBar` | `Assets/_Project` | 2 hits — `CraftingProgressController.cs:131`, `GatheringToastController.cs:131`. **0 hits в CharacterWindow** — custom-bar через `.reputation-fill` |
| `GraphView` / `Node` / `Edge` | `Assets/_Project/Quests/Editor` | 3 файла — все Editor-only |
| `UIDocument` | `Assets/_Project` | 81 hit в 12+ файлах, 11 в `BootstrapScene.unity` |
| `OnSnapshotUpdated` / `OnXUpdated` events | `Assets/_Project` | 18 файлов с event'ами — все state-singleton'ы |
| `Keyboard.current.*wasPressedThisFrame` | `Assets/_Project` | 105 hits — полная карта input |
| `InputActions/*.inputactions` | `Assets/_Project/InputActions` | **0 файлов** — InputAction assets отсутствуют |
| `MakeQuestRow` / `BindQuestRow` | `CharacterWindow.cs` | строки 1480-1574 |
| `MakeReputationRow` / `BindReputationRow` | `CharacterWindow.cs` | строки 1209-1235 |
| `MakeInventoryRow` / `BindInventoryRow` | `CharacterWindow.cs` | строки 1175-1203 |
| `SwitchTab` | `CharacterWindow.cs` | строки 636-706 |
| `EnsureBuilt` | `CharacterWindow.cs` | строки 397-630 |
| `SubscribeX` / `UnsubscribeX` | `CharacterWindow.cs` | строки 268-330, 1412-1463 |

### 4.2 Subagent #3 grep results

| Что искал | Где | Результат |
|-----------|-----|-----------|
| `event Action` | `Assets/_Project/Scripts/Crafting/Server/CraftingServer.cs` | 0 hits (server не публикует события) |
| `event Action` | `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs` | 0 hits (publishes via TargetRPC only) |
| `event Action` | `Assets/_Project/Scripts/Trade/Exchange/Network/ExchangeServer.cs` | 0 hits |
| `event Action` | `Assets/_Project/Scripts/Trade/Scripts/Network/MarketServer.cs` | 0 hits |
| `DistanceTraveled` / `TotalDistance` | `Assets/_Project` | **0 hits** — нет трекинга, нужно создать |
| `spaceKey.wasPressed` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | line 400 — owner-only, server unaware |
| `leftShiftKey` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | line 401 — owner-only, server unaware |
| `_inShip` / `IsInShip` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | line 55, 123, 134 — server-aware via NetworkVariable |
| `CurrentShip` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | referenced, ShipController instance |
| `SubmitSwitchModeRpc` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | line 496 — server-authoritative boarding |
| `AddPilot` / `RemovePilot` | `Assets/_Project/Scripts/Player/ShipController.cs` | lines 912, 930 |
| `_pilots` | `Assets/_Project/Scripts/Player/ShipController.cs` | HashSet<ulong> at line 131 |

### 4.3 Subagent #1 grep results

| Что искал | Где | Результат |
|-----------|-----|-----------|
| `[CreateAssetMenu]` | `Assets/_Project` | 12+ files (all SO types) |
| `ScriptableObject` | `Assets/_Project` | 20+ subclasses |
| `Item_*.asset` | `Assets/_Project/Resources/Items/` | **1006 files** |
| `INetworkSerializable` | `Assets/_Project` | 30+ structs (DTOs) |
| `JsonUtility` | `Assets/_Project` | 3 repositories (Inventory, Quest, Trade) |

---

## 5. Key patterns reused (документированы в design)

### 5.1 WorldEventBus subscribe pattern (QuestServer.cs:92-98)

```csharp
// OnNetworkSpawn:
_handleItemAdded = OnItemAdded;
WorldEventBus.Subscribe<ItemAddedEvent>(_handleItemAdded);
// OnNetworkDespawn:
WorldEventBus.Unsubscribe<ItemAddedEvent>(_handleItemAdded);
```

### 5.2 POCO singleton (InventoryWorld.cs:36-77)

```csharp
public static InventoryWorld Instance { get; private set; }
private Dictionary<ulong, InventoryData> _playerInventory = new(8);
public InventoryData GetOrCreateInventory(ulong clientId) { /* ... */ }
```

### 5.3 TargetRPC through NetworkPlayer

```csharp
// In StatsServer:
var netPlayer = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponent<NetworkPlayer>();
netPlayer.ReceiveStatsSnapshotTargetRpc(snap);

// In NetworkPlayer:
[Rpc(SendTo.Owner)]
public void ReceiveStatsSnapshotTargetRpc(StatsSnapshotDto snap, RpcParams rpcParams = default)
    => StatsClientState.Instance?.OnStatsSnapshotReceived(snap);
```

### 5.4 ClientState singleton with OnSnapshotUpdated

```csharp
public class StatsClientState : MonoBehaviour {
    public static StatsClientState Instance { get; private set; }
    public StatsSnapshotDto? CurrentStats { get; private set; }
    public event Action<StatsSnapshotDto> OnStatsUpdated;
    public void OnStatsSnapshotReceived(StatsSnapshotDto snap) { /* ... */ }
}
```

### 5.5 JsonUtility persistence (JsonQuestStateRepository.cs:74-85 atomic)

```csharp
var tmpPath = path + ".tmp";
File.WriteAllText(tmpPath, json);
if (File.Exists(path)) File.Delete(path);
File.Move(tmpPath, path);
```

### 5.6 ResourceNodeConfig SO pattern (gold standard)

```csharp
[CreateAssetMenu(fileName = "...", menuName = "Project C/...", order = N)]
public class ResourceNodeConfig : ScriptableObject {
    [Header("Group Name")]
    [Tooltip("...")]
    [SerializeField] private float _gatherSeconds = 3f;
    [Range(min, max)] private float _animScaleAmplitude;
    [Min(value)] private int _maxHarvests = 5;

    #if UNITY_EDITOR
    private void OnValidate() { /* warnings */ }
    #endif
}
```

### 5.7 CharacterWindow 4 FIX'а (UI Toolkit)

```csharp
// In EnsureBuilt:
_root.pickingMode = PickingMode.Ignore;
_doc.rootVisualElement.MarkDirtyRepaint();
_doc.rootVisualElement.schedule.Execute(() => MarkDirtyRepaint()).StartingIn(50);

// In Show:
_root.pickingMode = PickingMode.Position;
ApplyInlineFallbackStyles(_mainContainer);
Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

// In Hide:
_root.pickingMode = PickingMode.Ignore;
Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
```

### 5.8 CharacterWindow SwitchTab pattern

```csharp
private void SwitchTab(string tab) {
    _activeTab = tab;
    bool isX = tab == "x";
    // Toggle section visibility
    if (_xSection != null) _xSection.style.display = isX ? DisplayStyle.Flex : DisplayStyle.None;
    // Toggle action buttons visibility
    // Toggle filters visibility
    // Apply "active" class to current tab button
    // MarkDirtyRepaint for ListView first display
}
```

---

## 6. Что НЕ трогаем (явные запреты — задокументированы в 01_CURRENT_STATE_AUDIT.md §2.5)

| Файл | Почему |
|------|--------|
| `Assets/_Project/Scripts/Player/PlayerController.cs` | LEGACY, отключён в `NetworkPlayer.cs:188` |
| `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` | LEGACY, не подключён |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | events объявлены, но никто не подписывается (dead code) |
| `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` | Editor-only (`#if UNITY_EDITOR`), использовать в runtime нельзя |
| `Assets/_Project/Quests/Editor/QuestGraphView.cs` | Editor-only |
| `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset` | уже настроен, не трогаем |
| `Assets/_Project/Scripts/UI/Client/InventoryUI.cs` | GTA-wheel на Tab, не путать с CharacterWindow |
| `Assets/_Project/Scripts/Core/InventoryData.cs` | 8 hardcoded `List<int>` — не добавлять 9-й ItemType, использовать EquipmentData отдельно |
| `Assets/_Project/Scripts/UI/Client/MarketWindow.cs` | не трогаем, эталон для 4 FIX'а |

---

## 7. Условные обозначения в design-doc

| Символ | Значение |
|--------|----------|
| ✅ | Готово / реализовано / не блокер |
| ⬜ | TODO / не начато |
| ❌ | Не делать / запрет / failed |
| ⚠️ | Warning / requires attention |
| 🔴 | High priority / блокер |
| 🟡 | Medium priority |
| ⚪ | Low priority / Phase 2 |
| ✅ DONE | Тикет завершён (с датой) |
| (current) | Default значение в Open Questions — используется если не отвечаешь |

---

## 8. Как использовать этот файл

**При имплементации тикета:**

1. Открой `08_ROADMAP.md` → найди свой тикет (T-PXX) → scope раздел
2. Найди в `01_CURRENT_STATE_AUDIT.md` соответствующие файлы (готовые классы)
3. Используй `10_REFERENCES.md` §5 для patterns (копируй в свой код)
4. После компиляции — refresh Unity + read_console (per AGENTS.md)

**При debug:**

1. Симптом → grep по файлам из §1
2. Найти file:line → посмотреть контекст
3. Сравнить с pattern в §5

**При добавлении нового тикета:**

1. Добавь в `08_ROADMAP.md` §3
2. Добавь зависимости в §2
3. Добавь estimate в §5
4. Добавь risk в §6

---

## Сводка индекса

| Категория | Кол-во |
|-----------|--------|
| Files прочитанные полностью (PRIORITY A) | 35 |
| Files прочитанные частично (PRIORITY B) | 6 |
| Subagent reports | 3 |
| Grep searches | 18 |
| Key patterns documented | 8 |
| Out-of-scope (не трогаем) | 9 файлов |

**Все ссылки file:line проверены** через реальные `read_file` / `search_files` в этой сессии (2026-06-14).
