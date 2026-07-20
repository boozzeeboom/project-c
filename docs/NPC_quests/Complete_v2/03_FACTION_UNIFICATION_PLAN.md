# 🔗 Faction Unification Plan — NpcFaction → FactionDefinition

**Дата:** 2026-07-29 | **Статус:** In Progress — Stages A-D complete, E-F pending | **Зависит от:** Complete_v2 G1-G12 (в процессе)

---

## 0. ПРОБЛЕМА

Сейчас в проекте **две независимые системы фракций**, которые никак не связаны:

| | Квестовая (FactionDefinition) | Боевая (NpcFaction) |
|---|---|---|
| **ID** | `enum FactionId` (int) | `string factionId` |
| **Ассетов** | 11 | 5 |
| **Где** | `Quests/Data/Factions/` | `Resources/AI/` |
| **Данные** | displayName, color, tiers, lore | description, relations матрица |
| **Используется** | QuestWorld, NpcDefinition, UI | NpcSocialBrain, NpcBrain, Spawner |

**Пересечение:** только Pirates = pirates. Остальные 10 vs 4 — полностью разрознены.

**Цель:** дизайнер создаёт **1 ассет** = 1 фракция. Она работает и в социальном, и в боевом контексте.

---

## 1. АРХИТЕКТУРНОЕ РЕШЕНИЕ

**FactionDefinition — каноническая база.** NpcFaction — депрекейтится.

Почему FactionDefinition:
- Уже содержит богатый набор данных (displayName, color, lore, tiers)
- Уже использует канонический `FactionId` enum (15 значений после добавления)
- В коде явно помечен как "replaces the v1 NpcFaction runtime usage" (FactionDefinition.cs:49)
- NpcFaction — это только `string factionId` + матрица отношений — что легко перенести

**Что добавляем в FactionDefinition:**
- `defaultCombatRelation` — дефолтное боевое отношение к неизвестным фракциям
- `combatRelations[]` — матрица боевых отношений к конкретным фракциям (через `FactionCombatRelation`)
- Методы `GetCombatRelation()`, `IsHostileTowards()`, `IsAlliedWith()` — аналоги NpcFaction
- Свойство `CombatKey` — для VengeanceMemory (единый строковый ключ)

**Что удаляем:** NpcFaction.cs + 5 NpcFaction_*.asset (после полной миграции).

---

## 2. НОВЫЕ ЗНАЧЕНИЯ FactionId

Добавить в `FactionId.cs`:

```csharp
public enum FactionId
{
    None = 0,
    GuildOfThoughts = 1,
    GuildOfCreation = 2,
    GuildOfStrength = 3,
    GuildOfSecrets = 4,
    GuildOfSuccess = 5,
    Underground = 6,
    Resistance = 7,
    FreeTraders = 8,
    SOL_Patrol = 9,
    Pirates = 10,
    Neutral = 11,
    // === T-FACTION-UNIFY: новые значения из NpcFaction ===
    Bandits = 12,      // было NpcFaction_bandits (factionId="bandits")
    Cultists = 13,     // было NpcFaction_cultists (factionId="cultists")
    Guards = 14,       // было NpcFaction_guards (factionId="guards")
    Villagers = 15     // было NpcFaction_villagers (factionId="villagers")
}
```

> **Почему не удалили Pirates=10:** значение уже существует, NpcFaction_pirates мапится на него.

---

## 3. ИЗМЕНЕНИЯ В FactionDefinition

### 3.1 Новые поля

```csharp
[Header("Combat (T-FACTION-UNIFY)")]
[Tooltip("Боевое отношение по умолчанию к фракциям, не указанным в combatRelations.")]
public FactionRelation defaultCombatRelation = FactionRelation.Neutral;

[Tooltip("Боевые отношения с конкретными фракциями (кто враг, кто союзник).")]
public FactionCombatRelation[] combatRelations = Array.Empty<FactionCombatRelation>();

// T-FACTION-UNIFY: ключ для VengeanceMemory (PascalCase, напр. "Bandits").
// Используется вместо NpcFaction.factionId (который был lowercase "bandits").
// VengeanceMemory runtime-only — пересоздаётся при старте сервера, persisted-ключей нет.
public string CombatKey => factionId.ToString();
```

### 3.2 Новый тип (в отдельном файле `Quests/Factions/FactionRelation.cs`)

