# Knowledge System — Iterations

## Итерация от 2026-07-20

**Задача:** Интеграция Knowledge System (server-authoritative знание фракций/NPC) по анализу `02_KNOWLEDGE_SYSTEM_DEEP_ANALYSIS.md`

**Коммит:** `f0aae06` — T-KNOW: Knowledge System integration — server-authoritative faction/NPC knowledge with UI filtering

**Изменения (8 файлов + docs):**
- `Assets/_Project/Quests/Persistence/QuestSaveData.cs` — +knownFactions, +knownNpcs
- `Assets/_Project/Quests/Core/QuestWorld.cs` — +dicts, +6 методов, MarkNpcTalked/BuildSaveData/LoadPlayer/Shutdown
- `Assets/_Project/Quests/Dto/ReputationSnapshotDto.cs` — +knownFactionIds, +knownNpcIds в DTO
- `Assets/_Project/Quests/Network/QuestServer.cs` — Build-методы + BroadcastKnowledgeChange + wire
- `Assets/_Project/Reputation/ReputationClientState.cs` — +KnownFactionIds
- `Assets/_Project/Reputation/NpcAttitudeClientState.cs` — +KnownNpcIds
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — knowledge-фильтрация + fallback cleanup
- `docs/Character/Knowledges/` — анализы + интеграционный лог

## Итерация от 2026-07-23: V2 Deep Analysis

**Задача:** Глубокий анализ расширения Knowledge System V2 на все подсистемы игры.

**Документ:** `04_KNOWLEDGE_SYSTEM_V2_DEEP_ANALYSIS.md`

**4 области анализа:**
1. **CharacterWindow «Репутация» → «Знания»:** полный редизайн вкладки как knowledge-hub с 6 категориями (Фракции, NPC, Навыки боевые, Навыки соц., Рецепты, Квесты). Двухпанельный layout (left: категории, right: детали).
2. **Skills Knowledge Unlock:** `SkillNodeConfig` получает `KnowledgeUnlockType` enum (None/NpcTrainer/QuestReward/ItemUse/FactionLevel/WorldDiscovery/AutoOnSkillLearned) + 5 полей. `SkillsWorld` получает `_knownSkills` с авто-анлоком через `AutoOnSkillLearned`.
3. **Death → Knowledge Loss:** Новый `DeathKnowledgeLossConfig` SO. При смерти теряется % знаний (factions/NPCs/recipes), но НЕ навыки. Защита: высокий reputation/attitude защищает от забывания. Минимальный retention (minRetainFactions=2).
4. **Crafting Recipes Knowledge:** `RecipeData` получает `RecipeKnowledgeUnlockType` enum (None/NpcTrainer/BlueprintItem/QuestReward/Research). `CraftingWorld` получает `_knownRecipes`.

**Ключевые архитектурные решения:**
- **ADR-5:** Единый `KnowledgeSummaryDto` (все 6 типов знаний в одном пакете) вместо расширения v1 DTO
- **ADR-6:** Known skills ≠ Learned skills (две независимые структуры)
- **ADR-7:** Смерть → потеря знаний, не навыков (фрустрация без потери прогрессии)
- **ADR-8:** Всё конфигурируется в SO (никакого хардкода)

**Оценка:** 12 шагов, ~19 файлов (3 новых), ~12-14 часов.
**Статус:** Анализ завершён, ожидает approval перед реализацией.

## Итерация от 2026-08-01: V2 Implementation — Phase A (Data + Server)

**Задача:** Реализация Фазы A плана из `05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md`

**Коммиты:**
- `2bbd5571` — T-KNOWLEDGE-V2 Phase A: skills/recipes knowledge, death loss, CraftingWorld gates
- `c1ece4bc` — T-KNOWLEDGE-V2 A13: death knowledge loss hook in PlayerTarget.TriggerDeathRespawn

**Изменения (17 файлов: 12 modified + 5 new):**

