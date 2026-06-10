# ROADMAP — Crafting System: тикеты, порядок, риски

> **Цель:** распланировать имплементацию крафт-системы по сессиям. Каждая сессия —
> compile-clean, верифицируемый incremental progress.
>
> **База:** `docs/Crafting_system/00_OVERVIEW.md` (архитектура), `docs/Crafting_system/10_DESIGN.md` (дизайн),
> `ANALYSIS_REUSE_2026-06-10.md` (актуализация после Mining).
>
> **Обновлено 2026-06-10.** Дизайн-решения из `00_OVERVIEW.md` §5 (ADR-style) сохранены.
> Актуализирован REUSE-список — 8 из 16 компонентов берём из готовых Gathering/MetaRequirement вместо Market.
>
> **Ключевые изменения против оригинального дизайна (2026-06-07):**
> - Tool check → **MetaRequirement** (как Mining), не InventoryWorld.CountOf
> - Выдача корабля → **ключ-предмет в инвентарь** (уже есть Item_Key_Ship*)
> - UI → **отдельный UIDocument** (как DialogWindow), не tab в MarketWindow
> - `InventoryWorld.TryRemoveByItemId` → **не нужно** (use `InventoryServer.TryRemove`)
>
> Полный анализ: `ANALYSIS_REUSE_2026-06-10.md`

---

## §0 Что осталось / текущий open work

> **TL;DR:** крафт-система — новая, не реализована. 7 тикетов, ~8-11 ч чистого кода.
> После Фазы 1 можно будет подойти к станции → положить ресурсы → запустить крафт → таймер → забрать результат.

### Открыто (нужно делать)

| # | Тикет | Milestone | Приоритет | Скоуп | Фаза |
|---|-------|-----------|-----------|-------|------|
| 1 | **T-C01** — SO: `RecipeData` + `CraftingStationConfig` | M1 | 🔴 High (~1 ч) | ScriptableObjects: RecipeData (ingredients[], outputs[], craftSeconds), StationConfig (allowedRecipes[], displayName) | 1 |
| 2 | **T-C02** — `CraftingWorld` POCO + DTOs | M1 | 🔴 High (~1.5 ч) | POCO world singleton, `CraftingJob` state machine, 6 DTO structs (INetworkSerializable), `CraftingTimeService` | 2 |
| 3 | **T-C03** — `CraftingServer` RPC hub | M2 | 🔴 High (~1 ч) | NetworkBehaviour singleton, 5 RPCs (Subscribe/AddIngredient/Start/Cancel/Collect), rate-limit, tick handler | 3 |
| 4 | **T-C04** — `CraftingStation` NetworkBehaviour | M2 | 🔴 High (~2 ч) | Scene-placed station, state machine, MetaRequirement tool check, trigger zone (IInteractable), NetworkVariable state | 4 |
| 5 | **T-C05** — `CraftingClientState` + CraftingProgress toast | M3 | 🔴 High (~1.5 ч) | Client singleton (events), ProgressBar toast (копия GatheringToastController), auto-spawn NMC | 5 |
| 6 | **T-C06** — `CraftingWindow` отдельный UIDocument | M3 | 🟡 Med (~2 ч) | Отдельный UIDocument (как DialogWindow), Recipe selector, Buffer slots, Start/Cancel/Collect кнопки | 6 |
| 7 | **T-C07** — Scene placement: станция в WorldScene_0_0 | M3 | 🟢 Low (~0.5 ч) | Prefab + CraftingStation в WorldScene_0_0 рядом с Mira + [CraftingServer] в BootstrapScene | 7 |

### DEFERRED (post-MVP, не блокер)

| # | Тикет | Причина |
|---|-------|---------|
| 1 | **Drag-and-drop UI** | MVP: кнопки `+1`/`+All`. Drag-and-drop — Phase 2. |
| 2 | **Multi-source (Warehouse)** | Warehouse требует Market-зоны. Phase 2. |
| 3 | **Cargo → станция** | CargoSystem не NetworkBehaviour. Phase 3. |
| 4 | **Очередь крафтов (3 подряд)** | Сейчас 1 станция = 1 активный заказ. |
| 5 | **Tool durability** | Инструменты в крафте не расходуются. |
| 6 | **Recipe quality / random output** | Детерминированные рецепты в MVP. |
| 7 | **Skill gate (requiredSkillLevel)** | Задел в RecipeData на Phase 2. |
| 8 | **Persistence (PlayerPrefs)** | Job не переживает рестарт сервера. |

