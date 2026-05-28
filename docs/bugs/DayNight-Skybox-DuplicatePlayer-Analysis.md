# Day-Night & Duplicate Player Bug Analysis

**Date**: 2026-05-28  
**Status**: Root causes identified  
**Priority**: High

---

## 1. SKYBOX SWITCHING ISSUE

### Root Cause
The **DayNightController** object (`instanceID: 56696`) has a `Volume` component attached with its own VolumeProfile. This creates a conflict with:

1. **URP Global Volume** — uses `DefaultVolumeProfile` (has ColorAdjustments, Bloom, etc.)
2. **DayNightController Volume** — separate profile that may override skybox settings

The SPEC.md (section 6.3) mentions using 2 skybox materials with blending via `SkyboxBlending.shader`. However, the current implementation has:

- `Skybox_Day.mat` — day material
- `Skybox_Night.mat` — night material  
- `Skybox_Blended.mat` — blending material (possibly misconfigured)

### Symptoms
- Visual "flickering" or switching between 2 skybox appearances
- Skybox may not transition smoothly between day/night phases
- Night sky may show briefly during day and vice versa

### Why DayNightController Only Controls Sun
Looking at `DayNightController.cs`:
- **Only handles sun light** (rotation, intensity, color)
- **Does NOT control skybox materials**
- **Does NOT control RenderSettings.ambient**
- **Does NOT control RenderSettings.fog**

The controller is incomplete per the SPEC.md requirements.

### Files Involved
| File | Issue |
|------|-------|
| `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs` | Only controls sun, not skybox |
| `Assets/DefaultVolumeProfile.asset` | Global post-processing profile |
| `Assets/_Project/Materials/Skybox/*.mat` | 3 materials, blend logic unclear |
| Scene Volume on DayNightController | May conflict with global volume |

---

## 2. DUPLICATE PLAYER ISSUE

### Root Cause
**Triple spawn mechanism** causing duplicate player objects:

### Spawn Mechanism #1: NetworkManager Auto-Spawn
```
NetworkManager.prefab:
  AutoSpawnPlayerPrefabClientSide: 1
  PlayerPrefab: NetworkPlayer.prefab (guid: 224427a7f796e5b448f07ed8c2a1469b)
```
When host starts, NetworkManager auto-spawns `NetworkPlayer.prefab`.

### Spawn Mechanism #2: NetworkPlayerSpawner on PlayerSpawner
```csharp
// NetworkPlayerSpawner.cs line 20-28
if (useScenePlayerAsHost && !_hasSpawnedHostPlayer &&
    NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
{
    _hasSpawnedHostPlayer = true;
    SpawnLocalPlayer();  // Spawns same prefab again!
}
```
**This causes a second player object to spawn for the host.**

### Spawn Mechanism #3: NetworkPlayer itself
```csharp
// NetworkPlayer.cs line 128-130
if (IsOwner)
{
    SpawnCamera();  // Spawns ThirdPersonCamera (expected)
}
```
This is correct behavior, not the problem.

### Why "Inverted Control" is Observed
The duplicate player:
- Is NOT controlled by local user (not `IsOwner`)
- Has NetworkTransform synchronizing position
- Appears to mirror/follow movements
- Causes visual confusion about which is "real"

### Additional Issue Found
**PlayerSpawner.prefab has walkSpeed = 5000** (line 261)
- This is ~1000x normal walk speed (5 m/s expected)
- Indicates prefab is not properly configured
- Or was accidentally set during testing

### Files Involved
| File | Issue |
|------|-------|
| `Assets/_Project/Prefabs/NetworkManager.prefab` | AutoSpawnPlayerPrefabClientSide: 1 |
| `Assets/_Project/Prefabs/PlayerSpawner.prefab` | Has BOTH NetworkPlayerSpawner + NetworkPlayer |
| `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs` | Duplicates host player spawn |
| `Assets/_Project/Prefabs/NetworkPlayer.prefab` | walkSpeed: 5000 (wrong) |

---

## 3. RECOMMENDED FIXES

### Fix A: Disable Duplicate Player Spawn
**Option 1**: Disable NetworkManager auto-spawn
```yaml
# In NetworkManager.prefab
AutoSpawnPlayerPrefabClientSide: 0
```

**Option 2**: Disable NetworkPlayerSpawner on PlayerSpawner.prefab
- Remove `NetworkPlayerSpawner` component from PlayerSpawner.prefab
- OR set `useScenePlayerAsHost = false`

**Recommended**: Option 1 (disable auto-spawn, let NetworkPlayerSpawner handle it)

### Fix B: Complete DayNightController Skybox Integration
The controller needs to:
1. Control `RenderSettings.skybox` material
2. Control `RenderSettings.ambient` colors
3. Control `RenderSettings.fog`
4. Blend between Skybox_Day/Skybox_Night based on time

Per SPEC.md section 4.2, DayNightController should have:
- `SkyboxMaterialDay` Material
- `SkyboxMaterialNight` Material
- Transition logic via `Skybox_Blended.mat` or dynamic blending

### Fix C: Verify Volume Profiles Don't Conflict
1. Check DayNightController's Volume component in scene
2. Set its `weight` to 0 or remove it if not needed
3. Use global volume with profile that doesn't conflict

### Fix D: Fix walkSpeed on NetworkPlayer
Change from `walkSpeed: 5000` to `walkSpeed: 5` in PlayerSpawner.prefab

---

## 4. TESTING CHECKLIST

After fixes:
- [ ] Host starts → only 1 player spawned
- [ ] Player control works without duplication
- [ ] Day/Night skybox transitions smoothly
- [ ] No flickering between skybox materials
- [ ] Walk speed is normal (not 5000)

---

## 5. FILES TO MODIFY

1. `Assets/_Project/Prefabs/NetworkManager.prefab` — disable auto-spawn
2. `Assets/_Project/Prefabs/PlayerSpawner.prefab` — fix walkSpeed
3. `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs` — complete implementation
4. Scene: DayNightController Volume — verify it doesn't conflict