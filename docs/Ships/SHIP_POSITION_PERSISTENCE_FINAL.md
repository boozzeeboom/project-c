# Ship Position Persistence — Итоговый план (Merged)

> **Статус:** План ✅ | Реализация: ⏳
> **Цель:** Сохранение позиций ВСЕХ кораблей (игрок-корабли + NPC) каждые 5 сек, восстановление при перезапуске сервера без дублей и без потери состояния NPC FSM.
> **Server-only.** Клиенты не пишут save, не участвуют в restore.
> **Дата:** 2026-07-19
> **Основание:** Анализ существующего `ship_position_persistence_plan.md` (v1) + ревью фактического кода (NavTick v3.2, NpcShipWorld, ShipController, KeyRodInstanceWorld).

---

## 1. Текущая архитектура (факт, не wishful thinking)

### Ключевое расхождение плана v1 с реальностью

| Аспект | План v1 (ship_position_persistence_plan.md) | Фактический код (июль 2026) |
|--------|----------------------------------------------|------------------------------|
| Движение NPC | Через `ShipController.ApplyServerInput()` → SmoothDamp → AddTorque | **NavTick** — прямой Rigidbody control: `MoveRotation` + `linearVelocity` assignment. Минует ShipController полностью |
| FSM состояние | `NpcShipState.Status` (NpcShipStatus) + `TickNpc()` в `NpcShipWorld` | `NavMode` + `NavTick()` в **NpcShipController** (5 режимов: Docked/Lifting/Yawing/Cruising/Berthing/Avoiding). `NpcShipWorld.TickNpc()` — **мёртвый код** (не вызывается) |
| Dwell таймер | `NpcShipState.StateEnteredAt` + dwellTime check в `TickNpc` | `NpcShipController.DockedSinceTime` + `DwellTime` — живёт **в контроллере** |
| Dwell trade | Не упомянут | `_cargoTradeDone` flag + `RunDwellCargoTrade()` в NpcShipController |
| Расположение нового кода | `PeacefulShip/Network/` | **Неверно** — persistence нужна для **всех** кораблей (player + NPC), не только NPC |
| `_shipPersistentId` | План предлагает | **Не существует** в коде — нужно создавать |

### Игрок-корабли (Player Ships)

| Компонент | Файл |
|-----------|------|
| `ShipController` (NetworkBehaviour) | `Assets/_Project/Scripts/Player/ShipController.cs` |
| `Rigidbody` + `NetworkObject` + `NetworkTransform` | На корне GameObject |
| Scene-placed в `WorldScene_X_Z` | Спавн через `ScenePlacedObjectSpawner` |

- **Идентификация:** `NetworkObjectId` (uint64) — **меняется при каждом перезапуске!**
- **Стабильного ID нет** — `_shipPersistentId` будет добавлен

### NPC-корабли

| Компонент | Файл |
|-----------|------|
| `NpcShipController` (NetworkBehaviour) | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` |
| `NpcShipWorld` (server singleton) | `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipWorld.cs` |
| `NpcShipState` (POCO) | `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipState.cs` |
| `NpcShipSchedule` (ScriptableObject) | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipSchedule.cs` |

- **Runtime идентификация:** `NpcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL` — **тоже меняется при рестарте**
- **FSM живёт в NavTick:** `NavMode` на контроллере, `NpcShipState.Status` — **не используется NavTick-ом**
- **Движение:** NavTick напрямую пишет `Rigidbody.linearVelocity` + `MoveRotation`

### Существующий паттерн персистенции (образец для копирования)

`JsonKeyRodInstanceRepository` → `Application.persistentDataPath/KeyRodInstances.json`:
- `interface IKeyRodInstanceRepository` (LoadAll/SaveAll)
- `[Serializable] KeyRodInstanceSaveData` + `KeyRodInstanceListWrapper`
- `lock (_ioLock)` для thread safety
- **Паттерн копируем 1:1** — включая `JsonUtility`, wrapper, lock

**Файл:** `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` (147 LOC, проверенный).

---

## 2. Ключевые проблемы и решения

