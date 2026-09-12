# T-FO06AT — runtime transaction execution and rollback boundary

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AT` — следующий integration slice после `T-FO06AS`. Частичный пользовательский capture не доказал readiness, поэтому этот этап не устанавливает runtime driver и не создаёт concrete Unity adapters. Он закрывает только orchestration boundary между уже существующим driver, coordinator и будущим adapter implementation.

## Изменения

Изменены файлы:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeDriver.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseCoordinator.cs
```

### Ordered execution

После успешного `GlobalMotionRebaseCoordinator.TryPrepare(...)` driver теперь вызывает adapter boundary последовательно. После `TryPublish` coordinator выполняет explicit `TryCommit`, который releases freeze gate и очищает captured transaction state только после успешной публикации:

```text
Requested
→ FramePrepared
→ Captured
→ TryApply
→ Applied
→ TryRebuild
→ PhysicsSynchronized
→ TryValidate
→ Validated
→ TryPublish
→ Published
→ Completed
```

`TryRebuild` является только orchestration boundary для physics synchronization phase. Реальная работа Transform/Rigidbody/NavMesh/camera/NGO и публикация frame остаётся ответственностью concrete adapter и пока не подключена. `TryCommit` не выполняет дополнительную spatial mutation; он закрывает coordinator transaction и освобождает freeze gate.

### Rollback execution

В `IGlobalMotionRebaseRuntimeDriverAdapter` добавлен explicit метод:

```text
TryRestore(GlobalMotionRebaseRequest request, out string error)
```

При отказе после capture driver выполняет:

```text
RollbackBegun
→ adapter.TryRestore
→ coordinator.TryAbort
→ Restored
→ RollbackCompleted
```

При ошибке восстановления результат становится `Faulted`. При успешном rollback installation intent инвалидируется и пишет `InstallationInvalidated`. `TryReset` теперь принимает `RollbackCompleted` и возвращает lifecycle в `Idle` после coordinator reset.

## Fail-closed ограничения

Этап не:

- создаёт concrete adapter instances;
- устанавливает `GlobalMotionRebaseRuntimeDriver` в `BootstrapScene`;
- обнаруживает или публикует participants/manifest;
- вызывает `TryRequestUserControlled` в runtime;
- изменяет Transform, Rigidbody, NavMesh, camera или NGO state;
- запускает Play Mode;
- превращает частичный capture из `T-FO06AS` в readiness evidence.

Без proven readiness bundle и installation authorization `TryRequestUserControlled` остаётся заблокированным. Без concrete adapter pipeline runtime rebase фактически не запускается.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
Play Mode = not run by this stage
runtimeRebaseReadiness = NOT_READY
```

## Итог

Orchestration contract для успешных transaction phases и rollback теперь подключён к driver API, но runtime integration остаётся dormant до отдельного этапа concrete adapter implementation и нового user-controlled capture. Частичный capture `T-FO06AS` не изменяет этот статус.
