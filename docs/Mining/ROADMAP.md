# ROADMAP — Resource Gathering: тикеты, порядок, риски

> **Цель:** распланировать имплементацию системы сбора ресурсов (Mining) по сессиям. Каждая сессия —
> compile-clean, верифицируемый incremental progress.
>
> **База:** `docs/Mining/00_OVERVIEW.md` (архитектура), `docs/Mining/10_DESIGN.md` (дизайн),
> `docs/Mining/20_IMPLEMENTATION_PLAN.md` (план).
>
> **Обновлено 2026-06-10.** Все дизайн-решения приняты (Q1-F, Q2-no-movement, Q3-MetaRequirement).
> Код не начат — roadmap подготавливает старт.

---

## §0 Что осталось / текущий open work

> **TL;DR:** вся система сбора — новая, ничего не реализовано. 7 тикетов, ~8-11 ч чистого кода.
> После Фазы 1 можно будет подойти к ResourceNode нажать F и через N секунд получить предмет в инвентарь.

### Открыто (нужно делать)

| # | Тикет | Milestone | Приоритет | Скоуп | Фаза |
|---|-------|-----------|-----------|-------|------|
| 1 | **T-G01** — ScriptableObject `ResourceNodeConfig` | M1 | 🔴 High (~0.5-1 ч) | SO с gatherSeconds/maxHarvests/cooldownSeconds/resultItem/animParams | 1 |
| 2 | **T-G02** — `ResourceNode` NetworkBehaviour | M1 | 🔴 High (~2-3 ч) | Gather state machine, MetaRequirement tool check, `OnNetworkSpawn`, trigger registration, client MetaReq subscription | 2 |
| 3 | **T-G03** — `GatheringServer` RPC hub | M2 | 🔴 High (~1.5-2 ч) | RPC hub + server tick (0.5s) + GatherResult DTO + cooldown timer | 3 |
| 4 | **T-G04** — `GatheringClientState` + `GatheringToastController` | M2 | 🔴 High (~2-3 ч) | Client projection, UIDocument + ProgressBar, bind events, auto-spawn | 4 |
| 5 | **T-G05** — `InteractableManager` + F-key | M2 | 🔴 High (~0.5-1 ч) | RegisterResourceNode/FindNearestResourceNode, `NetworkPlayer.TryGatherNearestNode()`, TargetRPC | 5 |
| 6 | **T-G06** — ResourceNode client animation | M3 | 🟡 Med (~1-1.5 ч) | `GatherAnimationCoroutine` (scale-pulse + emissive), `DisappearCoroutine`, `_replicatedState.OnValueChanged` | 5.5 |
| 7 | **T-G07** — Scene placement + префабы | M3 | 🟡 Med (~0.5 ч) | Prefab (NetworkObject + ResourceNode + MetaRequirement + Collider), BootstrapScene `[GatheringServer]`, WorldScene placement | 6 |

### DEFERRED (post-MVP, не блокер)

| # | Тикет | Причина |
|---|-------|---------|
| 1 | **Tool durability** | Инструменты не расходуются. MVP — проверка наличия. Механика расхода — v2. |
| 2 | **Multi-player на одном узле** | Сейчас только один игрок за раз. Очередь/сплит — v2. |
| 3 | **Player gather animation** | `StateHasher` → `PlayerGatherState`. Требует refactor player state machine. Отдельная задача. |
| 4 | **Node tiering** | Tier 1/2/3 с разными инструментами. Нужен контент. |
| 5 | **Gather skill / уровень** | Скорость сбора зависит от навыка. |
| 6 | **Random yield** | 1-3 предмета за сбор. |
| 7 | **Persistence** | Состояние узла (depleted/cooldown) сохраняется между рестартами. |
| 8 | **Proximity auto-gather** | Сбор без F (для растений). |

---

## §1 Принципы разбивки

