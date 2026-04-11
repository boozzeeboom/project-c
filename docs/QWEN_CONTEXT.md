# Project C: The Clouds — Единый стартовый файл для Qwen Code

**Версия:** `v0.0.17-altitude-session2` | **Дата:** 12 апреля 2026 г.
**Ветка:** `qwen-gamestudio-agent-dev` (основная) | **Этап:** Этап 2 ЗАВЕРШЁН, Торговая система ЗАВЕРШЕНА (сессии 1-8F), UI система ЗАВЕРШЕНА (спринты 1-3), **Сессия 2 кораблей ЗАВЕРШЕНА** (Altitude Corridor System), Спринт 4 (Polish) в ожидании
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

### Торговая система (Сессии 1-8F + UI Спринты 1-3 — ЗАВЕРШЕНА)
- ✅ **ScriptableObject товаров** — TradeItemDefinition, TradeDatabase
- ✅ **CargoSystem** — груз корабля (вес/объём/слоты, влияние на скорость)
- ✅ **LocationMarket** — рынки для каждой локации (Primium, Secundus, Tertius, Quartus)
- ✅ **TradeUI** — интерфейс торговли (TextMeshPro, UITheme, UIFactory, cursor management)
- ✅ **ContractBoardUI** — контракты (TextMeshPro, UITheme, UIFactory, эмодзи устранены)
- ✅ **UIManager** — централизованный менеджер UI (приоритеты, z-ordering, input management)
- ✅ **UIFactory** — фабрика UI компонентов (8 методов, 0 дублирования кода)
- ✅ **UITheme** — ScriptableObject темы (51+ цвет → UITheme.Default, авто-создание)
- ✅ **ConfirmationDialog** — создан (отключён для торговли по фидбеку)
- ✅ **Audio feedback** — инфраструктура готова (нужны AudioClip)
- ✅ **Серверная торговля (NGO RPC)** — BuyItem/SellItem через ServerRpc, валидация
- ✅ **Tick-система** — динамическая экономика, NPC-трейдеры, затухание 0.92x
- ✅ **ContractSystem** — контракты НП (принятие/завершение/провал, долги)
- ✅ **PlayerTradeStorage** — склад игрока (локация, предметы, погрузка/разгрузка)
- ✅ **PlayerDataStore** — единый источник данных (кредиты общие, склады по локациям)
- ✅ **Валидация** — quantity > 0, locationId, currentPrice > 0, clamp факторов
- ✅ **Сохранение/загрузка** — PlayerDataStore (PlayerPrefs, P0: заменить на БД)
- ✅ **Защита от Double RPC** — _tradeLocked флаг, сброс только в OnTradeResult()
- ✅ **Защита от price=0** — itemId восстановление ссылки, RecalculatePrice()
- ✅ **Оценка UI системы:** 4.5/10 → 7/10 (+55%)
- 📋 **Полный отчёт UI:** docs/QWEN-UI-AGENTIC-SUMMARY.md

---

## 🔴 КРИТИЧНО: НЕ ТРОГАТЬ `.meta` ФАЙЛЫ

> **Никогда не создавать и не редактировать `.meta` файлы вручную!**

Unity автоматически создаёт `.meta` файлы для каждого ассета. Ручное создание/редактирование ломает ссылки.

---

## 🔴 ИЗВЕСТНЫЕ ПРОБЛЕМЫ (приоритет по убыванию)

