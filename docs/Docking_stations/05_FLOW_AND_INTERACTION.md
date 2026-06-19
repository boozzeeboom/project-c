# 05 — Flow & Interaction

> **Цель:** Описать полный поток стыковки от входа игрока в OuterCommZone
> до отстыковки. Включает горячие клавиши, edge-cases, последовательность
> событий и сетевой контракт. **Q8 (2026-06-19) — вылет через T → запрос
> разрешения — это отдельная подсистема Departure** (см. `08_DEPARTURE_SUBSYSTEM.md`).
> В этом файле только docking.

---

## 1. Горячие клавиши (Q1: T для CommPanel, Q9: F = boarding всегда)

### 1.1 Новые клавиши

| Клавиша | Действие | Контекст |
|---------|----------|----------|
| **T** | Открыть/закрыть CommPanel | Только если **игрок пилотирует корабль** (Q10) **И** рядом `OuterCommZone` (пешком или в корабле) |
| **Esc** | Закрыть CommPanel | Если открыт |

### 1.2 Изменения в существующих клавишах

| Клавиша | Действие | Изменение |
|---------|----------|-----------|
| **F** | Сесть/выйти из корабля | **Без изменений** (Q9: F = boarding всегда, даже внутри CommPanel). CommPanel **закрывается**. |
| **E** | Подобрать/взаимодействовать | Без изменений (стиковка не использует E) |

### 1.3 Полный F-key pipeline (Q8: F НЕ меняется)

```csharp
// В NetworkPlayer.cs, F-key handler:

// 1. Если открыт CommPanel — Esc-like закрытие
if (CommPanelWindow.Instance != null && CommPanelWindow.Instance.IsOpen) {
    CommPanelWindow.Instance.SetOpen(false);
    // Не return — продолжаем boarding-логику
}

// 2. Стандартная цепочка:
//    a. if _inShip → SubmitSwitchModeRpc() — выход
//    b. else → boarding ship — посадка
```

**Q8 (принято):** F остаётся boarding безусловно. Не блокируем, не
toast-предупреждаем. Если игрок вылетает из OuterCommZone без запроса —
это **ошибка**, но **НЕ блокируется** в MVP (toast-предупреждение
планируется в подсистеме Departure, см. `08_DEPARTURE_SUBSYSTEM.md`).

### 1.4 T-key handler (новый, Q1+Q10)

```csharp
// В PlayerInputReader.cs:
public event Action OnCommPanelPressed;

private void OnCommPanelActionPerformed(InputAction.CallbackContext ctx) {
    OnCommPanelPressed?.Invoke();
}

// В NetworkPlayer.cs:
private void OnCommPanelPressed() {
    if (!IsOwner) return;
    if (CommPanelWindow.Instance == null) return;
    // Q10: T игнорируется если игрок не пилотирует корабль
    if (!DockingClientState.IsLocalPlayerPilotingShip()) return;
    var station = DockingZoneRegistry.LocalPlayerStation
              ?? DockingZoneRegistry.LocalPlayerShipStation;
    if (station == null) return;  // не в OuterCommZone
    CommPanelWindow.Instance.ToggleOpen();
}
```

---

## 2. Полный поток стыковки (диаграмма последовательности)

### 2.1 Сценарий «успешная стыковка» (с Q7 двусторонней)

