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
