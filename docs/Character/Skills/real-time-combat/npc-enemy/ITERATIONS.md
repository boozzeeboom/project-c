# Итерации реализации Unified NPC Behavior Architecture

## Итерация от 2026-07-15 — Phase 1: «Живой NPC»

**Задача:** Реализация Phase 1 (P0) согласно `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`:
NPC ходят дозором (Patrol), убегают (Flee), помнят обидчика (Grudge).
Минимальные add-only изменения в NpcBrain.

**Коммит:** `bcc3795` — T-NPC-S01..S06: Phase 1 Unified NPC Behavior Architecture

**Изменения:**
- `NpcBrain.cs` — add-only API: CurrentAggroTarget, ForceChaseTarget, ForceFlee, SocialTick, _socialOverrideLock
- `NpcSocialBrain.cs` — (NEW) companion MonoBehaviour: Patrol/Flee/Grudge/SocialTick dispatch (~400 строк)
- `NpcSpawnerConfig.cs` — add-only: social/personality/idle/flee/alarm/group/memory поля
- `NpcSpawner.cs` — проброс social-конфига при спавне через ApplySpawnerConfig
- `NpcIdleActivity.cs` — (NEW) enum: StandStill/Patrol/LookAround/Socialize/Work/Sit/Sleep/Wander + PatrolPattern
- `GrudgeTable.cs` — (NEW) struct: playerId→timestamp память обидчиков
- `SocialTrigger.cs` — (NEW) enum: 7 триггеров с приоритетами (Phase 2 prep)

**Тикеты:** T-NPC-S01, T-NPC-S02, T-NPC-S03, T-NPC-S04, T-NPC-S05, T-NPC-S06

---

## Итерация от 2026-07-15 — Phase 1 fix v0.3.1: порядок инициализации

**Проблема:** NpcSpawner применял конфиги (в т.ч. AddComponent<NpcSocialBrain>) ПОСЛЕ netObj.Spawn().
NpcBrain.OnNetworkSpawn() искал NpcSocialBrain до его добавления → _socialBrain всегда null → patrol/flee/grudge не работали.

**Коммит:** `0219850` — NpcSpawner применяет конфиги ДО Spawn() чтобы OnNetworkSpawn видел NpcSocialBrain

**Изменения:**
- `NpcSpawner.cs` — TrySpawnAtPoint: visual/behavior/social конфиги перенесены ДО netObj.Spawn()
- `NpcBrain.cs` — Tick(): lazy GetComponent<NpcSocialBrain>() как страховка на edge-case

**Верификация:** SPAWN_TEST — patrol (waypoints), flee (HP<50%), grudge (повторная встреча) работают ✅

---

**Статус Phase 1:** ✅ Полностью завершён и проверен. 2 коммита, 7 новых/изменённых файлов, +800 строк.