---

## §1 Принципы разбивки

- **Один тикет = одна сессия = ~30-120 мин кодинга** (по объёму).
- **Каждый тикет компилируется и не ломает существующее.**
- **Тестирование — пользователь** (юзер запускает Unity, проверяет в Editor/PlayMode).
- **Без `.meta`/`.asmdef` writes** (см. AGENTS.md HARD RULES).
- **Cleanup-тикеты отдельно от feature-тикетов** (не смешивать).
- **Pitfall-листы:**
  - `10_DESIGN.md` §3 — edge-cases (оригинал).
  - Настоящий документ §6 — риски по фиксам.
- **Additive-only** — не удалять существующий код, добавлять рядом (кроме явного cleanup).

---

## §2 Финальный порядок тикетов (с зависимостями)

```
T-C01 (RecipeData + StationConfig SO)
   ↓
T-C02 (CraftingWorld + DTOs + CraftingTimeService)
   ↓
T-C03 (CraftingServer RPC hub)
   ↓
T-C04 (CraftingStation NetworkBehaviour + MetaReq tool check)
   ↓
T-C05 (CraftingClientState + CraftingProgress toast)
   ↓
T-C06 (CraftingWindow отдельный UIDocument)
   ↓
T-C07 (Prefab + BootstrapScene + WorldScene placement)
```

**Сводка по милстоунам:**

| Milestone | Тикеты | Суть |
|-----------|--------|------|
| **M1 — Data + World** | T-C01, T-C02 | SO существуют, RecipeData → int mapping, CraftingJob state machine без сети, DTOs сериализуются |
| **M2 — Server + Station** | T-C03, T-C04 | CraftingServer принимает RPC, CraftingStation спавнится, проверяет tool, тикает таймер, выдаёт результат |
| **M3 — UI + Deployment** | T-C05, T-C06, T-C07 | Интерфейс крафта (выбор рецепта, буфер, прогресс), станция в сцене, end-to-end проверка |

---

## §3 Тикеты (детально)

### T-C01 — RecipeData + CraftingStationConfig SO (Phase 1, ~1 ч)

**Файлы:**
- `Assets/_Project/Scripts/Crafting/RecipeData.cs`
- `Assets/_Project/Scripts/Crafting/CraftingStationConfig.cs`

**RecipeData.cs:**
- `[CreateAssetMenu(menuName = "Project C/Crafting/Recipe")]`
- Поля: `displayName`, `icon`, `description`, `category` (Module/Consumable/Ship/Material)
- `ingredients: List<RecipeIngredient>` (ItemData + quantity)
- `outputs: List<RecipeOutput>` (ItemData + quantity, XOR ShipKeyBinding)
- `craftSeconds: float` (600f = 10 мин по умолчанию)
- `requiredSkillLevel: int = 0` (задел на Phase 2)
- struct `RecipeIngredient` / `RecipeOutput` (Serializable)
- `ResolveItemIds()` — как ResourceNodeConfig

**CraftingStationConfig.cs:**
- `[CreateAssetMenu(menuName = "Project C/Crafting/Station Config")]`
- Поля: `displayName`, `icon`, `stationType` (Shipyard/CraftingTable/Forge/Loom), `allowedRecipes: List<RecipeData>`, `interactRadius = 4f`

**Тестовые .asset:**
- `Resources/Crafting/Recipes/Recipe_CopperIngot.asset` — 3 медной руды → 1 медный слиток, 10 сек (быстрый для теста)
- `Resources/Crafting/Recipes/Recipe_IronIngot.asset` — 3 железной руды → 1 железный слиток, 10 сек
- `Resources/Crafting/Recipes/Recipe_ShipKeyLight.asset` — 1 слиток + 1 крист. пыль → 1 ключ ShipLight, 30 сек
- `Resources/Crafting/Stations/Station_CraftingTable.asset` — станция с 3 рецептами
- `Resources/Crafting/Stations/Station_Shipyard.asset` — станция с ShipKeyLight рецептом

**Verify:** ✅ Создать .asset через `Assets/Create/Project C/Crafting/...`. Inspector показывает все поля. `ResolveItemIds()` отрабатывает.

