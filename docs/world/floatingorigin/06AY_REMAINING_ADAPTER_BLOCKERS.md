# T-FO06AY — remaining native adapter blocker review

Дата: 2026-09-12.

## Назначение тикета

Проверить, можно ли без архитектурных допущений продолжить concrete native adapter integration после `T-FO06AX`. По результатам source review реализация `ShipDeckNav` и `NetworkBaseline` adapters отложена fail-closed.

## ShipDeckNav

Фактическая реализация находится в:

```text
Assets/_Project/Scripts/Ship/ShipDeckNav.cs
```

Обнаруженные runtime-state boundaries:

- native `NavMeshDataInstance` validity;
- registration state и registration failure state;
- cached nav-frame origin и last registered ship position;
- asynchronous round-robin registration queue;
- proxy agent readiness и passenger attachment state.

`ShipDeckNavReadinessContract` подтверждает только evidence, но не предоставляет transaction-scoped capture, synchronous rebuild или rollback API.

### Blocker

`IGlobalMotionNativeAdapter.TryRebuild/TryRestore` являются синхронными transaction calls, а текущая registration lifecycle работает через deferred queue. Прямой вызов `NavMesh.AddNavMeshData` из adapter создаст новый uncontrolled lifecycle и не докажет корректный rollback/ownership.

Дополнительно `Descriptor.NativeReady` нельзя честно выставить по одной регистрации: текущий readiness gate требует proxy-agent, `isOnNavMesh`, explicit passenger attachment и provenance evidence. Эти данные ещё не являются sealed live participant evidence.

### Решение

Не создавать `GlobalMotionShipDeckNavNativeAdapter` до отдельного reviewed lifecycle seam, который явно определит:

1. synchronous transaction-safe registration/rebuild boundary;
2. snapshot/restore для `NavMeshDataInstance` и nav-frame origin;
3. completed passenger/proxy readiness admission;
4. rollback behavior при частичной регистрации или отказе.

## NetworkBaseline

В audited source path отсутствует отдельный concrete baseline adapter и отдельный readiness evidence contract. NGO ownership/lifetime, NetworkObject identity, spawn generation, tick ordering и baseline acknowledgement принадлежат network protocol layer.

### Blocker

Adapter не может восстанавливать NGO ownership или lifecycle state локально без нарушения server-authoritative protocol. Observation-only implementation при текущем `FullTransaction` capability будет ложным: `TryRestore` не сможет выполнить требуемый rollback.

Также live manifest publication и participant admission ещё не доказаны, поэтому baseline identity нельзя связать с закрытым runtime participant set.

### Решение

До concrete adapter сначала нужен отдельный pure contract для:

- NetworkObject identity и ownership generation;
- spawn/lifetime generation;
- baseline tick/sequence continuity;
- acknowledgement lineage;
- explicit observation-only versus restorable state classification.

После этого необходимо решить, допускает ли adapter contract observation-only domain либо требует отдельного capability/rollback класса. Текущий `FullTransaction` менять без reviewed contract decision нельзя.

## Итоговый gate

```text
Transform adapter = implemented
Rigidbody adapter = implemented
CameraHistory adapter = implemented
ShipDeckNav adapter = BLOCKED
NetworkBaseline adapter = BLOCKED
live manifest publication = NOT_PROVEN
participant admission = NOT_READY
runtime installation = NOT_CONNECTED
runtime rollback evidence = NOT_OBSERVED
runtimeRebaseReadiness = NOT_READY
Play Mode = not run
```

Следующий допустимый этап — отдельный pure NetworkBaseline readiness/rollback capability contract и отдельный ShipDeckNav synchronous lifecycle design. Concrete adapters, registration в `GlobalMotionNativeAdapterSet`, BootstrapScene installation и `Apply/Rebuild/Validate/Publish` остаются запрещёнными до закрытия этих blockers.
