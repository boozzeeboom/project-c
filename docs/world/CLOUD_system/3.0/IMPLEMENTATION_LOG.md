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

---

### 2.2B ✅ Variant B — Cloud Displacement Interaction — 2026-08-04

**Задача:** альтернативный (B) метод интерактивности: вместо вычитания плотности — displacement (сдвиг координат 3D-шума), чтобы облака видимо расходились за кораблём.

**Архитектура:**
- Displacement = radial push от центра сплата: `direction = normalize(cellPos - splatCenter)`, `magnitude = Gaussian(dist, radius) * amount`
- Формат RT: RGBAHalf (RGB = вектор, A = резерв). Density-режим: RFloat.
- Единый source of truth: `LocalDensityBuffer.Mode` enum — RenderFeature и шейдер реагируют автоматически.

**Изменённые файлы:**

| Файл | Изменение |
|---|---|
| `LocalDensity.compute` | +2 kernel: `AdvectAndRelax_Disp` (мультипликативная релаксация векторов), `ApplySplats_Disp` (radial push) |
| `LocalDensityBuffer.cs` | +`enum Mode { Density, Displacement }`, RGBAHalf RT в disp-режиме, dispatch правильных ядер, CPU mirror только для Density |
| `VolumetricClouds.shader` | +`SampleLocalDisplacement()`, keyword `_LOCALDENSITY_DISPLACEMENT`, сдвиг worldPos до сэмплирования шума |
| `VolumetricCloudsRenderFeature.cs` | +`DisplacementStrength` (0-1000, default 300), keyword по Mode |
| `ShipWakeCloudCutter.cs` | Дефолты: `ConeSegments=16`, `ConeSpacing=0.35`, `CutRadius=50`, `CutAmount=1.0`, сплаты с i=0 (прямо у корабля). Конус: 0-280 юнитов, радиус 50-200. |

**A/B Switching:**
- `LocalDensityBuffer` inspector → `Mode` = `Density` (A) / `Displacement` (B)
- Параметр тюнинга: `DisplacementStrength` на ассете RenderFeature (в URP Renderer Data)

**⚠️ Известная проблема — производительность:**
Рост `CutRadius` квадратично увеличивает количество затронутых ячеек. При radius=200 и TexelSize=20 — сфера диаметром ~23 ячейки = O(23³) = 12k ячеек на сплат на GPU (в худшем случае). Решение для будущей итерации: либо indirect dispatch с bounding box сплатов вместо полного 128³, либо переход на analytical displacement в шейдере (вычислять displacement по формуле сплата напрямую, без 3D-текстуры).

