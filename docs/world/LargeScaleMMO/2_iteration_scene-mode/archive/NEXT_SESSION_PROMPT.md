# NEW SESSION PROMPT - Scene System Integration Fix

**Date:** 29.04.2026
**Project:** ProjectC_client (Unity MMO)
**Status:** Architecture fixes applied, need verification

---

## CONTEXT: What Was Done

### Problem Identified
Scene-based architecture (24 scenes, 4×6 grid, 79,999×79,999 units per scene) was integrated but broken:
- Player didn't spawn on Host start
- "SceneTransitionCoordinator not found" errors
- ClientSceneLoader didn't auto-load initial scene

### Root Cause
`SceneTransitionCoordinator` was an unnecessary middle layer. ServerSceneManager sent RPCs to it, but it was located on wrong GameObject ("Runtime" not "NetworkManager"). This caused cascade of errors.

### Fixes Applied (29.04.2026)

| File | Change |
|------|--------|
| `ServerSceneManager.cs` | Direct RPC to ClientSceneLoader (no Coordinator) |
| `ClientSceneLoader.cs` | Auto-load scene on Host connect |
| `NetworkPlayerSpawner.cs` | Spawn in Update() to solve timing |
| `BootstrapSceneGenerator.cs` | Removed Coordinator creation |
| `WorldSceneSetup.cs` | Removed AddSceneTransitionCoordinator() |
| `SceneTransitionCoordinator.cs` | **DELETED** |

---

## CURRENT STATE

### Files Modified
```
Assets/_Project/Scripts/World/Scene/
├── ClientSceneLoader.cs      [MODIFIED] - Added OnClientConnectedCallback + AutoLoad
├── ServerSceneManager.cs     [MODIFIED] - Direct RPC to ClientSceneLoader
└── SceneTransitionCoordinator.cs [DELETED]

Assets/_Project/Scripts/Network/
└── NetworkPlayerSpawner.cs   [MODIFIED] - Update-based spawn timing

Assets/_Project/Editor/
├── BootstrapSceneGenerator.cs [MODIFIED] - No Coordinator created
└── WorldSceneSetup.cs         [MODIFIED] - No Coordinator method
```

### Documentation Created
```
docs/world/LargeScaleMMO/2_iteration_scene-mode/
├── INTEGRATION_FIX_PLAN.md     - Full fix plan with code changes
├── CORRECTATION_*.md           - Corrected architecture details
├── ARCHITECTURE_GRAPH.html     - Visual diagram
└── INDEX.md                    - Updated with fixes
```

---

## WHAT NEEDS TO BE DONE

### Priority 1: VERIFY FIXES
1. Regenerate BootstrapScene via Menu: `ProjectC → World → Generate Bootstrap Scene`
2. Check that SceneRegistry.asset exists: `Assets/_Project/Data/Scene/SceneRegistry.asset`
3. Verify all 24 WorldScene_X_Y.unity exist in `Assets/_Project/Scenes/World/`
4. Set BootstrapScene.unity first in Build Settings
5. Run Play Mode - press Host button
6. Check console for errors:
   - "SceneTransitionCoordinator not found" should be GONE
   - Player should spawn and appear in world
   - WorldScene_0_0 should load automatically

### Priority 2: If Errors Persist
Check these components:

1. **SceneRegistry.asset** - must exist and have:
   - GridColumns = 6
   - GridRows = 4
   - SceneNamePrefix = "WorldScene_"

2. **BootstrapScene GameObject hierarchy:**
   ```
   BootstrapScene.unity
   ├── NetworkManager (with NetworkManagerController)
   ├── Runtime
   │   ├── ClientSceneLoader (DontDestroyOnLoad)
   │   └── ServerSceneManager
   ├── PlayerSpawner
   │   ├── NetworkObject
   │   ├── NetworkPlayerSpawner
   │   └── NetworkPlayer
   ├── EventSystem
   ├── NetworkTestCanvas + NetworkTestMenu
   └── WorldRoot
   ```

3. **ServerSceneManager** must find ClientSceneLoader via `FindAnyObjectByType<ClientSceneLoader>()`

### Priority 3: Additional Issues to Check
- NetworkPrefab null errors (check NetworkManager prefab list)
- FindObjectsByType in Update() causing performance issues
- Scene transition when player crosses boundary

---

## KEY FILES REFERENCE

### Scene System Core
- `Assets/_Project/Scripts/World/Scene/SceneID.cs` - Scene identification struct
- `Assets/_Project/Scripts/World/Scene/SceneRegistry.cs` - ScriptableObject registry
- `Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs` - Server-side scene management
- `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` - Client-side loading

### Network
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` - Connection management
- `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs` - Player spawning
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` - Player component

### Generators
- `Assets/_Project/Editor/BootstrapSceneGenerator.cs` - Creates Bootstrap scene
- `Assets/_Project/Editor/WorldSceneGenerator.cs` - Creates 24 world scenes
- `Assets/_Project/Editor/WorldSceneSetup.cs` - Adds runtime to existing scenes

### Documentation
- `docs/world/LargeScaleMMO/2_iteration_scene-mode/INDEX.md` - Documentation index
- `docs/world/LargeScaleMMO/2_iteration_scene-mode/SCENE_ARCHITECTURE_DECISION.md` - Architecture decision
- `docs/world/LargeScaleMMO/2_iteration_scene-mode/INTEGRATION_FIX_PLAN.md` - Fix details

---

## EXPECTED BEHAVIOR (After Fixes)

```
[NetworkTestMenu] Select connection mode
[NetworkTestMenu] User clicks Host
[ServerSceneManager] ServerSceneManager initialized on server
[NetworkPlayerSpawner] Client connected: 0
[NetworkPlayerSpawner] Host/Server player spawned
[NetworkPlayer] OnNetworkSpawn - IsOwner=true
[ClientSceneLoader] Client connected: 0
[ClientSceneLoader] Auto-loading initial scene for Host: Scene(0, 0)
[ClientSceneLoader] Loading scene: WorldScene_0_0
[SceneManager] LoadSceneAsync Additive: WorldScene_0_0
[SceneManager] Scene loaded: WorldScene_0_0
[ClientSceneLoader] Scene loaded: WorldScene_0_0
[Player appears in world at (39999.5, 0, 39999.5)]
```

---

## IF PLAYER STILL DOESN'T SPAWN

Debug approach:
1. Check NetworkTestMenu.Start() - are buttons properly assigned?
2. Check NetworkManagerController.StartHost() - is it being called?
3. Check NetworkPlayerSpawner.Update() - is SpawnLocalPlayer() being called?
4. Check NetworkObject.SpawnAsPlayerObject() - does it work?
5. Check ClientSceneLoader.OnClientConnectedCallback() - is it firing?

---

## PREVIOUS COMMIT CONTEXT

Before scene integration (commit 0ba0398):
- Chunk-based system with 2,000×2,000 chunks
- FloatingOriginMP for precision at large coords
- Working player spawn via NetworkManager

After scene integration:
- 24 scenes of 79,999×79,999 (no FloatingOriginMP needed inside scenes)
- Scene-based loading with additive scenes
- Fixed: Removed SceneTransitionCoordinator, direct RPC to ClientSceneLoader

---

**Note:** Before running fixes, commit current state with `git add -A && git commit -m "Scene system integration fixes applied"`

**Start verification by regenerating Bootstrap scene:** Menu → ProjectC → World → Generate Bootstrap Scene