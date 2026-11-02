# 11 — Agent Tooling for AAA UI: Custom Tools, Skills, Workflows

> **Scope:** Research-отчёт по проектированию набора инструментов агента (custom tools / skills / MCP-инструменты) для создания красивого, адаптивного, отзывчивого UI в Unity 6 (URP, Windows standalone, MMO-песочница Project C). Дополняет `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` (архитектура), `09_DESIGN_PATTERNS.md` (паттерны), `10_TOOLING_PIPELINE.md` (дизайн → движок).
>
> **Цель:** перейти от «Windows 98»-уровня UI к AAA, автоматизировав создание UI (генерация UXML/USS, стилизация, анимация) через агента, а не ручным пиксель-копированием.

---

## 0. TL;DR

| Вопрос | Ответ |
|---|---|
| Что нужно для AAA-UI beyond Windows 98? | Design tokens + компонентная библиотека + state machines экранов + data binding + эффекты (halo/glow/blur/glass) + адаптивный layout + микро-интеракции + perf-бюджеты. |
| Как автоматизировать создание UI? | Агент генерирует UXML/USS из токенов + шаблонов компонентов + данных (Figma/FigCraft/HTML/Rive), проводит review в UI Builder, wiring через ViewModels, анимация через Rive/DOTween. |
| Какие custom MCP-инструменты нужны? | `ui_generate_screen`, `ui_generate_component`, `ui_apply_token_set`, `ui_create_rive_binding`, `ui_generate_shader` (glass/glow/progress), `ui_generate_vfx`, `ui_audit_layout`, `ui_check_perf`, `ui_export_tokens`. |
| Как обойти ограничения UITK? | backdrop-filter → frosted-glass shader / RenderTexture; gap → USS `gap` (Unity 6) или margins; transitions → USS transitions (только transform/opacity) + DOTween; box-shadow → child-ореолы + shader; conic-gradient → shader/custom mesh; blur → shader/URP Volume; particles → VFX Graph в RenderTexture. |
| Как делать «живой» UI? | FlowController FSM + ModalStack + InputRouter + ViewModels (INotifyBindablePropertyChanged) + контекстно-зависимый HUD + progressive disclosure + data-bound Rive state machines. |
| Примеры agent-driven создания? | Figma/FigCraft → UXML/USS (через importer), Rive → Unity (data binding), HTML/CSS → UXML/USS (механический перевод), Shader Graph → UI-эффекты через агент-генерацию HLSL/Shader Graph. |

**Ключевой принцип:** агент **не заменяет** дизайнера и UI Builder — он **автоматизирует механику** (генерация файлов, wiring, токены, эффекты), оставляя человеку review визуала и Play Mode-тест (см. `dont-start-play` / `manual-playtest-only`).

---

## 1. Проблема: почему «Windows 98» и как агент это чинит

Текущее состояние Project C (см. `01_CURRENT_STATE_AUDIT.md`): 14 файлов с `Clear()+CloneTree()`, 15 с `Resources.Load<StyleSheet>`, 3.1 MB/frame GC, скрытые панели продолжают рендериться, нет единой компонентной библиотеки, токены разбросаны. Результат — UI выглядит как набор разрозненных элементов без глубины, адаптивности и микро-интеракций.

**Агент-инструментарий решает три класса проблем:**

1. **Генерация (boilerplate)** — создание UXML/USS/скриптов по шаблонам, без ручного копирования.
2. **Согласованность (consistency)** — токены, компоненты, state machines применяются единообразно через инструменты, а не «на глаз».
3. **Эффекты (AAA-визуал)** — агент генерирует шейдеры, VFX, Rive-привязки, которые руками писать долго и ошибочно.

**Что агент НЕ делает:** не запускает Play Mode, не делает скриншоты, не принимает визуальные решения (цвета/композиция) — это остаётся за дизайнером/пользователем (см. HARD RULES в AGENTS.md).

---

## 2. Архитектура agent toolset

### 2.1 Три слоя инструментария

```
┌─────────────────────────────────────────────────────┐
│  Layer 3: SKILLS (opencode) — знания/процессы       │
│  ui, ui-uitk, ui-ugui, ui-imgui, unity-mcp-orchestr.│
│  floating-origin-vertical-slice, dont-start-play     │
├─────────────────────────────────────────────────────┤
│  Layer 2: CUSTOM MCP TOOLS (Unity-side, C#)         │
│  ui_generate_screen, ui_generate_component,         │
│  ui_apply_token_set, ui_create_rive_binding,        │
│  ui_generate_shader, ui_generate_vfx, ui_audit_*,   │
│  ui_export_tokens, ui_check_perf                    │
├─────────────────────────────────────────────────────┤
│  Layer 1: BASE MCP TOOLS (unityMCP / anklebreaker)  │
│  manage_gameobject, manage_script, manage_asset,     │
│  read_console, batch_execute, manage_scene, ...     │
└─────────────────────────────────────────────────────┘
```

- **Layer 1 (базовые MCP-инструменты)** — уже есть (unityMCP / anklebreaker). Используются для сцены, объектов, скриптов, консоли.
- **Layer 2 (custom MCP tools)** — регистрируются в Unity через `[McpTool]` / `execute_custom_tool` (см. `mcpforunity://custom-tools`). Это «умные» операции над UI: генерация экрана, применение токенов, создание Rive-привязки и т.д.
- **Layer 3 (skills)** — markdown-инструкции для агента (когда и как вызывать инструменты, какие шаблоны использовать). Живут в `.opencode/skills/`.

### 2.2 Регистрация custom MCP tools (Unity-side)

Unity MCP (mcp-for-unity) поддерживает кастомные инструменты через C#-атрибуты или `[CliCommand]` (unity CLI). Паттерн:

```csharp
// Assets/_Project/Scripts/Editor/UIAgentTools.cs
using McpForUnity; // или Unity.Pipeline.Commands для CLI

[McpTool("ui_generate_screen")]
public static class UiGenerateScreenTool
{
    [McpToolParam("screen_name", required: true)]
    public string ScreenName;

    [McpToolParam("template", required: false, default: "modal")]
    public string Template; // modal | hud | fullscreen | overlay

    [McpToolParam("tokens_asset", required: false)]
    public string TokensAssetPath; // path to ThemeData asset

    public static McpToolResult Execute(UiGenerateScreenParams p)
    {
        // 1. Load template UXML from Resources/Templates/
        // 2. Substitute tokens ({{color.surface}}, {{spacing.md}})
        // 3. Write Assets/_Project/UI/Screens/{ScreenName}.uxml
        // 4. Create companion .uss with class skeleton
        // 5. Create ViewModel stub C# script
        // 6. Trigger compile (automatic via create_script)
        // 7. Return { files: [...], next_steps: [...] }
    }
}
```

