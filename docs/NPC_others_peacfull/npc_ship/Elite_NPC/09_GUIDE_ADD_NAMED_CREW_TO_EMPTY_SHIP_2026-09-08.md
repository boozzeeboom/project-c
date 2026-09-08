# Пошаговый гайд: как добавить именной NPC-экипаж на пустой корабль

**Дата:** 2026-09-08  
**Тикет:** `T-CREW-12`  
**Проект:** Project C / Unity 6  
**Каталог:** `docs/NPC_others_peacfull/npc_ship/Elite_NPC`  
**Статус:** рабочий процедурный гайд для fixed named crew

---

## 1. Назначение гайда

Этот документ описывает полный порядок подключения именного NPC-экипажа к новому кораблю, если исходный ship prefab не содержит NPC, crew anchors, manifest или crew spawner.

Целевой результат:

```text
ShipRoot
├── NetworkObject
├── NetworkTransform
├── NpcShipController
├── ShipDeckNav
├── ShipCrewSpawner
└── CrewAnchors
    ├── PilotSpawn
    ├── PilotSeatStand
    └── Patrol_01 / Patrol_02 / ...
```

NPC не хранится как заранее размещённый обычный child корабля. Он создаётся сервером из `ShipCrewManifest`, получает постоянный `npcId`, становится NGO child корабля и подключается к `ShipDeckNav` через официальный API `NpcBrain.AttachToShipDeck()`.

---

## 2. Архитектурные правила, которые нельзя нарушать

Перед началом убедиться, что команда придерживается следующих правил:

1. `NpcDefinition` отвечает за identity NPC: `npcId`, имя, фракцию, portrait, dialogue, quests и canonical prefab.
2. Назначение NPC на конкретный корабль хранится только в `ShipCrewManifest`.
3. Не добавлять `shipId` в `NpcDefinition` для решения crew assignment.
4. NPC не является `NetworkPlayer`.
5. NPC нельзя добавлять в `ShipController._pilots` через fake client ID.
6. Движением корабля продолжает управлять `NpcShipController`.
7. Fixed crew создаётся server-side через `ShipCrewSpawner`.
8. Для каждого crew member используется exact prefab из manifest entry.
9. У каждого crew member должен быть постоянный уникальный `npcId`.
10. Все spawn/seat/activity anchors должны быть детьми ShipRoot.
11. Attachment NPC к кораблю не должен зависеть только от `_platformMask` или physics probe.
12. Для нового корабля нельзя оставлять активным конфликтующий generic `NpcSpawner`, если random NPC не предусмотрены дизайном.
13. Сначала создаётся один минимальный fixed crew member и проверяется его lifecycle. Остальные роли добавляются после успешного MVP-прогона.

---

## 3. Рабочие эталонные файлы в проекте

Использовать существующую реализацию «Горгоны» как reference setup:

| Назначение | Файл |
|---|---|
| Identity пилота | `Assets/_Project/Quests/Data/Npcs/GorgonaPilot.asset` |
| Named NPC prefab | `Assets/_Project/Prefabs/NPC/Ships/Gorgona_Pilot.prefab` |
| Crew manifest | `Assets/_Project/Resources/PeacefulShip/ShipCrewManifest_Gorgona.asset` |
| Manifest code | `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewManifest.cs` |
| Runtime spawner | `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs` |
| NPC brain | `Assets/_Project/Scripts/AI/NpcBrain.cs` |
| Moving-deck navigation | `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` |
| Social/activity brain | `Assets/_Project/Scripts/AI/NpcSocialBrain.cs` |
| Network prefab registry | `Assets/DefaultNetworkPrefabs.asset` |
| Stage reports | `docs/NPC_others_peacfull/npc_ship/Elite_NPC/03...` – `08...` |

Текущий `Горгона.prefab` подтверждает полный рабочий набор компонентов: `NetworkObject`, `NetworkTransform`, `NpcShipController`, `ShipDeckNav`, `ShipCrewSpawner`, а также старый `NpcSpawner`, который пока является отдельным конфликтом и должен быть отключён на Этапе 8.

Текущий `Gorgona_Pilot.prefab` содержит `NpcController`, `NetworkObject`, `NavMeshAgent`, `NpcBrain`, `NpcTarget`, `NpcAttacker`, `NpcSocialBrain`, `NetworkTransform` и `CharacterController`.

---

