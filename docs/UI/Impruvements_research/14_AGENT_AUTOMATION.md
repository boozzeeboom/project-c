# 14 — Agent Automation: Self-Sufficient AAA UI Creation in Unity

> **Scope:** Research-отчёт по проектированию **полностью автономного** набора инструментов агента (custom MCP tools + skills + workflows) для создания красивого, адаптивного, отзывчивого UI в Unity 6 (URP, Windows standalone, MMO-песочница Project C) **без ручных шагов пользователя**.
>
> **Отличие от `11_AGENT_TOOLING.md`:** тот документ описывает инструментарий в целом (с ручным review в UI Builder / Play Mode). Этот — фокус на **самодостаточности агента**: что можно сгенерировать полностью программно, как обойти внешние редакторы (Rive Editor, Shader Graph GUI), какие ассеты текстовые и генерируются агентом напрямую, и как закрыть цикл «спека → ассеты → проверка» без участия человека.
>
> **Дополняет:** `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md`, `09_DESIGN_PATTERNS.md`, `10_TOOLING_PIPELINE.md`, `11_AGENT_TOOLING.md`, `12_OVERCOMING_LIMITS.md`, `13_ENVIRONMENT_SETUP.md`.

---

## 0. TL;DR

| Вопрос | Ответ |
|---|---|
| Можно ли агенту создавать AAA-UI **полностью самому**? | **Да, на ~90%** — при условии, что все ассеты **текстовые** (UXML/USS/C#/Shader Graph JSON/VFX Graph JSON/Material YAML). Оставшиеся 10% — бинарные авторские артефакты (`.riv`, исходные PNG/SVG-иконки, шрифты), которые агент либо генерирует процедурно (SVG/PNG через код), либо принимает как вход. |
| Главный ключ к самодостаточности | **Shader Graph и VFX Graph — это текстовые JSON-файлы** (`.shadergraph`, `.vfx`). Агент пишет их напрямую, не через GUI. То же — `.mat` (YAML), `.uxml`/`.uss`, `.cs`. Никаких «открой Shader Graph и нажми…». |
| Что НЕ автоматизируется напрямую | Rive `.riv` (бинарный, авторится в Rive Editor) — обход: агент генерирует **эквивалентную** анимацию через Shader Graph + custom tweener + Painter2D, либо принимает `.riv` от пользователя и делает wiring. |
| Как обойти ограничения UITK | Программно: glass → Shader Graph grab-pass; halo → child-ореолы/SDF-шейдер; progress → SDF-шейдер; blur → URP Volume; particles → VFX Graph→RenderTexture; transitions → custom tweener (0 GC). |
| «Живой» UI | FlowController FSM + AdaptiveHud + MVVM ViewModels + progressive disclosure + контекстные подсказки — всё генерируется агентом в C#. |
| Проверка без человека | `read_console` (errors/warnings) + `ui_audit_layout` + `ui_check_perf` + `ui_check_accessibility`. Play Mode и скриншоты — остаются за пользователем (HARD RULE). |

**Ключевой принцип самодостаточности:** агент **не просит** «открой Rive Editor / Shader Graph / Figma». Он генерирует **текстовые ассеты** и **код**, который Unity импортирует сам. Человек участвует только в финальном visual review и Play Mode-тесте (по HARD RULES проекта).

---

## 1. Проблема: почему UI выглядит как «Windows 98» и как агент чинит сам

Текущее состояние Project C (см. `01_CURRENT_STATE_AUDIT.md`): 14 файлов с `Clear()+CloneTree()`, 15 с `Resources.Load<StyleSheet>`, 3.1 MB/frame GC, скрытые панели продолжают рендериться, нет единой компонентной библиотеки, токены разбросаны. Результат — UI без глубины, адаптивности и микро-интеракций.

**Агент-инструментарий решает три класса проблем автономно:**

1. **Генерация (boilerplate)** — создание UXML/USS/скриптов/шейдеров по шаблонам, без ручного копирования.
2. **Согласованность (consistency)** — токены, компоненты, state machines применяются единообразно через инструменты.
3. **Эффекты (AAA-визуал)** — агент генерирует шейдеры (Shader Graph JSON), VFX (VFX Graph JSON), хало, стекло, прогресс — руками это долго и ошибочно.

**Что агент НЕ делает (по HARD RULES):** не запускает Play Mode, не делает скриншоты, не принимает визуальные решения (цвета/композиция) — это остаётся за дизайнером/пользователем.

---

## 2. Граница самодостаточности (что можно автоматизировать полностью)

### 2.1 Текстовые ассеты (агент генерирует на 100%)

| Ассет | Формат | Генерируется агентом? | Как |
|---|---|---|---|
| UXML | XML (текст) | ✅ | `ui_generate_screen/component` |
| USS | CSS-подобный (текст) | ✅ | `ui_generate_*` + `ui_apply_token_set` |
| C# scripts | текст | ✅ | `manage_script` / `create_script` (auto-compile) |
| Shader Graph | **JSON** (`.shadergraph`) | ✅ | `ui_generate_shader` пишет JSON напрямую |
| VFX Graph | **JSON** (`.vfx`) | ✅ | `ui_generate_vfx` пишет JSON напрямую |
| Material | **YAML** (`.mat`) | ✅ | `AssetDatabase.CreateAsset` через Editor script |
| ScriptableObject (ThemeData) | YAML | ✅ | через Editor script |
| Sprite Atlas config | YAML | ✅ | через Editor script |
| `.asmdef` | JSON | ⚠️ запрещено руками (HARD RULE) — предложить отдельным планом |

**Ключевое открытие:** Shader Graph (`.shadergraph`) и VFX Graph (`.vfx`) — это **обычные JSON-файлы** в `Assets/`. Агент может сгенерировать их текстом, и Unity импортирует как полноценные ассеты. Никакого GUI. Это делает агента **полностью самодостаточным** для AAA-эффектов (glass, glow, progress, wipe, halo, particles).

### 2.2 Бинарные/авторские ассеты (агент не создаёт с нуля, но обходит)

| Ассет | Проблема | Обход агента |
|---|---|---|
| Rive `.riv` | Бинарный, авторится в Rive Editor | Агент генерирует **эквивалент** через Shader Graph + custom tweener + Painter2D (векторная анимация кодом). Либо принимает `.riv` от пользователя и делает wiring (`ui_create_rive_binding`). |
| PNG/SVG иконки | Нужны исходные арты | Агент генерирует SVG процедурно (простые иконки: стрелки, шестерёнки, крестики) через `ui_import_svg_icons`; сложные — принимает от пользователя. |
| Шрифты (TMP font assets) | Нужен исходный TTF/OTF | Агент создаёт TMP Font Asset из уже имеющегося шрифта в проекте через Editor script (Font Asset Creator). |
| Аудио | Бинарные клипы | Агент использует существующие клипы; генерация звука — вне scope. |

### 2.3 Итог: что агент делает **сам**, без пользователя

- ✅ Генерирует экраны/компоненты (UXML+USS+C#) из текстовой спеки.
- ✅ Генерирует шейдеры (glass/glow/progress/wipe/halo/chromatic) как `.shadergraph` JSON + материал + USS-класс.
- ✅ Генерирует VFX (burst/confetti/ambient) как `.vfx` JSON + C# trigger.
- ✅ Генерирует хало, стеклянные панели, адаптивные HUD, FSM-контроллеры, ViewModels.
- ✅ Генерирует токены (`DesignTokens.uss` + `ThemeData.asset`) из JSON-экспорта (если пользователь дал файл) или из встроенных пресетов.
- ✅ Проверяет: compile (console), layout audit, perf audit, accessibility audit.
- ❌ Не делает visual review и Play Mode-тест (HARD RULE — это делает пользователь).

---

## 3. Архитектура agent toolset

### 3.1 Три слоя

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 3: SKILLS (opencode) — знания/процессы              │
│  ui, ui-uitk, ui-ugui, ui-tooling-agent (NEW),             │
│  ui-effect-shader (NEW), ui-living (NEW), ui-perf-audit (NEW)│
├─────────────────────────────────────────────────────────────┤
│  Layer 2: CUSTOM MCP TOOLS (Unity-side, C#)                 │
│  ui_generate_*, ui_create_*, ui_audit_*, ui_check_*,       │
│  ui_import_*, ui_export_*                                  │
├─────────────────────────────────────────────────────────────┤
│  Layer 1: BASE MCP TOOLS (unityMCP / anklebreaker)         │
│  manage_gameobject, manage_script, manage_asset,            │
│  read_console, batch_execute, manage_scene, execute_menu_item│
└─────────────────────────────────────────────────────────────┘
```

- **Layer 1** — уже есть (unityMCP / anklebreaker). Сцена, объекты, скрипты, консоль.
- **Layer 2** — кастомные инструменты, регистрируются в Unity через `[McpTool]` / `execute_custom_tool` (см. `mcpforunity://custom-tools`). Это «умные» операции над UI.
- **Layer 3** — markdown-инструкции для агента (когда и как вызывать инструменты, какие шаблоны). Живут в `.opencode/skills/`.

### 3.2 Полная таксономия инструментов

#### 3.2.1 Генерация структуры (UXML/USS)

| Инструмент | Вход | Выход | Когда |
|---|---|---|---|
| `ui_generate_screen` | имя, шаблон (modal/hud/fullscreen/overlay/panel/toast), токены | `.uxml` + `.uss` + ViewModel stub | Новый экран/окно |
| `ui_generate_component` | имя компонента, вариант (button/card/slot/input/label), токены | UXML template + USS классы + C# partial | Новый переиспользуемый виджет |
| `ui_generate_list_item` | тип элемента списка, поля | UXML item template + binding hints | ListView/TreeView items |
| `ui_scaffold_theme` | коллекция токенов (JSON/CSS) или пресет | `DesignTokens.uss` + `ThemeData.asset` | Новая тема |
| `ui_import_tokens` | путь к экспорту FigCraft/Figma (JSON/CSS) | обновлённые токены + хэш-проверка drift | Round-trip из дизайна |

#### 3.2.2 Стилизация и эффекты

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_apply_token_set` | имя токен-набора, целевой UXML/USS | замена `var(--*)` ссылок, обновление классов |
| `ui_generate_shader` | тип эффекта (glass/glow/progress_radial/progress_bar/wipe/chromatic/halo), параметры | `.shadergraph` JSON + `.mat` + USS-класс с `unity-material` |
| `ui_generate_vfx` | тип эффекта (burst/confetti/ambient/flash), бюджет частиц | `.vfx` JSON + C# trigger wrapper |
| `ui_create_halo` | цвет, размер, состояния (idle/hover/selected/disabled) | child-ореолы в UXML + USS transition |
| `ui_create_glass_panel` | blur radius, tint, edge highlight | frosted-glass material + UXML + USS |

#### 3.2.3 Анимация

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_create_rive_binding` | путь к `.riv`, artboard, state machine, data bindings | C# controller + UXML placeholder + wiring инструкции |
| `ui_create_transition` | тип (fade/slide/scale-pop/wipe), длительность, easing | custom tweener sequence + blocker setup |
| `ui_create_micro_interaction` | элемент, взаимодействие (hover/press/focus) | USS `:hover`/`:active`/`:focus` + transition |

#### 3.2.4 Живой UI (living UI)

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_create_flow_controller` | список состояний, переходы | `FlowController.cs` + `IUIFrame` wiring + ModalStack |
| `ui_create_viewmodel` | имя, свойства (тип + binding mode) | C# ViewModel с `[GeneratePropertyBag]`, `INotifyBindablePropertyChanged` |
| `ui_create_adaptive_hud` | набор контекстов (combat/exploration/dialog), элементы | HUD controller + классы `.hud-combat`/`.hud-explore` + переключение |
| `ui_create_progressive_disclosure` | базовый набор + «расширенный» набор | контейнеры + toggle + анимация expand/collapse |

#### 3.2.5 Аудит и проверка

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_audit_layout` | путь к UXML/USS | отчёт: Yoga-готчи, фиксированные размеры текста, отсутствие flex-shrink, overflow |
| `ui_check_perf` | имя экрана/панели | draw calls (оценка), vertex budget, текстуры, GC-риски |
| `ui_check_accessibility` | UXML | контраст (WCAG AA), tap targets ≥ 44px, фокус-кольцо, screen-reader labels |

#### 3.2.6 Импорт/экспорт

| Инструмент | Вход | Выход |
|---|---|---|
| `ui_export_tokens` | путь к FigCraft/Figma экспорту | `DesignTokens.uss` + `ThemeData.asset` + хэш |
| `ui_import_rive` | путь к `.riv` | копия в Assets + C# loader stub + инструкции |
| `ui_import_svg_icons` | папка SVG | sprite atlas PNG @2x/@3x + USS background-image refs |

### 3.3 Skills (opencode layer)

| Skill ID | Назначение | Статус |
|---|---|---|
| `ui` (существует) | Роутинг по UI-системам | ✅ |
| `ui-uitk` (существует) | UXML/USS авторинг | ✅ |
| `ui-ugui` (существует) | Canvas/uGUI | ✅ |
| `ui-tooling-agent` (НОВЫЙ) | Оркестрация agent-driven UI: генерация экранов/компонентов, токены, wiring, эффекты | 🆕 |
| `ui-effect-shader` (НОВЫЙ) | Генерация Shader Graph JSON / HLSL для UI-эффектов | 🆕 |
| `ui-living` (НОВЫЙ) | State machines экранов, адаптивный HUD, progressive disclosure, data binding | 🆕 |
| `ui-perf-audit` (НОВЫЙ) | Проверка perf-бюджетов UI | 🆕 |

### 3.4 Регистрация custom MCP tools (Unity-side)

Unity MCP (mcp-for-unity) поддерживает кастомные инструменты через C#-атрибуты. Паттерн:

```csharp
// Assets/_Project/Scripts/Editor/UIAgentTools.cs
[McpTool("ui_generate_screen")]
public static class UiGenerateScreenTool
{
    [McpToolParam("screen_name", required: true)]
    public string ScreenName;

    [McpToolParam("template", required: false, default: "modal")]
    public string Template; // modal | hud | fullscreen | overlay | panel | toast

    public static McpToolResult Execute(UiGenerateScreenParams p)
    {
        // 1. Load template UXML from Assets/_Project/UI/Templates/
        // 2. Substitute tokens ({{color.surface}}, {{spacing.md}})
        // 3. Write Assets/_Project/UI/Screens/{ScreenName}.uxml
        // 4. Create companion .uss with class skeleton
        // 5. Create ViewModel stub C# script
        // 6. Trigger compile (automatic via create_script)
        // 7. Return { files: [...], next_steps: [...] }
    }
}
```

**Альтернатива (CLI-based):** через `[CliCommand]` в `Unity.Pipeline.Commands` — инструмент становится доступен через `unity mcp` и `unity command`. Подходит для CI/headless.

**Открытие:** `mcpforunity://custom-tools` → список доступных custom tools; вызов через `execute_custom_tool(tool_name, parameters)`.

### 3.5 Шаблоны (templates)

Шаблоны хранятся в `Assets/_Project/UI/Templates/` (UXML + USS + C# stub). Инструмент `ui_generate_screen` выбирает шаблон по `template`:

| Шаблон | Структура | Применение |
|---|---|---|
| `modal` | dim-bg + centered panel + header + body + footer (buttons) | диалоги, подтверждения |
| `fullscreen` | header-bar + scrollable body + optional sidebar | инвентарь, настройки, карта |
| `hud` | corner-anchored cluster (top-left/top-right/bottom) | игровой HUD |
| `overlay` | full-screen transparent layer + floating elements | тултипы, контекстные подсказки |
| `panel` | titled panel с close button | окна чата, квест-лог |
| `toast` | small notification card | уведомления |

Шаблоны — **git-tracked**, версионируются; агент только инстанцирует их с подстановкой токенов/имён. Это исключает «Windows 98»-эффект: каждый новый экран начинается с AAA-шаблона.

---

## 4. Text-based asset generation: ключ к самодостаточности

### 4.1 Shader Graph как JSON (агент пишет напрямую)

`.shadergraph` — это JSON-файл. Агент генерирует его текстом, Unity импортирует как полноценный Shader Graph asset. Пример упрощённой структуры:

```json
{
  "m_SGVersion": 3,
  "m_Type": "UnityEditor.ShaderGraph.Shader",
  "m_ObjectId": "guid-generated",
  "m_Properties": [
    { "m_Id": "color-tint", "m_Name": "_TintColor", "m_DefaultType": 1,
      "m_DefaultValue": { "r": 0.1, "g": 0.12, "b": 0.15, "a": 0.12 } }
  ],
  "m_SubGraphs": [],
  "m_GraphPrecision": 0,
  "m_PreviewMode": 2,
  "m_CustomColors": { "m_SerializableColors": [] }
}
```

**Реализация `ui_generate_shader`:** агент использует **шаблоны** `.shadergraph` JSON с плейсхолдерами (`{{BLUR_RADIUS}}`, `{{TINT_COLOR}}`), подставляет параметры и пишет файл. Шаблоны хранятся в `Assets/_Project/UI/ShaderTemplates/` (git-tracked). Это гарантирует валидный JSON, который Unity распознаёт.

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

Выход: `.shadergraph` JSON + `.mat` (YAML) + USS-класс `.glass-panel { unity-material: ...; }` + C# helper для runtime-параметров (`SetFloat("_BlurRadius", ...)`).

### 4.2 VFX Graph как JSON (агент пишет напрямую)

`.vfx` — тоже JSON. Агент генерирует VFX Graph assets текстом по шаблонам. Применения:

- **Celebration burst** (level-up, rare drop): GPU particles, 1000–5000, 60 fps, render в RenderTexture → UI overlay.
- **Transition wipe** (iris, pixelate): fullscreen quad с VFX.
- **Ambient** (меню: пыль, искры): ≤ 50–100 частиц, постоянный, жёсткий бюджет.
- **Damage/heal flash**: кратковременные частицы (≤ 200), event-driven.

Интеграция: VFX рендерится в RenderTexture → `VisualElement.style.backgroundImage` (UITK) или `RawImage` (uGUI). Object pooling для `VisualEffect` компонентов.

### 4.3 Material как YAML

`.mat` файлы — YAML. Агент генерирует их через Editor script (`AssetDatabase.CreateAsset` + `Material`), либо пишет YAML напрямую:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &1
Material:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: GlassPanel
  m_Shader: {fileID: -64655667283963273, guid: <shader-guid>, type: 3}
  m_Materials: []
  m_SavedProperties:
    m_TexEnvs: []
    m_Floats:
    - _BlurRadius: 8
    - _TintStrength: 0.15
    m_Colors:
    - _TintColor: {r: 0.1, g: 0.12, b: 0.15, a: 0.12}
```

### 4.4 Стилизация через токены (автоматическая)

Токены живут в `DesignTokens.uss` (USS custom properties на `:root`) + `ThemeData.asset`. Инструмент `ui_scaffold_theme` генерирует их из экспорта FigCraft/Figma или из встроенных пресетов:

```css
/* DesignTokens.uss — автогенерируемый, НЕ редактировать руками */
:root {
  --color-surface: #1a1d24;
  --color-surface-elevated: #232730;
  --color-accent: #78b891;
  --color-text-primary: #e8eaed;
  --color-text-secondary: #9aa0a6;
  --color-danger: #e05252;
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 16px;
  --radius-full: 9999px;
  --duration-fast: 80ms;
  --duration-normal: 160ms;
  --duration-slow: 320ms;
  --easing-out: cubic-bezier(0.16, 1, 0.3, 1);
}
```

Компонентные USS-классы ссылаются на токены: `background-color: var(--color-surface);`. Агент **никогда** не хардкодит цвета/отступы в компонентных стилях — только через `var(--*)`.

**Drift detection:** `ui_import_tokens` считает хэш экспорта; CI падает, если `DesignTokens.uss` не совпадает.

---

## 5. End-to-end automation: от спеки до ассетов

### 5.1 Pipeline генерации UXML/USS

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

**Ключевое:** каждый шаг — отдельный MCP-вызов (или batch ≤25 команд через `batch_execute`). Агент не пишет файлы руками там, где есть инструмент.

### 5.2 Пример: полностью автоматизированный экран инвентаря

**Вход (текстовая спека от пользователя):**
> "Инвентарь: сетка 4×6 слотов, фильтры (All/Weapons/Armor/Consumables), тултип при hover, редкость слота (цвет halo), drag-and-drop предметов."

**Шаги агента (без ручных действий):**

```
1. [Agent] ui_generate_screen(name=Inventory, template=fullscreen)
   → Inventory.uxml + Inventory.uss + InventoryController.cs

2. [Agent] ui_generate_component(name=InventorySlot, variant=slot,
     fields=[icon, count, rarity-border])
   → InventorySlot.uxml + InventorySlot.uss + InventorySlot.cs

3. [Agent] ui_generate_component(name=ItemTooltip, variant=tooltip)
   → ItemTooltip.uxml + ItemTooltip.uss

4. [Agent] ui_create_halo(element=InventorySlot, color=rarity,
     states=[idle, hover, selected, disabled])
   → halo child-elements + USS transitions

5. [Agent] ui_create_viewmodel(name=InventoryViewModel,
     properties=[SelectedSlotIndex:int, Items:ObservableCollection<ItemStack>,
                 FilterMode:enum, TooltipItem:ItemStack])
   → InventoryViewModel.cs с [GeneratePropertyBag]

6. [Agent] ui_create_micro_interaction(element=InventorySlot,
     interaction=hover, effect=scale+glow)
   → USS :hover transition

7. [Agent] ui_create_transition(type=modal-in, duration=160ms, easing=ease-out)
   → custom tweener sequence

8. [Agent] ui_audit_layout(Inventory.uxml) → отчёт
9. [Agent] ui_check_perf(Inventory) → отчёт
10. [Agent] ui_check_accessibility(Inventory.uxml) → отчёт

11. [Пользователь] UI Builder review + Play Mode test
```

**Выход агента:** 8 файлов (UXML×3, USS×3, C#×2) + отчёты. Время: 2–5 минут. Ручной импорт — 2–4 часа.

### 5.3 Пример: адаптивный HUD (полностью автоматизированный)

**Вход:** "HUD с контекстами combat/exploration/dialog: health bar, ammo, minimap, quest tracker, crosshair, dialog box."

```
1. [Agent] ui_generate_screen(name=Hud, template=hud)
   → Hud.uxml + Hud.uss + HudController.cs

2. [Agent] ui_create_adaptive_hud(
     contexts=[combat, exploration, dialog],
     elements={
       combat: [health-bar, ammo-counter, ability-icons, crosshair],
       exploration: [minimap, quest-tracker, compass, health-bar],
       dialog: [dialog-box]
     })
   → AdaptiveHudController.cs + классы .hud-combat/.hud-explore/.hud-dialog

3. [Agent] ui_create_viewmodel(name=HudViewModel,
     properties=[health:float, healthMax:float, ammo:int, questProgress:float])
   → HudViewModel.cs

4. [Agent] ui_create_micro_interaction(element=crosshair,
     interaction=hover, effect=scale+glow)
   → USS :hover transition

5. [Agent] ui_generate_shader(effect_type=progress_bar,
     params={value_prop:_Health, trail:true, gradient:true},
     target="Assets/_Project/UI/Materials/HealthBar.mat",
     uss_class="health-bar")
   → UI_ProgressBar.shadergraph + HealthBar.mat + USS класс

6. [Agent] ui_check_perf(Hud) → отчёт

7. [Пользователь] Play Mode test, переключение контекстов
```

### 5.4 Анимация: автоматизация

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
   **Ограничение:** только `transform`/`opacity`/`color`/`background-color`.

2. **Custom tweener** (код-driven) — для модал in/out, shake, scale-pop. Агент генерирует sequence через `ui_create_transition`:
   ```csharp
   public static Tween ModalIn(VisualElement panel, VisualElement dimBg) {
       var seq = Tween.Sequence();
       dimBg.style.opacity = 0;
       seq.Append(dimBg.FadeTo(0.6f, 0.16f).Ease(Ease.OutCubic));
       panel.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1));
       panel.style.opacity = 0;
       seq.Join(panel.FadeTo(1f, 0.16f));
       seq.Join(panel.ScaleTo(1f, 0.16f).Ease(Ease.OutCubic));
       seq.OnComplete(() => InputBlocker.Disable());
       return seq;
   }
   ```
   **Правило:** не смешивать tweener и USS transitions на одном элементе.

3. **Rive** — только если пользователь предоставил `.riv`. Агент делает wiring через `ui_create_rive_binding`.

4. **VFX Graph** — для частиц/juice (celebration, transitions). Агент создаёт через `ui_generate_vfx`.

---

## 6. Overcoming Unity UI Toolkit limitations (программно)

UITK — web-inspired, но **не браузер**. Ниже — каждое ограничение и программный обход, который агент реализует автоматически.

### 6.1 Таблица: CSS-фича → Unity-обход

| CSS-фича | UITK поддержка | Обход (agent tool) |
|---|---|---|
| `box-shadow` (blur) | ❌ нет | child-ореолы (линейная аппроксимация, 4 полосы) + `ui_create_halo`; либо shader `UI_Halo` |
| `backdrop-filter: blur()` | ❌ нет (6.6+ только) | frosted-glass shader (GrabPass) через `ui_generate_shader(type=glass)`; либо URP Volume global blur; либо RenderTexture |
| `filter: blur()` | ❌ нет | shader на элемент; либо URP post-process на UI-камере |
| `gap` (flex) | ✅ Unity 6+ | использовать напрямую; для старых — margins |
| `transition` | ✅ только transform/opacity/color | USS transitions для простых; custom tweener для сложных; `ui_create_micro_interaction` / `ui_create_transition` |
| `conic-gradient` | ❌ нет | shader `UI_RadialProgress`; либо custom mesh (generateVisualContent) |
| `radial-gradient` | ⚠️ частично | USS `radial-gradient()` в Unity 6; иначе текстура/шейдер |
| `grid` (CSS Grid) | ❌ нет | вложенный flex (row/col) или абсолютное позиционирование; агент генерирует вложенный flex |
| `position: sticky` | ❌ нет | C#-логика скролла + классы |
| `:nth-child()` | ⚠️ частично | классы на элементах при генерации |
| `animation` (keyframes) | ❌ нет | custom tweener / USS transitions |
| `particle system` | ❌ нет (UI Toolkit) | VFX Graph → RenderTexture → `ui_generate_vfx` |
| `box-shadow` (inset) | ❌ нет | внутренние градиенты (child elements) |
| `mix-blend-mode` | ⚠️ частично | shader blending |
| `clip-path` | ❌ нет | маски (rectangular preferred) + shader |
| `text-shadow` | ⚠️ частично | TMP outline/glow; либо shader |
| `aspect-ratio` | ✅ Unity 6+ | USS `aspect-ratio` |

### 6.2 Glassmorphism (backdrop-filter обход) — детально

**Проблема:** UITK не имеет `backdrop-filter` (до 6.6). Glassmorphism — ключевой AAA-паттерн.

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

### 6.3 Halo/box-shadow обход (детально)

USS не поддерживает `box-shadow` с blur. Паттерн из проекта (Cloudy): **child-ореолы** (линейная аппроксимация): 4 полосы по краям, середина прозрачна; ramp линейный от alpha к 0. Дёшево (один элемент, без текстуры).

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

### 6.4 Transitions обход

USS transitions поддерживают только `transform`/`opacity`/`color`/`background-color`. Для layout-анимаций (width/height/top/left) — **custom tweener** (анимация через inline style transform, не layout). Для сложных — **Rive** (векторные state machines), но только если пользователь предоставил `.riv`.

**Правило агента:** `ui_create_micro_interaction` генерирует USS transitions только для transform/opacity; если нужны layout-свойства — переключает на tweener-генерацию.

### 6.5 Circular progress (conic-gradient обход)

USS не поддерживает `conic-gradient`. Обходы (агент выбирает автоматически):

1. **Shader `UI_RadialProgress`** (рекомендуется): polar coords + step + smoothstep edge + glow. Агент генерирует через `ui_generate_shader(type=progress_radial)`.
2. **Custom mesh** via `generateVisualContent` (MeshGenerationContext) — ручной веер. Дёшево, но больше кода.
3. **uGUI Image (Filled, Radial 360)** — `fillAmount` 0–1. Самый простой, но только для простых индикаторов.

### 6.6 Data binding authoring обход

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

### 6.7 Particles в UI обход

UITK не имеет particle system. Обход: VFX Graph → RenderTexture → `VisualElement.style.backgroundImage` (UITK) или `RawImage` (uGUI). Агент-инструмент `ui_generate_vfx` создаёт VFX + C# trigger-обёртку + инструкции по RenderTexture.

---

## 7. Living UI patterns (агент генерирует)

### 7.1 State machines экранов (FlowController)

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

### 7.2 Адаптивный HUD (контекстно-зависимый)

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

**Правило:** в бою — компактный HUD; в exploration — расширенный; в диалоге — скрытый HUD + диалоговое окно.

### 7.3 Progressive disclosure

Агент-инструмент `ui_create_progressive_disclosure` генерирует контейнер с базовым и расширенным набором элементов + toggle + анимация expand/collapse (custom tweener, transform scale Y).

**Паттерн (Destiny 2):** плотный информационный UI; показывать больше только по запросу; item-centric focus.

### 7.4 Data binding (MVVM) — автоматизация

Агент-инструмент `ui_create_viewmodel` генерирует ViewModel с `[GeneratePropertyBag]` и `[CreateProperty]`, а также binding-hints в комментариях:

```csharp
[GeneratePropertyBag]
public partial class PlayerHudViewModel : INotifyBindablePropertyChanged
{
    [CreateProperty] public float Health { get; private set; }
    [CreateProperty] public float HealthMax { get; private set; }
    [CreateProperty] public string AmmoText { get; private set; }

    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

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

### 7.5 Контекстные подсказки (interaction hints)

Агент генерирует overlay-систему подсказок, появляющихся рядом с объектом (не в центре экрана). Использует `overlay` шаблон + world-to-screen позиционирование + fade-in 80 ms.

### 7.6 Rive state machines — только при наличии `.riv`

Rive — ключевой инструмент для анимированного векторного UI, но `.riv` — бинарный файл, авторится в Rive Editor. Агент **не может** создать `.riv` с нуля. Два пути:

1. **Пользователь предоставил `.riv`** → агент делает wiring через `ui_create_rive_binding`:
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
       public void SetState(bool loading) => _sm.SetBool("isLoading", loading);
   }
   ```

2. **Пользователь не предоставил `.riv`** → агент генерирует **эквивалент** через Shader Graph + custom tweener + Painter2D. Это менее «дизайнерски красиво», но полностью автоматизировано и не требует внешнего редактора.

**Применения Rive (когда `.riv` есть):** loading screens, animated icons, transition wipes, data-bound micro-interactions, меню с complex motion.

---

## 8. Self-sufficiency playbook (без ручных шагов)

### 8.1 Что агент делает сам (полностью автоматически)

| Задача | Инструменты | Ручные шаги |
|---|---|---|
| Создать экран/компонент | `ui_generate_screen/component` | 0 |
| Применить токены | `ui_apply_token_set` | 0 |
| Сгенерировать шейдер (glass/glow/progress/wipe/halo) | `ui_generate_shader` | 0 |
| Сгенерировать VFX (burst/confetti/ambient) | `ui_generate_vfx` | 0 |
| Создать хало | `ui_create_halo` | 0 |
| Создать стеклянную панель | `ui_create_glass_panel` | 0 |
| Создать FSM-контроллер | `ui_create_flow_controller` | 0 |
| Создать ViewModel | `ui_create_viewmodel` | 0 |
| Создать адаптивный HUD | `ui_create_adaptive_hud` | 0 |
| Создать микро-интеракцию | `ui_create_micro_interaction` | 0 |
| Создать переход (transition) | `ui_create_transition` | 0 |
| Проверить layout | `ui_audit_layout` | 0 |
| Проверить perf | `ui_check_perf` | 0 |
| Проверить accessibility | `ui_check_accessibility` | 0 |
| Импортировать токены | `ui_import_tokens` | 1 (предоставить экспорт) |
| Импортировать Rive | `ui_import_rive` | 1 (предоставить `.riv`) |
| Импортировать SVG-иконки | `ui_import_svg_icons` | 1 (предоставить SVG) |

### 8.2 Что требует человека (и как минимизировать)

| Задача | Почему нужно человеку | Как минимизировать |
|---|---|---|
| Visual review (UI Builder) | Визуальная оценка композиции/цвета | Агент генерирует консистентные шаблоны + токены; человек только смотрит |
| Play Mode-тест | Проверка интерактивности/анимации | Агент генерирует audit-отчёты; человек проверяет только критичные пути |
| Предоставить `.riv` (Rive) | Бинарный авторский артефакт | Агент генерирует эквивалент через Shader Graph + tweener |
| Предоставить иконки/шрифты | Бинарные арты | Агент генерирует простые SVG процедурно; сложные — принимает |
| Утвердить план/изменения ProjectSettings | HARD RULE | Агент предлагает отдельным планом |

### 8.3 Цикл верификации (без Play Mode)

После генерации агент **автономно** проверяет:

1. **Compile:** дождаться `is_compiling == false` в `mcpforunity://editor/state`, затем `read_console` (errors+warnings). Если ошибки — исправить и повторить.
2. **Layout audit:** `ui_audit_layout` → проверка Yoga-готчей, фиксированных размеров, overflow.
3. **Perf audit:** `ui_check_perf` → draw calls, vertex budget, GC-риски.
4. **Accessibility audit:** `ui_check_accessibility` → контраст, tap targets, фокус-кольцо.
5. **Отчёт пользователю:** список сгенерированных файлов + результаты аудитов + шаги для ручной проверки (UI Builder review + Play Mode).

**Агент не запускает Play Mode и не делает скриншоты** (см. `dont-start-play` / `manual-playtest-only`).

### 8.4 Принципы самодостаточности (жёсткие)

1. **Агент не просит «открой X Editor»** — он генерирует текстовые ассеты, которые Unity импортирует сам.
2. **Агент не пишет `.meta`/`.asmdef` руками** — Unity генерирует сам. Нужен `.asmdef` — предложить отдельным планом с одобрением.
3. **Агент использует шаблоны** (git-tracked) для всех генерируемых ассетов — это гарантирует валидность и консистентность.
4. **Агент проверяет результат** через console + audit tools, а не через визуальный review.
5. **Агент не смешивает** DOTween/tweener и USS transitions на одном элементе.
6. **Агент не делает grab-pass blur на каждом элементе** — только modal backgrounds, kernel ≤ 5×5, half-res.

---

## 9. Роадмап реализации

### Фаза A — Инфраструктура (1 неделя)
- [ ] Создать `Assets/_Project/Scripts/Editor/UIAgentTools/` с базовой регистрацией custom MCP tools.
- [ ] Реализовать `ui_generate_screen` (шаблоны: modal, hud, fullscreen, overlay, panel, toast).
- [ ] Реализовать `ui_generate_component` (button, card, slot, input, label).
- [ ] Создать шаблоны UXML/USS в `Assets/_Project/UI/Templates/`.
- [ ] Создать шаблоны `.shadergraph` JSON в `Assets/_Project/UI/ShaderTemplates/`.
- [ ] Создать скилл `ui-tooling-agent` в `.opencode/skills/`.

### Фаза B — Токены и стили (1 неделя)
- [ ] Реализовать `ui_scaffold_theme` + `ui_import_tokens`.
- [ ] Реализовать `ui_apply_token_set`.
- [ ] Drift detection (хэш-проверка в CI).
- [ ] Создать скилл `ui-living`.

### Фаза C — Эффекты (1–2 недели)
- [ ] Шаблоны Shader Graph JSON: glass, glow, progress_radial, progress_bar, wipe, chromatic, halo.
- [ ] Реализовать `ui_generate_shader`.
- [ ] Реализовать `ui_create_halo`, `ui_create_glass_panel`.
- [ ] Шаблоны VFX Graph JSON: burst, confetti, ambient, flash.
- [ ] Реализовать `ui_generate_vfx`.
- [ ] Создать скилл `ui-effect-shader`.

### Фаза D — Анимация (1 неделя)
- [ ] Реализовать custom tweener (~300 LOC, 0 GC).
- [ ] Реализовать `ui_create_transition`, `ui_create_micro_interaction`.
- [ ] Реализовать `ui_create_rive_binding` (если `.riv` предоставлен).
- [ ] Создать скилл `ui-rive-binding`.

### Фаза E — Living UI (1–2 недели)
- [ ] Реализовать `ui_create_flow_controller`, `ui_create_viewmodel`.
- [ ] Реализовать `ui_create_adaptive_hud`, `ui_create_progressive_disclosure`.
- [ ] Реализовать `ui_create_contextual_hint`.

### Фаза F — Аудит (1 неделя)
- [ ] Реализовать `ui_audit_layout`.
- [ ] Реализовать `ui_check_perf`.
- [ ] Реализовать `ui_check_accessibility`.
- [ ] Создать скилл `ui-perf-audit`.

### Фаза G — Импорт (1–2 недели)
- [ ] Реализовать `ui_import_tokens` (FigCraft/Figma → USS + ScriptableObject).
- [ ] Реализовать `ui_import_rive`, `ui_import_svg_icons`.
- [ ] Реализовать `ui_export_tokens` (round-trip).
- [ ] Интеграция с FigCraft MCP/CLI (headless export в CI).

**Итого:** ~8–10 недель для полного toolset. Milestone 1 (инструментарий рядом, без поломок основного проекта) — фазы A–B; Milestone 2 (применение к реальному UI) — только после явного «go».

---

## 10. Источники

### Unity UI Toolkit
- Unity Docs: UI Toolkit — https://docs.unity3d.com/Manual/UIElements.html
- USS Properties Reference — https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html
- UI Shader Graph (Unity 6.3+) — https://docs.unity3d.com/Manual/ui-shader-graph.html
- Runtime Data Binding — https://docs.unity3d.com/Manual/UIE-runtime-binding.html
- `[GeneratePropertyBag]` / `INotifyBindablePropertyChanged` — Unity 6 docs.

### MCP for Unity
- mcp-for-unity (Coplay) — https://github.com/CoplayDev/unity-mcp
- `mcpforunity://custom-tools` — регистрация кастомных инструментов.
- `mcpforunity://editor/state` — `is_compiling`, `ready_for_tools`.

### Rive
- Rive Runtime (MIT) — https://github.com/rive-app/rive-unity
- Rive Data Binding / View Models — https://rive.app/docs
- Rive State Machines — https://rive.app/docs/rive-concepts/state-machines

### Shader Graph / VFX Graph
- Shader Graph Manual — https://docs.unity3d.com/Manual/shader-graph.html
- VFX Graph Manual — https://docs.unity3d.com/Manual/VFXGraph.html
- URP Opaque Texture (GrabPass equivalent) — URP asset settings.

### Figma / FigCraft
- Figma Variables Export — https://www.figma.com/developers/api
- FigCraft CLI (headless export) — см. `13_ENVIRONMENT_SETUP.md`.

### Проектные документы
- `01_CURRENT_STATE_AUDIT.md` — текущее состояние UI.
- `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` — AAA-архитектура.
- `09_DESIGN_PATTERNS.md` — паттерны дизайна.
- `10_TOOLING_PIPELINE.md` — пайплайн дизайн → движок.
- `11_AGENT_TOOLING.md` — agent tooling (общий).
- `12_OVERCOMING_LIMITS.md` — преодоление ограничений UITK.
- `13_ENVIRONMENT_SETUP.md` — настройка окружения.
- `AGENTS.md` — HARD RULES проекта.

---

## Приложение A: Пример полного набора файлов для одного экрана

**Запрос:** "Создать экран настроек (Settings) с вкладками Graphics/Audio/Controls, слайдерами, тогглами, кнопками Apply/Cancel."

**Агент генерирует (полностью автоматически, 0 ручных шагов):**

```
Assets/_Project/UI/Screens/
├── SettingsScreen.uxml          (fullscreen template + tabs + content area)
├── SettingsScreen.uss          (токены + layout + tab styles)
├── SettingsScreenController.cs (FlowController integration + tab switching)
├── GraphicsSettingsSection.uxml (sliders + toggles + dropdowns)
├── GraphicsSettingsSection.uss
├── AudioSettingsSection.uxml
├── AudioSettingsSection.uss
├── ControlsSettingsSection.uxml
├── ControlsSettingsSection.uss
Assets/_Project/UI/ViewModels/
├── SettingsViewModel.cs        ([GeneratePropertyBag] + properties)
Assets/_Project/UI/Materials/
├── (нет новых — использует существующие токены)
```

**После генерации агент:**
1. Дожидается `is_compiling == false`.
2. `read_console` → проверка ошибок.
3. `ui_audit_layout(SettingsScreen.uxml)` → отчёт.
4. `ui_check_perf(SettingsScreen)` → отчёт.
5. `ui_check_accessibility(SettingsScreen.uxml)` → отчёт.
6. Отчитывается пользователю: список файлов + результаты аудитов + шаги для ручной проверки.

**Пользователь делает:** UI Builder review + Play Mode-тест (по HARD RULES).

---

## Приложение B: Чек-лист самодостаточности агента

- [ ] Агент генерирует UXML/USS/C# без ручного копирования.
- [ ] Агент генерирует Shader Graph JSON без открытия Shader Graph GUI.
- [ ] Агент генерирует VFX Graph JSON без открытия VFX Graph GUI.
- [ ] Агент генерирует Material YAML без открытия Material Inspector.
- [ ] Агент генерирует токены (DesignTokens.uss + ThemeData.asset) без ручного ввода.
- [ ] Агент проверяет результат через console + audit tools.
- [ ] Агент не просит «открой Rive Editor» — генерирует эквивалент через Shader Graph + tweener.
- [ ] Агент не запускает Play Mode и не делает скриншоты (HARD RULE).
- [ ] Агент не пишет `.meta`/`.asmdef` руками.
- [ ] Агент сохраняет тикеты (`R2-xxx`, `T-FOxx`) в комментариях.
- [ ] Агент делает коммит через Git CLI с тикетом в сообщении (без push).

---

*Документ создан: 2026-11-03. Автор: research subagent. Статус: research (pre-implementation).*
