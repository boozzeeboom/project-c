# Project C: The Clouds — Единый стартовый файл для Qwen Code

**Версия:** `v0.0.14-trade-system` | **Дата:** 9 апреля 2026 г.
**Ветка:** `qwen-gamestudio-agent-dev` (основная) | **Этап:** Этап 2 ЗАВЕРШЁН, Торговая система (сессии 1-8E)
**По мотивам книги «Интеграл Пьявица» — Бруно Арендт**

---

## 🚀 ВОЗОБНОВЛЕНИЕ СЕССИИ (начни отсюда)

### Быстрая команда для перезапуска:

```
Продолжи работу над Project C. Прочитай docs/QWEN_CONTEXT.md
```

Этого достаточно — я прочитаю этот файл и получу полный контекст: что сделано, какие проблемы, как устроена система агентов/навыков, как вызывать субагентов.

### Если что-то сломалось — откатиться:

```bash
git fetch upstream
git reset --hard upstream/qwen-gamestudio-agent-dev
```

---

## 📖 ЧТО ЭТО ЗА ПРОЕКТ

**Project C: The Clouds** — MMO-игра на Unity 6 по мотивам книги. 2090 год, человечество живёт на горных вершинах над облаками, корабли на антигравии курсируют между городами. Визуальный стиль: **Sci-Fi + Ghibli**.

**Технический стек:**
| Компонент | Технология |
|-----------|-----------|
| Клиент | Unity 6, URP, Input System |
| Сеть | Netcode for GameObjects (NGO) |
| Сервер | .NET 8 (авторитарный) |
| Язык | C# |

---

## 🎮 ЧТО УЖЕ РАБОТАЕТ

### Сетевой мультиплеер (полностью рабочий)
- ✅ Host + Client — синхронизация движения, камеры, инвентаря, кораблей
- ✅ Dedicated Server — кнопка в UI + `-server` build arg + headless режим
- ✅ Кооп-корабли — несколько игроков в одном корабле, ввод усредняется
- ✅ Boost (Shift) — передаётся через ServerRpc
- ✅ Посадка/выход из корабля (F) — синхронизация всем
- ✅ Disconnect UI — кнопка по центру экрана + события подключений/отключений
- ✅ Reconnect — авто-реконнект (5 попыток) + ручная кнопка + сохранение IP:Port
- ✅ Обработка обрывов — OnClientDisconnectCallback, OnTransportFailure
- ✅ Player Count — счётчик игроков обновляется в реальном времени
- ✅ Отладочное логирование Canvas и RectTransform

### Визуал (URP)
- ✅ URP Pipeline 17.4.0 — установлен и активен
- ✅ CloudGhibli.shader — кастомный шейдер облаков (noise + rim glow + morph)
- ✅ ProceduralNoiseGenerator — FBM noise текстуры
- ✅ Все материалы конвертированы: Standard → URP Lit/Unlit
- ✅ MaterialURPUpgrader — скрипт массовой конвертации
- ✅ WorldGenerator — URP-совместимые материалы
- ✅ docs/ART_BIBLE.md — полная визуальная спецификация

### Геймплей
- ✅ Пеший режим: WASD + Space + Shift
- ✅ Корабль: WASD + Q/E + мышь + Shift (boost)
- ✅ Инвентарь: подбор (E), сундуки, круговое колесо (Tab)
- ✅ Сохранение инвентаря — PlayerPrefs при Disconnect, восстановление при Reconnect
- ✅ Синхронизация подбора — HidePickupRpc + OpenChestRpc (SendTo.Everyone)
- ✅ ItemDatabaseInitializer — авто-регистрация предметов из Resources и сцены
- ✅ Персональная камера для каждого игрока
- ✅ Процедурная генерация мира (15 пиков, 890+ облаков, 3 слоя)

