# Система Знаний V2 — Глубокий Анализ Расширения на Все Подсистемы

> **Статус:** Анализ завершён. Покрыты 4 области: CharacterWindow, Skills, Death/Respawn, Crafting.
> **Дата:** 2026-07-23
> **Базируется:** v1 реализована (`f0aae06`, T-KNOW: factions + NPCs), `01_KNOWLEDGE_SYSTEM_ANALYSIS.md`, `02_KNOWLEDGE_SYSTEM_DEEP_ANALYSIS.md`, `03_KNOWLEDGE_INTEGRATION_LOG.md`
> **Охват:** CharacterWindow (UI), Skills (combat + social), Death → knowledge loss, Crafting (recipe knowledge)

---

## 0. Итоговый Scope V2

V2 расширяет концепцию Knowledge с «фракции + NPC» (v1) на **все аспекты игры**:

| # | Подсистема | Что меняется | Сложность |
|---|-----------|-------------|-----------|
| 1 | **CharacterWindow** | Вкладка «Репутация» → «Знания»: единая knowledge-панель с подсекциями (Фракции, NPC, Навыки, Рецепты, Квесты) | 🟡 Medium |
| 2 | **Skills** | Каждый SkillNodeConfig получает поле `knowledgeUnlock` — как игрок открывает знание о навыке. SkillsWorld получает `_knownSkills` dictionary + client state | 🟡 Medium |
| 3 | **Death/Respawn** | При смерти игрок теряет часть знаний (конфигурируемый %). Вызывается из `PlayerTarget.TriggerDeathRespawn` | 🟢 Low |
| 4 | **Crafting** | RecipeData получает `knowledgeUnlock`. CraftingWorld получает `_knownRecipes`. UI фильтрует рецепты по known | 🟡 Medium |

**Ключевой принцип:** Всё выносится из хардкода в ScriptableObject-конфиги. Server-authoritative persistence через расширение QuestSaveData + CharacterSaveData. Никаких client-side фейков.

---

## 1. CharacterWindow: «Репутация» → «Знания»

### 1.1 Текущее состояние (аудит)

**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`

- 6 табов: `character | ship | reputation | contracts | inventory | quests`
- Вкладка «reputation» показывает: 2 ListView (`_reputationList` + `_npcAttitudeList`) с knowledge-фильтрацией (v1)
- `SwitchTab("reputation")` → `RefreshReputationCache()` + `RefreshNpcAttitudeCache()`
- ReputationSection — один VisualElement с двумя подсекциями
- Кнопка «ИЗУЧИТЬ НАВЫК» открывает `SocialSkillTreeWindow` (overlay, не вкладка)

**UX-проблема:** Игрок должен открывать CharacterWindow → вкладка «Репутация» чтобы увидеть фракции/NPC, и отдельно открывать `SkillTreeWindow` (overlay) для навыков, и отдельно `CraftingWindow` для рецептов. Нет единого места «что я знаю».

### 1.2 Целевое состояние

Вкладка переименовывается в **«Знания» (Knowledge)**. Это становится единым knowledge-hub'ом, который агрегирует ВСЕ типы знаний игрока в одном месте с удобной навигацией.

```
┌─ CharacterWindow ──────────────────────────────────────────┐
│ [ПЕРСОНАЖ] [КОРАБЛЬ] [ЗНАНИЯ] [КОНТРАКТЫ] [ИНВЕНТАРЬ] [КВЕСТЫ] │
├────────────────────────────────────────────────────────────┤
│ ┌── knowledge-left-panel ──┐ ┌── knowledge-detail-panel ──┐ │
│ │                          │ │                              │ │
│ │  📋 ФРАКЦИИ (5/11)      │ │  Гильдия Мысли               │ │
│ │  👤 NPC (3/106)         │ │  ━━━━━━━━━━━━━━━━━━━━         │ │
│ │  ⚔️ Навыки боевые (2/29)│ │  Репутация: +42 (Дружелюбный)│ │
│ │  💬 Навыки соц. (1/4)   │ │  Открыто через: диалог с      │ │
│ │  ⚒️ Рецепты (0/12)      │ │    Архивариусом Мирой         │ │
│ │  📜 Квесты               │ │  NPC: Архивариус Мира (+10)   │ │
│ │                          │ │  Связанные навыки: ...        │ │
│ │                          │ │                              │ │
│ └──────────────────────────┘ └──────────────────────────────┘ │
└────────────────────────────────────────────────────────────┘
```

#### Layout-дизайн

**Левая панель — «Категории знаний» (knowledge-categories):**
- Список категорий с бейджами «сколько открыто / всего»
- Каждая категория = кликабельная кнопка → переключение правой панели
- Порядок: Фракции, NPC, Навыки боевые, Навыки социальные, Рецепты, Квесты (открытые)

**Правая панель — «Детали» (knowledge-detail):**
- Меняется в зависимости от выбранной категории
- Для **Фракций:** ListView с factionId, displayName, reputation-value (цветной бар), как открыли (npc name)
- Для **NPC:** ListView с npcId, displayName, attitude-value (цветной бар), фракция, как открыли
- Для **Навыков:** ListView с skillId, displayName, category, discipline, как открыли (source NPC/quest/auto), статус (изучен/известен/неизвестен)
- Для **Рецептов:** ListView с recipeId, displayName, category, как открыли, можно ли крафтить (станция есть?)
- Для **Квестов:** ListView с questId, displayName, статус (Discovered/Active/Completed/Failed) — реюз существующего квест-лога

#### Как открывается каждая категория (источник знаний)

| Категория | Источник данных | Как открывается |
|-----------|----------------|-----------------|
| Фракции | `ReputationClientState.KnownFactionIds` (уже есть) | Диалог с NPC фракции; квест; книга |
| NPC | `NpcAttitudeClientState.KnownNpcIds` (уже есть) | Диалог с NPC; квест; событие |
| Навыки боевые | `SkillsClientState` + новые `KnownSkillIds` | NPC-тренер; книга; квест; авто-известные starter |
| Навыки соц. | `SkillsClientState` + новые `KnownSkillIds` | NPC-тренер; диалог; квест |
| Рецепты | Новый `RecipeKnowledgeClientState` | Найти чертёж; NPC-учитель; исследование |
| Квесты | `QuestClientState` (уже есть) | Диалог; discovered через мир |

### 1.3 Технический план: Переработка CharacterWindow

#### Шаг 1.1: UXML — переименование таба и секции

**Файл:** `CharacterWindow.uxml`

- `tab-reputation` → `tab-knowledge` (label: «Репутация» → «Знания»)
- `reputation-section` → `knowledge-section`
- Добавить `knowledge-categories` (левый ScrollView с кнопками-категориями)
- Добавить `knowledge-detail` (правый контейнер, меняется по категории)
- Внутри `knowledge-detail`: 6 под-контейнеров (`knowledge-factions`, `knowledge-npcs`, `knowledge-skills-combat`, `knowledge-skills-social`, `knowledge-recipes`, `knowledge-quests`) — видимость переключается

#### Шаг 1.2: CharacterWindow.cs — новые поля и логика

```csharp
// --- Tab button ---
private Button _tabKnowledge; // было _tabReputation

