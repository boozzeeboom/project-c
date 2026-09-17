# NPC-корабли: ревью кругосветки, парковки/отстыковки и ухода от городов

Дата: 2026-09-17. Статус: **только планирование, кода нет**.
Тикет: не назначен (кандидат — серия `T-NS*`; см. ITERATIONS.md в `docs/NPC_others_peacfull/npc_ship/`).

## 1. Цель

Провести полное ревью кода NPC-кораблей, перепроверить ранее выполненный анализ
(`docs/NPC_others_peacfull/npc_ship/11_DOCK_NAV_RESEARCH.md` — далее **R11**),
разобраться с проблемами маршрутов в местах парковки и отстыковки и отсутствием
нормального ухода от городов («если включаем [уход] — они вообще тупят»),
оценить, можем ли улучшить курсирование. Результат — только план, без кода.

## 2. Что изучено

Доки (все в `docs/NPC_others_peacfull/npc_ship/`):
`00_README.md`, `11_DOCK_NAV_RESEARCH.md`, `ITERATIONS.md`,
`98_SUMMARY_POST_M3.2.md`, `99_RETROSPECTIVE.md`, `M2_FSM_DIAGNOSIS.md`,
`07_SHIP_PROXIMITY_AVOIDANCE.md`.

Код — `Assets/_Project/Scripts/PeacefulShip/` (27 файлов), ключевые:
- `NpcShipController.cs` (~1058 строк) — FSM `NavTick`: Docked → Lifting → Yawing → Cruising → Berthing;
  подъём `TickLift` (+5 м), прямой заход `Berthing` со скоростью `min(Approach, dist*2)`,
  avoidance только в Lifting/Yawing/Cruising, в Berthing — нет; ретрай пада каждые 3 с;
  стыковка при дистанции < 1.5 м; `AdvanceScheduleForCurrentNpc` ходит только по `routes[0]` (пинг-понг).
- `NpcProximityZone.cs` — `IsAvoidable` исключает Berthing/Docked; `FindClosestConflict`, `IsClearOf`.
- `NpcProximityZoneBuilds.cs` — фильтр `IsClosestPointSupported` отбрасывает невыпуклые (non-convex) меши.
- `NpcShipWorld.cs` — `FixedUpdate` дёргает `controller.NavTick` напрямую; `TickNpc`/`AdvanceScheduleIndex` — мёртвый код.
- `NpcShipTrafficManager.cs` — `ScheduleNextArrival` определён, вызовов ноль (мёртвый код).
- `DockingWorld.cs` — `AssignPadForNpc`/`RegisterPending` (`used=false`), `ConfirmAssignment`,
  `used=true` ставит только `ConfirmTouchdown`, который вызывается только из `DockingServer.cs`
  (игроковый RPC-путь); экспирация сметает `!used` по `landingWindowSec`; `ReleaseNpcAssignment`.

## 3. Вердикт по prior-анализу R11

**R11 в целом подтверждён.** Все пункты P0–P2, что удалось сверить с кодом построчно, — корректны:

| Утверждение R11 | Вердикт |
|---|---|
| Berthing — слепая прямая без avoidance и таймаута | ✅ Подтверждено (avoidance только Lifting/Yawing/Cruising; в Berthing нет) |
| Падовое окно всегда экспайрится для NPC (`ConfirmTouchdown` только в игроковом пути) | ✅ Подтверждено (`used=true` только там; NPC живут на `RegisterPending`+`ConfirmAssignment` с `used=false`) |
| `Consider Buildings` мёртв (0 валидных коллайдеров, non-convex фильтр) | ✅ Подтверждено (`IsClosestPointSupported` режет меши) |
| Подъём +5 м оставляет корабль в «чаше» порта | ✅ Подтверждено (`TickLift`) |
| `NpcShipTrafficManager.ScheduleNextArrival` — мёртвый код | ✅ Подтверждено (ноль вызовов) |
| `TickNpc`/`AdvanceScheduleIndex` в `NpcShipWorld` — мёртвые | ✅ Подтверждено (`FixedUpdate` → `NavTick` напрямую) |
| Мульти-маршруты сломаны (только `routes[0]`, пинг-понг) | ✅ Подтверждено (`AdvanceScheduleForCurrentNpc`) |
| Dwell до 5000 с × 20 кораблей на 21 пад = голодание падов | ✅ Подтверждено частично с уточнением (допроверка 2026-09-17, второй проход): `NpcShipSchedule_HeavyII_Default.asset` — `dwellTimeSec 60 + random 60..6000`, `maxDwellTimeSec 6000` (~100 мин на паде); остальные дефолтные расписания — 60–90 с (`Trader` до 120 с). Т.е. голодание реально, но точечное — от HeavyII-расписаний, а не поголовное |