### P1: `NetworkObjectId` нестабилен при перезапуске (CRITICAL)
**Условие:** У scene-placed кораблей `NetworkObjectId` генерируется NGO при старте и **не гарантированно совпадает** после рестарта.
**Решение:** Добавить `[SerializeField] private string _shipPersistentId` в `ShipController`. Авто-генерация при первом чтении: `gameObject.scene.name + "/" + gameObject.name`. Дизайнер может переопределить в инспекторе.

### P2: NPC NavMode ≠ NpcShipStatus (архитектурный)
**Условие:** План v1 предполагает восстановление через `NpcShipState.Status` — но NavTick использует `NpcShipController.CurrentMode` (NavMode), а не `NpcShipState.Status`.
**Решение:** `RestoreFromSave()` пишет напрямую в `NpcShipController.CurrentMode`, `DockedSinceTime`, `_scheduleAdvancedAfterDock`, `_cargoTradeDone`, `CruiseTargetPos`, `LiftStartY`, `AssignedPadId`. `NpcShipWorld.RestoreNpcState()` обновляет `ScheduleIndex`, `CurrentRoute`, `LastKnownPosition`.

### P3: Dwell таймер после рестарта
**Условие:** `Time.time` сбрасывается при рестарте сервера. Если NPC был в Docked 30 секунд из 60, после рестарта он должен продолжить с 30s, а не начать заново.
**Решение:** Сохраняем `dockedSinceTimeOffset = Time.time - DockedSinceTime` на момент save. При restore: `DockedSinceTime = Time.time - dockedSinceTimeOffset`. Если offset ≥ DwellTime → NavTick сразу перейдёт в Lifting.

### P4: Дубли при restore (idempotency)
**Условие:** `ScenePlacedObjectSpawner` спавнит корабли при старте. Если мы ещё и из save заспавним — будут дубли.
**Решение:** **Не спавним из save.** Только корректируем уже существующие (найденные через `FindObjectsByType<ShipController>()`). Матчинг по `_shipPersistentId`.

### P5: NPC в Avoiding — транзиентное состояние
**Условие:** Avoiding — временный манёвр расхождения. После рестарта соседних NPC может не быть рядом.
**Решение:** Если сохранённый `navMode == NavMode.Avoiding` → при restore переводим в `NavMode.Cruising`. Система proximity-зон переобнаружит конфликты сама.

### P6: Cargo trade flag после рестарта
**Условие:** `_cargoTradeDone = true` означает «торговля уже выполнена в этом docking-цикле». Если сервер упал после trade, но до lift — `_cargoTradeDone = true` восстанавливается и trade не повторяется.
**Решение:** Сохраняем `_cargoTradeDone` как есть. Если trade не был выполнен (false) — выполнится заново при входе в Docked. Это безопасно (trade идемпотентен по дизайну).

---

## 3. Что сохраняем — DTO

### 3.1 `ShipPositionSaveData`

```csharp
[Serializable]
public class ShipPositionSaveData
{
    // ═══ Identity ═══
    public string shipId;            // _shipPersistentId — стабильный ключ матчинга
    public string sceneName;         // валидация (сцена должна совпадать)
    public bool isNpc;               // true = NPC ship

    // ═══ Transform (всегда) ═══
    public float px, py, pz;         // world position
    public float rx, ry, rz, rw;     // world rotation (quaternion)

    // ═══ Player ship state ═══
    public bool isDocked;

    // ═══ NPC NavTick state ═══
    public int navMode;              // (int)NavMode
    public float dwellTime;          // DwellTime на момент save
    public float dockedSinceTimeOffset; // Time.time - DockedSinceTime (для продолжения dwell)
    public bool scheduleAdvancedAfterDock;
    public bool cargoTradeDone;      // T-CARGO-NPC-01
    public string assignedPadId;     // null если не назначен

    // ═══ NPC flight state ═══
    public float pxCruise, pyCruise, pzCruise;  // CruiseTargetPos
    public float liftStartY;

    // ═══ NPC schedule FSM state (NpcShipState) ═══
    public int scheduleIndex;        // индекс в routes[]
    public string fromLocationId;    // CurrentRoute.fromLocationId
    public string toLocationId;      // CurrentRoute.toLocationId

    // ═══ Meta ═══
    public long savedAtUnix;         // DateTimeOffset.UtcNow.ToUnixTimeSeconds()
}
```

