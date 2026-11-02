# AAA UI Design Patterns & Unity Implementation — Project C

**Контекст:** MMO-песочница, Unity 6, URP, Windows standalone. Документ — research-отчёт по визуальным/интерактивным паттернам AAA-уровня и их реализации в Unity (UI Toolkit + uGUI + Rive + Shader/VFX Graph). Дополняет `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` (архитектура) и `06_PERFORMANCE_GUARDRAILS.md` (бюджеты).

**Авторинг:** 2026-11-02. Статус: research (pre-implementation). Все утверждения — из публичных источников (доки, GDC-токи, статьи, исходники). Лицензии/цены проверять у вендоров перед коммитом.

---

## 0. Executive summary

AAA-UI сегодня — это **живая, контекстно-адаптивная система**, а не набор статичных экранов. Ключевые нити, общие для Destiny 2, Fortnite, Valorant, Genshin, Cyberpunk 2077, Star Citizen:

1. **Living UI / Adaptive UI** — UI реагирует на состояние игрока, мира и устройства в реальном времени: state machines экранов, контекстно-зависимые HUD-элементы, progressive disclosure (показывать больше только по запросу), «глаза остаются в одном месте» (Destiny).
2. **Визуальная глубина через эффекты** — halos, glows, particles, blur, glassmorphism/neumorphism — используются как **функциональные сигналы** (состояние, фокус, угроза, редкость), а не декорация. Каждый эффект имеет бюджет и триггер.
3. **Микро-интеракции** — hover/press/transition 90–160 ms с параметрическими твинами; input блокируется на время перехода; анимируются только transforms.
4. **Responsive layout** — adaptive к размеру экрана, aspect ratio, DPI, safe areas; Flexbox/Yoga; `.compact`-класс на root.
5. **Инструментарий** — UI Toolkit (UXML/USS) как layout/темизация, uGUI для world-space, Rive для state-machine-анимированного вектора, Shader Graph/VFX Graph для эффектов.

Рекомендация для Project C: **UITK — основной layout/темизация; uGUI — world-space; Rive — motion-слой; Shader/VFX Graph — эффекты; DOTween — твины.** Всё — с бюджетами из `06_PERFORMANCE_GUARDRAILS.md`.

---

## 1. Living UI / Adaptive UI

### 1.1 Определение

**Living UI** — интерфейс, который **непрерывно отражает состояние** (здоровье, ресурсы, угроза, контекст) и **меняет свою форму/плотность** в зависимости от ситуации, не требуя ручного переключения экранов. **Adaptive UI** — подстройка под устройство/разрешение/ввод/локаль.

Признаки living UI:
- Элементы появляются/исчезают по контексту (например, прицел расширяется при беге; HUD скрывается в кинематографических сценах).
- Плотность информации масштабируется (progressive disclosure): по умолчанию минимум, детали — по hover/focus/кнопке.
- Состояние «дышит»: пульсация, дыхание (breathing), реакция на урон/лечение.
- Контекстно-зависимые подсказки (interaction hints) появляются только рядом с объектом.

### 1.2 State machines экранов

**Правило:** flow высокого уровня живёт в **одном месте** — `FlowController` (FSM). Экраны — views, не state machines для flow.

Состояния (пример для MMO):
```
Boot → MainMenu → CharacterSelect → Loading → InWorld → Paused → Settings → ...
```
Каждое состояние объявляет `PanelsToMount` и `ModalsToPush`. На входе — монтирование панелей, пуш модалей, блокировка/разблокировка input по приоритету слоёв. На выходе — pop модалей (с переходом), unmount панелей.

**Подсостояния экрана** (например, вкладки инвентаря) — отдельный локальный FSM внутри Screen, но не глобальный flow.

**Рекомендуемые инструменты:**
- Лёгкий FSM на C# (enum + switch или ScriptableObject-состояния). Избегать тяжёлых фреймворков (StrangeIOC).
- **Rive State Machine** — для анимированного векторного UI (меню, иконки, переходы, data-bound микро-интеракции). Rive `.riv` = арт + моушн + логика в одном файле; тысячи векторов при 120 fps; файлы на 90% меньше [^Rive^].
- **Unity Animator** — только для exceptional, artist-authored последовательностей. Избегать для рутинного UI (churn vertex buffer, asset explosion).