| Приоритет | Проблема | Файл | Как починить |
|-----------|----------|------|--------------|
| 🔴 P0 | PlayerPrefs для данных игрока | PlayerDataStore | Заменить на IPlayerDataRepository + БД (Сессия 10) |
| 🔴 P0 | FindAnyObjectByType ненадёжно | TradeUI | PlayerRegistry словарь (Сессия 10) — частично решено кэшированием |
| 🔴 P0 | ScriptableObject state теряется | LocationMarket | Разделить MarketConfig + MarketState (Сессия 10) |
| 🟡 P1 | Нет проверки позиции в RPC | TradeMarketServer | Добавить player.currentLocationId == locationId (Сессия 10) |
| 🟡 P1 | Quantity overflow | TradeMarketServer | Clamp quantity до 9999 (Сессия 10) |
| 🟡 P1 | Rate limit отключён | TradeMarketServer | Включить 30/min по умолчанию (Сессия 10) |
| 🟡 UI | Контракты не сдаются с грузом на корабле | ContractBoardUI | Спринт 3.3 — MVC рефакторинг |
| 🟡 UI | InventoryUI остаётся на OnGUI | InventoryUI.cs | Спринт 3.1 — Canvas-based rewrite |
| 🟡 UI | TradeUI 1200 строк | TradeUI.cs | Спринт 3.3 — MVC разделение |
| 🟡 Средне | Модель корабля — примитив (сфера) | ShipController | Заменить на FBX модель (Этап 2.5) |
| 🟡 Средне | Персонаж — capsule | NetworkPlayer | Заменить на Mixamo модель (Этап 2.5) |
| 🔴 P0 | **AltitudeUI HUD не отображается** | AltitudeUI.cs | @unity-ui-specialist: Canvas/RectTransform/Sorting |
| 🟢 Низко | Горные пики — процедурные без текстур | WorldGenerator | Добавить текстуры из Poly Haven (Этап 2.5) |
| 🔴 Отложено | Отдельный серверный билд (.NET 8) | Архитектура | Этап 5+ |

### ✅ Исправлено (включая UI Спринты 1-3)

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
| ~~InventoryUI material leak~~ | ✅ Исправлено (Спринт 1) — OnDestroy cleanup |
| ~~InputAction lambda subscriptions~~ | ✅ Исправлено (Спринт 1) — кэшированный делегат |
| ~~"Type 1-8" без семантики~~ | ✅ Исправлено (Спринт 1) — Resources, Equipment, Food, Fuel, etc. |
| ~~Cursor не lock/unlock при UI~~ | ✅ Исправлено (Спринт 1) — cursor management |
| ~~PeakNavigationUI в production~~ | ✅ Исправлено (Спринт 1) — showInBuild flag |
| ~~120 строк дублирования кода~~ | ✅ Исправлено (Спринт 2) — UIFactory |
| ~~51+ хардкодный цвет~~ | ✅ Исправлено (Спринт 2) — UITheme ScriptableObject |
| ~~Эмодзи в sci-fi UI~~ | ✅ Исправлено (Спринт 2) — [Контракт] [Груз] [Срочный] |
| ~~UnityEngine.UI.Text legacy~~ | ✅ Исправлено (Спринт 2) — TextMeshProUGUI везде |
| ~~Нет input management~~ | ✅ Исправлено (Спринт 3) — UIManager с CanReceiveInput |
| ~~Нет z-ordering панелей~~ | ✅ Исправлено (Спринт 3) — приоритеты 200/300/400 |

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
| `docs/QWEN-UI-AGENTIC-SUMMARY.md` | ⭐⭐ **UI система полный отчёт** — спринты 1-3, метрики, проблемы, план |
| `docs/CHANGELOG.md` | ⭐ **История изменений** — что было сделано в каждой версии |
| `docs/TRADE_SYSTEM_RAG.md` | ⭐⭐ **RAG документация торговой системы** — архитектура, потоки, формулы |
| `docs/TRADE_DEBUG_GUIDE.md` | Отладка торговли — симптомы → решения |
| `docs/MMO_Development_Plan.md` | Полный план разработки MMO |
| `docs/gdd/GDD_22_Economy_Trading.md` | GDD экономики (обновлён v3.0) |
| `docs/gdd/GDD_25_Trade_Routes.md` | GDD торговых маршрутов |
| `docs/gdd/GDD_23_Faction_Reputation.md` | GDD репутации фракций |
| `docs/gdd/GDD_13_UI_UX_System.md` | GDD UI/UX системы |
| `docs/NETWORK_ARCHITECTURE.md` | Работа с сетью, RPC, синхронизация, reconnect |
| `docs/CONTROLS.md` | Полная карта клавиш |
| `docs/DEDICATED_SERVER.md` | Запуск Dedicated Server (кнопка, build args) |
| `docs/WORLD_LORE_BOOK.md` | Лор книги — мир, технологии, фракции |
| `docs/SHIP_SYSTEM_DOCUMENTATION.md` | Система кораблей (текущая реализация) |
| `docs/INVENTORY_SYSTEM.md` | Система инвентаря |

