# Project C: The Clouds — Agents & Skills

**39 специализированных агентов** и **37 навыков** для полного цикла разработки игры Project C на Unity.

---

## 📁 Структура

```
.agents/
├── agents/           # 39 файлов — определения агентов
├── skills/           # 37 папок — определения навыков
├── rules/            # 11 файлов — стандарты кода
├── docs/             # Справочная документация
├── hooks/            # (не используются в Qwen Code)
├── PROJECT_C_AGENT_GUIDE.md  # Какие агенты для каких задач Project C
└── SKILLS_USAGE_GUIDE.md     # Как использовать навыки
```

---

## 🎯 Как использовать

### Вызов агента

Напишите `@имя-агента` с задачей. Qwen Code найдёт файл `.agents/agents/{имя}.md` и переключится в эту роль:

```
@unity-specialist "Сделай архитектуру системы инвентаря на ScriptableObject"
@game-designer "Спроектируй систему крафта с открытием рецептов"
@network-programmer "Настрой NGO синхронизацию для торговли"
@technical-artist "Настрой CloudGhibli шейдер для URP"
```

### Использование навыка

Напишите название навыка в запросе. Qwen Code найдёт `.agents/skills/{навык}/SKILL.md` и выполнит инструкции:

```
Проведи brainstorm для "система погоды"
Выполни sprint-plan для UI системы
Сделай code-review для Assets/_Project/Scripts/Network/
Спланируй sprint-plan для первого спринта
```

> **Совет:** Можно просто описать задачу словами — Qwen Code сам определит нужного агента или навык.

---

## 👥 Доступные агенты (39)

### Директора (Tier 1) — Стратегия
| Агент | Роль | Project C использование |
|-------|------|-------------------------|
| `@creative-director` | Видение, пиллары, тон | Визуальный стиль Ghibli + Sci-Fi |
| `@technical-director` | Архитектура, техдолг | MMO архитектура, Unity 6 URP |
| `@producer` | Расписание, скоуп | Этапы разработки, roadmap |

### Лиды (Tier 2) — Управление отделами
| Агент | Роль | Project C использование |
|-------|------|-------------------------|
| `@game-designer` | Механики, системы | Core Loop, физика кораблей |
| `@lead-programmer` | Архитектура кода | Стандарты C#, качество |
| `@art-director` | Визуальный стиль | docs/ART_BIBLE.md |
| `@audio-director` | Звуковой дизайн | AudioMixer, SFX, музыка |
| `@narrative-director` | История, мир | docs/WORLD_LORE_BOOK.md |
| `@qa-lead` | Стратегия тестирования | Тест-план для мультиплеера |
| `@release-manager` | Релиз, деплой | Версии v0.0.x |
| `@localization-lead` | Локализация | Русский → English |

### Программисты (Tier 3) — Реализация
| Агент | Роль | Project C папки |
|-------|------|-----------------|
| `@gameplay-programmer` | Движение, инвентарь | `Assets/_Project/Scripts/`, `Items/` |
| `@engine-programmer` | Рендеринг, физика | `Assets/_Project/Settings/`, `Material/` |
| `@ai-programmer` | Pathfinding, AI | (будущие NPC) |
| `@network-programmer` | Мультиплеер, NGO | `Assets/_Project/Scripts/Network/`, `Trade/` |
| `@tools-programmer` | Редакторы, утилиты | `Assets/_Project/Editor/` |
| `@ui-programmer` | Меню, HUD | `Assets/_Project/Scripts/UI/` |

### Дизайнеры
| Агент | Роль | Project C документы |
|-------|------|---------------------|
| `@systems-designer` | Подсистемы, формулы | `docs/gdd/GDD_20_Progression_RPG.md` |
| `@level-designer` | Уровни, энкаунтеры | 15 пиков, 4 города |
| `@economy-designer` | Валюта, цены | `docs/gdd/GDD_22_Economy_Trading.md` |
| `@ux-designer` | UX-флоу, юзабилити | `docs/gdd/GDD_13_UI_UX_System.md` |
| `@prototyper` | Быстрые прототипы | Проверка механик |
| `@performance-analyst` | Профилирование | FPS, память, сеть |

### Творческие
| Агент | Роль | Project C |
|-------|------|-----------|
| `@technical-artist` | Шейдеры Unity, арт-пайплайн | URP, CloudGhibli |
| `@sound-designer` | Звуковые эффекты, эмбиент | Ветр, двигатели |
| `@writer` | Диалоги, текст | Квесты, NPC |
| `@world-builder` | Лор, фракции, география | 5 Гильдий, Завеса |

### Инженерные
| Агент | Роль | Project C |
|-------|------|-----------|
| `@devops-engineer` | CI/CD, билды | Dedicated Server |
| `@analytics-engineer` | Трекинг, телеметрия | (будущее) |
| `@security-engineer` | Античит, безопасность | Сетевая безопасность |
| `@qa-tester` | Тестирование фич | Баг-репорты |
| `@accessibility-specialist` | Доступность | UI/UX |

### Live-игры
| Агент | Роль | Project C |
|-------|------|-----------|
| `@live-ops-designer` | Ивенты, обновления | (будущее) |
| `@community-manager` | Комьюнити, фидбек | TheGravity.ru |

### Unity-специалисты
| Агент | Роль | Project C использование |
|-------|------|-------------------------|
| `@unity-specialist` | Архитектура, MonoBehaviour | Основной код игры |
| `@unity-dots-specialist` | DOTS/ECS, Jobs, Burst | Оптимизация (будущее) |
| `@unity-shader-specialist` | HLSL, Shader Graph, URP | CloudGhibli.shader |
| `@unity-addressables-specialist` | Addressables, async | Assets/_Project/Resources/ |
| `@unity-ui-specialist` | UI Toolkit, uGUI, Canvas | HUD, инвентарь, торговля |

