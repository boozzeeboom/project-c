# UI Improvements Research — Project C

Папка содержит результаты глобального ресерча по улучшению UI в проекте **Project C: The Clouds** (Unity 6, URP, Windows standalone, MMO-песочница). Цель: перейти от «кривого/убогого» UI к AAA-уровню, с удобным пайплайном (Figma → Unity), без тяжёлых/неоптимизированных плагинов, со своим крутым решением.

## Навигация

| Файл | Содержание |
|---|---|
| `01_CURRENT_STATE_AUDIT.md` | Инвентарь текущего UI: системы, окна, ассеты, анти-паттерны, PanelSettings/theme/sorting, метрики. |
| `02_SOLUTIONS_RESEARCH_2024-2026.md` | Сравнение решений: UI Toolkit, uGUI, NoesisGUI, Coherent Gameface, Rive, ThorVG, WebView, Dear ImGui. Таблица сравнения, рекомендации, риски, источники. |
| `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md` | AAA-архитектура UI: Screen/Window/Panel/Widget, UIFrame-фасад, MVVM/MVP, модальный стек, фокус/навигация, инпут-роутинг, темизация/токены, локализация, доступность, производительность, Figma-пайплайн, миграция. |
| `04_IMPLEMENTATION_PLAN.md` | План реализации для Project C (фазы, задачи, критерии приёмки, риски, откат). |
| `05_REVERSE_ENGINEERING_NOTES.md` | Заметки по реверс-инжинирингу: Unity UI Toolkit internals, batching, Figma connector, AAA-паттерны. |
| `06_PERFORMANCE_GUARDRAILS.md` | Бюджеты производительности и guardrails. |
| `07_DESIGN_TOOLING_AND_FIGMA_FREE.md` | Figma Free vs альтернативы (Penpot, Lunacy, FigCraft) vs «свой визуализатор»: хватит ли бесплатной Figma, рекомендации. |

## Краткое резюме

- **Текущее состояние:** UI Toolkit доминирует (32 UIDocument-скрипта, 19 UXML, 20 USS, 22 PanelSettings), но есть 14 файлов с `Clear()+CloneTree()`, 15 с `Resources.Load<StyleSheet>`, 3.1 MB/frame GC от repaint, скрытые панели продолжают рендериться. uGUI/TMP — только world-space/debug.
- **Рекомендация:** UI Toolkit как основной фреймворк окон/панелей/списков/форм; uGUI оставить для world-space/3D UI; Rive — для анимированного векторного UI; TextMeshPro — для всего текста. WebView/Noesis/Coherent — не для core UI (только при необходимости).
- **Архитектура:** Screen/Window/Panel/Widget таксономия, единый фасад `UIFrame`, модальный стек, фокус/навигация, инпут-роутинг по слоям, data-bound ViewModels, токен-ориентированная темизация, пулы виджетов, виртуализированные списки.
- **Пайплайн:** Figma → (официальный экспериментальный коннектор) → UXML/USS + токены → wiring ViewModel → playtest → обратная связь. Fallback-импортёры vendored.
- **Производительность:** ≤4 draw calls для chrome, ≤1 ms UI CPU/кадр, виртуализация 10k+ элементов, пулинг, dirty-tracking, off-screen culling.

## Принципы (жёсткие)

1. Не делать UI тяжёлым/грузным/неоптимизированным (никаких WebView для core UI, никаких готовых «кривых» плагинов).
2. Своё крутое: retained-mode, web-inspired, токены, компонентная библиотека, FigCraft/Figma round-trip.
3. uGUI остаётся для world-space/3D UI до созревания UITK world-space.
4. **Milestone 1 (инструментарий) — без поломки основного проекта.** Всё делается рядом (sandbox). К Milestone 2 (замена UI в игре) не переходим без явного «go» от пользователя.
5. Производительность — first-class: бюджеты в CI.

## Статус

Исследование завершено. План скорректирован: **Milestone 1 — инструментарий и отладка (рядом, без поломок)**. Milestone 2 (замена UI в игре) — только после явного «go». Следующий шаг: утвердить план (04) и начать M1.1 (структура + sandbox).