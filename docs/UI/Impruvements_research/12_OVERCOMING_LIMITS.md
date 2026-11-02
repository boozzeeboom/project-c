# Overcoming Unity UI Toolkit Limitations for AAA-Quality Visuals

**Project:** Project C: The Clouds — MMO-песочница, Unity 6000.5.2f1, URP 17.5.0, Windows standalone.
**Авторство:** research-отчёт (subagent), 2026-11-03.
**Назначение:** как выйти за рамки «Windows 98» в UI Toolkit — без тяжёлых плагинов, со своим крутым решением. Дополняет `02_SOLUTIONS_RESEARCH_2024-2026.md`, `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md`, `08_RENDERING_APPROACHES.md`, `09_DESIGN_PATTERNS.md`.
**Статус:** research (pre-implementation). Все факты — из публичных источников; лицензии/цены проверять у вендоров перед коммитом.

---

## 0. Executive summary

Unity UI Toolkit (UITK) — лучший базовый фреймворк для MMO (retained-mode, Flexbox, USS-токены, виртуализация, Figma round-trip), но **сам по себе он не даёт AAA-визуала**: нет `box-shadow` с blur, нет `backdrop-filter` (до 6.6), нет conic-gradient, нет частиц, нет state machines. «Windows 98»-эффект получается именно из-за этих пробелов.

**Стратегия преодоления (без тяжёлых плагинов):**

1. **UITK остаётся основой** layout/темизации/списков/форм — это «скелет».
2. **Shader Graph (Unity 6.3+)** — главный «бесплатный» инструмент AAA-эффектов: glow, blur, bloom, SDF-текст, скруглённые углы, circular progress, halos. Применяется как UI Material к конкретным элементам.
3. **Rive** — motion/векторный слой поверх UITK: state machines, data binding (view models), vector feathering (мягкие края/глоу **без** стоимости blur), 120 fps, виртуализированные списки, текстовый ввод, gamepad. Runtime MIT, бесплатно для шиппинга (splash на free-плане).
4. **VFX Graph → RenderTexture → UI quad** — точечно, только для «вау»-моментов (лут, апгрейд, победа). Не на каждый элемент.
5. **Bloom (URP Volume)** — глобально для всех emissive UI-элементов: один pass, дёшево.
6. **Эмуляция** отсутствующих CSS-фич (box-shadow, backdrop-filter, gap, transitions) — через child-ореолы, полупрозрачные поверхности + hairline + блик, USS transitions, стек слоёв.
7. **NoasisGUI / Coherent Gameface / WebView** — **не для core UI**. Рассматривать только при непреодолимом потолке UITK + наличии бюджета/экспертизы. WebView — только для реального web-контента (порталы, OAuth).

**Минимальный набор зависимостей (все бесплатные, встроены в Unity):** UI Toolkit + URP + TMP + Shader Graph + VFX Graph (опц.) + Rive runtime (MIT, splash) + свой микро-твинер (~300 LOC). **Не используем:** NoasisGUI, Coherent Gameface, WebView, Dear ImGui (player UI), Unity Vector Graphics (экспериментальный), ThorVG (нет интеграции), DOTween (GC + зависимость).

---

## 1. Сравнение подходов (rendering layer)

### 1.1 Таблица сравнения

