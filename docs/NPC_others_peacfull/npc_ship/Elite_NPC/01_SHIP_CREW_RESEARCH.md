# Elite NPC — Именные экипажи на движущемся корабле (исследование)

> **Цель:** Ответить на вопрос — можем ли мы создавать **именных** NPC (капитан, пилот, инженер) с **активностями и маршрутами по точкам** на **движущемся** корабле, чтобы игрок, найдя корабль «Горгона», увидел именно **пилота Горгоны**, а не рандомный спавн.
>
> **Статус:** исследование (read-only), без кода. 2026-08-27.
> **Вывод:** **Да, возможно.** Движок уже умеет держать NPC на движущемся корабле. Не хватает **привязки** + **данных** (manifest команды, маршруты, role→seat), а не нового движка.

---

## 1. TL;DR

| Вопрос | Ответ |
|---|---|
| NPC на движущемся корабле — поддерживается? | **Да.** `NpcBrain`: `_platformCarryEnabled` (не сдувает с палубы), `_useParentingOnShips` (TrySetParent к NetworkObject корабля), `BeginRide`/`EndRide`/`DriveDeckNav` (proxy NavMeshAgent в nav-фрейме), `WarpProxyToNpc`. |
| Именной NPC несёт тот же AI-стек? | **Да.** Префаб `[Mira]` = `NpcController` + `NpcBrain` + `NpcSocialBrain` + `NpcTarget`/`NpcAttacker` + `NetworkObject`/`NetworkTransform` + `Billboard`. |
| Сейчас на палубе Горгоны кто? | **Рандомный гоблин-страж.** `NpcSpawner` → `NpcSpawner_ship_deck.asset` → `npcPrefab: Npc_Goblin 2`, `behaviorType: 2`, `isGuard: 1`, `maxAliveCount: 20`, `patrolWaypoints: []`. |
| Именной пилот может сесть в штурвал? | **Нет.** `PilotSeatController.TryBoard(clientId)` — stub («для будущего NPC/AI»). |
| Что не хватает? | 6 пробелов (§4). |
| Что делать? | 6 шагов (§6), по возрастанию. |

---

## 2. Инвентаризация компонентов

| Компонент | Путь | Ключевое |
|---|---|---|
| **NpcBrain** | `Assets/_Project/Scripts/AI/NpcBrain.cs` | FSM (Idle/Chase/Attack/Dead/Surrendered). **Уже умеет быть на движущемся корабле:** `_platformCarryEnabled`, `_useParentingOnShips`, `BeginRide`/`EndRide`/`DriveDeckNav` (proxy NavMeshAgent в nav-фрейме), `WarpProxyToNpc` (SamplePosition). Кэширует `_npcId` из `NpcController.Definition.npcId`. |
| **NpcSocialBrain** | `Assets/_Project/Scripts/AI/NpcSocialBrain.cs` | Patrol, flee, grudge, social triggers. `ApplySpawnerConfig`. |
| **ShipDeckNav** | `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` | Нав-фрейм палубы: `IsReady`, `WorldToDeckLocal`, `DeckLocalToNav`. `_registerServerOnly`, `_registerUnderShip`. |
| **NpcSpawner** | `Assets/_Project/Scripts/AI/NpcSpawner.cs` | Server-side спавн. `TrySpawnAtPoint` = `Instantiate` + `NetworkObject.Spawn()` + применение конфигов **до** Spawn (behavior/visual/social/loot). `SpawnMode` (Infinite/Finite/FiniteCycle), `activationRadius`, rate-limit, group formation. |
| **NpcDefinition** | `Assets/_Project/Quests/Npcs/NpcDefinition.cs` | SO: `npcId`, `displayName`, `faction`, `portrait`, `prefab`, `dialogTree`, quests, services, attitude. **Нет** `shipId`/`shipCrewRole`/`shipRoot`. |
| **NpcController** | `Assets/_Project/Quests/NpcController.cs` | Runtime-обёртка над `NpcDefinition`. |
| **PilotSeatController** | `Assets/_Project/Scripts/Ship/PilotSeatController.cs` | Место пилота. `PilotSeatType` (Pilot/Gunner/Engineer/Navigator). `TryBoard(clientId)` — **stub**. |
| **NpcShipController** | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | Scene-placed NetworkBehaviour на корне NPC-корабля: автопилот (NavTick), `NpcShipSchedule`, control authority (игрок vs NPC-автопилот). |

**Корабль «Горгона»** (`Assets/_Project/Prefabs/Ships/Горгона.prefab`) уже содержит: `NpcShipController` (автопилот + schedule), `NpcSpawner` (config `NpcSpawner_ship_deck.asset`), `ShipDeckNav`, `PilotSeatController`, `ShipRootReference`.

**Именной NPC «Mira»** (`Assets/_Project/Prefabs/NPC/[Mira] - DON'T DELETE DEFOULT.prefab`) несёт: `NpcController` + `NpcBrain` + `NpcSocialBrain` + `NpcTarget`/`NpcAttacker` + `NetworkObject`/`NetworkTransform` + `Billboard`.

