# Cascade v3.x — Problems & Fixes

## Problem 1: Linear uniform decrease

**Symptom:** All children at level N have identical radius. All grandchildren at level N+1 are identical.

**Root cause:** Fixed ratio + near-identical noise multiplier for all children.

**Fix — each child gets independent size noise:**
```javascript
const sizeNoise = perlin3D(childX * freq, childY * freq, childZ * freq, seed + depth + uniqueId);
childRadius = parent.radius * childRatio * (0.3 + sizeNoise * 0.9)
```
Siblings at same level now have DIFFERENT sizes.

---

## Problem 2: Child ratio capped at 45%

**Symptom:** Ratio locked at max 45%. No child can exceed parent size. But real cauliflower has branches thicker than parent stem.

**Fix — uncap and add golden ratio modulation:**
```javascript
const phi = 1.618;
const phiNoise = perlin3D(childX * freq, seed) * 0.4;
const effectiveRatio = (phi + phiNoise) * childRatioBase * 0.25;
childRadius = parent.radius * effectiveRatio;
// Some children larger, some smaller, following φ-like progression
```

Also remove hardcoded max in slider. Allow 10%–200%.

---

## Problem 3: Only 1 parent sphere, no shape control

**Symptom:** Always 1 root sphere. Can't make elongated/wide/flat clouds.

**Fix — add parent configuration params:**
```javascript
parentCount: 1-20,      // number of root spheres
ellipsoidX: 0.5-2.0,   // X scale multiplier
ellipsoidY: 0.3-1.5,   // Y scale multiplier (smaller = flatter bottom)
ellipsoidZ: 0.5-2.0,   // Z scale multiplier
```

For cumulus (flat bottom): ellipsoidY = 0.5, ellipsoidX/Z = 1.2
For cumulonimbus (tall column): ellipsoidY = 2.0, ellipsoidX/Z = 0.8

---

## Problem 4: Uniform attachment = "virus" look

**Symptom:** Fibonacci gives too regular coverage. Easy to get "virus" pattern. Organic/cauliflower nearly impossible.

**Root cause:**
1. Fibonacci = mathematically perfect distribution (too regular)
2. All surface points treated equally
3. No "clumping" behavior

**Fix — add chaos from 4 sources:**

**A. Jitter the Fibonacci points:**
```javascript
const jitterX = perlin3D(pt.x * 20, seed) * jitterStrength;
const jitterY = perlin3D(pt.y * 20, seed + 1) * jitterStrength;
const jitterZ = perlin3D(pt.z * 20, seed + 2) * jitterStrength;
const jitteredPt = { x: pt.x + jitterX, y: pt.y + jitterY, z: pt.z + jitterZ };
```

**B. Worley clustering — creates clumps:**
```javascript
const worleyVal = WorleyNoise3D(surfacePoint * clusterFreq);
if (worleyVal > clusterThreshold) createBump(); // inside cluster = dense
else if (Math.random() > 0.9) createBump(); // between clusters = sparse
```

**C. Variable bumps per sphere (not fixed for all):**
```javascript
const actualBumps = bumpsPerLevel * (0.5 + noiseForThisSphere * 0.8);
// Some spheres get 20 bumps, others get 80, others get 5
```

**D. Per-child random axis perturbation:**
```javascript
const rotAngle = perlin3D(childX * freq, seed) * Math.PI * 2;
// Rotate the "up" direction for this child sphere
```

---

## Fix 5 (v5.4): Size Min/Max Controls Broken for Non-Sphere Archetypes

**Symptom:**
- Column: spheres too large to fit in viewport; Size Min/Max sliders did nothing
- Platform: Size Min worked but Size Max had no effect; overall sizes unpredictable
- Tree: size controls completely absent; children too small
- Sphere: no size range controls at all
- Jitter and Clustering sliders had no visible effect on any archetype
- Generate button appeared to do nothing when sliders changed

**Root Causes:**

1. **`updateLayerField` didn't handle nested paths** — the condition only checked for `columnParams.`, `platformParams.`, `treeParams.` prefixes. Fields like `sizeRange.min` fell through to `layer[field] = parseFloat(value)`, creating flat keys like `layer['sizeRange.min'] = 5` instead of `layer.sizeRange = {min: 5}`.

2. **`sizeRange` not set in `getDefaultLayer()`** — new layers created via Add Layer didn't have `sizeRange` at all.

3. **`updateLayerArchetype` didn't preserve `sizeRange`** — switching archetype reset the size controls.

4. **Generators didn't use `layer.sizeRange`** — Column/Platform formulas used `baseRadius` from `columnParams`/`platformParams`, not `layer.sizeRange`.

5. **Jitter values too small** — multipliers like `jitter * baseRadius * perlin3D(...)` were too weak to create visible displacement.

**Fixes Applied:**

1. Rewrote `updateLayerField()` to handle any dot-notation path:
```javascript
function updateLayerField(index, field, value) {
  const layer = window._advancedLayers[index];
  if (field.includes('.')) {
    const parts = field.split('.');
    let obj = layer;
    for (let i = 0; i < parts.length - 1; i++) {
      if (!obj[parts[i]]) obj[parts[i]] = {};
      obj = obj[parts[i]];
    }
    obj[parts[parts.length - 1]] = parseFloat(value);
  } else {
    layer[field] = parseFloat(value);
  }
  generate();
}
```

2. Added `sizeRange` to `getDefaultLayer()` for all archetypes:
```javascript
base.sizeRange = { min: 3, max: 10 }; // column, platform, tree
base.sizeRange = { min: 5, max: 20 }; // sphere
```

3. Preserve `sizeRange` in `updateLayerArchetype()`:
```javascript
newLayer.sizeRange = oldLayer.sizeRange;
```

4. **Column sphere radius** — `baseRadius * sizeBase * 0.08 * (0.5 + rNoise * 0.5)` where `sizeBase = sizeRange.min + rNoise * (sizeRange.max - sizeRange.min)`

5. **Platform sphere radius** — `sizeBase * (0.3 + 0.7*(1-dist*0.8)) * jitterFactor`

6. **Sphere child radius** — `sizeBase * sizeMult` where `sizeBase = minRadius + sizeNoise * (sizeMax - minRadius)`

7. **Tree radii** — root: `sizeRange.min`; children: `sizeBase * taperRatio * thicknessFalloff * (0.5 + sizeNoise*0.5) * (0.6 + radiusNoise*0.4)`

8. **Jitter amplification** — column/angle noise ×2, position wobble ×2; tree lateral angles ×(1 + jitter*2)

