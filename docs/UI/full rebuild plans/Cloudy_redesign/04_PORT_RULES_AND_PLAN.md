# 04 — Правила переноса и пошаговый план

## 1. CSS → USS: что нельзя и чем заменять

Таблица проверена на порте меню (`docs/UI/MainMenu/02_smoke_menu_port.md`, «Пределы платформы»), дополнена уроками
`03_visual_fidelity_pass.md`. Для этого окна это **обязательные правила**, а не советы.

| В моде | В USS | Замена |
|---|---|---|
| `linear-gradient` / `radial-gradient` | нет | процедурная `Texture2D` с гамма-коррекцией (как `ProjectCMainMenuRamps`) |
| `box-shadow`, `outline`, `inset` | нет | отдельный элемент-ореол с запечённой рампой и `opacity`; `left/top/right/bottom` |
| `::before` / `::after` | нет | реальные дочерние элементы |
| `display: grid`, `gap` | нет | flex; `margin` у ребёнка + `.is-first-in-group` |
| `+`, `:nth-child()` | **ошибка импорта** | `>` + явные классы |
| `:first-child`, `:focus-visible` | нет / `:focus` срабатывает на клик | классы; фокус — только цветом, без рамки |
| `@keyframes`, `animation-*` | нет | C#-драйвер в `Update(dt)` (как в меню) |
| `cubic-bezier()` | нет | `ease`, `ease-in`, `ease-out`, `ease-in-out` |
| `transform: …` | лонгхенды | `translate`, `scale`, `rotate` по отдельности |
| `filter:` (любой) | каскад «texture not allocated» в меню | **запрещён**; мягкость — в текстурах |
| `cursor:` | варнинг «Runtime cursors … texture» | **не использовать** |
| `text-shadow` списком | одна тень | одна тень; размытие ≤ паддинга SDF (24 у шрифтов меню) |
| `z-index`, `position: fixed` | порядок = порядок документа | порядок детей + абсолютное позиционирование в корне |
| `clamp/calc/vh/vw` | — | px на опоре 1920×1080, масштаб — `PanelSettings` ScaleWithScreenSize |
| `line-height`, `font-weight`, `font-variant-numeric`, `text-transform` | нет | веса — отдельными SDF-ассетами; верхний регистр — в C# |
| `overflow-x/y`, `pointer-events`, `[hidden]`, `@media` | нет | `ScrollView`, `pickingMode`, класс `.is-hidden`, порог в `GeometryChangedEvent` |
| `setTimeout`, `requestAnimationFrame` | — | только `Update(dt)` хоста (в моде уже так) |
| `localStorage` | — | `PlayerPrefs`, префикс `ProjectC.CloudyNavigator.` |

## 2. Цвет: Linear против гаммы

`PlayerSettings.colorSpace = Linear`, мод смешивает в sRGB. Каждая полупрозрачная заливка и каждый пуф переводятся
через `ProjectCMainMenuGamma.Alpha(a, tint, backdrop)`; у каждого слоя в контракте вёрстки записана подложка
(у меню: `Sky 0.80`, `Scrim 0.42`, `Cloud 0.30`, `Mist 0.07`). Светлые пуфы — с ослаблением пика (`PalePeak = 0.5` в меню).
Без этого шага повторятся «белые пятна» и пересвет из меню.

## 3. Шрифты

Inter Regular / Light уже в проекте (`Assets/Aura/Mods/ProjectCMainMenu/Fonts/Inter/`, OFL). Атласы — через
`ProjectCMainMenuFonts` (паддинг 24, `Alive()` против потери материала при выключенном Domain Reload).

## 4. План (шаги и ворота)

Каждый шаг заканчивается числовой проверкой в edit mode (без Play Mode — его делает пользователь) и отдельным коммитом.

