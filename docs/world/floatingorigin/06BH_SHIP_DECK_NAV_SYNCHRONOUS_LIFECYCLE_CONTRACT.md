# T-FO06BH — ShipDeckNav synchronous lifecycle contract

Дата: 2026-09-12  
Статус: **COMPILE_VERIFIED / CONTRACT_ONLY / NOT_READY**

## 1. Назначение

Зафиксировать отдельную lifecycle boundary для будущего `ShipDeckNav` native adapter. Контракт устраняет неоднозначность между асинхронной регистрацией NavMesh и transaction-safe `Rebuild/Restore`, но не реализует runtime регистрацию или перемещение объектов.

## 2. Реализация

Создан файл:

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseShipDeckNavLifecycleContract.cs`

Добавлены:

- `GlobalMotionShipDeckNavLifecyclePhase` с последовательностью:
  `Unbound → RegistrationRequested → Registered → Ready → SnapshotCaptured → RebuildRequested → Rebuilt → Validated → Restored`;
- immutable evidence envelope для ShipDeckNav/NavMesh/passenger lineage;
- `TryValidateReady` для проверки generation IDs и synchronous rebuild/restore capability;
- `TryValidateCaptured` для проверки transaction snapshot и captured passenger state;
- `TryValidateRestored` для проверки финальной restore phase и passenger restore.

## 3. Fail-closed правила

Контракт отклоняет evidence без:

- ShipDeckNav и NavMesh identity;
- transaction identity;
- registration, NavMesh-instance и passenger-attachment generations;
- synchronous rebuild capability;
- synchronous restore capability;
- captured passenger state;
- финальной `Restored` phase.

Асинхронный `IsRegistered/IsReady` сам по себе не считается доказательством transaction-safe adapter readiness.

## 4. Граница этапа

- concrete `ShipDeckNav` adapter: **не создан**;
- runtime registration/rebuild/restore: **не выполнялись**;
- `GlobalMotionNativeAdapterSet`: **не изменялся**;
- `BootstrapScene`: **не изменялась**;
- Play Mode: **не запускался**;
- `runtimeRebaseReadiness`: **NOT_READY**.

## 5. Проверки

- `check_compile_errors`: **No compile errors**;
- runtime evidence: **не предоставлено**;
- synchronous NavMesh lifecycle: **UNVERIFIED**;
- rollback execution: **UNVERIFIED**.

Следующий этап должен либо предоставить concrete producer, удовлетворяющий этому контракту, либо зафиксировать невозможность synchronous lifecycle на текущем `ShipDeckNav` API.
