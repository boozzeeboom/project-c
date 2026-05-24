# Day-Night Cycle System — Technical Specification

## 1. Status & Context

| Field | Value |
|-------|-------|
| **Doc path** | `docs/world/Day-Night/` |
| **Version** | 0.1 Draft |
| **Author** | AI (Claude) |
| **Date** | 2026-05-24 |
| **Status** | Draft — pending implementation |

---

## 2. Goal

Build a server-authoritative, networked day-night cycle with:

- **5 distinct phases**: Утро (Morning) → Полдень (Midday) → Вечер (Evening) → Сумерки (Twilight) → Ночь (Night)
- **Variability**: each phase instance has randomized color/shadow variations so no two sunrises feel the same
- **Temperature post-filter**: temperature overlays additional color grading
- **Full configurability**: no hardcoded magic numbers
- **Server controls**: server sets time-of-day and temperature; all clients render accordingly

---

## 3. Existing Systems

### 3.1 Old Integration — TO DISABLE

| File | What to Disable |
|------|-----------------|
| `Assets/_Project/Scripts/Core/CloudSystem.cs` | `enableDayNightCycle = false` (already false by default). Lines 173–246 contain old sun-position/color logic. **Do not use this** for the new system. |
| `Assets/_Project/Shaders/VeilRaymarchMesh.shader` | Hardcoded `_LightDir = (0.5, -0.5, 0.3, 0)`, `_DayFactor = 0.5` set once and never updated. Will be replaced by the new `DayNightController` feeding `_LightDir` and `_DayFactor` dynamically. |

### 3.2 Keep (Extend)

| File | Role |
|------|------|
| `ServerWeatherController.cs` | **Extend with temperature + time-of-day**. Becomes the single server authority for weather + TOD. |
| `WindManager.cs` | Continue using for wind state. No changes needed. |

### 3.3 CloudSystem — Clarification

`CloudSystem.cs` manages cloud **geometry** (layers, cumulonimbus). Its `enableDayNightCycle` is **already off by default** and its day-night code (lines 173–246) is **standalone (no networking)**. The new day-night system should **not** use `CloudSystem` for lighting/time. CloudSystem's cloud geometry stays; its day-night code is abandoned.

---

## 4. New Architecture

```
Server                                          Clients
┌─────────────────────┐                    ┌──────────────────────┐
│ ServerWeatherController│                 │ DayNightController   │
│  ├─ Wind (existing) │                    │  ├─ Directional Light │
│  ├─ TimeOfDay (new)  │── ClientRpc ──────│  ├─ Ambient Light      │
│  └─ Temperature (new)│                    │  ├─ Skybox Materials  │
└─────────────────────┘                    │  ├─ URP Volume        │
                                            │  ├─ Fog               │
                                            │  └─ TemperatureFilter │
                                            └──────────────────────┘
```

### 4.1 Server Side

**`ServerWeatherController`** — add fields:

```csharp
[Header("Time of Day")]
public float timeOfDay = 12f;            // 0–24 hours, server sets
public float dayCycleHours = 24f;        // real-time hours per full day
public bool syncTimeOfDay = true;        // broadcast TOD

[Header("Temperature")]
public float temperature = 20f;         // Celsius, server sets
public bool syncTemperature = true;
```

New RPCs:
- `BroadcastTimeOfDayClientRpc(float time, float speed)` — broadcast TOD + optional auto-advance speed
- `BroadcastTemperatureClientRpc(float temperature)`

### 4.2 Client Side

