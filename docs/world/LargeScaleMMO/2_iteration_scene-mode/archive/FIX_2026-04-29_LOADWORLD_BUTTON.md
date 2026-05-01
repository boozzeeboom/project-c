# BootstrapSceneGenerator & NetworkTestMenu Fixes - 29.04.2026

## Changes Applied

### 1. NetworkTestMenu.cs - Added LoadWorld button

**File:** `Assets/_Project/Scripts/UI/NetworkTestMenu.cs`

**Changes:**
- Added `loadWorldButton` serialized field (line 18)
- Added button subscription in Start() (line 48-50)
- Added `LoadWorldScene()` method (line 127-140)
- Method finds `ClientSceneLoader` and calls `LoadInitialScene(new SceneID(0, 0))`

**Code added:**
```csharp
[SerializeField] public Button loadWorldButton;

// In Start():
if (loadWorldButton != null)
    loadWorldButton.onClick.AddListener(() => LoadWorldScene());

private void LoadWorldScene()
{
    var loader = FindAnyObjectByType<ProjectC.World.Scene.ClientSceneLoader>();
    if (loader != null)
    {
        loader.LoadInitialScene(new ProjectC.World.Scene.SceneID(0, 0));
        UpdateStatus("Loading World Scene [0,0]...");
    }
    else
    {
        UpdateStatus("Error: ClientSceneLoader not found!");
    }
}
```

### 2. BootstrapSceneGenerator.cs - Fixed button panel size

**File:** `Assets/_Project/Editor/BootstrapSceneGenerator.cs`

**Changes:**
- Changed panel size from `300x250` to `300x310` to fit 4 buttons
- Added `loadWorldButton` assignment to `menuHandler`
- Fixed button Y positions: -20, -70, -120, -170 (spacing 50 instead of 60)

### 3. BootstrapSceneGenerator.cs - Removed duplicate code (earlier)

**Earlier fix:** Removed duplicate `CreatePlayerSpawner()` code block at lines 225-258.
This was causing CS1519 compilation errors.

## How Scene Transition Works

### Flow

```
1. Launch BootstrapScene
2. Click "Host" button
   - NetworkManagerController.StartHost()
   - Player spawns at scene(0,0) center: (39999.5, 3000, 39999.5)
   - ClientSceneLoader.AutoLoadInitialScene() loads WorldScene_0_0

OR

1. Launch BootstrapScene
2. Click "Host"
3. Click "Load World [0,0]" button (NEW)
   - Calls ClientSceneLoader.LoadInitialScene(new SceneID(0,0))
   - Loads WorldScene_0_0 + 3x3 neighbors

4. Player moves in WorldScene_0_0
5. At boundary (~79000 local), ClientSceneLoader.Update() detects scene change
6. ServerSceneManager.TransitionClient() sends RPC
7. ClientSceneLoader.LoadScene(newScene) loads new scene
8. ClientSceneLoader.UnloadScene(oldScene) unloads old scene
```

## Scene Grid

```
WorldScene_0_0 through WorldScene_0_5  (row 0, Z=0 - Equator)
WorldScene_1_0 through WorldScene_1_5  (row 1, Z=1 - Temperate)
WorldScene_2_0 through WorldScene_2_5  (row 2, Z=2 - Temperate)
WorldScene_3_0 through WorldScene_3_5  (row 3, Z=3 - Poles)
```

- GridColumns = 6 (X = 0-5)
- GridRows = 4 (Z = 0-3)
- SceneSize = 79,999

## Scene Names

- Format: `WorldScene_{GridX}_{GridZ}`
- Example: Scene at (X=2, Z=1) = `WorldScene_2_1`

## Files Modified

| File | Change |
|------|--------|
| `Assets/_Project/Scripts/UI/NetworkTestMenu.cs` | Added loadWorldButton field, subscription, and LoadWorldScene() method |
| `Assets/_Project/Editor/BootstrapSceneGenerator.cs` | Panel size increased to 310, loadWorldButton assigned to menuHandler |
| `Assets/_Project/Editor/BootstrapSceneGenerator.cs` | (Earlier) Removed duplicate CreatePlayerSpawner code |

## Related Documents

- `SCENE_TRANSITION_ANALYSIS.md` - Full analysis of scene transition
- `CORRECTED_ARCHITECTURE.md` - Architecture after FIX-001 to FIX-005
- `SCENE_ARCHITECTURE_DECISION.md` - Why 79,999 scenes