**Альтернатива (CLI-based):** через `[CliCommand]` в `Unity.Pipeline.Commands` namespace — инструмент становится доступен через `unity mcp` и `unity command`. Подходит для CI/headless.

**Открытие:** `mcpforunity://custom-tools` → список доступных custom tools; вызов через `execute_custom_tool(tool_name, parameters)`.

### 2.3 Skills (opencode layer)

Существующие UI-скиллы: `ui`, `ui-uitk`, `ui-ugui`, `ui-imgui`, `unity-mcp-orchestrator`. Для agent tooling нужны **новые/расширенные** скиллы:

| Skill ID | Назначение | Статус |
|---|---|---|
| `ui` (существует) | Роутинг по UI-системам | ✅ есть |
| `ui-uitk` (существует) | UXML/USS авторинг | ✅ есть |
| `ui-ugui` (существует) | Canvas/uGUI | ✅ есть |
| `ui-tooling-agent` (НОВЫЙ) | Оркестрация agent-driven UI: генерация экранов/компонентов, токены, wiring, эффекты | 🆕 создать |
| `ui-effect-shader` (НОВЫЙ) | Генерация Shader Graph / HLSL для UI-эффектов (glass, glow, progress, wipe) | 🆕 создать |
| `ui-rive-binding` (НОВЫЙ) | Rive state machines + data binding в Unity | 🆕 создать |
| `ui-living` (НОВЫЙ) | State machines экранов, адаптивный HUD, progressive disclosure, data binding | 🆕 создать |
| `ui-perf-audit` (НОВЫЙ) | Проверка perf-бюджетов UI (draw calls, GC, vertex budget) | 🆕 создать |

Скиллы живут в `.opencode/skills/<id>/SKILL.md` с YAML frontmatter (`name`, `description`, `user-invocable`). Формат см. существующие (например, `ui/SKILL.md`).

### 2.4 Полная таксономия инструментов

#### 2.4.1 Генерация структуры (UXML/USS)

| Инструмент | Вход | Выход | Когда |
|---|---|---|---|
| `ui_generate_screen` | имя, шаблон (modal/hud/fullscreen/overlay), токены | `.uxml` + `.uss` + ViewModel stub | Новый экран/окно |
| `ui_generate_component` | имя компонента, вариант (button/card/slot/input), токены | UXML template + USS классы + C# partial | Новый переиспользуемый виджет |
| `ui_generate_list_item` | тип элемента списка, поля | UXML item template + binding hints | ListView/TreeView items |
| `ui_scaffold_theme` | коллекция токенов (JSON/CSS) | `DesignTokens.uss` + `ThemeData.asset` | Новая тема/импорт токенов |
| `ui_import_tokens` | путь к экспорту FigCraft/Figma (JSON/CSS) | обновлённые токены + хэш-проверка drift | Round-trip из дизайна |

#### 2.4.2 Стилизация и эффекты

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_apply_token_set` | имя токен-набора, целевой UXML/USS | замена `var(--*)` ссылок, обновление классов |
| `ui_generate_shader` | тип эффекта (glass/glow/progress/wipe/chromatic), параметры | Shader Graph asset / HLSL + material + USS `unity-material` hint |
| `ui_generate_vfx` | тип эффекта (burst/confetti/ambient), бюджет частиц | VFX Graph asset + C# trigger wrapper |
| `ui_create_halo` | цвет, размер, состояния (idle/hover/selected/disabled) | child-ореолы в UXML + USS transition |
| `ui_create_glass_panel` | blur radius, tint, edge highlight | frosted-glass material + UXML + USS |

#### 2.4.3 Анимация

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_create_rive_binding` | путь к `.riv`, artboard, state machine, data bindings | C# controller + UXML `VisualElement` placeholder + инструкции wiring |
| `ui_create_transition` | тип (fade/slide/scale-pop/wipe), длительность, easing | DOTween sequence / USS transition + blocker setup |
| `ui_create_micro_interaction` | элемент, взаимодействие (hover/press/focus) | USS `:hover`/`:active`/`:focus` + transition + (опц.) Rive state |

#### 2.4.4 Живой UI (living UI)

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_create_flow_controller` | список состояний (Boot/MainMenu/InWorld/Paused/Settings), переходы | `FlowController.cs` + `IUIFrame` wiring + ModalStack |
| `ui_create_viewmodel` | имя, свойства (тип + binding mode) | C# ViewModel с `[GeneratePropertyBag]`, `INotifyBindablePropertyChanged` |
| `ui_create_adaptive_hud` | набор контекстов (combat/exploration/dialog), элементы | HUD controller + классы `.hud-combat`/`.hud-explore` + переключение |
| `ui_create_progressive_disclosure` | базовый набор элементов + «расширенный» набор | контейнеры + toggle + анимация expand/collapse |

#### 2.4.5 Аудит и проверка

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_audit_layout` | путь к UXML/USS | отчёт: Yoga-готчи, фиксированные размеры текста, отсутствие flex-shrink, overflow |
| `ui_check_perf` | имя экрана/панели | draw calls (оценка), vertex budget, текстуры, GC-риски |
| `ui_check_accessibility` | UXML | контраст (WCAG AA), tap targets ≥ 44px, фокус-кольцо, screen-reader labels |
| `ui_diff_design` | скриншот FigCraft + скриншот Unity (пользователь) | отчёт о расхождениях (только если пользователь предоставил оба) |

#### 2.4.6 Импорт/экспорт

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_export_tokens` | путь к FigCraft/Figma экспорту | `DesignTokens.uss` + `ThemeData.asset` + хэш |
| `ui_import_rive` | путь к `.riv` | копия в Assets + C# loader stub + инструкции |
| `ui_import_svg_icons` | папка SVG | sprite atlas PNG @2x/@3x + USS background-image refs |

---

## 3. Как автоматизировать создание красивого UI

### 3.1 Pipeline генерации UXML/USS

```
[Intent]  "Создать экран инвентаря с сеткой 4×6, фильтрами, тултипом"
   │
   ▼  ui_generate_screen (template=fullscreen, name=InventoryScreen)
[Structure]  InventoryScreen.uxml (root + header + filter-bar + grid + tooltip)
   │
   ▼  ui_generate_component (card=InventorySlot, variant=slot)
[Components]  InventorySlot.uxml (template) + InventorySlot.uss + InventorySlot.cs
   │
   ▼  ui_apply_token_set (tokens=DesignTokens)
