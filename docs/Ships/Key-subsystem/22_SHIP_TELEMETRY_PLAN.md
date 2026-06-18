# Ship Telemetry — UI/HUD проекция состояния корабля по ключу

**Подсистема:** Корабли — игрок видит актуальное состояние своих кораблей (груз, fuel, модули, позиция) в UI **и HUD**
**Тег:** `ship-telemetry`, `network-variable`, `my-ships-ui`, `key-driven-state`, `hud-telemetry`
**Статус:** 📋 Дизайн готов, код НЕ написан
**Дата:** 2026-06-18 (обновлено: 2026-06-18 — Q4: NetworkVariable-based вместо polling RPC)
**Связанные документы:**
- `20_UNIQUE_KEY_INSTANCE.md` — KeyRodInstance
- `21_SHIP_OWNERSHIP_MODEL.md` — кто чем владеет
- `23_ROADMAP.md` — тикеты
- `docs/gdd/GDD_10_Ship_System.md` §6 (Co-Op), §8 (State Machine)

---

## 1. Проблема (со слов пользователя, 2026-06-18)

> *"сейчас я технически могу мир наполнить 200 кораблями, и для каждого сделать ключ со своими ID и разложить их уже будет работать правило 1 ключ 1 корабль. поэтому важно сконцентрироваться на понимании: что сервер может игроку передать информацию про корабль по его ключу. условно мы сделаем потом UI который игрок открывает и получает информацию по своим кораблям, а своими считаются те - ключи которых лежат у него в инвентаре, тоесть сервер смотрит что за ключи какие от них корабли и передает игроку информацию о грузах в корабле где он и тд"*

### 1.1 Уточнение Q4 (2026-06-18)

> *"нужен NetworkVariable-based - будут HUD и UI связанные на актуальных данных."*

**Изменение дизайна**: вместо **polling RPC** (клиент запрашивает snapshot по требованию) — **NetworkVariable-based push** (сервер автоматически синхронизирует состояние кораблей всем заинтересованным клиентам). Это покрывает:
- **HUD** — постоянно актуальные данные (топливо, груз, состояние пилотирования) **без ручного refresh**.
- **UI "Мои корабли"** — открывается с уже актуальными данными (не нужно ждать RPC).
- **Multi-client** — если A передал ключ B, оба видят обновлённый ownership без явных RPC.

---

## 2. Архитектура — NetworkVariable-based

### 2.1 Концепция

```
ShipController (server-side authority)
    └── NetworkVariable<ShipTelemetryState> _state
        └── автоматически синхронизируется всем клиентам

ShipTelemetryClientState (client-side aggregation)
    └── Dictionary<ulong /*shipNetId*/, ShipTelemetryState>
        └── подписан на все NetworkVariable через сервер-агрегатор
    └── event OnShipStateChanged(shipNetId) — для UI/HUD

ShipOwnershipRegistry (server-side, NetworkBehaviour)
    └── хранит mapping shipNetId → clientId (кому слать)
    └── NetworkList<OwnershipEntry> _ownershipList
        └── синхронизируется клиентам для фильтрации "моих" кораблей
```

**Ключевая идея**: серверная сторона корабля — single source of truth. Данные летят клиентам **через стандартный NGO механизм NetworkVariable**. Клиент только фильтрует "мои" vs "чужие" через `ShipOwnershipRegistry`.

### 2.2 ShipTelemetryState (NetworkVariable payload)

