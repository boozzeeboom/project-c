# Fixes Applied: Type Namespace + Ambiguous Object Errors (28.04.2026)

## Error 1: CS0246 - Type Namespace Not Found

**Error:** `CS0246: The type or namespace name 'Type' could not be found`

**Affected files:**
- `Assets/_Project/Editor/MainMenuSceneGenerator.cs:90`
- `Assets/_Project/Editor/BootstrapSceneGenerator.cs:127`
- `Assets/_Project/Editor/TestSceneGenerator.cs:105`

**Root Cause:** All three generators have `GetTypeByName` method using `System.Type` and `System.AppDomain`, but `using System;` was missing.

**Fix Applied:** Added `using System;` and `using System.Reflection;` to all three files.

---

## Error 2: CS0104 - Ambiguous 'Object' Reference

**Error:** `CS0104: 'Object' is an ambiguous reference between 'UnityEngine.Object' and 'object'`

**File:** `Assets/_Project/Editor/BootstrapSceneGenerator.cs:367`

**Root Cause:** After adding `using System;`, the C# keyword `object` (alias for `System.Object`) conflicts with `UnityEngine.Object` when using `Object.FindAnyObjectByType<T>()`.

**Fix Applied:**
```csharp
// Before:
var nmc = Object.FindAnyObjectByType<NetworkManagerController>();

// After:
var nmc = UnityEngine.Object.FindAnyObjectByType<NetworkManagerController>();
```

**Also fixed:** `WorldSceneGenerator.cs` had two instances of `Object.DestroyImmediate()`:
- Line 204: `Object.DestroyImmediate(ground.GetComponent<Collider>());`
- Line 367: `Object.DestroyImmediate(vizObj.GetComponent<Collider>());`

Changed to `UnityEngine.Object.DestroyImmediate()`.

**Added `using System;` to WorldSceneGenerator.cs** to enable `System.IO.Path.GetDirectoryName` on line 373.

---

## Error 3: NetworkTestMenu Buttons Not Wired (Runtime)

**Symptom:** Host/Client/Server buttons clicked but nothing happens

**Root Cause:** `BootstrapSceneGenerator.CreateNetworkTestMenuContent()` created buttons but never assigned them to `NetworkTestMenu`'s serialized fields (`hostButton`, `clientButton`, `serverButton`). So `NetworkTestMenu.Start()` found all three null and never called `AddListener`.

**Console evidence:**
```
[NetworkTestMenu] Select connection mode  ← menu shows but buttons do nothing
[WorldChunkManager] Creating on-demand chunk Chunk(122, 82)  ← OLD system still running
[FloatingOriginMP] FindThirdPersonCamera: No valid camera found!  ← FloatingOriginMP shouldn't exist
```

**Fix Applied in `NetworkTestMenu.cs`:**
```csharp
// BEFORE:
[SerializeField] private Button hostButton;
[SerializeField] private Button clientButton;
[SerializeField] private Button serverButton;

// AFTER:
[SerializeField] public Button hostButton;
[SerializeField] public Button clientButton;
[SerializeField] public Button serverButton;
```

**Reason:** BootstrapSceneGenerator needs to assign these at generation time. Private fields with `[SerializeField]` are serialized but not accessible from other classes. Changed to `public` so the generator can assign them.

---

## Error 6: TestSceneGenerator Inconsistent with Bootstrap

**Symptom:** TestSceneGenerator creates deprecated FloatingOriginMP and WorldStreamingManager, inconsistent with BootstrapSceneGenerator which correctly removed them.

**Root Cause:** When BootstrapSceneGenerator was updated to scene-based architecture, TestSceneGenerator was not updated.

**Fix Applied in `TestSceneGenerator.cs`:**
```csharp
// REMOVED from CreateTestSceneContent():
CreateWorldStreamingManager();  // OLD chunk system
CreateFloatingOriginMP();       // DEPRECATED per scene architecture
```

Deprecated methods `CreateWorldStreamingManager()` and `CreateFloatingOriginMP()` remain in file as dead code but are no longer called.

---

## Error 7: NetworkPlayer Not Found After Waiting (Missing PlayerSpawner)

**Symptom:** `[ClientSceneLoader] NetworkPlayer not found after waiting. Will retry...` and `[ServerSceneManager] Could not find NetworkPlayer for client 0`

