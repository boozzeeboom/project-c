# CLOUD_system — Visual Design Document

**Версия:** 0.1 (Draft) | **Дата:** 3 мая 2026 | **Статус:** 🔴 Planning
**Автор:** Art Director

---

## 1. Overview

Визуальная система облаков должна создавать **"вкусные"**, атмосферные облака в стиле **Sci-Fi + Ghibli**. Текущая реализация слишком примитивная — сферы и цилиндры не создают нужного визуального впечатления.

---

## 2. Cloud Type Taxonomy

### 2.1 Tier 1: Decorative Atmospheric Clouds
**Purpose:** Background presence, world depth, atmospheric density

| Type | Height | Mesh | Density | Role |
|------|--------|------|---------|------|
| **High Cirrus** | 6000-8000m | Billboard impostors | 0.15-0.25 | Wispy, distant, background |
| **Layered Stratus** | 3000-5000m | Broad deformed planes | 0.3-0.5 | Flat sheets, atmospheric |
| **Background Cumulus** | 1500-3000m | Soft spheres | 0.4-0.6 | Distant puffy shapes |

**Visual Characteristics:**
- Low vertex count (LOD0: ~500 tris, LOD2: ~50 tris)
- Subtle rim glow (rimPower: 3.0-4.0)
- Slow morphing (morphSpeed: 0.1-0.3)
- Reduced noise scale for soft edges

### 2.2 Tier 2: Interactive Flight Clouds
**Purpose:** Player flies through, feels physical presence

| Type | Height | Mesh | Density | Role |
|------|--------|------|---------|------|
| **Fluffy Cumulus** | 2000-4000m | High-detail spheres/clusters | 0.6-0.8 | Navigate around |
| **Density Banks** | 1500-3000m | Large deformed meshes | 0.7-0.9 | Fly THROUGH — fog effect |
| **Anvil Heads** | 4000-6000m | Flat-topped shapes | 0.8-1.0 | Signals storms |

**Visual Characteristics:**
- Strong rim glow (rimPower: 1.5-2.0, rimColor alpha: 0.7-0.9)
- Medium morphing (morphSpeed: 0.5-1.0)
- **On player entry:**
  - Local fog density increases
  - Screen-space rain droplets
  - Engine bloom response

### 2.3 Tier 3: Storm Systems
**Purpose:** Dangerous weather, visibility impact, lightning spectacle

| Type | Height | Mesh | Density | Role |
|------|--------|------|---------|------|
| **Cumulonimbus** | 1200-9000m | Volumetric + VFX | 0.9-1.0 | Major storm, purple lightning |
| **The Veil** | <1200m | URP Fog | 1.0 | Toxic layer, fatal |

**Visual Characteristics:**
- Base color: `#2d1b4e` (dark violet)
- Rim color: `#9C27B0` (bright purple) high intensity
- Lightning: Random flashes every 2-5 seconds
- Aggressive vertex displacement (10-15 units)

---

## 3. Color Palette

### 3.1 Cloud Base Colors by Time of Day

| Time Phase | Cloud Base Color | Hex | Description |
|------------|------------------|-----|-------------|
| **Dawn (5-7am)** | Peach Mist | `#FFE4B5` | Warm morning glow |
| **Morning (7-11am)** | Pure White | `#FFFFFF` | Crisp, clean |
| **Noon (11am-1pm)** | White | `#FFFFFF` | Bright, minimal rim |
| **Afternoon (1-5pm)** | Warm White | `#FFF8DC` | Warm shift begins |
| **Dusk (5-7pm)** | Pink Rose | `#FFB6C1` | Peak "tasty" moment |
| **Twilight (7-8pm)** | Plum | `#DDA0DD` | Purple transition |
| **Night (8pm-5am)** | Slate Blue | `#4F628E` | Moonlit, cool |

### 3.2 Rim Glow Colors (Ghibli Signature)

