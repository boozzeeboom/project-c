# CLOUD_system — Architecture v0.4 (HorizonVeil Added)

**Версия:** 0.4 (HorizonVeil Added) | **Дата:** 4 мая 2026 | **Status:** 🟡 Phase 4a Planning
**Автор:** Technical Director + Subagent Analysis

---

## Executive Summary — CRITICAL ISSUES FOUND

Subagent analysis revealed **5 critical flaws** in v0.2 that would break user requirements:

| Flaw | v0.2 | v0.3 Fix |
|------|------|----------|
| Sky Dome as cloud layer | ❌ WRONG — It's sky renderer, not clouds | Remove from cloud system, keep as background only |
| Distant impostors follow camera | ❌ WRONG — Breaks multiplayer wind sync | Fixed world-space positions |
| 150 clouds total | ❌ TOO FEW — 0.008 clouds/km² | ~390 clouds (130/layer × 3 layers) |
| Per-scene generation | ❌ WRONG — Creates boundary popping | Player-centered recycling |
| Distance-based layers | ❌ WRONG — Contradicts altitude corridors | Altitude-based (Upper/Middle/Lower) |

---

## User Requirements (MUST NOT CONTRADICT)

From user conversation:
1. "2 игрока не должны видеть плывущие в разных направлениях основные облака" — wind synced
2. Storm positions server-authoritative — gameplay impact
3. Most clouds client-side only — no network overhead
4. "Не должно быть с нагрузкой на все системы" — performance budget
5. "Горизонт затянут облаками" — filled horizon with clouds
6. Cloud layers with different behaviors (movable, non-movable, storm)
7. Raymarch from another engine was reference — NOT VDB, not physically accurate
8. "Облака могут быть простыми - но максимально вкусными" — simple but "tasty"
9. "Подвижные и неподвижные слои" — movable and non-movable layers
10. "Слои с грозовыми тучами" — storm cloud layers

---

## Architecture v0.3 — Corrected

### Core Principle: Player-Centered, Altitude-Based

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    CLOUD SYSTEM v0.3 — CORRECTED ARCHITECTURE                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  WIND MANAGER (central, receives server wind at 0.5 Hz)                   │
│  ├── Interpolates wind direction/speed                                      │
│  └── Exposes to ALL cloud systems                                           │
│                                                                              │
│  LAYER UPPER (6000-8000m)                                                   │
│  ├── Near (0-5km): 80 clouds with volumetric shader                       │
│  ├── Distant (5-15km): 40 billboard impostors at WORLD positions          │
│  └── Player flies UNDER                                                     │
│                                                                              │
│  LAYER MIDDLE (3000-5000m) — DENSEST                                        │
│  ├── Near (0-5km): 120 clouds (player flies THROUGH)                       │
│  ├── Distant (5-15km): 60 billboard impostors at WORLD positions          │
│  └── This layer creates flying-through experience                           │
│                                                                              │
│  LAYER LOWER (1500-3000m)                                                   │
│  ├── Near (0-5km): 80 clouds                                                │
│  ├── Distant (5-15km): 40 billboard impostors at WORLD positions          │
│  └── Player flies ABOVE                                                     │
│                                                                              │
│  STORMS (server-authoritative, world-space)                                  │
│  ├── 5 storms at fixed world positions                                     │
│  ├── Visible if <50km from player                                           │
│  └── Affects local gameplay area only                                       │
│                                                                              │
│  SKY DOME — NOT A CLOUD LAYER                                                │
│  └── Procedural sky (blue gradient, sun, stars) — background only          │
│                                                                              │
│  TOTAL: ~390 clouds + 100 distant impostors + 5 storms = ~495              │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 1. Cloud Rendering Architecture

### 1.1 Layer Structure (Altitude-Based, NOT Distance-Based)

| Layer | Altitude | Near Clouds | Distant Impostors | Player Interaction |
|-------|----------|-------------|-------------------|-------------------|
| **Upper** | 6000-8000m | 80 | 40 | Fly under |
| **Middle** | 3000-5000m | 120 | 60 | Fly THROUGH (densest) |
| **Lower** | 1500-3000m | 80 | 40 | Fly above |
| **Storms** | 1200-9000m | 5 (special) | — | Gameplay impact |

**Total: 390 near + 140 distant = 530 objects** (vs v0.2's 150, vs current's 890)

