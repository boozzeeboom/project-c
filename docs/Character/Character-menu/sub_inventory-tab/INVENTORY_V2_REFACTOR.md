# Inventory v2 Refactor — единый server-authoritative инвентарь + TAB/P-таб

**Дата:** 2026-06-05
**Автор:** Mavis (Mavis)
**Статус:** 📐 Дизайн-док / план реализации
**Scope:** Phase 1-7 — рефакторинг всей инвентарной подсистемы по v2-архитектуре
**Зависит от:**
- `unity-v2-subsystem-migration` skill (канонический паттерн)
- `docs/dev/CONTRACT_V2_MIGRATION.md` (C2 — эталон реализации)
- `docs/Character-menu/00_OVERVIEW.md` (P-меню, таб "Инвентарь")
- `docs/INVENTORY_SYSTEM.md` (текущее состояние, v0.0.7)

---

## 1. Зачем (проблема)

Текущая инвентарная подсистема имеет **три критических архитектурных дефекта**, обнаруженных в начале сессии (recon 2026-06-05).

### 1.1 Две параллельные системы хранения

| Система | Файл | Тип | Где живёт | Кто пишет | Кто читает |
|---|---|---|---|---|---|
| **Локальный `Inventory`** | `Assets/_Project/Scripts/Core/Inventory.cs` | `MonoBehaviour` | child-GO каждого `NetworkPlayer` | НИКТО (см. §1.2) | `InventoryUI.cs` (TAB), `CharacterWindow.cs` (P-таб) |
| **Сетевой `NetworkInventory`** | `Assets/_Project/Scripts/Core/NetworkInventory.cs` | `NetworkBehaviour` | должен быть на `NetworkPlayer`, но **НЕ привязан** | `NetworkChestContainer` (через `AddItem`) | НИКТО из UI |

**Следствие:** сервер обновляет `NetworkInventory.NetworkVariable<InventoryData>`, но UI про это **не знает** — он читает устаревший локальный `Inventory` (который никем не заполняется).

### 1.2 Подбор предметов полностью сломан

`PickupItem.Collect()` (`Assets/_Project/Scripts/Core/PickupItem.cs:83`):
```csharp
public void Collect()
{
    if (_isCollected || itemData == null) return;
    _isCollected = true;
    gameObject.SetActive(false);   // ← ВСЁ. Никакого RPC на сервер, никакого AddItem.
    Core.InteractableManager.UnregisterPickup(this);
}
```

**Предмет исчезает из мира, но НЕ попадает в инвентарь.** Это не баг — это недоделанная фича. Плюс:
- `ItemPickupSystem.cs` (старый, 238 строк) требует `PlayerStateMachine` — **нигде не используется** в текущем коде.
- `NetworkPlayer.Update:359-374` сам обрабатывает `E`, ищет `PickupItem` / `ChestContainer` (локальные), **не `NetworkChestContainer`**.
- `NetworkInventory` ни разу не используется на `NetworkPlayer` (компонента нет на PlayerPrefab — см. §1.4).

### 1.3 TAB-колесо — устаревший IMGUI/GL код

`InventoryUI.cs`:
- Рисуется через `OnGUI` + `GL.PushMatrix` (IMGUI, не UI Toolkit) — не вписывается в v2-стиль остального проекта.
- Использует хак: `UIManager.OpenPanel("InventoryUI", 400, ...)` для отслеживания состояния.
- Триггер по Tab через собственную `InputAction` (не `PlayerInputReader`).
- Читает `inventory` (локальный) → показывает пустоту (никто туда не пишет).

### 1.4 NetworkInventory нигде не размещён

`find` по `Player*.prefab` находит **только** `PlayerSpawner.prefab`. Самого `Player.prefab` нет (или он сцен-placed). Компонент `NetworkInventory` НЕ висит ни на `PlayerSpawner`, ни на `NetworkPlayer` — поэтому `NetworkChestContainer.RequestOpenChestServerRpc:224`:
```csharp
var networkInventory = playerObject.GetComponent<NetworkInventory>();
if (networkInventory != null) { ... }
else { Debug.LogWarning("[NetworkChestContainer] NetworkInventory not found on player!"); }
```
**Drop лута из сундука молча проваливается.**

### 1.5 Датасет предметов — пустые заглушки

`Assets/_Project/Resources/Items/` содержит 8 .asset'ов `Item_Type1..8` с пустыми `itemName=""`, `description=""`, `icon=null`. Невозможно даже протестировать UI.

---

## 2. Что мы строим (целевая архитектура)

Привести инвентарь к v2-архитектуре (по образцу `ContractClientState` / `MarketClientState`):

