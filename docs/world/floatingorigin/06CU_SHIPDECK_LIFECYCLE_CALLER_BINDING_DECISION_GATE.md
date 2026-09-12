# T-FO06CU — ShipDeck lifecycle caller binding decision gate

Дата: 2026-09-12  
Статус: **EXPLICIT CALLER CONTRACT IMPLEMENTED / RUNTIME BINDING BLOCKED**

## Цель

После `T-FO06CT` определить минимальную границу owner-reviewed caller binding для `NpcShipController`, `ShipCrewSpawner`, `NpcBrain` и `ShipDeckNav`, не подключая неподтверждённые runtime seams и не подменяя stable protocol identity транспортным `NetworkObjectId`.

## Исходные факты

Read-only source review подтвердил:

- `NpcShipController.OnNetworkSpawn` и `OnNetworkDespawn` владеют server-side ship registration/despawn seam;
- `NpcShipController.NpcInstanceId` по умолчанию создаётся как `NetworkObjectId | 0x8000_0000_0000_0000UL`, поэтому он не принят как stable protocol `ShipId` для floating-origin lifecycle;
- `ShipCrewSpawner` владеет crew spawn/despawn, но не выдаёт coordinator-owned attachment generations;
- `NpcBrain` владеет attach/detach seam, но не имеет unified ship lifetime ledger;
- `ShipDeckNav` владеет asynchronous readiness prerequisite, но не является lifecycle identity owner.

Поэтому прямое добавление вызовов в `NpcShipController`, `ShipCrewSpawner`, `NpcBrain` или `ShipDeckNav` сейчас было бы недоказанным runtime binding и нарушило бы fail-closed границу `T-FO06CQ`.

## Реализация

Добавлен pure contract `GlobalMotionShipDeckLifecycleCallerBindingContract` с explicit receipt:

- caller role;
- caller-supplied stable protocol `ShipId`;
- supplemental `ShipNetworkObjectId`;
- заявленный ingress mask;
- explicit terminal invalidation support;
- owner review;
- server ownership;
- protocol ownership.

Contract не создаёт identity, generation, ordering или runtime callbacks. Он только допускает будущий binding после owner review.

Добавлен validator `ValidateGlobalMotionShipDeckLifecycleCallerBindingContract`.

Результат: **10/10 pure checks PASS**.

## Решение по callers

- `NpcShipController`: кандидат для `ShipRegistered` + `ShipInvalidated`, но stable protocol `ShipId` и timing terminal invalidation не подтверждены.
- `ShipCrewSpawner`: кандидат для crew spawn/despawn seams, но attachment completion и generations не подтверждены.
- `NpcBrain`: фактический attach/detach seam, но не owner ship lifetime generation.
- `ShipDeckNav`: prerequisite readiness, не lifecycle producer.

Ни один caller не подключён к producer binding. Synthetic identity из `NetworkObjectId`, `NpcInstanceId`, object references, callback order или `ShipDeckNav.RegistrationGeneration` запрещена.

## Проверки

- `check_compile_errors`: **No compile errors**.
- Новый caller contract validator: **10/10 PASS**.
- Binding и bridge runtime validators: ранее подтверждённые `12/12 PASS` и `11/11 PASS`.
- Script validation: **0 warnings / 0 errors** для новых scripts.
- Play Mode: **NOT RUN**.
- Сцены, prefabs и `BootstrapScene`: **не изменялись**.
- Provider, `GlobalMotionNativeAdapterSet`, runtime driver и combined host: **не подключались**.
- `runtimeRebaseReadiness`: **NOT_READY**.

## Блокеры

1. Нет owner-reviewed стабильного protocol `ShipId`, сохраняющегося через scene reload/pool/respawn.
2. Нет подтверждённого единого producer-а для всех lifecycle phases.
3. Нет explicit terminal invalidation boundary до/в момент despawn.
4. Нет runtime evidence порядка ship registration → passenger attachment → detach → invalidation.

Search за пределами audited paths остаётся **INCONCLUSIVE**. Следующий допустимый этап — owner-reviewed предоставление stable identity и конкретного server boundary, после чего возможен отдельный runtime caller binding/evidence gate. Автоматическое подключение в `BootstrapScene` не выполнялось.
