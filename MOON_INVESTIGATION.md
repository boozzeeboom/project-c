# Moon Implementation - Technical Debt & Known Issues

## Created: 2026-05-26
## Status: BLOCKED - Requires Investigation

---

## Problem Summary

The Moon object in the scene is not rendering correctly. The moon mesh is not visible in game despite:
- Light from moon illuminating objects correctly
- Shader appearing functional in Material preview
- MoonController logic being sound

---

## Root Causes Identified (Investigation Needed)

### 1. Moon Object Configuration
- **Layer**: Was set to Layer 2 (may be culled by certain camera settings)
- **Material**: Was incorrectly assigned to `TestMoonMat` instead of `MoonMaterial`
- **Position**: Object at world origin (0,0,0) instead of following camera

### 2. MoonController Logic Issues
- The original MoonController uses `ServerWeatherController.Instance.TimeOfDay` which may not be accessible
- Moon position calculation may be failing silently
- Material property updates (`_MoonAge`, `_MoonPhase`) may not be reaching the shader

### 3. Skybox Conflict
- Changing Moon material settings affects skybox rendering
- This suggests material or shader conflict with skybox system

---

## What Was Tried (FAILED Approaches)

### Attempt 1: Layer Fix
- Changed Moon from Layer 2 to Layer 0 (Default)
- Result: No improvement

### Attempt 2: Material Fix
- Changed material from TestMoonMat to proper MoonMaterial
- Result: No visible improvement

### Attempt 3: Simple White Shader Test
- Created simple test shader with solid white color
- Applied to Moon mesh
- Result: Moon not visible even as white sphere

### Attempt 4: Sprite/Quad Approach
- Created MoonSprite.cs for quad-based rendering
- Result: Sprite also not visible, plus broke skybox

### Attempt 5: Position Modification
- Manually moved Moon to (0, 5, -10) with scale 20
- Result: Still not visible

---

## Key Observations

1. **Moon light IS working** - objects are illuminated at night
2. **Moon mesh is not rendered** - even simple white sphere invisible
3. **Shader was working before** - user confirmed shader worked in Inspector preview
4. **Skybox gets affected** - changes to moon material/sprite affect skybox rendering

---

## Open Questions

1. Why is mesh completely invisible despite being enabled and properly configured?
2. Why does moon light work but mesh doesn't?
3. Why does skybox get affected by moon material changes?
4. Is there a camera culling mask issue specific to this project?

---

## Next Steps (Recommended)

1. **Check Camera Culling Mask** - ensure Moon layer is not being culled
2. **Check Render Queue** - ensure moon shader queue doesn't conflict
3. **Compare with Working Objects** - compare Moon settings with TestCube/TestSphere that ARE visible
4. **Test in Clean Scene** - create minimal test scene with just camera and moon
5. **Check URP Settings** - project uses URP, verify moon renders correctly in URP pipeline

---

## Files Modified During Investigation

- `Assets/_Project/Scripts/Core/DayNight/MoonController.cs` - Restored to original
- `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs` - ApplyMoon() emptied then restored
- `Assets/_Project/Shaders/Moon/MoonLunarPhase.shader` - Restored to original
- `Assets/_Project/Scenes/BootstrapScene.unity` - Moon layer and material corrected

---

## Lessons Learned

1. Do NOT modify multiple systems at once - change one thing, test, repeat
2. Always create backup/commit before making changes to working systems
3. Moon/skybox integration needs careful architectural planning
4. Large world scale (350000 units) requires special handling for distant objects