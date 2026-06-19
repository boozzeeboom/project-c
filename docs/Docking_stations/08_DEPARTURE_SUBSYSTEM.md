# 08 — Departure Subsystem (Q8)

> **Статус:** Дизайн-эскиз (2026-06-19). **Не входит в MVP docking** (Phase 1.5).
> Документ — задел на будущее. Кодинг **только после T-DOCK-* полностью завершён**.

---

## 1. Назначение

**Q8 (принято 2026-06-19):** вылет из OuterCommZone должен быть **по
запросу разрешения**. F остаётся boarding безусловно (НЕ блокируется),
но **toast-предупреждение** если пилот улетел без запроса.

**Цель подсистемы Departure:**
1. Игрок в корабле в OuterCommZone → T → `Запросить вылет` → ожидание → `Вылет разрешён` → может лететь.
2. Если игрок улетел без запроса → toast-предупреждение (через 5 сек после выхода из зоны).
3. NPC-диспетчер авторизует вылет по правилам (Phase 2: репутация, маршрут, груз).

**Почему отдельная подсистема:**
- Другая семантика (не «назначить место», а «получить разрешение»).
- Архитектурно похожа на docking (server hub + RPCs + ClientState), но
  другая state machine.
- Phase 2 расширение: NPC-правила, репутация, наказания.

---

## 2. Архитектура (эскиз)

### 2.1 Серверный хаб: `DepartureServer`

```csharp
// Assets/_Project/Scripts/Departure/Network/DepartureServer.cs
[RequireComponent(typeof(NetworkObject))]
public class DepartureServer : NetworkBehaviour {
    public static DepartureServer Instance { get; private set; }

    // Pending departures: clientId → PendingDeparture
    private readonly Dictionary<ulong, PendingDeparture> _pending = new Dictionary<ulong, PendingDeparture>();

    private struct PendingDeparture {
        public ulong shipNetId;
        public float requestedAt;
        public float waitingWindow;  // 30 сек
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if (Instance == null) Instance = this;
        if (!IsServer) { enabled = false; return; }
        // Init ...
    }

    // Клиент → Сервер: игрок нажал "Запросить вылет"
    [Rpc(SendTo.Server)]
    public void RequestDeparturePermissionRpc(ulong shipNetId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (IsRateLimited(clientId)) return;

        _pending[clientId] = new PendingDeparture {
            shipNetId = shipNetId,
            requestedAt = Time.time,
            waitingWindow = 30f
        };

        // Симулируем обработку (для MVP — мгновенное разрешение)
        // Phase 2: проверить репутацию, маршрут, груз
        SendDeparturePermissionTargetRpc(clientId, shipNetId, true);
    }

    // TargetRpc
    [Rpc(SendTo.SpecifiedInParams)]
    private void SendDeparturePermissionTargetRpc(ulong clientId, ulong shipNetId, bool granted, RpcParams rpcParams = default) {
        DepartureClientState.Instance?.HandlePermissionReceived(granted);
    }
}
```

### 2.2 Клиентская проекция: `DepartureClientState`

```csharp
// Assets/_Project/Scripts/Departure/Client/DepartureClientState.cs
public class DepartureClientState : MonoBehaviour {
    public static DepartureClientState Instance { get; private set; }

    public event Action OnPermissionGranted;
    public event Action OnPermissionDenied;
    public event Action OnViolationDetected;  // Q8: вылет без запроса

    public bool HasDeparturePermission { get; private set; }

    public void HandlePermissionReceived(bool granted) {
        if (granted) {
            HasDeparturePermission = true;
            OnPermissionGranted?.Invoke();
        } else {
            OnPermissionDenied?.Invoke();
        }
    }

    public void HandleViolationDetected() {
        OnViolationDetected?.Invoke();
    }
}
```

### 2.3 UI: добавить в CommPanel

В текущем `CommPanelWindow` добавить новый state `AwaitingTakeoffPermission`:

| Status | Header | Message | Buttons |
|--------|--------|---------|---------|
| **AwaitingTakeoffPermission** | `[Станция] — Диспетчерская` | «Диспетчер: «Запрос принят, ожидайте разрешения на вылет»» | `[Отменить]` |
| **TakeoffGranted** | То же | «Диспетчер: «Вылет разрешён. Доброго пути»» | `[Закрыть]` |
| **TakeoffDenied** | То же | «Диспетчер: «Вылет запрещён. Причина: ...»» | `[Закрыть]` |

