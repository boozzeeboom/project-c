# Cloud Generator - Blueprint Only Plugin v6.1

## Installation for Blueprint-Only Projects

### Option A: Project Plugin
1. Copy `CloudGenerator/` folder to `YourProject/Plugins/`
2. **DO NOT** regenerate project files
3. Restart Unreal Editor
4. Plugin auto-loads - no compilation needed

### Option B: Engine Plugin
1. Copy to `UE_5.x/Engine/Plugins/CloudGenerator/`
2. Restart UE
3. Works for all projects (Blueprint or C++)

---

## Quick Start

### Load JSON Config
```
1. Place JSON file in Content/ (e.g., /Game/Configs/cloud_config.json)
2. Use: Load File → Cloud Generator BP → Generate From JSON
3. Get array of FCloudSphere (locations, radii)
```

### Using Generated Spheres
```
Generate From JSON
      ↓
For Each Loop (CloudSphere array)
      ↓
Spawn Actor at Location (use Sphere.Radius for scale)
```

---

## Nodes Available

### Functions
- **Load JSON** - Load .json file as string
- **Generate From JSON** - String → Array of FCloudSphere
- **Generate From Config** - FCloudLayerConfig → Array of FCloudSphere
- **Layers To JSON** - Config array → JSON string
- **JSON To Layers** - JSON string → Config array

### Structs (for BP)
- **FCloudSphere** - Location, Radius, Density, Archetype
- **FCloudLayerConfig** - Full layer configuration
- **FColumnParams, FPlatformParams, FTreeParams** - Archetype params

---

## JSON Format (Same as Unity Visualizer)

```json
{
  "layers": [
    {
      "Archetype": 1,
      "Enabled": true,
      "Seed": 12345,
      "Density": 0.6,
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

**Archetype values:** 0=Sphere, 1=Column, 2=Platform, 3=Tree

---

## Blueprint Example

```
[Load JSON File] → [Generate From JSON] → [ForEach CloudSphere]
                                              ↓
                                    [Spawn Actor at Location]
                                           (Scale = Sphere.Radius)
```

---

## Troubleshooting

**Q: Plugin doesn't appear after restart**
A: Make sure you copied the FOLDER (CloudGenerator/), not just files

**Q: "Module not found" error**
A: You need C++ support in project. Create empty C++ class first via:
   File → New C++ Class → None

**Q: How to export JSON from Unity visualizer?**
A: In visualizer: Export → "Config Only" → download .json file

---

## Version
6.1 - Initial Blueprint-friendly release