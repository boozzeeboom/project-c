# F. Собственная система culling/visibility: инженерные варианты для Project C

Дата: **2026-10-30**. Автор: сабагент-инженер culling (F). Статус: **draft, research-only**. Область: можно ли написать свой culling лучше готовых ассетов и встроенного Umbra под наши условия (Unity **6000.5.2f1**, URP **17.5.0**, D3D12, Floating Origin, запрет static-флагов, PRIMUM + двери + корабли), что реалистично и где kill criteria. Правок в `Assets/`, `ProjectSettings/`, `Packages/`, сценах не было; Play Mode не запускался; эксперименты — **план**. Пометки: `[VERIFIED]` — исходник пакета/рефлексия/файл проекта; `[LIKELY]` — косвенные данные; `[UNVERIFIED]` — оценка.

**Двери — по аудиту C (`C_doors.csv`, 30.10):** 2 598 инстансов, 5 493 рендерера, **13 471 material slot**,
**5.2 слота/инстанс**; старое «2 363 × 2 рендерера» из брифа не использовать (занижает смету в ~2.6 раза).

---

## 0. TL;DR для Lead

1. **Двери доминируют:** 26 981 DC, двери **17 766–22 042 (66–82 %)**, при этом <1 млн тр. Модель
   `draws ≈ Σ видимых дверей [5.2 слота × (opaque + каскады)]` ≈ 20.8 сабмита/дверь; 17 766 DC в 250/1 ↔
   ≈1 700–1 800 видимых дверей — сходится.
2. **Дешёвый рычаг №1 — дистанция/тени дверей, а не окклюзия:** 1 687 дверей/км²; r=100 м → ~53 инстанса
   (276 слотов), 150 м → ~119 (619), 200 м → ~212 (1 102) против 13 471 слота; дверь на 150–200 м — единицы
   пикселей. `[LIKELY, нужен A/B]`
3. **Umbra-on-proxies — тупик для FO:** публичного runtime-типа `OcclusionCullingData` в 6000.5.2f1 **нет**
   (reflect + docs-lookup 0), бейк только editor, данные в мировых координатах, API сдвига нет, static-флаги
   запрещены FO, террейн в бейк не попадает, 6 бейков падали.
4. **GRD — рекомендованный путь №1:** API на месте, ререндер уже **Forward+** (`m_RenderingMode: 2` =
   `ForwardPlus`), включаем `m_GPUResidentDrawerMode` + occlusion; в `01_INDUSTRY_PRACTICE` §8 записан откат на
   Forward — сверить историю ассета.
5. **DOTS-блокер узкий:** 12 из 143 `.mat` на 8 кастомных шейдерах (4 — `ProceduralSteel`), и в
   `WorldScene_0_0.unity` **0 ссылок** на его материалы; город/двери — URP-шейдеры 17.5.
6. **GPU-окклюзия URP 17.5 уже есть и публична** (`GPUResidentDrawer.InstanceOcclusionTest` /
   `UpdateInstanceOccluders`, Hi-Z, двухфазный тест) — свою Hi-Z для GRD писать не надо.
7. **Двери при GRD — instancing:** 13 471 слот на 11 префабах → **≈72 (≤72) пары mesh×material** на камеру —
   самый большой единичный выигрыш; цена — shader-путь + проверка анимации створок.
8. **GRD даёт** `smallMeshScreenPercentage` (мелочь/двери), `supportDitheringCrossFade`, LOD, lightmaps;
   тени — через `BatchCullingViewType.Light`, но URP-shadow pass идёт `CreateShadowRendererList` → проверить E3.
9. **CPU VisibilityDirector:** MVP 5–8 дней, прод 3–4 недели, **26 981 → 4 500–6 500**; frustum Unity уже делает
   сам — ценность в distance/shadow-кастерах, управлении и заделе под окклюзию.
10. **Свой GPU-driven BRG:** 6–10 недель, высокий риск, не лучше GRD; оправдан только для контента вне GRD
    (атмосфера, корабли) или своей окклюзии вью.
11. **Portal/PVS:** максимум эффекта (**500–1 200 DC**), но 6–10 недель + rebake при правках города; FO-safe
    (данные в локальных координатах корня/ячейки).
12. **Low-code 2–4 дня:** `layerCullDistances` + `shadowCastingMode=Off` дальним + `CullingGroup` →
    **~5 000–6 000**; фазовый путь Ф0→Ф3; FO зелёный у вариантов 1–5, красный у 6; мультикамера требует union.

---

## 1. Вопросы и метод

