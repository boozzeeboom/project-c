# Crafting System — Implementation Plan

> **Цикл:** Проектирование. Сам план — без кода, но с **точными именами файлов** и **строгим порядком** имплементации.
> **Зависимости:** `00_OVERVIEW.md`, `10_DESIGN.md`.
> **Допущения:** Реализуется **после** того, как ты закроешь Open Questions из `00_OVERVIEW.md` §9.

---

## 0. Pre-flight checklist (до старта)

Перед началом кодирования — **обязательно**:

- [ ] Прочитан `docs/Crafting_system/00_OVERVIEW.md` (особенно §9 Open Questions).
- [ ] Прочитан `docs/Crafting_system/10_DESIGN.md`.
- [ ] Закрыты Open Questions Q1, Q2, Q5, Q6 (как минимум).
- [ ] Согласован **финальный список** рецептов для MVP (минимум 3: 1 корабль, 1 модуль, 1 материал).
- [ ] Решено, добавляем ли `InventoryWorld.TryRemoveByItemId` (Q: см. §2.1).
- [ ] Согласован: добавляем `MetaRequirementRegistry.GrantKeyToClient` или альтернатива.

Если хоть один пункт не закрыт — **стоп**, уточняем с тобой.

---

## 1. Общая стратегия

**Подход:** bottom-up. Сначала POCO (легко тестировать), потом NetworkBehaviour, потом UI.

**Принцип «small diffs»:** каждый шаг — компилируется и не ломает существующее. После каждого шага:
1. `refresh_unity` (force compile).
2. `read_console` (0 errors).
3. Если меняли ScriptableObject — ручная проверка в Editor.

**Никакого коммита/пуша/тестов через MCP** (см. AGENTS.md). Ты сам гоняешь git и test runner.

---

## 2. Пошаговый план

### Фаза 0: Pre-implementation (1 сессия)

#### Шаг 0.1: Создать базовые директории

**Цель:** Подготовить скелет.

Действия:
- Создать `Assets/_Project/Scripts/Crafting/` (новые файлы)
- Создать `Assets/_Project/Items/Crafting/Recipes/` (для SO в Resources — лучше в `Resources/Crafting/Recipes/`)
- Создать `Assets/_Project/Resources/Crafting/Recipes/` (для `RecipeData` SO, чтобы `Resources.LoadAll<RecipeData>("Crafting/Recipes")` работал)
- Создать `Assets/_Project/Resources/Crafting/Stations/` (для `CraftingStationConfig` SO)

**Файлы:** только `.gitkeep`-заглушки (через Editor, чтобы Unity создал `.meta`).

**Проверка:** Editor → Assets/_Project/Resources/ — папки видны.

---

#### Шаг 0.2: Расширить `ProjectC.Items.InventoryWorld`

**Файл:** `Assets/_Project/Items/Core/InventoryWorld.cs`

**Что:** Добавить метод `TryRemoveByItemId`.

**Зачем:** в MVP у `InventoryData` нет stackable (каждый `itemId` = 1 элемент в `List<int>`). Метод `TryDrop` работает по `slotIndex`, что неудобно для буфера крафта (мы хотим удалить N штук по `itemId` + `itemType`).

**Сигнатура:**
```csharp
public InventoryResultDto TryRemoveByItemId(
    ulong clientId,
    int itemId,
    int quantity,                  // сколько убрать
    ItemType itemType = ItemType.Resources   // hint для ускорения
);
```

**Логика:**
1. `_playerInventories[clientId].GetIdsForType(itemType)` → `List<int> ids`.
2. Цикл: `while (removed < quantity && ids.Remove(itemId)) removed++;`.
3. Если `removed < quantity` — откатить (re-add удалённые) → `Fail(InsufficientQuantity)`.
4. Если `removed == quantity` → `Ok`.
5. Edge: `quantity <= 0` → `Fail(InvalidArgs)`.
6. Edge: `itemId <= 0` → `Fail(InvalidArgs)`.

