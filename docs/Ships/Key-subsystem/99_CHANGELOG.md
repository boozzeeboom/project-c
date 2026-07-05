# Ship Key Subsystem — Changelog

Журнал изменений документации подсистемы Key.

---

## 2026-07-21 — P1 Refactor Complete (ветка `refactor/key-subsystem-p1-2026-07-21`)

**Контекст**: полный рефакторинг по плану `SHIP_REFACTOR_PLAN_2026-07-21.md`.

**Что изменилось в коде**:

| Шаг | Коммит | Что |
|-----|--------|-----|
| 1 | `d04c5e8` | Удалены 4 Obsolete-файла: ShipKeyBinding, ShipKeyServer, ShipKeyClientState, ShipKeyToast. Убран CreateShipKeyClientState из NetworkManagerController. Убраны ReceiveShipKey*TargetRpc из NetworkPlayer. |
| 2 | `f97bdcf` | Fix registeredShipId=0: поиск shipId из существующих instance-ов при fallback CreateInstance. |
| 3 | `37f25a2` | Удалён ShipOwnershipRegistry (200+ строк, дублировал KeyRodInstanceWorld). ShipTelemetryClientState читает ownerClientId из ShipTelemetryState напрямую. |
| 4 | `6742a84` | Удалён KeyRodInstanceBinding (retry-loop). ShipController создаёт KeyRodInstance через корутину CreateKeyInstanceWhenReady. TryPickup ищет Active+NONE instance. |
| fix | `01a4d13` | Guard от дубликата ключа при pickup. Корутина с таймаутом 5с для CreateKeyInstance. |

**Итог**:
- Удалено 7 файлов (~850 строк)
- 0 reflection
- 1 source of truth: KeyRodInstanceWorld
- ShipController._keyItemData — inspector field для привязки ключ→корабль

**Документация обновлена**:
- `00_OVERVIEW.md` — переписан §2-§4 (архитектура, wire-протокол, идентификация)
- `99_CHANGELOG.md` — эта запись
- `31_KEY_ANALYSIS_2026-07-21.md` — анализ перед рефакторингом
- `SHIP_REFACTOR_PLAN_2026-07-21.md` — P1 отмечен как complete

---

## 2026-06-18 — R2-SHIP-KEY-003 v10 (T-KEY-07: ShipTelemetry — NetworkVariable-based)

**Контекст**: восьмой тикет R2-SHIP-KEY-003 после T-KEY-06. Самый большой тикет — реализация ship telemetry через NetworkVariable. HUD/UI получают актуальные данные без polling RPC.

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` | NEW. INetworkSerializable struct с 14 полями (shipNetId, keyInstanceId, displayName, className, position, rotation, fuel, cargo, moduleCount, state, ownerClientId, lastUpdate). IEquatable + GetHashCode + operator==/!=. | ✅ |
| `Assets/_Project/Scripts/Ship/Network/ShipOwnershipRegistry.cs` | NEW. NetworkBehaviour, NetworkList<OwnershipEntry> (shipNetId ↔ ownerClientId). `SetOwner/RemoveOwner` API. Подписка на `KeyRodInstanceWorld.OnOwnershipChanged` для автоматической синхронизации. `OnOwnershipListChanged` event для клиентов. | ✅ |
| `Assets/_Project/Scripts/Ship/Client/ShipTelemetryClientState.cs` | NEW. Singleton (MonoBehaviour, не NetworkBehaviour). Агрегирует `ShipTelemetryState` со всех ShipController + ownership cache. `SubscribeToShip/UnsubscribeFromShip/SubscribeToRegistry`. `MyShips` (LINQ-style filter по ownership). `IsMyShip(shipNetId)`. Events `OnShipStateChanged/OnOwnershipUpdated`. | ✅ |
| `Assets/_Project/Scripts/Player/ShipController.cs` | + `using ProjectC.Ship.Network/Key`. + `NetworkVariable<ShipTelemetryState> _telemetryState` (read=Everyone, write=Server). + `TelemetryState` getter. + `OnTelemetryStateChanged` event. + `_telemetryState.OnValueChanged += HandleTelemetryValueChanged` в Awake. + `UpdateTelemetryState()` (5Hz throttle). + `ShipDisplayName` (customDisplayName fallback на "{Class} #{instanceId:D4}"). `FixedUpdate` вызывает `UpdateTelemetryState()` server-only. | ✅ |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | + `CreateShipTelemetryClientState()` (root GameObject, DontDestroyOnLoad). Вызывается в OnNetworkSpawn после `CreateShipKeyClientState`. | ✅ |

**Verify**:
- ✅ Compile: 0 errors
- ✅ Reflection probe: все 4 новых типа + их API присутствуют (TelemetryState, ShipOwnershipRegistry, ShipTelemetryClientState, ShipController.TelemetryState)
- ✅ Все events (OnTelemetryStateChanged, OnOwnershipListChanged) определены

**MVP flow (server → client)**:
1. Server: `ShipController.FixedUpdate` → `UpdateTelemetryState()` (5Hz) → пишет в `_telemetryState.Value`
2. NGO: автоматически синхронизирует deltas клиентам
3. Client: `_telemetryState.OnValueChanged` (подписка в Awake) → `HandleTelemetryValueChanged` → `OnTelemetryStateChanged` event
4. `ShipTelemetryClientState.SubscribeToShip(ship)` подписывается на event → обновляет `_allShips` cache
5. UI/HUD подписываются на `ShipTelemetryClientState.OnShipStateChanged(shipNetId)` → реактивно обновляются

**Throttle**: server пишет 5Hz (200ms interval), NGO sync — стандартный delta sync. Throttling сервера предотвращает перегрузку сети.

**Что НЕ сделано** (намеренно):
- ❌ MyShipsTab UI (Phase 2)
- ❌ HUD telemetry widget (зависит от UI-проекта — отдельный тикет)

---

## 2026-06-18 — R2-SHIP-KEY-003 v9 (T-KEY-06: NetworkPlayer F-key wiring — direct calls)

**Контекст**: седьмой тикет R2-SHIP-KEY-003 после T-KEY-05. Заменил reflection-based fallback'и на прямые вызовы `MetaRequirementClientState`/`MetaRequirementRegistry`.

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | + `using ProjectC.MetaRequirement;`. **Client-side F-key** (line 347+): reflection на `MetaRequirementClientState.RequestCanUse` → прямой вызов `MetaRequirementClientState.Instance.RequestCanUse(shipNetId)`. Legacy fallback на `ShipKeyClientState` удалён. **Server-side `SubmitSwitchModeRpc`** (line 518+): reflection на `MetaRequirementRegistry.CanPlayerUse` → прямой вызов `MetaRequirementRegistry.Instance != null && MetaRequirementRegistry.Instance.CanPlayerUse(serverClientId, shipNetId)`. | ✅ |

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings (warnings для pre-existing [Obsolete] ShipKey* в NetworkPlayer удалены — теперь не используются)
- ✅ Reflection probe: `MetaRequirementRegistry.CanPlayerUse(client, netId)` OK, `MetaRequirementClientState.RequestCanUse` OK
- ✅ Прямой call flow: F → `MetaRequirementClientState.RequestCanUse` → `MetaRequirementRegistry.RequestCanUseRpc` → `ShipOwnershipRequirement.CanPlayerUse` (server) → response → `MetaRequirementClientState.OnCanUseResponse` → allowed → `SubmitSwitchModeRpc` → server defense-in-depth check

**Backward compat**: `ShipKeyClientState`/`ShipKeyServer` остаются как `[Obsolete]` aliases для обратной совместимости (R2-SHIP-KEY-001 legacy), но не используются напрямую из NetworkPlayer.

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ ShipTelemetry server + client state → **T-KEY-07** (~3h)
- ❌ MyShipsTab UI → **T-KEY-08** (Phase 2)

---

## 2026-06-18 — R2-SHIP-KEY-003 v8 (T-KEY-05: Transfer logic — drop/pickup с instanceId)

**Контекст**: шестой тикет R2-SHIP-KEY-003 после T-KEY-04. Интеграция KeyRodInstanceWorld в drop/pickup flow.

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Items/Core/InventoryWorld.cs` (TryDrop) | Key-предметы: `GetKeySlotAt(indexInList).instanceId` захватывается ДО удаления. После удаления через `RemoveKeySlotAt` (оба списка) → `TransferInstance(clientId→OWNER_NONE)` + `UpdateState(Lost)` через reflection. | ✅ |
| `Assets/_Project/Items/Network/InventoryServer.cs` | `RequestPickupRpc(int itemId, byte typeByte, Vector3 worldPos, ...)` → `RequestPickupRpc(int itemId, byte typeByte, int instanceId, Vector3 worldPos, ...)`. После успешного pickup: если `instanceId>0 && type=Key` → `TransferInstance(OWNER_NONE→clientId)`. | ✅ |
| `Assets/_Project/Items/Client/InventoryClientState.cs` | + `RequestPickup(itemId, type, instanceId, pos)` — новый overload с instanceId. + `RequestPickup(itemId, type, instanceId, pos, onResult)` — callback-версия. Legacy-версия без instanceId вызывает с `0`. | ✅ |
| `Assets/_Project/Scripts/Core/PickupItem.cs` | `Collect()` читает instanceId из `GetComponent<KeyRodInstanceBinding>()?.TryGetInstanceId(out instanceId)` и передаёт в `RequestPickup(... instanceId ...)`. | ✅ |

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings
- ✅ `RequestPickupRpc` — `instanceId:Int32` в параметрах
- ✅ `InventoryClientState.RequestPickup` — 4 overloads
- ✅ `PickupItem.Collect` — OK
- ✅ `TryDrop` — signature unchanged (внутренний flow расширен)
- ✅ Smoke: `CreateInstance(owner=5)` → `TransferInstance(5→NONE)` → `UpdateState(Lost=1)` → `GetInstancesForPlayer(5)=0`