**`DayNightController.cs** (new component on scene root or Sun object):

| Property | Type | Description |
|----------|------|-------------|
| `PhaseDefinitions` | `TimeOfDayPhase[]` | Array of 5 phase configs (see §5) |
| `SunLight` | Light | Directional light |
| `MoonLight` | Light | Night directional light |
| `SkyboxMaterialDay` | Material | Skybox for day |
| `SkyboxMaterialNight` | Material | Skybox for night |
| `DayVolumeProfile` | VolumeProfile | URP volume for day |
| `NightVolumeProfile` | VolumeProfile | URP volume for night |
| `GlobalVolume` | Volume | Scene Volume component |
| `TemperatureFilter` | `TemperatureColorFilter` | Post-filter based on temperature |

**`TemperatureColorFilter.cs`** (new component):

| Property | Type | Description |
|----------|------|-------------|
| `ColdColor` | Color | Overlay for freezing temp |
| `HotColor` | Color | Overlay for hot temp |
| `ColdThreshold` | float | Below this → cold tint |
| `HotThreshold` | float | Above this → hot tint |
| `BlendCurve` | AnimationCurve | How strongly temperature affects color |

---

## 5. Time Phases

| # | Name (RU) | Name (EN) | Hours | Description |
|---|-----------|----------|-------|-------------|
| 0 | Утро | Morning | 5:00–8:00 | Sun rises, warm orange-pink light |
| 1 | Полдень | Midday | 8:00–17:00 | Sun at peak, neutral white |
| 2 | Вечер | Evening | 17:00–19:30 | Sun lowers, warm amber |
| 3 | Сумерки | Twilight | 19:30–21:00 | Deep blue-purple, afterglow |
| 4 | Ночь | Night | 21:00–5:00 | Moon + stars, deep navy |

### 5.1 Variability Within Phases

Each phase has **3 randomization seeds** controlled by a master seed per cycle:

| Parameter | Range | Description |
|-----------|-------|-------------|
| `hueShift` | ±0.05 | Slight hue rotation |
| `saturationVariance` | 0.8–1.2 | Saturation multiplier |
| `valueVariance` | 0.9–1.1 | Brightness multiplier |
| `shadowHue` | ±0.03 | Shadow color hue offset |
| `ambientIntensityVariance` | 0.85–1.15 | Ambient light multiplier |

**Randomization is deterministic per "day"**: seed is derived from `floor(serverDayNumber)` so all clients see the same variation for the same day.

### 5.2 Phase Transition Curve

Use `AnimationCurve` per phase for smooth interpolation between phases. Default:

```
Morning:  ease-in-out  curve rising
Midday:   flat 1.0
Evening:  ease-in-out  curve falling
Twilight: ease-in-out  curve falling (faster)
Night:    flat 0.0 then ease-in at dawn
```

---

## 6. Lighting Components Per Phase

### 6.1 Sun Light (Directional Light)

| Phase | Color | Intensity | Shadow softness |
|-------|-------|----------|-----------------|
| Morning | `rgb(255, 180, 120)` warm orange | 0.6→1.0 | Soft |
| Midday | `rgb(255, 250, 240)` near-white | 1.0 | Hard |
| Evening | `rgb(255, 140, 80)` amber | 1.0→0.5 | Soft |
| Twilight | `rgb(80, 60, 120)` deep blue | 0.3 | Soft |
| Night | Off or `rgb(40, 50, 80)` moon-blue | 0.15 | No shadows |

### 6.2 Ambient Light (RenderSettings.ambient)

| Phase | Sky Color | Equator Color | Ground Color | Intensity |
|-------|---------|---------------|-------------|-----------|
| Morning | `rgb(180, 160, 200)` lavender | `rgb(255, 200, 150)` | `rgb(100, 80, 60)` | 0.4 |
| Midday | `rgb(140, 170, 210)` blue-sky | `rgb(180, 200, 220)` | `rgb(80, 90, 100)` | 0.6 |
| Evening | `rgb(200, 130, 100)` orange-sky | `rgb(200, 150, 100)` | `rgb(60, 40, 30)` | 0.45 |
| Twilight | `rgb(40, 30, 80)` dark purple | `rgb(80, 60, 100)` | `rgb(20, 15, 30)` | 0.25 |
| Night | `rgb(10, 10, 30)` near-black | `rgb(30, 30, 60)` | `rgb(10, 10, 20)` | 0.1 |

### 6.3 Skybox

Use **2 Skybox materials** that blend via `SkyboxBlending.shader` or use `ProceduralSkybox` with sun direction linked:

- **Day material**: `Skybox/Procedural` with `Sun Disk = 1.0`, warm tint `rgb(0.9, 0.95, 1.0)`
- **Night material**: same procedural skybox with `Exposure = 0.3`, cold tint `rgb(0.4, 0.4, 0.8)`

Blend factor = `GetPhaseBlendTime()` (0=full day, 1=full night).

### 6.4 URP Volume — Post-processing

| Effect | Day Value | Night Value |
|--------|----------|-------------|
| Bloom | threshold 0.8, intensity 0.3 | threshold 0.6, intensity 0.8 |
| Vignette | centered, intensity 0.2 | offset dark edges, intensity 0.4 |
| Color Adjustments > Exposure | 0 | -0.3 |
| Color Adjustments > Saturation | 0 | -10 |

Use `VolumeProfile` blend via `weights` on Volume component.

### 6.5 Fog

| Phase | Fog Color | Fog Density | Mode |
|-------|----------|-------------|------|
| Morning | `rgb(200, 180, 160)` warm gray | 0.0005 | Exponential |
| Midday | `rgb(180, 200, 220)` cool gray | 0.0002 | Exponential |
| Evening | `rgb(180, 120, 80)` warm orange | 0.0008 | Exponential |
| Twilight | `rgb(40, 30, 80)` dark purple | 0.001 | Exponential |
| Night | `rgb(10, 10, 20)` near-black | 0.0003 | Exponential |

### 6.6 Temperature Post-Filter

Temperature modifies **final color output** via `TemperatureFilter`:

```
blendFactor = EvaluateTemperature(temperature)  // 0=cold, 1=hot
finalColor = Lerp(dynamicColor, Lerp(dynamicColor, hotFilter, blendFactor), temperatureInfluence)
```

| Temperature | Filter Effect |
|-------------|---------------|
| ≤0°C | Deep blue overlay +0.1 saturation + slight darkening |
| 0–15°C | Neutral, no filter |
| 15–25°C | Warm yellow tint (+5% value) |
| ≥25°C | Orange-red overlay + increased bloom intensity |

---

## 7. Moon Mesh

Moon is a **physical 3D mesh** visible in the night sky:

- **Geometry**: Sphere, radius ~Planet radius × 0.27 (like Earth's moon)
- **Orbit**: Opposite to sun. Moon rises when sun sets.
- **Lunar Phases**: Full cycle (~29.5 days synodic month):
  - 0.0 → new moon (fully dark)
  - 0.25 → first quarter (right half lit)
  - 0.5 → full moon (fully lit)
  - 0.75 → last quarter (left half lit)

**Implementation**: MoonController drives a sphere mesh with a texture/shader that shows illuminated fraction based on phase. Night phases (21:00–5:00) use `moonPhase` to determine visibility and texture.

---

## 8. Constellations

**12 constellations** with deterministic star placement (real sky reference). Two hemispheres, gameplay navigation function reserved for future (sextant + navigation skill).

Each constellation: name, array of star positions (azimuth/altitude), connecting lines for pattern recognition.

| Visibility | Stars |
|------------|-------|
| Night 100% | Full star visibility |
| Moonlit night | Stars dim 30% |
| Twilight fade | 70% at start, 0% at end |

---

## 7. Server → Client Data Flow

### 7.1 Server Weather Controller Extended

```csharp
// New fields
public float timeOfDay = 12f;
public float dayCycleHours = 24f;      // how many real hours = 24 game hours
public float temperature = 20f;
public bool enableTimeAutoAdvance = true;

