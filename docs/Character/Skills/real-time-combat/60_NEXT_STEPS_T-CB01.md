# Next Steps — T-CB01..T-CB09 (навыки + skill hook)

> **Статус:** план для следующей сессии. Combat MVP (T-RTC01..T-RTC09) реализован, end-to-end работает. Навыки = MVP+1, opt-in через hook `IDamageSource.GetSkillMultiplier(attackerId)`.
> **Цель:** подключить `SkillsWorld` к combat-движку через `WeaponDamageSource.GetSkillMultiplier`. Без изменений в `CombatServer`/`DamageCalculator`/interfaces.
> **Оценка:** ~16-21 ч (2-3 сессии).

---

## TL;DR

В combat-движке **уже есть hook** для навыков: `IDamageSource.GetSkillMultiplier(ulong attackerId)`. Сейчас `DefaultDamageSource.GetSkillMultiplier` возвращает `1.0f` (MVP). После T-CB01..T-CB09 — читает `SkillsWorld.GetLearnedSkills(attackerId)`, проходит по `SkillNodeConfig.effects`, накапливает `mult *= eff.multiplier` (без cap, per 2.18).

**Что нужно для подключения навыков к combat:**

1. **T-CB03** (NEW): `WeaponItemData` (SO, extends `ItemData`) с 3 ERPR-полями: `damageDice`, `baseDamage`, `critModifier`. После этого `WeaponDamageSource : IDamageSource` заменяет `DefaultDamageSource` (адаптер с реальными полями).

2. **T-CB06** (NEW): `armorDefense` поле в `ClothingItemData`. После этого `PlayerTarget.GetArmorDefense()` суммирует armor из экипированной одежды.

3. **T-CB07** (NEW): `WeaponDamageSource.GetSkillMultiplier(attackerId)` — читает `SkillsWorld.GetLearnedSkills(attackerId)`, накапливает mult из StatMod effects. **Это главный hook.**

4. **T-CB08** (NEW): 35 .asset файлов с `SkillNodeConfig` + combat-relevant effects (StatMod, AbilityUnlock, WeaponTechnique).

5. **T-CB09** (UI): фильтр по `CombatDiscipline` в CharacterWindow. Не блокер combat.

**Не нужно:**
- ❌ Менять `CombatServer.cs` (уже generic)
- ❌ Менять `DamageCalculator.cs` (уже принимает `IDamageSource`)
- ❌ Менять `IAttacker/IDamageTarget/IDamageSource` interfaces
- ❌ Менять `MeleeRangePolicy/RangedRangePolicy` (они не зависят от навыков)

---

## 1. Что УЖЕ есть в коде (для T-CB01..09)

