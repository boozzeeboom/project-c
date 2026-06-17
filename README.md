# Project C: The Clouds
**Version:** 0.0.26 | **Stage:** Этап 2.5 В ПРОЦЕССЕ (Визуальный прототип)
**Подсистемы:** NPC+Quests v2 ✅ (M1–M19.3) | Character Progression ✅ (T-P01–T-P18) | Gathering (Mining) ✅ (T-G01–T-G07) | **Crafting ✅ (T-C01–T-C07c)** | **Exchange ✅ (T-E01–T-E05)** | **Composite Ship ✅ (Phase 0–1)**
**По мотивам книги «Интеграл Пьявица» — Бруно Арендт**
## Весь проект: [TheGravity](https://thegravity.ru) & [TheClouds](https://thegravity.ru/project-c/)

---

> **Что нового в v0.0.26 (17 июня 2026):** **Composite Ship Architecture — Phase 0+1.** Корабль больше не монолитный блок — теперь составная конструкция (летающая баржа). `ShipRootReference` (маркер части корабля), `ShipComponentLocator` (поиск ShipController), `PilotSeatController` (место пилота как отдельный child), `DoorController` (slide E-key). Игрок НЕ пропадает при посадке — стоит в кресле, видимый. Парентинг к корню корабля — физика плавная, без дерганий. Камера следит за ShipRoot, а не за игроком. Полный анализ: `docs/Ships/analysis-composite-ship.md`.
> **Предыдущее (v0.0.25):** Character Progression — 18 тикетов T-P01..T-P18 ✅ + UI рефакторинг CharacterWindow. 3 характеристики (STR/DEX/INT) с геометрическим ростом от mining/walking/dialog. 13 слотов экипировки (10 одежда + 3 модуля). Effective stats (base + equip bonuses). [НАДЕТЬ]/[СНЯТЬ]. 8 навыков (4 боевых + 4 социальных). UI: single-page ПЕРСОНАЖ, inventory split layout, ScrollView вместо ListView. Compile 0 errors. См. `docs/Character/`.
> **Предыдущее (v0.0.23):** **CSV Pipeline — финал!** Теперь контент-райтер забивает квесты в Excel → CSV → 1 кнопка в Unity → готово. Auto-создание NPC из CSV (displayName, faction, questOffers, questTurnIns). `npcs.csv` (9 колонок: services, attitude, greeting, voice, radius). `dialogs.csv` (15 колонок: деревья диалогов с 11 условиями и 17 действиями). Auto-link `{npcId}_default` к NPC. Тестовый импорт: **106 NPC, 802 квеста** — 1 кнопка. Примеры: `Import/example_quests.csv`, `example_npcs.csv`, `example_dialogs.csv`. Writer-документация: `M19_CSV_PIPELINE_v2.md`.
> **Предыдущее (v0.0.21):** Crafting (крафт-система) — MVP завершён! Две станции в мире: Верстак (3 рецепта: медный слиток, железный слиток, ключ корабля) и Верфь (ключ ShipLight). Подойти → F → окно → выбрать рецепт → добавить ресурсы → запустить крафт → таймер с ProgressBar + тост → Готово → предмет в инвентаре. Станции работают независимо, анимация свечения в процессе. 9 тикетов, все проверено в Play Mode.
> **См. полную историю:** `docs/MMO_Development_Plan.md`
**no marketing/bullshit/tech-heavy/sound sections**
---

## 📁 Документация

Вся документация проекта находится в папке [`docs/`](docs/):

### 📋 Game Design Documents (GDD)

Полная спецификация всех игровых систем в папке [`docs/gdd/`](docs/gdd/):

