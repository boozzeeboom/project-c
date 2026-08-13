# 11 — Ресерч: почему NPC-корабли «тупят» в доках

> **Project C: The Clouds** | Unity 6000.4.1f1 | NGO 2.11.0 | URP 17.0.3
> **Дата:** 2026-07 (deep research по коду + префабам + сцене)
> **Связано с:** `07_SHIP_PROXIMITY_AVOIDANCE.md`, `10_BUILD_PROXIMITY_AVOIDANCE.md`,
> `M2_FSM_DIAGNOSIS.md`, `99_RETROSPECTIVE.md`, `08_CONTROL_AUTHORITY_AND_PHYSICS.md`
> **Статус:** Ресерч. Код не менялся — только факты и план.

---

## 1. TL;DR

Корабли «тупят в доках» не из-за одной причины, а из-за связки дефектов:

1. `Berthing` — слепая прямая линия к паду с дистанции comm-зоны (422–1000 м)
   **без avoidance и без таймаута** → вечное упирание в стены/крыши.
2. Окно посадки 90 сек истекает для NPC всегда (`used` не выставляется) →
   пад переназначается пока корабль летит → два корабля на одном паде.
3. `Consider Buildings` физически не работает: во всём проекте одна
   `NpcProximityZoneBuilds`, и у неё **0 валидных коллайдеров**.
4. Dwell до 5000 с × 20 кораблей на 21 пад → пад-голодание; ожидание пада =
   вечное зависание в воздухе (нет holding-точки/таймаута/divert).
5. Взлёт всего на +5 м оставляет корабль в «чаше» порта; горизонтальный
   выход через плотную геометрию = столкновения.

**Главная рекомендация:** «вертикальный коридор» — подъём выше палубы перед
горизонтальным полётом и вертикальный спуск на пад. Замерено: над всеми падами
Примума 300 м+ свободного неба по вертикали.

---

## 2. Что есть сейчас (факты)

### 2.1 Масштаб

| Факт | Значение |
|------|----------|
| NPC-корабли в `WorldScene_0_0` | 20 (по одному каждого именованного префаба) |
| Станций в `WorldScene_0_0` | 10 |
| `DockStation_Primium` | 21 пад, плотная городская геометрия `gorod port.glb` |
| Пады Примума | 13–15 солид-коллайдеров в радиусе 60 м от каждого |
| Расстояние между падами | 35–55 м |
| Avoidance-радиусы кораблей | 10–180 м (Гигант=180, Пещера=120, Мастодонт≈90) |
| Вертикальный зазор над падами Примума | **300 м+ свободно** |
| Горизонтальный зазор от пада | 121–165 м (лучшее направление), 300 м на периферии |

### 2.2 FSM и avoidance (текущий код)

- FSM: `Docked → Lifting(+5 м) → Yawing → Cruising → Berthing → Docked`
  (`NpcShipController.cs:425-529`), прямой `Rigidbody`-контроль.
- Ship-to-ship avoidance — **только в `Cruising`** (`:503-504`).
- Build avoidance — в `Lifting`/`Yawing`/`Cruising` (`:495-507`).
- **В `Berthing` нет никакого avoidance** (`:647-692`).
- Манёвр avoidance: `Separate → Stop → BackOff` + `avoidTimeout=8 с`
  (`:930-966`), escape-веер — только горизонтальные лучи (`:859-879`).
- Физические коллизии NPC включены всегда (`detectCollisions = true`),
  `Physics.IgnoreCollision` между NPC отсутствует (удалён при M3.2-переписи).

---

## 3. Почему `Consider Buildings` не помогает — система мертва

Во всём проекте (24 сцены) ровно **один** компонент `NpcProximityZoneBuilds`
— на `gorod port` в `WorldScene_0_0` (scene:3156). И он нерабочий:

1. `gorod port.glb` имеет **0 коллайдеров в префабе**.
2. В сцене на него добавлен **не-convex MeshCollider**.
3. `RefreshColliders()` отфильтровывает не-convex MeshCollider
   (`IsClosestPointSupported`, `NpcProximityZoneBuilds.cs:76-81`).
4. Итог: `_validColliders.Count == 0` → `IsIntruding()` всегда `false` →
   `FindClosestBuildConflict()` всегда `null`.

`considerBuildings=1` стоит у всех 20 кораблей, но учитывать нечего.

---

## 4. Корневые причины «ступора» (по убыванию тяжести)

### P0-1. `Berthing` — слепая прямая без avoidance и без таймаута

