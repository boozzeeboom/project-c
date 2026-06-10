# Resource Gathering — Детальный дизайн

> **Файл:** `docs/Mining/10_DESIGN.md`
> **Дата:** 2026-06-10
> **Статус:** Анализ (код не написан)

---

## 1. Ключевые классы

### 1.1 `ResourceNodeConfig` — ScriptableObject (конфигурация узла)

```csharp
// Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs
// Namespace: ProjectC.ResourceNode

[CreateAssetMenu(fileName = "ResourceNode_", menuName = "Project C/Resource Node Config")]
public class ResourceNodeConfig : ScriptableObject
{
    [Header("Result Item")]
    [Tooltip("Что выпадает после сбора. ItemData из InventoryWorld.")]
    [SerializeField] private ItemData _resultItem;          // ссылка на ассет (resolved → int itemId)
    
    [Header("Tool Requirement")]
    [Tooltip("Какой инструмент нужен в инвентаре (ItemType.Tool). null = не требуется.")]
    [SerializeField] private ItemData _requiredTool;        // ссылка на ассет (resolved → int itemId)
    
    [Header("Gathering")]
    [Tooltip("Время сбора в секундах (server-authoritative).")]
    [SerializeField] private float _gatherSeconds = 3f;     // 3s default
    
    [Tooltip("Максимальное кол-во сборов подряд до перезарядки.")]
    [SerializeField] private int _maxHarvests = 5;          // 5 раз подряд
    
    [Tooltip("Время перезарядки в секундах. Узел невидим/недоступен.")]
    [SerializeField] private float _cooldownSeconds = 60f;  // 1 минута
    
    [Tooltip("Макс. дистанция для сбора (default 3 м).")]
    [SerializeField] private float _gatherRange = 3f;
    
    [Header("Gather Animation")]
    [Tooltip("Название анимации сбора на клиенте (будущее, MVP: нет).")]
    [SerializeField] private string _animationTrigger = "";
    
    [Header("UI")]
    [Tooltip("Название узла для UI (\"Жила железа\", \"Куст травы\").")]
    [SerializeField] private string _nodeDisplayName = "Resource Node";
    
    [Tooltip("Формат тоста: \"Добыто: {0}\". {0} = itemName из ItemData.")]
    [SerializeField] private string _toastFormat = "Добыто: {0} × {1}";
    
    // --- Runtime resolved ---
    public int ResultItemId { get; private set; } = -1;
    public int RequiredToolId { get; private set; } = -1;
    public bool HasToolRequirement => RequiredToolId > 0;
    
    /// <summary>Вызывается сервером после регистрации ItemData (lazy resolve).</summary>
    public void ResolveItemIds(InventoryWorld inventoryWorld)
    {
        if (_resultItem != null)
            ResultItemId = inventoryWorld.GetOrRegisterItemId(_resultItem);
        if (_requiredTool != null)
            RequiredToolId = inventoryWorld.GetOrRegisterItemId(_requiredTool);
    }
}
```

### 1.2 `ResourceNode` — NetworkBehaviour (состояние узла)