```csharp
namespace ProjectC.Factions
{
    /// <summary>Тип боевого отношения между двумя фракциями.</summary>
    public enum FactionRelation
    {
        Allied,   // Союзники: помогают в бою, не атакуют, делят alarm
        Neutral,  // Нейтральные: игнорируют, не атакуют, не помогают
        Hostile,  // Враждебные: атакуют при обнаружении
    }

    [Serializable]
    public struct FactionCombatRelation
    {
        [Tooltip("Целевая фракция.")]
        public FactionId targetFaction;

        [Tooltip("Тип боевого отношения.")]
        public FactionRelation relation;
    }
}
```

> **Почему отдельный файл:** FactionRelation теперь живёт в `ProjectC.Factions`, а не в `ProjectC.AI`. Отдельный файл — чище, чем вкладывать в FactionDefinition.cs.

### 3.3 Новые методы (аналоги NpcFaction)

```csharp
private Dictionary<FactionId, FactionRelation> _combatRelationCache;

public FactionRelation GetCombatRelation(FactionId other)
{
    if (other == factionId) return FactionRelation.Allied;
    BuildCombatCache();
    if (_combatRelationCache.TryGetValue(other, out var rel))
        return rel;
    return defaultCombatRelation;
}

public bool IsHostileTowards(FactionId other)
    => GetCombatRelation(other) == FactionRelation.Hostile;

public bool IsAlliedWith(FactionId other)
    => GetCombatRelation(other) == FactionRelation.Allied;

private void BuildCombatCache()
{
    if (_combatRelationCache != null) return;
    _combatRelationCache = new Dictionary<FactionId, FactionRelation>();
    foreach (var entry in combatRelations)
        _combatRelationCache[entry.targetFaction] = entry.relation;
}

private void OnEnable()
{
    _combatRelationCache = null; // T-FACTION-UNIFY: очистка при domain reload
}
```

### 3.4 🔄 Отличие от NpcFaction: string → FactionId

NpcFaction хранит relations как `string factionId + FactionRelation`. FactionDefinition использует `FactionId targetFaction` (enum). Это **ломает** строковые ключи VengeanceMemory:

| Что | NpcFaction (сейчас) | FactionDefinition (после миграции) |
|---|---|---|
| Ключ | `"bandits"` (lowercase) | `FactionId.Bandits.ToString()` = `"Bandits"` (PascalCase) |
| Vengeance | `faction.factionId` → `"bandits"` | `faction.CombatKey` → `"Bandits"` |

**Решение:** VengeanceMemory runtime-only — persisted-ключей на диск нет. PascalCase безопасен. `CombatKey` свойство даёт единую точку правки.

---

## 4. ПЛАН ПО ШАГАМ

### Этап A: Подготовка (infrastructure, 0 риска для runtime)

| Шаг | Файл | Что | Строк |
|-----|------|-----|-------|
| **A1** | `FactionId.cs` | +4 значения: Bandits=12, Cultists=13, Guards=14, Villagers=15 | +4 |
| **A1.5** | `Quests/Factions/FactionRelation.cs` (новый) | enum `FactionRelation`, struct `FactionCombatRelation` | +25 |
| **A2** | `FactionDefinition.cs` | + поля `defaultCombatRelation`, `combatRelations[]` | +6 |
| **A3** | `FactionDefinition.cs` | + свойство `CombatKey` | +2 |
| **A4** | `FactionDefinition.cs` | + методы `GetCombatRelation`, `IsHostileTowards`, `IsAlliedWith`, `BuildCombatCache` | +32 |
| **A5** | `FactionDefinition.cs` | + `OnEnable` → `_combatRelationCache = null` | +3 |
| **A6** | `NpcFaction.cs` | + `[Obsolete("Use FactionDefinition instead. T-FACTION-UNIFY")]` | +1 |

**Итого этап A:** ~73 строки, 0 сломанных ссылок, можно компилировать.

**Верификация A:**
- Открыть Unity Editor → Console: 0 errors
- Открыть `GuildOfCreation.asset` — новые поля `Default Combat Relation` и `Combat Relations` видны
- `FactionRelation` enum доступен в инспекторе

### Этап B: Миграция AI-кода на FactionDefinition

