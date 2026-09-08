# Именная команда NPC на движущемся корабле — повторный ресерч и план

**Дата статического исследования:** 2026-09-08  
**Статус:** исследование завершено; Этап 0 MVP-контракт завершён 2026-09-08, реализация кода ещё не начиналась.
**Целевой сценарий:** игрок случайно находит летящий корабль «Горгона» и встречает на нём конкретного именного пилота/капитана, а не случайного NPC из общего спавнера.

**Предыдущий документ:** `docs/NPC_others_peacfull/npc_ship/Elite_NPC/01_SHIP_CREW_RESEARCH.md`  
**Метод:** чтение актуального кода, prefab YAML, ScriptableObject-ассетов, NetworkConfig и связанных документов. Play Mode, сетевой прогон и визуальная проверка в этой итерации не выполнялись, поэтому фактическая runtime-работоспособность навмеша и полного spawn-flow остаётся непроверенной.

---

## 1. Итоговый ответ

### Можно ли это реализовать на текущей архитектуре?

**Да, но не текущим `NpcSpawner` и не текущей логикой `NpcSocialBrain` без расширений.**

В проекте уже есть все низкоуровневые части:

- NetworkObject и NetworkTransform у именного NPC;
- `NpcDefinition` с постоянным `npcId`, именем, диалогами, квестами и фракцией;
- серверный spawn через NGO;
- `ShipDeckNav` для навигации по палубе движущегося корабля;
- proxy-`NavMeshAgent` в `NpcBrain`;
- маршруты и activity anchors в `NpcSocialBrain`;
- NPC-автопилот корабля через `NpcShipController`.

**Главный пробел — отсутствие отдельной системы фиксированного состава команды корабля.** Сейчас `NpcSpawner` знает только один общий prefab и правила случайного спавна. Он не знает, что у «Горгоны» должен быть именно `gorgona_pilot_01`, где этот персонаж появляется, какие у него точки маршрута и какое кресло ему назначено.

**Второй критический пробел — activities по палубе не подключены к deck-nav proxy.** `NpcSocialBrain` отправляет `NavMeshAgent` на мировые позиции. На корабле основной агент переводится в режим `updatePosition=false`, а proxy используется только в сценарии `Chase`. Поэтому обычный `Patrol`, `Work`, `Sit`, `Sleep`, `Socialize` и `Wander` на движущемся корабле сейчас не образуют законченный рабочий путь.

**Третий пробел — кресло пилота пока не является NPC-системой.** `PilotSeatController.TryBoard(ulong clientId)` только пишет лог и возвращает `true`. Реальное управление кораблём уже выполняет `NpcShipController` через `_hasNpcPilot` и `NavTick`; NPC, сидящий визуально в кресле, не связан с этим состоянием.

---

## 2. Подтверждённая инвентаризация

