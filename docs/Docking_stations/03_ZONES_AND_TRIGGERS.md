# 03 — Zones & Triggers

> **Цель:** Спроектировать иерархию композитной структуры **DockStation**
> и двух типов триггерных зон — большой `OuterCommZone` для связи с
> диспетчером и маленьких `DockingPadTriggerBox` для физического
> касания корабля.

---

## 1. Иерархия объектов

### 1.1 DockStation (composite) — корневой объект порта

```
DockStation_Primium (GameObject)
├── NetworkObject (NGO)
├── Rigidbody (kinematic=true, mass=1, useGravity=false) — для стабильности, но НЕ используется в физике
├── DockStationController (NetworkBehaviour)
│   └── dockStationDefinition → DockStation_Primium.asset (SO)
├── StationRootReference (marker MonoBehaviour)
├── OuterCommZone (SphereCollider isTrigger, radius=1000)
│   └── OuterCommZone (MonoBehaviour)
├── Pad_001 (GameObject)
│   ├── BoxCollider (isTrigger=true, size=8x3x8) — на pad model
│   ├── DockingPadTriggerBox (MonoBehaviour, padId="PAD-001")
│   └── PadTriggerReference (marker)
├── Pad_002 ...
├── Pad_006 ...
└── (опц.) Visual: модель станции, платформы, балки
```

### 1.2 Принцип «один Rigidbody, один NetworkObject»

По канону composite ship (`docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md`):
- **`Rigidbody` ОДИН** на корне (kinematic — dock не двигается).
- **`NetworkObject` ОДИН** на корне — `DockStationController` помечен
  как спавнящийся через `ScenePlacedObjectSpawner`.
- **Все pads — дети** без собственного `NetworkObject`/`Rigidbody`.
- **Триггеры на pads** — `BoxCollider` с `isTrigger=true`.

### 1.3 Что на сцене, что в коде

| Объект | Где | Кто создаёт |
|--------|-----|-------------|
| `DockStation_Primium` GameObject | `WorldScene_0_0.unity` | Пользователь (расставить pads на нужных позициях) |
| `DockStation_Primium.asset` (SO) | `ScriptableObjects/Docking/` | Редактор (CreateAssetMenu) |
| `DefaultDockPadLayout.asset` (SO) | `ScriptableObjects/Docking/` | Редактор |
| `DockStationController` компонент | scene | Расставить вручную или через MCP (T-DOCK-12) |
| `OuterCommZone` компонент | scene | Расставить вручную на root |
| `DockingPadTriggerBox` | scene на каждом Pad_* | Расставить вручную (6 шт на станцию) |
| `[DockingServer]` в BootstrapScene | scene | T-DOCK-02 |

---

## 2. `StationRootReference` + `StationComponentLocator`

### 2.1 Маркер `StationRootReference`

```csharp
using UnityEngine;
using ProjectC.Docking.Network;

namespace ProjectC.Docking.Stations {
    /// <summary>
    /// Marker MonoBehaviour на любой части DockStation.
    /// Кеширует DockStationController + NetworkObject от корня.
    /// По аналогии с ShipRootReference (composite ship pattern).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class StationRootReference : MonoBehaviour {
        public DockStationController StationController { get; private set; }
        public NetworkObject StationNetworkObject { get; private set; }
        public Transform StationRoot => transform.root;

        private void Awake() {
            var root = transform.root;
            StationController = root.GetComponent<DockStationController>();
            StationNetworkObject = root.GetComponent<NetworkObject>();
            if (StationController == null) {
                Debug.LogWarning($"[StationRootReference] No DockStationController on root of {gameObject.name}", this);
            }
        }
    }
}
```

### 2.2 Static helper `StationComponentLocator`

```csharp
using UnityEngine;

namespace ProjectC.Docking.Stations {
    /// <summary>
    /// Унифицированный поиск DockStationController от произвольной части композитного объекта.
    /// Порядок: marker → GetComponent → GetComponentInParent → GetComponentInChildren.
    /// </summary>
    public static class StationComponentLocator {
        public static DockStationController FindDockStationController(GameObject from) {
            if (from == null) return null;
            var ref_ = from.GetComponentInParent<StationRootReference>();
            if (ref_ != null && ref_.StationController != null) return ref_.StationController;
            var dsc = from.GetComponent<DockStationController>();
            if (dsc != null) return dsc;
            dsc = from.GetComponentInParent<DockStationController>();
            if (dsc != null) return dsc;
            return from.GetComponentInChildren<DockStationController>();
        }
    }
}
```