| # | Документ | Описание |
|---|----------|----------|
| 00 | [GDD_00: Game Overview](docs/gdd/GDD_00_Overview.md) | Концепция, пиллары, USP, целевая аудитория |
| 01 | [GDD_01: Core Gameplay](docs/gdd/GDD_01_Core_Gameplay.md) | Core Loop, управление, физика, режимы |
| 02 | [GDD_02: World & Environment](docs/gdd/GDD_02_World_Environment.md) | Мир, 15 пиков, 4 города, Завеса, погода |
| 10 | [GDD_10: Ship System](docs/gdd/GDD_10_Ship_System.md) | 4 класса кораблей, физика, кооп-пилотирование |
| 11 | [GDD_11: Inventory & Items](docs/gdd/GDD_11_Inventory_Items.md) | 8 типов, круговое колесо, LootTable, сундуки |
| 12 | [GDD_12: Network & Multiplayer](docs/gdd/GDD_12_Network_Multiplayer.md) | NGO, RPC, реконнект, Dedicated Server |
| 12.1 | [GDD_12.1: Scene World Streaming](docs/gdd/GDD_12_1_Scene_World_Streaming.md) | 24 сцены, 4×6 grid, boundary-based loading |
| 13 | [GDD_13: UI/UX System](docs/gdd/GDD_13_UI_UX_System.md) | HUD, Ghibli стиль, адаптивность, доступность |
| 14 | [GDD_14: Visual & Art Pipeline](docs/gdd/GDD_14_Visual_Art_Pipeline.md) | URP, CloudGhibli, шейдеры, постобработка |
| 15 | [GDD_15: Audio System](docs/gdd/GDD_15_Audio_System.md) | AudioMixer, SFX, музыка, 3D звук |
| 20 | [GDD_20: Progression & RPG](docs/gdd/GDD_20_Progression_RPG.md) | XP, уровни 1-50, деревья навыков |
| 21 | [GDD_21: Quest & Mission System](docs/gdd/GDD_21_Quest_Mission_System.md) | 5 типов квестов, цепочки гильдий |
| 22 | [GDD_22: Economy & Trading](docs/gdd/GDD_22_Economy_Trading.md) | Кредиты, ресурсы, спрос/предложение |
| 23 | [GDD_23: Faction & Reputation](docs/gdd/GDD_23_Faction_Reputation.md) | 5 Гильдий, подполье, СОЛ, репутация |
| 24 | [GDD_24: Narrative & World Lore](docs/gdd/GDD_24_Narrative_World_Lore.md) | Хронология, глоссарий, сюжетные арки |

**Полный каталог GDD:** [`docs/gdd/GDD_INDEX.md`](docs/gdd/GDD_INDEX.md)

### 📚 Техническая документация

| Файл | Описание |
|------|----------|
| [`docs/WORLD_LORE_BOOK.md`](docs/WORLD_LORE_BOOK.md) | **Полный лор книги** — мир, технологии, гильдии, персонажи, сюжет |
| [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md) | **Полный план разработки** MMO игры (v0.0.20-gathering-system-complete) |
| [`docs/QWEN_CONTEXT.md`](docs/QWEN_CONTEXT.md) | **Текущий контекст** — что сделано, какие задачи в работе |
| [`docs/STEP_BY_STEP_DEVELOPMENT.md`](docs/STEP_BY_STEP_DEVELOPMENT.md) | **Пошаговая разработка** |
| [`docs/CONTROLS.md`](docs/CONTROLS.md) | Документация по управлению |
| [`docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md`](docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md) | **Composite Ship Architecture** — составной корабль, Phase 0–1 |
| [`docs/ART_BIBLE.md`](docs/ART_BIBLE.md) | Визуальная спецификация |
| [`docs/world/CLOUD_system/STORM_EVENT_INTEGRATION_LOG.md`](docs/world/CLOUD_system/STORM_EVENT_INTEGRATION_LOG.md) | Storm Cloud System — implementation log |
| [`docs/world/CLOUD_system/STORM_SETUP_GUIDE.md`](docs/world/CLOUD_system/STORM_SETUP_GUIDE.md) | Storm setup & testing guide |
| [`docs/world/LargeScaleMMO/2_iteration_scene-mode/SYSTEM_OVERVIEW.md`](docs/world/LargeScaleMMO/2_iteration_scene-mode/SYSTEM_OVERVIEW.md) | **24+1 Scene World System** — documentation |
| [`docs/GIT_WORKFLOW.md`](docs/GIT_WORKFLOW.md) | Шпаргалка Git команд |
| [`docs/GIT_WORKFLOW_ADVANCED.md`](docs/GIT_WORKFLOW_ADVANCED.md) | Продвинутый Git workflow |
| [`docs/QUICK_GIT_COMMANDS.md`](docs/QUICK_GIT_COMMANDS.md) | Быстрые команды Git |
| [`docs/VERSION_BACKUP.md`](docs/VERSION_BACKUP.md) | Резервное копирование |