| Time Phase | Rim Color | Hex | Intensity |
|------------|-----------|-----|----------|
| **Dawn** | Gold | `#FFD700` | 0.8 |
| **Day** | Lemon | `#FFFACD` | 0.6 |
| **Dusk** | Hot Pink | `#FF69B4` | 0.9 |
| **Night** | Anti-grav Blue | `#4FC3F7` | 0.5 |

### 3.3 Storm Colors

| Element | Color | Hex |
|---------|-------|-----|
| Cumulonimbus Base | Dark Violet | `#2d1b4e` |
| Cumulonimbus Rim | Bright Purple | `#9C27B0` |
| Lightning | Electric Purple | `#b366ff` |
| The Veil | Deep Purple | `#2d1b4e` |
| The Veil Lightning | Violet | `#9C27B0` |

---

## 4. Shader Parameters

### 4.1 Current CloudGhibli.shader Properties

```hlsl
// EXISTING (keep):
_BaseColor         // Cloud base color
_RimColor          // Ghibli rim glow color
_RimPower          // Fresnel power (1.5-4.0)
_NoiseTex          // FBM noise texture 1
_NoiseTex2         // FBM noise texture 2
_NoiseScale        // UV scale for noise
_NoiseScrollSpeed  // Animation speed
_AlphaBase         // Base transparency (0.3-0.9)
_Softness          // Edge softness (0.1-0.5)
_VertexDisplacement// Morph amplitude (1.0-12.0)
```

### 4.2 Required New Properties

```hlsl
// LIGHTING (critical - current shader is Unlit):
_LightInfluence    // Range(0,1) = 0.3  — lighting response
_MainLightColor    // Auto - sun color
_MainLightDir     // Auto - sun direction

// DAY/NIGHT TRANSITION:
_TintColor1        // Gradient color 1 (dawn/day)
_TintColor2        // Gradient color 2 (dusk/night)
_TintBlend         // Range(0,1) = 0.0  — driven by time of day

// PLAYER INTERACTION:
_PlayerDistortion  // Float = 0.0  — set by trigger volume
_DensityBoost      // Float = 1.0  — increased when player nearby

// STORM:
_LightningFlash    // Float = 0.0  — runtime, for lightning
_EdgeDarkening     // Float = 0.2  — darker edges for depth
```

### 4.3 Parameter Values by Cloud Type

| Parameter | Decorative | Interactive | Storm |
|-----------|------------|-------------|-------|
| `_RimPower` | 3.5 | 1.8 | 1.2 |
| `_RimColor.a` | 0.4 | 0.8 | 1.0 |
| `_AlphaBase` | 0.3 | 0.6 | 0.9 |
| `_Softness` | 0.5 | 0.3 | 0.1 |
| `_VertexDisplacement` | 1.0 | 3.0 | 12.0 |
| `_NoiseScale` | 0.5 | 1.0 | 1.5 |
| `_LightInfluence` | 0.1 | 0.4 | 0.7 |

---

## 5. Post-Processing Requirements

### 5.1 URP Volume Profile (Priority)

| Effect | Settings | Purpose | Cloud Impact |
|--------|----------|---------|--------------|
| **Bloom** | Threshold: 0.8, Intensity: 0.7 | Rim glow amplification | Dramatic cloud edges |
| **Color Grading** | Temperature: +10 day, -10 night | Global tint | Clouds inherit tones |
| **Fog** | Exponential, Color: sky color | Depth integration | Cloud horizon blend |
| **Vignette** | Intensity: 0.2, Smoothness: 0.4 | Focus view | Emphasize flight |

### 5.2 Cloud-Specific Volume Overrides

**Inside Density Bank / Storm:**
```
Bloom.Intensity: 1.2 (increased rim glow)
ColorGrading.Saturation: -0.3 (desaturated)
Vignette.Intensity: 0.5 (focused)
```

**The Veil (Global Volume, Priority 100):**
```
Fog.Color: #2d1b4e
Fog.Density: 0.8
ColorGrading.Saturation: -0.7
Vignette.Intensity: 0.7
ChromaticAberration: 0.2
```

