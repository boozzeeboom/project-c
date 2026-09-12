# T-FO06CG — ShipDeck passenger lifecycle producer contract

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Цель

После аудита `T-FO06CF` создан protocol-owned contract для будущего server-owned producer-а ship lifetime и passenger attachment generations.

Это contract boundary, а не runtime producer. Он не читает NGO, не вызывает `NpcBrain`/`ShipDeckNav` и не выдаёт generation из `NetworkObject.IsSpawned` или object reference.

## 2. Реализация

Добавлены:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleProducerContract.cs
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckPassengerLifecycleProducerContract.cs
```

Receipt содержит:

- `ShipNetworkObjectId`;
- reviewed `ShipSpawnGeneration`;
- passenger и deck identity после attach;
- monotonic `AttachmentGeneration`;
- server/protocol ownership;
- lifecycle phase и ordinal.

## 3. Lifecycle phases

```text
ShipRegistered
→ PassengerAttached
→ PassengerDetached
→ PassengerAttached (только с новой AttachmentGeneration)
→ ShipInvalidated
```

`ShipInvalidated` является terminal phase. Инвалидация допускается как до attachment, так и после attachment, чтобы ship lifetime мог быть закрыт независимо от наличия пассажира.

Контракт запрещает:

- нулевой ship/lifetime identity;
- отсутствие server/protocol ownership;
- attach без passenger/deck identity;
- повторное использование attachment generation;
- detach из неправильной фазы;
- повторную инвалидацию.

## 4. Проверки

```text
T-FO06CG validator = 8 pure checks PASS / 0 FAIL
check_compile_errors = No compile errors
script validation = 0 warnings / 0 errors
Play Mode = NOT RUN
runtime producer = NOT BOUND
```

В ходе проверки были исправлены два fail-closed сценария валидатора: повторное присоединение теперь проверяется после detach, а unbound ship может корректно перейти в terminal `ShipInvalidated`.

## 5. Граница

```text
concrete server-owned producer = NOT IMPLEMENTED
IGlobalMotionShipDeckPassengerGenerationSource binding = NOT EXECUTED
NpcBrain integration = NOT EXECUTED
ShipDeckNav integration = NOT EXECUTED
GlobalMotionShipDeckPassengerProtocolLedger = DORMANT
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
BootstrapScene = UNCHANGED
runtimeRebaseReadiness = NOT_READY
```

Contract предоставляет reviewed receipt shape для следующего producer-а, но сам не создаёт generation values и не может открыть `NativeReady`.

## 6. Следующий gate

Следующий этап — определить один server-owned lifecycle owner, который будет выдавать эти receipts на реальных ship spawn/despawn и passenger attach/detach transitions. До explicit binding нельзя добавлять `TryGetGeneration()` в `NpcBrain` и нельзя подключать ledger к runtime.
