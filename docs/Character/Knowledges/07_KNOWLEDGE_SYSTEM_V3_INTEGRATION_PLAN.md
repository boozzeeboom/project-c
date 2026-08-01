# Knowledge System V3 — План интеграции

> **Основание:** `06_KNOWLEDGE_SYSTEM_V2_IMPLEMENTATION.md` (Фаза A: данные и сервер — завершена)
> **Дата анализа:** 2026-08-02
> **Статус:** План утверждён, **уточнён 2026-08-02 по результатам code review** (добавлены V3.0/V3.11, правки V3.1/V3.2/V3.7/V3.9/V3.10, актуализирована секция 1)

---

## 1. Что уже есть (V2 Phase A + Phase B — Done)

В V2 реализован **серверный слой знаний** + persistence + синхронизация на клиент:

| Домен | Серверное хранилище | Клиентский State | Тип ID |
|---|---|---|---|
| Навыки | `SkillsWorld._knownPerPlayer` | `SkillsClientState.KnownSkillIds` | `string` (skillId) |
| Рецепты | `CraftingWorld._knownRecipes` | `RecipeKnowledgeClientState.KnownRecipeIds` | `int` (recipeId) |
| Фракции | `QuestWorld._knownFactions` | `ReputationClientState.KnownFactionIds` | `byte` (FactionId) |
| NPC | `QuestWorld._knownNpcs` | `NpcAttitudeClientState.KnownNpcIds` | `string` (npcId) |

Сервер умеет:
- `UnlockSkillKnowledge(clientId, skillId)` — открыть знание навыка
- `UnlockRecipeKnowledge(clientId, recipeId)` — открыть знание рецепта
- `UnlockFactionKnowledge(clientId, faction)` — открыть знание фракции
- `UnlockNpcKnowledge(clientId, npcId)` — открыть знание NPC
- `AutoOnSkillLearned(clientId, skillId)` — авто-открытие LearnFirst-навыков
- Persistence: `BuildSaveData` / `LoadPlayer` во всех 4 доменах
- Death knowledge loss (`ApplyDeathSkillKnowledgeLoss`, `ApplyDeathRecipeLoss`)
- `SkillNodeConfig.knowledgeUnlockType` enum: `None / LearnFirst / Blueprint / NpcTeach / QuestReward`
- `RecipeData.RecipeKnowledgeUnlockType` enum: `Blueprint / NpcTeach / QuestReward / Station`

Клиент получает знания через снапшоты:
- `SkillsSnapshotDto.knownSkillIds[]`
- `RecipeKnowledgeDto.knownRecipeIds[]`
- `ReputationSnapshotDto.knownFactionIds[]` + `NpcAttitudeSnapshotDto.knownNpcIds[]`

### ✅ Уже реализовано (были pending в V2 — закрыто, не включать в V3)

| Пункт | Где |
|---|---|
| NMC auto-spawn `RecipeKnowledgeClientState` | `NetworkManagerController.cs:137,474` — `CreateRecipeKnowledgeClientState()` |
| `NetworkPlayer.ReceiveRecipeKnowledgeTargetRpc` | `NetworkPlayer.cs:2059` |
| Вкладка «Знания» в CharacterWindow (Phase B) | `CharacterWindow.cs` — кэши навыков `:1982`, рецептов `:2009`, фракций `:1098`, NPC `:1182` |
| `QuestServer.BroadcastKnowledgeChange(clientId)` | `QuestServer.cs:1139` — шлёт reputation + npcAttitude снапшоты одним вызовом |
| `SkillsServer.SendSnapshotToOwner(clientId)` | `SkillsServer.cs:180` — public, уже шлёт `knownSkillIds` |
| `CraftingServer.SendRecipeKnowledgeToClient(clientId)` | `CraftingServer.cs:521` — public |

---

## 2. Глубокий анализ: найденные нестыковки V2

### 🔴 Проблема 1 (критическая): SkillTreeWindow НЕ фильтрует по знаниям

**Файл:** `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs`, метод `ApplyFilterAndSearch()` (строка 243)

```csharp
private void ApplyFilterAndSearch()
{
    _filteredSkills.Clear();
    foreach (var s in _allSkillConfigs)
    {
        if (s == null) continue;
        if (!MatchesDiscipline(s)) continue;  // ✅ фильтр по дисциплине
        if (!MatchesSearch(s)) continue;      // ✅ поиск по тексту
        // ❌ НЕТ проверки: knowledgeUnlockType != None && !knownIds.Contains(skillId)
        _filteredSkills.Add(s);
    }
    RebuildSkillList();
}
```

**Последствия:**
- Игрок видит ВСЕ навыки в дереве изучения, даже если знание о них не открыто
- `SkillNodeConfig.knowledgeUnlockType = LearnFirst/Blueprint/NpcTeach/QuestReward` **игнорируется**
- Вкладка «Знания» пустая, а дерево навыков показывает полный список — **противоречие дизайну**

### 🔴 Проблема 2 (критическая): SocialSkillTreeWindow — та же проблема

