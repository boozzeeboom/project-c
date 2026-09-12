# T-FO06BO — protocol-owned NetworkBaseline native adapter seam

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Назначение

После `T-FO06BN` создан explicit producer seam для будущего `NetworkBaseline` native adapter. NGO ownership, spawn/lifetime и baseline generation остаются внутри отдельного protocol-owned host; adapter не пытается восстанавливать их локально.

Реализация:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNetworkBaselineNativeAdapter.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionNetworkBaselineNativeAdapter.cs
```

## 2. Контракт host-а

`IGlobalMotionNetworkBaselineTransactionHost` обязан предоставить:

- reviewed `GlobalMotionNetworkBaselineReversibleCapability`;
- capture evidence;
- protocol-owned apply;
- synchronous rebuild/continuity boundary;
- validation;
- restore с ownership/lifetime/baseline-generation semantics.

`GlobalMotionNetworkBaselineNativeAdapter`:

- покрывает только `NetworkBaseline`;
- сообщает `NativeReady` только при полном `T-FO06BN` capability;
- проверяет participant/session/spawn identity до делегирования;
- отклоняет rollback evidence другого participant или transaction;
- не выполняет discovery и не регистрируется автоматически.

## 3. Фактическая проверка

Menu:

```text
ProjectC/World/Floating Origin/Validate Network Baseline Native Adapter
```

Фактическая проверка:

```text
6 pure checks PASS / 0 FAIL
compile = No compile errors
```

Проверяется готовый и неполный host, identity/lifetime guards, делегирование capture, rollback identity rejection и отсутствие self-registration.

## 4. Граница

```text
NetworkBaseline adapter seam = IMPLEMENTED / PURE_ONLY
concrete protocol-owned host = NOT PRESENT
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
runtime driver = NOT INSTALLED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Это не concrete runtime readiness: тестовый host существует только внутри Editor validator. Реальный host должен быть реализован поверх protocol-owned NGO lifecycle и подтверждён пользовательским runtime capture до serial binding.