| Подход | Визуал (глоу/блюр/частицы) | Производительность | Авторинг | Figma/дизайн | Лицензия | Риск |
|---|---|---|---|---|---|---|
| **UI Toolkit + URP** | USS filter/backdrop-filter (6.6+), custom shaders (6.3+), SDF-текст через TextCore | Высокая (3 dc / 2 ms @1k) | Web-like (UXML/USS), UI Builder | Официальный экспериментальный коннектор + 3rd party | Бесплатно (Unity) | Средний (runtime rough, API churn) |
| **Rive (векторный GPU-рендер)** | Vector feathering (мягкие края/глоу без blur), аналитический AA, raster | Очень высокая (тысячи векторов @120 fps заявлено) | Rive Editor + CLI, state machines, view models | SVG/PSD/шрифты/аудио round-trip | Runtime MIT, free; splash на free-плане | Низко-средний |
| **NoasisGUI (XAML/C++)** | XAML-стили, sub-ms рендер, Noasis Studio | Очень высокая (sub-ms) | XAML + Blend/Noasis Studio в Unity | Нет нативного Figma-импорта | Платно: €195 indie / €9k+ pro | Средний (vendor) |
| **Coherent Gameface (HTML/CSS/JS)** | CSS (blur, glow через web-стек) | Средняя (browser-class память) | Web-навыки, live debugger | Нет нативного | Платные тиры | Средний |
| **WebView (Vuplex и др.)** | Полный web-стек | Низкая (texture capture, 3-й кадр) | Web-разработка | N/A | Платные ассеты | Высокий (z-order, input) |
| **Custom shaders (Shader Graph)** | Полный контроль: глоу, блюр, bloom, SDF, скругления | Зависит от шейдера; batching ломается при смене материала | Shader Graph (визуально) | Ручной перенос | Бесплатно | Низкий (но цена авторинга) |
| **VFX Graph** | Частицы, гало, искры, дым | Высокая в мире; в UI — через RenderTexture | VFX Graph (визуально) | Ручной | Бесплатно (Unity) | Средний (не нативно для overlay) |

### 1.2 Ключевые различия по задачам AAA-визуала

| Задача AAA-визуала | Лучший инструмент | Заметки |
|---|---|---|
| Глоу вокруг кнопки/элемента | Shader Graph (emissive + bloom) или Rive vector feathering | USS `filter: drop-shadow` в UITK **не поддерживается** (дока USS filter). Только через шейдер/материал или Rive. |
| Размытие фона за панелью (glassmorphism) | `backdrop-filter: blur()` — **только UITK, только URP, только с 6.6** | На 6000.5.2f1 недоступно. Обход: полупрозрачные поверхности + hairline + блик. Альтернатива: Shader Graph с grab-pass (дорого, ломает batching). |
| Скруглённые углы | USS `border-radius` (UITK/uGUI) — из коробки | Для SDF-масок/скруглений без артефактов — SDF-шейдеры (TLabAltoh/Unity-SDF-UI-Toolkit и аналоги). |
| SDF-текст (crisp at any scale) | TextMeshPro SDF (uGUI) / UITK text (TextCore) | UITK использует TextCore (TMP-совместимый). Один атлас на вес/стиль. |
| Частицы/гало в UI | VFX Graph → RenderTexture → UI quad; либо uGUI world-space canvas с Particle System | Прямой overlay VFX Graph в UITK не поддерживается. |
| Анимированные иконки/переходы | Rive state machines + view models | Лучшая связка «дизайнер анимирует, разработчик драйвит данными». |
| Циркулярный прогресс | Rive (vector) или USS `conic-gradient` (не в UITK) → шейдер/SDF | UITK не поддерживает conic-gradient. Делать через SDF-шейдер или Rive. |
| Реактивные списки 10k+ | UITK ListView + Rive virtualized scrolling list | Rive имеет встроенный virtualized scrolling (только видимые строки). |

---

## 2. Rive runtime for Unity — полные возможности

### 2.1 Что это

- **GPU-векторный/растровый рендерер**, `.riv` — один файл (арт + моушн + логика). Runtime MIT, без роялти/платы за шиппинг.
- Unity: работает с Unity 2021 LTS+, включая Unity 6. Устанавливается из Asset Store или GitHub.
- Рендерится в **uGUI Canvas** или на **mesh** (3D-поверхности). В UITK — через render-to-texture или встраивание как компонент.
- **Производительность:** аналитический AA, vector feathering (мягкие края/глоу **без** стоимости blur). Заявлено: тысячи векторов @120 fps, файлы на 90% меньше альтернатив.

### 2.2 State machines

