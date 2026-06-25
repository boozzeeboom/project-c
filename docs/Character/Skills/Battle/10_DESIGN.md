# Design — архитектура combat-навыков

> **Дата:** 2026-06-25 (v0.2 — обновлено под вариант B: ERPR-пакет принят)
> **Базируется на:** `01_ANALYSIS.md` (что есть), `02_LORE.md` (что подтверждено), `06_SKILL_TREE.md` (SkillNodeConfig / SkillEffect), `ERPR_collaboration.md` (ERPR-пакет)
> **Подход:** additive-only. Расширяем `SkillEffect.Type` enum, добавляем новое поле `CombatDiscipline` в `SkillNodeConfig`, вводим 3 новых SO (`WeaponItemData`, `ExplosiveItemData`, lookup-таблицы) + 3 ERPR-поля в `WeaponItemData` (`damageDice`, `baseDamage`, `critModifier`) + 1 ERPR-поле в `ClothingItemData` (`armorDefense`). Ничего не удаляем.

---

## 1. Высокоуровневая архитектура

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    COMBAT-SKILL SUBSYSTEM (NEW)                              │
│                                                                             │
│  ┌────────────────────────┐    ┌────────────────────────┐                    │
│  │ SkillNodeConfig (T-P11)│    │ SkillEffect (T-P11)    │                    │
│  │ + CombatDiscipline     │    │ + 5 новых Type         │                    │
│  │  (Melee/Ranged/        │    │   - WeaponProficiency  │                    │
│  │   Explosives/Antigrav/ │    │   - ArmorProficiency   │                    │
│  │   Defense/None)        │    │   - WeaponTechnique    │                    │
│  └────────────┬───────────┘    │   - ExplosiveRecipe    │                    │
│               │                │   - AntigravTechnique  │                    │
│               │                └────────────┬───────────┘                    │
│               │                             │                               │
│               ▼                             ▼                               │
│  ┌──────────────────────────────────────────────────────────────────┐       │
│  │  SkillsServer.ApplySkillEffects (T-CB07)                          │       │
│  │  switch (effect.type):                                            │       │
│  │    case WeaponProficiencyUnlock → Inventory/Equipment unlock     │       │
│  │    case WeaponTechniqueUnlock     → CombatSystem unlock (future) │       │
│  │    case ExplosiveRecipeUnlock    → Crafting recipe unlock        │       │
│  │    case AntigravTechniqueUnlock  → CombatSystem unlock (future) │       │
│  │    case StatMod (existing)       → StatsServer apply             │       │
│  └──────────────────────────────────────────────────────────────────┘       │
│               │                                                             │
│               ▼                                                             │
│  ┌────────────────────────┐    ┌────────────────────────┐                    │
│  │ WeaponItemData (T-CB03)│    │ ExplosiveItemData(T-CB04)│                  │
│  │  extends ItemData       │    │  extends ItemData       │                  │
│  │  - weaponClass         │    │  - explosiveType        │                  │
│  │  - damageType          │    │  - damageRadius         │                  │
│  │  - requiredSkillClass  │    │  - requiredSkill        │                  │
│  │  - minTier (req)       │    │  - fuseSeconds          │                  │
│  └────────────────────────┘    └────────────────────────┘                    │
│                                                                             │
│  ┌────────────────────────┐    ┌────────────────────────┐                    │
│  │ WeaponClassCatalog     │    │ ArmorClassCatalog      │                    │
│  │ (lookup, 1 .asset)     │    │ (lookup, 1 .asset)     │                    │
│  │ - Sword/Dagger/Spear/  │    │ - Light/Medium/Heavy/  │                    │
│  │   Mace/AntigravBlade/  │    │   Shield/AntigravShield│                    │
│  │   Crossbow/Pneumatic/  │    │                        │                    │
│  │   MesiumRifle          │    │                        │                    │
│  └────────────────────────┘    └────────────────────────┘                    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
                    ┌─────────────────────────────┐
                    │ EquipmentServer.TryEquip      │  (T-CB06 — расширение)
                    │  + принимает WeaponItemData   │
                    │  + проверяет weaponClass vs   │
                    │    unlocked proficiencies     │
                    │  + проверяет minTier vs       │
                    │    equipped skill tiers       │
                    └─────────────────────────────┘
```

---

## 2. Расширение `SkillNodeConfig`

### 2.1 Новое поле `CombatDiscipline`

```csharp
public enum CombatDiscipline : byte {
    None = 0,           // default для social-скилов и существующих 4 combat placeholder'ов
    Melee = 1,
    Ranged = 2,
    Explosives = 3,
    Antigrav = 4,
    Defense = 5,
}

// В SkillNodeConfig (добавление, не замена существующих полей):
[Header("Combat Discipline (only for SkillCategory.Combat)")]
[Tooltip("Display + filter inside combat sub-tab. None = generic combat (existing placeholders).")]
public CombatDiscipline discipline = CombatDiscipline.None;
```

**Backward-compat:**
- 4 существующих .asset (`Skill_Combat_BasicStrike.asset` и т.п.) — `discipline = None` (default при сериализации), попадают в фильтр «All» внутри combat-sub-tab.
- 4 social .asset — `category = Social` (не combat), `discipline` игнорируется.

### 2.2 Никаких других изменений в SkillNodeConfig

- `skillId, displayName, description, icon, category, prerequisites, effects, _learnXpCost, _requiredIntelligenceTier, treeX, treeY` — **без изменений**.
- `OnValidate` cycle detection — **без изменений**.

---

## 3. Расширение `SkillEffect`

### 3.1 Новые Type значения (5 штук)

```csharp
[Serializable]
public struct SkillEffect {
    public enum Type : byte {
        // === Existing (T-P11, 0..2) — НЕ ТРОГАЕМ ===
        StatMod = 0,
        AbilityUnlock = 1,
        PassiveEffect = 2,

