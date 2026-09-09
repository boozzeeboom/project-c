# T-FO05D — concrete checkpoint spawn source и остаток интеграции

Дата: 2026-09-09. Baseline: `82645cc0` (C, 560 pure PASS). Появившееся в переписке резюме на B/502 — историческое, не актуальное состояние проекта.

**Итог:** реализован concrete `IGlobalMotionPlayerSpawnSource` для явно подготовленного фиксированного набора frames. Restore/first-spawn plan связан с actual connection identity, immutable checkpoint observation, nonce/frame/deadline и factory G; после готового initial spawn identity передаётся C. **Compile PASS, 60 новых + 560 прежних = 620 pure PASS / 0 FAIL.** Ни один native source/spawn/disk вызов фактически не запускался. Source не настроен и не назначен в игре, global mode/world shift остаются выключенными.

## 1. Что закрывает этот этап

Раньше G требовал prepared spawn-source без concrete реализации. A/B/C уже давали global player record, repository и accepted capture, но не выдавали реальный G player spawn plan по stable PlayerId.

Теперь присутствуют:

- `Persistence/GlobalPlayerSpawnPlanResolver.cs`: pure lookup опубликованного player checkpoint и формирование `GlobalMotionPlayerSpawnPlan`.
- `Persistence/GlobalMotionCheckpointSpawnSource.cs`: настоящий MonoBehaviour source G с runtime configuration, connection identity grants, подготовкой и повторной валидацией plans, явным replica frame и identity handoff в C.
- `Editor/FloatingOrigin/ValidateGlobalCheckpointSpawn.cs`: pure проверки resolver/nonce/lease/storage fence/factory guard dispatch.

Точечные изменения:

- `Network/GlobalMotionSpawnContracts.cs`: server-memory `ReservationId`, отдельный guarded source lifecycle и exception-safe guard dispatch. ReservationId НЕ включён в wire seed, snapshot или save DTO.
- `Network/GlobalMotionPlayerBootstrap.cs`: guard до создания, повторная проверка после Awake/OnEnable до NGO spawn, identity confirmation после initial-ready spawn, cancel/fail-session при ошибках, disconnect notification.
- `Persistence/GlobalPlayerCheckpointRepository.cs`: read-only `IsObservationCurrent` для prepared restore.
- `Persistence/GlobalMotionPlayerCheckpointSource.cs`: callback-safe `TryRetire`, чтобы остановка во время readiness callback немедленно запрещала capture, но не рвала cleanup busy-исключением.

Сцены, prefab layouts, профиль/catalog, существующий save/load, Source G assignment и startup orchestration НЕ настраивались. Это код интеграционного звена, а не выполненный runtime gate.

## 2. Явный порядок использования — не автоматический запуск

1. Сначала внешняя content-система действительно подготавливает scenes/physics и immutable frame definitions.
2. **До network start** вызывается `ConfigureForStart(preparedFrames, explicitReplicaFrameId, serverRepository)`. Manager/World должны существовать на том же объекте, source должен быть enabled на Unity thread. Frames не создаются и не загружаются этим методом.
3. G вызывает `ValidatePreparedContent`, а source повторно использует настоящую проверку `GlobalSceneNativeExecutor.ValidatePreparation`. Нужны reviewed catalog/profile/markers и удовлетворённый I static/nonspatial scope; fake receipt или просто непустой frame list не отменяет эти требования.
4. **После actual connection и World start, до PlayerObject** доверенный серверный caller вызывает `TryAuthorizeVerifiedConnection(clientId, persistentPlayerId, ...)`.
5. После регистрации frames фабрикой G вызывается `TryPreparePlayerPlan(identityLease, frameId, rotation, scale, ownerRules, optionalFirstSpawn, lifetimeSeconds, ...)`.
6. G читает готовый plan через `TryGetPlayerPlan`; его queue уже допускает ожидание до 60 секунд, поэтому identity НЕ требуется выдумывать до server World start или в early Host callback.
7. Factory подтверждает исходные source/client references и lease до/после instantiate; только затем выполняет NGO player spawn. После baseline/native readiness source подтверждает player и связывает того же PlayerId с C capture source.