- Набор анимационных состояний и переходов между ними. Управляются **косвенно** — через data binding-свойства (не прямым вызовом состояния).
- Дизайнер назначает default state machine для каждого артборда в Rive Editor; может создать несколько (разные конфигурации).
- Runtime: state machine «advances» один раз в кадр на дельту времени. **Оптимизация:** runtime детектит, когда дальнейших изменений не будет (нет активных переходов/анимаций) и **останавливает advance** (settled). Разбужается внешними действиями (ввод, изменение data binding). Можно принудительно `play`.
- **Gamepad listeners и Focus** встроены в Rive Editor (навигация по меню контроллером/клавиатурой). В Unity/Unreal поддержка gamepad — в roadmap (проверять feature support).

### 2.3 Data binding (view models)

- **View models** описывают набор свойств, но сами по себе не хранят значения — это делают **view model instances**.
- Свойства: числа, строки, булевы, **списки** (для инвентарей/повторяющихся элементов), цвета, ассеты (шрифты, блобы).
- Игра устанавливает значения из кода → state machine реагирует → игра может слушать изменения, сделанные в файле.
- **Global view models** (окт 2026): авторские default-инстансы биндятся автоматически; views перерисовываются при изменении.
- **Luau/AnimaScript** — скриптинг внутри файла (логика, процедурный моушн, кастомный рисунок).

### 2.4 Возможности для AAA UI (применение в Project C)

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

### 2.5 Конкретно: halos, glows, particles, circular progress через Rive

- **Halos / glows:** Rive поддерживает blur-эффекты на слоях и vector feathering — мягкие края/свечение без grab-pass blur. Идеально для гало вокруг аватара/слота/уведомления. Состояния (idle/hover/selected/disabled) — в state machine, драйвятся data binding.
- **Particles:** Rive поддерживает векторные частицы (ограниченно). Для «вау»-эффектов (level-up, rare drop) — всё равно VFX Graph → RenderTexture. Rive подходит для мелких микро-частиц/искр в иконках.
- **Circular progress:** state machine с data-bound `progress` property; кольцо с glow/particles — векторное, 60 fps, малый размер файла. Лучше, чем SDF-шейдер, если нужна анимация (pulse при 100%, сегменты, celebration burst).
- **Loading screens:** state machine idle/loading/complete; data-bound progress; полноэкранный арт + совет + тонкий прогресс (Destiny/Genshin стиль).

### 2.6 Ограничения/риски

- **Не заменяет layout-систему.** Rive — motion/векторный слой, не оконный менеджер. Формы/таблицы/чат всё равно нужны в UITK/uGUI.
- **Gamepad support в Unity/Unreal** — в roadmap на момент исследования; проверять feature support перед ставкой.
- **Free-план:** экспортированные файлы показывают Rive splash; без splash — платный план (Cadet $9/seat/мес и выше).
- **Авторинг:** дизайнерам нужно освоить Rive Editor (отдельный инструмент, не Figma). Figma→Rive через SVG/PSD round-trip.
- **Встраивание в UITK:** Rive рендерится в uGUI Canvas или mesh. В чистом UITK-дереве — через render-to-texture или компонент. Это добавляет один render pass.

### 2.7 Когда использовать в Project C

- Анимированные иконки/бейджи/индикаторы (HP-кольцо, кулдаун, статус).
- Переходы/вайпы экранов (loading, смена сцены).
- Data-bound микро-интеракции (hover-эффекты, нажатия, state-переключения).
- Сложные векторные элементы: кольцевой прогресс, гало, свечение вокруг аватара.

**Не использовать:** для текстовых таблиц, чата, списков форм, настроек (это UITK/uGUI).

---

## 3. NoasisGUI — XAML, sub-ms, Noasis Studio, лицензирование

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
| **Indie** | < €100K годовой выручки, < €250K бюджет проекта | **€195** per project, все платформы |
| **Pro** | < €1.5M бюджет проекта | €9000 первая платформа + €3600 доп. |
| **Premium** | €1.5M–€12M бюджет | €18000 первая + €7200 доп. |
| **Enterprise** | > €12M | по запросу |

- Royalty-free, perpetual license. Indie включает minor releases lifetime + 1 год major upgrades.
- Pro/Premium/Enterprise: priority/premium support обязателен первый год (€3200–€6300/год).
- **Бесплатной опции больше нет** (раньше была free tier; убрали).
- Noasis Studio бесплатен для скачивания/использования как standalone editor.