### 1.3 Контекстно-зависимые экраны

Паттерны:
- **HUD-адаптация по состоянию:** в бою — компактный HUD (HP, ammo, cooldowns); в exploration — расширенный (minimap, quest tracker, compass); в диалоге — скрытый HUD + диалоговое окно.
- **Прогрессивное раскрытие (Destiny 2):** плотный информационный UI; показывать больше только по запросу («show more»); item-centric focus; «глаза остаются в одном месте»; избегать принудительных сдвигов взгляда [^Destiny^].
- **Контекстные подсказки (Genshin):** UI держит и новичка, и ветерана; постоянная итерация UX; много систем без перегрузки — через тогглы/advanced mode [^Genshin^].
- **Diegetic UI (Star Citizen):** UI как часть мира (голограммы, приборные панели) — высокая плотность, но perf-риски; для Project C — только для кокпита/корабля, не для core chrome.

### 1.4 Player-centric design

- **Единая точка фокуса:** важная информация не прыгает по экрану; новые элементы появляются рядом с текущим фокусом.
- **Приоритет по угрозе/состоянию:** критический HP → красный пульс + vignette; low ammo → жёлтый; норма → нейтральный.
- **Не блокировать геймплей:** UI не должен требовать остановки (кроме явных модалей); hover-тултипы вместо кликов; быстрые слоты.
- **Input-блокировка во время переходов:** прозрачный full-screen blocker предотвращает soft-locks и double-taps.

### 1.5 Реализация в Unity

- **FlowController** + `IUIFrame` фасад (см. blueprint §7.3): `ShowScreen`, `PushModal`, `PopModal`.
- **ModalStack (LIFO):** верхний — интерактивный; нижние затемняются + input-blocked (не уничтожаются — сохраняется состояние).
- **Focus ring:** каждый Dialog объявляет default focus target + tab order; при push фокус → новый default; при pop → last-focused предыдущего.
- **Input router:** слои по приоритету SystemOverlay → Modal → Tooltip → HUD → World; центральный `InputRouter` диспатчит action в высший активный слой; World input **приостанавливается** при открытом модале.
- **Адаптивность:** класс `.compact` на root переключает отступы/tap targets; USS variables на `:root`; theme swap.

---

## 2. Circular progress bars, status indicators, loading animations

### 2.1 Circular progress bar

**Техники:**
- **Shader Graph (URP):** радиальный прогресс через polar coordinates + step/threshold; цвет по градиенту; glow по краю; опционально dashed segments.
- **uGUI Image (Filled, Radial 360):** самый простой путь; `fillAmount` 0–1; подходит для простых индикаторов.
- **UI Toolkit:** `VisualElement` с USS `border-radius: 50%` + conic-gradient — **USS не поддерживает conic-gradient**; реализовать через shader (custom material) или через несколько сегментов. Практичный путь — custom `VisualElement` с `generateVisualContent` (MeshGenerationContext) и ручной меш-веер, либо shader-материал.
- **Rive:** state machine с data-bound `progress` property; идеально для анимированного векторного кольца с glow/particles.

**Шейдер (концепт, URP Shader Graph):**
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

**Анимация прогресса:** не линейно — ease-out (cubic) при заполнении; при «потере» прогресса — быстрый drop (ease-in). Для XP/level-up — celebration burst (particles + scale-pop).

### 2.2 Status indicators

Типы:
- **Dot/badge** (онлайн-статус, уведомление): цвет + пульсация; размер ≤ 8 px в HUD, ≤ 16 в списках.
- **Health/energy bar:** горизонтальный; цвет по порогам (зелёный→жёлтый→красный); «damage flash» (красная вспышка при уроне, decay 0.3 s); «heal flash» (зелёный).
- **Cooldown radial:** вокруг иконки способности; grayscale + overlay при кулдауне; восстановление — яркость возвращается.
- **Buff/debuff иконки:** стаки числом; таймер кольцом; цвет по типу (buff=зелёный, debuff=красный).
- **Resource pips:** дискретные (мана-шары); анимация consume (scale down + fade) и refill (scale up + glow).

