# Ship Key — Roadmap (R2-SHIP-KEY-003)

**Подсистема:** Уникальный ключ корабля + ownership + telemetry
**Дата:** 2026-06-18 (обновлено: 2026-06-18 — Q4/Q11/Q12 decisions integrated)
**Статус:** 📋 Дизайн готов, код НЕ начат
**Префикс тикетов:** `T-KEY-##` (расширение существующего R2-SHIP-KEY-XXX)
**Связанные документы:**
- `20_UNIQUE_KEY_INSTANCE.md` — KeyRodInstance
- `21_SHIP_OWNERSHIP_MODEL.md` — ownership model
- `22_SHIP_TELEMETRY_PLAN.md` — NetworkVariable-based telemetry
- `24_OPEN_QUESTIONS.md` — открытые вопросы (resolved)
- `00_OVERVIEW.md` — текущая реализация
- `docs/Markets/`, `docs/NPC_quests/` — формат roadmap'а (mirror NPC_quests/08_ROADMAP.md)

---

## §0 — TL;DR

**Цель**: реализовать уникальный ключ корабля (1 ключ ↔ 1 корабль, передаваемый) на базе существующих `MetaRequirement` + `InventoryWorld`. Сервер знает какие корабли принадлежат игроку по ключам в его инвентаре и передаёт актуальные данные (груз, fuel, modules, state) в HUD и UI через **NetworkVariable-based push** (Q4).

**Сейчас**: ключ = `ItemData` (definition), все ключи одного типа неразличимы. 2 ShipLight = 2 ключа работают на оба.

**Цель**: `KeyRodInstance` (POCO, server-side) с уникальным `instanceId`, `registeredShipId`, `ownerPlayerId`. Inventory хранит `(itemId, instanceId)` пары. Persist через `IPlayerDataRepository` (Q12).

**Изменения после Q4/Q11/Q12**:
- Q4 → telemetry = NetworkVariable-based (вместо polling RPC)
- Q11 → explicit `KeyRodInstanceBinding` компонент (вместо auto-bootstrap)
- Q12 → persist через `IPlayerDataRepository` (добавлен T-KEY-PERSIST)

**Открыто**: 0 вопросов (все Q1..Q12 решены, см. `24_OPEN_QUESTIONS.md`).

---

## §1 — Принципы разбивки

- **1 тикет = 1 сессия = 30-120 мин** — additive-only, без удаления legacy.
- **Backward compat**: `ShipKeyBinding` / `ShipKeyServer` остаются `[Obsolete]` алиасами. Новые сцены используют `ShipOwnershipRequirement`.
- **Compile-clean после каждого тикета** — refresh + read_console + 0 errors.
- **User runs Play Mode** — verify checklist, не автоматические тесты.

---

## §2 — Финальный порядок тикетов

```
T-KEY-01 → T-KEY-02 → T-KEY-PERSIST → T-KEY-03 → T-KEY-04 → T-KEY-05 → T-KEY-06 → T-KEY-07 → T-KEY-08
   │           │           │              │           │           │           │           │
   ▼           ▼           ▼              ▼           ▼           ▼           ▼           ▼
 KeyRod    Inventory   Persist        ShipOwner  Explicit   Transfer  NetworkPlayer  ShipTele    UI tab
Instance   slot       integration    shipReq    KeyRod      logic     F-key wiring   metry       (Phase 2)
World      extension   (Q12)         on ship    Instance    (drop→   (legacy+new)   server
                                                              pickup)                clientState
```

Зависимости:
- T-KEY-02 зависит от T-KEY-01 (нужен KeyRodInstance + World)
- T-KEY-PERSIST зависит от T-KEY-01 (расширяет KeyRodInstanceWorld)
- T-KEY-03 зависит от T-KEY-02 (нужны slot extension для ownership lookup)
- T-KEY-04 зависит от T-KEY-01 + T-KEY-03 (binding компонент ссылается на instance)
- T-KEY-05 зависит от T-KEY-02 + T-KEY-03 (transfer требует slot в инвентаре)
- T-KEY-06 зависит от T-KEY-03 + T-KEY-04 (NetworkPlayer wiring требует оба)
- T-KEY-07 зависит от T-KEY-01..T-KEY-06 + ShipController (полная картина)
- T-KEY-08 — отдельный поток (UI), может стартовать после T-KEY-07