#### 🆕 v0.0.20 — Новые подсистемы (NPC + Quests, Character, Lock-Key, Mining)

| Каталог | Описание |
|---------|----------|
| [`docs/Mining/`](docs/Mining/) | **Resource Gathering (Mining) v0.0.2** — 3D-объекты сбора, F-key, ProgressBar, tool check (MetaReq), анимация. 7 тикетов T-G01–T-G07 ✅. |
|| [`docs/NPC_quests/`](docs/NPC_quests/) | **NPC + Quests v2** — главный roadmap (50+ тикетов, M1–M19.3 ✅, новые: M19.3 CSV Pipeline NPC/dialogs, сессионные логи, риски). 19 milestones: data foundation, server core, real-time objectives, ItemRegistry, Toast, QuestDatabaseWindow, QuestNodeGraph, CSV Import/Export → **M19.3 CSV Pipeline** (NPC/dialogs auto-import из CSV), Mira E2E demo. |
| [`docs/NPC_quests/old_session_log/`](docs/NPC_quests/old_session_log/) | **Исторические devlog'и** — 27 файлов (M*, T-Q*, 99_FINAL_STATUS). Не читать для текущей работы; для возврата к старому. |
| [`docs/Character-menu/`](docs/Character-menu/) | **CharacterWindow v2** — 5+ табов (Персонаж, Корабль, Репутация, Контракты, Инвентарь, Квесты), P-key для открытия, 4 FIX'ы от MarketWindow, Visual fix 2026-06-05. |
| [`docs/Character-menu/sub_inventory-tab/`](docs/Character-menu/sub_inventory-tab/) | **sub_inventory-tab** — Inventory v2 (Phases 0-7), TAB-колесо + P-таб, single source of truth с `InventoryClientState`. 8 файлов (~150 KB). |
| [`docs/MetaRequirement/`](docs/MetaRequirement/) | **MetaRequirement v1 (lock-key)** — универсальная система требований (R2-META-REQ-001). Массив предметов с логикой ALL/ANY/AT_LEAST_N. 9 файлов. |
| [`docs/Ships/Key-subsystem/`](docs/Ships/Key-subsystem/) | **Ship Key MVP** (R2-SHIP-KEY-001) + **Migration guide** → MetaRequirement. Физический ключ-предмет для запуска корабля. |

**Полный каталог:** [`docs/INDEX.md`](docs/INDEX.md)

---

## 1. Мир

**1930 год.** На Землю падают метеориты, неся два вещества:
- **Антигравий** — металл, нарушающий гравитацию при подаче тока
- **Мезий** — яд, расщепляющий органику (t° кипения ≈ 17°C)

Человечество (~2 млн человек) вытеснено **«над облака»** — на платформы, закреплённые на горных вершинах. Поверхность скрыта под **Завесой** — искусственным дымом, сдерживающим пары мезия.

**~2090 год.** Новая Цивилизация — 4 города на вершинах (Примум/Эверест, Секунд, Тертиус, Квартус), фермерские угодья на пиках, корабли на антигравии курсируют между поселениями. Тоталитарное **Новое Правительство** контролирует всё через систему **СОЛ**. Пять **Гильдий** управляют инженерию, медицину, охрану, порядок и экономику.

