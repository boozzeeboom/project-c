# 03 — V2 Architecture: Peaceful NPC Ships

> **Цель:** Целевая архитектура подсистемы мирных NPC-кораблей. Namespace map, lifecycle, target classes, v2 extension points.
>
> **Обновлено 2026-06-22** после принятия 13 решений. См. `06_OPEN_QUESTIONS.md`.

---

## 1. Namespace map

```
ProjectC.PeacefulShip
├── Core/                          (POCO, server-only state)
│   ├── NpcShipWorld               (singleton, by-pattern DockingWorld)
│   ├── NpcShipState               (POCO: route, position, status, schedule index)
│   ├── NpcShipRoute               (struct: fromLocationId, toLocationId, dwellTimeSec)
│   └── NpcShipCargoManifest       (v2-hook struct, пустой в M1)
├── Network/                       (server hub + manager)
│   ├── NpcShipServer              (NetworkBehaviour hub, BootstrapScene)
│   ├── NpcShipTrafficManager      (singleton, Gaussian arrival shaping)
│   └── NpcShipZoneRegistry        (static: NpcInstanceId → NpcShipController)
├── Stations/                      (scene-placed)
│   ├── NpcShipController          (NetworkBehaviour, scene-placed на корне NPC-корабля)
│   └── NpcShipSchedule            (ScriptableObject — routes + dwell + traffic params)
├── Client/                        (UI projection)
│   ├── NpcShipClientState         (singleton, DontDestroyOnLoad)
│   └── NpcShipSnapshotDto         (INetworkSerializable)
└── Dto/
    ├── NpcShipSpawnDto            (INetworkSerializable — initial state)
    └── NpcShipStatusDto           (INetworkSerializable — текущий status)
```

**Зависимости от существующих подсистем:**

| Подсистема | Что используем |
|------------|----------------|
| `ProjectC.Docking.Core` (DockingWorld) | `AssignPadForNpc`, `ConfirmTouchdown`, `ReleaseAssignment`, `IsPadOccupied` |
| `ProjectC.Docking.Network` (DockingZoneRegistry) | `GetStation`, `GetByLocation` — routing |
| `ProjectC.Docking.Core` (DockStationDefinition) | `locationId` (синк с Market), `maxConcurrentLandings` |
| `ProjectC.Player` (ShipController) | `ApplyServerInput` (NEW), `EnterDocked`, `ExitDocked`, `ShipFlightClass`, `AntiGravity` |
| `ProjectC.Trade.Network` (NetworkingUtils) | `IsServerSafe` |
| `ProjectC.Core` (WorldEventBus — v2) | `OnNpcShipArrived/Departed` (v2) |

**Не зависим от:** `ProjectC.Quests.*` (M1 — отдельная система), `ProjectC.Items.*` (cargo — v2), `ProjectC.Trade.*` (кроме v2 events).

---

## 2. Target classes — sketches

### 2.1 `NpcShipState` (Core, POCO)

```csharp
namespace ProjectC.PeacefulShip.Core
{
    public enum NpcShipStatus : byte
    {
        Idle,           // только что заспавнен
        Departing,      // съезд с пада + набор высоты
        InTransit,      // полёт к целевой станции
        Approaching,    // заход на посадку
        Holding,        // ждёт свободный pad
        Diverting,      // уходит к другой станции (pad занят)
        Docking,        // финальное приземление на pad
        Docked,         // на паде, двигатель заблокирован
        Loading,        // 30-90 сек no-op пауза (визуальная жизнь, Q5)
        Undocking,      // отстыковка
        Done            // цикл завершён, restart
    }

    public class NpcShipState
    {
        public readonly ulong NpcInstanceId;  // NetworkObjectId | 0x8000_0000_0000_0000UL (Q3)
        public readonly ShipController Ship;
        public NpcShipStatus Status;
        public NpcShipRoute CurrentRoute;
        public float StateEnteredAt;
        public int ScheduleIndex;
        public Vector3 LastKnownPosition;
        public NpcShipCargoManifest Cargo; // v2 hook (Q10 — пустой в M1)
    }
}
```

### 2.2 `NpcShipRoute` (Core, struct)

