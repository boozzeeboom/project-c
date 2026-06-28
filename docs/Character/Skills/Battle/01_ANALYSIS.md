# Analysis — что есть / чего нет / расхождения

> **Дата:** 2026-06-25 (v0.2 — обновлено под вариант B: ERPR-пакет принят)
> **Статус на 2026-06-28:** ✅ **MVP+1 реализован.** См. `IMPLEMENTATION_PLAN_2026.md` итоговую таблицу.  
>   Анализ ниже остаётся валидным как **дизайн-док** — часть аналитики устарела,  
>   но ссылки на T-CB* тикеты теперь показывают ✅/❌ (см. roadmap в `00_README.md`).
> **Метод:** read_file существующих .cs + .md + grep по `docs/` и `Assets/`
> **Цель:** зафиксировать фактическое состояние кода и документации, чтобы combat-навыки проектировались **поверх существующего**, а не вместо.

---

## 1. Что УЖЕ реализовано (на что опираемся)

### 1.1 SkillNodeConfig + SkillEffect (T-P11, ✅)

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` (98 строк, реализован)
- `Assets/_Project/Scripts/Skills/SkillEffect.cs` (в T-P11 d-схеме, в 06_SKILL_TREE.md §1.2)

**Что есть:**
```csharp
public enum SkillCategory : byte { Social = 0, Combat = 1 }

[CreateAssetMenu(menuName = "Project C/Skill Node")]
public class SkillNodeConfig : ScriptableObject {
    public string skillId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public SkillCategory category;
    public SkillNodeConfig[] prerequisites;
    public SkillEffect[] effects;
    [SerializeField, Min(0)] private float _learnXpCost;
    [SerializeField, Min(0)] private int _requiredIntelligenceTier;
    public int treeX, treeY;
    // OnValidate cycle detection
}

[Serializable]
public struct SkillEffect {
    public enum Type : byte {
        StatMod = 0,            // +X к STR/DEX/INT
        AbilityUnlock = 1,      // открывает ability id
        PassiveEffect = 2,      // generic passive
    }
    public Type type;
    public StatType statType;       // только для StatMod
    public float floatValue;        // additive bonus / duration
    [Range(0f, 5f)] public float multiplier;
    public string stringParam;      // ability id / passive id
}
```

**Вердикт:** это **фундамент**, который мы расширяем. Backward-compat: 8 уже созданных `Skill_*.asset` (4 combat + 4 social) **не сломаются**, если расширим `Type` enum значениями `>= 3` и добавим опциональные поля в `SkillEffect`.

### 1.2 SkillsServer + SkillsWorld + SkillsClientState (T-P12..T-P13, ✅)

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillsServer.cs` (206 строк, реализован)
- `Assets/_Project/Scripts/Skills/SkillsWorld.cs` (реализован, см. `06_SKILL_TREE.md §3`)
- `Assets/_Project/Scripts/Skills/SkillsClientState.cs` (реализован)

**Что есть в SkillsServer:**
- `Instance` singleton, scene-placed в BootstrapScene
- `OnNetworkSpawn` → `SkillsWorld.LoadAllSkills(_config)`, `OnClientConnected` → `GrantDefaultSkills` + `SendSnapshotToOwner`
- RPC: `RequestLearnSkillRpc(skillId)`, `RequestForgetSkillRpc(skillId)` (Q3.4 — free respec)
- Rate-limit per-client (5 ops/sec, Q3.3)
- После learn: `TriggerStatsRecompute(clientId)` через reflection → `StatsServer.Instance.RecomputeAndSendSnapshot` (T-P05)
- `SendSnapshotToOwner` через `NetworkPlayer.ReceiveSkillsSnapshotTargetRpc` (reflection-based stub — `NetworkPlayer` ещё не имеет этого RPC, ожидается stub-рефакторинг)

