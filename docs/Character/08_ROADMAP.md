# ROADMAP — Character Progression: тикеты, порядок, риски

> **Цель:** распланировать имплементацию системы уникальности персонажа (Stats + Clothing + Modules + Skills) по сессиям. Каждая сессия — compile-clean, верифицируемый incremental progress.
> **База:** `docs/Character/00_README.md` (TL;DR), `docs/Character/02_V2_ARCHITECTURE.md` (v2 hub-pattern), `docs/Mining/ROADMAP.md` (канонический шаблон roadmap'а)
> **Обновлено 2026-06-17.** ✅ **Все 18 тикетов T-P01..T-P18 реализованы. Все milestone'и M1-M4 завершены.** Дизайн → код → отладка → финальный compile clean. Skills click handlers deferred до системы боя.

---

## §0 Что осталось / текущий open work

**TL;DR:** ✅ **18 тикетов реализованы. M1-M4 закрыты.** Skills click handlers deferred (Phase 2, после интеграции системы боя).

### Выполнено (реализовано, compile-clean, тестировано в Play Mode) [не найдено]

| # | Тикет | Milestone | Приоритет | Скоуп | Файлы |
|---|-------|-----------|-----------|-------|-------|
| 1 | **T-P01** — StatsConfig SO | M1 | 🔴 High (~1 ч) | 3 стата, геометрическая формула, 12 источников XP, глобальный множитель, per-stat множители | 1 .cs + 1 .asset |
| 2 | **T-P02** — PlayerStats + DTO | M1 | 🔴 High (~1 ч) | struct + INetworkSerializable + tier promotion logic | 2 .cs |
| 3 | **T-P03** — StatsWorld (POCO singleton) | M1 | 🔴 High (~1 ч) | Dictionary<ulong, PlayerStats>, GetOrCreate, SetStats, BuildSaveData | 1 .cs |
| 4 | **T-P04** — StatsClientState (singleton) | M1 | 🔴 High (~1 ч) | OnStatsUpdated event, OnStatTierUp event | 1 .cs |
| 5 | **T-P05** — StatsServer (RPC hub + WorldEventBus subscriptions) | M1 | 🔴 High (~3-4 ч) | subscribe to 9 events, FixedUpdate distance tracker, ApplyXp, RecomputeAndSendSnapshot | 1 .cs |
| 6 | **T-P06** — JsonCharacterDataRepository + NetworkManagerController | M1 | 🔴 High (~1.5-2 ч) | persistence file per clientId, auto-spawn client states, ReceiveStatsSnapshotTargetRpc | 2 .cs |
| 7 | **T-P07** — ClothingItemData + ModuleItemData SO | M2 | 🔴 High (~1.5 ч) | extends ItemData, slot, tier, statBonuses, requiredSkills | 2 .cs + 5+3 .asset |
| 8 | **T-P08** — EquipSlot enum + EquipmentData struct | M2 | 🔴 High (~1 ч) | enum (13 slots), struct + INetworkSerializable + SlotToIndex/IndexToSlot | 2 .cs |
| 9 | **T-P09** — EquipmentWorld + EquipmentServer | M2 | 🔴 High (~3 ч) | TryEquip/TryUnequip validation, RPC hub, snapshot sync | 2 .cs |
| 10 | **T-P10** — EquipmentClientState | M2 | 🟡 Med (~1 ч) | OnEquipmentUpdated event, OnEquipResult event | 1 .cs |
| 11 | **T-P11** — SkillNodeConfig SO + SkillEffect struct | M3 | 🔴 High (~2 ч) | category, prerequisites, effects, XP cost, INT tier req, OnValidate cycle detection | 2 .cs + 8 .asset |
| 12 | **T-P12** — SkillsConfig SO + SkillsWorld | M3 | 🔴 High (~2 ч) | defaultSkills, LoadAllSkills, GrantDefaultSkills, GetLearnedSkillIds | 2 .cs + 1 .asset |
| 13 | **T-P13** — SkillsServer + SkillsClientState | M3 | 🔴 High (~3 ч) | RequestLearnSkillRpc, TryLearnSkill validation, snapshot sync | 2 .cs |
| 14 | **T-P14** — Skill prerequisite-arrows UI в CharacterWindow | M3 | 🟡 Med (~2 ч) | MakeSkillRow/BindSkillRow, ListView для combat + social | 1 .cs (расширение) |
| 15 | **T-P15** — CharacterWindow: таб "ПРОГРЕССИЯ" + sub-tabs UXML/USS | M4 | 🔴 High (~1 ч) | добавить 1 top-level + 4 sub-tabs + sub-sections | 2 файла (UXML/USS) |
| 16 | **T-P16** — CharacterWindow.cs: stats UI (stat-row-progress) | M4 | 🔴 High (~1.5 ч) | stat-str/dex/int labels + fill bars + tier class | 1 .cs (расширение) |
| 17 | **T-P17** — CharacterWindow.cs: clothing + modules UI | M4 | 🟡 Med (~1.5 ч) | ListView + MakeEquipmentRow + BindClothingRow/BindModuleRow | 1 .cs (расширение) |
| 18 | **T-P18** — Scene placement + BootstrapScene integration | M4 | 🟡 Med (~0.5-1 ч) | GameObjects [StatsServer]/[EquipmentServer]/[SkillsServer] | 1 .unity |
| - | **T-P19** (Phase 2) — Painter2D skill tree view | M5+ | ⚪ Low | custom VisualElement + Painter2D, port QuestGraphView | 3+ файла |
| - | **T-P20** (Phase 2) — Drag-and-drop equip | M5+ | ⚪ Low | ItemInventory → Clothing slot | 1+ файла |
| - | **T-P21** (Phase 2) — Weapons integration (STR/DEX/INT weapon usage) | M5+ | ⚪ Low | требует weapon system (отдельная подсистема) | — |

### DEFERRED (post-MVP, не блокер)

| # | Причина |
|---|---------|
| 1 | **Painter2D skill tree** — Phase 2, после MVP-успеха |
| 2 | **Drag-and-drop equip** — Phase 2, UX polish |
| 3 | **RequestForgetSkill** — skills permanent в MVP |
| 4 | **Tier-up notification visual feedback** — toast/flash — Phase 2 |
| 5 | **Inventory integration** — InventoryItemId → ClothingItemData direct cast для inline equip |
| 6 | **Stat-total tracker** — суммарные значения за всё время (не только currentXp в tier) |
| 7 | **Module power consumption** — поле есть, не используется (нет ship power system) |
| 8 | **Seasonal reset** — XP/equipment не сбрасываются |
| 9 | **Anti-cheat** — trusted dedicated server, нет client-side validation |
| 10 | **Save format migration** — добавление слотов = ломает совместимость |

---

## §1 Принципы разбивки

- **Один тикет = одна сессия = ~30-120 мин кодинга** (по объёму)
- **Каждый тикет компилируется и не ломает существующее** (compile-clean increment)
- **Тестирование — пользователь** (юзер запускает Unity, проверяет в Editor/PlayMode)
- **Без `.meta`/`.asmdef` writes** (см. AGENTS.md HARD RULES)
- **Additive-only** — не удалять существующий код, добавлять рядом (кроме явного cleanup)
- **Stub-forward-dep pattern** (pitfall #30 в design-doc-session skill): forward-declare stub типы в тикетах которые их требуют, чтобы compilation проходила

### Применимые паттерны из существующих подсистем

| Паттерн | Источник | Применение в Character Progression |
|---------|----------|-------------------------------------|
| `WorldEventBus.Subscribe<T>` в `OnNetworkSpawn` | `QuestServer.cs:92-98` | T-P05 (StatsServer), T-P09 (EquipmentServer не подписывается, но публикует), T-P13 (SkillsServer) |
| POCO singleton с Dictionary + CRUD | `InventoryWorld.cs:36-77` | T-P03 (StatsWorld), T-P12 (SkillsWorld), EquipmentWorld в T-P09 |
| TargetRPC через NetworkPlayer | `QuestTracker.cs`, `InventoryClientState.cs` | T-P06, T-P10, T-P13 |
| ClientState singleton с OnSnapshotUpdated event | `ReputationClientState.cs` | T-P04 (StatsClientState), T-P10, SkillsClientState в T-P13 |
| JsonUtility persistence с parallel DTO | `JsonInventoryRepository.cs:38-39` | T-P06 (JsonCharacterDataRepository) |
| ScriptableObject с `[Header]`/Tooltip/Range | `ResourceNodeConfig.cs` | T-P01 (StatsConfig), T-P07, T-P11, T-P12 |
| `SubscribeX/UnsubscribeX` + lazy-subscribe в CharacterWindow | `CharacterWindow.cs:268-330, 1412-1463` | T-P14, T-P15-T-P17 |
| `SwitchTab` pattern + 4 FIX'а | `CharacterWindow.cs:636-706, 397-630` | T-P15 |
| INetworkSerializable struct + parallel arrays | `InventoryData.cs` | T-P02, T-P08 (EquipmentData) |
| Scene-placed NetworkBehaviour в BootstrapScene | `GatheringServer`, `CraftingServer` etc. | T-P05, T-P09, T-P13 (server GameObjects), T-P18 |
| `Auto-spawn` client state via NetworkManagerController | `CreateInventoryClientState/CreateMetaRequirementClientState` | T-P06, T-P10, T-P13 |
| Rate limit на RPC | `QuestServer.cs`, `ExchangeServer.cs` | T-P05, T-P09, T-P13 |
| UI Toolkit 4 FIX'а + MarkDirtyRepaint после display:flex | `CharacterWindow.cs:418, 1991, 2014, 2036, 624-627` | T-P15-T-P17 |

---

## §2 Финальный порядок тикетов (с зависимостями)

```
T-P01 (StatsConfig SO)
   ↓
T-P02 (PlayerStats struct + DTO) ─→ T-P03 (StatsWorld) ─→ T-P04 (StatsClientState) ─→ T-P05 (StatsServer)
                                                                                          ↓
                                                                                       T-P06 (Repository + NetworkPlayer + NMC)
   ↓
T-P07 (ClothingItemData + ModuleItemData SO) ─→ T-P08 (EquipSlot + EquipmentData) ─→ T-P09 (EquipmentWorld + EquipmentServer)
                                                                                       ↓
                                                                                    T-P10 (EquipmentClientState)
   ↓
T-P11 (SkillNodeConfig + SkillEffect) ─→ T-P12 (SkillsConfig + SkillsWorld) ─→ T-P13 (SkillsServer + SkillsClientState)
                                                                                  ↓
                                                                               T-P14 (Skill UI rows в CharacterWindow)
   ↓
T-P15 (UXML/USS для progression таба) ─→ T-P16 (CharacterWindow stats UI) ─→ T-P17 (CharacterWindow clothing/modules UI) ─→ T-P18 (Scene placement)
```

**Сводка по milestone'ам:**

| Milestone | Тикеты | Что работает после M_n |
|-----------|--------|------------------------|
| **M1 — Stats core** | T-P01..T-P06 | ✅ StatsConfig SO, PlayerStats struct, StatsServer подписан на WorldEventBus, FixedUpdate distance tracker, snapshots. В Console: `[StatsServer] OnNetworkSpawn`, после mine: `[StatsServer] Player X gained 1 XP (STR)`. |
| **M2 — Clothing & Modules** | T-P07..T-P10 | ✅ ClothingItemData/ModuleItemData, EquipSlot enum, EquipmentData struct, EquipmentServer.TryEquip/TryUnequip, EquipmentClientState. Unequip возвращает item в инвентарь. [НАДЕТЬ] кнопка в инвентаре. |
| **M3 — Skill Tree** | T-P11..T-P14 | ✅ SkillNodeConfig + SkillEffect, SkillsConfig + SkillsWorld, SkillsServer.RequestLearnSkill, SkillsClientState, skill rows в CharacterWindow (LOCKED/AVAILABLE/LEARNED). Click handlers deferred до системы боя. |
| **M4 — UI Integration** | T-P15..T-P18 | ✅ CharacterWindow: single-page ПЕРСОНАЖ layout (4 блока: Характеристики → Одежда → Модули → Навыки). Stat bars с effective value (base+equip). Scene placement [StatsServer]/[EquipmentServer]/[SkillsServer]. Inventory split layout (list + detail). |

---

## §3 Тикеты (детально)

### T-P01 — StatsConfig ScriptableObject (Phase 1, ~1 ч)

**Файл:** `Assets/_Project/Scripts/Stats/StatsConfig.cs`
**Namespace:** `ProjectC.Stats`
**CreateAssetMenu:** `"Project C/Stats/Stats Config"`

**Скоуп:**
- Поля по `03_DATA_MODEL.md §1.1`:
  - `_miningXpPerItem`, `_craftingXpPerItem`, `_exchangeXpPerOp`, `_marketXpPerOp`
  - `_questAcceptedXp`, `_questCompletedXp`, `_dialogXpPerVisit`
  - `_jumpXp`, `_walkXpPer10m`, `_pilotXpPer100m`
  - `_dialogXpCooldownSeconds = 60f`, `_walkDistanceXpThreshold = 10f`, `_pilotDistanceXpThreshold = 100f`
  - Per-stat mapping (10 полей `_miningTarget/_craftingTarget/...`)
  - `_globalMultiplier = 1f` `[Range(0.01f, 10f)]`
  - `_tierBaseXp = 100f`, `_tierGrowthRate = 1.5f`
  - Per-stat multipliers: `_strengthMultiplier`, `_dexterityMultiplier`, `_intelligenceMultiplier`
  - `_announceTierUp = true`
- Методы: `XpForNextTier(int)`, `ApplyGlobalMultiplier(float)`, `ApplyStatMultiplier(StatType, float)`, `GetStatFor(MiningXpSource)`, `GetBaseXp(MiningXpSource)`
- Тестовый .asset: `Assets/_Project/Resources/Stats/StatsConfig_Default.asset` с default values

**Verify:** Compile 0 errors. Создать .asset через `Assets → Create → Project C → Stats → Stats Config`. Инспектор показывает все поля.

**Risk:** low. Чистый ScriptableObject, нет runtime логики.

---

### T-P02 — PlayerStats struct + StatsSnapshotDto (Phase 1, ~1 ч)

**Файлы:**
- `Assets/_Project/Scripts/Stats/PlayerStats.cs` — struct (3 floats + 3 tiers + 3 totals)
- `Assets/_Project/Scripts/Stats/Dto/StatsSnapshotDto.cs` — INetworkSerializable struct (12 fields)

**Скоуп:**
- `enum StatType : byte { Strength = 0, Dexterity = 1, Intelligence = 2 }` (отдельный файл или в PlayerStats.cs)
- `PlayerStats` struct:
  - `float strength, dexterity, intelligence` (current XP в текущем тире)
  - `int strengthTier, dexterityTier, intelligenceTier`
  - `float strengthTotalXp, dexterityTotalXp, intelligenceTotalXp` (cumulative)
  - `static PlayerStats Default`
- `StatsSnapshotDto` struct:
  - Все 12 полей из PlayerStats + `strengthXpForNextTier`, `dexterityXpForNextTier`, `intelligenceXpForNextTier` (computed для UI progress bar)
  - `NetworkSerialize<T>` (12 SerializeValue calls)
- Helper: `static class PlayerStatsRef` с `ref float GetXpRef(ref PlayerStats, StatType)`, `ref int GetTierRef(ref PlayerStats, StatType)`, `ref float GetTotalXpRef(ref PlayerStats, StatType)` — для избежания копипаста в T-P05

**Verify:** Compile 0 errors. Сериализация round-trip test (manual в Unity Inspector — не нужен unit test для MVP).

**Risk:** low. Чистый struct + DTO.

---

### T-P03 — StatsWorld POCO singleton (Phase 1, ~1 ч)

**Файл:** `Assets/_Project/Scripts/Stats/StatsWorld.cs`
**Namespace:** `ProjectC.Stats`

**Скоуп:**
- `public static StatsWorld Instance { get; private set; }`
- `private Dictionary<ulong, PlayerStats> _stats = new();`
- `public PlayerStats GetOrCreateStats(ulong clientId)` — returns existing or new with `PlayerStats.Default`
- `public void SetStats(ulong clientId, PlayerStats stats)` — write
- `public IEnumerable<ulong> GetAllPlayerIds()`
- `public CharacterSaveData BuildSaveData(ulong clientId)` — load from NPC cooldowns (forward-decl, реализация в T-P06)
- `public void LoadPlayer(ulong clientId, CharacterSaveData data)` — load (forward-decl)
- `public void RemovePlayer(ulong clientId)` — disconnect cleanup

**Verify:** Compile 0 errors. После `OnNetworkSpawn` в любом сервере — `StatsWorld.Instance != null`.

**Risk:** low. POCO singleton, простая логика.

**Stub note:** В этом тикете `BuildSaveData`/`LoadPlayer` — forward-declare `CharacterSaveData` как partial class (реальное определение в T-P06). Это позволяет T-P05 компилироваться.

---

### T-P04 — StatsClientState singleton (Phase 1, ~1 ч)

**Файл:** `Assets/_Project/Scripts/Stats/StatsClientState.cs`
**Namespace:** `ProjectC.Stats`

**Скоуп:**
- `public static StatsClientState Instance { get; private set; }`
- `public StatsSnapshotDto? CurrentStats { get; private set; }`
- `public event Action<StatsSnapshotDto> OnStatsUpdated`
- `public event Action<StatType, int> OnStatTierUp`
- `public void OnStatsSnapshotReceived(StatsSnapshotDto snap)` — setter, fires events
- `Awake()` — `Instance = this`
- `public void RequestLearnSkill(string skillId)` — stub for T-P13 (calls NetworkPlayer → SkillsServer RPC, через null-safe)

**Verify:** Compile 0 errors. После CreateStatsClientState (T-P06) — `StatsClientState.Instance != null`.

**Risk:** low. Копия `ReputationClientState.cs`.

---

### T-P05 — StatsServer (NetworkBehaviour + WorldEventBus subscriptions) (Phase 1, ~3-4 ч)

**Файл:** `Assets/_Project/Scripts/Stats/StatsServer.cs`
**Namespace:** `ProjectC.Stats`
**Scene-placed:** BootstrapScene (next to other servers)

**Скоуп:**
- `public class StatsServer : NetworkBehaviour`
- `public static StatsServer Instance`
- `OnNetworkSpawn` / `OnNetworkDespawn` lifecycle
- Subscribe to 9 events:
  - `MiningCompletedEvent`, `CraftingCompletedEvent`, `ExchangeCompletedEvent`, `MarketTradedEvent`
  - `QuestAcceptedEvent`, `QuestCompletedEvent`
  - `DialogVisitedEvent`, `ShipPilotTickEvent`, `PlayerJumpedEvent`
- `private Dictionary<ulong, Vector3> _lastPosPerPlayer` — distance tracker state
- `private Dictionary<ulong, float> _walkedDistanceBuffer` — accumulator
- `private Dictionary<ulong, float> _pilotDistanceBuffer` — accumulator
- `private Dictionary<ulong, Dictionary<string, float>> _lastDialogPerPlayerNpc` — anti-spam
- `FixedUpdate()` — walk distance tracker (skip if `_inShip`)
- `private void ApplyXp(ulong clientId, StatType stat, float rawXp)` — central XP application
- Tier promotion loop внутри ApplyXp (per `04_STATS_PROGRESSION.md §1.4`)
- `WorldEventBus.Publish(new StatTierUpEvent { ... })` на promotion
- `private void SendSnapshotToOwner(ulong clientId)` — TargetRPC через NetworkPlayer
- 9 handler methods (delegated to ApplyXp с разными multipliers)
- `OnDialogVisited` — anti-spam cooldown check
- `OnShipPilotTick` — pilot distance accumulator
- `public void RecomputeAndSendSnapshot(ulong clientId)` — для EquipmentServer вызов после equip
- `public void ApplyXpDirect(ulong clientId, StatType stat, float xp)` — для SkillsServer (XP spend на learn)
- `CanGainDialogXp/MarkDialogXpGained` — anti-spam helpers

**Stub note (forward-dep):**
- В этом тикете создаём 9 event classes в `WorldEvent.cs` (если их нет) — это зависимость
- Если `WorldEvent.cs` ещё не имеет этих event classes — добавляем их в этот тикет (5-10 LOC на класс)

**Verify:** Compile 0 errors. Play mode: StartHost → `[StatsServer] OnNetworkSpawn` в Console. После mining: `[StatsServer] Player X gained 1 XP (STR)`.

**Risk:** medium. NetworkBehaviour + subscribe pattern + distance tracker. LockBox pattern (lazy-subscribe) не нужен — статический bus. Pitfall #30 (stub-forward-dep) — если SkillsServer/EquipmentServer ещё не созданы, references через null-safe calls (но в этом тикете они не нужны).

---

### T-P06 — JsonCharacterDataRepository + NetworkManagerController (Phase 1, ~1.5-2 ч)

**Файлы:**
- `Assets/_Project/Scripts/Stats/Persistence/ICharacterDataRepository.cs`
- `Assets/_Project/Scripts/Stats/Persistence/JsonCharacterDataRepository.cs`
- `Assets/_Project/Scripts/Stats/Persistence/CharacterSaveData.cs`
- `Assets/_Project/Scripts/Stats/Persistence/PlayerStatsSave.cs`
- `Assets/_Project/Scripts/Stats/Persistence/EquipmentSave.cs` (forward-declare для T-P07)
- `Assets/_Project/Scripts/Stats/Persistence/SkillsSave.cs` (forward-declare для T-P11)
- `Assets/_Project/Scripts/Stats/Persistence/NpcCooldownSave.cs`
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` (modify)

**Скоуп:**
- `CharacterSaveData` — `{ stats, equipment, skills }` (parallel DTO для JsonUtility)
- `ICharacterDataRepository` — `TryLoad(ulong, out CharacterSaveData)`, `Save(ulong, CharacterSaveData)`, `GetSavePath(ulong)`
- `JsonCharacterDataRepository` — file per clientId, atomic write (tmp + Move pattern из `JsonQuestStateRepository.cs:74-85`)
- `NetworkManagerController.cs` — добавить `CreateStatsClientState()` + auto-spawn `[StatsClientState]` GameObject (DontDestroyOnLoad)
- `NetworkPlayer.cs` — добавить `[Rpc(SendTo.Owner)] ReceiveStatsSnapshotTargetRpc(StatsSnapshotDto snap)` → `StatsClientState.Instance?.OnStatsSnapshotReceived(snap)`

**Verify:** Compile 0 errors. После StartHost: `[StatsClientState] Instance != null` в Console. Persistence test: kill server, restart, check file `character_<clientId>.json` exists.

**Risk:** medium. Persistence + NMC integration + NetworkPlayer modify. Atomic write — копия quest pattern.

---

### T-P07 — ClothingItemData + ModuleItemData SO (Phase 2, ~1.5 ч)

**Файлы:**
- `Assets/_Project/Scripts/Equipment/ClothingItemData.cs`
- `Assets/_Project/Scripts/Equipment/ModuleItemData.cs`
- `Assets/_Project/Resources/Items/Clothing/Clothing_*.asset` (5 штук)
- `Assets/_Project/Resources/Items/Modules/Module_*.asset` (3 штуки)

**Скоуп:**
- `ClothingItemData : ItemData` (по `03_DATA_MODEL.md §2.1`)
  - `public EquipSlot slot`
  - `public int tier [Range(1,10)]`
  - `public float strengthBonus, dexterityBonus, intelligenceBonus`
  - `public float strengthMultiplier, dexterityMultiplier, intelligenceMultiplier [Range(0,5)]`
  - `public SkillNodeConfig[] requiredSkills`
- `ModuleItemData : ItemData` (по `03_DATA_MODEL.md §3.1`)
  - `public EquipSlot slot = EquipSlot.Module1`
  - `public ModuleType moduleType` (Sensor/Processor/Weapon/Utility)
  - `public int tier [Range(1,10)]`
  - Per-type effects (sensorRangeBonus, craftingSpeedMultiplier, weaponDamageBonus, statBonuses)
  - `public SkillNodeConfig[] requiredSkills`
  - `public float powerConsumption` (future use)
- 8 .asset файлов (5 clothing + 3 modules) — примеры из `03_DATA_MODEL.md §2.3 + §3.2`

**Verify:** Compile 0 errors. Создать .asset через `Assets → Create → Project C/Equipment/Clothing/Module`. Инспектор показывает все поля + drag-drop SkillNodeConfig.

**Risk:** low. Чистый ScriptableObject.

**Stub note:** `EquipSlot` enum ещё не создан (T-P08). Forward-declare в этом тикете в `Assets/_Project/Scripts/Equipment/EquipSlot.cs` (10 LOC). Это позволяет ClothingItemData компилироваться.

---

### T-P08 — EquipSlot enum + EquipmentData struct (Phase 2, ~1 ч)

**Файлы:**
- `Assets/_Project/Scripts/Equipment/EquipSlot.cs` (enum)
- `Assets/_Project/Scripts/Equipment/EquipmentData.cs` (struct)

**Скоуп:**
- `enum EquipSlot : byte { None, Head, Chest, Legs, Feet, Back, Hands, Accessory1, Accessory2, WeaponMain, WeaponOff, Module1=20, Module2=21, Module3=22 }`
- `struct EquipmentData : INetworkSerializable`:
  - `public const int SLOT_COUNT = 13`
  - `public byte[] slotOccupied = new byte[SLOT_COUNT]`
  - `public int[] slotItemIds = new int[SLOT_COUNT]`
  - `static EquipmentData Empty`
  - `NetworkSerialize<T>` — 13 × 2 SerializeValue
  - `TryGetItemId(EquipSlot, out int)`, `SetItem(EquipSlot, int)`, `ClearSlot(EquipSlot)`
  - `EnumerateOccupiedSlots()` — для UI refresh
  - `static int SlotToIndex(EquipSlot)`, `static EquipSlot IndexToSlot(int)` — normalization
- `EquipmentSnapshotDto : INetworkSerializable`:
  - `public EquipmentData equip`
  - `public NetworkSerialize<T>` (delegate to EquipmentData)

**Verify:** Compile 0 errors. Round-trip serialization test (manual).

**Risk:** low. Чистый struct + enum + DTO.

---

### T-P09 — EquipmentWorld + EquipmentServer (Phase 2, ~3 ч)

**Файлы:**
- `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` (POCO singleton)
- `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` (NetworkBehaviour, scene-placed)
- `Assets/_Project/Scripts/Equipment/Dto/EquipResultDto.cs`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (modify — добавить RPCs)

**Скоуп:**
- `EquipmentWorld` singleton — `Dictionary<ulong, EquipmentData> _perPlayer`
  - `TryEquip(ulong, int itemId, EquipSlot slot, out string reason)` — 6-step validation
  - `TryUnequip(ulong, EquipSlot slot, out string reason)`
  - `GetEquipment(ulong)`, `SetEquipment(ulong, EquipmentData)`
  - `BuildSaveData(ulong)` / `LoadPlayer(ulong, CharacterSaveData)` (EquipmentSave part)
- `EquipmentServer` — RPC hub:
  - `[Rpc(SendTo.Server, RequireOwnership=true)] RequestEquipRpc(int itemId, EquipSlot slot)`
  - `[Rpc(SendTo.Server, RequireOwnership=true)] RequestUnequipRpc(EquipSlot slot)`
  - `SendEquipResult/SendEquipmentSnapshotToOwner`
  - `OnNetworkSpawn/Despawn` + `OnClientConnected/Disconnected` for persistence
- `EquipResultDto : INetworkSerializable` — `byte code, int itemId, EquipSlot slot, string reason`
- Rate limit на RPC (5 ops/sec per client)
- `NetworkPlayer.cs`:
  - `[Rpc(SendTo.Owner)] ReceiveEquipmentSnapshotTargetRpc(EquipmentSnapshotDto snap)` → `EquipmentClientState.Instance?.OnEquipmentSnapshotReceived(snap)`
  - `[Rpc(SendTo.Owner)] ReceiveEquipResultTargetRpc(EquipResultDto result)` → `EquipmentClientState.Instance?.OnEquipResultReceived(result)`

**Stub note:** `EquipmentClientState` ещё не создан (T-P10). Forward-declare в этом тикете (`Assets/_Project/Scripts/Equipment/EquipmentClientState.cs` — 50 LOC stub с OnEquipmentUpdated event + 2 Receive methods, null-safe).

**Verify:** Compile 0 errors. Play mode: StartHost → RPC RequestEquipRpc → в Console `[EquipmentWorld] Player X equipped WorkerHelmet`. Snapshot received on client.

**Risk:** medium. RPC + validation + cross-NetworkObject dependencies (StatsServer, SkillsWorld, InventoryWorld). Если они не готовы — null-safe calls (return false в TryEquip если SkillsWorld.Instance == null).

---

### T-P10 — EquipmentClientState (Phase 2, ~1 ч)

**Файл:** `Assets/_Project/Scripts/Equipment/EquipmentClientState.cs` (расширение stub из T-P09)

**Скоуп:**
- Полная версия singleton (копия T-P04 stats, но для Equipment)
- `public EquipmentSnapshotDto? CurrentSnapshot`
- `public event Action<EquipmentSnapshotDto> OnEquipmentUpdated`
- `public event Action<EquipResultDto> OnEquipResult`
- `public void OnEquipmentSnapshotReceived(EquipmentSnapshotDto snap)`
- `public void OnEquipResultReceived(EquipResultDto result)`
- `public void RequestEquip(int itemId, EquipSlot slot)` — RPC helper
- `public void RequestUnequip(EquipSlot slot)` — RPC helper
- `NetworkManagerController.CreateEquipmentClientState()` auto-spawn

**Verify:** Compile 0 errors. Play mode: `EquipmentClientState.Instance != null`.

**Risk:** low. Копия StatsClientState.

---

### T-P11 — SkillNodeConfig + SkillEffect (Phase 3, ~2 ч)

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs`
- `Assets/_Project/Scripts/Skills/SkillEffect.cs` (struct + enum)
- `Assets/_Project/Scripts/Skills/SkillCategory.cs` (enum)
- `Assets/_Project/Resources/Skills/Skill_*.asset` (8 штук: 4 combat + 4 social)

**Скоуп:**
- `enum SkillCategory : byte { Social = 0, Combat = 1 }`
- `SkillNodeConfig` SO (по `06_SKILL_TREE.md §1.1`):
  - `public string skillId, displayName, description, icon`
  - `public SkillCategory category`
  - `public SkillNodeConfig[] prerequisites`
  - `public SkillEffect[] effects`
  - `[SerializeField, Min(0)] _learnXpCost = 50f`
  - `[SerializeField, Min(0)] _requiredIntelligenceTier = 0`
  - `public int treeX, treeY` (для future Painter2D view)
  - `OnValidate` — DFS cycle detection в SO (warning)
- `SkillEffect` struct + `enum Type { StatMod, AbilityUnlock, PassiveEffect }`
- 8 .asset (по `06_SKILL_TREE.md §1.3`):
  - Combat: BasicStrike (free, +2 STR), DodgeRoll (free, +3 DEX), HeavySwing (100 XP, +5 STR×1.2, requires BasicStrike, INT tier 2), PrecisionStrike (200 XP, +5 DEX×1.3, requires DodgeRoll+HeavySwing, INT tier 4)
  - Social: BasicTalk (free, +2 INT), Barter (100 XP, +3 INT×0.95, requires BasicTalk), Persuasion (100 XP, PassiveEffect "+10% dialog XP", requires BasicTalk), Leadership (200 XP, AbilityUnlock "recruit_npc", requires Barter+Persuasion, INT tier 4)

**Verify:** Compile 0 errors. Создать .asset, попробовать создать цикл (HeavySwing требует PrecisionStrike, PrecisionStrike требует HeavySwing) → OnValidate warning в Console.

**Risk:** low-medium. Cycle detection — тестировать вручную.

---

### T-P12 — SkillsConfig SO + SkillsWorld POCO (Phase 3, ~2 ч)

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillsConfig.cs`
- `Assets/_Project/Skills/SkillsWorld.cs`
- `Assets/_Project/Resources/Skills/SkillsConfig_Default.asset`

**Скоуп:**
- `SkillsConfig` SO:
  - `public SkillNodeConfig[] defaultSkills` (BasicStrike, DodgeRoll, BasicTalk)
  - `[SerializeField, Min(1)] _maxOpsPerSec = 5`
- `SkillsWorld` singleton:
  - `LoadAllSkills(SkillsConfig)` — `Resources.LoadAll<SkillNodeConfig>("Skills")` → `_skillsById` Dictionary
  - `GrantDefaultSkills(ulong, SkillsConfig)` — auto-grant default skills
  - `GetLearnedSkillIds(ulong)` — `HashSet<string>`
  - `TryLearnSkill(ulong, string skillId, out string reason)` — 5-step validation (по `06_SKILL_TREE.md §3.3`)
  - `BuildSnapshot(ulong)` → `SkillsSnapshotDto`
  - `BuildSaveData/LoadPlayer` (SkillsSave part)

**Verify:** Compile 0 errors. После StartHost: `[SkillsWorld] Loaded 8 skills.` в Console.

**Risk:** low. Копия `InventoryWorld` + `QuestWorld` patterns.

---

### T-P13 — SkillsServer + SkillsClientState (Phase 3, ~3 ч)

**Файлы:**
- `Assets/_Project/Skills/SkillsServer.cs` (NetworkBehaviour, scene-placed)
- `Assets/_Project/Skills/SkillsClientState.cs` (singleton)
- `Assets/_Project/Skills/Dto/SkillsSnapshotDto.cs`
- `Assets/_Project/Skills/Dto/SkillResultDto.cs`
- `Assets/_Project/Player/NetworkPlayer.cs` (modify)

**Скоуп:**
- `SkillsServer`:
  - `OnNetworkSpawn` — `Instance = this`, `LoadAllSkills(config)`, `OnClientConnected += ...`
  - `OnClientConnected(ulong)` — `GrantDefaultSkills` + `SendSnapshotToOwner`
  - `[Rpc(SendTo.Server, RequireOwnership=true)] RequestLearnSkillRpc(string skillId)`
  - `SendSkillResult/SendSnapshotToOwner`
  - Recompute stats after learn: `StatsServer.Instance?.RecomputeAndSendSnapshot(clientId)`
- `SkillsClientState` — копия T-P10 (StatsClientState pattern)
- `SkillsSnapshotDto : INetworkSerializable` — `string[] learnedSkillIds`
- `SkillResultDto : INetworkSerializable` — `byte code, string skillId, string reason`
- `NetworkPlayer.cs`:
  - `ReceiveSkillsSnapshotTargetRpc(SkillsSnapshotDto snap)` → SkillsClientState
  - `ReceiveSkillResultTargetRpc(SkillResultDto result)` → SkillsClientState
- `NetworkManagerController.CreateSkillsClientState()` auto-spawn

**Verify:** Compile 0 errors. Play mode: StartHost → RPC RequestLearnSkillRpc("social_basic_talk") → в Console `[SkillsWorld] Player X learned skill 'Базовый разговор'`. Snapshot received on client.

**Risk:** medium. RPC + cross-NetworkObject dependency (StatsServer) — null-safe если ещё не готов.

---

### T-P14 — Skill UI rows в CharacterWindow (Phase 3, ~2 ч)

**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (расширение)

**Скоуп:**
- Добавить `_skillsCombatList`, `_skillsSocialList` ListView (placeholder refs — реальная инициализация в T-P15)
- Добавить `_skillsState` + `SubscribeSkills/UnsubscribeSkills`
- `private struct SkillRow { SkillId, DisplayName, Description, Category, State, XpCost, RequiredTier, Prerequisites }`
- `private List<SkillRow> _skillsCombatCache, _skillsSocialCache`
- `MakeSkillRow()` — VisualElement factory (по `06_SKILL_TREE.md §4.3`)
- `BindSkillRow(row, index, cache)` — bind state/title/cost/desc/prereq/button
- `RefreshSkillsCache(SkillsSnapshotDto snap)` — fill caches (load SkillNodeConfig from Resources)
- `RebuildSkillsListView()` — `itemsSource = cache; RefreshItems(); MarkDirtyRepaint();`

**Stub note:** `SkillsClientState` ещё не создан (T-P13). Forward-declare stub (50 LOC, null-safe). USS стили для skill-row-* — forward-declare в `CharacterWindow.uss` (можно в T-P15 сделать целиком, но skill-row-* нужен здесь).

**Verify:** Compile 0 errors. `Resources/Skills/` пустой → пустой list (acceptable для MVP).

**Risk:** low. Копия existing row-factory pattern.

---

### T-P15 — CharacterWindow UXML/USS для progression таба (Phase 4, ~1 ч)

**Файлы:**
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` (modify)
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` (modify)

**Скоуп:**
- UXML:
  - Добавить `<ui:Button name="tab-progression" text="ПРОГРЕССИЯ" class="tab-btn" />` в `<ui:VisualElement class="tabs">`
  - Добавить `<ui:VisualElement name="progression-section">` с 4 sub-sections:
    - `stats-sub-section` (3 stat-row-progress)
    - `clothing-sub-section` (ListView)
    - `modules-sub-section` (ListView)
    - `skills-sub-section` (2 ListView — combat + social)
  - Добавить `<ui:VisualElement name="progression-sub-tabs">` с 4 sub-tab buttons
- USS:
  - `.sub-tabs` / `.sub-tab-btn` / `.sub-tab-btn.active`
  - `.stat-row-progress` / `.stat-progress-bar` / `.stat-progress-fill` / `.stat-progress-fill.tier-*` / `.stat-tier-label`
  - `.skill-row` / `.skill-row-top` / `.skill-row-state` / `.skill-row-title` / `.skill-row-cost` / `.skill-row-desc` / `.skill-row-prereq` / `.skill-row-btn`
  - `.skill-row-locked` / `.skill-row-available` / `.skill-row-learned`
  - `.equip-slot-row` / `.equip-slot-name` / `.equip-slot-item` / `.equip-slot-bonuses` / `.equip-slot-btn`

**Verify:** Открыть Unity, открыть CharacterWindow.uxml в Inspector — видим новые elements. Play mode: нажать P → видим таб ПРОГРЕССИЯ + 4 sub-tabs.

**Risk:** low. Чисто визуальные изменения.

---

### T-P16 — CharacterWindow.cs: stats UI (Phase 4, ~1.5 ч)

**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (расширение)

**Скоуп:**
- Добавить fields:
  - `_tabProgression, _progressionSection, _progressionSubTabs`
  - `_tabStats, _tabClothing, _tabModules, _tabSkills`
  - `_statsSubSection, _clothingSubSection, _modulesSubSection, _skillsSubSection`
  - `_statStrTier/_statStrValue/_statStrFill` (и аналоги для DEX/INT)
- Расширить `EnsureBuilt` — Q<...>("...") для всех refs + wire `tab-progression.clicked += () => SwitchTab("progression")`
- Расширить `SwitchTab` — добавить `bool isProgression` + `_progressionSection.style.display` + `SwitchProgressionTab(_activeProgressionTab)`
- Добавить `SwitchProgressionTab(string tab)` — visibility для 4 sub-sections + active class highlight
- Добавить `SubscribeStats/UnsubscribeStats/HandleStatsSnapshot/RefreshStatsDisplay`
- Добавить `UpdateTierClass(VisualElement fill, int tier)` — переключает `tier-low/mid/high/master` class

**Verify:** Compile 0 errors. Play mode: P → таб "ПРОГРЕССИЯ" → "Статы" → видим 3 stat-row-progress (пока 0/100, после mining → 1/100).

**Risk:** low. Расширение существующего кода по pattern.

---

### T-P17 — CharacterWindow.cs: clothing + modules UI (Phase 4, ~1.5 ч)

**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (расширение)

**Скоуп:**
- Добавить fields:
  - `_clothingList, _modulesList` ListView
  - `_equipmentState` + `SubscribeEquipment/UnsubscribeEquipment`
- Wire sub-tabs:
  - `_tabClothing.clicked += () => SwitchProgressionTab("clothing")`
  - `_tabModules.clicked += () => SwitchProgressionTab("modules")`
- ListView factories:
  - `_clothingList.makeItem = MakeEquipmentRow; _clothingList.bindItem = BindClothingRow;`
  - `_modulesList.makeItem = MakeEquipmentRow; _modulesList.bindItem = BindModuleRow;`
  - `_clothingList.fixedItemHeight = 32; _modulesList.fixedItemHeight = 32;`
- `MakeEquipmentRow()` — VisualElement factory (slot/item/bonuses/btn)
- `BindClothingRow(row, index)` — bind slot/item/bonuses, wire `btn.clicked += () => EquipmentClientState.Instance?.RequestUnequip(entry.Slot)`
- `BindModuleRow` — аналогично
- `SubscribeEquipment` + `HandleEquipmentSnapshot(EquipmentSnapshotDto snap)` + `RefreshEquipmentCache(snap)` (по `05_CLOTHING_AND_MODULES.md §6.2`)
- `RebuildClothingListView/RebuildModulesListView` — itemsSource + RefreshItems + MarkDirtyRepaint

**Verify:** Compile 0 errors. Play mode: equip Helmet через RPC → открыть P → ПРОГРЕССИЯ → Одежда → видим "Head: WorkerHelmet +1 STR [СНЯТЬ]". Click [СНЯТЬ] → snapshot received → slot пустой.

**Risk:** low. Расширение существующего кода.

---

### T-P18 — Scene placement + BootstrapScene integration (Phase 4, ~0.5-1 ч)

**Файл:** `Assets/_Project/Scenes/BootstrapScene.unity` (modify через MCP)

**Скоуп:**
- Создать `[StatsServer]` GameObject в BootstrapScene (рядом с `[GatheringServer]`):
  - `NetworkObject` (scene-placed — будет spawn'ен `ScenePlacedObjectSpawner`)
  - `StatsServer` component с `StatsConfig` reference → `Resources/Stats/StatsConfig_Default.asset`
- Создать `[EquipmentServer]` GameObject:
  - `NetworkObject`
  - `EquipmentServer` component
- Создать `[SkillsServer]` GameObject:
  - `NetworkObject`
  - `SkillsServer` component с `SkillsConfig` reference → `Resources/Skills/SkillsConfig_Default.asset`

**Verify:** Открыть BootstrapScene → Hierarchy → видим 3 новых GameObjects. Play mode → StartHost → Console: `[StatsServer] OnNetworkSpawn`, `[EquipmentServer] OnNetworkSpawn`, `[SkillsServer] OnNetworkSpawn`.

**Risk:** low. Чисто scene-placement, паттерн существующих серверов.

**Pitfall (важно):** Scene-placed NetworkObject требует `ScenePlacedObjectSpawner` для auto-spawn (если `InScenePlacedSourceGlobalObjectIdHash == 0`). Spawner уже есть в BootstrapScene (см. AGENTS.md §scene-placed rule). Если новые GO не спавнятся — проверить spawner alive.

---

## §4 Milestones

| Milestone | Тикеты | Что работает после M_n |
|-----------|--------|------------------------|
| **M1 — Stats core** | T-P01..T-P06 | StatsConfig SO существует, PlayerStats struct + DTO, StatsWorld/ClientState/Server работают, WorldEventBus subscriptions активны, distance tracker считает walked/pilot distance, snapshots синхронизируются. Persistence save/load. После mining в Console: `[StatsServer] Player X gained 1 XP (STR)`. |
| **M2 — Clothing & Modules** | T-P07..T-P10 | ClothingItemData + ModuleItemData, EquipSlot enum + EquipmentData struct, EquipmentServer.TryEquip/TryUnequip, EquipmentClientState. После RPC RequestEquip: `[EquipmentWorld] Player X equipped Helmet`. Snapshot показывает equipped items в UI. |
| **M3 — Skill Tree** | T-P11..T-P14 | SkillNodeConfig + SkillEffect + 8 .asset, SkillsConfig + SkillsWorld, SkillsServer, SkillsClientState, skill rows в CharacterWindow. После RequestLearnSkillRpc: `[SkillsWorld] Player X learned BasicStrike`. UI показывает LOCKED/AVAILABLE/LEARNED states. |
| **M4 — UI Integration** | T-P15..T-P18 | Таб "ПРОГРЕССИЯ" в CharacterWindow, sub-tabs Статы/Одежда/Модули/Навыки, progress bars с tier colors, equip/unequip buttons, learn buttons. Scene placement — все 3 server GO в BootstrapScene. **Visual final**: открыть P → ПРОГРЕССИЯ → видим все 4 sub-таба с реальными данными. |

**Рекомендуемый темп:** 1-2 тикета за сессию.

---

## §5 Оценка общей трудоёмкости

| Milestone | Тикеты | ~Часов |
|-----------|--------|--------|
| M1 — Stats core | T-P01..T-P06 | ~7-10 ч |
| M2 — Clothing & Modules | T-P07..T-P10 | ~6.5 ч |
| M3 — Skill Tree | T-P11..T-P14 | ~9 ч |
| M4 — UI Integration | T-P15..T-P18 | ~4-5 ч |
| **TOTAL MVP** | **18 тикетов** | **~26.5-30.5 ч** |

**С учётом фикс-итераций (опыт Mining показывает +30-50% на неожиданные баги): ~35-45 ч, 10-15 сессий.**

Если соблюдать темп "1 тикет = 1 сессия = ~30-120 мин", получится ~10-15 сессий. Можно бандлить T-P02+T-P03+T-P04 в одну сессию (data model скелет), T-P15+T-P16 в одну (UI integration для stats), T-P17+T-P18 в одну (UI + scene placement).

---

## §6 Риски (прогноз)

| # | Риск | Статус | Митигация |
|---|------|--------|-----------|
| 1 | **WorldEventBus subscribers race (StatsServer.OnNetworkSpawn)** | 🟡 medium | `RecomputeAndSendSnapshot` + `SendSnapshotToOwner` в OnNetworkSpawn после Subscribe — double-safety (snapshot + deltas) |
| 2 | **NPC-spam protection clock skew** | 🟡 low | Используем `NetworkManager.ServerTime.Time` (double) не `Time.realtimeSinceStartup` |
| 3 | **StatsConfig._globalMultiplier = 0 (divide)** | 🟢 low | `[Range(0.01f, 10f)]` — минимум 0.01, не 0 |
| 4 | **EquipmentData Dictionary vs parallel arrays (NGO 2.x)** | 🟢 low | Fixed-size arrays byte[13] + int[13], SlotToIndex/IndexToSlot normalization |
| 5 | **Skill prerequisites цикл** | 🟡 medium | `OnValidate` SO DFS cycle detection (warning) + runtime visited set |
| 6 | **NetworkVariable vs snapshot RPC для stats** | 🟢 low | Snapshot RPC — 1 сообщение, не 9 NetworkVariable |
| 7 | **JsonUtility не сериализует Dictionary** | 🟢 low | Parallel `CharacterSaveData` DTO с List/byte[]/int[] (копия `JsonInventoryRepository`) |
| 8 | **CharacterWindow 9 tabs wrap** | 🟢 low | Nested sub-tabs под "ПРОГРЕССИЯ" — 7 top-level + 4 sub-tabs |
| 9 | **Atomic save vs crash** | 🟢 low | `tmp + Move` pattern из `JsonQuestStateRepository.cs:74-85` |
| 10 | **UI Toolkit 4 FIX'а для новых ListView** | 🟡 low | `MarkDirtyRepaint` после `display: flex`, `fixedItemHeight = 80` для skills, ReferenceEquals trick |
| 11 | **Scene-placed NetworkObject NRE** | 🟡 low | `ScenePlacedObjectSpawner` уже в BootstrapScene. Если NRE — проверить spawner alive |
| 12 | **Stub-forward-dep compile chain (T-P03, T-P05, T-P07, T-P10, T-P13, T-P14)** | 🟡 medium | Forward-declare stub types in earlier ticket — same public surface, expanded in own ticket |
| 13 | **StatsClientState.OnStatsUpdated spam (tier-up +5)** | 🟢 low | Throttle StatTierUpEvent (200ms min) + queue для остальных |
| 14 | **WalkedDistance buffer overflow на disconnect** | 🟢 low | MVP: < threshold loss acceptable (Phase 2 — save buffer) |
| 15 | **Multiple bootstrap scenes (24 streaming scenes)** | 🟢 low | Серверные singletons только в BootstrapScene — другие сцены не содержат их |
| 16 | **Resources.LoadAll<SkillNodeConfig> ordering** | 🟢 low | `skillId` string как ключ, не int — stable |
| 17 | **EquipmentClientState null в первые секунды** | 🟢 low | Lazy-subscribe pattern в CharacterWindow (уже работает для 5 других state'ов) |

---

## §7 Session Log (будущие записи)

> Каждая сессия кодинга добавляет запись сюда по шаблону:
>
> ### §7.X M<номер> — <суть> (<дата>)
> **Коммит(ы):** ...
> **Verify:**
> - ...
> **Key Lessons:**
> - ...

(Сейчас пусто — добавляется по мере имплементации.)

---

## §8 Сводный статус (1 строка)

**M1 (Stats core): ⬜ TODO → T-P01..T-P06** • **M2 (Clothing/Modules): ⬜ TODO → T-P07..T-P10** • **M3 (Skill tree): ⬜ TODO → T-P11..T-P14** • **M4 (UI integration): ⬜ TODO → T-P15..T-P18** • обновлено 2026-06-14
