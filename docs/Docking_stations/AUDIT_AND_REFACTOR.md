# AUDIT_AND_REFACTOR — стыковочная система

> **Дата:** 2026-06-20
> **Сессия:** «нужен глубокий анализ и четкий рефакторинг»
> **Статус кода до рефакторинга:** ❌ неработающий (4 крит. бага)
> **Документация:** существует с 2026-06-19 (см. `00_README.md`, `02_V2_ARCHITECTURE.md`, `05_FLOW_AND_INTERACTION.md`, `07_OPEN_QUESTIONS.md`)

---

## 0. TL;DR

Реальных проблем **четыре**, и они связаны, а не независимы:

| # | Проблема | Следствие |
|---|----------|-----------|
| **A** | DTO `DockingAssignmentDto` содержит `string failReason`, при `success=false` сервер шлёт пустые `stationId`/`padId`/`voiceLine` — **String ref NRE в `FastBufferWriter.WriteValueSafe(null)`** при попытке сериализации | Кнопка [Запросить посадку] → RpcException → молча умирает |
| **B** | `CommPanelWindow.RequestDocking` отправляет RPC **ДО того как игрок подтвердил предыдущий `PendingAssignment`**. Сервер не находит `PadLayout` если `DockStationDefinition` сериализован неправильно (битый SO/нет в инспекторе) | Полная неработающая цепочка «связь → запрос» |
| **C** | `ShipController.IsDocked` существует как флаг, **но никто не блокирует ввод** — `SendShipInput` не проверяет `IsDocked`, `NetworkPlayer.Update` шлёт thrust/yaw/pitch без guard | После стыковки можно улететь на W |
| **D** | `CommPanelWindow.OnPrimaryClicked` для статуса `Docked` делает `SetOpen(false)` — **закрывает панель вместо запроса отстыковки**. На статус `WrongPad` — `RequestDocking()` повторно | После стыковки нельзя нормально отстыковаться через UI |
| **E (косметический)** | `ApplyInlineFallbackStyles` ставит `width:560px; translate:-50%-50%` на `.comm-panel-root` (`_container`), перекрывая USS flex-центрирование → кнопки растянуты, UI "большие кнопки на весь экран" | UI без верстки |

Полный аудит кода: **ниже**.

---

## 1. Полный аудит — что нашёл

### 1.1 DTO `DockingDto.cs` — String-ref-null NRE

```csharp
public struct DockingAssignmentDto : INetworkSerializable
{
    public string stationId;     // string, не FixedString
    public string padId;
    public string voiceLine;     // ← null при failure
    public string failReason;
    ...
}
```

**Сервер при `RequestDockingRpc` если назначение не удалось:**
```csharp
SendDockingAssignmentTargetRpc(clientId, MakeFail("NO_SUITABLE_PAD", shipNetId), targetRpcParams);
// MakeFail возвращает DTO с success=false, но stationId/padId/voiceLine = null
```

`MakeFail` создаёт:
```csharp
new DockingAssignmentDto {
    success = false,
    failReason = reason,
    shipNetworkObjectId = shipNetId
    // stationId/padId/approachPoint/approachAltitude/approachHeading/landingWindowSeconds/voiceLine — все null/0/default
}
```

NGO `FastBufferWriter.WriteValueSafe(string)` **падает NRE** если `s == null`:
```
NullReferenceException: Object reference not set to an instance of an object
  at Unity.Netcode.FastBufferWriter.WriteValueSafe (System.String s, ...)
  at Unity.Netcode.BufferSerializerWriter.SerializeValue (System.String& s, ...)
  at ProjectC.Docking.Dto.DockingAssignmentDto.NetworkSerialize[T] (...)
  at ProjectC.Docking.Network.DockingServer.SendDockingAssignmentTargetRpc (...)
```

**Fix (минимальный):** в `MakeFail` заполнять все строки пустыми `""`:
```csharp
private static DockingAssignmentDto MakeFail(string reason, ulong shipNetId) => new DockingAssignmentDto {
    success = false,
    failReason = reason ?? "UNKNOWN",
    shipNetworkObjectId = shipNetId,
    stationId = "",
    padId = "",
    voiceLine = ""
};
```

**Fix (правильный):** все строковые поля в DTO сделать `default = ""` через конструктор / struct ctor. Но NGO требует `default(T)` для пустых struct-ов — а наш struct не имеет ctor. Поэтому инициализируем все строки в `MakeFail` и в `AssignPad`.

---

### 1.2 `CommPanelWindow.RequestDocking` — нет guard