Вопросы: цена CPU-директора на 1 207 / 20 000 / 147 000 рендереров; что даёт свой BRG и что уже есть в Unity; гибрид GRD + свой слой (keep merged vs unmerge); реален ли Portal/PVS по 10 817 боксам и реестрам дверей (FO-safe, поддержка); потолок low-code; переживёт ли Umbra-on-proxies FO.

Метод: прочитаны `_BRIEF.md`, `04_OCCLUSION_CULLING_RESEARCH.md`, `01_INDUSTRY_PRACTICE.md`, `ROADMAP.md`,
`floatingorigin/README.md`, URP-ассеты, `manifest/packages-lock`, `TagManager.asset`,
`DetailDistanceCuller.cs`, `DistantFocusRenderFeature.cs`; API — read-only `unity_reflect` на 6000.5.2f1 +
исходники `Library/PackageCache`. Числа: `04…` §3 (конфиг A), `ROADMAP` §2.1, ground truth MCP `scene_stats`
30.10 (8 863 MeshRenderer / 8 861 MeshFilter / 6 838 859 тр. / 2 630 DoorController / 2 238 Light / 18 358
коллайдеров) + аудит C. В сцене **2 076 static GameObject без MeshRenderer** → static batching батчить нечего
(при `m_StaticBatching=1`). Ограничения: замеров под изменениями нет (правки/Play Mode запрещены);
`web_search` и `docs.unity3d.com` недоступны → Manual не цитируется, API подтверждён исходниками пакетов.

---

## 2. Находки

### 2.0 Базовая модель кадра

| Компонент (04 §3, конфиг A: shadow 1000/2 каскада, far 1000, fov 60) | draw calls | setPass | тр., млн |
|---|---|---|---|
| Кадр | 26 981–26 996 | 232 | 15.86–16.57 |
| Двери выкл | 4 954 | 195 | 15.68 |
| **Вклад дверей** | **22 027–22 042 (81.6 %)** | 37 | 0.18–0.89 |

Город+мир (двери off): 4 954 DC на ~1 900 из 8 863 рендереров (в фрустуме из центра ~1/3 сцены, ×~2.5 прохода).
**Масштаб:** двери = 13 471 слот против 1 806 слотов города (`ROADMAP` §2.1) — **×7.5**. Бюджет «≤120 DC L0»
недостижим без HLOD/импостора; реалистичная цель — **≤2–3k** L0, ≤300–500 дальних LOD.

### 2.1 Вариант 1 — CPU VisibilityDirector (ячейки + Jobs/Burst)

```
WorldRoot_0_0 ── PRIMUM (23 чанка) ── улица ── дом ── группа дверей ── дверь (5.2 слота)
      │                └ CellNode{bounds, children[], renderers[], maxDrawDistance, shadowDistance}
      └ PRIMUM_DOORS ── chunk ── houseGroup ── instance
MainCamera ─┐
WorldCamera (far 1e6) ─┼─► UNION видимости (state рендерера глобальный!)
Atmosphere (120k, выкл) ┘
```

Данные — плоские массивы: `Node{Bounds localBounds; int firstChild/childCount/firstRenderer/rendererCount;
float maxDrawDistance, shadowDistance; byte state, wantState;}`, `NativeArray<Bounds> _worldBounds` (пересчёт при
ребейзе/стриминге), `NativeArray<byte> _visible`, managed `Renderer[]`. Flow: `[LateUpdate]` плоскости камер × N
активных → `IJobParallelFor` (distance → frustum → опц. occluded-битмаска) → гистерезис 10–15 % → применить
`Renderer.enabled` (+`shadowCastingMode=Off` при cull). `[Render]` Unity culls включённые. `[FO-ребейз]`
пересчитать `_worldBounds` (1 кадр допускается).

API `[VERIFIED]`: `Renderer.enabled`; `Renderer.forceRenderingOff`; `Renderer.shadowCastingMode` +
`ShadowCastingMode{Off,On,ShadowsOnly,TwoSided}`; `Camera.layerCullDistances` (`float[32]`),
`layerCullSpherical`; `CullingGroup` (`SetBoundingSpheres`, `SetBoundingDistances`, `SetDistanceReferencePoint`,
`onStateChanged`, `targetCamera`, `IsVisible`). Jobs/Burst — пакеты **уже в PackageCache** транзитивно: burst
**1.8.29**, collections **6.5.0**, mathematics **1.4.0** `[VERIFIED]`; прямых зависимостей в `manifest.json` нет
→ обычно хватает autoReferenced, иначе тикет на manifest.