// --- Knowledge section ---
private VisualElement _knowledgeSection; // было _reputationSection

// Left panel: categories
private Button _knowledgeCatFactions;
private Button _knowledgeCatNpcs;
private Button _knowledgeCatSkillsCombat;
private Button _knowledgeCatSkillsSocial;
private Button _knowledgeCatRecipes;
private Button _knowledgeCatQuests;

// Right panel: detail containers
private VisualElement _knowledgeDetailFactions;
private VisualElement _knowledgeDetailNpcs;
private VisualElement _knowledgeDetailSkillsCombat;
private VisualElement _knowledgeDetailSkillsSocial;
private VisualElement _knowledgeDetailRecipes;
private VisualElement _knowledgeDetailQuests;

// ListViews (реюз существующих + новые)
private ListView _reputationList;    // existing
private ListView _npcAttitudeList;   // existing
private ListView _skillKnowledgeList; // NEW
private ListView _recipeKnowledgeList; // NEW
private ListView _questKnowledgeList;  // NEW (или реюз _questsDiscoveredList)

private string _activeKnowledgeCat = "factions";
```

#### Шаг 1.3: SwitchTab — переименование

```csharp
bool isKnowledge = tab == "knowledge"; // было "reputation"
if (_knowledgeSection != null) _knowledgeSection.style.display = ...
SetActiveTabVisual(_tabKnowledge, isKnowledge);
if (isKnowledge) { SwitchKnowledgeCategory(_activeKnowledgeCat); }
```

#### Шаг 1.4: SwitchKnowledgeCategory

```csharp
private void SwitchKnowledgeCategory(string cat)
{
    _activeKnowledgeCat = cat;
    // Скрыть все detail-контейнеры
    _knowledgeDetailFactions.style.display = cat == "factions" ? DisplayStyle.Flex : DisplayStyle.None;
    _knowledgeDetailNpcs.style.display = cat == "npcs" ? DisplayStyle.Flex : DisplayStyle.None;
    // ... и т.д.

    // Refresh нужной подсекции
    switch (cat)
    {
        case "factions": RefreshReputationCache(); break;
        case "npcs": RefreshNpcAttitudeCache(); break;
        case "skills-combat": RefreshSkillKnowledgeCache(SkillCategory.Combat); break;
        case "skills-social": RefreshSkillKnowledgeCache(SkillCategory.Social); break;
        case "recipes": RefreshRecipeKnowledgeCache(); break;
        case "quests": RefreshQuestKnowledgeCache(); break;
    }
}
```

#### Шаг 1.5: Новые Refresh-методы

**RefreshSkillKnowledgeCache:**
```csharp
private void RefreshSkillKnowledgeCache(SkillCategory category)
{
    _skillKnowledgeCache.Clear();
    var skillsState = SkillsClientState.Instance;
    var allConfigs = Resources.LoadAll<SkillNodeConfig>("Skills"); // или через кэш SkillsClientState

    foreach (var skill in allConfigs)
    {
        if (skill.category != category) continue;
        bool isLearned = skillsState?.CurrentSkills?.Contains(skill.skillId) ?? false;
        bool isKnown = /* проверить KnownSkillIds */;

        _skillKnowledgeCache.Add(new SkillKnowledgeItem
        {
            skillId = skill.skillId,
            displayName = skill.displayName,
            discipline = skill.discipline,
            isLearned = isLearned,
            isKnown = isKnown,
            unlockSource = skill.knowledgeUnlock?.description ?? "Неизвестно",
        });
    }
    // Сортировка: известные → неизвестные; изученные → не изученные
    _skillKnowledgeList.itemsSource = _skillKnowledgeCache;
    _skillKnowledgeList.Rebuild();
}
```

**RefreshRecipeKnowledgeCache:**
```csharp
private void RefreshRecipeKnowledgeCache()
{
    _recipeKnowledgeCache.Clear();
    // Получаем все RecipeData через CraftingClientState
    // Фильтруем: только known рецепты
}
```

### 1.4 Data-model для KnowledgeItem (новые DTO)

```csharp
// KnowledgeCategory — enum для левой панели
public enum KnowledgeCategory : byte
{
    Factions = 0,
    Npcs = 1,
    SkillsCombat = 2,
    SkillsSocial = 3,
    Recipes = 4,
    Quests = 5,
}

