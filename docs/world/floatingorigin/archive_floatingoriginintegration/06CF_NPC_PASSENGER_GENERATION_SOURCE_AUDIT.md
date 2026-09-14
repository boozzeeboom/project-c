# T-FO06CF — NpcBrain passenger generation source audit

Дата: 2026-09-12  
Статус: **AUDITED / SOURCE NOT FOUND / INTEGRATION BLOCKED**

## 1. Цель

Проверить, может ли существующий server-side `NpcBrain` explicit ship attachment lifecycle быть legitimate producer-ом для `IGlobalMotionShipDeckPassengerGenerationSource` после `T-FO06CE`.

Аудит не изменяет `NpcBrain`, `ShipDeckNav`, NGO state или runtime integration.

## 2. Проверенные источники

```text
Assets/_Project/Scripts/AI/NpcBrain.cs
Assets/_Project/Scripts/AI/Editor/NpcBrainEditor.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerGenerationContract.cs
Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs
Assets/_Project/Scripts/Player/ShipController.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseShipDeckNavLifecycleContract.cs
```

## 3. Подтверждённые seams

`NpcBrain` действительно предоставляет server-only explicit attachment path:

- `AttachToShipDeck(NetworkObject, ShipDeckNav)` сохраняет ship/deck references;
- `_explicitShipAttachmentRequested` и `_explicitShipAttachmentActive` разделяют request и completed parent attachment;
- `TickExplicitShipAttachment()` проверяет `NetworkObject.IsSpawned`, выполняет `TrySetParent` и ждёт `ShipDeckNav.IsReady`;
- `TryCaptureFloatingOriginShipDeckSnapshot()` требует server authority, spawned ship, active attachment и proxy NavMesh readiness;
- `DetachFromShipDeck()` очищает attachment state.

Эти seams подтверждают текущую attachment readiness, но не создают protocol generation receipts.

## 4. Чего нет

В audited paths не найдено:

- реализации `IGlobalMotionShipDeckPassengerGenerationSource`;
- monotonic `ShipSpawnGeneration` producer-а;
- monotonic `AttachmentGeneration` producer-а;
- server-owned lifecycle receipt, связывающего ship spawn/despawn с passenger attachment;
- invalidation правила для повторного NGO spawn того же logical ship;
- generation fields или reviewed binding в `NpcBrainEditor`.

`AttachedShipNetworkObjectId`, `IsSpawned`, `IsExplicitShipAttachmentActive`, `ShipDeckNav.IsReady` и существующий `GlobalMotionActorLink` lifetime state не являются заменой требуемым generation receipts.

## 5. Вывод

`NpcBrain` нельзя безопасно адаптировать к `IGlobalMotionShipDeckPassengerGenerationSource` только добавлением `TryGetGeneration()` поверх существующих полей. Это создало бы synthetic generation из object reference/`IsSpawned` и нарушило fail-closed контракт `T-FO06CD`.

Следующий legitimate gate — отдельный server-owned lifecycle ledger/producer, который получает explicit ship lifetime and attachment transitions и выдаёт immutable generation receipts. Этот producer должен быть связан с attach/detach/spawn lifecycle, а не вычислять generation в snapshot capture.

## 6. Границы и неопределённость

```text
IGlobalMotionShipDeckPassengerGenerationSource implementation = NOT FOUND
server-owned ship lifetime producer = NOT FOUND
NpcBrain integration = NOT EXECUTED
ShipDeckNav integration = NOT EXECUTED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Поиск за пределами перечисленных audited paths, включая остальные spawn/despawn producers и package/generated NGO internals, остаётся **INCONCLUSIVE**.

## 7. Следующий gate

Спроектировать и отдельно проверить protocol-owned server lifecycle producer contract для ship lifetime и passenger attachment generations. До появления такого producer нельзя выдавать `GlobalMotionShipDeckPassengerGeneration`, подключать ledger к `NpcBrain` или открывать `NativeReady`.
