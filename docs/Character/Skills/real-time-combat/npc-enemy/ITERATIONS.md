# Итерации реализации Unified NPC Behavior Architecture

## Итерация от 2026-07-15 — Phase 1: «Живой NPC»

**Задача:** Реализация Phase 1 (P0) согласно `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`:
NPC ходят дозором (Patrol), убегают (Flee), помнят обидчика (Grudge).
Минимальные add-only изменения в NpcBrain.

**Коммит:** `bcc3795` — T-NPC-S01..S06: Phase 1 Unified NPC Behavior Architecture — NpcSocialBrain, Patrol, Flee, Grudge

**Изменения:**
- `NpcBrain.cs` — add-only API: CurrentAggroTarget, ForceChaseTarget, ForceFlee, SocialTick, _socialOverrideLock (+40 строк)
- `NpcSocialBrain.cs` — (NEW) companion MonoBehaviour: Patrol/Flee/Grudge/SocialTick dispatch (~400 строк)
- `NpcSpawnerConfig.cs` — add-only: social/personality/idle/flee/alarm/group/memory поля (+35 строк)
- `NpcSpawner.cs` — проброс social-конфига при спавне через ApplySpawnerConfig (+15 строк)
- `NpcIdleActivity.cs` — (NEW) enum: StandStill/Patrol/LookAround/Socialize/Work/Sit/Sleep/Wander + PatrolPattern
- `GrudgeTable.cs` — (NEW) struct: playerId→timestamp память обидчиков
- `SocialTrigger.cs` — (NEW) enum: 7 триггеров с приоритетами (Phase 2 prep)

**Тикеты:** T-NPC-S01, T-NPC-S02, T-NPC-S03, T-NPC-S04, T-NPC-S05, T-NPC-S06

**Статус:** ✅ Компиляция чистая, Phase 1 завершён
