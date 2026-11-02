# 15 — Overcoming Unity UI Toolkit Limitations V2 (Agent-Driven, Zero Manual Steps)

**Project:** Project C: The Clouds — MMO-песочница, Unity 6000.5.2f1, URP 17.5.0, Windows standalone.
**Авторство:** research-отчёт (subagent), 2026-11-03 (V2).
**Назначение:** конкретные техники выхода за рамки UITK для AAA-визуала **без ручных шагов** — всё генерируется агентом (код/CLI/скрипты). Дополняет `12_OVERCOMING_LIMITS.md`, `11_AGENT_TOOLING.md`, `08_RENDERING_APPROACHES.md`.
**Статус:** research (pre-implementation). Лицензии/цены проверять у вендоров перед коммитом.

---

## 0. Executive summary

UITK остаётся «скелетом» (layout/темизация/списки/формы), но AAA-визуал (глоу, блюр, частицы, state machines, SDF) требует четырёх дополнительных слоёв, и **все четыре теперь можно генерировать автоматически**:

1. **Rive** — motion/векторный слой. `.riv` создаётся **из текста (RML)** через CLI `rive` без открытия Editor; state machines + data binding описываются в XML; интеграция с UITK — через RenderTexture (официальный UITK-интеграция в roadmap).
2. **NoasisGUI** — XAML-слой с sub-ms рендерингом. XAML — это **текст**, генерируется скриптами (Python `figma-to-noasis-xaml.py` или C#); Noasis Studio встроен в Unity (4.0) с hot reload. Лицензия: €195 indie / €9000+ Pro — **не для core UI** при бюджете < €250K.
3. **Custom shaders (Shader Graph)** — `.shadergraph` — это **JSON/YAML-текст**, генерируется Python-пайплайном из конфигов; применяются к UITK через `-unity-material` (USS) или `VisualElement.style.unityMaterial`. Unity 6.6/6.7 добавляет drop-shadow filter, backdrop-filter, programmable vertices, mesh modifiers.
4. **VFX Graph** — частицы в UI через **dedicated camera → RenderTexture → VisualElement background**, паттерн Luna (CupkekGames) с пулом слотов и `VFXEffectHandler`. Официальная поддержка VFX Graph в UITK в Unity 6 **не планируется** — RenderTexture остаётся каноном.

**Ключевое правило автоматизации:** агент никогда не пишет файлы руками там, где есть инструмент; все артефакты (`.riv`, `.shadergraph`, `.xaml`, VFX asset) генерируются из декларативных конфигов (JSON/RML/YAML/XAML) и коммитятся как текст → diff-friendly, review-friendly.

---

## 1. Rive runtime — programmatic `.riv` creation & integration

### 1.1 Rive CLI + RML (agent-driven, без Editor)

**Rive CLI** (`rive`) — официальный инструмент, превращающий Rive в code-first workflow. Работает signed-out/offline для всего кроме `--publish`/`--rev`/push/pull.

| Команда | Что делает | Автоматизация |
|---|---|---|
| `rive create myproject` | Скаффолд: `rive.yaml`, `AGENTS.md`/`CLAUDE.md`, `.gitignore`, `scene.rml` | Агент вызывает в CI/терминале |
| `rive myproject` | Preview-окно + watch; rebuild on save → `myproject/build/myproject.riv` | Локальная итерация |
| `rive myproject --publish` | Signed `.riv` (требует `rive login`) | CI с аккаунтом |
| `rive myproject --verify` | Компиляция без записи; exit code != 0 при ошибке | CI gate |
| `rive myproject --test` | Запуск Luau-тестов | CI gate |
| `rive myproject --screenshot=out.png` | Рендер PNG без окна | Визуальный diff (агент не видит, пользователь проверяет) |
| `rive myproject --data=battery/level=100` | Установка view model property перед скриншотом | Тестирование состояний |
| `rive myproject --pointer=click@120,60 --advance=20` | Симуляция ввода | Тестирование интеракций |
| `rive inspect myproject --json` | Полная структура сцены (problems, object counts) | Агент-валидация |
| `rive schema Rectangle` / `--search gradient` / `--animatable` / `--bindable` | Схема типов и свойств | Агент не угадывает имена |
| `rive myproject --format=json` | JSON-вывод для парсинга агентом | CI-интеграция |

**RML (Rive Markup Language)** — текстовый XML-формат, 1:1 с типами Rive. Пример:

```xml
<Rive version="1" kind="fragment">
  <Artboard defaultStateMachineId="0:7" width="500" height="500" name="Main">
    <Shape x="250" y="250" name="Ring">
      <Ellipse width="200" height="200"/>
      <Fill><SolidColor colorValue="FF78B891"/></Fill>
    </Shape>
    <StateMachine name="LoadingSM" id="0:7">
      <StateMachineLayer name="Layer1">
        <EntryState><StateTransition stateToId="0:12"/></EntryState>
        <AnimationState x="200" animationId="0:6" id="0:12"/>
      </StateMachineLayer>
    </StateMachine>
    <!-- view model property binding -->
  </Artboard>
</Rive>
```

**Ключевые возможности RML для UI:**
- **View models** (data binding): числа, строки, булевы, **списки** (инвентари), цвета, ассеты (шрифты/блобы).
- **State machines**: состояния/переходы/условия (trigger/bool/number), AnyState, EntryState.
- **Luau-скрипты** (`.luau` файлы в папке проекта) — логика внутри файла.
- **WGSL-шейдеры** (`.wgsl`) — кастомный рендеринг.
- **Layouts**, text, rigging, transforms, easing, assets, keyboard focus, accessibility semantics.

**CI-пайплайн (без ручных шагов):**
```
# 1. Агент генерирует RML из шаблона (JSON-конфиг → RML)
python rive_gen.py --config loading_screen.json --out Assets/UI/Rive/Loading/
# 2. Верификация
rive Assets/UI/Rive/Loading --verify --format=json
# 3. Сборка
rive Assets/UI/Rive/Loading --once  # unsigned .riv для dev
# 4. (опц.) publish (требует login)
rive Assets/UI/Rive/Loading --publish
```

**Альтернатива (Dart):** `co_rive` — библиотека для программного создания `.riv` из JSON-схемы (artboards, shapes, animations, state machines). Подходит если нужен нативный C#/Dart-генератор без зависимости от Rive CLI. JSON-схема:
```json
{
  "artboards": [{
    "name": "LoadingRing", "width": 256, "height": 256,
    "shapes": [{"type": "circle", "name": "ring", "x": 128, "y": 128, "stroke": "#78b891"}],
    "animations": [{"name": "spin", "fps": 60, "duration": 60, "loop": "loop",
      "keyframes": [{"target": "ring", "property": "rotation", "frame": 0, "value": 0},
                    {"target": "ring", "property": "rotation", "frame": 60, "value": 6.28}]}],
    "stateMachines": [{"name": "LoadingSM", "inputs": [{"name": "progress", "type": "number"}],
      "layers": [{"name": "L1", "states": [...], "transitions": [...]}]}]
  }]
}
```

### 1.2 State machines & data binding (Rive)

- **View models** описывают набор свойств; **view model instances** хранят значения.
- Свойства: числа, строки, булевы, **списки** (для инвентарей/повторяющихся элементов), цвета, ассеты (шрифты, блобы).
- Игра устанавливает значения из C# → state machine реагирует; игра может слушать изменения, сделанные в файле.
- **Global view models** (окт 2026): авторские default-инстансы биндятся автоматически; views перерисовываются при изменении.
- **Luau-скриптинг** внутри файла (логика, процедурный моушн, кастомный рисунок).
- **Settled-оптимизация:** runtime детектит, когда дальнейших изменений не будет, и **останавливает advance**; разбужается внешними действиями.
- **Gamepad listeners + Focus** встроены в Rive Editor (навигация по меню контроллером/клавиатурой). В Unity поддержка gamepad — в roadmap (проверять feature support).

### 1.3 Интеграция с Unity UI Toolkit

**Статус:** Rive FAQ отвечает "Not yet, but we're working on it" на вопрос о UITK. Официальная интеграция в roadmap (без даты). Текущий путь — через RenderTexture.

**Паттерн (RenderTexture bridge):**
```
1. Rive Panel (Canvas) → рендерит в RenderTexture (SimpleRenderTargetStrategy).
2. RenderTexture присваивается VisualElement.style.backgroundImage (UITK)
   или PanelSettings background (UITK).
3. Input: Rive Canvas Renderer обрабатывает pointer input через GraphicRaycaster
   на uGUI Canvas. В чистом UITK input не регистрируется (известная проблема).
   Workaround: overlay uGUI Canvas поверх UITK для Rive-элементов с интерактивностью,
   либо ручной проброс pointer events из UITK в Rive Panel.
```

**Известные проблемы (2026):**
- **Flicker в Unity 6.4 + URP RenderGraph** при рендеринге Rive в RenderTexture, отображаемом в UITK: `UniversalRenderPipelineHandler.RenderCamera` по умолчанию = `Camera.main`, state leak между passes. Workaround: установить `RenderCamera` на dedicated camera, у которой нет downstream passes.
- **Input не регистрируется** в чистом UITK — Rive Canvas Renderer требует uGUI Canvas + EventSystem + GraphicRaycaster.

**Рекомендация для Project C:** использовать Rive через **uGUI overlay canvas** (поверх UITK) для интерактивных элементов (кнопки, HUD с hover/press). Для неинтерактивных (loading screens, animated icons, transition wipes) — RenderTexture → UITK background. Ожидать официальную UITK-интеграцию (следить за релизами rive-unity).

### 1.4 Автоматизация (agent-driven)

```
# Agent tool: ui_create_rive_binding
# Вход: riv_path, artboard, state_machine, view_model, bindings
# Выход: C# controller + UXML placeholder + wiring instructions

# Agent tool: ui_import_rive
# Вход: path to .riv
# Выход: копия в Assets + loader stub

# CI pipeline:
1. Agent генерирует RML из шаблона (JSON → RML converter)
2. rive <dir> --verify --format=json  (gate)
3. rive <dir> --once  (unsigned .riv для dev)
4. rive <dir> --screenshot=state.png --data=progress=0.5 (визуальная проверка пользователем)
5. Копия .riv в Assets/_Project/UI/Rive/
6. Unity import (автоматический при refresh)
```

### 1.5 Источники

- Rive CLI overview: https://rive.app/docs/cli/overview
- Rive Getting Started: https://rive.app/docs/cli/getting-started
- Rive RML: https://rive.app/docs/runtimes/advanced-topic/rml
- Rive AI Agents: https://rive.app/agents
- Rive Unity FAQ: https://rive.app/docs/game-runtimes/unity/faq
- Rive Unity components: https://rive.app/docs/game-runtimes/unity/components
- rive-unity GitHub: https://github.com/rive-app/rive-unity
- Rive Unity Asset Store release: https://rive.app/blog/rive-for-unity-updated-and-in-the-unity-asset-store
- Rive UITK RenderTexture issue: https://community.rive.app/c/support/rive-rendertexture-not-getting-input-in-unity-ui-toolkit
- Rive Unity flicker issue #160: https://github.com/rive-app/rive-unity/issues/160
- co_rive (Dart programmatic .riv): https://github.com/coco-de/co-rive
- Charmotion Rive Unity guide: https://charmotion.app/guides/rive-mascot-unity-ui

---

## 2. NoasisGUI — XAML generation, sub-ms rendering, automation

### 2.1 Архитектура и производительность

- **Декларативный XAML поверх лёгкого C++ ядра.** Без внешних зависимостей. «Sub-millisecond» рендеринг на всех платформах.
- **Noasis Studio 4.0** (авг 2026) — полностью интегрированный нативный редактор, построенный на самом Noasis runtime. Заменяет Microsoft Blend. Все официальные сэмплы мигрированы с Blend на Studio.
- **Noasis Studio интегрирован в Unity** (заменяет Blend); распространение через `.tgz` / scoped registry. Hot reload: изменения в Studio появляются в Unity Editor мгновенно, даже в Play mode.
- **Возможности:** data-driven binding, VS Code XAML preview/autocomplete, real-time hot reload, сэмплы, design-time data structures/data sets.
- **Noasis Studio бесплатен** для скачивания/использования как standalone editor.

### 2.2 XAML generation (текст-based, programmatic)

XAML — это **XML-текст**. Автоматизация не требует GUI: агент генерирует `.xaml` файлы как текст, Unity автоматически создаёт `NoasisXaml` asset при импорте.

**Существующие инструменты:**
- `figma-to-noasis-xaml` skill (skillsmp.com) — конвертер Figma → Noasis XAML с Python-скриптом `figma-to-noasis-xaml.py`. Маппит auto-layout → StackPanel, grid → Grid/UniformGrid, текст → TextBlock, векторы → Path Data, растры → PNG. Сохраняет имена слоёв Figma.
- Noasis Studio design-time data: структуры, data sets, sample values для normal/empty/long-text/selected/disabled states.

**Пример генерации XAML из агента:**
```xml
<!-- Assets/_Project/UI/Noesis/InventoryPanel.xaml — автогенерируемый -->
<UserControl x:Class="ProjectC.UI.InventoryPanel"
  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Border Background="{DynamicResource SurfaceBrush}" CornerRadius="16" Padding="16">
      <StackPanel Orientation="Vertical" Spacing="8">
        <TextBlock Text="{Binding Title}" FontSize="18" Foreground="{DynamicResource TextPrimary}"/>
        <ItemsControl ItemsSource="{Binding Items}">
          <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
              <UniformGrid Columns="4"/>
            </ItemsPanelTemplate>
          </ItemsControl.ItemsPanel>
        </ItemsControl>
      </StackPanel>
    </Border>
  </Grid>
</UserControl>
```

**Агент-пайплайн (без ручных шагов):**
```
1. Agent: Figma/FigCraft export → JSON (variables + layers)
2. Agent: figma-to-noasis-xaml.py --output Assets/UI/Noesis/Panel.xaml --asset-dir Assets/UI/Noesis/Assets
3. Agent: пост-обработка (bindings, view-model stubs, resource dictionaries)
4. Unity import: автоматическое создание NoasisXaml asset
5. Noasis Studio: review/tweak (hot reload в Play mode)
6. Agent: ui_audit_layout equivalent (XAML lint)
```

### 2.3 Noasis Studio automation

**Ограничение:** Noasis Studio — GUI-редактор (WYSIWYG), **нет CLI** для автоматизации. Автоматизация достигается через:
- **Генерацию XAML как текст** (Python/C# скрипты) — основной путь.
- **Hot reload** в Unity — изменения файлов подхватываются автоматически.
- **Design-time data** генерируется агентом (JSON → XAML data sets).
- **Noasis Studio встроен в Unity** (4.0+) — дизайнер/пользователь может tweak'ать, но агент работает через файлы.

**Важно:** Noasis Studio использует `Component` roots, которые текущий stable runtime API **не поддерживает** (unknown element errors). Рекомендация: inline visual tree, использовать `Grid`/`Page`/`UserControl` roots, `DataTemplate`/`ControlTemplate` ресурсы.

### 2.4 Лицензирование (актуально на 2026)

| Тир | Условие | Цена |
|---|---|---|
| **Indie** | < €100K годовой выручки, < €250K бюджет проекта | **€195** per project, все платформы |
| **Pro** | < €1.5M бюджет проекта | €9000 первая платформа + €3600 доп. |
| **Premium** | €1.5M–€12M бюджет | €18000 первая + €7200 доп. |
| **Enterprise** | > €12M | по запросу |

- Royalty-free, perpetual license. Indie: minor releases lifetime + 1 год major upgrades.
- Pro/Premium/Enterprise: priority/premium support обязателен первый год (€3200–€6300/год).
- **Бесплатной опции больше нет** (раньше была free tier; убрали).
- Noasis Studio бесплатен для скачивания/использования как standalone editor.

### 2.5 Интеграция и автоматизация

**Вердикт для Project C:** NoasisGUI — **не для core UI** (стоимость, vendor lock-in, нет нативного Figma-импорта). Рассматривать только если: (а) профилирование UITK покажет непреодолимый потолок, (б) есть XAML-экспертиза, (в) бюджет позволяет Pro-тира. Триал перед решением обязателен.

**Если всё же используется:** автоматизация через генерацию XAML-текста (Python/C#) + hot reload + design-time data. Никаких ручных шагов в Studio — Studio используется только для review/tweak.

### 2.6 Источники

- NoasisGUI Unity tutorial: https://www.noesisengine.com/docs/Gui.Core.Unity3DTutorial.html
- Noasis Studio docs: https://www.noesisengine.com/docs/Gui.Studio.Documentation_Index.html
- NoasisGUI 4.0 release notes: https://www.noesisengine.com/docs/Gui.Core.Changelog.html
- NoasisGUI workflow tutorial: https://www.noesisengine.com/docs/Gui.Core.WorkflowTutorial.html
- figma-to-noasis-xaml skill: https://skillsmp.com/creators/quabug/engine-demo-oss/codex-skills-figma-to-noesis-xaml
- noesis-ui skill: https://skillsmp.com/creators/quabug/engine-demo-oss/codex-skills-noesis-ui
- Noasis Studio integration forum: https://www.noesisengine.com/forums/viewtopic.php?t=3644
- Noasis Studio migration forum: https://www.noesisengine.com/forums/viewtopic.php?t=3654

---

## 3. Custom shaders — Shader Graph generation via code

### 3.1 Shader Graph file format & code generation

**`.shadergraph` — это текстовый файл (JSON/YAML)**, импортируемый Unity через `ShaderGraphImporter`. Это значит: агент может генерировать шейдеры как текст без открытия Shader Graph Editor.

**Подтверждённые подходы:**

1. **Python pipeline** (yoo.be, 2026): чтение JSON-конфига → конструкция графа в памяти → запись `.shadergraph`. Node IDs обеспечивают детерминированный порядок (version control). Запускается из CLI, Unity импортирует при `AssetDatabase.Refresh()`.

```python
# shaders/pbr_shader.json → .shadergraph
import json, os
from unity_tools import UnityAssetCreator

def build_shader_graph(config):
    creator = UnityAssetCreator()
    graph = creator.create_graph(config['graphName'])
    node_refs = {}
    for node in config['nodes']:
        n = graph.add_node(node['type'], node['id'])
        n.position = node['position']
        for prop, val in node.get('properties', {}).items():
            n.set_property(prop, val)
        node_refs[node['id']] = n
    for conn in config['connections']:
        graph.connect(node_refs[conn['from']], conn['out'],
                     node_refs[conn['to']], conn['in'])
    for prop_name, prop_def in config.get('properties', {}).items():
        graph.add_property(prop_name, prop_def['type'], prop_def['default'])
    return graph

def save_graph(graph, output_dir):
    path = os.path.join(output_dir, f"{graph.name}.shadergraph")
    graph.save(path)
```

2. **Reflection-based generation** (Bronson Zgeb): `ShaderGraphImporter` → `GetGraphData` (reflection) → `Generator` (reflection) → `generatedShader` (HLSL-строка). Позволяет конвертировать `.shadergraph` в `.shader` (HLSL) программно. Хрупко (зависит от внутренних API).

3. **Custom Function Node (CodeFunctionNode)**: внедрение HLSL через C#-классы, наследующие `CodeFunctionNode`. Метод `GetFunctionToConvert` использует reflection для конвертации метода в `MethodInfo`. Подходит для кастомных нод, не для генерации целого графа.

4. **Unity Skills ShaderGraph module** (SkillsForUnity): reflection-based MCP-инструменты для редактирования `.shadergraph` (создание графа, добавление нод, подключение, установка свойств). Версионно-зависим (14.0.x / Unity 6). Если reflection fails — сообщать о version mismatch, не retry.

**Рекомендация для Project C:** использовать **Python pipeline** (JSON-конфиг → `.shadergraph`) как основной путь. Он детерминированный, version-control-friendly, не зависит от reflection. Для сложных эффектов — шаблоны `.shadergraph` с параметрами (как в `ui_generate_shader`).

### 3.2 Техники AAA-визуала (Shader Graph)

| Техника | Реализация | Стоимость |
|---|---|---|
| **Glow (rim/inner/pulse)** | Fresnel + emissive + animated sin (pulse) | 1 draw call |
| **Blur (element)** | Kernel blur на самом элементе (grab-pass) | Высокая; ограничить kernel ≤ 5×5 |
| **Bloom** | URP Volume Bloom + emissive материалы на UI | Один pass на весь экран (дёшево) |
| **Frosted glass** | `_CameraOpaqueTexture` + kernel blur 5×5 + tint + noise + edge highlight | Средняя-высокая; только modal backgrounds |
| **Rounded corners / SDF** | SDF rounded rect + falloff | Низкая |
| **Circular progress** | Polar coords + step + smoothstep edge + glow | Низкая |
| **Progress bar (damage trail)** | `_Value` + `_TrailValue` (лагает) + gradient | Низкая |
| **Wipe (iris/pixelate/dissolve)** | Noise + threshold + edge glow | Низкая |
| **Chromatic (glitch)** | UV offset per channel + noise | Низкая |
| **Halo (soft glow around rect)** | SDF rounded rect + falloff + color | Низкая |
| **Gradient borders** | Два вложенных слоя (внешний hairline + внутренний блик) или SDF с двумя цветами | Низкая |

**Unity 6.6/6.7 новые возможности:**
- **Drop-shadow filter** (6.6): `filter: drop-shadow()` — конформится к геометрии и border silhouette элемента. **Анимируемый** (transition-property: filter).
- **Backdrop-filter** (6.6): blur/tint/grayscale/sepia/brightness/contrast/saturate/hue-rotate/invert/opacity за элементом. URP only, screen-space panels only.
- **Custom filter functions** в backdrop-filter (6.7).
- **Programmable vertices** (6.6): vertex stage programming, custom vertex channels (UV1/UV2), interpolators — GPU-driven deformation, waves, procedural transitions.
- **Mesh modification APIs** (6.6): `VisualElement.AddMeshModifier` / `RemoveMeshModifier` / `ClearMeshModifiers` — пост-обработка геометрии перед GPU.
- **Shader Graph target for UI Toolkit Filters** (6.7): авторинг кастомных post-processing фильтров в Shader Graph вместо написания шейдера с `UnityUIEFilter.cginc`.
- **Background gradients** (6.7): `linear-gradient()` и `radial-gradient()` на `background-image`.

**Ограничения (по-прежнему):**
- `box-shadow` (CSS-style) **не поддерживается** (planned, no committed release date). Обход: child-ореолы, drop-shadow filter (6.6), SDF-шейдер.
- `backdrop-filter` — только URP, только screen-space panels, не world-space, не WebGL (баг).
- USS transitions **не работают** с backdrop-filter (хотя эксперименты показывают, что иногда работает).
- Custom shader ломает batching при смене материала. Держать ≤8 distinct текстур/материалов на батч.

### 3.3 Применение к UI

**Способы применения шейдера к UITK-элементу:**

1. **USS `-unity-material`** (рекомендуется):
```css
.glass-modal {
  -unity-material: resource("GlassModal");
  background-color: rgba(26, 29, 36, 0.12);
  border-width: 1px;
  border-color: rgba(255, 255, 255, 0.18);
  border-radius: var(--radius-lg);
}
```

2. **C# inline style:**
```csharp
visualElement.style.unityMaterial = glassMaterial;
```

3. **VisualElement.style.unityMaterial** (экспериментально в Unity 6).

4. **Shader Graph target for UI Toolkit Filters** (6.7): авторинг кастомного фильтра, применяемого через USS `filter` property.

### 3.4 Автоматизация (agent-driven)

**Agent tool: `ui_generate_shader`**
- Вход: `effect_type` (glass/glow/progress_radial/progress_bar/wipe/chromatic/halo), `params`, `target_material`, `uss_class`
- Выход: Shader Graph asset + Material + USS-класс + C# helper для runtime-параметров

**Пайплайн:**
```
1. Agent: создаёт JSON-конфиг для эффекта (параметры из токенов)
2. Agent: Python pipeline → .shadergraph (из шаблона)
3. Unity import → Shader Graph asset
4. Agent: создаёт Material из шейдера
5. Agent: генерирует USS-класс с -unity-material ссылкой
6. Agent: генерирует C# helper (SetFloat/SetColor)
7. Agent: ui_audit_layout → проверка
```

**Шаблоны шейдеров** хранятся в `Assets/_Project/UI/Shaders/Templates/` (`.shadergraph` с параметрами). Агент инстанцирует их с подстановкой. Это исключает ручное создание шейдеров в Shader Graph Editor.

### 3.5 Источники

- Shader Graph scripting API: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/api/index.html
- Automating Shader Graph with Python (2026): https://yoo.be/automating-shader-graph-generation-python-unity-2025/
- Convert Shader Graph to HLSL (reflection): https://discussions.unity.com/t/how-to-change-shadergraph-file-assets-from-code/331697
- Custom Function Node: https://bitinn.github.io/ScriptableRenderPipeline/ShaderGraph/Custom-Nodes-With-CodeFunctionNode/
- Unity Skills ShaderGraph module: https://github.com/Besty0728/Unity-Skills/blob/main/SkillsForUnity/unity-skills~/skills/shadergraph/SKILL.md
- Unity 6.7 state of UITK (Shader Graph target for UI filters): https://discussions.unity.com/t/state-of-ui-toolkit-in-unity-6-7/1736756/1
- Backdrop filters in UITK 6.6: https://reko3d.com/blog/new-unity-6-6-feature-backdrop-filters-in-ui-toolkit/
- Drop shadows in UITK 6.6: https://reko3d.com/blog/drop-shadows/
- Custom shaders feedback request: https://discussions.unity.com/t/feedback-request-custom-shaders-and-post-processing-filters/1680973
- USS properties reference: https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html
- Backdrop filter docs: https://docs.unity.com/en-us/engine/6000.7/manual/uitoolkits/uielements/uie-uss/uss-filter/backdrop-filter

---

## 4. VFX Graph — particles for UI via RenderTexture

### 4.1 Паттерн (dedicated camera → RenderTexture → UI)

UITK **не поддерживает** VFX Graph нативно (Unity 6: "not planned", roadmap для будущих версий). Канонический паттерн:

```
1. Создать RenderTexture (fixed size, напр. 512×512).
2. Создать dedicated Camera (orthographic, culling mask = VFX layer).
3. На объекте с VisualEffect — layer = VFX.
4. Camera.targetTexture = RenderTexture.
5. В UI: VisualElement.style.backgroundImage = RenderTexture (UITK)
   или RawImage (uGUI).
6. Управлять play/stop через C# (event-driven).
```

**Известные проблемы:**
- **Dark edges** при alpha blending: VFX с alpha-шейдерами показывают тёмные края при рендеринге в RenderTexture → UITK. Workaround: premultiply alpha в шейдере или blit через кастомный шейдер.
- **Z-order:** RenderTexture отображается как background image, поэтому частицы всегда позади дочерних элементов. Для overlay-частиц (поверх UI) — использовать Luna PlayOverlay или отдельный fullscreen quad с VFX.
- **Performance:** один RenderTexture на элемент = катастрофа. Использовать пул слотов (Luna pattern).

### 4.2 Luna UI solution (CupkekGames) — pooled pipeline

**Luna** (Unity Asset Store, Unity 6+) — готовое решение для рендеринга 3D/VFX в UITK через pooled Camera → RenderTexture pipeline.

**Ключевые компоненты:**
- `UIRenderManager` — менеджер пула слотов (Camera + RenderTexture).
- `RenderElement` — custom VisualElement с built-in render resolution.
- `UIRenderHandle` — caller-facing API (Stop, Release, Replay).
- `PlayOverlay` — overlay 3D-эффектов поверх существующего UI-элемента (transparent child VisualElement, aspect-ratio из RenderTexture).
- `PlayBurst` — fire-and-forget burst (auto-release при drain частиц, 5s timeout).

**Effect handlers (pluggable):**
| Handler | Tracks | Условие |
|---|---|---|
| `ParticleEffectHandler` | ParticleSystem | Всегда доступен |
| `VFXEffectHandler` | VisualEffect (VFX Graph) | `UNITY_VFX` define |

**API пример:**
```csharp
// RenderElement в UXML:
// <CupkekGames.Luna.RenderElement name="MyRender" RenderWidth="512" RenderHeight="512" />

RenderElement renderElement = root.Q<RenderElement>("MyRender");
UIRenderHandle handle = renderElement.Play(myParticlePrefab);
handle.Stop();   // остановить
handle.Release(); // освободить слот

// Overlay burst:
UIRenderManager.Instance.PlayBurst(targetIcon, _coinBurstPrefab, size: 320f);
```

**Setup:**
1. Создать dedicated layer (напр. layer 31 `UIRender`).
2. Добавить `UIRenderManager` + `LunaUIManager` в сцену.
3. Main camera culling mask исключает UIRender layer.
4. URP: добавить UIRender layer в Filtering → Opaque/Transparent Layer Mask активного Universal Renderer Data.

### 4.3 VFX effect handler & alpha blending pitfalls

- **Alpha blending:** VFX Graph с alpha-шейдерами при рендеринге в RenderTexture → UITK показывает dark edges. Решение: premultiply alpha в шейдере VFX или blit через кастомный шейдер с корректным blend mode.
- **Overflow:** PlayOverlay создаёт transparent child с aspect-ratio из RenderTexture; overflow-visible позволяет частицам вылезать за границы элемента.
- **Auto-release:** слот автоматически возвращается в пул при drain частиц; 5s timeout защита от зависших слотов.
- **Custom effect handlers:** расширить `UIRenderEffectHandler` и зарегистрировать через `UIRenderEffectHandler.Register()`.

### 4.4 Автоматизация (agent-driven)

**Agent tool: `ui_generate_vfx`**
- Вход: `effect_type` (burst/confetti/ambient/damage_flash), `particle_budget`, `target_element`
- Выход: VFX Graph asset + C# trigger wrapper + RenderTexture setup instructions

**Пайплайн:**
```
1. Agent: создаёт VFX Graph asset из шаблона (VFX Graph — бинарный asset, не текст;
   генерация через Unity Editor API или шаблоны .vfx)
2. Agent: создаёт C# trigger wrapper (Play/Stop/Release)
3. Agent: настраивает RenderTexture + Camera (через C# или Luna UIRenderManager)
4. Agent: генерирует UXML с RenderElement или VisualElement с backgroundImage
5. Agent: ui_check_perf → проверка (particle budget, draw calls)
```

**Ограничение:** VFX Graph assets — бинарные (`.vfx`), не текстовые. Автоматизация через Unity Editor API (`VisualEffectAsset`) или шаблоны-префабы. Agent может создавать через `manage_asset` MCP tool или C# скрипты в Editor.

### 4.5 Источники

- VFX Graph UI support discussion: https://discussions.unity.com/t/vfx-graph-ui-support/1556550
- Luna UI documentation: https://www.docs.cupkek.games/luna/features/uirender
- Luna Asset Store: https://assetstore.unity.com/packages/tools/gui/luna-complete-ui-solution-299710
- VFX Graph manual: https://docs.unity3d.com/6000.1/Documentation/Manual/VFXGraph.html
- UI overlay particles dark edges: https://discussions.unity.com/t/overlaying-particle-effects-over-ui-toolkit-result-in-dark-edges/1717112

---

## 5. Unity UI Toolkit — limitations & workarounds (6.6/6.7)

### 5.1 Таблица: ограничение → техника → инструмент

| Ограничение | Статус (Unity 6.x) | Workaround (agent tool) |
|---|---|---|
| `backdrop-filter` | 6.6+, URP only, screen-space only | frosted-glass shader (grab-pass) через `ui_generate_shader(type=glass)`; или URP Volume global blur; или RenderTexture |
| `filter: drop-shadow()` | 6.6+ | child-ореолы (линейная аппроксимация) через `ui_create_halo`; либо shader `UI_Halo`; либо drop-shadow filter |
| `box-shadow` (CSS) | ❌ planned, no date | child-ореолы + drop-shadow filter (6.6) + SDF-шейдер |
| `gap` (flex) | 6.7 experimental (6 Unity: supported) | margins на детях; или `gap` напрямую |
| `transition` | ✅ только transform/opacity/color/bg-color; filter animatable (6.6) | USS transitions для простых; DOTween для сложных; `ui_create_micro_interaction` |
| `conic-gradient` | ❌ нет | shader `UI_RadialProgress`; custom mesh (generateVisualContent); uGUI Image (Filled, Radial 360); Rive |
| `radial-gradient` | ⚠️ 6.7 (background-image) | USS `radial-gradient()` (6.7); иначе текстура/шader |
| CSS Grid | 6.7 experimental | вложенный flex (row/col) или абсолютное позиционирование |
| `z-index` | 6.7 experimental | порядок в дереве (последний — сверху) |
| `position: sticky` | ❌ нет | C#-логика скролла + классы |
| `:nth-child()` | ⚠️ частично | классы на элементах при генерации |
| `animation` (keyframes) | ❌ нет | DOTween / Rive / USS transitions |
| Particle system | ❌ не планируется в 6.x | VFX Graph → RenderTexture → `ui_generate_vfx` (Luna pattern) |
| 3D objects in UI | ❌ не планируется в 6.x | RenderTexture (Luna UIRenderManager) |
| `box-shadow` (inset) | ❌ нет | внутренние градиенты (child elements) |
| `mix-blend-mode` | ⚠️ частично | shader blending |
| `clip-path` | ❌ нет | маски (rectangular preferred) + shader |
| `text-shadow` | ⚠️ частично (USS text-shadow) | TMP outline/glow; либо shader |
| `aspect-ratio` | ✅ Unity 6+ | AspectRatioFitter (uGUI) или USS `aspect-ratio` |
| Soft masking | ❌ planned | rectangular masks (shader-based) |

### 5.2 Ключевые workaround'ы (детально)

**Glassmorphism без backdrop-filter (на 6000.5):**
- Полупрозрачные поверхности (alpha 0.72–0.85).
- Hairline-рамки (1 px, rgba(255,255,255,0.18–0.30), верхняя/левая ярче).
- Верхний блик (subtle gradient top-down).
- Тень через child-ореолы.
- Noise/grain текстура для глубины.
- Обновиться до 6.6 для настоящего `backdrop-filter: blur()`.

**Halos/glows без box-shadow:**
```xml
<ui:VisualElement class="card">
  <ui:VisualElement class="halo-card" />
</ui:VisualElement>
```
```css
.halo-card {
  position: absolute; top: -12px; bottom: -12px; left: -12px; right: -12px;
  border-radius: 50%; opacity: 0;
  transition: opacity var(--duration-normal) var(--easing-out);
  pointer-events: none;
}
.card.hl > .halo-card { opacity: 1; background-color: rgba(120, 184, 145, 0.55); }
```
Бюджет: ≤ 1 halo per focusable element.

**Circular progress без conic-gradient:**
1. Shader `UI_RadialProgress` (polar coords + step + smoothstep + glow) — рекомендуется.
2. Custom mesh via `generateVisualContent` (MeshGenerationContext) — ручной веер.
3. uGUI Image (Filled, Radial 360) — `fillAmount` 0–1.
4. Rive — state machine с data-bound progress.

**Transitions без CSS transition на всех свойствах:**
USS `transition` поддерживает только opacity/translate/rotate/scale/color/background-color/filter. Для layout-анимаций — DOTween (анимация через inline style transform, не layout) или Rive.

---

## 6. Automation blueprint (agent-driven, zero manual)

### 6.1 Пайплайн генерации (полный цикл)

```
[Intent] "Создать glassmorphism-панель для модального окна"
   │
   ▼  ui_generate_shader(effect_type="glass", params={...})
[Shader]  UI_Glass.shadergraph + GlassModal.mat + USS .glass-modal
   │
   ▼  ui_create_glass_panel(blur=8, tint=0.12, edge=true)
[Structure]  добавляет VisualElement class="glass-modal" в ModalContainer.uxml
   │
   ▼  ui_generate_screen(name=Modal, template=modal)
[Screen]  Modal.uxml + Modal.uss + ModalViewModel.cs
   │
   ▼  ui_apply_token_set(tokens=DesignTokens)
[Style]  подстановка var(--*) ссылок
   │
   ▼  ui_create_halo(color=accent, states=[idle,hover,selected])
[Effects]  halo child-elements + USS transitions
   │
   ▼  ui_create_viewmodel(name=ModalViewModel, properties=[...])
[Data]  C# ViewModel с bindings
   │
   ▼  ui_audit_layout + ui_check_perf
[Verify]  отчёт
   │
   ▼  (пользователь) UI Builder review + Play Mode test
```

### 6.2 Инструменты автоматизации (сводка)

| Инструмент | Что генерирует | Вход |
|---|---|---|
| `ui_generate_screen` | UXML + USS + ViewModel stub | имя, шаблон, токены |
| `ui_generate_component` | UXML template + USS + C# partial | имя, вариант, токены |
| `ui_generate_shader` | Shader Graph + Material + USS класс | effect_type, params, target |
| `ui_create_halo` | child-ореолы + USS transitions | цвет, размер, состояния |
| `ui_create_glass_panel` | frosted-glass material + UXML + USS | blur, tint, edge |
| `ui_generate_vfx` | VFX Graph + C# trigger + RenderTexture setup | effect_type, budget |
| `ui_create_rive_binding` | C# controller + UXML placeholder | riv, artboard, SM, VM, bindings |
| `ui_import_rive` | копия в Assets + loader stub | path to .riv |
| `ui_create_viewmodel` | C# ViewModel с [GeneratePropertyBag] | имя, свойства |
| `ui_create_flow_controller` | FSM + ModalStack + InputRouter | состояния, переходы |
| `ui_create_adaptive_hud` | HUD controller + классы | контексты, элементы |
| `ui_audit_layout` | отчёт (Yoga-готчи, фикс. размеры) | путь к UXML/USS |
| `ui_check_perf` | draw calls, vertex budget, GC | имя экрана |

### 6.3 CI-пайплайн (headless, без ручных шагов)

```
# Design → Tokens
figcraft-cli mcp  # headless export
  → variable.export {format: "json+css"} → tokens.json + tokens.css
  → export {layers: icons, format: "png", scale: 2} → /tmp/icons/
  → inspect.code {format: "css"} → css-output.css
  → codeConnect.export → figcraft.connect.json

# Importer (Python/C#)
ui_import_tokens(tokens.json) → DesignTokens.uss + ThemeData.asset
ui_import_svg_icons(/tmp/icons/) → sprite atlas + USS refs

# Structure
ui_generate_screen(name=..., template=..., connect=figcraft.connect.json)
  → .uxml + .uss + ViewModel.cs

# Effects
ui_generate_shader(effect_type="glass", ...) → .shadergraph + .mat + USS
ui_create_halo(...) → child-ореолы
ui_generate_vfx(effect_type="burst", ...) → .vfx + trigger

# Rive
rive <dir> --verify --format=json  (gate)
rive <dir> --once  (unsigned .riv)
ui_import_rive(path=...) → Assets + loader stub
ui_create_rive_binding(...) → controller

# Verify
ui_audit_layout(...) → отчёт
ui_check_perf(...) → отчёт
rive <dir> --screenshot=state.png --data=...  (визуальная проверка пользователем)

# Commit (агент, через Git CLI, с тикетом)
```

### 6.4 Guardrails (производительность)

- **≤4 draw calls для chrome** (один на слой).
- **Vertex Budget** в PanelSettings (20k–50k вершин) — один буфер.
- **Usage hints:** `DynamicTransform` для анимируемых, `GroupTransform` для контейнеров.
- **Анимировать transforms** (translate/scale/rotate), никогда layout-props (width/height/top/left).
- **Не переключать USS-классы** на больших иерархиях во время анимации — inline style.
- **Rectangular masks** (shader-based) по умолчанию; complex ≤7 уровней.
- **≤8 distinct текстур/материалов** на батч; Sprite Atlas для статики.
- **Скрытые панели:** `DisplayStyle.None` + `UIDocument.enabled=false`.
- **Один shared PanelSettings** для нескольких UIDocument.
- **Rive:** бюджет per-instance; избегать 100+ одновременных state machines.
- **VFX Graph в UI:** RenderTexture at lower res; limit particle counts; object pooling; event-driven (не постоянные, кроме ambient ≤ 50 частиц).
- **Blur kernel ≤ 5×5**, half-res; только для modal backgrounds.
- **Halo:** ≤ 1 per focusable element.

---

## 7. Консолидированные источники

### Rive
- https://rive.app/docs/cli/overview
- https://rive.app/docs/cli/getting-started
- https://rive.app/docs/runtimes/advanced-topic/rml
- https://rive.app/agents
- https://rive.app/docs/game-runtimes/unity/faq
- https://rive.app/docs/game-runtimes/unity/components
- https://github.com/rive-app/rive-unity
- https://rive.app/blog/rive-for-unity-updated-and-in-the-unity-asset-store
- https://community.rive.app/c/support/rive-rendertexture-not-getting-input-in-unity-ui-toolkit
- https://github.com/rive-app/rive-unity/issues/160
- https://github.com/coco-de/co-rive
- https://charmotion.app/guides/rive-mascot-unity-ui

### NoasisGUI
- https://www.noesisengine.com/docs/Gui.Core.Unity3DTutorial.html
- https://www.noesisengine.com/docs/Gui.Studio.Documentation_Index.html
- https://www.noesisengine.com/docs/Gui.Core.Changelog.html
- https://www.noesisengine.com/docs/Gui.Core.WorkflowTutorial.html
- https://skillsmp.com/creators/quabug/engine-demo-oss/codex-skills-figma-to-noesis-xaml
- https://skillsmp.com/creators/quabug/engine-demo-oss/codex-skills-noesis-ui
- https://www.noesisengine.com/forums/viewtopic.php?t=3644
- https://www.noesisengine.com/forums/viewtopic.php?t=3654

### Custom shaders
- https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/api/index.html
- https://yoo.be/automating-shader-graph-generation-python-unity-2025/
- https://discussions.unity.com/t/how-to-change-shadergraph-file-assets-from-code/331697
- https://bitinn.github.io/ScriptableRenderPipeline/ShaderGraph/Custom-Nodes-With-CodeFunctionNode/
- https://github.com/Besty0728/Unity-Skills/blob/main/SkillsForUnity/unity-skills~/skills/shadergraph/SKILL.md
- https://discussions.unity.com/t/state-of-ui-toolkit-in-unity-6-7/1736756/1
- https://reko3d.com/blog/new-unity-6-6-feature-backdrop-filters-in-ui-toolkit/
- https://reko3d.com/blog/drop-shadows/
- https://discussions.unity.com/t/feedback-request-custom-shaders-and-post-processing-filters/1680973
- https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html
- https://docs.unity.com/en-us/engine/6000.7/manual/uitoolkits/uielements/uie-uss/uss-filter/backdrop-filter

### VFX Graph
- https://discussions.unity.com/t/vfx-graph-ui-support/1556550
- https://www.docs.cupkek.games/luna/features/uirender
- https://assetstore.unity.com/packages/tools/gui/luna-complete-ui-solution-299710
- https://docs.unity3d.com/6000.1/Documentation/Manual/VFXGraph.html
- https://discussions.unity.com/t/overlaying-particle-effects-over-ui-toolkit-result-in-dark-edges/1717112

### Internal (Project C)
- `docs/UI/Impruvements_research/12_OVERCOMING_LIMITS.md`
- `docs/UI/Impruvements_research/11_AGENT_TOOLING.md`
- `docs/UI/Impruvements_research/10_TOOLING_PIPELINE.md`
- `docs/UI/Impruvements_research/08_RENDERING_APPROACHES.md`
- `docs/UI/Impruvements_research/09_DESIGN_PATTERNS.md`
- `docs/UI/Impruvements_research/06_PERFORMANCE_GUARDRAILS.md`

---

## 8. Вердикт

**Для Project C (Unity 6000.5, URP, MMO, бюджет indie):**

1. **Rive** — рекомендуется как motion/векторный слой. CLI + RML позволяет генерировать `.riv` из кода (agent-driven). Интеграция с UITK через RenderTexture (официальная UITK-интеграция в roadmap). Начать с одного экрана (loading/main menu) для оценки.
2. **NoasisGUI** — не для core UI (стоимость €9000+ для бюджета > €250K). Рассматривать только при непреодолимом потолке UITK + бюджет.
3. **Custom shaders (Shader Graph)** — основной инструмент AAA-эффектов. `.shadergraph` генерируется Python-пайплайном из JSON-конфигов. Применять к конкретным элементам через `-unity-material`.
4. **VFX Graph** — точечно, только для «вау»-моментов (лут, апгрейд, победа). Использовать Luna pattern (pooled Camera → RenderTexture) или свой пул слотов.
5. **UITK limitations** — большинство обходится через 6.6/6.7 (backdrop-filter, drop-shadow filter, gap, z-index, CSS Grid, programmable vertices, mesh modifiers). Для версий < 6.6 — эмуляция (glassmorphism, halos, SDF-шейдеры).

**Минимальный набор зависимостей (все бесплатные):** UI Toolkit + URP + TMP + Shader Graph + VFX Graph (опц.) + Rive runtime (MIT, splash) + свой микро-твинер (~300 LOC) + Luna (опц., для VFX в UI) + Python pipeline для генерации ассетов.

**Не используем:** NoasisGUI (пока), Coherent Gameface, WebView, Dear ImGui (player UI), Unity Vector Graphics (экспериментальный), ThorVG (нет интеграции), DOTween (GC + зависимость).
