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
