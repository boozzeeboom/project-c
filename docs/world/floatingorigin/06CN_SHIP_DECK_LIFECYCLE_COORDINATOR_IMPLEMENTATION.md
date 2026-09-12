# T-FO06CN — ShipDeck lifecycle coordinator implementation gate

Дата: 2026-09-12  
Статус: **IMPLEMENTED / DORMANT / PURE-VALIDATED / RUNTIME-BINDING-BLOCKED**

## 1. Цель и граница

После `T-FO06CM` реализован выбранный protocol-owned server lifecycle coordinator `GlobalMotionShipDeckPassengerLifecycleCoordinator`. Реализация остаётся runtime-independent и dormant: она принимает только explicit server-authorized lifecycle facts и не обнаруживает Unity/NGO objects, не читает `IsSpawned`, не использует object references и не подключается к runtime.

В gate намеренно не изменялись и не связывались `NpcShipController`, `ShipCrewSpawner`, `NpcBrain`, `ShipDeckNav`, `GlobalMotionShipDeckCombinedTransactionHost`, provider, adapter set, `BootstrapScene`, сцены, префабы или runtime driver.

## 2. Реализация

Добавлены два source-level типа и один coordinator:

- `GlobalMotionShipDeckPassengerLifecycleFact` — explicit fact с protocol ship identity, supplemental `ShipNetworkObjectId`, expected lifetime/attachment lineage, passenger/deck identity, invalidation reason, server authorization и protocol ownership;
- `GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt` — immutable receipt с stable `ShipId`, coordinator-owned ship lifetime generation, passenger attachment generation, strict ledger ordinal и terminal invalidation reason;
- `GlobalMotionShipDeckPassengerLifecycleCoordinator` — sealed class, реализующий `IGlobalMotionShipDeckPassengerGenerationSource`.

`TryAccept` принимает только четыре разрешённых phase: `ShipRegistered`, `PassengerAttached`, `PassengerDetached`, `ShipInvalidated`. Coordinator:

- выдаёт ship lifetime generation только при принятом `ShipRegistered`; счётчик строго возрастает и не имеет reset/clear path;
- выдаёт attachment generation только при принятом attach; reattach требует предыдущую explicit attachment generation и получает строго большее значение;
- проверяет stable protocol identities, matching supplemental network identity, expected ship lifetime, active passenger uniqueness и deck identity;
- отклоняет duplicate, stale, mismatched и out-of-order facts, а также любое событие после terminal invalidation;
- сохраняет invalidated ship ledger для отказа от поздних receipts и позволяет тому же stable ship identity начать новый lifetime только с новой generation;
- выдаёт source generation только для active passenger attachment. После detach или invalidation `TryGetGeneration` fail-closed.

Synthetic generation из `IsSpawned`, `NetworkObjectId`, Unity/NGO object references, `ShipDeckNav.RegistrationGeneration` и host-local binding generation не используется. `NetworkObjectId` является только supplemental identity для результата существующего `IGlobalMotionShipDeckPassengerGenerationSource` contract.

## 3. Pure Edit Mode validator

Добавлен `ValidateGlobalMotionShipDeckPassengerLifecycleCoordinator` с menu item:

`ProjectC/World/Floating Origin/Validate ShipDeck Passenger Lifecycle Coordinator`

Validator не создаёт GameObject и не вызывает runtime/NGO API. Проверяются registration/lifetime lineage, source view, exact detach, reattach monotonicity, duplicate active attach, stale/mismatched/out-of-order rejection, terminal invalidation, lifetime rollover, authorization/ownership и stable identity/coordinator-owned generation guards.

Фактический результат Unity Editor:

```text
[T-FO06CN] Passenger lifecycle coordinator: 11 pure checks PASS / 0 FAIL; coordinator remains dormant and unbound.
```

Дополнительные проверки:

```text
check_compile_errors = No compile errors
validate_script coordinator = 0 warnings / 0 errors
validate_script validator = 0 warnings / 0 errors
Play Mode = NOT RUN
```

## 4. Integration status

```text
coordinator implementation = DORMANT / IMPLEMENTED
IGlobalMotionShipDeckPassengerGenerationSource = IMPLEMENTED / NOT BOUND
runtime lifecycle fact producers = NOT BOUND
combined transaction host binding = NOT EXECUTED
provider binding = NOT EXECUTED
adapter registration = NOT EXECUTED
BootstrapScene = UNCHANGED
scenes/prefabs = UNCHANGED
runtime driver = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Следующий этап должен отдельно owner-review explicit facts и выполнить serial source binding. Нельзя заменить его inference из существующих callbacks или object state.

## 5. Изменённые файлы

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleCoordinator.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleCoordinator.cs.meta
docs/world/floatingorigin/06CN_SHIP_DECK_LIFECYCLE_COORDINATOR_IMPLEMENTATION.md
docs/world/floatingorigin/06CN_SHIP_DECK_LIFECYCLE_COORDINATOR_IMPLEMENTATION.json
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleCoordinator.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleCoordinator.cs.meta
docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md
Assets/_Project/Docs/ITERATIONS.md
```
