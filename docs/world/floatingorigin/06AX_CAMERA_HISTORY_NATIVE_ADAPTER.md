# T-FO06AX — owner-reviewed CameraHistory native adapter

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AX` — explicit CameraHistory adapter slice после `T-FO06AW`. Реализована только dormant coverage для одной owner-reviewed `SpringArmCamera`; runtime installation и full rebase readiness не выполняются.

## Реализация

Созданы/изменены:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionCameraHistoryNativeAdapter.cs
Assets/_Project/Scripts/Core/SpringArmCamera.cs
```

`GlobalMotionCameraHistoryNativeAdapter` реализует:

- explicit `adapterId`, `ownerId` и переданный `SpringArmCamera`;
- descriptor coverage только `CameraHistory`;
- `Capture/Apply/Rebuild/Validate/Restore` capability declaration;
- transaction-scoped snapshot camera pose, lag target/speed, collision state/position/exit time, ship mode и camera/target/billboard identity;
- Apply через `request.Plan.LocalTranslation` с синхронным переносом camera pose, lag target и collision history;
- finite-state validation;
- identity-checked restore.

`SpringArmCamera` получил отдельный dormant API для capture/apply/restore/validate. Обычный camera update loop и ownership flow этот API не вызывают.

## Safety boundary

Adapter:

- не выполняет discovery;
- не регистрируется автоматически;
- не добавляется в `GlobalMotionNativeAdapterSet`;
- не устанавливается в BootstrapScene;
- не подключается к runtime driver;
- не создаёт live `CameraOwnershipHistoryEvidence` и не подменяет его runtime proof;
- не запускает Play Mode.

Для готовности camera state требуется active/enabled `SpringArmCamera`, initialized `Camera`, bound target и `Billboard.ActiveCamera == camera.transform`.

## Ограничения

Camera adapter покрывает только native Unity camera-history state. Отдельно остаются недоказанными:

```text
active-camera ownership/session publication
binding/history generation evidence
runtime camera continuity during a real rebase
ShipDeckNav adapter
NetworkBaseline adapter
live manifest publication
participant admission
runtime rollback evidence
```

`runtimeRebaseReadiness` остаётся `NOT_READY`.

## Проверка

```text
check_compile_errors = No compile errors
validate_script(SpringArmCamera) = 0 errors; existing advisory warning only
validate_script(GlobalMotionCameraHistoryNativeAdapter) = 0 errors, 0 warnings
git diff --check = pending documentation/commit check
BootstrapScene mutation = none
Play Mode = not run
runtimeRebaseReadiness = NOT_READY
```
