# Data Model — Stats, Clothing, Modules, Skills

> **Дата:** 2026-06-14
> **Базируется на:** `ResourceNodeConfig` как канонический SO-паттерн (146 строк), `ItemData` как база для всех предметов
> **Подход:** все новые SO наследуют конвенции проекта — `[Header]`, `[Tooltip]`, `[Range]`, `[SerializeField] private`, `[CreateAssetMenu]`

---

## 1. StatsConfig (ScriptableObject)

**Файл:** `Assets/_Project/Scripts/Stats/StatsConfig.cs`
**Namespace:** `ProjectC.Stats`
**CreateAssetMenu:** `"Project C/Stats/Stats Config"`

### 1.1 Поля

```csharp
[CreateAssetMenu(fileName = "StatsConfig", menuName = "Project C/Stats/Stats Config", order = 10)]
public class StatsConfig : ScriptableObject {
    [Header("Per-source base XP amounts")]
    [Tooltip("XP gained per 1 mining resource gathered (default: 1 XP per item)")]
    [SerializeField, Min(0f)] private float _miningXpPerItem = 1f;

    [Tooltip("XP gained per 1 craft completed")]
    [SerializeField, Min(0f)] private float _craftingXpPerItem = 5f;

    [Tooltip("XP gained per exchange operation (Pack or Unpack)")]
    [SerializeField, Min(0f)] private float _exchangeXpPerOp = 2f;

    [Tooltip("XP gained per market trade (Buy or Sell)")]
    [SerializeField, Min(0f)] private float _marketXpPerOp = 1f;

    [Tooltip("XP gained per quest accepted")]
    [SerializeField, Min(0f)] private float _questAcceptedXp = 3f;

    [Tooltip("XP gained per quest completed")]
    [SerializeField, Min(0f)] private float _questCompletedXp = 10f;

    [Tooltip("XP gained per dialog with NPC (anti-spam cooldown applies)")]
    [SerializeField, Min(0f)] private float _dialogXpPerVisit = 1f;

    [Tooltip("XP gained per jump (DEX)")]
    [SerializeField, Min(0f)] private float _jumpXp = 0.5f;

    [Tooltip("XP multiplier per 10m walked (DEX) — 1.0 = +1 XP per 10m")]
    [SerializeField, Min(0f)] private float _walkXpPer10m = 1f;

    [Tooltip("XP multiplier per 100m piloted (INT) — 1.0 = +1 XP per 100m")]
    [SerializeField, Min(0f)] private float _pilotXpPer100m = 1f;

    [Header("NPC dialog anti-spam")]
    [Tooltip("Minimum seconds between dialog XP gains from same NPC (per player)")]
    [SerializeField, Min(0f)] private float _dialogXpCooldownSeconds = 60f;

    [Header("Distance tracking")]
    [Tooltip("Walked distance accumulator threshold (meters). XP awarded per this much walked.")]
    [SerializeField, Min(1f)] private float _walkDistanceXpThreshold = 10f;

    [Tooltip("Piloted distance accumulator threshold (meters). XP awarded per this much piloted.")]
    [SerializeField, Min(1f)] private float _pilotDistanceXpThreshold = 100f;

    [Header("Per-stat mapping")]
    [Tooltip("Which stat grows from which source")]
    [SerializeField] private StatMapping _miningTarget = StatMapping.Strength;
    [SerializeField] private StatMapping _craftingTarget = StatMapping.Intelligence;
    [SerializeField] private StatMapping _exchangeTarget = StatMapping.Intelligence;
    [SerializeField] private StatMapping _marketTarget = StatMapping.Intelligence;
    [SerializeField] private StatMapping _questAcceptedTarget = StatMapping.Intelligence;
    [SerializeField] private StatMapping _questCompletedTarget = StatMapping.Intelligence;
    [SerializeField] private StatMapping _dialogTarget = StatMapping.Intelligence;
    [SerializeField] private StatMapping _jumpTarget = StatMapping.Dexterity;
    [SerializeField] private StatMapping _walkTarget = StatMapping.Dexterity;
    [SerializeField] private StatMapping _pilotTarget = StatMapping.Intelligence;

    [Header("Global multiplier (for testing / events)")]
    [Tooltip("Applied to ALL XP gains. 1.0 = no change. Tunable for season/event buffs.")]
    [SerializeField, Range(0.01f, 10f)] private float _globalMultiplier = 1f;

    [Header("Stat growth formula (geometric, no cap by design)")]
    [Tooltip("XP required to advance from tier N to N+1 = baseXp * (growthRate^N)")]
    [SerializeField, Min(1f)] private float _tierBaseXp = 100f;
    [SerializeField, Range(1.01f, 3.0f)] private float _tierGrowthRate = 1.5f;

    [Header("Per-stat growth multiplier (default 1.0 = no change)")]
    [SerializeField, Min(0f)] private float _strengthMultiplier = 1f;
    [SerializeField, Min(0f)] private float _dexterityMultiplier = 1f;
    [SerializeField, Min(0f)] private float _intelligenceMultiplier = 1f;

    [Header("Tier up notification")]
    [Tooltip("Show toast notification when player advances tier")]
    [SerializeField] private bool _announceTierUp = true;

    // === Public API ===
    public float GlobalMultiplier => _globalMultiplier;
    public float TierBaseXp => _tierBaseXp;
    public float TierGrowthRate => _tierGrowthRate;
    public float DialogXpCooldownSeconds => _dialogXpCooldownSeconds;
    public float WalkDistanceXpThreshold => _walkDistanceXpThreshold;
    public float PilotDistanceXpThreshold => _pilotDistanceXpThreshold;
    public bool AnnounceTierUp => _announceTierUp;

    public float XpForNextTier(int currentTier) =>
        currentTier < 0 ? _tierBaseXp : _tierBaseXp * Mathf.Pow(_tierGrowthRate, currentTier);

    public StatType GetStatFor(MiningXpSource source) => MapToStat(source switch {
        MiningXpSource.Mining => _miningTarget,
        MiningXpSource.Crafting => _craftingTarget,
        MiningXpSource.Exchange => _exchangeTarget,
        MiningXpSource.Market => _marketTarget,
        MiningXpSource.QuestAccepted => _questAcceptedTarget,
        MiningXpSource.QuestCompleted => _questCompletedTarget,
        MiningXpSource.Dialog => _dialogTarget,
        MiningXpSource.Jump => _jumpTarget,
        MiningXpSource.Walk => _walkTarget,
        MiningXpSource.Pilot => _pilotTarget,
        _ => StatMapping.Strength,
    });

    public float GetBaseXp(MiningXpSource source) => source switch {
        MiningXpSource.Mining => _miningXpPerItem,
        MiningXpSource.Crafting => _craftingXpPerItem,
        MiningXpSource.Exchange => _exchangeXpPerOp,
        MiningXpSource.Market => _marketXpPerOp,
        MiningXpSource.QuestAccepted => _questAcceptedXp,
        MiningXpSource.QuestCompleted => _questCompletedXp,
        MiningXpSource.Dialog => _dialogXpPerVisit,
        MiningXpSource.Jump => _jumpXp,
        MiningXpSource.Walk => _walkXpPer10m,
        MiningXpSource.Pilot => _pilotXpPer100m,
        _ => 0f,
    };

    public float ApplyGlobalMultiplier(float xp) => xp * _globalMultiplier;
    public float ApplyStatMultiplier(StatType stat, float xp) => xp * (stat switch {
        StatType.Strength => _strengthMultiplier,
        StatType.Dexterity => _dexterityMultiplier,
        StatType.Intelligence => _intelligenceMultiplier,
        _ => 1f,
    });

    private static StatType MapToStat(StatMapping m) => m switch {
        StatMapping.Strength => StatType.Strength,
        StatMapping.Dexterity => StatType.Dexterity,
        StatMapping.Intelligence => StatType.Intelligence,
        _ => StatType.Strength,
    };
}

public enum StatMapping : byte { Strength = 0, Dexterity = 1, Intelligence = 2 }
public enum MiningXpSource : byte {
    Mining, Crafting, Exchange, Market, QuestAccepted, QuestCompleted,
    Dialog, Jump, Walk, Pilot
}
```

