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
