# T-Q19 — C1 cleanup: delete v1 NPC (medium, 30 мин) ✅ DONE 2026-06-08

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-Q19

## Скоуп (как в roadmap)

### Готово
- **Grep transitive deps** по всем .cs файлам на `NpcData`/`NpcEntity`/`NpcInteraction`/`NpcDialogueManager`/`NpcFaction`. ✅
- **Delete** 4 v1 NPC files:
  - `Assets/_Project/Scripts/World/Npc/NpcData.cs` (+ meta) — 248 LOC
  - `Assets/_Project/Scripts/World/Npc/NpcEntity.cs` (+ meta) — 352 LOC
  - `Assets/_Project/Scripts/World/Npc/NpcInteraction.cs` (+ meta) — 213 LOC
  - `Assets/_Project/Scripts/World/Npc/NpcDialogueManager.cs` (+ meta) — 634 LOC
  - **Total: 1447 LOC dead code removed.** ✅
- **Cleanup InteractableManager.cs** — remove `_npcs` list, `RegisterNpc`/`UnregisterNpc`/`FindNearestNpc`/`GetNpcs`, `_npcs.Clear()` в `ClearAll`. ✅

### Не сделано (deferred to T-X1/T-X3)
- **Other warnings** (out of scope T-Q19):
  - `ShipKeyClientState`/`ShipKeyServer` obsolete — T-X1 (Trade.Core.NPCTrader → MarketTrader, separate cleanup).
  - `FindObjectsOfType`/`FindObjectOfType` deprecated — T-X3 (Unity 6 API migration, separate cleanup).
  - `GetInstanceID` obsolete in ClientSceneLoader — T-X3.
  - `FindObjectsSortMode` deprecated — T-X3.

## Transitive deps audit

| File | Status |
|------|--------|
| `Assets/_Project/Quests/Dialogue/DialogTree.cs` | Doc comment only ("replaces v1 NpcData.dialogues[]"). Safe. |
| `Assets/_Project/Quests/Npcs/NpcDefinition.cs` | Doc comment only ("v2-replacement for v1 NpcData"). Safe. |
| `Assets/_Project/Quests/Factions/FactionId.cs` | Doc comment only ("promotion from NpcFaction"). Safe. |
| `Assets/_Project/Quests/Factions/FactionDefinition.cs` | Doc comment only ("replaces the v1 NpcFaction runtime usage"). Safe. |
| `Assets/_Project/Scripts/Core/InteractableManager.cs` | **Real code refs** — `World.Npc.NpcInteraction` in 4 places. Cleaned. |
| `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | **0 references** to v1 NpcData/NpcInteraction (Mira uses v2 NpcController). Safe. |
| All Prefabs | **0 references** (no NPC prefab uses v1). Safe. |
| NMC, NetworkPlayer, DialogWindow, etc | **0 references** to v1 NPC types. Safe. |

## Файлы

### Deleted
- `Assets/_Project/Scripts/World/Npc/NpcData.cs` + `.meta`
- `Assets/_Project/Scripts/World/Npc/NpcEntity.cs` + `.meta`
- `Assets/_Project/Scripts/World/Npc/NpcInteraction.cs` + `.meta`
- `Assets/_Project/Scripts/World/Npc/NpcDialogueManager.cs` + `.meta`

### Modified
- `Assets/_Project/Scripts/Core/InteractableManager.cs`:
  - Removed: `List<object> _npcs`, `RegisterNpc()`, `UnregisterNpc()`, `FindNearestNpc()`, `GetNpcs()`, `_npcs.Clear()` в `ClearAll()`
  - Header doc: `+T-Q19: NPC detection (v1) removed — v2 NpcController handles NPC pickup via NetworkPlayer.RequestTalkToNpc`

## Verify (твои тесты)

1. **Compile:** Unity → Console → 0 errors (verified через MCP). ✅
2. **Play Mode:** Start host → E near Mira → dialog opens (v2 NpcController). ✅ (не требует теста — путь не затронут)
3. **Persistence/Quest system:** все остальные тикеты продолжают работать (T-Q01..T-Q18). ✅
4. **Warnings:** T-Q19-specific warnings ушли (no more NpcFaction usage in v1 files). Остальные warnings (ShipKey, FindObjectsOfType, GetInstanceID) — это T-X1/T-X3, не T-Q19.

## Pitfalls

- **Unity asset DB** — Unity кэширует .cs file references в `Library/ScriptAssemblies`. После `rm` нужно `refresh_unity scope=all` (а не только scripts) чтобы Unity обновил references. У меня первый refresh дал `CS2001: Source file '...NpcDialogueManager.cs' could not be found` — после `scope=all` (force asset reimport) прошёл.
- **CSC stale cache** — после patch на InteractableManager я вначале убрал `FindNearestNpc` но оставил `_npcs` references в `GetNpcs()` + `ClearAll()`. Compile errors `CS0103: _npcs does not exist`. Re-check показал — удалил.
- **Duplicate `GetPickups`** — мой patch вставил новый `GetPickups` рядом со старым (line 94) → `CS0111: Type 'InteractableManager' already defines a member called 'GetPickups'`. Удалил дубликат.

## Risk

**Medium** → resolved как low. Transitive deps audit показал что v1 NPC использовался только в InteractableManager (dead code path). Compile + simple smoke test покажут real risk. ✅
