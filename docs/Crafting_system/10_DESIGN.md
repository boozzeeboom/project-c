# Crafting System — Design (Классы, потоки, edge-cases)

> **Цикл:** Проектирование. **Без кода.** Только сигнатуры, псевдокод, sequence-диаграммы.
> **Зависимости от `00_OVERVIEW.md`:** Этот документ раскрывает §5, §6 и §9 из Overview.

---

## 1. Карта классов (v2-стиль, как Trade)

```
┌─────────────────────────────────────────────────────────────────────────┐
│  SERVER (host)                                                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  [CraftingServer] GameObject (in BootstrapScene)                          │
│   └── CraftingServer : NetworkBehaviour  (singleton, server-only enabled) │
│       • NetworkList<CraftingStationDto> _stations (server-write, all-read)│
│       • Dictionary<ulong, CraftingStation> _liveStations (by netId)      │
│       • OnNetworkSpawn → CraftingWorld.CreateAndInitialize()              │
│       • OnNetworkSpawn → subscribe onMarketTick (MarketTimeService)      │
│       • RPCs: Subscribe/Unsubscribe/AddIngredient/Start/Cancel/Collect   │
│       • Tick handler: CraftingWorld.OnTick(dt) → emit CraftingSnapshot   │
│                                                                            │
│  [CraftingStation] GameObject (per station, in WorldScene_X_Z)            │
│   └── CraftingStation : NetworkBehaviour, IInteractable                  │
│       • StationType type  (Shipyard | CraftingTable | ...)                 │
│       • CraftingStationConfig config (allowedRecipes[], craftSpeedMult)   │
│       • NetworkVariable<CraftingJobState> _state (server-write)            │
│       • NetworkList<BufferedIngredientDto> _buffer (server-write)          │
│       • OnNetworkSpawn → CraftingServer.RegisterStation(this)             │
│       • TryInteract() → client UI hook                                    │
│       • contains: GameObject visual + collider + NetworkObject            │
│                                                                            │
│  [CraftingWorld]  (POCO, server-only, in-memory)                          │
│   └── CraftingWorld : static Instance                                     │
│       • Dictionary<ulong, CraftingStation> stations (ссылка на live)      │
│       • Dictionary<ulong, CraftingJob> activeJobs   (by stationNetId)     │
│       • List<CompletedCraftingJob> completedJobs (для забора)              │
│       • TryAddIngredient / TryStart / TryCancel / TryCollect                │
│       • Tick(dt) → advance jobs, emit completions                          │
│       • Persistence: НЕТ в MVP. Через PlayerPrefsRepository в Phase 2.   │
│                                                                            │
├─────────────────────────────────────────────────────────────────────────┤
│  CLIENT (per-player)                                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  [CraftingClientState] GameObject (auto-spawn в NetworkManagerController) │
│   └── CraftingClientState : MonoBehaviour, DontDestroyOnLoad              │
│       • CraftingSnapshotDto? CurrentSnapshot                              │
│       • CraftingResultDto? LastResult                                     │
│       • Dictionary<ulong, CraftingStationDto> knownStations (per netId)   │
│       • event OnSnapshotUpdated                                           │
│       • event OnResultReceived                                            │
│       • RequestSubscribe(stationNetId) / Unsubscribe                      │
│       • RequestAddIngredient(recipeId, itemId, qty, source)               │
│       • RequestStartCraft(stationNetId)                                    │
│       • RequestCancelCraft(stationNetId)                                   │
│       • RequestCollect(stationNetId)                                       │
│       • LocalizeResultCode(CraftingResultCode) → string                    │
│                                                                            │
│  [CraftingWindow] в MarketWindow (tab)                                    │
│   └── Доп. в MarketWindow.cs (см. project-c-ui-as-tab skill)              │
│       • При activeTab == "crafting" показывает секцию                      │
│       • Подписка в EnsureBuilt / OnDisable                                │
│       • Drag-and-drop: inventory/warehouse → buffer                        │
│       • Optimistic update pattern                                          │
│                                                                            │
├─────────────────────────────────────────────────────────────────────────┤
│  DATA TYPES                                                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ScriptableObjects (in Resources/Crafting/):                                │
│   • CraftingStationConfig                                                 │
│   • RecipeData                                                            │
│   • RecipeOutput (struct, в RecipeData)                                   │
│   • RecipeIngredient (struct, в RecipeData)                               │
│                                                                            │
│  DTOs (network-serializable, в Dto/):                                      │
│   • CraftingStationDto (struct)                                           │
│   • CraftingJobDto (struct)                                               │
│   • CraftingSnapshotDto (struct)                                          │
│   • CraftingResultDto (struct)                                             │
│   • CraftingResultCode (enum)                                             │
│   • CraftingSourceType (enum: Inventory | Warehouse)                       │
│   • CraftingJobState (enum: Empty | Buffered | InProgress | Completed)    │
│   • BufferedIngredientDto (struct)                                        │
│   • RecipeDto (struct, сериализуемая версия RecipeData)                   │
│                                                                            │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. ScriptableObjects — поля

### 2.1 `CraftingStationConfig` (SO)

```csharp
[CreateAssetMenu(menuName = "Project C/Crafting/Station Config", fileName = "CraftingStationConfig")]
public class CraftingStationConfig : ScriptableObject
{
    [Header("Identity")]
    public string displayName;                       // "Верфь 'Новая Надежда'"
    public Sprite icon;                              // иконка для UI / tooltip
    [TextArea] public string description;
    public StationType stationType;                  // Shipyard | CraftingTable | ...

    [Header("Recipes")]
    [Tooltip("Какие рецепты можно крафтить. Порядок — UI order. Дубликаты игнорируются.")]
    public List<RecipeData> allowedRecipes = new List<RecipeData>();

    [Header("Limits")]
    [Tooltip("Макс одновременных заказов. MVP: жёстко 1.")]
    [Range(1, 1)] public int maxConcurrentJobs = 1;

    [Header("Timing")]
    [Tooltip("Множитель скорости крафта для этой станции. 1.0 = базовая скорость по рецепту.")]
    [Range(0.1f, 10f)] public float craftSpeedMultiplier = 1.0f;

