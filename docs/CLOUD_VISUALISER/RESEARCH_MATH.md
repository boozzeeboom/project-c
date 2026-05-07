# MATHEMATICS OF SPHERE-BASED PROCEDURAL CLOUD SHAPE GENERATION

**Context:** We have a sphere-based cloud generator (visualizer at `docs/CLOUD_VISUALISER/web/visualizer3d.html`). The CURRENT problem: spheres fill a cubic grid uniformly - they produce bubble-like shapes, not real cloud shapes.

**Goal:** Mathematical approaches for placing spheres in 3D space such that their aggregate shape resembles real clouds (cumulus, stratus, cumulonimbus/storm).

---

## 1. REAL CLOUD MORPHOLOGY

### 1.1 Cloud Types and Their Geometry

**Cumulus (fair weather):**
- **Shape:** Lumpy, cauliflower-like, rounded tops, FLAT bottoms
- **Flat bottom origin:** Clouds form at a specific ALTITUDE where temperature = dew point (condensation level). Below this, air is too dry. Above this, water condenses. This creates a sharp horizontal boundary at the base.
- **Fluffy top origin:** Turbulent rising air creates irregular, puffy upper surfaces
- **Scale:** Width ~1-2km, Height ~1-3km

**Stratus (layered):**
- **Shape:** Horizontal layers, diffuse edges
- **Formation:** Large-scale lifting (warm front), no strong convection
- **Scale:** Can cover thousands of km horizontally, thin vertically (~100-500m)

**Cumulonimbus (storm):**
- **Shape:** Massive vertical column + ANVIL top spreading horizontally
- **Structure:**
  - Column: 2-5km wide, can reach 10-15km tall
  - Anvil: Spreads at tropopause (~10km) due to temperature inversion
  - "Incus" (anvil) forms because stable air at tropopause prevents further vertical growth
- **Scale:** Width 2-5km, total height up to 15km

### 1.2 The "Flat Bottom / Fluffy Top" Mathematical Model

The flat bottom is a **hard threshold** at condensation level:
```
density(p) = 0, if y < condensationLevel
density(p) = fbm_noise(p), if y >= condensationLevel
```

The fluffy top is a **soft boundary** defined by noise:
```
top_boundary = base_height + noise(x, z) * turbulence_height
```

**Confidence: HIGH** - This is physically accurate for cumulus formation.

---

## 2. SPHERE PLACEMENT FOR CLOUD-LIKE SHAPES

### 2.1 The Core Insight: Two-Component Shape Generation

Real clouds are NOT uniform blob clusters. They have:
1. **LARGE-SCALE SHAPE** (overall silhouette - smooth, convex)
2. **MEDIUM DETAIL** (bumpy, cauliflower surface)
3. **FINE DETAIL** (wispy edges, tendrils)

The key fix for "bubble-like" clouds is **NOT TO PLACE SPHERES UNIFORMLY IN A CUBE**. Instead:

**Step 1: Define a CLOUD VOLUME BOUNDARY (large-scale shape)**
- Use a smooth 3D function (e.g., ellipsoid with noise-displaced edges)
- Spheres OUTSIDE this boundary are NOT placed
- Spheres INSIDE are placed with varying density

**Step 2: Modulate density INSIDE the boundary**
- Use FBM + Worley noise to create "empty" regions inside the cloud
- This creates the characteristic "bumpy" cloud look

### 2.2 Shape-Defining Functions

**A. Large-scale boundary: Ellipsoid + FBM displacement**
```csharp
// Define cloud volume as distorted ellipsoid
float CloudBoundary(Vector3 p, Vector3 center, Vector3 size, float distortion)
{
    // Normalize to ellipsoid shape
    Vector3 q = (p - center) / size;
    float dist = q.magnitude;
    
    // Add FBM distortion to make it bumpy
    float displacement = fbm(p * 0.5) * distortion;
    
    // Soft boundary: 1 inside, 0 at edge, -1 outside
    return 1.0f - dist + displacement;
}
```

