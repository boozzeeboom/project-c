# Cloud Generator Export System v7.0

## Overview

The export system allows users to extract either:
1. **Full Generator** — Complete math library + types + generator implementation for Unity/C#/other projects
2. **Config Only** — Just the parameters, for use with an already-connected generator
3. **Mesh Export** — Export geometry as OBJ or C# mesh data (supports remeshed output)
4. **Unity Mesh Merge** (roadmap) — Consolidate spheres into single mesh in Unity

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                   EXPORT SYSTEM v6.0                         │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────┐      ┌─────────────────┐      ┌──────────┐  │
│  │   Layer     │      │  EXPORT_SCHEMA  │      │ TEMPLATES│  │
│  │   Data      │─────▶│  (field map)    │─────▶│  (code   │  │
│  │ (JSON)      │      │                 │      │  output) │  │
│  └─────────────┘      └─────────────────┘      └──────────┘  │
│         │                                             │        │
│         │              ┌─────────────────┐            │        │
│         └─────────────▶│genericExport() │◀───────────┘        │
│                        │   (unified)    │                     │
│                        └─────────────────┘                     │
└──────────────────────────────────────────────────────────────┘
```

### Key Concepts

- **EXPORT_SCHEMA** — Single source of truth defining all layer fields
- **TEMPLATES** — Code generation templates per output format
- **genericExport()** — Unified function that reads schema and applies template

---

## EXPORT_SCHEMA Structure

```javascript
const EXPORT_SCHEMA = {
  // Fields shared by all archetypes
  common: ['enabled', 'yOffset', 'seed', 'density', 'jitter', ...],

  // Fields for sphere archetype
  sphere: ['cloudSize', 'cascadeDepth', 'bumpsPerLevel', ...],

  // Nested params for column (inside columnParams object)
  column: { section: 'columnParams', fields: ['height', 'baseRadius', ...] },

  // Nested params for platform
  platform: { section: 'platformParams', fields: ['width', 'depth', ...] },

  // Nested params for tree
  tree: { section: 'treeParams', fields: ['maxDepth', 'branchElongation', ...] }
};
```

### FIELD_TYPES Mapping

```javascript
const FIELD_TYPES = {
  'seed': 'int',
  'density': 'float',
  'enabled': 'bool',
  // ...
};
```

---

## Adding a New Parameter

### Step 1: Add to EXPORT_SCHEMA

Find the appropriate archetype section and add the field name:

```javascript
// To add 'newParam' to sphere archetype:
sphere: ['cloudSize', 'cascadeDepth', 'bumpsPerLevel', 'childRatio',
         'sizeVariation', 'parentCount', 'ellipsoidY', 'ellipsoidXZ',
         'maxSphereCount', 'newParam']  // ← added
```

### Step 2: Add to FIELD_TYPES

```javascript
const FIELD_TYPES = {
  // ... existing fields ...
  'newParam': 'float'  // ← added (or 'int', 'bool')
};
```

### Step 3: Ensure Default in getDefaultLayer()

Check `getDefaultLayer()` (around line 1602) sets the default value:

```javascript
// In getDefaultLayer('sphere'):
base.newParam = 0.5;  // ← default value
```

### Step 4: Add UI Control (if needed)

Find the archetype's UI rendering section in `renderLayerList()` and add the slider.

---

## Adding a New Archetype

### Step 1: Add to EXPORT_SCHEMA

```javascript
const EXPORT_SCHEMA = {
  common: [...],
  sphere: [...],
  // ... existing archetypes ...
  mynewarchetype: {
    section: 'myNewArchetypeParams',  // nested object name
    fields: ['param1', 'param2', ...]
  }
};
```

### Step 2: Add to FIELD_TYPES

```javascript
const FIELD_TYPES = {
  // ... existing fields ...
  'myNewArchetypeParams.param1': 'float',
  'myNewArchetypeParams.param2': 'int'
};
```

### Step 3: Add to UI Dropdown

In `renderLayerList()` around line 1937:
```html
<select class="layer-archetype-select" onchange="updateLayerArchetype(${index}, this.value)">
  <option value="sphere" ...>Sphere</option>
  <option value="column" ...>Column</option>
  <option value="platform" ...>Platform</option>
  <option value="tree" ...>Tree</option>
  <option value="mynewarchetype" ...>My New Archetype</option>  <!-- ← added -->
