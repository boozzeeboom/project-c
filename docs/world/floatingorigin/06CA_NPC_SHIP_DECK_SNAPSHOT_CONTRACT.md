# T-FO06CA — NPC ship-deck snapshot contract

Дата: 2026-09-12
Статус: **IMPLEMENTATION SLICE / EXPLICIT SERVER BOUNDARY / NOT INSTALLED**

## 1. Назначение

Связать `ShipDeckNav` snapshot boundary с фактическим состоянием `NpcBrain` на корабле. Этап добавляет explicit server-owned capture/restore API для одного passenger/proxy attachment, но не подключает его к runtime rebase driver или adapter host.

## 2. Реализовано

Добавлен `GlobalMotionNpcShipDeckSnapshot`, содержащий transaction identity и reviewed attachment state:

- passenger/deck/ship identity;
- requested и active attachment;
- parent-to-ship state;
- deck-navigation state;
- `NavMeshAgent` update flags;
- proxy creation, NavMesh membership, path/stopped state;
- world/local pose;
- proxy position и destination.

В `NpcBrain` добавлены:

- `TryCaptureFloatingOriginShipDeckSnapshot(...)`;
- `TryRestoreFloatingOriginShipDeckSnapshot(...)`.

Обе операции требуют server authority и явный transaction identity. Capture отклоняется без фактически активного attachment/proxy. Restore проверяет snapshot contract, ship lifetime и выполняет только explicit restore call.

## 3. Ограничения

- автоматический поиск пассажиров не выполняется;
- snapshot не сериализуется в Unity и не хранится между сессиями;
- методы не вызываются из `GlobalMotionShipDeckNavProtocolHost`;
- NavMesh rebuild и passenger restore ещё не объединены в один атомарный host transaction;
- сохранение полного NavMeshAgent path после перестройки остаётся ограниченным восстановлением destination;
- BootstrapScene, prefabs, provider, adapter set и runtime driver не изменялись.

## 4. Проверка

```text
check_compile_errors = No compile errors
T-FO06BX validator = 9 pure checks PASS / 0 FAIL
git diff --check = PASS
```

Pure validator дополнительно проверяет, что incomplete passenger snapshot identity отклоняется до любых Unity mutations.

## 5. Текущий статус

```text
NPC passenger snapshot API = PRESENT / NOT INSTALLED
ShipDeckNav snapshot API = PRESENT / NOT INSTALLED
atomic combined transaction host = NOT IMPLEMENTED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider runtime binding = NOT_EXECUTED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Фактическая совместимость parent restore, proxy warp и NavMesh rebuild остаётся **INCONCLUSIVE** без пользовательского Play Mode capture.

## 6. Следующий gate

Следующий serial этап должен связать `ShipDeckNavFloatingOriginSnapshot` и `GlobalMotionNpcShipDeckSnapshot` в одном explicit transaction host с capture/rebuild/validate/restore ordering. До такой связки adapter не может стать `NativeReady`.
