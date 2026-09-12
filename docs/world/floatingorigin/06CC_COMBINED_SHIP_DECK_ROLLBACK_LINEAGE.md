# T-FO06CC — combined ShipDeckNav rollback result and binding lineage

Дата: 2026-09-12
Статус: **IMPLEMENTATION SLICE / EXPLICIT SERVER BOUNDARY / NOT INSTALLED**

## 1. Назначение

Закрыть следующий gate после T-FO06CB: добавить формальный результат combined transaction, explicit frame/binding lineage и best-effort rollback accounting без подключения к native adapter set или runtime driver.

## 2. Реализовано

Создан runtime-independent контракт `GlobalMotionShipDeckCombinedTransactionContract`:

- ordered transaction phases;
- transaction/frame/binding generation identity;
- capture/rebuild/validate/restore flags;
- отдельные deck/passenger rollback attempt/result flags;
- fail-closed validation неполной rollback completion.

`GlobalMotionShipDeckCombinedSnapshot` теперь хранит:

- `FrameGeneration`;
- `PassengerBindingGeneration`.

`GlobalMotionShipDeckCombinedTransactionHost` теперь:

- увеличивает reviewed passenger binding generation при explicit configuration;
- проверяет snapshot transaction, frame и binding lineage;
- проверяет passenger/deck identity против reviewed sources;
- возвращает `GlobalMotionShipDeckCombinedTransactionResult` для rebuild;
- при отказе passenger restore пытается восстановить deck и уже затронутых пассажиров в обратном порядке;
- маркирует rollback как `RollbackCompleted` только при успешных deck и passenger restore attempts.

## 3. Ограничения

- Best-effort rollback не является доказанным atomic Unity rollback.
- Полная NavMeshAgent path/velocity history не сохраняется.
- Attachment lifetime generation не получен из NGO lifecycle ledger; binding generation покрывает только reviewed host configuration.
- Host не реализует `IGlobalMotionNativeAdapter` и не подключён к provider, adapter set, runtime driver или BootstrapScene.
- Play Mode и реальная synchronous NavMesh rebuild не запускались.

## 4. Проверка

```text
check_compile_errors = No compile errors
T-FO06CB combined-host validator = 7 pure checks PASS / 0 FAIL
Play Mode = NOT RUN
git diff --check = PENDING COMMIT REVIEW
```

## 5. Статус gates

```text
combined transaction result = PRESENT / NOT INSTALLED
rollback evidence = CONTRACT-LEVEL ONLY
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider runtime binding = NOT_EXECUTED
runtimeRebaseReadiness = NOT_READY
```

## 6. Следующий gate

Следующий этап может подготовить typed adapter seam для combined host только после отдельного reviewed решения о том, какой server-owned lifecycle source выдаёт attachment/lifetime generations. Этот этап не открывает `NativeReady` или `FullTransaction`.
