# Cloud Generator v8.x — Unity Integration Brief

## Context

This document is the **single source of truth** for understanding what needs to be done to bring Cloud Generator features from the web visualizer to Unity C#.

---

## What Already Works

### Export Full Unity C#

Clicking "Export Full Unity C#" in `visualizer3d.html` generates a ZIP with 4 files:
- `CloudMath.cs` — Perlin, FBM, Worley noise + Fibonacci sphere (✅ Works)
- `CloudTypes.cs` — CloudArchetype enum, SizeRange, ColumnParams, PlatformParams, TreeParams, CloudLayerConfig, CloudSphere (✅ Works)
- `CloudGenerator.cs` — Generate() + 4 archetype generators (⚠️ Has 1 bug: sphereCountScale ignored)
- `CloudGeneratorWindow.cs` — Unity Editor window with layer editor, sliders, load/save (✅ Works)

### History
- Commit `7ebfe2b5` (v6.1) — initial Unity C# export
- Current visualizer is v8.1

---

## What Needs to Be Added

### 1. sphereCountScale — BUG TO FIX

**Problem:** The slider in the visualizer (1%-400%) works in JS, but the generated C# code ignores `SphereCountScale`.

**JS code** (`generateCloud()`, line ~3260):
```javascript
const sphereCountScale = layer.sphereCountScale !== undefined ? layer.sphereCountScale : 1.0;
const maxSphereCount = Math.floor((layer.maxSphereCount || 5000) * sphereCountScale);
```
✅ Correctly applied.

**C# template** (`CloudGenerator.cs`, line ~1260):
```csharp
int maxSphereCount = layer.MaxSphereCount;
```
❌ `SphereCountScale` is NOT used.

**Fix needed:**
1. Add `SphereCountScale` field to `CloudLayerConfig` in `CloudTypes.cs`
2. Update `CloudGenerator.cs` template to apply the scale:
```csharp
float sphereCountScale = layer.SphereCountScale > 0 ? layer.SphereCountScale : 1.0f;
int maxSphereCount = (int)(layer.MaxSphereCount * sphereCountScale);
```
3. Add to EXPORT_SCHEMA in `visualizer3d.html` (already added in v8.1)

---

### 2. Parent Mesh System — MAIN FEATURE

**What it does:** Generate spheres ON THE SURFACE of a Unity Mesh asset (not on an ellipsoid).

**Visualizer behavior (JS):**
```
1. User loads OBJ file → loadParentMesh() parses OBJ → sampleMeshSurface() gets 2000 points
2. Points stored in _parentMeshPointsRaw (original) and _parentMeshPoints (transformed)
3. generateSphereLayer(layer, parentSpheres) uses mesh points instead of Fibonacci sphere
4. Transform: offset/scale/rotation applied to points and visual mesh
```

**For Unity:**
- No OBJ loading needed — use Mesh from Unity assets via `AssetDatabase.LoadAssetAtPath()`
- `CloudParentMesh.cs` class with `SampleSurface(Mesh mesh, int pointCount)` — Monte Carlo sampling
- New fields in `CloudLayerConfig`: `ParentMesh`, `ParentMeshScaleX/Y/Z`, `ParentMeshRotX/Y/Z`, `ParentMeshOffsetX/Y/Z`
- In `CloudGenerator.GenerateSphereLayer()`: if `ParentMesh != null`, use sampled mesh points instead of Fibonacci

**Why this is the main feature:** It allows projecting cloud spheres onto ANY 3D surface — skull, rock, terrain, anything with a Mesh.

---

## What NOT to Implement

The following JS features are **NOT needed** in Unity:
- **Remesh** — filter internal spheres (visual only)
- **Merge layers** — freeze multiple layers into one geometry
- **Unmerge** — restore after merge
- **Layer clone/copy** — basic Unity editor has this
- **Drag & drop reorder** — basic Unity editor has this
- **Undo/redo** — Unity has native

---

## File Structure for Implementation

```
Assets/
├── CloudGenerator/
│   ├── Math/
│   │   └── CloudMath.cs          ← already works, no changes
│   ├── Types/
│   │   └── CloudTypes.cs         ← ADD: SphereCountScale, ParentMesh fields
│   ├── Generation/
│   │   ├── CloudGenerator.cs     ← FIX: sphereCountScale, ADD: parent mesh support
│   │   └── CloudParentMesh.cs    ← NEW: surface sampling from Unity Mesh
│   └── Editor/
│       └── CloudGeneratorWindow.cs ← ADD: ParentMesh ObjectField, transform sliders
```

---

## Implementation Order

### Step 1: Fix sphereCountScale bug (30 min)

**Files to modify:**
1. `CloudTypes.cs` — add `SphereCountScale` field (float, default 1.0)
2. `CloudGenerator.cs` template in `visualizer3d.html` — apply scale when calculating maxSphereCount
3. `EXPORT_SCHEMA` already has `sphereCountScale` (added in v8.1)

**Verification:** Export with sphereCountScale=0.5, verify sphere count is ~2500 not 5000.

---

### Step 2: Create CloudParentMesh.cs (2-3 hours)

**Location:** `Assets/CloudGenerator/Generation/CloudParentMesh.cs`

**Core algorithm — Monte Carlo Surface Sampling:**
```csharp
public static List<Vector3> SampleSurface(Mesh mesh, int pointCount = 2000) {
    // 1. Extract triangles: mesh.triples gives indices, mesh.vertices gives positions
    // 2. Calculate triangle areas, build cumulative area array
    // 3. For each point:
    //    a. Random r in [0, totalArea) → binary search for triangle
    //    b. Random u,v in [0,1) → barycentric: p0 + u*(p1-p0) + v*(p2-p0)
    //    c. Use sqrt(u) for uniform distribution within triangle
    // 4. Return list of Vector3 points
}

public static List<Vector3> ApplyTransform(
    List<Vector3> points,
    Vector3 scale,
    Vector3 rotationDegrees,
    Vector3 offset
) {
    // 1. Scale: multiply each component
    // 2. Rotation: apply Euler angles in ZYX order (radians)
    // 3. Offset: add to position
}
```

