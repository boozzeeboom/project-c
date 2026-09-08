# Именные капитаны для остальных NPC-кораблей — этап массовой настройки

**Дата:** 2026-09-08  
**Тикет:** T-CREW-13

## Цель

Подключить fixed named captain crew к каждому ship prefab в `Assets/_Project/Prefabs/Ships`, где уже присутствует `ProjectC.AI.NpcSpawner`, кроме уже настроенной «Горгоны». Два player-only light ship без `NpcSpawner` не изменялись.

## Объём

Обработано 20 кораблей:

- `DONT-DELETE-NPC REFERENCE SHIP`
- «Альбатрос», «Берег», «Вавилон», «Ветроворот», «Гигант», «Жук»;
- «Летучий», «Лорейн», «Мастодонт», «Олимп», «Пещера», «Река»;
- «Сильфида», «Скат», «Странник», «Торренс», «Угольщик», «Цитадель», «Шмель».

Не изменялись:

- `Горгона` — fixed crew уже существовал;
- `Ship_Light_root (копия с компьютера DESKTOP-K00O7HK)` — нет `NpcSpawner`;
- `Ship_Light_root_fbx_test` — нет `NpcSpawner`.

## Изменения

Для каждого из 20 кораблей созданы:

- отдельный `NpcDefinition` в `Assets/_Project/Quests/Data/Npcs/Ships/`;
- отдельный named NPC prefab в `Assets/_Project/Prefabs/NPC/Ships/Captains/`, скопированный из полного `Gorgona_Pilot` component stack;
- отдельный `ShipCrewManifest_<slug>.asset` в `Assets/_Project/Resources/PeacefulShip/`;
- стабильные `npcId` и `memberId` вида `<slug>_captain_01`;
- entry с `role=Captain`, `required=true`, `respawnPolicy=Never`;
- ShipRoot-local `CrewAnchors` с `CaptainSpawn`, `CaptainSeatStand`, `Patrol_01`, `Patrol_02`, `Patrol_03`;
- ссылки `manifest` и `crewAnchors` на `ShipCrewSpawner`.

На каждом обработанном ship prefab:

- сохранён `NetworkObject` и существующий `NpcShipController`;
- сохранён `ShipDeckNav` и deck NavMesh dependency;
- включён `ShipCrewSpawner.spawnOnNetworkSpawn`;
- включены временные `ShipCrewSpawner.debugLogs`;
- generic `NpcSpawner` отключён, чтобы не создавать параллельных random NPC;
- fixed crew использует runtime-путь `ShipCrewSpawner → NpcBrain.AttachToShipDeck()`.

Named captain prefabs добавлены в `Assets/DefaultNetworkPrefabs.asset`, а identities — в `Assets/_Project/Quests/Data/QuestDatabase.asset`.

## Статическая проверка

- Captain prefabs: `20`.
- Captain `NpcDefinition` assets: `20`.
- New crew manifests: `20`; всего manifests вместе с «Горгоной»: `21`.
- Проверка 20 ship prefabs: `20/20 OK`.
- На каждом корабле: generic spawner disabled, crew spawner enabled, manifest assigned, anchors assigned, пять обязательных anchors присутствуют.
- `role/required/respawnPolicy`: Captain / true / Never для всех 20 entries.
- Prefab/definition identity match: `20/20 OK`.
- Duplicate `npcId` среди новых captain definitions: `0`.
- Captain refs в `QuestDatabase.asset`: `20`.
- Captain refs в `DefaultNetworkPrefabs.asset`: `20`.
- `check_compile_errors`: `No compile errors`.

## Ограничения проверки

Play Mode, host/client NGO replication, фактический spawn на runtime-палубе, перемещение вместе с кораблём и отсутствие визуального пересечения с геометрией ещё не запускались. Этот этап подтверждает только статическую конфигурацию ассетов и ссылок; runtime-прогон выполняется отдельным ручным тестом пользователя.