---

## §3 — Тикеты (детально)

### T-KEY-01: KeyRodInstance + KeyRodInstanceWorld

**Скоуп**:
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` (POCO struct/class)
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` (server-only static facade по типу CraftingWorld)
- `KeyRodInstanceState` enum
- Базовые методы: `CreateInstance(itemId, shipNetId, ownerId)`, `DestroyInstance`, `GetInstance`, `GetInstanceIdForShip`, `GetInstancesForPlayer`

**Что НЕ делаем**: persistence (T-KEY-PERSIST), integration с InventoryWorld (T-KEY-02).

**Verify**:
- Compile: 0 errors.
- Roslyn probe: `KeyRodInstanceWorld.IsInitialized == false` до bootstrap.
- После CreateInstance → GetInstance возвращает правильный объект.

**Риск**: низкий. Изолированный POCO.

**Effort**: 1.5 часа.

---

### T-KEY-02: Inventory slot extension (instance-id слой)

**Скоуп**:
- `InventoryData.cs`: `List<InventorySlot>` параллельно с `List<int>` (или полная замена с backward compat shim)
- `InventoryItemDto.cs`: + поле `int instanceId`
- `InventoryWorld.cs`: + `HasKeyInstance(clientId, instanceId)`, `GetMyShips(clientId)`, `AddItemDirect(..., instanceId)`
- `InventoryServer.cs`: TryPickup/TryDrop пробрасывают instanceId
- Backward compat: `HasItem`/`HasAllItems` работают как раньше (через `GetIdsForType` который извлекает itemId из slot)

**Что НЕ делаем**: ShipOwnershipRequirement, ShipTelemetry (это T-KEY-03+).

**Verify**:
- Compile: 0 errors.
- Roslyn: `inventoryWorld.AddItemDirect(0, 31, instanceId=42, ItemType.Equipment)` → slot добавлен с instanceId=42.
- Roslyn: `inventoryWorld.HasKeyInstance(0, 42) == true`.
- Roslyn: `inventoryWorld.GetMyShips(0)` возвращает правильные пары.

**Риск**: средний. `InventoryData` — широко используемая структура. Минимальное изменение shape, additive-only.

**Effort**: 2 часа.

---

### T-KEY-PERSIST: Persistence через IPlayerDataRepository (Q12)

**Скоуп**:
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` (`IPlayerDataRepository` имплементация)
- `KeyRodInstanceSave.cs` (DTO для JSON)
- Интеграция в `KeyRodInstanceWorld.CreateAndInitialize(IPlayerDataRepository)`
- Auto-save в `KeyRodInstanceWorld.CreateInstance` / `TransferInstance` / `DestroyInstance`
- Auto-load в `KeyRodInstanceWorld.Initialize` (через loop по репозиторию)

**Файл**: `Assets/_Project/Resources/KeyRodInstances/{clientId}.json` (по аналогии с CharacterSaveData).

**Что НЕ делаем**: salvage orphaned instances (фаза 2).

**Verify**:
- Compile: 0 errors.
- Play Mode:
  1. StartHost → создать instance → dостановить server.
  2. Restart server → instance восстанавливается с тем же owner.
- Roslyn: `repository.Load(clientId)` возвращает сохранённые instance'ы.

**Риск**: низкий. Шаблон уже есть в `InventoryWorld._repository` + `JsonCharacterDataRepository`.

**Effort**: 1.5 часа.

---

### T-KEY-03: ShipOwnershipRequirement на кораблях

**Скоуп**:
- `Assets/_Project/Scripts/Ship/Key/ShipOwnershipRequirement.cs` (NetworkBehaviour)
- `MetaRequirementRegistry.cs`: + `RegisterShipOwnership()`, расширить `CanPlayerUse` (ownership приоритет)
- `KeyRodInstanceWorld.cs`: + `IsOwnerOfShip(clientId, shipNetId)`

**Что НЕ делаем**: NetworkPlayer F-key wiring, explicit binding (T-KEY-04+).

**Verify**:
- Compile: 0 errors.
- После T-KEY-04: Roslyn `MetaRequirementRegistry.Instance.CanPlayerUse(0, shipNetId) == true` если ключ в инвентаре.

**Риск**: низкий. Новый компонент, additive.

**Effort**: 1.5 часа.

---

### T-KEY-04: KeyRodInstanceBinding (explicit pickup → ship) (Q11)

**Скоуп** (Q11, изменён с bootstrap на explicit component):
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceBinding.cs` (MonoBehaviour)
- `[SerializeField] private ShipController _ship` — drag-and-drop в инспекторе
- `[SerializeField] private ItemData _keyItemData` (backward compat с `PickupItem`)
- При `OnNetworkSpawn` (server-only): создаёт `KeyRodInstance` через `KeyRodInstanceWorld.CreateInstance` с `registeredShipId = _ship.NetworkObjectId`
- **НЕТ** `KeyRodInstanceBootstrap.cs` (отменён по Q11)
- **НЕТ** `FindNearestShip` auto-detect

