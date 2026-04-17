# Large World Solutions - Technical Analysis

**Date:** 2026-04-17
**Project:** ProjectC_client

---

## 1. Why Artifacts Appear at >100,000 Units

Unity uses float (32-bit) for positions. Precision loss causes:
- Vertex jitter on meshes
- Unstable physics collisions
- Camera jumping

| Distance | Precision | Symptoms |
|----------|-----------|----------|
| < 1,000 | ~0.001 | Excellent |
| 10,000 | ~0.01 | Good |
| 100,000 | ~0.1 | Problems begin |
| 350,000 | ~1.0 | CRITICAL |

---

## 2. Approach Comparison

### 2.1. Floating Origin Pattern

Shifts world back to (0,0,0) when camera is far from origin.

```csharp
public float threshold = 150000f;
public float shiftRounding = 10000f;

void LateUpdate() {
    if (Mathf.Abs(cameraPos.x) > threshold)
        ApplyShiftToAllRoots(RoundShift(cameraPos));
}
```

### 2.2. Chunk-Based World Streaming

World divided into 2000x2000 chunks, only visible loaded.

```csharp
public const int ChunkSize = 2000;
public ChunkId GetChunkAtPosition(Vector3 worldPos) {
    return new ChunkId(
        Mathf.FloorToInt(worldPos.x / ChunkSize),
        Mathf.FloorToInt(worldPos.z / ChunkSize));
}
```

### 2.3. DOTS/ECS

Uses double (64-bit) for positions. NOT compatible with NGO.

---

## 3. Current Project C Architecture

1. WorldChunkManager - Server-side registry
2. FloatingOriginMP - Threshold 150k, rounding 10k
3. ChunkLoader - Async loading
4. ProceduralChunkGenerator - Deterministic from Seed
5. WorldStreamingManager - Coordinates all systems

---

## 4. Recommendations

### Immediate Actions
1. Reset WorldRoot positions to (0,0,0)
2. Verify threshold = 150,000
3. Remove duplicate FloatingOriginMP from prefabs
4. Test in Play Mode at 150,000 units

### Recommended: Floating Origin + Chunk Streaming

| Parameter | Floating Origin | Chunk Streaming | DOTS |
|-----------|----------------|-----------------|------|
| Precision | Solved | Within chunk | Double |
| Performance | All loaded | Only visible | Efficient |
| NGO compatible | Yes | Yes | No |
| Time to implement | 1 day | 2 weeks | 3+ months |

---

## 5. Related Files

- Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs
- Assets/_Project/Scripts/World/Streaming/WorldChunkManager.cs
- Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs
- Assets/_Project/Scripts/World/Streaming/ProceduralChunkGenerator.cs
- Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs
