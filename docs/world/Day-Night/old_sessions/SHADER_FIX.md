# Day-Night Cycle — Shader Integration Notes

## VeilRaymarchMesh.shader — LightDir Fix

**File:** `Assets/_Project/Shaders/VeilRaymarchMesh.shader`

### Issue

Line 295 contains a **compile-time constant** that **overrides** the `_LightDir` uniform:

```hlsl
// BAD — hardcoded, ignores uniform
half3 lightDir = normalize(half3(-0.5, 0.5, -0.3));
```

The uniform is declared (line 93: `half4 _LightDir;`) but the shader body ignores it.

### Fix

Replace line 295 with:
```hlsl
half3 lightDir = normalize(_LightDir.xyz);
```

### Controller Update Required

`VeilRaymarchMeshController.cs` does NOT currently set `_LightDir`. Add:

```csharp
// In Start() or SetupMaterial():
if (_instanceMaterial != null)
    _instanceMaterial.SetVector(Property_LightDir, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0));
```

Where `sunDir` comes from `DayNightController.SetLightDir(Vector3 sunDirection)`.

---

## Other Shaders

No other shaders have day-night related hardcoding. The cloud shaders (`CloudGhibli`, `DistantCloudUnlit`) have no time-of-day parameters.