# Day-Night Cycle — Master Prompt (Session Runner)

## Overview

This document is the **master prompt template** for all future implementation sessions of the Day-Night Cycle system. Copy relevant sections per session. Read ALL docs in `docs/world/Day-Night/` before starting any session.

---

## Quick Reference

| Item | Value |
|------|-------|
| **Doc root** | `docs/world/Day-Night/` |
| **Scripts root** | `Assets/_Project/Scripts/Core/DayNight/` |
| **Shaders root** | `Assets/_Project/Shaders/` |
| **Prefabs** | `Assets/_Project/Prefabs/` |
| **Volumes** | `Assets/_Project/Volumes/DayNight/` |
| **Materials** | `Assets/_Project/Materials/` |
| **Server authority** | `ServerWeatherController.cs` (extend) |
| **Client controller** | `DayNightController.cs` (new) |

---

## Previous Session — What Was Done

1. **Shader fix**: `VeilRaymarchMesh.shader` line 295 — hardcoded light direction replaced with `_LightDir` uniform
2. **Controller update**: `VeilRaymarchMeshController.cs` — added `SetLightDirection()` and `SetDayNightFactor()` public methods
3. **Documentation created**: `SPEC.md`, `PLAN.md`, `REQUIREMENTS.md`, `SHADER_FIX.md`, `SESSION_SUMMARY.md`
4. **Old integration**: `CloudSystem.prefab` already has `enableDayNightCycle=0` — no action needed

---

## Requirements Summary

| Feature | Detail |
|---------|--------|
| **Phases** | Morning (5–8h) → Midday (8–17h) → Evening (17–19:30h) → Twilight (19:30–21h) → Night (21–5h) |
| **Variability** | Each phase has randomized hue/saturation/intensity ±range, seed = `floor(serverDayNumber)` |
| **Temperature** | Post-filter color overlay. Cold ≤0°C, Neutral 0–15°C, Warm 15–25°C, Hot ≥25°C |
| **Server authority** | `ServerWeatherController` sets `timeOfDay` + `temperature` |
| **Time speed** | Default: 24h / 30s. Server-configurable. |
| **Moon** | Mesh + lunar phases (full ~29.5 day cycle). Night-only visible. |
| **Stars** | 12 constellations (real sky positions). Visible at night, fade in twilight. Deferred: click interaction for future sextant skill. |
| **Storm** | Storms darken ambient ×0.6, fog density ×2.0. |
| **No hardcode** | All values in ScriptableObjects/configs. |

---

## Session Templates

---

### SESSION 1: Infrastructure — ScriptableObjects + Core Components

**Goal**: Create folder structure, ScriptableObject stubs, and component stubs.

**Read first**:
- `docs/world/Day-Night/SPEC.md` (§1–§6)
- `docs/world/Day-Night/PLAN.md` (§2)

**Do**:
1. Create folder `Assets/_Project/Scripts/Core/DayNight/`
2. Create `TimeOfDayPhase.cs` — ScriptableObject with fields per SPEC.md §5.1
3. Create `DayNightProfile.cs` — ScriptableObject containing `TimeOfDayPhase[5]`
4. Create `TemperatureFilterConfig.cs` — ScriptableObject per SPEC.md §5.3
5. Create `TemperatureFilter.cs` — component with `GetTemperatureOverlay(float temp)` method
6. Create stub `DayNightController.cs` — empty class with all fields declared (no logic yet)
7. Create `MoonController.cs` — stub with `moonPhase` field and `UpdateMoon()` placeholder
8. Create `ConstellationData.cs` — ScriptableObject with 12 constellation definitions (positions only, no rendering)
9. Create stub `ConstellationController.cs` — empty class (no star rendering yet)

**Agents to use**:
- `game-designer` — review phase definitions and variability ranges
- `unity-specialist` — verify ScriptableObject structure and component architecture

**Unity MCP verification**:
- After creating each script, use `unityMCP_validate_script` to check syntax
- Use `unityMCP_manage_asset(action="create_folder")` for folder creation

**Testing**:
- All scripts compile without errors
- ScriptableObjects appear in Create menu (ProjectC/DayNight/)

---

### SESSION 2: Server Authority — Extend ServerWeatherController

**Goal**: Make `ServerWeatherController` the single source of truth for time + temperature.

