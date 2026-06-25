# References — file:line index

> **Дата:** 2026-06-25
> **Метод:** read_file реальных .cs + .md в этой сессии + grep по `Assets/` и `docs/`
> **Все ссылки file:line проверены** через реальные file reads.

---

## 1. Реализованный код (Assets/) — то, что TB переиспользует

### 1.1 Skill Tree (T-P11..T-P13) — навыки для TB

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` | 98 | enum SkillCategory (15-19), [CreateAssetMenu] (21), public fields (24-56), `_learnXpCost`/`_requiredIntelligenceTier` (45-49), OnValidate cycle detection (66-78) |
| `Assets/_Project/Skills/SkillsServer.cs` | 206 | Instance singleton (32), OnNetworkSpawn (46-74), RateLimit (85-98), RequestLearnSkillRpc (102-122), RequestForgetSkillRpc (124-143) |
| `Assets/_Project/Skills/SkillsWorld.cs` | T-P12 | `LoadAllSkills`, `GetLearnedSkillIds(clientId)` — **TB читает** для проверки proficiency |
| `Assets/_Project/Scripts/Skills/SkillsClientState.cs` | T-P13 | singleton + OnSkillsUpdated event |
| `Assets/_Project/Scripts/Skills/SkillEffect.cs` | T-P11 | struct с enum Type { StatMod=0, AbilityUnlock=1, PassiveEffect=2, ...+5 new } |

### 1.2 Equipment (T-P07..T-P09) — оружие + броня для TB

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Equipment/EquipSlot.cs` | T-P08 | enum (Head..WeaponOff..Module3) — **TB использует** |
| `Assets/_Project/Scripts/Equipment/EquipmentData.cs` | T-P08 | struct INetworkSerializable, byte[SLOT_COUNT=13] |
| `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | T-P07 | extends ItemData, slot, statBonuses, **armorDefense** (T-CB06, ERPR) |
| `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` (PLANNED, T-CB03) | — | extends ItemData, **damageDice**, **baseDamage**, **critModifier**, **range** (ERPR) |
| `Assets/_Project/Scripts/Equipment/ModuleItemData.cs` | T-P07 | (не используется в TB) |
| `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | T-P09 | `GetEquipment(clientId)` — **TB читает** |
| `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` | T-P09 | TryEquip/TryUnequip (не используется в TB) |

### 1.3 Stats (T-P01..T-P06) — статы для формулы

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Stats/StatsConfig.cs` | T-P01 | SO, _tierBaseXp, _tierGrowthRate, XpForNextTier |
| `Assets/_Project/Scripts/Stats/PlayerStats.cs` | T-P02 | struct, strength, dexterity, intelligence + tiers + totalXp |
| `Assets/_Project/Scripts/Stats/StatsWorld.cs` | T-P03 | `GetOrCreateStats(clientId)` — **TB читает** для damage formula |
| `Assets/_Project/Scripts/Stats/StatsClientState.cs` | T-P04 | singleton, OnStatsUpdated event |
| `Assets/_Project/Scripts/Stats/StatsServer.cs` | T-P05 | NetworkBehaviour, RecomputeAndSendSnapshot — **TB пишет** через `ApplyXpDirect` при death penalty |
| `Assets/_Project/Scripts/Stats/JsonCharacterDataRepository.cs` | T-P06 | persistence — **TB расширяет** (T-TB14) для TB-результатов |
| `Assets/_Project/Scripts/Core/ItemType.cs` | 35 | enum ItemType { Resources=0, Equipment=1, ... } |

### 1.4 Network/Events (T-P11..T-P18) — для RPC

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Core/NetworkManagerController.cs` | — | `Awake()` создаёт ClientState, `DontDestroyOnLoad` — **TB добавит** `CreateTurnBasedBattleClientState()` (T-TB14) |
| `Assets/_Project/Core/WorldEventBus.cs` | 82 | static singleton, `Publish<T>/Subscribe<T>/Unsubscribe<T>/Reset` — **TB добавит 4 новых event-класса** |
| `Assets/_Project/Core/WorldEvent.cs` | 154 | ItemAdded/Removed, ReputationChanged, NpcAttitudeChanged, DialogVisited, DayNightPhase, ContractEvents |