```
PLAYER            NETWORKPLAYER       CLIENTSTATE        DOCKINGSERVER       DOCKINGWORLD      SHIPCTRL
  │                    │                  │                   │                  │               │
  │ press T (in ship)  │                  │                   │                  │               │
  ├───────────────────▶│                  │                   │                  │               │
  │                    │ check station    │                   │                  │               │
  │                    │ in zone?         │                   │                  │               │
  │                    │ (registry)       │                   │                  │               │
  │                    ├──────────────────▶                   │                  │               │
  │                    │                  │                   │                  │               │
  │                    │ ToggleOpen       │                   │                  │               │
  │                    ├──────────────────▶                   │                  │               │
  │                    │                  │                   │                  │               │
  │                    │   UpdateUI (Idle)│                   │                  │               │
  │                    │                  │                   │                  │               │
  │ see greeting       │                  │                   │                  │               │
  │◀───────────────────│                  │                   │                  │               │
  │                    │                  │                   │                  │               │
  │ click "Запросить"  │                  │                   │                  │               │
  ├───────────────────▶│                  │                   │                  │               │
  │                    │ RequestDockingRpc                   │                  │               │
  │                    │ (stationId, shipNetId)              │                  │               │
  │                    ├──────────────────────────────────▶  │                  │               │
  │                    │                  │                   │ AssignPad()      │               │
  │                    │                  │                   ├─────────────────▶│               │
  │                    │                  │                   │                  │               │
  │                    │                  │                   │ RegisterPending  │               │
  │                    │                  │                   │ Assignment       │               │
  │                    │                  │                   │ (Q7: НЕ занимать pad)            │
  │                    │                  │                   │                  │               │
  │                    │                  │                   │ SendAssignmentTargetRpc          │
  │                    │                  │                   │ (padId, voice)   │               │
  │                    │                  │◀──────────────────│                  │               │
  │                    │                  │ OnAwaitingConfirmation               │               │
  │                    │                  │ (success=true)    │                  │               │
  │                    │ UpdateUI (AwaitingConfirmation)    │                  │               │
  │                    │                  │                   │                  │               │
  │ see "Назначаю pad #5" + [Хорошо]/[Отбой]               │                  │               │
  │◀───────────────────│                  │                   │                  │               │
  │                    │                  │                   │                  │               │
  │ click "Хорошо"     │                  │                   │                  │               │
  ├───────────────────▶│                  │                   │                  │               │
  │                    │ RequestConfirmAssignmentRpc(accept=true)             │               │
  │                    ├──────────────────────────────────▶  │                  │               │
  │                    │                  │                   │ ConfirmAssignment               │
  │                    │                  │                   │ → _occupiedPads[padKey] = clientId
  │                    │                  │                   ├─────────────────▶│               │
  │                    │                  │                   │                  │               │
  │                    │                  │                   │ SendStatusTargetRpc (Assigned)  │
  │                    │                  │◀──────────────────│                  │               │
  │                    │                  │ OnStatusReceived  │                  │               │
  │                    │                  │ (Assigned)        │                  │               │
  │                    │ UpdateUI (Assigned) + timer        │                  │               │
  │                    │                  │                   │                  │               │
  │ see "Следуйте к pad #5", timer 1:30  │                   │                  │               │
  │◀───────────────────│                  │                   │                  │               │
  │                    │                  │                   │                  │               │
  │ flies ship to pad  │ (player-controlled physics)          │                  │               │
  │                    │                  │                   │                  │               │
  │ collides with      │                  │                   │                  │               │
  │ Pad_005 trigger    │                  │                   │                  │               │
  ├───────────────────▶│                  │                   │                  │               │
  │                    │ NotifyTouchedDownRpc                │                  │               │
  │                    │ (shipNetId, padId="PAD-005", stationId)               │               │
  │                    ├──────────────────────────────────▶  │                  │               │
  │                    │                  │                   │ ConfirmTouchdown │
  │                    │                  │                   ├─────────────────▶│               │
  │                    │                  │                   │                  │               │
  │                    │                  │                   │ SendStatusTargetRpc (Docked)   │
  │                    │                  │◀──────────────────│                  │               │
  │                    │                  │ OnStatusReceived  │                  │               │
  │                    │                  │ (Docked)          │                  │               │
  │                    │ UpdateUI (Docked)│                   │                  │               │
  │                    │                  │                   │                  │               │
  │                    │ EnterDocked()    │                   │                  │               │
  │                    ├───────────────────────────────────────────────────────────────▶│       │
  │                    │                  │                   │                  │ rb.isKinematic│
  │                    │                  │                   │                  │ = true        │
  │                    │                  │                   │                  │               │
  │ see "Стыковка!"    │                  │                   │                  │               │
  │◀───────────────────│                  │                   │                  │               │
  │                    │                  │                   │                  │               │
  │ press F (exit ship)│                  │                   │                  │               │
  ├───────────────────▶│                  │                   │                  │               │
  │                    │ F-handler: CommPanel закрылся        │                  │               │
  │                    │ затем boarding (выход)              │                  │               │
  │                    │ RequestTakeoffRpc                   │                  │               │
  │                    ├──────────────────────────────────▶  │                  │               │
  │                    │                  │                   │ ReleaseAssignment                │
  │                    │                  │                   ├─────────────────▶│               │
  │                    │                  │                   │                  │               │
  │                    │                  │                   │ SendTakeoffApprovedTargetRpc     │
  │                    │                  │◀──────────────────│                  │               │
  │                    │                  │ OnTakeoffApproved │                  │               │
  │                    │                  │                   │                  │               │
  │                    │ ExitDocked()     │                   │                  │               │
  │                    ├───────────────────────────────────────────────────────────────▶│       │
  │                    │                  │                   │                  │ rb.isKinematic│
  │                    │                  │                   │                  │ = false       │
```