**Read first**:
- `docs/world/Day-Night/SPEC.md` (§4)
- `docs/world/Day-Night/REQUIREMENTS.md` (§4)

**Do**:
1. Open `ServerWeatherController.cs`
2. Add fields:
   ```csharp
   [Header("Time of Day")]
   [SerializeField] private float _timeOfDay = 12f;
   [SerializeField] private float _dayCycleRealHours = 1f; // 1 real hour = 24 game hours
   [SerializeField] private bool _enableTimeAutoAdvance = true;
   [SerializeField] private float _timeBroadcastInterval = 5f;
   private float _timeTimer = 0f;

   [Header("Temperature")]
   [SerializeField] private float _temperature = 20f;
   [SerializeField] private float _tempBroadcastInterval = 10f;
   private float _tempTimer = 0f;
   ```
3. Add properties: `TimeOfDay`, `Temperature`
4. Add `BroadcastTimeOfDayClientRpc(float time)` — fires every `_timeBroadcastInterval`
5. Add `BroadcastTemperatureClientRpc(float temp)` — fires every `_tempBroadcastInterval`
6. Add `SetTimeOfDayServerRpc(float time)` — GM immediate set
7. Add `SetTemperatureServerRpc(float temp)` — GM immediate set
8. Add `SetDayCycleSpeedServerRpc(float speed)` — change cycle speed
9. In `Update()`, add time auto-advance + both broadcast timers
10. Keep wind system unchanged

**Agents to use**:
- `network-programmer` — review RPC design and synchronization logic
- `unity-specialist` — verify NetworkBehaviour patterns

**Unity MCP verification**:
- Use `unityMCP_execute_code` to test RPC attribute compilation (basic syntax check)

**Testing**:
- Server change to timeOfDay → all clients receive update within `_timeBroadcastInterval`
- GM command `SetTimeOfDay` works from client RPC
- Temperature syncs at correct interval

---

### SESSION 3: DayNightController — Client Lighting

**Goal**: `DayNightController` handles all client-side lighting based on server TOD.

**Read first**:
- `docs/world/Day-Night/SPEC.md` (§4–§6)
- `docs/world/Day-Night/SESSION_SUMMARY.md`

**Do**:
1. Implement `DayNightController.cs`:
   - Subscribe to `ServerWeatherController` events OR poll `_timeOfDay`
   - Phase detection: which of 5 phases `serverTimeOfDay` falls into
   - Blend factor: `Mathf.InverseLerp(phaseStart, phaseEnd, time)` fed through `phase.transitionCurve`
   - Apply variability: seeded RNG from `floor(serverDayNumber)` → hueShift, satVar, valVar
2. Implement sun control:
   - `sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection)`
   - `sunLight.color = ApplyVariability(phase.sunColor, phaseIdx)`
   - `sunLight.intensity = ApplyVariability(phase.sunIntensity, phaseIdx) * stormMod`
3. Implement ambient control:
   - `RenderSettings.ambientSkyColor = ApplyVariability(phase.ambientSkyColor, phaseIdx)`
   - `RenderSettings.ambientEquatorColor = phase.ambientEquatorColor`
   - `RenderSettings.ambientGroundColor = phase.ambientGroundColor`
   - `RenderSettings.ambientIntensity = phase.ambientIntensity`
4. Implement fog control:
   - `RenderSettings.fogColor = phase.fogColor`
   - `RenderSettings.fogDensity = phase.fogDensity`
5. Implement temperature filter:
   - Call `TemperatureFilter.GetTemperatureOverlay(temp)` each frame
   - Apply as post-process color adjustment (e.g., via `CommandBuffer` or volume)
6. Call `veilController.SetLightDirection(sunDir)` + `veilController.SetDayNightFactor(dayFactor)`

**Agents to use**:
- `unity-specialist` — URP Volume blending, RenderSettings API
- `performance-analyst` — review Update() cost, confirm no per-frame allocations

**Unity MCP verification**:
- Create scene object with `DayNightController` via `unityMCP_manage_gameobject`
- Add `Light` component via `unityMCP_manage_components`
- Verify scene hierarchy after adding component

**Testing**:
- Manually set `_timeOfDay = 6f` in Inspector → dawn colors apply
- Manually set `_timeOfDay = 13f` → midday
- Manually set `_timeOfDay = 20f` → twilight/night
- Temperature filter visible at extremes (temp ≤0°C and ≥25°C)
- Storm intensity darkens scene