**Файл:** `Assets/_Project/Scripts/Skills/UI/SocialSkillTreeWindow.cs`, метод `ApplyFilterAndSearch()` (строка 223)

```csharp
private void ApplyFilterAndSearch()
{
    _filteredSkills.Clear();
    foreach (var s in _allSkillConfigs)
    {
        if (s == null) continue;
        if (!MatchesSearch(s)) continue;      // только поиск
        // ❌ НЕТ knowledge-фильтра
        _filteredSkills.Add(s);
    }
    RebuildSkillTree();
}
```

### 🔴 Проблема 3: CharacterWindow social-колонка показывает навыки без учёта знаний

**Файл:** `CharacterWindow.cs`, метод `RefreshSkillsCache()` (строка 2365)

Social-навыки в CharacterWindow показываются с состояниями `LEARNED/AVAILABLE/LOCKED` **без учёта `KnownSkillIds`**. Игрок видит социальные навыки, которые ещё не открыл.

### 🔴 Проблема 4 (архитектурная): Нет централизованного механизма открытия знаний

Сейчас знания открываются ТОЛЬКО:
- Автоматически при изучении навыка (`AutoOnSkillLearned` → `LearnFirst`)
- При разговоре с NPC (`QuestWorld.MarkNpcTalked` → `UnlockNpcKnowledge` + `UnlockFactionKnowledge`)
- При крафте (`CraftingServer.StartCraft` → `UnlockRecipeKnowledge`)

**Отсутствует** `KnowledgeRevealTrigger` — компонент для trigger zone / события, куда дизайнер кидает ассеты и игрок открывает их при входе в зону.

### 🟡 Проблема 5: Разрозненные API для открытия знаний

Чтобы открыть разные типы знаний, нужно вызывать 4 разных метода в 3 разных классах:
- `SkillsWorld.UnlockSkillKnowledge(clientId, skillId)`
- `CraftingWorld.UnlockRecipeKnowledge(clientId, recipeId)`
- `QuestWorld.UnlockFactionKnowledge(clientId, faction)`
- `QuestWorld.UnlockNpcKnowledge(clientId, npcId)`

Нет единого фасада. Дизайнеру (через триггер-компонент) должно быть достаточно указать ассеты — система сама определит тип.

### 🟡 Проблема 6: KnowledgeUnlockType.Blueprint/NpcTeach/QuestReward — dead code

В `SkillNodeConfig` enum имеет значения `Blueprint`, `NpcTeach`, `QuestReward`, но:
- В runtime используется только `None` и `LearnFirst` (через `AutoOnSkillLearned`)
- `Blueprint`, `NpcTeach`, `QuestReward` не имеют кода обработки
- Поля `knowledgeUnlockId` и `knowledgeUnlockDescription` заполняются в инспекторе, но **никогда не читаются в runtime**

### 🟡 Проблема 7: RecipeData.KnowledgeUnlockType — также dead code

`RecipeData.RecipeKnowledgeUnlockType` enum (`Blueprint/NpcTeach/QuestReward/Station`) не используется для фильтрации в UI и не имеет runtime-обработчиков кроме server gate `IsRecipeKnown` в `StartCraftRpc`.

### 🔴 Проблема 8 (критическая, найдена при ревью): RecipeData не имеет стабильного строкового id

`RecipeData` не имеет строкового ключа (в отличие от `skillId` у навыков). Идентификация — только через `int`, который **зависит от порядка регистрации**:

- **Сервер:** `CraftingWorld.RegisterRecipe()` присваивает id по порядку `baseRecipes` из инспектора (`CraftingServer.cs:49`, `_nextRecipeId = 1`)
- **Клиент:** `RecipeClientRegistry.EnsureLoaded()` присваивает id по порядку `Resources.LoadAll("Crafting/Recipes")` (`RecipeClientRegistry.cs:24-31`, `nextId = 1`)

**Порядок `Resources.LoadAll` не гарантирован** → id на клиенте и сервере могут разойтись → `KnownRecipeIds` на клиенте будут указывать на **чужие рецепты**.

С триггерами становится хуже: сервер зарегистрирует рецепт из зоны (которого нет в `baseRecipes` станции) → его серверный id почти наверняка ≠ клиентскому. Усугубляет то, что `CraftingWindow.GetRecipeDisplayList()` вызывает `CraftingWorld.RegisterRecipe(r)` **на клиенте** (`CraftingWindow.cs:291`) — клиентский registry вообще отдельный от серверного.

**Без решения этой проблемы фича «открыть рецепт через зону» неработоспособна.**

### 🔴 Проблема 9 (критическая, найдена при ревью): Unlock* методы не сохраняют прогресс

Все 4 `Unlock*` метода **не вызывают** persistence:
- `QuestWorld.UnlockFactionKnowledge` / `UnlockNpcKnowledge` — комментарий «NOT calling SavePlayer here — caller already does» (`QuestWorld.cs:835,848`)
- `SkillsWorld.UnlockSkillKnowledge` — не сохраняет
- `CraftingWorld.UnlockRecipeKnowledge` — не сохраняет

