# Design Tooling: Figma Free vs Alternatives vs «свой визуализатор»

**Контекст:** нет платной Figma. Нужно понять, хватит ли бесплатной, или строить свой инструмент (Electron и т.п.), и как вписать FigCraft.

---

## 1. Figma Free (Starter) — хватит ли?

**Лимиты (официально, 2026):**
- 1 команда, 1 папка.
- **3 Figma Design/Sites файла** в командной папке (плюс 3 FigJam, 3 Slides).
- **Неограниченные черновики (drafts)** — не считаются в лимит 3.
- Редактор полноценный: auto layout, components, styles, prototypes, vector networks, variables (токены).
- Общие библиотеки (Team Library) — только платные.
- Dev Mode расширенный / MCP Server — только платные (базовый inspect доступен бесплатно).
- История версий — 30 дней.
- Права — coarse (Editor/Viewer).

**Вывод для Project C:**
- **Для соло-дизайнера/разработчика — да, хватит.** Неограниченные drafts снимают проблему «3 файла»: храните большинство дизайнов в drafts, а в командной папке держите 3 «живых» файла (например: Design System, Screens A, Screens B).
- **Официальный UI Toolkit Connector работает на бесплатном плане** для токенов (variables существуют на всех планах; `tokens.uss` приходит всегда; theming зависит от modes — modes доступны, но продвинутые фичи могут быть платными).
- **Ограничение:** без Team Library нельзя шарить компоненты между файлами автоматически; придётся дублировать или использовать copy/paste. Для одного человека это терпимо.
- **Рекомендация:** начать с Figma Free + drafts. Если упрётесь в лимиты (нужна библиотека/команда/Dev Mode) — апгрейд на Professional ($16/мес за full seat) оправдан.

---

## 2. Альтернативы Figma

### 2.1 Penpot (open source, self-hosted)
- **Что:** open-source дизайн-платформа (Clojure/ClojureScript), self-hosted, работает в браузере, SVG/CSS/HTML/JSON как открытые стандарты.
- **Плюсы:** бесплатно, без лимитов файлов, self-hosted (данные у вас), design tokens, components/variants, MCP-сервер, экспорт SVG/PNG/JPG/PDF.
- **Интеграция с Unity:** нет прямого импорта в UXML/USS. Экспорт SVG/PNG → ручная/полуручная сборка в Unity. Есть Penpot Exporter для Figma (миграция), но не для Unity.
- **Вывод:** хорошая бесплатная альтернатива Figma для дизайна и токенов; но пайплайн в Unity остаётся ручным (экспорт ассетов + UXML/USS руками или через скрипты). Подходит, если не хочется зависеть от Figma.

### 2.2 Lunacy (Icons8, free)
- **Что:** бесплатный десктопный дизайн-инструмент (Windows/Mac/Linux), поддерживает .fig, экспорт PNG/SVG/PDF/CSS, встроенные иконки/фото/иллюстрации.
- **Плюсы:** бесплатно, офлайн, открывает Figma-файлы, экспорт кода (CSS/HTML), встроенные ассеты.
- **Интеграция с Unity:** нет прямого импорта; экспорт SVG/PNG + ручная сборка.
- **Вывод:** удобная бесплатная альтернатива для индивидуальной работы и открытия .fig; но без Unity-пайплайна.

### 2.3 FigCraft (open source, alpha)
- **Что:** open-source reimplementation Figma-подобного инструмента на Rust (MIT/Apache-2.0), нативный (Win/Mac/Linux/FreeBSD/Web), без Electron, MCP-сервер, CLI, Dev Mode с генерацией кода (CSS/HTML/React/Vue/Leptos/SwiftUI/Compose/ArkUI/Flutter), экспорт PNG/SVG/PDF, поддержка .fig (best effort), variables, components, variants, auto layout, RTL, screen reader accessibility.
- **Плюсы:** бесплатно, open source, MCP-интеграция (агенты могут управлять), Dev Mode с кодом, экспорт ассетов, приватность (офлайн).
- **Минусы:** **ранняя alpha (~49% parity)**, .fig открывается best effort, нет нативного экспорта в Unity/UXML/USS, возможны rough edges.
- **Вывод:** интересен для экспериментов и agent-driven дизайна, но не для продакшена. Можно тестировать параллельно с Figma/Penpot. Не заменяет Figma для критичной работы.

### 2.4 Свой визуализатор на Electron / Tauri / и т.п.
- **Что:** написать собственный дизайн-инструмент.
- **Плюсы:** полный контроль, кастомный workflow, интеграция с Unity (экспорт UXML/USS напрямую).
- **Минусы:** **огромная трата времени** (months/years), дублирование Figma/Penpot, поддержка, баги. Не оправдано, пока не доказана необходимость.
- **Вывод:** **не рекомендуется.** Лучше использовать существующие инструменты + Unity UI Builder. Свой визуализатор имеет смысл только если нужен узкоспециализированный workflow, который нельзя покрыть существующими инструментами.

---

## 3. Unity UI Builder — встроенный визуальный редактор (бесплатный)

- **Что:** встроенный в Unity редактор UXML/USS (Window → UI Toolkit → UI Builder). Drag-and-drop элементов, визуальное редактирование стилей, live preview.
- **Плюсы:** бесплатно, нативно, сразу генерирует UXML/USS, интегрирован с проектом, работает офлайн, версионируется в git.
- **Минусы:** нет дизайна-прототипирования уровня Figma, нет совместной работы, нет токен-системы из коробки (но USS variables поддерживаются).
- **Вывод:** **для соло-разработчика UI Builder + USS tokens + компонентная библиотека может полностью заменить Figma** для создания UI. Figma/Penpot нужны только для визуального дизайна, прототипирования, итерации с артистами. Если дизайн делает сам разработчик — UI Builder достаточно.

