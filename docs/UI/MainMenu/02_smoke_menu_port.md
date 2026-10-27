# MainMenu — перенос меню мода в Unity 1в1

> **Дата:** 2026-10-27
> **Статус:** в работе (шаг 8 из 9)
> **Источник истины:** `.Aura/Mods/projectc-main-menu/` (`src/*.js`, `src/ui.css`)
> **Предыдущая документация меню:** `01_implementation.md`, `ITERATIONS.md`

---

## Зачем

Меню мода — это не «фон + старые кнопки», а самостоятельный интерфейс: 4 кнопки-облака,
алтиметр-лента, поле дыма, «выдувание» букв, карточка настроек из облаков с вкладками,
тосты, футер, параллакс. Навешивать 3D-фон на старый UI бессмысленно — нужен цельный порт.

Поэтому: **новое окно меню в Unity повторяет меню мода, и в него же переносится вся наша
реальная логика.** Старое меню остаётся в сцене рабочим до финального переключения одним
флагом — в рабочей сцене не появляется «полуменю».

## Что уже перенесено и остаётся

`Assets/Aura/Mods/ProjectCMainMenu/` — порт `atmosphere.js`: небо, звёзды, 4 слоя завесы,
меззий, 3 троса + паром, пыль, сутки + префаб `ProjectCMainMenu.prefab`, слой
`PCMM_Atmosphere`, камера глубины 100. Это фон меню мода.

## Пределы платформы (проверено, а не предположено)

Ранее этот раздел утверждал, что USS не умеет `filter: blur()` и `text-shadow`. **Это неверно
для Unity 6000.5.2f1.** Возможности измерены: 117 однотемных пробных `.uss` (по одному
конструкту на файл), импорт + `StyleSheet.importedWithErrors/importedWithWarnings`.

### Поддерживается (можно писать как в CSS)

`--custom-property` / `var(--x)` / `var(--x, fallback)`; `filter: blur() opacity() contrast()
grayscale() sepia() invert() hue-rotate()`; `text-shadow`; `aspect-ratio`; `translate` / `scale` /
`rotate` (в т.ч. `%` и `deg`); `transition-*` (`ease`, `ease-in`, `ease-out`, `ease-in-out`);
`-unity-font-style`; `-unity-text-outline`; `border-radius: %`; `-unity-background-scale-mode`;
`-unity-background-image-tint-color`; `:hover` / `:active` / `:focus`; селекторы по атрибуту;
`white-space`; `text-overflow`; `letter-spacing` (в px); `cursor: link|arrow`; `word-spacing`.

### Не поддерживается (и чем заменено)

| В моде | В Unity |
|---|---|
| `clamp()` `min()` `max()` `calc()` | разрешено в px на опоре 1920×1080 |
| `vh` `vw` `rem` `em` `ch` | разрешено в px; масштаб делает `PanelSettings` |
| `transform: translate/scale/rotate` | длинные свойства `translate` / `scale` / `rotate` |
| `cubic-bezier()` | только `ease` / `ease-in` / `ease-out` / `ease-in-out` |
| `linear-gradient()` / `radial-gradient()` | `[C#]` процедурная `Texture2D` в `style.backgroundImage` |
| `filter: brightness()` | отброшено (остальные операторы живут) |
| `@keyframes` / `animation-*` | `[C#]` драйвер (`pc-boil`, `pc-sweep`, burst, `pc-press`) |
| `text-transform` | `[C#]` подписи уже в верхнем регистре |
| `line-height` `font-weight` `font-variant-numeric` | отброшено (нет в USS) |
| `gap` `row-gap` `column-gap` | margin у ребёнка + класс `.is-first-in-group` |
| `+` (соседний селектор) | **жёсткая ошибка импорта**; раскрыто в `>` + классы |
| `:nth-child()` | **жёсткая ошибка импорта**; явные `.pc-btn-n1…n4` |
| `:first-child` `:first-of-type` | «неизвестный псевдокласс»; класс `.is-first-in-group` |
| `inset` `outline` `outline-offset` `box-shadow` `backdrop-filter` | `left/top/right/bottom`, border, `[C#]` элемент-свечение |
| `overflow-x` / `overflow-y` | только `overflow`; прокрутка — `ScrollView` в C# |
| `pointer-events` | `[C#]` `pickingMode` |
| `text-shadow` со списком из 2 теней | только одна тень |
| `cursor: pointer` | `cursor: link` |
| `z-index` | порядок отрисовки = порядок документа |
| `::before` / `::after` | реальные дочерние элементы (`__scrim`, `__sweep`, …) |
| `[hidden]` | класс `.is-hidden` (нет API атрибутов) |
| `@media` | медиазапросов нет; единственный (`max-width: 900px` у `.pc-proj-changelog`) перенесён порогом в `GeometryChangedEvent` (шаг 8) |

