# T-FO06BY — ShipDeckNav passenger provenance binding

Дата: 2026-09-12
Статус: **INTEGRATION FOLLOW-UP / OBSERVATION-ONLY / FAIL-CLOSED**

## 1. Назначение

Уточнить concrete `ShipDeckNav` host после T-FO06BX: passenger/proxy readiness теперь может быть передана только через explicit owner-reviewed `NpcBrain[]` binding. Автоматическое обнаружение пассажиров, изменение attachment state и открытие native readiness не выполняются.

## 2. Реализованное изменение

`GlobalMotionShipDeckNavProtocolHost` получил:

- сериализованный список `_reviewedPassengers`;
- `TryConfigureReviewedPassengers(NpcBrain[] passengers, out string error)`;
- проверку для каждого пассажира:
  - `IsExplicitShipAttachmentActive`;
  - `IsDeckNavigationActive`;
  - `IsDeckProxyCreated`;
  - `IsDeckProxyOnNavMesh`;
  - совпадение `DeckNavName` с текущим `ShipDeckNav`.

При успешной проверке host может наблюдать passenger attachment generation. Это не означает readiness: `SynchronousRebuildSupported` и `SynchronousRestoreSupported` по-прежнему `false`, а capture/restore passenger state отсутствует.

## 3. Fail-closed поведение

- пустой или неявный список пассажиров отклоняется;
- null-элемент отклоняется;
- пассажир с неполной attachment/proxy/NavMesh readiness отклоняется;
- host не использует `FindObjectsByType` для скрытого discovery;
- host не вызывает `AttachToShipDeck`, `TrySetParent`, `EnsureProxy`, `Warp`, `AddNavMeshData` или `RemoveNavMeshData`;
- `GlobalMotionNativeAdapterSet` не создаётся и не регистрируется.

## 4. Проверка

```text
check_compile_errors = No compile errors
T-FO06BX validator = 7 pure checks PASS / 0 FAIL
git diff --check = PASS
```

Дополнительная проверка подтверждает, что пустой reviewed passenger binding отклоняется с `passenger_reviewed_sources_required`.

## 5. Оставшиеся блокеры

```text
passenger provenance source = explicit binding API only
passenger snapshot = NOT_IMPLEMENTED
synchronous NavMesh rebuild = NOT_IMPLEMENTED
passenger restore = NOT_IMPLEMENTED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
provider runtime binding = NOT_EXECUTED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Проверка runtime-пассажиров и порядок attachment остаются **INCONCLUSIVE** без пользовательского Play Mode capture с реально назначенными reviewed passengers.

## 6. Следующий gate

Следующий этап должен реализовать и отдельно доказать transaction-safe snapshot/rebuild/restore для NavMesh instance и passenger/proxy state. До этого `ShipDeckNav` не может стать `NativeReady`.
