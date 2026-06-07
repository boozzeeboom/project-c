---
paths:
  - "Assets/_Project/Scripts/Network/**"
  - "Assets/_Project/Scripts/Player/Network*.cs"
---

# Network Code Rules

- Minimize NetworkVariable updates (batch when possible)
- Use GhostOwner to identify local player
- Implement proper cleanup in OnNetworkDespawn
- Use NetworkTickSystem for deterministic logic
- RPCs for unreliable/one-shot actions (ability cast, chat)
- NetworkVariables for persistent state (position, health, inventory)

## NGO Best Practices

```csharp
// Correct: Use NetworkVariable for state
private NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>();

// Correct: RPC for one-shot actions
[ServerRpc(RequireOwnership = false)]
public void CastAbilityServerRpc(int abilityId) { }

// Incorrect: Don't sync in Update()
void Update() {
    NetworkPosition.Value = transform.position; // VIOLATION: too frequent
}
```

## Floating Origin

- All positions stored relative to world origin
- Use `FloatingOriginMP.SnapToOrigin()` when distance > threshold
- Sync player positions after origin shift
- Handle scene transitions with proper cleanup