Остальное (раскладка, цвета палитры, тайминги, порядок экранов, подписи, поведение)
переносится буквально.

### Следствие для шага 9

`PanelSettings` нового окна обязан быть `ScaleWithScreenSize` с опорой **1920×1080** —
именно это заменяет все `clamp(min, Nvh|Nvw, max)` из `ui.css`. С `ConstantPixelSize`
вёрстка поедет.

## Шаги

- [x] **1. Процедурные листы дыма** — `Scripts/ProjectCMainMenuSmokeTextures.cs`:
  `smokeImage` / `smokePuff` / `smokeWisp` → `Texture2D` (+ кэш, `DisposeAll`).
  Проверено численно: Light 144² (alpha 0..255, mean 41), Dark 144² (mean 16.8),
  Wisp 256×96 (верх/низ alpha 0, перепад шва X = 2/255), Cloud 160² (mean 46.3);
  повторный вызов отдаёт тот же инстанс (детерминизм подтверждён).
- [x] **2. USS** — `UI/PCMM_Menu.uss`: перенос `ui.css` на USS-селекторы.
  Импорт чистый (`errors=False`, `warnings=False`, 0 сообщений). Покрытие: **110 из 110**
  классов `.pc-*` из `ui.css` присутствуют, ни одного не потеряно; добавлено 9 классов
  (`pc-btn-n1…n4`, `pc-cld`, `pc-close-line-1/2`, `pc-range-track/thumb`).
- [x] **3. Дерево в C#** — `Scripts/ProjectCMainMenuWindow.cs`: сборка DOM как в `ui.js`
  (`pc-scan`, `pc-main` → title-block / nav-row (altimeter + 4 кнопки с блобами) /
  menu-note, `pc-settings` → card-cloud + head + tabs + panels, `pc-toast`, `pc-foot`).
  Гигантского UXML нет: структура строится кодом, стили берутся из `PCMM_Menu.uss`.
  Проверено численно (`_tree_verify.txt`): **221 элемент**, все счётчики совпали с `ui.js` —
  4 `.pc-btn` × 7 `.pc-blob` = **28** блобов, 10 `.pc-cld`, 3 `.pc-tab`, 3 `.pc-panel`,
  14 `.pc-row`, 5 `.pc-group-title`, 5 `.pc-row-note`, 6 `.pc-range` (+ track/thumb),
  4 `.pc-switch`, 3 `.pc-select`, 5 `.pc-kbd`, 2 `.pc-close-line`; `is-first-in-group` = 50,
  `is-hidden` = 3 (окно настроек + 2 неактивные панели), `is-active` = 2,
  `.pc-btn-n1…n4` = 1/1/1/1, `focusable` = 21, `pickingMode = Position` = 44.
  Дети `pc-ui` = scan, main, settings, toast, foot; в `pc-nav` 4 кнопки; связка
  button→action проверена.
- [x] **4. Поле дыма** — `Scripts/ProjectCMainMenuSmokeField.cs`: пул пуфов,
  `Gather(rect)`, `Disperse(rect)`, `Count()`, `GetFlow()` (+ `Tick()`, `Clear()`).
  Проверено численно (`_smoke_field_verify.txt`, 31 проверка, 0 провалов):
  gather 400×60 → 10 пуфов, ширина 44.4…173.9 px, h/w 0.789…1.131,
  flow = gather / dir +1 / dx 317.6 / dy −6.9; 240 запрошенных пуфов → срез на 96;
  тот же seed даёт идентичные пуфы, другой seed — другие; светлый/тёмный лист
  делится по `sy > 0.44·H` (24/24 в каждом из двух диапазонов); при задержке 500 мс
  до неё пуфы невидимы и остаются в очереди; после `duration+delay` поле пусто;
  disperse 300×40 → dx 368.7 (допустимая полоса 231…495), dir −1 → −368.7;
  повторный `Tick` с той же меткой времени даёт те же числа (0.2983|181.717|0.8510).
