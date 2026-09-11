# T-FO06AH — runtime driver readiness authorization

Дата: 2026-09-11.

## Назначение тикета

`T-FO06AH` выбран как следующий свободный подэтап после T-FO06AG; существующего отчёта `06AH` в floating-origin документах не найдено.

Цель — интегрировать aggregated readiness bundle с explicit runtime driver boundary, сохранив fail-closed поведение.

## Изменение

Обновлён:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeDriver.cs
```

Добавлены:

- поле текущего `GlobalMotionRebaseReadinessBundle`;
- `IsReadinessAuthorized`;
- `Readiness`;
- `TryAuthorizeReadiness(...)`.

Перед `TryRequestUserControlled(...)` теперь требуется успешная явная авторизация валидного readiness bundle. Если bundle отсутствует или не готов, driver отказывает до создания rebase transaction и пишет:

```text
readiness_bundle_not_authorized
```

Авторизация возможна только в `Idle` и только для `bundle.IsReady == true`. `TryReset` очищает authorization state.

## Что это интегрирует

```text
GlobalMotionRebaseReadinessBundle
→ GlobalMotionRebaseRuntimeDriver.TryAuthorizeReadiness
→ TryRequestUserControlled
```

Это только admission boundary. Concrete adapter Apply/Rebuild/Validate/Publish остаётся неподключённым.

## Scope boundary

Этап не:

- создаёт valid readiness bundle;
- публикует live manifest;
- обнаруживает participants;
- создаёт native adapters;
- меняет Transform/Rigidbody/NavMesh/camera/NGO state;
- вызывает Unity mutation;
- реализует rollback;
- добавляет driver в BootstrapScene;
- запускает Play Mode.

При текущем состоянии проекта `TryAuthorizeReadiness` закономерно отвергнет blocked bundle, а `TryRequestUserControlled` не начнёт transaction без authorization.

## Проверка

```text
check_compile_errors = No compile errors
```

Рабочее дерево после коммита должно быть чистым. Play Mode не запускался.

## Текущее состояние gates

```text
readinessBundle = BLOCKED
readinessAuthorization = NOT_GRANTED
participantAdmission = NOT_READY
liveManifestPublication = NOT_PROVEN
nativeApplyPipeline = NOT_CONNECTED
runtimeRebaseReadiness = NOT_READY
```

## Следующий этап

Получить фактический user-controlled runtime evidence для создания valid readiness bundle. Только после этого можно отдельно рассматривать concrete adapter connection; автоматический trigger и native mutation остаются запрещёнными.
