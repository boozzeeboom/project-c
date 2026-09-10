# T-FO06L — global static-world pilot: текущий этап интеграции

Дата: 2026-09-09. Предыдущий этап: T-FO06K.

Актуальный статус на 2026-09-10 — раздел 15: пользовательский runtime отклонил aggregate-parent initial frame bridge. Он вызвал ошибки Netcode re-parent до `NetworkManager.IsListening`, карта WorldScene_0_0 не появилась под player, и player продолжал падать. Код bridge и Bootstrap wiring откатаны; предыдущий работающий Host/player startup и menu handoff сохранены. Jitter не измерялся, полноценный rebase остаётся BLOCKED до пообъектной классификации и transaction design.

## 1. Позиция в общем плане

T-FO06L — ограниченный подготовительный пилот внутри этапа T-FO06 «Контент и client rebase». Он не закрывает T-FO06 целиком, T-FO04 или T-FO09. Цель этапа — подготовить один global player prefab, статичный reviewed scope из `BootstrapScene` и `WorldScene_0_0`, явный scene catalog и native admission до запуска NGO.

Игровые Play Mode-прогоны, screenshots и приёмочные Host/client-тесты выполняет пользователь. В этом отчёте runtime-gate отмечается как UNVERIFIED/BLOCKED, а не как пройденный.

## 2. Что реализовано в текущем этапе

