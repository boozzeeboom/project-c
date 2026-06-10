# Анализ Crafting System — интеграция с существующим кодом

**Дата:** 2026-06-10
**Основание:** ревью `docs/Crafting_system/` (v0.0.1-design) после завершения Mining (T-G01–T-G07), MetaRequirement, Inventory v2.

---

## 1. Что изменилось с момента написания Crafting System (2026-06-07)

На момент проектирования крафта (2 дня назад) существовали:

| Система | Статус на 2026-06-07 | Статус на 2026-06-10 |
|---------|----------------------|---------------------|
| InventoryWorld + AddItemDirect | ✅ было | ✅ без изменений |
| InventoryServer (RPC hub) | ✅ было | ✅ без изменений |
| MetaRequirement + Registry | ✅ Stage 1 (ключ-замок) | ✅ Stage 1 + используется в Mining |
| ShipKeyServer (legacy) | ✅ | ✅ без изменений |
| MarketServer / MarketWindow | ✅ было | ✅ без изменений |
| NetworkPlayer RPC паттерны | ✅ было | ✅ +ReceiveGatherResultTargetRpc |
| NPC + Quests v2 | 🟡 частично | ✅ M1–M19 FSD |
| **Resource Gathering (Mining)** | ❌ не было | **✅ T-G01–T-G07 DONE** |

---

## 2. Что можно переиспользовать (НОВОЕ)

С Mining появились **готовые компоненты**, которые крафт может переиспользовать напрямую:

### 2.1 `GatheringServer.cs` — **образец для CraftingServer**
- RPC hub pattern (RequestXxxRpc → сервер → SendTargetRpc)
- `GatherResult DTO` (`INetworkSerializable`, null-string writeback) — готовый шаблон для `CraftingResultDto`
- `CheckDistance()` / `FindNetworkPlayer()` — переиспользовать без изменений
- Rate limit — готовый boilerplate
- **Не нужно изобретать** — копируем GatheringServer как CraftingServer

### 2.2 `GatheringClientState.cs` — **образец для CraftingClientState**
- Events pattern (OnGatherProgress / OnGatherCompleted / OnGatherDenied)
- Server timeout watcher
- `CurrentNodeNetId` guard (один сбор = одно окно) → **аналог: одна станция = один активный заказ**
- **CraftingClientState будет на 80% копией** GatheringClientState с другими event names

### 2.3 `GatheringToastController.cs` — **образец для CraftingToast/CraftingProgress**
- ProgressBar UI (runtime-constructed VisualElement) — **дословно** для крафт-прогресса
- `HandleProgress`, `HandleCompleted`, `HandleInterrupted` — те же события для крафта
- Позиционирование (ShipKeyPanelSettings, bottom: 48) — готовое решение

### 2.4 `ResourceNodeConfig.cs` — **аналог RecipeData**
- `gatherSeconds` → `craftSeconds`
- `_resultItem` → `outputs`
- `_requiredTool` → `ingredients` (через MetaReq)
- `_maxHarvests` → не нужно (крафт одноразовый)
- Вместо того чтобы делать RecipeData с нуля — **скопировать паттерн** SO с [CreateAssetMenu], lazy-resolve ItemData ids

### 2.5 `ResourceNode.cs` — **аналог CraftingStation**
- State machine (Idle / Occupied / Depleted / Cooldown) → (Empty / Buffered / InProgress / Completed)
- `NetworkVariable<ResourceNodeState>` → `NetworkVariable<CraftingJobState>`
- `TryStartGather` → `TryStartCraft`
- `CompleteGather` → `CompleteCraft` (выдать предмет)
- `OnTriggerEnter/Exit` → **те же** для входа в зону станции

---

## 3. Что можно обвязать (CHANGED — переписать в дизайне)

### 3.1 Tool check через MetaRequirement (было: через InventoryWorld.CountOf)

В `00_OVERVIEW.md` §6.4 написано: «входы рецепта — через `InventoryWorld.CountOf`». **Теперь это должно идти через `MetaRequirement`**, как в Mining.

**Рекомендация:** для станции, которая требует инструмент (например, «Молот» для верфи), используем `MetaRequirement` компонент на `CraftingStation` (как на `ResourceNode`). Это даёт:
- All/Any/AtLeastN логику
- Бесплатный toast отказа
- Единый механизм требований для ВСЕХ интерактивных объектов
- Задел на будущее: инструмент может быть и для крафта (не только для сбора)

### 3.2 `InventoryWorld.TryRemoveByItemId` — уже не нужен