---

## 6. Visual "Wow" Factors

### 6.1 The "Tasty" Cloud Formula

Clouds feel impressive when they combine:

1. **Rim glow reacting to sun position** — As player flies, rim color shifts (Fresnel + directional light)
2. **Organic noise** — 3+ octaves of FBM (currently 2), with occasional "surprise" shapes
3. **Soft edges inviting entry** — Player WANTS to fly into clouds
4. **Light shafts through gaps** — Volumetric light piercing cloud openings

### 6.2 Interactive Flight Moments

| Moment | Visual Effect | Implementation |
|--------|---------------|----------------|
| **Entering cloud** | Screen fog fades in, bloom increases | Trigger volume → post-process lerp |
| **Flying through density bank** | Rain particles on cockpit | Particle system on ship |
| **Near Cumulonimbus** | Lightning flash (0.1s whiteout), thunder | Lightning particles + camera flash |
| **Dusk flight** | Clouds glow pink-orange, rim intensifies | Day/night drives `_TintBlend` |
| **Breaking through cloud** | Dramatic landscape reveal | Cloud layer cutoff + fog reduction |

### 6.3 Volumetric Light Integration

**Light Shafts effect:**
- Directional Light (Sun) casts rays through cloud gaps
- Use URP Decal Projector with gradient texture
- Angle follows sun position
- Intensity peaks at dawn/dusk (0.8), lowest at noon (0.2)

---

## 7. Day/Night Appearance Matrix

| Time | Base Color | Rim Color | Fog | Bloom | Special |
|------|------------|-----------|-----|-------|---------|
| Dawn | `#FFE4B5` | `#FFD700` | `#FFB6C1` | 0.8 | Golden glow |
| Day | `#FFFFFF` | `#FFFACD` | `#87CEEB` | 0.5 | Crisp |
| Dusk | `#FFB6C1` | `#FF69B4` | `#DDA0DD` | 0.9 | Peak tasty |
| Night | `#4F628E` | `#4FC3F7` | `#1a1a2e` | 0.6 | Moonlit |

---

## 8. Implementation Priority

| Priority | Feature | Impact | Status |
|----------|---------|--------|--------|
| **P0** | Add lighting parameters to CloudGhibli.shader | High | 🔴 Not done |
| **P0** | URP Volume Profile with Bloom + Fog | High | 🔴 Not done |
| **P0** | Day/night tint blending | High | 🔴 Not done |
| **P1** | Create 3 cloud type configurations | Medium | 🔴 Not done |
| **P1** | Trigger volumes for cloud entry effects | Medium | 🔴 Not done |
| **P2** | Volumetric light shafts | Low | 🔴 Not done |
| **P2** | Lightning VFX for storms | Low | 🔴 Not done |
| **P3** | Cloud silhouette system (sunset) | Low | 🔴 Not done |

---

## 9. Shader Improvements Required

Current `CloudGhibli.shader` is **Unlit** — this is a problem.

### 9.1 Problems with Unlit
- Clouds don't receive shadows from mountains
- No reaction to directional light color/intensity
- Flat appearance, no depth

### 9.2 Recommended Changes

```hlsl
// Add to frag():
#ifdef _MAIN_LIGHT_SHADOWS
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.worldPos));
    half3 lightColor = mainLight.color * mainLight.shadowAttenuation;
    cloudColor *= lightColor;
#endif

// Add day/night tint blending
half3 tintedColor = lerp(_TintColor1.rgb, _TintColor2.rgb, _TintBlend);
finalColor *= tintedColor;

// Add 3rd noise octave for detail
float noise3 = Noise3D(uvw * 4.0); // Higher frequency detail
combinedNoise = noise1 * 0.5 + noise2 * 0.3 + noise3 * 0.2;
```

---

**Status:** 🔴 Draft — pending shader specialist implementation