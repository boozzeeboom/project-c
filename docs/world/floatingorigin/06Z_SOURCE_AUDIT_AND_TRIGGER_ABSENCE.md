# T-FO06Z — source audit: controlled-rebase trigger and runtime boundary

Дата: 2026-09-11.

## 1. Назначение

Зафиксировать результат продолжения T-FO06Z после второго runtime capture: проверить по исходникам, существует ли уже интегрированный runtime trigger для controlled rebase и подключены ли фазы `Apply/Rebuild/Validate/Publish`. Этап документационный и read-only. Код, сцены, префабы, NavMesh и runtime configuration не изменялись.

## 2. Проверенная область

Прямой поиск выполнен по:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/*.cs`;
- `Assets/_Project/Scripts/**/*.cs` по идентификаторам coordinator и participant lifecycle;
- существующим отчётам `docs/world/floatingorigin` по словам `trigger`, `rebase`, `rollback`, `shutdown` и фазовым маркерам.

Дополнительно прочитаны:

- `GlobalMotionRebaseCoordinator.cs`;
- `GlobalMotionRuntimeEvidenceProbe.cs`;
- `UnityStateRollbackContract.cs`.

Изолированный project-audit через Unity exploration agent завершился `network error`, поэтому итог основан на прямом source search/read, а не на результате этого агента.

## 3. Фактическое состояние coordinator

`GlobalMotionRebaseCoordinator` содержит фазовую последовательность:

```text
Idle → Requested → Frozen → Preflighted → Captured
```

И failure/cleanup состояния:

```text
Aborted
Faulted
```

`IGlobalMotionRebaseParticipant` уже объявляет методы:

```text
TryPreflight
TryCapture
TryApply
TryRebuild
TryValidate
TryRestore
```

Но реализация `TryPrepare` вызывает только:

1. `TryFreeze` gate;
2. `TryPreflight` каждого закрытого участника;
3. `TryCapture` каждого участника;
4. переход в `Captured`.

После `Captured` нет вызовов `TryApply`, `TryRebuild` или `TryValidate`. Единственный runtime lifecycle, использующий `TryRestore`, находится в `TryAbort`; это cleanup для уже захваченных snapshot, а не доказательство штатного rollback после начала применения.

Координатор не изменяет:

- `Transform` или `Rigidbody`;
- NavMesh или deck proxy;
- camera history;
- NGO baseline/control;
- опубликованный frame.

Следовательно, текущий coordinator остаётся preparation/capture/abort skeleton, а не runtime rebase implementation.

## 4. Runtime probe и фазовые markers

`GlobalMotionRuntimeEvidenceProbe` остаётся dormant и имеет `_captureEnabled = false` по умолчанию. Его существующие markers относятся к baseline, physics, NGO tick/control и camera/player/deck/passenger sampling.

В исходниках probe не найдено интегрированной последовательности:

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

Найденные строки `TryApply` относятся к другим контрактам/буферам либо к interface-level декларации и не образуют user-controlled rebase trigger. Revision transitions `1/2/20/21` из последнего capture также не классифицируются как controlled rebase evidence.

## 5. Rollback boundary

`UnityStateRollbackContract` является runtime-independent validator. Он проверяет уже предоставленное evidence envelope:

- transaction и participant identity;
- snapshot identity и frame generation;
- coverage `Transform/Rigidbody/ShipDeckNav/CameraHistory/NetworkBaseline`;
- attempted/successful restore;
- participant identity restoration;
- reverse rollback order;
- complete restored coverage.

Контракт не захватывает и не восстанавливает Unity state. Поэтому отсутствие `rollback.Begin`, `rollback.Restore`, `rollback.Complete` и native restore evidence в последнем capture остаётся ожидаемым при текущем уровне интеграции.

## 6. Вывод

По прямому source audit интегрированный runtime trigger для controlled rebase не найден. Это не означает, что поиск доказал отсутствие любого возможного внешнего вызова во всём проекте: проверка ограничена перечисленными исходными областями и точными lifecycle identifiers; exploration-agent audit завершился ошибкой транспорта.

Тем не менее, проверенная runtime boundary однозначна:

```text
coordinator = preparation/capture/abort only
apply = NOT CONNECTED
rebuild = NOT CONNECTED
validate = NOT CONNECTED
publish = NOT CONNECTED
runtime trigger = NOT FOUND IN AUDITED SOURCES
native rollback = NOT IMPLEMENTED
```

## 7. Gate decision

```text
sourceAudit = PASS_WITH_SCOPE_LIMIT
runtimeTrigger = NOT_FOUND_IN_AUDITED_SOURCES
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

## 8. Следующий шаг

Не подключать concrete adapters, live manifest, participant admission или `Apply/Rebuild/Validate/Publish` только на основании этого аудита. Следующий этап требует отдельного reviewed runtime boundary: explicit user-controlled trigger, ordered phase markers и native Unity rollback evidence. После этого возможен новый serial Play Mode capture, который выполняет пользователь.
