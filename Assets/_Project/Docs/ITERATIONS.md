## Итерация от 2026-09-11 (T-FO06N — compile-verified coordinator/preflight/snapshot skeleton)

**Задача:** Реализовать только первый fail-closed coordinator slice после design-only transaction contract, не подключая Unity objects и не выполняя runtime shift.

**Результат:** Создан `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseCoordinator.cs`. Coordinator остаётся plain C# class и реализует request validation, sealed closed-world participant set, freeze gate, participant preflight, immutable snapshot capture, reverse-order rollback и различение `Aborted/Faulted`. Фаза останавливается на `Captured`; `Apply/Rebuild/Validate/Publish` и публикация нового frame отсутствуют.

**Проверки:** Первоначальная Unity compile-проверка обнаружила `CS0165` для `restoreError` в rollback path. Ошибка исправлена явной инициализацией локальной диагностической строки; повторная проверка — `No compile errors`. `git diff --check` — без вывода/ошибок. Play Mode, сцены, префабы, catalog/profile, physics, NavMesh, camera и screenshots не выполнялись.

**Граница:** Изменены только coordinator script, рабочий документ `06N_REBASE_TRANSACTION_CONTRACT.md` и этот журнал. Concrete participant adapters, manifest digest source, camera ownership, NGO tick/physics ordering, runtime ShipDeckNav proof и доказательство Unity-state rollback остаются **INCONCLUSIVE**; runtime rebase readiness — **NOT READY**.

**Следующий шаг:** Отдельно получить доказательства и реализовать concrete adapters/manifest policy только после закрытия camera, physics, NavMesh, network baseline и dynamic participant gates.

---

## Итерация от 2026-09-11 (T-FO06N — design-only participant set и atomic rebase transaction contract)

**Задача:** Продолжить интеграционный план после успешного static pilot startup/player gate и формализовать закрытый participant set будущего rebase transaction.

**Результат:** Создан `docs/world/floatingorigin/06N_REBASE_TRANSACTION_CONTRACT.md`. Зафиксированы `CITY_STATIC`, `WORLD_ANCHORS`, 22 отдельных `SHIP_ROOT`, 20 `SHIP_DECK_NAV`, `PLAYER_FRAME`, camera participant и explicit per-root policy для scene-owned NetworkObject/NPC/crew. Запрещены implicit unknown participants, общий `SetParent`, player-only shift, `FloatingOriginMP` и восстановление `GroundPlane_0_0`.

**Контракт:** Описаны фазы `REQUEST → FREEZE → PREFLIGHT → CAPTURE → APPLY → REBUILD → VALIDATE → PUBLISH → RELEASE`, immutable transaction/frame identity, per-participant rollback snapshots, fail-closed abort и fault state при недоказанном rollback.

**Проверки и ограничения:** Этап design-only; C# runtime, сцены, префабы и settings не изменялись, Play Mode и screenshots не выполнялись. Camera owner/history, NGO tick/physics ordering, runtime ShipDeckNav registration, streamed content и moving-platform passenger state остаются **INCONCLUSIVE** и блокируют прямой runtime shift.

**Следующий шаг:** Отдельная implementation slice coordinator/preflight/snapshot skeleton с fail-closed gates; не включать `FloatingOriginMP` и не выполнять общий transform shift.

---

## Итерация от 2026-09-11 (T-FO06L.2.x follow-up 13 — успешный runtime gate после стабилизации Scene handle)

**Задача:** Закрыть пользовательский Start Host gate после отказа `loaded_scene_not_in_saved_catalog_scope` с `WorldScene_0_0;loaded=False`.

**Результат:** Пользовательский Console Log от `2026-09-11 07:36:57` подтвердил успешную стабилизацию pilot scene set и native preparation: `ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>`. Хост запущен, `PeerConnected` получил `worldRunning=True;scenePrepared=True;sceneReady=True`, создан и размещён `NetworkPlayer_GlobalPilot(Clone)`, а локальный игрок получил `origin=(0,0,0);local=(39992,1,40000)`.

**Подтверждено:** `StartHost`, player spawn, `CompletePlacement ready=True;baseline=True`, `SpawnAsPlayerObject`, скрытие startup menus и повторная native readiness после runtime-инициализации. Обнаружено 20 `NpcShipController`; повторной загрузки `WorldScene_0_0` или отказа по scene handle в предоставленном логе нет. Отсутствие duplicate/legacy takeover подтверждено только по отсутствию наблюдаемого отказа; отдельного handle-count instrumentation нет.

**Проверки:** Unity compile — `No compile errors`; `git diff --check` — `PASS`; пользовательский runtime startup/player gate — **PASS по Console Log**. Floating-origin rebase, grounding, movement, camera, physics, multiplayer client, jitter и screenshots остаются вне этого gate.

**Граница:** Изменены только `GlobalMotionPilotRuntime.cs`, рабочая документация и этот журнал. Catalog/profile/scenes/prefabs, `GroundPlane_0_0`, native fail-closed validation и legacy-owner contract не ослаблялись. Unrelated changes в TMP fallback, `Packages/packages-lock.json` и `ProjectSettings/EditorSettings.asset` не включать.

**Следующий шаг:** Перейти к следующему read-only boundary/rebase этапу; не считать успешный static pilot startup реализацией floating-origin rebase.

---

## Iteration 2026-09-11 (T-FO06L.2.x follow-up 12 - exclude DDOL from authored scene scope)

**Task:** Fix the user-reported Start Host refusal `scene_preparation:loaded_scene_not_in_saved_catalog_scope` after legacy-owner retirement and cataloged DDOL audit.

**Diagnosis:** `GlobalSceneNativeExecutor.BuildPreparation()` treated every `SceneManager.GetSceneAt(...)` result as an authored scene. Persistent Bootstrap roots are already in the special `DontDestroyOnLoad` scene and are audited separately; that scene must not expand the closed authored scope of `BootstrapScene + WorldScene_0_0`.

**Change:** DDOL is skipped by authored scene enumeration in `BuildPreparation()` and `InitialPreparedSceneSetIsIntact()`. DDOL markers remain under the separate ownership/identity/network census. Unknown authored scenes remain fail-closed and the diagnostic now includes `name/path/handle/isLoaded`.

**Checks:** Unity compile: `No compile errors`. User Play Mode, native preparation, `catalog=150;markers=150;bound=150`, Host/player spawn and screenshots remain **UNVERIFIED**.

**Boundary:** Catalog/profile/digest, scene assets, `GroundPlane_0_0`, ownership rules and legacy loader contract were not changed.

**Next:** User-controlled Play Mode from `Assets/_Project/Scenes/BootstrapScene.unity` -> `Start Host`; verify the 150/150/150 binding, player spawn and no duplicate/legacy scene takeover.

---

# Журнал итераций

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 11 — удержание world scene перед native preparation)

**Задача:** Исправить повторный ручной Start Host отказ `Pilot world scene is not loaded` после того, как `WorldScene_0_0` уже начал выполнять `Awake/OnEnable`.

**Диагностика:** Проверка владельцев показала, что `ClientSceneLoader` ретировался, но `WorldSceneManager` оставался активным legacy-координатором и продолжал владеть preload path. Кроме того, pilot после `LoadSceneAsync` повторно разрешал сцену только через `GetSceneByPath`, не сохраняя exact loaded-scene handle до `GlobalMotionPilotSpawnSource` и native boundary. `ServerSceneManager` имеет unload RPC только после NGO startup и первичным blocker не является.

**Результат:** Добавлен явный `WorldSceneManager.TryRetireForGlobalPilot()` с отпиской от legacy events, остановкой корутин и блокировкой Update/preload. Pilot ретирует оба legacy scene owner до additive load. `GlobalMotionPilotRuntime` теперь сохраняет exact loaded `Scene` handle через enumeration `SceneManager.GetSceneAt`, повторно проверяет его присутствие/loaded state и передаёт тот же handle в `GlobalMotionPilotSpawnSource`. Добавлен guard против параллельных pilot host requests.

**Проверки:** Unity Editor сообщает `hasCompilationErrors=false` после изменений. Play Mode, Host/player spawn, screenshots и новый ручной startup gate не выполнялись автоматически; runtime результат остаётся **UNVERIFIED**.

**Граница:** `WorldScene_0_0`, `BootstrapScene`, catalog/profile/digest, `GroundPlane_0_0`, NGO registry и rebase transaction не изменялись. Предупреждения NavMesh/ShipCargoVisual и вторичные NGO shutdown `NullReferenceException` не исправлялись как несвязанные с первичным gate.

**Следующий шаг:** Пользовательский новый Play Mode из canonical `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Ожидается лог retirement legacy scene owners, отсутствие `Pilot world scene is not loaded`, затем проверка `catalog=150;markers=150;bound=150`, `StartHost`, player spawn и отсутствия duplicate/legacy scene takeover.

**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, новый раздел follow-up 11.

---

## Итерация от 2026-09-10 (T-FO06M — exact read-only city/ship/camera boundary census)

**Задача:** Закрыть доступный Edit Mode census по city render/collider bounds, `Respawn_Default`, всем 22 ship/Rigidbody roots, `ShipDeckNav` и camera prefab chain перед design-only rebase transaction.

**Результат:** Для additive-loaded `WorldScene_0_0` подтверждены `10236` descendant GameObjects под 58 roots, `6345` renderers и `1004` colliders под `WorldRoot_0_0`. Renderer AABB: center `(39995.240,2713.469,36717.140)`, size `(78866.400,5734.984,70395.140)`; collider AABB: center `(40894.710,2392.994,36170.610)`, size `(73852.910,2216.482,69762.030)`. `Respawn_Default` находится отдельно в `(39992,0,40000)` и не имеет собственного collider.

**Ship boundary:** Найдены ровно `22` ship-like `Rigidbody + NetworkObject` roots. Все имеют `mass=1000` и `Interpolate`; текущий Edit Mode snapshot показывает `Discrete`. Двадцать roots имеют `ShipDeckNav`, два light/reference roots — нет. `ShipDeckNav` использует baked data, `_registerUnderShip=true`, separation `5000m`, re-registration threshold `2500m` и cooldown `30s`.

**Camera/lifecycle:** Loaded Bootstrap `MainCamera` не содержит `SpringArmCamera`; pilot prefab ссылается на `ThirdPersonCamera.prefab`, где `SpringArmCamera` получает target runtime. Legacy `MainCamera.prefab/FloatingOriginMP` остаётся отдельным asset-level компонентом. Intended execution orders подтверждены кодом, но runtime NGO tick/physics/interpolation order и moving-platform state остаются **UNVERIFIED**.

**Граница:** Read-only Edit Mode census; новая документация `docs/world/floatingorigin/06M_REBASE_BOUNDARY_CENSUS.md` и дополнение `06L_GLOBAL_STATIC_WORLD_PILOT.md`. Сцены, префабы, catalog/profile и runtime code не изменялись; Play Mode, screenshots и origin shift не выполнялись. TMP fallback и `ProjectSettings/EditorSettings.asset` исключаются.

**Следующий шаг:** Design-only participant set и atomic transaction contract. Не включать `FloatingOriginMP`, не делать player-only shift и не использовать общий `SetParent` для city root, scene-owned `NetworkObject` или ship/Rigidbody roots.

---

## Итерация от 2026-09-10 (T-FO06 — ship prefab bounds census)

**Задача:** Получить asset-level размеры ship prefabs для оценки rebase transaction без запуска Play Mode и без предположения о runtime instance layout.

**Результат:** В `Assets/_Project/Prefabs/Ships` найдено 23 prefab assets. Для 20 именованных NPC ships зафиксированы агрегированные prefab AABB sizes: диапазон X `7.20–133.00`, Y около `2.55`, Z `18.00–272.95`. Отдельно отмечены `DONT-DELETE-NPC REFERENCE SHIP`, `Ship_Light_root (копия...)` и `Ship_Light_root_fbx_test` как reference/test assets.

**Ограничение:** Эти размеры не дают pivot offsets, instance world positions, Rigidbody center-of-mass, collider geometry или ShipDeckNav bounds. Полный runtime ship instance census остаётся **INCONCLUSIVE**; scene-owned и dynamic ship roots нельзя классифицировать только по prefab AABB.

**Граница:** Read-only asset query only; сцены, префабы, catalog/profile и runtime code не изменялись. Play Mode автоматически не запускался. TMP fallback и `ProjectSettings/EditorSettings.asset` исключаются.

**Следующий шаг:** Использовать полученный масштабный диапазон как input для instance-level ownership/bounds inspection, затем проектировать rebase transaction только для явно классифицированных groups.

---

## Итерация от 2026-09-10 (T-FO06 — exact coordinate/rebase census refinement)

**Задача:** Уточнить числовые coordinate/frame facts и существующие origin-shift contracts перед проектированием rebase transaction.

**Подтверждено:** `WorldRoot_0_0=(0,0,0)`, `Respawn_Default=(39992,0,40000)`, delta `(39992,0,40000)`, расстояние около `56571` units; `WorldRoot_0_0` имеет 8 прямых дочерних структур, но aggregate city AABB не получен. `Respawn_Default` не имеет render/collider bounds.

**Кодовые boundaries:** `NetworkPlayer_GlobalPilot` использует `GlobalMotionReplicator + GlobalMotionPoseAdapter`; legacy `NetworkPlayer` использует `NetworkTransform`. `GlobalMotionPoseAdapter` регистрируется в `GlobalMotionWorld`, где actor/frame lifetime привязан к NGO run и PhysicsScene. `MainCamera.prefab` содержит `FloatingOriginMP`; его `threshold=150000`, `shiftRounding=10000`, root-name heuristic и `ApplyWorldShift()`/`OnWorldShifted` не являются готовой closed-world rebase transaction.

**INCONCLUSIVE:** полный city AABB/collider bounds, полный список runtime ship roots/deck/navmesh/colliders, camera-follow owner и момент синхронизации rebase с NGO tick/physics/baseline. Поэтому код rebase не изменялся и Play Mode автоматически не запускался.

**Граница:** Read-only scripts/prefab metadata и docs only; сцены, префабы, catalog/profile и runtime code не изменялись. TMP fallback и `ProjectSettings/EditorSettings.asset` исключаются.

**Следующий шаг:** Получить asset-level bounds/ownership census для city meshes/colliders, ship prefabs и camera follow chain; затем отдельно спроектировать transaction.

---

## Итерация от 2026-09-10 (T-FO06 — read-only rebase boundary census)

**Задача:** Зафиксировать фактические границы следующего floating-origin этапа после успешного native startup/player gate, не переходя к общему transform shift.

**Read-only результат:** `WorldRoot_0_0` является hierarchy anchor static city render/collider content; `Respawn_Default` остаётся отдельным root-level marker около `(39992,0,40000)` и не вложен в `WorldRoot_0_0`. Pilot prefab `NetworkPlayer_GlobalPilot` использует `GlobalMotionPoseAdapter` + `GlobalMotionReplicator`, а legacy `NetworkPlayer` использует `NetworkTransform`. `MainCamera` находится в Bootstrap hierarchy; ship roots имеют отдельные Rigidbody/NetworkObject boundaries, а `ShipDeckNav` относится к соответствующей deck structure.

**Архитектурное следствие:** Общий `SetParent` для city root, ship/Rigidbody roots и scene-placed NetworkObject не принимается. В будущей transaction должны быть разделены city render/collider group, anchors, pilot/frame state, camera follow state, каждый ship root с deck/navmesh и scene-owned gameplay roots.

**Неопределённость:** Точные city AABB/collider bounds относительно `Respawn_Default`, полный ship/deck AABB census, camera-follow ownership, authored-vs-dynamic ship lifecycle и sync-момент с NGO/physics/baseline не установлены. Эти данные помечены **INCONCLUSIVE**; код rebase не изменялся, Play Mode автоматически не запускался.

**Проверки и границы:** Read-only исследование завершено без изменения сцен, префабов, catalog/profile или runtime code. Startup gate commit `28a412f5` остаётся предыдущим этапом. TMP fallback и `ProjectSettings/EditorSettings.asset` не относятся к этапу.

**Следующий шаг:** Выполнить отдельный точный read-only AABB/ownership census для city colliders, `Respawn_Default`, camera follow и всех `ShipOrRigidbodyRoot`; только после него проектировать rebase transaction.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 10 — успешный native startup/player gate)

**Задача:** Закрыть ручной startup gate после исправления readiness для catalog-bound `Unmanaged` sources и подтвердить прохождение Host/player path.

**Результат пользователя:** Exported Unity Console Log от `2026-09-10 20:05:41` подтвердил `ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>`, запуск Host на порту `7777`, `PeerConnected(0)`, подготовленный spawn plan, вход в `SpawnPlayer`, `OnActorPostSpawn`, `CompletePlacement ready=True;baseline=True` и вызов `SpawnAsPlayerObject` для `NetworkPlayer_GlobalPilot(Clone)`. После этого зафиксировано `Local pilot player ready; startup menus hidden=2; origin=(0,0,0); local=(39992,1,40000)`.

**Quest handoff:** Ранний callback snapshot пришёл до появления PlayerObject, но bounded retry дождался игрока; затем отправлен snapshot с `1 quest`. Это подтверждает исправленный порядок initial snapshot без возврата ручного spawn.

**Границы:** В логе не обнаружены `replica_factory`, `Player prefab is null`, повторная загрузка `WorldScene_0_0` или соседних сцен. Формального loaded-scene handle counter нет, поэтому duplicate/legacy suppression отмечается как отсутствие наблюдаемого отказа, а не как отдельный instrumentation PASS. Последующий disconnect после EscMenu не классифицируется как startup failure по предоставленной последовательности.

**Незакрыто:** `origin=(0,0,0)` и local player около `(39992,1,40000)` означают, что floating-origin rebase не реализован. Grounding, движение, камера, physics, multiplayer client и jitter не проверялись; screenshots не предоставлены. В логе также остаются независимые warnings по ShipDeckNav/NavMesh, PlayerTarget HP, missing script, NpcSocialBrain Animator parameters, ShipCargoVisual, resource nodes и раннему registry order.

**Изменения этапа:** `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalSceneNativeExecutor.cs`, `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionPlayerBootstrap.cs`, `Assets/_Project/Scripts/Core/NetworkManagerController.cs`, `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, этот журнал. Catalog/profile/scenes/prefabs не изменялись; `GroundPlane_0_0` не восстанавливался. TMP fallback и `ProjectSettings/EditorSettings.asset` исключаются.

**Проверки:** Пользовательский runtime startup/player gate — **PASS по Console Log**; Unity compile — **No compile errors** до gate; `GlobalSceneNativeExecutor` diff-check исправлен до commit. Нормальный gameplay/rebase этап не закрыт.

**Следующий шаг:** Read-only classification actual city render/collider content, `Respawn_Default`, player/camera roots и ship/Rigidbody boundaries перед проектированием согласованной rebase transaction. Play Mode автоматически не запускать.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 9 — server-side player factory и deferred quest snapshot)

