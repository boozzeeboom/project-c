# I5-001 FIX: Chunk Loading Around Player - FINAL

## Status
**VERSION:** 2.0
**DATE:** 19.04.2026 10:52 MSK
**STATUS:** ✅ FIXED (Partially)

---

## Problem (Original)

ChunkLoader loads chunks around spawn position (origin) instead of around player position.

### Symptoms (Original)
1. Player moves 100,000 units away from spawn
2. F7 debug shows chunks loading around spawn (0,0,0)
3. HUD shows "loaded chunks 0" - chunks don't load!

### Root Cause (Original)

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

---

## NEW ISSUE DISCOVERED: WorldData Bounds Too Small

### Test Results (19.04.2026 10:48)
After fixing player position:
```
World bounds: X[-5500..2500], Z[-3500..5500]
Player position: (896, 3, -255627) → Chunk(0, -128)
Registry only has 25 chunks (GridX[-3..1], GridZ[-2..1])
Player's Z=-255627 is WAY outside world bounds Z[-3500..5500]!
```

**Problem:** `GetChunksInRadius()` returns 0 chunks because player is outside WorldData bounds.

### Fix: WorldChunkManager.cs

**Modified `GetChunksInRadius()` to return on-demand chunks:**

```csharp
public List<ChunkId> GetChunksInRadius(Vector3 centerPos, int radiusInChunks)
{
    var result = new List<ChunkId>();
    ChunkId centerChunk = GetChunkAtPosition(centerPos);

    for (int x = centerChunk.GridX - radiusInChunks; x <= centerChunk.GridX + radiusInChunks; x++)
    {
        for (int z = centerChunk.GridZ - radiusInChunks; z <= centerChunk.GridZ + radiusInChunks; z++)
        {
            ChunkId candidate = new ChunkId(x, z);
            
            // I5-001 FIX: First check if chunk exists in registry
            if (_chunkRegistry.TryGetValue(candidate, out var existingChunk))
            {
                result.Add(candidate);
            }
            else
            {
                // I5-001 FIX: Create on-demand for procedurally generated world
                // This allows streaming even if WorldData bounds are smaller than the actual world
                result.Add(candidate);
            }
        }
    }

    return result;
}
```

---

## NEW ISSUE: ChunkLoader Doesn't Find Chunk

### Error
```
[ChunkLoader] Чанк Chunk(36, -209) не найден в WorldChunkManager.
```

**Problem:** `ChunkLoader.LoadChunk()` requires chunk to exist in `WorldChunkManager.GetChunk()`, but we now return chunks that don't exist in registry.

### Fix: ChunkLoader.cs

**Modified `LoadChunk()` to create chunk on-demand:**

```csharp
public void LoadChunk(ChunkId chunkId)
{
    if (loadedChunks.Contains(chunkId))
    {
        return;
    }
    
    // I5-001 FIX: Try to get from registry, create on-demand if not found
    WorldChunk chunk = chunkManager.GetChunk(chunkId);
    if (chunk == null)
    {
        // I5-001 FIX: Create basic chunk data on-demand for procedural world
        if (_showDebugLogs)
            Debug.Log($"[ChunkLoader] Creating on-demand chunk {chunkId}");
        
        // Create basic chunk with empty peaks/farms
        chunk = new WorldChunk
        {
            Id = chunkId,
            State = ChunkState.Unloaded,
            Peaks = new List<PeakData>(),
            Farms = new List<FarmData>(),
            CloudSeed = chunkManager.GenerateCloudSeed(chunkId),
            WorldBounds = new Bounds(
                new Vector3(chunkId.GridX * 2000 + 1000, 0, chunkId.GridZ * 2000 + 1000),
                new Vector3(2000, 1000, 2000)
            )
        };
    }

    // Continue with chunk loading...
}
```

---

## Files Changed

| File | Change |
|------|--------|
| `WorldStreamingManager.cs` | Added GetLocalPlayerPosition(), fixed UpdateStreaming() |
| `StreamingTest_AutoRun.cs` | Added GetPlayerPosition(), fixed F7 handler |
| `WorldChunkManager.cs` | Modified GetChunksInRadius() to return on-demand chunks |
| `ChunkLoader.cs` | Modified LoadChunk() to create chunk on-demand |

---

## Testing

1. Enable `showDebugHUD = true` in WorldStreamingManager
2. Teleport far from spawn (e.g., to 66180, -416113)
3. Press F7 - chunks should load around player
4. Check logs: should show "Using NetworkPlayer IsOwner" or "Using Player tag"
5. Should see chunks being created on-demand

---

## Remaining Issues

1. **Preload system** may still have issues with "Skipping: center chunk unchanged"
2. **Chunk generation** for on-demand chunks (empty without peaks/farms)
3. **Procedural generation** needs to work without WorldData for distant chunks

---

## Author
Claude Code (Subagent Analysis + Implementation)
**Date:** 19.04.2026 10:52 MSK