# 07 — Ship-to-Ship Proximity Avoidance (расхождение NPC-кораблей)

> **Project C: The Clouds** | Unity 6000.4.1f1 | NGO 2.11.0
> **Статус:** M4 — дизайн + первичная реализация (2026-07-01)
> **Автор фичи-запроса:** пользователь. Цель — NPC-корабли не «влетают» друг в друга,
> а видят соседей и корректно расходятся, после чего продолжают прошлый маршрут.
> **Не ломать:** см. §6 и `M2_FSM_DIAGNOSIS.md` §5 «Что НЕ меняем».

---

## 1. Назначение

Вокруг каждого NPC-корабля назначаются **две сферические зоны** (радиусы настраиваются
в инспекторе на каждом корабле индивидуально):

| Зона | Поле | Смысл |
|------|------|-------|
| **Большая — зона связи** | `awarenessRadius` | В её радиусе корабль «знает» о других NPC-кораблях, которые работают рядом. Используется как фильтр кандидатов на расхождение. |
| **Малая — зона расхождения** | `avoidanceRadius` | Когда две малые зоны пересекаются — запускается манёвр расхождения. |

Логика манёвра при пересечении малых зон:

```
отодвинуться друг от друга → остановиться → отъехать →
если зоны больше не пересекаются → повторно проложить прошлый маршрут
```

«Прошлый маршрут» = сохранённый `NavMode` (обычно `Cruising`) и текущая цель
`CruiseTargetPos`. Восстановление = возврат в этот режим без пересчёта станции.

---

## 2. Почему это ложится на текущую архитектуру

- Уже есть образец «зоны вокруг объекта» — `OuterCommZone`
  (`Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`): `SphereCollider` +
  радиус в инспекторе + server-only детекция + гизмо. Новая зона повторяет паттерн,
  но целями считает другие NPC, а не игрока/станцию.
- Уже есть реестр для перебора кораблей — `NpcShipZoneRegistry.All`
  (`NpcInstanceId → NpcShipController`). Для 4 кораблей проверка «каждый с каждым»
  O(n²) незначительна.
- Движение уже целиком в `NpcShipController.NavTick` через прямое управление
  Rigidbody (`linearVelocity` / `MoveRotation`). Манёвр — ещё один режим в этом же
  цикле, в том же стиле.
- «Прошлый маршрут» тривиально восстанавливается: круиз — это прямая A→B к
  `CruiseTargetPos`; достаточно сохранить и вернуть режим.

---

## 3. Компоненты и интеграция

### 3.1 `NpcProximityZone` (новый, на корне корабля)

`Assets/_Project/Scripts/PeacefulShip/Stations/NpcProximityZone.cs`

- Хранит `awarenessRadius`, `avoidanceRadius`, `clearHysteresis` (SerializeField, per-ship).
- **Детекция — по дистанции через `NpcShipZoneRegistry.All`, а НЕ через физ-триггеры.**
  Причина: физ-коллизии между NPC намеренно приглушены (`rb.detectCollisions=false`
  на взлёте — `NpcShipController.cs:325`; риск Layer Collision Matrix —
  `M2_FSM_DIAGNOSIS.md` §7). Дистанция детерминирована, дешева, без магии триггеров.
- `FindClosestConflict(out float dist)` — ближайший NPC, чья малая зона пересекается
  с нашей и который находится в пределах `awarenessRadius`. Игнорирует корабли в
  `Docked`/`Berthing` (у станции рулит pad-contention).
- Компонент **опционален**: если его нет на корабле — расхождение просто выключено
  для этого корабля. Существующие оттюненные корабли не меняют поведение, пока
  компонент не добавлен.

### 3.2 `NpcShipController.NavTick` (правка, минимальная)

- Новое значение enum: `NavMode { Docked, Lifting, Yawing, Cruising, Berthing, Avoiding }`
  (добавлено в конец — `CurrentMode` не сериализуется, порядок безопасен).
- В начале тика, **только когда `CurrentMode == Cruising`**, вызывается
  `ProximityZone.FindClosestConflict`. При конфликте → `EnterAvoid()`.
- `case NavMode.Avoiding: TickAvoid(rb);` — трёхфазный манёвр (см. §4).
- Тюнинг манёвра — SerializeField на контроллере (никаких magic numbers).

---

## 4. Манёвр `Avoiding` (3 фазы + гистерезис)

| Фаза | Что делает | Завершение |
|------|-----------|-----------|
| `Separate` | `linearVelocity` в направлении «от соседа» (горизонталь), `avoidSeparateSpeed` | по `avoidSeparateTime` |
| `Stop` | обнулить `linearVelocity`/`angularVelocity` | по `avoidStopTime` |
| `BackOff` | короткий отъезд по вектору «от соседа», `avoidBackOffSpeed` | по `avoidBackOffTime`, затем проверка «чисто?» |

