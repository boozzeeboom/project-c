# Cloud Generator v8.x — Unity Integration Start Prompt

## DO THIS IN ORDER

### Step 1: Read Context Files

Read these files in order, one at a time:

1. `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\INTEGRATION\README.md`
   - This is the entry point. Read it first.

2. `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\INTEGRATION\SESSION_STARTUP_BRIEF.md`
   - Full context: what works, what's broken, what needs to be done.

3. `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\FIXES.md` (last 300 lines)
   - Detailed analysis of features, bug descriptions, implementation notes.

---

### Step 2: Do Task 1 (sphereCountScale bug fix)

**Read:** `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\INTEGRATION\SUBAGENT_FIX_SPHERE_COUNT.md`

Then modify `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\web\visualizer3d.html`:

1. Find `cloudTypesHeader` template (search for it in the file). Add `SphereCountScale` field to `CloudLayerConfig` class.

2. Find `cloudGeneratorHeader` template (search for it). Find line with `int maxSphereCount = layer.MaxSphereCount;` and replace with scale calculation.

---

### Step 3: Do Task 2 (Parent Mesh System)

**Read:** `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\INTEGRATION\SUBAGENT_PARENT_MESH.md`

This task requires creating a new file AND modifying templates in visualizer3d.html.

**New file to create:**
`C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\INTEGRATION\CloudParentMesh.cs`
- This is the C# code for Unity. Copy the code from the subagent prompt exactly.

**Templates to modify in visualizer3d.html:**

1. `cloudTypesHeader` — add ParentMeshPath and transform fields to CloudLayerConfig

2. `cloudGeneratorHeader` — modify GenerateSphereLayer() to use mesh points when ParentMeshPath is set

3. `cloudGeneratorWindow` — add UI section for parent mesh selection

---

## What NOT to implement

- Remesh
- Merge layers
- Undo/redo
- Layer clone/copy
- Drag & drop reorder

---

## Key Reference Locations

- **Visualizer file:** `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\web\visualizer3d.html`
- **CloudGenerator.cs template:** search for `cloudGeneratorHeader` in the visualizer
- **CloudTypes.cs template:** search for `cloudTypesHeader` in the visualizer

---

## Algorithm Reference (from JS visualizer)

**Surface sampling** (lines 4153-4218 in visualizer3d.html):
```
1. Extract triangles from mesh.vertices + mesh.triangles
2. Calculate area for each triangle (cross product / 2)
3. Build cumulative area array
4. For each point: binary search for triangle, then barycentric with sqrt(random) for uniform distribution
```

**Transform** (lines 4252-4282 in visualizer3d.html):
```
Order: scale → rotateZ → rotateY → rotateX → offset
Rotation in degrees, converted to radians. ZYX order.
```

---

## After All Changes

1. Verify EXPORT_SCHEMA includes `sphereCountScale` and `ParentMesh*` fields
2. Test by generating the ZIP and checking CloudGenerator.cs has sphereCountScale logic
3. Confirm CloudParentMesh.cs has correct algorithm (Monte Carlo, weighted by area)

---

## Commit When Done

```
feat: add sphereCountScale and Parent Mesh System to Unity C# export

- Fix sphereCountScale: now applied in CloudGenerator.cs template
- Add CloudParentMesh.cs: Monte Carlo surface sampling from Unity Mesh
- Add ParentMeshPath + transform fields to CloudLayerConfig
- Update CloudGeneratorWindow.cs: ParentMesh UI (ObjectField + sliders)

BREAKING: CloudLayerConfig has new fields
```

---

## If Stuck

Read the subagent prompt files again. They have detailed step-by-step instructions with exact line numbers and code snippets.