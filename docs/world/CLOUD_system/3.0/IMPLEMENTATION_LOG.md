# CLOUD_system 3.0 — Implementation Log

**Date:** 2026-08-02
**Plan:** `CLOUD_OCEAN_MEDIUM_DETAILED_STEPS.md`
**Status:** 🟡 Phase 1 complete (code), верификация — пользователем

---

## Phase 1 — Итоговый статус

### 1.1 ✅ HLSL-порт CloudMath.cs v7.0
- `CloudNoise.hlsl` — Perlin3D/Fbm/Worley3D/InvertedWorley, uint multiply-shift хэш, периодический режим
- `CloudCommon.hlsl` — HeightProfile, HG, MultiScatter, SilverLining, RaySlabIntersection, CameraRelativePosition, GhibliRamp, BeerLambert
- Приёмка: статистическое сравнение HLSL vs C# — placeholder, не выполнено

### 1.2 ✅ Бейк 3D текстуры
- `BakeCloudNoise.compute` — 128³ RGBA8, каналы: R=PerlinFBM, G=WorleyLow(4), B=WorleyHigh(16), A=InvertedWorley
- `CloudNoiseBaker.cs` — синхронный Graphics.CopyTexture по Z-слайсам
- `CloudNoise3D.asset` — забейкана, 128³
- Приёмка: текстура сохранена, бесшовность не проверена

### 1.3 ✅ VolumetricCloudsRenderFeature + VolumetricClouds.shader
- `VolumetricCloudsRenderFeature.cs` — URP RenderGraph, AfterOpaques, 3 pass'а (B&W / Color / Composite)
- `VolumetricClouds.shader` — Pass 0: B&W density, Pass 1: colored light-march, Pass 2: composite

### 1.4 ✅ Height profile + coverage + wind
- `HeightProfileSimple` — smoothstep градиент в density функции
- `_WindOffset` — из WindManager.Instance, null-guard
- `CameraRelativePosition` — для float32 на 80k×80k сценах
- Coverage = 1.0 (равномерное, как в плане)

### 1.5 ✅ Light marching + HG + multi-scatter + Ghibli ramps
- Light marching: 6 шагов к солнцу (LIGHT_STEPS=6)
- HG фаза: g=0.7 forward scattering
- Multi-scatter: pow(transmittance, 0.5)
- Ghibli ramps: день (GDD-14) + закат, rampBlend от _SunDirection.y × 2.0
- Ambient: cloudColor × 0.15, silver lining: pow(1-abs(dot), 8.0) × 0.3
- _SunDirection из RenderSettings.sun, fallback (0.3, 0.7, -0.6)

### 1.6 ✅ Half-res + blue-noise dither + temporal reprojection
- Half-res: _CloudRT половинного разрешения, Pass A → _CloudRT, Pass B → composite
- Blue noise: `#pragma multi_compile_local _BLUE_NOISE_ON`, текстура генерируется через меню
- Temporal: prevViewProj кэш в RenderFeature, репроекция в Pass 2 (lerp 90/10)
- Blue noise генератор: `CloudNoiseBaker.GenerateBlueNoise()` → `Assets/_Project/Textures/BlueNoise64.png`

### 1.7 ✅ CloudPerfMonitor.cs
- CustomSampler, FrameTimingManager, rolling average
- Editor OnGUI overlay

---

## Что нужно сделать вручную

1. **Сгенерировать BlueNoise64:** меню `ProjectC → Clouds → Generate Blue Noise 64×64`
2. **Назначить BlueNoise64** в `Blue Noise Texture` у Renderer Feature (если нужен дизеринг)
3. **Назначить CloudNoise3D** в `Cloud Noise 3D` у Renderer Feature — уже должно быть
4. **Play Mode** для верификации

---

## Файлы

```
Assets/_Project/
├── Shaders/Clouds/
│   ├── CloudNoise.hlsl              1.1 ✅
│   ├── CloudCommon.hlsl             1.1 ✅
│   ├── VolumetricClouds.shader      1.3/1.4/1.5/1.6 ✅ (3 pass'а)
│   └── BakeCloudNoise.compute       1.2 ✅
├── Scripts/
│   ├── Rendering/
│   │   └── VolumetricCloudsRenderFeature.cs  1.3/1.6 ✅
│   └── World/Clouds/
│       ├── CloudNoiseBaker.cs       1.2 + blue noise generator ✅
│       └── CloudPerfMonitor.cs      1.7 ✅
├── Data/Clouds/
│   └── CloudNoise3D.asset           1.2 ✅ (забейкан)
└── Textures/
    └── BlueNoise64.png              1.6 ⏳ (сгенерировать через меню)
```

---

## Phase 2 — Интерактивность

### 2.1 ✅ LocalDensityBuffer — 2026-08-03

**Создано:**
- `Assets/_Project/Scripts/World/Clouds/LocalDensityBuffer.cs` — MonoBehaviour singleton,
  тор-окно 96³ (RHalf), ping-pong, SplatDensity API, CPU-зеркало (Phase 2.5).
- `Assets/_Project/Shaders/Clouds/LocalDensity.compute` — 2 kernel'а:
  `AdvectAndRelax` (адвекция ветром + релаксация) и `ApplySplats` (гауссовы сплаты).

**Детали:**
- RT: `RenderTextureFormat.RHalf`, `dimension=Tex3D`, `enableRandomWrite=true`
- Тор: `uvw = (worldPos - Center) / (Res * TexelSize) + 0.5; uvw = frac(uvw);`
- Ветер читается из `WindManager.Instance.CurrentWindDirection`
- Сплаты: `StructuredBuffer<SplatData>` (Vector3 center, float radius, float amount),
  гауссово ядро exp(-d²/2σ²) с σ = radius/3
- CPU-зеркало: `float[] _cpuDensity` (Res³), обновляется синхронно со сплатами,
  релаксация в Update
- `SampleDensity(Vector3)` — трилинейная интерполяция по 8 соседям с тор-адресацией

**Приёмка:** ⏳ требуется Play Mode тест пользователем
  - Объект с LocalDensityBuffer в сцене
  - SplatDensity тестовым вызовом → пятно затухает за ~1–2 с
  - Ветер двигает пятно

**Коммит:** `04624af9` — T-CLOUD02: Phase 2.1 — LocalDensityBuffer
