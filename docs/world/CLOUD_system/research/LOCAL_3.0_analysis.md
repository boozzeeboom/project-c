# Анализ текущего состояния CLOUD_system 3.0 (ProjectC_client)

Дата: 2026-11-19. Источник: сабагент, читал доки `docs/world/CLOUD_system/3.0/*` (все 11 файлов), `1.0/`, `2.0/DEEP_ANALYSIS_2026-06-02.md`, весь релевантный код в `Assets/_Project`, `BootstrapScene.unity`, `ProjectC_URP.asset`, `ProjectC_URP_Renderer.asset`. Код/доки не изменялись, Play Mode не запускался.

> **Ключевая оговорка:** STATUS.md описывает желаемое состояние. Фактический код содержит **заглушки** вместо half-res/blue-noise/temporal — это главное расхождение при сравнении с внешним проектом.

---

## 1. Архитектура текущей системы

### 1.1 Общая модель

3.0 — отход от «объектов с шейдером» (mesh/billboard 1.0/2.0) к единому participating-medium raymarcher'у на весь мир:

```
CloudDensity(worldPos) = Σ(layer × HeightProfile × LayerNoiseShape × CoverageNoise × ColorRamp)  // GhibliRamp
                       - LocalDensity (корабельный след)
                       + StormDensity(originalPos)   // шторм иммунен к displacement
```

### 1.2 Инжект в URP

| Аспект | Факт |
|---|---|
| Механизм | `ScriptableRendererFeature` + `ScriptableRenderPass`, **RenderGraph API** (`RecordRenderGraph`), не `OnRenderImage`, не отдельная камера |
| Класс | `ProjectC.Rendering.VolumetricCloudsRenderFeature` (`Assets/_Project/Scripts/Rendering/VolumetricCloudsRenderFeature.cs`) |
| Pass | `internal sealed class VolumetricCloudsPass` (там же) |
| Событие | `RenderPassEvent.BeforeRenderingTransparents` (=450; после skybox, до transparents) |
| Вызов | `DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1)` — fullscreen triangle |
| Ассет | `Assets/_Project/Settings/ProjectC_URP_Renderer.asset`, feature активна, первая в списке |
| Глубина | `ConfigureInput(ScriptableRenderPassInput.Depth)`, `m_CopyDepthMode: 0` (AfterOpaques), `SampleSceneDepth` |
| Шейдер | `Hidden/ProjectC/VolumetricClouds` (`Assets/_Project/Shaders/Clouds/VolumetricClouds.shader`) |
| Uniform'ы | часть через `mat.Set*`, часть **только** через `Shader.SetGlobal*` (обход material-shadowing, T-CLOUD38) |

4 Pass в шейдере, реально используются 2:

| Pass | Имя | Статус |
|---|---|---|
| 0 | `VolumetricClouds_BW` | только при `DebugDensityDirect = true` |
| 1 | `VolumetricClouds_Color` | **основной**, бленд прямо в `colorTarget` (`Blend SrcAlpha OneMinusSrcAlpha`) |
| 2 | `VolumetricClouds_Composite` | **заглушка** `return half4(0,0,0,0)` |
| 3 | `VolumetricClouds_History` | **заглушка** `return half4(0,0,0,0)` |

RenderFeature инициализирует `_CloudHistoryA/B` (RTHandle, `ARGB32`), `_prevViewProj`, флаги `HalfResRender/TemporalReprojection/BlueNoiseDither` — но в `RecordRenderGraph()` не использует (`// Pass 1 → colorTarget direct (cloudRT/history bypassed for tuning)`).

### 1.3 Карта компонентов

**Ядро 3.0 (активное):**

