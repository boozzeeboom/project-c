# Cloud Generator Export System v6.0

## Overview

The export system allows users to extract either:
1. **Full Generator** — Complete math library + types + generator implementation for Unity/C#/other projects
2. **Config Only** — Just the parameters, for use with an already-connected generator

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

---

## Changelog

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
