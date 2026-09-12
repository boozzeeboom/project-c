# T-FO06AI — runtime connection evidence contract

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AI` выбран как следующий свободный подэтап после `T-FO06AH`; поиск по floating-origin документации и tracked-файлам не обнаружил существующего отчёта `06AI`.

Цель — связать уже созданный `GlobalMotionRebaseReadinessBundle` с sealed `GlobalMotionNativeAdapterSet` только через отдельный pure evidence contract. Этот этап не устанавливает runtime connection.

## Реализация

Создан файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeConnectionGate.cs
```

Добавлены:

- `GlobalMotionRebaseRuntimeConnectionEvidence` — immutable evidence token с manifest digest, session identity и количеством адаптеров;
- `GlobalMotionRebaseRuntimeConnectionGate.TryBuildEvidence(...)` — fail-closed validator для готового bundle и sealed native adapter set.

Проверки выполняются в порядке:

```text
readiness bundle ready
→ adapter set present
→ sealed/current/native-ready/full-capability/non-overlapping coverage
→ readiness digest/session identity present
→ evidence token
```

`TryBuildEvidence` не вызывает методы адаптеров `TryCapture`, `TryApply`, `TryRebuild`, `TryValidate` или `TryRestore`. `TryValidateReady` проверяет только декларативное состояние adapter set.

## Что это интегрирует

```text
GlobalMotionRebaseReadinessBundle
→ GlobalMotionRebaseRuntimeConnectionGate.TryBuildEvidence
← GlobalMotionNativeAdapterSet
```

Результат — evidence token, а не runtime connection. Driver, coordinator и BootstrapScene остаются без изменений.

## Scope boundary

Этап не:

- создаёт concrete native adapter instances;
- обнаруживает participants или публикует manifest;
- подключает evidence token к `GlobalMotionRebaseRuntimeDriver`;
- вызывает Unity APIs или меняет Transform/Rigidbody/NavMesh/camera/NGO state;
- выполняет `Apply/Rebuild/Validate/Publish`;
- реализует rollback;
- изменяет сцены, префабы или runtime configuration;
- запускает Play Mode.

При текущем состоянии проекта фактический вызов gate остаётся заблокированным, поскольку не доказаны live manifest publication, participant admission и concrete native adapter readiness.

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode не запускался. Runtime rebase readiness остаётся `NOT_READY`.

## Следующий этап

Отдельно получить user-controlled runtime evidence и reviewed concrete adapter instances. Только после этого можно рассматривать serial runtime installation gate; автоматические trigger, native mutation и rollback остаются запрещёнными.
