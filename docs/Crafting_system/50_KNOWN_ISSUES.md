# Crafting System — Known Issues / Open Risks

> **Цикл:** Проектирование. Этот документ — **живой** трекер рисков, edge-cases, открытых решений.
> **Обновляется:** после каждой имплементации и теста.

---

## 1. Открытые архитектурные вопросы (требуют решения до кодирования)

> Скопировано из `00_OVERVIEW.md` §9. Здесь — с трекингом статуса.

| # | Вопрос | Статус | Кто решает | Целевая сессия |
|---|--------|--------|------------|----------------|
| Q1 | Cargo корабля как источник ресурсов? | 🟡 Рекомендация: НЕТ в MVP | Ты + автор GDD | Сессия 2 (перед имплементацией) |
| Q2 | Persistence: `destroyWithScene=true` vs `IPlayerDataRepository`? | 🟡 Рекомендация: `destroyWithScene=true` в MVP | Ты | Сессия 2 |
| Q3 | Fallback `Time.realtimeSinceStartup` если нет server time? | 🟡 Рекомендация: да, в `CraftingServer` через `MarketTimeService.Instance ?? NetworkManager.ServerTime` | Я (при имплементации) | Сессия 3 |
| Q4 | Универсальные vs локационные рецепты? | 🟢 Рекомендация: `allowedRecipes[]` в station config | Закрыто | — |
| Q5 | `CompletedJobs` лимит? | 🟡 Рекомендация: 10 на клиента в MVP | Ты | Сессия 2 |
| Q6 | `requiredSkillLevel` заложить сразу? | 🟢 Рекомендация: да, default 0 | Закрыто | — |
| Q7 | Completed Job ждёт заборщика? | 🟢 Рекомендация: да, `state=Completed` без автосбора | Закрыто | — |

**🟢** = закрыт (рекомендация одобрена), **🟡** = требует обсуждения, **🔴** = блокер для имплементации.

---

## 2. Технические риски (выявлены при анализе)

### 2.1 NetworkVariable explosion (50 станций × 6 переменных)

**Риск:** На каждый `CraftingStation` — 5 `NetworkVariable` + 1 `NetworkList`. 50 станций = 300+ NetworkVariable. Это **на пределе** NGO 2.x (рекомендуется < 1000 на мир, но лаги начинаются уже с 500).

**Решение MVP:** оставить как есть (развёрнутые). Создаст 50-100 станций в худшем случае.

**Решение Phase 2:** refactor на `NetworkVariable<CraftingJobDto>` (один struct = 1 переменная на станцию).

**Tracking:** После MVP — benchmark на 50 станций в сцене. Если лагает — refactor.

### 2.2 Sprite в NetworkSerialize (RecipeDto.icon)

**Риск:** `Sprite` — UnityEngine.Object, не сериализуется через `BufferSerializer<T>`. Нельзя положить в DTO.

**Решение MVP:** клиент держит `_recipeCache: Dictionary<int, RecipeData>` (как `InventoryServer._itemCache`). Сервер шлёт только `recipeId` → клиент лезет в кэш.

**Решение Phase 2:** если рецепты появятся во время игры (а не в Resources) — прекешировать через `RecipeData[]` + `Addressables`.

**Edge:** Если `RecipeData` на клиенте **нет** в кэше (например, только что добавлен в проект) — UI показывает `displayName="???"`. **Превентивно:** `CraftingClientState` грузит ВСЕ `RecipeData` из `Resources/Crafting/Recipes/` в `Awake` (как `InventoryServer` грузит `Resources/Items/`).

### 2.3 RecipeData удалён / переименован

**Риск:** `CraftingStation._config.allowedRecipes[2]` теперь `MissingReferenceException`.

**Решение:** Unity подхватывает SO по GUID — если SO переименован (но не удалён) — ссылка жива. Если удалён — `null` в массиве.

**Превентивно:** `OnValidate` в `CraftingStationConfig` — warning при `null` в `allowedRecipes`. `CraftingWorld.RegisterRecipe` — warning при null.

### 2.4 Race: AddIngredient + StartCraft