[Style]  подстановка var(--color-surface), var(--spacing-md), var(--radius-lg)
   │
   ▼  ui_create_halo (color=rarity, states)
[Effects]  halo child-elements + USS transitions
   │
   ▼  ui_create_viewmodel (InventoryViewModel, properties)
[Data]  C# ViewModel с bindings
   │
   ▼  ui_audit_layout + ui_check_perf
[Verify]  отчёт
   │
   ▼  (пользователь) UI Builder review + Play Mode test
```

**Ключевое:** каждый шаг — отдельный MCP-вызов (или batch ≤25 команд через `batch_execute`). Агент не пишет файлы руками там, где есть инструмент; инструменты гарантируют консистентность (шаблоны, токены, naming).

### 3.2 Шаблоны (templates)

Шаблоны хранятся в `Assets/_Project/UI/Templates/` (UXML + USS + C# stub). Инструмент `ui_generate_screen` выбирает шаблон по `template`:

| Шаблон | Структура | Применение |
|---|---|---|
| `modal` | dim-bg + centered panel + header + body + footer (buttons) | диалоги, подтверждения |
| `fullscreen` | header-bar + scrollable body + optional sidebar | инвентарь, настройки, карта |
| `hud` | corner-anchored cluster (top-left/top-right/bottom) | игровой HUD |
| `overlay` | full-screen transparent layer + floating elements | тултипы, контекстные подсказки |
| `panel` | titled panel с close button | окна чата, квест-лог |
| `toast` | small notification card | уведомления |

Шаблоны — **git-tracked**, версионируются; агент только инстанцирует их с подстановкой токенов/имён. Это исключает «Windows 98»-эффект: каждый новый экран начинается с AAA-шаблона (glass-panel, halo, responsive flex, safe areas).

### 3.3 Стилизация через токены (автоматическая)

Токены живут в `DesignTokens.uss` (USS custom properties на `:root`) + `ThemeData.asset` (ScriptableObject для не-CSS значений). Инструмент `ui_scaffold_theme` генерирует их из экспорта FigCraft/Figma:

```css
/* DesignTokens.uss — автогенерируемый, НЕ редактировать руками */
:root {
  /* Colors */
  --color-surface: #1a1d24;
  --color-surface-elevated: #232730;
  --color-accent: #78b891;
  --color-text-primary: #e8eaed;
  --color-text-secondary: #9aa0a6;
  --color-danger: #e05252;
  /* Spacing */
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  /* Radius */
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 16px;
  --radius-full: 9999px;
  /* Motion */
  --duration-fast: 80ms;
  --duration-normal: 160ms;
  --duration-slow: 320ms;
  --easing-out: cubic-bezier(0.16, 1, 0.3, 1);
}
[data-theme="dark"] { /* overrides */ }
```

Компонентные USS-классы ссылаются на токены: `background-color: var(--color-surface);`. Агент **никогда** не хардкодит цвета/отступы в компонентных стилях — только через `var(--*)`. Это гарантирует согласованность и темизацию.

**Drift detection:** `ui_import_tokens` считает хэш экспорта; CI падает, если `DesignTokens.uss` не совпадает. Токены — single source of truth в FigCraft/Figma, Unity только потребляет.

### 3.4 Анимация: автоматизация

**Иерархия анимационных инструментов (по сложности):**

1. **USS transitions** (простейшие) — для hover/press/focus на простых элементах. Агент генерирует через `ui_create_micro_interaction`:
   ```css
   .button {
     transition: transform var(--duration-fast) var(--easing-out),
                 background-color var(--duration-fast) var(--easing-out);
   }
   .button:hover { transform: scale(1.03); background-color: var(--color-accent); }
   .button:active { transform: scale(0.97); }
   ```
   **Ограничение:** только `transform`/`opacity`/`color`/`background-color`. Не layout-свойства.

2. **DOTween** — для код-driven твинов (модал in/out, shake, scale-pop). Агент генерирует sequence через `ui_create_transition`:
   ```csharp
   // автогенерируемый
   public static Tween ModalIn(VisualElement panel, VisualElement dimBg) {
       var seq = DOTween.Sequence();
       dimBg.style.opacity = 0;
       seq.Append(dimBg.DOFade(0.6f, 0.16f).SetEase(Ease.OutCubic));
       panel.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1));
       panel.style.opacity = 0;
       seq.Join(panel.DOFade(1f, 0.16f));
       seq.Join(panel.DOScale(1f, 0.16f).SetEase(Ease.OutCubic));
       seq.OnComplete(() => InputBlocker.Disable());
       return seq;
   }
   ```
   **Правило:** не смешивать DOTween и USS transitions на одном элементе.

3. **Rive** — для векторной анимации, state machines, data-bound микро-интеракций. Агент создаёт binding через `ui_create_rive_binding`.

4. **VFX Graph** — для частиц/juice (celebration, transitions). Агент создаёт через `ui_generate_vfx`.

### 3.5 Эффекты: автоматическая генерация шейдеров

Агент не пишет HLSL с нуля — он использует **шаблоны Shader Graph** (URP) с параметрами. Инструмент `ui_generate_shader` принимает `effect_type` и генерирует Shader Graph asset + material + USS-класс с `unity-material` ссылкой.

**Поддерживаемые типы эффектов:**

| Эффект | Техника | Шаблон |
|---|---|---|
| `glass` (frosted glass) | GrabPass `_CameraOpaqueTexture` + kernel blur 5×5 + tint + noise + edge highlight | `UI_Glass.shadergraph` |
| `glow` (rim/inner/pulse) | Fresnel + emissive + animated sin (pulse) | `UI_Glow.shadergraph` |
| `progress_radial` | polar coords + step + smoothstep edge + glow | `UI_RadialProgress.shadergraph` |
| `progress_bar` | `_Value` + `_TrailValue` (damage trail) + gradient | `UI_ProgressBar.shadergraph` |
| `wipe` (iris/pixelate/dissolve) | noise + threshold + edge glow | `UI_Wipe.shadergraph` |
| `chromatic` (glitch) | UV offset per channel + noise | `UI_Chromatic.shadergraph` |
| `halo` (soft glow around rect) | SDF rounded rect + falloff + color | `UI_Halo.shadergraph` |

**Пример вызова:**
```
ui_generate_shader(
  effect_type="glass",
  params={ blur_radius: 8, tint_color: "var(--color-surface)", tint_strength: 0.15, noise_strength: 0.02, edge_highlight: true },
  target_material="Assets/_Project/UI/Materials/GlassPanel.mat",
  uss_class="glass-panel"
)
```

Выход: Shader Graph asset + Material + USS-класс `.glass-panel { unity-material: ...; }` + C# helper для runtime-параметров (`SetFloat("_BlurRadius", ...)`).

**Бюджет:** blur kernel ≤ 5×5, half-res; glow — один draw call; particles — event-driven.

### 3.6 VFX Graph для UI

`ui_generate_vfx` создаёт VFX Graph asset + C# trigger-обёртку. Применения:

- **Celebration burst** (level-up, rare drop): GPU particles, 1000–5000, 60 fps, render в RenderTexture → UI overlay.
- **Transition wipe** (iris, pixelate): fullscreen quad с VFX.
- **Ambient** (меню: пыль, искры): ≤ 50–100 частиц, постоянный, жёсткий бюджет.
- **Damage/heal flash**: кратковременные частицы (≤ 200), event-driven.

Интеграция: VFX рендерится в RenderTexture → `VisualElement.style.backgroundImage` (UITK) или `RawImage` (uGUI). Object pooling для `VisualEffect` компонентов.

---

## 4. Overcoming Unity UI Toolkit limitations

UITK — web-inspired, но **не браузер**. Ниже — каждое ограничение и обход.

### 4.1 Таблица: CSS-фича → Unity-обход

| CSS-фича | UITK поддержка | Обход (agent tool) |
|---|---|---|
| `box-shadow` (blur) | ❌ нет | child-ореолы (линейная аппроксимация, 4 полосы) + `ui_create_halo`; либо shader `UI_Halo` |
| `backdrop-filter: blur()` | ❌ нет | frosted-glass shader (GrabPass) через `ui_generate_shader(type=glass)`; либо URP Volume global blur; либо RenderTexture |
| `filter: blur()` | ❌ нет | shader на элемент; либо URP post-process на UI-камере |
| `gap` (flex) | ✅ Unity 6+ | использовать напрямую; для старых — margins |
| `transition` | ✅ только transform/opacity/color | USS transitions для простых; DOTween для сложных; `ui_create_micro_interaction` / `ui_create_transition` |
| `conic-gradient` | ❌ нет | shader `UI_RadialProgress`; либо custom mesh (generateVisualContent) |
| `radial-gradient` | ⚠️ частично | USS `radial-gradient()` в Unity 6; иначе текстура/шader |
| `grid` (CSS Grid) | ❌ нет | вложенный flex (row/col) или абсолютное позиционирование; агент генерирует вложенный flex |
| `position: sticky` | ❌ нет | C#-логика скролла + классы |
| `:nth-child()` | ⚠️ частично | классы на элементах при генерации |
| `animation` (keyframes) | ❌ нет | DOTween / Rive / USS transitions |
| `particle system` | ❌ нет (UI Toolkit) | VFX Graph → RenderTexture → `ui_generate_vfx` |
| `box-shadow` (inset) | ❌ нет | внутренние градиенты (child elements) |
| `mix-blend-mode` | ⚠️ частично | shader blending |
| `clip-path` | ❌ нет | маски (rectangular preferred) + shader |
| `text-shadow` | ⚠️ частично | TMP outline/glow; либо shader |
| `aspect-ratio` | ✅ Unity 6+ | AspectRatioFitter (uGUI) или USS `aspect-ratio` |

### 4.2 Glassmorphism (backdrop-filter обход) — детально

**Проблема:** UITK не имеет `backdrop-filter`. Glassmorphism (полупрозрачная панель с размытым фоном) — ключевой AAA-паттерн (Genshin, Cyberpunk, Destiny).

**Решение 1 — Frosted glass shader (рекомендуется для модалей/панелей):**

```hlsl
// UI_Glass.shadergraph (концепт фрагмента)
// Требует: URP Opaque Texture включён в URP asset
float2 uv = IN.uv;
float4 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);