### 1.2 Тестовый .asset

**Файл:** `Assets/_Project/Resources/Stats/StatsConfig_Default.asset`

Default values:
- Mining: 1 XP/item → STR
- Crafting: 5 XP → INT
- Dialog: 1 XP, cooldown 60s → INT
- Walk: 1 XP per 10m → DEX
- Pilot: 1 XP per 100m → INT
- Global multiplier: 1.0
- Tier base: 100 XP, growth rate 1.5

---

## 2. ClothingItemData (extends ItemData)

**Файл:** `Assets/_Project/Scripts/Equipment/ClothingItemData.cs`
**Namespace:** `ProjectC.Equipment`
**Наследование:** `class ClothingItemData : ItemData` (чтобы `ItemRegistry` регистрировал автоматически)
**CreateAssetMenu:** `"Project C/Equipment/Clothing"`

### 2.1 Поля

```csharp
[CreateAssetMenu(fileName = "Clothing_", menuName = "Project C/Equipment/Clothing", order = 11)]
public class ClothingItemData : ItemData {
    [Header("Equip")]
    [Tooltip("Slot this item occupies when equipped")]
    public EquipSlot slot;

    [Header("Tier (visual + scaling)")]
    [Tooltip("Tier of this item. Higher tier = better stats.")]
    [Range(1, 10)] public int tier = 1;

    [Header("Stat Bonuses (additive, base)")]
    [Tooltip("Flat bonus to Strength (additive to base stat)")]
    public float strengthBonus;

    [Tooltip("Flat bonus to Dexterity (additive to base stat)")]
    public float dexterityBonus;

    [Tooltip("Flat bonus to Intelligence (additive to base stat)")]
    public float intelligenceBonus;

    [Header("Stat Bonuses (multiplicative, % of base)")]
    [Range(0f, 5f)] public float strengthMultiplier;
    [Range(0f, 5f)] public float dexterityMultiplier;
    [Range(0f, 5f)] public float intelligenceMultiplier;

    [Header("Skill Requirements")]
    [Tooltip("All listed skills must be unlocked to equip this item.")]
    public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();

    [Header("Weight & Cost")]
    [Tooltip("Override maxStack to 1 (clothing is non-stackable)")]
    [Range(0.1f, 50f)] public float weightKgOverride = 2f;

    // Public accessors (no setters)
    public bool HasAnyStatBonus =>
        strengthBonus != 0 || dexterityBonus != 0 || intelligenceBonus != 0 ||
        strengthMultiplier != 0 || dexterityMultiplier != 0 || intelligenceMultiplier != 0;
}
```

