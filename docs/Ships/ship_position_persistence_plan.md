# Ship Position Persistence — План реализации

> **Статус:** План ✅ | Реализация: ⏳
> **Цель:** Сохранение позиций всех кораблей (игрок-корабли + NPC) каждые 5 сек, восстановление при перезапуске сервера без дублей.
> **Scope:** Server-only. Клиенты не участвуют в сохранении/восстановлении.

---

## 1. Текущая архитектура (контекст)

### Игрок-корабли (Player Ships)
| Компонент | Файл |
|-----------|------|
| `ShipController` (NetworkBehaviour) | `Assets/_Project/Scripts/Player/ShipController.cs` |
| `Rigidbody` + `NetworkObject` + `NetworkTransform` | На корне GameObject |
| Scene-placed в `WorldScene_X_Z` | Спавн через `ScenePlacedObjectSpawner` |

- **Идентификация:** `NetworkObjectId` (uint64) — **меняется при каждом перезапуске!**
- **Состояние:** `IsDocked` (NetworkVariable), `IsEngineRunning`, позиция через `NetworkTransform`
- **Пилоты:** `_pilots` HashSet (clientId) + `_hasNpcPilot` flag
- **Нет стабильного идентификатора** → нужно добавить

### NPC-корабли
| Компонент | Файл |
|-----------|------|
| `NpcShipController` (NetworkBehaviour) | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` |
| `NpcShipWorld` (server singleton) | `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipWorld.cs` |
| `NpcShipState` (POCO) | `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipState.cs` |
| `NpcShipSchedule` (ScriptableObject) | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipSchedule.cs` |

- **Идентификация:** `NpcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL` — **тоже меняется!**
- **FSM:** `NpcShipStatus` (12 состояний: Idle→Departing→InTransit→Approaching→Docking→Docked→Loading→Undocking→Done + Holding/Diverting)
- **Движение:** `NavTick()` — прямой Rigidbody control (`MoveRotation` + `linearVelocity`), минует `ApplyServerInput`
- **NavMode:** `Docked, Lifting, Yawing, Cruising, Berthing, Avoiding`

### Существующий паттерн персистенции
- `JsonKeyRodInstanceRepository` → `Application.persistentDataPath/KeyRodInstances.json`
- `interface IRepository` + `[Serializable] DTO` + `JsonUtility`
- Паттерн копируем.

---

## 2. Ключевые проблемы (P1-P5)

| # | Проблема | Решение |
|---|----------|---------|
| **P1** | `NetworkObjectId` нестабилен | Добавить `[SerializeField] _shipPersistentId` (string) в `ShipController`. Авто-генерация при первом Awake: `$"{sceneName}/{go.name}"`. Дизайнер может переопределить. |
| **P2** | Нет стабильного ID у игрок-кораблей | `_shipPersistentId` решает. Для NPC — тот же механизм, `NpcInstanceId` остаётся для runtime. |
| **P3** | Корабли спавнятся на исходных позициях из сцены | После `Spawn()` и `DiscoverNpcShipsDelayed()`: прочитать сохранённые позиции, `Rigidbody.MovePosition/MoveRotation`. |
| **P4** | Дубли FSM при восстановлении NPC | Не пересоздавать `NpcShipState` — только **обновить** существующий (созданный в `OnNetworkSpawn`), применив сохранённые `Status`, `ScheduleIndex`, `CurrentRoute`. |
| **P5** | Неполное восстановление NPC | Сохранять: `NavMode`, `DwellTime`, `DockedSinceTime`, `AssignedPadId`, `CruiseTargetPos`, `LiftStartY`, `_scheduleAdvancedAfterDock`, `_cargoTradeDone`. |

---

## 3. Что сохраняем (DTO)

```csharp
[Serializable]
public class ShipPositionSaveData
{
    // ── Identity ──
    public string shipId;            // _shipPersistentId
    public string sceneName;         // для валидации
    public bool isNpc;

    // ── Transform ──
    public float px, py, pz;         // position
    public float rx, ry, rz, rw;     // rotation (quaternion)

    // ── Player ship state ──
    public bool isDocked;

    // ── NPC ship state ──
    public int npcFsmStatus;         // (int)NpcShipStatus
    public int scheduleIndex;
    public string fromLocationId;
    public string toLocationId;
    public int navMode;              // (int)NpcShipController.NavMode
    public float dwellTime;
    public bool scheduleAdvancedAfterDock;
    public bool cargoTradeDone;
    public string assignedPadId;
    public float dockedSinceTimeOffset; // сколько секунд уже в Docked (чтобы после restore не начинать dwell заново)
    public float pxCruise, pyCruise, pzCruise; // CruiseTargetPos
    public float liftStartY;

    // ── Meta ──
    public long savedAtUnix;
}
```