```csharp
// Assets/_Project/Scripts/ResourceNode/ResourceNode.cs
// Namespace: ProjectC.ResourceNode
// ИСПОЛЬЗУЕТ MetaRequirement для tool check (MetaRequirement компонент на том же GameObject).
// См. docs/Mining/00_OVERVIEW.md §Q1-Q3.

public enum ResourceNodeState : byte
{
    Idle,                   // готов к сбору
    Occupied,               // кто-то собирает (soft-lock)
    Depleted,               // _currentHarvests == 0, ждёт cooldown
    Cooldown,               // невидим/недоступен, таймер
}

public class ResourceNode : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] private ResourceNodeConfig _config;
    
    [Header("MetaRequirement (tool check)")]
    [Tooltip("MetaRequirement на этом же GameObject, задающий инструменты сборщика. " +
             "Пустой _requiredItems + RequirementLogic.All = нет требований.")]
    [SerializeField] private MetaRequirement _metaRequirement;
    
    // === Server-only state ===
    private ResourceNodeState _currentState = ResourceNodeState.Idle;
    private int _currentHarvests;              // сколько осталось до cooldown
    private ulong _currentGathererClientId;    // кто сейчас собирает
    private float _gatherStartServerTime;      // server-time начала сбора
    private float _cooldownEndServerTime;      // server-time окончания cooldown
    // НЕТ _gatherStartPosition — игрок может бегать во время сбора (Q2).
    
    // === Replicated to clients ===
    private NetworkVariable<ResourceNodeState> _replicatedState = new(
        ResourceNodeState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    
    public ResourceNodeConfig Config => _config;
    public ResourceNodeState CurrentState => _replicatedState.Value;
    
    // ==========================================
    // Server-side API (вызывается из GatheringServer)
    // ==========================================
    
    /// <summary>Проверка: может ли игрок начать сбор?</summary>
    [Server]
    public bool CanStartGather(ulong clientId, out string failReason)
    {
        failReason = "";
        
        if (_currentState != ResourceNodeState.Idle)
        {
            failReason = _config._nodeDisplayName + " сейчас недоступен";
            return false;
        }
        // MetaRequirement проверяет инструмент (на сервере).
        // Если _metaRequirement == null или требования пустые — тривиально true.
        if (_metaRequirement != null && !_metaRequirement.CanPlayerUse(clientId, out failReason))
        {
            return false;
        }
        return true;
    }
    
    /// <summary>Сервер: начать сбор. Возвращает false если не удалось.</summary>
    [Server]
    public bool TryStartGather(ulong clientId, float serverTime)
    {
        string reason;
        if (!CanStartGather(clientId, out reason))
            return false;
        
        _currentState = ResourceNodeState.Occupied;
        _currentGathererClientId = clientId;
        _gatherStartServerTime = serverTime;
        _replicatedState.Value = ResourceNodeState.Occupied;
        
        return true;
    }
    
    /// <summary>Сервер: тик сбора (вызывается из GatheringServer.Update).</summary>
    [Server]
    public GatherTickResult TickGather(float serverTime)
    {
        // НЕТ проверки расстояния — игрок может двигаться (Q2).
        
        // Проверка: таймер истёк?
        if (serverTime - _gatherStartServerTime >= _config._gatherSeconds)
        {
            return CompleteGather();
        }
        
        return GatherTickResult.InProgress(
            1f - (serverTime - _gatherStartServerTime) / _config._gatherSeconds);
    }
    
    [Server]
    private GatherTickResult CompleteGather()
    {
        // Добавить предмет в инвентарь
        bool added = InventoryWorld.Instance.AddItemDirect(
            _currentGathererClientId,
            _config.ResultItemId,
            _config._resultItem.type);  // ItemType из SO
        
        if (!added)
        {
            CancelGather("Инвентарь полон");
            return GatherTickResult.Interrupted("Инвентарь полон");
        }
        
        // Декремент
        _currentHarvests++;
        
        // Узел истощён?
        if (_currentHarvests >= _config._maxHarvests)
        {
            _currentState = ResourceNodeState.Depleted;
            _replicatedState.Value = ResourceNodeState.Depleted;
            _cooldownEndServerTime = (_gatherStartServerTime + _config._gatherSeconds) + _config._cooldownSeconds;
            return GatherTickResult.Completed(_config._resultItem.itemName, 1, true);
        }
        
        // Узел ещё активен — возвращаем в Idle
        _currentState = ResourceNodeState.Idle;
        _currentGathererClientId = 0;
        _replicatedState.Value = ResourceNodeState.Idle;
        
        return GatherTickResult.Completed(_config._resultItem.itemName, _currentHarvests, false);
    }
    
    [Server]
    public void CancelGather(string reason)
    {
        if (_currentState != ResourceNodeState.Occupied) return;
        _currentState = ResourceNodeState.Idle;
        _currentGathererClientId = 0;
        _replicatedState.Value = ResourceNodeState.Idle;
    }
}
```

### 1.3 `ResourceNode` — клиентская подписка на MetaRequirement (паттерн LockBox)

