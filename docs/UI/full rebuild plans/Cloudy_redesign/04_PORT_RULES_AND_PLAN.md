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
| 6 | Превью 3D (`CnPreviewStage`) | камера выключена, пока окно скрыто; в превью 0 сетевых компонентов |
| 7 | Действия `CnActions` (только из `02` §7) + регистрация в `UIManager` | Esc закрывает, ввод блокируется, легаси-поведение не изменилось |
| 8 | Префаб `CloudyNavigatorUI` — пользователь ставит в `BootstrapScene` и сохраняет сцену | приёмка пользователем в Play Mode |

## 4a. Шаг 1: механика замены зафиксирована

Спецификация снятых конструкций — `06_HALOS_AND_FLEX.md`: базовые классы ореолов (`.halo-sh` 4 полосы,
`.halo-rad`, `.halo-in`), таблицы геометрии/цветов/стопов по каждому хозяину, таблица keyframes с длительностями
и easing, нерешённые случаи с числами. Там же правило для `grid/gap`:

- **группа с `--g: Npx` → у детей `margin-right`/`margin-bottom` = `Npx`, у первого ребёнка группы `0`**
  (класс `.is-first-in-group`); позиция в строке переноса — классом, а не `:nth-child()`;
- `src/flex.js`, `src/halos.js`, CSS-переменные `--g/--hc/--hs/--ht/--hb/--hg` и `MutationObserver` —
  **механизм мода, в порт не переносится**: в C# классы и отступы зашиваются при построении дерева,
  «рампа» ореола — запечённая `Texture2D` с гамма-коррекцией вместо градиента.

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