### 1.2 Near Cloud Rendering (0-5km from player)

**Why NOT billboards for near:**
- User said "mesh + shader not acceptable as ONLY solution"
- Must have volumetric feel for flying THROUGH
- "Tasty" clouds need proper rim glow, soft edges, shape variety

**Solution: High-quality instanced mesh rendering**

```
NearCloudRenderer (per layer, altitude-specific)
├── Graphics.DrawMeshInstanced (single draw call per layer)
├── Shared CloudGhibli shader with improvements:
│   ├── 3+ FBM noise octaves (not 2)
│   ├── Light influence (directional light response)
│   ├── Day/night tint blending
│   └── Rim glow with sun-position reaction
├── Object pooling: 80-120 clouds in buffer
├── Wind-driven movement (server direction)
└── Recycle when >5km from player
```

### 1.3 Distant Cloud Impostors (5-15km from player)

**CRITICAL FIX from v0.2:** Fixed WORLD positions, NOT camera-following

```
DistantCloudManager
├── Fixed world-space positions (NOT camera-relative)
├── GPU instanced billboards (single draw call for all 140)
├── Positions: Ring at 5-15km from WORLD ORIGIN (not player)
├── Wind affects movement direction
├── Visible to ALL players at same positions
└── Parallax handled naturally (different players see different angles)
```

**Why world positions, not camera-following:**
- Player A at (40000, 3000, 40000) — impostors at (50000-150000, 3000-7000, 50000-150000)
- Player B at (120000, 3000, 40000) — sees SAME impostors at same world positions
- Wind moves impostors in same direction for ALL players ✅

### 1.4 Simplified Volumetric (NOT full raymarch)

**Reference from another engine was SIMPLIFIED volumetric, NOT full raymarch**

User said: "raymarch в другом движке" — this was likely simplified, not 128-step physically-accurate.

**Solution for near clouds: Simplified volumetric look via shader**

```
CloudGhibli.shader improvements (P0):
├── 3rd noise octave (FBM with 3 octaves instead of 2)
├── _LightInfluence parameter (directional light response)
├── _TintColor1/_TintColor2/_TintBlend (day/night tints)
├── _LightningFlash for storms
├── Vertex displacement for shape morphing
└── Rim glow with sun-position reactivity
```

This creates "raymarch-like" visual quality without the GPU cost.

---

## 2. Player-Centered Cloud Generation (FIXED)

### 2.1 Why NOT Per-Scene Generation

v0.2 proposed: "150 clouds regenerated when scene changes"

**Problems with per-scene:**
1. Scene boundary at z=79,999
2. Preload starts at z=69,999 (10km before)
3. At boundary, old clouds pop out, new clouds pop in
4. **Visual popping breaks immersion**

### 2.2 Player-Centered Approach (v0.3)

```
CloudManager (singleton, DontDestroyOnLoad)
├── Maintains pool of ~390 "active" clouds
├── Each cloud: position + altitude layer + recycling state
│
├── Every frame:
│   ├── For each cloud in pool:
│   │   ├── Apply wind movement
│   │   ├── If distance to player > 15km → RECYCLE
│   │   └── If distance to player < 5km → keep
│   │
│   └── For recycled clouds:
│       ├── Reposition to edge of player's "bubble"
│       └── Maintain altitude layer (not scene!)
│
├── Result: Clouds always around player, follow player
│          No scene boundary pops
│          All players in same area see same clouds
```

### 2.3 Cloud Recycle Logic

```
When cloud.distanceFromPlayer > 15km:
    1. Pick random angle (for variety)
    2. Pick altitude from cloud's layer (1500-3000 OR 3000-5000 OR 6000-8000)
    3. Position at 8-12km from player in that direction
    4. This is "recycled" not "new" — no instantiation overhead
```

**Key insight:** Recycling existing cloud objects prevents GC spikes.

---

## 3. Wind System Architecture

### 3.1 WindManager (Central)

```
WindManager (MonoBehaviour, receives from server)
├── Vector3 windDirection (normalized)
├── float windSpeed
├── float targetWindDirection
├── float targetWindSpeed
├── float interpolationSpeed
│
├── Server calls ApplyWind(dir, speed) via RPC
├── Update() lerps current toward target
└── Exposes CurrentWindDirection, CurrentWindSpeed
```