**Flow MVP**:
- Drop: Player дропает Key → `InventoryWorld.TryDrop` → instanceId захвачен → `TransferInstance(clientId→OWNER_NONE)` + `UpdateState(Lost)` → auto-save
- Pickup: Player подбирает Key → `PickupItem.Collect` → instanceId из `KeyRodInstanceBinding` → `RequestPickupRpc(..., instanceId)` → сервер добавляет в инвентарь (slot с instanceId=0) → `TransferInstance(OWNER_NONE→clientId)` → auto-save

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ NetworkPlayer F-key wiring → **T-KEY-06** (~1.5h)
- ❌ ShipTelemetry server + client state → **T-KEY-07** (~3h)
- ❌ MyShipsTab UI → **T-KEY-08** (Phase 2)

---

## 2026-06-18 — R2-SHIP-KEY-003 v7 (T-KEY-04: KeyRodInstanceBinding explicit component)

**Контекст**: пятый тикет R2-SHIP-KEY-003 после T-KEY-03. Явный `KeyRodInstanceBinding` компонент для PickupItem. Заменяет отменённый auto-bootstrap (Q11).

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceBinding.cs` | NEW. `MonoBehaviour` (не `NetworkBehaviour` — не требует NetworkObject на PickupItem). `[SerializeField] _ship` (ShipController), `_keyItemData` (ItemData). `[RequireComponent(typeof(PickupItem))]`. При старте на сервере: ждёт готовности ShipController (Invoke loop, 15 retries), инициализирует `KeyRodInstanceWorld`, вызывает `CreateInstance(itemId, shipNetId, OWNER_NONE)`. Публичный `TryGetInstanceId(out int)` для T-KEY-05. Editor helpers: `OnValidate` автоподстановка _keyItemData из PickupItem, `Reset` для AddComponent. | ✅ создан |

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings
- ✅ KeyRodInstanceBinding: `MonoBehaviour`, поля `_ship (ShipController)`, `_keyItemData (ItemData)`, `_debugInstanceId (Int32)`, `TryGetInstanceId(out int)`, `RequireComponent(PickupItem)`

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ Pickup flow с instanceId (T-KEY-05 — Transfer логика, использует `TryGetInstanceId`)
- ❌ Wire `CreateAndInitialize(repository)` в рантайм (будет в T-KEY-07)
- ❌ Добавление компонента на существующие `[KeyRod_*]` PickupItem в WorldScene_0_0 (ручная работа через Editor — drag Ship + ItemData)

---

## 2026-06-18 — R2-SHIP-KEY-003 v6 (T-KEY-03: ShipOwnershipRequirement + Registry integration)

**Контекст**: четвёртый тикет R2-SHIP-KEY-003 после T-KEY-PERSIST. Новый `ShipOwnershipRequirement` component + интеграция в `MetaRequirementRegistry` (ownership приоритет).

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Scripts/Ship/Key/ShipOwnershipRequirement.cs` | NEW. `NetworkBehaviour` на кораблях. `CanPlayerUse(clientId, out string reason)` — проверяет `KeyRodInstanceWorld.IsOwnerOfShip(clientId, this.NetworkObjectId)`. DisplayName из `ShipController.CustomDisplayName` (Q6). Server-only. | ✅ создан |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs` | + `_ownershipRequirements` dictionary + `RegisterShipOwnership`/`UnregisterShipOwnership`. `CanPlayerUse` — ownership приоритет, затем MetaRequirement, затем allow. `RequestCanUseRpc` — ownership приоритет, затем MetaRequirement. `OnNetworkDespawn` — очистка ownership. | ✅ updated |

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings
- ✅ `ShipOwnershipRequirement` — `NetworkBehaviour` subclass, `CanPlayerUse(out string)` OK
- ✅ `MetaRequirementRegistry` — `RegisterShipOwnership`/`UnregisterShipOwnership`/`CanPlayerUse` (client, netId) OK
- ✅ Smoke test: `CreateInstance(owner=5, ship=200) → IsOwnerOfShip(5,200)=True, IsOwnerOfShip(99,200)=False`

**Backward compat**: все существующие `MetaRequirement` (LockBox, Legacy ships через ShipKeyBinding) продолжают работать через fallback-путь. Изменения в `CanPlayerUse`/`RequestCanUseRpc` — additive: добавляют ownership check перед MetaRequirement, но если ownership нет — fallback работает как раньше.

**Что НЕ нужно тестировать в Play Mode**:
- Компонент не будет задействован до T-KEY-04 (KeyRodInstanceBinding на pickup) и T-KEY-06 (NetworkPlayer F-key wiring), где ShipOwnershipRequirement будет вызываться через MetaRequirementRegistry
- Legacy `ShipKeyServer.CanPlayerBoard` (deprecated) продолжает работать как раньше

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ KeyRodInstanceBinding explicit pickup component → **T-KEY-04** (~1h)
- ❌ Transfer logic (drop → pickup с instanceId) → **T-KEY-05** (~1h)
- ❌ NetworkPlayer F-key wiring → **T-KEY-06** (~1.5h)

---

## 2026-06-18 — R2-SHIP-KEY-003 v5 (T-KEY-PERSIST: KeyRodInstance persistence через IPlayerDataRepository)

**Контекст**: третий тикет R2-SHIP-KEY-003 после T-KEY-02. Persistence для KeyRodInstance через `IKeyRodInstanceRepository`.

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` | NEW. Interface `IKeyRodInstanceRepository` (LoadAll/SaveAll), DTO `KeyRodInstanceSaveData` (public fields), `JsonKeyRodInstanceRepository` (один JSON файл `KeyRodInstances.json` в `Application.persistentDataPath`) | ✅ создан |
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` | + `CreateAndInitialize(IKeyRodInstanceRepository)` (загрузка из репозитория), + `_repository` field, + `AutoSave()` private метод (сохраняет только Active). Auto-save hook добавлен в `CreateInstance`, `TransferInstance`, `UpdateState`, `DestroyInstance`. `Shutdown` вызывает `AutoSave` перед очисткой. | ✅ updated |

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings
- ✅ Reflection probe:
  - `IKeyRodInstanceRepository` – interface с `LoadAll() → List<KeyRodInstanceSaveData>` и `SaveAll(List<KeyRodInstance>)`
  - `KeyRodInstanceSaveData` – `[Serializable]` class, 6 полей (itemId, registeredShipId, ownerPlayerId, originalOwnerId, state, createdAtUnix)
  - `JsonKeyRodInstanceRepository` – class, implements IKeyRodInstanceRepository
  - `KeyRodInstanceWorld.CreateAndInitialize` – 2 overloads (no-arg + repo)
- ✅ Round-trip smoke test:
  - Init(no repo) → Create → Transfer → UpdateState(Lost) → Destroy → Shutdown → OK
  - Init(with repo) → Create(2 instances) → Transfer → Shutdown (AutoSave: all Active)
  - Re-init(with repo) → GetInstanceCount=2 → id3=restored {item=300, ship=400, owner=99, state=Active} ✅
  - `IsOwnerOfShip(99, 400)=True`, `IsOwnerOfShip(1, 400)=False` ✅
  - Final Shutdown → OK. **Full round-trip PASSED.**

**Known: instanceId эпифемерный** — при загрузке из репозитория instanceId пересоздаётся (счётчик сборки сессии). Это по дизайну (`20_UNIQUE_KEY_INSTANCE.md` §2.5: "instanceId (server counter — НЕ сохраняется)"). Все остальные поля (itemId, shipId, owner, state) сохраняются через DTO.

**Что НЕ нужно тестировать в Play Mode**:
- Репозиторий/auto-save работают автоматически, без ручных вызовов
- Старые legacy ключи (через AddItem → instanceId=0) НЕ затрагиваются
- Эффект в game loop увидим в T-KEY-04 (экземпляры создаются при спавне) и T-KEY-05 (auto-save при transfer)

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ ShipOwnershipRequirement component → **T-KEY-03** (~1.5h)
- ❌ KeyRodInstanceBinding explicit pickup component → **T-KEY-04** (~1h)
- ❌ Transfer logic (drop → pickup с instanceId) → **T-KEY-05** (~1h)
- ❌ Wire `CreateAndInitialize(repo)` в рантайм → T-KEY-04 (KeyRodInstanceBinding.OnNetworkSpawn)

---

## 2026-06-18 — R2-SHIP-KEY-003 v4 (T-KEY-02: Inventory slot extension + instance-id слой)

**Контекст**: второй тикет R2-SHIP-KEY-003 после T-KEY-01. Добавление instance-id слоя в `InventoryData`/`InventoryItemDto`/`InventoryWorld`. Backward compat для всех существующих операций (HasItem, HasAllItems и т.д.).

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Scripts/Core/InventoryData.cs` | + struct `InventorySlot` (itemId + instanceId) + `INetworkSerializable`. + `_keySlots : List<InventorySlot>` + `_keyIds : List<int>` (parallel, для backward compat). + методы `AddKeyItem`, `GetKeySlotAt`, `RemoveKeySlotAt`, `RemoveKeyByInstanceId`, `GetKeyInstanceIdsForItem`, `HasKeyInstance`, `KeySlotCount`. `TotalCount` + `_keySlots.Count`. `NetworkSerialize` + InventorySlot-сериализация. | ✅ |
| `Assets/_Project/Items/Dto/InventoryItemDto.cs` | + поле `int instanceId` (default 0 в NetworkSerialize, Equals, GetHashCode) | ✅ |
| `Assets/_Project/Items/Core/InventoryWorld.cs` | + `AddItemDirect(clientId, itemId, instanceId, itemType)` (4-param overload). + `HasKeyInstance(clientId, instanceId)` – проверяет через data.HasKeyInstance. + `GetMyShips(clientId)` – возвращает пары (instanceId, shipNetId) через KeyRodInstanceWorld. `BuildSnapshot` – для Key-типа читает instanceId из _keySlots. `TryDrop` – для Key-типа синхронизирует удаление из _keySlots | ✅ |

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings (11 pre-existing `[Obsolete]` alias warnings не наши)
- ✅ Reflection probe:
  - `InventorySlot` – struct, `itemId (Int32)`, `instanceId (Int32)`
  - `InventoryData` – все 7 новых методов
  - `InventoryItemDto.instanceId` – Int32
  - `InventoryWorld.HasKeyInstance`, `GetMyShips` – OK
  - `AddItemDirect` – 2 overloads (3-param + 4-param)