- Добавлен явный `GlobalSceneTreatment.Unmanaged`. Такой источник обязан быть в каталоге и иметь `reviewNote`, но global executor не изменяет его authored state, размещение, активацию, spawn или retirement.
- Обновлены compiler, policy, lifecycle/executor seams и protocol version: `0xF005` → `0xF006`.
- Создан reviewed catalog для двух сцен:
  - `Assets/_Project/Scenes/BootstrapScene.unity`;
  - `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.
- Каталог содержит `150` observations: `61` для BootstrapScene и `89` для WorldScene_0_0. Все entries имеют `Unmanaged`, `spatial=false`, `poseKind=None` и непустой review note.
- Зафиксирован digest каталога:
  `bfe8008aa885a818b05f799799c504a1b174f87fdc0d73df2043a3ab91d93199`.
- Созданы профиль global-пилота, isolated `NetworkPlayer_GlobalPilot` wiring и pilot prefab registry.
- В BootstrapScene подключены `GlobalMotionWorld`, `GlobalSceneNativeExecutor`, `GlobalMotionPlayerBootstrap`, `GlobalMotionPilotSpawnSource` и `GlobalMotionPilotRuntime`.
- В pilot source задан authored `Respawn_Default`; старый `GroundPlane_0_0` не восстанавливался.
- В `GlobalMotionNetworkStartup` добавлена временная in-memory подмена global PlayerPrefab с восстановлением legacy PlayerPrefab при release/failure.
- Runtime roots без baked catalog marker и без marked NetworkObject descendants исключаются из authored candidate scope как внешняя runtime-инфраструктура.

## 3. Подтвержденные проверки

- Unity compile: ранее подтверждено `No compile errors`.
- Pure execution validator: ранее подтверждено `32 passed / 0 failed` после добавления Unmanaged/protocol checks.
- В актуальном Edit Mode snapshot обе пилотные сцены загружены.
- В актуальном snapshot обнаружены `150` уникальных live `GlobalSceneSourceMarker` IDs, то есть отсутствие marker в самом catalog/live snapshot не подтверждается.
- Тестовая запись `336a190646b19bc46b22dd4e78f99800:1044316355:0` присутствует в каталоге и в live snapshot как корневой `[QuestTracker]`.

Эти проверки не являются доказательством успешного native runtime startup, NGO spawn, grounding или gameplay.

## 4. Текущий runtime-блокер

Global host preparation остановилась до штатного запуска/спавна игрока с ошибкой:

```text
[T-FO06L] Pilot global startup refused:
scene_preparation:catalog_source_not_bound:336a190646b19bc46b22dd4e78f99800:1044316355:0
```

Следствие: `NetworkPlayer_GlobalPilot` не появляется не потому, что уже доказан дефект его CharacterController или камеры, а потому что player bootstrap является downstream от native scene preparation. До успешного `PrepareBeforeNetworkStart` запуск global session и выдача player spawn plan не должны считаться выполненными.

Предыдущий executor-блокер `missing_duplicate_wrong_scene_baked_source_marker:SkillTreeWindow` устранён фильтрацией внешних runtime roots. После этого осталась проблема binding между catalog entries и executor candidate set.

## 5. Техническая гипотеза для следующего шага

`GlobalSceneNativeExecutor.BuildPreparation()` сейчас добавляет в `candidates`:

1. reviewed scene roots;
2. `NetworkObject` descendants этих roots.

При этом catalog audit/marker snapshot видит все `GlobalSceneSourceMarker`, включая marked descendants, которые сами не являются root и не являются `NetworkObject`. Это объясняет расхождение между полным live marker set и меньшим executor candidate set и должно быть исправлено в binder до следующего runtime gate.

Следующий кодовый шаг — расширить candidate enumeration до всех marked descendants внутри reviewed roots, сохранив:

- пропуск внешних runtime roots без marker;
- duplicate/wrong-scene/parent identity guards;
- обязательную binding-проверку всех 150 записей;
- запрет молчаливого пропуска loaded-scene entries;
- отдельную диагностику количества catalog entries, live markers и bound candidates.

Это ещё не выполнено в T-FO06L и не должно маскироваться разрешением пропуска для `Unmanaged` entries: обе pilot-сцены загружаются, поэтому каталог должен быть полностью связан с их live authored sources.

## 6. Где мы находимся по итерациям

- T-FO06A–T-FO06K: завершены предыдущие подготовительные и scene/catalog этапы согласно `00_ARCHITECTURE_AND_PLAN.md` и `Assets/_Project/Docs/ITERATIONS.md`.
- T-FO06L: **частично выполнен** — контракт `Unmanaged`, каталог, профиль, markers, executor wiring и in-memory PlayerPrefab swap подготовлены; native runtime admission **BLOCKED** на `catalog_source_not_bound`.
- Полный T-FO06 не завершён.
- T-FO04 полный network/spatial replication gate не завершён.
- T-FO07/T-FO08 (physics regions, NavMesh, AI, ships и прочие spatial actors) не входят в текущий pilot fix.
- `GroundPlane_0_0` остаётся намеренно удалённым и не восстанавливается без отдельного решения пользователя.

## 7. Следующий этап

1. Исправить executor candidate binding и добавить чистую runtime-диагностику `catalog=150 / markers=150 / bound=150`.
2. Проверить только compile и чистые Edit Mode contracts после изменения.
3. Передать пользователю сборку для ручного Host/client Play Mode-прогона. Сам Play Mode и screenshots на этом этапе не запускать автоматически.
4. Только после пользовательского подтверждения startup/spawn отдельно решать grounding/опору, движение, камеру, client admission и release/restore legacy config.
5. Persistence/auth production path, checkpoint store и session coordinator не считать частью базового static-world pilot acceptance.

## 8. Состав текущего этапа

К этапу относятся изменения в pilot catalog/profile/registry, BootstrapScene и WorldScene_0_0 wiring, `Unmanaged` contract/compiler/policy/executor, protocol `0xF006`, pilot spawn source/runtime и execution validator. Независимое изменение `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` в этап не входит.

## 9. Обновление после продолжения этапа — 2026-09-10

`GroundPlane_0_0` подтверждённо оставлен удалённым и не восстанавливается. Каталог и digest не пересобираются из-за земли.

Исправлен `GlobalSceneNativeExecutor.BuildPreparation()`:

- candidate enumeration теперь добавляет все `GlobalSceneSourceMarker` внутри reviewed roots;
- marked descendants без `NetworkObject` больше не пропускаются;
- сохранены проверки duplicate, wrong-scene, parent identity и внешних runtime roots;
- добавлена обязательная проверка `catalog / markers / bound`;
- ошибка `catalog_source_not_bound` теперь содержит все три счётчика.

Проверки после исправления:

- Compile: `No compile errors`;
- `Validate Native Scene Execution Contracts`: `32 pure checks passed`;
- фактический Play Mode/native preparation/Host/client startup ещё не запускались.

Следующий gate — пользовательский ручной Host/client Play Mode-прогон текущего pilot scope. Если startup снова остановится, диагностика должна содержать значения `catalog=...;markers=...;bound=...`; повторную правку выполнять только по этой фактической причине.

## 10. Обновление после первого ручного Play Mode запуска — 2026-09-10

Фактический лог показал новую причину остановки до Bootstrap:

```text
[T-FO06L] Pilot global startup refused: pilot_frame_not_prepared
```

На момент `GlobalMotionPilotRuntime.Start()` была загружена только `BootstrapScene`; `WorldScene_0_0` отсутствовала среди loaded scenes. Поэтому `GlobalMotionPilotSpawnSource.Awake()` не нашёл `Respawn_Default`, оставил `_prepared = false`, и validation остановила запуск до native catalog binding.

Исправлено:

- `GlobalMotionPilotRuntime` теперь перед global `TryPrepare` дожидается/загружает `WorldScene_0_0` additive;
- после загрузки вызывается повторная подготовка `GlobalMotionPilotSpawnSource`;
- host запускается только после подтверждения loaded world scene и prepared frame;
- уже загруженная `WorldScene_0_0` повторно не загружается;
- `GroundPlane_0_0` не восстанавливается и catalog/digest не изменяются.

Проверка после исправления: Compile — `No compile errors`. Повторный Play Mode gate ожидает пользовательского запуска.

## 11. Обновление после повторного runtime gate — 2026-09-10

Повторный Play Mode gate дошёл до native preparation, но остановился на:

```text
[T-FO06L] Pilot global startup refused: scene_preparation:catalog_markers_bound_mismatch:catalog=150;markers=134;bound=134
```

Read-only проверка установила фактическую причину расхождения:

- на диске обе сцены содержат все `150` сериализованных `GlobalSceneSourceMarker` (`BootstrapScene=61`, `WorldScene_0_0=89`);
- во время Play Mode `16` Bootstrap-маркеров уже находятся в сцене `DontDestroyOnLoad`;
- это persistent Bootstrap infrastructure: `NetworkManager`/`Runtime`, client-state и UI/service roots;
- executor намеренно сканирует только загруженные catalog scenes и не принимает DDOL relocation как подмену authored scene identity.

Исправлено в `GlobalMotionPilotRuntime`:

- перед `RefreshPreparedContent()` пилот находит cataloged Bootstrap markers в `DontDestroyOnLoad`;
- переносит их корневые GameObject обратно в загруженную `BootstrapScene`;
- не меняет catalog, digest, `GlobalSceneNativeExecutor` или `Unmanaged` closed-world binding;
- не восстанавливает `GroundPlane_0_0` и не затрагивает независимый TMP fallback asset.

Проверки после исправления:

- Compile: `No compile errors`;
- Play Mode после этого исправления пользователем ещё не выполнен;
- Host/client, player spawn, physics и screenshots остаются `UNVERIFIED`.

Следующий gate — пользовательский Play Mode-прогон с ожиданием диагностики `catalog=150;markers=150;bound=150` либо следующей фактической причины отказа. Executor нельзя ослаблять до partial binding.

## 12. Обновление после проверки legacy scene ownership — 2026-09-10

Повторный compile/runtime анализ подтвердил отдельный startup-блокер:

```text
legacy_scene_loader_must_be_retired_by_content_bridge
```

Исправлено в `GlobalMotionPilotRuntime`:

- после восстановления cataloged Bootstrap roots pilot вызывает `RetireLegacySceneLoadersForPilot()`;
- метод находит активные `ProjectC.World.Scene.ClientSceneLoader` и отключает только их компоненты;
- `Runtime`, `NetworkManager`, scene catalog, digest и legacy loader-код не удаляются и не изменяются для обычного режима;
- global pilot остаётся единственным владельцем additive-загрузки `WorldScene_0_0` в своём startup scope.

Проверки после исправления:

- Compile: `No compile errors`;
- ручной Play Mode, Host/client, native preparation, player spawn, physics и screenshots ещё не выполнялись;
- независимый concave `MeshCollider` на `CABIN_ENTRY_DOOR_PIVOT` не изменялся.

Следующий gate — пользовательский Play Mode-запуск с проверкой исчезновения `legacy_scene_loader_must_be_retired_by_content_bridge` и перехода к следующей фактической причине, если она существует. Не ослаблять validation и не завершать весь T-FO04–T-FO09 до этого runtime feedback.

## 13. Повторная загрузка legacy-сцен после Start Host — 2026-09-10

Пользователь подтвердил повторение `Startup lease or initial scene set changed; streaming bridge required` в `GlobalSceneNativeExecutor.Advance()` после Start Host. Предыдущая попытка сравнивать только пути/количество загруженных сцен не устранила проблему. Объяснение через DontDestroyOnLoad не было подтверждено и не считается установленной причиной.

### Фактические свидетельства

В текущем `Logs/Editor.log` сохранён пользовательский запуск:

- строка 34854: первая загрузка `Assets/_Project/Scenes/World/WorldScene_0_0.unity`;
- строка 35968: `[NMC] HandleClientConnected: clientId=0, IsServer=True, IsClient=True`;
- строка 37879: повторная загрузка того же `WorldScene_0_0`;
- строки 37891–37892: отказ executor в `Advance:258`;
- строки 37963–37975: завершения загрузок `WorldScene_0_1`, `WorldScene_1_0`, `WorldScene_1_1`.

Номера относятся к исследованному логу, который впоследствии может быть заменён. Сам log-файл не включается в коммит.

Цепочка в коде до исправления: `NetworkManagerController.HandleClientConnected` вызывает `OnPlayerConnected`; подписанный в `Start()` `ClientSceneLoader.OnPlayerConnected` вызывает `OnClientConnectedCallback`, который запускает `AutoLoadInitialSceneCoroutine`; спустя 0.5 секунды Host вызывает `LoadSceneWithNeighborsCoroutine(0,0)`. Его внутренний `_loadedScenes` не знает о world scene, загруженной пилотом напрямую, поэтому legacy-путь повторно загружает центр и соседей. `loader.enabled=false` не снимал C#-подписку и не останавливал корутины. Новый сценовый экземпляр — реальное нарушение initial scope, а не повод разрешать неизвестные scenes.

В момент расследования редактор уже был в Edit Mode. Состояние конкретной startup-сессии в момент отказа не удалось прочитать; старое объединённое сообщение не различало lease/singleton/scenes. Подмена manager или потеря lease этим логом не доказаны.

### Исправление

- `ClientSceneLoader.TryRetireForGlobalPilot(out error)` — явный pilot-only handoff. Снимается подписка с сохранённого экземпляра `NetworkManagerController`, вызывается `StopAllCoroutines`, устанавливается runtime retirement-флаг и отключается компонент.
- Retirement запрещает повторные подписки в `Start`, `Update`, connect callbacks, public load/preload/unload входы и внутренние auto-load/load routines, даже если сторонний код снова включит компонент. Обычный legacy startup этот метод не вызывает.
- Активные native load/unload AsyncOperation отслеживаются отдельно от корутин. При незавершённых операциях/transition handoff возвращает `legacy_scene_operations_in_flight`, не выдавая ложного подтверждения отмены native загрузки.
- `ResetForMainMenu` не может выгрузить сцены работающего/подготовленного global pilot. Явная menu-cleanup после полного shutdown остаётся разрешена, но не возвращает legacy ownership.
- `GlobalMotionPilotRuntime` выполняет handoff **до** своей первой additive-загрузки и повторно после загрузки/восстановления Bootstrap roots, учитывая также уже отключённые loaders.
- `GlobalSceneNativeExecutor` проверяет реальные экземпляры `Scene` (handle), а не только совпадение path. Unknown, duplicate/reloaded, pathless, pending и потерянные prepared scenes не пропускаются.
- Отказ теперь различает `startup_lease_missing`, `startup_manager_replaced`, `initial_scene_set_changed:<reason>`. Диагностика содержит expected/actual scene sets с name/path/handle/isLoaded.

Retirement действует до уничтожения данного компонента, в том числе после неудачного запуска. Для следующего ручного gate требуется новый Play Mode-сеанс из BootstrapScene. Автоматическое возвращение legacy ownership внутри того же Play Mode этим изменением не реализуется.

### Проверки и границы

- Компиляция после исправлений: `No compile errors`.
- Существующий `ValidateGlobalSceneExecution.Run()`: `32 passed / 0 failed`, вызван в Edit Mode. Это чистые policy/ledger/seam проверки, не runtime-тест нового handoff.
- Read-only code review подтвердил порядок handoff и guards; выявленный открытый menu-reset вход защищён до финальной compile-проверки.
- Play Mode, screenshots, Host/player spawn и release/restore после изменения **не запускались** автоматически и остаются UNVERIFIED.
- Scene/prefab/assets и catalog/digest в этом исправлении не менялись; 150-marker binding не ослаблен. `GroundPlane_0_0` не восстанавливался.
- Предшествующие незакоммиченные изменения `CraftingStation`, трёх рецептов, TMP и любые изменения Packages не включаются в этот этап.

Пользовательский gate: новый Play Mode → Start Host; отсутствие повторного центра/соседей; затем native admission/player spawn. Если возникнет отказ, сохранить целую строку новой диагностики expected/actual. Не разрешать partial binding или произвольные сцены ради прохождения gate.

## 14. Первый успешный пользовательский startup/spawn; меню и jitter — 2026-09-10

После `9f45d953` пользователь сообщил: ошибок нет, игра запустилась, персонаж появился. Главное меню со Start Host осталось поверх игры; за ним виден персонаж, который продолжает дрожать как раньше. Это пользовательское подтверждение Host/startup и появления персонажа, **не** приёмка камеры, grounding, multiplayer, streaming/release или устранения jitter. Скриншоты не предоставлены; автоматический Play Mode/захват изображений не выполнялся.

### Почему меню оставалось открытым и что изменено

В актуальной `BootstrapScene` включено `_autoStartHost: 1` на `GlobalMotionPilotRuntime`. Пилот напрямую вызывает NGO `StartHost`, а `Hide()` у `MainMenuWindow`/`NetworkTestMenu` вызывается в обычных обработчиках UI-кнопок. `MainMenuWindow.Start()` независимо вызывает `Show()`; успешное создание local player раньше не инициировало закрытие меню в pilot-пути.

Живой Edit Mode осмотр подтвердил два конкретных UI-объекта:

- `MainMenu` с активным `MainMenuWindow` и baked marker `336a190646b19bc46b22dd4e78f99800:1567705303:0`;
- `TestObjects/NetworkTestCanvas` с активным `NetworkTestMenu` и ссылками на Host/Client/Server/Load World buttons. Компонент находится на Canvas, а не на дочернем `MenuPanel`.

Изменён **только** `GlobalMotionPilotRuntime.cs`:

- после успешного `StartHost` coroutine ждёт хотя бы один кадр, native scene admission и настоящий `ConnectedClients[LocalClientId].PlayerObject`;
- обязательны `IsSpawned`, `IsPlayerObject`, `IsOwner`, совпадение NetworkManager, global coordinate mode, `CanSimulateInCurrentCoordinates` и `GlobalMotionPoseAdapter.IsBaselineReady`;
- один раз вызывается штатный `Hide()` у активных стартовых меню двух указанных типов в BootstrapScene;
- early connection callback не используется как сигнал готовности, потому что global bootstrap спавнит player позже;
- при failed/disposed startup меню не скрывается; ожидание ограничено 60 секундами;
- лог содержит число обработанных меню и фактические origin/local координаты; нулевое число совпадений даёт warning, а не ложный success;
- gameplay/Esc/settings панели, cursor/input policy, локализация, scene/prefab assets и catalog/digest не изменялись. Автоматический startup и legacy button handlers сохранены.

Допущение этой узкой UI-правки: стартовые меню уже активны в BootstrapScene, как подтверждено осмотром. Позднее создание/перенос некаталогизированного меню в DDOL не принимается автоматически; это не причина расширять scene ownership.

### Почему этот прогон ещё не проверяет rebase

В `GlobalMotionPilotSpawnSource.Prepare()` по-прежнему:

- frame создаётся через `new LocalCoordinateFrame(GlobalPosition.Zero, _maxLocalCoordinate)`;
- `_maxLocalCoordinate = 100000f`, offset спавна — `Vector3.up * 1f`;
- сериализованный корневой `Respawn_Default` расположен в `(39992, 0, 40000)`;
- `LocalCoordinateFrame.TryToLocal()` вычисляет `global - Origin`, поэтому начальная локальная позиция по сохранённым данным — `(39992, 1, 40000)`, а не окрестность нуля. Это около 56.6 км от нулевой точки; не измерение текущей runtime-позиции после движения;
- текущие catalog entries имеют `Unmanaged/spatial=false`, и executor не перемещает world content.

Пользовательское наблюдение jitter остаётся отрицательным результатом. Точная составляющая дрожания (анимация/камера/контроллер/точность) в этом сеансе отдельно не измерялась, но важное ограничение доказано кодом: local origin не перенесён к игроку. Нельзя объявлять существующий global network contract или успешный spawn готовым floating-origin исправлением. Простая смена origin только у игрока отделит его от authored world/colliders; она **не выполнялась**. Следующий отдельный этап T-FO06 должен обеспечить согласованное размещение reviewed content/player/camera относительно ненулевого origin с сохранением глобальных координат, а не маскировать jitter сглаживанием.

### Проверки и следующий пользовательский gate

- Compile после UI-правки: `No compile errors`.
- `git diff --check` для изменённого script — PASS.
- Read-only review проверил readiness и one-shot hide; визуальное исчезновение меню после этой правки — UNVERIFIED.
- Следующий пользовательский прогон: новый Play Mode из BootstrapScene; после появления готового персонажа стартовые меню должны исчезнуть. Jitter этой UI-правкой **не исправлялся**; полноценный rebase/anti-jitter gate остаётся открытым.
- Предыдущие незакоммиченные `CraftingStation`, три recipe assets и TMP остаются вне этого этапа. Один коммит: UI code + этот отчёт + существующий ITERATIONS, без отдельной записи хеша коммитом.

## 15. Отрицательный runtime initial frame bridge; откат — 2026-09-10

### Результат пользовательского запуска

После добавления `GlobalMotionPilotFrameBridge` player появлялся, но карта `WorldScene_0_0` не была видна/доступна под ним, поэтому player продолжал падать. Корабли всё ещё создавались. Jitter нельзя было оценить из-за отсутствия рабочей опоры/карты. Это отрицательный runtime результат: bridge не принимается как исправление координат или jitter.

Console подтвердил первичную техническую ошибку bridge: до Start Host `GlobalMotionPilotFrameBridge.TryPrepare()` вызывал `Transform.SetParent` для scene-placed `NetworkObject`. NGO выдал `networkManager is not listening, start a server or host before re-parenting` не только для двух `Ship_Light_root`, но и для pickup, chest, resource, crafting и docking NetworkObject roots. Stack trace указывает на `GlobalMotionPilotFrameBridge.TryPrepare` line 129, вызванный `GlobalMotionPilotRuntime` до `NetworkManager.StartHost`.

Следствие: aggregate-parent операция не является допустимым pre-NGO placement mechanism для mixed WorldScene_0_0. По пользовательскому наблюдению карта также не оказалась согласованной с player. Конкретная причина её отсутствия (mesh-local origin, root layout или иной visual/content bridge) этим запуском отдельно не измерена; она не должна маскироваться предположением.

### Откат

Выполнен `git revert --no-commit 97ee4338`, затем зафиксирован один T-FO06L commit с отрицательным результатом. Откат удаляет только эксперимент:

- `GlobalMotionPilotFrameBridge.cs` и его `.meta`;
- bridge component из `BootstrapScene`;
- bridge invocation из `GlobalMotionPilotRuntime`;
- configured initial frame support из `GlobalMotionPilotSpawnSource`.

Возвращены прежние semantics: source создаёт fixed frame с `origin=(0,0,0)`, catalog/digest/150 marker bindings не изменяются, root parenting и `Physics.SyncTransforms` не вызываются. Коммит `6db7e78d` с закрытием стартовых меню после готовности local pilot player не откатывается. Ранее подтверждённый запуск Host/player остаётся baseline, но player grounding и jitter не считаются проверенными.

### Следующий корректный шаг

До новой runtime попытки требуется T-FO06 read-only classification, а не ещё один общий transform shift:

1. отделить render/collider city content от scene-placed NetworkObject gameplay roots и от ship Rigidbody roots;
2. определить, где реально лежит world-city mesh/collider относительно `WorldRoot_0_0` и `Respawn_Default`;
3. спроектировать atomic placement/rebase transaction без `SetParent` scene-placed NetworkObject до listening и без перемещения unknown roots;
4. отдельно подтвердить player grounding у `Respawn_Default`, поскольку `GroundPlane_0_0` намеренно удалён.

Это не разрешение partial scene ownership, не возврат GroundPlane и не замена реального rebase сглаживанием. Runtime Play Mode/screenshot после кода отката автоматически не выполняются.

## 16. Исправление pre-start проверки cataloged Unmanaged NetworkObject — 2026-09-10

### Фактический runtime отказ

После отката frame bridge пользовательский Host дошёл до native preparation и остановился на:

```text
uncontrolled_network_source_before_native_sweep:Road to Quartus
```

Read-only разбор `WorldScene_0_0` показал, что `Road to Quartus` — scene-authored `DockStation` с `NetworkObject`, `OuterCommZone`, trigger collider и `GlobalSceneSourceMarker`. Его source ID `837a3910185f2b9478ce71df025ccc7c:1987571259:0` уже присутствует в `GlobalMotionPilotSceneCatalog.asset`, с `treatment: 5` (`Unmanaged`), `spatial: false`, `frameId: 0`. В YAML этого объекта `InScenePlaced = 0`; до запуска NGO его `NetworkManager` остаётся `null`.

Причина была в pre-start sweep `GlobalSceneNativeExecutor`: он требовал `no.NetworkManager == manager` от каждого cataloged NetworkObject, включая `Unmanaged` source. Это противоречило контракту `Unmanaged`: executor не должен его регистрировать, размещать, активировать или спавнить.

### Изменение

В sweep добавлено узкое разрешение только для уже проверенного cataloged `Unmanaged` объекта, если одновременно выполнены условия:

- его `GlobalSceneSourceMarker` найден в подготовленном catalog binding;
- source действительно имеет `Unmanaged` treatment;
- `NetworkManager == null` до NGO startup;
- `NetworkObject` не spawned.

Неизвестный/внешний NetworkObject, объект другого manager или уже spawned объект по-прежнему вызывает `uncontrolled_network_source_before_native_sweep`. Active-state restriction также сохранена для всех managed sources.

### Проверки и следующий gate

- Compile: `No compile errors`.
- Catalog source ID и digest не изменялись; `Road to Quartus` не исключался и не добавлялся повторно.
- `GroundPlane_0_0` не восстанавливался.
- Play Mode после исправления ещё не запускался пользователем.

Следующий ручной gate: новый запуск из `BootstrapScene` → Start Host. Ожидаемый результат — пройти `Road to Quartus` и получить либо `catalog_markers_bound_mismatch`, либо следующую фактическую причину. Partial binding и автоматическое принятие неизвестных NetworkObject запрещены.

## 17. Отключение непреднамеренного autostart pilot — 2026-09-10

### Фактическое наблюдение

Пользователь получил ту же ошибку ещё до нажатия `Start Host`. Причина подтверждена в `BootstrapScene`: у `GlobalMotionPilotRuntime` было `_autoStartHost: 1`, а его `Start()` немедленно вызывал `StartPilotHost()` в начале Play Mode. Ошибка не была вызвана кнопкой UI.

Дополнительно обе кнопки Host (`MainMenuWindow` и `NetworkTestMenu`) вызывали обычный `NetworkManagerController.StartHost()`, поэтому ручной Host не проходил через pilot startup path.

### Изменение

- В `BootstrapScene` установлено `_autoStartHost: 0`.
- Host-кнопки теперь сначала ищут активный `GlobalMotionPilotRuntime` и вызывают `StartPilotHost()`.
- Если pilot отсутствует, сохраняется прежний обычный `NetworkManagerController.StartHost()`.
- Pilot UI не скрывается сразу после клика: меню закрывается только после подтверждённой готовности local pilot player штатным one-shot handoff.

Это возвращает контроль запуска пользователю и одновременно сохраняет pilot ownership для ручного Host в этой сцене.

### Проверки

- `NetworkTestMenu.cs`: стандартная валидация — 0 warnings, 0 errors.
- `MainMenuWindow.cs`: стандартная валидация — 0 warnings, 0 errors.
- Compile: `No compile errors`.
- Play Mode после изменения ещё не запускался.

Следующий gate: войти в Play Mode, убедиться, что до клика `Start Host` pilot не выполняет native preparation, затем нажать `Start Host` и зафиксировать следующую фактическую точку прохождения/ошибку.

## 18. Исправление Bootstrap scene path mismatch — 2026-09-10

### Фактическая причина

После ручного нажатия `Start Host` отказ произошёл на восстановлении cataloged Bootstrap roots:

```text
[T-FO06L] Pilot could not restore cataloged Bootstrap roots from DontDestroyOnLoad.
```

Read-only проверка установила расхождение путей:

- активная сцена Unity: `Assets/BootstrapScene.unity`;
- cataloged и build-enabled Bootstrap: `Assets/_Project/Scenes/BootstrapScene.unity`;
- cataloged Bootstrap GUID: `336a190646b19bc46b22dd4e78f99800`;
- `Assets/BootstrapScene.unity` — отдельный дубликат, отсутствующий в catalog и выключенный в `EditorBuildSettings`.

`GlobalMotionPilotRuntime` жёстко запрашивал только catalog path. Поэтому восстановление возвращало `false` ещё до проверки marker roots. Это был scene-path mismatch, а не отсутствие разрешённых `DontDestroyOnLoad` roots.

### Изменение

- `GlobalMotionPilotRuntime` сохраняет разрешение Bootstrap через cataloged path `Assets/_Project/Scenes/BootstrapScene.unity`; это важно, потому что `NetworkManager` может уже находиться в `DontDestroyOnLoad`.
- При отсутствии cataloged Bootstrap в loaded scenes выводятся `expectedPath`, `activePath` и `managerScene`; некаталогированный дубликат не принимается.
- Catalog entry дополнительно проверяется по `sceneGuid`; fail-closed binding и список cataloged paths сохраняются.
- UI handoff продолжает использовать cataloged Bootstrap scene, чтобы не скрывать меню в некаталогированной сцене.
- Каталог, digest, treatment-правила и fail-closed binding не ослаблялись.

### Проверки и границы

- Обновлены `GlobalMotionPilotRuntime.cs`, `Assets/_Project/Scenes/BootstrapScene.unity` (`_autoStartHost: 0`) и документация.
- Scene diff проверен: изменена ровно одна строка `_autoStartHost`, без посторонней сериализации.
- Standard script validation: 0 warnings, 0 errors; Unity compile: `No compile errors`.
- Свежий Play Mode после исправления не выполнен.
- Следующий runtime gate запускается из cataloged сцены `Assets/_Project/Scenes/BootstrapScene.unity`, а не из `Assets/BootstrapScene.unity`.

Следующий gate: открыть cataloged Bootstrap, нажать `Start Host` и проверить, что ошибка path mismatch исчезла. Если startup остановится снова, новый лог покажет точное условие отказа вместо общего сообщения.

## 19. Диагностика uncontrolled NetworkObject — 2026-09-10

### Результат следующего ручного gate

После запуска из правильной cataloged Bootstrap-сцены и нажатия `Start Host` path mismatch больше не является отказом. Native preparation снова остановилась на:

```text
scene_preparation:uncontrolled_network_source_before_native_sweep:Road to Quartus
```

Статический scene audit подтверждает, что `Road to Quartus` содержит marker и `NetworkObject` на одном GameObject, source ID присутствует в catalog, а `InScenePlaced = 0`. Однако stack trace не показывал, какое именно runtime-условие нарушено.

### Изменение

В `GlobalSceneNativeExecutor` добавлена fail-closed диагностика для каждого uncontrolled network source:

- `sourceId`;
- `catalogBound`;
- `unmanaged`;
- `candidate`;
- фактический и ожидаемый `NetworkManager`;
- `isSpawned`;
- `activeSelf` / `activeInHierarchy`;
- `enabled` и `InScenePlaced`;
- `SynchronizeTransform` и `AutoObjectParentSync`;
- scene path.

Проверка `Unmanaged` не ослаблена. Разрешённым остаётся только catalog-bound, non-spawned источник без runtime `NetworkManager`; unknown, spawned, foreign-manager и non-candidate источники по-прежнему блокируют startup.

### Проверки

- Standard script validation: 0 ошибок; 2 существующих advisory warnings о Rigidbody/GC.
- Unity compile: `No compile errors`.
- Play Mode после диагностического изменения не выполнялся.

Следующий gate: выполнить свежий Play Mode из cataloged Bootstrap и прислать полный однострочный отказ с полями состояния `Road to Quartus`. По этим полям будет применён следующий узкий фикс без ослабления closed-world проверки.

## 20. Восстановление cataloged DDOL roots — 2026-09-10

### Результат runtime gate

Полная диагностика отказа показала:

```text
catalogBound=True;unmanaged=True;candidate=False;networkManager=NetworkManager;expectedManager=NetworkManager;isSpawned=False;activeSelf=True;activeInHierarchy=True;inScenePlaced=True;scene=
```

`Road to Quartus` не был неизвестным и не был spawned. Его authored root оказался в `DontDestroyOnLoad`, поэтому candidate enumeration, которая обходила только loaded scene roots, не включила его. При этом native executor обязан видеть объект в исходной cataloged сцене: `RequireIdentity` проверяет `marker.gameObject.scene == node.Scene`.

### Исправление

`GlobalMotionPilotRuntime` теперь перед native preparation восстанавливает все catalog-bound roots из `DontDestroyOnLoad` в соответствующие загруженные cataloged сцены. Для каждого DDOL marker используется его `sourceId → sceneGuid`; если один DDOL root содержит источники разных сцен или целевая cataloged сцена не загружена, startup остаётся fail-closed.

Это исправляет candidate enumeration без принятия неизвестных DDOL NetworkObject и без ослабления политики `Unmanaged`.

### Проверки

- `GlobalMotionPilotRuntime.cs`: standard validation — `0 warnings, 0 errors`.
- Unity compile: `No compile errors`.
- Play Mode после изменения не выполнялся.

Следующий gate: свежий ручной `Start Host` из `Assets/_Project/Scenes/BootstrapScene.unity`. Ожидаемый первый новый лог — восстановление cataloged root(s); затем native preparation должна пройти `Road to Quartus` или выдать следующий конкретный catalog/runtime blocker.

## 21. Повторная DDOL-проверка перед TryPrepare — 2026-09-10

### Результат gate

Повторный runtime gate показал тот же отказ `Road to Quartus` с `candidate=False` и пустым `scene=`. Предыдущий restoration fix не дал достаточного подтверждения, возвращается ли root в WorldScene до native preflight или он снова перемещается после content preparation.

### Изменение

`GlobalMotionPilotRuntime` усилен дополнительным boundary audit:

- DDOL markers ищутся через `Resources.FindObjectsOfTypeAll`, чтобы не зависеть от охвата обычного scene search;
- restoration повторяется непосредственно после `RefreshPreparedContent()` и перед `GlobalMotionNetworkStartup.TryPrepare()`;
- после `SceneManager.MoveGameObjectToScene` проверяется фактическая принадлежность root целевой сцене;
- каждый проход выводит `markers`, `catalogedMarkers`, `roots` и `restored`.

Это не принимает DDOL source как допустимый runtime state: объект должен быть возвращён в cataloged scene до `RequireIdentity` и native sweep.

### Проверки

- `GlobalMotionPilotRuntime.cs`: standard validation — `0 warnings, 0 errors`.
- Unity compile: `No compile errors`.
- Play Mode после изменения не выполнялся.

Следующий gate: нажать `Start Host` из cataloged Bootstrap и прислать строки `Cataloged DDOL audit` и последующий отказ, если он останется.

## 22. Reconcile DDOL roots на native-preflight boundary — 2026-09-10

### Результат gate

Получены два audit:

```text
markers=16;catalogedMarkers=16;roots=16;restored=16
markers=0;catalogedMarkers=0;roots=0;restored=0
```

После второго audit `Road to Quartus` снова попадал в native sweep как `candidate=False` с пустым `scene=`. Это доказывает, что перемещение в DDOL происходит внутри подготовительного промежутка между pilot audit и `GlobalSceneNativeExecutor.BuildPreparation`.

### Исправление

`GlobalSceneNativeExecutor` теперь сам выполняет cataloged DDOL reconciliation:

- перед `BuildPreparation` в `ValidatePreparation`;
- перед `BuildPreparation` в `PrepareBeforeNetworkStart`;
- с тем же fail-closed контролем source ID, scene GUID, mixed-scene roots и фактического результата перемещения.

Таким образом native executor получает объект уже в исходной cataloged сцене даже если внешний NGO/config preflight повторно перевёл authored root в `DontDestroyOnLoad`.

### Проверки

- `GlobalSceneNativeExecutor.cs`: standard validation — 0 ошибок, 2 существующих advisory warnings.
- Unity compile: `No compile errors`.
- Play Mode после изменения не выполнялся.

Следующий gate: свежий ручной `Start Host`. В случае повторного DDOL-переноса должен появиться лог `Native preflight restored cataloged DDOL root(s): ...` перед обработкой scene catalog.

## 23. T-FO06L.1 — ownership/NetworkObject boundaries, read-only classification — 2026-09-10

### Граница этапа

Этот подэтап выполнен только в Edit Mode. Play Mode, DDOL round-trip, scene/prefab/catalog mutation и общий `SetParent` для смешанных scene-owned `NetworkObject` не выполнялись. Текущие незакоммиченные изменения двух floating-origin scripts не включаются в этот документационный коммит:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalSceneNativeExecutor.cs`;
- `Assets/_Project/Scripts/World/FloatingOrigin/Pilot/GlobalMotionPilotRuntime.cs`.