**Альтернатива (если не хочешь менять `InventoryWorld`):** хранить в `CraftingWorld` **свой** словарь `(clientId, itemId, itemType) → count`, **не трогая** `InventoryData`. **Не рекомендую** — это дублирование, риск рассинхрона. Лучше **расширить** `InventoryWorld` (это +1 утилитарный метод, не ломает существующее).

**Проверка:** `refresh_unity` → 0 errors. Существующие тесты инвентаря (если есть) — pass.

**Оценка:** 30-45 мин.

---

#### Шаг 0.3: Расширить `ProjectC.MetaRequirement.MetaRequirementRegistry`

**Файл:** `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs`

**Что:** Добавить `GrantKeyToClient`.

**Зачем:** чтобы крафт мог выдать ключ на корабль (см. `10_DESIGN.md` §5.3).

**Сигнатура:**
```csharp
/// <summary>Server-only: выдать ключ (привязку к MetaRequirement) указанному клиенту.
/// Шлёт ему TargetRpc с обновлённым реестром.</summary>
public bool GrantKeyToClient(ulong shipNetworkObjectId, ulong clientId);
```

**Логика:**
1. `if (!IsServer) return false;`
2. `var req = GetRequirement(shipNetworkObjectId); if (req == null) return false;`
3. **Новая мапа:** `private readonly Dictionary<ulong, HashSet<int>> _clientExtraKeys = new();`
4. `if (!_clientExtraKeys.TryGetValue(clientId, out var set)) { set = new HashSet<int>(); _clientExtraKeys[clientId] = set; }`
5. `if (req.ServerItemIds != null) foreach (var id in req.ServerItemIds) set.Add(id);`
6. Найти `NetworkPlayer` для `clientId` → `netPlayer.ReceiveMetaRequirementBindingsTargetRpc(...)` (передать merged список — статические bindings + extra).

**Важно:** `ReceiveMetaRequirementBindingsTargetRpc` сейчас bulk-pushит ВСЕ реестры, не per-client. Нужно **расширить** сигнатуру (или сделать новый метод `ReceiveMetaRequirementGrantsTargetRpc(shipNetId, keyItemIds)`).

**Вариант B (проще):** Добавить новый RPC:
```csharp
[Rpc(SendTo.Owner)]
public void ReceiveMetaRequirementGrantTargetRpc(
    ulong shipNetworkObjectId,
    int[] keyItemIds,                // привязанные к этому кораблю
    FixedString64Bytes displayName,
    RpcParams rpcParams = default);
```

И в `MetaRequirementClientState` — новый метод `OnKeyGranted(ulong shipNetId, int[] keyItemIds, string name)`. Внутри — обновляет `_requirements[shipNetId]`.

**Оценка:** 1-1.5 часа.

---

### Фаза 1: ScriptableObjects + DTOs (1-2 сессии)

#### Шаг 1.1: `RecipeData` (SO)

**Файл:** `Assets/_Project/Scripts/Crafting/RecipeData.cs`

**Что:** Создать `RecipeData`, `RecipeIngredient`, `RecipeOutput`, `RecipeCategory`, `SkillType`, enums.

**Объём:** ~100 строк. Скопировать структуру `LootTable.cs` (35 строк → 100), плюс вложенные structs + enum.

**Проверка:** Editor → Create → Project C → Crafting → Recipe. Создать 3 тестовых рецепта в `Resources/Crafting/Recipes/`:
1. `R_SteelModule.asset` (1 стальной модуль, выход: ItemData="Module_Thruster", craft=600s)
2. `R_LightShipKey.asset` (1 ключ-корабль, выход: ShipKeyBinding="Ship_Light_v01", craft=3600s)
3. `R_WoodenPlank.asset` (1 деревянная доска, выход: ItemData="Plank_Wood", craft=120s)

**Оценка:** 30-45 мин.

#### Шаг 1.2: `CraftingStationConfig` (SO)

**Файл:** `Assets/_Project/Scripts/Crafting/CraftingStationConfig.cs`