### 3.2 Wind Distribution to Systems

All cloud systems read from WindManager:

```
CloudLayer_Near → reads WindManager.CurrentWindDirection
DistantCloudManager → reads WindManager.CurrentWindDirection
StormController → reads WindManager.CurrentWindDirection
```

**Single source of truth** — all clouds move in same direction.

### 3.3 Network Sync

```
ServerWeatherController (NetworkBehaviour, IsServer)
├── Broadcasts every 2 seconds (0.5 Hz)
├── [ClientRpc] BroadcastWindClientRpc(dir, speed)
└── Clients receive → WindManager.ApplyWind()
```

**Bandwidth:** ~16 bytes per 2 seconds = 8 B/s

---

## 4. Storm System Architecture (Server-Authoritative)

### 4.1 ServerStormManager

```
ServerStormManager (NetworkBehaviour, IsServer)
├── List<StormData> _activeStorms (5 storms)
│   ├── ushort Id
│   ├── Vector3 WorldPosition (world-space, NOT scene-bound)
│   ├── float Intensity
│   └── bool LightningActive
│
├── Update():
│   ├── Move storms with wind direction
│   └── Periodic sync (0.5 Hz): StormStateUpdateClientRpc
│
├── TriggerLightning(stormId) → ClientRpc with flash
└── Storm spawns at server-authoritative positions
```

### 4.2 StormController (Client Visual)

```
StormController (MonoBehaviour, client-only)
├── WorldPosition (from server)
├── Intensity
├── LightningActive
│
├── Render:
│   ├── Volumetric-ish mesh (stretched cumulonimbus shape)
│   ├── Lightning VFX (VFX Graph)
│   ├── Shader with _LightningFlash parameter
│   └── Visible distance: <50km from player
│
├── Lives in Bootstrap scene (DontDestroyOnLoad)
└── Survives scene transitions automatically
```

### 4.3 World-Space Storms Survive Scene Transitions

```
Scene(0,0) center: (40000, 3000, 40000)
Storm at world position: (80000, 3000, 60000)

Player moves to Scene(1,0) center: (120000, 3000, 40000)
Storm world position: (80000, 3000, 60000) — STILL THE SAME
Player sees storm at same world location — correctly!
```

---

## 5. Scene Integration (With ClientSceneLoader)

### 5.1 Bootstrap Scene Contents

```
BootstrapScene.unity (Never unloaded)
├── NetworkManagerController
├── PlayerSpawner
├── WindManager                              ← NEW (central wind)
├── ServerWeatherController                  ← NetworkBehaviour
├── ServerStormManager                       ← NetworkBehaviour
├── StormController visuals (5)              ← DontDestroyOnLoad
└── (CloudSystem REMOVED — replaced by CloudManager)
```

### 5.2 CloudManager (Replaces CloudSystem)

```
CloudManager (MonoBehaviour, DontDestroyOnLoad)
├── WindManager reference
├── NearCloudRenderer Upper (6000-8000m)
├── NearCloudRenderer Middle (3000-5000m)
├── NearCloudRenderer Lower (1500-3000m)
├── DistantCloudManager (140 impostors at world positions)
├── Object pool management
│
├── Update():
│   ├── Apply wind to all systems
│   ├── Recycle distant clouds
│   └── Update distant cloud positions (wind-based)
│
└── NOT scene-bound — works across all 24 scenes
```

### 5.3 Integration with ClientSceneLoader

**CloudManager does NOT respond to OnSceneLoaded**

Why? Because clouds are player-centered, not scene-bound. No regeneration needed on scene change.

**What happens at scene boundary:**
1. Player crosses z=79,999 (scene 0,0 → 0,1)
2. ClientSceneLoader loads/unloads scenes
3. CloudManager continues running (DontDestroyOnLoad)
4. Near clouds stay around player
5. Distant clouds at world positions stay at same places
6. **No pop, no regeneration needed**

---

## 7. HorizonVeil — Volumetric Curtain for Horizon (v0.4)

### 7.1 Why We Need It

User requirement: "Горизонт затянут облаками" — horizon filled with clouds

But existing systems:
- **Near clouds (0-5km)** — individual puffy clouds, no curtain effect
- **Distant impostors (5-15km)** — individual blobs, gaps between them
- **Neither creates "завеса" (curtain)** — continuous cloud layer at horizon

