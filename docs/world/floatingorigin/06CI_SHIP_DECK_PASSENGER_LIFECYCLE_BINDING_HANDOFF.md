# T-FO06CI — ShipDeck passenger lifecycle binding handoff

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Цель

После `T-FO06CH` добавлен explicit handoff seam между reviewed lifecycle binding и `GlobalMotionShipDeckCombinedTransactionHost`.

Handoff принимает только уже созданные reviewed receipts. Он не создаёт generation, не ищет пассажиров и не подключает runtime coordinator.

## 2. Реализация

Добавлены:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.cs
```

Обновлён:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckCombinedTransactionHost.cs
```

В host добавлены explicit методы:

- `TryConfigureReviewedPassengerLifecycleBinding(...)`;
- `TryValidateReviewedPassengerLifecycleBinding(...)`;
- `HasReviewedPassengerLifecycleBinding`.

## 3. Проверяемые границы

Handoff требует совпадения:

- количества reviewed `NpcBrain` passenger sources;
- explicit lifecycle `BindingGeneration`;
- ship identity и ship lifetime generation;
- server/protocol ownership.

Binding остаётся отдельным от существующего `_reviewedPassengerBindingGeneration`: handoff не подменяет текущую snapshot binding lineage и не выполняет capture/rebuild/restore.

## 4. Проверки

```text
T-FO06CI validator = 8 pure checks PASS / 0 FAIL
check_compile_errors = No compile errors
script validation = 0 warnings / 0 errors
Play Mode = NOT RUN
runtime handoff = NOT EXECUTED
```

Проверены успешный handoff, отсутствие passenger sources, count mismatch, missing/mismatched binding generation, identity failure и ownership failure.

## 5. Граница

```text
server-owned lifecycle producer = NOT IMPLEMENTED
IGlobalMotionShipDeckPassengerGenerationSource = NOT BOUND
GlobalMotionShipDeckCombinedTransactionHost = explicit handoff API only
NpcBrain = not mutated
ShipDeckNav = not mutated
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
BootstrapScene = UNCHANGED
runtimeRebaseReadiness = NOT_READY
```

Существующие `TryCapture`, `TryRebuild`, `TryValidate` и `TryRestore` не начинают автоматически вызывать handoff. Runtime integration остаётся dormant и fail-closed.

## 6. Следующий gate

Следующий этап — передать в explicit handoff receipts от одного legitimate server-owned lifecycle owner-а. Пока такой owner не существует, handoff может проверяться только pure validator-ом.
