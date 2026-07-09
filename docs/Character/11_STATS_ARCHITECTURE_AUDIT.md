# Stats Architecture Audit — STR/DEX/INT

> **Дата:** 2026-07-26
> **Скоуп:** полный аудит архитектуры статов (str/dex/int), источники XP, бонусы экипировки/навыков, интеграция с combat, NPC.
> **Метод:** исследование кода (`PlayerStats.cs`, `StatsServer.cs`, `StatsConfig.cs`, `EquipmentWorld.cs`, `NpcCombatData.cs`, `SkillEffect.cs`, `DamageCalculator.cs`, `PlayerAttacker.cs`, etc.) + документов (`04_STATS_PROGRESSION.md`, `03_DATA_MODEL.md`, etc.)

---

## 1. Текущая архитектура — сводка

### 1.1 Что есть

| Слой | Файл | Роль |
|------|------|------|
| Enum | `PlayerStats.cs` | `StatType` enum (Strength=0, Dexterity=1, Intelligence=2) |
| Struct | `PlayerStats.cs` | 9 полей: 3×(xp, tier, totalXp) — per-stat hardcoded |
| Helper | `PlayerStatsRef.cs` | `ref` returns через switch — обход hardcoded полей |
| SO | `StatsConfig.cs` | XP per-source multipliers, формула роста, hardcoded source→stat mapping |
| Enum | `XpSource.cs` | 10 источников XP (Mining, Crafting, ..., Pilot) |
| Server | `StatsServer.cs` | Hub: subscribe WorldEventBus, ApplyXp, distance tracker, persist |
| World | `StatsWorld.cs` | POCO singleton: `Dictionary<ulong, PlayerStats>` |
| DTO | `StatsSnapshotDto.cs` | 18 полей: 3×(xp, tier, xpForNext, total, effective) |
| Client | `StatsClientState.cs` | Client-side singleton, OnStatsUpdated event |
| EquipSO | `ClothingItemData.cs` | 3 additive bonuses + 3 multipliers per stat |
| EquipSO | `ModuleItemData.cs` | 3 additive bonuses per stat |
| Equip | `EquipmentWorld.cs` | `GetEquipStatBonuses` — только аддитивные бонусы |
| SkillSO | `SkillNodeConfig.cs` | `effects[]` (SkillEffect), tier-requirements, XP-cost |
| SkillEffect | `SkillEffect.cs` | `StatMod` type: floatValue (additive) + multiplier |
| CombatIf | `IAttacker.cs` | `GetStrength()`, `GetDexterity()`, `GetIntelligence()` → int |
| PlayerCbt | `PlayerAttacker.cs` | `StatsToFlat(tier) = tier*5+10` — только TIER |
| NpcCbt | `NpcCombatData.cs` | Свой `int strength, dexterity, intelligence` |
| NpcCbt | `NpcAttacker.cs` | Возвращает `_data.strength` напрямую |
| Damage | `DamageCalculator.cs` | `baseAttack = roll + base + STR` — через `IAttacker.GetStrength()` |

### 1.2 Потоки данных (диаграмма)

```
WorldEventBus события (mining/craft/jump/...)
        │
        ▼
   StatsServer.ApplyXp()
        │
        ▼
   StatsWorld (PlayerStats struct)
        │                    │
        │         EquipmentWorld.GetEquipStatBonuses()  ← ClothingItemData/ModuleItemData
        │         (только additive, multiplier игнорируется)
        │                    │
        ▼                    ▼
   StatsSnapshotDto ──── effectiveXxx = base + equip additive
        │
        ▼
   StatsClientState → CharacterWindow UI
        │
        │ (effective отображается в UI)
        │
        │   ⚠️ НИЖЕ — РАЗРЫВ: combat НЕ читает effective
        │
        ▼
   PlayerAttacker.GetStrength() = tier*5 + 10
        │
        │   ⚠️ effectiveStrength из StatsSnapshot НЕ используется
        │   ⚠️ equip bonuses НЕ влияют на damage
        │   ⚠️ skill StatMod bonuses НЕ влияют на damage
        │
        ▼
   DamageCalculator: baseAttack = roll + base + STR
```