- [x] **5. Выдувание букв** — `Scripts/ProjectCMainMenuTextBlower.cs`: по буквам,
  `Blow(host, dir, stagger, duration, strength, puffs, whole)`, `RestoreWithin`.
  Проверено численно (`_text_blower_verify.txt`, 30 проверок, 0 провалов) в реальной
  панели (`EditorWindow`, настоящий layout, без сцены и без Play Mode):
  «ОДИНОЧНАЯ ИГРА» на хосте 400×40 → 13 букв (пробелы не летят), 1 гоуст,
  2 словных ряда + 1 промежуток, по 1 символу в глифе, кегль 24 скопирован;
  на 100 мс первая буква уже на 48.7 px, последняя ещё нет (`translate = Null`);
  на 840 мс финальные ключи: dx 55.0…120.4 (в [22…126]), dy в [−47…−5],
  scale в [1.35…1.85], opacity 0 у всех; на 860 мс гоуст снят и `opacity` хоста
  возвращён; `whole`-хост (PROJECT + C) летит одним куском — −40.6 px при dir −1;
  `RestoreWithin` вернул 2 полёта из-под корня; `max: 8` пропустил ровно 8 из 10;
  пуфы пришли только после layout (2 запроса, min width 20.0 px).
- [x] **6. Алтиметр** — `Scripts/ProjectCMainMenuAltimeter.cs`: отметки 800/1200/2200/3000 м,
  наклон от указателя, плавный ход к наведённой высоте.
  Проверено численно (`_altimeter_verify.txt`, 52 проверки, 0 провалов) в реальной панели
  1920×1080 на настоящих кнопках шага 3: **29 тиков** (500…3300 шагом 100), 4 `is-anchor`,
  5 `is-mid`, 20 без метки; все 29 `top` совпали с независимым пересчётом (≤ 0.5 px);
  отклонение каждой подписи от центра её кнопки **0.5 px** (высота ленты 223 px, px/m 0.0725);
  9 подписей в дереве, показано 8 (2000 м скрыт правилом 15 px — 2200 м стоит в 11.6 px от него),
  min зазор 18 px, зазор 2200…3000 поделён ровно (7.25 px на 100 м);
  `Update(10 c)` обрезан до 0.1 c → scroll 6.01, потолок 13 → 12.99, поворот 0.285°;
  полоса встала на 95.49 px = Y(2200) + scroll, readout «2200 м», затем `is-on` снят;
  цифры опоры 0.7 px / кегль 11.5, «м» 0 / 6.3, малые числа 0.2 не тронуты;
  дублей нет (по одному `.pc-alt-tape/.band/.readout`, 5 детей у `.pc-alt`).
- [x] **7. Окно меню** — `Scripts/ProjectCMainMenuWindow.Behaviour.cs` (второй `partial` того же
  класса, 1406 строк): сборка/распад частей (`--a`), кнопки (hover/press/клавиши), настройки
  (открытие/закрытие), вкладки (двухфазный переход), параллакс заголовка, тосты, густы в атмосферу.
  Проверено численно (`_behaviour_verify.txt`, **120 проверок, 0 провалов**) в реальной панели
  1920×1080, `AutoTick = false`, кадры вручную по 1/60 c: старт — 7 частей (6 меню + карта),
  `Ui` 5→7 детей, `MenuA` 1 / `CardA` 0; загрузка — все шесть в 0, задержки
  0.12/0.178/0.236/0.294/0.352/0.41, размытие 6/5/4 px, облака кнопок 0.22, `is-behind`;
  400 кадров → `a = 1`, `is-forming` снят, инлайновые блюр/opacity обнулены, scrim и лента 1.000;
  hover «Настройки» → 0.5 / 520 мс / `WindSeq` +1, лента «1200 м», `is-on`; press «Одиночная
  игра» → 1 / 720 мс, `is-clearing`, 6 гоустов и 53 пуфа (этот прогон), через 120 мс
  `Busy = false` и `OnAction("host", "ОДИНОЧНАЯ ИГРА")`; настройки — 0.85 / 700 мс, `TabA 0.08`,
  `CardA 1`, `Main` Hidden, scrim 0.000; вкладка «Звук» — 0.7 / 620 мс, `WindDir −1`, панель
  ушла и пришла, `TabA 1`; закрытие — 0.72 / 640 мс, задержки возврата 0.18…0.47; `SetView`
  в обе стороны, тост снимается сам, `Dispose` опустошает точку монтирования.
