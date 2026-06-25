# References — file:line index

> **Дата:** 2026-06-25 (v0.3 — после ответов пользователя, новый sequencing)
> **Метод:** read_file реальных .cs + .md + grep по `Assets/` и `docs/`
> **Все ссылки file:line проверены** через реальные file reads.

---

## 1. Реализованный код (Assets/) — что Combat-движок переиспользует

### 1.1 Skill tree (T-P11..T-P13) — навыки как **opt-in**

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` | 98 | enum SkillCategory (15-19), [CreateAssetMenu] (21), public fields (24-56), `_learnXpCost`/`_requiredIntelligenceTier` (45-49), OnValidate cycle detection (66-78) |
| `Assets/_Project/Skills/SkillsServer.cs` | 206 | Instance singleton (32), OnNetworkSpawn (46-74), RateLimit (85-98), RequestLearnSkillRpc (102-122), RequestForgetSkillRpc (124-143) |
| `Assets/_Project/Skills/SkillsWorld.cs` | T-P12 | `LoadAllSkills`, `GetLearnedSkillIds(clientId)` — **Combat-движок читает** (MVP+1) |
| `Assets/_Project/Scripts/Skills/SkillsClientState.cs` | T-P13 | singleton + OnSkillsUpdated event |
| `Assets/_Project/Scripts/Skills/SkillEffect.cs` | T-P11 | struct с enum Type { StatMod=0, AbilityUnlock=1, PassiveEffect=2, +5 new } |

### 1.2 Equipment (T-P07..T-P09) — оружие как `IDamageSource`

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Equipment/EquipSlot.cs` | T-P08 | enum (Head..WeaponOff..Module3) — **Combat-движок читает** `WeaponMain/Off` |
| `Assets/_Project/Scripts/Equipment/EquipmentData.cs` | T-P08 | struct INetworkSerializable, byte[SLOT_COUNT=13] + int[SLOT_COUNT=13] |
| `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | T-P07 | extends ItemData, slot, statBonuses, **armorDefense** (после T-CB06) |
| `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` (PLANNED, T-CB03) | — | extends ItemData, **damageDice**, **baseDamage**, **critModifier**, **range**, **damageType** (ERPR) |
| `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | T-P09 | `GetEquipment(clientId)` — **Combat-движок читает** |
| `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` | T-P09 | TryEquip/TryUnequip (не используется в Combat) |

### 1.3 Stats (T-P01..T-P06) — STR/DEX/INT модификаторы

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Stats/StatsConfig.cs` | T-P01 | SO, _tierBaseXp, _tierGrowthRate, XpForNextTier |
| `Assets/_Project/Scripts/Stats/PlayerStats.cs` | T-P02 | struct, strength, dexterity, intelligence + tiers + totalXp |
| `Assets/_Project/Scripts/Stats/StatsWorld.cs` | T-P03 | `GetOrCreateStats(clientId)` — **Combat-движок читает** |
| `Assets/_Project/Scripts/Stats/StatsClientState.cs` | T-P04 | singleton, OnStatsUpdated event |
| `Assets/_Project/Scripts/Stats/StatsServer.cs` | T-P05 | NetworkBehaviour, RecomputeAndSendSnapshot — **Combat-движок может подписаться** |
| `Assets/_Project/Scripts/Stats/JsonCharacterDataRepository.cs` | T-P06 | persistence — Combat может расширить (T-RTC10) |

### 1.4 Network/Events (T-P11..T-P18) — для RPC и шины

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | — | `Awake()` создаёт ClientState, `DontDestroyOnLoad` — **Combat добавит** `CreateCombatClientState()` (T-RTC07) |
| `Assets/_Project/Core/WorldEventBus.cs` | 82 | static singleton, `Publish<T>/Subscribe<T>/Unsubscribe<T>/Reset` — **Combat добавит 4 new event-класса** (T-RTC09) |
| `Assets/_Project/Core/WorldEvent.cs` | 154 | ItemAdded/Removed, ReputationChanged, NpcAttitudeChanged, DialogVisited, DayNightPhase, ContractEvents |

### 1.5 PlayerController / NetworkPlayer — точка регистрации

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 1270 | OnNetworkSpawn (line 188), input handling (line 396-401), `OnNetworkDespawn` (для cleanup) — **Combat-движок добавит регистрацию IAttacker/IDamageTarget** |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | 128 | input events — **Combat-движок может подписаться** (Phase 2, client prediction) |

### 1.6 ShipController (Phase 3, FUTURE)

| Файл | Строк | Ключевые места |
|------|-------|----------------|
| `Assets/_Project/Scripts/Player/ShipController.cs` | 939 | `_rb` (Rigidbody), `_pilots` (HashSet), FixedUpdate (line 352-355, 442-460), AddPilotRpc/RemovePilotRpc (line 921, 939) — **FUTURE: ShipAttacker/ShipTarget используют `_rb.position`** |
| `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | — | мирный NPC-корабль — **FUTURE: NpcShipAttacker** для враждебных |