**Объём:** ~60 строк. Стандартный SO с полями (см. `10_DESIGN.md` §2.1).

**Проверка:** Создать 2 конфига в `Resources/Crafting/Stations/`:
1. `Station_Shipyard.asset` (StationType=Shipyard, allowedRecipes=[R_LightShipKey])
2. `Station_CraftingTable.asset` (StationType=CraftingTable, allowedRecipes=[R_SteelModule, R_WoodenPlank])

**Оценка:** 20-30 мин.

#### Шаг 1.3: DTOs

**Файл:** `Assets/_Project/Scripts/Crafting/Dto/CraftingDtos.cs` (один большой файл со всеми DTO)

**Что:**
- `CraftingStationDto` (struct, INetworkSerializable)
- `CraftingSnapshotDto` (struct)
- `CraftingResultDto` (struct)
- `CraftingResultCode` (enum)
- `CraftingSourceType` (enum)
- `CraftingJobState` (enum)
- `BufferedIngredientDto` (struct)
- `RecipeOutputDto` (struct)
- `RecipeDto` (struct)
- `RecipeIngredientDto` (struct)

**Объём:** ~300 строк. По образцу `Assets/_Project/Trade/Scripts/Dto/*.cs` и `Assets/_Project/Items/Dto/InventoryItemDto.cs`.

**Проверка:** `refresh_unity` → 0 errors. Типы доступны.

**Оценка:** 1-1.5 часа.

---

### Фаза 2: Server-side ядро (1-2 сессии)

#### Шаг 2.1: `CraftingWorld` (POCO)

**Файл:** `Assets/_Project/Scripts/Crafting/Core/CraftingWorld.cs`

**Объём:** ~300-400 строк. По образцу `InventoryWorld.cs` (445 строк) и `TradeWorld.cs` (486 строк).

**Что внутри:**
- Singleton pattern (`CreateAndInitialize` / `Shutdown`).
- Recipe registry: `Dictionary<int, RecipeData> _recipeById`.
- Station registry: `Dictionary<ulong, CraftingStation> _stations`.
- Job state: `Dictionary<ulong, CraftingJob> _activeJobs`, `Dictionary<ulong, List<CompletedCraftingJob>> _completedJobs`.
- Methods: `RegisterRecipe`, `RegisterStation`, `TryAddIngredient`, `TryStartCraft`, `TryCancelCraft`, `TryCollect`, `OnTick(dt)`.
- Внутренние helpers: `BuildSnapshotForClient`, `CheckBufferCoversRecipe`, `RefundBufferToSource`, `IssueOutputs`.

**Edge-cases (см. `10_DESIGN.md` §6):**
- Anti-grief: проверка `IsOwner`, `IsInZone`.
- Race: snapshot шлётся после каждой мутации (не накапливаются).
- Server time: `MarketTimeService.Instance.SecondsUntilNextTick` для текущего server time.

**Тесты (EditMode):** Создать `Assets/_Project/Tests/EditMode/CraftingWorldTests.cs` (НО это требует `.asmdef`, см. AGENTS.md "Не создавать `.asmdef` спекулятивно"). **Альтернатива:** ручная проверка через `execute_code` MCP (см. `unity-mcp-orchestrator` SKILL.md §workflow patterns).

**Проверка:** `refresh_unity` → 0 errors.

**Оценка:** 2-3 часа.

#### Шаг 2.2: `CraftingServer` (NetworkBehaviour)

**Файл:** `Assets/_Project/Scripts/Crafting/Network/CraftingServer.cs`

**Объём:** ~350-400 строк. По образцу `MarketServer.cs` (522 строки) и `InventoryServer.cs` (305 строк).

