# T-FO06CQ — ShipDeck lifecycle fact-source ownership and explicit ingress decision gate

Дата: 2026-09-12  
Статус: **DOCS-ONLY / OWNERSHIP-GATED / INCONCLUSIVE / INTEGRATION-BLOCKED / RUNTIME-BINDING-BLOCKED**

## 1. Цель и граница

После `T-FO06CP` выполнен docs-only source review, который переводит четыре ShipDeck lifecycle ingress в explicit ownership decision gate для dormant `GlobalMotionShipDeckPassengerLifecycleCoordinator`. Gate фиксирует, какой concrete seam может наблюдать событие, кто вправе быть legitimate server/protocol owner-ом, какие identity/generation/lineage facts обязательны и как reviewed fact должен попасть в coordinator `TryAccept`.

Ни один существующий callback не объявлен lifecycle receipt автоматически. В текущем проекте **нет complete legitimate producer**, который одновременно выдаёт stable protocol identities, server/protocol ownership, coordinator-compatible generations, strict ordering и terminal invalidation. Поэтому решение остаётся **INTEGRATION-BLOCKED**; это не разрешение на binding.

Только explicit server-authorized и protocol-owned `GlobalMotionShipDeckPassengerLifecycleFact` может быть подан в:

```text
GlobalMotionShipDeckPassengerLifecycleCoordinator.TryAccept(
    fact,
    out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
    out string error)
```

Прямые вызовы coordinator из `NpcShipController`, `ShipCrewSpawner`, `NpcBrain` или `ShipDeckNav` не допускаются. Сначала должен существовать reviewed producer/bridge, который собирает факт, проверяет его provenance и только затем вызывает `TryAccept`; полученный coordinator receipt идёт через существующий typed mapping/handoff к `GlobalMotionShipDeckCombinedTransactionHost`.

## 2. Audited seams и ownership decision

Проверенные source paths:

```text
Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs
Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs
Assets/_Project/Scripts/AI/NpcBrain.cs
Assets/_Project/Scripts/Ship/ShipDeckNav.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleCoordinator.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleProducerContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckCombinedTransactionHost.cs
```

- `NpcShipController.OnNetworkSpawn` / `OnNetworkDespawn` — server registration/despawn seam для NPC ship. Это candidate boundary для `ShipRegistered` и terminal ship invalidation, но не protocol ledger owner. Его fallback `npcInstanceId = NetworkObjectId | sentinel` не является stable protocol `ShipId`.
- `ShipCrewSpawner.EnsureCrewSpawned` / `SpawnMember` — server crew spawn/reuse/despawn-map seam. `SpawnMember` вызывает NGO `Spawn`, затем запрашивает attachment через `NpcBrain`; spawner не владеет attachment completion, generation или terminal order.
- `NpcBrain.AttachToShipDeck` / `DetachFromShipDeck` — server explicit attachment request/completion и detach seam. Он видит passenger-local state и ship/deck references, но не выдаёт stable protocol passenger identity, attachment generation или coordinator ledger ordinal.
- `ShipDeckNav.OnNetworkSpawn` / `OnNetworkDespawn`, `IsReady` — server NavMesh registration/readiness seam. `IsReady` является prerequisite для completed attach; `RegistrationGeneration` описывает только NavMesh registration lifecycle.
- `GlobalMotionShipDeckCombinedTransactionHost` — downstream consumer с reviewed binding и ordered transaction API. Он не является lifecycle fact producer-ом и не должен принимать прямые callbacks.

**Ownership decision:** единственным legitimate owner-ом issued ship lifetime generation, passenger attachment generations, strict coordinator ledger order и terminal invalidation является protocol-owned server `GlobalMotionShipDeckPassengerLifecycleCoordinator` через reviewed producer/bridge. Ни один из существующих four seams не выбран complete producer-ом; они остаются evidence seams, пока не появится explicit source, который losslessly соберёт required facts.

## 3. Explicit ingress decision gate

Для всех ingress действуют общие правила:

1. Stable protocol `ShipId` и stable protocol passenger identity должны быть explicit reviewed values. `NetworkObjectId` допускается только как supplemental cross-check, не как protocol identity или generation.
2. Coordinator сам выдаёт новые `ShipLifetimeGeneration`, `AttachmentGeneration` и `LedgerOrdinal`. Fact передаёт expected lineage, а не синтетически вычисленную локальную замену.
3. Каждый fact обязан иметь `ServerAuthorized=true` и `ProtocolOwned=true`; local/client observations не принимаются.
4. Accepted receipts идут только в typed coordinator mapping/handoff, затем в reviewed host binding. Callback order, Unity references и host-local binding state не заменяют ledger order.
5. При отсутствии любого обязательного evidence producer должен не создавать fact и не вызывать `TryAccept`; если неполный fact всё же подан, coordinator отклоняет его fail-closed с error и без выдачи receipt.

### 3.1 `ShipRegistered`