Стоимость `[UNVERIFIED→LIKELY]` (≈30 флоп/AABB-тест; 2–4 нс/рендерер Burst/Jobs; 40–80 нс managed):
1 207 → 3–8 мкс / 0.05–0.1 мс; 8 863 → 25–45 мкс / 0.35–0.7 мс; 20 000 → 60–100 мкс / 0.8–1.6 мс;
147 000 → 0.4–0.7 мс / 6–12 мс; toggle ≈0.1–0.3 мкс/шт (200–400/кадр ≈ 30–120 мкс). GC 0 при `NativeArray`;
память 20k ≈ 2–3 МБ, 147k ≈ 15–20 МБ.

```csharp
[BurstCompile] struct FrustumCullJob : IJobParallelFor { /* planes, camLocal, nodes → _visible */ }
void LateUpdate() {
    var planes = GeometryUtility.CalculateFrustumPlanes(_mainCam);
    new FrustumCullJob{ planes = planes.ToNativeArray(), cam = ToRootLocal(_mainCam), nodes = _nodes,
                        visible = _visible }.Schedule(_nodes.Length, 64).Complete();
    for (int i = 0; i < _renderers.Length; i++) {                 // гистерезис + применение
        byte want = _visible[i]; if (want == _applied[i]) continue;
        _applied[i] = want; var r = _renderers[i]; r.enabled = want == 0;
        if (want == 0) r.shadowCastingMode = ShadowCastingMode.Off; }
}
```

Что даёт `[LIKELY]`: frustum Unity делает сам, ценность — distance/shadow-кастеры/группы. Двери: shadow off
(13 471 слот → opaque-only ~1 637 видимых инстансов × 5.2 ≈ 8 500), затем distance 120–150 м → ~60–120
инстансов ≈ 300–620 DC. Город: distance-мелочь/интерьеры −20…−30 % от 4 954; `RuinsValleys`/фермы через
`CullingGroup` −200…−400. **Итог: 26 981 → 4 500–6 500 (−76…−83 %)**, CPU 0.05–0.3 мс, GC 0; без окклюзии в
улице остаётся ~2.5–4k.

Трудозатраты: MVP **5–8 дней**; продакшн (ячейки/дома/группы, мультикамера-union, гистерезис, ребейз, бюджеты,
Editor-диагностика) **15–20 дней**. Риски: toggle перестраивает internal renderer list (мерить); мультикамера →
union «держит всё»; `OnBecameVisible/Invisible` чужих скриптов начнут срабатывать; 1 кадр неверной видимости
после ребейза. **Kill:** culling+apply > 0.7 мс/кадр → урезать гранулярность; артефакты не лечатся за 2 дня →
стоп; экономия < 30 % → не поддерживать. За 1–2 дня: `VisibilityDirectorLite` (~200 строк) на группах-детях
`PRIMUM` (23) и `PRIMUM_DOORS` (21), frustum+distance по bounds, счётчики; сначала без Burst.

### 2.2 Вариант 2 — собственный GPU-driven BRG-пайплайн

```
[CPU кадр N]                                   [GPU кадр N / N+1]
CullingJob → BatchCullingOutput                     ┌ URP depth prepass → Hi-Z pyramid
  ├ BRG.AddBatch(MetadataValue[])                   │      │
  ├ per-view Camera/Light (BatchCullingViewType)    ▼      ▼
  └ drawCommands/visibleInstances ───────────► indirect (RenderMeshIndirect / BRG draw)
                                     кадр N+1: тест против Hi-Z кадра N (1 кадр лага)
```

API `[VERIFIED]`: `BatchRendererGroup` — `AddBatch(NativeArray<MetadataValue>, GraphicsBufferHandle) → BatchID`,
`RemoveBatch`, `SetBatchBuffer`, `SetEnabledViewTypes(BatchCullingViewType[])`, `RegisterMesh/RegisterMaterial`,
`GetThreadedBatchContext`, `SetGlobalBounds`; создание — `new BatchRendererGroup(new BatchRendererGroupCreateInfo{
cullingCallback = OnPerformCulling, ... })` (`InstanceCullingBatcher.cs:76-81`), колбэк `JobHandle
OnPerformCulling(BatchRendererGroup, BatchCullingContext, BatchCullingOutput, IntPtr)` (`:326`).
`BatchCullingContext`: `cullingPlanes`, `cullingSplits`, `localToWorldMatrix`, `lodParameters`,
`receiverPlaneCount/Offset`, `viewID`, `viewType`; `BatchCullingOutputDrawCommands`: `drawCommands`,
`indirectDrawCommands`, `visibleInstances`; `BatchDrawCommand`: `batchID`, `meshID`, `materialID`, `submeshIndex`,
`visibleOffset/Count`, `flags`, `lightmapIndex`, `activeMeshLod`; `BatchCullingViewType`: Camera, Light, Picking,
SelectionOutline, Filtering. Indirect: `Graphics.RenderMeshIndirect/RenderMeshInstanced/DrawMeshInstancedIndirect`;
своя Hi-Z не нужна (`GPUResidentDrawer.InstanceOcclusionTest`), ядро `CreateFarDepthPyramid` — internal.

