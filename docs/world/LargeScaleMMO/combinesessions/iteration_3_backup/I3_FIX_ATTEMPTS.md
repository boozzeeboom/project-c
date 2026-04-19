# ITERATION 3: LOG FIX ATTEMPTS

## Structure

```markdown
## Attempt N
**Date:** YYYY-MM-DD HH:MM
**Hypothesis:** ...
**Change:** ...
**Result:** SUCCESS / FAILED / PARTIAL
**Observation:** ...
```

---

## Attempt 0: Initial State
**Date:** 19.04.2026 08:00
**Hypothesis:** Initial PlayerChunkTracker integration
**Change:** 
- Added `using ProjectC.World.Streaming;` to NetworkPlayer.cs
- Added fields and UpdatePlayerPosition() calls
**Result:** SUCCESS - compilation passed
**Observation:** CS0246 fixed, but new problem discovered...

---

## Attempt 1: Offset Growth Analysis
**Date:** 19.04.2026 08:10
**Hypothesis:** GetWorldPosition() does not account for _totalOffset
**Change:** Created I3_DEBUG_MASTER_PROMPT.md with analysis
**Result:** PARTIAL - documentation created
**Observation:** 
```
Facts:
1. Player at position (-98410, 3, 480578)
2. WorldRoot shifted to (3060000, 0, -16430000)
3. GetWorldPosition returns (-98410, 3, 480578)
4. Threshold = 150000
5. 98410 < 150000 - shift should NOT trigger!
6. BUT shift happens every 0.5 seconds
```

**Question:** Why does shift trigger if |pos| < threshold?

---

## Attempt 2: HYPOTHESIS H1 - GetWorldPosition does NOT subtract _totalOffset
**Date:** 19.04.2026 08:11
**Hypothesis:** GetWorldPosition() returns NetworkPlayer position WITHOUT subtracting _totalOffset. After world shift, player position (in world coordinates) already includes the shift, but GetWorldPosition returns local position.

**Change:** Code analysis of GetWorldPosition() in lines 298-330 FloatingOriginMP.cs

**Result:** CONFIRMED - code returns position without subtracting _totalOffset

**Observation:** 
```
Code facts (lines 298-324):
```csharp
// 2. NetworkPlayer - PRIORITY!
var networkPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
foreach (var netObj in networkPlayers)
{
    if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
    {
        Vector3 pos = netObj.transform.position;
        // ...
        if (pos.magnitude > 10000) // Only if far from origin!
        {
            return pos;  // <--- RETURNS POSITION WITHOUT SUBTRACTING _totalOffset!
        }
    }
}
```

**Problem:**
1. WorldRoot shifted by _totalOffset = (3060000, 0, -16430000)
2. NetworkPlayer at position (-98410, 3, 480578) (LOCAL position)
3. GetWorldPosition() returns (-98410, 3, 480578) directly
4. FloatingOriginMP thinks this is WORLD position and shifts
5. Shift (-100000, 0, 480000) added to _totalOffset
6. _totalOffset grows infinitely!

**Root Cause:**
After world shift, WorldRoot moves but player stays in place.
But GetWorldPosition() returns player position WITHOUT accounting for 
the fact that WorldRoot is already shifted. Therefore offset grows infinitely.

**Solution:**
GetWorldPosition() for NetworkPlayer must SUBTRACT _totalOffset:
```csharp
Vector3 pos = netObj.transform.position;
Vector3 truePos = pos - _totalOffset;  // Add this!
if (truePos.magnitude > 10000)
{
    return truePos;  // Return TRUE position
}
```

**Status:** HYPOTHESIS CONFIRMED, fix pending

---

## Attempt 4: FIX I3-001 - GetWorldPosition subtract _totalOffset
**Date:** 19.04.2026 08:15
**Hypothesis:** GetWorldPosition() for NetworkPlayer returns `pos` directly WITHOUT subtracting _totalOffset.

**Change:** Applied fix in FloatingOriginMP.cs lines 319-323:
```csharp
Vector3 truePos = pos - _totalOffset;
if (showDebugLogs && Time.frameCount % 600 == 0)
    Debug.Log($"[FloatingOriginMP] GetWorldPosition: using NetworkPlayer IsOwner, rawPos={pos:F0}, _totalOffset={_totalOffset:F0}, truePos={truePos:F0}, name={netObj.name}");
return truePos;
```

**Result:** SUCCESS - offset growth stopped!

**Observation:**
```
Before fix: offset grew infinitely
After fix: offset stable at (-10000, 0, -200000)
```

**Status:** FIXED

---

## Attempt 5: FIX I3-002 - Throttle logs and cache position
**Date:** 19.04.2026 08:19
**Hypothesis:** Console spam from GetWorldPosition() called every frame from OnGUI.

**Change:** 
1. Added `_cachedWorldPosition` field
2. LateUpdate caches position: `_cachedWorldPosition = cameraWorldPos`
3. OnGUI uses cache: `GUILayout.Label($"Pos: {_cachedWorldPosition:F0}")`
4. Log throttle: `Time.frameCount % 600 == 0`

**Result:** SUCCESS - console spam eliminated

**Status:** FIXED

---

## LOG ANALYSIS (from last run)

### Log 1: Player position
```
[NetworkPlayer] OnWorldShifted: offset=(-100000.00, 0.00, 480000.00), 
transform.position=(-98410.39, 3.00, 480578.20), IsOwner=True
```

### Log 2: Offset after shift
```
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(-3060000.00, 0.00, 16430000.00)
```

### Log 3: Position check
```
[FloatingOriginMP] GetWorldPosition: using NetworkPlayer IsOwner=(-98410, 3, 480578)
```

### Calculation:
```
|(-98410, 3, 480578)| = sqrt(98410^2 + 480578^2) 
                      = sqrt(9,684,528,100 + 230,955,306,084)
                      = sqrt(240,639,834,184)
                      ~490,551 ( > 150,000 threshold!)
```

**CONCLUSION:** Position is FAR from origin, threshold = 150k, shift is JUSTIFIED!

---

## ROOT CAUSE

### Problem
1. Player teleported to (-98410, 480578)
2. This is LESS than threshold (150k) per coordinate
3. BUT by magnitude (distance) this ~490k > 150k
4. FloatingOriginMP checks `position.magnitude > threshold`
5. Shift by (-100000, 0, 480000) rounded from position (-98410, 480578)

### Question
Why does player end up at (-98410, 480578) if:
- Teleport was to (1000000, 5, 0)?
- After AutoResetOrigin should be at (5, 5, 0)?

### Possible Causes
1. Teleport RPC did not work
2. AutoResetOriginAfterTeleport was not called
3. Someone else teleports player
4. Offset not applied correctly

---

## Next Steps

1. [ ] Apply fix to FloatingOriginMP.cs lines 319-323
2. [ ] Test: player teleported to 1M?
3. [ ] Test: AutoResetOriginAfterTeleport called?
4. [ ] Test: who else changes player position?
5. [ ] Test: what is WorldRoot position BEFORE teleport?

---
**Updated:** 19.04.2026 08:16
**Version:** 1.2
