# T-FO06AV — owner-reviewed Transform native adapter

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AV` — первый concrete adapter implementation slice после readiness audit `T-FO06AU`. Выбран только Transform domain; full adapter set и runtime installation остаются запрещёнными до закрытия manifest/admission/runtime-evidence gates.

## Реализация

Создан файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionTransformNativeAdapter.cs
```

`GlobalMotionTransformNativeAdapter` реализует `IGlobalMotionNativeAdapter` для одного явно переданного owner-reviewed `Transform`:

- стабильный explicit `adapterId`;
- descriptor coverage только `Transform`;
- capability declaration `Capture/Apply/Rebuild/Validate/Restore`;
- capture позиции, rotation и local scale в transaction-scoped snapshot;
- Apply через `request.Plan.LocalTranslation`;
- Validate конечного Transform state;
- Restore по transaction identity, adapter identity и captured Transform coverage;
- отсутствие discovery и автоматической регистрации.

`TryRebuild` остаётся no-op boundary для Transform adapter: physics/NavMesh/camera/NGO synchronization не относится к этому domain и не маскируется под Transform readiness.

## Safety boundary

Adapter не:

- добавлен в `GlobalMotionNativeAdapterSet` автоматически;
- зарегистрирован в BootstrapScene;
- подключён к runtime driver;
- создаёт participant manifest или readiness bundle;
- покрывает Rigidbody, ShipDeckNav, CameraHistory или NetworkBaseline;
- запускает Play Mode;
- изменяет scene/prefab serialization.

Transform mutation возможна только при явном вызове методов adapter владельцем runtime integration. Само создание класса не вызывает Unity mutation.

## Ограничения

Полный `UnityStateRollbackEvidence` требует runtime capture/restore proof. Adapter сохраняет transaction snapshot и проверяет identity при Restore, но этот этап не является доказательством runtime rollback и не разрешает `GlobalMotionRebaseRuntimeReadinessGate`.

`GlobalMotionNativeAdapterSet.TryValidateReady(...)` с одним этим adapter закономерно останется заблокированным из-за неполного coverage:

```text
missing = Rigidbody, ShipDeckNav, CameraHistory, NetworkBaseline
```

## Проверка

```text
check_compile_errors = No compile errors
validate_script = 0 errors, 0 warnings
git diff --check = PASS
Play Mode = not run
BootstrapScene mutation = none
runtimeRebaseReadiness = NOT_READY
```

Следующий concrete adapter domain нельзя подключать к scene или full set до отдельного reviewed participant identity и runtime evidence gate.