- ✅ Smoke test (полный flow):
  - `KeyRodInstanceWorld.CreateInstance(100, ship=200, owner=5)` → id=1
  - `InventoryData.AddKeyItem(100, instanceId=1)` → OK
  - `HasKeyInstance(1)=True`, `HasKeyInstance(999)=False`
  - `KeySlotCount=1`
  - `GetKeySlotAt(0) → {itemId=100, instanceId=1}`
  - `Shutdown` чистит реестр

**Backward compat**: все существующие операции (HasItem, HasAllItems, CountOf, GetMissingItems, AddItem, RemoveItems, TryPickup, TryDrop для non-Key типов) работают как раньше через `GetIdsForType` → `_keyIds` (для Key) или исходные `List<int>`.

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ ShipOwnershipRequirement component → **T-KEY-03** (~1.5h)
- ❌ KeyRodInstanceBinding explicit pickup component → **T-KEY-04** (~1h)
- ❌ Transfer logic (drop → pickup с instanceId) → **T-KEY-05** (~1h)
- ❌ Persistence через IPlayerDataRepository → **T-KEY-PERSIST** (~1.5h)

**Что НЕ нужно тестировать в Play Mode**:
- Старые KeyRod_* PickupItem в WorldScene_0_0 продолжают работать через legacy AddItem + AddItem(Key, itemId) → _keySlots с instanceId=0
- `ItemType.Key = 8` теперь полностью поддерживается в инвентаре (слоты, snapshot, serialization)
- Новые методы (HasKeyInstance, GetMyShips) НЕ вызываются из существующих сценариев — вызов будет в T-KEY-03..07