Несогласий с R11 нет. Сомнения автора в prior-анализе по итогам ревью **не подтверждаются** —
анализ точный, чинить надо код, а не анализ.

## 4. Корневые причины наблюдаемых симптомов

1. **«Тупят» при включённом уходе от городов** — avoidance (`NpcProximityZone`) работает только
   в Cruising/Lifting/Yawing и построен как 3-фазный Separate-Stop-BackOff; у порта/города корабль
   входит в Berthing, где avoidance выключен, а до того — в зоне зданий avoidance-конфликты
   держат его в Stop/BackOff без таймаута выхода. Итог: корабль либо ползёт/стоит у города,
   либо вслепую идёт по прямой в Berthing и упирается в геометрию.
2. **Парковка** — связка «слепой Berthing + экспайрящееся падовое окно + ретрай пада каждые 3 с»:
   корабль летит к паду, бронь слетает по `landingWindowSec`, его перебрасывают на другой пад,
   он разворачивается и летит заново. Со стороны — «тупит на парковке».
3. **Отстыковка** — подъём всего +5 м не выводит из «чаши» порта/города, следующий leg сразу
   упирается в здания, которых avoidance либо не видит (non-convex фильтр), либо видит, но
   без коридора выхода корабль застревает в Stop/BackOff.
4. **Курсирование** — фактически только пинг-понг по `routes[0]`; `TrafficManager` мёртв,
   расписания многолеговых маршрутов не работают, dwell-крайности + дефицит падов дают
   голодание и пробки у популярных портов.

## 5. План улучшения курсирования (без кода, по стадиям)

- **P0 — пады:** NPC-подтверждение касания (выставить `used=true` в NPC-ветке стыковки,
  а не только в игроковом `ConfirmTouchdown`) либо отдельное NPC-окно; ретрай пада не чаще
  смены цели Berthing (сейчас 3 с дёргают цель из-под захода).
- **P0 — Berthing:** коридор захода (вертикальная «труба»/chimney над падом: снижение только
  внутри радиуса коридора), таймаут Berthing с уходом на второй круг (go-around) вместо
  вечного полёта по прямой; дистанцию стыковки 1.5 м пересмотреть после коридора.
- **P0 — отстыковка/уход от города:** фаза Departure-Chimney — набор высоты над падом до
  клиренса городской геометрии *до* перехода в Cruising (замена «+5 м и лети»);
  клиренс считать от реальных габаритов порта, а не от константы.
- **P1 — avoidance:** включить buildings-уход для Berthing-коридора в ослабленном виде
  (только lateral, без Stop), добавить таймаут Stop/BackOff с эскалацией (набор высоты);
  починить/обойти non-convex фильтр (convex-коллайдеры-приближения для городских зданий
  или отдельный слой-препятствие).
- **P1 — расписание:** починить мульти-маршруты (`AdvanceScheduleForCurrentNpc` по всему
  списку legs, а не `routes[0]`), оживить или удалить `TrafficManager`/`TickNpc`
  (сейчас мёртвый код вводит в заблуждение), ограничить dwell и развести флот по падам.