**B. Density modulation with Worley noise:**
```csharp
// Worley noise gives distance to nearest cell edge
// High Worley = in middle of cell = INSIDE cloud
// Low Worley = near cell edge = sparse/cloud edge
float worleyValue = WorleyNoise3D(p * frequency);
float density = smoothstep(threshold, 1.0f, worleyValue);
```

**C. Combined shape formula:**
```csharp
float CloudDensity(Vector3 p)
{
    // 1. Large-scale envelope (smooth, convex)
    float envelope = 1.0f - SmoothEllipsoid(p, cloudCenter, cloudSize);
    
    // 2. Add medium-scale cauliflower detail
    envelope += fbm(p * 2.0) * 0.3;
    
    // 3. Subtract Worley-based "holes" for fluffy edges
    float worleyDetail = WorleyNoise3D(p * 8.0);
    envelope -= worleyDetail * 0.4;
    
    // 4. Height profile (flat bottom)
    float heightFactor = CondensationProfile(p.y);
    
    return envelope * heightFactor;
}
```

### 2.3 The WORLEY NOISE + FBM Combination (Critical Technique)

This is the standard game industry technique (Horizon Zero Dawn, etc.):

**Worley noise basics:**
- Place random seed points in 3D space
- For any point, compute distance to nearest seed
- Result: "cell-like" pattern where distance is HIGH in cell centers, LOW at edges

**Why Worley creates cloud-like edges:**
```
When you use 1 - Worley(p):
- Cell centers = high density (near 1)
- Cell edges = low density (near 0)
- This creates rounded, "blob" shapes

Combined with FBM:
- FBM defines WHERE clouds exist (large-scale)
- Worley defines HOW FILLING they are (cauliflower detail)
- Result: fluffy, organic edges
```

**Perlin-Worley noise (the industry standard):**
1. Generate Perlin noise at multiple octaves
2. Generate Worley noise 
3. Combine: Perlin provides smooth base, Worley adds edge detail

**Confidence: HIGH** - This is documented in multiple AAA game engines (Horizon Zero Dawn, Decima Engine).

---

## 3. CONVECTION MODEL FOR CUMULUS

### 3.1 Physical Model

Real cumulus forms from **thermals** - columns of warm air rising from the ground. As air rises:
- It expands (pressure decreases)
- It cools
- At condensation level, water vapor condenses → cloud forms
- Turbulence at top creates fluffy appearance

### 3.2 Mathematical Plume Model

**Vertical density profile (Gaussian entrainment):**
```csharp
float ConvectionProfile(float y, float baseHeight, float cloudTop)
{
    // Plume center - most dense at middle of vertical range
    float plumeCenter = (baseHeight + cloudTop) * 0.5f;
    float spread = (cloudTop - baseHeight) * 0.4f;
    
    // Gaussian profile
    float profile = exp(-pow(y - plumeCenter, 2) / (2 * spread * spread));
    
    // Add entrainment - density decreases as you go up (air mixes with drier surrounding air)
    float entrainment = exp(-y * 0.1f); // Tune 0.1 for faster/slower decay
    
    return profile * entrainment;
}
```

**Horizontal spread (plume widens with height):**
```csharp
Vector3 PlumeOffset(Vector3 p, float y, float baseHeight)
{
    float heightFraction = (y - baseHeight) / maxHeight;
    float spread = heightFraction * maxSpread;
    
    // Add turbulence-based horizontal displacement
    float turbulence = fbm(p * 0.5 + vec3(0, y * 0.1, 0));
    
    return vec3(turbulence * spread, 0, turbulence * spread);
}
```

**Convection + Shape combined:**
```csharp
float CumulusDensity(Vector3 p)
{
    // Start with a vertical cylinder/ellipsoid
    float shape = EllipsoidDistort(p, center, vec3(width, height, width));
    
    // Apply convection profile (more density in the rising column)
    float convection = ConvectionProfile(p.y, cloudBase, cloudTop);
    
    // Add horizontal spread with height
    p.xz += PlumeOffset(p, p.y, cloudBase);
    
    // Add cauliflower detail
    float detail = 1.0f - WorleyNoise3D(p * detailFreq);
    
    // Flat bottom - hard cutoff at condensation level
    float bottom = smoothstep(cloudBase - 10, cloudBase, p.y);
    
    return shape * convection * detail * bottom;
}
```