### 2.3 Где ставим маркер

| GameObject | Ставим `StationRootReference`? |
|------------|-------------------------------|
| `DockStation_Primium` (root) | ✅ да (по конвенции — на root тоже) |
| `Pad_001` (child) | ✅ да (для поиска от pad'а) |
| `Pad_002` | ✅ да |
| ... | ... |
| `OuterCommZone` GameObject (child) | ✅ да (чтобы внешние системы находили DockStation от OuterCommZone) |

---

## 3. `OuterCommZone` — большая зона связи с диспетчером

### 3.1 Назначение

Sphere-зона, при входе в которую игрок/корабль получают возможность
связаться с диспетчером. **Не вызывает UI автоматически** — только
обновляет клиентский singleton `DockingZoneRegistry.LocalPlayerStation` /
`LocalPlayerShipStation`. Открытие `CommPanel` — по нажатию T.

### 3.2 Структура

```csharp
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Network;
using ProjectC.Player;
using ProjectC.Docking.Network;
using ProjectC.Docking.Stations;

namespace ProjectC.Docking.Zones {
    /// <summary>
    /// Большая sphere-зона связи с диспетчером. По аналогии с MarketZone.
    /// Детектит NetworkPlayer и ShipController в радиусе commRange.
    /// Обновляет DockingZoneRegistry.LocalPlayerStation / LocalPlayerShipStation.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class OuterCommZone : MonoBehaviour {
        [Header("Identity")]
        [SerializeField] private string stationId = "";

        [Header("Zone")]
        [SerializeField, Min(50f)] private float commRange = 1000f;
        [SerializeField] private bool drawGizmos = true;

        public string StationId => stationId;
        public float CommRange => commRange;

        // Серверные данные
        private readonly HashSet<ulong> _playersInRange = new HashSet<ulong>();
        private readonly HashSet<ulong> _shipsInRange = new HashSet<ulong>();

        private SphereCollider _sphere;
        private DockStationController _stationController;
        private bool _isServer;

        private void Awake() {
            _sphere = GetComponent<SphereCollider>();
            _sphere.isTrigger = true;
            _sphere.radius = commRange;
            _stationController = GetComponentInParent<DockStationController>();
            if (_stationController == null)
                Debug.LogError($"[OuterCommZone:{stationId}] no DockStationController in parent", this);
        }

        private void OnEnable() {
            DockingZoneRegistry.Register(_stationController);
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null) {
                nm.OnServerStarted += HandleServerStarted;
                nm.OnClientStarted += HandleClientStarted;
                if (nm.IsListening) _isServer = nm.IsServer;
            }
        }

        private void OnDisable() {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null) {
                nm.OnServerStarted -= HandleServerStarted;
                nm.OnClientStarted -= HandleClientStarted;
            }
            // Unregister через DockingZoneRegistry.Unregister (внутри проверит owner)
        }

        private void HandleServerStarted() => _isServer = true;
        private void HandleClientStarted() => _isServer = NetworkManager.Singleton.IsServer;

        // ========================================================
        // SERVER: TRIGGERS + UPDATE POLL (debounced)
        // Копия паттерна из MarketZone с дебаунсом.
        // ========================================================

        private float _pollTimer;
        private const float POLL_INTERVAL = 0.25f;

        private void Update() {
            _pollTimer += Time.deltaTime;
            if (_pollTimer < POLL_INTERVAL) return;
            _pollTimer = 0f;

            if (_isServer) {
                PollPlayersInRange();
                PollShipsInRange();
            }
            PollLocalPlayerZone();
        }

        // ... PollPlayersInRange / PollShipsInRange / PollLocalPlayerZone
        //     аналогично MarketZone (см. Assets/_Project/Trade/Scripts/Network/MarketZone.cs:218-282)
        //     с заменой имен на _playersInRange/_shipsInRange и _stationController

        private void PollLocalPlayerZone() {
            if (_stationController == null) return;
            var localPlayer = FindLocalPlayer();
            var localShip = FindLocalShip();
            if (localPlayer != null) {
                float dist = Vector3.Distance(transform.position, localPlayer.GetEffectivePosition());
                if (dist <= commRange) {
                    if (DockingZoneRegistry.LocalPlayerStation != _stationController) {
                        DockingZoneRegistry.LocalPlayerStation = _stationController;
                    }
                } else {
                    if (DockingZoneRegistry.LocalPlayerStation == _stationController) {
                        DockingZoneRegistry.LocalPlayerStation = null;
                    }
                }
            }
            if (localShip != null) {
                float dist = Vector3.Distance(transform.position, localShip.transform.position);
                if (dist <= commRange) {
                    DockingZoneRegistry.LocalPlayerShipStation = _stationController;
                } else if (DockingZoneRegistry.LocalPlayerShipStation == _stationController) {
                    DockingZoneRegistry.LocalPlayerShipStation = null;
                }
            }
        }

        private static NetworkPlayer FindLocalPlayer() {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++) {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }

        private static ShipController FindLocalShip() {
            // Локальный корабль — это корабль, на котором сидит локальный игрок, или
            // любой ship с владельцем = local client (в пределах видимости).
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null && localPlayer.IsInShip) {
                return localPlayer.CurrentShip;  // существующее поле NetworkPlayer
            }
            // Без игрока в корабле: вернуть null (коммуникация пешком возможна без корабля)
            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (!drawGizmos) return;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, commRange);
        }
#endif
    }
}
```

### 3.3 Параметры по умолчанию (Q5 — настраивается)

| Поле | Default | Обоснование |
|------|---------|-------------|
| `commRange` | **1000 м** (default, но **настраивается в Inspector** как `MarketZone.tradeRadius`) | GDD-10 §7.1 говорит «500-1500 м». 1000 — компромисс; дизайнер меняет в сцене |
| `POLL_INTERVAL` | 0.25 с | Как в `MarketZone` |
| `MISS_THRESHOLD` | 3 тика (0.75 с) | Как в `MarketZone` — defense against NetworkTransform interpolation gaps |

**Q5 (принято):** никакого хардкода радиуса. Дизайнер задаёт значение
в Inspector как для `MarketZone.tradeRadius`. Default в коде = 1000м.

### 3.4 Регистрация в реестре

`OuterCommZone.Awake` находит `DockStationController` через
`GetComponentInParent` и регистрирует его. Это позволяет нам **один
DockStation = один OuterCommZone = один entry в реестре**, даже если
в будущем добавим дополнительные зоны (например, "приближение к городу"
на 2000м — другой OuterCommZone, но тот же DockStation).

---

## 4. `DockStationController` — NetworkBehaviour на root

### 4.1 Назначение

Server-side authoritative state для станции. **Не** держит активные
назначения (это делает `DockingWorld`), но:
- Регистрирует станцию в `DockingZoneRegistry` (через OuterCommZone.OnEnable).
- Предоставляет API `DockingWorld.AssignPad(station, ...)`.
- Помечает NetworkBehaviour для спавна через ScenePlacedObjectSpawner.

### 4.2 Структура

```csharp
using Unity.Netcode;
using UnityEngine;
using ProjectC.Docking.Core;
using ProjectC.Docking.Network;
using ProjectC.Docking.Stations;

namespace ProjectC.Docking.Network {
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(OuterCommZone))]
    public class DockStationController : NetworkBehaviour {
        [Header("Definition")]
        [Tooltip("Паспорт станции. Должен быть назначен в инспекторе.")]
        [SerializeField] private DockStationDefinition dockStationDefinition;

        public DockStationDefinition StationDefinition => dockStationDefinition;

        public string StationId => dockStationDefinition != null ? dockStationDefinition.StationId : "";
        public string LocationId => dockStationDefinition != null ? dockStationDefinition.LocationId : "";
        public string DisplayName => dockStationDefinition != null ? dockStationDefinition.DisplayName : "";

        private void Awake() {
            if (dockStationDefinition == null) {
                Debug.LogError($"[DockStationController:{gameObject.name}] dockStationDefinition is null! " +
                               $"Назначь SO в инспекторе.", this);
            }
        }

        public override void OnNetworkSpawn() {
            base.OnNetworkSpawn();
            // Регистрация уже произошла в OuterCommZone.OnEnable.
            // Здесь только проверка и логирование.
            if (debugMode) Debug.Log($"[DockStationController:{StationId}] OnNetworkSpawn — IsServer={IsServer}, stationDef={dockStationDefinition != null}");
        }

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Marker: на root, чтобы внешние системы знали что это DockStation
        public bool IsDockStation => true;
    }
}
```

**ScenePlacedObjectSpawner** (см. GDD-10 §13) автоматически спавнит
`DockStationController` при загрузке `WorldScene_0_0`, если у него
есть `NetworkObject` (что мы гарантируем через `[RequireComponent]`).

### 4.3 Альтернативный путь: авто-attach OuterCommZone

`[RequireComponent(typeof(OuterCommZone))]` гарантирует, что при
добавлении `DockStationController` в инспектор автоматически добавится
`OuterCommZone` со SphereCollider. Это **вторая защита** после ручной
расстановки.

---

## 5. `DockingPadTriggerBox` — зона физической стыковки

### 5.1 Назначение

Маленькая BoxCollider на каждом `Pad_*` GameObject. Детектит вход
корабля в зону, отправляет `NotifyTouchedDownRpc` на сервер.

### 5.2 Структура

```csharp
using UnityEngine;
using ProjectC.Network;
using ProjectC.Player;
using ProjectC.Docking.Network;
using ProjectC.Docking.Stations;

namespace ProjectC.Docking.Stations {
    /// <summary>
    /// Триггерная зона одного docking pad'а. Детектит вход ShipController.
    /// На сервере: проверяет, тот ли это pad что был назначен → NotifyTouchedDownRpc.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DockingPadTriggerBox : MonoBehaviour {
        [Header("Identity")]
        [SerializeField] private string padId = "PAD-001";

        [Header("Compatibility (overrides DockPadLayout if set)")]
        [SerializeField] private ShipFlightClass[] compatibleShipClasses;

        public string PadId => padId;

        private BoxCollider _box;
        private DockStationController _stationController;
        private bool _isServer;

        private void Awake() {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
            _stationController = GetComponentInParent<DockStationController>();
            if (_stationController == null)
                Debug.LogError($"[DockingPadTriggerBox:{padId}] no DockStationController in parent", this);
        }

        private void OnEnable() {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null) {
                nm.OnServerStarted += HandleServerStarted;
                if (nm.IsListening) _isServer = nm.IsServer;
            }
        }

        private void OnDisable() {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null) nm.OnServerStarted -= HandleServerStarted;
        }

        private void HandleServerStarted() => _isServer = true;

        private void OnTriggerEnter(Collider other) {
            if (!_isServer) return;
            var ship = other.GetComponentInParent<ShipController>();
            if (ship == null || !ship.IsSpawned) return;
            // Определить игрока-владельца корабля (первый пилот)
            ulong ownerClientId = GetFirstPilotClientId(ship);
            if (ownerClientId == ulong.MaxValue) return;
            // Отправить RPC на DockingServer (клиент сам вызовет через NetworkPlayer)
            // Примечание: DockingPadTriggerBox не NetworkBehaviour, поэтому
            // маршрутизируем через NetworkPlayer владельца — но удобнее, чтобы
            // клиент сам вызывал NotifyTouchedDownRpc. См. примечание ниже.
        }

        // ... GetFirstPilotClientId — приватный хелпер
    }
}
```

### 5.3 Альтернативный паттерн: клиент сам вызывает RPC

**Проблема:** `DockingPadTriggerBox` — `MonoBehaviour`, не `NetworkBehaviour`,
поэтому не может напрямую отправлять RPC на сервер.

**Решение:** обработка `OnTriggerEnter` на **клиенте** (владельце корабля)
→ вызов `DockingServer.Instance.NotifyTouchedDownRpc(...)`. Это
нормальный паттерн для game-triggers (см. `PickupItem` и др.).

```csharp
// Альтернативная реализация — обработка на клиенте

private void OnTriggerEnter(Collider other) {
    var ship = other.GetComponentInParent<ShipController>();
    if (ship == null) return;
    // Только владелец корабля (или хост) отправляет RPC
    var netPlayer = FindOwnerOfShip(ship);
    if (netPlayer == null || !netPlayer.IsOwner) return;
    var server = DockingServer.Instance;
    if (server == null) return;
    server.NotifyTouchedDownRpc(ship.NetworkObjectId, padId, _stationController.StationId);
}
```

**Где:** `FindOwnerOfShip` — хелпер, который ищет NetworkPlayer с
`_currentShip == ship` (уже существует в `NetworkPlayer`).

### 5.4 Альтернативный паттерн: DockingPadTriggerBox как NetworkBehaviour

**Если на сервере нужен single-source-of-truth для trigger-входа:**
сделать `DockingPadTriggerBox : NetworkBehaviour` и обрабатывать
`OnTriggerEnter` ТОЛЬКО на сервере. Минус: больше NetworkObject'ов в
сцене (по одному на каждый pad), что усложняет ScenePlacedObjectSpawner.

**Решение для MVP:** клиентский подход (паттерн 5.3). Сервер валидирует
в `DockingServer.NotifyTouchedDownRpc` (anti-cheat: проверка позиции
корабля, проверка назначения).

### 5.5 Размеры и форма

| Параметр | Default | Обоснование |
|----------|---------|-------------|
| Box size | 8×3×8 м | Light корабль: масса ~1т, размер ~6×0.9×12 (из GDD §14.5). 8×3×8 даёт запас. |
| Триггер vs collider | `isTrigger=true` | Без физического толчка — корабль мягко входит в зону |

**Фаза 3 (визуал):** добавить child-объект `Pad_Model` с mesh + материнским
BoxCollider (НЕ триггер, для физики) рядом с `DockingPadTriggerBox`
(триггер). Композитно: физический пол + триггер.

---

## 6. Маппинг pad ↔ shipClass (Q4 — без хардкода)

### 6.1 Источник правды

`ShipFlightClass` enum: `Light`, `Medium`, `Heavy`, `HeavyII` (см.
`Assets/_Project/Scripts/Player/ShipController.cs:18-24`).

### 6.2 Принцип: «пустой = для всех»

`PadDefinition.compatibleShipClasses[]` — массив. Серверная логика:

```csharp
private bool IsCompatible(ShipFlightClass[] allowed, ShipFlightClass shipClass) {
    if (allowed == null || allowed.Length == 0) return true;  // пустой = для всех
    foreach (var s in allowed) if (s == shipClass) return true;
    return false;
}
```

**Это значит:**
- `[Light]` — только Light.
- `[Light, Medium]` — Light + Medium.
- `[]` (пустой) — **для всех классов**.

### 6.3 Q4: дизайнер расставляет pads без ограничений

**Без хардкода количества pads.** Дизайнер в `DockPadLayout` SO добавляет
столько pads, сколько нужно для его станции (от 0 до любого количества).
Лимит **≤10 на класс** (Q4) — **soft-limit** в `OnValidate` SO (warning,
не запрет), чтобы UI не стал unwieldy.

**Примеры:**
| Распределение | Когда выбрать |
|---------------|---------------|
| 3 × `[Light, Medium]` | Только Light/Medium корабли |
| 3 × `[Light, Medium]` + 2 × `[Medium, Heavy, HeavyII]` | Универсальная станция |
| 1 × `[Heavy, HeavyII]` | Тяжёлый сектор |
| 5 × `[]` (для всех) | Один тип pad на всех |

### 6.4 Проверка совместимости

Серверная функция в `DockingWorld` (см. `02_V2_ARCHITECTURE.md` §6.1):

```csharp
private bool IsCompatible(ShipFlightClass[] allowed, ShipFlightClass shipClass) {
    if (allowed == null || allowed.Length == 0) return true;
    foreach (var s in allowed) if (s == shipClass) return true;
    return false;
}
```

**Расширение Phase 3:** если у игрока есть модуль `MODULE_AUTO_DOCK` —
сервер автоматически выбирает ближайший подходящий pad (для автопилота).
Для MVP — первый попавшийся в списке pads.

---

## 7. `PadTriggerReference` — мини-маркер

### 7.1 Зачем

Некоторые внешние системы захотят узнать «что за pad ближе к игроку».
Чтобы не лазить по иерархии в поисках `DockingPadTriggerBox`, ставим
на каждый `Pad_*` маркер:

```csharp
namespace ProjectC.Docking.Stations {
    [DisallowMultipleComponent]
    public class PadTriggerReference : MonoBehaviour {
        public DockingPadTriggerBox PadBox { get; private set; }

        private void Awake() {
            PadBox = GetComponent<DockingPadTriggerBox>();
            if (PadBox == null) {
                Debug.LogError($"[PadTriggerReference] No DockingPadTriggerBox on {gameObject.name}", this);
            }
        }
    }
}
```

Использование (из внешних систем):
```csharp
var nearest = FindObjectsByType<PadTriggerReference>(FindObjectsInactive.Exclude)
    .Select(r => r.PadBox)
    .Where(p => p != null && /* другие фильтры */)
    .OrderBy(p => Vector3.Distance(p.transform.position, playerPos))
    .FirstOrDefault();
```

---

## 8. Layout в сцене (пример `DockStation_Primium`)

### 8.1 Координаты для MVP (Q6)

**Q6 (принято):** `DockStation_Primium` ставим в `WorldScene_0_0.unity`
на координатах **(40500, 2510, 40500)** — 500м к NE от `Chest_Main`.

| Объект | Координаты (X, Y, Z) | Размер |
|--------|----------------------|--------|
| `DockStation_Primium` root | (40500, 2510, 40500) | — |
| `OuterCommZone` (центр = root) | (0, 0, 0) local | Sphere radius=**настраивается в Inspector** (default 1000) |
| `Pad_001` | (-12, 0, 0) local | Box 8×3×8 |
| `Pad_002` | (0, 0, 0) local | Box 8×3×8 |
| `Pad_003` | (12, 0, 0) local | Box 8×3×8 |
| `Pad_004` | (-6, 0, -12) local | Box 10×3×10 |
| `Pad_005` | (6, 0, -12) local | Box 10×3×10 |
| `Pad_006` | (0, 0, -24) local | Box 12×3×12 |

**Q4 (без хардкода):** количество и расположение pads — **пример**.
Дизайнер может изменить под свою станцию. `DockStation_Primium`
по умолчанию имеет 6 pads как иллюстрация; реальная `Primium`-станция
может иметь другое количество.

**Все позиции — локальные** относительно `DockStation_Primium`. В инспекторе
расставляет пользователь (тикет T-DOCK-12).

### 8.2 Визуализация (ASCII)

```
           Pad_001 (L+M)
              [  ]
   Pad_002  ──────  Pad_003
     [  ]   STN_01    [  ]     ← OuterCommZone sphere (radius 1000)
              [  ]
           Pad_004   Pad_005
              [  ]    [  ]
               Pad_006
                [  ]
```

Вид сверху. Pads 1-3 — спереди, 4-6 — сзади (для разных подлётов).

### 8.3 Визуальная модель (Q13)

**MVP (Q13):** простые ProBuilder-кубы (pads) + **цифры прямо на mesh'е**
(текстура с цифрой 1, 2, 3...). Дизайнер сам рисует цифры в ProBuilder
или Blender. Никаких floating labels (Q13).

**Пример цифры на pad:**
```
     ┌─────────┐
     │    5    │  ← текстура "5" на верхней грани BoxCollider'а
     │ ┌─────┐ │
     │ │ ░░░ │ │  ← ProBuilder mesh
     │ └─────┘ │
     └─────────┘
```

**Phase 3:** опционально — Editor tool для автогенерации цифр из
`DockPadLayout.padId` (извлечь число, нарисовать текстуру, применить
к pad'у). Для MVP дизайнер делает вручную.

**MVP станция:** маленькая «башня диспетчера» (один cube + emissive материал).

---

## 9. Совместимость с композитным кораблём

### 9.1 Что видит `DockStation` от корабля

`DockStation.PollShipsInRange()` ищет **только корневой** `ShipController`:
```csharp
var ship = hits[i].GetComponentInParent<ShipController>();
if (ship == null || !ship.IsSpawned) continue;
```
Это работает, потому что `ShipController` всегда на корне корабля (см.
`docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md`). Дочерние части корабля
(PilotSeat, Door, ModuleSlot) имеют `ShipRootReference`, но **не
`ShipController`**.

### 9.2 Что видит корабль от `DockStation`

`DockingPadTriggerBox.OnTriggerEnter(other)`:
- `other` — это `Collider` корабля (root BoxCollider или дочерний).
- `other.GetComponentInParent<ShipController>()` — находит root корабля.

Это работает для любого collider корабля, включая:
- Корневой `BoxCollider` (платформа корабля — основной кейс).
- PilotSeat BoxCollider (если игрок стоит на месте пилота).
- Door BoxCollider (если открытая дверь задевает pad).

**Ложные срабатывания** (когда дверь/PilotSeat касается pad, но сам
корабль ещё далеко) — отсекаются проверкой `_isServer` + проверкой
позиции корабля в `DockingServer.NotifyTouchedDownRpc` (distance check).

---

## 10. Альтернативные подходы (отвергнутые)

### 10.1 Один большой BoxCollider вместо маленьких

**Идея:** один BoxCollider вокруг всей платформы, любое касание = стыковка.

**Отклонено:** невозможно определить «на какой pad сел игрок» (нужно
для назначения + для визуала). Невозможно сделать «wrong-pad» warning
(все pads = одна зона).

### 10.2 NetworkBehaviour на каждом pad

**Идея:** каждый `DockingPadTriggerBox : NetworkBehaviour`, спавнится
через ScenePlacedObjectSpawner.

**Отклонено для MVP:**
- ScenePlacedObjectSpawner спавнит по одному NetworkObject'у — 6 pads × N станций = много NetworkObject'ов в сцене.
- Сетевой overhead (RPC на каждый pad вместо одного на сервер).
- Client-side detection проще и достаточно (сервер валидирует).

**Возможно для Phase 3:** если понадобится authority на стороне сервера
(anti-cheat: «игрок не может телепортироваться на pad»), поднимаем до NetworkBehaviour.

### 10.3 NavMesh + AI

**Идея:** пилот после назначения автоматически летит к pad'у по NavMesh.

**Отклонено:** Project C — летающие корабли, NavMesh для 3D-воздуха
избыточен. Автопилот (Phase 2) — это `MODULE_AUTO_DOCK`, не AI-навгация.

---

## 11. Тестовая сцена (T-DOCK-12 verification)

### 11.1 Чеклист расстановки

```
WorldScene_0_0:
├── [Chest_Main] (anchor, не трогаем) — (40000, 2502.77, 40000)
├── DockStation_Primium
│   ├── NetworkObject + DockStationController (dockStationDefinition assigned)
│   ├── StationRootReference
│   ├── OuterCommZone (sphere radius=1000)
│   ├── Pad_001 (BoxCollider trigger, DockingPadTriggerBox padId="PAD-001")
│   ├── Pad_002 ...
│   └── Pad_006
└── (опц.) Второй DockStation для теста multi-station — Phase 3

BootstrapScene:
└── [DockingServer] (NetworkObject, NetworkBehaviour, dockStationDefinition=N/A)
```

### 11.2 Минимальные тестовые сценарии

1. **Player outside zone:** T не открывает CommPanel. Hint не появляется.
2. **Player in zone (пешком):** Hint «T — связаться с диспетчером». T → CommPanel. Видно 6 pads.
3. **Player in zone (в корабле):** То же + можно запросить посадку с конкретным ship.
4. **Server assigns pad:** CommPanel показывает «Pad #N, окно X сек».
5. **Player flies to pad:** Коммуникация работает в воздухе.
6. **Wrong pad:** Приземление на свободный, но неправильный → toast warning.
7. **Correct pad:** Приземление → Docked, Engine Off, F для отстыковки.
8. **Window expires:** Через 90 сек без посадки → Assigned → Cancelled.

---

## 12. Связь с другими документами

| Документ | Что используем |
|----------|----------------|
| `02_V2_ARCHITECTURE.md` §4 `DockingServer` | hub, RPCs |
| `02_V2_ARCHITECTURE.md` §6 `DockingWorld` | server state, AssignPad |
| `02_V2_ARCHITECTURE.md` §10 FSM расширение | ShipController.IsDocked |
| `04_DIALOG_AND_DISPATCHER_UI.md` | UI на CommPanel |
| `05_FLOW_AND_INTERACTION.md` | полный поток от T до Docked |
| `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` | composite pattern |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | zone-trigger pattern |
| `Assets/_Project/Scripts/Player/ShipController.cs` | ShipFlightClass |

---

*Создано: 2026-06-19 | Аналитическая сессия | Без кода.*