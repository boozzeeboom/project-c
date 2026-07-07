# План рефакторинга: Боевые дисциплины + Инспектор навыков

> **Дата:** 2026-07-26  
> **Статус:** 🔄 В реализации  
> **Прогресс:** Фаза A ✅ | Фаза B ✅ | Фаза C ✅ | Фаза D ✅ | Фаза E ✅ | Фаза F ⬜  
> **Инициировано:** выявлением архитектурной проблемы throwables (см. `90_RANGED_AND_THROWABLES.md §6.5`)

---

## TL;DR

**Проблемы:**
1. Инспектор `SkillNodeConfig` показывает все поля одновременно — дизайнер путается
2. Дисциплины `Explosives` и `Antigrav` не вписываются в архитектуру `WeaponHandling` (R1)
3. Нет явного концепта «бросаемые» (throwables) на уровне навыка — определяется хрупким pattern-matching
4. Нет полей для throw-специфичных параметров (дальность, разброс, кол-во)

**Решение:** 4 дисциплины (Melee/Ranged/Defense/Placed) + подтипы (Throwables) + CustomEditor с адаптивными секциями + новые throw-поля на `SkillNodeConfig`.

---

## 1. Анализ текущего состояния

### 1.1 `CombatDiscipline` enum (текущий)

| Значение | Навыки | Проблема |
|----------|--------|----------|
| `None` (0) | Social-навыки | OK |
| `Combat` (1) | DodgeRoll, BasicStrike, HeavySwing, PrecisionStrike | Универсальные — OK |
| `Melee` (2) | 5 навыков (sword, dagger, spear, dualwield, heavy_swing) | OK |
| `Ranged` (3) | 3 навыка (bow, crossbow_mastery, quick_reload) | OK |
| `Explosives` (4) | 3 навыка (grenade, mine, basic_bomb) | ❌ Смешивает throw (граната) и placed (мина) |
| `Antigrav` (5) | 3 навыка (aura, basic_pulse, shield) | ❌ Не оружие, а тип урона/технологии |
| `Defense` (6) | 4 навыка (basic, heavy, master, antigrav_shield) | OK |

### 1.2 `WeaponHandling` enum (R1, уже в коде)

```csharp
public enum WeaponHandling : byte
{
    Melee = 0,    // ближний бой
    Ranged = 1,   // дальний бой
    Thrown = 2,   // метательное (граната, нож, топор)
    Placed = 3,   // устанавливаемое (мина, ловушка)
}
```

**Вывод:** `WeaponHandling` уже задаёт правильную таксономию. `CombatDiscipline` должен ей соответствовать.

### 1.3 Текущая детекция throwables (SkillInputService.cs:371-375)

```csharp
bool isThrownAoe = skillConfig != null
    && skillConfig.isActive
    && (skillConfig.aoeFormula == AoeFormula.Sphere
        || skillConfig.aoeFormula == AoeFormula.Box)
    && skillConfig.aoeSize > 0f;
```

**Проблема:** любой Sphere/Box AOE определяется как «бросок». Антиграв-аура (Sphere, 4м) тоже попадёт под это условие. Нужен явный subtype/флаг.

### 1.4 Фильтры SkillTreeWindow (текущие)

```csharp
enum SkillDisciplineFilter { All, Melee, Ranged, Explosives, Antigrav, Defense }
```

6 фильтров = 6 старых дисциплин. После рефакторинга → 5 фильтров: `All, Melee, Ranged, Defense, Placed`.

### 1.5 Полный инвентарь существующих .asset навыков

