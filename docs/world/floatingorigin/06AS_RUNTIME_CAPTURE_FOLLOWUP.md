# T-FO06AS — runtime capture follow-up and readiness status

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AS` — serial capture-review этап после `T-FO06AR`. Цель — зафиксировать фактическое частичное runtime evidence через Unity MCP, не выдавая его за доказательство controlled rebase.

## Capture context

- Unity Editor: Play Mode active.
- Active scene: `Assets/_Project/Scenes/BootstrapScene.unity`.
- Selected runtime object: `NetworkPlayer_GlobalPilot`.
- Pilot prefab: `Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab`.
- Compilation: no compile errors.

## Confirmed evidence

В доступном пользовательском capture подтверждены:

- initial `spawn.NetworkSpawn` с `armed=True`;
- baseline order: `physics.SyncTransforms` → `baseline.ActorApplied` → `spawn.InitialGateReleased` → `baseline.AcknowledgeApplied`;
- stable binding `5032352563919142904/94/1/1/2`;
- `baseline.AdapterReady(status=Ready)` и `baselinePlaced=True`;
- NGO `NetworkTick`/`ControlAccepted` evidence;
- camera owner `ThirdPersonCamera_0`, target `NetworkPlayer_GlobalPilot(Clone)` и camera history samples;
- `decks=20` с `reg=True;instance=True;ready=True`;
- `passengerCount=20`; у наблюдавшихся passengers `proxy=True;onNav=True;navActive=True`;
- post-respawn return to `grounded=True;ccGrounded=True`.

## Respawn sequence

Capture повторил server-authoritative fall respawn:

```text
CharacterController.Move y=1.00 → y=-7.41
→ respawn.FallThresholdReached(elapsed=0.501, delay=0.500)
→ respawn.TeleportRpc target y=2502.77
→ next frame begins at y=2502.77
```

`y=2502.77` не является floating-origin rebase и не должен учитываться как такой.

## Not proven / blocked

- В capture отсутствовали `rebase.*` markers.
- Отсутствовали `Apply`, `Rebuild`, `Validate`, `Publish`, `Completed` и rollback markers.
- `GlobalMotionRebaseRuntimeDriver` не установлен в `BootstrapScene`.
- Concrete native adapter instances не созданы.
- Live manifest publication, peer digest/count agreement и participant admission не доказаны.
- Unity-state rollback не захвачен и не восстановлен.
- Runtime evidence package не может пройти `GlobalMotionRebaseUserControlledEvidenceReviewGate`.
- Повторяются предупреждения `Failed to create agent because it is not close enough to the NavMesh`; в текущей проверке зафиксировано 21 такое предупреждение. Положительные passenger records не закрывают полный NavMesh lifecycle.

## Gate result

```text
capture = PARTIAL_RUNTIME_EVIDENCE
baseline = CONFIRMED
player_movement = CONFIRMED
camera_history = PARTIAL_CONFIRMED
ship_deck_passenger = OBSERVED_BUT_NAVMESH_WARNING_REMAINS
controlled_rebase = NOT_RUN
rollback = NOT_PROVEN
runtimeRebaseReadiness = NOT_READY
```

## Verification

```text
Unity editor state = Play Mode active during review
compile errors = 0
rebase/Exception search = no matching entries
NavMesh warnings = 21 observed
scene/prefab/runtime driver/native adapter mutation = none
```

Этот отчёт фиксирует evidence-review результат и оставшиеся блокеры. Он не устанавливает runtime driver, не создаёт native adapters, не запускает rebase и не запечатывает evidence package.