**Root Cause:** `BootstrapSceneGenerator` was NOT creating a `NetworkPlayerSpawner`. The spawn flow:
1. Host/Client/Server starts via NetworkTestMenu buttons
2. `ClientSceneLoader.WaitForPlayer()` and `ServerSceneManager.FindPlayerTransformCoroutine()` look for `NetworkPlayer` component
3. No spawner exists → no player is ever spawned → they retry forever

**Fix Applied in `BootstrapSceneGenerator.cs`:**

1. Added `CreatePlayerSpawner()` method (based on `MainMenuSceneGenerator.CreatePlayerSpawner()`):
```csharp
private void CreatePlayerSpawner()
{
    GameObject spawnerObj = new GameObject("PlayerSpawner");
    spawnerObj.transform.position = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);
    var spawner = spawnerObj.AddComponent<NetworkPlayerSpawner>();
    var networkObject = spawnerObj.AddComponent<NetworkObject>();

    // Creates Player child object with NetworkPlayer component (inactive, template for clones)
    GameObject playerObj = new GameObject("Player");
    playerObj.transform.SetParent(spawnerObj.transform);
    // ... CharacterController, NetworkPlayer, PlayerCamera
    playerObj.SetActive(false);
}
```

2. Added call in `GenerateBootstrapScene()` after `CreateNetworkManager()`:
```csharp
CreateEventSystem();
CreateNetworkManager();
CreatePlayerSpawner();  // ADDED
CreateSceneManagement();
```

3. Added `using ProjectC.Network;` and `using ProjectC.Player;` for `NetworkPlayerSpawner` and `NetworkPlayer` types.

---

## Error 8: Player Spawner Structure Wrong (ServerSceneManager Can't Find Player)

**Symptom:** Server still logs "Could not find NetworkPlayer for client 0" after host starts.

**Root Cause (Timing + Structure):**
1. `NetworkPlayerSpawner.Start()` registers callback at line 18, but host's `OnClientConnectedCallback(0)` fires **during NetworkManager init before Start() runs** - host never gets its own spawn callback
2. Player was a **child** of spawner with `playerObj.SetActive(false)` - not on the same object as NetworkObject.SpawnAsPlayerObject() was called on
3. `ServerSceneManager.FindPlayerTransformCoroutine()` searches for `NetworkObject` with `NetworkPlayer` component on **same object**, but NetworkPlayer was on child

**Fix Applied in `BootstrapSceneGenerator.CreatePlayerSpawner()`:**
```csharp
// BEFORE: Spawner had Player child (inactive) - wrong structure
GameObject spawnerObj = new GameObject("PlayerSpawner");
var spawner = spawnerObj.AddComponent<NetworkPlayerSpawner>();
var networkObject = spawnerObj.AddComponent<NetworkObject>();
GameObject playerObj = new GameObject("Player");  // CHILD - wrong!
playerObj.transform.SetParent(spawnerObj.transform);
playerObj.AddComponent<NetworkPlayer>();  // On child - ServerSceneManager can't find it
playerObj.SetActive(false);  // Inactive - can't be found

// AFTER: All components on same GameObject
GameObject spawnerObj = new GameObject("PlayerSpawner");
var networkObject = spawnerObj.AddComponent<NetworkObject>();
var spawner = spawnerObj.AddComponent<NetworkPlayerSpawner>();
spawnerObj.AddComponent<CharacterController>();
spawnerObj.AddComponent<NetworkPlayer>();  // On SAME object as NetworkObject
GameObject cameraObj = new GameObject("PlayerCamera");  // Camera stays as child
cameraObj.transform.SetParent(spawnerObj.transform);
```

**Additional fix in `NetworkPlayerSpawner.cs`:**
Refactored `Start()` to handle case where NetworkManager might not be fully initialized. Separated `SpawnLocalPlayer()` method for clarity.

**Summary of fixes for Error 8:**
1. `BootstrapSceneGenerator.CreatePlayerSpawner()` - put NetworkPlayer on same GameObject as NetworkObject (not child)
2. `NetworkPlayerSpawner.Start()` - handle NetworkManager.Singleton availability check

---

## Files Modified Summary

