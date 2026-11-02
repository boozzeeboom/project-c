# План реализации улучшения UI — Project C

**Основан на:** `01_CURRENT_STATE_AUDIT.md`, `02_SOLUTIONS_RESEARCH_2024-2026.md`, `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md`, `07_DESIGN_TOOLING_AND_FIGMA_FREE.md`  
**Цель:** AAA-уровень UI, удобный пайплайн (FigCraft/Figma → Unity), высокая производительность, без поломки основного проекта.  
**Статус:** Draft (требует утверждения).  
**Жёсткое правило:** до явного «go» от пользователя **не менять ни один UI в игре**. Всё делается рядом, в sandbox; основной проект остаётся нетронутым.

---

## 0. Жёсткие правила (HARD)

1. **Не трогать существующий UI:** BootstrapScene, существующие UIDocument, PanelSettings, скрипты UI, префабы UI — не модифицировать.
2. **Всё новое — рядом:** новая папка `Assets/_Project/UIv2/` (или `Assets/UIv2/`) и тестовая сцена `UIv2Sandbox.unity`. Не добавлять sandbox-сцену в Build Settings.
3. **Никаких изменений в BootstrapScene** до Milestone 2.
4. **Milestone 2 (замена UI в игре) — только после явного «go» от пользователя.** До этого момента мы занимаемся только инструментарием и отладкой.
5. **Всё сосуществует рядом:** старый и новый UI могут существовать параллельно; ничего не удаляется и не заменяется без согласования.
6. **Тесты не ломают сборку:** новые тесты в `Assets/_Project/Tests/UIv2/`, не трогать существующие тесты.

---

## 1. Вехи (Milestones)

### Milestone 1 (M1): Инструментарий и отладка — «рядом, без поломок»

**Цель:** создать и отладить весь инструментарий (Design System, токены, пайплайн FigCraft/Figma → Unity, perf guardrails, CI smoke) в изолированном sandbox. **Не трогать UI игры.**

**Критерий завершения M1:** инструментарий работает, пример экрана в sandbox рендерится и проходит perf-бюджеты, ничего в основном проекте не сломано, пользователь говорит «подходит».

Подзадачи (M1):
1. Создать структуру `Assets/_Project/UIv2/` (Sandbox, DesignSystem, Tools, Tests).
2. Создать сцену `UIv2Sandbox.unity` (не в Build Settings) с камерой + светом + тестовым UIDocument.
3. Создать `Core/UIFramework` skeleton (интерфейсы + минимальная реализация) в `Assets/_Project/UIv2/Core/`.
4. Создать Design System: токены (`DesignTokens.uss`), базовые компоненты (button, input, toggle, slider, tab, modal, toast, badge, avatar, empty state), иконки.
5. Настроить пайплайн FigCraft → Unity: экспорт токенов (variable.export → CSS/JSON → генератор USS), экспорт ассетов (file.exportLayers → PNG/SVG → Sprite Atlas), Dev Mode inspect.
6. Настроить Figma Free (резерв): trial коннектора, vendored fallback `FigmaToUnity`, manual polish checklist.
7. Настроить Unity UI Builder workflow: обучение, создание компонентов в UI Builder.
8. Реализовать `UIPerformanceMonitor` (dev) + perf-бюджеты + CI smoke (парсинг UXML/USS, разрешение переменных).
9. Создать пример экрана (например, MainMenu) в sandbox, end-to-end: дизайн → экспорт → UXML/USS → VM → play.
10. Проверить perf: draw calls ≤4, CPU ≤1 ms, 60 FPS.
11. Проверить, что основной проект не сломан (компиляция 0 errors, существующие тесты зелёные, сцены запускаются).

**Запрещено в M1:** изменять BootstrapScene, существующие UI-скрипты, PanelSettings, префабы UI, добавлять что-либо в Build Settings основного проекта.

### Milestone 2 (M2): Замена UI в игре — «только после явного go»

**Цель:** мигрировать UI игры на новый стек, фазами, с feature flag и откатом.

**Вход в M2:** пользователь явно говорит «go» / «подходит» / «начинаем замену». До этого M2 не начинается.

Подзадачи (M2, после go):
1. Включить feature flag `UI_USE_UITK` (per-screen) для мигрируемых экранов.
2. Мигрировать по приоритету: HUD → Core Windows → Social/Chat → Polish.
3. Удалить старые реализации после sign-off (не оставлять мёртвый код).
4. Обновить Build Settings при необходимости.

**Критерий завершения M2:** все основные UI-экраны на новом стеке, perf-бюджеты соблюдены, a11y + локализация, ничего не сломано.

---

## 2. Целевая архитектура (кратко)

(см. `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md`)

```
ProjectC/
├── Core/UIFramework/        # UIFrame, layers, modal stack, focus, input router, transitions, pools
├── Core/DataBinding/        # observable collections, VM base, throttling
├── Core/Theming/            # ThemeData, ThemeApplier, token USS gen
├── Core/Localization/       # locale service, RTL
├── Core/Accessibility/      # screen-reader nodes, focus ring
├── Core/Performance/        # pools, caches, dirty flags, profiling
├── Game/UI/Screens/         # UXML+USS+.cs+.VM per screen
├── Game/UI/Widgets/         # reusable widgets
├── Game/UI/Flow/            # FlowController FSM
├── DesignSystem/            # tokens, components, icons (submodule/UPM)
└── Tools/FigmaExport/        # token/asset export
```

**Слои (по приоритету ввода):** SystemOverlay → Modal → Tooltip → HUD → World.  
**Таксономия:** Screen (Panel/Dialog) → Widget. Game code обращается только к `IUIFrame`.

---

## 3. Структура sandbox (M1)

