# T-FO06AM — driver installation identity evidence

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AM` выбран как следующий свободный подэтап после `T-FO06AL`; поиск по floating-origin документации и tracked-файлам не обнаружил существующего отчёта `06AM`.

Цель — усилить driver admission boundary: installation authorization становится одноразовой в текущем driver lifecycle, а installation ID передаётся в `rebase.Requested` evidence.

## Реализация

Обновлён файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeDriver.cs
```

Изменения:

- повторная выдача installation authorization до `TryReset` блокируется ошибкой:

```text
installation_authorization_already_granted
```

- `rebase.Requested` теперь содержит `installation=<InstallationId>`;
- installation identity связывается с user-controlled request evidence;
- `TryReset` остаётся единственным переходом, который очищает installation authorization.

## Что это интегрирует

```text
InstallationIntent.InstallationId
→ TryAuthorizeInstallationIntent
→ rebase.Requested evidence
→ user-controlled transaction boundary
```

Это предотвращает повторное переиспользование одной installation authorization в рамках текущего driver lifecycle и делает связь intent/request наблюдаемой в evidence buffer.

## Scope boundary

Этап не:

- устанавливает runtime driver в сцену;
- вызывает concrete native adapters;
- изменяет Transform/Rigidbody/NavMesh/camera/NGO state;
- выполняет Apply/Rebuild/Validate/Publish;
- реализует rollback;
- добавляет automatic trigger;
- запускает Play Mode.

Coordinator по-прежнему останавливается на `Captured`, а `nativeApplyPipeline` остаётся `NOT_CONNECTED`.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
```

Play Mode не запускался. Runtime rebase readiness остаётся `NOT_READY`.
