# Итерации разработки MainMenu

## Итерация от 2026-08-07

**Задача:** T-UI04 — MainMenu: замена NetworkTestCanvas на UI Toolkit главное меню
**Коммит:** `1a2c791b` — T-UI04: MainMenu — замена NetworkTestCanvas на UI Toolkit главное меню
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — UXML: панель меню (4 кнопки), IP-экран
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — стили главного меню (full-screen overlay)
- `Assets/_Project/Resources/UI/MainMenuPanelSettings.asset` — PanelSettings
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — MonoBehaviour + UIDocument + stack-навигация
- `Assets/_Project/Editor/Localization/AddMainMenuLocKeys.cs` — 9 ключей `ui.main_menu.*` в UI_Table (ru + en)
- `Assets/_Project/Scenes/BootstrapScene.unity` — MainMenu GameObject добавлен, NetworkTestCanvas отключён

## Итерация от 2026-08-07 (2)

**Задача:** T-UI04 — fix: настройки не открывались (NavigateTo не добавлял панель в дерево)
**Коммит:** `61c926a6` — T-UI04: fix — Настройки не открывались (панель не добавлялась в визуальное дерево)
**Изменения:**
- `MainMenuWindow.cs` — `NavigateTo` добавляет динамические панели в `_contentWindow`, `EscMenuSettingsStyles.uss` загружается

## Итерация от 2026-08-07 (3)

**Задача:** T-UI04 — fix: вёрстка панели настроек (фон, border, скрытие title/subtitle)
**Изменения:**
- `MainMenuWindow.cs` — `BuildSettingsPanel`: контейнер 480×460 с backgroundColor/border-radius/border, header с back-btn + title; `SetHeaderVisible(false)` при NavigateTo
- `docs/UI/MainMenu/01_implementation.md` — полная документация с баг-фиксами

## Итерация от 2026-08-13

**Задача:** T-UI05 — MainMenu: выбор языка в правом верхнем углу
**Коммит:** `e0de174c` — T-UI05: MainMenu — выбор языка в правом верхнем углу
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — контейнер `main-lang-selector` (правый верхний угол)
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — стили компактного выпадающего списка языка
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — `CustomDropdown` с языками из `LocaleSelector` + двусторонняя синхронизация с настройками

## Итерация от 2026-08-13

**Задача:** T-UI06 — MainMenu: кнопки-ссылки в левом нижнем углу
**Коммит:** `3b0743ef` — T-UI06: MainMenu — кнопки-ссылки в левом нижнем углу
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — контейнер `main-links` (левый нижний угол)
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — стили компактных кнопок-ссылок
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — `BuildLinkButtons()`: 5 кнопок с `Application.OpenURL`

## Итерация от 2026-08-16

**Задача:** T-UI07 — MainMenu: удалённый changelog из GitHub справа по центру
**Коммит:** `36d4e40d7afbc255586ff4a1d15f6f29d914ed39` — T-UI07: добавить удалённый changelog в главное меню
**Изменения:**
- `docs/changelogs.md` — файл с записями об обновлениях перенесён в корень `docs`, добавлена первая запись с дорожной картой версии 0.1.0.
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — добавлена правая центральная панель changelog со ScrollView и кнопкой обновления.
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — добавлены стили панели и типов Markdown-строк.
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — загрузка raw-файла GitHub через `UnityWebRequest`, построчный парсинг и обработка ошибки сети.

## Итерация от 2026-08-16 (2)

**Задача:** T-UI07 — упростить расположение changelog для обновления
**Коммит:** `41c7685280d65677e66f875e9c98aaa7596bb896` — T-UI07: перенести changelog в корень docs
**Изменения:**
- `docs/changelogs.md` — changelog перенесён из `docs/UI/MainMenu/`, чтобы файл было проще находить и обновлять.
- `MainMenuWindow.cs` и документация — обновлён путь к raw-файлу GitHub.

## Итерация от 2026-08-16 (3)

**Задача:** T-UI07 — исправить 404 после переноса changelog
**Коммит:** `6d1ea83e9e7a0a35b9fc933bdf39c37473401302` — T-UI07: исправить загрузку changelog после переноса
**Изменения:**
- `MainMenuWindow.cs` — добавлено принудительное исправление старого сериализованного URL `docs/UI/MainMenu/changelogs.md` на новый `docs/changelogs.md`.
- В предупреждение добавлен фактический URL запроса для диагностики.