// KnowledgeSummaryDto — новый DTO от сервера клиенту (агрегирует все known-типы)
public struct KnowledgeSummaryDto : INetworkSerializable
{
    public byte[] knownFactionIds;
    public string[] knownNpcIds;
    public string[] knownSkillIds;      // NEW
    public int[] knownRecipeIds;        // NEW
    public string[] knownQuestIds;      // NEW — квесты в статусе Discovered+
}
```

> **Решение:** Вместо расширения `ReputationSnapshotDto` (v1 подход), для v2 логичнее создать единый `KnowledgeSummaryDto`, который сервер собирает из QuestWorld + SkillsWorld + CraftingWorld и отправляет клиенту одним пакетом. Это уменьшает количество snapshot-каналов и гарантирует консистентность.

### 1.5 Цвета/индикаторы статуса знаний

| Статус | Цвет | Пример |
|--------|------|--------|
| **Неизвестно** | Серый (затемнённый) `#444` | Навык скрыт, фракция невидима |
| **Известно** | Белый `#CCC` | Фракция видна в списке, навык можно изучать |
| **Изучено (skill)** | Зелёный `#4CAF50` | Навык в learned set |
| **Враждебно (faction)** | Красный `#F44336` | Репутация < -50 |

---

## 2. Навыки (Skills): Knowledge Unlock

### 2.1 Текущее состояние (аудит)

**SkillNodeConfig** (`Assets/_Project/Scripts/Skills/SkillNodeConfig.cs`):
- 343 строки, 25+ полей
- НЕТ поля «как игрок узнаёт о навыке»
- Все навыки видны в `SkillTreeWindow` сразу (клиент загружает `Resources.LoadAll<SkillNodeConfig>("Skills")`)

**SkillsWorld** (`Assets/_Project/Scripts/Skills/SkillsWorld.cs`):
- Server-side: `_learnedPerPlayer: Dictionary<ulong, HashSet<string>>`
- TryLearnSkill (5-step), TryForgetSkill, BuildSaveData/LoadPlayer
- НЕТ трекинга known skills

**SkillsClientState** (`Assets/_Project/Scripts/Skills/SkillsClientState.cs`):
- `CurrentSkills: HashSet<string>` — learned skill IDs
- `OnSkillsSnapshotReceived`, `OnSkillResultReceived`
- НЕТ known skills

**SkillTreeWindow / SocialSkillTreeWindow:**
- Показывают ВСЕ навыки из Resources — без фильтрации

### 2.2 Целевое состояние

Каждый навык в `SkillNodeConfig` получает знание о том, **как игрок его открывает**. До получения знания навык **невидим** в UI (SkillTreeWindow / SocialSkillTreeWindow / CharacterWindow→Знания→Навыки).

### 2.3 SkillNodeConfig — новое поле

```csharp
[Header("Knowledge Unlock")]
[Tooltip("Как игрок открывает знание об этом навыке. None = виден всегда (starter).")]
public KnowledgeUnlockType knowledgeUnlockType = KnowledgeUnlockType.None;

[Tooltip("NPC, у которого нужно взять урок (при UnlockType = NpcTrainer).")]
public string knowledgeNpcId;

[Tooltip("QuestId, который нужно завершить (при UnlockType = QuestReward).")]
public string knowledgeQuestId;

[Tooltip("ItemData — книга/артефакт (при UnlockType = ItemUse).")]
public ItemData knowledgeItem;

[Tooltip("Текстовое описание как открыть (для UI tooltip).")]
public string knowledgeUnlockDescription;
```

#### KnowledgeUnlockType enum

```csharp
public enum KnowledgeUnlockType : byte
{
    None = 0,              // Всегда виден (starter skills: BasicStrike, DodgeRoll, social_basic_talk)
    NpcTrainer = 1,        // Нужно поговорить с NPC-тренером
    QuestReward = 2,       // Награда за квест
    ItemUse = 3,           // Использовать предмет (книга, чертёж, артефакт)
    FactionLevel = 4,      // При достижении уровня репутации с фракцией
    WorldDiscovery = 5,    // Найти в мире (локация, poi, случайная находка)
    AutoOnSkillLearned = 6, // Авто-открывается когда изучен prerequisite навык
}
```

**Миграция существующих навыков (35 combat + 4 social):**

| Категория | UnlockType | Примечание |
|-----------|-----------|------------|
| Generic roots (BasicStrike, DodgeRoll, HeavySwing, PrecisionStrike) | `None` | Стартовые, видны всегда |
| social_basic_talk | `None` | Стартовый |
| social_barter, social_persuasion | `AutoOnSkillLearned` | Открываются после изучения basic_talk |
| social_leadership | `AutoOnSkillLearned` | После barter + persuasion |
| melee_basic_* (4 навыка) | `NpcTrainer` | Нужен NPC-мастер меча |
| melee_great_sword, melee_parry, melee_riposte | `AutoOnSkillLearned` | После basic |
| ranged_basic_* (2 навыка) | `NpcTrainer` | Нужен NPC-стрелок |
| explosives_basic_grenade | `NpcTrainer` | NPC-сапёр |
| antigrav_basic | `WorldDiscovery` | Найти антиграв-лабораторию |
| defense_basic_stance | `AutoOnSkillLearned` | После BasicStrike |
| defense_light_armor, etc. | `AutoOnSkillLearned` | После предыдущего |
| Кросс-веточные (antigrav_mine) | `AutoOnSkillLearned` | После antigrav_basic |

### 2.4 Сервер: SkillsWorld — _knownSkills

```csharp
// В SkillsWorld:
private readonly Dictionary<ulong, HashSet<string>> _knownSkills = new();

public bool IsSkillKnown(ulong clientId, string skillId)
{
    // None = виден всегда
    if (TryGetSkill(skillId, out var cfg) && cfg.knowledgeUnlockType == KnowledgeUnlockType.None)
        return true;
    return _knownSkills.TryGetValue(clientId, out var set) && set.Contains(skillId);
}

public void UnlockSkillKnowledge(ulong clientId, string skillId)
{
    if (!_knownSkills.TryGetValue(clientId, out var set))
    {
        set = new HashSet<string>();
        _knownSkills[clientId] = set;
    }
    set.Add(skillId);
    // TODO: broadcast KnowledgeSummaryDto
}
```

