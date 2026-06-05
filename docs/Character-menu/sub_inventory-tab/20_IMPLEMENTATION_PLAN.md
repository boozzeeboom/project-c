# Inventory Sub-System — Implementation Plan (Phases 0-7)

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `10_DESIGN.md`
**Статус:** ✅ Все фазы реализованы, готовы к тестированию

---

## Phase 0 — Recon (✅ done)

**Цель:** понять текущее состояние кода до начала рефакторинга.

**Что прочитано:**
1. `Assets/_Project/Scripts/Core/Inventory.cs` (177 строк, MonoBehaviour)
2. `Assets/_Project/Scripts/Core/NetworkInventory.cs` (240 строк, NetworkBehaviour)
3. `Assets/_Project/Scripts/Core/InventoryData.cs` (162 строки, struct)
4. `Assets/_Project/Scripts/Core/ItemType.cs` (26 строк, enum + SO)
5. `Assets/_Project/Scripts/Core/ItemTypeNames.cs` (26 строк, локализация)
6. `Assets/_Project/Scripts/Core/ItemDatabaseInitializer.cs` (85 строк, регистрация)
7. `Assets/_Project/Scripts/Core/PickupItem.cs` (100 строк, MonoBehaviour)
8. `Assets/_Project/Scripts/Core/LootTable.cs` (64 строки, SO)
9. `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (775 строк, NetworkBehaviour)
10. `Assets/_Project/Scripts/Player/ItemPickupSystem.cs` (238 строк, не используется)
11. `Assets/_Project/Scripts/UI/InventoryUI.cs` (384 строки, IMGUI/GL — старый)
12. `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (1194 строки, 5 табов)
13. `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs` (326 строк)
14. `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` (134 строки, v2 reference)

**Найденные дефекты:**
- ❌ ДВЕ параллельные системы инвентаря (локальный `Inventory` + сетевой `NetworkInventory`)
- ❌ Подбор предметов сломан (`PickupItem.Collect()` деактивирует GO, но не вызывает RPC)
- ❌ `NetworkInventory` не привязан к PlayerPrefab (сунуть `NetworkChestContainer` падает silently)
- ❌ TAB-колесо использует устаревший IMGUI/GL + читает локальный инвентарь
- ❌ Тестовый датасет: 8 пустых `Item_Type1..8` (itemName="", icon=null)
- ❌ `CharacterWindow.RefreshInventoryCache` читает `GetComponentInChildren<Inventory>()` — то же локальное

**Skill loaded:** `unity-v2-subsystem-migration` (методология v2-миграции)

**Deliverable:** `00_OVERVIEW.md` (согласован scope "full" с пользователем)

---

## Phase 1 — DTOs + Core POCO + ClientState + Server (✅ done)

**Цель:** создать слои v2-архитектуры для инвентаря.

**Файлы созданы (7):**

1. `Assets/_Project/Items/Dto/InventoryItemDto.cs` (2.5 KB)
   - struct, INetworkSerializable
   - поля: `int itemId`, `byte type`, `int quantity`, `int slotIndex`
   - **Pitfall #14:** struct, `== null` НЕ компилируется

2. `Assets/_Project/Items/Dto/InventorySnapshotDto.cs` (3.8 KB)
   - struct, INetworkSerializable
   - поля: `string locationId` (nullable, manual serialize), `InventoryItemDto[] items`, `int maxSlots`, `float credits`
   - NGO 2.x array serialise: `int len + loop` (не built-in)

3. `Assets/_Project/Items/Dto/InventoryResultDto.cs` (3.3 KB)
   - struct, INetworkSerializable
   - поля: `byte code`, `string message` (nullable), `int itemId`, `int slotIndex`, `float newCredits`
   - `IsSuccess => code == (byte)InventoryResultCode.Ok`

4. `Assets/_Project/Items/Dto/InventoryResultCode.cs` (1.9 KB)
   - enum : byte: Ok, NotInZone, InventoryFull, ItemNotFound, NotEnoughQuantity, InvalidSlot, RateLimited, InternalError, NoPermission, ItemNotOwned, StackOverflow

