# Resource Gathering — Implementation Plan

> **Файл:** `docs/Mining/20_IMPLEMENTATION_PLAN.md`
> **Дата:** 2026-06-10
> **Оценка:** ~7-10 часов (6 фаз)
> **Порядок:** build-зависимый — каждая фаза компилируется независимо

---

## Prerequisites checklist

Перед стартом:

- [ ] Есть папка `Assets/_Project/Scripts/ResourceNode/` (создать)
- [ ] Есть папка `Assets/_Project/Resources/ResourceNodes/` (создать)
- [ ] `InventoryWorld.CountOf(clientId, itemId)` — существует ✅
- [ ] `InventoryWorld.AddItemDirect(clientId, itemId, type)` — существует ✅
- [ ] `InteractableManager.cs` — существует ✅
- [ ] `QuestToast.cs` — существует (для копирования паттерна) ✅
- [ ] Согласован: F-key для сбора (Q1), tool check без durability (Q3), один ResourceNode на все типы (Q4)
- [ ] Согласован: **вариант A** (standalone NetworkBehaviour, не наследник MetaRequirement)

---

## Фаза 1: `ResourceNodeConfig` ScriptableObject (~0.5-1 ч)

### Шаг 1.1: Создать `ResourceNodeConfig.cs`

**Файл:** `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs`

```csharp
// Полный код — см. 10_DESIGN.md §1.1
// [CreateAssetMenu(fileName = "ResourceNode_", menuName = "Project C/Resource Node Config")]
```

Поля:
- `_resultItem: ItemData` — что выпадает
- `_requiredTool: ItemData` — какой инструмент нужен (null = не требуется)
- `_gatherSeconds: float` (3f) — время сбора
- `_maxHarvests: int` (5) — сколько раз можно собрать
- `_cooldownSeconds: float` (60f) — перезарядка
- `_gatherRange: float` (3f) — дистанция
- `_nodeDisplayName: string` — для UI
- `_toastFormat: string` (опционально)
- Runtime: `ResultItemId`, `RequiredToolId` (lazy resolve)
- Метод: `ResolveItemIds(InventoryWorld)`

### Шаг 1.2: Создать тестовый .asset

- Например, `Resources/ResourceNodes/ResourceNode_IronVein.asset`
- `_resultItem` = IronIngot (ItemData.id=???)
- `_requiredTool` = Pickaxe (ItemData.id=???)
- `_gatherSeconds = 3`, `_maxHarvests = 5`, `_cooldownSeconds = 60`

### Шаг 1.3: Проверка

```bash
# Open Unity → Console: 0 errors
# Создать через Assets → Create → Project C → Resource Node Config
```

---

## Фаза 2: `ResourceNode` NetworkBehaviour (~2-3 ч)

### Шаг 2.1: Создать `ResourceNode.cs`

**Файл:** `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs`

**Полный код:** `10_DESIGN.md` §1.2-1.3

Ключевые элементы:
- `[SerializeField] ResourceNodeConfig _config`
- `[SerializeField] MetaRequirement _metaRequirement` — ссылка на компонент на том же GameObject
- enum `ResourceNodeState` (Idle, Occupied, Depleted, Cooldown)
- `NetworkVariable<ResourceNodeState> _replicatedState`
- Server-only: `_currentHarvests`, `_currentGathererClientId`, `_gatherStartServerTime`, `_cooldownEndServerTime`
  — **НЕТ** `_gatherStartPosition` (игрок может двигаться)
- `CanStartGather(clientId, out reason)` — проверка состояния + **`_metaRequirement.CanPlayerUse()`**
- `TryStartGather(clientId, serverTime)` — переход в Occupied (без playerPosition)
- `TickGather(serverTime)` — только проверка таймера, без проверки дистанции
- `CompleteGather()` — `AddItemDirect` + декремент + переход в Depleted/Idle
- `CancelGather()` — прерывание
- **Клиент:** подписка на `MetaRequirementClientState.OnAccessAllowed` (паттерн LockBox)
  → `OnMetaAccessAllowed(netId)` → `GatheringClientState.RequestStartGather(netId)`
