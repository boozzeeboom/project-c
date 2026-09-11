# T-FO06N — closed-world rebase participant set and atomic transaction contract

Дата: 2026-09-11. Предыдущие этапы: T-FO06L static pilot startup/player gate, T-FO06M read-only boundary census.

## 1. Граница этапа

Этот этап является **design-only**. Он не меняет C# runtime, сцены, префабы, catalog/profile, `GroundPlane_0_0`, `FloatingOriginMP` или NGO settings. Play Mode, physics simulation и screenshots на этом этапе не выполняются.

Цель — превратить подтверждённые boundary facts в закрытый participant set и фазовый контракт будущего rebase transaction. Любая граница, для которой нет достаточного runtime-доказательства, остаётся `INCONCLUSIVE` и блокирует implementation, а не превращается в implicit participant.

## 2. Доказательная база

Используются только результаты `06M_REBASE_BOUNDARY_CENSUS.md` и пользовательского startup log от `2026-09-11 07:36:57`:

- `WorldRoot_0_0`: 10 236 descendants, 6 345 renderers, 1 004 colliders, position `(0,0,0)`;
- `Respawn_Default`: отдельный root-level marker в `(39992,0,40000)`, без собственного Renderer/Collider;
- loaded `WorldScene_0_0`: ровно 22 ship-like `Rigidbody + NetworkObject` roots;
- 20 ship roots имеют `ShipDeckNav`, 2 light/reference roots не имеют deck navigation;
- pilot startup прошёл с `ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>` и static frame `origin=(0,0,0)`, local player `(39992,1,40000)`;
- runtime deck registration, camera owner/history, NGO tick/physics order, streamed content и moving-platform passenger state остаются не полностью подтверждены.

## 3. Closed-world participant set

### 3.1 Обязательные transaction participants

| ID | Участник | Правило допуска |
|---|---|---|
| `CITY_STATIC` | Явный набор static render/collider content под `WorldRoot_0_0` | Включаются только entries из reviewed manifest. Общий `SetParent(WorldRoot_0_0)` не является способом допуска; unknown descendants блокируют transaction. |
| `WORLD_ANCHORS` | `Respawn_Default` и прочие явно reviewed world anchors | Каждый anchor имеет собственную identity/pose запись. Marker нельзя молча оставить в старой системе координат. |
| `SHIP_ROOT[n]` | Каждый из 22 ship/Rigidbody roots | Каждый root обрабатывается отдельно с собственной pose, Rigidbody, collider, NetworkObject и identity snapshot. |
| `SHIP_DECK_NAV[n]` | Каждый из 20 `ShipDeckNav` participants | NavMesh origin, baked data registration, cached ship position и pending registration входят в отдельный sub-transaction. |
| `PLAYER_FRAME` | Global frame, pilot actor placement, controller state и accepted global pose | Игрок не является обычным child static-city root. Frame generation, baseline/discontinuity и controller caches меняются согласованно. |
| `CAMERA` | Активная camera chain и history state | Точный runtime owner пока не подтверждён; implementation допускается только после runtime instrumentation и явного binding. |
| `NETWORK_GAMEPLAY_ROOT[n]` | Spatial scene-owned NetworkObject/NPC/crew roots, если они будут включены в reviewed runtime manifest | Каждый root получает отдельную classification и participant token. Не допускается перемещение неизвестных NetworkObject вместе с городом. |

`CITY_STATIC` и `SHIP_ROOT[n]` не объединяются общим parent. `PLAYER_FRAME`, `CAMERA` и network baselines являются логическими участниками transaction даже когда их Transform не сдвигается тем же способом, что static content.

### 3.2 Неявно исключённые или пока заблокированные границы

Следующие категории не могут быть сдвинуты best-effort:

- `DontDestroyOnLoad` bootstrap services и UI roots — они не являются world-space static content;
- `FloatingOriginMP` — legacy heuristic shift controller не включается как часть новой transaction;
- unknown authored roots, pool-generated/streamed content без manifest identity;
- scene-owned `NetworkObject`, NPC, crew или physics roots без отдельного participant token;
- `GroundPlane_0_0` — объект ранее удалён и не должен восстанавливаться;
- любой participant с pathless/invalid/duplicate scene identity;
- любой ship passenger/deck state, для которого нет parent/local/global provenance.

## 4. Atomic transaction contract

Transaction имеет один immutable `transactionId`, старый и новый `GlobalMotionFrame`, signed shift offset, source tick/physics step, participant manifest digest и monotonic frame generation. Ни один partial shift не публикуется как готовый новый frame.

### Фазы