**Задача:** Исправить ранний runtime shutdown после follow-up 8, когда `CreatePlayerObject = true` направил host/server в запрещённый client-side custom prefab handler `GlobalMotionPlayerBootstrap.CreateReplica()`.

**Диагностика:** `StartHost()` завершался на `replica_factory:InvalidOperationException` (`No prepared local frame for explicit spawn seed`) и `Player prefab is null`; это происходило до создания PlayerObject и было архитектурным конфликтом между NGO approval auto-spawn и server-side `GlobalMotionPlayerBootstrap.SpawnPlayer()`.

**Изменение:** В `GlobalMotionNetworkStartup.Approve()` восстановлено `response.CreatePlayerObject = false`; ручной spawn в `NetworkPlayerSpawner` не возвращён. В `QuestServer.OnClientConnectedForSnapshot()` initial quest snapshot получил bounded retry — 30 попыток по 0.1 секунды до фактического появления `ConnectedClients[clientId].PlayerObject`. Остальные snapshot paths, каталог, профиль, сцены и prefab assets не изменялись.

**Проверки:** Unity compile — **No compile errors**; `git diff --check` — **PASS**. Play Mode и screenshots не выполнялись; пользовательский Host gate остаётся следующим шагом. Посторонние изменения `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` исключаются.

**Изменённые файлы:** `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNetworkStartup.cs`, `Assets/_Project/Quests/Network/QuestServer.cs`, `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, этот журнал.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 8 — восстановить NGO PlayerPrefab auto-spawn)

**Задача:** Продолжить startup gate после batch ownership correction, когда Host стартовал, но `QuestServer` получил `no NetworkPlayer for client 0`, а local player/menu handoff не был подтверждён.

**Диагностика:** Read-only inspection показал, что `GlobalMotionNetworkStartup.TryApplyProfilePlayerPrefab()` корректно устанавливает единственный spatial prefab `NetworkPlayer_GlobalPilot.prefab`, но `GlobalMotionNetworkStartup.Approve()` выставлял `response.CreatePlayerObject = false`. Это запрещало NGO создать `ConnectedClients[0].PlayerObject` через `NetworkConfig.PlayerPrefab`. `NetworkPlayerSpawner` остаётся диагностическим и ручной `SpawnAsPlayerObject` не является допустимым recovery path.

**Изменение:** В `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNetworkStartup.cs` установлено `response.CreatePlayerObject = true`; approval validation, scene admission, catalog/ownership checks и профильный PlayerPrefab path не изменялись.

**Проверки:** Unity compile — **No compile errors**. Catalog/profile/scenes/prefabs не изменялись. Play Mode и screenshots не выполнялись; следующий runtime gate выполняет пользователь.

**Следующий gate:** Новый Play Mode из canonical `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Проверить NGO auto-spawn, `ConnectedClients[0].PlayerObject`, local pilot readiness, скрытие startup menus и отсутствие disconnect. Если `QuestServer` снова получит `no NetworkPlayer`, диагностировать callback order по свежему логу; ручной spawn не возвращать.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 7 — batch correction Bootstrap services)

**Задача:** Исправить весь оставшийся класс authored Bootstrap network services одним ownership-анализом после отказа `scene_preparation:persistent_bootstrap_service_runtime_location_mismatch:[GatheringServer]`, не смешивая authored scene location с runtime DDOL audit.

**Read-only evidence:** В catalog было `150` entries с distribution `AuthoredSceneContent=38`, `BootstrapService=9`, `PersistentBootstrapService=26`, `SceneOwnedNetworkGameplay=55`, `ShipOrRigidbodyRoot=22`. Десять remaining persistent entries с observed `NetworkObject` были сопоставлены с authored roots `[ShipCargoServer]`, `[CombatServer]`, `[SkillsServer]`, `[DockingServer]`, `[ExchangeServer]`, `[NpcShipServer]`, `[StatsServer]`, `[GatheringServer]`, `[CraftingServer]` и `[EquipmentServer]`. `[QuestServer]` уже имел `BootstrapService` после follow-up 6.

**Решение:** Для этих десяти entries ownership изменён с `PersistentBootstrapService` на `BootstrapService`; `treatment: Unmanaged` сохранён. Scene placement, authored active state, NGO lifecycle, DDOL restoration, `SceneOwnedNetworkGameplay`, `ShipOrRigidbodyRoot`, `GroundPlane_0_0`, fail-closed native admission и legacy loader не изменялись.

**Результат:** Compiled catalog остаётся `entries=150`; distribution после batch correction — `AuthoredSceneContent=38`, `BootstrapService=19`, `PersistentBootstrapService=16`, `SceneOwnedNetworkGameplay=55`, `ShipOrRigidbodyRoot=22`. Digest catalog/profile — `f6c2a4668b1fa3432432b5ab69bf7ae7500fd98293a6106446964958a0584954`.

**Ограничение:** Переданный ранее итог `BootstrapService=20`, `PersistentBootstrapService=15` не выводится из текущего saved catalog без дополнительного source. Оставшиеся persistent entries не имеют observed `NetworkObject`, поэтому их перевод в `BootstrapService` нарушит compiler invariant; одиннадцатый root без нового доказательства не менялся.

**Проверки:** `ValidateGlobalSceneCatalog.Run()` — **58 passed / 0 failed**; `ValidateGlobalSceneExecution.Run()` — **36 passed / 0 failed**; profile digest match — `True`; Unity compile — **No compile errors**. Play Mode и screenshots не выполнялись.

**Следующий gate:** Пользовательский startup gate из `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Зафиксировать `catalog=150;markers=150;bound=150`, `StartHost()`, local player spawn, duplicate `WorldScene_0_0` и legacy loader takeover. Нормальный gameplay playtest не начинать до этого результата.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 6 — исправление ownership QuestServer)

**Задача:** Продолжить startup gate после фактического отказа `persistent_bootstrap_service_runtime_location_mismatch:[QuestServer]`.
**Диагностика:** Свежий пользовательский лог от 2026-09-10 18:55:14 показал `markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`; legacy loader был retired, но native preparation остановилась на `[QuestServer]`. Read-only inspection подтвердил authored root-level `NetworkObject` в canonical `Assets/_Project/Scenes/BootstrapScene.unity`, `m_Father: {fileID: 0}`, `m_IsActive: 1` и baked marker `336a190646b19bc46b22dd4e78f99800:2129841567:0`.
**Решение:** Это Bootstrap network service, а не подтверждённый persistent DDOL root. Entry сохранён как `Unmanaged`, ownership изменён с `PersistentBootstrapService` на `BootstrapService`. Объект не активировался и не перемещался; DDOL restoration и ослабление fail-closed native gate не добавлялись.
**Изменения:** `Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset`, `Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotProfile.asset` (digest обновлён до `75d2e9d4b6e158e0cc523a446201e1bae891521d5636bc99b904ed6e0d2aecf2`), `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, этот журнал.
**Проверки:** Compiled catalog `entries=150`; profile digest match — `True`; `ValidateGlobalSceneCatalog.Run()` — **58 passed / 0 failed**; `ValidateGlobalSceneExecution.Run()` — **36 passed / 0 failed**; Unity compile — `No compile errors`. Play Mode не запускался.
**Следующий gate:** Новый пользовательский startup gate из `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Нормальный gameplay playtest по-прежнему не разрешён: native preparation, `catalog=150;markers=150;bound=150`, `StartHost()`, local player spawn, duplicate `WorldScene_0_0` и legacy loader takeover ещё не подтверждены.
**Граница:** Не активировать и не перемещать `[QuestServer]` только ради прохождения gate; `GroundPlane_0_0` не восстанавливать; исключить `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset`.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 5 — исправление ownership ServerWeatherController)

**Задача:** Продолжить startup gate после фактического отказа `persistent_bootstrap_service_runtime_location_mismatch:ServerWeatherController`.
**Диагностика:** Пользовательский лог, экспортированный 2026-09-10 в 18:47:03, показал `markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`, после чего native preflight отклонил `ServerWeatherController`. Read-only Bootstrap audit подтвердил root-level объект `ServerWeatherController` в canonical `Assets/_Project/Scenes/BootstrapScene.unity`, без parent, active и с baked marker `336a190646b19bc46b22dd4e78f99800:2074923228:0`; на объекте присутствуют `NetworkObject`, `ServerWeatherController` и `GlobalSceneSourceMarker`.
**Решение:** Это authored Bootstrap `NetworkObject`, а не подтверждённый DDOL root. Entry оставлен `Unmanaged`, ownership изменён с `PersistentBootstrapService` на `BootstrapService`. Объект не активировался дополнительно, DDOL restoration и ослабление native gate не добавлялись.
**Изменения:** `GlobalMotionPilotSceneCatalog.asset`, `GlobalMotionPilotProfile.asset` (digest обновлён до `d3f6617d0626a442b2134b3052942fdc19bfe31633ba9c22fd07dc302539a3e6`), этот журнал и `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`.
**Статическая проверка:** Ownership distribution — `AuthoredSceneContent=38`, `BootstrapService=8`, `PersistentBootstrapService=27`, `SceneOwnedNetworkGameplay=55`, `ShipOrRigidbodyRoot=22`; catalog `150`; profile digest match — `True`; `ValidateGlobalSceneCatalog.Run()` — `58 passed / 0 failed`; `ValidateGlobalSceneExecution.Run()` — `36 passed / 0 failed`; Unity compile — `No compile errors`.
**Следующий gate:** Пользовательский новый Play Mode из `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Нормальный gameplay playtest по-прежнему не разрешён: runtime native preparation, `catalog=150;markers=150;bound=150`, `StartHost()`, local player spawn, duplicate `WorldScene_0_0` и legacy loader takeover ещё не подтверждены.
**Граница:** Не активировать `ServerWeatherController` только ради прохождения gate; `GroundPlane_0_0` не восстанавливать; исключить `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset`.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 4 — исправление ownership CloudManager)

**Задача:** Продолжить startup gate после фактического отказа `persistent_bootstrap_service_runtime_location_mismatch:CloudManager`.
**Диагностика:** Пользовательский лог от 2026-09-10 18:43:14 показал `Cataloged DDOL audit: markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`, после чего native preflight отклонил `CloudManager`. Live Edit Mode audit подтвердил: `CloudManager` — inactive root `BootstrapScene`, без parent, с `CloudManager`, `VeilRaymarchMeshController`, `NetworkObject` и baked marker `336a190646b19bc46b22dd4e78f99800:909089444:0`.
**Решение:** В текущем сохранённом runtime состоянии `CloudManager` не достигает `Awake()`, потому что GameObject неактивен; его `DontDestroyOnLoad(gameObject)` не выполняется. Entry оставлен `Unmanaged`, ownership изменён с `PersistentBootstrapService` на `BootstrapService`, чтобы authored root оставался в canonical `BootstrapScene`. Это не добавляет DDOL restoration и не расширяет принятие обычного authored content или scene gameplay.
**Изменения:** `GlobalMotionPilotSceneCatalog.asset`, `GlobalMotionPilotProfile.asset` (digest обновлён до `4b028fe6a302e32510648f3716d86ec857bda85fb1fc77f6c6631f3aa3affce5`), этот журнал и `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`.
**Проверки:** Unity compile — `No compile errors`; `ValidateGlobalSceneCatalog.Run()` — `58 passed / 0 failed`; `ValidateGlobalSceneExecution.Run()` — `36 passed / 0 failed`; Play Mode после изменения не выполнялся.
**Следующий gate:** Новый пользовательский Play Mode из `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Ожидается прохождение CloudManager ownership check и следующий полный native startup результат. Это всё ещё startup gate, не нормальный gameplay playtest.
**Граница:** Не активировать CloudManager только ради прохождения gate; `GroundPlane_0_0` не восстанавливать; исключить `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset`.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 3 — исправление ownership PlayerSpawner)

**Задача:** Продолжить startup gate после фактического отказа `persistent_bootstrap_service_runtime_location_mismatch:PlayerSpawner`.
**Диагностика:** Свежий пользовательский лог от 2026-09-10 18:37:56 показал `Cataloged DDOL audit: markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`, после чего native preflight отклонил `PlayerSpawner`. Live Bootstrap audit подтвердил: `PlayerSpawner` — root `BootstrapScene`, `activeSelf=false`, с `NetworkObject`, `NetworkPlayer`, `NetworkPlayerSpawner` и baked marker `336a190646b19bc46b22dd4e78f99800:100000:1757335980`; ни один компонент не перемещает этот объект в DDOL.
**Решение:** Это не persistent Bootstrap service и не разрешение на DDOL restoration. Entry сохранён как `Unmanaged`, но ownership изменён с `PersistentBootstrapService` на `BootstrapService`, чтобы authored root оставался в canonical `BootstrapScene` и проходил ownership policy как Bootstrap NetworkObject. Обычный authored content, `SceneOwnedNetworkGameplay`, `ShipOrRigidbodyRoot`, unknown roots и mixed-root parenting по-прежнему не принимаются.
**Изменения:** `GlobalMotionPilotSceneCatalog.asset`, `GlobalMotionPilotProfile.asset` (digest обновлён до `dc5616f54e110d5e62063fd3273a4fd6fb3f5b1d6bd5801052fa6c1da3ab1b43`), этот журнал и `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`.
**Проверки:** Unity compile — `No compile errors`; `ValidateGlobalSceneCatalog.Run()` — `58 passed / 0 failed`; `ValidateGlobalSceneExecution.Run()` — `36 passed / 0 failed`; Play Mode после изменения не выполнялся.
**Следующий gate:** Новый пользовательский Play Mode из `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Ожидается отсутствие mismatch для `PlayerSpawner`, затем проверка native preparation, `StartHost()`, local player spawn и отсутствия duplicate/legacy scene takeover. Это всё ещё startup gate, не нормальный gameplay playtest.
**Граница:** Не запускать Play Mode автоматически; `GroundPlane_0_0` не восстанавливать; исключить `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset`.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up 2 — complete known DDOL root classification)

**Задача:** Продолжить startup gate после нового отказа `cataloged_source_in_ddol;restoration_forbidden` на `[ShipHudPanel]` и убрать повторное прохождение одного и того же DDOL класса по одному root за раз.
**Диагностика:** Пользовательский лог от 2026-09-10 18:33:33 показал `sourceId=336a190646b19bc46b22dd4e78f99800:258728291:0`, `[ShipHudPanel]`, `ownership=AuthoredSceneContent`. В том же экспортированном runtime логе предыдущий DDOL audit уже перечислял девять оставшихся persistent Bootstrap roots: `ShipHudPanel`, `ConstellationController`, `PlayerPositionServer`, `KnowledgeToast`, `GatheringToast`, `ShipPositionServer`, `WindManager`, `CraftingProgressController` и `QuestToast`. Их scene components подтверждены вызовами `DontDestroyOnLoad` или фактическим runtime relocation.
**Результат:** Эти девять и только эти девять entries переклассифицированы в `PersistentBootstrapService`. Ownership distribution: `AuthoredSceneContent=38`, `BootstrapService=5`, `PersistentBootstrapService=30`, `SceneOwnedNetworkGameplay=55`, `ShipOrRigidbodyRoot=22`. Catalog remains closed-world: `catalog=150;markers=150;bound=150`.
**Контракт:** Persistent Bootstrap services могут оставаться в `DontDestroyOnLoad`; обычный authored content, `SceneOwnedNetworkGameplay`, `ShipOrRigidbodyRoot`, `GroundPlane_0_0`, restoration обратно в scene и mixed-root parenting не разрешались. Никакие runtime-created unmarked client/UI objects в каталог не добавлялись.
**Проверки:** Unity compile — `No compile errors`; `ValidateGlobalSceneCatalog.Run()` — **58 passed / 0 failed**; `ValidateGlobalSceneExecution.Run()` — **36 passed / 0 failed**; новый digest — `9f5bafcb7217fc2a692195afacceab88dc28252def241d5dfbcfa39e950bbdaa`. Play Mode после этого изменения не выполнялся.
**Следующий gate:** Пользовательский новый `Start Host` из `Assets/_Project/Scenes/BootstrapScene.unity`. Это всё ещё не нормальный gameplay-плейтест: сначала нужен один чистый startup gate без cataloged DDOL rejection, с native preparation, `StartHost`, spawn и отсутствием duplicate/legacy scene takeover. После его успешного подтверждения можно переходить к нормальному Host/player runtime-плейтесту.
**Граница:** В этап входят только девять ownership reclassifications, digest и документация. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` исключаются.

---

## Итерация от 2026-09-10 (T-FO06L.2.x follow-up — runtime-confirmed persistent client/UI roots)

**Задача:** Исправить повторный пользовательский Start Host отказ `cataloged_source_in_ddol;restoration_forbidden` для пяти Bootstrap client/UI roots, которые фактически вызывают `DontDestroyOnLoad` и оставались ошибочно классифицированы как `AuthoredSceneContent`.
**Диагностика:** Edit Mode audit подтвердил пять сериализованных Bootstrap roots с baked markers и `dontDestroyOnLoad=true`: `[QuestClientState]`, `[QuestTracker]`, `[ReputationClientState]`, `[NpcAttitudeClientState]` и `[CustomisationClientState]`. Свежий runtime первым предъявил `NpcAttitudeClientState` (`sourceId=336a190646b19bc46b22dd4e78f99800:133441436:0`). Это был catalog mismatch, а не основание для DDOL restoration.
**Результат:** Эти пять и только эти пять entries переклассифицированы в `PersistentBootstrapService`. Каталог сохранён closed-world: `catalog=150;markers=150;bound=150`. Ownership distribution: `AuthoredSceneContent=47`, `BootstrapService=5`, `PersistentBootstrapService=21`, `SceneOwnedNetworkGameplay=55`, `ShipOrRigidbodyRoot=22`.
**Контракт:** Persistent Bootstrap services могут оставаться в `DontDestroyOnLoad`; обычный authored content, `SceneOwnedNetworkGameplay`, `ShipOrRigidbodyRoot`, `GroundPlane_0_0`, restoration обратно в scene и mixed-root parenting не разрешались.
**Проверки:** Unity compile — `No compile errors`; `ValidateGlobalSceneCatalog.Run()` — **58 passed / 0 failed**; `ValidateGlobalSceneExecution.Run()` — **36 passed / 0 failed**; новый digest — `a4bbb6e8cf21ca711490d7ba82811fec77ad190ddf6035bfcecbfb371727ab95`. Play Mode после этого исправления не выполнялся.
**Следующий gate:** Пользовательский новый `Start Host` из `Assets/_Project/Scenes/BootstrapScene.unity`. Нормальный плейтест ещё не начинать: сначала нужен чистый startup gate — отсутствие cataloged DDOL rejection, native preparation, `StartHost`, spawn и отсутствие duplicate/legacy scene takeover. Если этот gate пройдёт, отдельным этапом передаётся нормальный Host/player runtime плейтест; grounding, camera, physics, rebase и jitter остаются последующими gates.
**Граница:** В этап входят только пять ownership reclassifications, digest и документация. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` исключаются.