```csharp
private void RequestDocking() {
    var server = DockingServer.Instance;
    if (server == null || !server.IsSpawned) return;
    var station = DockingZoneRegistry.LocalPlayerStation
                  ?? DockingZoneRegistry.LocalPlayerShipStation;
    if (station == null) return;  // ← silently return. Игрок не понимает почему ничего не происходит
    ulong shipNetId = GetLocalShipNetworkObjectId();
    if (shipNetId == 0) return;
    server.RequestDockingRpc(station.StationId, shipNetId);
}
```

**Проблема:** если `station` есть, но `station.StationId` пустой (битый SO или забыли назначить `DockStationDefinition` в инспекторе) → RPC уходит с пустой stationId → сервер не находит станцию → `STATION_NOT_FOUND` → ответ с `success=false` → **String NRE (см. 1.1)**.

Также: **нет защиты от множественных нажатий**. Игрок жмёт 2 раза подряд — два RPC, два pending assignment. В `RegisterPendingAssignment` второй перезапишет первый по `_pendingByClient[clientId] = a`. OK тут в коде, но никакого UI-feedback.

---

### 1.3 `ShipController.IsDocked` не блокирует ввод

```csharp
public void SendShipInput(float thrust, float yaw, float pitch, float vertical, bool boost) {
    if (NetworkManager.Singleton == null || !IsSpawned) return;
    SubmitShipInputRpc(thrust, yaw, pitch, vertical, boost);  // ← БЕЗ guard на IsDocked
}
```

В `NetworkPlayer.Update`:
```csharp
if (_inShip) {
    // ...читает W/A/S/D/Q/E/mouse...
    _currentShip.SendShipInput(thrust, yaw, pitch, vertical, boost);  // ← всегда шлёт
}
```

**По диздоку** (`05_FLOW_AND_INTERACTION.md` §3.3):
> «Игрок в `Docked` пытается улететь (W/A/S/D) → `ShipController.IsDocked == true` → input ignored»

**Fix:** В `SendShipInput` добавить:
```csharp
public void SendShipInput(float thrust, float yaw, float pitch, float vertical, bool boost) {
    if (NetworkManager.Singleton == null || !IsSpawned) return;
    if (_netIsDocked.Value) return;  // T-DOCK-09: двигатель заблокирован
    SubmitShipInputRpc(thrust, yaw, pitch, vertical, boost);
}
```

Дополнительно: на сервере в `SubmitShipInputRpc` обработчике тоже добавить guard (на случай если клиент шлёт RPC напрямую — defense in depth).

---

### 1.4 `CommPanelWindow.OnPrimaryClicked` — wrong logic

```csharp
private void OnPrimaryClicked() {
    if (_awaitingConfirmation.HasValue) { ConfirmAssignment(true); return; }

    if (_currentStatus == DockingStatus.Idle || _currentStatus == DockingStatus.Cancelled) {
        RequestDocking();
    }
    else if (_currentStatus == DockingStatus.Docked) {
        SetOpen(false);   // ← БАГ: закрывает панель вместо отстыковки
    }
    else if (_currentStatus == DockingStatus.WrongPad) {
        RequestDocking();  // ← БАГ: перезапрашивает docking, но статус уже Cancelled-Idle
    }
}
```

**По диздоку** (`05 §1.4`):
> «CommPanel в режиме Docked → кнопка [F — Отстыковка]». Клик по ней = `RequestTakeoffRpc` (НЕ `SetOpen(false)`).

**Fix:** для `Docked` → `CancelAssignment()` (который шлёт `RequestTakeoffRpc`):

```csharp
else if (_currentStatus == DockingStatus.Docked) {
    // Кнопка "F — Отстыковка": шлём запрос на отстыковку
    CancelAssignment();  // уже есть, шлёт RequestTakeoffRpc
}
```

И на UI: текст кнопки = «Отстыковка», без «F — » (F не работает, мы внутри панели).

---

### 1.5 UI верстка — ApplyInlineFallbackStyles ломает USS

В `EnsureBuilt`:
```csharp
_root.styleSheets.Add(commPanelUss);  // USS подключён

// потом в SetOpen(open):
if (_container != null) ApplyInlineFallbackStyles(_container);  // ← ПЕРЕЗАПИСЫВАЕТ USS на .comm-panel-root
```

