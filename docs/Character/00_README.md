# Character Progression — каталог документации

> **Подсистема:** Уникальность персонажа (Stats + Progression + Clothing + Modules + Skills)
> **Версия:** v1.0-implemented (2026-06-17, актуализировано 2026-07-26)
> **Статус:** ✅ **Полностью реализовано.** Код: `StatsServer`, `EquipmentServer`, `SkillsServer` (+World, +ClientState, +DTO), 9 WorldEventBus событий, JsonCharacterDataRepository.
> **Назначение:** одежда с характеристиками, модули с характеристиками, дерево навыков (социальные/боевые), 3 характеристики (Сила/Ловкость/Интеллект) с геометрическим ростом от действий игрока.
> **Ключевые файлы:** `Assets/_Project/Scripts/Stats/`, `Scripts/Equipment/`, `Scripts/Skills/`, `Core/WorldEventBus.cs`, `Core/WorldEvent.cs`.
> **Связанные подсистемы:** Mining (`docs/Mining/`), Crafting (`docs/Crafting_system/`), Markets (`docs/Markets/`), Quests (`docs/NPC_quests/`), Ship (`docs/Ships/`), Character-menu (`docs/Character-menu/`).

---

## TL;DR — актуальное состояние (июль 2026)

**Что реализовано (июнь–июль 2026):**

1. **StatsServer** (`Assets/_Project/Scripts/Stats/StatsServer.cs`) — NetworkBehaviour, подписан на 9 WorldEventBus событий. FixedUpdate distance tracker для walk/pilot XP. Применяет XP через StatsConfig (геометрический рост). Persist в `character_<clientId>.json`.
2. **EquipmentServer** (`Assets/_Project/Scripts/Equipment/EquipmentServer.cs`) — TryEquip/TryUnequip RPC, валидация skill prerequisites. Stats bonus от одежды/модулей.
3. **SkillsServer** (`Assets/_Project/Scripts/Skills/SkillsServer.cs`) — Learn/Forget RPC, проверка prerequisites, эффекты.
4. **9 WorldEventBus событий** добавлено: `MiningCompletedEvent`, `CraftingCompletedEvent`, `ExchangeCompletedEvent`, `MarketTradedEvent`, `QuestAcceptedEvent`, `QuestCompletedEvent`, `ShipPilotTickEvent`, `PlayerJumpedEvent`, `AttackLandedEvent`/`DamageDealtEvent`/`EntityKilledEvent`.
5. **StatsClientState**, **EquipmentClientState**, **SkillsClientState** — singleton'ы с OnUpdated events → CharacterWindow.
6. **CharacterWindow** — расширен табом «Прогрессия» с sub-tabs (Статы/Одежда/Модули/Навыки).
7. **JsonCharacterDataRepository** — атомарная запись через tmp+Move.

**Что ещё открыто:**
- Часть вопросов из `09_OPEN_QUESTIONS.md` всё ещё актуальна (баланс, формулы).
- `11_STATS_ARCHITECTURE_AUDIT.md` (2026-07-26) — 7 проблем + план рефакторинга.

---

## Структура каталога

