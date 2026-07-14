# GDD 20: Progression & RPG System

**Game:** Project C: The Clouds
**Version:** 3.0
**Status:** 🟢 Реализовано (T-P01..T-P18 + T-STAT01..05 + T-CB-22/23 + T-HP01)
**Last Updated:** 14.07.2026
**Author:** Малков Леонид Андреевич

---

## 1. Обзор системы

### 1.1 Цель

Создать систему прогрессии, где рост персонажа основан на **per-stat tier'ах** (Strength/Dexterity/Intelligence) и **графе навыков (Skill Tree)** вместо традиционных уровней. Игрок получает XP за действия (майнинг, крафт, квесты, торговля, ходьба, полёт), XP накапливается в пуле соответствующей характеристики, и при достижении порога — повышается tier характеристики.

### 1.2 Объём

- **Experience System** — 10 источников XP (Mining, Crafting, Exchange, Market, QuestAccepted, QuestCompleted, Dialog, Jump, Walk, Pilot)
- **Tier System** — 3 характеристики (STR/DEX/INT), каждая со своим XP-пулом и tier
- **Skill Tree** — граф навыков (SkillNodeConfig SO), server-authoritative
- **Character Stats** — 3 базовые характеристики с единой формулой `tier × 5 + 10`
- **Equipment Bonuses** — flat + multiplier поверх base stats
- **Prestige System** — future, после заполнения всех систем

### 1.3 Этапы реализации

| Этап | Система | Статус |
|------|---------|--------|
| T-P01..T-P05 | Stats Architecture (StatBucket, StatsWorld, StatsServer, ApplyXp) | ✅ DONE |
| T-P06 | Persistence (JsonCharacterDataRepository) | ✅ DONE |
| T-P07..T-P13 | Skills (SkillsWorld, SkillsServer, SkillTreeWindow) | ✅ DONE |
| T-P14..T-P18 | Integration, scene-placement, CharacterWindow | ✅ DONE |
| T-STAT01..05 | Stats Architecture Refactoring (аудит, StatBucket, StatsConfig→3 SO) | ✅ DONE |
| T-CB-22/23 | Skill Tree MVP (27+ нод, AOC integration) | ✅ DONE |
| T-HP01 | Health System (HealthConfig, PlayerTarget) | ✅ DONE |
| Prestige | Система престижа | ⏳ Future |

### 1.4 Связанные документы

- GDD_21_Quest_Mission_System.md
- GDD_23_Faction_Reputation.md
- GDD_22_Economy_Trading.md
- `docs/Character/` — архитектурные доки

---

## 2. Experience System

### 2.1 Источники XP

| XpSource | Значение enum | Описание | Binding stat |
|----------|--------------|----------|--------------|
| Mining | 0 | Добыча ресурсов (за 1 единицу) | Настраивается в StatSourceMapConfig |
| Crafting | 1 | Завершённый крафт (за 1 единицу) | Настраивается |
| Exchange | 2 | Операция обмена (Pack/Unpack) | Настраивается |
| Market | 3 | Покупка/продажа | Настраивается |
| QuestAccepted | 4 | Принятие квеста | Настраивается |
| QuestCompleted | 5 | Завершение квеста | Настраивается |
| Dialog | 6 | Уникальный диалог (1 раз per player/npc/node) | Настраивается |
| Jump | 7 | Прыжок | Настраивается |
| Walk | 8 | Ходьба (за 1 метр) | Настраивается |
| Pilot | 9 | Пилотирование (за 1 метр) | Настраивается |

### 2.2 Per-source XP (ExperienceConfig)

```
GetBaseXp(XpSource) → float
```

| Source | Default XP | Примечание |
|--------|-----------|------------|
| Mining | 1.0 | за 1 единицу |
| Crafting | 5.0 | за 1 единицу |
| Exchange | 2.0 | за операцию |
| Market | 1.0 | за операцию |
| QuestAccepted | 3.0 | за принятие |
| QuestCompleted | 10.0 | за завершение |
| Dialog | 1.0 | уникальный визит |
| Jump | 0.5 | за прыжок |
| Walk | 1.0 | за метр |
| Pilot | 1.0 | за метр |

Глобальный множитель (`_globalMultiplier`, default 1.0) применяется ко всем источникам.

### 2.3 Формула tier-прогрессии