```csharp
namespace ProjectC.Ship.Network
{
    /// <summary>Server-authoritative state корабля. Полностью синхронизируется
    /// клиентам через NetworkVariable. Хранит достаточно данных для HUD + UI.</summary>
    public struct ShipTelemetryState : INetworkSerializable, IEquatable<ShipTelemetryState>
    {
        public ulong  shipNetworkObjectId;
        public int    keyInstanceId;          // → KeyRodInstance.instanceId (0 = не привязан)
        public FixedString64Bytes displayName;
        public FixedString32Bytes className;
        public Vector3 position;
        public Vector3 rotationEuler;
        public float  fuelNormalized;         // 0..1
        public float  fuelMax;                // абсолютный максимум
        public int    cargoUsed;
        public int    cargoMax;
        public int    moduleCount;
        public byte   state;                  // (byte)ShipState enum
        public ulong  ownerClientId;          // ← кто владеет ключом (для фильтрации)
        public double lastUpdateServerTime;   // для отладки stale-данных

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref shipNetworkObjectId);
            serializer.SerializeValue(ref keyInstanceId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref className);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotationEuler);
            serializer.SerializeValue(ref fuelNormalized);
            serializer.SerializeValue(ref fuelMax);
            serializer.SerializeValue(ref cargoUsed);
            serializer.SerializeValue(ref cargoMax);
            serializer.SerializeValue(ref moduleCount);
            serializer.SerializeValue(ref state);
            serializer.SerializeValue(ref ownerClientId);
            serializer.SerializeValue(ref lastUpdateServerTime);
        }

        public bool Equals(ShipTelemetryState other) =>
            shipNetworkObjectId == other.shipNetworkObjectId
            && keyInstanceId == other.keyInstanceId
            && displayName.Equals(other.displayName)
            && className.Equals(other.className)
            && position == other.position
            && rotationEuler == other.rotationEuler
            && Mathf.Approximately(fuelNormalized, other.fuelNormalized)
            && Mathf.Approximately(fuelMax, other.fuelMax)
            && cargoUsed == other.cargoUsed
            && cargoMax == other.cargoMax
            && moduleCount == other.moduleCount
            && state == other.state
            && ownerClientId == other.ownerClientId;
        // + GetHashCode hand-rolled (см. project-c-netcode-patterns §19a)
    }
}
```

### 2.3 ShipController — добавление NetworkVariable

```csharp
// В Assets/_Project/Scripts/Player/ShipController.cs (дополнение)

public class ShipController : NetworkBehaviour
{
    // Существующее: thrustForce, maxSpeed, etc.

    /// <summary>Server-authoritative state корабля. Автоматически синхронизируется
    /// клиентам через NGO. Server пишет в FixedUpdate / при изменении.
    /// Клиент читает через ShipTelemetryClientState.</summary>
    private NetworkVariable<ShipTelemetryState> _telemetryState = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Доступ для клиентов через ShipTelemetryClientState.</summary>
    public ShipTelemetryState TelemetryState => _telemetryState.Value;

    // ============================================================
    // Server-side: обновление state (вызывается в FixedUpdate / при событиях)
    // ============================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;
        // ... existing thrust/lift logic ...

        // Snapshot update (раз в ~0.2s, см. throttle ниже)
        UpdateTelemetryState();
    }

    private float _lastTelemetryUpdate = -10f;
    private const float TELEMETRY_UPDATE_INTERVAL = 0.2f;  // 5 Hz

    private void UpdateTelemetryState()
    {
        if (Time.time - _lastTelemetryUpdate < TELEMETRY_UPDATE_INTERVAL) return;
        _lastTelemetryUpdate = Time.time;

        var inst = KeyRodInstanceWorld.GetInstance(
            KeyRodInstanceWorld.GetInstanceIdForShip(NetworkObjectId));
        ulong ownerId = inst?.ownerPlayerId ?? OWNER_NONE;

        _telemetryState.Value = new ShipTelemetryState
        {
            shipNetworkObjectId = NetworkObjectId,
            keyInstanceId       = inst?.instanceId ?? 0,
            displayName         = ShipDisplayName,
            className           = ShipClass.ToString(),
            position            = transform.position,
            rotationEuler       = transform.rotation.eulerAngles,
            fuelNormalized      = GetFuelNormalized(),
            fuelMax             = MaxFuel,
            cargoUsed           = CargoUsed,
            cargoMax            = CargoMax,
            moduleCount         = ModuleManager?.InstalledCount ?? 0,
            state               = (byte)CurrentState,
            ownerClientId       = ownerId,
            lastUpdateServerTime = NetworkManager.ServerTime.Time,
        };
    }

    /// <summary>Кастомное имя из инспектора. Если пусто — автогенерация из class + instanceId
    /// (Q6, 2026-06-18: подтягивается к ключу).</summary>
    [SerializeField] private string _customDisplayName = "";
    public string ShipDisplayName =>
        !string.IsNullOrEmpty(_customDisplayName)
            ? _customDisplayName
            : (KeyRodInstanceWorld.GetInstance(
                   KeyRodInstanceWorld.GetInstanceIdForShip(NetworkObjectId))
               is KeyRodInstance inst
               ? $"{ShipClass} #{inst.instanceId:D4}"
               : $"{ShipClass} #{NetworkObjectId}");
}
```