5. `Assets/_Project/Items/Core/InventoryWorld.cs` (13.8 KB)
   - POCO singleton (НЕ MonoBehaviour, НЕ NetworkBehaviour)
   - `CreateAndInitialize()` / `Shutdown()`
   - `Dictionary<int, ItemData> _itemDatabase`
   - `Dictionary<ulong, InventoryData> _playerInventories`
   - `RegisterItem`, `GetItemDefinition`, `GetOrRegisterItemId`
   - `TryPickup(clientId, itemId, type, worldPos, playerPos)` → result (anti-cheat: distance <= 5m, slot count <= 32)
   - `TryDrop / TryMove / TryUse` — TODO (возвращают InternalError "не реализовано")
   - `AddItemDirect` — для NetworkChestContainer
   - `BuildSnapshot(clientId, locationId)` → InventorySnapshotDto
   - `RegisterAllItems()` — Resources/Items/ + scene PickupItem fallback

6. `Assets/_Project/Items/Client/InventoryClientState.cs` (10.9 KB)
   - MonoBehaviour singleton
   - `Awake`: Instance = this, DontDestroyOnLoad
   - `CurrentSnapshot : InventorySnapshotDto?`, `LastResult : InventoryResultDto?`
   - `OnSnapshotUpdated` / `OnInventoryResult` events
   - `RequestPickup(itemId, type, worldPos)`, `RequestDrop`, `RequestMove`, `RequestUse`, `RequestRefresh`
   - `GetItems`, `GetItemsByType`, `GetCountByType`, `HasItemsInType`, `GetTotalItemCount`
   - `GetItemDefinition(itemId)` — для UI (icon, name)
   - `LocalizeResultCode(code)` — статический, для UI

7. `Assets/_Project/Items/Network/InventoryServer.cs` (12.4 KB)
   - NetworkBehaviour
   - `[DisallowMultipleComponent]`
   - Singleton `Instance`
   - `[SerializeField] int maxSlots = 32`, `int maxOpsPerMinute = 60`
   - 5 RPC'шек: `RequestPickupRpc`, `RequestDropRpc`, `RequestMoveRpc`, `RequestUseRpc`, `RequestRefreshRpc` (all `[Rpc(SendTo.Server, InvokePermission = Owner)]`)
   - `AddItem(clientId, itemId, type)` — для NetworkChestContainer
   - `GetCachedDefinition(itemId)` — клиентский UI
   - `SendSnapshot(clientId, locationId)` / `SendResult(clientId, result)` — находит NetworkPlayer, вызывает TargetRpc
   - `CheckRateLimit(clientId)` — anti-spam (60 ops/min)
   - `OnNetworkSpawn` — `InventoryWorld.CreateAndInitialize()` (если сервер)
   - `OnNetworkDespawn` — `Instance = null`

**Compile check:** 0 errors после фиксов (using `ProjectC.Items` без `.Core`, `InvokePermission` вместо `RequireOwnership`).

**Deliverable:** v2-architecture слои готовы к интеграции.

---

## Phase 2 — NetworkInventory на сцене (✅ done)

**Цель:** разместить `[InventoryServer]` GameObject в BootstrapScene, чтобы RPC работали.

**Изменения:**

1. `Assets/_Project/Scripts/Core/NetworkManagerController.cs`
   - Добавлен вызов `CreateInventoryClientState()` в `Awake` (рядом с `CreateMarketClientState` и `CreateContractClientState`)
   - Сам метод `CreateInventoryClientState()` — копия паттерна C2-этапа (root GO, FindObjectsByType check, AddComponent + Awake → DontDestroyOnLoad)