## Итерация от 2026-10-27

**Задача:** PCMM — фон меню перекрывался градиентом проекта; табы настроек схлопывались.

**Причина (измерено, не гипотеза):**
- `PCMM_Sky.shader:14` — `Queue = "Background"`, единственный квад стека в этой очереди; остальные слои — `Transparent`.
  `ProjectC_URP` содержит один рендерер `ProjectC_URP_Renderer` с фичами `EdgeDetectionRenderFeature`,
  `VolumetricCloudsRenderFeature` (`BeforeRenderingTransparents`) и `DistantFocusRenderFeature` (`AfterRenderingOpaques`).
  Обе рисующие фичи отсекают только `CameraType.Preview`, поэтому работали и на камере меню и закрашивали
  скай; звёзды, вуали, газ и тросы рисовались уже после них — отсюда «фон частично сработал».
- Таб в `BuildSettings()` был `TextElement` с ребёнком `pc-tab__glow`. Как только у `TextElement` появляется
  ребёнок, собственный текст перестаёт учитываться в интринсик-размере: замер дал 5.1 px на таб, а тот же
  текст без ребёнка — 65.6 px. Три таба по 5.1 px накладывались в баре 623.7 px.

**Изменения:**
- `Assets/Aura/Mods/ProjectCMainMenu/PCMM_Renderer.asset` — копия `ProjectC_URP_Renderer` с очищенным списком
  фич; добавлена в `ProjectC_URP` (renderer list, индекс 1, `m_DefaultRendererIndex` остался 0).
- `ProjectCMainMenuAtmosphere.cs` — камера меню переведена на `PCMM_Renderer`, `renderPostProcessing = false`,
  чтобы палитра меню не зависела от Volume проекта.
- `ProjectCMainMenuWindow.cs` — таб стал контейнером, текст вынесен в отдельный `pc-tab__label`.
- `PCMM_Menu.uss` — правило `.pc-tab__label`; существующие правила `.pc-tab`, `.pc-tab:hover`,
  `.pc-tab.is-active`, `.pc-tab.is-active .pc-tab__glow`, `.pc-tabs > .pc-tab` не менялись.

**Проверка (замер на живом дереве в редакторе, без Play Mode):** табы 65.6 / 38.6 / 79.7 × 32.2 с шагом 18 px,
карточка 639.1 × 550.4 по центру, cloud bank 754.9 × 676.4 (11 блобов, `aspect-ratio` работает),
`pc-panels` 623.7 × 439.8 без переполнения. `check_compile_errors` — 0.

**Известное ограничение:** на панели с масштабом < 1 волосяные линии `height: 1px` округляются в 0
(измерено: USS 1px → 0, инлайн 2px → 1.286). Проявляется только в окне уже 1920 по ширине.

**Что не тронуто:** `WorldScene_0_0.unity`, `ProjectC_URP_Renderer.asset`, USS-фильтры
(`ProjectCMainMenuTextBlower.BlurFiltersEnabled` остаётся `false`).

**Решение по сцене:** вынос меню в отдельную пустую сцену в этот проход не входит — рендер-фичи живут на
`ProjectC_URP`, а не в сцене, поэтому переезд не менял бы ни градиент, ни вёрстку; кнопка «ОДИНОЧНАЯ ИГРА»
требует `GlobalMotionPilotRuntime` и `NetworkManager` из `BootstrapScene`, то есть переезд был бы выносом
сетевого бутстрапа. См. отдельную задачу по разбиению `MenuScene` / `BootScene`.

## Итерация от 2026-10-27 (2)

**Задача:** PCMM — у кнопок и карточки не было фона, «мягкое свечение таба» читалось как жёсткая
полоса, текст без размытия, время суток не отзывалось на смену таба; плюс убрать шаблонные тексты.