---

## Итерация от 2026-09-10 (T-FO06L.2.x — PersistentBootstrapService после fail-closed DDOL gate)

**Задача:** Завершить узкий ownership-fix после runtime отказа `cataloged_source_in_ddol;restoration_forbidden`, не возвращая DDOL restoration и не расширяя ownership scene gameplay.
**Результат:** Введён и применён `PersistentBootstrapService` для ровно 16 подтверждённых Bootstrap DDOL roots: `PlayerSpawner`, `[ShipCargoServer]`, `[CombatServer]`, `NetworkManager`, `[SkillsServer]`, `[DockingServer]`, `[ExchangeServer]`, `Runtime`, `[NpcShipServer]`, `ServerWeatherController`, `[QuestServer]`, `[StatsServer]`, `[GatheringServer]`, `[CraftingServer]`, `[EquipmentServer]` и `CloudManager`. Nested `[MetaRequirementRegistry]`, `[ContractServer]` и `[ShipKeyServer]` оставлены `BootstrapService`.
**Контракт:** Persistent Bootstrap services могут оставаться в `DontDestroyOnLoad`; authored scene content, `SceneOwnedNetworkGameplay`, `ShipOrRigidbodyRoot`, `GroundPlane_0_0`, mixed-root parenting и restoration обратно в scene не изменялись. Digest пересчитан: `42ec3d9e66b1eb99365233aa6ce6949beb163db2e5f9fe1fcf9060e5cf94c046`.
**Проверки:** Unity compile — `No compile errors`; `ValidateGlobalSceneCatalog.Run()` — **58 passed / 0 failed**; `ValidateGlobalSceneExecution.Run()` — **36 passed / 0 failed**; static snapshot — `catalog=150;markers=150;bound=150`; catalog/profile digest match подтверждён. Play Mode после фикса и screenshots не выполнялись.
**Граница:** В этап входят только ownership reclassification, digest, policy/validator fixture и документация runtime fail-closed результата. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` исключаются.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 25.

---

## Итерация от 2026-09-10 (T-FO06L.2 — scene-owned gameplay lifecycle contract)

**Задача:** Реализовать ownership-aware lifecycle contract для authored `NetworkObject` sources в `BootstrapScene` и `WorldScene_0_0`, не принимая `Unmanaged` как замену ownership и не используя DDOL restoration, partial binding или mixed-root parenting.
**Результат:** Добавлен `GlobalSceneOwnership` с категориями `AuthoredSceneContent`, `BootstrapService`, `SceneOwnedNetworkGameplay` и `ShipOrRigidbodyRoot`; `Unspecified` запрещён. Все 150 catalog entries классифицированы как `authored=54`, `bootstrap=19`, `sceneGameplay=55`, `ships=22`. Ownership проверяется компилятором, policy и native executor, включается в digest; профиль согласован с digest `d491f599e5fd9a7437a48e6d9474c53aff2320464f6851e24e08d58f68cd02b4`.
**Lifecycle:** `SceneOwnedNetworkGameplay` остаётся treatment-compatible с `Unmanaged`, но требует явного valid NGO lifetime receipt; ledger учитывает `RequiresNetworkLifecycle`, защищает network identity и stale generations, а executor не разрешает retirement spawned gameplay source. Cataloged DDOL roots отвергаются fail-closed; legacy scene loading передаётся через `ClientSceneLoader.TryRetireForGlobalPilot()`.
**Изменения:** ownership model/compiler digest, scene catalog/profile, source policy, lifecycle ledger, native executor, pilot runtime, execution/catalog validators и pure catalog fixture для ordinary content.
**Проверки:** Unity compile — `No compile errors`; `ValidateGlobalSceneExecution.Run()` — **35 passed / 0 failed**; `ValidateGlobalSceneCatalog.Run()` — **57 passed / 0 failed**; static snapshot — `catalog=150;markers=150;bound=150`; digest match — `d491f599e5fd9a7437a48e6d9474c53aff2320464f6851e24e08d58f68cd02b4`. Play Mode, Host/client, native startup, grounding, physics, camera, rebase и screenshots не выполнялись.
**Граница:** В этап входят только изменения T-FO06L.2; `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` исключаются как посторонние. После отдельного implementation-коммита пользовательский Play Mode gate может быть рассмотрен; static PASS не равен runtime acceptance.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 24.

---

## Итерация от 2026-09-10 (T-FO06L.1 — ownership/NetworkObject boundaries, read-only classification)

**Задача:** Зафиксировать границы ownership для `BootstrapScene` и `WorldScene_0_0` до нового Play Mode, не повторяя DDOL restoration как архитектурное решение.
**Результат:** Edit Mode census подтвердил `19` authored `NetworkObject` в BootstrapScene и `77` в WorldScene_0_0. World objects классифицированы как `45` scene-owned gameplay, `10` docking/network gameplay и `22` ship/Rigidbody roots; все `96` имеют `GlobalSceneSourceMarker`. Для всех в текущем snapshot: `InScenePlaced=false`, `IsSpawned=false`, `NetworkManager=<null>`, `SynchronizeTransform=true`, `AutoObjectParentSync=true`, `ActiveSceneSynchronization=false`, `SceneMigrationSynchronization=false`.
**Решение:** `Road to Quartus`, `Road to Secund`, `Road to Tertius`, пять Primium farm stations и две тестовые станции выделены в `SceneOwnedNetworkGameplay / DockingStation`. Они остаются в `WorldScene_0_0`; `Unmanaged` не считается lifecycle contract. Ship/Rigidbody roots отделены от static content и не участвуют в общем mixed-root parenting.
**DDOL audit:** Зафиксированы runtime relocation families: `NetworkManagerController`, `ClientSceneLoader`, `WorldSceneManager`, persistent client/UI factories, docking/ship services, world visual services, а также pilot restoration methods. `DockingWorld` и другие DDOL services не получают ownership authored station roots. Legacy loader retirement остаётся отдельным handoff seam.
**Проверки:** Unity compile — `No compile errors`; `Validate Native Scene Execution Contracts` — `32 passed / 0 failed`; оба floating-origin scripts validated без ошибок (у `GlobalSceneNativeExecutor.cs` сохранены 2 advisory warnings). Play Mode, screenshots, scene/catalog/digest mutation не выполнялись.
**Граница:** Текущие незакоммиченные `GlobalSceneNativeExecutor.cs` и `GlobalMotionPilotRuntime.cs`, а также посторонние `LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` в коммит не входят.
**Следующий этап:** Реализовать явный lifecycle contract для `SceneOwnedNetworkGameplay`, убрать DDOL round-trip из архитектуры executor и повторить static/native validators. Только после отдельного implementation-коммита разрешается первый нормальный пользовательский Play Mode gate из `Assets/_Project/Scenes/BootstrapScene.unity`.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 23.

---

## Итерация от 2026-09-10 (T-FO06L — reconcile DDOL roots на native-preflight boundary)

**Результат пользователя:** Первый pilot audit восстановил 16 cataloged DDOL roots, второй audit показал 0 DDOL roots, но native sweep снова получил `Road to Quartus` с `candidate=False` и пустым `scene=`.
**Вывод:** Перемещение authored roots в DDOL происходит между внешним pilot audit и `GlobalSceneNativeExecutor.BuildPreparation`.
**Исправление:** `GlobalSceneNativeExecutor` теперь повторно восстанавливает catalog-bound DDOL roots непосредственно перед `BuildPreparation` в `ValidatePreparation` и `PrepareBeforeNetworkStart`, с fail-closed проверкой scene GUID и фактического перемещения.
**Проверки:** `GlobalSceneNativeExecutor.cs` — 0 ошибок, 2 advisory warnings; Compile — `No compile errors`. Play Mode после изменения не выполнялся.
**Следующий gate:** Свежий `Start Host`; ожидать лог `Native preflight restored cataloged DDOL root(s): ...` при повторном DDOL-переносе.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 22.

---

## Итерация от 2026-09-10 (T-FO06L — повторно проверить cataloged DDOL roots перед TryPrepare)

**Результат пользователя:** После восстановления cataloged DDOL roots отказ сохранился с `candidate=False`, `networkManager=NetworkManager`, `isSpawned=False` и пустым `scene=`.
**Причина для следующей проверки:** Не было подтверждения, что DDOL marker был найден restoration-проходом и что root не возвращался в DDOL после content preparation.
**Изменение:** DDOL markers ищутся через `Resources.FindObjectsOfTypeAll`; restoration повторяется после `RefreshPreparedContent()` непосредственно перед `TryPrepare`; после перемещения проверяется фактическая сцена root; добавлен audit `markers/catalogedMarkers/roots/restored`.
**Проверки:** `GlobalMotionPilotRuntime.cs` — 0 warnings / 0 errors; Compile — `No compile errors`. Play Mode после изменения не выполнялся.
**Следующий gate:** Свежий `Start Host` и полный лог `Cataloged DDOL audit` вместе с последующим отказом.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 21.

---

## Итерация от 2026-09-10 (T-FO06L — восстановить cataloged DDOL roots перед native preparation)

**Результат пользователя:** Диагностика `Road to Quartus` показала `catalogBound=True`, `unmanaged=True`, `candidate=False`, совпадающий `NetworkManager`, `isSpawned=False`, `inScenePlaced=True`, `scene=`.
**Причина:** Authored root оказался в `DontDestroyOnLoad`; executor собирал candidates только из loaded scene roots. Простое принятие DDOL запрещено, так как `RequireIdentity` требует исходную cataloged scene.
**Исправление:** `GlobalMotionPilotRuntime` теперь восстанавливает все catalog-bound DDOL roots в соответствующие загруженные сцены по `sourceId -> sceneGuid`. Mixed-scene root и отсутствующая целевая сцена отклоняются fail-closed.
**Проверки:** `GlobalMotionPilotRuntime.cs` — 0 warnings / 0 errors; Compile — `No compile errors`. Play Mode после изменения не выполнялся.
**Следующий gate:** Свежий ручной `Start Host` из `Assets/_Project/Scenes/BootstrapScene.unity`.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 20.

---

## Итерация от 2026-09-10 (T-FO06L — диагностировать uncontrolled NetworkObject Road to Quartus)

**Результат пользователя:** После исправления Bootstrap path ручной `Start Host` проходит до native preparation, затем останавливается на `uncontrolled_network_source_before_native_sweep:Road to Quartus`.
**Статическая классификация:** `Road to Quartus` имеет catalog marker и `NetworkObject` на одном GameObject; source ID присутствует в catalog, `InScenePlaced=0`.
**Изменение:** В `GlobalSceneNativeExecutor` добавлена подробная fail-closed диагностика фактических `catalogBound`, `unmanaged`, `candidate`, `NetworkManager`, `IsSpawned`, active-state, `InScenePlaced`, sync/lifetime flags и scene path. Разрешение `Unmanaged` не расширялось.
**Проверки:** `GlobalSceneNativeExecutor.cs` — 0 ошибок, 2 advisory warnings; Compile — `No compile errors`. Play Mode после изменения не выполнялся.
**Следующий gate:** Свежий Play Mode из `Assets/_Project/Scenes/BootstrapScene.unity`; прислать полный отказной лог с полями состояния `Road to Quartus`.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 19.

---

## Итерация от 2026-09-10 (T-FO06L — диагностировать Bootstrap scene path mismatch)

**Результат пользователя:** После ручного нажатия `Start Host` pilot остановился на `Pilot could not restore cataloged Bootstrap roots from DontDestroyOnLoad`.
**Причина:** Editor работал со сценой `Assets/BootstrapScene.unity`, тогда как catalog и Build Settings используют `Assets/_Project/Scenes/BootstrapScene.unity` с GUID `336a190646b19bc46b22dd4e78f99800`. Активный файл оказался отдельным некаталогированным дубликатом; restoration method жёстко искал только catalog path и возвращал общий `false` до проверки roots.
**Исправление:** `GlobalMotionPilotRuntime` сохраняет разрешение через cataloged Bootstrap path `Assets/_Project/Scenes/BootstrapScene.unity` (NetworkManager может уже находиться в `DontDestroyOnLoad`) и добавляет диагностику `expectedPath`, `activePath` и `managerScene`. Некаталогированный дубликат остаётся fail-closed. UI handoff также использует cataloged Bootstrap scene.
**Проверки:** `Assets/_Project/Scenes/BootstrapScene.unity` сохранена с `_autoStartHost: 0`; scene diff — ровно 1 строка. `GlobalMotionPilotRuntime.cs` — standard validation: `0 warnings / 0 errors`; Compile — `No compile errors`. Play Mode после исправления не выполнялся.
**Следующий gate:** Открыть/использовать `Assets/_Project/Scenes/BootstrapScene.unity`, нажать `Start Host` и прислать следующий свежий результат.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 18; исправлена запись предыдущего autostart gate.

---

## Итерация от 2026-09-10 (T-FO06L — отключить непреднамеренный pilot autostart)

**Наблюдение:** Ошибка `uncontrolled_network_source_before_native_sweep:Road to Quartus` возникала ещё до нажатия `Start Host`.
**Причина:** В `BootstrapScene` у `GlobalMotionPilotRuntime` было `_autoStartHost: 1`; `Start()` автоматически вызывал `StartPilotHost()` сразу после входа в Play Mode. Кнопки Host при этом шли в обычный `NetworkManagerController.StartHost()`, а не в pilot path.
**Изменение:** В активном на тот момент дубликате `Assets/BootstrapScene.unity` значение `_autoStartHost` было установлено в `0`. `MainMenuWindow` и `NetworkTestMenu` сначала вызывают активный `GlobalMotionPilotRuntime.StartPilotHost()`, а при отсутствии pilot сохраняют обычный NMC Host. Pilot UI не скрывается до подтверждённой готовности local player.
**Проверки:** `NetworkTestMenu.cs` и `MainMenuWindow.cs` — standard validation: `0 warnings / 0 errors`; Compile — `No compile errors`. Позже установлено, что это был некataloged duplicate scene, поэтому изменение не является исправлением cataloged Bootstrap asset. Play Mode после изменения ещё не запускался.
**Следующий gate:** Использовать `Assets/_Project/Scenes/BootstrapScene.unity`; проверить отсутствие native preparation до клика, нажать `Start Host` и прислать следующую фактическую точку startup/error.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 17; обновлён этот журнал.

---

## Итерация от 2026-09-10 (T-FO06L — разрешить cataloged Unmanaged source до NGO startup)

**Runtime отказ:** После отката frame bridge native preparation остановилась на `uncontrolled_network_source_before_native_sweep:Road to Quartus`.
**Read-only классификация:** `Road to Quartus` — scene-authored `DockStation` с `NetworkObject`, `OuterCommZone`, trigger collider и `GlobalSceneSourceMarker`; source ID `837a3910185f2b9478ce71df025ccc7c:1987571259:0` уже есть в `GlobalMotionPilotSceneCatalog.asset` с `treatment: 5` (`Unmanaged`), `spatial=false`, `frameId=0`. В scene YAML `InScenePlaced=0`, поэтому до NGO startup `NetworkManager=null`.
**Причина:** pre-start sweep требовал `no.NetworkManager == manager` даже для reviewed `Unmanaged` source, хотя этот treatment означает, что executor не регистрирует, не размещает, не активирует и не спавнит source.
**Исправление:** Разрешён только catalog-bound `Unmanaged` NetworkObject с `NetworkManager=null` и `IsSpawned=false`. Unknown/foreign-manager/spawned objects по-прежнему блокируют native preparation; active-state validation managed sources сохранена.
**Проверки:** Compile — `No compile errors`; catalog/digest/150 binding и GroundPlane_0_0 не изменялись. Новый Play Mode после исправления ещё не запускался.
**Следующий gate:** Пользовательский новый BootstrapScene → Start Host. Ожидается прохождение `Road to Quartus` и следующая фактическая startup-причина, если она существует. Partial binding и автоматическое принятие неизвестных NetworkObject запрещены.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 16; обновлён этот журнал.

---

## Итерация от 2026-09-10 (T-FO06L — откат небезопасного initial frame bridge)

**Результат пользователя:** После initial XZ frame bridge player появлялся, но карта WorldScene_0_0 не была видна/доступна, player падал; ships создавались. Поэтому jitter нельзя было оценить. Это отрицательный runtime gate, не частичный успех anti-jitter.
**Фактическая причина отказа:** `GlobalMotionPilotFrameBridge.TryPrepare()` до StartHost делал `Transform.SetParent` для mixed scene-placed NetworkObject roots. NGO выдал `networkManager is not listening ... before re-parenting` для Ship_Light_root, pickup, chest, resource, CraftingStation и DockStation roots; user stack trace указывает именно на bridge line 129. Aggregate parent не подходит как pre-NGO transaction для WorldScene_0_0. Отдельная причина отсутствия city map не измерена и не угадывается.
**Откат:** Через `git revert --no-commit 97ee4338` удалены bridge script/meta, Bootstrap component, runtime invocation и configured-frame path в spawn source. Возвращён fixed frame origin=(0,0,0); root parenting/Physics.SyncTransforms больше не вызываются. Сохранён предыдущий Host/player baseline и `6db7e78d` menu handoff. Catalog/digest/150 marker binding и GroundPlane_0_0 не изменялись.
**Проверки:** Runtime-результат подтверждён пользователем. После кода отката автоматический Play Mode/screenshots не запускались; Compile — `No compile errors`. Предшествующие CraftingStation/recipe/TMP изменения исключены.
**Следующий шаг:** Read-only T-FO06 census: отделить city render/collider content от NetworkObject/Rigidbody roots, определить actual city/collider coordinates относительно Respawn_Default, затем спроектировать fail-closed atomic placement/rebase transaction. Не повторять общий SetParent или player-only origin shift.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 15; обновлён этот журнал. Один commit включает кодовый откат и эту отрицательную запись.

---

## Итерация от 2026-09-10 (T-FO06L — подтверждён startup/spawn; закрытие стартового меню)

**Результат пользователя:** После `9f45d953` ошибок нет, игра запустилась и персонаж появился. Меню StartHost оставалось открытым, jitter персонажа сохранился. Это подтверждение startup/spawn, не приёмка полного floating origin. Скриншоты не предоставлены.
**Диагностика меню:** В Bootstrap включён `_autoStartHost: 1`; pilot запускает NGO напрямую в обход UI button handlers с `Hide()`. Активны `MainMenu` (`MainMenuWindow`, baked marker подтверждён) и `TestObjects/NetworkTestCanvas` (`NetworkTestMenu`). `MainMenuWindow.Start()` вызывает `Show()` независимо от состояния pilot.
**Изменение:** В `GlobalMotionPilotRuntime.cs` после успешного StartHost добавлен one-shot UI handoff: минимум один кадр ожидания, native admission, настоящий local owner PlayerObject, готовые global coordinates и baseline; затем штатный Hide только стартовых MainMenuWindow/NetworkTestMenu в Bootstrap. Failure/disposed startup меню не скрывает; timeout 60s. Диагностика содержит число обработанных меню, origin и local position; при нуле совпадений — warning. Cursor/gameplay/Esc/settings/локализация не меняются.
**Jitter:** `GlobalMotionPilotSpawnSource` всё ещё использует origin=(0,0,0) и limit=100000; сохранённый Respawn_Default=(39992,0,40000), offset=+1 Y. Поэтому начальный Unity local остаётся около 56.6 км от нуля. Согласованного rebase мира/игрока/камеры здесь нет; менять только player origin нельзя. Антиджиттер-результат — FAIL по наблюдению пользователя, реальный rebase gate ещё не выполнен.
**Проверки:** Compile — `No compile errors`; scoped `git diff --check` — PASS; read-only review readiness/hide. Play Mode и screenshots автоматически не запускались; исчезновение меню после правки — UNVERIFIED.
**Граница:** Один runtime script + `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md` (раздел 14) + этот журнал. Scene/prefab/catalog/digest, спавн, координаты, физика и анимация не изменялись. Предшествующие незакоммиченные CraftingStation/recipes/TMP не входят в этап.
**Следующий шаг:** Пользователь проверяет исчезновение меню в новом Play Mode-сеансе. Следующий отдельный T-FO06 этап — согласованное размещение reviewed world/player/camera в ненулевом local frame и дальнейший rebase, а не очередная попытка скрыть jitter сглаживанием.

---

## Итерация от 2026-09-10 (T-FO06L — остановить legacy callbacks и повторную загрузку сцен)

**Задача:** Исправить повторный отказ `Startup lease or initial scene set changed` после Start Host, не разрешая посторонние сцены или partial catalog binding.
**Диагностика:** В пользовательском Editor.log после Host connect повторно загружается `WorldScene_0_0`, затем `WorldScene_0_1`, `WorldScene_1_0`, `WorldScene_1_1`. Отключённый через `enabled=false` ClientSceneLoader сохранял подписку на `NetworkManagerController.OnPlayerConnected` и корутины; callback запускал legacy `LoadSceneWithNeighbors`. Предыдущая гипотеза о DontDestroyOnLoad не подтвердилась. Конкретный lease/singleton snapshot в момент отказа недоступен после выхода из Play Mode.
**Результат:** В `ClientSceneLoader.cs` добавлен явный `TryRetireForGlobalPilot`: отписка, остановка корутин, runtime guards на повторные load/connect входы, учёт незавершённых native AsyncOperation и отказ при in-flight operations; menu reset защищён до полного shutdown. `GlobalMotionPilotRuntime.cs` вызывает handoff до additive load и повторно после загрузки. `GlobalSceneNativeExecutor.cs` проверяет реальные Scene handles и сообщает отдельно lease/manager/scene причину с expected/actual name/path/handle/isLoaded. Retirement сохраняется до уничтожения компонента; обычный legacy startup его не вызывает.
**Проверки:** Compile — `No compile errors`; существующий чистый `ValidateGlobalSceneExecution.Run()` — `32 passed / 0 failed` (не runtime-тест handoff). Play Mode, screenshots и Host/player spawn после исправления не запускались; приёмочный gate — UNVERIFIED. Общий `git diff --check` выявил whitespace только в постороннем TMP; этот файл не исправляется и не включается в этап.
**Граница:** Scene/prefab/catalog/profile/digest не изменены, 150-source binding сохранён, `GroundPlane_0_0` не восстановлен. Предыдущие незакоммиченные crafting/recipe, TMP и Packages изменения остаются вне этапа. Не создавать отдельный коммит только для записи хеша.
**Следующий шаг:** Пользовательский новый Play Mode-сеанс из BootstrapScene → Start Host. Если отказ останется, нужен полный новый diagnostic с expected/actual. Успех Host/player spawn пока не объявляется.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, раздел 13; обновлён этот журнал.

---

## Итерация от 2026-09-10 (T-FO06L — вывести legacy scene loader из pilot ownership)

**Задача:** Устранить startup-блокер `legacy_scene_loader_must_be_retired_by_content_bridge`, не обходя проверку ownership и не удаляя legacy-систему из обычного режима.
**Результат:** `GlobalMotionPilotRuntime` после восстановления cataloged Bootstrap roots вызывает `RetireLegacySceneLoadersForPilot()`. Метод отключает только активные компоненты `ProjectC.World.Scene.ClientSceneLoader`, оставляя `Runtime`, `NetworkManager`, catalog, digest и legacy-код без изменений.
**Проверки:** Compile — `No compile errors`. Ручной Play Mode, Host/client, native preparation, player spawn, physics и screenshots после исправления ещё не выполнялись.
**Граница:** Независимый concave `MeshCollider` на `Ship_Light_root/.../CABIN_ENTRY_DOOR_PIVOT` не изменялся. Полный T-FO04–T-FO09 не является prerequisite текущего T-FO06L pilot.
**Следующий шаг:** Пользовательский Play Mode-запуск; проверить отсутствие `legacy_scene_loader_must_be_retired_by_content_bridge` и зафиксировать следующий фактический runtime gate.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, обновлён этот журнал.

---

## Итерация от 2026-09-10 (T-FO06L — восстановить Bootstrap identity перед native preparation)

**Задача:** Исправить повторный Play Mode отказ `catalog_markers_bound_mismatch:catalog=150;markers=134;bound=134`, не ослабляя closed-world binding и не восстанавливая `GroundPlane_0_0`.
**Диагностика:** На диске `BootstrapScene` и `WorldScene_0_0` содержат все 150 сериализованных markers (`61 + 89`). Во время Play Mode 16 Bootstrap markers уже были перемещены в `DontDestroyOnLoad` persistent roots, поэтому executor, который принимает только authored catalog scenes, видел 134 markers.
**Результат:** `GlobalMotionPilotRuntime` перед native preparation находит cataloged Bootstrap markers в `DontDestroyOnLoad` и переносит их корневые GameObjects обратно в загруженную `BootstrapScene`. Catalog, digest, `GlobalSceneNativeExecutor` и `Unmanaged` contract не ослаблены.
**Проверки:** Compile — `No compile errors`. Play Mode после этого исправления, Host/client, player spawn, physics и screenshots не запускались; пользовательский runtime gate остаётся `UNVERIFIED`.
**Граница:** `GroundPlane_0_0` не восстанавливался. Независимый `LiberationSans SDF - Fallback.asset` в этап не входит.
**Следующий шаг:** Пользовательский Play Mode-прогон с ожиданием `catalog=150;markers=150;bound=150` или следующей фактической причины отказа. Не принимать partial binding.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, обновлён этот журнал.

---

## Итерация от 2026-09-10 (T-FO06L — подготовить world scene перед pilot startup)

**Задача:** Разобрать первый пользовательский Play Mode отказ после исправления catalog binding.
**Диагностика:** Лог показал `pilot_frame_not_prepared`. При старте была загружена только `BootstrapScene`; `WorldScene_0_0` не была загружена, поэтому `GlobalMotionPilotSpawnSource.Awake()` не смог найти `Respawn_Default` и не подготовил frame.
**Результат:** `GlobalMotionPilotRuntime` теперь перед `GlobalMotionNetworkStartup.TryPrepare` additive загружает `Assets/_Project/Scenes/World/WorldScene_0_0.unity`, дожидается завершения загрузки, повторно подготавливает `GlobalMotionPilotSpawnSource` и только затем запускает Host. Если world scene уже загружена, повторная загрузка не выполняется.
**Проверки:** Compile — `No compile errors`. Новый Play Mode/native startup gate ещё не запускался пользователем после исправления.
**Граница:** `GroundPlane_0_0` остаётся удалённым; catalog/digest и scene YAML не изменялись. Независимый `LiberationSans SDF - Fallback.asset` в этап не входит.
**Следующий шаг:** Повторить ручной Play Mode Host/client запуск. Ожидаемая первая проверка — additive loaded scenes (`BootstrapScene` + `WorldScene_0_0`), затем отсутствие `pilot_frame_not_prepared` и переход к следующему фактическому startup error, если он есть.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, обновлён этот журнал.

---

## Итерация от 2026-09-10 (T-FO06L — исправить binding executor)

**Задача:** Продолжить T-FO06L после отказа native preparation на `catalog_source_not_bound`, не восстанавливая удалённый `GroundPlane_0_0`.
**Результат:** `GlobalSceneNativeExecutor.BuildPreparation()` теперь перечисляет все `GlobalSceneSourceMarker` внутри reviewed roots, включая marked descendants без `NetworkObject`. Добавлена обязательная проверка согласованности `catalog / markers / bound`; диагностика `catalog_source_not_bound` теперь содержит счётчики. Внешние runtime roots без marker, duplicate, wrong-scene и parent identity guards сохранены.
**Проверки:** Compile — `No compile errors`; `Validate Native Scene Execution Contracts` — `32 pure checks passed`. Runtime native preparation, Play Mode, Host/client, grounding и screenshots не запускались.
**Граница:** `GroundPlane_0_0` подтверждённо удалён и не восстанавливается. Текущий catalog/digest не изменялся. Независимый `LiberationSans SDF - Fallback.asset` в этап не входит.
**Следующий шаг:** Передать текущую сборку пользователю для ручного Host/client Play Mode-прогона. При повторном отказе использовать фактические значения `catalog=...;markers=...;bound=...`; не разрешать silent skip.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, обновлён этот журнал.

---

## Итерация от 2026-09-09 (T-FO06L)

**Задача:** Подготовить и зафиксировать первый global static-world pilot: `NetworkPlayer_GlobalPilot`, reviewed catalog для `BootstrapScene` + `WorldScene_0_0`, explicit `Unmanaged` treatment и native scene admission до NGO spawn.
**Результат:** Добавлен `GlobalSceneTreatment.Unmanaged` с обязательной catalog/review binding и сохранением authored state без placement/activation/spawn/retirement. Обновлены catalog compiler, policy, executor seams и protocol `0xF005 → 0xF006`. Созданы/подключены `GlobalMotionPilotSceneCatalog`, `GlobalMotionPilotProfile`, `GlobalPilotNetworkPrefabs`, `GlobalMotionPilotSpawnSource` и `GlobalMotionPilotRuntime`; global startup временно заменяет `NetworkConfig.PlayerPrefab` только в памяти и восстанавливает legacy prefab при release/failure. Каталог содержит 150 observations (`BootstrapScene=61`, `WorldScene_0_0=89`) с digest `bfe8008aa885a818b05f799799c504a1b174f87fdc0d73df2043a3ab91d93199`.
**Проверки:** Compile ранее подтверждён как `No compile errors`; pure execution validator — `32 passed / 0 failed`. Edit Mode snapshot содержит 150 уникальных live markers. Runtime native preparation остановилась до player spawn с `scene_preparation:catalog_source_not_bound:336a190646b19bc46b22dd4e78f99800:1044316355:0`; поэтому персонаж не появляется и Host/client acceptance не пройдены.
**Граница:** Play Mode, screenshots и игровые acceptance-прогоны выполняет пользователь; автоматически не запускаются. `GroundPlane_0_0` намеренно не восстанавливается. Независимый `LiberationSans SDF - Fallback.asset` в этап не входит.
**Следующий шаг:** Исправить executor candidate binding так, чтобы все 150 catalog entries связывались со всеми marked descendants reviewed roots; добавить диагностику `catalog/markers/bound`, затем передать сборку пользователю для ручного Host/client прогона. Только после пользовательского PASS проверять grounding, движение, камеру и release/restore.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, обновлён этот журнал.

---

## Итерация от 2026-09-09 (T-FO06K)

**Задача:** Проверить результат ручной уборки и составить каталог пилота.
**Ручная правка сцены:** Пользователь убрал мёртвые ссылки вручную. Diff шире трёх компонентов: 165 добавлений, 1996 удалений, удалено 15 объектов — `Boundaries_0_0` со всем содержимым, `GroundPlane_0_0` (локально `39999.5, 0, 39999.5`) и весь объект `[KeyRod_ShipHeavy]`. `WorldRoot_0_0`, `[Ship_Key_Container]`, `Pad_10`, `PAD-007_npc` сохранились. Земля и границы были соседями под одним родителем, поэтому удаление контейнера границ не могло удалить землю попутно — это отдельные удаления. Удаление земли под исторической точкой спавна отмечено как риск: пилотному игроку нужна опора, а восстановление после составления каталога сделает digest устаревшим.
**Блокеры каталога снялись:** missing-компонентов 0 в обеих сценах, ошибок осмотра 0, observations 150, флаги dirty сброшены сохранением идентичного содержимого без изменения байтов. Отдельно установлено: флаг dirty поднимает перезагрузка домена при компиляции, поэтому авторинг каталога обязан сбрасывать его внутри собственного запуска, иначе аудит выведет `saved = false`.
**Главный результат — каталог не покрывает эти сцены:** из 150 наблюдений поддержано 58, НЕ поддержано 92. По сценам: `BootstrapScene` 54 против 7, `WorldScene_0_0` 4 против 85. Политика допускает только три комбинации — статичный контент как spatial, статичный контент как неспатиальный и неспатиальный сетевой сервис. `Exclude` и `ReplaceWithNetworkPrefab` отвергаются (`replacement_and_exclusion_need_extended_executor`), spatial+NetworkObject отвергается (`spatial_scene_network_actor_bridge_missing`), поддерево spatial-записи ограничено Transform/MeshFilter/Renderer/Collider/маркером, а неспатиальной — запрещает Renderer/Collider/Camera/Light/Animator/ParticleSystem.
**Семь проблемных наблюдений Bootstrap:** `Clouds` (VFX), `PlayerSpawner` (NetworkPlayer/CC/Animator), `MainCamera` (Camera/AudioListener), `Sun` (Light), `Moon` (Light/MeshRenderer/MoonController), `TestObjects` (Canvas/UI/коллайдеры), `ConstellationController` (LineRenderer). В `WorldScene_0_0` поддержаны только `Crossbow`, `Throw_grenade`, `Crossbow_weapon` и `Respawn_Default`.
**Развилка контракта:** вариант A — ввести проверенную категорию «не управляется» с обязательным `reviewNote`, без маркера, позы и контроля активации; затрагивает treatment, компилятор, политику, исполнителя и версию протокола, но снимает блокировку пилота и сохраняет принцип «ничего не исключается молча». Вариант B — расширить исполнителя до spatial-сетевых актёров, что соответствует объёму T-FO07/T-FO08. Рекомендован A; ни один не реализован, так как изменение затрагивает версию протокола и согласование всех участников.
**Файлы:** `WorldScene_0_0.unity` (правка пользователя), `06K_CATALOG_FEASIBILITY_LIMIT.md`, `06K_OBSERVATION_FEASIBILITY.json`, `tools/ClassifyPilotObservations.cs`, roadmap и этот журнал.
**Проверки:** классификация выполнена повторением фактических правил политики и проверки поддерева исполнителя, а не по предположению; обе сцены загружены и совпадают с файлами на диске. No compile errors. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** подтвердить вариант расширения контракта; решить судьбу удалённой земли; каталог на 150 записей; профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один scene/feasibility/docs commit; TMP fallback и Temp-скрипты исключены.

---

## Итерация от 2026-09-09 (T-FO06J)

**Задача:** Загрузить обе сцены пилота, осмотреть живьём и подготовить их к каталогу.
**Исправление оценки 06I:** живой осмотр опроверг разбор YAML. Корней в `WorldScene_0_0` — 58, а не 26; в Bootstrap — 56, а не 55; мёртвых компонентов в WorldScene_0_0 — 3, а не 1; observations — **150**, а не 95. Причины: корни prefab-инстансов в YAML сцены представлены блоками `PrefabInstance` и текстовым разбором не видны, а у двух мёртвых компонентов `m_Script` не содержит GUID, из-за чего парсер их пропускал. Авторитетная цифра — 150 записей от собственного аудита проекта на двух загруженных сценах.
**Опознание:** `WorldRoot_0_0/Boundaries_0_0/South` и `.../SouthPoleBlocker` — `Assembly-CSharp-Editor::ProjectC.Editor.PoleBlockerComponent`, класс не найден нигде в проекте и объявлен в Editor-сборке, то есть в рантайм-сцене неработоспособен по определению. `[Ship_Key_Container]/[KeyRod_ShipHeavy]` — `ProjectC.Ship.Key.KeyRodInstanceBinding`. Все три — обычные объекты сцены, не prefab-инстансы.
**Блокер:** удалить эти слоты публичными API Unity не удалось. `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` не удаляет ничего, так как ссылки на скрипт нет вовсе; `SerializedObject.m_Component` не поддаётся ни `DeleteArrayElementAtIndex` (первое удаление лишь обнуляет элемент), ни изменению `arraySize`. Все попытки прерваны собственными проверками ДО сохранения; обе сцены побайтово совпадают с зафиксированными файлами.
**Почему это блокирует каталог:** аудит вычисляет `inspectedComplete` как отсутствие ошибок при осмотре сцены, а отсутствующий компонент в поддереве наблюдаемого корня даёт ошибку. Оба проблемных объекта лежат в поддеревьях `WorldRoot_0_0` и `[Ship_Key_Container]`. Компилятор отвергает источник с `inspectedComplete = false`.
**BootstrapScene готова:** ошибок осмотра 0 — уборка T-FO06H подтверждена живым аудитом. Безобидный флаг dirty сброшен сохранением идентичного содержимого, байты файла не изменились (проверено хешем). Флаг важен не косметически: аудит выводит из него `saved`, а компилятор требует `saved = true`. Установлено, что флаг поднимает перезагрузка домена при компиляции скриптов, а не правки содержимого.
**Варианты решения:** точечная правка YAML сцены (не делаю без разрешения); удаление вручную через контекстное меню Inspector (рекомендуется, несколько кликов); каталог только для Bootstrap с добавлением WorldScene позже (противоречит заявленной области пилота).
**Инфраструктура:** повреждена ссылка `refs/remotes/origin/main (копия с компьютера DESKTOP-K00O7HK)` — `git log -S` падает с `bad object`; к задаче не относится. Связь с редактором дважды обрывалась на стороне ответа инструментов при живых процессах и чистом логе Unity; состояние проверялось через файловую систему.
**Файлы:** `06J_LIVE_SCENE_REVIEW.md`, `06J_LIVE_MISSING_COMPONENTS.json`, `tools/ListMissingComponentsLive.cs`, roadmap и этот журнал. Сцены не изменены по содержимому и в коммит не входят.
**Проверки:** обе сцены побайтово равны зафиксированным файлам; аудит — `loadedInspected=2`, `uninspected=24`, observations `150`, ошибок `2`, каталогов `0`. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** убрать три мёртвые ссылки; каталог на 150 записей; профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один diagnostics/docs commit; TMP fallback, Temp-скрипты и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06I)

**Задача:** Зафиксировать область пилота и измерить реальный объём работы по каталогу до его составления.
**Область:** По указанию пользователя пилот работает только с `BootstrapScene` и `WorldScene_0_0`; остальные 24 сцены содержат только разметку границ. Прежний открытый вопрос про 25 неосмотренных сцен снят: они вне пилота осознанно.
**Требование компилятора:** observation — это корневой объект ИЛИ NetworkObject, и на каждую нужна ровно одна запись с непустым `reviewNote`, иначе `review_does_not_cover_observations`. Дополнительно проверяются соответствие treatment сетевой природе объекта, совпадение parentSourceId, корректность позы, запрет spatial-потомка под неспатиальным родителем, обязательное исключение потомков retired-родителя и отсутствие циклов.
**Измеренный объём:** Bootstrap — 222 GameObject, 55 корней, 18 NetworkObject (13 корневых), 60 observations, 0 missing scripts. WorldScene_0_0 — 1191 GameObject, 26 корней, 23 NetworkObject (14 корневых), 35 observations, 1 missing script. Итого **95 проверенных записей**, а не 1413: вложенная геометрия города в каталог не попадает, объём посильный.
**Оговорка по цифрам:** прежний аудит сообщал 61 observation и 56 корней для Bootstrap против 60 и 55 здесь. Причина — способ подсчёта: аудит использует `GlobalObjectId` и учитывает prefab-instance, замер разбирает YAML. Источником для авторинга остаётся собственный аудит проекта; эти числа — оценка объёма, не окончательный состав.
**Четвёртый мёртвый компонент:** `[KeyRod_ShipHeavy]` в WorldScene_0_0 (GO fileID 1602512893, компонент 1602512899), guid `41bc0fc7a014832489bfd2db2ba89173`, класс `ProjectC.Ship.Key.KeyRodInstanceBinding`. Класс намеренно удалён: «P1-refactor: instanceId=0 (KeyRodInstanceBinding удалён)» в PickupItem.cs, а InventoryUI.cs защитно резолвит тип через `Type.GetType` и корректно работает без него. Мёртвая ссылка, не потерянная функциональность. Не удалён: сцена не загружена, удаление требует загрузки и сохранения. В Bootstrap отсутствующих скриптов теперь 0.
**Файлы:** `06I_PILOT_CATALOG_SIZING.md`, `06I_PILOT_CATALOG_SIZING.json`, `tools/SizePilotSceneCatalog.cs`, roadmap и этот журнал.
**Проверки:** замер по сохранённому YAML обеих сцен; ни одна сцена не загружалась, не изменялась и не сохранялась; каталог не создавался. No compile errors, Bootstrap не dirty. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** Убрать мёртвый KeyRodInstanceBinding из WorldScene_0_0 с восстановлением исходно открытой сцены; получить черновик каталога собственным аудитом на обеих загруженных сценах; проставить 95 решений; профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один sizing/docs commit; TMP fallback, Temp-скрипты, сцены, префабы, реестры и профиль исключены.

---

## Итерация от 2026-09-09 (T-FO06H)

**Задача:** Убрать три мёртвых компонента BootstrapScene после того, как пользователь сохранил сцену.
**Несохранённые изменения:** оказались безобидными — сохранение добавило ровно три строки `_globalMotionProfile`, `_globalSpawnBootstrap`, `_globalSessionCoordinator` со значением `fileID: 0`, то есть сериализацию полей, добавленных кодом на T-FO05E. Посторонней работы в несохранённом состоянии не было.
**Результат:** Удалены три компонента через штатный `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`, применённый ровно к трём опознанным объектам, без сплошной чистки сцены. Сцена сохранена.
**Подтверждение опознания:** diff показал `m_EditorClassIdentifier` удалённых компонентов — `ProjectC.Ship.Key.ShipKeyToast`, `ProjectC.Ship.Key.ShipKeyServer`, `ProjectC.Ship.Network.ShipOwnershipRegistry`. Это независимо подтверждает опознание T-FO06G по GUID и комментариям в коде. Замечание по методу: это поле стоило читать сразу в диагностике — оно даёт имя класса напрямую.
**Проверки:** скрипт отказывался работать при dirty-сцене, при числе missing-компонентов не равном трём, при неоднозначном имени и при любом отклонении счётчиков. После операции: missing `3 → 0`, количество GameObject и корней без изменений, компонентов ровно `−3`, сцена не dirty, файл на диске изменился, ни один из трёх GUID в файле не найден. Diff: 3 добавленные строки пользователя и 39 удалённых — три блока MonoBehaviour и три ссылки в `m_Component`, других изменений нет.
**Состояние редактора:** после сохранения Unity перестала отвечать на запросы инструментов; процессы живы, лог показывает успешный импорт сцены без ошибок и далее тишину. Работа проверена на файловой системе, данные не под угрозой; проверка компиляции отложена до восстановления связи.
**Файлы:** `BootstrapScene.unity`, `06H_DEAD_COMPONENT_CLEANUP.md`, roadmap и этот журнал.
**Остаток:** Восстановить связь с редактором и перепроверить компиляцию; минимальный набор сцен пилота и каталог только для него (теперь у Bootstrap есть основание для `saved = true` и нет missing-скриптов); профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один scene/docs commit; TMP fallback, Temp-скрипты, префабы, реестры и профиль исключены.

---

## Итерация от 2026-09-09 (T-FO06G)

**Задача:** Начать подготовку каталога в правильном порядке зависимостей и снять неопределённость по missing-компонентам Bootstrap.
**Порядок:** Профиль требует каталог и совпадающий digest, каталог требует осмотра сцен, поэтому начат осмотр, а не профиль. Установлено жёсткое требование компилятора: источник каталога обязан иметь `inspectedComplete` И `saved`, иначе `missing_uninspected_dirty_or_invalid_scene_source`. Каталог для несохранённой сцены невозможен, так как `dependencyHash` считается по содержимому на диске.
**Аудит сцен (собственный инструмент проекта):** 26 кандидатов, 1 осмотрена, 25 не осмотрены, 1 dirty, 61 unreviewed observation, 0 каталогов, 3 ошибки. Аудит прямо предупреждает, что dependency hash Bootstrap относится к сохранённому файлу, не к живой сцене.
**Опознание missing-скриптов:** По GUID из YAML сцены плюс подтверждение в коде установлены все три ранее UNRESOLVED компонента: `[ShipKeyToast]` `7e4592bffcae97841b2e9d1893479783` (DEPRECATED по комментарию в QuestToast.cs), `[ShipKeyServer]` `d915eb76076361f4bad13b481fe4c1cc` (CanPlayerBoard DEPRECATED, заменён MetaRequirementRegistry), `[ShipOwnershipRegistry]` `75a1a6152c04c0040b2c48c6fe27c18b` (удалён как дублирующий KeyRodInstanceWorld). Классы отсутствуют в коде, GUID отсутствуют во всех `.meta`. Вывод: мёртвые ссылки на намеренно удалённые классы, а не потерянная функциональность. Удаление не выполнялось.
**Исправление ложной диагностики:** первый прогон дал 21 «отсутствующий» компонент, но 18 оказались ссылками на встроенные объекты движка (`fileID 19102`, guid `0000…e000…`), у которых закономерно нет пути ассета. Ссылкой на скрипт считается только `fileID 11500000`. После исправления ровно 3, что совпадает с 3 ошибками собственного аудита — два независимых источника согласуются. По сохранённому файлу: 136 MonoBehaviour, 118 script refs, 18 built-in refs, 84 различных GUID.
**Файлы:** `06G_SCENE_REVIEW_AND_MISSING_SCRIPTS.md`, `06G_MISSING_SCENE_SCRIPTS.json`, `tools/DiagnoseMissingSceneScripts.cs`, roadmap и этот журнал. Сцены, префабы, реестры и runtime C# не изменялись.
**Проверки:** Диагностика выполнена по сохранённому YAML, сцена не загружалась и не сохранялась, компоненты не удалялись. No compile errors. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Блокеры:** Несохранённые изменения сцены (предшествуют этой работе, авторство не подтверждено) и связанная невозможность убрать мёртвые компоненты без сохранения всего текущего состояния сцены. Требуется решение пользователя.
**Остаток:** После решения по сцене — минимальный набор сцен пилота (25 world-сцен входить не обязаны), каталог только для него, профиль с digest и ссылкой на пилотный список, markers/frames/native executor, выбор пилота как PlayerPrefab, вывод legacy position services и ClientSceneLoader, issuer/store, пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один diagnostics/docs commit; TMP fallback, Temp-скрипты, сцены, префабы, реестры и профиль исключены.

---

## Итерация от 2026-09-09 (T-FO06F)

**Задача:** Реализовать вариант B — применять пилотный реестр только в global-пути, не меняя сцену и не ломая legacy.
**Результат:** `GlobalMotionNetworkProfile` получил декларативное `_registryLists` (пустое = конфигурация сцены без изменений, поведение по умолчанию не меняется). В подготовке запуска добавлены `TryApplyProfileRegistry`/`RestoreRegistry`: подмена только над живой конфигурацией, ни сцена, ни ассеты списков не записываются. Исходный экземпляр списка сохраняется и восстанавливается дословно — через `Release` при остановке/отмене и через `finally` на путях отказа до создания gate; владение передаётся gate в момент создания, поэтому двойного восстановления нет. Восстановление срабатывает только если текущий список — установленный нами, тем же приёмом, что уже применён для `ConnectionData`.
**Ключевая деталь:** `Prefabs.Prefabs` — агрегированный `[NonSerialized]` кэш `m_Prefabs`, пересобираемый в `Initialize()`; в редакторе он уже заполнен из default-списка через `OnValidate`. Проверка каталога перечисляет и списки, и кэш, поэтому подмена только `NetworkPrefabsLists` оставила бы старые 58 записей в эффективном реестре. После подмены и после восстановления вызывается `Prefabs.Initialize()`. Подмена выполняется до сборки hello и до снятия хеша конфигурации, поэтому защита `bootstrap_changed_network_start_ownership` продолжает проверять именно вмешательство bootstrap.
**Принятое последствие:** `Initialize()` подписывает `OnAdd`/`OnRemove` на списки без предварительной отписки, поэтому наш вызов плюс собственный вызов NGO при старте дают повторную подписку. Обработчики срабатывают только при изменении списка в рантайме, чего пилот не делает; пакет не правился.
**Валидация:** состав реестра вынесен в чистый `GlobalMotionNetworkContract.ValidateRegistryComposition` — отклоняются `null`-элементы, дубликаты и превышение предела; пустое объявление означает отсутствие подмены. Добавлен `ValidateGlobalRegistryComposition` — 13 чистых проверок на in-memory `NetworkConfig`/`NetworkPrefabsList`, без GameObject, NetworkManager, сцен, записи ассетов и Play Mode; проверяется и поведение NGO, на которое опирается подмена.
**Файлы:** изменены `GlobalMotionNetworkProfile.cs`, `GlobalMotionNetworkStartup.cs`, `GlobalMotionNetworkContract.cs`; новый `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalRegistryComposition.cs` с `.meta` (в этой папке метаданные скриптов исторически отслеживаются, общее правило `*.meta` не менялось). Отчёт `06F_GLOBAL_PATH_REGISTRY_SWAP.md`, roadmap и этот журнал.
**Проверки:** прогон всех валидаторов floating-origin — **16 из 16, 691 PASS / 0 FAIL** (678 прежних без изменений + 13 новых), регрессий нет. No compile errors. Фактическая подмена в реальном запуске НЕ проверялась: профиль не создан, запуск требует каталога, frames и executor. Play Mode/physics/network/native executor/screenshots/builds/auth/save access не использовались.
**Остаток:** Создать профиль с каталогом из одной Spatial-записи и ссылкой на пилотный список; scene catalog/markers/frames/native executor; выбрать пилота как PlayerPrefab и вывести legacy position services и ClientSceneLoader; три missing-компонента и несохранённое состояние Bootstrap; иерархия WorldScene_0_0; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один code/validator/docs commit; TMP fallback, Temp-скрипты прогона, профиль, сцены, префабы и реестры исключены.

---

## Итерация от 2026-09-09 (T-FO06E)

**Задача:** Добавить BootstrapScene в Git и определить, как применять пилотный реестр.
**Versioning:** В `.gitignore` добавлены точечные исключения по полным путям для `BootstrapScene.unity` и её `.meta`; общее правило `*.unity` сохранено, `BootstrapScene (копия с компьютера DESKTOP-K00O7HK).unity` остаётся игнорируемой. Проверено `git check-ignore`.
**Поправка к 06C/06D:** формулировка «остальные префабы и сцены остаются за Lore VCS» была неточной. `git ls-files` показывает, что `WorldScene_0_0.unity` и 43 префаба (включая 20 капитанских) уже отслеживаются: правила игнорирования добавлялись позже и на tracked-файлы не действуют. Правила блокируют добавление нового контента, но часть существующего ведётся в Git. Утверждения об изоляции пилотных ассетов остаются верны.
**Несохранённая сцена:** В коммит вошла версия файла с диска, байты идентичны снимку 06A. Несохранённые изменения редактора (`isDirty=true`, зафиксировано ещё на 06A и предшествует этой работе) в коммит не входят; сцена не сохранялась, так как авторство изменений не подтверждено и сохранение — решение пользователя.
**Развилка реестра:** Эффективный реестр перед старом собирается из всех списков `NetworkPrefabsLists` плюс встроенных `Prefabs.Prefabs`, поэтому пилотный список обязан заменять default, а не добавляться. Вариант A — замена в сцене: ломает спавн всех 58 префабов в legacy-режиме, обратимо только откатом сцены. Вариант B — условная подмена в ветке, которая включается лишь при назначенном и включённом `_globalMotionProfile`: legacy сохраняется, требует правки кода и аккуратности с существующей проверкой хеша конфигурации при старте. Вариант C — отдельная пилотная bootstrap-сцена с дублированием. Рекомендован B; ни один вариант не реализован, так как выбор влияет на работоспособность текущей игры.
**Файлы:** `.gitignore`, `BootstrapScene.unity` + `.meta`, `06E_SCENE_TRACKING_AND_REGISTRY_FORK.md`, roadmap и этот журнал.
**Проверки:** No compile errors. Сцены, префабы, реестры, настройки и runtime C# не изменялись — этап касается только версионирования и анализа. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** Global mode/world shift выключены, пилот не подключён. Далее: подтвердить вариант применения реестра, profile с каталогом из одной Spatial-записи и SceneLayoutDigest, scene catalog/markers/frames/native executor, вывод legacy position services, issuer/store, пользовательский Host+client gate. Jitter fixed не заявляется.
**Коммит:** один versioning/scene/docs commit; TMP fallback, Temp-скрипты, прочие сцены/префабы и DefaultNetworkPrefabs.asset исключены.

---

## Итерация от 2026-09-09 (T-FO06D)

**Задача:** Сократить реестр до минимального пилотного, чтобы проверить игрока раньше, и точно зафиксировать остаток.
**Результат:** Создан `Assets/_Project/Prefabs/FloatingOrigin/GlobalPilotNetworkPrefabs.asset` — ровно 1 запись (пилотный игрок, hash 3692800100), `Override=None`, `IsDefault=false`, ни одной ссылки из сцен. `DefaultNetworkPrefabs.asset` не изменён: 58 entries, serialized-содержимое и байты идентичны, пилот в него не утёк.
**Классификация 58 записей:** прогон реального валидатора контракта. Spatial-ready=0, NonSpatial-ready=1 (`NEWCHESTPREFAB`), требуют миграции=57, битых=0. Причины по Spatial: 54 `missing_global_motion_components_or_optin`, 2 `invalid_behaviour_count`, 2 `inactive_network_behaviour_object`. NonSpatial присваивался только при фактическом отсутствии пространственного содержимого, иначе запись отнесена к миграции.
**Структурный блокер:** `TestPlayer` и `PickupItem_Test` не имеют ни одного NetworkBehaviour; `NetworkChestContainer_Test` и `SPAWN_TEST` держат NetworkBehaviour на выключенном GameObject. Эти проверки выполняются до ветвления по роли, поэтому 4 записи не проходят ни одну роль — путь полного реестра блокирован структурно, а не только объёмом. Исправления не выполнялись.
**Остаток вне пилота:** спавн кораблей, NPC, торговых и квестовых зон, предметов, сундуков, pickup-объектов, ресурсных узлов, станций крафта и тестовых спавнеров не будет работать. Также остаются legacy ShipPositionServer/PlayerPositionServer, активный ClientSceneLoader, 3 неопознанных missing-компонента, неосмотренная WorldScene_0_0, issuer и store ownership.
**Чтобы пилот запустился:** пилотный список должен ЗАМЕНИТЬ default в BootstrapScene (эффективный реестр = все списки + встроенные записи, иначе станет 59 и каталог обязан описывать все 59); затем profile с каталогом из одной Spatial-записи и корректным SceneLayoutDigest, scene catalog/markers/frames/native executor, выбор пилота как PlayerPrefab, вывод legacy services, issuer/store и пользовательский Host+client gate.
**Файлы:** пилотный список + `.meta`, `.gitignore` (исключение расширено на `*.asset.meta` в пилотной папке для стабильных GUID), `06D_PILOT_REGISTRY_AND_REMAINDER.md`, `06D_REGISTRY_CLASSIFICATION.json`, `tools/AuditFo06DRegistryClassification.cs`, roadmap и этот журнал.
**Проверки:** аудит не изменил default-реестр (содержимое и байты); после создания списка перепроверены запись/override/IsDefault/hash и неизменность default-реестра; `git check-ignore` — `.meta` пилотного списка отслеживается, `NetworkPlayer.prefab.meta` по-прежнему игнорируется. No compile errors. Play Mode/physics/network/native executor/screenshots/builds/auth/save access не использовались.
**Остаток:** Global mode/world shift выключены, пилот не подключён, jitter fixed не заявляется.
**Коммит:** один asset/versioning/results/docs commit; TMP fallback, Temp-скрипты, DefaultNetworkPrefabs.asset, canonical префаб и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06C)

**Задача:** Сделать пилотный префаб floating-origin отслеживаемым в Git по указанию пользователя.
**Результат:** Общее правило `*.prefab` из блока Lore VCS сохранено; в конец `.gitignore` добавлено узкое отрицание для `Assets/_Project/Prefabs/FloatingOrigin` — сам префаб, его `.meta` и `.meta` папки. Метаданные включены осознанно: без них GUID нестабилен при клонировании. Глобальное снятие правила отклонено, так как добавило бы в Git все префабы проекта (корабли, NPC, зоны) и конфликтовало с Lore VCS. Отрицания размещены после правил `*.prefab` и `*.meta`, иначе не срабатывают. Сам `.gitignore` перечисляет себя, но остаётся tracked, поэтому правка коммитится.
**Проверки:** `git check-ignore -v` — пилотный префаб и оба `.meta` отслеживаются; `Assets/_Project/Prefabs/NetworkPlayer.prefab` и `BootstrapScene.unity` по-прежнему игнорируются. Повторная read-only верификация после правки: Spatial contract PASS, 6 NetworkBehaviour, пять NO flags = false, canonical остался legacy, 8 protected файлов побайтово равны 06A, автогенерация off, registry=58, пилот не зарегистрирован и не выбран. No compile errors. Play Mode/physics/network/screenshots/builds не запускались.
**Файлы:** `.gitignore`, пилотный префаб + `.meta`, `.meta` папки, `06C_PILOT_PREFAB_TRACKING.md`, уточнение §6 в `06B_PILOT_PLAYER_PREFAB.md`, roadmap и этот журнал.
**Остаток:** Изменение касается только версионирования; пилот не активирован, global mode/world shift выключены. Далее: классификация profile/catalog (эффективный registry должен точно совпадать с classified catalog — это все 58+ записей, не только игрок), prepared frames/markers/native executor, dirty Bootstrap с legacy position services/ClientSceneLoader/3 missing scripts, native audit WorldScene_0_0, issuer/store. Jitter fixed не заявляется.
**Коммит:** один commit versioning/asset/docs; TMP fallback, Temp-скрипты, DefaultNetworkPrefabs.asset, canonical префаб и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06B)

**Задача:** Перевести регистрацию сетевых префабов в явный режим и создать изолированный пилотный префаб игрока, не активируя global mode.
**Registration:** `GenerateDefaultNetworkPrefabs=true → false`, персистится в новом `ProjectSettings/NetcodeForGameObjects.asset` (Git его не игнорирует). `DefaultNetworkPrefabs.asset` не изменён: 58 entries, serialized-содержимое, bytes и meta идентичны, canonical player остался в списке. Список продолжает использоваться через NetworkConfig; отключён только AssetPostprocessor. Следствие: новые сетевые префабы требуют явной регистрации, а автоудаление записей больше не работает.
**Pilot prefab:** `Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab` создан независимой копией, а не variant — override мог бы вернуть удалённый NetworkTransform. Удалён NetworkTransform, добавлены PlayerAttacker, PlayerTarget, GlobalMotionReplicator и GlobalMotionPoseAdapter с `_coordinatesRequired=true`; SynchronizeTransform/AutoObjectParentSync/SceneMigrationSynchronization выставлены false, остальные два флага перепроверены. Порядок NetworkBehaviour (6) одинаков у сервера и клиентов, так как это один asset.
**Уточнение 06A:** ValidateLayout для Spatial требует **пятое** условие — baked PlayerAttacker/PlayerTarget; это совпадает с существующей ошибкой NetworkPlayer для global-игрока. Skill-компоненты, добавляемые owner-only в рантайме, — обычные MonoBehaviour и на порядок NetworkBehaviour не влияют.
**Файлы:** `06B_PILOT_PLAYER_PREFAB.md`, `06B_PILOT_PLAYER_PREFAB.json`, `tools/VerifyFo06BPilotPrefab.cs`, `ProjectSettings/NetcodeForGameObjects.asset`, roadmap и этот журнал. Runtime C# проекта не менялся.
**Проверки:** Spatial contract PASS (features включают Adapter/Replicator/CoordinatesRequired/PlayerAttacker/PlayerTarget), pilot hash `3692800100` ≠ canonical `186599647`, у пилота нет stock writers/nested NO/body/joint/nav/2D и лишних participants, canonical остался legacy, 8 protected файлов побайтово равны committed 06A baseline, автогенерация off, registry=58, pilot не зарегистрирован, выбранный PlayerPrefab по-прежнему canonical. No compile errors; 678 pure checks E не перезапускались. No Play Mode/physics/network/screenshots/builds/auth/save access.
**Остаток:** Пилот попадает под `.gitignore` `*.prefab`, существует только локально — принудительная публикация не выполнялась и требует решения. Далее: классификация profile/catalog (эффективный registry должен точно совпадать с classified catalog), prepared frames/markers/native executor, dirty Bootstrap с legacy position services/ClientSceneLoader/3 missing scripts, native audit WorldScene_0_0, issuer/store. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один docs/results/tool/settings commit; пилотный префаб, TMP fallback, Temp-скрипты, DefaultNetworkPrefabs.asset и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06A)

**Задача:** Завершить read-only preflight конкретного pilot scope после E, не включая global mode и не меняя пользовательские assets/settings.
**Результат:** Проверены actual NetworkManager selection и canonical NetworkPlayer, загруженная Bootstrap, NGO default registry/settings и WorldScene_0_0 asset dependencies. Найдены 4 группы player blockers: отсутствуют replicator/opt-in adapter, присутствует stock NetworkTransform, требуются global-only NO flag overrides. CC параметры, hierarchy/NetworkBehaviour order и visual references сохранены в JSON. Bootstrap dirty=true, 56 roots, 3 missing component entries с UNRESOLVED identity, enabled legacy position services и ClientSceneLoader; ничего не удалено. WorldScene hierarchy не осмотрена, прежний полный H census не закрыт.
**Registration boundary:** GenerateDefaultNetworkPrefabs=true, active default list=58 entries. Установленный NGO processor добавляет любой импортированный prefab с root NetworkObject независимо от папки: отдельная PilotAssets не обеспечивает изоляцию. Registration сама не означает spawn/global activation. Настройка и список не изменены; proposed явная регистрация с отключением auto-generation требует отдельного подтверждения, поскольку меняет добавление будущих сетевых prefab.
**Файлы:** `docs/world/floatingorigin/06A_PILOT_SCOPE_PREFLIGHT.md`, `06A_PILOT_SCOPE_BASELINE.json`, `tools/AuditFo06APilot.cs` вне Assets, roadmap и этот журнал. Runtime C# проекта не менялся; Temp runner в коммит не входит.
**Проверки:** Сохранённый read-only audit успешно выполнен внутри Editor, nested JSON evidence прошёл round-trip. Три integrity assertions PASS: loaded scene/hierarchy/dirty fingerprint; полное serialized registry и setting/dirty; SHA256 восьми protected файлов (player/Bootstrap/World/registry + meta). No compile errors. Исторические 678 pure checks E не перезапускались и не выдаются за новую проверку. No scene open/save/preview, prefab/GameObject creation, Play Mode/screenshots/physics/network/builds/auth или actual save access.
**Остаток:** Pilot configuration BLOCKED, global mode/world shift выключены. Согласовать registration policy → isolated player variant/pilot list → dirty Bootstrap/missing scripts/legacy service ownership → native WorldScene/catalog/frames/physics → issuer/store/config → пользовательский Host+client acceptance. Полные T-FO03–09 не закрыты, jitter fixed не заявляется.
**Коммит:** один audit/results/docs commit; TMP fallback, Temp runner, engine assets/settings/packages и исторические отчёты исключены.

---

## Итерация от 2026-09-09 (T-FO05E)

**Задача:** Связать подготовленные D/G/C/B с actual NMC global-session lifecycle без активации в текущей игре и без fake account identity.
**Результат:** GlobalMotionSessionCoordinator принимает explicit prepared frames/repository/timing/trusted Host policy, конфигурирует D перед существующим startup gate, принимает session-token + actual NetworkClient trusted receipts и откладывает identity/plans до World/frame readiness. Проверяются role, source ownership, уникальный PlayerId, текущая connection и confirmed PlayerObject/spawn lifetime. Account provider в исследованном пути не подтверждён (inconclusive); approval/72-byte hello не заменяются и не объявляются auth.
**Persistence/stop:** C/B checkpoint по расписанию только для полного verified/confirmed roster, backoff при неподготовленном capture, Applied-only success и latched write fault без automatic retry/quarantine. NMC deliberate stop/reconnect/restart требуют checkpoint до Shutdown либо явного abandon API; no-live-player stop не выдумывает final snapshot. Emergency/transport teardown отмечается unplanned. Individual unexpected-disconnect final-save и same-connection respawn ещё открыты.
**Legacy/UI isolation:** Global start блокируется при любых loaded ShipPositionServer/PlayerPositionServer, даже disabled: ShipPositionServer подписывается в Awake. Компоненты не удалялись; legacy-файлы не менялись. NMC global paths пропускают old Prepare/SaveNow/ClientSceneLoader reset, global NetworkPlayer рано выходит из legacy restore coroutine. Отказ checkpoint вызывает отдельный onBlocked; EscMenu сбрасывает exit-progress и открывает существующее меню, без новой layout/localization. Global teardown timeout не вызывает false success.
**Файлы:** новые GlobalSessionPersistencePolicy.cs, GlobalMotionSessionCoordinator.cs, Editor ValidateGlobalSessionOrchestration.cs и 3 meta. Точечно изменены NetworkManagerController.cs, GlobalMotionPlayerBootstrap.cs, GlobalMotionCheckpointSpawnSource.cs, NetworkPlayer.cs и EscMenuWindow.cs. Report `05E_SESSION_ORCHESTRATION.md`, JSON, roadmap и этот журнал.
**Проверки:** No compile errors; **58 E + 620 прежних = 678 pure PASS / 0 FAIL**, после final teardown guard повторены. Pure policies/schedule/ref-token/role/stop и actual C/B с memory storage; compiled native/menu seams только inspected. Actual E/NMC/auth/GUI/NGO/disk/native readiness, реальные saves, сцены, GameObjects, Play Mode, physics, network, builds/screenshots не вызывались. Read-only prefab guard: 58 candidates; opt-in/profile/loaded profile/adapters/markers/executors/source/session=0. Wire F005 и A/B formats сохранены.
**Остаток:** Код orchestration готов, actual issuer/config/store ownership и pilot content/catalog/markers/frames/prefab/profile не подготовлены. Native disk/readiness/GUI/Host+client acceptance остаётся пользовательским. Оценка D — ориентир, не автоматический процент/обратный счётчик; полные T-FO04–09 и semantic T-FO03 открыты, global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один code/meta/results/docs commit; TMP fallback, Temp runner, historical A–D reports/JSON, legacy save files, scene/prefab/profile/catalog и packages исключены.

---

## Итерация от 2026-09-09 (T-FO05D)

**Задача:** Реализовать explicit pre-spawn identity/restore plans и concrete source G; после этапа оценить оставшуюся интеграцию и тесты.
**Результат:** GlobalPlayerSpawnPlanResolver + GlobalMotionCheckpointSpawnSource реально реализуют G interface для fixed prepared frames. Configure до старта требует actual scene/physics definitions и explicit replica frame; actual I.ValidatePreparation остаётся обязательным. После connection/World start trusted caller назначает stable PlayerId actual NetworkClient reference/session/run, затем готовит checkpoint либо явно заданный first-spawn с pose/rules/frame/nonce/deadline. Нет account ID из clientId, implicit zero или nearest frame; ship/out-of-frame/unknown checkpoint без explicit first-spawn блокируются.
**Интеграционный код:** G factory guard до instantiate, после Awake/OnEnable до SpawnAsPlayerObject, confirm actual initial-ready player и handoff identity в C; original source/client/queue/nonce проверяются. Новое поле ReservationId — только server-memory plan, не wire/save. B readonly IsObservationCurrent проверяет raw published lineage без lease через NGO callbacks. Source не подключает startup/auth/loading/save schedule самостоятельно.
**Исправления review:** C.TryRetire немедленно запрещает доступ при retirement внутри readiness callback и откладывает dictionary cleanup до unwind; D.Clear не вызывает throwing busy Dispose. Cancel не отзывает identity Completed player. Disconnect observer не удаляет still-live/ref-matching peer по позднему ID-only сигналу; stale cleanup выполняется до capacity check. Native partial spawn failure требует halt/despawn/session shutdown, а не заявления об атомарном rollback.
**Файлы:** новые GlobalPlayerSpawnPlanResolver.cs, GlobalMotionCheckpointSpawnSource.cs, Editor ValidateGlobalCheckpointSpawn.cs и 3 Unity-generated meta. Изменены GlobalMotionSpawnContracts.cs, GlobalMotionPlayerBootstrap.cs, GlobalPlayerCheckpointRepository.cs и GlobalMotionPlayerCheckpointSource.cs. Отчёт/оценка `docs/world/floatingorigin/05D_CHECKPOINT_SPAWN_SOURCE.md`, JSON, roadmap, этот журнал.
**Проверки:** No compile errors; **60 D + 560 прежних = 620 pure PASS / 0 FAIL**. Pure lookup/projection/pose/rules/nonce/deadline/cancel policy, actual B repository с memory storage и fake-interface guard dispatch. Native Mono sources, actual identity/NGO/CC/disposal, disk/crash/network/IL2CPP UNTESTED. Guard: 58 prefab candidates; opt-in/profile/loaded profiles/adapters/markers/executors/checkpointSources=0. Ни GameObjects, ни сцены/префабы, реальные saves, Play Mode, physics, networking, builds или screenshots не использовались.
**Остаток:** Concrete fixed-frame G source теперь есть, но actual auth input (поиск inconclusive), orchestration/configuration, content/catalog/frame/prefab migration и native save lifecycle отсутствуют. Полные T-FO04–09 и semantic T-FO03 не закрыты. Оценка после D: 4–6 этапов до узкого fixed-frame Host+client прогона; 8–12 до первого реального rebase; 20–35 итераций по 10 крупным блокам до полной интеграции плюс ориентировочно 2–3 пользовательских тестовых цикла. Рубежи включают предыдущие, не суммируются; unknown native/scene scope может увеличить оценку. World shift/global mode выключены, jitter fixed не заявляется.
**Коммит:** один code/meta/results/docs commit, без TMP fallback, Temp runner, historical A/B/C report/JSON, scenes/prefabs/profile/catalog или package changes.

---

## Итерация от 2026-09-09 (T-FO05C)

**Задача:** Подготовить executable authoritative global player capture/merge и live guard перед B publication, не включая save/load в игре.
**Результат:** GlobalMotionPlayerCheckpointSource читает только latest server-accepted WORLD motion, проверяет полный connected-player roster и выдаёт ephemeral snapshots по explicit trusted stable identity leases. Lease привязана к реальным NetworkClient/PlayerObject refs и session/run/spawn; native source только на World Unity thread. Новые readonly World/Replicator APIs не меняют wire, sequence или Transform. Ship/ParentLocal/unmapped/unready players блокируют весь capture, не пропускаются.
**Evidence/liveness:** Instance-issued snapshots (weak table), bounded freshness, exact full binding/owner/identity lease/roster. Новая обычная accepted motion допустима при монотонных seq/time — сохраняется исходный свежий point-in-time. Same-sequence rewrite, teleport/authority/respawn/reconnect/expiry отвергаются. Final capture pass без participant callbacks выявляет изменения во время обхода readiness.
**Merge/commit:** Offline записи сохраняются, active IDs обновляются по global point и trusted caller UTC timestamp; clock regression не клэмпится. Single-use prepared batches, B CAS original observation и optional synchronous publicationGuard до staging и перед publish с raw-state recheck. Pending после позднего отказа явно возвращается без авто-cleanup/retry; escaped storage failure после входа не маскируется как NotApplied. Не вызываются legacy restore/Transform teleport, реальные files или auto-save loop.
**Файлы:** новые GlobalPlayerCaptureContracts.cs, GlobalMotionPlayerCheckpointSource.cs, GlobalPlayerCheckpointCapture.cs и Editor ValidateGlobalCheckpointCapture.cs, четыре Unity-generated meta. Точечно изменены GlobalMotionWorld, GlobalMotionReplicator и GlobalPlayerCheckpointRepository; A/B schemas и F005 protocol сохранены. C report/validation JSON, roadmap и этот журнал.
**Проверки:** compile PASS; **58 capture/merge/guard + 502 прежних = 560 pure PASS / 0 FAIL**. Контролируемый source, pure GlobalMotionAdmission/policy и реальный B repository against in-memory storage: ownership/freshness/sequence/identity/scope guards, offline retention, non-starvation при движении, staged expiry/teleport, explicit pending quarantine, CAS/single-use/re-entrancy/thread cases. Compiled native seams только inspected, реальные NGO actors/source/disk не запускались.
**Границы:** auth mapping до spawn и genuine G provider/restore-plan не реализованы; поиск отдельного account provider в исследованном пути inconclusive. Ship/ParentLocal/NPC/RPC, disconnect final-save, native disk/crash/network/IL2CPP и scheduling/performance acceptance открыты. Guard повторно: 58 candidates, opt-in/profile/loaded profiles/adapters/markers/executors=0. No GameObjects/Play Mode/physics/network/screenshots/builds или actual save access. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один commit source/meta/results/docs. Temp/Aura runners и несвязанный LiberationSans fallback исключены; historical A/B/I reports/JSON не переписывались.

---

## Итерация от 2026-09-09 (T-FO05B)

**Задача:** Реализовать отдельный transactional global player checkpoint repository с backup/recovery, не подключая его к работающим saves и source G.
**Результат:** Immutable полный player snapshot с StoreId/revision/fresh commit/parent IDs и canonical UTF8 envelope поверх frozen A v1 records. Repository-instance observation fingerprint связывает primary/backup/pending bytes; cooperative lease сериализует чтение/публикацию. Нет persisted runtime frame/NGO ids, default save path или auth inference.
**Native adapter:** DirectoryGlobalPlayerCheckpointStorage требует явный canonical absolute local directory, использует фиксированные non-legacy filenames, FileShare.None lock, CreateNew pending + Flush(true), File.Replace с previous backup либо File.Move при первой записи. Нет File.Copy overwrite/delete-before-move fallback. Permission/IO errors не означают Empty; unknown/future/foreign schema и broken backup lineage блокируются. Native adapter фактически не вызывался.
**Recovery/guards:** Backup только как explicit RecoveryCandidate, не автоматическая загрузка; recovery сохраняет known-good backup и corrupt primary bytes в quarantine, mint-ит fresh CommitId против ABA. Pending никогда не авто-promote-ится, только явный byte-verified quarantine. После exception результат публикации перепроверяется; Applied/NotApplied/Conflict/Unavailable/Indeterminate/RecoveryRequired не маскируют partial/uncertain state. Missing offline players требуют explicit removal authorization, older timestamps отвергаются.
**Файлы:** GlobalPlayerCheckpointSnapshot.cs, GlobalPlayerCheckpointStorage.cs, GlobalPlayerCheckpointRepository.cs; Editor ValidateGlobalCheckpointTransactions.cs и четыре Unity-generated script meta. 05B_CHECKPOINT_TRANSACTIONS.md / 05B_STATIC_VALIDATION.json, roadmap и существующий журнал. A formats/legacy repository/current save-load и прочий runtime не изменялись.
**Проверки:** compile PASS; **68 transaction-model + 434 прежних = 502 pure PASS / 0 FAIL**. Golden envelope, immutable/sorted scope, staged partial/corrupt writes, exceptions before/after publication/quarantine, broken backup/head, unsupported replace, lost read access, stale/cooperative/reentrant writers, explicit recovery/ABA, offline/timestamp guards. Всё на in-memory storage model; не доказательство native disk crash atomicity.
**Границы:** user save data не читались/не писались, реальные backups не создавались. Native FS/Replace/Flush/locking/power-loss/IL2CPP acceptance, authoritative collector/merge, stable identity/auth mapping, G spawn source и ship/RPC migration открыты. Нет directory fsync/гарантии всех FS/cloud mounts или универсального recovery любых blocked файлов; quarantines не очищаются автоматически. No Play Mode/GameObjects/network/physics/screenshots/builds. Guard: 58 candidates, opt-in/profile/loaded profiles/adapters/markers/executors=0. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один коммит code/meta/results/docs; Temp/Aura runners и LiberationSans fallback исключены. Исторические A/I/H reports/JSON сохранены.

---

## Итерация от 2026-09-09 (T-FO05A)

**Задача:** Подготовить безопасный global player position persistence контракт как зависимость G source, без активации save/load миграции и пересечения runtime gate T-FO04.
**Исходная граница:** PlayerPositionSaveData сохраняет float px/py/pz и NGO clientId в общем ShipPositions.json с ships. PlayerPositionServer восстанавливает по clientId, repository пишет через JsonUtility + File.WriteAllText. Постоянная account identity в исследованном пути не подтверждена; нет оснований сохранять runtime frameId или считать clientId устойчивым ключом. Старый DTO/repository/collector/restore не изменялись.
**Результат:** GlobalPlayerPositionRecord (immutable global point/persistent ID/ship affinity/timestamp); fileless GlobalPlayerPositionCodec с отдельным schema v1, invariant round-trip double strings, typed SHA256 и exact canonical JSON validation. Missing/unknown/duplicate/malformed/default inputs отклоняются. Checksum не является authentication. Persisted frame/origin/NGO ids отсутствуют, нулевая точка возможна только как явно предоставленные данные.
**Legacy import:** LegacyPlayerPositionImport создаёт all-player draft только для текущего известного compact JsonUtility wrapper. Требуются digest конкретного input text, полное взаимно-однозначное identity сопоставление и reviewed absolute либо explicit valid local-frame provenance. Нельзя угадать origin, восстановить потерянную float precision или принять новый clientId за identity. Ships не конвертируются/не отбрасываются; весь supplied text удерживается, но это НЕ disk backup. Older/pretty/reordered/extended layouts fail-closed; ошибка одной записи не публикует частичный результат.
**Файлы:** три новых runtime helper в Scripts/World/FloatingOrigin/Persistence, Editor ValidateGlobalPlayerPersistence, четыре script meta + folder meta; 05A_PLAYER_GLOBAL_PERSISTENCE.md, 05A_STATIC_VALIDATION.json; roadmap и этот журнал. Другие runtime C#, scenes/prefabs/assets/packages не изменялись.
**Проверки:** compile PASS; **69 persistence + 365 прежних = 434 pure PASS / 0 FAIL**. Frozen v1 JSON/checksum и known legacy fixture, double extremes/100 generated points, independent frames/locale, integrity/shape/size bounds, identity/provenance/timestamp guards и unchanged legacy DTO. Всё выполнено в памяти; настоящий persistentDataPath/ShipPositions.json/repository не читались и не вызывались. Guard: 58 candidates, opt-in/profile assets/loaded profiles/adapters/markers/executors=0.
**Границы:** нет transactional storage/backup/recovery, auth mapping, native restore/checkpoint collection или genuine source G; ships/RPC и дальнейшие native bridges открыты. Нельзя записывать player-only draft поверх unified legacy file. Без Play Mode, сетевых сессий, GameObject/physics тестов, screenshots и builds; actual save/reconnect/runtime UNTESTED. NGO protocol остаётся 0xF005, legacy=0; global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один коммит кода/meta/результатов/документации. Temp/Aura runners и несвязанный LiberationSans fallback исключены; исторические I/H/G reports и JSON не переписываются.

---

## Итерация от 2026-09-09 (T-FO04I)

**Задача:** Реализовать ограниченный native scene-source binding/executor поверх H ledger, не активируя неподготовленную игру.
**Результат:** GlobalSceneSourceMarker хранит baked identity/frame/activation intent; GlobalSceneNativeExecutor проверяет каждый catalog root/NetworkObject и полный preloaded initial scene set. До NGO Start размещается только уже inactive static content по явной GlobalPosition; server Spawn NONSPATIAL scene services выполняется после native sweep с receipts после факта. Клиент включает prepared sources в OnClientStarted до NGO lookup; server public Additive synchronization сохраняет prepared scene instances. Remote approval и G player spawn ждут scene receipts; host-local approval не блокируется циклом до OnServerStarted. Protocol=0xF005, legacy=0 не изменён.
**Retirement/guards:** CanRecordRetired — read-only preflight без потребления receipt. Local child-first Despawn(false)/deactivation → receipt; unknown runtime roots/NetworkObject descendants/persistent infrastructure блокируют retire. Explicit activation, enabled/inactive source, parent/frame/scene checks и post-native callback shutdown guards. Initial receipt timeout=60s. Rollback transform только до networking, для inactive unchanged identity; partial native failure означает fault/stop, не фиктивный atomic rollback.
**Файлы:** новые GlobalSceneSourceMarker.cs (включая pure policy/admission contract), GlobalSceneNativeExecutor.cs, Editor ValidateGlobalSceneExecution.cs и три Unity-generated meta; изменены GlobalMotionPlayerBootstrap/NetworkStartup/NetworkContract, GlobalSceneLifecycleLedger, исторический H validator. Добавлены I report и два JSON, обновлены roadmap и этот журнал.
**Проверки:** compile PASS; фактически выполнено **31 I + 56 H + 44 G + 40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 365 pure PASS / 0 FAIL**. В static review исправлены client inactive/IsSpawned lookup deadlock и Single-mode reload hazard. Эти проверки НЕ выполняли native executor operations, не проверяли реальный NGO lifecycle, physics или gameplay.
**Аудит/блокеры:** 58 prefab candidates, opt-in/profile assets/loaded profiles/adapters/source markers/scene executors=0; 26 scene candidates, loaded/dirty=1, uninspected=25, 61 Unreviewed observations, catalog assets=0. Прежние missing-component subtree diagnostics Inventory, Inventory/[ShipKeyServer], Toasts_and_meta не изменялись; сцены не загружались/не сохранялись.
**Границы:** только inactive static content и nonspatial scene services. Spatial scene actors, bodies/nav/CC, replacements/exclusions, DDOL, pool/streaming/distributed unload/recovery и genuine prepared-content/global-persistence source ещё не реализованы. Baking/назначение markers/catalog/profile не выполнены. Без Play Mode, игровых сетевых сессий, тестовых GameObject, physics simulation и screenshots. Global mode/world shift выключены; полный T-FO04 и jitter fix не заявляются.
**Коммит:** один коммит кода/meta/результатов/документации без собственного хеша отдельным коммитом. Temp runner и несвязанный LiberationSans fallback исключены; H/G исторические отчёты и JSON сохранены.

---

## Итерация от 2026-09-09 (T-FO04H)

**Задача:** Подготовить reviewed scene catalog и lifecycle сценовых/дочерних объектов без активации native миграции.
**Результат:** GlobalSceneCatalogCompiler проверяет замкнутость заявленного scene/observation/review набора, координатную семантику, authored parent graph, replacement/exclusion; deterministic SHA256 и immutable ordered plan. Profile/hello требуют computed catalog digest = declared digest, replacement prefab hash/role сверяются с классификацией. Protocol=0xF004, G hash-only global контракт несовместим; legacy=0 сохраняется.
**Lifecycle:** pure receipt ledger с уникальными ledger/load generations, same-parent/frame registration, duplicate/stale network identity rejection и отдельными registration tokens также для non-network content. Child-first cleanup/exclusions, indexed child lookup, no silent clear of live/unseen sources. Fault остаётся fail-closed до внешнего recovery. Это НЕ native Spawn/Despawn/SetParent/scene loader и НЕ readiness certificate.
**Файлы:** существующая untracked GlobalMotionSceneCatalog.cs сохранена без переписывания; новые GlobalSceneCatalogCompiler.cs, GlobalSceneLifecycleLedger.cs, Editor AuditGlobalSceneCatalog.cs и ValidateGlobalSceneCatalog.cs; пять Unity-generated meta. Изменены profile/PrefabInspector/NetworkContract, исторический G validator, roadmap и журнал; добавлены H report и два generated JSON.
**Проверки:** compile PASS; реально выполнены **56 catalog/lifecycle + 44 spawn + 40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 334 pure PASS / 0 FAIL**. Повторный prefab guard: 58 candidates, opt-in/profile assets/loaded profiles/adapters=0.
**Реальные ограничения аудита:** 26 сцен-кандидатов, inspected=1 (dirty), uninspected=25, observations=61 (все draft entries Unreviewed), catalog assets=0. Missing-component diagnostics в трёх пересекающихся/отдельных поддеревьях: Inventory, Inventory/[ShipKeyServer], Toasts_and_meta; это не обязательно три уникальных скрипта. Находки не исправлялись, dirty scene не сохранялась, неизвестные сцены не загружались. Реальный каталог не утверждён.
**Границы:** native NGO in-scene sweep происходит до OnServerStarted; следующий executor должен контролировать pre-start placement, а не только поздний callback. Runtime binding/baked scene IDs, native bridges, pools и full source provider ещё не реализованы. Scene/prefab/profile activation, Play Mode, physics simulation, сетевые сессии и screenshots не выполнялись; world shift выключен, jitter fixed не заявляется.
**Коммит:** один коммит source/meta/результатов/документации; без собственного хеша отдельным коммитом. Temp runner, LiberationSans fallback и исторические G/F artifacts не включаются/не переписываются.

---

## Итерация от 2026-09-09 (T-FO04G)

**Задача:** Реализовать ограниченный concrete player spawn bootstrap и initial local placement, не активируя неполную миграцию.
**Результат:** GlobalMotionPlayerBootstrap + startup installation/release; approved-peer tickets с epoch/serial, bounded queue, registration подготовленных frames после World.IsRunning; NGO typed global seed до instantiation; local scene/pose и initial CC hold до Awake; PostSpawn Bind → first World baseline → exact-binding initial release → ACK. Seed не передаёт origin/frame ID клиента, обновляется на network ticks для late join. Protocol=0xF003, F/E global peers несовместимы; legacy=0 сохраняется.
**Guards:** только enabled root NetworkPlayer/CC, без stock NT даже disabled, bodies/nav/joints/других colliders и дополнительных native participants; server initial World spawn синхронный. Frame lease не пересоздаётся молча; remote plans требуют game rules; foreign handler не заменяется при установке. Unspawned failures/Despawn/Release/OnDestroy очищают bookkeeping. Ошибки initial lifecycle приводят к fail-closed shutdown, не к фиктивному игроку. HP/velocities/input intent/docking/Animator не сбрасываются; SetInputEnabled не используется как coordinate pause.
**Изменения:** новые GlobalMotionSpawnContracts.cs, GlobalMotionPlayerBootstrap.cs, Editor ValidateGlobalMotionSpawn.cs и три Unity-generated meta; NetworkPlayer, GlobalMotionNetworkStartup/World/Replicator/NetworkContract и hierarchy validator; G report/JSON, roadmap и этот журнал.
**Проверки:** исправлены Scene namespace collision и только Editor serializer accessibility/definite-assignment ошибки. Compile PASS; фактически выполнены **44 spawn + 40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 278 pure PASS / 0 FAIL**. Повторный audit: 58 candidates, opt-in=0/profile assets=0/loaded enabled profiles и adapters=0. Без Play Mode, тестовых GameObject, physics, реальных сетевых сессий и screenshots; runtime UNTESTED.
**Границы:** реализация IGlobalMotionPlayerSpawnSource отсутствует намеренно: prepared content/global persistence/AOI/scene coverage должны быть настоящими, startup без них блокируется. Legacy ClientSceneLoader должен быть заменён внешним content bridge, здесь не отключается. General scene-object/child lifecycle, pools, physics/nav/ship bridges и actual late join/reconnect ещё не завершены. Scene/prefab/profile activation и world shift не выполнялись; полный T-FO04/jitter fix не заявляются.
**Коммит:** один коммит кода/meta/результатов/документации, без собственного хеша отдельным коммитом. Исторические E/F artifacts сохраняются; Temp runner и несвязанный LiberationSans fallback не включаются.

---

## Итерация от 2026-09-09 (T-FO04F)

**Задача:** Подключить limited custom parent/unparent placement к новым global baselines, не активируя неподготовленные игровые объекты.
**Результат:** GlobalMotionHierarchy pure policy; server candidate preflight до InstallServerControl; parent resolve по session/object/spawn + same frame/scene + ready/no-cycle; SetParent(false) и явная pose только при новом binding до cache callbacks/ACK. World detach использует GlobalPosition, а не старый local Vector3. Same-binding drift не исправляется reparent на каждом sample.
**Guards:** Rigidbody/joints, произвольные colliders/2D, enabled NavMeshAgent и cross-scene/frame changes блокируются. Root CC допускается при unit-scale parent с восстановлением enabled. Не переписываются velocities/HP/docking/Animator; native hierarchy/physics rollback отсутствует. Ошибка после записи → Faulted/revoke/Stop при живой сети, требуется внешний recovery. Global protocol поднят до 0xF002, F000–F0FF reserved; legacy=0 не меняется.
**Изменения:** GlobalMotionHierarchy.cs, Editor ValidateGlobalMotionHierarchy.cs и два Unity-generated meta; GlobalMotionPoseAdapter, GlobalMotionReplicator, GlobalMotionWorld, GlobalMotionNetworkContract, NetworkManagerController; F report/validation JSON, roadmap и журнал.
**Проверки:** после исправления CS1729 только в новом validator compile PASS (pose получается через public buffer без расширения production API). Финальный source review пройден. Реально выполнены **40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 234 pure PASS / 0 FAIL**. Повторный asset guard: 58 candidates, opt-in=0/profile assets=0/loaded enabled profiles и adapters=0. Play Mode, GameObject-тесты, native physics/network и screenshots не запускались; runtime UNTESTED.
**Границы:** сцены/префабы/профиль не изменялись, world shift выключен. NPC/crew/player gameplay parenting пока не перенаправлен; concrete spawn/bootstrap/initial placement, child lifecycle и native bridges T-FO05–08 ещё обязательны. Полный T-FO04 и jitter fix не заявляются.
**Коммит:** один коммит кода/meta/документации, без отдельного собственного хеша. E artifacts, Temp runner и несвязанный LiberationSans fallback не изменяются/не включаются.

---

## Итерация от 2026-09-09 (T-FO04E)

**Задача:** Завершить dormant startup/layout/spawn/parent contracts без активации неполной координатной миграции.
**Результат:** Канонический SHA256 prefab catalog с выбранным PlayerPrefab и порядком NB, strict 72-byte hello; opt-in profile class (asset не создан), read-only metadata preflight, per-manager approval gate и IGlobalMotionSpawnBootstrap. Startup не перезаписывает native hash/config поля, чужой callback или непустой payload. Approval не создаёт legacy player автоматически; concrete global placement/spawn/parenting ещё не реализованы.
**Guards:** NetworkManagerController проверяет контракт до всех трёх Start; adapter Bind требует startup gate и отключённые native spawn/parent flags; global player не добавляет combat NB поздно; legacy ScenePlacedObjectSpawner уступает активной global session provider. При unassigned/disabled profile сохраняется legacy ветка. ValidateNetworkStart — read-only контракт уже подготовленного мира, не native freeze/loader.
**Изменения:** четыре новых runtime source GlobalMotionNetworkContract/Profile/PrefabInspector/NetworkStartup; два новых Editor source ValidateGlobalMotionNetworkContracts и AuditGlobalMotionPrefabContracts; шесть Unity-generated meta; четыре существующих source (NetworkManagerController, NetworkPlayer, GlobalMotionPoseAdapter, ScenePlacedObjectSpawner); `04E_NETWORK_STARTUP_CONTRACTS.md`, два generated JSON, roadmap и этот журнал.
**Проверки:** compile PASS; реально выполнены **48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 194 pure PASS / 0 FAIL**. Audit: один prefab list, 58 candidate prefabs, opt-in candidates=0, profile assets=0, loaded enabled profiles/adapters=0. Проверки не создавали тестовых GameObject и не выполняли Play Mode/physics/network/screenshots. Live callbacks, runtime/gameplay и performance UNTESTED.
**Границы:** текущие scenes/prefabs не редактировались, world shift выключен; scene digest — заявленный будущий manifest, не доказанная полнота сцен. Concrete spawn/parent/native bridges и T-FO05–08 ещё обязательны. T-FO04 в целом не завершён; jitter fixed не заявляется.
**Коммит:** один коммит кода, meta, audit и документации; временный Temp runner и несвязанный LiberationSans fallback не включать. Собственный хеш отдельным коммитом не фиксируется.

---

## Итерация от 2026-09-09 (T-FO04D)

**Задача:** Подключить dormant coordinate readiness/cache hooks к игровым контроллерам, не активируя неполную миграцию.
**Результат:** `GlobalMotionActorContract.cs` (interface/state/link), serialized opt-in=false, two-stage baseline placed → actor ready → publication acknowledgement. Gates в NetworkPlayer Update/Fixed/Move, ShipController input/physics, NpcBrain, NpcSocialBrain.Tick, NpcShipController.NavTick, SkillInputService и SkillAnimationPlayer. Baseline обновляет пространственные кэши, но не velocity/HP/docking/aggro. Старые Vector3 player teleports/restore/correction блокируются только в global mode.
**Native граница:** Ship/NPC требуют внешней подготовки body/nav и exact-binding Confirm API. Сам gate НЕ замораживает Rigidbody/NavMeshAgent; native bridge ещё не реализован. SetInputEnabled/ExitDocked/ForceSurrender не используются как coordinate pause. Initial placement и полная активация префабов остаются впереди.
**Изменения:** новый contract + Editor `ValidateGlobalMotionActorReadiness.cs` и два Unity-generated meta; `GlobalMotionPoseAdapter.cs`, `GlobalMotionWorld.cs`; семь игровых исходников из `04D_ACTOR_READINESS.md`; отчёт, roadmap и этот журнал.
**Проверки:** compile PASS; реально выполнены **26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 146 PASS / 0 FAIL**. Только pure/state/compiled-contract checks. Play Mode, GameObject-тесты, physics simulation, сеть и screenshots не запускались; native/gameplay результат не заявляется.
**Границы:** сцены/префабы не менялись, opt-in нигде не включён; legacy gates проходят исходное поведение. Animator не отключается; stale cast cancellation касается opt-in teleport/handoff, не будущего rebase. Тикет T-FO04 в целом не завершён. Следующий T-FO04E — согласованные spawn/parent/prefab contracts; world shift выключен.
**Коммит:** один коммит кода, meta и документации; без отдельной фиксации хеша. Несвязанный LiberationSans fallback оставлен вне этапа.

---

## Итерация от 2026-09-09 (T-FO04C)

**Задача:** Добавить session/frame coordinator и безопасно ограниченный Unity pose adapter поверх global motion transport, не включая неподготовленную игру.
**Результат:** GlobalMotionWorld с одним issuer на живую NGO-сессию и immutable PhysicsScene frame registrations; GlobalMotionPoseAdapter с explicit Bind, baseline-before-publish, role separation, parent-chain readiness, synchronous pooled despawn и guarded Transform/CC/kinematic Rigidbody writes. Authority не получает обычные echo poses; remote dynamic body, активный nav teleport, вложенная физика/joints и интерполируемый parent render pose для server replica блокируются. В transport добавлены revoke acknowledgement и безопасный sequence resume.
**Изменения:** `GlobalMotionApplication.cs`, `GlobalMotionWorld.cs`, `GlobalMotionPoseAdapter.cs`, `GlobalMotionReplicator.cs`; Editor `ValidateGlobalMotionApplication.cs`; четыре Unity-generated meta; `docs/world/floatingorigin/04C_FRAME_POSE_ADAPTER.md`, roadmap и эта запись.
**Проверки:** compile PASS; фактически вызваны Run(): **32 application + 32 transport + 33 protocol + 23 foundation = 120 PASS / 0 FAIL**. Это чистые Edit Mode проверки, не native callbacks/physics/runtime tests. Play Mode, GameObject-тесты, screenshots и реальные сетевые сессии не выполнялись.
**Lifecycle:** disable намеренно unbind/stop; повторное включение требует explicit Bind/reactivation. Coordinator сохраняет issuer при disable/re-enable внутри той же NGO-сессии; реальный shutdown его очищает. Despawn cleanup не отправляет RPC.
**Границы:** компоненты не установлены на игровые префабы/сцены; gameplay controllers пока не читают IsReadyForSimulation. NT, Animator/root-motion, RPC/persistence и world shift не подключались/не менялись. Следующий T-FO04D — dormant actor readiness/lifecycle/cache hooks и согласованная spawn/parent/prefab подготовка. Полный T-FO04 не завершён, jitter fixed не заявляется.
**Коммит:** код, meta и документация вместе; без отдельного коммита хеша. Несвязанный LiberationSans fallback не включать.

---

## Итерация от 2026-09-09 (T-FO04B)

**Задача:** Связать global motion protocol с NGO lifecycle/control/motion без преждевременного подключения к игре.
**Результат:** Добавлены GlobalMotionSession, GlobalMotionControl/Receiver, GlobalMotionAdmission и настоящий NetworkBehaviour GlobalMotionReplicator. Reliable control/initial sync/keyframes, unreliable owner→server→clients motion, sender+owner+epoch/time/rate проверки, baseline acknowledgement, stop/rebind при ownership/parent изменениях и Tick cleanup. Компонент не пишет Transform и не установлен на существующие префабы.
**Изменения:** четыре новых source в `Assets/_Project/Scripts/World/FloatingOrigin/Network/` + Unity-generated meta; Editor `ValidateGlobalMotionTransport.cs` + meta; `docs/world/floatingorigin/04B_NGO_TRANSPORT.md`, roadmap и эта запись.
**Проверки:** compile PASS; реально выполнены **32 новых + 33 protocol + 23 foundation = 88 PASS / 0 FAIL** в Edit Mode. Control payload 3/144/132 байта. Реальные RPC-сессии, Host/clients, gameplay, physics и screenshots не запускались.
**Границы:** старый NT, scene/prefab layout, gameplay RPC и сохранения не менялись. Следующий T-FO04C — frame/pose adapter и session coordinator. World shift выключен; пользователю игровой тест пока не требуется.
**Коммит:** код и документация вместе, без отдельного коммита хеша; несвязанный LiberationSans fallback не включать.

---

## Итерация от 2026-09-09 (T-FO04A)

**Задача:** Продолжить floating-origin реализацию: независимый от origin сетевой формат и receive/interpolation state machine без замены работающих компонентов.
**Результат:** Добавлены MotionStreamBinding, GlobalMotionSnapshot, GlobalMotionPose, GlobalMotionBuffer в `Assets/_Project/Scripts/World/FloatingOrigin/Network/`. Разделены World/ParentLocal, session/spawn/authority/discontinuity/parent generations; atomic decode, strict binding, stale/reordered packet rejection, bounded interpolation без хранения client origin.
**Изменения:** четыре runtime source + Unity-generated meta; `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionProtocol.cs` + meta; `docs/world/floatingorigin/04A_NETWORK_PROTOCOL_AND_BUFFER.md`, статус roadmap и эта запись.
**Проверки:** compile PASS после исправления CS0165 в новом validator; реально выполнены **33 motion + 23 foundation = 56 PASS, 0 FAIL** вне Play Mode. NGO payload world=123, parent-local=111 байт. Игровые соединения, physics и screenshots не запускались.
**Ограничения:** это готовый codec/buffer, но НЕ подключённый NGO transport. T-FO03 semantic/closed-scene gate остаётся открыт. Existing NT, gameplay RPC, saves, prefab layout и scene transforms не менялись. Следующий T-FO04B — lifecycle/control/motion integration. Пользователю игровые тесты пока не нужны.
**Коммит:** код, отчёт и журнал вместе, без дополнительного коммита хеша; несвязанный LiberationSans fallback не включать.

---

## Итерация от 2026-09-09 (T-FO03 — census)

**Задача:** Сделать воспроизводимую инвентаризацию пространственных зависимостей до runtime миграции.
**Результат:** `FloatingOriginMigrationCensus.cs` прочитал 639 C#, 48 shader-файлов, 75 prefab assets и открытую Bootstrap; 2033 source candidates, 1391 prefab records, 197 scene records, errors=0. Найдены 50 prefab NetworkTransform и дополнительные Pickup/Drop RPC за пределами Scripts/. Классифицированы 16 RPC-кандидатов и поля docking/NPC DTO. Полный semantic/closed-scene gate остаётся открытым.
**Изменения:** Editor scanner + Unity-generated meta; четыре generated census отчёта и `03_BOUNDARY_REVIEW.md` в `docs/world/floatingorigin`; статус roadmap. Дополнительно finite guard extreme rebase translation в OriginRebasePlan и regression test.
**Проверки:** compile PASS; foundation **23 PASS / 0 FAIL**; census errors=[]. Unity fake-null ошибка первого запуска scanner исправлена, повторный запуск успешен. Play Mode/сеть/физика/скриншоты не запускались. Работающие gameplay consumers не менялись, world shift не включён.
**Ограничения:** 2033 совпадения — кандидаты, не список доказанных багов. Missing scripts исходных assets отмечены, но не изменялись. Bootstrap наблюдалась dirty, не сохранялась. Один коммит кода/результатов/документации без отдельного хеша.

---

## Итерация от 2026-09-09 (T-FO02)

**Задача:** Реализовать проверяемое global-double/local-frame ядро без включения частично мигрированного мира.
**Результат:** Добавлены GlobalPosition, LocalCoordinateFrame, GlobalGridCoordinates и OriginRebasePlan в `Assets/_Project/Scripts/World/FloatingOrigin/`; Editor validator — `Assets/_Project/Editor/FloatingOrigin/ValidateFloatingOriginFoundation.cs`.
**Проверки:** Unity compile PASS; фактический вызов Run() вне Play Mode: **22 PASS, 0 FAIL**. Проверены точность на 10^9 м, независимые frames, границы сетки, repeated rebases, finite/overflow guards, NGO double serialization и JSON. Это НЕ runtime/visual/network-session PASS.
**Документация:** `docs/world/floatingorigin/02_FOUNDATION_IMPLEMENTATION.md`. Игровые consumers, сцены, префабы и сохранения не изменялись; floating origin не включён. Полная миграция продолжается отдельными этапами.
**Коммит:** код, Unity-generated meta, отчёт и эта запись вместе; отдельный коммит хеша не создаётся.

---

## Итерация от 2026-09-09 (T-FO01)

**Задача:** Начать полноценную floating-origin миграцию MMO Host + Clients после отрицательного T-JITTER18.
**Результат:** Подтверждены Unity 6000.5.2f1 / NGO 2.13.0, прочитан baseline и исходник NetworkTransform. Зафиксированы global-double/local-frame архитектура, независимые клиентские origins, необходимость серверных регионов и карта позиционных RPC/кэшей. Старые origin-компоненты не включались.
**Изменения:** `docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md`, `01_AUDIT_AND_SOURCES.md`, эта запись.
**Проверки:** READ-ONLY осмотр редактора и исходников; нет runtime PASS. Аудит ещё не является полной семантической проверкой всех механик. Play Mode/билд/скриншоты только пользователя.
**Порядок:** Один коммит кода/документации на этап, без последующего коммита хеша. Baseline `53c86fe2`; чужой `LiberationSans SDF - Fallback.asset` не включать. Следующий T-FO02 — изолированное ядро, не включение world shift.

---

## Итерация от 2026-09-08 (T-JITTER18)

**Задача:** Подключить character-visual эксперимент к настоящей игре/билду вместо ограниченного стенда и проверить его на реальной проблемной дистанции.
**Результат:** Near-origin skinning копии текущей локальной позы был подключён к реальному `NetworkPlayer`. В пользовательском Play Mode персонаж был проверен на координатах примерно `(39877.62, 2502.17, 40026.44)` — около 56,5 км от Unity-origin. Персонажа продолжает трясти. Эксперимент **не устраняет микроджиттер**. Кодовый эксперимент откатан к baseline `57572dc006381f9523ed8d3c7d6a68e2baf7551f`; актуальная документация сохранена.
**Изменения:**
- Временно добавленные/изменённые файлы T-JITTER18 удалены или восстановлены к baseline: `CharacterLocalSkinningExperiment.cs`, `NetworkPlayer.cs`, `EdgeDetectionRenderFeature.cs`.
- `docs/Character/IMPLEMENTATION_CHARACTER_LOCAL_SKINNING_EXPERIMENT.md` — зафиксированы runtime FAIL и выполненный откат.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` — зафиксирован отрицательный результат D1/D2 и остановка эксперимента.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — актуальный статус обновлён, исторические измерения сохранены.
- `Assets/_Project/Docs/ITERATIONS.md` — эта запись.
**Проверки:** Unity compile до пользовательского прогона был PASS («No compile errors»). Runtime-проверка выполнена в реальной игре на проблемной дальней позиции; активация эксперимента подтверждалась, но визуальный джиттер сохранился. Итог теста: **FAIL**. Скриншоты/видео пользователем не предоставлялись; дополнительные регрессионные проверки после отрицательного результата не выполнялись.
**Сравнение:** проверка проводилась на штатном пути с включённым T-JITTER18; валидация показала, что локализация skinning через proxy не меняет наблюдаемый симптом на дальней позиции. Аргумент `-disableCharacterLocalSkinning` после решения об откате больше не нужен для сохранения текущего baseline.
**Откат:** код восстановлен из `57572dc006381f9523ed8d3c7d6a68e2baf7551f` без `reset --hard` и без `rebase`. Несвязанные изменения пользователя сохранены и не включаются в этап.