### 1.1 Skills инфраструктура (T-P11..T-P13, готова)

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` (98 строк) — `public enum SkillCategory { Combat=... }` + public fields (`_learnXpCost`, `_requiredIntelligenceTier`) + `OnValidate` cycle detection.
- `Assets/_Project/Scripts/Skills/SkillsWorld.cs` — `LoadAllSkills`, `GetLearnedSkillIds(clientId)`, `GetSkillConfig(skillId)`.
- `Assets/_Project/Scripts/Skills/SkillsClientState.cs` — singleton + `OnSkillsUpdated` event.
- `Assets/_Project/Scripts/Skills/SkillEffect.cs` — struct с `enum Type { StatMod=0, AbilityUnlock=1, PassiveEffect=2, ... }`.

**API для combat-движка (T-CB07 hook):**
```csharp
var learned = SkillsWorld.Instance.GetLearnedSkills(attackerId);
foreach (var skillId in learned) {
    var skill = SkillsWorld.Instance.GetSkillConfig(skillId);
    if (skill == null) continue;
    foreach (var eff in skill.effects) {
        if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
            mult *= eff.multiplier;  // без cap, per 2.18
        }
    }
}
```

### 1.2 Equipment (T-P07..T-P09, готова)

**Файлы:**
- `Assets/_Project/Scripts/Equipment/EquipSlot.cs` — enum (Head..WeaponOff=1..10, Module1..3=20..22).
- `Assets/_Project/Scripts/Equipment/EquipmentData.cs` — parallel arrays `byte[13] slotOccupied` + `int[13] slotItemIds`. API: `TryGetItemId(slot, out itemId)`, `IsSlotOccupied(slot)`, `SetItem/ClearSlot`.
- `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` — singleton, `GetEquipment(clientId)`, `GetEquipStatBonuses(...)`, `TryEquip/TryUnequip`.
- `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` (64 строки) — extends `ItemData`, fields: `slot`, `tier`, `strengthBonus/dexterityBonus/intelligenceBonus`, `strengthMultiplier/dexterityMultiplier/intelligenceMultiplier`, `requiredSkills[]`. **НЕТ** `armorDefense` поля (T-CB06).
- `Assets/_Project/Scripts/Equipment/ModuleItemData.cs` — аналогично для модулей.

**НЕТ** `WeaponItemData.cs` — это T-CB03.

### 1.3 Stats (T-P01..T-P06, готова)

- `Assets/_Project/Scripts/Stats/StatsConfig.cs` — SO, `_tierBaseXp`, `_tierGrowthRate`, `XpForNextTier`.
- `Assets/_Project/Scripts/Stats/PlayerStats.cs` — `struct { float strength, dexterity, intelligence; int strengthTier, dexterityTier, intelligenceTier; ... }`. Default = 0/0/0.
- `Assets/_Project/Scripts/Stats/StatsWorld.cs` — singleton, `GetOrCreateStats(clientId)`.
- `PlayerAttacker.GetStrength/Dexterity/Intelligence` → `tier*5+10` (default 10). **Готово.**

### 1.4 Inventory

- `Assets/_Project/Items/Core/InventoryWorld.cs` — singleton, `GetItemDefinition(int id) → ItemData`, `HasItem`, `CountOf`. **Готова.**

### 1.5 Combat Hook (готов, в коде)

`IDamageSource.GetSkillMultiplier(ulong attackerId)`:
```csharp
// В Core/IDamageSource.cs (interface)
float GetSkillMultiplier(ulong attackerId);
```

**Текущая реализация** (в `DefaultDamageSource`):
```csharp
public float GetSkillMultiplier(ulong attackerId) => 1.0f;  // MVP: навыки не подключены
```

**После T-CB07** (в новом `WeaponDamageSource`):
```csharp
public float GetSkillMultiplier(ulong attackerId) {
    if (SkillsWorld.Instance == null) return 1.0f;
    var learned = SkillsWorld.Instance.GetLearnedSkills(attackerId);
    if (learned == null || learned.Count == 0) return 1.0f;
    float mult = 1.0f;
    foreach (var skillId in learned) {
        var skill = SkillsWorld.Instance.GetSkillConfig(skillId);
        if (skill == null) continue;
        foreach (var eff in skill.effects) {
            if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
                mult *= eff.multiplier;
            }
        }
    }
    return mult;
}
```

---

## 2. T-CB03: WeaponItemData (ERPR-пакет)

### 2.1 Создать файл

`Assets/_Project/Scripts/Equipment/WeaponItemData.cs`:

```csharp
using UnityEngine;

namespace ProjectC.Equipment
{
    [CreateAssetMenu(fileName = "Weapon_", menuName = "Project C/Equipment/Weapon", order = 12)]
    public class WeaponItemData : ItemData
    {
        [Header("Weapon class")]
        [Tooltip("Sword / Dagger / Spear / Mace / Crossbow / Pneumatic / AntigravBlade / MesiumRifle")]
        public WeaponClass weaponClass = WeaponClass.Sword;

        [Header("ERPR-пакет (T-CB03)")]
        [Tooltip("Damage dice. d4-d20 (ERPR §3.1).")]
        public ProjectC.Combat.Core.DamageDice damageDice = ProjectC.Combat.Core.DamageDice.d6;

        [Tooltip("Базовый урон оружия (без кубика и модификаторов). Формула: " +
                 "final = (STR + 1dN + base) × hitLocation × crit × skillMult.")]
        [Range(0, 50)] public int baseDamage = 1;

        [Tooltip("Crit modifier: 1d100 + critModifier >= 100 → crit ×2.")]
        [Range(-50, 50)] public int critModifier = 0;

        [Header("Range (T-CB04)")]
        [Tooltip("Range в метрах. <3м = melee, ≥3м = ranged.")]
        [Range(0.5f, 200f)] public float range = 2.0f;

        [Header("Damage type (T-CB03)")]
        public ProjectC.Combat.Core.DamageType damageType = ProjectC.Combat.Core.DamageType.Physical;

        [Header("Required skill (T-CB06)")]
        [Tooltip("Минимальный навык для использования (proficiency gate).")]
        public SkillNodeConfig requiredProficiency;