| Шаг | Файл | Изменение | Сложность |
|-----|------|-----------|-----------|
| **B1** | `NpcSocialBrain.cs` | `public NpcFaction faction` → `public FactionDefinition faction` + `using ProjectC.Factions;` | 2 строки |
| **B2** | `NpcSocialBrain.cs` | Все `faction.IsHostile(o.faction)` → `faction.IsHostileTowards(o.faction.factionId)` | ~6 мест |
| **B3** | `NpcSocialBrain.cs` | Все `faction.IsAllied(o.faction)` → `faction.IsAlliedWith(o.faction.factionId)` | ~4 места |
| **B4** | `NpcSocialBrain.cs` | `faction.factionId` (string) → `faction.CombatKey` (для VengeanceMemory) | ~2 места |
| **B5** | `NpcSocialBrain.cs` | `faction.factionId` в логах → `faction.factionId.ToString()` | ~3 места |
| **B6** | `NpcSocialBrain.cs` | `faction != null` — guard остаётся (поле теперь FactionDefinition, не NpcFaction) | 0 изменений |
| **B7** | `NpcBrain.cs` | `_socialBrain.faction.IsHostile(npc.faction)` → `_socialBrain.faction.IsHostileTowards(npc.faction.factionId)` | 1 место |
| **B8** | `NpcGroupController.cs` | `members[0].faction.IsAllied(brain.faction)` → `members[0].faction.IsAlliedWith(brain.faction.factionId)` | ~4 места |
| **B9** | `NpcSpawnerConfig.cs` | `public NpcFaction faction` → `public FactionDefinition faction` | 1 строка |
| **B10** | `NpcSpawnerConfigEditor.cs` | Поле `_faction` остаётся (тип изменился, SerializedProperty автоматический) | 0 строк |
| **B11** | `NpcSocialBrainEditor.cs` | Поле `_faction` остаётся (аналогично) | 0 строк |

**Итого этап B:** ~20 изменённых строк, 5 файлов. **Критично:** ни одно имя метода не конфликтует — старые `IsHostile(NpcFaction)` и новые `IsHostileTowards(FactionId)` — разные сигнатуры.

**Верификация B:**
- Компиляция 0 errors (кроме [Obsolete] warnings на NpcFaction)
- Все NpcSocialBrain в сцене показывают `faction` поле как FactionDefinition (сейчас там NpcFaction → инспектор покажет Missing/None — норм, до этапа D)

### Этап C: Создание FactionDefinition для новых фракций

| Шаг | Что | Где |
|-----|-----|-----|
| **C1** | `Faction_Bandits.asset` | `Assets/_Project/Quests/Data/Factions/` |
| | factionId=Bandits, displayName="Бандиты", defaultCombatRelation=Hostile, defaultAttitude=Hostile | |
| **C2** | `Faction_Cultists.asset` | Там же |
| | factionId=Cultists, displayName="Культисты", defaultCombatRelation=Hostile, defaultAttitude=Hostile | |
| **C3** | `Faction_Guards.asset` | Там же |
| | factionId=Guards, displayName="Стража", defaultCombatRelation=Neutral, defaultAttitude=Neutral | |
| **C4** | `Faction_Villagers.asset` | Там же |
| | factionId=Villagers, displayName="Жители", defaultCombatRelation=Neutral, defaultAttitude=Neutral | |
| **C5** | `Pirates.asset` (существующий) | Обновить — добавить defaultCombatRelation=Hostile, defaultAttitude=Hostile |
| | combatRelations: Bandits=Allied, Cultists=Allied, Guards=Hostile, Villagers=Hostile | |

> **Важно:** Pirates.asset НЕ создаём новый — обновляем существующий ассет в `Quests/Data/Factions/Pirates.asset`.

### Этап D: Editor-скрипт миграции ссылок

Создать `Assets/_Project/Editor/Migration/FactionMigrationTool.cs`:

```
Меню: Tools → ProjectC → Migration → Migrate NpcFaction → FactionDefinition

Алгоритм:
1. Собрать mapping: NpcFaction.factionId → FactionDefinition.factionId
   "bandits"→Bandits, "cultists"→Cultists, "guards"→Guards,
   "pirates"→Pirates, "villagers"→Villagers
   (ключ: словарь <string, FactionDefinition> по имени ассета)

2. Для каждого NpcSpawnerConfig.asset (ищет по guid паттерну NpcFaction_*):
   - Загрузить через AssetDatabase.LoadAssetAtPath<NpcSpawnerConfig>
   - Сериализовать: SerializedObject → FindProperty("faction") → objectReferenceValue
   - Сопоставить старый guid NpcFaction → новый FactionDefinition
   - Присвоить objectReferenceValue = новый FactionDefinition
   - ApplyModifiedProperties + AssetDatabase.SaveAssets

3. Если у NpcSocialBrain в префабах/сценах есть прямые ссылки на NpcFaction:
   - Пройтись по всем префабам Resources.FindObjectsOfTypeAll<NpcSocialBrain>
   - То же самое: заменить faction reference

4. Вывести отчёт: что обновлено (N), что пропущено (0)
```

**Почему через SerializedObject, не через replace-in-text:** .asset файлы содержат fileID+guid. Прямая замена текста сломает fileID. SerializedObject корректно обновляет fileID при смене reference.