- Lazy-subscribe в `Update()` (как LockBox)
- `OnNetworkSpawn` → регистрация в `GatheringServer.RegisterNode`
- `OnNetworkDespawn` → unregister
- `OnTriggerEnter/Exit` → `InteractableManager.RegisterResourceNode/UnregisterResourceNode`

### Шаг 2.2: Создать вспомогательные DTO

В этом же файле (или в отдельном `GatherTickResult.cs`):
- struct `GatherTickResult` (InProgress / Completed / Interrupted)
- struct `GatherResult` (для RPC: `Denied`, `InProgress`, `Completed`, `Interrupted`, `ServerError`, `Cancelled`)

### Шаг 2.3: Проверка

```bash
# Open Unity → Console: 0 errors
# Можно добавить пустой GameObject с NetworkObject + ResourceNode + ResourceNodeConfig → проверить что OnNetworkSpawn вызывается
```

---

## Фаза 3: `GatheringServer` RPC hub (~1.5-2 ч)

### Шаг 3.1: Создать `GatheringServer.cs`

**Файл:** `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs`

Паттерн — копия `InventoryServer.cs` / `MarketServer.cs`.

**Static:** `Instance`
**Registry:** `Dictionary<ulong, ResourceNode> _nodes`
**Active jobs:** `Dictionary<ulong, ActiveGatherJob> _activeGathers`
**RPCs:**
- `RequestStartGatherRpc(ulong nodeNetId)` — сервер: проверка distance + rate-limit → `node.TryStartGather()`
- `RequestCancelGatherRpc()` — отмена сбора
**Server tick:** `Update()` с TICK_INTERVAL = 0.5f
- Проход по `_activeGathers` → `node.TickGather()` → результат клиенту
- Cooldown-таймер: `Depleted` → ждём `_cooldownSeconds` → переход в Idle + `NetworkVariable` sync
**TargetRPC:** `SendGatherResult(clientId, GatherResult)` — через `NetworkPlayer.ReceiveGatherResultTargetRpc` (см. Фаза 5)

### Шаг 3.2: `ActiveGatherJob` struct

```csharp
internal struct ActiveGatherJob
{
    public ulong NodeNetId;
    public ulong ClientId;
    // Можно хранить last position для проверки
}
```

### Шаг 3.3: Проверка

```bash
# Open Unity → Console: 0 errors
# Добавить GatheringServer в BootstrapScene (рядом с InventoryServer)
# Play mode → console: "[GatheringServer] OnNetworkSpawn"
```

---

## Фаза 4: `GatheringClientState` + `GatheringToast` (~1.5-2 ч)

### Шаг 4.1: Создать `GatheringClientState.cs`

**Файл:** `Assets/_Project/Scripts/ResourceNode/GatheringClientState.cs`

Паттерн — копия `InventoryClientState.cs` / `QuestClientState.cs`.

```csharp
public class GatheringClientState : MonoBehaviour
{
    public static GatheringClientState Instance { get; private set; }
    
    // Events
    public event System.Action<float> OnGatherProgress;     // 0..1 прогресс-бар
    public event System.Action<string, int> OnGatherCompleted; // itemName, quantity
    public event System.Action<string> OnGatherInterrupted;    // reason
    public event System.Action<string> OnGatherDenied;         // reason (нет инструмента)
    
    // Request helpers
    public void RequestStartGather(ulong nodeNetId) { /* RPC */ }
    public void RequestCancelGather() { /* RPC */ }
    
    // Receivers (вызываются из NetworkPlayer.ReceiveGatherResultTargetRpc)
    public void OnGatherResultReceived(GatherResult result) { /* events */ }
}
```

### Шаг 4.2: Создать UI тоста с прогресс-баром

**Файлы:**
- `Assets/_Project/Scripts/ResourceNode/GatheringToastController.cs` — код контроллера
- `Assets/_Project/UI/ResourceNode/GatheringToast.uxml` — разметка
- `Assets/_Project/UI/ResourceNode/GatheringToast.uss` — стили

**Не просто текст (как QuestToast), а UIDocument с ProgressBar:**

