# T-FO06CO — ShipDeck lifecycle coordinator source-binding / fact-handoff gate

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE-VALIDATED / DORMANT / RUNTIME-BINDING-BLOCKED**

## 1. Цель и граница

После `T-FO06CN` закрыт source-level provenance mismatch между protocol-owned lifecycle coordinator и downstream lifecycle binding boundary. Gate добавляет typed explicit mapping/handoff only. Он не вызывает runtime producer, не связывает coordinator с существующим host и не открывает runtime readiness.

Не изменялись и не вызывались `NpcShipController`, `ShipCrewSpawner`, `NpcBrain`, `ShipDeckNav`, `GlobalMotionShipDeckCombinedTransactionHost`, provider/adapter registration, `BootstrapScene`, сцены, префабы или runtime driver. Play Mode не запускался.

## 2. Typed mapping и downstream conversion

Добавлены:

- `GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping` — lossless typed mapping одной `GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt`;
- `GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoff` — explicit envelope с mapping-ами и их downstream receipts;
- `GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract` — fail-closed `TryMap`, `TryCreateHandoff`, mapping/sequence/downstream validation.

Mapping сохраняет без вывода или подмены:

- stable protocol `ShipId`;
- `ShipNetworkObjectId` только как supplemental identity;
- coordinator-owned `ShipLifetimeGeneration`;
- passenger `AttachmentGeneration`;
- `DeckNavId`;
- monotonic contiguous `CoordinatorLedgerOrdinal`;
- `ServerOwned` и `ProtocolOwned`;
- lifecycle event kind (`ShipRegistered`, `PassengerAttached`, `PassengerDetached`, `ShipInvalidated`);
- terminal invalidation reason.

`GlobalMotionShipDeckPassengerLifecycleReceipt` получил provenance-поля `ShipId`, `ShipLifetimeGeneration`, `CoordinatorLedgerOrdinal` и `InvalidationReason`, а новый explicit constructor используется только coordinator handoff mapping-ом. Старый producer constructor и его validation path сохранены для обратной совместимости существующих T-FO06CG/CH/CI seams.

Contract не выводит identity или generation из `IsSpawned`, `NetworkObjectId`, object references, `ShipDeckNav.RegistrationGeneration`, callback order или host-local binding generation. Sequence order проверяется только по явно переданному coordinator ledger ordinal и lifecycle event kind.

## 3. Fail-closed правила

Отклоняются:

- отсутствующие stable ship/passenger/deck identities;
- zero ship lifetime, attachment или coordinator ledger generation;
- отсутствующие server/protocol ownership flags;
- неполные registration/attach/detach/invalidation mappings;
- mismatched ship/network/lifetime provenance;
- non-contiguous ledger ordinal;
- registration не первым событием;
- duplicate active attachment, invalid detach order и non-monotonic reattach generation;
- duplicate registration и любое событие после terminal invalidation;
- downstream receipt, не совпадающий losslessly с coordinator mapping.

## 4. Pure Edit Mode validator

Добавлен menu item:

`ProjectC/World/Floating Origin/Validate ShipDeck Passenger Coordinator Handoff Contract`

Validator проверяет event ordering, stable identity, supplemental network identity, coordinator lifetime/attachment provenance, deck identity, ledger continuity, server/protocol ownership, terminal invalidation, stale/duplicate/out-of-order/post-invalidation rejection, incomplete mapping rejection и lossless downstream conversion. Validator не создаёт GameObject и не вызывает NGO/runtime API.

Фактические результаты Unity Editor:

```text
[T-FO06CO] Passenger coordinator handoff: 7 pure checks PASS / 0 FAIL; provenance handoff remains dormant.
[T-FO06CN] Passenger lifecycle coordinator: 11 pure checks PASS / 0 FAIL; coordinator remains dormant and unbound.
check_compile_errors = No compile errors
validate_script contract = 0 warnings / 0 errors
validate_script validator = 0 warnings / 0 errors
Play Mode = NOT RUN
```

## 5. Integration status

```text
coordinator provenance mapping = IMPLEMENTED / PURE-VALIDATED
source-binding contract = IMPLEMENTED / DORMANT
runtime caller binding = NOT EXECUTED
GlobalMotionShipDeckCombinedTransactionHost = NOT MODIFIED
provider/adapter registration = NOT EXECUTED
BootstrapScene = UNCHANGED
scenes/prefabs = UNCHANGED
runtime driver = UNCHANGED
runtimeRebaseReadiness = NOT_READY
```

## 6. Exact changed files

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleProducerContract.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.cs
docs/world/floatingorigin/06CO_SHIP_DECK_LIFECYCLE_COORDINATOR_SOURCE_BINDING_HANDOFF.md
docs/world/floatingorigin/06CO_SHIP_DECK_LIFECYCLE_COORDINATOR_SOURCE_BINDING_HANDOFF.json
docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md
Assets/_Project/Docs/ITERATIONS.md
```

Unity-generated `.meta` files may be imported for the two new C# assets; no scene/prefab YAML was changed.