Данные: `MetadataValue[]` (ObjectToWorld/WorldToObject/custom), пулы `NativeArray<BatchDrawCommand>` и
`visibleInstances` (`Allocator.TempJob`), буфер indirect-аргументов; состояние per-view — по
`BatchCullingViewType`/`viewID`.

```csharp
var brg = new BatchRendererGroup(new BatchRendererGroupCreateInfo{ cullingCallback = OnPerformCulling });
var batch = brg.AddBatch(metadata, instanceBuffer.bufferHandle);
JobHandle OnPerformCulling(BatchRendererGroup g, BatchCullingContext ctx, BatchCullingOutput outC, IntPtr _)
    => new CullJob{ ctx = ctx, output = outC, instances = _instances }.Schedule();
```
Трудозатраты: MVP **10–15 дней**; продакшн **30–50 дней**. Даёт как GRD
(~1 500–3 000 команд), но не лучше его, пока не нужна своя логика (окклюзия вью вне URP, корабли/атмосфера).
Риски: DOTS-шейдеры нужны; синхронизация indirect/depth; конфликты с RendererFeature; двойная поддержка.
**Kill:** MVP за 10 дней не даёт ≤3k и/или ломает картинку → закрыть, включить GRD. За 1–2 дня: один BRG-батч
1 000 кубов с frustum-culling + indirect после depth prepass.

### 2.3 Вариант 3 — гибрид: встроенный GRD + наш слой

```
URP 17.5, Forward+ (m_RenderingMode: 2 [VERIFIED])
 ├ GRD: m_GPUResidentDrawerMode = InstancedDrawing  ← включаем
 │   ├ регистрация MeshRenderer'ов (GPUDrivenProcessor); GPU culling frustum/LOD/smallMesh%+Hi-Z
 │   └ indirect draws (Hybrid Batch Group в Frame Debugger)
 ├ наш слой: ячейки/двери/тени/лампы + контент ВНЕ GRD
 └ shadow passes: CreateShadowRendererList + CPU culling ← проверить покрытие GRD
```

Состояние (ассеты 30.10) `[VERIFIED]`: `m_RenderingMode: 2` = ForwardPlus (`UniversalRenderer.cs:11-23`);
`m_GPUResidentDrawerMode: 0`; `m_SmallMeshScreenPercentage: 0`; occlusion culling 0 (`ProjectC_URP.asset:86-88`);
SRP Batcher 1, Dynamic Batching 0; тени 1000/2 каскада, `m_AnyShadowsSupported: 1`, доп. свет-тени 0;
`m_StaticBatching=1`, но 2 076 static GameObject без MeshRenderer. Блокер шейдеров: 143 `.mat`, **12** на 8
кастомных шейдерах (ProceduralSteel 4, Veil 2, VeilRaymarch 2, прочие по 1) `[VERIFIED, скан]`;
`ProceduralSteel` — шейдер ошибки 18.10 (`unity_ObjectToWorld` стр. 233), но в `WorldScene_0_0.unity` **0
ссылок** на его материалы `[VERIFIED, скан GUID 60 МБ]`; остальные — атмосфера/скай. Город/двери — URP-шейдеры
17.5, `DOTS_INSTANCING_ON` поддерживают.

GRD покрывает `[VERIFIED code]`: `GPUDrivenProcessor` (`EnableGPUDrivenRenderingAndDispatchRendererData`,
`DisableGPUDrivenRendering`, `ClassifyMaterials`, `RegisterMaterials/RegisterMeshes`, `DispatchLODGroupData`),
GPU-frustum/LOD, GPU-окклюзия (Hi-Z), `smallMeshScreenPercentage`, dithering cross-fade, lightmaps; тени —
`BatchCullingViewType.Light` + receiver-плоскости (`FrustumPlanes.cs:79`, `InstanceCuller.cs:354-380`), фикс
CullingMask shadow casters (`CHANGELOG.md:90`). **Наш слой:** `DoorController`, стриминг/ячейки, бюджеты
(интерьеры/OUTLIERS), `AtmosphereCamera`, корабли/палубы, лампы, fallback не-GRD контента.