UXML:
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
  <ui:VisualElement name="GatheringToastRoot"
    style="flex-grow: 1; align-items: center; justify-content: flex-end; margin-bottom: 200px;">
    <ui:VisualElement name="ToastContainer"
      style="width: 320px; background-color: rgba(0,0,0,0.7); border-radius: 8px; padding: 10px;">
      <ui:Label name="ToastLabel" text="Добыча: Руда" />
      <ui:ProgressBar name="GatherProgressBar" low-value="0" high-value="1" value="0" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

**GatheringToastController.cs:**
- OnEnable: создаёт UIDocument на корне (reuse существующий PanelSettings)
- Подписка на `GatheringClientState.OnGatherProgress`, `OnGatherCompleted`, `OnGatherInterrupted`, `OnGatherDenied`
- `OnGatherProgress(0..1)` → `_progressBar.value = progress`
- `OnGatherCompleted(itemName, quantity)` → `_progressBar.value = 1.0`, `_label.text = $"Добыто: {itemName} × {quantity}"`, через 0.5 сек скрыть
- `OnGatherInterrupted/Denied(reason)` → `_progressBar.value = 1.0` (flash-fill 0.2s), label = reason, через 1 сек скрыть
- Queue: один сбор = один toast, не мультиплекс (один игрок = один сбор).
- Таймаут: если нет `OnGatherProgress` > 2.5 сек — скрыть toast.

### Шаг 4.3: Auto-spawn в NetworkManagerController

**Файл:** `Assets/_Project/Scripts/Core/NetworkManagerController.cs`

```csharp
// В Awake, после CreateInventoryClientState() и CreateMetaRequirementClientState():
private void CreateGatheringClientState() { /* по аналогии */ }
```

### Шаг 4.4: Проверка

```bash
# Open Unity → Console: 0 errors
# StartHost → check GatheringClientState.Instance != null
```

---

## Фаза 5: F-key интеграция + InteractableManager (~0.5-1 ч)

### Шаг 5.1: `InteractableManager.cs`

Добавить:

```csharp
private static readonly List<ResourceNode> _resourceNodes = new List<ResourceNode>(16);

public static void RegisterResourceNode(ResourceNode node) { /* как RegisterPickup */ }
public static void UnregisterResourceNode(ResourceNode node) { /* как UnregisterPickup */ }
public static ResourceNode FindNearestResourceNode(Vector3 position, float range) { /* как FindNearestPickup */ }
```

### Шаг 5.2: `ResourceNode.cs` — регистрация через триггер

Добавить в `ResourceNode`:

```csharp
// По аналогии с PickupItem:
private void OnTriggerEnter(Collider other) {
    if (other.CompareTag("Player")) {
        InteractableManager.RegisterResourceNode(this);
    }
}
private void OnTriggerExit(Collider other) {
    if (other.CompareTag("Player")) {
        InteractableManager.UnregisterResourceNode(this);
    }
}
```

### Шаг 5.3: `NetworkPlayer.cs` — F-key

В `NetworkPlayer.Update()` в блоке F-key (`Keyboard.current.fKey.wasPressedThisFrame`), **перед** boarding-логикой:

```csharp
// Try gather resource (выше приоритет чем посадка — Q1)
if (TryGatherNearestNode()) return;

// Original boarding logic:
if (TryBoardNearestShip()) return;
```

Метод `TryGatherNearestNode()`:

```csharp
private bool TryGatherNearestNode()
{
    var nearest = InteractableManager.FindNearestResourceNode(
        transform.position, pickupRange); // pickupRange = 3f
    if (nearest == null) return false;
    
    // Race protection (как у MetaRequirement — reuse существующую защиту)
    if (Time.unscaledTime - _lastCanUseRequestTime < CAN_USE_REQUEST_TIMEOUT
        && _pendingCanUseInteractableId == nearest.NetworkObjectId)
        return true; // уже ждём ответ сервера
    
    _lastCanUseRequestTime = Time.unscaledTime;
    _pendingCanUseInteractableId = nearest.NetworkObjectId;
    
    // MetaRequirement проверит инструмент (All/Any/AtLeastN).
    // Если deny → MetaRequirementToast сам покажет отказ (бесплатно).
    // Если allow → OnAccessAllowed → ResourceNode сам стартует сбор (см. §1.3).
    MetaRequirementClientState.Instance?.RequestCanUse(nearest.NetworkObjectId);
    return true;
}
```

