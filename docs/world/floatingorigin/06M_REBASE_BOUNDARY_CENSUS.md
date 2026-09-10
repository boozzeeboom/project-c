# T-FO06M — read-only rebase boundary census

Дата: 2026-09-10. Предыдущий этап: T-FO06L.2.x follow-up 10.

## Граница этапа

Выполнен только read-only Edit Mode census после успешного native startup/player gate. `WorldScene_0_0` была открыта additive вместе с canonical `BootstrapScene`; сцены, префабы, catalog/profile и runtime code не изменялись и не сохранялись. Play Mode, screenshots, physics simulation и origin shift не выполнялись.

Цель — получить фактические instance-level bounds и ownership inputs перед проектированием atomic rebase transaction.

## 1. City content и Respawn_Default

Для `Assets/_Project/Scenes/World/WorldScene_0_0.unity` получены следующие данные:

| Метрика | Значение |
|---|---:|
| Scene roots | 58 |
| Descendant GameObjects | 10 236 |
| `WorldRoot_0_0` direct children | 8 |
| Renderers под `WorldRoot_0_0` | 6 345 |
| Colliders под `WorldRoot_0_0` | 1 004 |

`WorldRoot_0_0` имеет position `(0, 0, 0)`, identity rotation и scale `(1, 1, 1)`.

Агрегированные bounds всех descendants под `WorldRoot_0_0`:

- Renderers: center `(39995.240, 2713.469, 36717.140)`, size `(78866.400, 5734.984, 70395.140)`;
- Renderers min/max: `(562.040, -154.023, 1519.570)` → `(79428.440, 5580.961, 71914.710)`;
- Colliders: center `(40894.710, 2392.994, 36170.610)`, size `(73852.910, 2216.482, 69762.030)`;
- Colliders min/max: `(3968.255, 1284.753, 1289.595)` → `(77821.165, 3501.235, 71051.625)`.

`Respawn_Default` — отдельный root-level object, не child `WorldRoot_0_0`:

- position `(39992.000, 0.000, 40000.000)`;
- rotation identity;
- собственных Renderer/Collider bounds нет;
- delta относительно `WorldRoot_0_0`: `(39992, 0, 40000)`;
- расстояние от Unity origin: примерно `56571` units.

Относительно `Respawn_Default` агрегированный центр renderer bounds находится в `(3.240, 2713.469, -3282.860)`, а агрегированный центр collider bounds — в `(902.710, 2392.994, -3829.390)`.

### Ограничение

Bounds являются текущими Unity world-space AABB загруженной сцены. Они не доказывают, что весь runtime-streamed/pool-generated content уже представлен в сцене, и не определяют допустимый состав будущей rebase transaction. Static city content нельзя сдвигать вместе с scene-owned `NetworkObject` roots только по факту общего hierarchy anchor.

## 2. Ship/Rigidbody instance boundary

В loaded `WorldScene_0_0` обнаружено ровно `22` ship-like Rigidbody roots. Каждый root имеет `Rigidbody` и `NetworkObject`; все 22 входят в отдельную динамическую boundary и не относятся к общей static-city transaction.

Общий Edit Mode snapshot:

- `Rigidbody.mass = 1000` у всех 22 экземпляров;
- `interpolation = Interpolate` у всех 22;
- `collisionDetectionMode = Discrete` у всех 22 в текущем scene snapshot;
- velocity и angular velocity равны нулю в Edit Mode snapshot и не являются runtime доказательством;
- center of mass не переопределён отдельным authored override: значения `centerOfMass` получены как текущий Rigidbody-local результат Unity;
- world positions находятся примерно в диапазонах `X=39537.000…40553.130`, `Y=2501.380…2505.430`, `Z=39633.410…40035.300`.

Первые два light/reference roots не имеют `ShipDeckNav`; остальные 20 именованных ship roots имеют `ShipDeckNav` и по 7 attached colliders. У двух light roots обнаружено соответственно 9 и 6 colliders.

