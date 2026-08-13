# ITERATIONS — Peaceful NPC Ships (runtime fixes)

## Итерация от 2026-07-?? — Ресерч: «корабли тупят в доках» (только документация)

**Задача:** глубокий ресерч кода, префабов и сцены — почему NPC-корабли застревают
в доках при заходе/выходе; как улучшить манёвры между кораблями и расход.

**Коммит:** см. ниже (фиксируется после commit)

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
