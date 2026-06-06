# Crafting System — Обзор

> **Скоуп документа:** Архитектурный дизайн крафт-системы Project C: The Clouds. Цикл — анализ и проектирование, **без кода** (только сигнатуры и описания).
> **Версия:** `v0.0.1-design` (2026-06-07)
> **Связанные GDD:** `docs/gdd/GDD_20_Progression_RPG.md` (будущее), `docs/gdd/GDD_22_Economy_Trading.md` (контракты), `docs/gdd/GDD_10_Ship_System.md` (верфь)
> **Связанные подсистемы:** `docs/MetaRequirement/00_OVERVIEW.md` (lock/key), `docs/INVENTORY_SYSTEM.md` (v0.0.7), `docs/Ships/Key-subsystem/` (ship key → meta requirement)

---

## 1. Что такое крафт в Project C

**Крафт** — server-authoritative процесс, в котором игрок:
1. Подходит к крафт-станции (стол/верфь/наковальня) в общей зоне.
2. Выбирает рецепт (1 станция = 1 активный заказ в MVP, см. §5.3).
3. Любой игрок в зоне станции кладёт ресурсы в **буфер станции** (soft-lock: ресурсы зарезервированы, нельзя забрать до завершения/отмены).
4. **Заказчик** (тот, кто инициировал рецепт) запускает крафт — на сервере списывается рецепт + таймер.
5. По истечении таймера (глобальный, считается по `ServerTimeController` / `MarketTimeService`) сервер выдаёт **готовый продукт**:
   - обычный предмет → в инвентарь заказчика (`InventoryWorld.AddItemDirect`)
   - корабль/модуль → через существующий `ShipKeyServer` (он уже алиасится в `MetaRequirementRegistry`, выдача `ShipKeyBinding` ключа)
6. Заказчик забирает готовое. **Только он** (anti-grief, см. §6.4).

**Ключевая идея:** не вводим новые типы хранилищ. Ресурсы — те же `ItemData` (8 `ItemType`), которые уже живут в `ProjectC.Items.InventoryWorld` (per-player) и `ProjectC.Trade.Warehouse` (per-player per-location). Буфер станции — серверная запись `CraftingJob.BufferedItems`, которая **не дублирует** ItemDatabase, а только резервирует itemId + quantity + owner.

---

## 2. Почему именно так (а не простой стол-забрал-выдал)

Простой сценарий (стол забрал входы → мгновенно выдал выход) уже реализуем на коленке: `MetaRequirementRegistry` + `LootTable`. **Это слишком бедно для MMO-песочницы «Интеграл Пьявица»**, где:
- корабли — основной игровой объект, и хочется, чтобы их создавали игроки (а не только NPC-контракты, см. `docs/gdd/GDD_25_Trade_Routes.md`),
- ресурсы — много, и нужна **социальная** механика (несколько игроков сбрасываются на один заказ),
- нужен **таймер** (глобальный, как в рынке), чтобы рецепты оставались заказанными офлайн.

Возвращаемся к `docs/gdd/GDD_20_Progression_RPG.md` (когда допишется) и `docs/gdd/GDD_01_Core_Gameplay.md` за подтверждением, что craft = долгое, социальное, ресурсо-затратное действие.

---

## 3. Скоуп MVP (то, что делаем в первом релизе)

| # | Фича | В MVP? | Заметки |
|---|------|--------|---------|
| 1 | CraftingStation NetworkObject (верфь + крафт-стол) | ✅ | Минимум 2 типа — верфь и стол. Через ScriptableObject `CraftingStationConfig` |
| 2 | RecipeData ScriptableObject (входы/выход/время) | ✅ | В `Resources/Crafting/`. `RecipeData` ≠ `LootTable` (это детерминированно) |
| 3 | UI: Crafting tab в MarketWindow | ✅ | По образцу Contracts tab (C2 refactor) |
| 4 | Drag-and-drop ресурсов (inventory/warehouse → station buffer) | ✅ | `VisualElement` slots, drop events |
| 5 | Server-authoritative: `CraftingServer` NetworkBehaviour | ✅ | По образцу `MarketServer` |
| 6 | Soft-lock буфер (ресурсы зарезервированы, откат при cancel) | ✅ | По `TradeWorld.TryBuy` rollback pattern |
| 7 | Глобальный таймер крафта (server time) | ✅ | `MarketTimeService`-like; разделяемое время с рынком |
| 8 | Выдача результата: предмет | ✅ | `InventoryWorld.AddItemDirect` |
| 9 | Выдача результата: корабль | ✅ | Через `ShipKeyServer` алиас (см. §6.6) |
| 10 | Забрать результат может **только заказчик** | ✅ | `CraftingJob.ownerClientId` |
| 11 | Station broadcast: все в зоне видят прогресс | ✅ | `NetworkVariable<float>` для прогресс-бара |
| 12 | Очередь крафтов (3 рецепта подряд) | ❌ | Phase 2 |
| 13 | Уровни/ускорения станций | ❌ | Phase 2+ |
| 14 | Случайные выходы / качество | ❌ | Phase 2+ |
| 15 | Топливо/инструменты как расходники | ❌ | Phase 2+ |