- **Concrete source seam:** server-only `NpcShipController.OnNetworkSpawn`, где ship регистрируется в `NpcShipZoneRegistry` и `NpcShipWorld`. Это наблюдаемый registration seam, не receipt.
- **Legitimate server/protocol owner:** reviewed server/protocol lifecycle producer, координирующий accepted ship registration; coordinator остаётся единственным owner-ом issued lifetime generation и ledger entry. `NpcShipController` не может стать owner только потому, что его callback сработал.
- **Stable identity source:** explicit stable protocol `ShipId` от owner-reviewed ship registry/protocol identity. Current `npcInstanceId` fallback из `NetworkObjectId` запрещён для этой роли; `ShipNetworkObjectId` сохраняется только как supplemental transport identity.
- **Generation/lineage source:** новая lifetime boundary не несёт synthetic generation во входном fact. Producer передаёт `CreateShipRegistered(shipId, shipNetworkObjectId, serverAuthorized, protocolOwned)`; coordinator выдаёт fresh never-reused `ShipLifetimeGeneration` и первый `LedgerOrdinal`.
- **Ordering/terminal guarantees:** registration должна быть первым событием этого stable `ShipId`; duplicate active registration и stale/reused lifecycle отклоняются. Этот event открывает ledger; terminal `ShipInvalidated` должен быть принят позже ровно для matching lifetime.
- **Mapping into `TryAccept`:** reviewed producer maps the seam to `GlobalMotionShipDeckPassengerLifecycleFact.CreateShipRegistered(...)`, then calls `coordinator.TryAccept`. Only the returned coordinator receipt may enter `GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping` and downstream handoff.
- **Fail-closed:** без explicit `ShipId`, server/protocol ownership, accepted registration или supplemental identity coordinator call не выполняется/возвращает rejection. `IsSpawned`, object presence, callback arrival, `NetworkObjectId`-derived ID и local registry membership не создают fact.

### 3.2 `PassengerAttached`

- **Concrete source seam:** server `ShipCrewSpawner.EnsureCrewSpawned` / `SpawnMember` provides crew spawn candidate; `NpcBrain.AttachToShipDeck` provides explicit attach request and completion path; `ShipDeckNav.IsReady` is required readiness evidence. Spawn alone is not attachment.
- **Legitimate server/protocol owner:** one reviewed server/protocol producer that joins accepted crew/passenger identity, matching ship lifetime, completed `NpcBrain` attachment and ready deck evidence. Neither `ShipCrewSpawner` nor `NpcBrain` alone owns the lifecycle ledger.
- **Stable identity source:** explicit reviewed protocol `ShipId` plus stable passenger identity. A manifest `memberId`/NPC identity may be a candidate only after owner review proves it is stable across reuse and reconnect; a `NetworkObject` reference, object name or `NetworkObjectId` is not sufficient.
- **Generation/lineage source:** fact carries matching expected `ShipLifetimeGeneration` and the previous attachment lineage (`previousAttachmentGeneration` in `CreatePassengerAttached`). The coordinator allocates the new strictly increasing `AttachmentGeneration`; no generation may be synthesized from `IsSpawned`, attach callback order, NavMesh registration or host-local binding.
- **Ordering/terminal guarantees:** only after `ShipRegistered`, with matching ship lifetime, completed attachment, stable deck identity and `ShipDeckNav.IsReady`. No duplicate active attachment; reattach requires the coordinator's prior detached lineage. `ShipInvalidated` is terminal and forbids later attach.
- **Mapping into `TryAccept`:** reviewed producer builds `CreatePassengerAttached(shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, passengerId, previousAttachmentGeneration, deckNavId, true, true)` and calls `TryAccept`. The receipt's coordinator-owned generation and ordinal, not the source callback sequence, are mapped into the existing binding handoff.
- **Fail-closed:** missing stable passenger/ship identity, matching lifetime, explicit ownership, completed attach, `deckNavId` or `IsReady` means no fact/receipt. Spawned object, `_spawnedByMemberId`, requested attachment, parent reference, `IsSpawned` or `RegistrationGeneration` cannot be promoted to attached evidence.

### 3.3 `PassengerDetached`

