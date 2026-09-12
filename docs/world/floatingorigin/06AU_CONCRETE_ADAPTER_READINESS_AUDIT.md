# T-FO06AU — concrete adapter readiness audit

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AU` — комплексный gate-review после `T-FO06AT`. Цель — проверить, позволяет ли фактическое состояние проекта перейти от transaction orchestration к concrete Unity adapter implementation и runtime installation.

Проверка выполнена без запуска Play Mode, без изменения сцен и без runtime mutation.

## Reviewed scope

Проверены:

- `GlobalMotionRebaseRuntimeDriver.cs`;
- `GlobalMotionRebaseCoordinator.cs`;
- `GlobalMotionNativeAdapterContracts.cs`;
- `GlobalMotionRebaseRuntimeConnectionAuthorization.cs`;
- `GlobalMotionRebaseRuntimeInstallationBoundary.cs`;
- `GlobalMotionPoseAdapter.cs`;
- canonical `Assets/_Project/Scenes/BootstrapScene.unity` через Unity asset metadata и component search;
- текущие Unity console records.

## Source census result

### Runtime driver adapter

Поиск `IGlobalMotionRebaseRuntimeDriverAdapter` обнаружил только interface declaration в `GlobalMotionRebaseRuntimeDriver.cs`. Concrete implementation отсутствует.

### Native adapters

Поиск `IGlobalMotionNativeAdapter` обнаружил только contract и `GlobalMotionNativeAdapterSet`. Concrete adapters для пяти областей отсутствуют:

```text
Transform
Rigidbody
ShipDeckNav
CameraHistory
NetworkBaseline
```

### Scene installation

Unity component search для `GlobalMotionRebaseRuntimeDriver` в активной сцене вернул:

```text
instanceIDs = []
totalCount = 0
```

`get_asset_meta` для BootstrapScene не вернул component properties; поэтому полная serialized metadata-проверка сцены через этот вызов inconclusive. Component search не обнаружил установленный driver, но это не заменяет будущий explicit scene audit после появления concrete MonoBehaviour installer.

### Existing pose adapter

`GlobalMotionPoseAdapter` уже содержит реальные baseline/pose operations и `Physics.SyncTransforms`, но он:

- не реализует `IGlobalMotionNativeAdapter`;
- не реализует `IGlobalMotionRebaseRuntimeDriverAdapter`;
- не предоставляет rollback snapshot для полного coverage;
- работает в существующем baseline/motion pipeline, а не в rebase transaction.

Его нельзя автоматически считать готовым native adapter без отдельной reviewed binding policy.

## Runtime evidence review

Доступные Unity logs подтверждают только следующее:

- compile errors отсутствуют;
- `rebase` search не содержит runtime transaction markers;
- сохраняются повторные warnings `Failed to create agent because it is not close enough to the NavMesh`;
- последние MCP transport write errors являются инфраструктурными сообщениями канала и не должны классифицироваться как floating-origin runtime failures.

Capture `T-FO06AS` остаётся partial:

```text
baseline = CONFIRMED
NGO/physics = CONFIRMED
camera/deck/passenger = OBSERVED
controlled_rebase = NOT_RUN
rollback = NOT_PROVEN
runtimeRebaseReadiness = NOT_READY
```

## Gate decision

Переход к установке concrete adapters и BootstrapScene runtime driver сейчас **не разрешён**.

Причины:

1. отсутствуют concrete implementations для обоих adapter seams;
2. отсутствует полный reviewed runtime evidence package;
3. participant admission и live manifest publication не доказаны;
4. ShipDeckNav positive records сопровождаются NavMesh warnings;
5. rollback evidence отсутствует;
6. serialized scene metadata verification для отсутствующего driver через `get_asset_meta` inconclusive, хотя component search дал `0` объектов.

## Следующий разрешённый этап

Следующий implementation slice должен быть отдельным и reviewed:

- создать concrete adapter только для одного owner-reviewed domain;
- определить его exact participant identity и snapshot/restore semantics;
- не добавлять его в BootstrapScene автоматически;
- получить compile/static validation;
- затем отдельно провести user-controlled capture для этого adapter domain.

До выполнения этих условий нельзя создавать full-coverage adapter set, readiness bundle или runtime installation intent.

## Verification

```text
source census = 0 concrete runtime-driver adapters
native adapter census = 0 concrete native adapters
BootstrapScene component search = 0 GlobalMotionRebaseRuntimeDriver objects
compile = No compile errors
Play Mode = not run
runtimeRebaseReadiness = NOT_READY
```
