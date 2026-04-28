# Test Workflow: 24 Scenes

**Date:** 28.04.2026
**Updated:** After adding BootstrapSceneGenerator

---

## Problem

After running `WorldSceneGenerator`, hitting Play shows no camera and no player because:
- World scenes contain **geometry only** (correct for scene-based architecture)
- No Bootstrap/Menu scene exists with runtime components

---

## Solution: Generate Bootstrap Scene First

**Workflow Order:**

```
1. Generate Bootstrap Scene ← NEW
2. Generate 24 World Scenes
3. Set BootstrapScene as first in Build Settings
4. Play
```

---

## Step-by-Step Instructions

### Step 1: Generate Bootstrap Scene

**Menu:** `ProjectC → World → Generate Bootstrap Scene`

This creates `Assets/_Project/Scenes/BootstrapScene.unity` with:
- NetworkManager + NetworkManagerController
- ServerSceneManager
- ClientSceneLoader (DontDestroyOnLoad)
- SceneTransitionCoordinator
- WorldStreamingManager + Chunk components
- MainCamera + FloatingOriginMP
- AltitudeCorridorSystem
- CloudSystem
- WorldRoot (with sub-containers)
- NetworkTestMenu (Host/Client/Server buttons + Load World button)

### Step 2: Generate 24 World Scenes

**Menu:** `ProjectC → World → Generate World Scenes`

This creates 24 scenes in `Assets/_Project/Scenes/World/`:
- WorldScene_0_0 through WorldScene_3_5
- Each contains: GroundPlane, DirectionalLight, SceneLabel, Boundaries

### Step 3: Configure Build Settings

1. Open `File → Build Settings`
2. Drag `BootstrapScene.unity` to top (index 0)
3. Ensure all `WorldScene_X_Y.unity` scenes are added
4. Click "Build" or "Play"

### Step 4: Test

1. Click Play in Editor
2. NetworkTestMenu appears
3. Click **"Host"** to start as host+client
4. Click **"Load World [0,0]"** to load the first world scene
5. Player spawns and can fly around

---

## Scene Architecture (Corrected)

### Bootstrap Scene (Entry Point)
```
BootstrapScene.unity
├── NetworkManager
│   └── NetworkManagerController
├── Runtime
│   ├── ClientSceneLoader (DontDestroyOnLoad)
│   ├── ServerSceneManager
│   └── SceneTransitionCoordinator
├── WorldStreamingManager
│   ├── WorldChunkManager
│   ├── ChunkLoader
│   ├── ProceduralChunkGenerator
│   ├── PlayerChunkTracker
│   └── ChunkNetworkSpawner
├── MainCamera
│   └── FloatingOriginMP
├── AltitudeCorridorSystem
├── CloudSystem
├── WorldRoot
│   ├── Mountains
│   ├── Clouds
│   ├── Farms
│   ├── TradeZones
│   └── ChunksContainer
├── EventSystem
└── NetworkTestCanvas
    └── NetworkTestMenu
```

### World Scenes (Content Only)
```
WorldScene_X_Y.unity
├── WorldRoot_X_Y
│   ├── DirectionalLight
│   ├── GroundPlane_X_Y
│   ├── SceneLabel_X_Y (TMPro, debug)
│   └── Boundaries_X_Y
│       ├── North/South/East/West (colliders)
│       ├── SouthPoleBlocker (if row=0)
│       └── NorthPoleBlocker (if row=3)
```

---

## Key Files Created/Modified

| File | Action | Purpose |
|------|--------|---------|
| `BootstrapSceneGenerator.cs` | CREATED | Generates Bootstrap scene |
| `BootstrapSceneGenerator.cs` | FIXED | UnityTransport, AltitudeCorridor, UI namespaces |
| `WorldSceneGenerator.cs` | REFACTORED | Generates 24 world scenes (geometry only) |
| `ClientSceneLoader.cs` | MODIFIED | Added `LoadSceneOnly()` and `LoadInitialScene()` for testing |

---

## FloatingOriginMP in Bootstrap

**Important:** FloatingOriginMP IS in Bootstrap scene, but:
- Threshold set to **150,000** (never triggers inside 79,999 scenes)
- Only shifts WorldRoot objects
- Player/NetworkPlayer are NOT shifted
- For 24 scenes (79,999 each), **FloatingOriginMP never activates**

FloatingOriginMP is kept in Bootstrap for edge cases:
- If somehow player reaches world coords >100,000
- For future expansion beyond 24 scenes

---

## Network Flow

```
User clicks "Host"
    ↓
NetworkManager.StartHost()
    ↓
ServerSceneManager tracks client
    ↓
User clicks "Load World [0,0]"
    ↓
ClientSceneLoader.LoadInitialScene(new SceneID(0,0))
    ↓
Loads WorldScene_0_0 + neighbors (3×3 grid)
    ↓
Player spawns at world position (0, 3000, 0)
    ↓
Player moves → ClientSceneLoader detects boundary
    ↓
ServerSceneManager coordinates transition
    ↓
NetworkHide/NetworkShow for objects
```

---

## Troubleshooting

### "No camera" after Play
- Did you generate Bootstrap scene first?
- Is BootstrapScene first in Build Settings?

### "Player not found" warnings
- ClientSceneLoader searches for NetworkPlayer via:
  1. `NetworkManager.Singleton.IsOwner` (if networking active)
  2. GameObject with tag "Player"
- Make sure player spawned via NetworkManager

### Scenes don't load
- Check SceneRegistry exists: `Assets/_Project/Data/Scene/SceneRegistry.asset`
- Check scene names match: `WorldScene_0_0`, `WorldScene_0_1`, etc.
- Check Console for "Invalid target scene" warnings

---

## Related Documents

- `GRAPH_REPORT.md` - Full architecture analysis
- `WORLD_SCENE_GENERATOR_REFACTORING.md` - Generator changes explanation
- `SCENE_ARCHITECTURE_DECISION.md` - Why 79,999 scenes, FloatingOriginMP not needed