// Blur kernel 5x5 (25 samples) — бюджет
float4 blurred = 0;
for (int x = -2; x <= 2; x++)
  for (int y = -2; y <= 2; y++)
    blurred += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler, uv + float2(x,y) * _BlurSize);
blurred /= 25;

float4 tinted = lerp(blurred, _TintColor, _TintStrength);
// noise
float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler, uv * _NoiseScale + _Time.y * _NoiseSpeed).r;
tinted.rgb += noise * _NoiseStrength;
// edge highlight (top-left brighter)
float edge = smoothstep(0.98, 1.0, max(abs(uv.x-0.5), abs(uv.y-0.5)) * 2);
tinted.rgb += edge * _EdgeColor.rgb;

return tinted;
```

**Настройка:** URP asset → Rendering → Opaque Texture = ON. Material назначается на VisualElement через `style.unityMaterial` (экспериментально в Unity 6) или через uGUI Image.material.

**Бюджет:** blur — O(n²) семплов/пиксель; ограничить kernel 5×5, разрешение blur ≤ half-res, применять только к modal backgrounds (один раз), не per-element.

**Решение 2 — URP Volume global blur:** Depth of Field / Gaussian Blur на UI-камере — глобальный, дешёвый (один проход), но размывает всё. Подходит для «открытие модала → размытие фона».

**Решение 3 — RenderTexture:** рендер UI/сцены в RenderTexture → blur shader → quad. Дороже, но полный контроль.

Агент-инструмент `ui_create_glass_panel` выбирает решение по контексту (modal → shader; fullscreen blur → Volume; сложный → RenderTexture) и генерирует соответствующие ассеты.

### 4.3 Halo/box-shadow обход (детально)

USS не поддерживает `box-shadow` с blur. Проект Cloudy использует **child-ореолы** (линейная аппроксимация): 4 полосы по краям, середина прозрачна; ramp линейный от alpha к 0. Это дёшево (один элемент, без текстуры) и выглядит как soft shadow.

```xml
<!-- UXML -->
<ui:VisualElement class="card">
  <ui:VisualElement class="halo-card" />  <!-- glow, opacity 0→1 при .hl -->