Единственная точка сохранения — `QuestWorld.SavePlayer(clientId)` (`QuestWorld.cs:1349`), которая через `BuildSaveData` собирает **все 4 домена** (knownFactions, knownNpcs, knownRecipes из CraftingWorld + skills). Если `KnowledgeManager.UnlockAll` просто вызовет доменные методы — знания исчезнут после рестарта.

### 🟡 Проблема 10 (найдена при ревью): Семантика RecipeKnowledgeUnlockType.Blueprint противоречива

`RecipeData.cs:54-60`:
- Комментарий enum: «Blueprint = чертёж/предмет» (рецепт **скрыт**, открывается чертежом)
- Tooltip поля: «Blueprint (default) — текущее поведение: рецепт **доступен** через станцию» (рецепт виден)
- Значения `None`/`AlwaysVisible` **нет**

Если V3.9 сделает «показывать только known» — **все текущие Blueprint-рецепты исчезнут со станций**, сломав существующую игру. Нужно однозначное правило видимости.

### 🟡 Проблема 11 (найдена при ревью): Unlock NPC в одиночку не открывает его фракцию

`QuestWorld.MarkNpcTalked` открывает NPC **и его фракцию** (`QuestWorld.cs:794-803`). Планируемый `KnowledgeManager.Unlock(NpcDefinition)` откроет только NPC — дизайнеру придётся дублировать фракцию в массиве вручную. Не консистентно с диалоговым путём.

### 🟡 Проблема 12 (найдена при ревью): SkillInputService.SetKnownSkills не вызывается нигде

`SkillInputService.SetKnownSkills(IEnumerable<string>)` (`SkillInputService.cs:611`) — **нет ни одного вызова** в проекте. `_allSkillIds` пуст, а `SkillBindingWindow.RebuildModal()` (`SkillBindingWindow.cs:159`) отдаёт «(нет доступных навыков)». Когда дерево навыков начнёт фильтроваться по знаниям — окно биндинга должно фильтроваться синхронно, иначе игрок сможет «увидеть» навык только в биндинге.

### 🟡 Проблема 13 (найдена при ревью): Двойное срабатывание триггера (server + client RPC)

Scene-placed объект с триггером существует на **всех** машинах. Если по плану «на клиенте `OnTriggerEnter` → RPC, на сервере → UnlockAll» — на сервере unlock сработает дважды (свой `OnTriggerEnter` + RPC от клиента). Идемпотентно (HashSet), но задвоятся логи и toast. Нужна чёткая server-authoritative схема.

---

## 3. Видение V3 (от пользователя)

### Главная логика

> Игрок не знает ничего — и он может что-то открывать через события. Зашёл в триггерную зону — получил знание о рецепте, навыке, NPC и т.п. Во вкладке «Знания» появилась информация. Навыки начинают отображаться в деревьях изучения. Рецепты появляются в крафтовом столе.

### Ключевые принципы

1. **Всё скрыто по умолчанию.** Навыки, рецепты, NPC, фракции — игрок видит только то, что открыл.
2. **Единый провайдер знаний (KnowledgeManager).** Централизованный менеджер в Bootstrap, который отслеживает открытия.
3. **Триггер-компонент для дизайнера.** На любую trigger zone / collider / event вешается скрипт с массивом ассетов. Дизайнер кидает ассеты в инспектор — игрок открывает их при активации.
4. **Не нужно редактировать ассеты.** Достаточно распределить знания по колайдерам (зоны, NPC, ивенты), указав открываемые ассеты.

---

## 4. План реализации V3

### Шаг V3.0 (🔴 БЛОКЕР, новый): стабильный строковый id рецепта

**Проблема:** см. Проблема 8. `int recipeId` зависит от порядка регистрации на сервере и клиенте.

**Решение (рекомендуемое):** строковый ключ, как `skillId` у навыков.

**Файлы:**
- `Assets/_Project/Scripts/Crafting/RecipeData.cs`
  - Добавить `[Header("Identity")] public string recipeId;` — stable key (например `"recipe_health_potion"`)
  - `OnValidate` (Editor): warning если `recipeId` пуст
- `Assets/_Project/Scripts/Crafting/CraftingWorld.cs`
  - Registry перевести с `Dictionary<int, RecipeData>` на `Dictionary<string, RecipeData>` (+ обратный lookup)
  - `RegisterRecipe(RecipeData)` → использует `recipe.recipeId`; если пуст — `LogError` и пропуск
  - `_knownRecipes` → `Dictionary<ulong, HashSet<string>>`
  - `UnlockRecipeKnowledge(clientId, string recipeId)`, `IsRecipeKnown`, `GetKnownRecipeIds`, `ApplyDeathRecipeLoss`, `BuildRecipeKnowledgeSave`, `LoadRecipeKnowledge` — все на string