```
SERVER (host или dedicated)
└── [InventoryServer] : NetworkBehaviour    ← в BootstrapScene, DontDestroyOnLoad
    ├── Initialize InventoryWorld on OnNetworkSpawn
    ├── InventoryWorld (POCO singleton)     ← business logic, persistence
    │     ├── Dictionary<int, InventorySlot> ← runtime state
    │     ├── TryPickup / TryDrop / TryUse  ← return InventoryOpResult
    │     ├── BuildSnapshot(clientId) → DTO
    │     └── GetItemDefinition(itemId)
    ├── [Rpc(SendTo.Server)] per operation (Pickup, Drop, Use, Move)
    └── [Rpc(SendTo.Owner)] private for snapshot/result delivery

└── [NetworkPlayer] (изменения)
    ├── Привязка NetworkInventory → NetworkInventory
    └── 2 новых TargetRpc: ReceiveInventorySnapshotTargetRpc, ReceiveInventoryResultTargetRpc

CLIENT
└── [InventoryClientState] : MonoBehaviour   ← auto-spawn в NetworkManagerController
    ├── CurrentSnapshot / LastResult
    ├── OnSnapshotUpdated / OnInventoryResult
    └── RequestPickup / RequestDrop / RequestUse / RequestMove

└── [InventoryUI] : MonoBehaviour + UIDocument   ← TAB-колесо, переписано на UI Toolkit
    └── Subscribe to InventoryClientState.OnSnapshotUpdated

└── [CharacterWindow] (изменения, таб "Инвентарь")
    └── Subscribe to InventoryClientState.OnSnapshotUpdated (вместо GetComponentInChildren<Inventory>)
```

### 2.1 Инварианты

1. **Сервер — единственный источник истины.** Клиент НИКОГДА не мутирует состояние инвентаря напрямую. Все изменения — через `[Rpc(SendTo.Server)]`.
2. **UI читает ТОЛЬКО из `InventoryClientState`.** Никаких `GetComponentInChildren<Inventory>()`, `FindObjectsByType<Inventory>()`, `Resources.Load<Inventory>()` в UI-коде.
3. **TAB и P-таб подписаны на ОДНО событие** (`InventoryClientState.OnSnapshotUpdated`) — гарантирует согласованность данных.
4. **Дроп из сундука** идёт через `NetworkInventory.AddItem` (server), `NetworkVariable` обновляется → все клиенты получают snapshot → оба UI (TAB + P) обновляются.
5. **Подбор pickup'а** идёт через `NetworkInventory.PickupItemServerRpc` (server-authoritative) — клиент только запрашивает.

---

## 3. Структура файлов (создаются/изменяются)

### 3.1 Создаются (новые)

| # | Путь | Размер (примерно) | Назначение |
|---|---|---|---|
| 1 | `Assets/_Project/Items/Items.asmdef?` | — | **НЕ создаём** (AGENTS.md: нельзя без явного одобрения) |
| 2 | `Assets/_Project/Items/Client/InventoryClientState.cs` | ~150 строк | Клиентская проекция (singleton, DontDestroyOnLoad) |
| 3 | `Assets/_Project/Items/Network/InventoryServer.cs` | ~250 строк | RPC hub + NetworkVariable<InventoryData> (наследует/объединяет NetworkInventory) |
| 4 | `Assets/_Project/Items/Dto/InventoryItemDto.cs` | ~50 строк | Один предмет (itemId, type, qty, definition) |
| 5 | `Assets/_Project/Items/Dto/InventorySnapshotDto.cs` | ~70 строк | Снимок инвентаря (locationId, slots[], credits) |
| 6 | `Assets/_Project/Items/Dto/InventoryResultDto.cs` | ~50 строк | Результат операции (success, code, message, newCredits) |
| 7 | `Assets/_Project/Items/Dto/InventoryResultCode.cs` | ~25 строк | Enum: Ok, NotInZone, InventoryFull, ... |
| 8 | `Assets/_Project/Items/Core/InventoryWorld.cs` | ~300 строк | POCO singleton, бизнес-логика |
| 9 | `Assets/_Project/Items/Core/InventoryDefinition.cs` | ~80 строк | ScriptableObject для предмета (itemName, icon, maxStack, ... ) |
| 10 | `Assets/_Project/Items/Core/InventoryItemConfig.cs` | ~50 строк | SO для LootTable и тестов |
| 11 | `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` | ~80 строк | UI Toolkit: колесо 8 секторов + sublist |
| 12 | `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` | ~150 строк | Стили (реюз из `MarketWindow.uss` + radial layout) |
| 13 | `Assets/_Project/UI/Client/InventoryUI.cs` | переписан | UI Toolkit окно, подписка на `InventoryClientState` |