</ui:VisualElement>
```

```css
/* USS */
.halo-card {
  position: absolute;
  top: -12px; bottom: -12px; left: -12px; right: -12px;
  border-radius: 50%;
  opacity: 0;
  transition: opacity var(--duration-normal) var(--easing-out);
  pointer-events: none;
}
.card.hl > .halo-card {
  opacity: 1;
  background-color: rgba(120, 184, 145, 0.55);
}
```

Агент-инструмент `ui_create_halo` генерирует такие ореолы для заданного элемента с параметрами цвет/размер/состояния. Для более сложных (радиальный glow) — shader `UI_Halo`.

**Бюджет:** ≤ 1 halo per focusable element; не стакать без причины.

### 4.4 Transitions обход

USS transitions поддерживают только `transform`/`opacity`/`color`/`background-color`. Для layout-анимаций (width/height/top/left) — **DOTween** (анимация через inline style transform, не layout). Для сложных — **Rive** (векторные state machines).

**Правило агента:** `ui_create_micro_interaction` генерирует USS transitions только для transform/opacity; если нужны layout-свойства — переключает на DOTween-генерацию.

### 4.5 Circular progress (conic-gradient обход)

USS не поддерживает `conic-gradient`. Обходы:

1. **Shader `UI_RadialProgress`** (рекомендуется): polar coords + step + smoothstep edge + glow. Агент генерирует через `ui_generate_shader(type=progress_radial)`.
2. **Custom mesh** via `generateVisualContent` (MeshGenerationContext) — ручной веер. Дёшево, но больше кода.
3. **uGUI Image (Filled, Radial 360)** — `fillAmount` 0–1. Самый простой, но только для простых индикаторов.
4. **Rive** — state machine с data-bound `progress`. Идеально для анимированного векторного кольца.

### 4.6 Data binding authoring обход

UITK runtime bindings (Unity 6) настраиваются в коде, не в Builder. Агент-инструмент `ui_create_viewmodel` генерирует:

```csharp
[GeneratePropertyBag]
public partial class InventoryViewModel : INotifyBindablePropertyChanged
{
    [CreateProperty] public int SelectedSlotIndex { get; set; }
    [CreateProperty] public ObservableCollection<ItemStack> Items { get; } = new();

    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    // binding hint (generated comment):
    // visualElement.SetBinding("selectedIndex", new DataBinding {
    //     dataSource = this, dataSourcePath = "SelectedSlotIndex", bindingMode = BindingMode.TwoWay
    // });
}
```

**Производительность:** для высокочастотных данных (HP, cooldown) — bypass binding, прямая запись в элемент, троттлинг ≤10 Hz для текста, per-frame только transforms.

### 4.7 Particles в UI обход

UITK не имеет particle system. Обход: VFX Graph → RenderTexture → `VisualElement.style.backgroundImage` (UITK) или `RawImage` (uGUI). Агент-инструмент `ui_generate_vfx` создаёт VFX + C# trigger-обёртку + инструкции по RenderTexture.

---

## 5. Agent workflows для «живого» UI (living UI)

### 5.1 Living UI — определение

**Living UI** — интерфейс, который непрерывно отражает состояние (здоровье, ресурсы, угроза, контекст) и меняет форму/плотность в зависимости от ситуации. Признаки:

- Элементы появляются/исчезают по контексту (прицел расширяется при беге; HUD скрывается в кинематографических сценах).
- Плотность информации масштабируется (progressive disclosure): минимум по умолчанию, детали — по hover/focus/кнопке.
- Состояние «дышит»: пульсация, реакция на урон/лечение.
- Контекстно-зависимые подсказки появляются только рядом с объектом.

### 5.2 State machines экранов (FlowController)

**Правило:** flow высокого уровня живёт в **одном месте** — `FlowController` (FSM). Экраны — views, не state machines для flow.

Агент-инструмент `ui_create_flow_controller` генерирует:

```csharp
public class FlowController : MonoBehaviour
{
    public enum State { Boot, MainMenu, CharacterSelect, Loading, InWorld, Paused, Settings }

    private State _current;
    private readonly Stack<Modal> _modalStack = new();

    public void TransitionTo(State next)
    {
        // OnExit: pop modals (with transition), unmount panels
        // OnEnter: mount panels (PanelsToMount), push modals (ModalsToPush),
        //          lock/unlock input by layer priority
        _current = next;
        ApplyState(_current);
    }

    public void PushModal(Modal m) { _modalStack.Push(m); m.Show(); InputBlocker.Enable(); }
    public void PopModal() { var m = _modalStack.Pop(); m.Hide(); if (_modalStack.Count==0) InputBlocker.Disable(); }
}
```

**Подсостояния экрана** (вкладки инвентаря) — локальный FSM внутри Screen, не глобальный flow.

**Инструменты состояния:** лёгкий FSM на C# (enum + switch); **Rive State Machine** для анимированного векторного UI; Unity Animator — только для exceptional последовательностей.

### 5.3 Адаптивный HUD (контекстно-зависимый)

Агент-инструмент `ui_create_adaptive_hud` генерирует HUD-контроллер, переключающий классы на root:

```csharp
public class AdaptiveHudController : MonoBehaviour
{
    public enum Context { Combat, Exploration, Dialog, Cutscene }

    [SerializeField] private VisualElement _root;
    [SerializeField] private List<VisualElement> _combatOnly, _exploreOnly, _dialogOnly;

    public void SetContext(Context ctx)
    {
        _root.RemoveFromClassList("hud-combat");
        _root.RemoveFromClassList("hud-explore");
        _root.RemoveFromClassList("hud-dialog");
        _root.AddToClassList($"hud-{ctx.ToString().ToLower()}");

        // progressive disclosure: show/hide groups
        SetGroup(_combatOnly, ctx == Context.Combat);
        SetGroup(_exploreOnly, ctx == Context.Exploration);
        SetGroup(_dialogOnly, ctx == Context.Dialog);
    }
}
```

USS:
```css
.hud-combat .minimap { display: none; }
.hud-combat .quest-tracker { display: none; }
.hud-explore .ammo-counter { opacity: 0.4; }
```

**Правило:** в бою — компактный HUD (HP, ammo, cooldowns); в exploration — расширенный (minimap, quest tracker, compass); в диалоге — скрытый HUD + диалоговое окно.

### 5.4 Progressive disclosure

Агент-инструмент `ui_create_progressive_disclosure` генерирует контейнер с базовым и расширенным набором элементов + toggle + анимация expand/collapse (DOTween, transform scale Y).

**Паттерн (Destiny 2):** плотный информационный UI; показывать больше только по запросу («show more»); item-centric focus; «глаза остаются в одном месте».

### 5.5 Data binding (MVVM) — автоматизация

Агент-инструмент `ui_create_viewmodel` генерирует ViewModel с `[GeneratePropertyBag]` и `[CreateProperty]`, а также binding-hints в комментариях. Паттерн:

```csharp
[GeneratePropertyBag]
public partial class PlayerHudViewModel : INotifyBindablePropertyChanged
{
    [CreateProperty] public float Health { get; private set; }
    [CreateProperty] public float HealthMax { get; private set; }
    [CreateProperty] public string AmmoText { get; private set; }

    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    // Вызывается из game code (не из UI)
    public void UpdateHealth(float hp, float max)
    {
        Health = hp; HealthMax = max;
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Health)));
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(HealthMax)));
    }
}
```

Wiring в View:
```csharp
var vm = new PlayerHudViewModel();
healthBar.SetBinding("value", new DataBinding {
    dataSource = vm, dataSourcePath = "Health", bindingMode = BindingMode.ToTarget
});
```

**Оптимизация:** для высокочастотных данных (HP, cooldown, чат-тайпинг) — bypass binding, прямая запись в элемент, троттлинг (≤10 Hz текст, per-frame только transforms).

### 5.6 Контекстные подсказки (interaction hints)

Агент-инструмент генерирует overlay-систему подсказок, появляющихся рядом с объектом (не в центре экрана). Использует `overlay` шаблон + world-to-screen позиционирование + fade-in 80 ms.

### 5.7 Rive state machines для «живого» UI

Rive — ключевой инструмент для анимированного векторного UI. Агент-инструмент `ui_create_rive_binding` генерирует C# controller:

```csharp
public class RiveLoadingController : MonoBehaviour
{
    [SerializeField] private RiveWidget _widget; // rive-unity
    private StateMachine _sm;
    private ViewModel _vm;

