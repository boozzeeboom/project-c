# Inventory Sub-System — Changes Summary (diff)

**Дата:** 2026-06-05
**Зависит от:** `20_IMPLEMENTATION_PLAN.md`

Этот документ — **полный diff** по каждому изменённому/созданному файлу. Используй для code review.

---

## 1. НОВЫЕ ФАЙЛЫ (12 .cs/.uxml/.uss + 24 .asset)

### 1.1 `Assets/_Project/Items/Dto/InventoryItemDto.cs` (2.5 KB)

**Назначение:** INetworkSerializable struct для одного предмета в инвентаре.

```csharp
using System;
using Unity.Netcode;

namespace ProjectC.Items.Dto
{
    [Serializable]
    public struct InventoryItemDto : INetworkSerializable, IEquatable<InventoryItemDto>
    {
        public int  itemId;
        public byte type;        // (byte)ItemType
        public int  quantity;
        public int  slotIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref slotIndex);
        }
        // ... Equals / GetHashCode
    }
}
```

**Pitfalls отмечены:**
- struct (не class) → `== null` НЕ компилируется (pitfall #14)
- `INetworkSerializable` обязателен для NGO 2.x

---

### 1.2 `Assets/_Project/Items/Dto/InventorySnapshotDto.cs` (3.8 KB)

**Назначение:** полный snapshot инвентаря (locationId + items[] + maxSlots + credits).

```csharp
public struct InventorySnapshotDto : INetworkSerializable
{
    public string locationId;            // nullable
    public InventoryItemDto[] items;     // can be null
    public int   maxSlots;
    public float credits;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // string nullable — manual hasLoc flag
        bool hasLoc = !string.IsNullOrEmpty(locationId);
        serializer.SerializeValue(ref hasLoc);
        if (hasLoc) {
            if (serializer.IsReader) locationId = string.Empty;
            serializer.SerializeValue(ref locationId);
        } else {
            if (serializer.IsReader) locationId = null;
        }

        // T[] — manual length-prefixed
        int len = items != null ? items.Length : 0;
        serializer.SerializeValue(ref len);
        if (serializer.IsReader) items = len > 0 ? new InventoryItemDto[len] : null;
        for (int i = 0; i < len; i++) {
            var x = items[i];
            x.NetworkSerialize(serializer);
            items[i] = x;
        }

        serializer.SerializeValue(ref maxSlots);
        serializer.SerializeValue(ref credits);
    }
}
```

**Pitfalls:**
- string nullable: `hasLoc` flag + manual re-serialise на reader
- T[]: length-prefixed, не null-safe by default
- Двойная вложенность nullable (см. `unity-v2-subsystem-migration` §3.2) — НЕ применима (string — reference)

---

### 1.3 `Assets/_Project/Items/Dto/InventoryResultDto.cs` (3.3 KB)

**Назначение:** результат операции (success/fail + code + message + newCredits).

```csharp
public struct InventoryResultDto : INetworkSerializable
{
    public byte   code;
    public string message;
    public int    itemId;
    public int    slotIndex;
    public float  newCredits;

    public bool IsSuccess => code == (byte)InventoryResultCode.Ok;
    // ... NetworkSerialize аналогично InventorySnapshotDto
}
```

---

### 1.4 `Assets/_Project/Items/Dto/InventoryResultCode.cs` (1.9 KB)

```csharp
public enum InventoryResultCode : byte
{
    Ok                = 0,
    NotInZone         = 1,
    InventoryFull     = 2,
    ItemNotFound      = 3,
    NotEnoughQuantity = 4,
    InvalidSlot       = 5,
    RateLimited       = 6,
    InternalError     = 7,
    NoPermission      = 8,
    ItemNotOwned      = 9,
    StackOverflow     = 10,
}
```

---

### 1.5 `Assets/_Project/Items/Core/InventoryWorld.cs` (13.8 KB)

**Назначение:** POCO singleton, бизнес-логика инвентаря.

Ключевые методы:
- `CreateAndInitialize()` — singleton entry point
- `GetOrRegisterItemId(item)` — для PickupItem + ChestContainer
- `TryPickup(clientId, itemId, type, worldPos, playerPos)` — anti-cheat (dist <= 5m), slot count <= 32
- `TryDrop / TryMove / TryUse` — TODO (возвращают `InternalError`)
- `AddItemDirect(clientId, itemId, type)` — для NetworkChestContainer
- `BuildSnapshot(clientId, locationId)` → DTO
- `RegisterAllItems()` — Resources/Items/ + scene PickupItem

---

### 1.6 `Assets/_Project/Items/Client/InventoryClientState.cs` (10.9 KB)

**Назначение:** singleton-проекция server-state.

API:
- `OnSnapshotUpdated` / `OnInventoryResult` — events
- `RequestPickup/Drop/Move/Use/Refresh` — client → server
- `GetItems / GetItemsByType / GetCountByType / HasItemsInType` — UI helpers
- `GetItemDefinition(itemId)` — для icon/name
- `LocalizeResultCode(code)` — статический, русские строки

---

### 1.7 `Assets/_Project/Items/Network/InventoryServer.cs` (12.4 KB)

**Назначение:** NetworkBehaviour RPC hub.

5 RPC'шек:
```csharp
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestPickupRpc(int itemId, byte typeByte, Vector3 worldPos, RpcParams rpcParams = default)
public void RequestDropRpc(int slotIndex, int quantity, Vector3 worldPos, RpcParams rpcParams = default)
public void RequestMoveRpc(int fromSlot, int toSlot, RpcParams rpcParams = default)
public void RequestUseRpc(int slotIndex, RpcParams rpcParams = default)
public void RequestRefreshRpc(RpcParams rpcParams = default)
```

Delivery (находит NetworkPlayer → TargetRpc):
```csharp
private void SendSnapshot(ulong clientId, string locationId)  // → NetworkPlayer.ReceiveInventorySnapshotTargetRpc(snap)
private void SendResult(ulong clientId, InventoryResultDto result)  // → NetworkPlayer.ReceiveInventoryResultTargetRpc(result)
```

Rate limit: `CheckRateLimit(clientId)` — max 60 ops/min per client.

---

### 1.8 `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs` (11.4 KB)

**Назначение:** `[MenuItem("Tools/Project C/Inventory/Generate Test Dataset")]` создаёт 24 ItemData.

Список `_specs` (24 элемента, struct `ItemSpec { baseName, description, type, maxStack, weightKg }`).

Пример: `Железная руда` (Resources, stack=20, weight=2kg).

---

### 1.9 `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` (5.2 KB)

8 `#sector-0..7` + 8 `#label-0..7` + `.wheel-center` + `#sublist` (ListView) + actions.

Структура: `.wheel-container > .header + .wheel-area (wheel + sublist-panel) + .actions + .message-label`.

---

### 1.10 `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` (8.7 KB)

Ключевые классы:
- `.sector` (базовый 110×110)
- `.sector-N` (0-7) с позициями top/left
- `.sector-empty` / `.sector-has-items` / `.sector-hover` / `.sector-selected` (states)
- `.sublist-row` / `.sublist-row-icon` / `.sublist-row-name` / `.sublist-row-qty`
- Все стили с `!important` (pitfall #24)

---

### 1.11 `Assets/_Project/UI/Client/InventoryUI.cs` (20 KB, полностью переписан)

```csharp
namespace ProjectC.UI.Client
{
    [RequireComponent(typeof(UIDocument))]
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [SerializeField] private VisualTreeAsset inventoryWheelUxml;
        [SerializeField] private StyleSheet       inventoryWheelUss;
        [SerializeField] private bool visibleOnStart = false;

        private UIDocument _doc;
        private VisualElement _root, _wheelContainer, _wheel;
        private List<VisualElement> _sectors = new(8);
        private List<Label> _sectorLabels = new(8);
        private VisualElement _wheelCenter;
        private Label _centerTypeLabel, _centerCountLabel, _sublistTitle, _messageLabel;
        private ListView _sublist;
        private Button _useBtn, _closeBtn;

        private bool _built, _isOpen, _subscribed;
        private int _hoveredSector = -1, _selectedSector = -1, _selectedItemIndex = -1;
        private List<InventoryItemDto> _sublistCache = new();

        private InputAction _toggleAction;  // <Keyboard>/tab

        // Lifecycle
        private void Awake() { /* load UXML/USS, setup InputAction */ }
        private void OnEnable() { /* Enable InputAction, EnsureBuilt, TrySubscribe */ }
        private void OnDisable() { /* Disable, Unsubscribe */ }
        private void OnDestroy() { /* Dispose InputAction */ }
        private void Update() { /* retry subscribe */ }

        // Subscription
        private void TrySubscribeToClientState() { /* OnSnapshotUpdated + OnInventoryResult */ }
        private void UnsubscribeFromClientState() { /* ... */ }

        // Build
        private void EnsureBuilt() { /* Q<>() refs, listview factory, pointer events */ }

        // Snapshot handlers
        private void HandleSnapshotUpdated(InventorySnapshotDto snap) { /* sectors empty/has-items */ }
        private void HandleResultReceived(InventoryResultDto result) { /* message label */ }

        // Sector interaction
        private void OnSectorHover(int idx) { /* sector-hover class */ }
        private void OnSectorHoverEnd(int idx) { /* remove */ }
        private void OnSectorClick(int idx) { /* sector-selected + RefreshSublist */ }
        private void RefreshSublist(ItemType type) { /* sublist.itemsSource = state.GetItemsByType */ }

        // ListView
        private VisualElement MakeSublistRow() { /* row + icon + name + qty */ }
        private void BindSublistRow(VisualElement row, int index) { /* fill from cache */ }
        private static int FindSelectedIndex<T>(...) { /* generic helper */ }

        // Actions
        private void OnUseClicked() { /* TODO: InventoryClientState.Instance?.RequestUse */ }
        private void OnCloseClicked() => SetVisible(false);
        private void SetMessage(string msg, bool isError) { /* color + text */ }

        // Visibility
        public void Toggle() { /* Tab handler */ }
        public bool IsVisible() { ... }
        public void SetVisible(bool v) { /* display + pickingMode + cursor */ }
    }
}
```

---

### 1.12 `Assets/_Project/Resources/Items/Item_*.asset` (24 файла, ~50 KB)

Сгенерированы `ItemDatasetGenerator` через MCP `execute_code`:
- Item_Antigrav_Антиграв-камень_большой
- Item_Antigrav_Антиграв-камень_малый
- Item_Antigrav_Стабилизатор_поля
- Item_Equipment_Верёвка_10м
- Item_Equipment_Карабин
- Item_Equipment_Фонарь
- Item_Food_Бутыль_воды
- Item_Food_Консервы
- Item_Food_Сухпаёк
- Item_Fuel_Антигравитационное_топливо
- Item_Fuel_Газовый_баллон
- Item_Fuel_Угольные_брикеты
- Item_Medical_Антисептик
- Item_Medical_Бинт
- Item_Medical_Стимулятор
- Item_Meziy_Мезий-кристалл
- Item_Meziy_Мезий-крошка
- Item_Meziy_Мезий-сердцевина
- Item_Resources_Железная_руда
- Item_Resources_Кристаллическая_пыль
- Item_Resources_Медная_руда
- Item_Tech_Батарея
- Item_Tech_Кабель
- Item_Tech_Микросхема

---

## 2. ПАТЧИ (6 файлов)

### 2.1 `Assets/_Project/Scripts/Core/NetworkManagerController.cs`

**Diff:** +30 строк (1 вызов + 1 метод)

```csharp
// В Awake (после CreateContractClientState):
+ // Phase 1 (INVENTORY_V2_REFACTOR.md): InventoryClientState тоже auto-spawn.
+ // Создаётся root GO сразу, чтобы AddComponent→Awake→DontDestroyOnLoad отработали
+ // (см. FIX 2026-06-04 — на child DontDestroyOnLoad падает, singleton теряется).
+ CreateInventoryClientState();

// Новый метод (рядом с CreateContractClientState):
+ private void CreateInventoryClientState()
+ {
+     var existing = FindObjectsByType<ProjectC.Items.Client.InventoryClientState>(FindObjectsInactive.Include);
+     foreach (var inst in existing)
+     {
+         if (inst != null && inst.transform.parent == null)
+         {
+             Debug.Log("[NMC] InventoryClientState already root, skipping creation");
+             return;
+         }
+     }
+     if (existing.Length > 0)
+     {
+         Debug.LogWarning($"[NMC] Found {existing.Length} non-root InventoryClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
+     }
+     var go = new GameObject("[InventoryClientState]");
+     go.AddComponent<ProjectC.Items.Client.InventoryClientState>();
+     Debug.Log("[NMC] Created [InventoryClientState] as root GameObject");
+ }
```

---

### 2.2 `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

**Diff:** +30 / -10 строк

**2.2.1 Добавлены 2 TargetRpc (в конце файла, рядом с Contract RPCs):**

```csharp
+ // ==================== INVENTORY V2 RPC TARGETS ====================
+ [Rpc(SendTo.Owner)]
+ public void ReceiveInventorySnapshotTargetRpc(ProjectC.Items.Dto.InventorySnapshotDto snapshot, RpcParams rpcParams = default)
+ {
+     ProjectC.Items.Client.InventoryClientState.Instance?.OnSnapshotReceived(snapshot);
+ }
+
+ [Rpc(SendTo.Owner)]
+ public void ReceiveInventoryResultTargetRpc(ProjectC.Items.Dto.InventoryResultDto result, RpcParams rpcParams = default)
+ {
+     ProjectC.Items.Client.InventoryClientState.Instance?.OnResultReceived(result);
+ }
```

**2.2.2 `SpawnInventory()` — no-op:**

```csharp
- private void SpawnInventory()
- {
-     var invObj = new GameObject("Inventory");
-     invObj.transform.SetParent(transform);
-     _inventory = invObj.AddComponent<Inventory>();
-
-     var uiObj = new GameObject("InventoryUI");
-     _inventoryUI = uiObj.AddComponent<InventoryUI>();
-     _inventoryUI.SetInventory(_inventory);
- }

+ private void SpawnInventory()
+ {
+     // Phase 4 (INVENTORY_V2_REFACTOR.md): TAB-колесо теперь — UI Toolkit версия,
+     // создаётся как сцен-placed [InventoryWheel] GameObject (см. BootstrapScene setup).
+     // Оно само подписывается на InputAction "<Keyboard>/tab" в Awake и слушает
+     // InventoryClientState. Старый IMGUI InventoryUI (ProjectC.Items.InventoryUI)
+     // НЕ инстанцируем здесь — файл остаётся жить для совместимости (cleanup в Phase 8).
+     _inventory = null;
+     _inventoryUI = null;
+ }
```

> Все `if (_inventory != null)` блоки в NetworkPlayer **останутся в коде** (NRE-safe), но никогда не сработают (т.к. `_inventory = null` после `SpawnInventory`).

---

### 2.3 `Assets/_Project/Scripts/Core/PickupItem.cs` (полностью переписан)

**БЫЛО (100 строк):**
- `Collect()` — просто `gameObject.SetActive(false)`, **предмет терялся**
- Никакой связи с NetworkInventory / InventoryClientState

**СТАЛО (8.8 KB):**
- `Collect()` — `RequestPickup` через `InventoryClientState`
- `HandlePickupResult(InventoryResultDto)` — обработка server confirmation
- `ForceCollect()` — legacy fallback (если нет InventoryClientState)
- Подписка `OnInventoryResult` (одноразовая)

Ключевые изменения:
```csharp
public void Collect()
{
    if (_isCollected || itemData == null || _isAwaitingServer) return;
    int itemId = ProjectC.Items.InventoryWorld.Instance?.GetOrRegisterItemId(itemData) ?? -1;
    if (itemId < 0) { Debug.LogWarning(...); return; }

    _isAwaitingServer = true;
    var clientState = ProjectC.Items.Client.InventoryClientState.Instance;
    if (clientState != null)
    {
        clientState.RequestPickup(itemId, itemData.itemType, transform.position);
        clientState.OnInventoryResult += HandlePickupResult;
    }
    else
    {
        ForceCollect();  // legacy fallback
    }
}

private void HandlePickupResult(InventoryResultDto result)
{
    var clientState = ProjectC.Items.Client.InventoryClientState.Instance;
    if (clientState == null) return;
    clientState.OnInventoryResult -= HandlePickupResult;
    _isAwaitingServer = false;

    if (result.IsSuccess)
    {
        _isCollected = true;
        gameObject.SetActive(false);
        Core.InteractableManager.UnregisterPickup(this);
    }
    // else: НЕ деактивируем, можно попробовать ещё раз
}
```

---

### 2.4 `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`

**Diff:** +90 строк

**2.4.1 `using` directives:**

```csharp
+ using ProjectC.Items.Client;
+ using ProjectC.Items.Dto;
```

**2.4.2 `OnDisable` — добавить отписку:**

```csharp
private void OnDisable()
{
    if (_contractState != null) { ... }

+   // Phase 5 (INVENTORY_V2_REFACTOR.md): отписка от InventoryClientState
+   var invState = ProjectC.Items.Client.InventoryClientState.Instance;
+   if (invState != null)
+   {
+       invState.OnSnapshotUpdated -= HandleInventorySnapshotUpdated;
+       invState.OnInventoryResult -= HandleInventoryResultReceived;
+   }
}
```

**2.4.3 `EnsureBuilt` — добавить подписку:**

```csharp
+ // ---- Phase 5: Subscribe to InventoryClientState ----
+ var invState = ProjectC.Items.Client.InventoryClientState.Instance;
+ if (invState == null) { Debug.LogWarning(...); }
+ else
+ {
+     invState.OnSnapshotUpdated += HandleInventorySnapshotUpdated;
+     invState.OnInventoryResult += HandleInventoryResultReceived;
+     invState.RequestRefresh();
+ }
```

**2.4.4 `RefreshInventoryCache` — полностью переписан:**

```csharp
- // БЫЛО: GetComponentInChildren<Inventory>() + reflection
- Inventory inv = _localPlayer.GetComponentInChildren<Inventory>(true);
- if (inv == null) { /* reflection fallback */ }
- foreach (var t in inv.GetNonEmptyTypes()) { ... }

+ // СТАЛО: читать из InventoryClientState.CurrentSnapshot
+ var invState = ProjectC.Items.Client.InventoryClientState.Instance;
+ if (invState == null || !invState.CurrentSnapshot.HasValue) return;
+ var snap = invState.CurrentSnapshot.Value;
+ var items = snap.items ?? Array.Empty<InventoryItemDto>();
+
+ var groups = new Dictionary<int, (int totalQty, InventoryItemDto first)>();
+ foreach (var dto in items)
+ {
+     // pitfall #14: dto — struct, == null не компилируется
+     if (dto.itemId <= 0) continue;
+     if (groups.TryGetValue(dto.itemId, out var existing))
+         groups[dto.itemId] = (existing.totalQty + dto.quantity, existing.first);
+     else
+         groups[dto.itemId] = (dto.quantity, dto);
+ }
+
+ foreach (var kvp in groups)
+ {
+     var first = kvp.Value.first;
+     ItemData def = invState.GetItemDefinition(first.itemId);
+     _inventoryCache.Add(new InventoryListItem
+     {
+         itemId      = first.itemId.ToString(),
+         displayName = def != null ? def.itemName : $"Item#{first.itemId}",
+         type        = (ItemType)first.type,
+         quantity    = kvp.Value.totalQty,
+         icon        = def != null ? def.icon : null,
+     });
+ }
```

**2.4.5 Два новых handler'а:**

```csharp
private void HandleInventorySnapshotUpdated(InventorySnapshotDto snap)
{
    if (_creditsLabel != null) _creditsLabel.text = $"Кредиты: {snap.credits:F0} CR";
    if (_statCredits != null) _statCredits.text = $"{snap.credits:F0} CR";
    if (_activeTab == "inventory")
    {
        RefreshInventoryCache();
        ApplyInventoryFilters();
    }
}

private void HandleInventoryResultReceived(InventoryResultDto result)
{
    if (_messageLabel == null || !IsVisible()) return;
    string msg = !string.IsNullOrEmpty(result.message)
        ? result.message
        : ProjectC.Items.Client.InventoryClientState.LocalizeResultCode(
            (ProjectC.Items.Dto.InventoryResultCode)result.code);
    _messageLabel.text = msg;
    _messageLabel.style.color = result.IsSuccess
        ? new StyleColor(new Color(0.4f, 0.95f, 0.4f))
        : new StyleColor(new Color(0.95f, 0.4f, 0.4f));
}
```

---

### 2.5 `Assets/_Project/Scripts/Core/ItemType.cs`

**Diff:** +10 строк

```csharp
public class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    [TextArea] public string description;
    public Sprite icon;

+   // Phase 6 (INVENTORY_V2_REFACTOR.md): доп-поля для stack + weight.
+   [Header("Stack & Weight (Phase 6)")]
+   [Tooltip("Сколько таких предметов может быть в одном слоте. 1 = non-stackable.")]
+   public int   maxStack = 1;
+   [Tooltip("Вес одного предмета (кг). Используется в будущей cargo-системе.")]
+   public float weightKg = 0.1f;
}
```

> Обратная совместимость: `maxStack=1`, `weightKg=0.1` — старые `Item_Type1..8` .asset'ы **не сломаются**.

---

### 2.6 `Assets/_Project/Scenes/BootstrapScene.unity`

**Изменения через MCP:**

**A. Создан `[InventoryWheel]` GameObject:**
```yaml
[InventoryWheel]  (root GO, без transform)
  ├── Transform
  ├── UIDocument
  │     panelSettings: MarketPanelSettings.asset
  │     sourceAsset:    InventoryWheel.uxml
  └── InventoryUI
        inventoryWheelUxml: null (fallback to Resources.Load)
        inventoryWheelUss:  null (fallback to Resources.Load)
        visibleOnStart: false
```

**B. Создан `[InventoryServer]` GameObject:**
```yaml
[InventoryServer]  (root GO, без transform)
  ├── Transform
  ├── NetworkObject  (scene-placed, GlobalObjectIdHash = 0 → требует ScenePlacedObjectSpawner)
  └── InventoryServer
        maxSlots: 32
        maxOpsPerMinute: 60
```

> ⚠️ **ВАЖНО:** `ScenePlacedObjectSpawner` в BootstrapScene **должен** спавнить `[InventoryServer]` (как спавнит `[ContractServer]` и `[MarketServer]`). Проверь в Console: `ScenePlacedObjectSpawner] Scene (0,0): spawned=N` — N должен включать InventoryServer.

---

## 3. ДОКУМЕНТАЦИЯ (8 файлов)

### 3.1 Создано в `docs/Character-menu/sub_inventory-tab/`
1. `00_OVERVIEW.md` (10.8 KB) — обзор
2. `10_DESIGN.md` (13.7 KB) — UXML/USS/classes
3. `20_IMPLEMENTATION_PLAN.md` (20.9 KB) — пошаговый план
4. `30_VERIFICATION.md` (9.6 KB) — manual smoke checklist
5. `40_CHANGES_SUMMARY.md` (этот файл)
6. `50_TESTING_GUIDE.md` (см. ниже)
7. `60_KNOWN_ISSUES.md` (см. ниже)

### 3.2 Создано в `docs/dev/`
- `INVENTORY_V2_REFACTOR.md` (37 KB) — главный дизайн-док

### 3.3 Копия в `sub_inventory-tab/`
- `INVENTORY_V2_REFACTOR.md` (копия `docs/dev/`)

---

## 4. ИТОГО (статистика)

| Метрика | Значение |
|---|---|
| Новых .cs файлов | 8 (DTO×4, World, ClientState, Server, Editor) |
| Новых .uxml | 1 (InventoryWheel) |
| Новых .uss | 1 (InventoryWheel) |
| Изменённых .cs | 5 (NetworkManagerController, NetworkPlayer, PickupItem, CharacterWindow, ItemType) |
| Новых .asset | 24 (тестовый датасет) |
| Изменённых .unity | 1 (BootstrapScene) |
| Документов | 8 (sub_inventory-tab) + 1 (docs/dev) |
| Общий объём нового кода | ~75 KB (~2100 строк) |
| Compile errors | 0 |
| Compile warnings (моих) | 0 |