</select>
```

### Step 4: Add Generator Function

Create `generateMyNewArchetypeLayer()` function following the pattern of existing generators.

### Step 5: Register in Dispatcher

In `getArchetypeGenerator()` around line 1128:
```javascript
const getArchetypeGenerator = (archetype) => {
  switch (archetype) {
    case 'sphere': return generateSphereLayer;
    case 'column': return generateColumnLayer;
    case 'platform': return generatePlatformLayer;
    case 'tree': return generateTreeLayer;
    case 'mynewarchetype': return generateMyNewArchetypeLayer;  // ← added
    default: return generateSphereLayer;
  }
};
```

---

## Adding a New Export Format

### Step 1: Add Template to TEMPLATES

```javascript
const TEMPLATES = {
  csharp: { /* existing */ },
  json: { /* existing */ },
  // Add new format:
  myformat: {
    // Template string or function
    exportConfig: (layers) => { /* return code string */ },
    importHint: '// How to use this format'
  }
};
```

### Step 2: Update genericExport()

```javascript
function genericExport(layers, format, namespace) {
  if (format === 'json') { /* existing */ }
  if (format === 'csharp-config') { /* existing */ }
  // Add new format:
  if (format === 'myformat') {
    return TEMPLATES.myformat.exportConfig(layers);
  }
  return '// Unsupported format: ' + format;
}
```

### Step 3: Add Button in Dialog

In `showExportDialog()` HTML, add a button:
```html
<button id="btn-myformat" ...>My Format</button>
```

And the handler:
```javascript
document.getElementById('btn-myformat').onclick = () => {
  const ns = document.getElementById('export-namespace').value.trim() || 'ProjectC';
  exportMyFormat(layers, ns);
};
```

---

## Export Version History

| Version | Date | Changes |
|---------|------|---------|
| 7.0 | 2026-05-10 | Remesh feature, merged geometry export |
| 6.0 | 2026-05-09 | Initial data-driven export system with schema, Editor integration |
| 5.x | prior | Hardcoded C# export |

---

## Unity Integration

### Full Generator ZIP Structure

When exporting **Full Generator**, the ZIP contains:

```
CloudGenerator_v6.0/
├── CloudMath.cs              ← Math library (noise, fibonacci, etc.)
├── CloudTypes.cs             ← Enums, configs, CloudSphere class
├── CloudGenerator.cs        ← Generator implementation
└── Editor/
    └── CloudGeneratorWindow.cs  ← Unity Editor window (Tools menu)
