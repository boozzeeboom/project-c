# Knowledge System V3 — Лог интеграции

> **План:** `07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md`
> **Дата реализации:** 2026-08-03
> **Коммиты:** `467e20b3` (V3.0), `8605b008` (V3.1–V3.11)

---

## Реализованные шаги

### V3.0 — Стабильный строковый recipeId
✅ `467e20b3`

- `RecipeData.recipeId` (string) + `RecipeKnowledgeUnlockType` сдвиг `None=0`
- `RecipeClientRegistry` → `Dictionary<string, RecipeData>`
- `CraftingWorld` → string recipeId во всём: registry, `_knownRecipes`, методы
- `CraftingJob.RecipeId` → string
- `CraftingSnapshotDto.activeRecipeId` → string (NetworkSerialize)
- `RecipeKnowledgeDto` → `string[]`
- `RecipeKnowledgeClientState` → `HashSet<string>`
- `CraftingServer.StartCraftRpc(string)`, `BuildSnapshot`, `SendRecipeKnowledgeToClient`
- `CraftingStation._activeRecipeId` → `NetworkVariable<string>`
- `CraftingClientState` → string в RequestStartCraft/GetRecipe/EnsureRecipesLoaded
- `CraftingWindow` → `_selectedRecipeKey` (string), `RecipeClientRegistry` вместо `RegisterRecipe`
- `CharacterWindow` → `RecipeKnowledgeItem.recipeId` string
- `QuestSaveData.knownRecipes` → `List<string>`
- `CraftingProgressController` → null-check

### V3.1 — KnowledgeManager единый фасад
✅ `8605b008`

- `Knowledge/KnowledgeManager.cs`: POCO-синглтон
- `Unlock(clientId, asset)` — auto-detect type (SkillNodeConfig/RecipeData/NpcDefinition/FactionDefinition)
- `UnlockAll(clientId, assets[])` — batch + SavePlayer + SendSnapshots
- NpcDefinition → auto-открытие фракции (консистентно с MarkNpcTalked)

### V3.2 — KnowledgeRevealTrigger
✅ `8605b008`

- `Knowledge/KnowledgeRevealTrigger.cs`: server-authoritative MonoBehaviour
- Collider isTrigger auto-set в Awake
- `OnTriggerEnter` → `KnowledgeManager.UnlockAll` (server-only gate)
- `triggerOnce`, `playerTags[]`, `UnityEvent onRevealed`
- OnValidate: валидация пустых recipeId/skillId

### V3.3 — Knowledge-фильтр SkillTreeWindow
✅ `8605b008`

- `ApplyFilterAndSearch`: +`IsSkillVisible(s, learned, knownIds)`
- `None` → visible; learned → visible; in `KnownSkillIds` → visible; else hidden

### V3.4 — Knowledge-фильтр SocialSkillTreeWindow
✅ `8605b008`

- Аналогично V3.3

### V3.5 — Knowledge-фильтр CharacterWindow social
✅ `8605b008`

- `RefreshSkillsCache`: social-блок — если `knowledgeUnlockType != None`, не изучен и не known → skip

### V3.6 — Интеграция KnowledgeManager в Bootstrap
✅ `8605b008`

- `SkillsServer.OnNetworkSpawn`: `new KnowledgeManager()`
- `SkillsServer.OnNetworkDespawn`: `KnowledgeManager.Reset()`

### V3.7 — Сетевая интеграция (снапшоты)
✅ `8605b008`

- Встроено в `KnowledgeManager.UnlockAll`: после доменных вызовов + SavePlayer → `SendSnapshots(clientId)`
- `SkillsServer.SendSnapshotToOwner`, `CraftingServer.SendRecipeKnowledgeToClient`, `QuestServer.BroadcastKnowledgeChange`

### V3.8 — UI toast
✅ `8605b008`

- `Knowledge/KnowledgeToast.cs`: подписка на 4 события, diff до/после, Debug.Log toast

### V3.9 — CraftingWindow knowledge gate
✅ `8605b008`

- `GetRecipeDisplayList`: `KnowledgeUnlockType != None` → проверка в `KnownRecipeIds`
- Подписка на `OnRecipeKnowledgeUpdated` → `BuildRecipeList()` при открытом окне

### V3.10 — Editor UX
✅ `8605b008`

- `Knowledge/Editor/KnowledgeRevealTriggerEditor.cs`: кастомный инспектор
- Preview названий, группировка по типам, валидация, сводка

### V3.11 — SkillInputService.SetKnownSkills
✅ `8605b008`

- `SkillsClientState.OnSkillsSnapshotReceived`: `SkillInputService.Instance?.SetKnownSkills(CurrentSkills)` (только изученные)

---

## Проверка

- [x] V3.0: recipeId на всех RecipeData, клиент и сервер используют один строковый ключ
- [x] V3.1–V3.7: зайти в trigger zone → знания открываются, сохраняются, снапшоты рассылаются
- [x] V3.3–V3.5: неизвестные навыки скрыты в SkillTreeWindow, SocialSkillTreeWindow, CharacterWindow
- [x] V3.9: неизвестные рецепты скрыты на станции, список обновляется при получении знаний
- [x] Build → 0 compile errors

---

## Отложено (не в V3)

- Death loss для NPC/фракций — не реализовано, отдельный тикет
- Квестовые знания (QuestUnlockType.Recipe в ApplyQuestRewards) — KnowledgeManager.Unlock можно переиспользовать
- RPC-путь для клиентски-спавненных зон (не нужно в host-and-play)
- Backward-compat для старых int-сейвов рецептов (TryResolveLegacyId есть в RecipeClientRegistry, но не используется при загрузке сейвов)
