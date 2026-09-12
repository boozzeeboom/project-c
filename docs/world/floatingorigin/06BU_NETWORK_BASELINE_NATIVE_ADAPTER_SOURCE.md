# T-FO06BU — NetworkBaseline native adapter source binding

Дата: 2026-09-12  
Статус: **IMPLEMENTED / FAIL-CLOSED / NO RUNTIME CHANGE**

## 1. Назначение

После `T-FO06BT` добавлен concrete component seam, который exposes existing `GlobalMotionNetworkBaselineNativeAdapter` как `MonoBehaviour`-source для будущего `GlobalMotionRebaseNativeAdapterEvidenceSource`. Этот этап не регистрирует adapter и не выполняет provider binding.

## 2. Реализация

Создан `GlobalMotionNetworkBaselineNativeAdapterSource`:

- реализует `IGlobalMotionNativeAdapter`;
- требует `GlobalMotionNetworkBaselineProtocolHost` через `RequireComponent`;
- разрешает host лениво и создаёт delegate adapter только при обращении;
- предоставляет descriptor с покрытием только `NetworkBaseline` и полным declared capability set;
- передаёт capture/apply/rebuild/validate/restore в существующий adapter;
- сохраняет `NativeReady=false`, пока host не выдаёт reviewed `Restorable` capability;
- не выполняет self-registration в `GlobalMotionNativeAdapterSet`;
- не вызывает provider binding и не устанавливается в BootstrapScene.

## 3. Проверка

```text
check_compile_errors = No compile errors
T-FO06BU validator = 6 pure checks PASS / 0 FAIL
git diff --check = PASS
```

Pure validator подтвердил:

- source component создаётся вместе с host и replicator dependencies;
- descriptor валиден, покрывает только `NetworkBaseline` и не сообщает `NativeReady`;
- invalid request отклоняется;
- Apply и Rebuild доходят до host boundary и остаются закрытыми при отсутствии accepted server baseline;
- source не открывает readiness и не регистрирует себя автоматически.

## 4. Граница

```text
adapter source component = IMPLEMENTED / FAIL-CLOSED
NetworkBaseline adapter = SEAM ONLY
NativeReady = false
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Descriptor намеренно объявляет `FullTransaction` как требуемую форму adapter contract, но `NativeReady` остаётся `false`. Это не является доказательством готовности transaction pipeline.

## 5. Следующий gate

До admission/provider binding необходимо получить protocol-owned server receipts для ownership, spawn/lifetime и baseline-generation restore, после чего host сможет выдать capability, совместимую с `GlobalMotionNetworkBaselineReversibleTransactionContract`. До этого `GlobalMotionRebaseNativeAdapterEvidenceSource` должен продолжать отклонять неполный adapter set.
