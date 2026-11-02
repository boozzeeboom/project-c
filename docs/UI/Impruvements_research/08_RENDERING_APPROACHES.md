# Rendering Approaches for AAA UI in Unity (Project C)

**Контекст:** Unity 6 (6000.5.2f1), URP 17.5.0, Windows standalone, MMO-песочница. Цель — выйти за рамки «Windows 98»: живой, адаптивный, анимированный, размытый, светящийся UI без тяжёлых плагинов.

**Авторство:** research-документ, дополняет `02_SOLUTIONS_RESEARCH_2024-2026.md`, `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md`, `cloud-light-interactive-research-2026-09-29.md`. Фокус — именно **рендеринг** (как рисовать AAA-визуал), а не архитектура окон.

**Дата:** 2026-11-02

---

## 0. TL;DR

- **Основной рендерер UI остаётся UI Toolkit (UITK)** на URP. Он даёт retained-mode, Flexbox, USS-токены, виртуализацию, Figma-пайплайн и (с 6.3+) **кастомные шейдеры через Shader Graph**. Это база «без тяжёлых плагинов».
- **Rive** — лучший слой ** motion/векторной графики** поверх UITK: state machines, data binding (view models), GPU-рендер, vector feathering (мягкие края/глоу без blur), виртуализированные списки, текстовый ввод, gamepad. Runtime MIT, бесплатно для шиппинга (splash на free-плане).
- **NoesisGUI** — премиум-альтернатива (XAML + sub-ms рендер + Noesis Studio в Unity), но **дорого** (€195 indie / €9k+ pro) и vendor lock-in. Оправдан только если XAML-экспертиза и бюджет.
- **Coherent Gameface** — web-стек (HTML/CSS/JS), browser-class память, платные тиры. Оправдан при web-команде; иначе — избыточен.
- **WebView (Vuplex и др.)** — только для реального web-контента (порталы, OAuth, моды). Не для core UI: texture capture, z-order, input latency, память.
- **Кастомные шейдеры** (Shader Graph → UI Material) — главный «бесплатный» инструмент AAA-визуала в UITK/uGUI: глоу, блюр, bloom, SDF-текст, скруглённые углы, градиенты. Доступны с Unity 6.3+ (UITK custom shaders).
- **VFX Graph** — **не для UI overlay** напрямую (VFX Graph рендерится в мир/камеру). Для UI-частиц/гало — рендер в RenderTexture + вывод на quad/VisualElement, либо world-space. Дорого; использовать точечно.
- **Backdrop-filter (blur за элементом)** — доступен **только с Unity 6.6 (6000.6)** на URP. На нашей 6000.5.2f1 — **нет**. Дизайн должен работать без настоящего backdrop-blur (эмуляция: полупрозрачные поверхности + hairline + верхний блик).
- **Комбинация для «живого» UI:** UITK (layout/токены/списки) + Rive (анимированные векторные акценты/иконки/переходы) + Shader Graph (глоу/блюр/скругления/SDF-текст) + USS transitions + свой микро-твинер + audio. Без WebView/Noasis/Coherent в core.

---

## 1. Сравнение рендеринг-подходов

### 1.1 Таблица сравнения

| Подход | Визуал (глоу/блюр/частицы) | Производительность | Авторинг | Figma/дизайн | Лицензия | Риск |
|---|---|---|---|---|---|---|
| **UI Toolkit + URP** | USS filter/backdrop-filter (6.6+), custom shaders (6.3+), SDF-текст через TMP | Высокая (3 dc / 2 ms @1k) | Web-like (UXML/USS), UI Builder | Официальный экспериментальный коннектор + 3rd party | Бесплатно (Unity) | Средний (runtime rough, API churn) |
| **Rive (векторный GPU-рендер)** | Vector feathering (мягкие края/глоу без blur), аналитический AA, raster | Очень высокая (тысячи векторов @120 fps заявлено) | Rive Editor + CLI, state machines, view models | SVG/PSD/шрифты/аудио round-trip | Runtime MIT, free; splash на free-плане | Низко-средний |
| **NoesisGUI (XAML/C++)** | XAML-стили, sub-ms рендер, Noesis Studio | Очень высокая (sub-ms) | XAML + Blend/Noasis Studio в Unity | Нет нативного Figma-импорта | Платно: €195 indie / €9k+ pro | Средний (vendor) |
| **Coherent Gameface (HTML/CSS/JS)** | CSS (blur, glow через web-стек) | Средняя (browser-class память) | Web-навыки, live debugger | Нет нативного | Платные тиры | Средний |
| **WebView (Vuplex и др.)** | Полный web-стек | Низкая (texture capture, 3-й кадр) | Web-разработка | N/A | Платные ассеты | Высокий (z-order, input) |
| **Custom shaders (Shader Graph)** | Полный контроль: глоу, блюр, bloom, SDF, скругления | Зависит от шейдера; batching ломается при смене материала | Shader Graph (визуально) | Ручной перенос | Бесплатно | Низкий (но цена авторинга) |
| **VFX Graph** | Частицы, гало, искры, дым | Высокая в мире; в UI — через RenderTexture | VFX Graph (визуально) | Ручной | Бесплатно (Unity) | Средний (не нативно для overlay) |