В `Crafting_system/00_OVERVIEW.md` п.6.1 написано: «нужно добавить `TryRemoveByItemId`». **Gathering показал**, что `AddItemDirect` единственный метод, который понадобится для выдачи результата. Для списания ингредиентов — **не надо трогать InventoryWorld**.

**Лучшее решение:** `CraftingWorld` сам ведёт буфер станции и списывает «виртуально» (buffer → committed → output). Ресурсы в буфере — это server-only state, не `InventoryData`. Когда ресурсы переходят в committed — мы их удаляем из инвентаря **одним RPC** через существующий `InventoryServer.TryRemove` (T-Q14). Если TryRemove не хватает — добавить **там** (в InventoryServer), а не в InventoryWorld.

### 3.3 `MetaRequirementRegistry.GrantKeyToClient` — пересмотреть

В `Crafting_system/00_OVERVIEW.md` п.5.5 сказано: «добавить `GrantKeyToClient()` в MetaRequirementRegistry». **Теперь у нас есть Mining** который использует `MetaRequirement` как тулчек на ResourceNode. **Лучше**: когда крафт завершён — не выдавать ключ в MetaRequirementRegistry, а просто:
1. Добавить ключ-предмет (`Item_Key_ShipXX`) в инвентарь через `InventoryWorld.AddItemDirect` (уже работает)
2. Ключ-предмет уже есть в `Resources/Items/Item_Key_Ship*.asset` (от Ship Key MVP)
3. Игрок подходит к кораблю — F → MetaReq проверяет ключ в инвентаре (уже работает)

**Это проще, безопаснее, не требует нового API.** Выдача корабля через ключ-предмет, а не через прямую регистрацию в MetaRequirementRegistry.

### 3.4 Cargo → Warehouse (не cargo → ship)

В `00_OVERVIEW.md` п.5.1: «cargo не источник в MVP». Сейчас у нас есть:
- `CargoSystem` — локальный (не NetworkBehaviour)
- `Warehouse` — уже есть в `ProjectC.Trade`, per-player per-location

**Рекомендация:** в MVP крафт-станция принимает ресурсы только из `InventoryWorld` (инвентарь игрока). Warehouse — Phase 2 (требует Market-интеграции). Cargo — Phase 3 (требует переписывания CargoSystem в NetworkBehaviour).

### 3.5 `market-ui-as-tab` → свой отдельный CraftingWindow

В `00_OVERVIEW.md` §7: «Crafting как TAB в MarketWindow». **После опыта с GatheringToast (который мы переделывали 4 раза из-за PanelSettings) — рекомендую отдельный UIDocument.**

Причина: MarketWindow — 3 таба + P-табы, сложный layout, общие PanelSettings. Крафт-станция имеет **drag-and-drop**, **буфер**, **выбор рецепта**, что сильно сложнее простого таба. Добавить как tab внутри MarketWindow — высокий риск сломать 3 существующих таба.

**Вместо:** отдельный UIDocument `CraftingWindow` с `CraftingPanelSettings` (копировать GatheringPanelSettings). Открывается при `E` на станции, закрывается по `Esc`. Паттерн — `DialogWindow` (который уже работает).

---

## 4. Что оставить как есть (OK — не менять)

### 4.1 `RecipeData` SO — оставить как есть
- Детерминированный рецепт с `ingredients[]` + `outputs[]` + `craftSeconds` — верно спроектирован
- `LootTable` ≠ `RecipeData` — верно
- `requiredSkillLevel: 0 = no requirement` — верно (задел на будущее)

### 4.2 `CraftingWorld` POCO — оставить как есть
- Отдельный от NetworkBehaviour, тестируемый — верно
- `RecipeData → id` реестр — верно (как `InventoryWorld._itemDatabase`)

### 4.3 `CraftingServer` как отдельный NetworkBehaviour — оставить как есть
- Не наследник GatheringServer — верно (разные домены, разный tick)
- `MarketTimeService` для таймера — верно (уже есть, работает)

### 4.4 `CraftingJob` state machine — оставить как есть
- Empty → Buffered → InProgress → Completed → Collect — верно
- `ownerClientId` guard — верно
- Soft-lock (BUFFER) → Hard-lock (COMMITTED) — верно

### 4.5 `RecipeOutput.shipKeyBinding` — оставить как концепт, но заменить реализацию
- Концепт «выдать предмет ИЛИ корабль» — правильный
- Реализация: вместо `GrantKeyToClient` → `InventoryWorld.AddItemDirect(Item_Key_ShipXX)` (см. §3.3)