    [Header("UI")]
    public Color tintColor = Color.white;            // подкрашивает UI элементы станции
}

public enum StationType { Shipyard, CraftingTable, Forge, Loom, Alchemy /* Phase 2+ */ }
```

**Использование:** `CraftingStation._config` (на самой станции в инспекторе). `CraftingServer` **не** хранит SOs — он хранит `CraftingStationDto` (сериализуемую проекцию, см. §4.1) с `recipeIds: int[]` (runtime-индексы в реестре `CraftingWorld`).

### 2.2 `RecipeData` (SO)

```csharp
[CreateAssetMenu(menuName = "Project C/Crafting/Recipe", fileName = "NewRecipe")]
public class RecipeData : ScriptableObject
{
    [Header("Identity")]
    public string displayName;                       // "Стальной модуль крыла"
    public Sprite icon;
    [TextArea] public string description;
    public RecipeCategory category;                  // Module | Consumable | Ship | Material

    [Header("Inputs (ingredients)")]
    [Tooltip("Что нужно положить в буфер. Сумма Qty = сколько надо каждого. Дубликаты по itemId ЗАПРЕЩЕНЫ.")]
    public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

    [Header("Outputs")]
    [Tooltip("Что выдаётся. MVP: 1 элемент, но список — для будущих многовыходных рецептов.")]
    public List<RecipeOutput> outputs = new List<RecipeOutput>();

    [Header("Timing")]
    [Tooltip("Сколько секунд серверного времени нужно для крафта. Умножается на station.craftSpeedMultiplier.")]
    [Min(1f)] public float craftSeconds = 600f;     // default 10 минут

    [Header("Skill gate (Phase 2, MVP: 0 = нет требования)")]
    [Min(0)] public int requiredSkillLevel = 0;
    [Tooltip("Какой навык (Phase 2). MVP: 0 = Engineering.")]
    public SkillType requiredSkill = SkillType.None;
}

[Serializable]
public struct RecipeIngredient
{
    public ItemData item;                            // какой предмет
    [Min(1)] public int quantity;                    // сколько штук
}

[Serializable]
public struct RecipeOutput
{
    public ItemData item;                            // обычный предмет выдачи
    [Min(1)] public int quantity = 1;
    [Tooltip("Если задан — выдаётся корабль-ключ вместо обычного item. item должен быть null.")]
    public ShipKeyBinding shipKeyBinding;            // для рецепта-корабля
}

public enum RecipeCategory { Module, Consumable, Ship, Material, Misc }
public enum SkillType { None, Engineering, Piloting, Trading, Combat /* Phase 2 */ }
```

**Валидация на OnValidate:**
- `outputs.Count >= 1`
- Каждый output: либо `item != null` (XOR `shipKeyBinding != null`).
- `ingredients.Count >= 1`, нет дубликатов по `item.itemName`.

### 2.3 `LootTable` vs `RecipeData` — почему разные

| | LootTable | RecipeData |
|---|-----------|-----------|
| Рандом | Да (chance, minCount/maxCount) | Нет (детерминированно) |
| Назначение | «Что выпадает из сундука» | «Что получается из рецепта» |
| Use-case | Chest (world-spawn) | Crafting (player-driven) |
| Time | Нет | Да (`craftSeconds`) |
| Output | Один ItemData | Список (item OR shipKeyBinding) |
| Persistence | Нет | Нет (in MVP) |

---

## 3. Server-side классы (детально)

### 3.1 `CraftingWorld` (POCO, сервер)

```csharp
public class CraftingWorld
{
    public static CraftingWorld Instance { get; private set; }

    public static CraftingWorld CreateAndInitialize() { /* ... */ }
    public void Shutdown() { /* ... */ }

    // === Recipe registry ===
    // RecipeData → recipeId (int). Server-only.
    public int RegisterRecipe(RecipeData recipe);    // → int
    public RecipeData GetRecipe(int recipeId);

    // === Stations ===
    public void RegisterStation(ulong stationNetId, CraftingStation station);
    public void UnregisterStation(ulong stationNetId);
    public CraftingStation GetStation(ulong stationNetId);

    // === Job lifecycle (server-only) ===
    public CraftingResult TryAddIngredient(ulong stationNetId, ulong clientId,
                                          int itemId, int quantity, CraftingSourceType source);
    public CraftingResult TryStartCraft(ulong stationNetId, ulong clientId);
    public CraftingResult TryCancelCraft(ulong stationNetId, ulong clientId);
    public CraftingResult TryCollect(ulong stationNetId, ulong clientId);

    // === Tick ===
    public void OnTick(float serverDeltaSeconds);    // called from CraftingServer.OnMarketTick
}
```

**Зачем world-отдельный:** переиспользование логики (станции могут мигрировать между сценами, умирать — но world-у живёт `RecipeData → id` маппинг). Контраст: в `TradeWorld` markets живут долго, у нас — станции могут быть временные.

**Тесты:** `CraftingWorld` — POCO, можно EditMode-тестировать без сети. Регистрация рецепта → id. Tick → job completion. Mock-`InventoryWorld` через интерфейс.

### 3.2 `CraftingServer` (NetworkBehaviour, серверный singleton)

```csharp
[RequireComponent(typeof(NetworkObject))]
[DisallowMultipleComponent]
public class CraftingServer : NetworkBehaviour
{
    public static CraftingServer Instance { get; private set; }

    [Header("Setup")]
    [Tooltip("Базовые рецепты, регистрируемые при старте. Должны быть в Resources/Crafting/.")]
    [SerializeField] private List<RecipeData> baseRecipes = new List<RecipeData>();

    [Header("Rate Limiting")]
    [SerializeField] private int maxOpsPerMinute = 30;

