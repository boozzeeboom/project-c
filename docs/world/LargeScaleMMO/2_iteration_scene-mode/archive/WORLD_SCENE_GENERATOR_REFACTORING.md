# WorldSceneGenerator Refactoring Report

**Date:** 28.04.2026
**File:** `Assets/_Project/Editor/WorldSceneGenerator.cs`

---

## Changes Applied

### Imports Fixed
- Re-added `UnityEngine.SceneManagement` (was accidentally removed, caused CS0246 error)

### Lines Removed: ~240

| Removed Method | Lines | Reason |
|----------------|-------|--------|
| `CreateRuntimeSetup()` | 393-410 | Runtime components belong in Bootstrap, not world scenes |
| `AddWorldSceneManager()` | 412-418 | Runtime component |
| `AddClientSceneLoader()` | 420-431 | Runtime component |
| `AddServerSceneManager()` | 433-443 | Runtime component |
| `AddSceneTransitionCoordinator()` | 445-452 | Runtime component |
| `AddWorldStreamingManager()` | 454-466 | Runtime component |
| `AddFloatingOriginMP()` | 468-497 | **NOT NEEDED** - 79,999 is float-safe |
| `AddAltitudeCorridorSystem()` | 499-553 | Runtime component |
| `AddCloudSystem()` | 555-574 | Runtime component |
| `CreatePlayerSpawnPoint()` | 576-590 | **WRONG pattern** - NetworkPlayer spawns via NetworkManager |
| `CreatePlayerPrefab()` | 592-615 | **WRONG pattern** - Creates duplicate players |

### Toggle Flags Removed
- `_addRuntimeSetup`
- `_addPlayerSpawnPoint`

### Final State: 449 lines (was 690)

---

## Final Scene Structure (Generated)

```
WorldScene_{row}_{col}.unity
├── WorldRoot_{row}_{col}
│   ├── DirectionalLight (at world pos: col*79999, 100, row*79999)
│   ├── GroundPlane_{row}_{col} (at world pos: col*79999+39999.5, 0, row*79999+39999.5)
│   ├── SceneLabel_{row}_{col} (TMPro, debug)
│   └── Boundaries_{row}_{col}
│       ├── North (BoxCollider, if row=3)
│       ├── South (BoxCollider, if row=0)
│       ├── East (BoxCollider)
│       ├── West (BoxCollider)
│       ├── SouthPoleBlocker (if row=0)
│       └── NorthPoleBlocker (if row=3)
```

---

## What Goes Where

| Component | Location | Reason |
|-----------|----------|--------|
| WorldSceneGenerator | Editor folder | Generates scene files |
| SceneRegistry.asset | `Assets/_Project/Data/Scene/` | ScriptableObject, loaded at runtime |
| GroundPlane, Boundaries, Light | WorldScene_X_Y.unity | Scene geometry |
| WorldSceneManager | **Bootstrap.unity** | Persistent manager |
| ClientSceneLoader | **Bootstrap.unity** | Persistent manager |
| ServerSceneManager | **NetworkManager prefab** | NetworkBehaviour |
| FloatingOriginMP | **NOT NEEDED** | Precision safe in 79,999 scenes |
| NetworkPlayer | **Spawned dynamically** | Via NetworkManager, not per-scene |
| SceneBoundNetworkObject | **On network objects** | Marks which scene owns the object |

---

## Bootstrap Scene Contents (Manual Setup Required)

```
Bootstrap.unity (or MainMenu.unity)
├── NetworkManager
│   ├── ServerSceneManager (component)
│   └── SceneTransitionCoordinator (component)
├── ClientSceneLoader (DontDestroyOnLoad)
├── WorldStreamingManager
├── AltitudeCorridorSystem
├── CloudSystem
└── Main Camera + AudioListener
```

---

## Grid Parameters

| Parameter | Value |
|-----------|-------|
| Rows | 4 |
| Columns | 6 |
| Scene size | 79,999 × 79,999 |
| Total scenes | 24 |
| Overlap | 1,600 units (2%) |
| Pole rows | 0 (equator), 3 (poles blocked) |

---

## Coordinate System

- Scene naming: `WorldScene_{row}_{col}` where row=Z, col=X
- World position: `X = col * 79999`, `Z = row * 79999`
- Scene center: `X = col * 79999 + 39999.5`, `Z = row * 79999 + 39999.5`
- Horizontal wrap: Scene[0,Y] connects to Scene[5,Y]
- Vertical block: Row 0 and Row 3 have PoleBlocker colliders

---

## Key Architectural Decision

**FloatingOriginMP is NOT needed inside scenes because:**
- Scene size 79,999 < float32 precision threshold (~100,000)
- Each scene resets local origin to 0
- Players never exceed 79,999 within a scene
- FloatingOriginMP threshold (150,000) is never reached

This was a deliberate architectural choice: **scene-based world partitioning solves the floating point precision problem without requiring FloatingOriginMP inside scenes.**