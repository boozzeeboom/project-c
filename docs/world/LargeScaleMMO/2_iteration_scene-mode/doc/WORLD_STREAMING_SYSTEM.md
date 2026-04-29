# World Streaming System - Architecture Report
**Generated:** 2026-04-29
**Project:** ProjectC_client

## 1. System Overview

The World Streaming System manages loading/unloading of world scenes in a grid-based world (6 columns x 4 rows = 24 scenes total). It uses additive scene loading to keep multiple scenes in memory.

### Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ClientSceneLoader` | `Scripts/World/Scene/ClientSceneLoader.cs` | Loads scenes on client based on player position |
| `ServerSceneManager` | `Scripts/World/Scene/ServerSceneManager.cs` | Tracks client positions and coordinates scene transitions on server |
| `WorldSceneManager` | `Scripts/World/WorldSceneManager.cs` | Central coordinator, handles preloading, keeps scene refs |
| `SceneRegistry` | `Scripts/World/Scene/SceneRegistry.cs` | ScriptableObject defining grid size (6x4) and scene naming |
| `SceneID` | `Scripts/World/Scene/SceneID.cs` | Struct identifying scenes by GridX/GridZ coordinates |

### Scene Grid Configuration

- **GridColumns:** 6 (X dimension: 0-5)
- **GridRows:** 4 (Z dimension: 0-3)
- **Scene Size:** 79,999 units (with 1,600 unit overlap)
- **Scene Naming:** `WorldScene_{GridX}_{GridZ}` (e.g., `WorldScene_0_0`)

### Available Scenes (24 total)

```
WorldScene_0_0 through WorldScene_0_5 (6 scenes)
WorldScene_1_0 through WorldScene_1_5 (6 scenes)
WorldScene_2_0 through WorldScene_2_5 (6 scenes)
WorldScene_3_0 through WorldScene_3_5 (6 scenes)
```

**Note:** Scenes WorldScene_4_* and above are referenced in code but NOT present in the project.

## 2. Known Issues

### Issue #1: Missing Scenes in Build Settings
**Severity:** High
**Error:** `Scene 'WorldScene_4_2' couldn't be loaded because it has not been added to the active build profile`

**Root Cause:** The scene grid in `SceneRegistry` is configured as 6x4, but only 4 rows (0-3) of scenes exist. When player moves to positions where `SceneID.GridX >= 4` or `SceneID.GridZ >= 4`, the system tries to load non-existent scenes.

**Affected Scenes:** WorldScene_4_* (columns 4), WorldScene_*_4 (rows 4) - none exist

**Fix Options:**
1. Reduce `SceneRegistry.GridColumns` from 6 to 4 (matching actual scenes)
2. Generate missing scenes (WorldScene_4_0 through WorldScene_4_5 and WorldScene_*_4)
3. Clamp player position to valid grid range in world bounds

### Issue #2: NullReferenceException in LoadSceneAsync
**Severity:** High
**Error:** `NullReferenceException: Object reference not set to an instance of an object at ClientSceneLoader+<LoadSceneAsync>d__38.MoveNext ()`

**Location:** `ClientSceneLoader.cs:316`

**Root Cause:** When `SceneManager.LoadSceneAsync` fails (e.g., scene doesn't exist), `_loadedScenes` is not populated but `_loadingScenes` may be cleared elsewhere, causing inconsistent state. The subsequent `OnSceneLoaded?.Invoke(sceneId)` may receive invalid data.

**Fix:** Add null-check and proper error handling in LoadSceneAsync

### Issue #3: Scenes Not Unloading Properly
**Severity:** Medium
**Observation:** Old scenes remain loaded when player moves to new areas

**Root Cause:** `UnloadDistantScenes` logic may not be executing correctly, or scene references are being held elsewhere

### Issue #4: Preload Not Working
**Severity:** Medium
**Observation:** Preload triggers fire but scenes don't load in time

**Root Cause:** Preload logic checks against `IsValid` which rejects out-of-range scenes before attempting load

## 3. Data Flow

```
[Player Moves]
    -> ClientSceneLoader.Update() detects scene boundary crossing
    -> ClientSceneLoader.LoadScene(targetScene, localSpawnPos)
    -> ServerSceneManager.CheckSceneTransition() also tracks on server
    -> ServerSceneManager.TransitionClient() sends RPC to client
    -> ClientSceneLoader.LoadSceneCoroutine() receives RPC
    -> LoadSceneWithNeighborsCoroutine() loads 3x3 grid
    -> LoadSceneAsync() calls SceneManager.LoadSceneAsync
    -> OnSceneLoaded callback fires
    -> WorldSceneManager.HandleSceneLoaded() tracks total loaded
```

## 4. Scene Transition Logic

### Server Side (ServerSceneManager)
- Tracks each client's current scene in `_clientSceneMap`
- Checks player position every `updateInterval` (0.5s default)
- Uses `SceneID.FromWorldPosition()` to convert world coords to grid coords
- Clamped to valid range in recent fix (lines 185-194 of ServerSceneManager.cs)
- Sends `SceneTransitionData` via `LoadSceneTransitionClientRpc`

### Client Side (ClientSceneLoader)
- Receives transition RPC with target scene and local spawn position
- Checks if scene already loaded or loading before attempting
- Loads scene with `LoadSceneMode.Additive`
- Loads 3x3 neighbor scenes via `LoadSceneWithNeighborsCoroutine`
- Unloads distant scenes via `UnloadDistantScenes(center)` if enabled

## 5. Files Reference

### Core Files
- `Assets/_Project/Scripts/World/Scene/SceneID.cs` - SceneID struct, SceneTransitionData
- `Assets/_Project/Scripts/World/Scene/SceneRegistry.cs` - Grid config ScriptableObject
- `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` - Client-side scene loading
- `Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs` - Server-side scene tracking
- `Assets/_Project/Scripts/World/WorldSceneManager.cs` - Central coordinator

### Related Systems
- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs` - Chunk-level streaming
- `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs` - Chunk tracking per player
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` - Network host/client management

## 6. Recommendations

### Immediate Fixes
1. **Reduce SceneRegistry grid to 4x4** to match actual available scenes, OR generate missing columns/rows
2. **Add null-check in LoadSceneAsync** to handle missing scenes gracefully
3. **Verify scene unloading** - check `_loadedScenes` cleanup on scene unload

### Medium Term
1. Implement proper scene reference counting
2. Add debug HUD showing which scenes are currently loaded
3. Add frame-by-frame scene loading visualization

### Long Term
1. Consider Addressables-based scene loading for better memory management
2. Implement LOD system for distant scene content
3. Add scene preload prediction based on player velocity