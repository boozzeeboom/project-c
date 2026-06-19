# 02 — V2 Architecture: Docking Stations

> **Цель:** Описать целевую архитектуру подсистемы стыковочных портов в
> v2-конвенции проекта (server-hub + RPCs + DTO + ClientState).
> Основано на референсах `QuestServer`, `MarketZone`, `Composite Ship`.

---

## 1. Карта namespaces и классов

```
ProjectC.Docking
├── Network
│   ├── DockingServer           (NetworkBehaviour singleton, BootstrapScene)
│   └── DockingZoneRegistry     (static registry по locationId)
├── Core
│   ├── DockingWorld            (MonoBehaviour singleton, server state machine)
│   ├── DockStationDefinition   (ScriptableObject — паспорт станции)
│   ├── DockPadLayout           (ScriptableObject — pads + совместимость)
│   ├── DispatcherVoiceLines    (ScriptableObject — фразы диспетчера)
│   └── DockingSnapshot         (POCO — server-side projection для DTO)
├── Client
│   ├── DockingClientState      (singleton projection, MonoBehaviour)
│   └── DockingSnapshotDto      (struct INetworkSerializable)
├── Stations
│   ├── DockStationController   (NetworkBehaviour, scene-placed на root)
│   ├── StationRootReference    (marker MonoBehaviour)
│   ├── StationComponentLocator (static helper)
│   ├── DockingPadTriggerBox    (MonoBehaviour, child, триггерная зона)
│   └── PadTriggerReference     (marker MonoBehaviour на pad)
├── Zones
│   └── OuterCommZone           (MonoBehaviour, scene-placed, sphere trigger)
├── UI
│   ├── CommPanelWindow         (UIDocument — диалог с диспетчером)
│   └── CommPanelToast          (UIDocument — wrong-pad warning)
└── Dto
    ├── DockStationInfoDto      (struct INetworkSerializable)
    ├── DockPadInfoDto          (struct INetworkSerializable)
    ├── DockingAssignmentDto    (struct INetworkSerializable)
    └── DockingStatusDto        (struct INetworkSerializable)
```

### 1.1 Зависимости от существующих подсистем

| Подсистема | Что используем |
|------------|----------------|
| `ProjectC.Player.NetworkPlayer` | F-key chain, GetEffectivePosition, владелец корабля |
| `ProjectC.Player.ShipController` | ShipFlightClass, FSM, ShipRoot, NetworkObject |
| `ProjectC.Player.PlayerStateMachine` | Mode check (inShip) |
| `ProjectC.Trade.Network.MarketZone` | Референс pattern (НЕ прямое использование) |
| `ProjectC.Quests.Network.QuestServer` | Референс pattern (rate limiting, OnNetworkSpawn) |
| `ProjectC.Quests.UI.DialogWindow` | Референс pattern (UIDocument lifecycle, EnsureBuilt) |
| `ProjectC.Ship.ShipRootReference` | Референс marker pattern (НЕ прямое — наш `StationRootReference` отдельный) |
| `ProjectC.Network.NetworkingUtils` | IsServerSafe, IsClientSafe |
| `ProjectC.Core.WorldEventBus` | (опционально для Phase 2: событие `OnPlayerDocked`) |

**Не зависим от:** `InventoryWorld` (стиковка не трогает инвентарь —
топливо/груз — Phase 2+), `TradeWorld` (только если будет docking fee — Phase 5),
`ReputationClientState` (может модифицировать репутацию за успешную стыковку — Phase 3).

---

## 2. ScriptableObject модели (данные)

### 2.1 `DockStationDefinition` (ScriptableObject)

**Файл:** `Assets/_Project/ScriptableObjects/Docking/DockStationDefinition.cs`

```csharp
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Docking.Core {
    [CreateAssetMenu(fileName = "DockStation_", menuName = "ProjectC/Docking/DockStationDefinition", order = 100)]
    public class DockStationDefinition : ScriptableObject {
        [Header("Identity")]
        [SerializeField] private string stationId = "";      // "STN-PRM-001"
        [SerializeField] private string locationId = "";     // "PRIMIUM" — синк с MarketZone.LocationId
        [SerializeField] private string displayName = "";    // "Док-станция Примум"

        [Header("Geometry")]
        [SerializeField] private Vector3 platformCenter = Vector3.zero;
        [SerializeField] private float platformAltitude = 4348f;  // из GDD-10 §2.2

        [Header("Pads")]
        [Tooltip("Ссылка на общий layout pads (Light/Medium/Heavy slots).")]
        [SerializeField] private DockPadLayout padLayout;

        [Header("Dispatcher")]
        [SerializeField] private DispatcherVoiceLines voiceLines;

        [Header("Limits")]
        [SerializeField, Min(1)] private int maxConcurrentLandings = 1;
        [SerializeField, Min(10f)] private float landingWindowSeconds = 90f;

        public string StationId => stationId;
        public string LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? stationId : displayName;
        public DockPadLayout PadLayout => padLayout;
        public DispatcherVoiceLines VoiceLines => voiceLines;
        public float LandingWindowSeconds => landingWindowSeconds;
    }
}
```

### 2.2 `DockPadLayout` (ScriptableObject)

**Без хардкода** (Q4): дизайнер сам расставляет pads в `DockPadLayout` SO.
Ограничение ≤10 на класс — **soft-limit** в `OnValidate` (warning, не
запрет). Серверный код не знает про лимиты — принимает любой `pads` list.