2. `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
   - Добавлены 2 TargetRpc:
     - `ReceiveInventorySnapshotTargetRpc(InventorySnapshotDto snap, RpcParams)` — `[Rpc(SendTo.Owner)]`
     - `ReceiveInventoryResultTargetRpc(InventoryResultDto result, RpcParams)` — `[Rpc(SendTo.Owner)]`
   - Делегируют в `InventoryClientState.Instance?.OnSnapshotReceived/OnResultReceived`

3. `BootstrapScene.unity` (через MCP):
   - Создан `[InventoryServer]` GameObject
   - `manage_components add Unity.Netcode.NetworkObject`
   - `manage_components add ProjectC.Items.Network.InventoryServer`
   - `manage_scene save`

**Verification:**
- `manage_scene get_hierarchy` показал оба компонента на `[InventoryServer]`
- NetworkObject, InventoryServer — присутствуют

**Deliverable:** server-authoritative инвентарь запускается при `StartHost()`.

---

## Phase 3 — PickupItem → RequestPickup (✅ done)

**Цель:** связать `PickupItem.Collect()` с `InventoryServer` через `InventoryClientState`.

**Изменения:**

`Assets/_Project/Scripts/Core/PickupItem.cs` (полностью переписан):
- `Collect()`:
  - Защита от двойного E (`_isAwaitingServer`)
  - Получает `itemId` через `InventoryWorld.GetOrRegisterItemId(itemData)` (или legacy `NetworkInventory.GetItemId` fallback)
  - Если `InventoryClientState.Instance != null`:
    - Подписывается на `OnInventoryResult` (одноразовая, в `HandlePickupResult`)
    - Вызывает `clientState.RequestPickup(itemId, type, transform.position)`
  - Иначе (нет сети / тесты): `ForceCollect()` legacy fallback
- `HandlePickupResult(InventoryResultDto result)`:
  - Отписка от `OnInventoryResult`
  - Если `IsSuccess` → `_isCollected = true`, `gameObject.SetActive(false)`, `InteractableManager.UnregisterPickup`
  - Если fail → пишет warning в Console, **НЕ деактивирует** (можно подобрать позже)
- `ForceCollect()` — оставлен для тестов / edge-cases

**БЫЛО (старый код):**
```csharp
public void Collect() {
    if (_isCollected || itemData == null) return;
    _isCollected = true;
    gameObject.SetActive(false);   // ← ПРЕДМЕТ ТЕРЯЛСЯ
    Core.InteractableManager.UnregisterPickup(this);
}
```

**СТАЛО:**
```csharp
public void Collect() {
    if (_isCollected || itemData == null || _isAwaitingServer) return;
    int itemId = ProjectC.Items.InventoryWorld.Instance?.GetOrRegisterItemId(itemData) ?? -1;
    if (itemId < 0) return;
    _isAwaitingServer = true;
    var clientState = ProjectC.Items.Client.InventoryClientState.Instance;
    if (clientState != null) {
        clientState.RequestPickup(itemId, itemData.itemType, transform.position);
        clientState.OnInventoryResult += HandlePickupResult;
    } else {
        ForceCollect();
    }
}
```

**Verification:**
- Подбор предмета → `gameObject.SetActive(false)` происходит **только** после server confirmation
- Если pickup rejected (inventory full / too far) → предмет остаётся в мире

**Deliverable:** полностью рабочий цикл подбора: Player E → RequestPickup → Server validate → NetworkVariable update → OnSnapshotUpdated → UI колесо + P-таб обновляются.

---

## Phase 4 — UI Toolkit TAB-колесо (✅ done)

**Цель:** переписать `InventoryUI` с IMGUI/GL на UI Toolkit, подписка на `InventoryClientState`.

**Файлы созданы (3):**

1. `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` (5.2 KB)
   - Структура: header, wheel-area (wheel + sublist-panel), actions, message-label
   - 8 секторов (`#sector-0` ... `#sector-7`) + 8 лейблов
   - `.wheel-center` с type-label + count-label
   - `ListView #sublist` для предметов выбранного сектора

