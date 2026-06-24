# Retrospective: NPC Ship Navigation (M2 → M3.2)

> **Дата:** 2026-06-24
> **Статус:** M3.2.15 — первый рабочий round-trip
> **Всего коммитов:** ~27 (от 9d8bc1c до 706a9d1)
> **Потрачено:** ~15 итераций, из них ~10 — по кругу

---

## 1. Исходная архитектура (до всех правок)

**Состояние 9d8bc1c (23 июня):**

```
NpcShipWorld.TickNpc (FSM):
  Idle → Departing (vertical climb, thrust=0, vertical=1)
       → InTransit (yaw: clamp(bearing*0.02, ±0.3), thrust: dir·forward*1.2, altitude hold)
       → Approach (yaw: clamp(bearing*0.02, ±0.3), thrust: dist/APPROACH*0.8, descent to station+5m)
       → Docking (proximity check, TryAssignPadForNpc)
       → Docked (dwell → cycle)
```

**Что РАБОТАЛО в 9d8bc1c:**
- FSM цепочка проходила (корабли взлетали, разворачивались, летели)
- `ApplyMovementInput` → `ApplyServerInput` (ShipController)

**Что НЕ РАБОТАЛО:**
- Parkovka (парковка на пады): корабли кружились над станцией, не могли сесть
- `yawForce=5` в сцене — в 5 раз слабее дефолта, корабли не разворачивались
- `AddTorque(ForceMode.Force)` для mass=2000 даёт мизерную angular accel

---

## 2. Полная хронология (по коммитам)

### Фаза M2 (исходная, от 9d8bc1c)

| # | Коммит | Что делали | Результат |
|---|--------|-----------|-----------|
| M2.0 | 9d8bc1c | "many fixes routers done" | Базовый FSM, корабли летают но не стыкуются |
| M2.1 | 47db44f | Barge/crane movement refactor | Diagonal flight, course correction |
| M2.2 | 7ac37c1 | "before refactor _3" | Подготовка к M3 |

### Фаза M3.0 (мой первый NavTick)

| # | Коммит | Что делали | Результат |
|---|--------|-----------|-----------|
| M3.0 | eb44019 | 7-mode NavTick (Docked/Lifting/Yawing/Cruising/Holding/Berthing/Hover) | Написал с нуля, перенёс FSM в NpcShipController |

### Фаза M3.1 (5 итераций — всё по кругу)

| # | Коммит | СИМПТОМ | ДИАГНОЗ | ФИКС | ЧТО ПОШЛО НЕ ТАК |
|---|--------|---------|---------|------|-------------------|
| M3.1.1 | f7a72cc | "фиксы" | — | NavTick utilities, AutoStabilize | Ещё не тестировалось |
| M3.1.2 | eb44019 | — | — | 7-mode NavTick code | Сразу много кода, не отлажено |
| M3.1.3 | 3ab6ebd | NPC не видят станцию | locationId mismatch: "PRIMIUM_TEST_ZONE_2" vs "PRIMIUM_TEST_ZONE" | Выровнял locationId | **ПЕРВЫЙ КРУГ: расхождение конфигов** |
| M3.1.4 | cf3173b | NPC улетают вверх | Momentum от Lifting | KillVerticalVelocity | Не влияло — причина была в yawForce |
| M3.1.5 | cf3173b | NPC не крутятся | yawForce 50/500/5000 разбросан | Унифицировал yawForce=200 | **ВТОРОЙ КРУГ: настройка сцены вместо кода** |
| M3.1.6 | e7e0a5f | Lifting→Docked флик | NGO NetworkVariable batching | Guard в NavTick | **СЛОМАЛ: guard выходил до ApplyServerInput** |
| M3.1.7 | e22ec1a | NPC застрял в Lifting | Guard блокирует | Убрал guard | Вернул флик |
| M3.1.8 | 97a1afa | "ползут" | bearing gate + zero-angular | 3 фикса | **ТРЕТИЙ КРУГ: симптомы, не причина** |
| M3.1.9 | d592937 | heading to PRIMIUM (назад) | AdvanceScheduleIndex на первом цикле | route[0] при регистрации | Починил |
| M3.1.10 | 3378cb1 | "ползут" (снова) | yawForce=50 не крутит mass=2000 | Crane-approach из 9d8bc1c | **ЧЕТВЁРТЫЙ КРУГ: копирование формул** |

**Итог M3.1:** 10 итераций, 0 рабочих переходов. Корабли "ползут" (Lifting→Yawing→Docked флик, никогда не долетают).

### Фаза M3.2 (глубокий рефакторинг)