| .asset | skillId | discipline (текущая) | → целевая | → подтип |
|--------|---------|---------------------|-----------|----------|
| Skill_Combat_BasicStrike | combat_basic_strike | Combat | Combat | None |
| Skill_Combat_BasicStrike 1 | (дубликат) | Combat | Combat | None |
| Skill_Combat_BasicStrike 2 | (дубликат) | Combat | Combat | None |
| Skill_Combat_DodgeRoll | combat_dodge_roll | Combat | Combat | None |
| Skill_Combat_HeavySwing | combat_heavy_swing | Combat | Combat | None |
| Skill_Combat_PrecisionStrike | combat_precision_strike | Combat | Combat | None |
| Skill_Melee_BasicSword | melee_basic_sword | Melee | **Melee** | None |
| Skill_Melee_DaggerMastery | melee_dagger_mastery | Melee | **Melee** | None |
| Skill_Melee_DualWield | melee_dual_wield | Melee | **Melee** | None |
| Skill_Melee_HeavySwing | melee_heavy_swing | Melee | **Melee** | None |
| Skill_Melee_PrecisionStrike | melee_precision_strike | Melee | **Melee** | None |
| Skill_Melee_SpearReach | melee_spear_reach | Melee | **Melee** | None |
| Skill_Ranged_BasicBow | ranged_basic_bow | Ranged | **Ranged** | None |
| Skill_Ranged_CrossbowMastery | ranged_crossbow_mastery | Ranged | **Ranged** | None |
| Skill_Ranged_QuickReload | ranged_quick_reload | Ranged | **Ranged** | None |
| Skill_Explosives_Grenade | expl_grenade | Explosives | **Ranged** | **Throwables** |
| Skill_Explosives_BasicBomb | expl_basic_bomb | Explosives | **Ranged** | **Throwables** |
| Skill_Explosives_Mine | expl_mine | Explosives | **Placed** | Traps |
| Skill_Antigrav_BasicPulse | antigrav_basic_pulse | Antigrav | **Defense** | None |
| Skill_Antigrav_Aura | antigrav_aura | Antigrav | **Defense** | None |
| Skill_Antigrav_Shield | antigrav_shield | Antigrav | **Defense** | None |
| Skill_Defense_BasicArmor | defense_basic | Defense | **Defense** | None |
| Skill_Defense_HeavyArmor | defense_heavy | Defense | **Defense** | None |
| Skill_Defense_Master | defense_master | Defense | **Defense** | None |
| Skill_Defense_AntigravShield | defense_antigrav_shield | Defense | **Defense** | None |
| Skill_Social_BasicTalk | social_basic_talk | None | None | None |
| Skill_Social_Barter | social_barter | None | None | None |
| Skill_Social_Leadership | social_leadership | None | None | None |
| Skill_Social_Persuasion | social_persuasion | None | None | None |

---

## 2. Целевая архитектура

### 2.1 Новый `CombatDiscipline` enum

```csharp
public enum CombatDiscipline : byte
{
    None = 0,      // social / non-combat
    Combat = 1,    // универсальные (DodgeRoll, BasicStrike)
    Melee = 2,     // ближний бой (мечи, копья, кинжалы)
    Ranged = 3,    // дальний бой (луки, арбалеты, throwables)
    Defense = 4,   // защита (броня, стойки, щиты, ауры)
    Placed = 5,    // устанавливаемое (мины, ловушки, турели)
}
```

**Изменения:**
- `Explosives (4)` → удалён. Гранаты → Ranged, мины → Placed.
- `Antigrav (5)` → удалён. Распределён по логике: blade→Melee, pulse→Ranged, aura/shield→Defense.
- `Placed (5)` — новый, соответствует `WeaponHandling.Placed`.

### 2.2 Новый `CombatSubtype` enum

```csharp
/// <summary>
/// Подтип боевого навыка внутри дисциплины.
/// Виден только дизайнеру в инспекторе. Игроку не показывается.
/// Определяет какие дополнительные поля активны в инспекторе.
/// </summary>
public enum CombatSubtype : byte
{
    None = 0,         // обычный навык (большинство)
    Throwables = 1,   // бросаемые предметы (гранаты, ножи, топоры)
    Traps = 2,        // ловушки/мины (пример подтипа, не фокусируемся)
    // Будущие: AntigravWeapon = 3, Shield = 4, ...
}
```

### 2.3 Новые поля на `SkillNodeConfig`