Посторонние изменения `LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` также не относятся к этапу.

### Фактический scene census

Живой Edit Mode audit открыл только cataloged сцены `BootstrapScene` и `WorldScene_0_0`, перечислил все их authored descendants и закрыл дополнительно открытые сцены без сохранения.

| Сцена | NetworkObject | Классификация |
|---|---:|---|
| `Assets/_Project/Scenes/BootstrapScene.unity` | 19 | scene-owned Bootstrap network services: 19 |
| `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | 77 | scene-owned gameplay: 45; docking gameplay: 10; ship/Rigidbody roots: 22 |
| **Итого** | **96** | все 96 имеют `GlobalSceneSourceMarker` |

В текущем Edit Mode snapshot у всех 96 authored `NetworkObject` одинаковая наблюдаемая граница: `InScenePlaced=false`, `IsSpawned=false`, `NetworkManager=<null>`, `SynchronizeTransform=true`, `AutoObjectParentSync=true`, `ActiveSceneSynchronization=false`, `SceneMigrationSynchronization=false`. Это состояние до NGO startup, а не доказательство допустимости runtime relocation.

### Полный список authored NetworkObject boundaries

**BootstrapScene — 19:** `[CombatServer]`, `[CraftingServer]`, `[DockingServer]`, `[EquipmentServer]`, `[ExchangeServer]`, `[GatheringServer]`, `[NpcShipServer]`, `[QuestServer]`, `[ShipCargoServer]`, `[SkillsServer]`, `[StatsServer]`, `CloudManager`, `Contracts/[ContractServer]`, `Inventory/[InventoryServer]`, `Inventory/[ShipKeyServer]`, `Markets/[MarketServer]`, `PlayerSpawner`, `ServerWeatherController`, `Toasts_and_meta/[MetaRequirementRegistry]`.

**WorldScene_0_0 — 22 ship/Rigidbody roots:** `Ship_Light_root`, `Ship_Light_root (копия с компьютера DESKTOP-K00O7HK)`, `Альбатрос`, `Берег`, `Вавилон`, `Ветроворот`, `Гигант`, `Горгона`, `Жук`, `Летучий`, `Лорейн`, `Мастодонт`, `Олимп`, `Пещера`, `Река`, `Сильфида`, `Скат`, `Странник`, `Торренс`, `Угольщик`, `Цитадель`, `Шмель`.

**WorldScene_0_0 — 10 docking/network gameplay roots:**

- `DockStation_Primium` — `837a3910185f2b9478ce71df025ccc7c:2099880783:0`;
- `DockStation_TestZone` — `837a3910185f2b9478ce71df025ccc7c:52261661:0`;
- `WorldRoot_0_0/Primum_farms/Средняя 0_0` — `837a3910185f2b9478ce71df025ccc7c:1266341961:0`;
- `WorldRoot_0_0/Primum_farms/Ферма Примума 0_1` — `837a3910185f2b9478ce71df025ccc7c:1177720397:0`;
- `WorldRoot_0_0/Primum_farms/Ферма Примума 0_2` — `837a3910185f2b9478ce71df025ccc7c:226542065:0`;
- `WorldRoot_0_0/Primum_farms/Ферма Примума 0_3` — `837a3910185f2b9478ce71df025ccc7c:1656927025:0`;
- `WorldRoot_0_0/Primum_farms/Ферма Примума 0_4` — `837a3910185f2b9478ce71df025ccc7c:182231080:0`;
- `WorldRoot_0_0/Secund/Верхняя панель (1)/Road to Secund` — `837a3910185f2b9478ce71df025ccc7c:1631178272:0`;
- `WorldRoot_0_0/Tertius/Верхняя панель (1)/Road to Tertius` — `837a3910185f2b9478ce71df025ccc7c:1505408850:0`;
- `WorldRoot_0_0/Quartus/Верхняя панель (1)/Road to Quartus` — `837a3910185f2b9478ce71df025ccc7c:1987571259:0`.

Каждый из этих 10 roots имеет `DockStationController`, `OuterCommZone`, `PadStateSync`, `NetworkObject` и `GlobalSceneSourceMarker`. `Road to Quartus` дополнительно подтверждён по runtime отказу и имеет trigger `SphereCollider`; его source ID уже catalog-bound.

**WorldScene_0_0 — остальные 45 scene-owned gameplay sources:**

- crafting: `[CraftingStation_Shipyard]`, `[CraftingStation_Table]`;
- resources: `[ResourceNode_CopperVein]`, `[ResourceNode_IronVein]`, `[ResourceNode_PlantHerb]`;
- chests: `Chest_East`, `Chest_Main`, `Chest_North`;
- ordinary pickups: `[Pickup_TimeCrystal]`, `Pickup_Clothing_SteelChestplate`, `Pickup_Clothing_TravelerBoots`, `Pickup_Clothing_WorkerHelmet`, `Pickup_Food_1`, `Pickup_Food_2`, `Pickup_Res_1`, `Pickup_Res_2`, `Pickup_Weapon_AntigravBlade`, `Pickup_Weapon_IronDagger`, `Pickup_Weapon_IronSpear`, `Pickup_Weapon_WoodenSword`;
- authored NPCs: `NPC/[Mira]`, `NPC/[Onboarding alfa]`, `Q001_RuntimeTest_Line/Q001_NPC_Bram__Bram`, `Q001_RuntimeTest_Line/Q001_NPC_Kael__Kael`, `Q001_RuntimeTest_Line/Q001_NPC_Lyra__Lyra`, `Q001_RuntimeTest_Line/Q001_NPC_Noll__Noll`, `Q001_RuntimeTest_Line/Q001_NPC_Sela__Sela`, `Q001_RuntimeTest_Line/Q001_NPC_Veska__Veska`;
- Q001 pickups: `Q001_RuntimeTest_Line/Q001_Pickup_blackbox_core`, `Q001_RuntimeTest_Line/Q001_Pickup_false_manifest`, `Q001_RuntimeTest_Line/Q001_Pickup_false_seal`, `Q001_RuntimeTest_Line/Q001_Pickup_fragment_archive`, `Q001_RuntimeTest_Line/Q001_Pickup_fragment_dock`, `Q001_RuntimeTest_Line/Q001_Pickup_fragment_sela`, `Q001_RuntimeTest_Line/Q001_Pickup_resonance_lens`;
- market actors: `WorldRoot_0_0/Primum_farms/Средняя 0_0/Market_zone_farm_0_0/Npc_peacfull_market_zone`, the four corresponding `Market_zone_Primium_farm_0_1`…`0_4/Npc_peacfull_market_zone` objects, and `Market_zone_Road to Secund`, `Market_zone_Road to Tertius`, `Market_zone_Road to Quartus` variants;
- scene spawners: `SPAWN_TEST`, `SPAWN_TEST cult`.

### Ownership decisions

1. **Static city render/collider content** — remains authored scene content and is not a `NetworkObject` boundary. It must not be moved by the native network executor merely because a parent contains a marker.
2. **Scene-owned Bootstrap network services** — the 19 authored Bootstrap `NetworkObject` roots remain owned by the cataloged `BootstrapScene`. The `NetworkManager` root itself is a persistent Bootstrap service, but its runtime-created client states/UI/services are a separate DDOL infrastructure category and are not authored world roots.
3. **SceneOwnedNetworkGameplay** — all authored docking stations, NPCs, pickups, chests, resource/crafting objects and scene spawners belong here. They are not passive static `Unmanaged` sources. Current catalog binding remains unchanged in this read-only stage; a later implementation must give this group one explicit lifecycle owner.
4. **ShipOrRigidbodyRoot** — all 22 ship roots are separate dynamic actors. Their `Rigidbody`, `ShipController`, `NetworkTransform` and ship-specific services exclude them from a common static-world rebase or mixed-root parenting operation. Ship rebase/physics participation is a later, explicit contract.
5. **Persistent services** — `NetworkManagerController` calls `DontDestroyOnLoad` on the `NetworkManager` root and creates additional persistent client states/UI/service roots. `DockingWorld`, `DockingClientState`, `NpcShipWorld`, `NpcShipClientState`, `NpcShipTrafficManager`, `NpcCargoService`, `ShipPositionServer` and `PlayerPositionServer` also create DDOL runtime infrastructure. These objects do not acquire ownership of authored `WorldScene_0_0` roots.
6. **Legacy streaming ownership** — authored `Runtime/ClientSceneLoader` is DDOL-capable and can load/unload world scenes; `WorldSceneManager` is another DDOL scene/chunk coordination layer. `GlobalMotionPilotRuntime` must retire the legacy loader before pilot ownership is transferred. A disabled component without detached callbacks/coroutines is not sufficient; the existing explicit retirement path is the relevant handoff seam.
7. **Runtime-generated DDOL infrastructure** — factory-created singleton services, UI, toasts, client state and docking/ship services are not valid catalog substitutes for scene-authored roots. They must either remain in a dedicated persistent scene/service boundary or remain outside the authored catalog with explicit ownership.

### Decision for `Road to Quartus` and docking analogues

`Road to Quartus`, `Road to Secund`, `Road to Tertius`, the five Primium farm stations and the two named test stations are classified as:

```text
SceneOwnedNetworkGameplay
subtype=DockingStation
lifecycle=authored-scene-owned
```

They must remain in `WorldScene_0_0`, preserve their catalog marker, parent and scene identity, and be initialized/spawned through one explicit scene/network lifecycle path. Their docking runtime counterpart (`DockingWorld` and client state) is persistent service infrastructure, not a reason to move the station roots to DDOL. `Unmanaged` is not accepted as a substitute for this contract, and no partial binding or automatic sync-flag relaxation is introduced here.

### Sources capable of DDOL relocation

Static code audit found the following relevant relocation families:

- Bootstrap/network: `NetworkManagerController`, `ClientSceneLoader`, `WorldSceneManager`;
- persistent client state and UI factories: Inventory/Contract/Market/Exchange/Crafting/Gathering/Equipment/Skills/Stats/MetaRequirement/Quest/Customisation/Recipe knowledge states, `UIManager`, `InputBindingsRuntime`, settings windows, toast/HUD services;
- ship and docking services: `ShipPositionServer`, `PlayerPositionServer`, `DockingWorld`, `DockingClientState`, `NpcShipWorld`, `NpcShipClientState`, `NpcShipTrafficManager`, `NpcCargoService`;
- world visuals/services: `CloudManager`, `WindManager`, `ConstellationController`, `HorizonVeilRenderer`, `VeilRaymarchMeshController`, combat/target/VFX services;
- explicit scene moves: `GlobalMotionPilotRuntime.RestoreCatalogedSceneRootsFromDontDestroyOnLoad`, `GlobalSceneNativeExecutor.RestoreCatalogedDdolRoots`, and player-only `GlobalMotionPlayerBootstrap` placement.

`NpcBrain.TrySetParent` ship attachment is a different operation: it is NGO network parenting for an already spawned NPC to an already spawned ship, not a scene-identity/DDOL mechanism. It must not be conflated with world-root relocation.

### Native and compile checks

- Unity compile: `No compile errors`.
- `GlobalSceneNativeExecutor.cs`: 0 errors, 2 existing advisory warnings.
- `GlobalMotionPilotRuntime.cs`: 0 warnings, 0 errors.
- `Validate Native Scene Execution Contracts`: `32 passed / 0 failed`.
- Scene census: `19 + 77 = 96` authored NetworkObjects; no scene/catalog/digest changes; no Play Mode.

### Playtest gate after this classification

Нормальный T-FO06L pilot Play Mode ещё **не разрешён** этим этапом. Сначала требуется отдельная implementation stage, которая:

- переводит docking/network sources из пассивной `Unmanaged` трактовки в явный `SceneOwnedNetworkGameplay` lifecycle contract;
- убирает зависимость startup от DDOL restoration как архитектуры, оставляя его только fail-closed diagnostic guard;
- подтверждает один owner для legacy loader retirement и additive scene admission;
- сохраняет `catalog=150;markers=150;bound=150` и закрытый world scene set;
- повторно выполняет static/native validators и создаёт отдельный commit.

Только после этого пользовательский свежий Play Mode из `Assets/_Project/Scenes/BootstrapScene.unity` является первым нормальным startup gate. Его acceptance sequence: native preparation → `NetworkManager.StartHost()` → player spawn → stable scene ownership → `Road to Quartus` остаётся в `WorldScene_0_0` → отсутствие duplicate `WorldScene_0_0`/legacy streaming takeover. Grounding, camera, jitter, rebase, physics и client admission проверяются отдельными последующими gates.

## 24. T-FO06L.2 — scene-owned gameplay lifecycle contract, static gate — 2026-09-10

### Реализация

- Введён явный `GlobalSceneOwnership`: `AuthoredSceneContent`, `BootstrapService`, `SceneOwnedNetworkGameplay` и `ShipOrRigidbodyRoot`; `Unspecified` запрещён.
- Все `150` catalog entries классифицированы по фактически наблюдаемой ownership boundary: `authored=54`, `bootstrap=19`, `sceneGameplay=55`, `ships=22`.
- Ownership включён в compiler policy и digest; текущий digest профиля: `d491f599e5fd9a7437a48e6d9474c53aff2320464f6851e24e08d58f68cd02b4`.
- `SceneOwnedNetworkGameplay` отделён от старого `GlobalSceneTreatment.Unmanaged`: treatment не выдаёт lifecycle ownership автоматически; scene-owned gameplay требует явного NGO lifetime receipt.
- Native executor теперь fail-closed отвергает cataloged DDOL roots, проверяет ownership, ожидаемый `NetworkManager`, spawn/lifetime receipt и не допускает retirement spawned scene-owned gameplay.
- `ClientSceneLoader.TryRetireForGlobalPilot()` остаётся единственным handoff seam для legacy streaming; partial binding, DDOL restoration как архитектура, mixed-root `SetParent` и sync-flag relaxation не добавлялись.

### Изменение pure fixtures

Каталожный тест ordinary content приведён в соответствие с ownership policy: после удаления observed `NetworkObject` fixture явно становится `AuthoredSceneContent`. Это сохраняет проверку stale-retirement protection и не ослабляет компилятор.

### Проверки

- Unity compile: `No compile errors`.
- `ValidateGlobalSceneExecution.Run()`: **35 passed / 0 failed**.
- `ValidateGlobalSceneCatalog.Run()`: **57 passed / 0 failed**.
- Static snapshot: `catalog=150; markers=150; bound=150`.
- Catalog digest/profile match: `d491f599e5fd9a7437a48e6d9474c53aff2320464f6851e24e08d58f68cd02b4`.
- Play Mode, native startup, Host/client, grounding, physics, camera, rebase и screenshots не выполнялись; пользовательский runtime gate остаётся UNVERIFIED.

### Граница коммита

В T-FO06L.2 входят ownership/compiler/ledger/executor/runtime изменения, catalog/profile, validators и эта документация. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` являются посторонними и не включаются. После этого отдельного implementation-коммита пользовательский Play Mode gate может быть рассмотрен, но сам по себе static PASS не является runtime acceptance.