---

## 2026-06-18 — R2-SHIP-KEY-003 v3 (T-KEY-01: KeyRodInstance + KeyRodInstanceWorld + ItemType.Key)

**Контекст**: первый тикет R2-SHIP-KEY-003 после Q6 префикса. Создание POCO registry для уникальных ключей кораблей.

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` | NEW. `[Serializable]` class с полями `instanceId/itemId/registeredShipId/ownerPlayerId/originalOwnerId/state/createdAtUnix` + enum `KeyRodInstanceState {Active, Destroyed, Lost}` + const `OWNER_NONE = ulong.MaxValue` | ✅ создан |
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` | NEW. Server-only static facade (паттерн `CraftingWorld`). API: `CreateInstance`, `TransferInstance`, `UpdateState`, `DestroyInstance`, `GetInstance/GetInstanceForShip/GetInstancesForPlayer/GetPlayerShips`, `IsOwnerOfInstance/IsOwnerOfShip`, `GetAllInstances/GetInstanceCount`. Lifecycle: `CreateAndInitialize` / `Shutdown`. Event: `static OnOwnershipChanged(int instanceId, ulong newOwner)` для T-KEY-07. | ✅ создан |
| `Assets/_Project/Scripts/Core/ItemType.cs` | + enum value `Key = 8` (Q1) | ✅ добавлено |

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings
- ✅ Reflection probe (run after compile):
  - `ItemType.Key = 8` (enum)
  - `KeyRodInstance` — `[Serializable]` class с 7 public полями + `OWNER_NONE = 18446744073709551615` (ulong.MaxValue)
  - `KeyRodInstanceState` — `Active=0, Destroyed=1, Lost=2`
  - `KeyRodInstanceWorld` — `abstract+sealed static` class, 16 публичных static методов, event `Action<int, ulong>`
- ✅ Smoke test (полный flow):
  - `CreateInstance(itemId=31, ship=100, owner=NONE)` → id=1 (instance в мире)
  - `CreateInstance(itemId=32, ship=101, owner=0)` → id=2 (instance у player 0)
  - `TransferInstance(1, NONE→5)` → True (из мира → player 5)
  - `IsOwnerOfShip(0, 101)=True`, `IsOwnerOfShip(5, 100)=True`, `IsOwnerOfShip(99, 100)=False`
  - `GetPlayerShips(0)=1`, `GetPlayerShips(5)=1`, `GetInstanceCount()=2`
  - `Shutdown` → `IsInitialized=False`

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ Persistence через IPlayerDataRepository → **T-KEY-PERSIST** (~1.5h)
- ❌ Inventory slot extension (instance-id слой) → **T-KEY-02** (~2h)
- ❌ ShipOwnershipRequirement component → **T-KEY-03** (~1.5h)
- ❌ KeyRodInstanceBinding explicit pickup component → **T-KEY-04** (~1h)
- ❌ Wire в `KeyRodInstanceBinding.OnNetworkSpawn` для авто-вызова `CreateAndInitialize` → T-KEY-04

**Что НЕ нужно тестировать в Play Mode**:
- POCO registry полностью изолирован, не подключён к существующим сценам
- Старый `ShipKeyBinding` / `ShipKeyServer` legacy aliases продолжают работать как раньше — никаких изменений в API
- `ItemType.Key = 8` — новый enum value, существующие ItemData с `Equipment=1` НЕ затронуты

**Известные особенности**:
- `KeyRodInstanceWorld.CreateInstance` валидирует `itemId` через `InventoryWorld.Instance.GetItemDefinition(itemId) != null` — если InventoryWorld ещё не инициализирован (race на StartHost), валидация пропускается (lazy check). Это намеренно: в T-KEY-04 binding будет создан в OnNetworkSpawn ПОСЛЕ InventoryServer.OnNetworkSpawn.
- Smoke test запускал API через reflection — `_nextInstanceId` сбрасывается в `Shutdown`, поэтому повторные smoke test чистые.

---

## 2026-06-18 — R2-SHIP-KEY-003 v3 (T-KEY-00: Q6 ShipController префикс)

**Контекст**: реализация Q6 (displayName через ShipController) — первый код-шаг R2-SHIP-KEY-003, до T-KEY-01.

**Что изменилось в коде**:

| Файл | Что | Зачем |
|---|---|---|
| `Assets/_Project/Scripts/Player/ShipController.cs` | + поле `[SerializeField] private string _customDisplayName = ""` + геттер `public string CustomDisplayName` | Q6: минимальный фикс в ShipController, "подтягивается к ключу". Доступно с клиента и сервера (scene-placed object, не требует NetworkVariable) |

**Совместимость**: 100% backward compat. Поле дефолт `""` = пустая строка. Если в инспекторе ничего не задано — клиент сам сделает fallback `"Light #42"` / `"Medium #42"` и т.п. (T-KEY-07).

**Verify**:
- ✅ Compile: 0 errors, 0 new warnings
- ✅ Runtime reflection probe: `type_found=true`, `field_found=true (System.String)`, `property_found=true, can_read=true`
- ✅ Никаких конфликтов с существующими полями (поиск по 'name/display' нашёл только наш `_customDisplayName` + `CustomDisplayName` + стандартный `Object.name`)

**Что НЕ сделано** (намеренно, для следующих тикетов):
- ❌ TelemetryState NetworkVariable (T-KEY-07)
- ❌ Fallback-логика "Light #42" (T-KEY-07)
- ❌ Pull-through в KeyRodInstance.displayName (T-KEY-01..02)

**Что НЕ нужно тестировать в Play Mode**:
- Поле пока НЕ читается ни одним скриптом (всё ещё ссылается на старое `_shipDisplayName` через ShipKeyBinding legacy alias)
- Эффект увидим в T-KEY-07 когда TelemetryState.startnet читает `CustomDisplayName`