```csharp
// В ResourceNode.cs, дополнительно к server-логике:

private bool _subscribedToMeta = false;

private void OnEnable()
{
    // Подписка на OnAccessAllowed (паттерн LockBox из docs/MetaRequirement)
}

private void OnDisable()
{
    UnsubscribeFromMeta();
}

private void Update()
{
    // Lazy-subscribe если MetaRequirementClientState появился позже
    if (!_subscribedToMeta && MetaRequirementClientState.Instance != null)
    {
        MetaRequirementClientState.Instance.OnAccessAllowed += OnMetaAccessAllowed;
        _subscribedToMeta = true;
    }
}

private void OnMetaAccessAllowed(ulong netId)
{
    if (netId != NetworkObjectId) return;
    
    // MetaRequirement tool check прошёл → стартуем сбор на сервере
    // (через GatheringClientState, через 2-й RPC)
    GatheringClientState.Instance?.RequestStartGather(netId);
}
```

### GatherTickResult — DTO для тика

```csharp
public struct GatherTickResult
{
    public enum Type { InProgress, Completed, Interrupted }
    public Type ResultType;
    public float Progress;        // 0..1 для клиента (прогресс-бар)
    public string Message;        // для тоста
    public string ItemName;       // что собрано
    public int CurrentHarvest;    // какой по счёту сбор
    public bool IsDepleted;       // узел истощён?

    public static GatherTickResult InProgress(float progress)
        => new GatherTickResult { ResultType = Type.InProgress, Progress = progress };

    public static GatherTickResult Completed(string itemName, int harvest, bool depleted)
        => new GatherTickResult { ResultType = Type.Completed, ItemName = itemName, CurrentHarvest = harvest, IsDepleted = depleted };

    public static GatherTickResult Interrupted(string message)
        => new GatherTickResult { ResultType = Type.Interrupted, Message = message };
}
```

### 1.3 `GatheringServer` — NetworkBehaviour (RPC hub)

```csharp
// Assets/_Project/Scripts/ResourceNode/GatheringServer.cs
// Namespace: ProjectC.ResourceNode
// Живёт в BootstrapScene, DontDestroyOnLoad (как InventoryServer, MarketServer).

public class GatheringServer : NetworkBehaviour
{
    public static GatheringServer Instance { get; private set; }
    
    // Словарь: clientId → активный сбор
    private Dictionary<ulong, ActiveGatherJob> _activeGathers = new Dictionary<ulong, ActiveGatherJob>(8);
    
    // Registry: netId → ResourceNode (как MetaRequirementRegistry)
    private Dictionary<ulong, ResourceNode> _nodes = new Dictionary<ulong, ResourceNode>(32);
    
    private const float TICK_INTERVAL = 0.5f;  // проверка каждые 500ms
    private float _nextTickTime;
    
    public void RegisterNode(ulong netId, ResourceNode node) { _nodes[netId] = node; }
    public void UnregisterNode(ulong netId) { _nodes.Remove(netId); }
    
    // === RPC: клиент → сервер ===
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestStartGatherRpc(ulong nodeNetId, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Rate-limit
        if (!CheckRateLimit(clientId)) return;
        
        // Игрок уже собирает?
        if (_activeGathers.ContainsKey(clientId))
        {
            SendGatherResult(clientId, GatherResult.ServerError("Вы уже собираете другой ресурс"));
            return;
        }
        
        // Узел существует?
        if (!_nodes.TryGetValue(nodeNetId, out var node))
        {
            SendGatherResult(clientId, GatherResult.ServerError("Ресурс не найден"));
            return;
        }
        
        // Расстояние
        // (находим NetworkPlayer по clientId, проверяем дистанцию)
        if (!CheckDistance(clientId, node.transform.position, node.Config._gatherRange))
        {
            SendGatherResult(clientId, GatherResult.ServerError("Слишком далеко от ресурса"));
            return;
        }
        
        // Сервер: начать сбор (без передачи позиции — Q2: движение не прерывает)
        float serverTime = Time.realtimeSinceStartup;  // или ServerTimeController

        if (!node.TryStartGather(clientId, serverTime))
        {
            string reason;
            node.CanStartGather(clientId, out reason);
            SendGatherResult(clientId, GatherResult.Denied(reason));
            return;
        }
        
        _activeGathers[clientId] = new ActiveGatherJob(nodeNetId, clientId);
        SendGatherResult(clientId, GatherResult.InProgress(0f));
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestCancelGatherRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!_activeGathers.TryGetValue(clientId, out var job)) return;
        
        if (_nodes.TryGetValue(job.NodeNetId, out var node))
            node.CancelGather("Отменено игроком");
        
        _activeGathers.Remove(clientId);
        SendGatherResult(clientId, GatherResult.Cancelled());
    }
    
    // === Server-side tick ===
    
    [Server]
    private void Update()
    {
        if (!IsServer) return;
        if (Time.time < _nextTickTime) return;
        _nextTickTime = Time.time + TICK_INTERVAL;
        
        float serverTime = Time.realtimeSinceStartup;
        var toRemove = new List<ulong>();  // избегаем аллокаций pool-ом в релизе
        
        foreach (var kvp in _activeGathers)
        {
            ulong clientId = kvp.Key;
            var job = kvp.Value;
            
            if (!_nodes.TryGetValue(job.NodeNetId, out var node))
            {
                toRemove.Add(clientId);
                SendGatherResult(clientId, GatherResult.ServerError("Ресурс исчез"));
                continue;
            }
            
            var playerPos = GetPlayerPosition(clientId);
            var tickResult = node.TickGather(serverTime, playerPos);
            
            switch (tickResult.ResultType)
            {
                case GatherTickResult.Type.InProgress:
                    // Шлём прогресс клиенту (для UI прогресс-бара)
                    SendGatherResult(clientId, GatherResult.InProgress(tickResult.Progress));
                    break;
                    
                case GatherTickResult.Type.Completed:
                    toRemove.Add(clientId);
                    SendGatherResult(clientId, GatherResult.Completed(
                        tickResult.ItemName, 1, tickResult.IsDepleted));
                    break;
                    
                case GatherTickResult.Type.Interrupted:
                    toRemove.Add(clientId);
                    SendGatherResult(clientId, GatherResult.Interrupted(tickResult.Message));
                    break;
            }
        }
        
        foreach (var clientId in toRemove)
            _activeGathers.Remove(clientId);
        
        // Cooldown checks: узлы в Depleted → Cooldown → Idle
        foreach (var kvp in _nodes)
        {
            var node = kvp.Value;
            // ... проверка cooldown ...
        }
    }
}
```

