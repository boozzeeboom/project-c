# T-FO06AC — native adapter contract

Дата: 2026-09-11.

## Назначение тикета

`T-FO06AC` выбран как следующий свободный подэтап после T-FO06AB; существующего отчёта `06AC` в floating-origin документах не найдено.

Цель — зафиксировать concrete native adapter seam для пяти обязательных областей Unity state:

```text
Transform
Rigidbody
ShipDeckNav
CameraHistory
NetworkBaseline
```

Этап не подключает реальные Unity adapters и не изменяет runtime state.

## Реализация

Создан:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNativeAdapterContracts.cs
```

Добавлены:

- `GlobalMotionNativeAdapterCapability`;
- `GlobalMotionNativeAdapterDescriptor`;
- `IGlobalMotionNativeAdapter`;
- `GlobalMotionNativeAdapterSet`.

## Validation rules

Closed-world adapter set требует:

- явный `AdapterId`;
- непустое coverage;
- declared capabilities `Capture | Apply | Rebuild | Validate | Restore`;
- актуальный adapter (`IsCurrent`);
- native readiness (`NativeReady`);
- отсутствие duplicate adapter IDs;
- отсутствие overlapping coverage;
- полное покрытие `UnityStateRollbackCoverage.All`.

Set must be sealed before readiness validation. Unknown adapters не обнаруживаются и автоматически не принимаются.

## Scope boundary

Контракт:

- не вызывает методы adapter implementations;
- не захватывает Transform/Rigidbody/NavMesh/camera/network state;
- не применяет world shift;
- не выполняет rebuild или rollback;
- не подключён к `GlobalMotionRebaseRuntimeDriver`;
- не меняет participant admission, live manifest или runtime readiness.

`GlobalMotionRebaseRuntimeDriver` остаётся fail-closed на `native_apply_pipeline_not_connected`.

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode не запускался; runtime adapter instances в сцену не добавлялись.

## Gate

```text
nativeAdapterContract = IMPLEMENTED_COMPILE_PASS
nativeAdapterInstances = NOT_CONNECTED
participantAdmission = NOT_READY
liveManifestPublication = NOT_READY
nativeRollback = NOT_IMPLEMENTED
runtimeRebaseReadiness = NOT_READY
```

## Следующий этап

Связать adapter set с runtime driver только после подтверждения participant admission и live manifest publication. Реальные Unity adapters должны быть отдельными serial этапами и не добавляться одновременно для нескольких областей state.