```
XpForNextTier(currentTier) = TierBaseXp × TierGrowthRate^currentTier
Default: TierBaseXp = 100, TierGrowthRate = 1.5
```

Tier promotion loop: при каждом получении XP, если XP >= порога — вычитается порог, tier повышается. Без капа.

### 2.4 Архитектура (xP flow)

```
WorldEventBus Event (MiningCompleted, etc.)
  → StatsServer.OnEvent()
    → ApplyXp(clientId, statType, rawXp)
      → PlayerStats.Xp += rawXp × globalMultiplier
      → while (Xp >= XpForNextTier(tier)): Xp -= XpForNextTier(tier); tier++
      → SendSnapshotToOwner()
```

### 2.5 Client State

`StatsClientState` (singleton, AutoSpawn) — получает `StatsSnapshotDto` от StatsServer:
- `strength`, `dexterity`, `intelligence` — tier, xp, totalXp
- `effectiveStr/Dex/Int` — с учётом экипировки
- `maxHp`, `currentHp` — от HealthConfig

---

## 3. Character Stats

### 3.1 Три характеристики

| StatType | Enum | Описание | Combat formula |
|----------|------|----------|----------------|
| Strength | 0 | Сила — физическая мощь | tier × 5 + 10 |
| Dexterity | 1 | Ловкость — точность и скорость | tier × 5 + 10 |
| Intelligence | 2 | Интеллект — XP для изучения навыков | tier × 5 + 10 |

Формула: `combatStat = tier × 5 + 10` (tier 0=10, tier 1=15, tier 2=20, ...)

### 3.2 StatBucket

```
struct StatBucket {
    float xp;        // текущий XP в этом tier
    int tier;        // текущий tier (0+)
    float totalXp;   // накопленный XP за всё время
}
```

3 StatBucket'а: `strength`, `dexterity`, `intelligence` — внутри `PlayerStats` struct.

### 3.3 PlayerStats struct

```
struct PlayerStats {
    StatBucket strength;
    StatBucket dexterity;
    StatBucket intelligence;
    
    static StatsToFlat(int tier) => tier × 5 + 10;
    static Default => all tiers = 0, xp = 0
}
```

### 3.4 Взаимодействие со Skills

Интеллект (Intelligence XP pool) тратится на изучение навыков в Skill Tree:
- `SkillsWorld.TryLearnSkill()` проверяет `PlayerStats.Intelligence.xp >= cost`
- `StatsServer.ApplyXpDirect(clientId, StatType.Intelligence, -cost)` списывает XP

Требования по tier для навыков:
- `RequiredStrengthTier` — минимальный tier силы
- `RequiredDexterityTier` — минимальный tier ловкости
- `RequiredIntelligenceTier` — минимальный tier интеллекта

### 3.5 Equipment Bonuses

Экипировка даёт flat бонусы и мультипликаторы:
```
effective = (base_tier_stat + equip_flatBonus) × (1.0 + equip_multiplierSum)
```

EquipmentWorld.GetEquipStatBonuses() возвращает `(bonusStr, bonusDex, bonusInt, multStr, multDex, multInt)`.

---

## 4. Skill Trees

### 4.1 Структура

В отличие от GDD-дизайна (3 ветки «пилот/торговец/исследователь»), реализована **двухкатегорийная система** с combat-дисциплинами:

```
SkillCategory: Social (0), Combat (1)
  └─ CombatDiscipline: None, Combat, Melee, Ranged, Defense, Placed
      └─ CombatSubtype: None, Throwables, Traps, Bows, Crossbows
```

### 4.2 SkillNodeConfig (ScriptableObject)

Каждый навык — отдельный SO:

| Поле | Тип | Описание |
|------|-----|----------|
| skillId | string | Уникальный ID (префикс: `melee_`, `ranged_`, `social_`, ...) |
| displayName | string | Имя навыка |
| description | string | Описание (2-4 строки) |
| icon | Sprite | Иконка |
| category | SkillCategory | Social / Combat |
| discipline | CombatDiscipline | Auto-set по skillId prefix в OnValidate |
| subtype | CombatSubtype | Подтип (Throwables/Bows/Crossbows) |
| requiredWeaponMask | WeaponClassMask | Какое оружие нужно для активации |
| prerequisites | SkillNodeConfig[] | DAG зависимостей (cycle detection в OnValidate) |
| effects | SkillEffect[] | Эффекты при изучении (stat bonuses, unlocks) |
| learnXpCost | float | XP Intelligence cost (0 = free) |
| requiredStrengthTier | int | Мин. tier силы |
| requiredDexterityTier | int | Мин. tier ловкости |
| requiredIntelligenceTier | int | Мин. tier интеллекта |
| isActive | bool | Active (bindable) vs Passive |
| cooldownSeconds | float | Кулдаун для active навыков |
| attackClip | AnimationClip | Анимация атаки (data-driven) |
| aoeFormula | AoeFormula | SingleTarget/Cone/Sphere/Line/Box |
| aoeSize / aoeConeAngleDeg / aoeWidth | float | Параметры AOE |
| VFX поля | GameObject, Material | Cast/Projectile/Impact VFX |

### 4.3 Каталоги (3 ScriptableObject)

- `WeaponCatalog.asset` — 9+ видов оружия
- `ArmorCatalog.asset` — 5+ видов брони
- `TechniqueCatalog.asset` — 13+ техник/заклинаний

### 4.4 Flow изучения навыка

```
SkillTreeWindow (UI Toolkit)
  → NetworkSkillTree.RequestLearnRpc(skillId)
  → SkillsServer.RequestLearnSkillRpc (Rate limit 5 ops/sec)
  → SkillsWorld.TryLearnSkill (5-step validation):
      1. SkillId exists?
      2. Already learned?
      3. Prerequisites met? (BFS по DAG)
      4. Stat tiers sufficient? (STR/DEX/INT)
      5. XP cost sufficient? → StatsServer.ApplyXpDirect
  → On success: _learnedSkills.Add(), SkillEffect.Apply()
  → SkillTreeSnapshot broadcast → SkillAnimationPlayer reload AOC
```

### 4.5 SkillEffect

```
enum SkillEffect.Type {
    StatMod,          // стат бонус (Strength +2)
    Damage,           // модификатор урона
    Heal,             // модификатор лечения
    WeaponProficiencyUnlock,  // Phase 2 stub
    ArmorProficiencyUnlock,
    WeaponTechniqueUnlock,
    ExplosiveRecipeUnlock,
    AntigravTechniqueUnlock,
}
```

### 4.6 Skill Animation System

- `SkillAnimationPlayer` — загружает AOC (AnimatorOverrideController) из `Resources/Animations/Combat/`
- `SkillInputService` — клиентский сервис для активации навыков
- `SkillAnimationEventPassthrough` — Animation Event → damage/detection

### 4.7 Перераспределение навыков

- Q3.4: Free respec (без возврата XP). `TryForgetSkill()` — удаляет навык из learned set.
- XP за навык НЕ возвращается (user decision: XP ≠ currency).

---

## 5. Persistence

### 5.1 CharacterSaveData

Единый JSON-файл на игрока:

```
CharacterSaveData {
    PlayerStatsSave stats;   // 3 StatBucket'а
    SkillsSave skills;       // learnedSkillIds[]
    EquipmentSave equipment; // equipped items
}
```

### 5.2 Lifecycle

- `OnClientConnectedCallback` → LoadPlayer (stats + skills + equipment)
- `OnClientDisconnectCallback` → BuildSaveData → Save (atomic JSON через tmp→rename)
- `OnNetworkDespawn` → FLUSH save для всех игроков перед shutdown

---

## 6. Logging & Debug

### 6.1 StatDebugConfig (ScriptableObject)

- `DebugLogging` — включить подробный лог XP/tier
- `TrackTotalDistance` — трекать общую дистанцию ходьбы/полёта
- `WalkDistanceXpThreshold` — порог для начисления XP за ходьбу (default 10m)
- `PilotDistanceXpThreshold` — порог для начисления XP за полёт

### 6.2 AOE Debug Visualization

- `SkillNodeConfig.debugVisualizeAoe` — в Editor режиме рисует 3D wireframe AOE зоны
- `SkillAoeDebugVisualizer` — компонент для отладки

---

## 7. Prestige System (Future)

Не реализовано. Запланировано как post-MVP фича:
- Сброс tier'ов до 0
- Сохранение навыков, экипировки, репутации
- Получение Prestige Points за уникальные достижения

---