```csharp
using UnityEngine;
using ProjectC.Player;
using System.Collections.Generic;

namespace ProjectC.Docking.Core {
    [CreateAssetMenu(fileName = "DockPadLayout_", menuName = "ProjectC/Docking/DockPadLayout", order = 101)]
    public class DockPadLayout : ScriptableObject {
        [System.Serializable]
        public class PadDefinition {
            [Tooltip("Stable ID для синхронизации и сериализации. Должен быть уникален в рамках layout. " +
                     "Дизайнер пишет сюда 'PAD-001', 'PAD-002' и т.п. Цифра с плата отображается в UI " +
                     "(Q13 — цифры на mesh'е).")]
            public string padId = "PAD-001";

            [Tooltip("Локальная позиция относительно DockStation root. Заполняется на сцене, не в SO.")]
            public Vector3 localPosition;

            [Tooltip("Вращение pad (обычно forward в центр станции).")]
            public Vector3 localEulerAngles;

            [Tooltip("Какие классы кораблей могут сесть на этот pad. Без ограничений → пустой массив = " +
                     "совместим со всеми. Типичный вариант: [Light] или [Light, Medium].")]
            public ShipFlightClass[] compatibleShipClasses = { ShipFlightClass.Light };

            [Tooltip("Размер триггерной зоны (overrides global padSize).")]
            public Vector3 triggerBoxSize = new Vector3(8f, 3f, 8f);
        }

        [Header("Pads (любое количество, дизайнер расставляет)")]
        [SerializeField] private List<PadDefinition> pads = new List<PadDefinition>();

        [Header("Default pad geometry (если в PadDefinition triggerBoxSize == zero)")]
        [SerializeField] private Vector3 defaultTriggerBoxSize = new Vector3(8f, 3f, 8f);

        public IReadOnlyList<PadDefinition> Pads => pads;
        public Vector3 DefaultTriggerBoxSize => defaultTriggerBoxSize;

#if UNITY_EDITOR
        private void OnValidate() {
            // Уникальность padId в рамках layout
            var seen = new HashSet<string>();
            foreach (var p in pads) {
                if (string.IsNullOrEmpty(p.padId)) continue;
                if (!seen.Add(p.padId))
                    Debug.LogError($"[DockPadLayout:{name}] duplicate padId '{p.padId}'");
            }

            // Q4: soft-limit ≤10 на класс. Warning, не ошибка.
            // Сервер не знает про лимит — это только для удобства дизайнера.
            var perClass = new Dictionary<ShipFlightClass, int>();
            foreach (var p in pads) {
                if (p.compatibleShipClasses == null) continue;
                foreach (var cls in p.compatibleShipClasses) {
                    perClass.TryGetValue(cls, out int n);
                    perClass[cls] = ++n;
                    if (n > 10) {
                        Debug.LogWarning($"[DockPadLayout:{name}] класс {cls} имеет {n} pads (>10). " +
                                         $"Это soft-limit для MVP, не блокирует, но усложняет UI.");
                    }
                }
            }
        }
#endif
    }
}
```

### 2.3 `DispatcherVoiceLines` (ScriptableObject)

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Docking.Core {
    [CreateAssetMenu(fileName = "DispatcherVoiceLines_", menuName = "ProjectC/Docking/DispatcherVoiceLines", order = 102)]
    public class DispatcherVoiceLines : ScriptableObject {
        [System.Serializable]
        public class PhraseSet {
            public string context;        // "Greeting", "AssignedLight", "AssignedMedium", "AssignedHeavy", "WindowExpired", "Touchdown", "Takeoff", "Goodbye"
            [TextArea] public string[] lines;
        }

        [SerializeField] private List<PhraseSet> phraseSets = new List<PhraseSet>();

        public string GetRandomLine(string context) {
            foreach (var set in phraseSets) {
                if (set.context == context && set.lines != null && set.lines.Length > 0) {
                    return set.lines[Random.Range(0, set.lines.Length)];
                }
            }
            return $"[нет фраз для контекста '{context}']";
        }

        public bool HasContext(string context) {
            foreach (var set in phraseSets) if (set.context == context) return true;
            return false;
        }
    }
}
```

**Контексты, которые нам нужны:**
- `Greeting` — «Борт [ID], [Станция] на связи»
- `Assigning` — «Назначаю pad #N, окно посадки [X] секунд»
- `AssignedLight` / `AssignedMedium` / `AssignedHeavy` — вариации
- `WindowExpired` — «Борт [ID], окно истекло, повторите запрос»
- `Touchdown` — «Зафиксирована стыковка, двигатели заблокированы»
- `Takeoff` — «Отстыковка разрешена, удачного полёта»
- `Goodbye` — нейтральное прощание
- `Occupied` — «Этот pad занят, обождите»
- `WrongPad` — «Борт [ID], вы на чужом pad'е, перепаркуйтесь» (это идёт в toast)

---

## 3. DTO (INetworkSerializable structs)

### 3.1 `DockStationInfoDto`

```csharp
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Dto {
    public struct DockStationInfoDto : INetworkSerializable {
        public string stationId;
        public string locationId;
        public string displayName;
        public Vector3 platformCenter;
        public float platformAltitude;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
            s.SerializeValue(ref stationId);
            s.SerializeValue(ref locationId);
            s.SerializeValue(ref displayName);
            s.SerializeValue(ref platformCenter);
            s.SerializeValue(ref platformAltitude);
        }
    }
}
```

### 3.2 `DockPadInfoDto`

```csharp
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Dto {
    public struct DockPadInfoDto : INetworkSerializable {
        public string padId;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 triggerBoxSize;
        public bool isOccupied;          // текущее состояние (для UI)
        public ulong occupiedByClientId; // 0 = свободен

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
            s.SerializeValue(ref padId);
            s.SerializeValue(ref localPosition);
            s.SerializeValue(ref localEulerAngles);
            s.SerializeValue(ref triggerBoxSize);
            s.SerializeValue(ref isOccupied);
            s.SerializeValue(ref occupiedByClientId);
        }
    }
}
```

### 3.3 `DockingAssignmentDto`

```csharp
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Dto {
    public struct DockingAssignmentDto : INetworkSerializable {
        public string stationId;
        public string padId;
        public Vector3 approachPoint;       // в мировых координатах
        public float approachAltitude;
        public float approachHeading;       // градусы, Y rotation
        public float landingWindowSeconds;
        public string voiceLine;            // уже выбранная фраза диспетчера
        public ulong shipNetworkObjectId;   // к которому привязано назначение
        public bool success;                // false = отказ (см. reason)
        public string failReason;           // "NO_SUITABLE_PAD", "STATION_FULL", "RATE_LIMITED"

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
            s.SerializeValue(ref stationId);
            s.SerializeValue(ref padId);
            s.SerializeValue(ref approachPoint);
            s.SerializeValue(ref approachAltitude);
            s.SerializeValue(ref approachHeading);
            s.SerializeValue(ref landingWindowSeconds);
            s.SerializeValue(ref voiceLine);
            s.SerializeValue(ref shipNetworkObjectId);
            s.SerializeValue(ref success);
            s.SerializeValue(ref failReason);
        }
    }
}
```

### 3.4 `DockingStatusDto`

```csharp
using Unity.Netcode;

