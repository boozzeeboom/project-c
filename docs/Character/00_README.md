# Character Progression — каталог документации

> **Подсистема:** Уникальность персонажа (Stats + Progression + Clothing + Modules + Skills)
> **Версия:** v0.0.1-design (2026-06-14)
> **Статус:** 📋 Дизайн завершён, код не начат
> **Назначение:** одежда с характеристиками, модули с характеристиками, дерево навыков (социальные/боевые), 3 характеристики (Сила/Ловкость/Интеллект) с геометрическим ростом от действий игрока.
> **Связанные подсистемы:** Mining (`docs/Mining/`), Crafting (`docs/Crafting_system/`), Exchange (`docs/Markets/`), Quests (`docs/NPC_quests/`), Ship (`docs/Ships/`), Character-menu (`docs/Character-menu/`).
> **Внешний контракт:** `CharacterWindow` уже имеет 6 табов (character/ship/reputation/contracts/inventory/quests). Новые табы встраиваются как **вложенные sub-tabs** под табом "ПРОГРЕССИЯ".

---

## TL;DR для тех, кто возвращается через неделю

**Что сделано в этой сессии (14.06.2026):**

1. Проведён глубокий анализ 3-х сабагентами: (1) RPG entry-points (entry-points для подписки на серверные события), (2) Data-Model (паттерны SO), (3) UI/Player-Controller.
2. Подтверждено: **`WorldEventBus` уже существует** и `QuestServer` подписан на 7 событий — это **готовая инфраструктура** для подписки на новые события.
3. Подтверждено: **`CharacterWindow` готов к расширению** — 4 FIX'а UI Toolkit применены, 6 табов работают, P-key занят. Никаких новых keybindings не нужно.
4. Подтверждено: **ItemData уже имеет `itemType.Equipment`**, но **нет понятия "equippable"** — нужна новая `EquipmentData` + `EquipmentWorld` + `EquipmentServer` (паттерн как `InventoryWorld` + `InventoryServer`).
5. Спроектированы 3 типа ScriptableObject: `StatsConfig` (геометрический рост), `ClothingItemData`/`ModuleItemData` (расширяют `ItemData`), `SkillNodeConfig` (с `SkillEffect[]`).
6. Подготовлен roadmap из **18 тикетов T-P01..T-P18** в 4 milestone'ах.

**Открыто (нужно от тебя):**

- ❓ Какая формула геометрического роста (10 разделов в 09_OPEN_QUESTIONS.md)
- ❓ Стартовые значения (1/10/100/1000?)
- ❓ Стат-бонусы — additive или multiplicative?
- ❓ Одежда — additive бонусы или % от базовой характеристики?
- ❓ Модули — это персонажные импланты или модификации корабля?
- ❓ Какие навыки в MVP (4 combat + 4 social — какие?)
- ❓ Сколько placeholder-итераций до полноценного сервера?

---

## Структура каталога

```
docs/Character/
├── 00_README.md                    (этот файл — навигация + TL;DR)
├── 01_CURRENT_STATE_AUDIT.md       (что уже есть в проекте, готовые точки входа)
├── 02_V2_ARCHITECTURE.md           (серверный hub + DTO + ClientState + WorldEventBus подписки)
├── 03_DATA_MODEL.md                (StatsConfig, ClothingItemData, ModuleItemData, SkillNodeConfig)
├── 04_STATS_PROGRESSION.md         (геометрическая формула, источники XP, NPC-spam protection)
├── 05_CLOTHING_AND_MODULES.md      (слоты, equip/unequip, stat-bonuses)
├── 06_SKILL_TREE.md                (нодовая система, prerequisites, effects, social/combat)
├── 07_UI_TABS_IN_CHARACTER_WINDOW.md (расширение CharacterWindow, sub-tabs, row-patterns)
├── 08_ROADMAP.md                   (тикеты T-P01..T-P18, milestones, оценка)
├── 09_OPEN_QUESTIONS.md            (10 разделов, 30+ вопросов для тебя)
└── 10_REFERENCES.md                (file:line индекс всех прочитанных файлов)
```

---

## Статус проекта (1 строка)

**M1 (Stats core): ⬜ TODO → T-P01..T-P06** • **M2 (Clothing/Modules): ⬜ TODO → T-P07..T-P10** • **M3 (Skill tree): ⬜ TODO → T-P11..T-P14** • **M4 (UI integration): ⬜ TODO → T-P15..T-P18** • обновлено 2026-06-14

---