### 1.4 `GatheringClientState` + `GatheringToast` (с прогресс-баром)

```csharp
// По образцу InventoryClientState / QuestClientState — одна проекция.
// GatheringToast — UI Toolkit UIDocument с ProgressBar (не просто текст).
//
// UXML-структура (Assets/_Project/UI/ResourceNode/GatheringToast.uxml):
//   <ui:VisualElement name="GatheringToastRoot">
//     <ui:Label name="ToastLabel" text="Добыча: Руда" />
//     <ui:ProgressBar name="GatherProgressBar" low-value="0" high-value="1" />
//   </ui:VisualElement>
//
// GatheringToastController.cs наследует (или содержит) UIDocument,
// подписывается на GatheringClientState.OnGatherResult:
//   - InProgress(progress) → GatherProgressBar.value = progress
//   - Completed → GatherProgressBar.value = 1, через 0.5 сек скрыть
//   - Interrupted/Denied → скрыть через 1 сек
//
// Queue: один активный сбор = один toast. Новый сбор заменяет текущий toast.
// (Упрощение: без очереди, т.к. один игрок = один сбор).
```

- **UI Toolkit ProgressBar** (`<ui:ProgressBar>`) — родной элемент, поддерживает title, low/high value.
- Позиция: центр экрана, нижняя треть (выше квестового тоста, если есть).
- Таймаут: если сервер не присылает InProgress > 2 сек — toast скрывается (fallback).
### 1.5 Визуальная обратная связь на клиенте

```csharp
// === ResourceNode (client-side) ===

// При _replicatedState == Occupied:
// - Coroutine _gatherAnimCoroutine запускает scale-pulse + emissive flash
//   (аналогично LockBox анимации из MetaRequirement.Test, но LOOP)
// - Анимация: scale колеблется 1.0x ↔ 1.15x, emission мерцает с периодом ~0.4 сек
// - Параметры анимации вынесены в ResourceNodeConfig (можно менять в инспекторе):
//   _animScaleAmplitude = 0.15f;  // амплитуда пульсации scale
//   _animPulsePeriod = 0.4f;      // период в секундах

// При _replicatedState == Idle/Depleted/Cooldown — анимация останавливается.
// При Depleted: корутина плавного исчезновения (scale → 0 за 0.3 сек), потом SetActive(false).
// При Cooldown → Cooldown → Idle: при появлении scale → 1 + небольшой overshoot.

// Player animation (MVP):
//   - Пока НЕ реализовано (дефолтный state машина персонажа — отдельная задача).
//   - В MVP: только ResourceNode анимация + тост с прогресс-баром.
//   - Phase 2: StateHasher → PlayerGatherState (анимация рук/инструмента).
```

