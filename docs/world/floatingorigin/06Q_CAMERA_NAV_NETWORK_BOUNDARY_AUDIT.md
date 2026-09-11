# T-FO06Q — camera, ShipDeckNav and NGO boundary audit

Дата: 2026-09-11. Основание: T-FO06O identity census, T-FO06P candidate mapping и read-only source investigation по camera/NavMesh/network lifecycle.

## 1. Граница этапа

Этап закрывает только source-level boundary audit для будущих adapters. Runtime instrumentation, Play Mode, scene/prefab changes, transform mutation, physics simulation, NavMesh mutation, camera ownership mutation и network baseline mutation не выполнялись.

`FloatingOriginMP`, player-only shift и `GroundPlane_0_0` не включались.

## 2. Camera ownership and history

### Подтверждено исходниками

- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `SpawnCamera()` создаёт `SpringArmCamera`, именует экземпляр `ThirdPersonCamera_{OwnerClientId}`, вызывает `SetTarget(player.transform)` и `InitializeCamera()`.
- Spawned camera является отдельным root и не parentится к player.
- `NetworkPlayer._myCamera` хранит binding и уничтожается при despawn.
- `Assets/_Project/Scripts/Core/SpringArmCamera.cs` хранит camera history/lag state: `_lagTargetPos`, `_lagSpeed`, `_lastClearTime`, `_collisionExitTime`, `_wasColliding`, `_lastCollisionPos`.
- `InitializeCamera()` устанавливает `Billboard.ActiveCamera`.
- Loaded `BootstrapScene/MainCamera` остаётся единственным Edit Mode camera candidate из T-FO06O, но это не доказывает, что он является runtime-owned active camera после player spawn.

### Gate

Для camera adapter обязательны runtime evidence и explicit binding:

- фактический owner после `NetworkPlayer.SpawnCamera()`;
- target lifetime и повторная инициализация при respawn/despawn;
- сохранение/сброс `_lagTargetPos`, `_lagSpeed` и collision history при frame change;
- порядок camera history update относительно player pose, physics sync и network baseline;
- согласованность `Billboard.ActiveCamera` при нескольких clients.

Статус: **INCONCLUSIVE**. Camera adapter и camera mutation не создаются.

## 3. ShipDeckNav and ship Rigidbody boundary

### Подтверждено исходниками

- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` — `OnNetworkSpawn()` ставит компонент в pending registration queue.
- `LateUpdate()` обрабатывает максимум одну регистрацию за кадр.
- `Register()` кэширует `_navFrameOrigin` и `_lastRegisteredShipPos`, затем вызывает `NavMesh.AddNavMeshData`.
- При drift больше половины `_navFrameSeparation` компонент вызывает `Unregister()`, повторно ставит регистрацию в очередь и применяет cooldown `30s`.
- `ShipDeckNav` использует `ShipRootReference` для связи с ship controller, Rigidbody и NetworkObject.
- Edit Mode census подтверждает `20` deck-nav candidates и `22` ship-root candidates; это не доказывает runtime registration, valid `NavMeshDataInstance`, current nav origin или passenger provenance.

### Gate

До ShipDeckNav adapter обязательны runtime evidence:

- фактический момент регистрации относительно NGO spawn, Rigidbody pose и physics step;
- поведение при сдвиге ship root и одновременном drift нескольких ships;
- сохранение deck-local/global provenance для passengers и NPC;
- NavMesh origin, registration generation и rollback/re-registration acknowledgement;
- порядок `NavMesh` rebuild относительно `Physics.SyncTransforms` и network publish.

Статус: **INCONCLUSIVE**. ShipDeckNav registration не считается готовой к Apply/Rebuild.

## 4. NGO tick, physics and baseline boundary

### Подтверждено исходниками

- `GlobalMotionReplicator.OnNetworkSpawn()` подписывается на `NetworkManager.NetworkTickSystem.Tick`.
- `OnNetworkTick()` обновляет server control/revision и периодически публикует reliable control keyframe.
- `Physics.SyncTransforms()` вызывается в `PlayerPositionServer.RestorePosition()`, `PlayerRespawnTracker` и `GlobalMotionPoseAdapter.ApplyFixedPose()`.
- Legacy `NetworkTransform` path в `NetworkPlayer.OnNetworkSpawn()` отключает interpolation и устанавливает owner authority для CharacterController-пути.
- Global motion snapshots содержат binding, sequence, space, parent identity, pose и sample time; discontinuity state меняется при authority/parent-space transitions.

### Gate

Source code подтверждает наличие отдельных hooks, но не их exact runtime ordering. Не доказаны:

- relation `NetworkTickSystem.Tick` ↔ `FixedUpdate` ↔ `Physics.SyncTransforms`;
- момент, когда NetworkTransform/global baseline перестаёт принимать старую pose после rebase;
- ordering для camera history, ShipDeckNav re-registration и publish;
- Host-specific двойная роль server/client;
- rollback of native Unity state after partial Apply.

Статус: **INCONCLUSIVE**. Runtime transaction remains fail-closed.

## 5. Scene-owned NetworkObject policy

Source contract distinguishes:

- `BootstrapService` / `PersistentBootstrapService` — BootstrapScene services without Rigidbody;
- `SceneOwnedNetworkGameplay` — WorldScene spatial gameplay object with NetworkObject and explicit catalog ownership;
- `ShipOrRigidbodyRoot` — WorldScene spatial root requiring NetworkObject + Rigidbody.

T-FO06P classified all `96` NetworkObject census records with `admitted=false`. No automatic `NETWORK_GAMEPLAY_ROOT` entry is created. Exact ownership, spawn lifetime, authority and spatial participation still require reviewed policy per object/root.

## 6. Решение этапа

Source-level camera/Nav/network boundary audit — **PASS**.

Runtime readiness — **NOT READY**. The audit narrows the required instrumentation gates but does not authorize a camera adapter, ShipDeckNav adapter, live manifest publication, `Apply/Rebuild/Validate/Publish` or frame mutation.

Следующий этап: подготовить только dormant, explicit runtime probes/contracts для user-controlled Play Mode capture, либо получить отдельное reviewed решение по camera/deck/network policy. Автоматический Play Mode не запускается.
