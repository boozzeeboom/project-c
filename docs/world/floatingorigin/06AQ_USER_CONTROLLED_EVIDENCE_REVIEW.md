# T-FO06AQ — user-controlled runtime evidence review

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AQ` выбран как следующий реальный gate после сводного этапа `T-FO06AP`. В отличие от предыдущих micro-contracts, этот этап комплексно объединяет уже существующие pure validators в один reviewed evidence gate.

Цель — проверять полный пакет user-controlled runtime evidence до создания readiness bundle и до любой runtime installation.

## Реализация

Создан файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseUserControlledEvidenceReview.cs
```

Добавлены:

- `GlobalMotionRebaseUserControlledEvidenceReview` — aggregate result;
- `GlobalMotionRebaseUserControlledEvidenceReviewGate.TryReview(...)` — комплексный fail-closed review.

Gate последовательно проверяет:

```text
live manifest session evidence
→ runtime readiness/admission + sealed native adapters
→ complete runtime proof envelope
→ complete Unity rollback evidence
→ participant identity match
→ frame-generation match
→ rollback participant count == manifest count
→ aggregate review result
```

Переиспользованы существующие контракты:

- `GlobalMotionRebaseLiveManifestSessionEvidenceGate`;
- `GlobalMotionRebaseRuntimeReadinessGate`;
- `GlobalMotionRebaseRuntimeProofContract`;
- `UnityStateRollbackContract`.

## Что это интегрирует

Новый gate объединяет в одном месте требования, которые ранее были распределены по отдельным contracts:

- publisher/server/peer manifest evidence;
- participant admission;
- native adapter readiness по пяти доменам;
- camera/deck/passenger/NGO/physics/baseline proof;
- rollback capture/restore proof;
- согласование participant и frame identity.

Результат не создаёт `GlobalMotionRebaseReadinessBundle` автоматически и не утверждает runtime readiness без фактических входных evidence.

## Текущее состояние

В проекте нет valid runtime evidence package, поэтому фактический `TryReview(...)` остаётся заблокированным. Это ожидаемое fail-closed поведение:

```text
connectionEvidence = NOT_PROVEN
connectionAuthorization = NOT_GRANTED
installationIntent = NOT_CREATED
driverInstallationAuthorization = NOT_GRANTED
runtimeRebaseReadiness = NOT_READY
```

## Scope boundary

Этап не:

- собирает Play Mode данные автоматически;
- обнаруживает participants;
- публикует manifest;
- создаёт native adapter instances;
- устанавливает driver в BootstrapScene;
- вызывает Apply/Rebuild/Validate/Publish;
- выполняет rollback;
- изменяет Unity state;
- запускает Play Mode.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
```

Play Mode не запускался. Следующий шаг после получения пользовательского evidence package — serial review входных данных и только затем создание valid readiness bundle.