**Итого: ~1255 строк нового кода** + датасет (8-16 .asset'ов).

### 3.2 Изменяются

| # | Путь | Что меняется | Размер diff |
|---|---|---|---|
| 1 | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | +2 TargetRpc: `ReceiveInventorySnapshotTargetRpc`, `ReceiveInventoryResultTargetRpc`. Они делегируют в `InventoryClientState`. | +30 строк |
| 2 | `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | +1 метод: `CreateInventoryClientState()` (auto-spawn рядом с `CreateMarketClientState`, `CreateContractClientState`) | +20 строк |
| 3 | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | `RefreshInventoryCache` — вместо `GetComponentInChildren<Inventory>()` читать `InventoryClientState.Instance?.CurrentSnapshot`. Подписка на `OnSnapshotUpdated` в `OnEnable`, отписка в `OnDisable`. | ~50 строк diff |
| 4 | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (в `Update` → pickup-логика) | `FindNearestInteractable` — искать `NetworkChestContainer` + `PickupItem` правильно; вызывать `PickupItem.Collect()` → `NetworkInventory.PickupItemServerRpc`. | ~30 строк diff |
| 5 | `Assets/_Project/Scripts/Core/PickupItem.cs` | `Collect()` — НЕ деактивировать сразу. Сделать `RequestPickupServerRpc(itemId, itemType, position)` через `NetworkInventory`. После server-confirmation — деактивировать. | ~40 строк diff |
| 6 | `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs` | Проверить, что `NetworkInventory` гарантированно есть на `playerObject` (после Phase 2). | ~5 строк diff |
| 7 | `Assets/_Project/Scripts/Player/PlayerInputReader.cs` (?) | Возможно, добавить `inventory` action (Tab). Или оставить legacy InputAction в `InventoryUI.Awake`. | tbd |

### 3.3 Удаляются (Phase 7 — после верификации)

| # | Путь | Когда удалять |
|---|---|---|
| 1 | `Assets/_Project/Scripts/Core/Inventory.cs` | После Phase 6: подтверждено, что UI везде использует `InventoryClientState` |
| 2 | `Assets/_Project/Scripts/Player/ItemPickupSystem.cs` | После Phase 3: подтверждено, что pickup идёт через `NetworkPlayer.Update` |
| 3 | Старый `InventoryUI.cs` (IMGUI/GL версия) | После Phase 4: новая UI Toolkit версия заменяет полностью |

**ВНИМАНИЕ:** удаление делается в **отдельной cleanup-фазе** (отдельная сессия), не в этой. Параллельный стек живёт 1-2 сессии для safety net, как в C2 (см. `unity-v2-subsystem-migration` §3, parallel-stack pattern).

---

## 4. Слой за слоем (детали)

### 4.1 DTO (`Assets/_Project/Items/Dto/`)

#### `InventoryItemDto.cs`
```csharp
[Serializable]
public struct InventoryItemDto : INetworkSerializable
{
    public int  itemId;        // unique ID в ItemDatabase
    public byte type;          // (byte)ItemType
    public int  quantity;      // stack count (1 по умолчанию)
    public int  slotIndex;     // 0..maxSlots-1

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref itemId);
        s.SerializeValue(ref type);
        s.SerializeValue(ref quantity);
        s.SerializeValue(ref slotIndex);
    }
}
```

#### `InventorySnapshotDto.cs`
```csharp
[Serializable]
public struct InventorySnapshotDto : INetworkSerializable
{
    public string locationId;            // null если не в зоне
    public InventoryItemDto[] items;     // все предметы (включая qty=0 для пустых)
    public int   maxSlots;
    public float credits;                // для кросс-таба в CharacterWindow

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        // STRING (с длиной)
        bool hasLoc = !string.IsNullOrEmpty(locationId);
        s.SerializeValue(ref hasLoc);
        if (hasLoc) { s.SerializeValue(ref locationId); }

        // ARRAY (с длиной)
        int len = items?.Length ?? 0;
        s.SerializeValue(ref len);
        if (s.IsReader) items = len > 0 ? new InventoryItemDto[len] : null;
        for (int i = 0; i < len; i++)
        {
            var x = items[i];
            x.NetworkSerialize(s);
            items[i] = x;
        }

        s.SerializeValue(ref maxSlots);
        s.SerializeValue(ref credits);
    }
}
```

**Pitfall #2 (nullable struct serialise)** не применим — `locationId` это `string?` (ссылочный тип), не `Nullable<T>`. Для строк паттерн «hasLoc flag» достаточен.

#### `InventoryResultDto.cs` + `InventoryResultCode.cs`
```csharp
public enum InventoryResultCode : byte
{
    Ok = 0,
    NotInZone = 1,
    InventoryFull = 2,
    ItemNotFound = 3,
    NotEnoughQuantity = 4,
    InvalidSlot = 5,
    RateLimited = 6,
    InternalError = 7,
    NoPermission = 8,    // не Owner
}

[Serializable]
public struct InventoryResultDto : INetworkSerializable
{
    public byte  code;           // (byte)InventoryResultCode
    public string message;       // локализованное (на сервере — на языке клиента, см. ниже)
    public int   itemId;         // -1 если не применимо
    public int   slotIndex;      // -1 если не применимо
    public bool  isSuccess => code == (byte)InventoryResultCode.Ok;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref code);
        bool hasMsg = !string.IsNullOrEmpty(message);
        s.SerializeValue(ref hasMsg);
        if (hasMsg) s.SerializeValue(ref message);
        s.SerializeValue(ref itemId);
        s.SerializeValue(ref slotIndex);
    }
}
```

### 4.2 Core (`Assets/_Project/Items/Core/InventoryWorld.cs`)

POCO singleton (НЕ `MonoBehaviour`, НЕ `NetworkBehaviour`), как `ContractWorld`:

```csharp
namespace ProjectC.Items
{
    public class InventoryWorld
    {
        public static InventoryWorld Instance { get; private set; }

        private readonly Dictionary<ulong, InventoryData> _playerInventories = new();
        private readonly Dictionary<int, ItemData> _itemDatabase = new();
        private const int MAX_SLOTS = 32;
        private const int MAX_STACK = 99;

        public static InventoryWorld CreateAndInitialize()
        {
            Instance = new InventoryWorld();
            Instance.RegisterAllItems();
            return Instance;
        }

