# T-FO06CS — ShipDeck lifecycle producer/bridge implementation gate

Дата: 2026-09-12
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE-VALIDATED / DORMANT / RUNTIME-CALLER-BINDING-BLOCKED**

## 1. Цель и граница

После `T-FO06CR` реализован следующий реальный code gate: concrete server/protocol-owned `GlobalMotionShipDeckPassengerLifecycleProducerBridge`. Он владеет ровно одним экземпляром `GlobalMotionShipDeckPassengerLifecycleCoordinator` и принимает только explicit caller-supplied protocol facts.

Bridge не является object-discovery adapter-ом и не подключается к существующим gameplay seams. В этом gate не изменялись и не вызывались `NpcShipController`, `ShipCrewSpawner`, `NpcBrain`, `ShipDeckNav`, `GlobalMotionShipDeckCombinedTransactionHost`, provider, adapter set, `BootstrapScene`, сцены или prefabs. Play Mode не запускался.

## 2. Concrete producer/bridge

Новый runtime-independent класс:

`GlobalMotionShipDeckPassengerLifecycleProducerBridge`

Он реализует `IGlobalMotionShipDeckPassengerGenerationSource` и содержит private readonly coordinator instance. Публичные explicit server-authorized ingress methods:

- `TryRegisterShipServerAuthorized(...)` — `ShipRegistered`;
- `TryAttachPassengerServerAuthorized(...)` — `PassengerAttached`;
- `TryDetachPassengerServerAuthorized(...)` — `PassengerDetached`;
- `TryInvalidateShipServerAuthorized(...)` — terminal `ShipInvalidated`.

Каждый ingress требует:

- caller-supplied stable protocol `ShipId`;
- caller-supplied stable passenger identity for attach/detach;
- explicit non-zero `ShipNetworkObjectId` as supplemental transport identity;
- explicit `serverAuthorized` and `protocolOwned` flags;
- matching expected ship lifetime and attachment lineage where the phase requires it;
- explicit `DeckNavId` for attachment/detach and explicit invalidation reason for terminal invalidation.

`NetworkObjectId` не превращается в stable protocol identity или generation. Bridge не принимает object references, `IsSpawned`, callback sequence, `ShipDeckNav.RegistrationGeneration` или host-local binding generation и не выводит факты из них.

## 3. Coordinator and T-FO06CO handoff

Каждый accepted fact сначала проходит `GlobalMotionShipDeckPassengerLifecycleCoordinator.TryAccept`. Coordinator остаётся единственным владельцем:

- ship lifetime generations;
- passenger attachment generations;
- contiguous ledger ordinals;
- active passenger lineage;
- terminal invalidation state.

Только после успешного coordinator acceptance bridge вызывает `GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract.TryMap` и возвращает lossless T-FO06CO mapping. При любой rejection mapping остаётся `default` и не эмитируется.

Таким образом, downstream получает только coordinator provenance: stable `ShipId`, supplemental transport ID, coordinator-owned generations, deck/passenger identity, ledger ordinal, ownership flags, phase и terminal reason.

## 4. Fail-closed boundary

Bridge отклоняет через typed fact/coordinator path:

- missing server/protocol ownership;
- missing stable ship/passenger/deck IDs or transport ID;
- registration before any later ingress;
- stale ship lifetime or attachment generation;
- mismatched transport identity;
- duplicate active attachment;
- detach without the exact active attachment epoch;
- duplicate/out-of-order lifecycle events;
- post-invalidation events;
- terminal invalidation without an explicit reason.

No rejected fact advances coordinator generation/ordinal and no rejected fact returns a mapping.

## 5. Pure Edit Mode validator

Добавлен:

`Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleProducerBridge.cs`

Menu item:

`ProjectC/World/Floating Origin/Validate ShipDeck Lifecycle Producer Bridge`

Validator проверяет все четыре ingress phases, coordinator-owned ship/attachment generations, ledger continuity, lossless mapping/downstream conversion, active generation view, reattach lineage, missing IDs/ownership, invalid lineage, stale/duplicate/mismatched facts and post-invalidation rejection. Validator не создаёт GameObject, не ищет объекты и не вызывает NGO/runtime callbacks.

Фактический результат Unity Editor:

```text
[T-FO06CS] ShipDeck lifecycle producer bridge: 11 pure checks PASS / 0 FAIL; runtime caller binding remains dormant.
check_compile_errors = No compile errors
producer bridge validate_script = 0 warnings / 0 errors
validator validate_script = 0 warnings / 0 errors
Play Mode = NOT RUN
```

## 6. Integration status

```text
producer/bridge = IMPLEMENTED / PURE-VALIDATED
coordinator ownership = PRESERVED
T-FO06CO mapping output = ACCEPTED FACTS ONLY
runtime caller binding = NOT EXECUTED
provider/adapter registration = NOT EXECUTED
GlobalMotionShipDeckCombinedTransactionHost = NOT MODIFIED
BootstrapScene = UNCHANGED
scenes/prefabs = UNCHANGED
runtime driver = UNCHANGED
runtimeRebaseReadiness = NOT_READY
```

This gate creates a real explicit protocol owner, but it does not prove that existing gameplay callers currently possess the required stable protocol facts. Caller binding remains intentionally dormant and fail-closed for a later reviewed integration gate.

## 7. Exact changed files

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleProducerBridge.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleProducerBridge.cs
docs/world/floatingorigin/06CS_SHIP_DECK_LIFECYCLE_PRODUCER_BRIDGE_IMPLEMENTATION.md
docs/world/floatingorigin/06CS_SHIP_DECK_LIFECYCLE_PRODUCER_BRIDGE_IMPLEMENTATION.json
docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md
Assets/_Project/Docs/ITERATIONS.md
```

Unity-generated `.meta` files for the two new C# assets are generated/imported by Unity as applicable. No scene/prefab YAML was changed.
