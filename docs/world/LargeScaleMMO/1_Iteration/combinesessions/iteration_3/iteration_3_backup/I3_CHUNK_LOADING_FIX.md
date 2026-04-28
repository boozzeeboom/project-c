# FIX I3-002: Chunk Loading Around Player (Not Spawn)

## Problem

Chunks loaded around spawn position (0,0,0) instead of around actual player position.

**Symptoms:**
1. Player moves 100,000 units away from spawn
2. F7 debug shows chunks loading around spawn (0,0,0)
3. HUD shows "loaded chunks 20" near spawn
4. Player moves further - chunks unload, loaded chunks = 0
5. F7 shows chunks loading at spawn again, not at player

## Root Cause Analysis

### Problem 1: WorldStreamingManager used Camera.main

**Location:** `WorldStreamingManager.UpdateStreaming()` (OLD code)
```csharp
private void UpdateStreaming()
{
    Camera mainCamera = Camera.main;
    if (mainCamera != null)
    {
        LoadChunksAroundPlayer(mainCamera.transform.position); // WRONG!
    }
}
```

**Why it's wrong:**
- `Camera.main` returns camera tagged "MainCamera"
- In this project, camera is under TradeZones (which shifts with FloatingOriginMP)
- Camera position = `(0,0,0)` when world shifts
- Chunks load around (0,0,0) not player

### Problem 2: Same issue in UpdatePreload()

```csharp
private void UpdatePreload()
{
    Camera mainCamera = Camera.main; // WRONG!
    // ...
    Vector3 playerPos = mainCamera.transform.position; // WRONG!
}
```

## Solution

### New methods added to WorldStreamingManager

1. `GetPlayerPositionForChunking()` - Gets player position for chunk streaming
2. `FindLocalPlayerTransform()` - Reliable NGO player finding using:
   - Priority 1: `NetworkManager.Singleton.LocalClient.PlayerObject`
   - Priority 2: NetworkPlayer with `IsOwner`
   - Priority 3: Object with "Player" tag

### Caching for performance

```csharp
private Transform _cachedLocalPlayerTransform;
private float _lastPlayerSearchTime = 0f;
private const float PLAYER_SEARCH_INTERVAL = 1f; // Search every 1 second
```

## Changes Made

1. **UpdateStreaming()** - Now uses `GetPlayerPositionForChunking()` instead of `Camera.main`
2. **UpdatePreload()** - Now uses `GetPlayerPositionForChunking()` instead of `Camera.main`
3. **Added** `GetPlayerPositionForChunking()` method
4. **Added** `FindLocalPlayerTransform()` method
5. **Added** caching variables for player transform

## Testing

### Test F7 after fix:
1. Player spawns at (0,0,0)
2. Move 100,000 units away
3. Press F7
4. Expected: Chunks load around PLAYER position, not spawn
5. HUD shows chunks near player, not near (0,0,0)

---

**Date:** 19.04.2026  
**Version:** 1.0  
**Status:** IMPLEMENTED