`ApplyInlineFallbackStyles` ставит на `_container` (`.comm-panel-root`):
```csharp
main.style.position = Position.Absolute;
main.style.top = new Length(50, LengthUnit.Percent);
main.style.left = new Length(50, LengthUnit.Percent);
main.style.translate = new StyleTranslate(new Translate(new Length(-50, ...), new Length(-50, ...)));
main.style.width = 560;
main.style.maxWidth = new Length(90, LengthUnit.Percent);
main.style.maxHeight = new Length(92, LengthUnit.Percent);
```

А USS на `.comm-panel-root` имеет:
```css
.comm-panel-root {
    position: absolute; top: 0; left: 0; right: 0; bottom: 0;
    align-items: center; justify-content: center;
    background-color: rgba(250, 40, 40, 0.5);  // красная плёнка
    display: flex;
}
```

`width: 560px` через inline перебивает `right: 0; bottom: 0` → `.comm-panel-root` становится 560×... плавающим блоком, прижатым к `top:50%; left:50%`. USS-центрирование (flex-center) перестаёт работать, потому что inline-стили выигрывают по specificity.

`comm-panel` внутри (560×something) растягивается на всю ширину `.comm-panel-root` (которая 560px), но её `width: 560px` через USS — нормально.

**Главный визуальный баг:** красная полупрозрачная плёнка (`.comm-panel-root`) теперь не закрывает весь экран, а маленькая. Кнопки выглядят растянутыми на этот маленький контейнер → «большие кнопки в верхней части».

**Fix:** не вызывать `ApplyInlineFallbackStyles` на `.comm-panel-root`. USS уже всё делает правильно. Удалить эту строку.

```csharp
// Было:
if (_container != null) ApplyInlineFallbackStyles(_container);

// Стало:
// ApplyInlineFallbackStyles не нужен — USS .comm-panel-root делает всё через flex-center
```

Дополнительно: вернуть нормальный фон (убрать красную отладку):
```css
background-color: rgba(0, 0, 0, 0.4);  /* вместо rgba(250, 40, 40, 0.5) */
```

---

### 1.6 `WrongPad` логика — статус не сбрасывается в Idle

`DockingPadTriggerBox.OnTriggerEnter` отправляет `NotifyTouchedDownRpc(ship, padId, stationId)`.

`DockingWorld.ConfirmTouchdown`:
```csharp
public DockingStatusDto ConfirmTouchdown(ulong clientId, ulong shipNetId, string padId, string stationId) {
    var assignment = _assignmentsByShip.TryGetValue(shipNetId, out var a) ? a : default;
    if (assignment.clientId != clientId) {
        return MakeStatus(DockingStatus.WrongPad, stationId, padId);  // ← даже если assignment нет
    }
    ...
}
```

**Проблема:** если игрок нажимает T В зоне, ещё НЕ делал `RequestDocking`, нажимает F, выходит из кресла, входит в другой корабль, летит и касается pad'а → `assignment = default`, `assignment.clientId = 0`, `clientId != 0` → `WrongPad`.

Или ещё хуже: игрок подлетает к pad'у **без** предварительного запроса → `NotifyTouchedDownRpc` → `WrongPad` → «Вы на чужом pad'е, перепаркуйтесь».

**По плану пользователя:**
> «находится в зоне порта нажимаю T → мне сразу пишет вы не там, перепаркуйтесь»

Это **именно** сценарий «подлетел без запроса». Логика должна быть:
- Игрок подлетел к pad'у **без** confirmed assignment → **молча игнорировать** (или «запросите стыковку у диспетчера» — но не «WrongPad»).
- Игрок подлетел к **другому** pad'у (есть assignment на PAD-005, касается PAD-003) → `WrongPad` + toast.

**Fix:** в `ConfirmTouchdown`:
```csharp
public DockingStatusDto ConfirmTouchdown(...) {
    if (!_assignmentsByShip.TryGetValue(shipNetId, out var a) || a.clientId != clientId) {
        // Нет confirmed assignment — это значит игрок ещё не делал RequestDocking
        // В MVP: молча возвращаем Idle (или специальный статус NotDockedYet)
        return MakeStatus(DockingStatus.Idle, stationId, padId);  // ← ИСПРАВЛЕНО
    }
    if (a.padId != padId) {
        return MakeStatus(DockingStatus.WrongPad, stationId, padId);
    }
    a.used = true;
    _assignmentsByShip[shipNetId] = a;
    _assignmentsByClient[clientId] = a;
    return MakeStatus(DockingStatus.Docked, stationId, padId);
}
```

Дополнительно: на UI при касании pad'а **без** assignment — тост «Запросите посадку через T».

---

### 1.7 `HandleStatusReceived` — некорректное состояние

