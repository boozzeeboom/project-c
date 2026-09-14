# T-FO06AA — runtime capture 02 after respawn-writer instrumentation

Дата: 2026-09-11.

## Источник

Точечная проверка Unity Console через Unity MCP после пользовательского Play Mode capture.
Полный лог не выгружался.

Окно capture:

```text
2026-09-11T23:53:47 +05:00
```

## Initial spawn gate

На `frame=171` подтверждено:

```text
spawn.NetworkSpawn(... armed=True position=[39992.00, 1.00, 40000.00])
physics.SyncTransforms.begin(baseline=True)
physics.SyncTransforms.end(baseline=True)
baseline.ActorApplied
spawn.InitialGateReleased(... position=[39992.00, 1.00, 40000.00])
baseline.AcknowledgeApplied
baseline.AdapterReady(status=Ready)
```

В точечной выборке снова не найдены отдельные строки `spawn.InitialGateArmed`, `spawn.FactoryPosePrepared` и `player.FixedUpdate.blocked`; ранняя часть lifecycle остаётся только частично наблюдаемой.

## Установленный порядок anomaly

На `frame=181` зафиксирована полная последовательность:

```text
player.FixedUpdate.begin(pos=[39992.04, -7.50, 40000.00])
ngo.NetworkTick(tick=65 ...)
player.Update.begin(position=[39992.04, -7.50, 40000.00])
player.Update.beforeProcessMovement(position=[39992.04, -7.50, 40000.00])
movement.CharacterController.Move.before(motion=[0.00, -0.52, 0.00] ...)
movement.CharacterController.Move.after(pos=[39992.04, -8.02, 40000.00] ...)
respawn.Update.begin(owner=0 y=-8.025 deathY=0.000 ...)
respawn.FallThresholdReached(... elapsed=0.526 delay=0.500)
respawn.PerformRespawn.begin(... position=[39992.04, -8.02, 40000.00])
respawn.TeleportRpc.begin(... target=[39992.00, 2502.77, 40000.00])
respawn.TeleportRpc.beforePositionWrite(... controllerEnabled=False)
respawn.TeleportRpc.afterPositionWrite(... position=[39992.00, 2502.77, 40000.00])
respawn.TeleportRpc.afterPhysicsSync(... controllerEnabled=True)
respawn.TeleportRpc.end(... position=[39992.00, 2502.77, 40000.00])
```

## Вывод по writer

Переход `y≈-8.02 → y≈2502.77` является подтверждённым `PlayerRespawnTracker.TeleportToClientRpc` после server-side fall detection.

Это не controlled rebase и не неизвестный competing writer:

- `CharacterController.Move` завершает движение до respawn writer;
- `respawn.Update` видит `y=-8.025` и превышение `0.5 s`;
- `PerformRespawn` выбирает target `(39992.00, 2502.77, 40000.00)`;
- direct position write происходит в `TeleportRpc.afterPositionWrite`;
- `Physics.SyncTransforms` выполняется внутри teleport path.

На следующем `frame=182` уже видно:

```text
player.Update.begin(position=[39992.00, 2502.77, 40000.00])
player.Update.beforeProcessMovement(position=[39992.00, 2502.77, 40000.00])
movement.CharacterController.Move.after(pos=[39992.00, 2502.75, 40000.00])
respawn.Update.begin(y=2502.753 ...)
```

Это подтверждает, что следующий movement начинается уже после respawn на новой позиции.

## Scheduling note

В `frame=181` порядок был:

```text
FixedUpdate → NGO NetworkTick → Update/Move → respawn.Update/TeleportRpc → LateUpdate
```

Это объясняет, почему `FixedUpdate.begin` ещё видит отрицательную позицию, а `LateUpdate` уже работает с teleported position. Повторные `FixedUpdate`/`NetworkTick` остаются отдельной scheduling characteristic, но больше не нужны для объяснения скачка координаты.

## Warnings / errors

- Unity errors/exceptions в точечной выборке не найдены.
- Повторяется `Failed to create agent because it is not close enough to the NavMesh`.
- Эти warnings не связаны доказанно с player respawn writer.

## Решение

`T-FO06AA` initial spawn gate не является причиной и не должен расширяться до respawn suppression.

Историческая anomaly классифицируется как обычный server-authoritative fall respawn, который ранее ошибочно выглядел как unexplained writer, потому что в старом capture отсутствовала instrumentation `PlayerRespawnTracker`.

Следующий этап floating-origin должен быть отдельным: explicit user-controlled rebase driver и его ordered `rebase.*` markers. Повторять respawn instrumentation capture не требуется.

## Gate

```text
initialGlobalSpawnGate = PARTIAL_RUNTIME_CONFIRMATION
respawnWriterOrder = CONFIRMED
initialPlacementAnomaly = EXPLAINED_AS_FALL_RESPAWN
controlledRebase = NOT_OBSERVED
rollback = NOT_OBSERVED
runtimeRebaseReadiness = NOT_READY
```
