# ProjectC - Technical Overview
**Generated:** 2026-04-29
**Project:** ProjectC_client

## 1. Project Structure

```
Assets/_Project/
├── Scripts/
│   ├── Core/              # Core systems (Network, Clouds, etc.)
│   ├── Player/           # Player-related scripts
│   ├── Items/            # Item/inventory systems
│   ├── Trade/            # Trading system
│   ├── World/            # World management
│   │   ├── Scene/        # Scene loading system (world streaming)
│   │   └── Streaming/    # Chunk-level streaming
│   ├── UI/               # User interface
│   ├── Network/          # Network-specific scripts
│   └── Editor/           # Editor utilities
├── Scenes/
│   ├── BootstrapScene.unity
│   └── World/            # 24 world scenes (4x6 grid, currently 4x4 populated)
└── Prefabs/
```

## 2. Core Architecture

### Network System (Netcode for GameObjects)
- **NetworkManagerController** - Central network management
- Supports Host/Client/Server modes
- Uses UnityTransport for connectivity
- Singleton pattern via `Unity.Netcode.NetworkManager.Singleton`

### Scene System (Grid-Based World Streaming)
- **SceneID** - Coordinates in world grid (GridX, GridZ)
- **SceneRegistry** - ScriptableObject defining grid dimensions
- **ClientSceneLoader** - Loads scenes client-side based on player position
- **ServerSceneManager** - Server-side tracking and RPC coordination

### World Coordinate System
- Scene Size: 79,999 units
- Overlap: 1,600 units (for seamless transitions)
- Scene coordinates to world: `WorldX = GridX * 79999`

### Chunk System (Underlying)
- Chunk size: 2,000 x 2,000 units
- Managed by `WorldStreamingManager` and `PlayerChunkTracker`
- Sits beneath the scene layer

## 3. Build Status

| Component | Status | Notes |
|-----------|--------|-------|
| Network Host | Working | After recent fixes |
| Scene Loading | Partially Working | Some scenes missing from build |
| Scene Unloading | Not Tested | Likely broken |
| Preload System | Not Tested | Likely broken |
| Cloud System | Not Working | Configs not assigned |

## 4. Scene Grid Issue

**Current Configuration (SceneRegistry):**
- GridColumns: 6 (X: 0-5)
- GridRows: 4 (Z: 0-3)

**Available Scenes in Project:**
- Only 24 scenes exist: WorldScene_0_0 through WorldScene_3_5
- No scenes with GridX >= 4 or GridZ >= 4

**Error Triggered:** When player moves to world positions that map to non-existent scenes (e.g., position x=350000 maps to GridX=4)

**Recommended Fix:** Reduce `SceneRegistry.GridColumns` from 6 to 4

## 5. Key Files Reference

| File | Purpose |
|------|---------|
| `NetworkManagerController.cs:244` | StartHost() - network initialization |
| `ClientSceneLoader.cs:245` | LoadSceneCoroutine - scene load orchestration |
| `ServerSceneManager.cs:185` | CheckSceneTransition - clamps invalid coordinates |
| `WorldSceneManager.cs:149` | HandleSceneLoaded - tracks loaded scenes |
| `SceneID.cs:37` | FromWorldPosition - converts world to grid coords |

## 6. Recent Changes (2026-04-29)

1. Fixed NullReferenceException in NetworkManagerController.Awake()
2. Fixed null checks in ServerSceneManager.CheckSceneTransition()
3. Fixed null handling in ClientSceneLoader.LoadSceneCoroutine()
4. Added coordinate clamping to prevent invalid scene lookups
5. Fixed TransitionClient to use TryGetValue for player transforms

## 7. Next Steps

1. **Immediate:** Fix SceneRegistry grid mismatch (6x4 -> 4x4)
2. **Immediate:** Add null-handling in LoadSceneAsync for missing scenes
3. **Medium:** Implement proper scene reference counting
4. **Medium:** Add debug tools to visualize loaded scenes
5. **Long:** Consider Addressables migration for scene loading