namespace ProjectC.Docking.Dto {
    public enum DockingStatus : byte {
        Idle = 0,
        Assigned = 1,     // пилот получил pad, летит
        Approaching = 2,  // (Phase 2: автопилот)
        TouchedDown = 3,  // пилот коснулся pad'а (любого)
        Docked = 4,       // успешная стыковка на правильном pad'е
        Cancelled = 5,    // пилот отменил / окно истекло
        WrongPad = 6      // коснулся чужого pad'а (warning toast)
    }

    public struct DockingStatusDto : INetworkSerializable {
        public DockingStatus status;
        public string stationId;
        public string padId;
        public float timestamp;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
            byte sByte = (byte)status;
            s.SerializeValue(ref sByte);
            status = (DockingStatus)sByte;
            s.SerializeValue(ref stationId);
            s.SerializeValue(ref padId);
            s.SerializeValue(ref timestamp);
        }
    }
}
```

---

## 4. Серверный хаб: `DockingServer`

### 4.1 Структура и lifecycle

```csharp
using Unity.Netcode;
using UnityEngine;
using ProjectC.Network;  // NetworkingUtils

namespace ProjectC.Docking.Network {
    [RequireComponent(typeof(NetworkObject))]
    public class DockingServer : NetworkBehaviour {
        public static DockingServer Instance { get; private set; }

        [Header("Defaults")]
        [SerializeField] private int maxOpsPerMinute = 30;
        [SerializeField] private bool debugMode = true;

        // Rate limiting (копия паттерна из QuestServer)
        private readonly System.Collections.Generic.Dictionary<ulong, System.Collections.Generic.List<float>> _opTimestamps
            = new System.Collections.Generic.Dictionary<ulong, System.Collections.Generic.List<float>>();

        public override void OnNetworkSpawn() {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            if (!IsServer) {
                enabled = false;
                return;
            }
            // Initialize DockingWorld
            DockingWorld.CreateAndInitialize();
            if (debugMode) Debug.Log($"[DockingServer] OnNetworkSpawn — IsServer=true, maxOps/min={maxOpsPerMinute}");
        }

        public override void OnNetworkDespawn() {
            base.OnNetworkDespawn();
            if (IsServer) DockingWorld.Shutdown();
            if (Instance == this) Instance = null;
        }

        // ... RPCs (см. §5)
    }
}
```

**Размещение:** `BootstrapScene` рядом с `QuestServer`, `ExchangeServer`,
`CraftingServer` — все 4 серверных хаба в одном «server infrastructure»
кластере.

### 4.2 Rate limiting

```csharp
private bool IsRateLimited(ulong clientId) {
    if (maxOpsPerMinute <= 0) return false;
    if (!_opTimestamps.TryGetValue(clientId, out var list)) {
        list = new System.Collections.Generic.List<float>();
        _opTimestamps[clientId] = list;
    }
    float now = Time.unscaledTime;
    list.RemoveAll(t => now - t > 60f);
    if (list.Count >= maxOpsPerMinute) return true;
    list.Add(now);
    return false;
}
```

---

## 5. RPCs (server-client контракт)

**Архитектурное замечание по Q7:** двусторонняя связь обязательна для MVP.
После того как сервер назначил pad (шаг 5.1), клиент **обязан подтвердить**
(шаг 5.5 `RequestConfirmAssignmentRpc`). Только после подтверждения сервер
бронирует pad. Это даёт игроку шанс отказаться до фактической посадки.

### 5.1 `RequestDockingRpc`

```csharp
// Клиент → Сервер: пилот нажал "Запросить посадку"
[Rpc(SendTo.Server)]
public void RequestDockingRpc(string stationId, ulong shipNetworkObjectId, RpcParams rpcParams = default) {
    ulong clientId = rpcParams.Receive.SenderClientId;
    if (IsRateLimited(clientId)) {
        SendDockingAssignmentTargetRpc(clientId, MakeFailure("RATE_LIMITED", shipNetworkObjectId));
        return;
    }
    var station = DockingZoneRegistry.GetStation(stationId);
    if (station == null) {
        SendDockingAssignmentTargetRpc(clientId, MakeFailure("STATION_NOT_FOUND", shipNetworkObjectId));
        return;
    }
    var ship = FindNetworkObject(shipNetworkObjectId)?.GetComponent<ShipController>();
    if (ship == null || !ship.IsSpawned) {
        SendDockingAssignmentTargetRpc(clientId, MakeFailure("SHIP_NOT_FOUND", shipNetworkObjectId));
        return;
    }
    // Найти подходящий pad
    var assignment = DockingWorld.Instance.AssignPad(station, ship, ship.ShipFlightClass);
    // Q7: НЕ регистрируем сразу. Только после RequestConfirmAssignmentRpc.
    if (assignment.success) {
        // Временное состояние "awaiting confirmation" — клиент должен подтвердить за 30 сек
        DockingWorld.Instance.RegisterPendingAssignment(clientId, shipNetworkObjectId, assignment);
    }
    SendDockingAssignmentTargetRpc(clientId, assignment);
}
```

### 5.2 `RequestTakeoffRpc`

```csharp
// Клиент → Сервер: пилот хочет отстыковться
// ВНИМАНИЕ: Q8 — это ТОЛЬКО для отстыковки из Docked. Для вылета из
// OuterCommZone сначала нужно RequestDeparturePermission (см. 08_DEPARTURE_SUBSYSTEM).
[Rpc(SendTo.Server)]
public void RequestTakeoffRpc(ulong shipNetworkObjectId, RpcParams rpcParams = default) {
    ulong clientId = rpcParams.Receive.SenderClientId;
    if (IsRateLimited(clientId)) return;
    var ship = FindNetworkObject(shipNetworkObjectId)?.GetComponent<ShipController>();
    if (ship == null) return;
    DockingWorld.Instance.ReleaseAssignment(clientId, shipNetworkObjectId);
    SendTakeoffApprovedTargetRpc(clientId, shipNetworkObjectId);
}
```

### 5.3 `NotifyTouchedDownRpc`

```csharp
// Клиент → Сервер: пилот коснулся pad'а (или любой collider на DockStation)
[Rpc(SendTo.Server)]
public void NotifyTouchedDownRpc(ulong shipNetworkObjectId, string padId, string stationId, RpcParams rpcParams = default) {
    ulong clientId = rpcParams.Receive.SenderClientId;
    if (IsRateLimited(clientId)) return;
    var status = DockingWorld.Instance.ConfirmTouchdown(clientId, shipNetworkObjectId, padId, stationId);
    SendDockingStatusTargetRpc(clientId, status);
}
```

### 5.5 `RequestConfirmAssignmentRpc` (Q7 — новое)

```csharp
// Клиент → Сервер: пилот подтверждает назначение (после фразы диспетчера).
// Q7: без подтверждения pad не считается занятым.
[Rpc(SendTo.Server)]
public void RequestConfirmAssignmentRpc(ulong shipNetworkObjectId, bool accept, RpcParams rpcParams = default) {
    ulong clientId = rpcParams.Receive.SenderClientId;
    if (IsRateLimited(clientId)) return;
    if (accept) {
        DockingWorld.Instance.ConfirmAssignment(clientId, shipNetworkObjectId);
        // Status Assigned → подтверждено, окно посадки пошло
        var assignment = DockingWorld.Instance.GetAssignment(clientId, shipNetworkObjectId);
        if (assignment.HasValue) {
            var status = new DockingStatusDto {
                status = DockingStatus.Assigned,
                stationId = assignment.Value.stationId,
                padId = assignment.Value.padId,
                timestamp = Time.time
            };
            SendDockingStatusTargetRpc(clientId, status);
        }
    } else {
        // Отбой — освобождаем pending assignment, клиент снова может запросить другой pad
        DockingWorld.Instance.CancelPendingAssignment(clientId, shipNetworkObjectId);
        var status = new DockingStatusDto {
            status = DockingStatus.Cancelled,
            stationId = "",
            padId = "",
            timestamp = Time.time
        };
        SendDockingStatusTargetRpc(clientId, status);
    }
}
```

### 5.4 TargetRpcs (сервер → конкретный клиент)

```csharp
[Rpc(SendTo.SpecifiedInParams)]
private void SendDockingAssignmentTargetRpc(ulong clientId, DockingAssignmentDto assignment, RpcParams rpcParams = default) {
    // Клиент: получить назначение → DockingClientState
    DockingClientState.Instance?.HandleAssignmentReceived(assignment);
}