**Реализация:**
- uGUI: `Image.fillAmount` + цвет через `Color.Lerp`; damage flash через DOTween `DOColor`.
- UI Toolkit: inline style `width` анимировать нельзя (layout churn) → использовать `transform: scale` или шейдер; для баров — шейдер с `_Value` property.
- Шейдер: `_Value` (0–1), цвет по градиенту, glow на leading edge, damage trail (отдельный `_TrailValue`, лагает за `_Value`).

### 2.3 Loading animations

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
- **UI Toolkit:** shimmer через USS `background` с градиентом + анимация `background-position` (если поддерживается) или через shader; проще — shader-материал на VisualElement (custom material).
- **DOTween:** для простых spinners — `DORotate`.

**Правило производительности:** loading screen — единственное место, где можно позволить heavy-эффекты (particles, blur), т.к. 3D-рендер отключён. Но не злоупотреблять — время загрузки всё равно должно быть минимальным.

---

## 3. Halos, glows, particles, blur в UI

### 3.1 Halos (ореолы)

**Что:** мягкое свечение вокруг элемента (кнопка, слот, узел), сигнализирующее состояние/фокус/редкость/доступность.

**Техники:**
- **Box-shadow approximation (UITK):** USS не поддерживает `box-shadow` с blur; использовать child-элементы с градиентами (линейная аппроксимация) — как в проекте Cloudy (см. `06_HALOS_AND_FLEX.md`): 4 полосы по краям, середина прозрачна; ramp линейный от alpha к 0.
- **Shader Graph (glow sprite):** quad с радиальным градиентом + additive blending; масштабируется.
- **Rive:** векторный halo с blur (Rive поддерживает blur effects).
- **uGUI:** Image с sprite градиента + `CanvasGroup.alpha` для fade.

**Параметры (из проекта, Cloudy):**
- Внешнее размытие: `--hs/--ht/--hb` (spread), цвет с alpha, linear ramp.
- Круглое свечение точки: `inset: -hg`, `border-radius: 50%`, radial transparent 0…40% → color 100%.
- Внутреннее размытие: `inset: 0`, radial ellipse, transparent 0…46% → color 100%.

**Состояния halo:** idle (низкая alpha / скрыт), hover (fade-in 0.15 s), selected/active (полная alpha + pulse), disabled (серый/отсутствует). Переходы — opacity, не layout.

**Бюджет:** halo = дополнительный элемент + (опционально) shader; держать ≤ 1 halo per focusable element; не стакать без причины.

### 3.2 Glows

**Типы:**
- **Edge glow** (контур элемента): подсветка границы при focus/hover; цвет по состоянию.
- **Inner glow:** внутреннее свечение (например, слот с редким предметом).
- **Ambient glow:** мягкое фоновое свечение панели (glassmorphism).
- **Pulse glow:** пульсация (дыхание) для привлечения внимания (новый предмет, непрочитанное).

**Реализация:**
- **Shader Graph:** rim light (Fresnel) + emissive; для 2D UI — gradient + additive.
- **UI Toolkit:** custom VisualElement с `generateVisualContent` (mesh с vertex colors) — дёшево, без текстуры.
- **Post-processing (URP):** Bloom на UI-камере (если UI рендерится в отдельную камеру/RenderTexture) — global glow; осторожно с производительностью.
- **Rive:** glow как эффект слоя.

### 3.3 Particles в UI

**Когда использовать:** celebration (level-up, достижение, rare drop), transition wipes, ambient (пыль, искры в фоне меню), feedback (hit confirm, heal).

**Инструменты:**
- **VFX Graph:** GPU particles, тысячи частиц, 60 fps; идеально для полноэкранных эффектов (level-up burst). Рендер в отдельный quad/canvas.
- **ParticleSystem (Shuriken):** проще, CPU/GPU, подходит для небольших эффектов (confetti, искры). Использовать GPU simulation.
- **Rive:** векторные частицы (ограниченно).
- **UI Toolkit:** не подходит для частиц (нет particle system) — использовать uGUI/Canvas или fullscreen VFX.

**Правила:**
- Частицы в UI — **event-driven**, не постоянные (кроме ambient с жёстким бюджетом ≤ 50 частиц).
- Использовать object pooling для ParticleSystem.
- Отключать при `motion-reduction` setting.
- Не рендерить частицы поверх критичного текста (читаемость).

### 3.4 Blur (размытие)