---

## 3. Как сейчас спавнится экипаж Горгоны

Цепочка (всё уже в репо, проверено по guid):

```
Горгона.prefab
  └─ NpcSpawner (config = NpcSpawner_ship_deck.asset)
       └─ npcPrefab:      Npc_Goblin 2.prefab   (guid 6bd79345e39535a4c876dbecdf904793)
          behaviorType:   2  (enum BehaviorType, NpcBrain)
          isGuard:        1
          maxAliveCount:  20
          spawnChance:    1
          activationRadius: 147
          socialRole:     SocialRole_Guard.asset
          faction:        (guid da33f2fb82104954f84d56578574f344)
          patrolWaypoints: []        ← ПУСТО
          patrolPattern:  0
```

**Итог:** на палубу Горгоны спавнится **рандомный гоблин-страж** вокруг точки спавнера (`activationRadius: 147`), без маршрутов. Именного пилота/капитана нет.

---

## 4. Что не хватает (6 пробелов)

1. **Нет привязки именного NPC к кораблю.** В `NpcDefinition` **нет** `shipId`/`shipCrewRole`/`shipRoot`. Именной NPC — свободный агент, не привязанный к кораблю. *(grep `shipId|ShipId|shipPrefab|shipRoot` в `NpcDefinition.cs`/`NpcController.cs` → 0 совпадений.)*
2. **NpcSpawner спавнит рандомный префаб.** Конфиг Горгоны → `Npc_Goblin 2`. Именной пилот/капитан не спавнится.
3. **Нет маршрутов по точкам.** `patrolWaypoints: []`. У именных NPC маршруты не заданы.
4. **`PilotSeatController.TryBoard` — stub.** Комментарий «для будущего NPC/AI». Именной пилот не садится в штурвал; нет маппинга role→seat.
5. **Нет ShipCrew manifest (SO/скрипт).** Нет единого объекта «команда корабля» (роли + именные NPC + waypoint + seat). Всё разбросано: `NpcSpawnerConfig` + `NpcDefinition` + `PilotSeat`.
6. **SpawnMode для экипажа не используется.** `Finite`/`FiniteCycle` есть, но режим «спавнить фиксированный набор ИМЕННЫХ NPC при спавне корабля» не реализован.

---

## 5. Сложности

- **Networking.** Именные NPC — `NetworkObject`. При спавне корабля (scene-placed через `ScenePlacedObjectSpawner`) они должны спавниться **как часть корабля** (`destroyWithScene`), быть `_parentedToShip` (TrySetParent). Механизм уже есть в `NpcBrain.BeginRide`, но нужен **триггер «спавн при спавне корабля»**, а не «по подходу игрока» (сейчас `NpcSpawner` спавнит по `activationRadius` вокруг точки).
- **Waypoint в DeckLocal.** `patrolWaypointMarkers` — `Transform[]` (world). Для движущегося корабля маршруты должны быть в **DeckLocal** (дети палубы), иначе NPC «уедет» при перемещении. Конвертация уже есть: `ShipDeckNav.WorldToDeckLocal`/`DeckLocalToNav`.
- **Role→seat маппинг.** пилот→штурвал (`PilotSeat`), капитан→рубка, инженер→двигатель. Нужна таблица роль→(seat + waypoint).
- **Identity.** `_npcId` уже кэшируется из `NpcController.Definition.npcId` — работает. Но для «пилота Горгоны» нужен уникальный id, связанный с кораблём (`gorgona_pilot_01`), чтобы квест/attitude/диалог били в **конкретного** персонажа, а не в «гоблина».

---

## 6. План реализации (по возрастанию)

1. `NpcDefinition`: добавить `shipCrewRole` (enum Captain/Pilot/Gunner/Engineer/Navigator/None) + `shipId` (string).
2. Новый SO `ShipCrewDefinition` — manifest команды: `ShipCrewMember[]` { `role`, `NpcDefinition` ref, waypoint (DeckLocal), `PilotSeat` ref }.
3. Новый `ShipCrewSpawner` (или расширение `NpcSpawner`): при `OnNetworkSpawn` корабля спавнит **именных** NPC из `ShipCrewDefinition`, `TrySetParent` к палубе, назначает role→seat/waypoint.
4. Реализовать `PilotSeatController.TryBoard(npcId)` — именной пилот садится в штурвал.
5. Заполнить `patrolWaypoints` Горгоны (маркеры-дети палубы в DeckLocal).
6. **Тест:** спавн Горгоны → на палубе именные капитан+пилот (не гоблин), пилот в штурвале, патрулируют по точкам; игрок заходит → видит «пилот Горгоны», а не рандом.

---

## 7. История

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-08-27 | Elite_NPC research | Первый анализ: движок поддерживает NPC на движущемся корабле; 6 пробелов; план из 6 шагов. Без кода. |
