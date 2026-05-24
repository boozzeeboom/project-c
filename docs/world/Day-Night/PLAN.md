# Day-Night Cycle — Implementation Plan

## 1. Disable Old Integration

### 1.1 CloudSystem.prefab
```yaml
enableDayNightCycle: false  # Already false but set explicitly
```
Edit prefab or CloudSystem.cs line 41: ensure `enableDayNightCycle = false` is the default and cannot be changed via Inspector without warning.

### 1.2 VeilRaymarchMesh.shader — remove hardcoded light
Remove or comment hardcoded `half3 lightDir = normalize(half3(-0.5, 0.5, -0.3));` at shader line 295. Replace with uniform `_LightDir`.

---

## 2. Create ScriptableObject Stubs

### 2.1 `TimeOfDayPhase.cs`
Path: `Assets/_Project/Scripts/Core/DayNight/TimeOfDayPhase.cs`

```csharp
using UnityEngine;
namespace ProjectC.Core
{
    [CreateAssetMenu(fileName = "NewPhase", menuName = "ProjectC/DayNight/TimeOfDayPhase")]
    public class TimeOfDayPhase : ScriptableObject
    {
        [Header("Identity")]
        public string phaseName = "New Phase";
        public float startHour = 0f;
        public float endHour = 24f;

        [Header("Sun Light")]
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        public float sunTemperature = 5500f;
        public bool castShadows = true;

        [Header("Ambient Light")]
        public Color ambientSkyColor = new Color(0.2f, 0.2f, 0.3f);
        public Color ambientEquatorColor = new Color(0.3f, 0.3f, 0.4f);
        public Color ambientGroundColor = new Color(0.1f, 0.1f, 0.15f);
        public float ambientIntensity = 0.5f;

        [Header("Skybox")]
        public Gradient skyHorizonGradient;
        public float skyboxExposure = 1f;
        public Color skyboxTint = Color.white;

        [Header("Fog")]
        public Color fogColor = Color.gray;
        public float fogDensity = 0.0003f;

        [Header("Variability (randomization ranges)")]
        public Vector2 hueShiftRange = new Vector2(-0.05f, 0.05f);
        public Vector2 saturationRange = new Vector2(0.8f, 1.2f);
        public Vector2 intensityRange = new Vector2(0.85f, 1.15f);

        [Header("Transition")]
        public AnimationCurve transitionCurve = AnimationCurve.Linear(0,0,1,1);
    }
}
```

### 2.2 `DayNightProfile.cs`
Path: `Assets/_Project/Scripts/Core/DayNight/DayNightProfile.cs`

```csharp
using UnityEngine;
namespace ProjectC.Core
{
    [CreateAssetMenu(fileName = "NewDayNightProfile", menuName = "ProjectC/DayNight/DayNightProfile")]
    public class DayNightProfile : ScriptableObject
    {
        public TimeOfDayPhase[] phases = new TimeOfDayPhase[5];
    }
}
```

### 2.3 `TemperatureFilterConfig.cs`
Path: `Assets/_Project/Scripts/Core/DayNight/TemperatureFilterConfig.cs`

```csharp
using UnityEngine;
namespace ProjectC.Core
{
    [CreateAssetMenu(fileName = "NewTempFilterConfig", menuName = "ProjectC/DayNight/TemperatureFilterConfig")]
    public class TemperatureFilterConfig : ScriptableObject
    {
        [Header("Cold (<= coldThreshold)")]
        public float coldThreshold = 0f;
        public Color coldOverlayColor = new Color(0.3f, 0.4f, 0.6f);
        public float coldSaturationBoost = 0.1f;
        public float coldValueOffset = -0.1f;

        [Header("Hot (>= hotThreshold)")]
        public float hotThreshold = 25f;
        public Color hotOverlayColor = new Color(0.6f, 0.3f, 0.1f);
        public float hotSaturationBoost = 0.1f;
        public float hotValueOffset = 0.05f;

        [Header("Blending")]
        public AnimationCurve blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
    }
}
```

---

## 3. Extend ServerWeatherController

**File:** `Assets/_Project/Scripts/Core/ServerWeatherController.cs`

Add after existing `[Header("Variation")]` section:

```csharp
[Header("Time of Day")]
[SerializeField] private float _timeOfDay = 12f;
[SerializeField] private float _dayCycleRealHours = 1f;  // 1 real hour = 24 game hours
[SerializeField] private bool _enableTimeAutoAdvance = true;
[SerializeField] private float _timeBroadcastInterval = 5f;
private float _timeTimer = 0f;

[Header("Temperature")]
[SerializeField] private float _temperature = 20f;
[SerializeField] private float _tempBroadcastInterval = 10f;
private float _tempTimer = 0f;

// Properties
public float TimeOfDay => _timeOfDay;
public float Temperature => _temperature;

// new Update() logic additions
// new ClientRpc: BroadcastTimeOfDayClientRpc(float time)
// new ClientRpc: BroadcastTemperatureClientRpc(float temp)
// new ServerRpc: SetTimeOfDayServerRpc(float time)
// new ServerRpc: SetTemperatureServerRpc(float temp)
```

---

## 4. Create DayNightController.cs

**File:** `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs`

Key responsibilities:
1. Subscribe to `ServerWeatherController` events (or poll)
2. Compute current phase + blend factor from time
3. Apply randomized color/intensity based on day-seed
4. Control `DirectionalLight` (sun)
5. Control `RenderSettings.ambient`
6. Control `RenderSettings.fog`
7. Blend URP Volume profiles
8. Feed `_LightDir` + `_DayFactor` to veil shader

```csharp
using UnityEngine;
using UnityEngine.Rendering;
namespace ProjectC.Core
{
    public class DayNightController : MonoBehaviour
    {
        [Header("Profile")]
        public DayNightProfile profile;

        [Header("Sun")]
        public Light sunLight;

        [Header("Ambient")]
        public bool controlAmbient = true;

        [Header("Fog")]
        public bool controlFog = true;

        [Header("Volume")]
        public Volume globalVolume;
        public VolumeProfile dayVolumeProfile;
        public VolumeProfile nightVolumeProfile;
        private VolumeProfile _activeVolumeProfile;

        [Header("Temperature Filter")]
        public TemperatureFilterConfig temperatureConfig;
        public float currentTemperature = 20f;

        [Header("Veil Shader Integration")]
        public bool updateVeilShader = true;
        public VeilRaymarchMeshController veilController;

        private float _serverTimeOfDay = 12f;
        private int _currentPhaseIndex = -1;
        private float _phaseBlend = 0f;
        private float _daySeed = 0f;

        void Start()
        {
            if (ServerWeatherController.Instance != null)
            {
                // Subscribe to events or use polling
            }
            ApplyAll();
        }

        void Update()
        {
            // Poll or event-driven update
            UpdateLighting();
        }

        public void SetTimeOfDay(float time)
        {
            _serverTimeOfDay = time;
            UpdateLighting();
        }

        public void SetTemperature(float temp)
        {
            currentTemperature = temp;
        }

        private void UpdateLighting() { /* phase detection + interpolation */ }
        private void ApplySun() { /* sun color/intensity/rotation */ }
        private void ApplyAmbient() { /* RenderSettings.ambient */ }
        private void ApplyFog() { /* RenderSettings.fog */ }
        private void ApplyVolumeBlend() { /* URP volume profile blend */ }
        private void ApplyTemperatureFilter() { /* color post-filter */ }
        private void ApplyVeilShader() { /* _LightDir + _DayFactor */ }

        private Color ApplyVariability(Color baseColor, int phaseIdx) { /* seeded randomness */ }
    }
}
```

---

## 5. Create TemperatureFilter.cs

**File:** `Assets/_Project/Scripts/Core/DayNight/TemperatureFilter.cs`

```csharp
using UnityEngine;
namespace ProjectC.Core
{
    public class TemperatureFilter : MonoBehaviour
    {
        public TemperatureFilterConfig config;
        private Color _lastFilterColor = Color.clear;

        public Color GetTemperatureOverlay(float temperature)
        {
            if (config == null) return Color.clear;
            // evaluate blendCurve
            // return cold/hot overlay color
        }
    }
}
```

---

## 6. Create URP Volume Profiles

