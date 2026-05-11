# Session Startup Brief — Cloud Generator v8.x Unity Integration

## READ FIRST

Before doing anything else, read these files:
1. `docs/CLOUD_VISUALISER/SESSION_STARTUP_BRIEF.md` — this document
2. `docs/CLOUD_VISUALISER/FIXES.md` — full analysis (last section)

---

## Current State

**What works:**
- Export Full Unity C# in visualizer (button → ZIP with 4 files)
- CloudMath.cs, CloudTypes.cs, CloudGenerator.cs, CloudGeneratorWindow.cs all work
- Sphere, Column, Platform, Tree archetypes all generate correctly

**What is broken:**
- `sphereCountScale` slider (1%-400%) — works in JS, ignored in generated C# code

**Main feature to add:**
- Parent Mesh System — generate spheres ON THE SURFACE of a Unity Mesh asset

---

## Task 1: Fix sphereCountScale Bug (START HERE)

This is the smallest change (~10 lines). Do this first.

**Subagent prompt:** `docs/CLOUD_VISUALISER/INTEGRATION/SUBAGENT_FIX_SPHERE_COUNT.md`

**What to do:**
1. Find `cloudGeneratorHeader` template in `visualizer3d.html` (search for it)
2. Find line `int maxSphereCount = layer.MaxSphereCount;`
3. Replace with scale calculation using `SphereCountScale`
4. Add `SphereCountScale` field to `CloudLayerConfig` in `cloudTypesHeader` template

---

## Task 2: Implement Parent Mesh System (MAIN WORK)

This is the main feature. Read the subagent prompt first.

**Subagent prompt:** `docs/CLOUD_PROJECTS/ProjectC_client/docs/CLOUD_VISUALISER/INTEGRATION/SUBAGENT_PARENT_MESH.md`

**Steps:**
1. Create `CloudParentMesh.cs` — surface sampling algorithm
2. Update `CloudTypes.cs` — add ParentMeshPath + transform fields
3. Update `CloudGenerator.cs` — use mesh points in generation
4. Update `CloudGeneratorWindow.cs` — UI for mesh selection + transform sliders

**Key algorithms (mirror from JS visualizer):**
- `sampleMeshSurface()` — Monte Carlo weighted triangle sampling (lines 4153-4218)
- `applyParentMeshTransform()` — scale → rotateZ → rotateY → rotateX → offset (lines 4252-4282)

---

## What NOT to Implement

These JS features are NOT needed in Unity:
- Remesh (filter internal spheres)
- Merge layers (freeze geometry)
- Unmerge
- Layer clone/copy
- Drag & drop reorder
- Undo/redo (Unity has native)

---

## File Locations

| File | Location |
|------|----------|
| Visualizer | `docs/CLOUD_VISUALISER/web/visualizer3d.html` |
| Analysis | `docs/CLOUD_VISUALISER/FIXES.md` |
| Export docs | `docs/CLOUD_VISUALISER/EXPORT_SYSTEM.md` |
| Startup brief | `docs/CLOUD_VISUALISER/SESSION_STARTUP_BRIEF.md` |
| Subagent: sphereCountScale | `docs/CLOUD_VISUALISER/INTEGRATION/SUBAGENT_FIX_SPHERE_COUNT.md` |
| Subagent: Parent Mesh | `docs/CLOUD_VISUALISER/INTEGRATION/SUBAGENT_PARENT_MESH.md` |

---

## Quick Verification

After changes, test:

1. **sphereCountScale:** Set to 50%, generate → should get ~2500 spheres from 5000 base
2. **Parent Mesh:** Select any Mesh asset, set scale/rotation/offset → spheres should appear on mesh surface
3. **Config save/load:** Save JSON with parent mesh path, load back → mesh should restore

---

## Commit Message Template (when done)

```
feat: add sphereCountScale and Parent Mesh System to Unity C# export

- Fix sphereCountScale: now applied in CloudGenerator.cs template
- Add CloudParentMesh.cs: Monte Carlo surface sampling from Unity Mesh
- Add ParentMeshPath + transform fields to CloudLayerConfig
- Add ParentMesh UI in CloudGeneratorWindow.cs (ObjectField + sliders)

BREAKING: CloudLayerConfig has new fields (provide defaults for old configs)
```