**Вердикт:** **переиспользуем без изменений**. `ApplySkillEffects` сейчас no-op (`06_SKILL_TREE.md §3 §334-338` — просто `learned.Add(skillId)`). **Место для новых effect types** — добавить handler'ы в `ApplySkillEffects` (T-CB07).

### 1.3 EquipmentServer + EquipSlot (T-P08..T-P09, ✅)

**Файлы:**
- `Assets/_Project/Scripts/Equipment/EquipSlot.cs` (реализован, T-P08)
- `Assets/_Project/Scripts/Equipment/EquipmentData.cs` (реализован)
- `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` (реализован, T-P09)
- `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` (реализован, T-P07) — **будет расширен `armorDefense` (T-CB06, ERPR)**
- `Assets/_Project/Scripts/Equipment/ModuleItemData.cs` (реализован, T-P07)

**Что есть:**
```csharp
public enum EquipSlot : byte {
    None = 0,
    Head = 1, Chest = 2, Legs = 3, Feet = 4, Back = 5, Hands = 6,
    Accessory1 = 7, Accessory2 = 8,
    WeaponMain = 9, WeaponOff = 10,
    Module1 = 20, Module2 = 21, Module3 = 22
}
// EquipmentData: byte[SLOT_COUNT] slotOccupied + int[SLOT_COUNT] slotItemIds, INetworkSerializable
```

**В `TryEquip` уже есть валидация:**
- Item is clothing/module (instance check)
- Slot match
- `requiredSkills[]` — hard/soft (Q2.3 = c, см. `09_OPEN_QUESTIONS.md`)
- Item ownership in inventory
- Slot empty / unequip-first

**Вердикт:** **`EquipSlot.WeaponMain` / `WeaponOff` УЖЕ объявлены** и попадут в SLOT_COUNT. Не нужно трогать enum или EquipmentData. **Нужно:**
- (а) создать `WeaponItemData` (extends `ItemData`, как `ClothingItemData` / `ModuleItemData`) — **+ 3 ERPR-поля** (T-CB03)
- (б) расширить `TryEquip` чтобы принимал `WeaponItemData` (сейчас только clothing/module)
- (в) переиспользовать существующую `requiredSkills` логику — **без изменений в API**
- (г) **НОВОЕ (T-CB06)**: добавить `armorDefense` в `ClothingItemData` — для Defense-ветки (ERPR-пакет)

### 1.4 Stats + ItemData + Inventory (T-P01..T-P06 + Items/Core, ✅)

**Файлы:**
- `Assets/_Project/Scripts/Stats/StatsConfig.cs` (реализован, T-P01)
- `Assets/_Project/Scripts/Core/ItemType.cs` (35 строк, реализован)
- `Assets/_Project/Items/Core/InventoryWorld.cs` (666 строк, реализован)
- `Assets/_Project/Items/Core/ItemRegistry.cs` (120 строк, реализован)

**Что есть:**
- `enum ItemType { Resources=0, Equipment=1, Food=2, Fuel=3, Antigrav=4, Meziy=5, Medical=6, Tech=7 }` — **уже различает Antigrav и Meziy как тип предметов**
- `ItemData` базовый SO: `itemName, itemType, description, icon, maxStack, weightKg`
- `InventoryData` (struct) — 8 `List<int>` по одному на каждый ItemType
- `ItemRegistry.RegisterItem` — preferred stable ids

**Вердикт:** **переиспользуем**:
- `WeaponItemData` наследуем от `ItemData` (паттерн Clothing/Module) → `itemType = ItemType.Equipment`
- Антигравийное оружие → `itemType = ItemType.Antigrav` (уже есть)
- Мезиевое оружие → **см. §3.3** (ItemType.Meziy коллизия)

### 1.5 CharacterWindow + skills UI (T-P14..T-P18, ✅)

