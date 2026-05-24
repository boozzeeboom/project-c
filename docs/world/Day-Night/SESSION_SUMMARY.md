# Day-Night Cycle — Session Summary

## Analysis Complete ✅

### Old Integration Status

| Component | Status |
|-----------|--------|
| `CloudSystem.cs` `enableDayNightCycle` | **Already disabled** (`= 0` in prefab). Old sun/color logic at lines 173–246 will NOT run. No action needed. |
| `VeilRaymarchMesh.shader` line 295 | **Hardcoded `_LightDir`** — needs fix: replace `normalize(half3(-0.5, 0.5, -0.3))` with `normalize(_LightDir.xyz)`. This is the ONLY hardcoded light direction in any shader. |
| `VeilRaymarchMeshController.cs` | Does NOT push `_LightDir` to shader — needs `SetLightDir()` method added. |

### What's Active Now

| System | Status |
|--------|--------|
| `CloudManager` + `NearCloudRenderer` | Active (cloud geometry, NOT day-night lighting) |
| `ServerWeatherController` | Active (wind only) |
| `WindManager` | Active (singleton) |
| `VeilRaymarchMesh` | Active (poison fog) — but `_DayFactor = 0.5` static, not driven |
| Directional "Sun" light | **Missing from scene** — created only via `ProjectCSceneSetup.cs` tool |

### What's NOT Active

- No day-night lighting (sun/moon/ambient/skybox)
- No TimeOfDay synchronization
- No temperature-based post-filter
- No moon mesh
- No constellations

---

## Requirements Locked

1. **Moon mesh** — physical moon geometry in sky
2. **Constellations** — named star patterns for navigation (procedural)
3. **Time cycle speed** — server-configurable, default 24h / 30s
4. **Storm → darker lighting** — storms affect ambient/fog/sun intensity

---

## Documentation Created

| Doc | Path |
|-----|------|
| Main spec | `docs/world/Day-Night/SPEC.md` |
| Implementation plan | `docs/world/Day-Night/PLAN.md` |
| Requirements | `docs/world/Day-Night/REQUIREMENTS.md` |
| Shader fix notes | `docs/world/Day-Night/SHADER_FIX.md` |

---

## Implementation Order (Next Session)

### Step 1: Infrastructure
- [ ] Create folder `Scripts/Core/DayNight/`
- [ ] Create `TimeOfDayPhase.cs` (ScriptableObject)
- [ ] Create `DayNightProfile.cs` (ScriptableObject)
- [ ] Create `TemperatureFilterConfig.cs` (ScriptableObject)
- [ ] Create `TemperatureFilter.cs` (component)

### Step 2: Server Authority
- [ ] Extend `ServerWeatherController` with TOD + temperature fields + RPCs
- [ ] Add `SetTimeOfDayServerRpc` + `SetTemperatureServerRpc` handlers
- [ ] Add `BroadcastTimeOfDayClientRpc` + `BroadcastTemperatureClientRpc`

### Step 3: Client Lighting Controller
- [ ] Create `DayNightController.cs`
- [ ] Phase detection + interpolation with variability
- [ ] Sun directional light control
- [ ] Ambient light control
- [ ] Fog control
- [ ] URP Volume blend (day/night profiles)

### Step 4: Skybox + Moon + Stars
- [ ] Create `Skybox_Day.mat` + `Skybox_Night.mat`
- [ ] Create `MoonController.cs` (moon mesh + orbit)
- [ ] Create `ConstellationData.cs` (SO with star patterns)
- [ ] Create `ConstellationController.cs` (render stars + lines)
- [ ] Create `Stars.shader` (point stars with twinkle)

### Step 5: Shader Integration
- [ ] Fix `VeilRaymarchMesh.shader` line 295: use `_LightDir` uniform
- [ ] Add `SetDayNight()` to `VeilRaymarchMeshController`
- [ ] Call `SetDayNight()` from `DayNightController`

### Step 6: Storm Integration
- [ ] Add `OnStormIntensityChanged` event to `ServerStormManager`
- [ ] Subscribe `DayNightController` to storm intensity
- [ ] Apply darker ambient + denser fog during storms

### Step 7: Verification
- [ ] Verify server→client TOD sync
- [ ] Verify all 5 phases visually
- [ ] Verify temperature filter at thresholds
- [ ] Verify constellation visibility by phase
- [ ] Performance profiling

---

## Open Questions (Action Needed)

| # | Question |
|---|----------|
| 1 | **Lunar phases?** — Full cycle (new→full→new) or always full moon? |
| 2 | **Star click interaction?** — Players click stars for navigation info? |
| 3 | **Zone-based eternal day/night** — Separate biome zones with permanent twilight etc? |
| 4 | **How many constellations?** — 5 minimum, recommend 8-12 |