**Параметры анимации в ResourceNodeConfig:**

```csharp
[Header("Gather Animation (client)")]
[SerializeField] [Range(0f, 0.5f)] private float _animScaleAmplitude = 0.15f;
[SerializeField] [Range(0.1f, 1.5f)] private float _animPulsePeriod = 0.4f;
[SerializeField] [Range(0.1f, 1.5f)] private float _animHiddenDuration = 0.3f; // появление/исчезание

// Emissive
[SerializeField] private Color _animIdleEmission = new Color(0.05f, 0.05f, 0.1f);
[SerializeField] private Color _animGatherEmission = new Color(0.3f, 1.5f, 0.3f);
```

---

## 2. Sequence-диаграмма (успешный сбор)

```
[Client]                          [Server]                              [ResourceNode]
  F pressed                         GatheringServer                       _currentState=Idle
    ↓                                 ↓                                      ↓
  InteractableManager
  .FindNearestResourceNode()
    ↓
  MetaRequirementClientState
  .RequestCanUse(nodeNetId)
    → RPC ───────────────────────→ MetaRequirementRegistry
                                    ↓
                                  MetaRequirement
                                  .CanPlayerUse(clientId)
                                    → CountOf(tool) ≥ 1  (All/Any/AtLeastN)
                                    ← true
                                    ↓
                                  TargetRPC: allow
  ←────────────────────────────── TargetRPC

  MetaRequirementClientState
  .OnAccessAllowed(nodeNetId)
    ↓
  ResourceNode.OnMetaAccessAllowed()
    → GatheringClientState.RequestStartGather(nodeNetId)
    → RPC ───────────────────────→ GatheringServer
                                    ↓
                                  ResourceNode.TryStartGather(serverTime)
                                    → _state = Occupied
                                    ↓
                                  [activeGathers[clientId] = job]
                                    ↓
                                  SendGatherResult(InProgress(0f))

  ←────────────────────────────── TargetRPC
    ↓
  GatheringClientState.OnGatherResult
    → GatheringToast.show()
    → ProgressBar.value = 0.0
    → ResourceNode.StartGatherAnimation()
      (scale pulse + emissive flash — LOOP)

                                  [Tick 0.5s]
                                  TickGather(serverTime)
                                    → check time left (≈ 62.5%)
                                  SendGatherResult(InProgress(0.375))
  ←────────────────────────────── TargetRPC
    ↓
  GatheringClientState.OnGatherResult
    → ProgressBar.value = 0.375

                                  [Tick 1.0s] ...
                                  [Tick 1.5s] ...

                                  CompleteGather()
                                    → AddItemDirect(clientId, resultItem)
                                    → _currentHarvests++
                                    → if < _maxHarvests: _state = Idle
                                    → if >= _maxHarvests: _state = Depleted
                                  SendGatherResult(Completed("Руда", 1))
  ←────────────────────────────── TargetRPC
    ↓
  GatheringClientState.OnGatherResult
    → ResourceNode.StopGatherAnimation()
    → ProgressBar.value = 1.0
    → GatheringToast("Добыто: Руда × 1", 1)
    → Инвентарь: OnSnapshotUpdated → P-таб обновлён
    → Через 0.5 сек: toast скрывается
```

---

## 3. Edge-cases

### 3.1 Double-F (race)
Игрок нажал F дважды.
- **Защита:** `GatheringServer` проверяет `_activeGathers.ContainsKey(clientId)` — второй RPC получит "Вы уже собираете".
- `NetworkPlayer._lastCanUseRequestTime` (уже есть) — дополнительная локальная защита.

### 3.2 Движение во время сбора
- **Нет проверки.** Сервер не отслеживает позицию игрока. Таймер идёт независимо от движения. Сбор завершится даже если игрок убежал на 100 метров.
- **Причина:** решение Q2 (`docs/Mining/00_OVERVIEW.md` §Q2) — "пусть бегает и рубит".
- **Защита от абуза:** один игрок = один активный сбор (`_activeGathers[clientId]`). Второй сбор начать нельзя пока не завершён первый.

