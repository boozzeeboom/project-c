# T-FO06AE — live manifest receipt source

Дата: 2026-09-11.

## Назначение тикета

`T-FO06AE` выбран как следующий свободный подэтап после T-FO06AD; существующего отчёта `06AE` в floating-origin документах не найдено.

Цель — подготовить pure evidence source для будущей participant admission/live manifest publication, не подключая его к runtime discovery или сетевой публикации.

## Реализация

Создан:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseLiveManifestReceipt.cs
```

Добавлены:

- `GlobalMotionRebaseLiveManifestReceipt`;
- `GlobalMotionRebaseLiveManifestReceiptSource.TryIssue`;
- `GlobalMotionRebaseLiveManifestReceiptSource.TryValidate`.

## Контракт receipt

Receipt содержит:

- digest reviewed participant manifest;
- publisher identity;
- session generation;
- publication generation;
- entry count.

`TryIssue` разрешает создание receipt только после:

- проверки required manifest coverage;
- проверки identity/generation tokens;
- успешного `GlobalMotionRebaseParticipantAdmissionPolicy.TryAdmit` для каждого manifest entry.

`TryValidate` повторно проверяет digest, entry count и publisher identity.

## Scope boundary

Receipt source:

- не обнаруживает Unity objects;
- не читает live scene hierarchy;
- не публикует manifest в NGO или другой transport;
- не создаёт native adapters;
- не вызывает runtime driver;
- не изменяет participant admission policy;
- не выполняет Apply/Rebuild/Validate/Publish rebase phases;
- не реализует rollback.

Создание receipt является evidence-contract операцией и не означает фактическую live publication.

## Текущее состояние gates

Из-за текущих project gates `TryIssue` не может выдать valid receipt:

```text
participantAdmission = NOT_READY
runtimeAdapterReady = NOT_READY
runtimeProofComplete = false
rollbackReady = false
liveManifestPublication = false
runtimeRebaseReadiness = NOT_READY
```

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode не запускался.

## Следующий этап

Получить user-controlled evidence source для reviewed participant admission и live publication, затем отдельно проверить receipt на фактическом runtime session. До этого не подключать receipt source к runtime driver или automatic discovery.