### Торговая система (Сессии 1-8E)
- ✅ **ScriptableObject товаров** — TradeItemDefinition, TradeDatabase
- ✅ **CargoSystem** — груз корабля (вес/объём)
- ✅ **LocationMarket** — рынки для каждой локации (Primium, Secundus, Tertius, Quartus)
- ✅ **TradeUI** — интерфейс торговли (покупка/продажа, склад/трюм)
- ✅ **Серверная торговля (NGO RPC)** — BuyItem/SellItem через ServerRpc
- ✅ **Tick-система** — динамическая экономика, изменение цен
- ✅ **ContractSystem** — контракты НП (принятие/завершение/провал)
- ✅ **PlayerTradeStorage** — склад игрока (локация, предметы, кредиты)
- ✅ **Синхронизация кредитов** — единый источник TradeMarketServer (Dictionary + PlayerPrefs)
- ✅ **Валидация** — quantity > 0, locationId, clamp demandFactor/supplyFactor
- ✅ **Сохранение/загрузка** — кредиты, предметы, вес, объём

---

## 🔴 КРИТИЧНО: НЕ ТРОГАТЬ `.meta` ФАЙЛЫ

> **Никогда не создавать и не редактировать `.meta` файлы вручную!**

Unity автоматически создаёт `.meta` файлы для каждого ассета. Ручное создание/редактирование ломает ссылки.

---

## 🔴 ИЗВЕСТНЫЕ ПРОБЛЕМЫ (приоритет по убыванию)

| Приоритет | Проблема | Файл | Как починить |
|-----------|----------|------|--------------|
| 🟡 Средне | Модель корабля — примитив (сфера) | ShipController | Заменить на FBX модель (Этап 2.5) |
| 🟡 Средне | Персонаж — capsule | NetworkPlayer | Заменить на Mixamo модель (Этап 2.5) |
| 🟡 Средне | Инвентарь не синхронизируется между игроками | Архитектура | Этап 3 (RPG система, серверная валидация) |
| 🟡 Средне | Boost (Shift) корабля не передаётся в RPC | ShipController | Добавить параметр в SubmitShipInputRpc |
| 🟢 Низко | Горные пики — процедурные без текстур | WorldGenerator | Добавить текстуры из Poly Haven (Этап 2.5) |
| 🔴 Отложено | Отдельный серверный билд (.NET 8) | Архитектура | Этап 5+ |
| 🔴 Отложено | Система лобби/комнат | Архитектура | Этап 5+ |

### ✅ Исправлено

| Проблема | Статус |
|----------|--------|
| ~~Disconnect кнопка в левом углу~~ | ✅ Пофиксено — кнопка по центру экрана |
| ~~Предметы не исчезают у других игроков~~ | ✅ HidePickupRpc (SendTo.Everyone) |
| ~~Сундуки не работали~~ | ✅ Возвращён старый рабочий подбор |
| ~~Инвентарь терялся при реконнекте~~ | ✅ Сохранение/загрузка через PlayerPrefs |
| ~~NetworkInventory не работал~~ | ✅ Откат — NGO не поддерживает NetworkVariable<string> |
| ~~Все материалы розовые (Standard в URP)~~ | ✅ Конвертированы в URP Lit/Unlit |
| ~~CloudGhibli.shader не компилировался~~ | ✅ URP пакет установлен, Pipeline Asset назначен |
| ~~Кредиты не синхронизировались (два источника)~~ | ✅ Единый источник TradeMarketServer (сессия 8E) |
| ~~Сдача контрактов не добавляла кредитов~~ | ✅ ContractSystem → TradeMarketServer.SetPlayerCreditsStatic |

---

## 🗺️ НАВИГАЦИЯ ПО ДОКУМЕНТАЦИИ

### Файлы для понимания проекта (читай по мере необходимости)

| Файл | Когда читать |
|------|-------------|
| `docs/NETWORK_ARCHITECTURE.md` | Работа с сетью, RPC, синхронизация |
| `docs/STEP_BY_STEP_DEVELOPMENT.md` | История шагов, что и как делалось |
| `docs/CONTROLS.md` | Полная карта клавиш |
| `docs/MMO_Development_Plan.md` | Полный план разработки MMO |
| `docs/WORLD_LORE_BOOK.md` | Лор книги — мир, технологии, фракции |
| `docs/SHIP_SYSTEM_DOCUMENTATION.md` | Система кораблей (текущая реализация) |
| `docs/INVENTORY_SYSTEM.md` | Система инвентаря |

### Файлы .qwenencode — система агентов и навыков

Эта папка содержит **Game Studio** — систему из 39 агентов и 37 навыков для профессиональной разработки.

### Файлы документации

