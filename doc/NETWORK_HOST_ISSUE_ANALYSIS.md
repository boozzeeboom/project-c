# NetworkManagerController - Issue Analysis
**Date:** 2026-04-29
**Component:** `Assets/_Project/Scripts/Core/NetworkManagerController.cs`

## Problem Summary

`networkManager.NetworkConfig` is `null` when `StartHost()` is called, causing NullReferenceException at `NetworkManager.CanStart()`.

**Stack trace:**
```
at Unity.Netcode.NetworkManager.CanStart (NetworkManager+StartType type) [0x00033]
at Unity.Netcode.NetworkManager.StartHost () [0x00012]
```

## Root Cause

The private field `m_NetworkConfig` inside `NetworkManager` is never initialized. When we call `networkManager.NetworkConfig` (the public property), it returns `null` instead of creating a new `NetworkConfig()`.

This suggests the `NetworkManager` component is being used in a way Netcode doesn't expect - perhaps added at runtime rather than being a properly configured in-scene component.

## Attempted Solutions

| Attempt | Approach | Result |
|---------|----------|--------|
| 1 | Use local NM instead of Singleton | NRE - config still null |
| 2 | Direct StartHost call without config | NRE inside CanStart |
| 3 | Create NetworkConfig via reflection | Compiled but failed at runtime |
| 4 | Multiple attempts to access config | All return null |

## The CS0136 Error Cycling Problem

Every time we try to add code to handle the null config case, we accidentally declare `var transport` inside an `if` block, conflicting with the `var transport` at method scope level. This causes CS0136 compilation errors.

**See:** `docs/.../editor_errors/CS0136_PREVENTION.md`

## Current Code State

The current `StartHost()` method at lines 245-320 attempts to:
1. Use local NetworkManager component
2. If NetworkConfig is null, create it via reflection
3. Set transport in config
4. Call StartHost

But this STILL fails because reflection doesn't work as expected.

## What Works: BootstrapSceneGenerator

In `BootstrapSceneGenerator.cs:155-158`:
```csharp
var nmConfig = new SerializedObject(networkManager);
nmConfig.FindProperty("NetworkConfig.NetworkTransport").objectReferenceValue = transport;
nmConfig.ApplyModifiedProperties();
```

However, this uses UnityEditor API and can't be used at runtime.

## Next Investigation Steps

1. **Check BootstrapScene.unity** - Is there a properly configured in-scene NetworkManager that works?
2. **Git history** - Find when StartHost stopped working
3. **Alternative:** Use `StartHostCoroutine` which might have different initialization path
4. **Consider:** The NetworkManager on the NMC object might be a DUPLICATE - check if there's another NM in scene

## Files to Check