- **Один тикет = одна сессия = ~30-120 мин кодинга** (по объёму).
- **Каждый тикет компилируется и не ломает существующее.**
- **Тестирование — пользователь** (юзер запускает Unity, проверяет в Editor/PlayMode).
- **Без `.meta`/`.asmdef` writes** (см. AGENTS.md HARD RULES).
- **Cleanup-тикеты отдельно от feature-тикетов** (не смешивать).
- **Pitfall-листы:**
  - `10_DESIGN.md` §3 — 10 edge-cases.
  - Настоящий документ §6 — риски по фиксам.
- **Additive-only** — не удалять существующий код, добавлять рядом (кроме явного cleanup).

---

## §2 Финальный порядок тикетов (с зависимостями)

```
T-G01 (ResourceNodeConfig SO)
   ↓
T-G02 (ResourceNode NetworkBehaviour + MetaReq + trigger)
   ↓
T-G03 (GatheringServer RPC hub + server tick)
   ↓
T-G04 (GatheringClientState + GatheringToastController + UXML/USS)
   ↓
T-G05 (InteractableManager + F-key in NetworkPlayer)
   ↓
T-G06 (ResourceNode client animation: scale-pulse + emissive)
   ↓
T-G07 (Prefab + BootstrapScene + WorldScene placement)
```

**Сводка по милстоунам:**

| Milestone | Тикеты | Суть |
|-----------|--------|------|
| **M1 — Data + Node state** | T-G01, T-G02 | SO существует, ResourceNode спавнится, проверяет инструмент, регистрируется |
| **M2 — Server + UI** | T-G03, T-G04, T-G05 | Федератор собирает, тост показывает прогресс, F-key работает |
| **M3 — Visuals + deployment** | T-G06, T-G07 | Анимация узла, префаб в сцене |

---

## §3 Тикеты (детально)

### T-G01 — ResourceNodeConfig ScriptableObject (Phase 1, ~0.5-1 ч) ✅ DONE 2026-06-10

**Файл:** `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs`

**Скоуп:**
- `[CreateAssetMenu(fileName = "ResourceNode_", menuName = "Project C/Resource Node Config")]`
- Поля:
  - `_resultItem: ItemData` — что выпадает
  - `_gatherSeconds: float` (3f) — время сбора
  - `_maxHarvests: int` (5) — сколько раз можно собрать до cooldown
  - `_cooldownSeconds: float` (60f) — перезарядка
  - `_gatherRange: float` (3f) — дистанция сбора
  - `_nodeDisplayName: string` — для UI
  - `_animScaleAmplitude: float` (0.15f) — амплитуда пульсации scale
  - `_animPulsePeriod: float` (0.4f) — период пульсации
  - `_animHiddenDuration: float` (0.3f) — анимация появления/исчезания
  - `_animIdleEmission: Color` — цвет emission в покое
  - `_animGatherEmission: Color` — цвет emission при сборе
- Runtime: `ResultItemId: int` (lazy resolve через InventoryWorld)
- Метод: `ResolveItemIds(InventoryWorld)`

**Тестовый asset:** `Resources/ResourceNodes/ResourceNode_IronVein.asset` (железная руда, gatherSeconds=3, maxHarvests=5, cooldownSeconds=60, без инструмента)

**Verify:** ✅ Создать .asset через `Assets/Create/Project C/Resource Node Config`. Инспектор показывает все поля. `ResolveItemIds()` отрабатывает без ошибок.

**Фактически реализовано (2026-06-10):**
- `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs` (215 LOC) — все поля + ResolveItemIds() + OnValidate.
- 3 .asset в `Assets/_Project/Resources/ResourceNodes/`:
  - `ResourceNode_IronVein.asset` — результат "Железная руда", без инструмента, 3 сек, 5 harvests, 60 сек кулдаун
  - `ResourceNode_CopperVein.asset` — результат "Медная руда", требует "ShipLight" как tool (placeholder), 3 сек, 5 harvests, 45 сек кулдаун
  - `ResourceNode_PlantHerb.asset` — результат "Кристаллическая пыль" (используем как "траву"), без инструмента, 1.5 сек, 3 harvests, 30 сек кулдаун
