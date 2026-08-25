# T-UI10 — Контекстные подсказки взаимодействия

**Дата документа:** 25 августа 2026 года  
**Статус:** реализовано и подтверждено пользователем в Play Mode; локализация добавлена аддитивно.
**Связанный постмортем:** `docs/world/Localization/06_POSTMORTEM_T-UI09_Localization_Blast_Radius.md`

## 1. Цель

При приближении локального игрока к объекту взаимодействия в нижней правой части экрана, но не вплотную к краю, показывается одна короткая контекстная подсказка:

- NPC: `Press E to talk`;
- объектам, у которых текущий runtime-flow использует F: `Press F to use`.

Подсказка только сообщает уже существующее действие. Она не должна менять обработчики ввода, сетевые RPC, приоритеты взаимодействия или серверную валидацию.

## 2. Фактический input-flow, который нельзя сломать

Источник истины для клавиш — текущая логика `NetworkPlayer.Update` и `InputBindingsConfig`, а не новая система ввода.

| Объект | Текущий flow | Подсказка T-UI10 |
|---|---|---|
| `NpcController` | E → `TryInteractNearestNpc` → `QuestServer.RequestTalkToNpcRpc` | `ui.interaction_hint.talk` / E |
| Корабль при посадке | F → `FindNearestShip` → проверка владения/ключа | `ui.interaction_hint.use` / F |
| `PickupItem` | F → `TryPickup` | `ui.interaction_hint.use` / F |
| `NpcLootPickup` | F → `TryPickup` | `ui.interaction_hint.use` / F |
| `ResourceNode` | F → `TryGatherNearestNode` → `MetaRequirement` | `ui.interaction_hint.use` / F |
| `CraftingStation` | F → `TryInteractNearestCraftingStation` | `ui.interaction_hint.use` / F |
| `ShipCargoConsole` | F → `TryInteractNearestShipCargoConsole` | `ui.interaction_hint.use` / F |
| `DoorController` | F → `TryInteractNearestDoor` | `ui.interaction_hint.use` / F |

В коде также есть E-flow для `MetaRequirement`, `RepairManager`, `NetworkChestContainer` и fallback рынка. T-UI10 не переводит эти flow на F и не показывает для них ложную F-подсказку. Отдельный универсальный контракт «любой не-NPC объект использует F» потребует отдельного решения по input-flow.

## 3. Архитектурное решение

### 3.1 Runtime

- Не добавлять новые `InputAction` и не читать `Keyboard.current.eKey/fKey` из UI.
- Не менять `IInteractable`: сейчас он содержит только `InstanceId`, `DisplayName`, `InteractionRadius` и `Position`, поэтому добавление hint-поля во все interactable-классы избыточно для минорной фичи.
- Состояние подсказки вычислять централизованно на owner-клиенте в существующем `NetworkPlayer`, используя те же диапазоны и проверки, что и реальные обработчики E/F.
- Для UI передавать только состояние `None`, `Talk` или `Use` либо эквивалентный компактный результат резолвера.
- Обновлять UI только при изменении состояния/цели, а не записывать текст в TMP каждый кадр.
- Если одновременно обнаружены разные кандидаты, приоритет должен повторять текущий порядок обработки F/E в `NetworkPlayer.Update`: сначала действительный F-candidate, затем NPC для E. Нельзя выбирать объект только по наличию коллайдера.
- При `_inShip`, отсутствии локального игрока, неактивном/невалидном target или выходе из радиуса состояние становится `None`.

### 3.2 UI

`ControlHintsUI` уже существует как legacy TMP HUD и управляет постоянной памяткой клавиш по F1. Его текущая памятка не переписывается и не заменяется контекстной строкой.

Рекомендуемый вариант для T-UI10:

1. Сохранить `ControlHintsUI` как единственный контроллер этого HUD.
2. Добавить в его prefab отдельный TMP-элемент для контекстной подсказки.
3. Разместить этот элемент отдельным `RectTransform` в нижней правой зоне safe area; существующий статический блок не двигать.
4. Скрывать контекстный элемент при `None`.
5. F1 продолжает управлять существующей постоянной памяткой; контекстная подсказка не должна случайно исчезать из-за переключения статического блока.
6. Вся строка контекстной подсказки локализуется через `ProjectC.Localization.Loc`.

Для динамической смены `Talk`/`Use` не использовать постоянный bind на один ключ. Контроллер хранит текущее состояние, вызывает `Loc.Get` при смене состояния и повторяет это при `Loc.OnLocaleChanged`.

