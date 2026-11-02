# Reverse Engineering Notes — UI (Project C)

Собранные наблюдения по внутреннему устройству Unity UI Toolkit, сторонних фреймворков и AAA-паттернов. Публичная информация (доки, GDC-токи, форумы). Не содержит проприетарного кода.

---

## 1. Unity UI Toolkit — внутреннее устройство

### 1.1 Retained mode & batching
- UI Toolkit — retained-mode: дерево `VisualElement`, стили, layout (Yoga/Flexbox), рендер в один (или несколько) mesh-буферов.
- Батчинг по GPU-состоянию: шейдер, текстуры, mesh. Разрывы батча: смена текстуры (>8 distinct), маски (stencil), смена материала, смена z/layer.
- Pre-allocated vertex buffer ("heap"); задаётся `Vertex Budget` в PanelSettings. Если UI не помещается — буфер сплитится → дополнительные draw calls.
- Usage hints: `DynamicTransform` для анимируемых элементов, `GroupTransform` для контейнеров с множеством анимируемых детей. Анимировать только `translate/scale/rotate`, не layout-свойства.

### 1.2 Layout (Yoga)
- Flexbox-семантика. Ключевые готчи (из `UI_TOOLKIT_GUIDE.md`):
  - В flex-column дочерний `width:100%` не нужен — `align-self: stretch` (по умолчанию `auto`=stretch).
  - `flex-grow:1` + `flex-basis:0` → min-size = content size → может не сжиматься → wrap. Нужен `flex-basis:0` + `flex-shrink:1`.
  - `flex-shrink:1` + `min-height:0` → сжатие в flex-column (для scroll-секций).
  - Header/actions с `flex-shrink:0`, иначе съезжают.
  - `overflow: hidden` для scroll-контейнеров; ListView с `fixedItemHeight` для scroll.

### 1.3 Specificity & темизация
- В Unity 6+ type-selector (`.unity-base-button`) имеет **низшую** specificity, чем class-selector (`.my-button`). Без `!important` class выигрывает.
- `!important` — надёжный, но тяжёлый путь; альтернатива — compound selector (`.unity-button.my-button`).
- Theme: `PanelSettings.themeUss` должен указывать на `UnityDefaultRuntimeTheme.tss`, иначе нет базовых стилей → UI «плоский».
- USS custom properties (`--*`) на `:root` + `var(--name, fallback)`. Unity 6 добавляет `@import`, `background-size`, `-unity-background-image-tint-color`, parent-state cascades. Порядок импортов важен (general → specific → mobile last).

### 1.4 UIDocument lifecycle
- `UIDocument` сам монтирует `visualTreeAsset` в `rootVisualElement` при `OnEnable`. Ручной `Clear()+CloneTree()` конфликтует с этим → двойной маунт / хрупкость при domain reload.
- Канонический паттерн (из `UI_TOOLKIT_GUIDE.md`): `_root = _doc.rootVisualElement;` + добавление USS через `styleSheets.Add` (дедуп); без `Clear/CloneTree`.
- `visualTreeAsset` и `panelSettings` должны быть назначены (через SerializedObject в Editor или Inspector). Null → пустой root.
- `Resources.Load<StyleSheet>` возвращает «inlineStyle»-мусор (ScriptedImporter не отрабатывает) — использовать только SerializedField/Inspector refs.

### 1.5 Data binding (Unity 6)
- Runtime Bindings API: binding plain C# объектов к visual elements.
- По умолчанию reflection-based → медленно для тяжёлых интерфейсов. Оптимизация: `[GeneratePropertyBag]`, `[CreateProperty]` (codegen), `INotifyBindablePropertyChanged`, `IDataSourceViewHashProvider`.
- Binding modes: `TwoWay` (inputs), `ToOneWay` (display), `ToSource` (редко). Update triggers: `OnValueChanged` (default) / `OnFocusOut`.
- Для высокочастотных данных (HP, cooldown, чат-тайпинг) — bypass binding, прямая запись в элемент, троттлинг (≤10 Hz текст, per-frame только transforms).