- Compile: 0 errors (4 warnings от старого кода, не моих).

**Tool "ShipLight" placeholder:** для MVP используем существующий `Item_Key_ShipLight.asset` как "инструмент-сюрприз" (выглядит странно, но работает). Когда добавим `Item_Tool_Pickaxe.asset` в Resources/Items — переключим `_requiredTool` IronVein/CopperVein на кирку. Сейчас **не блокирует** тест: для IronVein tool = null (без требования), для PlantHerb tool = null, для CopperVein — нужен ShipLight.

**Risk:** low. Чистый ScriptableObject.

---

### T-G02 — ResourceNode NetworkBehaviour (Phase 2, ~2-3 ч) ✅ DONE 2026-06-10

**Файл:** `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs`

**Скоуп:**
- `[SerializeField] ResourceNodeConfig _config`
- `[SerializeField] MetaRequirement _metaRequirement` — ссылка на компонент на том же GameObject
- `enum ResourceNodeState` (Idle, Occupied, Depleted, Cooldown)
- `NetworkVariable<ResourceNodeState> _replicatedState` (NetworkVariableReadPermission.Everyone, WritePermission.Server)
- Server-only state: `_currentHarvests`, `_currentGathererClientId`, `_gatherStartServerTime`, `_cooldownEndServerTime`
- **НЕТ** `_gatherStartPosition` (Q2: движение не прерывает)

**Server методы:**
- `CanStartGather(clientId, out reason)` — проверка: `_currentState == Idle` + `_metaRequirement.CanPlayerUse()` (если null — без требований)
- `TryStartGather(clientId, serverTime)` → `_state = Occupied`
- `TickGather(serverTime)` → `InProgress(progress)` или `CompleteGather()` или `Interrupted`
- `CompleteGather()` → `AddItemDirect` + декремент + Depleted/Idle
- `CancelGather(reason)` → Idle

**Client методы:**
- Подписка на `MetaRequirementClientState.OnAccessAllowed` (паттерн LockBox)
  - Lazy-subscribe в `Update()` (как LockBox)
  - `OnMetaAccessAllowed(netId)` → `GatheringClientState.RequestStartGather(netId)`
- `_replicatedState.OnValueChanged` (заготовка для T-G06 анимации)

**Lifecycle:**
- `OnNetworkSpawn` (server) → `GatheringServer.RegisterNode(this)`
- `OnNetworkDespawn` → `GatheringServer.UnregisterNode(this)`
- `OnTriggerEnter/Exit` → `InteractableManager.RegisterResourceNode / UnregisterResourceNode`

**Verify:**
- ✅ Compile: 0 errors
- Start host → ResourceNode в сцене → NetworkBehaviour.IsSpawned = true
- `_replicatedState` читается на клиенте

**Risk:** medium. Сетевая логика + подписка на MetaRequirementClientState. Паттерн LockBox уже работает.

**Фактически реализовано (2026-06-10):**
- `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs` (~430 LOC) — NetworkBehaviour + state machine + MetaReq subscription.
- enum `ResourceNodeState` (Idle/Occupied/Depleted/Cooldown) + NetworkVariable + public getter `CurrentState`.
- Server API: `CanStartGather`, `TryStartGather`, `TickGather`, `CompleteGather`, `CancelGather`, `TickCooldown`.
- Client API: lazy-subscribe на `MetaRequirementClientState.OnAccessAllowed` (паттерн LockBox) → `OnMetaAccessAllowed(netId)` → рефлективный вызов `GatheringClientState.RequestStartGather` (null-safe — T-G04 создаст тип).
- `OnTriggerEnter/Exit` → `InteractableManager.RegisterResourceNode / UnregisterResourceNode`.
- `OnNetworkSpawn` (server) → `ResolveItemIds` + рефлективная регистрация в `GatheringServer` (null-safe).
- `OnNetworkDespawn` (server) → unregister.
- `InteractableManager.cs` — `+_resourceNodes` список + `Register/Unregister/FindNearest` (3 новых метода, 1 в ClearAll).