### 2.2 Тестовые .asset

**Папка:** `Assets/_Project/Resources/Items/Clothing/`
**Примеры (5 штук для теста):**

| Filename | Slot | Tier | STR | DEX | INT | Required Skills |
|----------|------|------|-----|-----|-----|-----------------|
| `Clothing_WorkerHelmet.asset` | Head | 1 | +1 | 0 | 0 | none |
| `Clothing_SteelChestplate.asset` | Chest | 2 | +3 | -1 | 0 | Skill_Mining1 |
| `Clothing_TravelerBoots.asset` | Feet | 1 | 0 | +2 | 0 | Skill_Walk1 |
| `Clothing_MerchantCloak.asset` | Back | 2 | 0 | 0 | +3 | Skill_Trade1 |
| `Clothing_SmithApron.asset` | Chest | 1 | +1 | 0 | +1 | none |

---

## 3. ModuleItemData (extends ItemData)

**Файл:** `Assets/_Project/Scripts/Equipment/ModuleItemData.cs`
**Namespace:** `ProjectC.Equipment`
**CreateAssetMenu:** `"Project C/Equipment/Module"`

### 3.1 Поля

```csharp
[CreateAssetMenu(fileName = "Module_", menuName = "Project C/Equipment/Module", order = 12)]
public class ModuleItemData : ItemData {
    [Header("Equip")]
    [Tooltip("Module slot this item occupies (Module1, Module2, Module3)")]
    public EquipSlot slot = EquipSlot.Module1;

    [Header("Module Type")]
    public enum ModuleType : byte { Sensor = 0, Processor = 1, Weapon = 2, Utility = 3 }
    public ModuleType moduleType;

    [Header("Tier")]
    [Range(1, 10)] public int tier = 1;

    [Header("Effects")]
    [Tooltip("Sensor range bonus (meters) for Sensor modules")]
    public float sensorRangeBonus;

    [Tooltip("Crafting speed multiplier for Processor modules")]
    [Range(0f, 5f)] public float craftingSpeedMultiplier;

    [Tooltip("Damage bonus for Weapon modules (future)")]
    public float weaponDamageBonus;

    [Tooltip("Stat bonuses (additive, base)")]
    public float strengthBonus;
    public float dexterityBonus;
    public float intelligenceBonus;

    [Header("Skill Requirements")]
    public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();

    [Header("Power Consumption")]
    [Tooltip("Power draw in Watts (future use — ship power system)")]
    public float powerConsumption;
}
```

