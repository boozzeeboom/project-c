# UIv2 — Sandbox (Milestone 1)

Эта папка содержит **новый UI-стек** (UI Toolkit + Design System + Core framework), разрабатываемый **рядом** с основным проектом.

## Правила (Milestone 1)
- **Не трогать основной проект.** BootstrapScene, существующие UIDocument/PanelSettings/скрипты UI — не модифицировать.
- **Всё новое — здесь.** Никаких ссылок из существующего кода на UIv2.
- **Sandbox-сцена** `Sandbox/UIv2Sandbox.unity` — не добавлять в Build Settings.
- **Milestone 2 (замена UI в игре)** — только после явного «go» от пользователя.

## Структура
- `Sandbox/` — тестовые сцены и префабы.
- `Core/` — UIFramework, DataBinding, Theming, Localization, Accessibility, Performance.
- `DesignSystem/` — токены, компоненты, иконки.
- `Screens/` — примеры экранов (MainMenu и т.д.).
- `Widgets/` — переиспользуемые виджеты.
- `Tools/` — FigmaExport, скрипты конвертации.
- `Tests/` — тесты UIv2.

## Статус
M1.1 завершён: структура + sandbox-сцена созданы, компиляция 0 errors, основной проект не тронут.
M1.8 завершён: пример экрана MainMenu создан в sandbox (UXML+USS+C#+VM+PanelSettings), ассеты валидны (vta/uss/ps/theme OK), компиляция 0 errors, основной проект не тронут.
M1.3 завершён: Design System — 10 компонентов (Button, Input, Toggle, Slider, TabBar, Modal, Toast, Badge, Avatar, EmptyState) с UXML+USS, токены, иконки (placeholder), README. Компиляция 0 errors.
M1.5 завершён: Figma Free резерв — документация в Tools/Figma/README.md.
M1.6 завершён: Unity UI Builder workflow — документация в Tools/UIBuilder/README.md.
M1.7 завершён: Perf guardrails — UIPerformanceMonitor.cs + бюджеты. Компиляция 0 errors.
M1.9 завершён: проверка «ничего не сломано» — PASS (0 errors, активная сцена WorldScene_0_0, не dirty).

## Статус Milestone 1
Все задачи M1.1–M1.9 завершены. Инструментарий готов и отлажен. Основной проект не тронут.
**Ожидаем явное «go» от пользователя для перехода к Milestone 2 (замена UI в игре).**