- **P2 — чистка:** удалить мёртвые `ScheduleNextArrival`, `TickNpc`, `AdvanceScheduleIndex`,
  константы-заглушки; тикеты вида `R2-003`/`T-NS*` в комментариях сохранить (трекер проекта).

## 6. Риски и ограничения

- NPC-корабли — scene-placed контент: любые новые мировые `Vector3`-состояния между кадрами
  (коридоры, вейпоинты chimney, пороги клиренса) потребуют хука сдвига floating origin
  (`ApplyRebaseTranslation`, паттерн `StormCellDirector`/`WindManager`); иначе F8 уронит
  заход/выход. Проверка: F8 → `runtimeRebase.Completed` + 0 errors, F9 → возврат.
- Не трогать `NetworkManager`/`ClientSceneLoader`/`ScenePlacedObjectSpawner` (см. AGENTS.md);
  NPC-логика — вне сетевого спавна, держать её там же.
- Сначала P0 падов+коридор (минимальный дифф, максимальный эффект на «тупняк»),
  затем Departure-Chimney, затем расписание; avoidance-города — только после коридоров,
  иначе «включение ухода» снова всё заклинит.

## 7. Как проверять (когда дойдёт до кода)

Compile: Console → 0 errors. Manual: NPC кругосветка M3.2.15-сценарий —
отстыковка, уход от города, заход, парковка, повтор; F8/F9 rebase без срыва захода;
0 «вечных» Berthing (считать go-around в лог). Тесты: Test Runner после добавления
`.asmdef` (отдельная задача, руками `.asmdef` не писать).

## 8. Второй проход (допроверка того же дня, только чтение кода)

Повторное ревью по просьбе автора («сомневаюсь в прошлом анализе»).
Вывод не изменился: R11 и §§3–4 выше подтверждаются. Что допроверено построчно:

- `TickLift` — `targetY = LiftStartY + 5f` (`NpcShipController.cs:569`) — хардкод на месте,
  план R11 «вертикальный коридор» не внедрён.
- `TickBerth` (`:647–692`) — по-прежнему без avoidance, без watchdog/таймаута,
  без детекции отсутствия прогресса; ожидание пада — `velocity = 0` на месте (`:663–664`).
- `ConfirmTouchdown` вызывается только из `DockingServer.cs:266` (игроковый RPC-путь);
  NPC-ветка его не вызывает — падовое окно `landingWindowSec` (дефолт 90 с,
  `DockStationDefinition.cs:27`) для NPC истекает всегда.
- Avoidance строго горизонтальный: `away.y = 0` (`:936–937`), escape-веер —
  только горизонтальные лучи (`ComputeEscapeDir`, `:860–880`). Вертикальный выход
  из «чаши» порта манёвром не рассматривается.
- `IsAvoidable` (`NpcProximityZone.cs:247–255`) исключает `Docked`/`Berthing`:
  зависший в ожидании пада корабль в `Berthing` для крейсерских невидим —
  в него влетают. В `Lifting`/`Yawing` ship-to-ship уже виден (прогресс после R11),
  но толку мало без коридора.
- После коммита R11 (`4da179be`) план R11 §§5–7 не внедрён: в git-истории
  `NpcShipController.cs` после ресёрча — только crew/cargo/docs-коммиты
  (`T-CREW-*`, `T-CARGO-NPC-01`, `T-DOCK15`, `T-NPCEDIT02`), коридоров/watchdog/
  holding-точек/divert нет.
- Floating origin: во всём `PeacefulShip` нет `ApplyRebaseTranslation`
  (grep — 0 совпадений); `CruiseTargetPos`/`LiftStartY`/`_avoidFromPos` — мировые
  `Vector3` без хука сдвига (🟡-категория по AGENTS.md). Есть только гейт
  `CanSimulateInCurrentCoordinates` (`:428`). При F8 цель круиза протухает —
  это отдельный от «тупняка в доки» фактор, чинить вместе с коридорами (§6).
- Код не менялся, только этот документ.