## 25. T-FO06L.2.x — PersistentBootstrapService после fail-closed DDOL gate — 2026-09-10

### Фактический runtime отказ

Свежий пользовательский Play Mode остановился до загрузки мира на ожидаемом fail-closed барьере:

```text
cataloged_source_in_ddol;restoration_forbidden
```

`16` ранее подтверждённых Bootstrap-root объектов с baked `GlobalSceneSourceMarker` были перемещены в `DontDestroyOnLoad`. Затем свежие Start Host попытки выявили пять client/UI roots, а после их классификации — ещё девять известных persistent roots. Последний фактический отказ был на `[ShipHudPanel]` (`sourceId=336a190646b19bc46b22dd4e78f99800:258728291:0`, `ownership=AuthoredSceneContent`). Контракт корректно отказал в принятии root и не выполнил restoration обратно в authored scene.

### Узкий фикс

- Добавлен отдельный ownership `PersistentBootstrapService`.
- Переклассифицированы подтверждённые 30 DDOL roots: прежние 16 roots (`PlayerSpawner`, `[ShipCargoServer]`, `[CombatServer]`, `NetworkManager`, `[SkillsServer]`, `[DockingServer]`, `[ExchangeServer]`, `Runtime`, `[NpcShipServer]`, `ServerWeatherController`, `[QuestServer]`, `[StatsServer]`, `[GatheringServer]`, `[CraftingServer]`, `[EquipmentServer]` и `CloudManager`), пять client/UI roots (`[QuestClientState]`, `[QuestTracker]`, `[ReputationClientState]`, `[NpcAttitudeClientState]`, `[CustomisationClientState]`) и девять roots из свежего runtime evidence: `[ShipHudPanel]`, `ConstellationController`, `[PlayerPositionServer]`, `[KnowledgeToast]`, `[GatheringToast]`, `[ShipPositionServer]`, `WindManager`, `[CraftingProgressController]`, `[QuestToast]`.
- Три nested Bootstrap entries (`[MetaRequirementRegistry]`, `[ContractServer]`, `[ShipKeyServer]`) сохранены как `BootstrapService`; их ownership не расширялся за пределы подтверждённых DDOL roots.
- `SceneOwnedNetworkGameplay`, `ShipOrRigidbodyRoot`, `GroundPlane_0_0`, DDOL restoration и mixed-root parenting не изменялись.
- Native executor и pilot принимают persistent Bootstrap services в DDOL без восстановления в authored scene; обычные cataloged roots по-прежнему отвергаются fail-closed.
- Profile digest пересчитан и сохранён: `9f5bafcb7217fc2a692195afacceab88dc28252def241d5dfbcfa39e950bbdaa`.
- Ownership distribution после correction: `AuthoredSceneContent=38`, `BootstrapService=5`, `PersistentBootstrapService=30`, `SceneOwnedNetworkGameplay=55`, `ShipOrRigidbodyRoot=22`.