### 3.2 Тестовые .asset (3-4 штуки)

**Папка:** `Assets/_Project/Resources/Items/Modules/`
**Примеры:**

| Filename | Slot | Type | Bonus | Required Skills |
|----------|------|------|-------|-----------------|
| `Module_RangefinderMk1.asset` | Module1 | Sensor | sensorRange=+10 | Skill_Tech1 |
| `Module_CraftingAssistant.asset` | Module2 | Processor | craftSpeed×1.5 | Skill_Craft1 |
| `Module_DataAnalyzer.asset` | Module3 | Utility | INT+2 | Skill_Analysis1 |

---

## 4. SkillNodeConfig (ScriptableObject)

**Файл:** `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs`
**Namespace:** `ProjectC.Skills`
**CreateAssetMenu:** `"Project C/Skill Node"`

### 4.1 Поля

```csharp
public enum SkillCategory : byte { Social = 0, Combat = 1 }

[CreateAssetMenu(fileName = "Skill_", menuName = "Project C/Skill Node", order = 13)]
public class SkillNodeConfig : ScriptableObject {
    [Header("Identity")]
    [Tooltip("Stable skill ID (used as Dictionary key, persisted across sessions)")]
    public string skillId;             // "social_diplomacy_1"

    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Category (display + future combat/social split)")]
    public SkillCategory category;

    [Header("Prerequisites (DAG, no cycles)")]
    [Tooltip("All listed skills must be unlocked to learn this one.")]
    public SkillNodeConfig[] prerequisites = Array.Empty<SkillNodeConfig>();

    [Header("Effects (applied when learned)")]
    [Tooltip("Effects applied to player when this skill is unlocked")]
    public SkillEffect[] effects = Array.Empty<SkillEffect>();

    [Header("XP Cost to Learn")]
    [Tooltip("XP spent from Intelligence stat pool to unlock this skill. Set 0 for free (e.g. starter skills).")]
    [SerializeField, Min(0f)] private float _learnXpCost = 50f;

    [Header("Tier Requirement (optional)")]
    [Tooltip("Minimum Intelligence tier required. 0 = no requirement.")]
    [SerializeField, Min(0)] private int _requiredIntelligenceTier = 0;

    [Header("UI Layout (for skill tree visualization)")]
    [Tooltip("Position X in skill tree layout (pixels, used for Painter2D view)")]
    public int treeX;

    [Tooltip("Position Y in skill tree layout (pixels)")]
    public int treeY;

    // === Public API ===
    public float LearnXpCost => _learnXpCost;
    public int RequiredIntelligenceTier => _requiredIntelligenceTier;

    // === OnValidate: cycle detection ===
    #if UNITY_EDITOR
    private void OnValidate() {
        if (prerequisites == null || prerequisites.Length == 0) return;
        var visited = new HashSet<SkillNodeConfig>();
        var recursionStack = new HashSet<SkillNodeConfig>();
        if (HasCycle(this, visited, recursionStack)) {
            Debug.LogWarning($"[SkillNodeConfig] Cycle detected in prerequisites for '{skillId}'. Remove one of the edges.", this);
        }
    }

    private static bool HasCycle(SkillNodeConfig node, HashSet<SkillNodeConfig> visited, HashSet<SkillNodeConfig> stack) {
        if (stack.Contains(node)) return true;
        if (visited.Contains(node)) return false;
        visited.Add(node);
        stack.Add(node);
        if (node.prerequisites != null) {
            foreach (var p in node.prerequisites) {
                if (p != null && HasCycle(p, visited, stack)) return true;
            }
        }
        stack.Remove(node);
        return false;
    }
    #endif
}
```

