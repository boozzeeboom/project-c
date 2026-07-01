# Project C: The Clouds

**MMO-песочница на Unity 6** в сеттинге постапокалиптической небесной цивилизации.

> По мотивам книги **«Интеграл Пьявица» — Бруно Арендт**

| Стек | Версия |
|------|--------|
| Unity Editor | **6000.4.1f1** |
| Render Pipeline | **URP 17.0.3** (CloudGhibli стиль) |
| Netcode | **NGO 2.11.0** |
| Cinemachine | **3.1.6** |

**Текущий этап:** 2.5 — Визуальный прототип (v0.0.35, 30 июня 2026)

```
Реализованные магистральные подсистемы:
  NPC+Quests v2 ✅  Mining v0.0.2 ✅  Crafting ✅  Exchange ✅
  Character Progression ✅  Composite Ship ✅  Docking Stations MVP ✅
  NPC Ships M3.2 ✅  Real-Time Combat + Skills ✅  NPC Enemy P0-P2 ✅
  Character Customisation L1+L3+L4 ✅  Equipment Visual Ph.2 ✅
  Input System Ph.1-2.5 ✅  Character Animations v0.5.1 ✅
```

> **Детально по каждой:** [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md)

---

## О чём это

**~1930 г.** На Землю падают метеориты с двумя веществами: **Антигравий** (нарушает гравитацию при подаче тока) и **Мезий** (яд, расщепляющий органику, t° кипения ~17°C).

Человечество (~2 млн человек) вытеснено **«над облака»** — на платформы, закреплённые на горных вершинах. Поверхность скрыта под **Завесой** — искусственным дымом, сдерживающим пары мезия.

**~2090 г.** Новая Цивилизация — 4 города-вершины (Примум, Секунд, Тертиус, Квартус), фермы на пиках, корабли на антигравии. **Новое Правительство** контролирует всё через систему **СОЛ**. Пять **Гильдий** управляют ключевыми отраслями.

**Игрок** — вольный небесный странник: берёт контракты, исследует мир, торгует, улучшает корабль, выбирает между Гильдиями и Подпольем.

### Технологии мира

| Технология | Принцип |
|------------|---------|
| **МАГ/ГМАГ** | Жидкий мезий + антигравиевый вал → электричество → рамка-контур → антигравитационное поле |
| **Ветровые лопасти** | Электродвигатели горизонтального движения |
| **Паромы** | Маршрутный транспорт по подвесным тросам (до 40 км/ч) |
| **ГРАДАР** | Гравитационный радар навигации |
| **СОЛ** | Система идентификации граждан (гравитационное сканирование) |

### Корабли

| Класс | Форма | Скорость |
|-------|-------|----------|
| Лёгкий | Торообразный, 2-4 чел. | ~350 км/ч |
| Средний | Сигарообразный, до 13 чел. | ~300 км/ч |
| Тяжёлый I | Платформа до 3 ярусов | 150 км/ч |
| Тяжёлый II | Грузовая открытая платформа | 150 км/ч |

> Физика: медленный разгон, инерция массы, плавное зависание, автоматическая стабилизация. Баржи, не истребители.

---

## Визуальный стиль

**Sci-Fi × Ghibli** — мягкие закаты, объёмные светящиеся облака, градиенты + промышленный реализм.

- **Города НП:** многоуровневые структуры на гигантских стойках, гранит, кирпич, металл
- **Фермы:** террасы на горных пиках, антигравийные платформы
- **Завеса:** фиолетовые молнии, ядовитый слой внизу
- **Облака:** 3 стратифицированных слоя, procedural generation
- **Корабли:** утилитарный дизайн с ветровыми лопастями, плавные контуры

---

## Core Loop

1. **Прими контракт** — доставка, разведка, сопровождение, крафт
2. **Выполни:**
   - Управляй кораблём (навигация, стыковка)
   - Исследуй пешком (NPC, квесты, лут, крафт)
   - Управляй ресурсами (мезий, инвентарь, груз)
3. **Прогресс:**
   - Зарабатывай кредиты и ресурсы
   - Улучшай корабль / расти репутацию
   - Развивай навыки (Skill Tree, 27+ узлов)
   - Открывай новые миссии и локации

---

## Архитектура проекта

```
C:\UNITY_PROJECTS\ProjectC_client\
├── Assets/_Project/           ← Весь игровой код и контент
│   ├── Scripts/               ← Core, Player, Ship, UI, Network, World, Combat, Skills, Customisation
│   ├── Scenes/                ← BootstrapScene + 24 WorldScene_X_Z
│   ├── Prefabs/               ← Префабы игровых объектов
│   ├── Data/                  ← ScriptableObject конфиги
│   ├── InputActions/          ← Input Action assets
│   └── Resources/UI/          ← UXML + USS шаблоны
├── docs/                      ← Вся документация
│   ├── gdd/                   ← 26+ GDD документов
│   ├── Character/             ← Customisation, Skills, Input, EnemyNPC
│   ├── NPC_quests/            ← NPC+Quests v2 (26+ сессионных логов)
│   └── Ships/                 ← Composite Ship, Key-subsystem, Docking, NPC Ships
└── src/                       ← Переиспользуемая C# математика (no Unity deps)
```