**Изменения в сцене**:
- `WorldScene_0_0.unity`: на каждый `[KeyRod_*]` GameObject добавляется `[KeyRodInstanceBinding]` + drag соответствующего корабля

**Что НЕ делаем**: pickup flow с instance (уже работает через T-KEY-02).

**Verify**:
- Compile: 0 errors.
- Play Mode → StartHost → Console: `[KeyRodInstanceBinding] Registered: keyRod=Item_Key_ShipLight, ship=Ship_Light (netId=N), instanceId=42`.
- Roslyn: `KeyRodInstanceWorld.GetInstanceIdForShip(shipNetId)` возвращает правильный instanceId.

**Риск**: низкий. Явный компонент = понятная ответственность.

**Effort**: 1 час.

---

### T-KEY-05: Transfer logic (drop → pickup другого)

**Скоуп**:
- `InventoryServer.TryDrop` → после успешного drop: `KeyRodInstanceWorld.UpdateState(instanceId, Lost)` (auto-save через T-KEY-PERSIST)
- `InventoryServer.TryPickup` → после успешного pickup: `KeyRodInstanceWorld.TransferInstance(instanceId, from=NONE, toClientId)` (auto-save)
- `KeyRodInstanceWorld`: + `UpdateState`, `TransferInstance`, событие `OnOwnershipChanged` (для ShipOwnershipRegistry)

**Что НЕ делаем**: cross-scene pickup (это уже работает).

**Verify**:
- Compile: 0 errors.
- Play Mode:
  1. Player A подбирает ключ → instanceId=42, owner=0.
  2. A дропает → instanceId=42, state=Lost.
  3. Player B подбирает → instanceId=42, owner=1.
  4. Console: `[KeyRodInstanceWorld] TransferInstance: 42, NONE → 1`.

**Риск**: низкий. Интеграция в существующие методы.

**Effort**: 1 час.

---

### T-KEY-06: NetworkPlayer F-key wiring

**Скоуп**:
- `NetworkPlayer.cs`: в `SubmitSwitchModeRpc` (server side) добавить `MetaRequirementRegistry.Instance.CanPlayerUse` check (вместо текущего `ShipKeyServer.CanPlayerBoard`).
- `NetworkPlayer.cs`: в `Update` (client side, owner) добавить client-side pre-check через `ShipTelemetryClientState.IsMyShip` + toast.
- `NetworkPlayer.cs`: + Target RPC `ReceiveShipKeyBindingsTargetRpc` остаётся как legacy alias.

**Что НЕ делаем**: ShipTelemetry (T-KEY-07).

**Verify**:
- Compile: 0 errors.
- Play Mode:
  1. Player без ключа подходит к Ship → F → toast "Нет ключа корабля (владелец: client#0)".
  2. Player с ключом подходит к Ship → F → садится.

**Риск**: средний. Меняем critical path F-key.

**Effort**: 1.5 часа.

---

### T-KEY-07: ShipTelemetry (NetworkVariable-based, Q4)

**Скоуп** (Q4 — переписан с polling RPC на NetworkVariable):
- `Assets/_Project/Ship/Network/Dto/ShipTelemetryState.cs` (struct, INetworkSerializable + IEquatable)
- `Assets/_Project/Ship/Network/ShipOwnershipRegistry.cs` (NetworkBehaviour, NetworkList<OwnershipEntry>)
- `Assets/_Project/Ship/Client/ShipTelemetryClientState.cs` (singleton, агрегатор NetworkVariable)
- `Assets/_Project/Player/ShipController.cs` (расширение):
  - + `NetworkVariable<ShipTelemetryState> _telemetryState` (server-write, everyone-read)
  - + `[SerializeField] private string _customDisplayName` (Q6)
  - + `UpdateTelemetryState()` в `FixedUpdate` (5 Hz throttle)
  - + `MaybeBroadcastToInterestedClients()` (2 Hz throttled broadcast владельцу)
