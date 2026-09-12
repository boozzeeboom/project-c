# T-FO06CK — Combined snapshot lifecycle binding identity gate

Дата: 2026-09-12  
Статус: **IMPLEMENTED / PURE_VALIDATED / COMPILE_VERIFIED / DORMANT**

## 1. Цель

Явно перенести reviewed lifecycle binding identity в `GlobalMotionShipDeckCombinedSnapshot` и проверять её на snapshot boundary без создания runtime producer и без подключения runtime coordinator.

## 2. Изменение

`GlobalMotionShipDeckCombinedSnapshot` теперь переносит полный `GlobalMotionShipDeckPassengerLifecycleBindingReceipt` через `LifecycleBinding`.

`GlobalMotionShipDeckCombinedTransactionHost` теперь:

- требует валидный reviewed lifecycle binding перед `TryCapture`;
- записывает binding receipt в каждый combined snapshot;
- при snapshot validation повторно валидирует host binding;
- валидирует snapshot binding по handoff contract;
- отклоняет snapshot при несовпадении lifecycle binding identity.

Автоматическое обнаружение пассажиров, генерация receipts из `NetworkObject.IsSpawned`, `NpcBrain`, `ShipDeckNav` или object references не добавлялись.

## 3. Pure Edit Mode validation

Validator временным component graph проверяет:

- перенос `ShipNetworkObjectId`, `ShipSpawnGeneration`, `BindingGeneration`, `PassengerCount` и ownership flags в snapshot;
- rejection `TryCapture` без lifecycle binding;
- существующий explicit handoff и rejection mismatched binding generation;
- существующие null/identity fail-closed guards.

Результат:

```text
T-FO06CB combined host validator = 10 pure checks PASS / 0 FAIL
check_compile_errors = No compile errors
Play Mode = NOT RUN
```

## 4. Граница

```text
server-owned lifecycle producer = NOT IMPLEMENTED
IGlobalMotionShipDeckPassengerGenerationSource = NOT BOUND
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
BootstrapScene = UNCHANGED
runtimeRebaseReadiness = NOT_READY
```

Это snapshot-boundary gate только для dormant protocol host. `NpcBrain`, `ShipDeckNav`, passenger ledger, adapter set, provider и runtime driver не подключались.

## 5. Следующий gate

Следующий отдельный этап — read-only audit и выбор legitimate server-owned lifecycle owner-а, который сможет выпускать receipts из фактических ship spawn/despawn и passenger attach/detach переходов. До этого runtime integration остаётся заблокированной.