**Файлы:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (~3400 строк, реализован)
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` (реализован)

**Что есть:**
- Sub-tab «НАВЫКИ» внутри «ПРОГРЕССИЯ»
- `skills-combat-list` + `skills-social-list` (ListView)
- `MakeManualSkillRow` / `BindManualSkillRow` — pattern row factory
- Filter by category: `category == Combat` → combat-list, иначе → social-list
- CHANGELOG: *«Skills click handlers deferred до battle system integration»*

**Вердикт:** **для MVP не трогаем UI** — фильтр по `category == Combat` уже работает. В Phase 2 (T-CB09) добавим второй уровень фильтрации внутри combat-таба по `CombatDiscipline`. Существующий click handler (`btn.clicked += ... RequestLearnSkill`) уже подключён — базовый learn flow работает.

### 1.6 WorldEventBus + существующие publishers (✅)

**Файлы:**
- `Assets/_Project/Core/WorldEventBus.cs` (82 строки, реализован)
- `Assets/_Project/Core/WorldEvent.cs` (154 строки, реализован)

**Что есть (для нас):**
- `WorldEventBus.Publish<T>(T ev)` — static singleton, type-routed
- Существующие event types: ItemAdded/Removed, ReputationChanged, NpcAttitudeChanged, DialogVisited, DayNightPhaseChanged, ContractAccepted/Completed/Failed

**Вердикт:** когда combat-движок (отдельная подсистема) появится, он будет публиковать `AttackLandedEvent`, `WeaponFiredEvent`, `DamageDealtEvent`, `AntigravPushedEvent` и т.п. **SkillsServer** в будущем сможет **подписываться** на эти события для runtime-проверки «навык X требует, чтобы у игрока было оружие Y в руке» (T-CB07 forward-dep). **Сейчас — не нужно**; `ApplySkillEffects` чисто синхронный (skill learned → флаг выставлен → эффект применён).

---

## 2. Чего НЕТ (gaps) — обновлено под ERPR

| # | Gap | Где должно быть | Что делаем |
|---|---|---|---|
| G1 | `WeaponItemData` SO + **3 ERPR-поля** | `Assets/_Project/Scripts/Equipment/` | спецификация в `10_DESIGN.md §3.1` — `damageDice`, `baseDamage`, `critModifier` |
| G2 | `ExplosiveItemData` SO | там же | спецификация в `10_DESIGN.md §3.2` |
| G3 | `ClothingItemData.armorDefense` поле | `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | **НОВОЕ (T-CB06)**: для Defense-ветки, ERPR-пакет |
| G4 | `HitLocation` enum | `Assets/_Project/Scripts/Combat/HitLocation.cs` | **НОВОЕ (T-CB10/T-TB10)**: для Combat-движка + turn-based-battles |
| G5 | Damage формула (ERPR-based) | Combat-движок | спецификация в `10_DESIGN.md §7` |
| G6 | Combat-навыки (новые .asset) | `Assets/_Project/Resources/Skills/Combat/` | 5 веток × 5-7 нод = ~25-30 нод, в `20_SKILL_TREES.md` |
| G7 | Combat-движок (real-time hit/damage/projectile) | **отдельная подсистема** | НЕ в этом scope; см. `docs/Character/Skills/turn-based-battles/` |
| G8 | NPC-противники (HostileNPC, Faction AI) | **отдельная подсистема** | НЕ в этом scope |
| G9 | Damage-types система (Physical/Ballistic/Antigrav/Explosive/Mesium) | Combat-движок (real-time) / TB-battles | `DamageType` enum в WeaponItemData — см. `10_DESIGN.md §3.1` |
| G10 | Combat-анимации, hit-feedback | **отдельная подсистема** | future |
| G11 | PvP / дуэли / враждебные фракции | **отдельная подсистема (TB-battles)** | см. `docs/Character/Skills/turn-based-battles/30_SCENARIOS.md` |

**Вердикт:** 4 из 11 gaps — это **другие подсистемы** вне нашего scope (combat-движок real-time, NPC-AI, анимации, PvP через TB). Навыки дают **разблокировки, стат-бонусы, damage-параметры** (ERPR-пакет), а реальный бой — отдельный engine + `turn-based-battles/`.