---

## 4. Рекомендация (порядок действий) — FigCraft как primary

Пользователь уже использует FigCraft (скачан, запущен). FigCraft — open source, rapidly развивается, имеет MCP/CLI/Dev Mode. Поэтому **думаем в первую очередь под него**, Figma Free — вторичный/резервный.

1. **FigCraft — primary design tool.** Использовать для дизайна всех экранов, компонентов, токенов, прототипов. Преимущества: офлайн, приватность, MCP (агенты могут управлять), Dev Mode с кодогенерацией, экспорт ассетов, .fig import (best effort).
2. **Figma Free + drafts — резерв/совместимость.** Если нужен импорт .fig из Figma-экосистемы или коллаборация с внешними дизайнерами. Официальный коннектор — опционально.
3. **Unity UI Builder — основной инструмент реализации.** Сборка UXML/USS из дизайнов FigCraft (через экспорт ассетов + ручной/полуручной перенос стилей). UI Builder даёт live preview и версионирование.
4. **Penpot/Lunacy** — fallback для self-hosted/офлайн, если FigCraft не покрывает нужды.
5. **Не строить свой визуализатор** — FigCraft уже покрывает 90% нужд; свой инструмент не оправдан.

### 4.1 Интеграция FigCraft → Unity (пайплайн)

FigCraft не экспортирует напрямую в UXML/USS, но предоставляет достаточно данных для полуавтоматического пайплайна:

| FigCraft capability | Как использовать в Unity |
|---|---|
| **variable.export** (JSON/CSS/Tailwind/Dart) | Экспорт токенов → генератор `DesignTokens.uss` + `ThemeData`. CSS-переменные (`--*`) мапятся 1:1 в USS custom properties. Tailwind → можно использовать как референс для utility-классов. |
| **file.exportLayers** (PNG/SVG/PDF) | Экспорт иконок/спрайтов → Sprite Atlas → `-unity-background-image-tint-color` для тинтинга. SVG → конвертация в PNG (или Vector Graphics, если нужно). |
| **file.exportSvg** | Экспорт векторных иконок → PNG/SVG → Sprite Atlas. |
| **inspect.code** (CSS/HTML/React/Leptos/Vue/Tailwind/SwiftUI/Compose/ArkUI/Flutter) | CSS-вывод использовать как референс для USS (flex-direction, padding, gap, colors). Не копировать 1:1, но брать значения. |
| **codeConnect.export/import** | Экспорт связей компонентов → JSON (`figcraft.connect.json`) → можно использовать для маппинга FigCraft-компонентов на Unity-контролы (custom importer). |
| **MCP сервер** (`figcraft-cli mcp --attach <port>`) | Агенты могут управлять FigCraft: создавать/редактировать дизайны, экспортировать ассеты, читать свойства. Интеграция в workflow: дизайнер/агент → FigCraft → экспорт → Unity. |
| **CLI** (`figcraft-cli run`, `render`, `export`) | Автоматизация: headless-рендер, экспорт, скрипты. Можно встроить в CI/CD (экспорт ассетов при изменении дизайна). |
| **Dev Mode** (measurements, redlines, ready-for-dev) | Разработчик смотрит точные размеры/отступы/цвета → переносит в USS. |

### 4.2 Автоматизация (будущее)

- **Скрипт-конвертер:** FigCraft CSS export → USS (парсинг CSS custom properties и правил → генерация .uss). Можно написать Python/C# скрипт, который берёт CSS-вывод `inspect.code` и конвертирует в USS.
- **MCP-интеграция:** агент может экспортировать токены/ассеты из FigCraft и автоматически обновлять `DesignTokens.uss` в Unity-проекте.
- **CI-шаг:** при изменении дизайна в FigCraft → экспорт → регенерация токенов → smoke-тест парсинга UXML/USS.

### 4.3 Ограничения FigCraft (alpha)

- .fig импорт — best effort (может потерять данные).
- Нет нативного Unity/UXML/USS экспорта (нужен ручной/полуручной перенос).
- Нет shared libraries (как Figma Team Library) — компоненты дублируются между файлами.
- Возможны rough edges в UI/рендере.
- **Митигация:** использовать FigCraft для дизайна, но финальную сборку делать в Unity UI Builder; держать дизайн-систему в Unity как single source of truth для реализации.

---

## 5. Пайплайн без Figma (если выберете UI Builder / Penpot / Lunacy)

- **Дизайн-токены:** хранить в `DesignTokens.uss` + `ThemeData` ScriptableObject. В UI Builder можно редактировать USS variables вручную или через скрипт-генератор из JSON.
- **Компоненты:** UXML templates + USS classes в `DesignSystem/`. Переиспользование через UXML `<Template>`/`<Instance>`.
- **Ассеты (иконки, спрайты):** экспорт PNG/SVG из любого инструмента → Sprite Atlas → `-unity-background-image-tint-color` для тинтинга.
- **Итерация:** UI Builder live preview + hot reload UXML/USS (где возможно). Тестирование в Play Mode.

---

## 6. Обновление плана (04_IMPLEMENTATION_PLAN.md)

- Phase 0: добавить задачу «UI Builder as primary authoring tool» — обучение команды, создание Design System в UI Builder.
- Figma connector trial — опционально (если Figma Free хватает). Если нет — пайплайн через UI Builder + ручной USS tokens.
- Добавить fallback: Penpot/Lunacy для дизайна, если Figma Free лимиты мешают.

---

*Источники: Figma Starter plan overview (help.figma.com), Figma pricing (figma.com/pricing), Penpot (github.com/penpot/penpot), FigCraft (github.com/storytold/figcraft), Unity UI Builder docs.*