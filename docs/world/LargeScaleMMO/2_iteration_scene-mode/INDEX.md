# Iteration 2: Scene-Based World Streaming

## Overview
This iteration implements a 24-scene world (4x6 grid) with additive scene loading. Each scene is 79,999 x 79,999 units. System loads current scene + 1 preloaded neighbor based on player position.

## Documentation

| File | Description |
|------|-------------|
| `INDEX.md` | This file - overview and navigation |
| `COMPLETION_REPORT.md` | Full summary of implementation, fixes, and current state |
| `SCENE_ARCHITECTURE_DECISION.md` | Architecture decision record for scene-based approach |
| `WORLD_STREAMING_SYSTEM.md` | Detailed world streaming system design |

## Architecture

### World Grid
- **Grid:** 4 rows x 6 columns = 24 scenes
- **Scene Size:** 79,999 x 79,999 units
- **Total World:** ~480,000 x ~320,000 units

### Loading Strategy
- Current scene only (no 3x3 loading)
- Preload next scene when within 10,000 units of boundary
- Unload scenes >10,000 units from player
- Maximum 4 loaded scenes at once

## Key Files

### Scripts
- `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` - Main loader
- `Assets/_Project/Scripts/World/Scene/SceneID.cs` - Scene coordinates
- `Assets/_Project/Scripts/World/Scene/SceneRegistry.cs` - Scene metadata

### Scene Files
- `Assets/_Project/Scenes/World/WorldScene_X_Z.unity` - 24 scenes

## Status: ✅ Complete

Scene loading/unloading works correctly:
- Player position correctly tracked
- Scene boundaries detected
- Preload triggers at boundary approach
- Distant scenes properly unloaded
