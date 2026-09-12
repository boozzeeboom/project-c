# T-FO06CR — ShipDeck lifecycle producer owner-review gate

Дата: 2026-09-12  
Статус: **DOCS-ONLY / OWNER-REVIEW-GATED / IMPLEMENTATION-BLOCKED / RUNTIME-BINDING-BLOCKED**

## 1. Цель и граница

После `T-FO06CQ` выполнен owner-review proposed ShipDeck lifecycle producer-а. Gate должен был определить, может ли один legitimate server/protocol owner losslessly own all four lifecycle phases — `ShipRegistered`, `PassengerAttached`, `PassengerDetached` и `ShipInvalidated` — and provide the stable protocol identity, coordinator-issued generation lineage, strict order and terminal semantics required by the dormant `GlobalMotionShipDeckPassengerLifecycleCoordinator`.

Review закрыт как **IMPLEMENTATION-BLOCKED**. В audited paths нет legitimate single server/protocol owner с одновременно доказанными stable protocol `ShipId`, stable passenger identity, complete four-phase ownership, coordinator-issued generations, contiguous ordering and terminal invalidation. Поэтому proposed producer не создан и не promoted из существующих seams; это не разрешение на implementation, caller binding или runtime integration.

## 2. Owner-review decision

Единственный допустимый future producer — explicit reviewed server/protocol owner, который losslessly принимает authoritative lifecycle facts и подаёт их в:

```text
reviewed producer/typed bridge
  -> GlobalMotionShipDeckPassengerLifecycleCoordinator.TryAccept
  -> coordinator-owned receipt and generations
  -> typed coordinator mapping/handoff
  -> GlobalMotionShipDeckCombinedTransactionHost
```

Прямые callbacks от `NpcShipController`, `ShipCrewSpawner`, `NpcBrain` или `ShipDeckNav` не являются producer implementation. Ни один из них не может быть promoted только по наличию подходящего callback/seam. `GlobalMotionShipDeckCombinedTransactionHost` остаётся downstream consumer, а не source owner.

| Existing seam | Что он действительно доказывает | Почему promotion запрещён |
|---|---|---|
| `NpcShipController` | Server registration/despawn boundary для ship в `NpcShipZoneRegistry`/`NpcShipWorld`; наблюдаемый кандидат для `ShipRegistered` и terminal boundary | Не владеет stable protocol `ShipId`, coordinator lifetime generation, all passenger attach/detach facts, strict ledger order или one authoritative terminal invalidation. `NetworkObjectId`-derived `npcInstanceId` не является protocol identity. |
| `ShipCrewSpawner` | Server crew spawn/reuse/despawn-map seam; candidate input for passenger ingress | Spawn/reuse не доказывает completed attachment или detach, не владеет stable passenger identity, ship lifetime/attachment generations, coordinator order и terminal invalidation; local map/`IsSpawned` не заменяют protocol facts. |
| `NpcBrain` | Server explicit attach/detach request/completion seam, включая parent/deck readiness checks | Local attachment state и object references не выдают stable protocol passenger identity, coordinator-issued attachment generation, ship lifetime lineage, cross-phase order или terminal ship ownership. `NpcBrain` не является unified owner для registration, spawn and invalidation. |
| `ShipDeckNav` | Async NavMesh registration/readiness prerequisite; `IsReady` может быть supporting evidence для `PassengerAttached` | NavMesh lifecycle не является server/protocol lifecycle owner. `RegistrationGeneration` относится только к NavMesh registration и не может стать ShipId, ship lifetime generation, attachment generation или ledger ordinal; async callback order не является protocol order. |

Разделённые seams не образуют легитимного owner-а через callback chaining. Полный project search за пределами перечисленных audited paths остаётся **INCONCLUSIVE**, поэтому отсутствие evidence не преобразуется в разрешение на synthetic implementation.

## 3. Required owner evidence before implementation

Implementation может быть рассмотрена только после отдельного owner review, который предъявит explicit evidence одного server/protocol owner-а для всех четырёх phases:

### 3.1 Stable identity and ownership

- Explicit stable protocol `ShipId`, stable across spawn/reuse/reconnect and independent of Unity/NGO object identity.
- Explicit stable protocol passenger identity for every attachment epoch, with reviewed reuse/reconnect semantics.
- Proof that the same legitimate server/protocol owner can account for ship registration, passenger attachment, passenger detachment and terminal invalidation; split observation seams are insufficient.
- `ServerAuthorized=true` and `ProtocolOwned=true` provenance for every accepted fact.
- `ShipNetworkObjectId`/other NGO identity may be supplemental cross-check only; it cannot substitute for protocol identity.

### 3.2 Coordinator-issued generations and lineage

