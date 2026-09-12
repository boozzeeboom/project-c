# T-FO06CP — ShipDeck lifecycle coordinator explicit fact-source binding readiness audit

Дата: 2026-09-12  
Статус: **DOCS-ONLY / AUDITED / INCONCLUSIVE / INTEGRATION-BLOCKED / RUNTIME-BINDING-BLOCKED**

## 1. Цель и граница

После `T-FO06CO` выполнен read-only audit готовности explicit fact-source binding для protocol-owned `GlobalMotionShipDeckPassengerLifecycleCoordinator`. Gate проверяет, какие существующие server-side seams могут поставлять reviewed facts, и сверяет их с четырьмя lifecycle ingress phases. Это документационный gate: реализация, caller binding и runtime integration не выполнялись.

В отчёте не объявляется, что существующие callbacks уже являются lifecycle receipts. Только explicit, server-authorized и protocol-owned facts могут войти в coordinator. Stable protocol identity, lifetime/attachment generations, ownership, order и terminal invalidation должны быть переданы как факты, а не выведены из текущего object state.

## 2. Audited source paths и exact seams

Проверенные пути:

```text
Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs
Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs
Assets/_Project/Scripts/AI/NpcBrain.cs
Assets/_Project/Scripts/Ship/ShipDeckNav.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckCombinedTransactionHost.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleCoordinator.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleProducerContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.cs
```

### `NpcShipController`

- Server-only `OnNetworkSpawn` регистрирует NPC ship в `NpcShipZoneRegistry` и `NpcShipWorld`.
- Server-only `OnNetworkDespawn` снимает эти регистрации; это seam окончания текущего ship lifecycle, но не protocol receipt.
- При отсутствии explicit `npcInstanceId` текущий код выводит его из `NetworkObjectId`. Это наблюдаемая fallback identity, а не stable protocol `ShipId` и не ship lifetime generation.
- Seam может быть источником reviewed `ShipRegistered`/ship-invalidating fact только после явной server/protocol binding boundary; сам компонент не выдаёт coordinator receipt, monotonic lifetime generation или terminal ledger order.

### `ShipCrewSpawner`

- Server-only `OnNetworkSpawn` запускает `EnsureCrewSpawned`.
- `SpawnMember` создаёт crew `NetworkObject` и вызывает NGO `Spawn(destroyWithScene: true)`; это accepted spawn seam для crew/passenger candidate.
- Existing crew переиспользуется текущими runtime checks; stale entries удаляются из локальной map по `IsSpawned`; server-only `OnNetworkDespawn` очищает map.
- Эти seams не являются protocol-owned `PassengerAttached`/`PassengerDetached` receipts: spawner не ведёт explicit attachment generation, passenger identity ledger, ship lifetime generation, ordered receipt sequence или terminal `ShipInvalidated`.

### `NpcBrain`

- Server-only `AttachToShipDeck` задаёт requested/active attachment и требует spawned ship, успешного parent attachment и `ShipDeckNav.IsReady`.
- Server-only `DetachFromShipDeck` очищает attachment/ride state; это explicit detach seam, но не immutable coordinator receipt.
- Attachment flags, parent/object references и текущий `NetworkObjectId` описывают local/native state, но не создают stable passenger protocol identity или attachment generation.
- Возможный ingress — только reviewed explicit attach/detach completion fact, включающий stable `ShipId`, stable passenger identity, explicit lifetime/attachment generations, ownership и coordinator ordering.

### `ShipDeckNav`

- Server-side `OnNetworkSpawn` ставит NavMesh registration в asynchronous queue.
- `IsReady` появляется после успешной NavMesh registration; это readiness prerequisite для `PassengerAttached`, не lifecycle ledger owner.
- `OnNetworkDespawn` выполняет unregister; asynchronous registration/unregister order не заменяет protocol order.
- `RegistrationGeneration` относится к NavMesh registration lifecycle. Он не является ship lifetime generation, passenger attachment generation или coordinator ledger ordinal и не может быть преобразован в них.

### `GlobalMotionShipDeckCombinedTransactionHost`

- Host имеет explicit reviewed `NpcBrain[]` binding через `TryConfigureReviewedPassengerLifecycleBinding` и `TryValidateReviewedPassengerLifecycleBinding`.
- Ordered transaction seams `TryCapture`, `TryRebuild`, `TryValidate`, `TryRestore` используют reviewed lifecycle binding receipt и combined snapshot/result boundaries.
- Host должен получать coordinator-produced reviewed lifecycle binding receipt, а не прямые callbacks `NpcShipController`, `ShipCrewSpawner`, `NpcBrain` или `ShipDeckNav`.
- Host-local passenger binding generation — это binding/snapshot provenance только этого host. Она не является stable protocol identity, ship lifetime generation или coordinator attachment generation.

Ни один из перечисленных seams сам по себе не доказывает complete source binding. Поиск за пределами этих audited paths остаётся **INCONCLUSIVE**.

## 3. Explicit four-phase ingress matrix

