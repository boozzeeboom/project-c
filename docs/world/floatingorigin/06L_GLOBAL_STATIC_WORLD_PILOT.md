# T-FO06L — global static-world pilot: текущий этап интеграции

Дата: 2026-09-09. Предыдущий этап: T-FO06K.

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