Ни один из этих методов сейчас не вызван игровым orchestration-кодом. User/auth/content input остаются явными зависимостями. На клиенте repository не нужен; используется только заранее выбранный local replica frame.

## 3. Identity: доверенный вход, не выдуманная аутентификация

Grant хранит exact NetworkClient reference, actual clientId, server session/run, источник конфигурации и уникальный lease ID. Постоянный PlayerId не выводится из NGO ID, имени GameObject, PlayerPrefs или непроверенного payload.

- Смена live PlayerId в том же подключении запрещена.
- Один persistent PlayerId не назначается двум live connections.
- Reconnect с новым NetworkClient не переиспользует прежний grant.
- Pending и confirmed entries очищаются при retirement; stale sweep перед проверкой capacity убирает старые подключения.
- Disconnect notification консервативна: ID-only callback не удаляет entry, если exact NetworkClient всё ещё присутствует. После фактического удаления очистка выполняется callback либо следующим authorize sweep.
- Это initial-spawn-per-connection. Новый respawn в той же connection, resumption и final-save-on-disconnect требуют следующей lifecycle интеграции; не считаются реализованными одним наличием disconnect cleanup.

Повторный поиск отдельного account/auth provider в исследованном player/network пути **inconclusive**. Это не доказательство отсутствия любой account-системы в проекте. `TryAuthorizeVerifiedConnection` доверяет серверному caller и не подтверждает аккаунт самостоятельно. Для реального запуска нужен конкретный доверенный источник и его wiring; тестовые `player_ + clientId` не добавлялись.

## 4. Checkpoint lookup и планы

- Только Ready/Empty опубликованный B observation. Pending/Blocked/RecoveryAvailable не становятся первым spawn и не выбирают backup автоматически.
- Поиск exact ordinal PlayerId. Если запись есть, используется именно её global-double Position.
- Rotation/scale и owner game rules отсутствуют в A record и должны быть предоставлены явно; resolver не угадывает их и не исполняет admission rule ради проверки проекции.
- Если записи нет, нужен **явный optional first-spawn point**. Ноль допустим только как действительно переданный caller point, представимый в выбранном frame; implicit zero fallback отсутствует.
- Saved ship affinity блокирует on-foot restore. Даже переданный first-spawn point не переопределяет неподдержанный ship checkpoint.
- Saved point вне выбранного prepared frame блокирует plan, а не заменяется nearby/default точкой.
- Frame ID приходит от текущего prepared World registry, не из сохранения. Сверяются frame instance, origin, local budget и PhysicsScene.
- Lifetime плана задаётся явно, 0 < ttl ≤ 60 секунд, на realtime monotonic clock; clock regression/expiry отклоняются.
- Plan nonce и точный payload, включая reference owner-rules delegate, должны совпасть. Restaging выдаёт новый nonce и лишает старый plan права на spawn.

`IsObservationCurrent` сверяет repository ownership и fingerprint primary/backup/pending под cooperative lease, без staging/writes. После I/O source снова проверяет живой connection/frame/deadline. Store change требует новой подготовки; blind retry на свежий checkpoint не реализован.

## 5. Factory boundary и ограничения транзакционности

Source с reserved plan обязан реализовать guard. Старые unreserved source contracts не ломаются, но D всегда выдаёт reservation.

G:

1. Проверяет plan до создания объекта.
2. После Awake/OnEnable повторно проверяет original source instance, queue ticket, actual NetworkClient reference, отсутствие другого PlayerObject и source reservation.
3. Вызывает `SpawnAsPlayerObject` по существующему global pipeline.
4. После initial-ready placement проверяет actual PlayerObject и вызывает confirmation/handoff к C.
5. Только после confirmation закрывает queue ticket; ошибки отменяют pending plan, despawn/destroy и останавливают session.

NGO API подтверждён live reflection установленного пакета; справка `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.NetworkObject.html`. Важно: confirmation после native spawn НЕ является атомарным rollback сетевой публикации. Если native callbacks успели изменить мир/диск, failure policy — остановка session, не продолжение частично принятого spawn. Lease файла не удерживается через NGO callbacks.

Confirmed plan нельзя отменой лишить capture identity живого игрока. Поздний cancel игнорируется; при уже начавшемся factory failure native despawn/shutdown делает actor недействительным, дальнейшую очистку выполняет retirement/disconnect.