### 2.4 ShipOwnershipRegistry (NetworkBehaviour, Bootstrap)

```csharp
namespace ProjectC.Ship.Network
{
    /// <summary>Server-side реестр ownership для всех кораблей. NetworkList синхронизируется
    /// клиентам — каждый клиент фильтрует "мои" корабли локально.</summary>
    [DisallowMultipleComponent]
    public class ShipOwnershipRegistry : NetworkBehaviour
    {
        public static ShipOwnershipRegistry Instance { get; private set; }

        [Serializable]
        public struct OwnershipEntry : INetworkSerializable, IEquatable<OwnershipEntry>
        {
            public ulong shipNetworkObjectId;
            public ulong ownerClientId;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref shipNetworkObjectId);
                s.SerializeValue(ref ownerClientId);
            }

            public bool Equals(OwnershipEntry other) =>
                shipNetworkObjectId == other.shipNetworkObjectId
                && ownerClientId == other.ownerClientId;
        }

        private NetworkList<OwnershipEntry> _ownership = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public IReadOnlyList<OwnershipEntry> Ownership => _ownership;

        // ============================================================
        // Server-side: обновление при изменении ownership
        // ============================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;
            if (IsServer)
            {
                // Подписка на изменения в KeyRodInstanceWorld
                KeyRodInstanceWorld.OnOwnershipChanged += HandleOwnershipChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                KeyRodInstanceWorld.OnOwnershipChanged -= HandleOwnershipChanged;
            }
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private void HandleOwnershipChanged(int instanceId, ulong newOwner)
        {
            // Найти ship по instanceId и обновить запись
            ulong shipNetId = KeyRodInstanceWorld.GetShipIdForInstance(instanceId);
            if (shipNetId == 0) return;

            // Заменяем или добавляем запись
            for (int i = 0; i < _ownership.Count; i++)
            {
                if (_ownership[i].shipNetworkObjectId == shipNetId)
                {
                    var entry = _ownership[i];
                    entry.ownerClientId = newOwner;
                    _ownership[i] = entry;
                    return;
                }
            }
            _ownership.Add(new OwnershipEntry { shipNetworkObjectId = shipNetId, ownerClientId = newOwner });
        }
    }
}
```

### 2.5 ShipTelemetryClientState (singleton)

