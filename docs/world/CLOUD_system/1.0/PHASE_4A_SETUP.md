# Phase 4a: HorizonVeil — Scene Setup Instructions

## Overview

HorizonVeil integrates into existing CloudManager architecture. Main veil is **client-side** (loads immediately with bootstrap), additional veils are **server-controlled modules**.

---

## Step 1: Add HorizonVeilRenderer to CloudManager

### In BootstrapScene.unity:

1. Find `CloudManager` GameObject in BootstrapScene
2. In Inspector, add new component: `HorizonVeilRenderer`
3. Configure settings:

```
CloudManager Inspector:
├── HorizonVeil: [drag HorizonVeilRenderer component]
├── BaseVeilHeight: 1200
├── VeilThickness: 400
└── MaxAdditionalVeils: 10
```

---

## Step 2: Configure HorizonVeilRenderer Component

On the HorizonVeilRenderer component:

### Render Settings
| Field | Value | Notes |
|-------|-------|-------|
| Render Width | 512 | Half resolution for GPU budget |
| Render Height | 288 | Half resolution for GPU budget |
| Veil Raymarch Material | [VeilRaymarch.mat] | Create from shader |

### Veil Position
| Field | Value | Notes |
|-------|-------|-------|
| Base Veil Height | 1200 | Y=1200m base |
| Veil Layer Height | 400 | Total 800-1200m range |
| Global Altitude Offset | 0 | Set dynamically per city |

### Raymarch Settings
| Field | Value | Notes |
|-------|-------|-------|
| Raymarch Steps | 12 | Balance quality/GPU |
| Raymarch Max Distance | 8000 | 8km render distance |

### Noise Settings
| Field | Value | Notes |
|-------|-------|-------|
| Noise Scale | 0.002 | FBM scale |
| Noise Octaves | 3 | For canyon/valley effect |
| Noise Speed | 0.01 | Wind animation |

### Color (Purple-Green Dark)
| Field | Value | Notes |
|-------|-------|-------|
| Veil Base Color | (0.08, 0.06, 0.15, 1) | Dark purple-green |
| Veil Lightning Color | (0.7, 0.4, 1, 1) | Purple lightning |

---

## Step 3: Create VeilRaymarch Material

1. In `Assets/_Project/Materials/Clouds/`
2. Right-click → Create → Material
3. Name: `VeilRaymarch.mat`
4. Shader: `Project C/Clouds/VeilRaymarch`
5. Assign to HorizonVeilRenderer component

---

## Step 4: AdditionalVeilModule Setup (Optional)

For server-controlled event veils:

### In CloudManager GameObject:

1. Add component: `AdditionalVeilManager`
2. Configure:
   - Max Modules: 10
   - Module Prefab: [create prefab with AdditionalVeilModule]

### Create AdditionalVeilModule Prefab:

1. Create empty GameObject
2. Add component: `AdditionalVeilModule`
3. Configure default values:
   - Module Radius: 5000
   - Altitude Offset: 0
   - Module Color: (0.1, 0.08, 0.2, 0.9)
   - Density Multiplier: 1.5
   - Lightning Chance: 0.1
4. Assign material (VeilMaterial.mat or similar)
5. Drag to `Assets/_Project/Prefabs/Clouds/`
6. Assign prefab to AdditionalVeilManager

---

## Step 5: Verify Hierarchy

```
BootstrapScene.unity (Never Unloaded)
├── NetworkManager
├── WindManager
├── ServerWeatherController
├── ServerStormManager
├── CloudManager
│   ├── NearCloudRenderer (Upper)
│   ├── NearCloudRenderer (Middle)
│   ├── NearCloudRenderer (Lower)
│   ├── DistantCloudManager
│   ├── HorizonVeilRenderer     ← NEW
│   └── AdditionalVeilManager   ← NEW (optional)
└── PlayerSpawner
```

---

## Step 6: Global Altitude Integration (Per City)

When player enters different altitude corridor:

```csharp
// In your city/altitude system:
CloudManager.Instance?.SetGlobalAltitudeOffset(cityAltitudeOffset);

// Example:
// City at 2000m base altitude
// Veil moves to: 1200 + (2000 - 1200) = 2000m (but -200 from global altitude)
// So: SetGlobalAltitudeOffset(2000 - 1200 - 200) = 600
```

---

## Step 7: Server-Controlled Additional Veils

For event "city поглощено завесой":

```csharp
// Server-side:
AdditionalVeilModule module = CloudManager.Instance.AdditionalVeilMgr
    .SpawnModule(cityWorldPosition, radius: 5000, altOffset: 0);

// Sync to all clients via NetworkBehaviour RPC
```

---

## Verification Checklist

### In Unity Editor:
- [ ] HorizonVeilRenderer component on CloudManager
- [ ] VeilRaymarch.mat assigned
- [ ] No console errors on play
- [ ] In Game view, should see purple-dark volumetric curtain at horizon

### Performance Test:
- [ ] Open Profiler → GPU tab
- [ ] Look for "VeilRaymarchPass"
- [ ] Target: <1.5ms

### Multi-Client Test (future):
- [ ] 2+ clients see same veil positions
- [ ] Wind animation synchronized

---

## Troubleshooting

### "Veil not visible at horizon"
- Check BaseVeilHeight matches your world scale (1200 = 1200 units, not 12)
- Check camera is looking toward veil layer (Y=800-1200)
- Verify VeilRaymarch shader is assigned

### "GPU too high"
- Reduce Raymarch Steps from 12 to 8
- Reduce Render Width/Height to 256/144

### "No canyon/valley effect"
- Increase Noise Octaves to 4
- Increase Noise Scale to 0.003

### "Wind not affecting veil"
- Verify WindManager exists in scene
- Check WindManager.Instance is not null

---

## Files Reference

| File | Location | Purpose |
|------|----------|---------|
| HorizonVeilRenderer.cs | Assets/_Project/Scripts/World/Clouds/ | Main volumetric renderer |
| VeilRaymarch.shader | Assets/_Project/Shaders/ | Raymarch shader |
| AdditionalVeilModule.cs | Assets/_Project/Scripts/World/Clouds/ | Server-controlled veils |
| CloudManager.cs | Assets/_Project/Scripts/Core/ | Integration (modified) |
| VeilRaymarch.mat | Assets/_Project/Materials/Clouds/ | Shader material |