[Rpc(SendTo.SpecifiedInParams)]
private void SendDockingStatusTargetRpc(ulong clientId, DockingStatusDto status, RpcParams rpcParams = default) {
    DockingClientState.Instance?.HandleStatusReceived(status);
}

[Rpc(SendTo.SpecifiedInParams)]
private void SendTakeoffApprovedTargetRpc(ulong clientId, ulong shipNetId, RpcParams rpcParams = default) {
    DockingClientState.Instance?.HandleTakeoffApproved(shipNetId);
}
```

---

## 6. Серверное состояние: `DockingWorld`

### 6.1 Назначение

`DockingWorld` — singleton (MonoBehaviour, DontDestroyOnLoad), держит
**серверное состояние занятости pads** (single source of truth, Q3). Клиент
НЕ хранит представление о занятости — запрашивает или получает push при
изменениях. Архитектура совместима с NPC-кораблями (Phase 2):
`_occupiedPads[padKey] = occupantId` работает одинаково для игрока и NPC.

**Слои состояния (Q7 — двусторонняя связь):**
1. **Pending**: `RequestDockingRpc` назначил pad, но клиент ещё не подтвердил.
2. **Assigned (confirmed)**: клиент подтвердил, идёт окно посадки.
3. **Docked**: игрок приземлился.
4. **Released**: пилот отстыковался, pad снова свободен.

```csharp
using UnityEngine;
using ProjectC.Docking.Dto;
using ProjectC.Player;
using System.Collections.Generic;

namespace ProjectC.Docking.Core {
    public class DockingWorld : MonoBehaviour {
        public static DockingWorld Instance { get; private set; }

        // server-only state (Q3: SOT)
        // Occupant = кто занимает pad. ulong = clientId для игрока, или NPC ID (Phase 2).
        private readonly Dictionary<string, ulong> _occupiedPads = new Dictionary<string, ulong>();
        // Pending assignments: ждут RequestConfirmAssignmentRpc от клиента
        private readonly Dictionary<ulong, ActiveAssignment> _pendingByClient = new Dictionary<ulong, ActiveAssignment>();
        private readonly Dictionary<ulong, ActiveAssignment> _pendingByShip = new Dictionary<ulong, ActiveAssignment>();
        // Confirmed assignments: окно посадки идёт
        private readonly Dictionary<ulong, ActiveAssignment> _assignmentsByClient = new Dictionary<ulong, ActiveAssignment>();
        private readonly Dictionary<ulong, ActiveAssignment> _assignmentsByShip = new Dictionary<ulong, ActiveAssignment>();

        private struct ActiveAssignment {
            public string stationId;
            public string padId;
            public ulong shipNetId;
            public ulong clientId;       // для NPC (Phase 2): clientId = NpcInstanceId
            public float assignedAt;
            public float landingWindowSec;
            public bool used;             // уже приземлился
        }