### 2.2 Сценарий «wrong pad»

```
... (до touch trigger)

  │ collides with      │                  │                   │                  │
  │ Pad_003 trigger    │                  │                   │                  │
  ├───────────────────▶│                  │                   │                  │
  │                    │ NotifyTouchedDownRpc                │                  │
  │                    │ (padId="PAD-003", stationId)        │                  │
  │                    ├──────────────────────────────────▶  │                  │
  │                    │                  │                   │ ConfirmTouchdown │
  │                    │                  │                   │ → assignment.padId (PAD-005) != PAD-003
  │                    │                  │                   │ → status = WrongPad
  │                    │                  │◀──────────────────│                  │
  │                    │                  │ OnStatusReceived  │                  │
  │                    │                  │ (WrongPad)        │                  │
  │                    │ ShowWrongPadWarning                  │                  │
  │                    │ (toast)           │                  │                  │
  │                    │ UpdateUI (WrongPad)                  │                  │
  │                    │                  │                   │                  │
  │ see warning toast  │                  │                   │                  │
  │◀───────────────────│                  │                   │                  │
  │                    │                  │                   │                  │
  │ click "Перепарковаться"               │                   │                  │
  ├───────────────────▶│                  │                   │                  │
  │                    │ RequestDockingRpc (повторно)         │                  │
  │                    ├──────────────────────────────────▶  │                  │
  │                    │                  │                   │ AssignPad()      │
  │                    │                  │                   │ → может назначить другой pad
  │                    │                  │◀──────────────────│                  │
  │                    │                  │ OnAssignmentReceived (Assigned)     │
  │                    │ UpdateUI (Assigned)                  │                  │
  │ see "Назначаю pad #7"               │                   │                  │
```

### 2.3 Сценарий «окно истекло»

```
... (Assigned, игрок не долетел)

  │ (через 90 сек)     │                  │                   │                  │
  │                    │                  │                   │                  │
  │                    │                  │                   │ Update() loop    │
  │                    │                  │                   │ в DockingWorld:  │
  │                    │                  │                   │ time - assignedAt > window
  │                    │                  │                   │                  │
  │                    │                  │                   │ ReleaseAssignment│
  │                    │                  │                   │ SendStatusTargetRpc(Cancelled)
  │                    │                  │◀──────────────────│                  │
  │                    │                  │ OnStatusReceived  │                  │
  │                    │                  │ (Cancelled)       │                  │
  │                    │ UpdateUI (Cancelled)                 │                  │
  │                    │                  │                   │                  │
  │ see "Окно истекло" │                  │                   │                  │
  │◀───────────────────│                  │                   │                  │
```

### 2.4 Сценарий «отмена игроком»

```
... (Assigned)

  │ click "Отменить"   │                  │                   │                  │
  ├───────────────────▶│                  │                   │                  │
  │                    │ RequestTakeoffRpc (или новый RequestCancelRpc)         │
  │                    ├──────────────────────────────────▶  │                  │
  │                    │                  │                   │ ReleaseAssignment│
  │                    │                  │                   │ SendStatusTargetRpc(Cancelled)
  │                    │                  │◀──────────────────│                  │
  │                    │                  │ OnStatusReceived  │                  │
  │                    │                  │ (Cancelled)       │                  │
  │                    │ UpdateUI (Cancelled)                 │                  │
```

### 2.5 Сценарий «отстыковка» — в Docked (после успешной стыковки)

