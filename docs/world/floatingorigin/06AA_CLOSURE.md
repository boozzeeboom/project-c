# T-FO06AA — closure report

Дата: 2026-09-11.

## Итог этапа

T-FO06AA завершён как diagnosis-driven initial spawn and respawn-writer investigation.

Этап установил две отдельные границы:

1. `GlobalMotionSpawnLatch` fail-closed gate защищает initial global spawn до exact baseline release.
2. Историческая последовательность `y≈1 → y≈-8 → y≈2502.77` объясняется не неизвестным floating-origin writer, а штатным server-authoritative fall respawn через `PlayerRespawnTracker.TeleportToClientRpc`.

## Runtime evidence

В capture 02 на `frame=181` подтверждён порядок:

```text
NetworkPlayer.FixedUpdate
NGO NetworkTick
NetworkPlayer.Update
CharacterController.Move → y=-8.02
PlayerRespawnTracker.Update
FallThresholdReached after 0.526 s
PerformRespawn
TeleportToClientRpc → target y=2502.77
Physics.SyncTransforms
```

На `frame=182` игрок уже начинает movement с `y=2502.77`.

Это доказывает, что переход к `y≈2502.77` является respawn target write, а не controlled rebase и не необъяснимым competing writer.

## Что остаётся частично подтверждённым

В доступном pointwise buffer не появились отдельные ранние markers:

```text
spawn.InitialGateArmed
spawn.FactoryPosePrepared
player.FixedUpdate.blocked
```

При этом `spawn.NetworkSpawn(... armed=True)` и корректный порядок baseline → `spawn.InitialGateReleased` подтверждены. Поэтому initial gate имеет статус `PARTIAL_RUNTIME_CONFIRMATION`, а не полный lifecycle proof.

## Решение по instrumentation

Respawn-writer instrumentation сохранена как dormant evidence instrumentation поверх существующего `GlobalMotionRuntimeEvidenceProbe`. Она не меняет gameplay semantics и позволяет повторно анализировать аналогичные writer-order случаи без отдельной логики teleport/rebase.

## Scope boundary

T-FO06AA не реализует:

- controlled rebase;
- automatic threshold trigger;
- runtime `Apply/Rebuild/Validate/Publish`;
- native rollback;
- participant admission;
- live manifest publication;
- runtime rebase readiness.

`runtimeRebaseReadiness` остаётся `NOT_READY`.

## Следующий этап

Не повторять T-FO06AA respawn capture.
Следующий этап floating-origin — отдельная reviewed integration explicit user-controlled rebase driver с ordered `rebase.*` markers, native adapters и fail-closed readiness gates.