---

## Итерация от 2026-09-08 (T-JITTER17)

**Задача:** Задокументировать актуальную архитектуру без FloatingOrigin, проверить готовые anti-jitter решения и ограничить область исправления персонажем.
**Результат:** Docs-only этап D0 завершён. Исследованы Origin Shift, Floating Origin Network, World Streamer 2 и SAM. Готовый plug-and-play фикс для Unity 6000.5.2f1 / URP 17.5 / NGO 2.13 и нашего Humanoid не подтверждён. Масштабная миграция координат мира отложена; предложен один изолированный visual-only эксперимент A/B/C с явными условиями PASS/STOP. Эксперимент не реализован и не запускался.
**Изменения:**
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` — новый анализ, первоисточники, оценка пакетов и ограниченный порядок следующих этапов.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — ссылка на T-JITTER17 и отметка, что исторический §9.5 не является текущим планом внедрения; результаты прошлых тестов сохранены.
- `Assets/_Project/Docs/ITERATIONS.md` — запись этого этапа в существующем журнале T-JITTER; новый журнал не создавался.
**Проверки:** только документация; код, сцены, префабы, настройки и сохранения не изменялись. Пакеты не покупались/не импортировались. Play Mode и скриншоты не выполнялись; исправление симптома не заявляется. Перед docs-коммитом проверяется точный состав diff и `git diff --check`.
**Следующий этап:** только после согласования — отдельный character-visual стенд, затем пользовательский прогон; не автоматическая перестройка MMO.

---

## Итерация от 2026-08-17

**Задача:** Проверить гипотезу `skinnedMotionVectors` после body-swap персонажа (T-JITTER15)  
**Коммит:** `3b74ab5a8c5fd5312ad4d72b0fb69fe6f9931047` — T-JITTER15: диагностический фикс откатан после runtime-проверки  
**Результат:** Гипотеза не подтверждена — после отключения `skinnedMotionVectors` тряска сохранилась. Кодовый фикс откатан; запись оставлена как отрицательный результат теста.
**Изменения:**
- `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` — диагностическое изменение удалено
- Следующий шаг — runtime-зонд вершин через `SkinnedMeshRenderer.BakeMesh()`

---

## Итерация от 2026-08-17 (T-JITTER16)

**Задача:** Измерить baked-вершины персонажа в рантайме на origin и в WorldScene_0_0  
**Коммит:** `2f5191ae8917065dd421b7ceb8ca893f8c035644` — T-JITTER16: runtime vertex probe и последующая очистка  
**Результат:** Подтверждено: на `distOrigin≈56493м` local baked-vertex deltas вырастают примерно в 3–5 раз относительно origin. `local` и `relative-world` совпадают, значит источник до камеры и `NetworkTransform` — в humanoid deformation/skinning-пути при больших абсолютных координатах.
**Изменения:**
- Создан и после теста удалён временный `SkinnedVertexRuntimeProbe`.
- Удалены временные `BoneJitterRuntimeProbe`, `JitterClipProbe`, `InvestigateAnimator`.
- Компонент `ProjectC.DebugTools.SkinnedVertexRuntimeProbe` удалён из `NetworkPlayer.prefab`.
- T-JITTER15 (`skinnedMotionVectors=false` после body-swap) откатан: симптом не изменился.
- Compile check после очистки: ошибок нет.

**Историческое архитектурное решение (отложено в T-JITTER17, 2026-09-08):** перейти к local-coordinate слою для MMO: `SceneID/ChunkID + localPosition`, чтобы humanoid и физика работали рядом с Unity origin; глобальные координаты не хранить в одном float `Transform.position`. Это больше не обязательный следующий шаг устранения текущего визуального бага: сначала ограниченный character-visual эксперимент, см. запись T-JITTER17 выше.

---

## Итерация от 2026-07-14

**Задача:** Исправить баг: при перезаходе теряется доступ к кораблю (ключ в инвентаре, но корабль заблокирован)  
**Коммит:** `4b95e65` — T-KEY-FIX: persistentShipId для KeyRodInstance — фикс потери доступа к кораблю между сессиями  
**Изменения:**
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` — добавлено поле `persistentShipId`
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` — `persistentShipId` в DTO и SaveAll
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` — индекс `_instancesByPersistentId`, rebind `registeredShipId` при спавне, очистка stale-инстансов
- `Assets/_Project/Scripts/Player/ShipController.cs` — `CreateKeyInstanceWhenReady` передаёт `ShipPersistentId`