---

## 2. Найденные архитектурные проблемы

### 🔴 P0 — Equip/skill stat bonuses не влияют на combat

**Файлы:** `PlayerAttacker.cs:136-141`, `DamageCalculator.cs:45`, `EquipmentWorld.cs:67-91`

`PlayerAttacker` вычисляет статы как `StatsToFlat(tier) = tier * 5 + 10`. Это значит:
- **Бонусы от одежды** (`ClothingItemData.strengthBonus`) — отображаются в UI, но НЕ добавляются к `GetStrength()`.
- **Бонусы от модулей** (`ModuleItemData.strengthBonus`) — аналогично.
- **Множители** (`strengthMultiplier`) — НЕ применяются нигде. Dead data.
- **SkillEffect.StatMod** бонусы (`floatValue`, `multiplier`) — нигде не агрегируются и не применяются.

**Следствие:** игрок тратит XP/intelligence на изучение навыка `Skill_Combat_BasicStrike` с эффектом `StatMod(STR+2)`, но его урон в бою не меняется. Одежда `Clothing_SteelChestplate` (+3 STR) чисто косметическая для UI.

**Корень проблемы:** нет централизованного слоя агрегации, который бы:
1. Взял base stat (из `PlayerStats`)
2. Добавил equip additive bonuses
3. Применил equip multipliers
4. Добавил skill additive bonuses
5. Применил skill multipliers
6. Вернул **final effective stat** → который читает `PlayerAttacker`

### 🔴 P1 — Hardcoded stat explosion: 3 стата × N полей в 10+ файлах

**Файлы:** `PlayerStats.cs`, `PlayerStatsRef.cs`, `StatsSnapshotDto.cs`, `CharacterSaveData.cs`, `ClothingItemData.cs`, `ModuleItemData.cs`, `EquipmentWorld.cs`, `CharacterWindow.cs`, `InventoryTab.cs`

Добавление нового стата (например, `Luck` или `Constitution`) требует правок ВСЕХ этих файлов:

```
PlayerStats.cs          +1 xp field, +1 tier field, +1 totalXp field → +3 поля
PlayerStatsRef.cs       +1 case в каждом из 3 switch → +3 case
StatsSnapshotDto.cs     +1 xp, +1 tier, +1 xpForNext, +1 total, +1 effective → +5 полей
CharacterSaveData.cs    +1 xp, +1 tier, +1 totalXp → +3 поля
ClothingItemData.cs     +1 bonus, +1 multiplier → +2 поля
ModuleItemData.cs       +1 bonus → +1 поле
EquipmentWorld.cs       +1 out param в GetEquipStatBonuses → +3 строки
CharacterWindow.cs      +1 ProgressBar, +1 Label, +N строк кода
InventoryTab.cs         +1 строка форматирования
NpcCombatData.cs        +1 int поле
IAttacker.cs            +1 метод GetXxx() → +1 сигнатура
PlayerAttacker.cs       +1 GetXxx() реализация
NpcAttacker.cs          +1 GetXxx() реализация
```

**Корень проблемы:** структура `PlayerStats` — плоский struct с полями `strength`, `dexterity`, `intelligence` вместо коллекции (array/dictionary). Из-за этого каждый метод, работающий с полем по `StatType`, вынужден использовать `switch`, а не простую индексацию.

### 🟡 P2 — PlayerStatsRef: workaround для структурной проблемы

**Файл:** `PlayerStatsRef.cs`

Класс `PlayerStatsRef` с `ref` returns через `switch` существует исключительно потому, что поля `PlayerStats` не addressable по `StatType` напрямую. Если бы `PlayerStats` хранил `float[] xp = new float[statCount]`, `int[] tier = new int[statCount]`, `float[] totalXp = new float[statCount]`, все три метода `PlayerStatsRef` выродились бы в однострочник:

```csharp
// Вместо 60 строк PlayerStatsRef:
public float GetXp(StatType stat) => stats.xp[(int)stat];
public int GetTier(StatType stat) => stats.tier[(int)stat];
public float GetTotalXp(StatType stat) => stats.totalXp[(int)stat];
```

### 🟡 P3 — NPC-статы: полностью дублированная система

**Файлы:** `NpcCombatData.cs`, `NpcAttacker.cs`

NPC хранят свои статы в отдельном SO `NpcCombatData` (поля `int strength/dexterity/intelligence`), полностью игнорируя:
- `StatType` enum
- `PlayerStats` struct
- Tier progression
- XP систему

Если в будущем NPC должны получать XP/level-up (например, враги которые растут вместе с игроком), эту систему придётся переписывать заново. NPC и игроки не могут использовать общий код статов.

Более того, `NpcCombatData` использует `int`, а `PlayerStats` — `float`. `IAttacker` возвращает `int`. Это создаёт неявное округление при переходе float→int.

### 🟡 P4 — IAttacker фиксирует сигнатуру под 3 стата

**Файл:** `IAttacker.cs`

Интерфейс имеет методы `GetStrength()`, `GetDexterity()`, `GetIntelligence()` — по одному на каждый стат. При добавлении нового стата нужно добавлять новый метод `GetLuck()` и ломать всех имплементоров.

Решение: интерфейс должен иметь `int GetStat(StatType stat)` — один метод на все статы.

### 🟡 P5 — StatsSnapshotDto: effective поля только для UI

**Файл:** `StatsSnapshotDto.cs`, `StatsServer.cs`

DTO содержит 18 полей (6 на каждый стат). Поля `effectiveXxx` вычисляются как `base + equipAdditive` в `SendSnapshotToOwner`, но:
- multipliers не применяются (см. P0)
- `RecomputeAndSendSnapshot` дублирует ту же логику
- Кэш `_pendingEquipBonus` почти не используется

### 🟡 P6 — Equipment multipliers: семантика неоднозначна

**Файл:** `ClothingItemData.cs:40`

Tooltip: `"Множитель Strength (1.0 = +100% = ×2.0 итого)"`.

Это значит: `multiplier = 1.0` → финальный множитель = `1.0 + 1.0 = 2.0`. Это нестандартная семантика (обычно multiplier 1.0 = ×1.0, без изменений). Но главное — они вообще не применяются в коде (P0).

### 🟢 P7 — StatsConfig: две роли в одном SO

**Файл:** `StatsConfig.cs`

StatsConfig одновременно:
1. Хранит XP-multipliers per-source (`_miningXpPerItem`, ...)
2. Хранит формулу роста (`_tierBaseXp`, `_tierGrowthRate`)
3. Содержит hardcoded source→stat mapping (`GetStatFor`)
4. Хранит distance thresholds и флаги

Это 4 разные ответственности в одном SO. При росте будет сложно редактировать (дизайнеру XP, программисту — формула).

---

## 3. Что нужно рефакторить (приоритеты)

### P0 (блокер): Централизованный агрегатор статов

Создать единый слой, который для любого `clientId` возвращает `EffectiveStats`:
- base = `PlayerStats.xp` в текущем тире (уже есть)
- + equip additive (уже есть, но не доходит до combat)
- × equip multiplier (мёртвый код — оживить)
- + skill additive (SkillEffect.floatValue — мёртвый код)
- × skill multiplier (SkillEffect.multiplier — мёртвый код)
- = final → `PlayerAttacker.GetStrength()` возвращает это значение

### P1 (структурный): PlayerStats → array-backed

Заменить 9 плоских полей на массивы:
```csharp
public struct PlayerStats {
    public float[] xp;       // [StatTypeCount]
    public int[]   tier;     // [StatTypeCount]
    public float[] totalXp;  // [StatTypeCount]
}
```

Это удалит `PlayerStatsRef` целиком, упростит `StatsServer.ApplyXp`, `StatsSnapshotDto`, `CharacterSaveData`.