**Паттерн:** копия `ResourceNodeConfig.cs` (T-G01).

**Risk:** low.

---

### T-C02 — CraftingWorld POCO + DTOs + CraftingTimeService (Phase 2, ~1.5 ч)

**Файл:** `Assets/_Project/Scripts/Crafting/CraftingWorld.cs` (+ `/Dto/*`)

**CraftingWorld:**
- `static CraftingWorld Instance`
- `RegisterRecipe(RecipeData) → int` (маппинг, как InventoryWorld._itemDatabase)
- `GetRecipe(int recipeId) → RecipeData`
- `RegisterStation/UnregisterStation(ulong netId, CraftingStation)`
- `TryAddIngredient(stationNetId, clientId, itemId, qty, source)` — валидация → buffer
- `TryStartCraft(stationNetId, clientId)` — buffer → committed
- `TryCancelCraft(stationNetId, clientId)` — возврат ресурсов
- `TryCollect(stationNetId, clientId)` — выдача результата
- `OnTick(float dt)` — advance jobs, emit completions
- `CraftingJob` (POCO): stationNetId, ownerClientId, recipeId, state, startTime, buffer[], committed[]

**DTOs (INetworkSerializable, как GatherResult):**
- `CraftingStationDto` — stationNetId, displayName, stationType, allowedRecipeIds[]
- `CraftingSnapshotDto` — stationNetId, jobState, ownerClientId, startTime, craftSeconds, buffer[], committed[]
- `CraftingResultDto` — code, message, stationNetId
- `CraftingResultCode` — Ok/NotEnoughResources/StationBusy/NotOwner/NotFound/AlreadyStarted/NotStarted/AlreadyCompleted/InvalidArgs/InternalError
- `CraftingJobState` — Empty/Buffered/InProgress/Completed
- `CraftingSourceType` — Inventory

**CraftingTimeService:**
- Отдельный `MonoBehaviour` (не MarketTimeService, чтобы не склеивать таймеры)
- `baseTickIntervalSeconds = 1f` (для быстрых рецептов)
- Event `OnTick(float dt)` — подписывается `CraftingServer`
- Pattern — как `MarketTimeService` (уже работает)

**Verify:** ✅ EditMode тест: регистрация рецептов, TryAddIngredient → buffer, TryStart → InProgress, OnTick → Completed, TryCollect → Ok.

**Паттерн:** `CraftingJob` state machine из `ResourceNode.cs` (Idle/Occupied/Depleted → Empty/Buffered/InProgress/Completed).

**Risk:** medium. DTOs требуют `INetworkSerializable` с null-string writeback (P16d).

---

### T-C03 — CraftingServer RPC hub (Phase 3, ~1 ч)

**Файл:** `Assets/_Project/Scripts/Crafting/CraftingServer.cs`

**Скоуп:**
- `public static CraftingServer Instance`
- `[RequireComponent(typeof(NetworkObject))]`, scene-placed в BootstrapScene
- `[SerializeField] List<RecipeData> baseRecipes` — регистрируются при OnNetworkSpawn
- Rate limit (30 ops/min/client) — копия из GatheringServer

**RPCs (Client → Server):**
- `SubscribeStationRpc(ulong stationNetId)` — подписаться на snapshot станции
- `AddIngredientRpc(ulong stationNetId, int itemId, int quantity, CraftingSourceType source)` — положить в буфер
- `StartCraftRpc(ulong stationNetId)` — запустить крафт (только owner)
- `CancelCraftRpc(ulong stationNetId)` — отменить (только owner)
- `CollectRpc(ulong stationNetId)` — забрать готовое (только owner)

**Server lifecycle:**
- `OnNetworkSpawn` → `CraftingWorld.CreateAndInitialize()`, подписка на `CraftingTimeService.OnTick`
- `OnNetworkDespawn` → `CraftingWorld.Shutdown()`, отписка

**Senders (Server → Client TargetRPC):**
- `SendSnapshotTo(clientId, snapshot)` → `NetworkPlayer.ReceiveCraftingSnapshotTargetRpc`
- `SendResultTo(clientId, result)` → `NetworkPlayer.ReceiveCraftingResultTargetRpc`