### 1.2 Ключевые различия по задачам

| Задача AAA-визуала | Лучший инструмент | Заметки |
|---|---|---|
| Глоу вокруг кнопки/элемента | Shader Graph (emissive + bloom) или Rive vector feathering | USS `filter: drop-shadow` в UITK **не поддерживается** (дока USS filter). Только через шейдер/материал или Rive. |
| Размытие фона за панелью (glassmorphism) | `backdrop-filter: blur()` — **только UITK, только URP, только с 6.6** | На 6000.5.2f1 недоступно. Обход: полупрозрачные поверхности + hairline + блик. Альтернатива: Shader Graph с grab-pass (дорого, ломает batching). |
| Скруглённые углы | USS `border-radius` (UITK/uGUI) — из коробки | Для SDF-масок/скруглений без артефактов — SDF-шейдеры (TLabAltoh/Unity-SDF-UI-Toolkit и аналоги). |
| SDF-текст ( crisp at any scale) | TextMeshPro SDF (uGUI) / UITK text (TextCore) | UITK использует TextCore (TMP-совместимый). Один атлас на вес/стиль. |
| Частицы/гало в UI | VFX Graph → RenderTexture → UI quad; либо uGUI world-space canvas с Particle System | Прямой overlay VFX Graph в UITK не поддерживается. |
| Анимированные иконки/переходы | Rive state machines + view models | Лучшая связка «дизайнер анимирует, разработчик драйвит данными». |
| Циркулярный прогресс | Rive (vector) или USS `conic-gradient` (не в UITK) → шейдер/SDF | UITK не поддерживает conic-gradient. Делать через SDF-шейдер или Rive. |
| Реактивные списки 10k+ | UITK ListView + Rive virtualized scrolling list | Rive имеет встроенный virtualized scrolling (только видимые строки). |

---

## 2. Rive runtime for Unity

### 2.1 Что это

- **GPU-векторный/растровый рендерер**, `.riv` — один файл (арт + моушн + логика). Runtime MIT, без роялти/платы за шиппинг.
- Unity: работает с Unity 2021 LTS+, включая Unity 6. Устанавливается из Asset Store или GitHub.
- Рендерится в **uGUI Canvas** или на **mesh** (3D-поверхности). В UITK — через render-to-texture или встраивание как компонент.
- **Производительность:** аналитический AA, vector feathering (мягкие края/глоу **без** стоимости blur). Заявлено: тысячи векторов @120 fps, файлы на 90% меньше альтернатив.

### 2.2 State machines

- Набор анимационных состояний и переходов между ними. Управляются **косвенно** — через data binding-свойства (не прямым вызовом состояния).
- Дизайнер назначает default state machine для каждого артборда в Rive Editor; может создать несколько (разные конфигурации).
- Runtime: state machine «advances» один раз в кадр на дельту времени. **Оптимизация:** runtime детектит, когда дальнейших изменений не будет (нет активных переходов/анимаций) и **останавливает advance** (settled). Разбуждается внешними действиями (ввод, изменение data binding). Можно принудительно `play`.
- **Gamepad listeners и Focus** встроены в Rive Editor (навигация по меню контроллером/клавиатурой). В Unity/Unreal поддержка gamepad — в roadmap (проверять feature support).

### 2.3 Data binding (view models)

- **View models** описывают набор свойств, но сами по себе не хранят значения — это делают **view model instances**.
- Свойства: числа, строки, булевы, **списки** (для инвентарей/повторяющихся элементов), цвета, ассеты (шрифты, блобы).
- Игра устанавливает значения из кода → state machine реагирует → игра может слушать изменения, сделанные в файле.
- **Global view models** (окт 2026): авторские default-инстансы биндятся автоматически; views перерисовываются при изменении.
- **Luau/AnimaScript** — скриптинг внутри файла (логика, процедурный моушн, кастомный рисунок).

