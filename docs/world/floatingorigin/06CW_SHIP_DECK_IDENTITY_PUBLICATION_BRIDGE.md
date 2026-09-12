# T-FO06CW — ShipDeck identity publication bridge

Дата: 2026-09-12.

## Цель

После T-FO06CV создать dormant, explicit bridge от owner-reviewed стабильной идентичности корабля к существующему lifecycle producer binding. Bridge не выполняет runtime discovery, не подключается к `OnNetworkSpawn`/`OnNetworkDespawn`, не создаёт generations и не изменяет сцены, prefabs или `BootstrapScene`.

## Реализация

Созданы:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckIdentityPublicationBridge.cs`
- `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionShipDeckIdentityPublicationBridge.cs`

Bridge:

- принимает только caller-supplied `ShipController.ShipPersistentId` и supplemental `NetworkObjectId`;
- требует caller role `NpcShipController`;
- требует ingress `ShipRegistered | ShipInvalidated`;
- требует owner/server/protocol ownership и отдельное подтверждение terminal invalidation owner-а;
- проверяет `ShipCrewManifest.shipId == ShipPersistentId`;
- публикует identity ровно один раз;
- forwarding registration/invalidation выполняет через `GlobalMotionShipDeckPassengerLifecycleProducerBinding`;
- делегирует `IGlobalMotionShipDeckPassengerGenerationSource` существующему binding;
- terminal invalidation использует coordinator-owned `ShipLifetimeGeneration` и не создаёт собственную generation.

## Проверка

- `validate_script` bridge: `0 warnings / 0 errors`.
- `validate_script` validator: `0 warnings / 0 errors`.
- `check_compile_errors`: `No compile errors`.
- T-FO06CW pure Edit Mode validator: `11/11 PASS`, `0 FAIL`.
- Regression validators: T-FO06CV `11/11 PASS`, T-FO06CU `10/10 PASS`, T-FO06CT `12/12 PASS`.
- `git diff --check`: выполняется перед commit.
- Play Mode и screenshots: `NOT RUN`, согласно manual-playtest-only.

## Граница доказательств

Bridge остаётся dormant. Caller binding к `NpcShipController`, `ShipCrewSpawner`, `NpcBrain` и `ShipDeckNav` не выполнен. Persistence fallback `scene.name/gameObject.name` через rename, pooling, scene reload и respawn не доказан. Terminal invalidation timing и runtime lifecycle ordering не доказаны. `GlobalMotionNativeAdapterSet=EMPTY / UNSEALED`; `runtimeRebaseReadiness=NOT_READY`; search outside audited paths остаётся `INCONCLUSIVE`.

## Файлы этапа

- `GlobalMotionShipDeckIdentityPublicationBridge.cs`
- `ValidateGlobalMotionShipDeckIdentityPublicationBridge.cs`
- `06CW_SHIP_DECK_IDENTITY_PUBLICATION_BRIDGE.md`
- `06CW_SHIP_DECK_IDENTITY_PUBLICATION_BRIDGE.json`
- `00_ARCHITECTURE_AND_PLAN.md`
- `Assets/_Project/Docs/ITERATIONS.md`