| # | Коммит | СИМПТОМ | ДИАГНОЗ | ФИКС | ЧТО ПОШЛО НЕ ТАК |
|---|--------|---------|---------|------|-------------------|
| **M3.2.0** | bc5444b | Все предыдущие фиксы не работали | **AddTorque(ForceMode.Force) для mass=2000 даёт 0.02°/с²** | Прямой Rigidbody control (MoveRotation + linearVelocity) | **ПЕРВОЕ ПРАВИЛЬНОЕ РЕШЕНИЕ** |
| M3.2.1 | baf9107 | ShipController затирает velocity | ApplyAntiGravity + ClampVelocity | `if (_hasNpcPilot && _pilots.Count==0) return;` | **ПЯТЫЙ КРУГ: пропуск физики ShipController** |
| M3.2.2 | 886722b | Нет CommZone/pad | Выкинул навигацию в rewrite | CommZone check, altitude hold | Вернул навигацию |
| M3.2.3 | 56f8202 | "ползёт вперёд" на взлёте | Pitch-наклон от падения | rotation reset в SetMode(Lifting) | Визуально чисто |
| M3.2.4 | 991591b | NPC скользят/падают | Нет EnterDocked → isKinematic=false | isKinematic=true в Docked | **СЛОМАЛ: заморозил NPC** |
| M3.2.5 | 8526cf9 | "ползёт вперёд" (снова) | Collider зацепляет геометрию | detectCollisions=false в Lifting | **ШЕСТОЙ КРУГ: симптом, не причина** |
| M3.2.6 | 0cb0a9a | NPC стоят (isKinematic) | Guard выходил до dwell-check | guard: `rb.isKinematic && mode!=Docked` | **СЕДЬМОЙ КРУГ: блокировка своей же логики** |
| M3.2.7 | a3f7469 | Не возвращаются | schedule не advance | AdvanceScheduleForCurrentNpc() | Нет освобождения падов |
| M3.2.8 | fd7db52 | schedule advance сразу | DockedSinceTime=-1000 | `_scheduleAdvancedAfterDock=true` сначала | Правильно |
| M3.2.9 | 3017913 | schedule advance блокирован | guard `ship.IsDocked` → return | AdvanceSchedule в SetMode(Docked) | **ВОСЬМОЙ КРУГ: guard блокировал advance** |
| M3.2.10 | 64dfa6a | dwell не срабатывает | Тот же guard блокирует dwell | guard: `IsDocked && mode!=Docked` | Правильно |
| M3.2.11 | f6a... (lost) | Double schedule advance | `_scheduleAdvancedAfterDock` не ставится | Флаг в AdvanceScheduleForCurrentNpc() | Правильно |
| M3.2.12 | b6ee4f2 | Паркуются в центр | CruiseTargetPos=station center | pad pos после AssignPad | Правильно |
| M3.2.13 | f34a727 | **Старый пад не чистится** | AssignedPadId между легами | null в Lifting | **ДЕВЯТЫЙ КРУГ: потерял пад при дистанции** |
| M3.2.14 | b973cee | Пады заняты | Release не вызывался | ReleaseNpcAssignment в Lifting | Правильно |
| **M3.2.15** | 706a9d1 | Пад сбрасывается на CommZone | `distToPad>200f` очищал pad на 600м | Убрал dist-проверку | **ДЕСЯТЫЙ КРУГ: своя же защита убивала пад** |

---

## 3. Корневые причины (10 кругов)

### Круг 1: Config mismatch
`locationId` расходился (PRIMIUM_TEST_ZONE vs PRIMIUM_TEST_ZONE_2) из-за SO в разных папках — один в `Resources/PeacefulShip/`, другой в `Docking/Resources/Data/`.

### Круг 2: Scene vs Code forces
yawForce/thrustForce/verticalForce в сцене. 4 NPC имели разные значения (50/500/5000). При попытке унифицировать — каждый раз менял то код, то сцену, не понимая что влияет.

### Круг 3: NGO NetworkVariable race
`_netIsDocked.Value` батчится NGO → синхронизируется на следующий кадр. NavTick guard `if (ship.IsDocked)` срабатывал на stale значении. 3 фикса, и каждый ломал что-то ещё.

### Круг 4: ForceMode.Force ≠ angular velocity (ГЛАВНЫЙ)
```csharp
_rb.AddTorque(Vector3.up * yawRate, ForceMode.Force);
```
`ForceMode.Force` = torque (Н⋅м). Для mass=2000, inertiaTensor≈50000: torque=15 → accel=0.017°/с² → 10000 сек на 180°. **Ни один фикс M3.1 не мог работать из-за этого.** Я не понял 5 итераций.

### Круг 5: ShipController затирает velocity
`ShipController.FixedUpdate` применяет AntiGravity + ClampVelocity даже при `_hasNpcPilot=true`. Моя `linearVelocity` из NavTick обнуляется. Нужен был guard `if (_hasNpcPilot && _pilots.Count==0) return;`.

### Круг 6: Collider зацепляет геометрию
Даже с `linearVelocity=(0,8,0)` — коллайдер NPC касается станции → contact force даёт горизонтальный impulse.

### Круг 7: Guard против самого себя
`rb.isKinematic=true` в Docked → `if (rb.isKinematic) return;` выходил до dwell-check → NPC висел навечно.