#### Триггеры UnlockSkillKnowledge:

1. **NpcTrainer:** В `QuestWorld.MarkNpcTalked` → чекаем все `SkillNodeConfig` с `knowledgeUnlockType == NpcTrainer && knowledgeNpcId == npcId` → `SkillsWorld.UnlockSkillKnowledge`
2. **QuestReward:** В `QuestWorld.TryTurnIn` → при Complete → чекаем `SkillNodeConfig` с `knowledgeQuestId == questId`
3. **ItemUse:** Новый handler в InventoryServer → `UseItemRpc` → если ItemData соответствует `knowledgeItem` навыка → unlock
4. **FactionLevel:** В `QuestWorld.ModifyReputation` → после изменения → чекаем `SkillNodeConfig` с `FactionLevel` + threshold
5. **WorldDiscovery:** Через новый `WorldDiscoveryServer` / триггер-зоны
6. **AutoOnSkillLearned:** В `SkillsWorld.TryLearnSkill` → после `learned.Add(skillId)` → чекаем все навыки где `prerequisites` содержит этот skill → UnlockSkillKnowledge

### 2.5 Клиент: SkillsClientState — KnownSkillIds

```csharp
// В SkillsClientState:
public HashSet<string> KnownSkillIds { get; private set; } = new();

public void OnKnowledgeSummaryReceived(KnowledgeSummaryDto summary)
{
    KnownSkillIds = summary.knownSkillIds != null
        ? new HashSet<string>(summary.knownSkillIds)
        : new HashSet<string>();
    OnSkillsUpdated?.Invoke(CurrentSkills);
}
```

### 2.6 UI: SkillTreeWindow / SocialSkillTreeWindow — фильтрация

```csharp
// В SkillTreeWindow.BuildGraph / PopulateSkillList:
foreach (var skill in allSkills)
{
    bool isKnown = SkillsClientState.Instance?.KnownSkillIds?.Contains(skill.skillId) ?? false;
    bool isAlwaysVisible = skill.knowledgeUnlockType == KnowledgeUnlockType.None;
    if (!isKnown && !isAlwaysVisible) continue; // Скрыть неизвестные навыки
    // ... рендер ноды
}
```

### 2.7 AutoOnSkillLearned — цепная реакция

При изучении навыка A → все навыки B где `prerequisites` содержит A → авто-анлок:
- **Знание открывается** (B появляется в UI как доступный для изучения)
- **НЕ изучается автоматически** (игрок должен потратить XP)

Это естественный DAG-обход: при `TryLearnSkill` → после `learned.Add` → для каждого `(skillId, cfg)` в `_skillsById` где `prerequisites` содержит `skillId` → `UnlockSkillKnowledge(clientId, cfg.skillId)`.

### 2.8 Persistence: SkillsSave — расширение

```csharp
[Serializable]
public class SkillsSave
{
    public string[] learnedSkillIds = Array.Empty<string>();
    public string[] knownSkillIds = Array.Empty<string>(); // NEW: T-KNOW-V2
}
```

В `SkillsWorld.BuildSaveData` / `LoadPlayer` добавить knownSkillIds.

---

## 3. Смерть → Потеря Знаний

### 3.1 Текущее состояние (аудит)

**PlayerTarget.ApplyDamage** (строка 250-272):
- `newHp <= 0` → disable input, Death animation
- `_deathRespawnTimer = Time.time + _deathRespawnDelay` (1.5s)
- `TriggerDeathRespawn()` — teleport + HP restore (30% by default)

**PlayerRespawnTracker.RespawnWithHpRestore:**
- Телепорт на точку респавна
- HP restore
- Сброс Death animation → Idle
- Включение управления

**Никакой логики потери знаний/навыков/ресурсов при смерти нет.**

### 3.2 Целевое состояние

При смерти игрок теряет **часть знаний** (но НЕ навыки — изученные навыки остаются). Потеря знаний означает:
- Часть **known factions** становится unknown (забыл о существовании фракции)
- Часть **known NPCs** забывается
- Часть **known recipes** забывается
- **known skills НЕ забываются** (узнал технику → навсегда)
- **learned skills НЕ забываются** (изученное остаётся)

### 3.3 Конфигурация (выносим из хардкода)

**Новый SO:** `DeathKnowledgeLossConfig`:

```csharp
[CreateAssetMenu(menuName = "Project C/Knowledge/Death Loss Config", fileName = "DeathKnowledgeLossConfig")]
public class DeathKnowledgeLossConfig : ScriptableObject
{
    [Header("Chance to lose each knowledge item (0-1)")]
    [Range(0f, 1f)] public float factionLossChance = 0.1f;    // 10% шанс забыть каждую известную фракцию
    [Range(0f, 1f)] public float npcLossChance = 0.15f;       // 15% шанс забыть каждого NPC
    [Range(0f, 1f)] public float recipeLossChance = 0.2f;     // 20% шанс забыть каждый рецепт
    [Range(0f, 1f)] public float skillLossChance = 0f;        // 0% — навыки не забываются

    [Header("Protection")]
    [Tooltip("Фракции с репутацией >= этого значения НЕ забываются.")]
    public int factionReputationProtectionThreshold = 50;     // "Уважаемый"+

    [Tooltip("NPC с отношением >= этого значения НЕ забываются.")]
    public int npcAttitudeProtectionThreshold = 100;

    [Tooltip("Фракции из этого списка НЕ забываются никогда.")]
    public FactionId[] neverForgetFactions = { FactionId.Neutral };

    [Tooltip("NPC из этого списка НЕ забываются никогда.")]
    public string[] neverForgetNpcs = { }; // ключевые сюжетные NPC

    [Header("Minimum retain")]
    [Tooltip("Минимальное количество known фракций после смерти (не даём забыть всё).")]
    public int minRetainFactions = 2; // Хотя бы Neutral + ещё одна

    [Tooltip("Минимальное количество known NPC после смерти.")]
    public int minRetainNpcs = 1;
}
```