    void Start()
    {
        var file = _widget.LoadFile("LoadingScreen.riv");
        var artboard = file.Artboard("Main");
        _sm = artboard.StateMachine("LoadingSM");
        _vm = artboard.ViewModel("LoadingVM");
    }

    public void SetProgress(float p) => _vm.SetNumber("progress", p);
    public void SetState(bool loading) => _sm.SetBool("isLoading", loading);
}
```

**Применения:** loading screens (idle/loading/complete; data-bound progress), animated icons (health, ammo, cooldown), transition wipes, data-bound micro-interactions (button hover/press, toggle, slider), меню с complex motion (Genshin-style).

**Когда Rive vs Unity:** Rive — векторная анимация, state machines, data-bound motion, малый размер файлов, designer-friendly. Unity (DOTween/USS transitions) — layout-driven анимации, простые твины. Rive **не заменяет** layout-систему — дополняет.

---

## 6. Примеры agent-driven UI creation

### 6.1 Figma/FigCraft → Unity (полный цикл)

**Сценарий:** дизайнер создал экран инвентаря в FigCraft (компоненты, варианты, переменные Light/Dark). Агент автоматизирует импорт.

```
1. [Agent] Запускает headless FigCraft: figcraft-cli mcp
2. [Agent] variable.export {format: "json+css"} → tokens.json + tokens.css
3. [Agent] export {layers: icons, format: "png", scale: 2} → /tmp/icons/
4. [Agent] inspect.code {format: "css"} → css-output.css
5. [Agent] codeConnect.export → figcraft.connect.json
6. [Agent] Вызывает ui_import_tokens(tokens.json) → DesignTokens.uss + ThemeData.asset
7. [Agent] Вызывает ui_import_svg_icons(/tmp/icons/) → sprite atlas + USS refs
8. [Agent] Вызывает ui_generate_screen(name=Inventory, template=fullscreen, connect=figcraft.connect.json)
   → Inventory.uxml + Inventory.uss + InventoryViewModel.cs
9. [Agent] Вызывает ui_audit_layout(Inventory.uxml) → отчёт
10. [Agent] Вызывает ui_check_perf(Inventory) → отчёт
11. [Пользователь] Открывает UI Builder, review, Play Mode test, visual diff vs FigCraft screenshot
12. [Agent] При изменении дизайна: re-export → importer → diff → review → commit
```

**Инструменты:** `ui_import_tokens`, `ui_import_svg_icons`, `ui_generate_screen`, `ui_audit_layout`, `ui_check_perf`.

**Время:** ручной импорт — 2–4 часа; автоматизированный — 5–10 минут + review.

### 6.2 Rive → Unity (data-bound loading screen)

**Сценарий:** дизайнер создал `.riv` loading screen со state machine `LoadingSM` (states: idle/loading/complete) и ViewModel `LoadingVM` (properties: progress, message).

```
1. [Пользователь] Создаёт LoadingScreen.riv в Rive editor (state machine + data binding)
2. [Agent] ui_import_rive(path=LoadingScreen.riv) → копия в Assets + C# loader stub
3. [Agent] ui_create_rive_binding(riv=LoadingScreen.riv, artboard=Main, sm=LoadingSM, vm=LoadingVM,
   bindings={progress: float, message: string, isLoading: bool})
   → RiveLoadingController.cs + UXML placeholder + wiring инструкции
4. [Agent] ui_generate_screen(name=Loading, template=fullscreen, content=rive_placeholder)
   → Loading.uxml (с VisualElement class="rive-container")
5. [Пользователь] Wiring: game code вызывает controller.SetProgress(0.42f)
6. [Пользователь] Play Mode test
```

**Выход (RiveLoadingController.cs):**
```csharp
public class RiveLoadingController : MonoBehaviour
{
    [SerializeField] private RiveWidget _widget;
    private StateMachine _sm;
    private ViewModel _vm;

    void Start()
    {
        var file = _widget.LoadFile("LoadingScreen.riv");
        var artboard = file.Artboard("Main");
        _sm = artboard.StateMachine("LoadingSM");
        _vm = artboard.ViewModel("LoadingVM");
    }

    public void SetProgress(float p) => _vm.SetNumber("progress", p);
    public void SetMessage(string m) => _vm.SetString("message", m);
    public void SetLoading(bool b) => _sm.SetBool("isLoading", b);
}
```

### 6.3 HTML/CSS → UXML/USS (механический перевод)

**Сценарий:** у вас есть HTML/CSS-прототип (или экспорт из FigCraft `inspect.code` в CSS/HTML). Агент переводит в UXML/USS.

**Правила перевода (agent skill `ui-tooling-agent`):**

| HTML/CSS | UXML/USS |
|---|---|
| `<div class="card">` | `<ui:VisualElement class="card">` |
| `<span>` / `<p>` | `<ui:Label>` |
| `<button>` | `<ui:Button>` |
| `<input>` | `<ui:TextField>` / `<ui:IntegerField>` |
| `display: flex; flex-direction: column` | `flex-direction: column;` |
| `gap: 16px` | `gap: 16px;` (Unity 6) |
| `padding: 16px` | `padding: 16px;` |
| `border-radius: 8px` | `border-radius: 8px;` |
| `background-color: #1a1d24` | `background-color: #1a1d24;` → заменить на `var(--color-surface)` |
| `box-shadow: 0 4px 16px rgba(0,0,0,0.3)` | child-ореол (ui_create_halo) |
| `backdrop-filter: blur(8px)` | frosted-glass shader (ui_generate_shader type=glass) |
| `transition: all 0.2s ease` | USS transition (только transform/opacity) или DOTween |
| `@media (max-width: 768px)` | класс `.compact` на root + USS overrides |
| `grid-template-columns: repeat(4, 1fr)` | вложенный flex (row с 4 дочерними flex-grow:1) |
| `position: absolute` | `position: absolute;` |
| `z-index` | порядок в дереве (последний — сверху) |

