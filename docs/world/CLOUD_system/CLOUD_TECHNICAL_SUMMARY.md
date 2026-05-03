# CLOUD_system — Summary v0.3

**Version:** 0.3 | **Date:** 3 мая 2026 | **Status:** 🔴 Critical Corrections Applied

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

## Why NOT Full Raymarch

User mentioned "raymarch from another engine" as reference. Subagent analysis clarified:

1. **Full raymarch (64-128 steps) = 4-20ms GPU** — TOO EXPENSIVE for 60Hz
2. **Simplified approach is correct**: instanced mesh with improved CloudGhibli shader
   - 3 FBM octaves instead of 2
   - Light influence (directional response)
   - Day/night tint blending
   - This achieves "raymarch-like quality" without GPU cost

---

## Key Differences: v0.2 → v0.3

| Aspect | v0.2 (WRONG) | v0.3 (CORRECT) |
|--------|-------------|----------------|
| Sky Dome | As cloud layer | Sky renderer only |
| Distant impostors | Follow camera | Fixed world positions |
| Near cloud count | 150 total | 280 total |
| Generation | Per-scene | Player-centered recycling |
| Layer type | Distance-based | Altitude-based |
| Impostor positions | Camera-relative | World-relative |

---

## Documents Updated

| Document | Status |
|----------|--------|
| `CLOUD_ARCHITECTURE.md` | ✅ v0.3 with critical corrections |
| `CLOUD_IMPLEMENTATION_PLAN.md` | ✅ v0.3 with Phase 1-6 |
| `CLOUD_TECHNICAL_SUMMARY.md` | ✅ (will be regenerated) |
| `CLOUD_VISUAL_DESIGN.md` | ⚠️ Still relevant, shader improvements listed |
| `CLOUD_ONBOARDING.md` | ⚠️ Needs update for v0.3 |

---

**Status:** 🔴 v0.3 — Critical corrections applied, architecture validated against requirements