**Типы:**
- **Background blur (glassmorphism):** размытие фона за панелью/модалом.
- **Motion blur:** размытие при быстром движении UI (редко, дорого).
- **Depth of field:** размытие заднего плана при открытии модала (фокус на диалоге).

**Реализация в Unity:**
- **URP Volume (post-processing):** Depth of Field / Gaussian Blur на UI-камере — глобальный, дешёвый (один проход), но размывает всё.
- **Shader Graph (frosted glass):** GrabPass / `_CameraOpaqueTexture` (URP) + kernel blur (5×5 или 9×9) + tint + noise; рендерится как материал на quad/panel. Стоит один draw call + texture sample; бюджетировать.
- **RenderTexture approach:** рендер UI/сцены в RenderTexture → blur shader → quad. Дороже (дополнительный проход).
- **UI Toolkit:** нет встроенного blur; использовать USS `filter: blur()` — **USS не поддерживает filter** (как в CSS). Обход: shader-материал на VisualElement (custom material) — экспериментально; либо рендерить панель в RenderTexture с blur.
- **Rive:** поддерживает blur effects на слоях.

**Бюджет:** blur = O(n²) семплов на пиксель; ограничить kernel 5×5 (25 samples) для UI; избегать per-element blur (размывать фон один раз).

### 3.5 Glassmorphism

**Определение:** полупрозрачная панель с размытым фоном, тонкой границей, мягким свечением — «стекло».

**Компоненты:**
- Фон: `rgba(255,255,255,0.08–0.15)` + blur (frosted glass).
- Граница: 1 px `rgba(255,255,255,0.18–0.30)` (верхняя/левая ярче — имитация света).
- Внутреннее свечение: subtle gradient top-down.
- Тень: мягкая, большая (ambient occlusion).
- Noise/grain: лёгкая текстура для глубины.

**Реализация:**
- **uGUI:** Panel с Image (sprite rounded rect) + blur shader material + outline.
- **UI Toolkit:** VisualElement с USS background `rgba(...)` + border + box-shadow (через child-ореолы); blur — через shader-материал на элемент (custom material) или URP Volume global blur.
- **Shader Graph (frosted glass):** GrabPass → blur kernel → tint → noise → output. Назначить как material на UI Image/VisualElement.

**Когда использовать:** модальные окна, HUD-панели поверх 3D-сцены, карточки предметов. Не для всего UI (читаемость, perf).

### 3.6 Neumorphism

**Определение:** «мягкий» рельеф — элементы выглядят выдавленными/вдавленными в фон через пару теней (светлая сверху-слева, тёмная снизу-справа).

**Плюсы:** тактильность, минимализм.
**Минусы:** низкий контраст, проблемы доступности, плохо работает на тёмных темах, устаревший тренд (2020–2021). В AAA используется редко и точечно (кнопки, слайдеры).

**Рекомендация для Project C:** не базовый стиль; использовать как **акцент** (например, toggle-кнопки, слайдеры в settings) в сочетании с glassmorphism/flat. Приоритет — читаемость и контраст (WCAG AA).

---

## 4. Responsive layout (адаптивность к экрану, aspect ratio)

### 4.1 Принципы

- **Reference resolution:** 1920×1080; scale mode `ScaleWithScreenSize`, match 0.5 (баланс width/height).
- **Safe areas:** учитывать notch/safe area на будущих платформах (даже на Windows — DPI scaling).
- **Aspect ratio handling:** 16:9 (основной), 16:10, 21:9 (ultrawide), 4:3. Не растягивать UI — использовать anchors/flex с min/max.
- **DPI awareness:** scale UI по `Screen.dpi` / reference; тестировать at 100/125/150/200% Windows scaling.
- **Compact mode:** `.compact` класс на root для маленьких экранов/Steam Deck; уменьшает отступы и tap targets.

### 4.2 Unity UI Toolkit

