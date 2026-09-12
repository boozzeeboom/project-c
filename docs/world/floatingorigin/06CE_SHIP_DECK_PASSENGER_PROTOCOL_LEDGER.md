# T-FO06CE — ShipDeck passenger protocol ledger boundary

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Назначение

После `T-FO06CD` добавлена protocol-owned ledger boundary для reviewed passenger/deck lifecycle transaction. Ledger фиксирует обязательные receipts и фазовую lineage, но не является concrete NGO producer и не читает runtime state.

Контракт намеренно не подключён к `NpcBrain`, `ShipDeckNav`, `GlobalMotionNativeAdapterSet` или runtime driver.

## 2. Реализация

Добавлены:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerProtocolLedger.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerProtocolLedger.cs
```

Ledger receipt содержит:

- transaction identity;
- passenger/ship/deck generation identity;
- frame generation;
- server и protocol ownership;
- capture, rebuild, validate, restore и rollback receipts;
- monotonic ordinal и terminal phase.

## 3. Проверяемая последовательность

Обычный путь:

```text
CaptureRequested
→ Captured
→ RebuildRequested
→ Rebuilt
→ ValidateRequested
→ Validated
→ RestoreRequested
→ Restored
→ Completed
```

Rollback может начаться до завершения обычного пути:

```text
Any non-terminal phase
→ RollbackRequested
→ RollbackCompleted
```

`Faulted` создаётся отдельным `TryFault` и является terminal receipt. Faulted receipt валидируется по базовой identity/ownership/generation boundary и не требует повторного прохождения обычных capture/rebuild/validate/restore требований.

Контракт отклоняет phase skip, invalid transaction identity, missing generation, missing receipts и неполное завершение rollback.

## 4. Проверки

```text
T-FO06CE validator = 8 pure checks PASS / 0 FAIL
check_compile_errors = No compile errors
script validation = 0 warnings / 0 errors
Play Mode = NOT RUN
runtime producer = UNBOUND
```

Проверены begin identity, capture ordering, phase skip rejection, early rollback, rollback receipt, terminal fault, missing generation и invalid transaction identity.

До исправления validator фиксировал fail-closed ошибки на rollback и Faulted receipt; после исправления повторный запуск дал `8 pure checks PASS / 0 FAIL`.

## 5. Граница

```text
concrete server-owned passenger generation producer = NOT FOUND
protocol-owned passenger ledger = PRESENT
NpcBrain integration = NOT EXECUTED
ShipDeckNav integration = NOT EXECUTED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
runtime driver = NOT INSTALLED
runtimeRebaseReadiness = NOT_READY
```

Search за пределами ранее audited paths остаётся **INCONCLUSIVE**. Ledger не заменяет отсутствующий server-owned producer и не открывает `NativeReady` или `FullTransaction`.

## 6. Следующий gate

Следующий этап должен предоставить concrete server-owned producer receipts для ship lifetime и passenger attachment generation либо явно подтвердить отдельный lifecycle host. До этого ledger остаётся dormant protocol contract без runtime mutation.