```
... (Docked)

  │ press F            │                  │                   │                  │
  ├───────────────────▶│                  │                   │                  │
  │                    │ (Q9: F = стандартное boarding)        │                  │
  │                    │ CommPanel закрывается (если открыт)  │                  │
  │                    │ затем boarding (выход)               │                  │
  │                    │ RequestTakeoffRpc                   │                  │
  │                    ├──────────────────────────────────▶  │                  │
  │                    │                  │                   │ ReleaseAssignment│
  │                    │                  │                   ├─────────────────▶│
  │                    │                  │                   │                  │ rb.isKinematic│
  │                    │                  │                   │                  │ = false       │
```

### 2.6 Сценарий «вылет без запроса разрешения» (Q8)

**Q8 (принято):** F остаётся boarding безусловно. Вылет из OuterCommZone
**без** предварительного запроса разрешения — **НЕ блокируется** в MVP
(это отдельная подсистема Departure, см. `08_DEPARTURE_SUBSYSTEM.md`).

```
... (Docked, игрок в OuterCommZone)

  │ press F (выход из корабля)            │                  │                   │
  ├───────────────────▶│                  │                   │                  │
  │                    │ CommPanel закрывается (Q9)            │                  │
  │                    │ стандартное boarding-логика            │                  │
  │                    │ ExitDocked() + EnterAutoHover         │                  │
  │                    │                                       │                  │
  │                    │ ⚠️ В MVP: вылет без запроса РАЗРЕШЁН.   │                  │
  │                    │ Нет toast, нет блокировки.             │                  │
  │                    │                                       │                  │
  │                    │ (Phase 1.5 — Departure подсистема)     │                  │
  │                    │ T → "Запросить вылет" → ожидание       │                  │
  │                    │ → разрешение → OK лети                │                  │
  │                    │                                       │                  │
  │ see "Отстыковка разрешена"             │                   │                  │
  │◀───────────────────│                  │                   │                  │
  │                    │                  │                   │                  │
  │ вылетает из зоны   │ (физика)          │                   │                  │
  │                    │                  │                   │                  │
  │ (Phase 1.5: если вылетил без запроса,  │                  │                  │
  │  через 5 сек — toast "нарушение")      │                  │                  │
```

**Ключевое:** в MVP вылет без разрешения **не наказывается**. Тост —
в подсистеме Departure (Phase 1.5). Сейчас мы НЕ делаем toast в docking.

**Тонкость:** `F` в Docked — это отстыковка, не выход из корабля.
`NetworkPlayer.F_handler` должен проверять `IsDocked` перед boarding-логикой.

---

## 3. Edge-cases (полный список)

### 3.1 Граничные случаи по зоне

| Случай | Поведение |
|--------|-----------|
| Игрок на границе OuterCommZone (вход-выход за 1 сек) | Debounce 3 тика (0.75с) в `OuterCommZone.PollPlayersInRange` — флаг `LocalPlayerStation` стабилен |
| Игрок в OuterCommZone двух станций одновременно | `LocalPlayerStation` = та, к которой ближе (по дистанции). CommPanel показывает её |
| OuterCommZone пересекает OuterCommZone другой станции | `LocalPlayerStation` обновляется по ближайшей — корректно |
| Игрок уничтожен в OuterCommZone | Respawn → `NetworkPlayer.OnNetworkSpawn` подпишется заново |
| OuterCommZone выключен (`gameObject.SetActive(false)`) | `OnDisable` снимет регистрацию |
| Станция выключена во время сессии | `DockingZoneRegistry.Unregister` → `LocalPlayerStation` = null (если она была) |

### 3.2 Граничные случаи по запросу (Q7 — двусторонняя)