- **PanelSettings:** `scaleMode = ScaleWithScreenSize`, `referenceResolution = 1920×1080`, `match = 0.5`.
- **Flexbox (Yoga):** `flex-direction`, `flex-wrap`, `flex-grow/shrink`, `min-width/max-width`. Избегать фиксированных размеров для текста.
- **Media queries (USS):** `@media (max-width: ...) { ... }` — поддержка ограничена; использовать классы на root, переключаемые из C# по разрешению.
- **Overflow:** `overflow: hidden` для scroll-контейнеров; `ListView` с `fixedItemHeight`.
- **Gotchas Yoga (из `UI_TOOLKIT_GUIDE.md`):** в flex-column дочерний `width:100%` не нужен (align-self: stretch); `flex-grow:1 + flex-basis:0` → min-size = content → wrap, нужен `flex-shrink:1`; `flex-shrink:1 + min-height:0` для scroll-секций; header/actions с `flex-shrink:0`.

### 4.3 Unity uGUI

- **Canvas Scaler:** `Scale With Screen Size`, reference 1920×1080, match 0.5.
- **Anchors:** stretch для фонов, center для фиксированных элементов, corners для HUD-углов.
- **AspectRatioFitter:** для элементов с фиксированным соотношением (миникарта, портреты).
- **Safe Area:** `Screen.safeArea` → RectTransform margins.

### 4.4 Ultrawide / multi-monitor

- Ограничить максимальную ширину контента (max-width: 1920 или 2560) и центрировать; не растягивать HUD по краям (читаемость).
- Миникарта/чат — anchored к углам, не масштабируются бесконечно.

---

## 5. Micro-interactions (hover, press, transition)

### 5.1 Принципы

- **Длительности:** hover 80–120 ms, press 60–90 ms, transition in 120–200 ms, transition out 80–120 ms (out быстрее in).
- **Easing:** ease-out для появления (decelerate), ease-in для исчезновения (accelerate), ease-in-out для перемещения. Избегать linear (кроме spinners).
- **Анимировать только transforms** (translate/scale/rotate) и opacity; не layout-свойства (width/height/top/left) — иначе layout churn.
- **Не переключать классы** на больших иерархиях во время анимации; использовать inline style.
- **Input блокируется** во время переходов (full-screen blocker).
- **Haptic/звуки:** тонкий звук на hover/press (опционально, с настройкой громкости).

### 5.2 Паттерны

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

### 5.3 Реализация

- **DOTween (рекомендуется для uGUI/Transform):** `transform.DOScale`, `DOFade`, `DOColor`, sequences. Пулить твины, убивать при disable.
- **USS Transitions (UI Toolkit):** `transition: all 120ms ease-out;` — для простых hover/press. Ограничение: только opacity/translate/rotate/scale (не layout). Использовать `:hover`, `:active`, `:focus`.
- **Rive:** state machine с hover/press states; data-bound; идеально для векторных кнопок/иконок.
- **Shader Graph:** для glow pulse — animated `_Time` в shader (sin wave) с amplitude/frequency.

**Правило:** не смешивать DOTween и USS transitions на одном элементе — конфликты. Выбрать один слой per element.

---

## 6. Примеры из AAA-игр

### 6.1 Destiny 2 (Bungie)

- **Философия:** «глаза остаются в одном месте» — минимизировать сдвиги взгляда; item-centric focus.
- **Progressive disclosure:** плотный информационный UI; детали по запросу (hover/кнопка «show more»).
- **Визуал:** тёмная тема, cyan/amber акценты, тонкие линии, глубокие тени, стеклянные панели, subtle glow на выбранном предмете.
- **Анимации:** плавные crossfade между экранами, slide-переходы, scale-pop для предметов.
- **HUD:** минималистичный в бою, расширяется вне боя; health bar с damage flash.
- **Источник:** GDC 2016 «Building the user interface for Destiny»; Medium «UI of Destiny 2: Pursuits page».

### 6.2 Fortnite (Epic)

- **HUD:** конфигурируемый (пользователь может настроить), минималистичный в бою.
- **Визуал:** яркие, плоские цвета, bold typography, cartoon-стиль, glow на редких предметах, particle bursts при pickup.
- **Анимации:** быстрые, punchy (scale-pop, bounce), feedback-heavy.
- **Адаптивность:** cross-platform consistency (PC/console/mobile), responsive layout.
- **Источник:** GDC 2018 «Optimizing UE4 for Fortnite».

### 6.3 Valorant (Riot)