```

### Using in Unity

1. Extract ZIP into your Unity project's `Assets` folder
2. Unity compiles automatically
3. Menu: **Tools → Cloud Generator** opens the editor window
4. Click **Generate** to create spheres via `CloudGenerator.Generate(layers)`
5. Results logged to Console: `Generated N spheres`

### Editor Window Features

- Layer list editor (add, duplicate, delete)
- Per-archetype parameter sliders:
  - **Sphere**: CloudSize, CascadeDepth, BumpsPerLevel, ChildRatio, etc.
  - **Column**: Height, BaseRadius, TopRadius, Floors, RingsPerFloor
  - **Platform**: Width, Depth, CenterThickness, InteriorDensity
  - **Tree**: MaxDepth, BranchElongation, BranchAngle, BranchProbability
- Common settings: Seed, Density, Jitter, Clustering, PositionVariation
- **Load Config / Save Config** buttons — import/export JSON configs
- Generate button with result count display

---

## Files Modified

- `visualizer3d.html` — Export system implementation (lines 675-2200 approx)
- `EXPORT_SYSTEM.md` — This documentation

---

## Notes for Maintainers

### When to Regenerate Schema Version

Increment `EXPORT_VERSION` and update this document when:
- A new field is added to any archetype
- A new archetype is added
- A field is removed or renamed
- An export format is added or removed

### Backward Compatibility

The export system maintains **forward-only compatibility**:
- Adding new fields is safe (old exports just don't include them)
- Removing fields is a breaking change (increment major version)
- Renaming fields is a breaking change

### Namespace

The namespace prefix is user-configurable in the export dialog. Default is `ProjectC`.

---

## Critical: Enum Serialization for Unity

**Unity's JsonUtility requires integer enums, NOT strings!**

### Wrong (string):
```json
"Archetype": "Column"
```

### Correct (integer):
```json
"Archetype": 1
```

The convertLayer() function handles this via archMap:
```javascript
const archMap = { sphere: 0, column: 1, platform: 2, tree: 3 };
```

### When adding a new archetype:
1. Add to archMap in convertLayer() (line 1862):
   ```javascript
   const archMap = { sphere: 0, column: 1, platform: 2, tree: 3, mynew: 4 };
   ```
2. Add to CloudArchetype enum in CloudTypes.cs:
   ```csharp
   public enum CloudArchetype { Sphere, Column, Platform, Tree, MyNew }
   ```

---

## Mesh Export (v6.1+)

Two mesh export options added for exporting generated cloud geometry.

### Mesh OBJ Export

Exports spheres as `.obj` file with real UV sphere geometry.

**Usage:** Click "Mesh OBJ" button in export dialog
**Output file:** `cloud_mesh_YYYY-MM-DD.obj`

**Format:**
- Each sphere converted to UV sphere (lat/lon segments based on radius)
- Vertex coordinates + face indices
- Comment headers with position/radius info

**Unity import:** Use ObjImporter or assimp pipeline

### Mesh Positions C# Export

Exports sphere data as static C# class for direct Unity integration.

**Usage:** Click "Mesh Positions" button in export dialog
**Output file:** `CloudMeshPositions_YYYY-MM-DD.cs`

**Remesh support:** When remesh is active, exports only visible spheres.

**Generated class:**
```csharp
namespace ProjectC.CloudGenerator
{
    public static class CloudMeshPositions
    {
        public const int SphereCount = N;
        public static readonly Vector3[] Positions;
        public static readonly float[] Radii;
        public static readonly float[] Densities;
        public static Mesh CreateMesh(); // Bonus: creates Unity Mesh
    }
}
```

**Note on Remesh:** The remesh operation filters internal spheres for visualization and export. The `CloudGenerator.Generate()` function in C# still generates all spheres. To get remeshed geometry in Unity:

1. **For merged mesh**: Export as OBJ → import into Unity as model
2. **For sphere data**: Export as Mesh Positions → use arrays directly (still individual spheres)

---

## Remesh (v7.0)

### Overview

Remesh consolidates multiple layer spheres into a single unified mesh, removing internal (occluded) spheres and creating one visual object.

### How It Works

1. **Generate** → creates all spheres from layers
2. **Remesh** → filters visible spheres, creates merged geometry
3. **Export** → exports remeshed geometry (if remesh was performed)

### Algorithm

```
1. For each sphere S:
   - Check if S is fully inside any other sphere
   - If YES → S is internal, mark for removal
   - If NO → S is visible, keep

2. For each visible sphere:
   - Generate UV sphere geometry (16 lat × 16 lon segments)
   - Add vertices and colors to merged BufferGeometry
   - Build triangle indices

3. Result: single mesh with all visible spheres merged
```

### Visibility Check (isSphereFullyInternal)

```javascript
function isSphereFullyInternal(sphere, allSpheres, epsilon = 0.1) {
  for (const other of allSpheres) {
    if (other === sphere) continue;
    const dist = distance(sphere, other);
    if (dist + sphere.radius <= other.radius + epsilon) {
      return true; // fully inside
    }
  }
  return false;
}
```

### State Variables

| Variable | Type | Description |
|----------|------|-------------|
| `_isRemeshed` | bool | True if remesh has been performed |
| `_remeshedGeometry` | THREE.BufferGeometry | Merged geometry (or null) |
| `_visibleSpheres` | array | Array of visible sphere objects |

### Export Behavior

When remesh is active, Mesh OBJ and Mesh Positions exports use `_visibleSpheres` instead of `currentSpheres`:

```javascript
const spheres = _isRemeshed && _visibleSpheres.length > 0
  ? _visibleSpheres
  : (currentSpheres.length > 0 ? currentSpheres : generateCloud(resolveLayers()));