```csharp
namespace ProjectC.Ship.Client
{
    public class ShipTelemetryClientState : MonoBehaviour
    {
        public static ShipTelemetryClientState Instance { get; private set; }

        // shipNetId → последний известный state (агрегация со всех ShipController)
        private readonly Dictionary<ulong, ShipTelemetryState> _allShips = new();

        // shipNetId → cached owner (из ShipOwnershipRegistry)
        private readonly Dictionary<ulong, ulong> _ownershipCache = new();

        public event Action<ulong> OnShipStateChanged;  // (shipNetId) — для HUD/UI
        public event Action OnOwnershipUpdated;         // после любого ownership change

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>Все корабли текущего клиента (по ownership).</summary>
        public IEnumerable<KeyValuePair<ulong, ShipTelemetryState>> MyShips
        {
            get
            {
                ulong myClientId = NetworkManager.Singleton?.LocalClientId ?? 0;
                foreach (var kvp in _allShips)
                {
                    if (_ownershipCache.TryGetValue(kvp.Key, out var owner)
                        && owner == myClientId)
                    {
                        yield return kvp;
                    }
                }
            }
        }

        public ShipTelemetryState? GetMyShip(ulong shipNetId)
            => IsMyShip(shipNetId) ? _allShips[shipNetId] : (ShipTelemetryState?)null;

        public bool IsMyShip(ulong shipNetId)
            => _ownershipCache.TryGetValue(shipNetId, out var owner)
               && owner == (NetworkManager.Singleton?.LocalClientId ?? 0);

        // ============================================================
        // Server → Client delivery (вызывается из NetworkPlayer или напрямую)
        // ============================================================

        /// <summary>Вызывается при любом изменении NetworkVariable на ShipController.
        /// ShipController сам триггерит это через [ServerRpc/OwnerRpc] хук
        /// (см. §2.6 "Push notification").</summary>
        public void OnShipTelemetryUpdated(ulong shipNetId, ShipTelemetryState state)
        {
            _allShips[shipNetId] = state;
            try { OnShipStateChanged?.Invoke(shipNetId); }
            catch (Exception ex) { Debug.LogError($"... {ex}"); }
        }

        /// <summary>ShipOwnershipRegistry.OnListChanged → обновляем кэш ownership.</summary>
        public void OnOwnershipListUpdated(IReadOnlyList<ShipOwnershipRegistry.OwnershipEntry> entries)
        {
            _ownershipCache.Clear();
            foreach (var e in entries)
            {
                _ownershipCache[e.shipNetworkObjectId] = e.ownerClientId;
            }
            try { OnOwnershipUpdated?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"... {ex}"); }
        }
    }
}
```

### 2.6 Push notification с сервера на клиент

NetworkVariable автоматически синхронизирует VALUE, но не вызывает custom callback на клиенте "при любом изменении". Решение — **server-side хук + Target RPC** для уведомления заинтересованных клиентов:

```csharp
// В ShipController (server side)
private ShipTelemetryState _lastBroadcastedState;
private float _lastBroadcastTime = -10f;
private const float BROADCAST_INTERVAL = 0.5f;  // 2 Hz (вместо 5 Hz у UpdateTelemetryState)

private void MaybeBroadcastToInterestedClients()
{
    if (Time.time - _lastBroadcastTime < BROADCAST_INTERVAL) return;
    if (!IsServer) return;

    // Сравнить с последним broadcast (упрощённая проверка — полная в §6 edge-cases)
    if (_telemetryState.Value.Equals(_lastBroadcastedState)) return;

    _lastBroadcastTime = Time.time;
    _lastBroadcastedState = _telemetryState.Value;

    // Найти всех клиентов, у которых этот корабль в "моих" (по ownership)
    ulong ownerId = _telemetryState.Value.ownerClientId;
    var nm = NetworkManager.Singleton;
    if (nm == null) return;

    if (ownerId != OWNER_NONE && nm.ConnectedClients.TryGetValue(ownerId, out var client))
    {
        var playerObj = client.PlayerObject;
        if (playerObj != null)
        {
            var netPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (netPlayer != null)
            {
                netPlayer.NotifyShipTelemetryChangedRpc(_telemetryState.Value);
            }
        }
    }
}
```

```csharp
// В NetworkPlayer (дополнение)
[Rpc(SendTo.Owner)]
public void NotifyShipTelemetryChangedRpc(ShipTelemetryState state)
{
    ShipTelemetryClientState.Instance?.OnShipTelemetryUpdated(state.shipNetworkObjectId, state);
}
```

**Оптимизация**: broadcast только владельцу (не всем клиентам). Это снижает трафик до N×1 (где N = количество владельцев = количество кораблей), а не M×N (M клиентов).

**Throttle**: 2 Hz для изменений (HUD не требует 60 Hz). State синхронизируется через NetworkVariable в любом случае — broadcast это только сигнал клиенту "обнови UI".

### 2.7 ShipOwnershipRegistry → ShipTelemetryClientState на клиенте