        // === NEW (T-CB01, 3..7) ===
        WeaponProficiencyUnlock = 3,   // открывает право экипировать класс оружия
        ArmorProficiencyUnlock = 4,    // открывает право экипировать класс брони
        WeaponTechniqueUnlock = 5,     // открывает боевую технику (парирование, рикошет, ...)
        ExplosiveRecipeUnlock = 6,     // открывает рецепт крафта гранаты/мины
        AntigravTechniqueUnlock = 7,  // открывает antigrav-приём (гравитационный рывок, ...)
    }

    public Type type;
    public StatType statType;       // для StatMod
    public float floatValue;        // additive (StatMod) / duration (Passive)
    [Range(0f, 5f)] public float multiplier;
    public string stringParam;      // abilityId / passiveId / weaponClass / armorClass / techniqueId / recipeId
}
```

**Семантика `stringParam` по type:**

| Type | stringParam содержит | Пример |
|---|---|---|
| `WeaponProficiencyUnlock` | `WeaponClass` (из `WeaponClassCatalog`) | `"sword"`, `"crossbow"`, `"antigrav_blade"` |
| `ArmorProficiencyUnlock` | `ArmorClass` (из `ArmorClassCatalog`) | `"medium"`, `"heavy"`, `"shield"` |
| `WeaponTechniqueUnlock` | `TechniqueId` (из будущего `WeaponTechniqueCatalog`) | `"parry"`, `"riposte"`, `"aimed_shot"` |
| `ExplosiveRecipeUnlock` | `RecipeId` (из `CraftingSystem` рецептов) | `"recipe_grenade_basic"`, `"recipe_mine_antigrav"` |
| `AntigravTechniqueUnlock` | `TechniqueId` (из `AntigravTechniqueCatalog`) | `"grav_pull"`, `"grav_push"`, `"anti_gravity_aura"` |

### 3.2 Backward-compat

- 8 существующих .asset используют только `StatMod` и `AbilityUnlock` (BasicTalk+2 INT, Barter+3 INT и т.п.). Новые Type = 3..7 — никак не задевают.
- Старые `.asset` сериализуются с `type = 0` или `1` — это остаётся валидным.

### 3.3 Что НЕ расширяем (явно)

- ❌ Не вводим `DamageTypeBonus` / `ResistanceBonus` — это **combat-движок**, не навыки. Если потребуется — отдельный `SkillEffect.Type` в T-CB11+ (после combat-движка).
- ❌ Не вводим `StatRequirement` (gate по STR/DEX для skill) — используем существующий `RequiredIntelligenceTier` для базового gate, а спец. gate по другим статам — внутри `ApplySkillEffects` через `StatsServer.Instance.GetStats` (см. `01_ANALYSIS.md §3.8`).

---

## 4. Новые SO (3 типа)

### 4.1 `WeaponItemData` (extends `ItemData`)

**Файл (новая цель):** `Assets/_Project/Scripts/Equipment/WeaponItemData.cs`
**Namespace:** `ProjectC.Equipment`
**Pattern:** копия `ClothingItemData.cs` (T-P07)
**ERPR-пакет:** 3 новых поля (damageDice, baseDamage, critModifier) — см. `ERPR_collaboration.md §3.1`

```csharp
[CreateAssetMenu(fileName = "Weapon_", menuName = "Project C/Equipment/Weapon", order = 14)]
public class WeaponItemData : ItemData {

    [Header("Weapon Identity")]
    [Tooltip("Какой weaponClass это оружие. Игрок может экипировать ТОЛЬКО если у него изучен " +
             "skill с эффектом WeaponProficiencyUnlock для этого класса.")]
    public WeaponClass weaponClass;

    [Header("Combat Properties")]
    [Tooltip("Damage type (Physical / Ballistic / Antigrav). Combat-движок интерпретирует.")]
    public DamageType damageType = DamageType.Physical;

    [Header("ERPR Damage Formula (см. docs/Character/Skills/Battle/ERPR_collaboration.md §3.1)")]
    [Tooltip("Класс кубика урона: d6 / d8 / d10 / d12 / d20. Киньте 1dN при каждой атаке.")]
    public DamageDice damageDice = DamageDice.d6;

    [Tooltip("Базовый урон оружия (без кубика и модификаторов). Используется в формуле " +
             "final = (STR + 1dN + base) × hitLocation × crit × skillMult.")]
    [Range(0, 50)] public int baseDamage = 1;

    [Tooltip("Модификатор крит-броска: 1d100 + critModifier >= 100 → crit ×2. " +
             "Антиграв-оружие обычно +10, мезиевое +5, обычное 0.")]
    [Range(-20, 20)] public int critModifier = 0;

    [Header("Range (метры)")]
    [Tooltip("Дальность оружия в метрах. Melee = 2, Crossbow = 30, MesiumRifle = 100.")]
    [Range(1f, 200f)] public float range = 2f;

    [Header("Skill Requirements")]
    [Tooltip("Все указанные skills должны быть изучены для экипировки (Q2.3 = c: hard/soft).")]
    public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();

    [Header("Tier")]
    [Range(1, 10)] public int tier = 1;  // влияет на базовый damage (combat-движок)

    [Header("Stackability")]
    [Tooltip("Оружие не стакается. maxStack = 1 всегда.")]
    public new int maxStack => 1;  // override (ItemData.maxStack)
}

public enum WeaponClass : byte {
    None = 0,             // зарезервировано для ошибок
    Sword = 1,            // меч (одноручный, базовый) — d6
    GreatSword = 2,       // двуручный меч — d10 (медленнее, сильнее)
    Dagger = 3,           // кинжал — d4 (быстрый, off-hand)
    Spear = 4,            // копьё/древковое — d8 (reach)
    Mace = 5,             // булава/моргенштерн — d8 (blunt, anti-armor)
    Crossbow = 6,         // арбалет — d8, range 30m
    Pneumatic = 7,        // пневматическая винтовка — d10, range 50m
    MesiumRifle = 8,      // мезиевое стрелковое (продвинутое) — d12, range 100m
    AntigravBlade = 9,    // антигравийный клинок — d8, critMod +10
    AntigravHammer = 10,  // антигравийный молот — d10, critMod +10
}