- `Assets/_Project/Scripts/Crafting/RecipeClientRegistry.cs`
  - `Dictionary<string, RecipeData>` по `recipeId` (вместо int по порядку LoadAll)
  - `GetRecipe(string recipeId)`, `GetRecipeId(RecipeData)` → возвращает `recipe.recipeId`
- `Assets/_Project/Scripts/Crafting/Dto/RecipeKnowledgeDto.cs`
  - `int[] knownRecipeIds` → `string[] knownRecipeIds` (NetworkSerialize string array — как `NpcAttitudeSnapshotDto.knownNpcIds`)
- `Assets/_Project/Scripts/Crafting/RecipeKnowledgeClientState.cs`
  - `HashSet<int> KnownRecipeIds` → `HashSet<string> KnownRecipeIds`
- `Assets/_Project/Scripts/Crafting/UI/CraftingWindow.cs`
  - `GetRecipeDisplayList()`: НЕ вызывать `CraftingWorld.RegisterRecipe(r)` на клиенте; использовать `RecipeClientRegistry.GetRecipeId(r)` / `GetRecipe(recipeId)`
- `Assets/_Project/Scripts/Crafting/CraftingServer.cs`
  - `StartCraftRpc(int recipeId)` → `StartCraftRpc(string recipeId)` (и все вызовы `CraftingWorld.*`)
  - `SendRecipeKnowledgeToClient` — уже использует `GetKnownRecipeIds`, тип поменяется сам
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`
  - `RefreshRecipesKnowledgeCache()` (`:2019`) — `RecipeClientRegistry.GetRecipe(string)`
- **Persistence:** `QuestSaveData.knownRecipes` — `List<int>` → `List<string>` (+ backward-compat загрузка старых int-сейвов через попытку `ToString()` и сверку с registry)

**Оценка:** 3.0 ч.

> **Альтернатива (если дизайн не хочет трогать RecipeData):** гарантировать одинаковый порядок регистрации — оба registry сортируют по `DisplayName` (или имени ассета) перед присваиванием id. Менее надёжно (коллизии имён, рефакторинг), но без миграции сейвов.

---

### Шаг V3.1: KnowledgeManager — единый фасад (сервер)

**Новый файл:** `Assets/_Project/Scripts/Knowledge/KnowledgeManager.cs`

POCO-синглтон (паттерн `SkillsWorld` / `QuestWorld`). Предоставляет единый метод:

```csharp
public bool Unlock(ulong clientId, UnityEngine.Object asset)
```

Автоматически определяет тип ассета и вызывает соответствующий доменный метод:
- `SkillNodeConfig` → `SkillsWorld.UnlockSkillKnowledge(clientId, skill.skillId)`
- `RecipeData` → `CraftingWorld.UnlockRecipeKnowledge(clientId, recipe.recipeId)` (после V3.0 — строковый ключ)
- `FactionDefinition` → `QuestWorld.UnlockFactionKnowledge(clientId, def.factionId)`
- `NpcDefinition` → `QuestWorld.UnlockNpcKnowledge(clientId, npc.npcId)` **+ `UnlockFactionKnowledge(clientId, npc.faction)` если `!= None`** (консистентно с `MarkNpcTalked`, см. Проблема 11)

А также batch-метод:

```csharp
public int UnlockAll(ulong clientId, UnityEngine.Object[] assets)
```

**🔥 Обязательно (Проблема 9):** после любых изменений вызвать persistence:

```csharp
// V3: знания сохраняются сразу (BuildSaveData собирает все 4 домена)
ProjectC.Quests.QuestWorld.Instance?.SavePlayer(clientId);
```

**Создаётся в `SkillsServer.OnNetworkSpawn`** (рядом с `_world = new SkillsWorld()`), `Reset()` в `OnNetworkDespawn` (рядом с `SkillsWorld.Reset()`).

**Оценка:** 2.0 ч.

---

### Шаг V3.2: KnowledgeRevealTrigger — компонент для trigger zone

**Новый файл:** `Assets/_Project/Scripts/Knowledge/KnowledgeRevealTrigger.cs`

**Ключевое решение (Проблема 13):** триггер — **server-authoritative plain MonoBehaviour** (паттерн существующего `SpawnRestartTriggerZone`). Scene-placed объект существует на всех машинах, поэтому **RPC не нужен** — сервер сам видит `OnTriggerEnter`.

```csharp
[RequireComponent(typeof(Collider))]
public class KnowledgeRevealTrigger : MonoBehaviour
{
    [Header("Ассеты, открываемые при активации")]
    public SkillNodeConfig[] skillsToReveal;
    public RecipeData[] recipesToReveal;
    public FactionDefinition[] factionsToReveal;
    public NpcDefinition[] npcsToReveal;

    [Header("Активация")]
    public bool triggerOnce = true;
    public string[] playerTags = { "Player" };   // фильтр «кто считается игроком»
    public UnityEvent onRevealed; // feedback (VFX, звук) — вызывается на сервере

