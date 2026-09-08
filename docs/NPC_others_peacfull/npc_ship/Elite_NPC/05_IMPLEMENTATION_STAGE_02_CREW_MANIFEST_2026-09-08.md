# Именная команда NPC на движущемся корабле — Этап 2: ShipCrewManifest

**Дата:** 2026-09-08
**Тикет:** `T-CREW-08`
**Статус:** завершён; runtime spawn и Play Mode не запускались.
**Предыдущий этап:** `04_IMPLEMENTATION_STAGE_01_IDENTITY_AND_PREFAB_2026-09-08.md`

## Цель этапа

Вынести состав экипажа в отдельный ship-specific ScriptableObject, не добавляя ship assignment в `NpcDefinition`.

## Реализация

Создан runtime-тип:

`Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewManifest.cs`

В типе определены:

- `ShipCrewRole` — `Pilot`, `Captain`, `Engineer`, `Gunner`, `Navigator`, `Passenger`;
- `ShipCrewRespawnPolicy` — `Never`, `OnShipSpawn`, `AfterDelay`;
- `ShipCrewMemberEntry` — identity, exact prefab, role, required flag, respawn policy, spawn/seat/activity anchor IDs;
- `ShipCrewManifest` — stable `shipId`, fixed `members[]`, lookup by `memberId` и role.

`ShipCrewManifest.OnValidate()` проверяет:

- null entries;
- пустые и duplicate `memberId`;
- duplicate `npcId`;
- отсутствующие required `NpcDefinition` и prefab;
- наличие `NpcController` на prefab;
- соответствие `NpcController.Definition` и entry `npcDefinition`;
- повторное использование одной роли.

## Созданный asset

`Assets/_Project/Resources/PeacefulShip/ShipCrewManifest_Gorgona.asset`

Подтверждённая конфигурация:

- `shipId = gorgona`;
- один member entry;
- `memberId = gorgona_pilot_01`;
- `npcDefinition = GorgonaPilot.asset`;
- `prefab = Gorgona_Pilot.prefab`;
- `role = Pilot`;
- `required = true`;
- `respawnPolicy = Never`;
- `spawnAnchorId = pilot_spawn`;
- `seatAnchorId = pilot_seat_stand`;
- `activityAnchorIds = patrol_01, patrol_02, patrol_03`.

Anchor IDs зафиксированы как стабильные строки до создания реальных ShipRoot-local transforms на Этапе 3.

## Важное ограничение

Manifest пока является только data asset. Он ещё не подключён к `Горгона.prefab`, `ShipCrewSpawner` ещё не существует, generic `NpcSpawner` не отключён, а runtime mapping и NGO parenting не выполняются. Это намеренно: сначала создаются данные и validation, затем ship-local anchors и spawn lifecycle.

## Проверки

- `validate_script` для `ShipCrewManifest.cs`: diagnostics отсутствуют;
- `check_compile_errors`: `No compile errors`;
- `manage_scriptable_object` dry-run подтвердил наличие всех scalar/reference/array paths entry;
- `get_asset_meta` подтвердил тип `ShipCrewManifest` и зависимости на manifest script, `GorgonaPilot.asset` и `Gorgona_Pilot.prefab`;
- временный editor setup script был удалён после заполнения asset.

Play Mode, spawn, duplicate prevention, seat occupancy и movement пока не проверялись.

## Acceptance

Этап закрыт: manifest создаётся отдельным типом, содержит exact pilot entry и policy, не расширяет `NpcDefinition`, имеет editor-time validation и готов для подключения к кораблю на следующих этапах.

## Следующий этап

**Этап 3 — ship-local anchors:** добавить на `Горгону` дочерний `CrewAnchors`, `PilotSpawn`, `PilotSeatStand` и три patrol точки с устойчивыми local transforms.
