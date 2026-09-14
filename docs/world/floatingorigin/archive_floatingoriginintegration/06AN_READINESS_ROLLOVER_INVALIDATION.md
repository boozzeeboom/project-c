# T-FO06AN — readiness rollover invalidation

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AN` выбран как следующий свободный подэтап после `T-FO06AM`; поиск по floating-origin документации и tracked-файлам не обнаружил существующего отчёта `06AN`.

Цель — не допустить использования installation intent, выданного для предыдущего readiness bundle, после повторной авторизации readiness.

## Реализация

Обновлён файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeDriver.cs
```

При каждом успешном `TryAuthorizeReadiness(...)` теперь:

- очищаются `_installationIntent` и `_installationIntentAuthorized`;
- требуется заново авторизовать installation intent;
- в `ReadinessAuthorized` evidence добавляется `installationInvalidated=true`.

Таким образом, readiness rollover не может сохранить старую installation identity даже при совпадении части полей.

## Что это интегрирует

```text
new readiness bundle
→ invalidate previous installation intent
→ authorize new installation intent
→ user-controlled request
```

Это lifecycle fail-closed правило. Оно не выполняет runtime installation и не вызывает native adapters.

## Scope boundary

Этап не:

- создаёт valid readiness bundle;
- публикует manifest или обнаруживает participants;
- устанавливает runtime driver;
- вызывает Transform/Rigidbody/NavMesh/camera/NGO mutation;
- выполняет Apply/Rebuild/Validate/Publish;
- реализует rollback;
- изменяет сцены, префабы или runtime configuration;
- запускает Play Mode.

Native pipeline остаётся `NOT_CONNECTED`, coordinator по-прежнему останавливается на `Captured`.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
```

Play Mode не запускался. Runtime rebase readiness остаётся `NOT_READY`.