### 3.4 Серверная логика: QuestWorld.ApplyDeathKnowledgeLoss

```csharp
// В QuestWorld:
public void ApplyDeathKnowledgeLoss(ulong clientId, DeathKnowledgeLossConfig config)
{
    if (config == null) return;

    // --- Factions ---
    if (_knownFactions.TryGetValue(clientId, out var factions))
    {
        var toRemove = new List<FactionId>();
        foreach (var fid in factions.ToArray())
        {
            if (config.neverForgetFactions.Contains(fid)) continue;
            int rep = GetReputation(clientId, fid);
            if (rep >= config.factionReputationProtectionThreshold) continue;
            if (Random.value < config.factionLossChance)
                toRemove.Add(fid);
        }
        // Не даём забыть меньше minRetainFactions
        if (factions.Count - toRemove.Count < config.minRetainFactions)
        {
            int toKeep = config.minRetainFactions - (factions.Count - toRemove.Count);
            toRemove = toRemove.Take(Math.Max(0, toRemove.Count - toKeep)).ToList();
        }
        foreach (var fid in toRemove) factions.Remove(fid);
    }

    // --- NPCs (аналогично) ---
    if (_knownNpcs.TryGetValue(clientId, out var npcs))
    {
        var toRemove = new List<string>();
        foreach (var npcId in npcs.ToArray())
        {
            if (config.neverForgetNpcs.Contains(npcId)) continue;
            int att = GetNpcAttitude(clientId, npcId);
            if (att >= config.npcAttitudeProtectionThreshold) continue;
            if (Random.value < config.npcLossChance)
                toRemove.Add(npcId);
        }
        if (npcs.Count - toRemove.Count < config.minRetainNpcs)
        {
            int toKeep = config.minRetainNpcs - (npcs.Count - toRemove.Count);
            toRemove = toRemove.Take(Math.Max(0, toRemove.Count - toKeep)).ToList();
        }
        foreach (var npcId in toRemove) npcs.Remove(npcId);
    }

    // --- Recipes ---
    // Вызываем CraftingWorld.ApplyDeathRecipeLoss(clientId, config) — см. §4

    // Сохраняем
    SavePlayer(clientId);

    // Отправляем обновлённый KnowledgeSummaryDto
    BroadcastKnowledgeChange(clientId);

    Debug.Log($"[QuestWorld] Death knowledge loss applied for client={clientId}");
}
```

### 3.5 Точка вызова

В `PlayerTarget.TriggerDeathRespawn()` добавить:

```csharp
// После респавна:
var knowledgeConfig = Resources.Load<DeathKnowledgeLossConfig>("Configs/DeathKnowledgeLossConfig");
QuestWorld.Instance?.ApplyDeathKnowledgeLoss(_clientId, knowledgeConfig);
```

**Почему после респавна, а не в момент смерти:** Игрок должен сначала увидеть death animation, а знания теряются при «пробуждении» (респавне). Это нарративно: «после смерти часть воспоминаний стёрлась».

---

## 4. Крафт и Рецепты: Knowledge of Recipes

### 4.1 Текущее состояние (аудит)

**RecipeData** (`Assets/_Project/Scripts/Crafting/RecipeData.cs`):
- 195 строк, поля: displayName, icon, description, category, ingredients, outputs, craftSeconds, requiredSkillLevel, requiredSkill
- НЕТ поля «как игрок узнаёт рецепт»

**CraftingWorld** (`Assets/_Project/Scripts/Crafting/CraftingWorld.cs`):
- Статический серверный реестр: `_recipesById`, `_idsByRecipe`, `RegisterRecipe`, `GetRecipe`
- НЕТ трекинга known recipes per player

**CraftingClientState** (`Assets/_Project/Scripts/Crafting/CraftingClientState.cs`):
- Клиентский кеш рецептов (T3 fix)
- НЕТ known recipes

**CraftingWindow** — показывает ВСЕ рецепты из кеша.

### 4.2 Целевое состояние

Рецепты скрыты до получения знания. Игрок может:
- **Найти чертёж** (ItemUse → unlockRecipe)
- **Получить от NPC** (диалог → unlockRecipe)
- **Исследовать** (станция → «исследование» — отдельная механика, Phase 2)
- **Авто-известные** (стартовые рецепты: basic components)

### 4.3 RecipeData — новое поле

```csharp
[Header("Knowledge Unlock")]
[Tooltip("Как игрок открывает этот рецепт.")]
public RecipeKnowledgeUnlockType knowledgeUnlockType = RecipeKnowledgeUnlockType.None;

[Tooltip("NPC, у которого нужно спросить / который даёт чертёж.")]
public string knowledgeNpcId;

[Tooltip("QuestId, который даёт рецепт как награду.")]
public string knowledgeQuestId;

[Tooltip("ItemData — чертёж/книга (при UnlockType = BlueprintItem).")]
public ItemData knowledgeBlueprintItem;

[Tooltip("Текст для UI: 'Найти чертёж у Архивариуса Миры'.")]
public string knowledgeUnlockDescription;
```

```csharp
public enum RecipeKnowledgeUnlockType : byte
{
    None = 0,              // Известен всем с начала (basic components)
    NpcTrainer = 1,        // NPC-учитель (диалог)
    BlueprintItem = 2,     // Найти/купить/получить чертёж
    QuestReward = 3,       // Награда за квест
    Research = 4,          // Исследовать на станции (Phase 2)
}
```

### 4.4 Сервер: CraftingWorld — _knownRecipes