---

## 2026-06-18 — R2-SHIP-KEY-003 v2 (decision integration)

**Контекст**: пользователь ответил на 12 вопросов в `24_OPEN_QUESTIONS.md` (2026-06-18). Применены 3 архитектурных изменения.

**Что изменилось**:

| Изменение | Где применено |
|---|---|
| **Q4: NetworkVariable-based telemetry** (было polling RPC) | `22_SHIP_TELEMETRY_PLAN.md` — полностью переписан. `23_ROADMAP.md` T-KEY-07 (effort 2.5h → 3h). |
| **Q11: Explicit `[KeyRodInstanceBinding]`** (было auto-bootstrap через FindNearestShip) | `20_UNIQUE_KEY_INSTANCE.md` §2.4, §3.4, §6. `23_ROADMAP.md` T-KEY-04 (новое название + уточнённый scope). |
| **Q12: Persist через `IPlayerDataRepository`** (было без persist) | `20_UNIQUE_KEY_INSTANCE.md` §2.5 (новая секция). `23_ROADMAP.md` — добавлен T-KEY-PERSIST (~1.5h). |
| **Q8: pilotCount убран из MVP** | `22_SHIP_TELEMETRY_PLAN.md` §5 (убран). `23_ROADMAP.md` §6 (out of scope). |
| **Q6: DisplayName через ShipController._customDisplayName** (было отдельное inspector поле) | `21_SHIP_OWNERSHIP_MODEL.md` §2.2. `22_SHIP_TELEMETRY_PLAN.md` §2.3 (ShipController расширение). |

**Обновлены файлы** (5 патчей):
- `20_UNIQUE_KEY_INSTANCE.md` — добавлены §2.5, §2.6, уточнены §2.4, §3.4, §4 (точки вставки), §5.1, §6 edge-cases
- `21_SHIP_OWNERSHIP_MODEL.md` — displayName через ShipController (Q6)
- `22_SHIP_TELEMETRY_PLAN.md` — полностью переписан под NetworkVariable (Q4)
- `23_ROADMAP.md` — переписан: T-KEY-04, T-KEY-07, новый T-KEY-PERSIST
- `24_OPEN_QUESTIONS.md` — все Q1..Q12 resolved, архив оригиналов

**Что НЕ сделано**: код. Только документация.

**Связь с существующим**: ShipKeyBinding / ShipKeyServer / ShipKeyClientState / ShipKeyToast остаются как `[Obsolete]` legacy aliases (R2-META-REQ-001). MetaRequirement для блоков/дверей продолжает работать.

**Что отложено в фазу 2** (без изменений после decision integration):
- Крафт ключей на верфи
- `isDuplicate` (нелегальные копии)
- `KeyRodAccessLevel` (Limited / OneTime)
- NPC-продажа ключей
- Угон / pirate flow
- Salvage / repair
- Cargo items breakdown в telemetry DTO
- Multi-pilot display

---

## 2026-06-18 — R2-SHIP-KEY-003 v1 (planned, initial design)

**Что добавлено** (6 новых файлов):

| Файл | Что в нём |
|---|---|
| `20_UNIQUE_KEY_INSTANCE.md` | Концепция KeyRodInstance, POCO singleton `KeyRodInstanceWorld`, расширение `InventoryData` для instance-id слоя. |
| `21_SHIP_OWNERSHIP_MODEL.md` | Server-side реестр владельцев, новый компонент `ShipOwnershipRequirement`, расширение `MetaRequirementRegistry`. |
| `22_SHIP_TELEMETRY_PLAN.md` | Подсистема `ShipTelemetry` (v1: polling RPC + ShipTelemetryDto + ShipTelemetryServer/ClientState). |
| `23_ROADMAP.md` | Тикеты T-KEY-01..T-KEY-08. Milestones M1..M5. ~11 часов работы. |
| `24_OPEN_QUESTIONS.md` | 12 вопросов перед стартом T-KEY-01. |
| `99_CHANGELOG.md` | Этот файл. |

**Что НЕ сделано**: код. Дизайн-документы только.

**Связь с существующим**:
- `ShipKeyBinding` / `ShipKeyServer` / `ShipKeyClientState` / `ShipKeyToast` остаются как `[Obsolete]` legacy aliases.
- `MetaRequirement` для блоков/дверей продолжает работать.
- `InventoryWorld` расширяется additive-only.

---

## 2026-06-06 — R2-META-REQ-001 (resolved)

**Что сделано**: миграция с `ShipKeySubsystem` (MVP, 1 корабль ↔ 1 ключ) на обобщённую `MetaRequirement` подсистему.

См. `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` + `00_OVERVIEW.md §12`.

---

## 2026-06-06 — R2-SHIP-KEY-001 (resolved)

**Что сделано**: первичная реализация физического ключа-предмета для запуска корабля.

См. `KNOWN_ISSUES.md` (баг с `Resources.LoadAll` не рекурсивен → ключи не подбирались).
---

## 2026-06-19 — R2-SHIP-KEY-003 v11 (bugfix round: name display, persistence, pickup guard)

**Контекст**: финальный bugfix раунд R2-SHIP-KEY-003 после Play Mode тестирования. Исправлены 4 бага:

1. **F-key не сажал после получения ключа** (T-KEY-06 fix)
2. **Имя ключа в инвентаре показывало generic `Key_Heavy_Ship`** (display name resolution)
3. **Ключ не сохранялся при рестарте** (JsonInventoryRepository не имел Key-типа)
4. **Дубликат ключа при повторном подборе** (guard в TryPickup)

### Баг 1: F-key ship boarding не срабатывал

**Root cause**: `MetaRequirementClientState.OnAccessAllowed` дёргался, но `NetworkPlayer` не был на него подписан для F-key. Событие срабатывало, но `SubmitSwitchModeRpc()` не вызывался.

**Фикс**:
| Файл | Изменение |
|---|---|
| `NetworkPlayer.cs` | `ReceiveMetaRequirementResponseTargetRpc` — после передачи в `MetaRequirementClientState.OnCanUseResponse`, проверяет `allowed && _pendingCanBoardShipId == netId` → вызывает `SubmitSwitchModeRpc()` с `Debug.Log`. |

### Баг 2: имя ключа — generic `Key_Heavy_Ship`

**Root cause**: display name резолвился через `instanceId` / `NetworkObjectId` — оба эфемерные (пересоздаются при каждом старте NGO). `KeyRodInstanceWorld` при загрузке из persistence переназначал instanceId, и старый instanceId в слоте инвентаря не совпадал.

**Фикс**: три уровня fallback:

| Приоритет | Метод | Когда работает |
|---|---|---|
| 1 | `ShipTelemetryClientState.MyShips` по `keyInstanceId` | Внутри сессии, после синхронизации |
| 2 | `KeyRodInstanceWorld.GetInstance` по `instanceId` + `FindShipNameByNetworkId` | На Host внутри сессии |
| 3 | `KeyRodInstanceBinding._ship` по `itemId` (reflection, scene-placed ссылка) | **Стабильно между рестартами** |

