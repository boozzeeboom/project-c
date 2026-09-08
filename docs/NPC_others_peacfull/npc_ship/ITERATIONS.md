# ITERATIONS — Peaceful NPC Ships (runtime fixes)

## Итерация от 2026-09-08 — T-CREW-10: ShipCrewSpawner для «Горгоны»

**Задача:** подключить fixed crew manifest к server-side lifecycle корабля и реализовать exact prefab spawn с NGO parent и duplicate guard.

**Статус:** Этап 4 завершён; runtime spawn и Play Mode не запускались.

**Изменения:**
- `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs` — новый server-only spawner, runtime mapping и idempotent reuse.
- `Assets/_Project/Prefabs/Ships/Горгона.prefab` — добавлен и настроен `ShipCrewSpawner`.
- `docs/NPC_others_peacfull/npc_ship/Elite_NPC/07_IMPLEMENTATION_STAGE_04_SHIP_CREW_SPAWNER_2026-09-08.md` — отчёт этапа.

**Проверка:** `validate_script` без diagnostics; `check_compile_errors` — `No compile errors`; prefab dependencies подтверждают manifest и spawner.

**Следующий этап:** Этап 5 — explicit moving-ship attachment API в `NpcBrain`.

**Коммит:** будет добавлен после создания коммита этапа.

---

## Итерация от 2026-09-08 — T-CREW-09: ship-local anchors для fixed crew «Горгоны»

**Задача:** добавить ShipRoot-local anchors для deterministic spawn, pilot seat stand и MVP patrol.

**Статус:** Этап 3 завершён; runtime spawn и Play Mode не запускались.

**Изменения:**
- `Assets/_Project/Prefabs/Ships/Горгона.prefab` — добавлены `CrewAnchors`, `PilotSpawn`, `PilotSeatStand`, `Patrol_01`, `Patrol_02`, `Patrol_03`.
- `docs/NPC_others_peacfull/npc_ship/Elite_NPC/06_IMPLEMENTATION_STAGE_03_SHIP_LOCAL_ANCHORS_2026-09-08.md` — отчёт этапа.

**Проверка:** prefab hierarchy и editor verification подтвердили parent/position/rotation/scale всех пяти anchors.

**Следующий этап:** Этап 4 — `ShipCrewSpawner`.

**Коммит:** `86b93dad` — T-CREW-09: add Gorgona crew anchors.

---

## Итерация от 2026-09-08 — T-CREW-08: ShipCrewManifest для «Горгоны»

**Задача:** описать fixed named crew отдельным ship-specific asset и добавить editor validation до подключения runtime spawner.

**Статус:** Этап 2 завершён; runtime spawn и Play Mode не запускались.

**Изменения:**
- `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewManifest.cs` — новый manifest type, entry, roles, respawn policies и validation.
- `Assets/_Project/Resources/PeacefulShip/ShipCrewManifest_Gorgona.asset` — manifest с одним required pilot `gorgona_pilot_01`.
- `docs/NPC_others_peacfull/npc_ship/Elite_NPC/05_IMPLEMENTATION_STAGE_02_CREW_MANIFEST_2026-09-08.md` — отчёт этапа.

**Проверка:** `validate_script` без diagnostics; `check_compile_errors` — `No compile errors`; manifest references подтверждены через editor metadata.

**Следующий этап:** Этап 3 — ship-local anchors.

**Коммит:** `e1da0115` — T-CREW-08: add Gorgona crew manifest.

---

## Итерация от 2026-09-08 — T-CREW-07: identity и named prefab пилота «Горгоны»

**Задача:** создать отдельные identity asset и NetworkObject prefab variant для fixed pilot `gorgona_pilot_01`.

**Статус:** Этап 1 завершён; runtime Play Mode не запускался.

**Изменения:**
- `Assets/_Project/Quests/Data/Npcs/GorgonaPilot.asset` — новый `NpcDefinition` с `npcId=gorgona_pilot_01` и canonical prefab reference.
- `Assets/_Project/Prefabs/NPC/Ships/Gorgona_Pilot.prefab` — новый variant базового named NPC prefab; `NpcController.definition` назначен на `GorgonaPilot.asset`.
- `Assets/DefaultNetworkPrefabs.asset` — новый prefab подтверждён в dependency list.
- `Assets/_Project/Quests/Data/QuestDatabase.asset` — новый `GorgonaPilot.asset` добавлен в список `npcs`.
- `docs/NPC_others_peacfull/npc_ship/Elite_NPC/04_IMPLEMENTATION_STAGE_01_IDENTITY_AND_PREFAB_2026-09-08.md` — отчёт этапа.

**Проверка:** `check_compile_errors` — `No compile errors`; component stack и prefab parent подтверждены через editor asset metadata.