| Файл | Класс / сущность | Роль |
|---|---|---|
| `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader` | 4 Pass | Реймарч, multi-layer, StormDensity, displacement, light marching |
| `Assets/_Project/Shaders/Clouds/CloudNoise.hlsl` | `Perlin3D`, `Perlin3D_noPeriod`, `Fbm`, `Worley3D`, `InvertedWorley`, `Hash3/Hash3Periodic` | HLSL-порт `CloudMath.cs` v7.0, uint multiply-shift хэш, периодический режим |
| `Assets/_Project/Shaders/Clouds/CloudCommon.hlsl` | `CloudRemap`, `HeightProfile(Simple)`, `HG`, `HG_DualLobe` (не используется), `MultiScatterApprox`, `SilverLining`, `BeerLambert`, `RaySlabIntersection`, `CameraRelativePosition`, `GhibliRamp` | Общая математика |
| `Assets/_Project/Shaders/Clouds/BakeCloudNoise.compute` | kernel `BakeNoise`, 128³ RGBA8 | R=PerlinFBM, G=WorleyLow(4), B=WorleyHigh(16), A=InvertedWorley(8) |
| `Assets/_Project/Shaders/Clouds/LocalDensity.compute` | 6 kernel'ов | Тороидальный 3D-буфер, wind-адвекция, гауссовские сплаты |
| `Assets/_Project/Scripts/Rendering/VolumetricCloudsRenderFeature.cs` | `VolumetricCloudsRenderFeature`, `VolumetricCloudsPass`, `CloudLayerDef` | Инжект в URP, multi-layer параметры, глобалы |
| `Assets/_Project/Scripts/World/Clouds/LocalDensityBuffer.cs` | `LocalDensityBuffer` (singleton) | 96³–128³ ping-pong 3D RT, `SplatDensity()`, CPU mirror, `SampleDensity()` |
| `Assets/_Project/Scripts/World/Clouds/StormCellDirector.cs` | `StormCellDirector`, struct `StormCell` | Источник штормовых ячеек, `OnLightningTriggered` |
| `Assets/_Project/Scripts/World/Clouds/StormLightningVfx.cs` | `StormLightningVfx` | Подписка → параметры `LightningBolt.vfx` + `Play()` |
| `Assets/_Project/Scripts/World/Clouds/Editor/StormCellDirectorEditor.cs` | — | Regenerate / Respawn / Save-Load Defaults |
| `Assets/_Project/Scripts/World/Clouds/Editor/CloudNoiseBaker.cs` | `CloudNoiseBaker` | `[MenuItem("ProjectC/Clouds/Bake 3D Noise Texture")]` |
| `Assets/_Project/Scripts/World/Clouds/ShipWakeCloudCutter.cs` | — | Корабельный кильватерный конус сплатов |
| `Assets/_Project/Scripts/Ship/ShipContrailVfx.cs` | — | VFX Graph контраилы, 1–5 точек, `StopDelay` |
| `Assets/_Project/Scripts/World/Clouds/CloudPerfMonitor.cs` | — | CustomSampler + FrameTimingManager + OnGUI overlay |
| `Assets/_Project/VFX/Contrail.vfx`, `LightningBolt.vfx` | VFX Graph 17.5.0 | — |
| `Assets/_Project/Data/Clouds/CloudNoise3D.asset` | Texture3D 128³ | Запечённый шум |

**Veil (параллельная активная система):**

| Файл | Роль |
|---|---|
| `Assets/_Project/Scripts/World/Clouds/VeilRaymarchMeshController.cs` | Плейн 200 000 юнитов, 24 шага, FO-хук `ApplyRebaseTranslation` |
| `Assets/_Project/Shaders/VeilRaymarchMesh.shader`, `VeilRaymarch.shader`, `VeilShader.shader` | CLOUDENGINE-style накопление, cel-shading, rim, молнии |
| `Assets/_Project/Scripts/World/Clouds/VeilRaymarchBlit.cs` | Легаси `OnRenderImage` → `Graphics.Blit` |
| `VeilSystem.cs`, `HorizonVeilRenderer.cs`, `AdditionalVeilModule.cs` | Геймплей Завесы + легаси-рендер |