Priority 3 — ключевой фикс: `KeyRodInstanceBinding` — scene-placed компонент, его `_ship` ссылка стабильна. Использует reflection для чтения приватных полей `_keyItemData` → `InventoryWorld.GetOrRegisterItemId()` → совпадение по `itemId` → `_ship.CustomDisplayName`.

**Изменённые файлы**:
| Файл | Изменение |
|---|---|
| `InventoryTab.cs` | + `using ProjectC.Ship.Client/Key/Player`. + `ResolveKeyItemDisplayName()` с 3-уровневым fallback. + `TryGetShipNameFromTelemetry()`, `TryGetShipNameFromKeyWorld()`, `TryGetShipNameByItemId()` (reflection-based), `FindShipNameByNetworkId()`. |

### Баг 3: ключ не сохранялся при рестарте

**Root cause**: `JsonInventoryRepository.Converters` не знали про `ItemType.Key`. `InventorySaveData` не имел `keyIds`/`keyInstanceIds`. `ConvertToSaveData` switch дропал Key-тип. `ConvertToInventoryData` не восстанавливал.

**Фикс**:
| Файл | Изменение |
|---|---|
| `JsonInventoryRepository.cs` | `InventorySaveData`: + `List<int> keyIds`, + `List<int> keyInstanceIds`. `ConvertToSaveData`: + `case ItemType.Key:` (сохраняет keyIds + keyInstanceIds). `ConvertToInventoryData`: + загрузка Key через `inv.AddKeyItem(keyIds[i], keyInstanceIds[i])`. |
| `InventoryData.cs` | + `GetKeyInstanceIds()` — доступ к instanceId для конвертера. |

### Баг 4: дубликат ключа при подборе (x2)

**Root cause**: scene-placed `[KeyRod_ShipLight]` респавнится при каждом Play Mode. Игрок может подобрать его снова → второй ключ с неправильным именем.

**Фикс**:
| Файл | Изменение |
|---|---|
| `InventoryWorld.cs` (`TryPickup`) | Для Key-типа: проверка `GetIdsForType(Key).Contains(itemId)` → если есть, `Fail("Ключ уже есть в инвентаре")`. |

### Сопутствующие: wiring persistence в bootstrap flow

**Проблема**: `KeyRodInstanceBinding.Start()` вызывал `KeyRodInstanceWorld.CreateAndInitialize()` без репозитория → перетирал сохранённые данные.

**Фикс**:
| Файл | Изменение |
|---|---|
| `InventoryServer.cs` | `OnNetworkSpawn` — инициализирует `KeyRodInstanceWorld` с `JsonKeyRodInstanceRepository` (после `InventoryWorld`). |
| `KeyRodInstanceBinding.cs` | `TryRegister()` — больше НЕ вызывает `CreateAndInitialize()` если уже инициализирован. `CreateInstance()` сам возвращает существующий instanceId (guard). |

### Тест-план (end-to-end)

| Шаг | Ожидание |
|---|---|
| 1. Play Host | 0 CS errors. Console: `[InventoryServer] KeyRodInstanceWorld initialized with JsonKeyRodInstanceRepository` |
| 2. Подойти к `[KeyRod_ShipLight]`, **E** | `[InventoryWorld] Player 0 picked up ID=... (Key). Total: N` |
| 3. **P** → инвентарь → фильтр "Key" | `🚀 Pushka` (не `Key_Heavy_Ship`) |
| 4. Закрыть P, **F** у корабля | `[NetworkPlayer] MetaRequirement allowed for ship (netId=...). Calling SubmitSwitchModeRpc.` → игрок садится |
| 5. Exit Play Mode → Play Host | В консоли: `JsonKeyRodInstanceRepository` + `Loaded N instances` |
| 6. **P** → "Key" фильтр | `🚀 Pushka` — имя сохранилось |
| 7. **E** на том же ключе | `[InventoryWorld] Ключ (ID=...) уже есть в инвентаре` |
| 8. **P** → всё ещё 1 ключ (не x2) | ✅ |

---

## 2026-06-19 — R2-SHIP-KEY-003 v12 (T-KEY-08: MyShipsTab UI + Architecture Refactor)

**Контекст**: финальный тикет MVP — UI вкладка "Мои корабли" + архитектурный рефакторинг после Play Mode багов.

### Что реализовано

**T-KEY-08 (MyShipsTab UI)**:
- ✅ NEW `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` (~530 строк) — dropdown + info panel + telemetry
- ✅ PATCH `CharacterWindow.uxml` — заменён placeholder на полную структуру
- ✅ PATCH `CharacterWindow.uss` — 11 стилей для вкладки
- ✅ PATCH `CharacterWindow.cs` — добавлено поле `_myShipsTab`, удалены мусорные поля

**Архитектурный рефакторинг (после Play Mode тестов)**:
- ✅ PATCH `Assets/_Project/Items/Data/ItemRegistry.asset` — добавлены 3 Key entries (id=2009/2010/2011). Стабильные itemId навсегда.
- ✅ PATCH `Assets/_Project/Scripts/Player/ShipController.cs` — auto-attach `ShipOwnershipRequirement` в `Awake()` (каждый корабль защищён автоматически)
- ✅ PATCH `Assets/_Project/Items/Network/InventoryServer.cs` — guard дубликата по `instanceId` (не по `itemId` — раньше блокировало Medium/Heavy)
- ✅ PATCH `MyShipsTab.cs` — 3-level fallback для ownedKeyItemIds (серверные данные / KeyRodInstanceWorld / snapshot клиента)
- ✅ Real-time refresh dropdown: подписка на `InventoryClientState.OnSnapshotUpdated`
- ✅ Persistence файл `KeyRodInstances.json` очищен (3 кривых instance с itemId=1010)

**Compile**: 0 errors.

### Архитектурный принцип

> **Стабильный ID для каждого предмета.** Все Key-предметы должны быть зарегистрированы в `ItemRegistry.asset` с явными ID. Auto-ID через `GetOrRegisterItemId` — fallback для тестов, не production.

### Известные смежные баги (требуют решения)

| Проблема | Приоритет | Effort |
|---|---|---|
| InventoryTab показывает "x2 Pushka" если 2 Key с одним itemId | P1 | 30min |
| Фильтр "Key" иногда неактивен после pickup 3 ключей | P2 | 1h |

---

## 2026-06-19 — R2-SHIP-KEY-003 v13 (T-KEY-08 fix: InventoryTab Key group by instanceId)

**Контекст**: после Play Mode теста выявлен последний баг — при подборе 2+ Key-предметов с одним itemId инвентарь показывал "x2 Pushka" (группировка по itemId, а не по instance).

**Что изменилось**:

