# T-FO06BC — live manifest runtime bridge

Дата: 2026-09-12.

## 1. Назначение

Это первый кодовый этап после observational/source-only reviews. Добавлен runtime bridge для server-authoritative публикации reviewed participant manifest и peer digest/count acknowledgement.

Bridge публикует evidence only. Он не устанавливает native adapters, не подключает `GlobalMotionRebaseRuntimeDriver`, не запускает rebase transaction и не изменяет BootstrapScene.

## 2. Реализация

Созданы:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseLiveManifestRuntimeBridge.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionRebaseLiveManifestRuntimeBridge.cs
```

### Runtime source contract

`IGlobalMotionRebaseLiveManifestRuntimeSource` требует от owner-reviewed runtime source:

- фактически собрать closed-world `GlobalMotionRebaseParticipantManifest`;
- вернуть `GlobalMotionRebaseParticipantAdmissionEvidence`;
- вернуть ошибку и остановить публикацию при неполном источнике.

Bridge не создаёт manifest entries самостоятельно и не подменяет readiness значениями.

### Server publication

`GlobalMotionRebaseLiveManifestRuntimeBridge`:

1. получает manifest/admission от source;
2. проверяет required coverage через существующий manifest contract;
3. выдаёт receipt через `GlobalMotionRebaseLiveManifestReceiptSource.TryIssue`;
4. реплицирует digest, publisher, session/publication generations и entry count через NGO `NetworkVariable`;
5. отмечает server acceptance;
6. пишет `[T-FO06BC] manifest.Published`.

### Peer acknowledgement

Клиент отправляет digest/count/session/publication acknowledgement через NGO RPC. Сервер принимает только exact match текущего receipt и фиксирует:

- `PeerAccepted`;
- observed peer count;
- digest match;
- entry-count match;
- отсутствие manifest drift.

`TryGetSessionEvidence` возвращает evidence только после прохождения существующего `GlobalMotionRebaseLiveManifestSessionEvidenceGate`.

## 3. Fail-closed boundaries

Публикация отклоняется при:

- отсутствующем runtime source;
- некорректном publisher identity;
- неполном manifest coverage;
- неполной admission evidence;
- несовпадении peer digest/count;
- устаревших session/publication generations.

Bridge не имеет кода для:

- регистрации в `GlobalMotionNativeAdapterSet`;
- создания readiness bundle;
- `TryAuthorizeReadiness` или installation intent;
- `Apply/Rebuild/Validate/Publish` rebase transaction;
- rollback;
- автоматического threshold trigger.

## 4. Проверки

```text
check_compile_errors = No compile errors
Validate Live Manifest Runtime Bridge = 6 pure checks PASS / 0 FAIL
git diff --check = PASS
Play Mode = not started
BootstrapScene mutation = none
```

Pure checks покрывают required manifest coverage, receipt issue/validation, observed-peer requirement, complete session evidence и digest drift rejection.

## 5. Текущее состояние

Код bridge создан, но component не установлен в `BootstrapScene`, а concrete runtime source для фактического discovery участников ещё не подключён. Поэтому этот этап не создаёт live evidence автоматически.

```text
runtime bridge code              = IMPLEMENTED_DORMANT
runtime source binding           = NOT_CONNECTED
live manifest publication        = NOT_OBSERVED
participant admission            = NOT_READY
native adapter installation      = NOT_CONNECTED
runtimeRebaseReadiness           = NOT_READY
```

Следующий user-controlled Play Mode capture должен подтвердить `[T-FO06BC] manifest.Published`, `manifest.PeerAccepted`, одинаковые digest/count и отсутствие drift. Только после этого можно рассматривать installation boundary; adapter set и `FullTransaction` этим этапом не изменяются.