```
Assets/_Project/
├── UIv2/                    # всё новое здесь
│   ├── Sandbox/             # сцены, тестовые префабы
│   ├── Core/                # UIFramework, DataBinding, Theming, etc.
│   ├── DesignSystem/        # токены, компоненты, иконки
│   ├── Screens/             # примеры экранов (MainMenu и т.д.)
│   ├── Widgets/             # переиспользуемые виджеты
│   ├── Tools/               # FigmaExport, скрипты конвертации
│   └── Tests/               # тесты UIv2
└── Tests/                   # существующие тесты (не трогать)
```

**Важно:** `UIv2/` не импортируется в основной проект автоматически. Никаких ссылок из существующего кода на UIv2 в M1.

---

## 4. Задачи M1 (детально)

### 4.1 Структура и sandbox
1. Создать папки `Assets/_Project/UIv2/...`.
2. Создать сцену `UIv2Sandbox.unity` (камера + свет + UIDocument + тестовый скрипт).
3. Не добавлять сцену в Build Settings.

### 4.2 Core framework skeleton
1. Интерфейсы: `IUIFrame`, `IUILayer`, `IUIScreen`, `IModalStack`, `IFocusNavigator`, `IInputRouter`, `IThemeService`, `ILocalizationService`, `IUIPool<T>`, `ITransition`.
2. Минимальная реализация (без game-логики): `UIFrame`, базовые слои, `ModalStack`, `InputRouter`, `ThemeService`, `ThemeApplier`.
3. Тесты: юнит-тесты на интерфейсы (без Play Mode).

### 4.3 Design System
1. Токены: `DesignTokens.uss` + `ThemeData` ScriptableObject (dark/light).
2. Компоненты: button, input, toggle, slider, tab, modal, toast, badge, avatar, empty state (UXML templates + USS).
3. Иконки: 40+ SVG/PNG в Sprite Atlas.
4. Документация: как использовать компоненты.

### 4.4 Пайплайн FigCraft → Unity
1. Настроить экспорт токенов: `variable.export` (CSS/JSON) → генератор `DesignTokens.uss`.
2. Настроить экспорт ассетов: `file.exportLayers` (PNG/SVG) → Sprite Atlas.
3. Dev Mode inspect: документация по переносу размеров/цветов.
4. MCP: подключить FigCraft MCP к агенту (опционально).
5. CLI: скрипты автоматизации (экспорт, рендер).

### 4.5 Figma Free (резерв)
1. Trial официального коннектора (если нужно).
2. Vendored fallback `FigmaToUnity`.
3. Manual polish checklist.

### 4.6 Unity UI Builder workflow
1. Обучение (документация/видео).
2. Создание компонентов в UI Builder.
3. Live preview и итерация.

### 4.7 Perf guardrails
1. `UIPerformanceMonitor` (dev).
2. Бюджеты (draw calls, CPU, память).
3. CI smoke: парсинг UXML/USS, разрешение переменных.

### 4.8 Пример экрана (sandbox)
1. Выбрать экран (например, MainMenu).
2. Дизайн в FigCraft → экспорт → UXML/USS → VM → play в sandbox.
3. Проверить perf: draw calls ≤4, CPU ≤1 ms, 60 FPS.
4. Документировать время Figma→play (<1 день).

### 4.9 Проверка «ничего не сломано»
1. Компиляция основного проекта: 0 errors.
2. Существующие тесты: зелёные.
3. BootstrapScene запускается без регрессий.
4. `git status` / `git diff` — только новые файлы в UIv2.

---

## 5. Бюджеты и guardrails (CI)

(см. `06_PERFORMANCE_GUARDRAILS.md`)

| Метрика | Бюджет | Проверка |
|---|---|---|
| Draw calls chrome | ≤4 | Frame Debugger smoke |
| UI CPU/кадр | ≤1.0 ms (mid-range PC) | Profiler marker |
| Открытие модала | ≤16 ms | Profiler |
| Inventory scroll 10k | ≥60 FPS | PlayMode perf test |
| Chat append 1k | ≤0.5 ms | PlayMode perf test |
| Память UI-ассетов | ≤120 MB | Memory Profiler |
| World-space nameplates | ≤50 | Runtime cap + LOD |

---

## 6. Риски и митигации

| Риск | Вероятность | Влияние | Митигация |
|---|---|---|---|
| UITK runtime roughness / churn API | Средняя | Среднее | Pin minor version; абстракция `IUIScreen`; фазовый гейт |
| FigCraft alpha rough edges | Средняя | Низкое | Figma Free как резерв; ручной перенос стилей |
| Figma connector ломается | Средняя | Низкое | Vendored fallback; manual polish |
| USS переменные/темизация | Средняя | Среднее | Тема через stylesheet swap; тест в CI |
| RTL/CJK text shaping | Средняя | Высокое | Валидация в standalone build; fallback на TMP |
| Perf регрессия | Средняя | Высокое | Виртуализация+пулы; perf-тесты в CI |
| A11y gaps | Высокая | Среднее | A11y с M1; еженедельные smoke |
| Случайная поломка основного проекта | Низкая | Высокое | Жёсткие правила §0; проверка перед каждым коммитом |

---

## 7. Откат

В M1 откат не нужен (ничего не подключено). В M2 — feature flag `UI_USE_UITK` (per-screen), откат одного экрана без отката всей фазы.

---

## 8. Следующие шаги (немедленно)

1. Утвердить этот план (или скорректировать).
2. M1.1: создать структуру `Assets/_Project/UIv2/` + sandbox-сцену.
3. M1.2: core framework skeleton.
4. M1.4: настроить пайплайн FigCraft → Unity (экспорт токенов + ассетов).
5. Проверить, что основной проект не сломан.

---

*Конец плана. M2 — только после явного «go» от пользователя.*