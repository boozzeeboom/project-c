# Contract V2 — Migration Design Note

**Дата:** 2026-06-05
**Автор:** Mavis (этап C2 из `MARKETS_V2_AUDIT_2026-06-05.md`)
**Связь:** `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md` §4 этап 1

---

## 0. Цель

Перевести контрактную подсистему с v1 (legacy `ContractSystem`/`ContractBoardUI`/`ContractTrigger` + UGUI + UIFactory + `PlayerTradeStorage`/`TradeMarketServer`) на v2 (та же архитектура, что у `MarketServer` 2.11.0 + UI Toolkit), **не ломая** существующий пайплайн C1-C8 cleanup.

После миграции:
- `ContractSystem.cs` можно удалить (C1) — его заменит `ContractServer` + `ContractWorld`.
- 6 legacy Contract RPC в `NetworkPlayer.cs:725-815` (C5) можно удалить — заменены парой `ReceiveContractSnapshotTargetRpc` / `ReceiveContractResultTargetRpc` (аналог `ReceiveMarketSnapshotTargetRpc`).
- `ContractBoardUI.cs` (UGUI, 549 строк) можно удалить (C1) — заменён `ContractBoardWindow.cs` + UXML/USS (UI Toolkit).
- `ContractTrigger.cs` (124 строки) перенаправлен на новый `ContractServer` через `ContractClientState.RequestXxxRpc`.
- `PlayerTradeStorage.cs` (использовался `ContractSystem:817-825` для доступа к грузу) — больше не нужен контрактам; удаляется в C1.

---

## 1. Архитектура (target)

