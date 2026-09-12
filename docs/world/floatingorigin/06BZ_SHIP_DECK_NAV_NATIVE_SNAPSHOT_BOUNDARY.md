# T-FO06BZ — ShipDeckNav native snapshot boundary

Дата: 2026-09-12
Статус: **IMPLEMENTATION SLICE / DORMANT / FAIL-CLOSED**

## 1. Назначение

Добавить transaction-scoped native boundary для регистрации `ShipDeckNav`, не устанавливая её в runtime rebase pipeline и не подменяя passenger/proxy restore отсутствующей реализацией.

## 2. Реализовано

В `ShipDeckNav` добавлены:

- `ShipDeckNavFloatingOriginSnapshot`;
- `RegistrationGeneration`;
- `TryCaptureFloatingOriginSnapshot(...)`;
- `TryRebuildFloatingOriginSnapshot(...)`;
- `TryRestoreFloatingOriginSnapshot(...)`.

Операции требуют:

- непустой точный `transactionId`;
- server authority;
- готовый зарегистрированный `NavMeshDataInstance` для capture/rebuild;
- явный origin для rebuild;
- snapshot identity для restore.

Регистрация вынесена во внутренний `RegisterAt(Vector3)`, который выполняет синхронный `Remove + AddNavMeshData` только при прямом explicit вызове будущего transaction host. Существующая round-robin очередь обычной регистрации не изменялась.

## 3. Ограничения

Этот этап **не** даёт `ShipDeckNav` полную native readiness:

- passenger/proxy state не захватывается и не восстанавливается;
- NavMeshAgent path/velocity/active state не входят в snapshot;
- `NpcBrain` parent/attachment lifecycle не изменяется;
- adapter host не вызывает новые методы;
- `GlobalMotionNativeAdapterSet` не создаётся и не регистрируется;
- provider, runtime driver, BootstrapScene и Play Mode не изменялись.

Поэтому `synchronous rebuild/restore` остаются закрытой capability boundary до отдельного passenger-aware transaction host.

## 4. Проверка

```text
check_compile_errors = No compile errors
T-FO06BX validator = 8 pure checks PASS / 0 FAIL
git diff --check = PASS
```

Дополнительная pure-проверка подтверждает, что snapshot API отклоняет пустой transaction identity до обращения к server/NavMesh state.

## 5. Текущий статус

```text
ShipDeckNav snapshot API = PRESENT / NOT INSTALLED
passenger-aware restore = NOT_IMPLEMENTED
native adapter readiness = BLOCKED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider runtime binding = NOT_EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Runtime эффективность `Remove + AddNavMeshData`, сохранение proxy path/velocity и восстановление пассажиров остаются **INCONCLUSIVE** без пользовательского Play Mode capture.

## 6. Следующий gate

Следующий serial этап должен добавить explicit passenger/proxy snapshot contract и связать его с этим NavMesh snapshot так, чтобы rebuild/restore могли быть атомарно проверены одним reviewed transaction host. До этого adapter остаётся `NativeReady=false`.