### 3.2 Wrapper для JsonUtility

```csharp
[Serializable]
public class ShipPositionListWrapper
{
    public List<ShipPositionSaveData> ships = new List<ShipPositionSaveData>();
}
```

---

## 4. Архитектура — новые файлы

```
Assets/_Project/Scripts/Core/ShipPosition/
├── ShipPositionSaveData.cs          # [Serializable] DTO + Wrapper
├── ShipPositionRepository.cs        # IShipPositionRepository + JsonShipPositionRepository
└── ShipPositionServer.cs            # MonoBehaviour: periodic save + restore on server
```

**Почему `Core/ShipPosition/`, а не `PeacefulShip/`:** Persistence нужна для ВСЕХ кораблей (player + NPC). Папка `PeacefulShip/` — NPC-специфична. `Core/` — переиспользуема и нейтральна.

**Почему не `Scripts/ShipPosition/`:** В проекте нет папки `Scripts/ShipPosition/`, а `Core/` уже существует и содержит системно-нейтральные компоненты (DayNight, etc.).

### Изменения в существующих файлах

| Файл | Что меняется |
|------|-------------|
| `Player/ShipController.cs` | + `_shipPersistentId` (SerializeField), + `ShipPersistentId` getter (lazy gen) |
| `PeacefulShip/Stations/NpcShipController.cs` | + `GetSaveData()` / `RestoreFromSave()` — сериализация/восстановление NavTick-состояния |
| `PeacefulShip/Core/NpcShipWorld.cs` | + `RestoreNpcState(ulong id, ShipPositionSaveData)` — восстановление schedule/route |

**НЕ меняем:** `PeacefulShip/Network/NpcShipServer.cs` — `ShipPositionServer` создаётся отдельно.

### Жизненный цикл

```
Server start:
  Scenes load → ScenePlacedObjectSpawner spawns ships
  → NpcShipServer.OnNetworkSpawn (2s delay → DiscoverNpcShipsDelayed)
  → ShipPositionServer.OnServerStarted (3s delay → Restore)
     ✓ Все корабли уже зарегистрированы
     ✓ Ищем каждый по _shipPersistentId
     ✓ Применяем позицию + состояние
  → ShipPositionServer.Update (каждые 5s, если IsServer)
     ✓ Проходим по всем ShipController
     ✓ Собираем SaveData
     ✓ Пишем в ShipPositions.json

Server shutdown / crash:
  Последние сохранённые данные в ShipPositions.json
```

---

## 5. Детальная реализация

### 5.1 ShipPositionSaveData.cs (~40 LOC)

**Путь:** `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionSaveData.cs`
**Namespace:** `ProjectC.Core.ShipPosition`

Чистый DTO:
- `[Serializable]` class `ShipPositionSaveData` со всеми полями из §3.1
- `[Serializable]` class `ShipPositionListWrapper`
- Никакой логики

### 5.2 ShipPositionRepository.cs (~80 LOC)

**Путь:** `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionRepository.cs`
**Namespace:** `ProjectC.Core.ShipPosition`

```csharp
public interface IShipPositionRepository
{
    List<ShipPositionSaveData> LoadAll();
    void SaveAll(List<ShipPositionSaveData> ships);
}

public class JsonShipPositionRepository : IShipPositionRepository
{
    private readonly object _ioLock = new();
    private string FilePath => Path.Combine(Application.persistentDataPath, "ShipPositions.json");

    public List<ShipPositionSaveData> LoadAll() { /* паттерн 1:1 KeyRodInstanceRepository */ }
    public void SaveAll(List<ShipPositionSaveData> ships) { /* паттерн 1:1 */ }
}
```

**Точная копия** `JsonKeyRodInstanceRepository`:
- `lock (_ioLock)` — thread safety
- `File.Exists` → null-check → JSON parse
- Ошибки: LogError, возврат пустого списка
- JsonUtility.ToJson/FromJson

