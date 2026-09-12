# T-FO06CB — combined ShipDeckNav and passenger transaction host

Дата: 2026-09-12
Статус: **IMPLEMENTATION SLICE / EXPLICIT SERVER BOUNDARY / NOT INSTALLED**

## 1. Назначение

Объединить уже существующие `ShipDeckNavFloatingOriginSnapshot` и `GlobalMotionNpcShipDeckSnapshot` в одном owner-reviewed host с фиксированным порядком capture → rebuild → passenger restore → validate.

## 2. Реализовано

Создан `GlobalMotionShipDeckCombinedTransactionHost` без автоматического поиска участников. Host принимает только явно переданный `NpcBrain[]` и предоставляет:

- `TryConfigureReviewedPassengers(...)`;
- `TryCapture(transactionId, frameGeneration, ...)`;
- `TryRebuild(snapshot, navFrameOrigin, ...)`;
- `TryValidate(snapshot, ...)`;
- `TryRestore(snapshot, ...)`.

Capture сначала получает deck snapshot, затем снимки всех reviewed passengers. Rebuild выполняет synchronous `ShipDeckNav` rebuild и затем восстанавливает passenger snapshots. При ошибке passenger restore host пытается восстановить исходный deck snapshot.

Исправлена compile-ошибка `CS0177`: успешный путь `ValidateIdentity(...)` теперь явно назначает `error = null`.

## 3. Ограничения

- Host не реализует `IGlobalMotionNativeAdapter` и не подключён к adapter set.
- Provider, runtime driver, BootstrapScene и live manifest не изменялись.
- Частичный passenger rollback не является доказанным атомарным rollback: при отказе одного restore host выполняет best-effort restore deck snapshot, но отдельное rollback evidence ещё отсутствует.
- Проверка snapshot использует текущую explicit passenger binding и не создаёт generation ledger для attachment lifetime.
- NavMesh `Remove + AddNavMeshData`, parent restore и proxy warp не проверялись в Play Mode.

## 4. Проверка

```text
check_compile_errors = No compile errors
T-FO06BX existing validator = 9 pure checks PASS / 0 FAIL
T-FO06CB combined-host validator = 6 pure checks PASS / 0 FAIL
git diff --check = PENDING COMMIT REVIEW
```

Pure validator создаёт только временный component graph, проверяет required dependency, explicit passenger binding rejection и fail-closed identity/snapshot paths. Runtime server authority, NavMesh mutation и Play Mode не запускаются.

## 5. Текущий статус

```text
combined ShipDeckNav + passenger host = PRESENT / NOT INSTALLED
GlobalMotionShipDeckNavNativeAdapter = SEAM ONLY
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider runtime binding = NOT_EXECUTED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

## 6. Следующий gate

Следующий serial этап должен решить formal rollback result и reviewed attachment/lifetime generation lineage до adapter integration. Этот этап сам по себе не открывает `NativeReady`, `FullTransaction` или runtime admission.
