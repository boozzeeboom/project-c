# T-FO06BG — native adapter evidence source

Дата: 2026-09-12  
Статус: **COMPILE_VERIFIED / PARTIAL_PRODUCER / NOT_READY**

## 1. Назначение

Добавить первый concrete provenance producer для native adapter readiness. Источник принимает только owner-reviewed Unity targets и explicit adapter components; incomplete coverage отклоняется до публикации.

## 2. Реализация

Создан файл:

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseNativeAdapterEvidenceSource.cs`

Источник реализует `IGlobalMotionRebaseNativeAdapterEvidenceSource` и:

- требует reviewed `Transform`, `Rigidbody` и `SpringArmCamera` targets;
- строит explicit adapters для `Transform`, `Rigidbody` и `CameraHistory`;
- требует отдельные concrete `IGlobalMotionNativeAdapter` sources для `ShipDeckNav` и `NetworkBaseline`;
- запрещает выдачу частичного adapter set;
- проверяет duplicate IDs, sealing, current/native-ready descriptors и полную capability/coverage через `GlobalMotionNativeAdapterSet.TryValidateReady`;
- не выполняет discovery и не устанавливается автоматически в сцену.

## 3. Фактический результат

Producer компилируется, но readiness не выдаёт, пока отсутствуют concrete sources для:

```text
ShipDeckNav       = BLOCKED / source not assigned
NetworkBaseline   = BLOCKED / source not assigned
```

Transform/Rigidbody/Camera adapters не регистрировались в runtime и не подключались к `BootstrapScene`. Частичный set намеренно не принимается.

## 4. Проверки

- `check_compile_errors`: **No compile errors**;
- `GlobalMotionNativeAdapterSet` registration: **NONE**;
- `BootstrapScene` mutation: **NONE**;
- provider/source binding: **NONE**;
- live manifest publication: **NOT OBSERVED**;
- Play Mode: **NOT RUN**;
- controlled rebase/rollback: **NOT RUN**;
- `runtimeRebaseReadiness`: **NOT_READY**.

## 5. Ограничение

Этот этап не создаёт ShipDeckNav или NetworkBaseline adapters. Их отсутствие является фактическим fail-closed результатом, а не пропуском проверки. Следующий gate должен отдельно решить synchronous ShipDeckNav lifecycle и protocol-owned NetworkBaseline rollback boundary.
