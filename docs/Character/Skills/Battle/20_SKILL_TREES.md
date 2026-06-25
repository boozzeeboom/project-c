# Skill Trees — 5 дисциплин combat-навыков

> **Дата:** 2026-06-25 (v0.2 — обновлено под вариант B: ERPR-пакет)
> **Базируется на:** `10_DESIGN.md` (5 дисциплин, 5 новых SkillEffect.Type, ERPR damage-формула), `ERPR_collaboration.md`
> **Подход:** каждая дисциплина = самостоятельная ветка Combat-таба. Навыки упорядочены в **2-3 тира** по сложности (basic → advanced → master). DAG-prereq внутри ветки. Кросс-веточные prereq — где это естественно (например, antigrav-клинок требует basic sword). **ERPR-пакет:** каждый навык имеет damage dice/crit/armor параметры (см. §0.1).
> **Что внутри:** skillId, displayName, prereq[], effects[], XP cost, INT tier req, **damage-параметры (ERPR)**, обоснование.

---

## 0. Корневые generic-combat навыки (из T-P11)

Эти 4 навыка — **уже созданы** (см. `06_SKILL_TREE.md §1.3`) и остаются **обязательным фундаментом** для всех combat-веток. `discipline = None` (default) — попадают в фильтр «All» внутри combat-sub-tab (T-CB09).

| skillId | Prereq | Effects | XP | INT tier | Damage-параметры (ERPR) |
|---|---|---|---|---|---|
| `Skill_Combat_BasicStrike` | none | StatMod(STR+2) | 0 | 0 | — (без оружия: урон 1d4+STR, d4 кулак) |
| `Skill_Combat_DodgeRoll` | none | StatMod(DEX+3) | 0 | 0 | — (defensive, no damage) |
| `Skill_Combat_HeavySwing` | BasicStrike | StatMod(STR+5, ×1.2) | 100 | 2 | skillMult ×1.2 на атаки (для любого оружия) |
| `Skill_Combat_PrecisionStrike` | DodgeRoll, HeavySwing | StatMod(DEX+5, ×1.3) | 200 | 4 | skillMult ×1.3 + +15% шанс Head hit location (Phase 2) |

**Вердикт:** **сохраняем** как есть. Не рефакторим. Деревья 5 дисциплин **дополняют**, не заменяют. **ERPR-совместимы:** `HeavySwing/PrecisionStrike` действуют как `skillMult` в damage-формуле (`10_DESIGN.md §7`).

---

## 0.1 ERPR-пакет в навыках (как применяется)

> **Источник:** `10_DESIGN.md §7` (полная формула), `ERPR_collaboration.md §3`.

В навыке **нет** своего `damageDice` или `baseDamage` — это **свойства оружия** (в `WeaponItemData`). Навык **модифицирует** урон через:

1. **skillMult** — `StatMod` effect с `multiplier > 0` (например, `HeavySwing` ×1.2 → `skillMult = 1.2`)
2. **critModifier** (опционально) — `StatMod` с `multiplier = 0`, но эффект на `crit` (Phase 2, требует Combat-движок)
3. **hitLocation bias** (Phase 2) — `WeaponTechniqueUnlock` с `stringParam = "aimed_shot"` → +15% Head
4. **proficiency gate** — `WeaponProficiencyUnlock` с `stringParam = "sword"` → позволяет надеть `Weapon_SteelSword` (d6, base=3)
5. **armor gate** — `ArmorProficiencyUnlock` с `stringParam = "heavy"` → позволяет надеть `Clothing_SteelChestplate` (armor=8)

