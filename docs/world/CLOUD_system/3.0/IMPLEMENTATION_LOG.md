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

## Phase 2 — Интерактивность

### 2.1 ✅ LocalDensityBuffer — 2026-08-03

**Создано:**
- `Assets/_Project/Scripts/World/Clouds/LocalDensityBuffer.cs` — MonoBehaviour singleton,
  тор-окно 96³ (RHalf), ping-pong, SplatDensity API, CPU-зеркало (Phase 2.5).
- `Assets/_Project/Shaders/Clouds/LocalDensity.compute` — 2 kernel'а:
  `AdvectAndRelax` (адвекция ветром + релаксация) и `ApplySplats` (гауссовы сплаты).

**Коммит:** `04624af9` — T-CLOUD02: Phase 2.1 — LocalDensityBuffer

---

### 2.2B ✅ Variant B — Cloud Displacement Interaction — 2026-08-04

**Задача:** альтернативный (B) метод интерактивности: displacement вместо вычитания плотности.

**Изменённые файлы:** `LocalDensity.compute`, `LocalDensityBuffer.cs`, `VolumetricClouds.shader`, `VolumetricCloudsRenderFeature.cs`, `ShipWakeCloudCutter.cs`

**⚠️ Известная проблема — производительность:**
Рост `CutRadius` → O(radius³). Будущий фикс: indirect dispatch или analytical displacement.

---

### 3.0 ✅ Multi-Layer Cloud System — 2026-08-04

**Задача:** разбить единый облачный слой на 4 независимых слоя (800-1200, 1200-2500, 2500-4500, 4500-7000) с per-layer density, coverageThreshold и GhibliRamp.

**Изменённые файлы:**

| Файл | Изменение |
|---|---|
| `VolumetricCloudsRenderFeature.cs` | +`CloudLayerDef` struct, `Layers[4]` с дефолтами, `ActiveLayerCount` 1–4, `SetVectorArray` |
| `VolumetricClouds.shader` | `_LayerBounds[4]`, per-layer ramps, `ComputeLayerColor()`, per-layer цикл в `CloudDensity()` |

**Архитектура:**
- `CloudCoverageNoise()` — raw noise 0..1; per-layer порог в цикле
- `ComputeLayerColor(y, rampBlend)` — блендит GhibliRamp по heightFade-весу слоёв
- `CloudDensity()` — цикл: `shape * hFade * layerCov * densityMult`
- `_CloudBottomY`/`_CloudTopY` = глобальный min/max для RaySlabIntersection

**Дефолтные слои:**
1. 800–1200: CovThresh=0.35, Dens=1.5 (тёмный штормовой пол)
2. 1200–2500: CovThresh=0.5, Dens=1.0 (текущий слой)
3. 2500–4500: CovThresh=0.65, Dens=0.6 (перистые)
4. 4500–7000: CovThresh=0.75, Dens=0.3 (дымка)

**Перф:** +5–10% GPU. Displacement работает сквозь все слои.

---

## 🩹 Depth Fix — CopyDepthMode AfterOpaques — 2026-08-04

### Проблема

Облака рендерились **только за 3D-объектами** (на небе), без depth fade на границах геометрии.
Software depth cull в шейдере не работал — `_CameraDepthTexture` не содержал актуальных данных.

### Диагностика

Стек depth-теста в VolumetricClouds:

| Уровень | Механизм | Где |
|---|---|---|
| Hardware ZTest | `ZTest LEqual` на fullscreen triangle | Shader line 33 |
| Software depth cull | `SampleSceneDepth` → `tMax = min(tMax, sceneLinear)` | Shader lines 310–316 |
| Depth fade | `cloudThickness / DepthFadeDistance` пост-цикла | Shader lines 363–367 |

Корень проблемы: `m_CopyDepthMode = AfterTransparents` в `ProjectC_URP_Renderer.asset`.
Облачный pass исполняется на `RenderPassEvent.BeforeRenderingTransparents` — **до** копирования depth-текстуры. `ConfigureInput(ScriptableRenderPassInput.Depth)` теоретически должен форсировать копию, но с RenderGraph API на практике не срабатывает.

**Цепочка отказа:**
1. `_CameraDepthTexture` содержит значения по умолчанию (1.0 — near plane в reverse-Z)
2. `SampleSceneDepth(uv)` → 1.0 для всех пикселей
3. `sceneDepth < 0.999` → **FALSE** всегда — проверка «есть ли геометрия» не срабатывает
4. `sceneLinear = 1e9`, `tMax` никогда не климпится — **software depth cull выключен**
5. Единственная защита — hardware `ZTest LEqual`: fullscreen triangle depth=1.0, geometry depth 0.01–0.9 → `1.0 ≤ 0.9 = FALSE` → облака отбрасываются на объектах
6. Depth fade не работает (нет данных о расстоянии до геометрии)

**Результат:** облака видны только на небе, всегда за объектами, жёсткая граница без blend.

### Исправление

**Файл:** `Assets/_Project/Settings/ProjectC_URP_Renderer.asset`
**Изменение:** `m_CopyDepthMode: 1` (AfterTransparents) → `m_CopyDepthMode: 0` (AfterOpaques)

Это гарантирует, что `_CameraDepthTexture` заполняется после opaque-геометрии и **до** `BeforeRenderingTransparents`, где работает облачный pass. Теперь:
- `SampleSceneDepth` возвращает корректную глубину геометрии
- Software depth cull климпит `tMax` → облака не лезут сквозь объекты
- Post-loop depth fade имеет данные для плавного перехода на границах