- [x] **8. Наши блоки** — `Scripts/ProjectCMainMenuProjectPanels.cs` (третий `partial`):
  IP-экран → `NetworkManagerController.ConnectToServer`, 10 админ-сбросов → `PersistenceDebugTools`,
  changelog → реальный `UnityWebRequest` (raw GitHub `docs/changelogs.md`),
  5 ссылок → `Application.OpenURL`, 9 локалей → `LocaleSelector`,
  дисклеймер → `PlayerPrefs` `ProjectC.MainMenu.FirstLaunchDisclaimerAccepted`.
  Проверено численно (`_project_panels_verify.txt`, **63 проверки, 0 провалов**) в реальной панели
  1920×1080: слой — последний ребёнок `Root`, 6 блоков в порядке
  admin/changelog/lang/links/ip/disclaimer, слой `pickingMode = Ignore`, все шесть — `Position`;
  10 сбросов с точными подписями и единственным `is-danger` у `all`; 5 ссылок с точными url;
  9 локалей, начальное значение — сохранённая; IP-лист закрыт, `openIp`/`closeIp` → Flex/None;
  `SubmitIp` → адрес и порт 7777 ушли в приложение, тост «Подключение к 10.0.0.7:7777»,
  действие `ip-connect`, пустое поле → 127.0.0.1; `Сброс: inventory — fake reset inventory`;
  `de` → `LocaleSelector`, тост «Язык: Deutsch», селект «Deutsch»; changelog — «Загрузка…»,
  фикстура даёт ровно `heading|gap|divider|bullet|bullet|milestone|plain|heading|gap`,
  `**жирный** текст` → «жирный текст»; дисклеймер Flex→None и `PlayerPrefs` = 1 (прежнее
  значение восстановлено); ширина 800 → `.pc-proj-changelog` получает `is-hidden` и `display: none`;
  нажатие «ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ» через 120 мс открывает IP-лист; `Dispose` снимает слой и
  оставляет точку монтирования пустой.
- [ ] **9. Переключение** — новый префаб `ProjectCMainMenuUI` (`UIDocument` + `PanelSettings`
  Screen Space Overlay, `ScaleWithScreenSize` 1920×1080), `MainMenuWindow` остаётся
  выключенным как откат; `ProjectCMainMenuMenuBridge` перевешивается с `main-menu-*`
  на новое окно (`OnBlow`, `OnAction`, `Hovered`, `SettingsOpen`); затем
  `check_compile_errors` = 0 и проверка шейдерных ошибок в `get_unity_logs`.

## Что делаем вручную

Play Mode и визуальную приёмку делает пользователь (правило проекта). Агент не сохраняет
сцены и не запускает Play Mode.

## Что не попадает в Git

`.gitignore` исключает `*.prefab`, `*.shader`, `*.shadergraph` и `*.meta`. В репозитории
лежат скрипты порта, `PCMM_Menu.uss`, материалы `PCMM_*.mat`, билдер и отчёты
`_*_verify.txt`. Вне репозитория остаются:

- семь шейдеров `Shaders/PCMM_{Sky,Gas,Stars,Veil,Rope,Hull,Glow}.shader`;
- префаб `Prefabs/ProjectCMainMenu.prefab`;
- все `.meta`, то есть GUID-ы — в чистом клоне ссылки `BootstrapScene` на префаб
  и материалы не разрешатся.

`Tools/ProjectC/MainMenu/Build Atmosphere Prefab` пересоздаёт материалы и префаб, но
**не** исходники шейдеров: билдер только ищет их через `Shader.Find`, поэтому на чистом
клоне он сообщит `[PCMM] shader not found: …`. Файлы шейдеров нужно переносить отдельно.
`.gitignore` в рамках этого прохода не менялся.

## Журнал

### 2026-10-27 — регрессии после первой попытки

