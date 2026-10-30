# A. Culling-стек Unity 6000.5.2f1 + URP 17.5: как устроено, что управляемо

**Дата:** 2026-10-30 · **Автор:** сабагент A («unity-stack», research-only) · **Статус:** final
**Область:** конвейер culling/отрисовки Unity 6000.5.2f1 + URP 17.5.0, применимость к PRIMUM
(1 207 батчей, 4 949 дверных рендереров, 6.69 млн тр., тени 1000 м / 2 каскада) и Floating Origin.
**Не входит:** ассеты (B), FO-архитектура (D), смета (E).

---

## 0. TL;DR для Lead

1. Рендерер — **Forward+**: `m_RenderingMode: 2` = `RenderingMode.ForwardPlus` (Forward=0, Deferred=1,
   ForwardPlus=2, DeferredPlus=3). **[VERIFIED, `UniversalRenderer.cs:11–23`]**
2. Тени курятся отдельно от камеры: `ShadowCulling.CullShadowCasters` → `context.CullShadowCasters(...)` с
   per-cascade `ShadowSplitData`. Каскады 1000 м/2, split 0.25 → **0–250 м и 250–1000 м**. **[VERIFIED]**
3. Depth priming off, но **depth prepass есть** (`VolumetricClouds`/`DistantFocus` просят
   `ScriptableRenderPassInput.Depth`) → каждый непрозрачный рендерер рисуется ≥2 раз. **[VERIFIED]**
4. `m_AdditionalLightShadowsSupported: 0` → additional-shadow проходов нет; счёт дверей не умножается на свет
   фонарей. **[VERIFIED, ассет]**
5. `Camera.useOcclusionCulling` доезжает до параметров через `Camera.TryGetCullingParameters`, URP его не
   сбрасывает → Umbra в URP 17.5 работал бы, но данных нет. **[VERIFIED-код]**
6. **Umbra мёртв дважды:** бейк падает в split-фазе и baked-PVS — world-space данные, а FO двигает мир (>256 м):
   после ребейза карта описывает прошлое. **[LIKELY]**
7. **GPU RD** — единственный встроенный путь, сокращающий *число* draw calls (indirect через BRG); его
   Hi-Z-окклюзия **FO-safe** (пирамида строится каждый кадр) и двухпроходна (`TestAll` по прошлому кадру →
   `TestCulled` по промежуточному текущему), лимиты 8 мипов / 6 плоскостей / 6 сабвью. **[VERIFIED]**
8. GRD требует RawBuffer (D3D12 ✔), compute-ядро `OccluderDepthDownscale`, **BRG Variants = Keep All**,
   DOTS-инстансинг в шейдерах; последнее нас и отбросило 18.10. **[VERIFIED]**
9. **Двери = ≥2 сабмеша** (аудит C: в среднем **2.02**); `draws = submeshes × (opaque + depthPrepass + #каскадов)`.
   При 1 сабмеше арифметика невозможна. **[LIKELY]**
10. Реконструкция: **≈4 100 дверей в фрустуме, ≈700 в каскаде 0, ≈2 130 в каскаде 1** → 17 766 (250/1) и
    22 042 (1000/2) сходятся с точностью <1 %; ~93 % счёта — opaque + prepass, тени 8–26 %. **[LIKELY, арифметика]**
11. Дешёвые рычаги без переделки: `shadowCastingMode=Off` дальним дверям (−4 260 draw) и
    `Camera.layerCullDistances` для слоя дверей (−8 000). **[план]**
12. **SRP Batcher draw calls не сокращает** (только SetPass); сокращают instancing / BRG / склейка. 1 806 слотов
    города = ~1 806 draw на проход. Склейка «чанк × материал» → единица culling = AABB в сотни метров:
    761 батч = 761 draw в каждом проходе. **[VERIFIED-механика]**
13. Мультикамерность = N полных `Cull`: каждая камера в стеке (включая Overlay) cull'ит заново;
    `AtmosphereCamera` (far 120 000) видит весь мир. **[VERIFIED-код]**
14. Из коробки Unity **не умеет**: окклюзию без бейка для обычных рендереров, per-object distance-fade,
    иерархический culling групп, бюджет теней по объекту, FO-совместимую PVS (ниши — §4).

---

## 1. Вопросы и метод

RQ1 из `01_PROBLEM_STATEMENT.md` + вопросы брифа (конвейер, механизмы, Umbra, GRD, дверная математика,
батчинг, мультикамерность, инструментация, матрица).