        [Header("Min tier")]
        [Range(0, 10)] public int minTier = 0;
    }

    public enum WeaponClass : byte
    {
        Sword = 0, Dagger = 1, Spear = 2, Mace = 3,
        Crossbow = 4, Pneumatic = 5, AntigravBlade = 6, MesiumRifle = 7,
    }
}
```

### 2.2 OnValidate defaults per weaponClass (per answer 2.14)

```csharp
#if UNITY_EDITOR
private void OnValidate() {
    // Auto-set defaults по weaponClass
    switch (weaponClass) {
        case WeaponClass.Sword: damageDice = DamageDice.d8; baseDamage = 2; range = 2.0f; break;
        case WeaponClass.Dagger: damageDice = DamageDice.d4; baseDamage = 1; range = 1.5f; break;
        case WeaponClass.Spear: damageDice = DamageDice.d10; baseDamage = 3; range = 3.0f; break;
        case WeaponClass.Mace: damageDice = DamageDice.d8; baseDamage = 3; range = 2.0f; break;
        case WeaponClass.Crossbow: damageDice = DamageDice.d10; baseDamage = 4; range = 30.0f; break;
        case WeaponClass.Pneumatic: damageDice = DamageDice.d8; baseDamage = 3; range = 50.0f; break;
        case WeaponClass.AntigravBlade: damageDice = DamageDice.d8; baseDamage = 3; critModifier = 10; range = 2.0f; damageType = DamageType.Antigrav; break;
        case WeaponClass.MesiumRifle: damageDice = DamageDice.d10; baseDamage = 5; range = 50.0f; damageType = DamageType.Mesium; break;
    }
}
#endif
```

### 2.3 Создать WeaponDamageSource

`Assets/_Project/Scripts/Combat/Implementations/WeaponDamageSource.cs`:

```csharp
using ProjectC.Combat.Core;
using ProjectC.Equipment;

namespace ProjectC.Combat
{
    /// <summary>
    /// IDamageSource adapter для WeaponItemData (T-CB03).
    /// После T-CB07 — читает SkillsWorld для skillMult.
    /// </summary>
    public sealed class WeaponDamageSource : IDamageSource
    {
        private readonly WeaponItemData _weapon;
        private readonly ulong _sourceId;

        public WeaponDamageSource(WeaponItemData weapon, ulong sourceId) {
            _weapon = weapon;
            _sourceId = sourceId;
        }

