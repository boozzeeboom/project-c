# T-FO06BN — protocol-owned NetworkBaseline reversible capability boundary

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Назначение

После `T-FO06AZ` observation-only evidence всё ещё нельзя помещать в `FullTransaction`. Этот этап фиксирует отдельную reviewed capability boundary для будущего server-coordinated reversible NGO baseline transaction.

Реализация:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNetworkBaselineReversibleTransactionContract.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionNetworkBaselineReversibleTransactionContract.cs
```

## 2. Что теперь считается обязательным

Capability строится только при одновременном наличии:

- valid Restorable NetworkBaseline evidence;
- server-authoritative execution;
- protocol ownership;
- explicit capture, apply, validate и restore boundaries;
- ownership restore и NetworkObject lifetime restore;
- baseline-generation restore;
- explicit transaction identity.

Receipt дополнительно связывает ordered phase с завершёнными capture/apply/validate/restore boundaries. `None` и `Faulted` не являются valid receipt phases; поздняя фаза без завершённой ранней границы отклоняется.

## 3. Что этап не делает

Контракт не читает и не меняет `NetworkObject`, ownership, spawn/lifetime, `GlobalMotionReplicator` или NGO transport. Он не создаёт concrete adapter, не меняет `GlobalMotionNativeAdapterSet`, не устанавливает runtime driver и не выполняет rollback.

Observation-only evidence по-прежнему отвергается. Capability только описывает минимальный reviewed shape будущего protocol-owned producer; наличие capability-контракта не является доказательством runtime readiness.

## 4. Проверка

Menu:

```text
ProjectC/World/Floating Origin/Validate Network Baseline Reversible Contract
```

Фактическая pure-проверка после импорта:

```text
16 pure checks PASS / 0 FAIL
compile = No compile errors
```

## 5. Текущие границы

```text
NetworkBaseline reversible capability = IMPLEMENTED / PURE_ONLY
NetworkBaseline concrete adapter = BLOCKED
ShipDeckNav adapter = BLOCKED / SEPARATE LIFECYCLE GATE
provider binding = NOT EXECUTED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
runtime installation = NOT_CONNECTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Следующий serial gate — source-level design/review of a concrete protocol-owned producer that can satisfy this capability without local ownership or lifetime mutation. До такого producer нельзя seal/register the full adapter set или bind the dormant sources in `BootstrapScene`.