| Узел | Файл/ассет | Подтверждённое состояние |
|---|---|---|
| Общий спавнер | `Assets/_Project/Scripts/AI/NpcSpawner.cs` | Server-only, один `_prefab`, случайная точка, activation radius, лимиты, social override, `SpawnMode` |
| Конфиг общего спавнера | `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs` | `npcPrefab`, spawn rules, behavior, faction, idle activity, patrol `Vector3[]`, loot |
| Конфиг палубного спавнера | `Assets/_Project/Resources/AI/NpcSpawner_ship_deck.asset` | `Npc_Goblin 2`, `maxAliveCount=20`, `activationRadius=147`, `spawnMode=Infinite`, `defaultIdleActivity=StandStill`, `patrolWaypoints` пустой |
| Именной prefab | `Assets/_Project/Prefabs/NPC/[Mira] - DON\`T DELETE DEFOULT.prefab` | `NpcController`, `NpcDefinition`, `NetworkObject`, `NavMeshAgent`, `NpcBrain`, `NpcTarget`, `NpcAttacker`, `NpcSocialBrain`, `NetworkTransform`, `CharacterController` |
| Идентичность Mira | `Assets/_Project/Quests/Data/Npcs/Mira.asset` | `npcId=mira_01`, локализованное имя, faction, default dialog, quest turn-in; поле `prefab` сейчас `null` |
| Network registration Mira | `Assets/DefaultNetworkPrefabs.asset` | prefab Mira уже зарегистрирован в сетевом списке, GUID `172a127fa0f35124bb11eda19a17b16e` найден |
| Навигация палубы | `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` | `NavMeshData`, server-only registration, `WorldToDeckLocal`, `DeckLocalToWorld`, `DeckLocalToNav`, `SampleOnDeck`, `_registerUnderShip` |
| AI движения | `Assets/_Project/Scripts/AI/NpcBrain.cs` | platform carry, optional NGO parenting, deck proxy, `WarpProxyToNpc`, `DriveDeckNav` |
| Activities | `Assets/_Project/Scripts/AI/NpcSocialBrain.cs` | Patrol/Wander/LookAround/Socialize/Work/Sit/Sleep, `patrolWaypointMarkers`, activity anchors |
| NPC-корабль | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | server-side NPC pilot/autopilot, schedule, cargo, control authority |
| Сервер NPC-кораблей | `Assets/_Project/Scripts/PeacefulShip/Network/NpcShipServer.cs` | только discovery/registration NPC-кораблей; экипаж не создаёт |
| DTO NPC-корабля | `Assets/_Project/Scripts/PeacefulShip/Dto/NpcShipSpawnDto.cs` | данные самого NPC-корабля, crew members в DTO отсутствуют |
| Корабль «Горгона» | `Assets/_Project/Prefabs/Ships/Горгона.prefab` | `NpcShipController`, `NpcSpawner`, `ShipDeckNav`, `PilotSeat`, `ShipRootReference`, DeckNavSurface |
| Предыдущий анализ | `docs/.../Elite_NPC/01_SHIP_CREW_RESEARCH.md` | правильно выявил отсутствие crew roster и generic spawn, но не довёл до конца анализ deck-aware activities и разделения NPC-seat/autopilot |

---

## 3. Как работает текущий поток

### 3.1. Что происходит при появлении корабля

```text
ScenePlacedObjectSpawner
    → NetworkObject.Spawn() для корабля
        → NpcShipController.OnNetworkSpawn()
            → EnableNpcPilot(true)
            → ShipController.SetEngineRunning(true)
            → регистрация в NpcShipWorld
        → ShipDeckNav.OnNetworkSpawn()
            → постановка регистрации NavMeshData в очередь
        → NpcSpawner.OnNetworkSpawn()
            → читает NpcSpawner_ship_deck.asset
            → позже начинает случайный spawn вокруг anchor
```

`NpcShipServer` через задержку обнаруживает `NpcShipController`, но занимается регистрацией расписания и транспорта. В коде нет шага «прочитать roster корабля и создать фиксированных членов команды».

### 3.2. Что происходит при обычном NPC spawn

`NpcSpawner.TrySpawnAtPoint()`:

1. делает `Instantiate(_prefab, spawnPos, Quaternion.identity)`;
2. ищет `NetworkObject`;
3. применяет visual/behavior/social/loot config;
4. вызывает `NetworkObject.Spawn(destroyWithScene: true)`;
5. добавляет объект в список generic NPC.

В этом пути нет:

- ссылки на `NpcDefinition` конкретного crew member;
- проверки роли;
- фиксированной spawn-точки на корабле;
- явного parent к ShipRoot;
- назначения кресла;
- отдельной политики смерти/респавна именного NPC.

### 3.3. Что происходит с именным NPC

`NpcController` берёт `NpcDefinition` из prefab и предоставляет `NpcId`. `NpcBrain.OnNetworkSpawn()` кэширует `definition.npcId` в `_npcId`. Это означает, что identity path уже работает:

```text
NpcController.definition
    → NpcDefinition.npcId
        → NpcBrain._npcId
            → QuestWorld / attitude / dialogue / quest references
```

Для Mira это `mira_01`.

Однако `Mira.asset` имеет `prefab: null`. Поэтому текущая система не имеет единого гарантированного правила «по `NpcDefinition` найти prefab для runtime spawn». Для crew spawn prefab должен быть назначен явно в crew roster либо поле `NpcDefinition.prefab` должно стать обязательным для таких NPC.

---

## 4. Что уже работает для движущегося корабля

### 4.1. Перенос NPC вместе с палубой

`NpcBrain` имеет:

- `_platformCarryEnabled`;
- `_platformMask`;
- `_useParentingOnShips`;
- `BeginRide()` / `EndRide()`;
- `NetworkObject.TrySetParent()`;
- вычисление дельты движения платформы.