## 9. Инвентаризация «мёртвого» кода (проверка по вызовам, 2026-09-17)

Вопрос: что из подозреваемого — мёртвое, что — резерв, что — живое.
Метод: grep вызовов по `Assets/` (определение vs вызовы).

| # | Элемент | Вызовы | Вердикт |
|---|---------|--------|---------|
| 1 | `NpcShipWorld.TickNpc` + `ApplyDeparting/Transit/ApproachMovement` + `CalcBearing` + `NPC_*` константы + `_lastPadAttempt` + `_lastArrivalAtStation` (world) | Внешнего вызова нет: `FixedUpdate` идёт в `controller.NavTick` напрямую; вызовы только внутри себя | **МЁРТВ** — кандидат на снос (P2) |
| 2 | `NpcShipWorld.AdvanceScheduleIndex` / `TryAssignPadForNpc(state)` / `ReleaseNpcAssignment(state)` / `ResolveStationWorldPos` / `TransitionTo` | Только из `TickNpc` (не путать с живым `DockingWorld.ReleaseNpcAssignment(ids)`) | **МЁРТВ** — но логика `Advance` уже портирована в контроллер (T-NS-ROUTES5); сносить вместе с п.1 |
| 3 | `NpcShipTrafficManager.ScheduleNextArrival` / `Clear` / словарь | Вызовов ноль; lifecycle (`Create/Shutdown` из `NpcShipServer`) живой | Мёртвая фича в живом шелле — **РЕЗЕРВ** (v2 shaping), не сносить |
| 4 | `ApplyMovementInput` / `ServerTeleport` / `StartAntiGravityBoost` | Вызовов ноль (были только из `TickNpc`); boost фактически не нужен — `ShipController:611` скипает всю физику при `_hasNpcPilot` | **РЕЗЕРВ**: публичный API (Q1-хук автопилота), не сносить; поправить вводящие в заблуждение комменты |
| 5 | `NpcShipStatus` enum + DTO + `NpcShipClientState.HandleNpcSpawn/Status` | `Handle*` никто не зовёт (RPC не слались никогда); `ClientState` читает только `AdminRuntimeWindow` | Каркас проекции, не запитан — **РЕЗЕРВ**; побочка: UI вечно покажет `Idle` (статус всегда начальный) |
| 6 | `NpcShipState.Status` / `StateEnteredAt` / `LastKnownPosition` | Пишут ctor/`RestoreNpcState`/мёртвый `TransitionTo`; читает только мёртвый код | Вестигиальные поля — **оставить** (мелочь, persist-смежно, риск > пользы) |
| 7 | События `OnNpcShipArrived/Departed` | Объявлены, `pragma 0067`, нет вызовов и подписчиков | **РЕЗЕРВ** v2 — оставить |
| 8 | Реестр `NpcShipWorld` (`Register/Unregister/Get/GetSchedule/AllNpcCount/RestoreNpcState`) + `NpcShipServer` + `NpcShipZoneRegistry` | Живые вызовы из контроллера/сервера/`ShipPositionServer` | **ЖИВОЕ** — не трогать |
| 9 | `AllNpcs` | Внешних читателей нет | Публичное, для дебага — оставить |

**Вывод для P2-чистки:** сносить ТОЛЬКО unreachable private-блок `NpcShipWorld`
(`TickNpc`, 3×`Apply*`, `CalcBearing`, мёртвый `AdvanceScheduleIndex`,
мёртвые `TryAssign/Release(state)/Resolve/TransitionTo`, `_lastPadAttempt`,
world-`_lastArrivalAtStation`, `NPC_*` константы) + поправить 3 вводящих в заблуждение
коммента (`NpcShipController:12`, `:256`, `NpcShipTrafficManager:40` — ссылаются на
`TickNpc`, которого нет в пути выполнения). Всё остальное — резерв или живое.
Снос behavior-neutral (private/недостижимо), после — `refresh_unity` + 0 errors.
