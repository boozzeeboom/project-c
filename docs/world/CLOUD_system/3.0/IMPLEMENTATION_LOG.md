# CLOUD_system 3.0 — Implementation Log

**Date:** 2026-08-02 – 2026-08-04
**Plan:** `CLOUD_OCEAN_MEDIUM_DETAILED_STEPS.md`
**Status:** 🟢 Phase 1 + Phase 2 (2.1–2.3) + Multi-Layer (3.0) — завершены и верифицированы

---

## Phase 1 — Визуальное ядро ✅

### 1.1 ✅ HLSL-порт CloudMath.cs v7.0
- `CloudNoise.hlsl` — Perlin3D/Fbm/Worley3D/InvertedWorley, uint multiply-shift хэш, периодический режим
- `CloudCommon.hlsl` — HeightProfile, HG, MultiScatter, SilverLining, RaySlabIntersection, CameraRelativePosition, GhibliRamp, BeerLambert

### 1.2 ✅ Бейк 3D текстуры
- `BakeCloudNoise.compute` — 128³ RGBA8, каналы: R=PerlinFBM, G=WorleyLow(4), B=WorleyHigh(16), A=InvertedWorley
- `CloudNoiseBaker.cs` — синхронный Graphics.CopyTexture по Z-слайсам
- `CloudNoise3D.asset` — забейкана, 128³

### 1.3 ✅ VolumetricCloudsRenderFeature + VolumetricClouds.shader
- `VolumetricCloudsRenderFeature.cs` — URP RenderGraph, Pass 0: B&W debug, Pass 1: color direct
- `VolumetricClouds.shader` — fullscreen raymarch, ZTest Always (manual depth), multi-layer

### 1.4 ✅ Height profile + coverage + wind
- `HeightProfileSimple` — smoothstep градиент, min 100m fade, overlap слоёв
- `_WindOffset` — из WindManager.Instance, null-guard
- `CameraRelativePosition` — для float32 на 80k×80k сценах

### 1.5 ✅ Light marching + HG + multi-scatter + Ghibli ramps
- Light marching: 6 шагов к солнцу (LIGHT_STEPS=6)
- HG фаза: g=0.7 forward scattering
- Multi-scatter: pow(transmittance, 0.5)
- Ghibli ramps: день + закат, rampBlend от _SunDirection.y × 2.0
- Ambient + silver lining

### 1.6 ✅ Half-res + blue-noise dither + temporal reprojection
- Blue noise: `#pragma multi_compile_local _BLUE_NOISE_ON`
- Temporal: prevViewProj кэш, 90/10 lerp
- BlueNoise64.png

### 1.7 ✅ CloudPerfMonitor.cs
- CustomSampler, FrameTimingManager, rolling average, Editor OnGUI overlay

---

## Phase 2 — Интерактивность ✅

### 2.1 ✅ LocalDensityBuffer — 2026-08-03
- `LocalDensityBuffer.cs` — MonoBehaviour singleton, тор-окно 96³ (RHalf), ping-pong, SplatDensity API
- `LocalDensity.compute` — AdvectAndRelax + ApplySplats
- **Коммит:** `04624af9`

### 2.2B ✅ Variant B — Cloud Displacement Interaction — 2026-08-04
- Displacement вместо вычитания плотности
- Gate по высоте корабля ±400м, vertical suppression (disp.y × 0.15)
- **Изменённые файлы:** `LocalDensity.compute`, `LocalDensityBuffer.cs`, `VolumetricClouds.shader`, `VolumetricCloudsRenderFeature.cs`, `ShipWakeCloudCutter.cs`

---

### 2.3 ✅ VFX Graph: конденсационные следы — 2026-08-04

- `Contrail.vfx` — VFX Graph из шаблона Simple_Trail (VFX Graph 17.5.0)
- `ShipContrailVfx.cs` — управление Play/Stop + движение GameObject за кораблём
- `Ship_Light_root.prefab` — дочерний ContrailVFX с VisualEffect + ShipContrailVfx
- **Коммит:** `ad1f2364`

---

## Phase 3.0 — Multi-Layer Cloud System ✅

- 4 независимых слоя с per-layer bounds, density, coverageThreshold, GhibliRamp
- `_LayerNoiseMask = 0` — все слои делят шум (красивый вид)
- Overlap слоёв + min 100м fade
- `_DebugDensityScale`, `_DebugLayerMask`
- **Дефолтные слои:** 800–1200 / 1200–2500 / 2500–4500 / 4500–7000

---

## Depth Fixes — 2026-08-04

### 🩹 Fix #1: CopyDepthMode AfterTransparents → AfterOpaques
**Коммит:** `32480d50`

`m_CopyDepthMode = AfterTransparents` → `_CameraDepthTexture` была недоступна на `BeforeRenderingTransparents`. Software depth cull не работал. Hardware ZTest был единственной защитой → жёсткая граница без depth fade.

### 🩹 Fix #2: Reverse-Z safe depth check
**Коммит:** `c1c75851`

`sceneDepth < 0.999` несовместимо с reverse-Z: sky clear=0.0 → `0.0 < 0.999 = true` → `LinearEyeDepth(0) ≈ 0` → `tMax=0` → облака гаснут.
Замена на `Linear01Depth(sceneDepth) < 0.999` — платформо-независимая нормализация [0=near, 1=far].

### 🩹 Fix #3: ZTest LEqual → Always
**Коммит:** `e887c4ee`

RenderGraph pass без depth attachment → `ZTest LEqual` = undefined behavior → фрагменты отбрасываются.
Шейдер делает свой depth-тест через `SampleSceneDepth` → аппаратный ZTest избыточен.

### Итоговое состояние depth

| Уровень | Механизм | Статус |
|---|---|---|
| Software depth cull | `Linear01Depth` → `tMax = min(tMax, sceneLinear)` | ✅ |
| Depth fade | `cloudThickness / DepthFadeDistance` пост-цикла | ✅ |
| Hardware ZTest | ZTest Always (отключён) | — |