```

### Limitations

- **O(n²) visibility check** — may be slow for 5000+ spheres
- **Spheres remain separate** — not a true hull union, just merged vertices
- **No mesh simplification** — vertex count can be high

### Future Improvements (Roadmap)

1. Spatial hashing for O(n log n) visibility
2. True convex hull or marching cubes for smooth surface
3. Mesh decimation after merge
4. Configurable segment count for remesh

---

## Changelog

### v7.0 (2026-05-10)
- Added **Remesh** button for mesh consolidation
- Added `isSphereFullyInternal()` — visibility check
- Added `getVisibleSpheres()` — filters internal spheres
- Added `performRemesh()` — creates merged geometry
- Modified `rebuildMesh()` — renders remeshed mesh
- Modified `exportMeshOBJ()` / `exportMeshPositions()` — support remeshed output
- Export version bumped to 7.0

### v6.1 (2026-05-09)
- Added Mesh OBJ export (real 3D geometry file)
- Added Mesh Positions C# export (static class with arrays)
- Fixed archetype serialization: string → integer for Unity JsonUtility
- Added Debug.Log after Load Config to confirm layer count
- OnEnable() now starts with empty layers list (user must Load Config)

### v6.0 (2026-05-09)
- Initial data-driven export system with schema, Editor integration
- Two export modes: Full Generator (ZIP) and Config Only (JSON)
- Added CloudConfigExport class for Unity JSON serialization
- Added Tools/Cloud Generator menu integration

---

## Unity Mesh Merge / Remesh (Roadmap)

### Overview

Currently the Unity export generates individual sphere GameObjects (one per sphere) which is expensive. A mesh merge feature would consolidate spheres into a single mesh.

### Architecture

```
CloudGenerator.Generate(layers)
    │
    ▼
List<CloudSphere> (1000-5000+ spheres)
    │
    ├──► [Current]  → CreatePrimitive per sphere (N draw calls)
    │
    └──► [New]  CloudMeshMerger.FilterInternalSpheres()
                        │
                        ▼
                List<CloudSphere> (visible, ~30-60% removed)
                        │
                        ▼
                CloudMeshMerger.MergeToMesh()
                        │
                        ▼
                UnityEngine.Mesh (1 draw call)
```

### New File: CloudMeshMerger.cs

```csharp
namespace {{NAMESPACE}}.CloudGenerator
{
    public static class CloudMeshMerger
    {
        public static List<CloudSphere> FilterInternalSpheres(
            List<CloudSphere> spheres,
            float epsilon = 0.1f
        );

        public static Mesh MergeToMesh(
            List<CloudSphere> spheres,
            bool filterInternal = true,
            int latSegments = 16,
            int lonSegments = 16,
            bool vertexColors = true
        );

        public static Mesh MergeToMeshAdaptive(
            List<CloudSphere> spheres,
            bool filterInternal = true,
            int minSegments = 8,
            int maxSegments = 24,
            float segmentsPerUnit = 0.5f
        );
    }
}
```

### Modified File: CloudGeneratorWindow.cs

Add "Generate & Remesh" button:
```csharp
if (GUILayout.Button("Generate & Remesh", GUILayout.Height(30)))
{
    lastResult = CloudGenerator.Generate(layers);
    var visible = CloudMeshMerger.FilterInternalSpheres(lastResult);
    var mesh = CloudMeshMerger.MergeToMesh(visible);

    GameObject go = new GameObject("CloudMesh_Merged");
    go.AddComponent<MeshFilter>().mesh = mesh;
    go.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
}
```

### Algorithm Options

| Option | Quality | Performance | Days | Notes |
|--------|---------|-------------|------|-------|
| **A. UV Sphere Merge** | Good | ~0.5-2s bake | 2-3 | Matches JS visualizer output |
| **B. Adaptive UV Sphere** | Better | ~0.5-2s bake | 2.5 | Radius-based segment count |
| **C. Marching Cubes** | Best (smooth surface) | ~5-10 days | 3-4 weeks | True iso-surface, not individual spheres |
| **D. GPU Instancing** | Same as A | Real-time | 1 day | No merge, 1 draw call via instancing |

**Recommendation:**
- **Baking/Export** → Option A or B (fast, matches visualizer)
- **Runtime in-game** → Option D (GPU Instancing, no merge needed)

### Implementation Checklist

- [ ] Create `CloudMeshMerger.cs` with `FilterInternalSpheres()`
- [ ] Add `MergeToMesh()` with UV sphere tessellation
- [ ] Add `MergeToMeshAdaptive()` for quality improvement
- [ ] Modify `CloudGeneratorWindow.cs` — add "Generate & Remesh" button
- [ ] Add mesh save to `.asset` via `AssetDatabase.CreateAsset()`
- [ ] Update EXPORT_VERSION to 8.0

### Notes

- Use `IndexFormat.UInt32` for >64K vertices
- Vertex colors map density: R=0.2+d*0.6, G=0.4+d*0.4, B=1.0
- Memory: 5000 spheres × 24×24 seg × 32 bytes ≈ 100MB — acceptable for baked assets

### Files Summary

| File | Action | Changes |
|------|--------|---------|
| `CloudMeshMerger.cs` | **New** | 4 static methods |
| `CloudGeneratorWindow.cs` | Modify | +1 button, mesh handling |
| `CloudMath.cs` | No change | — |
| `CloudTypes.cs` | No change | — |
| `CloudGenerator.cs` | No change | — |

---

## UE Plugin Export (v6.0+) - EXPERIMENTAL/UNSTABLE

### Overview

UE Plugin export generates a complete Unreal Engine 5 plugin with Blueprint API for procedural cloud generation.

### Usage

1. Click **UE Plugin** button in export dialog
2. ZIP file `CloudGenerator_v6.0.zip` downloads
3. Extract to `YourProject/Plugins/CloudGenerator_v6.0/`
4. Regenerate VS project files
5. Build in UE Editor

### Plugin Structure

```
CloudGenerator_v6.0/
├── CloudGenerator_v6.0.uplugin
└── Source/
    └── CloudGenerator/
        ├── CloudGenerator.Build.cs
        ├── Public/
        │   ├── CloudMath.h
        │   └── CloudGeneratorBPLibrary.h
        └── Private/
            ├── CloudMath.cpp
            └── CloudGeneratorBPLibrary.cpp