Но у проверенного prefab Mira в YAML:

```yaml
_platformCarryEnabled: 1
_platformMask:
  m_Bits: 0
_useParentingOnShips: 1
```

`m_Bits: 0` означает, что физический probe палубы отключён. Для hand-placed NPC это может быть намеренной настройкой мира, но для crew spawn нельзя рассчитывать на автоматическое обнаружение палубы. Именной экипаж должен получать явное ship attachment при создании.

### 4.2. Навигация proxy-agent

`ShipDeckNav` и `NpcBrain` имеют нужную базовую архитектуру:

```text
NPC world position
    ↔ ShipRoot local position
    ↔ nav-frame position
    ↔ proxy NavMeshAgent
```

`ShipDeckNav` у «Горгоны» имеет назначенный `_deckNavMeshData`, `_registerServerOnly=1` и `_registerUnderShip=1`. У Mira и DeckNavSurface текущий `AgentTypeID=0`, то есть статически тип совпадает.

**Но это не подтверждает, что именно текущий NavMeshData «Горгоны» покрывает палубу в runtime.** В `Горгона.prefab` отключённый `DeckNavSurface` имеет `m_LayerMask.m_Bits=64` и `m_UseGeometry=1`; это может быть корректно для уже сохранённого ассета, но без Play Mode/`SamplePosition` проверки результат остаётся inconclusive.

### 4.3. Автопилот корабля

`NpcShipController.OnNetworkSpawn()` вызывает:

```text
ShipController.EnableNpcPilot(true)
ShipController.SetEngineRunning(true)
```

`NpcShipController.NavTick()` затем управляет полётом корабля независимо от того, есть ли физический NPC в кресле.

Это важное разделение:

- **NPC-pilot mode** — техническое управление кораблём;
- **NPC в PilotSeat** — визуальная/игровая принадлежность crew member к креслу.

Сейчас эти понятия не связаны, и связывать их через `_pilots` нельзя: `ShipController.AddPilot()` принимает только `NetworkPlayer`, а не NPC.

---

## 5. Критические блокеры

### Блокер A — `NpcSpawner` не является crew spawner

`NpcSpawner` работает с одним `_prefab` и generic spawn rules. Для «Горгоны» это `Npc_Goblin 2`, а не конкретный капитан или пилот.

Даже `Finite`/`FiniteCycle` не решают задачу: они ограничивают количество generic экземпляров, но не содержат список конкретных `NpcDefinition`.

**Вывод:** нужен отдельный fixed-roster spawn path. Generic `NpcSpawner` на корабле нельзя считать системой именной команды.

### Блокер B — activities не умеют двигаться через deck proxy

`NpcSocialBrain.ExecutePatrol()`, `ExecuteWork()`, `ExecuteSit()`, `ExecuteSleep()`, `ExecuteSocialize()` и `ExecuteWander()` напрямую вызывают `_agent.SetDestination(...)`.

На движущемся корабле:

- основной агент получает `updatePosition=false` в `NpcBrain.BeginRide()`;
- позицию должен менять `_proxyAgent`;
- `NpcBrain.DriveDeckNav()` ставит proxy destination только в ветке `_state == Chase`;
- в остальных состояниях proxy останавливается.

Следствие: NPC может быть корректно приклеен к кораблю, но обычная работа/патруль не будет двигать его по палубе.

Это главный технический пробел, который не был явно закрыт в предыдущем исследовании.

### Блокер C — автоматическое attachment не подходит для crew spawn

Для generic NPC attachment определяется лучом/сферой по `_platformMask`. У Mira mask пустой. Кроме того, crew должен быть прикреплён детерминированно сразу после spawn, а не «когда следующий FixedUpdate найдёт палубу».

Нужен серверный API наподобие:

```text
NpcBrain.AttachToShipDeck(shipRoot, shipDeckNav, localPose)
```

Он должен:

- установить ship parent;
- сохранить локальную позицию/rotation;
- активировать deck proxy;
- warp proxy в локальную spawn-точку;
- сообщить `NpcSocialBrain`, что движение должно идти через deck-nav.

### Блокер D — PilotSeat не хранит NPC occupant

`PilotSeatController.TryBoard(ulong clientId)`:

- проверяет только `ShipController`;
- пишет лог;
- возвращает `true`;
- не хранит occupant;
- не вызывает отдельную NPC-связку.

`ShipController._pilots` — это `HashSet<ulong>` клиентских ID. NPC не должен добавляться туда как поддельный client ID.

### Блокер E — нет crew manifest и уникальной привязки

`NpcDefinition` хранит identity персонажа, но не является хорошим местом для `shipId`:

- один и тот же NPC может быть временно на корабле, в порту или в квестовой сцене;
- ship membership — контекст конкретного экземпляра корабля;
- добавление `shipId` в identity смешает постоянные данные NPC и runtime assignment.

Нужен отдельный manifest корабля:

```text
ShipCrewManifest
    → CrewMemberEntry
        → exact prefab / NpcDefinition
        → role
        → spawn anchor
        → activity profile
        → optional seat
```

### Блокер F — lifecycle и persistence не определены

Не определено, что происходит с именным членом команды, если:

- NPC погиб;
- игрок покинул корабль;
- корабль выгрузил сцену;
- корабль восстановлен из persistence;
- `NpcShipServer` повторно обнаружил тот же корабль;
- ship prefab уже содержит старую crew-группу.

Без idempotent spawn можно получить дубликаты именных NPC.

### Блокер G — conflict с generic spawner

Если на «Горгоне» оставить текущий `NpcSpawner_ship_deck.asset`, после добавления crew system корабль будет содержать одновременно:

- фиксированных именованных членов команды;
- случайных `Npc_Goblin 2`.

Это противоречит целевому сценарию и создаёт неоднозначность при проверке численности экипажа.

---

## 6. Сопоставление с предыдущим исследованием

### Что предыдущий документ подтвердил правильно

1. На корабле уже есть `NpcShipController`, `NpcSpawner`, `ShipDeckNav` и `PilotSeatController`.
2. Именной NPC имеет полноценный `NpcDefinition` и тот же общий AI stack.
3. Текущий «экипаж» «Горгоны» — generic NPC из `NpcSpawner_ship_deck.asset`.
4. Нет отдельного `ShipCrewManifest`/roster.
5. `PilotSeatController.TryBoard()` ещё не реализует NPC boarding.
6. Для маршрутов на движущемся корабле нужны локальные координаты или child markers палубы.

### Что уточнено или скорректировано

#### 1. Нельзя считать patrol уже готовым для движущейся палубы

Предыдущий документ перечисляет `NpcSocialBrain` и patrol как существующие возможности. Это верно для обычного NavMesh, но недостаточно для движущегося корабля. Текущий deck proxy активен в основном для Chase; idle activities продолжают работать через основной `_agent`.

**Новая оценка:** deck carry и deck chase существуют, deck idle/activity navigation — отсутствует.

#### 2. `shipId` в `NpcDefinition` не должен быть первым изменением

Предыдущий план предлагал добавить `shipId` и `shipCrewRole` в `NpcDefinition`. Для production-архитектуры правильнее оставить `NpcDefinition` идентичностью NPC, а назначение на корабль вынести в `ShipCrewManifest`.

`shipCrewRole` допустимо хранить в assignment entry, потому что роль зависит от конкретной команды/корабля.

#### 3. `PilotSeatController.TryBoard(npcId)` не решает управление кораблём

Даже после добавления NPC ID в seat controller корабль будет двигаться через `NpcShipController`, а не через `ShipController._pilots`. Поэтому нужны две независимые сущности:

- `NpcPilotSeatOccupant` — кто занимает место;
- `NpcShipController` — кто даёт кораблю движение.

Связывать их можно событием/ссылкой, но нельзя симулировать NPC через `NetworkPlayer` client ID.

#### 4. Для spawn лучше ship-local component, а не глобальный `NpcShipServer`

Предыдущий план допускал реализацию crew spawn в `NpcShipServer`. Это глобальный server hub транспорта. Более безопасно поместить `ShipCrewSpawner` на prefab корабля и запускать его в lifecycle самого `NetworkObject` корабля. `NpcShipServer` должен остаться registry/traffic subsystem.

#### 5. Network registration уже частично решён

