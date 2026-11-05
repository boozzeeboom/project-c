# Iterations

## 05 ноября 2026

**Тикет:** T-CAM22
**Тема:** SpringArm дёргается на движущейся платформе (паром) — персонаж ровно, камеру мотает.
**Причина:** (1) на клиенте кабинку двигает `NetworkVariable<float> _netS` тиком сети (30 Гц) → позиция цели ступенчатая; `UpdateLag` считал скорость как остаток пружины (`0↔18 м/с` каждый тик) → дребезг `dynamicLag`/`vertBoost` + каскад exp-фильтров; (2) anti-pop держал точку в мировых координатах — на 9 м/с она уезжает от игрока, а stale-guard 0.35 м истекает за 40 мс → камера прыгала между стеной и свободной позицией каждый кадр.
**Что сделано:** лаг читает интерполированный по истории сэмпл цели (`targetSignalInterpolation`, `targetSignalDelay` 0.07 с ≥ 1 тика); скорость цели — по сглаженному сэмплу, а не по остатку пружины; anti-pop держится относительно цели (`antiPopFollowTarget`) и только окном по времени. Ship-режим — без задержки. FO-контракт: история едет в `TryApplyGlobalMotionCameraTranslation`, сбрасывается на restore, новые точки в finite-check.
**Файлы:**
- `Assets/_Project/Scripts/Core/SpringArmCamera.cs`
- `docs/dev/T-CAM22_CAMERA_MOVING_PLATFORM.md` (новая нота)
- `docs/Character/ThirdpersonCamera/CHANGELOG.md`

**Проверено:**
- Compile (MCP): 0 ошибок компиляции.
- Play Mode / ручная проверка — за пользователем (агент Play Mode не запускает). Шаги — в §5 ноты.

## 05 ноября 2026

**Тикет:** T-PAROM-24
**Тема:** Отладочный предвизуал кабинки на станциях — чтобы настраивать высоту точек маршрута и строить платформы посадки.
**Что сделано:** в инспекторе `ParomRoute` блок «Предвизуал кабинки (отладка)»: кнопка «👁 Показать префаб на станциях (пересоздать)» ставит инстансы `Trolley Prefab` на каждую станцию в игровой позиции (`якорь + Cabin Hang Depth`, yaw вдоль сегмента), повторное нажатие пересоздаёт; «Убрать предвизуал» удаляет. Дублируется пунктами меню `ProjectC → Ships → Parom (Ferry) → Предвизуал … (Selection)`.
**Безопасность:** превью — дети `__ParomPreview` с `HideFlags.DontSave` (в сцену/билд не попадают, сцена не dirty), коллайдеры выключены, `ParomTrolley` не добавляется (рантайм не подхватит). В Console печатаются `y` якоря, `y` кабинки и точка посадки (верх BoxCollider + 0.3).
**Файлы:**
- `Assets/_Project/Scripts/World/Parom/Editor/ParomRoutePreview.cs` (новый)
- `Assets/_Project/Scripts/World/Parom/Editor/ParomRouteEditor.cs`
- `docs/world/parom_road/00_PAROM_DESIGN.md`

**Проверено:** на `WorldScene_0_0` / `ParomRoute_01` — создались 2 превью (start/end), повторное нажатие даёт те же 2 (без дублей), `Clear` удаляет, сцена остаётся не dirty; компиляция 0 ошибок.

## 05 ноября 2026

**Тикет:** T-PAROM-23
**Тема:** `Trolley Prefab` игнорировался — на маршруте оставалась кубическая заглушка.
**Причина:** `ParomRoute.EnsureTrolley()` сначала подхватывал любого ребёнка с `ParomTrolley` и выходил, не глядя на `_trolleyPrefab`. В `WorldScene_0_0` такая заглушка запечена у `ParomRoute_01` и `ParomRoute_02`: `Box_cargo_Parom` (built-in Cube, scale 3.58×1.41×7.71, материал `M_PC_Concrete`, kinematic Rigidbody, BoxCollider) — отсюда «просто куб блок с материалом» в Play.
**Исправление:** поле `Trolley Prefab` теперь в приоритете — если оно непустое, запечённая кабинка-ребёнок отключается и удаляется в `Awake` (+ warning в консоль); запечённая используется только при пустом поле. `ParomRouteEditor` предупреждает о таком дубле.
**Изменённые файлы:**
- `Assets/_Project/Scripts/World/Parom/ParomRoute.cs`
- `Assets/_Project/Scripts/World/Parom/Editor/ParomRouteEditor.cs`
- `docs/world/parom_road/00_PAROM_DESIGN.md`

**Проверки:**
- Compile (MCP): 0 ошибок компиляции.
- Play Mode / визуал: за пользователем (агент Play Mode не запускает).

## Материалы от 04 октября 2026