### 2.4 Возможности для AAA UI

| Возможность | Применение в Project C |
|---|---|
| State machines | Hover/selected/low-health/transition состояния живут в файле, не в коде |
| Data binding (view models) | HP/боеприпасы/имя игрока → UI без ручной синхронизации |
| Responsive layouts | Меню/HUD рефлоу под любой экран |
| Vector feathering | Мягкие края/глоу без стоимости blur-пасса |
| Virtualized scrolling lists | Инвентари/лидерборды 10k+ без просадки |
| Text input | Поля ввода имени/чата/поиска |
| Gamepad listeners + Focus | Навигация по меню контроллером |
| Локализация | Смена строк через data binding; RTL-флип; fallback-шрифты |
| Аудио-ивенты | Звуки из ключфрейма/перехода/листенера |
| Components + Libraries | Переиспользование кнопок/панелей/иконок |

### 2.5 Ограничения/риски

- **Не заменяет layout-систему.** Rive — motion/векторный слой, не оконный менеджер. Формы/таблицы/чата всё равно нужны в UITK/uGUI.
- **Gamepad support в Unity/Unreal** — в roadmap на момент исследования; проверять feature support перед ставкой.
- **Free-план:** экспортированные файлы показывают Rive splash; без splash — платный план (Cadet $9/seat/мес и выше).
- **Авторинг:** дизайнерам нужно освоить Rive Editor (отдельный инструмент, не Figma). Figma→Rive через SVG/PSD round-trip.
- **Встраивание в UITK:** Rive рендерится в uGUI Canvas или mesh. В чистом UITK-дереве — через render-to-texture или компонент. Это добавляет один render pass.

### 2.6 Когда использовать в Project C

- Анимированные иконки/бейджи/индикаторы (HP-кольцо, кулдаун, статус).
- Переходы/вайпы экранов (loading, смена сцены).
- Data-bound микро-интеракции (hover-эффекты, нажатия, state-переключения).
- Сложные векторные элементы: кольцевой прогресс, гало, свечение вокруг аватара.

**Не использовать:** для текстовых таблиц, чата, списков форм, настроек (это UITK/uGUI).

---

## 3. NoasisGUI

### 3.1 Что это

- **Декларативный XAML поверх лёгкого C++ ядра.** Без внешних зависимостей. «Sub-millisecond» рендеринг на всех платформах.
- **Глубокая интеграция с Unity:** XAML как нативные ресурсы; **Noasis Studio теперь интегрирован внутрь Unity** (заменяет Blend); распространение через .tgz / scoped registry.
- **Возможности:** data-driven binding, VS Code XAML preview/autocomplete, real-time hot reload, сэмплы.
- **Noasis Studio** — бесплатный WYSIWYG-редактор (скачивается из Microsoft Store), дизайнеры без кода.

### 3.2 Производительность и память

- C++ ядро, оптимизированное API для C#. Пользователи отмечают: быстрее WPF/UWP по загрузке контролов и рендерингу, память сопоставима с WPF/UWP.
- Sub-ms рендеринг заявлен как приоритет №1.

### 3.3 Лицензирование (актуально на 2026)

| Тир | Условие | Цена |
|---|---|---|
| **Indie** | \< €100K годовой выручки, \< €250K бюджет проекта | **€195** per project, все платформы |
| **Pro** | \< €1.5M бюджет проекта | €9000 первая платформа + €3600 доп. |
| **Premium** | €1.5M–€12M бюджет | €18000 первая + €7200 доп. |
| **Enterprise** | \> €12M | по запросу |

- Royalty-free, perpetual license. Indie включает minor releases lifetime + 1 год major upgrades.
- Pro/Premium/Enterprise: priority/premium support обязателен первый год (€3200–€6300/год).
- **Бесплатной опции больше нет** (раньше была free tier; убрали).
- Noasis Studio бесплатен для скачивания/использования как standalone editor.

### 3.4 Плюсы/минусы для Project C

**Плюсы:** AAA-производительность, XAML-инструментарий (Blend/Studio), data binding, hot reload, все платформы.

**Минусы:**
- **Стоимость:** для MMO-проекта с бюджетом \> €250K — минимум €9000+support, что для indie/solo-разработчика неприемлемо.
- **Vendor lock-in:** XAML-экспертиза, собственный рендерер, нет нативного Figma-импорта (дизайн-пайплайн = XAML/Studio).
- **Риск:** зависимость от вендора, roadmap под их контроль.

### 3.5 Вердикт

