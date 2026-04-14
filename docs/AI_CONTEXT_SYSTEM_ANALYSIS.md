# 🎯 AI Context Memory System — Анализ и Рекомендации

**Проект:** Project C: The Clouds  
**Дата:** 2026-04-15  
**Статус:** Исследование

---

## 📊 Текущее Состояние

### Что есть сейчас:

| Компонент | Файл | Размер | Назначение |
|-----------|------|--------|------------|
| **Основной контекст** | `docs/QWEN_CONTEXT.md` | 910 строк | Полная история проекта, системы, проблемы |
| **Системный промпт** | `.cline/CLAUDE.md` | 133 строки | Краткий контекст + правила |
| **Агенты** | `.clinerules/agents/*.md` | 39 файлов | Роли специалистов |
| **Правила** | `.clinerules/rules/*.md` | 11 файлов | Стандарты кода |
| **Навыки** | `.clinerules/skills/*.md` | 37 файлов | Workflows |
| **Hooks** | `.clinerules/hooks/*.sh` | 6 файлов | Автоматические триггеры |

### Проблемы текущей системы:

| # | Проблема | Влияние |
|---|----------|---------|
| 1 | **QWEN_CONTEXT.md — монолитный файл** | 910 строк загружаются каждый раз, контекст "распухает" |
| 2 | **Нет分层 (tiered) индексации** | AI не знает что важно для текущей задачи |
| 3 | **Устаревший контекст** | Код меняется быстрее чем документация |
| 4 | **Нет авто-обновления** | После каждой сессии нужно вручную править QWEN_CONTEXT.md |
| 5 | **Хуки примитивные** | session-start/stop только показывают branch, не анализируют |
| 6 | **Архив Old_sessions/** | 20+ файлов сессий — иногда нужны, но не ищутся |
| 7 | **Нет приоритизации** | Все файлы равны, критичные правила URP теряются |

---

## 🔬 Анализ Best Practices

### Подход 1: Tiered Memory (Google, Anthropic Best Practices)

```
┌─────────────────────────────────────────┐
│  L1: Hot Memory (CLAUDE.md)              │  ← ~100 строк, КРИТИЧНОЕ
│     • URP правила                        │
│     • Collaboration Protocol             │
│     • Текущий этап                      │
├─────────────────────────────────────────┤
│  L2: Warm Memory (QWEN_CONTEXT.md)      │  ← ~500 строк, АКТУАЛЬНОЕ
│     • Что сделано                        │
│     • Следующие шаги                    │
│     • Известные проблемы                 │
├─────────────────────────────────────────┤
│  L3: Cold Memory (docs/**/*.md)         │  ← ЛЮБОЙ размер, ПОИСК
│     • GDD документы                      │
│     • История сессий                     │
│     • Архитектура                        │
└─────────────────────────────────────────┘
```

**Плюсы:** ✅ Легко понять приоритеты  
**Минусы:** ❌ Требует синхронизации между уровнями

---

### Подход 2: Semantic Index (VS Code Agent Toolkit)

```
AI Memory Index/
├── INDEX.md                    # Корневой указатель
├── systems/
│   ├── network.idx             # Теги: [network, NGO, RPC, sync]
│   ├── ui.idx                  # Теги: [UI, HUD, inventory, trade]
│   └── gameplay.idx            # Теги: [movement, combat, ship]
├── api/
│   └── unity.idx               # Теги: [URP, Shader, Camera]
└── sessions/
    └── session-latest.idx      # Теги: [active, current-sprint]
```

**Плюсы:** ✅ Быстрый поиск по тегам  
**Минусы:** ❌ Сложнее поддерживать

---

### Подход 3: Session-Recovery Pattern (Cline Default Enhanced)

```
.cline/
├── memory/                     # Векторная память (future)
├── recovery/
│   └── active-session.md       # Авто-сохранение состояния
├── hooks/
│   ├── session-start.sh        # Улучшенный: анализ git diff
│   └── session-stop.sh         # Авто-обновление recovery
└── CLAUDE.md                   # Краткий, всегда в контексте
```

**Плюсы:** ✅ Контекст всегда актуален  
**Минусы:** ❌ Требует надежного hook execution

---

## 🎯 Рекомендуемые Реализации

### Вариант A: "Умный Recovery" (⭐ Рекомендую)

**Принцип:** Hooks генерируют контекст автоматически, AI читает только актуальное.

```
.cline/
├── hooks/
│   ├── session-start.sh        # Анализирует: git diff, TODO, session-log
│   └── session-stop.sh          # Пишет: session-recovery.md
├── session-recovery.md         # Авто-ген: 1-2 страницы max
└── CLAUDE.md                   # 100 строк max

docs/
├── context/                    # Тематические индексы
│   ├── network.md              # Только для network задач
│   ├── ui.md                  # Только для UI задач
│   └── ship.md                # Только для ship задач
└── QWEN_CONTEXT.md            # АРХИВ (не читать автоматически)
```

**Hooks enhancement:**

```bash
# session-stop.sh - авто-генерация recovery
git diff --stat HEAD > session-recovery.md
echo "## Session Summary" >> session-recovery.md
grep "^##" session-log.md | tail -5 >> session-recovery.md
echo "## Key Files Changed" >> session-recovery.md
git diff --name-only | head -10 >> session-recovery.md
```

---

### Вариант B: "Документация по Запросу" (On-Demand)

**Принцип:** AI загружает документацию только когда нужно.

```
# В CLAUDE.md:
## 📚 Documentation On-Demand
When working on [topic], read the relevant doc:
- Network: `docs/NETWORK_*.md`
- UI: `docs/QWEN-UI-AGENTIC-SUMMARY.md`
- Ship: `docs/SHIP_SYSTEM_*.md`