Предыдущий документ не фиксировал, что Mira prefab уже найден в `Assets/DefaultNetworkPrefabs.asset`. Для нового crew prefab registration всё равно должен быть частью editor validation, но это не фундаментальный неизвестный блокер.

---

## 7. Рекомендуемая архитектура реализации

### 7.1. `NpcDefinition` остаётся identity asset

Не добавлять в него постоянный `shipId` на первом этапе.

Оставить там:

- `npcId`;
- localized display name;
- faction;
- portrait;
- dialogue/quests/services;
- prefab reference как canonical reference для named NPC.

Для каждого реального crew member создать уникальный `NpcDefinition`, например:

```text
gorgona_pilot_01
```

Если персонаж должен быть полноценным сюжетным NPC, его `npcId` не должен зависеть от случайного spawn instance ID.

### 7.2. Новый `ShipCrewManifest` ScriptableObject

Создать один manifest на корабль или ship archetype:

```text
ShipCrewManifest_Gorgona.asset
```

Запись команды должна содержать минимум:

```text
CrewMemberEntry
    memberId
    npcDefinition
    npcPrefab
    role: Captain / Pilot / Gunner / Engineer / Navigator
    required
    respawnPolicy
```

`npcPrefab` указывать явно на первом этапе, потому что у Mira текущий `NpcDefinition.prefab` пустой.

### 7.3. Новый `ShipCrewSpawner` на корне корабля

Компонент должен быть дочерним функциональным слоем `Горгона.prefab` и работать только на сервере.

Его ответственность:

1. дождаться, когда ship `NetworkObject` spawned;
2. прочитать manifest;
3. проверить duplicate `memberId`/`npcId`;
4. создать точный prefab каждого entry;
5. выставить локальную spawn pose;
6. вызвать `NetworkObject.Spawn(destroyWithScene: true)`;
7. явно parent к ship root через NGO;
8. связать NPC с `ShipDeckNav`;
9. передать activity profile в `NpcSocialBrain`;
10. зарегистрировать occupant в соответствующем `PilotSeatController`;
11. не создавать повторно уже существующего member instance.

`NpcSpawner` для fixed crew не использовать.

### 7.4. Ship-local anchors

На prefab корабля создать дочерние markers под ShipRoot:

```text
CrewAnchors/
    PilotSpawn
    PilotSeatStand
    Patrol_01
    Patrol_02
    Patrol_03
    EngineWork
    CargoWork
```

В manifest/binding нужно хранить ссылки на эти точки либо устойчивые anchor IDs, разрешаемые через `ShipCrewSpawner`.

Все markers должны быть детьми корабля. Тогда их world position корректно следует за кораблём, а их local pose остаётся стабильной.

### 7.5. Явное подключение NPC к deck navigation

В `NpcBrain` добавить публичный серверный API для crew attachment. Он должен заменить нести ответственность за частный state, а не позволить внешним системам вручную менять `_deckNavActive`.

Минимальный контракт:

```text
AttachToShipDeck(NetworkObject shipRoot,
                 ShipDeckNav deckNav,
                 Vector3 localPosition,
                 Quaternion localRotation)
DetachFromShipDeck()
SetDeckActivityDestination(Vector3 worldDestination)
ClearDeckActivityDestination()
IsDeckNavigationActive
```

`AttachToShipDeck()` должен:

- использовать `TrySetParent` к spawned ship root;
- сохранить local pose;
- создать/warp proxy после `ShipDeckNav.IsReady`;
- не зависеть от `_platformMask`;
- быть idempotent.

### 7.6. Deck-aware `NpcSocialBrain`

`NpcSocialBrain` должен выбирать не напрямую `_agent.SetDestination`, а единый movement adapter:

```text
if deck navigation active:
    NpcBrain.SetDeckActivityDestination(target)
else:
    main NavMeshAgent.SetDestination(target)
```

Изменения нужны минимум для:

- Patrol;
- Work;
- Sit;
- Sleep;
- Socialize;
- Wander;
- arrival/stuck checks.

Для deck mode arrival должен вычисляться по proxy-agent, а не по main agent.

`NpcBrain.DriveDeckNav()` должен использовать три типа destination:

1. combat chase target;
2. social/activity destination;
3. no destination → stop.

### 7.7. Pilot seat — отдельное состояние occupant

В `PilotSeatController` добавить NPC-specific state, например:

```text
TryBoardNpc(NetworkObject npc)
RemoveNpcOccupant(NetworkObject npc)
CurrentNpcOccupant
```

Этот state нужен для:

- визуального размещения пилота;
- запрета занять кресло несколькими NPC;
- UI/interaction проверки;
- будущей передачи роли.

В MVP `NpcShipController` продолжает управлять кораблём через собственный autopilot. `PilotSeatController` не добавляет NPC в `_pilots` и не подменяет `NetworkPlayer`.

### 7.8. Generic spawner на «Горгоне»

После подключения fixed crew:

- отключить `NpcSpawner` на `Горгоне`, либо
- убрать его `NpcSpawner_ship_deck.asset`, либо
- добавить явный режим `GenericSpawnDisabled`.

Для целевого сценария на корабле не должно быть случайного goblin spawn, если он не предусмотрен отдельным типом корабля.

---

## 8. Пошаговый план реализации

### Этап 0 — зафиксировать MVP-контракт

**Статус:** ✅ DONE — `T-CREW-06`, документировано в `03_IMPLEMENTATION_STAGE_00_MVP_CONTRACT_2026-09-08.md`.

**Цель:** один корабль, один именной пилот, одна activity.

1. Выбрать точный `NpcDefinition` пилота «Горгоны».
2. Создать стабильный `npcId`, например `gorgona_pilot_01`.
3. Решить, что корабль в MVP продолжает лететь через `NpcShipController`.
4. Решить политику смерти: fixed crew не заменяется случайным NPC.
5. Решить, что generic `NpcSpawner` на «Горгоне» выключается.

**Зафиксировано:**
- корабль: `Горгона`;
- пилот: `gorgona_pilot_01`;
- MVP activity: `Patrol`;
- fixed crew не заменяется случайным NPC после смерти;
- управление кораблём остаётся у `NpcShipController`;
- pilot seat хранит occupant отдельно и не добавляет NPC в `_pilots`;
- generic `NpcSpawner` отключается после готовности fixed crew path;
- attachment выполняется явно через crew system и не зависит от `_platformMask`.

**Результат:** зафиксированный контракт без смешения autopilot и NPC seat.

### Этап 1 — подготовить identity и prefab

**Статус:** ✅ DONE — `T-CREW-07`, документировано в `04_IMPLEMENTATION_STAGE_01_IDENTITY_AND_PREFAB_2026-09-08.md`.

1. Создан `Assets/_Project/Quests/Data/Npcs/GorgonaPilot.asset` с `npcId=gorgona_pilot_01`.
2. Создан `Assets/_Project/Prefabs/NPC/Ships/Gorgona_Pilot.prefab` как variant текущего named NPC prefab.
3. `NpcController.definition` назначен на `GorgonaPilot.asset`.
4. Уникальность `npcId` подтверждена статически относительно выбранного нового asset; глобальный runtime duplicate scan ещё не выполнялся.
5. Заполнена canonical `NpcDefinition.prefab` ссылка на `Gorgona_Pilot`.
6. Наличие в `DefaultNetworkPrefabs.asset` подтверждено по dependency list; `QuestDatabase.asset` также получил ссылку на новый NPC в общем списке `npcs`.
7. На variant сохранены `NetworkObject`, `NetworkTransform`, `NpcBrain`, `NpcSocialBrain`, `NpcTarget`, `NpcAttacker`, `NavMeshAgent` и `CharacterController`.
8. Baseline `_platformMask` не изменялся; attachment остаётся задачей crew system.

**Acceptance:** named prefab связан с конкретным `NpcDefinition` и ожидаемым `NpcId`; `check_compile_errors` — `No compile errors`.

### Этап 2 — создать manifest команды

**Статус:** ✅ DONE — `T-CREW-08`, документировано в `05_IMPLEMENTATION_STAGE_02_CREW_MANIFEST_2026-09-08.md`.

1. Создан `ShipCrewManifest` и serializable entry в `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewManifest.cs`.
2. Entry содержит `npcDefinition`, exact prefab, role, required, respawn policy, spawn/seat/activity anchor IDs.
3. Создан `Assets/_Project/Resources/PeacefulShip/ShipCrewManifest_Gorgona.asset`.
4. Добавлен pilot entry `gorgona_pilot_01` с ролью `Pilot`, `required=true` и `respawnPolicy=Never`.
5. Добавлена editor validation:
   - prefab не null;
   - `NpcController` присутствует;
   - `NpcController.Definition` совпадает с entry;
   - `memberId` и `npcId` не дублируются;
   - повторное использование role выдаёт warning.

