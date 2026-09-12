# T-FO06CJ — Combined ShipDeck host lifecycle binding preflight

Дата: 2026-09-12  
Статус: **PREFLIGHT VERIFIED / COMPILE_VERIFIED / PURE_ONLY / DORMANT**

## 1. Цель

Проверить фактический Edit Mode handoff reviewed passenger lifecycle receipts в `GlobalMotionShipDeckCombinedTransactionHost` после `T-FO06CI`.

Проверка использует временный component graph и уничтожает его в конце validator-а. Сетевой runtime, NavMesh lifecycle, сцены и Play Mode не запускаются.

## 2. Проверенный путь

```text
TryConfigureReviewedPassengers(NpcBrain[])
→ ReviewedPassengerBindingGeneration
→ TryConfigureReviewedPassengerLifecycleBinding(bindingGeneration, receipts[])
→ TryValidateReviewedPassengerLifecycleBinding()
```

Receipts создаются только explicit pure contract calls, а не из `NpcBrain`, `NetworkObject.IsSpawned` или scene discovery.

## 3. Изменение проверки

Обновлён:

```text
Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckCombinedTransactionHost.cs
```

Добавлены проверки:

- успешный handoff двух reviewed `NpcBrain` sources и двух `PassengerAttached` receipts;
- сохранение lifecycle binding в host;
- повторная валидация сохранённого binding;
- rejection при mismatched binding generation.

Существующие snapshot capture/rebuild/restore методы не вызываются как runtime operation; invalid transaction и null snapshot guards по-прежнему проверяются fail-closed.

## 4. Проверки

```text
T-FO06CB combined host validator = 9 pure checks PASS / 0 FAIL
check_compile_errors = No compile errors
script validation = 0 warnings / 0 errors
Play Mode = NOT RUN
runtime producer = NOT BOUND
```

## 5. Граница

```text
lifecycle handoff = Edit Mode preflight only
server-owned lifecycle producer = NOT IMPLEMENTED
IGlobalMotionShipDeckPassengerGenerationSource = NOT BOUND
GlobalMotionNativeAdapterSet = EMPTY / UNSEALED
BootstrapScene = UNCHANGED
runtimeRebaseReadiness = NOT_READY
```

Временные `GameObject` и компоненты удаляются validator-ом; постоянные сцены и префабы не изменяются.

## 6. Следующий gate

Следующий этап должен заменить pure test receipts legitimate server-owned lifecycle owner-ом. До этого host handoff остаётся explicit dormant API и не участвует в runtime rebase coordinator.