```
docs/Character/
├── 00_README.md                          (этот файл — навигация + TL;DR)
├── 11_STATS_ARCHITECTURE_AUDIT.md        (2026-07-26: аудит статов — 7 проблем + план)
├── 12_STATS_ARCHITECTURE_AUDIT_V2.md     (2026-07-09: глубокий trace code STR/DEX/INT)
├── 14_PLAYTESTS_STATS_AUDIT.md           (2026-07-09: playtest-гайд 10 исправлений)
├── CHANGELOG.md                          (история изменений)
├── INVESTIGATION_CHARACTER_MICRO_JITTER.md
├── INVESTIGATION_GHOST_PLAYER_CLONE.md
│
├── Character-menu/                       (CharacterWindow: UI + инвентарь)
│   ├── CHARACTER_WINDOW_INVENTORY_TAB_REFACTOR.md
│   ├── recon_visual_bugs.md
│   ├── recon_visual_fix_plan.md
│   └── sub_inventory-tab/                (Inventory v2: дизайн, реализация, тесты)
│
├── Customisation/                        (кастомизация персонажа)
│   └── CHANGELOG.md
│
├── EquipmentVisual/                      (3D-меши предметов, надевание на персонажа)
│   ├── 02_CHARACTER_APPLIER.md           (CharacterEquipmentVisualApplier — код + edge cases)
│   └── EquipmentVisual_BUGS_TICKETS.md
│
├── input-system/                         (Input System: ребинд, фазы)
│   ├── 60_PHASE_1_5_SUMMARY.md
│   ├── 70_PHASE_2_SUMMARY.md
│   ├── 80_PHASE_3_SUMMARY.md
│   └── ITERATIONS.md
│
├── respawn/                              (система респавна)
│   ├── 01_ARCHITECTURE.md
│   ├── 02_USAGE.md
│   ├── 03_ARCHITECTURE_AUDIT.md
│   ├── 04_PLAYER_SHIP_PERSISTENCE_FINAL.md
│   ├── PLAYER_SHIP_POSITION_PERSISTENCE.md
│   ├── T-HP01-respawn-fix.md
│   └── ITERATIONS.md
│
├── Skills/                               (боевые навыки + real-time combat)
│   ├── AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md
│   ├── AUDIT_2026-07-24_ITEM_WEAPON_REFACTOR.md
│   ├── INP06_AOE_DEBUG_VISUALIZATION.md
│   ├── INP08_ANIMATOR_CLIP_PIPELINE.md
│   ├── SKILLS_NEXT_STEPS_T-CB_LOG.md
│   ├── ITERATIONS.md
│   ├── Battle/                           (боевые навыки: дизайн, skill tree, VFX)
│   └── real-time-combat/                 (real-time combat engine: MVP + NPC enemies)
│
└── ThirdpersonCamera/                    (камера от 3-го лица)
    └── CHANGELOG.md

Архив дизайн-фазы: docs/archive/Character_design_legacy/ (02-10, 13)
Архивы подсистем: Character_menu_design_legacy/, Customisation_design_legacy/,
                  EquipmentVisual_design_legacy/, Input_system_design_legacy/,
                  Knowledges_legacy/, ThirdpersonCamera_design_legacy/
```

---

## Status bar (actual — 2026-07-26)

**M1 (Stats core): ✅ DONE (T-P01..T-P06)** • **M2 (Clothing/Modules): ✅ DONE (T-P07..T-P10)** • **M3 (Skill tree): ✅ DONE (T-P11..T-P14)** • **M4 (UI integration): ✅ DONE (T-P15..T-P18)**

---

## Карта систем

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

## Архитектурные правила (актуально)

- ✅ **Additive-only подход** — Stats/Equipment/Skills не пересекаются с Level/XP.
- ✅ **Не создавать отдельных окон** (`CharacterMenuWindow`, `CharacterStatsWindow`, `SkillTreeWindow`) — всё внутри существующего `CharacterWindow` (архитектурное правило из `docs/Character-menu/00_OVERVIEW.md`).
- ✅ **WorldEventBus** — все события публикуются из существующих серверов в success-ветках (минимальное изменение).

---

## История создания

- Исходные анализы сабагентов (14.06.2026): RPG entry-points, Data-Model (SO), UI/Player-Controller.
- Аудит `11_STATS_ARCHITECTURE_AUDIT.md` (2026-07-26) — наиболее актуальный технический документ.

---

## Связанные документы проекта

- `docs/Character-menu/00_OVERVIEW.md` — план 5-табового P-окна
- `docs/Character-menu/10_DESIGN.md` — UXML/USS дизайн
- `docs/Character-menu/sub_inventory-tab/` — Inventory v2 референс
- `docs/Mining/ROADMAP.md` — канонический шаблон roadmap'а
- `docs/NPC_quests/02_V2_ARCHITECTURE.md` — канонический v2 hub-паттерн
- `docs/NPC_quests/08_ROADMAP.md` — пример roadmap с 22 тикетами

---

## С чего начать

- **`11_STATS_ARCHITECTURE_AUDIT.md`** — аудит архитектуры статов: 7 проблем + план рефакторинга (2026-07-26).
- **`12_STATS_ARCHITECTURE_AUDIT_V2.md`** — глубокий trace code STR/DEX/INT (2026-07-09).
- **`14_PLAYTESTS_STATS_AUDIT.md`** — playtest-гайд 10 исправлений.
- **`respawn/01_ARCHITECTURE.md`** — архитектура системы респавна.
- **`Skills/real-time-combat/00_README.md`** — real-time combat engine.