```csharp
[Header("Subtype (designer-only)")]
[Tooltip("Подтип внутри дисциплины. Определяет доп. настройки в инспекторе.")]
public CombatSubtype subtype = CombatSubtype.None;

[Header("Throwables (активно при subtype = Throwables)")]
[Tooltip("Максимальная дальность броска в метрах.")]
[Range(1f, 100f)] public float throwRange = 25f;

[Tooltip("Разброс броска: D6. Чем выше — тем точнее бросок. 1 = граната может взорваться в руках.")]
[Range(1, 6)] public int throwScatter = 3;

[Tooltip("Кол-во одновременно брошенных предметов. Умножает расход TROWN-предметов.")]
[Range(1, 10)] public int throwCount = 1;
```

### 2.4 Адаптивный инспектор (CustomEditor)

`SkillNodeConfigEditor : Editor` — показывает/скрывает секции по правилам:

| Условие | Показать |
|---------|----------|
| `category == Combat` | discipline, subtype, requiredWeaponMask |
| `isActive == true` | AOE Formula, AOE Size, AOE Cone Angle, AOE Width, Attack Clip |
| `subtype == Throwables` | throwRange, throwScatter, throwCount |
| `discipline == Placed` | (будущие placed-поля: placementRadius, triggerType, ...) |
| Всегда | Identity, Prerequisites, Effects, XP Cost, Tier Requirements |

---

## 3. Фазы реализации

### Фаза A: Enum-рефакторинг (SkillNodeConfig.cs)

**Файл:** `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs`

**A1.** Заменить `CombatDiscipline` enum:
- Удалить `Explosives = 4`, `Antigrav = 5`
- Добавить `Placed = 5`
- Перенумерация: `Defense = 4` (было 6)

**A2.** Добавить `CombatSubtype` enum (см. §2.2)

**A3.** Добавить новые поля: `subtype`, `throwRange`, `throwScatter`, `throwCount`

**A4.** Обновить `OnValidate()::AutoSetDisciplineFromPrefix()`:
- `expl_` / `explosives_` → больше не матчится. Будет задаваться вручную через миграцию.
- `antigrav_` → больше не матчится. Будет задаваться вручную.
- `defense_` → `Defense` (новое значение 4)
- Добавить: `placed_` → `Placed`

> ⚠️ **Backward-compat:** старые .asset сохранят `discipline = Explosives (4)`. При загрузке Unity выдаст warning «Enum value out of range». Нужна миграция в Фазе E.

### Фаза B: Миграция существующих .asset

**B1.** Создать Editor-скрипт `SkillAssetMigration.cs` с пунктом меню:
```
Project C > Skills > Migrate All Skill Assets to New Disciplines
```

**B2.** Логика миграции (по skillId prefix):

| skillId prefix | → discipline | → subtype |
|----------------|-------------|-----------|
| `combat_` | Combat (1) | None (0) |
| `melee_` | Melee (2) | None (0) |
| `ranged_` | Ranged (3) | None (0) |
| `expl_grenade` | Ranged (3) | Throwables (1) |
| `expl_basic_bomb` | Ranged (3) | Throwables (1) |
| `expl_mine` | Placed (5) | Traps (2) |
| `antigrav_aura` | Defense (4) | None (0) |
| `antigrav_shield` | Defense (4) | None (0) |
| `antigrav_basic_pulse` | Defense (4) | None (0) |
| `defense_` | Defense (4) | None (0) |
| `social_` | None (0) | None (0) |

**B3.** `AssetDatabase.SaveAssets()` после миграции.

### Фаза C: Custom Editor

**Файл:** `Assets/_Project/Editor/SkillNodeConfigEditor.cs` (новый)

**C1.** Создать `[CustomEditor(typeof(SkillNodeConfig))]` класс.