**Ключевое:** F → MetaRequirementClientState.RequestCanUse (существующий, без изменений).
Не добавляем новый RPC в NetworkPlayer — сбор стартует через ResourceNode.OnMetaAccessAllowed → GatheringClientState.RequestStartGather.

### Шаг 5.5: `NetworkPlayer.cs` — TargetRPC для результатов сбора (GatheringServer → клиент)

```csharp
[Rpc(SendTo.Owner)]
public void ReceiveGatherResultTargetRpc(GatherResult result, RpcParams rpcParams = default)
    => GatheringClientState.Instance?.OnGatherResultReceived(result);
```

### Шаг 5.6: Проверка

```bash
# Open Unity → Console: 0 errors
# Play mode: host → подойти к ResourceNode → F
#   - если есть инструмент (MetaRequirement All) → сбор начинается
#   - если нет инструмента → MetaRequirementToast "Нужен ..."
```

---

## Фаза 5.5: Клиентская анимация ResourceNode (~1-1.5 ч)

### Шаг 5.5.1: Параметры анимации в ResourceNodeConfig

Добавить в `ResourceNodeConfig.cs`:

```csharp
[Header("Gather Animation (client)")]
[SerializeField] [Range(0f, 0.5f)] private float _animScaleAmplitude = 0.15f;
[SerializeField] [Range(0.1f, 1.5f)] private float _animPulsePeriod = 0.4f;
[SerializeField] [Range(0.1f, 1.5f)] private float _animHiddenDuration = 0.3f;

[Header("Emissive")]
[SerializeField] private Color _animIdleEmission = new Color(0.05f, 0.05f, 0.1f);
[SerializeField] private Color _animGatherEmission = new Color(0.3f, 1.5f, 0.3f);
```

### Шаг 5.5.2: Логика анимации в ResourceNode.cs

Добавить (client-only, по паттерну LockBox):

- `_replicatedState.OnValueChanged` — подписка на смену состояния
  - `Idle` → стоп анимация
  - `Occupied` → запустить `GatherAnimationCoroutine`
  - `Depleted` → стоп анимация, запустить `DisappearCoroutine` (scale → 0 за `_animHiddenDuration`)
  - `Cooldown` → `SetActive(false)`
  - Idle (после Cooldown) → `SetActive(true)`, scale 1.0 + overshoot

- `GatherAnimationCoroutine()`:
  - LOOP: scale = 1.0 + `_animScaleAmplitude * sin(time * 2π / _animPulsePeriod)`
  - emissive = `Color.Lerp(_animIdleEmission, _animGatherEmission, (sin + 1) / 2)`
  - Apply через `MaterialPropertyBlock` (как LockBox)

- `DisappearCoroutine()`:
  - t = 0..1 за `_animHiddenDuration`
  - scale = `Mathf.Lerp(1, 0, t)`
  - в конце `gameObject.SetActive(false)`

- **Player animation:** НЕ входит в MVP (Phase 2: StateHasher → PlayerGatherState) 

---

## Фаза 6: Scene placement + префабы (~0.5 ч)

### Шаг 6.1: Создать префаб `ResourceNode_Default.prefab`

- GameObject с: `NetworkObject`, `Collider` (isTrigger), `MeshRenderer` (сфера/куб), `ResourceNode` компонент, **`MetaRequirement` компонент**
- `ResourceNode._metaRequirement` → ссылка на свой `MetaRequirement` (SerializeReference в той же GameObject)
- `ResourceNode._config` = `IronVein.asset`
- `MetaRequirement._requiredItems[0]` = tool (кирка/ножницы)
- `Renderer` нужен для анимации (MaterialPropertyBlock scale + emissive)
- Положить в `Assets/_Project/Prefabs/ResourceNode_Default.prefab`

### Шаг 6.2: Добавить в `WorldScene_0_0.unity`

