# T-FO06BB — live manifest and participant admission runtime census

Дата: 2026-09-12.

## 1. Назначение

Проверить, существует ли в текущем runtime source path фактический publisher/discovery/admission bridge для live manifest, либо в проекте присутствуют только pure contracts. Этап не добавляет runtime component и не изменяет BootstrapScene.

## 2. Исследованные области

Проверены:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network
Assets/_Project/Editor/FloatingOrigin
```

Точечный source search охватывал:

- `GlobalMotionRebaseLiveManifestReceiptSource`;
- `GlobalMotionRebaseLiveManifestSessionEvidence`;
- `GlobalMotionRebaseParticipantAdmissionPolicy`;
- `GlobalMotionNativeAdapterSet`;
- `GlobalMotionRebaseRuntimeDriver`;
- `GlobalMotionRebaseUserControlledEvidenceReview`.

## 3. Результат census

### Pure-only paths confirmed

- `GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit` — pure admission policy; комментарий source прямо указывает, что runtime discovery и frame mutation не подключены.
- `GlobalMotionRebaseLiveManifestReceiptSource.TryIssue/TryValidate` — pure receipt construction/validation; receipt сам не публикуется live peer-ам.
- `GlobalMotionRebaseLiveManifestSessionEvidenceGate.TryValidate` — принимает уже полученное runtime evidence, но не обнаруживает peers и не публикует manifest.
- `GlobalMotionNativeAdapterSet` — sealed collection contract; source explicitly states, что set не discovers adapters и не вызывает Unity APIs.
- `GlobalMotionRebaseRuntimeDriver` — plain driver contract; source path не содержит live publisher/admission component.

### Runtime bridge not found

В audited source paths не найдено concrete `MonoBehaviour`/`NetworkBehaviour`, который одновременно:

1. обнаруживает runtime participants;
2. строит reviewed live manifest;
3. публикует digest/count через NGO/transport;
4. собирает server/peer acceptance;
5. создаёт participant admission evidence;
6. устанавливает native adapters в `GlobalMotionNativeAdapterSet`.

`NetworkPlayer`, `ShipController` и `NpcBrain` реализуют `IGlobalMotionActorParticipant`, но это не является live manifest publisher или participant admission receipt.

## 4. Gate result

```text
pure admission policy                 = IMPLEMENTED
pure manifest receipt source          = IMPLEMENTED
live publisher component              = NOT_FOUND
runtime participant discovery         = NOT_CONNECTED
server/peer digest agreement          = NOT_PROVEN
participant admission evidence        = NOT_READY
native adapter discovery              = NOT_CONNECTED
GlobalMotionNativeAdapterSet          = EMPTY / UNSEALED
BootstrapScene runtime installation   = NOT_AUTHORIZED
runtimeRebaseReadiness                = NOT_READY
```

## 5. Decision

Следующий runtime implementation нельзя начинать с регистрации adapters или с подмены pure evidence готовыми значениями. Сначала нужен явно reviewed runtime bridge для participant discovery, publisher/server/peer agreement и admission receipts, после чего потребуется отдельный user-controlled capture.

Текущий этап закрывает только source census и фиксирует, почему `T-FO06AQ`, `T-FO06BA` и существующие readiness gates не могут быть переведены в `READY` по наличию pure contracts.