В source initial accepted position/rotation/scale сравниваются с reserved pose точно. Pure проверка включает near-unit quaternion и подтверждает его неизменённый путь через CreateWorld; настоящий NGO/CC путь всё ещё UNTESTED.

## 6. Исправления по static review

- C retirement больше не вызывает busy Dispose exception: disposed устанавливается немедленно, Scope становится invalid, очистка dictionary откладывается до finally текущего callback. D обнуляет своё состояние до retirement helper.
- Cancel policy явно не затрагивает Completed reservation.
- Добавлены явные null guards repository/capture, консервативный disconnect hook и сохранён stale-before-capacity порядок.
- При retirement из confirmation callback `_configured=false` проверяется последующим ValidateEntry; успешный identity handoff не предполагается после потери scope.

## 7. Фактические результаты

`Temp/Aura/ValidateFo05D.cs` выполнен в stable Edit Mode. Результаты: `05D_STATIC_VALIDATION.json`.

- D resolver/spawn guard/fence: **60 PASS / 0 FAIL**.
- Все 560 предыдущих pure checks: **PASS**.
- Всего **620 PASS / 0 FAIL**, compile **No compile errors**.
- 58 prefab candidates; opt-in/profile assets/loaded global profiles/adapters/markers/executors/checkpointSources: **0**.

60 новых проверок включают restored/explicit-first-spawn, unknown/ship/no-fallback, preserved doubles/explicit pose, invalid frame/rules/clock, nonce/payload/closure identity, Completed cancellation, readonly repository fence/changed lineage/backup/pending/foreign ownership, guarded factory dispatch и compiled seams.

Все behavioral fixtures — чистые данные, memory storage и fake **интерфейсный** source, не поддельные runtime NetworkObjects. Native Mono source, connection registry, NGO spawn/despawn, actual retirement callbacks, disk adapter и реальные save данные не вызывались. Не создавались GameObjects, scenes, test physics; нет Play Mode/screenshots/builds/network sessions. Число тестов НЕ означает процент готовности интеграции.

## 8. Что остаётся — оценка после D

### Проверенная база оценки

- Главный план сохраняет незакрытые native gates **T-FO04–09**, плюс незавершённую semantic/scene классификацию T-FO03.
- По последнему H audit: 26 scene candidates, 25 uninspected, 61 unreviewed observation, 3 missing-component subtree diagnostics. Это исторический baseline аудита, не новый полный осмотр сцен в D.
- Текущий read-only guard: 58 candidate prefabs, opt-in=0, profiles=0. Кандидат не означает, что каждый asset нужно одинаково менять.
- T-FO03 census содержит 639 C#, 48 shader files и 16 RPC signatures-кандидатов. Это объём разбора, НЕ 639/48/16 подтверждённых дефектов и не количество автоматических замен.
- I всё ещё ограничен заранее loaded static content/nonspatial services; spatial scene actors, DDOL, pools, replacement/exclusion и distributed streaming не закрыты.
- D даёт fixed-prepared-frame source. Он не выбирает/грузит новые регионы по аккаунту, не делает AOI, dynamic client rebase или межрегиональный handoff.

### До первого ограниченного игрового прогона: примерно 4–6 этапов

Ориентир — основной игровой build, согласованный небольшой content scope, пеший игрок, Host + client, фиксированный frame около проблемной дальней точки, reconnect/save/load. Это ещё **не** тест реального сдвига origin.

1. Связать доверенный persistent identity и старт session с D (configure → real auth connection → resolve plan → G), разобрать жизненный цикл initial/respawn/reconnect.
2. Подготовить согласованный pilot scope контента: устранить blockers этого scope, reviewed catalog/markers, actual frames/physics placement и корректную замену legacy загрузки без молчаливого исключения неизвестного gameplay.
3. Перевести выбранный player prefab и startup layout/profile; сохранить одинаковые layouts у server/client, убрать конкурирующие writers/неподдержанные participant/collider configurations.
4. Подключить реальное B/C persistence lifecycle с explicit path/store, безопасным baseline/backup решением, initial read, save scheduling и выходом; не перезаписывать unified legacy ShipPositions.json.
5. Свести реальные Native I/G/C/D readiness и startup/retirement порядок; подготовить пользовательский test checklist и выполнить необходимые исправления после первого запуска.