- Разместить 3-5 ResourceNode в кластере @ (40000, 2510, 40000) рядом с тестовыми сундуками
- Разные конфиги: IronVein, CopperVein, PlantHerb

### Шаг 6.3: Добавить `GatheringServer` в `BootstrapScene.unity`

- Новый GameObject `[GatheringServer]` с `NetworkObject` + `GatheringServer` (server-only)
- Добавить `GatheringToastController` (UIDocument + GatheringToastController.cs) на корень

### Шаг 6.4: Проверка

```bash
# Open Unity → Console: 0 errors
# Play mode: host → подойти к узлу → F → сбор
```

---

## Сводка файлов

### Новые файлы (10)

| # | Файл | Фаза | Примечание |
|---|------|------|-----------|
| 1 | `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs` | 1 | ScriptableObject с параметрами (+ anim params) |
| 2 | `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs` | 2 | NetworkBehaviour + MetaReq subscription + client animation |
| 3 | `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs` | 3 | RPC hub + tick |
| 4 | `Assets/_Project/Scripts/ResourceNode/GatheringClientState.cs` | 4 | Client projection |
| 5 | `Assets/_Project/Scripts/ResourceNode/GatheringToastController.cs` | 4 | UIDocument контроллер с ProgressBar |
| 6 | `Assets/_Project/UI/ResourceNode/GatheringToast.uxml` | 4 | Разметка: ToastLabel + ProgressBar |
| 7 | `Assets/_Project/UI/ResourceNode/GatheringToast.uss` | 4 | Стили тоста |
| 8 | `Assets/_Project/Resources/ResourceNodes/ResourceNode_IronVein.asset` | 1 | Пример конфига |
| 9 | `Assets/_Project/Prefabs/ResourceNode_Default.prefab` | 6 | **Содержит:** NetworkObject + ResourceNode + MetaRequirement + Collider trigger + Renderer |

### Изменяемые файлы (3-4)

| # | Файл | Фаза | Примечание |
|---|------|------|-----------|
| 1 | `Assets/_Project/Scripts/Core/InteractableManager.cs` | 5 | + Register/FindNearest ResourceNode |
| 2 | `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | 4 | + CreateGatheringClientState() |
| 3 | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 5 | + TryGatherNearestNode() (F → MetaRequirement) + TargetRPC |
| 4 | `Assets/_Project/Client/Player/State/StateHasher.cs` | * | **Phase 2** — Player gather animation |
| 5 | `Assets/_Project/Scenes/BootstrapScene.unity` | 6 | + [GatheringServer] GO + GatheringToastController |
| 6 | `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | 6 | + ResourceNode placement |

---

## Порядок коммитов (рекомендуемый)

```powershell
# Фаза 1
git add -A && git commit -m "feat(resource-node): ResourceNodeConfig SO with tool/gather/cooldown/anim params"

# Фаза 2
git add -A && git commit -m "feat(resource-node): ResourceNode NetworkBehaviour with gather state machine + client animation"

# Фаза 3
git add -A && git commit -m "feat(resource-node): GatheringServer RPC hub + server-side gather tick"

# Фаза 4
git add -A && git commit -m "feat(resource-node): GatheringClientState + GatheringToastController with ProgressBar UI"

# Фаза 5
git add -A && git commit -m "feat(resource-node): InteractableManager + F-key integration in NetworkPlayer"

# Фаза 5.5
git add -A && git commit -m "feat(resource-node): ResourceNode client animation (scale-pulse + emissive flash)"

# Фаза 6
git add -A && git commit -m "feat(scene): ResourceNode prefab + placement in WorldScene_0_0"
```

---

## Post-MVP (Phase 2+) backlog

- [ ] Tool durability (consume tool on gather)
- [ ] Multi-player on same node (queue / split)
- [ ] Node tiering (Tier 1/2/3 с разными инструментами)
- [ ] Player gather animation (StateHasher → PlayerGatherState)
- [ ] Gather skill (уровень влияет на скорость)
- [ ] Random yield (1-3 предмета за сбор)
- [ ] Persistence (сохранение состояния узла)
- [ ] ResourceNode как world-pickup (spawnable)