    // === RPCs (Client → Server) ===
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SubscribeStationRpc(ulong stationNetworkObjectId, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void AddIngredientRpc(ulong stationNetId, int itemId, int quantity,
                                  CraftingSourceType source, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void StartCraftRpc(ulong stationNetId, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelCraftRpc(ulong stationNetId, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CollectRpc(ulong stationNetId, RpcParams rpcParams = default);

    // === Server lifecycle ===
    public override void OnNetworkSpawn();      // init CraftingWorld, subscribe MarketTimeService
    public override void OnNetworkDespawn();    // shutdown, unsubscribe

    // === Internal ===
    private void OnMarketTick();                // → CraftingWorld.OnTick
    private void SendSnapshotTo(ulong clientId, CraftingSnapshotDto snap);
    private void SendResultTo(ulong clientId, CraftingResultDto result);
    private bool CheckRateLimit(ulong clientId);
}
```

**Lifecycle идентичен `MarketServer`** (см. `MarketServer.cs:71-118`). Дословно копируем: instance guard, repository-free (мы не персистим в MVP), `MarketTimeService.OnServerStarted` (только если мы главный таймер; иначе слушаем `onMarketTick`), cleanup в `OnNetworkDespawn`.

### 3.3 `CraftingStation` (NetworkBehaviour, per-station)

```csharp
[RequireComponent(typeof(NetworkObject))]
[DisallowMultipleComponent]
public class CraftingStation : NetworkBehaviour, IInteractable
{
    [Header("Config")]
    [SerializeField] private CraftingStationConfig _config;

    [Header("Settings")]
    [SerializeField] private float _interactRadius = 4f;     // как у NetworkChestContainer
    [SerializeField] private string _displayNameOverride;    // перебивает config.displayName

    // IInteractable
    public string DisplayName => string.IsNullOrEmpty(_displayNameOverride)
        ? (_config != null ? _config.displayName : "Station")
        : _displayNameOverride;
    public float InteractionRadius => _interactRadius;
    public Vector3 Position => transform.position;

    // NetworkList — только сервер пишет, все читают
    private NetworkList<BufferedIngredientDto> _bufferItems;
    private NetworkVariable<CraftingJobState> _jobState;
    private NetworkVariable<ulong> _jobOwnerClientId;
    private NetworkVariable<float> _jobStartServerTime;
    private NetworkVariable<float> _jobCraftSeconds;
    private NetworkVariable<int> _jobRecipeId;       // = -1 если пусто

    public override void OnNetworkSpawn();         // → CraftingServer.RegisterStation(this)
    public override void OnNetworkDespawn();       // → CraftingServer.UnregisterStation(this)
    public void TryInteract();                     // → CraftingClientState.RequestSubscribe
}
```

**Важно:** NetworkList + NetworkVariable — это **минимум 6 переменных** на станцию. С учётом 50 станций в зоне = 300 NetworkVariable. Это на пределе. Оптимизация: **ОДНА** `NetworkVariable<CraftingJobDto>` (custom INetworkSerializable struct) — кладёт ВСЁ состояние job в одну переменную. Snapshot = struct diff. Это аналог `MarketSnapshotDto` (struct целиком в одном RPC).

**В MVP — оставляем развёрнутые NetworkVariable** (для читаемости, как в NetworkChestContainer). В Phase 2 — refactor на `NetworkVariable<CraftingJobDto>`. Зафиксирую как TODO.

### 3.4 `CraftingJob` (POCO, сервер, in-memory)

```csharp
public class CraftingJob
{
    public ulong stationNetId;
    public ulong ownerClientId;          // кто заказал
    public int recipeId;
    public CraftingJobState state;       // Buffered | InProgress | Completed
    public float startServerTime;        // когда нажат StartCraft (=0 пока не стартовал)
    public float craftSeconds;           // с учётом station.craftSpeedMultiplier
    public List<BufferedIngredientDto> buffer;     // до StartCraft
    public List<BufferedIngredientDto> committed;  // после StartCraft
    public List<RecipeOutput> outputs;             // копия из RecipeData на момент Start
}
```

Состояния (state machine):

```
       AddIngredient (xN)         StartCraft              Tick(dt)              Collect
[No Job] ─────────────────► [Buffered] ──────────► [InProgress] ─────────► [Completed] ──► [No Job]
                              │                                                                 ▲
                              └────────── CancelCraft (только до InProgress) ───────────────────┘
                                                  (возврат ресурсов)
```

`Completed` → `Collect` → удаляем. **Без `Collect`** Job остаётся `Completed` (см. Q5 в `00_OVERVIEW.md` §9).

---

## 4. DTOs (network-serializable)

### 4.1 `CraftingStationDto`

```csharp
public struct CraftingStationDto : INetworkSerializable
{
    public ulong stationNetworkObjectId;     // ключ в реестре
    public FixedString64Bytes displayName;
    public byte stationType;                 // (byte)StationType
    public int[] allowedRecipeIds;           // что можно крафтить
    public float interactionRadius;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref stationNetworkObjectId);
        s.SerializeValue(ref displayName);
        s.SerializeValue(ref stationType);
        // ...int[] и float по pattern из MetaRequirement/Market DTOs
    }
}
```

### 4.2 `CraftingSnapshotDto`

```csharp
public struct CraftingSnapshotDto : INetworkSerializable
{
    public ulong stationNetworkObjectId;     // на какую станцию snapshot
    public int recipeIdInProgress;           // = -1 если пусто
    public byte jobState;                    // (byte)CraftingJobState
    public ulong jobOwnerClientId;           // ulong.MaxValue если пусто
    public float jobStartServerTime;
    public float jobCraftSeconds;
    public BufferedIngredientDto[] buffer;   // до StartCraft
    public BufferedIngredientDto[] committed; // после StartCraft
    public RecipeOutputDto[] pendingOutputs; // когда Completed — что забрать
}
```

### 4.3 `CraftingResultDto`

```csharp
public struct CraftingResultDto : INetworkSerializable
{
    public byte code;                        // (byte)CraftingResultCode
    public FixedString128Bytes message;      // human-readable
    public ulong stationNetworkObjectId;     // на какую станцию
    public int itemId;                       // для UI toast: что выдано/не хватило
    public int quantity;
    public byte source;                      // откуда (Inventory/Warehouse) — для UI
}

public enum CraftingResultCode : byte
{
    Ok = 0,
    InternalError,
    NotInZone,                  // игрок не рядом со станцией
    StationBusy,                // Job InProgress
    StationEmpty,               // буфер пуст / Cancel без Job
    NotOwner,                   // не заказчик (для Start/Cancel/Collect)
    NoRecipe,                   // recipeId не в allowed
    InsufficientBuffer,         // буфер не покрывает recipe
    InventoryFull,              // при Collect: не влезает
    WarehouseFull,              // при Collect: не влезает
    NoShipInStation,            // для рецепта-корабля: станция не имеет нужного ShipKeyBinding
    RateLimited,
    ServerTimeUnavailable,      // см. Q3 в Overview §9
}
```

### 4.4 `BufferedIngredientDto`

```csharp
public struct BufferedIngredientDto : INetworkSerializable
{
    public int itemId;
    public byte itemType;                     // (byte)ItemType — для UI фильтра
    public int quantity;
    public ulong sourceOwnerClientId;        // кто положил (важно для refund)
    public byte sourceType;                  // (byte)CraftingSourceType
}

public enum CraftingSourceType : byte { Inventory = 0, Warehouse = 1 /* Cargo в Phase 2 */ }
```

### 4.5 `RecipeOutputDto` + `RecipeDto`

```csharp
public struct RecipeOutputDto : INetworkSerializable
{
    public int itemId;                       // = -1 если не ItemData
    public int quantity;
    public bool isShipKey;                   // true → это ключ
    public ulong shipNetworkObjectId;        // = 0 если не корабль
}

public struct RecipeDto : INetworkSerializable
{
    public int recipeId;                     // server-assigned int
    public FixedString64Bytes displayName;
    public Sprite /* not serializable */ iconRef;   // ← ПРОБЛЕМА: Sprite нельзя в NetworkSerialize
    // Решение: шлём iconAssetGuid (string) и клиент сам резолвит через AssetDatabase-style lookup.
    // В MVP: у RecipeData есть icon, кладём рядом ScriptableObject; клиент знает assetBundle.
    // Альтернатива: прислать texture (PNG-encoded в bytes) — но это жрёт трафик.
    // Решение MVP: на клиенте ДЕРЖИМ кэш всех RecipeData (как InventoryServer._itemCache).
    public byte category;
    public RecipeIngredientDto[] ingredients;
    public RecipeOutputDto[] outputs;
    public float craftSeconds;
}

public struct RecipeIngredientDto : INetworkSerializable
{
    public int itemId;
    public int quantity;
}
```

**Проблема иконок:** в `InventoryServer._itemCache` (строка 58) уже есть локальный кэш `Dictionary<int, ItemData>`. **Повторяем** для `RecipeData`:
```csharp
// In CraftingClientState (или CraftingServer)
private readonly Dictionary<int, RecipeData> _recipeCache = new();
```
При `OnSnapshotReceived` сервер шлёт только `recipeId` → клиент лезет в свой кэш. **Но**: для корректной работы `CraftingServer` тоже должен иметь `RecipeData` lookup. Решение: `CraftingWorld.RegisterRecipe` кеширует по `recipeId` (см. §3.1).

### 4.6 `CraftingJobState`

```csharp
public enum CraftingJobState : byte
{
    Empty = 0,           // нет Job (default)
    Buffered = 1,        // ресурсы в буфере, не стартовано
    InProgress = 2,      // таймер тикает
    Completed = 3,       // готово, ждёт Collect
}
```

---

## 5. Sequence-диаграммы

### 5.1 Полный флоу: «Игрок заказал модуль»

```
[Игрок A]   [Player]    [CraftingClient]   [CraftingServer]  [CraftingStation]  [CraftingWorld]  [InventoryWorld]   [MetaReq.Registry]
   │            │              │                  │                │                  │               │                  │
   │ ──── E в зоне станции ─► │                  │                │                  │               │                  │
   │            │ ──Subscribe► │                  │                │                  │               │                  │
   │            │              │ ──SubscribeRpc──►│                │                  │               │                  │
   │            │              │                  │ ──find station (server)───►       │               │                  │
   │            │              │                  │ ──BuildSnapshot─►                │               │                  │
   │            │              │ ◄──Snapshot──────│                │                  │               │                  │
   │ ──── видит UI: рецепты, буфер пуст ───────────────────────────────────────────────────────────►                  │
   │            │              │                  │                │                  │               │                  │
   │ ──── Drag&Drop: Steel x3 из inventory ─►   │                  │                │                  │               │                  │
   │            │              │ ──AddIngredientRpc(itemId=steel, qty=3, source=Inv)─►                │               │                  │
   │            │              │                  │ ──CheckRateLimit, IsInZone, source has 3?──►                      │                  │
   │            │              │                  │                                         ──HasItems(steel,3)─►                  │
   │            │              │                  │ ◄───────────────true────────────────                       │                  │
   │            │              │                  │ ──TryRemoveByItemId(steel,3)──►                            │                  │
   │            │              │                  │ ──buffer.Add(BufferedIngredientDto{item=steel, qty=3, owner=A, source=Inv})─►        │
   │            │              │ ◄──Result{code=Ok}──              │                │                  │               │                  │
   │ ──── видит: буфер: 3 steel ────────────────────────────────────────────────────────────────►                  │
   │            │              │                  │                │                  │               │                  │
   │ ──── Drag&Drop: Bolt x5 из warehouse (в той же зоне) ─►   │                │                  │               │                  │
   │            │              │ ──AddIngredientRpc(itemId=bolt, qty=5, source=Warehouse)─►                       │               │                  │
   │            │              │                  │ ──Warehouse.TryRemove(bolt,5)─►                               │                  │
   │            │              │ ◄──Result{code=Ok}──              │                │                  │               │                  │
   │ ──── видит: буфер: 3 steel, 5 bolt ────────────────────────────────────────────────────►                  │
   │            │              │                  │                │                  │               │                  │
   │ ──── Клик "Старт" ─►      │                  │                │                  │               │                  │
   │            │              │ ──StartCraftRpc(station)─►       │                │                  │               │                  │
   │            │              │                  │ ──Check(buffer == recipe.ingredients)──►              │               │                  │
   │            │              │                  │ ──state = InProgress, committed = buffer, start = now───►              │               │                  │
   │            │              │ ◄──Result{code=Ok, jobOwner=A, recipeId=...}──    │                  │               │                  │
   │            │              │ ◄──Snapshot{state=InProgress, progress=0.0}──     │                  │               │                  │
   │ ──── видит: прогресс-бар 0% ────────────────────────────────────────────────────►                  │
   │            │              │                  │                │                  │               │                  │
   │ ... 10 мин (серверных) ...│                  │                │                  │               │                  │
   │            │              │                  │                │                  │               │                  │
   │            │              │                  │ MarketTimeService.OnMarketTick (каждые 5 мин)        │               │                  │
   │            │              │                  │ ──CraftingWorld.OnTick(dt)──►                          │               │                  │
   │            │              │                  │                                         (проверяет: now - job.start >= job.craftSeconds?)            │
   │            │              │                  │                                         (если да)──state=Completed, outputs=...──►              │
   │            │              │                  │ ◄──Snapshot{state=Completed, pendingOutputs=[]}──  │                  │               │                  │
   │ ──── видит: "Готово! Заберите." ──────────────────────────────────────────────────►                  │
   │            │              │                  │                │                  │               │                  │
   │ ──── Клик "Забрать" ─►    │                  │                │                  │               │                  │
   │            │              │ ──CollectRpc(station)─►          │                │                  │               │                  │
   │            │              │                  │ ──CheckIsOwner(A), InventoryFull?──►                │               │                  │
   │            │              │                  │ ──For each output: AddItemDirect(A, itemId, type)──►              │                  │
   │            │              │                  │ ──state=Empty──►                                    │               │                  │
   │            │              │ ◄──Result{code=Ok, itemId=module, qty=1}──  │                  │               │                  │
   │            │              │ ◄──Snapshot{state=Empty}──         │                  │               │                  │
   │            │              │                  │ ──SendInventorySnapshot(A)─►                        │               │                  │
   │            │              │ ◄──InventorySnapshot{+1 module}──  │                  │               │                  │
   │ ──── видит: модуль в инвентаре! ──────────────────────────────────────────────────────►                  │
```

**Длительность:** ~10 мин. На каждом `MarketTimeService` тике (5 мин) — приходит `CraftingSnapshotDto` с обновлённым `progress`. Клиент сам считает `progress = (now - startServerTime) / craftSeconds` и показывает ползунок, не дожидаясь snapshot (плавно).

### 5.2 Отмена (Cancel)

```
A ──► CancelCraftRpc(station)
   │ Server: CheckIsOwner(A)
   │ state In Progress? 
   │   YES: вернуть committed → inventory A (если есть место) или warehouse A
   │   NO (state Buffered): вернуть buffer → source (inventory или warehouse)
   │ state = Empty
   │   Snapshot{state=Empty}
```

**Если inventory full при возврате committed:** см. Q5 в `00_OVERVIEW.md` §9. **Рекомендация:** пробуем warehouse (если в той же зоне) → иначе ставим в "inbox" (отдельный per-player list, который будет ждать в `CompletedJobs`-like очереди). Сложно — для MVP: **всегда возвращаем в warehouse (drop on overflow)**. Это проще и понятнее игроку.

### 5.3 Корабль — рецепт через `MetaRequirement.GrantKeyToClient`

```
A ──► StartCraftRpc(station)        // stationType=Shipyard, recipe="Build Light Ship"
   │ Server: CheckIsOwner
   │   buffer covers recipe?
   │   YES: state=InProgress, start=now
   │   ... тик ...
   │ state=Completed
   │   output = RecipeOutput{item=null, shipKeyBinding=ShipA}
   │   → вызвать MetaRequirementRegistry.GrantKeyToClient(shipNetId=A_net, clientId=A)
   │     → Проверить, что ShipA зарегистрирован в MetaRequirement
   │     → Отправить ReceiveMetaRequirementBindingsTargetRpc(...) с обновлённым keyItemIds для клиента A
   │   state=Empty
   │   → SendResultTo(A, Ok+shipNetId)
   │   → SendSnapshotTo(AllInZone, state=Empty)
A ──► видит: "Готов корабль Light Ship. Ключ выдан!"
A ──► открывает инвентарь → видит ключ-Item "Key: Light Ship"
A ──► подходит к ShipA в мире, жмёт E → MetaRequirementRegistry.CanPlayerUse(A, ShipA) → true → борт
```

**Реальный вызов:** см. `MetaRequirementRegistry.cs:80-100` (текущий код принимает bindings при старте, для крафта надо добавить **grant**). План:
- Добавить `MetaRequirementRegistry.GrantKeyToClient(ulong shipNetId, ulong clientId)` (server-side).
- Метод: 1) Находит `MetaRequirement req` по `shipNetId`, 2) добавляет запись в per-client override (новая мапа `Dictionary<ulong, HashSet<int>>` в `MetaRequirementRegistry`), 3) Шлёт `ReceiveMetaRequirementBindingsTargetRpc` обновлённый.
- Это **мини-фикс в MetaRequirement** — отдельный ticket, но фундаментальный. Заложу в Implementation Plan §2.

---

## 6. Edge-cases и риски (с решениями)

### 6.1 Anti-grief: забрать чужой буфер

**Сценарий:** A наложил 3 steel, B (заказчик) нажал StartCraft. C хочет вмешаться.

**Защита:**
- `RequestAddIngredient` — нет owner-guard (станция общая, любой в зоне может **добавлять**).
- `RequestStartCraft` / `Cancel` / `Collect` — **только owner** (тот, кто стартовал или будет стартовать).
- До `StartCraft`: буфер можно отменить `RequestClearBuffer` (только тот, кто положил свою часть? Или owner-может сбросить ВСЁ? Решение в MVP: **clear buffer = owner-only**).

### 6.2 Anti-grief: заспамить буфер

**Сценарий:** C добавляет 999 steel, чтобы станция вечно была занята чужим буфером.

**Защита:**
- Rate limit (уже в `MarketServer`/`InventoryServer`).
- Лимит на размер буфера: `CraftingStation.MaxBufferSize = 32` (по числу слотов в inventory). Больше — `ResultCode.StationFull`.
- До `StartCraft` — owner может `RequestClearBuffer` (см. 6.1).

### 6.3 Игрок вышел из зоны во время крафта

**Сценарий:** A в зоне станции, нажал StartCraft, потом телепортировался / умер / оффлайн.

**Поведение:**
- Job **продолжается** (server time не зависит от игрока).
- `CraftingStation._jobState = InProgress`, `ownerClientId = A`.
- Если A оффлайн: при следующем заходе `CraftingClientState` получит `OnClientConnected` → `CraftingServer.SendSnapshotTo(A, allInProgress)` — увидит «у вас крафт на верфи: 70%».
- Если A ушёл в другую сцену (`WorldScene_X_Y`): `CraftingClientState` всё равно жив (DontDestroyOnLoad) и получит snapshot при `RequestSubscribe(stationNetId)`.

**Реализация:** `CraftingClientState` (как `MarketClientState` и `MetaRequirementClientState`) — **один инстанс** на клиента, DontDestroyOnLoad. Сцена не убивает.

### 6.4 Reconnect: игрок перезашёл, пока Job InProgress

**Сценарий:** A нажал StartCraft, вышел из игры, зашёл через 10 мин. Job уже Completed.

**Поведение:**
- `CraftingServer.OnClientConnected` → `SendSnapshotTo(A, allCompletedForClient)`.
- Snapshot: `state=Completed, pendingOutputs=[itemId=module]`.
- UI: «Готов модуль, заберите».
- A клик Collect → модуль в инвентарь.

**Сложность:** если A был Completed, но не забрал, потом вышел, потом опять зашёл — Completed лежит **вечно**? В MVP — да. **Лимит:** `CraftingWorld.MaxCompletedJobsPerClient = 10` (см. Q5 в `00_OVERVIEW.md` §9). При превышении — сервер архивирует в `OverflowLog`, игрок теряет (без warning, hard MMO-фишка). Это обсуждаемо — для MVP, скорее, **без лимита**, потому что рецепты редкие.

### 6.5 Сцена-station unloads пока Job InProgress

**Сценарий:** Игрок в `WorldScene_0_0`, крафтит на верфи. Ушёл в `WorldScene_1_0` (через стриминг). `WorldScene_0_0` **выгружается** → все `NetworkObject` в ней destroyed.

**Поведение (в MVP):**
- `CraftingStation.OnNetworkDespawn` → `CraftingServer.UnregisterStation(stationNetId)`.
- Job **уничтожается** вместе со станцией. Ресурсы **возвращаются** owner'у (inventory/warehouse) **до** уничтожения (в `UnregisterStation`).

**Это и есть Q2 в `00_OVERVIEW.md` §9.** Рекомендация: `destroyWithScene: true`, ресурсы возвращаются. Если хочется persistence — отдельный ticket, Phase 2+.

### 6.6 RecipeData удалён / переименован

**Сценарий:** Дизайнер переименовал `Recipe_SteelModule` → `Recipe_SteelModule_v2`. Старая станция имеет ссылку на старое.

**Защита:**
- `RecipeData` хранится в `Resources/Crafting/`.
- При загрузке уровня (Awake / OnEnable) — `[SerializeField] RecipeData` остаётся валидной (Unity подхватывает SO по GUID).
- Если SO удалён — `_config = null` на станции, `RegisterStation` шлёт warning + не регистрирует.
- На сервере: `CraftingWorld.RegisterRecipe` ловит null → `Debug.LogWarning("Recipe deleted, id=N not registered")`.
- UI: `RecipeDto.displayName = "???"` (или filter out из allowed).

### 6.7 Server-time drift (clock skew)

**Сценарий:** Два клиента (host + remote) видят разный `ServerTimeController.Instance.ServerTimeSeconds`.

**Защита:**
- `ServerTimeController` — server-authoritative, клиент получает через RPC или `NetworkTime` (NGO built-in).
- Прогресс считается **только на сервере**. Клиент показывает «примерно» (на глаз).
- Если `ServerTimeController` не реализован — `CraftingServer` использует `NetworkManager.ServerTime.TimeAsFloat` (NGO).

**В MVP** полагаемся на `MarketTimeService.SecondsUntilNextTick` (см. `MarketTimeService.cs:60`). Таймер крафта — `float serverTimeAtStart`, при `OnMarketTick` — `(now - start) / craftSeconds`.

### 6.8 Один игрок крафтит на 5 станциях одновременно

**Сценарий:** A заказал на верфи, на столе, на наковальне. Что видно в UI?

**Поведение:**
- `CraftingClientState` держит `Dictionary<ulong, CraftingJobDto> _myActiveJobs` (per station).
- В UI — список «Мои заказы» (отдельный sub-таб или секция).
- Каждая станция независима.

### 6.9 NetworkVariable explosion (50 станций × 6 переменных = 300 переменных)

**Решение:** см. §3.3 — refactor на `NetworkVariable<CraftingJobDto>` (один struct). **В MVP — оставляем развёрнутые**, в `50_KNOWN_ISSUES.md` записываю как TODO.

### 6.10 Race: AddIngredient + StartCraft одновременно

**Сценарий:** A добавляет ингредиент, B (заказчик) в этот же миллисекунд жмёт StartCraft. Сервер обрабатывает в порядке RPC. Если StartCraft пришёл **раньше** AddIngredient, B скажет "не хватает" → сразу после AddIngredient получит Ok. UI обновится (snapshot шлётся после каждой операции). Идемпотентно.

### 6.11 Что если рецепт — 1 модуль, а ингредиенты 999 steel?

**Поведение:** ОК. `AddIngredient` можно вызывать много раз, буфер аккумулирует. Один `StartCraft` проверяет `buffer.sum() == recipe.ingredients.sum()`. Если превышает — `ResultCode.BufferOverflow` (или просто `Ok` + warning).

### 6.12 Что если recipe.outputs = пустой массив?

**Защита:** `OnValidate` в `RecipeData`: `if (outputs.Count == 0) Debug.LogError("Recipe must have at least 1 output")`. На сервере: при `RegisterRecipe` — пропускаем с warning.

---

## 7. Подробный pipeline добавления tab в `MarketWindow`

(Уже зафиксировано в `00_OVERVIEW.md` §7; здесь — детали реализации)

### 7.1 UXML (Resources/UI/MarketWindow.uxml)

Добавить в tab-bar (рядом с существующими кнопками `market-tab-btn`, `warehouse-tab-btn`, `contracts-tab-btn`):
```xml
<ui:Button name="crafting-tab-btn" class="tab-btn" text="Крафт" />
```

Добавить секцию (рядом с `contracts-section`):
```xml
<ui:VisualElement name="crafting-section" class="tab-section" style="display: none;">
    <ui:VisualElement class="row">
        <ui:Label name="crafting-station-name" class="section-title" />
        <ui:Label name="crafting-progress" class="section-title" />
    </ui:VisualElement>
    <ui:ProgressBar name="crafting-progress-bar" low-value="0" high-value="1" />
    <ui:VisualElement class="row">
        <ui:VisualElement name="crafting-recipes-list-container" class="list-half">
            <ui:Label text="Рецепты" class="list-title" />
            <ui:ListView name="crafting-recipes-list" />
        </ui:VisualElement>
        <ui:VisualElement name="crafting-buffer-container" class="list-half">
            <ui:Label text="Буфер" class="list-title" />
            <ui:VisualElement name="crafting-buffer-grid" class="buffer-grid" />
        </ui:VisualElement>
    </ui:VisualElement>
    <ui:VisualElement class="row">
        <ui:Button name="crafting-start-btn" text="Старт" class="primary-btn" />
        <ui:Button name="crafting-cancel-btn" text="Отмена" class="secondary-btn" />
        <ui:Button name="crafting-collect-btn" text="Забрать" class="primary-btn" />
    </ui:VisualElement>
</ui:VisualElement>
```

### 7.2 USS (Resources/UI/MarketWindow.uss)

Добавить стили (по аналогии с `.contract-row`):
```css
.buffer-grid {
    flex-direction: row;
    flex-wrap: wrap;
    justify-content: flex-start;
    align-items: flex-start;
    min-height: 80px;
}
.buffer-slot {
    width: 64px;
    height: 64px;
    margin: 4px;
    background-color: rgba(0, 0, 0, 0.3);
    border-width: 1px;
    border-color: rgba(255, 255, 255, 0.2);
    border-radius: 4px;
}
.buffer-slot-filled {
    background-color: rgba(80, 180, 120, 0.4);
    border-color: rgb(80, 180, 120);
}
.buffer-slot-drop-hover {
    border-color: rgb(255, 220, 100);
    border-width: 2px;
}
```

### 7.3 MarketWindow.cs (доп. поля + методы)

(Шаблон — см. `MarketWindow.cs:50-86` (C2 contracts refactor))

```csharp
// Fields
private ListView _craftingRecipesList;
private VisualElement _craftingSection;
private VisualElement _craftingBufferGrid;
private ProgressBar _craftingProgressBar;
private Button _craftingStartBtn;
private Button _craftingCancelBtn;
private Button _craftingCollectBtn;
private int _selectedCraftingRecipeId = -1;
private int _selectedCraftingStationNetId = -1;       // 0 если не в зоне
private CraftingSnapshotDto? _craftingCache;

// In EnsureBuilt (после _contractsList):
_craftingRecipesList = _root.Q<ListView>("crafting-recipes-list");
_craftingSection = _root.Q<VisualElement>("crafting-section");
_craftingBufferGrid = _root.Q<VisualElement>("crafting-buffer-grid");
_craftingProgressBar = _root.Q<ProgressBar>("crafting-progress-bar");
_craftingStartBtn = _root.Q<Button>("crafting-start-btn");
_craftingCancelBtn = _root.Q<Button>("crafting-cancel-btn");
_craftingCollectBtn = _root.Q<Button>("crafting-collect-btn");

// Subscribe
CraftingClientState.Instance.OnSnapshotUpdated += HandleCraftingSnapshotUpdated;
CraftingClientState.Instance.OnResultReceived += HandleCraftingResultReceived;

// In OnDisable:
CraftingClientState.Instance.OnSnapshotUpdated -= HandleCraftingSnapshotUpdated;
CraftingClientState.Instance.OnResultReceived -= HandleCraftingResultReceived;

// Handlers
private void HandleCraftingSnapshotUpdated(CraftingSnapshotDto snap) { ... }
private void HandleCraftingResultReceived(CraftingResultDto result) { ... }

// Tab switching
private void SwitchTab(string tab)
{
    // ... existing code ...
    if (tab == "crafting")
    {
        _craftingSection.style.display = DisplayStyle.Flex;
        _craftingTabBtn.AddToClassList("active");
        if (_selectedCraftingStationNetId != 0)
            CraftingClientState.Instance.RequestSubscribe(_selectedCraftingStationNetId);
        ApplyCraftingFilters();
    }
    else
    {
        _craftingSection.style.display = DisplayStyle.None;
        _craftingTabBtn.RemoveFromClassList("active");
    }
}

// Drag-and-drop на _craftingBufferGrid
// Используем UI Toolkit DragAndDrop (на inventory slot + warehouse slot делаем drag операции)
// При drop на _craftingBufferGrid → RequestAddIngredient
```

### 7.4 Edge: первый показ tab без активной станции

Если игрок нажал "Крафт" tab, **не** будучи в зоне станции, UI должен показать:
- `name-label`: "—"
- `recipes-list`: пусто
- `buffer-grid`: пусто
- `progress-bar`: `value=0`, `title="Нет активной станции"`
- кнопки: disabled
- `message-label`: "Подойдите к крафт-станции"

`CraftingClientState` будет иметь `CurrentSnapshot == null` или `state == Empty` → UI рендерит placeholder.

---

## 8. Связь с NetworkPlayer

Минимальные изменения в `Assets/_Project/Scripts/Player/NetworkPlayer.cs`:

1. Добавить 2 метода (после строки 877, в секции `// ==================== CRAFTING RPC TARGETS ====================`):
```csharp
[Rpc(SendTo.Owner)]
public void ReceiveCraftingSnapshotTargetRpc(CraftingSnapshotDto snapshot, RpcParams rpcParams = default)
{
    CraftingClientState.Instance?.OnSnapshotReceived(snapshot);
}

[Rpc(SendTo.Owner)]
public void ReceiveCraftingResultTargetRpc(CraftingResultDto result, RpcParams rpcParams = default)
{
    CraftingClientState.Instance?.OnResultReceived(result);
}
```

2. **Не трогать** существующие RPC. **Не добавлять** `using ProjectC.Crafting;` в `NetworkPlayer` (не надо, `CraftingClientState.Instance?` достаточно).

---

## 9. Persistence (Phase 2+)

`CraftingWorld` в MVP **НЕ персистится**. После рестарта сервера:
- Активные Job — теряются.
- Completed Jobs (не забранные) — теряются.
- Buffered ресурсы — возвращаются в `InventoryWorld` (если ещё можно) или теряются.

Если GDD потребует persistence (см. Q2 в `00_OVERVIEW.md` §9) — добавляем:
- `IPlayerDataRepository.GetActiveCraftingJobs(clientId) → List<CraftingJobDto>`
- `IPlayerDataRepository.SaveActiveCraftingJob(clientId, job)`
- `CraftingWorld.OnServerStarted` → restore
- Тот же `ServerFileRepository` / `PlayerPrefsRepository` (см. `Assets/_Project/Trade/Scripts/Repository/`)

---

## 10. Что нужно уточнить с автором GDD (см. Open Questions)

1. Должна ли крафт-станция иметь level (1→2→3) с ускорением? (Phase 2+, но в `CraftingStationConfig` оставим `int level = 1`)
2. Должны ли разные станции крафтить разные **категории** рецептов (верфь = Ship, стол = Module)? (Скорее да, но фильтрация по `RecipeData.category` в `allowedRecipes` решает)
3. Должны ли быть `recipe.toolRequired: ItemData` (например, «молоток»)? (Phase 2+, но поле оставим `ItemData[] tools = new ItemData[0]`)
4. Должен ли быть расход топлива (`RecipeData.fuelRequired: int`)? (Phase 2+, поле оставим)
5. Должна ли быть очередь (`CraftingJobQueue`)? (Phase 2+)
6. Должны ли NPC-заказчики использовать станции? (Phase 3+, обсуждаемо)

---

## 11. Связь с существующими подсистемами — итоговая таблица

| Подсистема | Переиспользуем | Не используем | Изменяем |
|------------|----------------|---------------|----------|
| `ProjectC.Items.InventoryWorld` | `CountOf`, `HasAllItems`, `AddItemDirect`, `GetOrRegisterItemId`, `GetItemDefinition` | `TryPickup`, `TryDrop` (по slotIndex неудобно) | ДОБАВИТЬ `TryRemoveByItemId` (Phase 1) |
| `ProjectC.Items.Network.InventoryServer` | Pattern RPC, RateLimit, SendSnapshot | `_dropPickupPrefab`, `TryUse` | Ничего |
| `ProjectC.Items.Client.InventoryClientState` | Singleton, event, RequestXxx | `GetItemsByType` (только для UI) | Ничего |
| `ProjectC.Items.ItemData` | Целиком | — | Ничего |
| `ProjectC.Items.ItemType` | Целиком | — | Ничего |
| `ProjectC.Trade.Network.MarketTimeService` | Целиком (`onMarketTick`) | — | Ничего (но возможно второй сервис для sub-second — Phase 2) |
| `ProjectC.Trade.Network.MarketServer` | Pattern (RPC, Snapshot) | Логика цены, stock, NPC | Ничего |
| `ProjectC.Trade.Client.MarketClientState` | Pattern (singleton) | Логика credits, warehouse | Ничего |
| `ProjectC.Trade.Core.Warehouse` | `TryAdd` / `TryRemove` | `ComputeTotalWeight/Volume` (не наш use-case) | Ничего |
| `ProjectC.MetaRequirement.MetaRequirementRegistry` | Registry pattern, `ReceiveMetaRequirement*TargetRpc` | (это про "use", не "craft") | ДОБАВИТЬ `GrantKeyToClient` (Phase 1) |
| `ProjectC.MetaRequirement.MetaRequirement` | (для **станции** как ёлочный шар — нет) | — | Ничего |
| `ProjectC.Ship.Key.ShipKeyServer` | (legacy алиас) | Использовать напрямую не надо | Ничего (мигрирует дальше) |
| `ProjectC.World.Chest.NetworkChestContainer` | `IInteractable`, distance check, OnNetworkSpawn lifecycle | `_isOpen`, `_animationPlayed` (нам не надо) | Ничего |
| `ProjectC.UI.Client.MarketWindow` | Все 4 FIX'ы (pickingMode и пр.) | Существующий код | ДОБАВИТЬ Crafting tab |
| `ProjectC.UI.Client.CharacterWindow` | SwitchTab pattern (5 табов), RefreshCache, ensure built | `EnsureBuilt` копировать | Ничего |
| `ProjectC.Items.LootTable` | Ничего (разные use-cases) | — | Ничего |
| `ProjectC.Player.CargoSystem` | Ничего в MVP (Phase 2+) | Cargo не источник в MVP | Phase 2: сделать network |
| `ProjectC.Player.NetworkPlayer` | — | — | ДОБАВИТЬ 2 target-RPC метода |
| `ProjectC.Core.NetworkManagerController` | Auto-spawn паттерн | — | ДОБАВИТЬ спавн `CraftingClientState` |
| `ProjectC.Scripts.World.Scene.ScenePlacedObjectSpawner` | (уже есть в BootstrapScene) | — | Ничего |

---

## 12. Дальнейшее чтение

- `00_OVERVIEW.md` — обзор
- `20_IMPLEMENTATION_PLAN.md` — пошаговый план кодирования
- `30_VERIFICATION.md` — как тестировать
- `40_INSPECTOR_REFERENCE.md` — все поля SO
- `50_KNOWN_ISSUES.md` — открытые риски