1. **REQUEST** — authority формирует кандидата только из finite offset и согласованного frame generation.
2. **FREEZE** — transaction привязывается к конкретному NGO tick и physics step; новые movement, parenting, nav requests и competing rebase requests блокируются.
3. **PREFLIGHT** — повторно проверяются loaded scene handles, catalog scope, participant manifest digest, ownership, active writers, frame identity, NetworkObject lifecycle, Rigidbody/Joint constraints, camera binding и deck registrations. Unknown, duplicate или stale participant немедленно блокирует transaction.
4. **CAPTURE** — каждый participant выдаёт immutable rollback snapshot: pose/identity, Rigidbody state, colliders, network/baseline state, nav registration, controller/camera history и relevant caches. Snapshot считается полным только после проверки количества и digest.
5. **APPLY** — offset применяется к explicit participants. Static city, anchors, ships, scene gameplay roots, player frame и camera history обрабатываются отдельными adapters; общий `SetParent` для scene-owned NetworkObject, city root и ship root запрещён.
6. **REBUILD** — выполняются physics synchronization, `Physics.SyncTransforms`, отдельная `ShipDeckNav` re-registration, controller grounding/cache repair, camera history update и network discontinuity/baseline preparation. При отсутствии подтверждённого adapter фаза блокирует commit.
7. **VALIDATE** — проверяются exact new poses, finite coordinates, participant count/digest, scene identity, Rigidbody invariants, nav registration, parent identity, player baseline и camera continuity.
8. **PUBLISH** — только после полного Validate публикуются новый frame generation, network control/discontinuity и accepted local projections. До этой точки remote peers не получают подтверждение успешного rebase.
9. **RELEASE** — снимаются freeze gates на том же generation; обычные motion samples возобновляются только после всех participant acknowledgements.

### Failure and rollback

- Ошибка до `PUBLISH` переводит transaction в `ABORTED` и отзывает pending frame.
- Каждый participant обязан иметь обратимый snapshot/restore token до первой записи.
- При ошибке apply/rebuild выполняется rollback всех уже изменённых participants в обратном порядке; новый frame и ACK не публикуются.
- Если rollback какого-либо participant не доказан как успешный, simulation остаётся frozen в диагностическом fault state; система не продолжает работу с частично сдвинутым миром.
- После `PUBLISH` старый generation не может быть принят как обычный motion sample. Recovery после post-publish fault — отдельный session/transaction protocol, а не повторный blind retry.

## 5. Обязательные invariants

- `GroundPlane_0_0` не восстанавливается.
- `FloatingOriginMP` не включается.
- WorldRoot, scene-owned NetworkObject roots, ship/Rigidbody roots и deck/NavMesh structures не объединяются generic `SetParent`.
- Не допускаются player-only shift, partial binding, path-only scene substitution и unknown participant adoption.
- Static city shift не считается доказательством ship/deck/player/camera readiness.
- Zero velocities из Edit Mode не заменяют runtime Rigidbody capture.
- `catalog=150;markers=150;bound=150` остаётся обязательным startup invariant, но сам по себе не подтверждает rebase transaction.

## 6. Gates перед implementation

До написания runtime coordinator должны быть получены:

1. serialized/read-only participant manifest с устойчивой identity каждого включённого root;
2. runtime instrumentation exact NGO tick, FixedUpdate, physics sync и rebase phase ordering;
3. подтверждённый active camera owner, target и history binding;
4. runtime proof для каждой `ShipDeckNav` re-registration и её relation к ship Rigidbody;
5. explicit policy для scene-owned NetworkObject/NPC/crew roots и moving-platform passengers;
6. pure validator для phase transitions, duplicate/missing participants, rollback coverage и stale generation;
7. пользовательский plan для host+client verification после первой implementation slice.

## 7. Решение этапа

`T-FO06N` фиксирует design-only closed-world participant set и atomic transaction contract. Данных достаточно, чтобы начать отдельную implementation slice, но недостаточно, чтобы объявить runtime rebase готовым: camera ownership, tick/physics ordering, runtime nav registration и часть dynamic gameplay boundaries остаются **INCONCLUSIVE**.

Следующий этап должен реализовывать только coordinator/preflight/snapshot skeleton с fail-closed gates. Первый runtime shift, player-only workaround, включение `FloatingOriginMP` и общий transform-parenting остаются запрещёнными до завершения implementation checks.

## 8. T-FO06N implementation slice — coordinator/preflight/snapshot skeleton

Дата: 2026-09-11.

Создан runtime-inert coordinator skeleton:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseCoordinator.cs`;
- plain C# coordinator без `MonoBehaviour`, `Transform`, `Rigidbody`, `NavMesh`, camera, NGO baseline или scene access;
- closed-world participant set с seal, exact count, stable identity и stale-participant rejection;
- request validation для transaction identity, frame generation, valid plan, participant count и manifest digest;
- fail-closed flow `REQUEST → FREEZE → PREFLIGHT → CAPTURE`;
- immutable participant snapshots с identity check;
- reverse-order restore, freeze release и `Aborted/Faulted` distinction при cleanup failure;
- coordinator намеренно останавливается на `Captured`; `Apply`, `Rebuild`, `Validate`, `Publish` и runtime frame mutation не подключены.

После создания Unity compile первоначально выявил только ошибку definite assignment `CS0165` для локального `restoreError` в rollback path. Ошибка исправлена через явную инициализацию диагностической строки перед вызовом `TryRestore`; повторная проверка Unity сообщает `No compile errors`.

Сцены, префабы, catalog/profile, frame publication и runtime state не изменялись. Play Mode, physics simulation, screenshots и runtime rebase не выполнялись. Camera ownership, NGO tick/physics ordering, concrete participant adapters, manifest digest source, runtime `ShipDeckNav` proof, dynamic participant policy и доказательство Unity-state rollback остаются **INCONCLUSIVE** и блокируют следующий Apply/Rebuild slice.

**Итог:** compile gate для runtime-inert coordinator skeleton — **PASS**; runtime rebase readiness — **NOT READY**.