---

## 3. Расхождения и конфликты

### 3.1 Существующие Combat-навыки (placeholder'ы из 06_SKILL_TREE.md §1.3)

Из `06_SKILL_TREE.md §1.3` Combat-навыки:

| skillId | Category | Prereq | Effects | XP | INT tier |
|---|---|---|---|---|---|
| `Skill_Combat_BasicStrike` | Combat | none | StatMod(STR+2) | 0 | 0 |
| `Skill_Combat_DodgeRoll` | Combat | none | StatMod(DEX+3) | 0 | 0 |
| `Skill_Combat_HeavySwing` | Combat | BasicStrike | StatMod(STR+5, ×1.2) | 100 | 2 |
| `Skill_Combat_PrecisionStrike` | Combat | DodgeRoll, HeavySwing | StatMod(DEX+5, ×1.3) | 200 | 4 |

**Проблема:** это всё **обобщённые** бонусы к статам, не привязанные к оружию. Игрок, выучивший `HeavySwing`, получает `STR+5` — но непонятно, с каким оружием этот удар, как он анимируется, какой у него damage-тип.

**Решение:** **НЕ УДАЛЯЕМ** эти 4 навыка. Они остаются как «базовые дисциплина-агностик» навыки (free-form бонусы к статам, срабатывающие в любом бою). Добавляем **5 новых веток** с **привязкой к оружию + ERPR damage-параметрами** (см. `20_SKILL_TREES.md`):
- BasicStrike / DodgeRoll остаются **корнями** дерева (бесплатные, без prereq, для всех дисциплин)
- HeavySwing, PrecisionStrike → пересматриваем: переносим в **Melee** подветку, делаем их prereq-зависимыми от нового `Skill_Melee_BasicSword`
- Новые 5 дисциплин — самостоятельные ветки с damage dice/crit (ERPR)

### 3.2 Расхождение с GDD 20 (Progression & RPG System)

**Файл:** `docs/gdd/GDD_20_Progression_RPG.md` (764 строки, статус «запланировано» в GDD_INDEX)

**Что описывает GDD 20:**
- **50 уровней** (Level 1..50), 4 базовые стата: Endurance / Navigation / Mechanics / Luck
- **3 ветки** навыков: Пилот / Торговец / Исследователь
- +1 Skill Point за уровень
- 4 корабля, milestones каждые 5/10 уровней
- Prestige System (50+)

**Что реализовано в v2 character progression (T-P01..T-P18):**
- **3 стата персонажа** (Сила/Ловкость/Интеллект) — НЕ Endurance/Navigation/Mechanics/Luck
- **геометрический `tier`** (формула `baseXp * growthRate^tier`) — НЕ классический Level 1..50
- **2 ветки** навыков (Combat/Social) — НЕ Pilot/Merchant/Explorer
- INT-pool для `LearnXpCost` (skill learn) — НЕ Skill Point за уровень

**Конфликт:**
- GDD 20 — **корабельный** бой, Pilot/торговец/исследователь (per-ship, в `ShipController` scope)
- v2 character progression — **пехотный** бой, Combat/Social (per-character, в `NetworkPlayer` scope)
- Это **разные сущности**: пилот корабля ≠ пехотинец; статы корабля (Endurance) ≠ статы персонажа (Сила)

**Решение:**
- ❌ **Не трогаем GDD 20** (gdd/ read-only по AGENTS.md)
- ✅ Combat-навыки проектируем как **пехотное расширение** существующего `06_SKILL_TREE.md` (Сила/Ловкость/Интеллект, геометрический tier)
- 📝 Зафиксировать в `00_README.md` и здесь: **GDD 20 остаётся vision doc** для корабельного прогресса, реализуется отдельной подсистемой. **v2 character progression — отдельная подсистема пехоты**, не зависит от GDD 20.

### 3.3 ItemType.Meziy — коллизия смысла

