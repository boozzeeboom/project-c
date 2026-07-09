# CHANGELOG — Character Progression

> **Что это:** лог изменений дизайн-документации. Каждая запись = одна сессия актуализации (сводка принятых решений пользователя → обновление файлов).
> **Дата первой записи:** 2026-06-14

---

## 2026-07-09 — P1: PlayerStats flat struct → StatBucket (f4ca1af)

**Сессия:** P1 из `13_SESSION_CONTINUATION.md` — замена 3×3 flat struct на StatBucket группировку.

### Выполнено

| # | Проблема | Статус | Коммит |
|---|---------|--------|--------|
| P1 | Flat 3×3 struct → StatBucket | ✅ Fixed | `f4ca1af` |
| P2 | PlayerStatsRef workaround | ✅ Удалён | `f4ca1af` |

### Изменённые файлы (7)
- `PlayerStats.cs` — StatBucket struct + PlayerStats со static ref accessors (Xp/Tier/TotalXp/GetBucket)
- `PlayerStatsRef.cs` — УДАЛЁН (методы перенесены в PlayerStats как static)
- `CharacterSaveData.cs` — PlayerStatsSave: 9 плоских полей → StatBucket[3]
- `StatsServer.cs` — PlayerStatsRef вызовы → PlayerStats.Xp/Tier/TotalXp
- `PlayerAttacker.cs` — .strengthTier → PlayerStats.GetTier(StatType)
- `SkillsWorld.cs` — .strengthTier/.intelligence → PlayerStats.GetTier/GetXp

### Не затронуты (NGO/ScriptableObject constraints)
- `StatsSnapshotDto` — 18 flat полей сохранены (NGO INetworkSerializable требует фиксированные поля)
- `ClothingItemData.cs`, `ModuleItemData.cs` — поля остаются публичными для Unity Inspector
- `EquipmentWorld.GetEquipStatBonuses` — out params без изменений (публичный API)
- `CharacterWindow.cs`, `StatsClientState.cs` — используют StatsSnapshotDto поля, не изменились

---

## 2026-07-09 — Stats Architecture Audit V2 fixes (Q0+Q1+Q2.P4)

**Сессия:** Поэтапное исправление проблем согласно сводному аудиту `12_STATS_ARCHITECTURE_AUDIT_V2.md`. Два коммита: T-STATS02 (P0/P5/P6/P7/P10) и T-STATS03 (P4).

### Выполнено (6 из 10 проблем)

| # | Проблема | Severity | Статус | Коммит |
|---|---------|----------|--------|--------|
| P0 | Combat bypasses equip bonuses | 🔴 Critical | ✅ Fixed | `8c49ee1` |
| P5 | GatheringServer mapping bypass | 🟡 Medium | ✅ Fixed | `8c49ee1` |
| P6 | Effective stat formula inconsistency | 🔴 Critical | ✅ Fixed | `8c49ee1` |
| P7 | Skill stat bonuses never applied | 🟡 Medium | ✅ Fixed | `8c49ee1` |
| P10 | DamageResultDto lacks stat breakdown | 🟢 Low | ✅ Fixed | `8c49ee1` |
| P4 | StatsConfig overload (4 responsibilities) | 🟡 Medium | ✅ Fixed | `7b8460c` |

### T-STATS02 — P0/P5/P6/P7/P10 (8c49ee1)

**Изменённые файлы:**
- `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs` — GetStrength/Dex/Int: tier + equip bonus + skill bonus
- `Assets/_Project/Scripts/Stats/PlayerStats.cs` — public static StatsToFlat(tier)
- `Assets/_Project/Scripts/Stats/StatsServer.cs` — effective stat = StatsToFlat(tier) + bonus (P6); GetStatFor(XpSource) (P5 helper)
- `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs` — StatType.Strength → ss.GetStatFor(XpSource.Mining)
- `Assets/_Project/Scripts/Skills/SkillsWorld.cs` — новый GetStatModBonuses(clientId, out str, dex, intl)
- `Assets/_Project/Scripts/Combat/Core/DamageResult.cs` — поля diceRoll/strengthContrib/baseContrib
- `Assets/_Project/Scripts/Combat/Network/DamageResultDto.cs` — поля + serialize + FromResult
- `Assets/_Project/Scripts/Combat/DamageCalculator.cs` — заполнение новых полей
- `docs/Character/12_STATS_ARCHITECTURE_AUDIT_V2.md` — новый