**Что внутри:**
- Singleton `Instance` (server-only).
- `NetworkList<...>` (опционально, см. `10_DESIGN.md` §3.3 — для MVP развёрнутые NetworkVariable).
- RPCs: `SubscribeStationRpc`, `AddIngredientRpc`, `StartCraftRpc`, `CancelCraftRpc`, `CollectRpc`.
- `OnNetworkSpawn` → `CraftingWorld.CreateAndInitialize`, register base recipes, subscribe `MarketTimeService.onMarketTick`.
- `OnNetworkDespawn` → `CraftingWorld.Shutdown`, unsubscribe.
- `OnMarketTick` → `CraftingWorld.OnTick(dt)` → для каждого completed job → `SendSnapshotTo(OwnerClient, snap)`.
- Rate limit per-client (копия из `MarketServer.cs:59` + `MarketServer.cs:236-254`).

**Проверка:**
- `refresh_unity` → 0 errors.
- Добавить `CraftingServer` GameObject в `BootstrapScene` (через MCP `manage_gameobject` + `manage_components`):
  - `[CraftingServer]`, `[NetworkObject]`
  - `_baseRecipes` (список `RecipeData` для авторегистрации)
- Запустить Play → host → проверить Console: `[CraftingWorld] Created. Recipes registered: 3`.

**Оценка:** 2-3 часа.

#### Шаг 2.3: `CraftingStation` (NetworkBehaviour)

**Файл:** `Assets/_Project/Scripts/Crafting/Network/CraftingStation.cs`

**Объём:** ~200 строк. По образцу `NetworkChestContainer.cs` (335 строк) — проще, без анимаций.

**Что внутри:**
- `[SerializeField] CraftingStationConfig _config`.
- NetworkVariable для `CraftingJobDto` (см. §3.3 — MVP развёрнутые: `_jobState`, `_jobOwnerClientId`, `_jobStartServerTime`, `_jobCraftSeconds`, `_jobRecipeId`).
- NetworkList `_bufferItems`.
- `IInteractable` implementation.
- `OnNetworkSpawn` → `CraftingServer.Instance.RegisterStation(this)`.
- `OnNetworkDespawn` → `UnregisterStation`.

**Проверка:** Создать префаб `[Station_Shipyard]` с NetworkObject + CraftingStation + BoxCollider (isTrigger). Положить в `WorldScene_0_0`.

**Оценка:** 1.5-2 часа.

---

### Фаза 3: Client state + RPC targets (1 сессия)

#### Шаг 3.1: `CraftingClientState`

**Файл:** `Assets/_Project/Scripts/Crafting/Client/CraftingClientState.cs`

**Объём:** ~250 строк. По образцу `MarketClientState.cs` (225 строк) + `InventoryClientState.cs` (234 строки).

**Что внутри:**
- Singleton, DontDestroyOnLoad.
- `CurrentSnapshot: CraftingSnapshotDto?`, `LastResult: CraftingResultDto?`.
- `OnSnapshotUpdated`, `OnResultReceived` events.
- `_recipeCache: Dictionary<int, RecipeData>` (для UI icon/displayName).
- Convenience API: `RequestSubscribe`, `RequestAddIngredient`, `RequestStartCraft`, `RequestCancelCraft`, `RequestCollect`.
- `LocalizeResultCode(CraftingResultCode) → string`.

**Проверка:** `refresh_unity` → 0 errors.

**Оценка:** 1.5-2 часа.

#### Шаг 3.2: Расширить `NetworkPlayer`