### Технологии
- **МАГ/ГМАГ** — мезий-антигравиевые генераторы: жидкий мезий + антигравиевый вал → электричество → рамка-контур корабля → антигравитационное поле
- **Ветровые лопасти** — электродвигатели горизонтального движения
- **Паромы** — маршрутный транспорт по подвесным тросам (до 40 км/ч)
- **ГРАДАР** — гравитационный радар навигации
- **СОЛ** — система идентификации граждан (гравитационное сканирование)

### Корабли
| Класс | Форма | Скорость |
|-------|-------|----------|
| Лёгкий | Торообразный, 2-4 чел. | ~350 км/ч |
| Средний | Сигарообразный, до 13 чел. | ~300 км/ч |
| Тяжёлый I | Платформа до 3 ярусов | 150 км/ч |
| Тяжёлый II | Грузовая открытая платформа | 150 км/ч |

**Физика:** медленный разгон, нет крена, плавное зависание, инерция массы. Баржи, не истребители.

---

## 2. Визуальный стиль: Sci-Fi + Ghibli

**Архитектура:**
- **Города НП:** многоуровневые структуры на гигантских стойках, уходящих в Завесу. Гранит, кирпич, металл
- **Фермы:** террасы на горных пиках, антигравийные опорные платформы
- **Платформы:** промышленный дизайн (сталь, трубы) + плавные контуры, купола, intricate мосты

**Небо и облака:**
- **Ghibli-эстетика:** мягкие закаты, объёмные светящиеся облака
- **Реалистичные слои облаков** стратифицированы по высоте
- **Тех-элементы:** Завеса с фиолетовыми молниями, патрульные дроны

**Корабли:**
- Утилитарный дизайн с ветровыми лопастями + плавные контуры, градиентная окраска

---

## 3. Геймплей

### Управление (PC)

#### Пеший режим (вид от третьего лица)
| Клавиша | Действие |
|---------|----------|
| W/S | Вперёд/назад |
| A/D | Поворот влево/вправо |
| Мышь | Вращение камеры |
| Left Shift | Бег |
| Space | Прыжок |
| **E** | Взаимодействие: NPC (диалог) / PickupItem / Chest / MetaRequirement (lock-key) |
| **F** | Сесть в корабль (ближайший < 5м) — **🟡 требуется ключ** ([Ship Key](docs/Ships/Key-subsystem/00_OVERVIEW.md) / [MetaRequirement](docs/MetaRequirement/00_OVERVIEW.md)) |
| **Tab** | Круговое колесо инвентаря (GTA-стиль, 8 секторов) |
| **P** | CharacterWindow (5+ табов: Персонаж, Корабль, Репутация, Контракты, Инвентарь, Квесты) |
| ESC | Закрыть активное окно |

#### Режим корабля
| Клавиша | Действие |
|---------|----------|
| W/S | Тяга вперёд/назад |
| A/D | Рыскание (поворот) |
| Q/E | Подъём/спуск (лифт) |
| Мышь Y | Тангаж (нос вверх/вниз) |
| Left Shift | Ускорение |
| Z/X | Крен (требует MODULE_ROLL) |
| C/V | Мезиевый тангаж (требует MODULE_MEZIY_PITCH) |
| Shift+A/D | Мезиевое рыскание (требует MODULE_MEZIY_YAW) |
| Shift+W/S | Мезиевый рывок/торможение (требует MODULE_MEZIY_THRUST) |
| L | Дозаправка (stationary) |
| F3 | Debug HUD |
| F4 | Meziy Status HUD |
| **F** | Выйти из корабля |

**Полная карта:** [`docs/CONTROLS.md`](docs/CONTROLS.md)

### Физика полёта
- **Антигравитация** компенсирует гравитацию — корабль зависает
- **Ветровые лопасти** обеспечивают горизонтальную тягу
- **Плавность** — ключевая характеристика (масса + инерция)
- **Автоматическая стабилизация** — возврат к горизонту
- **Нет крена** — рамка-контур стабилизирует