Источники: исходники URP **17.5.0** (`Library/PackageCache/...universal@748d7458cc62/`, `package.json: unity 6000.5`)
и RP Core 17.5.0 (`...core@9fe9377312a8/Runtime/GPUDriven/**`) — **первичные, версия в точку**; рефлексия сборок
(`C:\UNITY_PROJECTS\6000.5.2f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll`,
`Library/ScriptAssemblies/Unity.RenderPipelines.{Core,Universal,GPUDriven}.Runtime.dll`) — **первичный**; настройки
`Assets/_Project/Settings/ProjectC_URP*.asset`, `QualitySettings.asset`, скрипты `DistantFocus/VolumetricClouds/
DetailDistanceCuller/ViewDistanceApplier` — **первичный**; доки `04_OCCLUSION_CULLING_RESEARCH.md`,
`01_INDUSTRY_PRACTICE.md`, `01_PROBLEM_STATEMENT.md` и `08_DATA/C_primum_batches.csv` — **вторичные**.

**Ограничение:** официальные доки недоступны — `docs.unity3d.com/6000.5/...` редиректит на `docs.unity.com`,
версионные пути (`/en-us/6000.5/manual/...`) отдают 404. Доковые утверждения помечены `[LIKELY]` и продублированы
местом в исходниках. Play Mode не запускался, `Assets`/`ProjectSettings` не менялись, коммитов нет.

---

## 2. Находки

### 2.1 Конвейер кадра URP 17.5: где culling

`UniversalRenderPipeline.RenderSingleCamera` (строки по 17.5.0):

| # | Шаг | Стр. |
|---|---|---|
| 1 | `camera.TryGetCullingParameters` — frustum-плоскости, `cullingMask`, `layerCullDistances`, `lodParameters`, `shadowDistance`, флаг occlusion | 861 |
| 2 | `renderer.SetupCullingParameters` — URP правит `maximumVisibleLights`, `reflectionProbeSortingCriteria`, `shadowDistance`, `DisablePerObjectCulling` (Forward+/Deferred+ с RP-atlas), снимает `ShadowCasters` если теней нет (`UniversalRenderer.cs:627–674`) | 908–909 |
| 3 | **`context.Cull(ref p)`** — **один** CPU-cull на камеру: frustum + layers + layer-distances + LOD + Umbra + visible lights + probes | 971 |
| 4 | `GPUResidentDrawer.PostCullBeginCameraRendering(...)` — хук BRG/GRD к результатам | 973 |
| 5 | `CreateLightData/CreateShadowData` — свет и тени из тех же `cullResults` | 985–986 |
| 6 | **`ShadowCulling.CullShadowCasters` → `context.CullShadowCasters`** — отдельный отсев теневых кастеров, per-light/per-slice | 1000, 1028 |
| 7 | `AddRenderPasses` + RenderGraph — проходы строят renderer list'ы из `cullResults` | 1002–1003 |

**Тени** (`ShadowCulling.ComputeShadowCasterCullingInfos`): по `cullResults.visibleLights`; directional →
`splitCount = mainLightShadowCascadesCount` (**2**), на каскад `ExtractDirectionalLightMatrix` → `ShadowSplitData`
(`cullingSphere`, `cullingMatrix`, до 6 плоскостей); point → 6 граней, spot → 1; затем `context.CullShadowCasters`.
**Объект попадает в теневой проход каскада только если его bounds проходят проверку сферы/плоскостей каскада** —
отсюда разница 1000/2 vs 250/1.

**Проходы:** Depth Prepass — **да** (фичи требуют Depth; «full prepass», `UniversalRendererRenderGraph.cs:974–980`);
Opaque Forward — да; ShadowCaster MainLight — да, 2 каскада; ShadowCaster Additional — **нет**
(`m_AdditionalLightShadowsSupported: 0`); MotionVectors — [UNVERIFIED]; Transparent/Overlay/Post — да.

**Перехватывается** **[VERIFIED]**: `ScriptableRendererFeature`/`ScriptableRenderPass` (любой `RenderPassEvent`,
доступны `cullResults`/`UniversalCameraData`/`LightData`/`ShadowData`); **`CullContextData`** —
`frameData.Get<CullContextData>().Cull(ref p)` / `CullShadowCasters(...)` = **свой дополнительный cull** с другим
`cullingMatrix`/`cullingMask`/`shadowDistance` (ключ к FO-совместимой кастомной видимости);
`context.CreateRendererList(RendererListParams)` со своим `FilteringSettings`; `BatchRendererGroup.OnPerformCulling`
— **единственное место, где виден список инстансов** (+`OcclusionTestAABB`); `RenderPipelineManager.beginCameraRendering`
(URP зовёт сам, стр. 512) — замена `OnPreCull`; правка `ScriptableCullingParameters` до `Cull`.

