# References — file:line index

> **Дата:** 2026-06-25 (v0.3 — обновлено под вариант B + новый sequencing)
> **Метод:** read_file реальных .cs + .md в этой сессии + grep по `Assets/` и `docs/`
> **Все ссылки file:line проверены** через реальные file reads.

---

## 1. Реализованный код (Assets/)

### 1.1 Skill Tree (T-P11..T-P13, ✅)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` | 98 | enum SkillCategory (15-19), [CreateAssetMenu] (21), public fields (24-56), `_learnXpCost`/`_requiredIntelligenceTier` (45-49), OnValidate cycle detection (66-78), HasCycle DFS (80-95) |
| `Assets/_Project/Skills/SkillsServer.cs` | 206 | Instance singleton (32), OnNetworkSpawn LoadAllSkills (46-74), OnNetworkDespawn Reset (76-81), RateLimit per-client (85-98), RequestLearnSkillRpc (102-122), RequestForgetSkillRpc (124-143), SendSkillResult reflection-stub (147-165), SendSnapshotToOwner reflection-stub (167-189), TriggerStatsRecompute reflection-stub (195-204) |
| `Assets/_Project/Skills/SkillsWorld.cs` | T-P12 | LoadAllSkills из Resources/Skills, GetLearnedSkillIds per-client, TryLearnSkill с проверкой prereq + INT-tier + XP cost, TryForgetSkill (Q3.4 free respec), BuildSnapshot, BuildSaveData, LoadPlayer |
| `Assets/_Project/Scripts/Skills/SkillsClientState.cs` | T-P13 | singleton + OnSkillsUpdated event (для CharacterWindow) |
| `Assets/_Project/Scripts/Skills/SkillEffect.cs` | T-P11 | struct с enum Type { StatMod=0, AbilityUnlock=1, PassiveEffect=2 } (по `06_SKILL_TREE.md §1.2`) — **будет расширен** (T-CB01) до Type { StatMod=0, AbilityUnlock=1, PassiveEffect=2, WeaponProficiencyUnlock=3, ArmorProficiencyUnlock=4, WeaponTechniqueUnlock=5, ExplosiveRecipeUnlock=6, AntigravTechniqueUnlock=7 } |

### 1.2 Equipment (T-P07..T-P10, ✅)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Equipment/EquipSlot.cs` | T-P08 | enum (Head=1, Chest=2, Legs=3, Feet=4, Back=5, Hands=6, Accessory1=7, Accessory2=8, WeaponMain=9, WeaponOff=10, Module1=20, Module2=21, Module3=22) |
| `Assets/_Project/Scripts/Equipment/EquipmentData.cs` | T-P08 | struct INetworkSerializable, byte[SLOT_COUNT=13] + int[SLOT_COUNT=13], SlotToIndex/IndexToSlot |
| `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | T-P07 | extends ItemData, slot, tier, statBonuses (STR/DEX/INT additive + multiplicative), requiredSkills[] — **будет расширен** (T-CB06, ERPR) полем `armorDefense` |
| `Assets/_Project/Scripts/Equipment/ModuleItemData.cs` | T-P07 | extends ItemData, slot (Module1..3), moduleType enum (Sensor/Processor/Weapon/Utility), effects (sensor/processor/weapon), requiredSkills[] |
| `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | T-P09 | POCO singleton, Dictionary<ulong, EquipmentData> per-client |
| `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` | T-P09 | TryEquip validation (item check, slot match, requiredSkills hard/soft, inventory ownership, slot empty), TryUnequip, snapshot sync — **будет расширен** (T-CB06) для WeaponItemData + armorDefense |