**Следующий этап:** Этап 2 — `ShipCrewManifest`.

**Коммит:** `4db549af` — T-CREW-07: create Gorgona pilot identity and prefab.

---

## Итерация от 2026-09-08 — T-CREW-06: MVP-контракт именной команды «Горгоны»

**Задача:** перейти от ресерча к последовательной реализации fixed named crew для движущегося корабля «Горгона».

**Статус:** Этап 0 завершён; код и Unity-ассеты не изменялись.

**Документация:**
- `docs/NPC_others_peacfull/npc_ship/Elite_NPC/03_IMPLEMENTATION_STAGE_00_MVP_CONTRACT_2026-09-08.md`
- `docs/NPC_others_peacfull/npc_ship/Elite_NPC/02_SHIP_CREW_RESEARCH_REVIEW_AND_PLAN_2026-09-08.md`

**Зафиксировано:**
- стабильный pilot ID: `gorgona_pilot_01`;
- MVP activity: `Patrol`;
- `NpcShipController` остаётся источником движения корабля;
- NPC не является fake `NetworkPlayer` и не попадает в `ShipController._pilots`;
- pilot seat будет отдельным NPC occupant state;
- fixed crew не заменяется случайным NPC после смерти;
- generic `NpcSpawner` на «Горгоне» должен быть отключён после подключения fixed crew;
- attachment должен быть explicit и не зависеть только от `_platformMask`.

**Следующий этап:** Этап 1 — identity и named prefab.

**Коммит:** `142f0362` — T-CREW-06: define named crew MVP contract.

---

## Итерация от 2026-07-?? — Ресерч: «корабли тупят в доках» (только документация)

**Задача:** глубокий ресерч кода, префабов и сцены — почему NPC-корабли застревают
в доках при заходе/выходе; как улучшить манёвры между кораблями и расход.

**Коммит:** `4da179be1111c6b9d3e99c9fcde89c80bc1f5a08` — T-NS-RESEARCH: ресерч «корабли тупят в доках» — документация

**Изменения:**
- `docs/NPC_others_peacfull/npc_ship/11_DOCK_NAV_RESEARCH.md` — новый документ
  с полным разбором (P0/P1/P2 дефекты, план «вертикальный коридор», тикеты)

**Ключевые выводы:**
- `Berthing` — слепая прямая к паду без avoidance/таймаута → вечное упирание в геометрию
- Окно посадки 90 с истекает для NPC всегда (`ConfirmTouchdown` не вызывается из NPC-пути)
- `Consider Buildings` не работает: 1 `NpcProximityZoneBuilds` во всём проекте с 0 валидных коллайдеров
- Dwell до 5000 с × 20 кораблей → пад-голодание
- Решение: «вертикальный коридор» (подъём выше палубы + вертикальный спуск на пад)

---

## Итерация от 2026-07-24 — T-DOCK15: фикс спама Pad Occupied в FixedUpdate (NpcShipController)

**Задача:** NPC-корабли в режиме Berthing спамили `TryAssignPadFromDispatcher()` каждый FixedUpdate.

**Коммит:** `8d391a9d3c6616c1215f59b98e56d2cdd8876a06` — T-DOCK15

**Изменения:**
- `NpcShipController.cs` — добавлен `_lastPadAssignAttemptTime` + `PAD_ASSIGN_RETRY_SEC = 3f` в `TickBerth`
- `DockingWorld.cs` — авто-регистрация occupancy в `_occupiedPads` (см. `docs/Docking_stations/ITERATIONS.md`)

**Эффект:** вместо 50 вызовов/сек → не чаще раза в 3 секунды на NPC.

---

## Итерация от 2026-07-?? — M3.2.N: Class-based speed variation

**Задача:** Ввести разнообразие скоростей NPC-кораблей относительно класса (ShipFlightClass). Все корабли летят с одинаковой скоростью.
**Коммит:** `613b763` — T-NS-N01: Class-based speed variation для NavTick (NPC-корабли)
**Изменения:**
- `PeacefulShip/Stations/NpcShipController.cs`: +`GetClassBaseSpeeds()` static lookup, +4 serialized multiplier поля, +`ResolveClassSpeeds()`, старые public поля → computed properties
- `PeacefulShip/Editor/NpcShipControllerEditor.cs`: Movement foldout показывает класс/базу/множители/эффективные скорости
- `docs/NPC_others_peacfull/npc_ship/CHANGELOG.md`: запись итерации

---

## Итерация от 2026-07-17