---

## 4. Что НЕ входит в скоуп (намеренно)

- **Новые типы предметов.** Всё остаётся на `ItemData` (8 типов). Никаких «crafting tokens» или «resource tokens».
- **Новое общее хранилище.** Буфер станции — server-only state, не `Warehouse` (тот per-player per-location).
- **Соло-станции (per-player placement).** Только world-placed NetworkObject, общие для всех в зоне.
- **Pickup-to-station.** Подбор с пола → в буфер станции НЕ поддержан. Только через инвентарь/трюм/склад → UI → буфер. Решение `A` из `clarify` §1.
- **Cross-scene станции.** Станция живёт в одной стриминговой сцене (`WorldScene_X_Z`). Если игрок уходит из сцены, Job всё ещё тикает (server time), но UI не показывает прогресс до возврата в зону.

---

## 5. Ключевые архитектурные решения (ADR-style)

### 5.1 Источники ресурсов — `InventoryWorld` + `Warehouse`, без cargo в MVP

Крафт-станция принимает ресурсы из **любого** из этих источников, в любой комбинации, **per-player** (тот, кто кладёт):
- `InventoryWorld` (8-секторный инвентарь игрока, 32 слота max) — `AddItemDirect` уже есть (строка 355 `InventoryWorld.cs`).
- `Warehouse` (per-player per-location) — `TryAdd`/`TryRemove` уже есть (строки 59, 94 `Warehouse.cs`).

**Cargo корабля НЕ источник в MVP.** Причины:
1. `CargoSystem` в `Assets/_Project/Player/CargoSystem.cs:36` ещё **локальный**, не `NetworkBehaviour`. Чтобы тащить из трюма, нужно его переписать в сетевую подсистему — отдельная большая задача.
2. Если игрок у причала, то для верфи логичнее сначала `Unload` в warehouse, потом крафтить. Это укладывается в существующий pipeline.
3. В v2 при необходимости — добавляем как третий источник (см. Open Questions §9.5).

### 5.2 Soft-lock (RESERVE) — а не Hard-lock (STRICT)

Игрок выбрал: «забрать может тот кто заказал» (см. `clarify` §3 ответ) + «если в процессе крафта ресурс забрали — крафт отменяется, ресурсы возвращаются» (см. `clarify` §4 ответ).

Значит:
- При `RequestAddIngredient(recipeId, itemId, qty, source)`:
  1. Валидация: игрок — owner (для hard-lock режима) или любой в зоне (для coop, как у нас).
  2. Списать с source (inventory/warehouse). Если не хватает → `Result.NotEnoughResources`.
  3. **Положить в `CraftingJob.BufferedItems[(itemId, qty, sourceOwnerClientId)]`** — буфер станции.
- При `RequestStartCraft(jobId)`:
  1. Валидация: у заказчика есть `CraftingServer.IsInZone(station, clientId)`.
  2. Проверить, что буфер содержит все `RecipeData.ingredients[]` (по `itemId + qty`).
  3. Перевести ресурсы из буфера в **committed** (`CraftingJob.CommittedItems`).
  4. `startServerTime = ServerTimeController.Instance.ServerTimeSeconds`.
  5. Уведомить всех в зоне через `NetworkVariable<float>` (progress 0..1).
- При тике `MarketTimeService.MarketTick(dt)`:
  1. `progress = (now - start) / recipe.craftSeconds`.
  2. Когда `progress >= 1`: выдать `RecipeData.outputs[]` (см. §5.5).
  3. **Очистить** `CommittedItems`. Job уходит в `CompletedJobs[]` (для забора).