```csharp
// В CraftingWorld:
private static Dictionary<ulong, HashSet<int>> _knownRecipes = new();

public static bool IsRecipeKnown(ulong clientId, int recipeId)
{
    var recipe = GetRecipe(recipeId);
    if (recipe != null && recipe.knowledgeUnlockType == RecipeKnowledgeUnlockType.None)
        return true;
    return _knownRecipes.TryGetValue(clientId, out var set) && set.Contains(recipeId);
}

public static void UnlockRecipeKnowledge(ulong clientId, int recipeId)
{
    if (!_knownRecipes.TryGetValue(clientId, out var set))
    {
        set = new HashSet<int>();
        _knownRecipes[clientId] = set;
    }
    if (set.Add(recipeId))
    {
        Debug.Log($"[CraftingWorld] Recipe knowledge unlocked: player={clientId} recipe={recipeId}");
        // TODO: broadcast KnowledgeSummary или CraftingSnapshot
    }
}

public static void ApplyDeathRecipeLoss(ulong clientId, DeathKnowledgeLossConfig config)
{
    if (!_knownRecipes.TryGetValue(clientId, out var set)) return;
    var toRemove = new List<int>();
    foreach (var rid in set.ToArray())
    {
        if (Random.value < config.recipeLossChance)
            toRemove.Add(rid);
    }
    foreach (var rid in toRemove) set.Remove(rid);
}

// Persistence
public static RecipeKnowledgeSave BuildRecipeKnowledgeSave(ulong clientId)
{
    if (!_knownRecipes.TryGetValue(clientId, out var set)) return new RecipeKnowledgeSave();
    return new RecipeKnowledgeSave { knownRecipeIds = new List<int>(set).ToArray() };
}

public static void LoadRecipeKnowledge(ulong clientId, RecipeKnowledgeSave save)
{
    if (save?.knownRecipeIds == null) return;
    _knownRecipes[clientId] = new HashSet<int>(save.knownRecipeIds);
}
```

### 4.5 Триггеры UnlockRecipeKnowledge

1. **NpcTrainer:** В `QuestWorld.MarkNpcTalked` → проверяем `RecipeData` с `knowledgeNpcId == npcId` → `CraftingWorld.UnlockRecipeKnowledge`
2. **BlueprintItem:** В `InventoryServer` → новый handler `UseBlueprintRpc` → consumе item → unlock recipe
3. **QuestReward:** В `QuestWorld.TryTurnIn` → проверяем `RecipeData` с `knowledgeQuestId == questId`
4. **Диалог:** В `FireDialogAction` → новый `DialogueAction.UnlockRecipe(recipeId)`

### 4.6 Клиент: CraftingClientState — KnownRecipeIds

```csharp
// В CraftingClientState:
public HashSet<int> KnownRecipeIds { get; private set; } = new();

public void OnKnowledgeSummaryReceived(KnowledgeSummaryDto summary)
{
    KnownRecipeIds = summary.knownRecipeIds != null
        ? new HashSet<int>(summary.knownRecipeIds)
        : new HashSet<int>();
}
```

### 4.7 UI: CraftingWindow — фильтрация

```csharp
// В CraftingWindow.PopulateRecipeList:
foreach (var recipe in allRecipes)
{
    bool isKnown = CraftingClientState.Instance?.KnownRecipeIds?.Contains(recipeId) ?? false;
    bool isAlwaysVisible = recipe.knowledgeUnlockType == RecipeKnowledgeUnlockType.None;
    if (!isKnown && !isAlwaysVisible) continue;
    // ... рендер рецепта
}
```

### 4.8 Persistence: QuestSaveData — расширение

```csharp
// В QuestSaveData:
public List<int> knownRecipes = new List<int>(); // NEW: T-KNOW-V2
```

**Либо** отдельный файл `recipe_knowledge_{clientId}.json` — решается при имплементации. Для v2 оставляем в QuestSaveData (единый knowledge-файл).

---

## 5. Единый KnowledgeSnapshotDto (архитектурное решение)

### 5.1 Почему единый DTO

В v1 мы расширили существующие DTO (`ReputationSnapshotDto` + `knownFactionIds`, `NpcAttitudeSnapshotDto` + `knownNpcIds`). Это было оправдано для 2 типов знаний.

В v2 у нас **6+ типов знаний** (factions, NPCs, skills, recipes, quests, locations). Делать 6 отдельных каналов — переусложнение. 

**Решение:** Единый `KnowledgeSummaryDto`, который сервер собирает и отправляет клиенту при:
- Первом подключении (полный снапшот)
- Изменении любого knowledge-типа (инкрементальный broadcast)

```csharp
public struct KnowledgeSummaryDto : INetworkSerializable
{
    public byte[] knownFactionIds;     // FactionId → byte
    public string[] knownNpcIds;
    public string[] knownSkillIds;     // skillId strings
    public int[] knownRecipeIds;       // compact int ids
    public string[] knownQuestIds;     // questId strings
    // Future:
    // public string[] knownLocationIds;
}
```

### 5.2 Сервер: QuestServer.BuildKnowledgeSummary

```csharp
private KnowledgeSummaryDto BuildKnowledgeSummary(ulong clientId)
{
    var w = QuestWorld.Instance;
    var sw = SkillsWorld.Instance;

    return new KnowledgeSummaryDto
    {
        knownFactionIds = BuildKnownFactionBytes(clientId),
        knownNpcIds = BuildKnownNpcArray(clientId),
        knownSkillIds = sw?.GetKnownSkillIds(clientId)?.ToArray() ?? Array.Empty<string>(),
        knownRecipeIds = CraftingWorld.GetKnownRecipeIds(clientId)?.ToArray() ?? Array.Empty<int>(),
        knownQuestIds = w?.GetKnownQuestIds(clientId)?.ToArray() ?? Array.Empty<string>(),
    };
}
```

### 5.3 Клиент: KnowledgeClientState (новый singleton)

