# Именная команда NPC на движущемся корабле — Этап 1: identity и named prefab

**Дата:** 2026-09-08
**Тикет:** `T-CREW-07`
**Статус:** завершён; runtime Play Mode не запускался.
**Предыдущий этап:** `03_IMPLEMENTATION_STAGE_00_MVP_CONTRACT_2026-09-08.md`

## Цель этапа

Создать отдельные identity asset и named prefab для пилота «Горгоны», не изменяя существующую identity Mira и не меняя базовый `_platformMask`.

## Созданные ассеты

- `Assets/_Project/Quests/Data/Npcs/GorgonaPilot.asset`
- `Assets/_Project/Prefabs/NPC/Ships/Gorgona_Pilot.prefab`

## Identity-конфигурация

`GorgonaPilot.asset` имеет тип `ProjectC.Quests.NpcDefinition` и подтверждённые значения:

- `npcId = gorgona_pilot_01`;
- `displayName = dialogue.npc.gorgona_pilot_01.displayName`;
- `animatorTriggerPrefix = GorgonaPilot`;
- `faction = Neutral` без новой faction-specific привязки;
- `defaultDialogTree = null`;
- `showGreeting = false`;
- `prefab = Gorgona_Pilot`.

Отсутствие dialog tree, portrait и локализационных записей на этом этапе намеренное: они не являются частью identity/prefab acceptance и будут добавляться отдельным этапом только при наличии сценарного контента.

## Prefab-конфигурация

`Gorgona_Pilot.prefab` создан как prefab variant от проверенного базового prefab:

`Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab`

Подтверждённые свойства variant:

- `prefabType = Variant`;
- `parentPrefab` указывает на baseline named NPC prefab;
- root name: `Gorgona_Pilot`;
- root содержит `NpcController`, `NetworkObject`, `NavMeshAgent`, `NpcBrain`, `NpcTarget`, `NpcAttacker`, `NpcSocialBrain`, `NetworkTransform` и `CharacterController`;
- `NpcController.definition` назначен на `GorgonaPilot.asset`;
- dependency graph prefab содержит `GorgonaPilot.asset`.

## Network registration

`Assets/DefaultNetworkPrefabs.asset` содержит `Assets/_Project/Prefabs/NPC/Ships/Gorgona_Pilot.prefab` в dependency list. Таким образом, variant уже присутствует в текущем NetworkPrefabsList и отдельная ручная регистрация на этом этапе не потребовалась.
`Assets/_Project/Quests/Data/QuestDatabase.asset` автоматически получил ссылку на новый `GorgonaPilot.asset`; это сохраняет его в общем реестре `npcs` проекта.

## Что не изменялось

- `Mira.asset` не изменялся;
- baseline prefab `[Mira] - DON\`T DELETE DEFOULT.prefab` не изменялся;
- `Горгона.prefab` не изменялся;
- `NpcSpawner`, `NpcShipController`, `ShipDeckNav` и `PilotSeatController` не изменялись;
- `_platformMask` не изменялся;
- `NpcSpawner_ship_deck.asset` не изменялся.

## Проверки

- `manage_prefabs.get_info` подтвердил prefab variant, parent prefab и полный обязательный component stack;
- `get_asset_meta` подтвердил identity properties и prefab dependency;
- `get_asset_meta` подтвердил наличие нового prefab в `DefaultNetworkPrefabs.asset` dependency list;
- diff `QuestDatabase.asset` подтвердил добавление нового NPC в общий список `npcs`;
- `check_compile_errors` вернул `No compile errors`.

Play Mode, spawn, NGO replication, NPC movement, seat occupancy и локализация пока не проверялись — это следующие этапы.

## Acceptance

Этап закрыт: создан отдельный `NpcDefinition`, создан named prefab variant, prefab связан с identity, component stack сохранён, network registration подтверждён, compile errors отсутствуют.

## Следующий этап

**Этап 2 — `ShipCrewManifest`:** создать ship-specific manifest, описать entry `gorgona_pilot_01`, role `Pilot`, required flag и respawn policy без внесения ship assignment в `NpcDefinition`.