### 1.7 Resources (asset-файлы)

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Resources/Stats/StatsConfig_Default.asset` | default значения статов |
| `Assets/_Project/Resources/Skills/SkillsConfig_Default.asset` | default SkillsConfig |
| `Assets/_Project/Resources/Skills/Skill_*.asset` (8 шт, T-P11) | combat + social placeholder |
| `Assets/_Project/Resources/Items/Clothing/*.asset` (5 шт) | T-P07 — **нужно добавить armorDefense** (T-CB06) |
| `Assets/_Project/Resources/Items/Weapons/*` (PLANNED, T-CB03) | будущие .asset |
| `Assets/_Project/Resources/Combat/CombatConfig_Default.asset` (PLANNED, T-RTC09) | будущие .asset |

---

## 2. Документация (docs/) — что переиспользуем

### 2.1 Character Progression (база — T-P11..T-P18)

| Файл | Назначение |
|------|-----------|
| `docs/Character/00_README.md` | TL;DR |
| `docs/Character/01_CURRENT_STATE_AUDIT.md` | что есть (CharacterWindow, WorldEventBus, etc.) |
| `docs/Character/02_V2_ARCHITECTURE.md` | server-side hub + DTO + ClientState pattern — **Combat-движок копирует** |
| `docs/Character/03_DATA_MODEL.md` | SkillNodeConfig/SkillEffect, ClothingItemData, armorDefense (T-CB06) |
| `docs/Character/04_STATS_PROGRESSION.md` | StatsConfig, XP формула |
| `docs/Character/05_CLOTHING_AND_MODULES.md` | equip/unequip, stat-bonuses |
| `docs/Character/06_SKILL_TREE.md` | SkillNodeConfig (proficiency, technique) — **Combat-движок читает (MVP+1)** |
| `docs/Character/07_UI_TABS_IN_CHARACTER_WINDOW.md` | UI pattern (Combat-движок НЕ использует) |
| `docs/Character/08_ROADMAP.md` | roadmap pattern |
| `docs/Character/09_OPEN_QUESTIONS.md` | решённые вопросы (Q1-Q6) |
| `docs/Character/10_REFERENCES.md` | file:line index |
| `docs/Character/CHANGELOG.md` | changelog |

### 2.2 Battle/ (combat-навыки, v0.2)

| Файл | Назначение |
|------|-----------|
| `docs/Character/Skills/Battle/00_README.md` | манифест, 5 дисциплин, ERPR-пакет |
| `docs/Character/Skills/Battle/01_ANALYSIS.md` | что есть / gaps / расхождение с GDD 20 / ERPR |
| `docs/Character/Skills/Battle/02_LORE.md` | лор-факты, damage-types, дистанции (с ERPR) |
| `docs/Character/Skills/Battle/ERPR_collaboration.md` | анализ ERPR-пакета, mapping A/B/C |
| `docs/Character/Skills/Battle/10_DESIGN.md` | архитектура + **§7 ERPR damage-формула** (Combat-движок переиспользует) |
| `docs/Character/Skills/Battle/20_SKILL_TREES.md` | 35 нод (отложены в roadmap) |
| `docs/Character/Skills/Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md` | pitfalls + open questions (обновлено с ответами) |
| `docs/Character/Skills/Battle/40_REFERENCES.md` | file:line index |

### 2.3 Real-Time Combat (ЭТОТ каталог, MVP)

| Файл | Назначение |
|------|-----------|
| `docs/Character/Skills/real-time-combat/00_README.md` | манифест, новый sequencing, anti-restrictive |
| `docs/Character/Skills/real-time-combat/01_ANALYSIS.md` | что есть / gaps / sequencing / ship-combat extensibility |
| `docs/Character/Skills/real-time-combat/02_LORE.md` | пеший vs корабельный бой, общая лор-база |
| `docs/Character/Skills/real-time-combat/10_DESIGN.md` | архитектура, IAttacker/ITarget abstractions |
| `docs/Character/Skills/real-time-combat/20_TECHNICAL.md` | NGO RPC, server-authoritative, hooks |
| `docs/Character/Skills/real-time-combat/30_SCENARIOS.md` | пеший MVP, ship-extensibility примеры |
| `docs/Character/Skills/real-time-combat/30_PITFALLS_AND_OPEN_QUESTIONS.md` | pitfalls + open questions (обновлённые) |
| `docs/Character/Skills/real-time-combat/40_REFERENCES.md` | этот файл |

### 2.4 Turn-Based Battles (PARKING)

| Файл | Назначение | Статус |
|------|-----------|--------|
| `docs/Character/Skills/turn-based-battles/00_README.md` | манифест TB | **PARKING** (отложен) |
| `docs/Character/Skills/turn-based-battles/01_ANALYSIS.md` | TB scope | **PARKING** |
| `docs/Character/Skills/turn-based-battles/10_DESIGN.md` | TB архитектура | **PARKING** |
| `docs/Character/Skills/turn-based-battles/20_TECHNICAL.md` | TB NGO | **PARKING** |
| `docs/Character/Skills/turn-based-battles/30_SCENARIOS.md` | TB сценарии | **PARKING** |
| `docs/Character/Skills/turn-based-battles/30_PITFALLS_AND_OPEN_QUESTIONS.md` | TB pitfalls | **PARKING** |
| `docs/Character/Skills/turn-based-battles/40_REFERENCES.md` | TB references | **PARKING** |

**НЕ удалять, не править** (ЗБТ может пересмотреть).

### 2.5 GDD (для справки)

| Файл | Назначение |
|------|-----------|
| `docs/gdd/GDD_00_Overview.md` | пиллар 2 «Исследование над боем» — **TB как opt-in совместимо**, Combat-движок = open world |
| `docs/gdd/GDD_01_Core_Gameplay.md` | режимы (пеший/корабль/камера) |
| `docs/gdd/GDD_02_World_Environment.md` | мир, погода |
| `docs/gdd/GDD_10_Ship_System.md` | **vision doc для ship combat (FUTURE)**, читаем для архитектурных hooks |
| `docs/gdd/GDD_20_Progression_RPG.md` | **расхождение** (корабельный Pilot/Merchant/Explorer, не наш scope) |

### 2.6 ERPR-источник

| Файл | Назначение |
|------|-----------|
| `docs/Character/Skills/Battle/ERPR_collaboration.md` | анализ ERPR, рекомендация B (принят) |
| `F:\yandex\Yandex.Disk\Glob file\homerule\erpr\ERPR1.2.pdf` | исходник (9 стр): damage dice 1dN (стр.7), hit location 1d4, crit 1d100, 3 сек (стр.5), 20 ОЗ (стр.3) |

---

## 3. Шаблоны и стандарты

| Документ | Назначение |
|----------|-----------|
| `MOON_SYSTEM.md` | шаблон тех. спецификации (мы пишем по нему) |
| `MOON_INVESTIGATION.md` | шаблон investigation log |
| `docs/Mining/ROADMAP.md` | канонический шаблон roadmap'а (8 секций) |
| `docs/NPC_quests/02_V2_ARCHITECTURE.md` | v2 hub-pattern (server NetworkBehaviour + DTO + ClientState) — **Combat-движок копирует** |
| `AGENTS.md` | hard rules |

---

## 4. Grep-данные (для Combat-движка)

| Что | Где | Результат |
|-----|-----|-----------|
| `IAttacker` | grep пусто в `Assets/` | **НЕ существует** — T-RTC01 |
| `IDamageTarget` | grep пусто в `Assets/` | **НЕ существует** — T-RTC01 |
| `IDamageSource` | grep пусто в `Assets/` | **НЕ существует** — T-RTC01 |
| `IRangePolicy` | grep пусто в `Assets/` | **НЕ существует** — T-RTC01 |
| `DamageResult` | grep пусто в `Assets/` | **НЕ существует** — T-RTC01 |
| `DamageType` | grep пусто в `Assets/` | **НЕ существует** — T-RTC01 (или переиспользуем из `Battle/10_DESIGN.md §3.1`) |
| `DamageDice` | grep пусто в `Assets/` | **НЕ существует** — T-RTC01 (или переиспользуем из `Battle/10_DESIGN.md §3.1`) |
| `HitLocation` | grep пусто в `Assets/` | **НЕ существует** — T-CB10 / T-RTC01 (но в real-time = `locMult=1.0`, hitLocation=1=default) |
| `CombatServer` | grep пусто в `Assets/` | **НЕ существует** — T-RTC06 |
| `CombatClientState` | grep пусто в `Assets/` | **НЕ существует** — T-RTC07 |
| `DamageCalculator` | grep пусто в `Assets/` | **НЕ существует** — T-RTC05 (спецификация в `Battle/10_DESIGN.md §7`) |
| `WeaponDamageSource` | grep пусто в `Assets/` | **НЕ существует** — T-RTC04 |
| `PlayerAttacker` / `PlayerTarget` | grep пусто в `Assets/` | **НЕ существует** — T-RTC02 |
| `NpcAttacker` / `NpcTarget` | grep пусто в `Assets/` | **НЕ существует** — T-RTC03 |
| `ShipAttacker` / `ShipTarget` | grep пусто в `Assets/` | **НЕ существует** — T-RTC16 (Phase 3) |
| `Turret` | grep пусто в `Assets/` | **НЕ существует** — T-RTC17 (Phase 3) |
| `AttackStartedEvent` / `AttackLandedEvent` / `DamageDealtEvent` / `EntityKilledEvent` | grep пусто в `WorldEvent.cs` | **НЕ существует** — T-RTC09 |
| `CombatConfig` | grep пусто в `Assets/` | **НЕ существует** — T-RTC09 |
| `armorDefense` поле | grep пусто в `Assets/` | **НЕ существует** — T-CB06 (ClothingItemData) |
| `damageDice/baseDamage/critModifier` поля | grep пусто в `Assets/` | **НЕ существует** — T-CB03 (WeaponItemData) |

---

## 5. Что НЕ нашлось (явно подтверждено)

| Что | Статус |
|-----|--------|
| **Real-Time Combat Engine целиком** | **НЕ существует** — проектируется (T-RTC01..T-RTC10) |
| `IAttacker` / `IDamageTarget` / `IDamageSource` / `IRangePolicy` interfaces | **НЕ существуют** — T-RTC01 |
| `DamageCalculator` (static) | **НЕ существует** — T-RTC05 (спецификация в `Battle/10_DESIGN.md §7` готова) |
| `CombatServer` (NetworkBehaviour) | **НЕ существует** — T-RTC06 |
| `CombatClientState` (singleton) | **НЕ существует** — T-RTC07 |
| `PlayerAttacker` / `PlayerTarget` (NetworkBehaviour) | **НЕ существуют** — T-RTC02 |
| `NpcAttacker` / `NpcTarget` (NetworkBehaviour) | **НЕ существуют** — T-RTC03 |
| `WeaponDamageSource` / `MeleeRangePolicy` / `RangedRangePolicy` | **НЕ существуют** — T-RTC04 |
| `CombatConfig` (SO) | **НЕ существует** — T-RTC09 |
| 4 новых event-класса в `WorldEvent.cs` | **НЕ существуют** — T-RTC09 |
| `DamageType` / `DamageDice` enums | **НЕ существуют** (но дизайн в `Battle/10_DESIGN.md §3.1`) — T-RTC01 |
| `HitLocation` enum | **НЕ существует** (но в real-time = `locMult=1.0`, hitLocation=1=default) — T-RTC01 |
| `WeaponItemData` (T-CB03) | **НЕ существует** — отложен в MVP+1 |
| `armorDefense` поле в `ClothingItemData` (T-CB06) | **НЕ существует** — отложен в MVP+1 |
| `ShipAttacker` / `ShipTarget` (T-RTC16) | **НЕ существует** — Phase 3 |
| `Turret` (T-RTC17) | **НЕ существует** — Phase 3 |
| `ShipRangePolicy` (T-RTC18) | **НЕ существует** — Phase 3 |
| `NpcShipAttacker` (T-RTC20) | **НЕ существует** — Phase 3 |
| NPC-AI для open world (HostileNPC, FactionAI) | **НЕ существует** — отдельная подсистема |
| PvP-дуэль flow (T-RTC11..T-RTC15) | **НЕ существует** — Phase 2 |
| UI damage numbers / hit flash (T-RTC10) | **НЕ существует** — Phase 2 |
| Client prediction (Phase 2) | **НЕ существует** |
| AreaOfInterest broadcasting (Phase 3) | **НЕ существует** |
| Line-of-sight raycast (Phase 2) | **НЕ существует** |

---

## 6. Связь с другими подсистемами (project)

| Документ | Связь с Combat-движком |
|---|---|
| `docs/Character/Skills/Battle/` | ERPR-формула (`10_DESIGN.md §7`), навыки (T-CB01..T-CB09, MVP+1) |
| `docs/Character/Skills/turn-based-battles/` | **PARKING** (отложен) — НЕ развиваем |
| `docs/Character/Skills/real-time-combat/` | **этот документ** |
| `docs/Character-menu/sub_inventory-tab/INVENTORY_V2_REFACTOR.md` | Inventory для ammo (стрелы, болты) |
| `docs/Character-menu/10_DESIGN.md` | UI pattern (НЕ переиспользуем, Combat — отдельное UI) |
| `docs/Crafting_system/` | рецепты гранат/мин (после T-CB04) — `ExplosiveDamageSource` |
| `docs/NPC_quests/` | quest-events (kill pirate → quest progress) — Combat-движок публикует `EntityKilledEvent` |
| `docs/Markets/` | rewards (credits за combat-achievements) |
| `docs/Ships/` (Player/ShipController) | **FUTURE** — `ShipAttacker` использует `ShipController._rb.position` (Phase 3) |
| `docs/PeacefulShip/` | **FUTURE** — `NpcShipAttacker` для враждебных (Phase 3) |
| `docs/gdd/GDD_10_Ship_System.md` | **FUTURE** — vision doc для ship combat, читаем для hooks |
| `docs/gdd/GDD_20_Progression_RPG.md` | **расхождение** (корабельный Pilot/Merchant/Explorer, не наш scope) |

---

## 7. Зависимости (Combat-движок от других подсистем)

| Combat-тикет | Зависит от | Что именно |
|---|---|---|
| T-RTC01 (interfaces) | **нет** (самостоятельный) | — |
| T-RTC02 (PlayerAttacker/Target) | **NetworkPlayer.cs** (T-P11), **StatsWorld** (T-P03), **EquipmentWorld** (T-P09) | читает owner clientId, STR/DEX, equipped weapon |
| T-RTC03 (NpcAttacker/Target) | **нет** (новый компонент) | — |
| T-RTC04 (WeaponDamageSource, range policies) | **после T-CB03** (WeaponItemData) | читает `damageDice/baseDamage/critModifier/range` |
| T-RTC05 (DamageCalculator) | **нет** (статический класс, спецификация в `Battle/10_DESIGN.md §7`) | — |
| T-RTC06 (CombatServer) | **NGO 2.x** (T-P11..T-P18 pattern) | scene-placed в BootstrapScene |
| T-RTC07 (CombatClientState) | **NetworkManagerController** (Awake), **WorldEventBus** (4 events) | singleton, persistent |
| T-RTC08 (NGO RPC + DTO) | **NGO 2.x** | INetworkSerializable, BufferSerializer |
| T-RTC09 (CombatConfig + 4 WorldEvent) | **WorldEvent.cs** | новые event-классы |
| T-RTC10 (UI) | **CharacterWindow pattern** | UI Toolkit |
| T-RTC11..T-RTC15 (PvP duel) | **T-RTC01..T-RTC10** | использует движок как есть |
| T-RTC16..T-RTC20 (ship combat) | **T-RTC01..T-RTC10** + **ShipController.cs** (add-only) | реализует интерфейсы |

**Вердикт:** **T-RTC01..T-RTC10 (MVP) самодостаточны**. Не блокируются другими подсистемами. Навыки (T-CB01..T-CB09) подключаются позже через хуки.

---

## 8. Sequenced roadmap (обновлённый v0.3)

| Фаза | Тикеты | Трудозатраты | Статус |
|---|---|---|---|
| **MVP (пеший combat без навыков)** | T-RTC01..T-RTC09 | **~23-32 ч** (3-4 сессии) | проектируется (v0.3) |
| **MVP+1 (пеший + навыки)** | T-CB01..T-CB09 + интеграция hooks | **~16-21 ч** (2-3 сессии) | отложен в roadmap |
| Phase 2 (UI + PvP-дуэль + NPC-AI + line-of-sight) | T-RTC10 + T-RTC11..T-RTC15 + NPC-AI (отдельная подсистема) | ~30-40 ч | Phase 2 |
| Phase 3 (ship combat + ЗБТ) | T-RTC16..T-RTC20 | ~25-33 ч | Phase 3 (после ЗБТ) |
| **PARKING** | turn-based-battles/ (отложен) | — | parking, не развиваем |

**ИТОГО до играбельного combat (пеший MVP + skills):** ~40-53 ч (5-7 сессий).
**ИТОГО до играбельного combat (включая ship + PvP):** ~95-126 ч (12-17 сессий).