# 4. Порядок реализации с пустого ship prefab

## Этап 0. Подготовить исходный корабль

### 0.1. Зафиксировать имя и asset path

Создать или выбрать prefab корабля, например:

```text
Assets/_Project/Prefabs/Ships/MyShip.prefab
```

Зафиксировать стабильный `shipId`, например:

```text
my_ship
```

`shipId` не должен зависеть от имени scene instance, `NetworkObjectId` или случайного runtime ID.

### 0.2. Проверить обязательный root

На верхнем ShipRoot должны находиться:

- `Transform`;
- `NetworkObject`;
- `NetworkTransform`, если transform корабля реплицируется клиентам;
- `Rigidbody`, если он нужен текущей корабельной физике;
- `NpcShipController`, если корабль управляется NPC autopilot;
- ship-specific компоненты управления, топлива, hull и ownership согласно обычной архитектуре корабля.

Crew-specific компоненты пока не добавлять, пока не проверен базовый network spawn самого корабля.

### 0.3. Проверить базовый spawn корабля

До добавления NPC убедиться, что:

- prefab присутствует в нужном registry/списке кораблей;
- root `NetworkObject` действительно спавнится server-side;
- клиент получает корабль через `NetworkTransform`;
- у корабля нет compile errors;
- colliders палубы существуют и не являются случайными trigger-only коллайдерами.

Если сам корабль не спавнится корректно, crew добавлять нельзя: ошибки crew lifecycle будут смешаны с ошибками корабля.

---

## Этап 1. Создать identity именного NPC

### 1.1. Создать `NpcDefinition`

Создать asset в каталоге NPC definitions:

```text
Assets/_Project/Quests/Data/Npcs/MyShipPilot.asset
```

В identity asset заполнить:

- уникальный `npcId`, например `my_ship_pilot_01`;
- localized display name;
- faction;
- portrait, если используется;
- dialogue/quest/service references, если NPC сюжетный;
- canonical prefab reference после создания prefab.

`npcId` должен быть глобально уникальным среди NPC проекта.

### 1.2. Не добавлять ship assignment в identity

Не записывать в `NpcDefinition`:

```text
shipId = my_ship
```

Один и тот же identity asset может использоваться в документах, диалогах и квестах, а assignment на корабль должен находиться в manifest.

### 1.3. Зарегистрировать identity в проекте

Если конкретная система проекта использует общий список NPC, добавить новый `NpcDefinition` в соответствующий `QuestDatabase.asset` или другой registry.

Проверить:

- нет второго asset с тем же `npcId`;
- dialogue/quest keys используют именно этот ID;
- canonical prefab reference не указывает на случайный generic prefab.

---

## Этап 2. Создать отдельный named NPC prefab

### 2.1. Взять безопасную основу

Использовать variant существующего полноценного NPC prefab, а не собирать минимальный GameObject вручную. Эталон:

```text
Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab
```

Создать variant, например:

```text
Assets/_Project/Prefabs/NPC/Ships/MyShip_Pilot.prefab
```

### 2.2. Проверить обязательный component stack

На root named prefab должны быть проверены как минимум:

- `Transform`;
- `NetworkObject`;
- `NetworkTransform`;
- `NpcController`;
- `NavMeshAgent`;
- `NpcBrain`;
- `NpcSocialBrain`, если NPC использует social/activity behavior;
- `NpcTarget`;
- `NpcAttacker`, если NPC имеет combat stack;
- `CharacterController` или другой требуемый controller;
- необходимые child renderers, colliders, animator и visual hierarchy.

### 2.3. Связать identity и prefab

На `NpcController` named prefab назначить:

```text
Definition = MyShipPilot.asset
```

В `MyShipPilot.asset` назначить canonical prefab:

```text
Prefab = MyShip_Pilot.prefab
```

Это двустороннее соответствие обязательно. `ShipCrewManifest.OnValidate()` и `ShipCrewSpawner` проверяют, что `NpcController.Definition` prefab совпадает с `npcDefinition` entry.

### 2.4. Настроить NPC как crew member

Проверить:

- `NpcBrain` не должен получать ship-specific assignment через identity;
- `_platformMask` может оставаться `0` — fixed crew attachment использует explicit API;
- `NavMeshAgent.agentTypeID` совместим с deck NavMesh;
- высота/радиус agent не ломают проходы по палубе;
- `NetworkObject` имеет корректный prefab identity;
- visual/collider hierarchy не содержит случайных лишних solid colliders, мешающих палубе.