### 5.3 ShipController._shipPersistentId (~20 LOC)

**Файл:** `Assets/_Project/Scripts/Player/ShipController.cs`

```csharp
[Header("Persistence (T-PERSIST)")]
[Tooltip("Стабильный идентификатор для сохранения позиции. "
    + "Если пусто — генерируется автоматически при первом чтении: sceneName/gameObject.name.")]
[SerializeField] private string _shipPersistentId = "";

public string ShipPersistentId
{
    get
    {
        if (!string.IsNullOrEmpty(_shipPersistentId))
            return _shipPersistentId!;
        var scene = gameObject.scene;
        _shipPersistentId = $"{scene.name}/{gameObject.name}";
        return _shipPersistentId;
    }
}
```

**NPC side:** `NpcShipController` читает `ShipController.ShipPersistentId` — не дублирует поле.

### 5.4 ShipPositionServer.cs — ядро (~250 LOC)

**Путь:** `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionServer.cs`
**Namespace:** `ProjectC.Core.ShipPosition`

```csharp
public class ShipPositionServer : MonoBehaviour
{
    public static ShipPositionServer Instance { get; private set; }

    [SerializeField] private float saveIntervalSec = 5f;
    [SerializeField] private bool debugMode = true;

    private IShipPositionRepository _repo;
    private float _nextSaveTime;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _repo = new JsonShipPositionRepository();
    }

    void Start()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        if (Instance == this) Instance = null;
    }
```

#### Save (Update, каждые 5 сек)

```csharp
void Update()
{
    if (!IsServerSafe()) return;
    if (Time.time < _nextSaveTime) return;
    _nextSaveTime = Time.time + saveIntervalSec;

    var allShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
    var allData = new List<ShipPositionSaveData>(allShips.Length);

    foreach (var ship in allShips)
    {
        if (!ship.IsSpawned) continue;

        var data = new ShipPositionSaveData
        {
            shipId = ship.ShipPersistentId,
            sceneName = ship.gameObject.scene.name,
            isNpc = false,
            px = ship.transform.position.x,
            py = ship.transform.position.y,
            pz = ship.transform.position.z,
            rx = ship.transform.rotation.x,
            ry = ship.transform.rotation.y,
            rz = ship.transform.rotation.z,
            rw = ship.transform.rotation.w,
            isDocked = ship.IsDocked,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // NPC-specific: дополняем из NpcShipController
        var npc = ship.GetComponent<NpcShipController>();
        if (npc != null)
        {
            data.isNpc = true;
            data.navMode = (int)npc.CurrentMode;
            data.dwellTime = npc.DwellTime;
            data.dockedSinceTimeOffset = (npc.CurrentMode == NpcShipController.NavMode.Docked
                && npc.DockedSinceTime > 0)
                ? Time.time - npc.DockedSinceTime
                : 0f;
            data.scheduleAdvancedAfterDock = npc.ScheduleAdvancedAfterDock;
            data.cargoTradeDone = npc.CargoTradeDone;
            data.assignedPadId = npc.AssignedPadId ?? "";
            data.pxCruise = npc.CruiseTargetPos.x;
            data.pyCruise = npc.CruiseTargetPos.y;
            data.pzCruise = npc.CruiseTargetPos.z;
            data.liftStartY = npc.LiftStartY;

            var state = NpcShipWorld.Instance?.GetNpc(npc.NpcInstanceId);
            if (state != null)
            {
                data.scheduleIndex = state.ScheduleIndex;
                data.fromLocationId = state.CurrentRoute.fromLocationId ?? "";
                data.toLocationId = state.CurrentRoute.toLocationId ?? "";
            }
        }

        allData.Add(data);
    }

    _repo.SaveAll(allData);

    if (debugMode)
        Debug.Log($"[ShipPositionServer] Saved {allData.Count} ships");
}
```

#### Restore (OnServerStarted, с задержкой)