**Из `ItemType.cs` (35 строк):**
```csharp
public enum ItemType : byte {
    Resources=0, Equipment=1, Food=2, Fuel=3,
    Antigrav=4, Meziy=5, Medical=6, Tech=7
}
```

**Сейчас в проекте (по `docs/Character/Character-menu/sub_inventory-tab/40_CHANGES_SUMMARY.md`):**
- `Item_Fuel_Антигравитационное_топливо` → `ItemType.Fuel` (НЕ `Antigrav`)
- `Item_Antigrav_Антиграв_камень_малый` (если есть) → `ItemType.Antigrav`
- Мезий сейчас: скорее всего сырой ресурс/топливо, не оружие

**Проблема:** когда мы введём `WeaponItemData` с `itemType = ItemType.Meziy` (мезиевое стрелковое), это **семантически смешает**:
- Мезий-газ-ресурс (топливо, взрывчатка-сырьё)
- Мезий-оружие (мезиевая винтовка, мезиевый разрядник)

**Решение (варианты):**
- (a) **Разделить ItemType.Meziy** на `MeziyResource` (сырьё) и `MeziyWeapon` (оружие). Миграция существующих .asset — автоматическая по itemName, но ломает `InventoryData` (8 списков → 9).
- (b) **Не вводить `ItemType.Meziy` для оружия** — оставить `WeaponItemData` с `itemType = ItemType.Equipment`, а мезиевую природу обозначать **отдельным полем** `WeaponSubType.MeziyBased` (см. `10_DESIGN.md §3.1`).
- (c) **Игнорировать коллизию** — игрок не фильтрует инвентарь по `itemType == Meziy`, так что смешение не видно.

**Вердикт:** вариант **(b)** — самый чистый. `ItemType` остаётся broad category, оружие-специфика — в `WeaponItemData.weaponClass`. Открытое решение для пользователя в `30_PITFALLS_AND_OPEN_QUESTIONS.md §2.3`.

### 3.4 AbilityUnlock — текущее использование

В существующем `SkillEffect.Type.AbilityUnlock` (T-P11) — `stringParam` = ability id. **Сейчас в коде нет ни одного consumer** этого type (combat-engine не существует).

**Решение:** переиспользуем для `WeaponTechniqueUnlock` (отдельный Type, чтобы не смешивать семантику) и оставляем `AbilityUnlock` для будущих spell-like abilities (которые в лоре отсутствуют, но в коде enum сохраняем).

### 3.5 Стат-бонусы (StatMod) на combat-навыках

Сейчас `BasicStrike = +2 STR`, `DodgeRoll = +3 DEX` и т.п. **В рамках новой системы**:
- Стат-бонусы **остаются** (StatMod) — это generic stat bonuses, не привязанные к оружию.
- Дополнительно появляются **effect-typs** (`WeaponProficiencyUnlock`, `WeaponTechniqueUnlock` и т.п.) — для разблокировок.
- **НОВОЕ (ERPR)**: навыки могут также содержать **damage-параметры** — `damageDice`, `baseDamage`, `critModifier`. Эти параметры применяются к оружию, **которое** игрок экипирует (а не к самому навыку). Например, `melee_basic_sword` не имеет своего урона — он **разблокирует** `WeaponClass.Sword`; само оружие `Weapon_SteelSword` имеет `damageDice = d6, baseDamage = 3`. См. §3.7 ниже.
- Один skill может иметь **массив effects**: `[{StatMod, STR+2}, {WeaponProficiencyUnlock, "sword"}]`.

**Вердикт:** без изменений. `SkillEffect[]` уже массив — это естественно ложится. ERPR-пакет **добавляет** параметры в `WeaponItemData`, не в `SkillNodeConfig`.

### 3.6 Pitfall: Combat-навыки не должны ломать социальные

