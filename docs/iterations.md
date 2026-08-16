# Iterations

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
