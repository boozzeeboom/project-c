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

## Итерация от 2026-08-04 (Depth Fix)

**Задача:** Исправить некорректную работу глубины — облака были только за объектами, без depth fade

**Коммит:** `32480d50` — T-CLOUD08: Fix depth — CopyDepthMode AfterTransparents → AfterOpaques

**Изменения:**
- `Assets/_Project/Settings/ProjectC_URP_Renderer.asset`: `m_CopyDepthMode: 1` → `0` (AfterOpaques вместо AfterTransparents)
- `docs/world/CLOUD_system/3.0/IMPLEMENTATION_LOG.md`: задокументирован анализ и фикс

## Итерация от 2026-08-04 (Depth Fix #2 — Reverse-Z)

**Задача:** После фикса CopyDepthMode облака исчезли полностью — depth check в шейдере несовместим с reverse-Z

**Коммит:** `c1c75851` — T-CLOUD09: Fix reverse-Z depth check — sceneDepth < 0.999 → Linear01Depth

**Изменения:**
- `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader`: `sceneDepth < 0.999` → `Linear01Depth(sceneDepth) < 0.999` (строки 310-317)

## Итерация от 2026-08-04 (Depth Fix #3 — ZTest + закрытие фаз)

**Задача:** ZTest LEqual без depth-буфера в RenderGraph → облака не отображались. Актуализация документации.

**Коммит:** `e887c4ee` — T-CLOUD10: ZTest LEqual → Always — RenderGraph pass has no depth attachment

**Изменения:**
- `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader`: `ZTest LEqual` → `ZTest Always`
- `docs/world/CLOUD_system/3.0/IMPLEMENTATION_LOG.md`: консолидированная документация, статус 🟢

**Итог:** Фазы 1.1–3.0 (включая 2.1 LocalDensityBuffer, 2.2B Displacement, multi-layer) закрыты и верифицированы. Depth работает корректно.

## Итерация от 2026-08-04 (Phase 2.3 — VFX Contrail)

**Задача:** Создать VFX Graph конденсационного следа за кораблём

**Коммит:** `ad1f2364` — T-CLOUD11: Phase 2.3 — VFX конденсационный след (Contrail.vfx)

**Изменения:**
- `Assets/_Project/VFX/Contrail.vfx` (NEW) — VFX Graph из шаблона Simple_Trail, частицы спавнятся за кораблём
- `Assets/_Project/Scripts/Ship/ShipContrailVfx.cs`: `GetComponent<ShipController>` → `GetComponentInParent<ShipController>` (авторезолв на родителе)
- `Assets/_Project/Prefabs/Ships/Ship_Light_root.prefab`: добавлен дочерний `ContrailVFX` с `VisualEffect` + `ShipContrailVfx`