```csharp
public class KnowledgeClientState : MonoBehaviour
{
    public static KnowledgeClientState Instance { get; private set; }

    public HashSet<byte> KnownFactionIds { get; private set; } = new();
    public HashSet<string> KnownNpcIds { get; private set; } = new();
    public HashSet<string> KnownSkillIds { get; private set; } = new();
    public HashSet<int> KnownRecipeIds { get; private set; } = new();
    public HashSet<string> KnownQuestIds { get; private set; } = new();

    public event Action<KnowledgeSummaryDto> OnKnowledgeUpdated;

    public void OnKnowledgeSummaryReceived(KnowledgeSummaryDto summary)
    {
        KnownFactionIds = summary.knownFactionIds != null ? new HashSet<byte>(summary.knownFactionIds) : new();
        KnownNpcIds = summary.knownNpcIds != null ? new HashSet<string>(summary.knownNpcIds) : new();
        KnownSkillIds = summary.knownSkillIds != null ? new HashSet<string>(summary.knownSkillIds) : new();
        KnownRecipeIds = summary.knownRecipeIds != null ? new HashSet<int>(summary.knownRecipeIds) : new();
        KnownQuestIds = summary.knownQuestIds != null ? new HashSet<string>(summary.knownQuestIds) : new();

        OnKnowledgeUpdated?.Invoke(summary);
    }
}
```

### 5.4 Миграция: что делать с существующими ReputationClientState + NpcAttitudeClientState?

**План:** v1 код НЕ удаляем, а депрекейтим:
1. `ReputationClientState.KnownFactionIds` → начинает брать данные из `KnowledgeClientState.KnownFactionIds`
2. `NpcAttitudeClientState.KnownNpcIds` → начинает брать данные из `KnowledgeClientState.KnownNpcIds`
3. `ReputationSnapshotDto.knownFactionIds` → остаётся для обратной совместимости, но новые знания идут через `KnowledgeSummaryDto`

**Фаза 2 (после стабилизации):** удаляем `knownFactionIds`/`knownNpcIds` из старых DTO, оставляем только `KnowledgeSummaryDto`.

---

## 6. Полный План Интеграции (12 Шагов)

### Шаг 1: KnowledgeSummaryDto + KnowledgeClientState

**Новые файлы:**
- `Assets/_Project/Quests/Dto/KnowledgeSummaryDto.cs`
- `Assets/_Project/Knowledge/KnowledgeClientState.cs`

**Изменения:**
- `NetworkManagerController.cs` — auto-spawn `KnowledgeClientState`

### Шаг 2: KnowledgeUnlockType + поле в SkillNodeConfig

**Файл:** `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs`
- +enum `KnowledgeUnlockType` (7 значений)
- +поля: `knowledgeUnlockType`, `knowledgeNpcId`, `knowledgeQuestId`, `knowledgeItem`, `knowledgeUnlockDescription`

### Шаг 3: RecipeKnowledgeUnlockType + поле в RecipeData

**Файл:** `Assets/_Project/Scripts/Crafting/RecipeData.cs`
- +enum `RecipeKnowledgeUnlockType` (5 значений)
- +поля: `knowledgeUnlockType`, `knowledgeNpcId`, `knowledgeQuestId`, `knowledgeBlueprintItem`, `knowledgeUnlockDescription`

### Шаг 4: DeathKnowledgeLossConfig (новый SO)

**Новый файл:** `Assets/_Project/Scripts/Knowledge/DeathKnowledgeLossConfig.cs`
- `[CreateAssetMenu]` + поля из §3.3

### Шаг 5: Сервер — _knownSkills в SkillsWorld

**Файл:** `Assets/_Project/Scripts/Skills/SkillsWorld.cs`
- +`_knownSkills: Dictionary<ulong, HashSet<string>>`
- +`IsSkillKnown`, `UnlockSkillKnowledge`, `GetKnownSkillIds`
- +`BuildSaveData`/`LoadPlayer` → knownSkillIds
- +`AutoOnSkillLearned` логика в `TryLearnSkill`

### Шаг 6: Сервер — _knownRecipes в CraftingWorld

**Файл:** `Assets/_Project/Scripts/Crafting/CraftingWorld.cs`
- +`_knownRecipes: Dictionary<ulong, HashSet<int>>`
- +`IsRecipeKnown`, `UnlockRecipeKnowledge`, `GetKnownRecipeIds`, `ApplyDeathRecipeLoss`
- +`BuildRecipeKnowledgeSave`, `LoadRecipeKnowledge`

### Шаг 7: Сервер — ApplyDeathKnowledgeLoss в QuestWorld

**Файл:** `Assets/_Project/Quests/Core/QuestWorld.cs`
- +`ApplyDeathKnowledgeLoss(clientId, config)` — логика из §3.4

### Шаг 8: Интеграция Death → Knowledge Loss

**Файл:** `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs`
- В `TriggerDeathRespawn()` → после респавна → `QuestWorld.Instance?.ApplyDeathKnowledgeLoss(clientId, config)`

### Шаг 9: Сервер — BuildKnowledgeSummary + broadcast

**Файл:** `Assets/_Project/Quests/Network/QuestServer.cs`
- +`BuildKnowledgeSummary(clientId)`
- +`SendKnowledgeSummaryToClient(clientId, dto)`
- +Вызов при: первом подключении, MarkNpcTalked, TryLearnSkill, UnlockRecipeKnowledge, смерти

### Шаг 10: Persistence — QuestSaveData расширение

**Файл:** `Assets/_Project/Quests/Persistence/QuestSaveData.cs`
- +`knownSkills: List<string>`
- +`knownRecipes: List<int>`
- +`knownQuests: List<string>`

**Файл:** `Assets/_Project/Scripts/Stats/Persistence/SkillsSave.cs`
- +`knownSkillIds: string[]`

### Шаг 11: CharacterWindow — вкладка «Знания»

**Файлы:**
- `CharacterWindow.uxml` — переименование, новый layout
- `CharacterWindow.cs` — `SwitchKnowledgeCategory`, новые refresh-методы
- `CharacterWindow.uss` — стили для knowledge-панели

### Шаг 12: UI фильтрация в SkillTreeWindow / CraftingWindow

**Файлы:**
- `SkillTreeWindow.cs` — фильтр по known skills
- `SocialSkillTreeWindow.cs` — фильтр по known skills
- `CraftingWindow.cs` — фильтр по known recipes
- `CraftingClientState.cs` — +`KnownRecipeIds`