**C2.** Реализовать адаптивные секции:
```csharp
public override void OnInspectorGUI()
{
    serializedObject.Update();
    
    // Identity (всегда)
    EditorGUILayout.PropertyField(skillIdProp);
    EditorGUILayout.PropertyField(displayNameProp);
    EditorGUILayout.PropertyField(descriptionProp);
    EditorGUILayout.PropertyField(iconProp);
    
    // Category + Discipline (всегда)
    EditorGUILayout.PropertyField(categoryProp);
    EditorGUILayout.PropertyField(disciplineProp);
    
    // Subtype (только Combat)
    if (category == SkillCategory.Combat)
    {
        EditorGUILayout.PropertyField(subtypeProp);
        EditorGUILayout.PropertyField(requiredWeaponMaskProp);
    }
    
    // Prerequisites, Effects, Costs (всегда)
    // ...
    
    // Active/Passive
    EditorGUILayout.PropertyField(isActiveProp);
    
    // AOE + Animation (только isActive)
    if (isActiveProp.boolValue)
    {
        EditorGUILayout.PropertyField(aoeFormulaProp);
        // ... aoeSize, aoeConeAngleDeg, aoeWidth
        EditorGUILayout.PropertyField(attackClipProp);
        EditorGUILayout.PropertyField(attackClipSpeedProp);
    }
    
    // Throwables (subtype == Throwables)
    if (subtypeProp.enumValueIndex == (int)CombatSubtype.Throwables)
    {
        EditorGUILayout.PropertyField(throwRangeProp);
        EditorGUILayout.PropertyField(throwScatterProp);
        EditorGUILayout.PropertyField(throwCountProp);
    }
    
    serializedObject.ApplyModifiedProperties();
}
```

### Фаза D: SkillTreeWindow — новые фильтры

**Файл:** `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs`

**D1.** Заменить `SkillDisciplineFilter` enum:
```csharp
// Было:
enum SkillDisciplineFilter { All, Melee, Ranged, Explosives, Antigrav, Defense }
// Стало:
enum SkillDisciplineFilter { All, Melee, Ranged, Defense, Placed }
```

**D2.** Обновить `MatchesDisciplineFilter()` — матчить по новому `discipline` (не по prefix):
```csharp
bool MatchesDisciplineFilter(SkillNodeConfig s)
{
    return _activeFilter switch
    {
        SkillDisciplineFilter.All => true,
        SkillDisciplineFilter.Melee => s.discipline == CombatDiscipline.Melee,
        SkillDisciplineFilter.Ranged => s.discipline == CombatDiscipline.Ranged,
        SkillDisciplineFilter.Defense => s.discipline == CombatDiscipline.Defense,
        SkillDisciplineFilter.Placed => s.discipline == CombatDiscipline.Placed,
        _ => true
    };
}
```

**D3.** Обновить `BindChip()` вызовы — убрать chip-explosives, chip-antigrav; добавить chip-placed.

**D4.** Обновить UXML (если чипы привязаны по имени).

### Фаза E: Runtime-код

**Файл:** `Assets/_Project/Scripts/Skills/SkillInputService.cs`

**E1.** Заменить pattern-matching детекцию throwables:
```csharp
// Было (хрупкий heuristic):
bool isThrownAoe = skillConfig != null
    && skillConfig.isActive
    && (skillConfig.aoeFormula == AoeFormula.Sphere
        || skillConfig.aoeFormula == AoeFormula.Box)
    && skillConfig.aoeSize > 0f;

// Стало (явный subtype):
bool isThrownAoe = skillConfig != null
    && skillConfig.subtype == CombatSubtype.Throwables
    && skillConfig.isActive;
```

**E2.** Использовать `skillConfig.throwRange` вместо `GetActiveThrowableRange()` (которая ищет throwable в инвентаре):
```csharp
float throwRange = skillConfig.throwRange; // вместо GetActiveThrowableRange()
```

**E3.** Использовать `skillConfig.throwScatter` для расчёта разброса (D6 бросок):
```csharp
int scatterRoll = Random.Range(1, 7); // D6
float scatterFactor = Mathf.Clamp01((float)(scatterRoll - skillConfig.throwScatter) / 6f);
// scatterFactor > 0 → отклонение траектории
```

**E4.** Использовать `skillConfig.throwCount` для мульти-броска:
```csharp
int count = skillConfig.throwCount;
for (int i = 0; i < count; i++)
{
    Vector3 offsetTarget = targetPoint + scatterOffset * i;
    ThrowArcVisual.Fire(origin, offsetTarget, flightTime, aoeSize, color);
}
```