| Root | World position | Collider AABB size | Colliders | `ShipDeckNav` |
|---|---:|---:|---:|---|
| `Гигант` | `(39537.000,2501.810,39653.000)` | `(272.953,119.582,107.195)` | 7 | yes |
| `Пещера` | `(39731.300,2501.380,39633.410)` | `(26.602,58.480,221.844)` | 7 | yes |
| `Мастодонт` | `(39862.100,2501.380,39694.930)` | `(133.000,54.729,104.484)` | 7 | yes |
| `Странник` | `(39992.180,2501.380,39706.100)` | `(27.500,11.641,127.758)` | 7 | yes |
| `Олимп` | `(40064.800,2501.380,39704.500)` | `(29.258,11.727,72.266)` | 7 | yes |
| `Вавилон` | `(40159.300,2501.380,39665.790)` | `(69.156,13.099,148.188)` | 7 | yes |
| `Берег` | `(40260.400,2501.380,39713.500)` | `(22.000,11.641,51.156)` | 7 | yes |
| `Река` | `(40373.100,2501.380,39714.700)` | `(22.000,11.641,57.000)` | 7 | yes |
| `Угольщик` | `(40436.400,2501.380,39710.900)` | `(21.758,11.759,28.797)` | 7 | yes |
| `Торренс` | `(40480.000,2501.380,39744.780)` | `(10.641,11.641,56.250)` | 7 | yes |
| `Ветроворот` | `(40537.200,2501.380,39710.700)` | `(49.281,12.709,30.023)` | 7 | yes |
| `Летучий` | `(40188.860,2501.380,40034.800)` | `(20.000,11.641,45.000)` | 7 | yes |
| `Скат` | `(40228.330,2501.380,40032.800)` | `(9.000,11.641,48.000)` | 7 | yes |
| `Горгона` | `(40266.200,2501.380,40031.550)` | `(13.602,11.641,52.500)` | 7 | yes |
| `Цитадель` | `(40311.700,2501.380,40029.950)` | `(33.000,11.910,57.383)` | 7 | yes |
| `Альбатрос` | `(40367.400,2501.380,40018.500)` | `(13.203,11.641,80.563)` | 7 | yes |
| `Шмель` | `(40413.800,2501.380,40022.700)` | `(12.719,11.641,45.359)` | 7 | yes |
| `Сильфида` | `(40456.430,2501.380,40016.300)` | `(13.359,11.641,58.648)` | 7 | yes |
| `Жук` | `(40507.470,2501.380,40015.320)` | `(17.523,11.641,59.703)` | 7 | yes |
| `Лорейн` | `(40553.130,2501.380,40035.300)` | `(7.203,11.641,18.000)` | 7 | yes |
| `Ship_Light_root (копия с компьютера DESKTOP-K00O7HK)` | `(40118.830,2503.600,40001.660)` | `(6.000,12.780,18.133)` | 9 | no |
| `Ship_Light_root` | `(40120.700,2505.430,39973.300)` | `(8.859,12.417,15.828)` | 6 | no |

### Rigidbody transaction requirements

Общий direct shift этих roots запрещён. Будущая transaction должна для каждого root отдельно сохранять и восстанавливать как минимум:

- world pose и authored/global identity;
- center of mass semantics;
- interpolation и collision detection mode;
- linear/angular velocity;
- sleep/constraints/kinematic state;
- attached collider representation;
- `NetworkObject`/`NetworkTransform` state;
- ship telemetry/cache state;
- `ShipDeckNav` registration and cached origin.

Нулевые velocities из Edit Mode не являются основанием пропустить snapshot/restore в runtime.

## 3. ShipDeckNav boundary

В исходнике `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` подтверждено:

- `_registerUnderShip = true`;
- `_navFrameSeparation = 5000m`;
- `Register()` устанавливает `_navFrameOrigin = transform.position` и `_lastRegisteredShipPos = transform.position`;
- `NavMesh.AddNavMeshData(_deckNavMeshData, _navFrameOrigin, Quaternion.identity)` регистрирует baked data в текущей позиции корабля;
- `LateUpdate()` отслеживает горизонтальный drift относительно `_lastRegisteredShipPos`;
- threshold re-registration — `2500m` (`_navFrameSeparation / 2`);
- cooldown после re-registration — `30s`;
- registration queue ограничена `1` `AddNavMeshData` за кадр.

В текущем Edit Mode snapshot у 20 `ShipDeckNav`:

- baked `NavMeshData` присутствует;
- `_navFrameOrigin` и `_lastRegisteredShipPos` равны `(0,0,0)`, поскольку runtime registration ещё не выполнялась;
- реальные runtime registration origins, `NavMeshDataInstance` bounds и состояние `NavMeshAgent` остаются **UNVERIFIED**.

Отдельно подтверждены пользовательским runtime логом ошибки `Failed to create agent because it is not close enough to the NavMesh` для deck registrations. Это делает `ShipDeckNav` отдельной transaction boundary: нельзя сдвигать только Transform корабля, оставляя nav origin, baked data, agent goals или cached registration position в старой системе координат.

## 4. Camera boundary

В loaded Edit Mode snapshot активная камера — root-level `MainCamera` в `BootstrapScene`:

- scene path: `Assets/_Project/Scenes/BootstrapScene.unity`;
- tag `MainCamera`;
- position `(239997,3000,159998)`;
- components: `Camera`, `AudioListener`, URP camera data и `GlobalSceneSourceMarker`;
- `SpringArmCamera` на этом scene instance отсутствует.

Pilot player prefab содержит ссылку на `Assets/_Project/Prefabs/ThirdPersonCamera.prefab` через `NetworkPlayer.cameraPrefab`. Этот prefab содержит `ProjectC.Core.SpringArmCamera`, а `target` в authored prefab равен `None`; target назначается runtime. `SpringArmCamera.LateUpdate()` использует target, `_lagTargetPos`, SmoothDamp velocity fields и `Snap()`/auto-snap paths.

`Assets/_Project/Prefabs/MainCamera.prefab` отдельно содержит legacy `FloatingOriginMP` с `threshold=100000`, `shiftRounding=10000` и root-name heuristics. Это asset-level legacy component; наличие prefab не доказывает, что он является активной камерой canonical Bootstrap pilot. В текущем loaded Bootstrap instance `FloatingOriginMP` не обнаружен.

### Camera conclusion

Точный runtime owner после player spawn, момент назначения `ThirdPersonCamera` и связь camera history с `GlobalMotionFrame` этим Edit Mode census не подтверждены. Поэтому camera lag state, `_lagTargetPos`, SmoothDamp velocity и active camera Transform должны входить в будущий rebase snapshot, но ownership остаётся **INCONCLUSIVE** до пользовательского runtime instrumentation.

## 5. Lifecycle boundary

Статический кодовый порядок остаётся:

- `GlobalMotionWorld` — `DefaultExecutionOrder(-20000)`, `Update()` baseline preparation и `FixedUpdate()` pose application;
- `GlobalMotionPoseAdapter` — `DefaultExecutionOrder(10000)`, `LateUpdate()` finalization/display path;
- `ShipDeckNav` — обычный `LateUpdate()`;
- `SpringArmCamera` — `LateUpdate()`.

Это задаёт intended ordering, но не доказывает точную межсистемную последовательность NGO tick → FixedUpdate → rebase → interpolation в runtime. Реальный tick correlation, physics step ordering и NetworkTransform interpolation history остаются **UNVERIFIED** и требуют пользовательского Play Mode instrumentation.

## 6. Decision

T-FO06M закрывает read-only asset/scene boundary census, но не разрешает implementation общего rebase shift.

Подтверждено:

- city render/collider aggregate bounds;
- `Respawn_Default` world position и отсутствие собственного collider;
- ровно 22 ship/Rigidbody roots и их текущие scene-level bounds;
- authored `ShipDeckNav` registration contract;
- scene MainCamera и player camera prefab split.

Остаётся inconclusive:

- streamed/pool-generated city content вне loaded authored scene;
- runtime ship velocities, sleep/interpolation histories и actual NavMesh registration origins;
- точный runtime camera owner/target/history;
- NGO tick/physics/interpolation synchronization;
- moving-platform passenger state во время потенциального shift.

Следующий этап должен быть отдельным design-only шагом: определить closed-world participant set и atomic transaction contract по этим boundary facts. Не включать `FloatingOriginMP`, не выполнять player-only shift, не использовать общий `SetParent` для `WorldRoot_0_0`, scene-owned `NetworkObject` или ship/Rigidbody roots.