**Пример полного flow (упрощённо):**
```
Игрок: Skill_Combat_HeavySwing (×1.2) + Skill_Melee_GreatSword (proficiency GreatSword, ×1.15)
      + экипировка: Weapon_GreatSword_Antigrav (d10, base=4, critMod=+10)
      + экипировка: Clothing_SteelChestplate (armor=8) + WorkerHelmet (armor=2)

Атака: target — NPC в лёгкой броне (armor=2)
  roll d10 = 7, baseAttack = 7 + 4 + 10 (STR) = 21
  locRoll d4 = 4 (Head!) → locMult = 2.0
  critRoll d100 = 95 + 10 = 105 → critMult = 2.0
  skillMult = 1.2 × 1.15 = 1.38
  final = round(21 × 2.0 × 2.0 × 1.38) = 116
  effective_defense = 2 × 0.5 (Antigrav vs armor) = 1
  final = 116 - 1 = 115
```

---

## 1. Ветка Melee (ближний бой, холодное оружие)

**Дисциплина:** `CombatDiscipline.Melee`
**Источник XP для learn:** Intelligence pool (как и все навыки)
**Gate:** `RequiredIntelligenceTier` на продвинутых узлах
**Корневой prereq (опц.):** `Skill_Combat_BasicStrike` (для узлов с физ. уроном)

### 1.1 Дерево (7 навыков, 2 тира + 1 утилита)

```
[Tier 0, free]                                  [Tier 0, free]
BasicStrike (root, T-P11)                       DodgeRoll (root, T-P11)
        │                                                │
        ▼                                                ▼
[Tier 1]                            [Tier 1]   [Tier 1]
melee_basic_sword ───────────────► melee_heavy_swing (T-P11, basic)
(XP 0)                                       │
  │ effects:                                  ▼
  │   WeaponProficiencyUnlock("sword")   [Tier 2]
  │   StatMod(STR+1)                     melee_great_sword
  │                                       (XP 100, INT tier 2)
  │                                       prereq: melee_basic_sword
  │                                       effects:
  │                                         WeaponProficiencyUnlock("great_sword")
  │                                         StatMod(STR+3, ×1.15)
  │
  ├──► melee_basic_dagger (XP 0)
  │    effects: WeaponProficiencyUnlock("dagger"), StatMod(DEX+2)
  │
  ├──► melee_basic_spear (XP 0)
  │    effects: WeaponProficiencyUnlock("spear"), StatMod(STR+1, DEX+1)
  │
  └──► melee_basic_mace (XP 0)
       effects: WeaponProficiencyUnlock("mace"), StatMod(STR+2)

[Tier 2 — техники]
melee_parry (XP 100, INT tier 2)
  prereq: melee_basic_sword (или dagger, или spear) — любое
  effects:
    WeaponTechniqueUnlock("parry")
    StatMod(DEX+2)

melee_riposte (XP 150, INT tier 3)
  prereq: melee_parry
  effects:
    WeaponTechniqueUnlock("riposte")
    StatMod(STR+2, DEX+2)

melee_precision_strike (XP 200, INT tier 4)
  prereq: melee_basic_sword, melee_parry
  effects:
    WeaponTechniqueUnlock("precision_strike")
    StatMod(DEX+5)
    (перенесён в подветку как master-tier вариант)
```

### 1.1.1 Damage-параметры Melee-навыков (ERPR)

| skillId | proficiency (открывает оружие) | skillMult | Crit-эффект | Hit-Loc bias (Phase 2) |
|---|---|---|---|---|
| `melee_basic_sword` | `sword` (d6, base 3) | ×1.0 | — | — |
| `melee_basic_dagger` | `dagger` (d4, base 2) | ×1.0 | — | +10% Limbs (быстрый, бьёт по рукам) |
| `melee_basic_spear` | `spear` (d8, base 3, range 3m) | ×1.0 | — | — |
| `melee_basic_mace` | `mace` (d8, base 3) | ×1.0 | — | +5% Torso (blunt, anti-armor) |
| `melee_great_sword` | `great_sword` (d10, base 4) | **×1.15** | — | — |
| `melee_parry` | — | — | — | техника (не урон) |
| `melee_riposte` | — | — | — | техника (контратака) |
| `melee_precision_strike` | — | — | — | **+20% Head** (мастер-удар) |