public enum DamageType : byte {
    Physical = 0,         // холодное оружие
    Ballistic = 1,        // арбалет, пневматика, мезиевое (снаряд)
    Antigrav = 2,         // антигравийное оружие, мины, гранаты
    Explosive = 3,        // для гранат/мин
    Mesium = 4,           // мезий-токсин (Phase 3, future)
}

/// <summary>
/// ERPR-совместимый damage dice. 1dN = UnityEngine.Random.Range(1, N+1).
/// См. docs/Character/Skills/Battle/ERPR_collaboration.md §3.1.
/// </summary>
public enum DamageDice : byte {
    d4 = 4,    // кинжал
    d6 = 6,    // меч (default)
    d8 = 8,    // копьё, булава, арбалет
    d10 = 10,  // двуручник, пневматика, antigrav-hammer
    d12 = 12,  // мезиевое
    d20 = 20,  // reserved для будущих легендарных
}

public static class DamageDiceExtensions {
    public static int Roll(this DamageDice dice) => UnityEngine.Random.Range(1, (int)dice + 1);
    public static float Average(this DamageDice dice) => ((int)dice + 1) / 2f;
}
```

**Backward-compat:** `ItemData.maxStack` — public поле в `ItemType.cs:34`. **Override с `new`** скроет в `WeaponItemData`. Если не сработает — переименовать в `weaponMaxStack` и warning в OnValidate.

**ERPR-пакет ноты:**
- `damageDice` — кубик, кидается при каждой атаке (combat-движок / turn-based).
- `baseDamage` — статичный, настраивается designer'ом.
- `critModifier` — модификатор к `1d100` (т.е. `crit если Random.Range(1, 101) + critModifier >= 100`).
- `range` — в метрах (для real-time), но в turn-based battles пересчитывается в клетки (1 клетка = 2м, см. `turn-based-battles/10_DESIGN.md`).

### 4.1.1 `ClothingItemData` (расширение для ERPR — armorDefense)

**Файл (расширение существующего):** `Assets/_Project/Scripts/Equipment/ClothingItemData.cs`
**Namespace:** `ProjectC.Equipment`
**ERPR-пакет:** 1 новое поле `armorDefense` — для Defense-ветки навыков.

```csharp
// В ClothingItemData.cs (добавляем секцию):
[Header("ERPR Defense (см. docs/Character/Skills/Battle/ERPR_collaboration.md §3.4)")]
[Tooltip("Защита от физического/баллистического/антиграв-урона. " +
         "Финальная защита = sum(armorDefense по всем экипированным ClothingItemData). " +
         "0 = без брони. Шлем/нагрудник = основная защита, перчатки/сапоги = дополнительная.")]
[Range(0, 50)] public int armorDefense = 0;
```

**Backward-compat:** 5 существующих .asset (`Clothing_WorkerHelmet.asset` и т.п.) получат `armorDefense = 0` (default при сериализации). Designer вручную проставит значения для брони (например, `WorkerHelmet = 2`, `SteelChestplate = 8`, `TravelerBoots = 1`).

**Лор-пример маппинга `armorDefense`:**
- `WorkerHelmet` (Head, Tier 1) → `armorDefense = 2`
- `SteelChestplate` (Chest, Tier 2) → `armorDefense = 8`
- `TravelerBoots` (Feet, Tier 1) → `armorDefense = 1`
- `MerchantCloak` (Back, Tier 2) → `armorDefense = 3`
- `SmithApron` (Chest, Tier 1) → `armorDefense = 1`

**Defense-формула (используется в Combat-движок + turn-based):**
```csharp
// В PlayerStats или EquipmentWorld (расчёт total defense):
int totalArmor = 0;
foreach (var slot in armorSlots) {  // Head, Chest, Legs, Feet, Back
    if (equipment.TryGetItemId(slot, out var itemId)) {
        var data = InventoryWorld.Instance.GetItemDataById(itemId);
        if (data is ClothingItemData clothing) totalArmor += clothing.armorDefense;
    }
}
// final defense = totalArmor + skillBonus (из Defense-навыков)
```

**Эффективность по damageType (для Combat-движок):**
- `Physical/Ballistic` → defense = `totalArmor × 1.0`
- `Antigrav` → defense = `totalArmor × 0.5` (g-волна частично игнорирует)
- `Explosive` → defense = `totalArmor × 0.7` (взрывная волна частично проходит)
- `Mesium` → defense = `0` (токсин не блокируется бронёй)

**Вердикт:** `armorDefense` — **нативная интеграция** с существующим `ClothingItemData`. Минимальные изменения, большая ценность.

### 4.2 `ExplosiveItemData` (extends `ItemData`)

**Файл:** `Assets/_Project/Scripts/Equipment/ExplosiveItemData.cs`

```csharp
[CreateAssetMenu(fileName = "Explosive_", menuName = "Project C/Equipment/Explosive", order = 15)]
public class ExplosiveItemData : ItemData {

    [Header("Explosive Identity")]
    public ExplosiveType explosiveType;  // Grenade / Mine / Charge

    [Header("Combat Properties")]
    public DamageType damageType = DamageType.Explosive;
    [Tooltip("Радиус поражения в метрах (combat-движок интерпретирует).")]
    [Range(0.5f, 50f)] public float damageRadius = 3f;
    [Tooltip("Время до детонации (сек). 0 = мгновенно (для детонаторов).")]
    [Range(0f, 30f)] public float fuseSeconds = 3f;

