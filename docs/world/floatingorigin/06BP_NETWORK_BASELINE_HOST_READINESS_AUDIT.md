# T-FO06BP — protocol-owned NetworkBaseline host readiness audit

Дата: 2026-09-12  
Статус: **AUDITED / BLOCKED / NO RUNTIME CHANGE**

## 1. Назначение

После `T-FO06BO` проверена возможность реализовать concrete `IGlobalMotionNetworkBaselineTransactionHost` поверх существующего NGO motion layer. Цель — отличить доступное observation/control API от transaction-safe protocol-owned capture/restore.

## 2. Проверенные существующие seams

В `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionReplicator.cs` существуют:

- `Control` и `TryReadServerAcceptedMotion(...)` для чтения текущего server-accepted baseline;
- `ActivateWorldServer(...)` и `ActivateParentLocalServer(...)` для публикации нового stream;
- `AcknowledgeBaselineApplied(...)` для подтверждения применения baseline;
- `StopServer()` для остановки публикации;
- `OnOwnershipChanged(...)`, который инвалидирует старый owner validator и останавливает stream;
- `OnNetworkDespawn()` и `OnNetworkObjectParentChanged(...)` lifecycle guards.

Эти API достаточны для наблюдения и controlled reactivation, но не являются reversible transaction host.

## 3. Необходимые возможности, которых нет

Текущий `GlobalMotionReplicator` не предоставляет protocol-owned host API для:

1. immutable capture ownership/lifetime state с exact `NetworkObject` spawn generation;
2. capture и restore control revision, binding authority/discontinuity generations и acknowledgement lineage;
3. server-coordinated ownership restore с подтверждением фактического owner;
4. reversible spawn/despawn/lifetime restoration;
5. transaction-scoped restore после частичного `Apply/Rebuild/Validate/Publish`;
6. sealed evidence, связывающего перечисленные операции с `GlobalMotionNetworkBaselineReversibleCapability`.

Private `_serverControl`, `_serverSession`, `_baselineApplied` и `_ownerPoseValidator` нельзя считать host contract: внешний adapter не может безопасно читать или восстанавливать их через текущий public API.

## 4. Решение

Concrete protocol-owned host на текущем API **не реализован**. Нельзя:

- собирать Restorable evidence из `Control` или `[T-FO06Y]` log-only capture;
- выдавать `NativeReady=true` на основании initial baseline/acknowledgement;
- трактовать `StopServer()` и повторный `Activate*Server(...)` как rollback;
- использовать `ChangeOwnership`/despawn-recreate как локальную замену reversible protocol transaction;
- seal/register `GlobalMotionNativeAdapterSet` или bind provider.

Следующий допустимый design gate — отдельный reviewed NGO protocol change с server-owned transaction ledger и explicit capture/restore receipts. До такого изменения `GlobalMotionNetworkBaselineNativeAdapter` остаётся seam-only.

## 5. Проверки и граница

```text
source audit = COMPLETE for GlobalMotionReplicator.cs public lifecycle seams
concrete protocol-owned host = NOT IMPLEMENTED / BLOCKED
NetworkBaseline adapter = SEAM ONLY
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

`check_compile_errors` и `git diff --check` выполняются после подготовки документационного этапа. Никакой Unity state, NGO state, scene, prefab или runtime installation этим этапом не изменяется.