| Случай | Поведение |
|--------|-----------|
| `RequestDockingRpc` для несуществующей станции | Сервер отвечает `failReason="STATION_NOT_FOUND"` → UI показывает «Связь потеряна» |
| `RequestDockingRpc` без корабля (shipNetId=0) | Сервер проверяет `ship == null` → `failReason="SHIP_NOT_FOUND"` |
| `RequestDockingRpc` для чужого корабля | Сервер проверяет `GetFirstPilotClientId(ship) == clientId` → fail `NOT_YOUR_SHIP` |
| `RequestDockingRpc` при отсутствии свободных pads | Сервер проверяет `AssignPad()` → `failReason="NO_SUITABLE_PAD"` |
| `RequestDockingRpc` при rate limit | Сервер отвечает `failReason="RATE_LIMITED"` без обработки |
| `RequestDockingRpc` дважды подряд | Второй вызов отменяет первое **pending** назначение (`CancelPendingAssignment`) + назначает новый |
| **Q7:** `RequestDockingRpc` → `RequestConfirmAssignmentRpc(false)` (отбой) | Сервер снимает pending, шлёт `Status(Cancelled)`. Клиент может запросить снова. |
| **Q7:** pending assignment истёк (клиент не подтвердил за 30 сек) | Сервер `CancelPendingAssignment` + шлёт `Status(Cancelled)`. |
| **Q7:** `RequestConfirmAssignmentRpc(accept=true)` | Сервер `ConfirmAssignment` → `_occupiedPads[padKey] = clientId` + шлёт `Status(Assigned)`. |
| **Q7:** клиент `RequestConfirmAssignmentRpc` без pending | Сервер noop (нет `_pendingByClient[clientId]`). |
| **Q7:** клиент подтвердил, но сервер уже выдал этот pad другому (race) | Сервер проверяет `_occupiedPads[padKey] == clientId` (или pending). Если нет — noop. Теоретически не должно случиться (SOT). |
| `RequestTakeoffRpc` без активного confirmed assignment | Сервер noop (нет `_assignmentsByClient[clientId]`) |
| `RequestTakeoffRpc` для чужого корабля | Сервер проверяет `_assignmentsByShip[shipNetId].clientId == clientId` — noop если нет |
| `NotifyTouchedDownRpc` для несуществующего pad | Сервер не находит `DockingWorld.ConfirmTouchdown` → status `WrongPad` |
| `NotifyTouchedDownRpc` дважды (для одного и того же ship) | Второй раз сервер проверяет `assignment.used == true` → noop (статус не меняется) |

### 3.3 Граничные случаи по FSM корабля (Q11 — KeyRod не обрабатываем)

| Случай | Поведение |
|--------|-----------|
| Игрок в `Docked` пытается улететь (W/A/S/D) | `ShipController.IsDocked == true` → input ignored |
| Игрок в `Docked` пытается сесть в другой корабль | Невозможно (он уже в корабле). F = отстыковка. |
| Корабль в `Docked` подвергается внешнему импульсу (физика) | `rb.isKinematic = true` → импульс игнорируется. Безопасно. |
| **Q11:** KeyRod-извлечение во время Docked | **НЕ обрабатываем** в MVP. F = выход из кресла = «выключает» корабль. Полноценная KeyRod-блокировка → Phase 2. |
| Игрок disconnect во время Docked | Сервер сохраняет `_occupiedPads` (session-only для MVP) → после reconnect `LocalPlayerStation` обновится автоматически |
| Игрок disconnect во время Assigned | Сервер `ReleaseAssignment` в `OnClientDisconnected` (см. §3.4 ниже) |
| Игрок disconnect во время AwaitingConfirmation (Q7) | Сервер `CancelPendingAssignment` в `OnClientDisconnected` |

### 3.4 Disconnect handling

```csharp
// В DockingServer.OnNetworkSpawn:
NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

private void OnClientDisconnected(ulong clientId) {
    if (DockingWorld.Instance != null) {
        DockingWorld.Instance.ReleaseAssignment(clientId, /* shipNetId */ 0);
        // ReleaseAssignment чистит и pending, и confirmed (см. §6)
    }
}
```

### 3.5 Граничные случаи по UI (Q10 — T игнорируется вне кресла)

| Случай | Поведение |
|--------|-----------|
| Открыть CommPanel, нажать T ещё раз | Закрыть (ToggleOpen) |
| Открыть CommPanel, нажать Esc | Закрыть |
| **Q9:** Открыть CommPanel, нажать F | CommPanel закрывается + стандартное F-boarding/отстыковка |
| Открыть CommPanel, нажать мышью вне панели | Не закрывается (`pickingMode=Ignore` на root) |
| Несколько клиентов открывают CommPanel одновременно | Каждый клиент — свой экземпляр панели. Нет конфликта. |
| CommPanel открыт на Assigned, окно истекло | UI переходит в `Cancelled` state. Auto-close через 5 сек (опц.) |
| **Q10:** Игрок не в корабле нажимает T | `IsLocalPlayerPilotingShip() == false` → **silently ignore** (нет UI, нет toast) |
| **Q10:** Игрок в корабле, но НЕ в кресле пилота | Сейчас `IsInShip` = true (грубая проверка). Phase 2 — точная через PilotSeat. |