| Симптом | Причина | Решение |
|---|---|---|
| Поток `Invalid AABB a` / `Invalid worldAABB` / `abnormal mesh bounds` | на последнем узле троса `sin(π)` = −8.7e−8, `Mathf.Pow(<0>, 1.12f)` = `NaN` | `Mathf.Max(0f, sin)` перед `Pow`; проверено: 8208 сэмплов, было 144 нефинитных, стало 0 |
| Тросы/паром/пыль не видны | объекты создавались на слое 0, камера атмосферы видит только `PCMM_Atmosphere` | `ApplyLayer(int)` в `RopeRig` и `WindDust`, вызов из `Atmosphere.Build()` |
| После «ОДИНОЧНАЯ ИГРА» мир не появляется | `MainMenuWindow.Hide()` прячет **обёртку** из `CloneTree()`, а мост смотрел на `display` самого `main-menu-root` (всегда `Flex`) → камера атмосферы не выключалась и её непрозрачный слой закрывал мир | `IsShown()` обходит всю цепочку предков; плюс fail-closed: если меню не найдено — камера выключается |
| Меню выглядело как старое | `.main-menu-root` в `MainMenuStyles.uss` — единственный полноэкранный непрозрачный элемент (`rgba(8,12,22,0.92)`), он перекрывал 3D-слой | мост ставит инлайновый прозрачный фон (`clearMenuBackdrop`); сам USS не правится |

### 2026-10-27 — шаг 2: USS перенесён

`Assets/Aura/Mods/ProjectCMainMenu/UI/PCMM_Menu.uss` создан и проверен импортом:
`importedWithErrors=False`, `importedWithWarnings=False`, 0 сообщений в консоли.

| Проверка | Результат |
|---|---|
| Классы `.pc-*` из `ui.css` | 110 найдено / **0 потеряно** |
| Классы, добавленные портом | 9 (`pc-btn-n1…n4`, `pc-cld`, `pc-close-line-1/2`, `pc-range-track/thumb`) |
| Блоки `ui.css` → правила USS | 211 → 197 (селекторы с `+` и `:nth-child` раскрыты) |
| Ошибки / предупреждения импорта | 0 / 0 |

Что выяснилось и попало в таблицу выше: `filter: blur()` и `text-shadow` в USS **есть**
(прошлая версия этого документа утверждала обратное); `+` и `:nth-child()` — **жёсткие
ошибки импорта**, а не «молча игнорируются»; `transform` не существует как свойство,
но его длинные формы `translate` / `scale` / `rotate` работают полноценно.

Палитра объявлена один раз на `.pc-ui-root` — это корректно, потому что `ui.js` вызывает
`applyPalette()` ровно один раз (строка 182) из константного `PALETTE`; палитра UI не
связана с циклом суток 3D-фона. Не обновляемое из C# осталось ровно одно значение — `--a`
(сборка частей): у `VisualElement` нет API записи кастомных свойств, только чтение
(`customStyle`), поэтому `opacity` / `translate` / `scale` / `filter` пишет окно каждый кадр.

Градиенты и `@keyframes` в USS невозможны в принципе — под них заведены классы-заглушки
с комментарием `[C#]`, которые заполнят генератор ramp-текстур и драйвер окна (шаг 7);
шаг 4 — только поле дыма.

### 2026-10-27 — шаг 3: дерево, шаг 4: поле дыма

**Шаг 3** — `ProjectCMainMenuWindow.cs` (607 строк, 221 элемент). UXML нет: дерево
строится там же, где его строит `ui.js`. Кнопки — не `Button`, а `VisualElement` с
`focusable = true`, иначе тема `.unity-button` протекает поверх `PCMM_Menu.uss`.

**Шаг 4** — `ProjectCMainMenuSmokeField.cs`. Два отличия от DOM:

| В моде | В Unity |
|---|---|
| WAAPI-кейфреймы с `easing: ease-out` | пуф хранит три кейфрейма как данные и считается от `Time.realtimeSinceStartupAsDouble` |
| `anim.onfinish` удаляет пуф | удаление по `duration + delay` в тике |
| `field.node` живёт сам | `schedule.Execute(Tick).Every(16)`, пауза пока пусто |

Ключевое: значение считается из часов, а не накоплением `dt`, поэтому поздний тик
показывает ровно то, что показал бы браузер в этот момент; повторный `Tick` с той же
меткой времени ничего не сдвигает (проверено). Поэтому же `Tick()` можно звать и
извне — драйвер шага 7 не обязателен и полю не навредит.

Easing переехал в `ProjectCMainMenuPalette`: `EaseOut` = `CubicBezier(0, 0, 0.58, 1)`,
параметр ищется Ньютоном. Там же будет решён `cubic-bezier(.22,.62,.3,1)` шага 5.