**Не для core UI в Project C** (бюджет не оправдывает, команда Unity-native). Рассматривать только если: (а) профилирование UITK покажет непреодолимый потолок, (б) есть XAML-экспертиза, (в) бюджет позволяет Pro-тира. Триал перед решением обязателен.

---

## 4. Coherent Gameface

### 4.1 Что это

- **Web-технологии (HTML/CSS/JS) в нативном плагине.** Live HTML debugger, компонентная библиотека. Поддерживает React/Angular/Svelte.
- Движок сохраняет контроль над рендерингом, памятью, загрузкой ресурсов, потоками.

### 4.2 Производительность и память

- Native C++ рендеринг; внутренние кэши для CPU/GPU памяти; batching CSS-свойств.
- **Но:** browser-class memory footprint. Каждый webview — отдельный процесс/контекст. Для MMO с десятками окон — серьёзная нагрузка.
- Roadmap 2026: улучшение update performance (избегать лишнего парсинга HTML при простых текстовых обновлениях).

### 4.3 Плюсы/минусы

**Плюсы:** web-навыки переиспользуются; live editing; data binding; локализация; все платформы.

**Минусы:** тяжёлая память; платные тиры; менее Unity-native; overkill для Unity-native команды.

### 4.4 Вердикт

**Не для core UI.** Оправдан только если команда состоит из web-разработчиков и нет Unity UI-экспертизы. Для Project C (Unity-native, MMO, много окон) — избыточен и дорог по памяти.

---

## 5. WebView-based UI (Vuplex, Embedded Browser, gree/unity-webview)

### 5.1 Как работает

- Встраивает реальный браузер (Chromium/WebKit/Gecko) как текстуру или нативный overlay.
- Vuplex: поддерживает Native 2D Mode (взаимодействие через Unity → браузер), WebGL-ограничения (только Native 2D, нельзя ставить объекты поверх).

### 5.2 Производительность

- Windows: захват через `CapturePreview` дорогой. gree/unity-webview по умолчанию обновляет **каждый 3-й кадр** для поддержания FPS.
- Память: тяжёлая; per-webview стоимость.
- Рекомендации Vuplex: ограничивать количество webview, паузить неиспользуемые, снижать разрешение.

### 5.3 Z-order и input

- **Z-order:** webview покрывает Unity UI (особенно в overlay-режиме). Нативный 2D mode позволяет размещать Unity-объекты поверх, но с ограничениями.
- **Input:** ввод детектится через Unity и ретранслируется в браузер → латентность. Поддержка New Input System.
- **Transparency:** ограничена (особенно на мобильных, но для Windows standalone терпимо).

### 5.4 Вердикт

**Только для реального web-контента:** порталы модов, OAuth/авторизация, встроенный браузер, медиа. **Не для core game UI** на MMO-масштабе (память, texture capture, z-order, input latency).

---

## 6. Custom shaders for UI

### 6.1 Что доступно в Unity 6

- **UI Shader Graph** (Unity 6.3+ LTS): создание кастомных шейдеров для UI-элементов через Shader Graph. Применяются к VisualElement через USS `material` или C#.
- **Возможности:** glow, blur, интерактивные цветовые изменения, text rendering, rounded corners, SDF.
- **URP Cookbook:** compute shaders для частиц; Shader Graph UGUI Shaders Sample (демонстрация динамических, resolution-independent UI).
- **TMP/SDF:** TextMeshPro SDF-атласы (один на вес/стиль), SDF16 для крупных глифов.

### 6.2 Конкретные техники AAA-визуала

| Техника | Как | Стоимость |
|---|---|---|
| **Glow (свечение)** | Emissive-канал + Bloom (URP Volume) вокруг элемента; либо shader с outer glow | Низкая-средняя (bloom — один pass) |
| **Blur (размытие)** | Shader Graph blur (grab-pass) на элементе; для backdrop — `backdrop-filter` (6.6+) | Высокая при grab-pass; низкая для backdrop-filter |
| **Bloom** | URP Volume Bloom + emissive материалы на UI | Один pass на весь экран (дешево) |
| **Rounded corners** | USS `border-radius` (из коробки) или SDF-шейдер | Низкая |
| **SDF text** | TMP SDF (uGUI) / TextCore (UITK) | Один атлас на шрифт |
| **Circular progress** | SDF-шейдер (кольцо с прогрессом) или Rive | Низкая |
| **Halos (гало)** | Shader с радиальным градиентом + bloom; или Rive vector feathering | Низкая-средняя |
| **Gradient borders** | Два вложенных слоя (внешний hairline + внутренний блик) — USS не поддерживает gradient borders | Низкая |

