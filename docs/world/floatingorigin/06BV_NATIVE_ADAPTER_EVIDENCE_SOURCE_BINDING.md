# T-FO06BV — native adapter evidence source binding

Дата: 2026-09-12  
Статус: **IMPLEMENTED / FAIL-CLOSED / NO RUNTIME CHANGE**

## 1. Назначение

После `T-FO06BU` добавлен explicit source-binding API для `GlobalMotionRebaseNativeAdapterEvidenceSource`. Это связывает owner-reviewed Transform/Rigidbody/Camera targets и typed adapter sources через serial-safe component boundary, но не создаёт runtime adapter set и не выполняет provider binding.

## 2. Реализация

`GlobalMotionRebaseNativeAdapterEvidenceSource` получил:

- `TryConfigureSources(...)` для explicit передачи reviewed targets и ShipDeckNav/NetworkBaseline adapter sources;
- `TryValidateSourceBindings(...)` для повторной проверки всех source contracts;
- общую fail-closed проверку targets, camera owner identity и `IGlobalMotionNativeAdapter` contracts;
- раннюю остановку до создания `GlobalMotionNativeAdapterSet`, если binding неполный.

Существующий `TryGetNativeAdapterSet` теперь использует ту же binding validation перед построением кандидата. Частичный набор по-прежнему отвергается через sealing/readiness checks.

## 3. Проверка

```text
check_compile_errors = No compile errors
T-FO06BV validator = 6 pure checks PASS / 0 FAIL
git diff --check = PASS
```

Pure validator подтвердил:

- source начинается unbound;
- неполный target set отклоняется;
- отсутствующий ShipDeckNav source отклоняется;
- explicit typed binding успешно принимается;
- incomplete native readiness остаётся fail-closed;
- binding не создаёт provider и не выполняет self-registration.

## 4. Граница

```text
source binding API = IMPLEMENTED
GlobalMotionNativeAdapterSet = NOT CREATED BY BINDING API
provider binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Успешный `TryConfigureSources` означает только корректность typed source binding. Он не означает, что adapter descriptors готовы или что NetworkBaseline обладает `Restorable` capability.

## 5. Следующий gate

Необходимо отдельно получить реальные reviewed sources для ShipDeckNav lifecycle и protocol-owned NetworkBaseline restore. Только после этого допустим serial construction/sealing adapter set и последующая provider admission validation.