CLOUDENGINE VeilShader analysis confirmed: flat plane + noise = NOT volumetric. Cannot create "клубящаяся завеса со своими впадинами каньонами" (boiling curtain with valleys/canyons).

### 7.2 Decision: Simplified Volumetric Raymarch

Based on CLOUDENGINE raymarch analysis (cloud_advanced.frag):
- 64 steps = ~3ms (too high combined with other systems)
- **16 steps = ~1ms** (acceptable)
- 8 steps = ~0.5ms (good for half-res)

**HorizonVeil approach:**
- 8-16 raymarch steps (NOT 64-128)
- FBM noise with 2-3 octaves (value noise, fast)
- Height gradient: Y=1000-3000m (curtain layer)
- Single scatter directional light (Beer-Lambert)
- **Half-resolution render target** (512x288) + upscale
- Result: ~1-1.5ms GPU

### 7.3 Architecture

```
HorizonVeilRenderer.cs (component in CloudManager)
├── Render at half-resolution (0.5x)
├── VeilRaymarch.shader (8-16 steps)
│   ├── FBM noise for density
│   ├── Height gradient (smoothstep)
│   ├── Beer-Lambert absorption
│   └── Single directional light
├── Blur pass (optional)
└── Composite to screen

Integration with existing:
├── CloudManager owns HorizonVeilRenderer
├── Wind affects veil movement (via shader uniform)
├── Phase 4a (before Storm Authority)
```

### 7.4 Shader Parameters

```hlsl
// VeilRaymarch.shader properties
_VeilColor              // Base color
_FogDensity             // Density multiplier
_LightDir               // Sun direction
_DayFactor              // Day/night blend
_NoiseScale             // FBM scale
_NoiseSpeed             // Wind animation speed
_LightningIntensity     // Storm lightning (from ServerStormManager)
```

### 7.5 Performance Budget

| Metric | Value | Notes |
|--------|-------|-------|
| Resolution | 512x288 (half-res) | Upscale to screen |
| Raymarch Steps | 12 | Balance quality/cost |
| FBM Octaves | 2 | Fast noise |
| GPU | ~1-1.5ms | With half-res |
| Draw calls | 2 (RT write + blit) | Separate from cloud draws |

### 7.6 Relationship with DistantImpostors

| System | Distance | Purpose |
|--------|----------|---------|
| Near clouds | 0-5km | Individual puffy clouds |
| HorizonVeil | 1-5km | Volumetric curtain at horizon |
| Distant impostors | 5-15km | Individual clouds at mid-sky |
| Sky dome | background | Sky renderer only |

**Note:** HorizonVeil covers 1-5km horizon area (LOW altitude). Distant impostors cover 5-15km (MID altitude). They complement each other.

---

## 8. Sky Dome — NOT a Cloud Layer

### 6.1 Correct Understanding

Sky dome is a **SKY RENDERER**, not a cloud layer:
- Blue gradient sky
- Sun/stars
- Optional: very subtle wisps (purely cosmetic)

**Sky dome does NOT create "horizon filled with clouds"**

### 6.2 Horizon Coverage Comes From:

1. **Distant Impostors at world positions** (5-15km from player in all directions)
2. **Near clouds around player** (0-5km, 390 total)
3. **Storms at world positions** (5 visible if <50km away)

These create continuous cloud coverage at all distances.

---

## 9. Performance Budget (v0.3)

### 7.1 Cloud Count

| Type | Count | Draw Calls |
|------|-------|------------|
| Near clouds (instanced, 3 layers) | 390 | 3 (one per layer) |
| Distant impostors (instanced) | 140 | 1 |
| Storms | 5 | 1 |
| **Total** | **535** | **~5-7** |

vs Current: 890+ draw calls

### 7.2 GPU Budget

| Component | Est. GPU Time |
|-----------|---------------|
| Near cloud rendering (3 layers instanced) | ~1.5 ms |
| Distant impostors (instanced) | ~0.5 ms |
| Storm visuals | ~0.3 ms |
| Cloud shader (improved FBM + lighting) | ~0.7 ms |
| **Total** | **~3.0 ms** |

### 7.3 CPU Budget

| Component | Est. CPU Time |
|-----------|---------------|
| Wind interpolation | ~0.1 ms |
| Cloud recycling logic | ~0.3 ms |
| Storm update (5) | ~0.2 ms |
| **Total** | **~0.6 ms** |