### P2 (структурный): Data-driven stat definitions

Вынести определения статов (id, displayName, icon, etc.) в ScriptableObject/конфиг вместо hardcoded enum. Это позволит добавлять статы без изменения кода (только конфиг + array в `PlayerStats`).

### P3 (унификация): Unify NPC + Player stat model

NPC должны использовать тот же `StatType` + массив значений, что и игроки. `NpcCombatData` становится thin wrapper: `int[] stats = {10, 10, 10}` вместо 3 отдельных полей. `IAttacker` меняется на `int GetStat(StatType stat)`.

### P4: StatsConfig — разделить роли

Вынести source→stat mapping в отдельный SO (`StatSourceMappingConfig`), формулу роста в `StatGrowthConfig` (уже задумано, но не реализовано), оставив в `StatsConfig` только per-source XP multipliers.

### P5: Effective stats pipeline — fix multiplier application

В `EquipmentWorld.GetEquipStatBonuses` или в новом агрегаторе: применить multipliers после additive. Семантика: `result = base + sum(additive) * product(1 + multiplier_i)`.

---

## 4. Что НЕ трогать

- ❌ Архитектуру WorldEventBus + StatsServer подписки — она работает хорошо
- ❌ Tier progression формулу — геометрический рост без капа, по дизайну
- ❌ Persistence (JsonCharacterDataRepository) — атомарная запись работает
- ❌ StatsClientState → CharacterWindow UI pipeline — работает корректно
- ❌ Unique-event dialog tracking (ХОРОШО: HashSet, не cooldown)
- ❌ Distance tracker (walk/pilot) — хорошо изолирован в StatsServer.FixedUpdate

---

## 5. Рекомендованная последовательность refactor

1. **Array-backed PlayerStats** — убрать PlayerStatsRef, упростить 10+ файлов
2. **Stat aggregator** — новый класс `EffectiveStatsCalculator`, подключается к `PlayerAttacker`
3. **IAttacker → GetStat(StatType)** — ломает сигнатуру, но нужно для extensibility
4. **NPC unification** — `NpcCombatData` использует тот же array-backed формат
5. **Data-driven stats** — ScriptableObject определения статов
6. **StatsConfig split** — разделение ответственности

---

## 6. Приложение: полный список затронутых файлов

```
Stats/PlayerStats.cs           — struct, StatType enum
Stats/PlayerStatsRef.cs        — ref returns helper
Stats/StatsConfig.cs           — SO: XP multipliers, formula, mapping
Stats/StatsServer.cs           — NetworkBehaviour hub
Stats/StatsWorld.cs            — POCO singleton
Stats/StatsClientState.cs      — client singleton
Stats/Dto/StatsSnapshotDto.cs  — 18-field DTO
Stats/Persistence/CharacterSaveData.cs — save DTO
Stats/XpSource.cs              — enum
Equipment/ClothingItemData.cs  — SO: 3 bonuses + 3 multipliers
Equipment/ModuleItemData.cs    — SO: 3 bonuses
Equipment/EquipmentWorld.cs    — GetEquipStatBonuses (additive only)
Skills/SkillNodeConfig.cs      — SO: effects[], tier-reqs, XP-cost
Skills/SkillEffect.cs          — StatMod struct
Skills/SkillsWorld.cs          — learned skills
Combat/Core/IAttacker.cs       — GetStrength/Dex/Int interface
Combat/Implementations/PlayerAttacker.cs — StatsToFlat(tier*5+10)
Combat/Implementations/NpcAttacker.cs    — _data.strength напрямую
Combat/Implementations/NpcCombatData.cs  — SO: свой int str/dex/int
Combat/DamageCalculator.cs     — baseAttack = roll + base + STR
UI/Client/CharacterWindow.cs   — stat bars, labels, FormatBonuses
UI/Client/CharacterWindow/InventoryTab.cs — FormatBonuses (дубликат)
```

**Всего:** 21 файл так или иначе касается архитектуры статов.