### 4.2 SkillEffect struct

```csharp
[Serializable]
public struct SkillEffect {
    public enum Type : byte {
        StatMod = 0,            // +X к STR/DEX/INT
        AbilityUnlock = 1,      // открывает конкретную способность (для будущего оружия)
        PassiveEffect = 2,      // generic passive (future use)
    }

    public Type type;
    public StatType statType;       // только для StatMod
    public float floatValue;        // additive bonus (для StatMod) или duration (для PassiveEffect)
    [Range(0f, 5f)] public float multiplier;   // multiplicative bonus (для StatMod), 0 = no multiplier
    public string stringParam;      // ability id / passive id (для AbilityUnlock / PassiveEffect)
}
```

### 4.3 Тестовые .asset (8 штук: 4 combat + 4 social)

**Папка:** `Assets/_Project/Resources/Skills/`
**Примеры Combat:**

| Filename | Category | Prerequisites | Effects | XP Cost | INT Tier Req |
|----------|----------|---------------|---------|---------|--------------|
| `Skill_Combat_BasicStrike.asset` | Combat | none | StatMod(STR+2) | 0 | 0 |
| `Skill_Combat_HeavySwing.asset` | Combat | BasicStrike | StatMod(STR+5, damage×1.2) | 100 | 2 |
| `Skill_Combat_DodgeRoll.asset` | Combat | none | StatMod(DEX+3) | 0 | 0 |
| `Skill_Combat_PrecisionStrike.asset` | Combat | DodgeRoll, HeavySwing | StatMod(DEX+5, damage×1.3) | 200 | 4 |

**Примеры Social:**

| Filename | Category | Prerequisites | Effects | XP Cost | INT Tier Req |
|----------|----------|---------------|---------|---------|--------------|
| `Skill_Social_BasicTalk.asset` | Social | none | StatMod(INT+2) | 0 | 0 |
| `Skill_Social_Barter.asset` | Social | BasicTalk | StatMod(INT+3, marketPrice×0.95) | 100 | 2 |
| `Skill_Social_Persuasion.asset` | Social | BasicTalk | PassiveEffect("+10% dialog XP") | 100 | 2 |
| `Skill_Social_Leadership.asset` | Social | Barter, Persuasion | AbilityUnlock("recruit_npc") | 200 | 4 |

---

## 5. Persistence DTO (JsonCharacterDataRepository)

**Файл:** `Assets/_Project/Scripts/Stats/JsonCharacterDataRepository.cs`
**Namespace:** `ProjectC.Stats.Persistence`

### 5.1 CharacterSaveData (parallel DTO для JsonUtility)

```csharp
[Serializable]
public class CharacterSaveData {
    public PlayerStatsSave stats = new();
    public EquipmentSave equipment = new();
    public SkillsSave skills = new();
}

[Serializable]
public class PlayerStatsSave {
    public float strength;
    public float dexterity;
    public float intelligence;
    public int strengthTier;
    public int dexterityTier;
    public int intelligenceTier;
    public float strengthTotalXp;
    public float dexterityTotalXp;
    public float intelligenceTotalXp;
}

[Serializable]
public class EquipmentSave {
    // Fixed-size arrays вместо Dictionary<>
    public byte[] slotOccupied = new byte[EquipmentData.SLOT_COUNT];
    public int[] slotItemIds = new int[EquipmentData.SLOT_COUNT];
}

[Serializable]
public class SkillsSave {
    public string[] learnedSkillIds = Array.Empty<string>();
    // NPC-spam cooldown timestamps (Unix seconds) для восстановления после перезахода
    public NpcCooldownSave[] dialogCooldowns = Array.Empty<NpcCooldownSave>();
}

[Serializable]
public struct NpcCooldownSave {
    public string npcId;
    public float lastTimestamp;
}
```