| Файл | Изменение |
|---|---|
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/InventoryTab.cs` | `InventoryListItem` struct: + `int instanceId` поле. Группировка в `RefreshInventoryCache`: Key-предметы группируются по `(itemId, instanceId)`, остальные — по `itemId`. |

**Логика**:
```csharp
int groupKey2 = (ItemType)dto.type == ItemType.Key ? dto.instanceId : 0;
var compositeKey = (dto.itemId, groupKey2);
```

**Verify**: 0 errors compile.

**Тест-план**:
1. Подобрать 2 разных ключа (Light + Medium) с разными ItemData
2. Открыть **P** → ИНВЕНТАРЬ → фильтр "Key"
3. Должно быть **2 отдельных строки** с разными именами кораблей
4. Если у 2 разных Key один itemId (legacy) — всё равно 2 строки, т.к. instanceId разный

**MVP завершён полностью.** R2-SHIP-KEY-003 done.

---

## 2026-06-19 — R2-SHIP-KEY-003 v14 (TAB-колесо: Equipment + Key → "ВЛАДЕНИЕ")

**Контекст**: TAB-колесо (InventoryUI) имело 8 секторов = 8 ItemType (0..7), Key=8 отсутствовал. Дублирование фильтра в P-табе — игрок путался между Equipment (одежда/модули) и Key.

**Что изменилось**:

| Файл | Изменение |
|---|---|
| `Assets/_Project/UI/Client/InventoryUI.cs` | Сектор 1 (Equipment) теперь объединён с Key — счётчик `count += state.GetCountByType(Key)`. Sublist показывает оба типа. Для Key-предметов отображается имя корабля через scene-placed binding. |
| `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` | Label сектора 1: "ОБОРУДОВАНИЕ" → "ВЛАДЕНИЕ" |

**UX**:
- Сектор "ВЛАДЕНИЕ" показывает: одежда + модули + ключи
- Sublist: `[Equipment] куртка MK2` → `[Key] 🚀 Pushka` (с эмодзи)
- Счётчик: `[3]` если 1 одежда + 2 ключа

**Compile**: 0 errors.

**Архитектурное правило**: TAB-колесо остаётся на 8 секторах (8 типов = 8 углов). Объединение Equipment+Key — единственное исключение, потому что оба являются "носимыми". Если добавятся другие "нательные" типы (м.б. Consumables?), их тоже стоит объединить в "ВЛАДЕНИЕ".

**MVP завершён полностью.** R2-SHIP-KEY-003 done.

---

## 2026-06-19 — R2-SHIP-KEY-003 v15 (T-KEY-09: Drop key ownership fix)

**Контекст**: критический архитектурный баг — при drop'е Key-предмета `KeyRodInstanceWorld` оставался с `ownerPlayerId = playerId, state = Active`. Игрок мог продолжать управлять кораблём.

**Root cause**: 3 причины найдены через глубокий sub-анализ:

| # | Причина | Эффект |
|---|---|---|
| 1 | `TryDrop` вызывал `TransferInstance` только если `slot.instanceId > 0`. При `instanceId = 0` (drop'нутый ключ) — ownership не сбрасывался | Drop bug — основной |
| 2 | При pickup drop'нутого ключа (`KeyRodInstanceBinding` отсутствует) — `instanceId = 0` в слоте | Никак не отличить от нового |
| 3 | `KeyRodInstanceBinding.TryRegister()` стартует в `Start` — race condition с scene-load и InventoryServer.OnNetworkSpawn | Persistence файл загрязняется кривыми instance'ами |

### Фиксы (3 шага)

| Шаг | Файл | Что |
|---|---|---|
| Шаг 1: Drop fix | `Assets/_Project/Items/Core/InventoryWorld.cs` | + `FindActiveKeyInstance(clientId, itemId)` — поиск instance по (itemId, owner, state=Active). `TryDrop` сначала пробует slot.instanceId, fallback на поиск. Если не нашли — Debug.LogWarning но продолжает. |
| Шаг 2: Pickup creates instance | `Assets/_Project/Items/Core/InventoryWorld.cs` | + `TryPickup` принимает `instanceId` параметром. Для Key без `KeyRodInstanceBinding` (drop'нутый) — создаёт новый instance через `CreateInstance(itemId, 0, clientId)`. Slot получает правильный instanceId. |
| Шаг 3: Binding.TryRegister в Awake | `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceBinding.cs` | Awake() → если IsServer + InventoryWorld есть → TryRegister() сразу. Fallback — Start → TryRegister. |

**Файлы изменены:**
- ✅ `InventoryWorld.cs` (TryDrop + TryPickup signature + FindActiveKeyInstance helper)
- ✅ `InventoryServer.cs` (передаёт instanceId в TryPickup)
- ✅ `KeyRodInstanceBinding.cs` (Awake TryRegister)

**Compile**: 0 errors.

### Тест-план (drop bug fix)

| Шаг | Ожидание |
|---|---|
| 1. Подобрать Light ключ | Key в инвентаре, instanceId=1, state=Active, owner=player |
| 2. **TAB → ВЛАДЕНИЕ → БРОСИТЬ** | Console: `[InventoryWorld] Key dropped: instanceId=1, TransferInstance(client=0, NONE) + UpdateState(Lost)` |
| 3. F у корабля (Light) | ❌ Доступ запрещён (IsOwnerOfShip → false, state=Lost) |
| 4. Exit Play → Play Host | Persistence сохранён state=Lost, owner=NONE |
| 5. F у корабля | ❌ Всё ещё запрещён |
| 6. Pickup drop'нутого ключа | Console: `[InventoryWorld] Created new KeyRodInstance for drop-ped key: itemId=2010, instanceId=N, owner=0` |
| 7. F у корабля | ✅ Доступ разрешён (новый instanceId=Active, owner=player) |

**Архитектурный комментарий**: эти 3 фикса решают конкретные баги но НЕ решают фундаментальную проблему двойной структуры данных. Полный рефакторинг по плану `28_KEY_ARCHITECTURE_REVIEW.md` остаётся в Phase 2 (~13 часов).

---

## 2026-06-19 — R2-SHIP-KEY-003 v16 (Phase C: Remove ALL reflection → direct calls)

**Контекст**: полное удаление reflection-вызовов из подсистемы ключей. Найдено и заменено 5 мест.

**Что изменилось**:

| Файл | Было | Стало |
|---|---|---|
| `InventoryWorld.cs` TryPickup | `typeof(KeyRodInstanceWorld).GetMethod("CreateInstance")...Invoke()` | `KeyRodInstanceWorld.CreateInstance()` — прямой вызов |
| `InventoryWorld.cs` TryDrop | `typeof(KeyRodInstanceWorld).GetMethod("TransferInstance")...Invoke()` + `GetMethod("UpdateState")...Invoke()` | `KeyRodInstanceWorld.TransferInstance()` + `.UpdateState()` — прямые вызовы |
| `InventoryServer.cs` RequestPickupRpc | `Type.GetType("...KeyRodInstanceWorld, Assembly-CSharp").GetMethod("TransferInstance")...Invoke()` | `KeyRodInstanceWorld.TransferInstance()` — прямой вызов |

**Что дало**: 0 reflection к KeyRodInstanceWorld. Все вызовы компилируются — ошибки в аргументах выявляются на этапе компиляции, а не в runtime.

**Техника замены**: удалено 3 уровня проверок (`krwType != null`, `transfer != null`, `if (createMethod != null)`) — методы гарантированно есть в сборке.

**Compile**: 0 errors.

**Связанный документ**: `docs/Ships/Key-subsystem/29_KEY_REFACTOR_PLAN.md` — Phase C complete.

**Что осталось (Phases D-F)**:
- Phase D: `InventoryData` — `_keyIds` не сериализуется. `GetIdsForType(Key)` возвращает пустой список на клиенте после Network-десериализации
- Phase E: `KeyRodInstanceBinding` — упрощение регистрации (сейчас все ещё в Wake/Start с retries)
- Phase F: UI — reflection fallback'и пока остаются (упрощать при рефакторинге UI)

---

*Changelog ведёт агент Mavis.*

---

## 2026-06-19 — v17 (Phase D: InventoryData._keyIds serialization fix)

**Root cause**: `GetIdsForType(ItemType.Key)` возвращал `_keyIds` (не сериализуется через NetworkVariable). После Network-синхронизации на клиенте `_keyIds` пуст → `HasItem`/`CountOf`/`HasAllItems` для Key-предметов возвращали неверные значения.

**Fix**: `GetIdsForType(ItemType.Key)` теперь вычисляется из `_keySlots` (сериализуется через `InventorySlot.NetworkSerialize`). `_keyIds` больше не нужен для чтения — сохраняется только для backward compat через `AddItem`/`AddKeyItem`.

**Файл**: `Assets/_Project/Scripts/Core/InventoryData.cs` — метод `GetIdsForType()`.

**Compile**: 0 errors.
---

*Changelog ведёт агент Mavis.*

---

## 2026-06-19 — R2-SHIP-KEY-003 v18-v20 (Drop↔Pickup bugfix: фаза окончательная)

**Контекст**: после v17 обнаружен повторяющийся баг — после drop+pickup ключа вкладка "КОРАБЛЬ" не показывает корабль, ключ которого был выброшен и подобран. Каждое предыдущее исправление давало регрессию где-то ещё. Найден **финальный root cause**: `data.AddItem(itemType, itemId)` создавал слот с `instanceId=0` для Key-предметов, а последующее `UpdateKeySlotInstanceId(clientId, instanceId)` не работало корректно с таймингом AddItem → BuildSnapshot.

**Что изменилось в коде**:

| Файл | Что | Статус |
|---|---|---|
| `KeyRodInstanceWorld.cs` | + `FindActiveKeyInstance(clientId, itemId)` — поиск существующего Active instance по (itemId, owner). Используется при pickup drop'нутого ключа чтобы **не создавать дубль** с `registeredShipId=0`. | ✅ |
| `InventoryWorld.cs` (TryPickup) | **КРИТИЧНЫЙ ФИКС**: для Key-предметов теперь `data.AddKeyItem(itemId, instanceId)` вместо `data.AddItem(itemType, itemId)`. `AddKeyItem` создаёт слот сразу с правильным instanceId. `GetMyShips` фильтрует `instanceId<=0`, поэтому instanceId=0 в слоте делал корабль невидимым в UI. | ✅ |
| `InventoryWorld.cs` (TryPickup) | Логика `FindLostInstance` остаётся первой попыткой (реактивация); `FindActiveKeyInstance` — fallback если Lost не нашли. Если ничего — `CreateInstance(itemId, 0, clientId)` (последний fallback). | ✅ |
| `KeyRodInstanceBinding.cs` | TryRegister с **retry-loop** через `Invoke(nameof(TryRegister), 1.0f)` × 15 попыток. Без retry регистрация могла не успеть до первого pickup (InventoryServer спавнится ПОЗЖЕ scene-placed объектов). | ✅ |

**Verify**:
- ✅ Compile: 0 errors
- ✅ Flow: drop → pickup → вкладка "КОРАБЛЬ" показывает корабль корректно
- ✅ Persistence: `KeyRodInstances.json` хранит Lost instances (AutoSave фильтр `!= Destroyed`)
- ✅ Не дубликатов: instance[4] (shipId=0) больше не создаётся при drop↔pickup

**Архитектурное замечание**: фикс прошёл через несколько итераций:
- v18: добавлен `FindActiveKeyInstance` (не помогло — слот всё ещё instanceId=0)
- v19: retry-loop в KeyRodInstanceBinding (стабилизировало регистрацию)
- v20: `data.AddKeyItem` вместо `data.AddItem` для Key-предметов (финальное решение)

**Что НЕ сделано** (Phase 2):
- Phase E+F (reflection removal в UI) — частично сделано в v16
- Полный рефакторинг по `28_KEY_ARCHITECTURE_REVIEW.md` — отложен, текущая система работает

---

## 2026-06-19 — Phase G: Документация и планы

**Что добавлено**:

| Файл | Описание |
|---|---|
| `docs/Ships/Key-subsystem/27_TKEY09_DROP_FIX_PLAN.md` | Полный план 3-шагового фикса drop bug |
| `docs/Ships/Key-subsystem/28_KEY_ARCHITECTURE_REVIEW.md` | Глубокий архитектурный анализ: 5 reflection → 0, 11 проблем найдено, рекомендация полного рефакторинга |
| `docs/Ships/Key-subsystem/29_KEY_REFACTOR_PLAN.md` | Дизайн новой архитектуры (KeyInstance + KeyRegistry) |
| `docs/Ships/Key-subsystem/SHIP_KEY_SETUP_v11.md` | Пошаговая инструкция настройки ключ↔корабль |
| `docs/dev/SHIP_KEY_SETUP_v11.md` | Дубликат для dev-секции |

**Итоговое состояние подсистемы**:

| Компонент | Статус |
|---|---|
| KeyRodInstance + KeyRodInstanceWorld + ItemType.Key | ✅ v3 |
| Inventory slot extension | ✅ v4 |
| Persistence через IPlayerDataRepository | ✅ v5 |
| ShipOwnershipRequirement + Registry | ✅ v6 |
| KeyRodInstanceBinding explicit | ✅ v7 |
| Transfer logic (drop/pickup) | ✅ v8 + v18-v20 |
| NetworkPlayer F-key wiring | ✅ v9 |
| ShipTelemetry NetworkVariable | ✅ v10 |
| Bugfix round (name, persistence, guard) | ✅ v11 |
| MyShipsTab UI | ✅ v12 |
| InventoryTab instanceId group | ✅ v13 |
| TAB-колесо ВЛАДЕНИЕ | ✅ v14 |
| T-KEY-09 Drop fix | ✅ v15 |
| Phase C: Remove reflection | ✅ v16 |
| Phase D: Serialization fix | ✅ v17 |
| Drop↔Pickup final fix | ✅ v18-v20 |

**MVP готов** ✅