```csharp
namespace ProjectC.PeacefulShip.Core
{
    [System.Serializable]
    public struct NpcShipRoute
    {
        public string fromLocationId;       // "PRIMIUM"
        public string toLocationId;         // "TERTIUS_TEST_ZONE"
        public float dwellTimeSec;          // 600 = 10 мин на станции (Q5)
        public float flightDurationSec;     // вычисляемое от дистанции
        public ShipFlightClass preferredShipClass;
        public NpcShipDemandCategory demandCategory; // v2: market-driven
    }

    public enum NpcShipDemandCategory : byte
    {
        Generic = 0,    // M1: random traffic
        HighDemand,     // v2: route to station with low stock
        LowDemand,      // v2: route to station with high stock
        Contract,       // v2: tied to player contract
    }
}
```

### 2.3 `NpcShipSchedule` (Stations, ScriptableObject)

```csharp
namespace ProjectC.PeacefulShip.Stations
{
    [CreateAssetMenu(fileName = "NpcShipSchedule_", menuName = "ProjectC/PeacefulShip/NpcShipSchedule", order = 110)]
    public class NpcShipSchedule : ScriptableObject
    {
        public enum ScheduleType : byte { RoundTrip, Loop, RandomFromPool }

        [Header("Identity")]
        public string scheduleId = "SCH-NPC-001";
        public string displayName = "Курьер Примум-Терциус";

        [Header("Behavior")]
        public ScheduleType scheduleType = ScheduleType.RoundTrip;
        public NpcShipRoute[] routes;

        [Header("Traffic Shaping (Gaussian)")]
        [Tooltip("Mean time between arrivals (sec).")]
        public float meanArrivalIntervalSec = 480f;         // 8 мин
        [Tooltip("Std dev for Gaussian (sec).")]
        public float arrivalIntervalStdDev = 90f;
        [Tooltip("Min seconds between consecutive arrivals at same station.")]
        public float minArrivalSpacingSec = 60f;
    }
}
```

### 2.4 `NpcShipWorld` (Core, server singleton)

```csharp
namespace ProjectC.PeacefulShip.Core
{
    public class NpcShipWorld : MonoBehaviour
    {
        public static NpcShipWorld Instance { get; private set; }

        private readonly Dictionary<ulong, NpcShipState> _npcByInstanceId = new();
        private readonly Dictionary<string /*stationId*/, List<NpcShipArrival>> _arrivalsByStation = new();

        // events (v2 hooks)
        public event Action<ulong, string> OnNpcShipArrived;
        public event Action<ulong, string> OnNpcShipDeparted;
        public event Action<ulong, NpcShipCargoManifest> OnNpcShipLoaded;    // v2
        public event Action<ulong, NpcShipCargoManifest> OnNpcShipUnloaded;  // v2

        public static void CreateAndInitialize() { /* pattern: DockingWorld.CreateAndInitialize */ }
        public void RegisterNpc(ulong id, ShipController ship, NpcShipSchedule schedule) { }
        public void UnregisterNpc(ulong id) { }
        public NpcShipState GetNpc(ulong id) => _npcByInstanceId.TryGetValue(id, out var s) ? s : null;

        private void TickNpc(NpcShipState s, float dt) { /* FSM — см. 04_LIVING_BEHAVIOR.md §2 */ }
    }
}
```

### 2.5 `NpcShipController` (Stations, scene-placed NetworkBehaviour)