- **Визуал:** чистый, минималистичный, high-contrast; чёрный/белый/красный; тонкие линии; никакого glassmorphism в HUD (читаемость в соревновательном шутере).
- **UI-фреймворк:** единый кросспроектный фреймворк (Valorant → 2XKO); масштабируемая brand identity; переход от patchwork к единой гибкой системе для «forever game» [^Valorant^].
- **HUD:** минималистичный (HP, ammo, ability icons с cooldown radial); иконки с glow при готовности.
- **Анимации:** быстрые, функциональные, не декоративные.
- **Источник:** GDC 2017 «Art Directing a Consistent and Scalable Interactive Brand Identity»; GDC 2XKO talk.

### 6.4 Genshin Impact (miHoYo)

- **Визуал:** anime-style, мягкие градиенты, glassmorphism-панели, glow/bloom на UI, particle effects (искорки, лепестки), rounded corners.
- **Living UI:** UI держит и новичка, и ветерана; постоянная итерация UX; много систем без перегрузки (progressive disclosure, тогглы).
- **Loading:** полноэкранный арт + цитата/совет + тонкий прогресс-бар; skeleton screens.
- **Анимации:** плавные, «дышащие» (breathing), particle-rich.
- **Адаптивность:** mobile-first, но масштабируется на PC.
- **Источник:** GDC 2021 miHoYo.

### 6.5 Cyberpunk 2077 (CDPR)

- **UI-tech:** построен с нуля для тысяч независимых UI-инстансов по всему Night City; UI использовался для геймплея (diegetic UI на устройствах); акцент на бюджеты и платформенные таргеты [^Cyberpunk^].
- **Визуал:** glitch, scanlines, neon glow, chromatic aberration, holographic panels, glassmorphism, animated noise.
- **Living UI:** diegetic UI (терминалы, приборные панели) реагирует на состояние мира.
- **Performance:** агрессивный pooling; бюджетирование множества инстансов рано.
- **Источник:** GDC Showcase 2023 «User Interface in Cyberpunk 2077: Challenges and Optimizations».

### 6.6 Star Citizen (CIG)

- **Визуал:** крайне плотный симуляционный UI; diegetic (голограммы, приборные панели кокпита); glassmorphism + holographic.
- **Cautionary tale:** perf-проблемы с множеством элементов; scope creep. Урок: бюджетировать и ограничивать количество одновременных UI-элементов; distance-based LOD для world-space UI.
- **Источник:** публичные наблюдения (форумы/CIG).

### 6.7 Общие AAA-нити

1. UI — first-class система с собственным бюджетом, не afterthought.
2. Переиспользуемые компонентные библиотеки + design tokens.
3. Event-driven, data-bound, virtualized.
4. Большие инвестиции в tooling (Figma/Blend/Noasis Studio) и empowerment артистов.
5. Perf-guardrails (draw calls, overdraw, pooling) enforced в CI.

---

## 7. Unity-специфика: Shader Graph, VFX Graph, Rive, UI Toolkit

### 7.1 Shader Graph (URP) для UI-эффектов

**Применения:**
- Circular progress bars (radial mask + glow edge).
- Glows/halos (rim, inner glow, pulse).
- Frosted glass (grab pass + blur kernel + tint + noise).
- Dissolve/transition wipes (noise + threshold).
- Status bar damage trail (lagging value).
- Chromatic aberration / glitch (Cyberpunk-style).

**Техники:**
- **GrabPass / `_CameraOpaqueTexture`:** URP предоставляет `_CameraOpaqueTexture` после opaque pass; использовать для frosted glass. Включить «Opaque Texture» в URP asset.
- **Custom material на VisualElement:** UI Toolkit поддерживает custom materials (экспериментально) через `VisualElement.style.unityMaterial` или `generateVisualContent` + material. Проверять в Unity 6 версии.
- **uGUI:** Image с material (shader) — надёжный путь; `CanvasRenderer` поддерживает custom materials.
- **Shader Graph nodes:** Polar Coordinates, Gradient, Smoothstep, Fresnel, Noise, Time, Lerp.

**Пример: frosted glass shader (концепт):**
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

**Бюджет:** blur kernel 5×5 = 25 samples/px; на fullscreen panel — дорого. Ограничить: blur только для modal backgrounds (один раз), kernel ≤ 5×5, разрешение blur ≤ half-res.

### 7.2 VFX Graph для UI