**Причина (измерено, не гипотеза):**
- В `PCMM_Menu.uss` на каждом правиле, где в `ui.css` был `linear-gradient()` / `radial-gradient()`,
  стоял только комментарий `[C#] ramp`: генератор ramp-текстур был отложен на «шаг 7»
  (`02_smoke_menu_port.md:246-248`) и не написан. Поэтому `.pc-btn-cloud`, `.pc-btn-cloud__rim`,
  `.pc-tab__glow`, `.pc-card__mist`, `.pc-card-cloud__rim`, `.pc-cld`, `.pc-main__scrim`, `.pc-rule`,
  `.pc-close`, `.pc-row`, `.pc-range-track`, `.pc-switch`, `.pc-select`, `.pc-kbd` не рисовали ничего.
- `--pc-cloud-base` = rgba(7,10,9,0.34) и `--pc-track-2` = rgba(7,10,9,0.55) — угольные тона: они
  писались под светлое небо веб-меню. На затемнённой атмосфере Unity такой фон невидим — отсюда
  «у кнопок нет светлой подложки».
- `ProjectCMainMenuAtmosphere`: `daySpeed` читалось только в строке цикла суток — ни сеттера, ни
  вызова из `RequestTab` / `OpenSettings`.
- Шаблонные тексты `Eyebrow` («THE CLOUDS · ТЕРМИНАЛ ДОСТУПА»), `MenuNote` («В глубине качаются
  тросы…»), хинт с легендой клавиш и строка сборки — это копия `ui.js`, а не интерфейс проекта.

**Изменения:**
- `ProjectCMainMenuRamps.cs` (новый) — генератор ramp-текстур: `Elliptical()` читает CSS-геометрию
  буквально (`radial-gradient(rx ry at cx cy, tint, transparent stop)`), `Horizontal()` — таблица
  стопов; кэш и `HideFlags.DontSave` как у `ProjectCMainMenuSmokeTextures`.
- `ProjectCMainMenuWindow.Ramps.cs` (новый, partial) — `ApplyRamps()` сразу после сборки дерева.
- Тона: подложка кнопки, ореолы и дорожки контролов — светлые (`#e9f2ec` / `#eef3ef`), потому что
  угольные на вуали не видны; угольные листы дыма остались поверх подложки кнопки, так что кнопка
  читается и в светлую фазу суток.
- `ProjectCMainMenuWindow.cs` — убраны `Eyebrow`, `MenuNote`, хинт, легенда клавиш и строка сборки;
  `pc-foot` остался пустым хостом.
- `ProjectCMainMenuWindow.Behaviour.cs` — сняты `notePart` и `BlowTextOf(Eyebrow, …)`.
- `ProjectCMainMenuAtmosphere.cs` — `DaySpeed`, `CurrentDaySpeed`, `PulseDaySpeed(boost, seconds)`;
  буст затухает линейно через `MoveTowards`.
- `ProjectCMainMenuMenuBridge.cs` — `tabDayBoost = 4`, `tabDayBoostSeconds = 2`: скачок `ActiveTab`
  даёт `PulseDaySpeed`, базовая скорость возвращается сама.
- `PCMM_Menu.uss` — `background-size: 100% 100%` на всех ramp-элементах, высота волосяных линий
  1 → 2 px, тёмный ореол `text-shadow` на `.pc-tab`, `.pc-tab__label`, `.pc-card-title`,
  `.pc-row-label`, `.pc-row-note`, `.pc-note-line`.

**Проверка (замер на живом дереве в редакторе, без Play Mode):** root 1920.0 × 1089.2; у
`pc-menu-main` 3 ребёнка (scrim, title, nav-row), у `pc-foot` — 0; карточка 639.1 × 550.4; табы
65.6 / 38.6 / 79.7 (не изменились); `.pc-rule` 2.57 px вместо 0. Текстуры назначены: `pc-btn-cloud`,
`pc-blob`, `pc-tab__glow`, `pc-card__mist`, `pc-main__scrim`, `pc-switch`, `pc-select`, `pc-kbd`.
`check_compile_errors` — 0, ошибок импорта USS в консоли нет.