### Исследование
- **4 города НП** — торговые аллеи, административные центры, хостелы, библиотеки
- **Фермерские угодья** — разбросаны по горным пикам (Эверест, Килиманджаро, Эльбрус)
- **Заброшенные корабли** — остовы, лут, убежища потеряшек
- **Завеса** — ядовитый слой, спуск = опасность
- **NPC + Диалоги** — E на NPC открывает DialogWindow (typewriter, F-skip), ветвящиеся сюжеты
- **Квестовые триггер-зоны** — `TriggerZone_*` для auto-discover квестов (M13)

### Фракции
- **5 Гильдий** (Мысли, Созидания, Силы, Тайн, Успеха) — ранговая система, квесты, репутация
- **Подпольные организации** — сопротивление, свободные торговцы, культ Фрейхейта
- **Новое Правительство** — тоталитарный контроль, цензура, репрессии
- **ReputationClientState + NpcAttitudeClientState** — `ProjectC.Factions.FactionId` (12 lore значений), per-NPC attitude. Dialog actions `AddReputation` / `AddNpcAttitude` (T-Q13, T-Q16). Mira E2E: `+25 GuildOfThoughts, +10 mira_01`.

---

## 4. Core Loop

1. **Прими контракт** — доставка, разведка, сопровождение,走私
2. **Выполни:**
   - Управляй кораблём (навигация, стыковка, торговля)
   - Исследуй локации пешком (NPC, квесты, лут)
   - Управляй ресурсами (мезий, МНП, груз)
3. **Прогресс:**
   - Зарабатывай кредиты/ресурсы
   - Улучшай корабль / расти репутацию у фракций
   - Открывай новые миссии и локации
4. **Квесты (NPC+Quests v2):**
   - `E` на NPC → `DialogWindow` (typewriter, F-skip) → принять квест
   - `QuestTracker` (HUD overlay) — отслеживает текущий quest + objective counter
   - `QuestToast` — "📜 Accepted", "💚 +5", "💰 +200 CR", "✨ Найден квест"
   - Persistence: `JsonQuestStateRepository` — квесты + репутация переживают restart сервера

**Пример:** Игрок доставляет груз на базу → находит квест на артефакт → находит его на заброшенной платформе → улучшает корабль → открывает миссии Гильдии.

**Mira E2E (M11, верифицировано 2026-06-08):** Pickup ключа → E → Mira → "Помогу" → TakeItem → AcceptQuest → Active → Pickup кристалла → E → "Отдать" → AddRep 25 + AddAtt 10 + GiveCredits 1000 → Completed. Полный playthrough: `old_session_log/M11_COMMIT_SUMMARY.md`.

---

## 5. Уникальные особенности

- **Основано на книге** — глубокий лор, уникальные технологии, фракции, история
- **Ghibli x Sci-Fi** — градиенты и мягкий свет + промышленный реализм
- **Нет магии** — всё на антигравии и мезии
- **Плавный полёт** — корабли-баржи, не истребители
- **Стелс** — обход системы СОЛ, избежание правительственных агентов
- **Шифрование** — головоломки с кодами и дешифровкой

---

## 6. Co-Op / MMO

**Цель:** MMO с открытым миром, шардингом, серверной экономикой и сохранением прогресса.

**Реалистичный план:** Разработка MMO — масштабная задача. Если не хватит навыков, времени или ресурсов — проект будет масштабирован до **кооп-режима на 2-4 игрока** в одной сессии. В любом случае сетевая игра заложена с самого начала (Netcode for GameObjects, авторитарный сервер на .NET 8).

---

## 7. Технический стек

| Компонент | Технология |
|-----------|-----------|
| Клиент | Unity 6, URP, Input System |
| Сеть | Netcode for GameObjects |
| Сервер | .NET 8 |
| Язык | C# |

---

## 8. Текущий статус

**Ветка:** `feature/npc-quest-v2` (merged) | **Версия:** `v0.0.19-npc-quests-v2-complete`