**Acceptance:** asset описывает состав «Горгоны» без ссылок на generic `NpcSpawner`; compile errors отсутствуют.

### Этап 3 — добавить ship-local anchors

**Статус:** ✅ DONE — `T-CREW-09`, документировано в `06_IMPLEMENTATION_STAGE_03_SHIP_LOCAL_ANCHORS_2026-09-08.md`.

1. На `Assets/_Project/Prefabs/Ships/Горгона.prefab` создан `CrewAnchors`.
2. Созданы `PilotSpawn` и `PilotSeatStand`.
3. Созданы три patrol точки: `Patrol_01`, `Patrol_02`, `Patrol_03`.
4. Проверено, что все точки являются детьми `Горгона/CrewAnchors`, то есть двигаются вместе с ShipRoot.
5. В manifest уже зафиксированы соответствующие binding IDs: `pilot_spawn`, `pilot_seat_stand`, `patrol_01`, `patrol_02`, `patrol_03`.

**Acceptance:** все точки имеют стабильные ShipRoot-local transforms; runtime sampling ещё не проверялся.

### Этап 4 — реализовать `ShipCrewSpawner`

1. Добавить компонент на корень `Горгона`.
2. Сослаться на manifest.
3. Запускать spawn только на сервере после `NetworkObject.IsSpawned`.
4. Сделать spawn idempotent.
5. Спавнить точный prefab в local pose.
6. Вызывать `NetworkObject.Spawn(destroyWithScene: true)`.
7. Делать explicit NGO parent к ShipRoot.
8. Убрать зависимость от player activation radius.
9. Убрать зависимость от ground raycast generic spawner.
10. Сохранять runtime mapping `memberId → NetworkObject`.

**Acceptance:** при появлении «Горгоны» создаётся ровно один нужный пилот и не создаётся goblin из crew path.

### Этап 5 — explicit moving-ship attachment

1. Добавить `NpcBrain.AttachToShipDeck()`.
2. Добавить `DetachFromShipDeck()`.
3. Подключить `ShipDeckNav` корабля.
4. При `IsReady=false` дождаться регистрации, не создавая ошибочный proxy.
5. Warp proxy в spawn point.
6. Подтвердить local position после движения корабля.
7. Не использовать `_platformMask` как единственный источник attachment.

**Acceptance:** пилот следует за движением корабля даже при `_platformMask=0` на исходном prefab.

### Этап 6 — deck-aware activities

1. Добавить activity destination API в `NpcBrain`.
2. Расширить `DriveDeckNav()` для social/activity destination.
3. Перевести `ExecutePatrol()` на новый movement adapter.
4. Перевести arrival/stuck checks на proxy-agent в deck mode.
5. Перевести `Work`, `Sit`, `Sleep`, `Socialize`, `Wander`.
6. Сохранить старый путь для NPC вне корабля.
7. Добавить reset activity state при detach/despawn.

**Acceptance:** пилот проходит `Patrol_01 → Patrol_02 → Patrol_03` по палубе движущегося корабля и продолжает цикл.

### Этап 7 — pilot seat occupancy

1. Добавить NPC occupant API в `PilotSeatController`.
2. Связать role `Pilot` с конкретным `PilotSeat`.
3. Разместить NPC в seat stand/seat pose.
4. Не добавлять fake client ID в `ShipController._pilots`.
5. Оставить движение корабля за `NpcShipController.NavTick()`.
6. Добавить публичные read-only данные `AssignedNpcId`/`CurrentNpcOccupant` для UI и отладки.

**Acceptance:** в runtime можно однозначно сказать, какой `npcId` занимает pilot seat «Горгоны».

### Этап 8 — отключить конфликтующий generic spawn

1. На `Горгоне` отключить `NpcSpawner` или перевести его в explicit disabled mode.
2. Проверить, что `NpcSpawner_ship_deck.asset` не вызывает goblin refill.
3. Для других ship prefabs не менять поведение без отдельного задания.

**Acceptance:** fixed crew не смешивается с random crew.