---

### SESSION 4: URP Volume + Skybox

**Goal**: Day/night post-processing profiles + skybox materials with sun direction binding.

**Read first**:
- `docs/world/Day-Night/SPEC.md` (§6.4)

**Do**:
1. Create `Assets/_Project/Volumes/DayNight/DayVolumeProfile.asset`
   - Add Bloom (threshold 0.8, intensity 0.3)
   - Add Vignette (intensity 0.2)
   - Add ColorAdjustments (exposure 0, saturation 0)
2. Create `Assets/_Project/Volumes/DayNight/NightVolumeProfile.asset`
   - Add Bloom (threshold 0.6, intensity 0.8)
   - Add Vignette (intensity 0.4)
   - Add ColorAdjustments (exposure -0.3, saturation -10)
3. In `DayNightController`, add volume blend:
   - `Volume.profile` = blended VolumeProfile OR use `Volume.weight`
   - Lerp weights between day/night based on phase blend
4. Create `Assets/_Project/Materials/Skybox/Skybox_Day.mat`
   - Use `Skybox/Procedural`
   - Tint: `rgb(0.9, 0.95, 1.0)`, exposure 1.0
5. Create `Assets/_Project/Materials/Skybox/Skybox_Night.mat`
   - Use `Skybox/Procedural`
   - Tint: `rgb(0.4, 0.4, 0.8)`, exposure 0.3
6. Set `RenderSettings.skybox` via `DayNightController` based on phase

**Agents to use**:
- `unity-specialist` — URP Volume profile setup, skybox material properties
- `unity-shader-specialist` — if custom skybox blending shader needed

**Unity MCP verification**:
- `unityMCP_manage_asset(action="create", asset_type="VolumeProfile")` to create profiles
- `unityMCP_manage_asset(action="create", asset_type="Material")` to create skybox materials
- Verify with `unityMCP_manage_asset(action="get_info")`

**Testing**:
- Day volume profile active during day phases → bloom visible
- Night volume profile active during night → stronger vignette + desaturated
- Skybox changes at phase boundaries

---

### SESSION 5: Moon Mesh + Lunar Phases

**Goal**: Physical moon with full ~29.5 day phase cycle.

**Read first**:
- `docs/world/Day-Night/SPEC.md` §7
- `docs/world/Day-Night/REQUIREMENTS.md` (§7, lunar phases answer)

**Do**:
1. Create `MoonController.cs`:
   - Field `moonPhase` (0.0–1.0, where 0.5 = full moon)
   - Field `moonOrbitRadius`, `moonOrbitSpeed`
   - `UpdateMoon()`:
     - `moonPhase = (timeOfDay / 24f) * phaseSpeed` OR separate counter
     - `moonPosition` = opposite of sun direction (180° offset)
     - `moonLight.transform.rotation` = moonlight direction
   - `moonMeshRenderer.material` — shader-based phase texture OR blended between textures
2. Create or assign moon sphere mesh
3. Create moon material with phase shader (or 2-texture blend approach)
4. Connect `moonPhase` to `_DayFactor` on veil shader (optional: veil less visible under bright moon)
5. Moon visible only during night phases (21:00–5:00), fade during twilight

**Shader approach for lunar phases** (two options):
- **Option A**: Blend between "full moon texture" and "new moon texture" based on `moonPhase`
- **Option B**: Single shader calculates lit fraction = `abs(sin(moonPhase * PI))`, colors lit portion

**Agents to use**:
- `art-director` — moon texture/color reference
- `unity-shader-specialist` — phase shader implementation

**Unity MCP verification**:
- Create sphere mesh via `unityMCP_manage_gameobject(action="create", primitive_type="Sphere")`
- Add material via `unityMCP_manage_material`
- Set position via `unityMCP_manage_gameobject`

**Testing**:
- Advance time quickly → moon phase cycles through all phases
- Full moon (phase=0.5) shows full illuminated face
- Moon absent/weak during daytime

---

### SESSION 6: Constellation System

**Goal**: 12 constellations with star field rendering.

**Read first**:
- `docs/world/Day-Night/REQUIREMENTS.md` (§7, constellations table)
- `docs/world/Day-Night/SPEC.md` §8