| Файл | Когда читать |
|------|-------------|
| `docs/CHANGELOG.md` | ⭐ **История изменений** — что было сделано в каждой версии |
| `docs/QWENTRADING8SESSION.md` | ⭐ **План торговой системы** — 8 сессий, зависимости, команды |
| `docs/NETWORK_ARCHITECTURE.md` | Работа с сетью, RPC, синхронизация, reconect |
| `docs/STEP_BY_STEP_DEVELOPMENT.md` | История шагов, что и как делалось |
| `docs/DEDICATED_SERVER.md` | Запуск Dedicated Server (кнопка, build args) |
| `docs/CONTROLS.md` | Полная карта клавиш |
| `docs/MMO_Development_Plan.md` | Полный план разработки MMO |
| `docs/WORLD_LORE_BOOK.md` | Лор книги — мир, технологии, фракции |
| `docs/SHIP_SYSTEM_DOCUMENTATION.md` | Система кораблей (текущая реализация) |
| `docs/INVENTORY_SYSTEM.md` | Система инвентаря |

---

## 🤖 СИСТЕМА АГЕНТОВ (роли)

### Что такое агенты

Агенты — это специализированные роли с глубокими знаниями в своей области. Каждый агент имеет свой протокол работы, ограничения и карту делегирования.

### Где находятся

```
.qwenencode/agents/
├── creative-director.md     — Видение, пиллары, тон
├── technical-director.md    — Архитектура, техдолг
├── producer.md              — Расписание, скоуп
├── game-designer.md         — Механики, системы, прогрессия
├── lead-programmer.md       — Архитектура кода, качество
├── gameplay-programmer.md   — Механики, инвентарь, управление
├── network-programmer.md    — Сеть, синхронизация, репликация
├── ui-programmer.md         — Меню, HUD, ввод
├── unity-specialist.md      — Unity: MonoBehaviour, префабы, пакеты
├── unity-dots-specialist.md — DOTS/ECS, Jobs, Burst
├── unity-shader-specialist.md — HLSL, Shader Graph, VFX
├── unity-ui-specialist.md   — UI Toolkit, UGUI
├── ... и ещё 27 агентов
```

### Как вызвать агента

**Способ 1: Текстовый запрос (основной)**
Опиши задачу — я сам определю нужного агента и применю его протокол:
```
"Реализуй систему топлива для кораблей"  → gameplay-programmer + unity-specialist
"Сделай ревью кода NetworkPlayer.cs"     → lead-programmer + code-review
"Придумай новые миссии для Гильдии"      → game-designer + narrative-director
```

**Способ 2: Прямой вызов через @имя (если поддерживается)**
```
@gameplay-programmer "Добавь систему топлива"
@unity-specialist "Настрой URP для облаков"
@network-programmer "Оптимизируй синхронизацию кораблей"
```

### Иерархия агентов

```
ДИРЕКТОРА (Tier 1) — решения по архитектуре и видению
├── creative-director    — видение, тон, пиллары
├── technical-director   — архитектура, стек, техдолг
└── producer             — расписание, координация

ЛИДЫ (Tier 2) — владеют своими областями
├── game-designer        — механики, системы
├── lead-programmer      — архитектура кода
├── art-director         — визуальный стиль
├── qa-lead              — стратегия тестирования
└── ... другие лиды

СПЕЦИАЛИСТЫ (Tier 3) — исполняют в своей области
├── gameplay-programmer  — механики, инвентарь
├── network-programmer   — сеть, репликация
├── ui-programmer        — UI, HUD
├── unity-specialist     — Unity API, пакеты
├── ... и другие
```

### Протокол работы агентов

Каждый агент следует **Collaboration Protocol**:
1. **Задаёт вопросы** для прояснения контекста
2. **Предлагает 2-4 варианта** с плюсами/минусами
3. **Ждёт решения** пользователя — НЕ решает за него
4. **Пишет черновик** → показывает → ждёт одобрения
5. **Спрашивает** «Могу записать в [filepath]?» перед записью
6. **Записывает** только после явного «да»

---

## 🛠️ НАВЫКИ (slash-команды)

### Что такое навыки

Навыки — это автоматизированные рабочие процессы для типовых задач. Они вызываются текстом или `/командой`.

### Где находятся