**Нельзя:** получить список видимых рендереров из `CullingResults` (только `visibleLights`, `visibleReflectionProbes`,
index-maps) **[VERIFIED]**; ждать `OnPreCull`/`OnPreRender` в SRP **[LIKELY]**; вклиниться в Umbra (только `OcclusionPortal.open`).

### 2.2 Встроенные механизмы в URP 17.5

| Механизм | API (проверено) | Отсекает | Правила/лимиты | FO-safe |
|---|---|---|---|---|
| Frustum | `Camera.cullingMatrix` | вне пирамиды | `cullingMatrix` позволяет подменить пирамиду **[LIKELY]** | да |
| `cullingMask` | `int` 32 бита | слои | следующий кадр | да |
| `layerCullDistances` | `float[32]` + `layerCullSpherical` | слой дальше N м | массив строго 32; spherical → сфера от камеры, иначе глубина; 0 = без лимита; доезжает до `SetLayerCullingDistance` **[VERIFIED-поля]** | да |
| `renderingLayerMask` | `Renderer.renderingLayerMask` + `FilteringSettings` | подмножества | light-layers только при `m_SupportsLightLayers: 1` (у нас 0) | да |
| `LODGroup` | `ForceLOD`, `QualitySettings.lodBias`, `forceMeshLod` | уровень LOD | по экранной высоте через `LODParameters`; **0 LODGroup** | да |
| `CullingGroup` | `SetBoundingSpheres/Distances`, `SetDistanceReferencePoint`, `targetCamera`, `onStateChanged`, `IsVisible`, `GetDistance`, `QueryIndices` | **сам не куллит** — CPU-API «видно / полоса» | десятки тысяч сфер, события при смене, окклюзию не учитывает **[VERIFIED-API]** | да |
| `enabled` / `forceRenderingOff` / `SetActive` | `bool` | рендер+тени / только отрисовку / весь объект | `enabled` дробит батчи, на каденции 0.25–0.5 с по группам; `forceRenderingOff` дешевле (GRD уважает **[LIKELY]**); `SetActive` — не для per-frame куллинга | да |
| `shadowCastingMode` / `receiveShadows` / `isVisible` | enum / `bool` / read-only | тени / приём теней / — | `shadowCastingMode` — дешёвый теневой рычаг; `receiveShadows` меняет keyword (SetPass, не draw); `isVisible` обновляется после culling → лаг 1 кадр **[LIKELY]** | да |
| `smallMeshCulling`/`allowGPUDrivenRendering` | `bool` (Renderer) | мелкие меши / opt-out | только GPU-путь | да |

Практика: `layerCullDistances` — самый дешёвый серьёзный рычаг; `CullingGroup` — полосы без `Update()` по
2 363 трансформам; массовые переключения `enabled` — на каденции 0.25–0.5 с и по **группам** (дом/хамлет/модуль).

### 2.3 Umbra: устройство и почему мёртв

**Факты.** Бейк — **Umbra 3 Optimizer 3.3.22** (лог `04_OCCLUSION…` §10) **[VERIFIED-проект]**; API
`UnityEditor.StaticOcclusionCulling` (`Compute`, `GenerateInBackground`, `umbraDataSize`, `smallestOccluder`,
`smallestHole`, `backfaceThreshold`; **нет** `doesSceneHaveOcclusionCullingData`). Runtime — флаг
**`CullingOptions.OcclusionCull`** + `accurateOcclusionThreshold` в `ScriptableCullingParameters` **[VERIFIED, рефлексия]**;
есть `OcclusionPortal.open`, `OcclusionArea{center,size}` **[VERIFIED]**. Данные — ассет `OcclusionCullingData`
(`m_OcclusionCullingData: {fileID: 0}`, `umbraDataSize=0`). `Terrain_0_0` без Renderer → в Umbra-геометрию не
входит. **[VERIFIED-проект]**