9. Added try-catch to `generate()` with alert on error for debugging.

---

## Summary: v5.4 New Parameters

| Param | Purpose | Range |
|-------|---------|-------|
| `sizeRange.min` | Minimum sphere radius for layer | 1–30 |
| `sizeRange.max` | Maximum sphere radius for layer | 5–60 |
| `jitter` | Position noise amplitude (amplified ×2 in v5.4) | 0.0–0.5 |
| `clustering` | Worley clustering strength (affects density in Platform, jitterFactor) | 0.0–1.0 |

---

## v5.5 — Enhanced Size Randomization ("Улучшение случайностей")

### Problem: Size variation was too subtle, siblings nearly identical

**Root Causes Identified by Agent Analysis:**

1. **Correlated noise** — same `sizeNoise` drove both `sizeBase` AND `sizeMult`. When high, both high; when low, both low. Never got "small base × large mult" combos.

2. **Aggressive culling** — `if (childRadius < minRadius) continue` with `minRadius=5` clipped 93% of children, leaving only the top ~7% of distribution.

3. **Fixed multipliers** — base constants like `0.2` dominated noise terms, compressing range.

4. **Integer-only Perlin inputs** (Column, Tree) — `perlin3D(floor, r, seed)` with integer coords = discrete lattice samples, no interpolation between cells.

5. **Single noise sample, double usage** — same `rNoise` used for both `sizeBase` and final radius multiplier.

### Fixes Applied:

**SPHERE:**
```javascript
// Before: single correlated noise
const sizeNoise = (perlin3D(...) + 1) * 0.5;
const sizeBase = minRadius + sizeNoise * (sizeMax - minRadius);
const sizeMult = (0.2 + sizeNoise * sizeVariation + effectiveRatio) * 0.3;
if (childRadius < minRadius) continue;

// After: two independent noise sources + power curve
const sizeNoiseBase = Math.pow((perlin3D(...) + 1) * 0.5, 1.5);
const sizeNoiseMult = Math.pow((perlin3D(..., seed + childUniqueId + 500) + 1) * 0.5, 1.5);
const sizeBase = minRadius + sizeNoiseBase * (sizeMax - minRadius);
const sizeMult = (0.05 + sizeNoiseMult * sizeVariation * 2.0 + effectiveRatio) * 0.3;
if (childRadius < minRadius * 0.1) continue;
```

**COLUMN:**
```javascript
// Before: integer-only inputs, single noise
const rNoise = (perlin3D(floor, r, layer.seed + 200, layer.seed) + 1) * 0.5;

// After: fractional offsets for continuity, two independent noises
const rNoiseBase = Math.pow((perlin3D(floor + r * 0.1, r + floor * 0.1, layer.seed + 200, layer.seed) + 1) * 0.5, 1.8);
const rNoiseMult = Math.pow((perlin3D(floor + r * 0.1 + 50, r + floor * 0.1 + 50, layer.seed + 250, layer.seed) + 1) * 0.5, 1.5);
const sphereRadius = baseRadius * sizeBase * 0.12 * (0.1 + rNoiseMult * 0.9);
```

**PLATFORM:**
```javascript
// Before: single correlated noise, uniform edge rings
const rNoise = (perlin3D(px * 0.5, pz * 0.5, layer.seed + 600, 0) + 1) * 0.5;
for (let ring = 1; ring <= edgeRings; ring++) { ... } // hardcoded rings REMOVED

// After: two independent noises, spiralIdx offset for uniqueness
const spiralIdx = interiorPoints.indexOf(pt);
const rNoiseBase = Math.pow((perlin3D(px * 0.5, pz * 0.5, layer.seed + 600 + spiralIdx, 0) + 1) * 0.5, 1.8);
const rNoiseMult = Math.pow((perlin3D(px * 0.5 + 70, pz * 0.5 + 70, layer.seed + 650 + spiralIdx, 0) + 1) * 0.5, 1.5);
const radius = sizeBase * (0.1 + rNoiseMult * 0.9) * jitterFactor;
```

**TREE:**
```javascript
// Before: sizeNoise used twice (correlated)
const sizeNoise = (perlin3D(depth, pathId, seed + 80, 0) + 1) * 0.5;
const childRadius = sizeBase * taperRatio * thicknessFalloff * (0.5 + sizeNoise * 0.5) * (0.6 + radiusNoise * 0.4);

// After: three independent power-curved noises
const sizeNoiseBase = Math.pow((perlin3D(depth, pathId, seed + 80, 0) + 1) * 0.5, 1.8);
const sizeNoiseMult = Math.pow((perlin3D(depth, pathId, seed + 82, 0) + 1) * 0.5, 1.5);
const radiusNoise = Math.pow((perlin3D(depth, pathId, seed + 81, 0) + 1) * 0.5, 2.0);
const childRadius = sizeBase * taperRatio * thicknessFalloff * (0.1 + sizeNoiseMult * 0.9) * (0.3 + radiusNoise * 0.7);
```

### Default Value Changes:
- `sizeVariation`: 0.5 → 1.0
- UI slider max: 100 → 150
- `sizeMult` base: 0.2 → 0.05

### Removed:
- **Edge rings** from Platform archetype — they created hard-coded circular patterns immune to jitter/clustering controls

### Mathematical Summary:

| Archetype | Fix Type | Key Change |
|-----------|----------|-----------|
| Sphere | Decouple + Power curve + Relax culling | `pow(..., 1.5)`, cull at 0.1× |
| Column | Continuity + Decouple + Power curve | `+ r*0.1` fractional inputs, `pow(..., 1.8/1.5)` |
| Platform | Remove rings + Decouple + Uniqueness | Edge rings REMOVED, `+ spiralIdx` offset |
| Tree | Triple independent noise + Power curve | 3 separate `pow(..., 1.8/1.5/2.0)` |

### Result:
- Size distribution now covers full `[min, max]` range more uniformly
- Extreme sizes (very small × very large) now possible in same generation
- Power curve (`Math.pow`) pushes values toward extremes (0 or 1) while maintaining smooth interpolation
- All archetypes respond properly to `jitter` and `clustering` controls

---

## v5.6 — Position Variation ("Per-child chaos")

### Problem: Children locked to archetype shape, appear as regular polyhedra

**Symptom:**
- Sphere children form dodecahedron-like patterns — too regular, no randomness in direction
- All archetypes constrained to their native growth pattern
- `jitter` and `clustering` parameters had minimal effect on child positioning
- No way to create truly chaotic cloud formations