        public static void CreateAndInitialize() {
            if (Instance != null) return;
            var go = new GameObject("[DockingWorld]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DockingWorld>();
        }

        public static void Shutdown() {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        // === Public API для DockingServer ===

        /// <summary>
        /// Назначить свободный pad. НЕ регистрирует сразу — только в pending.
        /// </summary>
        public DockingAssignmentDto AssignPad(DockStationController station, ShipController ship, ShipFlightClass shipClass) {
            var def = station.StationDefinition;
            if (def == null) return MakeFail("STATION_NO_DEFINITION", ship.NetworkObjectId);

            // Q4: без хардкода — берём все pads из layout, проверяем каждый
            foreach (var pad in def.PadLayout.Pads) {
                if (!IsCompatible(pad.compatibleShipClasses, shipClass)) continue;
                string padKey = PadKey(def.StationId, pad.padId);
                if (_occupiedPads.ContainsKey(padKey)) continue;  // уже занят (SOT check)
                if (IsPending(def.StationId, pad.padId)) continue;  // ждёт подтверждения
                // OK — назначаем (но не регистрируем)
                return new DockingAssignmentDto {
                    stationId = def.StationId,
                    padId = pad.padId,
                    approachPoint = station.transform.TransformPoint(pad.localPosition),
                    approachAltitude = station.transform.position.y + 30f,
                    approachHeading = station.transform.eulerAngles.y + pad.localEulerAngles.y,
                    landingWindowSeconds = def.LandingWindowSeconds,
                    voiceLine = def.VoiceLines?.GetRandomLine("Assigning") ?? "",
                    shipNetworkObjectId = ship.NetworkObjectId,
                    success = true
                };
            }
            return MakeFail("NO_SUITABLE_PAD", ship.NetworkObjectId);
        }

        /// <summary>
        /// Q7: после RequestDockingRpc клиент получает assignment, шлёт
        /// RequestConfirmAssignmentRpc(accept=true) → ConfirmAssignment.
        /// </summary>
        public void RegisterPendingAssignment(ulong clientId, ulong shipNetId, DockingAssignmentDto assignment) {
            if (!assignment.success) return;
            var a = new ActiveAssignment {
                stationId = assignment.stationId,
                padId = assignment.padId,
                shipNetId = shipNetId,
                clientId = clientId,
                assignedAt = Time.time,
                landingWindowSec = assignment.landingWindowSeconds,
                used = false
            };
            _pendingByClient[clientId] = a;
            _pendingByShip[shipNetId] = a;
            // Pending assignment не занимает pad — другой игрок может запросить этот же
            // pad, если pending истечёт. После Confirm — pad блокируется.
        }

        public void ConfirmAssignment(ulong clientId, ulong shipNetId) {
            if (!_pendingByClient.TryGetValue(clientId, out var a)) return;
            if (a.shipNetId != shipNetId) return;
            string padKey = PadKey(a.stationId, a.padId);
            // Q3: занимаем pad (SOT)
            _occupiedPads[padKey] = clientId;
            _assignmentsByClient[clientId] = a;
            _assignmentsByShip[shipNetId] = a;
            // Удаляем из pending
            _pendingByClient.Remove(clientId);
            _pendingByShip.Remove(shipNetId);
        }

        public void CancelPendingAssignment(ulong clientId, ulong shipNetId) {
            _pendingByClient.Remove(clientId);
            _pendingByShip.Remove(shipNetId);
        }

        public ActiveAssignment? GetAssignment(ulong clientId, ulong shipNetId) {
            if (_assignmentsByClient.TryGetValue(clientId, out var a)) return a;
            return null;
        }

        public DockingStatusDto ConfirmTouchdown(ulong clientId, ulong shipNetId, string padId, string stationId) {
            var assignment = _assignmentsByShip.TryGetValue(shipNetId, out var a) ? a : default;
            if (assignment.clientId != clientId) {
                return MakeStatus(DockingStatus.WrongPad, stationId, padId);
            }
            if (assignment.padId != padId) {
                return MakeStatus(DockingStatus.WrongPad, stationId, padId);
            }
            a.used = true;
            _assignmentsByShip[shipNetId] = a;
            _assignmentsByClient[clientId] = a;
            return MakeStatus(DockingStatus.Docked, stationId, padId);
        }

        public void ReleaseAssignment(ulong clientId, ulong shipNetId) {
            if (_assignmentsByClient.TryGetValue(clientId, out var a)) {
                string padKey = PadKey(a.stationId, a.padId);
                _occupiedPads.Remove(padKey);  // Q3: освобождаем pad
                _assignmentsByClient.Remove(clientId);
                _assignmentsByShip.Remove(shipNetId);
            }
            // Также чистим pending (если был)
            _pendingByClient.Remove(clientId);
            _pendingByShip.Remove(shipNetId);
        }

        public bool IsPadOccupied(string stationId, string padId) {
            return _occupiedPads.ContainsKey(PadKey(stationId, padId));
        }

        public bool IsPending(string stationId, string padId) {
            string padKey = PadKey(stationId, padId);
            foreach (var kv in _pendingByClient) {
                if (PadKey(kv.Value.stationId, kv.Value.padId) == padKey) return true;
            }
            return false;
        }

        // === Helpers ===

        private bool IsCompatible(ShipFlightClass[] allowed, ShipFlightClass shipClass) {
            if (allowed == null || allowed.Length == 0) return true;  // пустой = для всех
            foreach (var s in allowed) if (s == shipClass) return true;
            return false;
        }

        private static string PadKey(string stationId, string padId) => $"{stationId}/{padId}";

        private static DockingAssignmentDto MakeFail(string reason, ulong shipNetId) => new DockingAssignmentDto {
            success = false, failReason = reason, shipNetworkObjectId = shipNetId
        };

        private static DockingStatusDto MakeStatus(DockingStatus status, string stationId, string padId) =>
            new DockingStatusDto { status = status, stationId = stationId, padId = padId, timestamp = Time.time };

        // === Update: expiration check ===

        private void Update() {
            if (!NetworkingUtils.IsServerSafe()) return;
            // Истечение pending assignment (клиент не подтвердил за 30 сек)
            var expiredPending = new List<ulong>();
            foreach (var kv in _pendingByClient) {
                if (Time.time - kv.Value.assignedAt > 30f) {
                    expiredPending.Add(kv.Key);
                }
            }
            foreach (var cId in expiredPending) {
                if (_pendingByClient.TryGetValue(cId, out var a)) {
                    CancelPendingAssignment(cId, a.shipNetId);
                    SendDockingStatusTargetRpc(cId, MakeStatus(DockingStatus.Cancelled, a.stationId, a.padId));
                }
            }
            // Истечение окна посадки (confirmed, но не приземлился)
            var expiredAssigned = new List<ulong>();
            foreach (var kv in _assignmentsByClient) {
                if (Time.time - kv.Value.assignedAt > kv.Value.landingWindowSec && !kv.Value.used) {
                    expiredAssigned.Add(kv.Key);
                }
            }
            foreach (var cId in expiredAssigned) {
                if (_assignmentsByClient.TryGetValue(cId, out var a)) {
                    ReleaseAssignment(cId, a.shipNetId);
                    SendDockingStatusTargetRpc(cId, MakeStatus(DockingStatus.Cancelled, a.stationId, a.padId));
                }
            }
        }

        // === Q3: Pad occupancy snapshot (для клиента при необходимости) ===

        /// <summary>
        /// Возвращает список (padId, isOccupied, isPending) для всех pads станции.
        /// Клиент может вызвать через RPC для отладки или UI.
        /// </summary>
        public List<PadStatusInfo> GetPadStatusSnapshot(string stationId, DockStationController station) {
            var result = new List<PadStatusInfo>();
            if (station?.StationDefinition?.PadLayout == null) return result;
            foreach (var pad in station.StationDefinition.PadLayout.Pads) {
                result.Add(new PadStatusInfo {
                    padId = pad.padId,
                    isOccupied = IsPadOccupied(stationId, pad.padId),
                    isPending = IsPending(stationId, pad.padId)
                });
            }
            return result;
        }

        public struct PadStatusInfo {
            public string padId;
            public bool isOccupied;
            public bool isPending;
        }
    }
}
```

---

## 7. Клиентская проекция: `DockingClientState`

### 7.1 Структура

**Изменения после решений:**
- Q7: добавлен `AwaitingConfirmation` state (между Assigned и Confirmed).
- Q10: helper `IsLocalPlayerPilotingShip()` для проверки «в кресле пилота».

```csharp
using UnityEngine;
using ProjectC.Docking.Dto;
using System;

namespace ProjectC.Docking.Client {
    /// <summary>
    /// Клиентская проекция серверного DockingWorld (Q3: SOT на сервере,
    /// клиент только отображает то, что пришло по RPC).
    /// Singleton, RuntimeInitializeOnLoadMethod (auto-spawn при старте).
    /// </summary>
    public class DockingClientState : MonoBehaviour {
        public static DockingClientState Instance { get; private set; }