**Фазы** (по стеку лога): сбор окклюдеров → **spatial split** (ячейки/tome + порталы) → PVS → упаковка tome.
Падение в `OcclusionCullingTask::GenerateTome` / `Failure in split phase` — **до** PVS, т.е. проблема во
входной геометрии/разбиении, а не в «сложной видимости». **[интерпретация LIKELY]**

**Почему не наш путь:** (1) **world-space данные vs FO** — ребейз двигает мир, данные нет → камера попадает
«в чужую ячейку», PVS врёт, пересчёт = бейк в редакторе **[LIKELY]**; (2) запрет static-флагов; (3) бейк не
проходит; (4) Terrain не окклюдер → PVS неполон для котловины; (5) окклюдеры — merged-батчи с «дырявыми»
оболочками, а Umbra строит замкнутые. **[VERIFIED-проект, кроме п.1]**

**Гипотезы отказа и дешёвые тесты:** H1 невалидная геометрия (бейк сцены из 20 замкнутых кубов, 30 мин);
**H2 окклюди, а не окклюдеры ломают split** — «1 куб + 0 окклюди», затем «+2 464 Frame» (попытка 4 не снимала
occludee-флаги!); H3 порог `smallestOccluder` — довести попытку 6 (`=20`) до конца, ждать 30–60 мин; H4 баг
3.3.22/лимит tome — 5 000 кубов в сетке. Исключено: объём/разлёт, >65k вершин, число окклюдеров, большие мировые
координаты, нулевой `m_SceneGUID`.

**Runtime-стоимость** (если данные были бы): доли мс — обход tome/PVS, растёт от числа ячеек, не объектов;
выигрыш зависит от плотности окклюзии, а стены у нас тонкие. **Вердикт: не инвестировать.**

### 2.4 GPU Resident Drawer + GPU occlusion culling

**Механика.** GRD — встроенный потребитель `BatchRendererGroup`: рендереры регистрируются в GPU-буферах
инстансов; CPU (Burst) считает per-view видимость, GPU строит **Hi-Z** из depth и делает окклюжн-тесты в
compute; результат — **indirect draw args** на batch layer (окклюдированные инстансы туда не попадают).

| Что | Факт | Источник |
|---|---|---|
| Hi-Z лимиты | `MaxOccluderMips = 8`, `MaxOccluderSilhouettePlanes = 6`, `MaxSubviewsPerView = 6` | `Culling/OcclusionCullingCommon.cs:10–15` |
| Два прохода | `TestAll` (пирамида прошлого кадра) → `TestCulled` (промежуточная текущего) | `UniversalRendererRenderGraph.cs:1027–1078, 1111–1138, 1160–1178` |
| Маска батчей | `OcclusionTest.GetBatchLayerMask()` → `BatchLayer.InstanceCullingIndirectMask` | `GPUResidentDrawerTypes.cs:34–48` |
| Включение | `IsInstanceOcclusionCullingEnabled() && renderer.supportsGPUOcclusion && cameraType ∈ {SceneView,Game,Preview}`; `requiresDepthTexture \|= true` | `UniversalRenderPipeline.cs:1752–1756` |
| Окклюдер-проход | после prepass, иначе gbuffer/opaque; Qualcomm исключён (UUM-82677) | `UniversalRendererRenderGraph.cs:984–1003, 696–709` |
| Публичный API | `IsInitialized`, `IsInstanceOcclusionCullingEnabled`, `InstanceOcclusionTest`, `UpdateInstanceOccluders`, `Reinitialize`, `settings`, `ForceOcclusion`, `debugDisplaySettings`, `GetDebugStats` | рефлексия GPUDriven.dll **[VERIFIED]** |

**Требования** (`GPUResidentDrawer.Validator.cs:24–69`): `BatchRendererGroup.BufferTarget == RawBuffer` (D3D12 ✔);
ядро `OccluderDepthDownscale` (фактически compute); `batchRendererGroupShaderStrippingMode == KeepAll`; не VisionOS.
Плюс mode ≠ Disabled, в редакторе `allowInEditMode`/Play Mode, RP-ассет реализует `IGPUResidentRenderPipeline`.
«Forward+/Deferred+» и DOTS-инстансинг в шейдерах — **[LIKELY]** по доке; для нас подтверждено откатом 18.10
(`unity_ObjectToWorld`/`DOTS_INSTANCING_ON`).