**Применения:**
- Celebration bursts (level-up, rare drop): GPU particles, thousands, 60 fps.
- Transition wipes (iris, pixelate): fullscreen quad с VFX.
- Ambient backgrounds (меню): пыль, искры, энергия — жёсткий бюджет ≤ 50–100 частиц.
- Damage/heal feedback: кратковременные частицы.

**Интеграция:**
- VFX Graph рендерит в world space или в RenderTexture; для UI — рендер в RenderTexture → UI Image/quad.
- Или использовать `VisualEffect` компонент на Canvas (uGUI) — поддерживается.
- UI Toolkit: нет нативной интеграции; использовать через RenderTexture → VisualElement background.

**Правила:**
- Частицы — event-driven, не постоянные (кроме ambient с бюджетом).
- Object pooling для VisualEffect.
- Отключать при motion-reduction.
- Не рендерить поверх критичного текста.

### 7.3 Rive integration

**Что:** Rive — GPU vector/raster renderer; `.riv` = арт + моушн + логика (state machines, view models, Luau). Тысячи векторов при 120 fps; файлы на 90% меньше; responsive layouts, components, data binding [^Rive^].

**Применения в UI:**
- Loading screens (state machine: idle/loading/complete; data-bound progress).
- Animated icons (health, ammo, cooldown) с state machines.
- Transition wipes (векторные анимации).
- Data-bound micro-interactions (button hover/press, toggle, slider).
- Меню/экраны с complex motion (Genshin-style).

**Интеграция в Unity:**
- Asset Store runtime (`com.rive.rive-unity` или аналог); C# control через `RiveWidget`/`RiveFile`.
- Встраивание: UI Toolkit — через `VisualElement` + Rive render texture или custom element; uGUI — через `RawImage` с RenderTexture от Rive.
- Data binding: Rive ViewModels ↔ C# (set number/string/boolean/color).
- State machines: `stateMachine.Fire("trigger")`, `stateMachine.SetBool`.