---

## 4. Архитектура новых файлов

```
Assets/_Project/Scripts/PeacefulShip/Network/
├── ShipPositionSaveData.cs       # DTO [Serializable]
├── ShipPositionRepository.cs     # IShipPositionRepository + JsonShipPositionRepository
└── ShipPositionServer.cs         # MonoBehaviour: периодическое сохранение + восстановление
```

**Модификации существующих файлов:**
| Файл | Что меняется |
|------|-------------|
| `Player/ShipController.cs` | + `_shipPersistentId` (SerializeField), + `ShipPersistentId` getter |
| `PeacefulShip/Stations/NpcShipController.cs` | + `GetSaveData()` / `RestoreFromSave()` методы, expose `NavMode`, `DwellTime`, `CruiseTargetPos`, `LiftStartY`, `_scheduleAdvancedAfterDock`, `_cargoTradeDone` |
| `PeacefulShip/Core/NpcShipWorld.cs` | + `RestoreNpcState(ulong id, ShipPositionSaveData)` |
| `PeacefulShip/Network/NpcShipServer.cs` | + создание `ShipPositionServer` при `OnNetworkSpawn` |

---

## 5. Пошаговый план реализации (6 тикетов)

### T-PERSIST-01: `ShipPositionSaveData` DTO (15 мин, ~40 LOC)
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Network/ShipPositionSaveData.cs`

- `[Serializable]` класс с полями из секции 3
- `[Serializable]` wrapper `ShipPositionListWrapper { public List<ShipPositionSaveData> ships; }` для `JsonUtility`
- Без методов — чистый DTO

### T-PERSIST-02: `ShipPositionRepository` (20 мин, ~60 LOC)
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Network/ShipPositionRepository.cs`

- `interface IShipPositionRepository { List<ShipPositionSaveData> LoadAll(); void SaveAll(...); }`
- `JsonShipPositionRepository : IShipPositionRepository`
- Файл: `Application.persistentDataPath/ShipPositions.json`
- Паттерн: точная копия `JsonKeyRodInstanceRepository`
- `lock (_ioLock)` для thread safety
- Обработка ошибок: если файл не найден → пустой список

### T-PERSIST-03: Стабильный `ShipPersistentId` (15 мин, ~20 LOC)
**Файл:** `Assets/_Project/Scripts/Player/ShipController.cs`

- Добавить `[SerializeField] private string _shipPersistentId = "";`
- Добавить public getter:
  ```csharp
  public string ShipPersistentId
  {
      get
      {
          if (!string.IsNullOrEmpty(_shipPersistentId))
              return _shipPersistentId;
          // Auto-generate
          var scene = gameObject.scene;
          _shipPersistentId = $"{scene.name}/{gameObject.name}";
          return _shipPersistentId;
      }
  }
  ```
- Для NPC: `NpcShipController` использует тот же `ShipController.ShipPersistentId`

### T-PERSIST-04: `ShipPositionServer` — ядро (90 мин, ~200 LOC)
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Network/ShipPositionServer.cs`

**Инициализация:**
```csharp
public class ShipPositionServer : MonoBehaviour
{
    public static ShipPositionServer Instance { get; private set; }
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
}
```

**Сохранение (каждые 5 сек):**
```csharp
void Update()
{
    if (!IsServerActive()) return;
    if (Time.time < _nextSaveTime) return;
    _nextSaveTime = Time.time + 5f;
    
    var allData = new List<ShipPositionSaveData>();
    foreach (var shipController in FindObjectsByType<ShipController>(FindObjectsSortMode.None))
    {
        if (!shipController.IsSpawned) continue;
        var npc = shipController.GetComponent<NpcShipController>();
        var data = (npc != null) 
            ? npc.GetSaveData() 
            : GetPlayerShipData(shipController);
        allData.Add(data);
    }
    _repo.SaveAll(allData);
}
```

**Восстановление при старте сервера:**
```csharp
void OnServerStarted()
{
    StartCoroutine(RestoreAfterDelay());
}