| Схема | Рендереров | Уник. mesh×material | Команд/камеру | Гранулярность окклюзии |
|---|---|---|---|---|
| Сейчас: город 761 батч + 446 прочих | 1 207 | ~1 207 | ~1 207 × доля | плохая (батч = чанк, 22–104 объекта) |
| Разъединить на дом | ~19 944 / ~26 566 слотов | ~19 944 | ×21 к текущему | хорошая, команд в разы больше |
| Компромисс: батч × подквадрат 2×2 | ~3 000 | ~3 000 | ×2.5 | средняя |
| **Двери (11 префабов)** | 5 493 / **13 471 слот** | **≈72 (≤72)** | **≈72** | отличная (per-instance) |

```csharp
// URP Asset: GPU Resident Drawer = Instanced Drawing; GPU Occlusion Culling = on
var visible = _visibilityDirector.Compute(camLocalPos);   // наш слой: ячейки/двери/тени
ApplyToNonGrdContent(visible);                            // GRD-контент куллит engine на GPU
```
Разъединять имеет смысл **только двери** (они уже инстансы префабов — GRD инстансит по mesh+material: 13 471
слот → ≈72 команды). Город не разъединять: рост команд перевесит выигрыш окклюзии; если GPU-окклюзия города
слабая — делить батчи на подквадраты (Э4-совместимо). Ожидание `[LIKELY]`: двери ≈150–300 команд (камера+тени)
вместо ~22 000 DC; город ~450–700 видимых команд (frustum+GPU-окклюзия 40–60 %) × ~2.5 прохода ≈ 1 100–1 750;
фермы/руины/прочее 50–200. **Итого 26 981 → ~1 500–2 500 (−91…−94 %)**; добавляется обязательный depth texture
(`requiresDepthTexture |= useGPUOcclusionCulling`, `UniversalRenderPipeline.cs:1753-1756` `[VERIFIED]`).

Трудозатраты: аудит/правка 8 шейдеров **2–5 дней** (только ProceduralSteel — 0.5–1 день); включение/настройка **1–2 дня**; проверка/профилирование **3–5 дней** → **2–3 недели**. Риски `[UNVERIFIED]`: (а) вращение створок под GRD (E-открытие; fallback `DisableGPUDrivenRendering`); (б) тени дверей/домов; (в) артефакты кастомных шейдеров при `BRG Variants = Keep All`; (г) SRP Batcher; (д) 1 кадр после F8. **Kill:** shader-ошибки/визуал не закрываются за **5 дней** → откат; двери не анимируются без исключения из GRD → двери CPU-путём; `Hybrid Batch Group` не появляется → стоп. За 1–2 дня: read-only аудит материалов, затем с одобрения включить GRD, снять Frame Debugger/`UnityStats`, откатить.

### 2.4 Вариант 4 — Portal/PVS для города

```
[город = сетка ячеек ~15×15 м]                  [данные]
 cell(cx,cz) ── portal(дверь/проём/улица) ── cell'    PVS: bitset[cells][visibleCells]
      └ renderers[] (локальные координаты PRIMUM)     в Assets, координаты корня (FO-safe)
[рантайм] camPosWorld → local = world - root.position → PVS → включить рендереры видимых ячеек
```

Для авторинга есть: 10 817 коллайдеров (`WALL/FLOOR/ROOF/STAIR`), 2 598 инстансов дверей + реестры
`03_DOORS/registries/`, 27 чанк-узлов; окна дверей уже вырезаны в боксах стен → **порталы из проёмов почти
бесплатны**; «улицы/переулки» — граф проходимости по зазорам (сложнее). PVS без Umbra: offline скрипт (2–4
высоты на ячейку, лучи к центрам кандидатов через `Physics.Raycast` по 10 817 боксам; технология проверена меню
363/364) → bitset; ~1 500 ячеек × 64 луча ≈ минуты на прогон; rebake при изменении города. Данные — в локальных
координатах корня (как боксы: «внутрь префаба не кладём — оттуда всё теряется»); при ребейзе пересчитывается
камера. Ожидание: видимых ячеек с улицы 3–10 % → город ~60–200 команд, двери ~30–150 → **~500–1 200 DC с
тенями**; запрос PVS 1–3 мкс.

```csharp
int cell = CellOf(camLocalPos);
BitSet vis = _pvs[cell];                       // запечено offline, локальные координаты корня
for (int i = 0; i < _cellRenderers.Length; i++) _cellRenderers[i].enabled = vis[i];
```

Трудозатраты: авто-порталы/ячейки 5–8 дней; бейкер+верификация 5–8; рантайм 5–7;
тулинг/rebake 5–8 → **6–10 недель**. Риски: устаревание PVS при правках (главный); ложные «дыры» (луч в центр
двери упирается в стену — как в прокси-оценке 04 §5); открытые двери → избыточная видимость (лечится слиянием
видимости ячеек). **Kill:** бейк > 2 ч/прогон → упрощать; видимых ячеек > 35 % с уличной точки → не брать;
город меняется чаще раза в 2 недели → PVS не поддерживается. За 1–2 дня: PVS одного чанка (`S9_TRD-S2`, 104
рендерера) по боксам → доля видимых ячеек (E6).