**3 ResourceNode размещены в WorldScene_0_0 рядом с Mira (40000, 2502.77, 39985):**
- `[ResourceNode_IronVein]` @ (40000, 2502.77, 40020) — без tool, "Железная руда", 3с/5harv
- `[ResourceNode_CopperVein]` @ (40020, 2502.77, 40000) — требует ShipLight, "Медная руда", 3с/5harv
- `[ResourceNode_PlantHerb]` @ (40000, 2502.77, 39980) — без tool, "Кристаллическая пыль", 1.5с/3harv
- Каждый: BoxCollider (isTrigger, size 1×1×1, localScale 1.5×1.5×1.5) + NetworkObject (scene-placed — будет spawn'ен ScenePlacedObjectSpawner'ом) + ResourceNode + MetaRequirement (только на CopperVein).

**Key Lessons:**
- Namespace collision: `ProjectC.ResourceNode` + `using ProjectC.MetaRequirement` → C# считает `MetaRequirement` вложенным namespace, путает с type → `CS0118`. **Fix**: alias `using MetaReq = ProjectC.MetaRequirement.MetaRequirement;`
- `InventoryWorld.AddItemDirect` возвращает `InventoryResultDto` (struct), не `bool`. Проверка успеха через `result.IsSuccess` (P36: enum pitfall).
- `NetworkManager.ServerTime.Time` это `double`, `NetworkTime` — struct (не nullable). Упрощённо: `nm != null ? (float)nm.ServerTime.Time : Time.realtimeSinceStartup`.
- `GatheringServer` и `GatheringClientState` ещё не созданы (T-G03, T-G04). В T-G02 связь идёт через рефлексию (`Type.GetType` + `GetMethod`) — null-safe.

---

### T-G03 — GatheringServer RPC hub (Phase 3, ~1.5-2 ч)

**Файл:** `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs`

**Скоуп:**
- `public static GatheringServer Instance { get; private set; }`
- `Dictionary<ulong, ResourceNode> _nodes` — registry (netId → node)
- `Dictionary<ulong, ActiveGatherJob> _activeGathers` — clientId → активный сбор
- `TICK_INTERVAL = 0.5f`
- `RegisterNode(ulong netId, ResourceNode node)`
- `UnregisterNode(ulong netId)`

**RPCs (client → server):**
- `RequestStartGatherRpc(ulong nodeNetId)`
  - Rate-limit
  - `_activeGathers.ContainsKey(clientId)` → deny (один сбор за раз)
  - `_nodes.TryGetValue` → deny если нет
  - `CheckDistance(clientId, nodePos, _gatherRange)`
  - `node.TryStartGather(clientId, serverTime)` → `_activeGathers[clientId] = job`
  - `SendGatherResult(InProgress(0f))`
- `RequestCancelGatherRpc()` → cancel + Idle

**Server tick (Update):**
- `Time.time < _nextTickTime` → skip; `_nextTickTime = Time.time + TICK_INTERVAL`
- For each active gather: `node.TickGather(serverTime)` → switch (InProgress/Completed/Interrupted)
- `SendGatherResult(client, GatherResult)` — TargetRPC
- Cooldown: Depleted → ждать `_cooldownSeconds` → Idle + `_replicatedState` sync

**DTO:** `GatherResult` enum (InProgress/Completed/Interrupted/Denied/Cancelled) + progress + itemName + quantity

**Verify:**
- StartHost → GatheringServer.Instance != null
- `RequestStartGatherRpc` → `TryStartGather` → `_activeGathers` populated
- Tick fires every 0.5 sec → `CompleteGather` after `_gatherSeconds`

**Risk:** low-medium. Паттерн — копия `InventoryServer`.

---

### T-G04 — GatheringClientState + GatheringToastController (Phase 4, ~2-3 ч)

**Файлы:**
- `Assets/_Project/Scripts/ResourceNode/GatheringClientState.cs`
- `Assets/_Project/Scripts/ResourceNode/GatheringToastController.cs`
- `Assets/_Project/UI/ResourceNode/GatheringToast.uxml`
- `Assets/_Project/UI/ResourceNode/GatheringToast.uss`