        public void Shutdown() { /* ... */ }

        // === ITEM DATABASE ===
        public void RegisterItem(int id, ItemData def) => _itemDatabase[id] = def;
        public ItemData GetItemDefinition(int id) => _itemDatabase.TryGetValue(id, out var d) ? d : null;

        // === PER-PLAYER STATE ===
        public InventoryData GetOrCreate(ulong clientId) { /* ... */ }
        public InventoryData Get(ulong clientId) { /* ... */ }

        // === OPERATIONS ===
        public InventoryResult TryPickup(ulong clientId, int itemId, ItemType type, Vector3 worldPos, Vector3 playerPos) { /* ... */ }
        public InventoryResult TryDrop(ulong clientId, int slotIndex, int quantity, Vector3 worldPos) { /* ... */ }
        public InventoryResult TryMove(ulong clientId, int fromSlot, int toSlot) { /* ... */ }
        public InventoryResult TryUse(ulong clientId, int slotIndex) { /* ... */ }

        // === SNAPSHOT ===
        public InventorySnapshotDto BuildSnapshot(ulong clientId, string locationId) { /* ... */ }

        // === INITIALIZATION ===
        private void RegisterAllItems()
        {
            // Resources/Items/ — все ItemData
            var all = Resources.LoadAll<ItemData>("Items");
            int id = 1;
            foreach (var item in all)
            {
                if (item == null) continue;
                _itemDatabase[id++] = item;
            }
        }
    }
}
```

**Ключевые решения:**
- `InventoryData` — уже существует, **используем его** (не дублируем). Расширим: добавим `int[]` слотов для `quantity` (сейчас каждый ID = 1 юнит, что неэффективно по bandwidth).
- `maxSlots = 32` — default в `NetworkInventory` сейчас, оставляем.
- `MAX_STACK = 99` — стандарт для RPG, оставляем как `ItemData.maxStack` (добавим поле, см. §4.3).
- Анти-чит: `TryPickup` проверяет `Vector3.Distance(worldPos, playerPos) <= 5f` (как в текущем `NetworkInventory.PickupItemServerRpc:121`).

### 4.3 ItemData (расширение существующего)

`Assets/_Project/Scripts/Core/ItemType.cs`:
```csharp
[CreateAssetMenu(fileName = "NewItem", menuName = "Project C/Item Data", order = 1)]
public class ItemData : ScriptableObject
{
    public string   itemName;
    public ItemType itemType;
    [TextArea] public string description;
    public Sprite   icon;
    public int      maxStack   = 1;    // НОВОЕ: 1 = non-stackable, >1 = stackable
    public float    weightKg   = 0.1f; // НОВОЕ: для будущего cargo system
}
```

**Без `using` изменений** в существующих файлах — просто доп-поле. Дефолт `maxStack=1` сохраняет старое поведение.

### 4.4 Network (`Assets/_Project/Items/Network/InventoryServer.cs`)

**Подход:** НЕ создаём новый файл с нуля. **Рефакторим** существующий `NetworkInventory.cs`:
- Переименовываем в `InventoryServer.cs` (как `MarketServer` vs `MarketClientState`).
- Добавляем `[Rpc(SendTo.Owner)]` для snapshot/result delivery через `NetworkPlayer.ReceiveInventorySnapshotTargetRpc` (как `ContractServer`).
- Добавляем `InventoryWorld.Instance` интеграцию.
- Anti-cheat: distance check, rate limit, ownership check.

**Решение:** файл `NetworkInventory.cs` → переименовать/слить с новым `InventoryServer.cs`. Старые callers (`NetworkChestContainer`, `NetworkPlayer`) обновить. Делаем в Phase 2.

```csharp
public class InventoryServer : NetworkBehaviour
{
    public static InventoryServer Instance { get; private set; }

    private NetworkVariable<InventoryData> _data = new(
        new InventoryData(true),
        NetworkVariableReadPermission.Owner,    // ← ТОЛЬКО владелец (security)
        NetworkVariableWritePermission.Server
    );