**Verify:** ✅ StartHost → CraftingServer.Instance != null. RequestAddIngredientRpc → buffer. RequestStartCraftRpc → InProgress. CraftingTimeService.OnTick → Completed. CollectRpc → выдача.

**Паттерн:** копия `GatheringServer.cs` (T-G03) — RPC hub + rate-limit + FindNetworkPlayer + CheckDistance.

**Risk:** low. Полностью повторяет GatheringServer.

---

### T-C04 — CraftingStation NetworkBehaviour (Phase 4, ~2 ч)

**Файл:** `Assets/_Project/Scripts/Crafting/CraftingStation.cs`

**Скоуп:**
- `[RequireComponent(typeof(NetworkObject))]`, scene-placed в WorldScene_X_Z
- `[SerializeField] CraftingStationConfig _config`
- `[SerializeField] ProjectC.MetaRequirement.MetaRequirement _metaRequirement` (tool check, как ResourceNode)
- `NetworkVariable<CraftingJobState> _replicatedState` (Empty/Buffered/InProgress/Completed)
- `NetworkVariable<ulong> _jobOwnerClientId`
- `NetworkVariable<float> _jobStartTime`
- `NetworkVariable<float> _jobDuration`
- `NetworkVariable<int> _activeRecipeId` (= -1 если пусто)
- `NetworkList<BufferedIngredientDto> _buffer`
- `IInteractable` — `interactionRadius`, `TryInteract()` → открыть CraftingWindow
- `OnTriggerEnter/Exit` — регистрация зоны
- `OnNetworkSpawn` → `CraftingServer.RegisterStation(this)`
- `OnNetworkDespawn` → `CraftingServer.UnregisterStation(this)`

**Server-side методы:**
- `CanStartCraft(clientId, out reason)` — Idle + MetaReq check (как ResourceNode.CanStartGather)
- `TryStartCraft(clientId)` → InProgress
- `TryAddIngredient(clientId, itemId, qty, source)` → buffer
- `CancelCraft()` → Empty + возврат
- `CompleteCraft()` → Collect доступен

**Verify:** ✅ StartHost → CraftingStation.IsSpawned. `_replicatedState` читается на клиенте. TriggerEnter → InteractableManager.Register station.

**Паттерн:** копия `ResourceNode.cs` (T-G02) — NetworkBehaviour + state machine + MetaReq + trigger + NetworkVariable replication.

**Risk:** low-medium. 6 NetworkVariable на станцию — APi как у ResourceNode, проверено.

---

### T-C05 — CraftingClientState + CraftingProgress toast (Phase 5, ~1.5 ч)

**Файлы:**
- `Assets/_Project/Scripts/Crafting/CraftingClientState.cs`
- `Assets/_Project/Scripts/Crafting/CraftingProgressController.cs`

**CraftingClientState.cs:**
- Singleton (client-only), auto-spawn через `NetworkManagerController.CreateCraftingClientState()`
- `CurrentSnapshot: CraftingSnapshotDto?`
- Events: `OnCraftingProgress(float 0..1)`, `OnCraftingCompleted(string)`, `OnCraftingInterrupted(string)`, `OnCraftingDenied(string)`, `OnCraftingCancelled()`
- `RequestSubscribe(stationNetId)` / `RequestUnsubscribe()`
- `RequestAddIngredient(recipeId, itemId, qty, source)` / `RequestStart() / Cancel() / Collect()`
- Server timeout watcher (5s без InProgress → прервано)

**CraftingProgressController.cs:**
- UIDocument + ProgressBar (runtime-constructed, как GatheringToastController)
- Подписка на `CraftingClientState` events
- `OnProgress(p)` → ProgressBar.value = p
- `OnCompleted(itemName)` → "✅ Готово: Железный слиток"
- `OnInterrupted/Denied(reason)` → flash-fill + reason

**Verify:** ✅ Compile 0 errors. `CraftingClientState.Instance != null`. Toast показывает прогресс.

**Паттерн:** копия `GatheringClientState.cs` (T-G04) + `GatheringToastController.cs` (T-G04).

**Risk:** low (2 готовые копии).

---

### T-C06 — CraftingWindow отдельный UIDocument (Phase 6, ~2 ч)