### 4.6 DestroyWithScene:true — оставить как есть
- Станция в WorldScene_X_Z, `destroyWithScene: true` — верно (как ResourceNode, NetworkChestContainer)
- Если Job активен, а сцена выгрузилась — Cancel + возврат ресурсов

---

## 5. Что нужно изменить в дизайне (CHANGES REQUIRED)

| Пункт дизайна | Было | Стало (рекомендация) | Причина |
|--------------|------|---------------------|---------|
| §6.4 Tool check | `InventoryWorld.CountOf` | **MetaRequirement** на станции (как Mining) | Единый механизм, бесплатный toast |
| §6.1 Remove items | `InventoryWorld.TryRemoveByItemId` (new) | **Уже есть** `InventoryServer.TryRemove` (T-Q14) | Не плодить новые методы |
| §5.5 Выдача корабля | `MetaRequirementRegistry.GrantKeyToClient` (new) | `InventoryWorld.AddItemDirect(Item_Key_*)` | Проще, не требует нового API |
| §7 UI | TAB в MarketWindow | **Отдельный UIDocument CraftingWindow** | Избежать поломки 3-x табов |
| §5.1 Источники | Inventory + Warehouse + Cargo | **Только Inventory в MVP** | warehouse требует Market, cargo требует сети |
| §6.2 Образец | MarketServer | **GatheringServer** (ближе по смыслу) | Оба — таймер + прогресс + выдача |
| §6.3 Образец | MarketClientState | **GatheringClientState** (короче, только 5 events) | Меньше boilerplate |
| §6.5 Culprit | QuestToast → copy | **GatheringToastController** → copy | Уже есть рабочая ProgressBar-реализация |
| 10_DESIGN.md §3.3 | 6 NetworkVariable на станцию | **`NetworkVariable<CraftingJobDto>`** (одна struct) | Оптимизация, меньше трафика |
| 20_IMPLEMENTATION.md Шаг 0.2 | `TryRemoveByItemId` в InventoryWorld | **Не нужно** — использовать `InventoryServer.TryRemove` | Существующий API покрывает |

---

## 6. Новый REUSE-список (актуальный на 2026-06-10)

| # | Компонент | Где крафт | Как |
|---|-----------|-----------|-----|
| 1 | `GatheringServer` | **Образец** для CraftingServer | RPC hub + tick + DTO + rate limit |
| 2 | `GatheringClientState` | **Образец** для CraftingClientState | Events + timeout + singleton |
| 3 | `GatheringToastController` | **Образец** для CraftingProgress | ProgressBar UI (runtime VisualElement) |
| 4 | `ResourceNodeConfig` | **Образец** для RecipeData SO | [CreateAssetMenu], ResolveItemIds |
| 5 | `ResourceNode.cs` state machine | **Образец** для CraftingStation | TryStart/Tick/Cancel/Complete |
| 6 | `MetaRequirement` (component) | **Инструмент** для tool check на станции | All/Any/AtLeastN (как на ResourceNode) |
| 7 | `MetaRequirementClientState.OnAccessDenied` | **Тост** при отказе в доступе к станции | Бесплатно |
| 8 | `InventoryWorld.AddItemDirect` | **Выдача** результата | Уже работает |
| 9 | `InventoryServer.TryRemove` (T-Q14) | **Списание** ингредиентов | Уже есть |
| 10 | `InventoryServer.RequestRemoveRpc` | **RPC** для списания | Уже есть |
| 11 | `Item_Key_Ship*` (Resources/Items/) | **Ключ** корабля для крафта-верфи | Существуют (3 шт) |
| 12 | `NetworkPlayer` RPC pattern | **TargetRPC** добавления | +ReceiveCraftingSnapshotTargetRpc |
| 13 | `NetworkManagerController` | **Auto-spawn** CraftingClientState | +CreateCraftingClientState() |
| 14 | `MarketTimeService` | **Таймер** крафта | Уже работает для рынка |
| 15 | `DialogWindow` | **Образец** для CraftingWindow (отдельный UIDocument) | PanelSettings, Show/Close pattern |
| 16 | `ShipKeyPanelSettings` / `ShipKeyToast` | **PanelSettings** для CraftingWindow | Копировать с уже работающего |

---

## 7. Оценка effort (пересчитанная)

