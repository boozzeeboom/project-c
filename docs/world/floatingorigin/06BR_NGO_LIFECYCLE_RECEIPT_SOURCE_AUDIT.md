# T-FO06BR — NGO lifecycle receipt source audit

Дата: 2026-09-12  
Статус: **AUDITED / BLOCKED / NO RUNTIME CHANGE**

## 1. Назначение

После `T-FO06BQ` проверено, где фактически находятся NGO spawn, despawn, ownership и lifetime seams, необходимые для наполнения server-owned NetworkBaseline ledger receipts.

Аудит ограничен исходниками, связанными с global motion pilot, player lifecycle, respawn и NPC spawn. Это не утверждение о полном закрытом мире проекта.

## 2. Подтверждённые lifecycle источники

### GlobalMotionReplicator

`GlobalMotionReplicator`:

- читает accepted baseline через `TryReadServerAcceptedMotion(...)`;
- публикует новый baseline через `ActivateWorldServer(...)`/`ActivateParentLocalServer(...)`;
- сбрасывает внутреннее состояние в `OnNetworkDespawn()`;
- инвалидирует owner validator в `OnOwnershipChanged(...)`;
- останавливает stream при ownership и parent changes через `StopServer()`.

Это transport/lifecycle surface, но не transaction ledger и не источник reversible ownership/lifetime receipts.

### NetworkPlayer

`NetworkPlayer`:

- сбрасывает coordinate lifetime в `OnNetworkSpawn()` и `OnNetworkDespawn()`;
- сохраняет global spawn latch через network spawn;
- различает scene-placed `PlayerSpawner` и настоящий player clone через component marker;
- имеет player-specific cleanup при despawn.

В этих seams нет protocol-owned capture/restore exact spawn generation, ownership generation или server transaction identity.

### NPC spawn

`NpcSpawner` создаёт runtime `NetworkObject`, добавляет `NpcGroupController` и вызывает `netObj.Spawn(destroyWithScene: true)`. Это подтверждает наличие runtime spawn/lifetime producers, но не предоставляет reversible transaction receipt или restore ledger.

### Respawn

`PlayerRespawnTracker.TeleportToClientRpc(...)` напрямую изменяет `transform.position`, временно отключает `CharacterController`, вызывает `Physics.SyncTransforms()` и затем завершает respawn. Это positional correction path, а не NGO spawn/lifetime rollback. Он должен быть отделён от NetworkBaseline lifetime receipt.

## 3. Отсутствующие seams

В audited paths не найден единый protocol-owned producer, который атомарно выдаёт и восстанавливает:

1. exact `NetworkObject` spawn/lifetime generation;
2. owner client и ownership generation;
3. `GlobalMotionReplicator` control revision и binding generations;
4. acknowledgement lineage;
5. despawn/spawn or pooled-lifetime transition receipt;
6. rollback receipt после частично выполненного transaction;
7. receipt, связывающий ownership/lifetime state с `T-FO06BQ` transaction identity.

`NetworkPlayer` coordinate lifetime reset, `NpcSpawner.Spawn(...)`, `OnNetworkDespawn()` и respawn position write нельзя объединять в один synthetic host без reviewed protocol change.

## 4. Решение

Concrete host и adapter registration не создаются на основании этого аудита. Следующий implementation gate должен выбрать один server-owned NGO lifecycle owner и добавить в него explicit transaction ledger integration, а не собирать состояние из отдельных actor callbacks.

До этого:

```text
NetworkBaseline protocol ledger = PURE CONTRACT ONLY
GlobalMotionNetworkBaselineNativeAdapter = SEAM ONLY
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

## 5. Неопределённость

Поиск выполнен по audited global-motion/player/NPC paths. Полный проектный census всех ownership mutation, pooled spawn и scene-object lifecycle producers не выполнялся; альтернативные producers вне этих путей остаются **INCONCLUSIVE** и не могут быть приняты как host.
