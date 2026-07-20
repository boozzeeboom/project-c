# Knowledge System — Integration Log

> **Начало:** 2026-07-20
> **Источник:** `02_KNOWLEDGE_SYSTEM_DEEP_ANALYSIS.md` (§5 — Синтез)
> **Финальный план:** 10 шагов

## Status

| # | Step | File(s) | Status |
|---|---|---|---|
| 1 | QuestSaveData — +2 поля (`knownFactions`, `knownNpcs`) | `QuestSaveData.cs` | ✅ |
| 2 | QuestWorld — `_knownFactions`, `_knownNpcs` + 4 метода | `QuestWorld.cs` | ✅ |
| 3 | QuestWorld — `MarkNpcTalked` расширение (unlock faction + NPC knowledge) | `QuestWorld.cs` | ✅ |
| 4 | QuestWorld — `BuildSaveData` / `LoadPlayer` / `Shutdown` интеграция | `QuestWorld.cs` | ✅ |
| 5 | DTO — `ReputationSnapshotDto` (+`knownFactionIds`), `NpcAttitudeSnapshotDto` (+`knownNpcIds`) | `ReputationSnapshotDto.cs` | ✅ |
| 6 | QuestServer — `BuildReputationSnapshot` / `BuildNpcAttitudeSnapshot` (+known arrays) | `QuestServer.cs` | ✅ |
| 7 | Client — `ReputationClientState.KnownFactionIds`, `NpcAttitudeClientState.KnownNpcIds` | `ReputationClientState.cs`, `NpcAttitudeClientState.cs` | ✅ |
| 8 | QuestServer — `BroadcastKnowledgeChange` + wire в `RequestTalkToNpcRpc` | `QuestServer.cs` | ✅ |
| 9 | CharacterWindow — `RefreshReputationCache` / `RefreshNpcAttitudeCache` knowledge-фильтрация | `CharacterWindow.cs` | ✅ |
| 10 | CharacterWindow — Fallback cleanup (FactionFallback только как маппинг имён, не как placeholder) | `CharacterWindow.cs` | ✅ |

## Commits

| Commit | Steps | Hash |
|---|---|---|
| Initial integration | 1-10 | `f0aae06` |

## Architecture Decisions (ADRs)

### ADR-1: Knowledge — server-authoritative
Все изменения knowledge происходят на сервере. Клиент только потребляет `knownFactionIds`/`knownNpcIds` из snapshot.

### ADR-2: Единый канал синхронизации (reputation snapshot)
Knowledge передаётся внутри существующих `ReputationSnapshotDto` / `NpcAttitudeSnapshotDto`.

### ADR-3: `_knownNpcs` — отдельная структура
Не реюз `_npcTalkedTo`. Семантическая разница: talked = технический, known = семантический.

### ADR-4: All-entries + фильтр
Сервер отправляет все entries + known-массив, клиент фильтрует сам.

## Что НЕ Покрывает v1
- Knowledge для локаций / предметов / рецептов
- Quest-gated knowledge
- Knowledge decay
- Admin tools
- Батч-миграция существующих персонажей
