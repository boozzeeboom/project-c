# T-FO06BS — NGO ownership/lifetime producer census

Дата: 2026-09-12  
Статус: **AUDITED / BLOCKED / NO RUNTIME CHANGE**

## 1. Назначение

После `T-FO06BR` выполнен расширенный source census по `Assets/_Project/Scripts` для поиска прямых NGO ownership и spawn/lifetime producers, которые могли бы быть server-owned host-ом для T-FO06BQ ledger.

## 2. Ownership mutation

Поиск прямых вызовов:

```text
ChangeOwnership
RemoveOwnership
```

в `Assets/_Project/Scripts` не нашёл совпадений.

`GlobalMotionReplicator.OnOwnershipChanged(...)` является lifecycle callback и не выполняет ownership mutation. Поэтому source tree не предоставляет обнаруженного server-owned ownership mutation seam, к которому можно безопасно привязать ledger restore.

## 3. Найденные Spawn/Despawn producers

Поиск `.Spawn(...)`/`.Despawn(...)` нашёл следующие пути:

```text
AI/NpcLootPickup.cs
AI/NpcSpawner.cs
Combat/Client/DamageNumberService.cs
Combat/Implementations/NpcTarget.cs
Core/ServerStormManager.cs
PeacefulShip/Crew/ShipCrewSpawner.cs
World/Streaming/ChunkNetworkSpawner.cs
World/Scene/ScenePlacedObjectSpawner.cs
World/FloatingOrigin/Network/GlobalMotionPlayerBootstrap.cs
World/FloatingOrigin/Network/GlobalSceneNativeExecutor.cs
World/Chest/NetworkChestContainer.cs
```

Эти producers принадлежат разным доменам: NPC, loot, combat effects, ships/crew, streaming, scene execution, global player bootstrap и chests. Общего transaction ledger или общего lifecycle owner-а между ними не найдено.

## 4. Lifecycle callback spread

`OnNetworkSpawn`/`OnNetworkDespawn` присутствуют в player, NPC, ship, docking, combat, crafting, resource, streaming и floating-origin компонентах. Callback spread подтверждает, что сборка receipts из отдельных callbacks не будет protocol-atomic.

Особенно важные наблюдения:

- `GlobalMotionPlayerBootstrap` управляет только подготовленным player spawn path;
- `GlobalSceneNativeExecutor` имеет отдельные scene-object retirement receipts;
- `NpcSpawner`, `ShipCrewSpawner` и `ChunkNetworkSpawner` являются независимыми producers;
- `GlobalMotionReplicator` не владеет их spawn/lifetime state.

## 5. Решение

Не найдено оснований выбирать существующий actor/spawner как concrete protocol-owned NetworkBaseline host. Не добавлялись synthetic ownership calls, reflection в NGO internals, переподключение через despawn/recreate или adapter registration.

Следующий design gate должен явно определить server-owned lifecycle coordinator/ledger owner, который получает protocol-level receipts до и после ownership/lifetime операций. До этого T-FO06BQ остаётся pure contract.

```text
ownership mutation seam = NOT FOUND IN AUDITED PROJECT SCRIPTS
lifetime producers = MULTIPLE / NOT UNIFIED
NetworkBaseline adapter = SEAM ONLY
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

## 6. Неопределённость

Census покрывает `Assets/_Project/Scripts`. NGO package internals, generated code, serialized scene callbacks и external tooling не заменяют найденный server-owned contract и остаются вне доказанной области.
