# T-FO06CT — ShipDeck lifecycle producer binding and explicit fact-source ingress

Дата: 2026-09-12  
Статус: **PURE CONTRACT IMPLEMENTED / RUNTIME CALLER BINDING DORMANT**

## Цель

После `T-FO06CS` добавить узкий explicit binding façade между owner-reviewed server/gameplay fact callers и `GlobalMotionShipDeckPassengerLifecycleProducerBridge`. Binding должен сохранять coordinator provenance и подготовить typed active-passenger handoff для `GlobalMotionShipDeckCombinedTransactionHost`, не выполняя discovery и не выводя identity/generation из Unity/NGO state.

## Реализация

Добавлен `GlobalMotionShipDeckPassengerLifecycleProducerBinding`.

Он:

- принимает только explicit typed ingress для `ShipRegistered`, `PassengerAttached`, `PassengerDetached` и `ShipInvalidated`;
- делегирует все lifecycle facts существующему producer bridge;
- сохраняет только accepted downstream receipts;
- фиксирует stable protocol `ShipId`, supplemental `ShipNetworkObjectId` и coordinator-owned ship lifetime generation после регистрации;
- хранит active passenger receipts по stable `PassengerId`;
- удаляет только exact passenger receipt после accepted detach;
- очищает active roster при accepted terminal invalidation;
- создаёт deterministic, ordinal-independent active receipt array, сортированный по `PassengerId`, и передаёт его в существующий `GlobalMotionShipDeckPassengerLifecycleSourceBindingContract`;
- реализует `IGlobalMotionShipDeckPassengerGenerationSource` через delegation к producer bridge.

После terminal invalidation binding блокирует дальнейшие attach/detach/register/build операции. Rejected ingress не изменяет binding state.

## Pure validation

Добавлен `ValidateGlobalMotionShipDeckPassengerLifecycleProducerBinding` с `12/12 PASS`:

1. registration сохраняется как lossless downstream receipt;
2. attachment появляется в active roster;
3. active binding сортируется детерминированно и проходит source-binding contract;
4. detach закрывает exact active epoch;
5. reattach получает новую coordinator-owned generation;
6. invalidation очищает active state и блокирует binding;
7. duplicate registration и post-invalidation registration отклоняются;
8. attach до регистрации и mismatched ship identity отклоняются;
9. ownership rejection не изменяет state;
10. пустой active roster отклоняется;
11. mismatched lifetime generation отклоняется без мутации;
12. generation source делегирует coordinator-owned generation.

Также повторно пройдена регрессия T-FO06CS: `11/11 PASS`.

## Проверки

- `check_compile_errors`: **No compile errors**.
- `validate_script` binding: **0 warnings / 0 errors**.
- `validate_script` validator: **0 warnings / 0 errors**.
- T-FO06CT pure validator: **12/12 PASS**.
- T-FO06CS regression validator: **11/11 PASS**.
- Play Mode: **NOT RUN** согласно `manual-playtest-only`.
- Сцены и prefabs: **не изменялись**.
- `BootstrapScene`: **не изменялась**.
- Provider, `GlobalMotionNativeAdapterSet`, runtime driver и combined-host runtime binding: **не подключались**.
- `runtimeRebaseReadiness`: **NOT_READY**.

## Граница и оставшийся gate

Это не является доказательством runtime integration. Реальные callers (`NpcShipController`, `ShipCrewSpawner`, `NpcBrain`, `ShipDeckNav`) ещё не передают факты в binding. Stable protocol identities, owner-reviewed caller ownership, runtime ordering и terminal invalidation должны быть подтверждены отдельным user-controlled/runtime gate. Автоматический discovery и synthetic identity запрещены.

Search за пределами ранее audited paths остаётся **INCONCLUSIVE**. Следующий gate должен быть owner-reviewed decision/runtime capture для конкретного caller boundary, а не автоматическая установка binding в `BootstrapScene`.