---

## 10. Shader Improvements Required

### 8.1 CloudGhibli.shader — Must Add

```hlsl
// LIGHTING (currently Unlit — WRONG)
_LightInfluence    // Range(0,1) — how much directional light affects color

// DAY/NIGHT TINT
_TintColor1        // Dawn/day tint
_TintColor2        // Dusk/night tint
_TintBlend         // Range(0,1) — driven by time of day

// STORM
_LightningFlash    // Float — for storm lightning effect

// SHAPE
// Already has: _VertexDisplacement, but needs better animation
```

### 8.2 Recommended Values

| Parameter | Decorative | Interactive | Storm |
|-----------|------------|-------------|-------|
| _RimPower | 3.5 | 1.8 | 1.2 |
| _RimColor.a | 0.4 | 0.8 | 1.0 |
| _AlphaBase | 0.3 | 0.6 | 0.9 |
| _LightInfluence | 0.1 | 0.4 | 0.7 |
| _NoiseScale | 0.5 | 1.0 | 1.5 |
| _VertexDisplacement | 1.0 | 3.0 | 12.0 |

---

## 11. Implementation Phases (Testing-Based)

### Phase 1: Wind System + CloudManager
- [ ] WindManager (central wind)
- [ ] ServerWeatherController (RPC)
- [ ] CloudManager (singleton, object pool)
- [ ] Test: 2 clients see same wind direction

### Phase 2: Instanced Near Clouds
- [ ] NearCloudRenderer (3 altitude layers)
- [ ] Graphics.DrawMeshInstanced implementation
- [ ] Cloud recycling logic
- [ ] Test: Clouds around player, recycle at 15km

### Phase 3: Distant Impostors at World Positions
- [ ] DistantCloudManager (world-space positions)
- [ ] GPU instancing for 140 impostors
- [ ] Test: Same impostors visible to all clients

### Phase 4a: HorizonVeil — Volumetric Curtain (NEW)
- [ ] HorizonVeilRenderer.cs (half-res volumetric)
- [ ] VeilRaymarch.shader (8-16 steps, FBM noise)
- [ ] Render target setup (512x288)
- [ ] Wind integration (shader uniform)
- [ ] Test: "Клубящаяся завеса со своими впадинами каньонами"

### Phase 4b: Storm Authority
- [ ] ServerStormManager
- [ ] StormController visual
- [ ] Test: Storms at same positions for all clients

### Phase 5: Shader Improvements
- [ ] CloudGhibli: 3 noise octaves
- [ ] CloudGhibli: Light influence
- [ ] CloudGhibli: Day/night tint blending
- [ ] Test: "Tasty" visual quality

### Phase 6: Polish
- [ ] URP Volume with Bloom
- [ ] Lightning VFX
- [ ] Test: Visual appeal

---

## 12. Summary: Why v0.4 is Correct

### Correct vs v0.2:

| v0.2 (WRONG) | v0.3 (CORRECT) | Reason |
|--------------|-----------------|--------|
| Sky Dome as cloud layer | Sky Dome is sky renderer only | Sky dome doesn't create horizon clouds |
| Impostors follow camera | Fixed world-space impostors | Camera-following breaks multiplayer |
| 150 clouds total | ~390 near + 140 distant | 150 was too sparse |
| Per-scene regeneration | Player-centered recycling | Per-scene causes boundary pop |
| Distance-based layers | Altitude-based layers | Altitude = flight corridor mechanic |

### Matches User Requirements:

| Requirement | How v0.3 Meets It |
|-------------|-------------------|
| Wind synced for 2 players | WindManager + fixed world positions |
| Storms server-authoritative | ServerStormManager + world-space |
| Client-side clouds | Near/distant clouds are client-only |
| Filled horizon | 140 distant impostors at world positions |
| Altitude layers | Upper (6000-8000), Middle (3000-5000), Lower (1500-3000) |
| Movable + non-movable layers | Impostors (mostly static) + near clouds (wind-driven) |
| Storm layers | 5 server-controlled storms with VFX |
| Not too heavy | ~5-7 draw calls, ~3ms GPU |
| "Tasty" visual | Improved CloudGhibli shader |

---

**Status:** 🔴 v0.3 — Critical corrections applied based on subagent analysis