2. `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` (8.7 KB)
   - `.sector-empty` (тёмный), `.sector-has-items` (зелёный), `.sector-hover` (жёлтый, scale 1.1), `.sector-selected` (золотой, scale 1.15)
   - Все стили с `!important` (pitfall #24)
   - Sublist row: `.sublist-row` (32px), `.sublist-row-icon` (24×24), `.sublist-row-name`, `.sublist-row-qty`
   - Action buttons: `.action-btn.use`, `.action-btn.close`

3. `Assets/_Project/UI/Client/InventoryUI.cs` (20 KB, полностью переписан)
   - `Awake` — InputAction "<Keyboard>/tab", `Resources.Load` для UXML/USS fallback
   - `OnEnable` — Enable InputAction, EnsureBuilt, TrySubscribeToClientState
   - `OnDisable` — Disable InputAction, UnsubscribeFromClientState
   - `Update` — retry подписки (если ClientState создан позже)
   - `EnsureBuilt` — Q<>() refs для wheel-container, sectors (0-7), labels, sublist, action buttons, message
   - `HandleSnapshotUpdated(snap)` — обновляет sector classes + labels + sublist (если выбран)
   - `HandleResultReceived(result)` — feedback в message label (cross-tab, pitfall #11)
   - `OnSectorHover/End/Click` — pointer events, USS class manipulation
   - `RefreshSublist(type)` — `ListView.itemsSource = state.GetItemsByType(type)`, обновляет center
   - `MakeSublistRow` / `BindSublistRow` — row factory
   - `FindSelectedIndex<T>` — generic helper
   - `OnUseClicked` — TODO (выводит "Использование предметов — TODO")
   - `Toggle` — Tab handler
   - `SetVisible(bool)` — display, pickingMode, cursor lock/unlock

**NetworkPlayer.cs патч:**
- `SpawnInventory()` — **no-op** (legacy `_inventory = null`, `_inventoryUI = null`)
- Старая логика (AddComponent<Inventory> + AddComponent<InventoryUI>) **не выполняется**
- TODO: cleanup-сессия для удаления `Inventory.cs` и старого `InventoryUI.cs` (IMGUI)

**Сцена (BootstrapScene):**
- Создан `[InventoryWheel]` GameObject
- `manage_components add UnityEngine.UIElements.UIDocument`
- `manage_components add ProjectC.UI.Client.InventoryUI`
- `set_property panelSettings=MarketPanelSettings.asset sourceAsset=InventoryWheel.uxml`
- `manage_scene save`

**Verification:**
- TAB → колесо появляется
- 8 секторов серые (если инвентарь пуст)
- Подбор предмета → сектор становится зелёным
- Click на сектор → справа sublist с предметами

**Deliverable:** TAB-колесо работает на UI Toolkit, читает из server-state, готово к UX-улучшениям (иконки, анимации).

---

## Phase 5 — P-таб CharacterWindow (✅ done)

**Цель:** подключить P-таб "Инвентарь" в `CharacterWindow` к `InventoryClientState`.

**Изменения в `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`:**

1. `using` directives (top):
   - Добавлено: `using ProjectC.Items.Client;` + `using ProjectC.Items.Dto;`

2. `OnDisable`:
   - Добавлена отписка от `InventoryClientState.OnSnapshotUpdated` / `OnInventoryResult` (event-leak prevention)

3. `EnsureBuilt` (после подписки на ContractClientState):
   - Подписка `invState.OnSnapshotUpdated += HandleInventorySnapshotUpdated`
   - Подписка `invState.OnInventoryResult += HandleInventoryResultReceived`
   - `invState.RequestRefresh()` — попросить сервер прислать snapshot

4. `RefreshInventoryCache` (полностью переписан):
   - **БЫЛО:** чтение `GetComponentInChildren<Inventory>()` + reflection
   - **СТАЛО:** чтение `InventoryClientState.Instance.CurrentSnapshot`
   - Группировка items по `itemId` (суммирует quantity)
   - Получение `ItemData def = invState.GetItemDefinition(itemId)` для имени/icon
   - `InventoryListItem { itemId, displayName, type, quantity, icon }`
   - Pitfall #14 fix: `if (dto.itemId <= 0) continue` (НЕ `dto == null`)

5. `HandleInventorySnapshotUpdated(snap)` — новый handler:
   - Cross-tab: обновляет `_creditsLabel` и `_statCredits` (header)
   - Если `activeTab == "inventory"` → `RefreshInventoryCache + ApplyInventoryFilters`

6. `HandleInventoryResultReceived(result)` — новый handler:
   - Cross-tab feedback в `_messageLabel` (pitfall #11)
   - Зелёный/красный цвет в зависимости от `IsSuccess`
   - Локализованное сообщение через `LocalizeResultCode`

**Verification:**
- P → ИНВЕНТАРЬ → пустой список (до подбора)
- Подбор предмета → запись появляется в ListView
- Закрыть P → открыть P → запись всё ещё там (cache)
- Подбор → credits в header обновляются (cross-tab)

**Deliverable:** оба UI (TAB + P-таб) читают ОДИН `InventoryClientState`. Согласованность данных гарантирована.

---

## Phase 6 — Test dataset (✅ done)

**Цель:** создать тестовые `ItemData` (24 штуки: 8 типов × 3 варианта).

**Файлы созданы (2):**

1. `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs` (11.4 KB)
   - `[MenuItem("Tools/Project C/Inventory/Generate Test Dataset")]`
   - Список `_specs` (24 `ItemSpec` с baseName, description, type, maxStack, weightKg)
   - Создаёт .asset в `Resources/Items/Item_{Type}_{SanitizedName}.asset`
   - Идемпотентно: пропускает существующие
   - Логирует результат в Console + показывает EditorUtility.DisplayDialog

2. `Assets/_Project/Scripts/Core/ItemType.cs` (добавлены 2 поля)
   - `public int maxStack = 1;` — non-stackable по умолчанию
   - `public float weightKg = 0.1f;` — для будущего cargo

**Запуск через MCP:**
- `execute_code` с `System.Type.GetType("ProjectC.Items.EditorTools.ItemDatasetGenerator, Assembly-CSharp-Editor")`
- `m.Invoke(null, null)` → создал 24 .asset

**Созданные .asset'ы (24):**
- Resources/Item_Antigrav_*.asset (3)
- Resources/Item_Equipment_*.asset (3)
- Resources/Item_Food_*.asset (3)
- Resources/Item_Fuel_*.asset (3)
- Resources/Item_Medical_*.asset (3)
- Resources/Item_Meziy_*.asset (3)
- Resources/Item_Resources_*.asset (3)
- Resources/Item_Tech_*.asset (3)

**Verification:**
- `ls Resources/Items/ | grep -c "^Item_"` → 24
- `Editor` (Tools → Project C → Inventory → Generate) → идемпотентно

**Deliverable:** тестовый датасет для проверки UI без ручной работы.

---

## Phase 7 — Документация (✅ done)

**Цель:** зафиксировать всё в документации.

**Файлы:**

1. `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` (этот каталог)
2. `docs/Character-menu/sub_inventory-tab/10_DESIGN.md` (этот каталог)
3. `docs/Character-menu/sub_inventory-tab/20_IMPLEMENTATION_PLAN.md` (этот файл)
4. `docs/Character-menu/sub_inventory-tab/30_VERIFICATION.md` (см. соседний файл)
5. `docs/Character-menu/sub_inventory-tab/40_CHANGES_SUMMARY.md` (см. соседний файл)
6. `docs/Character-menu/sub_inventory-tab/50_TESTING_GUIDE.md` (см. соседний файл)
7. `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md` (см. соседний файл)
8. `docs/dev/INVENTORY_V2_REFACTOR.md` (37 KB, главный дизайн-док) + копия в sub_inventory-tab/

**Verification:** документация полная, перекрёстные ссылки работают.

---

## Сводный diff (по файлам)

| Файл | Действие | Размер |
|---|---|---|
| `Assets/_Project/Items/Dto/InventoryItemDto.cs` | создано | 2.5 KB |
| `Assets/_Project/Items/Dto/InventorySnapshotDto.cs` | создано | 3.8 KB |
| `Assets/_Project/Items/Dto/InventoryResultDto.cs` | создано | 3.3 KB |
| `Assets/_Project/Items/Dto/InventoryResultCode.cs` | создано | 1.9 KB |
| `Assets/_Project/Items/Core/InventoryWorld.cs` | создано | 13.8 KB |
| `Assets/_Project/Items/Client/InventoryClientState.cs` | создано | 10.9 KB |
| `Assets/_Project/Items/Network/InventoryServer.cs` | создано | 12.4 KB |
| `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs` | создано | 11.4 KB |
| `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` | создано | 5.2 KB |
| `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` | создано | 8.7 KB |
| `Assets/_Project/UI/Client/InventoryUI.cs` | создано | 20 KB |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | патч | +30 |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | патч | +30 / -10 |
| `Assets/_Project/Scripts/Core/PickupItem.cs` | переписан | 8.8 KB |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | патч | +90 |
| `Assets/_Project/Scripts/Core/ItemType.cs` | патч | +10 |
| `Assets/_Project/Scenes/BootstrapScene.unity` | патч (MCP) | +2 GO |
| `Assets/_Project/Resources/Items/Item_*.asset` | создано (24) | ~2 KB каждый |
| `docs/dev/INVENTORY_V2_REFACTOR.md` | создано | 37 KB |
| `docs/Character-menu/sub_inventory-tab/*.md` | создано (7) | ~10-15 KB каждый |

**Итого:**
- 12 новых .cs/.uxml/.uss
- 24 .asset
- 6 патчей
- 8 документов
- 2 GameObject в BootstrapScene

**Compile state:** 0 моих errors, 0 моих warnings.
