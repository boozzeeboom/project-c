# T-Q22: QuestTracker — all objectives HUD

> Ticket: T-Q22  
> Date: 2026-08-26  
> Status: DONE (compile clean, 0 errors)

## Problem

`QuestTracker` (top-right HUD) shows only the **first** non-completed objective, even when the tracked quest has multiple objectives. The user needs to see all objectives at a glance.

## Root cause

`QuestTracker.RefreshDisplay()` calls `BuildObjectiveText()` which iterates `tracked.objectives`, finds the first `!o.completed` entry, and formats it as a single string into one `Label`. All other objectives are invisible.

## Design

Mirror the `CharacterWindow` quest-row pattern (T-Q21) where each objective gets its own `Label` with a `☐`/`☑` bullet and optional `(current/required)` counter.

### Files changed

| File | Change |
|---|---|
| `QuestTracker.uxml` | Replace `<Label name="quest-objective">` with `<VisualElement name="quest-objectives-container">` |
| `QuestTracker.uss` | Add `.quest-tracker-objectives-container` (flex column) and `.quest-tracker-objective-line` / `-done` styles |
| `QuestTracker.cs` | `_objectiveLabel` → `_objectivesContainer`; `RefreshDisplay` clears container and adds one Label per objective; `BuildObjectiveText` removed |

### UI layout (after)

```
┌──────────────────────────────┐
│ Quest Name                   │
│  ☐ Objective A (2/5)        │
│  ☑ Objective B              │
│  ☐ Objective C              │
│ [Скрыть]                    │
└──────────────────────────────┘
```

### Format rules (same as CharacterWindow)

- Bullet: `☐` (not completed) / `☑` (completed)
- Counter: shown only when `requiredQuantity > 1` → `(current/required)`
- Completed lines get class `quest-tracker-objective-line-done` (green)

## Verification

1. Open Unity → Console: 0 errors after import.
2. In Play Mode: get a quest with ≥ 2 objectives → open Character Window → Quests tab → click "Следить".
3. HUD top-right should show ALL objectives, each on its own line, with ☐/☑ and counters.
4. Complete one objective → HUD updates (☐ → ☑, counter increments).
5. Complete all → all lines ☑.
6. Click "Скрыть" → HUD hides.