### 2.5 Вариант 5 — low-code стек

```
Camera.layerCullDistances[doorLayer] = 120 ─┐
Camera.layerCullDistances[detailLayer] = 60 ├─► Unity режет по дистанции без кода
CullingGroup(targetCamera = Main) ──────────┘   сферы/band'ы/onStateChanged
Renderer.shadowCastingMode = Off (дальние группы, двери); LODGroup — позже (сейчас 0, нужен Э6)
```

`layerCullDistances` (`float[32]`), `layerCullSpherical`, `CullingGroup`, `LODGroup` — все `[VERIFIED]` reflect;
0 `LODGroup` в проекте `[ROADMAP §2]`. Слои: `Default`, `PRIMUM` (8), `PCMM_Atmosphere` (9), `ShipDeck` (6),
`ZoneCollider`, `Water`, `UI` → слотов много. Двери и город оба на `PRIMUM` → нужен слой (напр. 10
`PRIMUM_DOORS`) и перенос 11 префабов `PM_DOOR_*` (инстансы наследуют слой → ~11 правок, не 2 598).
Ограничения: 32 слоя/32 дистанции (одна на слой на камеру); `layerCullDistances` **не влияет на тени** → нужен
`shadowCastingMode`; `CullingGroup` требует обновления сфер при движении (только ребейз) и не отсекает по
фрустуму; LODGroup без LOD-мешей ничего не даёт.

Что даёт `[LIKELY]`: двери `shadowCastingMode=Off` → 22 042 →
~8 500 (opaque-only, 1 637 инстансов × 5.2); слой + `layerCullDistances=120` → ~300–620; `CullingGroup` для
`RuinsValleys`/мелочи −200…−400. **Итого 26 981 → ~5 000–6 000 (−78…−81 %)**; без «тени дверям off» ~19–20k.

```csharp
_cam.layerCullDistances[doorLayer] = 120f;     // Unity режет по дистанции сам
_group.SetBoundingSpheres(_spheres);           // CullingGroup: видимость/дистанции
r.shadowCastingMode = ShadowCastingMode.Off;   // дальним группам
```
Трудозатраты: код 100–200 строк **1–2 дня**; правка слоёв/префабов/сцены **1–2 дня**; A/B **1 день** →
**2–4 дня**. Риски: потеря теней дверей (per-object feature, **не** shadowDistance/каскады); резкая граница
дистанции (гистерезиса нет); перенос слоя затрагивает физику/raycast. **Kill:** тени заметны → оставить,
выигрыш перенести на GRD; перенос слоя ломает физику/сеть → пересмотреть. За 1–2 дня: `LayerDistanceApplier` +
Editor-кнопка + A/B по `UnityStats`.

### 2.6 Вариант 6 — Umbra-on-proxies: тупик для FO

Идея: локальная сцена (origin 0) с боксами домов (10 817) как окклюдерами, запечённая окклюзия, реальная сцена «спрашивает» видимость. Факты: (1) бейк только editor `UnityEditor.StaticOcclusionCulling` `[VERIFIED, 04 §11]`; (2) публичного runtime-типа данных окклюзии **нет** — reflect «Occlusion» даёт `OcclusionArea`, `OcclusionPortal`, `LightProbeOcclusion`, editor-`StaticOcclusionCulling`, URP-`OcclusionCullingSettings`, но **`OcclusionCullingData` отсутствует**, docs-lookup 0 `[VERIFIED 30.10]` → **нет API** подменить/сдвинуть/назначить данные сцене; (3) `OcclusionPortal` умеет только `open`; (4) данные живут в сцене и запечены в мировых координатах; (5) static-флаги обязательны, правило FO их запрещает; (6) `Terrain_0_0` без Renderer → в окклюзию не попадает; (7) 6 бейков падали (`Failure in split phase`). Следствие: **не переживёт FO** — запрос идёт к данным **своей** сцены по позиции камеры. **Статус: мёртвая ветка.** Спасаема только идея offline-oracle (вариант 4), но у Umbra нет экспорта формата — практичнее свой raycast-бейкер (доказательство — 1–2 дня: сцена 20 боксов → бейк → читать нечем).

---

## 3. Матрица вариантов