**Корень бага:** `NetworkObjectId` нестабилен между сессиями → дубликаты `KeyRodInstance` → проверка владения находила новый instance с `owner=NONE`

---

## Итерация от 2026-07 (v2)
=======


**Задача:** Исправить микротряску персонажа при standing  
**Коммит:** `3866c59` — T-JITTER01-v2: корневая причина — NetworkTransform.Interpolate конфликтует с CharacterController.Move/NavMeshAgent  
**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` для owner; `using Unity.Netcode.Components`; фильтрация sleeping Rigidbody + delta threshold в platform carry
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` на хосте; `using Unity.Netcode.Components`
- `Assets/_Project/Docs/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — полный v2-диагноз
- `Assets/_Editor/InvestigateAnimator.cs` — diagnostic tool (создан)

**Стратегия отката:** `git revert 3866c59`

## Итерация от 2026-08-14

**Задача:** Вынести номер версии в главном меню в поле инспектора (слова локализованы, цифры подставляются)
**Коммит:** `731db58` — T-UI03: версия в главном меню вынесена в поле инспектора
**Изменения:**
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — добавлено поле `versionText` (секция Version), подпись через `Loc.BindFormat`
- `Assets/_Project/Scripts/Localization/Loc.cs` — добавлен `BindFormat` + `FormatWithFallback`
- `Assets/_Project/Settings/Localization/UI_Table_ru.asset` — `ui.main_menu.subtitle` → `Версия Alpha {0}`
- `Assets/_Project/Settings/Localization/UI_Table_en.asset` — `ui.main_menu.subtitle` → `Alpha {0}`
- `Assets/_Project/Editor/Localization/AddMainMenuLocKeys.cs` — seed обновлён на `{0}`

---

## Итерация от 2026-08-15

**Задача:** Исправить persistence экипировки персонажа и убрать отладочную выдачу одежды при подключении  
**Коммит:** `aff555584b6a13ccce2d319a6a205b284b6efd9b` — T-EQP01: исправить persistence экипировки и убрать debug seed  
**Изменения:**
- `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` — удалена hardcoded seed-выдача тестовых предметов; добавлена загрузка экипировки при подключении; equip/unequip теперь сохраняются сразу
- `Assets/_Project/Scripts/Stats/StatsServer.cs` — добавлена загрузка equipment из общего character persistence независимо от порядка спавна серверных объектов

---

## Итерация от 2026-08-16

**Задача:** Добавить в MainMenu debug-блок для удаления отдельных состояний persistence и всех игровых сохранений  
**Коммит:** `1a354294c7c0a1810180a5cb6c3d1fe752259dbc` — T-UI04: debug-очистка persistence в MainMenu  
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — добавлен блок Debug слева сверху с кнопками очистки состояний
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — добавлены стили debug-панели
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — подключены обработчики кнопок
- `Assets/_Project/Scripts/UI/MainMenu/PersistenceDebugTools.cs` — удаление JSON/TXT persistence и trade PlayerPrefs с сохранением настроек и input bindings