    [Header("Skill Requirements")]
    [Tooltip("Skills (например, Explosives-ветка) для крафта/использования.")]
    public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();

    [Header("Stackability")]
    [Tooltip("Расходный предмет, стакается. Default maxStack = 5.")]
    public new int maxStack => 5;
}

public enum ExplosiveType : byte {
    None = 0,
    Grenade = 1,         // метательная, радиус 3-5 м
    Mine = 2,            // устанавливаемая, радиус 5-10 м
    Charge = 3,          // подрывной заряд (для дверей/стен, не combat)
    AntigravMine = 4,    // антигравийная мина (зона g-воздействия, не взрыв)
    AntigravGrenade = 5, // антигравийная граната
}
```

### 4.3 Lookup-SO: `WeaponClassCatalog` и `ArmorClassCatalog`

**Файлы:**
- `Assets/_Project/Scripts/Equipment/WeaponClassCatalog.cs`
- `Assets/_Project/Scripts/Equipment/ArmorClassCatalog.cs`

**Зачем:** skill effect хранит `stringParam = "sword"`. `EquipmentServer.TryEquip` должен проверить: есть ли у игрока `Skill_X` с `WeaponProficiencyUnlock` + `stringParam = "sword"`? Lookup-таблица маппит string → displayName + категория.

```csharp
[CreateAssetMenu(menuName = "Project C/Equipment/Weapon Class Catalog")]
public class WeaponClassCatalog : ScriptableObject {
    [Serializable]
    public struct Entry {
        public WeaponClass cls;
        public string displayName;        // "Меч", "Арбалет"
        public string description;        // tooltip
        public Sprite icon;                // для UI фильтра
        public Discipline discipline;      // Melee / Ranged / Antigrav
    }
    public Entry[] entries = Array.Empty<Entry>();

    public string GetDisplayName(WeaponClass cls) {
        foreach (var e in entries) if (e.cls == cls) return e.displayName;
        return cls.ToString();
    }
}
```

Аналогично для `ArmorClassCatalog`:
```csharp
public enum ArmorClass : byte {
    None = 0,
    Light = 1,        // лёгкая (тряпки, банданы, кожа)
    Medium = 2,       // средняя (chain, plate-части)
    Heavy = 3,        // тяжёлая (полная plate, шлем)
    Shield = 4,       // щит (отдельный слот Back или off-hand)
    AntigravShield = 5,  // антигравийный щит (если подтвердит game-designer)
}
```

**Размещение:** в `Resources/Equipment/` (по 1 .asset-файлу на каталог).

---

## 5. Расширение `EquipmentServer.TryEquip` (T-CB06)

### 5.1 Текущая логика (T-P09)

Из `01_CURRENT_STATE_AUDIT.md §2.2` + `05_CLOTHING_AND_MODULES.md`:
- Item is `ClothingItemData` или `ModuleItemData` (instance check) — **отвергает `WeaponItemData`**
- Slot match
- `requiredSkills[]` — hard/soft (Q2.3 = c)
- Item ownership in inventory
- Slot empty / unequip-first

### 5.2 Что меняем

```csharp
public bool TryEquip(ulong clientId, int itemId, EquipSlot slot, out string reason) {
    reason = "";
    var inventory = _inventoryWorld.GetInventory(clientId);
    if (!InventoryWorld.Instance.GetItemDataById(itemId, out var itemData)) {
        reason = "Предмет не найден"; return false;
    }

    // === EXPANDED: itemData can be ClothingItemData | ModuleItemData | WeaponItemData ===
    if (itemData is not ClothingItemData &&
        itemData is not ModuleItemData &&
        itemData is not WeaponItemData) {
        reason = "Этот предмет не надевается"; return false;
    }

    // Slot match
    EquipSlot requiredSlot = itemData switch {
        ClothingItemData c => c.slot,
        ModuleItemData m => m.slot,
        WeaponItemData w when slot == EquipSlot.WeaponMain || slot == EquipSlot.WeaponOff => slot,
        WeaponItemData w => EquipSlot.None,  // оружие ТОЛЬКО в WeaponMain/Off
        _ => EquipSlot.None,
    };
    if (requiredSlot == EquipSlot.None || requiredSlot != slot) {
        reason = $"Слот не подходит"; return false;
    }

    // === NEW: WeaponProficiency check (T-CB06) ===
    if (itemData is WeaponItemData weapon) {
        var learned = _skillsWorld.GetLearnedSkills(clientId);
        bool hasProficiency = false;
        foreach (var skillId in learned) {
            var skillConfig = _skillsWorld.GetSkillConfig(skillId);
            if (skillConfig == null) continue;
            foreach (var effect in skillConfig.effects) {
                if (effect.type == SkillEffect.Type.WeaponProficiencyUnlock &&
                    effect.stringParam == weapon.weaponClass.ToString().ToLower()) {
                    hasProficiency = true; break;
                }
            }
            if (hasProficiency) break;
        }
        if (!hasProficiency) {
            reason = $"Нужен навык владения: {weapon.weaponClass}";
            return false;
        }
    }

    // === EXPANDED: requiredSkills (был только для clothing/module, теперь и для weapon) ===
    var requiredSkills = itemData switch {
        ClothingItemData c => c.requiredSkills,
        ModuleItemData m => m.requiredSkills,
        WeaponItemData w => w.requiredSkills,
        _ => Array.Empty<SkillNodeConfig>(),
    };
    // ... существующая логика Q2.3 (hard/soft) ...

    // Item ownership, slot empty — без изменений
}
```

**Что НЕ меняем:**
- `EquipmentData` struct (13 слотов уже включают WeaponMain/Off)
- `EquipSlot` enum
- `TryUnequip` (без изменений)
- Persistence (character_<clientId>.json Equipment секция)

---

## 6. Расширение `SkillsServer.ApplySkillEffects` (T-CB07)

### 6.1 Текущая логика (T-P13)

Из `06_SKILL_TREE.md §3 §334-338`:
```csharp
private void ApplySkillEffects(ulong clientId, SkillNodeConfig skill) {
    // Skill effects применяются в StatsServer.RecomputeAndSendSnapshot
    // (см. Equipment.md §2.2 — RecomputeEffectiveStat)
    // Здесь просто сигнализируем: skill добавлен в learned set.
}
```

**Вердикт:** сейчас no-op. Всё работает потому, что StatMod-эффекты на placeholder-combat-навыках (`+2 STR`) **фактически не применяются** — T-P13 не реализует handler. Это **известный gap** (CHANGELOG строка 46).

### 6.2 Расширенная логика (T-CB07)

```csharp
private void ApplySkillEffects(ulong clientId, SkillNodeConfig skill) {
    if (skill.effects == null) return;
    foreach (var effect in skill.effects) {
        switch (effect.type) {
            case SkillEffect.Type.StatMod:
                // (T-CB07 part 1) применяем к StatsServer
                ApplyStatMod(clientId, effect);
                break;

            case SkillEffect.Type.WeaponProficiencyUnlock:
                // (T-CB07 part 2) НЕ применяем runtime — это маркер для EquipmentServer.TryEquip
                // (там проверяется наличие в learned set при попытке экипировки)
                // MarkProficiencyLearned(clientId, effect.stringParam);
                break;

            case SkillEffect.Type.ArmorProficiencyUnlock:
                // (T-CB07 part 2) аналогично — marker для EquipmentServer
                break;

            case SkillEffect.Type.WeaponTechniqueUnlock:
                // (T-CB07 part 3) future: combat-engine подпишется и будет использовать techniqueId
                // Пока просто флаг в _learnedTechniques
                MarkTechniqueLearned(clientId, effect.stringParam);
                break;

            case SkillEffect.Type.ExplosiveRecipeUnlock:
                // (T-CB07 part 4) регистрируем рецепт в CraftingSystem (если ещё нет)
                // или в локальном реестре игрока
                RegisterExplosiveRecipe(clientId, effect.stringParam);
                break;

            case SkillEffect.Type.AntigravTechniqueUnlock:
                // (T-CB07 part 3) аналог WeaponTechniqueUnlock
                MarkAntigravTechniqueLearned(clientId, effect.stringParam);
                break;

            case SkillEffect.Type.AbilityUnlock:
            case SkillEffect.Type.PassiveEffect:
                // (existing) no-op for now
                break;

            default:
                Debug.LogWarning($"[SkillsServer] Unimplemented effect type: {effect.type}");
                break;
        }
    }
}