### Проверки

- Unity compile: `No compile errors`.
- `ValidateGlobalSceneCatalog.Run()`: **58 passed / 0 failed**.
- `ValidateGlobalSceneExecution.Run()`: **36 passed / 0 failed**.
- Static snapshot: `catalog=150;markers=150;bound=150`.
- Catalog/profile digest match: `9f5bafcb7217fc2a692195afacceab88dc28252def241d5dfbcfa39e950bbdaa`.
- Play Mode после follow-up 2 correction не выполнялся; новый runtime gate остаётся за пользователем. Screenshots не выполнялись.
- Нормальный gameplay плейтест пока не начинать: сначала нужен один чистый startup gate без DDOL rejection, с native preparation, `StartHost`, player spawn и проверкой отсутствия duplicate/legacy scene takeover. После его успешного пользовательского подтверждения можно передавать отдельный Host/player runtime плейтест; grounding, camera, physics, rebase и jitter остаются последующими gates.

### Граница коммита

В отдельный T-FO06L.2.x входят только ownership reclassification, digest, policy/validator fixture и документация этого fail-closed runtime результата. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` исключаются.

## 26. T-FO06L.2.x follow-up 3 — PlayerSpawner ownership correction — 2026-09-10

### Фактический runtime отказ

Свежий пользовательский запуск из canonical `Assets/_Project/Scenes/BootstrapScene.unity` дошёл до native preflight и остановился на:

```text
[T-FO06L] Pilot global startup refused: scene_preparation:persistent_bootstrap_service_runtime_location_mismatch:PlayerSpawner
```

Перед отказом audit сообщил `markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`. Это означает, что cataloged persistent roots были обнаружены, но `PlayerSpawner` не был в DDOL — он оставался authored root BootstrapScene.

### Read-only evidence and decision

- `PlayerSpawner` найден root-level в `Assets/_Project/Scenes/BootstrapScene.unity`, без parent, `activeSelf=false`.
- На объекте присутствуют `NetworkObject`, `NetworkPlayer`, `NetworkPlayerSpawner` и `GlobalSceneSourceMarker`.
- `NetworkPlayerSpawner` является диагностическим компонентом: старый ручной `SpawnAsPlayerObject` удалён; фактический PlayerObject создаётся через `NetworkConfig.PlayerPrefab`.
- В runtime/source search не найдено кода, который переносит именно этот `PlayerSpawner` в `DontDestroyOnLoad`; `NetworkManagerController.Awake()` переносит свой собственный root, а не PlayerSpawner.
- Поэтому `PlayerSpawner` не соответствует `PersistentBootstrapService`. Это authored Bootstrap NetworkObject, который должен оставаться в canonical BootstrapScene.

### Узкий фикс

`PlayerSpawner` оставлен `treatment: Unmanaged`, но ownership изменён с `PersistentBootstrapService` на `BootstrapService`. Это меняет только ownership classification и digest; DDOL acceptance, restoration, mixed-root parenting, unknown runtime roots и scene gameplay boundaries не ослабляются.

Новый digest профиля:

`dc5616f54e110d5e62063fd3273a4fd6fb3f5b1d6bd5801052fa6c1da3ab1b43`

### Проверки и следующий gate

- Unity compile: **No compile errors**.
- `ValidateGlobalSceneCatalog.Run()`: **58 passed / 0 failed**.
- `ValidateGlobalSceneExecution.Run()`: **36 passed / 0 failed**.
- Static closed-world count remains expected `catalog=150;markers=150;bound=150` by catalog/compiler contract; native runtime binding after this correction is still **UNVERIFIED**.
- Play Mode after this correction was not run automatically.

Следующий этап — новый пользовательский startup gate: fresh Play Mode из `Assets/_Project/Scenes/BootstrapScene.unity`, `Start Host`, затем зафиксировать полный результат native preparation, `StartHost()`, local player spawn и duplicate/legacy scene checks. До его успешного прохождения нормальный gameplay playtest не начинать.

Граница этапа: `GroundPlane_0_0` не восстанавливается; обычный authored content, `SceneOwnedNetworkGameplay` и `ShipOrRigidbodyRoot` не переводятся в DDOL; unrelated `LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` не входят.

## 27. T-FO06L.2.x follow-up 4 — CloudManager ownership correction — 2026-09-10

### Фактический runtime отказ

Следующий пользовательский startup gate от 2026-09-10 18:43:14 завершился на:

```text
[T-FO06L] Pilot global startup refused: scene_preparation:persistent_bootstrap_service_runtime_location_mismatch:CloudManager
```

Перед отказом повторно получен audit `markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`.

### Read-only evidence and decision

- `CloudManager` найден root-level в `Assets/_Project/Scenes/BootstrapScene.unity`.
- У объекта нет parent; в Edit Mode `IsActive=false`.
- На root присутствуют `CloudManager`, `VeilRaymarchMeshController`, `NetworkObject` и `GlobalSceneSourceMarker`.
- Source ID: `336a190646b19bc46b22dd4e78f99800:909089444:0`.
- `CloudManager.Awake()` содержит `DontDestroyOnLoad(gameObject)`, но для inactive GameObject этот путь в текущем сохранённом runtime состоянии не выполняется.
- Поиск по проекту не выявил отдельного кода, который активирует именно этот root перед native preflight.

Поэтому текущая live location — authored BootstrapScene, а не DDOL. Не следует активировать CloudManager только для прохождения pilot gate: это изменило бы фактическую cloud/runtime семантику и могло бы скрыть ошибку классификации.

### Узкий фикс

`CloudManager` оставлен `treatment: Unmanaged`, но ownership изменён с `PersistentBootstrapService` на `BootstrapService`. Catalog/profile digest обновлён:

`4b028fe6a302e32510648f3716d86ec857bda85fb1fc77f6c6631f3aa3affce5`

Это не разрешает DDOL restoration и не меняет обработку обычного authored content, `SceneOwnedNetworkGameplay`, `ShipOrRigidbodyRoot` или unknown roots.

### Проверки и следующий gate

- Unity compile: **No compile errors**.
- `ValidateGlobalSceneCatalog.Run()`: **58 passed / 0 failed**.
- `ValidateGlobalSceneExecution.Run()`: **36 passed / 0 failed**.
- Runtime native binding, `StartHost()` и player spawn после этой коррекции остаются **UNVERIFIED**.
- Нормальный gameplay playtest по-прежнему не разрешён.

Следующий этап — новый пользовательский startup gate из canonical `Assets/_Project/Scenes/BootstrapScene.unity` с `Start Host`. Нужно получить следующий полный `[T-FO06L]` результат и проверить, прошёл ли native preflight дальше CloudManager. Только после полного startup gate с native preparation, `StartHost()`, player spawn и отсутствием duplicate/legacy scene takeover можно переходить к нормальному Host/player runtime playtest.

## 28. T-FO06L.2.x follow-up 5 — ServerWeatherController ownership correction — 2026-09-10

### Фактический runtime отказ

Следующий пользовательский startup gate, экспортированный 2026-09-10 в 18:47:03, завершился на:

```text
[T-FO06L] Pilot global startup refused: scene_preparation:persistent_bootstrap_service_runtime_location_mismatch:ServerWeatherController
```

Перед отказом повторно получен audit `markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`. Legacy scene loader был retired; до native preparation, `StartHost()`, local player spawn и duplicate/legacy scene verification выполнение не дошло.

### Read-only evidence and decision

- `ServerWeatherController` найден root-level в `Assets/_Project/Scenes/BootstrapScene.unity`.
- Root имеет `m_Father: {fileID: 0}`, `m_IsActive: 1` и baked marker `336a190646b19bc46b22dd4e78f99800:2074923228:0`.
- На root присутствуют `NetworkObject`, `ServerWeatherController` и `GlobalSceneSourceMarker`; `NetworkObject` остаётся authored scene object (`InScenePlaced=false`, `IsSpawned=false` до старта NGO).
- В текущей реализации `ServerWeatherController.OnNetworkSpawn()` устанавливает серверный singleton и запускает серверную логику, но сам скрипт не является доказательством DDOL ownership.
- Поэтому фактическая live location на native-preparation boundary — authored `BootstrapScene`, а не DDOL.

### Узкий фикс

`ServerWeatherController` оставлен `treatment: Unmanaged`, но ownership изменён с `PersistentBootstrapService` на `BootstrapService`. DDOL restoration, активация объекта и ослабление fail-closed проверки не добавлялись.

Новый catalog/profile digest:

`d3f6617d0626a442b2134b3052942fdc19bfe31633ba9c22fd07dc302539a3e6`

После фикса distribution ownership: `AuthoredSceneContent=38;BootstrapService=8;PersistentBootstrapService=27;SceneOwnedNetworkGameplay=55;ShipOrRigidbodyRoot=22`.

### Проверки и следующий gate

- Unity compile: **No compile errors**.
- `ValidateGlobalSceneCatalog.Run()`: **58 passed / 0 failed**.
- `ValidateGlobalSceneExecution.Run()`: **36 passed / 0 failed**.
- Catalog closed-world binding remains `catalog=150`; profile digest matches compiled catalog.
- Runtime native preparation, `StartHost()`, local player spawn, duplicate `WorldScene_0_0` and legacy scene-loader suppression после этой коррекции остаются **UNVERIFIED**.
- Нормальный gameplay playtest по-прежнему не разрешён.

Следующий шаг — новый пользовательский startup gate из canonical `Assets/_Project/Scenes/BootstrapScene.unity` с `Start Host`. Если native preflight пройдёт дальше, зафиксировать полную последовательность `catalog=150;markers=150;bound=150` → `StartHost()` → local player spawn → отсутствие duplicate `WorldScene_0_0` и legacy loader takeover.

## 29. T-FO06L.2.x follow-up 6 — QuestServer ownership correction — 2026-09-10

### Фактический runtime отказ

Свежий пользовательский startup gate, экспортированный 2026-09-10 в 18:55:14, завершился на:

```text
[T-FO06L] Pilot global startup refused:
scene_preparation:persistent_bootstrap_service_runtime_location_mismatch:[QuestServer]
```

Перед отказом legacy loader был retired; DDOL audit показал `markers=16;catalogedMarkers=16;persistentBootstrapRoots=16;restored=0`. Native preparation снова не завершилась, поэтому `StartHost()`, local player spawn и duplicate/legacy scene checks не выполнялись.

### Read-only evidence and decision

- `[QuestServer]` подтверждён как active root-level object в canonical `Assets/_Project/Scenes/BootstrapScene.unity`: `m_Father: {fileID: 0}`, `m_IsActive: 1`.
- На authored root присутствуют `NetworkObject`, `ProjectC.Quests.QuestServer` и `GlobalSceneSourceMarker`; baked source ID — `336a190646b19bc46b22dd4e78f99800:2129841567:0`.
- `QuestServer.cs` содержит только документационное указание на bootstrap lifecycle; отдельного `DontDestroyOnLoad` вызова в исходнике нет. Сохранённый object остаётся authored Bootstrap root на native-preparation boundary.
- Поэтому catalog entry не соответствует `PersistentBootstrapService`; это Bootstrap network service, который должен оставаться в canonical BootstrapScene.

### Узкий фикс

`[QuestServer]` оставлен `treatment: Unmanaged`, но ownership изменён с `PersistentBootstrapService` на `BootstrapService`. Объект не активировался и не перемещался; DDOL restoration, ослабление fail-closed native gate, `SceneOwnedNetworkGameplay` и `ShipOrRigidbodyRoot` не изменялись.

Новый catalog/profile digest:

`75d2e9d4b6e158e0cc523a446201e1bae891521d5636bc99b904ed6e0d2aecf2`

После фикса distribution ownership: `AuthoredSceneContent=38;BootstrapService=9;PersistentBootstrapService=26;SceneOwnedNetworkGameplay=55;ShipOrRigidbodyRoot=22`.

### Проверки и следующий gate

- Compiled catalog: `entries=150`.
- Profile digest matches compiled catalog: **True**.
- `ValidateGlobalSceneCatalog.Run()`: **58 passed / 0 failed**.
- `ValidateGlobalSceneExecution.Run()`: **36 passed / 0 failed**.
- Unity compile: **No compile errors**.
- Runtime native preparation, `StartHost()`, local player spawn, duplicate `WorldScene_0_0` and legacy scene-loader suppression после этой коррекции остаются **UNVERIFIED**.
- Нормальный gameplay playtest по-прежнему не разрешён.

Следующий шаг — новый пользовательский startup gate из canonical `Assets/_Project/Scenes/BootstrapScene.unity` с `Start Host`. Проверить, проходит ли native preflight дальше `[QuestServer]`; при успехе зафиксировать полную последовательность `catalog=150;markers=150;bound=150` → `StartHost()` → local player spawn → отсутствие duplicate `WorldScene_0_0` и legacy loader takeover.

## 30. T-FO06L.2.x follow-up 7 — batch Bootstrap service ownership correction — 2026-09-10

### Причина пакетной коррекции

Новый отказ `scene_preparation:persistent_bootstrap_service_runtime_location_mismatch:[GatheringServer]` показал, что runtime DDOL audit нельзя использовать как единственное доказательство authored ownership. Для сохранённых roots сопоставлены scene path, `NetworkObject`, source marker и наличие собственного `DontDestroyOnLoad` пути.

### Read-only evidence and decision

В текущем сохранённом catalog было `150` entries с distribution `AuthoredSceneContent=38;BootstrapService=9;PersistentBootstrapService=26;SceneOwnedNetworkGameplay=55;ShipOrRigidbodyRoot=22`. Из оставшихся persistent entries ровно десять являются authored Bootstrap `NetworkObject` services:

- `[ShipCargoServer]` — `336a190646b19bc46b22dd4e78f99800:1010490802:0`;
- `[CombatServer]` — `336a190646b19bc46b22dd4e78f99800:1052858226:0`;
- `[SkillsServer]` — `336a190646b19bc46b22dd4e78f99800:121334975:0`;
- `[DockingServer]` — `336a190646b19bc46b22dd4e78f99800:1286596461:0`;
- `[ExchangeServer]` — `336a190646b19bc46b22dd4e78f99800:1341219859:0`;
- `[NpcShipServer]` — `336a190646b19bc46b22dd4e78f99800:2047115830:0`;
- `[StatsServer]` — `336a190646b19bc46b22dd4e78f99800:299327984:0`;
- `[GatheringServer]` — `336a190646b19bc46b22dd4e78f99800:406607961:0`;
- `[CraftingServer]` — `336a190646b19bc46b22dd4e78f99800:467779776:0`;
- `[EquipmentServer]` — `336a190646b19bc46b22dd4e78f99800:690069688:0`.

`[QuestServer]` из заявленного класса уже был исправлен в follow-up 6 и оставлен `BootstrapService`; поэтому в этом batch изменены именно десять ещё persistent entries. Для всех сохранены `treatment: Unmanaged`, authored scene placement, active state и NGO lifecycle; DDOL restoration и native gate не ослаблялись.

### Узкий batch-фикс

Для десяти entries ownership изменён с `PersistentBootstrapService` на `BootstrapService`. Review notes зафиксировали общий invariant: authored root остаётся в canonical `BootstrapScene`, а отсутствие собственного DDOL пути не компенсируется runtime relocation.

После batch-коррекции фактическая distribution: `AuthoredSceneContent=38;BootstrapService=19;PersistentBootstrapService=16;SceneOwnedNetworkGameplay=55;ShipOrRigidbodyRoot=22`.

Переданный ранее ожидаемый итог `BootstrapService=20;PersistentBootstrapService=15` не выводится из текущего сохранённого catalog без дополнительного одиннадцатого source. Все оставшиеся `PersistentBootstrapService` entries, кроме этих десяти, не имеют observed `NetworkObject`; перевод любого из них в `BootstrapService` нарушит compiler invariant `requiresNetworkIdentity`. Поэтому дополнительный root не переклассифицировался без нового доказательства.

Новый catalog/profile digest:

`f6c2a4668b1fa3432432b5ab69bf7ae7500fd98293a6106446964958a0584954`

### Проверки и следующий gate

- Compiled catalog: `entries=150`.
- Profile digest matches compiled catalog: **True**.
- `ValidateGlobalSceneCatalog.Run()`: **58 passed / 0 failed**.
- `ValidateGlobalSceneExecution.Run()`: **36 passed / 0 failed**.
- Unity compile: **No compile errors**.
- Runtime native preparation, `StartHost()`, local player spawn, duplicate `WorldScene_0_0` and legacy scene-loader suppression после этой коррекции остаются **UNVERIFIED**.
- Play Mode и screenshots не выполнялись; нормальный gameplay playtest по-прежнему не разрешён.

Следующий шаг — один пользовательский startup gate из canonical `Assets/_Project/Scenes/BootstrapScene.unity` с `Start Host`. Зафиксировать фактический результат `catalog=150;markers=150;bound=150` и имя следующего блокера, если native preflight всё ещё остановится.

## 31. T-FO06L.2.x follow-up 8 — восстановить NGO PlayerPrefab auto-spawn — 2026-09-10

### Фактический runtime симптом

После batch ownership correction пользовательский Host прошёл native preparation и `StartHost()`, но runtime сообщил:

```text
[QuestServer] OnClientConnectedForSnapshot: client=0
[QuestServer] SendQuestSnapshotToClient: no NetworkPlayer for client 0
```

Одновременно отсутствовали подтверждение auto-spawn от `NetworkPlayerSpawner` и лог готовности local pilot player/скрытия startup menus. `NetworkTestMenu` показывал одного подключённого игрока, но это не доказывало наличие `ConnectedClients[0].PlayerObject`.

### Read-only причина

В `GlobalMotionNetworkStartup.Approve()` глобальный connection approval выставлял:

```csharp
response.CreatePlayerObject = false;
```

Это явно запрещало NGO создать PlayerObject через `NetworkConfig.PlayerPrefab`. При этом `TryApplyProfilePlayerPrefab()` уже устанавливает единственный spatial prefab профиля — `NetworkPlayer_GlobalPilot.prefab`, содержащий `NetworkObject`, `NetworkPlayer`, `GlobalMotionReplicator` и `GlobalMotionPoseAdapter`. Ручной `SpawnAsPlayerObject` не возвращался.

`QuestServer`-сообщение о `no NetworkPlayer` признано downstream-симптомом отсутствующего NGO PlayerObject, а не основанием для ручного spawn или ослабления startup gate.

### Узкий фикс

В `GlobalMotionNetworkStartup.Approve()` установлено `response.CreatePlayerObject = true`. Остальные approval-проверки, global hello, scene admission, catalog ownership и единственный `NetworkConfig.PlayerPrefab` path не изменялись.

### Проверки и границы

- Unity compile: **No compile errors**.
- Изменён только `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionNetworkStartup.cs` из runtime-кода; catalog, profile, scenes и prefabs не изменялись.
- Play Mode, screenshots и пользовательский Host runtime gate после фикса **не выполнялись автоматически**.
- Ручной `SpawnAsPlayerObject` не добавлялся.
- Unrelated `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` остаются вне этапа.

Следующий gate выполняет пользователь: новый Play Mode из canonical `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Нужны строки auto-spawn, local player readiness/menu handoff и отсутствие последующего disconnect. Если `QuestServer` всё ещё увидит `no NetworkPlayer`, следующий фикс должен разбирать фактический порядок NGO callbacks, не возвращая ручной spawn.