- `BootstrapScene.unity` - NetworkManager setup
- `NetworkManagerController.cs` - Current (broken) implementation
- `doc/NETWORK_HOST_ISSUE_ANALYSIS.md` - This file
- `doc/.../editor_errors/NETWORK_CONFIG_CYCLICAL_ERRORS.md` - Error pattern docs
[NMC] Using local NM: NetworkManager, IsListening: False
[NMC] NetConfig:
[NMC] NetConfig is NULL - trying direct StartHost
[NMC] Calling StartHost() directly...
```

The local `NetworkManager` component exists and is valid, but `NetworkConfig` property returns `null`.

### BootstrapSceneGenerator Workaround

In `BootstrapSceneGenerator.cs:155-158`:
```csharp
var nmConfig = new SerializedObject(networkManager);
nmConfig.FindProperty("NetworkConfig.NetworkTransport").objectReferenceValue = transport;
nmConfig.ApplyModifiedProperties();
```

This uses `SerializedObject` to set the transport BEFORE calling StartHost.

## Potential Solutions Considered

### 1. Don't check for null - call StartHost anyway
```csharp
if (netConfig == null)
{
    var transport = GetComponent<UnityTransport>();
    if (transport == null) transport = gameObject.AddComponent<UnityTransport>();
    networkManager.StartHost(); // Let Netcode handle it
}
```
**Status:** Doesn't work - StartHost throws NRE

### 2. Use SerializedObject to set transport
```csharp
var nmConfig = new SerializedObject(networkManager);
nmConfig.FindProperty("NetworkConfig.NetworkTransport").objectReferenceValue = transport;
nmConfig.ApplyModifiedProperties();
```
**Status:** Can't use in runtime (UnityEditor namespace)

### 3. Force NetworkConfig initialization before StartHost
```csharp
// Access NetworkConfig property before calling StartHost
var config = networkManager.NetworkConfig; // Forces initialization
if (config != null) { /* use config */ }
```
**Status:** Already tried - returns null

### 4. Create new NetworkManager component if config is null
**Status:** Doesn't help - new NM also has null config

### 5. Don't use Singleton for StartHost
**Status:** Already implemented - uses local NM

## Current Code State (2026-04-29)

The current `StartHost()` implementation:
1. Uses local NetworkManager component (not Singleton)
2. Checks if NetworkConfig is null
3. If null, creates UnityTransport and calls StartHost directly
4. This still fails

## Questions to Investigate

1. **Why does NetworkConfig property return null?**
   - Is there a timing issue with component initialization?
   - Does Netcode expect certain order of operations?

2. **Why did it work before?**
   - What changed in the project that broke this?
   - Was there a different initialization path being used?

3. **Is the BootstrapScene properly configured?**
   - Check if NetworkManager in BootstrapScene has proper configuration
   - Check if there are multiple NetworkManager instances

## Files to Check

- `BootstrapScene.unity` - NetworkManager setup
- `NetworkManagerController.cs` - Current implementation
- `ClientSceneLoader.cs` - How it accesses network
- `ServerSceneManager.cs` - Server-side networking

## Recommended Next Steps

1. **Compare working vs non-working states:**
   - Find a recent backup or git commit that worked
   - Compare the exact code and scene configurations

2. **Add diagnostic to Awake:**
   - Log NetworkConfig immediately after creating NetworkManager
   - Check if it ever becomes non-null

3. **Check for multiple NetworkManagers:**
   - There might be two instances - one from BootstrapSceneGenerator, one from NMC

4. **Check Unity Netcode version:**
   - Could be a bug in specific version
   - Consider updating package

## Code Copy (Current StartHost method)

```csharp
public void StartHost()
{
    Debug.Log("[NMC] StartHost() called");

    if (networkManager == null)
    {
        networkManager = GetComponent<Unity.Netcode.NetworkManager>();
    }

    if (networkManager == null)
    {
        Debug.LogError("[NMC] NetworkManager component not found!");
        return;
    }

    Debug.Log($"[NMC] Using local NM: {networkManager}, IsListening: {networkManager.IsListening}");

    var netConfig = networkManager.NetworkConfig;
    Debug.Log($"[NMC] NetConfig: {netConfig}");

    if (netConfig == null)
    {
        Debug.LogWarning("[NMC] NetConfig is NULL - trying direct StartHost");

        var existingTransport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (existingTransport == null)
        {
            existingTransport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        }

        Debug.Log("[NMC] Calling StartHost() directly...");
        networkManager.StartHost();
        Debug.Log($"[NMC] StartHost done, IsListening: {networkManager.IsListening}");
        return;
    }

    var transport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
    if (transport == null)
    {
        transport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
    }

    if (netConfig.NetworkTransport == null)
    {
        netConfig.NetworkTransport = transport;
    }

    Debug.Log($"[NMC] Config ready: NetTransport={netConfig.NetworkTransport}");

    if (!networkManager.IsListening)
    {
        Debug.Log("[NMC] Calling StartHost()...");
        networkManager.StartHost();
        Debug.Log("[NMC] StartHost() completed");
    }
}
```