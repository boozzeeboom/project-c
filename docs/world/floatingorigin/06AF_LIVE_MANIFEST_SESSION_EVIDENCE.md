# T-FO06AF — live manifest session evidence gate

Дата: 2026-09-11.

## Назначение тикета

`T-FO06AF` выбран как следующий свободный подэтап после T-FO06AE; существующего отчёта `06AF` в floating-origin документах не найдено.

Цель — отделить pure receipt eligibility от фактического наблюдения manifest в конкретной runtime session.

## Реализация

Создан:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseLiveManifestSessionEvidence.cs
```

Добавлены:

- `GlobalMotionRebaseLiveManifestSessionEvidence`;
- `GlobalMotionRebaseLiveManifestSessionEvidenceGate.TryValidate`.

## Required session evidence

Gate требует одновременно:

- valid `GlobalMotionRebaseLiveManifestReceipt`;
- совпадение manifest digest и entry count;
- session identity;
- publisher observation;
- server acceptance;
- peer digest match;
- peer entry-count match;
- отсутствие manifest drift;
- `ObservedPeerCount > 0`.

Pure receipt сам по себе не считается live publication.

## Scope boundary

Этап не:

- обнаруживает peers или Unity objects;
- публикует manifest через NGO/transport;
- создаёт runtime participant entries;
- подключает runtime driver;
- создаёт native adapters;
- выполняет Apply/Rebuild/Validate/Publish;
- изменяет Unity state;
- реализует rollback.

Evidence должен поступить из отдельного user-controlled runtime capture либо из явно reviewed session source. Автоматический Play Mode этим этапом не запускается.

## Текущее состояние

Фактического session evidence в этом этапе не предоставлено, поэтому следующие gates остаются закрытыми:

```text
participantAdmission = NOT_READY
liveManifestPublication = NOT_PROVEN
runtimeAdapterReady = NOT_READY
runtimeProofComplete = false
rollbackReady = false
runtimeRebaseReadiness = NOT_READY
```

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode не запускался.

## Следующий этап

Получить user-controlled runtime capture с конкретными session identity, publisher/server acceptance, peer digest и entry-count evidence. До этого не переводить `liveManifestPublication` в `true` и не подключать native adapter set к runtime driver.