**Ограничения:** только MeshRenderer/SkinnedMeshRenderer (не Terrain/VFX/Sprite) **[LIKELY]**; DOTS-варианты во
всех шейдерах объектов **[VERIFIED-проект]**; `MaterialPropertyBlock`-путь **[UNVERIFIED]**; `allowGPUDrivenRendering`
— opt-out **[VERIFIED-API]**; LODGroup через GPU-LOD **[VERIFIED-наличие]**; `smallMeshScreenPercentage` —
готовый отсев мелких мешей **[VERIFIED-API]**.

**FO:** запекать нечего (Hi-Z каждый кадр, инстансы из трансформов) → **FO-safe по построению**. Цена: ребейз
двигает все корни → разовое обновление матриц всех 7 152 инстансов (спайк, замерить — E6); ребейз редкий
(>256 м, кулдаун 35 с) — терпимо.

**Кастомизируется:** режим/окклюзия/`allowInEditMode`/`smallMeshScreenPercentage`, per-renderer opt-out, типы
камер, debug-оверлеи. **Нет:** алгоритм Hi-Z и лимиты, «свои окклюдеры», список видимых инстансов на CPU —
полный контроль только через свой BRG с `OnPerformCulling`.

### 2.5 Почему 4 949 дверных рендереров дают ~17 766 draw calls

`DC = Σ_дверь [ submeshes × (1_opaque + 1_depthPrepass + Σ_каскады 1_shadow) ]`; SRP Batcher не влияет,
additional-shadow проходов нет. Известно: 4 949 рендереров (2 363 × 2: frame+leaf), **сабмешей в среднем
2.02** (1→653, 2→1768, 3→502, 4→110 из 3 033 строк `C_primum_batches.csv`), ~204 вершины на рендерер,
каскады 0–250/250–1000, ΔA(1000/2) = 22 042, ΔB(250/1) = 17 766, A−B = **4 276**.

Пусть `V` — в фрустуме, `S0`/`S1` — в каскадах, `m` — сабмеши: `m·(2V+S0)=17 766`, `m·S1=4 276`.

| m | 2V+S0 | S1 | Следствие |
|---|---|---|---|
| 1 | 17 766 | 4 276 | **невозможно**: 2V>17 766 ⇒ V>8 800 > 4 949 |
| **2** | **8 883** | **2 138** | S0≈700 ⇒ **V≈4 090** (83 %) — согласуется (точка выбиралась по максимуму дверей) ✔ |
| 3 | 5 922 | 1 425 | V≈2 700 — противоречит CSV (2.02) |

Сходимость: m=2, V≈4 100, S0≈700, S1≈2 130 → opaque 8 200 + prepass 8 200 + тени B 1 400 = **17 800**
(+0.2 %); для A: 8 200+8 200+(700+2 130)×2 = **22 060** (+0.1 %). **[LIKELY, арифметика]**

Выводы: (а) ~93 % счёта — opaque+prepass, тени 8–26 %; (б) 1000/2→250/1 экономит 4 276 = тени каскада 1;
(в) главные рычаги без переделки — `shadowCastingMode=Off` дальним дверям (−4 260) и `layerCullDistances`
(−8 000 в opaque+prepass).

**Проверка:** Frame Debugger → `Draw Mesh` в `Depth Prepass` / `Draw Opaque` / `ShadowCaster` (фильтр по дверным
мешам) → точные `V/S0/S1/m`. **Треугольники:** ΔA в старом доке 0.18 млн не сходится с моделью (~1 млн), ΔB
(0.73 млн) сходится → **draw calls надёжны, triangles перемерить** (в A/«выкл», похоже, смешаны условия). **[флаг]**

### 2.6 Батчинг: что склеивает draw calls, а что только state

| Механизм | Что делает | Draw calls | Условия | Culling |
|---|---|---|---|---|
| **SRP Batcher** | CBUFF материала, убирает SetPass внутри шейдерного варианта | **не меняет** | SRP-Batcher-compatible шейдер; `MaterialPropertyBlock` ломает | без изменений |
| **GPU instancing** | 1 draw на группу инстансов одного меша+материала | сокращает | instancing-вариант шейдера | per-instance **отключён** (bounds группы) |
| **BRG / GRD** | indirect draws произвольных мешей/материалов | сильно сокращает | DOTS-инстансинг, RawBuffer, Keep All | **per-instance сохраняется** |
| **Static batching** | объединяет VB/IB статиков | сокращает | статик-флаги (**запрещены**) | per-object запись есть (`staticBatchIndex`), гранулярность — чанки **[LIKELY]** |
| **Ручная склейка** (наш join) | 1 меш = 1 рендерер | сокращает | — | **гранулярность = общий AABB** |