### 3.4 Плюсы/минусы для Project C

**Плюсы:** AAA-производительность, XAML-инструментарий (Blend/Studio), data binding, hot reload, все платформы.

**Минусы:**
- **Стоимость:** для MMO-проекта с бюджетом > €250K — минимум €9000+support, что для indie/solo-разработчика неприемлемо.
- **Vendor lock-in:** XAML-экспертиза, собственный рендерер, нет нативного Figma-импорта (дизайн-пайплайн = XAML/Studio).
- **Риск:** зависимость от вендора, roadmap под их контроль.

### 3.5 Вердикт

**Не для core UI в Project C** (бюджет не оправдывает, команда Unity-native). Рассматривать только если: (а) профилирование UITK покажет непреодолимый потолок, (б) есть XAML-экспертиза, (в) бюджет позволяет Pro-тира. Триал перед решением обязателен.

---

## 4. Coherent Gameface — HTML/CSS/JS, performance, memory

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

## 5. WebView-based UI (Vuplex, UniWebView, Embedded Browser, gree/unity-webview)

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

## 6. Custom shaders for UI (Shader Graph)

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
| **Frosted glass** | GrabPass / `_CameraOpaqueTexture` (URP) + kernel blur (5×5) + tint + noise | Средняя-высокая (один draw call + 25 samples/px) |
| **Dissolve/transition wipes** | Noise + threshold (Shader Graph) | Низкая |
| **Damage trail (HP bar)** | Два значения `_Value` и `_TrailValue` (лагает) | Низкая |

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

### 6.6 Пример: frosted glass shader (концепт, URP Shader Graph)

```
// Fragment:
float2 uv = IN.uv;
float4 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
// blur kernel 5x5
float4 blurred = 0;
for (int x = -2; x <= 2; x++)
  for (int y = -2; y <= 2; y++)
    blurred += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler, uv + float2(x,y)*_BlurSize);
blurred /= 25;
float4 tinted = lerp(blurred, _TintColor, _TintStrength);
float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler, uv*_NoiseScale + _Time.y*_NoiseSpeed).r;
tinted.rgb += noise * _NoiseStrength;
// edge highlight
float edge = smoothstep(0.98, 1.0, max(abs(uv.x-0.5), abs(uv.y-0.5))*2);
tinted.rgb += edge * _EdgeColor.rgb;
return tinted;
```

**Бюджет:** blur kernel 5×5 = 25 samples/px; на fullscreen panel — дорого. Ограничить: blur только для modal backgrounds (один раз), kernel ≤ 5×5, разрешение blur ≤ half-res. Включить «Opaque Texture» в URP asset.

### 6.7 Пример: circular progress shader (концепт, URP Shader Graph)

```
// Vertex: quad
// Fragment:
float2 uv = IN.uv - 0.5;
float angle = atan2(uv.y, uv.x); // -pi..pi
float radius = length(uv);
float progressAngle = _Progress * 2*PI - PI; // от -pi
float mask = step(angle, progressAngle) * step(radius, _Outer) * step(_Inner, radius);
float edge = smoothstep(_EdgeWidth, 0, abs(angle - progressAngle));
float glow = edge * _GlowIntensity;
return _Color * mask + _GlowColor * glow;
```

Добавить: glow по краю (additive), pulse при достижении 100%, сегменты (шаг дискретизации), антиалиасинг по радиусу.

---

## 7. VFX Graph for UI particles/halos/glows (via RenderTexture)

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
| Celebration bursts (level-up, rare drop) | VFX Graph → RenderTexture, thousands of particles, 60 fps | Средняя-высокая |
| Transition wipes (iris, pixelate) | Fullscreen quad с VFX | Средняя |
| Ambient backgrounds (меню) | Пыль, искры, энергия — жёсткий бюджет ≤ 50–100 частиц | Низкая |

### 7.3 Рекомендация

