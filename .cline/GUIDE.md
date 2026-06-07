# 🎯 Полное руководство по использованию системы

---

## ⚡ АВТОМАТИЧЕСКИ (без действий пользователя)

### 🔗 Hooks — автоматические триггеры

| Hook | Когда срабатывает | Что делает |
|------|-------------------|------------|
| `session-start` | При начале новой сессии | Показывает branch, recent commits, recovery state |
| `session-stop` | При завершении сессии | Напоминает сохранить состояние |
| `detect-gaps` | При начале сессии | Ищет пропущенную документацию |
| `validate-commit` | Перед `git commit` | Проверяет TODO/FIXME, magic numbers |
| `validate-push` | Перед `git push` | Предупреждает о protected branches |
| `validate-assets` | После Write/Edit | Проверяет naming conventions, JSON |

**Ничего делать не нужно** — всё автоматически.

---

## 🎮 ВЫЗЫВАТЬ ВРУЧНУЮ

### 📦 Agents — роли для специализированных задач

**Как вызвать:** `@agent-name`

| Агент | Когда использовать |
|-------|---------------------|
| `@unity-specialist` | Архитектура Unity, URP настройка, MonoBehaviour vs ECS |
| `@gameplay-programmer` | Player controller, inventory, abilities, combat |
| `@network-programmer` | NGO, синхронизация, Floating Origin, dedicated server |
| `@technical-artist` | Шейдеры, VFX, CloudGhibli, URP pipeline |
| `@ui-programmer` | Меню, HUD, InventoryWheel, trade UI |

**Примеры:**
```
@unity-specialist "Настрой URP для лучшей производительности"
@gameplay-programmer "Реализуй систему гравитационных зон"
@network-programmer "Исправь desync при Floating Origin shift"
@ui-programmer "Добавь drag-and-drop в инвентарь"
@technical-artist "Оптимизируй CloudGhibli shader"
```

---

### 🔧 Skills — навыки для конкретных задач

**Как вызвать:** `/skill-name` или `/навык`

| Skill | Когда использовать | Пример |
|-------|-------------------|--------|
| `/code-review` | Проверить качество кода | `/code-review Assets/_Project/Scripts/Player` |
| `/sprint-plan` | Спланировать спринт | `/sprint-plan new` |
| `/project-stage-detect` | Определить этап проекта | `/project-stage-detect` |
| `/tech-debt` | Найти технический долг | `/tech-debt` |
| `/brainstorm` | Сгенерировать идеи | `/brainstorm "система торговли"` |

**Доступные навыки:** смотри `.clinerules/skills/INDEX.md`

---

### 🔄 Workflows — процессы для стандартных задач

**Как вызвать:** `/workflow [название]`

| Workflow | Описание | Пример |
|----------|----------|--------|
| `/workflow codereview` | Полная проверка кода | `/workflow codereview Assets/_Project/Scripts/` |
| `/workflow bugfix` | Исправление бага | `/workflow bugfix Игрок не может открыть сундук` |
| `/workflow feature` | Разработка фичи | `/workflow feature Система торговли` |
| `/workflow sprint` | Планирование спринта | `/workflow sprint` |
| `/workflow test` | Тестирование | `/workflow test После изменений в сети` |

---

## 🔑 БЫСТРЫЕ КОМАНДЫ (сокращения)

| Полная команда | Сокращение | Описание |
|----------------|------------|----------|
| `/workflow codereview [path]` | `/code-review [path]` | Проверка кода |
| `/workflow bugfix [описание]` | `/bugfix [описание]` | Исправление бага |
| `/workflow feature [описание]` | `/feature [описание]` | Разработка фичи |
| `/workflow sprint` | `/sprint-plan` | Планирование |
| `/workflow test` | `/test` | Тестирование |
| `/skill project-stage-detect` | `/stage` | Этап проекта |

---

## 📋 ЧТО КОГДА ИСПОЛЬЗОВАТЬ

### При начале работы с файлами
```
→ Автоматически: hooks проверяют naming, commit validation
→ Вручную: /code-review если нужно глубокую проверку
```

### При исправлении бага
```
→ /workflow bugfix "описание бага"
→ Следует процессу: понять → найти причину → исправить → протестировать
```

### При разработке новой фичи
```
→ /workflow feature "новая функция"
→ Следует процессу: требования → архитектура → реализация → тесты → review
```

### При планировании
```
→ /sprint-plan new — новый спринт
→ /sprint-plan status — текущий статус
```

### При непонятном состоянии проекта
```
→ /project-stage-detect — определит этап и покажет gaps
```

---

## 🏗️ АРХИТЕКТУРА СИСТЕМЫ

```
.cline/
├── clinerules.json     ← Конфиг: 39 агентов + hooks
├── CLAUDE.md           ← Мой системный промпт
└── README.md           ← Инструкции

.clinerules/
├── agents/             ← 5 ключевых ролей (из 39)
├── rules/              ← 5 правил кода (автоматич. примен.)
├── skills/             ← 5 навыков (вызывать вручную)
├── hooks/              ← 6 автоматических триггеров
├── workflows/          ← 5 процессов (вызывать вручную)
└── docs/              ← Документация
```

---

## 🎯 ПРИНУДИТЕЛЬНОЕ ВЫЗОВАНИЕ

Если что-то не срабатывает автоматически:

1. **Agents:** Просто напиши `@agent-name` — подхватит из `.clinerules/agents/`

2. **Skills:** Напиши `/skill-name` — ищет в `.clinerules/skills/`

3. **Workflows:** Напиши `/workflow workflow-name` или используй shortcut

4. **Rules:** Автоматически применяются по путям в frontmatter `paths:`

5. **Hooks:** Срабатывают по событиям в `clinerules.json`

---

## 🚨 ОБЫЧНЫЕ СЦЕНАРИИ

### "Проверь код перед коммитом"
```
/code-review Assets/_Project/Scripts/Player
```

### "Нужно исправить баг с сетью"
```
@network-programmer "Исправь desync"
или
/workflow bugfix "desync при origin shift"
```

### "Спланируй следующий спринт"
```
/sprint-plan new
или
/workflow sprint
```

### "Не понимаю где проект"
```
/project-stage-detect
```

### "Нужна архитектура для фичи"
```
@unity-specialist "Спроектируй систему X"
```

---

## 📊 СВОДКА

| Тип | Автоматически? | Как вызвать |
|-----|----------------|-------------|
| **Hooks** | ✅ Да | Никак — по событиям |
| **Rules** | ✅ Да | Никак — по путям файлов |
| **Agents** | ❌ Нет | `@agent-name` |
| **Skills** | ❌ Нет | `/skill-name` |
| **Workflows** | ❌ Нет | `/workflow name` |

---

**Версия:** 1.0 | **Обновлено:** 2026-04-15