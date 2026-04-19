# I5-001 FIX: Chunk Loading Around Player - COMPLETED

## Status
**VERSION:** 1.0
**DATE:** 19.04.2026 10:40
**STATUS:** ✅ FIXED

---

## Problem

ChunkLoader loads chunks around spawn position (origin) instead of around player position.

### Symptoms
1. Player moves 100,000 units away from spawn
2. F7 debug shows chunks loading around spawn (0,0,0)
3. HUD shows "loaded chunks 0" - chunks don't load!
4. After teleport, chunks load at spawn again

### Root Cause

**`WorldStreamingManager.UpdateStreaming()` and `UpdatePreload()` used `Camera.main.transform.position`:**

```
AFTER FLOATINGORIGINMP SHIFT:
  Player: (100000, 5, 500000)
  Camera: (0, 100, 0) <- under TradeZones, EXCLUDED from shift!
  
  LoadChunksAroundPlayer(Camera) → (0, 100, 0) → origin → WRONG!
```

---

## Fixes Applied

### 1. WorldStreamingManager.cs

**Added `GetLocalPlayerPosition()` method:**

```csharp
private Vector3 GetLocalPlayerPosition()
{
    // Priority 1: NetworkPlayer with IsOwner
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
    {
        var networkObjects = FindObjectsByType<Unity.Netcode.NetworkObject>();
        foreach (var netObj in networkObjects)
        {
            if (netObj.IsOwner && netObj.name.Contains("NetworkPlayer"))
            {
                _cachedLocalPlayerTransform = netObj.transform;
                return netObj.transform.position;
            }
        }
    }
    
    // Priority 2: Object with "Player" tag
    GameObject playerByTag = GameObject.FindGameObjectWithTag("Player");
    if (playerByTag != null)
    {
        _cachedLocalPlayerTransform = playerByTag.transform;
        return playerByTag.transform.position;
    }
    
    // Priority 3: Camera fallback
    return Camera.main.transform.position;
}
```

**Fixed `UpdateStreaming()`:**
```csharp
private void UpdateStreaming()
{
    Vector3 playerPosition = GetLocalPlayerPosition(); // I5-001 FIX
    if (playerPosition != Vector3.zero || _cachedLocalPlayerTransform != null)
    {
        LoadChunksAroundPlayer(playerPosition);
    }
    UpdatePreload();
}
```

**Fixed `UpdatePreload()`:**
```csharp
private void UpdatePreload()
{
    Vector3 playerPos = GetLocalPlayerPosition(); // I5-001 FIX
    if (playerPos == Vector3.zero || chunkManager == null) return;
    // ...
}
```

### 2. StreamingTest_AutoRun.cs

**Added `GetPlayerPosition()` method and fixed F7:**

```csharp
private Vector3 GetPlayerPosition()
{
    // Priority 1: NetworkPlayer with IsOwner
    var networkObjects = FindObjectsByType<Unity.Netcode.NetworkObject>();
    foreach (var netObj in networkObjects)
    {
        if (netObj.IsOwner && netObj.name.Contains("NetworkPlayer"))
            return netObj.transform.position;
    }
    
    // Priority 2: Object with "Player" tag
    GameObject playerByTag = GameObject.FindGameObjectWithTag("Player");
    if (playerByTag != null)
        return playerByTag.transform.position;
    
    // Priority 3: Camera fallback
    return _mainCamera.transform.position;
}
```

**F7 now uses player position:**
```csharp
if (f7Pressed)
{
    Vector3 playerPosition = GetPlayerPosition(); // I5-001 FIX
    streamingManager.LoadChunksAroundPlayer(playerPosition);
}
```

---

## Files Changed

| File | Change |
|------|--------|
| `WorldStreamingManager.cs` | Added GetLocalPlayerPosition(), fixed UpdateStreaming(), UpdatePreload() |
| `StreamingTest_AutoRun.cs` | Added GetPlayerPosition(), fixed F7 handler |

---

## Testing

1. Enable `showDebugHUD = true` in WorldStreamingManager
2. Teleport 100,000 units away from spawn
3. Check HUD: `Cached Player` should show player name (not NULL)
4. Press F7 - chunks should load around player
5. Check logs: should show "Using NetworkPlayer IsOwner" or "Using Player tag"

---

## Author
Claude Code (Subagent Analysis + Implementation)
**Date:** 19.04.2026 10:40 MSK