    private bool _triggered;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkManager.Singleton.IsServer) return;   // server-only
        if (_triggered && triggerOnce) return;
        if (!MatchesPlayerTag(other)) return;

        // clientId из NetworkObject вошедшего
        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;
        ulong clientId = netObj.OwnerClientId;

        var all = CollectAssets();
        if (KnowledgeManager.Instance != null)
            KnowledgeManager.Instance.UnlockAll(clientId, all);

        if (triggerOnce) _triggered = true;
        onRevealed?.Invoke();
    }
}
```

Поведение:
- Только на сервере (`NetworkManager.Singleton.IsServer`)
- `clientId` — из `other.GetComponentInParent<NetworkObject>().OwnerClientId` (фильтр `NetworkPlayer`/тег `Player` — защита от NPC/предметов в зоне)
- `triggerOnce`: после первого срабатывания `_triggered = true` (локальный флаг сервера; при деактивации GameObject триггер не сработает повторно)
- `onRevealed`: локальный feedback на сервере (в будущем — broadcast VFX/звук)
- После unlock — снапшоты рассылаются **существующими** методами (см. V3.7)

**Опциональный RPC-путь** (только если зона спавнится клиентски и сервер не видит коллайдер):
- `NetworkPlayer.RequestRevealKnowledgeRpc` c DTO `KnowledgeRevealRequestDto`:
  - `string[] skillIds`, `string[] recipeIds`, `byte[] factionIds`, `string[] npcIds`
  - Клиент маппит свои ассеты → id через `RecipeClientRegistry.GetRecipeId` (после V3.0) и т.п.
  - Rate limit на сервере (паттерн `SkillsServer.RateLimit` / `CraftingServer.CheckRateLimit`)
- **В host-and-play этот путь не нужен.**

**Оценка:** 2.0 ч. (1.5 ч. без RPC-пути)

---

### Шаг V3.3: Knowledge-фильтр в SkillTreeWindow

**Файл:** `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs`

В метод `ApplyFilterAndSearch()` добавить проверку:

```csharp
private void ApplyFilterAndSearch()
{
    _filteredSkills.Clear();
    var knownIds = SkillsClientState.Instance?.KnownSkillIds
                   ?? new HashSet<string>();

    foreach (var s in _allSkillConfigs)
    {
        if (s == null) continue;
        if (!MatchesDiscipline(s)) continue;
        if (!MatchesSearch(s)) continue;

        // V3: knowledge gate
        if (!IsSkillVisible(s, knownIds)) continue;

        _filteredSkills.Add(s);
    }
    RebuildSkillList();
}

private bool IsSkillVisible(SkillNodeConfig s, HashSet<string> knownIds)
{
    // None = visible to everyone
    if (s.knowledgeUnlockType == KnowledgeUnlockType.None) return true;
    // Learned = implicitly known
    var learned = SkillsClientState.Instance?.CurrentSkills;
    if (learned != null && learned.Contains(s.skillId)) return true;
    // Explicitly known
    return knownIds.Contains(s.skillId);
}
```

**Дизайн-вопрос (уточнить у дизайнера):** скрывать неизвестный навык полностью или показывать «заблокированный» узел с подсказкой `knowledgeUnlockDescription` («как узнать»)? Поле уже есть в конфиге, но нигде не читается (Проблема 6). **Рекомендация MVP:** скрывать полностью; подсказки — отдельной итерацией.

**Оценка:** 0.5 ч.

---

### Шаг V3.4: Knowledge-фильтр в SocialSkillTreeWindow

**Файл:** `Assets/_Project/Scripts/Skills/UI/SocialSkillTreeWindow.cs`

Аналогично V3.3 — добавить `IsSkillVisible` в `ApplyFilterAndSearch()`:

```csharp
private void ApplyFilterAndSearch()
{
    _filteredSkills.Clear();
    var knownIds = SkillsClientState.Instance?.KnownSkillIds
                   ?? new HashSet<string>();

    foreach (var s in _allSkillConfigs)
    {
        if (s == null) continue;
        if (!MatchesSearch(s)) continue;

        // V3: knowledge gate
        if (!IsSkillVisible(s, knownIds)) continue;

        _filteredSkills.Add(s);
    }
    RebuildSkillTree();
}
```

**Оценка:** 0.5 ч.

---

### Шаг V3.5: Knowledge-фильтр в CharacterWindow social-колонке

**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`, метод `RefreshSkillsCache()` (строка 2365)

В social-блоке добавить проверку: если навык не `None` и не в `KnownSkillIds` — пропустить (не добавлять в `_skillsSocialCache`).

```csharp
else // Social
{
    var knownIds = SkillsClientState.Instance?.KnownSkillIds;
    // V3: knowledge gate — skip if not known
    if (skill.knowledgeUnlockType != KnowledgeUnlockType.None)
    {
        bool isLearned = learned != null && learned.Contains(skill.skillId);
        bool isKnown = knownIds != null && knownIds.Contains(skill.skillId);
        if (!isLearned && !isKnown) continue;
    }
    // ... existing logic
}
```

**Оценка:** 0.5 ч.

---

### Шаг V3.6: Интеграция KnowledgeManager в Bootstrap

