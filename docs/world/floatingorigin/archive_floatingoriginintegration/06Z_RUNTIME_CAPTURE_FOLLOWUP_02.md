# T-FO06Z runtime capture follow-up 02 — pointwise Unity MCP review

Дата: 2026-09-11.

## 1. Scope

Пользователь выполнил один Play Mode-плейтест в canonical `BootstrapScene`: движение, прыжок и завершение Play Mode. Полный Console Log не передавался в контекст; проверка выполнена точечно через Unity MCP по состоянию Editor и отдельным фильтрам Console.

Во время проверки не изменялись код, сцены, префабы, NavMesh или runtime configuration.

## 2. Editor state after test

После остановки Play Mode Unity MCP подтвердил:

```text
playMode=false
activeAssetPath=Assets/_Project/Scenes/BootstrapScene.unity
hasCompilationErrors=false
selectedGameObject=NetworkPlayer_GlobalPilot
```

## 3. Baseline

На `frame=162` подтверждены в правильном порядке:

```text
physics.SyncTransforms.begin
physics.SyncTransforms.end
baseline.ActorApplied
baseline.AcknowledgeApplied
baseline.AdapterReady
```

Binding identity:

```text
5038395829736550539/94/1/1/2
```

Baseline result: **PASS**.

## 4. Movement and jump

Movement evidence присутствует в `[T-FO06Y]` records. Player position менялась от baseline около `(39992.00, 2502.17, 40000.00)` до sampled post-movement состояния около `(39988.30, 2502.17, 40004.43)`.

Jump подтверждён через вертикальную CharacterController evidence, хотя отдельной строки с именем `jump` probe не пишет:

```text
frame=528: grounded=False, velocity.y=6.07, motion.y=0.22
frame=529: grounded=False, velocity.y=5.21, motion.y=0.11
```

Позже sampled records вернулись к:

```text
grounded=True
ccGrounded=True
ccEnabled=True
```

Movement/jump result: **PASS по instrumented evidence**.

## 5. Revision and binding continuity

В доступных sampled records наблюдались revisions `1`, `2`, `20` и `21`. Binding identity оставался неизменным:

```text
5038395829736550539/94/1/1/2
```

`adapter=Ready` и `baselinePlaced=True` сохранялись в ранних и поздних snapshots.

Это подтверждает сохранение player binding при runtime revision transitions, но не доказывает полный rebase transaction.

## 6. Deck and passenger state

В sampled snapshots подтверждены:

```text
decks=20
passengerCount=20
```

Для всех 20 deck entries:

```text
reg=True
instance=True
ready=True
data=NavMesh-DeckNavSurface
```

Для всех 20 passengers:

```text
active=True
proxy=True
onNav=True
navActive=True
```

Deck/passenger result: **PASS с qualification по NavMesh warnings**.

## 7. Camera and scheduling qualification

В ранних records присутствуют `camera.LateUpdate.begin/end` с active owner и target `NetworkPlayer_GlobalPilot(Clone)`.

В поздних records примерно с `frame=873` наблюдается:

```text
camera.LateUpdate.skip(... cursor=None)
```

Поэтому полная camera continuity после позднего участка остаётся **INCONCLUSIVE**.

Также наблюдались повторные `FixedUpdate` и несколько `NetworkTick` в одном frame. Это сохраняется как `RUNTIME_ORDERING_INCONCLUSIVE`, без вывода о двойной физической симуляции.

## 8. Rebase, shutdown and rollback

Фильтр Console по `rebase` не нашёл явных rebase markers. Revision transitions сами по себе не заменяют evidence фаз `APPLY/REBUILD/VALIDATE/PUBLISH`.

Фильтры `rollback`, `shutdown` и `disconnect` после остановки Play Mode не нашли явных rollback/shutdown records. Поэтому:

```text
controlledRebase = INCONCLUSIVE
rollback = NOT_OBSERVED
```

Остановка Play Mode подтверждена состоянием Editor, но штатный runtime rollback evidence отсутствует.

## 9. Warnings and tooling qualification

В Console многократно наблюдалось:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Позднее состояние 20/20 passengers оставалось готовым, поэтому предупреждение классифицировано как transient startup NavMesh warning и не считается устранённым.

Часть `get_unity_logs` запросов завершалась transport errors (`TCP connection lost`/timeout). Итог основан на успешно полученных `get_unity_editor_state` и `read_console` точечных snapshots; transport errors не классифицируются как игровые compile errors.

## 10. Gate decision

```text
baseline = PASS
movement = PASS
jump = PASS_INSTRUMENTED
shipDeckNav = PASS_WITH_TRANSIENT_NAVMESH_WARNINGS
passengerProvenance = PASS_WITH_TRANSIENT_NAVMESH_WARNINGS
bindingContinuity = PASS_SAMPLED
cameraContinuity = INCONCLUSIVE
controlledRebase = INCONCLUSIVE
rollback = NOT_OBSERVED
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
applyRebuildValidatePublishConnected = false
runtimeRebaseReadiness = NOT_READY
```

Итог: пользовательский capture закрыл baseline, movement/jump и deck/passenger evidence, но не закрыл controlled rebase transaction и rollback. Concrete adapters, live manifest и `Apply/Rebuild/Validate/Publish` остаются заблокированы.