Социальные навыки (`Skill_Social_BasicTalk`, `Skill_Social_Barter` и т.д.) уже созданы и работают (T-P11). Расширение `SkillEffect.Type` (добавление `WeaponProficiencyUnlock` и др.) **не должно** ломать социальные эффекты. Все 4 Type (StatMod, AbilityUnlock, PassiveEffect + новые) **дополняют** друг друга, никакой не deprecate.

### 3.7 Pitfall: Pitfall #30 из design-doc-session — forward-declare stubs

При реализации T-CB01..T-CB08 **в одной сессии** есть риск: расширили `SkillEffect.Type`, добавили `WeaponItemData`, но обработка `WeaponProficiencyUnlock` появится только в T-CB07. **Между** T-CB01 и T-CB07 код не компилируется. Решение: **stub-обработчик** (default case → `Debug.LogWarning("[SkillsServer] Unimplemented effect type")`) — pattern из существующего `ApplySkillEffects` (который сейчас no-op).

### 3.8 `RequiredIntelligenceTier` — не лучший gate для Combat

`SkillNodeConfig` имеет `RequiredIntelligenceTier` — gate по INT. Для Combat-навыков это работает (placeholder'ы используют), но **не идеально**: для чисто Melee-навыка хочется gate по **STR** или **DEX**, не INT.

**Решение (варианты):**
- (a) Оставить только `RequiredIntelligenceTier` (текущее) — все Combat-навыки требуют INT. Просто, но не соответствует интуиции («силач» не обязан быть умным).
- (b) Расширить `SkillNodeConfig` — `StatRequirement[]` (любой стат). Меняет API, ломает обратную совместимость 8 существующих .asset (но .asset-сериализация совместима, если поле опционально).
- (c) Оставить `RequiredIntelligenceTier` для базовых Combat (BasicStrike/DodgeRoll), ввести **WeaponTechniqueUnlock effect type** для gating у продвинутых — техника разблокируется только если `STR.tier >= N` (проверка в `ApplySkillEffects`).

**Вердикт:** вариант **(c)** — наименее invasive. `RequiredIntelligenceTier` остаётся, gate по STR/DEX — внутри effect handler. Подробности в `10_DESIGN.md §4.2`.

### 3.9 Категория «Combat» не различает дисциплины

Сейчас `SkillCategory.Combat` — один bucket. Игрок видит плоский список 4+ combat-нодов. Когда добавим 25-30 нод по 5 дисциплинам — UI станет шумным.

**Решение:** новое поле `CombatDiscipline discipline` в `SkillNodeConfig` (опциональное, default = `None` для старых .asset). В UI (T-CB09) — второй уровень фильтра: внутри combat-sub-tab — `Melee / Ranged / Explosives / Antigrav / Defense / All`. Backward-compat: 4 существующих .asset (Combat) получат `discipline = None` и попадут в «All».

### 3.10 ERPR-пакет — что добавляем, что НЕ переносимо

**Подробный разбор:** `ERPR_collaboration.md`. Кратко:

| ERPR-элемент | Переносимо? | Где в Project C |
|---|---|---|
| Damage dice 1dN (d6/d8/d10/d12/d20) | ✅ | `WeaponItemData.damageDice` (T-CB03) |
| `baseDamage` оружия | ✅ | `WeaponItemData.baseDamage` (T-CB03) |
| `critModifier` (1d100+mod>=100 → crit) | ✅ | `WeaponItemData.critModifier` (T-CB03) |
| Hit location 1d4 (×0.5/1/2) | ✅ опционально | `HitLocation` enum (T-CB10/T-TB10) |
| Сила/Ловкость/Интеллект как модификаторы | ✅ 1:1 | `StatsConfig` (уже есть) |
| Защита от экипировки | ✅ | `ClothingItemData.armorDefense` (T-CB06) |
| **Сетка квадратов/гексов** | ❌ | только в `turn-based-battles/` |
| **3 Секунды на ход (пошаговость)** | ❌ | только в `turn-based-battles/` |
| **20 ОЗ + "судьба у ГМ"** | ❌ | persistent health, MMO |
| **ГМ-свобода** | ❌ | сервер-авторитативный |
| **Магия** (стихия воды) | ❌ ЗАПРЕЩЕНО | lore «без магии» |
| **ОД = 100/день** | ❌ | respawn-loop MMO |

**Вердикт:** принят **вариант B** (Combat-навыки + ERPR-пакет). 70% ядра ERPR переносимо. 30% — нет. Подробности в `ERPR_collaboration.md §6-7`.

---

## 4. Сводка рисков (обновлено под B)

| # | Риск | Severity | Mitigation |
|---|---|---|---|
| R1 | Расширение `SkillEffect.Type` ломает существующий `ApplySkillEffects` (no-op) | low | T-CB07 вводит switch по Type; default → stub warning |
| R2 | Новые SO (`WeaponItemData`) — нестандартный extends ItemData, могут не зарегистрироваться в `ItemRegistry` | medium | Копия `RegisterEquipmentAssets()` pattern из T-P07 (см. `01_CURRENT_STATE_AUDIT.md §1.1.10`) |
| R3 | `EquipSlot.WeaponMain/Off` уже есть, но `EquipmentData.SLOT_COUNT` рассчитан на 13 (Head..Module3) — новых слотов не нужно | none | 0 — слоты уже есть |
| R4 | `CharacterWindow` skills UI — фильтр по category уже работает; новый фильтр по discipline (T-CB09) — Phase 2 | low | MVP = текущий плоский список combat |
| R5 | Combat-движок не существует → навыки **только разблокируют** классы/техники/рецепты, реальный бой — future | accepted | Это явно зафиксировано в `00_README.md §НЕ делаем`. Real-time — отдельная подсистема. Turn-based — `turn-based-battles/`. |
| R6 | ItemType.Meziy коллизия (см. §3.3) | low | Вариант (b) — `WeaponItemData.weaponClass` (отдельное поле) |
| R7 | GDD 20 расхождение (см. §3.2) | low | Не трогаем GDD 20, фиксируем расхождение в README |
| R8 (NEW) | ERPR-пакет: `damageDice/baseDamage/critModifier` могут быть не заполнены designer'ом → дефолты должны быть безопасными | low | T-CB03 устанавливает дефолты: `damageDice = d6`, `baseDamage = 1`, `critModifier = 0` |
| R9 (NEW) | ERPR-пакет: `armorDefense` в `ClothingItemData` — старые .asset (5 шт) получат `armorDefense = 0` | low | Миграция автоматическая (Unity default), defense-формула проверит: `if (clothing.armorDefense > 0) ...` |
| R10 (NEW) | ERPR-пакет: формула `(STR + 1dN + base) × hitLocation × crit` — нужно избежать over-stacking навыков (multiplier × crit × skillBonus одновременно) | medium | T-CB08: документировать максимальный множитель per-цепочка (например, Melee Tier 2 = ×1.5 max) |
| R11 (NEW) | Turn-based-battles: гипотетический Gamedesigner-feature-creep — TB может попытаться стать «основной» боевой системой, а не мини-игрой | medium | В `turn-based-battles/00_README.md` явно: TB = **mini-game**, не replacement для real-time combat |

---

## 5. Что НЕ делаем в этой сессии (явные запреты)

- ❌ Не пишем код (research + design-doc only)
- ❌ Не модифицируем `docs/gdd/`
- ❌ Не модифицируем `docs/WORLD_LORE_BOOK.md`
- ❌ Не пишем .meta / .asmdef
- ❌ Не запускаем `run_tests` MCP
- ❌ Не делаем git commit / push
- ❌ Не проектируем combat-движок (hit, damage, projectile) — отдельная подсистема (см. `turn-based-battles/`)
- ❌ Не проектируем NPC-AI / Faction AI
- ❌ Не проектируем PvP (через TB-battles — см. `turn-based-battles/30_SCENARIOS.md`)
- ❌ Не вводим Skill Point / Level систему (GDD 20 фича, не наш scope)
