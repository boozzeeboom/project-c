# T-FO06AO — terminal failure invalidation

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AO` выбран как следующий свободный подэтап после `T-FO06AN`; поиск по floating-origin документации и tracked-файлам не обнаружил существующего отчёта `06AO`.

Цель — исключить сохранение installation authorization после terminal failure текущей rebase attempt.

## Реализация

Обновлён файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeDriver.cs
```

В `Fail(...)` добавлена fail-closed invalidation:

- `_installationIntent` очищается;
- `_installationIntentAuthorized` сбрасывается;
- в evidence buffer записывается `InstallationInvalidated` с причиной terminal failure;
- затем записывается terminal phase (`Aborted` или `Faulted`).

После ошибки повторный user-controlled request невозможен до полного `TryReset` и новой цепочки readiness → installation intent → driver authorization.

## Что это интегрирует

```text
terminal failure
→ InstallationInvalidated
→ reset required
→ fresh readiness/installation authorization
```

Это предотвращает повторное использование installation identity после отказа coordinator или native preparation boundary.

## Scope boundary

Этап не:

- создаёт valid readiness bundle;
- публикует manifest или обнаруживает participants;
- вызывает concrete native adapters;
- выполняет Apply/Rebuild/Validate/Publish;
- реализует native rollback;
- изменяет Transform/Rigidbody/NavMesh/camera/NGO state;
- добавляет automatic trigger;
- запускает Play Mode.

Coordinator и native pipeline остаются без изменений; runtime rebase readiness остаётся `NOT_READY`.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
```

Play Mode не запускался.
