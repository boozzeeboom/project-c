# T-FO06AR — user runtime evidence package and capture protocol

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AR` выбран как следующий комплексный этап после `T-FO06AQ`. Поиск по floating-origin отчётам не обнаружил существующего отчёта `06AR`.

Цель — добавить явную intake boundary для пользовательского runtime evidence package и одновременно зафиксировать точный Play Mode capture protocol.

## Реализация

Создан файл:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseUserControlledEvidencePackage.cs
```

Добавлены:

- `GlobalMotionRebaseUserControlledEvidencePackage` — immutable package envelope;
- `GlobalMotionRebaseUserControlledEvidencePackageGate.TrySeal(...)` — pure sealing gate.

Package можно запечатать только если:

- `GlobalMotionRebaseUserControlledEvidenceReview.IsReady == true`;
- evidence действительно предоставлен пользователем;
- есть непустой и trimmed `CaptureReference`.

Seal не публикует evidence, не устанавливает runtime driver и не изменяет Unity state.

## Нужен ли сейчас тест пользователя

Для текущего кода — нет: implementation является pure intake contract и прошёл compile/static validation.

Для следующего перехода к valid readiness bundle — да, нужен один user-controlled capture. Без него `TrySeal` закономерно остаётся заблокированным.

## Что именно должен сделать пользователь

1. Открыть каноническую сцену `Assets/_Project/Scenes/BootstrapScene.unity`.
2. Использовать isolated pilot `Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab`.
3. Запустить Host/Client Play Mode capture с включённым существующим read-only evidence probe.
4. Зафиксировать в одном capture:
   - publisher observation, server acceptance, peer digest/count match и отсутствие manifest drift;
   - baseline placement/acknowledgement;
   - movement и jump после baseline;
   - camera ownership/history;
   - ShipDeckNav registration и passenger provenance;
   - NGO tick ordering и physics ordering;
   - controlled rebase markers, если runtime driver уже установлен;
   - rollback capture/restore markers.
5. Сохранить полный export без ручного удаления строк и передать путь к файлу/логу как `CaptureReference`.
6. Не считать повторные NavMesh warning сами по себе доказательством готовности или неготовности; их нужно отметить отдельно в отчёте capture.

## Важное ограничение

В текущем проекте driver не установлен в BootstrapScene, concrete native adapters отсутствуют, а coordinator останавливается на `Captured`. Поэтому capture до отдельного runtime installation не может доказать успешные `Applied → PhysicsSynchronized → Validated → Published → Completed` фазы. Эти поля должны быть отмечены как `UNVERIFIED`, а не придуманы.

## Scope boundary

Этап не:

- запускает Play Mode;
- автоматически собирает пользовательский capture;
- создаёт valid runtime evidence без входного файла;
- устанавливает driver или adapters;
- выполняет native mutation или rollback;
- изменяет сцены и префабы.

## Проверка

```text
validate_script = 0 errors, 0 warnings
check_compile_errors = No compile errors
git diff --check = PASS
playMode = NOT_RUN_USER_CONTROLLED
```

После пользовательского capture следующий этап должен выполнить serial review файла и только при полном наборе доказательств передать package в readiness builder.