    // Событие для legacy-кода (пока оставлено для плавной миграции)
    public event Action OnInventoryChanged;
    public InventoryData Data => _data.Value;
    public int Count => _data.Value.TotalCount;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && InventoryWorld.Instance == null)
            InventoryWorld.CreateAndInitialize();
        Instance = this;
        _data.OnValueChanged += (prev, next) => OnInventoryChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
        base.OnNetworkDespawn();
    }

    // =====================================================
    // SERVER APIs (legacy — для NetworkChestContainer)
    // =====================================================

    public void AddItem(int itemId, ItemType type)
    {
        if (!IsServer) return;
        var data = _data.Value;
        if (data.TotalCount >= MAX_SLOTS) return;
        data.AddItem(type, itemId);
        _data.Value = data;
    }

    // =====================================================
    // CLIENT RPCs (новые)
    // =====================================================

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestPickupRpc(int itemId, byte typeByte, Vector3 worldPos, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        // World pickup from world position
        var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerObj == null) { SendFail(clientId, InventoryResultCode.NoPermission); return; }
        float dist = Vector3.Distance(playerObj.transform.position, worldPos);
        if (dist > 5f) { SendFail(clientId, InventoryResultCode.NotInZone); return; }

        var result = InventoryWorld.Instance.TryPickup(clientId, itemId, (ItemType)typeByte, worldPos, playerObj.transform.position);
        if (result.IsSuccess)
        {
            _data.Value = InventoryWorld.Instance.Get(clientId);
            SendSnapshot(clientId, InventoryWorld.Instance.BuildSnapshot(clientId, null));
        }
        SendResult(clientId, result);
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestDropRpc(int slotIndex, int quantity, Vector3 worldPos, RpcParams rpcParams = default) { /* ... */ }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestMoveRpc(int fromSlot, int toSlot, RpcParams rpcParams = default) { /* ... */ }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestUseRpc(int slotIndex, RpcParams rpcParams = default) { /* ... */ }

    // =====================================================
    // DELIVERY (через NetworkPlayer TargetRpc)
    // =====================================================

    private void SendSnapshot(ulong clientId, InventorySnapshotDto snap) { /* find NetworkPlayer → ReceiveInventorySnapshotTargetRpc */ }
    private void SendResult(ulong clientId, InventoryResultDto result) { /* find NetworkPlayer → ReceiveInventoryResultTargetRpc */ }
    private void SendFail(ulong clientId, InventoryResultCode code) => SendResult(clientId, new InventoryResultDto { code = (byte)code, ... });
}
```

### 4.5 Client (`Assets/_Project/Items/Client/InventoryClientState.cs`)

Копия `ContractClientState` структуры:

```csharp
public class InventoryClientState : MonoBehaviour
{
    public static InventoryClientState Instance { get; private set; }

    public InventorySnapshotDto? CurrentSnapshot { get; private set; }
    public InventoryResultDto? LastResult { get; private set; }

    public event Action<InventorySnapshotDto> OnSnapshotUpdated;
    public event Action<InventoryResultDto> OnInventoryResult;

    private void Awake() { Instance = this; DontDestroyOnLoad(gameObject); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void OnSnapshotReceived(InventorySnapshotDto snap)
    {
        CurrentSnapshot = snap;
        OnSnapshotUpdated?.Invoke(snap);
    }

    public void OnResultReceived(InventoryResultDto result)
    {
        LastResult = result;
        OnInventoryResult?.Invoke(result);
    }

    // === UI API ===
    public void RequestPickup(int itemId, ItemType type, Vector3 worldPos)
    {
        if (InventoryServer.Instance == null) return;
        InventoryServer.Instance.RequestPickupRpc(itemId, (byte)type, worldPos);
    }

    public void RequestDrop(int slotIndex, int qty, Vector3 worldPos) { /* ... */ }
    public void RequestMove(int fromSlot, int toSlot) { /* ... */ }
    public void RequestUse(int slotIndex) { /* ... */ }

    // === HELPERS (для UI) ===
    public List<InventoryItemDto> GetItemsByType(ItemType type) { /* ... */ }
    public int GetCountByType(ItemType type) { /* ... */ }
    public bool HasItem(int itemId) { /* ... */ }
}
```

### 4.6 Network patches

#### `NetworkPlayer.cs` — добавить 2 TargetRpc

```csharp
// === Inventory (Phase 1) ===
[Rpc(SendTo.Owner)]
public void ReceiveInventorySnapshotTargetRpc(InventorySnapshotDto snapshot, RpcParams rpcParams = default)
{
    var state = ProjectC.Items.Client.InventoryClientState.Instance;
    if (state != null) state.OnSnapshotReceived(snapshot);
}

[Rpc(SendTo.Owner)]
public void ReceiveInventoryResultTargetRpc(InventoryResultDto result, RpcParams rpcParams = default)
{
    var state = ProjectC.Items.Client.InventoryClientState.Instance;
    if (state != null) state.OnResultReceived(result);
}
```

#### `NetworkManagerController.cs` — auto-spawn

```csharp
private void CreateInventoryClientState()
{
    if (ProjectC.Items.Client.InventoryClientState.Instance != null) return;
    var go = new GameObject("[InventoryClientState]");
    go.AddComponent<ProjectC.Items.Client.InventoryClientState>();
    // DontDestroyOnLoad вызывается внутри Awake
}
```

Вызвать рядом с `CreateMarketClientState()` / `CreateContractClientState()`.

### 4.7 PickupItem → NetworkInventory

```csharp
public class PickupItem : MonoBehaviour, IInteractable
{
    public ItemData itemData;
    private int _itemId = -1;
    private bool _isCollected;

    private void Start()
    {
        // Регистрируем в ItemDatabase (получаем id)
        _itemId = NetworkInventory.GetItemId(itemData);
        // ... существующий код инициализации
    }

    public void Collect()
    {
        if (_isCollected || itemData == null) return;
        // НЕ деактивируем сразу — дождёмся server confirmation
        var inv = FindLocalPlayerInventory();
        if (inv == null) { Debug.LogWarning("[PickupItem] No local NetworkInventory"); return; }
        inv.RequestPickupRpc(_itemId, (byte)itemData.itemType, transform.position);
    }