Механика деградации: culling оперирует `Renderer.bounds` (один AABB); батч чанк×материал имеет AABB в сотни
метров → почти всегда во фрустуме → рисуется целиком (дома за спиной тоже) и попадает во **все** каскады.
761 батч = 761 draw в каждом проходе; город: 1 806 слотов → ~1 806 opaque + 1 806 prepass + тени ≈ 5 400
(это и видно как «двери выкл = 4 954»). **Правило для C/F:** мелкая склейка (дом / ячейка 50–100 м) + GRD —
гранулярность возвращается, draw calls склеиваются инстансингом.

### 2.7 Мультикамерность

- **Каждая камера — свой `Cull` и свой набор проходов**; Overlay-камера в стеке тоже вызывает
  `RenderSingleCamera` (стр. 1263) → отдельный cull. 3 камеры = 3× CPU- culling и 3× draw для попавшей геометрии. **[VERIFIED-код]**
- **`AtmosphereCamera`** (runtime, far 120 000) видит весь мир → второй полный кадр (в замерах выключалась, в игре
  включена). Лечение: `cullingMask` только атмосферные слои либо маленький RT + `far` 30–60 км.
- **Reflection probes:** Realtime рендерят 6 граней при обновлении (свой cull на грань), Baked — 0 draw;
  `Renderer.reflectionProbeUsage` (Off/Simple/Blend) сокращает SetPass, не draw. Сколько у нас Realtime-проб —
  **[UNVERIFIED]** (сцену-гигант не парсил).
- Фичи culling не добавляют, но добавляют проходы: `DistantFocus`/`VolumetricClouds` требуют Depth → prepass (§0.4).

### 2.8 Инструментация

**Инструменты.** `UnityStats` (editor-only; **[VERIFIED-рефлексия]**) — готовое разложение счёта:
`drawCalls`, `setPassCalls`, `srpBatcherDrawCalls`, `standardDrawCalls`, `instancedBatchedDrawCalls`,
`staticBatchedDrawCalls`, `dynamicBatchedDrawCalls`, `hybridBatcherDrawCalls`, `totalIndirectDrawCalls`,
`shadowCasters`, `trianglesLong`. Frame Debugger — по-проходный `Draw Mesh`, единственный точный способ получить
V/S0/S1/m. GPU RD debug (`debugDisplaySettings`) — heatmap окклюзии, Visible/Culled Instances/Primitives,
Occluder Debug View, `ForceOcclusion`, `Override Occlusion Test To Always Pass` **[VERIFIED]**. `ProfilerRecorder`
(`Unity.Profiling`) — `StartNew(ProfilerCategory, "Counter")`/`GetLastValue`/`ToArray`, имена через
`ProfilerRecorderHandle.GetAvailable(List<>)` **[VERIFIED]**. Плюс маркеры URP `<SetupCullingParameters>`,
`<CullShadowCasters>`, `<InstanceOcclusionTest>` и RenderDoc/PIX для точных D3D12 draws.

**Методика A/B** (из `04_OCCLUSION…` §2): FPS первых ~1 500 кадров врёт (23.5→34.1 без изменений),
`UnityStats.drawCalls` стабилен ±15. Сравнивать на равном `frameCount`, Scene View — в небо, камера —
фиксированная точка, `AtmosphereCamera` off, прогрев ≥1 500 кадров, 3 прогона → медиана, фиксировать CPU/GPU-bound.

---

## 3. Матрица: что отсекает → стоимость → FO → применимость

| Механизм | Отсекает | Стоимость | FO | PRIMUM / двери / корабли |
|---|---|---|---|---|
| Frustum | вне пирамиды | ~0 | да | работает, но 761 батч всё равно в кадре |
| `cullingMask` | слои | ~0 | да | слои покрытия, мультикамерность (Atmosphere) |
| `layerCullDistances` | слой дальше N | ~0 | да | **Топ-1**: слой дверей 120–150 м |
| `CullingGroup` | «не рисовать дальше N» | низкая, события | да | иерархические полосы дверей/домов/модулей |
| `shadowCastingMode` | тени | ~0 | да | **Топ-2**: двери >200 м → Off (−4 260) |
| Гранулярность склейки | внутри батча | 0 | да | сейчас убита (чанк×материал) → дом/ячейка |
| `LODGroup` | меши уровня | низкая | да | нет LOD; нужен домам (Э6) |
| Umbra | всё за стенами | низкая в рантайме | **нет** | **не использовать** (бейк + FO) |
| GRD instancing / occlusion | число draws / за окклюдерами | CPU-джобы+GPU | да | фундамент «много независимых объектов»; нужны DOTS-шейдеры |
| Свой BRG culling | что угодно | наша цена | да | путь для собственной видимости (F) |
| `enabled`/`forceRenderingOff` | рендерер | средняя | да | для групп на каденции 0.25–0.5 с |