### Этап E: Верификация

| Шаг | Что | Как |
|-----|-----|-----|
| **E1** | Компиляция | 0 errors, 0 warnings (кроме Obsolete на NpcFaction) |
| **E2** | NpcSocialBrain в инспекторе | Поле `faction` теперь `FactionDefinition`, дропдаун работает с новыми ассетами |
| **E3** | NpcSpawnerConfig в инспекторе | Поле `faction` теперь `FactionDefinition` |
| **E4** | Запуск сцены | NPC корректно определяют врагов/союзников |
| **E5** | VengeanceMemory | `CombatKey` ("Bandits") корректно работает в логах |
| **E6** | Quest-сторона | Репутация, диалоги — без изменений (FactionId enum не сломан) |
| **E7** | Pirates | Существующий NpcFaction_pirates.asset больше не назначен нигде (только старый NpcSpawner_Quest, если не мигрирован) |
| **E8** | Resources.Load | Проверка: `Resources.Load<NpcFaction>("...")` — 0 вхождений в коде проекта |

### Этап F: Очистка (после успешной верификации)

| Шаг | Что |
|-----|-----|
| **F1** | Удалить `NpcFaction.cs` |
| **F2** | Удалить `NpcFaction.cs.meta` |
| **F3** | Удалить `Assets/_Project/Resources/AI/NpcFaction_*.asset` (5 файлов + .meta) |
| **F4** | Убрать `[Obsolete]` атрибутные warning'и из кода (если остались референсы на NpcFaction — не должно) |
| **F5** | Обновить документацию: `02_V2_ARCHITECTURE.md`, `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md` |

---

## 5. МАТРИЦА БОЕВЫХ ОТНОШЕНИЙ (целевая)

После миграции дизайнер заполняет в каждом FactionDefinition:

```
                    Bandits Cultists Guards Pirates Villagers Guilds* Neutral
Bandits               A       H       H       A?       H        H       H
Cultists              H       A       H       H        H        H       H
Guards                H       H       A       H        A        A       A
Pirates               A?      H       H       A        H        H       H
Villagers             H       H       A       H        A        N       N
GuildOfCreation       H       H       A       H        A        A       N
... (все 15×15)
```

Где: A=Allied, H=Hostile, N=Neutral.

> **Важно:** дизайнер настраивает `combatRelations` **per-faction**, а не в централизованной матрице. Это даёт гибкость: например, Pirates могут быть Hostile к GuildOfCreation, но Allied с Underground.

> **Совет:** Первыми настроить Bandits, Cultists, Guards, Villagers — у них сейчас есть relations в NpcFaction. Перенести эти данные в новые FactionDefinition ассеты.

---

## 6. ДЕТАЛИ МИГРАЦИИ (Gaps analysis от Mavis)

### 6.1 String case для VengeanceMemory

Обоснование: `enum.ToString()` → PascalCase. VengeanceMemory — runtime-словарь, не сохраняется на диск. PascalCase безопасен.

Свойство `CombatKey` на FactionDefinition — единая точка доступа. При желании можно переопределить потом:
```csharp
public virtual string CombatKey => factionId.ToString();
```

### 6.2 FactionRelation — отдельный файл в ProjectC.Factions

Не оставлять FactionRelation внутри NpcFaction.cs — он нужен и после удаления NpcFaction. Новый файл `Quests/Factions/FactionRelation.cs`:

```csharp
namespace ProjectC.Factions
{
    public enum FactionRelation { Allied, Neutral, Hostile }
    [Serializable]
    public struct FactionCombatRelation { ... }
}
```

После удаления NpcFaction.cs дубликат enum оттуда удаляется автоматически.

### 6.3 Зависимости namespace

```
ProjectC.AI (NpcBrain, NpcSocialBrain, NpcGroupController)
    └── использует ProjectC.Factions (FactionDefinition, FactionId, FactionRelation)
    └── НЕТ обратной зависимости
```

Никакой cyclic dependency. AI-код уже использует `ProjectC.Combat`, `ProjectC.Core` — `ProjectC.Factions` ещё один.

### 6.4 Проверка Resources.Load

Поиск `Resources.Load<NpcFaction>` и `Resources.LoadAll<NpcFaction>` — **0 вхождений**. Все референсы через инспектор. Удаление ассетов из `Resources/AI/` на этапе F безопасно.

### 6.5 Существующие NpcSpawnerConfig.asset