**Паттерн (C#):**
```csharp
// Загрузка .riv, привязка к UI
var riveFile = riveAsset.Load();
var artboard = riveFile.Artboard("Main");
var stateMachine = artboard.StateMachine("LoadingSM");
stateMachine.SetBool("isLoading", true);
stateMachine.SetNumber("progress", 0.42f);
// Render to RenderTexture -> RawImage (uGUI) or VisualElement background (UITK)
```

**Когда использовать Rive vs Unity:**
- Rive: векторная анимация, state machines, data-bound motion, малый размер файлов, designer-friendly.
- Unity (DOTween/USS transitions): layout-driven анимации, простые твины, интеграция с game state.
- **Не заменяет** layout-систему (UITK/uGUI) — дополняет.

### 7.4 UI Toolkit (UXML/USS) для эффектов

**Что умеет:**
- USS transitions (`transition: all 120ms ease-out;`) — hover/press/focus.
- `-unity-background-image-tint-color` — тинтинг спрайтов.
- USS variables (`--*`) на `:root` — токены.
- `background-size`, `@import`, parent-state cascades (Unity 6).
- Rectangular masks (shader-based, без stencil) — предпочтительны.

**Что НЕ умеет (ограничения):**
- Нет `box-shadow` с blur (аппроксимация через child-ореолы).
- Нет `filter: blur()` / `backdrop-filter` (glassmorphism — через shader/URP).
- Нет conic-gradient (circular progress — через shader/custom mesh).
- Нет particle system.
- USS variables нельзя мутировать из C# во всех случаях — theming через stylesheet swap.

**Паттерн halo в UITK (из проекта):**
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

---

## 8. Performance guardrails (сводка)

| Метрика | Бюджет | Источник |
|---|---|---|
| Chrome draw calls | ≤ 4 | 06_PERFORMANCE_GUARDRAILS |
| UI CPU/кадр | ≤ 1.0 ms (mid-range PC) | blueprint §4.9 |
| Modal open transition | ≤ 16 ms (no hitch) | blueprint |
| Inventory scroll (10k) | ≥ 60 FPS | blueprint |
| Chat append (1k lines) | ≤ 0.5 ms | blueprint |
| UI assets memory | ≤ 120 MB | blueprint |
| World-space nameplates | ≤ 50 simultaneous | blueprint |
| Blur kernel | ≤ 5×5, half-res | §3.4 |
| Particles (ambient) | ≤ 50–100 | §3.3 |
| Halos per focusable | ≤ 1 | §3.1 |
| Distinct textures per batch | ≤ 8 | 05 §1.1 |

**Правила:**
- `DisplayStyle.None` для скрытых панелей (останавливает layout+render); `opacity:0` — всё ещё рендерится.
- Анимировать transforms, не layout.
- Pool widgets, не destroy.
- Dirty-tracking bindings (`INotifyBindablePropertyChanged`).
- Профилировать draw calls через Frame Debugger в CI.

---

## 9. Рекомендации для Project C (приоритеты)

**P0 (основа):**
1. Утвердить living UI-принципы: progressive disclosure, контекстно-зависимый HUD, единая точка фокуса.
2. State machine экранов через `FlowController` + ModalStack (уже в blueprint).
3. Responsive layout: PanelSettings scale mode, compact class, safe areas.

**P1 (эффекты, высокая отдача):**
4. Halo/glow система в UITK через child-ореолы (по образцу Cloudy) — недорого, визуально сильно.
5. Status indicators (HP/energy/cooldown/buff) с damage flash и glow.
6. Circular progress bars через shader (Shader Graph) для loading/cooldown/radial.

**P2 (полировка):**
7. Glassmorphism для модальных окон (frosted glass shader).
8. Loading screens: полноэкранный арт + совет + progress (Destiny/Genshin стиль).
9. Rive для анимированного векторного UI (loading, иконки, переходы).
10. VFX Graph для celebration bursts (level-up, rare drop).

**P3 (продвинутое):**
11. Neumorphism-акценты (toggles, sliders) — только если читаемость OK.
12. Particles ambient в меню (бюджет ≤ 50).
13. Diegetic UI для кокпита/корабля (осторожно, scope).

---

## 10. Источники

**AAA-игры / GDC:**
- [^Destiny^]: Bungie, "Building the user interface for Destiny" (GDC 2016); Medium "UI of Destiny 2: Pursuits page".
- [^Cyberpunk^]: CDPR, "User Interface in Cyberpunk 2077: Challenges and Optimizations" (GDC Showcase 2023).
- [^Valorant^]: Riot, "Art Directing a Consistent and Scalable Interactive Brand Identity" (GDC 2017); "Lessons from Building UI/UX in 2XKO" (GDC).
- Genshin Impact: miHoYo (GDC 2021).
- Fortnite: Epic (GDC 2018) "Optimizing UE4 for Fortnite".
- Star Citizen: CIG (публичные наблюдения).
- Sybo Games (GDC 2019) "Building a Unified Cross-Project UI Framework".
- DICE LA (GDC 2018) "Art Direction for AAA UI".
- SimCity 2013 (EA GDC) "AAA Game UI in HTML and JavaScript".

**Unity:**
- Unity Docs: "Optimizing performance (UI Toolkit)" (6000.5/6000.6).
- Unite 2024: "Getting the best performance with UI Toolkit" (Nicolas Borromeo).
- Unite 2025: "Reach new audiences with Accessibility and Localization in Unity 6".
- Unity Shader Graph docs (URP).
- Unity VFX Graph docs.
- Rive: rive.app/game-ui; "Rive for Unity" blog; "Building Pay 2 Win with Rive and Unity".

**Проект:**
- `docs/UI/Impruvements_research/03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` (архитектура, интерфейсы).
- `docs/UI/Impruvements_research/05_REVERSE_ENGINEERING_NOTES.md` (UITK internals).
- `docs/UI/Impruvements_research/06_PERFORMANCE_GUARDRAILS.md` (бюджеты).
- `docs/UI/full rebuild plans/Cloudy_redesign/06_HALOS_AND_FLEX.md` (halo-паттерны проекта).
- `docs/UI/UI_TOOLKIT_GUIDE.md` (Yoga gotchas, UIDocument lifecycle).

**Web/общее:**
- Material Design 3 (motion, elevation).
- Apple HIG (motion, accessibility).
- Refactoring UI (Wathan/Schoger) — микро-интеракции, spacing, цвет.

---

*Документ — research; реализация — через план в `04_IMPLEMENTATION_PLAN.md` и согласование с пользователем (Milestone 1 — инструментарий рядом, без поломок).*