Часть работ объединится, а часть потребует отдельного исправления: поэтому диапазон 4–6, не обещание ровно пяти коммитов. Если pilot scope оказывается несовместим с ограничениями I/G без более широкой миграции, оценка увеличится. Нельзя обходить этот барьер упрощённой сценой и выдавать её за принятие основного билда.

### До первого настоящего rebase-прогона: примерно 8–12 этапов от текущей точки

Включает предыдущий рубеж, а не прибавляется к нему. Дополнительно нужны native rebase transaction с фазой/участниками, согласованная physics/CC обработка, перенос/сброс caches и camera/history, корректная content load/unload граница. Затем пользователь проверяет Idle/walk/jump на ~56.5 км и повторные forced/threshold shifts. Фиксированный near-origin frame сам по себе не подтверждает rebase.

### До полной интеграции: около 10 крупных блоков, ориентир 20–35 сопоставимых итераций

Диапазон **включает** подготовку пилота/rebase выше; это плановая оценка декомпозиции по фактическим открытым границам, не календарная гарантия.

| Блок | Что остаётся | План |
|---|---|---|
| 1. Identity/session/persistence lifecycle | Реальный auth mapping, startup/shutdown, save cadence, disconnect final capture, respawn/reconnect, native disk/backup/migration policy | T-FO04/05 |
| 2. Scene/prefab coverage | Закрытие semantic audit, reviewed catalog/baking, layouts/profile, missing/dirty/uninspected scope, scene-only actors | T-FO03/04/06 |
| 3. Native content lifecycle | Spatial scene objects, placement до spawn, replacement/exclusion, DDOL/pools, dynamic loading/unload и receipts | T-FO04/06 |
| 4. Independent client rebase | Atomic phase/frame commit, root-only transfer, native physics/CC и interpolation/caches/camera | T-FO06 |
| 5. Server regions/Host | Несколько PhysicsScene, region routing всех queries, remote-region visibility, разные далёкие игроки и handoff | T-FO07 |
| 6. Ships/decks/crew | ParentLocal checkpoint/global composition, посадка/выход, passengers/named crew, joints/platform velocity, docking/cruise | T-FO05/07/08 |
| 7. Navigation/AI | World/deck/proxy NavMesh, paths/goals/cache rebasing, combat/social activities, transitions | T-FO08 |
| 8. Остальные positional boundaries | Battle/teleport/pickup/RPC/DTO/ship saves/storm points; сохранение direction/local семантики | T-FO05/08 |
| 9. Visual/global-height/gameplay consumers | Clouds/weather/altitude/shaders/FX/trails/lines/UI/map, equipment/body swap, quest/interaction regressions | T-FO06/08 |
| 10. Acceptance/performance | Host+2 clients near/far/together, packet loss/late join, repeated shift, restart/crash, CPU/GC/traffic, default activation | T-FO09 |

К этому планируются **2–3 пользовательских цикла проверки → исправления → повторной проверки**; число циклов также не гарантировано. Большой объём неизвестного scene/native поведения может вывести за 35 этапов. Срок в днях пока не обоснован: зависит от содержимого закрытых сцен, результатов первого native прогона и времени пользовательских тестов.

**Вывод:** основание координат/сети/persistence существенно подготовлено; bottleneck теперь — actual startup/content/native physics/gameplay integration. До первого узкого запуска уже не нужно переписывать форматы или создавать ещё один пустой provider. Но назвать систему почти полностью интегрированной по 620 pure checks было бы неверно.

Следующий рабочий фокус — интеграционное orchestration identity/session/persistence и подготовка реального pilot scope, сохраняя native gates. Автоматически переходить к сценам/Play Mode/активации из этого отчёта нельзя: следующий этап выполняется по команде пользователя; все игровые тесты и screenshots делает пользователь.

Один commit: source/meta/results/report/roadmap/существующий ITERATIONS. Temp runner, TMP fallback и historical A/B/C reports/JSON исключаются. Без push/rebase и YAML edits.
