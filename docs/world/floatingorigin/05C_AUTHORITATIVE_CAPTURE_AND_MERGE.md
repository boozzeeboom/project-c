# T-FO05C — authoritative player capture, offline merge и publication guard

Дата: 2026-09-09. Baseline: `f2296f36`. **Dormant concrete capture/merge код реализован, к игре не подключён. Compile PASS; 58 новых + 502 прежних = 560 pure PASS / 0 FAIL. Actual NGO source/capture и native disk adapter не вызывались.**

## 1. Подтверждённая граница

B добавил player-only repository, но не source глобальных позиций. C связывает настоящий server-accepted motion с подготовкой immutable player checkpoints и проверкой перед B publication. Это не полный IGlobalMotionPlayerSpawnSource, не restore/teleport и не прохождение T-FO04 runtime gate.

Критичные факты по исходникам:

- `GlobalMotionReplicator.AcceptServerMotion` обновляет **_serverControl.Baseline = accepted** после `GlobalMotionAdmission.TryAccept`. Это последнее принятое server состояние, не только начальный spawn baseline.
- Public `Control` возвращает receiver control; его reliable baseline между keyframes может быть старее. Display/interpolation sample также не canonical persistence source.
- `GlobalMotionAdmission.TryAccept` заменяет SampleTime на server receive time лишь после sender/binding/sequence/time/rate/game-rule validation. Клиентская точка не становится доверенной от самого факта отправки.
- `GlobalMotionWorld` — MonoBehaviour, содержит текущий server issuer/run и frame/actor registry. Frame ID не является durable player identity.
- `NetworkManager.ConnectedClients`, реальные NetworkClient instance и PlayerObject задают текущую connection/player lifetime. Public поля NetworkClient подтверждены live reflection; API-справка: `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.NetworkClient.html`. Приоритет у установленного NGO 2.13.0 и фактического кода.
- `NetworkPlayer.IsInShip` и `CurrentShip` должны быть проверены: World-space в motion alone недостаточно для вывода, что игрок пеший.

Поиск отдельного persistent account/auth provider вновь не дал подтверждённой реализации в исследованном пути. Это **inconclusive**, не утверждение об отсутствии любого auth-кода во всём проекте. NGO owner/session IDs не подменяют доверенный account mapping.

## 2. Изменения

Новые helper файлы в `Assets/_Project/Scripts/World/FloatingOrigin/Persistence/`:

1. `GlobalPlayerCaptureContracts.cs`: ephemeral capture facts, trusted-source contract и pure validation/liveness policy.
2. `GlobalMotionPlayerCheckpointSource.cs`: конкретный NGO reader + явно выдаваемые stable identity leases.
3. `GlobalPlayerCheckpointCapture.cs`: одноразовый prepared batch, offline merge и guarded B commit.

Новый Editor validator: `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalCheckpointCapture.cs`.

Точечные изменения существующего кода:

- World: main-thread identity, readonly `IsOnWorldThread` / `TryGetServerCheckpointScope` без выдачи mutable issuer.
- Replicator: readonly `TryReadServerAcceptedMotion`, не меняющий публикацию, sequence, binding или Transform.
- B repository: optional `publicationGuard` у TryCommit, default=null сохраняет прежние вызовы. Проверки до staging и перед publication выполняются под lease, после callbacks raw files перепроверяются.

Нет новых NetworkBehaviour/NetworkVariable/RPC layouts, wire изменений, default paths или auto-save loop. A/player и B/envelope v1 не менялись; NGO protocol остаётся 0xF005, legacy=0. Scenes/prefabs/profiles, legacy save/restore, настоящий ShipPositions.json и packages не изменялись.

## 3. Настоящий source и доверенная identity

`GlobalMotionPlayerCheckpointSource` создаётся явно на initialized World Unity thread. Bind/capture запрещены без running server scope, текущего manager, installed global startup и session/run. World/connection lifetime не восстанавливаются из файла.

`TryBindVerifiedIdentity(playerId, actualNetworkPlayer, ...)` требует уже spawned реальный PlayerObject данного NetworkClient, opt-in global adapter в registry, native baseline readiness и accepted World motion. PlayerId должен быть явно предоставлен **доверенным caller**; метод не аутентифицирует аккаунт и не преобразует NGO clientId в постоянный ID.

