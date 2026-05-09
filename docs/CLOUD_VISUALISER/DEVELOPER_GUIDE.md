# Cloud Visualizer - Developer Guide

## Quick Reference

### What Gets Auto-Converted (Don't Touch)
- **camelCase → PascalCase**: All simple fields convert automatically (seed → Seed, density → Density)
- **nested objects**: sizeRange, columnParams, platformParams, treeParams auto-convert
- **field types**: Handled by FIELD_TYPES mapping

### What Requires Manual Updates
- **New simple field**: Add to EXPORT_SCHEMA.common or archetype section + FIELD_TYPES + getDefaultLayer()
- **New nested param**: Add to archetype section in EXPORT_SCHEMA + FIELD_TYPES with dot notation
- **New archetype**: Add to EXPORT_SCHEMA, FIELD_TYPES, UI dropdown, generator function, dispatcher
- **New export format**: Add template to TEMPLATES + genericExport() case + UI button

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        VISUALIZER DATA FLOW                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐  │
│   │   UI Layer   │───▶│window._      │───▶│ genericExport()  │  │
│   │ (renderList) │    │advancedLayers│    │  (convertLayer)  │  │
│   └──────────────┘    └──────────────┘    └──────────────────┘  │
│                                                      │          │
│                            ┌─────────────────────────┘          │
│                            ▼                                    │
│                   ┌─────────────────┐                           │
│                   │   JSON Config   │                           │
│                   │   (camelCase)   │                           │
│                   └────────┬────────┘                           │
│                            │ convertLayer()                      │
│                            ▼                                    │
│                   ┌─────────────────┐                           │
│                   │   Unity JSON    │                           │
│                   │  (PascalCase)   │                           │
│                   └─────────────────┘                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Key Files

| File | Purpose | When to Edit |
|------|---------|--------------|
| `visualizer3d.html` | Main application | Rarely - core structure |
| `EXPORT_SCHEMA` (line 685) | Field definitions | When adding params |
| `FIELD_TYPES` (line 715) | Type mappings | When adding params |
| `TEMPLATES` (line 761) | Code generation | When adding formats |
| `getDefaultLayer()` (line 3249) | Default values | When adding params |
| `convertLayer()` (line 1820) | JSON conversion | Rarely |
| `genericExport()` (line 1780) | Export dispatcher | When adding formats |
| `showExportDialog()` (line 1902) | Export UI | When adding formats |
| `renderLayerList()` (line 3393) | UI rendering | When adding params |

---

## Field Conversion Rules

### Simple Fields (Auto-Convert)
```javascript
// UI (camelCase) → JSON (PascalCase)
// seed → Seed, density → Density, jitter → Jitter
```
**No manual work needed** - `convertLayer()` handles this automatically.

### Nested Objects (Auto-Convert)
```javascript
// UI → JSON
// sizeRange.min → SizeRange.Min
// columnParams.height → ColumnParams.Height
// platformParams.width → PlatformParams.Width
// treeParams.maxDepth → TreeParams.MaxDepth
```
**No manual work needed** - special cases in `convertLayer()`.

### Enum Fields (Must Convert to Integer)
```javascript
// UI (string) → JSON (int)
// archetype: "sphere" → Archetype: 0
// archetype: "column" → Archetype: 1
// archetype: "platform" → Archetype: 2
// archetype: "tree" → Archetype: 3
```
**Manual fix required** - see Archetype Enum Map below.

### Archetype Enum Map
```javascript
const archMap = { sphere: 0, column: 1, platform: 2, tree: 3 };
```
If you add a new archetype, add it here AND update CloudTypes.cs CloudArchetype enum.

---

## Adding a New Simple Field (e.g., "newParam" for Sphere)

### 1. EXPORT_SCHEMA (line 685)
```javascript
sphere: [
  'cloudSize', 'cascadeDepth', 'bumpsPerLevel', 'childRatio',
  'sizeVariation', 'parentCount', 'ellipsoidY', 'ellipsoidXZ',
  'maxSphereCount', 'newParam'  // ← ADD HERE
],
```

### 2. FIELD_TYPES (line 715)
```javascript
'newParam': 'float',  // ← ADD HERE (or 'int', 'bool')
```

### 3. getDefaultLayer() (line 3249)
```javascript
base.newParam = 0.5;  // ← ADD DEFAULT VALUE in sphere section
```

### 4. renderLayerList() (find sphere section around line 3416)
```javascript
<div class="control-group">
  <label>New Param <span class="value">${layer.newParam || 0.5}</span></label>
  <input type="range" ... onchange="updateLayerField(${index}, 'newParam', this.value)">
</div>
```

### 5. CloudGeneratorWindow.cs DrawLayerInspector() - AUTO-GENERATED
If using Full Generator export, this is auto-generated from template. No manual edit needed.