**Перемещено в Old_sessions/** (архив старых сессий):
- `docs/Old_sessions/QWENTRADING8SESSION.md` — план 8 сессий торговли
- `docs/Old_sessions/QWENTRADING8D_SESSION.md` — итоги сессии 8D
- `docs/Old_sessions/QWENTRADING8E_SESSION.md` — итоги сессии 8E
- `docs/Old_sessions/QWENTRADING9SESSION.md` — итоги сессии 9
- `docs/Old_sessions/SESSION_8B_PLAN.md` — план сессии 8B
- `docs/Old_sessions/KODA-UI-AGENTIC-SUMMARY.md` — первоначальный Koda анализ

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

### Ядро системы
| Скрипт | Назначение |
|--------|-----------|
| `NetworkManagerController.cs` | Обёртка NGO, обработка подключений |
| `NetworkPlayer.cs` | Игрок: движение, камера, инвентарь |
| `ShipController.cs` | Корабль: физика, кооп-пилотирование |
| `ThirdPersonCamera.cs` | Орбитальная камера от 3-го лица |
| `Inventory.cs` | Инвентарь: хранение по типам, группировка |
| `ChestContainer.cs` | Сундук с LootTable, анимация |
| `WorldGenerator.cs` | Процедурная генерация: 15 пиков, 890+ облаков |
| `PlayerController.cs` | Пеший контроллер (WASD, бег, прыжок) |

### UI система (Спринты 1-3 завершены)
| Скрипт | Назначение | Статус |
|--------|-----------|--------|
| `UIManager.cs` | ⭐ Централизованный менеджер UI (приоритеты, z-ordering) | ✅ Новый (Спринт 3) |
| `UIFactory.cs` | ⭐ Фабрика UI компонентов (8 методов) | ✅ Новый (Спринт 2) |
| `UITheme.cs` | ⭐ ScriptableObject темы (авто-создание) | ✅ Новый (Спринт 2) |
| `ConfirmationDialog.cs` | ⭐ Диалог подтверждения (отключён для торговли) | ✅ Новый (Спринт 3) |
| `TradeUI.cs` | Торговля (TextMeshPro, UITheme, UIFactory) | 🟡 Мигрирован (Спринт 2) |
| `ContractBoardUI.cs` | Контракты (TextMeshPro, UITheme, UIFactory) | 🟡 Мигрирован (Спринт 2) |
| `InventoryUI.cs` | Круговое колесо (8 секторов, semantic labels) | 🟡 Спринт 1 fixes |
| `NetworkUI.cs` | UI сети: Disconnect/Reconnect/Player Count | ✅ Good |
| `ControlHintsUI.cs` | Подсказки клавиш на экране | ✅ Good |
| `PeakNavigationUI.cs` | Навигация по пикам (скрыт в production) | ✅ Good |

### Визуал и материалы
| Скрипт | Назначение |
|--------|-----------|
| `CloudGhibli.shader` | Кастомный шейдер облаков (URP Unlit + noise + rim glow) |
| `ProceduralNoiseGenerator.cs` | Генерация FBM noise-текстур для облаков |
| `MaterialURPConverter.cs` | Авто-конвертация материалов при запуске |
| `MaterialURPUpgrader.cs` | Editor-скрипт массовой конвертации Standard → URP |
| `CloudLayer.cs` | Авто-интеграция CloudGhibli шейдера |

### Торговля
| Скрипт | Назначение |
|--------|-----------|
| `TradeItemDefinition.cs` | ScriptableObject определения товаров |
| `TradeDatabase.cs` | База данных торговли |
| `LocationMarket.cs` | Рынок локации (demand/supply) |
| `TradeMarketServer.cs` | Серверная обработка торговли (RPC) |
| `CargoSystem.cs` | Груз корабля (вес/объём/слоты) |
| `PlayerTradeStorage.cs` | Склад игрока (локация, погрузка/разгрузка) |
| `PlayerDataStore.cs` | Единый источник данных (кредиты, склады) |
| `ContractSystem.cs` | Система контрактов НП |

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

**Последнее обновление:** 11 апреля 2026 г.  
**Версия:** `v0.0.16-ui-sprint1-complete`  
**Автор документа:** Qwen Code (Game Studio)

---

## 📝 ИТОГИ СЕССИИ (11 апреля 2026) — UI Спринт 1

### Спринт 1: Критические фиксы — ЗАВЕРШЁН

| # | Задача | Файл | Изменения |
|---|--------|------|-----------|
| **1.1** | InventoryUI material leak | `InventoryUI.cs` | Добавлен `OnDestroy()` — уничтожает `_glMaterial` и вызывает `Dispose()` для InputAction |
| **1.2** | InputAction lambda subscriptions | `InventoryUI.cs` | Лямбда заменена на кэшированный делегат `_onTogglePerformed` — отписка работает |
| **1.3** | Null checks в TradeUI | `TradeUI.cs` | Добавлены `Debug.LogWarning` при null Player/PlayerTradeStorage |
| **1.4** | "Type 1-8" → semantic labels | `ItemType.cs`, `ItemTypeNames.cs`, `InventoryUI.cs` | Enum: Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech. UI показывает "Ресурсы", "Топливо" и т.д. |
| **1.5** | Cursor lock/unlock | `TradeUI.cs`, `ContractBoardUI.cs`, `InventoryUI.cs` | При открытии UI — курсор разблокирован. При закрытии — заблокирован обратно. |
| **1.6** | PeakNavigationUI debug flag | `PeakNavigationUI.cs` | Скрыт в production build. `_cachedWorldGenerator` добавлен для оптимизации. |

### Метрики после Спринт 1

| Метрика | До | После |
|---------|-----|-------|
| Memory leaks | 3 | 1 |
| FindAnyObjectByType (runtime) | 9+ | 7 |
| UI labels | "Type 1-8" | Semantic names |

### Что НЕ было сделано (Спринт 2)
- ❌ UIFactory / BaseUIPanel
- ❌ Миграция TradeUI/ContractBoardUI на TextMeshPro
- ❌ UITheme ScriptableObject
- ❌ Замена эмодзи на sci-fi иконки

### Следующий шаг
**Спринт 2: Унификация (2-3 недели)** — готов к работе.

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

---

## 🚢 СЕССИЯ 2 КОРАБЛЕЙ: Altitude Corridor System (11-12 апреля 2026)

### Реализовано ✅
- ShipController v2.1 — интеграция системы коридоров высот
- AltitudeCorridorData (ScriptableObject) — данные коридора
- AltitudeCorridorSystem (Manager, singleton) — 6 коридоров
- TurbulenceEffect — случайные силы + torque × масса × forceMultiplier(50)
- SystemDegradationEffect — модификаторы тяги/маневренности/сопротивления
- AltitudeUI — HUD (код готов, НЕ отображается)
- CreateAltitudeCorridorAssets — Editor утилита (Tools → Project C)
- 6 ScriptableObject ассетов: Global, Primus, Tertius, Quartus, Kilimanjaro, Secundus
- Теги: `backup-2_session-ship-improved`

### Известные проблемы Сессии 2
| Приоритет | Проблема | Решение |
|-----------|----------|---------|
| 🔴 P0 | AltitudeUI HUD не отображается | @unity-ui-specialist: проверить RectTransform, Canvas Scaler, sorting |