Lease связывает:

- конкретный NetworkClient reference, NetworkObject/NetworkPlayer/adapter;
- server SessionId + World RunGeneration;
- actual network object ID + spawn generation;
- предоставленный PlayerId и уникальный lease ID.

Дубликат live PlayerId запрещён. Live reassignment требует explicit unbind. Reconnect/новый PlayerObject/новая spawn lifetime не переиспользуют старый grant; stale entries могут удаляться при следующей binding операции. Stale unbind не отзывает новую lease. Authority/discontinuity не меняют постоянную identity сами по себе, но меняют допустимость уже подготовленного capture.

Source требует **полного текущего ConnectedClients roster**: missing PlayerObject, unready/unmapped player или unsupported ship state отклоняет весь capture, не пропускается молча. Нет active players — нет fabricated empty capture. Это намеренное ограничение C.

## 4. Откуда берётся сохраняемая точка

`TryReadServerAcceptedMotion` читает _serverControl.Baseline после проверки server/spawn/active/baseline acknowledgement, current session/object identity, custom hierarchy/startup lease, отсутствия конкурирующего writer и правильного authority publisher.

Source также проверяет current frame/actor registry и `IsBaselineReady`. **Эти readiness getters могут читать native pose и вызывать participant callbacks**; но persisted XYZ берутся исключительно из accepted snapshot.WorldPosition. GetEffectivePosition, render pose, Transform.position и frame.ToGlobal не используются как источник checkpoint.

После readiness проверок повторно сверяются scope/connection/actor lifetime. В конце capture — ещё один проход без participant callbacks, с точным сравнением accepted sample/authority/publisher; callbacks позднего игрока не могут незаметно изменить ранее захваченную запись.

Поддержан только **пеший WORLD-space** player. InShip, ненулевой CurrentShip или ParentLocal требуют будущего ship/global-composition bridge; нулевой WorldPosition parent-local пакета не сохраняется как мировой origin.

## 5. Point-in-time evidence без голодания движущихся игроков

Начальный вариант exact latest-sample equality между prepare и commit заменён после static review: обычное движение не должно непрерывно отменять корректный point-in-time checkpoint.

Native source признаёт только **те exact snapshot objects, которые сам выдал** (ConditionalWeakTable). Look-alike DTO с теми же SourceId/полями не является доказательством получения accepted sample. Weak keys не удерживают отработанные batches навсегда.

`CanPublishCaptured` требует:

- тот же source/session/run, полный roster, stable PlayerId, identity lease, owner, authority и publisher;
- тот же **полный** MotionStreamBinding, включая spawn/authority/discontinuity/space/parent fields;
- captured sample ещё свежий относительно текущего server time;
- та же sequence допускается только с точно тем же sample; новая sequence должна быть монотонно новее (с wrap policy), accepted SampleTime не регрессирует.

Новая обычная позиция в той же binding допустима. Сохраняется **первоначальная captured global point**, а не подмена её новым sample при commit. Teleport/reparent/ownership/respawn/reconnect, clock regression, expiry или roster replacement отменяют batch.

Freshness явно задаёт caller: 0 < maximumSampleAge ≤ 60 секунд. Это validation budget, не синхронизация часов. `checkpointAtUnix` также явно предоставляет trusted server caller; при регрессии относительно сохранённой записи C отказывает, а не выдумывает/клэмпит время.

## 6. Offline merge и запись

`GlobalPlayerCheckpointCapture.TryPrepare(storageObservation, checkpointAtUnix, maxAge, ...)`:

1. Принимает только явный Ready/Empty observation B. Pending/Blocked/RecoveryAvailable не превращаются в новый пустой store.
2. Запрашивает и валидирует полный accepted source snapshot.
3. Сохраняет существующие offline records без изменений, обновляет/добавляет только захваченные stable PlayerId. Offline ship affinity также сохраняется, но active aboard-player C не захватывает.
4. Отклоняет timestamp regression для обновляемого игрока, duplicates и превышение лимита.
5. Повторно проверяет source и создаёт immutable single-use batch. Durable records содержат только A поля; ephemeral session/NGO/binding/lease facts в disk payload не попадают.