### T-STATS03 — P4 StatsConfig Split (7b8460c)

**Новые файлы:**
- `Assets/_Project/Scripts/Stats/ExperienceConfig.cs` — per-source XP, TierBaseXp, TierGrowthRate, GlobalMultiplier
- `Assets/_Project/Scripts/Stats/StatSourceMapConfig.cs` — XpSource → StatType mapping
- `Assets/_Project/Scripts/Stats/StatDebugConfig.cs` — DebugLogging, Distance thresholds, AnnounceTierUp
- `Assets/_Project/Resources/Stats/ExperienceConfig_Default.asset`
- `Assets/_Project/Resources/Stats/StatSourceMapConfig_Default.asset`
- `Assets/_Project/Resources/Stats/StatDebugConfig_Default.asset`

**Изменённые файлы:**
- `Assets/_Project/Scripts/Stats/StatsServer.cs` — `_config` → `_expConfig` / `_sourceMapConfig` / `_debugConfig`

### Осталось (P1/P2/P3/P8/P9 — следующие сессии)

| # | Проблема | Severity | План |
|---|---------|----------|------|
| P1 | Flat 3×3 struct — rigid for expansion | 🟡 Medium | Q2 — заменить на Dictionary<StatType, float> |
| P2 | PlayerStatsRef — workaround for P1 | 🟢 Low | Автоматически решится при P1 |
| P3 | Two stat systems (Player vs NPC) | 🟡 Medium | Q3 — унифицировать формулой |
| P8 | Equipment multipliers silently ignored | 🟢 Low | Q3 — GetEquipStatBonuses |
| P9 | NPC stats not linked to any formula | 🟢 Low | Q3 — вместе с P3 |

### Планы в Assets/.Aura/plans/
- `stats_audit_fixes_q0_q1_v1.md` — ✅ выполнено (T-STATS02)
- `stats_audit_q2_p4_statsconfig_split_v1.md` — ✅ выполнено (T-STATS03)

---

## 2026-07-26 — Stats Architecture Audit (deep analysis)

**Сессия:** Полный аудит архитектуры статов (str/dex/int) — код + документация. Выявлены структурные проблемы и предложен план рефакторинга.

### Ключевые находки

| # | Проблема | Severity |
|---|---------|----------|
| P0 | Equip/skill stat bonuses не влияют на combat — `PlayerAttacker` использует только tier, игнорируя effective stats | 🔴 Blocker |
| P1 | Hardcoded stat explosion: 3 стата × N полей дублируются в 21 файле | 🔴 Structural |
| P2 | `PlayerStatsRef` — workaround для hardcoded полей (60 строк switch) | 🟡 |
| P3 | NPC-статы — полностью дублированная система (`NpcCombatData` vs `PlayerStats`) | 🟡 |
| P4 | `IAttacker` фиксирует сигнатуру под 3 стата (`GetStrength/GetDexterity/GetIntelligence`) | 🟡 |
| P5 | `StatsSnapshotDto`: effective поля только для UI, не потребляются combat | 🟡 |
| P6 | Equipment multipliers — dead data (нигде не применяются) | 🟡 |

### Новые файлы

| Файл | Назначение |
|------|-----------|
| `docs/Character/11_STATS_ARCHITECTURE_AUDIT.md` | Полный аудит: текущая архитектура, диаграмма потоков, 7 проблем, план рефакторинга |

---
## 2026-07-26 — T-STAT01: Архитектурный аудит системы статов

**Задача:** Провести глубокий анализ архитектуры статов (str/dex/int) — код + документация, выявить структурные проблемы.
**Коммит:** `857f442` — T-STAT01: Архитектурный аудит системы статов (STR/DEX/INT)
**Изменения:**
- `docs/Character/11_STATS_ARCHITECTURE_AUDIT.md` — новый: полный аудит, диаграмма потоков, 7 проблем, план рефакторинга
- `docs/Character/00_README.md` — обновлён: добавлена ссылка на аудит
- `docs/Character/CHANGELOG.md` — обновлён: запись об аудите

