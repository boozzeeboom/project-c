# Day-Night Cycle — Clarifications & Requirements

| Field | Value |
|-------|-------|
| **Doc path** | `docs/world/Day-Night/` |
| **Date** | 2026-05-24 |
| **Status** | Updated with user requirements |

---

## User Answers Summary

| Question | Answer |
|----------|--------|
| Moon | **Moon mesh** — 3D procedural geometry |
| Stars | **Constellations for navigation** — procedural with named star patterns for flight navigation |
| Day cycle duration | **Server-configurable** — default 24h in 30s for testing, adaptive for game |
| Storm effect on lighting | **Yes** — storms make ambient darker + fog denser |

---

## 1. Moon Mesh

Moon is a physical object in the sky that players can see. Implementation approach:

### 1.1 Geometry
- Sphere or low-poly moon mesh at fixed sky position (above horizon at night)
- Moon orbits opposite to sun: rises when sun sets
- Crescent or full moon phases? (To clarify: do we need phase cycling?)

### 1.2 Material
- PBR material with:
  - Albedo: pale gray-white `rgb(220, 220, 210)`
  - Normal map: crater detail (procedural or texture)
  - Emissive: moon is NOT emissive (reflects sun), but moonlit side receives light from sun direction

### 1.3 Integration
- `DayNightController` controls moon `GameObject` visibility (on during night phases)
- Moon `Transform` rotates based on `_serverTimeOfDay`
- When sun is below horizon, moon light activates

### 1.4 Open Question
> Should we implement **lunar phases** (new moon → full moon cycle)? If yes, add `moonPhase` field (0–1) that controls illuminated percentage.

---

## 2. Constellations (Star Navigation)

Stars serve a **gameplay function**: players navigate by constellations for long-range flights.

### 2.1 Requirements
- **Procedural star field** with deterministic placement per night
- **Named constellations** (minimum: 5-8 recognizable patterns)
- **Star visibility** changes with phase:
  - Twilight: faint stars begin appearing
  - Night: full star visibility
  - Moonlit night: stars dimmer (moon washes them out)

### 2.2 Implementation Approaches

| Approach | Pros | Cons |
|----------|------|------|
| **Skybox shader** | Best performance, always in view | Hard to make interactive/clickable |
| **Particle system** | Easy to animate twinkle | Too many particles expensive |
| **Mesh stars** | Precise constellation shapes | Memory for geometry |
| **Custom stars shader** | Best quality + twinkle | Complex shader |

**Recommendation**: Custom `Stars.shader` that draws point stars with:
- Size variation (0.5–2.0 px)
- Twinkle animation (per-star phase offset)
- Constellation lines drawn as mesh or line renderer
- Named star groups for navigation

### 2.3 Constellation List (Initial)

| Name | Stars | Navigation Use |
|------|-------|---------------|
| Polaris (Полярная) | 3 main + 5 minor | North direction |
| Orion (Орион) | 7 main belt + limbs | Equator reference |
| Cassiopeia (Кассиопея) | 5 in W shape | Latitude marker |
| Ursa Major (Большая Медведица) | 7 + 2 pointers | Northwest direction |
| Southern Cross (Южный Крест) | 4 + 2 pointers | South direction |
| Dragon (Дракон) | 8 winding | Mid-latitude navigation |
| Swallow (Ласточка) | 5 | Seasonal marker |

### 2.4 Star Visibility by Phase

```
Night (21:00-5:00):
  - Stars fully visible
  - Moon: full → stars dim 30%
  - Moon: new → stars at 100%

Twilight (19:30-21:00):
  - Stars fade in (0% → 100% over 90 min)
  - Last 15 min: stars at ~70%

Morning (5:00-8:00):
  - Stars fade out (100% → 0% over 90 min)
```

---

## 3. Storm → Lighting Integration

When `ServerStormManager` triggers a storm event, `DayNightController` receives the event and adjusts:

### 3.1 Storm Lighting Parameters

| Effect | Normal | During Storm |
|--------|--------|--------------|
| Ambient Intensity | Phase default | × 0.6 |
| Ambient Sky Color | Phase default | × 0.7 desaturate |
| Fog Density | Phase default | × 2.0 |
| Fog Color | Phase default | shift to gray-purple |
| Sun Intensity | Phase default | × 0.5 |
| Bloom (storm lightning) | Phase default | + thunder flash (spike) |

### 3.2 Implementation

Add to `ServerStormManager`:
```csharp
public event System.Action<float> OnStormIntensityChanged; // 0=clear, 1=full storm
```

Add to `DayNightController`:
```csharp
private float _stormIntensity = 0f;
public void SetStormIntensity(float intensity) {
    _stormIntensity = intensity;
}
```

Storm intensity lerps to 0 over 30s when storm ends.

---

## 4. Server Time Configuration