---

## 4. Приоритезация F-key (детальный анализ) — Q9: F = boarding всегда

### 4.1 Текущий F-key pipeline (без стыковки)

```
F pressed:
  1. TryInteractNearestCraftingStation() — внутри корабля (NEW в Phase 5)
  2. TryInteractNearestDoor() (NEW в Phase 3)
  3. if _inShip → SubmitSwitchModeRpc() — выход
  4. else → boarding ship — посадка
```

### 4.2 Новый F-key pipeline (с стыковкой, Q9)

**Q9 (принято):** F = boarding **всегда**, даже если открыт CommPanel.
CommPanel **закрывается** при F (как Esc). Две конфликтующих команды —
игрок явно выбирает.

```
F pressed:
  1. Если CommPanel открыт → SetOpen(false) (НЕ return — продолжаем)
  2. Стандартная цепочка:
     a. TryInteractNearestCraftingStation() — внутри корабля
     b. TryInteractNearestDoor()
     c. if _inShip → SubmitSwitchModeRpc() — выход (включая отстыковку из Docked)
     d. else → boarding ship — посадка
```

**НЕ добавляем** `TryInteractNearestDockStation` в F-key chain (Q8).
DockStation открывается через **T**, не F.

### 4.3 TryInteractNearestDockStation (Q8: НЕ вызывается из F)

Этот метод **не существует** в F-key chain. Вместо этого:

```csharp
// T handler (в NetworkPlayer.cs):
private void OnCommPanelPressed() {
    if (!IsOwner) return;
    if (CommPanelWindow.Instance == null) return;
    // Q10: T игнорируется вне кресла
    if (!DockingClientState.IsLocalPlayerPilotingShip()) return;
    var station = DockingZoneRegistry.LocalPlayerStation
              ?? DockingZoneRegistry.LocalPlayerShipStation;
    if (station == null) return;
    CommPanelWindow.Instance.ToggleOpen();
}
```

**Расстояние:** проверка в `OuterCommZone.PollLocalPlayerZone` уже
выполнена — `LocalPlayerStation` = null если игрок далеко. Дополнительная
distance check не нужна.

---

## 5. Согласованность с другими подсистемами

### 5.1 QuestServer / NPC-диалоги

Игрок подходит к NPC → E → DialogWindow (typewriter, опции).
Игрок подходит к DockStation → T → CommPanel (без typewriter).

**Разделение зон:** NPC стоят на земле (WorldScene_0_0 на y=2510), DockStation
— на высоте города (или в тестовой зоне). Они не пересекаются геометрически.
Hotkey разные: E vs T.

**Edge case:** если NPC стоит на DockStation (теоретически) — игрок
может взаимодействовать обоими. E приоритетнее T в текущем дизайне.

### 5.2 Trade/Market — открытие рынка

`MarketZone` (5м радиус) использует E для открытия MarketWindow.
`OuterCommZone` (1000м радиус) использует T для открытия CommPanel.

Если игрок в OuterCommZone и в MarketZone одновременно (DockStation +
рынок рядом) → F вызывает CommPanel (DockStation выше приоритетом).
E вызывает MarketWindow.

### 5.3 Pickup (E-key)

`PickupItem` использует E для подбора. OuterCommZone использует T.
Pickup остаётся на E, стыковка на T. Нет конфликта.

### 5.4 ShipKey / MetaRequirement (F-key для boarding)

Текущая F-key boarding требует `MetaRequirementRegistry.CanPlayerUse(shipNetId)`.
С добавлением `TryInteractNearestDockStation` в начало цепочки — boarding
**не сработает**, если игрок в OuterCommZone. Это **изменение поведения**.

**Митигация:** если игрок хочет boarding (не dock) — он может отойти
от DockStation >1000м (выйти из OuterCommZone). Внутри зоны — стыковка
приоритетнее.

### 5.5 Compositing with PlayerStateMachine

`PlayerStateMachine` имеет состояния: Walking, Ship, FreeCamera.
Стыковка не вводит нового состояния (игрок всё ещё в Ship).
Но `ShipController` получает флаг `IsDocked`, который **подавляет ввод**.

---

