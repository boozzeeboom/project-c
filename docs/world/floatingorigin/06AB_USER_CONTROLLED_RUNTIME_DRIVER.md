# T-FO06AB — explicit user-controlled runtime driver boundary

Дата: 2026-09-11.

## Назначение тикета

`T-FO06AB` выбран как следующий свободный подэтап после завершённого `T-FO06AA`; отдельного занятого `06AB` в floating-origin документах не найдено.

Цель этапа — подключить explicit user-controlled request boundary к существующему closed-world coordinator без автоматического trigger и без преждевременного изменения Unity state.

## Реализация

Создан:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeDriver.cs
```

Добавлены:

- `IGlobalMotionRebaseRuntimeDriverAdapter` для будущих native adapters;
- `GlobalMotionRebaseRuntimeDriver` с explicit `TryRequestUserControlled`;
- validation `GlobalMotionRebaseTriggerRequest` с `UserControlled` kind;
- transaction identity через `Guid`;
- обязательные `frameGeneration` и bounded reason;
- связь с `GlobalMotionRebaseCoordinator.TryPrepare`;
- reset только после `Aborted` или `Faulted`;
- fail-closed отказ до native Apply/Rebuild/Validate/Publish.

## Ordered markers

Driver пишет через существующий `GlobalMotionRuntimeEvidenceProbe`:

```text
rebase.Requested
rebase.FramePrepared
rebase.AbortRequested
rebase.Aborted
rebase.Faulted
rebase.Reset
```

Если coordinator успешно доходит до текущего `Captured`, driver всё равно завершает транзакцию отказом:

```text
native_apply_pipeline_not_connected
```

Он не выдаёт `Applied`, `PhysicsSynchronized`, `Validated`, `Published` или `Completed` без concrete adapter.

## Граница безопасности

Этап намеренно не:

- подключает automatic threshold trigger;
- добавляет компонент в BootstrapScene;
- изменяет `Transform`, `Rigidbody`, NavMesh, camera или NGO baseline;
- вызывает `TryApply`, `TryRebuild`, `TryValidate` или `Publish` как успешные runtime operations;
- реализует native rollback;
- меняет runtime rebase readiness.

Таким образом, существующий проект остаётся fail-closed, а новый driver является boundary/runtime orchestration shell, а не завершённым rebase.

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode не запускался. Для проверки runtime markers требуется отдельный user-controlled capture после подключения concrete adapter; текущий driver без adapter не должен быть добавлен в сцену.

## Gate

```text
userControlledTriggerBoundary = IMPLEMENTED_COMPILE_PASS
runtimeDriver = CONNECTED_TO_COORDINATOR_PREPARE_ONLY
nativeApply = NOT_CONNECTED
nativeRollback = NOT_IMPLEMENTED
runtimeRebaseReadiness = NOT_READY
```

## Следующий этап

Подготовить concrete native adapter contract для `Transform`/`Rigidbody`/ShipDeckNav/camera/network baseline, не подключая его к runtime до закрытия participant admission и live manifest gates.