**Откат:** `RequestCancelCraft(jobId)`:
1. Только `ownerClientId` может отменить.
2. Вернуть `CommittedItems` в инвентарь заказчика (если есть место) или warehouse.
3. Если `CraftingJob` не в `InProgress` (а в `Buffered`/до `StartCraft`) — буфер тоже возвращается в source (но `source` уже сохранён в записи буфера, см. п.3 в `RequestAddIngredient`).

### 5.3 Одна станция = один активный заказ в MVP

Игрок выбрал: «одна рецепт → один входной набор → один выходной предмет» (MIN из `clarify` §3).

Значит:
- `CraftingStation.MaxConcurrentJobs = 1` (жёстко в MVP).
- Если игрок пытается положить второй набор — сервер вернёт `CraftingResultCode.StationBusy`.
- Очереди нет (Phase 2).

### 5.4 Таймер = глобальный server time

Игрок указал: «таймер по серверу, у нас есть серверные часы в serverweather» + выбрал «оффлайн = продолжается» (default `clarify` §4).

Значит:
- Не используем `Time.deltaTime` на клиенте.
- Время старта: `ServerTimeController.Instance.ServerTimeSeconds` (см. `MarketTimeService.cs:60` — `SecondsUntilNextTick`, pattern тот же).
- Длительность: `RecipeData.craftSeconds` (например, 600 для модуля, 3600 для корпуса корабля).
- Тик: на сервере `MarketTimeService` уже дёргает `onMarketTick` каждые `baseTickIntervalSeconds` (default 300). В обработчике `CraftingServer.OnMarketTick(dt)` мы пройдёмся по всем активным `CraftingJob` и проверим `now - job.startServerTime >= job.recipe.craftSeconds`.
- Длительность в `RecipeData` — настраивается на ScriptableObject, читается в runtime.

**Допущение** (нужно подтвердить с автором GDD_20): короткие рецепты (<5 мин) можно тоже выражать через тот же `MarketTimeService` с `multiplier=1`; длинные (>1 час) — та же система, но `baseTickInterval` будет реже дёргать UI, что норм. Если GDD хочет sub-second таймер для быстрых рецептов — добавим второй `CraftingTimeService` с `baseTickIntervalSeconds = 1f`, **отдельный** от `MarketTimeService` (чтобы изменение `MarketTimeMultiplier` в дебаге не ломало крафт).

### 5.5 Выдача результата — предмет или корабль

`RecipeData.outputs[]` — это **список** (вдруг потом добавятся многовыходные рецепты). Каждый `RecipeOutput`:
- `itemData: ItemData` — обычный предмет → `InventoryWorld.AddItemDirect(ownerClientId, itemId, itemType)` + `SendSnapshot` (см. `InventoryServer.AddItem:79`).
- `produceShip: ShipKeyBinding` (опционально) — для рецепта «корабль» → `ShipKeyServer` (он теперь `MetaRequirementRegistry` алиас) выдаёт ключ через `ReceiveMetaRequirementBindingsTargetRpc`-like RPC. Конкретный API: смотрим `ShipKeyServer.cs:80-90` (legacy `RegisterBinding`) + `MetaRequirement.cs:80-90` (логика `CanPlayerUse`). В v2 — добавим `MetaRequirementRegistry.GrantKeyToClient(shipNetId, clientId)` server-side метод.

**Важно:** корабль не «появляется в мире» из крафта. Крафт выдаёт **ключ-привязку** к заранее размещённому в сцене кораблю (через `ShipKeyBinding`-like `MetaRequirement`). Это соответствует существующему GDD: корабли размещаются на сцене, ключ — в инвентаре.

### 5.6 Права в мультиплеере — общая станция, забирает заказчик

Игрок ответил: «станции общие, игроки приносят ресурсы, забирает тот кто заказал. если корабль - то заказавший получает ключ для корабля» (см. `clarify` §3 второе уточнение).

Значит:
- `CraftingJob.ownerClientId` — единственный, кто может:
  - `RequestStartCraft` (после того, как буфер полон)
  - `RequestCollectResult` (забрать готовое)
  - `RequestCancelCraft` (отменить до завершения)
- `RequestAddIngredient` — **любой** в зоне (т.к. станция общая, кооп).
- Анти-гриф: после `StartCraft` буфер → committed, и если до конца кто-то ещё попытается положить — `StationBusy`.

### 5.7 Что добавляем к `NetworkPlayer` (target-RPC точки доставки)