---

## 4. Что это значит для Project C

- **Двери — не «город», а два массовых прохода:** ~8 200 opaque + 8 200 prepass, тени 1 400–5 660; город
  целиком — ~5 400 draw, т.е. без дверей оптимизировать нечего.
- **Приоритет 1 (дни):** слой `Doors` + `layerCullDistances`; `shadowCastingMode=Off` дальше 200 м; проверить,
  нужен ли полный depth prepass (если да — искать вариант без него для одной из фич).
- **Приоритет 2 (недели):** GRD + DOTS-шейдеры — единственная FO-совместимая окклюзия и способ дёшево держать
  «много независимых объектов»; основной объём — шейдеры, не C#.
- **Приоритет 3 (C/F):** вернуть гранулярность — склейка по дому/ячейке 50–100 м вместо чанк×материал; без GRD
  это поднимет CPU-culling, с GRD — оптимум.
- **Корабли:** внутренние объекты почти всегда в фрустуме → окклюзия только внешними окклюдерами; выгоднее
  LOD/импосторы и `layerCullDistances` на мелкие модули; «не склеивать, а куллить» — при гранулярности модуля + GRD.
- **Мультикамерность:** проверить `AtmosphereCamera` (маска/дальность) и Realtime-пробы — скрытый 2-й/3-й кадр.
- **FO-контракт:** никаких данных в мировых координатах; решения frame-local из живых `Transform`/`bounds`;
  после ребейза достаточно сбросить кэши. **Umbra:** закрыть (несовместим с FO принципиально).

---

## 5. Рекомендованные эксперименты

| # | Что мерить | Как | Ожидание | Kill criteria |
|---|---|---|---|---|
| E1 | Разложение счёта по проходам | Frame Debugger + `UnityStats` до/после выключения дверей | V≈4 100, S0≈700, S1≈2 130, m=2 | m=1 или opaque+prepass ≠ половина Δ → модель §2.5 неверна |
| E2 | Цена теней дверей | `shadowCastingMode=Off` для дверей >200 м | −4 000…−4 300 draw, FPS +10–20 % | Δ<1 500 → каскад 1 не так дорог |
| E3 | `layerCullDistances` для дверей | слой + `[Doors]=120/150`, `layerCullSpherical=false` | −7 000…−8 500 draw | дыры/поппинг ближе 150 м → откат |
| E4 | Стоимость prepass | убрать `ScriptableRenderPassInput.Depth` у одной фичи (ветка) | −1 800 city −8 200 doors | prepass исчез, quality не упал → оставить без него |
| E5 | CullingGroup-полосы дверей | 2 363 сферы, полосы {60,120,200,400} | CPU < `DetailDistanceCuller`, точнее | события каждый кадр → полосы/гистерезис неверны |
| E6 | GRD + occlusion + FO-ребейз | ветка с исправленными шейдерами, замер до/после F8 | draw calls ↓ 2–5× | спайк ребейза >50 мс или шейдеры >2 недель → отложить |
| E7 | Мультикамерность | `AtmosphereCamera.cullingMask` только небо | −30…−50 % draw при включённой атмосфере | разницы нет → камера уже off в геймплее |

---

## 6. Риски и открытые вопросы

1. **Доки недоступны** → рантайм-поведение (`isVisible`-лаг, `forceRenderingOff`+GRD, Lightmap/SkinnedMesh в
   GRD) помечено `[LIKELY]/[UNVERIFIED]`; проверять тестовой сценой.
2. **Сабмеши дверей** взяты из batched-геометрии PRIMUM (аудит C), не из префабов `PRIMUM_DOORS` — расхождение
   возможно; меряется за минуту (material slots у 10 дверей).
3. **Треугольники старого замера не сходятся** с моделью → перемерить перед выводами о fillrate.
4. `m_ShadowCascades: 0` — deprecated; актуально `m_ShadowCascadeCount: 2`. **`ViewDistanceApplier` shadow-пресет
   — no-op** (URP читает ассет) — если заказчик считает иначе, это надо исправить.