- `NetworkPlayer.cs`: + `NotifyShipTelemetryChangedRpc(ShipTelemetryState)` [SendTo.Owner]
- `NetworkManagerController.cs`: + `CreateShipTelemetryClientState`
- Read-only API на `ShipCargoRegistry` / `ShipFuelSystem` / `ShipModuleManager` для snapshot

**Что НЕ делаем**: UI tab (T-KEY-08), cargo items breakdown.

**Verify**:
- Compile: 0 errors.
- Play Mode:
  1. Player с ключом → HUD показывает fuel/cargo/state в реальном времени.
  2. A передаёт ключ B → A теряет корабль из списка, B получает.
- Roslyn: `ShipTelemetryClientState.Instance.IsMyShip(shipNetId)` обновляется при transfer.

**Риск**: средний. Много файлов, но additive-only. NetworkVariable — стандартный NGO механизм.

**Effort**: 3 часа.

---

### T-KEY-08: MyShipsTab UI (отложен, Phase 2)

**Скоуп**:
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` (по аналогии с ContractsTab/InventoryTab)
- UXML/USS
- Добавить в CharacterWindow как 5-й tab "Корабли" (Q5)

**Причина отсрочки**: пользователь сказал "сделаем потом UI" — это Phase 2 (после того как server-side полностью готов и протестирован).

**Effort**: 2 часа.

---

## §4 — Milestones

| Milestone | Тикеты | Что работает | Статус |
|---|---|---|---|
| **M1: KeyRodInstance foundation** | T-KEY-01..T-KEY-02 | Instance создаётся, хранится в инвентаре | 📋 не начат |
| **M2: Persistence + Ownership binding** | T-KEY-PERSIST, T-KEY-03..T-KEY-04 | Сервер знает кто чем владеет, persist через репозиторий, explicit binding на pickup | 📋 не начат |
| **M3: Transfer + F-key** | T-KEY-05..T-KEY-06 | Drop/pickup обновляет ownership, F-key правильно блокирует/разрешает | 📋 не начат |
| **M4: Telemetry (NetworkVariable-based)** | T-KEY-07 | Сервер пушит актуальные данные всем клиентам, HUD показывает real-time state | 📋 не начат |
| **M5: UI** (Phase 2) | T-KEY-08 | Игрок видит список своих кораблей в CharacterWindow | 📋 отложен |

---

## §5 — Оценка трудоёмкости

| Ticket | Effort | Cumulative |
|---|---|---|
| T-KEY-01 | 1.5h | 1.5h |
| T-KEY-02 | 2h | 3.5h |
| T-KEY-PERSIST | 1.5h | 5h |
| T-KEY-03 | 1.5h | 6.5h |
| T-KEY-04 | 1h | 7.5h |
| T-KEY-05 | 1h | 8.5h |
| T-KEY-06 | 1.5h | 10h |
| T-KEY-07 | 3h | 13h |
| T-KEY-08 (Phase 2) | 2h | 15h |

**Total MVP**: ~13 часов (1-2 недели при 1 сессии/день).
**Total с UI**: ~15 часов.

**Изменения effort после Q4/Q11/Q12**:
- T-KEY-04 уменьшился с auto-bootstrap (1h) до explicit binding (1h) — без изменений
- T-KEY-07 увеличился с polling RPC (2.5h) до NetworkVariable-based (3h) — больше boilerplate
- T-KEY-PERSIST добавлен (1.5h)

---

## §6 — Риски

| Риск | Митигация |
|---|---|
| **`InventoryData` breaking change** | Additive-only: `List<InventorySlot>` параллельно с `List<int>` (slot.itemId = int). `GetIdsForType` возвращает List<int> из slot |
| **Scene-placed NetworkObject timing** | `KeyRodInstanceBinding.OnNetworkSpawn` регистрирует instance после `InventoryWorld` готов (Invoke delay 0.5s) |
| **PickupItem не имеет itemType = Key** | После Q11: `KeyRodInstanceBinding` явный — `_keyItemData` в инспекторе, не полагаемся на `_itemData` |
| **200 кораблей** | NetworkVariable broadcast только владельцу (Q4 throttle 2 Hz), payload ~80 bytes |
| **OwnerPlayerId при pickup `from=NONE`** | Q3: `OWNER_NONE = ulong.MaxValue`. TransferInstance проверяет if from == OWNER_NONE → просто ставит owner = to |
| **NetworkObjectId корабля изменился (server restart)** | T-KEY-04: binding хранит GameObject-ссылку, resolved в `OnNetworkSpawn`. T-KEY-PERSIST: при restore lookup по `(ownerPlayerId, itemId)` если NetID mismatch |
| **Persist file corrupted** | T-KEY-PERSIST: try/catch при load, fallback к дефолтам (как `JsonCharacterDataRepository`) |
| **Q4: NetworkVariable overhead при 200 кораблях** | Broadcast только владельцу (N×1 = 200), throttle 2 Hz. ~32 KB/s на максимуме |
| **Q8: pilotCount не нужен в MVP** | Убран из ShipTelemetryState (см. `22_SHIP_TELEMETRY_PLAN.md` §5) |

---

## §7 — Session Log

| Дата | Событие |
|---|---|
| 2026-06-06 | R2-SHIP-KEY-001: первичная реализация (ShipKeySubsystem) |
| 2026-06-06 | R2-META-REQ-001: миграция на MetaRequirement |
| 2026-06-18 | **R2-SHIP-KEY-003** (planned): Unique Key Instance + Ownership + Telemetry (дизайн v1) |
| 2026-06-18 | **R2-SHIP-KEY-003 v2** (planned): Q4 (NetworkVariable) + Q11 (explicit binding) + Q12 (persist) |
| TBD | T-KEY-01..T-KEY-08 — implementation sessions |

---

## §8 — Сводный статус (1 строка)

**M1=📋 M2=📋 M3=📋 M4=📋 M5=📋 (Phase 2)** | **Обновлено:** 2026-06-18 (Q4/Q11/Q12 integrated) | **Next:** T-KEY-01 готов к старту.

---

## Связь с существующими тикетами

| Existing | Что делаем |
|---|---|
| R2-SHIP-KEY-001 (✅ resolved) | ShipKeySubsystem → остаётся как legacy alias, не трогаем |
| R2-META-REQ-001 (✅ resolved) | MetaRequirement → расширяем для ShipOwnership, не ломаем существующее |
| R2-SHIP-KEY-003 (📋 planned, 2026-06-18) | Этот roadmap (v2 после decision integration) |

---

## Что не делаем (out of scope)

- ❌ Крафт ключей на верфи (фаза 2, T-KEY-CRAFT)
- ❌ `isDuplicate` (фаза 2)
- ❌ `accessLevel = Limited/OneTime` (фаза 2)
- ❌ NPC-продажа ключей (фаза 2)
- ❌ Угон / pirate (фаза 2)
- ❌ Salvage / repair (фаза 2)
- ❌ Cargo items breakdown в telemetry DTO (фаза 2, отдельная Cargo UI)
- ❌ Multi-pilot display (фаза 2)
- ❌ Pilot count в telemetry (Q8 — убран, см. `22_SHIP_TELEMETRY_PLAN.md` §5)

---

## Связанные документы

- `00_OVERVIEW.md` — current state
- `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — legacy compat
- `KNOWN_ISSUES.md` — bug history
- `20_UNIQUE_KEY_INSTANCE.md` — концепция (включает Q11/Q12)
- `21_SHIP_OWNERSHIP_MODEL.md` — ownership (включает Q6)
- `22_SHIP_TELEMETRY_PLAN.md` — telemetry NetworkVariable-based (включает Q4/Q6/Q8)
- `24_OPEN_QUESTIONS.md` — все Q1..Q12 resolved
- `99_CHANGELOG.md` — что менялось в документации

---

*Roadmap v1: 2026-06-18. Roadmap v2 (decision integration): 2026-06-18. Агент: Mavis. Шаблон: docs/NPC_quests/08_ROADMAP.md.*