### Этап 9 — lifecycle и persistence

1. Определить, что происходит при смерти pilot.
2. Определить, можно ли его встретить мёртвым/временно отсутствующим.
3. Зафиксировать `RespawnPolicy`.
4. При unload сцены уничтожать crew вместе с ship scene object.
5. При повторном discover не создавать дубликаты.
6. При persistence restore не менять identity и role.
7. При повторном spawn проверять runtime mapping и `npcId`.

**Acceptance:** повторная загрузка сцены и повторный discovery не создают второго пилота.

### Этап 10 — editor validation и документация

1. Создать custom editor для crew manifest/spawner.
2. Показывать ошибки отсутствующего prefab, definition, anchor, seat и duplicate IDs.
3. Добавить gizmos spawn/activity points.
4. Документировать setup для нового корабля.
5. Сохранить отдельный iteration report после реализации.

---

## 9. Обязательные проверки после кода

Play Mode в этой исследовательской итерации не запускался. После реализации пользователь должен провести host/client проверку и сделать screenshots.

### Spawn и identity

- загрузить сцену с «Горгоной»;
- убедиться, что появляется только fixed crew;
- проверить `NpcController.NpcId == gorgona_pilot_01`;
- проверить localized display name;
- убедиться, что `Npc_Goblin 2` не появляется от этого корабля;
- проверить один экземпляр при повторном discovery.

### Движение корабля

- зафиксировать пилота на палубе;
- дать кораблю пройти lift/yaw/cruise;
- проверить отсутствие смещения относительно корабля;
- проверить pitch/roll policy;
- проверить NetworkTransform на client.

### Activities

- выбрать Patrol;
- пройти все палубные точки;
- проверить idle wait на waypoint;
- проверить anti-stuck;
- проверить Work/Sit только после Patrol MVP;
- остановить корабль и снова продолжить движение.

### Seat

- убедиться, что pilot seat занят нужным NPC;
- убедиться, что `PilotCount` не получает fake client;
- убедиться, что автопилот не ломается после удаления/смерти NPC;
- проверить переход control authority при посадке игрока.

### Despawn/persistence

- выгрузить world scene;
- загрузить сцену повторно;
- восстановить позицию корабля;
- убедиться, что crew создаётся ровно один раз и остаётся привязанным к кораблю.

---

## 10. Что считать готовым результатом

Фича считается готовой для MVP, если одновременно выполнены все условия:

1. «Горгона» создаёт exact named prefab, а не generic prefab.
2. У NPC сохраняется постоянный `npcId` и работают его dialogue/quest references.
3. NPC создаётся server-side и реплицируется клиентам через NGO.
4. NPC сразу привязан к ShipRoot и не зависит от `_platformMask` для crew attachment.
5. NPC перемещается по палубным local anchors через deck proxy в состоянии Idle.
6. Pilot seat знает конкретного NPC occupant.
7. Корабль продолжает управляться `NpcShipController`, без fake `NetworkPlayer` pilot.
8. Generic `NpcSpawner` не создаёт дублирующий random crew.
9. Повторный spawn/discovery не создаёт дубликаты.
10. Пользовательский Play Mode прогон подтверждает spawn, движение, activity, seat и scene reload.

---

## 11. Финальный вердикт

**Проект технически готов как база, но сценарий “именованный пилот на движущейся Горгоне” ещё не реализован.**

Недостаёт не нового AI-движка, а четырёх связующих слоёв:

1. fixed named crew manifest;
2. ship-local deterministic spawn и explicit attachment;
3. deck-aware execution всех idle activities;
4. отдельная NPC seat occupancy модель поверх существующего ship autopilot.

Самый безопасный путь — не перегружать `NpcDefinition`, не расширять глобальный `NpcShipServer` обязанностями экипажа и не пытаться представить NPC как `NetworkPlayer`. Команда должна быть data-driven объектом конкретного корабля, а `NpcBrain`/`NpcSocialBrain` должны получить официальный API для движения по deck proxy в Idle/activity состояниях.

**Runtime-часть пока остаётся частично inconclusive:** статический код подтверждает наличие нужной инфраструктуры, но фактическую работу `Горгона NavMeshData`, spawn timing, NGO parenting и activity movement необходимо подтвердить отдельным Play Mode-прогоном пользователя со screenshots.
