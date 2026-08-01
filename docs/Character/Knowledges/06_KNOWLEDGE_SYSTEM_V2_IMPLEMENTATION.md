# Knowledge System v2 — Реализация (Фаза A: данные и сервер)

> **Основание:** `05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md` §6
> **Начало:** 2026-08-01

## Статус

| Шаг | Описание | Статус |
|-----|----------|--------|
| A1 | KnowledgeUnlockType + поля в SkillNodeConfig | ✅ готово |
| A2 | RecipeKnowledgeUnlockType + поля в RecipeData | ✅ готово |
| A3 | SkillsSave + knownSkillIds | ✅ готово |
| A4 | QuestSaveData + knownRecipes | ✅ готово |
| A5 | KnowledgeLossConfig (SO) | ✅ готово |
| A6 | FactionDefinition + FactionCatalog | ✅ готово |
| A7 | SkillsWorld: _knownSkills, UnlockSkillKnowledge, AutoOnSkillLearned | ✅ готово |
| A8 | SkillsSnapshotDto + knownSkillIds; SkillsServer + SkillsClientState | ✅ готово |
| A9 | CraftingWorld: _knownRecipes, IsRecipeKnown, UnlockRecipeKnowledge | ✅ готово |
| A10 | CraftingServer.StartCraftRpc: server gate IsRecipeKnown + AllowedRecipes | ✅ готово |
| A11 | RecipeKnowledgeDto + RecipeKnowledgeClientState (новые файлы) | ✅ готово |
| A12 | QuestWorld: ApplyDeathKnowledgeLoss + knownRecipes в BuildSaveData/LoadPlayer | ✅ готово |
| A13 | Death hook в PlayerTarget.TriggerDeathRespawn | ✅ готово |
| A14 | DialogueActionType (опционально) | ⏸️ отложен |
| NMC | NetworkManagerController auto-spawn RecipeKnowledgeClientState | ⬜ pending |
| NP | NetworkPlayer ReceiveRecipeKnowledgeTargetRpc | ⬜ pending |
| QR | QuestUnlockType.Recipe в ApplyQuestRewards | ⬜ pending (T-Q16 ещё не реализован) |

## Изменённые файлы

### Модифицированные
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` — +enum KnowledgeUnlockType, +3 поля
- `Assets/_Project/Scripts/Crafting/RecipeData.cs` — +enum RecipeKnowledgeUnlockType, +3 поля, +3 свойства
- `Assets/_Project/Scripts/Stats/Persistence/SkillsSave.cs` — +knownSkillIds (backward-compat: null → пусто)
- `Assets/_Project/Quests/Persistence/QuestSaveData.cs` — +knownRecipes
- `Assets/_Project/Scripts/Skills/SkillsWorld.cs` — +_knownPerPlayer, +GetKnownSkillIds, +IsSkillKnown, +UnlockSkillKnowledge, +AutoOnSkillLearned, +BuildSaveData/LoadPlayer, +RemovePlayer cleanup
- `Assets/_Project/Scripts/Skills/Dto/SkillsDto.cs` — +knownSkillIds field, +NetworkSerialize refactor
- `Assets/_Project/Scripts/Skills/SkillsClientState.cs` — +KnownSkillIds, +OnSkillsSnapshotReceived handle, +ClearState
- `Assets/_Project/Scripts/Skills/SkillsServer.cs` — SendSnapshotToOwner включает knownSkillIds
- `Assets/_Project/Scripts/Crafting/CraftingWorld.cs` — +_knownRecipes, +IsRecipeKnown, +UnlockRecipeKnowledge, +GetKnownRecipeIds, +ApplyDeathRecipeLoss, +BuildRecipeKnowledgeSave, +LoadRecipeKnowledge, +Shutdown cleanup
- `Assets/_Project/Scripts/Crafting/CraftingServer.cs` — StartCraftRpc: +IsRecipeKnown gate, +AllowedRecipes gate
- `Assets/_Project/Quests/Core/QuestWorld.cs` — +ApplyDeathKnowledgeLoss, +Shuffle, +knownRecipes in BuildSaveData/LoadPlayer
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — +ApplyDeathKnowledgeLoss(), hook в TriggerDeathRespawn

### Новые
- `Assets/_Project/Scripts/Knowledge/KnowledgeLossConfig.cs` — ScriptableObject конфига потери знаний
- `Assets/_Project/Scripts/Knowledge/FactionDefinition.cs` — ScriptableObject данных фракции
- `Assets/_Project/Scripts/Knowledge/FactionCatalog.cs` — каталог FactionDefinition из Resources/Data/Factions/
- `Assets/_Project/Scripts/Crafting/Dto/RecipeKnowledgeDto.cs` — DTO для сети
- `Assets/_Project/Scripts/Crafting/RecipeKnowledgeClientState.cs` — client-side state

### Не трогались
- `docs/gdd/*`, `BootstrapScene.unity`, `UIManager.cs`, v1-каналы репутации/NPC

## Оставшиеся задачи (out of scope этой итерации)

1. **NetworkManagerController**: добавить `CreateRecipeKnowledgeClientState()` auto-spawn
2. **NetworkPlayer**: добавить `ReceiveRecipeKnowledgeTargetRpc(RecipeKnowledgeDto)`
3. **QuestServer**: добавить `SendRecipeKnowledgeToClient()` + вызов после разблокировки рецепта
4. **QuestUnlockType.Recipe**: реализовать в ApplyQuestRewards (зависит от T-Q16)
5. **SO-ассеты**: создать default `KnowledgeLossConfig.asset` в `Resources/Data/Knowledge/`
6. **SO-ассеты**: создать `FactionDefinition` для каждой фракции в `Resources/Data/Factions/`
7. **Фаза B (UI)**: вкладка «Знания» в CharacterWindow (UXML + USS + C#)

## История

| Дата | Изменения |
|------|-----------|
| 2026-08-01 | Фаза A (A1-A13): данные + серверная логика знаний v2. 12 файлов изменено, 5 создано. |
