# NGO Best Practices - Project C

## 1. Null-Safe Position Patterns

### Get Local Player Position Safely
```csharp
using Unity.Netcode;

public static class NetworkUtils
{
    public static Vector3 GetSafeLocalPosition(Vector3 fallback = default)
    {
        if (NetworkManager.Singleton?.LocalClient?.PlayerObject != null)
            return NetworkManager.Singleton.LocalClient.PlayerObject.transform.position;
        return fallback;
    }

    public static NetworkObject GetLocalPlayer() =>
        NetworkManager.Singleton?.LocalClient?.PlayerObject;

    public static bool IsConnected =>
        NetworkManager.Singleton?.LocalClient?.PlayerObject != null;
}
```
### Usage
```csharp
Vector3 myPos = NetworkUtils.GetSafeLocalPosition();

if (NetworkUtils.IsConnected)
    player.NetworkPlayerComponent.TradeBuyServerRpc(itemId, qty, locationId);
```

---

## 2. ServerRpc/ClientRpc Patterns

### ServerRpc (Client -> Server)
```csharp
[ServerRpc(RequireOwnership = false)]
public void TradeBuyServerRpc(string itemId, int quantity, string locationId,
    ServerRpcParams rpcParams = default)
{
    var sender = rpcParams.Receive.SenderClientId;
    // Validate and process on SERVER
}
```

### ClientRpc (Server -> Clients)
```csharp
[ClientRpc]
public void TradeResultClientRpc(ulong targetClientId, bool success, string msg,
    ClientRpcParams rpcParams = default)
{
    if (NetworkManager.Singleton.LocalClientId == targetClientId)
        HandleResult(success, msg);
}
```
### CustomMessagingManager (No serialization limits)
```csharp
using Unity.Collections;
using Unity.Networking.Transport;

var writer = new DataWriter();
writer.WriteValue(itemId); writer.WriteValue(quantity);
NM.Singleton.CustomMessagingManager.SendNamedMessage("CustomTrade", clientId, writer);
```

---

## 3. Floating Origin - World Shift RPC

### Server Authority (Host/Server)
```csharp
[ClientRpc]
private void BroadcastWorldShiftClientRpc(Vector3 offset)
{
    if (!IsServer) ApplyWorldShift(offset);
}

public void ApplyWorldShift(Vector3 offset)
{
    foreach (var root in worldRoots) root.position -= offset;
    FloatingOriginMP.OnWorldShifted?.Invoke(offset);
}
```

### Client-Side Listen
```csharp
void OnEnable() => FloatingOriginMP.OnWorldShifted += HandleShift;
void HandleShift(Vector3 offset) { /* sync physics, audio */ }
```
---

## 4. NetworkVariable Best Practices

### Use NetworkVariable for state
```csharp
private NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>(
    onValueChanged: (prev, next) =>
    {
        if (!IsOwner) transform.position = Vector3.Lerp(prev, next, 0.5f);
    }
);
```

### Sync only on meaningful change
```csharp
// GOOD
void OnTeleport() { if (IsOwner) NetworkPosition.Value = newPos; }

// BAD
void Update() { NetworkPosition.Value = transform.position; }
```

---

## 5. Quick Reference

| Pattern | Code |
|---------|------|
| Local player | NM.Singleton?.LocalClient?.PlayerObject |
| Ownership | IsOwner (NetworkBehaviour) |
| Connection | NM.Singleton.IsListening |
| Server role | NM.Singleton.IsServer |
| Safe RPC | if (IsOwner) MyServerRpc() |

---

## 6. Common Mistakes

1. Null PlayerObject - Check LocalClient?.PlayerObject != null
2. RPC before spawn - Dont call RPCs before OnNetworkSpawn
3. Ownership - Use [ServerRpc(RequireOwnership = false)] for cross-player
4. Float precision - Use FloatingOriginMP when world >100,000 units
5. Desync - Use NetworkVariable for position, not manual sync

---
*ProjectC_client*