**Do**:
1. Populate `ConstellationData.cs` with 12 constellations:
   - Each has `name`, `stars[]` (azimuth/altitude positions), `lines[]` (pairs of star indices to connect)
   - Positions based on real sky (approximate for game world)
2. Create `Stars.shader`:
   - Point sprites with size variation
   - Twinkle: `sin(time * phaseOffset + starIndex)` per star
   - Alpha fade based on phase (night → full, twilight → fade)
   - Color: white with slight temperature variation (cooler stars = blue tints)
3. Create `ConstellationController.cs`:
   - On `Enable()`: generate star mesh from `ConstellationData`
   - Draw connecting lines (LineRenderer or mesh lines)
   - Name labels for each constellation (optional, future UI)
   - Fade stars based on `GetStarVisibility(timeOfDay)`
4. Star visibility by phase:
   ```
   Night (21:00-5:00):    100% (moon may dim to 70%)
   Twilight (19:30-21:00): 0→100% over 90min
   Morning (5:00-8:00):   100→0% over 180min
   ```
5. Future-proof: add `IsNavigable` flag per constellation for sextant skill

**Agents to use**:
- `art-director` — star colors/sizes, constellation line aesthetics
- `game-designer` — review constellation positions for gameplay fairness
- `unity-shader-specialist` — Stars.shader with twinkle

**Unity MCP verification**:
- Use `unityMCP_manage_asset(action="create", asset_type="Shader")` to create Stars.shader skeleton
- Scene verification via hierarchy

**Testing**:
- At midnight, all 12 constellations visible
- At noon, no stars visible
- During twilight, stars fade in/out smoothly
- Star names can be toggled (future UI)

---

### SESSION 7: Storm Integration

**Goal**: Storms darken lighting per SPEC.md §3.

**Read first**:
- `docs/world/Day-Night/REQUIREMENTS.md` §3
- `ServerStormManager.cs` (existing)

**Do**:
1. Open `ServerStormManager.cs`
2. Add `public event System.Action<float> OnStormIntensityChanged;`
3. Add field `_currentStormIntensity` (0–1)
4. When storm spawns: `_currentStormIntensity = 1f`; fire event
5. When storm dissipates: lerp `_currentStormIntensity → 0f` over 30s; fire event
6. Open `DayNightController.cs`
7. Subscribe to `ServerStormManager.OnStormIntensityChanged`
8. Add `SetStormIntensity(float intensity)`:
   - `stormIntensity = intensity`
   - Apply modifiers:
     ```
     ambientIntensity *= Lerp(1f, 0.6f, stormIntensity)
     fogDensity *= Lerp(1f, 2.0f, stormIntensity)
     sunIntensity *= Lerp(1f, 0.5f, stormIntensity)
     fogColor = Lerp(phaseFogColor, gray-purple, stormIntensity)
     ```

**Agents to use**:
- `game-designer` — validate storm intensity values
- `unity-specialist` — verify event subscription pattern

**Unity MCP verification**:
- Read `ServerStormManager.cs` to confirm event integration points

**Testing**:
- Trigger storm → ambient darkens, fog thickens
- Storm ends → smooth 30s recovery to normal lighting
- Multiple storms don't stack beyond 1.0 intensity

---

### SESSION 8: Integration + Scene Setup

**Goal**: Wire everything into BootstrapScene, verify full pipeline.

**Read first**:
- All docs in `docs/world/Day-Night/`

**Do**:
1. Ensure `ServerWeatherController` has TimeOfDay + Temperature fields (Session 2)
2. Place `DayNightController` on scene (e.g., `WorldRoot` or dedicated `DayNight` object)
3. Assign references:
   - `profile` = DayNightProfile asset
   - `sunLight` = Directional Light "Sun"
   - `globalVolume` = Scene Volume
   - `dayVolumeProfile` / `nightVolumeProfile` = assets
   - `temperatureConfig` = TemperatureFilterConfig asset
   - `veilController` = VeilRaymarchMeshController in scene
4. Verify Sun Directional Light exists in scene (create if missing)
5. Verify Moon GameObject exists with `MoonController`
6. Verify Constellation GameObject exists with `ConstellationController`
7. Verify Global Volume exists in scene with `Volume` component
8. Test server time sync with 5 clients (Build or Play mode with multiple instances)