| Шаг | Что | Ворота |
|---|---|---|
| 0 | **Мод:** слой-адаптер с полями как в C# DTO (`03`), стенды переписаны в этих формах; вычисляемые поля помечены | тесты мода зелёные; поиск по моду не находит полей вне `03` |
| **0 ✅** | Сделано: `state.contracts.active/available/canAcceptContracts/…`, `objectives[]` → `description/currentValue/requiredQuantity/completed`, `state.bag = {locationId, items[], maxSlots, credits}`, `ITEM_DEFS` (`weightKg`), `ships[]` → `hullCurrent/hullMax/fuelNormalized/fuelMax/cargoUsed/cargoMax/…`, `isEngineRunning`, `modules[] {slotType, installedModule}`, `cargo[shipId].cargoDetail[]` с битовой маской `flags` | 616 проверок в 35 файлах + `v44-names` 20/20; `findstr` по `src/*.js`: `state.cx`, `state.rcpt`, `engineOn`, `ITEMS[` — 0 (единственное совпадение — проба-утверждение `hasEngineOn`) |
| 1 | **Мод:** снять остаток непереносимого (`01` §3): `box-shadow`-пульсы → ореолы, градиенты → «рампы», grid/gap → flex | число вхождений из `01` §3 → 0 для box-shadow/grid/gap/::before |
| **1 ✅** | Сделано: `box-shadow`/`::before`/`::after` → реальные дети-ореолы (`.halo-sh/.halo-rad/.halo-in`, канты, волоски, кольца); `display:grid`/`grid-template*`/`gap` → flex + `margin` с `.is-first-in-group`; `outline` → рамки-ореолы. Спецификация для C# — `06_HALOS_AND_FLEX.md` | Счётчики `findstr`: 43/10/11 → **0/0/0**; `grid` 23 → 0; `grid-template*` 26 → 0; `gap:` 115+1 → 0; `outline` 19 → 0. Вёрстка прежняя (станция 4 размера, док 96/187/278/324, строки 44/48, календарь 12×30); тесты `v44-halos` 23/23, `v44-flex` 21/21 и прежние наборы — числа не изменились |
| 2 | Папка `ProjectCCloudyNavigator`, хост `UIDocument` + `CN_PanelSettings` (Overlay, ScaleWithScreenSize 1920×1080), пустое окно, `CloudyNavigatorHotkey` | 0 ошибок компиляции; легаси P не изменён (diff пуст вне папки) |
| **2 ✅** | Сделано: `Scripts/CloudyNavigatorHost.cs` (`PanelName = "CloudyNavigator"`, приоритет 60, взаимоисключение с `P` через `IsVisible()/Hide()`), `CloudyNavigatorHotkey.cs` (`Key.LeftBracket` мимо `InputBindingsConfig`), `CloudyNavigatorWindow.cs` (`Mount/Update(dt)/Dispose`), `UI/CN_Window.uss`, `UI/CN_PanelSettings.asset` | `check_compile_errors` — «No compile errors»; `git status --porcelain` — единственная запись `?? Assets/Aura/Mods/ProjectCCloudyNavigator/` |
| 3 | Провайдеры `Data/*` на чтение + лог `[CN]` сверки с легаси | на одном снапшоте значения провайдера = значения легаси-окна |
| **3 ✅ (код)** | `Scripts/Data/CnDataFeed.cs` — читающий слой: `PlayerTarget` (HP), `StatsClientState`, `MarketClientState` (кредиты, как легаси), `QuestClientState` + `QuestTracker`, `ContractClientState` (active/available/debt/тик), `InventoryClientState` + `ItemData.weightKg`, корабли по тому же поиску, что `MyShipsTab.RefreshShipList` (хост → `InventoryWorld.GetMyShips`, клиент → Key-предметы + `KeyRodInstanceWorld`), `ShipTelemetryState` + `ShipModuleManager.slots` + `TelemetryCargoState.cargoDetail` (канал легаси, D5), `ReputationClientState` + `FactionCatalog`/`GetTier`, `NpcAttitudeClientState` + имя из `QuestDatabase.GetNpc`, `ServerWeatherController` + `CalendarConfig`, `AltitudeCorridorSystem.GetActiveCorridor` + `ValidateAltitude` + `CumulativeRebaseOffset` (только диагностика) | `check_compile_errors` — «No compile errors». `Describe()` печатает `[CN]`-снимок в форматах легаси (HP, `Кредиты: N CR`, `Прочность: h/max`, `Топливо: p% (max max)`, `Груз: used/max`, `{min}:{sec:D2}`, репутация с тиром). **Ворота в рантайме НЕ ПРОВЕРЕНЫ** — нужен компонент в сцене и Play Mode пользователя; сетевых запросов слой не делает (`RequestDetail` — шаг 7) |
| 4 | USS и дерево по моду (по образцу шагов 2–3 меню) | USS без ошибок импорта, 0 `filter`/`cursor` |
| **4 ✅ (код)** | `Scripts/UI/CnStyle.cs` — палитра из `ProjectCMainMenuPalette`, полупрозрачные заливки через `ProjectCMainMenuGamma.Alpha`, шрифт тела из `ProjectCMainMenuFonts.BodyAsset` (SDF-атлас, паддинг 24); `CnStationView` — рамка под 3D-фигуру, HP-полоса, кредиты, 10 слотов, МОДУЛЬ I/II/III, репутация ≤ 6 строк, дата; `CnAltRailView` — кружок «К», границы коридора, насечки, текущая высота; `CnMainView` — шапка, лента квестов/контрактов, док `КОРАБЛЬ \| БАГАЖ`; `CloudyNavigatorWindow.ApplyLayout` — `homeW = 152 / 164 / clamp(0.17·W, 186…226)`, `altW = 36 / 34 / 32 / 30`, `colW = W − homeW − altW`, `narrow` при `colW < 720`, `dock-narrow` при `colW < 660` (числа из `src/main.js:362-376`) | `check_compile_errors` после принудительной перекомпиляции — «No compile errors»; `CN_Window.uss` реимпортирован как `StyleSheet` без сообщений в консоли; в USS 0 `filter`/`cursor`/`box-shadow`/`outline`/градиентов/`@keyframes`/`grid`/`gap`/`::before`/`::after`/`:nth-child()`/`+`; **вид в рантайме НЕ ПРОВЕРЕН** — компоненты в сцене нет (шаг 8) |
| 5 | Движение: поле дыма/gather/disperse/swap на `ProjectCMainMenuSmokeField` + `TextBlower` | пул ≤ 48, нет роста дерева после 20 открытий |
| **5 ✅ (код)** | `Scripts/UI/CnMotion.cs` — один слой на всё окно: `Open` (gather + fade после `lead`), `Close` (disperse + fade, вызывается пока раскладка живая), `Swap` (смена содержимого: disperse 4 пуфа + проявление против ветра на 14 px), `Edge` (кромка дока), `Dissolve` (снос подписи по буквам через `TextBlower`), общий порыв `{value, dir}` с затуханием `−2.2·dt`. Длительности — DUR мода (`gather .26` / `lead .12` / `reveal .22` / `disperse .32` / `swapOut .22` / `swapIn .24` / `swapLead .06` / `edge .30` / `fade .15`), кривая — `ProjectCMainMenuPalette.EaseOut` (CSS ease-out). `CloudyNavigatorWindow` — `RequestOpen/RequestClose`, облако собирается по реальным `worldBound` в первом кадре с раскладкой (страховка 0.2 с); `CloudyNavigatorHost` — `ClosePanel` сразу (ввод разблокирован), корень прячется в `Update` по `Motion.Busy`. Reduced motion — `PlayerPrefs` `ProjectC.CloudyNavigator.ReducedMotion` | `check_compile_errors` — «No compile errors»; `CN_Window.uss` реимпортирован без сообщений; пул в конструкторе — `CnMotionTiming.PoolMax = 48` (POOL_MAX мода), на переход уходит 6 + 8 + 4 = **18** пуфов, одновременный максимум ≤ 36; пуфы и ghost-копии удаляются сами классы на `progress ≥ 1` (`RemoveFromHierarchy`), наши переходы узлов не создают — рост дерева **0 по коду**, в рантайме не измерен (D20) |
| 6 | Превью 3D (`CnPreviewStage`) | камера выключена, пока окно скрыто; в превью 0 сетевых компонентов |
| **6 ✅ (код)** | `Scripts/UI/CnPreviewStage.cs` — фигура из примитивов по схеме мода (Torso / Head / Arm L,R / Leg L,R / Foot L,R, рост ≈ 1.75 м, цвета из палитры), своя ортокамера (`orthographic`, `size 1.0`, `cullingMask = 1 << 30`, `targetTexture` 256×256 AA2, прозрачный clear), свой `Directional` свет, драг (yaw + pitch ±25°), инерция, авто-вращение 12°/с после 4 с, колесо 0.8…1.6 через `orthographicSize`. Объекты превью — на слое **30**: в TagManager он не заведён, но по индексу работает (Настройки проекта не правятся, `grep` по `Assets/_Project` не находит ни `<< 30`, ни `.layer = 30`). Фигура вставлена в новую рамку станции `CnStationView.PreviewHost` | `check_compile_errors` — «No compile errors» (принудительная перекомпиляция, `hasCompilationErrors: false`), в консоли нет записей от `CnPreviewStage`; сетевых компонентов нет по построению — только `Transform`/`MeshFilter`/`MeshRenderer` + `Camera` + `Light`, коллайдеры примитивов уничтожаются сразу; `Camera.enabled = false` в конструкторе и на `RequestClose`. **Картинка в рантайме не проверена**: нужен Play Mode пользователя |
| 7 | Действия `CnActions` (только из `02` §7) + регистрация в `UIManager` | Esc закрывает, ввод блокируется, легаси-поведение не изменилось |
| **7 ✅ (код)** | `Scripts/Actions/CnActions.cs` — единственная точка вызова игровых `Request*`, ровно таблица `02` §7: `QuestTracker.Toggle` (локально), `QuestClientState.RequestAcceptQuest(id, "")` (как кнопка легаси), отказ — только честный `Loc.Get("ui.quest.reject_unavailable")`, сдача квеста не вызывается (идёт через диалог NPC); контракты `ContractClientState.RequestAccept/Complete/Fail/ReceiveCargo` с гейтами `ContractState` как у `ContractsMarketTabController` (accept — `Pending`, complete/fail — `Active`, receive — `Active && isReceiptContract && !receiptCargoIssued`); перенос `ShipCargoServer.RequestStoreToCargoRpc/RequestRetrieveFromCargoRpc` только при `ReadZone().allowed` (рынок `MarketZoneRegistry.LocalPlayerZone` либо консоль трюма `InteractableManager.FindNearestShipCargoConsole(…, 3f)` — тот же радиус, что `NetworkPlayer.pickupRange`) и только порциями, кратными `ExchangeRateEntry.inventoryQty/warehouseQty`. `CnStationView.RefreshModules` получил null-safety (`modules != null ? modules.Length : 0`). Полоса действий `.cn-actions` + бирки `.cn-tag`, клик по строке ленты = выбор (`cn-line--selected`), док на плитках `.cn-chip` | `check_compile_errors` — «No compile errors», `hasCompilationErrors: false`. Смоук через `execute_code` (Mount → 5 Update → RequestOpen → 12/16/40 Update → RequestClose → 30 Update → повторный RequestOpen → Dispose): без исключений, `root.childCount = 4`, `cn-actions` 1, `cn-dock__half` 2; прозрачность станции 0.00 → 1.00 → 0.00 → 1.00, `Motion.Busy` True только во время перехода. Вне зоны: `ReadZone().allowed = False`, `label = «подойди к обменнику»`, `StoreToShip/RetrieveFromShip` → `ok=False` с той же причиной, `Request*(` не вызывается (счётчик: 8 в `CnActions.cs`, из них 7 реальных + 1 в комментарии); `RejectQuest` отдаёт текст игры. `git status --porcelain` — только 6 файлов порта + `Scripts/Actions/`, `Assets/_Project/**` не тронут. **Ворота шага в рантайме (Esc, блокировка ввода, легаси P) не проверены** — по коду `UIManager`: `HandleGlobalInput` → `CloseTopPanel` → `onClose` → `Hide()`, `IsGameplayInputBlocked()` = `_openPanels.Count > 0`; нужен Play Mode пользователя |
| 8 | Префаб `CloudyNavigatorUI` — пользователь ставит в `BootstrapScene` и сохраняет сцену | приёмка пользователем в Play Mode |
| **8 ✅ (префаб) / ⏳ установка** | `Prefabs/CloudyNavigatorUI.prefab` собран: один корень без детей, `active = true`, компоненты `Transform` + `UIDocument` (`panelSettings = CN_PanelSettings`, `sortingOrder = 20` — выше легаси-панелей `0` и корабельных окон `10`) + `CloudyNavigatorHost` (`document` → свой `UIDocument`, `styleSheet` → `CN_Window.uss`, `debugLogging = false`) + `CloudyNavigatorHotkey` (`toggleKey = Key.LeftBracket`, 11). Зависимости префаба — только 4 файла внутри папки порта (`CN_Window.uss`, `CN_PanelSettings.asset`, `CloudyNavigatorHost.cs`, `CloudyNavigatorHotkey.cs`), внешних ссылок нет | Префаб создан скриптом `execute_code` во **временной пустой аддитивной сцене**: `NewPreviewScene` не годится (Unity отказывает в `SetActiveScene` для превью-сцены), а создать `GameObject` в активной сцене — значит испортить её флаг `isDirty`. После сборки временная сцена закрыта без сохранения; `WorldScene_0_0` и `BootstrapScene` остались `isDirty = False` — сцены проекта не тронуты. Проверка загрузкой ассета: `panelSettings = CN_PanelSettings`, `styleSheet = CN_Window`, `toggleKey = 11`, корень `active`; `check_compile_errors` — «No compile errors», ошибок `UIDocument`/`CN` в консоли нет (только старые предупреждения `Assets/_Project`). **Установка в `BootstrapScene` — за пользователем**; **префаб не закоммичен**: `.gitignore:153` исключает `*.prefab` (метаданные — `.gitignore:182`) |

