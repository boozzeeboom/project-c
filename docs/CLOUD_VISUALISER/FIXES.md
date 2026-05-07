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

## Summary: New Parameters Needed

| Param | Purpose | Range |
|-------|---------|-------|
| `sizeVariation` | How much siblings differ in size | 0.0–1.0 |
| `childRatioMax` | Upper bound of child/parent ratio | 10%–200% |
| `parentCount` | Number of root spheres | 1–20 |
| `ellipsoidX/Y/Z` | Shape scaling per axis | 0.3–2.0 |
| `jitterStrength` | Jitter amount on surface points | 0.0–0.5 |
| `clusterStrength` | Worley clustering (0=none, 1=strong) | 0.0–1.0 |
| `variableBumps` | Randomize bumps count per sphere | 0.0–1.0 |

---

## Implementation Order

1. **Fix 1** — per-child size noise
2. **Fix 2** — uncap ratio, φ-modulation
3. **Fix 3** — parentCount + ellipsoidXYZ
4. **Fix 4** — jitter + cluster + variable bumps
