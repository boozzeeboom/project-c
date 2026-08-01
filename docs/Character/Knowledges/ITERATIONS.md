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