**Лор-обоснование ERPR damage dice:**
- **Кинжал** d4 — быстрый, лёгкий, off-hand. Меньше урона, но быстрее.
- **Меч** d6 — базовый, универсальный.
- **Копьё** d8 — reach (дистанция 3м), anti-cavalry.
- **Булава** d8 — blunt, эффективнее против брони (anti-armor).
- **Двуручник** d10 — медленный, тяжёлый, максимальный урон.

### 1.2 Почему именно так

- **Basic skills бесплатны (XP 0)** — игрок сразу может надеть базовое оружие (меч/кинжал/копьё/булава). Это **онбординг** combat.
- **Каждое оружие — отдельный skill** (sword ≠ dagger ≠ spear). Логика: **владение разными классами — разные навыки**. Базовый меч ≠ двуручный меч (great_sword требует отдельного навыка).
- **Техники (parry, riposte)** — отдельные навыки, не привязаны к оружию (parry = общее). Конкретное оружие даёт бонус к технике через `weaponClass.weaponTechniqueAffinity` (Phase 3, combat-движок).
- **PrecisionStrike** из T-P11 пересмотрен: остаётся как **generic StatMod** (он бесплатный/корневой), а **master precision** вариант становится отдельным skill `melee_precision_strike` (требует mastery оружия + parry). Это устраняет противоречие T-P11 (`PrecisionStrike` = DEX+5 generic vs нужен «для мастера»).

### 1.3 Пример конкретного .asset

```yaml
# Skill_Combat_Melee_BasicSword.asset
skillId: "melee_basic_sword"
displayName: "Владение мечом"
description: "Базовое владение одноручным мечом. Открывает экипировку мечей."
category: Combat          # 1
discipline: Melee         # 1 (NEW)
prerequisites:
  - Skill_Combat_BasicStrike.asset
effects:
  - { type: WeaponProficiencyUnlock, stringParam: "sword" }
  - { type: StatMod, statType: Strength, floatValue: 1.0 }
_learnXpCost: 0
_requiredIntelligenceTier: 0
treeX: 100
treeY: 100
```

---

## 2. Ветка Ranged (стрелковое)

**Дисциплина:** `CombatDiscipline.Ranged`
**Корневой prereq:** `Skill_Combat_DodgeRoll` (DEX-ориентирован)

### 2.1 Дерево (6 навыков, 2 тира)

```
[Tier 0, free]
ranged_basic_crossbow
  effects:
    WeaponProficiencyUnlock("crossbow")
    StatMod(DEX+2)
  prereq: DodgeRoll (T-P11)

ranged_basic_pneumatic
  effects:
    WeaponProficiencyUnlock("pneumatic")
    StatMod(DEX+2, INT+1)
  prereq: DodgeRoll

[Tier 1, INT tier 2]
ranged_aimed_shot (XP 100, INT tier 2)
  prereq: ranged_basic_crossbow OR ranged_basic_pneumatic
  effects:
    WeaponTechniqueUnlock("aimed_shot")
    StatMod(DEX+3)

ranged_quick_reload (XP 80, INT tier 1)
  prereq: ranged_basic_crossbow OR ranged_basic_pneumatic
  effects:
    WeaponTechniqueUnlock("quick_reload")
    StatMod(DEX+2)

[Tier 2, advanced — мезиевое]
ranged_mesium_propulsion (XP 200, INT tier 4)
  prereq: ranged_basic_pneumatic, INT tier 4
  effects:
    WeaponProficiencyUnlock("mesium_rifle")
    StatMod(DEX+3, INT+2)

ranged_mesium_mastery (XP 300, INT tier 5)
  prereq: ranged_mesium_propulsion, ranged_aimed_shot
  effects:
    WeaponTechniqueUnlock("mesium_burst")
    StatMod(DEX+5, INT+3)
```