---

## 7. Оценка Трудозатрат

| # | Шаг | Файлы | Оценка |
|---|-----|-------|--------|
| 1 | KnowledgeSummaryDto + KnowledgeClientState | 3 новых | 1.5ч |
| 2 | SkillNodeConfig: KnowledgeUnlockType | 1 | 30мин |
| 3 | RecipeData: RecipeKnowledgeUnlockType | 1 | 30мин |
| 4 | DeathKnowledgeLossConfig SO | 1 новый | 30мин |
| 5 | SkillsWorld: _knownSkills | 1 | 1.5ч |
| 6 | CraftingWorld: _knownRecipes | 1 | 1.5ч |
| 7 | QuestWorld: death knowledge loss | 1 | 1ч |
| 8 | PlayerTarget: hook death→loss | 1 | 15мин |
| 9 | QuestServer: BuildKnowledgeSummary | 1 | 1ч |
| 10 | Persistence: QuestSaveData + SkillsSave | 2 | 30мин |
| 11 | CharacterWindow: вкладка «Знания» | 3 (uxml+cs+uss) | 3-4ч |
| 12 | UI фильтрация (SkillTree + Crafting) | 3 | 1.5ч |
| | **Итого** | **~19 файлов** | **~12-14 часов** |

---

## 8. Что остаётся на V3 (Future)

- **Локации** (LocationDefinition): knowledge о городах/POI → появление на карте
- **Knowledge decay** (затухание знаний со временем без взаимодействия)
- **Research-механика** (станция → «исследовать» → открыть случайный рецепт)
- **Знания о кораблях** (увидел корабль фракции → знаешь её маркировку)
- **Admin tools** для просмотра/редактирования knowledge
- **Батч-миграция** существующих персонажей (добавить known skills/recipes из конфига)
- **Knowledge trading** (NPC продаёт знания / чертежи)

---

## 9. Архитектурные Решения (ADR для V2)

### ADR-5: Единый KnowledgeSummaryDto вместо множества каналов

**Решение:** Все типы знаний передаются в одном DTO через один RPC-канал.

**Обоснование:** v1 подход (расширение существующих DTO) не масштабируется на 6+ типов. Единый канал уменьшает race conditions и упрощает клиентский код.

### ADR-6: Known skills ≠ Learned skills

**Решение:** `_knownSkills` (знание о существовании навыка) и `_learnedPerPlayer` (изученные навыки) — две независимые структуры.

**Обоснование:** Игрок может знать о навыке, но не изучить его (не хватает XP/tier). И наоборот: изучил навык → автоматически знает о следующем в цепочке (AutoOnSkillLearned).

### ADR-7: Смерть → потеря знаний, не навыков

**Решение:** При смерти теряются knowledge (factions, NPCs, recipes), но не learned skills.

**Обоснование:** Потеря изученных навыков (за которые игрок потратил XP) — слишком жёсткое наказание, ведущее к фрустрации. Потеря знаний о фракциях/NPC — реалистичный нарративный элемент («после смерти память фрагментирована») без ущерба прогрессии.

### ADR-8: KnowledgeUnlock — конфигурируется в SO

**Решение:** Способ открытия каждого навыка/рецепта задаётся в `SkillNodeConfig`/`RecipeData`, а НЕ хардкодится в коде.

**Обоснование:** Дизайнер должен иметь возможность менять условия открытия без правки C# кода. Все enum'ы исчерпывающие, но могут быть расширены.

---

## 10. Файлы, Которые Будут Изменены (Итоговая Таблица)

| # | Файл | Тип изменений | Шаг |
|---|------|--------------|-----|
| 1 | `KnowledgeSummaryDto.cs` | **Новый** | 1 |
| 2 | `KnowledgeClientState.cs` | **Новый** | 1 |
| 3 | `DeathKnowledgeLossConfig.cs` | **Новый** | 4 |
| 4 | `SkillNodeConfig.cs` | +enum +5 полей | 2 |
| 5 | `RecipeData.cs` | +enum +5 полей | 3 |
| 6 | `SkillsWorld.cs` | +_knownSkills +4 метода | 5 |
| 7 | `CraftingWorld.cs` | +_knownRecipes +6 методов | 6 |
| 8 | `QuestWorld.cs` | +ApplyDeathKnowledgeLoss | 7 |
| 9 | `PlayerTarget.cs` | +hook death→loss | 8 |
| 10 | `QuestServer.cs` | +BuildKnowledgeSummary +broadcast | 9 |
| 11 | `QuestSaveData.cs` | +3 поля | 10 |
| 12 | `SkillsSave.cs` | +1 поле | 10 |
| 13 | `CharacterWindow.uxml` | Переименование + новый layout | 11 |
| 14 | `CharacterWindow.cs` | +knowledge-логика | 11 |
| 15 | `CharacterWindow.uss` | +стили knowledge | 11 |
| 16 | `SkillTreeWindow.cs` | +known-фильтр | 12 |
| 17 | `SocialSkillTreeWindow.cs` | +known-фильтр | 12 |
| 18 | `CraftingWindow.cs` | +known-фильтр | 12 |
| 19 | `CraftingClientState.cs` | +KnownRecipeIds | 12 |
| 20 | `NetworkManagerController.cs` | +spawn KnowledgeClientState | 1 |
| 21 | `SkillsServer.cs` | +broadcast knowledge change | 5 |
| 22 | `NetworkPlayer.cs` | +ReceiveKnowledgeSummaryTargetRpc | 1 |

**Новых файлов:** 3 (KnowledgeSummaryDto, KnowledgeClientState, DeathKnowledgeLossConfig)
**Изменяемых:** 19

---

*Документ создан для Project C: The Clouds. Базируется на полном аудите кодовой базы (QuestWorld, SkillsWorld, CraftingWorld, CharacterWindow, PlayerTarget, релевантных GDD).*