**Не сделано — шрифт:** в моде своего шрифта нет вообще (`grep` по `.Aura/Mods/projectc-main-menu`
не находит ни `font-family`, ни `--pc-font`), поэтому «шрифт мода» нечего портировать: `ui.css`
полагался на шрифт браузера. В проекте нет ни одного `PanelTextSettings` (все 23 `PanelSettings`
с `textSettings = null`), кириллические начертания есть только у `Graphy` и `LiberationSans`.
Нужен файл шрифта от заказчика — до этого меню рисуется системным шрифтом UITK.

**Что не тронуто:** `WorldScene_0_0.unity`, `BootstrapScene.unity` (не сохранялась), статические
`filter: blur()` в USS, `BlurFiltersEnabled` остаётся `false`, угольные листы
`ProjectCMainMenuSmokeTextures.Cloud` на блобах кнопок, `ProjectC_URP.asset` и `PCMM_Renderer.asset`.

## Итерация от 2026-10-27 (3) — подложки после плейтеста

Замечания после Play Mode: слева подложка стала чёрной; внутри карточки настроек подложки грубые,
«как в Windows XP», лёгкости и cloudy-стиля мода нет; светлый фон делает шрифт нечитаемым.

**Причины — обе в коде, не в рендере:**

1. `ProjectCMainMenuRamps.Horizontal()` собирал пиксель как `ToColor32(SampleStops(...), 1f)`:
   альфа из таблицы стопов жёстко заменялась единицей. Замер: `RowWash` слева `22,65,44,255` и
   справа `22,65,44,255`, `MainScrim` слева `alpha 255`. Поэтому scrim слева стал чёрным
   прямоугольником (в CSS 0.66 → 0), а строки — сплошными плашками (в CSS 0.2 → 0).
2. `.pc-card__mist` собирался из ОДНОГО градиента 0.74, тогда как в `ui.css` их три:
   `--pc-glass-deep` 0.74 / `--pc-scrim` 0.66 / `--pc-glass-2` 0.62. Композит даёт ~0.97 в центре;
   при 0.74 карточка оставалась светлой, и светлые чернила на ней не читались.

**Изменения:**
- `Horizontal()` сохраняет альфу стопа: `ToColor32(color, color.a)`.
- `ProjectCMainMenuRamps`: движок `Stacked()` + `RadialLayer` — CSS-список фонов композитится
  source-over в одну текстуру 256×256; `CardMist()` — эти три слоя с CSS-радиусами и центрами.
- `EdgeMask()` гасит альфу в последних 3.2% листа — запечённый аналог `filter: blur(18px)`,
  которым CSS сглаживает край миста.
- `PCMM_Menu.uss` — у `.pc-card__mist` убран `filter: blur(18px)`: контраст даёт сама текстура,
  а блюр стоил вложенного прохода на всю карточку каждый кадр.
- Порядок отрисовки внутри карточки сверен с CSS (`-3` / `-2` / `0`): `CardCloud`, `CardMist`,
  `CardHead`, `TabsBar`, `PanelsHost` — порядок не менялся.

**Проверка (замер дерева и текстур, без Play Mode):** `row tex left=22,65,44,51 right=22,65,44,0`;
`scrim tex left alpha=166 right alpha=0`; `mist tex center=14,19,14,234 corner=7,10,9,7`;
карточка 639.1 × 555.6; `check_compile_errors` — 0.

**Что не тронуто:** `WorldScene_0_0.unity`, `BootstrapScene.unity`, остальные статические
`filter: blur()` в USS, `BlurFiltersEnabled` остаётся `false`, `ProjectC_URP.asset`,
`PCMM_Renderer.asset`. Визуальная приёмка и Play Mode — за пользователем.

## Итерация от 2026-10-28 (4) — общий проход по меню: гамма, тона мода, контролы, текст

**Жалобы:** подложки под кнопками контрастные и не гладкие, где должна быть прозрачность —
общий цвет или жёсткое белое пятно; фон пересвечен; текст грубый (в моде было лёгкое
размытие); во вкладках настроек недоделанная вёрстка, куски закрашены.

**Корневые причины:**