- **Concrete source seam:** server `NpcBrain.DetachFromShipDeck` is the explicit detach seam. Ship/crew despawn cleanup can be an observation that a terminal boundary may be approaching, but cleanup-map removal is not a detach receipt.
- **Legitimate server/protocol owner:** the same reviewed server/protocol lifecycle producer must close the exact active attachment epoch and submit it to the coordinator; `NpcBrain` owns only its local attachment state, not the global generation ledger.
- **Stable identity source:** reviewed stable protocol `ShipId` and passenger identity, plus the exact active attachment epoch held by the coordinator. Unity/NGO references and current component names are not identity sources.
- **Generation/lineage source:** fact carries matching expected `ShipLifetimeGeneration` and exact active `AttachmentGeneration` returned/recorded for that passenger, together with reviewed `DeckNavId`. No new attachment generation is issued on detach; coordinator closes the matching active epoch.
- **Ordering/terminal guarantees:** detach is valid only after a matching active `PassengerAttached`, with exact ship lifetime, passenger, deck and attachment generation. Duplicate/stale detach fails; a later attach must use a strictly greater coordinator-issued generation. After `ShipInvalidated`, detach is post-terminal and rejected; terminal invalidation does not require synthetic detach callbacks for every passenger.
- **Mapping into `TryAccept`:** reviewed producer builds `CreatePassengerDetached(shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, passengerId, attachmentGeneration, deckNavId, true, true)` and calls `TryAccept`; only the resulting receipt is handed downstream.
- **Fail-closed:** if active ledger state, exact attachment generation, stable identities, ownership or completed server detach is missing, do not infer from `DetachFromShipDeck` invocation, map cleanup, `IsSpawned=false`, parent null, callback order or object destruction. `TryAccept` must reject `passenger_not_active`, stale generation or identity mismatch without a receipt.

### 3.4 `ShipInvalidated`

- **Concrete source seam:** server `NpcShipController.OnNetworkDespawn` is the primary ship lifecycle boundary; `ShipCrewSpawner.OnNetworkDespawn` and `ShipDeckNav.OnNetworkDespawn` are related cleanup seams. They are separate observations, not a complete terminal producer.
- **Legitimate server/protocol owner:** reviewed server/protocol lifecycle authority that confirms the ship lifetime is terminal and emits one invalidation fact. The coordinator owns terminal ledger state; no collection of passenger detach callbacks may substitute for this owner.
- **Stable identity source:** explicit stable protocol `ShipId`, matching the registered lifetime; `ShipNetworkObjectId` is supplemental only. Recreated ships require a new explicit protocol lifetime boundary and must not reuse the prior terminal lineage.
- **Generation/lineage source:** fact carries the exact expected active `ShipLifetimeGeneration`; coordinator checks it and assigns the terminal `LedgerOrdinal`. `CreateShipInvalidated` also requires an explicit stable invalidation reason. Open passenger epochs are closed by coordinator terminal handling, not guessed from local cleanup.
- **Ordering/terminal guarantees:** invalidation is after registration and any accepted ordered attach/detach receipts, is terminal exactly once, records the reason and receipt ordinal, clears active passenger generations and rejects every later fact for that lifetime. Recreate is a new `ShipRegistered`/new coordinator lifetime, never a continuation.
- **Mapping into `TryAccept`:** reviewed producer builds `CreateShipInvalidated(shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, invalidationReason, true, true)` and calls `TryAccept`; the terminal coordinator receipt is then losslessly mapped to the downstream handoff, with no direct host callback.
- **Fail-closed:** absent stable identity, matching lifetime, explicit reason, server/protocol ownership or terminal authority means no invalidation fact. `OnNetworkDespawn`, `IsSpawned=false`, object destruction, NavMesh unregister, crew-map cleanup, callback order and `NetworkObjectId` reuse cannot independently prove terminal protocol invalidation. Duplicate, stale, out-of-order or post-terminal facts are rejected.

## 4. Decision outcome

```text
complete legitimate lifecycle producer = NOT FOUND
ShipRegistered source seam = CANDIDATE / NOT OWNER-BOUND
PassengerAttached source seams = CANDIDATE / NOT OWNER-BOUND
PassengerDetached source seam = CANDIDATE / NOT OWNER-BOUND
ShipInvalidated source seams = SPLIT / NO UNIFIED TERMINAL OWNER
coordinator TryAccept mapping = DEFINED / NOT EXECUTED
coordinator and handoff = DORMANT / UNBOUND
provider binding = NOT EXECUTED
adapter registration = NOT EXECUTED
BootstrapScene = UNCHANGED
scenes/prefabs = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
search outside audited paths = INCONCLUSIVE
integration decision = BLOCKED
```

Explicitly rejected inference sources are: `IsSpawned`; `NetworkObjectId` as stable identity or generation; Unity/NGO object references; callback order; `ShipDeckNav.RegistrationGeneration`; and host-local binding generation. They may be supplemental observations only where a future reviewed protocol owner supplies the real facts.

The dormant coordinator, coordinator-to-downstream mapping/handoff and combined host remain untouched and unbound. No provider/adapter/`BootstrapScene` binding, scene/prefab change or runtime configuration change was made. No runtime evidence was collected.

## 5. Exact changed files

```text
docs/world/floatingorigin/06CQ_SHIP_DECK_LIFECYCLE_FACT_SOURCE_OWNERSHIP_AND_INGRESS_DECISION_GATE.md
docs/world/floatingorigin/06CQ_SHIP_DECK_LIFECYCLE_FACT_SOURCE_OWNERSHIP_AND_INGRESS_DECISION_GATE.json
docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md
Assets/_Project/Docs/ITERATIONS.md
```

Only these four documentation files belong to T-FO06CQ. C# scripts, scenes, prefabs, `BootstrapScene` and runtime configuration were not modified.
