# T-FO06BF — provenance-bearing admission provider boundary

Дата: 2026-09-12  
Статус: **COMPILE_VERIFIED / DORMANT / NOT_READY**

## 1. Назначение

Добавить безопасную aggregation boundary для admission evidence после аудита T-FO06BE. Provider принимает только данные от explicit provenance-bearing source contracts и не создаёт synthetic readiness.

## 2. Реализация

Создан файл:

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseAdmissionEvidenceProvider.cs`

Добавлены source contracts:

- `IGlobalMotionRebaseReviewedAdmissionEvidenceSource`;
- `IGlobalMotionRebaseNativeAdapterEvidenceSource`;
- `IGlobalMotionRebaseRuntimeProofEvidenceSource`;
- `IGlobalMotionRebaseRollbackEvidenceSource`.

`GlobalMotionRebaseAdmissionEvidenceProvider`:

- реализует существующий `IGlobalMotionRebaseRuntimeAdmissionEvidenceSource`;
- требует четыре отдельные source references;
- отклоняет missing source contracts;
- принимает identity/policy/catalog только от reviewed source;
- валидирует `GlobalMotionNativeAdapterSet` через `TryValidateReady`;
- валидирует runtime proof через `GlobalMotionRebaseRuntimeProofContract`;
- валидирует rollback evidence через `UnityStateRollbackContract`;
- требует совпадение participant identity и frame generation между proof и rollback;
- выдаёт runtime adapter/proof/rollback flags только после успешной валидации всех источников.

Provider не выполняет discovery, не создаёт adapter set, не собирает Play Mode evidence, не вызывает native adapters и не меняет Unity state.

## 3. Результат

Это только provenance boundary, а не источник готовности. Concrete producers для четырёх source contracts ещё не созданы, поэтому provider остаётся dormant и fail-closed. При отсутствии любого source он возвращает explicit error вместо admission evidence.

Синтетические значения и all-`true` defaults отсутствуют.

## 4. Проверки

- `check_compile_errors`: **No compile errors**;
- source/provider binding: **не выполнялся**;
- `BootstrapScene`: **не изменялась**;
- `GlobalMotionNativeAdapterSet`: **не создавался и не регистрировался**;
- live manifest publication: **не наблюдалась**;
- Play Mode: **не запускался**;
- controlled rebase/rollback: **не выполнялись**;
- `runtimeRebaseReadiness`: **NOT_READY**.

## 5. Следующий gate

Следующим этапом нужен первый concrete producer с реальным происхождением данных. Порядок не меняется: adapter readiness, runtime proof и rollback должны быть доказаны отдельно; только после этого допустимы serial binding и user-controlled runtime capture.