## 32. T-FO06L.2.x follow-up 9 — восстановить server-side player factory и отложить initial quest snapshot — 2026-09-10

### Фактический runtime отказ

После follow-up 8 пользовательский Host завершился внутри `StartHost()` на custom prefab handler:

```text
[T-FO04G] replica_factory:InvalidOperationException
InvalidOperationException: No prepared local frame for explicit spawn seed.
[Netcode] [NetworkObject] Player prefab is null! Cannot spawn player object!
```

Стек показал `GlobalMotionPlayerBootstrap.CreateReplica()` → NGO prefab handler → `HandleConnectionApproval`. `CreateReplica()` намеренно запрещает server-side путь при `_manager.IsServer`, поэтому `response.CreatePlayerObject = true` направлял host/server в client-side replica path и вызывал shutdown. `QuestServer` и `Player prefab is null` были downstream-симптомами отсутствующего PlayerObject.

### Узкий фикс

В `GlobalMotionNetworkStartup.Approve()` восстановлено `response.CreatePlayerObject = false`. Server-side PlayerObject снова должен создаваться единственным проектным путём `PeerConnected → GlobalMotionPlayerBootstrap.SpawnPlayer() → CreateInstance() → SpawnAsPlayerObject()`. `NetworkPlayerSpawner` не возвращает ручной spawn и остаётся диагностическим монитором.