### 1.6 Text
- TextMeshPro SDF: один атлас на вес/стиль; dynamic atlas с clear-on-build; отключить AutoSize на dynamic fields; SDF16 для крупных глифов.
- UITK text: статические font assets; fallback-цепочка для CJK/Arabic; проверять shaping в билде.
- Лимит: ≤4 семейств шрифтов.

### 1.7 World-space UI
- UITK world-space (PanelSettings renderMode = WorldSpace) появился в 6.x, но незрелый. uGUI для world-space пока надёжнее.
- Рендер в RenderTexture через отдельную камеру + quad — стоит проход, бюджетировать.

### 1.8 Производительность (из доков Unity + Unite 2024)
- 1000 элементов: UITK 3 draw calls / 2 ms vs uGUI 25 / 8 ms (бенчмарк Angry Shark).
- ListView/TreeView виртуализация: 10k+ items.
- Скрытые панели: `DisplayStyle.None` останавливает layout+render; `opacity:0` — всё ещё рендерится.
- Маски: прямоугольные (shader-based, без stencil) предпочтительны; сложные/круглые — только где нужно, ≤7 вложенных уровней, плоская иерархия.
- Анимация: классы не переключать на больших иерархиях во время анимации; использовать inline style.

---

## 2. Сторонние фреймворки (кратко)

### 2.1 NoasisGUI
- XAML over C++ core; «sub-millisecond» рендер; Noasis Studio интегрирован в Unity; глубокая интеграция.
- Лицензирование: tiers, путаница в лицензиях. Стоимость масштабируется.
- Нет нативного Figma-импортёра; пайплайн XAML/Blend/Studio.
- Вывод: premium-альтернатива, если есть бюджет и XAML-экспертиза. Для Project C — не сейчас.

### 2.2 Coherent Gameface
- HTML/CSS/JS в нативном плагине; live debugger; React/Angular/Svelte возможны.
- Память browser-class; licensing tiers; overkill для Unity-native команды.
- Вывод: для web-контента/порталов, не для core UI.

### 2.3 Rive
- GPU vector/raster; `.riv` = арт + моушн + логика; state machines; view models; Luau; виртуализированные списки; текстовый ввод.
- Тысячи векторов при 120 fps; файлы на 90% меньше.
- Unity Asset Store runtime; C# control.
- Figma: импорт SVG/PSD/шрифтов/аудио (не прямой Figma, но SVG round-trip).
- Вывод: лучший слой анимированного векторного UI (меню, иконки, переходы, data-bound микро-интеракции). Дополняет, не заменяет layout-систему.

### 2.4 ThorVG / Unity Vector Graphics
- ThorVG: MIT, production-ready C++ vector engine (SVG Tiny + Lottie), WebGPU backend. Нет официальной Unity-интеграции.
- Unity Vector Graphics package — experimental, не рекомендован для продакшена.
- Вывод: избегать для shipping UI.

### 2.5 WebView (Vuplex, UniWebView, gree/unity-webview)
- Захват текстуры (`CapturePreview`) дорогой; gree по умолчанию обновляет каждый 3-й кадр.
- Память heavy; z-order проблемы (webview поверх Unity UI); input routing; transparency limits.
- Вывод: web-контент, auth-флоу, мод-порталы — не core game UI.

### 2.6 Dear ImGui / IMGUI
- Immediate mode; отлично для dev-инструментов; один draw call на окно; нет designer workflow, a11y/локализации.
- Unity IMGUI (OnGUI) явно не рекомендован для player UI.
- Вывод: только debug overlays.

---

## 3. AAA-паттерны (публичные наблюдения)