**Confidence: MEDIUM** - Convection physics are accurate, but simplified for procedural generation.

---

## 4. HEIGHT-BASED DENSITY PROFILES

### 4.1 Stratus (Layered) - Horizontal Layers

```csharp
float StratusDensity(Vector3 p)
{
    // Slow variation in Y direction = horizontal layer
    float layer = fbm(vec3(p.x * 0.5, 0, p.z * 0.5)); // Y is constant = layer
    
    // Add fine detail
    layer += fbm(p * 3.0) * 0.2;
    
    // Height-based envelope
    float heightEnvelope = smoothstep(bottom, bottom + thickness, p.y) * 
                          smoothstep(top, top - thickness, p.y);
    
    return layer * heightEnvelope;
}
```

### 4.2 Cumulus (Vertical Convection)

```csharp
float CumulusDensity(Vector3 p, float cloudBase)
{
    // Start with smoothed ellipsoid for overall shape
    float shape = SmoothEllipsoid(p, center, vec3(1.0, 1.5, 1.0));
    
    // Add puffy top detail
    float topNoise = fbm(p * 2.0 + vec3(0, p.y * 0.5, 0));
    
    // Flat bottom at cloudBase
    float bottom = smoothstep(cloudBase - 50, cloudBase + 50, p.y); // Soft transition zone
    
    // Apply height profile (Gaussian with entrainment)
    float heightProfile = GaussianProfile(p.y, cloudBase, cloudTop);
    
    return shape * topNoise * bottom * heightProfile;
}
```

### 4.3 Density Profile Functions

**Gaussian Profile (soft top, soft bottom):**
```csharp
float GaussianProfile(float y, float base, float top)
{
    float center = (base + top) * 0.5f;
    float sigma = (top - base) * 0.3f;
    return exp(-pow(y - center, 2) / (2 * sigma * sigma));
}
```

**Exponential Profile (sharp top, soft bottom):**
```csharp
float ExponentialProfile(float y, float base, float top)
{
    float normalized = (y - base) / (top - base);
    return exp(-normalized * 2.0f) * (1.0f - normalized);
}
```

**Step + Noise Profile (flat bottom, noisy top):**
```csharp
float FlatBottomNoisyTop(float y, float base, float top, float noiseAmount)
{
    float flatBottom = smoothstep(base - 20, base + 20, y);
    float noisyTop = 1.0f - (y - base) / (top - base) + fbm(vec3(y)) * noiseAmount;
    return flatBottom * noisyTop;
}
```

**Confidence: HIGH** - These profiles match observed cloud density distributions.

---

## 5. WORLEY NOISE FOR CLOUD BOUNDARIES

### 5.1 How Worley Noise Creates Cloud Edges

**Standard Worley noise (cellular/Voronoi):**
- Seeds randomly placed in 3D grid
- For point p, compute distance d to nearest seed
- Return d

**This creates:**
- Cell CENTER: high value (surrounded by seeds)
- Cell EDGE: low value (equidistant from multiple seeds)
- Between cells: smooth gradient

**For clouds, we use INVERTED Worley:**
```csharp
float InvertedWorley3D(Vector3 p, float freq)
{
    float w = WorleyNoise3D(p * freq);
    return 1.0f - w; // Now cell edges = low, cell centers = high
}
```

**When combined with threshold:**
```csharp
float density = InvertedWorley(p, 4.0);
// High frequency Worley = tight cells = fine detail
// Threshold at 0.5 = only cell centers are "cloud"
// Result: bumpy, cauliflower surface
```

### 5.2 FBM + Worley Layering

The standard approach for real-time cloud rendering (Horizon Zero Dawn, etc.):

