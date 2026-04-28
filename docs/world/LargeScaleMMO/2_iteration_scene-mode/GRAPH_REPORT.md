# Graph Report: ProjectC LargeScale MMO Architecture

**Generated:** 28.04.2026
**Updated:** After WorldSceneGenerator refactoring
**Corpus:** docs/world/LargeScaleMMO/2_iteration_scene-mode + Scene/Streaming scripts

---

## God Nodes (Most Connected Concepts)

1. **Scene Layer (80k×80k)** — 4 connections
   Central concept: all scene management revolves around the 79,999 unit boundary

2. **NGO CheckObjectVisibility** — 3 connections
   Critical for multiplayer traffic filtering per scene

3. **FloatingOriginMP** — 2 connections
   NOT needed inside scenes (threshold 150k never reached)

---

## Key Architecture Decisions

### Floating Origin Analysis

| Question | Answer |
|----------|--------|
| Need FloatingOriginMP inside scenes? | **NO** |
| Why? | 79,999 < 100,000 (float precision safe zone) |
| When needed? | Only between scenes at large world coords |
| Threshold | 150,000 units (never reached inside scene) |

**Conclusion:** The 79,999 scene size was specifically chosen to avoid floating point precision issues. Players never exceed this within a scene. Scene transitions reset local origin to 0.

### Scene Grid: 24 Scenes (6×4)

```
Grid: 6 columns (X) × 4 rows (Z)

[0,3] ────────────────── [5,3]   ← Row 3: Pole boundary (blocked)
  │                           │
  │  WorldScene_X_Y          │
  │  Size: 79,999 × 79,999   │
  │                           │
[0,0] ────────────────── [5,0]   ← Row 0: Equator
       Wrap-around →           (columns connect)
```

**Horizontal wrap:** Scene[0,Y] connects to Scene[5,Y]
**Vertical block:** Rows 0 and 3 have PoleBlocker colliders

### Network Architecture

```
NetworkPlayer.prefab
├── NetworkObject (NGO identity)
├── NetworkTransform (client authority)
├── NetworkPlayer (movement, trade, interaction)
├── CharacterController
└── PlayerInputReader

Scene Management (in Bootstrap scene, NOT in world scenes):
├── ServerSceneManager (tracks client→scene mapping)
├── ClientSceneLoader (loads 3×3 neighborhood)
├── SceneBoundNetworkObject (CheckObjectVisibility)
└── SceneTransitionCoordinator (RPC coordination)
```

### Visibility System

NGO's `CheckObjectVisibility`:
- Called at **spawn time** — not continuously
- Returns `false` → object **never** spawns on that client
- For runtime changes: `NetworkHide()` / `NetworkShow()`

### Two-Layer Architecture

```
┌─────────────────────────────────────────┐
│  SCENE LAYER (79,999 × 79,999)         │
│  - ServerSceneManager loads/unloads     │
│  - SceneID identifies location          │
│  - Preload at 10,000 from boundary      │
│  - 24 scenes in 6×4 grid               │
└────────────────────┬────────────────────┘
                     │ uses
                     ▼
┌─────────────────────────────────────────┐
│  CHUNK LAYER (2,000 × 2,000)           │
│  - ProceduralChunkGenerator             │
│  - ChunkLoader (load radius 2 chunks)   │
│  - 625 chunks per scene (40×40)         │
│  - Works unchanged inside scenes        │
└─────────────────────────────────────────┘
```

---

## WorldSceneGenerator Refactoring + BootstrapSceneGenerator

### WorldSceneGenerator: What Was REMOVED:

| Method | Lines | Reason |
|--------|-------|--------|
| `CreateRuntimeSetup()` | 393-410 | Runtime components DON'T belong in world scenes |
| `AddWorldSceneManager()` | 412-418 | Belongs in Bootstrap scene |
| `AddClientSceneLoader()` | 420-431 | Belongs in Bootstrap scene |
| `AddServerSceneManager()` | 433-443 | Belongs in Bootstrap scene |
| `AddSceneTransitionCoordinator()` | 445-452 | Belongs in Bootstrap scene |
| `AddWorldStreamingManager()` | 454-466 | Belongs in Bootstrap scene |
| `AddFloatingOriginMP()` | 468-497 | **NOT NEEDED** - precision safe in 79,999 scenes |
| `AddAltitudeCorridorSystem()` | 499-553 | Belongs in Bootstrap scene |
| `AddCloudSystem()` | 555-574 | Belongs in Bootstrap scene |
| `CreatePlayerSpawnPoint()` | 576-590 | **WRONG** - NetworkPlayer spawns via NetworkManager |
| `CreatePlayerPrefab()` | 592-615 | **WRONG** - Creates duplicate player in every scene |

### What Was KEPT:

| Component | Lines | Why Correct |
|-----------|-------|-------------|
| **GroundPlane** | 191-210 | World geometry only |
| **DirectionalLight** | 174-188 | Per-scene lighting |
| **SceneLabel** (TMPro) | 260-275 | Debug visualization |
| **Boundary Colliders** | 278-374 | Scene transition walls + pole blockers |
| **SceneRegistry creation** | 376-401 | ScriptableObject config |

