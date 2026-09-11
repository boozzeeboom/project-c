# T-FO06AA — respawn writer runtime instrumentation

Дата: 2026-09-11.

## Цель

Добавить узкую read-only instrumentation после source audit `06AA_RESPAWN_WRITER_SOURCE_AUDIT`, чтобы в одном следующем пользовательском Play Mode capture установить порядок между respawn writer, `NetworkPlayer.Update`, `CharacterController.Move`, `FixedUpdate` и NGO evidence.

Instrumentation не меняет movement semantics, authority, respawn targets, controller state или rebase behavior.

## Изменённые файлы

- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

## Добавленные runtime markers

### PlayerRespawnTracker

- `respawn.Update.begin`
- `respawn.FallThresholdReached`
- `respawn.PerformRespawn.begin`
- `respawn.ResetRespawningFlag`
- `respawn.TeleportRpc.begin`
- `respawn.TeleportRpc.beforePositionWrite`
- `respawn.TeleportRpc.afterPositionWrite`
- `respawn.TeleportRpc.afterPhysicsSync`
- `respawn.TeleportRpc.end`

Markers содержат frame/sequence через существующий `GlobalMotionRuntimeEvidenceProbe`, target/current position, owner/server/client flags, состояние `CharacterController` и fall timing.

### NetworkPlayer

- `player.Update.begin`
- `player.Update.beforeProcessMovement`
- `player.ShipExit.beforePositionWrite`
- `player.ShipExit.afterPositionWrite`
- `player.LegacyTeleport.begin`
- `player.LegacyTeleport.beforePositionWrite`
- `player.LegacyTeleport.afterPositionWrite`

Существующие markers `player.FixedUpdate.begin`, `movement.CharacterController.Move.before` и `movement.CharacterController.Move.after` сохранены.

## Compile verification

```text
check_compile_errors = No compile errors
```

Play Mode после instrumentation не запускался. Согласно рабочему процессу, следующий capture выполняет пользователь.

## Scope boundary

Эта правка:

- не является controlled rebase;
- не добавляет automatic trigger;
- не реализует rollback;
- не устанавливает frame fence между server и owner;
- не исправляет anomaly автоматически;
- не меняет initial spawn gate.

Она только добавляет evidence markers для подтверждения или опровержения порядка writers.

## Следующий пользовательский capture

Нужен один serial capture с включённым `T-FO06Y` probe. В pointwise MCP review искать последовательность:

```text
respawn.Update.begin
respawn.FallThresholdReached
respawn.PerformRespawn.begin
respawn.TeleportRpc.begin
respawn.TeleportRpc.beforePositionWrite
afterPositionWrite
player.Update.begin
player.Update.beforeProcessMovement
movement.CharacterController.Move.before/after
player.FixedUpdate.begin
```

Особенно важны frame и sequence относительно:

- `PlayerRespawnTracker.Client teleported`;
- `baseline.*`;
- `ngo.NetworkTick`;
- отрицательной позиции;
- следующего появления `y≈2502.77`.

## Gate

```text
runtimeInstrumentation = COMPILE_PASS
initialGlobalSpawnGate = PARTIAL_RUNTIME_CONFIRMATION
initialPlacementAnomaly = NEEDS_POST_INSTRUMENTATION_CAPTURE
writerOrder = NOT_YET_OBSERVED
controlledRebase = NOT_OBSERVED
rollback = NOT_OBSERVED
runtimeRebaseReadiness = NOT_READY
```