# Hook загружает только:
- Git status
- Last session recovery
- Current branch
```

**Плюсы:** ✅ Просто реализовать  
**Минусы:** ❌ AI должен "знать что спросить"

---

### Вариант C: "Гибридная Система" (Best of Both)

Комбинация A + B:

```
1. CLAUDE.md — 100 строк MAX (hot memory)
   └─ Краткое описание + ссылки на тематические .md

2. .cline/session-recovery.md — авто-ген, 50 строк
   └─ Что сделано в этой сессии

3. docs/context/*.md — 5-10 файлов по системам
   └─ network-context.md, ui-context.md, ship-context.md

4. docs/QWEN_CONTEXT.md — АРХИВ для recovery
   └─ Только при restore после сбоя
```

---

## 📋 Implementation Checklist

### Для Варианта A (Рекомендуемый):

- [ ] 1. Создать `.cline/session-recovery.md` шаблон
- [ ] 2. Улучшить `session-start.sh` — анализ recovery файла
- [ ] 3. Улучшить `session-stop.sh` — авто-генерация recovery
- [ ] 4. Создать тематические `.cline/tags/*.md`
- [ ] 5. Сократить `CLAUDE.md` до 100 строк
- [ ] 6. Создать `docs/context/network.md`, `ui.md`, `ship.md`
- [ ] 7. Обновить `clinerules.json` hooks

### Для Варианта C (Гибрид):

- [ ] 1-3 из A
- [ ] 5 из A
- [ ] 7. Создать `docs/context/` с индексами
- [ ] 8. Удалить/архивировать старые сессии в `/archive`

---

## 🔧 Детальный План: Вариант A

### Файл 1: `.cline/session-recovery.md` (шаблон)

```markdown
# Session Recovery — {{DATE}}

## Current Sprint
- **Sprint:** {{NUMBER}}
- **Phase:** {{PHASE}}
- **Goal:** {{GOAL}}

## Last Session ({{DATE}})
### Completed
- {{TASK 1}}
- {{TASK 2}}

### Next Steps
1. {{STEP 1}}
2. {{STEP 2}}

## Active Problems
| Priority | Issue | File |
|----------|-------|------|
| P0 | {{PROBLEM}} | {{FILE}} |

## Recently Changed
```
{{GIT DIFF STAT}}
```

## Key Context
> {{IMPORTANT NOTE FOR NEXT SESSION}}
```

### Файл 2: Улучшенный `session-start.sh`

```bash
#!/bin/bash
echo "=== Project C Session Start ==="

# Branch & commits
echo "Branch: $(git rev-parse --abbrev-ref HEAD)"
echo ""
echo "Recent commits:"
git log --oneline -3

# Check recovery
if [ -f ".cline/session-recovery.md" ]; then
    echo ""
    echo "=== RECOVERY FILE FOUND ==="
    head -20 .cline/session-recovery.md
fi

# Hot files check
echo ""
echo "=== CRITICAL FILES (check if modified) ==="
git status --porcelain | grep -E "\.md$|\.cs$" | head -5
```

### Файл 3: Улучшенный `session-stop.sh`

```bash
#!/bin/bash
echo "=== Session End: Saving State ==="

DATE=$(date '+%Y-%m-%d %H:%M')

# Update recovery file
cat > .cline/session-recovery.md << EOF
# Session Recovery — $DATE

## Last Session
Updated: $DATE

## Changed Files
$(git diff --name-only --staged 2>/dev/null || echo "Nothing staged")
$(git diff --name-only 2>/dev/null | head -10)

## TODO for Next Session
1. Continue from where we stopped
2. Check git status before starting
EOF

echo "Recovery saved to .cline/session-recovery.md"
```

---

## 🎯 Финальные Рекомендации

### Выбери подход:

| Подход | Сложность | Эффект | Когда использовать |
|--------|-----------|--------|-------------------|
| **A: Умный Recovery** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Для активной разработки |
| **B: On-Demand** | ⭐ | ⭐⭐⭐ | Для простых проектов |
| **C: Гибрид** | ⭐⭐ | ⭐⭐⭐⭐ | Лучший баланс |

### Для Project C рекомендую **Вариант A** или **Вариант C**:

1. **Немедленно:** Сократить `CLAUDE.md` до 100 строк
2. **Немедленно:** Создать `session-recovery.md` 
3. **Немедленно:** Улучшить hooks
4. **Потом:** Создать тематические индексы в `docs/context/`

---

## 📝 Результат Subagent Research

### Контекст использования:
- Peak usage: **84,200 / 192,000 tokens (43.9%)** — это нормально для сложных задач
- Для простых задач: ~3-8K tokens
- Для тяжелых (рефакторинг): до 50-60K tokens

### Key Insight:
> "Беседы эфемерны и могут быть сжаты. Файлы — нет."

**Файловая память важнее чем история чата!**

---

**Хочешь чтобы я реализовал один из вариантов? Какой выбираешь?**

1. **A** — Полная система с умными hooks
2. **B** — Минимальный on-demand подход  
3. **C** — Гибрид (рекомендую)