```
┌──────────────────────────────────────────────────────────────────┐
│  SERVER  (host or dedicated)                                     │
│                                                                  │
│  [ContractServer] NetworkBehaviour  ← в BootstrapScene          │
│    ├── RPC: RequestListRpc(locationId)                          │
│    ├── RPC: RequestAcceptRpc(contractId)                        │
│    ├── RPC: RequestCompleteRpc(contractId)                      │
│    ├── RPC: RequestFailRpc(contractId)                          │
│    └── RPC: SetTimeMultiplierRpc (опционально, future)         │
│                                                                  │
│  [ContractWorld] POCO singleton  (как TradeWorld)                │
│    ├── Dictionary<string, ContractData> _availableContracts     │
│    ├── Dictionary<ulong, List<string>> _playerContracts         │
│    ├── Dictionary<ulong, ContractDebt> _playerDebts             │
│    ├── Init/Generate contracts per location                     │
│    ├── TryAccept(clientId, contractId) → ContractResult         │
│    ├── TryComplete(clientId, contractId, toLocationId) → result │
│    ├── TryFail(clientId, contractId) → result                   │
│    ├── Tick(deltaTime) — уменьшает таймеры, авто-fail           │
│    └── GetSnapshot(clientId, locationId) → ContractSnapshotDto  │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
                ↓ ClientRpc (SendTo.Owner)
┌──────────────────────────────────────────────────────────────────┐
│  CLIENT                                                         │
│                                                                  │
│  [NetworkPlayer]  — добавлены 2 TargetRpc:                      │
│    ├── ReceiveContractSnapshotTargetRpc(snapshot)               │
│    └── ReceiveContractResultTargetRpc(result)                   │
│                                                                  │
│  [ContractClientState] MonoBehaviour singleton  (DONO)         │
│    ├── ContractSnapshotDto? CurrentSnapshot                      │
│    ├── ContractResultDto? LastResult                             │
│    ├── RequestList(locationId)                                  │
│    ├── RequestAccept(contractId)                                │
│    ├── RequestComplete(contractId)                              │
│    ├── RequestFail(contractId)                                  │
│    ├── OnSnapshotReceived / OnTradeResultReceived               │
│    └── OnSnapshotUpdated / OnContractResult events              │
│                                                                  │
│  [ContractInteractor] static helper (как MarketInteractor)       │
│    └── TryOpenContractBoard() — ищет ContractZone (см. §6)    │
│                                                                  │
│  [ContractBoardWindow] MonoBehaviour + UIDocument (UI Toolkit)   │
│    ├── ContractBoardWindow.uxml / .uss в Resources/UI/         │
│    ├── Подписывается на ContractClientState.OnSnapshotUpdated  │
│    └── Принимает/завершает/сдаёт через ContractClientState     │
│                                                                  │
│  [ContractTrigger] scene-placed MonoBehaviour                    │
│    └── Открывает ContractBoardWindow при E (как сейчас)        │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 2. Файлы — что создаём

### 2.1 Core (server POCO)

**`Assets/_Project/Trade/Scripts/Core/ContractWorld.cs`** (~200 строк)
- Singleton (POCO, не MonoBehaviour, не NetworkBehaviour).
- Создаётся в `ContractServer.OnNetworkSpawn` (на сервере).
- API: `TryAccept(clientId, contractId)`, `TryComplete(clientId, contractId, toLocationId)`, `TryFail(clientId, contractId)`, `Tick(deltaTime)`, `GetSnapshot(clientId, locationId)`.
- Использует `IPlayerDataRepository` для кредитов (как `TradeWorld`).
- Таблица расстояний между 4 локациями (primium/secundus/tertius/quartus) — копия из старого `ContractSystem.cs:107-117`.
- Генерация контрактов — копия `ContractSystem.GenerateContractsForLocation:252-298`.

**`Assets/_Project/Trade/Scripts/Core/ContractDebt.cs`** (~80 строк, новый)
- POCO (НЕ MonoBehaviour). Раньше это был `PlayerDebt : MonoBehaviour` (`Scripts/PlayerDebt.cs`), и `ContractSystem` создавал его на лету (lines 759-784). В v2 долг становится серверной структурой, не вешается на NetworkPlayer.
- Хранит `currentDebt`, `lastDecayTime`, `playerId`.
- `CanAcceptContracts()` — лимит по долгу (None/Warning/Restricted).
- `AddDebt(amount)`, `CheckAndApplyDecay()`.

### 2.2 DTO

**`Assets/_Project/Trade/Scripts/Dto/ContractDto.cs`** (новый, ~80 строк)
```csharp
public struct ContractDto : INetworkSerializable
{
    public string contractId;
    public byte type; // ContractType
    public byte state; // ContractState
    public string itemId;
    public string displayName; // кэшируется при создании (TradeItemDefinition.displayName)
    public int quantity;
    public string fromLocationId;
    public string toLocationId;
    public float reward;
    public float cargoValue;
    public float timeLimit;
    public float timeRemaining;
    public bool isReceiptContract;
    // NetworkSerialize...
}
```

**`Assets/_Project/Trade/Scripts/Dto/ContractSnapshotDto.cs`** (новый, ~50 строк)
```csharp
public struct ContractSnapshotDto : INetworkSerializable
{
    public string locationId;
    public string displayName;
    public ContractDto[] available;     // pending
    public ContractDto[] active;        // assigned to client
    public float debtAmount;
    public int debtLevel; // byte enum
    public bool canAcceptContracts;
    public float marketTimeMultiplier;  // для синхронизации UI-таймера
    public float secondsUntilNextTick;
}
```

**`Assets/_Project/Trade/Scripts/Dto/ContractResultDto.cs`** (новый, ~40 строк)
```csharp
public struct ContractResultDto : INetworkSerializable
{
    public ContractResultCode code;
    public string contractId;
    public bool success;
    public string message;       // локализованное
    public float newCredits;     // обновлённое
    public float newDebt;        // обновлённый долг
    public float reward;         // награда (если complete)
    public ContractDto updatedContract; // для active-контракта (таймер уменьшился)
    public ContractSnapshotDto? newSnapshot; // опционально для re-fetch UI
}
```

**`Assets/_Project/Trade/Scripts/Dto/ContractResultCode.cs`** (новый, ~30 строк)
```csharp
public enum ContractResultCode : byte
{
    Ok = 0,
    NotInZone = 1,
    ContractNotFound = 2,
    ContractNotPending = 3,    // уже принят или истёк
    ContractNotActive = 4,
    ContractNotAssigned = 5,    // чужой контракт
    MaxActiveReached = 6,       // 3 по умолчанию
    TooMuchDebt = 7,            // debt level == Restricted/Bounty
    TimerExpired = 8,
    WrongDestination = 9,      // не в целевой локации
    CargoMissing = 10,         // нет груза на складе/в трюме
    WarehouseFull = 11,         // для Receipt контракта
    ItemNotFound = 12,         // itemId не существует
    InternalError = 99,
}
```

### 2.3 Network (server)

**`Assets/_Project/Trade/Scripts/Network/ContractServer.cs`** (~280 строк)
- NetworkBehaviour (RequireComponent NetworkObject), в BootstrapScene, DontDestroyOnLoad.
- Singleton `Instance`.
- 4 `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]`:
  - `RequestListRpc(locationId)` → `ContractWorld.GetSnapshot()` → `ReceiveContractSnapshotTargetRpc`.
  - `RequestAcceptRpc(contractId)` → `TryAccept` → `ReceiveContractResultTargetRpc` + re-snapshot.
  - `RequestCompleteRpc(contractId)` → `TryComplete` → result + re-snapshot.
  - `RequestFailRpc(contractId)` → `TryFail` → result.
- 2 `[Rpc(SendTo.Owner)]` private:
  - `ReceiveContractSnapshotTargetRpc(ContractSnapshotDto)`.
  - `ReceiveContractResultTargetRpc(ContractResultDto)`.
- `FixedUpdate` на сервере → `ContractWorld.Tick(Time.fixedDeltaTime)` → если у контракта истёк таймер → авто-fail + шлёт result клиенту.
- `MarketZoneRegistry`-аналог: **`ContractZoneRegistry`** (см. §6) — для проверки позиции игрока.

### 2.4 Network — zone registry

**`Assets/_Project/Trade/Scripts/Network/ContractZoneRegistry.cs`** (новый, ~70 строк)
- Статический `Dictionary<string, ContractZone> _zones`.
- `Register(ContractZone)`, `Unregister`, `Get(locationId)`, `LocalPlayerZone` (как у `MarketZoneRegistry`).
- `ContractZone` — scene-placed MonoBehaviour с `locationId`, `tradeRadius`, списком `playersInZone`.

**`Assets/_Project/Trade/Scripts/Network/ContractZone.cs`** (новый, ~150 строк)
- Аналог `MarketZone`, но для NPC-агента НП (доска контрактов).
- Сцена: при `OnTriggerEnter(player.IsOwner)` → регистрирует playerId в `_playersInZone`.
- `ContractBoardWindow` UI рисует «NPC-агент НП — Приму», «Сдать контракты можно здесь» и т.д.

> **Замечание:** в v1 `ContractTrigger` использовался как единственный entry-point. В v2 имеет смысл сразу сделать `ContractZone` (для RPC-валидации позиции) и оставить `ContractTrigger` как scene-marker (только UI hint: «Подойдите к NPC и нажмите E»). Это согласуется с архитектурой `MarketZone`.

### 2.5 Client

**`Assets/_Project/Trade/Scripts/Client/ContractClientState.cs`** (~180 строк)
- Аналог `MarketClientState`.
- Один инстанс на клиентский процесс. Создаётся в `NetworkManagerController.Awake` (FIX 3 паттерн, см. `MARKETS_V2_AUDIT_2026-06-05.md` §1).
- `CurrentSnapshot`, `LastResult`.
- События: `OnSnapshotUpdated`, `OnContractResult`.
- Методы: `RequestList(locationId)`, `RequestAccept(contractId)`, `RequestComplete(contractId)`, `RequestFail(contractId)`.

**`Assets/_Project/Trade/Scripts/Client/ContractInteractor.cs`** (~60 строк)
- `TryOpenContractBoard()` — вызывается из `NetworkPlayer` при нажатии E (после приоритета pickup и market).
- Ищет `ContractZoneRegistry.LocalPlayerZone` (с fallback `FindNearestZone`).
- Шлёт `ContractClientState.RequestList(locationId)`, открывает `ContractBoardWindow`.

### 2.6 UI Toolkit

**`Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uxml`** (~50 строк, новый)
- Стиль `MarketWindow.uxml`:
  - Header: «КОНТРАКТЫ НП — [Location]»
  - Debt label: «ДОЛГ: 0 CR» (red, скрыт если 0)
  - Tabs: «ДОСТУПНЫЕ» / «МОИ КОНТРАКТЫ»
  - ListView: `available-list`, `active-list`
  - Action buttons: «ВЗЯТЬ» / «СДАТЬ» / «ПРОВАЛИТЬ» / «ЗАКРЫТЬ»
  - Message label снизу
- Реюз стилей из `MarketWindow.uss` где возможно (font sizes, colors).

**`Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uss`** (~80 строк, новый)
- Копия основных классов из `MarketWindow.uss`:
  - `.contract-window` (главный контейнер)
  - `.header` (заголовок)
  - `.location-label` (locationId)
  - `.debt-label` (red, hidden by default)
  - `.list-section` (обёртка)
  - `.contract-row` (стиль строки в ListView)
  - `.type-standard` (blue), `.type-urgent` (orange), `.type-receipt` (green) — из `ContractData.GetTypeColor()`
  - `.timer-warn` (yellow), `.timer-danger` (red) — для <30% / <10% времени
  - `.actions` (кнопки внизу)
  - `.message-label` (как в MarketWindow)
- Цвета берём из `Assets/_Project/UI/UITheme.cs` (если есть) или хардкодим как в `MarketWindow.uss`.

**`Assets/_Project/Trade/Scripts/Client/ContractBoardWindow.cs`** (~400 строк, новый)
- `[RequireComponent(typeof(UIDocument))]`.
- Singleton `Instance`.
- `Awake`: грузит UXML/USS из Resources (как `MarketWindow:79-80`).
- `OnEnable`: строит UI, подписывается на `ContractClientState.OnSnapshotUpdated`.
- `HandleSnapshot`: перерисовывает ListView (2 списка: available + active).
- `HandleResult`: показывает message, обновляет snapshot.
- Кнопки: `OnAcceptClicked` → `ContractClientState.RequestAccept(contractId)`, `OnCompleteClicked` → `RequestComplete`, `OnFailClicked` → `RequestFail`, `OnCloseClicked` → `Hide()`.

---

## 3. Изменения в существующих файлах

### 3.1 `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