**Root Cause:**
Each child received the same directional displacement calculated from parent's surface normal. No per-child randomization of position.

### Fix: Add `positionVariation` — per-child independent noise offset

Each child sphere now receives an independent random offset scaled by its own radius:

```javascript
// SPHERE archetype
const posNoiseX = (perlin3D(jpt.x, jpt.y, jpt.z, seed + childUniqueId * 200) + 1) * 0.5;
const posNoiseY = (perlin3D(jpt.x + 53, jpt.y, jpt.z, seed + childUniqueId * 200 + 1) + 1) * 0.5;
const posNoiseZ = (perlin3D(jpt.x, jpt.y + 53, jpt.z, seed + childUniqueId * 200 + 2) + 1) * 0.5;
const ox = (posNoiseX - 0.5) * 4 * parent.radius * posVariation;
const oy = (posNoiseY - 0.5) * 4 * parent.radius * posVariation;
const oz = (posNoiseZ - 0.5) * 4 * parent.radius * posVariation;

// COLUMN archetype
const colPosNoiseX = (perlin3D(cos(angle)*dist + wobbleX, y, sin(angle)*dist + wobbleZ, seed + floor*300 + r*17) + 1) * 0.5;
// ... Y/Z with offsets +83, +83
const colOx = (colPosNoiseX - 0.5) * 4 * sphereRadius * posVariation;

// PLATFORM archetype
const platPosNoiseX = (perlin3D(px, y, pz, seed + spiralIdx * 200) + 1) * 0.5;
// ... Y/Z with offsets +41
const platOx = (platPosNoiseX - 0.5) * 4 * radius * posVariation;

// TREE archetype (main + lateral children)
const treePosNoiseX = (perlin3D(childPos.x, childPos.y, childPos.z, seed + pathId * 200) + 1) * 0.5;
// ... Y/Z with offsets +71
const treeOx = (treePosNoiseX - 0.5) * 4 * childRadius * posVariation;
```

### New Parameter:

| Param | Purpose | Range |
|-------|---------|-------|
| `positionVariation` | Strength of per-child position randomization | 0.0 to 2.0 |

- `0.0` = no change, identical to v5.5
- `0.5` = moderate chaos
- `1.0+` = strong chaotic displacement

### Key Implementation Details:

1. **Multiplier**: `4 × radius × positionVariation` (doubled from initial 2×)
2. **Perlin inputs**: Real world coordinates (not abstract indices) for better independence
3. **Axis separation**: Each axis uses different offset (+53, +71, +83, +91, +41) in perlin inputs
4. **Unique seeds**: `childUniqueId * 200`, `floor * 300 + r * 17`, `spiralIdx * 200`, `pathId * 200`

### Where Applied:

**SPHERE:** Children after `perturbed` displacement
**COLUMN:** Ring spheres after wobble (uses real position coords)
**PLATFORM:** Interior spiral points after y-noise (uses px, y, pz)
**TREE:** Main children AND lateral children (uses childPos/lateralPos)

### Default:
```javascript
positionVariation: 0.5
```

---

## v5.7 — Random Seed on Generate

### Problem: Seed parameter lost across updates, no way to quickly explore variations

**Symptom:**
- Each archetype had its own seed handling that got lost during refactoring
- Pressing Generate repeatedly produced identical results
- No easy way to "shuffle" and explore different random variations of the same settings

### Fix: Auto-randomize seed on each Generate press

```javascript
function generate() {
  const randomSeed = Math.floor(Math.random() * 999999);
  for (const layer of layers) {
    layer.seed = randomSeed;
  }
  // ... rest of generation
}
```

### Behavior:
- Every press of Generate button → new random seed (0–999999)
- All layers share the same seed for that generation
- All other settings (sizeRange, positionVariation, jitter, etc.) preserved
- Only randomness regenerated, not configuration

### Effect:
- Click Generate multiple times to cycle through different arrangements
- Same settings → many different valid clouds
- Quick exploration of design space without manual slider fiddling

---

## v5.7b — Integer Inputs to Perlin Noise Causing Silent Failure

### Problem: Visualizer showed nothing (no children of any type) with no console errors

**Symptom:**
- Platform archetype produced no visible spheres
- Column archetype showed nothing
- Tree archetype invisible
- Sphere archetype children absent
- Generate button appeared to do nothing

**Root Cause: Integer inputs to perlin3D cause fade() = 0, preventing interpolation**

In classic Perlin noise, the `fade()` function produces smoothstep values:
```javascript
fade(t) = 6t^5 - 15t^4 + 10t^3
```

When `t` is an integer (0 or 1), `fade(0) = 0` and `fade(1) = 0`. This means:
- Integer lattice coordinates return 0 from fade()
- Gradient dot products computed but multiplied by 0
- Result: ALL lattice points return identical noise value regardless of position

**Locations with integer-only inputs (15 total):**

| Archetype | Line | Problem Code | Fixed Code |
|-----------|------|--------------|------------|
| Sphere child | 732-733 | `perlin3D(jpt.x * noiseScale * 2, ...)` with jpt.x as integer | Added 0.5 offsets |
| Column | 788-790 | `perlin3D(floor + 0.5, r + 0.5, ...)` | Already had 0.1 offsets |
| Platform | 879-880 | `perlin3D(px * 0.5, pz * 0.5, spiralIdx, ...)` | `+ spiralIdx + 0.5` |
| Tree | 981-983 | `perlin3D(depth, pathId, seed + 80, 0)` | `seed` param fixed |
| Lateral | 1006 | `perlin3D(pathId * 10, i * 5, seed, seed)` | Now uses proper seeds |

**Also Fixed:**
- `worley3D` and `invertedWorley` functions missing seed parameter
- All perlin3D calls with 4th arg = 0 were ignoring seed (should pass layer.seed or layer.noiseSalt)

### Fix Applied:

```javascript
// Before (platform) — spiralIdx was integer, seed was 0
const rNoiseBase = Math.pow((perlin3D(px * 0.5, pz * 0.5, layer.seed + 600 + spiralIdx, 0) + 1) * 0.5, 1.8);

// After — add 0.5 fractional offset, proper seed
const rNoiseBase = Math.pow((perlin3D(px * 0.5, pz * 0.5, layer.seed + 600 + spiralIdx, layer.seed) + 1) * 0.5, 1.8);
```

All 15 perlin noise sampling locations now use proper seeds and fractional inputs.

---

## v5.7b — Position Variation: Math.random() вместо Perlin

### Problem: Position variation produced diagonal stretching, not 3D scatter

**Symptom:**
- Setting positionVariation slider had minimal effect
- When it did work, children stretched along diagonal (x=y=z correlation)
- All three axes used similar noise values (correlated)
- Platform "jumped" but didn't truly vary

**Root Cause: Correlated Perlin noise across axes**

```javascript
// Before — all axes sample similar spatial regions
const posNoiseX = perlin3D(id * 1000, 0, 0, seed);
const posNoiseY = perlin3D(0, id * 1000, 0, seed + 1);
const posNoiseZ = perlin3D(0, 0, id * 1000, seed + 2);
// X, Y, Z still correlated because all use same id*1000 magnitude
```

### Fix: Use Math.random() for independent per-axis offsets

```javascript
// After — each axis independent, no correlation
const posVariation = layer.positionVariation !== undefined ? layer.positionVariation : 0.5;
const ox = (Math.random() - 0.5) * 12 * parent.radius * posVariation;
const oy = (Math.random() - 0.5) * 12 * parent.radius * posVariation;
const oz = (Math.random() - 0.5) * 12 * parent.radius * posVariation;
```

### Applied To All 5 Archetypes:

| Archetype | Offset Variables | Base Radius Used |
|-----------|------------------|------------------|
| Sphere child | ox, oy, oz | parent.radius |
| Column | colOx, colOy, colOz | sphereRadius |
| Platform | platOx, platOy, platOz | radius |
| Tree main | treeOx, treeOy, treeOz | childRadius |
| Tree lateral | latOx, latOy, latOz | lateralRadius |

### Key Changes:
1. **Removed perlin3D noise sampling** for position offsets — replaced with `Math.random()`
2. **Added posVariation definition** at top of each generator function (was missing in tree/lateral)
3. **Multiplier**: `12 × radius × positionVariation` (was 4× in v5.6)
4. **UI slider max**: 200 → 500

### Result:
- Each Generate press produces completely different child positions
- No diagonal stretching — axes fully independent
- Chaotic 3D scatter matches expected behavior of "variation"
- Same settings → visually distinct clouds on each press

---

## v5.9 — Color Scheme Alignment + Mobile Fix + Math.random() Position Offsets

### Changes in this session:

#### 1. Position Variation: Replace Perlin noise with Math.random()

**Problem:** Position variation produced diagonal stretching, not 3D scatter.

**Fix:** Used `Math.random()` for independent per-axis offsets:
```javascript
const ox = (Math.random() - 0.5) * 12 * parent.radius * posVariation;
const oy = (Math.random() - 0.5) * 12 * parent.radius * posVariation;
const oz = (Math.random() - 0.5) * 12 * parent.radius * posVariation;
```

**Applied to all 5 archetypes:**

| Archetype | Offset Variables | Base Radius Used |
|-----------|------------------|------------------|
| Sphere child | ox, oy, oz | parent.radius |
| Column | colOx, colOy, colOz | sphereRadius |
| Platform | platOx, platOy, platOz | radius |
| Tree main | treeOx, treeOy, treeOz | childRadius |
| Tree lateral | latOx, latOy, latOz | lateralRadius |

#### 2. Color Scheme Alignment with roadmap.html

Updated visualizer colors to match roadmap design language:

| Element | Old Color | New Color |
|---------|-----------|----------|
| Background | #0a0a1a | #0a0a0f |
| Surface | #16233e | #12121a |
| Border | #0f3460 | #1e1e2e |
| Text | #e0e0e0 | #c8c8d4 |
| Text-dim | #888 | #6e6e82 |
| Accent (primary) | #00d9ff | #4ade80 |
| Accent2 | — | #38bdf8 |
| Danger | #ff4466 | #f87171 |

Applied to:
- Header, buttons, layer headers
- Input range sliders (thumb + track)
- Layer items (border, hover, expanded state)
- Section titles, remove button, clone button
- Scrollbars throughout
- 3D scene background and grid helper

#### 3. Mobile Layout Fix

**Problem:** Layer params panel not scrollable on mobile.

**Fix:**
```css
.layer-params {
  max-height: 55vh;
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: #4ade80 #1e1e2e;
}
```

Also fixed:
- Layer header background: `rgba(15, 52, 96, 0.7)` → `rgba(18, 18, 26, 0.95)`
- Layer header hover effect consistent with new scheme

### Result:
- Each Generate press produces completely different child positions
- No diagonal stretching — axes fully independent
- Chaotic 3D scatter matches expected behavior
- UI consistent with roadmap.html color scheme
- Mobile-friendly scrolling for layer params

---

## v7.5 — PARENT Layer Feature

### Feature: Generate Children on Parent Sphere Surface

**Concept:**
- Any layer (even merged) can be designated as "parent" (основа)
- The next layer AFTER a parent layer generates children ON THE SURFACE of parent spheres
- Parent spheres serve as the "ground" for the next layer's generation
- Only Sphere archetype supports parent mode (other archetypes use normal generation)

### How It Works:

1. **Mark a layer as Parent** — Click PARENT button, select target layer (or last layer if only one)
2. **Layer turns red with "P" badge** — Visual indication of parent status
3. **Add next layer** — This layer will generate on parent's surface
4. **Generate** — Parent layer generates normally; next layer generates children on parent's spheres

**Technical Logic:**
```javascript
const useParentMode = prevWasParent && parentSpheres.length > 0 && generatorSupportsParent(layer.archetype);
```

A layer uses parent mode when:
- Previous layer was marked as isParent
- There are parent spheres available
- The archetype supports parent mode (only Sphere)

### UI Changes:

1. **PARENT button** added between "Add Layer" and "Merge Layers"
2. **Layer selection dialog** appears if multiple layers exist
3. **Red styling** for parent layers (`#f87171` border + background tint)
4. **"P" badge** displayed in layer header
5. **Unmerge button removed** from merged layers

### Parent Mode Generator (Sphere only):

When parent mode is active, `generateSphereLayer(layer, parentSpheres)`:
- Iterates over all parent spheres
- Uses Fibonacci sphere to sample surface points
- Creates children on those surface points using standard noise/clustering
- Children inherit parent's position and radius for surface normal calculation

## v7.6 — Merged Layer as Parent

### Feature: Use Merged Layer Spheres as Parent Surface

**Concept:**
- Merged Layer can be designated as Parent (основа)
- When a merged layer is marked as isParent, its spheres are used as the surface for the next layer's generation
- This allows generating children on previously merged/frozen cloud formations

### How It Works:

1. **Mark Merged Layer as Parent** — Click PARENT button, select merged layer from dialog
2. **Merged Layer turns red with "P" badge** — Visual indication of parent status
3. **Add next layer** — This layer will generate children ON the merged spheres' surface
4. **Generate** — Merged spheres transformed and used as parent surface

### Technical Implementation:

**generateCloud changes:**
```javascript
if (layer.isMerged) {
  if (layer.isParent && _mergedSpheres.length > 0) {
    // Transform merged spheres (apply scale/rotation/offset) and add to parentSpheres
    const transformedMerged = applyMergedTransform(_mergedSpheres);
    parentSpheres = [...parentSpheres, ...transformedMerged];
  }
  prevWasParent = layer.isParent;
  continue;
}
```

**generate() changes:**
```javascript
// Only filter merged layer if it's NOT the parent
const hasParentMerged = _isMerged && window._advancedLayers.some(l => l.isMerged && l.isParent);
if (_isMerged && !hasParentMerged) {
  layers = layers.filter(l => !l.isMerged);
}
```

### UI Changes:

1. **PARENT dialog shows all layers** — including merged layer with "(merged)" label
2. **Merged layer UI turns red** — when isParent is true
3. **3-column grid for buttons** — Add, Parent, Merge in one row

### Workflow:

1. Create sphere layers → Generate
2. Merge → Merged Layer appears
3. PARENT → dialog shows "Layer 0 (merged)"
4. Select merged as parent → turns red with P badge
5. Add new sphere layer → Generate
6. New layer generates children on merged sphere surface

### TODO v7.7:
- [ ] Visual feedback when parent mode is active (highlight next layer?)
- [ ] Support multiple merged layers as parents
- [ ] Allow unmerge to restore individual layers

---

## v8.0 — Load Parent Mesh (Custom OBJ as Parent Surface)

### Feature: Load External OBJ Mesh as Parent for Generation

Позволяет загрузить любой OBJ меш (например череп) и использовать его поверхность как родителя для генерации сфер следующего слоя.

### Concept

```
┌─────────────────────────────────────────────────────────────┐
│  Load Parent Mesh (OBJ file)                                │
│  → Mesh отображается в 3D сцене                            │
│  → Точки на поверхности семплируются (2000 точек)         │
│  → Следующий слой генерирует сферы на этих точках         │
│  → Меш можно скрыть/показать (Toggle)                      │
│  → Меш можно трансформировать (Offset/Scale/Rotation)    │
└─────────────────────────────────────────────────────────────┘
```

### How It Works

1. **Load Parent Mesh** — пользователь выбирает OBJ файл
2. **OBJ Parser** — парсит вершины и грани из OBJ
3. **Surface Sampling** — Monte Carlo семплирование на треугольниках (2000 точек)
4. **Parent Mode** — при генерации следующего слоя используются точки меша вместо сфер
5. **Transform** — Offset/Scale/Rotation применяются к точкам и к визуальному мешу
6. **Visibility** — меш можно скрыть через Toggle (не экспортируется)

### UI Changes

```
┌─────────────────────────────────────────────────────────────┐
│  [Undo] [Redo] [Load Parent Mesh]  ← новая кнопка         │
├─────────────────────────────────────────────────────────────┤
│  [Parent Mesh Section] — появляется после загрузки:         │
│  File: skull.obj                                          │
│  Offset: [X] [Y] [Z]                                     │
│  Scale:  [X] [Y] [Z]                                     │
│  Rot:    [X] [Y] [Z]                                     │
│  [Toggle] [Clear]                                         │
├─────────────────────────────────────────────────────────────┤
│  Layer 0 (sphere, isParent=true, parentMeshFile=skull)   │
│  Layer 1 (sphere) — генерирует на поверхности черепа      │
└─────────────────────────────────────────────────────────────┘
```

### Technical Implementation

**State Variables:**
```javascript
let _parentMeshObject = null;      // THREE.Mesh для рендера
let _parentMeshPointsRaw = [];      // Оригинальные семплированные точки
let _parentMeshPoints = [];         // Трансформированные точки для генерации
let _parentMeshFileName = '';       // Имя файла
let _parentMeshTransform = {       // Трансформ
  offsetX, offsetY, offsetZ,
  sizeX, sizeY, sizeZ,
  rotationX, rotationY, rotationZ
};
```

**OBJ Parser:**
- Поддерживает `v` (вершины) и `f` (грани)
- Грани триангулируются (поддержка polygon faces)
- Фильтрует вырожденные треугольники (area < 0.001)

**Surface Sampling (Monte Carlo):**
```javascript
function sampleMeshSurface(vertices, indices, numPoints) {
  // 1. Построить список треугольников с площадями
  // 2. Weighted random selection по площади
  // 3. Barycentric coordinates для равномерного распределения
  // 4. 2000 точек по умолчанию
}
```

**Transform Integration:**
```javascript
// applyParentMeshTransform() — трансформирует точку:
// 1. Scale (X, Y, Z независимо)
// 2. Rotation (X → Y → Z, как Three.js)
// 3. Offset (сложение)

// updateParentMeshTransform() — применяет трансформ:
// 1. Обновляет _parentMeshTransform
// 2. Применяет к визуальному _parentMeshObject
// 3. Пересчитывает _parentMeshPoints из _parentMeshPointsRaw
// 4. Regenerate
```

**generateCloud() Logic:**
```javascript
// 1. Если слой имеет parentMeshFileName → это carrier
//    → пропускаем генерацию сфер
//    → сохраняем _parentMeshPoints как parentSpheres для следующего

// 2. Если prevWasParent && hasCustomParentMesh
//    → используем _parentMeshPoints для генерации
```

### Workflow

```
1. Load Parent Mesh → выбрать OBJ (например череп)
2. Меш отображается, семплируются 2000 точек
3. Parent Mesh Section показывает трансформ контролы
4. Add Layer → сферы/колоны и т.д.
5. Generate → новый слой генерирует на поверхности меша
6. Adjust Transform → Offset/Scale/Rotation
7. Toggle → скрыть/показать меш
8. Export → экспортирует только видимое
```

### Export Behavior

- Parent mesh **не экспортируется** (это reference geometry)
- Экспортируются только сгенерированные сферы
- Сферы генерируются на трансформированных позициях меша

### Limitations

- Только OBJ формат (no GLTF/GLB)
- no materials/textures from OBJ
- no vertex normals preservation
- Fixed 2000 sampling points

### TODO v8.0:
- [ ] Support GLTF/GLB loading
- [ ] Configurable sample point count
- [ ] Wireframe mode for parent mesh
- [ ] Save/load parent mesh reference in config
- [ ] Multiple parent meshes support