В `QuestServer.OnClientConnectedForSnapshot()` initial quest snapshot переведён на bounded retry: до `30` попыток с интервалом `0.1` секунды проверяется фактический `ConnectedClients[clientId].PlayerObject`/`NetworkPlayer`; snapshot отправляется только после его появления. Остальные snapshot paths и загрузка состояния клиента не изменялись. При disconnect, shutdown или timeout retry завершается без отправки ложного snapshot.

### Проверки и границы

- Unity compile: **No compile errors**.
- `git diff --check`: **PASS**.
- Изменены только `GlobalMotionNetworkStartup.cs` и `QuestServer.cs` из runtime-кода; каталог, профиль, сцены и prefab assets не изменялись.
- `LiberationSans SDF - Fallback.asset` и `ProjectSettings/EditorSettings.asset` остаются посторонними рабочими изменениями и в этап не входят.
- Play Mode, screenshots и пользовательский Host runtime gate не выполнялись автоматически.

Следующий gate выполняет пользователь: новый Play Mode из canonical `Assets/_Project/Scenes/BootstrapScene.unity` → `Start Host`. Ожидаемая последовательность: `PeerConnected(0)` → `SpawnPlayer` → `CreateInstance` → `SpawnAsPlayerObject` → `ConnectedClients[0].PlayerObject != null` → quest snapshot sent → local pilot ready/menu handoff. Строки `replica_factory`, `Player prefab is null` и последующий disconnect должны отсутствовать.