**Добавить** (рядом с существующими `ReceiveMarketSnapshotTargetRpc:887-892`):

```csharp
[Rpc(SendTo.Owner)]
public void ReceiveContractSnapshotTargetRpc(ContractSnapshotDto snapshot, RpcParams rpcParams = default)
{
    ProjectC.Trade.Client.ContractClientState.Instance?.OnSnapshotReceived(snapshot);
}

[Rpc(SendTo.Owner)]
public void ReceiveContractResultTargetRpc(ContractResultDto result, RpcParams rpcParams = default)
{
    ProjectC.Trade.Client.ContractClientState.Instance?.OnTradeResultReceived(result);
}
```

**НЕ ТРОГАТЬ** пока legacy RPC (`ContractRequestServerRpc:725`, `ContractAcceptServerRpc:741`, `ContractCompleteServerRpc:757`, `ContractFailServerRpc:773`, `ContractListClientRpc:789`, `ContractResultClientRpc:805`) — они нужны для регресса старого `ContractSystem`. Удаляются в C5 ПОСЛЕ успешного C2-теста.

### 3.2 `Assets/_Project/Trade/Scripts/ContractTrigger.cs`

Перенаправить `OpenContractBoard` на новый `ContractClientState` + `ContractBoardWindow`. Оставить scene-marker scene.

