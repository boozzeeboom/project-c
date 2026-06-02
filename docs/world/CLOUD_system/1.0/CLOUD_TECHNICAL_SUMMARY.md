# CLOUD_system — Summary v0.4

**Version:** 0.4 | **Date:** 4 мая 2026 | **Status:** 🟡 Phase 4a Planning

---

## What Subagents Found (Critical Issues)

Subagent analysis identified **5 critical flaws** in my v0.2 proposal:

| # | Flaw | Why It Was Wrong |
|---|------|------------------|
| 1 | **Sky Dome as cloud layer** | Sky dome is a SKY RENDERER, not clouds. Doesn't create volumetric clouds at horizon. |
| 2 | **Distant impostors follow camera** | Breaks multiplayer requirement — each player would see different clouds at different positions. |
| 3 | **150 clouds total** | TOO FEW — 0.008 clouds/km² creates empty sky, not "filled horizon" |
| 4 | **Per-scene cloud regeneration** | Causes visual POPPING at scene boundaries |
| 5 | **Distance-based layers** | Contradicts altitude corridor gameplay (Upper/Middle/Lower by height = flight mechanic) |

---

## Corrected Architecture (v0.3)

### Core Principles:
1. **Altitude-based layers** (Upper 6000-8000m, Middle 3000-5000m, Lower 1500-3000m) — matches flight corridors
2. **Player-centered generation** — clouds generate around player, recycle at 15km
3. **Fixed world-space distant impostors** — ALL players see same clouds at same positions
4. **Sky dome is SKY RENDERER only** — does NOT count as cloud layer

### Cloud Distribution:

| Layer | Altitude | Near (0-5km) | Distant (5-15km) | Total |
|-------|----------|--------------|------------------|-------|
| Upper | 6000-8000m | 80 | 40 | 120 |
| Middle | 3000-5000m | 120 | 60 | 180 |
| Lower | 1500-3000m | 80 | 40 | 120 |
| **Total** | | **280** | **140** | **420** |

Plus 5 storms = **425 clouds** (vs original 890, vs v0.2's 150)

### Architecture:

```
BootstrapScene.unity (Never unloaded)
├── WindManager                              ← Central wind (receives server RPC)
├── ServerWeatherController (NetworkBehaviour)
├── ServerStormManager (NetworkBehaviour)
├── StormController visuals (5, DontDestroyOnLoad)
└── CloudManager (DontDestroyOnLoad)
    ├── NearCloudRenderer Upper (80 instanced)
    ├── NearCloudRenderer Middle (120 instanced)
    ├── NearCloudRenderer Lower (80 instanced)
    └── DistantCloudManager (140 impostors at WORLD positions)
```

---

## User Requirements Verification

| Requirement | Status | How v0.3 Meets It |
|-------------|--------|-------------------|
| "2 игрока видят облака в одном направлении" | ✅ | WindManager + fixed world positions for distant |
| Storm positions server-authoritative | ✅ | ServerStormManager + world-space |
| Most clouds client-side | ✅ | Near/distant clouds client-only |
| Not too heavy | ✅ | 425 clouds, GPU instanced (~5-7 draw calls) |
| "Горизонт затянут облаками" | ✅ | 140 distant impostors at world positions |
| Altitude-based layers | ✅ | Upper/Middle/Lower by altitude |
| Movable + non-movable layers | ✅ | Impostors mostly static, near clouds wind-driven |
| Storm layers | ✅ | 5 server-controlled storms with VFX |
| "Tasty" visual | ✅ | CloudGhibli shader improvements (3 octaves, light, tint) |

---

## Implementation Phases

| Phase | Duration | Goal | Success Criteria |
|-------|----------|------|------------------|
| 1. Wind + CloudManager | Week 1 | Server wind, synchronized | 2 clients see same wind |
| 2. Instanced Near Clouds | Week 1-2 | 280 near clouds | Clouds around player, recycle at 15km |
| 3. Distant Impostors | Week 2 | 140 at world positions | Same positions for all clients |
| 4. Storm Authority | Week 2-3 | 5 server storms | Same position for all clients |
| 5. Shader Improvements | Week 3 | "Tasty" visuals | 3 octaves, light, tint |
| 6. Polish | Week 4 | Final integration | 60 FPS, visual appeal |

---

## Performance Budget

| Metric | Target | v0.3 Estimate |
|--------|--------|---------------|
| Draw Calls | <10 | ~5-7 |
| GPU | <3ms | ~3ms |
| CPU | <3.6ms | ~0.6ms |
| Memory | <30MB | ~21MB |

---

## Why NOT Full Raymarch (Original Analysis)

User mentioned "raymarch from another engine" as reference. Subagent analysis clarified:

1. **Full raymarch (64-128 steps) = 4-20ms GPU** — TOO EXPENSIVE for 60Hz
2. **Simplified approach is correct**: instanced mesh with improved CloudGhibli shader
   - 3 FBM octaves instead of 2
   - Light influence (directional response)
   - Day/night tint blending
   - This achieves "raymarch-like quality" without GPU cost

---

## HorizonVeil Decision (2026-05-04)

### CLOUDENGINE Analysis

Analyzed C:\CLOUDPROJECT\CLOUDENGINE for raymarch implementation:

| File | Approach | GPU Cost |
|------|----------|----------|
| cloud_advanced.frag | Full volumetric raymarch (64 steps, 6 octaves FBM) | ~3ms |
| VeilShader.shader | Flat plane + noise + depth fade (NOT volumetric) | ~0.2ms |

**Key Finding:** VeilShader is NOT volumetric — just smoke on a flat plane. Cannot create " клубящаяся завеса со своими впадинами каньонами" (boiling curtain with valleys/canyons).

### Why Raymarch Was Rejected (Reconsidered)

Original rejection based on: "Full raymarch 64-128 steps = 4-20ms GPU"

But CLOUDENGINE shows:
- 64 steps = ~3ms (reasonable)
- 32 steps = ~2ms
- **16 steps = ~1ms**

The cost is manageable if we use fewer steps (8-16) and half-resolution rendering.

### Decision: HorizonVeil with Simplified Raymarch

**Approved approach:**
- 8-16 raymarch steps (not 64-128)
- FBM noise (value noise as in CLOUDENGINE — fast)
- Height gradient for "curtain layer"
- Single scatter directional light
- Half-resolution render target + upscale
- Result: ~1-1.5ms GPU, volumetric quality

**Existing distant impostors remain** — they handle mid-sky clouds. HorizonVeil adds the bottom-horizon volumetric layer.

### Architecture Integration

```
Phase 4a: HorizonVeil (NEW)
├── HorizonVeilRenderer.cs — half-res volumetric
├── VeilRaymarch.shader — 8-16 steps, FBM noise
└── Render to RT, blur, composite

Phase 4b: Storm Authority (existing)
├── ServerStormManager (5 storms)
└── StormController visuals
```

---

**Status:** 🟡 v0.4 — HorizonVeil with simplified raymarch approved for Phase 4a