**Процесс:**
```
1. [Agent] Принимает HTML/CSS (из FigCraft inspect.code или от пользователя)
2. [Agent] ui_generate_screen с template=none, source=html_css (парсинг в UXML/USS)
3. [Agent] Заменяет хардкод-цвета/отступы на var(--*) через ui_apply_token_set
4. [Agent] Обнаруживает неподдерживаемые фичи (box-shadow, backdrop-filter, grid) → генерирует обходы
5. [Agent] ui_audit_layout → отчёт
6. [Пользователь] UI Builder review
```

**Ограничение:** автоматический перевод — ~80% механический; остальные 20% (эффекты, анимации, responsive) требуют ручного review.

### 6.4 Shader generation example (glass panel)

**Сценарий:** создать glassmorphism-панель для модального окна.

```
1. [Agent] ui_generate_shader(
     effect_type="glass",
     params={ blur_radius: 8, tint: "var(--color-surface)", tint_strength: 0.12,
              noise: 0.02, edge_highlight: true },
     target="Assets/_Project/UI/Materials/GlassModal.mat",
     uss_class="glass-modal"
   )
   → UI_Glass.shadergraph + GlassModal.mat + USS класс .glass-modal

2. [Agent] ui_create_glass_panel(
     blur=8, tint=0.12, edge=true,
     uxml_parent="ModalContainer",
     uss_class="glass-modal"
   )
   → добавляет VisualElement с class="glass-modal" в ModalContainer.uxml

3. [Agent] ui_audit_layout(ModalContainer.uxml) → проверка
4. [Пользователь] UI Builder review, Play Mode test, проверка blur perf
```

**Выход (USS):**
```css
.glass-modal {
  unity-material: resource("GlassModal");
  background-color: rgba(26, 29, 36, 0.12);
  border-width: 1px;
  border-color: rgba(255, 255, 255, 0.18);
  border-radius: var(--radius-lg);
}
```

### 6.5 Living UI example (адаптивный HUD)

**Сценарий:** создать адаптивный HUD с контекстами combat/exploration.

```
1. [Agent] ui_generate_screen(name=Hud, template=hud) → Hud.uxml + Hud.uss + HudController.cs
2. [Agent] ui_create_adaptive_hud(
     contexts=[combat, exploration, dialog],
     elements={
       combat: [health-bar, ammo-counter, ability-icons, crosshair],
       exploration: [minimap, quest-tracker, compass, health-bar],
       dialog: [dialog-box]
     }
   )
   → AdaptiveHudController.cs + классы .hud-combat/.hud-explore/.hud-dialog
3. [Agent] ui_create_viewmodel(name=HudViewModel, properties=[health, ammo, questProgress])
   → HudViewModel.cs
4. [Agent] ui_create_micro_interaction(element=crosshair, interaction=hover, effect=scale+glow)
   → USS :hover transition
5. [Agent] ui_check_perf(Hud) → отчёт (draw calls, vertex budget)
6. [Пользователь] Play Mode test, переключение контекстов
```

---

## 7. Реализация agent tooling (дорожная карта)

### 7.1 Фаза A — Инфраструктура (1 неделя)

- [ ] Создать `Assets/_Project/Scripts/Editor/UIAgentTools/` с базовой регистрацией custom MCP tools.
- [ ] Реализовать `ui_generate_screen` (шаблоны: modal, hud, fullscreen, overlay, panel, toast).
- [ ] Реализовать `ui_generate_component` (button, card, slot, input, label).
- [ ] Создать шаблоны UXML/USS в `Assets/_Project/UI/Templates/`.
- [ ] Создать скилл `ui-tooling-agent` в `.opencode/skills/`.

### 7.2 Фаза B — Токены и стили (1 неделя)

- [ ] Реализовать `ui_scaffold_theme` + `ui_import_tokens` (FigCraft/Figma export → USS + ScriptableObject).
- [ ] Реализовать `ui_apply_token_set`.
- [ ] Drift detection (хэш-проверка в CI).
- [ ] Создать скилл `ui-living`.

### 7.3 Фаза C — Эффекты (1–2 недели)

- [ ] Шаблоны Shader Graph: glass, glow, progress_radial, progress_bar, wipe, chromatic, halo.
- [ ] Реализовать `ui_generate_shader`.
- [ ] Реализовать `ui_create_halo`, `ui_create_glass_panel`.
- [ ] Реализовать `ui_generate_vfx` (VFX Graph templates).
- [ ] Создать скилл `ui-effect-shader`.

### 7.4 Фаза D — Анимация (1 неделя)

- [ ] Реализовать `ui_create_rive_binding` (rive-unity integration).
- [ ] Реализовать `ui_create_transition`, `ui_create_micro_interaction`.
- [ ] Создать скилл `ui-rive-binding`.

### 7.5 Фаза E — Living UI (1–2 недели)

- [ ] Реализовать `ui_create_flow_controller`, `ui_create_viewmodel`.
- [ ] Реализовать `ui_create_adaptive_hud`, `ui_create_progressive_disclosure`.
- [ ] Реализовать `ui_create_contextual_hint`.

### 7.6 Фаза F — Аудит (1 неделя)

- [ ] Реализовать `ui_audit_layout` (Yoga-готчи, фиксированные размеры).
- [ ] Реализовать `ui_check_perf` (draw calls, vertex budget, GC).
- [ ] Реализовать `ui_check_accessibility` (контраст, tap targets, фокус).
- [ ] Создать скилл `ui-perf-audit`.

### 7.7 Фаза G — Импорт (1–2 недели)

- [ ] Реализовать `ui_import_tokens` (FigCraft/Figma → USS + ScriptableObject).
- [ ] Реализовать `ui_import_rive`, `ui_import_svg_icons`.
- [ ] Реализовать `ui_export_tokens` (round-trip).
- [ ] Интеграция с FigCraft MCP/CLI (headless export в CI).

**Итого:** ~8–10 недель для полного toolset. Milestone 1 (инструментарий рядом, без поломок основного проекта) — фазы A–B; Milestone 2 (применение к реальному UI) — только после явного «go».