- **VFX Graph — точечно**, только для «вау»-моментов (лут, апгрейд, победа), не для постоянного UI.
- **Гало/свечение — через шейдеры** (дешевле, батчатся лучше).
- **Не делать VFX Graph на каждый элемент** — render texture на каждый элемент = катастрофа по памяти/draw calls.
- Частицы — **event-driven**, не постоянные (кроме ambient с жёстким бюджетом ≤ 50 частиц).
- Object pooling для VisualEffect.
- Отключать при `motion-reduction` setting.
- Не рендерить поверх критичного текста.

### 7.4 Интеграция (паттерн)

```
1. Создать RenderTexture (fixed size, напр. 512×512).
2. Создать dedicated Camera (orthographic, culling mask = VFX layer).
3. На объекте с VisualEffect — layer = VFX.
4. Camera.targetTexture = RenderTexture.
5. В UI: VisualElement.style.backgroundImage = RenderTexture (или RawImage в uGUI).
6. Управлять play/stop через C# (event-driven).
```

---

## 8. Unity UI Toolkit: limitations & workarounds для AAA-визуала

### 8.1 Известные ограничения (2025–2026) и обходы

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
| Нет `box-shadow` с blur | Child-ореолы (halo) с градиентами; либо SDF-шейдер. |
| Нет `gap` (в ранних версиях) / ограниченная поддержка | Использовать margin на детях; либо flex с spacing через padding. |
| Нет CSS `transition` на всех свойствах | USS `transition` только на opacity/translate/rotate/scale; для остального — свой микро-твинер. |
| Нет conic-gradient | Circular progress через SDF-шейдер или Rive. |
| Нет particle system | VFX Graph → RenderTexture → UI quad; либо uGUI. |
| Нет state machines | Rive (motion-слой) или свой FSM в C#. |

### 8.2 Конкретные workaround'ы (из проекта и research)

#### 8.2.1 Glassmorphism без backdrop-filter (на 6000.5)

На нашей версии (6000.5.2f1) настоящего blur за элементом нет. Эмуляция (из cloud-light-ресерча):

- **Полупрозрачные поверхности:** светлые карточки (alpha 0.72–0.85), не полностью прозрачные.
- **Hairline-рамки:** 1 px `rgba(255,255,255,0.18–0.30)` (верхняя/левая ярче — имитация света).
- **Верхний блик:** subtle gradient top-down.
- **Тень:** мягкая, большая (ambient occlusion) — через child-ореолы.
- **Noise/grain:** лёгкая текстура для глубины.
- **Обновиться до 6.6** для настоящего `backdrop-filter: blur()` (один pass, дёшево).

#### 8.2.2 Halos/glows без box-shadow

USS не поддерживает `box-shadow` с blur. Паттерн из проекта (Cloudy, `06_HALOS_AND_FLEX.md`):

```xml
<!-- VisualElement с child-ореолом -->
<ui:VisualElement class="hm-slot">
  <ui:VisualElement class="halo-hmslot" />  <!-- glow, opacity 0→1 при .hl -->
</ui:VisualElement>
```

```css
.halo-hmslot {
  position: absolute;
  top: -12px; bottom: -12px; left: -12px; right: -12px;
  background-color: rgba(120,184,145,0); /* ramp через opacity */
  border-radius: 50%;
  transition: opacity 150ms ease-out;
  pointer-events: none;
}
.hm-slot.hl > .halo-hmslot { opacity: 1; background-color: rgba(120,184,145,0.55); }
```

**Состояния halo:** idle (низкая alpha / скрыт), hover (fade-in 0.15 s), selected/active (полная alpha + pulse), disabled (серый/отсутствует). Переходы — opacity, не layout.

**Бюджет:** halo = дополнительный элемент + (опционально) shader; держать ≤ 1 halo per focusable element; не стакать без причины.

Альтернативы: Shader Graph (radial gradient + additive), Rive (blur effects на слоях), uGUI (Image с sprite градиента + CanvasGroup.alpha).

#### 8.2.3 Transitions без CSS transition на всех свойствах