---

## 🛠️ Доступные навыки (37)

### Ревью
| Навык | Как вызвать |
|-------|------------|
| design-review | «Проведи design-review для docs/gdd/...» |
| code-review | «Сделай code-review для Assets/_Project/Scripts/...» |
| balance-check | «Проверь баланс системы крафта» |
| asset-audit | «Проведи аудит ассетов в Assets/_Project/Art/» |
| perf-profile | «Профилируй производительность» |
| tech-debt | «Проанализируй технический долг» |

### Продакшн
| Навык | Как вызвать |
|-------|------------|
| sprint-plan | «Спланируй спринт» |
| milestone-review | «Проведи обзор майлстоуна» |
| estimate | «Оцени задачи» |
| retrospective | «Проведи ретроспективу» |
| bug-report | «Создай отчёт о баге» |

### Проект
| Навык | Как вызвать |
|-------|------------|
| start | «Проведи start — онбординг проекта» |
| project-stage-detect | «Определи текущий этап проекта» |
| reverse-document | «Создай документацию из кода» |
| gate-check | «Проверь готовность к этапу» |
| map-systems | «Декомпозируй концепт на системы» |

### Релиз
| Навык | Как вызвать |
|-------|------------|
| release-checklist | «Создай чек-лист релиза» |
| launch-checklist | «Создай чек-лист запуска» |
| changelog | «Составь лог изменений» |
| patch-notes | «Напиши заметки к патчу» |
| hotfix | «Исправь хотфикс» |

### Креатив
| Навык | Как вызвать |
|-------|------------|
| brainstorm | «Проведи brainstorm для "идея"» |
| playtest-report | «Создай отчёт плейтеста» |
| prototype | «Сделай прототип механики X» |
| onboard | «Проведи onboard для нового участника» |
| localize | «Подготовь локализацию» |

### Командные (оркестрация)
| Навык | Как вызвать |
|-------|------------|
| team-combat | «team-combat для grappling hook ability» |
| team-narrative | «team-narrative для сюжетной арки» |
| team-ui | «team-ui для инвентаря с drag-and-drop» |
| team-release | «team-release для подготовки релиза» |
| team-polish | «team-polish для полировки игры» |
| team-audio | «team-audio для звукового дизайна» |
| team-level | «team-level для дизайна уровня» |

---

## 📋 Протокол сотрудничества

Каждый агент следует принципу:

```
Вопрос → Варианты → Решение → Черновик → Утверждение
```

- Агент **спрашивает** перед записью файлов: «Могу ли я записать это в [filepath]?»
- Агент **показывает черновики** перед запросом одобрения
- Агент **не принимает решений** за вас — даёт экспертизу, вы решаете
- Multi-file изменения требуют явного одобрения полного набора

---

## ⚡ Quick Start для Project C

### Текущий статус: Этап 2.5 (Визуальный прототип)

```
✅ Реализовано:
- Процедурная генерация мира (15 пиков, 890+ облаков)
- Контроллер персонажа и корабля
- UI: подсказки, навигация
- Инвентарь (круговое колесо, 8 типов, сундуки)
- Сетевой мультиплеер (NGO, Host + Client + Dedicated Server)
- URP Pipeline, CloudGhibli.shader

🔜 Следующие задачи:
- Модель корабля (Blender → FBX)
- Модель персонажа (Mixamo)
- Текстуры горных пиков
- Post-Processing (Bloom, Color Grading)
- Система топлива (мезий)
```

### Рекомендуемый workflow

```
1. "Проведи project-stage-detect"      — подтвердить этап
2. "Спланируй sprint-plan"             — план на спринт
3. @unity-specialist "Реализуй X"      — код
4. @technical-artist "Настрой Y"       — арт
5. "code-review для Assets/_Project/Scripts/..." — проверка
6. @qa-tester "Тест-кейсы"             — тестирование
```

---

## 🚨 Критичные правила для Project C

### URP (КРИТИЧНО)
- ❌ НЕ создавать URP ассеты через C#
- ✅ ТОЛЬКО через Unity Editor UI
- ✅ Pipeline Asset → Edit → Project Settings → Graphics
- ✅ UniversalRendererData (НЕ ForwardRendererData)
- ✅ Standard шейдер → Universal Render Pipeline/Lit

### Git Workflow
- ✅ Ветка: `qwen-gamestudio-agent-dev`
- ❌ НЕ пушить в `main` без разрешения
- ✅ Коммитить часто, маленькими изменениями

### Collaboration
- ✅ Пользователь принимает ВСЕ решения
- ✅ Показывать черновики перед записью
- ✅ Спрашивать "Могу ли я записать в [filepath]?"
- ❌ НЕ коммитить без инструкции
- ❌ НЕ спрашивать "применять ли изменения?" — сразу применять

---

## 📚 Ссылки

- **GDD каталог:** [`docs/gdd/GDD_INDEX.md`](docs/gdd/GDD_INDEX.md)
- **Текущий контекст:** [`docs/QWEN_CONTEXT.md`](docs/QWEN_CONTEXT.md)
- **ART Bible:** [`docs/ART_BIBLE.md`](docs/ART_BIBLE.md)
- **Lore Book:** [`docs/WORLD_LORE_BOOK.md`](docs/WORLD_LORE_BOOK.md)
- **MMO Plan:** [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md)
- **Roadmap:** [`docs/roadmap.html`](docs/roadmap.html)

---

**Источник:** [Qwen Unity Game Studio](https://github.com/boozzeeboom/qwen-unity-game-studio)
**Версия:** v0.0.13-urp-setup | **Дата:** 12 апреля 2026