| Игра / Источник | Наблюдение |
|---|---|
| **Destiny 2** (Bungie GDC 2016) | Плотный информационный UI; progressive disclosure; item-centric focus; «глаза остаются в одном месте»; избегать принудительных сдвигов взгляда. |
| **Cyberpunk 2077** (CDPR GDC Showcase 2023) | UI-тех с нуля для тысяч независимых UI-инстансов по всему Night City; UI использовался для геймплея; акцент на бюджеты и платформенные таргеты. Урок: бюджетировать множество инстансов рано; агрессивный пулинг. |
| **Fortnite** (Epic GDC 2018) | UI как часть общего perf-бюджета; конфигурируемый HUD; кроссплатформенная консистентность. |
| **Valorant** (Riot GDC 2017, 2XKO) | Единый кросспроектный UI-фреймворк; масштабируемая brand identity; переход от patchwork к единой гибкой системе для «forever game». |
| **Genshin Impact** (miHoYo GDC 2021) | Аниме-стиль open world; UI держит и новичка, и ветерана; постоянная итерация UX; много систем без перегрузки. |
| **Star Citizen** (CIG) | Очень плотный симуляционный UI; тяжёлый diegetic UI; perf-проблемы с множеством элементов — cautionary tale по скоупу. |
| **SimCity 2013** (EA GDC) | Web-тех UI внутри нативного движка через embedded browser; валидирует web-inspired retained-mode подход UITK. |
| **Sybo Games** (GDC 2019) | Единый фреймворк между проектами во избежание визуальной/интерактивной несогласованности; общая компонентная библиотека. |
| **DICE LA** (GDC 2018) | Консистентный UI-арт через теорию + best practices; дисциплина арт-дирекшена. |
| **Nordeus** (Unite) | Когда лимиты фреймворка бьют — пишут кастомные иерархии; взвешивать cost/benefit. |

**Общие AAA-нити:**
1. UI — first-class система с собственным бюджетом, не afterthought.
2. Переиспользуемые компонентные библиотеки + design tokens.
3. Event-driven, data-bound, virtualized.
4. Большие инвестиции в tooling (Figma/Blend/Noasis Studio) и empowerment артистов.
5. Perf-guardrails (draw calls, overdraw, pooling) enforced в CI.

### 3.1 Screen/Window/Panel/Widget таксономия (Sybo-style)
- **Screen** = self-contained full-context UI surface. Два вида:
  - **Panel** — persistent chrome, сосуществует со всем (HUD, minimap, chat dock, currency bar, action bar). Всегда смонтирован или дёшево тогглится.
  - **Dialog (Window)** — модальная/semi-модальная поверхность, фокус внимания (inventory, map, settings, crafting). Имеет историю.
- **Widget** — переиспользуемый data-bound фрагмент (item slot, chat line, quest entry, tooltip, damage number).
- **Layer** — контейнер, владеющий одним классом Screen; контролирует z-order, input blocking, transitions (DialogLayer, PanelLayer, OverlayLayer, TooltipLayer, WorldSpaceLayer).
- **Para-layer** — иерархический bucket, в который Layer репарентится (UnderModals, Modals, Tooltips, Debug), чтобы z-order был структурным, а не числовым.
- **Правило:** game code никогда не трогает Screens/Layers напрямую. Общение через `UIFrame`.

### 3.2 MVVM vs MVP vs event-driven
- Строгий MVVM в играх создаёт параллельные иерархии моделей и boilerplate (публичная критика MVC/MVVM для игр).
- Прагматичный AAA-ответ (Bungie/Sybo-style):
  - Один контроллер на Screen (View + тонкий presenter в одном классе), **не** отдельный ViewModel на каждое поле.
  - Общие game-owned data models — это «Model»; Screen читает их через data binding или события. Не дублировать game state в UI-моделях.
  - Events/signals для cross-module decoupling: game systems эмитят (`OnInventoryChanged`); UI подписывается, если смонтирован. Если UI нет — никакой работы, никаких аллокаций.
  - MVVM-стиль binding используется на листьях: UITK runtime bindings биндят свойства Screen к UXML. Это механически, не архитектурно.
- Итог: **MVP-ish screens + event-driven data flow + UITK leaf binding**. Избегать full MVVM frameworks (StrangeIOC-style).