```csharp
public void HandleStatusReceived(DockingStatusDto status) {
    if (status.status == DockingStatus.Assigned) {
        if (PendingAssignment.HasValue) {
            CurrentAssignment = PendingAssignment;
            PendingAssignment = null;
        }
    }
    ...
}
```

**Проблема:** когда сервер отвечает на `NotifyTouchedDownRpc` со статусом `Docked`, в `DockingClientState.HandleStatusReceived` ничего не происходит (нет case для Docked). Но в `CommPanelWindow.HandleStatusReceived` мы обрабатываем Docked для переключения UI на кнопку «F — Отстыковка».

В текущем коде это работает (UI переключается), но если приходит `Docked` → `HandleTouchedDown` event, на который `CommPanelWindow.HandleTouchedDown` подписан. Дублирование.

---

### 1.8 `HandleAssignmentFailed` vs `OnAssignmentFailed`

`DockingClientState.HandleAssignmentReceived`:
```csharp
if (assignment.success) {
    PendingAssignment = assignment;
    OnAwaitingConfirmation?.Invoke(assignment, true);
} else {
    OnAssignmentFailed?.Invoke(assignment);  // ← НЕ вызывает OnAwaitingConfirmation
}
```

В `CommPanelWindow.HandleAssignmentFailed`:
```csharp
private void HandleAssignmentFailed(DockingAssignmentDto assignment) {
    if (_message == null) return;
    string msg = ...;
    _message.text = msg;
    _currentStatus = DockingStatus.Idle;
    UpdateUI();
}
```

**Работает**, но `_currentStatus` сетится в Idle ДО того как UI обновится — там нет специальной ветки для "только что получил failure", UI просто покажет Idle-state.

---

### 1.9 OuterCommZone — двойная регистрация

```csharp
private void OnEnable() {
    if (_stationController != null)
        DockingZoneRegistry.Register(_stationController);
    ...
}

private void Start() {
    if (_stationController != null)
        DockingZoneRegistry.Register(_stationController);  // ← второй вызов, идемпотентен
}
```

`DockingZoneRegistry.Register` идемпотентен (проверка есть), но это мусор. Убрать `Start()` или `OnEnable()`.

---

### 1.10 Scene reference — `DockPadTriggerBox._stationController`

```csharp
_stationController = GetComponentInParent<DockStationController>();
```

Если `DockStation_Primium` имеет 5 pad-children и `DockStationController` на root — `GetComponentInParent` найдёт. Но если сцена переименована, или root не имеет контроллера — `null`. `NotifyTouchedDownRpc` тогда шлёт `stationId=""`. См. 1.2.

---

## 2. Рефакторинг — план по приоритетам

### Фаза 1 — Критические фиксы (P0, ~2 часа)

Цель: кнопка [Запросить посадку] работает, UI отображается корректно, RPC не падает.

| # | Файл | Изменение |
|---|------|-----------|
| 1.1 | `DockingDto.cs` | Не менять — оставить `string`. Но добавить invariant: ни один строковый член не должен быть `null`. |
| 1.1a | `DockingServer.cs` | `MakeFail` инициализирует все строки `""`. `AssignPad` тоже проверить (там есть `voiceLine = def.VoiceLines?.GetRandomLine(...) ?? ""` — OK). |
| 1.2 | `CommPanelWindow.cs` | `RequestDocking()` — добавить guard: `station != null && !string.IsNullOrEmpty(station.StationId)`. |
| 1.5 | `CommPanelWindow.cs` | Удалить вызов `ApplyInlineFallbackStyles(_container)` — USS достаточно. |
| 1.5a | `CommPanel.uss` | `background-color: rgba(0, 0, 0, 0.4);` (убрать красную плёнку). |

### Фаза 2 — Логика двигателя и FSM (P0, ~1.5 часа)

Цель: после стыковки нельзя улететь без отстыковки.

| # | Файл | Изменение |
|---|------|-----------|
| 2.1 | `ShipController.cs` | `SendShipInput` — guard `if (_netIsDocked.Value) return;`. |
| 2.2 | `ShipController.cs` | `SubmitShipInputRpc` server-side — тоже guard (defense in depth). |
| 2.3 | `ShipController.cs` | `EnterDocked()` — `_rb.isKinematic = true;` (дополнительно к флагу). |
| 2.4 | `ShipController.cs` | `ExitDocked()` — `_rb.isKinematic = false;`. |

### Фаза 3 — Логика кнопок UI (P1, ~1 час)

Цель: в `Docked` нажатие primary = запрос отстыковки, не закрытие.