## 4. Ключи локализации

Добавляются ровно две записи в `UI_Table`:

| Key | RU | EN | Fallback в коде |
|---|---|---|---|
| `ui.interaction_hint.talk` | `Нажмите E, чтобы поговорить` | `Press E to talk` | RU-текст |
| `ui.interaction_hint.use` | `Нажмите F, чтобы использовать` | `Press F to use` | RU-текст |

Переводы остальных существующих локалей (`de`, `es`, `fr`, `hi`, `ja`, `pt`, `zh`) добавляются только согласованными значениями. Пустая ячейка допустима и должна проходить по существующей fallback-цепочке `translation → ru → passed literal → key`.

### Обязательные правила локализации

- Добавлять записи только аддитивно.
- Использовать существующую коллекцию `Assets/_Project/Settings/Localization/UI_Table.asset` и её `UI_Table Shared Data`.
- Источник/экспорт: `Assets/_Project/Localization/Export/UI_Table.csv`.
- Не очищать `SharedData`, `m_TableData` или локальные таблицы.
- Не запускать `ProjectC/Localization/Rebuild Tables (SharedData fix)`.
- Не изменять `Static_Table`, `System_Table` или `Dialogue_Table`.
- Не редактировать YAML локализационных ассетов вручную.
- После добавления проверить, что в `UI_Table Shared Data` стало ровно на две записи больше, а существующие ключи и ID не изменились.

## 5. Затрагиваемые файлы

Планируемый минимальный scope:

- `Assets/_Project/Scripts/UI/ControlHintsUI.cs`;
- `Assets/_Project/Prefabs/ControlHintsUI.prefab` или соответствующий scene instance;
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`;
- `Assets/_Project/Localization/Export/UI_Table.csv`;
- `Assets/_Project/Settings/Localization/UI_Table.asset`;
- `Assets/_Project/Settings/Localization/UI_Table Shared Data.asset`;
- локальные `UI_Table_*.asset` только через официальный Unity Localization API/окно.

Не входят в scope:

- `Assets/_Project/Editor/Localization/LocalizationTableRepair.cs`;
- любые repair/rebuild всех четырёх коллекций;
- изменение `IInteractable` и всех классов interactable;
- изменение E/F input-flow;
- серверные RPC и сетевые DTO;
- world-space labels над объектами;
- автоматические playtests или screenshots со стороны агента.

## 6. Acceptance criteria

1. Вне радиуса подсказка скрыта.
2. У NPC в радиусе отображается `Press E to talk` на английской локали и соответствующий RU-текст на русской.
3. У корабля, pickup, resource node, crafting station, cargo console и двери в их текущем F-flow отображается `Press F to use`.
4. Подсказка исчезает после выхода из радиуса, отключения объекта или перехода в корабль.
5. Переключение локали обновляет уже видимую подсказку без перезапуска сцены.
6. Существующая памятка `ControlHintsUI` и F1-переключение остаются работоспособными.
7. E/F обработчики, приоритеты, MetaRequirement и серверная валидация не изменились по поведению.
8. Компиляция проходит без ошибок.
9. В `UI_Table` добавлены только две новые записи; другие коллекции и существующие SharedData ID не затронуты.
10. Финальное поведение подтверждается пользовательским Play Mode-тестом и screenshots; до этого этап нельзя помечать завершённым.

## 7. Главный урок T-UI09

Эта фича не требует глобальной миграции локализации. `Loc.Get` уже предоставляет fallback, а `ControlHintsUI`/`NetworkPlayer` могут быть изменены независимо от Static/System/Dialogue-таблиц.

Любое расхождение при добавлении двух ключей — причина остановить операцию и откатить только файлы T-UI10. Нельзя исправлять локализацию запуском полного rebuild из частичного `UIKeyMap`.

## 8. Фактический результат T-UI10

- Реализованы контекстные состояния `None`, `Talk`, `Use` в `ControlHintsUI` и owner-only resolver в `NetworkPlayer`.
- F/E input-flow, RPC, приоритеты взаимодействия и серверная валидация не изменялись.
- Добавлены только два ключа UI-локализации: `ui.interaction_hint.talk` и `ui.interaction_hint.use`.
- `UI_Table Shared Data`: 421 → 423; существующие UI ID сохранены.
- `Static_Table`, `System_Table` и `Dialogue_Table` не перестраивались; их baseline digests сохранены.
- Unity compile check: `No compile errors`.
- Play Mode и screenshots подтверждены пользователем как успешно пройденные.