`TryCommit(batch)` потребляет batch один раз, проверяет source до входа в B и передаёт B original storage observation + frozen merged records + source guard. Нет blind CAS retry на новый head, удаления offline records или implicit recovery.

B запускает read-only guard под lease **перед staging и после staging перед публикацией**, затем перепроверяет raw primary/backup/pending. False/throw до staging не создаёт pending. Отказ после staging сохраняет pending evidence и возвращает NotApplied/Pending; C передаёт результат вызывающему без авто-quarantine или авто-retry. Caller может явно выполнить B.TryQuarantinePending после рассмотрения observation. Reentrant repository calls из guard не получают lease; делать их запрещено контрактом guard и проверено моделью.

Неожиданное exception, вышедшее из repository после входа в него, не превращается C в ложное NotApplied: результат Indeterminate без старого active observation. До входа в storage отказ остаётся NotApplied.

## 7. Фактическая проверка

Compile: **No compile errors**. Read-only source review подтвердил latest-server state, instance-issued evidence, liveness policy, callback final-pass, thread guards, offline merge и storage publication boundary.

`Temp/Aura/ValidateFo05C.cs` выполнен в стабильном Edit Mode. `05C_STATIC_VALIDATION.json`:

| Suite | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalCheckpointCapture | 58 | 0 |
| ValidateGlobalCheckpointTransactions | 68 | 0 |
| ValidateGlobalPlayerPersistence | 69 | 0 |
| ValidateGlobalSceneExecution | 31 | 0 |
| ValidateGlobalSceneCatalog | 56 | 0 |
| ValidateGlobalMotionSpawn | 44 | 0 |
| ValidateGlobalMotionHierarchy | 40 | 0 |
| ValidateGlobalMotionNetworkContracts | 48 | 0 |
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Total** | **560** | **0** |

58 новых проверок: capture identity/scope/world-space/freshness, authority publisher, rejected owner admission, duplicate/default/clock errors, exact-vs-newer motion, sequence wrap, binding discontinuities, immutable facts, actual C coordinator + B repository against memory storage, retention offline records, timestamps, point-in-time movement, staged expiry/teleport, CAS/single-use/guard/re-entrancy/thread behavior. Проверки compiled native seams не вызывают настоящие NGO actors.

Guard: 58 prefab candidates; opt-in/profile assets/loaded assigned profiles/adapters/markers/executors=0. H dirty/missing-component/uninspected scene blockers не исправлялись; новые неизвестные сцены не обследовались.

**UNTESTED:** actual native source/identity leases/readiness callbacks, real network session/capture/owner admission, disk adapter/real saves/crash/reconnect, performance и IL2CPP. Все source/storage в новых behavioral tests — управляемые in-memory fixtures; это не фиктивный runtime PASS. Нет тестовых GameObject, Play Mode, physics, screenshots, builds или реальных backup/save writes.

## 8. Незавершённые зависимости

- Authenticated persistent identity **до spawn** и её согласование с post-spawn capture leases; существующий explicit binding API не заменяет account system.
- Saved-checkpoint lookup/restore-plan и genuine source G с реальными content/frame/AOI readiness, а не legacy Transform teleport.
- Ship/ParentLocal global composition и affinity, NPC/ship/RPC DTO migration, disconnect/final-checkpoint lifecycle.
- Scheduling: C синхронен на source-owning Unity thread; full-roster readiness повторяется перед записью под storage lease. Native disk latency/performance и будущий async dispatch требуют отдельного решения и проверки. Нет подключённого auto-save loop.
- User acceptance native storage B, runtime Host+clients, scene/physics/nav coverage T-FO04/06–08. Пока любой active player в неподдержанном ship/unready состоянии блокирует весь capture.

Global mode/world shift выключены; весь T-FO04/T-FO05 и jitter fix не объявляются завершёнными. Один коммит source/meta/results/report/roadmap/существующего ITERATIONS; Temp runners и LiberationSans fallback исключаются. Historical A/B/I reports и JSON не переписываются.