## 6. Сценарии тестирования (для QA)

### 6.1 Smoke test (базовый сценарий)

```
1. Запустить Editor Play Mode
2. Спавниться на [Chest_Main] (40000, 2502.77, 40000)
3. Подойти к DockStation_Primium (40500, 2510, 40500)
   - пешком: ~500м
   - в корабле: подлететь
4. Увидеть HUD-hint «T — связаться с диспетчером»
5. Нажать T → CommPanel открывается
6. Увидеть «Примум — Диспетчерская», фразу приветствия, кнопки
7. Сесть в корабль (F, рядом с кораблём из тестового кластера ships)
8. Нажать T → CommPanel показывает «Запросить посадку» (enabled)
9. Нажать «Запросить посадку»
10. UI переходит в Assigned, показывает pad #N, таймер окна
11. Лететь к pad #N (W + маневрирование)
12. Коснуться pad → UI → Docked, корабль заморожен
13. Нажать F → отстыковка → корабль снова двигается
14. Нажать T → UI → Idle
15. Нажать Esc → CommPanel закрывается
```

### 6.2 Wrong-pad test

```
... (после шага 11 из smoke test)

12. Передумать, сесть на pad #M (любой другой свободный)
13. Увидеть toast «Вы на чужом pad'е» (4 сек fade-out)
14. UI переходит в WrongPad state, кнопка «Перепарковаться»
15. Нажать «Перепарковаться» → RequestDockingRpc повторно → Assigned с новым pad
16. Продолжить smoke test с шага 11
```

### 6.3 Window expiry test

```
... (после шага 10 из smoke test)

11. НЕ лететь к pad'у. Ждать 90 сек.
12. UI → Cancelled state. Прогресс-бар доходит до 0.
13. Нажать «Запросить снова» → повторное назначение
```

### 6.4 No-station test

```
1. Спавниться далеко от DockStation (>1000м)
2. Нажать T → ничего не происходит (hint не появляется)
```

### 6.5 Multi-station test (Phase 3 — out of MVP)

```
1. Расставить 2 DockStation (для теста)
2. Зайти в зону первой → T → CommPanel показывает её
3. Перейти в зону второй → T → CommPanel показывает вторую
```

### 6.6 Disconnect test

```
1. Assigned состояние
2. Закрыть Editor (без graceful disconnect)
3. Открыть Editor → Server перезапустится
4. _occupiedPads пуст (session-only) → можно сесть снова
```

---

## 7. Диаграмма состояний клиента (UI state machine)

```
                        ┌──────────────────────┐
                        │                      │
                        │     IDLE (no UI)     │
                        │                      │
                        └──────────┬───────────┘
                                   │ T pressed, in zone
                                   ▼
                        ┌──────────────────────┐
              ┌────────▶│                      │◀──────┐
              │         │   IDLE (UI open)     │       │
              │         │   Greeting           │       │
              │         │   [Запросить][Отмена]│       │
              │         └──────┬───────────────┘       │
              │                │ Запросить              │ Отмена
              │                ▼                        │
              │         ┌──────────────────────┐       │
              │         │                      │       │
              │         │     ASSIGNED         │       │
              │         │   Timer running      │       │
              │         │   [───●──────] 1:30  │       │
              │         │   [Отменить запрос]  │       │
              │         └────┬──────┬──────────┘       │
              │              │      │                  │
              │   TouchedDown│      │ WindowExpired/    │
              │   (correct)  │      │ Cancel by user   │
              │              │      │                  │
              │              ▼      ▼                  │
              │         ┌──────────────────────┐       │
              │         │                      │       │
              │         │      DOCKED          │       │
              │         │   "F — Отстыковка"   │       │
              │         │   [F — Отстыковка]   │       │
              │         └──────────┬───────────┘       │
              │                    │ F pressed         │
              │                    ▼                   │
              │         ┌──────────────────────┐       │
              │         │                      │       │
              └─────────┤   IDLE (UI open)     │───────┘
                        │   "Отстыковка разрешена"
                        │   [Отмена]
                        └──────────────────────┘

        TouchedDown (wrong):
              │
              ▼
        ┌──────────────────────┐
        │     WRONG PAD        │
        │  "Вы на чужом pad'е" │
        │  [Перепарковаться]   │
        │  [Закрыть]           │
        └──────┬───────────────┘
               │ Перепарковаться
               ▼
        (loop back to ASSIGNED)

        Cancel assigned:
              │
              ▼
        ┌──────────────────────┐
        │     CANCELLED        │
        │  "Окно истекло"      │
        │  [Запросить снова]   │
        │  [Закрыть]           │
        └──────┬───────────────┘
               │ Запросить снова
               ▼
        (loop back to ASSIGNED)
```