| # | Вариант | Draw calls (ожид.) | Срок | Риск | FO | Поддержка | 1–2 дня |
|---|---|---|---|---|---|---|---|
| 1 | CPU VisibilityDirector | 4 500–6 500 (−76…−83 %) | MVP 5–8 дн; прод 3–4 нед | средний | 🟢 | средняя | да |
| 2 | Свой GPU-driven BRG | 1 500–3 000 | 6–10 нед | высокий | 🟢 | высокая | да |
| 3 | **GRD + свой слой** | **1 500–2 500 (−91…−94 %)** | 2–3 нед | средний/низкий | 🟢 | низкая | да |
| 4 | Portal/PVS | 500–1 200 | 6–10 нед | высокий (rebake) | 🟢 | высокая | да (1 чанк) |
| 5 | Low-code стек | 5 000–6 000 (−78…−81 %) | 2–4 дн | низкий | 🟢 | низкая | да |
| 6 | Umbra-on-proxies | — | — | — | 🔴 | — | 1–2 дня «доказать» |

Оценки от базы 26 981 (конфиг A) — **прогнозы** по модели §2.0; замеры — §5.

---

## 4. Что это значит для Project C

**PRIMUM:** дистанция не лечит (дома 10–20 м видны с 1 км) → frustum + окклюзия (GRD/PVS); батчи 761/1 806
слотов дают плохую гранулярность окклюзии, при слабом эффекте — деление на подквадраты (Э4-совместимо).
**Двери:** 13 471 слот против 1 806 у города (×7.5) — два рычага: дистанция/тени (2–4 дня) и GRD-instancing
(≈72 команды); делать оба; рама двери статична — её можно вернуть в батч дома. **Корабли:** динамика —
GRD-инстансы (если DOTS) + CPU-директор по группам/палубам + LOD; PVS не подходит. **FO:** варианты 1–5
FO-safe (локальные координаты, static не нужен, хук только на кэши); прецедент — `DetailDistanceCuller`.
**Стриминг:** внутригородские ячейки строятся при загрузке сцены, ребейз их не трогает. **Мультикамера:**
union только «настоящих» камер (`SpringArm/WorldCamera` far 1e6 и `AtmosphereCamera` 120k не включать);
дистанции — per-camera `layerCullDistances`; окклюзия GRD per-camera сама. **Бюджет:** «≤120 DC L0» недостижим
(даже GRD+PVS ~500–1 200): предложение — L0 ≤ 2–3k, L1 ≤ 500, L2 ≤ 10, привязать к Э5/Э6.
---

## 5. Рекомендованные эксперименты

С одобрения, **вне research-режима**, замеры — пользователь в Play Mode. Метрики: `UnityStats.
drawCalls/setPassCalls/triangles` на равном `frameCount` (FPS первых ~1500 кадров врёт, 04 §2),
Profiler→Rendering, Frame Debugger (Camera / Directional Shadow Map).

| ID | Что | Как | Ожидание | Kill |
|---|---|---|---|---|
| E0 | Split дверей по проходам | Frame Debugger из центра: Camera vs 2 каскада | подтвердить 5.2 слота × проходы | теней <30 % → приоритеты иные |
| E1 | Двери: shadow off + слой + `layerCullDistances=120` | 11 префабов → слой; A/B на 3 точках (улица/площадь/крыша) | 26 981 → 5 000–6 000 | <30 % экономии/артефакты → стоп |
| E2 | DOTS-аудит 8 шейдеров | компиляция с `DOTS_INSTANCING_ON`; затем GRD + `Keep All` | 0 ошибок или ≤2 дней правок | >5 дней правок → откат |
| E3 | GRD + GPU occlusion + `smallMeshScreenPercentage` 1–2 % | включить, Frame Debugger (`Hybrid Batch Group`), 3 точки | 26 981 → 1 500–2 500 | нет Hybrid Batch Group/створки/тени → стоп |
| E4 | Unmerge 1 чанка | `S9_TRD-S2` (104) → per-house; команды/окклюзия | нужна ли нарезка батчей | ×3 команд без выигрыша → нет |
| E5 | CPU-директор на всей сцене | lite-скрипт; Profiler: culling+apply, draw calls | ≤0.3 мс, −40…−60 % | >0.7 мс/артефакты → урезать гранулярность |
| E6 | PVS 1 чанка | raycast-бейк по боксам, доля видимых ячеек | <35 % видимых | >35 % или >2 ч/чанк → не брать |
| E7 | FO после F8/F9 | выбранный вариант: город на месте, 0 errors | — | 1 кадр «пустого мира» → доработать хук |

Точки: центр `(39593.1, 2509.6, 39833.8)` yaw 337.5° + 2 уличные + 1 с крыши; `AtmosphereCamera` выкл.

---

## 6. Риски и открытые вопросы

