# T-FO06BI — NetworkBaseline adapter boundary

Дата: 2026-09-12  
Статус: **COMPILE_VERIFIED / CONTRACT_ONLY / NOT_READY**

## 1. Назначение

Зафиксировать protocol-owned boundary для будущего `NetworkBaseline` native adapter. Existing baseline evidence может быть observation-only; такой результат не должен открывать `FullTransaction` или заявлять rollback.

## 2. Реализация

Создан файл:

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseNetworkBaselineAdapterBoundary.cs`

Добавлены:

- `IGlobalMotionRebaseNetworkBaselineEvidenceSource` для будущего concrete evidence producer;
- immutable `GlobalMotionRebaseNetworkBaselineAdapterBoundaryEvidence`;
- `TryBuildRestorableBoundary`, который требует:
  - валидный `GlobalMotionNetworkBaselineEvidence`;
  - классификацию `Restorable`;
  - protocol ownership;
  - отдельную capture boundary;
  - restore boundary;
  - ownership restore boundary;
  - lifetime restore boundary.

## 3. Fail-closed поведение

`ObservationOnly` evidence отклоняется. Даже валидное baseline observation не преобразуется в native adapter readiness без доказанного восстановления ownership и NetworkObject lifetime.

Контракт не:

- читает NGO runtime state;
- захватывает baseline;
- меняет ownership/spawn state;
- выполняет restore;
- регистрирует adapter в `GlobalMotionNativeAdapterSet`.

## 4. Проверки и границы

- `check_compile_errors`: **No compile errors**;
- concrete NetworkBaseline adapter: **не создан**;
- concrete evidence producer: **не создан**;
- `GlobalMotionNativeAdapterSet`: **не изменялся**;
- `BootstrapScene`: **не изменялась**;
- Play Mode: **не запускался**;
- rollback execution: **не выполнялся**;
- `runtimeRebaseReadiness`: **NOT_READY**.

Следующий gate должен предоставить protocol-owned producer, который сможет доказать Restorable baseline evidence и все четыре transaction boundaries. Наблюдательный baseline из T-FO06BA для этого недостаточен.