**Мёртвый/legacy:** `StormController.cs`, `ServerStormManager.cs`, `StormCloudGenerator.cs`, `TestStormSpawner.cs`, `CloudManager.cs` (в сцене `m_IsActive: 0`), `NearCloudRenderer.cs`, `DistantCloudManager.cs`, `CloudSpherePhysics.cs`, `CumulonimbusCloud.cs`, `CloudClimateTinter.cs`, `StormController.prefab`.

---

## 2. Реализованные техники

### 2.1 Шейдинг-модель (Pass 1)

- Fullscreen raymarch, реконструкция луча `_Cloud_InvProj` + `_Cloud_ViewToWorld`.
- Slab intersection с Y=`_CloudBottomY`/`_CloudTopY` → `tMin/tMax`, early-exit `a>=0.99`.
- `CloudDensity()` = сумма 4 слоёв (`shape × hFade × coverage × densityMult`) + сплат-вычитание + `StormDensity()`.
- **Light marching:** `LIGHT_STEPS=6`, `lightStepSize=min(stepSize*3,200)`, `exp(-lightDensity*stepSize*_LightAbsorption*0.5)`.
- **Фаза:** `HG(cosTheta,0.7)`; `HG_DualLobe` есть, не применяется.
- **Multi-scatter:** `pow(transmittance,0.5)`.
- **Цвет:** `GhibliRamp(h01, rampBlend, dayTop/Mid/Bot, sunsetTop/Mid/Bot)`, `rampBlend=saturate(_SunDirection.y*2)`, `ambient=color*0.25`, `silver=pow(1-|dot(rd,sun)|,8)*intensity`.
- **Depth:** софтверcкий тест `Linear01Depth(sceneDepth)<0.999` → `tMax=min(tMax, LinearEyeDepth(...))`, `depthFade=saturate(thickness/_DepthFadeDistance)`. `ZTest Always`, `ZWrite Off`.
- **Локальная точность:** `CameraRelativePosition()` тайлит XZ, Y абсолютный — защита float32 на 80 000×80 000.
- Фактические значения (`ProjectC_URP_Renderer.asset`): `RaymarchSteps=37`, `MaxRayDistance=20000`, `LightAbsorption=0.014`, `Opacity=1`, `ColorIntensity=5`, `HeightEdgeSoftness=0.991`, `CoverageScale=0.0001`, `DepthFadeDistance=10`, `WindSpeedMultiplier=0.01`.

Слои (4):

| Слой | Bottom–Top | DensityMult | CoverageThreshold |
|---|---|---|---|
| Layer0 | 800–1200 | 10 | 0.1 |
| Layer1 | 1200–2500 | 1.88 | 0.362 |
| Layer2 | 2000–4500 | 1.05 | 0.496 |
| Layer3 | 4500–7000 | 0.3 | 0.435 |

> C#-дефолты другие (700–1500 / 1000–2800 / 2200–4800 / 4000–7200) — рассинхрон с ассетом; `EnsureLayerDefaults()` подставляет C#-значения только при нулевых bounds. `_LayerNoiseMask=0` → все слои на одном шуме, отдельного силуэтного типа нет.

### 2.2 Шумы

- `CloudNoise3D.asset` 128³ RGBA8 UNORM, `Repeat`/Trilinear, бесшовность через `Hash3Periodic`, не `fract`. Каналы: R=Perlin FBM (5 октав, p=0.5, l=2.0, seed 42), G=Worley 4, B=Worley 16, A=InvertedWorley 8.
- Coverage — процедурная 2D FBM (4 октавы `Perlin3D`, scale `0.0001`).
- `LayerNoiseShape`: `uvw=frac(samplePos/1024)`, `s = noise.r*lerp(0.5,1,noise.a) + (noise.g+noise.b)*0.15`.
- Ветер `_WindOffset` из `WindManager.Instance`, аккумулируется; coverage тоже сдвигается.
- Штормы (T-CLOUD39): процедурный `abs(Perlin3D)` FBM с lacunarity 2.3 + 3D domain warp + vertical noise — без текстуры и без `frac()` (лечило «гофротрубу»).