В `Assets/_Project/Scripts/Player/NetworkPlayer.cs:848-934` уже есть паттерн:
```csharp
[Rpc(SendTo.Owner)]
public void ReceiveContractSnapshotTargetRpc(...) { ContractClientState.Instance?.OnSnapshotReceived(snapshot); }

[Rpc(SendTo.Owner)]
public void ReceiveInventorySnapshotTargetRpc(...) { InventoryClientState.Instance?.OnSnapshotReceived(snapshot); }
```

Добавляем:
```csharp
[Rpc(SendTo.Owner)]
public void ReceiveCraftingSnapshotTargetRpc(CraftingSnapshotDto snapshot, RpcParams rpcParams = default)
    => CraftingClientState.Instance?.OnSnapshotReceived(snapshot);

[Rpc(SendTo.Owner)]
public void ReceiveCraftingResultTargetRpc(CraftingResultDto result, RpcParams rpcParams = default)
    => CraftingClientState.Instance?.OnResultReceived(result);
```

Каждое добавление в этот файл — **мини-риск**, потому что:
- Новые методы с уникальными именами не ломают ничего.
- Не трогаем существующие RPC.
- Не добавляем полей в `NetworkVariable` (только новые методы).

---

## 6. Существующий код, который переиспользуем (REUSE-список)

### 6.1 `ProjectC.Items.InventoryWorld` (core) — **ОСНОВА**

- `CountOf(clientId, itemId)` (строка 200) — проверка «хватает ли».
- `HasAllItems` / `HasAnyItem` / `GetMissingItems` (строки 158, 180, 219) — для прогресс-бара.
- `AddItemDirect` (строка 355) — выдача результата.
- `GetOrRegisterItemId` (строка 98) — резолв `ItemData → int` для отправки в RPC.
- `GetOrCreate` (строка 115) — если нужно сохранить инвентарь.
- `BuildSnapshot` (строка 373) — проекция для клиента (своя snapshot, не переиспользуем напрямую, но pattern тот же).

**Что НЕ хватает (TODO для крафта):** метода `TryRemove(clientId, itemType, itemId, quantity)` для списания ресурсов. Сейчас есть только `TryDrop` (по `slotIndex`, неудобно для буфера). **Нужно добавить** `InventoryWorld.TryConsumeByItemId(clientId, itemId, quantity)` в Phase 1 имплементации. Pattern — `Warehouse.TryRemove` (строки 94-109 `Warehouse.cs`), но ищущий по `List<int>` (по типу).

### 6.2 `ProjectC.Items.Network.InventoryServer` — **ОБРАЗЕЦ для CraftingServer**

- RPC pattern `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]` (строки 98, 119, 168).
- `SendSnapshot` через `NetworkPlayer.ReceiveXxxTargetRpc` (строки 199-218).
- Rate limit per-client (строки 236-254) — копируем.
- `_opTimestamps: Dictionary<ulong, List<float>>` (строка 54).

**Не переиспользуем:** `AddItem` API (онлайн-добавление от сундука), `_dropPickupPrefab` (не наш flow).

### 6.3 `ProjectC.Items.Client.InventoryClientState` — **ОБРАЗЕЦ для CraftingClientState**

- Singleton pattern (строки 26-78).
- `OnSnapshotReceived` → `OnSnapshotUpdated` event (строки 80-93).
- `OnResultReceived` → `OnInventoryResult` event.
- `CurrentSnapshot: InventorySnapshotDto?` (строка 38) — copy-paste, заменяем тип.
- Convenience API (строки 113-146) — `RequestXxx` обёртки над `InventoryServer.RequestXxxRpc`.
- `LocalizeResultCode` (строки 215-232) — copy-paste с новым enum.

### 6.4 `ProjectC.MetaRequirement.MetaRequirement` + `MetaRequirementRegistry` — **ПАРТНЁР для выдачи корабля**

- `MetaRequirement` (NetworkBehaviour, вешается на объект) — **идеально подходит для крафт-станции**. У него уже есть:
  - `CanPlayerUse(clientId, out reason)` (строки 96-142 `MetaRequirement.cs`) — мы пере-используем для проверки «игрок в зоне станции».
  - `OnNetworkSpawn` → `RegisterRequirement` (строка 174-190) — стандартный lifecycle.
  - **`_consumeOnUse = false` — это именно то, что нам НЕ нужно**; для крафт-станции мы создаём **новый** компонент `CraftingStation` (НЕ наследник `MetaRequirement`), у него свой lifecycle и своя логика буфера.