**Файл:** `Assets/_Project/Scripts/Skills/SkillsServer.cs`, метод `OnNetworkSpawn()`

Добавить создание `KnowledgeManager`:

```csharp
// V3: KnowledgeManager — единый фасад
if (KnowledgeManager.Instance == null)
    new KnowledgeManager();
```

Также `KnowledgeManager.Reset()` при `OnNetworkDespawn`.

**Оценка:** 0.5 ч.

---

### Шаг V3.7: Сетевая интеграция (снапшоты после unlock)

**Без новых RPC.** После `KnowledgeManager.UnlockAll` сервер рассылает снапшоты **существующими** публичными методами:

```
[Server] KnowledgeManager.UnlockAll(clientId, assets)
  → QuestWorld.SavePlayer(clientId)                      // V3.1 — persistence
  → SkillsServer.SendSnapshotToOwner(clientId)           // уже public, шлёт knownSkillIds
  → CraftingServer.SendRecipeKnowledgeToClient(clientId) // уже public, шлёт knownRecipeIds
  → QuestServer.BroadcastKnowledgeChange(clientId)       // уже public: reputation + npcAttitude
  → [Client] UI обновляется через существующие события:
      SkillsClientState.OnSkillsUpdated
      RecipeKnowledgeClientState.OnRecipeKnowledgeUpdated
      ReputationClientState.OnReputationUpdated
      NpcAttitudeClientState.OnNpcAttitudeUpdated
```

**Опционально (только для клиентски-спавненных зон):** `NetworkPlayer.RequestRevealKnowledgeRpc` + `KnowledgeRevealRequestDto` + rate limit — см. V3.2.

**Оценка:** 0.5 ч. (только опциональный RPC-путь 1.5 ч.)

---

### Шаг V3.8: UI toast «Открыто знание»

**Файл:** `Assets/_Project/Scripts/Knowledge/KnowledgeRevealTrigger.cs` (расширение) или отдельный `KnowledgeToast.cs`

Все 4 события уже существуют (см. V3.7). Схема «до/после»:

- Подписаться на `SkillsClientState.OnSkillsUpdated`, `RecipeKnowledgeClientState.OnRecipeKnowledgeUpdated`, `ReputationClientState.OnReputationUpdated`, `NpcAttitudeClientState.OnNpcAttitudeUpdated`
- В обработчике сравнить предыдущий set с новым — определить что именно открылось
- Показать UI toast: «Открыто знание: ???» (displayName навыка/рецепта/NPC/фракции)
- NPC и фракции приходят **вместе** (BroadcastKnowledgeChange) — toast группирует

**Опционально:** использовать существующий `UIManager` toast или создать легковесный `KnowledgeToast`.

**Оценка:** 1.5 ч.

---

### Шаг V3.9: CraftingWindow — фильтр по KnownRecipeIds

**Файл:** `Assets/_Project/Scripts/Crafting/UI/CraftingWindow.cs`, `GetRecipeDisplayList()` (строка 283)

**Сначала — семантика видимости рецепта (Проблема 10):**

Добавить в `RecipeKnowledgeUnlockType` значение `None = 0` (рецепт виден всегда) и **сдвинуть** `Blueprint` на 1:

```csharp
public enum RecipeKnowledgeUnlockType : byte
{
    None = 0,        // всегда виден (backward compat: старые ассеты со значением 0 получают None)
    Blueprint = 1,   // скрыт, открывается чертежом/предметом
    NpcTeach = 2,    // скрыт, открывается обучением у NPC
    QuestReward = 3, // скрыт, открывается наградой квеста
    Station = 4,     // скрыт, открывается первым использованием станции
}
```

> Существующие ассеты сериализованы со значением 0 = Blueprint → после сдвига станут `None` = виден всегда. **Backward compatible**, ничего не сломается.

**Фильтр в списке станции:**

```csharp
private List<KeyValuePair<string, string>> GetRecipeDisplayList()
{
    var list = new List<KeyValuePair<string, string>>();
    if (_currentConfig == null) return list;
    var known = RecipeKnowledgeClientState.Instance?.KnownRecipeIds;
    for (int i = 0; i < _currentConfig.AllowedRecipes.Count; i++)
    {
        var r = _currentConfig.AllowedRecipes[i];
        if (r == null) continue;
        // V3: knowledge gate
        if (r.KnowledgeUnlockType != RecipeKnowledgeUnlockType.None)
        {
            bool isKnown = known != null && known.Contains(r.recipeId);  // после V3.0
            if (!isKnown) continue;
        }
        string recipeId = RecipeClientRegistry.GetRecipeId(r);           // после V3.0 — string
        string displayName = CraftingClientState.Instance != null
            ? CraftingClientState.Instance.GetRecipeDisplayName(recipeId)
            : r.DisplayName;
        list.Add(new KeyValuePair<string, string>(recipeId, $"{displayName} ({r.CraftSeconds:0.#}с)"));
    }
    return list;
}
```