| Ассет | GUID NpcFaction | Фракция |
|-------|----------------|---------|
| `NpcSpawner_Default.asset` | `42a12103b0432c94eaf41010f0d6993f` | bandits (?) |
| `NpcSpawner_ship_deck.asset` | `d1ebf12d1b4b8c140894ec68927ccc28` | guards (?) |
| `NpcSpawner_neutral.asset` | `d1ebf12d1b4b8c140894ec68927ccc28` | guards (?) |
| `NpcSpawner_Quest.asset` | `9d04bc8ae32d31446b15ee6c18a4b916` | pirates (?) |

Editor-скрипт (Этап D) резолвит guid и заменяет на соответствующий FactionDefinition.

---

## 7. ОЦЕНКА ТРУДОЗАТРАТ

| Этап | Часов | Риск |
|------|-------|------|
| A (подготовка) | 0.5 | Нулевой |
| B (миграция AI-кода) | 1.5 | Низкий |
| C (создание ассетов) | 0.5 | Нулевой |
| D (editor-скрипт) | 2.0 | Средний |
| E (верификация) | 1.0 | — |
| F (очистка) | 0.3 | Низкий |
| **Итого** | **~6 часов** | |

---

## 8. ЧТО НЕ ДЕЛАЕМ (out of scope)

- ❌ Не трогаем `VengeanceMemory` — она работает со `string`, продолжит через `CombatKey`
- ❌ Не переделываем `FactionRelation` на `FactionAttitude` — это разные концепты (боевое vs социальное отношение)
- ❌ Не удаляем `FactionId` enum в пользу string — enum удобнее для редактора (дропдаун)
- ❌ Не создаём централизованную матрицу отношений — per-faction гибче
- ❌ Не добавляем runtime-изменение combatRelations через `SetRelation` — пока не требуется
- ❌ Не трогаем `NpcDefinition.faction` — он уже использует `FactionId`, остаётся как есть
- ❌ Не трогаем `QuestWorld` — его репутация по `FactionId` не меняется
- ❌ Не добавляем Scripting Define Symbol — достаточно `[Obsolete]` на NpcFaction

---

## 9. ПОРЯДОК ВЫПОЛНЕНИЯ (рекомендованный)

```
1. Этап A (подготовка) → компиляция → verify A
2. Этап C (ассеты) → создать 4 новых, обновить Pirates
3. Этап B (код AI) → миграция → компиляция → verify B
4. Этап D (editor-скрипт) → запустить миграцию → verify (инспекторы показывают FactionDefinition)
5. Этап E (верификация) → PlayMode тест NPC поведения
6. Этап F (очистка) → удалить NpcFaction.cs + 5 ассетов → финальная компиляция
```

> **Почему C раньше B:** Чтобы NpcSocialBrain и NpcSpawnerConfig могли сразу назначить FactionDefinition в инспекторе. Без C этапа B скомпилируется, но поля будут пусты.

---

## 10. РЕЗУЛЬТАТ

**После миграции дизайнер:**

1. Создаёт **один** `FactionDefinition` asset через `Create → ProjectC/Factions/Faction Definition`
2. Заполняет:
   - **Identity** (factionId, displayName)
   - **Visuals** (color, iconSprite) — для UI
   - **Lore** (loreDescription)
   - **Defaults** (defaultAttitude, reputationThresholds) — для социальной репутации
   - **Combat** (defaultCombatRelation, combatRelations) — для AI боевого поведения
3. Назначает фракцию NPC через `NpcDefinition.faction` (FactionId)
4. Назначает фракцию спавнеру через `NpcSpawnerConfig.faction` (FactionDefinition)
5. Назначает фракцию социальному мозгу через `NpcSocialBrain.faction` (FactionDefinition)

Всё. **Один ассет, одно место правки, одна правда.**

---

## 11. ИСТОРИЯ ИЗМЕНЕНИЙ

| Дата | Сессия | Изменения |
|------|--------|-----------|
| 2026-07-29 | Mavis v2 | **v2:** добавлены секции 6 (Gaps), 9 (порядок), 11 (история). A1.5 (FactionRelation.cs), CombatKey, уточнения по namespace, Resources.Load проверка, mapping спавнеров. План расширен без опровержения v1. |

*План составлен на основе полного анализа: FactionDefinition.cs (107 строк), NpcFaction.cs (135 строк), NpcSocialBrain.cs (976 строк), NpcBrain.cs (1227 строк), NpcGroupController.cs (470 строк), NpcSpawnerConfig.cs (201 строка), FactionId.cs (31 строка), NpcDefinition.cs (123 строки), QuestWorld.cs (1350 строк), VengeanceMemory.cs (193 строки), NpcAttitude.cs (55 строк), 11+5 faction assets, 2 editor скрипта, 4 spawner configs.*