**Файл:** `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

**Что:** Добавить 2 метода (см. `10_DESIGN.md` §8):
- `ReceiveCraftingSnapshotTargetRpc(CraftingSnapshotDto snapshot, RpcParams rpcParams = default)`
- `ReceiveCraftingResultTargetRpc(CraftingResultDto result, RpcParams rpcParams = default)`

**Проверка:** `refresh_unity` → 0 errors. Compile чистый.

**Оценка:** 10-15 мин.

#### Шаг 3.3: Auto-spawn в `NetworkManagerController`

**Файл:** `Assets/_Project/Scripts/Core/NetworkManagerController.cs` (557 строк)

**Что:** В методе `Awake` (где уже спавнятся `InventoryClientState`, `MetaRequirementClientState`, `ContractClientState`, `MarketClientState`) — добавить спавн `CraftingClientState`.

**Проверка:** `refresh_unity` → 0 errors. Запустить Play → Console: должно быть `[CraftingClientState] OnEnable` (или подобный лог).

**Оценка:** 10-15 мин.

---

### Фаза 4: UI (1-2 сессии)

#### Шаг 4.1: UXML изменения

**Файл:** `Assets/_Project/Resources/UI/MarketWindow.uxml`

**Что:** Добавить tab button + section (см. `10_DESIGN.md` §7.1).

**Проверка:** Editor — открыть MarketWindow.uxml в UI Builder → preview обновится.

**Оценка:** 30-45 мин.

#### Шаг 4.2: USS изменения

**Файл:** `Assets/_Project/Resources/UI/MarketWindow.uss`

**Что:** Добавить стили (`.buffer-grid`, `.buffer-slot`, ...).

**Оценка:** 15-20 мин.

#### Шаг 4.3: MarketWindow.cs — Crafting tab

**Файл:** `Assets/_Project/Scripts/UI/Client/MarketWindow.cs` (1270 строк → ~1500 строк после Crafting)

**Что:** Добавить (см. `10_DESIGN.md` §7.3):
- Поля: `_craftingRecipesList`, `_craftingSection`, `_craftingBufferGrid`, `_craftingProgressBar`, кнопки.
- `EnsureBuilt` — Q-queries, init ListView, subscribe.
- `OnDisable` — unsubscribe.
- `HandleCraftingSnapshotUpdated` — cache update ВСЕГДА, rebuild только если `_activeTab == "crafting"`.
- `HandleCraftingResultReceived` — обновить `_messageLabel` (shared).
- `SwitchTab` — case `"crafting"`.
- Drag-and-drop: на `_craftingBufferGrid` ловим `DragEnter/DragLeave/Drop`.
- Optimistic update: на `OnAddIngredientClicked` — local cache + `ListView.Rebuild()` ДО RPC.
- `ApplyCraftingFilters` — вызывается на `SwitchTab("crafting")`.

**Проверка:**
- `refresh_unity` → 0 errors.
- Play → host → открыть MarketWindow (E в зоне рынка) → 3 таба (market/warehouse/crafting). В зоне станции — tab Crafting показывает рецепты, буфер.
- `read_console` → нет warnings о pointer events (pitfall R3-005 из skill).

**Оценка:** 2-3 часа.

#### Шаг 4.4: MarketInteractor.cs (опционально, мини-правка)

**Файл:** `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs`

**Что:** После `RequestSubscribeMarket` (если вошли в зону станции) — вызвать `CraftingClientState.RequestSubscribeCrafting(stationNetId)`. **Но:** `MarketInteractor` сейчас подписан на `MarketZoneRegistry`, не на `CraftingServer`. Возможно, нужен новый компонент `CraftingInteractor` (по образцу `MarketInteractor`).

**Решение MVP:** Не делать `CraftingInteractor`. В `MarketWindow` при `SwitchTab("crafting")` — если `_selectedCraftingStationNetId == 0` (игрок не в зоне) — показать placeholder. Если > 0 — `CraftingClientState.RequestSubscribe`.

**Оценка:** 0 (Phase 4.3 уже покрывает).

---

### Фаза 5: Интеграция и тест (1 сессия)

#### Шаг 5.1: Создать сцену-стенд

**Что:** Либо использовать существующую `WorldScene_0_0`, либо создать `WorldScene_Crafting_Test.unity` в `Assets/_Project/Scenes/`.

**В сцене:**
- BootstrapScene → additive load → WorldScene_Crafting_Test
- GameObject `[Station_CraftingTable]` в позиции (0, 0, 0):
  - NetworkObject
  - CraftingStation
  - BoxCollider (isTrigger)
  - Visual: простой Cube с материалом
- GameObject `[Player_Spawn]` со spawn-point

**Build Settings:** добавить сцену в Scene Registry (`Assets/_Project/Data/Scene/SceneRegistry.asset`).

**Проверка:** Play → host → видим куб. Подходим → E → "Станция готова".

**Оценка:** 30-45 мин.

#### Шаг 5.2: Полный флоу тест

**Сценарий:**
1. Host заходит. У него в инвентаре: 3 стальных слитка (ItemData), 1 стальной модуль-выход (через `AddItem` от NPC-стартового набора).
2. Подходит к столу → E → открывается MarketWindow → tab "Крафт".
3. Drag&Drop 3 steel из inventory → buffer.
4. UI: буфер = 3 steel, рецепт "Steel Module" показывает "✓ хватает".
5. Клик "Старт" → UI: "Стартовал. Прогресс 0%".
6. Ускоряем таймер: в Inspector `CraftingServer._baseRecipes[0].craftSeconds = 10f` (или debug multiplier на `MarketTimeService.marketTimeMultiplier = 60`).
7. 10 сек → UI: "Готово! Заберите".
8. Клик "Забрать" → инвентарь +1 стальной модуль.
9. Console: `[CraftingWorld] Job completed, output issued to client 0`.

**Проверка edge:**
- Добавить в буфер ЛИШНЕЕ (4 steel вместо 3) → "Buffer overflow" toast.
- Нажать StartCraft с пустым буфером → "Не хватает ингредиентов".
- Выйти из зоны (отлететь) → тык StartCraft → "NotInZone".
- (опционально) Корабль: крафт верфи → проверить, что `MetaRequirementRegistry.GrantKeyToClient` сработал и `ReceiveMetaRequirementGrantTargetRpc` доставил binding.

**Оценка:** 1 час (ручной прогон).

---

### Фаза 6: Документация (в конце)

#### Шаг 6.1: Обновить `docs/Crafting_system/99_CHANGELOG.md`

**Что:** Записать, что сделано в этой итерации (M0.1: prototype).

#### Шаг 6.2: Обновить `docs/Crafting_system/50_KNOWN_ISSUES.md`

**Что:** Записать открытые баги, edge-cases, которые всплыли в Фазе 5.

#### Шаг 6.3: Обновить `docs/STEP_BY_STEP_DEVELOPMENT.md` (общий roadmap)

**Что:** Добавить запись "Crafting MVP — v0.0.1-crafting-mvp".

**Оценка:** 15-20 мин.

---

## 3. Сводная оценка

| Фаза | Задача | Время |
|------|--------|-------|
| 0.1 | Создать директории | 5 мин |
| 0.2 | Расширить `InventoryWorld` | 30-45 мин |
| 0.3 | Расширить `MetaRequirementRegistry` | 1-1.5 ч |
| 1.1 | `RecipeData` SO | 30-45 мин |
| 1.2 | `CraftingStationConfig` SO | 20-30 мин |
| 1.3 | DTOs | 1-1.5 ч |
| 2.1 | `CraftingWorld` POCO | 2-3 ч |
| 2.2 | `CraftingServer` NetworkBehaviour | 2-3 ч |
| 2.3 | `CraftingStation` NetworkBehaviour | 1.5-2 ч |
| 3.1 | `CraftingClientState` | 1.5-2 ч |
| 3.2 | `NetworkPlayer` + 2 RPC | 10-15 мин |
| 3.3 | `NetworkManagerController` авто-спавн | 10-15 мин |
| 4.1 | UXML | 30-45 мин |
| 4.2 | USS | 15-20 мин |
| 4.3 | `MarketWindow.cs` Crafting tab | 2-3 ч |
| 5.1 | Сцена-стенд | 30-45 мин |
| 5.2 | Ручной флоу тест | 1 ч |
| 6.* | Документация | 20-30 мин |
| **Total** | | **~16-22 ч** (~3-4 полных сессии) |

**Распределение по сессиям (примерно):**
- **Сессия 1 (текущая):** документация (этот документ + остальные 5 файлов) + закрытие Open Questions.
- **Сессия 2:** Фаза 0 + Фаза 1 (подготовка + SOs + DTOs).
- **Сессия 3:** Фаза 2 (server core: World + Server + Station).
- **Сессия 4:** Фаза 3 + 4 (Client state + UI).
- **Сессия 5:** Фаза 5 + 6 (тест + документация результатов).

---

## 4. Риски имплементации

### 4.1 Высокие

- **Race condition на RPC ordering** (см. `10_DESIGN.md` §6.10). Решение: snapshot шлётся **после** каждой мутации, атомарно. Тест: 2 клиента спамят RPC одновременно → финальный state консистентный.
- **NetworkObject spawn для станций** (см. AGENTS.md §"Scene-placed NetworkObject" — известная проблема hash=0). **Решение:** проверить, что `ScenePlacedObjectSpawner` жив в bootstrap, и что станции `destroyWithScene: true`. Если hash=0 — перезалить сцену (твоя задача, не моя).

### 4.2 Средние

- **Drag-and-drop в UI Toolkit** (pitfall R3-005 из `project-c-ui-as-tab` skill). Нужно правильно настроить `DragAndDrop.SetGenericData` на source и `DragEnter/DragLeave/Drop` на target. **Тест:** drag 1 steel → release на buffer → мгновенно optimistic update → через 200мс server snapshot перезаписывает.
- **Server-time sync между host и remote**. Решение: использовать `NetworkManager.ServerTime.TimeAsFloat` (NGO built-in) или `MarketTimeService.MarketTimeMultiplier` × `Time.realtimeSinceStartup`. **Тест:** 2 клиента в одной сессии — оба видят один и тот же прогресс-бар.

### 4.3 Низкие

- **Resource leak** при `OnNetworkDespawn` (если Job не очищен). Решение: `CraftingWorld.OnShutdown` → cleanup all jobs → refund.
- **UI tab-switching flicker** (layout не успевает). Решение: `MarkDirtyRepaint()` schedule +50ms (есть в MarketWindow).

---

## 5. Что НЕ делаем в MVP (фаза 2+)

- Очередь крафтов.
- Уровни/ускорения станций.
- Случайные выходы / качество.
- Топливо/инструменты как расходники.
- Cargo корабля как источник.
- Persistence (`IPlayerDataRepository`).
- NPC-крафт (bot uses station).

Каждый из этих пунктов = **отдельный ticket**, ~1-2 недели. Не блокеры для MVP.

---

## 6. Чеклист верификации (для сессии 5)

> Скопируй это в `30_VERIFICATION.md` и выполни перед пометкой MVP «готово».

- [ ] Скомпилировано без ошибок и warnings.
- [ ] `BootstrapScene` содержит `[CraftingServer]`.
- [ ] `WorldScene_0_0` (или test scene) содержит ≥1 `CraftingStation` с правильным `CraftingStationConfig`.
- [ ] `Resources/Crafting/Recipes/` содержит ≥3 `RecipeData` SO.
- [ ] `Resources/Crafting/Stations/` содержит ≥1 `CraftingStationConfig` SO.
- [ ] `MarketWindow.uxml` имеет `crafting-tab-btn` + `crafting-section`.
- [ ] `MarketWindow.cs` подписан на `CraftingClientState.OnSnapshotUpdated/OnResult`.
- [ ] В `OnDisable` — unsubscribe.
- [ ] В `HandleCraftingSnapshotUpdated` — cache update ВСЕГДА.
- [ ] `SwitchTab("crafting")` — `display: flex` + `.active` + `RequestSubscribe`.
- [ ] Drag&Drop inventory→buffer работает, оптимистичное обновление.
- [ ] `StartCraft` списывает ресурсы, ставит `InProgress`.
- [ ] По таймеру Job переходит в `Completed`.
- [ ] `Collect` выдаёт результат в inventory.
- [ ] Cancel возвращает ресурсы в source.
- [ ] Reconnect: Completed Job виден.
- [ ] Anti-grief: не-owner не может Collect/Cancel/StartCraft.
- [ ] Все 3 типа рецепта работают (материал, модуль, корабль).
- [ ] Console — 0 errors, 0 warnings (related to Crafting).