### Круг 8: IsDocked guard блокирует advance
`if (ship.IsDocked) return;` — выходил до schedule advance + dwell. SetMode(Docked) не работал.

### Круг 9: Pad ID очищался на дистанции
`distToPad > 200f → AssignedPadId = null`. NPC входит в CommZone на 600м → пад в 50м от центра → 600 > 200 → очистка сразу после назначения.

### Круг 10: Pad не освобождается
`ReleaseNpcAssignment` не вызывался → пады "заняты" навсегда → новым NPC не назначаются.

---

## 4. Что реально нужно было сделать (постфактум)

```
1. MoveRotation + linearVelocity (не AddTorque/ForceMode)
2. ShipController skip physics для NPC
3. isKinematic=false, detectCollisions=false в Lifting
4. CruiseTargetPos = padPos (не stationPos) в Berthing
5. ReleaseNpcAssignment при Lifting
6. AdvanceSchedule при Berthing→Docked
7. Не чистить AssignedPadId на расстоянии
```

Из 15 попыток — **только 3 были правильными** (M3.2.0, M3.2.1, M3.2.15). Остальные 12 — хождение по кругу.

---

## 5. Текущая архитектура (M3.2.15)

```
NpcShipWorld.FixedUpdate → controller.NavTick(dt)

NavTick(Docked):
  - isKinematic=true
  - DockedSinceTime < 0 → установить
  - schedule advance (если после полёта)
  - dwell → isKinematic=false → ExitDocked → Lifting

NavTick(Lifting):
  - rotation reset (pitch/roll = 0)
  - detectCollisions = false
  - linearVelocity = (0, LiftSpeed, 0)
  - pos.y > startY+5 → ResolveTargetStation → set CruiseTargetPos → Yawing

NavTick(Yawing):
  - detectCollisions = true
  - MoveRotation к target bearing (MaxYawRate deg/s)
  - |deltaYaw| < 3° → Cruising

NavTick(Cruising):
  - MoveRotation (постоянная коррекция)
  - linearVelocity = dir * speed + altitude hold
  - CommZone detection → stop → Berthing

NavTick(Berthing):
  - padId = TryAssignPadFromDispatcher()
  - CruiseTargetPos = padPos
  - dist < 1.5f → EnterDocked → Docked
```

### ShipController (изменения)
```csharp
if (_hasNpcPilot && _pilots.Count == 0) return; // skip ALL physics
```

---

## 6. Ключевые открытия

| Открытие | Цена | Когда поняли |
|----------|------|-------------|
| `AddTorque(ForceMode.Force)` не крутит mass=2000 | 5 итераций (M3.1.1 → M3.1.11) | M3.2.0 |
| ShipController затирает velocity NPC | 1 итерация (M3.2.0 → M3.2.1) | M3.2.1 |
| `isKinematic` guard блокирует свой код | 2 итерации (M3.2.4 → M3.2.6) | M3.2.6 |
| Schedule advance должен быть в SetMode, не в NavTick | 2 итерации (M3.2.7 → M3.2.9) | M3.2.9 |
| Pad ID не очищать на дистанции | 2 итерации (M3.2.13 → M3.2.15) | M3.2.15 |
| ReleaseNpcAssignment при вылете | 1 итерация | M3.2.14 |

---

## 7. Что дальше

### Известные проблемы (нуждаются в исправлении)

1. **DwellTime=5s (хардкод)** — должен браться из route.dwellTimeSec
2. **AltHold=+5м (хардкод)** — должен быть высота пада + garage offset
3. **AssignedPadId дублируется** — NpcShipState.AssignedPadId (старая FSM) vs controller.AssignedPadId (новая)
4. **TryAssignPadFromDispatcher нет throttle** — вызывается каждый FixedUpdate
5. **detectCollisions=false** остаётся true при переключении SetMode в Lifting (должно быть false на весь Lifting)
6. **4 NPC на одной станции** — maxConcurrentLandings может ограничивать
7. **CommRange=600 vs 242.9** — пользователь сам уменьшает; код должен брать из сцены

### Не трогать

- ShipController.AddTorque (игрок)
- ShipController.ApplyAntiGravity (игрок)
- DockingWorld.AssignPadForNpc
- DockingPadTriggerBox / OuterCommZone

---

## 8. Уроки

1. **Не менять настройки сцены в коде** — commRange, yawForce, locationId должны быть в Префабе/SO, не в Editor tool
2. **Guard-ы не должны блокировать свой же switch-case** — NavTick guard `rb.isKinematic` убил dwell-check на 2 итерации
3. **Выключение коллизий при взлёте** — detectCollisions=false чинит "ползёт вперёд"
4. **Причина ≠ симптом** — 10 из 15 итераций лечили симптомы (bearing gate, crane-approach) вместо корня (ForceMode.Force ≠ angular velocity)
5. **NetworkVariable race** — `_netIsDocked.Value` читается stale; не использовать как guard внутри NavTick