| Файл | Изменение |
|------|-----------|
| `Scripts/Skills/SkillNodeConfig.cs` | +enum KnowledgeUnlockType, +3 поля |
| `Scripts/Crafting/RecipeData.cs` | +enum RecipeKnowledgeUnlockType, +3 поля + свойства |
| `Scripts/Stats/Persistence/SkillsSave.cs` | +knownSkillIds (backward-compat) |
| `Quests/Persistence/QuestSaveData.cs` | +knownRecipes |
| `Scripts/Skills/SkillsWorld.cs` | +_knownPerPlayer, +5 knowledge-методов, +BuildSaveData/LoadPlayer |
| `Scripts/Skills/Dto/SkillsDto.cs` | +knownSkillIds, refactor NetworkSerialize |
| `Scripts/Skills/SkillsClientState.cs` | +KnownSkillIds, +handle knownSkillIds |
| `Scripts/Skills/SkillsServer.cs` | SendSnapshotToOwner включает knownSkillIds |
| `Scripts/Crafting/CraftingWorld.cs` | +_knownRecipes, +7 knowledge-методов |
| `Scripts/Crafting/CraftingServer.cs` | StartCraftRpc: +IsRecipeKnown gate, +AllowedRecipes gate |
| `Quests/Core/QuestWorld.cs` | +ApplyDeathKnowledgeLoss, +Shuffle, +knownRecipes persist |
| `Scripts/Combat/Implementations/PlayerTarget.cs` | +ApplyDeathKnowledgeLoss(), hook в TriggerDeathRespawn |
| **NEW** `Scripts/Knowledge/KnowledgeLossConfig.cs` | SO конфига потери знаний |
| **NEW** `Scripts/Knowledge/FactionDefinition.cs` | SO данных фракции |
| **NEW** `Scripts/Knowledge/FactionCatalog.cs` | каталог фракций из Resources |
| **NEW** `Scripts/Crafting/Dto/RecipeKnowledgeDto.cs` | DTO для сети |
| **NEW** `Scripts/Crafting/RecipeKnowledgeClientState.cs` | client-side state |

**Оставшиеся задачи:**
- NMC auto-spawn RecipeKnowledgeClientState
- NetworkPlayer ReceiveRecipeKnowledgeTargetRpc
- QuestServer SendRecipeKnowledgeToClient
- SO-ассеты: KnowledgeLossConfig.asset, FactionDefinition для каждой фракции
- Фаза B (UI): вкладка «Знания» в CharacterWindow

---

## Итерация от 2026-08-01 (вечер): V2 Implementation — Phase A Reground

**Задача:** Перезапись всех файлов Phase A после повреждения merge-конфликтами через `replace_in_file`

**Коммит:** `a6397959` — T-KNOWLEDGE-V2 Phase A: навыки/рецепты knowledge, death loss, CraftingWorld/SkillsWorld гейты, персистенс

**Изменения (12 файлов):**
| Файл | Изменение |
|------|-----------|
| `Scripts/Skills/SkillNodeConfig.cs` | +enum KnowledgeUnlockType, +3 поля |
| `Scripts/Crafting/RecipeData.cs` | +enum RecipeKnowledgeUnlockType, +3 поля + свойства |
| `Scripts/Stats/Persistence/SkillsSave.cs` | +knownSkillIds |
| `Quests/Persistence/QuestSaveData.cs` | +knownRecipes |
| `Scripts/Skills/SkillsWorld.cs` | +_knownPerPlayer, +IsSkillKnown, +UnlockSkillKnowledge, +AutoOnSkillLearned, +ApplyDeathSkillKnowledgeLoss, +persist |
| `Scripts/Skills/Dto/SkillsDto.cs` | +knownSkillIds, refactor SerializeStringArray |
| `Scripts/Skills/SkillsClientState.cs` | +KnownSkillIds HashSet, +ClearState |
| `Scripts/Skills/SkillsServer.cs` | SendSnapshotToOwner включает knownSkillIds |
| `Scripts/Crafting/CraftingWorld.cs` | +_knownRecipes, +IsRecipeKnown, +UnlockRecipeKnowledge, +ApplyDeathRecipeLoss, +LoadRecipeKnowledge |
| `Scripts/Crafting/CraftingServer.cs` | StartCraftRpc: +IsRecipeKnown gate, +AllowedRecipes gate |
| `Quests/Core/QuestWorld.cs` | +knownRecipes persist, +ApplyDeathKnowledgeLoss |
| `Scripts/Combat/Implementations/PlayerTarget.cs` | +ApplyDeathKnowledgeLoss hook (25% loss) в TriggerDeathRespawn |

**Статус:** Компиляция чистая. Фаза A (data + server) завершена.

---

## Итерация от 2026-08-02: V3 Integration Plan

**Задача:** Глубокий аудит V2 + план V3-интеграции: KnowledgeManager, KnowledgeRevealTrigger, knowledge-фильтры в SkillTreeWindow/SocialSkillTreeWindow/CharacterWindow.

**Документ:** `07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md`

**Ключевые находки аудита (7 проблем):**
1. 🔴 SkillTreeWindow.ApplyFilterAndSearch — нет knowledge-фильтра (все навыки видны)
2. 🔴 SocialSkillTreeWindow.ApplyFilterAndSearch — та же проблема
3. 🔴 CharacterWindow social-колонка — нет knowledge-фильтра
4. 🔴 Нет KnowledgeRevealTrigger для trigger zone / событий
5. 🟡 Разрозненные API (4 метода в 3 классах) — нужен KnowledgeManager фасад
6. 🟡 KnowledgeUnlockType.Blueprint/NpcTeach/QuestReward — dead code
7. 🟡 RecipeKnowledgeUnlockType — dead code

**План:** 10 шагов, ~7 модифицируемых + 3 новых файла, ~11.5 часов.

**Статус:** План утверждён, ожидает реализацию.