## 4a. Шаг 1: механика замены зафиксирована

Спецификация снятых конструкций — `06_HALOS_AND_FLEX.md`: базовые классы ореолов (`.halo-sh` 4 полосы,
`.halo-rad`, `.halo-in`), таблицы геометрии/цветов/стопов по каждому хозяину, таблица keyframes с длительностями
и easing, нерешённые случаи с числами. Там же правило для `grid/gap`:

- **группа с `--g: Npx` → у детей `margin-right`/`margin-bottom` = `Npx`, у первого ребёнка группы `0`**
  (класс `.is-first-in-group`); позиция в строке переноса — классом, а не `:nth-child()`;
- `src/flex.js`, `src/halos.js`, CSS-переменные `--g/--hc/--hs/--ht/--hb/--hg` и `MutationObserver` —
  **механизм мода, в порт не переносится**: в C# классы и отступы зашиваются при построении дерева,
  «рампа» ореола — запечённая `Texture2D` с гамма-коррекцией вместо градиента.

## 4b. Шаг 8: что делает пользователь

1. Перетащить `Assets/Aura/Mods/ProjectCCloudyNavigator/Prefabs/CloudyNavigatorUI.prefab` в `Assets/_Project/Scenes/BootstrapScene.unity` (любой корневой объект, детей не нужно — дерево строится кодом в `CloudyNavigatorHost.EnsureMounted`).
2. Сохранить сцену и запустить Play Mode **в живой сессии с локальным игроком** (то же условие, что у P): клавиша `[` открывает/закрывает окно, `P` продолжает открывать легаси-окно персонажа, окна не сосуществуют.
3. Проверить ворота шага 7: Esc закрывает верхнюю панель (`UIManager.CloseTopPanel` → `Hide`), игровой ввод блокирован пока окно открыто, курсор разблокирован (парент — `panelSettings` Overlay).
4. Префаб в git не попадает (`.gitignore:153` — `*.prefab`, `.gitignore:182` — `*.meta`): либо так и оставить (тогда установка разовая, локальная), либо добавить `git add -f` для префаба и его `.meta` — без стабильного GUID ссылка из сцены не переживёт клон. Решение — за пользователем.