```csharp
// На клиенте (через NetworkVariable callback или прямой подписки):
public class ShipOwnershipClientHook : NetworkBehaviour  // на ShipOwnershipRegistry GameObject
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsClient) return;
        // Subscribe to NetworkList changes
        // (NGO 2.x: используется OnListChanged event на NetworkList<T>)
        GetComponent<NetworkBehaviour>().OnListChanged += HandleListChanged;
        // Альтернатива: hook через OnOwnershipUpdated callback от NetworkList
    }

    private void HandleListChanged(NetworkListEvent<ShipOwnershipRegistry.OwnershipEntry> ev)
    {
        // Re-fetch full list (NetworkList не даёт diff напрямую в общем случае)
        var registry = ShipOwnershipRegistry.Instance;
        if (registry != null)
        {
            ShipTelemetryClientState.Instance?.OnOwnershipListUpdated(registry.Ownership);
        }
    }
}
```

**Важно**: NetworkList события доступны через `OnListChanged` на сервере. Для клиента — нужно использовать `[ClientRpc]` или другой механизм. Конкретная имплементация уточняется в T-KEY-07.

---

## 3. Use cases

### 3.1 Игрок открывает "Мои корабли" tab (HUD уже актуален)

```
1. CharacterWindow открывается → MyShipsTab.OnEnable
2. MyShipsTab.RebuildList() читает ShipTelemetryClientState.MyShips
3. (данные УЖЕ синхронизированы — никакого RPC не нужно)
4. ListView отображает
5. При любом изменении (fuel упал, cargo загружен) → OnShipStateChanged → list refresh
```

### 3.2 Игрок кликает на корабль в списке

```
1. MyShipsTab: клик на row с shipNetId=5
2. → читает ShipTelemetryClientState.GetMyShip(5)
3. → отображает детали (cargo, fuel, modules, position)
4. (никакого RPC — данные уже там)
```

### 3.3 Игрок передал ключ → UI обновляется у обоих

```
1. A дропнул ключ → InventoryServer.TryDrop → KeyRodInstance.TransferInstance(42, A, NONE)
2. KeyRodInstanceWorld.OnOwnershipChanged(42, NONE) → server
3. ShipOwnershipRegistry.HandleOwnershipChanged → обновляет NetworkList
4. NetworkList синхронизируется всем клиентам
5. У A: ShipTelemetryClientState.OnOwnershipListUpdated → убирает корабль из MyShips
6. У B: ShipTelemetryClientState.OnOwnershipListUpdated → добавляет корабль в MyShips (после pickup)
```

### 3.4 Игрок A на корабле, B подошёл — UI B показывает "Чужой корабль"

```
1. A летит → ShipController.FixedUpdate → UpdateTelemetryState (5 Hz)
2. NetworkVariable sync → все клиенты получают state
3. У B: ShipTelemetryClientState.OnShipTelemetryChanged → не показывает как "мой"
4. B нажимает F → ShipOwnershipRequirement.IsOwner(B) = false → denied
```

---

## 4. UI (отложено — UI делается в отдельном ticket)

`MyShipsTab.cs` в `CharacterWindow` (5-й tab "Корабли"):

```
[MyShipsTab] в CharacterWindow
├── ListView (left): список кораблей (icon + name + state)
│   └── bindItem читает ShipTelemetryClientState.MyShips (уже синхронизировано)
├── Detail panel (right): cargo / fuel / modules / position
│   └── bindItem читает ShipTelemetryClientState.GetMyShip(shipNetId)
├── Empty state: "У вас нет кораблей. Найдите ключ-стержень на верфи или в мире."
└── (auto-refresh через OnShipStateChanged — никаких кнопок)
```

Делается **отдельным тикетом T-KEY-08** после T-KEY-07 (server-side + client state готов).

---

## 5. Edge-cases