### 2.1.1 Damage-параметры Ranged-навыков (ERPR)

| skillId | proficiency (открывает оружие) | skillMult | Crit-эффект | Hit-Loc bias (Phase 2) | range |
|---|---|---|---|---|---|
| `ranged_basic_crossbow` | `crossbow` (d8, base 4, **critMod +5**) | ×1.0 | — | — | 30м |
| `ranged_basic_pneumatic` | `pneumatic` (d10, base 4) | ×1.0 | — | — | 50м |
| `ranged_aimed_shot` | — | — | — | **+15% Head** (прицельный) | — |
| `ranged_quick_reload` | — | — | — | техника (перезарядка) | — |
| `ranged_mesium_propulsion` | `mesium_rifle` (d12, base 5, **critMod +5**) | ×1.0 | — | — | 100м |
| `ranged_mesium_mastery` | — | — | техника: `crit × 3` (вместо ×2) | — | — |

**Лор-обоснование ERPR damage dice:**
- **Арбалет** d8 — механический, болт-снаряд. critMod +5 (хорошая начальная скорость болта).
- **Пневматика** d10 — сжатый воздух, выше скорость, но без critMod (воздух «рыхлый»).
- **Мезиевое** d12 — продвинутое, опасное, critMod +5 (газ нестабилен, может детонировать).
- **range** — в метрах, в turn-based пересчитывается в клетки (1 кл = 2м).

### 2.2 Особенности

- **Crossbow и Pneumatic — параллельные** (не prereq друг друга). Игрок выбирает стиль.
- **Мезиевое** — отдельная подветка с prereq = Pneumatic (пневматика = технологическая база для мезиевого стрелкового). Crossbow не подходит как prereq (механика, не газовая среда).
- **Только DEX/INT-статы** — никакого STR (стрелковое не требует физической силы в лоре).

### 2.3 Lore-обоснование

- **Арбалет** — массовое, механическое, не требует пороха → basic.
- **Пневматика** — средне-доступное, требует баллоны со сжатым воздухом → basic (чуть выше требования).
- **Мезиевое** — продвинутое, опасное, требует знание физики газа → INT tier 4, INT stat-bonus.

---

## 3. Ветка Explosives (взрывчатка)

**Дисциплина:** `CombatDiscipline.Explosives`
**Корневой prereq:** `Skill_Combat_BasicStrike` (физ. бросок) **или** `ranged_basic_crossbow` (для дальнобойных мин)

### 3.1 Дерево (4 навыка, 2 тира)

```
[Tier 0, free]
explosives_basic_grenade
  effects:
    ExplosiveRecipeUnlock("recipe_grenade_basic")
    StatMod(DEX+1)
  prereq: none (доступно всем)

[Tier 1, INT tier 2]
explosives_mine_setting (XP 100, INT tier 2)
  prereq: explosives_basic_grenade
  effects:
    ExplosiveRecipeUnlock("recipe_mine_basic")
    WeaponTechniqueUnlock("place_mine")

explosives_charge_crafting (XP 150, INT tier 3)
  prereq: explosives_basic_grenade
  effects:
    ExplosiveRecipeUnlock("recipe_charge_basic")
    (для подрыва дверей/стен, не combat)

[Tier 2, advanced — антигравий]
explosives_antigrav_mine (XP 200, INT tier 4)
  prereq: explosives_mine_setting, antigrav_basic (см. §4)
  effects:
    ExplosiveRecipeUnlock("recipe_mine_antigrav")
    StatMod(INT+3)

explosives_antigrav_grenade (XP 250, INT tier 4)
  prereq: explosives_basic_grenade, antigrav_basic
  effects:
    ExplosiveRecipeUnlock("recipe_grenade_antigrav")
    StatMod(INT+3, DEX+2)
```

### 3.1.1 Damage-параметры Explosives (ERPR)