**Сценарий:** A добавляет steel (RPC #1), B жмёт StartCraft (RPC #2) в тот же фрейм.

**Решение:** NGO RPC обрабатываются **в порядке прихода на сервер**. Если #1 пришёл раньше — buffer+1 steel, потом StartCraft видит buffer, ок. Если #2 раньше — StartCraft видит старый buffer, Fail "Insufficient", потом A добавляет, Ok. Snapshot шлётся после каждой операции — UI консистентен.

**Тест:** запустить 2 клиента, спамить RPC → финальное состояние корректное.

### 2.5 Multiple stations in same chunk → коллизия NetworkObjectId

**Риск:** 2 `CraftingStation` в одной сцене с одинаковым префабом → NetworkObjectId коллизия?

**Решение:** `NetworkObjectId` назначается сервером при spawn, уникальный в NetworkManager. Префаб ≠ id. ОК.

### 2.6 Host migration

**Сценарий:** Host крафтит, выходит. Клиент становится новым host'ом.

**Ришение:** `CraftingWorld` (POCO) — **умирает** при `NetworkManager.OnServerStopped`. Все Jobs теряются. Ресурсы **НЕ возвращаются** (нет сервера, чтобы вернуть). Это **катастрофа** для игрока.

**Превентивно (MVP):** документируем как known issue, не fix'им. Phase 2 — persistence через `PlayerPrefsRepository`.

**User-facing warning:** в `CraftingServer.OnNetworkSpawn` → если есть активные Jobs без `IPlayerDataRepository` → log: "WARNING: active jobs will be lost on host migration".

### 2.7 CargoSourceType (Phase 2)

**Сейчас:** `CraftingSourceType` имеет только `Inventory`, `Warehouse`. Если добавим `Cargo` — ломаем DTO (бинарная совместимость).

**Решение:** использовать `byte` enum с reserved values. Не менять `enum CraftingSourceType` без версионирования DTO.

### 2.8 UI Drag-and-drop при 0 recipe (нет выбранного рецепта)

**Сценарий:** Игрок открыл Crafting tab, **не выбрал** рецепт, попытался Drag&Drop steel.

**Решение:** `CraftingClientState.RequestAddIngredient(recipeId=-1, ...)` → сервер: `CraftingResultCode.NoRecipe` → toast: "Сначала выберите рецепт".

---

## 3. Anti-grief сценарии (требуют теста)

| # | Сценарий | Защита | Тест? |
|---|----------|--------|-------|
| 1 | Не-owner крадёт из буфера | `TryCollect/Cancel/Start` проверяют `ownerClientId` | ✅ Сценарий 3 в `30_VERIFICATION.md` |
| 2 | Спам RPC | `RateLimit` per-client | ✅ EditMode unit test |
| 3 | Buffer overflow (>32 ингредиентов) | `MaxBufferSize = 32` | ⚠️ TODO |
| 4 | Заказ в не-зоне | `IsInZone` check | ✅ |
| 5 | Ресурс забрали во время InProgress | Cancel + refund | ⚠️ TODO |
| 6 | Два игрока стартуют одновременно | Snapshot перезаписывает | ✅ Race test |
| 7 | Один игрок крафтит 100 рецептов подряд | `MaxActiveJobsPerClient = 3` (TODO) | ⚠️ |

---

## 4. Сценарии, которые мы НЕ покрываем в MVP (осознанно)

| # | Сценарий | Почему не в MVP | Когда |
|---|----------|-----------------|-------|
| 1 | Persistence между рестартами | `IPlayerDataRepository` integration — +2 дня | Phase 2 |
| 2 | Cargo как источник | `CargoSystem` локальный, не network | Phase 2 (отдельный ticket) |
| 3 | Очередь крафтов | Архитектурное усложнение | Phase 2+ |
| 4 | Уровни/ускорения станций | Требует progression | Phase 3 |
| 5 | Случайные выходы | Требует re-design RecipeOutput | Phase 3 |
| 6 | Топливо/инструменты | Расширение RecipeData | Phase 2 |
| 7 | NPC-крафт | Требует AI на станции | Phase 3 |
| 8 | Мобильный UI (touch) | UI Toolkit поддерживает, но не адаптировано | Phase 3 |
| 9 | Cross-server крафт | Server boundary | Когда MMO перейдёт на shard'ы |

---

## 5. Pitfall checklist (для разработчика)

> Скопировано из существующих skills и памяти. **Применять** при имплементации.

- [ ] **`refresh_unity` schema** = `{"scope": "all", "compile": "request", "wait_for_ready": true}`. **НЕ** `{"mode": "force"}`. (см. memory + `unity-mcp-orchestrator` SKILL.md)
- [ ] **Jagged arrays `int[][]`** валидны в RPC (NGO 2.x). Не надо flatten. (см. `unity-mcp-orchestrator` SKILL.md §pitfall 5)
- [ ] **`ScenePlacedObjectSpawner`** жив в bootstrap. Не удалять! Иначе scene-placed NetworkObject (станция) с hash=0 не заспавнится. (см. AGENTS.md §"Scene architecture")
- [ ] **`pickingMode = Ignore`** на `_root` когда окно закрыто. Иначе перекрывает UGUI. (см. `project-c-ui-as-tab` SKILL.md FIX #1)
- [ ] **`OnDisable` unsubscribe** для всех client-state subscriptions. (см. `project-c-ui-as-tab` SKILL.md cross-tab rule)
- [ ] **Cache update ВСЕГДА** в `HandleXSnapshotUpdated`, rebuild только при `_activeTab == "crafting"`. (см. `project-c-ui-as-tab` SKILL.md pitfall R3-005)
- [ ] **`MarkDirtyRepaint()`** schedule +50ms при первом `Show()`. (см. `project-c-ui-as-tab` SKILL.md FIX #4)
- [ ] **Не создавать `.asmdef` спекулятивно**. (см. AGENTS.md §"Never auto-create .meta or .asmdef")
- [ ] **Не создавать `.meta`** вручную. (см. AGENTS.md §"HARD RULES")
- [ ] **User run-tests, не мы.** После компиляции — сказать "go to Test Runner". (см. AGENTS.md §"Agent workflow")

---

## 6. Документация, которая может потребовать обновления после имплементации

- `docs/gdd/GDD_20_Progression_RPG.md` — добавить раздел "Crafting" со ссылкой на эту подсистему.
- `docs/gdd/GDD_22_Economy_Trading.md` — перекрёстная ссылка: ресурсы → craft → ship.
- `docs/gdd/GDD_10_Ship_System.md` — секция "Постройка корабля через craft".
- `docs/INVENTORY_SYSTEM.md` — обновить, т.к. устарел (v0.0.7). Привязать к этой подсистеме.
- `README.md` — добавить "Crafting" в список подсистем.

---

## 7. Решения, которые мы приняли (с обоснованием)

| # | Решение | Обоснование | Альтернативы |
|---|---------|-------------|--------------|
| D1 | Source = Inventory + Warehouse (не Cargo) | Cargo не network; MVP не блокирует. | Cargo, общий пул (отвергнуто) |
| D2 | Soft-lock (буфер на станции) | Игрок выбрал: `RESERVE`. | Hard-lock (отвергнуто) |
| D3 | Server-time, не `Time.deltaTime` | Игрок выбрал: "по серверу, serverweather". | Real-time, pausible (отвергнуты) |
| D4 | Один Job на станцию в MVP | Игрок выбрал: `MIN`. | Очередь (Phase 2) |
| D5 | One-shot, не stackable инвентарь | Существующий `InventoryData` так устроен. | Stackable (Phase 2) |
| D6 | Станция = `NetworkObject` + `IInteractable` | По образцу `NetworkChestContainer`. | Singleton manager (отвергнуто) |
| D7 | Job НЕ персистится в MVP | `IPlayerDataRepository` integration — большой объём. | Persistence (Phase 2) |
| D8 | Корабль выдаётся через `MetaRequirementRegistry.GrantKeyToClient` (новый метод) | Переиспользуем существующую инфраструктуру ключей. | Прямой спаун NetworkObject (отвергнуто) |
| D9 | Station общая (не per-player) | Игрок выбрал: "станции общие, кооп". | Per-player (отвергнуто) |
| D10 | Забирает заказчик (любой может положить) | Игрок выбрал. | Open access (отвергнуто) |

---

## 8. Следующий шаг (action items)

1. **Закрыть Q1, Q2, Q5** (см. §1) — обсуждение с тобой.
2. Создать `99_CHANGELOG.md` после MVP.
3. **При имплементации:** обновлять `50_KNOWN_ISSUES.md` при обнаружении новых рисков.

---

## 9. Интеграция с предыдущими аудитами

### 9.1 Аудит 2026-06-17 (AUDIT_2026-06-17.md)

Зафиксировал 11 проблем (B1-B5, D1-D3, T1-T5). **Все живы** — ни одна не исправлена по состоянию на 2026-07-09.

### 9.2 Аудит 2026-07-09 (AUDIT_2026-07-09.md)

Полный аудит существующего кода (16 файлов):
- 5 критических багов (B1-B5) — включая CollectRpc без owner-guard (B1)
- 7 технических проблем (T1-T7)
- 5 косметических (L1-L5)
- Поэтапный план исправлений на 2 сессии (~5 ч)

**Ссылка:** `docs/Crafting_system/AUDIT_2026-07-09.md`

---
