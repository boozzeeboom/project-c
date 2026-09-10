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