```csharp
namespace ProjectC.PeacefulShip.Stations
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ShipController))]
    public class NpcShipController : NetworkBehaviour
    {
        [Header("Schedule")]
        [SerializeField] private NpcShipSchedule schedule;
        [SerializeField] private ulong npcInstanceId = 0;

        [Header("Movement (server-only)")]
        [SerializeField] private float npcThrustMult = 0.6f;
        [SerializeField] private float npcArrivalToleranceMeters = 50f;

        [Header("Anti-gravity boost after ExitDocked (Q8)")]
        [SerializeField] private float antiGravityBoostDuration = 5f;
        [SerializeField] private float antiGravityBoostValue = 1.5f;

        public NpcShipSchedule Schedule => schedule;
        public ulong NpcInstanceId => npcInstanceId;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }
            // sentinel id: high bit = 1 (Q3)
            if (npcInstanceId == 0)
                npcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL;

            // Auto-enable NPC pilot (Q2: explicit _hasNpcPilot flag)
            var ship = GetComponent<ShipController>();
            ship.EnableNpcPilot(true);

            // Registration
            NpcShipZoneRegistry.Register(this);
            NpcShipWorld.Instance?.RegisterNpc(npcInstanceId, ship, schedule);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                var ship = GetComponent<ShipController>();
                if (ship != null) ship.EnableNpcPilot(false);
                NpcShipWorld.Instance?.UnregisterNpc(npcInstanceId);
                NpcShipZoneRegistry.Unregister(this);
            }
            base.OnNetworkDespawn();
        }

        public void ApplyMovementInput(float thrust, float yaw, float pitch, float vertical)
        {
            if (!IsServer) return;
            var ship = GetComponent<ShipController>();
            ship.ApplyServerInput(thrust * npcThrustMult, yaw, pitch, vertical);
        }

        /// <summary>Server-only snap для финального позиционирования на pad'е.</summary>
        public void ServerTeleport(Vector3 pos, Quaternion rot)
        {
            if (!IsServer) return;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) { rb.position = pos; rb.rotation = rot; rb.linearVelocity = Vector3.zero; }
        }

        /// <summary>Q8: anti-gravity boost на 5 сек после ExitDocked чтобы корабль не упал.</summary>
        private IEnumerator AntiGravityBoostAfterExitDocked()
        {
            var ship = GetComponent<ShipController>();
            float originalAntiGrav = ship.AntiGravity;
            ship.AntiGravity = antiGravityBoostValue;
            yield return new WaitForSeconds(antiGravityBoostDuration);
            ship.AntiGravity = originalAntiGrav;
        }
    }
}
```

### 2.6 `NpcShipTrafficManager` (Network, server singleton)

```csharp
namespace ProjectC.PeacefulShip.Network
{
    public class NpcShipTrafficManager : MonoBehaviour
    {
        public static NpcShipTrafficManager Instance { get; private set; }

        [SerializeField] private float globalJitterMaxSec = 2f;
        [SerializeField] private float defaultMinSpacingSec = 8f;

        // Q9: без rate limiting (FSM сама ограничивает)

        public float ScheduleNextArrival(string stationId, NpcShipSchedule schedule, float now)
        {
            // Box-Muller Gaussian
            float u1 = UnityEngine.Random.value;
            float u2 = UnityEngine.Random.value;
            float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
            float sampled = schedule.meanArrivalIntervalSec + schedule.arrivalIntervalStdDev * z;
            sampled = Mathf.Clamp(sampled, schedule.minArrivalSpacingSec, schedule.meanArrivalIntervalSec * 2f);

            // spacing enforcement
            float lastArrival = GetLastArrivalAt(stationId);
            float proposed = now + sampled;
            float minSpacing = Mathf.Max(defaultMinSpacingSec, schedule.minArrivalSpacingSec);
            if (proposed - lastArrival < minSpacing)
                proposed = lastArrival + minSpacing;

            RegisterArrival(stationId, proposed);
            return proposed + Random.Range(-globalJitterMaxSec, globalJitterMaxSec);
        }
    }
}
```

### 2.7 `NpcShipServer` (Network, hub)

```csharp
namespace ProjectC.PeacefulShip.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class NpcShipServer : NetworkBehaviour
    {
        public static NpcShipServer Instance { get; private set; }

        [SerializeField] private NpcShipSchedule[] allSchedules;
        [SerializeField] private bool debugMode = true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            if (!IsServer) { enabled = false; return; }

            NpcShipWorld.CreateAndInitialize();
            NpcShipTrafficManager.CreateAndInitialize();
            StartCoroutine(DiscoverNpcShipsDelayed());
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer) NpcShipWorld.Shutdown();
            if (Instance == this) Instance = null;
        }

        private IEnumerator DiscoverNpcShipsDelayed()
        {
            yield return new WaitForSeconds(2f);
            var npcs = FindObjectsByType<NpcShipController>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (npc == null || npc.Schedule == null) continue;
                ulong id = npc.NpcInstanceId;
                if (id == 0) continue;
                NpcShipWorld.Instance.RegisterNpc(id, npc.GetComponent<ProjectC.Player.ShipController>(), npc.Schedule);
            }
            if (debugMode) Debug.Log($"[NpcShipServer] Discovered {NpcShipWorld.Instance.AllNpcs.Count()} NPC ships");
        }
    }
}
```