- `ShipRegistered` must establish a new lifetime boundary from explicit `ShipId`; the coordinator, not the source seam, issues the never-reused `ShipLifetimeGeneration` and first `LedgerOrdinal`.
- `PassengerAttached` must carry the matching accepted ship lifetime and exact stable passenger identity after completed attach and ready deck evidence; the coordinator issues the new strictly increasing `AttachmentGeneration` and ledger ordinal.
- `PassengerDetached` must identify the exact active coordinator attachment epoch and matching ship lifetime; it closes that epoch without inventing a new generation.
- `ShipInvalidated` must identify the exact active ship lifetime and explicit terminal reason; the coordinator issues the terminal ledger ordinal, clears active passenger generations and rejects all post-terminal facts.
- The future owner must prove that generations/lineage are coordinator-issued and recoverable from typed receipts, never synthesized from local callbacks, object state or host state.

### 3.3 Four-phase lifecycle ownership, ordering and terminal invalidation

The reviewed owner must provide an authoritative, lossless sequence:

1. **`ShipRegistered`** — accepted server registration for explicit stable `ShipId`; opens one coordinator lifetime.
2. **`PassengerAttached`** — accepted server/protocol passenger identity, matching ship lifetime, completed `NpcBrain` attachment and `ShipDeckNav.IsReady`; opens one active attachment epoch.
3. **`PassengerDetached`** — completed server detach for that exact active epoch; closes it and preserves lineage for a later coordinator-issued generation.
4. **`ShipInvalidated`** — one explicit terminal server/protocol decision with stable ship identity, matching lifetime and reason; closes the lifetime, accounts for open epochs and rejects every later fact.

Evidence must establish contiguous coordinator ledger ordering, duplicate/stale/out-of-order rejection, reattach lineage, terminal invalidation exactly once and a new lifetime for any recreated ship. A set of callback observations, cleanup operations or passenger detach events cannot substitute for the unified owner or terminal fact.

## 4. Typed handoff requirement

After owner review, the producer must create a typed `GlobalMotionShipDeckPassengerLifecycleFact`, call `GlobalMotionShipDeckPassengerLifecycleCoordinator.TryAccept`, and use only the resulting coordinator receipt for `GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping`/handoff and `GlobalMotionShipDeckCombinedTransactionHost` binding. Direct source callbacks, raw object references or host-local binding state must not cross this boundary. The handoff remains dormant until the owner evidence and implementation are separately reviewed.

## 5. Explicitly prohibited synthetic producer and inference

This gate explicitly rejects creating a synthetic lifecycle producer from incomplete observations. No implementation may infer protocol facts, identity, generations, order or terminal invalidation from:

- `IsSpawned`;
- `NetworkObjectId` as stable protocol `ShipId`, passenger identity or generation;
- Unity/NGO object references, parent references, object names or destruction;
- `ShipDeckNav.RegistrationGeneration`;
- callback arrival/order, async completion order or local cleanup order;
- host-local binding generation.

These values may be supplemental observations only after a legitimate owner supplies the actual protocol facts. They cannot promote any of the four existing seams or justify a producer implementation.

## 6. Closure and preserved boundaries

```text
owner-review decision = IMPLEMENTATION-BLOCKED
legitimate single server/protocol owner = NOT FOUND
proposed lifecycle producer = NOT CREATED / NOT PROMOTED
ShipRegistered ownership = NOT PROVEN
PassengerAttached ownership = NOT PROVEN
PassengerDetached ownership = NOT PROVEN
ShipInvalidated ownership = NOT PROVEN
coordinator-issued generation evidence = NOT PROVEN
strict ordering evidence = NOT PROVEN
terminal invalidation evidence = NOT PROVEN
coordinator = DORMANT
coordinator typed mapping/handoff = DORMANT
runtime caller binding = NOT EXECUTED
provider binding = NOT EXECUTED
adapter registration = NOT EXECUTED
BootstrapScene = UNCHANGED
scenes/prefabs = UNCHANGED
runtime configuration = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
search outside audited paths = INCONCLUSIVE
```

Только документация изменяется в рамках этого gate. Не создавался synthetic producer; не изменялись C# scripts, coordinator/handoff, provider/adapter configuration, `BootstrapScene`, scenes, prefabs или runtime configuration. Runtime evidence не собиралось.

## 7. Exact changed files

```text
docs/world/floatingorigin/06CR_SHIP_DECK_LIFECYCLE_PRODUCER_OWNER_REVIEW_GATE.md
docs/world/floatingorigin/06CR_SHIP_DECK_LIFECYCLE_PRODUCER_OWNER_REVIEW_GATE.json
docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md
Assets/_Project/Docs/ITERATIONS.md
```

Only these four documentation files belong to T-FO06CR. No scripts, scenes, prefabs, `BootstrapScene`, provider/adapter bindings or runtime configuration were modified.