private void ApplyStatMod(ulong clientId, SkillEffect effect) {
    // Делегируем в StatsServer (T-P05, существующий)
    var stats = _statsWorld.GetOrCreateStats(clientId);
    switch (effect.statType) {
        case StatType.Strength:    stats.strengthBonus += effect.floatValue; break;
        case StatType.Dexterity:   stats.dexterityBonus += effect.floatValue; break;
        case StatType.Intelligence: stats.intelligenceBonus += effect.floatValue; break;
    }
    _statsWorld.SetStats(clientId, stats);
    StatsServer.Instance?.RecomputeAndSendSnapshot(clientId);
}
```

**Что НЕ меняем:**
- `RequestLearnSkillRpc` flow
- `TryLearnSkill` validation
- Snapshot sync
- Rate limit
- `RequestForgetSkillRpc` (revert)

**Место в `SkillsServer.cs`:** новая `ApplySkillEffects` (T-CB07) — **заменяет** текущий no-op stub. Diff ~50 строк.

### 6.3 Pitfall: 5 новых Type — NRE в switch

Если забудете case в switch → `default` ловит, `Debug.LogWarning` пишет. **Никаких NRE** (это pattern из `06_SKILL_TREE.md §6.5`).

### 6.4 Pitfall: TryLearnSkill success ДО ApplySkillEffects

В T-P13 `TryLearnSkill`:
1. `learned.Add(skillId)` — OK
2. `ApplySkillEffects(clientId, skill)` — OK (внутри T-P13 вызов)

Расширение T-CB07: `ApplySkillEffects` вызывается **на том же шаге 2**, не после snapshot. Это **синхронно** — никаких race.

---

## 8. Сценарии end-to-end (для верификации)

### 8.1 Сценарий A: «Игрок учит владение мечом»

1. Игрок открывает CharacterWindow → sub-tab «НАВЫКИ» → combat-list
2. Видит `Skill_Melee_BasicSword` (XP cost = 0, prereq = none)
3. Нажимает [ИЗУЧИТЬ] → `RequestLearnSkillRpc("melee_basic_sword")`
4. Server: `TryLearnSkill` → success → `learned.Add("melee_basic_sword")` → `ApplySkillEffects`
5. `ApplySkillEffects` видит `WeaponProficiencyUnlock` + `stringParam = "sword"` → ставит маркер
6. Snapshot → client: `learnedSkillIds += ["melee_basic_sword"]`
7. UI: row переходит в `Learned` state

**Verify:** `Debug.Log("[SkillsServer] Player X learned skill 'Skill_Melee_BasicSword'")` в Console.

### 8.2 Сценарий B: «Игрок пытается надеть меч без навыка»

1. Игрок открывает инвентарь → `Weapon_SteelSword` (itemId = 5001)
2. Нажимает [НАДЕТЬ] → `RequestEquipRpc(itemId=5001, slot=WeaponMain)`
3. Server: `TryEquip`:
   - itemData is `WeaponItemData` ✓
   - slot = WeaponMain ✓
   - **проверка proficiency**: ищем в `learned` skill с `WeaponProficiencyUnlock` + `stringParam = "sword"`. Не находим → reason = «Нужен навык владения: Sword» → **deny**
4. Client: `EquipResult.Denied("Нужен навык владения: Sword")` → toast

**Verify:** в Console `[EquipmentServer] TryEquip denied: Нужен навык владения: Sword`. UI показывает reason.

### 8.3 Сценарий C: «Игрок учит продвинутую технику парирования»

1. У игрока уже изучен `Skill_Melee_BasicSword`
2. Игрок пытается изучить `Skill_Melee_Parry` (XP cost = 100, prereq = BasicSword)
3. `TryLearnSkill`: prereq OK, XP OK, `learned.Add("melee_parry")`
4. `ApplySkillEffects`: видит `WeaponTechniqueUnlock` + `stringParam = "parry"` → `MarkTechniqueLearned("parry")`
5. (Future) Combat-движок при атаке на игрока проверяет: `HasTechnique("parry")` → включает парирование

**Сейчас (без combat-движка):** флаг лежит, ни на что не влияет. Это **намеренно** — навыки готовы, движок придёт.

### 8.4 Сценарий D: «Игрок изучает рецепт антигравийной гранаты»

1. Игрок изучает `Skill_Explosives_AntigravRecipe` (XP cost = 200, prereq = BasicGrenade, INT tier 4)
2. `ApplySkillEffects`: `ExplosiveRecipeUnlock` + `stringParam = "recipe_antigrav_grenade_basic"` → регистрирует в `CraftingSystem` реестре игрока
3. UI: крафтинг-станция показывает новый рецепт
4. (Future) Игрок крафтит `Item_AntigravGrenade_Basic`

**Замечание:** интеграция с CraftingSystem — отдельный тикет T-CB07+ (см. `30_PITFALLS_AND_OPEN_QUESTIONS.md §6.5`). В MVP-1 навык просто разблокирует «знание» (флаг), реальный крафт — позже.

### 8.5 Сценарий E: «Игрок носит тяжёлую броню» (ERPR-пакет)

1. Игрок изучает `Skill_Defense_HeavyArmor` (XP cost = 200, prereq = medium armor, INT tier 3)
2. `ApplySkillEffects`: видит `ArmorProficiencyUnlock` + `stringParam = "heavy"` → ставит маркер
3. Игрок экипирует `Clothing_SteelChestplate` (armorDefense = 8) + `Clothing_WorkerHelmet` (armorDefense = 2) + `Clothing_TravelerBoots` (armorDefense = 1)
4. Server: `TryEquip` — `ArmorProficiencyUnlock("heavy")` есть → equip OK
5. Игрок получает урон от NPC: `final_damage = baseAttack × hitLocation × crit × skillMult - 11` (armorDefense = 8+2+1)
6. Если damageType = Antigrav: `effective_defense = 11 × 0.5 = 5.5 → 6` (частично игнорируется)

**Verify:** в Console `[EquipmentServer] TryEquip allowed: heavy armor (armorDefense=8+2+1=11)`. Combat-движок (или turn-based battles) использует ту же формулу.

### 8.6 Сценарий F: «Игрок атакует двуручным мечом» (ERPR-пакет)

1. Игрок изучил `Skill_Melee_GreatSword` (proficiency, dmg ×1.15) + экипировал `Weapon_GreatSword_Antigrav` (damageDice=d10, baseDamage=4, critModifier=+10)
2. Игрок атакует NPC в turn-based battle (или real-time combat-движок)
3. Combat-движок: `CalculateDamage(attackerId, npcId, skill_great_sword)`:
   - `roll = Random.Range(1, 11) = 7` (пример)
   - `baseAttack = 7 + 4 + 10 (STR) = 21`
   - `locRoll = Random.Range(1, 5) = 4` (Head!) → `locMult = 2.0`
   - `critRoll = Random.Range(1, 101) = 95` + 10 = 105 ≥ 100 → `critMult = 2.0`
   - `skillMult = 1.15` (great_sword)
   - `final = round(21 × 2.0 × 2.0 × 1.15) = 97`
   - `effective_defense = 5 (NPC в лёгкой броне) × 1.0 (Physical) = 5`
   - `final = 97 - 5 = 92`
4. NPC получает 92 урона, hit в голову + crit

**Verify:** в Console `[Damage] Player→NPC: roll=7 + base=4 + str=10 = 21; loc=2×, crit=2×, skill=1.15× → 97 (armor=5)`. Урон зафиксирован в логе (для replay).

---

## 9. Persistence — что сохраняем

### 9.1 Существующее (T-P12, T-P13)

`SkillsWorld.LoadPlayer(clientId, data)`:
```csharp
public void LoadPlayer(ulong clientId, CharacterSaveData data) {
    var learned = GetLearnedSkillIds(clientId);
    learned.Clear();
    if (data.skills?.learnedSkillIds != null) {
        foreach (var id in data.skills.learnedSkillIds) learned.Add(id);
    }
}
```

### 9.2 Что НЕ меняем в персистенции (для MVP)

`learnedSkillIds` — единый список **всех** изученных навыков (combat + social + future). Расширение `SkillEffect.Type` **не требует** дополнительных полей в `SkillsSave` (см. `03_DATA_MODEL.md §5.1`):
```csharp
[Serializable]
public class SkillsSave {
    public string[] learnedSkillIds = Array.Empty<string>();
    public NpcCooldownSave[] dialogCooldowns = Array.Empty<NpcCooldownSave>();
}
```

**Почему достаточно:** runtime-маркеры (proficiency, technique, recipe) **вычисляются** из `learnedSkillIds` при каждом equip/combat. Не нуждаются в персистенции.

### 9.3 Pitfall: pitfall #30 — forward-declare

Если T-CB07 (SkillsServer.ApplySkillEffects) **не реализован**, а T-CB01 (SkillEffect enum) уже — компилируется, но эффекты no-op (default case). Это **намеренно** для инкрементальных тикетов.

---

## 10. UI (T-CB09, Phase 2)

### 10.1 MVP: плоский combat-list (текущее)

Сейчас в CharacterWindow sub-tab «НАВЫКИ» → 2 ListView (combat + social). Combat — плоский список. **Работает** для 4 placeholder'ов, но при 25+ нодах — шумно.

### 10.2 Phase 2: фильтр по `CombatDiscipline`

**Подход:** внутри combat-sub-tab — **второй уровень фильтра**:
- Top row (sub-sub-tabs): `[Все] [Ближний бой] [Стрелковое] [Взрывчатка] [Антигравитация] [Защита]`
- Ниже — существующий ListView, фильтрованный по `skill.discipline == selected` (или `All`)

**Реализация:** `CharacterWindow.cs:1480+` — `BuildSkillRow` уже берёт `skill.Category`. Расширяем до `skill.Category + skill.discipline`.

**Эстимейт:** ~1.5 ч кодинга. **Phase 2**, не блокер.

### 10.3 Что НЕ делаем в UI

- ❌ Не делаем Painter2D-граф (T-P19, отдельный тикет)
- ❌ Не делаем drag-and-drop (T-P20, отдельный тикет)
- ❌ Не делаем визуализацию proficiency-маркеров в инвентаре (Phase 3)

---

## 7. ERPR Damage-формула (полная, актуализировано под B)
> **Источник:** `ERPR_collaboration.md §3.1-3.3` + `ERPR1.2.pdf` стр.7. Подробный анализ — в `ERPR_collaboration.md`.

### 7.1 Формула (server-side, deterministic по seed)

```
final_damage = max(0, base_attack + stat_mod) × hitLocation × crit × skillMult
final_damage -= effective_defense
final_damage = max(0, final_damage)
```

**где:**
- `base_attack = 1dN + weapon.baseDamage` (ERPR-пакет: `weapon.damageDice.Roll() + weapon.baseDamage`)
- `stat_mod = STR` (или базовые 10, если не введена; у нас введён через `StatsConfig`)
- `hitLocation` = множитель от 1d4: `Limbs=0.5, Torso=1, Head=2` (ERPR §3.2)
- `crit` = 2.0 если `1d100 + weapon.critModifier >= 100`, иначе 1.0 (ERPR §3.3)
- `skillMult` = множитель от навыка (например, `HeavySwing` ×1.2, `PrecisionStrike` ×1.3)
- `effective_defense = armorDefense × typeMultiplier` (см. §4.1.1)

### 7.2 Реализация (псевдокод, server-authoritative)

```csharp
public static int CalculateDamage(ulong attackerId, ulong defenderId, SkillNodeConfig skill) {
    // 1. Получить данные
    var weapon = EquipmentWorld.Instance.GetEquippedWeapon(attackerId);  // WeaponItemData
    if (weapon == null) return 0;

    var defenderEquip = EquipmentWorld.Instance.GetEquipment(defenderId);
    int totalArmor = ComputeTotalArmor(defenderEquip);

    var stats = StatsWorld.Instance.GetOrCreateStats(attackerId);
    int strMod = stats.strengthTier > 0 ? stats.strengthTier * 5 : 10;  // tier → модификатор

    // 2. Base attack (ERPR)
    int roll = weapon.damageDice.Roll();  // Random.Range(1, N+1)
    int baseAttack = roll + weapon.baseDamage + strMod;

    // 3. Hit location (ERPR 1d4)
    int locRoll = UnityEngine.Random.Range(1, 5);  // 1..4
    float locMult = locRoll switch {
        1 or 2 => 0.5f,  // Limbs
        3 => 1.0f,       // Torso
        4 => 2.0f,       // Head
        _ => 1.0f,
    };

    // 4. Crit (ERPR 1d100)
    int critRoll = UnityEngine.Random.Range(1, 101);
    float critMult = (critRoll + weapon.critModifier >= 100) ? 2.0f : 1.0f;

    // 5. Skill multiplier (из навыка)
    float skillMult = 1.0f;
    if (skill != null) {
        foreach (var eff in skill.effects) {
            if (eff.type == SkillEffect.Type.StatMod && eff.multiplier > 0) {
                skillMult *= eff.multiplier;  // напр. HeavySwing ×1.2
            }
        }
    }

    // 6. Final damage
    int final = Mathf.RoundToInt(baseAttack * locMult * critMult * skillMult);

    // 7. Defense
    float armorMult = weapon.damageType switch {
        DamageType.Physical or DamageType.Ballistic => 1.0f,
        DamageType.Antigrav => 0.5f,
        DamageType.Explosive => 0.7f,
        DamageType.Mesium => 0.0f,
        _ => 1.0f,
    };
    int effectiveDefense = Mathf.RoundToInt(totalArmor * armorMult);
    final -= effectiveDefense;
    final = Mathf.Max(0, final);

    // 8. Логирование (для replay/аналитики, отключаемо)
    Debug.Log($"[Damage] {attackerId}→{defenderId}: roll={roll} + base={weapon.baseDamage} + str={strMod} = {baseAttack}; loc={locMult}×, crit={critMult}×, skill={skillMult}× → {final} (armor={effectiveDefense})");

    return final;
}
```

### 7.3 Где используется

- **Real-time combat-движок (отдельная подсистема, T-CB10)**: `CalculateDamage` вызывается при попадании в NetworkPlayer/NPC.
- **Turn-based battles (`docs/Character/Skills/turn-based-battles/`)**: `CalculateDamage` вызывается при выборе атаки в бою, с `skill` = выбранный навык.
- **НЕ используется**: при social-взаимодействиях (диалоги, торговая), при крафтинге, при путешествиях.

### 7.4 Опциональные механики (ERPR §3.2-3.3, Phase 2)

- **Без hit_location (упрощение)**: `locMult = 1.0` для всех атак. Для real-time — оправдано (нет времени на анимации зон попадания).
- **Без crit (упрощение)**: `critMult = 1.0`. Можно для бета-теста, потом включить.
- **С дополнительными модификаторами**: навык `melee_precision_strike` даёт +20% шанс `Head`, навык `antigrav_aura` даёт +10% шанс `Limbs` (Phase 2, требует `WeaponTechniqueUnlock` модификации бросков в Combat-движок).

### 7.5 Баланс (предложение)

- **Средний урон меча (d6) на STR=10**: `(3.5 + 1 + 10) × 1.0 × 1.0 × 1.0 = 14.5` (без защиты) → ~10 с базовой бронёй.
- **Средний урон двуручника (d10) на STR=15**: `(5.5 + 2 + 15) × 1.0 × 1.0 × 1.0 = 22.5` → ~15 с базовой бронёй.
- **Средний урон арбалета (d8) на DEX=15**: то же ~22.5 (DEX в формуле не используется — но DEX может быть в crit-бросках или hit_location в Phase 2).
- **Крит (×2)**: раз в 100/модификатор бросков. Меч = 1%, мезиевое = 6%, antigrav = 11%.
- **Headshot (×2)**: 25% шанс. С `precision_strike` (+20%) = 45%.

**Вердикт:** формула даёт **широкий разброс урона** (1d6 + base = 2..7), но **средние значения** стабильны. Это и нужно для MMO — динамика без overstacking.

---

## 11. Сводка изменений по файлам

| Файл | Тип | Сложность | Тикет |
|---|---|---|---|
| `Assets/_Project/Scripts/Skills/SkillEffect.cs` | изменить (enum +5) | ~0.5 ч | T-CB01 |
| `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` | изменить (+1 field `discipline`) | ~0.5 ч | T-CB02 |
| `Assets/_Project/Scripts/Skills/CombatDiscipline.cs` | новый enum | ~0.25 ч | T-CB02 |
| `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` | новый + **3 ERPR-поля** (`damageDice`, `baseDamage`, `critModifier`) | ~2 ч | T-CB03 |
| `Assets/_Project/Scripts/Equipment/ExplosiveItemData.cs` | новый | ~1.5 ч | T-CB04 |
| `Assets/_Project/Scripts/Equipment/WeaponClass.cs` | новый enum | ~0.25 ч | T-CB03 |
| `Assets/_Project/Scripts/Equipment/ExplosiveType.cs` | новый enum | ~0.25 ч | T-CB04 |
| `Assets/_Project/Scripts/Equipment/DamageType.cs` | новый enum | ~0.25 ч | T-CB03 |
| `Assets/_Project/Scripts/Equipment/DamageDice.cs` | новый enum (ERPR) | ~0.25 ч | T-CB03 |
| `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | изменить (+1 field `armorDefense`, ERPR) | ~0.5 ч | T-CB06 |
| `Assets/_Project/Scripts/Equipment/WeaponClassCatalog.cs` | новый | ~0.5 ч | T-CB05 |
| `Assets/_Project/Scripts/Equipment/ArmorClassCatalog.cs` | новый | ~0.5 ч | T-CB05 |
| `Assets/_Project/Scripts/Combat/HitLocation.cs` | новый enum (ERPR, Phase 2) | ~0.5 ч | T-CB10/T-TB10 |
| `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` | изменить (TryEquip +~30 строк + WeaponItemData + armorDefense) | ~2 ч | T-CB06 |
| `Assets/_Project/Scripts/Skills/SkillsServer.cs` | изменить (ApplySkillEffects +~50 строк) | ~2 ч | T-CB07 |
| `Assets/_Project/Resources/Equipment/WeaponClassCatalog_Default.asset` | новый | ~0.5 ч | T-CB05 |
| `Assets/_Project/Resources/Equipment/ArmorClassCatalog_Default.asset` | новый | ~0.5 ч | T-CB05 |
| `Assets/_Project/Resources/Items/Clothing/*` (5 шт) | изменить (добавить `armorDefense`) | ~0.5 ч | T-CB06 |
| `Assets/_Project/Resources/Items/Weapons/*` (~10 .asset) | новые (для тестов) | ~1 ч | T-CB08 |
| `Assets/_Project/Resources/Skills/Combat/*.asset` | новые (25-30 шт, с damage dice/crit) | ~4-5 ч | T-CB08 |
| `Assets/_Project/UI/Client/CharacterWindow.cs` | изменить (фильтр discipline, Phase 2) | ~1.5 ч | T-CB09 |

