# T-FO06CH — ShipDeck passenger lifecycle source binding preflight

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Цель

После `T-FO06CG` добавлена explicit preflight boundary для binding нескольких reviewed active-passenger lifecycle receipts к одному ship lifetime.

Preflight не ищет пассажиров, не обращается к `NpcBrain`/`ShipDeckNav`, не создаёт receipts и не выполняет runtime binding.

## 2. Реализация

Добавлены:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleSourceBindingContract.cs
```

Binding receipt фиксирует:

- ship `NetworkObjectId`;
- ship lifetime generation;
- explicit binding generation;
- число active passengers;
- server/protocol ownership.

## 3. Проверяемые правила

`TryBindActivePassengers` принимает только explicit receipts с фазой `PassengerAttached` и проверяет:

- одинаковую ship identity и `ShipSpawnGeneration` для всех пассажиров;
- уникальный `PassengerId`;
- валидность каждого lifecycle receipt;
- ненулевой binding generation;
- отсутствие пустого списка.

`PassengerDetached`, `ShipRegistered` и `ShipInvalidated` receipts не могут быть представлены как active passenger binding.

## 4. Проверки

```text
T-FO06CH validator = 8 pure checks PASS / 0 FAIL
check_compile_errors = No compile errors
script validation = 0 warnings / 0 errors
Play Mode = NOT RUN
runtime binding = NOT EXECUTED
```

Проверены accepted binding, phase restriction, ship lifetime mismatch, duplicate passenger identity, missing receipts, missing binding generation и ownership preservation.

## 5. Граница

```text
concrete lifecycle producer = NOT IMPLEMENTED
IGlobalMotionShipDeckPassengerGenerationSource = NOT BOUND
GlobalMotionShipDeckCombinedTransactionHost = UNCHANGED
NpcBrain = UNCHANGED
ShipDeckNav = UNCHANGED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
BootstrapScene = UNCHANGED
runtimeRebaseReadiness = NOT_READY
```

Binding preflight является pure contract boundary и не публикует receipts в ledger или runtime coordinator.

## 6. Следующий gate

Следующий этап должен выполнить explicit source handoff из одного server-owned lifecycle owner-а в этот binding preflight. До появления такого owner-а любые данные должны оставаться reviewed input, а не автоматически вычисляться из object references или `IsSpawned`.