---

## 8. Принципы (жёсткие, из AGENTS.md)

1. **Не писать `.meta`/`.asmdef` руками** — Unity генерирует сам. Нужен `.asmdef` — отдельный план с одобрением.
2. **Не трогать** `Library/`, `Temp/`, `Builds/`, `UserSettings/`, `Logs/`, `obj/`, `bin/`, `node_modules/`, `.agents/`, `graphify-out/`, `docs/gdd/`, `docs/WORLD_LORE_BOOK.md`, `src/`.
3. **Спросить перед** `ProjectSettings/*`, `Packages/manifest.json`, `.gitignore`, `git push`, `git reset --hard`, `git stash drop`.
4. **Не запускать** `run_tests` MCP и Build через MCP — это делает пользователь.
5. **Не запускать Play Mode** — агент не делает скриншоты и не запускает Play Mode (см. `dont-start-play`, `manual-playtest-only`).
6. **Минимальные диффы** — изменения только относящиеся к задаче, без посторонних форматирований.
7. **Коммиты** — только через Git CLI, с тикетом в сообщении; никогда не push.
8. **Floating Origin** — новый контент под корнями `WorldScene_*` едет со сдвигом бесплатно; скрипты, кэширующие мировые `Vector3`, и runtime-спавн вне корней — по пайплайну (`docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md`).

---

## 9. Источники

### Внутренние (проект)
- `docs/UI/Impruvements_research/00_README.md` — навигация по research-папке.
- `docs/UI/Impruvements_research/03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` — архитектура (Screen/Window/Panel/Widget, UIFrame, MVVM).
- `docs/UI/Impruvements_research/05_REVERSE_ENGINEERING_NOTES.md` — UITK internals, batching, Figma connector.
- `docs/UI/Impruvements_research/06_PERFORMANCE_GUARDRAILS.md` — бюджеты производительности.
- `docs/UI/Impruvements_research/07_DESIGN_TOOLING_AND_FIGMA_FREE.md` — Figma Free vs альтернативы.
- `docs/UI/Impruvements_research/08_RENDERING_APPROACHES.md` — рендеринг-подходы (Rive, Noasis, Coherent, WebView, shaders, VFX).
- `docs/UI/Impruvements_research/09_DESIGN_PATTERNS.md` — AAA-паттерны (living UI, halos, responsive, micro-interactions).
- `docs/UI/Impruvements_research/10_TOOLING_PIPELINE.md` — пайплайн FigCraft → Unity.
- `docs/UI/UI_TOOLKIT_GUIDE.md` — Yoga gotchas, UIDocument lifecycle.
- `AGENTS.md` — правила проекта, HARD RULES, MCP routing.
- `.opencode/skills/unity-mcp-orchestrator/references/tools-reference.md` — MCP tools reference (custom tools, batch_execute).

### Внешние (MCP / Unity / инструменты)
- mcp-for-unity: https://github.com/CoplayDev/unity-mcp (custom tools via `execute_custom_tool`, `mcpforunity://custom-tools`).
- Unity CLI custom commands: `[CliCommand]` in `Unity.Pipeline.Commands` (unity-cli skill, `integration-advanced.md`).
- Unity UI Toolkit docs: https://docs.unity3d.com/6000.6/Documentation/Manual/UIElements.html
- Unity UI Builder: https://docs.unity3d.com/6000.6/Documentation/Manual/UIBuilder.html
- Unity Shader Graph (URP): https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/
- Unity VFX Graph: https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/
- Rive Unity runtime: https://github.com/rive-app/rive-unity
- Rive data binding: https://rive.app/blog/rive-as-a-lottie-alternative
- FigCraft: https://github.com/storytold/figcraft (MCP 400+ commands, headless/attached, variable.export, codeConnect)
- Unity runtime bindings: https://docs.unity3d.com/6000.6/Documentation/Manual/UIE-runtime-binding.html

### AAA / GDC (паттерны)
- Bungie, "Building the user interface for Destiny" (GDC 2016) — progressive disclosure, item-centric focus.
- CDPR, "User Interface in Cyberpunk 2077" (GDC Showcase 2023) — budgets, pooling, thousands of instances.
- Riot, "Art Directing a Consistent and Scalable Interactive Brand Identity" (GDC 2017) — "Play" design system.
- miHoYo, Genshin Impact (GDC 2021) — living UI, progressive disclosure.
- Epic, "Optimizing UE4 for Fortnite" (GDC 2018) — configurable HUD, cross-platform.
- Sybo Games, "Building a Unified Cross-Project UI Framework" (GDC 2019) — shared component library.
- DICE LA, "Art Direction for AAA UI" (GDC 2018).

### Дизайн-инструменты
- FigCraft: https://github.com/storytold/figcraft
- Figma Free/Starter: https://www.figma.com/pricing
- Penpot: https://penpot.app
- Lunacy: https://icons8.com/lunacy
- ProtoPie × Riot: https://www.protopie.io/blog/revolutionizing-game-ux-ui-design-riot-games

---

## 10. Чек-лист для нового UI-экрана (agent workflow)

При создании любого нового UI-экрана агент следует этому чек-листу:

1. **Определить тип экрана** → выбрать шаблон (modal/hud/fullscreen/overlay/panel/toast).
2. **Сгенерировать структуру** → `ui_generate_screen`.
3. **Сгенерировать компоненты** → `ui_generate_component` для каждого переиспользуемого виджета.
4. **Применить токены** → `ui_apply_token_set` (никаких хардкод-цветов).
5. **Добавить эффекты** → `ui_create_halo`, `ui_create_glass_panel`, `ui_generate_shader` (по необходимости).
6. **Добавить анимацию** → `ui_create_micro_interaction`, `ui_create_transition` (USS transitions или DOTween).
7. **Создать ViewModel** → `ui_create_viewmodel` + wiring bindings.
8. **Аудит** → `ui_audit_layout` + `ui_check_perf` + `ui_check_accessibility`.
9. **Review** → пользователь открывает UI Builder, проверяет визуал.
10. **Play Mode test** → пользователь (не агент).
11. **Commit** → агент коммитит через Git CLI с тикетом.

**Запрещено:** запускать Play Mode, делать скриншоты, изменять `ProjectSettings/*` без одобрения, писать `.meta`/`.asmdef` руками.

---

*Документ — research; реализация toolset — через дорожную карту §7 и согласование с пользователем. Milestone 1 — инструментарий рядом (sandbox), без поломок основного проекта. Milestone 2 (применение к реальному UI) — только после явного «go».*