| Фаза | Что | Старый план (на 2026-06-07) | Новый план (2026-06-10) |
|------|-----|---------------------------|------------------------|
| 0 | Pre-implementation (SO + InventoryWorld) | ~1.5 ч | **~1 ч** (меньше boilerplate) |
| 1 | RecipeData + CraftingStationConfig SO | ~1 ч | ~1 ч (без изменений) |
| 2 | CraftingWorld POCO + DTOs | ~2 ч | ~1.5 ч (GatherResult DTO как образец) |
| 3 | CraftingServer NetworkBehaviour | ~2 ч | **~1 ч** (GatheringServer — готовый pattern) |
| 4 | CraftingStation NetworkBehaviour | ~2 ч | ~2 ч (ResourceNode — готовый pattern) |
| 5 | CraftingClientState + UI | ~3 ч | **~1.5 ч** (GatheringClientState копия) |
| 6 | CraftingWindow (UIDocument) | ~2 ч (tab) | ~2 ч (отдельный UIDocument, как DialogWindow) |
| 7 | Scene placement + тесты | ~1 ч | ~0.5 ч (меньше нового) |
| **TOTAL** | | **~14.5 ч** | **~10.5 ч** |

**За счёт чего экономия ~4 ч (28%):**
- GatheringClientState → CraftingClientState: -1.5 ч (готовые events, timeout, singleton)
- GatheringServer → CraftingServer: -1 ч (готовый tick, rate-limit, distance, FindNetworkPlayer)
- GatheringToastController → CraftingToast: -0.5 ч (готовый ProgressBar, PanelSettings)
- ResourceNodeConfig → RecipeData: -0.5 ч (готовый SO pattern)
- InventoryWorld.TryRemoveByItemId — не нужно: -0.5 ч
- Убрать GrantKeyToClient: -0.5 ч

---

## 8. Риски (актуализированные)

| # | Риск | Митигация |
|---|------|-----------|
| 1 | **UI Toolkit PanelSettings** — столько же проблем сколько с GatheringToast | Делать отдельный UIDocument (как DialogWindow). PanelSettings = копия ShipKeyPanelSettings (работает). См. 18 persistent UI Toolkit bugs в мемори. |
| 2 | **6 NetworkVariable на станцию** — нагрузка на сеть | В MVP ок (1 станция). В Phase 2 → `NetworkVariable<CraftingJobDto>` struct. |
| 3 | **MarketTimeService race** — крафт-таймер склеится с рыночным | Отдельный `CraftingTimeService` с `baseTickIntervalSeconds = 1f` для быстрых рецептов (<5 мин). Длинные (>1 час) — через MarketTimeService. |
| 4 | **CraftingStation как scene-placed NetworkObject** — InScenePlacedSourceGlobalObjectIdHash = 0 (см. AGENTS.md) | `ScenePlacedObjectSpawner` в BootstrapScene уже спавнит всё. Решение из INTEGRATION_SHIPS_TO_WORLD_0_0.md. |
| 5 | **Drag-and-drop UI** — сложность, MVP можно без него | MVP: кнопки `+1` / `+All` вместо drag-and-drop. Drag-and-drop — Phase 2. |
| 6 | **Concurrent players у одной станции** — второй не может даже смотреть | `CraftingStation` не блокирует просмотр. `RequestStartCraft` проверяет `ownerClientId`. `RequestAddIngredient` разрешён любому в зоне. |

---

## 9. Рекомендованный план (актуальный порядок)

```
T-C01: RecipeData + CraftingStationConfig SO (создать .cs + 3 test .asset)
T-C02: CraftingWorld POCO (CraftingJob, DTOs, state machine)
T-C03: CraftingServer NetworkBehaviour (RPC hub + tick + CraftingTimeService)
T-C04: CraftingStation NetworkBehaviour (state, MetaReq tool, trigger zone)
T-C05: CraftingClientState + CraftingToast (events + ProgressBar + auto-spawn)
T-C06: CraftingWindow отдельный UIDocument (как DialogWindow)
T-C07: Scene placement (станция в WorldScene_0_0 + BootstrapScene)
```

**Оценка:** ~8-11 ч (6 тикетов, с учётом реюза Gathering-кода).

---

## 10. Итоговая рекомендация

1. **Не менять** старый `Crafting_system/` дизайн — он верно спроектирован архитектурно.
2. **Обновить REUSE-список** — 8 из 16 пунктов теперь берутся из Gathering/MetaRequirement (вместо Market).
3. **Упростить реализацию:**
   - Tool check → MetaRequirement (как Mining)
   - Выдача корабля → ключ-предмет в инвентарь (не GrantKeyToClient)
   - UI → отдельный UIDocument (не tab в MarketWindow)
   - InventoryWorld.TryRemoveByItemId → не нужно
4. **Экономия:** ~4 ч (28%) за счёт реюза готовых паттернов.
