# 01. Наша оптимизация города PRIMUM: рендеринг, куллинг, Floating Origin

> Дата: 2026-10-30. Источник: исследование кодовой базы Project C (Assets/_Project/Scripts, docs/world/*). Код не менялся. Режим: research-only.

## 0. TL;DR

- Город PRIMUM после Э4: **1 207 рендереров** (761 батч по материалу + 446 прочих), ~5.44 млн тр., 1 806 слотов; до join — 147 715 объектов / 4.49 млн тр.
- Бюджет draw calls L0 — **≤ 120** (DoD), фактически **26 981–26 996** (shadow 1000 / 2 каскада, двери вкл). Зазор ~225×. Главный рычаг — Э5 (GPU-путь) + Э6 (LOD/HLOD/импосторы), оба ещё не начаты.
- **GPU Resident Drawer выключен** (`m_GPUResidentDrawerMode: 0`), SRP Batcher вкл, Static Batching off. Откат 18.10 из-за кастомных шейдеров без `DOTS_INSTANCING_ON`.
- **Umbra окклюжн-бейк провален** (6 попыток, `Failure in split phase`), данные не созданы; статик-флаги = 0 по правилу FO. Террейн не даёт Renderer → невидим для Umbra.
- **0 `LODGroup`** в сцене. Дальность управляется `ViewDistanceApplier` (far 30K/60K/120K, shadow 500/1500/4000, pixelError 300/200/50, basemap 12K/20K/80K, fogScale 1.3/1.0/0.6) + `DetailDistanceCuller` (RuinsValleys целыми хамлетами) + `PrimumLightPool` (≤96 источников).
- **Floating Origin запрещает статику** → culling обязан быть динамическим (frustum/distance/GPU-occlusion), а не bake. Контент под корнями `WorldScene_*` едет бесплатно.
- Оптимизация города: **Э1, Э2, Э3, Э3.1, Э4, Э8 закрыты**; **Э5 (GPU-путь) и Э6 (LOD/HLOD) — следующие**; **Э7 (интерьеры префабами) отменён** решением заказчика.

## 1. Архитектура рендеринга города (PRIMUM и окружение)

### 1.1 Сцена `WorldScene_0_0` (фокус, ~62.9 МБ)

| Показатель | Значение |
|---|---|
| Рендереров всего (после ферм, 30.10 вечер) | ~7 152 |
| Треугольников (уникальные меши) | ~6.69 млн |
| Узлов | ~19 754 |
| Материалов (различных) | 92 |
| Слотов материалов | 15 403 |
| До Э8 (24 670 рендереров) → после снятия легаси `Primum` | 18 447 |
| До Э4 (23.10) | 66 175 трансформов / 52 054 рендерера / 7.73 млн тр. / 818 материалов |

### 1.2 Город `PRIMUM` (инстанс `PRIMUM_CITY.prefab` под `WorldRoot_0_0`)

| Показатель | Значение |
|---|---|
| Рендереров (после Э4) | 1 207 = 761 батч по материалу + 446 прочих |
| Слотов материалов | 1 806 |
| Треугольников | ~5.44 млн |
| До Э4 | 19 944 рендерера, 26 566 слотов, 5.44 млн тр. |
| До join (исходник) | 147 715 объектов, 4.49 млн тр. (версия 18 чанков — старые цифры) |
| Мешей на чанк | 22 … 104 (`S9_TRD-S2` = 104: 67 батчей + 37 цельных FBX-дверей) |
| Позиция/поворот корня | (39 460.0, 2 475.67, 40 000.0), rotY 180°, scale 1, слой `PRIMUM` (8) |
| Масштаб моделей | k = 1.07 (Blender) |

### 1.3 Двери `PRIMUM_DOORS`

| Показатель | Значение |
|---|---|
| Инстансов префабов | 2 161 (2 051 одиночных + 110 двустворчатых) + 133 в ADMIN |
| Рендереров | 4 949 = 2 363 инстанса × 2 |
| BoxCollider | 4 432 |
| static | false |
| Материалы | 5 `PM_DOOR_*` (проектные, но URP/Lit) |

### 1.4 Свет `PRIMUM_LIGHTS`

| Показатель | Значение |
|---|---|
| Источников | 2 087 (1 557 по узлам `LAMPS` + 530 интерьер ADMIN) |
| В редакторе включено | 0 (включает пул) |
| Одновременно горит | ≤ 96 (пул) |
| Префабов | 7 `PM_LIGHT_*` (Point, без теней, range 3.5–14 м) |
| Emissive | `PM_LAMP_GLOW_WARM` (URP Unlit HDR), 1 293 рендерера |

### 1.5 Физика `PRIMUM_COLLIDERS`

| Показатель | Значение |
|---|---|
| Коллайдеров (PRIMUM) | 10 817 = 10 809 боксов + 8 точных мешей |
| Дома | 10 355 боксов (25 чанков) |
| BASE | 74 (DECK 44 / STAIR 24 / LANDING 4 / SURFACE 2 меша) |
| ADMIN | 382 (WALL 298 / FLOOR 47 / ROOF 9 / STAIR 28) |
| Всего в `WorldRoot_0_0` (после Э8) | 17 181 коллайдер = 17 172 бокса + 9 мешей |

### 1.6 Окружение PRIMUM в сцене

| Корень | Рендереры | Треугольники | Примечание |
|---|---|---|---|
| `RuinsValleys` | 496 инстансов | ~11.4k | 2 инстансинг-материала, тени выкл, коллайдеров нет, `isStatic=false`, разбросаны на 77.7×78.2 км |
| `Secund` | 25 | ~7.6–8.8k | центр (15 733, 61 731) |
| `Tertius` | 25 | ~7.6–8.8k | центр (4 428, 2 931) |
| `Quartus` | 25 | ~7.6–8.8k | центр (76 842, 70 471) |
| `Primum_farms` | 142 | 1 269 389 | батчи (было 11 712 → 142) |
| `Terrain_0_0` | 0 Renderer | — | рисуется `Terrain`-компонентом, для Umbra невидим |

### 1.7 Террейн `Terrain_0_0`

- 80×80 км, `TerrainData` 1025 (size 80000×4400×80000), `heightmapPixelError = 200`, `basemapDistance = 20000`, `drawInstanced = true`, `TerrainCollider` вкл.
- Герои: Main 5000 (срезано с 5581), East 3500, West 3000, Northwest 2800 + 32 пика хребтов.
- Ребёнок `WorldRoot_0_0` → по FO едет бесплатно (🟢).

### 1.8 Материалы

- После Э3.1 (30.10): 0 слотов `URP/Lit` в городе+фермах; 53 материала (DoD ≤ 60).
- До Э3.1: 444 объекта `Material` / 105 имён, 24 378 слотов (92.1 %) из FBX-подматериалов `URP/Lit`.
- SRP Batcher: вкл (`m_UseSRPBatcher: 1`).
- GPU Resident Drawer: выкл (`m_GPUResidentDrawerMode: 0`).
- Static Batching: выкл (`m_StaticBatching: 0`).
- Dynamic Batching: выкл.
- Rendering Path renderer: `m_RenderingMode: 2` (Forward+ по конвенции docs; но docs фиксируют откат к Forward в §8 `01_INDUSTRY_PRACTICE.md` — расхождение требует проверки).

### 1.9 LOD

- 0 `LODGroup` в сцене.
- LOD-дизайн запроектирован (`04_LOD_AND_DISTANCE.md` §1–6), но не реализован (Э6 ⏳).

## 2. Механизмы culling / LOD / distance

### 2.1 `DetailDistanceCuller` (`Assets/_Project/Scripts/World/DetailDistanceCuller.cs`)

- Визуальный culling групп `RuinsValleys` целыми хамлетами (`Renderer.enabled`), гистерезис 10 %, чек каждые 0.5 с.
- Позиции читаются живьём (`transform.position` / `bounds`) — между кадрами мировой `Vector3` не хранится → FO-safe (🟢).
- Хостер: `ViewDistanceRuntime` (`DontDestroyOnLoad`), создаётся `ViewDistanceApplier.EnsureCullerHost`.
- Только визуально; чанки/стриминг не трогает.

### 2.2 `ViewDistanceApplier` + `ViewDistanceConfig`

- `Assets/_Project/Scripts/World/ViewDistanceApplier.cs` — static-апplier (образец `GraphicsEffectsApplier`): `RuntimeInitializeOnLoadMethod` + `sceneLoaded` + подписки на `SettingsManager.OnViewDistanceChanged` / `OnQualityLevelChanged`.
- Владеет: `camera.far`, `QualitySettings.lodBias`, `QualitySettings.shadowDistance`, `Terrain.heightmapPixelError` + `basemapDistance`.
- Не владеет и никогда не выключает: сам террейн (L2-фон всегда видим), чанки стриминга.
- `ResolveCameraFar()` — для камер в `Awake` (SpringArmCamera, WorldCamera) — было `1000000f`, теперь из пресета.
- `GetFogScale()` — множитель плотности тумана для `DayNightController`.
- `Ultra` → ранний return + лог (Far), резерв до 2+ сцен.
- Мировых `Vector3` не хранит → FO-safe (🟢).
- Конфиг: `Assets/_Project/Resources/Config/ViewDistanceConfig.asset` (ScriptableObject).

Пресеты (`ViewDistanceConfig.cs:34-68`):

| Параметр | Near | Medium (дефолт) | Far |
|---|---|---|---|
| `cameraFar` | 30 000 | 60 000 | 120 000 |
| `lodBias` | 0.7 | 1.0 | 1.3 |
| `terrainPixelError` | 300 (код) / 150 (дизайн) | 200 / 100 | 50 / 40 |
| `terrainBasemapDistance` | 12 000 | 20 000 | 80 000 |
| `shadowDistance` | 500 | 1 500 | 4 000 |
| `detailCullDistance` | 4 000 | 8 000 | 12 000 |
| `fogScale` | 1.3 | 1.0 | 0.6 |

### 2.3 `PrimumLightPool` (`Assets/_Project/Scripts/World/PrimumLightPool.cs`)

- Пул источников света города: `maxActive = 96`, `activeRadius = 140 м`, `checkInterval = 0.25 с`, гистерезис 0.1.
- Источники в префабе выключены (`Light.enabled = false`); пул включает не более `maxActive` ближайших.
- Focus: не `Camera.main` (две камеры с тегом) — выбирает активную камеру, у которой ближайший светильник ближе всего; есть явное поле `focus`.
- Позиции читаются живьём → FO-safe (🟢).
- Forward + per-object limit 4: цена пикселя не зависит от числа источников в сцене.

### 2.4 `DistantFocusRenderFeature` + `FarFocusController`

- `Assets/_Project/Scripts/Rendering/DistantFocusRenderFeature.cs`: far-field фокус своим проходом, `FarStart = 800`, `FarEnd = 4000`, `AfterRenderingOpaques`, копия цвета через `AddCopyPass`.
- `Assets/_Project/Scripts/Rendering/FarFocusController.cs`: dwell 1 с, raycast каждые 4 кадра, `_farThreshold = 60`, Gaussian DoF через Volume. Скаляры между кадрами → FO-safe (🟢).
- `GraphicsEffectsApplier` (`Assets/_Project/Scripts/Rendering/GraphicsEffectsApplier.cs`): применяет DepthOfField/EdgeDetection из `SettingsManager`; мировых `Vector3` не хранит → 🟢.

### 2.5 Стриминг сцен: `ClientSceneLoader`

- `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` (1028 строк): singleton + `DontDestroyOnLoad`.
- Сетка 6×4, `SCENE_SIZE = 79999f`, `preloadDistance = 10000f`, `unloadDistance = 10000f`, `maxLoadedScenes = 4`.
- Загрузка — обычный `SceneManager.LoadSceneAsync(path, Additive)` — не через `NetworkSceneManager` (запрет рефакторинга).
- Фокус — `WorldScene_0_0`; соседних игровых сцен пока нет (L3 Ultra forced OFF).
- Handoff пилота: `TryRetireForGlobalPilot`.

### 2.6 `ScenePlacedObjectSpawner`

- `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs`: серверный спавн scene-placed `NetworkObject` с `InScenePlacedSourceGlobalObjectIdHash == 0` (иначе их [Rpc] падают с NRE). `destroyWithScene: true` для world-сцен, `false` для Bootstrap. Удалять запрещено.

### 2.7 `WorldStreamingManager` (legacy, не используется в сценах)

- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs`: `loadRadius = 2`, `unloadRadius = 3`, `updateInterval = 0.5f`, `preloadLayers = 1`, `maxLoadedChunks = 0`. Чанки 2000 units.
- Ни одна `.unity`-сцена не ссылается на него и на `WorldChunkManager`/`ChunkLoader`/`ProceduralChunkGenerator`/`PlayerChunkTracker`/`ChunkNetworkSpawner`/`FloatingOriginMP` (проверено 2026-09-27). Прототипы лежат кодом на диске, в сценах отсутствуют.
- Не воскрешать, не чинить, не дорабатывать.

### 2.8 `FloatingOriginMP` (legacy)

- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`: `threshold = 150000f`, `shiftRounding = 10000f`, режимы Local/ServerSynced/ServerAuthority, world roots по именам, исключает `TradeZones/Player`.
- Не использовать в новом дизайне: транзакция `06N` явно запрещает его включение. Действующий FO — `GlobalMotionControlledRebaseSlice`.

### 2.9 Occlusion culling (Umbra)

- Настройки сцены: `m_OcclusionCullingData: {fileID: 0}`, `smallestOccluder: 5`, `smallestHole: 0.25`, `backfaceThreshold: 100`, `Camera.useOcclusionCulling = True`.
- 6 попыток бейка — все неуспешны: `Failure in split phase` (Umbra 3.3.22). Данные не созданы, папка `WorldScene_0_0/` пуста.
- Попытка 6 с `smallestOccluder = 20` не упала за 5.5 мин, но результат неизвестен (отменена).
- Сцена восстановлена к HEAD `5b41dd7c`, рабочее дерево чистое; статик-флаги = 0 у всех рендереров.
- Подробности: `docs/world/primum_city/06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md`.

### 2.10 GPU-путь (статус)

- `Assets/_Project/Settings/ProjectC_URP.asset`: `m_UseSRPBatcher: 1`, `m_GPUResidentDrawerMode: 0`, `m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0`, `m_ShadowDistance: 1000`, `m_MSAA: 1`.
- `ProjectSettings/ProjectSettings.asset`: `m_StaticBatching: 0`, `m_DynamicBatching: 0`.
- `ProjectSettings/GraphicsSettings.asset`: BRG Variants — Strip (был Keep All при включении).
- GPU Resident Drawer включали 17.10 → откатили 18.10 из-за shader errors (`ProjectC/ProceduralSteel`: undeclared identifier `unity_ObjectToWorld` в варианте `DOTS_INSTANCING_ON`).
- Э5 (следующий этап): адаптировать кастомные шейдеры (`#pragma multi_compile _ DOTS_INSTANCING_ON`, `UNITY_DOTS_INSTANCED_PROP`) → включить Drawer = Instanced Drawing, GPU occlusion culling, BRG Variants = Keep All, Forward+.

## 3. Floating Origin и его влияние на culling

### 3.1 Действующий механизм

- `GlobalMotionControlledRebaseSlice`: F8 вручную, авто при отлёте > 256 м от origin (опрос 5 с, кулдаун 35 с, галка `Auto Rebase Enabled`), F9 — откат.
- Корни сцен + игроки едут `position += T` с серверным `NetworkTransform.Teleport` (`NetworkPublished`); локальные кэши сдвигаются тем же `T` под флагами; вторым клиентам — broadcast `FO06_REBASE_SHIFT` (±T).
- Маркеры успеха: `runtimeRebase.Completed`, `ActorRebound(ok=True)`.

### 3.2 Две подсистемы (не путать)

| # | Подсистема | Правило для города |
|---|---|---|
| A | Сдвиг мира (F8 / авто / F9) | корень `PRIMUM` — ребёнок `WorldRoot_0_0` → едет бесплатно 🟢; коллайдеры хранят только локальные `center/size` → тоже 🟢; никаких собственных кэшей мировых `Vector3` в скриптах города |
| B | Допуск при старте (`GlobalSceneNativeExecutor.BuildPreparation`) | цензит только `NetworkObject` (и маркеры). Пока город — статика (меши + коллайдеры, без `NetworkObject`/`Rigidbody`), он гейту невидим и каталог трогать не нужно |

### 3.3 Пайплайн нового контента (`09S_NEW_CONTENT_PIPELINE.md`)

- Контент под корнями `WorldScene_*` едет со сдвигом бесплатно.
- Скрипты, кэширующие мировые `Vector3`, рантайм-спавн вне корней сцен и сейвы координат — по пайплайну (хуки сдвига / регистрация участником / кумулятив).
- Каталог пилота: 151 запись (прецедент парома), тулза `ProjectC/World/Floating Origin/Bake Pilot Catalog (Preview + Apply)` (`Assets/_Project/Editor/FloatingOrigin/BakePilotSceneCatalog.cs`).
- Контракт транзакции: closed participant set, фазы REQUEST → FREEZE → PREFLIGHT → CAPTURE → APPLY → REBUILD → VALIDATE → PUBLISH → RELEASE; инварианты (в т.ч. `FloatingOriginMP` не включается, общий `SetParent` для NO/ship/city запрещён).

### 3.4 FO-классы (🟢/🟡/🔴)

**🟢 (не хранит мировые Vector3, едет бесплатно):**
- `DetailDistanceCuller` — позиции читает живьём (`transform.position` / `bounds`).
- `ViewDistanceApplier` — только настройки + дистанции, не координаты.
- `PrimumLightPool` — позиции читаются живьём (`transform.position`).
- `DistantFocusRenderFeature` / `FarFocusController` — только скаляры между кадрами.
- `GraphicsEffectsApplier` — мировых `Vector3` не хранит.
- `DayNightController` (fog) — множитель плотности, не координаты.
- Контент под корнями `WorldScene_*` (меши, коллайдеры, свет, двери) — едет бесплатно.

**🔴 (кэширует мировые Vector3 без хука — требует проверки):**
- Любые скрипты, хранящие мировые позиции между кадрами без `ApplyRebaseTranslation` (поиск по коду нужен при добавлении нового контента).
- Сейвы координат (кумулятивный offset).
- Рантайм-спавн вне корней сцен (без маркеров/каталога → Host fail-closed `uncontrolled_network_source_before_native_sweep`).

### 3.5 Влияние на culling (ключевое)

1. **Запрет статических флагов**: мир ездит ребейзом → объекты города не помечаем static (запечённый static batching/occlusion ломается при сдвиге; именно так «отвалились» двери у заказчика). Поэтому:
   - Static Batching off (и в ProjectSettings, и на объектах).
   - Umbra baked occlusion неприменим (статик-флаги запрещены) — и бейк провален.
   - Culling обязан быть динамическим: frustum, distance, GPU occlusion (Drawer), LOD/HLOD.
2. **Контент под корнями едет бесплатно** → HLOD/импосторы должны быть детьми корня сцены и хранить только скаляры (FO-класс 🟢).
3. **Запрет кэширования мировых позиций** в скриптах куллинга → дистанции считаются каждый кадр от текущей позиции камеры (как в `DetailDistanceCuller` и `PrimumLightPool`).
4. **Рантайм-спавн** вне корней сцен упрётся в гейт B (каталог/маркер/digest) — весь интерактив города должен быть scene-placed (префабы/сцена), а не runtime-spawn (блокер: `NetworkPrefabsList` не назначен → динамический спавн сломан; назначен только 30.10 `DefaultNetworkPrefabs`, 58 записей).

## 4. Оптимизация города (Э1–Э8): что сделано, что нет

| Этап | Что | Статус | Ключевой результат |
|---|---|---|---|
| Э1 | Коллайдеры: BASE 74 + дома 10 355 боксов + ADMIN 382 | ✅ закрыт 23.10 | 10 817 коллайдеров; 6 `MeshCollider` STAIRS удалены в Э8 |
| Э2 | Двери: 3 одиночных + 1 двустворчатый префаб; расстановка | ✅ закрыт 23.10 | 2 161 инстанс + 133 ADMIN; verify 0 ошибок |
| Э3 | Свет: 7 префабов `PM_LIGHT_*`, emissive, бюджет 96 | ✅ закрыт 23.10 | 2 087 источников (1 557 + 530 ADMIN); дворцы не тронуты |
| Э3.1 | Материалы: весь `PRIMUM` на процедурные | ✅ закрыт 30.10 | 0 слотов `URP/Lit` в городе+фермах; 53 материала (DoD ≤ 60) |
| Э4 | Зернистость пасс 2 | ✅ закрыт 30.10 | PRIMUM 19 944 → 1 207; двери 11 726 → 4 949; ADMIN-двери 712 → 266; чанк 22…104 |
| Э5 | GPU-путь: шейдеры → DOTS-инстансинг → GPU Resident Drawer | ⏳ следующий | Блокер: кастомные шейдеры не поддерживают `DOTS_INSTANCING_ON` |
| Э6 | LOD L1/HLOD + L2-импостор | ⏳ не начат | 0 `LODGroup`; решение открыто: HLOD по чанку (23 группы) или сначала нарезать блоки |
| Э7 | Интерьеры отдельными префабами (вариант B) | ⏸ отменён 30.10 | Решение заказчика: интерьеры остаются в батчах |
| Э8 | Чистка и долги | ✅ закрыт 30.10 | Легаси `Primum` снят (−6 223 рендерера, −873 коллайдера, −11 ламп); заглушки дверей удалены; `NetworkPrefabsList` назначен (`DefaultNetworkPrefabs`, 58 записей). Отложены `OUTLIERS` (75 рендереров, выключены) и NavMesh |

### 4.1 Что осталось открытым по оптимизации

- **Э5 (GPU-путь)**: адаптация кастомных шейдеров (`ProjectC/ProceduralSteel` и др.) к `DOTS_INSTANCING_ON` (`#pragma multi_compile _ DOTS_INSTANCING_ON`, `UNITY_DOTS_INSTANCED_PROP` / `UnityDOTSInstancing.hlsl`) → включение `GPU Resident Drawer = Instanced Drawing`, GPU occlusion culling, `BRG Variants = Keep All`, `Forward+`. Приёмка: 0 shader errors, Frame Debugger показывает `Hybrid Batch Group`, SetPass падает.
- **Э6 (LOD/HLOD)**:
  - L0 (0–3 км): полные блоки (как сейчас).
  - L1 (3–12 км): HLOD по кварталу/чанку — 1 меш на блок, окна эмиссией, мелочь скрыта (`DetailDistanceCuller`), bake в редакторе + `LODGroup`.
  - L2 (12–80 км): 1 билборд-квад на город (camera-facing, pre-rendered силуэт с альфой) + аддитивный квад ночных огней; позиция пересчитывается от камеры каждый кадр, хранятся только скаляры → FO-класс 🟢.
  - Открытое решение: HLOD по чанку (23 группы) или сначала нарезать блоки 20–60.
- **Импосторы**: запрещено генерировать в рантайме из сида; bake один раз в Editor (RenderCamera → RenderTexture → Sprite), ассеты рядом со слоями террейна (пути вне `*Textures*`).
- **Интерьеры**: вариант B отменён; интерьеры остаются в общих батчах (1 116 рендереров `_INT_`).

### 4.2 Остаток вне батчей (172 рендерера `PRIMUM`)

| Что | Рендереров | Состояние | Куда |
|---|---|---|---|
| `OUTLIERS` ×3 (`S1_GLD` 33, `ADMIN` 38, `TRD_P2` 4) | 75 | дети `activeSelf = false`, 2 900 тр. | Э7/Э8 (отложено) |
| Цельные FBX-двери `*_X` в узлах `*/DOORS` | 73 | видимы, 1 рендерер на дверь, нет инстанса префаба и `DoorController` | остаток Э2 |
| Интерьерные цветовые группы `S9_RING` | 14 | видимы, 1 материал на объект | Э7 |
| Эмблемы гильдий в `LAMPS` | 6 | `M_GUILD_*_EMIT` | Э3.1/Э8 |
| `…_INT_NOMAT` (2) + `…_S7_BIG…_INT_` (1) | 3 | материал `No Name` | Э3.1 |
| `ADM_WING_S_ENTRY_STEPS` | 1 | мраморные ступени в корне чанка | оставлено |

⚠️ `OUTLIERS` нельзя вливать в батчи: батч-объект включён, поэтому стол `GLD_THOUGHT_01` (задвоен по трём этажам) и 20 бра ADMIN с габаритом 90–172 м станут видимыми.

## 5. Двери: 2 161 инстанс, 2 рендерера на дверь, draw calls

### 5.1 Устройство

- 3 одиночных префаба (`PLANK_A`, `PANEL_B`, `ARCH_C`) + 1 двустворчатый (`DOUBLE_A`) + ADMIN-варианты.
- Каждый инстанс = 2 рендерера (полотно + рама/детали) → 2 363 инстанса × 2 = 4 726 рендереров (+ 133 ADMIN × 2 = 266).
- `static = false` (правило FO).
- 4 432 `BoxCollider` на дверях.
- `DoorController` — интерактив (E), коллайдер полотна блокирует, рама — триггер.

### 5.2 Влияние на draw calls

Из `04_OCCLUSION_CULLING_RESEARCH.md` §3 (кадр из центра города, fov 60, far 1000, `MainCamera` yaw 337.5°):

| Конфиг | Двери | Draw calls | SetPass | Треугольники, млн | FPS (равный кадр) |
|---|---|---|---|---|---|
| A (shadow 1000 / 2 каскада) | вкл | 26 981–26 996 | 232 | 15.86–16.57 | 27.8 (frame 2659) |
| B (shadow 250 / 1 каскад) | вкл | 21 118 | 216 | 11.61 | 34.1 (frame 2685) |
| A | выкл | 4 954 | 195 | 15.68 | 38.8 |
| B | выкл | 3 352 | 182 | 10.88 | 35.7 |

- В конфиге B двери дают 17 766 из 21 118 draw calls = 84 %.
- Δ A vs B (двери вкл): draw calls −21.5 %, FPS +22.7 %.
- Конфиг откачен к A (1000 / 2 каскада) по решению заказчика («задача — идеальный culling», тени не трогаем).

### 5.3 Почему двери не батчатся

- Каждый инстанс — отдельный `GameObject` с собственным трансформом и материалом (`PM_DOOR_*`).
- SRP Batcher снижает state changes, но не объединяет вызовы; объединяют GPU instancing / BRG / GPU Resident Drawer / merged meshes.
- GPU Resident Drawer выключен → нет GPU-инстансинга.
- Static Batching запрещён (FO) → нельзя запечь.
- Двери имеют интерактив (`DoorController`) и свой коллайдер → нельзя просто слить в общий меш чанка (потеря функциональности).
- Цель: Э5 — адаптация шейдеров + включение Drawer'а. Поскольку двери используют общие префабы (одинаковые меш-ассеты), GPU-инстансинг потенциально выгоден (одинаковые меш + материал → один Hybrid Batch Group). Но это не проверено.

### 5.4 Прокси-оценка видимости (по коллайдерам, не окклюзия)

- Из центра города: фрустум 2 881 дверь, «видимо» по физике 10 → ~99 % скрыто.
- Город: 758 во фрустуме, «видимо» 70.
- Скан 49 открытых уличных точек: среднее frus 1 106 / vis 2; плотный вид 4 691 / 16.
- ⚠️ Оценка не является замером окклюзии: коллайдеры дверей — упрощённые боксы, луч в центр двери упирается в стену, а не в проём. Годится только как порядок величины.

## 6. Корабли и тяжёлые объекты

### 6.1 Корабли в сцене

- В `WorldScene_0_0` historically есть root `ships` с 3 тестовыми `ShipController` (instanceID 94204, 94146, 94074) — из архивного doc `docs/archive/Ships_legacy/INTEGRATION_SHIPS_TO_WORLD_0_0.md`.
- Каждый корабль: `Rigidbody` + `NetworkObject` + `ShipController` (NetworkBehaviour, scene-placed).
- Спавнится через `ScenePlacedObjectSpawner` (иначе [Rpc] падают с NRE — `InScenePlacedSourceGlobalObjectIdHash == 0`).
- В текущей сцене root `ships` не найден grep'ом (возможно переименован/удалён или корабли не статичны) — статус не верифицирован.
- Корабли — участники ребейза (NetworkTransform.Teleport). Адаптер кораблей: `docs/Ships/08C_SHIP_ADAPTER_DESIGN.md` (dormant).
- Trade зависит от `CargoSystem` на каждом корабле (`docs/Markets/INTEGRATION.md`).

### 6.2 `CombineMeshesToCollider` (`Assets/_Project/Editor/CombineMeshesToCollider.cs`)

- Склеивает все дочерние `MeshFilter` в один комбинированный меш (`*_Combined.asset`) и вешает `MeshCollider` (convex=false) на корень.
- Зачем: на импортированных схемах («Primum / gorod port») 100+ дочерних объектов, у всех только `MeshFilter`+`MeshRenderer`, без коллайдеров. `CharacterController` требует одного невыпуклого mesh-коллайдера.
- Опция `RemoveRenderersOnChildren`: true → удаляет рендер с детей, один общий меш на корне; false → оставляет рендер детей.
- Пример ассета: `gorod port_Combined.asset`.
- ⚠️ Тяжёлые корабли хранят кучу объектов — второй кандидат на «не склеивать, а куллить».

### 6.3 Корабли и оптимизация

- Корабли не упомянуты в бюджетах PRIMUM (draw calls города). Но в брифе исследования они выделены как кандидат на culling («внутри куча объектов»).
- В `docs/world/primum_city/06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md` §4 корабли не фигурируют в инвентаризации окклюдеров (только PRIMUM, двери, фермы, руины, горы).
- В `docs/world/chunk_system/03_DISTANT_CITIES_TERRAIN.md` §3 L0 упоминает «корабли» в числе объектов полной геометрии (0–3 км).

## 7. Ограничения и бюджеты

### 7.1 Бюджеты (Definition of Done города, `ROADMAP.md` §5)

| Критерий | Норма |
|---|---|
| Ходимость | площадь, улицы, лестницы, входы, крыши проходимы; двери открываются E |
| Коллайдеры | боксы по классам; лестницы рампой; 0 промахов луча по плитам |
| Зернистость | PRIMUM ≤ ~1 200 мешей (✅ 1 207), дверь = 2 рендерера (✅), чанк ≤ ~110 (✅ 104) |
| **Draw calls (L0)** | **≤ 120** |
| Дальность | 0.5 км полный, 5–10 км HLOD, 20–80 км 1–2 вызова |
| Ребейз | F8/F9: город, физика, двери, огни, HLOD/импостор едут; 0 errors |
| Редактор | открытие `WorldScene_0_0` и вход в Play без заметных задержек |
| Сеть | `NetworkPrefabsList` назначен (✅ `DefaultNetworkPrefabs`, 58 записей) |

### 7.2 Настройки дальности и тумана

- Far plane камеры: 30 000 / 60 000 / 120 000 (ViewDistance пресеты; было 1 000 000 до T-LOD01).
- Near plane: 0.1 (`SpringArmCamera`) / 0.5 (`WorldCamera`).
- Shadow distance: 1 000 (URP asset); пресеты 500 / 1 500 / 4 000.
- Basemap distance: 12 000 / 20 000 / 80 000 (всегда ≤ far).
- Fog: DayNightController, экспоненциальный density 0.0003 (`TimeOfDayPhase.fogDensity`); fogScale множитель 1.3 / 1.0 / 0.6.
- Terrain pixelError: 300 / 200 / 50 (код) — дизайн 150 / 100 / 40 (калибровка).
- DistantFocus: FarStart 800 / FarEnd 4000 (не менять при смене пресета).
- FarFocusController: `_farThreshold = 60`, `_lockTime = 1`, raycast каждые 4 кадра.

### 7.3 Ограничения архитектуры (HARD RULES из AGENTS.md)

- Не добавлять второй `NetworkManager`; не переносить его; не рефакторить `ClientSceneLoader` на `NetworkSceneManager`; не удалять `ScenePlacedObjectSpawner`.
- Не писать `.meta` / `.asmdef` руками.
- Не трогать: `Library/`, `Temp/`, `Builds/`, `UserSettings/`, `Logs/`, `obj/`, `bin/`, `node_modules/`, `.agents/`, `graphify-out/`, `docs/gdd/`, `docs/WORLD_LORE_BOOK.md`, `src/`.
- Спросить перед: `ProjectSettings/*`, `Packages/manifest.json`, `.gitignore`, `git push`, `git reset --hard`, `git stash drop`.
- Не запускать `run_tests` MCP и Build через MCP.

## 8. Известные проблемы и постмортемы

### 8.1 Нормали (постмортем `02_PIPELINE/02_NORMALS_POSTMORTEM.md`)

- 0 вывернутых островов по городу (после ремонта `fix_normals_all.py`).
- 19 221 из 19 944 мешей — Z-up (поворот X 270°), масштаб ×100, пивот в 20–30 м от детали.
- Графы ждут Y-up, пивот внутри, единица ≈ размер объекта.
- Вырожденные тангенсы у мешей без UV: 17 394 из 19 239 мешей (22.9 % вершин) — импортёр ставит всем вершинам tangent (1,0,0,−1); на гранях ±X TBN вырожден → `NormalTS` любого рельефного графа даёт чёрные мелькающие треугольники на одном боку всех домов.
- Лечение: тангенс из нормали в копии меша (`PrimumMeshRebase`).
- Normal From Height неточен далеко от origin: шаг float ≈ 3.9 мм на ~40 км → чёрная грануляция на bump. Лечение: Custom Function `PC_NormalFromHeightPrecise` (`ProceduralBumpPrecise.hlsl`), патчер `PrecisionBumpPatcher`; принято на `Cobble` + 17 графов.

### 8.2 Z-fighting

- `117_CITY_ZFIGHT_AUDIT.md` (детектор/фикс), `118_CITY_FACADE_FIX.md` (перекрытия/подоконники/коробки окон), `119_CITY_NORMALS.md` (ориентация граней).
- При far 1 000 000 шаг глубины на 4 км ≈ 10 м → z-fighting на склонах.
- Уменьшение far до 30–120K (T-LOD01) лечит.
- `120_RES-ALLEY_REIMPORT_UV.md`: переимпорт S9 RES-ALLEY по v13.8 + UV smart unwrap после join + подмена в Unity 1:1.

### 8.3 Umbra failure (`04_OCCLUSION_CULLING_RESEARCH.md`)

- 6 попыток бейка — все `Failure in split phase` (Umbra 3.3.22).
- Данные не созданы, `umbraDataSize = 0`, папка окклюзии пуста.
- Исключено как причина: объём/разлёт окклюдеров, лимит 65 535 вершин, число окклюдеров, большие мировые координаты, грубость `smallestOccluder`.
- Подозреваемое: невалидная геометрия в merged-мешах (дырявые оболочки — Umbra строит замкнутые окклюдерные оболочки); поведение Umbra 3.3.22 в Unity 6; террейн не даёт Renderer → невидим для Umbra (для города в котловине — заведомо неполная карта).
- Попытка 6 с `smallestOccluder = 20` не упала за 5.5 мин, но результат неизвестен (отменена).
- Сцена восстановлена к HEAD, рабочее дерево чистое; статик-флаги = 0 у всех рендереров.

### 8.4 GPU Resident Drawer откат (18.10)

- Кастомные шейдеры (`ProjectC/ProceduralSteel` и др.) не поддерживают `DOTS_INSTANCING_ON` → `undeclared identifier 'unity_ObjectToWorld'` → съеденные куски мешей.
- Откачено: GPU Resident Drawer Disabled, GPU occlusion off, Rendering Path Forward, BRG Variants Strip.
- SRP Batcher остался вкл.
- Рецепт возврата: `#pragma multi_compile _ DOTS_INSTANCING_ON` + `UNITY_DOTS_INSTANCED_PROP` / `unity_ObjectToWorld` из `UnityDOTSInstancing.hlsl`.

### 8.5 Подводные камни рендера

- **`Camera.main`**: две камеры с тегом `MainCamera` (статичная Bootstrap в (239997, 3000, 159998) — 233 км от города + спавнимая `ThirdPersonCamera`). `Camera.main` отдаёт статичную → пул/FO/стриминг получают расстояния в сотни километров. Обход: `PrimumLightPool.FindFocusCamera`, `FloatingOriginMP.GetWorldPosition`, `WorldStreamingManager.GetLocalPlayerPosition`.
- **Shadow config A vs B**: A (1000/2) vs B (250/1): draw calls −21.5 %, FPS +22.7 % (двери вкл). Откачено к A.
- **Fog**: exp-туман `density 0.0003` топит даль раньше far → рычаги far/basemap не видны без `fogScale` (T-LOD01b).
- **Forward+**: включение 17.10 изменило свет/тени (кластерное освещение); откачено к Forward.
- **Precision bump**: Normal From Height неточен далеко от origin → чёрная грануляция на bump; принято Custom Function на 17 графах.

### 8.6 Материалы (ловушки из `09_MATERIALS/01_MATERIAL_UNIFICATION.md`)

- `.material` вместо `sharedMaterials` — множит объекты и убивает SRP Batcher (абсолютный запрет).
- Оверрайды живут в `PRIMUM_CITY.prefab` — подмена FBX их снимает.
- `M_BRONZE` — 23 объекта, цвета могут отличаться.
- Палитра живёт в пресете, не в дефолтах блэкборда.
- Все Shader Graph односторонние (`m_RenderFace = 2` Front), кровельные `Primum/Roof/*.mat` — двусторонние (`_Cull = 0`) намеренно.
- `Normal From Height` — известный дефект сборки: у `Cobble`, `Bark`, `Concrete`, `Plastic` рельеф погашен.
- Штатный Normal From Height неточен далеко от origin (шаг float).
- `Fabric_v2/` не использовать.

## 9. Источники (пути)

### 9.1 Скрипты (ключевые)

| Файл | Роль |
|---|---|
| `Assets/_Project/Scripts/World/DetailDistanceCuller.cs` | визуальный culling RuinsValleys |
| `Assets/_Project/Scripts/World/ViewDistanceApplier.cs` | static-апplier дальности |
| `Assets/_Project/Scripts/World/ViewDistanceConfig.cs` | пресеты дальности |
| `Assets/_Project/Scripts/World/PrimumLightPool.cs` | пул источников света |
| `Assets/_Project/Scripts/Rendering/DistantFocusRenderFeature.cs` | far-field фокус |
| `Assets/_Project/Scripts/Rendering/FarFocusController.cs` | контроллер взгляда |
| `Assets/_Project/Scripts/Rendering/GraphicsEffectsApplier.cs` | тумблеры графики |
| `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs` | legacy стриминг чанков |
| `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` | legacy FO |
| `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` | стриминг сцен |
| `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` | серверный спавн scene-placed NO |
| `Assets/_Project/Editor/CombineMeshesToCollider.cs` | склейка мешей кораблей |
| `Assets/_Project/Editor/FloatingOrigin/BakePilotSceneCatalog.cs` | каталог пилота FO |

### 9.2 Документы (ключевые)

| Файл | Роль |
|---|---|
| `docs/world/primum_city/ROADMAP.md` | дорожная карта Э1–Э8, бюджеты, DoD |
| `docs/world/primum_city/README.md` | индекс, сводка состояния |
| `docs/world/primum_city/07_STATE/01_RESUME_STATE.md` | состояние (исторический) |
| `docs/world/primum_city/01_SETUP/04_LOD_AND_DISTANCE.md` | L0/L1/L2 |
| `docs/world/primum_city/06_REFERENCE/01_INDUSTRY_PRACTICE.md` | GPU-путь, SRP Batcher/Drawer |
| `docs/world/primum_city/06_REFERENCE/02_FO_NETWORK_AND_GAMEPLAY.md` | FO/сеть/NavMesh |
| `docs/world/primum_city/06_REFERENCE/03_ACCEPTANCE.md` | приёмка |
| `docs/world/primum_city/06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md` | Umbra failure, замеры draw calls |
| `docs/world/primum_city/08_LIGHTING/01_LIGHTING.md` | свет, пул, emissive |
| `docs/world/primum_city/09_MATERIALS/01_MATERIAL_UNIFICATION.md` | материалы |
| `docs/world/primum_city/10_CULLING_RESEARCH/_BRIEF.md` | бриф исследования |
| `docs/world/optimization/README.md` | оптимизация, контекст |
| `docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md` | дизайн 4 колец |
| `docs/world/optimization/IMPLEMENTATION_PLAN.md` | план T-LOD01 |
| `docs/world/optimization/ITERATIONS.md` | лог T-LOD01 |
| `docs/world/chunk_system/01_CURRENT_STATE.md` | сцены, стриминг, террейн, FO |
| `docs/world/chunk_system/02_DO_WE_NEED_CLIENT_CHUNK_LOADER.md` | чанк-лоадер: нет |
| `docs/world/chunk_system/03_DISTANT_CITIES_TERRAIN.md` | города-силуэты |
| `docs/world/floatingorigin/README.md` | FO указатель |
| `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` | пайплайн нового контента |
| `docs/world/terrain/README.md` | террейн |

### 9.3 Настройки рендера

| Файл | Ключевые значения |
|---|---|
| `Assets/_Project/Settings/ProjectC_URP.asset` | `m_UseSRPBatcher: 1`, `m_GPUResidentDrawerMode: 0`, `m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0`, `m_ShadowDistance: 1000`, `m_MSAA: 1` |
| `Assets/_Project/Settings/ProjectC_URP_Renderer.asset` | `m_RenderingMode: 2` |
| `ProjectSettings/ProjectSettings.asset` | `m_StaticBatching: 0`, `m_DynamicBatching: 0` |
| `ProjectSettings/GraphicsSettings.asset` | BRG Variants: Strip |

## 10. Handoff

- **Главный вывод:** город PRIMUM доведён до зернистости 1 207 мешей и 0 слотов `URP/Lit`, но draw calls 26 981–26 996 против бюджета 120. Зазор закрывается только Э5 (GPU-путь: адаптация шейдеров + включение Drawer'а) и Э6 (LOD/HLOD + импосторы). Umbra baked occlusion неприменим (статик запрещён + бейк провален).
- **3 главных факта:** (1) двери = 84 % draw calls кадра (17 766 из 21 118), но они интерактивны и не сливаются в чанк; (2) GPU Resident Drawer выключен из-за кастомных шейдеров без `DOTS_INSTANCING_ON`; (3) Floating Origin запрещает статику → culling обязан быть динамическим (frustum/distance/GPU-occlusion), а не bake.
- **Что осталось непроверенным:** визуальная приёмка всех этапов (NOT RUN — за пользователем); замер draw calls после Э5/Э6; статус кораблей в сцене (root `ships` не найден grep'ом); расхождение Rendering Path (Forward vs Forward+) в ассете; калибровка чисел ViewDistance (пиксель-ошибка, fogScale) по скриншотам.