**Файлы:**
- `Assets/_Project/Scripts/Crafting/UI/CraftingWindow.cs`
- `Assets/_Project/UI/Crafting/CraftingWindow.uxml`
- `Assets/_Project/UI/Crafting/CraftingWindow.uss`
- `Assets/_Project/UI/Crafting/CraftingPanelSettings.asset`

**UXML структура:**
```
<ui:UXML xmlns:ui="UnityEngine.UIElements" ...>
  <ui:VisualElement name="CraftingRoot">
    <ui:Label name="StationNameLabel" />
    <ui:ListView name="RecipeListView" />     <!-- слева: список рецептов -->
    <ui:VisualElement name="CenterPanel">
      <ui:Label name="RecipeDescription" />
      <ui:VisualElement name="BufferGrid">     <!-- 4-8 слотов -->
        <!-- динамически: слоты ингредиентов -->
      </ui:VisualElement>
      <ui:Button name="StartBtn" text="Начать крафт" />
      <ui:Button name="CancelBtn" text="Отменить" />
      <ui:Button name="CollectBtn" text="Забрать" />
      <ui:ProgressBar name="ProgressBar" />    <!-- если InProgress -->
    </ui:VisualElement>
    <ui:Label name="MessageLabel" />            <!-- ошибки/статус -->
  </ui:VisualElement>
</ui:UXML>
```

**CraftingWindow.cs:**
- Паттерн DialogWindow — `Show()/Close()`, cursor unlock/lock, pickingMode toggle
- `Initialize(ulong stationNetId, CraftingStationConfig config)` — загружает рецепты
- Recipe ListView: bindItem → иконка + displayName + craftSeconds
- BufferGrid: динамические VisualElement с иконками предметов
- Кнопки: Start → `CraftingClientState.RequestStart()`, Cancel → `RequestCancel()`, Collect → `RequestCollect()`
- MessageLabel: shared (как в MarketWindow)

**Без drag-and-drop в MVP:** кнопки `+1` / `+All` рядом с каждым ингредиентом в списке.

**PanelSettings:** копировать `ShipKeyPanelSettings.asset` (доказано работает).

**Verify:** ✅ E на станции → открывается окно. Recipe ListView показывает рецепты. Кнопки срабатывают. ProgressBar виден. ESC закрывает.

**Паттерн:** `DialogWindow.cs` (работает) + `ProjectC.UI.Client.MarketWindow` (Recipe ListView pattern).

**Risk:** medium. UI Toolkit. См. 18 persistent UI Toolkit bugs (Memory: T-Q11b session log).

---

### T-C07 — Scene placement: станция в WorldScene_0_0 (Phase 7, ~0.5 ч)

**Файлы:** префаб + `BootstrapScene.unity` + `WorldScene_0_0.unity`

**Префаб `CraftingStation_CraftingTable.prefab`:**
- GameObject с: `NetworkObject`, `BoxCollider` (isTrigger, size 2×1×2), `MeshRenderer` (стол/куб), `CraftingStation`, `MetaRequirement` (если tool check пустой — `_requiredItems` пуст + `_logic=All` = всегда доступен)
- `CraftingStation._config` = `Station_CraftingTable.asset`
- `CraftingStation._metaRequirement` = ссылка на MetaRequirement (или null)

**BootstrapScene.unity:**
- Новый GameObject `[CraftingServer]` с `NetworkObject` + `CraftingServer`
- `NetworkManagerController` → `+CreateCraftingClientState()` (auto-spawn)

**WorldScene_0_0.unity:**
- 1-2 CraftingStation рядом с ResourceNode (например @ 39980, 2502.77, 40000)
- `[CraftingStation_Table]` — универсальная (3 рецепта: CopperIngot, IronIngot, ShipLight)
- `[CraftingStation_Shipyard]` — только ShipLight рецепт (требует MetaReq?)

**Verify:**
- Загрузить WorldScene_0_0 → станция видна
- StartHost → станция зарегистрирована в CraftingServer
- E на станции → открыть CraftingWindow → список рецептов

**Risk:** low.

---

## §4 Milestones