### 5.2 Repository pattern

```csharp
public interface ICharacterDataRepository {
    bool TryLoad(ulong clientId, out CharacterSaveData data);
    void Save(ulong clientId, CharacterSaveData data);
    string GetSavePath(ulong clientId);
}

public class JsonCharacterDataRepository : ICharacterDataRepository {
    private readonly string _folder;

    public JsonCharacterDataRepository(string folder = null) {
        _folder = folder ?? Path.Combine(Application.persistentDataPath, "Character");
        if (!Directory.Exists(_folder)) Directory.CreateDirectory(_folder);
    }

    public bool TryLoad(ulong clientId, out CharacterSaveData data) {
        var path = GetSavePath(clientId);
        if (!File.Exists(path)) { data = new CharacterSaveData(); return false; }
        try {
            var json = File.ReadAllText(path);
            data = JsonUtility.FromJson<CharacterSaveData>(json) ?? new CharacterSaveData();
            return true;
        } catch (Exception ex) {
            Debug.LogError($"[JsonCharacterDataRepository] Load failed for client {clientId}: {ex.Message}");
            data = new CharacterSaveData();
            return false;
        }
    }

    public void Save(ulong clientId, CharacterSaveData data) {
        var path = GetSavePath(clientId);
        try {
            var json = JsonUtility.ToJson(data, prettyPrint: false);
            var tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmpPath, path);
        } catch (Exception ex) {
            Debug.LogError($"[JsonCharacterDataRepository] Save failed for client {clientId}: {ex.Message}");
        }
    }

    public string GetSavePath(ulong clientId) => Path.Combine(_folder, $"character_{clientId}.json");
}
```

---

## 6. Editor tooling — CSV-импорт (Phase 2)

**Файл (Phase 2):** `Assets/_Project/Scripts/Skills/Editor/SkillsCsvImporter.cs`
**Pattern:** копия `ResourcesCsvImporter.cs` (966 LOC) — multi-block CSV → SO

### 6.1 CSV schema (single file, 1 row = 1 skill)

```csv
skillId,displayName,description,category,prerequisites,effects,learnXpCost,requiredIntelligenceTier,treeX,treeY
social_basic_talk,Базовый разговор,Позволяет поддержать беседу.,social,,StatMod(INT+2),0,0,100,100
combat_basic_strike,Базовый удар,Простой удар оружием.,combat,,StatMod(STR+2),0,0,200,100
combat_heavy_swing,Тяжёлый замах,Мощный удар с бонусом к урону.,combat,combat_basic_strike,StatMod(STR+5),100,2,300,150
social_barter,Торг,Снижает цены на рынке на 5%.,social,social_basic_talk,StatMod(INT+3)+Multiplier(0.95),100,2,150,200
```

### 6.2 Связь с существующим ResourcesCsvImporter

**Рекомендация:** расширить `ResourcesCsvImporter.cs` новыми блоками `clothing`, `modules`, `skills` (по pattern `ProcessInventory`, `ProcessTradeItems`, etc.) вместо создания нового importer.

**Почему:** единый CSV-импорт для всего контента проще для не-технарей (1 файл, 1 menu item `Tools/ProjectC/CSV Import/Export`). Прецедент: пользователь уже одобрил "1 файл на writer persona" (2026-06-09 M19).

---

## 7. Pitfalls (data-model specific)

### 7.1 JsonUtility не сериализует Dictionary

**Проблема:** `JsonUtility.FromJson` не работает с `Dictionary<string, float>`.