    private void OnPickupConfirmed()  // вызывается из InventoryClientState.OnInventoryResult
    {
        _isCollected = true;
        gameObject.SetActive(false);
    }
}
```

**Важно:** `NetworkInventory` сейчас `static GetItemId` (ищет в `_itemDatabase`). После Phase 1 эта база переедет в `InventoryWorld._itemDatabase`. Адаптируем.

### 4.8 UI: TAB-колесо (UI Toolkit)

**Полная переписка `InventoryUI.cs`.** Новые правила:
- `UIDocument` + UXML/USS, не `OnGUI`/`GL`.
- 8 секторов = `VisualElement` 45° каждый, расположены по кругу через `transform: rotate()` + `position: absolute`.
- Hover: через `PointerEnter/Leave` (не raycast).
- Подписка на `InventoryClientState.OnSnapshotUpdated`.
- Скрытие по Tab (через PlayerInputReader или свой `InputAction`, как раньше — обсудим с пользователем).

**Альтернатива:** не писать своё колесо с нуля, а найти Unity Asset или использовать `<RadialMenu>` из `UIExtensions`. Решение: сначала делаем своё (полный контроль над стилями), в будущем — refactor на готовое.

### 4.9 P-таб в CharacterWindow

`CharacterWindow.cs` изменения:
1. `RefreshInventoryCache` — заменить чтение `GetComponentInChildren<Inventory>()` на `InventoryClientState.Instance.CurrentSnapshot`.
2. `OnEnable` — подписаться на `InventoryClientState.OnSnapshotUpdated` → пересобрать список.
3. `OnDisable` — отписаться (как для `ContractClientState`).
4. Если таб "Инвентарь" активен и snapshot обновился — `Rebuild()`.

Минимальный diff, ~50 строк.

---

## 5. Датасет предметов (Phase 6)

Заменяем 8 пустых `Item_Type1..8.asset` на 8 типов × 2-3 варианта = **16-24 реальных ItemData**.

По 2-3 предмета на каждый `ItemType`:

| ItemType | Примеры (2-3 шт) |
|---|---|
| Resources | Железная руда, Медная руда, Кристаллическая пыль |
| Equipment | Верёвка 10м, Карабин, Фонарь |
| Food | Сухпаёк, Консервы, Бутыль воды |
| Fuel | Антигравитационное топливо, Угольные брикеты |
| Antigrav | Антиграв-камень малый, Антиграв-камень большой |
| Meziy | Мезий-крошка, Мезий-кристалл |
| Medical | Бинт, Антисептик, Стимулятор |
| Tech | Батарея, Микросхема, Кабель |

Каждый .asset получает:
- Уникальное имя (не пустое)
- Краткое описание
- `icon` — заглушка (UnityEngine.UI.Image placeholder, или пропустить icon)
- `maxStack` (1 для оборудования, 10-20 для ресурсов/еды)
- `weightKg` (0.1 — 5.0)

Создаём через `MCP` (Unity-side, требует Editor) или питон-скриптом, который генерирует .asset файлы руками. Последнее сложнее из-за GUID'ов; **рекомендую** делать через Editor.

---

## 6. Verification (что ты запускаешь после моих изменений)

### 6.1 Compile check (каждый batch)
```
1. Открой Unity Editor → дождись компиляции → Console → 0 errors expected.
2. Если ошибки — кинь текстом, я исправлю.
```

### 6.2 In-editor smoke (после Phase 2 — NetworkInventory на PlayerPrefab)
```
1. Запусти Play → Start Host.
2. Подбери предмет (E) → проверь, что:
   - Предмет исчез из мира ✓
   - В TAB-колесе появилась запись (icon + name + qty)
   - В P-меню → Инвентарь та же запись
3. Открой сундук (E) → проверь, что:
   - Все предметы из LootTable попали в инвентарь
   - Оба UI обновились
4. Поставь курсор на сектор колеса → появился подсписок
5. Закрой TAB → P → Inventory → тот же набор
```

### 6.3 Multi-client smoke
```
1. Запусти dedicated server (headless) + 2 client'a через ParrelSync или 2 Editor.
2. На Client 1: подбери предмет.
3. На Client 2: открой TAB-колесо → видно тот же предмет (серверная синхронизация работает).
```

### 6.4 Code verification
```bash
# Файлы созданы
ls Assets/_Project/Items/Client/InventoryClientState.cs
ls Assets/_Project/Items/Network/InventoryServer.cs
ls Assets/_Project/Items/Dto/*.cs
ls Assets/_Project/Items/Core/InventoryWorld.cs
ls Assets/_Project/UI/Resources/UI/InventoryWheel.uxml
ls Assets/_Project/UI/Resources/UI/InventoryWheel.uss

# Legacy ещё жив (для safety net)
ls Assets/_Project/Scripts/Core/Inventory.cs            # legacy, НЕ удалён в Phase 1-6
ls Assets/_Project/Scripts/UI/InventoryUI.cs            # legacy, заменён, НЕ удалён
ls Assets/_Project/Scripts/Player/ItemPickupSystem.cs   # legacy, НЕ удалён

# Проверка что TAB + P-таб используют ОДИН source of truth
grep -n "InventoryClientState" Assets/_Project/UI/Client/InventoryUI.cs
grep -n "InventoryClientState" Assets/_Project/Scripts/UI/Client/CharacterWindow.cs
# оба должны иметь подписку OnSnapshotUpdated
```

---

## 7. Открытые вопросы (нужны твои ответы)

1. **TAB-колесо: своё UI Toolkit или найти готовое решение?**
   - Своё: полный контроль, +150 строк USS, время = 1-2 часа
   - Готовое (Unity UI Extensions RadialMenu): быстрее, но стили не под наш CloudGhibli
   - **Моя рекомендация:** своё (соответствует паттерну C2).

2. **Input: Tab для колеса — оставить legacy `InputAction` или мигрировать в `PlayerInputReader`?**
   - Legacy работает, не ломаем. Можно отдельным тикетом.

3. **`NetworkInventory` → `InventoryServer`: rename или новое имя?**
   - Rename чище, но много диффов. Можно оставить `NetworkInventory` как legacy-имя + новый `InventoryServer` (parallel stack).
   - **Моя рекомендация:** rename, так как v1-вызовов (кроме `NetworkChestContainer` и `PickupItem`) почти нет, и те обновим в Phase 2.

4. **Стоимость: PickupItem подвешиваем как trigger-зона или остаёмся на E-press?**
   - Сейчас на E-press. Оставляем.

5. **Тестовый датасет: генерируем через Editor-скрипт или руками?**
   - Editor-скрипт `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs` — создаёт 16-24 .asset'ов одной кнопкой (Tools → Project C → Generate Test Items).
   - **Моя рекомендация:** Editor-скрипт + запуск через MCP. Удобнее, чем руками.

---

## 8. Порядок реализации (для меня)

| Phase | Что делаю | Твои действия |
|---|---|---|
| 0 | Дизайн-док (этот файл) | Прочитать, ответить на вопросы §7 |
| 1 | DTOs + Core (InventoryWorld) + ClientState + NetworkServer stub | Compile-check, читаешь код |
| 2 | NetworkServer full + переименование NetworkInventory + привязка к PlayerPrefab через MCP | Compile, 0 errors. Сцена готова. |
| 3 | PickupItem + NetworkPlayer pickup-lогика | Smoke test: подбор работает |
| 4 | UI Toolkit: InventoryWheel.uxml/uss + InventoryUI переписан | Визуальная проверка колеса |
| 5 | CharacterWindow: подписка на InventoryClientState | Smoke: TAB и P-таб показывают одно и то же |
| 6 | Датасет: 16-24 ItemData через Editor-скрипт | Видно, что предметы красивые |
| 7 | Документация + verification checklist | Финальный smoke, скриншоты |
| **+1 сессия** | Cleanup: удалить `Inventory.cs`, `ItemPickupSystem.cs`, legacy `InventoryUI.cs` | — |

**Ожидаемый объём:** ~1255 строк нового кода + 30-40 строк диффов в NetworkPlayer/NetworkManagerController/CharacterWindow + 16-24 .asset'а.

---

## 9. Связанные документы

- `unity-v2-subsystem-migration` skill (skill_view name)
- `docs/dev/CONTRACT_V2_MIGRATION.md` — C2 reference (аналогичная миграция)
- `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` — паттерн merge в таб
- `docs/Character-menu/00_OVERVIEW.md` — P-меню архитектура
- `docs/INVENTORY_SYSTEM.md` — текущее состояние (v0.0.7)
- `docs/gdd/GDD_11_Inventory_Items.md` — game design
- `AGENTS.md` — hard rules
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — reference RPC hub
- `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` — reference projection

---

## 10. История изменений

| Дата | Что |
|---|---|
| 2026-06-05 | Создан. Recon завершён, scope согласован с пользователем ("full"). |

---

## 11. Статус реализации (после сессии 2026-06-05)

### 11.1 Что сделано

**Phase 0 (Recon) — ✅**
- Прочитаны все ключевые файлы: `Inventory.cs`, `NetworkInventory.cs`, `InventoryUI.cs` (старый IMGUI), `InventoryData.cs`, `ItemType.cs`, `ItemDatabaseInitializer.cs`, `NetworkChestContainer.cs`, `NetworkPlayer.cs`, `ItemPickupSystem.cs`, `PickupItem.cs`, `CharacterWindow.cs`, `ContractClientState.cs`, `MarketClientState.cs`.
- Найдены 3 критических дефекта (две параллельные системы инвентаря, сломанный pickup, отсутствие NetworkInventory на PlayerPrefab).
- Согласован scope "full" (Phases 1-7).

**Phase 1 (InventoryClientState + World) — ✅**
- `Assets/_Project/Items/Dto/InventoryItemDto.cs` (1 INetworkSerializable struct)
- `Assets/_Project/Items/Dto/InventorySnapshotDto.cs` (с locationId, items[], maxSlots, credits)
- `Assets/_Project/Items/Dto/InventoryResultDto.cs` (с IsSuccess, LocalizeResultCode)
- `Assets/_Project/Items/Dto/InventoryResultCode.cs` (11 кодов: Ok, NotInZone, InventoryFull, ...)
- `Assets/_Project/Items/Core/InventoryWorld.cs` (POCO singleton, бизнес-логика, item database, per-player state, TryPickup/TryDrop/TryMove/TryUse, BuildSnapshot)
- `Assets/_Project/Items/Client/InventoryClientState.cs` (singleton, OnSnapshotUpdated/OnInventoryResult, GetItems/GetItemsByType/GetCountByType/HasItemsInType/GetTotalItemCount, Request* API)
- `Assets/_Project/Items/Network/InventoryServer.cs` (NetworkBehaviour, RPC hub, RequestPickupRpc/DropRpc/MoveRpc/UseRpc/RefreshRpc, SendSnapshot/SendResult, rate limit, item cache)

**Phase 2 (NetworkInventory на сцене) — ✅**
- `[InventoryServer]` GameObject создан в BootstrapScene через MCP: NetworkObject + InventoryServer.
- Сцена сохранена.
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — `CreateInventoryClientState()` auto-spawn (как `CreateMarketClientState`/`CreateContractClientState`).
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — 2 новых TargetRpc: `ReceiveInventorySnapshotTargetRpc`, `ReceiveInventoryResultTargetRpc`.

**Phase 3 (PickupItem → NetworkInventory) — ✅**
- `Assets/_Project/Scripts/Core/PickupItem.cs` — `Collect()` теперь шлёт `RequestPickup` через `InventoryClientState`. Подписка на `OnInventoryResult` для подтверждения. `ForceCollect()` оставлен как legacy fallback.

**Phase 4 (UI Toolkit TAB-колесо) — ✅**
- `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` (8 секторов, центр, sublist, actions, message)
- `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` (radial layout через position: absolute + классы sector-empty/has-items/hover/selected)
- `Assets/_Project/UI/Client/InventoryUI.cs` — полностью переписан с IMGUI/GL на UI Toolkit. Подписка на `InventoryClientState.OnSnapshotUpdated`, hover/select через USS classes, sublist `ListView`.
- `[InventoryWheel]` GameObject создан в BootstrapScene: UIDocument (PanelSettings=MarketPanelSettings, sourceAsset=InventoryWheel.uxml) + InventoryUI.
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs:SpawnInventory()` — не инстанцирует старые `Inventory`/`InventoryUI` (legacy fallback через null-check).

**Phase 5 (P-таб CharacterWindow) — ✅**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`:
  - `using ProjectC.Items.Client;` + `ProjectC.Items.Dto;`
  - Подписка на `InventoryClientState.OnSnapshotUpdated` + `OnInventoryResult` в `EnsureBuilt`/`OnDisable` (с unsubscribe — event-leak prevention)
  - `RefreshInventoryCache` — читает из `InventoryClientState.CurrentSnapshot` (вместо локального `Inventory` через reflection)
  - `HandleInventorySnapshotUpdated` — обновляет credits в header (cross-tab) + кэш sublist
  - `HandleInventoryResultReceived` — cross-tab feedback в message label

**Phase 6 (Test dataset) — ✅**
- `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs` — `[MenuItem("Tools/Project C/Inventory/Generate Test Dataset")]` создаёт 24 ItemData (8 типов × 3 варианта) с именами, описаниями, maxStack, weightKg.
- **Запущен через MCP `execute_code`** — 24 .asset созданы в `Resources/Items/` (Item_Antigrav_*, Item_Equipment_*, ..., Item_Tech_*).
- `Assets/_Project/Scripts/Core/ItemType.cs` — добавлены поля `maxStack` (1 по дефолту) + `weightKg` (0.1 по дефолту) в `ItemData`. Обратная совместимость с Item_Type1..8 сохранена.

**Phase 7 (Документация) — ✅**
- Этот документ (37 KB).
- Все патчи прокомментированы (// Phase N (INVENTORY_V2_REFACTOR.md): ...)

### 11.2 Compile state
**0 ошибок**, 0 моих warnings (pre-existing legacy warnings не мои).

### 11.3 Parallel stack (safety net, не удалено в этой сессии)
- `Assets/_Project/Scripts/Core/Inventory.cs` — локальный, больше не инстанцируется (NetworkPlayer.SpawnInventory ставит `_inventory = null`)
- `Assets/_Project/Scripts/UI/InventoryUI.cs` (IMGUI) — больше не инстанцируется, файл живёт
- `Assets/_Project/Scripts/Player/ItemPickupSystem.cs` — нигде не используется, файл живёт
- `Assets/_Project/Scripts/Core/NetworkInventory.cs` — файл живёт, используется в NetworkChestContainer (TODO: мигрировать на InventoryServer в Phase 8)
- Старые `Item_Type1..8` (заглушки) — живут рядом с новыми 24 .asset'ами

### 11.4 Что осталось (отдельные сессии)
- **Cleanup-сессия:** удалить `Inventory.cs`, `InventoryUI.cs` (IMGUI), `ItemPickupSystem.cs`, `NetworkInventory.cs`, `Item_Type1..8` (после проверки что LootTable'ы на них не ссылаются).
- **Phase 8 (следующая большая сессия):**
  - Stackable inventory (inventory-by-slot вместо flat-list; quantity-aware операции)
  - Drop в мир (SpawnPickupItem на сервере при drop)
  - Cargo system (use weightKg)
  - Multi-client sync verify (ParrelSync)
  - Drag-and-drop в sublist (P-таб)
  - Иконки секторов (поверхность сектора)
  - Анимация вспышки при pickup

### 11.5 Verification (что ТЫ запускаешь)
См. §6 (выше) + §12 (ниже).