`ShipCrewSpawner` на NPC prefab не добавлять. Spawner находится на корабле.

### 2.5. Не размещать fixed NPC вручную в новом пустом корабле

Для нового пустого корабля не добавлять pilot как заранее созданный scene child. Он должен быть создан `ShipCrewSpawner` из manifest.

Текущий spawner умеет повторно использовать уже существующего прямого child с тем же `npcId`, но это fallback для idempotency/reload, а не основной способ настройки нового корабля.

---

## Этап 3. Зарегистрировать named prefab в NGO

Добавить exact named prefab в:

```text
Assets/DefaultNetworkPrefabs.asset
```

Проверить:

- зарегистрирован именно `MyShip_Pilot.prefab`, а не исходный generic prefab;
- prefab не добавлен дважды;
- на prefab есть `NetworkObject`;
- все network components находятся на ожидаемом root;
- prefab доступен server-side в момент `NetworkObject.Spawn()`.

Без этой регистрации runtime `ShipCrewSpawner` может создать обычный GameObject, но NGO spawn/replication будет некорректным или завершится ошибкой.

---

## Этап 4. Создать `ShipCrewManifest`

### 4.1. Создать asset

Создать:

```text
Assets/_Project/Resources/PeacefulShip/ShipCrewManifest_MyShip.asset
```

Asset type:

```text
ProjectC/Peaceful Ship/Crew Manifest
```

### 4.2. Заполнить Ship Identity

```text
shipId = my_ship
```

Этот ID должен соответствовать назначению корабля в проектной документации и не должен быть `NetworkObjectId`.

### 4.3. Добавить первый crew entry

Для pilot entry заполнить:

```text
memberId          = my_ship_pilot_01
npcDefinition     = MyShipPilot.asset
prefab            = MyShip_Pilot.prefab
role              = Pilot
required          = true
respawnPolicy     = Never
spawnAnchorId     = pilot_spawn
seatAnchorId      = pilot_seat_stand
activityAnchorIds = patrol_01, patrol_02, patrol_03
```

### 4.4. Разница между `memberId` и `npcId`

- `memberId` — стабильный ключ участника внутри конкретного manifest;
- `npcId` — глобальная identity NPC из `NpcDefinition`.

Рекомендуется использовать одинаковую смысловую основу, но не путать эти поля. Например:

```text
memberId = my_ship_pilot_01
npcId    = my_ship_pilot_01
```

Для нескольких кораблей один и тот же NPC identity не следует без отдельного design decision назначать нескольким fixed crew manifest одновременно.

### 4.5. Проверки manifest

`ShipCrewManifest.OnValidate()` должен подтвердить:

- нет null entry;
- нет пустого `memberId`;
- нет duplicate `memberId`;
- нет duplicate `npcId`;
- задан `NpcDefinition` для required entry;
- задан exact prefab для required entry;
- prefab имеет `NpcController`;
- `NpcController.Definition` совпадает с `npcDefinition`;
- duplicate role даёт осознанное решение, а не случайную ошибку.

Manifest не должен ссылаться на generic `NpcSpawnerConfig`.

---

## Этап 5. Создать ship-local crew anchors

### 5.1. Создать иерархию

На ShipRoot создать direct child:

```text
CrewAnchors
```

На нём создать direct children:

```text
CrewAnchors
├── PilotSpawn
├── PilotSeatStand
├── Patrol_01
├── Patrol_02
└── Patrol_03
```

`ShipCrewSpawner.FindAnchor()` в текущей реализации ищет direct child под `CrewAnchors`. Не прятать рабочие anchors глубже в дополнительные промежуточные объекты.

### 5.2. Настроить transforms

Для каждого anchor:

- parent: `ShipRoot/CrewAnchors`;
- local position соответствует реальной геометрии корабля;
- rotation задаёт ожидаемую ориентацию NPC;
- scale: `(1, 1, 1)`;
- не добавлять renderer;
- не добавлять collider;
- не добавлять Rigidbody;
- не делать anchor частью отдельного движущегося визуального объекта.

### 5.3. Правила имён

Текущий lookup поддерживает прямое совпадение и normalised ID без `_` и `-`:

```text
pilot_spawn     → PilotSpawn
pilot-seat      → PilotSeat
patrol_01       → Patrol01
```