**E5.** Убрать `CombatDiscipline.Explosives` из проверки цвета дуги:
```csharp
// Было:
Color arcColor = skillConfig.discipline == CombatDiscipline.Explosives
    ? new Color(1f, 0.4f, 0.1f) : new Color(0.3f, 0.7f, 1f);
// Стало:
Color arcColor = skillConfig.subtype == CombatSubtype.Throwables
    ? new Color(1f, 0.4f, 0.1f) : new Color(0.3f, 0.7f, 1f);
```

**Файл:** `Assets/_Project/Scripts/Combat/Network/CombatServer.cs`

**E6.** `ResolveThrowableSourceFromInventory` / `ConsumeThrowableFromInventory` — без изменений (работают через `WeaponClass.Throwable` на предмете, а не через skill-дисциплину).

### Фаза F: Документация

**F1.** Обновить `docs/Character/Skills/Battle/20_SKILL_TREES.md` — заменить Explosives/Antigrav секции на Placed + описать subtype Throwables.

**F2.** Обновить `docs/Character/Skills/Battle/10_DESIGN.md` при необходимости.

**F3.** Обновить этот документ (`100_SKILL_REFACTOR_PLAN.md`) статусом по мере выполнения фаз.

---

## 4. Ответ на вопрос: CustomEditor vs пресеты

> «если реализация в едиторе не позволит делать адаптивное переключение — вернемся к пресетам»

**Рекомендация:** CustomEditor (`[CustomEditor(typeof(SkillNodeConfig))]`).

**Обоснование:**
- CustomEditor в Unity **полноценно поддерживает** адаптивное переключение через `EditorGUILayout.PropertyField` с условиями
- Все поля остаются в одном `SkillNodeConfig` — не нужно синхронизировать отдельные SO-пресеты
- Один `.asset` на навык = проще дизайнеру (не надо думать «какой пресет выбрать»)
- `OnValidate()` продолжает работать (авто-исправление discipline, cycle detection)
- Сериализация всех полей сохраняется даже когда они скрыты (backward-compat)

**Минусы подхода с пресетами:**
- Фрагментация: `ThrowablePreset`, `MeleePreset`, `PlacedPreset` — отдельные SO типы
- Дублирование общих полей (skillId, displayName, prerequisites, effects...)
- Сложность поиска/фильтрации: `Resources.LoadAll<SkillNodeConfig>` не соберёт разные типы
- Переключение типа навыка = удаление старого SO + создание нового

**Вывод:** CustomEditor — правильный путь.

---

## 5. Порядок выполнения

| # | Фаза | Файлы | Риск |
|---|------|-------|------|
| 1 | **A** — Enum refactor | `SkillNodeConfig.cs` | 🔴 Высокий: ломает существующие .asset до миграции |
| 2 | **B** — Asset migration | Editor-скрипт | 🟡 Средний: нужно протестировать на всех 29 .asset |
| 3 | **C** — CustomEditor | `SkillNodeConfigEditor.cs` | 🟢 Низкий: только Editor, не влияет на runtime |
| 4 | **D** — SkillTreeWindow | `SkillTreeWindow.cs` + UXML | 🟢 Низкий: UI-only |
| 5 | **E** — Runtime | `SkillInputService.cs`, `CombatServer.cs` | 🟡 Средний: меняет flow throw-навыков |
| 6 | **F** — Docs | `.md` файлы | 🟢 Низкий |

**Важно:** Фазы A+B должны выполняться **в одной сессии** (или B сразу после A до перекомпиляции, либо через меню-миграцию после перекомпиляции).

---

## 6. Открытые вопросы

- ✅ `antigrav_basic_pulse` → **Defense** (решено пользователем)
- ✅ `expl_mine` → **Placed + Traps** (подтип введён для примера, не фокусируемся)
- ✅ `throwScatter` → **D6 (1-6)** (чем выше значение в skill — тем точнее бросок)
- ❓ Как именно scatter влияет на траекторию? (Нужен отдельный дизайн-документ для throw-механики)

---

## 7. История изменений

| Дата | Автор | Изменения |
|------|-------|-----------|
| 2026-07-26 | Aura | Первичный план на основе анализа кода и документации |
| 2026-07-26 | Aura | Фаза A+B: enum refactor + миграция 29 .asset. Коммит `f6cfac5` |