```
.qwenencode/skills/
├── start/                 — Первичный онбординг
├── brainstorm/            — Генерация идей (MDA, SDT, Bartle)
├── prototype/             — Быстрый прототип (изолированный код)
├── code-review/           — Ревью кода
├── design-review/         — Ревью дизайн-документа
├── sprint-plan/           — Планирование спринта
├── map-systems/           — Декомпозиция на системы
├── design-system/         — Поэтапное написание GDD
├── onboard/               — Генерация онбординга для роли
├── setup-engine/          — Настройка движка
├── tech-debt/             — Сканирование техдолга
├── changelog/             — Генерация changelog из git
├── hotfix/                — Экстренный фикс
├── gate-check/            — Проверка готовности к фазе
├── project-stage-detect/  — Авто-определение этапа
├── reverse-document/      — Документация из кода
├── estimate/              — Оценка усилий
├── perf-profile/          — Профилирование производительности
├── team-combat/           — Оркестровка команды: геймдизайнер + программист + AI + QA
├── team-ui/               — Оркестровка UI команды
├── team-narrative/        — Оркестровка нарратив команды
├── ... и ещё 15 навыков
```

### Как вызвать навык

**Текстовый запрос (основной способ):**
| Ключевые слова | Вызываемый навык |
|----------------|-----------------|
| "brainstorm", "идея", "придумай" | `/brainstorm` |
| "start", "онбординг", "с чего начать" | `/start` |
| "code-review", "ревью кода" | `/code-review` |
| "sprint-plan", "спринт" | `/sprint-plan` |
| "prototype", "прототип" | `/prototype` |
| "tech-debt", "техдолг" | `/tech-debt` |
| "changelog", "лог изменений" | `/changelog` |
| "hotfix", "хотфикс" | `/hotfix` |
| "gate-check", "проверка готовности" | `/gate-check` |
| "onboard", "онбординг для роли" | `/onboard` |
| "оцени", "оценка усилий" | `/estimate` |
| "профилирование", "производительность" | `/perf-profile` |
| "обзор майлстоуна" | `/milestone-review` |

**Slash-команды (если поддерживаются):**
```
/brainstorm "система погоды над облаками"
/prototype "физика корабля с инерцией"
/code-review Assets/_Project/Scripts/Player/ShipController.cs
/sprint-plan new
/tech-debt scan
/changelog
```

### Полный список навыков

| Команда | Назначение |
|---------|-----------|
| `/start` | Первичный онбординг — определяет где ты, направляет к workflow |
| `/brainstorm` | Генерация идей через MDA, психологию игроков, verb-first дизайн |
| `/prototype` | Быстрый прототип для проверки механики (изолированный, throwaway) |
| `/design-review` | Ревью дизайн-документа на полноту и согласованность |
| `/code-review` | Ревью кода: архитектура, безопасность, производительность |
| `/sprint-plan` | Создать/обновить план спринта |
| `/map-systems` | Декомпозировать концепцию на системы, маппинг зависимостей |
| `/design-system` | Поэтапное написание GDD для одной системы |
| `/setup-engine` | Настроить движок + версию, заполнить reference docs |
| `/gate-check` | Проверить готовность перейти к следующей фазе (PASS/CONCERNS/FAIL) |
| `/project-stage-detect` | Авто-анализ состояния проекта, определение этапа, поиск пробелов |
| `/reverse-document` | Сгенерировать документацию из существующего кода |
| `/tech-debt` | Сканировать, отслеживать, приоритизировать техдолг |
| `/changelog` | Авто-генерация changelog из git коммитов |
| `/hotfix` | Экстренный фикс с audit trail |
| `/estimate` | Структурная оценка усилий с разбивкой по сложности/рискам |
| `/perf-profile` | Профилирование производительности, поиск bottlenecks |
| `/milestone-review` | Обзор прогресса майлстоуна |
| `/retrospective` | Ретроспектива спринта/майлстоуна |
| `/bug-report` | Структурированный баг-репорт |
| `/playtest-report` | Шаблон отчёта плейтеста |
| `/balance-check` | Анализ баланса игры |
| `/scope-check` | Проверка scope creep против плана |
| `/localize` | Локализация: скан, извлечение, валидация |
| `/asset-audit` | Аудит ассетов: имена, размеры, стандарты |
| `/architecture-decision` | Создание Architecture Decision Record (ADR) |
| `/release-checklist` | Валидация чек-листа перед релизом |
| `/launch-checklist` | Проверка готовности к запуску |
| `/patch-notes` | Генерация патч-нот для игроков |
| `/onboard` | Онбординг-документ для новой роли/агента |
| `/team-combat` | Оркестровка: game-designer + gameplay-programmer + ai-programmer + TA + sound + QA |
| `/team-ui` | Оркестровка: ux-designer + ui-programmer + art-director |
| `/team-narrative` | Оркестровка: narrative-director + writer + world-builder + level-designer |
| `/team-release` | Оркестровка: release-manager + qa-lead + devops + producer |
| `/team-polish` | Оркестровка: performance + TA + sound + QA |
| `/team-audio` | Оркестровка: audio-director + sound-designer + TA + gameplay-programmer |
| `/team-level` | Оркестровка: level-designer + narrative + world-builder + art + systems + QA |