Несмотря на normalisation, использовать однозначные имена PascalCase для Transform и snake_case для binding IDs.

### 5.4. Минимальный набор anchors

Для одного пилота минимум:

```text
PilotSpawn
PilotSeatStand
Patrol_01
Patrol_02
Patrol_03
```

Если seat или activities ещё не реализованы, anchors всё равно можно создать заранее, но нельзя считать их подключёнными только потому, что они существуют. Binding должен быть реализован соответствующим runtime этапом.

---

## Этап 6. Подготовить `ShipDeckNav`

### 6.1. Добавить компонент

На ShipRoot добавить:

```text
ProjectC.Ship.ShipDeckNav
```

Текущая реализация `ShipDeckNav`:

- регистрирует baked `NavMeshData` server-side;
- выдаёт `IsReady` после регистрации;
- конвертирует world/deck/nav coordinates;
- используется `NpcBrain` для proxy navigation.

### 6.2. Подготовить NavMeshData

Создать/запечь deck NavMesh для геометрии палубы.

Согласно текущему коду `ShipDeckNav`, bake должен учитывать следующий принцип:

1. временно поставить корабль в origin/identity согласно pipeline проекта;
2. запечь только проходимую палубную поверхность;
3. назначить полученный `NavMeshData` в `_deckNavMesh`;
4. проверить agent type;
5. вернуть prefab transform в исходную конфигурацию;
6. сохранить asset.

Если `ShipDeckNav.IsReady == false`, `NpcBrain` должен ждать и не создавать proxy раньше времени.

### 6.3. Проверить настройки `ShipDeckNav`

Для движущегося корабля проверить:

```text
_registerServerOnly = true
_registerUnderShip = true
```

`_registerUnderShip=true` нужен для привязки nav-frame к положению ShipRoot при регистрации. Не заменять `ShipDeckNav` обычным мировым NavMesh корабля.

### 6.4. Проверить палубу

До подключения NPC проверить:

- палуба имеет проходимую геометрию;
- deck NavMesh не содержит дыр в местах anchors;
- `PilotSpawn` находится над допустимой поверхностью;
- высота палубы совместима с `NavMeshAgent` и `CharacterController`;
- agent type NPC совпадает с agent type baked NavMesh;
- палубные коллайдеры не являются только trigger.

---

## Этап 7. Добавить `ShipCrewSpawner` на корабль

### 7.1. Добавить компонент на ShipRoot

На root корабля добавить:

```text
ProjectC.PeacefulShip.Crew.ShipCrewSpawner
```

Компонент требует `NetworkObject`.

### 7.2. Назначить ссылки

В Inspector заполнить:

```text
Manifest     = ShipCrewManifest_MyShip.asset
Crew Anchors = ShipRoot/CrewAnchors
```

Lifecycle:

```text
spawnOnNetworkSpawn = true
```

Для первого runtime прогона:

```text
debugLogs = true
```

После подтверждения можно отключить подробные логи.

### 7.3. Что делает spawner

При server-side `OnNetworkSpawn()` корабля spawner:

1. сохраняет ссылку на ship `NetworkObject`;
2. ждёт один deferred frame;
3. проверяет manifest и CrewAnchors;
4. проверяет каждый entry;
5. разрешает spawn anchor;
6. создаёт exact prefab через `Instantiate`;
7. вызывает `NetworkObject.Spawn(destroyWithScene: true)`;
8. вызывает `NpcBrain.AttachToShipDeck()`;
9. сохраняет `memberId → NetworkObject` mapping;
10. повторно использует существующий spawned NPC с тем же `npcId`, если он уже является direct child ShipRoot.

Spawner server-only. На клиенте компонент отключается.

### 7.4. Чего spawner не делает на текущем этапе

Текущий `ShipCrewSpawner` пока не делает автоматически:

- pilot seat occupancy;
- `NpcSocialBrain` activity destination binding;
- patrol movement по `activityAnchorIds`;
- manifest respawn policy enforcement;
- отключение generic `NpcSpawner`.

Эти задачи относятся к отдельным этапам и не должны считаться готовыми только из-за успешного spawn.

---

## Этап 8. Подключить explicit moving-ship attachment

Текущий `NpcBrain` содержит:

```text
AttachToShipDeck(NetworkObject shipNetworkObject, ShipDeckNav deckNav = null)
DetachFromShipDeck()
IsExplicitShipAttachmentRequested
IsExplicitShipAttachmentActive
```