---

## 8. Network-bandwidth оценка

### 8.1 RPC sizes (грубая прикидка)

| RPC | Поля | Size |
|-----|------|------|
| `RequestDockingRpc` | stationId (string ~12) + shipNetId (ulong 8) + RpcParams | ~30 B |
| `SendDockingAssignmentTargetRpc` | DockingAssignmentDto | ~80 B |
| `SendDockingStatusTargetRpc` | DockingStatusDto | ~30 B |
| `SendTakeoffApprovedTargetRpc` | shipNetId | ~10 B |
| `NotifyTouchedDownRpc` | shipNetId + padId + stationId | ~30 B |

### 8.2 Частота

| Действие | Частота на клиента |
|----------|-------------------|
| RequestDocking | 1 на стыковку (минимум) |
| NotifyTouchedDown | 1 на стыковку |
| Status updates | 1-2 на стыковку |
| Timer ticks | 0 (нет per-frame RPC) |

**Итого:** ~5 RPC на одну стыковку. Пренебрежимо мало.

### 8.3 Client updates

`CommPanelWindow.Update()` — чистый UI update (никакой сети). Timer
тикает локально. Только при `Time.time` change (или network event) —
обновляется прогресс-бар.

---

## 9. Тестирование через Play Mode

### 9.1 Debug-логи

Каждый критический момент пишет `Debug.Log`:

```csharp
[DockingServer] RequestDockingRpc clientId=0 stationId=STN-PRM-001 shipNetId=42
[DockingServer] Assigned padId=PAD-005, window=90s
[DockingServer] NotifyTouchedDown clientId=0 shipNetId=42 padId=PAD-005 → status=Docked
[DockingServer] ReleaseAssignment clientId=0 (reason: takeoff)

[OuterCommZone:STN-PRM-001] client: local player entered zone (dist=420m)
[OuterCommZone:STN-PRM-001] client: local player left zone (dist=1100m)

[DockingPadTriggerBox:PAD-005] OnTriggerEnter ShipLight (netId=42)
```

### 9.2 Что должен увидеть пользователь в Console

1. После StartHost: `[DockingServer] OnNetworkSpawn — IsServer=true`
2. После подхода к DockStation: `[OuterCommZone:STN-PRM-001] client: local player entered zone`
3. После T: `OnAssignmentReceived → stationId=STN-PRM-001, padId=PAD-005`
4. После touch: `OnStatusReceived → status=Docked, padId=PAD-005`
5. После F (отстыковка): `OnTakeoffApproved shipNetId=42`

### 9.3 Чеклист для пользователя

- [ ] Compile: 0 errors в Editor Console
- [ ] StartHost → видим `[DockingServer] OnNetworkSpawn`
- [ ] Подойти к DockStation_Primium → видим entered zone log
- [ ] T → CommPanel открывается
- [ ] Сесть в корабль + T → CommPanel показывает «Запросить посадку»
- [ ] Запросить → Assigned state с таймером
- [ ] Лететь к pad → таймер тикает вниз
- [ ] Касание pad → Docked state, корабль freeze
- [ ] F → отстыковка, корабль двигается
- [ ] T → Esc → CommPanel закрывается

---

## 10. Связь с другими документами

| Документ | Что используем |
|----------|----------------|
| `02_V2_ARCHITECTURE.md` §5 RPCs | серверная логика |
| `02_V2_ARCHITECTURE.md` §10 FSM | ShipController.IsDocked |
| `03_ZONES_AND_TRIGGERS.md` | OuterCommZone, DockingPadTriggerBox |
| `04_DIALOG_AND_DISPATCHER_UI.md` | CommPanelWindow, CommPanelToast |
| `06_ROADMAP.md` | тикеты на implementation |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | F-key chain, текущее место для изменений |

---

*Создано: 2026-06-19 | Аналитическая сессия | Без кода.*