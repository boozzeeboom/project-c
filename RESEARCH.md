# Cloud Generator — Deep Research

## SOURCES
1. Horizon Zero Dawn Real-time Volumetric Cloudscapes (ARTR 2015) — industry standard
2. Jan Wedekind — Procedural Volumetric Clouds (2023) — practical implementation
3. Unity HDRP Volumetric Clouds docs — Unity native approach
4. Shadertoy Perlin-Worley 3D — tileable noise implementation
5. Reddit/Unity Forums — community implementations

---

## KEY FINDINGS

### Best Practice: Perlin-Worley Noise
Horizon Zero Dawn uses combination of:
- **Perlin noise** for base shape (smooth, fog-like)
- **Worley noise** (cellular/Voronoi) for billowy edges
- Inverted Worley at base = fluffy underside

### Noise Stack for Cloud Shapes
```
Level 1 (large scale): Perlin — overall cloud mass
Level 2 (medium):      Worley — erosion, puffy details
Level 3 (small):       FBM + Worley — micro-detail
```

### Weather Effects (from research)
- **Wind**: distort noise coordinates over time
- **Humidity**: controls density threshold
- **Storm**: combines turbulence + darker base + vertical stretching

---

## APPROACH FOR LEONID'S PROJECT

### Sphere-Based Architecture
Instead of raymarching (GPU-heavy), use sphere packing:
1. Worley noise determines cell centers → sphere anchor points
2. Sphere radius = distance to nearest Worley feature point
3. Multiple layers of Worley at different scales
4. Perlin modulates overall density

### Why Worley for Clouds?
- Natural "cell-like" structure matches cloud formation physics
- Edges naturally erode into wispy patterns
- Inverted Worley = billowy tops, flat bottoms (cumulus shape)

### Parameters to Expose
- `coverage` (0-1): how much sky is filled
- `density` (0-1): how thick clouds are
- `turbulence` (0-1): wind distortion intensity
- `humidity` (0-1): affects sphere size
- `stormIntensity` (0-1): vertical stretch + darkness
- `erosionScale`: how much Worley erodes Perlin shape

---

## ALGORITHM

```
For each grid cell:
  1. sample Perlin3D(pos * scale) → base density
  2. sample Worley3D(pos * erosionScale) → erosion factor
  3. density = base * erosion → threshold → spawn sphere
  4. radius = Worley distance * humidity factor
  5. density modulated by height profile (flatter at bottom)
```

---

## STORM MODE
- Increase vertical scale of spheres
- Add sharp edges (lower erosion threshold)
- Darken density (storm clouds are denser)

## REFERENCES
- Perlin-Worley 3D implementation: shadertoy.com/view/3dVXDc
- Horizon Zero Dawn paper: advances.realtimerendering.com/s2015/
- Jan Wedekind: wedesoft.de/software/2023/05/03/volumetric-clouds/