### Сетевая архитектура

- **BootstrapScene** → NetworkManager (NGO) + ClientSceneLoader (асинхронная загрузка 24+1 сцен)
- **Server-authoritative** — все ключевые системы (DamageCalculator, SkillManager, QuestServer, Inventory, Ship Key)
- **Scene-placed NetworkObject** — `ScenePlacedObjectSpawner` в BootstrapScene для старта host
- **NetworkVariable** + **RPC (NGO 2.x)** — синхронизация состояния

---

## Быстрая навигация по документации

### 🎯 Game Design Documents

| # | Документ | О чём |
|---|----------|-------|
| 01 | [Core Gameplay](docs/gdd/GDD_01_Core_Gameplay.md) | Core loop, управление, физика, режимы, **Combat + Input System** |
| 10 | [Ship System](docs/gdd/GDD_10_Ship_System.md) | 4 класса, физика, кооп-пилотирование, стыковка |
| 11 | [Inventory & Items](docs/gdd/GDD_11_Inventory_Items.md) | 8 типов, круговое колесо, LootTable, **WeaponItemData + Equipment Visual** |
| 12 | [Network & Multiplayer](docs/gdd/GDD_12_Network_Multiplayer.md) | NGO, RPC, реконнект |
| 13 | [UI/UX System](docs/gdd/GDD_13_UI_UX_System.md) | HUD, Ghibli стиль, **SkillTreeWindow, CustomisationWindow, InputRebinding** |
| 20 | [Progression & RPG](docs/gdd/GDD_20_Progression_RPG.md) | XP, уровни, **Skill Tree** (27+ навыков) |
| 21 | [Quest & Mission System](docs/gdd/GDD_21_Quest_Mission_System.md) | 5 типов, CSV pipeline, **NPC Enemy system** |
| 25 | [Combat & Skills](docs/gdd/GDD_25_Combat_Skills.md) | DamageCalculator, AOE, прицеливание, Skill Tree |
| 26 | [Character Customisation](docs/gdd/GDD_26_Character_Customisation.md) | Внешность, Equipment Visual, bone mapping |

**Полный каталог:** [`docs/gdd/GDD_INDEX.md`](docs/gdd/GDD_INDEX.md)

### 📚 Техническая документация

| Где | Что |
|-----|-----|
| [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md) | **Полный план разработки** — актуальный статус всех подсистем, roadmap |
| [`docs/WORLD_LORE_BOOK.md`](docs/WORLD_LORE_BOOK.md) | **Лор мира** — технологии, фракции, сюжет, персонажи |
| [`docs/CONTROLS.md`](docs/CONTROLS.md) | Управление (пеший режим + корабль) |
| [`docs/gdd/GDD_00_Overview.md`](docs/gdd/GDD_00_Overview.md) | Концепция, пиллары, USP |
| [`docs/dev/COMBAT_ENGINE_IMPL_PLAN.md`](docs/dev/COMBAT_ENGINE_IMPL_PLAN.md) | План имплементации Combat Engine |
| [`docs/ART_BIBLE.md`](docs/ART_BIBLE.md) | Визуальная спецификация |

### 🧩 Документация подсистем

| Подсистема | Каталог |
|------------|---------|
| Character Customisation + Equipment Visual | [`docs/Character/Customisation/`](docs/Character/Customisation/) |
| Skills + Combat | [`docs/Character/Skills/`](docs/Character/Skills/) |
| Input Rebinding | [`docs/Character/Input/`](docs/Character/Input/) |
| NPC Enemy | [`docs/Character/EnemyNPC/`](docs/Character/EnemyNPC/) |
| NPC+Quests v2 | [`docs/NPC_quests/`](docs/NPC_quests/) |
| Crafting | [`docs/Crafting_system/`](docs/Crafting_system/) |
| Resource Gathering (Mining) | [`docs/Mining/`](docs/Mining/) |
| Ship System (Composite, Key, Docking, NPC Ships) | [`docs/Ships/`](docs/Ships/) |
| Market / Exchange | [`docs/Markets/`](docs/Markets/) |
| MetaRequirement (lock-key) | [`docs/MetaRequirement/`](docs/MetaRequirement/) |
| CharacterMenu / Inventory | [`docs/Character-menu/`](docs/Character-menu/) |

---

## Технический стек

| Компонент | Технология |
|-----------|-----------|
| **Клиент** | Unity 6000.4.1f1, URP 17.0.3 |
| **Сеть** | Netcode for GameObjects 2.11.0 |
| **UI** | Unity UI Toolkit |
| **Анимации** | AnimatorController + BlendTree (8-way movement) |
| **Визуальные эффекты** | VFX Graph 17.4.0 |
| **Камеры** | Cinemachine 3.1.6 |
| **Язык** | C# (Unity) + HLSL (шейдеры) |
| **Build target** | StandaloneWindows64 |

---

**Контакты:** [@indeed174](https://t.me/indeed174) · [github.com/boozzeeboom/project-c](https://github.com/boozzeeboom/project-c)
