# Realistic 3D Cloud Generation Research

**Project:** Project C / TheGravity - Procedural Cloud Generator  
**Research Date:** 2026-05-07  
**Status:** Complete

---

## Executive Summary

The current sphere-grid approach with basic FBM/Perlin noise produces "clouds" that look like clusters of spheres — not realistic clouds. To achieve realistic cumulus, stratus, and cumulonimbus clouds, you need a fundamentally different approach centered on **volumetric raymarching with Worley-based noise**.

**Recommended approach:** Perlin-Worley 3D noise textures + volumetric raymarching + Beer-Lambert scattering + height gradient density control.

---

## 1. Noise Algorithms for Cloud Shape Generation

### 1.1 Perlin-Worley Noise (Primary Approach) — Confidence: HIGH

The industry-standard approach, pioneered by Horizon Zero Dawn and used in Frostbite, No Man's Sky.

**What it is:**
- Combines Worley noise (cellular/Voronoi) with Perlin/smooth noise
- Worley noise creates the cloud "skeleton" — natural cell-like cloud boundaries
- Perlin/smooth noise fills in detail texture

**Why it works:**
- Worley noise naturally produces cloud-like shapes when viewed as density
- The combination gives you both macro shape (Worley) and micro detail (Perlin)
- Can be precomputed as 3D tileable textures (typically 128x128x128)

**Reference:** "Real-time Volumetric Cloudscapes of Horizon Zero Dawn" (Andrew Schneider, GDC/SIGGRAPH 2015)

### 1.2 FBM Variants — Confidence: MEDIUM-HIGH

Fractal Brownian Motion with multiple octaves is standard for detail.

**Best practices:**
- 4-6 octaves for clouds
- Decreasing amplitude (~0.5) and increasing frequency (~2.0) per octave
- Consider "billow noise" (abs(noise)) for fluffier appearance

**Note:** Pure FBM+Perlin produces the "round but bubbly" look you currently have. Needs Worley component to break into real cloud shapes.

### 1.3 Simplex vs Perlin — Confidence: MEDIUM

Simplex noise has less directional artifacts than Perlin but is slightly more expensive. Either works; Perlin is more commonly used in AAA implementations.

### 1.4 Domain Warping — Confidence: MEDIUM

**What it is:** Using noise to distort the sampling coordinates before sampling another noise.

```
position = position + noise(position) * warpStrength
```

**Effect:** Creates more organic, swirling, turbulent shapes. Used in some advanced implementations.

**Use case:** Add to high-altitude cirrus clouds for wispy detail. For cumulus/storm clouds, less critical.

### 1.5 Curl Noise for Wind — Confidence: HIGH

**What it is:** Divergence-free vector field derived from gradient noise.

**Purpose:** Animate clouds with realistic swirling motion without divergence artifacts.

**Implementation:** Compute curl of 3D FBM gradient to get advection vector for cloud particles/density.

---

## 2. Game Implementation Approaches

### 2.1 Horizon Zero Dawn (Decima Engine) — Confidence: HIGH

**Method:** 
- Precomputed Perlin-Worley 3D noise textures (128x128x128)
- Raymarching in screen space
- 256 steps per ray
- Temporal reprojection (1 full resolution frame per 16 frames)
- Beer-Powder lighting model

**Key innovation:** Split full-resolution rendering over 16 frames for performance.

**Source:** Schneider & Vos, "Real-time Volumetric Cloudscapes of Horizon Zero Dawn" (GDC 2015)

### 2.2 Frostbite (EA) — Confidence: HIGH

**Method:**
- Similar noise approach
- Physically-based atmosphere scattering
- Height-based density gradients

**Source:** Hillaire, "Physically Based Sky, Atmosphere and Cloud Rendering in Frostbite" (SIGGRAPH 2016)

### 2.3 No Man's Sky — Confidence: MEDIUM

Uses volumetric cloud rendering with custom noise. Patch 5.0 (2024) significantly improved the system with "completely rewritten" volumetric cloud renderer.

**Source:** DSOGaming article on Patch 5.0

### 2.4 Unity HDRP — Confidence: HIGH