**Assets/_Project/Volumes/DayNight/**
- `DayVolumeProfile.asset`
- `NightVolumeProfile.asset`

VolumeComponents to include:
| Component | Day Value | Night Value |
|----------|----------|-------------|
| Bloom | threshold 0.8, intensity 0.3 | threshold 0.6, intensity 0.8 |
| Vignette | intensity 0.2 | intensity 0.4 |
| ColorAdjustments | exposure 0, saturation 0 | exposure -0.3, saturation -10 |

---

## 7. Skybox Setup

Create 2 skybox materials in `Assets/_Project/Materials/Skybox/`:
- `Skybox_Day.mat` — Procedural, warm tint, exposure 1.0
- `Skybox_Night.mat` — Procedural, cool tint, exposure 0.3

Create blending shader `Skybox_Blend.shader` that lerps between two skybox materials based on blend factor, OR use URP's `Skybox` component with `CustomSkybox` shader.

---

## 8. Update VeilRaymarchMeshController

**File:** `Assets/_Project/Scripts/World/Clouds/VeilRaymarchMeshController.cs`

Add method:
```csharp
public void SetDayNight(float dayFactor, Vector3 lightDir)
{
    DayFactor = dayFactor;
    // Also update light direction uniform
}
```

Call from `DayNightController` when time changes.

---

## 9. Execution Order

1. Create `Assets/_Project/Scripts/Core/DayNight/` folder
2. Create 3 ScriptableObject classes (TimeOfDayPhase, DayNightProfile, TemperatureFilterConfig)
3. Create 2 component classes (DayNightController, TemperatureFilter)
4. Create 2 VolumeProfile assets in `Assets/_Project/Volumes/DayNight/`
5. Create 2 Skybox materials
6. Extend `ServerWeatherController`
7. Update `VeilRaymarchMeshController` to expose `SetDayNight`
8. Place `DayNightController` on scene object (or Sun object)
9. Disable old day-night in `CloudSystem.prefab`
10. Test

---

## 10. Files to Create / Modify

| File | Action |
|------|--------|
| `Assets/_Project/Scripts/Core/DayNight/` | Create folder |
| `Assets/_Project/Scripts/Core/DayNight/TimeOfDayPhase.cs` | Create |
| `Assets/_Project/Scripts/Core/DayNight/DayNightProfile.cs` | Create |
| `Assets/_Project/Scripts/Core/DayNight/TemperatureFilterConfig.cs` | Create |
| `Assets/_Project/Scripts/Core/DayNight/TemperatureFilter.cs` | Create |
| `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs` | Create |
| `Assets/_Project/Scripts/Core/ServerWeatherController.cs` | Modify (extend) |
| `Assets/_Project/Scripts/World/Clouds/VeilRaymarchMeshController.cs` | Modify (add SetDayNight) |
| `Assets/_Project/Scripts/Core/CloudSystem.cs` | Modify (disable day-night section) |
| `Assets/_Project/Prefabs/CloudSystem.prefab` | Modify (set enableDayNightCycle=false) |
| `Assets/_Project/Volumes/DayNight/DayVolumeProfile.asset` | Create |
| `Assets/_Project/Volumes/DayNight/NightVolumeProfile.asset` | Create |
| `Assets/_Project/Materials/Skybox/Skybox_Day.mat` | Create |
| `Assets/_Project/Materials/Skybox/Skybox_Night.mat` | Create |
| `Assets/_Project/Materials/Skybox/Skybox_Blend.shader` | Create (optional) |
| `docs/world/Day-Night/SPEC.md` | Already created |
| `docs/world/Day-Night/PLAN.md` | This file |
| `docs/world/Day-Night/PROFILE_Morning.md` | Create per-phase detail docs |
| `docs/world/Day-Night/PROFILE_Midday.md` | Create |
| `docs/world/Day-Night/PROFILE_Evening.md` | Create |
| `docs/world/Day-Night/PROFILE_Twilight.md` | Create |
| `docs/world/Day-Night/PROFILE_Night.md` | Create |

---

## 11. Checklist Before Implementation

- [ ] Confirm server time-of-day control approach (event vs polling)
- [ ] Confirm volume blend approach (weight vs profile swap)
- [ ] Confirm skybox blend approach (shader vs material swap)
- [ ] Create phase profile documents (5 files)
- [ ] Verify Unity version and URP version compatibility
- [ ] Confirm "Moon" requirement (light only? mesh?)
- [ ] Confirm star field requirement