| skillId | recipe (открывает крафт) | AoE-радиус (м) | damageType | Crit-эффект | special |
|---|---|---|---|---|---|
| `explosives_basic_grenade` | `grenade_basic` (d10, base 5, radius 3м) | 3м | Explosive | — | — |
| `explosives_mine_setting` | `mine_basic` (d12, base 6, radius 5м) | 5м | Explosive | — | техника: place_mine |
| `explosives_charge_crafting` | `charge_basic` (не combat) | — | — | — | для подрыва дверей/стен |
| `explosives_antigrav_mine` | `mine_antigrav` (d8, base 4, **critMod +15**) | 5м | **Antigrav** | — | зона g-воздействия, не взрыв |
| `explosives_antigrav_grenade` | `grenade_antigrav` (d8, base 4, **critMod +15**) | 3м | **Antigrav** | — | зона g-воздействия |

**Лор-обоснование ERPR damage dice:**
- **Граната** d10 — взрыв, урон по площади. critMod 0 (взрыв стабилен).
- **Мина** d12 — больше урона, радиус больше. critMod 0.
- **Антиграв-мина** d8 + critMod +15 — НЕ взрыв, а зона g-воздействия (легче/тяжелее). critMod +15 потому что g-волна нестабильна, часто попадает в уязвимости.

### 3.2 Особенности

- **Grenade — стартовый** (XP 0, никаких prereq) — это метательное, базовое.
- **Mine и Charge** — INT tier 2-3, требует grenade как prereq.
- **Antigrav-взрывчатка** — cross-tree prereq с Antigrav-веткой (`antigrav_basic`). Логика: «нельзя сделать антиграв-мину, не понимая физики антигравия».
- **Stat-bonuses** — DEX для метания, INT для тактики установки.

### 3.3 Lore-обоснование

- **Гранаты** — базовые, не требуют особых знаний, есть у каждого бойца.
- **Мины** — требуют знание тактики (где ставить, как маскировать).
- **Антиграв-мины/гранаты** — продвинутые, требуют знания антиграв-физики → prereq из Antigrav-ветки.

---

## 4. Ветка Antigrav (гравитационное)

**Дисциплина:** `CombatDiscipline.Antigrav`
**Корневой prereq:** **нет** (INT-ориентированная, доступна сразу)
**Особенность:** это **самая редкая и дорогая** ветка — антигравий дефицитен, оружие/устройства дорогие. Навыки — это «право использовать дорогие устройства».

### 4.1 Дерево (5 навыков, 2 тира)

```
[Tier 0, free]
antigrav_basic
  effects:
    StatMod(INT+2)
    (без proficiency — это "теоретическая база")
  prereq: none
  description: "Базовое понимание антигравитационной физики. Необходимо для antigrav-устройств."

[Tier 1, INT tier 2]
antigrav_blade_wield (XP 150, INT tier 2)
  prereq: antigrav_basic
  effects:
    WeaponProficiencyUnlock("antigrav_blade")
    StatMod(STR+2, INT+1)

antigrav_hammer_wield (XP 150, INT tier 2)
  prereq: antigrav_basic
  effects:
    WeaponProficiencyUnlock("antigrav_hammer")
    StatMod(STR+3, INT+1)

antigrav_push (XP 200, INT tier 3)
  prereq: antigrav_basic
  effects:
    AntigravTechniqueUnlock("grav_push")
    (техника: толчок от себя, AoE-g-волна)
    StatMod(INT+3)

[Tier 2, advanced]
antigrav_pull (XP 250, INT tier 4)
  prereq: antigrav_push
  effects:
    AntigravTechniqueUnlock("grav_pull")
    (техника: притяжение к себе)
    StatMod(STR+2, INT+2)

antigrav_aura (XP 300, INT tier 5)
  prereq: antigrav_blade_wield OR antigrav_hammer_wield, antigrav_push
  effects:
    AntigravTechniqueUnlock("anti_gravity_aura")
    (пассивная аура вокруг игрока, отталкивает g-волны)
    StatMod(INT+5)
```

