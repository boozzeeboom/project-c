# T-FO06Z — controlled rebase transaction boundary contract

Дата: 2026-09-11.

## 1. Назначение

Продолжить интеграцию после source audit `06Z_SOURCE_AUDIT_AND_TRIGGER_ABSENCE` безопасным runtime-independent этапом: зафиксировать reviewed boundary для explicit user-controlled trigger, ordered rebase phases и rollback phases.

Этап не подключает coordinator к игровому runtime и не изменяет Unity state.

## 2. Основание в проекте

Предыдущий audit подтвердил:

- `GlobalMotionRebaseCoordinator` останавливается на `Captured`;
- `Apply/Rebuild/Validate/Publish` не подключены;
- runtime trigger в проверенных исходных областях не найден;
- `UnityStateRollbackContract` валидирует evidence, но не выполняет native restore.

Поэтому сначала добавлен отдельный pure contract, а не runtime driver. Это исключает скрытый вызов старого `FloatingOriginMP`, player-only shift или общий `SetParent`.

## 3. Добавленный contract

Файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseTransactionContract.cs
```

Контракт содержит:

- `GlobalMotionRebaseTriggerRequest`;
- `GlobalMotionRebaseTriggerKind.UserControlled`;
- валидацию transaction ID, frame generation и bounded reason;
- строгую проверку разрешённых фазовых переходов;
- terminal-state classification;
- отдельные rollback transitions.

Автоматический threshold trigger намеренно не добавлен. До user-controlled proof нельзя допускать автоматическую mutation path.

## 4. Требуемая последовательность

```text
Idle
→ Requested
→ FramePrepared
→ Applied
→ PhysicsSynchronized
→ Validated
→ Published
→ Completed
```

Rollback после начала транзакции:

```text
FramePrepared/Applied/PhysicsSynchronized/Validated/Published
→ RollbackBegun
→ Restored
→ RollbackCompleted
```

До завершения capture/preparation допускается fail-closed `Aborted` или `Faulted` без выдачи success.

## 5. Границы контракта

Контракт не:

- ищет участников;
- вызывает `Transform`, `Rigidbody`, `NavMesh`, camera или NGO APIs;
- изменяет `GlobalMotionRebaseCoordinator`;
- публикует manifest/frame;
- создаёт runtime markers;
- утверждает, что rollback уже работает.

Таким образом, это только проверяемая state-machine boundary для следующей integration slice.

## 6. Проверка

Unity compile:

```text
No compile errors
```

Play Mode, scene/prefab save, runtime trigger, frame mutation, rollback и screenshots не выполнялись.

## 7. Gate

```text
transactionBoundaryContract = PASS
runtimeTrigger = NOT_CONNECTED
controlledRebase = INCONCLUSIVE
rollback = NOT_IMPLEMENTED
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
applyRebuildValidatePublishConnected = false
runtimeRebaseReadiness = NOT_READY
```

## 8. Следующий шаг

Следующая интеграция должна отдельно реализовать reviewed driver, который сможет принимать только `UserControlled` trigger и писать ordered markers. До появления concrete adapters и native rollback evidence нельзя подключать `Apply/Rebuild/Validate/Publish` к рабочему runtime.