1. Все цифры «после» — прогноз; реальные только после E0–E5. 2. **Тени под GRD:** URP shadow pass строит
`RendererList` CPU-путём (`MainLightShadowCasterPass.cs:365-376`); попадают ли GRD-инстансы в shadow map — E3.
3. **Створки дверей под GRD** — главный `[UNVERIFIED]`; fallback `DisableGPUDrivenRendering`. 4. **Forward+ уже
включён** (вопреки `01_INDUSTRY_PRACTICE` §8) — сверить историю ассета. 5. Перенос дверей на новый слой
затрагивает физику/raycast/возможно сеть. 6. `layerCullDistances` и тени `[LIKELY]` не влияют — E1. 7. Прямых
зависимостей Burst/Collections/Mathematics в manifest нет — `[UNVERIFIED]` хватает ли autoReferenced. 8. PVS
при живом изменении города — главный организационный риск варианта 4. 9. `OcclusionCullingData` отсутствует
как публичный тип `[VERIFIED]` → данные нельзя читать/двигать из рантайма; ветка 6 закрыта.
---

## 7. Источники

**Проект:** `_BRIEF.md`; `04_OCCLUSION_CULLING_RESEARCH.md` (§2–§12); `01_INDUSTRY_PRACTICE.md` (§7–§8);
`ROADMAP.md` (§1–4, §2.1); `floatingorigin/README.md`; `03_DOORS/02…`, `03_MASS_PLACEMENT.md`;
`ProjectC_URP.asset:50-94`, `ProjectC_URP_Renderer.asset:189`; `TagManager.asset`; `manifest.json`,
`packages-lock.json`; `Shaders/*.shader`+`.meta`, `Materials/**.mat`; `DetailDistanceCuller.cs`,
`DistantFocusRenderFeature.cs`. **Ground truth 30.10:** MCP `scene_stats` (8 863 MR / 8 861 MF / 6 838 859 тр. /
2 630 DoorController / 2 238 Light / 18 358 коллайдеров) + аудит C (`C_doors.csv`).

**Пакеты (URP 17.5):** `core@9fe9377312a8/Runtime/GPUDriven/` (`GPUResidentDrawer.cs`,
`GPUResidentDrawerTypes.cs`, `Batching/InstanceCullingBatcher.cs:76-81,326`, `Culling/InstanceCuller.cs:354-380`,
`Utilities/FrustumPlanes.cs:79`, `Culling/InstanceOcclusionCuller.cs:389`); `universal@748d7458cc62/Runtime/`
(`UniversalRenderer.cs:11-23`, `UniversalRendererRenderGraph.cs:689-711,781-836,984-1003,1027-1080,1111-1209`,
`UniversalRenderPipeline.cs:1753-1756`, `MainLightShadowCasterPass.cs:365-376`, `CHANGELOG.md:90`); burst 1.8.29 /
collections 6.5.0 / mathematics 1.4.0. **Недоступно:** `web_search` (нет ключа), `docs.unity3d.com` (редирект) → Manual не цитируется; API подтверждён
исходниками пакетов.

---

## 8. Приложение

```
draws ≈ Σ_views Σ_visible renderers slots            (SRP Batcher не сливает вызовы)
двери: 2 598 инстансов × 5.2 слота = 13 471; плотность 1 687/км²; r=100 м → 276 слотов, 150 → 619, 200 → 1 102
проверка модели: 5.2 × 2 × 1 700 ≈ 17 680 ≈ измеренные 17 766; фрустум города из центра ≈ 63 % (758/1 207)
```

## Handoff

**Главный вывод:** «своя система culling вместо всего» не нужна; 90 % эффекта даёт связка «low-code дистанция/
тени дверей (2–4 дня) → встроенный GRD + GPU occlusion (2–3 недели)», а собственный VisibilityDirector/PVS
оправдан как слой управления поверх (мультикамера, корабли, бюджеты) или если нужен бюджет <1k draw calls.

**3 главных факта.** (1) Двери = 13 471 material slot (5.2 на инстанс) против 1 806 у города — доминирующий
источник сабмитов (~22k DC); (2) в 6000.5.2f1 нет публичного runtime-типа/API данных окклюзии →
Umbra-on-proxies для FO — тупик; (3) URP 17.5 уже содержит публичный API GPU-окклюзии и GRD, ререндер уже
Forward+, блокер — только 8 кастомных шейдеров, материалы которых в сцене города не используются.

**Не проверено:** split дверей по проходам (E0); тени и анимация створок под GRD (E3); хватает ли
autoReferenced-режима Burst для `[BurstCompile]` в Assembly-CSharp; реальная доля видимых ячеек PVS (E6);
история `m_RenderingMode` в URP-ассете (сейчас Forward+).