| Milestone | Тикеты | Что работает |
|-----------|--------|--------------|
| **M1 — Data + World** | T-C01, T-C02 | `RecipeData` SO с ингредиентами/выходами. `CraftingWorld` POCO: реестр рецептов, state machine, TryAddIngredient/TryStart/TryCancel/TryCollect. DTOs сериализуются (INetworkSerializable, null-string writeback). `CraftingTimeService` тикает раз в секунду. **Всё без сети.** |
| **M2 — Server + Station** | T-C03, T-C04 | `CraftingServer` принимает RPC, CraftingTimeService.OnTick → CraftingWorld.OnTick. `CraftingStation` спавнится, проверяет tool через MetaRequirement (как ResourceNode), 5 RPC работают. End-to-end: ингредиенты → буфер → Start → InProgress → Completed → Collect → предмет в инвентаре. |
| **M3 — UI + Deployment** | T-C05, T-C06, T-C07 | `CraftingClientState` доставляет события на клиент. `CraftingProgressController` показывает ProgressBar. `CraftingWindow` (отдельный UIDocument, как DialogWindow) — выбор рецепта, буфер, кнопки. Станция в WorldScene_0_0, `[CraftingServer]` в BootstrapScene. **Play Mode: E → окно → положить 3 руды → Start → 10 сек → Collect → слиток в инвентаре.** |

---

## §5 Оценка общей трудоёмкости

| Milestone | Тикеты | ~Часов |
|-----------|--------|--------|
| M1 — Data + World | T-C01, T-C02 | ~2.5 ч |
| M2 — Server + Station | T-C03, T-C04 | ~3 ч |
| M3 — UI + Deployment | T-C05, T-C06, T-C07 | ~4 ч |
| **TOTAL** | **7 тикетов** | **~9.5-11 ч** |

**С учётом фикс-итераций (опыт Mining: +30-50%): ~12-16 ч, 4-5 сессий.**

---

## §6 Риски (прогноз)

| # | Риск | Статус | Митигация |
|---|------|--------|-----------|
| 1 | **UI Toolkit UIDocument pitfalls** | 🟡 unknown | **Обязательно** прочитать T-Q11b_c_session_log.md (18 persistent UI Toolkit bugs). PanelSettings, PickingMode, cursor, styleSheets. |
| 2 | **CraftingWindow как отдельный UIDocument** — Show/Close pattern с Cursor unlock | 🟡 medium | Паттерн DialogWindow (уже работает) + MarketWindow (SetVisible). Show → Cursor None/visible, Close → Locked (если IsListening). |
| 3 | **Scene-placed NRE (NetworkObject)** | 🟡 low | ScenePlacedObjectSpawner в BootstrapScene уже жив. InScenePlacedSourceGlobalObjectIdHash может быть 0. |
| 4 | **CraftingTimeService race с MarketTimeService** | 🟡 low | Отдельный MonoBehaviour, не привязан к рынку. TickInterval = 1f. |
| 5 | **Много NetworkVariable на станцию** (6 шт) | 🟡 low | 1 станция в MVP — не критично. Phase 2: `NetworkVariable<CraftingJobDto>` struct. |
| 6 | **`RecipeData.requiredSkillLevel` не имплементируется в MVP** | 🟢 accepted | Задел `int = 0` в SO. Код не проверяет. |
| 7 | **Drag-and-drop упрощён до кнопок `+1`** | 🟢 accepted | Пользователь сказал «MVP — минимум». Кнопки проще. |
| 8 | **Выдача корабля через Item_Key_Ship* — ключ надо подбирать (E) после выдачи** | 🟢 accepted | Ключ падает в инвентарь (AddItemDirect). Игрок подходит к кораблю и F. |

---

## §7 Session Log (будущие записи)

> Каждая сессия кодинга добавляет запись сюда по шаблону:

### §7.X T-C<номер> — <суть> (<дата>)

**Verify:**
- ✅ Compile: 0 errors
- ...

**Key Lessons:**
- ...

**Files modified/new:**
```
A ...
M ...
```

---

## §8 Сводный статус (1 строка)

**M1–M3 = 📋 PLANNED.** 7 тикетов. Дизайн готов (оригинал `Crafting_system/` + актуализация `ANALYSIS_REUSE_2026-06-10.md`). 8 из 16 компонентов переиспользуют готовые паттерны из Gathering + MetaRequirement. **Оценка: ~9.5-11 ч.** Старт — по готовности.

---

*См. `00_OVERVIEW.md` для архитектуры, `10_DESIGN.md` для дизайна, `ANALYSIS_REUSE_2026-06-10.md` для актуализации после Mining.*
