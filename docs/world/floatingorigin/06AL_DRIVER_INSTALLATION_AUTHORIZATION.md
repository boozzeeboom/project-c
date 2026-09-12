# T-FO06AL — driver installation authorization

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AL` выбран как следующий свободный подэтап после `T-FO06AK`; поиск по floating-origin документации и tracked-файлам не обнаружил существующего отчёта `06AL`.

Цель — подключить pure installation intent к существующему runtime driver как отдельную fail-closed admission boundary. Это не runtime installation и не native mutation.

## Реализация

Обновлён файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeDriver.cs
```

Добавлены:

- `TryAuthorizeInstallationIntent(...)`;
- `IsInstallationIntentAuthorized`;
- `InstallationIntent`;
- очистка installation authorization в `TryReset`.

`TryRequestUserControlled(...)` теперь требует две последовательные авторизации:

```text
ready bundle
→ installation intent with exact manifest/session identity
→ user-controlled request
```

Проверяются:

- driver находится в `Idle`;
- readiness authorization уже выдана;
- intent валиден;
- `ManifestDigest` совпадает с текущим readiness bundle;
- `SessionIdentity` совпадает с текущим readiness bundle.

## Что это интегрирует

```text
GlobalMotionRebaseReadinessBundle
→ GlobalMotionRebaseRuntimeDriver.TryAuthorizeReadiness
→ GlobalMotionRebaseRuntimeInstallationIntent
→ GlobalMotionRebaseRuntimeDriver.TryAuthorizeInstallationIntent
→ TryRequestUserControlled
```

При отказе до request driver возвращает `installation_intent_not_authorized` и не создаёт rebase transaction.

## Scope boundary

Этап не:

- создаёт valid readiness bundle;
- создаёт concrete native adapters;
- публикует manifest или обнаруживает participants;
- устанавливает Unity runtime components;
- вызывает Transform/Rigidbody/NavMesh/camera/NGO mutation;
- выполняет `Apply/Rebuild/Validate/Publish`;
- реализует rollback;
- добавляет driver в BootstrapScene;
- запускает Play Mode.

Coordinator по-прежнему останавливается на `Captured`, а native pipeline остаётся `NOT_CONNECTED`.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
```

Play Mode не запускался. Runtime rebase readiness остаётся `NOT_READY`.