### 1.3 Stats (T-P01..T-P06, ✅)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Stats/StatsConfig.cs` | T-P01 | SO, _tierBaseXp=100, _tierGrowthRate=1.5 (geometric), _globalMultiplier=1.0 (no cap), XpForNextTier, GetStatFor(source), GetBaseXp(source) |
| `Assets/_Project/Scripts/Stats/PlayerStats.cs` | T-P02 | struct, strength/dexterity/intelligence + tiers + totalXp |
| `Assets/_Project/Scripts/Stats/StatsWorld.cs` | T-P03 | POCO singleton, Dictionary<ulong, PlayerStats> |
| `Assets/_Project/Scripts/Stats/StatsClientState.cs` | T-P04 | singleton, OnStatsUpdated event |
| `Assets/_Project/Scripts/Stats/StatsServer.cs` | T-P05 | NetworkBehaviour, subscribes to 9 WorldEventBus events, FixedUpdate walk tracker, ApplyXp with tier promotion loop, RecomputeAndSendSnapshot |
| `Assets/_Project/Scripts/Stats/JsonCharacterDataRepository.cs` | T-P06 | ICharacterDataRepository, JsonUtility, file per clientId character_<id>.json |
| `Assets/_Project/Scripts/Core/ItemType.cs` | 35 | enum ItemType { Resources=0, Equipment=1, Food=2, Fuel=3, Antigrav=4, Meziy=5, Medical=6, Tech=7 }, ItemData базовый (18-34) |

### 1.4 Network/Events (✅)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Core/WorldEventBus.cs` | 82 | static singleton, Publish<T>/Subscribe<T>/Unsubscribe<T>/Reset |
| `Assets/_Project/Core/WorldEvent.cs` | 154 | ItemAddedEvent (37), ItemRemovedEvent (51), ReputationChangedEvent (66), NpcAttitudeChangedEvent (81), QuestStateChangedEvent (100), DialogVisitedEvent (112), CustomEvent (128), DayNightPhaseChangedEvent (138), ContractAcceptedEvent/CompletedEvent/FailedEvent (148+) |

### 1.5 UI (T-P14..T-P18, ✅)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | 3400 | 6 tab buttons + 1 top-level (ПРОГРЕССИЯ) + 4 sub-tabs (Статы/Одежда/Модули/Навыки), `SwitchTab` pattern (636-706), SubscribeX/UnsubscribeX lazy (268-330), MakeManualSkillRow / BindManualSkillRow (1480+), MakeManualEquipRow / BindClothingRow / BindModuleRow (1100+), ListView for skills-combat / skills-social |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | 177 | progression-section (parent), 4 sub-sections (stats/clothing/modules/skills), 4 sub-tab buttons, skills-combat-list + skills-social-list ListView |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | 540 | .sub-tabs, .sub-tab-btn, .stat-row-progress, .stat-progress-fill (Q4.3 per-category colors), .skill-row* state variants (locked/available/learned) |