| Кейс | Решение |
|---|---|
| **Передача ключа (race condition)** | `KeyRodInstanceWorld.TransferInstance` — server-authoritative, NetworkList синхронизируется автоматически. Оба клиента увидят изменение в один и тот же tick |
| **200 кораблей → bandwidth** | Broadcast только владельцу (N×1 = 200 пакетов на broadcast, не 200×M). Throttle 2 Hz. Payload ~80 bytes на state — итого ~32 KB/s на клиента при максимуме |
| **ShipController не заспавнился (scene transition)** | `_allShips` хранит последний известный state. NetworkVariable имеет встроенный handling для late-join (NGO sync). Client просто получит state когда объект появится |
| **Чит: подмена NetworkVariable** | NGO серверно авторитетен — клиент не может писать в `_telemetryState.Value` (только Server permission). Клиент читает `OnValueChanged` |
| **HUD spam (5 Hz на все корабли)** | `MaybeBroadcastToInterestedClients` throttled 2 Hz + equality check → реальный broadcast только при изменениях |
| **Player disconnect с ключом** | `KeyRodInstance.state = Lost` (instance остаётся в мире). HUD не показывает корабль никому (ownerClientId = OWNER_NONE). После pickup — нормальный flow |
| **Корабль уничтожен (state=Destroyed)** | `ShipController.OnDestroy` → сервер пишет `_telemetryState.Value.state = Destroyed`. Клиенты видят обновление. UI скрывает корабль (фильтр state != Destroyed) |
| **Несколько клиентов с одним ключом (баг)** | Не должно быть — `KeyRodInstance.ownerPlayerId` всегда один. Если кто-то подделал NetworkVariable — NGO reject |
| **Q8: pilotCount в snapshot** | Убран из MVP. NetworkVariable хранит только данные, не зависящие от multiplayer (cargo/fuel/modules/state). Pilot state — это про co-op (фаза 2, GDD_10 §6) |

---

## 6. Что НЕ входит в MVP

- ❌ **Cargo items breakdown в state** (фаза 2, отдельная Cargo UI)
- ❌ **Module swap через UI** (отдельная подсистема)
- ❌ **Multi-pilot display** (фаза 2, GDD_10 §6)
- ❌ **Telemetry для не-владельцев** (фаза 2, public info channel)
- ❌ **NetworkVariableWritePermission.Owner** (не нужно — server-authoritative всегда)
- ❌ **Push to ALL clients** (сейчас только владельцу)

---

## 7. Сравнение с polling-RPC (отклонённый вариант)

| Аспект | Polling RPC (было) | NetworkVariable-based (выбрано) |
|---|---|---|
| **HUD актуальность** | Нет (обновляется только по запросу) | ✅ Да (5 Hz push) |
| **UI открытие** | RPC → ждать ответ → отрисовать | Мгновенно (state уже синхронизирован) |
| **Bandwidth** | 0 в покое, всплеск при запросе | Постоянный ~32 KB/s при 200 кораблях (всем владельцам) |
| **Сложность кода** | 1 RPC + 1 DTO + snapshot builder | 1 NetworkVariable + 1 NetworkList + throttled broadcast |
| **Late-join client** | Получает snapshot при connect | NGO автоматически синхронизирует |
| **Race conditions** | Возможны (transfer во время snapshot) | Меньше (NGO server-authoritative) |

**Trade-off**: больше bandwidth, но **значительно лучше UX** для HUD/UI. Для 200 кораблей bandwidth всё равно мал.

---

## 8. Ссылки

- `20_UNIQUE_KEY_INSTANCE.md` §2 — KeyRodInstance
- `21_SHIP_OWNERSHIP_MODEL.md` §2 — ownership + NetworkBehaviour
- `23_ROADMAP.md` T-KEY-07 — этот ticket
- `24_OPEN_QUESTIONS.md` Q4 — решение NetworkVariable-based
- `Assets/_Project/Scripts/Player/ShipController.cs` — host для NetworkVariable
- `Assets/_Project/Scripts/Ship/ShipCargoRegistry.cs` — cargo данные
- `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs` — fuel данные
- `Assets/_Project/Scripts/Ship/ShipModuleManager.cs` — module данные
- `docs/gdd/GDD_10_Ship_System.md` §8 — ShipState enum
- `unity-v2-subsystem-migration` skill — server-hub + DTO + ClientState паттерн
- `project-c-netcode-patterns` skill — §19 (DTO struct), §19a (HashCode arity), §26 (NetworkVariable)

---

**Обновлено:** 2026-06-18 — первичный дизайн (polling RPC).
**Обновлено:** 2026-06-18 — Q4: переход на NetworkVariable-based для поддержки HUD на актуальных данных.