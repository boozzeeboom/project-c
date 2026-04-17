# FLOATING ORIGIN DEEP ANALYSIS

**Date:** Analysis of artifacts at coordinates >100,000 units
**Project:** ProjectC_client
**Status:** CRITICAL ISSUE FOUND

---

## 1. ROOT CAUSE OF ARTIFACTS

### 1.1 Position Fragmentation

Current FloatingOriginMP implementation creates a fundamental problem:

TradeZones position: (-8100000.00, 0.00, -8100000.00) <- ALREADY SHIFTED!
WorldRoot position: (-4050000.00, 0.00, -4050000.00) <- ALREADY SHIFTED!
totalOffset: (4200000.00, 0.00, 4200000.00)

Problem: Different objects have DIFFERENT positions after shift. No single reference point.

### 1.2 RoundShift() Bug Analysis

Current code:
```csharp
private Vector3 RoundShift(Vector3 position)
{
    return new Vector3(
        Mathf.Round(position.x / shiftRounding) * shiftRounding,
        Mathf.Round(position.y / shiftRounding) * shiftRounding,
        Mathf.Round(position.z / shiftRounding) * shiftRounding
    );
}
```

**Problem:** RoundShift rounds camera position, NOT computing optimal shift!

Example of incorrect shift:
- Player position: (-8,100,000, 500, -8,100,000)
- RoundShift result: (-8,100,000, 0, -8,100,000) <- Y rounded to 0!
- World shifts by: (-8,100,000, 0, -8,100,000)

### 1.3 Hierarchical Desync

All objects are parented to WorldRoot:
```csharp
string[] childNames = { Mountains, Clouds, Farms, TradeZones };
```

But FloatingOriginMP searches them separately:
```csharp
private string[] worldRootNames = new string[]
{
    WorldRoot, Mountains, Clouds, farms, TradeZones, ...
};
```

Problem: If Mountains is a CHILD of WorldRoot, both will be added and shifted TWICE!

---

## 2. WHY CURRENT IMPLEMENTATION FAILS

### 2.1 Algorithmic Error

**Current approach:**
```csharp
Vector3 offset = RoundShift(cameraWorldPos);  // WRONG!
ApplyShiftToAllRoots(offset);
```

**Correct approach:**
```csharp
Vector3 offset = -cameraWorldPos;  // INVERT!
offset = SnapToGrid(offset, gridSize);
ApplyShiftToAllRoots(offset);
```

### 2.2 No Nesting Awareness

Code searches WorldRoot, Mountains, TradeZones as SEPARATE objects.

### 2.3 ChunksContainer Not Properly Handled

Problem: FloatingOriginMP shifts ChunksContainer (if found), but does NOT recalculate positions of already loaded chunks.

---

## 3. ALTERNATIVE APPROACHES FOR LARGE WORLDS

### 3.1 Player-Centric Centering (Recommended)

```csharp
void LateUpdate()
{
    Transform target = GetPlayerTransform();
    if (target.position.magnitude > threshold)
    {
        // INVERT position!
        Vector3 shift = new Vector3(
            -Mathf.Round(target.position.x / gridSize) * gridSize,
            0,
            -Mathf.Round(target.position.z / gridSize) * gridSize
        );
        ApplyShift(shift);
    }
}
```

### 3.2 Chunk-Based Origin

Use ChunkId-based origin instead of world coordinates.

### 3.3 Double-Precision Coordinates

Use Vector3d for world positions.

---

## 4. SPECIFIC RECOMMENDATIONS FOR FIX

### 4.1 Critical Fixes

#### 4.1.1 Fix RoundShift - ADD INVERSION

**Was:**
```csharp
Vector3 offset = RoundShift(cameraWorldPos);
```

**Should be:**
```csharp
Vector3 offset = -RoundShift(cameraWorldPos);  // ADD MINUS!
```

#### 4.1.2 Fix Hierarchy - Shift ONLY Root

Do NOT add children separately if they are already children of WorldRoot.

#### 4.1.3 Add Validation After Shift

### 4.2 Important Improvements

#### 4.2.1 Synchronize with ChunkLoader

After shift, call streamingManager.LoadChunksAroundPlayer(playerPos).

#### 4.2.2 Correct Parameters

- threshold = 50000f (was 150,000 - TOO HIGH!)
- shiftRounding = 5000f (was 10,000 - TOO LARGE!)

---

## 5. SUMMARY

### Root Cause:
1. RoundShift() rounds camera position instead of computing optimal shift
2. Double shift: WorldRoot and children shift separately
3. No ChunkLoader synchronization

### Why Current Implementation Fails:
- Algorithmic error in RoundShift (missing inversion)
- Hierarchical desync of objects
- ChunksContainer created dynamically

### Recommended Approach:
1. **IMMEDIATE:** Fix RoundShift - INVERT the result
2. **IMMEDIATE:** Shift ONLY world root, not children separately
3. **IMPORTANT:** Synchronize with ChunkLoader after shift
4. **STRATEGIC:** Consider Chunk-Based Origin as long-term solution

---

## RELEVANT FILE PATHS

Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs
Assets/_Project/Scripts/World/Core/FloatingOrigin.cs
Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs
Assets/_Project/Scripts/World/Streaming/ProceduralChunkGenerator.cs
Assets/_Project/Scripts/World/Streaming/WorldChunkManager.cs
Assets/_Project/Scripts/World/Streaming/StreamingTest_AutoRun.cs
Assets/_Project/Scripts/Core/StreamingSetupRuntime.cs
Assets/_Project/Scripts/Editor/PrepareMainScene.cs