**GatheringClientState.cs:**
- Singleton (client-only): `OnGatherResultReceived(GatherResult)`
- Events: `OnGatherProgress(float 0..1)`, `OnGatherCompleted(string, int)`, `OnGatherInterrupted(string)`, `OnGatherDenied(string)`, `OnGatherCancelled()`
- `RequestStartGather(ulong nodeNetId)` → `GatheringServer.RequestStartGatherRpc`
- `RequestCancelGather()` → `GatheringServer.RequestCancelGatherRpc`
- Timeout: если нет `InProgress` > 2.5 сек → `OnGatherInterrupted("Таймаут")`

**GatheringToastController.cs:**
- OnEnable: создаёт UIDocument на корне (reuse существующий PanelSettings)
- Находит `_progressBar` и `_label` по имени
- Подписка на `GatheringClientState` events:
  - `OnGatherProgress(p)` → `_progressBar.value = p`
  - `OnGatherCompleted(name, qty)` → `_progressBar.value = 1.0`, `_label.text = $"Добыто: {name} × {qty}"`, hide через 0.5 сек
  - `OnGatherInterrupted/Denied(reason)` → flash-fill прогресс-бар за 0.2с до 1.0, `_label.text = reason`, hide через 1 сек
  - `OnGatherCancelled()` → hide через 0.3 сек
- OnDisable: отписка

**UXML структура:**
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="GatheringToastRoot">
    <ui:VisualElement name="ToastContainer">
      <ui:Label name="ToastLabel" text="Добыча: Руда" />
      <ui:ProgressBar name="GatherProgressBar" low-value="0" high-value="1" value="0" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

**Auto-spawn:** `NetworkManagerController.Awake` → `CreateGatheringClientState()` (по аналогии с InventoryClientState).

**Verify:**
- Compile: 0 errors
- `GatheringClientState.Instance != null` в Play Mode
- `OnGatherProgress(0.5)` → `_progressBar.value == 0.5`
- Toast показывается/скрывается корректно

**Risk:** medium. UI Toolkit pitfalls (см. §6.3). **ВАЖНО:** читать `T-Q11b_c_session_log.md` (8 постоянных UI Toolkit багов) перед написанием UXML/USS.

---

### T-G05 — InteractableManager + F-key (Phase 5, ~0.5-1 ч)

**Файлы:** `InteractableManager.cs`, `NetworkPlayer.cs`

**InteractableManager.cs:**
- `private static readonly List<ResourceNode> _resourceNodes = new List<ResourceNode>(16)`
- `RegisterResourceNode(ResourceNode node)` / `UnregisterResourceNode(ResourceNode node)`
- `FindNearestResourceNode(Vector3 position, float range)` — как `FindNearestPickup`

**NetworkPlayer.cs:**
- `TryGatherNearestNode()` — в F-key блоке **перед** boarding
  - `InteractableManager.FindNearestResourceNode(position, pickupRange)`
  - Race protection (reuse `_lastCanUseRequestTime` / `_pendingCanUseInteractableId`)
  - `MetaRequirementClientState.Instance?.RequestCanUse(nearest.NetworkObjectId)`

**TargetRPC в NetworkPlayer:**
- `ReceiveGatherResultTargetRpc(GatherResult result)` → `GatheringClientState.Instance?.OnGatherResultReceived(result)`

**Порядок F-key в Update():**
```csharp
if (TryGatherNearestNode()) return;  // new — highest priority
if (TryBoardNearestShip()) return;   // existing
// ... остальные F-действия
```

**Verify:**
- Compile: 0 errors
- Play Mode: подойти к ResourceNode → нажать F → MetaRequirement check → OnAccessAllowed → сбор начинается
- Если нет инструмента → MetaRequirementToast "Нужен ..."
- Если F рядом с узлом + кораблём → приоритет сбора

