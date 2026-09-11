# T-FO06Y — second user runtime capture follow-up

Дата: 2026-09-11. Источник: предоставленный пользователем Unity Console export от `2026-09-11 18:53:31`, `805` записей, `15 секунд` геймплея.

## 1. Результат capture

Второй предоставленный export также не содержит ни одной строки с префиксом:

```text
[T-FO06Y]
```

Фактическое число probe records: `0`.

Это повторно оставляет без runtime evidence следующие boundaries:

- `NetworkPlayer.FixedUpdate` и `CharacterController.Move`;
- grounding и platform carry;
- Animator/root-motion state;
- NGO tick, accepted control и baseline acknowledgement;
- `Physics.SyncTransforms`;
- camera owner, target, lag и collision history.

После двух последовательных пользовательских exports с `0` `[T-FO06Y]` причина всё ещё **INCONCLUSIVE**: по логам нельзя установить, выключен ли `Capture Enabled` на runtime clone или строки probe не попадают в экспорт.

## 2. Подтверждённые static/runtime startup boundaries

Второй запуск подтверждает:

```text
[T-FO06G] Native scene readiness: ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>
[T-FO06G] Spawn plan ready: client=0;frame=1;position=Global(39992, 1, 40000)
[T-FO06G] CompletePlacement finished: object=NetworkPlayer_GlobalPilot(Clone);ready=True;baseline=True
[T-FO06G] SpawnAsPlayerObject called: client=0;object=NetworkPlayer_GlobalPilot(Clone)
[T-FO06L] Local pilot player ready;startup menus hidden=2;origin=(0,0,0);local=(39992.00, 1.00, 40000.00)
```

Это подтверждает initial native/player startup, но не runtime rebase, post-rebase baseline continuity или jitter source.

## 3. Deck/passenger evidence

Наблюдаются `20` `ShipDeckNav:... Registered at ...`, `20` explicit attachment requests и `20` named crew spawns.

При этом повторяются сообщения:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Источники:

- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs:200`;
- `Assets/_Project/Scripts/AI/NpcBrain.cs:688`;
- `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs:154`.

Следовательно, valid `NavMeshDataInstance`, proxy-agent readiness, `isOnNavMesh`, completed attachment и passenger provenance остаются **INCONCLUSIVE**.

## 4. Additional warnings in this run

В начале запуска появилась новая отдельная ошибка целостности сцены:

```text
The referenced script (Unknown) on this Behaviour is missing!
```

Она зафиксирована как gameplay/scene warning и не связывается с floating-origin jitter без отдельной проверки объекта-владельца. Массовое исправление в рамках T-FO06Y не выполнялось.

Также наблюдались collision/damage сообщения кораблей, `PlayerTarget HP init FAILED after 20 retries`, отсутствующие Animator parameters `WorkVariant`/`Work` и прежние warnings по cargo/resource references. Их причинная связь с jitter этим export не доказана.

## 5. Admission decision

Повторный capture не закрыл ни один runtime proof gate:

```text
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
```

`Apply/Rebuild/Validate/Publish`, native frame mutation, live manifest, `FloatingOriginMP`, player-only shift и generic shared `SetParent` не подключались.

## 6. Следующий gate

Нужен ещё один пользовательский запуск, в котором после появления `NetworkPlayer_GlobalPilot(Clone)` необходимо явно включить `Capture Enabled`, затем убедиться по Console, что появились `[T-FO06Y]` строки, и только после этого экспортировать лог.

До получения records concrete adapters и runtime rebase остаются запрещёнными.