`TickBerth` (`NpcShipController.cs:647-692`): полёт по прямой к паду со скоростью
`min(ApproachSpeed, dist*2)`. В Berthing отключены оба вида avoidance, **нет
таймаута и нет детекции отсутствия прогресса**. Корабль входит в Berthing на
границе comm-зоны (422 м у Примума, **1000 м у дорожных станций**) и тянет
прямую сквозь городскую геометрию. Коллайдеры включены → корабль упирается и
**вечно давит velocity в препятствие**.

### P0-2. Окно посадки 90 с истекает для NPC всегда

`AssignPadForNpc` ставит `landingWindowSec` (дефолт 90 с из SO). Флаг `used`
выставляется только в `ConfirmTouchdown`, который вызывается **только из
RPC-пути игрока** (`DockingServer.cs:266`). NPC не вызывает его никогда. Через
90 с `DockingWorld.Update` (`:464-480`) делает `ReleaseAssignment` → пад
освобождается, пока NPC ещё летит. Тяжёлые корабли (ApproachSpeed 2–3 м/с)
физически не успевают за 90 с. Другой NPC получает тот же пад → два корабля
сходятся на одну точку → `EnterDocked` у обоих по `dist<1.5`. `AssignedPadId`
в контроллере при этом не сбрасывается (чистится только в `SetMode(Lifting)`).

### P0-3. Пад-голодание + вечное зависание в ожидании

- Dwell из SO: Гигант `60 + rand(250..5000)` → до 83 минут на паде; Сильфида
  `dwellTimeSec=5000`. 20 кораблей на 21 пад → пады заняты почти всегда.
- При отказе `AssignPadForNpc` корабль обнуляет velocity и висит
  (`:663-664`), retry каждые 3 с. Нет holding-точки, нет таймаута, нет divert.
- `maxConcurrentLandings=1` (дефолт) ужесточает очередь.

### P1-4. Корабль-к-кораблю слепота именно внутри дока

Avoidance ship-to-ship проверяется **только в `Cruising`**. В
`Lifting`/`Yawing`/`Berthing` (т.е. внутри дока) корабли друг друга не видят.
Пады в 35–55 м, радиусы 25–180 м, массовый взлёт после dwell → корабли
толкаются корпусами, сдвигают друг друга с падов, заклинивают о геометрию.
Зависший в Berthing корабль не считается препятствием (`IsAvoidable` исключает
Berthing, `NpcProximityZone.cs:247-255`) → в него влетают крейсерские.

### P1-5. Взлёт всего на +5 м

`TickLift`: `targetY = LiftStartY + 5f` (`:569`). Корабль перестаёт набирать
высоту на уровне крыш и должен выбираться горизонтально через самую плотную
геометрию. Вертикальный выход (300 м+ свободно) не используется.

### P2-6. Даже рабочая build-зона не даст манёвру завершиться

- `IsClearOf(build)` требует дистанцию > `avoidanceRadius × 1.5 + 30` до
  любого коллайдера здания. Для Гиганта это 300 м — в городе недостижимо.
- Манёвр всегда завершается только по `avoidTimeout=8 с` →
  `ResumeFromAvoid` → на следующем тике конфликт детектится снова (cooldown
  отсутствует) → вечный цикл `Avoiding ↔ Cruising`.
- Манёвр строго горизонтальный (`away.y=0`, `:936`); escape-веер горизонтальный.
  В Π-доке/чаше порта выход — вверх, но вертикаль не рассматривается.

### P2-7. Побочные баги

- **Мульти-маршруты сломаны**: `AdvanceScheduleForCurrentNpc` (`:733-756`)
  использует только `routes[0]` туда-обратно. Гигант/Мастодонт/Вавилон имеют
  по 3–4 маршрута, но летают только первый. Полноценная
  `NpcShipWorld.AdvanceScheduleIndex` (modulo) — в мёртвом `TickNpc`.
- `NpcShipTrafficManager` — мёртвый код (`ScheduleNextArrival` не вызывается).
- `Средняя 0_0`: `Pad_01` и `Pad_02` имеют идентичную позицию.

---

## 5. Рекомендуемое решение: «вертикальный коридор» (dock chimney)

Не маневрировать горизонтально внутри дока вообще. Над падами 300 м+ свободно.
Два новых режима рядом с существующими, под фичефлагом `useCorridorNav`
(старый путь не трогаем):

```
ВЫХОД:  Docked → Lifting → [DepartClimb: вертикально до deckClearanceY]
        → Yawing → Cruising (НАД городом)

ЗАХОД:  Cruising → Berthing(назначение пада у comm-зоны, как сейчас)
        → [OverheadCruise: горизонталь к точке pad + (0, clearance, 0)]
        → [Descend: строго вертикальный спуск на пад] → Docked
```