IEnumerator RestoreAfterDelay()
{
    yield return new WaitForSeconds(3f); // ждём ScenePlacedObjectSpawner + DiscoverNpcShipsDelayed
    
    var saved = _repo.LoadAll();
    if (saved.Count == 0) { Debug.Log("[ShipPositionServer] No saved positions found."); yield break; }
    
    foreach (var shipController in FindObjectsByType<ShipController>(FindObjectsSortMode.None))
    {
        if (!shipController.IsSpawned) continue;
        var shipId = shipController.ShipPersistentId;
        var match = saved.Find(s => s.shipId == shipId);
        if (match == null) continue;
        
        ApplyRestore(shipController, match);
    }
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
    
    var npc = ship.GetComponent<NpcShipController>();
    if (npc != null && data.isNpc)
    {
        npc.RestoreFromSave(data);
    }
}
```

### T-PERSIST-05: `NpcShipController` — GetSaveData / RestoreFromSave (45 мин, ~120 LOC)
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs`

**GetSaveData (сбор состояния):**
```csharp
public ShipPositionSaveData GetSaveData()
{
    var ship = GetComponent<ShipController>();
    var state = NpcShipWorld.Instance?.GetNpc(npcInstanceId);
    var t = transform;
    return new ShipPositionSaveData
    {
        shipId = ship.ShipPersistentId,
        sceneName = gameObject.scene.name,
        isNpc = true,
        px = t.position.x, py = t.position.y, pz = t.position.z,
        rx = t.rotation.x, ry = t.rotation.y, rz = t.rotation.z, rw = t.rotation.w,
        isDocked = ship.IsDocked,
        npcFsmStatus = state != null ? (int)state.Status : 0,
        scheduleIndex = state?.ScheduleIndex ?? 0,
        fromLocationId = state?.CurrentRoute.fromLocationId ?? "",
        toLocationId = state?.CurrentRoute.toLocationId ?? "",
        navMode = (int)CurrentMode,
        dwellTime = DwellTime,
        scheduleAdvancedAfterDock = _scheduleAdvancedAfterDock,
        cargoTradeDone = _cargoTradeDone,
        assignedPadId = AssignedPadId ?? "",
        dockedSinceTimeOffset = (CurrentMode == NavMode.Docked && DockedSinceTime > 0) 
            ? Time.time - DockedSinceTime : 0f,
        pxCruise = CruiseTargetPos.x, pyCruise = CruiseTargetPos.y, pzCruise = CruiseTargetPos.z,
        liftStartY = LiftStartY,
        savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
}
```

**RestoreFromSave (восстановление):**
```csharp
public void RestoreFromSave(ShipPositionSaveData data)
{
    var rb = GetComponent<Rigidbody>();
    
    // Восстановить NavMode — это критично, должно быть ДО position/rotation
    // потому что EnterDocked() ставит kinematic
    NavMode savedMode = (NavMode)data.navMode;
    DwellTime = data.dwellTime;
    _scheduleAdvancedAfterDock = data.scheduleAdvancedAfterDock;
    _cargoTradeDone = data.cargoTradeDone;
    AssignedPadId = string.IsNullOrEmpty(data.assignedPadId) ? null : data.assignedPadId;
    CruiseTargetPos = new Vector3(data.pxCruise, data.pyCruise, data.pzCruise);
    LiftStartY = data.liftStartY;
    
    var ship = GetComponent<ShipController>();
    
    switch (savedMode)
    {
        case NavMode.Docked:
            ship.EnterDocked();
            CurrentMode = NavMode.Docked;
            DockedSinceTime = Time.time - Mathf.Min(data.dockedSinceTimeOffset, DwellTime * 0.9f);
            break;
        case NavMode.Lifting:
            CurrentMode = NavMode.Lifting;
            if (rb != null) rb.isKinematic = false;
            ship.ExitDocked();
            StartAntiGravityBoost();
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
            break;
        case NavMode.Avoiding:
            CurrentMode = NavMode.Cruising; // transient → safe fallback
            if (rb != null) rb.isKinematic = false;
            if (ship.IsDocked) ship.ExitDocked();
            break;
    }
    
    // Восстановить NpcShipState в NpcShipWorld
    NpcShipWorld.Instance?.RestoreNpcState(npcInstanceId, data);
}
```

**Нужно также сделать приватные поля доступными (internal или public getter):**
- `_scheduleAdvancedAfterDock` → `internal` или public getter
- `_cargoTradeDone` → `internal` или public getter