---

## Adding a New Nested Parameter (e.g., "newColumnParam")

### 1. EXPORT_SCHEMA (line 685)
```javascript
column: {
  section: 'columnParams',
  fields: ['height', 'baseRadius', 'topRadius', 'floors',
           'ringsPerFloor', 'wobble', 'newColumnParam']  // ← ADD
},
```

### 2. FIELD_TYPES (line 715)
```javascript
'columnParams.newColumnParam': 'float',  // ← ADD with dot notation
```

### 3. getDefaultLayer() (line 3266)
```javascript
base.columnParams = {
  height: 40,
  baseRadius: 8,
  // ... existing ...
  newColumnParam: 0.5  // ← ADD DEFAULT
};
```

### 4. convertLayer() (line 1828) - IF NEEDED
Usually auto-handled, but check if structure is standard nested object.

### 5. CloudTypes.cs ColumnParams class
```csharp
public float NewColumnParam = 0.5f;  // ← ADD HERE
```

---

## Adding a New Archetype

### 1. EXPORT_SCHEMA (line 685)
```javascript
mynew: {
  section: 'myNewParams',
  fields: ['param1', 'param2', ...]
},
```

### 2. FIELD_TYPES (line 715)
```javascript
'myNewParams.param1': 'float',
'myNewParams.param2': 'int',
```

### 3. getDefaultLayer() (line 3249)
```javascript
} else if (archetype === 'mynew') {
  base.sizeRange = { min: 3, max: 10 };
  base.myNewParams = {
    param1: 1.0,
    param2: 5
  };
}
```

### 4. UI Dropdown (find archetype selects in renderLayerList())
```html
<option value="mynew">My New</option>
```

### 5. Archetype Mapping (line 1862)
```javascript
const archMap = { sphere: 0, column: 1, platform: 2, tree: 3, mynew: 4 };
```

### 6. Generator Function
Create `generateMyNewLayer()` following existing pattern (generateSphereLayer, etc.)

### 7. Dispatcher (getArchetypeGenerator or similar)
```javascript
case 'mynew': return generateMyNewLayer;
```

### 8. CloudTypes.cs
```csharp
public enum CloudArchetype { Sphere, Column, Platform, Tree, MyNew }
```

---

## Data Flow Deep Dive

### On Export (Visualizer → JSON)

```
1. User clicks "Config Only" or "Full Generator"
2. showExportDialog() calls genericExport(layers, format, namespace)
3. genericExport('json') uses convertLayer() for each layer
4. convertLayer() transforms:
   - archetype string → integer (archMap)
   - sizeRange {min,max} → SizeRange {Min,Max}
   - columnParams → ColumnParams (nested object)
   - other fields: first letter capitalized
5. JSON.stringify() produces final output
```

### On Load (JSON → Unity)

```
1. User clicks "Load Config" in Unity Editor
2. File.ReadAllText() reads JSON file
3. JsonUtility.FromJson<CloudConfigExport>(json) deserializes
4. config.layers assigned to EditorWindow.layers
5. Repaint() refreshes UI
6. User clicks "Generate"
7. CloudGenerator.Generate(layers) uses layer.Archetype (enum as int)
```

---

## Unity JsonUtility Compatibility

### Critical Rules
1. **Enums must be integers** - "Column" won't work, use 1
2. **PascalCase required** - Unity's JsonUtility doesn't convert camelCase
3. **No private fields** - all serialized fields must be public
4. **No property getters** - JsonUtility can't serialize computed properties

### JSON Format for Unity
```json
{
  "layers": [
    {
      "Archetype": 1,
      "Enabled": true,
      "Seed": 42,
      "SizeRange": { "Min": 5, "Max": 20 },
      "ColumnParams": { "Height": 40, "BaseRadius": 8, ... }
    }
  ],
  "generatorVersion": "6.0"
}
```

NOT:
```json
{
  "layers": [
    {
      "archetype": "column",    ← WRONG: string not int
      "enabled": true,           ← WRONG: camelCase not PascalCase
      ...
    }
  ]
}
```

---

## Testing Checklist

After any change to export system:

1. **Visualizer renders correctly** - UI shows updated values
2. **Config Only JSON exports** - open in text editor, check PascalCase
3. **Full Generator ZIP contains**:
   - CloudMath.cs
   - CloudTypes.cs
   - CloudGenerator.cs
   - Editor/CloudGeneratorWindow.cs
4. **Unity Load Config works** - layers show correct Archetype in dropdown
5. **Unity Generate works** - correct shape type generated (sphere/column/platform/tree)
6. **Enum values correct** - Platform=2, Column=1, etc.

---

## Common Mistakes

### 1. Forgetting to add to FIELD_TYPES
New field won't have type info, may cause runtime issues.

