# Cloud Generator Plugin for Unreal Engine 5

**Version:** 6.1
**Compatibility:** UE 5.0+

Procedural cloud generation system with multi-archetype support. Plug-and-play integration with Blueprint API.

## Installation

### Option 1: Copy to Project
```
Copy CloudGenerator/ folder to your UE project:
YourProject/Plugins/CloudGenerator/
```

### Option 2: Copy to Engine
```
Copy to UE engine plugins:
UE_Engine/Plugins/CloudGenerator/
```

### Build
1. Regenerate project files (right-click .uproject > Generate Visual Studio files)
2. Build solution in Visual Studio
3. Restart UE editor

## Quick Start

### Blueprint Usage

1. Create a new Blueprint or open existing
2. Add "Cloud Generator Library" to your graph
3. Use functions:

```
Generate From JSON    → Input JSON string, get array of FCloudSphere
Generate From Layers  → Input array of FCloudLayerConfig structs
Layers To JSON        → Convert layers config to JSON string
JSON To Layers         → Parse JSON to layer configs
```

### Example BP Graph

```
[JSON String] ──→ [Generate From JSON] ──→ [Sphere Locations]
                   │
                   └── Debug: Print result count
```

### JSON Config Format

Same format as Unity Cloud Generator (compatible):

```json
{
  "layers": [
    {
      "Archetype": 1,
      "Enabled": true,
      "YOffset": 0,
      "Seed": 625458,
      "Density": 0.6,
      "Jitter": 0.3,
      "Clustering": 0.5,
      "SizeRange": { "Min": 5, "Max": 20 },
      "ColumnParams": {
        "Height": 40,
        "BaseRadius": 8,
        "TopRadius": 3,
        "Floors": 12,
        "RingsPerFloor": 8,
        "Wobble": 0.3
      }
    }
  ],
  "generatorVersion": "6.0"
}
```

### Archetype Values (for JSON)
- `0` = Sphere
- `1` = Column
- `2` = Platform
- `3` = Tree

## Structs

### FCloudLayerConfig
Main configuration for each layer. All fields are Blueprint-accessible.

**Common:**
- `Enabled` - Layer active
- `Seed` - Random seed for deterministic generation
- `Density` - Sphere density (0-1)
- `Jitter` - Position noise (0-0.5)
- `Clustering` - Clustering factor (0-1)
- `PositionVariation` - Random offset scale
- `YOffset` - Vertical offset between layers

**Sphere-specific:**
- `CloudSize` - Overall cloud radius (20-200)
- `CascadeDepth` - Recursion depth (1-5)
- `BumpsPerLevel` - Children per parent (12-128)
- `ChildRatio` - Spawn threshold (10-200)
- `SizeVariation` - Size randomization (0-1.5)
- `ParentCount` - Root spheres (1-12)
- `EllipsoidY/XZ` - Shape deformation

**Column-specific:**
- `Height` - Column height (10-100)
- `BaseRadius/TopRadius` - Column shape
- `Floors` - Number of levels (3-20)
- `RingsPerFloor` - Spheres per level (3-12)

**Platform-specific:**
- `Width/Depth` - Platform size (20-200)
- `CenterThickness` - Center height (1-10)
- `EdgeThickness` - Edge height (0.1-5)
- `InteriorDensity` - Interior sphere count

**Tree-specific:**
- `MaxDepth` - Recursion depth (2-8)
- `BranchElongation` - Branch length factor
- `BranchAngle` - Branch spread angle
- `BranchProbability` - Spawn chance

### FCloudSphere
Output struct from generation:

- `Location` - FVector (X, Y, Z)
- `Radius` - Sphere radius
- `Density` - Density value (for coloring)
- `Archetype` - Source archetype

## C++ Integration

```cpp
#include "CloudGeneratorLibrary.h"

// Generate from JSON
FString JsonConfig = TEXT("{\"layers\": [...]}");
TArray<FCloudSphere> Spheres = UCloudGeneratorLibrary::GenerateFromJSON(JsonConfig);

// Generate from layers
TArray<FCloudLayerConfig> Layers;
Layers.Add DefaultLayer;
TArray<FCloudSphere> Spheres = UCloudGeneratorLibrary::GenerateFromLayers(Layers);

// Convert to/from JSON
FString Json = UCloudGeneratorLibrary::LayersToJSON(Layers);
UCloudGeneratorLibrary::JSONToLayers(Json, Layers);
```

## Editor Module

The `CloudGeneratorEditor` module provides:
- Menu integration under Window category
- Extensible for future visual editor UI

Current version: Basic BlueprintFunctionLibrary integration only.
Full visual editor (Slate UMG) planned for future release.

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 6.1 | 2026-05-09 | Initial UE plugin release |
| 6.0 | 2026-05-09 | Unity version released |

## Notes

- Generation is deterministic (same seed = same result)
- PositionVariation adds random offset to each sphere
- Layers stack vertically with automatic Y offset
- MaxSphereCount prevents infinite recursion on Sphere archetype
- JSON uses integer enums (0-3) for cross-platform compatibility