| File | Line | Change |
|------|------|--------|
| `MainMenuSceneGenerator.cs` | 1-4 | Added `using System;`, `using System.Reflection;` |
| `BootstrapSceneGenerator.cs` | 1-5 | Added `using System;`, `using System.Reflection;` |
| `BootstrapSceneGenerator.cs` | 367 | `Object.` → `UnityEngine.Object.` |
| `BootstrapSceneGenerator.cs` | 369-371 | **ADDED:** Button assignment to menuHandler |
| `BootstrapSceneGenerator.cs` | 184-202 | **REMOVED:** FloatingOriginMP creation |
| `BootstrapSceneGenerator.cs` | 196 | **REMOVED:** `CreateWorldStreamingManager()` call |
| `BootstrapSceneGenerator.cs` | 198-225 | **COMMENTED:** `CreateWorldStreamingManager()` method |
| `BootstrapSceneGenerator.cs` | 312 | **REMOVED:** `ChunksContainer` from subRoots |
| `BootstrapSceneGenerator.cs` | 84 | **ADDED:** `CreatePlayerSpawner()` call |
| `BootstrapSceneGenerator.cs` | 152-182 | **ADDED:** `CreatePlayerSpawner()` method |
| `BootstrapSceneGenerator.cs` | 18 | **ADDED:** `using ProjectC.Player;` |
| `TestSceneGenerator.cs` | 62-79 | **REMOVED:** Calls to CreateWorldStreamingManager() and CreateFloatingOriginMP() |
| `TestSceneGenerator.cs` | 1-5 | Added `using System;`, `using System.Reflection;` |
| `WorldSceneGenerator.cs` | 1-3 | Added `using System;` |
| `WorldSceneGenerator.cs` | 205,368 | `Object.DestroyImmediate` → `UnityEngine.Object.DestroyImmediate` |
| `NetworkTestMenu.cs` | 16-18 | Changed `private` to `public` for button fields |
| `NetworkPlayerSpawner.cs` | 14-32 | Refactored Start() to handle NetworkManager availability |

## Scene-Based Architecture Alignment

**OLD Bootstrap Scene (chunk-based):**
- NetworkManager + FloatingOriginMP + WorldChunkManager + ChunkLoader + ProceduralChunkGenerator
- Used in iteration 1 for 2,000×2,000 chunk system

**NEW Bootstrap Scene (scene-based):**
- NetworkManager + ClientSceneLoader + ServerSceneManager + SceneTransitionCoordinator
- No FloatingOriginMP, no chunk system
- 24 scenes of 79,999×79,999 units each

**Key architectural decisions (per SCENE_ARCHITECTURE_DECISION.md):**
1. NGO Scene Management = ON (CheckObjectVisibility works correctly)
2. Scene = Unity Scene files (80,000×80,000 units, additive loading)
3. Preload triggers at 10k before boundary
4. 2% overlap (1,600 units) for visual seamlessness

## Related Documentation

- `BOOTSTRAP_GENERATOR_FIXES.md` - Previous fixes for BootstrapSceneGenerator (InputSystemUIInputModule reflection, UnityTransport namespace, etc.)
- `ERRORS_ANALYSIS_28042026.md` - Analysis of runtime errors in scene mode
- `SCENE_ARCHITECTURE_DECISION.md` - Scene-based architecture rationale (4×6 grid, 79,999 scene size)
- `INDEX.md` - Iteration 2 documentation index

## Scene System Integration (24 Scenes)

The generators create scenes for the 24-scene world system:
- **BootstrapSceneGenerator** - Creates BootstrapScene.unity with NetworkManager, ClientSceneLoader, ServerSceneManager, WorldStreamingManager, FloatingOriginMP
- **TestSceneGenerator** - Creates single test scene (WorldTestScene.unity) for debugging
- **MainMenuSceneGenerator** - Creates MainMenu.unity for main menu with NetworkManager

Grid configuration (from all three generators):
- ROWS = 4, COLS = 6 → 4×6 = 24 scenes
- SCENE_SIZE = 79,999 units
- Camera positioned at (COLS * SCENE_SIZE / 2, 3000, ROWS * SCENE_SIZE / 2)

---
**Fixed by:** Claude Code
**Date:** 28.04.2026