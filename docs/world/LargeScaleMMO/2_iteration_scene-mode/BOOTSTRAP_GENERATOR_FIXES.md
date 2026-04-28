# BootstrapSceneGenerator.cs - Fixes Log

**Date:** 28.04.2026
**File:** `Assets/_Project/Editor/BootstrapSceneGenerator.cs`

---

## Errors Fixed

### 1. CS0234: UnityTransport namespace (line 112)
**Error:** `The type or namespace name 'UnityTransport' does not exist in the namespace 'Unity.Netcode'`

**Fix:** Changed `Unity.Netcode.UnityTransport` to `Unity.Netcode.Transports.UTP.UnityTransport`
```csharp
// Before:
var transport = networkObj.AddComponent<Unity.Netcode.UnityTransport>();

// After:
var transport = networkObj.AddComponent<UnityTransport>();
```

**Required usings:**
```csharp
using Unity.Netcode.Transports.UTP;
```

---

### 2. CS1656: DontDestroyOnLoad assignment (line 136)
**Error:** `Cannot assign to 'DontDestroyOnLoad' because it is a 'method group'`

**Fix:** Changed property access to method call
```csharp
// Before:
networkObject.DontDestroyOnLoad = true;

// After:
UnityEngine.Object.DontDestroyOnLoad(networkObject);
```

---

### 3. CS0246: AltitudeCorridorSystem not found (line 214)
**Error:** `The type or namespace name 'AltitudeCorridorSystem' could not be found`

**Fix:** Added correct namespace
```csharp
using ProjectC.Ship;
```

**Class location:** `Assets/_Project/Scripts/Ship/AltitudeCorridorSystem.cs`

---

### 4. CS0246: AltitudeCorridorData not found (lines 221, 232, 239)
**Error:** Same namespace issue for AltitudeCorridorData

**Fix:** Same `using ProjectC.Ship;` resolves all AltitudeCorridorData references
```csharp
using ProjectC.Ship;
```

**Class location:** `Assets/_Project/Scripts/Ship/AltitudeCorridorData.cs`

---

### 5. CS0246: CanvasScaler/GraphicRaycaster not found (lines 310-311)
**Error:** Types not found in namespace

**Fix:** Added `using UnityEngine.UI;`
```csharp
using UnityEngine.UI;
```

---

### 6. CS0618: Object.FindObjectOfType deprecated (line 328)
**Warning:** `'Object.FindObjectOfType<T>()' is obsolete`

**Fix:** Changed to `Object.FindFirstObjectByType<T>()`
```csharp
// Before:
var nmc = Object.FindObjectOfType<NetworkManagerController>();

// After:
var nmc = Object.FindFirstObjectByType<NetworkManagerController>();
```

Also applied to `ClientSceneLoader` lookup:
```csharp
var loader = Object.FindFirstObjectByType<ClientSceneLoader>();
```

---

### 7. CS1503: Cannot convert GameObject to Transform (lines 334, 339, 344, 349)
**Error:** `Argument 1: cannot convert from 'UnityEngine.GameObject' to 'UnityEngine.Transform'`

**Cause:** `CreateNetworkTestMenu` was passing `canvasObj.gameObject` but `CreateNetworkTestMenuContent` expected `GameObject`. Methods refactored to use `Transform` consistently.

**Fix:** Changed method signatures to use `Transform`:
```csharp
// Before:
private void CreateNetworkTestMenu()
{
    Canvas canvasObj = CreateNetworkTestCanvas();
    CreateNetworkTestMenuContent(canvasObj.gameObject);
}

private void CreateNetworkTestMenuContent(GameObject canvasObj)

// After:
private void CreateNetworkTestMenu()
{
    Canvas canvasObj = CreateNetworkTestCanvas();
    CreateNetworkTestMenuContent(canvasObj.transform);
}

private void CreateNetworkTestMenuContent(Transform canvasTransform)
```

---

## Final Using Statements

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectC.World.Scene;
using ProjectC.World.Streaming;
using ProjectC.World;
using ProjectC.Ship;
using ProjectC.Core;
using ProjectC.UI;
```

---

## Summary of Changes

| Issue | Type | Fix |
|-------|------|-----|
| UnityTransport namespace | Error | `UnityTransport` from UTP package |
| DontDestroyOnLoad | Error | Method call instead of property |
| AltitudeCorridorSystem | Error | Added `using ProjectC.Ship` |
| AltitudeCorridorData | Error | Added `using ProjectC.Ship` |
| CanvasScaler/GraphicRaycaster | Error | Added `using UnityEngine.UI` |
| FindObjectOfType deprecated | Warning | Changed to `FindFirstObjectByType` |
| GameObject→Transform conversion | Error | Method refactored to use `Transform` |

**Also fixed:** `ClientSceneLoader.cs` now calls `DontDestroyOnLoad(gameObject)` in its own `Awake()` method instead.

---

## Related Documents

- `TEST_WORKFLOW.md` - How to use BootstrapSceneGenerator
- `GRAPH_REPORT.md` - Architecture overview
- `WORLD_SCENE_GENERATOR_REFACTORING.md` - WorldSceneGenerator changes