### 2.3 Ocean medium / интерактивность

- Концепт = единое поле: слой 800–7000 + coverage + локальный мутабельный буфер + шторма (Фаза 1–2).
- **Фаза 3.1 «море-пол» и 3.2 «завеса-граница» НЕ реализованы** (🔜 v3.5). Реймарчер — горизонтальный слой без `MaxRayDistanceHorizon`/`BottomDensityBoost`.
- `LocalDensityBuffer`: `Resolution=128`, `TexelSize=40.4` → окно ≈5171 юнит, `RFloat`/`ARGBHalf`, ping-pong, тор-адресация, адвекция + релаксация. В сцене `BufferMode:1 = Displacement`, `DisplacementStrength=1000`, `AdvectionStrength=5`, `RelaxationRate=0.643`.
- Displacement (T-CLOUD41): `_LOCALDENSITY_DISPLACEMENT`, `dispFactor = 1-saturate(|y-shipY|/400)`, `disp.y*=0.15`; шторм считается по оригинальной позиции.
- `ShipWakeCloudCutter`: `ConeSegments=16`, сплаты позади по `-dir`.

### 2.4 Контраилы

- `Contrail.vfx` — VFX Graph 17.5, шаблон `Simple_Trail`.
- `ShipContrailVfx`: `TrailCount` 1–5, авторазмер по largest enabled `MeshRenderer`, `sizeScale=shipBounds.z/15`, `StopDelay=0.4s`, эмиссия при `speed>5` и `!Ship.IsDocked`.
- Ручная доводка по гайду не завершена (alpha over life, цвет, Size 2.5, Lifetime 3.5, `Cloud_Noise1.png`); файл `Cloud_Noise1.png` **не найден**.

### 2.5 Штормы и молнии

- `StormCellDirector`: активен, но `SpawnTestCells=0`, debug-маркеры выключены → в проде ячеек нет.
- До 8 ячеек в шейдер (`MaxStormCellsInShader=3` в сцене): `_StormCellPos[8]`, `_StormCellParams[8]`, цвета, `_StormEdgeSoftness`, `_StormVerticalPeak`, шумовые параметры — всё `Shader.SetGlobal*`.
- `LightningBolt.vfx` **реально существует** (Spawn/Initialize/Update, exposed `StartPos/EndPos/Seed/Intensity`), `StormLightningVfx` подписан. Доки отстают: пишут «v3.5», а код уже есть.

### 2.6 Тени и лучи

- **Cloud shadows на мир — не найдено.** Self-shadow 6 шагами внутри реймарча + `SilverLining`.
- **Sun shafts / crepuscular rays — не найдено.**
- Fog integration (`unity_FogColor`) в 3.0 — не найдена.

---

## 3. Проблемы и расхождения

| Документ утверждает | Реальный код |
|---|---|
| STATUS 1.6: «Half-res, blue-noise, temporal ✅» | Pass 2/3 — заглушки, `RecordRenderGraph` рендерит Pass 1 full-res в `colorTarget`; `_BlueNoiseTex` не сэмплится; `_prevViewProj` не попадает в рендер |
| IMPLEMENTATION_LOG: «temporal prevViewProj, 90/10 lerp ✅» | `_TemporalBlend` выставляется, но шейдер его не объявляет; `_CloudHistoryA/B` аллоцируются, не читаются/пишутся |
| «Перф-замер ✅ CloudPerfMonitor» | `PERF_RESULTS.md` нет; `FrameTimingManager` требует Dynamic Resolution (в ассете выключён); sampler не обёртывает GPU-пасс |

Причина: при отладке «почему не видно облака» cloudRT/history/half-res/temporal отключили и не вернули; STATUS писался по плану.