## Карта систем — что мы проектируем

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        CHARACTER PROGRESSION SUBSYSTEM                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────┐  ┌─────────────────────────┐                   │
│  │  StatsConfig (SO)       │  │  SkillNodeConfig (SO)   │                   │
│  │  base=10, growth=1.5    │  │  social/combat          │                   │
│  │  globalMultiplier=1.0   │  │  prerequisites[]        │                   │
│  └────────────┬────────────┘  │  effects[]              │                   │
│               │               └────────────┬────────────┘                   │
│               ▼                            ▼                                │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  StatsServer (NetworkBehaviour) — BootstrapScene, scene-placed      │    │
│  │  Subscribes to WorldEventBus + distance tracker (FixedUpdate)       │    │
│  │  Singleton, server-authoritative, fires StatsSnapshotDto via TargetRPC│   │
│  │  Persists: JsonCharacterRepository (character_<clientId>.json)      │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│               │                                                             │
│               ▼                                                             │
│  ┌─────────────────────────┐  ┌─────────────────────────┐                   │
│  │  ClothingItemData       │  │  ModuleItemData         │                   │
│  │  extends ItemData       │  │  extends ItemData       │                   │
│  │  slot=Head/Chest/...    │  │  slot=Module1..3        │                   │
│  │  statBonuses {STR,DEX,INT} │  statBonuses, sensor/speed │                │
│  │  requiredSkills[]       │  │  requiredSkills[]       │                   │
│  └────────────┬────────────┘  └────────────┬────────────┘                   │
│               │                            │                                │
│               ▼                            ▼                                │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  EquipmentServer (NetworkBehaviour) — BootstrapScene                │    │
│  │  EquipmentData per player (Dictionary<EquipSlot, int itemId>)       │    │
│  │  TryEquip/TryUnequip, validates skill prerequisites                  │    │
│  │  Computes total stat bonuses from equipped items                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│               │                                                             │
│               └──────────────┐                                              │
│                              ▼                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  StatsClientState (singleton) — DontDestroyOnLoad                  │    │
│  │  OnStatsUpdated event → CharacterWindow → display                  │    │
│  │  EquipmentClientState (singleton) → CharacterWindow → clothing UI  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│               │                                                             │
│               ▼                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  CharacterWindow.cs — extend with "ПРОГРЕССИЯ" tab                  │    │
│  │  Sub-tabs: [Статы] [Одежда] [Модули] [Навыки]                       │    │
│  │  Skills list: ListView с LOCKED/AVAILABLE/LEARNED states            │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Что НЕ входит в scope этой сессии (явные запреты)

- ❌ **Никакого кода** — это design-doc-only сессия. Код пишется в следующих сессиях по тикетам T-P01..T-P18.
- ❌ **Не удалять ничего** — additive-only подход (характеристики игрока нигде не пересекаются с существующим Level/XP).
- ❌ **Не модифицировать `docs/gdd/`** — игровой дизайнер пишет GDD'ы отдельно.
- ❌ **Не трогать существующие серверы** (`GatheringServer`, `CraftingServer`, `ExchangeServer`, `MarketServer`, `QuestServer`) — только добавляем `WorldEventBus.Publish` в success-ветки (минимальное изменение).
- ❌ **Не создавать `CharacterMenuWindow`, `CharacterStatsWindow`, `SkillTreeWindow`** — всё внутри существующего `CharacterWindow` (архитектурное правило из `docs/Character-menu/00_OVERVIEW.md:25-33`).

---

## Ссылки на исходные анализы

- `C:\Users\leon7\ANALYSIS_CHARACTER_RPG_UI.md` — UI/Player-Controller анализ (6723 слов, 12 секций)
- `C:\Users\leon7\ANALYSIS_CHARACTER_DATA_MODEL.md` — Data-Model/SO-паттерны анализ (~2400 слов, 12 секций, доставлен inline из-за лимита итераций сабагента)
- `C:\Users\leon7\ANALYSIS_CHARACTER_ENTRY_POINTS.md` — entry-points для подписки (210 строк, 22 KB, 6 секций)

Все три отчёта — это **сырые материалы** сабагентов. Документы в этом каталоге — **синтезированная версия** с финальными решениями.

---

## Связанные документы проекта

- `docs/Character-menu/00_OVERVIEW.md` — план 5-табового P-окна (line 25-33 — архитектурное правило "не создавать отдельные окна")
- `docs/Character-menu/10_DESIGN.md` — UXML/USS дизайн, 4 FIX'а UI Toolkit
- `docs/Character-menu/sub_inventory-tab/` — Inventory v2 референс (9 файлов)
- `docs/Mining/ROADMAP.md` — канонический шаблон roadmap'а (структура 8 секций)
- `docs/NPC_quests/02_V2_ARCHITECTURE.md` — канонический v2 hub-паттерн
- `docs/NPC_quests/08_ROADMAP.md` — пример roadmap с 22 тикетами
- `AGENTS.md` — hard rules проекта
- `MOON_SYSTEM.md` — шаблон технической спецификации подсистемы

---

## Следующий шаг

**Прочитай `01_CURRENT_STATE_AUDIT.md`** — там полная картина что есть в проекте и какие точки входа готовы.
**Затем `09_OPEN_QUESTIONS.md`** — там 10 разделов с 30+ вопросами для тебя. Твои ответы определят финальный дизайн в следующей сессии.
