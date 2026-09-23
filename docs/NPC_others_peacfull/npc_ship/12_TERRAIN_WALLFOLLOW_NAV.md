# 12 — Terrain WallFollow + City Gates (облёт гор и городов)

> **Project C: The Clouds** | Unity 6000.4.1f1 | NGO 2.11.0
> **Статус:** дизайн принят, реализация T-NS-WF01..WF03 + T-NS-GATE04 (2026-09-23)
> **Связано с:** `07_SHIP_PROXIMITY_AVOIDANCE.md`, `10_BUILD_PROXIMITY_AVOIDANCE.md`,
> `11_DOCK_NAV_RESEARCH.md`, `M2_FSM_DIAGNOSIS.md`
> **Лор-ограничение:** корабли ОБЛЕТАЮТ пики вокруг (не перелетают сверху).
> Высота — core gameplay, принудительный набор высоты запрещён.

---

## 1. Решения пользователя (зафиксированы)

| # | Решение |
|---|---------|
| D1 | Уровень 0 (пол высоты / terrain-floor) — **отклонён**. Алтитуда — геймплей, коридоры высот в отладке |
| D2 | Обход — **латеральный** (вокруг), вертикаль в обходе заморожена на высоте входа |
| D3 | Возврат к профильной высоте после выхода на LOS — **нужен**, пологой глиссадой (кап вертикали) |
| D4 | Ворота городов — **отдельный тикет с kill-switch** (`useCityGates=false` = старое поведение бит-в-бит) |
| D5 | Proximity-проверка работает и в `WallFollow`. Задел на 200+ кораблей — сразу (stagger + broadphase) |
| D6 | Цель прямо за большой горой — решается **цепочкой с правилом стены**, не одной точкой |

---

## 2. Почему не точка, а правило стены (D6)

Одна detour-точка не работает для массива шире `lookAhead`-дистанции: корабль
долетает до точки, смотрит на цель — а там опять гора. Вместо точки хранится
**направление**: «вперёд + от склона», а условие выхода — **прямая видимость
(LOS) до цели**, а не прибытие в точку. Это упрощённый bug-алгоритм:

```
пока LOS до цели закрыт:
    держать гору с одной и той же стороны (право/лево — фиксируется при входе)
    лететь вперёд со сдвигом от склона на clearance
как только LOS чист → resume Cruising (цель — прежняя CruiseTargetPos)
```

- Массив любой ширины огибается — хоть весь хребет.
- Правило одной стороны исключает метания влево-вправо.
- Сторона при входе выбирается скорингом веера (где чище + меньше отклонение от курса).

---

## 3. Режимы и переходы (T-NS-WF01)

Новые значения в `NpcShipController.NavMode` (в конец enum — сериализация не ломается):

| Режим | Вход | Выход |
|---|---|---|
| `WallFollow` | `Cruising`: forward-SphereCast попал в террейн/скалу (stagger-проверка) | LOS до цели чист → `Cruising`; таймаут/круг 360°/застревание → `DivertToNextStation` |
| `GateApproach` | `Cruising`: до станции < `gateTriggerDist`, вне `cityRadius`, `useCityGates=true` | dist до ворот < tol → `CorridorLeg` |
| `CorridorLeg` | из `GateApproach` | вход в `OuterCommZone` (та же проверка, что в `TickCruise`) → `Berthing` |

`WallFollow` — transient для сейвов (`RestoreFromSave`: маппится в `Cruising`,
как `Avoiding`). Ворота процедуру не хранят мировых точек: цель ворот считается
вживую каждый тик из `cityCenter/cityRadius` (F8-безопасно по построению).

---

## 4. Steering-контур WallFollow

- **Детекция:** 1× `SphereCast` вперёд (радиус = `wallProbeRadius`, дистанция =
  `clamp(CruiseSpeed × wallLookAheadSec, min, max)`), `QueryTriggerInteraction.Ignore`.
  Попадания в объекты с `ShipController`/`NpcShipController` в родителях игнорируются
  (корабли — зона ответственности proximity-системы, не стены).
- **Выбор стороны:** веер 5 лучей (0, ±30°, ±60°, горизонталь): сторона с большим
  клиренсом минус штраф за угловое отклонение от курса.
- **Движение:** yaw через `MoveRotation` (`MaxYawRate`), `linearVelocity` =
  горизонталь-команда × `CruiseSpeed` + удержание `_wallEntryY` (кап ±2 м/с).
  Вертикального обхода нет (D2).
- **Выход на LOS:** staggered Raycast корабль→цель тем же фильтром. Чисто → `Cruising`
  + cooldown против дребезга. Возврат к профилю — штатный `altHold` из `TickCruise`
  с капом `returnVerticalCap` (D3).
- **Предохранители:** `wallTimeoutSec` → divert; накопленный разворот азимута ≥ 360°
  (кольцевая гора / кружение) → divert; нет смещения > N метров за `wallNoProgressSec`
  (притирание к склону) → divert. Divert — существующий `DivertToNextStation`.