## 5. Решения пользователя (2026-10-28)

1. Клавиша нового окна — **`[`** (`Key.LeftBracket`). Когда окно отлажено — P отключается (`02` §3).
2. Перенос багаж ↔ трюм — **только в триггер-зоне обменника** (рынок или консоль трюма); вне зоны — «подойди к обменнику» (`02` §7).
3. Одновременно с легаси P окно **не** открывается — взаимоисключение через публичные `IsVisible()/Hide()` (`02` §6).
4. Кредиты — **как легаси** (`MarketClientState`); двойной источник записан проблемой (§6).
5. Репутация фракций — **−100…+100**, тиры — из ассетов `FactionDefinition`; где тиров нет — помечено на будущее (`03` §2).

Все пять решений перенесены в мод `v4-2` (тесты `v42-exchange` 7/7, `v42-rep-tiers` 11/11, регрессия зелёная).

## 6. Известные проблемы данных (решить потом, отдельными задачами)

| # | Проблема | Где | Что нужно |
|---|---|---|---|
| D1 | Два источника кредитов: `MarketClientState.CurrentSnapshot.credits` и `InventorySnapshotDto.credits` | `CharacterWindow.RefreshCharacterStats`, `InventorySnapshotDto.cs` | одна истина; окно читает как легаси до решения |
| D2 | Тир «Почитаемый» (порог 150) недостижим при диапазоне ±100 | 8 ассетов `Resources/Data/Factions/*.asset` | пересмотреть пороги или диапазон (баланс) |
| D3 | У `Faction_Bandits` и `Faction_Villagers` нет тиров | те же ассеты | задать тиры; окно показывает «тиры не заданы» |
| D4 | Клэмп фракционной репутации ±100 в серверном коде не найден поиском | `Quests/Core/QuestWorld.cs` и др. | проверить, где ограничивается значение |
| D5 | Канал деталей груза: легаси читает `sc.TelemetryCargoState`, а broadcast-кэш `ShipTelemetryClientState` помечен `Obsolete` | `MyShipsTab.cs`, `ShipTelemetryClientState.cs` | выбрать один канал (`ShipCargoClientState.RequestDetail`) |
| D16 | Коридор высоты: мод держал 1200–4450, в игре `DangerUpper` наступает только выше `max + criticalUpperMargin`. В `BootstrapScene` у `AltitudeCorridorSystem` назначены 5 ассетов: `veil_lower` 100–1500 (140/210.00002), `cloud_layer` 1000–3500 (250/375), `open_sky` 3500–5000 (150/225.00002), `high_altitude` 5000–6500 (150/225.00002), `global` 0–99999 (9999.9/14999.851); `Data/AltitudeCorridors/Corridor_*.asset` в сцену не подключены | `AltitudeCorridorSystem`, `AltitudeCorridorData.GetStatus` | в рантайме проверить `GetActiveCorridor(shipPos)`: по данным в радиусе 500 м побеждает `veil_lower`, дальше `global`; шкала обязана брать границы и статус из игры, а не из демо-переключателя мода |
| D17 | Вуаль окна: в моде за интерфейсом стоит облачная сцена, в игре — мир игрока. `cn-root` залит `Veil` с альфой 0.90 через `ProjectCMainMenuGamma.Alpha`, подложка принята тёмной; фактическая яркость мира под окном не измерялась | `CloudyNavigatorWindow.Mount` | подтвердить/подобрать альфу в Play Mode: при светлом небе текст может не читаться |
| D18 | Подписи состояний квестов и контрактов — Derived-перевод членов `QuestState` / `ContractState` / `ContractType`, ключей локализации для них в проекте нет | `CnMainView.QuestStateLabels`, `ContractStateLabels`, `ContractTypeLabels` | завести ключи `ui.cn.*` (или взять соседние `ui.quest.*`) и убрать строки из кода |
| D19 | Слоты экипировки станции — 10 пустых рамок: `CnDataFeed` не проецирует надетые предметы | `CnStationView`, `CnDataFeed` | спроецировать экипировку тем же путём, что легаси `EquipSlot`, на шаге 5+ |
| D20 | Пул движения и рост дерева посчитаны по коду (`PoolMax = 48`, 18 пуфов на переход, самоудаление по `progress ≥ 1`), но в рантайме не измерены — нужен Play Mode | `CnMotion`, `ProjectCMainMenuSmokeField.Tick` | прогнать 20 открытий/закрытий и сравнить `panel.visualTree` до/после |
| D21 | Правила позиционирования слоя движения висят на классах меню (`.pc-smoke-field`, `.pc-ghost-layer`, `.pc-smoke-puff`, `.pc-ghost`) — класс пуфа добавляет чужой код, переименовать его нельзя | `CN_Window.uss` §«слой движения» | при выносе окна из репозитория (шаг 8) либо оставить зависимость явной, либо завести свои обёртки поверх `Node` |
| D22 | Превью персонажа — фигура из примитивов, а не реальная модель: у префабов персонажа есть сетевые компоненты, а готового «превью-ассета» в проекте нет | `CnPreviewStage.BuildFigure` | решить, нужна ли настоящая модель (тогда — визуальная копия с вычищенными сетевыми компонентами и отдельным ассетом) |
| D23 | `Shader.Find("Universal Render Pipeline/Lit")` в рантайме зависит от попадания шейдера в билд (стриппинг); при `null` материал падает на `Standard` и в URP будет выглядеть иначе | `CnPreviewStage.MakeMaterial` | проверить в сборке; при выносе — вынести материал в ассет шага 8 |
| D24 | Слой 30 превью не заведён в TagManager — работает только по индексу, в инспекторе безымянный | `CnPreviewStage.PreviewLayer` | завести имя слоя при выносе окна или оставить зависимость от индекса явной |
