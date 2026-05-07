# Cloud Generator — Architecture

## Overview
Spherical packing approach: clouds = clusters of overlapping spheres with noise-driven radius variation.

## Pipeline

```
1. Cloud Seeding      → Place anchor spheres on FBM-driven grid
2. Radius Modulation  → Perlin/Simplex noise modulates sphere radii
3. Density Field      → Overlap creates density gradient (dense center, wispy edges)
4. Weather Effects    → Wind distortion, storm intensity, turbulence
5. LOD Spheres        → Output list of (position, radius) for Unity mesh
```

## Core Algorithms

### 1. FBM Base Shape
- 3D Perlin noise sampled on grid
- Octaves: 4-6, persistence: 0.5, lacunarity: 2.0
- Threshold to create cloud "footprint"

### 2. Spherical Packing
- Poisson disk sampling for anchor points
- Fill rate controlled by density parameter
- Minimum distance between sphere centers = avg_radius * 2

### 3. Radius Modulation
- Local density from surrounding spheres
- Noise-based micro-variation
- Storm mode: sharper edges, darker base

### 4. Weather Parameters
- `humidity`: how "full" clouds are (0-1)
- `windDirection`: vec2 distortion vector
- `turbulence`: noise amplitude multiplier
- `stormIntensity`: combines multiple effects

## Output Format

```csharp
public struct CloudSphere {
    public Vector3 position;  // local to cloud pivot
    public float radius;
    public float density;     // 0-1, for opacity
}
```

## Web Visualizer
- `web/visualizer.html` — standalone file, open in browser
- Top-down projection of sphere packing
- Controls: cloudSize, density, turbulence, humidity, stormIntensity, seed
- Export button generates C# code snippet with current params

## Unity Port
- Core math in pure C# static class (no Unity deps)
- `src/CloudMath.cs` — all algorithms
- Copy directly into any C# project