### ✅ Реализовано
- Процедурная генерация мира (15 горных пиков + 890+ облаков, 3 слоя)
- **Система штормов (Storm Cloud System):**
  - StormCloudGenerator — пул штормов (max 5), спавн по паттерну
  - CloudSpherePhysics — parting physics (сферы разлетаются при пролёте игрока)
  - EventCloud — event-driven шторма через ServerStormManager
  - CloudLayerConfig — ScriptableObject с generator7.0 параметрами
  - RuntimeMeshSampler — runtime sampling меша для ParentMeshPath
  - ⏳ Parent mesh pattern, Lightning VFX, Runtime loading (отложено)
- Контроллер персонажа (пеший режим: WASD, бег, прыжок)
- **Корабли (Сессии 1-5_4, ShipController v2.7):**
  - Smooth movement — Mathf.SmoothDamp, инерция, стабилизация
  - 4 класса: Light/Medium/Heavy/HeavyII (масса 800-2000кг)
  - Altitude Corridor System — коридоры высот, турбулентность, деградация
  - Wind & Environmental Forces — зоны ветра (Constant, Gust, Shear)
  - Module System — 7 модулей (YAW_ENH, PITCH_ENH, LIFT_ENH, ROLL, MEZIY_*)
  - Fuel System — расход/регенерация, дозаправка L
  - Meziy Passive/Active/Overheat — C/V (pitch), Z/X (roll), Shift+A/D (yaw), Shift+W/S (thrust)
  - Meziy Status HUD (F4) — индикаторы, прогресс-бары
  - Co-op пилотирование — несколько игроков, усреднение ввода
- Переключение режимов F (пеший ↔ корабль, радиус 5м)
- Third-person камера (адаптивная)
- WorldCamera (режим свободного полёта для разработки)
- UI: подсказки управления, навигация по пикам
- **Inventory v2 (Phases 0-7, 2026-06-05):**
  - Круговое колесо (TAB) + детальный список (P-таб sub_inventory-tab)
  - Single source of truth: `InventoryClientState` (server-authoritative)
  - 8 типов предметов, сундуки с LootTable, drop в мир
  - `JsonInventoryRepository` (atomic JSON per-client в persistentDataPath)
- Сетевой мультиплеер (Host + Client + Dedicated Server)
- Disconnect/Reconnect UI (авто-реконнект, сохранение инвентаря)
- Синхронизация подбора (предметы/сундуки исчезают у всех)
- Player Count (счётчик игроков в реальном времени)
- Торговля — динамическая экономика, контракты, PlayerDataStore
- **24+1 Scene World System (Этап 2.1):**
  - 24 сцены в сетке 4×6 (79,999×79,999 units каждая)
  - ClientSceneLoader, ServerSceneManager, SceneID, SceneRegistry
  - Стратегия "1+1": текущая + 1 предзагруженная соседняя
  - Предзагрузка при приближении к границе (10,000 units)
  - Интеграция с FloatingOriginMP, RPC для переходов
- **URP Pipeline** — Universal Render Pipeline 17.4.0
- **CloudGhibli.shader** — кастомный шейдер облаков (noise + rim glow)
- **MaterialURPUpgrader** — массовая конвертация Standard → URP
- **docs/ART_BIBLE.md** — полная визуальная спецификация
- **Day-Night Cycle System (Этап 2.5):**
  - ServerWeatherController — timeOfDay + temperature, ClientRpc broadcasting
  - DayNightController — 5 фаз (Morning/Midday/Evening/Twilight/Night), smooth transitions
  - 3 VolumeProfiles: Day/Night/Twilight (Bloom, Vignette, ColorAdjustments)
  - Temperature filter via dedicated Volume (priority 200, aggressive color grading)
  - Fog + Ambient lighting per phase
  - Skybox materials: Day/Night/Twilight (material swap)
  - Sun directional light animation (position follows server time)
  - Moon mesh + phase material (MoonController) at 400000 distance
  - ConstellationController — 215 stars, 24 constellations, sky dome radius 900000
  - Runtime profile instantiation (prevents asset reset on play/stop)