Открытые/известные проблемы:
- Phase 2.2 displacement/разрез корабля визуально не виден, хотя данные живы (отладка остановлена по указанию пользователя). Гипотезы: шаг 20000/37≈540 юнитов, blending, `_LocalDensityInfluence`.
- Contrail VFX требует ручной доводки; `Cloud_Noise1.png` отсутствует.
- Phase 3 (море-пол, завеса-граница, серверные ячейки, shared-возмущения, выпиливание Veil-рендереров, перф-аудит) — 🔜 v3.5, не реализовано.
- `WeatherCellManager`, `CloudDisturbanceServer/Client`, `NetworkList<DisturbanceDto>` — не найдено.
- Исправлено: «гофротруба» (T-CLOUD39), material shadowing (T-CLOUD38), debug-позиционирование `Camera.main`→`Player` (T-CLOUD28–33), Veil без FO (T-FO09P), инцидент с маркером.

---

## 4. Ограничения и контракты

| Ограничение | Детали |
|---|---|
| **Floating Origin (критично)** | Мир 80 000×80 000, ребейз сдвигает корни. Новый контент обязан иметь `ApplyRebaseTranslation(t)`. Реализовано: `StormCellDirector` (T-FO07F), `VeilRaymarchMeshController` (T-FO09P), вызовы в `GlobalMotionControlledRebaseSlice.cs` (`ShiftStormCells()` 905–911, `ShiftVeilPlane()` 947–953) + маркеры `runtimeRebase.VeilShifted`. `LocalDensityBuffer` явного хука НЕ имеет (центр от `FollowTarget`, сплаты 1 кадр). |
| **URP 17.5 / RenderGraph** | `RecordRenderGraph`, `AddRasterRenderPass`. Одну текстуру нельзя read+write в пассе → ping-pong истории. MRT-бленд признан ненадёжным (A.4). `OnRenderImage` запрещён для нового кода. |
| **Compute shaders** | Доступны: `BakeCloudNoise`, `LocalDensity` (6 kernel), `#pragma target 5.0`, `[numthreads(8,8,8)]`, `StructuredBuffer<SplatData>`, 3D UAV очищаются compute. |
| **Depth/Normal** | Depth — да (`SampleSceneDepth`, reverse-Z safe). Normal buffer — не используется. |
| **HDR / MSAA** | `m_SupportsHDR: 0`, `m_MSAA: 1`, `m_RenderScale: 1`. Silver lining обрезается без HDR. |
| **Платформа** | StandaloneWindows64, `#pragma exclude_renderers gles`, target 5.0 (DX11). |
| **Perf-бюджет** | Фаза 1 ≤3 мс на 1080p mid-GPU; облака ≤4–5 мс. При full-res и ~260 вызовах `CloudDensity` на пиксель бюджет, скорее всего, превышен. |

---

## 5. Пробелы относительно современного volumetric rendering

Temporal reprojection+upscale ❌ (фактически), half-res ❌, blue-noise ❌, атмосферные LUT ❌, физические фазы 🟡 (один HG, `HG_DualLobe` не используется), интеграция с небом/туманом ❌, sun shafts ❌, cloud shadows на геометрию ❌, LOD/адаптивные шаги 🟡 (только early-exit), раздельные силуэты stratus/cumulus ❌, baked weather map + server cells ❌, HDR ❌, GPU-профайлинг cloud-pass ❌, чистота архитектуры 🟡 (Veil/легаси дублирование перенесено в v3.5).

**Итог:** функциональный, но «замороженный на полпути» кастомный raymarcher. Правильная архитектурная база + отключённые качественные механизмы + недоступный профиль перформанса. Ключевые вопросы к внешнему проекту: working temporal upscale, atmospheric LUT + multi-scatter, cloud shadows/sun shafts, FO-совместимость, реальный GPU-бюджет.
