---
paths:
  - "Assets/_Project/Scripts/Core/**"
  - "Assets/_Project/Scripts/World/**"
---

# Engine Code Rules

- ZERO allocations in hot paths (update loops, rendering, physics) — pre-allocate, pool, reuse
- All engine APIs must be thread-safe OR explicitly documented as single-thread-only
- Profile before AND after every optimization — document the measured numbers
- Use RAII / deterministic cleanup for all resources
- All engine systems must support graceful degradation
- Document public API with usage examples

## Examples

**Correct** (zero-alloc hot path):

```csharp
// Pre-allocated list reused each frame
private List<Collider> _nearbyCache = new List<Collider>();

void Update() {
    _nearbyCache.Clear();
    Physics.OverlapSphereNonAlloc(transform.position, radius, _colliderBuffer);
}
```

**Incorrect** (allocating in hot path):

```csharp
void Update() {
    var nearby = new List<Collider>();  // VIOLATION: allocates every frame
    nearby = Physics.OverlapSphere(...);  // VIOLATION: allocates list
}