---

## v8.1 — Sphere Count Slider, Button Width Fix, Layer Expand Fix

### Changes
- **Button Width**: GENERATE/REMESH/EXPORT buttons now use `width: 100%` (were 248px fixed)
- **Sphere Count Control**: Slider 1%-400%, multiplies maxSphereCount (5000) by scale factor
  - New field `sphereCountScale` (float, 0.01-4.0, default 1.0)
  - Added to EXPORT_SCHEMA and FIELD_TYPES
- **Layer Expand**: Only `.layer-header` or `.expand-arrow` toggles expand, not entire item

### BUG: sphereCountScale not applied in C# export
The JS `generateCloud()` correctly uses `sphereCountScale` (line 3260-3261), but C# template uses `MaxSphereCount` directly without scaling.

---

## v8.2 Analysis — Feature Architecture Deep Dive

### 1. Parent Mesh System (v8.0)

```
User clicks "Load Parent Mesh"
  → loadParentMesh()
    → OBJ parsing (manual, v/f commands only)
    → Three.js mesh creation + scene add (0x8866aa, opacity 0.4)
    → sampleMeshSurface(vertices, indices, 2000)
      → Triangle extraction + area calculation
      → Monte Carlo weighted sampling (area-weighted)
      → Barycentric uniform distribution
    → Store in _parentMeshPointsRaw + _parentMeshPoints
    → Mark/create layer with parentMeshFileName
    → generate()
      → generateCloud() detects parentMeshFileName
      → Passes _parentMeshPoints to generateSphereLayer()
      → Each mesh point acts as "parent sphere"
      → Fibonacci surface sampling + noise evaluation
      → Child spheres on mesh surface

Transform updates: updateParentMeshTransform() → recomputes _parentMeshPoints → regenerate
```

**State Variables:**
| Variable | Type | Purpose |
|---|---|---|
| `_parentMeshObject` | THREE.Mesh | Visual mesh (purple, 40% opacity) |
| `_parentMeshPointsRaw` | Array<{x,y,z,radius}> | Original sampled points (never mutated after load) |
| `_parentMeshPoints` | Array<{x,y,z,radius}> | Transformed points (recomputed on transform change) |
| `_parentMeshFileName` | string | Display name |
| `_parentMeshTransform` | {offsetX/Y/Z, sizeX/Y/Z, rotationX/Y/Z} | 9-DOF transform |

**Transform applies: scale → rotation(ZYX) → translate**  
**Radius scaled by average of sizeX/Y/Z**

**Only sphere archetype supports parent mesh** (generatorSupportsParent returns true only for 'sphere')

---

### 2. Layer Merge System (v7.4-v7.6)

```
performMerge()
  → Filters visible spheres (respects layer visibility)
  → Stores filtered spheres into _mergedSpheres (frozen snapshot)
  → Stores deep copy of _advancedLayers into _mergedLayerData (for unmerge)
  → Replaces ALL layers with single merged layer (transform-only config)
  → Sets _isMerged = true

Merged layer UI:
  - Header: visibility toggle, expand arrow, name + (P) if parent
  - Body (layer-params): Offset X/Y/Z (-200..200), Size X/Y/Z (10..300%), Rot X/Y/Z (0..360°)
  - NO archetype selector, NO generation params — frozen geometry only

setParentLayer()
  → Toggles isParent flag on selected layer
  → Cannot toggle OFF if layer has parentMeshFileName loaded

In generateCloud():
  - Layer with isMerged flag: skips generation, injects applyMergedTransform(_mergedSpheres) into parentSpheres
  - parentSpheres accumulates across layers (every layer's output adds to it)
  - Next layer with isParent=true receives parentSpheres in generateSphereLayer(layer, parentSpheres)
  - Child spheres grow on parent sphere surfaces

applyMergedTransform():
  → Scale (x*sizeX, y*sizeY, z*sizeZ)
  → Rotation ZYX
  → Offset (x+offsetX, y+offsetY, z+offsetZ)
  → Radius NOT scaled (only position)

unmergeLayers():
  → Restores _advancedLayers from _mergedLayerData
  → Restores sphere layerIndex from _originalLayerIndex
  → Clears _isMerged, _mergedSpheres, _mergedLayerData
```

**Key State:**
| Variable | Type | Purpose |
|---|---|---|
| `_isMerged` | bool | Global merged mode flag |
| `_mergedSpheres` | Sphere[] | Frozen sphere positions at merge time |
| `_mergedLayerData` | Layer[] | Deep copy of layers for unmerge |
| `isMerged` (layer field) | bool | Marks the merged layer in _advancedLayers[0] |
| `isParent` (layer field) | bool | Layer's output feeds into next layer |
| `parentSpheres` | Sphere[] (local) | Accumulated spheres passed to next generator |

**Two parent sources converge in same pipeline:**
- Merged spheres → applyMergedTransform(_mergedSpheres)
- Custom mesh points → applyParentMeshTransform(_parentMeshPointsRaw)

Both go through `generateSphereLayer(layer, parentSpheres)` — same output path.

---

### 3. Export System (v6.0-v7.0)

**EXPORT_VERSION: '7.0'**

**EXPORT_SCHEMA fields:**
```
common: enabled, yOffset, seed, density, jitter, clustering, sizeRange,
        positionVariation, condensationLevel, noiseSalt

sphere: cloudSize, cascadeDepth, bumpsPerLevel, childRatio, sizeVariation,
        parentCount, ellipsoidY, ellipsoidXZ, maxSphereCount, sphereCountScale

column: height, baseRadius, topRadius, floors, ringsPerFloor, wobble
platform: width, depth, centerThickness, edgeThickness, interiorDensity, edgeRings
tree: baseRadius, maxDepth, branchElongation, taperRatio, branchAngle,
      branchProbability, trunkUpBias, lengthFalloff, thicknessFalloff
```

**Generated C# files (from templates):**
- `CloudMath.cs` — noise library (Hash3, Perlin3D, Fbm, Worley3D, FibonacciSphere, PerturbDir)
- `CloudTypes.cs` — CloudArchetype enum, SizeRange, ColumnParams, PlatformParams, TreeParams, CloudLayerConfig, CloudSphere
- `CloudGenerator.cs` — full generator implementation (Generate, GenerateSphereLayer, GenerateColumnLayer, GeneratePlatformLayer, GenerateTreeLayer)
- `CloudGeneratorWindow.cs` — Unity Editor window (layer list, sliders, load/save config, generate button)
- `CloudMeshMerger.cs` — **NOT YET IMPLEMENTED** (roadmap only: FilterInternalSpheres, MergeToMesh, MergeToMeshAdaptive)