        public ulong GetSourceId() => _sourceId;
        public DamageType GetDamageType() => _weapon.damageType;
        public DamageDice GetDamageDice() => _weapon.damageDice;
        public int GetBaseDamage() => _weapon.baseDamage;
        public int GetCritModifier() => _weapon.critModifier;
        public float GetRange() => _weapon.range;
        public float GetCooldownSeconds() => _weapon.damageDice switch {
            DamageDice.d4 or DamageDice.d6 => 1.0f,
            DamageDice.d8 or DamageDice.d10 => 1.5f,
            DamageDice.d12 or DamageDice.d20 => 2.5f,
            _ => 1.0f,
        };
        public float GetSkillMultiplier(ulong attackerId) {
            // T-CB07 hook — см. 60_NEXT_STEPS_T-CB01.md §1.5
            return 1.0f;  // stub до T-CB07
        }
        public string GetDisplayName() => _weapon.itemName;
    }
}
```

### 2.4 Обновить PlayerAttacker

`PlayerAttacker.TryAddSourceFromSlot` — если `data is WeaponItemData` → создавать `WeaponDamageSource`, иначе fallback `DefaultDamageSource`:

```csharp
private void TryAddSourceFromSlot(EquipmentData equip, EquipSlot slot, string slotName) {
    if (equip == null) return;
    if (!equip.TryGetItemId(slot, out int itemId) || itemId <= 0) return;
    var inv = InventoryWorld.Instance;
    if (inv == null) return;
    var data = inv.GetItemDefinition(itemId);
    if (data == null) return;

    if (data is WeaponItemData w) {
        _activeSources.Add(new WeaponDamageSource(w, (ulong)itemId));
    } else {
        _activeSources.Add(new DefaultDamageSource((ulong)itemId, $"{slotName}:{data.itemName}"));
    }
}
```

### 2.5 Создать дефолтные WeaponItemData assets

`Assets/_Project/Resources/Items/Weapons/`:
- `Weapon_WoodenSword.asset` (d8, base=2, range=2м, Physical)
- `Weapon_IronDagger.asset` (d4, base=1, range=1.5м, Physical)
- `Weapon_AntigravBlade.asset` (d8, base=3, critMod=+10, range=2м, Antigrav)
- `Weapon_Crossbow.asset` (d10, base=4, range=30м, Ballistic)

---

## 3. T-CB06: armorDefense в ClothingItemData

### 3.1 Добавить поле

`ClothingItemData.cs` — add-only:

```csharp
[Header("Armor (T-CB06)")]
[Tooltip("Физическая защита (для Combat-движка: armor × typeMultiplier = effectiveDefense).")]
[Range(0, 50)] public int armorDefense = 0;
```

### 3.2 Обновить PlayerTarget.GetArmorDefense

`PlayerTarget.cs` — заменить stub:

```csharp
public int GetArmorDefense() {
    int total = 0;
    if (EquipmentWorld.Instance == null || InventoryWorld.Instance == null) return 0;
    var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
    foreach (var slot in new[] { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Back }) {
        if (equip.TryGetItemId(slot, out int itemId) && itemId > 0) {
            var data = InventoryWorld.Instance.GetItemDefinition(itemId);
            if (data is ClothingItemData c) total += c.armorDefense;
        }
    }
    return total;
}
```

### 3.3 Создать clothing с armor

- Обновить существующие `Clothing_*.asset` (Head, Chest, Legs, Feet, Back) — добавить `armorDefense`:
  - `Clothing_Рабочая каска.asset` (Head) → `armorDefense = 2`
  - `Clothing_WorkerChestplate.asset` (Chest) → `armorDefense = 8`
  - `Clothing_WorkerLeggings.asset` (Legs) → `armorDefense = 5`
  - `Clothing_TravelerBoots.asset` (Feet) → `armorDefense = 1`
  - `Clothing_Cloak.asset` (Back) → `armorDefense = 2`

**Итого базовая экипировка:** 18 armor → Antigrav проходит через ×0.5 = 9 effective. Игрок не умирает от первого удара.

---

## 4. T-CB07: SkillMult hook (главный)

### 4.1 Реализация в WeaponDamageSource

```csharp
using ProjectC.Skills;