**🔥 Ребилд при открытом окне:** если игрок стоит у станции и получил рецепт через триггер — список должен обновиться без переоткрытия. Подписать `CraftingWindow` на `RecipeKnowledgeClientState.OnRecipeKnowledgeUpdated` → `BuildRecipeList()` (lazy-subscribe в `Update`, как остальные подписки).

**Сопутствующие правки:**
- `_selectedRecipeId` (int) → `_selectedRecipeKey` (string)
- `OnRecipeSelected`, `BuildIngredientsPanel`, `OnStartClicked` — по строковому ключу
- `CraftingClientState.GetRecipeDisplayName(int)` → `(string)`, `GetRecipe(int)` → `(string)`

**Оценка:** 1.5 ч. (с учётом смены int → string по V3.0)

---

### Шаг V3.10: Инспектор KnowledgeRevealTrigger — UX для дизайнера

- Кастомный инспектор или `PropertyDrawer`, который показывает:
  - Preview названий открываемых ассетов (не только object fields)
  - Группировка по типам (Skills / Recipes / Factions / NPCs)
  - Кнопка «Добавить все зависимости» (авто-заполнение из связанных ассетов)
  - **Валидация:** предупреждение, если у `RecipeData` пустой `recipeId` (после V3.0), или `SkillNodeConfig.skillId` пуст
- **Опционально:** Editor-скрипт `KnowledgeRevealTriggerEditor.cs`

**Оценка:** 1.5 ч.

---

### Шаг V3.11 (новый): SkillInputService.SetKnownSkills — синхронизация биндинга