`ShipCrewSpawner` уже вызывает `AttachToShipDeck()` для нового и повторно найденного crew member.

### 8.1. Ожидаемый lifecycle attachment

```text
Ship NetworkObject.Spawn()
    ↓
ShipCrewSpawner.OnNetworkSpawn()
    ↓
ShipCrewSpawner.EnsureCrewSpawned()
    ↓
Instantiate exact NPC prefab
    ↓
NPC NetworkObject.Spawn()
    ↓
NpcBrain.AttachToShipDeck(ship, deckNav)
    ↓
NetworkObject.TrySetParent(ship)
    ↓
wait ShipDeckNav.IsReady
    ↓
create deck proxy
    ↓
WarpProxyToNpc()
```

### 8.2. Важные ограничения

- `_platformMask=0` не должен блокировать fixed crew attachment;
- не вызывать `TrySetParent` вручную из нового внешнего кода вместо `NpcBrain.AttachToShipDeck()`;
- не менять приватные `_deckNavActive`, `_parentedToShip` или `_proxyAgent` извне;
- не создавать proxy вручную в spawner;
- не добавлять NPC в `_pilots`;
- не выдавать NPC fake client ID.

### 8.3. Что проверять

После spawn на сервере проверить:

- `IsExplicitShipAttachmentRequested == true`;
- `IsExplicitShipAttachmentActive == true` после успешного parent;
- parent NPC — ShipRoot;
- `ShipDeckNav.IsReady == true`;
- proxy создан только после готовности NavMesh;
- NPC не остаётся в мировом пространстве при движении корабля.

---

## Этап 9. Подключить deck-aware activity movement

Это следующий runtime слой после attachment. В текущем проекте он ещё не считается завершённым.

### 9.1. Изменить `NpcBrain`

Добавить activity destination API, например:

```text
SetDeckActivityDestination(...)
ClearDeckActivityDestination()
```

`DriveDeckNav()` должен поддерживать три источника destination:

1. combat chase target;
2. social/activity destination;
3. отсутствие destination — stop.

### 9.2. Изменить `NpcSocialBrain`

В обычном NPC:

```text
NavMeshAgent.SetDestination(worldPosition)
```

В deck mode должно происходить:

```text
NpcBrain.SetDeckActivityDestination(deckTarget)
```

Нужно перевести как минимум:

- `Patrol`;
- `Work`;
- `Sit`;
- `Sleep`;
- `Socialize`;
- `Wander`;
- arrival checks;
- stuck timeout;
- activity reset при detach/despawn.

### 9.3. MVP-порядок activities

Для первого экипажа реализовать только:

```text
Patrol_01 → Patrol_02 → Patrol_03 → Patrol_01
```

Проверить:

- waypoint arrival;
- idle wait;
- переход к следующему waypoint;
- proxy-agent movement;
- отсутствие использования main agent для deck destination;
- продолжение patrol при движении корабля.

Только после успешного Patrol добавлять Work/Sit/Sleep/Socialize/Wander.

---

## Этап 10. Подключить pilot seat occupancy

Pilot seat — отдельное состояние NPC и не является ship control authority.

### 10.1. Подготовить seat component

Найти существующий `PilotSeatController` на корабле или его дочернем seat object.

Добавить NPC-specific API:

```text
TryBoardNpc(NetworkObject npc)
RemoveNpcOccupant(NetworkObject npc)
CurrentNpcOccupant
AssignedNpcId
```

### 10.2. Связать manifest entry с seat

Использовать:

```text
role        = Pilot
seatAnchorId = pilot_seat_stand
```

Seat integration должна:

- принимать только назначенного crew member;
- не позволять двум NPC занять один seat;
- сохранять `npcId` для UI/отладки;
- размещать NPC в seat pose;
- удалять occupant при despawn/death/detach.

### 10.3. Что запрещено

Не делать:

```text
ShipController._pilots.Add(fakeClientId)
```

NPC seat occupant и ship autopilot должны оставаться независимыми.

---

## Этап 11. Отключить generic `NpcSpawner`

Для нового пустого корабля правильный вариант — не добавлять `NpcSpawner` вообще.

Если корабль был скопирован с prefab, где он уже есть:

1. сначала завершить identity, prefab, manifest, anchors, `ShipDeckNav`, spawner и static checks;
2. убедиться, что fixed crew path готов к runtime;
3. отключить `NpcSpawner` только на этом ship prefab;
4. не менять поведение других кораблей;
5. проверить, что `NpcSpawner_ship_deck.asset` больше не создаёт generic NPC на этом корабле;
6. убедиться, что `Npc_Goblin 2` или другой random NPC не появляется параллельно fixed crew.

Не маскировать проблему generic spawn удалением fixed crew компонентов. Сначала должен быть проверен новый path.

---

## Этап 12. Зафиксировать lifecycle, death и respawn policy

В manifest есть:

```text
ShipCrewRespawnPolicy.Never
ShipCrewRespawnPolicy.OnShipSpawn
ShipCrewRespawnPolicy.AfterDelay
```

Но текущий `ShipCrewSpawner` ещё не применяет эту policy к `NpcBrain` автоматически. Это необходимо учитывать отдельно.

### 12.1. Обязательное design decision

Для каждого crew member письменно определить:

- исчезает ли NPC после смерти;
- создаётся ли он заново при новом spawn корабля;
- допускается ли runtime respawn;
- что происходит при unload/reload сцены;
- может ли NPC быть временно мёртвым без replacement;
- должен ли seat освободиться;
- нужно ли сохранять identity и role при persistence restore.

### 12.2. Важная текущая проверка

`NpcBrain` имеет собственные scene-NPC respawn settings. Нельзя считать `respawnPolicy=Never` достаточным, пока не выполнено одно из условий:

- manifest policy действительно применяется кодом к NPC;
- либо для fixed crew явно отключён обычный respawn path `NpcBrain`;
- либо stage 9 документирует контролируемое взаимодействие двух политик.

Иначе pilot может самореспавниться по старой generic NPC логике, несмотря на `ShipCrewManifest`.

### 12.3. Duplicate protection

При повторном `EnsureCrewSpawned()` проверить:

- mapping `memberId → NetworkObject` не создаёт второй объект;
- существующий NPC определяется по `npcId`;
- existing NPC действительно является direct child правильного ShipRoot;
- другой корабль не забирает чужого NPC;
- despawn/reload не оставляет stale mapping.

---

## Этап 13. Добавить editor validation и gizmos

Для каждого нового корабля полезно иметь editor validation, которая проверяет:

### ShipRoot

- `NetworkObject` присутствует;
- `ShipDeckNav` присутствует;
- `ShipCrewSpawner` присутствует;
- manifest назначен;
- `CrewAnchors` назначен;
- generic `NpcSpawner` отсутствует или выключен.

### Manifest

- `shipId` не пустой;
- `memberId` уникальны;
- `npcId` уникальны;
- роли корректны;
- required entry имеет definition и prefab;
- prefab definition совпадает;
- anchor IDs существуют.

### Named prefab

- `NetworkObject` присутствует;
- prefab зарегистрирован в `DefaultNetworkPrefabs.asset`;
- `NpcController.Definition` назначен;
- `NpcDefinition.prefab` указывает обратно на этот prefab;
- component stack не потерян в variant.

### Anchors

- anchors direct children `CrewAnchors`;
- нет duplicate names;
- scale равен `(1,1,1)`;
- spawn/seat/activity points не находятся за пределами палубы;
- gizmos показывают направление rotation и binding ID.

---

# 5. Короткий порядок действий для нового корабля

Если нужен только практический чеклист, выполнять строго так:

1. Создать ship prefab и проверить его самостоятельный network spawn.
2. Зафиксировать стабильный `shipId`.
3. Создать уникальный `NpcDefinition` и `npcId` каждого crew member.
4. Создать named NPC prefab variant.
5. Назначить `NpcController.Definition` на named prefab.
6. Назначить canonical prefab обратно в `NpcDefinition`.
7. Проверить полный NPC component stack.
8. Добавить named prefab в `DefaultNetworkPrefabs.asset`.
9. Создать `ShipCrewManifest_MyShip.asset`.
10. Добавить entries с role, prefab, definition, policy и anchor IDs.
11. Создать `CrewAnchors` direct child ShipRoot.
12. Создать `PilotSpawn`, seat anchor и activity anchors.
13. Добавить/настроить `ShipDeckNav` и baked deck NavMeshData.
14. Проверить `_registerServerOnly=true` и `_registerUnderShip=true`.
15. Добавить `ShipCrewSpawner` на ShipRoot.
16. Назначить manifest и `CrewAnchors`.
17. Включить `spawnOnNetworkSpawn`.
18. Для нового пустого ship не добавлять `NpcSpawner`; для старого prefab отключить его только на этом ship.
19. Проверить compile errors и manifest validation.
20. Проверить NGO prefab registration.
21. Запустить host runtime и подтвердить exact named spawn.
22. Проверить `npcId`, parent и local pose.
23. Проверить `ShipDeckNav.IsReady` и explicit attachment.
24. Проверить движение корабля без смещения NPC.
25. Подключить Patrol только после успешного attachment.
26. Подключить seat occupancy после успешного Patrol.
27. Отдельно проверить death/respawn/reload/persistence.
28. Сохранить screenshots и runtime report.
29. Только после acceptance считать crew setup завершённым.