```csharp
// Low frequency - defines overall shape
float largeShape = 1.0f - PerlinNoise3D(p * 0.5);

// Medium frequency - adds cauliflower detail  
float mediumDetail = InvertedWorley3D(p * 4.0);

// High frequency - fine edge detail
float fineDetail = InvertedWorley3D(p * 12.0);

// Combine
float cloudShape = largeShape * mediumDetail * fineDetail;
```

**Why this works:**
- Perlin gives smooth, natural-looking base
- Worley at medium freq creates "lobes" and "bumps" characteristic of real clouds
- Worley at high freq creates wispy, detailed edges

### 5.3 C# Implementation of Worley Noise

```csharp
// Simple Worley noise implementation
public float WorleyNoise3D(Vector3 p, float frequency)
{
    // Find the integer cell containing point p
    int ix = (int)floor(p.x * frequency);
    int iy = (int)floor(p.y * frequency);
    int iz = (int)floor(p.z * frequency);
    
    float minDist = float.MaxValue;
    
    // Check 3x3x3 neighborhood of cells
    for (int dx = -1; dx <= 1; dx++)
    for (int dy = -1; dy <= 1; dy++)
    for (int dz = -1; dz <= 1; dz++)
    {
        // Random seed for this cell
        // Use hash function for deterministic randomness
        int cellX = ix + dx;
        int cellY = iy + dy;
        int cellZ = iz + dz;
        
        // Pseudo-random point in this cell
        Vector3 cellSeed = RandomPointInCell(cellX, cellY, cellZ);
        
        // Distance from p to this seed
        Vector3 diff = p * frequency - cellSeed;
        float dist = diff.magnitude;
        
        minDist = Math.Min(minDist, dist);
    }
    
    return minDist;
}

// Deterministic pseudo-random point in cell
private Vector3 RandomPointInCell(int x, int y, int z)
{
    // Use a hash to get repeatable "random" values
    float hashX = Hash(x * 12345, y * 67890, z * 13579);
    float hashY = Hash(y * 12345, z * 67890, x * 13579);
    float hashZ = Hash(z * 12345, x * 67890, y * 13579);
    
    return new Vector3(hashX, hashY, hashZ);
}
```

**Note:** This is a simplified implementation. Real Worley uses proper hash functions and handles edge cases. For better quality, consider using existing libraries or precomputing look-up tables.

**Confidence: HIGH** - Worley noise is extensively documented and used in real production systems.

---

## 6. DOMAIN WARPING FOR ORGANIC SHAPES

### 6.1 The Mathematics

Domain warping: instead of `fbm(p)`, compute `fbm(p + displacement(p))` where displacement is itself an fbm.

**Basic warping:**
```csharp
float WarpedFbm(Vector3 p)
{
    // Displacement is another fbm
    Vector3 displacement;
    displacement.x = fbm(p + new Vector3(0, 0, 0));
    displacement.y = fbm(p + new Vector3(5.2f, 1.3f, 8.1f));
    displacement.z = fbm(p + new Vector3(1.7f, 9.2f, 2.8f));
    
    // Warp the original position
    Vector3 warpedP = p + displacement * warpStrength;
    
    return fbm(warpedP);
}
```

**The key insight:** The warped fbm has similar overall shape but MORE ORGANIC, less regular patterns.

### 6.2 Cloud-Specific Warping

```csharp
float WarpedCloudDensity(Vector3 p)
{
    // First level warping - makes shapes less uniform
    Vector3 warp1;
    warp1.x = fbm(p + new Vector3(0, 0, 0)) * 4.0f;
    warp1.y = fbm(p + new Vector3(3.1f, 2.7f, 8.4f)) * 4.0f;
    warp1.z = fbm(p + new Vector3(7.2f, 1.3f, 4.6f)) * 4.0f;
    
    // Second level warping - adds fine detail
    Vector3 p2 = p + warp1;
    Vector3 warp2;
    warp2.x = fbm(p2 + new Vector3(1.7f, 9.2f, 2.8f)) * 2.0f;
    warp2.y = fbm(p2 + new Vector3(8.3f, 2.8f, 5.1f)) * 2.0f;
    warp2.z = fbm(p2 + new Vector3(2.4f, 6.7f, 9.3f)) * 2.0f;
    
    // Final position with double warping
    Vector3 finalP = p + warp2;
    
    // Evaluate fbm at warped position
    return fbm(finalP);
}
```