| Phase | Required explicit ingress fact | Coordinator acceptance/output | Fail-closed rejection |
|---|---|---|---|
| `ShipRegistered` | Server подтверждает accepted ship registration boundary от `NpcShipController`; explicit stable protocol `ShipId`, supplemental `ShipNetworkObjectId` только для cross-check, server/protocol ownership и новая lifetime boundary. | Открывает новый ship ledger; coordinator выдаёт strictly monotonic, never-reused `ShipLifetimeGeneration` и первый coordinator ledger ordinal. | Отсутствуют stable `ShipId`, ownership или registration fact; identity выведена из `IsSpawned`/`NetworkObjectId`; duplicate registration, stale lifetime, non-first order или callback-only observation. |
| `PassengerAttached` | Server подтверждает accepted crew/passenger ingress от `ShipCrewSpawner` и completed attachment от `NpcBrain`; stable passenger identity, matching `ShipId`/lifetime, explicit new `AttachmentGeneration`, parent/attachment completion, `ShipDeckNav.IsReady`, server/protocol ownership. | Открывает active passenger attachment epoch; coordinator выдаёт strictly monotonic attachment generation и следующий contiguous ledger ordinal. | Нет stable passenger identity, matching ship lifetime, explicit generation/ownership, completed attach или NavMesh readiness; duplicate active attach, stale/mismatched lineage, inferred generation или out-of-order fact. |
| `PassengerDetached` | Server подтверждает completed detach от `NpcBrain` для конкретной active ship/passenger attachment epoch; повторяет stable identities, matching lifetime и exact active `AttachmentGeneration`, ownership и next ordinal. | Закрывает именно active attachment epoch; source generation для passenger становится unavailable. Следующий attach требует строго большую explicit generation. | Detach без active matching attachment; stale/mismatched generation, identity/ownership mismatch, cleanup-map/`IsSpawned=false` inference, duplicate detach или non-contiguous order. |
| `ShipInvalidated` | Server/protocol coordinator принимает terminal ship despawn/invalidation boundary после ship lifecycle seam; stable `ShipId`, matching lifetime, explicit invalidation reason, ownership, last/next ledger order и explicit accounting незакрытых passenger epochs. | Закрывает ship lifetime, фиксирует terminal invalidation и последний ledger order. После terminal event новые attach/detach или повторная invalidation запрещены; recreate требует новой lifetime generation. | Нет explicit reason/identity/lifetime/ownership, invalidation не terminal, stale/duplicate/out-of-order fact или попытка синтетически заменить invalidation набором detach callbacks. |

Допустимый общий порядок: `ShipRegistered` → zero or more ordered `PassengerAttached`/`PassengerDetached` pairs → terminal `ShipInvalidated`. Все receipts должны проверяться вместе по stable identity, generations, server/protocol ownership, contiguous order и terminal state.

## 4. Required provenance and prohibited inference

Обязательные свойства будущего fact source binding:

- stable protocol `ShipId`, не равный автоматически текущему Unity/NGO object identity;
- stable protocol passenger identity для каждой attachment epoch;
- explicit coordinator-owned ship lifetime generation;
- explicit coordinator-owned passenger attachment generation;
- server authority и protocol ownership на каждом accepted fact;
- strict coordinator ledger ordering, включая terminal invalidation;
- matching ship/passenger/lifetime/attachment lineage и fail-closed stale/duplicate/post-terminal handling.

Запрещено делать source binding или generation synthesis из:

- `IsSpawned`;
- `NetworkObjectId` как stable identity или generation;
- Unity/NGO object references;
- `ShipDeckNav.RegistrationGeneration`;
- callback order;
- host-local binding generation combined host-а.

## 5. Inherited validation evidence (не runtime evidence)

Из предыдущих gates наследуются только результаты pure validation:

```text
T-FO06CO = 7/7 pure checks PASS
T-FO06CN = 11/11 pure checks PASS
```

Они подтверждают contract/mapping/coordinator validation only. Они не доказывают runtime source binding, caller invocation, live producer output, NGO ordering, attachment completion в реальной сессии, provider/adapter readiness или rollback capability.

T-FO06CP сам не добавляет runtime validator и не переисполняет эти проверки; в этом docs-only gate нет нового runtime evidence.

## 6. Gate status and blockers

```text
explicit fact-source binding readiness = INCONCLUSIVE / NOT READY
runtime caller binding = NOT EXECUTED
NpcShipController binding = NOT EXECUTED
ShipCrewSpawner binding = NOT EXECUTED
NpcBrain binding = NOT EXECUTED
ShipDeckNav binding = NOT EXECUTED
combined transaction host binding = NOT EXECUTED
provider/adapter registration = NOT EXECUTED
BootstrapScene = UNCHANGED
scenes/prefabs = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
search outside audited paths = INCONCLUSIVE
```

Нет runtime caller binding, provider/adapter/`BootstrapScene` change или Play Mode evidence. До отдельного reviewed implementation/binding gate coordinator не может быть объявлен bound, а `GlobalMotionNativeAdapterSet` и rebase readiness не могут быть открыты.

## 7. Exact changed files

```text
docs/world/floatingorigin/06CP_SHIP_DECK_LIFECYCLE_COORDINATOR_FACT_SOURCE_BINDING_READINESS_AUDIT.md
docs/world/floatingorigin/06CP_SHIP_DECK_LIFECYCLE_COORDINATOR_FACT_SOURCE_BINDING_READINESS_AUDIT.json
docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md
Assets/_Project/Docs/ITERATIONS.md
```

Только эти четыре документа относятся к T-FO06CP; C# scripts, scenes, prefabs, `BootstrapScene` и runtime configuration не изменялись.