---

# 6. Обязательная проверка перед закрытием задачи

## Static/editor checks

- `check_compile_errors` возвращает `No compile errors`;
- `validate_script` проходит для новых/изменённых C# scripts;
- manifest не выдаёт duplicate/missing validation errors;
- ship prefab metadata содержит `NetworkObject`, `ShipDeckNav`, `ShipCrewSpawner`;
- named prefab metadata содержит `NetworkObject`, `NpcController`, `NpcBrain`, `NavMeshAgent`, `NetworkTransform`;
- exact named prefab находится в `DefaultNetworkPrefabs.asset`;
- `NpcDefinition` и prefab ссылаются друг на друга корректно;
- anchors находятся под правильным ShipRoot;
- generic `NpcSpawner` не создаёт random crew для целевого корабля.

## Runtime host checks

- ship spawned server-side;
- fixed crew spawned ровно по одному разу;
- создан exact prefab, а не generic NPC;
- `NpcController.NpcId` совпадает с ожидаемым stable ID;
- `memberId → NetworkObject` mapping заполнен;
- NPC является child правильного ShipRoot;
- NPC не попал в `_pilots`;
- NPC не получил fake client ID;
- attachment активен даже при `_platformMask=0`;
- proxy создаётся после `ShipDeckNav.IsReady`;
- ship movement не оставляет NPC позади;
- при повторном `EnsureCrewSpawned()` дубль не появляется.

## Runtime client checks

- NPC появляется на client;
- NetworkTransform не создаёт заметного рассинхрона;
- parent/position/rotation визуально корректны;
- NPC не дёргается при lift/yaw/cruise;
- NPC не проваливается через палубу;
- NPC не появляется в origin или в старом мировом spawn point.

## Activity checks

После реализации Stage 6:

- NPC проходит `Patrol_01 → Patrol_02 → Patrol_03`;
- arrival threshold считается по deck proxy;
- idle wait работает;
- stuck timeout работает;
- activity продолжается во время движения корабля;
- после остановки корабля цикл не ломается;
- после detach activity state сбрасывается.

## Seat checks

После реализации Stage 7:

- seat знает конкретный `npcId`;
- второй NPC не может занять тот же seat;
- seat освобождается при despawn/death;
- autopilot корабля продолжает работать;
- NPC не становится fake player pilot.

## Lifecycle checks

После реализации Stage 9:

- смерть соответствует manifest policy;
- нет неожиданного generic respawn;
- reload сцены не создаёт второго NPC;
- unload корабля удаляет crew вместе с кораблём;
- persistence сохраняет identity и role;
- stale mappings очищаются.

---

# 7. Текущий статус реализации в проекте

На 2026-09-08 в проекте уже реализованы и статически проверены:

- MVP-контракт fixed named crew;
- identity и named prefab для пилота «Горгоны»;
- `ShipCrewManifest`;
- ship-local crew anchors;
- `ShipCrewSpawner`;
- explicit `NpcBrain.AttachToShipDeck()` / `DetachFromShipDeck()`;
- ожидание `ShipDeckNav.IsReady`;
- attachment независимо от `_platformMask`.

Ещё не подтверждены пользовательским Play Mode прогоном:

- фактический server spawn timing;
- NGO host/client replication;
- deck NavMesh registration;
- отсутствие смещения во время движения корабля;
- activity Patrol;
- pilot seat occupancy;
- generic spawner отключение;
- death/respawn/persistence policy.

Поэтому новый корабль нельзя считать полностью готовым только после добавления `ShipCrewSpawner`. Готовность должна подтверждаться всем checklist из раздела 6.