**Key points:**
- Use `mesh.triangles` for index array, `mesh.vertices` for positions
- Weighted random selection: larger triangles get more sample points
- Barycentric with `sqrt(random)` for uniform surface distribution
- Rotation order: Z → Y → X (matches JS implementation)

---

### Step 3: Update CloudTypes.cs (30 min)

**Add to CloudLayerConfig class:**
```csharp
// Parent Mesh support
public Mesh ParentMesh;              // Reference to Unity Mesh asset
public float ParentMeshScaleX = 1f;
public float ParentMeshScaleY = 1f;
public float ParentMeshScaleZ = 1f;
public float ParentMeshRotX = 0f;
public float ParentMeshRotY = 0f;
public float ParentMeshRotZ = 0f;
public float ParentMeshOffsetX = 0f;
public float ParentMeshOffsetY = 0f;
public float ParentMeshOffsetZ = 0f;

// Sphere count scale (default 1.0)
public float SphereCountScale = 1f;
```

**Note:** `Mesh` is not serializable by Unity's JsonUtility. Use `string` (asset path) and resolve at runtime via `AssetDatabase.LoadAssetAtPath()`.

---

### Step 4: Update CloudGenerator.cs (1 hour)

**In GenerateSphereLayer():**
```csharp
// At the start of GenerateSphereLayer, after parent sphere setup:
if (layer.ParentMesh != null) {
    // Sample mesh surface
    var meshPoints = CloudParentMesh.SampleSurface(layer.ParentMesh, 2000);
    meshPoints = CloudParentMesh.ApplyTransform(meshPoints,
        new Vector3(layer.ParentMeshScaleX, layer.ParentMeshScaleY, layer.ParentMeshScaleZ),
        new Vector3(layer.ParentMeshRotX, layer.ParentMeshRotY, layer.ParentMeshRotZ),
        new Vector3(layer.ParentMeshOffsetX, layer.ParentMeshOffsetY, layer.ParentMeshOffsetZ));

    // Generate on each mesh point instead of Fibonacci sphere
    // For each mesh point, treat it like a parent sphere:
    // - Generate actualBumps on its surface
    // - Process with noise/density/clustering
    // - Continue cascade normally
}
```

**Apply sphereCountScale** at maxSphereCount calculation:
```csharp
float sphereCountScale = layer.SphereCountScale > 0 ? layer.SphereCountScale : 1.0f;
int maxSphereCount = (int)(layer.MaxSphereCount * sphereCountScale);
```

---

### Step 5: Update CloudGeneratorWindow.cs (1-2 hours)

**In layer editor UI, add:**
1. **ObjectField** for `ParentMesh` — select Mesh from assets
2. **Transform section** (shown only when ParentMesh is set):
   - Scale X/Y/Z sliders (0.1 to 3.0)
   - Rotation X/Y/Z sliders (0 to 360)
   - Offset X/Y/Z sliders (-200 to 200)
3. **Sphere Count Scale** slider (1% to 400%) — already in visualizer, add to Unity too

**For loading/saving config:**
- Serialize `ParentMesh` as asset path string
- On load: resolve path to Mesh via `AssetDatabase.LoadAssetAtPath()`

---

## Code Reference

### JS implementations to mirror:

**Surface sampling** (`visualizer3d.html`, lines 4153-4218):
- `sampleMeshSurface(vertices, indices, numPoints)` — Monte Carlo with weighted triangle selection

**Transform** (`visualizer3d.html`, lines 4252-4282):
- `applyParentMeshTransform(point)` — scale → rotateZ → rotateY → rotateX → offset

**Parent mesh integration** (`visualizer3d.html`, lines 3272-3333):
- `generateSphereLayer(layer, parentSpheres)` — when parentSpheres provided, iterates each as parent

---

## Validation Checklist

After implementing:

- [ ] sphereCountScale slider in Unity produces correct sphere count (test: 50% → ~2500 spheres from 5000)
- [ ] ParentMesh ObjectField allows selecting any Mesh asset
- [ ] Transform sliders (scale/rotation/offset) visually affect generation
- [ ] Cloud generated on mesh surface matches expected distribution
- [ ] Config save/load works with ParentMesh serialized as path
- [ ] No breaking changes to existing layer generation (test sphere/column/platform/tree archetypes)

---

## Contact / Context

- **Project:** ProjectC_client
- **Visualizer:** `docs/CLOUD_VISUALISER/web/visualizer3d.html`
- **Documentation:** `docs/CLOUD_VISUALISER/FIXES.md`, `docs/CLOUD_VISUALISER/EXPORT_SYSTEM.md`
- **Unity version:** 6000.4.1f1 with URP
- **Last working Unity export:** commit 7ebfe2b5 (v6.1)

---

## Quick Start for New Session

When starting a new conversation, read this file first. The complete context is:

1. **What works:** Export Full Unity C# with 4 files — CloudMath, CloudTypes, CloudGenerator, CloudGeneratorWindow
2. **Bug:** sphereCountScale ignored in C# template
3. **Main feature:** Parent Mesh System — generate spheres on any Unity Mesh surface
4. **Not needed:** Remesh, Merge layers, Undo/Redo (Unity has native)

Start implementation with Step 1 (fix sphereCountScale bug) as it's the smallest change.