**Было** (строки 80-102):
```csharp
public void OpenContractBoard(NetworkPlayer player)
{
    if (market == null) { ... return; }
    if (ContractBoardUI.Instance != null) ContractBoardUI.Instance.OpenBoard(market, player);
    else { var go = new GameObject("[ContractBoardUI]"); ... }
}
```

**Стало:**
```csharp
public void OpenContractBoard(NetworkPlayer player)
{
    // locationId берём из самого GameObject (бывший market.locationId) или поля
    var state = ProjectC.Trade.Client.ContractClientState.Instance;
    if (state == null) return;
    state.RequestList(locationId);
    var window = ContractBoardWindow.Instance;
    if (window != null) window.Show();
}
```

**Новое поле:** `[SerializeField] private string locationId = "primium"` (вместо `public LocationMarket market`).

### 3.3 `Assets/_Project/Trade/Scripts/ContractData.cs`

**Сохранить** как есть (это POCO enum'ы + `[Serializable] ContractData`). `ContractWorld` использует напрямую. Никаких изменений — это контракт данных.

**Микро-чистка** (опционально, не блокирует):
- Поля сериализуются через `ContractDto`, не `ContractData`. `ContractData` остаётся для серверной логики и legacy-сериализации (`SerializeContracts:710-719`).

### 3.4 `Assets/_Project/Trade/Scripts/ContractSystem.cs`

**Не удалять** в этой сессии. В C1 (отдельный тикет) после C2-теста.

**НЕ ТРОГАТЬ** код `ContractSystem` в рамках C2 — он продолжает работать параллельно через legacy RPC. Это даёт нам «подстраховку»: если новая подсистема сломается — откатываемся на старую.

### 3.5 `Assets/_Project/Trade/Editor/TradeSceneSetupTool.cs`

**НЕ ТРОГАТЬ** — он создаёт v1 компоненты (`PlayerTradeStorage`, `TradeUI`). Будет удалён в C1.

### 3.6 `Assets/_Project/Scripts/Core/NetworkManagerController.cs`

**Добавить** в `Awake` (после создания `[MarketClientState]`):

```csharp
// Создаём [ContractClientState] GO автоматически (FIX C2)
if (ProjectC.Trade.Client.ContractClientState.Instance == null)
{
    var go = new GameObject("[ContractClientState]");
    go.AddComponent<ProjectC.Trade.Client.ContractClientState>();
}
```

Это паттерн из FIX 3 (см. `MARKETS_V2_AUDIT_2026-06-05.md` §1 — MarketClientState создаётся так же).

### 3.7 `Assets/_Project/Scenes/BootstrapScene.unity`

**Добавить** через MCP (Unity Editor должен быть запущен):
- GameObject `[ContractServer]` с компонентами `NetworkObject` + `ContractServer`. Transform (0,0,0). Рядом с `[MarketServer]` (line 23644).
- GameObject `[ContractBoardWindow]` с компонентами `UIDocument` (PanelSettings = `Assets/_Project/Trade/Resources/UI/MarketPanelSettings.asset`) + `ContractBoardWindow`. Transform (0,0,0). Рядом с `[MarketWindow]` (line 17087).
- **Перед** добавлением `ContractBoardWindow` сначала создать в Editor `ContractBoardWindow.uxml/uss` ассеты — иначе UIDocument не сможет показать UXML.

> **MVP:** если Editor не запущен, `ContractBoardWindow` создаётся динамически в `ContractInteractor.TryOpenContractBoard()` (как сейчас создаётся `ContractBoardUI`).

---

## 4. Логика миграции (пошагово)

1. **Создаём DTO** (`ContractDto`, `ContractSnapshotDto`, `ContractResultDto`, `ContractResultCode`) — нет side effects, компилируется изолированно.
2. **Создаём `ContractWorld`** (POCO) — копируем таблицу расстояний и генерацию контрактов из старого `ContractSystem.cs:107-298`. Использует `IPlayerDataRepository` (уже есть в `Repository/`).
3. **Создаём `ContractDebt`** (POCO) — копируем логику из `PlayerDebt.cs` (если `PlayerDebt` не используется другим кодом — удалим в C1).
4. **Создаём `ContractZone` + `ContractZoneRegistry`** — копируем структуру `MarketZone` (без shipDockRadius, без MarketState).
5. **Создаём `ContractServer`** (NetworkBehaviour) — `OnNetworkSpawn` создаёт `ContractWorld`, `FixedUpdate` тикает таймеры. 4 RPC + 2 TargetRpc.
6. **Создаём `ContractClientState`** + `ContractInteractor` (helpers).
7. **Создаём UI Toolkit файлы** (`ContractBoardWindow.uxml/uss/cs`).
8. **Патчим `NetworkPlayer`** — добавляем 2 `ReceiveContract*TargetRpc`.
9. **Патчим `NetworkManagerController`** — auto-create `[ContractClientState]`.
10. **Патчим `ContractTrigger`** — перенаправляем на новый API.
11. **(MCP)** Добавляем GO в `BootstrapScene`: `[ContractServer]` + `[ContractBoardWindow]`. Если Editor недоступен — пропускаем, `ContractBoardWindow` создаётся динамически.
12. **Verify:** открыть Unity, дождаться компиляции, `Console → 0 errors`.
13. **Smoke test:** host → подойти к NPC-агенту → E → видим контракты → взять → доставить → сдать → кредиты начислились.

---

## 5. Что НЕ трогаем в этой сессии (C1/C4/C5/C6 — отдельные тикеты)

- ❌ `ContractSystem.cs` (838 строк) — продолжает работать параллельно, удаляется в C1.
- ❌ `ContractBoardUI.cs` (549 строк) — UGUI-версия для регресса, удаляется в C1.
- ❌ `ContractBoardUI.Instance` ссылки в `NetworkPlayer.cs:791, 807` — оставляем, legacy RPC до C5.
- ❌ `PlayerTradeStorage.cs`, `PlayerDebt.cs` — удаляются в C1.
- ❌ `NetworkPlayer.cs:725-815` legacy Contract RPC — удаляются в C5.
- ❌ `NetworkPlayer.cs:640-697` legacy Trade RPC — удаляются в C4 (отдельный тикет, не блокирует C2).
- ❌ `.meta`/`.asmdef` — никогда не создаём.
- ❌ `docs/gdd/GDD_22_Economy_Trading.md` — D1, требует user approval.
- ❌ `docs/Markets/KNOWN_ISSUES.md` — обновляется в D3 после успешного теста.

---

## 6. Открытые вопросы / риски

1. **`ContractBoardWindow` в `BootstrapScene`:** если Editor не запущен — невозможно добавить GO через MCP. Решение: компонент создаётся динамически в `ContractInteractor.TryOpenContractBoard()` (как сейчас `ContractBoardUI`). Тогда `UIDocument` с `PanelSettings` тоже создаётся на лету.
2. **Какой `PanelSettings` использовать?** В проекте уже есть `Assets/_Project/Trade/Resources/UI/MarketPanelSettings.asset` (по аудиту §3 — упоминается в INTEGRATION.md). Реюз: `ContractBoardWindow` грузит тот же `PanelSettings` через `Resources.Load<PanelSettings>("UI/MarketPanelSettings")`.
3. **`PlayerDataStore` ↔ `IPlayerDataRepository`:** старый `ContractSystem.cs:269` использует `TradeMarketServer.GetPlayerCreditsStatic/SetPlayerCreditsStatic` (строки 552, 553, 621, 624). В v2 это `TradeWorld.Instance.Repository.GetCredits/AddCredits`. `ContractWorld` будет ходить в `Repository` напрямую (как `TradeWorld` сейчас).
4. **Debt decay:** старый `PlayerDebt.CheckAndApplyDecay()` вызывается в `ContractSystem.FixedUpdate:666`. Перенесём в `ContractWorld.Tick()`.
5. **Receipt контракт логика:** «товар бесплатно, не доставил = долг ×1.5» — `ContractSystem.cs:603-610` + `HandleFailedContract:598-631`. Перенесём в `ContractWorld.TryComplete` + `ContractWorld.AutoFail` без изменений в формулах.
6. **Тестирование dual-stack:** legacy `ContractSystem` (UGUI) + новая `ContractServer` (UI Toolkit) живут одновременно. Контракты в них — **разные** (legacy имеет свой пул с `Random.Range`, v2 — свой). Это **намеренно** для регресса. После C2-теста и C1-cleanup legacy убирается.
7. **`ContractResultCode` vs `TradeResultCode`:** сделали **отдельный** enum для контрактов, чтобы коды ошибок были специфичны (`MaxActiveReached`, `WrongDestination`, `CargoMissing`). Если в будущем захочется объединить — отдельный refactor.

---

## 7. Verification (Mavis hands over, user runs)

**Сейчас (после создания C2-кода):** Unity Editor закрыт, MCP недоступен, статическая компиляция не проверена автоматически. Ниже — что должен сделать пользователь после запуска Unity.

```powershell
# 1. Открыть Unity Editor → дождаться компиляции
# ОЖИДАЕТСЯ: 0 errors. Warnings допустимы (некоторые legacy поля станут unused).

# 2. Если компиляция ОК — добавить [ContractServer] GameObject в BootstrapScene:
#   • В Hierarchy создать пустой GO "[ContractServer]" в позиции (0,0,0)
#   • Добавить компонент NetworkObject (ProjectC.Netcode.NetworkObject)
#   • Добавить компонент ProjectC.Trade.Network.ContractServer
#   • В инспекторе: оставить tradeDatabase = null (используется дефолтный набор 7 items)
#   • (опционально) Привязать tradeDatabase = Assets/_Project/Trade/Data/TradeItemDatabase.asset

# 3. (опционально) Добавить [ContractBoardWindow] GameObject:
#   • Создать GO "[ContractBoardWindow]" в BootstrapScene
#   • Добавить компонент UIDocument
#   • В UIDocument: Visual Tree Asset = ContractBoardWindow.uxml (из Resources/UI/)
#   • Panel Settings = Assets/_Project/Trade/Resources/UI/MarketPanelSettings.asset
#   • Добавить компонент ProjectC.Trade.Client.ContractBoardWindow
#   • Если GO не создан — ContractInteractor создаст его динамически (с базовым fallback)

# 4. Добавить ContractZone в WorldScene_0_0 (и другие, где есть NPC-агенты):
#   • В нужной точке сцены создать GO "[NPCAgent_Primium]" (или secundus и т.д.)
#   • Добавить SphereCollider (radius=5, isTrigger=true)
#   • Добавить компонент ProjectC.Trade.Network.ContractZone
#   • В инспекторе: locationId = "primium", displayName = "Приму"
#   • Если есть ContractTrigger scene-marker — заменить triggerRadius/locationId в нём
#     (старый ContractTrigger.cs всё ещё работает с locationId fallback)

# 5. Smoke test
# • File → Build Profiles → Play
# • В игре: host → подойти к NPC-агенту у MarketZone_Primium
# • Нажать C → открывается ContractBoardWindow
# • Должно быть: «3 контракта» (Standard + Urgent + Receipt)
# • Выбрать Standard → Enter
# • Должно появиться сообщение «Контракт принят: [Стандарт] mesium x5»
# • В вкладке «МОИ КОНТРАКТЫ» — 1 контракт с таймером 5:00
# • Долететь до MarketZone_Secundus
# • Подойти к NPC-агенту → C → выбрать свой контракт → Enter
# • Должно появиться «Контракт завершён! Награда: X CR»
# • Кредиты в HUD увеличились

# 6. Receipt тест (туториал)
# • Взять контракт [Расписка]
# • БЕЗ доставки подождать 10 минут (или TimeMultiplier=10x)
# • Контракт провалится → долг = cargoValue × 1.5 → DebtLevel = Warning
# • В окне контрактов: «ДОЛГ: X CR» красным

# 7. Regression legacy (опционально)
# • Запустить в build, проверить что старые UGUI ContractBoardUI
#   тоже работают (parallel stack). Если что-то падает — откат на legacy.
```

---

## 8. Файлы для создания/изменения (summary)

**Создать (9 новых файлов):**
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs`
- `Assets/_Project/Trade/Scripts/Core/ContractDebt.cs`
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs`
- `Assets/_Project/Trade/Scripts/Network/ContractZone.cs`
- `Assets/_Project/Trade/Scripts/Network/ContractZoneRegistry.cs`
- `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs`
- `Assets/_Project/Trade/Scripts/Client/ContractInteractor.cs`
- `Assets/_Project/Trade/Scripts/Client/ContractBoardWindow.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractDto.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractSnapshotDto.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractResultDto.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractResultCode.cs`
- `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uxml`
- `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uss`

**Изменить (3 файла):**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — добавить 2 TargetRpc (8 строк)
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — auto-create ContractClientState (5 строк)
- `Assets/_Project/Trade/Scripts/ContractTrigger.cs` — перенаправить на новый API (~15 строк изменений)

**Сцена (через MCP, опционально):**
- `Assets/_Project/Scenes/BootstrapScene.unity` — добавить `[ContractServer]` и `[ContractBoardWindow]` GO

**Не трогать (legacy, удаляются в C1/C5):**
- `ContractSystem.cs` (838 строк) — legacy
- `ContractBoardUI.cs` (549 строк) — legacy UGUI
- `PlayerTradeStorage.cs`, `PlayerDebt.cs` — удаляются в C1
- 6 legacy Contract RPC в `NetworkPlayer.cs:725-815` — удаляются в C5
- 4 legacy Trade RPC в `NetworkPlayer.cs:640-697` — удаляются в C4 (отдельный тикет)

**Документация (после успешного C2-теста):**
- `docs/Markets/KNOWN_ISSUES.md` — пометить C2 RESOLVED
- `docs/Markets/README.md` — добавить C2 в «changelog»
- (опционально) `docs/Markets/TRADE_V2_INTEGRATION.md` — секция «Contract subsystem v2»

---

**Связанные документы:**
- `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md` §2.1 C2, §4 этап 1
- `docs/Markets/ARCHITECTURE.md` — слои v2 (Network/Client/Core/Dto/Config/Service/Repository)
- `docs/Markets/INTEGRATION.md` — связи с остальным проектом
- `docs/Markets/FLOW_TRADE.md` — аналог для контрактов (есть в legacy)
- `docs/Markets/FIXES_HISTORY.md` — что чинили (FIX 3 = `[MarketClientState]` auto-spawn)
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` — главный референс архитектуры
- `Assets/_Project/Trade/Scripts/Client/MarketClientState.cs` — референс client-проекции
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs` — референс E-handler
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml/.uss` — референс UI Toolkit стиля
- `AGENTS.md` — hard rules
