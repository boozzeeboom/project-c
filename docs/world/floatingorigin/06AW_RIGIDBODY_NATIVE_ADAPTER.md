# T-FO06AW — owner-reviewed Rigidbody native adapter

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AW` — второй concrete adapter domain после `T-FO06AV`. Реализован только explicit Rigidbody coverage для одного owner-reviewed `Rigidbody`; full adapter set и runtime installation не выполняются.

## Реализация

Создан файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRigidbodyNativeAdapter.cs
```

`GlobalMotionRigidbodyNativeAdapter` реализует:

- explicit stable `adapterId` и target `Rigidbody`;
- descriptor coverage только `Rigidbody`;
- `Capture/Apply/Rebuild/Validate/Restore` capability declaration;
- transaction-scoped snapshot позиции, rotation, linear velocity и angular velocity;
- Apply через `request.Plan.LocalTranslation`;
- `Physics.SyncTransforms()` на `TryRebuild` boundary;
- finite-state validation;
- identity-checked restore с восстановлением velocity state.

## Safety boundary

Adapter:

- не выполняет discovery;
- не регистрируется автоматически;
- не добавляется в `GlobalMotionNativeAdapterSet`;
- не устанавливается в BootstrapScene;
- не подключается к runtime driver;
- не изменяет prefab/scene serialization;
- не запускает Play Mode.

Реальная Rigidbody mutation возможна только после explicit invocation из будущего reviewed runtime integration.

## Ограничения

Один Rigidbody adapter не делает readiness bundle валидным. Остаются отсутствующими:

```text
Transform adapter registration
ShipDeckNav adapter
CameraHistory adapter
NetworkBaseline adapter
live manifest publication
participant admission
rollback runtime evidence
```

`TryRebuild` синхронизирует Unity physics transforms только для этого explicit Rigidbody domain; он не доказывает готовность ShipDeckNav, camera или NGO baseline.

## Проверка

```text
check_compile_errors = No compile errors
validate_script = 0 errors, 0 warnings
git diff --check = PASS
Play Mode = not run
BootstrapScene mutation = none
runtimeRebaseReadiness = NOT_READY
```