1. **Линейное цветовое пространство.** Проект в `Linear`, мод — целиком гамма-пространство
   (three.js `ShaderMaterial` пишет сырые sRGB-значения `palette.js`, браузер смешивает CSS-слои
   в sRGB). При той же альфе светлый слой над тёмным в линейном смешивании в разы сильнее
   (`#e9f2ec` 0.26 над чёрным: 0.26 в браузере, ~0.55 здесь — «жёсткое белое пятно»), тёмный над
   светлым — слабее (вуаль 0.6 над дневным небом: 0.37 в браузере, ~0.62 здесь — «пересвет»).
   Затемнение неба `col * 0.36` выполнялось на линейных значениях (визуально ×0.63).
2. **Инвертированные тона.** Облака карточки (`pc-cld`) брали светлый высокочастотный лист
   `SmokeTextures.Light` вместо угольного `--pc-smoke-cloud` (`smokePuff` 22,28,23) — это и были
   «пятнистые белые круги». Ореол кнопок, ободки, пилюли переключателей/селектов/клавиш тоже были
   переведены в светлый тон вопреки моду.
3. **`border-radius` обрезает фон резкой кромкой** — жёсткий эллипс вокруг карточки, края блобов.
4. **`:focus` с рамкой 1px** — зелёный прямоугольник вокруг кликнутой вкладки (в моде рамка только
   на `:focus-visible`, т. е. с клавиатуры) и сдвиг содержимого на ширину рамки.
5. **Ряды и контролы:** `.pc-row-text` не растягивался и переносил собственную измеренную ширину
   («Общая / громкость»); у слайдера не было позиционированного бегунка (голая линия); у
   переключателя, селекта и крестика не было смены фона по состоянию.
6. **Текст:** размытие `text-shadow` в UI Toolkit ограничено паддингом SDF-атласа (9 px при 90 pt,
   ~1.4 px на 13–15 px кегле), поэтому ореолы 9–16 px схлопывались в плотный тёмный контур вокруг
   букв.

**Исправлено:**

- Новый `ProjectCMainMenuGamma.cs`: `Alpha(a, tint, backdrop[, opacity])` — альфа, при которой
  линейное смешивание попадает в результат sRGB-смешивания мода над заданной подложкой
  (`Sky 0.80`, `Scrim 0.42`, `Cloud 0.30`, `Mist 0.07`), с учётом авторской opacity элемента.
- `ProjectCMainMenuRamps.cs` переписан: тона мода (`--pc-cloud-base`, `--pc-cloud-rim`,
  `--pc-track-2`, `--pc-accent-soft`, `--pc-glow`), гамма-коррекция каждого текселя; мист карточки
  получил запечённый эллиптический спад вместо `border-radius` + `blur(18px)`; у полос рядов —
  мягкое перо сверху/снизу. Новые рампы: `RowHover`, `RangeThumb`, `ThumbGlow`, `SwitchOn/Off`,
  `SwitchGlow`, `SelectIdle/Hover`, `SelectGlow`, `KeyCap`, `CloseHaloHover`; `SoftPill` удалён.
- `ProjectCMainMenuSmokeTextures.cs`: листы с гамма-коррекцией; `CardCloud` — угольный пуф для
  карточки (как в моде).
- Шейдеры атмосферы: общий `PCMM_Gamma.hlsl`; `Sky`, `Veil`, `Gas`, `Rope`, `Hull` считают цвет в
  sRGB, как мод, и отдают альфу sRGB-эквивалентного смешивания относительно глобала
  `_PCMM_Backdrop` (яркость неба, ставит `ProjectCMainMenuAtmosphere` каждый кадр).
- `PCMM_Menu.uss`: снят `border-radius` у блобов, облаков карточки, ободков, миста, свечения
  вкладки; рамка фокуса оставлена только у текстового поля; `.pc-row-text` растягивается;
  слайдер — абсолютные трек/бегунок/свечение; тени текста карточки убраны (в моде их нет);
  у кнопок оставлена видимая из двух теней мода (`0 1px 4px`), у вордмарка — мягкое свечение
  `0 0 3px --pc-glow`.
- Новый `ProjectCMainMenuWindow.Controls.cs`: перетаскивание слайдера с захватом указателя,
  ←/→/Home/End, подпись `%` следует; смена фона переключателя по `.is-on`; селект — подсветка и
  свечение на hover/focus, клик/Enter/→ — следующее значение, ← — предыдущее; крестик — hover.
  `Row()` создаёт `.pc-row__wash`.