- **Рамка коридора:** обход не меняет высоту, поэтому из коридора не выводит по
  построению. `AltitudeCorridorSystem` читается как рамка, не мутируется
  (мутация SO сдвигом — кейс `09M`, запрещён).

---

## 5. Масштаб 200+ (T-NS-WF02)

- **Stagger проб:** forward-probe и LOS не каждый `FixedUpdate`, а раз в
  `wallProbeIntervalSec` (0.2 с) со сдвигом фазы по `NpcInstanceId % K`.
- **Broadphase для ship-to-ship:** spatial hash по XZ в `NpcShipZoneRegistry`
  (ячейка 500 м, перестроение не чаще 0.2 с, запрос 3×3 ячеек). `FindClosestConflict`
  идёт через индекс вместо полного перебора `All`. Логика пересечений не менялась.
- **Proximity в WallFollow:** ship-проверка расширена с `Cruising` на
  `Cruising + WallFollow`. Приоритет: корабль важнее горы — `EnterAvoid` из
  `WallFollow`, `_resumeMode` умеет помнить `WallFollow` (уже обобщён:
  `CurrentMode`, кроме Avoiding/AvoidYield).
- Приёмка: профайлер Nav-проверок при 20 кораблях + экстраполяция на 200.

---

## 6. Floating Origin (T-NS-WF03)

Состояние `WallFollow` F8-безопасно по построению: сторона (sbyte), таймеры,
накопленный азимут — не мировые координаты. Но `CruiseTargetPos`, `_avoidFromPos`,
`_wallEntryY`, `LiftStartY` — мировые и протухают после F8 (старый долг:
хука не было вообще).

- `NpcShipController.ApplyRebaseTranslation(Vector3 t)` — сдвигает 4 поля,
  возвращает счётчик (паттерн `StormCellDirector`, `WindManager`).
- `GlobalMotionControlledRebaseSlice.ShiftNpcShipNav()` — `FindObjectsByType`
  один раз на путь (урок T-FO09G-fix: не в цикле участников), вызов в success
  и в rollback-revert, маркер `NpcShipNavShifted`. Клиентам не рассылается:
  NavTick server-only, на клиентах контроллер выключен.
- Сейвы: `navMode` теперь может быть 7..9 — `RestoreFromSave` маппит transient
  режимы в `Cruising`; `CruiseTargetPos` уже покрыт кумулятивом PERSIST01.

---

## 7. Ворота городов (T-NS-GATE04, kill-switch)

- Флаг `useCityGates` (default **false**). Выключен = старое поведение бит-в-бит.
- Код gates — отдельная область/файл, одна точка входа из `TickCruise`.
  Удаление тикета = удалить область + один if + 2 значения enum.
- Ворота процедурные: точка на окружности `cityRadius` (из существующего
  `AltitudeCorridorData`) на ближней к кораблю стороне + азимутальный разнос
  по `NpcInstanceId % gateVariants`. Сопоставление «станция ↔ городской коридор» —
  по дистанции (станция внутри `cityRadius` + margin). Если коридора нет —
  ворота пропускаются, обычный заход.
- `CorridorLeg` ведёт к центру станции на профильной высоте; дальше штатный
  `Berthing` (Overhead→Descend) без изменений.

---

## 8. Что НЕ трогаем (как в `11_` §6 + новое)

- Прямой Rigidbody-контроль; `ShipController`/`EnableNpcPilot`; `EnterDocked` /
  `ExitDocked` / `AssignPadForNpc`; `detectCollisions=true`; NGO/RPC/NetworkTransform.
- `AltitudeCorridorData` (SO) — только чтение (кейс `09M`).
- Старый `Avoiding`-манёвр и `NpcProximityZoneBuilds` — не трогаем, работают как раньше.
- `Core/NavMode.cs` — не используется контроллером, не трогаем.

---

## 9. Тикеты

| # | Тикет | Объём |
|---|---|---|
| T-NS-WF01 | `WallFollow`: enum + Enter/Tick/Resume + сторона + LOS + watchdog/divert + кап возврата | ~250 LOC в `NpcShipController.cs` |
| T-NS-WF02 | Spatial hash в реестре + stagger проб + proximity в `WallFollow` | ~80 LOC (`NpcShipZoneRegistry.cs`, `NpcProximityZone.cs`) |
| T-NS-WF03 | `ApplyRebaseTranslation` + `ShiftNpcShipNav` в слайсе (success + rollback) | ~40 LOC |
| T-NS-GATE04 | `useCityGates` + `GateApproach`/`CorridorLeg` + процедурные ворота | ~150 LOC, default false |

## 10. Проверка (делает пользователь, Play Mode)

1. Маршрут через гору: корабль огибает по дуге, выходит на LOS, возвращается на профиль, долетает. Лог `Cruising → WallFollow → Cruising`.
2. Цель за большим массивом: нет петель Avoiding↔Cruising, нет зависания; в худшем случае — divert на другую станцию (лог).
3. F8 во время `WallFollow`: `runtimeRebase.Completed`, корабль продолжает облёт, 0 errors.
4. `useCityGates=true`: заход через ворота, посадка; `=false`: поведение как до патча.
5. Console → 0 errors после compile.
