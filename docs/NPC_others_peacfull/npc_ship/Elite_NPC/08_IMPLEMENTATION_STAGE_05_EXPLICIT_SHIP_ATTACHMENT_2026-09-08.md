# Именная команда NPC на движущемся корабле — Этап 5: explicit ship attachment

**Дата:** 2026-09-08
**Тикет:** `T-CREW-11`
**Статус:** завершён; compile/static checks пройдены, Play Mode и host/client runtime ещё не запускались.
**Предыдущий этап:** `07_IMPLEMENTATION_STAGE_04_SHIP_CREW_SPAWNER_2026-09-08.md`

## Цель этапа

Добавить официальный explicit API в `NpcBrain` для fixed crew на движущемся корабле и переключить `ShipCrewSpawner` с прямого parenting на этот API. Attachment не должен зависеть от `_platformMask` и physics probe.

## Реализация

### `NpcBrain`

В `Assets/_Project/Scripts/AI/NpcBrain.cs` добавлены:

- `AttachToShipDeck(NetworkObject shipNetworkObject, ShipDeckNav deckNav = null)`;
- `DetachFromShipDeck()`;
- `IsExplicitShipAttachmentRequested`;
- `IsExplicitShipAttachmentActive`;
- внутренние поля explicit ship/`ShipDeckNav` state;
- `TickExplicitShipAttachment()`.

Explicit path:

1. принимает server-side `NetworkObject` корабля;
2. разрешает `ShipDeckNav` напрямую или через children корабля;
3. делает `NetworkObject.TrySetParent` к ShipRoot;
4. приостанавливает обычный `NavMeshAgent` transform drive;
5. ожидает `ShipDeckNav.IsReady` без создания ошибочного proxy;
6. после регистрации deck NavMesh создаёт proxy и выполняет `WarpProxyToNpc()`;
7. в `FixedUpdate()` использует deck proxy независимо от значения `_platformMask`;
8. при `DetachFromShipDeck()` освобождает parent/deck state и восстанавливает обычный agent drive.

Пока `ShipDeckNav` не готов, explicit request остаётся активным, physics-probe fallback не запускается, а proxy не создаётся.

### `ShipCrewSpawner`

В `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs`:

- добавлено разрешение `NpcBrain` и `ShipDeckNav`;
- создан helper `AttachMemberToShipDeck`;
- новый fixed crew после `NetworkObject.Spawn()` вызывает `NpcBrain.AttachToShipDeck`;
- повторно найденный existing crew member также проходит через тот же официальный API;
- прямой вызов `TrySetParent` из spawner удалён.

Таким образом, spawn lifecycle и explicit attachment используют один путь и сохраняют duplicate guard по `npcId`.

## Что намеренно не изменялось

- `NpcDefinition` не получил ship-specific assignment;
- NPC не добавляется в `ShipController._pilots` и не представляется fake `NetworkPlayer`;
- `NpcShipController` остаётся источником движения корабля;
- generic `NpcSpawner` на «Горгоне» пока не выключен — это Этап 8;
- deck-aware `Patrol`/activity destinations ещё не подключены — это Этап 6;
- pilot seat occupancy ещё не подключён — это Этап 7.

## Проверки

- `validate_script` для `NpcBrain.cs`: ошибок нет; сохранилось только существующее предупреждение о конкатенации строк в `Update()`;
- `validate_script` для `ShipCrewSpawner.cs`: diagnostics отсутствуют;
- `check_compile_errors`: `No compile errors`.

Play Mode, NGO host/client replication, фактическая регистрация `ShipDeckNav`, движение корабля и отсутствие смещения пилота ещё не проверялись.

## Acceptance

Статически выполнены требования этапа:

- официальный explicit attach/detach API создан;
- attachment не зависит от `_platformMask`;
- fixed crew spawner вызывает официальный API для нового и повторно найденного NPC;
- ожидание `ShipDeckNav.IsReady` выполняется до создания proxy;
- существующий generic platform carry path сохранён для NPC без explicit attachment.

Runtime acceptance остаётся открытым до пользовательского Play Mode прогона со screenshots.

## Следующий этап

**Этап 6 — deck-aware activities:** передать `Patrol` destinations в `NpcBrain`, расширить `DriveDeckNav()` для activity movement и проверить цикл `Patrol_01 → Patrol_02 → Patrol_03`.