**Проблема:** см. Проблема 12.

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillsClientState.cs`
  - В `OnSkillsSnapshotReceived()` после обновления `KnownSkillIds` вызвать:
    ```csharp
    // V3: список навыков для окна биндинга = изученные (+ известные, по дизайну)
    var bindable = new HashSet<string>(CurrentSkills);
    bindable.UnionWith(KnownSkillIds);   // или только CurrentSkills — решение дизайна
    ProjectC.Skills.SkillInputService.Instance?.SetKnownSkills(bindable);
    ```
- `SkillBindingWindow` — уже читает `GetAllSkillIds()`, отдельная правка не нужна

**Дизайн-вопрос:** можно ли биндить на слот только изученный навык (тогда `SetKnownSkills(CurrentSkills)`) или также известный, но не изученный (тогда объединение)? **Рекомендация:** только изученные — активация всё равно пройдёт server gate.

**Оценка:** 0.5 ч.

---

## 5. Сводка изменений

### Новые файлы (3)

| Файл | Описание |
|---|---|
| `Assets/_Project/Scripts/Knowledge/KnowledgeManager.cs` | POCO-синглтон, единый фасад Unlock/UnlockAll + SavePlayer |
| `Assets/_Project/Scripts/Knowledge/KnowledgeRevealTrigger.cs` | MonoBehaviour для trigger zone / события (server-only) |
| `Assets/_Project/Scripts/Knowledge/KnowledgeToast.cs` | UI toast «Открыто знание» (опционально) |

### Модифицируемые файлы (12)

| Файл | Изменение |
|---|---|
| `Crafting/RecipeData.cs` | **V3.0**: +`recipeId` (string), `KnowledgeUnlockType`: +`None`, сдвиг `Blueprint` |
| `Crafting/RecipeClientRegistry.cs` | **V3.0**: registry по `recipeId` (string) |
| `Crafting/CraftingWorld.cs` | **V3.0**: registry + `_knownRecipes` на string |
| `Crafting/Dto/RecipeKnowledgeDto.cs` | **V3.0**: `knownRecipeIds` → `string[]` |
| `Crafting/RecipeKnowledgeClientState.cs` | **V3.0**: `KnownRecipeIds` → `HashSet<string>` |
| `Crafting/CraftingServer.cs` | **V3.0**: `StartCraftRpc(string recipeId)` и вызовы |
| `Crafting/UI/CraftingWindow.cs` | **V3.9**: knowledge gate + ребилд по событию + string-ключ; **V3.0**: без `RegisterRecipe` на клиенте |
| `Skills/UI/SkillTreeWindow.cs` | **V3.3**: `ApplyFilterAndSearch`: +knowledge gate |
| `Skills/UI/SocialSkillTreeWindow.cs` | **V3.4**: `ApplyFilterAndSearch`: +knowledge gate |
| `Skills/SkillsServer.cs` | **V3.6**: `OnNetworkSpawn`: +KnowledgeManager создание/Reset |
| `Skills/SkillsClientState.cs` | **V3.11**: `OnSkillsSnapshotReceived` → `SkillInputService.SetKnownSkills` |
| `UI/Client/CharacterWindow.cs` | **V3.5**: knowledge gate в social-колонке; **V3.0**: `RefreshRecipesKnowledgeCache` по string |
| `Quests/Persistence/QuestSaveData.cs` | **V3.0**: `knownRecipes` → `List<string>` (+ backward-compat загрузка) |
| `Player/NetworkPlayer.cs` | **V3.2 (опционально)**: `RequestRevealKnowledgeRpc` + DTO |

### Оценка

| Шаг | Часы |
|---|---|
| V3.0 Стабильный recipeId (блокер) | 3.0 |
| V3.1 KnowledgeManager (+SavePlayer, NPC→фракция) | 2.0 |
| V3.2 KnowledgeRevealTrigger (server-only) | 2.0 |
| V3.3 Filter SkillTreeWindow | 0.5 |
| V3.4 Filter SocialSkillTreeWindow | 0.5 |
| V3.5 Filter CharacterWindow social | 0.5 |
| V3.6 Bootstrap integration | 0.5 |
| V3.7 Network integration (существующие снапшоты) | 0.5 |
| V3.8 UI toast | 1.5 |
| V3.9 CraftingWindow filter + ребилд + None-семантика | 1.5 |
| V3.10 Editor UX | 1.5 |
| V3.11 SkillInputService.SetKnownSkills | 0.5 |
| **Итого** | **~14.5 часов** |

---

## 6. Дизайн-контракт: как это работает для дизайнера

### Сценарий: игрок заходит в деревню

1. Дизайнер создаёт GameObject `TriggerZone_RevealVillage` с `Collider (IsTrigger=true)`
2. Вешает компонент `KnowledgeRevealTrigger`
3. В инспекторе заполняет:
   - **Skills To Reveal:** `skill_bow_making`, `skill_herbalism`, `skill_trade_basic`
   - **Recipes To Reveal:** `recipe_health_potion`, `recipe_arrow_wooden`
   - **NPCs To Reveal:** `npc_blacksmith`, `npc_herbalist`
   - **Factions To Reveal:** `Faction_VillageCouncil`
4. Игрок заходит в зону → триггер срабатывает (на сервере):
   - Во вкладке «Знания» появляются: 3 навыка, 2 рецепта, 2 NPC, 1 фракция
   - В дереве боевых/социальных навыков появляются 3 новых узла
   - В крафтовом столе появляются 2 новых рецепта (если окно открыто — список обновится сразу)
   - NPC «Кузнец» и «Травница» отображаются в списке отношений
   - Фракция «Совет деревни» отображается в списке репутации
   - UI toast: «Открыто знание: ???»
   - Прогресс сохранён (даже если игрок вышел через 5 секунд)

### Сценарий: NPC рассказывает о навыке

1. На NPC-префаб вешается `KnowledgeRevealTrigger`
2. Активация через `UnityEvent` в диалоговой системе (`DialogueAction.FireUnityEvent`)
3. Тот же результат — знания открываются централизованно

> **Важно (Проблема 11):** если в массиве указан NPC — его фракция открывается **автоматически**. Дизайнеру не нужно дублировать её в `Factions To Reveal`.

> **Важно (Проблема 10):** рецепты с `KnowledgeUnlockType = None` (включая все существующие, после сдвига enum) видны на станции всегда. Скрыты только `Blueprint/NpcTeach/QuestReward/Station` до момента открытия.

---

## 7. Примечания и риски

- **Backward compatibility (навыки):** все существующие навыки с `knowledgeUnlockType = None` будут видны как и раньше. Фильтр влияет только на `!= None`.
- **Backward compatibility (рецепты):** сдвиг enum `Blueprint(0→1)` + `None(0)` — существующие ассеты автоматически становятся `None` (виден всегда). Поведение станций не меняется.
- **Backward compatibility (сейвы рецептов):** `QuestSaveData.knownRecipes` — int → string; старые int-сейвы грузятся через попытку `ToString()` + сверку с registry (V3.0).
- **SkillInputService.SetKnownSkills** (V3.11): сейчас метод мёртвый; после подключения окно биндинга начнёт показывать список — проверить, что старый флоу биндинга не сломался (раньше показывал «(нет доступных навыков)»).
- **Death loss:** `ApplyDeathSkillKnowledgeLoss` / `ApplyDeathRecipeLoss` уже работают. Потеря знаний NPC/фракций при смерти **не реализована** — вопрос дизайна, вынести в отдельный тикет.
- **Квестовые знания (QuestWorld.knownRecipes):** в V2-документе шаги QR (`QuestUnlockType.Recipe` в `ApplyQuestRewards`) и A14 (`DialogueActionType`) отложены — не входят в V3, но KnowledgeManager.Unlock можно будет переиспользовать для них.
- **Rate limit:** если включается опциональный RPC-путь (V3.2) — обязателен rate limit (паттерн `SkillsServer.RateLimit` / `CraftingServer.CheckRateLimit`).
- **Согласованность recipeId:** после V3.0 сервер и клиент читают один и тот же строковый ключ из ассета — расхождение id исключено. Единственный источник правды — `RecipeData.recipeId`.
- **NpcDefinition в триггере:** `UnlockNpcKnowledge` не валидирует существование NPC в `QuestDatabase` — если дизайнер ошибся, знание «пустое» (UI не покажет). V3.10 валидирует в инспекторе.