public float GetSkillMultiplier(ulong attackerId) {
    if (ProjectC.Skills.SkillsWorld.Instance == null) return 1.0f;
    var learned = ProjectC.Skills.SkillsWorld.Instance.GetLearnedSkills(attackerId);
    if (learned == null || learned.Count == 0) return 1.0f;
    float mult = 1.0f;
    foreach (var skillId in learned) {
        var skill = ProjectC.Skills.SkillsWorld.Instance.GetSkillConfig(skillId);
        if (skill == null) continue;
        foreach (var eff in skill.effects) {
            if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
                mult *= eff.multiplier;
            }
        }
    }
    return mult;
}
```

**Без изменений в CombatServer** — `DamageCalculator.Calculate` уже вызывает `source.GetSkillMultiplier(attackerId)`.

### 4.2 Verify

`Player` учил `melee_basic_sword (×1.0) + melee_great_sword (×1.15) + heavy_swing (×1.2)`:
- `mult = 1.0 × 1.15 × 1.2 = 1.38`
- Damage = `roll d10 + base + STR × loc × crit × 1.38`
- Пример: d10=7, base=4, STR=19 (с StatMod+9), crit=no → `7 + 4 + 19 = 30 × 1.0 × 1.0 × 1.38 = 41.4 → 41`
- Без навыков: `30 × 1.0 = 30`
- **Навыки +35% damage** (как в дизайне `Battle/30_SCENARIOS.md §2.2`).

---

## 5. T-CB08: 35 SkillNodeConfig .asset файлов

**Где:** `Assets/_Project/Resources/Skills/Skill_*.asset`.

**Что:** 35 нод из `Battle/20_SKILL_TREES.md`. Каждая с:
- `category` = `CombatDiscipline` (Melee/Ranged/Explosives/Antigrav/Defense/None).
- `effects` = массив `SkillEffect[]` с `StatMod(STR+X, multiplier=Y)`, `WeaponProficiencyUnlock("sword")`, etc.

**Минимум для теста:** 5 базовых combat-скилов (по одному на дисциплину):
- `Skill_Melee_BasicSword.asset` (StatMod STR+1)
- `Skill_Ranged_BasicBow.asset` (StatMod DEX+1)
- `Skill_Antigrav_BasicPulse.asset` (StatMod INT+1, critMod+5)
- `Skill_Defense_BasicArmor.asset` (StatMod DEX+1, defense ×1.1)
- `Skill_Explosives_BasicBomb.asset` (StatMod INT+2, baseDamage+2)

**Через JSON импорт** (отдельный Editor-скрипт) или вручную через Unity.

---

## 6. T-CB09: UI фильтр по CombatDiscipline

В `CharacterWindow.uxml` — добавить tab "Combat" (как существующие tabs: Stats, Equipment, Skills).

**Не блокер для combat** — может быть сделан позже.

---

## 7. Что НЕ трогать

- ❌ `CombatServer.cs` — не меняем. Generic, работает с любыми IDamageSource.
- ❌ `DamageCalculator.cs` — не меняем. Уже принимает `source.GetSkillMultiplier()`.
- ❌ `IAttacker/IDamageTarget/IDamageSource/IRangePolicy` interfaces — не меняем.
- ❌ `MeleeRangePolicy/RangedRangePolicy` — не меняем. Они не зависят от навыков.
- ❌ `WorldEvent.cs` — не меняем. 4 event-класса уже есть.
- ❌ `NetworkManagerController.cs` — не меняем.
- ❌ `NetworkPlayer.cs` — минимально (RegisterWithCombatServer не трогаем).

**Файлы для add-only правок:**
- `Equipment/ClothingItemData.cs` (+`armorDefense` поле)
- `Combat/Implementations/PlayerAttacker.cs` (TryAddSourceFromSlot: if `WeaponItemData` → WeaponDamageSource)
- `Combat/Implementations/PlayerTarget.cs` (GetArmorDefense: реальный подсчёт)
- `Combat/Implementations/WeaponDamageSource.cs` (NEW: GetSkillMultiplier с SkillsWorld)

**Файлы NEW:**
- `Equipment/WeaponItemData.cs` (T-CB03)
- `Combat/Implementations/WeaponDamageSource.cs` (T-CB03/07)
- `Resources/Items/Weapons/*.asset` (4+ weapons, T-CB03)
- `Resources/Skills/Skill_*Combat*.asset` (5+ skills, T-CB08)

---

## 8. Roadmap (предложение)

| Сессия | Тикеты | Файлов | Verify |
|---|---|---|---|
| **#1** | T-CB03 | 2 .cs + 4 .asset | Compile + Play Mode: WoodenSword даёт правильный d8, base=2 |
| **#2** | T-CB06 | 1 .cs edit + 5 .asset edit | Compile + Play Mode: экипировка даёт armor > 0 в damage log |
| **#3** | T-CB07 | 1 .cs edit | Compile + manually Learn Skill → damage × multiplier |
| **#4** | T-CB08 | 5+ .asset | Manual verify: навыки видны в CharacterWindow, XP тратится |
| **#5** | T-CB09 | UI | Manual verify UI |

**ИТОГО:** 4-5 сессий, ~16-21 ч.

---

## 9. Verify checklist (после T-CB01..09)

1. ☐ `WeaponItemData` создан, дефолтные weapon assets в `Resources/Items/Weapons/`.
2. ☐ Экипировка WoodenSword → `DamageCalculator` логирует `source=WeaponName:...` (вместо `Unarmed`).
3. ☐ `armorDefense` в `ClothingItemData` → `effectiveDefense > 0` в логе.
4. ☐ Игрок учил `melee_basic_sword` → `skillMult > 1.0` в логе.
5. ☐ Multiple skills (3+) → `mult = product` (без cap per 2.18).
6. ☐ Antigrav-клинок → `armorMult = 0.5` в логе (Antigrav type).
7. ☐ Mesium-оружие → `armorMult = 0.0` в логе (Mesium игнорирует armor).
8. ☐ RangedRangePolicy (crossbow) → `hitChance = 0.75 * distMod * dexMod` в логе.

---

## 10. После T-CB01..09

- **T-RTC10** (UI damage numbers + hit flash) — Phase 2.
- **T-RTC11..T-RTC15** (PvP duel) — Phase 2.
- **T-RTC16..T-RTC20** (ship combat) — Phase 3.
- **HostileNPC + AI** (отдельная подсистема) — Phase 2.
- **Crafting** (рецепты гранат → ExplosiveDamageSource) — Phase 2.

Combat MVP полностью играбелен с навыками: пеший бой с прогрессией, equipment, stats, damage variance, crit, defense, skillMult, range policy.