### 3.3 Modal stack & focus
- `ModalStack` (LIFO) Dialogs. Открытие пушит; закрытие попит. Верхний Dialog — единственный интерактивный; нижние затемняются + input-blocked (не уничтожаются — сохраняется состояние).
- Focus ring: каждый Dialog объявляет default focus target и tab order. При push фокус переходит к новому default; при pop — возвращается к last-focused предыдущего верхнего.
- Navigation graph: явные next/prev (up/down/left/right) per control; fallback — геометрический nearest-neighbor. UITK `Focusable`+`FocusController` — база; надстройка `FocusNavigator` для wrap-around и modal scoping.
- Escape/Back попит верхний modal; если стек пуст — открывает pause menu.

### 3.4 Input routing
Слои по приоритету (высший первый): SystemOverlay → Modal → Tooltip → HUD → World. Центральный `InputRouter` (на Unity Input System) диспатчит каждое input action в высший активный слой, который его claim-ит. World input **приостанавливается** (не просто игнорируется) при открытом модале.

### 3.5 Локализация
- Unity Localization + UITK bindings (`LocalizedString`).
- Бюджет +40% расширения текста (немецкий/финский); Flexbox для reflow.
- RTL: Arabic/Hebrew требуют shaped text + RTL layout. UITK text shaping ограничен — валидировать рано; fallback на TMP для RTL-heavy поверхностей.
- Font fallback chains для CJK; не полагаться на OS-шрифты в билдах.
- Переключение локали без reload сцены: bind locale к `Locale` asset, re-evaluate bindings.

### 3.6 Доступность
- Windows: интеграция Unity Accessibility module (screen reader nodes) для Narrator. Каждый интерактивный UITK-элемент → accessibility node (role, label, value, action).
- Клавиатура: полная tab-навигация, видимое focus ring (high-contrast), skip links.
- Визуал: уважать Windows high-contrast themes, масштабируемость (DPI), color-blind-safe палитры (не кодировать смысл только цветом), motion-reduction.
- Аудио: субтитры для voiced контента; UI-звуки с регулировкой громкости.

### 3.7 Theming / design tokens
- Токены — single source of truth: цвета, отступы, радиусы, типографика, тайминги моушна, elevation. Хранятся как USS custom properties (`--color-primary`, `--space-md`, `--radius-pill`, …) на `:root`.
- Темы — **ассеты, не код**: `ThemeData` ScriptableObject бейкает токены в USS `StyleSheet` sub-asset; `ThemeApplier` добавляет этот sheet в root панели. `var()` cascade перекрашивает всё дерево, включая `:hover/:disabled/:checked`.
- Dark + Light из коробки; brand/seasonal темы в Theme Configurator с live preview. Scope темы к `:root` (вся панель) или классу (`.theme-night`), чтобы две темы сосуществовали.
- Один `.compact` класс на root переключает отступы/tap targets — forward-compat.

---

## 4. Figma-пайплайн (реверс-инжиниринг инструментов)

| Инструмент | Тип | Выход | Примечание |
|---|---|---|---|
| Official UI Toolkit Connector for Figma | Официальный, experimental | UXML + USS + tokens | Основной путь; beta |
| FigmaToUnity (TrackMan) | Open source | UXML + USS | Fallback |
| cdmvision unity-figma-importer | Open source | uGUI prefabs + UITK | Hybrid fallback |
| Figma Converter for Unity (D.A. Assets) | Платный | Multi-framework | Safety net |
| Unity Assistant 2.7 (pre) | Официальный, pre-release | Figma import | Watchlist |
| VectoUI | Платный | uGUI prefabs | Legacy fallback |

**Workflow:** Figma (дизайн + токены) → sync → UXML/USS в `Assets/UI/Screens/...` → инженер вяжет ViewModel + регистрация → playtest → обратная связь в Figma. Не hand-edit структуру сгенерированного UXML способом, ломающим re-sync; ручные правки — в companion `.cs` или partial UXML includes.

---

## 5. Архитектурные интерфейсы (см. blueprint §7.3)

См. `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` §7.3: `IUIFrame`, `IUILayer`, `IUIScreen`, `IModalStack`, `IFocusNavigator`, `IInputRouter`, `IThemeService`, `ILocalizationService`, `IUIPool<T>`, `ITransition`.

---

## 6. Источники (см. 02)

Полный список источников — в `02_SOLUTIONS_RESEARCH_2024-2026.md` §6.