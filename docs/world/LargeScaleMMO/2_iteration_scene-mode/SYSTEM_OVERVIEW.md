# 24+1 Scene World System

**Project:** ProjectC Client
**Date:** May 1, 2026
**Status:** ✅ Implemented and Working

---

## Overview

The world consists of **24 scenes** arranged in a **4×6 grid** (4 rows × 6 columns), plus 1 Bootstrap scene. Each scene is **79,999 × 79,999 units**. Total world size: ~480,000 × ~320,000 units.

```
Grid Layout (Z is north-south, X is east-west):

    X→0     X→1     X→2     X→3     X→4     X→5
  ┌───────┬───────┬───────┬───────┬───────┬───────┐
Z→0 │ 0_0  │ 1_0  │ 2_0  │ 3_0  │ 4_0  │ 5_0  │
  ├───────┼───────┼───────┼───────┼───────┼───────┤
Z→1 │ 0_1  │ 1_1  │ 2_1  │ 3_1  │ 4_1  │ 5_1  │
  ├───────┼───────┼───────┼───────┼───────┼───────┤
Z→2 │ 0_2  │ 1_2  │ 2_2  │ 3_2  │ 4_2  │ 5_2  │
  ├───────┼───────┼───────┼───────┼───────┼───────┤
Z→3 │ 0_3  │ 1_3  │ 2_3  │ 3_3  │ 4_3  │ 5_3  │
  └───────┴───────┴───────┴───────┴───────┴───────┘
```

## Scene Loading Strategy

### "1+1" Model
- **Current:** Only the scene where player is located
- **Preload:** One adjacent scene when approaching boundary (within 10,000 units)
- **Max loaded:** 4 scenes at any time
- **Unload:** Scenes >10,000 units from player are unloaded

### Boundary Detection
- Scene boundaries at: z=79999, 159998, 239997, 319996 (Z-axis)
- Scene boundaries at: x=79999, 159998, 239997, 319996, 399995 (X-axis)

### Scene Calculation
```csharp
SceneID.FromWorldPosition(position)
// Returns Scene(gridX, gridZ) based on world position
// gridX = floor(worldPos.x / 79999f)
// gridZ = floor(worldPos.z / 79999f)
```

## Components

### ClientSceneLoader.cs
Main client-side scene loader handling:
- Scene boundary detection
- Preload triggering
- Distance-based unloading
- Scene state management

**Key Settings:**
- `preloadDistance = 10000` - Start preloading at 10k from boundary
- `unloadDistance = 10000` - Unload scene if player >10k away
- `maxLoadedScenes = 4` - Maximum concurrent loaded scenes

### SceneID.cs
Struct representing scene coordinates:
```csharp
public struct SceneID
{
    public int GridX;  // Column (0-5)
    public int GridZ;  // Row (0-3)
}
```

### SceneRegistry.cs
ScriptableObject containing:
- Grid dimensions (6 columns × 4 rows)
- Scene naming prefix ("WorldScene_")
- Helper methods: `GetSceneGrid3x3()`, `GetSceneGrid5x5()`, `IsValid()`

## Scene Files

**Location:** `Assets/_Project/Scenes/World/`

**Naming Convention:** `WorldScene_{GridX}_{GridZ}.unity`

**Examples:**
- `WorldScene_0_0.unity` - Scene at column 0, row 0
- `WorldScene_2_3.unity` - Scene at column 2, row 3
- `WorldScene_5_3.unity` - Scene at column 5, row 3 (last one)

## Player Position Tracking

Uses **PlayerSpawner** object (tagged "Player") for world position because:
- PlayerSpawner gets shifted by FloatingOriginMP when world shifts
- NetworkPlayer(Clone) stays at local origin (~40,000)

**Why PlayerSpawner:**
- Scene(0,0) center: (39999, 3000, 39999)
- When player flies to z=166,000 (Scene 0,2), PlayerSpawner is at (39999, 3000, 166,000)
- NetworkPlayer(Clone) stays at (~40000, 3000, ~40000) but PlayerSpawner moves with world

## Loading Flow

```
1. Player crosses scene boundary
   ↓
2. SceneID.FromWorldPosition() calculates new scene
   ↓
3. Scene mismatch detected (_currentScene ≠ playerScene)
   ↓
4. LoadSceneBoundaryBased() called
   - Load new scene
   - Set _currentScene to new scene
   - Call UnloadDistantScenes()
   ↓
5. Every frame in Update():
   - Check if approaching boundary → preload adjacent scene
   - Check distant scenes → unload if >10k away
```

## Key Methods

| Method | Purpose |
|--------|---------|
| `Update()` | Main loop - boundary detection, preload/unload |
| `LoadSceneBoundaryBased()` | Loads current scene, unloads distant |
| `CalculatePreloadScene()` | Returns adjacent scene to preload |
| `CheckDistanceBasedUnload()` | Unloads scenes >10k from player |
| `ManageLoadedScenesCount()` | Ensures max 4 scenes loaded |
| `LoadSceneAsync()` | Async scene loading via SceneManager |
| `UnloadSceneCoroutine()` | Async scene unloading |

## FloatingOriginMP Interaction

- **PlayerSpawner** is in `excludeFromShift` list - not shifted with world roots
- **NetworkPlayer(Clone)** is in `excludeFromShift` list - not shifted
- This means PlayerSpawner tracks actual world position while NetworkPlayer tracks local position
- CSL uses PlayerSpawner for correct world position tracking

## Testing

1. Launch as Host
2. Player spawns at Scene(0,0) center: (39999, 3000, 39999)
3. Fly to z=80000+ → Scene(0,1) loads, Scene(0,0) unloads
4. HUD shows: Player Scene, Loaded count, Player Position
5. Console logs: preload triggers, distance-based unloads

## File Structure

```
Assets/_Project/Scripts/World/Scene/
├── ClientSceneLoader.cs    # Main loader
├── SceneID.cs             # Scene coordinate struct
└── SceneRegistry.cs      # Scene metadata

Assets/_Project/Scenes/World/
├── WorldScene_0_0.unity
├── WorldScene_0_1.unity
├── ...
└── WorldScene_5_3.unity   # 24 scenes total
```

## Related Systems

- **WorldStreamingManager** - coordinates chunk loading
- **ChunkLoader** - manages terrain chunks within scenes
- **FloatingOriginMP** - handles large coordinate precision
- **NetworkPlayer** - player controller with teleport RPC
- **SceneDebugHUD** - debug UI showing loaded scenes

## Known States

| Item | Status |
|------|--------|
| Scene boundary detection | ✅ Working |
| Scene preloading | ✅ Working |
| Distance-based unload | ✅ Working |
| Position correction disabled | ✅ Working (for testing) |
| Spawn at Y=3000 | ✅ Working (for testing) |
| Scene X/Z naming | ✅ Fixed |

## Git Commits

```
9b36e47 - Iteration 2 complete: Scene-based world streaming
01ed79e - New strategy: boundary-based scene loading
d8fdcce - Fix: distance-based unloading
3531640 - Fix: Disable position correction
2438a34 - Fix: FindLocalPlayer uses PlayerSpawner
7eda914 - Fix: ClientSceneLoader singleton
```

---

**Next Steps:**
- Revert spawn Y from 3000 to 3 for normal gameplay
- Implement proper position correction for multiplayer
- Address visual delay in chunk loading