- `deckClearanceY` — SerializeField per-station (дефолт pad Y + 60–80 м).
- Тот же crane-style (`linearVelocity`/`MoveRotation`), без pathfinding/NavMesh.
- Горизонтальное движение всегда выше крыш; Avoiding остаётся для Cruising.

### Safety-неты

1. **Berthing watchdog**: если `dist` не уменьшается N секунд → прервать заход:
   набор высоты до clearance → повторный запрос пада (или следующая станция
   после K попыток).
2. **Pad window fix**: при `EnterDocked` в NPC-пути вызывать
   `DockingWorld.ConfirmTouchdown(npcInstanceId, shipNetId, padId, stationId)`
   (выставит `used=true`) + в `TickBerth` обновлять `assignedAt` при уменьшении
   дистанции (progress refresh).
3. **Holding-точка**: при отказе в паде — подъём до clearance и зависание над
   станцией, а не `velocity=0` на месте.

### Расход кораблей (separation)

4. **Эшелонирование**: высота cruise = `clearance + (NpcInstanceId % 4) × 15 м`
   внутри воздушного пространства станции.
5. **Departure mutex per station**: не более одного Lifting/DepartClimb в радиусе
   R одновременно (приоритет — существующий `AvoidancePriority`).
6. Ship-to-ship avoidance включить и в `Lifting`/`Yawing`; расширить
   `IsAvoidable` режимом Berthing-hovering (застывший корабль = препятствие).

### Починка build-зон

7. Editor-инструмент: генерация дочерних BoxCollider'ов по рендер-боундам
   здания. **Не вешать зону на сам док/чашу порта** — только на отдельно
   стоящие препятствия.
8. `IsClearOf(build)` — использовать `AvoidanceExtent`, не `ClearExtent`
   (или кап ~1.2×). Cooldown 2–3 с после timeout-resume.
9. Escape corridor: добавить вертикальные лучи (fallback «вверх»).

### Конфиг (нулевой риск)

10. Cap dwell: `maxDwellTimeSec` 5000 → 300–600 с.
11. Разнести `Pad_01`/`Pad_02` на `Средняя 0_0`.
12. Починить мульти-маршруты (port `AdvanceScheduleIndex` в контроллер).

---

## 6. Что НЕ трогаем

- Прямой `Rigidbody`-контроль (`linearVelocity`/`MoveRotation`) — не возвращать
  `AddTorque(ForceMode.Force)`.
- `ShipController`, `EnableNpcPilot`, `_hasNpcPilot` gate.
- `EnterDocked`/`ExitDocked`/`AssignPadForNpc` (только **добавление** вызова
  `ConfirmTouchdown` из NPC-пути, без изменения сигнатур).
- `rb.detectCollisions = true` всегда (регрессия T-NS11).
- NGO/RPC/NetworkTransform.

---

## 7. Порядок внедрения (эффект/риск)

| # | Тикет | Эффект | Риск |
|---|-------|--------|------|
| 1 | Конфиг: cap dwell, разнести дублированные пады | Убирает «стоят вечно» и часть голодания | Нулевой |
| 2 | Pad window fix (`ConfirmTouchdown` + progress refresh) | Убирает двойную посадку на один пад | Низкий |
| 3 | Berthing watchdog + holding-точка | Убирает «вечное тупление» | Низкий |
| 4 | Corridor modes (DepartClimb/OverheadCruise/Descend) | Структурное решение захода/выхода | Средний |
| 5 | Эшелонирование + departure mutex | Расход между кораблями | Средний |
| 6 | Build zones: инструмент + кап clear + cooldown + вертикальный луч | Рабочий обход зданий | Низкий |
| 7 | Мульти-маршруты + TrafficManager | Настоящие маршрутные сети | Низкий |

---

## 8. Метрики для верификации

После внедрения мерять:

- Время `Berthing → Docked` (сейчас у тяжёлых > 90 с — окно истекает).
- Количество циклов `Avoiding → Cruising → Avoiding` (сейчас стремится к
  бесконечности у работающих build-зон).
- Число пар «два корабля на одном паде» (по `_occupiedPads` + физический
  OverlapBox).
- Доля кораблей в `Berthing` дольше N секунд (зависания в ожидании пада).

Логи диагностики: `debugMode` на `NpcShipController`, `verboseBuildLogging` на
`NpcProximityZone`, `drawGizmos` на `NpcProximityZoneBuilds`.