После `BackOff`:
- если малые зоны разошлись (с гистерезисом `ClearRadius = avoidanceRadius * clearHysteresis`)
  → `ResumeFromAvoid()` → возврат в сохранённый `_resumeMode` (Cruising) с прежним
  `CruiseTargetPos`;
- иначе цикл повторяется с `Separate`.

**Гарантии стабильности:**
- Расхождение **симметрично**: оба корабля считают вектор «от соседа», уходят в
  противоположные стороны — не «дерутся» за одно направление.
- `avoidTimeout` (safety): по истечении принудительный `ResumeFromAvoid`, чтобы
  корабль не завис в манёвре.
- Гистерезис на выходе исключает дребезг «зашёл-вышел-зашёл».

---

## 5. Параметры (инспектор)

**`NpcProximityZone` (per-ship):**

| Поле | Дефолт | Смысл |
|------|--------|-------|
| `awarenessRadius` | 400 | Большая зона связи |
| `avoidanceRadius` | 120 | Малая зона расхождения |
| `clearHysteresis` | 1.5 | Множитель радиуса «разошлись» |

**`NpcShipController` (манёвр, server-only):**

| Поле | Дефолт | Смысл |
|------|--------|-------|
| `avoidSeparateSpeed` | 8 | Скорость расхождения (м/с) |
| `avoidSeparateTime` | 1.5 | Длительность фазы Separate (с) |
| `avoidStopTime` | 0.7 | Пауза Stop (с) |
| `avoidBackOffSpeed` | 5 | Скорость отъезда (м/с) |
| `avoidBackOffTime` | 1.0 | Длительность BackOff (с) |
| `avoidTimeout` | 8 | Предохранитель: макс. время в манёвре (с) |

> Радиусы подобрать так, чтобы `avoidanceRadius` был заметно меньше `awarenessRadius`
> и обе были меньше дистанции между станциями, иначе корабль будет «расходиться»
> постоянно.

---

## 6. Что НЕЛЬЗЯ ломать (критично)

- **Прямое управление Rigidbody** (`linearVelocity` / `MoveRotation`) и `MaxYawRate` —
  не возвращать `ApplyMovementInput`/torque (`M2_FSM_DIAGNOSIS.md` R3/R7).
- **`Docked`: `isKinematic=true` + dwell-логика** (`NpcShipController.cs:276-293`) —
  манёвр не запускается в `Docked` (проверка `CurrentMode == Cruising`).
- **`rb.detectCollisions=false` на взлёте** (`NpcShipController.cs:325,330-333`).
- **`ExitDocked`/`EnterDocked`/`ReleaseNpcAssignment`** при смене лега — не трогаем.
- **`ShipController`, `DockingWorld.AssignPadForNpc`, NGO/RPC/NetworkTransform** —
  не трогаем (`M2_FSM_DIAGNOSIS.md` §5).
- **`Berthing`/pad-contention** — расхождение здесь выключено, чтобы не сломать посадку
  (`maxConcurrentLandings` уже управляет очередью у станции).
- **Никаких magic numbers** — все пороги через `[SerializeField]`.

---

## 7. Крайние случаи

| Случай | Реакция |
|--------|---------|
| 3+ корабля в одной точке | Расхождение только в `Cruising`; у станции рулит pad-contention |
| Сосед на паде / в Berthing | Игнорируется (`IsAvoidable` пропускает `Docked`/`Berthing`) |
| Оба отъезжают вечно | Симметричный вектор + `avoidTimeout` |
| Сосед исчез (despawn) во время манёвра | `_avoidOther == null` → `IsClearOfConflict()` == true → resume |
| Корабль без `NpcProximityZone` | Расхождение выключено, поведение как раньше |

---

## 8. Roadmap (тикеты)

| # | Тикет | Объём |
|---|-------|-------|
| T-NS-AV01 | `NpcProximityZone` компонент (радиусы + запрос по реестру + гизмо) | ~90 LOC |
| T-NS-AV02 | Интеграция в `NpcShipController.NavTick` (enum `Avoiding` + `EnterAvoid`/`TickAvoid`/`ResumeFromAvoid`) | ~90 LOC |
| T-NS-AV03 | Добавить `NpcProximityZone` на 4 корабля `NPC_Ship_HeavyII_01..04`, настроить радиусы | scene |
| T-NS-AV04 | Verify: два встречных корабля расходятся и продолжают маршрут; лог переходов `Cruising → Avoiding → Cruising` | Play Mode |

---

## 9. Открытые вопросы

| # | Вопрос |
|---|--------|
| Q1 | Нужен ли приоритет «кто уступает» (по `NpcInstanceId`), или симметричного расхождения достаточно? Сейчас — симметрично. |
| Q2 | Учитывать ли корабль игрока как препятствие для NPC? Сейчас — только NPC↔NPC. |
| Q3 | Нужна ли вертикальная составляющая расхождения (эшелонирование по высоте) или только горизонталь? Сейчас — горизонталь. |
