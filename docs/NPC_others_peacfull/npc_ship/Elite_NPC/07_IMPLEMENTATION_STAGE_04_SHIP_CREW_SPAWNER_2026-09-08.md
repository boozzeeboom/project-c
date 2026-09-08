# Именная команда NPC на движущемся корабле — Этап 4: ShipCrewSpawner

**Дата:** 2026-09-08
**Тикет:** `T-CREW-10`
**Статус:** завершён; runtime spawn и Play Mode не запускались.
**Предыдущий этап:** `06_IMPLEMENTATION_STAGE_03_SHIP_LOCAL_ANCHORS_2026-09-08.md`

## Цель этапа

Подключить fixed crew manifest к lifecycle корабля «Горгона» и реализовать server-authoritative spawn точного named prefab с NGO parenting и защитой от дублей.

## Реализация

Создан компонент:

`Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs`

Компонент:

- наследуется от `NetworkBehaviour`;
- работает только на сервере;
- читает `ShipCrewManifest`;
- запускает spawn после `OnNetworkSpawn` корабля через deferred coroutine;
- разрешает `CrewAnchors` под ShipRoot;
- сопоставляет manifest anchor IDs с child names через direct и normalized lookup;
- создаёт exact prefab из `ShipCrewMemberEntry.prefab`;
- вызывает `NetworkObject.Spawn(destroyWithScene: true)`;
- делает explicit `NetworkObject.TrySetParent` к root «Горгоны»;
- хранит runtime mapping `memberId -> NetworkObject`;
- повторно использует уже существующего spawned NPC с тем же `npcId`, если он уже является прямым child корабля;
- очищает stale mappings после despawn;
- проверяет definition/prefab mismatch и отсутствие spawn anchor до Instantiate.

## Подключение к кораблю

`Assets/_Project/Prefabs/Ships/Горгона.prefab` получил компонент:

`ProjectC.PeacefulShip.Crew.ShipCrewSpawner`

Назначены ссылки:

- manifest: `Assets/_Project/Resources/PeacefulShip/ShipCrewManifest_Gorgona.asset`;
- crew anchors: `Горгона/CrewAnchors`;
- `spawnOnNetworkSpawn = true`;
- `debugLogs = true` для первого runtime-прогона.

Dependency metadata prefab подтверждает ссылки на `ShipCrewSpawner.cs` и `ShipCrewManifest_Gorgona.asset`.

## Lifecycle и границы

- Spawn выполняется server-side и не зависит от player activation radius.
- Generic `NpcSpawner` на «Горгоне» пока не выключен: это отдельный Этап 8, поэтому до его прохождения runtime может дополнительно создать generic NPC. Новый fixed crew path сам generic spawn не вызывает.
- `NpcBrain` пока не получил официальный `AttachToShipDeck()` API; на этом этапе parenting выполняется непосредственно spawner'ом. Выделение attachment API — Этап 5.
- Pilot seat occupancy пока не подключён; это Этап 7.

## Проверки

- `validate_script` для `ShipCrewSpawner.cs`: diagnostics отсутствуют;
- `check_compile_errors`: `No compile errors`;
- `get_asset_meta` подтвердил наличие `ShipCrewSpawner` и зависимости на manifest/script в «Горгоне»;
- `manage_prefabs.get_info` подтвердил component stack корня корабля;
- `CrewAnchors` hierarchy и anchor IDs уже подтверждены на предыдущем этапе.

Play Mode, NGO replication, actual spawn timing, movement и duplicate behavior ещё не проверялись.

## Acceptance

Этап закрыт: server-only fixed crew spawner создан и подключён к «Горгоне», manifest и anchors назначены, exact prefab spawn и idempotent mapping реализованы без изменения `NpcDefinition` и `NpcShipController`.

## Следующий этап

**Этап 5 — explicit moving-ship attachment:** добавить официальный API в `NpcBrain` для attach/detach к `ShipDeckNav`, parent state и deck navigation readiness без зависимости от `_platformMask`.
