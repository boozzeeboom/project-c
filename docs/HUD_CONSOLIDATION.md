# HUD Consolidation (Session 5_3)

## Problem

Multiple HUD scripts were creating their own Canvas objects independently:

| Script | Canvas Created | Location |
|--------|----------------|----------|
| ThirdPersonCamera.cs | "Canvas" + ControlHintsUI + TextMeshProUGUI | On camera |
| AltitudeUI.cs | "HUD_Canvas" | At start |
| TradeDebugTools.cs | "TradeDebugCanvas" | On self |
| SceneDebugHUD.cs | "SceneDebugCanvas" | On self |
| WorldCamera.cs | "Canvas" | On camera |

This caused control hints to be "scattered" across the screen — rendered in different canvases with different sorting orders.

## Solution

Created centralized **HUDManager** that owns a single Canvas:

```
[HUDManager] (DontDestroyOnLoad)
  └── HUD_Canvas (ScreenSpaceOverlay, sortOrder=100)
        ├── ControlHintsUI
        ├── AltitudeHUD_Panel
        ├── SceneDebug_Panel
        └── TradeDebug_Panel
```

## Implementation

### HUDManager.cs (Assets/_Project/Scripts/UI/)

```csharp
public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }
    
    [SerializeField] private int canvasSortOrder = 100;
    
    public Canvas GetOrCreateHUDCanvas();
    public (GameObject, RectTransform, TextMeshProUGUI) CreateHUDText(...);
    public (GameObject, RectTransform, Image) CreateHUDPanel(...);
    public static HUDManager EnsureExists(); // Auto-create on first access
}
```

### Refactored Scripts

1. **ThirdPersonCamera.cs** — `CreateControlHintsUI()` uses HUDManager
2. **WorldCamera.cs** — `CreateControlHintsUI()` uses HUDManager
3. **AltitudeUI.cs** — `SetupUI()` uses HUDManager
4. **SceneDebugHUD.cs** — `CreateUI()` uses HUDManager
5. **TradeDebugTools.cs** — `CreateUI()` uses HUDManager

### Key Design Decisions

1. **Tag-based Canvas detection** — HUDManager searches for existing Canvas tagged "HUDCanvas" before creating new one
2. **EnsureExists() pattern** — HUDs don't need to check for null; HUDManager auto-creates itself
3. **Separate prefabs still work** — if ControlHintsUI.prefab exists in scene, HUDManager finds it first
4. **Sort order = 100** — leaves room for higher-priority overlays (menus: 200+, debug: 50)

## Benefits

- All HUD elements render in single Canvas
- Consistent sorting order
- Reduced overhead (1 Canvas vs 5)
- Easier debugging (one place to inspect)
- Unified canvas scaling

## Migration Guide

For any new HUD scripts:

```csharp
private void CreateMyHUD()
{
    var hudManager = HUDManager.EnsureExists();
    var canvas = hudManager.GetOrCreateHUDCanvas();
    
    // Use HUDManager methods or create children manually
    var (panelObj, panelRect, panelImage) = hudManager.CreateHUDPanel(
        "MyPanel", null, ...
    );
}
```

## Files Modified

- `Assets/_Project/Scripts/UI/HUDManager.cs` — **CREATED**
- `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs` — refactored
- `Assets/_Project/Scripts/Core/WorldCamera.cs` — refactored
- `Assets/_Project/Scripts/UI/AltitudeUI.cs` — refactored
- `Assets/_Project/Scripts/UI/SceneDebugHUD.cs` — refactored
- `Assets/_Project/Trade/Scripts/TradeDebugTools.cs` — refactored