**Итого: ~16-21 ч кодинга** в 2-3 сессии.

**ERPR-пакет ноты** (см. `ERPR_collaboration.md`):
- 3 новых поля в `WeaponItemData` (`damageDice`, `baseDamage`, `critModifier`) + 1 новое поле в `ClothingItemData` (`armorDefense`) — **минимальные** изменения базовых SO, большая ценность для Combat-движка и turn-based battles.
- `HitLocation` enum — Phase 2 (для `turn-based-battles/`).
- Damage-формула в `10_DESIGN.md §7` — готова, используется в Combat-движок + turn-based battles.

---

## 12. Что НЕ делаем (явные запреты)

- ❌ Не трогаем `GDD_20_Progression_RPG.md` (gdd/ read-only)
- ❌ Не переписываем `06_SKILL_TREE.md` (только дополняем этим диздоком)
- ❌ Не вводим `SkillPoint` / `Level` (GDD 20 фича)
- ❌ Не проектируем combat-движок (hit/damage/projectile) — отдельная подсистема
- ❌ Не вводим PvP / faction-AI / NPC-врагов — отдельные подсистемы
- ❌ Не делаем `StatusEffectSystem` (баффы/дебаффы) — future, Phase 3
- ❌ Не пишем код в этой сессии (research + design-doc only)