USS `transition` поддерживает только opacity/translate/rotate/scale. Для остального — свой микро-твинер (~300 LOC) с пулом твинов, 0 GC. Паттерны (из `09_DESIGN_PATTERNS.md`):

| Взаимодействие | Эффект | Длительность | Easing |
|---|---|---|---|
| Hover (кнопка) | scale 1.0→1.03, glow fade-in, цвет shift | 100 ms | ease-out |
| Press | scale 1.0→0.97, цвет darken, ripple | 80 ms | ease-in |
| Release | scale back, glow | 120 ms | ease-out |
| Focus (keyboard) | focus ring fade-in + scale | 120 ms | ease-out |
| Modal in | fade + slide/scale-pop + dim bg | 160 ms | ease-out (cubic) |
| Modal out | fade + scale-down | 100 ms | ease-in |
| Tooltip in | fade + slight slide (8 px) | 80 ms | ease-out |
| Item pickup | scale-pop + glow burst + particles | 200 ms | ease-out (back) |
| Damage | shake (2–4 px, 100 ms) + flash | 100 ms | ease-out |
| Level-up | scale 1→1.2→1 + particles + glow | 400 ms | ease-out (back) |

**Правило:** не смешивать DOTween и USS transitions на одном элементе — конфликты. Выбрать один слой per element. DOTween не тащить (GC + зависимость) — свой твинер.

#### 8.2.4 Circular progress без conic-gradient

USS не поддерживает conic-gradient. Варианты:
1. **SDF-шейдер** (Shader Graph) — радиальный прогресс через polar coordinates (см. §6.7). Дёшево, настраиваемо.
2. **Rive** — state machine с data-bound progress; векторный, с glow/particles. Лучше для анимированного кольца.
3. **uGUI Image (Filled, Radial 360)** — самый простой путь для простых индикаторов; `fillAmount` 0–1.
4. **UITK custom VisualElement с `generateVisualContent`** — ручной меш-веер (MeshGenerationContext). Дёшево, без текстуры, но больше кода.

**Анимация прогресса:** не линейно — ease-out (cubic) при заполнении; при «потере» прогресса — быстрый drop (ease-in). Для XP/level-up — celebration burst (particles + scale-pop).

#### 8.2.5 Gradient borders без border-image

Два вложенных слоя: внешний hairline (1 px, alpha) + внутренний блик (gradient). Или SDF-шейдер с двумя цветами по краю.

#### 8.2.6 Status indicators (HP/energy/cooldown/buff)

- **Горизонтальный бар:** цвет по порогам (зелёный→жёлтый→красный); «damage flash» (красная вспышка при уроне, decay 0.3 s); «heal flash» (зелёный).
- **Cooldown radial:** вокруг иконки способности; grayscale + overlay при кулдауне; восстановление — яркость возвращается.
- **Buff/debuff иконки:** стаки числом; таймер кольцом; цвет по типу (buff=зелёный, debuff=красный).
- **Resource pips:** дискретные (мана-шары); анимация consume (scale down + fade) и refill (scale up + glow).

**Реализация:**
- uGUI: `Image.fillAmount` + цвет через `Color.Lerp`; damage flash через твинер `DOColor`.
- UI Toolkit: inline style `width` анимировать нельзя (layout churn) → использовать `transform: scale` или шейдер; для баров — шейдер с `_Value` property.
- Шейдер: `_Value` (0–1), цвет по градиенту, glow на leading edge, damage trail (отдельный `_TrailValue`, лагает за `_Value`).

#### 8.2.7 Loading animations / skeleton screens

**Паттерны AAA:**
- **Spinner (ring):** вращающееся кольцо с градиентом alpha; 1–2 s/rev; ease linear.
- **Progress ring + percent:** круговой прогресс + текст; для длинных загрузок.
- **Skeleton screens (Genshin, Cyberpunk):** плейсхолдеры с shimmer (градиент, бегущий слева направо) вместо пустых экранов/спиннеров — снижает perceived wait.
- **Lore/арт-заставки (Destiny, Genshin):** полноэкранный арт + цитата/совет + тонкий прогресс-бар; превращает ожидание в контент.
- **Transition wipes:** круговое раскрытие (iris wipe), pixelate, glitch, slide — для смены сцен/экранов.
- **Rive:** state machine с idle/loading/complete состояниями; data-bound progress; векторная анимация.