- **🆕 Ship Key Subsystem (R2-SHIP-KEY-001, 2026-06-06):** `ShipKeyBinding` + `ShipKeyServer` + `ShipKeyClientState` + `ShipKeyToast`. 3 ключа (`Item_Key_ShipLight/Medium/Heavy`), F-boarding с server-side валидацией. [Документация](docs/Ships/Key-subsystem/00_OVERVIEW.md).
- **🆕 MetaRequirement v1 (R2-META-REQ-001, 2026-06-06):** Универсальная система требований. `MetaRequirementRegistry` + `MetaRequirementClientState` + `MetaRequirementToast` + 4 extensions в `InventoryWorld` (`HasAllItems` / `HasAnyItem` / `CountOf` / `GetMissingItems`). Логика ALL/ANY/AT_LEAST_N. [Документация](docs/MetaRequirement/00_OVERVIEW.md).
|- **🆕 CharacterWindow v2 (2026-06-05):** P-окно, 5+ табов (Персонаж, Корабль, Репутация, Контракты, Инвентарь, Квесты), 4 FIX'ы от MarketWindow, visual fix (characterWindowUss). [Документация](docs/Character-menu/00_OVERVIEW.md).
|- **🆕 Character Progression (2026-06-15..17):** 18 тикетов T-P01..T-P18, 4 milestone M1-M4. 3 характеристики (STR/DEX/INT) с геометрическим ростом. Источники XP: майнинг→STR, ходьба/прыжок→DEX, квесты/диалоги→INT. 13 слотов экипировки (10 одежда + 3 модуля). Effective stats (base + equip bonuses). [НАДЕТЬ]/[СНЯТЬ] через Inventory. Save/load (auto-save 30s). 8 навыков (4 боевых + 4 социальных). CharacterWindow: single-page ПЕРСОНАЖ layout. [Документация](docs/Character/00_README.md).
- **🆕 NPC + Quests v2 (50+ тикетов, 19 milestones ✅, 2026-06-07..09):**
  - **M1-M11 Foundation + E2E:** Data foundation (FactionId, NpcAttitude, QuestDefinition, DialogTree, NpcDefinition, FactionDefinition) → Server core (QuestServer, QuestWorld, QuestInstance, RPCs) → Player interaction (E→NPC, DialogWindow) → Reputation + NpcAttitude → Item integration (TryRemove, ContractMetaBridge) → Action set (GiveCredits/AddRep/AddAtt) → Persistence (JsonQuestStateRepository, immediate save) → Cleanup (v1 NPC removed) → Editor (QuestDatabase) → **Mira E2E demo** (10 bugfixes, verified).
  - **M13 Real-time objectives:** Server tick (5 sec) → `EvaluateAndAdvanceStage()` → multi-stage квесты → onEnter/onComplete actions → `TryTurnIn` мигрирован на `TryAdvanceStage`.
  - **M14 ItemRegistry:** Single source of truth для 32 items, `id ↔ ItemData` mapping. Устраняет fragile `Resources.LoadAll` alphabetical order.
  - **M15 Toast notifications:** `QuestToast` (queue-based) — "📜 Accepted", "💚 +5", "💰 +200 CR", "✨ Найден квест".
  - **M16 QuestDatabaseWindow:** UI Toolkit EditorWindow — `Tools > ProjectC > Quests > Quest Database Explorer`.
  - **M17 QuestNodeGraph (readonly):** `Tools > ProjectC > Quests > Quest Node Graph`, 4 node types.
  - **M18 Editable QuestNodeGraph:** T-Q30..T-Q34 — TextField, save back to SO, add/delete stages, prereq edge, drag-create.
  - **M19 CSV Import/Export:** Single-file flat CSV, 18 колонок, `Tools > ProjectC > Quests > CSV Import/Export`. Round-trip compatible.
  - **Тестовые квесты:** `collect_copper_ore`, `find_artifact` (EventDriven), `stage_intro_demo`, `stage_multi_demo`, `collect_copper` (CSV-imported).
  - [Документация](docs/NPC_quests/08_ROADMAP.md) + [Итоговый статус 2026-06-09](docs/NPC_quests/old_session_log/99_FINAL_STATUS.md).