```

### Features

- `UCloudGeneratorBPLibrary` BlueprintFunctionLibrary
- `GenerateFromJSON(const FString&)` - Generate spheres from JSON
- `GenerateFromConfig(const FCloudLayerConfigBP&)` - Generate from struct
- `ConfigToJSON(const FCloudLayerConfigBP&)` - Struct to JSON
- `JSONToConfig(const FString&, FCloudLayerConfigBP&)` - JSON to struct
- `GetVersion()` - Returns plugin version

### USTRUCT Types

- `FCloudSphereBP` - Output: Location, Radius, Density, Archetype
- `FCloudSizeRangeBP` - Min, Max
- `FColumnParamsBP` - Height, BaseRadius, TopRadius, Floors, RingsPerFloor, Wobble
- `FPlatformParamsBP` - Width, Depth, CenterThickness, EdgeThickness, InteriorDensity, EdgeRings
- `FTreeParamsBP` - BaseRadius, MaxDepth, BranchElongation, TaperRatio, BranchAngle, BranchProbability
- `FCloudLayerConfigBP` - Full layer config with nested structs

### KNOWN ISSUES - CRASHES ON LOAD

**Status: DISABLED due to crashes. UE 5.7 build fails with EXCEPTION_ACCESS_VIOLATION.**

The generated plugin causes UE Editor to crash on startup with:
```
Plugin 'CloudGenerator_v6.0' failed to load because module 'CloudGenerator' 
could not be initialized successfully after it was loaded.
```

**Crash Details:**
- Exception: `EXCEPTION_ACCESS_VIOLATION reading address 0x0000...`
- Stack: CoreUObject → registration code
- Timing: Module initialization (SecondsSinceStart: 0)

**Attempted Fixes (all failed):**
1. Removed in-class default initializers from UPROPERTY members
2. Removed invalid include paths from Build.cs
3. Added `bUseUnity = false` to Build.cs
4. Cleaned Intermediate/Binaries folders
5. Changed PI constants to UE_DOUBLE_PI
6. Fixed TJsonWriterFactory API usage

**Root Cause (suspected):**
- Visual Studio 2026 (14.50) not supported by UE 5.7 (prefers VS2022 14.44)
- Compiler version mismatch causes ABI issues with generated struct layout

### Forcing UE Plugin Export (NOT RECOMMENDED)

To enable the UE Plugin button despite crashes, search for:
```javascript
// TODO: UE Plugin crashes on load - disabled
```

And comment out the early return. Be aware crashes will occur.

### Recommendation

Use Unity export instead. UE plugin export requires:
1. VS2022 v14.44 installed (not VS2026)
2. OR manual fixes to generated code
3. OR wait for UE version that supports VS2026
