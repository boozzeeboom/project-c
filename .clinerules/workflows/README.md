# Workflows для Cline + MiniMax

Набор готовых workflow-процессов для стандартных задач разработки.

---

## 📋 Доступные Workflows

| Workflow | Описание | Команда |
|----------|----------|---------|
| `codereview` | Проверка качества кода | `/workflow codereview` |
| `bugfix` | Стандартный процесс исправления багов | `/workflow bugfix` |
| `feature` | Разработка новой функциональности | `/workflow feature` |
| `sprint` | Планирование спринта | `/workflow sprint` |
| `test` | Тестирование изменений | `/workflow test` |

---

## 🚀 Быстрый старт

### Использование workflow

```
/workflow [название]
```

Примеры:
```
/workflow codereview Assets/_Project/Scripts/Player
/workflow bugfix Игрок не может открыть сундук
/workflow feature Система торговли
```

### Или используй сокращения

```
/code-review [path]   → codereview workflow
/bugfix [описание]    → bugfix workflow
/feature [описание]   → feature workflow
/sprint-plan          → sprint workflow
/test                 → test workflow
```

---

## 📝 Структура Workflow

Каждый workflow содержит:

1. **Metadata** — name, description, trigger
2. **Steps** — последовательность шагов
3. **Actions** — что делать на каждом шаге
4. **Output** — ожидаемый результат

---

## 🔧 Кастомизация

Workflows можно адаптировать под проект в `.clinerules/workflows/`.

Структура:
```
workflows/
├── README.md
├── codereview-workflow.md
├── bugfix-workflow.md
├── feature-workflow.md
├── sprint-workflow.md
└── test-workflow.md
```

---

## 📖 Подробная документация

- [codereview-workflow.md](codereview-workflow.md) — Проверка кода
- [bugfix-workflow.md](bugfix-workflow.md) — Исправление багов
- [feature-workflow.md](feature-workflow.md) — Разработка фич
- [sprint-workflow.md](sprint-workflow.md) — Планирование спринтов
- [test-workflow.md](test-workflow.md) — Тестирование