### 4.1.1 Damage-параметры Antigrav-навыков (ERPR)

| skillId | proficiency (открывает оружие) | skillMult | Crit-эффект | Hit-Loc bias (Phase 2) |
|---|---|---|---|---|
| `antigrav_basic` | — | — | — | — (теория) |
| `antigrav_blade_wield` | `antigrav_blade` (d8, base 3, **critMod +10**) | ×1.0 | — | +5% Head (g-волна «притягивает» к уязвимости) |
| `antigrav_hammer_wield` | `antigrav_hammer` (d10, base 4, **critMod +10**) | ×1.0 | — | +5% Torso (g-удар отбрасывает) |
| `antigrav_push` | — | — | — | техника: AoE 3м, отталкивание (g-волна) |
| `antigrav_pull` | — | — | — | техника: AoE 3м, притяжение |
| `antigrav_aura` | — | — | — | пассив: ×1.5 урон в g-зоне вокруг игрока |

**Лор-обоснование ERPR damage dice:**
- **Антиграв-клинок** d8 + critMod +10 — локальная g-аномалия, броня частично игнорируется (armorMult 0.5 в `10_DESIGN.md §7.2`).
- **Антиграв-молот** d10 + critMod +10 — тяжёлый g-импульс, отбрасывает цель.
- **critMod +10 на оба** — g-поле «притягивает» удар к уязвимости (lore-обоснование, см. `02_LORE.md §4.3`).

### 4.4 Open question

> См. `30_PITFALLS_AND_OPEN_QUESTIONS.md §2.4`: подтвердить, что в lore существует «антиграв-щит» (если да — добавить `antigrav_shield_wield` в Tier 1).
- **Blade vs Hammer** — параллельные стили ближнего боя с антиграв-эффектом. Разные stat-bias (Blade = STR+2/INT+1, Hammer = STR+3/INT+1).
- **Push / Pull** — техники дальнего действия (без оружия), манипуляция g-полем вокруг игрока.
- **Aura** — пассивная, требует mastery оружия + push.

### 4.3 Lore-обоснование

- **Базовая теория** — без неё нельзя безопасно обращаться с антиграв-устройствами (риск выхода из-под контроля).
- **Blade/Hammer** — антиграв-металл как холодное оружие: при контакте создаёт локальную g-аномалию (лёгкость для владельца, тяжесть для цели). Разница Blade/Hammer — стиль и stat-bias.
- **Push/Pull/Aura** — манипуляция g-полем без оружия. Требует высокого INT (физика гравитации).

### 4.4 Open question

> См. `30_PITFALLS_AND_OPEN_QUESTIONS.md §2.4`: подтвердить, что в lore существует «антиграв-щит» (если да — добавить `antigrav_shield_wield` в Tier 1).

---

## 5. Ветка Defense (защита, броня, щиты)

**Дисциплина:** `CombatDiscipline.Defense`
**Корневой prereq:** `Skill_Combat_BasicStrike` (общая готовность к бою)

### 5.1 Дерево (5 навыков, 2 тира)

```
[Tier 0, free]
defense_basic_stance
  effects:
    StatMod(STR+1)
    (базовая оборонительная стойка)
  prereq: BasicStrike

[Tier 1, INT tier 1]
defense_light_armor (XP 50, INT tier 1)
  prereq: defense_basic_stance
  effects:
    ArmorProficiencyUnlock("light")
    StatMod(DEX+1)
  description: "Ношение лёгкой брони (тряпки, кожа)."

defense_medium_armor (XP 100, INT tier 2)
  prereq: defense_basic_stance, defense_light_armor
  effects:
    ArmorProficiencyUnlock("medium")
    StatMod(STR+1)
  description: "Ношение средней брони (chain, plate-части)."

defense_shield_wield (XP 100, INT tier 2)
  prereq: defense_basic_stance
  effects:
    ArmorProficiencyUnlock("shield")
    WeaponTechniqueUnlock("shield_bash")
    StatMod(STR+2)

[Tier 2, advanced]
defense_heavy_armor (XP 200, INT tier 3)
  prereq: defense_medium_armor
  effects:
    ArmorProficiencyUnlock("heavy")
    StatMod(STR+3)
    (штраф DEX -2, баланс)

defense_master_defender (XP 300, INT tier 5)
  prereq: defense_heavy_armor, melee_parry (из §1)
  effects:
    WeaponTechniqueUnlock("counter_strike")
    StatMod(STR+3, DEX+2)
```