---

## 📐 ПРАВИЛА (rules)

### Что такое правила

Правила автоматически применяются при редактировании файлов в определённых путях. Они обеспечивают стандарты кода и документации.

### Где находятся

```
.qwenencode/rules/
├── gameplay-code.md    — src/gameplay/**  (data-driven, delta time, без UI)
├── engine-code.md      — src/core/**      (zero alloc, thread safety)
├── network-code.md     — src/networking/** (server-authoritative, security)
├── ui-code.md          — src/ui/**        (без ownership state, localization)
├── ai-code.md          — src/ai/**        (performance budgets, debug)
├── design-docs.md      — design/gdd/**    (8 секций, формулы, edge cases)
├── narrative.md        — design/narrative/** (лор, голос персонажей)
├── data-files.md       — assets/data/**   (JSON валидность, схема)
├── test-standards.md   — tests/**         (имена, покрытие)
├── prototype-code.md   — prototypes/**    (расслабленные стандарты, README)
└── shader-code.md      — assets/shaders/** (имена, кросс-платформа)
```

### Применительно к Project C

Unity проект использует `Assets/_Project/Scripts/` — правила применяются аналогично:
- **Core/** → engine-code (zero alloc в hot paths)
- **Player/** → gameplay-code (data-driven, delta time)
- **UI/** → ui-code (без ownership game state, localization-ready)
- **Сетевые скрипты** → network-code (server-authoritative)

---

## 🔄 ХУКИ (автоматические действия)

### Что такое хуки

Хуки срабатывают автоматически при определённых событиях (коммит, пуш, старт сессии).

### Где находятся

```
.qwenencode/hooks/
├── session-start.sh     — Старт сессии: загружает контекст (спринт, ветка, коммиты)
├── session-stop.sh      — Конец сессии: суммирует достижения
├── validate-commit.sh   — Pre-commit: проверяет стандарты
├── validate-push.sh     — Pre-push: предупреждает о защищённых ветках
├── validate-assets.sh   — Post-write: проверяет имена ассетов, JSON
├── detect-gaps.sh       — Старт сессии: ищет пробелы в документации
├── pre-compact.sh       — Сжатие контекста: сохраняет состояние перед компактизацией
└── log-agent.sh         — Субагент: логирует вызовы агентов
```

---

## 🧩 СУБАГЕНТЫ (вложенные агенты)

### Что такое субагенты

Субагенты — это агенты, которые вызываются из других агентов для глубокой экспертизы в подобластих. Основной агент управляет контекстом, субагент выполняет специализированную работу.

### Карта субагентов для Unity

```
unity-specialist (корневой агент Unity)
├── → unity-dots-specialist       — DOTS/ECS, Jobs, Burst
├── → unity-shader-specialist     — Shader Graph, VFX Graph, URP/HDRP
├── → unity-addressables-specialist — Addressables, AssetBundles, async
└── → unity-ui-specialist         — UI Toolkit, UGUI, data binding
```

### Как вызываются субагенты

**Через инструмент Task/Agent:** Основной агент (например, `unity-specialist`) вызывает субагента с полным контекстом:

```
agent:
  subagent_type: unity-shader-specialist
  prompt: "Напиши HLSL шейдер для облаков с Ghibli-эстетикой. 
           Контекст: Project C, URP, 3 слоя облаков на разных высотах.
           Файл: Assets/_Project/Material/CloudShader.shader"
```

**Через текстовый запрос:** Опиши задачу — я вызову нужного агента и/или субагента:
```
"Настрой Shader Graph для облаков" → unity-shader-specialist
"Оптимизируй DOTS для 1000+ объектов" → unity-dots-specialist
"Настрой Addressables для загрузки уровней" → unity-addressables-specialist
```

### Когда вызывать субагентов

| Ситуация | Вызывай |
|----------|---------|
| Сложный шейдер, VFX, рендер-пайплайн | `unity-shader-specialist` |
| ECS, Jobs, Burst, высокая производительность | `unity-dots-specialist` |
| Загрузка ассетов, память, группы | `unity-addressables-specialist` |
| Сложный UI, data binding, UI Toolkit | `unity-ui-specialist` |
| Общая Unity архитектура, пакеты, настройки | `unity-specialist` (корневой) |

---

## 🎯 ПРОТОКОЛ СОТРУДНИЧЕСТВА

### Цикл: Вопрос → Варианты → Решение → Черновик → Утверждение

1. **ЗАДАВАЙ вопросы** для прояснения контекста
2. **ПРЕДЛАГАЙ 2-4 варианта** с плюсами/минусами, примерами
3. **ЖДИ решения** пользователя — НЕ решай за него
4. **ПИШИ черновик** секции → показывай → жди одобрения
5. **СПРАШИВАЙ** «Могу ли я записать это в [filepath]?» перед Write/Edit
6. **ЗАПИСЫВАЙ** только после явного «да»

### Incremental Writing (для дизайн-доков)

1. Создай файл со скелетом (все заголовки, пустые тела)
2. Обсуди одну секцию → получи одобрение → запиши
3. Обнови `production/session-state/active.md` с прогрессом
4. Переходи к следующей секции

### Чего Я НЕ ДЕЛАЮ

- ❌ НЕ пропускаю чтение файла агента/навыка перед действием
- ❌ НЕ принимаю финальные решения за пользователя (кроме технических исправлений по согласованию)
- ❌ НЕ коммичу без инструкции пользователя

---

## 🗂️ СТРУКТУРА ПРОЕКТА

```
ProjectC_client/
├── docs/                          # Документация проекта
│   ├── QWEN_CONTEXT.md            # ⭐ ЭТОТ ФАЙЛ — стартовый
│   ├── NETWORK_ARCHITECTURE.md    # Сетевая архитектура
│   ├── STEP_BY_STEP_DEVELOPMENT.md# История шагов
│   ├── CONTROLS.md                # Карта клавиш
│   ├── MMO_Development_Plan.md    # План MMO
│   ├── WORLD_LORE_BOOK.md         # Лор книги
│   ├── SHIP_SYSTEM_DOCUMENTATION.md
│   ├── INVENTORY_SYSTEM.md
│   └── ... другие документы
├── .qwenencode/                   # Game Studio система
│   ├── QWEN-MASTER-PROMPT.md      # Мастер-промпт (инструкция Studio)
│   ├── qwencode.json              # Конфигурация агентов
│   ├── agents/                    # 39 определений агентов
│   ├── skills/                    # 37 определений навыков
│   ├── rules/                     # 11 файлов правил
│   ├── hooks/                     # 8 хук-скриптов
│   └── docs/                      # Шаблоны, reference docs
├── Assets/
│   ├── _Project/
│   │   ├── Scripts/
│   │   │   ├── Core/              # Ядро: сеть, генерация, инвентарь
│   │   │   ├── Player/            # Контроллеры: пешеход, корабль
│   │   │   └── UI/                # Интерфейс
│   │   ├── Prefabs/               # Префабы
│   │   ├── Scenes/                # Сцены (ProjectC_1.unity — основная)
│   │   ├── Art/                   # 3D модели, текстуры
│   │   ├── Items/                 # ScriptableObject предметов
│   │   ├── Material/              # Материалы
│   │   └── InputActions/          # Input System ассеты
│   └── ...
├── ProjectSettings/               # Настройки Unity
├── Packages/                      # Unity пакеты
└── README.md                      # Общее описание
```

---

## 🎮 УПРАВЛЕНИЕ

| Клавиша | Пеший режим | Режим корабля |
|---------|-------------|---------------|
| **W/S** | Движение вперёд/назад | Тяга вперёд/назад |
| **A/D** | Поворот | Рыскание (поворот) |
| **Space** | Прыжок | — |
| **Left Shift** | Бег (x2 скорость) | Ускорение (x2 тяга) |
| **Q/E** | — | Лифт вниз/вверх |
| **Мышь** | Вращение камеры | Тангаж (нос вверх/вниз) |
| **F** | Сесть в корабль | Выйти из корабля |
| **E** | Подобрать предмет / сундук | — |
| **Tab** | Круговой инвентарь | — |
| **Escape** | Toggle Disconnect кнопка | Toggle Disconnect кнопка |

---

## 📋 КЛЮЧЕВЫЕ СКРИПТЫ

| Скрипт | Назначение |
|--------|-----------|
| `NetworkManagerController.cs` | Обёртка NGO, обработка подключений |
| `NetworkPlayer.cs` | Игрок: движение, камера, инвентарь |
| `ShipController.cs` | Корабль: физика, кооп-пилотирование |
| `NetworkUI.cs` | UI сети: Disconnect кнопка, панель |
| `ThirdPersonCamera.cs` | Орбитальная камера от 3-го лица |
| `Inventory.cs` | Инвентарь: хранение по типам, группировка |
| `InventoryUI.cs` | Круговое колесо (8 секторов, GL-рендер) |
| `ChestContainer.cs` | Сундук с LootTable, анимация |
| `WorldGenerator.cs` | Процедурная генерация: 15 пиков, 890+ облаков |
| `PlayerController.cs` | Пеший контроллер (WASD, бег, прыжок) |
| `ControlHintsUI.cs` | Подсказки клавиш на экране |
| `CloudGhibli.shader` | Кастомный шейдер облаков (URP Unlit + noise + rim glow) |
| `ProceduralNoiseGenerator.cs` | Генерация FBM noise-текстур для облаков |
| `MaterialURPConverter.cs` | Авто-конвертация материалов при запуске |
| `MaterialURPUpgrader.cs` | Editor-скрипт массовой конвертации Standard → URP |
| `CloudLayer.cs` | Обновлён: авто-интеграция CloudGhibli шейдера |
| `WorldGenerator.cs` | Обновлён: URP/Lit + URP/Unlit материалы |

---

## 🔗 GIT WORKFLOW

```
upstream  https://github.com/boozzeeboom/project-c.git
```

**Основные ветки:**
- `qwen-gamestudio-agent-dev` — основная ветка разработки

**Команды:**
```bash
# Откатиться к последнему стабильному
git fetch upstream
git reset --hard upstream/qwen-gamestudio-agent-dev

# Создать бэкап-тег перед изменениями
git tag backup/перед-изменениями
git push upstream --tags

# Проверить статус
git status && git log --oneline -3
```

**Принцип:** "Медленнее = Быстрее" — 1 файл/класс за раз, тестировать после каждого шага.

---

## 🚀 БЫСТРЫЕ ДЕЙСТВИЯ

### Начать новую фичу
```
"Реализуй [название фичи]" → Я вызову gameplay-programmer или нужного специалиста
```

### Протестировать идею быстро
```
/prototype "физика корабля с инерцией"
```

### Проверить качество кода
```
/code-review Assets/_Project/Scripts/Player/ShipController.cs
```

### Спланировать работу
```
/sprint-plan new
```

### Починить техдолг
```
/tech-debt scan
```

### Узнать текущий этап
```
/project-stage-detect
```

### Создать документацию из кода
```
/reverse-document "архитектура сети"
```

---

## ⚡ ПАМЯТКА

| Правило | Описание |
|---------|----------|
| 🔴 **НЕ ТРОГАТЬ `.meta`** | Unity создаёт их автоматически |
| 🟡 **Маленькие шаги** | 1 файл/класс за раз |
| 🟡 **Тестируй после шага** | Запусти Unity → Play → проверь |
| 🟢 **Коммить после теста** | git add . && git commit -m "Шаг X: ..." |
| 🟢 **Читать файл агента** | Перед вызовом — читай .md агента/навыка |
| 🟢 **Технические решения** | Qwen принимает сам, пользователь только тестирует |

---

## 📞 КОНТАКТЫ И ССЫЛКИ

- **Репозиторий:** [github.com/boozzeeboom/project-c](https://github.com/boozzeeboom/project-c)
- **Сайт:** [TheGravity.ru](https://thegravity.ru) & [TheClouds](https://thegravity.ru/project-c/)
- **Контакт:** [@indeed174](https://t.me/indeed174)
- **Книга:** «Интеграл Пьявица» — Бруно Арендт

---

**Запомнить:**
> **Лучше 10 маленьких шагов за день, чем 1 большой за неделю.**

**Последнее обновление:** 5 апреля 2026 г.
**Версия:** `v0.0.12-stage2-complete`
**Автор документа:** Qwen Code (Game Studio)

---

## 📝 ИТОГИ СЕССИИ (6 апреля 2026)

### Что было сделано:
1. ✅ **URP Pipeline** — установлен пакет 17.4.0, Pipeline Asset создан и назначен
2. ✅ **CloudGhibli.shader** — кастомный URP Unlit шейдер (noise + rim glow + morph)
3. ✅ **ProceduralNoiseGenerator** — FBM noise текстуры 512×512
4. ✅ **MaterialURPConverter** — авто-конвертация при запуске
5. ✅ **MaterialURPUpgrader** — Editor-скрипт массовой конвертации
6. ✅ **WorldGenerator.cs** — Standard → URP/Lit + URP/Unlit
7. ✅ **docs/ART_BIBLE.md** — полная визуальная спецификация (12 секций)
8. ✅ **docs/unity6/UNITY6_URP_SETUP.md** — справочник по URP в Unity 6
9. ✅ **docs/MMO_Development_Plan.md** — добавлен Этап 2.5: Визуальный прототип
10. ✅ **docs/CHANGELOG.md** — добавлена v0.0.13-urp-setup
11. ✅ **Исправлены ошибки** — CS0618, CS0246, розовые материалы

### Что НЕ было сделано (отложено на Этап 2.5):
- ❌ Модель корабля (замена примитива на FBX)
- ❌ Модель персонажа (Mixamo вместо capsule)
- ❌ Текстуры горных пиков (Poly Haven)
- ❌ Постройки на пиках (префабы зданий)
- ❌ Post-Processing (Bloom, Color Grading, Vignette)

### Что работает:
- URP Pipeline активен (ProjectC_URP)
- CloudGhibli.shader компилируется
- Облака рендерятся через URP Unlit
- Пики и персонаж через URP Lit
- Все материалы конвертированы

### Следующий шаг:
**Этап 2.5: Визуальный прототип** — модель лёгкого корабля в Blender
- Торообразная форма, 5-8k tri, ветровые лопасти
- Интеграция в ShipController (замена примитива)
- См. docs/ART_BIBLE.md раздел 4.1

---

## 📝 ИТОГИ СЕССИИ (5 апреля 2026)

### Что было сделано:
1. ✅ **Dedicated Server** — кнопка + `-server` build arg + headless режим
2. ✅ **Reconnect** — авто-реконнект (5 попыток) + ручная кнопка + сохранение инвентаря
3. ✅ **Синхронизация подбора** — HidePickupRpc + OpenChestRpc (SendTo.Everyone)
4. ✅ **Player Count** — счётчик игроков в реальном времени
5. ✅ **ItemDatabaseInitializer** — авто-регистрация предметов из Resources + сцены
6. ✅ **Сохранение инвентаря** — PlayerPrefs при Disconnect, восстановление при Reconnect
7. ✅ **Исправлены баги** — дубликат RPC, сломанные сундуки, NetworkInventory откат

### Что НЕ было сделано (отложено):
- ❌ Полная синхронизация инвентаря (NetworkVariable<string> не работает в NGO)
- ❌ Отдельный серверный билд (.NET 8) — Этап 5+
- ❌ Система лобби/комнат — Этап 5+

### Что работает:
- Host + Client + Dedicated Server
- Подбор предметов синхронизирован (исчезают у всех)
- Сундуки открываются синхронно
- Инвентарь сохраняется при реконнекте
- Reconnect работает (авто + ручной)

### Следующий шаг:
**Этап 3: Ролевая система и прогрессия** (недели 9-12)
- Характеристики и навыки
- Серверная база данных (PostgreSQL)
- Серверная валидация инвентаря
- Крафт и торговля
