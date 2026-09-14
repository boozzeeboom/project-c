# T-FO06AD — runtime readiness gate for native adapter connection

Дата: 2026-09-11.

## Назначение тикета

`T-FO06AD` выбран как следующий свободный подэтап после T-FO06AC; отдельного отчёта `06AD` в floating-origin документах не найдено.

Цель — создать fail-closed gate, который не позволит подключить native adapter set к runtime driver до подтверждения participant admission и live manifest publication.

## Реализация

Создан:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeReadinessGate.cs
```

Добавлены:

- `GlobalMotionRebaseRuntimeReadinessEvidence`;
- `GlobalMotionRebaseRuntimeReadinessGate.TryValidate`.

Gate проверяет:

1. наличие participant manifest;
2. `LiveManifestPublished = true`;
3. required manifest coverage (`CITY_STATIC`, `WORLD_ANCHORS`, `PLAYER_FRAME`, `CAMERA`, 22 ship roots и 20 ship deck nav entries);
4. sealed native adapter set и полное coverage пяти native областей;
5. current/native-ready status каждого adapter;
6. полный capability set `Capture/Apply/Rebuild/Validate/Restore`;
7. admission evidence для каждого manifest entry через `GlobalMotionRebaseParticipantAdmissionPolicy`.

## Fail-closed результат текущего проекта

Текущие project gates остаются закрытыми:

```text
participantAdmission = NOT_READY
runtimeAdapterReady = NOT_READY
runtimeProofComplete = false
rollbackReady = false
liveManifestPublication = false
runtimeRebaseReadiness = NOT_READY
```

Поэтому gate не разрешает соединение native adapter set с `GlobalMotionRebaseRuntimeDriver`.

## Scope boundary

Этап не:

- обнаруживает участников;
- публикует manifest;
- создаёт native adapter instances;
- вызывает Unity APIs;
- изменяет Transform/Rigidbody/NavMesh/camera/NGO state;
- запускает Apply/Rebuild/Validate/Publish;
- реализует rollback;
- добавляет runtime component в BootstrapScene.

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode не запускался.

## Gate

```text
readinessGateContract = IMPLEMENTED_COMPILE_PASS
nativeAdapterSetConnection = BLOCKED_BY_READINESS
participantAdmission = NOT_READY
liveManifestPublication = NOT_READY
runtimeRebaseReadiness = NOT_READY
```

## Следующий этап

Отдельно подготовить evidence source для participant admission/live manifest, не подключая native adapters к runtime до появления подтверждённых live receipts.