### 5.1.1 Defense-параметры (ERPR — armorDefense)

| skillId | proficiency (открывает броню) | armorBonus (skillMult на суммарную броню) | statMod | Спецэффект |
|---|---|---|---|---|
| `defense_basic_stance` | — | — | STR+1 | стойка, +5% effective defense |
| `defense_light_armor` | `light` (armorDefense ≤ 5) | ×1.0 | DEX+1 | без штрафов |
| `defense_medium_armor` | `medium` (armorDefense 5-15) | ×1.0 | STR+1 | без штрафов |
| `defense_shield_wield` | `shield` (off-hand item, +armor 3-5) | ×1.0 | STR+2 | техника: shield_bash (контрудар) |
| `defense_heavy_armor` | `heavy` (armorDefense 15+) | ×1.0 | STR+3, **DEX-2** | штраф DEX за массу |
| `defense_master_defender` | — | **×1.2** (effective defense +20%) | STR+3, DEX+2 | техника: counter_strike + parry + heavy |

**Defense-формула (ERPR, `10_DESIGN.md §7.2`):**
```
totalArmor = sum(armorDefense по экипированным Clothing в armor-slots)
effective_defense = (totalArmor + skillBonus) × typeMultiplier
```
- `typeMultiplier`: Physical/Ballistic=1.0, Antigrav=0.5, Explosive=0.7, Mesium=0.0.
- `skillBonus` — бонус от `defense_master_defender` (+20% = ×1.2).

**Пример:**
- Игрок в SteelChestplate (armor=8) + WorkerHelmet (armor=2) + TravelerBoots (armor=1) = 11
- Изучен `defense_heavy_armor` (proficiency) + `defense_master_defender` (+20% skill bonus)
- Атака с Antigrav-клинка: `effective_defense = 11 × 1.2 × 0.5 = 6.6 → 7`
- Атака с обычного меча: `effective_defense = 11 × 1.2 × 1.0 = 13.2 → 13`

### 5.2 Особенности

- **Прогрессия брони** — Light → Medium → Heavy, каждая требует предыдущую. Это **forced vertical progression** (DAG-линейная цепочка внутри категории брони).
- **Shield** — параллельная ветка (щит в Back-slot, не body).
- **Heavy armor — штраф DEX** (предложение: StatMod(DEX, -2)) — отражает физический штраф тяжёлой брони. **Открытое решение** для пользователя (`30_PITFALLS_AND_OPEN_QUESTIONS.md §2.5`).
- **Master defender** — cross-prereq с Melee (требует parry), превращает защитника в мастера «блок + контратака».

### 5.3 Lore-обоснование

- **Лёгкая броня** — кожа, тряпки, банданы. Любой может носить, не требует подготовки.
- **Средняя броня** — chain mail, частичный plate. Требует привычки (training).
- **Тяжёлая броня** — полный plate, шлем. Требует физической силы и выносливости.
- **Щит** — отдельный класс защитного снаряжения, не зависит от body-брони (можно со щитом в средней/тяжёлой).

---

## 6. Кросс-веточные prereq (DAG)

| Skill | Prereq из другой ветки | Обоснование |
|---|---|---|
| `explosives_antigrav_mine` | `antigrav_basic` | Нельзя сделать антиграв-мину, не понимая физику |
| `explosives_antigrav_grenade` | `antigrav_basic` | То же |
| `defense_master_defender` | `melee_parry` | Master defender = melee parry + heavy armor |
| `melee_precision_strike` | `melee_parry` | Точный удар требует parry как базу |