**Warping scales that work for clouds:**
- First warp: ~4.0 (significant displacement of overall shape)
- Second warp: ~2.0 (finer detail distortion)
- Final fbm: 0.5-1.0 for overall shape, 2.0-4.0 for detail

### 6.3 Inigo Quilez's Domain Warping Formula (Reference)

From iq's article on domain warping:
```
fbm(p + fbm(p + fbm(p)))  // Triple-nested warping
```

**For clouds, simpler versions work well:**
```
cloudDensity = fbm(p + fbm(p * 0.5) * 4.0)  // Single warping often sufficient
```

**Confidence: HIGH** - Domain warping is a standard technique in procedural generation, documented extensively by Inigo Quilez.

---

## 7. CUMULONIMBUS (STORM) GEOMETRY

### 7.1 Shape Components

Cumulonimbus has three distinct regions:

**1. Column (main updraft tower):**
- Tall vertical cylinder/ellipsoid
- Very dense core
- Slight tilt possible (wind shear)

**2. Anvil (spreading top):**
- Horizontal disk/ellipsoid at top
- Much wider than column
- Thin, diffuse edges
- Spreads DOWNWIND (directional)

**3. Scud (irregular bottom):**
- Wispy, turbulent base
- Below condensation level

### 7.2 Mathematical Model

```csharp
float CumulonimbusDensity(Vector3 p, Vector3 stormCenter, float intensity)
{
    // Column parameters
    float columnHeight = 8.0f + intensity * 7.0f; // 8-15km
    float columnRadius = 1.0f + intensity * 1.5f; // 1-2.5km
    
    // Anvil parameters
    float anvilHeight = columnHeight * 0.95f; // Anvil at ~95% of total height
    float anvilRadius = columnRadius * (3.0f + intensity * 2.0f); // Much wider
    float anvilThickness = 0.5f + intensity * 0.5f;
    
    // 1. Column (tall ellipsoid)
    Vector3 columnP = (p - stormCenter) / new Vector3(columnRadius, columnHeight * 0.5f, columnRadius);
    float columnDensity = 1.0f - columnP.magnitude;
    columnDensity = smoothstep(0, 0.3f, columnDensity);
    
    // 2. Anvil (flat ellipsoid at top)
    Vector3 anvilP = p - new Vector3(stormCenter.x, anvilHeight, stormCenter.z);
    anvilP.x /= anvilRadius;
    anvilP.y /= anvilThickness;
    anvilP.z /= anvilRadius;
    float anvilDensity = 1.0f - anvilP.magnitude;
    anvilDensity = smoothstep(0, 0.5f, anvilDensity);
    
    // 3. Add detail to column (cauliflower)
    float columnDetail = InvertedWorley(p * 2.0f) * 0.5f;
    columnDensity += columnDetail;
    
    // 4. Add fine detail to anvil (wispy)
    float anvilDetail = InvertedWorley(p * 6.0f) * 0.3f;
    anvilDensity += anvilDetail;
    
    // 5. Combine with height profile
    float column = columnDensity * smoothstep(0, 2.0f, p.y) * smoothstep(columnHeight + 2.0f, columnHeight - 2.0f, p.y);
    float anvil = anvilDensity * smoothstep(anvilHeight + anvilThickness, anvilHeight - anvilThickness, p.y);
    
    // 6. Add scud (turbulent base)
    float scud = fbm(p * 3.0f + new Vector3(0, stormCenter.y * 0.8f, 0)) * 0.3f;
    scud *= smoothstep(stormCenter.y * 0.8f + 0.5f, stormCenter.y * 0.8f - 1.0f, p.y);
    
    return column + anvil + scud;
}
```

### 7.3 Anvil Shape (Horizontal Spread)