**Risk:** low. Паттерны уже есть (FindNearestPickup, _pendingCanUseInteractableId).

---

### T-G06 — ResourceNode client animation (Phase 5.5, ~1-1.5 ч)

**Файл:** `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs` (дополнение к T-G02)

**Скоуп:**
- `_replicatedState.OnValueChanged` подписка:
  - `Idle` → стоп `_gatherAnimCoroutine`
  - `Occupied` → `StartCoroutine(GatherPulse())`
  - `Depleted` → стоп + `StartCoroutine(Disappear())`
  - `Cooldown` → `gameObject.SetActive(false)`
  - Idle (после Cooldown) → `SetActive(true)`, scale → 1.0 + overshoot

**GatherPulse coroutine (LOOP):**
- `float t = Mathf.Sin(Time.time * 2f * Mathf.PI / _config._animPulsePeriod)`
- `transform.localScale = Vector3.one * (1.0f + _config._animScaleAmplitude * t)`
- emissive = `Color.Lerp(_config._animIdleEmission, _config._animGatherEmission, (t + 1f) / 2f)`
- Apply через `MaterialPropertyBlock` (как LockBox)

**Disappear coroutine:**
- `float t = 0` → `1` за `_config._animHiddenDuration`
- `transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t)`
- После завершения: `gameObject.SetActive(false)`

**Не входит в MVP:** анимация персонажа (StateHasher → PlayerGatherState — post-MVP).

**Verify:**
- Play Mode: нажать F на узле → узел пульсирует (scale + emission)
- Сбор завершён → анимация останавливается
- Depleted → исчезает (scale → 0)
- Cooldown → скрыт
- Cooldown завершён → появляется

**Risk:** low. Паттерн скопирован из LockBox. MaterialPropertyBlock уже используется.

---

### T-G07 — Prefab + сцена (Phase 6, ~0.5 ч)

**Файлы:** префаб + BootstrapScene.unity + WorldScene_0_0.unity

**Префаб `ResourceNode_Default.prefab`:**
- GameObject с: `NetworkObject`, `Collider` (isTrigger), `MeshRenderer` (сфера/куб для теста), `ResourceNode` компонент, **`MetaRequirement` компонент**
- `ResourceNode._metaRequirement` → ссылка на свой `MetaRequirement`
- `ResourceNode._config` = `IronVein.asset`
- `MetaRequirement._requiredItems[0]` = tool (кирка), или пусто (без требований)
- `Renderer` нужен для анимации (MaterialPropertyBlock)

**BootstrapScene.unity:**
- Новый GameObject `[GatheringServer]` с `NetworkObject` + `GatheringServer` (server-only)
- `GatheringToastController` на корневом UIDocument (reuse существующий PanelSettings)

**WorldScene_0_0.unity:**
- 3-5 ResourceNode в кластере @ (40000, 2510, 40000) рядом с тестовым Mira
- Разные конфиги: IronVein (руда), CopperVein (медь), PlantHerb (трава без инструмента)

**Verify:**
- Загрузить WorldScene_0_0 → 3-5 ResourceNode видны
- StartHost → все ResourceNode зарегистрированы в GatheringServer
- `[GatheringServer]` GameObject active в BootstrapScene

**Risk:** low. Scene placement.

---

## §4 Milestones

| Milestone | Тикеты | Что работает |
|-----------|--------|--------------|
| **M1 — Data + Node state** | T-G01, T-G02 | `ResourceNodeConfig` SO с полями. `ResourceNode` спавнится как NetworkBehaviour, проверяет инструмент через `MetaRequirement`, регистрируется в `GatheringServer`. Состояние (Idle/Occupied/Depleted) реплицируется. `OnTriggerEnter/Exit` — регистрация в `InteractableManager`. |
| **M2 — Server + UI** | T-G03, T-G04, T-G05 | `GatheringServer` принимает RPC, тикает таймер, выдаёт предмет в инвентарь. `GatheringToast` показывает ProgressBar + имя предмета. F-key запускает сбор через MetaReq → OnAccessAllowed → gather. |
| **M3 — Visuals + deployment** | T-G06, T-G07 | Узел пульсирует при сборе, исчезает при истощении. Префаб готов, `[GatheringServer]` в BootstrapScene, ResourceNode в WorldScene_0_0. Play Mode: F → сбор → тост → анимация → предмет в инвентаре. |