- **🆕 Composite Ship Architecture (Phase 0–1, 2026-06-17):** Переход от монолитного блока к составной конструкции. `ShipRootReference`, `ShipComponentLocator`, `PilotSeatController` (место пилота child), `DoorController` (slide E-key). Игрок видим при посадке, парентится к ShipRoot. Камера следит за корнем. [Анализ](docs/Ships/analysis-composite-ship.md) | [Roadmap](docs/Ships/roadmap-integration.md).
- **🆕 Crafting System (T-C01–T-C07c, 2026-06-10):** Крафт-станции в мире (Верстак + Верфь), рецепты (слитки, ключ корабля), UI с ингредиентами, таймер крафта 10с, анимация станции.
  - [Анализ](docs/Crafting_system/10_DESIGN.md) | [Roadmap](docs/Crafting_system/ROADMAP.md)

- **🆕 Resources Exchanger (T-E01–T-E05, 2026-06-11):** Мост между двумя системами предметов — pickable (инвентарь) и boxed (склад). 4-я вкладка «Обменник» в MarketWindow. Pack: 100 осколков → 1 ящик. Unpack: 1 ящик → 100 осколков. Config-driven (DefaultExchangeRate.asset). MAX_SLOTS=1000.
  - [Анализ](docs/Markets/Resources_exchanger/01_ANALYSIS.md) | [Реализация](docs/Markets/Resources_exchanger/02_IMPLEMENTATION.md)

### 🔄 В процессе (открыто)

- **M12 — Input remap (F = pickup, E = NPC)** — T-X4 future TODO. Сейчас E = NPC, pickup = F, ship boarding = F (с ключом). [Дорожная карта §8.3 T-X4](docs/NPC_quests/08_ROADMAP.md).
- **Quest content creation** — 5 тестовых квестов, нужен авторский контент (5-10 production квестов на базе Mira, FindArtifact, EventDrivenQuest).
- **M17 polish — edges always visible** в QuestGraphView (~1 ч).
- Модель корабля (Blender → FBX, замена примитива)
- Модель персонажа (Mixamo)
- Текстуры горных пиков (Poly Haven)
- Post-Processing — ✅ ЗАВЕРШЕНО (Bloom, ColorAdjustments, Vignette работают)
- ⏳ Moon orbit angle fine-tuning (mesh visible, phases work)
- ⏳ Рефакторинг ShipController.cs — разделение на подсистемы
- ⏳ T-X2 — Faction migration (TradeItemDefinition → FactionId) — design discussion needed
- ⏳ Localization (все строки в .po / LocalizationTable)
- ⏳ MetaRequirement `_consumeOnUse` логика + `ProgressInfo` UI (Этап 2+)

---

**Подробный roadmap всех 19 milestones + 50+ тикетов:** [`docs/NPC_quests/08_ROADMAP.md`](docs/NPC_quests/08_ROADMAP.md)
**Полный план проекта (Этапы 0-7):** [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md)
**GDD-каталог (дизайн всех систем):** [`docs/gdd/GDD_INDEX.md`](docs/gdd/GDD_INDEX.md)
**Resources Exchanger (Pack/Unpack инвентарь↔склад):** [`docs/Markets/Resources_exchanger/`](docs/Markets/Resources_exchanger/README.md)
**Итоговый статус 2026-06-09 (17/19 milestones done):** [`docs/NPC_quests/old_session_log/99_FINAL_STATUS.md`](docs/NPC_quests/old_session_log/99_FINAL_STATUS.md)

**Репозиторий:** [github.com/boozzeeboom/project-c](https://github.com/boozzeeboom/project-c)
**Контакт:** [@indeed174](https://t.me/indeed174)
