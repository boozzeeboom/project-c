# Research: Hierarchical Sphere-Based Procedural Cloud Generation (Cascade Approach)

## Executive Summary

**Current Problem:** The existing `CloudMath.cs` uses a uniform 3D grid approach where spheres are placed at regular intervals, with density determined by noise. This creates a "bubbly" layered appearance rather than natural cauliflower-like cumulus clouds.

**Target Approach:** Instead of filling volume uniformly, start with ONE smooth ellipsoid blob, then recursively generate "bumps" (sub-spheres) ON THE SURFACE of the parent shape. This mimics how real cumulus clouds form — a main volume with fluffy surface detail.

**Key Reference:** Houdini 20+ has identical nodes: `Cloud Shape From Polygon` (fills mesh with spheres) and `Cloud Shape Replicate` (scatters spheres ON the surface of existing spheres for cascade). This confirms the approach is industry-validated.

---

## 1. Hierarchical/Recursive Sphere Generation for Cloud-Like Shapes

### The Core Concept: Bumps on Surface, Not Volume Fill

The key insight (which matches Leonid's vision):

```
WRONG (current): Grid of spheres filling a volume → bubble-like layers
RIGHT (target):  One ellipsoid → bumps on surface → sub-bumps on those bumps → cauliflower
```

**How recursive subdivision differs from octree/BSP:**
- **Octree/BSP:** Recursively divides 3D SPACE into smaller volumes (creates tree/branch structure)
- **Surface bump cascade:** Recursively adds detail to SURFACE (creates fluffy/puffy structure)
- The difference is "not a tree with branches, but clouds"

### Algorithm: Surface Point Sampling → Noise → Bump Placement

```csharp
// Pseudocode for cascade generation
void GenerateBumps(Sphere parent, int depth, float maxDepth) {
    if (depth >= maxDepth) return;
    
    // 1. Sample many points ON the parent sphere surface
    List<Vector3> surfacePoints = SampleSphereSurface(parent.position, parent.radius, pointCount);
    
    // 2. For each surface point, sample noise to determine bump parameters
    foreach (Vector3 point in surfacePoints) {
        float noiseValue = SampleNoise(point); // 3D noise sampled at surface point
        
        // Only create bump if noise indicates it
        if (noiseValue > threshold) {
            float bumpSize = MapNoiseToSize(noiseValue, parent.radius);
            Vector3 bumpDirection = (point - parent.position).normalized;
            
            // 3. Create child sphere "on the surface" of parent
            Sphere child = new Sphere() {
                position = point + bumpDirection * (parent.radius * 0.1f), // slight offset outward
                radius = bumpSize
            };
            
            // 4. RECURSIVE: Generate bumps on THIS child's surface
            GenerateBumps(child, depth + 1, maxDepth);
        }
    }
}
```

**Why this creates "cauliflower" not "tree":**
- Child spheres are attached to parent SURFACE, not extending as thin branches
- Each level maintains roughly spherical proportions
- The "fluffiness" comes from many small bumps on many bumps

---

## 2. Surface Noise vs Volume Noise

### Current (Volume) Approach
```csharp
// This samples noise at grid cell centers to determine IF sphere exists there
float noiseValue = PerlinWorley3D(nx, ny, nz);
if (noiseValue > threshold) { /* place sphere */ }
```
Problem: Creates uniformly distributed bubbles filling the volume.

### Surface Bump Approach

**Key difference:** Noise is sampled AT a specific point ON the ellipsoid surface, not in 3D space.

```csharp
// To sample noise "on the surface":
Vector3 SurfacePoint = parent.position + normal * parent.radius;
float bumpIntensity = Noise3D(SurfacePoint * frequency);
```

**How to "push" spheres outward using noise:**

```csharp
Vector3 GetBumpDirection(Vector3 surfacePoint, Vector3 parentCenter) {
    Vector3 normal = (surfacePoint - parentCenter).normalized;
    
    // Perturb normal using noise for organic variation
    float perturbX = Noise3D(surfacePoint * scale + offset);
    float perturbY = Noise3D(surfacePoint * scale + offset + 100);
    float perturbZ = Noise3D(surfacePoint * scale + offset + 200);
    
    Vector3 perturbation = new Vector3(perturbX, perturbY, perturbZ) * perturbationStrength;
    return (normal + perturbation).normalized;
}
```

**Bump size from noise:**
```csharp
float bumpRadius = baseRadius * (0.5f + noiseValue * 0.5f);
```

---

## 3. Recursive Subdivision Algorithms for Spherical Surfaces

### A. Fibonacci Sphere (Golden Spiral) — Even Point Distribution

Best method for uniform point distribution on sphere surface:

```csharp
// C# implementation
Vector3[] FibonacciSphere(int numPoints) {
    Vector3[] points = new Vector3[numPoints];
    float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
    float angleIncrement = Mathf.PI * 2f * goldenRatio;
    
    for (int i = 0; i < numPoints; i++) {
        float t = (float)i / numPoints;
        float inclination = Mathf.Acos(1f - 2f * t);
        float azimuth = angleIncrement * i;
        
        points[i] = new Vector3(
            Mathf.Sin(inclination) * Mathf.Cos(azimuth),
            Mathf.Sin(inclination) * Mathf.Sin(azimuth),
            Mathf.Cos(inclination)
        );
    }
    return points;
}
```

**Why Fibonacci over icosphere subdivision:**
- Fibonacci gives even distribution regardless of sphere size
- Icosphere subdivision creates more points at poles
- Fibonacci is O(n), icosphere is O(n log n)

### B. Icosphere Subdivision — For Multi-Resolution

If you need exact vertex correspondence at multiple levels:

```csharp
// Start with icosahedron (12 vertices, 20 faces)
// Each subdivision: split each triangle into 4
// Vertices are normalized to sphere surface
```

### C. Houdini's Approach (Industry Standard)

Houdini `Cloud Shape Replicate` uses:
1. **Scatter on Surface:** Points scattered onto existing primitive spheres
2. **Point Separation:** Controls density of scattered points
3. **Constraint to Source:** Keeps spheres from drifting too far
4. **Jitter:** Position and normal jitter for organic look

---

## 4. The "Cauliflower" Mathematical Structure

### Why Real Cauliflower/Broccoli Looks Fluffy

Real cauliflower has:
- Main stem → branches → sub-branches (Sierpinski-like)
- But crucially: each sub-branch is THICK and ROUNDED, not thin

**Cloud structure is similar:**
- Main ellipsoid blob (cumulus base)
- Bulges and bumps on surface (not thin wisps)
- Smaller fluffy detail on those bumps

**NOT like:** Tree branches, lightning, blood vessels (those are fractal trees with thin connections)

### Key Property: Aspect Ratio of Child Spheres

```csharp
// Child radius relative to parent
float childRadiusRatio = 0.3f; // 30% of parent size
float minRadius = 0.05f; // Stop when spheres get too small
```

**Critical:** Child spheres must be substantially smaller but not tiny. A ratio of 25-40% creates the "fluffy" look. Below 20% looks like hair/fur.

---

## 5. Procedural Sphere-Packing on a Surface

### Houdini's Cloud Shape From Polygon Node

This node fills a polygonal mesh with adaptively-sized primitive spheres:
- **Packing Density:** Controls amount of spheres (higher = smaller spheres)
- **Iso Value:** Controls how tightly spheres fit
- **Particle Scale:** Uniform size multiplier
- **Particle Scale Limit:** Minimum sphere size

### Incircle Sphere Packing on Mesh Triangles

For arbitrary meshes, the "incircle" method places spheres in each triangle's inscribed circle:

```
For each triangle:
    1. Calculate inscribed circle center (incenter)
    2. Calculate inradius
    3. Place sphere at incenter with radius = inradius
    4. Recurse on gaps between circles
```

### Our Approach: Noise-Driven Density

For cloud generation, we DON'T want uniform packing. Instead:

```csharp
// For each surface point (via Fibonacci or mesh vertex):
float density = Noise3D(surfacePoint * frequency);
if (density > threshold) {
    float radius = parentRadius * density * sizeMultiplier;
    CreateSphere(surfacePoint, radius);
}
```

---

## 6. Noise-Driven Bump Generation on Surfaces

### Noise Type Selection

| Noise Type | Use Case | Characteristics |
|------------|----------|-----------------|
| **Perlin** | Base shape | Smooth, rounded transitions |
| **Worley (Voronoi)** | Erosion/detail | Sharp cellular boundaries |
| **FBM (Fractal Brownian Motion)** | Natural variation | Multi-scale detail |

### Bump Height from Noise

```csharp
float GetBumpHeight(Vector3 surfacePoint) {
    // Use FBM for natural cloud-like variation
    float largeScale = FBM3D(surfacePoint * 0.5f, 3, 0.5f, 2.0f); // Base bulges
    float mediumScale = FBM3D(surfacePoint * 2.0f, 4, 0.5f, 2.0f); // Medium detail
    float smallScale = FBM3D(surfacePoint * 8.0f, 4, 0.5f, 2.0f); // Fine fluffiness
    
    // Combine scales
    float height = largeScale * 0.6f + mediumScale * 0.3f + smallScale * 0.1f;
    
    return Mathf.Clamp01(height); // 0-1 range
}
```

### Bump Direction (Outward from Surface)

```csharp
Vector3 GetBumpNormal(Vector3 point, Vector3 sphereCenter) {
    Vector3 normal = (point - sphereCenter).normalized;
    
    // Add noise-based perturbation for organic feel
    float noiseA = Noise3D(point * 3f);
    float noiseB = Noise3D(point * 3f + 50);
    
    // Perturb normal slightly (tangent plane variation)
    Vector3 tangent = Vector3.Cross(normal, Vector3.up).normalized;
    Vector3 bitangent = Vector3.Cross(normal, tangent);
    
    normal = (normal + tangent * noiseA * 0.2f + bitangent * noiseB * 0.2f).normalized;
    
    return normal;
}
```

### Mapping Bump Height to Child Sphere Radius

```csharp
float HeightToRadius(float height, float parentRadius) {
    // Larger bumps = larger child spheres
    // But we don't want them bigger than ~40% of parent
    float maxChildRatio = 0.4f;
    float radius = parentRadius * height * maxChildRatio;
    return Mathf.Max(radius, minRadiusThreshold);
}
```

---

## 7. Hybrid Approach: Large Cloud Shape + Surface Bumps

### Recommended Pipeline

```
1. START: Smooth ellipsoid as "cloud envelope"
          ↓
2. SAMPLE: Many points on ellipsoid surface (Fibonacci)
          ↓
3. NOISE: For each point, sample FBM to determine:
          - Should there be a bump? (threshold)
          - How big? (noise value → size)
          - Which direction? (outward normal + jitter)
          ↓
4. CREATE: Child spheres at bump locations
          ↓
5. REPEAT: For each child sphere (up to max depth)
          ↓
6. OUTPUT: Hierarchy of spheres representing cloud shape
```

### Ellipsoid vs Sphere

Real clouds are often flattened (cumulus) or elongated (wind streaks):

```csharp
Vector3 ellipsoid = new Vector3(
    baseRadius * horizontalStretch,  // X
    baseRadius * verticalSquash,     // Y (smaller)
    baseRadius * horizontalStretch   // Z
);
```

---

## 8. Cascade Termination Conditions

### When Does a Sub-Sphere STOP Generating Children?

**1. Size Threshold (Most Important)**
```csharp
if (sphere.radius < minBumpRadius) return; // e.g., 0.5 units
```

**2. Depth Limit**
```csharp
if (currentDepth >= maxCascadeDepth) return; // e.g., 4-5 levels
```

**3. Noise Threshold**
```csharp
float bumpPotential = SampleNoise(surfacePoint);
if (bumpPotential < bumpThreshold) return; // Don't create tiny bumps
```

**4. Hybrid (Recommended)**
```csharp
bool ShouldCreateChildren(Sphere sphere, int depth) {
    if (depth >= maxDepth) return false;
    if (sphere.radius < minRadius) return false;
    if (sphere.radius < parentRadius * 0.08f) return false; // Too small relative to parent
    
    float continuationChance = sphere.radius / (parentRadius * 0.3f);
    return Random.value < continuationChance; // Probabilistic continuation
}
```

### Real Cloud Analogy

Real cumulus clouds:
- Top is fluffy (active upward growth)
- Bottom is relatively flat (less detail)
- Individual bumps grow until they hit "unny" air or merge with neighbors

---

## 9. Specific Algorithms and Pseudocode

### Algorithm A: Fibonacci-Based Cascade

```csharp
public class CascadeCloudGenerator
{
    public float minRadius = 0.5f;
    public int maxDepth = 4;
    public float childToParentRatio = 0.35f;
    public int pointsPerSphere = 64; // Fibonacci points
    
    public List<CloudSphere> Generate(Vector3 center, float radius)
    {
        var result = new List<CloudSphere>();
        var root = new CloudSphere { position = center, radius = radius };
        
        GenerateBumpsRecursive(root, 0, result);
        
        return result;
    }
    
    void GenerateBumpsRecursive(CloudSphere parent, int depth, List<CloudSphere> output)
    {
        if (depth >= maxDepth) return;
        
        // Generate points on parent sphere surface
        Vector3[] surfacePoints = FibonacciSphere(parent.position, parent.radius, pointsPerSphere);
        
        foreach (var point in surfacePoints)
        {
            // Sample noise at this surface point
            float noise = FBM3D(point * 2f, 4, 0.5f, 2.0f);
            
            // Threshold: only create bumps where noise indicates
            if (noise < 0.4f) continue;
            
            // Calculate bump parameters
            float bumpRadius = parent.radius * childToParentRatio * noise;
            if (bumpRadius < minRadius) continue;
            
            // Bump direction: outward from sphere center
            Vector3 direction = (point - parent.position).normalized;
            
            // Add slight jitter for organic feel
            direction = PerturbDirection(direction, 0.15f);
            
            // Create child sphere slightly outside parent surface
            Vector3 childPos = point + direction * (parent.radius * 0.05f);
            
            var child = new CloudSphere {
                position = childPos,
                radius = bumpRadius,
                density = noise
            };
            
            output.Add(child);
            
            // RECURSE: Generate bumps on this child's surface
            GenerateBumpsRecursive(child, depth + 1, output);
        }
    }
    
    Vector3 PerturbDirection(Vector3 dir, float strength)
    {
        // Add noise-based perturbation
        float angle1 = Noise1D(dir.x * 10) * strength;
        float angle2 = Noise1D(dir.y * 10) * strength;
        
        // Simple rotation around perpendicular axis
        Quaternion rot = Quaternion.AxisAngle(Vector3.up, angle1);
        dir = rot * dir;
        rot = Quaternion.AxisAngle(Vector3.right, angle2);
        dir = rot * dir;
        
        return dir.normalized;
    }
}
```

### Algorithm B: Hybrid With Worley Erosion

```csharp
// Combines the base ellipsoid with surface bump cascade
// Worley noise determines WHERE bumps appear (erosion pattern)

public List<CloudSphere> GenerateHybridCloud(CloudConfig config)
{
    var spheres = new List<CloudSphere>();
    
    // 1. Base ellipsoid
    var baseSphere = new CloudSphere {
        position = Vector3.zero,
        radius = config.baseRadius,
        density = 1.0f
    };
    spheres.Add(baseSphere);
    
    // 2. First cascade level: Worley-driven bumps
    GenerateSurfaceBumps(baseSphere, config.worleyScale, config.firstLevelCount, 1, spheres);
    
    // 3. Further levels: FBM-driven for fluffiness
    for (int d = 1; d < config.maxDepth; d++)
    {
        var parentSpheres = spheres.Where(s => s.depth == d).ToList();
        foreach (var parent in parentSpheres)
        {
            GenerateFBMBumps(parent, config, d, spheres);
        }
    }
    
    return spheres;
}
```

### Algorithm C: Apollonian Gasket (Alternative, More Dense)

The Apollonian gasket is a fractal circle packing where each new circle is tangent to three existing circles. While more mathematically pure, it's harder to control for cloud shapes:

```csharp
// For 3D, this becomes sphere packing in gaps
// Not recommended as primary approach but useful for "filling gaps"
```

---

## 10. Real-World References

### Industry Implementation: SideFX Houdini

**Cloud Shape From Polygon** (Houdini 20+):
- Fills polygonal mesh with adaptively-sized primitive spheres
- Used to model cumulus cloud-like shapes
- Parameters: Packing Density, Iso Value, Particle Scale, Particle Scale Limit

**Cloud Shape Replicate** (Houdini 20+):
- "Generates primitive spheres used to model cumulus cloud-like shapes around existing primitive spheres"
- Scatters points ON SURFACE of input spheres
- Supports: Scatter on Surface, Point Separation, Jitter, Scale by Noise, Directional Influence

**Workflow in Houdini:**
1. Create base ellipsoid mesh
2. Cloud Shape From Polygon → fills it with spheres
3. Cloud Shape Replicate → adds detail by scattering spheres ON those spheres
4. Cloud Billowy Noise + Cloud Wispy Noise → final volumetric refinement

### Academic References

- **"Texturing and Modeling: A Procedural Approach"** — Classic book with chapter on Cumulus Cloud Models
- **Spherical Fibonacci Mapping** — Nearly uniform point distribution on sphere (see Keinert et al.)
- **Apollonian Gaskets** — Fractal circle/sphere packing with Hausdorff dimension ~1.3057

### Games Using Similar Approaches

- **Horizon Zero Dawn** — Perlin-Worley composite for cloud density (our current approach)
- **Shadow of the Colossus** style clouds — likely billboard sprites, not procedural spheres
- Various procedural planet generators use icosphere + displacement mapping

---

## 11. Key Differences: Volume Fill vs Surface Bump Cascade

| Aspect | Volume Fill (Current) | Surface Bump Cascade (Target) |
|--------|----------------------|-------------------------------|
| **Structure** | Uniform grid of spheres | One parent + surface bumps |
| **Appearance** | Bubbles stacked in layers | Cauliflower / cumulus |
| **Noise role** | Determines if sphere exists at grid point | Determines bump size on surface |
| **Inter-sphere gaps** | Intentional (creates cell structure) | Filled by overlapping bumps |
| **Scalability** | More spheres = denser fill | More cascade levels = fluffier |
| **Computation** | O(n³) for grid | O(n × cascade_factor^depth) |

---

## 12. Implementation Recommendations

### Phase 1: Basic Cascade
1. Implement Fibonacci sphere point sampling
2. Add noise-driven bump generation
3. Create recursive cascade with depth limit
4. Basic sphere hierarchy output

### Phase 2: Refinement
1. Add directional jitter for organic feel
2. Implement Worley-based bump placement (erosion pattern)
3. Add ellipsoid deformation (flatten for cumulus shape)
4. Hybrid: combine base shape with cascade detail

### Phase 3: Polish
1. Multi-frequency noise for different cascade levels
2. Probabilistic continuation (not all bumps make children)
3. LOD: fewer cascade levels for distant clouds
4. Wind animation via procedural offset

### Performance Considerations

- **Fibonacci sampling:** O(n) per sphere, no spatial acceleration needed
- **Cascade branching factor:** If each sphere creates ~20 children, depth=4 means 20^4 = 160,000 terminal spheres
- **Mitigation:** Size threshold + noise threshold reduces actual count significantly
- **GPU:** This isembarrassingly parallel — perfect for compute shaders

---

## 13. Confidence Assessment

| Topic | Confidence | Notes |
|-------|------------|-------|
| Fibonacci sphere sampling | HIGH | Well-documented, trivial to implement |
| Noise-driven bump size | HIGH | Standard noise techniques apply |
| Cascade termination | MEDIUM | Multiple valid approaches, need experimentation |
| Directional jitter | MEDIUM | Conceptually sound, tuning required |
| Worley erosion pattern | HIGH | Industry-proven in Houdini |
| Hybrid with base ellipsoid | HIGH | Recommended approach |
| Performance (branching factor) | MEDIUM | Concern at depth > 5, manageable with thresholds |

---

## 14. Appendix: Key Code References

### Fibonacci Sphere (Unity C#)
```csharp
static Vector3[] FibonacciSphere(int numPoints, float radius, Vector3 center) {
    Vector3[] points = new Vector3[numPoints];
    float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
    float angleIncrement = Mathf.PI * 2f * goldenRatio;
    
    for (int i = 0; i < numPoints; i++) {
        float t = (float)i / numPoints;
        float inclination = Mathf.Acos(1f - 2f * t);
        float azimuth = angleIncrement * i;
        
        float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
        float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
        float z = Mathf.Cos(inclination);
        
        points[i] = center + new Vector3(x, y, z) * radius;
    }
    return points;
}
```

### Noise Sample on Sphere Surface
```csharp
float SampleOnSphereSurface(Vector3 point, Vector3 sphereCenter, float noiseScale) {
    Vector3 normalizedPoint = (point - sphereCenter).normalized;
    return Noise3D(normalizedPoint * noiseScale);
}
```

---

*Research compiled: 2026-05-07*
*For CloudGenerator project — Cascade approach documentation*