**Рекомендуемый темп:** 1-2 тикета за сессию.

---

## §5 Оценка общей трудоёмкости

| Милстоун | Тикеты | ~Часов |
|----------|--------|--------|
| M1 — Data + Node state | T-G01, T-G02 | ~3-4 ч |
| M2 — Server + UI | T-G03, T-G04, T-G05 | ~4.5-6 ч |
| M3 — Visuals + deployment | T-G06, T-G07 | ~1.5-2 ч |
| **TOTAL** | **7 тикетов** | **~9-12 ч** |

**С учётом фикс-итераций (опыт квестов показывает +30-50% на неожиданные баги): ~12-18 ч, 3-5 сессий.**

---

## §6 Риски (прогноз)

| # | Риск | Статус | Митигация |
|---|------|--------|-----------|
| 1 | **Scene-placed NRE (NetworkObject)** | 🟡 unknown | Проверить `InScenePlacedSourceGlobalObjectIdHash`. Если ResourceNode не спавнится — `ScenePlacedObjectSpawner` в BootstrapScene уже жив. См. AGENTS.md §scene-placed. |
| 2 | **MetaRequirementClientState race (lazy-subscribe)** | 🟡 unknown | Паттерн LockBox уже работает (lazy-subscribe в Update). Если ResourceNode создаётся после MetaRequirementClientState — гарантия подписки. |
| 3 | **UI Toolkit UIDocument pitfalls** | 🟡 unknown | **Обязательно** прочитать `docs/NPC_quests/old_session_log/T-Q11b_c_session_log_2026-06-08.md` (8 persistent UI Toolkit bugs). PanelSettings, PickingMode, styleSheets, cursor. |
| 4 | **`InventoryWorld.AddItemDirect` отсутствует / изменился API** | 🟡 low | Проверить сигнатуру: `AddItemDirect(ulong clientId, int itemId, ItemType type)`. Если не совпадает — адаптировать. |
| 5 | **NetworkPlayer F-key конфликт** | 🟡 low | `TryGatherNearestNode()` ДО `TryBoardNearestShip()`. Если корабль и узел рядом — F запускает сбор. |
| 6 | **Таймаут тоста (2.5 сек без InProgress)** | 🟡 low | Если сервер не тикает (пауза/лаг) — toast скрывается. Это OK для MVP — игрок видит что сбор прерван. |
| 7 | **Multiple GatheringServer instance** | 🟡 low | Singleton guard (`Instance` setter warns if already set). DontDestroyOnLoad. |
| 8 | **Cooldown race при одновременном завершении сбора и достижении maxHarvests** | 🟡 low | Защита: `CompleteGather` атомарная (проверка не требуется — node в Occupied, только один gatherer). |
| 9 | **MaterialPropertyBlock глобальное состояние** | 🟡 low | LockBox уже использует MPB. `_mpb.Clear()` перед `ApplyBaseAppearance()`. |
| 10 | **F-key срабатывает дважды в одном кадре (холостой вызов)** | 🟡 low | Race protection через `_lastCanUseRequestTime + CAN_USE_REQUEST_TIMEOUT` (как у MetaRequirement). |

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
> **Files modified/new:**
> ```
> M ...
> A ...
> ```

### §7.2 T-G02 — ResourceNode NetworkBehaviour (2026-06-10)

**Verify:**
- ✅ Compile: 0 errors (`refresh_unity scope=all` + `read_console types=["error"]`)
- ✅ 3 ResourceNode GO созданы в WorldScene_0_0, сцена сохранена
- ✅ IronVein + PlantHerb без MetaRequirement, CopperVein с MetaRequirement (logic=All, 1 item ShipLight)
- ✅ BoxCollider isTrigger=true, localScale 1.5