Ошибки в первой редакции проверки были в самих проверках, не в порте: диапазон
высоты брался один (все пуфы ниже 44 % — все тёмные), а «до задержки» мерилось
на 50 мс при случайной задержке 0…70 мс. Обе переписаны: две зоны кадра и явные
500 мс задержки.

### 2026-10-27 — шаг 5: буквы в дым

`ProjectCMainMenuTextBlower.cs`. Где DOM скопировать буквально нельзя и что стоит вместо:

| В моде | В Unity |
|---|---|
| `cloneNode(true)` | копия только текста: `whole`-хост — ряд Label с текстом и кеглем детей; обычный Label — слова-ряды из по-буквенных Label |
| `host.textContent = ''` | живой текст только прячется (`opacity: 0`) и восстанавливается: вёрстка вокруг него не дёргается, восстанавливать текст не нужно |
| `getBoundingClientRect()` пуфа сразу | `worldBound` свежесозданного элемента ещё нулевой, поэтому пуфы выдаются на первом тике, где ширина > 0.5 |
| `opts.whole \|\| ghost.childElementCount > 0` | `whole` или у хоста есть дети (вордмарк PROJECT + C) |

Три момента, которые выяснились по дороге:

1. `FilterFunction` не имеет фабрики `Blur(...)`: только `new FilterFunction(FilterFunctionType.Blur)`
   + `AddParameter(new FilterParameter(px))`, и в `style.filter` он попадает только через
   `StyleList<FilterFunction>(List<FilterFunction>)`.
2. У гоуста `flex-direction: row` — значит `justify-content` в нём горизонтальный, а
   `align-items` вертикальный. Первая редакция маппинга `TextAnchor` была написана для
   колонки и перепутывала оси.
3. Одиночный пробел между словами в моде — это текстовый узел, который остаётся на месте.
   Здесь он тоже не летит: ряд `.pc-word` с пробелом держит зазор.

Проверка потребовала настоящей вёрстки, поэтому шаг проверялся в реальной панели
(`EditorWindow` + `EditorApplication.update` в два такта), а не на отсоединённых элементах
как шаги 3–4. Первый заход дал 0/10 в проверке лимита — это была ошибка проверки:
десять хостов создавались и выдувались в одном кадре, когда `worldBound` ещё нулевой
(порт тут прав: `Blow` отказывает хосту с нулевым прямоугольником, как `rect.width > 1`
в моде). Хосты теперь живут кадр до выдувания.

### 2026-10-27 — шаг 6: алтиметр

`ProjectCMainMenuAltimeter.cs`. Где DOM скопировать буквально нельзя и что стоит вместо:

| В моде | В Unity |
|---|---|
| `<span class="pc-alt-num">3000<em class="pc-alt-unit">м</em></span>` | Label не может иметь ребёнка, поэтому у опоры `.pc-alt-num` — ряд из двух Label (цифры + «м»); у малых чисел `.pc-alt-num` остаётся на самом Label |
| `letter-spacing` в `em` наследуется сам | цифры получают трекинг опоры через `SyncNumberStyle`; проверено: цифры 0.7 px, «м» 0, малые числа 0.2 не тронуты |
| `ResizeObserver` + `resize` + 8 × `requestAnimationFrame` | `GeometryChangedEvent` + 8 тиков `schedule.Execute(WatchFrame).Every(16)`, потом сам останавливается |
| `transform: translate3d(...) rotate(...)` | `style.translate` + `style.rotate` по отдельности — компилируются в те же лонгхенды |
| `host.textContent = ''` в `dispose()` | `host.Clear()` — так же сносит и фейд-полосы шага 3, поэтому `Dispose()` только когда слой действительно уходит |

Четыре наблюдения, которые стоило записать:

1. `.pc-alt-tick` имеет `height: 0` и `translate: 0 -50%` — то есть −50 % от собственной
   высоты, то есть 0. Число центрируется ровно на линии тика (`align-items: center` в
   нулевом контейнере даёт −h/2), отсюда и попадание в кнопки.
2. Yoga округляет позиции до целого пикселя: шаг 7.25 px читается как
   −8/−7/−7/−7/−8/−7/−7/−7, а подпись встаёт на 0.5 px ниже центра кнопки. Первая
   редакция проверки требовала разброс < 0.05 px и «падала» на верном порте — проверка
   переписана под пиксельную сетку (средний шаг = пролёт/8, разброс ≤ 1 px).