- `MetaRequirementRegistry.RequestCanUseRpc` (строки 101-131) — серверный RPC-образец.

**Реальный план:** `CraftingStation` — отдельный `NetworkBehaviour`, рядом с `NetworkObject`. Внутри — `NetworkList<CraftingJob>` (server-write, everyone-read). НЕ наследник `MetaRequirement`, но использует **ту же серверную авторизацию** через `IsServer`-only методы.

### 6.5 `ProjectC.Trade.Network.MarketServer` + `MarketClientState` + `MarketWindow` — **ГЛАВНЫЙ ОБРАЗЕЦ**

Это **точная копия** архитектуры:
- `MarketServer` (NetworkBehaviour, server-only enabled) → `CraftingServer`
- `MarketState` (POCO, server) → `CraftingWorld` (POCO, server)
- `MarketClientState` (MonoBehaviour, DontDestroyOnLoad) → `CraftingClientState`
- `MarketSnapshotDto` (struct) → `CraftingSnapshotDto`
- `MarketResultDto` / `TradeResultDto` → `CraftingResultDto`
- `MarketTimeService` (`MonoBehaviour` с таймером) → переиспользуем **целиком** (см. §5.4)
- `MarketWindow` (UI Toolkit) → **добавляем TAB** по `project-c-ui-as-tab` skill (см. §7)

### 6.6 `ProjectC.Ship.Key.ShipKeyServer` (legacy алиас) — **ПАРТНЁР для выдачи корабля**

- Уже deprecated и делегирует в `MetaRequirementRegistry` (строки 60-76 `ShipKeyServer.cs`).
- Для крафта: **вызываем** `MetaRequirementRegistry.GrantKeyToClient(shipNetId, clientId)` (это **новый** server-side метод, который добавим в §MVP). Он:
  1. Проверит, что корабль существует в реестре.
  2. Пошлёт `ReceiveMetaRequirementBindingsTargetRpc` (уже есть) — клиент получит обновлённый реестр.
  3. Вернёт `CraftingResult.Ok` + `shipNetworkObjectId` (для UI toast).
- **Не дублируем** `ShipKeyServer` API — используем как мост к `MetaRequirementRegistry`.

### 6.7 `ProjectC.Items.PickupItem` — **НЕ переиспользуем**

- Это world-spawn подбираемый объект (строки 1-191). Станция — не pickup, это интерактивный объект с буфером.
- Зато **`NetworkChestContainer.cs`** (строки 16-335) — **очень похожий** паттерн:
  - `NetworkBehaviour` + `IInteractable`.
  - `OnNetworkSpawn` подписывается.
  - `[Rpc(SendTo.Server)]` для server-side операций.
  - distance validation (строка 211) — копируем.
- `NetworkChestContainer.TryOpen()` (строка 133) — копируем как `CraftingStation.RequestOpenWindow()`.

### 6.8 `ProjectC.Items.LootTable` — **НЕ подходит, делаем `RecipeData`**

- `LootTable` рандомный (строки 35-62). `RecipeData` — детерминированный (1+1+1 = 1). Это разные domain objects.
- Кладём `RecipeData.cs` рядом с `LootTable.cs` в `Assets/_Project/Scripts/Core/`.

### 6.9 `ProjectC.UI.Client.CharacterWindow` — **образец tab-логики в CharacterWindow**

- 5 табов: character/ship/reputation/contracts/inventory (строка 65 `CharacterWindow.cs`).
- Подписки на per-tab singleton в `EnsureBuilt` / unsubscribe в `OnDisable`.
- `SwitchTab(string tab)` — единая точка.
- `Refresh<X>Cache()` — **всегда** при snapshot (pitfall R3-005, см. `project-c-ui-as-tab` SKILL.md).

### 6.10 `ProjectC.UI.Client.MarketWindow` — **образец для добавления tab в Market**

- Уже 3 таба: market/warehouse/contracts (строка 76 `MarketWindow.cs`).
- 4 FIX'ы (pickingMode, layout fallback, cursor, MarkDirtyRepaint) — **наследуются автоматически**.
- Конкретный рецепт добавления таба — `project-c-ui-as-tab` SKILL.md, цитировать не буду, есть в skill.

---

## 7. UI — Crafting как TAB в `MarketWindow`

