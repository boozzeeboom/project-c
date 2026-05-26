# Project C — Skills Usage Guide

## 🎯 Какие навыки когда использовать

### 📋 Проектирование и Планирование

| Навык | Когда использовать | Пример |
|-------|-------------------|--------|
| `brainstorm` | Новая идея/механика | "Проведи brainstorm для системы погоды" |
| `map-systems` | Декомпозиция фичи | "Декомпозируй систему крафта" |
| `sprint-plan` | Начать спринт | "Спланируй спринт для UI системы" |
| `estimate` | Оценка задач | "Оцени задачи для этапа 3" |

### 🛠️ Реализация

| Навык | Когда использовать | Пример |
|-------|-------------------|--------|
| `prototype` | Быстрая проверка идеи | "Прототип двойного прыжка" |
| `reverse-document` | Документация из кода | "Создай документацию из Assets/_Project/Scripts/" |
| `code-review` | После реализации | "code-review для Assets/_Project/Scripts/Network/" |
| `design-review` | Проверка дизайна системы | "design-review для docs/gdd/GDD_11_Inventory_Items.md" |

### 🎮 Game Design

| Навык | Когда использовать | Пример |
|-------|-------------------|--------|
| `balance-check` | Проверка баланса | "Проверь баланс экономики торговли" |
| `playtest-report` | После тестирования | "Создай отчёт плейтеста кораблей" |
| `onboard` | Новый разработчик | "Проведи onboard для Project C" |

### 🔍 Качество и Оптимизация

| Навык | Когда использовать | Пример |
|-------|-------------------|--------|
| `perf-profile` | Проблемы FPS | "Профилируй производительность сцены" |
| `asset-audit` | Проверка ассетов | "Проведи аудит ассетов в Assets/_Project/Art/" |
| `tech-debt` | Анализ техдолга | "Проанализируй технический долг" |
| `bug-report` | Нашли баг | "Создай отчёт о баге с инвентарём" |

### 🚀 Релиз и Деплой

| Навык | Когда использовать | Пример |
|-------|-------------------|--------|
| `release-checklist` | Подготовка релиза | "Создай чек-лист релиза v0.0.14" |
| `changelog` | После изменений | "Составь лог изменений" |
| `patch-notes` | Публикация | "Напиши заметки к патчу" |
| `hotfix` | Срочное исправление | "Исправь хотфикс для сетевого бага" |

### 👥 Командная работа (Оркестрация)

| Навык | Когда использовать | Пример |
|-------|-------------------|--------|
| `team-combat` | Боевая система | "team-combat для combat с дроном" |
| `team-ui` | Сложный UI | "team-ui для инвентаря с drag-and-drop" |
| `team-release` | Релиз | "team-release для v0.0.14" |
| `team-polish` | Полировка | "team-polish для визуала" |
| `team-narrative` | Сюжет | "team-narrative для арки Гильдии" |

---

## 🎯 Project C — Рекомендуемые Workflow

### Новый проект (уже начат)

```
Текущий статус: Этап 2.5 (Визуальный прототип)

Следующие шаги:
1. "Проведи project-stage-detect" — подтвердить этап
2. "Спланируй sprint-plan" — план на спринт
3. @unity-specialist "Реализуй модель корабля"
4. @technical-artist "Настрой CloudGhibli шейдер"
5. "code-review для Assets/_Project/Scripts/"
```

### Новая фича

```
1. @game-designer "Спроектируй систему X"
2. "Выполни map-systems для системы X"
3. @unity-specialist "Архитектура"
4. "Сделай prototype механики"
5. "Спланируй sprint-plan"
6. Реализация
7. "code-review"
8. @qa-tester "Тест-кейсы"
```

### Исправление бага

```
1. @qa-tester "Анализ бага"
2. "Создай bug-report"
3. @unity-specialist "Исправление"
4. "code-review"
5. Тестирование
```

---

## 📁 Где живут навыки

Все навыки находятся в `.qwenencode/skills/` (junction → `game-studio/.qwenencode/skills/`)

```
.qwenencode/skills/
├── code-review/SKILL.md
├── design-review/SKILL.md
├── sprint-plan/SKILL.md
├── brainstorm/SKILL.md
├── perf-profile/SKILL.md
├── bug-report/SKILL.md
├── team-combat/SKILL.md
├── team-ui/SKILL.md
├── team-release/SKILL.md
└── ... (37 всего)
```

---

## ⚡ Быстрый старт

**Минимальный набор для работы:**

```
1. brainstorm        — генерация идей
2. sprint-plan       — планирование
3. code-review       — проверка кода
4. bug-report        — отчёты о багах
5. changelog         — лог изменений
```

**Для командной работы:**

```
+ team-combat        — боевые системы
+ team-ui            — UI/UX
+ team-release       — релизы
+ design-review      — проверка дизайна
```

---

**Полный список навыков:** [game-studio/README.md](../game-studio/README.md)
**Примеры workflows:** [game-studio/docs/examples/](../game-studio/docs/examples/)