3. Правило 15 px при настоящей высоте ленты 223 px скрывает **2000 м**: 2200 — опора и
   стоит в 11.6 px от него. Это поведение мода, а не порта; при другой высоте ленты
   набор скрытых меняется.
4. `Update` пишет `translate` только при |Δ| > 0.004 (как в моде), поэтому последнее
   прочитанное смещение отстаёт от внутреннего `scroll` на сотые: 12.96 против 12.99.
   Вредно только для проверок, не для вида.

`FindOrCreate` по классу подтвердил, что мебель шага 3 переиспользуется: `readout` —
тот же `Label`, что отдал `ProjectCMainMenuWindow` (сверено по ссылке), дублей нет,
у `.pc-alt` осталось 5 детей.

### 2026-10-27 — шаг 7: поведение

`ProjectCMainMenuWindow.Behaviour.cs` — второй `partial` того же класса (класс объявлен
`public sealed partial`). Дерево шага 3 не правилось: файл только пишет стили и гоняет состояние.

Где DOM скопировать буквально нельзя и что стоит вместо:

| В моде | В Unity |
|---|---|
| `el.style.setProperty('--a', a)` | `customStyle` только для чтения, поэтому каждый потребитель `var(--a, …)` получает инлайновый стиль: `opacity` у части, `filter: blur()` у `is-forming`, `translate`/`scale` у карты, `opacity` у `.pc-btn-cloud` |
| `--a` наследуется каскадом | в USS `.pc-main` объявляет `--a: 1`, и кастомные свойства в UITK действительно наследуются — иначе scrim и лента не получили бы значение; им пишется `MenuA` вручную |
| `setTimeout(fn, ms)` | `Ui.schedule.Execute(fn).StartingIn(ms)`; одноразовые элементы копятся в списке и чистятся по `isActive` |
| `window.addEventListener('pointermove')` | колбэк на `Root.panel.visualTree` — панельный корень видит указатель там, где само меню `pickingMode = Ignore`; при появлении панели позже хук перевешивается (`HookPointer` в каждом `Update`) |
| `void el.offsetWidth` для перезапуска перехода | класс `.is-on` снимается сразу и возвращается через 16 мс: UITK разрешает стили раз в кадр |
| `requestAnimationFrame` | `Update(dt)`; `AutoTick` (16 мс) по умолчанию включён, проверка гоняет кадры вручную |

Две настоящие ошибки нашла именно численная проверка, а не глаз:

1. **Порог записи оставлял позу недотянутой.** `WritePart` писал только при |Δ| > 0.0025,
   а `MenuA` — при |Δ| > 0.004. После снапа последней части в 1.0 разница с записанным
   оказывалась меньше порога, и scrim с лентой навсегда оставались на 0.997 (а при закрытии —
   на 0.001). Теперь 0 и 1 проходят точно: `endpoint && written != a`.
2. **Снимок зависел от локали.** `TakeSnapshot()` форматировал числа текущей культурой, и на
   ru-RU в отчёте стояло `1,000`; сравнение со строкой ломалось. `Items`, `ScrimOpacity`,
   `AltOpacity` и `Gust.ToString()` теперь форматируются `InvariantCulture`.

Наблюдения:

1. Проверку нельзя писать на `resolvedStyle` там, где значение пишется инлайново в том же
   кадре — читать надо после кадра панели; сами числа при этом точные (не переходные).
2. `.is-closing` снимается только при следующем открытии — так же ведёт себя `ui.js`; это не
   недоделка порта.
3. `Focus()` в редакторном окне может не дать `FocusInEvent`, поэтому подавление густа
   при возврате фокуса проверяется по `WindSeq` (+1 за жест), а не по факту фокуса.
4. Проверочное окно должно быть собственным `EditorWindow` с именем файла = имени класса,
   иначе Unity не создаст его как `MonoScript`; сам скрипт после прогона удалён.

Сознательно **не перенесено** и остаётся на потом: перетаскивание ползунков `.pc-range`
(и обновление `.pc-out`), всплывающий список `.pc-select` (в `ui.css` у него нет стилей,
а нативных попапов в UITK нет), декоративные `@keyframes` (`pc-boil`, `pc-blob-burst`,
`pc-cloud-burst`, `pc-sweep`, а также `pc-press`, который двигает кнопку — USS-правила
`.is-clearing` дают только конечное состояние) и все ~40 заглушек `[C#]` под градиентные рампы.

