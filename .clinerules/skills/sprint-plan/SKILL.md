---
name: sprint-plan
description: "Generates a new sprint plan or updates an existing one based on the current milestone, completed work, and available capacity."
argument-hint: "[new|update|status]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Bash
---

When this skill is invoked:

1. **Parse the argument**: `new` for new sprint, `update` to update existing, `status` for quick status

2. **Check current context**:
   - Read milestone file from `docs/` if exists
   - Check recent commits for velocity
   - Review open bugs in `docs/bugs/`

3. **For new sprint**:
   - Review backlog items
   - Estimate effort using `/estimate`
   - Prioritize based on:
     - Dependencies (blockers)
     - User value
     - Technical risk
   
4. **Sprint structure**:
   ```
   ## Sprint [N] — [Date Range]
   
   ### Goal
   [One sentence on sprint objective]
   
   ### Commitment
   - [X] Story points (based on velocity)
   
   ### Stories
   - [STORY-001] [Title] — [Points] — [@owner]
   - [STORY-002] [Title] — [Points] — [@owner]
   
   ### Extra (stretch goals)
   - [STORY-003] [Title] — [Points]
   
   ### Definition of Done
   - [ ] Code reviewed
   - [ ] Tested in editor
   - [ ] No new compiler warnings
   ```

5. **Save to** `docs/sprints/sprint-[N]-[date].md`

6. **Update** sprint index if exists