        // События для UI
        public event Action<DockingAssignmentDto> OnAssignmentReceived;     // Q7: сервер назначил, ждём подтверждения
        public event Action<DockingStatusDto> OnStatusReceived;             // Assigned/Docked/Cancelled/WrongPad
        public event Action<DockingAssignmentDto, bool> OnAwaitingConfirmation;  // Q7: новый — UI показывает [Хорошо]/[Отбой]
        public event Action<ulong> OnTakeoffApproved;
        public event Action<DockingStatusDto> OnTouchedDown;

        // Текущее состояние (Q7: добавлен AwaitingConfirmation)
        public DockingAssignmentDto? PendingAssignment { get; private set; }  // ждёт подтверждения
        public DockingAssignmentDto? CurrentAssignment { get; private set; }   // подтверждено
        public DockingStatusDto? CurrentStatus { get; private set; }
        public string NearestStationId { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate() {
            if (Instance != null) return;
            var go = new GameObject("[DockingClientState]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DockingClientState>();
        }

        private void OnDestroy() {
            if (Instance == this) Instance = null;
        }

        // === Handlers (called by DockingServer via TargetRpc) ===

        public void HandleAssignmentReceived(DockingAssignmentDto assignment) {
            if (assignment.success) {
                PendingAssignment = assignment;
                // Q7: посылаем событие UI показать [Хорошо]/[Отбой]
                OnAwaitingConfirmation?.Invoke(assignment, true);
            } else {
                // Failure (no pad, rate limit, etc) — UI покажет сообщение
                OnAssignmentReceived?.Invoke(assignment);
            }
        }

        public void HandleStatusReceived(DockingStatusDto status) {
            // Q7: если Assigned — значит клиент подтвердил. Переводим Pending → Current.
            if (status.status == DockingStatus.Assigned) {
                if (PendingAssignment.HasValue) {
                    CurrentAssignment = PendingAssignment;
                    PendingAssignment = null;
                }
            } else if (status.status == DockingStatus.Docked) {
                OnTouchedDown?.Invoke(status);
            } else if (status.status == DockingStatus.Cancelled) {
                // Отбой: очищаем оба
                PendingAssignment = null;
                CurrentAssignment = null;
            }
            CurrentStatus = status;
            OnStatusReceived?.Invoke(status);
        }

        public void HandleTakeoffApproved(ulong shipNetId) {
            CurrentAssignment = null;
            CurrentStatus = null;
            OnTakeoffApproved?.Invoke(shipNetId);
        }

        public void SetNearestStation(string stationId) {
            NearestStationId = stationId;
        }

        // === Q10: helper для проверки «в кресле пилота» ===

        public static bool IsLocalPlayerPilotingShip() {
            var np = GetLocalPlayer();
            if (np == null) return false;
            if (!np.IsInShip) return false;
            // Q10: для MVP — IsInShip достаточно (грубая проверка).
            // В Phase 2 — точная проверка через PilotSeat.
            return np.CurrentShip != null;
        }

        private static NetworkPlayer GetLocalPlayer() {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++) {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }
    }
}
```

**Auto-spawn через `NetworkManagerController.Awake`** (по канону `QuestClientState`):
```csharp
// В NetworkManagerController.Awake, рядом с CreateQuestClientState:
private void CreateDockingClientState() {
    if (DockingClientState.Instance != null) return;
    var go = new GameObject("[DockingClientState]");
    DontDestroyOnLoad(go);
    DockingClientState.Instance = go.AddComponent<DockingClientState>();
}
```

---

## 8. Реестр станций: `DockingZoneRegistry`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Docking.Network {
    /// <summary>
    /// Серверный реестр DockStationController + клиентский трекинг ближайшей станции.
    /// По аналогии с MarketZoneRegistry.
    /// </summary>
    public static class DockingZoneRegistry {
        private static readonly Dictionary<string, DockStationController> _stationsById = new Dictionary<string, DockStationController>();
        private static readonly Dictionary<string, DockStationController> _stationsByLocation = new Dictionary<string, DockStationController>();

        // Клиентский: текущая ближайшая станция к локальному игроку
        public static DockStationController LocalPlayerStation { get; set; }
        public static DockStationController LocalPlayerShipStation { get; set; }  // для корабля

        public static IReadOnlyDictionary<string, DockStationController> All => _stationsById;

        public static void Register(DockStationController station) {
            if (station == null) return;
            var def = station.StationDefinition;
            if (def == null || string.IsNullOrEmpty(def.StationId)) {
                Debug.LogError($"[DockingZoneRegistry] station {station.gameObject.name} has no StationId");
                return;
            }
            _stationsById[def.StationId] = station;
            if (!string.IsNullOrEmpty(def.LocationId))
                _stationsByLocation[def.LocationId] = station;
        }

        public static void Unregister(DockStationController station) {
            if (station == null) return;
            var def = station.StationDefinition;
            if (def == null) return;
            if (_stationsById.TryGetValue(def.StationId, out var existing) && existing == station)
                _stationsById.Remove(def.StationId);
            if (!string.IsNullOrEmpty(def.LocationId) && _stationsByLocation.TryGetValue(def.LocationId, out var existing2) && existing2 == station)
                _stationsByLocation.Remove(def.LocationId);
            if (LocalPlayerStation == station) LocalPlayerStation = null;
            if (LocalPlayerShipStation == station) LocalPlayerShipStation = null;
        }

        public static DockStationController GetStation(string stationId) {
            if (string.IsNullOrEmpty(stationId)) return null;
            _stationsById.TryGetValue(stationId, out var s);
            return s;
        }

        public static DockStationController GetByLocation(string locationId) {
            if (string.IsNullOrEmpty(locationId)) return null;
            _stationsByLocation.TryGetValue(locationId, out var s);
            return s;
        }

        public static void Clear() {
            _stationsById.Clear();
            _stationsByLocation.Clear();
            LocalPlayerStation = null;
            LocalPlayerShipStation = null;
        }
    }
}
```

---

## 9. Scene-placed объекты

### 9.1 BootstrapScene
- `[DockingServer]` (NetworkBehaviour, DontDestroyOnLoad через NetworkObject).

### 9.2 WorldScene_0_0
- `DockStation_Primium` (composite):
  ```
  DockStation_Primium (GameObject)
  ├── NetworkObject + NetworkBehaviour  // auto-spawn
  ├── DockStationController (NetworkBehaviour)
  │   └── dockStationDefinition (assigned in inspector)
  ├── StationRootReference (marker)
  ├── OuterCommZone (SphereCollider radius=1000, isTrigger)
  ├── Pad_001 (GameObject, BoxCollider isTrigger, localPosition)
  │   ├── DockingPadTriggerBox (MonoBehaviour, padId="PAD-001")
  │   └── PadTriggerReference (marker)
  ├── Pad_002 ...
  └── ...
  ```

**Сцена `MarketZone_Primium` уже есть** (см. `01_CURRENT_STATE_AUDIT.md`)
на координатах `(40096.5, 2510, 40140.6)` — но это пешая зона (5м радиус),
а dock-станция будет на высоте Примум 4348м. Размещаем в новом месте.

**Координаты для MVP:** ориентируемся на `Chest_Main` как центр тестовой зоны
(40000, 2502.77, 40000). `DockStation_Primium` ставим в `WorldScene_0_0` на
высоте 4000-4500м (но сцена пока не имеет «неба», поэтому для теста
ставим на существующей высоте 2510м — Phase 3 будет отдельная sky-карта).

---

## 10. Расширение FSM корабля

### 10.1 Новые состояния

`ShipController` уже имеет состояния из GDD-10 §8. Добавляем (Q15: bool-флаги):

| Состояние | Триггер Входа | Триггер Выхода | Эффект |
|-----------|---------------|----------------|--------|
| **Docking** | `DockingClientState.HandleStatusReceived(Assigned)` + игрок в корабле | Landed / Cancelled / WindowExpired | Тяга/lift подавлены (input ignored), HUD: «Следуйте к pad #N» |
| **Docked** | `DockingClientState.HandleStatusReceived(Docked)` | `RequestTakeoffRpc` одобрено → AutoHover/Idle | Engine Off, `rb.isKinematic = true`, HUD: «Пристыковано, F для отстыковки» |

### 10.2 Где живёт FSM (Q15: bool-флаги)

**Текущее состояние:** `ShipController` не имеет явной FSM enum —
проверки разбросаны (`if (pilotCount > 0)` и т.п.). Это **не** канон v2.

**Решение для MVP (Q15):** НЕ вводим новую enum-FSM. Вместо этого
добавляем в `ShipController`:
- `_isDockingAssigned : bool`
- `_isDocked : bool`
- `EnterDockingAssigned(assignment)` — переводит корабль в «assigned» режим (подавить thrust/lift)
- `EnterDockedState(padId)` — kinematic + freeze
- `ExitDockedState()` — обратно в Idle/AutoHover

В Phase 3 (после обкатки) — рефакторинг в `enum ShipState` + `ShipStateBehaviour`.

### 10.3 Модификация ShipController (выдержка)

```csharp
// В ShipController:

private bool _isDockingAssigned;
private bool _isDocked;
private string _assignedPadId;
private string _assignedStationId;

public bool IsDockingAssigned => _isDockingAssigned;
public bool IsDocked => _isDocked;

public void EnterDockingAssigned(string stationId, string padId) {
    _isDockingAssigned = true;
    _assignedStationId = stationId;
    _assignedPadId = padId;
    // подавить thrust/lift: в FixedUpdate
    //   if (_isDockingAssigned) { ignore pilotInput thrust/lift; allow yaw/pitch для подлёта }
}

public void EnterDocked() {
    _isDockingAssigned = false;
    _isDocked = true;
    _rb.isKinematic = true;          // freeze
    _rb.velocity = Vector3.zero;
    _rb.angularVelocity = Vector3.zero;
}

public void ExitDocked() {
    _isDocked = false;
    _rb.isKinematic = false;
}
```

### 10.4 Q11 — KeyRod-извлечение в Docked

**НЕ обрабатываем** для MVP. Причина: F = выход из кресла пилота = «выключает»
корабль (имитируя KeyRod-извлечение на текущем уровне). Игрок физически
не может бросить KeyRod во время пилотирования через стандартный flow.

**Если в Phase 2 будет нужен полноценный KeyRod-флоу** — добавим блокировку
отдельным тикетом `T-DOCK-14`.

---

## 11. Phase 2 hooks (задел на будущее)

### 11.1 Автопилот (`MODULE_AUTO_DOCK`)

`DockingWorld.AssignPad()` уже возвращает `DockingAssignmentDto` с
`approachPoint / approachAltitude / approachHeading` — **ровно те данные,
которые нужны автопилоту** (см. GDD-10 §7.1 шаг 5). Phase 2: `ShipController`
добавит `ApproachToPad(assignment)` (lerp позиции) при наличии модуля.
**Никаких изменений в `DockingServer` / `DockingWorld` не потребуется.**

### 11.2 NPC-диспетчер с ИИ

`DispatcherVoiceLines.GetRandomLine(context)` — точка расширения. Phase 2:
заменить на `IDispatcherStrategy` interface (статичный / rule-based / LLM).
**Никаких изменений в серверной архитектуре.**

### 11.3 Traffic controller (очереди)

`DockingWorld._occupiedPads` уже dictionary. Phase 2: добавить
`_pendingRequests` queue + таймауты. `DockingServer.RequestDockingRpc`
уже имеет rate limiting. **Изменения локализованы в `DockingWorld`.**

---

## 12. Совместимость с другими подсистемами

| Подсистема | Что делаем | Что НЕ делаем |
|------------|-----------|---------------|
| **TradeWorld** | (Phase 3) Может быть docking fee | Не зависим сейчас |
| **InventoryWorld** | — | Не трогаем |
| **Reputation** | (Phase 3) +reputation за успешную стыковку | Не сейчас |
| **QuestServer** | (Phase 4) M14+ квесты могут требовать стыковку в Примум | Не сейчас |
| **ShipKeyServer** (deprecated → MetaRequirement) | — | Не влияет |
| **WorldEventBus** | (Phase 3) `OnPlayerDocked(stationId, padId)` event | Не сейчас |

---

## 13. Диаграмма последовательности (полный поток)

```
PLAYER                CLIENT                  SERVER              WORLD
  │                     │                       │                   │
  │─ press T ──────────▶│                       │                   │
  │                     │─ FindNearestStation() │                   │
  │                     │  (client-side)        │                   │
  │                     │                       │                   │
  │                     │─ RequestDockingRpc ──▶│                   │
  │                     │                       │─ AssignPad() ────▶│
  │                     │                       │  (find free pad)  │
  │                     │                       │◀─ assignment ─────│
  │                     │◀─ AssignmentTargetRpc │                   │
  │                     │  (with padId, voice)  │                   │
  │                     │                       │                   │
  │◀─ Open CommPanel ───│                       │                   │
  │   "Pad #5 assigned" │                       │                   │
  │                     │                       │                   │
  │─ fly ship to pad ───│ (physics, client-side)                   │
  │                     │                       │                   │
  │─ trigger pad box ──▶│                       │                   │
  │                     │─ NotifyTouchedDownRpc ▶                   │
  │                     │                       │─ ConfirmTouchdown ▶│
  │                     │                       │◀─ status: Docked ─│
  │                     │◀─ StatusTargetRpc ────│                   │
  │                     │                       │                   │
  │◀─ "Docked" toast ───│                       │                   │
  │   Ship freeze       │                       │                   │
```

Подробнее с edge-cases — в `05_FLOW_AND_INTERACTION.md`.

---

## 14. Ключевые решения и обоснования

| # | Решение | Обоснование |
|---|---------|-------------|
| D1 | `DockingServer` в `BootstrapScene` | По канону v2 (QuestServer, ExchangeServer, CraftingServer) |
| D2 | `DockingWorld` как отдельный singleton | Separation: серверный hub vs server state. Совместимо с ExchangeWorld pattern |
| D3 | `DockingClientState` через `RuntimeInitializeOnLoadMethod` + `NetworkManagerController.CreateXxx` (как `QuestClientState`) | Race-free singleton creation |
| D4 | `StationRootReference` / `StationComponentLocator` (по аналогии с `ShipRootReference`) | Маркер pattern для внешних систем, чтобы не лазить по иерархии |
| D5 | DTO как `INetworkSerializable` struct (не класс) | Канон v2, никаких рефлексий |
| D6 | Rate limiting 30 ops/min/client (copy-paste из QuestServer) | Anti-spam |
| D7 | FSM через `bool` флаги в `ShipController` (не enum) | Минимальный диф для MVP. Phase 3 — рефакторинг в `ShipState` enum |
| D8 | `DockingPadTriggerBox` — `MonoBehaviour` (не `NetworkBehaviour`) | Локальный триггер, на сервере детектится через `OnTriggerEnter` серверного `DockStationController` |
| D9 | `OuterCommZone` копирует `MarketZone` 1:1 (с изменениями) | 95% общего кода, 5% специфики |
| D10 | `DispatcherVoiceLines` — статичный набор фраз (не ИИ) | Простота MVP, детерминированный UX |
| D11 | Session-only persistence (без JSON) | MVP. Phase 3 — JSON repository по канону `KeyRodInstanceWorld` |

---

*Создано: 2026-06-19 | Аналитическая сессия | Без кода.*