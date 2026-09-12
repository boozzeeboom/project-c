# T-FO06AQ — pointwise Play Mode evidence follow-up

Дата: 2026-09-12.

## 1. Назначение

Этот документ фиксирует частичный Host/Play Mode capture, полученный после `T-FO06AQ`. Он является observational evidence-report и не закрывает comprehensive review, participant admission, runtime installation или controlled rebase readiness.

Capture рассмотрен точечно, без загрузки полного Console Log. Код, сцены, префабы, `BootstrapScene`, `GlobalMotionNativeAdapterSet`, `FullTransaction` и runtime adapters не изменялись.

## 2. Контекст capture

- Unity Editor: Play Mode.
- Сцена: `Assets/_Project/Scenes/BootstrapScene.unity`.
- Compilation: `hasCompilationErrors=false`.
- Host capture; peer-agreement package отсутствует.
- Стабильный binding: `4780900133407987940/94/1/1/2`.

## 3. Подтверждённые наблюдения

На `frame=885` подтверждена initial baseline transition:

```text
ngo.ControlAccepted(revision=1 active=True sequence=0)
→ physics.SyncTransforms.begin/end(baseline=True role=Authority)
→ baseline.ActorApplied
→ spawn.InitialGateReleased
→ baseline.AcknowledgeApplied(revision=1 sequence=0)
→ baseline.AdapterReady(status=Ready)
```

Также наблюдались:

- `baselinePlaced=True`;
- NGO ticks минимум до `tick=116`;
- продолжение control revisions/sequences (`revision=1`, затем `2` до `29` и `3` до `55`);
- неизменный binding во время движения и jump;
- controlled movement примерно на `frames=942–970`;
- jump на `frames=971–984`, включая `jump=True`, `grounded=True → False` и вертикальную скорость `17.89`;
- camera ownership/history: `ThirdPersonCamera_0`, `activeOwner=True`, target `NetworkPlayer_GlobalPilot(Clone)`;
- `20/20` зарегистрированных deck entries с `reg=True; instance=True; ready=True`;
- `20/20` passenger/proxy records с `proxy=True; onNav=True; navActive=True`.

## 4. Respawn classification

На `frame=901` подтверждена server-authoritative fall-respawn sequence:

```text
movement.CharacterController.Move.after(pos.y=-7.33)
→ respawn.FallThresholdReached(elapsed=0.501 delay=0.500)
→ respawn.PerformRespawn.begin
→ respawn.TeleportRpc.beforePositionWrite(controllerEnabled=False)
→ respawn.TeleportRpc.afterPositionWrite(pos.y=2502.77)
→ respawn.TeleportRpc.afterPhysicsSync(controllerEnabled=True)
```

После этого `frame=902` начинался около `y=2502.77`, а к `frames=913–970` состояние снова было grounded/stable. В этой последовательности отсутствуют `rebase.*` markers. Переход классифицирован как `PlayerRespawnTracker` fall-respawn, а не controlled rebase или неизвестный baseline writer.

## 5. Scheduling observation

В одном rendered frame наблюдались несколько `FixedUpdate` и несколько `NetworkTick`, в частности на `frames=886`, `937`, `951` и `981`.

Это подтверждает только observation порядка вызовов. Без фактической rebase transaction capture оно не доказывает корректный порядок rebase относительно NGO tick и physics step.

## 6. Отрицательные доказательства

В доступном capture не наблюдались:

```text
rebase.*
rollback.*
manifest publisher/server/peer digest-count agreement
participant admission
post-rebase binding transition
native NetworkBaseline adapter installation
```

Не наблюдались также `Apply`, `Rebuild`, `Validate`, `Publish`, `Completed`, rollback restore или peer-agreement markers.

`adapter=Ready` и `baselinePlaced=True` относятся только к существующему initial-baseline pipeline. Они не доказывают наличие или готовность native `NetworkBaseline` adapter и не подтверждают post-rebase continuity.

## 7. Full-log pointwise audit

Источник: `Q:\Project-c_logs\01.txt`.

Точечный PowerShell-аудит полного файла, ограниченный строками с `[T-FO06Y]`, дал:

- `7995` строк capture;
- диапазон rendered frames `885–8879`;
- `rebase` matches: `0`;
- `rollback` matches: `0`;
- `manifest` matches: `0`;
- `admission` matches: `0`;
- `Exception` matches: `0`;
- `Error` matches: `0`;
- `Failed to create agent because it is not close enough to the NavMesh`: `247`.

Первичная baseline-последовательность найдена в source line `3742`, где присутствуют `ControlAccepted`, `SyncTransforms`, `ActorApplied`, `InitialGateReleased`, `AcknowledgeApplied` и `AdapterReady` при том же binding. Respawn-последовательность найдена в source line `3867`; jump — в source line `4342`.

В полном capture сохраняется scheduling anomaly: например, на `frame=886` обнаружены `16` `FixedUpdate.begin` и `24` `NetworkTick`; на поздних кадрах повторные callbacks также продолжаются, например `frame=8873` (`2/2`), `frame=8874` (`3/2`) и `frame=8875` (`2/1`). Это подтверждает только повторение callbacks внутри rendered frame и не доказывает корректный rebase ordering.

Поздний sampled state на `frame=8630` показывает `revision=191`, `seq=5064`, а поздние frames сохраняют тот же binding и `baseline.AdapterReady`; это обычное initial-baseline/control stream continuation, а не post-rebase evidence.

## 8. Gate result

```text
initialBaseline              = CONFIRMED
networkBaselineObservation   = PARTIAL_CONFIRMED
networkTickActivity          = OBSERVED
playerMovement               = CONFIRMED
jump                         = CONFIRMED
cameraOwnershipHistory       = OBSERVED
shipDeckNavPassengerState    = OBSERVED
respawnWriter                = EXPLAINED_AS_SERVER_AUTHORITATIVE_FALL_RESPAWN
controlledRebase             = NOT_OBSERVED
postRebaseContinuity         = NOT_PROVEN
rollback                     = NOT_OBSERVED
liveManifestPublication      = NOT_PROVEN
participantAdmission         = NOT_READY
runtimeInstallation          = NOT_CONNECTED
runtimeRebaseReadiness       = NOT_READY
```

## 9. Решение и границы продолжения

Этот capture можно использовать только как partial/observational follow-up к `T-FO06AQ`. Он не авторизует `GlobalMotionRebaseRuntimeDriver`, не создаёт readiness bundle и не даёт права изменять `FullTransaction` или подключать `GlobalMotionNativeAdapterSet`.

Следующими обязательными evidence остаются peer manifest digest/count agreement, participant admission, controlled rebase, post-rebase binding/baseline continuity и rollback capture/restore. `ShipDeckNav` остаётся отдельным deferred-registration lifecycle gate.

Runtime rebase readiness остаётся **NOT READY**.