### 4.1 Default (Testing)
```
24 game hours = 30 real seconds
→ 1 game hour = 1.25 real seconds
→ 1 game minute = ~0.02 real seconds
```

### 4.2 Production (Adaptive)
Server can dynamically adjust `_dayCycleRealHours` based on:
- Game events (longer nights for boss fights?)
- Zone settings (some areas have eternal day/night?)
- Player voting

### 4.3 Server RPC Interface

```csharp
// Set time immediately (GM command)
[ServerRpc(RequireOwnership = false)]
public void SetTimeOfDayServerRpc(float time)
{
    _timeOfDay = Mathf.Repeat(time, 24f);
    BroadcastTimeOfDayClientRpc(_timeOfDay);
}

// Set cycle speed
[ServerRpc(RequireOwnership = false)]
public void SetDayCycleSpeedServerRpc(float realHoursForFullCycle)
{
    _dayCycleRealHours = realHoursForFullCycle;
}
```

---

## 5. Updated Component Inventory

Add:

| Component | File | Purpose |
|-----------|------|---------|
| `MoonController.cs` | `Scripts/Core/DayNight/` | Moon mesh + orbit + **lunar phase cycle** (~29.5 days) |
| `ConstellationController.cs` | `Scripts/Core/DayNight/` | Star field + constellation visibility |
| `ConstellationData.cs` | `Scripts/Core/DayNight/` | SO: 12 constellations with star positions + future click support |

---

## 6. Files to Create — Updated List

### New Files

| File | Purpose |
|------|---------|
| `Scripts/Core/DayNight/TimeOfDayPhase.cs` | Phase config SO |
| `Scripts/Core/DayNight/DayNightProfile.cs` | Profile containing 5 phases |
| `Scripts/Core/DayNight/TemperatureFilterConfig.cs` | Temp filter SO |
| `Scripts/Core/DayNight/TemperatureFilter.cs` | Temp color filter component |
| `Scripts/Core/DayNight/DayNightController.cs` | Main client lighting controller |
| `Scripts/Core/DayNight/MoonController.cs` | Moon mesh + orbit |
| `Scripts/Core/DayNight/ConstellationController.cs` | Stars + constellation manager |
| `Scripts/Core/DayNight/ConstellationData.cs` | SO: constellation star definitions |
| `Shaders/Stars/Stars.shader` | Star field shader with twinkle |
| `Materials/Skybox/Skybox_Day.mat` | Day skybox material |
| `Materials/Skybox/Skybox_Night.mat` | Night skybox material |
| `Volumes/DayNight/DayVolumeProfile.asset` | Day post-processing |
| `Volumes/DayNight/NightVolumeProfile.asset` | Night post-processing |

### Modified Files

| File | Change |
|------|--------|
| `Scripts/Core/ServerWeatherController.cs` | +TimeOfDay, +Temperature, +RPCs |
| `Scripts/Core/WindManager.cs` | +storm intensity event |
| `Scripts/Core/CloudSystem.cs` | Disable day-night (already off) |
| `Scripts/World/Clouds/VeilRaymarchMeshController.cs` | +SetDayNight() method |
| `Prefabs/CloudSystem.prefab` | Explicit enableDayNightCycle=false |

---

## 7. Open Questions (Answered)

| # | Question | Answer |
|---|----------|--------|
| 1 | **Lunar phases?** | **Full cycle** — new moon → waxing crescent → first quarter → waxing gibbous → full moon → waning gibbous → last quarter → waning crescent → new moon. Cycle: ~29.5 real days (synodic month). Moon mesh texture changes based on phase. |
| 2 | **Star click interaction?** | **Deferred** — implemented later when star navigation skill is added (requires sextant tool + navigation skill). Code should support this in future. Constellations visible but not interactive initially. |
| 3 | **Constellation count** | **12 constellations** — use real star map for positioning. Both hemispheres. |

### 12 Constellations (Reference Positions)

Based on real sky (approximate RA/Dec):

| # | Name (RU) | Name (EN) | Hemisphere | Primary Use |
|---|-----------|-----------|------------|-------------|
| 1 | Полярная | Polaris | North | North direction |
| 2 | Большая Медведица | Ursa Major | North | NW direction, pointer stars |
| 3 | Малая Медведица | Ursa Minor | North | Latitude reference |
| 4 | Кассиопея | Cassiopeia | North | W-shape, latitude marker |
| 5 | Орион | Orion | Equator | Equator reference, belt E-W |
| 6 | Дракон | Draco | North | Mid-latitude navigation |
| 7 | Лебедь | Cygnus | North | South pointing (cross) |
| 8 | Пегас | Pegasus | North | Autumn sky direction |
| 9 | Южный Крест | Crux | South | South direction |
| 10 | Центавр | Centaurus | South | Alpha Centauri ref |
| 11 | Ласточка | Delphinus | North | Small distinctive |
| 12 | Скорпион | Scorpius | South | Red Antares ref |