Применяем `project-c-ui-as-tab` skill **дословно**:

1. **UXML/USS:** добавить в `Resources/UI/MarketWindow.uxml`:
   - Кнопка `crafting-tab-btn` (в существующий tab-bar)
   - Секция `crafting-section` (с `display: none` по умолчанию)
   - Внутри:
     - Recipe selector (left): `ListView` с рецептами, доступными для этой станции
     - Station info (top): `name-label`, `progress-bar`, `time-label`
     - Buffer slots (right): 4–8 `VisualElement` для drag-and-drop
     - Source panels (bottom): мини-panels для player inventory + current warehouse (если в зоне рынка)
     - Action buttons: `start-btn`, `cancel-btn`, `collect-btn`
     - `message-label` (shared с другими табами)

2. **MarketWindow.cs:**
   - Поля: `_craftingList`, `_craftingSection`, `_bufferGrid`, `_startBtn`/`_cancelBtn`/`_collectBtn`, `_selectedRecipeId`, `_craftingCache`.
   - `EnsureBuilt` → init ListView, buttons, subscribe на `CraftingClientState.OnSnapshotUpdated/OnResult`.
   - `OnDisable` → unsubscribe (pitfall R3-005).
   - `HandleCraftingSnapshotUpdated(snap)` — cache update ВСЕГДА, rebuild только если `_activeTab == "crafting"`.
   - `HandleCraftingResultReceived(result)` — обновить `_messageLabel` (shared), gate list mutation.
   - `SwitchTab("crafting")` — `display: flex` на `_craftingSection`, `.active` на кнопке, обновить `_craftingCache`, `ApplyCraftingFilters()`, `CraftingClientState.RequestSubscribeCrafting(stationNetId)`.
   - Drag-and-drop: на `_bufferGrid` ловим `DragEnter/DragLeave/Drop`. На `Drop` вызываем `CraftingClientState.RequestAddIngredient(recipeId, itemId, qty, source)`.
   - Optimistic update: на `OnAddIngredientClicked` — сразу обновить `_craftingCache` + `ListView.Rebuild()` ДО RPC. Server snapshot — придёт через ~100-300мс, перезапишет тем же значением (идемпотентно). Pattern из `project-c-ui-as-tab` SKILL.md §C2-stage gotchas.

3. **MarketInteractor.cs:** после `RequestSubscribeMarket` (если вошли в зону станции) — вызвать `CraftingClientState.RequestSubscribeCrafting(stationNetId)`.

4. **NetworkManagerController.Awake:** добавить `CraftingClientState.Instance` к спавну (уже есть 4 client state: Inventory, MetaRequirement, Contract, Market — добавляется 5-й). Минимальная правка.

---

## 8. Документы в этом каталоге

| Файл | Содержание |
|------|------------|
| `00_OVERVIEW.md` (этот) | Что / зачем / скоуп / REUSE |
| `10_DESIGN.md` | Классы, схемы, sequence-диаграммы, edge-cases |
| `20_IMPLEMENTATION_PLAN.md` | Пошаговый план кодирования, файл-за-файлом, с оценкой |
| `30_VERIFICATION.md` | Что и как проверять, test-plan, ручные сценарии |
| `40_INSPECTOR_REFERENCE.md` | Все ScriptableObject-поля (CraftingStationConfig, RecipeData, RecipeOutput) с примерами |
| `50_KNOWN_ISSUES.md` | Открытые риски, edge-cases, open questions для follow-up |
| `99_CHANGELOG.md` | История изменений документации |

---

## 9. Открытые вопросы (требуют решения)

> ⚠️ **Прежде чем писать код**, нужно закрыть эти вопросы (часть — с автором GDD, часть — с тобой).