**Agents to use**:
- `unity-specialist` — scene hierarchy, GameObject setup
- `qa-lead` — test plan for multi-client sync

**Unity MCP verification**:
- `unityMCP_manage_scene(action="get_hierarchy")` to see full scene
- `unityMCP_manage_components(action="add")` to add DayNightController to existing object
- Add directional light via `unityMCP_manage_gameobject(action="create")` + `unityMCP_manage_components`

**Testing checklist**:
- [ ] Server sets time → all clients update within 5s
- [ ] All 5 phases visually distinct
- [ ] Each dawn/dusk feels unique (variability)
- [ ] Temperature filter active at thresholds
- [ ] Moon visible at night, phase correct
- [ ] 12 constellations visible at midnight
- [ ] Storm darkens scene
- [ ] No hardcoded values (check all values come from ScriptableObjects)
- [ ] Performance: frame time increase < 2ms

---

### SESSION 9: Polish + Performance + Edge Cases

**Goal**: Final polish, performance validation, edge cases.

**Do**:
1. Performance profiling:
   - `unityMCP_manage_profiler(action="profiler_start")` — record frame data
   - Check `unityMCP_manage_profiler(action="stats_get")` for draw calls
   - Target: < 2ms additional frame time for day-night system
2. Edge cases:
   - Server disconnect → clients hold last known time
   - Rapid time changes (GM commands) → smooth transition, no pop
   - Midnight crossing (23:59 → 00:00) → no glitches
   - Temperature boundary crossing (0°C, 15°C, 25°C) → smooth blend
3. Tuning:
   - Adjust variability ranges if sunrises too similar
   - Fine-tune fog densities per biome (via BiomeProfile integration?)
   - Tune bloom thresholds for visual clarity
4. Documentation:
   - Finalize all phase profile docs (`docs/world/Day-Night/PROFILE_*.md`)
   - Update `PLAN.md` with completed items checked off
   - Create `SETTINGS_GUIDE.md` for server operators

**Agents to use**:
- `performance-analyst` — profiling and optimization
- `technical-artist` — visual quality pass
- `unity-specialist` — edge case review

**Testing**:
- Full day cycle (24h @ 30s = 30s real time)
- Temperature extremes
- Stress test with 10 rapid GM time commands
- Profile session with > 0.5ms day-night cost → optimize

---

## Agent Usage Reference

| When | Use Agent |
|------|-----------|
| Creating new C# scripts (design, stubs) | `game-designer` |
| Network code (RPCs, sync) | `network-programmer` |
| Unity scene setup, GameObjects | `unity-specialist` |
| URP Volume, post-processing | `unity-specialist` |
| Shader authoring/modification | `unity-shader-specialist` |
| Visual quality, colors, aesthetics | `art-director` |
| Performance profiling, optimization | `performance-analyst` |
| Testing plans, QA | `qa-lead` |
| Complex exploration (find files, patterns) | `explore` |

## Unity MCP Usage Reference

| When | Use Tool |
|------|---------|
| Create folder | `unityMCP_manage_asset(action="create_folder")` |
| Validate C# script | `unityMCP_validate_script` |
| Check Unity API via reflection | `unityMCP_unity_reflect` |
| Get doc for Unity API | `unityMCP_unity_docs` |
| Create GameObject | `unityMCP_manage_gameobject(action="create")` |
| Add component | `unityMCP_manage_components(action="add")` |
| Set component property | `unityMCP_manage_components(action="set_property")` |
| Create asset (profile, material) | `unityMCP_manage_asset(action="create")` |
| Get scene hierarchy | `unityMCP_manage_scene(action="get_hierarchy")` |
| Create Volume | `unityMCP_manage_graphics(action="volume_create")` |
| Screenshot for verification | `unityMCP_manage_camera(action="screenshot", include_image=true)` |

## Rules

1. **Always read all docs in `docs/world/Day-Night/` before any session** — do not rely on memory
2. **Do not hardcode** — every magic number must come from ScriptableObject fields or be declared as configurable
3. **Test each session** — do not stack 3+ sessions of untested code before verification
4. **Use correct agents** — do not ask `art-director` to review networking code
5. **Use Unity MCP for verification** — validate scripts compile, verify scene hierarchy
6. **Mark items complete in PLAN.md** after each session
7. **Document any deviation** from this master prompt in session summary doc