```csharp
void OnServerStarted()
{
    StartCoroutine(RestoreCoroutine());
}

System.Collections.IEnumerator RestoreCoroutine()
{
    // Ждём ScenePlacedObjectSpawner + DiscoverNpcShipsDelayed (2s) + небольшой запас
    yield return new WaitForSeconds(3.5f);

    var savedList = _repo.LoadAll();
    if (savedList.Count == 0)
    {
        Debug.Log("[ShipPositionServer] No saved positions. Skip restore.");
        yield break;
    }

    var allShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
    int restored = 0;

    foreach (var ship in allShips)
    {
        if (!ship.IsSpawned) continue;

        var match = savedList.Find(s => s.shipId == ship.ShipPersistentId);
        if (match == null)
        {
            if (debugMode)
                Debug.Log($"[ShipPositionServer] No save for {ship.ShipPersistentId} — keeping scene position");
            continue;
        }

        ApplyRestore(ship, match);
        restored++;
    }

    Debug.Log($"[ShipPositionServer] Restored {restored}/{savedList.Count} ships from save");
}

void ApplyRestore(ShipController ship, ShipPositionSaveData data)
{
    var rb = ship.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.position = new Vector3(data.px, data.py, data.pz);
        rb.rotation = new Quaternion(data.rx, data.ry, data.rz, data.rw);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // restore docking state
    if (data.isDocked && !ship.IsDocked)
        ship.EnterDocked();
    else if (!data.isDocked && ship.IsDocked)
        ship.ExitDocked();

    if (!data.isNpc) return; // всё, player ship готов

    var npc = ship.GetComponent<NpcShipController>();
    if (npc != null)
        npc.RestoreFromSave(data);
}
```

### 5.5 NpcShipController: GetSaveData / RestoreFromSave (~100 LOC)

**Файл:** `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs`

#### GetSaveData вызывается из ShipPositionServer.Save

```csharp
// T-PERSIST: заполняет ShipPositionSaveData NPC-специфичными полями
// Вызывается из ShipPositionServer.Update для каждого NPC-корабля
public void FillSaveData(ShipPositionSaveData data)
{
    data.isNpc = true;
    data.navMode = (int)CurrentMode;
    data.dwellTime = DwellTime;
    data.dockedSinceTimeOffset = (CurrentMode == NavMode.Docked && DockedSinceTime > 0)
        ? Time.time - DockedSinceTime : 0f;
    data.scheduleAdvancedAfterDock = _scheduleAdvancedAfterDock;
    data.cargoTradeDone = _cargoTradeDone;
    data.assignedPadId = AssignedPadId ?? "";
    data.pxCruise = CruiseTargetPos.x;
    data.pyCruise = CruiseTargetPos.y;
    data.pzCruise = CruiseTargetPos.z;
    data.liftStartY = LiftStartY;

    var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
    if (state != null)
    {
        data.scheduleIndex = state.ScheduleIndex;
        data.fromLocationId = state.CurrentRoute.fromLocationId ?? "";
        data.toLocationId = state.CurrentRoute.toLocationId ?? "";
    }
}
```

#### RestoreFromSave (Server-only)