### What Changed:

1. **Removed toggles:** `_addRuntimeSetup`, `_addPlayerSpawnPoint`
2. **Removed unused imports:** Unity.Netcode, ProjectC.World.Streaming, ProjectC.Player, ProjectC.Ship
3. **Cleaned imports:** Only ProjectC.World.Scene remains

### BootstrapSceneGenerator: NEW

**Menu:** `ProjectC → World → Generate Bootstrap Scene`
**Output:** `Assets/_Project/Scenes/BootstrapScene.unity`

Creates a persistent Bootstrap scene with all runtime components:

| Component | Purpose |
|-----------|---------|
| NetworkManager + NetworkManagerController | Networking core |
| ServerSceneManager | Server-side scene tracking |
| ClientSceneLoader | Client-side additive scene loading (DontDestroyOnLoad) |
| SceneTransitionCoordinator | RPC coordination |
| WorldStreamingManager + Chunk components | Chunk streaming |
| MainCamera + FloatingOriginMP | Camera with floating origin |
| AltitudeCorridorSystem | Flight altitude zones |
| CloudSystem | Cloud layers |
| NetworkTestMenu | Host/Client/Server/LoadWorld buttons |

**Fixes applied:** UnityTransport namespace, DontDestroyOnLoad method call, AltitudeCorridor namespaces, UnityEngine.UI usings, Transform signatures. See `BOOTSTRAP_GENERATOR_FIXES.md` for details.

**Runtime persistence:** `ClientSceneLoader` now calls `DontDestroyOnLoad(gameObject)` in its own `Awake()` — editor scripts must NOT call DontDestroyOnLoad.

---

## Correct Scene Architecture

### World Scene (Generated) — CONTAINS ONLY:
```
WorldScene_X_Y.unity
├── WorldRoot_X_Y (empty root)
│   ├── DirectionalLight
│   ├── GroundPlane_X_Y
│   ├── SceneLabel_X_Y (TMPro)
│   └── Boundaries_X_Y
│       ├── North (collider)
│       ├── South (collider)
│       ├── East (collider)
│       ├── West (collider)
│       ├── SouthPoleBlocker (if row=0)
│       └── NorthPoleBlocker (if row=3)
└── (NO managers, NO player spawns, NO FloatingOriginMP)
```

### Bootstrap Scene (Manual Setup) — CONTAINS:
```
Bootstrap.unity
├── NetworkManager
│   ├── ServerSceneManager
│   └── SceneTransitionCoordinator
├── ClientSceneLoader (DontDestroyOnLoad)
├── WorldStreamingManager
├── AltitudeCorridorSystem
├── CloudSystem
└── Main Camera
```

---

## Critical Implementation Notes

1. **Scene preload triggers at 10,000 units from boundary**
2. **Client loads 3×3 scene neighborhood (9 scenes max)**
3. **Unload scenes outside 5×5 radius (hysteresis)**
4. **NetworkPlayer never shifted by FloatingOriginMP**
5. **TradeZones excluded from FloatingOriginMP shift**
6. **Overlap: 1,600 units (2%) for terrain seamlessness**
7. **Clouds/farms NOT generated in overlap zones**

---

## File Map

### Scene Scripts
| File | Purpose |
|------|---------|
| `SceneID.cs` | Scene identifier + coordinate conversions |
| `SceneRegistry.cs` | ScriptableObject with 6×4 grid config |
| `ServerSceneManager.cs` | Server-side scene tracking + RPC |
| `ClientSceneLoader.cs` | Client-side additive scene loading |
| `SceneBoundNetworkObject.cs` | CheckObjectVisibility implementation |
| `SceneTransitionCoordinator.cs` | RPC coordination layer |

### Streaming Scripts
| File | Purpose |
|------|---------|
| `FloatingOriginMP.cs` | Large coord handling (NOT needed in scenes) |
| `PlayerChunkTracker.cs` | Server tracks client chunk positions |
| `ChunkNetworkSpawner.cs` | Spawns network objects per chunk |
| `ChunkLoader.cs` | Async chunk loading with fade |
| `ProceduralChunkGenerator.cs` | Terrain/clouds generation |

### Editor Scripts
| File | Purpose |
|------|---------|
| `WorldSceneGenerator.cs` | **REFACTORED** - Now generates only scene geometry |

---

## Answer to Core Questions

**Q: Do we need FloatingOriginMP enabled in these 24 scenes if we don't move beyond 79,999 and just load new scenes?**

**A: NO — FloatingOriginMP is NOT needed inside scenes.**

The scene-based approach with 79,999 limit **is** the solution to the floating origin problem, not something that requires floating origin.

**Q: Where should runtime components (WorldSceneManager, ClientSceneLoader, etc.) live?**

**A: In the Bootstrap/Menu scene, NOT in world scenes.**

World scenes are "dumb containers" — they contain geometry only. Runtime components live in the persistent Bootstrap scene.

**Q: How do players spawn?**

**A: Via NetworkManager, not per-scene spawn points.**

NetworkPlayer spawns through the standard NGO spawn system, not via pre-placed spawner objects in each scene.