**Key Lessons:**
- **Namespace collision pitfall (новый, расширяет P4):** namespace `ProjectC.ResourceNode` + `using ProjectC.MetaRequirement` → компилятор C# считает `MetaRequirement` "вложенным namespace" (даже при разных namespace!), путает с type → `CS0118`. **Fix**: `using MetaReq = ProjectC.MetaRequirement.MetaRequirement;` alias
- **AddItemDirect returns DTO, not bool:** `InventoryWorld.AddItemDirect(ulong, int, ItemType)` returns `InventoryResultDto` (struct). Success check: `result.IsSuccess` (P36)
- **NetworkTime — not nullable:** `NetworkManager.Singleton.ServerTime.Time` это `double`, `NetworkTime` это struct. `?.Time` не компилируется
- **Рефлективная связь для forward-dependency:** GatheringServer / GatheringClientState ещё не созданы (T-G03/T-G04). Связь через `Type.GetType` + `GetMethod` — null-safe до появления типов. Заменим на прямую в T-G03

**Files modified/new:**
```
A Assets/_Project/Scripts/ResourceNode/ResourceNode.cs (430 LOC)
M Assets/_Project/Scripts/Core/InteractableManager.cs (+_resourceNodes, +3 methods, +ClearAll)
M Assets/_Project/Scenes/World/WorldScene_0_0.unity (+3 ResourceNode GO)
A docs/Mining/ROADMAP.md (T-G02 ✅ DONE)
```

### §7.1 T-G01 — ResourceNodeConfig SO (2026-06-10)

**Verify:**
- ✅ Compile: 0 errors (`refresh_unity scope=all` + `read_console types=["error"]` → 0 моих)
- ✅ 3 .asset созданы через `ScriptableObject.CreateInstance` + `AssetDatabase.CreateAsset`
- ✅ Поля заполнены через `SerializedObject.FindProperty(...).objectReferenceValue` (P32: Roslyn resolves correct m_Script GUID)
- ✅ `_resultItem`, `_requiredTool`, `_gatherSeconds`, `_maxHarvests`, `_cooldownSeconds`, `_gatherRange`, `_nodeDisplayName` — все bind'ятся корректно

**Key Lessons:**
- `Item_Resources_*` уже существуют (русские имена в файлах, OK для editor) — можно использовать без создания новых
- `Item_Key_ShipLight` взят как placeholder для `_requiredTool` (нет Item_Tool_Pickaxe, не критично для MVP)
- Roslyn `CreateInstance(string typeName)` (по имени, не generic) + `as ScriptableObject` — обходной путь для новых типов в текущей сессии (P27: новые типы не видны `AddComponent<T>`, но `CreateInstance(string)` работает)

**Files modified/new:**
```
A Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs
A Assets/_Project/Resources/ResourceNodes/ResourceNode_IronVein.asset
A Assets/_Project/Resources/ResourceNodes/ResourceNode_CopperVein.asset
A Assets/_Project/Resources/ResourceNodes/ResourceNode_PlantHerb.asset
A docs/Mining/ROADMAP.md (T-G01 ✅ DONE)
```

---

## §8 Сводный статус (1 строка)

**M1–M3 = 📋 PLANNED.** 7 тикетов. Код не начат. Дизайн-решения утверждены (F-key, MetaRequirement tool check, без distance check). Оценка: ~9-12 ч чистого кода, ~12-18 ч с фиксами. Старт — по готовности.

**Обновлено 2026-06-10:** T-G01 ✅ DONE (ResourceNodeConfig SO + 3 .asset'а). T-G02 ✅ DONE (ResourceNode NetworkBehaviour + 3 GO в WorldScene_0_0 + InteractableManager.FindNearestResourceNode). 5 тикетов осталось.

---

*См. `00_OVERVIEW.md` для обзора архитектуры, `10_DESIGN.md` для детального дизайна, `20_IMPLEMENTATION_PLAN.md` для пошагового плана.*