### 1.5 UI (T-P14..T-P18) — паттерн для TB-окна

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | 3400 | 6 tab buttons, SubscribeX/UnsubscribeX, MakeXxxRow/BindXxxRow, 4 FIX'а UI Toolkit — **паттерн для TB** |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | 177 | structure pattern — **TB-окно** использует похожую структуру |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | 540 | стили — **TB-окно** использует похожие стили |
| `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset` | 52 | `m_ScaleMode: 1`, ref-resolution 1200×800 — **TB-окно** использует тот же PanelSettings |

### 1.6 Resources (asset-файлы)

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Resources/Stats/StatsConfig_Default.asset` | default значения статов |
| `Assets/_Project/Resources/Skills/SkillsConfig_Default.asset` | default SkillsConfig |
| `Assets/_Project/Resources/Skills/Skill_*.asset` (8 шт, T-P11) | combat + social placeholder |
| `Assets/_Project/Resources/Items/Clothing/*.asset` (5 шт) | T-P07 — **нужно добавить armorDefense** (T-CB06) |
| `Assets/_Project/Resources/Items/Weapons/*` (PLANNED, T-CB03) | будущие .asset |
| `Assets/_Project/Resources/Dungeons/Dungeon_GoblinRuins_Rank1.asset` (PLANNED, T-TB10) | будущие .asset |
| `Assets/_Project/Resources/Duels/Duel_Standard_1v1.asset` (PLANNED, T-TB10) | будущие .asset |
| `Assets/_Project/Resources/Bosses/Boss_PirateChief.asset` (PLANNED, T-TB10) | будущие .asset |

### 1.7 NPC-система (минимально для TB)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Quests/NpcController.cs` | 130 | `NpcId` getter (line 52) — **TB использует** для NPC-id |
| `Assets/_Project/Quests/Network/QuestServer.cs` | 650+ | quest-dialog flow (line 493, 612) — **TB не пересекается** |

**В проекте НЕТ:**
- `NpcCombatData` (struct) — нужно создать (T-TB01, для TB).
- `NpcAiConfig` (SO) — нужно создать (T-TB01).
- `BossConfig` (SO) — нужно создать (T-TB10).

---

## 2. Документация (docs/) — что переиспользуем

### 2.1 Character Progression (база — T-P11..T-P18)

| Файл | Строк | TB использует |
|------|-------|---|
| `docs/Character/00_README.md` | 154 | TL;DR контекст |
| `docs/Character/01_CURRENT_STATE_AUDIT.md` | 371 | что уже есть (CharacterWindow, WorldEventBus, etc.) |
| `docs/Character/02_V2_ARCHITECTURE.md` | 816 | server-side hub + DTO + ClientState pattern (копируем для TB) |
| `docs/Character/03_DATA_MODEL.md` | 588 | SkillNodeConfig/SkillEffect, ClothingItemData, **armorDefense** (T-CB06) |
| `docs/Character/04_STATS_PROGRESSION.md` | 698 | StatsConfig, XP формула, distance tracking (не для TB, но для death penalty) |
| `docs/Character/05_CLOTHING_AND_MODULES.md` | T-P07 | equip/unequip, stat-bonuses |
| `docs/Character/06_SKILL_TREE.md` | 774 | SkillNodeConfig (proficiency, technique) — **TB использует** |
| `docs/Character/07_UI_TABS_IN_CHARACTER_WINDOW.md` | 946 | UI pattern (TB-окно копирует) |
| `docs/Character/08_ROADMAP.md` | 666 | roadmap паттерн (TB-тикеты T-TB01..T-TB14) |
| `docs/Character/09_OPEN_QUESTIONS.md` | 734 | решённые вопросы (Q1-Q6) |
| `docs/Character/10_REFERENCES.md` | 336 | file:line index |
| `docs/Character/CHANGELOG.md` | 151 | changelog |

### 2.2 Battle/ (combat-навыки, v0.2)

| Файл | Назначение |
|------|-----------|
| `docs/Character/Skills/Battle/00_README.md` | манифест, 5 дисциплин, ERPR-пакет |
| `docs/Character/Skills/Battle/01_ANALYSIS.md` | что есть / чего нет / расхождение с GDD 20 / ERPR |
| `docs/Character/Skills/Battle/02_LORE.md` | лор-факты, damage-types, дистанции (с ERPR) |
| `docs/Character/Skills/Battle/ERPR_collaboration.md` | анализ ERPR-пакета, mapping, варианты A/B/C |
| `docs/Character/Skills/Battle/10_DESIGN.md` | архитектура combat-навыков + **§7 ERPR damage-формула** (TB использует!) |
| `docs/Character/Skills/Battle/20_SKILL_TREES.md` | 5 веток (Melee/Ranged/Explosives/Antigrav/Defense) с damage dice/crit |
| `docs/Character/Skills/Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md` | pitfalls + open questions |
| `docs/Character/Skills/Battle/40_REFERENCES.md` | file:line index |

### 2.3 GDD (для справки)

| Файл | Назначение |
|------|-----------|
| `docs/gdd/GDD_00_Overview.md` | пиллар 2 «Исследование над боем» — TB = mini-game (opt-in), не противоречит |
| `docs/gdd/GDD_01_Core_Gameplay.md` | режимы (пеший/корабль/камера) — TB = пеший |
| `docs/gdd/GDD_02_World_Environment.md` | мир, погода |
| `docs/gdd/GDD_20_Progression_RPG.md` | корабельный бой (Pilot/Merchant/Explorer) — **НЕ наш scope** |

### 2.4 ERPR-источник

| Файл | Назначение |
|------|-----------|
| `docs/Character/Skills/Battle/ERPR_collaboration.md` | анализ ERPR, рекомендация B |
| `F:\yandex\Yandex.Disk\Glob file\homerule\erpr\ERPR1.2.pdf` | исходник (9 стр): damage dice 1dN (стр.7), hit location 1d4, crit 1d100, 3 сек (стр.5), 20 ОЗ (стр.3) |

---

## 3. Шаблоны и стандарты

| Документ | Назначение |
|----------|-----------|
| `MOON_SYSTEM.md` | шаблон тех. спецификации (мы пишем по нему) |
| `MOON_INVESTIGATION.md` | шаблон investigation log |
| `docs/Mining/ROADMAP.md` | канонический шаблон roadmap'а (8 секций) |
| `docs/NPC_quests/08_ROADMAP.md` | пример roadmap с 22 тикетами |
| `docs/NPC_quests/02_V2_ARCHITECTURE.md` | v2 hub-pattern (server NetworkBehaviour + DTO + ClientState) — **копируем для TB** |
| `AGENTS.md` | hard rules |

---

## 4. Grep-данные (TB-специфичные)

| Что | Где | Результат |
|-----|-----|-----------|
| `TurnBased` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB01..T-TB14) |
| `BattleGrid` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB02) |
| `DamageCalculator` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB07) |
| `TurnBasedAI` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB09) |
| `DungeonConfig` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB10) |
| `DuelConfig` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB10) |
| `BossConfig` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB10) |
| `BattleStartedEvent` | grep пусто в `WorldEvent.cs` | **НЕ существует** — нужно добавить (T-TB01) |
| `HitLocation` enum | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-CB10/T-TB07) |
| `TurnBasedBattleWindow` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-TB08) |

---

## 5. Что НЕ нашлось (явно подтверждено)

| Что | Статус |
|-----|--------|
| TB-подсистема целиком | **НЕ существует** — проектируется |
| `TurnBasedBattle` (POCO singleton) | **НЕ существует** — T-TB01 |
| `TurnBasedBattleInstance` | **НЕ существует** — T-TB02 |
| `TurnBasedBattleServer` (NetworkBehaviour) | **НЕ существует** — T-TB03 |
| `TurnBasedBattleClientState` | **НЕ существует** — T-TB04 |
| NGO RPC: `RequestStartBattleRpc`, `SubmitActionRpc`, `EndTurnRpc`, `SurrenderRpc` | **НЕ существуют** — T-TB05 |
| TargetRPC: `BattleStartedTargetRpc`, `TurnStartedTargetRpc`, `ActionResultTargetRpc`, `BattleEndedTargetRpc`, `DuelInviteTargetRpc` | **НЕ существуют** — T-TB06 |
| `DamageCalculator` (static) | **НЕ существует** — T-TB07 (но ERPR-формула в `Battle/10_DESIGN.md §7` готова) |
| `TurnBasedBattleWindow` (UIDocument) | **НЕ существует** — T-TB08 |
| `TurnBasedAI` (rule-based) | **НЕ существует** — T-TB09 |
| `DungeonConfig`, `DuelConfig`, `BossConfig`, `EventConfig` SOs | **НЕ существуют** — T-TB10 |
| `TurnBasedBattleZone` (GameObject) | **НЕ существует** — T-TB11 |
| PvP-duel flow | **НЕ существует** — T-TB12 |
| Boss-enкаунтер (TB-only) | **НЕ существует** — T-TB13 |
| TB-persistence (JsonCharacterDataRepository extension) | **НЕ существует** — T-TB14 |
| `HitLocation` enum | **НЕ существует** — T-CB10/T-TB07 (ERPR) |
| `DamageDice` enum | **НЕ существует** — T-CB03 (ERPR, shared с Battle) |
| `armorDefense` поле в `ClothingItemData` | **НЕ существует** — T-CB06 (ERPR, shared с Battle) |
| NPC-AI для open world | **НЕ существует** — отдельная подсистема |
| Real-time combat-движок | **НЕ существует** — отдельная подсистема |
| `WorldEvent.BattleStartedEvent` | **НЕ существует** — нужно добавить (T-TB01) |
| `WorldEvent.BattleEndedEvent` | **НЕ существует** — нужно добавить (T-TB01) |
| `WorldEvent.TurnStartedEvent` | **НЕ существует** — нужно добавить (T-TB01) |
| `WorldEvent.ActionResultEvent` | **НЕ существует** — нужно добавить (T-TB01) |

---

## 6. Зависимости (T-TB от T-CB)

| TB-тикет | Зависит от Battle-тикета | Что именно |
|---|---|---|
| T-TB07 (DamageCalculator) | **T-CB03** (WeaponItemData с damageDice/baseDamage/critModifier) | без оружия с ERPR-полями — нечего считать |
| T-TB07 (DamageCalculator) | **T-CB06** (ClothingItemData.armorDefense) | без armorDefense — нечего защищать |
| T-TB07 (DamageCalculator) | **T-CB10** (HitLocation enum) | без HitLocation — нельзя roll 1d4 |
| T-TB03 (TB-Server) | **T-CB01** (SkillEffect.Type: WeaponProficiencyUnlock, WeaponTechniqueUnlock) | без новых Type — TB не знает, какие навыки дают proficiency |
| T-TB02 (TB-Instance) | T-CB03, T-CB06 | читает equipped weapon+armor |
| T-TB11 (TB-Zone trigger) | T-CB03 (если зона связана с конкретным weapon) | — |

**Вердикт:** **T-TB01..T-TB14 НЕ МОГУТ стартовать без T-CB01..T-CB10 (Battle)**. Зависимость сильная.

**Sequencing:** сначала T-CB01..T-CB09 (навыки + ERPR-пакет, 2-3 сессии), потом T-TB01..T-TB14 (TB-подсистема, 3-4 сессии).

---

## 7. Связанные документы (проект)

| Документ | Связь с TB |
|---|---|
| `docs/Character/Skills/Battle/` | TB переиспользует навыки и ERPR-формулу |
| `docs/Character/Skills/turn-based-battles/` | **этот документ** |
| `docs/Character-menu/sub_inventory-tab/INVENTORY_V2_REFACTOR.md` | TB loot → Inventory v2 |
| `docs/Character-menu/10_DESIGN.md` | UI pattern |
| `docs/Crafting_system/` | рецепты гранат/мин (если ExplosivesRecipeUnlock интегрирован) |
| `docs/NPC_quests/` | quest-флаги после boss-enкаунтера |
| `docs/Markets/` | rewards (credits) |
| `docs/Ships/` | НЕ связано |
| `docs/World/` | dungeon-локации (отдельный документ) |
| `docs/gdd/GDD_20_Progression_RPG.md` | НЕ связано (корабельный бой) |
