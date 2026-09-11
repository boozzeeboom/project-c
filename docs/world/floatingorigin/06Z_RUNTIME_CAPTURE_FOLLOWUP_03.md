# T-FO06Z runtime capture follow-up 03 — post-playtest pointwise review

Дата: 2026-09-11.

## 1. Scope

Пользователь завершил очередной Play Mode capture в canonical `BootstrapScene`. Проверка выполнена точечно через Unity MCP без загрузки полного Console Log.

Во время анализа код, сцены, префабы, NavMesh и runtime configuration не изменялись.

## 2. Editor state after stop

```text
playMode=false
activeAssetPath=Assets/_Project/Scenes/BootstrapScene.unity
selectedGameObject=NetworkPlayer_GlobalPilot
hasCompilationErrors=false
```

## 3. Evidence window

MCP получил `41` записи `[T-FO06Y]` в доступном окне; первая часть содержит baseline и раннюю физику, последующие sampled records показывают стабильное состояние после восстановления позиции.

Binding оставался стабильным:

```text
4872018665147106836/94/1/1/2
```

### Baseline

На `frame=221` подтверждены:

```text
ngo.ControlAccepted(revision=1)
physics.SyncTransforms.begin/end
baseline.ActorApplied
baseline.AcknowledgeApplied
baseline.AdapterReady(status=WaitingForActors)
baseline.AdapterReady(status=Ready)
```

Baseline: **PASS**.

## 4. Initial spawn/physics anomaly

Сразу после baseline игрок начал падать с локального состояния около:

```text
frame=221: y=1.00
frame=222: y=-1.22
frame=225: y=-2.70
frame=230: y=-4.47
frame=234: y=-7.08
frame=235: y=-7.41 в Move.after
```

В том же `frame=235` sampled state уже показывает:

```text
playerPos.y=2502.77
velocity=(0,0,0)
grounded=False
```

В доступной evidence window нет соответствующего ordered marker `ActorApplied`, `SyncTransforms` или отдельного explicit rebase marker для перехода `y=-7.41 → y=2502.77`. Поэтому это классифицировано как:

```text
initialPlacementOrWriterAnomaly = INCONCLUSIVE
```

Это не считается controlled rebase и не доказывает корректность rollback.

## 5. Scheduling anomaly

В отдельных кадрах наблюдается несколько callbacks одного типа и пачки network ticks. Например, на `frame=222`:

```text
много player.FixedUpdate.begin
ngo.NetworkTick tick=25..46
```

Подобные повторения встречаются и позднее. Capture не позволяет доказать двойную физическую симуляцию или установить владельца записи, поэтому:

```text
runtimeSchedulingOrder = INCONCLUSIVE
```

## 6. Stable post-placement state

После скачка к `y≈2502.77` состояние стабилизировалось:

- на `frame=245` `ccGrounded=True`;
- на `frames=250..290` позиция удерживалась около `(39992.00,2502.17,40000.00)`;
- на `frames=291..300` движение изменило позицию примерно до `(39993.45,2502.17,40000.56)`;
- `adapter=Ready`, `baselinePlaced=True`;
- binding не изменился;
- camera owner и target оставались активными.

Movement: **PASS** после восстановления placement.

Jump: **NOT_OBSERVED** в доступном точечном окне; отдельного `velocity.y > 0` не найдено.

## 7. Deck/passenger state

Sampled records сохраняют:

```text
decks=20
passengerCount=20
```

Все sampled deck entries:

```text
reg=True
instance=True
ready=True
data=NavMesh-DeckNavSurface
```

Все sampled passengers:

```text
active=True
proxy=True
onNav=True
navActive=True
```

Это даёт:

```text
shipDeckNav = PASS_WITH_TRANSIENT_NAVMESH_WARNING
passengerProvenance = PASS_WITH_TRANSIENT_NAVMESH_WARNING
```

## 8. Rebase and rollback markers

Точечные фильтры Unity Console после остановки вернули:

```text
rebase = 0
rollback = 0
```

Также в доступном capture отсутствуют:

```text
rebase.Begin
rebase.FramePrepared
rebase.Apply
rebase.PhysicsSync
rebase.Validate
rebase.Publish
rebase.Complete
rollback.Begin
rollback.Restore
rollback.Complete
rollback.Failed
```

Причина теперь подтверждена архитектурой: transaction contract и coordinator ещё не подключены к runtime driver.

## 9. Errors and warnings

После остановки Play Mode compile errors отсутствуют. В Console были обнаружены только два MCP lifecycle messages о завершении client handlers; они не являются игровыми compile/runtime errors.

В capture сохраняется известное предупреждение:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Оно остаётся transient startup blocker/qualification и не считается устранённым состоянием `20/20 ready`.

## 10. Gate decision

```text
editorStopped = PASS
compile = PASS
baseline = PASS
movement = PASS_AFTER_PLACEMENT_RECOVERY
jump = NOT_OBSERVED
initialPlacementOrWriterAnomaly = INCONCLUSIVE
runtimeSchedulingOrder = INCONCLUSIVE
cameraOwnership = PASS_SAMPLED
shipDeckNav = PASS_WITH_TRANSIENT_NAVMESH_WARNING
passengerProvenance = PASS_WITH_TRANSIENT_NAVMESH_WARNING
controlledRebase = NOT_OBSERVED
rollback = NOT_OBSERVED
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
applyRebuildValidatePublishConnected = false
runtimeRebaseReadiness = NOT_READY
```

## 11. Decision to stop repeating the same capture

Этот capture закрывает повторную проверку baseline/movement/deck/passenger для текущей dormant evidence ветки. Повторять идентичный Play Mode capture до появления runtime driver больше не нужно и не следует: controlled rebase и rollback всё равно не могут появиться в логах без подключённого trigger/driver.

Обнаруженный initial placement/writer anomaly должен быть вынесен в отдельную implementation/debugging slice. Следующая работа по плану — не новый одинаковый capture, а reviewed runtime driver boundary с ordered markers и native rollback path, после чего потребуется один новый serial capture для проверки уже изменённого runtime.