## 8. Tuning Knobs

### ExperienceConfig

| Параметр | Default | Описание |
|----------|---------|----------|
| MiningXpPerItem | 1.0 | XP за единицу майнинга |
| CraftingXpPerItem | 5.0 | XP за единицу крафта |
| ExchangeXpPerOp | 2.0 | XP за обмен |
| MarketXpPerOp | 1.0 | XP за торговлю |
| QuestAcceptedXp | 3.0 | XP за принятие квеста |
| QuestCompletedXp | 10.0 | XP за завершение квеста |
| DialogXpPerVisit | 1.0 | XP за уникальный диалог |
| JumpXp | 0.5 | XP за прыжок |
| WalkXpPerMeter | 1.0 | XP за метр ходьбы |
| PilotXpPerMeter | 1.0 | XP за метр полёта |
| GlobalMultiplier | 1.0 | Множитель ко всем XP |
| TierBaseXp | 100 | Базовый XP для порога |
| TierGrowthRate | 1.5 | Рост между тирами |

### SkillsConfig

| Параметр | Default | Описание |
|----------|---------|----------|
| defaultSkills | empty | Стартовые навыки (Q3.2: пусто) |
| MaxOpsPerSec | 5 | Rate limit learn/forget |
| SkillsResourcesPath | "Skills" | Путь в Resources/ для SO |

### HealthConfig

| Параметр | Default | Описание |
|----------|---------|----------|
| baseHp | 100 | Базовая HP |
| strToHpMultiplier | 10 | HP за tier силы |
| respawnHpPercent | 0.5 | HP при респавне |

---

## 9. Реализация в коде (v3, актуальная)

### 9.1 Ключевые отличия от v2 GDD

| Аспект | GDD 2.0 (дизайн) | Реализация (код) |
|--------|-------------------|------------------|
| Skill Trees | 3 ветки: пилот/торговец/исследователь, ~60-70 навыков | 2 категории (Social/Combat), 5 Discipline, CombatSubtype |
| XP/Leveling | 50 уровней, XP_to_next = floor(100 × level^1.5) | Per-stat tier'ы, XP_for_tier = 100 × 1.5^tier |
| Character Stats | 4: Endurance/Navigation/Mechanics/Luck | 3: Strength/Dexterity/Intelligence |
| Skill Points | +1 SP per level, respec за кредиты | XP-стоимость (Intelligence pool), free respec |
| Milestones | Levels 3/5/7/10/... разблокируют контент | Не реализованы |
| XP Sources | Контракты, доставка, разведка, сопровождение, контрабанда, торговля, артефакты | Mining, Crafting, Exchange, Market, Quests, Dialog, Jump, Walk, Pilot |

### 9.2 Архитектура Stats

```
┌──────────────────────────────────────────────────────────┐
│ StatsServer (NetworkBehaviour, BootstrapScene)           │
│ • Subscribe к 9 WorldEventBus событиям                    │
│ • ApplyXp / ApplyXpDirect                                │
│ • RecomputeAndSendSnapshot (после equip/unequip)          │
│ • Walk/Pilot distance tracker (FixedUpdate)              │
│ • Unique dialog tracking (per player/npc/node)           │
│ • Persistence hooks (OnClientConnected/Disconnected)     │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ StatsWorld (POCO singleton, server-only)                 │
│ • Dictionary<ulong, PlayerStats>                         │
│ • GetOrCreateStats / SetStats / RemovePlayer             │
│ • BuildSaveData / LoadPlayer                             │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ StatsClientState (singleton, AutoSpawn)                  │
│ • CurrentSnapshot (StatsSnapshotDto)                     │
│ • OnStatsUpdated event                                   │
└──────────────────────────────────────────────────────────┘
```

### 9.3 Архитектура Skills

