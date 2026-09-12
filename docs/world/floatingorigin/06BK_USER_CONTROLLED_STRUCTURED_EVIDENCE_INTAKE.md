# T-FO06BK — user-controlled structured evidence intake

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / DORMANT**

## 1. Реальное действие

Добавлен concrete intake component вместо очередного audit-only этапа:

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseUserControlledEvidenceIntake.cs`

Компонент реализует:

- `IGlobalMotionRebaseRuntimeProofEvidenceSource`;
- `IGlobalMotionRebaseRollbackEvidenceSource`.

## 2. Поведение intake

`TrySubmit(...)` принимает только внешне подготовленные structured evidence:

- `GlobalMotionRebaseRuntimeProofEvidence`;
- `UnityStateRollbackEvidence`;
- explicit capture reference;
- `userSupplied=true`.

Перед сохранением выполняются:

- `GlobalMotionRebaseRuntimeProofContract.TryValidate`;
- `UnityStateRollbackContract.TryValidate`;
- проверка participant identity lineage;
- проверка frame-generation lineage;
- запрет повторного принятия без явного `Clear()`.

До успешного `TrySubmit` оба source метода возвращают fail-closed error. Компонент не создаёт capture ID, frame generation, proof mask, rollback flags или capture reference самостоятельно.

## 3. Интеграционная граница

Это первый concrete structured evidence producer/intake path, но он не объявляет readiness сам по себе:

```text
structured intake component = IMPLEMENTED
external evidence submitted = NO
runtime proof source        = BLOCKED UNTIL TrySubmit
rollback source             = BLOCKED UNTIL TrySubmit
BootstrapScene binding      = NONE
runtimeRebaseReadiness      = NOT_READY
```

Компонент не запускает Play Mode, не читает текстовые логи автоматически, не вызывает native adapters и не меняет NGO/Unity state.

## 4. Проверки

- `check_compile_errors`: **No compile errors**;
- structured evidence submission: **не выполнялась**, так как пользовательский capture не предоставлен;
- provider/source binding: **не выполнялся**;
- BootstrapScene: **не изменялась**;
- Play Mode: **не запускался**;
- `git diff --check`: выполняется перед коммитом.

Следующий этап может подключить этот intake к dormant provider только после явного user-controlled submission и отдельной проверки manifest/session/adapters.