### 6.3 SDF-библиотеки

- **TLabAltoh/Unity-SDF-UI-Toolkit** (GitHub): UI-компонент и утилиты для рендеринга UI-графики с outlines, shadows, rounded corners через signed distance fields.
- **Unity Vector Graphics package** — экспериментальный, **не рекомендован для продакшена**.
- **ThorVG** — MIT, production-ready C++ vector engine (SVG Tiny + Lottie), WebGPU backend; нет официальной Unity-интеграции (высокая цена обёртки).

### 6.4 Важные ограничения

- **USS `filter: drop-shadow()` не поддерживается** в UITK (явно в доке USS filter). Мягкая тень — вложенным слоем/9-slice.
- **USS `backdrop-filter`** — только с Unity 6.6 (6000.6), только URP, только screen-space панели. На нашей 6000.5.2f1 — **нет**.
- **Gradient borders / `border-image`** — нет в USS. Эмуляция двумя слоями.
- **Кастомный шейдер ломает batching** при смене материала. Держать ≤8 distinct текстур/материалов на батч; переиспользовать материалы.

### 6.5 Рекомендация по шейдерам в Project C

1. **Базовый визуал (глоу, скругления, градиенты) — через USS + стандартные свойства** (border-radius, opacity, background-color, tint).
2. **Точечные AAA-эффекты (кольцевой прогресс, гало аватара, свечение HP-бара) — через Shader Graph → UI Material**, применяемый к конкретным элементам.
3. **Bloom (URP Volume) — глобально** для всех emissive UI-элементов (дешево, один pass).
4. **SDF-шейдеры — для критичных элементов** (кольцевой прогресс, скруглённые маски без артефактов), не для всего UI.
5. **Не делать grab-pass blur на каждом элементе** — слишком дорого. Использовать backdrop-filter (когда обновимся до 6.6) или эмуляцию.

---

## 7. VFX Graph for UI particles/halos

### 7.1 Возможности и ограничения

- **VFX Graph** — визуальный редактор частиц на GPU. Рендерится в **мир/камеру**, не в UI overlay напрямую.
- **Для UI-частиц:** рендер VFX Graph в **RenderTexture** через dedicated camera → вывод на quad/VisualElement в UI. Или world-space canvas с Particle System (uGUI).
- **Overlay canvases** всегда рисуются последними; Particle System — 3D-меши, не UI-графика → canvas не может их отрендерить в overlay-режиме. Нужен отдельный camera/render texture.

### 7.2 Применение в Project C

| Задача | Решение | Стоимость |
|---|---|---|
| Искры/конфетти при получении лута | VFX Graph → RenderTexture → UI quad | Средняя (один render pass) |
| Гало вокруг аватара/предмета | Shader (радиальный градиент + bloom) — дешевле | Низкая |
| Пыль/дым в фоне меню | VFX Graph в мире (за UI), UI полупрозрачный поверх | Низкая (фоновый) |
| Свечение HP-бара | Shader emissive + bloom | Низкая |

### 7.3 Рекомендация

- **VFX Graph — точечно**, только для «вау»-моментов (лут, апгрейд, победа), не для постоянного UI.
- **Гало/свечение — через шейдеры** (дешевле, батчатся лучше).
- **Не делать VFX Graph на каждый элемент** — render texture на каждый элемент = катастрофа по памяти/draw calls.

---

## 8. Unity UI Toolkit: limitations & workarounds для AAA-визуала

### 8.1 Известные ограничения (2025–2026)

| Ограничение | Workaround |
|---|---|
| `backdrop-filter` — только 6.6+, только URP, только screen-space | На 6000.5: эмуляция (полупрозрачные поверхности + hairline + блик). Обновиться до 6.6 для настоящего blur. |
| `filter: drop-shadow()` не поддерживается | Вложенный слой/9-slice для тени. |
| Gradient borders / `border-image` — нет | Два вложенных слоя (внешний hairline + внутренний блик). |
| `filter` (USS) — blur/grayscale/invert и др. есть, но не backdrop | Использовать `filter` для эффектов на самом элементе; для фона — backdrop-filter (6.6+). |
| USS variables нельзя мутировать из C# во всех случаях | Менять через stylesheet swap (ThemeApplier), не runtime var mutation. |
| Runtime data binding — reflection по умолчанию | `[GeneratePropertyBag]` + `[CreateProperty]` + change-tracking interfaces (INotifyBindablePropertyChanged). |
| Некоторые компоненты только текстовые (Images, TwoPaneSplitView) | Кастомные VisualElement-контролы. |
| Runtime debugger не редактирует live text | Тестировать через UI Builder + Play Mode. |
| World-space UI — молодой (6.2+) | Оставить uGUI для world-space в v1. |
| Custom shaders — только 6.3+ | Обновиться до 6.3 LTS минимум; на 6000.5 — доступны. |
| `ProgressBar` жёсткий | Структура `bg+fill`, перекрашивание. |
| `DropdownField`/`GenericDropdownMenu` не стилизуются локальным USS | `CustomDropdown` (VisualElement-based, попап на `panel.visualTree`). |

