# CLOUD_system 3.0 — Implementation Log

**Date:** 2026-08-02
**Plan:** `CLOUD_OCEAN_MEDIUM_DETAILED_STEPS.md`
**Status:** 🟡 Phase 1 in progress

---

## Phase 1 Progress

### 1.1 ✅ HLSL-порт CloudMath.cs → CloudNoise.hlsl + CloudCommon.hlsl
**Files:**
- `Assets/_Project/Shaders/Clouds/CloudNoise.hlsl` — Perlin3D, Fbm, Worley3D, InvertedWorley с uint-хэшем и периодическим (seamless) режимом
- `Assets/_Project/Shaders/Clouds/CloudCommon.hlsl` — Remap, HeightProfile, HG, MultiScatterApprox, SilverLining, RaySlabIntersection, CameraRelativePosition, GhibliRamp

**Done:** Hash3 на uint multiply-shift, все функции из v7.0 CloudMath. Периодический шум через mod(cellIndex, texSize). float32 точность.

### 1.2 ✅ Бейк 3D Worley-текстуры
**Files:**
- `Assets/_Project/Shaders/Clouds/BakeCloudNoise.compute` — compute shader 128³ RGBA8, каналы R=PerlinFBM, G=WorleyLow(freq=4), B=WorleyHigh(freq=16), A=InvertedWorley
- `Assets/_Project/Scripts/World/Clouds/CloudNoiseBaker.cs` — Editor MenuItem "ProjectC/Clouds/Bake 3D Noise Texture", AsyncGPUReadback → Texture3D asset

**Note:** Бейк запускается вручную через меню. Сравнение C# vs HLSL — placeholder, требует доработки.

### 1.3 ✅ VolumetricCloudsRenderFeature + VolumetricClouds.shader (скелет)
**Files:**
- `Assets/_Project/Scripts/Rendering/VolumetricCloudsRenderFeature.cs` — URP RendererFeature (RenderGraph API, паттерн EdgeDetection), AfterOpaques, fullscreen triangle
- `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader` — Shader "Hidden/ProjectC/VolumetricClouds", SV_VertexID, camera ray reconstruction, density-only B&W (Phase 1.3) + light marching + Ghibli ramps (Phase 1.5)

**Note:** RendererFeature нужно добавить в `ProjectC_URP_Renderer.asset` через Inspector вручную.

### 1.4 ✅ Height profile + coverage + wind (включено в шейдер)
- HeightProfileSimple: smoothstep градиент 0 у CloudBottomY, пик в середине, 0 у CloudTopY
- Wind: _WindOffset из WindManager.Instance, null-guard
- Camera-relative позиции для 80k×80k сцен (CameraRelativePosition)
- Coverage: константа 1 (равномерное покрытие) — как указано в плане

### 1.5 ✅ Light marching + HG + multi-scatter + Ghibli ramps
- 6 light steps к солнцу, Beer-Lambert поглощение
- HG фаза (g=0.7 forward scattering)
- Multi-scatter: pow(transmittance, 0.5)
- Ghibli ramps: день/закат (rampBlend от _SunDirection.y * 2.0)
- Ambient 0.15 + silver lining (pow, 8.0) × 0.3
- Sun direction из RenderSettings.sun или fallback (0.3, 0.7, -0.6)

GDD-14 рампы:
- День: #FFFFFF (top) → #D4E6F1 (mid) → #A9CCE3 (bot)
- Закат: #FFFFFF (top) → #FFB6C1 (mid) → #CD5C5C (bot)

### 1.6 ⚠️ Half-res + blue-noise + temporal (отложено)
Параметры в RenderFeature заведены (HalfResRender, TemporalReprojection, BlueNoiseTexture), но текущая реализация Pass — single-pass fullscreen. Полноценный half-res + temporal reprojection требует:
- Создание промежуточного RT половинного разрешения
- Кэш prevViewProj в C#
- Репроекцию в шейдере
- Composite pass → colorTarget

Сделано частично: blue-noise дизеринг в шейдере через `#if _BLUE_NOISE_ON`.

### 1.7 ✅ CloudPerfMonitor.cs
**File:** `Assets/_Project/Scripts/World/Clouds/CloudPerfMonitor.cs`
- CustomSampler, FrameTimingManager, rolling average
- Editor OnGUI overlay (зелёный ≤3ms, жёлтый ≤5ms, красный >5ms)
- `PERF_RESULTS.md` — заполняется после запуска в Play Mode

---

## Что нужно сделать вручную

1. **Добавить `VolumetricCloudsRenderFeature` в `ProjectC_URP_Renderer.asset`** через Inspector
2. **Забейкать `CloudNoise3D.asset`** — меню ProjectC → Clouds → Bake 3D Noise Texture
3. **Назначить `CloudNoise3D.asset`** в свойство `CloudNoise3D` Renderer Feature
4. **Запустить Play Mode** для проверки визуала и перф-замеров

---

## Файлы (создано)

```
Assets/_Project/
├── Shaders/Clouds/
│   ├── CloudNoise.hlsl            ✅ 1.1
│   ├── CloudCommon.hlsl           ✅ 1.1
│   ├── VolumetricClouds.shader    ✅ 1.3 + 1.4 + 1.5
│   └── BakeCloudNoise.compute     ✅ 1.2
├── Scripts/
│   ├── Rendering/
│   │   └── VolumetricCloudsRenderFeature.cs  ✅ 1.3
│   └── World/Clouds/
│       ├── CloudNoiseBaker.cs     ✅ 1.2
│       └── CloudPerfMonitor.cs    ✅ 1.7
└── Data/Clouds/
    └── CloudNoise3D.asset         ⏳ требуется бейк (меню)
```