**JS vs C# generation differences:**
| Aspect | JS | C# |
|---|---|---|
| Random | Math.random() with noiseSalt | DeterministicRandom (Xorshift) |
| sphereCountScale | ✅ Applied (maxSphereCount * sphereCountScale) | ❌ Ignored (uses MaxSphereCount directly) |
| Parent mesh | ✅ _parentMeshPoints pipeline | ❌ Not exported |
| Merge | ✅ _mergedSpheres pipeline | ❌ Not exported |
| Remesh | ✅ visibility-based filtering | ❌ Not exported |

---

### 4. Unity Plugin — Gap Analysis

**CRITICAL: No Unity C# files exist in repo.** The web visualizer generates them on-the-fly via "Full Generator" export. There is NO committed Unity plugin.

**Existing plugin is Unreal Engine 5 C++:**
| File | Status | Purpose |
|---|---|---|
| CloudGenerator.uplugin | Experimental/disabled | UE plugin manifest (crashes on load) |
| CloudMath.h/cpp | ✅ Working | Perlin, FBM, Worley, Fibonacci sphere, perturbation |
| CloudGeneratorLibrary.cpp | ✅ v6.1 (441 lines) | Full generation — all 4 archetypes, all params |
| CloudGeneratorBPLibrary.cpp | ⚠️ v6.0 (262 lines) | Stripped BP variant — missing 6 params |
| CloudTypes.h | ✅ | FCloudLayerConfig, FCloudSphere, archetypes |

**Unreal C++ vs Web Visualizer feature gaps:**

| Feature | Web v8.0 | UE C++ | UE BP | Unity |
|---|---|---|---|---|
| All 4 archetypes | ✅ | ✅ | ✅ | ✅ |
| sphereCountScale | ✅ | ❌ | ❌ | ❌ |
| NoiseSalt | ✅ | ✅ | ❌ | ❓ |
| CondensationLevel | ✅ | ✅ | ❌ | ❓ |
| Remesh (visibility filter) | ✅ | ❌ | ❌ | ❌ |
| Merge layers (frozen) | ✅ | ❌ | ❌ | ❌ |
| Parent layer mode | ✅ | ❌ | ❌ | ❌ |
| Merged as parent | ✅ | ❌ | ❌ | ❌ |
| OBJ parent mesh | ✅ | ❌ | ❌ | ❌ |
| Layer clone/copy | ✅ | ❌ | ❌ | ❓ |
| Drag & drop reorder | ✅ | ❌ | ❌ | ❌ |
| Undo/redo | ✅ | ❌ | ❌ | ❌ |
| OBJ export | ✅ | ❌ | ❌ | ❌ |
| Config JSON | ✅ | ❌ | ❌ | ❌ |

**UE plugin crash issue:** VS 2026 (v14.50) not supported by UE 5.7 — plugin crashes on module init.

---

### 5. Unity Port Architecture (Recommended)

To bring full visualizer feature parity to Unity:

**Phase 1 — Core Generation (parity with current C# export)**
- `CloudMath.cs` — noise library (already in template)
- `CloudTypes.cs` — types (already in template)
- `CloudGenerator.cs` — generation (add sphereCountScale support)

**Phase 2 — Visualizer Workflow Features**
- `CloudMeshMerger.cs` — implement FilterInternalSpheres, MergeToMesh, MergeToMeshAdaptive
- `CloudParentMeshSystem.cs` — OBJ loading, surface sampling, transform pipeline
- `CloudLayerMergeSystem.cs` — merge/unmerge, applyMergedTransform, parentSpheres conduit
- `CloudVisualizerWindow.cs` — add undo/redo, clone, drag-reorder

**Data model for merged layer (Unity):**
```csharp
[Serializable]
public class MergedLayerData {
    public List<CloudSphere> spheres;  // frozen snapshot
    public Vector3 offset;
    public Vector3 scale;
    public Vector3 rotation;
    public bool isParent;
}
```

**Data model for parent mesh (Unity):**
```csharp
[Serializable]
public class ParentMeshData {
    public string meshFilePath;       // path to OBJ
    public List<Vector3> surfacePoints;  // sampled positions
    public Vector3 offset;
    public Vector3 scale;
    public Vector3 rotation;
}
```

**Key decision points:**
1. Unity uses `JsonUtility` — PascalCase, no $type fields
2. Parent mesh: store sampled points in config JSON OR reload OBJ at runtime
3. Merge: freeze spheres as positions/radii OR as mesh reference
4. Remesh: apply visibility filter in Unity editor OR keep spheres and use mesh for display

---

### TODO v8.2:
- [ ] Implement sphereCountScale in C# CloudGenerator.cs
- [ ] Create CloudMeshMerger.cs (FilterInternalSpheres, MergeToMesh, MergeToMeshAdaptive)
- [ ] Design ParentMeshData serialization for JSON config
- [ ] Design MergedLayerData serialization for JSON config
- [ ] Unity project setup with committed .cs files (not just export-generated)

---

## v8.2 Analysis — Corrected

### Ключевой факт: Export Full Unity C# работает

Экспорт генерирует 4 файла:
- `CloudMath.cs` — математика (Perlin, FBM, Worley, Fibonacci)
- `CloudTypes.cs` — типы и конфиги
- `CloudGenerator.cs` — генерация сфер
- `CloudGeneratorWindow.cs` — Unity Editor window

Эти файлы были сделаны в коммите `7ebfe2b5` (v6.1) и работают корректно.

---

### Фичи v7 и v8 НЕ включены в экспорт

При экспорте передаются только **параметры слоёв** (конфиг JSON). Следующие фичи — это **runtime состояние**, которое не экспортируется:

| Фича | Что в JS | Что экспортируется | Что нужно для Unity |
|------|----------|-------------------|---------------------|
| **Remesh** (v7.0) | `_isRemeshed`, `_visibleSpheres`, `_remeshedGeometry` | ❌ Ничего | Реализовать `CloudMeshMerger.cs` |
| **Merge** (v7.4) | `_mergedSpheres` (массив позиций), `_isMerged` | ❌ Ничего | Экспортировать как `CloudMeshPositions.cs` + transform |
| **Parent mode** (v7.5) | `isParent` флаг на слое | ✅ `isParent: bool` в JSON | Уже в конфиге |
| **Merged as parent** (v7.6) | `_mergedSpheres` → `parentSpheres` | ❌ Не экспортируется | Нужна логика в C# генераторе |
| **Parent mesh OBJ** (v8.0) | `_parentMeshPoints` (2000 точек), `_parentMeshTransform` | ❌ Не экспортируется | Либо путь к OBJ, либо точки в JSON |
| **sphereCountScale** (v8.1) | Слайдер 1-400% | ✅ В схеме, но **баг: не применяется в C# шаблоне** | Исправить шаблон |

---

### Подробный разбор каждой фичи

#### 1. sphereCountScale — БАГ В ШАБЛОНЕ

**JS** (`generateCloud()`, строка 3260):
```javascript
const sphereCountScale = layer.sphereCountScale !== undefined ? layer.sphereCountScale : 1.0;
const maxSphereCount = Math.floor((layer.maxSphereCount !== undefined ? layer.maxSphereCount : 5000) * sphereCountScale);
```
✅ Применяется корректно.

**C# шаблон** (`CloudGenerator.cs`, строка 1260):
```csharp
int maxSphereCount = layer.MaxSphereCount;
```
❌ `SphereCountScale` не используется. Шаблон игнорирует поле.

**Исправление:** Добавить `SphereCountScale` в `CloudGenerator.cs` шаблон.

---

#### 2. Remesh — CloudMeshMerger.cs не существует

В JS это работает так:
1. `performRemesh()` → `isSphereFullyInternal()` проверяет каждую сферу
2. Внутренние сферы удаляются
3. Оставшиеся объединяются в один `BufferGeometry`

В Unity **этого нет**. `CloudMeshMerger.cs` описан в EXPORT_SYSTEM.md как roadmap, но **не реализован**.

**Что нужно:** Реализовать в C#:
```csharp
public static class CloudMeshMerger {
    public static List<CloudSphere> FilterInternalSpheres(List<CloudSphere> spheres, float epsilon = 0.1f);
    public static Mesh MergeToMesh(List<CloudSphere> spheres, int latSegments = 16, int lonSegments = 16);
    public static Mesh MergeToMeshAdaptive(List<CloudSphere> spheres, float segmentsPerUnit = 0.5f);
}
```

---

#### 3. Merge Layers — данные не экспортируются

**В JS:**
- `performMerge()` сохраняет сферы в `_mergedSpheres` (массив позиций/радиусов)
- Создаётся один слой с `isMerged: true` и трансформом (offset/size/rotation)
- Исходные параметры слоёв теряются

**Проблема экспорта:**
- `_mergedSpheres` содержит тысячи записей `{x, y, z, radius}` — слишком много для JSON конфига
- Можно экспортировать как `CloudMeshPositions.cs` (статический класс с массивами)
- Но трансформ (Size X/Y/Z, Rotation X/Y/Z) нужно сохранить

**Вариант решения:**
```
Merged Layer экспортируется как:
1. CloudMeshPositions.cs — позиции всех сфер
2. JSON конфиг с доп. полями:
   {
     "isMerged": true,
     "mergedSphereCount": 1234,
     "offsetX": 0, "offsetY": 0, "offsetZ": 0,
     "sizeX": 1.0, "sizeY": 1.0, "sizeZ": 1.0,
     "rotationX": 0, "rotationY": 0, "rotationZ": 0
   }
3. CloudGeneratorWindow читает mergedSphereCount и подгружает CloudMeshPositions
```

---

#### 4. Parent Mesh OBJ — не экспортируется

**В JS:**
- `loadParentMesh()` парсит OBJ, семплирует 2000 точек на поверхности
- Точки хранятся в `_parentMeshPointsRaw` и `_parentMeshPoints`
- Трансформ применяется при изменении

**При экспорте:** ничего не экспортируется. OBJ файл остаётся на диске пользователя.

**Варианты для Unity:**
1. **Хранить путь к OBJ** — при загрузке конфига пересэмплировать точки
2. **Хранить точки в JSON** — 2000 Vector3 это ~24KB, приемлемо
3. **Хранить только трансформы** — а пользователь сам загружает OBJ в Unity

**Рекомендация:** Вариант 1 — путь к OBJ + трансформ. При загрузке в Unity пересоздавать меш и сэмплировать точки.

---

#### 5. Merged как parent для следующего слоя

**В JS это работает так:**
```
Layer 0 (merged, isParent=true)
  → _mergedSpheres трансформируются
  → transform в parentSpheres

Layer 1 (sphere)
  → получает parentSpheres
  → generateSphereLayer(layer, parentSpheres)
  → сферы генерируются НА поверхности merged сферы
```

**В C#:** Такая логика не реализована. `CloudGenerator.Generate()` обрабатывает слои последовательно, но **не накапливает parentSpheres** между слоями.

**Нужно:** Модифицировать `CloudGenerator.Generate()` чтобы он передавал выходные сферы одного слоя как parentSpheres для следующего, если у слоя `isParent = true`.

---

### Итог: что нужно сделать для Unity

| Приоритет | Задача | Файлы |
|-----------|--------|-------|
| 🔴 Срочно | Исправить sphereCountScale в C# шаблоне | CloudGenerator.cs template |
| 🟡 Важно | Реализовать CloudMeshMerger.cs | CloudMeshMerger.cs |
| 🟡 Важно | Добавить логику merged-as-parent в CloudGenerator.Generate() | CloudGenerator.cs |
| 🟡 Важно | Экспорт merged layer как CloudMeshPositions + JSON metadata | Export system |
| 🟢 Доп | Поддержка parent mesh OBJ (путь или точки в JSON) | CloudParentMeshSystem.cs |

### Какие файлы уже экспортируются и работают

| Файл | Статус | Примечание |
|------|--------|-----------|
| CloudMath.cs | ✅ Работает | Perlin, FBM, Worley, Fibonacci — всё ок |
| CloudTypes.cs | ✅ Работает | Enum, SizeRange, ColumnParams, PlatformParams, TreeParams, CloudLayerConfig |
| CloudGenerator.cs | ⚠️ Частично | sphereCountScale игнорируется |
| CloudGeneratorWindow.cs | ✅ Работает | Layer editor, sliders, load/save, generate |

### Файлы которых нет (roadmap)

| Файл | Статус | Номер версии когда появится |
|------|--------|----------------------------|
| CloudMeshMerger.cs | ❌ Не существует | Планировался для v7.0, не реализован |
| CloudParentMeshSystem.cs | ❌ Не существует | v8.0 — не был в планах экспорта |

---

---