5. **GRD:** объём шейдерной работы неизвестен — главный риск сроков; `MaterialPropertyBlock`-места не проверены.
6. **Reflection probes** (Realtime/time-slicing) и **FO-спайк ребейза + GRD** не измерены (второе требует Play Mode).

---

## 7. Источники

**Исходники (версия в точку):** URP 17.5.0 — `UniversalRenderPipeline.cs` (512, 861, 869–1015, 1000, 1028, 1700,
1752–1756), `UniversalRenderer.cs` (11–23, 435–479, 627–674), `ShadowCulling.cs` (19–162),
`UniversalRendererRenderGraph.cs` (686–711, 974–1200), `UniversalRenderPipelineAsset.cs` (596, 1727),
`UniversalCameraData.cs` (259); RP Core 17.5.0 — `GPUDriven/GPUResidentDrawer.cs`, `GPUResidentDrawer.Validator.cs`
(24–69), `IGPUResidentRenderPipeline.cs`, `GPUResidentDrawerTypes.cs`, `Culling/OcclusionCullingCommon.cs` (10–15).

**Сборки (рефлексия):** `...\Editor\Data\Managed\UnityEngine\{CoreModule,UmbraModule}.dll`,
`Library/ScriptAssemblies/Unity.RenderPipelines.{Core,Universal,GPUDriven}.Runtime.dll`. Ключевые типы: `Renderer`,
`Camera`, `CullingGroup`, `LODGroup`, `CullingResults`, `ScriptableRenderContext`, `ScriptableCullingParameters`,
`CullingOptions`, `BatchRendererGroup`, `BatchCullingContext`, `ShadowSplitData`, `GPUResidentDrawer`,
`UniversalCameraData`, `CullContextData`, `OcclusionPortal/OcclusionArea`, `UnityEditor.UnityStats`,
`Unity.Profiling.{ProfilerRecorder, ProfilerRecorderHandle, ProfilerCategory}`.

**Проект:** `ProjectC_URP.asset` (`m_RenderingMode: 2`, `m_ShadowDistance: 1000`, `m_ShadowCascadeCount: 2`,
`m_Cascade2Split: 0.25`, `m_AdditionalLightShadowsSupported: 0`, `m_MainLightShadowmapResolution: 2048`,
`m_UseSRPBatcher: 1`, `m_GPUResidentDrawerMode: 0`, `m_SupportsLightLayers: 0`, `m_MSAA: 1`, `m_SupportsHDR: 0`);
`ProjectC_URP_Renderer.asset` (`m_DepthPrimingMode: 0`, три активные фичи); `QualitySettings.asset` (URP игнорирует);
скрипты `ViewDistanceApplier/ViewDistanceConfig/DetailDistanceCuller/DistantFocusRenderFeature/VolumetricCloudsRenderFeature`;
доки `06_REFERENCE/{04_OCCLUSION_CULLING_RESEARCH,01_INDUSTRY_PRACTICE}.md`, `10_CULLING_RESEARCH/{_BRIEF,01_PROBLEM_STATEMENT}.md`,
`08_DATA/C_primum_batches.csv`.

**Внешние доки:** не использованы (см. §1: `docs.unity3d.com` → redirect на `docs.unity.com`, версионные пути
не резолвятся). URL не выдумывал.

---

## Handoff

- **Главный вывод:** счёт дверей = ~8.2k opaque + 8.2k depth-prepass, тени 1.4–5.7k; самые дешёвые рычаги —
  `layerCullDistances` и `shadowCastingMode` для дальних дверей, без переделки геометрии.
- **Факт 1:** `m_RenderingMode: 2` = **Forward+**; теневые кастеры курятся отдельным `context.CullShadowCasters`
  с per-cascade `ShadowSplitData` (0–250 / 250–1000 м).
- **Факт 2:** depth prepass есть (фичи просят Depth) и удваивает draws; additional-light shadows в ассете выключены.
- **Факт 3:** GPU RD — единственная встроенная FO-совместимая окклюзия (Hi-Z каждый кадр), но требует DOTS-инстансинга
  во всех шейдерах; Umbra несовместим с FO принципиально, не только из-за бага бейка.
- **Не проверено:** точные m/V/S0/S1 (Frame Debugger), объём шейдерной работы под GRD, FO-спайк ребейза в GRD,
  число/режим reflection probes, `forceRenderingOff` в GRD-пути.
