# T-FO06BX — ShipDeckNav native adapter source

Дата: 2026-09-12
Статус: **INTEGRATION SLICE / FAIL-CLOSED / NO RUNTIME INSTALLATION**

## 1. Назначение

Продолжить concrete adapter integration после T-FO06BW отдельным serial gate для `ShipDeckNav`. Этап не объявляет NavMesh готовым к rebase transaction и не изменяет BootstrapScene, префабы, runtime driver или `GlobalMotionNativeAdapterSet`.

## 2. Реализованный путь

```text
ShipDeckNav
  → GlobalMotionShipDeckNavProtocolHost
  → GlobalMotionShipDeckNavNativeAdapter
  → GlobalMotionShipDeckNavNativeAdapterSource
  → GlobalMotionRebaseNativeAdapterEvidenceSource
```

Добавлены:

- `GlobalMotionShipDeckNavProtocolHost` — observation-only host, который читает фактические `ShipDeckNav.IsRegistered`, `IsNavMeshInstanceValid`, `IsReady` и имя baked `NavMeshData`;
- `IGlobalMotionShipDeckNavTransactionHost` и `GlobalMotionShipDeckNavNativeAdapter` — typed adapter seam с полным contract shape;
- `GlobalMotionShipDeckNavNativeAdapterSource` — concrete `MonoBehaviour` source для будущего binding.

## 3. Fail-closed граница

Host не создаёт synthetic readiness:

- registration generation равна нулю до фактической регистрации;
- NavMesh instance generation равна нулю до валидного native instance;
- passenger attachment generation остаётся нулевой, потому что host не владеет snapshot/restore passenger state;
- synchronous rebuild/restore флаги остаются `false`;
- `TryCapture`, `TryApply`, `TryRebuild`, `TryValidate` и `TryRestore` не изменяют NavMesh или passenger state;
- adapter descriptor объявляет только `ShipDeckNav` coverage и `FullTransaction` как требуемую форму, но `NativeReady=false`.

Это намеренно не превращает существующую asynchronous registration queue (`≤1 AddNavMeshData за кадр`) в transaction-safe rebase boundary.

## 4. Проверка

```text
check_compile_errors = No compile errors
T-FO06BX validator = 6 pure checks PASS / 0 FAIL
git diff --check = PASS
```

Pure checks подтверждают:

1. source автоматически связывается с protocol host и `ShipDeckNav` dependency;
2. descriptor покрывает только `ShipDeckNav`;
3. descriptor не сообщает `NativeReady`;
4. invalid request отклоняется;
5. capture/rebuild отклоняются до lifecycle readiness;
6. source не регистрируется сам и не открывает readiness.

## 5. Что не выполнено

```text
ShipDeckNav native readiness = BLOCKED
synchronous NavMesh rebuild = NOT_IMPLEMENTED
passenger snapshot/restore = NOT_IMPLEMENTED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider runtime binding = NOT_EXECUTED
live manifest publication = NOT_EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

## 6. Следующий допустимый gate

Следующий serial gate должен отдельно доказать transaction-safe synchronous lifecycle для конкретного `ShipDeckNav`: capture NavMesh instance/frame, passenger/proxy provenance, rebuild в новой frame, validation и restore в прежнюю frame. До такой проверки `ShipDeckNav` нельзя включать в sealed adapter set и нельзя переходить к provider admission.