### 3.3 Disconnect во время сбора
- `GatheringServer.OnClientDisconnectCallback` → `CancelGather()` для всех job этого clientId.
- Узел возвращается в Idle. Ресурсы НЕ добавлены.

### 3.4 Несколько игроков на одном узле
- **MVP:** только один за раз (`_currentState == Occupied` → второй получит "Ресурс сейчас занят").
- **Phase 2:** очередь / разделение ресурса.

### 3.5 Cooldown и визуал
- `_replicatedState` (NetworkVariable) синхронизируется всем клиентам.
- На клиенте при `Depleted` / `Cooldown` — 3D-объект скрывается (SetActive(false) или dissolve шейдер).
- MVP: просто `SetActive(false)`. Без анимации исчезания.

### 3.6 InventoryWorld.Instance == null (race при StartHost)
- `CanStartGather` проверяет `InventoryWorld.Instance != null`. Если null — deny.

### 3.7 Переполнение инвентаря
- `AddItemDirect` возвращает false → тост "Инвентарь полон". Узел НЕ декрементит, остаётся в Idle.

### 3.8 Узел в стриминговой сцене
- `ResourceNode` живёт в `WorldScene_X_Z`, `destroyWithScene=true`.
- `OnNetworkDespawn` → `GatheringServer.UnregisterNode()`.
- Если игрок собирал, а сцена выгрузилась → `CancelGather` (как disconnect).

### 3.9 Null / missing ResourceNodeConfig
- `OnValidate` в `ResourceNode`: `Debug.LogWarning` если `_config == null`.
- В `CanStartGather`: если `_config == null` → deny.

### 3.11 Toast-прогресс: отмена / сбой в середине
- Если сервер присылает `Interrupted` или `Denied` в середине сбора:
  - `ProgressBar` заполняется до 1 за 0.2 сек (flash-завершение), toast показывает "Сбор прерван"
  - Через 1 сек — скрыть
- Если сервер не присылает InProgress > 2.5 сек (таймаут):
  - `GatheringClientState` считается, что сбор прерван — toast скрывается, ResourceNode останавливает анимацию
- Если игрок нажал F на другой узел во время сбора:
  - Текущий сбор не отменяется (один игрок = один сбор, сервер отклонит второй)
  - toast продолжает показывать первый сбор
- `Config.ResolveItemIds()` → если `_requiredTool` не зарегистрирован в `InventoryWorld`, `RequiredToolId = -1`.
- `CanStartGather` проверяет `RequiredToolId > 0` — если -1, считаем что требование не задано (allow).

---

## 4. Сетевой трафик (estimate)

| Событие | Server → Client | Client → Server | Частота |
|---------|----------------|-----------------|---------|
| `RequestStartGatherRpc` | — | 1 RPC (~30B) | per F |
| `RequestCancelGatherRpc` | — | 1 RPC (~10B) | per F / movement |
| `GatherResult` (InProgress) | 1 RPC (~20B) | — | каждые 0.5 сек при сборе |
| `GatherResult` (Completed) | 1 RPC (~40B) | — | 1 раз |
| `GatherResult` (Interrupted) | 1 RPC (~40B) | — | 1 раз |
| `NetworkVariable<ResourceNodeState>` | ~1B delta | — | при смене состояния |

**Baseline:** ~150B на один успешный сбор (3-5 сек). При 10 сборах/мин = 1.5KB/min. Не критично.

---

## 5. Что НЕ входит в MVP (Phase 2+)

- **Proximity-based auto-gather** (сбор без нажатия F — для растений)
- **Multi-player на одном узле** (конкуренция или очередь)
- **Tool durability** (инструменты не ломаются)
- **Gather bot / automation protection** (капча или сложность)
- **ResourceNode как world-pickup** (spawnable через спавнер)
- **Tiered nodes** (уровни руды, разные для разных инструментов)
- **Gather mini-game** (QTE для ускорения)
- **Gather skill / уровень сбора** (влияет на скорость)
- **Persistence** (сохранение состояния узла между рестартами)
