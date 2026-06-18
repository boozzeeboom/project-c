# Ship Key Subsystem — Changelog

Журнал изменений документации подсистемы Key.

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

*Changelog ведёт агент Mavis.*