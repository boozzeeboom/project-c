# Moon System - Technical Documentation

## Last Updated: 2026-05-26

---

## Overview

The Moon system provides a lunar object that orbits around the game world center (0,0,0), visible in the sky at night with proper phase cycling.

---

## Components

### MoonController.cs
Controls moon position, orbit, and visual properties.

**Settings (configurable in Inspector):**
| Parameter | Default | Description |
|-----------|---------|-------------|
| `moonOrbitRadius` | 80000 | Distance from world center (world is ~350000 units) |
| `moonOrbitSpeed` | 1 | Orbit speed multiplier (1 = full cycle in ~60 seconds at 60fps) |
| `lunarCycleGameDays` | 29.5 | Full lunar cycle in game days |
| `moonAge` | 0 | Current moon age in game days (0 to lunarCycleGameDays) |

**Timing Source:**
- Moon phase is now synchronized with **ServerWeatherController** game time
- Uses `TotalGameDays` (cumulative game days) + current `TimeOfDay`
- Phase formula: `moonAge = (totalDays + timeOfDay/24) % lunarCycleGameDays`
- No hardcoded real-time calculations

**Behavior:**
- Orbits around world origin (0, 0, 0)
- Moves in vertical arc (rises and sets)
- Visible only at night (21:00 - 05:00)
- Twilight fade during transition hours (1.5 hours)

### MoonMaterial.mat
Material using `Project C/Moon/MoonLunarPhase` shader.

**Shader Properties:**
| Property | Description |
|----------|-------------|
| `_MoonPhase` | Current phase (0-1) |
| `_NightVisibility` | Opacity multiplier for day/night |
| `_MoonColor` | Base moon color (0.9, 0.9, 0.85) |
| `_ShadowColor` | Dark side color (0.1, 0.1, 0.15) |
| `_CraterScale` | Crater texture scale (8.0) |
| `_CraterContrast` | Crater texture contrast (0.3) |
| `_RimPower` | Rim light power (3.0) |
| `_RimIntensity` | Rim light intensity (0.2) |

---

## Known Issues

### Phase Shader Not Updating
**Symptom:** Lunar phases work in material preview but don't change during gameplay.

**Cause:** The `_MoonPhase` property is being set via `moonMaterial.SetFloat()` but the shader may not be responding correctly to this parameter change.

**Shader formula (original):**
```hlsl
float phaseAngle = _MoonPhase * 6.28318;  // 2 * PI
float litEdge = -cos(phaseAngle * 2.0) * 0.5 + 0.5;
```

**Recommended fix:** The shader uses a 2D UV-based approach that may not produce accurate crescent phases. A proper crescent phase shader should use:
- Terminator position based on `sin(phaseAngle)`
- Waxing/Waning logic based on phase < 0.5

### Phase Sync with Game Time
**Issue:** Phase was using real-time calculations instead of game time.

**Fix:** Added `TotalGameDays` to `ServerWeatherController` and synchronized moon phase:
- Moon phase now derives from game calendar
- 29.5 game days = full lunar cycle
- Future: expose calendar/time API for other systems

---

## Architecture

```
Moon GameObject (at ~240000, 1200, 160000)
├── MoonController.cs     - Orbit and phase logic
├── MeshRenderer          - Renders moon mesh
├── SphereCollider         - Collision (if needed)
├── Light (Directional)   - Moonlight
└── MoonMaterial.mat      - Shader material
```

---

## Future Improvements

1. **Phase Shader Rewrite** - Current shader uses confusing UV manipulation. Should use proper terminator line calculation based on phase angle.

2. **Integration with DayNightController** - Currently ApplyMoon() is empty. MoonController handles its own updates but should coordinate with overall day/night system.

3. **Network Synchronization** - Moon phase should be synchronized across all clients via ServerWeatherController.

---

## 2026-05-26 Session Summary

### Moon Phase System Refactor
1. **ServerWeatherController** - Added `TotalGameDays` property
   - Increments when `_timeOfDay` wraps from 24f back to 0f
   - Provides cumulative game day count for other systems

2. **MoonController** - Refactored phase calculation
   - Now uses `ServerWeatherController.TotalGameDays + TimeOfDay/24f`
   - Phase synced to game calendar, not real time
   - `lunarCycleGameDays = 29.5` (configurable)
   - Removed hardcoded phase speed calculations

### Skybox/Night Cycle Fix
**Problem:** Night skybox was tied to moon visibility, causing black gaps when moon was not visible.

**Solution:** Separated skybox switching from moon:
- **Skybox** - Switches based on sun position (6f-20f = day, else night)
- **Volume** - Switches based on sun position (6f-20f = day, else night)  
- **Moon** - Independent orbit and visibility (21f-5f = visible)

---

## Related Files

- `Assets/_Project/Scripts/Core/DayNight/MoonController.cs` - Main controller
- `Assets/_Project/Shaders/Moon/MoonLunarPhase.shader` - Phase shader
- `Assets/_Project/ScriptableObjects/DayNight/MoonMaterial.mat` - Moon material
- `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs` - Day/night coordinator