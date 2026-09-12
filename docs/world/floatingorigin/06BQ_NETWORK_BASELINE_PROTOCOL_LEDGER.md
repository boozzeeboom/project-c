# T-FO06BQ — server-owned NetworkBaseline protocol ledger boundary

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Назначение

После `T-FO06BP` добавлена следующая reviewed boundary для будущего concrete NGO host: server-owned transaction ledger с immutable receipt identity и строгим порядком protocol phases.

Это не concrete NGO host. Контракт фиксирует, какие receipts обязан выдавать host, но не читает и не изменяет `NetworkObject`, ownership, spawn/lifetime или `GlobalMotionReplicator` state.

## 2. Реализация

Добавлены:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNetworkBaselineProtocolLedger.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionNetworkBaselineProtocolLedger.cs
```

Ledger receipt связывает каждую фазу с:

- transaction и participant identity;
- `GlobalMotionNetworkBaselineIdentity`;
- authority/discontinuity generations;
- control revision и baseline sequence;
- server ownership и protocol ownership;
- capture/apply/validate/restore receipts;
- ownership, lifetime и baseline-generation restore receipts;
- monotonic ordinal.

## 3. Проверяемая последовательность

```text
CaptureRequested
→ Captured
→ ApplyRequested
→ Applied
→ ValidateRequested
→ Validated
→ RestoreRequested
→ Restored
→ Completed
```

Отдельный `Faulted` receipt является terminal state. Контракт отклоняет phase skip, identity mismatch, missing capture/apply/validate receipts и неполное ownership/lifetime/generation restore.

## 4. Проверки

```text
T-FO06BQ validator = 10 pure checks PASS / 0 FAIL
check_compile_errors = No compile errors
Play Mode = NOT RUN
```

Проверены begin/identity, server+protocol ownership, phase ordering, capture/apply/validate lineage, complete restore requirements, completed ledger, terminal fault и отсутствие host registration.

## 5. Граница

```text
concrete protocol-owned NGO host = NOT IMPLEMENTED
GlobalMotionNetworkBaselineNativeAdapter = SEAM ONLY
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
runtime driver = NOT INSTALLED
runtimeRebaseReadiness = NOT_READY
```

`TryBegin`, `TryAdvance` и `TryFault` являются pure receipt validation. Они не вызывают NGO API и не являются заменой server-owned transaction ledger implementation.

Следующий gate — reviewed implementation host-а, который будет выдавать эти receipts из фактического NGO lifecycle и сможет доказать ownership/lifetime/baseline-generation capture и restore. До него `NativeReady` и `FullTransaction` остаются закрытыми.