**Задача:** NPC не спавнятся на палубе, игрок проваливается сквозь платформу при включённом NpcShipController.
**Коммит:** `65c3293` — T-NS11: fix detectCollisions=false ломал коллайдер платформы NPC-корабля
**Изменения:**
- `NpcShipController.cs` — убран `detectCollisions=false` в SetMode(Lifting), гарантия `true` в OnNetworkSpawn
- `NpcSpawner.cs` — отладочные логи в TickSpawn/TryFindSpawnPoint
- `NpcSpawner_ship_deck.asset` — новый конфиг спавнера для палубы
- `NpcSpawner_neutral.asset` — новый конфиг
- `Ship_Medium.prefab` — префаб корабля с платформой, NpcShipController, NpcSpawner
- `Npc_Goblin 2.prefab` — префаб NPC для тестов
- `10_COLLIDER_BUG_detectCollisions_false.md` — документ с разбором бага
- `CHANGELOG.md` — запись в логе

---

## Итерация от 2026-08-24 — TradeItem asset reference в NPC Ship Buy Items

**Задача:** убрать необходимость вводить строковый Item ID вручную в `Cargo Trade > Buy Items` у `NpcShipSchedule` и разрешить назначение товара перетаскиванием `TradeItemDefinition`-ассета.

**Коммит:** не создан автоматически — в текущей Unity-сессии доступного Git-инструмента нет.

**Изменения:**
- `Assets/_Project/Scripts/PeacefulShip/Core/NpcCargoTradeConfig.cs` — добавлено поле `tradeItem` типа `TradeItemDefinition`; старый `itemId` сохранён как legacy fallback.
- `Assets/_Project/Scripts/PeacefulShip/Editor/NpcCargoTradeConfigDrawer.cs` — в инспекторе `Buy Items` теперь доступно поле `Trade Item` для drag-and-drop, а resolved `Item ID` заполняется автоматически.
- `Assets/_Project/Scripts/PeacefulShip/Network/NpcCargoService.cs` — runtime использует `tradeItem.itemId`, сохраняя совместимость со старыми строковыми конфигурациями.
- `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipSchedule.cs` — валидация учитывает asset reference.
- `Assets/_Project/Editor/Tools/NpcShipScheduleOverviewWindow.cs` — таблица `Cargo Trade` также принимает `TradeItemDefinition`-ассеты.

**Проверка:** `check_compile_errors` — compile errors отсутствуют.

---

## Итерация от 2026-08-24 (T-CARGO-NPC-01) — Cargo limits from assigned ship

**Задача:** убрать max load slots/weight из `NpcCargoTradeListConfig` и брать capacity конкретного корабля, на который назначен schedule.

**Изменения:**
- `NpcCargoTradeConfig.cs` — удалены редактируемые `maxLoadSlots` и `maxLoadWeightKg`; в tooltip зафиксирован источник лимитов — назначенный корабль.
- `NpcCargoService.cs` — обычный и random trade используют `ShipCargoRegistry.GetEffectiveLimits(shipNetworkObjectId)` с учётом базовых лимитов и cargo-модулей; fallback по `ShipClassLimits` остаётся только для старта до регистрации корабля. Учитывается также фактический объём трюма.
- `NpcShipSchedule.cs` — убраны preset-присваивания и валидация лимитов маршрута.
- `NpcShipScheduleOverviewWindow.cs` — убраны поля Max Slots/Max Weight, добавлена подсказка о per-ship capacity.
- `NpcShipSchedule_*.asset` — удалены устаревшие сериализованные значения лимитов из schedule-ассетов.
- `T_CARGO_NPC_01_DESIGN_2026-07-03.md` и `IMPLEMENTATION_2026-07-03.md` — обновлено архитектурное описание источника capacity.

**Проверка:** `check_compile_errors` — No compile errors.

---

## Итерация от 2026-08-24 (T-CARGO-NPC-01) — Random NPC cargo trade mode

**Задача:** добавить в `NpcShipSchedule` режим, в котором NPC одной галочкой продаёт весь cargo на станции и покупает случайные доступные товары до лимитов загрузки.

**Коммит:** `c4948836` (`T-CARGO-NPC-01: add random NPC cargo trade mode`)

**Изменения:**
- `NpcCargoTradeConfig.cs` — добавлен флаг `randomTradeItems`; при `false` сохраняется режим покупки через `buyItems`.
- `NpcCargoService.cs` — random mode выбирает товары из текущего `MarketState`, фильтрует buyable/stocked позиции и покупает их максимально возможными партиями до лимитов назначенного корабля.
- `NpcShipScheduleOverviewWindow.cs` — добавлен переключатель `Random trade (buy to full)`; список `Buy Items` явно помечается как игнорируемый в random mode.
- `NpcShipController.cs` — режим добавлен в диагностический лог DwellTrade.

**Проверка:** `check_compile_errors` — compile errors отсутствуют.
