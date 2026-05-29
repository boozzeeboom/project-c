# Star Sky System - Session Summary

## Overview
The ConstellationController manages the star field and constellations for the day-night cycle in this MMO project. Stars are rendered on a SKY DOME that follows the player's camera, ensuring all players see the same stars regardless of their world position.

## Working Parameters (Tested for MMO Large World)

For large MMO world (coordinates around 240000, 3000, 160000):

| Parameter | Working Value | Default | Description |
|-----------|---------------|---------|-------------|
| `skyDomeRadius` | **900000** | 900000f | Radius of sky dome sphere around player |
| `baseStarSize` | **3000** | 3000f | Base size of star quads in world units |
| `starSizeMagnitudeScale` | **1500** | 1500f | Size variation based on star magnitude |
| `skyDomeHeightOffset` | 100 | 100f | Height offset above camera |
| `constellationLineWidth` | 1.5 | 1.5f | Width of constellation lines |

## Architecture

### Key Concepts

1. **Sky Dome Pattern**: Stars are positioned on a sphere (radius 900000) that follows the player camera
2. **Local Coordinates**: Star positions are stored in LOCAL coordinates relative to SkyDome center
3. **DontDestroyOnLoad**: Both ConstellationController and SkyDome_Stars persist across scene loads

### Object Hierarchy
```
ConstellationController (DontDestroyOnLoad, in Bootstrap scene)
  └── SkyDome_Stars (child, DontDestroyOnLoad)
       ├── MeshFilter + MeshRenderer (stars mesh with 200+ stars)
       └── [ConstellationName]_Line (LineRenderers for each constellation)
```

## Features Implemented

### 1. Sky Dome Rendering
- Stars rendered as quads on inside of sphere
- Additive blending (`Blend SrcAlpha One`) for bright star effect
- Sorting order 32767 for rendering priority

### 2. Constellation Lines (NEW)
- `showConstellationLines` (bool, default: true) - Enable/disable all constellation lines globally
- `constellationLineWidth` (float, default: 1.5f) - Width of constellation lines
- `SetConstellationLinesVisible(bool)` - Runtime method to toggle lines
- `SetConstellationLineWidth(float)` - Runtime method to change width

**Usage for character perks:**
```csharp
// Example: Astronomer perk unlocks constellation lines
var constellationController = FindObjectOfType<ConstellationController>();
constellationController.SetConstellationLinesVisible(hasAstronomerPerk);
```

### 3. Sky Dome Rotation
- `enableRotation` (bool, default: true) - Enable/disable sky rotation
- `rotationSpeedMultiplier` (float, default: 1.0f) - Rotation speed multiplier
  - 1.0 = Earth sidereal rotation (360° per 24 game hours)
  - 2.0 = 2x faster, 0.5 = 2x slower
- `serverTimeOfDay` (float) - Server time reference (auto-updated from ServerWeatherController)
- Auto-subscribes to `ServerWeatherController.OnTimeOfDayChanged` on enable
- Reads `ServerWeatherController.Instance.TimeOfDay` every frame for smooth rotation

### 4. Visibility System
- `forceFullVisibility` (bool, default: true) - Force 100% visibility (for testing)
- `SetStarVisibility(float timeOfDay)` - Calculate visibility based on time
- Night: 21:00 - 05:00 (100% visibility)
- Twilight: 1.5 hours before/after night

## Data Files

### ConstellationData_FullSky.asset
Location: `Assets/_Project/Data/ScriptableObjects/DayNight/ConstellationData_FullSky.asset`

Contains **200+ stars** across **24 constellations**:
- Ursa Major, Ursa Minor (Big and Little Dippers)
- Orion, Cassiopeia, Cygnus, Lyra
- Scorpius, Leo, Gemini, Taurus
- Aquila, Canis Major/Minor, Virgo, Bootes
- Perseus, Auriga, Ophiuchus, Pegasus
- Andromeda, Draco
- BackgroundStars (55 stars distributed across sky)
- BackgroundStars2 (45 stars at polar regions)

## Magnitude System

Star magnitude affects size using formula:
```
starSize = baseStarSize + (2f - star.magnitude) * starSizeMagnitudeScale
```

Lower magnitude = brighter star = larger on screen (inverse of real astronomy).

Real-world examples mapped to game:
- Sirius (mag -1.5): Largest stars
- Vega (mag 0.0): Very bright
- Arcturus (mag -0.1): Bright orange
- Polaris (mag 2.0): Medium
- Background stars (mag 3.5-5.0): Smaller

## Debug Features

- `showDebugGizmos` (bool) - Show yellow wire sphere and cyan line to camera in Scene view
- Minimal logging - only error when constellationData missing, and initialization complete with star count

## Known Working Configuration

**For large MMO world with coordinates ~240000, 3000, 160000:**
- `skyDomeRadius`: 900000
- `baseStarSize`: 3000
- `starSizeMagnitudeScale`: 1500

This configuration ensures stars are visible at appropriate scale for the large world coordinates.

## Integration

### With ServerWeatherController
- Subscribes to `OnTimeOfDayChanged` event
- Receives server-authoritative time
- Uses time to drive sky rotation

### Scene Loading Behavior
1. ConstellationController starts in BootstrapScene
2. After DontDestroyOnLoad, moves to DDOL
3. Survives scene 0_0, 1, 2 loading/unloading
4. SkyDome follows player camera via Update()

## Inspector Reference

```
ConstellationController (Script)
├─ Data
│  └─ Constellation Data: [ConstellationData_FullSky]
├─ Materials
│  ├─ Star Material: [StarMaterial.mat]
│  └─ Constellation Line Material: [optional]
├─ Sky Dome Settings
│  ├─ Sky Dome Radius: 900000
│  └─ Sky Dome Height Offset: 100
├─ Star Settings
│  ├─ Base Star Size: 3000
│  └─ Star Size Magnitude Scale: 1500
├─ Sky Dome Rotation (NEW)
│  ├─ Enable Rotation: ✓
│  └─ Rotation Speed Multiplier: 1.0
├─ Constellation Lines (NEW)
│  ├─ Show Constellation Lines: ✓
│  └─ Constellation Line Width: 1.5
└─ Debug
   ├─ Force Full Visibility: ✓
   └─ Show Debug Gizmos: ✓
```

## Future Enhancements

1. **Character Perk**: "Astronomer" skill that unlocks constellation lines
2. **Telescope Item**: Allow zooming on stars to see info
3. **Shooting Stars**: Random meteor events
4. **Aurora Borealis**: Northern lights effect at high latitudes
5. **Star Colors**: Implement temperature-based star colors (O/B/A/F/G/K/M spectral types)