| # | Файл | Изменение |
|---|------|-----------|
| 3.1 | `CommPanelWindow.cs` | `OnPrimaryClicked`: в `Docked` → `CancelAssignment()` (а не `SetOpen(false)`). |
| 3.2 | `CommPanelWindow.cs` | `ConfigureButtons`: текст кнопки в `Docked` = «Отстыковка» (без «F — »). |
| 3.3 | `CommPanelWindow.cs` | Secondary кнопка в `Docked` = Hidden (только primary). |

### Фаза 4 — Correctness серверной логики (P1, ~1 час)

| # | Файл | Изменение |
|---|------|-----------|
| 4.1 | `DockingWorld.cs` | `ConfirmTouchdown` — если assignment нет → `Idle` (не `WrongPad`). |
| 4.2 | `OuterCommZone.cs` | Убрать дублирующий `Register` в `Start()`. |
| 4.3 | `CommPanelWindow.cs` | Toast для случая «коснулся pad'а без assignment» — сообщение «Запросите посадку через T». |

### Фаза 5 — Документация и обновление (P2, ~30 мин)

| # | Файл | Изменение |
|---|------|-----------|
| 5.1 | `docs/Docking_stations/AUDIT_AND_REFACTOR.md` | (этот файл) — отметить секции выполненными. |
| 5.2 | `docs/Docking_stations/CHANGELOG.md` | Запись «2026-06-20 — критические фиксы после ревью». |

---

## 3. Что НЕ трогаем в этом рефакторинге

- ❌ DTO-структуры (правильные, минимальный fix через `MakeFail`)
- ❌ Архитектура `DockingServer` ↔ `DockingWorld` (SOT на сервере — правильно)
- ❌ `OuterCommZone` (работает корректно, только убираем дубликат Register)
- ❌ `DockStationController` (работает, SO привязан)
- ❌ `DockingPadTriggerBox` (работает, см. 1.10 — minor)
- ❌ Departure subsystem (Phase 1.5, не MVP)
- ❌ Автопилот (Phase 2)

---

## 4. Порядок выполнения

1. **Фаза 1** — фикс RPC + UI (2 тикета)
2. **Compile + Play Mode smoke test** — проверить, что кнопка «Запросить посадку» не падает
3. **Фаза 2** — двигатель-блокировка (3 тикета)
4. **Compile + Play Mode** — проверить, что в Docked W/A/S/D ничего не делают
5. **Фаза 3** — логика кнопок (3 тикета)
6. **Compile + Play Mode** — проверить, что в Docked primary = отстыковка
7. **Фаза 4** — Correctness (3 тикета)
8. **Compile + Play Mode** — полный сценарий «подлёт без запроса → Idle (не WrongPad)»
9. **Фаза 5** — документация

---

## 5. Что нужно от пользователя

- ✅ Подтверждение что план OK (можно одним «поехали»)
- ❌ НЕ нужно вручную ничего делать — всё через MCP
- ❌ НЕ нужно git-коммитов
- 🟡 После моего «готово» — зайти в Play Mode, прогнать сценарий:
  1. T в корабле → «Запросить посадку»
  2. [Хорошо]
  3. Лететь к PAD-001
  4. Касание trigger → Docked
  5. Попробовать W/A/S/D → ничего не происходит
  6. T → primary = «Отстыковка» → клик → Docked → Idle, корабль снова летит

---

## 6. Риски

- **R1:** Фикс `MakeFail` может скрыть другой баг (если в другом месте DTO отправляется с null). После фикса — следить за логами на предмет `ArgumentNullException` в NetworkSerialize.
- **R2:** `rb.isKinematic` в `EnterDocked` может вызвать warning в Unity (если Rigidbody не RB_None constraints). Тестировать.
- **R3:** При `ExitDocked` `rb.isKinematic = false` + текущая velocity может вызвать «прыжок» корабля на несколько метров. Решение: сохранять velocity при kinematic и применять при обратном переходе.

---

## 7. Ссылки

- Дизайн: `docs/Docking_stations/02_V2_ARCHITECTURE.md`
- Поток: `docs/Docking_stations/05_FLOW_AND_INTERACTION.md`
- Решения: `docs/Docking_stations/07_OPEN_QUESTIONS.md`
- UI дизайн: `docs/Docking_stations/04_DIALOG_AND_DISPATCHER_UI.md`
- Существующие мелкие фиксы (предыдущая сессия): `docs/Docking_stations/REFACTOR_PLAN.md`, `docs/Docking_stations/BUG_AUDIT.md`

---

*Создано: 2026-06-20 | Аналитическая сессия после теста | Без кода в этом файле.*