```csharp
// Anvil spreads downwind - directional
Vector2 windDirection = normalize(windVelocity.xz);
float anvilSpread = intensity * 5.0f; // Up to 5x column width

Vector3 anvilCenter = stormCenter + new Vector3(windDirection.x * anvilSpread * 0.3f, anvilHeight, windDirection.y * anvilSpread * 0.3f);

float anvil = EllipsoidDist(p, anvilCenter, 
    new Vector3(columnRadius * anvilSpread, anvilThickness, columnRadius * anvilSpread));
```

**Confidence: MEDIUM-HIGH** - The overall structure is physically accurate. Specific parameter tuning needed for visual quality.

---

## 8. VERTICAL VS HORIZONTAL CLOUD TYPES

### 8.1 Why Stratus is Flat (Horizontal)

Stratus forms when there's **large-scale lifting** without strong convection:
- Warm front passage (warm air slides over cold air)
- No thermals/spires

**Mathematical difference:**
```csharp
// Stratus: Noise varies SLOWLY in Y direction
float stratus = fbm(vec3(p.x * 0.3, p.y * 0.05, p.z * 0.3));
//                                      ^ very small = slow variation = horizontal layer

// Cumulus: Noise varies QUICKLY in Y direction  
float cumulus = fbm(vec3(p.x * 0.5, p.y * 1.5, p.z * 0.5));
//                                      ^ larger = fast variation = vertical structure
```

### 8.2 Transition Between Types

Cloud type is controlled by **environmental stability**:
- Unstable atmosphere → vertical development (cumulus, cumulonimbus)
- Stable atmosphere → horizontal layers (stratus)

**Procedural parameter:**
```csharp
float cloudTypeFactor; // 0 = stratus, 1 = cumulus

// Blend between slow-Y and fast-Y noise
float slowNoise = fbm(vec3(p.x * scale, p.y * 0.05, p.z * scale));
float fastNoise = fbm(vec3(p.x * scale, p.y * 1.5, p.z * scale));
float cloud = lerp(slowNoise, fastNoise, cloudTypeFactor);
```

**Confidence: HIGH** - This is the standard atmospheric science model for cloud formation.

---

## 9. NOISE OCTAVES FOR DIFFERENT CLOUD SCALES

### 9.1 Scale Hierarchy

**Large-scale (overall silhouette):**
- Frequency: 0.5-1.0
- Amplitude: 0.6-1.0
- Octaves: 1-2
- Defines: Cloud extent, overall shape

**Medium-scale (fluffy bumps):**
- Frequency: 2.0-4.0
- Amplitude: 0.3-0.5
- Octaves: 3-4
- Defines: Cauliflower surface detail

**Fine-scale (wispy edges):**
- Frequency: 8.0-16.0
- Amplitude: 0.1-0.2
- Octaves: 2
- Defines: Edge detail, tendrils

### 9.2 C# Implementation

```csharp
public float CloudFbm(Vector3 p, int octaves)
{
    float value = 0.0f;
    float amplitude = 1.0f;
    float frequency = 1.0f;
    float amplitudeSum = 0.0f;
    
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * noise(p * frequency);
        amplitudeSum += amplitude;
        
        amplitude *= 0.5f;  // Gain = 0.5 (standard for clouds)
        frequency *= 2.0f;
    }
    
    return value / amplitudeSum;
}
```

**Standard octaves for cloud types:**
- Cumulus: 4-6 octaves (moderate detail)
- Cumulonimbus: 6-8 octaves (high detail needed for anvil)
- Stratus: 2-3 octaves (smooth, less detail)

**Confidence: HIGH** - This octave structure matches both game industry practice and atmospheric science.

---

## 10. SPIRAL/VORTEX PATTERNS IN STORM CLOUDS

### 10.1 Physics

Storm clouds often have **rotating updrafts** (mesocyclones in supercells). This creates spiral banding patterns.

### 10.2 Mathematical Model

