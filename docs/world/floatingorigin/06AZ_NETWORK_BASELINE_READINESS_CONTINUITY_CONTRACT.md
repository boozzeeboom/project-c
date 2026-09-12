# T-FO06AZ — pure NetworkBaseline readiness and continuity contract

Дата: 2026-09-12. Предыдущий коммит: `70ef0cb0 T-FO06AY: document remaining adapter blockers`.

## 1. Назначение этапа

`T-FO06AZ` закрывает только спецификацию и validation boundary для `NetworkBaseline`. Контракт описывает identity, ownership/lifetime generations, baseline continuity и acknowledgement lineage без объявления concrete adapter готовым.

Этап не подключает `NetworkBaseline` к `GlobalMotionNativeAdapterSet`, runtime driver или BootstrapScene. `ShipDeckNav` остаётся отдельным lifecycle/design gate.

## 2. Реализация

Созданы:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNetworkBaselineContract.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionNetworkBaselineContract.cs
```

Добавлены pure-типы:

- `GlobalMotionNetworkBaselineIdentity` — session, `NetworkObjectId`, spawn/lifetime, owner и ownership generation;
- `GlobalMotionNetworkBaselineEvidence` — binding generations, control revision, baseline sequence, network tick, acknowledgement lineage и participant binding;
- `GlobalMotionNetworkBaselineStateClassification` — `ObservationOnly` либо `Restorable`;
- `GlobalMotionNetworkBaselineContract` — deterministic fail-closed validation и явные `IsObservationOnly`/`CanClaimFullTransaction` проверки.

## 3. Validation boundary

Evidence принимается только при одновременном наличии:

1. valid session/spawn/ownership identity;
2. совпадения baseline binding с session/object/spawn lifetime;
3. ненулевых authority и discontinuity generations;
4. ненулевого control revision и наблюдаемого network tick;
5. подтверждённой tick continuity при наличии предыдущего tick;
6. подтверждённой baseline sequence continuity при наличии предыдущего sequence;
7. baseline, ownership и spawn/lifetime observations;
8. explicit participant identity binding;
9. acknowledgement и проверенной acknowledgement lineage;
10. acknowledgement revision, не опережающей control revision.

Проверка не пытается сама восстановить NGO ownership, `NetworkObject` spawn state или server authority.

## 4. Observation-only versus restorable

`ObservationOnly` разрешён только без claims о restore capability и без restore evidence. Это описывает наблюдаемую protocol continuity, но не является native rollback adapter и не удовлетворяет текущему `FullTransaction` capability.

`Restorable` допускается только при explicit capability declaration и полном отдельном evidence для capture/apply/validate/restore. Такой evidence в этом этапе не создаётся и не связывается с реальным NGO runtime.

Таким образом:

```text
observation-only contract = IMPLEMENTED
NetworkBaseline concrete adapter = BLOCKED
FullTransaction capability change = NOT AUTHORIZED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
runtime installation = NOT_CONNECTED
```

## 5. Pure checks

Edit Mode menu:

```text
ProjectC/World/Floating Origin/Validate Network Baseline Contract
```

Результат:

```text
19 pure checks PASS / 0 FAIL
compile = No compile errors
```

Проверены identity/lifetime mismatch, authority/discontinuity generations, control revision, tick regression/continuity, sequence continuity, ownership/lifetime observations, participant binding, acknowledgement lineage, observation-only prohibition of restore claims и distinction между incomplete и complete restorable evidence.

## 6. Граница и нерешённые вопросы

Не выполнялись:

- Play Mode и user runtime capture;
- чтение или mutation реальных `NetworkObject`/NGO ownership state;
- adapter discovery/registration;
- `Apply`, `Rebuild`, `Validate`, `Publish` или `Restore` native pipeline;
- live manifest publication и participant admission;
- изменение BootstrapScene, сцен или prefabs.

Текущий контракт не доказывает post-rebase runtime continuity, NGO/physics ordering, peer agreement или rollback. Он только предотвращает смешение observation-only baseline evidence с ложным локальным восстановлением NGO state.

`runtimeRebaseReadiness=NOT_READY`.

## 7. Следующий допустимый шаг

Отдельно решить reviewed capability boundary для protocol-owned baseline: либо вводить специальный observation-only adapter capability, либо проектировать server-coordinated reversible network transaction. Нельзя помещать observation-only evidence в текущий `FullTransaction`, заполнять `GlobalMotionNativeAdapterSet` или устанавливать adapter в BootstrapScene.
