# Именная команда NPC на движущемся корабле — Этап 0: MVP-контракт

**Дата:** 2026-09-08  
**Тикет:** `T-CREW-06`  
**Статус:** завершён; код и Unity-ассеты на этом этапе не изменялись.  
**Источник плана:** `02_SHIP_CREW_RESEARCH_REVIEW_AND_PLAN_2026-09-08.md`

## Цель этапа

Зафиксировать минимальный контракт для первого runtime-сценария: корабль «Горгона» перевозит одного конкретного именного пилота, а не случайного NPC из общего спавнера.

## Принятые решения

1. **Целевой корабль:** `Assets/_Project/Prefabs/Ships/Горгона.prefab`.
2. **Стабильный identity ID пилота:** `gorgona_pilot_01`.
3. **Identity asset:** отдельный `NpcDefinition` для пилота; существующий `Mira.asset` не переиспользуется как identity пилота.
4. **Prefab пилота:** отдельный named prefab/variant на базе проверенного named NPC prefab:
   `Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab`.
   Создание и заполнение ссылки `NpcDefinition.prefab` выполняется на Этапе 1.
5. **Единственная MVP-активность:** `Patrol` по ship-local точкам. Остальные social activities подключаются после подтверждения базового deck movement.
6. **Управление кораблём:** остаётся за `NpcShipController`. NPC не добавляется в `ShipController._pilots` и не моделируется как fake `NetworkPlayer`.
7. **Посадка в кресло:** occupant пилота будет отдельным состоянием `PilotSeatController`, не источником control authority корабля.
8. **Смерть пилота:** в MVP погибший fixed crew не заменяется случайным NPC и не создаёт generic replacement. Автоматический respawn в рамках текущего runtime не вводится; политика восстановления при загрузке сцены будет проверяться на Этапе 9.
9. **Generic spawn:** `NpcSpawner` на «Горгоне» считается конфликтующим и должен быть выключен/переведён в явный disabled mode до подключения fixed crew. Изменение prefab выполняется на Этапе 8, чтобы не скрыть текущий baseline до готовности нового пути.
10. **Идемпотентность:** повторный discovery/spawn не должен создавать второго `gorgona_pilot_01`; это обязательное требование для `ShipCrewSpawner`.
11. **Attachment:** crew attachment будет явным через ship-local систему и `NpcBrain`, а не через `_platformMask` или случайный physics probe.

## Границы этапа

На этом этапе намеренно не изменялись:

- C#-скрипты;
- prefab и ScriptableObject-ассеты;
- NetworkConfig и `DefaultNetworkPrefabs.asset`;
- `NpcSpawner_ship_deck.asset`;
- сцены и runtime-расстановка.

Причина: identity prefab, manifest и ship-local anchors должны вводиться последовательно, чтобы не получить промежуточный generic/fixed spawn конфликт.

## Acceptance criteria этапа

Этап считается закрытым, потому что зафиксированы:

- точный корабль и стабильный `npcId`;
- разделение identity, ship assignment, seat occupancy и autopilot;
- политика смерти без случайной замены;
- обязательное отключение generic spawn на целевом корабле;
- единственная activity для MVP — `Patrol`;
- следующий технический шаг — Этап 1: отдельные `NpcDefinition` и named prefab.

## Подтверждённая база проекта

Статический аудит подтвердил:

- «Горгона» содержит `NpcShipController`, `NpcSpawner` и `ShipDeckNav`;
- текущий generic `NpcSpawner` использует конфигурацию `NpcSpawner_ship_deck.asset` и не знает fixed roster;
- проверенный named NPC prefab содержит `NpcController`, `NetworkObject`, `NavMeshAgent`, `NpcBrain`, `NpcSocialBrain`, `NpcTarget`, `NpcAttacker`, `NetworkTransform` и `CharacterController`;
- `Mira.asset` имеет `npcId=mira_01` и `prefab=null`, поэтому его нельзя считать готовой identity-связкой для нового пилота;
- текущий `NpcShipController` уже является источником движения NPC-корабля.

## Следующий этап

**Этап 1 — identity и prefab:** создать отдельный `NpcDefinition` пилота `gorgona_pilot_01`, named prefab/variant, заполнить canonical prefab reference и проверить network registration без изменения baseline `_platformMask`.
