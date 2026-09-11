# T-FO06AA — runtime capture 01

Дата: 2026-09-11.

## Источник

Точечная проверка Unity Console через Unity MCP после пользовательского Play Mode capture.
Полный лог не выгружался.

Ключевое окно: `2026-09-11T23:37:37–23:37:43 +05:00`.

## Проверенные маркеры

На `frame=137` найдено:

```text
spawn.NetworkSpawn(object=NetworkPlayer_GlobalPilot[Clone] armed=True position=[39992.00, 1.00, 40000.00])
physics.SyncTransforms.begin(baseline=True ...)
physics.SyncTransforms.end(baseline=True ...)
baseline.ActorApplied(...)
spawn.InitialGateReleased(... position=[39992.00, 1.00, 40000.00])
baseline.AcknowledgeApplied(...)
baseline.AdapterReady(status=Ready ...)
```

Это подтверждает, что `Armed=True` дошёл до `NetworkSpawn`, а release произошёл после baseline application и `Physics.SyncTransforms`.

В проверенном Unity Console buffer не найдены отдельные строки:

```text
spawn.InitialGateArmed
spawn.FactoryPosePrepared
player.FixedUpdate.blocked
```

Их отсутствие в точечном buffer не доказывает, что они не были записаны: ранние события могли выйти за пределы доступного окна или быть агрегированы до выбранной evidence-записи. Поэтому gate ordering до `NetworkSpawn` классифицируется как `PARTIAL`, а не как полный runtime proof.

## Anomaly result

Аномалия сохраняется после release initial gate.

На `frame=146` после уже выполненного `spawn.InitialGateReleased` наблюдается:

```text
player.FixedUpdate.begin(pos=[39992.04, -6.36, 40000.00] ...)
```

Эта строка повторяется многократно в одном frame. Затем:

```text
movement.CharacterController.Move.after(pos=[39992.05, -9.18, 40000.00] ...)
```

В том же runtime window присутствуют явные записи другого writer:

```text
[PlayerRespawnTracker] Respawning player 0 to index=0 pos=(39992.00, 2502.77, 40000.00)
[PlayerRespawnTracker] Client teleported to (39992.00, 2502.77, 40000.00)
```

После этого `frame=147` уже показывает:

```text
player.FixedUpdate.begin(pos=[39992.00, 2502.77, 40000.00] ...)
```

## Вывод

T-FO06AA initial spawn gate работает частично подтверждённо:

- `NetworkSpawn` видит `armed=True`;
- baseline и `Physics.SyncTransforms` происходят до `InitialGateReleased`;
- до release в доступном окне не наблюдается `FixedUpdate` игрока;
- однако `InitialGateArmed`, `FactoryPosePrepared` и `FixedUpdate.blocked` не попали в точечную выборку.

Главное: историческая последовательность `y≈1 → y=-7.41 → y≈2502.77` повторилась и после release initial gate. Поэтому initial spawn gate не является достаточным объяснением anomaly.

Точечные логи указывают на competing writer/respawn path:

```text
CharacterController.Move.after(... y=-9.18)
PlayerRespawnTracker.Client teleported ... y=2502.77
следующий FixedUpdate ... y=2502.77
```

`PlayerRespawnTracker` теперь является главным проверяемым кандидатом для следующего source/runtime audit. Это ещё не доказывает, что именно он единственный writer: порядок `FixedUpdate`, respawn callback и NGO/network correction внутри кадра требует отдельной instrumentation.

## Отсутствие controlled rebase

В проверенном окне не найдены `rebase.*`, rollback или `baseline.ActorApplied` между падением до отрицательной высоты и переходом к `y≈2502.77`.

Следовательно, переход не классифицируется как controlled rebase.

## Warnings / errors

- Unity errors/exceptions в точечной выборке не найдены.
- Повторяется warning `Failed to create agent because it is not close enough to the NavMesh`.
- `20/20` deck registrations и `20/20` passenger attachment entries остаются видимыми как `ready=True`, `proxy=True`, `onNav=True`, но NavMesh warning сохраняет эту часть evidence в статусе `INCONCLUSIVE`.

## Gate

```text
initialGlobalSpawnGate = PARTIAL_RUNTIME_CONFIRMATION
initialPlacementAnomaly = REPRODUCED
likelyWriterCandidate = PlayerRespawnTracker / respawn-or-correction path
controlledRebase = NOT_OBSERVED
rollback = NOT_OBSERVED
runtimeRebaseReadiness = NOT_READY
```

## Следующий этап

Не повторять тот же baseline/movement capture.
Выполнить read-only source audit и узкую instrumentation-проверку `PlayerRespawnTracker`, его respawn point, callback order и всех вызовов teleport/set-position для `NetworkPlayer_GlobalPilot(Clone)`. Отдельно сохранить frame/timestamp correlation с `CharacterController.Move` и NGO correction.