```
┌──────────────────────────────────────────────────────────┐
│ SkillsServer (NetworkBehaviour, BootstrapScene)          │
│ • RPC: RequestLearnSkillRpc / RequestForgetSkillRpc      │
│ • Rate limit 5 ops/sec/client                            │
│ • SendSkillResult / SendSnapshotToOwner                  │
│ • ApplySkillEffects (StatMod, WeaponProficiency, etc.)   │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ SkillsWorld (POCO singleton, server-only)                │
│ • _skillsById: Dictionary<string, SkillNodeConfig>       │
│ • _learnedPerPlayer: Dictionary<ulong, HashSet<string>>  │
│ • LoadAllSkills / GrantDefaultSkills                     │
│ • TryLearnSkill (5-step) / TryForgetSkill                │
│ • GetStatModBonuses (P7 fix)                             │
│ • BuildSaveData / LoadPlayer                             │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ SkillTreeWindow (UI Toolkit)                             │
│ • SkillsClientState → OnSkillTreeUpdated event           │
│ • Таб в CharacterWindow                                  │
└──────────────────────────────────────────────────────────┘
```

### 9.4 Файлы (C#)

**Stats:**
- `Scripts/Stats/PlayerStats.cs` — struct с 3 StatBucket'ами
- `Scripts/Stats/StatsWorld.cs` — server-side state
- `Scripts/Stats/StatsServer.cs` — XP hub, 9 WorldEventBus подписок
- `Scripts/Stats/StatsClientState.cs` — клиентская проекция
- `Scripts/Stats/StatsConfig.cs` — (устарел, split в v4)
- `Scripts/Stats/ExperienceConfig.cs` — per-source XP + tier formula
- `Scripts/Stats/StatSourceMapConfig.cs` — XpSource → StatType mapping
- `Scripts/Stats/StatDebugConfig.cs` — debug logging
- `Scripts/Stats/HealthConfig.cs` — HP formula
- `Scripts/Stats/XpSource.cs` — enum 10 источников
- `Scripts/Stats/Dto/StatsSnapshotDto.cs` — DTO
- `Scripts/Stats/Persistence/CharacterSaveData.cs`, `EquipmentSave.cs`, `SkillsSave.cs`
- `Scripts/Stats/Persistence/JsonCharacterDataRepository.cs`

**Skills:**
- `Scripts/Skills/SkillNodeConfig.cs` — SO (343 строки)
- `Scripts/Skills/SkillEffect.cs` — struct
- `Scripts/Skills/SkillsServer.cs` — NetworkBehaviour RPC hub
- `Scripts/Skills/SkillsWorld.cs` — POCO singleton
- `Scripts/Skills/SkillsConfig.cs` — SO config
- `Scripts/Skills/SkillsClientState.cs` — client projection
- `Scripts/Skills/SkillManager.cs` — удалён (логика в SkillsWorld)
- `Scripts/Skills/SkillAnimationPlayer.cs` — AOC integration
- `Scripts/Skills/SkillInputService.cs` — client activation
- `Scripts/Skills/SkillAnimationEventPassthrough.cs` — animation events
- `Scripts/Skills/UI/SkillTreeWindow.cs` — UI Toolkit window
- `Scripts/Skills/Vfx/ISkillVfxProvider.cs`, `SkillVfxService.cs` — VFX
- `Scripts/Skills/Dto/SkillsDto.cs` — DTO
- `Scripts/Skills/Debug/SkillAoeDebugVisualizer.cs` — debug viz

### 9.5 Что открыто

| # | Задача | Приоритет |
|---|--------|-----------|
| 1 | **Global character levels** (1-50 с milestone unlocks) — заменено на per-stat tiers | 🟢 Backlog |
| 2 | **Prestige System** (reset + PP) | 🟢 Low |
| 3 | **Milestone unlocks** (new ships, contracts, zones by tier) | 🟡 Med |
| 4 | **Co-op XP distribution** (shared party XP) | 🟢 Low |
| 5 | **Achievement system** (статы/трекинг) | 🟢 Low |
| 6 | **Per-active skill cooldown tracking** в HUD | 🟡 Med |

---

## 10. Глоссарий

| Термин | Определение |
|--------|-------------|
| StatBucket | struct: tier + xp в tier + totalXp для одной характеристики |
| Tier | Уровень характеристики (STR tier 5 = combatStat 35) |
| ApplyXp | Центральная функция начисления XP с tier promotion loop |
| SkillNodeConfig | SO — узел графа навыков |
| SkillEffect | struct — эффект изучения навыка (StatMod, unlock и т.п.) |
| CombatDiscipline | Melee/Ranged/Defense/Placed/Combat |
| AOC | AnimatorOverrideController — смена анимаций под навык |
| Free Respec | Бесплатное перераспределение (Q3.4) |

---

*Документ создан для Project C: The Clouds.*