**Тикет:** T-STEELV2
**Задача:** Сделать сталь v2 нодовым Shader Graph (рядом с `Steel.shadergraph`): процедурные потёртости на гранях через шум, ржавчина через шум и переходные состояния; создать материал и назначить граф-шейдер.
**Результат:** Создан `Steel_v2.shadergraph` (`Shader Graphs/Steel_v2`). Ядро шума — **Custom Function `ProceduralSteelNoise`** (object-space **3D** fbm 3 октавы + Voronoi 27 ячеек + domain warp) — корректный шум на всех 6 гранях (без растяжения 2D UV-нод) и с хаосом; остальное — ноды (маски worn/transition/rust, кромочная маска, цвета, металлик/гладкость). **18 параметров выведены в Blackboard** (4 цвета, Metallic/Smoothness/Rust/Worn Smoothness, Noise Scale/Detail/Cell/Warp/Seed, Rust Amount/Threshold, Transition Threshold, Wear Amount, Edge Width); граф 68 связей. Материал `M_PC_Steel_v2.mat` с дефолтами.
**Изменённые файлы:**
- `Assets/_Project/Materials/Steel/Steel_v2.shadergraph` (+ `.meta`)
- `Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat` (+ `.meta`)
- `Assets/_Project/Shaders/ProceduralSteelNoise.hlsl`
- `docs/Materials/SteelGraphV2/README.md`
- `docs/Materials/README.md`

**Проверки:**
- Импорт через anklebreaker MCP: шейдер `supported=True`, `ShaderHasError=false`, 0 warnings.
- Visual и F8/F9 — NOT RUN (агент Play Mode и скриншоты не делает), за пользователем.
- Свойства материала выведены в инспектор (18 шт.): заданы в ассете графа, Property-ноды подключены через graph-инструменты (сам Blackboard инструмент создавать не умеет).
- Исправлен баг первой сборки: цветовые Vector4-ноды были сдвинуты на компонент (X=0) → материал отдавал зелёный; заменены на Property-ноды с корректными дефолтами.
- Ядро шума переведено с 2D Noise-нод на Custom Function 3D (fbm+Voronoi+domain warp): устранено растяжение шума на 4 гранях куба и «ровность» паттерна; добавлены настройки шума (Scale/Detail/Cell/Warp/Seed).
- Устранены артефакты-«сетка/тайлы»: хеш без `sin` (Hoskins) + гладкий domain warp (интерполированный, не `floor`); ядро вынесено в `Assets/_Project/Shaders/ProceduralSteelNoise.hlsl` (Custom Function в режиме File → `#include`).

## Завершение от 25 августа 2026

**Тикет:** T-UI10
**Коммит:** `fd960e6a` — T-UI10: Добавить контекстные подсказки взаимодействия
**Задача:** Добавить контекстные подсказки взаимодействия для NPC и F-flow объектов без изменения существующей input/network логики и без повторной перестройки локализации.
**Результат:** Реализованы `Talk`/`Use` hints в runtime HUD; resolver работает owner-only и сохраняет приоритеты текущего `NetworkPlayer.Update`. Добавлены только `ui.interaction_hint.talk` и `ui.interaction_hint.use` через официальный Localization API.
**Изменённые файлы:**
- `Assets/_Project/Scripts/UI/ControlHintsUI.cs`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
- `Assets/_Project/Settings/Localization/UI_Table Shared Data.asset`
- `Assets/_Project/Settings/Localization/UI_Table_ru.asset`
- `Assets/_Project/Settings/Localization/UI_Table_en.asset`

**Проверки:**
- Unity compile check: `No compile errors`.
- `UI_Table Shared Data`: 421 → 423; существующие UI ID сохранены.
- `Static_Table`, `System_Table`, `Dialogue_Table`: без изменений и без rebuild.
- RU/EN lookup для обеих новых строк возвращает ожидаемые значения.
- Play Mode и screenshots подтверждены пользователем.

## Итерация от 15 августа 2026

**Задача:** Настройка первого квеста-гайда Onboarding alfa для проверки NPC, диалогов, квестовых стадий, локализации и выдачи ключа корабля.
**Коммит:** `f9eac76d7149ec33bd97c31fdb6a9bdc4cd1e275` — T-QST01: Добавлен первый квест-гайд Onboarding alfa
**Изменения:**
- Созданы QuestDefinition `onboarding_alfa`, NpcDefinition `OnboardingAlfa` и DialogTree `OnboardingAlfaDialog`.
- Onboarding alfa размещён на крыше `Ангар1 средняя часть.001` как экземпляр канонического NPC-префаба.
- Обновлён `MiraDefault`: маршрут через `RepairManager`, `MarketZone_Primium` и возвраты к Мира.
- Зарегистрированы новые NPC, диалог и квест в `QuestDatabase`.
- Добавлены русские и английские строки в `Dialogue_Table`.
- Исправлена выдача квестовых предметов с сохранением `ItemType`, включая `Key`.
- Серверный диалог теперь локализует текст реплик, имена NPC и варианты ответа.
- Проверка графов и компиляции пройдена без ошибок.

## Коррекция от 16 августа 2026