---

## 2026-06-15..17 — S2: Полная реализация Character Progression (18 тикетов)


**Сессия:** Имплементация T-P01..T-P18 по `08_ROADMAP.md`. ~30 часов кодинга, 4 фазы, 50+ .cs файлов, 20+ .asset файлов.

### Милестоуны

| Milestone | Тикеты | Статус | Ключевые результаты |
|-----------|--------|--------|-------------------|
| **M1 — Stats core** | T-P01..T-P06 | ✅ DONE | StatsConfig SO, PlayerStats, StatsWorld, StatsClientState, StatsServer (9 событий WorldEventBus + FixedUpdate walk tracker), JsonCharacterDataRepository, auto-spawn через NMC. STR/INT/DEX работают. |
| **M2 — Clothing & Modules** | T-P07..T-P10 | ✅ DONE | ClothingItemData/ModuleItemData (2 SO), EquipSlot enum, EquipmentData, EquipmentWorld, EquipmentServer (TryEquip/TryUnequip + rate limit), EquipmentClientState. Equip → remove from inventory, Unequip → add back. |
| **M3 — Skill Tree** | T-P11..T-P14 | ✅ DONE | SkillNodeConfig + SkillEffect (8 .asset: 4 combat + 4 social), SkillsConfig/SkillsWorld/SkillsServer, SkillsClientState. Skill rows в CharacterWindow (LOCKED/AVAILABLE/LEARNED). Click handlers deferred до battle system. |
| **M4 — UI Integration** | T-P15..T-P18 | ✅ DONE | CharacterWindow: single-page ПЕРСОНАЖ (Характеристики+Одежда+Модули+Навыки). Effective stat bars. [СНЯТЬ] button. [НАДЕТЬ] button в инвентаре. Inventory split layout (list+detail). 3 server GO в BootstrapScene. |

### Ключевые архитектурные решения (bugfixes)

| Проблема | Решение |
|----------|---------|
| ItemID хардкод (1001..2001) не работали | Переход на `FindItemIdByName()` + slot-based fallback при ID mismatch |
| ClothingItemData/ModuleItemData не загружались в `_itemDatabase` | `RegisterEquipmentAssets()` → `Resources.LoadAll<T>(path)` + auto-ID |
| Множитель DEX (10M XP при scene load) | `MaxWalkDeltaPerFixedUpdate = 5.0f` clamp |
| `[СНЯТЬ]` button не работала | `RegisterCallback<ClickEvent>` через closure с captured EquipRow + `TrickleDown.TrickleDown` capture phase |
| Unequip не возвращает item в инвентарь | `Equip → RemoveItems`, `Unequip → AddItemDirect` + ID resolution fallback |
| Save не срабатывал при exit | `Flush` в `OnNetworkDespawn` + `periodic auto-save 30s` |
| Effective stats = 0 при equip | `SendSnapshotToOwner` → inline `GetEquipStatBonuses()` (не через cache) |
| Пустое место под inventory ListView | ScrollView заменил ListView в UXML (flex-grow корректно) |

### Файлы