Unity's High Definition Render Pipeline includes built-in volumetric clouds:
- Uses a combination of 3D noise textures
- Raymarching-based rendering
- Built-in height and density parameters
- Volumetric light/shadow support

**Documentation:** https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@13.1/manual/Override-Volumetric-Clouds.html

---

## 3. Unity-Specific Approach

### 3.1 Recommended: HDRP Volumetric Clouds

If using Unity HDRP, the built-in system is already optimized.

**Parameters:**
- Geometry: defines shape via 3D noise
- Density: overall thickness
- Step count: quality vs performance
- Shadow: receives volumetric shadows

### 3.2 Alternative: Custom Shader + Compute Shader

For more control:

**Architecture:**
1. **Precompute 3D noise textures** (Perlin-Worley) via Compute Shader or C# at startup
2. **Raymarching shader** samples the 3D textures
3. **Height gradient** controls density by altitude
4. **Lighting** via Beer-Lambert + Henyey-Greenstein phase function

**Implementation references:**
- Fewes/CloudNoiseGen (GitHub) — Unity utility class for generating periodic cloud noise textures
- bshishov/UnityVolumetric (GitHub) — Shaders and tools for volumetric rendering
- Rajin Shankar's Unity volumetric project (rajinshankar.com)

### 3.3 For Project C: Hybrid Approach

Given the web-based visualizer (Three.js) and eventual Unity target:

**Phase 1 (Current visualizer):**
- Implement 3D Perlin-Worley noise generation in JS
- Use raymarching in WebGL fragment shader
- Start with precomputed 3D noise textures if performance allows

**Phase 2 (Unity):**
- Use HDRP built-in or migrate noise texture generation to Compute Shader

---

## 4. Mathematical Cloud Formation

### 4.1 Simplified Fluid Dynamics — Confidence: MEDIUM

Real cloud formation involves:
- Moisture condensation at dew point
- Buoyancy from thermal lifting
- Wind shear and turbulence
- Advection

**Simplified for games:**
- Use height gradient (density peaks at certain altitudes)
- Add "thermal" terms (upward motion = lower density at base)
- Wind vector shifts noise sampling coordinates over time

**Source:** "Interactive Simulation of Clouds Based on Fluid Dynamics" thesis

### 4.2 Height-Based Density Profiles

Cloud density varies with altitude:

```
density(altitude) = baseProfile(altitude) * noiseDetails
```

**Typical profiles:**

| Cloud Type | Bottom | Top | Peak |
|------------|--------|-----|------|
| Cumulus | 500m | 2000m | ~1000m |
| Cumulonimbus | 200m | 12000m+ | ~4000m |
| Stratus | surface | 2000m | ~500m |

**For game implementation:** Use parameterized height gradient:
```
density = coverage * heightFalloff * detailNoise
```

### 4.3 Coverage Parameter

Global coverage (0-1) controls how "filled" the sky is:
- 0.3 = scattered puffy cumulus
- 0.6 = partly cloudy
- 0.9 = overcast

---

## 5. Key Parameters for Cloud Generation

### 5.1 Shape vs Density Control

**Primary shape controls:**
1. **Worley noise scale** — large cells = big fluffy clouds
2. **Perlin detail amplitude** — high = very textured, stormy
3. **FBM octaves** — more = more detail, higher cost

**Primary density controls:**
1. **Base density multiplier**
2. **Height gradient peak/width**
3. **Coverage threshold**

### 5.2 Parameter Set for Different Cloud Types

**Cumulus (fair weather):**
```
worleyScale: 1.0
perlinDetail: 0.4
heightPeak: 1000m
heightWidth: 1500m
coverage: 0.4-0.6
turbulence: 0.2
```

**Stratus (overcast):**
```
worleyScale: 0.5
perlinDetail: 0.2
heightPeak: 500m
heightWidth: 2000m
coverage: 0.8-1.0
turbulence: 0.1
```

**Cumulonimbus (thunderstorm):**
```
worleyScale: 2.0
perlinDetail: 0.7
heightPeak: 4000m
heightWidth: 8000m
coverage: 0.5-0.9
turbulence: 0.8
wind: strong
verticalDraft: 1.0
```