### T-PERSIST-06: `NpcShipWorld.RestoreNpcState` (20 мин, ~40 LOC)
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipWorld.cs`

```csharp
public void RestoreNpcState(ulong npcInstanceId, ShipPositionSaveData data)
{
    if (!_npcByInstanceId.TryGetValue(npcInstanceId, out var state)) return;
    if (!_scheduleByNpcInstanceId.TryGetValue(npcInstanceId, out var schedule)) return;
    
    state.Status = (NpcShipStatus)data.npcFsmStatus;
    state.ScheduleIndex = data.scheduleIndex;
    state.StateEnteredAt = Time.time; // сервер перезапущен — сбрасываем таймер
    state.LastKnownPosition = new Vector3(data.px, data.py, data.pz);
    
    // Восстановить CurrentRoute из schedule
    if (schedule.routes != null && schedule.routes.Length > 0 
        && data.scheduleIndex >= 0 && data.scheduleIndex < schedule.routes.Length)
    {
        state.CurrentRoute = schedule.routes[data.scheduleIndex];
    }
    // Если был reverse (RoundTrip с нечётным индексом):
    // NavTick сам разберётся при следующем AdvanceSchedule
    
    Debug.Log($"[NpcShipWorld] RestoreNpcState id={npcInstanceId:X} status={state.Status} idx={state.ScheduleIndex}");
}
```

### T-PERSIST-07: Интеграция — создание `ShipPositionServer` (10 мин, ~5 LOC)
**Файл:** `Assets/_Project/Scripts/PeacefulShip/Network/NpcShipServer.cs`

В `OnNetworkSpawn()` (после создания NpcShipWorld, строка ~51):

```csharp
// T-PERSIST-07: создаём сервер сохранения позиций
var persistGo = new GameObject("[ShipPositionServer]");
DontDestroyOnLoad(persistGo);
persistGo.AddComponent<ShipPositionServer>();
```

---

## 6. Порядок выполнения и зависимости

```
T-PERSIST-01 (DTO)
    ↓
T-PERSIST-03 (ShipPersistentId) ──┐
    ↓                              │
T-PERSIST-02 (Repository)          │
    ↓                              │
T-PERSIST-06 (NpcShipWorld.Restore)│
    ↓                              │
T-PERSIST-05 (NpcShipController) ──┘
    ↓
T-PERSIST-04 (ShipPositionServer)
    ↓
T-PERSIST-07 (NpcShipServer integration)
```

---

## 7. Оценка времени

| Тикет | Часов | LOC | Новых файлов |
|-------|-------|-----|-------------|
| T-PERSIST-01 | 0.25 | 40 | 1 |
| T-PERSIST-02 | 0.33 | 60 | 1 |
| T-PERSIST-03 | 0.25 | 20 | 0 |
| T-PERSIST-04 | 1.5 | 200 | 1 |
| T-PERSIST-05 | 0.75 | 120 | 0 |
| T-PERSIST-06 | 0.33 | 40 | 0 |
| T-PERSIST-07 | 0.17 | 5 | 0 |
| **Итого** | **~3.6** | **~485** | **3** |

---

## 8. Валидация (smoke test)

1. ✅ Запустить Host → в консоли `[ShipPositionServer] No saved positions found.` (первый запуск)
2. ✅ Через 5+ секунд → файл `ShipPositions.json` создан в `persistentDataPath`
3. ✅ Остановить сервер → перезапустить → консоль: `[ShipPositionServer] Restored N ships from save`
4. ✅ Корабли на тех же позициях, что и до перезапуска
5. ✅ NPC в Docked остаются в Docked, не стартуют Dwell заново
6. ✅ NPC в Cruising продолжают движение к целевой станции
7. ✅ 0 errors, 0 дублей

---

## 9. Что НЕ делаем (out of scope)

- ❌ Клиентская синхронизация сохранений (чисто сервер)
- ❌ Сохранение cargo/инвентаря кораблей (для этого есть отдельные системы)
- ❌ Сохранение пассажиров на борту
- ❌ Debounce/diff-save оптимизация (пока ~10-20 кораблей, полный dump ок)
- ❌ Сохранение позиций игроков на кораблях (это отдельная Player persistence)

---

**Создано:** 2026-07-10
**Следующий шаг:** сказать «поехали T-PERSIST-01» для начала реализации.