**Задача:** Перевести возвраты к перемещающейся Mira на `TalkToNpc` и обновить внутренний гайдлайн первого onboarding-квеста.
**Коммит:** `ddfa001c08bf184a4fa0df0e0f9817eb65f01e5b` — T-QST02: Исправлен возврат к Mira через TalkToNpc
**Изменения:**
- В `onboarding_alfa` этапы `return_from_repair` и `return_from_market` используют `TalkToNpc` с `mira_01` и `Mira.asset`.
- Статичные точки `RepairManager` и `MarketZone_Primium` оставлены на `ReachLocation`.
- Гайдлайн сохранён в `docs/dev/TESTS/first-auto-quest/README.md` и дополнен правилом выбора objective по типу цели.
- Статическая проверка Unity: `No compile errors`.

## Исправление от 16 августа 2026

**Задача:** Исправить выдачу квестового ключа корабля, уникальность ключей кораблей и runtime-загрузку ItemRegistry.
**Коммит:** `cae0591bf121a9578124b492d2d66f42a02a962c` — T-KEY01: Исправлена выдача ключей кораблей и runtime-регистрация
**Изменения:**
- Key-награды теперь передают уникальный `KeyRodInstance` игроку и сохраняют `instanceId` в инвентаре.
- Добавлена миграция legacy-слотов с `instanceId=0` и защита от неоднозначной привязки ключа к кораблю.
- `NPC_Ship_HeavyII_03` получил отдельный `Key_heavyII_ship`; `Key_light_ship` остался только у `Ship_Light_root`.
- `ItemRegistry.asset` перемещён в `Resources/Items/Data` и зарегистрирован новый ключ с ID 2012.
- Проверка Unity: `No compile errors`; статическая проверка ассетов пройдена.

## Исправление от 16 августа 2026

**Задача:** Исправить выход из сетевой игры в главное меню без перезагрузки BootstrapScene и сохранить позицию игрока при следующем `StartHost()`.
**Коммит:** `f208134d5dc9aa6019b89dd6cc838ce1b483fcc9` — T-PERSIST01: Исправить сохранение позиции при выходе в меню
**Изменения:**
- Убран возврат через повторную загрузку `BootstrapScene`; выход теперь останавливает NGO, сбрасывает `ClientSceneLoader` и показывает bootstrap-resident `MainMenuWindow`.
- Добавлено принудительное сохранение `ShipPositionServer.SaveNow()` до `NetworkManager.Shutdown()`, пока объекты игрока и кораблей ещё существуют.
- Сохранение игрока и кораблей вынесено в общий `SaveCurrentState()`, чтобы периодический и принудительный save использовали один поток данных.
- При каждом новом старте сервера сбрасываются `_restoreCompleted`, `PlayerPositionServer.DataLoaded` и старый кэш сохранённых игроков; ранняя запись до завершения restore блокируется.
- Исправлен сброс состояния потоковой загрузки мировых сцен и ожидание завершения teardown перед возвратом в меню.
- Добавлено предупреждение о остановке host-сервера в `ui.esc_menu.exit_confirm` для 9 локалей; обновлены `LocalizationTableRepair` и локализационные YAML-таблицы.
- Исправлены compile-проблемы с `Scene` namespace/type и неоднозначным `Cursor`.
- Изменённые скрипты: `NetworkManagerController.cs`, `ShipPositionServer.cs`, `PlayerPositionServer.cs`, `EscMenuWindow.cs`, `MainMenuWindow.cs`, `ClientSceneLoader.cs`.
- Изменённые данные локализации: `LocalizationTableRepair.cs`, `UI_Table_de/en/es/fr/hi/ja/pt/ru/zh.asset`.
- Проверка Unity: `No compile errors`; ручная проверка полного runtime-цикла выхода и повторного запуска хоста ожидает подтверждения пользователя.

## Исправление от 16 августа 2026

**Задача:** Исправить ситуацию, когда позиция игрока загружается из `ShipPositions.json`, но затем перезаписывается стандартным телепортом `ClientSceneLoader` или stale-состоянием предыдущего host-сеанса.
**Коммит:** `d409c0e4b44357df5baa1394c6fe7c404fbc656a` — T-PERSIST02: Исправить порядок восстановления позиции игрока
**Изменения:**
- Перед каждым новым `StartHost()`/`StartServer()` сбрасываются `_restoreCompleted`, `DataLoaded`, сохранённый список игроков и pending-состояние.
- `NetworkPlayer` теперь ждёт завершения полного restore кораблей и игроков, а не только загрузки списка игроков.
- `ClientSceneLoader` после загрузки стартовых сцен ждёт server restore и пропускает default spawn, если для текущего clientId найдена сохранённая позиция.
- Устранена гонка, подтверждённая логами: после корректного `Player 0 restored to position (39821.23, 2532.83, 39999.52)` выполнялся последующий `Teleport to (39999.50, 3000.00, 39999.50)`.
- Проверка Unity: `No compile errors`. Runtime-проверку после нового коммита необходимо повторить.
