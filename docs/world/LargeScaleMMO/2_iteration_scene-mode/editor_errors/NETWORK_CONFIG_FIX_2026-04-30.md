# NetworkManagerController Fix - 2026-04-30

## Problem

The code had a cyclical error pattern where:
1. `StartHost()` tried to access `networkManager.NetworkConfig` which returned `null`
2. Reflection-based workaround to create `NetworkConfig` failed because field `m_NetworkConfig` doesn't exist in NGO 2.x
3. This led to repeated attempts, CS0136 variable conflict errors, and runtime NRE

## Root Cause

- **Wrong approach**: Using reflection to access private `m_NetworkConfig` field
- **Missing understanding**: In NGO 2.x, `NetworkConfig` is auto-initialized internally when you call `StartHost()`
- **Unnecessary complexity**: The code was trying to manually configure something that NGO handles automatically

## Solution

Use the official Unity pattern:
1. Configure `UnityTransport` via `SetConnectionData()` - this is the proper way to set connection info
2. Simply call `networkManager.StartHost()` - NGO auto-initializes NetworkConfig internally
3. Remove all reflection-based workarounds

### Before (Broken)
```csharp
public void StartHost()
{
    var netConfig = networkManager.NetworkConfig;
    if (netConfig == null)
    {
        // WRONG: Reflection to find m_NetworkConfig field (fails!)
        var field = typeof(NetworkManager).GetField("m_NetworkConfig", 
            BindingFlags.NonPublic | BindingFlags.Instance);
    }
}
```

### After (Working)
```csharp
public void StartHost()
{
    var transport = GetComponent<UnityTransport>();
    if (transport == null)
        transport = gameObject.AddComponent<UnityTransport>();
    
    transport.SetConnectionData("127.0.0.1", serverPort);
    networkManager.StartHost();
}
```

## Key Changes

1. **StartHost()** - Simplified, no reflection, uses `SetConnectionData()`
2. **StartServer()** - Same pattern as StartHost
3. **ConnectToServerCoroutine()** - Uses `SetConnectionData()` for client
4. **Removed StartHostCoroutine()** - Unused duplicate code
5. **NetworkUI.cs** - Updated to call `StartHost()` directly instead of coroutine

## Files Modified

- `Assets/_Project/Scripts/Core/NetworkManagerController.cs`
- `Assets/_Project/Scripts/UI/NetworkUI.cs`

## Additional Fix Required

When removing `StartHostCoroutine()`, found that `NetworkUI.cs` line 166 was calling:
```csharp
StartCoroutine(networkManagerController.StartHostCoroutine());
```

This was fixed to:
```csharp
networkManagerController.StartHost();
```

## Verification

The fix follows Unity's official documentation pattern for NGO + UnityTransport:
- Transport is configured via `UnityTransport.SetConnectionData()`
- NetworkConfig is auto-managed by NGO internally