---

## 3. Lifecycle

### 3.1 Startup

```
NetworkManager.StartHost
  → ScenePlacedObjectSpawner.SpawnInAllLoadedScenes()
    → [NpcShipServer].OnNetworkSpawn
      → NpcShipWorld.CreateAndInitialize()
      → NpcShipTrafficManager.CreateAndInitialize()
      → (2s delay) DiscoverNpcShipsDelayed()
        → FindObjectsByType<NpcShipController>
        → NpcShipWorld.RegisterNpc(npcId, ship, schedule)
  → [NpcShipController].OnNetworkSpawn (per-NPC)
    → npcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL (Q3)
    → ship.EnableNpcPilot(true)  (Q2: explicit flag)
    → NpcShipZoneRegistry.Register
```

### 3.2 Runtime

```
NpcShipWorld.Update()
  └── для каждого NpcShipState: TickNpc(state, dt)
        ├── Status == Departing:
        │     Anti-gravity boost 5 сек (Q8)
        │     ApplyMovementInput(thrust=0.6, pitch=+0.3, vertical=+0.5)
        ├── Status == InTransit:
        │     ApplyMovementInput(thrust=0.6)
        │     dist < 500m → Status = Approaching
        ├── Status == Approaching:
        │     ApplyMovementInput(thrust=0.2, vertical=-0.2)
        │     DockingWorld.AssignPadForNpc (с учётом maxConcurrentLandings, Q6)
        │       success → Docked / fail → Holding или Diverting
        ├── Status == Holding:
        │     5s → retry AssignPadForNpc
        ├── Status == Diverting:
        │     курс на следующую станцию в маршруте
        ├── Status == Docked:
        │     dwellTime elapsed (30-90 сек Q5) → Loading → Undocking
        │     [player displacement] → Diverting (immediate)
        └── Status == Loading:
              пауза 30-90 сек → Undocking → Departing → next leg
```

### 3.3 Shutdown

```
NpcShipServer.OnNetworkDespawn
  → NpcShipWorld.Shutdown()
  → NpcShipTrafficManager.Shutdown()
  → NpcShipZoneRegistry.Clear()
```

---

## 4. Docking integration — server-internal API

### 4.1 Sentinel design (Q3)

`NpcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL` — старший бит = 1.

- Отличимо от реальных clientId (NGO v2: 0..N, max ~64k)
- Стабильно между scene reloads
- Проверка: `if (id > 0x7FFF_FFFF_FFFF_FFFFUL) → это NPC`

### 4.2 `AssignPadForNpc` в `DockingWorld`

```csharp
public DockingAssignmentDto AssignPadForNpc(
    DockStationController station,
    ShipController ship,
    ShipFlightClass shipClass,
    ulong npcInstanceId)
{
    if (!IsServerSafe()) return MakeFail("NOT_SERVER", ship.NetworkObjectId);
    if (IsNpcAlreadyAssigned(npcInstanceId)) return MakeFail("ALREADY_ASSIGNED", ship.NetworkObjectId);

    // Q6: учитывать maxConcurrentLandings — если лимит достигнут, NO_SUITABLE_PAD
    var stationDef = station.StationDefinition;
    if (stationDef != null && CountCurrentLandings(stationDef.StationId) >= stationDef.MaxConcurrentLandings)
        return MakeFail("STATION_FULL", ship.NetworkObjectId);

    var assignment = AssignPad(station, ship, shipClass);
    if (!assignment.success) return assignment;

    RegisterPendingAssignment(npcInstanceId, ship.NetworkObjectId, assignment);
    ConfirmAssignment(npcInstanceId, ship.NetworkObjectId);
    if (ship.IsServer) ship.EnterDocked();

    return assignment;
}
```

### 4.3 Touchdown auto-detection

Modify `DockingServer.NotifyTouchedDownRpc`: если `ship.GetComponent<NpcShipController>() != null` → route to `DockingWorld.ConfirmTouchdown(npcInstanceId, ...)` server-internal.

---

## 5. Movement API — `ShipController.ApplyServerInput()` + `_hasNpcPilot`