| Файл | LOCs | Назначение |
|------|------|-----------|
| `Assets/_Project/Scripts/Stats/` (11 .cs) | ~2000 | StatsConfig, PlayerStats, StatsWorld, StatsClientState, StatsServer, Repository |
| `Assets/_Project/Scripts/Equipment/` (7 .cs) | ~1500 | ClothingItemData, ModuleItemData, EquipSlot, EquipmentData, World, Server, ClientState |
| `Assets/_Project/Skills/` (6 .cs) | ~1200 | SkillNodeConfig, SkillEffect, SkillCategory, SkillsConfig, World, Server, ClientState |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (+~900 LOC) | 3400 | SubscribeX/UnsubscribeX, MakeManualSkillRow, MakeManualEquipRow, MakeInventoryRow/BindInventoryRow |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` (+~400 LOC) | 540 | progression, stats, skills, equipment, inventory layout |

### Что осталось (deferred)

- Skills click handlers (awaiting battle system integration)
- [НАДЕТЬ] кнопка в пустых equip slots (сейчас только [СНЯТЬ] для filled)
- Painter2D skill tree graph (Phase 2)
- Drag-and-drop equip (Phase 2)

### Closed

- ✅ **Все 18 тикетов T-P01..T-P18**
- ✅ All milestones M1-M4
- ✅ Compile 0 errors
- ✅ 18 .asset files (StatsConfig, SkillsConfig, 8 Skills, 5 Clothing, 3 Modules)
- ✅ 3 server GameObjects в BootstrapScene

---

## 2026-06-14 — S1: Первые ответы пользователя на Open Questions

**Сессия:** пользователь записал ответы под каждым вопросом в `09_OPEN_QUESTIONS.md`. Задача — актуализировать всю дизайн-документацию по ответам.

### Решения пользователя (сводка)

| # | Вопрос | Ответ | Действие |
|---|--------|-------|----------|
| Q1.1 | Стартовые значения | **(a) 0/0/0** | OK — current default |
| Q1.2 | Per-stat base XP разные? | **все одинаковые**, главное — **множители за действия в SO** | Подтвердить, никаких изменений tierBaseXp |
| Q1.3 | Глобальный множитель | **без ограничений**, 1 = ориентир на норму | Расширить Range |
| Q1.4 | NPC-spam cooldown | **НЕ cooldown**, а **per unique dialog/нажатие** | **ПЕРЕПИСАТЬ** §3 NPC-spam protection |
| Q1.5 | Walk threshold | **per 1m**, настраиваемо, **зацемпить total walked для ачивок** | Изменить + добавить `totalDistanceWalked` |
| Q1.6 | Pilot XP | только пилотирование (boarding не считается) | OK — current default |
| Q1.7 | Stat-bonus | **(c) Both** additive + multiplicative | OK — current default |
| Q2.1 | Clothing vs Module | **(a) Wearable vs Implant**, **но оба видны** | Уточнить semantic "видны" |
| Q2.2 | Слоты | **(a) 13 слотов** | OK — current default |
| Q2.3 | Required skills | **(c) Both** — hard для уникальных, soft для обычных | **ИЗМЕНИТЬ** TryEquip |
| Q2.4 | Unequip delay | **(a) мгновенно** | OK — current default |
| Q2.5 | Модули | **(a) персонажные импланты** | OK — current default |
| Q3.1 | Навыки в MVP | **8 навыков достаточно** | OK — current default |
| Q3.2 | Стартовые навыки | **(b) никаких** | **ИЗМЕНИТЬ** SkillsConfig.defaultSkills = empty |
| Q3.3 | Skill XP cost | **(a) 0/100/200**, настраиваемо | OK — current default |
| Q3.4 | Забывание навыков | **(c) Free respec без потерь** | **ДОБАВИТЬ** RequestForgetSkillRpc |
| Q3.5 | Prerequisites | **(a) DAG**, **нодовая система → Phase 2** | OK — current default |
| Q3.6 | Skill tree UI | **сразу с Painter2D graph** | **ИЗМЕНИТЬ** — T-P14 + T-P19 объединить |
| Q4.1 | Tab placement | **(a) nested sub-tabs** | OK — current default |
| Q4.2 | Progress bar | **(b) Fill + value, без тиров в UI** | **УБРАТЬ** tier label |
| Q4.3 | Tier color | **(b) per-category + свечение по уровню** | **ИЗМЕНИТЬ** USS-стили (continuous glow) |
| Q4.4 | Tier-up notification | **все 3 комплексно (toast + inline + progress)** | **ДОБАВИТЬ** QuestToast integration |
| Q4.5 | Skill row action | **без панели, только кнопки** | OK — current default |
| Q5.1 | Save format | JSON (a) | OK — current default |
| Q5.2 | Atomic write | общий подход без костылей | OK — current default (tmp + Move) |
| Q5.3 | Save триггеры | (a) + (b) + (c) | OK — current default |
| Q5.4 | Load триггеры | (a) OnClientConnected | OK — current default |
| Q6.1 | Placeholder | M1 = working server сразу | OK — current default |
| Q6.2 | Стартовый StatsConfig | настраиваемо + глобальный мультипликатор | OK — current default |
| Q7.1 | Out-of-scope | всё верно | OK — current default |
| Q8.1 | Приоритет | **всё по порядку, без костылей** | Подтвердить |
| Q8.2 | M4 разделение | по приоритетам только если нужно | OK — current default |
| Q9.1 | Локализация | (a) hardcoded, локализация позже | OK — current default |
| Q9.2 | Тестирование | manual через unity-mcp | Подтвердить |
| Q9.3 | Структура | per-subsystem folders | OK — current default |
| Q10.1 | Формула | (a) classic geometric | OK — current default |
| Q10.2 | Heavy weapons | (a) только mining, **задокументировать вход** | OK + добавить в roadmap |
| Q10.3 | Mining mapping | **hardcoded mining → STR**, не нужна вариативность | **УБРАТЬ** per-stat mapping fields |
| Q10.4 | Глобальный множитель в UI | (a) Скрыт | OK — current default |
| Q10.5 | Когда добавлять events | все 8 в одном, **с debug on/off в инспекторе** | **ДОБАВИТЬ** _debugLogging поле |

### Файлы обновлённые

| Файл | Что изменилось |
|------|----------------|
| `00_README.md` | TL;DR обновлён — убраны "угадайки", заменены на confirmed answers |
| `02_V2_ARCHITECTURE.md` | Убрано per-stat mapping (Q10.3), добавлен debug logging (Q10.5), добавлен total walked tracking (Q1.5) |
| `03_DATA_MODEL.md` | `StatsConfig`: расширен Range globalMultiplier (Q1.3), убраны per-stat mapping (Q10.3), добавлен debug logging toggle (Q10.5), добавлен total walked distance field (Q1.5) |
| `04_STATS_PROGRESSION.md` | §3 NPC-spam полностью переписан (Q1.4), §1.3 уточнён per-1m (Q1.5), добавлен total walked achievement tracking |
| `05_CLOTHING_AND_MODULES.md` | §4.3 TryEquip поддерживает Both (hard+soft) (Q2.3), §1.2 "видны" уточнено (Q2.1) |
| `06_SKILL_TREE.md` | §4.3 Painter2D graph как primary подход (Q3.6), §1.3 default skills = empty (Q3.2), §3.2 RequestForgetSkillRpc (Q3.4) |
| `07_UI_TABS_IN_CHARACTER_WINDOW.md` | §3.2 убран tier label, упрощён stat-row-progress (Q4.2), §3.2 stat-progress-fill — per-category colors + continuous glow (Q4.3), §4.6 tier-up = 3 эффекта (Q4.4) |
| `08_ROADMAP.md` | T-P11 (SkillsConfig) defaultSkills = empty, T-P13 (SkillsServer) добавить forget, T-P14 (Skill UI) **Painter2D graph** = primary, T-P19 помечен как T-P14b (объединён), T-P20 forget skill |
| `09_OPEN_QUESTIONS.md` | Добавлена секция "## 11. Decision Log" — зафиксированы все ответы |

### Не изменилось (подтверждение defaults)

- Q1.1 стартовые 0/0/0
- Q1.6 только пилотирование
- Q1.7 additive+multiplicative bonuses
- Q2.2 13 слотов
- Q2.4 unequip мгновенно
- Q2.5 персонажные импланты
- Q3.1 8 навыков достаточно
- Q3.3 0/100/200 XP cost
- Q3.5 DAG
- Q4.1 nested sub-tabs
- Q4.5 без панели
- Q5.1 JSON
- Q5.2 tmp + Move
- Q5.3 (a)+(b)+(c) save triggers
- Q5.4 (a) load on connect
- Q6.1 M1 = working server
- Q6.2 настраиваемо
- Q7.1 out-of-scope OK
- Q9.1 hardcoded Russian
- Q9.3 per-subsystem folders
- Q10.1 classic geometric
- Q10.4 скрыт

### Открыто осталось (требует доп. решений)

Ничего критичного — все топ-5 вопросов получили ответы. Дизайн зафиксирован, можно начинать T-P01.
