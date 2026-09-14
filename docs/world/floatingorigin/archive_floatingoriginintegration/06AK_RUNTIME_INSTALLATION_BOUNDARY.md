# T-FO06AK — runtime installation boundary

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AK` выбран как следующий свободный подэтап после `T-FO06AJ`; поиск по floating-origin документации и tracked-файлам не обнаружил существующего отчёта `06AK`.

Цель — зафиксировать последнюю pure boundary перед будущей serial runtime installation: только valid connection authorization и explicit user-controlled reason могут создать installation intent.

## Реализация

Создан файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeInstallationBoundary.cs
```

Добавлены:

- `GlobalMotionRebaseRuntimeInstallationIntent` — immutable intent с уникальным installation ID, evidence identity и reason;
- `GlobalMotionRebaseRuntimeInstallationBoundary.TryCreateUserControlledIntent(...)` — fail-closed boundary.

Проверки:

```text
connection authorization valid
→ explicit non-empty reason
→ user-controlled installation intent
```

Intent всегда маркируется как user-controlled. Automatic threshold trigger этим контрактом создать нельзя.

## Что это интегрирует

```text
GlobalMotionRebaseRuntimeConnectionAuthorization
        + explicit reason
        → GlobalMotionRebaseRuntimeInstallationIntent
```

Создание intent не устанавливает driver, не вызывает adapter methods и не меняет Unity state. Это только доказательство допуска к отдельному будущему installation step.

## Scope boundary

Этап не:

- устанавливает runtime driver или coordinator;
- вызывает concrete native adapters;
- обнаруживает participants или публикует manifest;
- выполняет `Apply/Rebuild/Validate/Publish`;
- реализует rollback;
- изменяет Transform/Rigidbody/NavMesh/camera/NGO state;
- изменяет сцены, префабы или runtime configuration;
- запускает Play Mode.

Фактический installation intent сейчас не может быть создан: valid connection authorization и concrete adapter instances не доказаны.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
```

Play Mode не запускался. Runtime rebase readiness остаётся `NOT_READY`.

## Следующий этап

После user-controlled runtime evidence и reviewed concrete adapter instances нужен отдельный serial installation implementation с explicit lifecycle и rollback policy. До этого runtime connection и native mutation запрещены.