```csharp
/// <summary>Восстановление NavTick-состояния после перезапуска сервера.</summary>
public void RestoreFromSave(ShipPositionSaveData data)
{
    if (!IsServer) return;

    // ── NavMode (критично: EnterDocked ставит kinematic) ──
    NavMode savedMode = (NavMode)data.navMode;

    // avoiding → transient → fallback to cruising
    if (savedMode == NavMode.Avoiding)
        savedMode = NavMode.Cruising;

    DwellTime = data.dwellTime > 0 ? data.dwellTime : 60f;
    _scheduleAdvancedAfterDock = data.scheduleAdvancedAfterDock;
    _cargoTradeDone = data.cargoTradeDone;
    AssignedPadId = string.IsNullOrEmpty(data.assignedPadId) ? null : data.assignedPadId;
    CruiseTargetPos = new Vector3(data.pxCruise, data.pyCruise, data.pzCruise);
    LiftStartY = data.liftStartY;

    var ship = GetComponent<ShipController>();
    var rb = GetComponent<Rigidbody>();

    // Восстанавливаем режим
    switch (savedMode)
    {
        case NavMode.Docked:
            CurrentMode = NavMode.Docked;
            DockedSinceTime = Time.time - Mathf.Min(data.dockedSinceTimeOffset, DwellTime * 0.9f);
            if (rb != null) rb.isKinematic = true;
            if (!ship.IsDocked) ship.EnterDocked();
            break;

        case NavMode.Lifting:
            CurrentMode = NavMode.Lifting;
            if (rb != null) rb.isKinematic = false;
            if (ship.IsDocked) ship.ExitDocked();
            break;

        case NavMode.Yawing:
        case NavMode.Cruising:
            CurrentMode = savedMode;
            if (rb != null) rb.isKinematic = false;
            if (ship.IsDocked) ship.ExitDocked();
            break;

        case NavMode.Berthing:
            CurrentMode = NavMode.Berthing;
            if (rb != null) rb.isKinematic = false;
            if (ship.IsDocked) ship.ExitDocked();
            // Если пад назначен и мы на дистанции касания — док сработает на первом NavTick
            break;
    }

    // Восстановить NpcShipState
    if (NpcShipWorld.Instance != null)
        NpcShipWorld.Instance.RestoreNpcState(npcInstanceId, data);

    if (debugMode)
        Debug.Log($"[NpcShipController:{gameObject.name}] RestoreFromSave mode={savedMode} " +
                  $"idx={data.scheduleIndex} docked={ship.IsDocked}");
}
```

### 5.6 NpcShipWorld.RestoreNpcState (~30 LOC)

**Файл:** `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipWorld.cs`

```csharp
/// <summary>T-PERSIST: восстановить FSM-состояние NPC из сохранённых данных.</summary>
public void RestoreNpcState(ulong npcInstanceId, ShipPositionSaveData data)
{
    if (!_npcByInstanceId.TryGetValue(npcInstanceId, out var state)) return;
    if (!_scheduleByNpcInstanceId.TryGetValue(npcInstanceId, out var schedule)) return;

    state.ScheduleIndex = data.scheduleIndex;
    state.LastKnownPosition = new Vector3(data.px, data.py, data.pz);
    state.StateEnteredAt = Time.time; // сервер перезапущен — таймер состояния сброшен

    // Восстановить CurrentRoute из schedule по сохранённому индексу
    if (schedule.routes != null && schedule.routes.Length > 0
        && data.scheduleIndex >= 0 && data.scheduleIndex < schedule.routes.Length)
    {
        state.CurrentRoute = schedule.routes[data.scheduleIndex];
    }

    // RoundTrip reverse detection: если индекс нечётный и scheduleType == RoundTrip с одним route
    // → CurrentRoute уже правильный (NavTick сам разберётся при AdvanceSchedule в Docked)
    // (см. NpcShipWorld.AdvanceScheduleIndex — RoundTrip с 1 route)

    Debug.Log($"[NpcShipWorld] RestoreNpcState id={npcInstanceId:X} idx={state.ScheduleIndex} " +
              $"route={state.CurrentRoute.fromLocationId}→{state.CurrentRoute.toLocationId}");
}
```

---

## 6. Необходимые изменения в существующих файлах

### ShipController.cs (+25 LOC)

| Добавить | Где |
|----------|-----|
| `[SerializeField] private string _shipPersistentId = "";` | Поле (Header "Persistence") |
| `public string ShipPersistentId { get; }` | Auto-generate getter |
| Импорт: не требуется (namespace уже есть) | — |

### NpcShipController.cs (+80 LOC)

| Добавить | Где |
|----------|-----|
| `public void FillSaveData(ShipPositionSaveData data)` | Public метод |
| `public void RestoreFromSave(ShipPositionSaveData data)` | Public метод |
| `internal bool ScheduleAdvancedAfterDock => _scheduleAdvancedAfterDock;` | Getter (private→internal) |
| `internal bool CargoTradeDone => _cargoTradeDone;` | Getter (private→internal) |
| Импорт: `ProjectC.Core.ShipPosition` | Верх файла |