**DAG-check:** все 4 кросс-ссылки **в одну сторону** (Antigrav → Explosives, Melee → Defense), нет циклов. `SkillNodeConfig.OnValidate` (T-P11) их поймает если что.

---

## 7. Сводная таблица: сколько нод в каждой ветке (с ERPR damage dice)

| Ветка | Tier 0 (free) | Tier 1 | Tier 2 (master) | Всего | ERPR damage dice (характерные) |
|---|---|---|---|---|---|
| **Generic roots (T-P11)** | 4 (BasicStrike, DodgeRoll, HeavySwing, PrecisionStrike) | — | — | 4 | — (без оружия / no-skill) |
| **Melee** | 4 (basic_sword, basic_dagger, basic_spear, basic_mace) | 2 (parry, riposte) | 2 (great_sword, precision_strike) | 8 | d4-d10 (кинжал-двуручник) |
| **Ranged** | 2 (basic_crossbow, basic_pneumatic) | 2 (aimed_shot, quick_reload) | 2 (mesium_propulsion, mesium_mastery) | 6 | d8-d12 (арбалет-мезиевое) |
| **Explosives** | 1 (basic_grenade) | 2 (mine_setting, charge_crafting) | 2 (antigrav_mine, antigrav_grenade) | 5 | d10-d12 + antigrav d8+critMod+15 |
| **Antigrav** | 1 (basic) | 3 (blade, hammer, push) | 2 (pull, aura) | 6 | d8-d10 + critMod+10 |
| **Defense** | 1 (basic_stance) | 3 (light, medium, shield) | 2 (heavy, master_defender) | 6 | armorDefense stat, не damage dice |
| **ВСЕГО combat** | 9 | 12 | 10 | **31** | — |

**+ 4 generic roots (T-P11) = 35 combat-навыков в полном наборе.**

**Средний урон по дисциплинам (ERPR-расчёт, см. `10_DESIGN.md §7.5`):**
- **Melee (меч d6 + heavy_swing ×1.2)**: ~17.4 базовый (без защиты, head 25%, crit 1%)
- **Ranged (арбалет d8 + aimed_shot)**): ~22.5 базовый (range 30м)
- **Explosives (граната d10)**: ~16 базовый + AoE 3м
- **Antigrav (клинок d8 + critMod+10)**: ~16 базовый + armorMult 0.5
- **Defense (SteelChestplate 8)**: блокирует 8 от physical, 4 от antigrav

---

## 8. Баланс (предложение для пользователя)

| Дисциплина | Начальная стоимость (Tier 0-1) | Продвинутая (Tier 2) | XP cost прогрессия |
|---|---|---|---|
| Melee | 0-100 | 150-300 | линейная 0/100/200 (Q3.3a) |
| Ranged | 0-100 | 200-300 | такая же |
| Explosives | 0-150 | 200-250 | чуть дороже (1 рецепт = деньги) |
| Antigrav | 0-200 | 250-300 | **самая дорогая** (материал дефицитен) |
| Defense | 0-100 | 200-300 | линейная |

**Все XP-cost настраиваемые** через `SkillNodeConfig._learnXpCost` (Q3.3a, не hardcode).

---

## 9. Что НЕ делаем в этом диздоке (явные запреты)

- ❌ Не проектируем конкретные уроны/бронепробития (combat-движок)
- ❌ Не проектируем VFX-эффекты (3D-отдел)
- ❌ Не пишем .asset-файлы (T-CB08)
- ❌ Не пишем код (T-CB01..T-CB09 — будущие сессии)
- ❌ Не вводим Skill Point / Level (GDD 20 фича)
- ❌ Не делаем prestige / respec с возвратом XP (Q3.4 = free respec без возврата)