### 2. Wrong default value type
```javascript
// WRONG: default as string
base.newParam = "0.5";
// CORRECT: default as number
base.newParam = 0.5;
```

### 3. Wrong archetype mapping
```javascript
// WRONG: string value
result['Archetype'] = layer[key].charAt(0).toUpperCase() + layer[key].slice(1);
// CORRECT: integer value
const archMap = { sphere: 0, column: 1, platform: 2, tree: 3 };
result['Archetype'] = archMap[layer[key]] ?? 0;
```

### 4. Forgetting CloudTypes.cs update
When adding new archetype, must update enum:
```csharp
public enum CloudArchetype { Sphere, Column, Platform, Tree, MyNew }
```

---

## File Location Reference

| Line | Content | Purpose |
|------|---------|---------|
| 685 | `EXPORT_SCHEMA` | Field definitions per archetype |
| 715 | `FIELD_TYPES` | JavaScript → C# type mapping |
| 761 | `TEMPLATES` | Code generation templates |
| 1780 | `genericExport()` | Export format dispatcher |
| 1820 | `convertLayer()` | JSON key transformation |
| 1902 | `showExportDialog()` | Export UI HTML |
| 1971 | `exportFullGenerator()` | ZIP creation |
| 2287 | `exportConfigOnly()` | JSON export |
| 3249 | `getDefaultLayer()` | Default values per archetype |
| 3393 | `renderLayerList()` | UI rendering |

---

## Export Functions Reference

| Line | Function | Purpose |
|------|----------|---------|
| 685 | `EXPORT_SCHEMA` | Field definitions per archetype |
| 715 | `FIELD_TYPES` | JavaScript → C# type mapping |
| 761 | `TEMPLATES` | Code generation templates |
| 1780 | `genericExport()` | Export format dispatcher |
| 1820 | `convertLayer()` | JSON key transformation |
| 1902 | `showExportDialog()` | Export UI HTML |
| 1971 | `exportFullGenerator()` | ZIP creation (Full Generator) |
| 2287 | `exportConfigOnly()` | JSON export (Config Only) |
| 2318 | `exportMeshOBJ()` | OBJ mesh file export (v6.1+) |
| 2401 | `exportMeshPositions()` | C# static class export (v6.1+) |
| 3249 | `getDefaultLayer()` | Default values per archetype |
| 3393 | `renderLayerList()` | UI rendering |

---

## Adding New Export Format

To add a new export format (like mesh exports in v6.1):

### 1. Add button in showExportDialog() HTML
```javascript
<button id="btn-myformat" style="...">My Format</button>
```

### 2. Add click handler after existing handlers
```javascript
document.getElementById('btn-myformat').onclick = () => {
  exportMyFormat();
};
```

### 3. Create export function
```javascript
function exportMyFormat() {
  const spheres = currentSpheres.length > 0 ? currentSpheres : generateCloud(resolveLayers());
  // ... generate content ...
  downloadFile('output.ext', content);
}
```

### 4. Do NOT modify genericExport() - it's only for layer config JSON

---

## When to Increment Version

Increment `EXPORT_VERSION` when:
- New field added to any archetype
- New archetype added
- Field removed or renamed
- Export format added/removed
- JSON structure changes in any way

Do NOT increment for:
- UI changes only
- Internal variable renaming
- Comment updates

---

## Quick Troubleshooting

**Problem: Unity shows wrong archetype (always Sphere)**
→ JSON has string archetype ("Column") instead of integer (1)
→ Fix: Update convertLayer() archetype mapping

**Problem: JSON has camelCase keys**
→ convertLayer() not being used correctly
→ Check: genericExport() calls convertLayer() for JSON format

**Problem: Load Config does nothing**
→ config.layers is null after deserialization
→ Check: JSON structure matches CloudConfigExport class

**Problem: New field not appearing in export**
→ Forgot to add to EXPORT_SCHEMA
→ Check both archetype section AND FIELD_TYPES

**Problem: Mesh export gives empty file**
→ Need to click Generate first to populate currentSpheres
→ Or the function will auto-generate from current layers

---

## v6.1 Mesh Exports

### OBJ Export (exportMeshOBJ)
- Generates `.obj` file with real sphere geometry
- Each sphere = UV sphere mesh (lat/lon segments proportional to radius)
- Format: vertices + face indices
- Can be imported into Unity, Blender, or any 3D software

### C# Positions Export (exportMeshPositions)
- Generates static class `CloudMeshPositions`
- Contains: `Positions[]`, `Radii[]`, `Densities[]` arrays
- Bonus method `CreateMesh()` creates Unity Mesh from point cloud
- Direct integration into Unity projects

### When to use which
- **OBJ**: When you need the actual 3D model file (exchange format)
- **C# Positions**: When you want to integrate sphere data directly in code