**Реализация:**
- **Rive (рекомендуется):** `.riv` с state machine; C# задаёт progress (0–1) и state (loading/complete); анимация — векторная, 60 fps, малый размер.
- **VFX Graph:** для particle-based loading (искорки, пыль, энергия) — отдельный fullscreen quad с VFX.
- **UI Toolkit:** shimmer через USS `background` с градиентом + анимация `background-position` (если поддерживается) или через shader; проще — shader-материал на VisualElement.
- **Твинер:** для простых spinners — rotation.

**Правило производительности:** loading screen — единственное место, где можно позволить heavy-эффекты (particles, blur), т.к. 3D-рендер отключён. Но не злоупотреблять — время загрузки всё равно должно быть минимальным.

### 8.3 Производительность (guardrails)

- **≤4 draw calls для chrome** (один на слой: HUD, Modal, Tooltip, Overlay).
- **Vertex Budget** в PanelSettings подогнать под весь UI (20k–50k вершин) — один буфер.
- **Usage hints:** `DynamicTransform` для анимируемых, `GroupTransform` для контейнеров с анимированными детьми.
- **Анимировать transforms** (`translate/scale/rotate`), никогда layout-props (`width/height/top/left`).
- **Не переключать USS-классы** на больших иерархиях во время анимации — inline style.
- **Rectangular masks** (shader-based, без stencil) по умолчанию; complex ≤7 уровней.
- **≤8 distinct текстур/материалов** на батч; Sprite Atlas для статики.
- **Скрытые панели:** `DisplayStyle.None` + `UIDocument.enabled=false` (убивает PrepareRepaint).
- **Один shared PanelSettings** для нескольких UIDocument (официальная оптимизация Unity 6).

### 8.4 Архитектурные workaround'ы (из проекта)

- **Kill-switch repaint:** скрытие = `_doc.enabled=false` + перед этим мягкий фейд + `display:none + pickingMode Ignore`.
- **Shared panel:** один `[CloudUI] UIDocument` с внутренними слоями (Background/Hints/Toasts/Progress) вместо 20 PanelSettings.
- **Токены:** USS custom properties (`--*`) на `:root`; темы через stylesheet swap, не runtime var mutation.
- **Pooling:** reparent + disable (disable first), keyed by template.
- **Virtualization:** `ListView`/`TreeView` для 10k+; `fixedItemHeight` где возможно.
- **Один shared PanelSettings** для нескольких UIDocument.

---

## 9. Как комбинировать для AAA-визуала без тяжёлых плагинов

### 9.1 Стек слоёв (рекомендуемый для Project C)

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
[Layer 4] World-space UI (uGUI + TMP + твинер)
   └── Nameplates, prompts, floating text, minimap blips
