# NetworkManagerController - Cyclical Error Pattern Analysis
**Date:** 2026-04-29
**Location:** `Assets/_Project/Scripts/Core/NetworkManagerController.cs`
**Issue Type:** Recurring Compilation Error / NullReferenceException

## Pattern: CS0136 "transport" Variable Conflict

### Error Description
```
Assets\_Project\Scripts\Core\NetworkManagerController.cs(XXX,21): error CS0136: 
A local or parameter named 'transport' cannot be declared in this scope 
because that name is used in an enclosing local scope to define a local or parameter
```

This error appears repeatedly whenever we add a block that declares `var transport = ...` inside an existing block that already has a `transport` variable.

### Why This Happens

The `StartHost()` method has TWO code paths for handling `netConfig == null`:

**Path 1 (netConfig is NULL):**
- Creates local `var transport = GetComponent<UnityTransport>()`
- Then calls `networkManager.StartHost()`
- Returns early

**Path 2 (netConfig is NOT NULL):**
- Creates local `var transport = GetComponent<UnityTransport>()`
- Uses `transport` for further setup

Both paths create identically-named variables at the same scope level, causing CS0136 when both paths exist in the same method.

### Timeline of This Specific Issue

| Iteration | What Changed | Result |
|-----------|--------------|--------|
| 1 | Added `var transport` in null-check block | CS0136 error |
| 2 | Renamed to `existingTransport` in null-check block | Compiled but NRE at runtime |
| 3 | Changed to direct StartHost call without config | Compiled but NRE at runtime |
| 4 | Added reflection to create NetworkConfig | CS0136 again - forgot to rename inner `transport` |
| 5 | Renamed inner to `newTransport` | Compiled |

### The Root Issue: NetworkConfig is NULL

Despite all the code attempts, `networkManager.NetworkConfig` is returning `null` at runtime. This is the REAL problem.

**What we've tried:**
1. Using local NetworkManager vs Singleton
2. Multiple attempts to initialize/force NetworkConfig creation
3. Direct StartHost call without config
4. Reflection to create and set NetworkConfig

**All failed** because either:
- NetworkConfig stays null despite all attempts
- Or we get CS0136 compilation error due to variable naming

### The Real Question: Why is NetworkConfig NULL?

In Unity Netcode for GameObjects, `NetworkManager.NetworkConfig` should auto-initialize on first access. But it returns null.

**Possible causes:**
1. NetworkManager component added at runtime vs in-scene
2. Timing - Awake/Start hasn't run yet when StartHost is called
3. Serializer issue - config not being restored in builds
4. Multiple NetworkManager instances conflicting

### Code Structure That Would Work

```csharp
public void StartHost()
{
    // 1. Get local NM
    if (networkManager == null)
        networkManager = GetComponent<Unity.Netcode.NetworkManager>();

    // 2. ALWAYS create transport first (at method scope level)
    var transport = GetComponent<UnityTransport>();
    if (transport == null)
        transport = gameObject.AddComponent<UnityTransport>();

    // 3. Try to get config (may be null)
    var netConfig = networkManager.NetworkConfig;

    // 4. If config is null, create it via reflection
    if (netConfig == null)
    {
        var field = typeof(NetworkManager).GetField("m_NetworkConfig",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            netConfig = new NetworkConfig();
            field.SetValue(networkManager, netConfig);
        }
    }

    // 5. Set transport in config if needed
    if (netConfig != null && netConfig.NetworkTransport == null)
        netConfig.NetworkTransport = transport;

    // 6. Call StartHost
    networkManager.StartHost();
}
```

### Key Insight

The error cycling happens because we KEEP adding new blocks inside existing ones without properly restructuring. The solution is to:

1. Move ALL `transport` declarations to the TOP of the method (method scope level)
2. Keep them all as one variable
3. Then handle the null config case separately

### Documentation Next Steps

Need to investigate:
1. Check if BootstrapScene.unity has a properly configured in-scene NetworkManager
2. Compare with a working version (git history)
3. Check Unity Netcode package version for bugs
4. Consider using UnityTransport.SetConnectionData before StartHost

### Related Files

- `NetworkManagerController.cs` - Main file with issue
- `BootstrapSceneGenerator.cs` - Works around this issue with SerializedObject
- `doc/NETWORK_HOST_ISSUE_ANALYSIS.md` - More detailed analysis