# CLOUD_system — Iteration Log

## Итерация от 2026-06-12

**Задача:** Upper-layer billboard quad mode — Phase 0 quick-win test (DEEP_ANALYSIS 2026-06-02, Approach C)

**Коммит:** `7db0825` — T-CLOUD01: Upper-layer billboard quad mode

**Изменения:**
- `Assets/_Project/Scripts/Core/NearCloudRenderer.cs` (+47/-6): Добавлен `UseBillboardQuad` флаг, `CreateDefaultMesh()` создаёт Quad вместо Sphere, `LateUpdate()` доворачивает quads лицом к камере
- `Assets/_Project/Scripts/Core/CloudManager.cs` (+3): `UpperUseBillboardQuad=true` по умолчанию, передаётся в UpperLayer
- `Assets/_Project/Scenes/BootstrapScene.unity`: сериализовано новое поле

**Результат:** Upper слой (6000-8000m) теперь рендерит 80 camera-facing quads вместо 3D-сфер. Middle и Lower слои без изменений.

## Итерация от 2026-08-02

**Задача:** Детальный план реализации Cloud Ocean Medium 3.0 — Фаза 1 (Визуальное ядро)

**Коммит:** `a4e18df` — T-CLOUD01: детальный план реализации Cloud Ocean Medium 3.0 (Фаза 1)

## Итерация от 2026-08-02 (Phase 1 Implementation)

**Задача:** CLOUD_system 3.0 — Phase 1: Визуальное ядро (задачи 1.1–1.7)

**Коммит:** `6add42ac` — T-CLD01: CLOUD_system 3.0 Phase 1 — визуальное ядро (1.1–1.7)

**Изменения:**
- `Assets/_Project/Shaders/Clouds/CloudNoise.hlsl` (NEW) — HLSL-порт CloudMath v7.0: Perlin3D, Fbm, Worley3D, InvertedWorley, uint-хэш, периодический шум
- `Assets/_Project/Shaders/Clouds/CloudCommon.hlsl` (NEW) — хелперы: Remap, HeightProfile, HG, MultiScatter, SilverLining, RaySlabIntersection, CameraRelativePosition, GhibliRamp
- `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader` (NEW) — Fullscreen raymarch: density + height profile + wind + light marching (6 steps) + HG (g=0.7) + multi-scatter + Ghibli day/sunset ramps (GDD-14)
- `Assets/_Project/Shaders/Clouds/BakeCloudNoise.compute` (NEW) — Compute shader 128³ RGBA8 UNORM, каналы Perlin/WorleyLow/WorleyHigh/InvertedWorley
- `Assets/_Project/Scripts/Rendering/VolumetricCloudsRenderFeature.cs` (NEW) — URP RenderGraph RendererFeature, AfterOpaques, fullscreen triangle, WindManager null-guard
- `Assets/_Project/Scripts/World/Clouds/CloudNoiseBaker.cs` (NEW) — Editor MenuItem «Bake 3D Noise Texture», AsyncGPUReadback → Texture3D
- `Assets/_Project/Scripts/World/Clouds/CloudPerfMonitor.cs` (NEW) — CustomSampler + FrameTimingManager + Editor OnGUI overlay
- `docs/world/CLOUD_system/3.0/IMPLEMENTATION_LOG.md` (NEW) — лог реализации

**Изменения:**
- `docs/world/CLOUD_system/3.0/CLOUD_OCEAN_MEDIUM_DETAILED_STEPS.md`: создан документ с 7 пошаговыми задачами Фазы 1
