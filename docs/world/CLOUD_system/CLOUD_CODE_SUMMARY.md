# CLOUD_system — Code Summary v1.0

**Date:** 4 мая 2026 | **Status:** ✅ Working

---

## Architecture Overview

```
BootstrapScene/
├── CloudManager          (singleton, DontDestroyOnLoad)
├── WindManager           (singleton, central wind source)
└── ServerWeatherController (NetworkBehaviour, server-authoritative)

CloudManager children/
├── UpperLayer            (NearCloudRenderer, 6000-8000m)
├── MiddleLayer           (NearCloudRenderer, 3000-5000m)
├── LowerLayer            (NearCloudRenderer, 1500-3000m)
└── DistantManager        (DistantCloudManager, 5000-15000m)
```

---

## Components

### 1. CloudManager.cs
**Purpose:** Central orchestrator for all cloud systems

**Key fields:**
| Field | Default | Description |
|-------|---------|-------------|
| `UpperCount` | 80 | Cloud count for upper layer |
| `UpperMinAlt` | 6000m | Upper layer min altitude |
| `UpperMaxAlt` | 8000m | Upper layer max altitude |
| `MiddleCount` | 120 | Cloud count for middle layer |
| `MiddleMinAlt` | 3000m | Middle layer min altitude |
| `MiddleMaxAlt` | 5000m | Middle layer max altitude |
| `LowerCount` | 80 | Cloud count for lower layer |
| `LowerMinAlt` | 1500m | Lower layer min altitude |
| `LowerMaxAlt` | 3000m | Lower layer max altitude |
| `DistantCount` | 140 | Impostor count |
| `DistantMinDist` | 5000m | Min distance from player |
| `DistantMaxDist` | 15000m | Max distance from player |
| `DistantMinSize` | 500f | Min impostor size |
| `DistantMaxSize` | 2000f | Max impostor size |

**Public methods:**
- `RegenerateAllClouds()` — Regenerates all clouds around current player position
- `GetTotalCloudCount()` — Returns total active cloud count

### 2. NearCloudRenderer.cs
**Purpose:** GPU-instanced near clouds for one altitude layer

**Settings (inspector):**
| Field | Default | Description |
|-------|---------|-------------|
| `CloudCount` | 80 | Number of clouds |
| `MinAltitude` | 3000m | Layer min height |
| `MaxAltitude` | 5000m | Layer max height |
| `CloudSize` | 100f | Base cloud size |
| `CloudMaterial` | null | Material for rendering |

**Behavior:**
- Generates clouds in radius 5000m around player
- Wind moves clouds each frame
- Recycles clouds when >10000m from player (regenerates within 1000-5000m radius)
- Uses seeded RNG (seed = 12345 + name hash) for consistent positions across clients

**Public methods:**
- `Initialize()` — Creates mesh and material
- `Generate(playerPos)` — Generates cloud positions around player
- `SetWind(dir, speed)` — Sets wind direction and speed

### 3. DistantCloudManager.cs
**Purpose:** Large billboard impostors for distant horizon clouds

**Settings (inspector):**
| Field | Default | Description |
|-------|---------|-------------|
| `ImpostorCount` | 140 | Number of impostors |
| `MinDistance` | 5000m | Min spawn distance |
| `MaxDistance` | 15000m | Max spawn distance |
| `MinSize` | 500f | Min impostor scale |
| `MaxSize` | 2000f | Max impostor scale |
| `ImpostorMaterial` | null | Material for rendering |

**Behavior:**
- Horizontal quad planes (rotated -90° on X)
- Generated around player with seeded RNG
- Move with wind
- Recycle when >18000m from player
- Uses seeded RNG (seed = 54321) for consistency across clients

**Public methods:**
- `Initialize()` — Creates quad mesh and material
- `Generate(playerPos)` — Generates impostors around player
- `SetWind(dir, speed)` — Sets wind direction and speed

### 4. WindManager.cs
**Purpose:** Central wind singleton — single source of truth for wind

**Key fields:**
| Field | Default | Description |
|-------|---------|-------------|
| `CurrentWindDirection` | Vector3.right | Current wind direction |
| `CurrentWindSpeed` | 0f | Current wind speed (m/s) |
| `_interpolationSpeed` | 0.5f | Lerp speed for wind changes |

**Key features:**
- Singleton pattern
- Receives wind from server via `ApplyWindUpdate(dir, speed)`
- Broadcasts via `OnWindUpdated` event
- Smooth interpolation between wind states

### 5. ServerWeatherController.cs
**Purpose:** Server-authoritative weather broadcast

**Key fields:**
| Field | Default | Description |
|-------|---------|-------------|
| `_windDirection` | Vector3.right | Server wind direction |
| `_windSpeed` | 0f | Server wind speed |
| `_broadcastInterval` | 2f | Broadcast every X seconds (0.5 Hz) |
| `_enableWindVariation` | false | Enable wind variation |
| `_directionVariationAngle` | 15f | Max direction variation |
| `_speedVariationPercent` | 0.2f | Max speed variation |

**Network:**
- Must be on NetworkObject
- Uses `[ClientRpc]` to broadcast to all clients
- `TransitionWind()` for smooth weather transitions

---

## Wind Sync Flow

```
ServerWeatherController (IsServer)
    ↓ every 2 seconds
    ↓ ClientRpc: BroadcastWindClientRpc(dir, speed)
    ↓
WindManager (all clients)
    ↓ ApplyWindUpdate()
    ↓
CloudManager.Update()
    ↓ reads CurrentWindDirection/Speed
    ↓ calls SetWind() on all renderers
    ↓
NearCloudRenderer.Update() + DistantCloudManager.Update()
    ↓ moves clouds with wind
```

---

## Cloud Recycling Logic

**Near clouds (10000m threshold):**
```
if (distance_to_player > 10000):
    regenerate at 1000-5000m from player
```

**Distant impostors (18000m threshold):**
```
if (distance_to_player > 18000):
    regenerate at 5000-15000m from player (using MinDist/MaxDist)
```

---

## Performance

| Metric | Target | Notes |
|--------|--------|-------|
| Draw calls | ~5-7 | 3 layers + 1 distant = 4 max |
| GPU | <3ms | Instanced rendering |
| Memory | <30MB | ~420 cloud matrices |

---

## Seeded Random for Multiplayer

All cloud positions use seeded `System.Random` for consistency:
- NearCloudRenderer: `new System.Random(12345 + name.GetHashCode())`
- DistantCloudManager: `new System.Random(54321)`

This ensures all clients see identical cloud positions.

---

## Phase Status

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Wind System + CloudManager | ✅ Done |
| 2 | Instanced Near Clouds | ✅ Done |
| 3 | Distant Impostors at World Positions | ✅ Done |
| 4 | Storm Authority | 🔜 Next |
| 5 | Shader Improvements | 🔜 Next |
| 6 | Polish | 🔜 Next |

---

**Last updated:** 2026-05-04