**Simple spiral (2D in horizontal plane):**
```csharp
Vector3 SpiralDisplacement(Vector3 p, Vector3 center, float strength, float twist)
{
    Vector3 offset = p - center;
    float angle = atan2(offset.z, offset.x);
    float radius = offset.magnitude;
    
    // Add twist based on height
    float spiralAngle = angle + p.y * twist;
    
    // Calculate displacement
    float displacement = sin(spiralAngle) * strength * radius;
    
    return new Vector3(
        -offset.z / radius * displacement,
        0,
        offset.x / radius * displacement
    );
}
```

**3D Vortex:**
```csharp
Vector3 VortexField(Vector3 p, Vector3 center, float strength, float pitch)
{
    Vector3 r = p - center;
    float dist = r.magnitude;
    
    // Tangential velocity
    Vector3 tangent = cross(new Vector3(0, 1, 0), r).normalized;
    
    // Vertical velocity (upward in center)
    float verticalVelocity = exp(-dist * 0.5f) * strength;
    
    // Combine
    Vector3 vortex = tangent * strength / (dist + 1.0f);
    vortex.y += verticalVelocity;
    
    return vortex;
}
```

**Application to cloud density:**
```csharp
float VortexCloudDensity(Vector3 p)
{
    // Base cloud shape
    float density = CumulonimbusBaseShape(p);
    
    // Apply spiral displacement
    Vector3 spiralDisp = SpiralDisplacement(p, stormCenter, 0.3f, 0.1f);
    
    // Evaluate density at displaced point
    float spiraledDensity = CloudFbm(p + spiralDisp, 4);
    
    return density * spiraledDensity;
}
```

**Confidence: MEDIUM** - Spiral patterns are observed but difficult to tune procedurally.

---

## 11. RECOMMENDED ALGORITHM FOR IMPLEMENTATION

### 11.1 Priority Order

Based on research, implement in this order:

**Phase 1: Basic cloud shape (HIGH IMPACT)**
1. Ellipsoid boundary for cloud volume
2. FBM for large-scale shape variation
3. Flat bottom + noisy top profile

**Phase 2: Cloud-like edges (HIGH IMPACT)**
4. Worley noise for cauliflower detail
5. Combine FBM + Worley for shape
6. Domain warping for organic look

**Phase 3: Cloud types (MEDIUM IMPACT)**
7. Stratus: slow-Y noise
8. Cumulus: fast-Y noise + convection profile
9. Cumulonimbus: column + anvil model

**Phase 4: Polish (LOW-MEDIUM IMPACT)**
10. Spiral/vortex for storm clouds
11. Multi-octave tuning

### 11.2 Pseudocode: Recommended Approach

```csharp
public class CloudGenerator
{
    public float GenerateCloudDensity(Vector3 p, CloudType type, CloudParams parms)
    {
        // 1. Define cloud center and size
        float boundary = 1.0f - SmoothEllipsoid(p, parms.center, parms.size);
        
        // 2. Apply height profile (flat bottom for cumulus types)
        if (type == CloudType.CUMULUS || type == CloudType.CUMULONIMBUS)
        {
            boundary *= FlatBottomProfile(p.y, parms.condensationLevel, parms.turbulenceHeight);
        }
        else // stratus
        {
            boundary *= LayerProfile(p.y, parms.layerBottom, parms.layerTop);
        }
        
        // 3. Add large-scale FBM variation
        float largeShape = fbm(p * parms.largeScaleFreq, 2);
        boundary += largeShape * parms.largeShapeAmount;
        
        // 4. Add Worley detail (cauliflower)
        float worleyDetail = InvertWorley(p * parms.detailFreq);
        boundary *= worleyDetail * parms.detailAmount + (1.0f - parms.detailAmount);
        
        // 5. Domain warping for organic look
        Vector3 warpedP = p + WarpVector(p) * parms.warpStrength;
        float warpedFbm = fbm(warpedP * parms.warpFreq, 3);
        boundary += warpedFbm * parms.warpEffectAmount;
        
        return boundary;
    }
    
    private Vector3 WarpVector(Vector3 p)
    {
        return new Vector3(
            fbm(p + new Vector3(0, 0, 0)),
            fbm(p + new Vector3(5.2f, 1.3f, 8.1f)),
            fbm(p + new Vector3(1.7f, 9.2f, 2.8f))
        ) * 4.0f; // warpStrength = 4.0
    }
    
    private float FlatBottomProfile(float y, float base, float topFluff)
    {
        float bottom = smoothstep(base - 10, base + 10, y);
        float top = 1.0f - (y - base) / topFluff;
        return bottom * top;
    }
    
    private float LayerProfile(float y, float bottom, float top)
    {
        return smoothstep(bottom - 50, bottom + 50, y) * 
               smoothstep(top + 50, top - 50, y);
    }
}
```

