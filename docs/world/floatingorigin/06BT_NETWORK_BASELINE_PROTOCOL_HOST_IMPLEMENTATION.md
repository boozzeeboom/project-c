# T-FO06BT — concrete NetworkBaseline protocol host

Дата: 2026-09-12  
Статус: **IMPLEMENTED / FAIL-CLOSED / NO RUNTIME CHANGE**

## 1. Назначение

После `T-FO06BS` и границы `T-FO06BO` создан первый concrete `IGlobalMotionNetworkBaselineTransactionHost` поверх существующего `GlobalMotionReplicator`. Срез проверяет только фактическое наличие host-компонента и его fail-closed поведение; protocol-owned NGO restore не имитируется.

## 2. Реализация

Создан `GlobalMotionNetworkBaselineProtocolHost`:

- `MonoBehaviour` с `[RequireComponent(typeof(GlobalMotionReplicator))]`;
- lazy `ResolveReplicator()` связывает host с требуемым компонентом и делает binding проверяемым в Edit Mode без `Awake`/spawn;
- читает только accepted server baseline из `GlobalMotionReplicator`;
- строит `GlobalMotionNetworkBaselineEvidence` и observation-only rollback envelope;
- сохраняет transaction и NetworkObject identity/lifetime capture tracking;
- `TryApply`, `TryRebuild`, `TryValidate` и `TryRestore` остаются fail-closed до реализации protocol-owned ownership/lifetime/baseline-generation restore;
- `IsRestorable == false`;
- `TryGetCapability` не объявляет `Restorable` capability;
- автоматическая регистрация, scene installation и provider binding отсутствуют.

## 3. Проверка

После исправления lazy component resolution выполнены:

```text
check_compile_errors = No compile errors
T-FO06BT validator = 8 pure checks PASS / 0 FAIL
```

Pure validator подтверждает:

- concrete host component создаётся;
- host разрешает требуемый `GlobalMotionReplicator`;
- unspawned host не выдаёт capability;
- invalid capture request отклоняется;
- Apply и Rebuild остаются закрытыми;
- Validate требует capture identity;
- Restore остаётся закрытым;
- host не регистрирует себя и не открывает readiness.

## 4. Граница

```text
concrete host component = IMPLEMENTED / FAIL-CLOSED
NativeReady = false
Restorable capability = NOT AVAILABLE
NetworkBaseline adapter = SEAM ONLY
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

`TryReadObservation` требует реального server-spawned/listening NGO replicator с accepted baseline. Pure Edit Mode validator намеренно проверяет отсутствие capture identity до этого runtime условия; он не является runtime capture.

## 5. Следующий gate

Для открытия `Restorable` capability необходим отдельный protocol implementation, владеющий server-side ownership mutation, spawn/lifetime generations, baseline-generation restore и protocol receipts для ordered capture/apply/validate/restore. До этого host нельзя регистрировать в native adapter set или связывать с provider/runtime driver.

## 6. Неопределённость

NGO package internals, generated code, serialized scene callbacks и external tooling не проверялись этим срезом и не могут считаться доказательством protocol-owned restore.