### 8.2 Производительность (guardrails)

- **≤4 draw calls для chrome** (один на слой: HUD, Modal, Tooltip, Overlay).
- **Vertex Budget** в PanelSettings подогнать под весь UI (20k–50k вершин) — один буфер.
- **Usage hints:** `DynamicTransform` для анимируемых, `GroupTransform` для контейнеров с анимированными детьми.
- **Анимировать transforms** (`translate/scale/rotate`), никогда layout-props (`width/height/top/left`).
- **Не переключать USS-классы** на больших иерархиях во время анимации — inline style.
- **Rectangular masks** (shader-based, без stencil) по умолчанию; complex ≤7 уровней.
- **≤8 distinct текстур/материалов** на батч; Sprite Atlas для статики.
- **Скрытые панели:** `DisplayStyle.None` + `UIDocument.enabled=false` (убивает PrepareRepaint).
- **Один shared PanelSettings** для нескольких UIDocument (официальная оптимизация Unity 6).

### 8.3 Архитектурные workaround'ы (из проекта)

- **Kill-switch repaint:** скрытие = `_doc.enabled=false` + перед этим мягкий фейд + `display:none + pickingMode Ignore`.
- **Shared panel:** один `[CloudUI] UIDocument` с внутренними слоями (Background/Hints/Toasts/Progress) вместо 20 PanelSettings.
- **Токены:** USS custom properties (`--*`) на `:root`; темы через stylesheet swap, не runtime var mutation.
- **Pooling:** reparent + disable (disable first), keyed by template.
- **Virtualization:** `ListView`/`TreeView` для 10k+; `fixedItemHeight` где возможно.

---

## 9. Архитектура «живого» UI (living UI)

### 9.1 Принципы

1. **Адаптивность:** responsive layouts (Rive) + Flexbox (UITK) + `.compact`/`.mobile` класс на root для разных экранов/DPI.
2. **Отзывчивость:** USS transitions (hover/fade 120–150ms) + свой микро-твинер (fade/rise/scale/stagger/numbers-tick) — 0 GC при пулинге.
3. **Анимированность:** state machines (Rive) для сложных состояний; USS transitions для простых; DOTween **не тащить** (GC + зависимость).
4. **Размытость:** backdrop-filter (6.6+) или эмуляция; bloom (URP Volume) глобально.
5. **Свечение:** emissive + bloom + vector feathering (Rive) / Shader Graph.
6. **Частицы:** VFX Graph → RenderTexture (точечно, для «вау»-моментов).
7. **Аудио:** звуковая сетка (open/close/hover/select/error) через существующий `UIManager`.

### 9.2 Стек слоёв (рекомендуемый для Project C)

```
[Layer 0] World (3D rendering + VFX Graph в мире)
[Layer 1] UITK Chrome (HUD, Modal, Tooltip, Overlay) — Screen Space Overlay
   ├── USS tokens + ThemeApplier (dark/light/seasonal)
   ├── Custom shaders (Shader Graph) для точечных эффектов
   ├── Bloom (URP Volume) глобально для emissive
   └── Backdrop-filter (6.6+) или эмуляция glassmorphism
[Layer 2] Rive (векторные анимированные элементы)
   ├── Встраивается в uGUI Canvas или mesh
   ├── State machines + view models (data binding)
   └── Vector feathering для глоу/мягких краёв
[Layer 3] VFX Graph (точечно, через RenderTexture → UI quad)
   └── Только для "вау"-моментов (лут, апгрейд, победа)
[Layer 4] World-space UI (uGUI + TMP + DOTween)
   └── Nameplates, prompts, floating text, minimap blips
[Layer 5] Audio (UIManager sound grid)
```

### 9.3 Data flow (живой UI)