private float _timeAccumulator = 0f;

// In Update():
if (enableTimeAutoAdvance && IsServer) {
    float gameHoursPerRealSecond = 24f / (dayCycleHours * 3600f);
    timeOfDay += gameHoursPerRealSecond * Time.deltaTime;
    if (timeOfDay >= 24f) timeOfDay -= 24f;
}
```

### 7.2 RPCs

| RPC | Params | When |
|-----|--------|------|
| `BroadcastTimeOfDayClientRpc(float time)` | time 0–24 | Every 5 seconds OR on manual set |
| `BroadcastTemperatureClientRpc(float temp)` | temp °C | Every 10 seconds OR on manual set |
| `SetTimeOfDayServerRpc(float time)` | time 0–24 | Client→Server request (GM tool) |
| `SetTemperatureServerRpc(float temp)` | temp °C | Client→Server request (GM tool) |

### 7.3 Client DayNightController

```csharp
private float _serverTimeOfDay;
private float _serverTemperature;

// On RPC received:
public void ApplyTimeOfDay(float time) {
    _serverTimeOfDay = time;
    UpdateAllLighting();
}

public void ApplyTemperature(float temp) {
    _serverTemperature = temp;
    UpdateTemperatureFilter();
}
```

---

## 8. Configuration Data

### 8.1 TimeOfDayPhase ScriptableObject

```csharp
public class TimeOfDayPhase : ScriptableObject
{
    [Header("Identity")]
    public string phaseName;
    public float startHour;      // 0–24
    public float endHour;        // 0–24

    [Header("Sun")]
    public Color sunColor;
    public float sunIntensity;
    public float sunTemperature; // correlated color temperature (K)
    public bool castShadows;

    [Header("Ambient")]
    public Color ambientSkyColor;
    public Color ambientEquatorColor;
    public Color ambientGroundColor;
    public float ambientIntensity;