**Решение:** Всегда используем parallel `List<T>` или fixed-size arrays (как `InventorySaveData` в `JsonInventoryRepository.cs:38-39`).

### 7.2 EquipmentData fixed-size vs dynamic slots

**Проблема:** Если завтра добавим новый слот (например `Module4 = 23`), нужно менять `EquipmentData.SLOT_COUNT` + пересобирать все сохранения.

**Решение:** Пока MVP — фиксированный размер. Если нужно расширяемость — мигрируем на `string slotName → int itemId` Dictionary (но `JsonUtility` опять не сработает).

### 7.3 SkillNodeConfig.OnValidate cycle detection — performance

**Проблема:** `OnValidate` вызывается на каждом импорте/Ctrl+S. Для 50+ навыков DFS может быть медленным.

**Решение:** Cache visited set на ScriptableObject level через `HashSet<SkillNodeConfig>` static + invalidate on script reload. MVP — простой DFS приемлем.

### 7.4 ItemRegistry не знает про ClothingItemData

**Проблема:** `ItemRegistry` регистрирует только `ItemData`. `ClothingItemData : ItemData` будет зарегистрирован как `ItemData`, потеряем type info.

**Решение:** `InventoryWorld.GetItemDataById` возвращает `ItemData`. При equip/unequip кастим в `ClothingItemData` или `ModuleItemData` для slot check. Runtime check — безопасно.

### 7.5 SkillEffect.Multiplier — integer overflow

**Проблема:** Если 5 навыков дают ×2.0 multiplier STR — итоговый STR ×32 → значения вне float precision.

**Решение:** В `OnValidate` SO warning при `sum(effects) > 5.0`. В runtime — clamp effective multiplier до [0.1, 10].

### 7.6 StatsConfig._globalMultiplier = 0 — divide by zero

**Проблема:** Если тестировщик случайно поставит globalMultiplier = 0 — все XP = 0, ничего не растёт, не понятно почему.

**Решение:** `[Range(0.01f, 10f)]` — минимум 0.01 (не 0). Plus warning в `OnValidate` если `_globalMultiplier < 0.1`.

### 7.7 ClothingItemData.weightKgOverride vs ItemData.weightKg

**Проблема:** `ItemData.weightKg` уже есть, не override'им, а добавляем новое поле. Лишний шум.

**Решение:** Удаляем `weightKgOverride` — используем `ItemData.weightKg` напрямую (выставляем в инспекторе per-item).

### 7.8 ModuleItemData.powerConsumption не используется

**Проблема:** Сейчас нет системы power (поздняя реализация). Dead field?

**Решение:** Оставляем поле — будущая интеграция с ship power system. Прецедент: `ItemData.weightKg` тоже "reserved for future" (`ItemType.cs:33`).

### 7.9 Skill ID stability

**Проблема:** Если разработчик переименует `Skill_Social_BasicTalk.asset` → `Skill_Social_Talk.asset`, `skillId` остаётся `"social_basic_talk"`, но asset path изменился. Player может иметь `"social_basic_talk"` в сохранении, а SO файла нет — orphan reference.

**Решение:** Validate в `OnValidate` — warning если `skillId` пустой или содержит пробелы/спецсимволы. В runtime — orphan skill = пропускаем (fail silent, лог warning).

---

## 8. Что НЕ делаем

- ❌ Не создаём `CharacterItems` namespace — используем `ProjectC.Equipment` и `ProjectC.Skills` (по convention `ProjectC.<Subsystem>`)
- ❌ Не override'им `ItemData.weightKg` в ClothingItemData (лишний шум)
- ❌ Не используем `UnityEditor.Experimental.GraphView` в runtime SO
- ❌ Не пишем `.meta` файлы вручную
- ❌ Не создаём отдельный CSV-importer — расширяем существующий `ResourcesCsvImporter`
- ❌ Не делаем `StatsConfig` per-player (single global instance)
- ❌ Не делаем `SkillNodeConfig.prerequisites` как runtime-configurable (design-time only — редактируется в Editor)