```
Game Systems → Events (*ClientState.OnSnapshotUpdated)
    ↓
ViewModels (thin adapters, INotifyBindablePropertyChanged)
    ↓
UITK bindings (leaf-level) + direct writes (high-freq, throttled)
    ↓
VisualElements + Rive view models (set values from C#)
    ↓
State machines react → animations play
    ↓
Audio (UIManager.PlaySound) on transitions
```

### 9.4 Паттерны «живости»

| Паттерн | Реализация |
|---|---|
| **Fade+rise при появлении** | Твинер: opacity 0→1 + translate 8–12px за 220ms easeOutCubic |
| **Stagger списков** | 25ms задержка между элементами |
| **Тикающие числа** | 300–500ms анимация значения + dirty-check (EqualsApprox) |
| **Hover-отклик** | USS transition 120ms + звук (только верхний уровень) |
| **Пульсация при изменении** | Scale 1→1.05→1 за 200ms |
| **Dim при модалке** | `--modal-dim` overlay + input block |
| **Living background** | Мир рендерится за полупрозрачным UI (L1), вуаль 0.15–0.25 при модалке |
| **Theme switching** | ThemeData asset → USS sheet swap → var() cascade repaint |

### 9.5 Без тяжёлых плагинов: минимальный набор

| Компонент | Источник | Лицензия | Обязателен? |
|---|---|---|---|
| UI Toolkit | Unity (встроено) | Бесплатно | Да |
| URP | Unity (встроено) | Бесплатно | Да |
| TextMeshPro | Unity (встроено) | Бесплатно | Да |
| Shader Graph | Unity (встроено) | Бесплатно | Да (для AAA-эффектов) |
| VFX Graph | Unity (встроено) | Бесплатно | Опционально (точечно) |
| Rive runtime | Asset Store / GitHub | MIT (splash на free) | Рекомендуется (motion layer) |
| Свой микро-твинер | ~300 LOC | Свой код | Да (вместо DOTween) |
| Audio (UIManager) | Существующий | Свой код | Да |

**Не используем:** NoasisGUI, Coherent Gameface, WebView, Dear ImGui (player UI), Unity Vector Graphics (экспериментальный), ThorVG (нет интеграции), DOTween (GC + зависимость).

---

## 10. Рекомендации (приоритеты)

### P0 (сейчас, без новых зависимостей)

1. **Эмуляция glassmorphism** без backdrop-filter: светлые полупрозрачные карточки (0.72–0.85), hairline-рамки, верхний блик. (Из cloud-light-ресерча.)
2. **Bloom (URP Volume)** глобально для emissive UI-элементов — один pass, дёшево.
3. **Shader Graph для точечных эффектов:** кольцевой прогресс, гало аватара, свечение HP-бара. Применять к конкретным элементам через UI Material.
4. **SDF-шейдеры** (TLabAltoh/Unity-SDF-UI-Toolkit) для скруглённых масок без артефактов.
5. **Свой микро-твинер** (~300 LOC) для fade/rise/scale/stagger/numbers-tick.
6. **Звуковая сетка** (open/close/hover/select/error) через существующий `UIManager`.

### P1 (ближайшие итерации)

7. **Rive как motion layer:** анимированные иконки, переходы, data-bound микро-интеракции. Начать с одного экрана (loading/main menu) для оценки.
8. **VFX Graph → RenderTexture** для «вау»-моментов (лут, апгрейд) — точечно.
9. **Обновление Unity до 6.6 LTS** для backdrop-filter (настоящий blur за элементами) — когда стабильно.

### P2 (по необходимости)

10. **NoasisGUI trial** — только если UITK профилирование покажет непреодолимый потолок + есть бюджет/экспертиза.
11. **WebView** — только для web-контента (порталы, OAuth).
12. **World-space UI миграция на UITK** (6.2+) — decision gate после оценки.

### P3 (не делать)

- Coherent Gameface для core UI (избыточен).
- Dear ImGui для player UI (dev-only).
- Unity Vector Graphics для продакшена (экспериментальный).
- ThorVG обёртка (высокая цена).
- DOTween (GC + зависимость).
- VFX Graph на каждый элемент (render texture на каждый = катастрофа).

---

## 11. Источники и ссылки

### Rive
- Rive for game UI: https://rive.app/game-ui
- Rive runtime updates (Oct 2026, global view models): https://community.rive.app/c/announcements/rive-runtime-updates-october-1-2026
- Rive animation guide 2026: https://hedrick.io/post/rive-animation-guide-2026
- Rive data binding docs: https://rive.app/docs/game-runtimes/unity/data-binding
- Rive state machines docs: https://rive.app/docs/game-runtimes/unity/state-machines
- Rive pricing: https://rive.app/pricing
- Rive Unity blog: https://rive.app/blog/rive-for-unity-updated-and-in-the-unity-asset-store
- Pay 2 Win case study: https://rive.app/blog/building-pay-2-win-with-rive-and-unity