    [Header("Skybox")]
    public Gradient skyHorizonGradient;  // horizon color by sun height
    public float skyboxExposure;
    public Color skyboxTint;

    [Header("Fog")]
    public Color fogColor;
    public float fogDensity;

    [Header("Variability")]
    public Vector2 hueShiftRange = new Vector2(-0.05f, 0.05f);
    public Vector2 saturationRange = new Vector2(0.8f, 1.2f);
    public Vector2 intensityRange = new Vector2(0.85f, 1.15f);

    [Header("Transition")]
    public AnimationCurve transitionCurve = AnimationCurve.Linear(0,0,1,1);
}
```

### 8.2 DayNightProfile ScriptableObject

Contains array of `TimeOfDayPhase` (5 entries: Morning, Midday, Evening, Twilight, Night).

### 8.3 TemperatureFilterConfig ScriptableObject

```csharp
public class TemperatureFilterConfig : ScriptableObject
{
    [Header("Cold")]
    public float coldThreshold = 0f;       // Celsius
    public Color coldOverlayColor;
    public float coldSaturationBoost;
    public float coldValueOffset;

    [Header("Hot")]
    public float hotThreshold = 25f;
    public Color hotOverlayColor;
    public float hotSaturationBoost;
    public float hotValueOffset;

    [Header("Curve")]
    public AnimationCurve blendCurve = AnimationCurve.Linear(0,0,1,1);
}
```

---

## 9. Component Inventory

| Component | File | Purpose |
|-----------|------|---------|
| `ServerWeatherController.cs` | `Scripts/Core/` | Extend: add TOD + temperature |
| `DayNightController.cs` | `Scripts/Core/` | New: main client-side lighting controller |
| `TemperatureFilter.cs` | `Scripts/Core/` | New: temperature-based color post-filter |
| `TimeOfDayPhase.cs` | `Scripts/Core/` | New: ScriptableObject for phase definition |
| `DayNightProfile.cs` | `Scripts/Core/` | New: ScriptableObject containing 5 phases |
| `TemperatureFilterConfig.cs` | `Scripts/Core/` | New: ScriptableObject for temp filter config |

---

## 10. Implementation Phases

### Phase 1: Infrastructure (this session — docs only)
- [ ] Write this spec
- [ ] Confirm all agents agree on architecture
- [ ] Create ScriptableObject stubs
- [ ] Disable old CloudSystem day-night (set `enableDayNightCycle = false` explicitly in prefab)

### Phase 2: Server Authority (next session)
- [ ] Extend `ServerWeatherController` with TOD + temperature
- [ ] Add ClientRpc for time and temperature
- [ ] Add server RPC handlers for GM tools

### Phase 3: Client Lighting Controller
- [ ] Create `DayNightController.cs` component
- [ ] Implement phase detection and interpolation
- [ ] Implement variability (deterministic per day-seed)
- [ ] Control sun directional light (position, color, intensity, shadows)
- [ ] Control `RenderSettings.ambient`
- [ ] Control fog

### Phase 4: URP Volume + Skybox
- [ ] Create 2 VolumeProfiles (day/night)
- [ ] Blend between profiles based on phase
- [ ] Set up skybox materials with sun direction binding
- [ ] Implement skybox cross-fade

### Phase 5: Temperature Post-Filter
- [ ] Create `TemperatureFilter.cs`
- [ ] Create `TemperatureFilterConfig.cs`
- [ ] Connect temperature RPC to filter

### Phase 6: VeilShader integration
- [ ] Update `VeilRaymarchMeshController` to receive `_DayFactor` and `_LightDir` from `DayNightController`
- [ ] Verify `_LightDir` is no longer hardcoded

### Phase 7: Testing & Tuning
- [ ] Test all 5 phases visually
- [ ] Test temperature filter at thresholds
- [ ] Verify networking (server sets time, all clients update)
- [ ] Performance test

---

## 11. Open Questions / Clarifications Needed

1. **Day cycle duration**: Should 24 game-hours = 1 real-time hour? Or configurable per server?
2. **Manual time control**: Should GMs be able to set time via command?
3. **Moon**: Is moon visual (geo mesh) required, or just directional "moonlight" light sufficient?
4. **Stars**: Should we generate star field procedurally or use skybox?
5. **Storm integration**: When storm triggers, does it affect time-of-day lighting (darker during storm)?
6. **Biome-based profiles**: Should different biomes have different phase color profiles?
7. **Existing Skybox shader**: Is `Skybox/Procedural` sufficient or do we need custom skybox shader for blending?