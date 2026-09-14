# T-FO06AJ — runtime connection authorization gate

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AJ` выбран как следующий свободный подэтап после `T-FO06AI`; поиск по floating-origin документации и tracked-файлам не обнаружил существующего отчёта `06AJ`.

Цель — добавить точную fail-closed проверку, которая связывает ранее созданный connection evidence token с текущими readiness bundle и sealed native adapter set. Результат является authorization evidence, а не установкой runtime connection.

## Реализация

Создан файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeConnectionAuthorization.cs
```

Добавлены:

- `GlobalMotionRebaseRuntimeConnectionAuthorization` — immutable authorization token;
- `GlobalMotionRebaseRuntimeConnectionAuthorizationGate.TryAuthorize(...)` — pure binding gate.

Проверки:

```text
connection evidence valid
→ readiness bundle ready
→ adapter set present and ready
→ manifest digest exact match
→ session identity exact match
→ adapter count exact match
→ authorization evidence
```

Проверка digest/session выполняется с ordinal-сравнением. Несовпадение evidence с текущим bundle или adapter set блокирует авторизацию.

## Что это интегрирует

```text
GlobalMotionRebaseRuntimeConnectionEvidence
        + GlobalMotionRebaseReadinessBundle
        + GlobalMotionNativeAdapterSet
        → GlobalMotionRebaseRuntimeConnectionAuthorization
```

Gate не вызывает native adapter methods и не устанавливает authorization в `GlobalMotionRebaseRuntimeDriver`.

## Scope boundary

Этап не:

- создаёт concrete native adapter instances;
- обнаруживает participants или публикует manifest;
- устанавливает connection в driver/coordinator;
- вызывает Unity APIs или меняет Transform/Rigidbody/NavMesh/camera/NGO state;
- выполняет `Apply/Rebuild/Validate/Publish`;
- реализует rollback;
- изменяет сцены, префабы или runtime configuration;
- запускает Play Mode.

Фактическая авторизация остаётся заблокированной: в проекте нет доказанного valid runtime bundle, live manifest publication, participant admission и concrete native adapter instances.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
```

`git diff --check` пройден. Play Mode не запускался. Runtime rebase readiness остаётся `NOT_READY`.

## Следующий этап

Только после user-controlled runtime evidence и reviewed concrete adapter instances можно рассматривать отдельную serial installation boundary. Automatic trigger, native mutation и rollback остаются запрещёнными.