В режиме Docked + кнопка `Запросить вылет` (рядом с `F — Отстыковка`).

### 2.4 Violation detection

```csharp
// В OuterCommZone или отдельном компоненте на root'е DockStation:

private void OnTriggerExit(Collider other) {
    var ship = other.GetComponentInParent<ShipController>();
    if (ship == null) return;
    if (!ship.IsSpawned) return;
    var netPlayer = FindOwnerOfShip(ship);
    if (netPlayer == null || !netPlayer.IsOwner) return;
    // Клиент: если нет permission — это violation
    if (!DepartureClientState.Instance.HasDeparturePermission) {
        DepartureClientState.Instance.HandleViolationDetected();
    }
}
```

**Тост violation (Q8):** через 5 сек после выхода из зоны (чтобы дать
время на «опомниться»). Можно использовать `CommPanelToast` или новый
`DepartureToast`.

---

## 3. RPCs (полный список)

| RPC | Direction | Описание |
|-----|-----------|----------|
| `RequestDeparturePermissionRpc(shipNetId)` | Client → Server | Игрок запрашивает разрешение |
| `SendDeparturePermissionTargetRpc(clientId, granted, reason)` | Server → Client | Сервер выдаёт/отказывает |
| `NotifyDepartureViolationRpc(shipNetId)` | Client → Server | Игрок улетел без запроса (для логов/штрафов Phase 2) |

---

## 4. Edge-cases

| Случай | Поведение |
|--------|-----------|
| Игрок запрашивает вылет не в OuterCommZone | Сервер: `failReason="NOT_IN_ZONE"` |
| Игрок улетает в течение 30 сек после Granted | Норма (разрешение есть) |
| Игрок улетает без запроса | Через 5 сек после выхода → violation toast |
| Игрок запрашивает вылет 2 раза подряд | Второй отменяет первый (новый pending) |
| Pending истёк (30 сек) | Сервер `SendDeparturePermissionTargetRpc(granted=false, reason="TIMEOUT")` |
| Игрок disconnect во время Awaiting | Cleanup pending в `OnClientDisconnected` |

---

## 5. Milestone: T-DEPART-*

| Тикет | Описание | LOC | Часов |
|-------|----------|-----|-------|
| T-DEPART-00 | `DepartureWorld` singleton (server state) + DTO `DeparturePermissionDto` | 200 | 3 |
| T-DEPART-01 | `DepartureServer` hub + RPCs + rate limiting | 250 | 4 |
| T-DEPART-02 | `DepartureServer` placement в BootstrapScene | 30 (MCP) | 1 |
| T-DEPART-03 | `DepartureClientState` + подписки в CommPanelWindow | 200 | 3 |
| T-DEPART-04 | Violation detection в OuterCommZone + DepartureToast | 150 | 2.5 |
| T-DEPART-05 | Testing guide + документация | 100 (doc) | 1.5 |
| **Итого** | | **~930 LOC** | **~15 часов** |

**8 → 11 сессий** (если включаем Departure).

---

## 6. Phase 2 расширения

| Фича | Описание |
|------|----------|
| NPC-правила вылета | Проверка репутации, маршрута, груза перед выдачей разрешения |
| Штрафы за вылет без запроса | Снятие репутации, проверка на наличие SOL (Law) |
| Takeoff autopilot | Модуль `MODULE_AUTO_TAKEOFF` (не в MVP) |
| NPC-корабли вылетают/улетают | Phase 3 — NPC traffic |

---

## Связь с другими документами

| Документ | Что |
|----------|-----|
| `02_V2_ARCHITECTURE.md` §6 DockingWorld | `ReleaseAssignment` интегрируется с `DepartureServer` |
| `05_FLOW_AND_INTERACTION.md` §2.6 | Сценарий «вылет без запроса» |
| `00_README.md` | `08_DEPARTURE_SUBSYSTEM.md` в навигации |
| `06_ROADMAP.md` §5 Departure | T-DEPART-* тикеты |

---

*Создано: 2026-06-19 | Phase 1.5 (после Docking MVP) | Без кода.*