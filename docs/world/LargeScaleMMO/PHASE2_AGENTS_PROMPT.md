# .agents Prompt: World Streaming Phase 2 Orchestration

## Context
Разрабатывается Large-Scale MMO с миром 100,000 x 100,000 units. Система World Streaming реализована, но есть незавершённые задачи.

### Current Session (14.04.2026) - Completed
- ✅ WorldEditorTools.cs (StreamingSetup, ChunkVisualizer)
- ✅ StreamingTestAutoRunner.cs
- ✅ StreamingTest_AutoRun.cs
- ✅ FloatingOriginMP HUD improvements

### Known Issues to Fix
1. **HUD остаётся слева** - другой компонент рисует свой HUD раньше FloatingOriginMP
2. **Телепорты не работают** - `TeleportToPeak()` в WorldStreamingManager не работает
3. **F7 показывает кубы над головой** - OnDrawGizmos рисует wireframe чанк

---

## Tasks for Next Agent

### Priority 1: Fix HUD Position Issue
**Problem:** FloatingOriginMP HUD рисуется слева, несмотря на изменение Rect
**Root Cause:** Другой компонент рисует свой HUD в (10, 10) раньше

**Steps:**
1. Найти все компоненты с `OnGUI()` в проекте
2. Определить какой рисует HUD слева
3. Исправить порядок или координаты
4. Проверить в Play Mode

### Priority 2: Fix Teleportation
**Problem:** F5/F6 не телепортируют камеру
**Root Cause:** `TeleportToPeak()` не реализован или не связан

**Files to check:**
- `WorldStreamingManager.cs` - найти `TeleportToPeak()`
- `StreamingTest_AutoRun.cs` - проверить вызов

**Steps:**
1. Найти реализацию `TeleportToPeak()`
2. Добавить логирование для отладки
3. Проверить что компоненты связаны
4. Протестировать в Play Mode

### Priority 3: Investigate F7 Chunk Loading
**Status:** F7 работает - загружает 16 чанков ✅
**Optional:** Оптимизировать отображение загруженных чанков

---

## Agent Team Structure

```
Orchestrator (this prompt)
├── Agent: UI-Debug (Priority 1)
├── Agent: Network-Debug (Priority 2)  
└── Agent: Testing-Lead (Verification)
```

---

## Execution Plan

### Agent 1: UI-Debug
**Task:** Fix FloatingOriginMP HUD position

**Instructions:**
```
1. Search for all OnGUI() implementations in project
2. Find which component draws at Rect(10, 10)
3. Fix by either:
   - Moving FloatingOriginMP.OnGUI() to use later coordinates
   - Or finding and fixing the other HUD component
4. Test in Play Mode with F10 to toggle HUD
```

### Agent 2: Network-Debug
**Task:** Fix TeleportToPeak implementation

**Instructions:**
```
1. Read WorldStreamingManager.cs
2. Find or implement TeleportToPeak(Vector3 position)
3. Add Debug.Log statements
4. Ensure FloatingOriginMP.ResetOrigin() called before teleport
5. Test with F5/F6 in Play Mode
```

### Agent 3: Testing-Lead
**Task:** Verify all fixes and document

**Instructions:**
```
1. Run Play Mode tests
2. Check Console for errors
3. Verify:
   - HUD is on RIGHT side
   - F5/F6 teleports work
   - F7 loads chunks
4. Update SESSION_2026-04-14.md with results
5. Create commit if all works
```

---

## Files Reference

### Key Files
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` - HUD
- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs` - TeleportToPeak
- `Assets/_Project/Scripts/World/Streaming/StreamingTest_AutoRun.cs` - Test controls
- `docs/world/LargeScaleMMO/SESSION_2026-04-14.md` - Session report

### Recent Commits
- `3e9db34` - docs: session report
- `0817a64` - feat: World Streaming Editor Tools

---

## Success Criteria

| Task | Success Condition |
|------|------------------|
| HUD Fix | HUD visible on RIGHT side of screen |
| Teleport Fix | F5/F6 moves camera to test points |
| Chunk Load | F7 loads chunks, counter shows > 0 |

---

## Notes
- Камера находится на префабе ThirdPersonCamera, не на MainCamera
- StreamingTest_AutoRun добавляется автоматически через InitializeOnLoad
- FloatingOriginMP теперь на WorldStreaming root, ищет камеру в runtime
