# Project C: The Clouds — Agent Quick Reference

## 🎯 Какие агенты использовать для Project C

### 📍 Core Gameplay (основная разработка)

| Задача | Агент | Путь |
|--------|-------|------|
| Архитектура систем | `@unity-specialist` | `.qwenencode/agents/unity-specialist.md` |
| Контроллер персонажа/корабля | `@gameplay-programmer` | `.qwenencode/agents/gameplay-programmer.md` |
| Физика, URP, шейдеры | `@unity-shader-specialist` | `.qwenencode/agents/unity-shader-specialist.md` |
| Инвентарь, предметы | `@gameplay-programmer` + `@systems-designer` | |

### 🌐 Network & Multiplayer

| Задача | Агент | Путь |
|--------|-------|------|
| NGO, синхронизация | `@network-programmer` | `.qwenencode/agents/network-programmer.md` |
| Dedicated Server | `@network-programmer` + `@devops-engineer` | |
| Сетевая торговля | `@network-programmer` + `@economy-designer` | |

### 🎨 Art & Visual

| Задача | Агент | Путь |
|--------|-------|------|
| URP Pipeline | `@technical-artist` | `.qwenencode/agents/technical-artist.md` |
| CloudGhibli шейдер | `@unity-shader-specialist` | `.qwenencode/agents/unity-shader-specialist.md` |
| Визуальный стиль | `@art-director` | `.qwenencode/agents/art-director.md` |

### 🖥️ UI/UX

| Задача | Агент | Путь |
|--------|-------|------|
| HUD, меню, инвентарь | `@ui-programmer` | `.qwenencode/agents/ui-programmer.md` |
| UX флоу | `@ux-designer` | `.qwenencode/agents/ux-designer.md` |

### 🎮 Game Design

| Задача | Агент | Путь |
|--------|-------|------|
| Механики, системы | `@game-designer` | `.qwenencode/agents/game-designer.md` |
| Экономика, торговля | `@economy-designer` | `.qwenencode/agents/economy-designer.md` |
| Квесты, прогрессия | `@systems-designer` | `.qwenencode/agents/systems-designer.md` |
| Лор, мир | `@world-builder` | `.qwenencode/agents/world-builder.md` |

### 🔧 Production & Quality

| Задача | Навык | Как вызвать |
|--------|-------|-------------|
| Спринт | `sprint-plan` | "Спланируй спринт" |
| Code Review | `code-review` | "Сделай code-review для Assets/_Project/Scripts/..." |
| Performance | `perf-profile` | "Профилируй производительность" |
| Баг-репорт | `bug-report` | "Создай отчёт о баге" |

---

## 📁 Структура Project C (где что лежит)

```
Assets/_Project/
├── Art/              → @technical-artist, @art-director
├── Data/             → @unity-specialist (ScriptableObjects)
├── Editor/           → @tools-programmer
├── InputActions/     → @gameplay-programmer
├── Items/            → @gameplay-programmer, @systems-designer
├── Material/         → @unity-shader-specialist, @technical-artist
├── Prefabs/          → @unity-specialist
├── Resources/        → @unity-addressables-specialist
├── Scenes/           → @level-designer, @unity-specialist
├── Scripts/          → @lead-programmer, @gameplay-programmer
│   ├── UI/           → @ui-programmer
│   ├── Network/      → @network-programmer
│   ├── Items/        → @gameplay-programmer
│   └── Trade/        → @economy-designer
├── Settings/         → @technical-artist (URP)
├── Tests/            → @qa-tester
└── Trade/            → @economy-designer

docs/
├── gdd/              → @game-designer, @systems-designer
├── ART_BIBLE.md      → @art-director
├── QWEN_CONTEXT.md   → @producer (текущий статус)
└── MMO_Development_Plan.md → @producer, @technical-director
```

---

## ⚡ Quick Commands

### Начать новую фичу
```
1. "Определи текущий этап проекта" (project-stage-detect)
2. @game-designer "Спроектируй систему X"
3. @unity-specialist "Архитектура для системы X"
4. "Спланируй спринт" (sprint-plan)
```

### Реализовать код
```
1. @unity-specialist "Реализуй компонент X"
2. "code-review для Assets/_Project/Scripts/X.cs"
3. @qa-tester "Создай тест-кейсы"
```

### Командная работа
```
"team-combat для [механика]"
"team-ui для [UI элемент]"
"team-release для подготовки релиза"
```

---

## 🚨 Critical Rules for Project C

### URP (КРИТИЧНО)
- ❌ НЕ создавать URP ассеты через C#
- ✅ ТОЛЬКО через Unity Editor UI
- ✅ Pipeline Asset → Edit → Project Settings → Graphics
- ✅ UniversalRendererData (НЕ ForwardRendererData)

### Git Workflow
- ✅ Ветка: `qwen-gamestudio-agent-dev`
- ❌ НЕ пушить в `main` без разрешения
- ✅ Коммитить часто, маленькими изменениями

### Collaboration Protocol
- ✅ Пользователь принимает ВСЕ решения
- ✅ Показывать черновики перед записью
- ✅ Спрашивать "Могу ли я записать в [filepath]?"
- ❌ НЕ коммитить без инструкции

---

**Полный список агентов:** [game-studio/README.md](../game-studio/README.md)
**GDD каталог:** [docs/gdd/GDD_INDEX.md](../docs/gdd/GDD_INDEX.md)
**Текущий контекст:** [docs/QWEN_CONTEXT.md](../docs/QWEN_CONTEXT.md)
