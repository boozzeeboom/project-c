# T-FO06CD — passenger attachment and lifetime generation source gate

Дата: 2026-09-12
Статус: **CONTRACT GATE / SOURCE NOT FOUND / NOT INSTALLED**

## 1. Цель

Проверить и зафиксировать минимальную provenance boundary, необходимую перед adapter seam: passenger identity должна быть связана с конкретным ship lifetime и attachment generation, а не только с `NetworkObject.IsSpawned` и object reference.

## 2. Source audit

В проверенных исходниках подтверждены только следующие runtime facts:

- `NpcBrain` хранит explicit ship reference, attachment requested/active flags и resolved `ShipDeckNav`;
- `NetworkObject.IsSpawned` и `NetworkObjectId` доступны для текущего ship instance;
- `ShipCrewSpawner` создаёт/спавнит экипаж и вызывает `AttachToShipDeck`;
- отдельного server-owned monotonic ship spawn/lifetime generation producer-а не найдено;
- отдельного protocol-owned attachment generation ledger для `NpcBrain` не найдено.

Следовательно, `IsSpawned`, `NetworkObjectId`, `NpcBrain` reference и host binding generation нельзя объявить заменой lifetime ledger. По audited paths источник генераций остаётся **NOT FOUND**; поиск за пределами проверенных путей остаётся inconclusive.

## 3. Реализовано

Созданы:

- `GlobalMotionShipDeckPassengerGeneration`;
- `IGlobalMotionShipDeckPassengerGenerationSource`;
- `GlobalMotionShipDeckPassengerGenerationContract`.

Contract требует:

- passenger identity;
- ship `NetworkObjectId`;
- ненулевой ship spawn/lifetime generation;
- ненулевой attachment generation;
- deck identity;
- server ownership;
- protocol ownership.

Automatic discovery и inference из `IsSpawned` запрещены. Concrete producer не создан, поэтому combined host остаётся на прежнем explicit binding API.

## 4. Проверка

```text
check_compile_errors = No compile errors
T-FO06CD validator = 5 pure checks PASS / 0 FAIL
Play Mode = NOT RUN
generation source binding = NOT EXECUTED
```

## 5. Статус

```text
reviewed generation contract = PRESENT
concrete server-owned generation source = NOT FOUND
combined host adapter integration = BLOCKED
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
runtimeRebaseReadiness = NOT_READY
```

## 6. Следующий gate

Следующий этап должен либо подтвердить существующий server-owned producer с прямыми receipts, либо отдельно спроектировать protocol-owned lifecycle ledger. Нельзя поднимать `NativeReady` на основании `IsSpawned`, object references или host-local binding generation.