## 33. T-FO06L.2.x follow-up 10 — успешный native startup/player gate — 2026-09-10

### Результат пользовательского runtime gate

Пользовательский Play Mode-прогон из canonical `BootstrapScene` после нажатия `Start Host` прошёл native preparation, Host startup и server-side player factory. Exported Console Log создан `2026-09-10 20:05:41`.

Подтверждённая последовательность:

```text
[T-FO06G] Native scene readiness: ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>
[NetworkTestMenu] Сервер запущен на порту 7777
[NMC] HandleClientConnected: clientId=0, IsServer=True, IsClient=True
[T-FO06G] PeerConnected queued: client=0;...;worldRunning=True;scenePrepared=True;sceneReady=True
[T-FO06G] Spawn plan ready: client=0;frame=1;position=Global(39992, 1, 40000)
[T-FO06G] SpawnPlayer entered: client=0;frame=1;position=Global(39992, 1, 40000)
[T-FO06G] OnActorPostSpawn entered: object=NetworkPlayer_GlobalPilot(Clone)
[T-FO06G] CompletePlacement finished: object=NetworkPlayer_GlobalPilot(Clone);ready=True;baseline=True
[T-FO06G] SpawnAsPlayerObject called: client=0;object=NetworkPlayer_GlobalPilot(Clone)
[T-FO06L] Local pilot player ready; startup menus hidden=2;origin=(0,0,0);local=(39992.00, 1.00, 40000.00)
```

Это закрывает текущий startup gate:

- native closed-world readiness — **PASS**: `cataloged nodes=150`, `recorded=150`, `pending=0`, `unspawned=0`, `retired=0`, blocker отсутствует;
- Host/client startup и локальный client `0` — **PASS**;
- server-side explicit player spawn — **PASS**;
- `NetworkPlayer_GlobalPilot` post-spawn placement и baseline — **PASS**;
- startup menu handoff — **PASS**: обработаны `2` стартовых меню;
- initial quest snapshot — **PASS по последовательности**: ранний callback до появления PlayerObject дождался игрока, затем отправлен snapshot с `1` quest.

В предоставленном логе не зафиксирован повторный `WorldScene_0_0`/neighbor load или `replica_factory` отказ. Это подтверждает отсутствие наблюдаемого duplicate/legacy takeover в данном прогоне, но не заменяет отдельный счётчик loaded-scene handles.

### Границы результата

Лог не является доказательством grounding, устойчивой опоры, движения, камеры, physics/rebase transaction или устранения jitter. Начальный frame по-прежнему имеет `origin=(0,0,0)`, а player находится примерно в `(39992, 1, 40000)` local coordinates; floating-origin rebase ещё не реализован.

Сессия завершилась последующим disconnect после открытия EscMenu. По предоставленной последовательности это выглядит как завершение пользовательского Play Mode, а не как startup failure; отдельного stack trace причины остановки в экспортированном логе нет.

### Незакрытые runtime-наблюдения

Следующие предупреждения не были причиной native admission blocker, но требуют отдельных этапов диагностики:

- повторяющиеся `ShipDeckNav`: `Failed to create agent because it is not close enough to the NavMesh`;
- `PlayerTarget`: `HP init FAILED after 20 retries for client=0`;
- missing script warning на одном Behaviour;
- `NpcSocialBrain`: отсутствуют Animator parameters `WorkVariant` и `Work`;
- `ShipCargoVisual`: пустые `_boxPrefabs`;
- resource node warnings о `_resultItem`/`_requiredTool`;
- ранние `ShipHull`/`ShipOwnershipRequirement` warnings до появления соответствующих server registries.

Их причинная связь с T-FO06L startup не доказана этим логом; они не должны исправляться массово в рамках текущего commit.

### Изменение и проверки этапа

На этапе сохранены узкие изменения T-FO06G:

- `GlobalSceneNativeExecutor` не записывает и не блокирует readiness для catalog-bound `Unmanaged` источников;
- `GlobalMotionPlayerBootstrap` логирует очередь, plan, explicit spawn, post-spawn и baseline;
- `NetworkManagerController` логирует явный путь shutdown для следующей диагностики.

Проверки:

- пользовательский Play Mode startup/player gate — **PASS по Console Log**;
- Unity compile до runtime gate — **No compile errors**;
- screenshots — **не предоставлены**;
- grounding, camera, movement, physics, multiplayer client и jitter — **UNVERIFIED**;
- `GroundPlane_0_0` не восстанавливался; catalog/profile/digest не изменялись.

Следующий этап общего плана — read-only classification actual city render/collider content, `Respawn_Default`, player/camera roots и ship/Rigidbody boundaries перед проектированием согласованной rebase transaction. Не выполнять общий `SetParent`, player-only origin shift или новый Play Mode автоматически.

## 34. T-FO06 — read-only rebase boundary census — 2026-09-10

### Подтверждённые границы

Read-only исследование проекта после успешного startup gate установило следующие границы:

- `Assets/_Project/Scenes/World/WorldScene_0_0.unity/WorldRoot_0_0` — основной hierarchy anchor сцены; под ним находятся static city render/collider content и ship-related scene structures.
- `Respawn_Default` — отдельный root-level marker в `WorldScene_0_0`, с authored global position около `(39992, 0, 40000)`; он не является дочерним объектом `WorldRoot_0_0`.
- `Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab` — текущий pilot player с `NetworkObject`, `NetworkPlayer`, `GlobalMotionPoseAdapter`, `GlobalMotionReplicator` и `CharacterController`.
- legacy `Assets/_Project/Prefabs/NetworkPlayer.prefab` использует `NetworkTransform` и не должен автоматически смешиваться с frame-aware pilot path.
- `MainCamera` находится в Bootstrap hierarchy; camera follow/rebase coupling пока не подтверждён по точной runtime-иерархии.
- Ship roots имеют отдельные `Rigidbody`/`NetworkObject` boundaries; `ShipDeckNav` относится к физической структуре соответствующего корабля и не является независимым static-world root.

### Следствие для rebase design

Общий `SetParent` для `WorldRoot_0_0`, ship/Rigidbody roots и scene-placed NetworkObject не принимается. Минимальная будущая transaction должна отдельно определить:

1. static city render/collider group под `WorldRoot_0_0`;
2. `Respawn_Default` и остальные authored positional anchors;
3. pilot player и `GlobalMotionPoseAdapter` frame state;
4. `MainCamera`/camera follow state;
5. каждый `ShipOrRigidbodyRoot` вместе с Rigidbody, NetworkObject, NetworkTransform/ship services и attached `ShipDeckNav`;
6. scene-owned gameplay roots, включая docking stations, NPC, pickups, chests и resource/crafting objects.

### Что осталось неопределённым

Read-only census не дал достаточных данных для implementation transaction:

- точные AABB/коллайдерные bounds полного city mesh относительно `Respawn_Default`;
- полный проверенный список transform/AABB всех `ShipOrRigidbodyRoot` и их deck collider/navmesh extents;
- точная runtime camera-follow hierarchy и ownership камеры;
- для каждого ship root — authored scene placement против dynamic spawn/reconciliation path;
- единый момент rebase относительно NGO tick, physics step и `GlobalMotionPoseAdapter` baseline.

Эти пункты отмечены как **INCONCLUSIVE**, а не заполняются предположениями. До их закрытия код rebase не изменяется и Play Mode автоматически не запускается.

### Проверки этапа

- Исследование выполнено read-only; сцены, префабы, catalog/profile и runtime code не изменялись.
- `WorldRoot_0_0`/`Respawn_Default`/pilot prefab/legacy prefab/camera/ship boundaries зафиксированы как следующие design inputs.
- Текущий commit startup gate: `28a412f5`.
- Этот census требует отдельной документационной фиксации; implementation transaction ещё не проектировалась.

## 35. T-FO06 — exact coordinate/rebase census refinement — 2026-09-10

### Подтверждённые числовые данные

- `WorldRoot_0_0`: world position `(0,0,0)`, rotation identity, scale `(1,1,1)`.
- `Respawn_Default`: root-level world position `(39992,0,40000)`, rotation identity, scale `(1,1,1)`.
- Относительная разница `Respawn_Default - WorldRoot_0_0`: `(39992,0,40000)`; расстояние от нулевой точки — примерно `56571` Unity units.
- `Respawn_Default` содержит только Transform/marker и не имеет собственного render/collider AABB; его bounds практически точечные.
- `WorldRoot_0_0` имеет `8` прямых дочерних структур, но агрегированный AABB всех descendants read-only инструментами не получен.

### Координатные и сетевые контракты

- `NetworkPlayer_GlobalPilot.prefab` содержит `GlobalMotionReplicator` и `GlobalMotionPoseAdapter`; legacy `NetworkPlayer.prefab` содержит `NetworkTransform` и не содержит `GlobalMotionPoseAdapter`.
- `GlobalMotionPoseAdapter.Bind()` регистрирует actor через `GlobalMotionWorld.RegisterActor`; `GlobalMotionWorld` хранит actors в `Dictionary<ulong, GlobalMotionPoseAdapter>` и frame registration привязана к текущему NGO run/PhysicsScene.
- `GlobalMotionWorld` прямо документирует, что регистрация не двигает content и что изменение populated frame требует отдельной rebase transaction.
- `FloatingOriginMP` — отдельный legacy shift controller на `MainCamera.prefab`. Он использует heuristic `worldRootNames`, исключает player/camera roots, имеет `threshold=150000` и `shiftRounding=10000`, вызывает `ApplyShiftToAllRoots()` и `OnWorldShifted`.
- `FloatingOriginMP.ApplyWorldShift()` применяет переданный offset напрямую, без проверки threshold; server/client mode, physics handling и NetworkTransform correction остаются отдельными responsibilities.

### Dynamic ship boundary

Статический census не дал достоверного полного списка runtime ship instances с `Rigidbody + NetworkObject + ShipDeckNav`. Это **INCONCLUSIVE**: ships, deck navmesh и часть colliders могут создаваться/регистрироваться после загрузки сцены. Подтверждено только требование `ShipController` к `Rigidbody` и `NetworkObject` и необходимость рассматривать каждый ship root вместе с его deck/navmesh structure.

### Почему implementation пока не начинается

Точные агрегированные city AABB/collider bounds, полный ship/deck bounds, фактический camera-follow owner и момент синхронизации rebase с NGO tick/physics/baseline не подтверждены. Поэтому существующий `FloatingOriginMP` нельзя принять как готовую T-FO06 transaction: его root-name heuristic и direct all-roots shift не доказывают closed-world ownership для текущего pilot.

Следующий шаг — отдельный read-only runtime-independent census по prefab/scene asset bounds и исходным ownership markers; до получения этих данных код rebase не менять и Play Mode автоматически не запускать.