### 11.3 Key Parameter Ranges

| Parameter | Range | Notes |
|-----------|-------|-------|
| largeScaleFreq | 0.5-1.0 | Lower = bigger clouds |
| detailFreq | 4.0-8.0 | Higher = finer detail |
| warpStrength | 3.0-5.0 | How much domain warping |
| condensationLevel | 1000-2000m | Cloud base height |
| turbulenceHeight | 500-2000m | How tall fluffy top extends |

### 11.4 Sphere Placement Logic

```csharp
public bool ShouldPlaceSphere(Vector3 p, float threshold)
{
    float density = GenerateCloudDensity(p, cloudType, params);
    return density > threshold && 
           p.y >= params.condensationLevel; // Only above flat bottom
}
```

**Tune threshold** to control how "full" vs "fluffy" the cloud appears:
- Higher threshold → sparse, wispy clouds
- Lower threshold → dense, puffy clouds

**Confidence: HIGH** - This algorithm structure is well-documented across multiple sources (Horizon Zero Dawn, various SIGGRAPH papers).

---

## 12. KEY REFERENCES

1. **Horizon Zero Dawn cloud system** (SIGGRAPH 2015, 2017)
   - "The real-time volumetric cloudscapes of Horizon Zero Dawn"
   - "Nubis: Authoring realtime volumetric cloudscapes with the Decima Engine"
   - Source: https://www.guerrilla-games.com/read/the-real-time-volumetric-cloudscapes-of-horizon-zero-dawn

2. **Inigo Quilez articles:**
   - Domain Warping: https://iquilezles.org/articles/warp/
   - fBM: https://iquilezles.org/articles/fbm/
   - Various noise techniques

3. **Perlin's cloud particle system:**
   - "Applying Noise to Particle Clouds" (Ken Perlin)
   - Source: https://cs.nyu.edu/~perlin/experiments/vpuff/

4. **Real-time cloud rendering:**
   - Reindernijhoff's shader implementation: https://reindernijhoff.net/2018/05/volumetric-clouds-himalays/
   - Meteoros (Decima engine port): https://github.com/AmanSachan1/Meteoros

5. **Cloud physics:**
   - Self-organized criticality in cumulus clouds (Phys Rev E 2021)
   - Various meteorological texts on convection and cloud formation

---

## 13. SUMMARY OF KEY FORMULAS

**FBM (Fractional Brownian Motion):**
```
fbm(p) = sum(noise(2^i * p) * 0.5^i) for i = 0 to N
```

**Domain Warping:**
```
warped(p) = fbm(p + fbm(p) * warpStrength)
```

**Worley Noise:**
```
worley(p) = min(distance to nearest seed point)
cloudWorley(p) = 1 - worley(p * freq)
```

**Convection Profile:**
```
profile(y) = Gaussian(y, center, spread) * exp(-y * entrainmentRate)
```

**Flat Bottom:**
```
flatBottom(y, base) = smoothstep(base - epsilon, base + epsilon, y)
```

**Combined Cloud Density:**
```
density(p) = boundary(p) * heightProfile(p) * detailNoise(p) * warping(p)
```

---

**Document Version:** 1.0
**Confidence Level:** HIGH for core techniques (FBM, Worley, domain warping, height profiles), MEDIUM for advanced features (vortex, detailed physics)
**Recommendation:** Start with Phase 1+2 algorithms, then extend to Phase 3+4 based on visual needs.