### NpcShipWorld.cs (+30 LOC)

| Добавить | Где |
|----------|-----|
| `public void RestoreNpcState(ulong id, ShipPositionSaveData data)` | Public метод |

---

## 7. Порядок выполнения и оценки

| Этап | Тикет | Файлы | LOC | Часы |
|------|-------|-------|-----|------|
| **1** | T-PERSIST-DTO | `ShipPositionSaveData.cs` (новый) | 40 | 0.25 |
| **2** | T-PERSIST-REPO | `ShipPositionRepository.cs` (новый) | 80 | 0.5 |
| **3** | T-PERSIST-ID | `Player/ShipController.cs` (+_shipPersistentId) | 25 | 0.25 |
| **4** | T-PERSIST-NPC-FILL | `NpcShipController.cs` (+FillSaveData/RestoreFromSave) | 80 | 0.75 |
| **5** | T-PERSIST-WORLD | `NpcShipWorld.cs` (+RestoreNpcState) | 30 | 0.33 |
| **6** | T-PERSIST-SERVER | `ShipPositionServer.cs` (новый) | 250 | 1.5 |
| **7** | T-PERSIST-INTEGRATE | Создание ShipPositionServer в BootstrapScene | 5 | 0.1 |
| | **Итого** | **4 новых, 3 изменённых** | **~510** | **~3.7** |

### Зависимости

```
T-PERSIST-DTO (1)
    ↓
T-PERSIST-REPO (2) ───┐
T-PERSIST-ID   (3) ───┤
    ↓                 │
T-PERSIST-NPC-FILL    │
(4) ←─────────────────┘
    ↓
T-PERSIST-WORLD (5)
    ↓
T-PERSIST-SERVER (6)
    ↓
T-PERSIST-INTEGRATE (7)
```

---

## 8. Процесс инициализации (chronology)

```
Time 0.0s  NetworkManager.StartHost()
           → ScenePlacedObjectSpawner.SpawnInAllLoadedScenes()
             → ShipController.OnNetworkSpawn (per-ship)
               → _shipPersistentId auto-gen (если пусто)
             → NpcShipController.OnNetworkSpawn (per-NPC)
               → npcInstanceId = NetworkObjectId | 0x8000...
               → ship.EnableNpcPilot(true)
               → NpcShipZoneRegistry.Register(this)
               → NpcShipWorld.Instance?.RegisterNpc(...) -- может быть null

Time ~0.1s NpcShipServer.OnNetworkSpawn
           → NpcShipWorld.CreateAndInitialize()
           → StartCoroutine(DiscoverNpcShipsDelayed())  -- 2s

Time ~2.1s DiscoverNpcShipsDelayed
           → FindObjectsByType<NpcShipController>
           → NpcShipWorld.RegisterNpc() для каждого
           → Все NPC зарегистрированы

Time ~3.5s ShipPositionServer.RestoreCoroutine
           → _repo.LoadAll()
           → FindObjectsByType<ShipController>
           → Match по ShipPersistentId
           → ApplyRestore() для каждого найденного
             → Rigidbody.MovePosition/MoveRotation
             → NpcController.RestoreFromSave() для NPC
             → NpcShipWorld.RestoreNpcState()
           → Debug.Log("Restored N ships")
```

**Почему 3.5s, а не 3s:** Запас 0.5s на то, что `DiscoverNpcShipsDelayed` может выполниться не ровно через 2s (есть корутинный overhead). Конкретное значение можно вынести в `[SerializeField] _restoreDelaySec = 3.5f`.

---

## 9. Валидация (smoke test checklist)

| # | Проверка | Ожидание |
|---|----------|----------|
| 1 | Первый старт Host | Console: `[ShipPositionServer] No saved positions. Skip restore.` |
| 2 | Через 6+ секунд | Файл `ShipPositions.json` создан в `Application.persistentDataPath` |
| 3 | В файле есть данные | N записей (все NPC + все player-корабли в сцене) |
| 4 | Остановить → перезапустить | Console: `[ShipPositionServer] Restored N/M ships from save` |
| 5 | Корабли на тех же позициях | Визуально: NPC и player-корабли на тех же координатах |
| 6 | NPC Docked → таймер продолжается | DwellTime не начинается заново (если offset < dwellTime) |
| 7 | NPC Cruising → летит к цели | CruiseTargetPos восстановлен, NavTick продолжает полёт |
| 8 | NPC Berthing → завершает стыковку | Position рядом с pad → первый NavTick завершает док |
| 9 | 0 errors в консоли | Нет NRE, нет MissingReference, нет дублей |

---

## 10. Анализ расхождений с планом v1

| Аспект | План v1 | Итоговый план | Причина |
|--------|---------|---------------|---------|
| **Расположение** | `PeacefulShip/Network/` | `Core/ShipPosition/` | Persistence касается всех кораблей, не только NPC |
| **Сохранение позиции** | Через `GetSaveData()` на контроллере | `FillSaveData()` вызывается из `ShipPositionServer` (централизованно) | Упрощает сбор данных: один цикл `FindObjectsByType<ShipController>` |
| **NPC FSM состояние** | `NpcShipState.Status` (NpcShipStatus) + старая `TickNpc` | `NpcShipController.NavMode` + NavTick | Фактический код использует NavTick, не старую FSM |
| **Dwell таймер** | `NpcShipState.StateEnteredAt` | `NpcShipController.DockedSinceTime` + offset | NavTick хранит время в контроллере |
| **Cargo trade** | Не упомянут | `_cargoTradeDone` сохраняется | T-CARGO-NPC-01 уже реализован |
| **Restore порядок** | `RestoreAfterDelay()` 3s | `RestoreCoroutine()` 3.5s | Запас на `DiscoverNpcShipsDelayed` |
| **Thread safety** | `lock` на репозиторий | `lock` на репозиторий (паттерн KeyRodInstance) | Без изменений |
| **Ship** `_shipPersistentId` | `sceneName/gameObject.name` | То же + lazy init getter | Без изменений |
| **Восстановление Avoiding** | Не рассмотрено | → Cruising fallback | Avoiding — транзиентное состояние |

---

## 11. Что НЕ делаем (out of scope)

- ❌ Клиентская синхронизация сохранений (чисто сервер)
- ❌ Сохранение cargo/inventory кораблей (есть отдельная система ShipCargoServer + TradeWorld)
- ❌ Сохранение пассажиров на борту
- ❌ Debounce/diff-save оптимизация (сейчас 10-20 кораблей, полный dump ok)
- ❌ Сохранение позиций игроков на кораблях (Player persistence — отдельная система)
- ❌ Создание `ShipPositionServer` как scene-placed объекта в BootstrapScene через MCP — будет сделано на этапе T-PERSIST-INTEGRATE
- ❌ `.meta` / `.asmdef` файлы — не создаём (Unity сгенерирует при импорте)

---

## 12. Риски

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| `FindObjectsByType<ShipController>` в Update каждые 5s — дешёво? | Низкая | 10-20 кораблей, 5s интервал — overhead ~0.01ms |
| JSON-файл повреждён (краш во время записи) | Низкая | `File.ReadAllText` + try/catch → пустой список |
| `Time.time` не совпадает на dedicated server | Низкая | Используется только для relative offset, не абсолют |
| Дубли после `ScenePlacedObjectSpawner` + restore | Низкая | Матчинг по `_shipPersistentId`, не создаём новые объекты |
| NPC в `NavMode.Docked` с `_cargoTradeDone=false` после рестарта → повторный trade | Низкая | Trade идемпотентен (проверка в `NpcCargoService.RunDwellTrade`) |
| Переименование GameObject в сцене → ID сломается | Средняя | Дизайнер может задать `_shipPersistentId` вручную в инспекторе |

---

**Создано:** 2026-07-19 на основе анализа кода (NpcShipController NavTick, NpcShipWorld, ShipController, KeyRodInstanceWorld) и плана v1 (`docs/Ships/ship_position_persistence_plan.md`).
**Следующий шаг:** Запрос на реализацию T-PERSIST-DTO.