---

## 6. Storm/Thunderstorm Cloud Differences

### 6.1 Mathematical Differences — Confidence: HIGH

**Vertical development:**
- Much greater height range (base to top)
- Peak density higher in sky
- "Anvil" top at tropopause (flattened)

**Shape characteristics:**
- More turbulent, irregular edges
- Strong vertical structure
- Larger Worley cell scale
- More FBM octaves for detail

**Density profile:**
```
stormCloud = cumulusProfile * turbulenceFactor * verticalStretch
```

**Wind effects:**
- Strong horizontal wind shear
- Vertical updraft/downdraft zones
- Animated faster than fair weather

**Lighting differences:**
- Darker bases
- Bright tops due to direct sunlight
- Internal shadowing (self-shadowing during raymarch)
- Silver lining effect on edges

---

## 7. Performance Considerations

### 7.1 Raymarching Costs — Confidence: HIGH

**Number of steps:**
- 64 steps: minimum acceptable, visible stepping
- 128 steps: good quality
- 256 steps: high quality (used by Horizon)
- More = exponential cost

**Optimization strategies:**
1. **Adaptive step size** — larger steps in empty space
2. **Early termination** — stop when accumulated opacity ~1
3. **Temporal reprojection** — render 1/N frames at full res, interpolate
4. **Low-resolution raymarch** — render at 0.5x0.5, upscale
5. **Banding reduction** — blue noise or Bayer dithering

### 7.2 Noise Texture Costs — Confidence: HIGH

**3D texture size vs quality:**
- 64x64x64: blocky, noticeable artifacts
- 128x128x128: industry standard (2MB per channel)
- 256x256x256: very high quality, expensive

**Alternative:** Procedural noise in shader (slower but lower memory)

### 7.3 Lighting Costs — Confidence: HIGH

**Shadow rays:**
- Each sample doing a shadow ray to sun = N^2 samples
- Typically do 4-8 shadow steps, not per sample
- Use opacity shadow map for global cloud shadows

**Phase function:**
- Henyey-Greenstein is cheap (one dot product)
- Multiple scattering approximations add cost

### 7.4 Performance Targets

| Target FPS | Steps | Texture Size | Notes |
|------------|-------|--------------|-------|
| 60 FPS | 64 | 64x64x64 | Mobile/low-end |
| 60 FPS | 128 | 128x128x128 | Desktop standard |
| 60 FPS | 256 | 128x128x128 | High quality |
| 30 FPS | 256 | 256x256x256 | Cinematic |

---

## 8. Lighting Model Summary

### 8.1 Beer-Lambert Law — Confidence: HIGH

Describes light absorption through participating media:

```
transmittance = exp(-density * pathLength)
```

**Beer term:** exp(-extinction * sampleDistance)
**Powder term:** for edge brightening effect

### 8.2 Henyey-Greenstein Phase Function — Confidence: HIGH

Approximates angular scattering distribution:

```
HG(g, cosTheta) = (1 - g²) / (1 + g - 2g*cosTheta)^1.5
```

`g` = asymmetry parameter (-1 to 1)
- g = 0: isotropic
- g > 0: forward scattering (sun behind viewer)
- g < 0: backward scattering (sun in front)

For clouds: g ≈ 0.3-0.5 typically

### 8.3 Ambient Term — Confidence: HIGH

Light scattered from all directions, approximated by:
- Sample sky color at cloud position
- Blue-shifted at top, gray at bottom
- Add "silver lining" edge detection

### 8.4 "Beer-Powder" Effect — Confidence: HIGH

Horizon Zero Dawn technique:

```
beerPowder = exp(-density * stepSize) * (1 - exp(-density * stepSize * 2))
```

Creates the bright edge / dark interior cloud appearance.

---

## 9. Implementation Recommendations for Project C

### 9.1 Algorithm to Implement

**Primary:** Perlin-Worley 3D noise with volumetric raymarching

**Implementation plan:**

1. **Noise Generation (Priority 1):**
   - Implement Worley noise (Voronoi/cellular)
   - Combine with Perlin/smooth noise per Horizon Zero Dawn formula
   - Generate tileable 3D texture (128x128x128 minimum)