### 1.6 Resources (asset-файлы)

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Resources/Stats/StatsConfig_Default.asset` | default значения статов |
| `Assets/_Project/Resources/Skills/SkillsConfig_Default.asset` | default SkillsConfig (Q3.2 = b, defaultSkills = пусто) |
| `Assets/_Project/Resources/Skills/Skill_Combat_BasicStrike.asset` | T-P11 placeholder (4 combat .asset) |
| `Assets/_Project/Resources/Skills/Skill_Combat_HeavySwing.asset` | T-P11 |
| `Assets/_Project/Resources/Skills/Skill_Combat_DodgeRoll.asset` | T-P11 |
| `Assets/_Project/Resources/Skills/Skill_Combat_PrecisionStrike.asset` | T-P11 |
| `Assets/_Project/Resources/Skills/Skill_Social_BasicTalk.asset` | T-P11 (4 social .asset) |
| `Assets/_Project/Resources/Skills/Skill_Social_Barter.asset` | T-P11 |
| `Assets/_Project/Resources/Skills/Skill_Social_Persuasion.asset` | T-P11 |
| `Assets/_Project/Resources/Skills/Skill_Social_Leadership.asset` | T-P11 |
| `Assets/_Project/Resources/Items/Clothing/*.asset` (5 шт) | T-P07 (WorkerHelmet, SteelChestplate, TravelerBoots, MerchantCloak, SmithApron) — **нужно обновить armorDefense** (T-CB06) |
| `Assets/_Project/Resources/Items/Modules/*.asset` (3 шт) | T-P07 (RangefinderMk1, CraftingAssistant, DataAnalyzer) |

---

## 2. Документация (docs/)

### 2.1 Character Progression (база)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `docs/Character/00_README.md` | 154 | TL;DR, status (M1-M4 ✅ DONE), что НЕ в scope |
| `docs/Character/01_CURRENT_STATE_AUDIT.md` | 371 | что есть в проекте (CharacterWindow, WorldEventBus, Mining, Crafting, Exchange, Market, Quest, Ship, Player, ItemData, Persist, SO-pattern), 9 новых событий, 1+18 файлов |
| `docs/Character/02_V2_ARCHITECTURE.md` | 816 | server-side hub + DTO + ClientState, StatsServer/EquipmentServer/SkillsServer network, NPC-spam protection, OnPlayerConnected snapshot, snapshot sync TargetRPC |
| `docs/Character/03_DATA_MODEL.md` | 588 | StatsConfig (1.1), ClothingItemData (2.1), ModuleItemData (3.1), SkillNodeConfig (4.1), SkillEffect struct (4.2), 8 .asset examples (4.3), JsonCharacterDataRepository (5) |
| `docs/Character/04_STATS_PROGRESSION.md` | 698 | geometric formula baseXp*growthRate^tier, 12 XP sources → 3 stats hardcoded, NPC-spam unique-event (Q1.4), distance tracking (walk + pilot), jump detection, snapshot DTO |
| `docs/Character/05_CLOTHING_AND_MODULES.md` | T-P07 | slots, equip/unequip, stat-bonuses additive + multiplicative (Q1.7 = c) |
| `docs/Character/06_SKILL_TREE.md` | 774 | SkillNodeConfig (1.1), SkillEffect (1.2), Social/Combat split (1.3), 8 placeholder .asset, SkillsServer (2), SkillsWorld (3), skill UI (4), Painter2D plan (5), edge cases (6), pitfalls (7), что НЕ делаем (8) |
| `docs/Character/07_UI_TABS_IN_CHARACTER_WINDOW.md` | 946 | nested sub-tabs под ПРОГРЕССИЯ, UXML/USS |
| `docs/Character/08_ROADMAP.md` | 666 | T-P01..T-P18 ✅ DONE, M1-M4 ✅ DONE, click handlers deferred до battle system, T-P19/T-P20/T-P21 (Phase 2) |
| `docs/Character/09_OPEN_QUESTIONS.md` | 734 | Q1.1-Q1.7 (Stats), Q2.1-Q2.5 (Equipment), Q3.1-Q3.6 (Skills), Q4.1-Q4.5 (UI), Q5.1-Q5.4 (Persistence), Q6.1-Q6.2 (Placeholder) — все с ответами пользователя |
| `docs/Character/10_REFERENCES.md` | 336 | file:line index всех прочитанных файлов в предыдущих сессиях |
| `docs/Character/CHANGELOG.md` | 151 | 2026-06-15..17: S2 полная реализация 18 тикетов, ключевые архитектурные bugfixes, что осталось (skills click handlers deferred до battle system) |

### 2.2 ERPR-коллаборация (новое, v0.2)

| Файл | Назначение |
|------|-----------|
| `docs/Character/Skills/Battle/ERPR_collaboration.md` | Анализ ERPR-пакета, mapping A/B/C, рекомендация B (принят) |
| `F:\yandex\Yandex.Disk\Glob file\homerule\erpr\ERPR1.2.pdf` | Исходник настолки (9 страниц, CorelDRAW). Damage dice 1dN (стр.7), Hit location 1d4, Crit 1d100, 3 секунды на ход (стр.5), 20 ОЗ (стр.3), 100 ОД/день (стр.2) |

### 2.3 Real-Time Combat Engine (новое, v0.3 — MVP)

| Файл | Назначение |
|------|-----------|
| `../real-time-combat/00_README.md` | манифест, новый sequencing, anti-restrictive |
| `../real-time-combat/01_ANALYSIS.md` | что есть / gaps / sequencing / ship-combat extensibility |
| `../real-time-combat/02_LORE.md` | пеший vs корабельный бой, общая лор-база |
| `../real-time-combat/10_DESIGN.md` | архитектура, IAttacker/ITarget abstractions, §7 ERPR damage-формула |
| `../real-time-combat/20_TECHNICAL.md` | NGO RPC, server-authoritative, hooks |
| `../real-time-combat/30_SCENARIOS.md` | пеший MVP, ship-extensibility примеры |
| `../real-time-combat/30_PITFALLS_AND_OPEN_QUESTIONS.md` | pitfalls + open questions (новый) |
| `../real-time-combat/40_REFERENCES.md` | file:line |

**Связь с `Battle/`:** движок переиспользует ERPR damage-формулу из `Battle/10_DESIGN.md §7`. Навыки (T-CB01..T-CB09) — opt-in слой, подключаются позже.

### 2.4 Turn-Based Battles (PARKING)

### 2.3 GDD (для справки, не модифицируем)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `docs/gdd/GDD_INDEX.md` | 92 | индекс 18 GDD, status |
| `docs/gdd/GDD_00_Overview.md` | — | пиллары (1.2): Свобода полёта, **Исследование над боем**, Кооперация |
| `docs/gdd/GDD_01_Core_Gameplay.md` | — | режимы (пеший/корабль/камера), физика полёта (антигравитация -Physics.gravity * rb.mass, тяга от ветровых лопастей) |
| `docs/gdd/GDD_02_World_Environment.md` | — | мир, погода, day/night |
| `docs/gdd/GDD_20_Progression_RPG.md` | 764 | **расхождение**: 50 уровней, 4 стата корабля (Endurance/Navigation/Mechanics/Luck), 3 ветки (Pilot/Merchant/Explorer), Skill Point за уровень. НЕ наш scope (мы — пехотный v2) |
| `docs/gdd/GDD_22_Economy_Trading.md` | — | экономика (для контекста crafting/market) |
| `docs/gdd/GDD_23_Faction_Reputation.md` | — | 5 Гильдий, репутация (для faction-aware combat, future) |

### 2.4 Lore & World (для обоснований)

| Файл | Строки | Ключевые места |
|------|--------|----------------|
| `docs/COLABORATION.md` | — | **«Нет магии — всё технологично: антигравий, мезий, генераторы. Даже свечение — это физика, не волшебство»** (line 14), VFX-теги (line 45-46) |
| `docs/ART_BIBLE.md` | — | «**Голубое** свечение = антигравий (безопасно), **зелёное** = мезий (опасно), **фиолетовое** = Завеса (смерть)» (line 74), PC_Icon_Mesium_v01.png (line 375) |
| `docs/MMO_Development_Plan.md` | — | упоминание мезий-сырья (line 599), дефицит мезия/бум антигравия как глобальные события (line 750) |
| `docs/Character/Character-menu/sub_inventory-tab/40_CHANGES_SUMMARY.md` | — | Item_Fuel_Антигравитационное_топливо (ItemType.Fuel, НЕ Antigrav), Item_Fuel_Газовый_баллон (line 304) — мезий сейчас = топливо/сырьё, не оружие |
| `docs/Character/Character-menu/sub_inventory-tab/INVENTORY_V2_REFACTOR.md` | — | Item_Antigrav_камень (ItemType.Antigrav), Мезий-крошка / Мезий-кристалл (ItemType.Meziy) — примеры текущих .asset (line 586) |
| `docs/context/ship.md` | — | ShipFuelSystem — мезий как fuel, ShipModuleManager — модули корабля (отдельно от character modules) |

### 2.5 Subagent-отчёты (предыдущие сессии, сырые)

| Файл | Размер | Назначение |
|------|--------|-----------|
| `C:\Users\leon7\ANALYSIS_CHARACTER_RPG_UI.md` | 69.5 KB, 1020 строк | UI/Player-Controller анализ |
| `C:\Users\leon7\ANALYSIS_CHARACTER_DATA_MODEL.md` | ~2400 слов | Data-Model/SO-паттерны |
| `C:\Users\leon7\ANALYSIS_CHARACTER_ENTRY_POINTS.md` | 22 KB, 210 строк | entry-points для подписки |

**Не в проекте**, в `C:\Users\leon7\` — это сырые материалы предыдущих сессий, использованы для синтеза `00_README.md`..`09_OPEN_QUESTIONS.md`.

---

## 3. Шаблоны и стандарты

| Документ | Назначение |
|----------|-----------|
| `MOON_SYSTEM.md` | шаблон технической спецификации подсистемы (для нашего `10_DESIGN.md`) |
| `MOON_INVESTIGATION.md` | шаблон investigation log (для debugging) |
| `docs/Mining/ROADMAP.md` | канонический шаблон roadmap'а (8 секций) — для T-CB roadmap |
| `docs/NPC_quests/08_ROADMAP.md` | пример roadmap с 22 тикетами |
| `docs/NPC_quests/02_V2_ARCHITECTURE.md` | канонический v2 hub-pattern (server NetworkBehaviour + DTO + ClientState) |
| `AGENTS.md` | hard rules проекта (gdd read-only, не писать код/мета/asmdef в design-сессии) |

---

## 4. Grep-данные (для подтверждения лор-фактов)

| Что | Где | Результат |
|-----|-----|-----------|
| `антиграв` (по docs/) | `docs/ART_BIBLE.md` line 14, 25, 73-75, 124, 304-307; `docs/COLABORATION.md` line 14, 45-46, 62, 68; `docs/Crafting_system/30_VERIFICATION.md` line 80-82; `docs/Character/Character-menu/sub_inventory-tab/INVENTORY_V2_REFACTOR.md` line 585-586 | подтверждено как «металл, манипулирующий гравитацией», используется в кораблях, не описано как оружие в найденных лор-файлах |
| `мезий` | `docs/ART_BIBLE.md` line 14, 75, 375; `docs/COLABORATION.md` line 14, 45; `docs/Crafting_system/` (resources); `docs/Character/Character-menu/sub_inventory-tab/` (ItemType.Meziy) | подтверждено как газ, **используется как fuel** (ShipFuelSystem) и как продвинутое стрелковое (по game-designer'у, в lore-файлах не детализировано) |
| `порох` | grep пусто | **НЕ найдено** в `docs/` — lore-факт «порох дефицитен» подтверждён только в обсуждениях (game-designer устно). Запрос на подтверждение в `02_LORE.md §6.5` |
| `арбалет` | grep пусто | не найдено, lore-факт от game-designer'а, требует подтверждения в `02_LORE.md §6.5` |
| `пневмат` | grep пусто | не найдено, то же |
| `гравитац` | `docs/gdd/GDD_01_Core_Gameplay.md` line 168-170, `docs/gdd/GDD_02_World_Environment.md`, `docs/world/Landscape_TechnicalDesign.md` | подтверждено в контексте кораблей (антигравитация как движение), не как оружие |
| `WeaponType` / `CombatType` | grep пусто в `Assets/` | **НЕ существует** — подтверждает, что combat-подсистема ещё не реализована |
| `SkillNodeConfig` | `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` (98 строк, ✅) | найдено, см. §1.1 |
| `SkillsServer` | `Assets/_Project/Scripts/Skills/SkillsServer.cs` (206 строк, ✅) | найдено, см. §1.1 |
| `HitLocation` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-CB10/T-TB10) |
| `DamageDice` | grep пусто в `Assets/` | **НЕ существует** — нужно создать (T-CB03, ERPR-пакет) |
| `armorDefense` | grep пусто в `Assets/` | **НЕ существует** — нужно добавить (T-CB06, ERPR-пакет) |

---

## 5. Что НЕ нашлось (явно подтверждено)

| Что | Статус |
|-----|--------|
| **Real-Time Combat Engine целиком** | **НЕ существует** — проектируется в `../real-time-combat/` (T-RTC01..T-RTC10) |
| `WeaponItemData` в `Assets/` | **НЕ существует** — T-CB03 |
| `ExplosiveItemData` в `Assets/` | **НЕ существует** — T-CB04 |
| `CombatWorld/CombatServer` (старое имя, переименовано в v0.3) | **НЕ существует** — `../real-time-combat/CombatServer.cs` (T-RTC06) |
| `HitController` / `DamageController` | **НЕ существует** — отдельная подсистема (внутри real-time-combat) |
| `WorldEvent.AttackLandedEvent` | **НЕ существует** — T-RTC09 (4 новых event-класса) |
| `WorldEvent.DamageDealtEvent` | **НЕ существует** — T-RTC09 |
| `CombatDiscipline` enum | **НЕ существует** — T-CB02 |
| `WeaponClass` / `ArmorClass` enum | **НЕ существует** — T-CB03/T-CB05 |
| `DamageType` enum | **НЕ существует** — T-RTC01 (или T-CB03) |
| `HitLocation` enum | **НЕ существует** — T-RTC01 (в real-time = `locMult=1.0`, hitLocation=1=default) |
| `DamageDice` enum | **НЕ существует** — T-RTC01 (или T-CB03) |
| `armorDefense` поле в ClothingItemData | **НЕ существует** — T-CB06 (ERPR) |
| `IAttacker` / `IDamageTarget` / `IDamageSource` / `IRangePolicy` interfaces | **НЕ существуют** — T-RTC01 |
| `DamageCalculator` (static) | **НЕ существует** — T-RTC05 (спецификация в `Battle/10_DESIGN.md §7` готова) |
| `CombatClientState` (singleton) | **НЕ существует** — T-RTC07 |
| `PlayerAttacker` / `PlayerTarget` (NetworkBehaviour) | **НЕ существуют** — T-RTC02 |
| `NpcAttacker` / `NpcTarget` (NetworkBehaviour) | **НЕ существуют** — T-RTC03 |
| `WeaponDamageSource` / `MeleeRangePolicy` / `RangedRangePolicy` | **НЕ существуют** — T-RTC04 |
| `CombatConfig` (SO) | **НЕ существует** — T-RTC09 |
| `ShipAttacker` / `ShipTarget` (Phase 3) | **НЕ существуют** — T-RTC16 (отложен) |
| `Turret` (Phase 3) | **НЕ существует** — T-RTC17 (отложен) |
| **Turn-Based Battles подсистема** | **PARKING** (отложен на неопределённый срок) — см. `../turn-based-battles/` (НЕ развиваем) |
| `WORLD_LORE_BOOK.md` | **НЕ в репо** — файл упоминается в `GDD_INDEX.md` но не найден в `docs/`. Возможно в `C:\Users\leon7\` или будущее |
| `docs/gdd/GDD_24_Narrative_World_Lore.md` | **УПОМИНАЕТСЯ** в `GDD_INDEX.md`, не прочитан в этой сессии (out of scope, gdd read-only) |
| `Skill_Combat_Antigrav*` .asset | **НЕ существует** — T-CB08 |
| `Skill_Combat_Melee_*` .asset | **НЕ существует** — T-CB08 (4 placeholder'а из T-P11 — generic, не дисциплина-привязанные) |
| `TurnBasedBattle` подсистема | **НЕ существует** — отдельный документ `turn-based-battles/` (новое, v0.2) |