[Layer 5] Audio (UIManager sound grid)
```

### 9.2 Data flow (живой UI)

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

### 9.3 Паттерны «живости» (living UI)

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
| **Progressive disclosure** | Плотный инфо-UI; детали по hover/focus (Destiny 2) |
| **Контекстно-зависимый HUD** | В бою — компактный; в exploration — расширенный; в диалоге — скрытый HUD + диалог |
| **Единая точка фокуса** | Новые элементы появляются рядом с текущим фокусом (Destiny) |

### 9.4 Минимальный набор зависимостей (без тяжёлых плагинов)

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

### 9.5 План действий (приоритеты)

**P0 (сейчас, без новых зависимостей):**
1. Эмуляция glassmorphism без backdrop-filter: светлые полупрозрачные карточки (0.72–0.85), hairline-рамки, верхний блик.
2. Bloom (URP Volume) глобально для emissive UI-элементов — один pass, дёшево.
3. Shader Graph для точечных эффектов: кольцевой прогресс, гало аватара, свечение HP-бара. Применять к конкретным элементам через UI Material.
4. SDF-шейдеры (TLabAltoh/Unity-SDF-UI-Toolkit) для скруглённых масок без артефактов.
5. Свой микро-твинер (~300 LOC) для fade/rise/scale/stagger/numbers-tick.
6. Звуковая сетка (open/close/hover/select/error) через существующий `UIManager`.
7. Halo/glow система в UITK через child-ореолы (по образцу Cloudy) — недорого, визуально сильно.
8. Status indicators (HP/energy/cooldown/buff) с damage flash и glow.

**P1 (ближайшие итерации):**
9. Rive как motion layer: анимированные иконки, переходы, data-bound микро-интеракции. Начать с одного экрана (loading/main menu) для оценки.
10. VFX Graph → RenderTexture для «вау»-моментов (лут, апгрейд) — точечно.
11. Обновление Unity до 6.6 LTS для backdrop-filter (настоящий blur за элементами) — когда стабильно.
12. Loading screens: полноэкранный арт + совет + progress (Destiny/Genshin стиль).
13. Circular progress bars через shader (Shader Graph) для loading/cooldown/radial.

**P2 (по необходимости):**
14. NoasisGUI trial — только если UITK профилирование покажет непреодолимый потолок + есть бюджет/экспертиза.
15. WebView — только для web-контента (порталы, OAuth).
16. World-space UI миграция на UITK (6.2+) — decision gate после оценки.
17. Glassmorphism для модальных окон (frosted glass shader).
18. VFX Graph для celebration bursts (level-up, rare drop).

**P3 (не делать):**
- Coherent Gameface для core UI (избыточен).
- Dear ImGui для player UI (dev-only).
- Unity Vector Graphics для продакшена (экспериментальный).
- ThorVG обёртка (высокая цена).
- DOTween (GC + зависимость).
- VFX Graph на каждый элемент (render texture на каждый = катастрофа).

---

## 10. Источники

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
- `docs/UI/Impruvements_research/08_RENDERING_APPROACHES.md` — рендеринг-подходы.
- `docs/UI/Impruvements_research/09_DESIGN_PATTERNS.md` — AAA-паттерны.
- `docs/UI/cloud-light-interactive-research-2026-09-29.md` — cloud-эстетика, токены, motion.
- `docs/UI/full rebuild plans/Cloudy_redesign/06_HALOS_AND_FLEX.md` — halo-паттерны проекта.
- `docs/UI/UI_TOOLKIT_GUIDE.md` — канон по 5 фатальным ошибкам UITK.

### AAA-игры / GDC
- Destiny 2: Bungie, "Building the user interface for Destiny" (GDC 2016); Medium "UI of Destiny 2: Pursuits page".
- Cyberpunk 2077: CDPR, "User Interface in Cyberpunk 2077: Challenges and Optimizations" (GDC Showcase 2023).
- Valorant: Riot, "Art Directing a Consistent and Scalable Interactive Brand Identity" (GDC 2017); "Lessons from Building UI/UX in 2XKO" (GDC).
- Genshin Impact: miHoYo (GDC 2021).
- Fortnite: Epic (GDC 2018) "Optimizing UE4 for Fortnite".
- Star Citizen: CIG (публичные наблюдения).
- Sybo Games (GDC 2019) "Building a Unified Cross-Project UI Framework".
- DICE LA (GDC 2018) "Art Direction for AAA UI".
- SimCity 2013 (EA GDC) "AAA Game UI in HTML and JavaScript".

### Web/общее
- Material Design 3 (motion, elevation).
- Apple HIG (motion, accessibility).
- Refactoring UI (Wathan/Schoger) — микро-интеракции, spacing, цвет.

---

*Примечание: цены/лицензии проверены по публичным источникам на 2026-11-03; перед коммитом проверять у вендоров. Версии Unity/URP — по `ProjectSettings/ProjectVersion.txt` и `Packages/manifest.json` проекта. Документ — research; реализация — через план в `04_IMPLEMENTATION_PLAN.md` и согласование с пользователем (Milestone 1 — инструментарий рядом, без поломок).*