2. **Raymarching (Priority 2):**
   - Full-screen quad rendering
   - Cast ray through cloud volume bounding box
   - March with fixed step size (start at 128 steps)
   - Accumulate color and opacity

3. **Height Profile (Priority 3):**
   - Add height gradient multiplier
   - Parameterize bottom, top, peak altitude
   - Control with global cloud type parameter

4. **Lighting (Priority 4):**
   - Beer-Lambert absorption
   - Henyey-Greenstein phase function
   - Shadow rays toward sun (reduced frequency)

5. **Animation (Priority 5):**
   - Offset noise sampling by wind vector + time
   - Add curl noise for turbulence
   - Weather map modulation for coverage changes

### 9.2 Reference Implementations

**Open Source:**
- Fewes/CloudNoiseGen (GitHub) — Unity C# noise texture generator
- bshishov/UnityVolumetric (GitHub) — Unity volumetric shader examples
- Shadertoy "Clouds" by iq — iq's classic cloud shader
- Shadertoy "Tileable Perlin-Worley 3D" — implementation based on Horizon

**Key Papers:**
- Schneider & Vos, "Real-time Volumetric Cloudscapes of Horizon Zero Dawn" (GDC 2015)
- Hillaire, "Physically Based Sky, Atmosphere and Cloud Rendering in Frostbite" (SIGGRAPH 2016)

### 9.3 Three.js Visualizer Path

**For current web visualizer:**
1. Implement Perlin-Worley noise in JavaScript
2. Create 3D noise texture as Float32Array
3. Pass to WebGL shader as 3D texture (or multiple 2D slices)
4. Raymarch in fragment shader
5. Apply height gradient + lighting

**Performance path:**
- Start with 64x64x64 texture, 64 ray steps
- Increase if performance allows
- Consider half-resolution rendering + upscale

---

## 10. Gaps and Unknowns

### 10.1 Known Gaps

1. **Precise Perlin-Worley combination formula** — the exact weights and combination method vary by implementation. The research points to general approach but specific formula would require reverse-engineering from Shadertoy demos.

2. **Real-time performance on target hardware** — depends on whether WebGL or Unity, GPU capability, resolution. Recommend starting with conservative values.

3. **No Man's Sky's specific algorithm** — the 2024 rewrite details aren't publicly documented.

### 10.2 Recommendations for Further Research

1. Study Fewes/CloudNoiseGen source code for exact noise combination formula
2. Examine Shadertoy "Tileable Perlin-Worley 3D" shader code (if accessible)
3. Look at Unity HDRP cloud shader source (available in HDRP package)

---

## Sources and Credibility

| Source | Credibility | Notes |
|--------|-------------|-------|
| Horizon Zero Dawn GDC talk | HIGH | Primary industry reference |
| Frostbite SIGGRAPH 2016 | HIGH | EA production-tested |
| Maxime Heckel blog | MEDIUM-HIGH | Recent, practical, code examples |
| jpg's blog (Stingray) | MEDIUM-HIGH | Implementation details, links to sources |
| Roman Taylor blog | MEDIUM | Project details, good overview |
| Unity HDRP docs | HIGH | Official, authoritative |
| Shadertoy community | MEDIUM | Various quality, useful code |

---

## Summary: From Current Approach to Realistic Clouds

**Current (problematic):**
```
FBM + Perlin noise → sphere positions in grid
```

**Target:**
```
Perlin-Worley 3D noise (precomputed texture)
    + volumetric raymarching (not mesh generation)
    + height gradient density
    + Beer-Lambert + Henyey-Greenstein lighting
    + wind animation (curl noise)
```

**Key insight:** The sphere-grid approach fundamentally cannot produce realistic clouds because it samples at discrete points in a regular grid. Real volumetric cloud rendering samples continuously through a density field using raymarching.

**Immediate next steps:**
1. Replace sphere generation with 3D noise texture sampling
2. Implement raymarching through cloud volume
3. Add height-based density gradient
4. Add lighting with Beer-Lambert model
5. Add wind animation