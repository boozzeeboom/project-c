# T-FO06CL — ShipDeck lifecycle owner audit

Дата: 2026-09-12  
Статус: **AUDITED / INCONCLUSIVE / INTEGRATION BLOCKED**

## 1. Цель

После `T-FO06CK` определить, существует ли legitimate server-owned lifecycle owner для `IGlobalMotionShipDeckPassengerGenerationSource` и может ли он быть связан с существующими ShipDeck attachment/spawn seams.

Аудит выполнен read-only. `NpcBrain`, `ShipCrewSpawner`, `NpcShipController`, provider, adapter set, BootstrapScene и runtime state не изменялись.

## 2. Проверенные источники

```text
Assets/_Project/Scripts/AI/NpcBrain.cs
Assets/_Project/Scripts/AI/Editor/NpcBrainEditor.cs
Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs
Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerGenerationContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleProducerContract.cs
```

## 3. Подтверждённые seams

В audited paths подтверждены реальные server-side attachment и spawn seams:

- `NpcBrain` предоставляет explicit server-side attach/detach lifecycle с проверками spawned ship, parent attachment и `ShipDeckNav` readiness;
- `ShipCrewSpawner` является реальным server-owned producer-ом crew spawn, но не ведёт generation ledger и не выдаёт lifecycle receipts;
- `NpcShipController` регистрирует ship lifecycle, но не реализует `IGlobalMotionShipDeckPassengerGenerationSource`;
- существующие attachment/snapshot guards подтверждают readiness состояния, но не заменяют protocol-owned generation evidence.

## 4. Отсутствующая lifecycle ownership evidence

В проверенных путях не найдено:

- legitimate implementation `IGlobalMotionShipDeckPassengerGenerationSource`;
- monotonic ship lifetime или passenger attachment generation ledger;
- server-owned lifecycle receipts, связывающих ship registration/spawn/despawn с passenger attach/detach;
- runtime binding source к provider или adapter set.

`NetworkObject.IsSpawned`, object references и текущие attachment flags не используются как synthetic generation producer. Новый producer или inference из `IsSpawned` не создавались.

## 5. Вывод

Вердикт: **INCONCLUSIVE / INTEGRATION BLOCKED**.

Подтверждённые server-side attachment и spawn seams недостаточны для выбора legitimate lifecycle owner-а. Ни один из рассмотренных компонентов не предоставляет monotonic generation ledger и reviewed receipts, необходимые для `IGlobalMotionShipDeckPassengerGenerationSource`. Адаптация через object reference или `IsSpawned` создала бы synthetic producer и нарушила fail-closed lifecycle boundary.

## 6. Границы и неопределённость

```text
IGlobalMotionShipDeckPassengerGenerationSource implementation = NOT FOUND
monotonic generation ledger = NOT FOUND
server-side attachment and spawn seams = CONFIRMED
synthetic producer = NOT CREATED
IsSpawned inference = NOT USED
runtime binding = NOT EXECUTED
provider binding = NOT EXECUTED
adapter binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Поиск за пределами перечисленных audited paths остаётся **INCONCLUSIVE**. Это не является доказательством отсутствия lifecycle owner-а во всём проекте.

## 7. Следующий gate

Нужен отдельный read-only/design gate для выбора protocol-owned server lifecycle producer-а, который получает explicit ship registration/spawn/despawn и passenger attach/detach transitions, ведёт monotonic generation ledger и выдаёт immutable lifecycle receipts. Только после этого допустимы reviewed source binding, provider/adapter binding и runtime integration.