**Решение Q1:** новый public server-only метод. **V2 hook:** этот же API будет основой для автопилота игрока (`ProjectC.Player.AutoPilot.AutoPilotController` может вызывать `ship.ApplyServerInput(...)` для автономного движения).

**Решение Q2:** явный флаг `_hasNpcPilot` (bool, server-only). Выставляется через `EnableNpcPilot(bool)`.

```csharp
// В Assets/_Project/Scripts/Player/ShipController.cs (server-only секция)

/// <summary>T-NS01 + Q1: Server-only прямой вход. Минует _pilots gate.
/// Используется NPC-pilot и (v2) player autopilot.</summary>
public void ApplyServerInput(float thrust, float yaw, float pitch, float vertical, bool boost = false)
{
    if (!IsServer) return;
    if (_netIsDocked.Value) return;    // T-DOCK-09 defense
    if (engineStalled) return;

    _sumThrust += thrust;
    _sumYaw += yaw;
    _sumPitch += pitch;
    _sumVertical += vertical;
    if (boost) _boostCount++;
    _inputCount++;
}

/// <summary>T-NS01 + Q2: включает NPC-pilot режим. Server-only.</summary>
public void EnableNpcPilot(bool enable)
{
    if (!IsServer) return;
    _hasNpcPilot = enable;
}
```

**Изменение в `FixedUpdate` (line 773):**
```csharp
// БЫЛО:
if (_pilots.Count == 0) return;

// СТАЛО (Q2):
if (_pilots.Count == 0 && !_hasNpcPilot) return;
```

**Anti-gravity property (Q8):**
```csharp
public float AntiGravity { get; set; } = 1f;  // expose for NpcShipController.AntiGravityBoost
```

---

## 6. v2 Forward-compat hooks

### 6.1 `NpcShipCargoManifest` (Q10 — пустой в M1)

```csharp
[System.Serializable]
public struct NpcShipCargoManifest : INetworkSerializable
{
    public int capacitySlots;
    public float capacityWeight;
    public NpcCargoEntryDto[] items; // null в M1
    // + NetworkSerialize()
}
```

### 6.2 `NpcShipRoute.demandCategory`

В M1 = `Generic` (random routing). В v2: `NpcShipTrafficManager` подписан на `TradeWorld.OnMarketTick`.

### 6.3 Events on `NpcShipWorld`

```csharp
public event Action<ulong, string> OnNpcShipArrived;    // npcId, stationId
public event Action<ulong, string> OnNpcShipDeparted;
public event Action<ulong, NpcShipCargoManifest> OnNpcShipLoaded;   // v2
public event Action<ulong, NpcShipCargoManifest> OnNpcShipUnloaded; // v2
```

---

## 7. Pad contention policy (Q6)

**Player-first.** Когда игрок `ConfirmAssignment` на pad, занятый NPC:

```
1. DockingWorld: prevOccupant = occupant NPC
2. Event: NpcShipWorld.OnPadTakenByPlayer(npcId, station, padId)
3. NPC FSM: Docked → Diverting
4. Ship.ExitDocked(), ReleaseAssignment()
5. NPC летит к следующей станции
```

**NPC уступает мгновенно** — без лагов, что сохраняет отзывчивость для игрока.

**NPC учитывает maxConcurrentLandings (Q6):** при вызове `AssignPadForNpc`, если станция уже имеет `maxConcurrentLandings` запрашиваемых/текущих посадок — NPC получает `STATION_FULL` и переходит в `Holding` или `Diverting`. Игрок и NPC ограничены одинаково.

---

## 8. References

| Файл | Что |
|------|-----|
| `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` | DockingWorld — server state pattern |
| `Assets/_Project/Scripts/Player/ShipController.cs` | ShipController — movement + docking (ApplyServerInput: NEW) |
| `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` | Auto-spawn scene-placed NetworkObject |
| `docs/Docking_stations/ARCHITECTURE.md` | Hub-pattern канон |
| `docs/Docking_stations/02_V2_ARCHITECTURE.md` | Docking V2 детали |
| `docs/Docking_stations/08_DEPARTURE_SUBSYSTEM.md` | Вылет/отстыковка |
| `docs/Markets/ARCHITECTURE.md` | Market system — ссылочный паттерн |
| `06_OPEN_QUESTIONS.md` | Финальные решения (13 ответов) |