- `ProjectCMainMenuFonts`: SDF-атласы Roboto / Northwest с паддингом 24 при 90 pt
  (`FontAsset.CreateFontAsset`), чтобы тени оставались мягкими; исправлен неверный комментарий —
  мод шрифт объявляет (`palette.js FONT`: Inter / Segoe UI / Helvetica Neue / Arial).

**Проверка (замер дерева и текстур, без Play Mode):** ряд «Общая громкость» — текстовая колонка
360.1 px, подпись 353.7 × 23.1 (одна строка); вкладки 66.9 / 38.6 / 78.4 px, рамка 0;
бегунки на 77.7 / 54.9 / 83.5 / 69.4 % при значениях 78 / 55 / 84 / 70; мист `border-radius 0`,
альфа по горизонтали `0.00 … 0.98 … 0.00`, по диагонали угол 0; шрифт подписи
`PCMM Roboto-Regular SDF`, паддинг 24; шейдеры — 0 сообщений; `check_compile_errors` — 0.

**Ограничение:** сглаживание краёв глифов UI Toolkit по-прежнему выполняет в линейном
пространстве — светлый текст на тёмном выглядит чуть плотнее, чем в браузере. Полное совпадение
потребовало бы рендера панели в гамма-RT; не делалось.

**Что не тронуто:** `WorldScene_0_0.unity`, `BootstrapScene.unity`, `ProjectC_URP.asset`,
`PCMM_Renderer.asset`, `BlurFiltersEnabled` (`false`), статические `filter: blur()` в USS,
`Stars` / `Glow` (аддитивные). Визуальная приёмка и Play Mode — за пользователем.

## Итерация от 2026-10-28 (5) — второй запуск, альтиметр, вспышка на наведении

**Второй Play без меню.** В проекте включены Enter Play Mode Options (`DisableDomainReload,
DisableSceneReload`), статика переживает сессию. SDF-атлас шрифта, закэшированный на
`PCMM_Fonts`, переживал Stop, а его материал — нет: первая же отрисовка текста бросала
`MissingReferenceException` (`TextUtilities.GetTextCoreSettingsForElement` → `Material.mainTexture`)
внутри `RepaintPanels`, панель оставалась в состоянии «идёт рендер», и дальше каскадом шли
`InvalidOperationException: VisualElements cannot change their render data …` + `Assertion failed`.
Исправление: `ProjectCMainMenuFonts.Alive()` — кэш переиспользуется, только если живы
материал и все текстуры атласа, иначе атлас строится заново; материалу и текстурам ставится
`HideFlags.DontSave`.

**Варнинг курсора.** `cursor: link` в USS требует текстурного курсора в рантайме — все 8
деклараций убраны из `PCMM_Menu.uss`. То же свойство есть в `CustomisationWindow.uss`,
`CharacterWindow.uss`, `MarketWindow.uss` — не тронуты.

**Вспышка на наведении.** Ободок кнопки прыгал 0.55 → 1 за один кадр (без transition),
облако раздувалось до 1.06 × 1.3 за 0.46 с и меняло `filter` (анимируемый фильтр),
а светлые пуфы gather были запечены под подложку 0.42 и над тёмным облаком выходили ~на 40%
ярче. Теперь: ободок — transition 0.6 с до 0.85; облако — 1.05 × 1.2, opacity/scale 0.6/0.75 с
`ease-in-out`, `filter` из transition и hover-правил убран (также у крестика, селекта, ручки
переключателя); `SmokeTextures.Light` запечён под подложку 0.18.

**Альтиметр.** Ход ленты 13 → 44 px по всей высоте указателя, демпфирование 6.2 → 3.4 (лента
догоняет указатель с инерцией), наклон 0.022 → 0.018°/px, тихое покачивание ±3.5 px;
деления продлены на 900 м за крайние кнопки (40 делений), чтобы конец ленты не открывался.

**Проверка (без Play Mode):** `check_compile_errors` — 0; USS 205 правил, скобки 207/207, деклараций
`cursor` и `filter: …contrast` — 0; лента за 2 с доходит до −41.5 / +46.3 px (было ±13); у атласа
материал и текстура `DontSave`.
