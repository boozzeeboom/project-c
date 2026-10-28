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
| 1 | **Мод:** снять остаток непереносимого (`01` §3): `box-shadow`-пульсы → ореолы, градиенты → «рампы», grid/gap → flex | число вхождений из `01` §3 → 0 для box-shadow/grid/gap/::before |
| 2 | Папка `ProjectCCloudyNavigator`, хост `UIDocument` + `CN_PanelSettings` (Overlay, ScaleWithScreenSize 1920×1080), пустое окно, `CloudyNavigatorHotkey` | 0 ошибок компиляции; легаси P не изменён (diff пуст вне папки) |
| 3 | Провайдеры `Data/*` на чтение + лог `[CN]` сверки с легаси | на одном снапшоте значения провайдера = значения легаси-окна |
| 4 | USS и дерево по моду (по образцу шагов 2–3 меню) | USS без ошибок импорта, 0 `filter`/`cursor` |
| 5 | Движение: поле дыма/gather/disperse/swap на `ProjectCMainMenuSmokeField` + `TextBlower` | пул ≤ 48, нет роста дерева после 20 открытий |
| 6 | Превью 3D (`CnPreviewStage`) | камера выключена, пока окно скрыто; в превью 0 сетевых компонентов |
| 7 | Действия `CnActions` (только из `02` §7) + регистрация в `UIManager` | Esc закрывает, ввод блокируется, легаси-поведение не изменилось |
| 8 | Префаб `CloudyNavigatorUI` — пользователь ставит в `BootstrapScene` и сохраняет сцену | приёмка пользователем в Play Mode |

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