Визуальная приёмка шага в реальной панели — за пользователем.

### 2026-10-27 — шаг 8: наши блоки

`ProjectCMainMenuProjectPanels.cs` — третий `partial` того же класса. Новых правил USS не
понадобилось: все `pc-proj-*` пришли ещё шагом 2. Слой `.pc-proj-layer` добавляется
последним ребёнком `Root`, соседом `.pc-ui` — ровно так его перечисляет шапка
`PCMM_Menu.uss`, и так же его делает `root.append(shell)` в моде. Поэтому он рисуется
поверх дыма, карточки настроек и тоста.

Где DOM скопировать буквально нельзя и что стоит вместо:

| В моде | В Unity |
|---|---|
| `fetch` + `AbortController` + `setTimeout(8000)` | `UnityWebRequest` + `AsyncOperation.completed`; таймер — `schedule.Execute(Abort).StartingIn(8000)`, и `request.timeout = 8` на случай, если окно ещё не в панели и планировщик не тикает |
| `window.open(url, '_blank')` | `Application.OpenURL` |
| `localStorage` | `PlayerPrefs` `ProjectC.MainMenu.FirstLaunchDisclaimerAccepted` — тот же ключ, что писал `MainMenuWindow` |
| `<input type="text">` | `TextField` — единственный настоящий ввод в рантайм-панели |
| `<select>` + 9 `<option>` | `DropdownField` — единственный нативный попап; сам список выглядит как системное меню, как и `<select>` в браузере |
| `hidden` / `node.hidden = false` | инлайновый `display: None/Flex` |
| `el('p', { text })`; `<button>` | `Label`, в том числе кнопки: тема `.unity-button` не должна протекать (то же решение, что в шаге 3) |
| `.pc-proj-layer > * { pointer-events: auto }` | `pickingMode = Ignore` у слоя и `Position` у каждого из шести блоков |
| `@media (max-width: 900px)` | порог в `GeometryChangedEvent`: ширина слоя < 900 → `is-hidden` на `.pc-proj-changelog` |
| `onAction({action, ...extra})` | `OnProjectAction(action, payload)`, сбои — строкой payload |

Четыре вещи, которые выяснились по дороге:

1. **`TextField` и `DropdownField` рисует тема.** Их внутренние элементы несут фон, рамку и
   отступы `.unity-*`, и получается серая плашка внутри `.pc-proj-input` / `.pc-proj-select`.
   Своих правил на чужую тему не написать, поэтому `FlattenControl` гасит у двух уровней
   детей инлайново фон, рамки, отступы и padding — текст, каретка и стрелка попапа целы.
2. **`Dispose` не уносит слой.** `Dispose()` снимает `.pc-ui`, а `.pc-proj-layer` — его
   сосед; без `DisposeProjectPanels()` слой остался бы висеть на старой точке монтирования.
   Сам `Update()` после `Dispose` снова зовёт `Start()` — проверка поэтому перестаёт тикать
   окно сразу после `Dispose`, иначе слой строится обратно.
3. **Статус сброса склеен из двух строк.** Мод пишет только «Сброс: id»; здесь к нему
   добавляется то, что вернул `PersistenceDebugTools` («removed N file(s)…»), иначе
   админ-панель не сообщает, что именно удалилось: `Сброс: inventory — …`.
4. **Проверке нужны швы.** `ResetHandler`, `IpConnectHandler`, `LocaleHandler` и
   `UrlOpenHandler` по умолчанию — настоящие проектные API, но прогон подменяет их: иначе
   проверка удалила бы сохранения, дозвонилась бы до сервера, открыла бы браузер и записала
   бы локаль. `PlayerPrefs` дисклеймера проверка запоминает и возвращает назад.

Сознательно **не перенесено**: Esc не закрывает IP-лист (в моде у него тоже нет обработчика,
только «НАЗАД»), поле IP и селект языка не забирают фокус сами, и раскладка `.pc-proj-*`
на узких экранах ограничена одним правилом 900 px — других медиазапросов в `project_panels.js` нет.

Сетевой путь changelog прогнан только до состояния «Загрузка…»: ответ GitHub в проверке не
ждётся, рендер проверен фикстурой через публичный `RenderChangelog`. Ручная приёмка в
Play Mode — за пользователем.
