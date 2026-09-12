# T-FO06CV — ShipDeck lifecycle stable identity and server boundary

Дата: 2026-09-12  
Статус: **SOURCE-LEVEL IDENTITY BOUNDARY IMPLEMENTED / RUNTIME PERSISTENCE UNVERIFIED**

## Цель

После `T-FO06CU` проверить, существует ли в проекте explicit stable identity source, который можно передать в ShipDeck lifecycle producer без использования `NetworkObjectId` как protocol identity.

## Подтверждённые источники

Read-only source audit подтвердил:

- `ProjectC.Player.ShipController.ShipPersistentId` — отдельное persistence identity поле;
- при заданном `_shipPersistentId` возвращается именно этот explicit ID;
- при пустом поле fallback формируется как `scene.name/gameObject.name`;
- `ShipCrewManifest.shipId` также документирован как stable ship assignment key и явно отделён от `NetworkObjectId`;
- `NpcShipController.NpcInstanceId` по умолчанию формируется из `NetworkObjectId` с sentinel bit и поэтому не используется как floating-origin protocol `ShipId`;
- `ShipCrewSpawner` принимает crew lifecycle через manifest/spawned member IDs, но не выдаёт ship lifetime или attachment generations;
- `ShipDeckNav` остаётся readiness seam и не является identity owner.

## Реализация

Добавлены:

- `GlobalMotionShipDeckStableIdentityBoundary`;
- `GlobalMotionShipDeckStableIdentityReceipt`;
- `ValidateGlobalMotionShipDeckStableIdentityBoundary`.

Boundary принимает только explicit `ShipController.ShipPersistentId`, supplemental `ShipNetworkObjectId`, owner review, server/protocol ownership и отдельное подтверждение terminal invalidation ownership.

Также добавлена проверка согласованности `ShipController.ShipPersistentId` и `ShipCrewManifest.shipId`. Mismatch или пустой manifest ID fail closed.

Contract не вызывает runtime callers, не изменяет `ShipController`, `NpcShipController`, `ShipCrewSpawner`, `NpcBrain`, `ShipDeckNav`, сцены или prefabs и не выдаёт lifecycle generations.

## Pure validation

- Stable identity boundary: **11/11 PASS**.
- `check_compile_errors`: **No compile errors**.
- Новые scripts: validation `0 warnings / 0 errors` по доступному source validation; повторная проверка validator после domain reload подтверждена Unity log.
- Play Mode: **NOT RUN**.

## Решение

`ShipController.ShipPersistentId` принят как единственный source-level кандидат для будущего stable protocol `ShipId`. `ShipCrewManifest.shipId` является отдельным cross-check и должен совпадать с ним перед runtime binding.

Это ещё не runtime proof: fallback `scene.name/gameObject.name` требует owner review для всех pool/scene-reload/rename сценариев, а terminal invalidation timing и lifecycle ordering не подтверждены.

## Граница

Реальный caller binding не выполнялся. `NpcShipController.OnNetworkSpawn/OnNetworkDespawn`, `ShipCrewSpawner`, `NpcBrain` и `ShipDeckNav` не изменялись. Provider, adapter set, combined host и `BootstrapScene` не подключались.

`runtimeRebaseReadiness=NOT_READY`. Search за пределами audited paths остаётся **INCONCLUSIVE**. Следующий gate — owner-reviewed explicit identity publication/caller binding и user-controlled runtime evidence, без автоматического вывода ID из object state.