1. **Q1**: Должна ли верфь принимать ресурсы из cargo корабля? Сейчас `CargoSystem` локальный. Если да — отдельный ticket «cargo → network», который **больше** чем этот. **Моя рекомендация: нет в MVP, добавим в v2.**
2. **Q2**: Должна ли крафт-станция быть `NetworkObject` с `destroyWithScene: true` (живёт в `WorldScene_X_Z` и умирает при выгрузке), или сохранять state через `DontDestroyOnLoad`? **Рекомендация: `destroyWithScene: true` (как `NetworkChestContainer`).** Если игрок ушёл из сцены — Job ушёл, ресурсы вернулись. Альтернатива: Job сохраняется в `IPlayerDataRepository` и ресторится при повторном заходе — это +1–2 дня работы и нужно для долгих рецептов (1+ час).
3. **Q3**: Если `NetworkTimeService` или `MarketTimeService` отключены (multiplayer off / single player), что делать с таймером? **Рекомендация:** если `NetworkManager.Singleton == null || !IsListening` — таймер реального времени (`Time.realtimeSinceStartup`), и Job не персистится. Это даёт single-player fallback.
4. **Q4**: «Глобальные» рецепты vs «локационные»? Сейчас `RecipeData` — ScriptableObject, не привязан к station. Можно ли **одной и той же** станции крафтить разные рецепты (выбор в UI)? Или у каждой станции — фиксированный набор? **Рекомендация:** у станции — `RecipeData[] allowedRecipes[]` (ссылки в инспекторе). Это даёт и универсальные, и специализированные станции.
5. **Q5**: При нехватке ресурсов в инвентаре при `RequestCollectResult` — что делать? **Рекомендация:** результат **ждёт** в `CompletedJobs[]` пока не заберут (как в реальной жизни — заказ лежит на верфи). Если игрок в оффлайне 3 дня — при заходе видит «у вас готово N заказов». Лимит — 10 completed jobs на клиента, потом сервер архивирует.
6. **Q6**: Скиллы/уровни игрока на крафт (quality of output) — **нет в MVP**, фаза 2+. Но заложить `RecipeData.requiredSkillLevel: int` чтобы потом не ломать? **Рекомендация: да, заложить как `int = 0` (= нет требования) в MVP, чтобы не ломать миграцию данных.**
7. **Q7**: Станция-верфь крафтит «корабль» (через `MetaRequirement.GrantKeyToClient`). А если заказчик не в зоне (ушёл гулять)? **Рекомендация:** результат ждёт (см. Q5). При заходе в зону `CraftingClientState` получит snapshot и покажет «у вас готов заказ на верфи». Это и есть MMO-фича.

---

## 10. Что я НЕ предлагаю делать в этом дизайне (намеренно)

- **Не делаем новый namespace `ProjectC.Crafting`** (как ты мог ожидать). Кладём всё в `ProjectC.Items` (как `RecipeData`, `CraftingClientState` в подпапке) и `ProjectC.World.Crafting` (как `CraftingStation`, `CraftingServer`). Это согласуется с тем, что `ProjectC.Items` уже владеет `ItemData`/`InventoryWorld`/`InventoryClientState`, а `ProjectC.World.Chest` — `NetworkChestContainer`.
- **Не делаем `CraftingWorld : MonoBehaviour`.** Это POCO singleton (как `InventoryWorld`, `TradeWorld`), создаётся в `CraftingServer.OnNetworkSpawn`.
- **Не делаем `RecipeData` наследником `LootTable`.** Разные domain objects, разные use-cases.
- **Не вводим `CraftingItemType` enum.** Используем существующий `ItemType` (8 типов) — входы и выходы рецепта всегда `ItemData`.
- **Не дублируем `NetworkVariable<T>` для прогресс-бара в каждой станции.** Используем **server-side POCO** `CraftingJob` и пересчитываем прогресс на клиенте по `startServerTime + craftSeconds - now`. Snapshot шлёт только `progress: float` + `state: enum`.

---

## 11. Связанные документы (для следующего чтения)

- `docs/Crafting_system/10_DESIGN.md` — детальный дизайн классов, sequence-диаграммы
- `docs/MetaRequirement/00_OVERVIEW.md` — почему `MetaRequirement` не подходит как основа, но его Registry используем
- `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — миграция ShipKey → MetaRequirement
- `docs/dev/INVENTORY_V2_REFACTOR.md` — как делался v2 рефакторинг инвентаря (паттерн повторяем)
- `docs/dev/CONTRACT_V2_MIGRATION.md` — как делался v2 рефакторинг контрактов (паттерн повторяем)
- `docs/Markets/FIXES_HISTORY.md` — все баги, пойманные в v2-рынке (избегаем повторения)
- `docs/Character-menu/sub_inventory-tab/` — R3-005 inventory tab (R3-005 pitfall: cache update unconditional)
- `unity-mcp-orchestrator` skill — для MCP-работы при имплементации
- `project-c-ui-as-tab` skill — для добавления Crafting tab в MarketWindow
- `unity-v2-subsystem-migration` skill — для всего процесса