### NoasisGUI
- Technology & features: https://www.noesisengine.com/noesisgui
- Unity tutorial: https://www.noesisengine.com/docs/Gui.Core.Unity3DTutorial.html
- Licensing/pricing: https://www.noesisengine.com/licensing.php
- Licensing docs: https://www.noesisengine.com/docs/Gui.Core.Licensing.html
- Noasis Studio: https://apps.microsoft.com/detail/9p878k7f3h1c
- Forums (license confusion): https://www.noesisengine.com/forums/viewtopic.php?t=268
- Licensing changes: https://www.noesisengine.com/forums/viewtopic.php?t=3260

### Coherent Gameface
- Product: https://coherent-labs.com/products/coherent-gameface/
- Internal caches (perf): https://docs.coherent-labs.com/unity-gameface/performance_optimization/internalcaches/
- Public roadmap: https://coherentlabs.zendesk.com/hc/en-us/articles/7957306751261-Public-roadmap
- Unity Discussions: https://discussions.unity.com/t/coherent-ui-html5-based-user-interface-middleware/489158

### WebView
- Vuplex support (performance): https://support.vuplex.com/articles/how-to-improve-performance
- Vuplex memory: https://support.vuplex.com/articles/how-to-reduce-memory
- Vuplex native 2D mode: https://support.vuplex.com/articles/native-2d-mode
- gree/unity-webview: https://github.com/gree/unity-webview

### Custom shaders / UI Toolkit
- UI Shader Graph intro (Unity 6.6): https://docs.unity.com/en-us/engine/6000.7/manual/uitoolkits/uielements/uie-uss/ui-shader-graph/introduction-to
- Get started with UI Shader Graph (6.6): https://docs.unity.com/en-us/engine/6000.3/Documentation/Manual/ui-systems/get-started-with-ui-shader-graph.html
- Backdrop filter (6.6): https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uie-uss/uss-filter/backdrop-filter
- Built-in filters (6.6): https://docs.unity.com/6000.6/Documentation/Manual/ui-systems/built-in-filters.html
- Custom shaders for UI Toolkit (6.3) tutorial: https://www.youtube.com/watch?v=xAOBBW9hsjA
- SDF UI Toolkit: https://github.com/TLabAltoh/Unity-SDF-UI-Toolkit
- UGUI Shader Graph samples: https://docs.unity3d.com/Packages/com.unity.ugui@2.7/manual/HOWTO-ShaderGraph.html
- ThorVG: https://www.thorvg.org
- Unity Vector Graphics docs: https://docs.unity3d.com/Packages/com.unity.vectorgraphics@2.0/manual/index.html

### VFX Graph / UI particles
- Render particles over UI canvas: https://medium.com/university-of-games/how-to-render-particle-system-over-ui-canvas-089780cfd0a7
- Unity UI optimization: https://unity.com/how-to/unity-ui-optimization-tips

### Unity UI Toolkit (limitations/perf)
- Optimizing performance (UI Toolkit): https://docs.unity3d.com/6000.6/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html
- Unite 2024 performance talk: https://www.youtube.com/watch?v=bECmaYIvZJg
- UI Toolkit roadmap (Nov 2025): https://discussions.unity.com/t/ui-toolkit-development-status-and-next-milestones-november-2025/1698009
- State of UI Toolkit (6.7): https://discussions.unity.com/t/state-of-ui-toolkit-in-unity-6-7/1736756

### Проектные документы (внутренние)
- `docs/UI/Impruvements_research/00_README.md` — навигация и принципы.
- `docs/UI/Impruvements_research/02_SOLUTIONS_RESEARCH_2024-2026.md` — сравнение решений.
- `docs/UI/Impruvements_research/03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` — AAA-архитектура.
- `docs/UI/Impruvements_research/06_PERFORMANCE_GUARDRAILS.md` — бюджеты.
- `docs/UI/cloud-light-interactive-research-2026-09-29.md` — cloud-эстетика, токены, motion.
- `docs/UI/UI_TOOLKIT_GUIDE.md` — канон по 5 фатальным ошибкам UITK.

---

*Примечание: цены/лицензии проверены по публичным источникам на 2026-11-02; перед коммитом проверять у вендоров. Версии Unity/URP — по `ProjectSettings/ProjectVersion.txt` и `Packages/manifest.json` проекта.*
