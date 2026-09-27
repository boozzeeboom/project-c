# 01. Текущее состояние: сцены, стриминг, террейн, FO

Дата: 2026-09-27. Цель — зафиксировать фактуру, на которую опираются выводы ресёрча.
Всё ниже — по коду и докам на диске; рантайм-поведение в Play Mode помечено отдельно.

## 1. Сцены: Bootstrap + 24 стриминговые

- `BootstrapScene` (build [0], `DontDestroyOnLoad`): `NetworkManager` (+`NetworkManagerController` +
  `UnityTransport`), `NetworkPlayerSpawner`, `ClientSceneLoader`, `ScenePlacedObjectSpawner`.
  Второй `NetworkManager` запрещён; перенос запрещён (`AGENTS.md`, раздел Scene architecture).
- 24 файла `WorldScene_X_Z` лежат на диске (`Assets/_Project/Scenes/World/`, сетка 6×4):
  `WorldScene_0_0 … WorldScene_5_3` — итого 24. **Игровой контент сфокусирован в `WorldScene_0_0`**;
  соседних игровых сцен пока нет — это прямо зафиксировано в
  `docs/world/optimization/README.md:19` и `docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md:20,39`
  (L3 Ultra — forced OFF до появления 2+ сцен).
- Реестр: `Assets/_Project/Scripts/World/Scene/SceneRegistry.cs:14-18`
  (`GridColumns = 6`, `GridRows = 4`, `SceneNamePrefix = "WorldScene_"`,
  `ScenePathFormat = "Assets/_Project/Scenes/World/{0}{1}_{2}.unity"`).
  Хелперы `GetSceneGrid3x3` / `GetSceneGrid5x5` (`SceneRegistry.cs:87-114`) используются лоадерами.
- Идентификатор: `Assets/_Project/Scripts/World/Scene/SceneID.cs:17`
  (`SCENE_SIZE = 79999f`, `OVERLAP_SIZE = 1600f`), `FromWorldPosition` / `ToLocalPosition` /
  `IsNearBoundary` (`SceneID.cs:37-69`).

## 2. Клиентский стриминг сцен: ClientSceneLoader (действующий)

Файл: `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` (1028 строк — самый полный источник).

- Singleton + `DontDestroyOnLoad` (`ClientSceneLoader.cs:13-51`); поиск `SceneRegistry` в `Resources`
  (`ClientSceneLoader.cs:36-41`).
- Параметры: `preloadDistance = 10000f`, `unloadDistance = 10000f`, `maxLoadedScenes = 4`
  (`ClientSceneLoader.cs:64-70`). Константа дублирует `SCENE_SIZE` (`ClientSceneLoader.cs:119`).
- Цикл `Update` (`ClientSceneLoader.cs:148-232`): позиция игрока → `SceneID.FromWorldPosition` →
  mismatch → `LoadSceneBoundaryBased`; приближение к границе → preload соседа (`CalculatePreloadScene`,
  `ClientSceneLoader.cs:259-270`); контроль числа (`ManageLoadedScenesCount`, `ClientSceneLoader.cs:272-298`);
  дистанционная выгрузка (`CheckDistanceBasedUnload`, `ClientSceneLoader.cs:234-257`).
- Стартовая загрузка: `LoadSceneWithNeighborsCoroutine` грузит 3×3 вокруг центра
  (`ClientSceneLoader.cs:843-902`); хост стартует с `(0,0)` (`ClientSceneLoader.cs:406`);
  защита от «фантом-клона» `PlayerSpawner` — поиск настоящего `PlayerObject` через
  `ConnectedClients[LocalClientId].PlayerObject` (`ClientSceneLoader.cs:648-661`).
- Загрузка — обычный `SceneManager.LoadSceneAsync(path, Additive)` (`ClientSceneLoader.cs:956`),
  **не** через `NetworkSceneManager` (запрет рефакторинга — `AGENTS.md`).
- Handoff пилота: `TryRetireForGlobalPilot` (`ClientSceneLoader.cs:92-111`) — отписка, стоп корутин,
  guards на in-flight `AsyncOperation`, `enabled = false`, без выгрузки сцен.
  Вызывается из `GlobalMotionPilotRuntime` до additive load и после.
- События: `OnSceneLoaded` / `OnSceneUnloaded` / `OnSceneTransition` (`ClientSceneLoader.cs:123-126`) —
  на них подписаны `ScenePlacedObjectSpawner`, `WorldSceneManager`, квест-бриджи
  (`Assets/_Project/Quests/Bridges/ContractMetaBridge.cs:36`).

## 3. Координация сцена↔чанк: WorldSceneManager (legacy, retired для пилота)

Файл: `Assets/_Project/Scripts/World/WorldSceneManager.cs`.

- Заявленная роль — «coordination layer между Scene Layer (80k) и Chunk Layer (2k)»
  (`WorldSceneManager.cs:10-13`): сцены грузятся раньше чанков, scene-aware фильтр чанков,
  управление `FloatingOriginMP`, preload-триггеры (`WorldSceneManager.cs:205-287`).
- Подписки на события `ClientSceneLoader` (`WorldSceneManager.cs:150-168`), прокидывание
  `SetLoadedScenesFilter` в `WorldStreamingManager` (`WorldSceneManager.cs:170-196`).
- Управление FO по гистерезису 90K/70K (`WorldSceneManager.cs:292-313`).
- **Точно так же retired:** `TryRetireForGlobalPilot` (`WorldSceneManager.cs:76-91`).
  Диагностика из `ITERATIONS.md:2260` прямо называет его «оставшимся активным legacy-координатором,
  продолжавшим владеть preload path» после retire `ClientSceneLoader`.
- Вывод для ресёрча: связка «сцены → фильтр чанков» проектировалась под legacy-чанки (см. §6)
  и не используется пилотом FO.

## 4. Серверная сторона сцен: ServerSceneManager + ScenePlacedObjectSpawner

- `Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs` — серверный `NetworkBehaviour`:
  per-client карта `clientId → SceneID` + `loadedScenes` + `playerTransforms` + `lastUpdateTimes`
  (`ServerSceneManager.cs:30-37`); тик `updateInterval = 0.5f`; защита от быстрых ретранзишенов
  `MIN_TRANSITION_INTERVAL = 1.0f` (`ServerSceneManager.cs:46-48`).
- Инициализация и транзишены шлются таргетными `ClientRpc` (`SendInitialSceneToClient`,
  `SendSceneTransitionToClient`, `SendSceneUnloadToClient` — `ServerSceneManager.cs:267-374`).
- Реестр видимости: `SceneID → HashSet<NetworkObject>` + `NetworkHide/NetworkShow`
  (`ServerSceneManager.cs:40-42,407-486`). Это **зачаток interest management**, но:
  реестр заполняется только явными `RegisterSceneObject` (автоскана сцены нет),
  показ после загрузки — фиксированная задержка `1f` (`ShowSceneObjectsAfterLoad`, `ServerSceneManager.cs:242-246`).
- `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` — критический костыль NGO:
  у части scene-placed `NetworkObject` `InScenePlacedSourceGlobalObjectIdHash == 0`, их NGO
  через `NetworkSceneManager` не спавнит автоматически; спавнер вручную `Spawn()` всё неспаwned
  (`ScenePlacedObjectSpawner.cs:10-22,113-213`), `destroyWithScene: true` для world-сцен,
  `false` для Bootstrap. Удалять запрещено (`AGENTS.md`). При активном GlobalPilot ownership
  переходит к explicit global bootstrap (`ScenePlacedObjectSpawner.cs:116,170`).
- Известная проблема (действующая): `NetworkPrefabsList` не присвоен → **динамический спавн сломан**
  (отдельный тикет, `AGENTS.md`). Любой дизайн «спавнить чанки динамически» упирается в это первым.

## 5. Мир: единый Terrain_0_0 + ViewDistance (почему «картинка хорошая»)

- `WorldScene_0_0/WorldRoot_0_0/Terrain_0_0`: 80×80 км, `TerrainData` 1025 (size 80000×4400×80000),
  `heightmapPixelError = 200`, `basemapDistance = 20000`, `drawInstanced = true`, `TerrainCollider` включён.
  Источник: `docs/world/terrain/README.md:7-16`, `docs/world/optimization/README.md:15`.
  Ребёнок `WorldRoot_0_0` → по FO едет бесплатно (🟢), heightmap в локальных координатах.
- Геройские пики: Main 5000 (срезано с 5581), East 3500, West 3000, Northwest 2800 + 32 пика хребтов
  (`docs/world/terrain/README.md:18-28`). Отключённые FBX-массивы лежат рядом (`SetActive(false)`).
- Руины низин `RuinsValleys` (T-TERR-02): 60 хамлетов, 496 инстансов, 2 инстансинг-материала → ~5–7 draw calls,
  тени выкл, коллайдеров нет, `isStatic = false` (не ломает батчинг при сдвиге)
  (`docs/world/terrain/README.md:33-37`, `docs/world/optimization/README.md:16`).
- Камеры до T-LOD01: `far = 1000000`, `near = 0.1/0.5` — главный кандидат на снижение
  (`docs/world/optimization/README.md:17`; про шаг глубины 10 м на 4 км — `docs/world/distantfocus/DESIGN_farfocus.md:20-23`).
- T-LOD01 (реализован, `docs/world/optimization/README.md:25`, `ITERATIONS.md` там же):
  `Assets/_Project/Scripts/World/ViewDistanceConfig.cs:34-68` — пресеты
  Near 30K / Medium 60K (дефолт = текущий вид) / Far 120K; `lodBias` 0.7/1.0/1.3;
  `terrainPixelError` 300/200/50 (код) — в дизайне 150/100/40, калибровка за пользователем;
  `terrainBasemapDistance` 12K/20K/80K; `shadowDistance` 500/1500/4000;
  `detailCullDistance` 4K/8K/12K; `fogScale` 1.3/1.0/0.6.
- `Assets/_Project/Scripts/World/ViewDistanceApplier.cs` — static-апplier: `camera.far`,
  `QualitySettings.lodBias/shadowDistance`, `heightmapPixelError/basemapDistance`, туман через
  `DayNightController`, culling через `DetailDistanceCuller`; мировых `Vector3` не хранит → FO-хук не нужен
  (`ViewDistanceApplier.cs:1-8,66-96,116-149`). Принцип: **«дальний террейн — фон, а не off»**
  (`docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md:9-29`); Ultra — disabled до 2+ сцен.
- Пост-эффект дали: `DistantFocusRenderFeature` (`FarStart = 800`, `FarEnd = 4000`, `AfterRenderingOpaques`)
  живёт внутри L0/L1 и с новой системой не конфликтует
  (`docs/world/optimization/README.md:20`, `docs/world/distantfocus/DESIGN_farfocus.md`).

## 6. Ранние прототипы чанков (deprecated — не используются)

> Указание пользователя: «то, что ранее когда-то пробовали прототипы чанков — это слишком ранние попытки —
> можно их игнорировать, они не используются». Фиксируем состав и причину, чтобы никто не воскресил их молча.

Файлы (все — `Assets/_Project/Scripts/World/Streaming/`):

| Файл | Роль | Ключевые константы |
|---|---|---|
| `WorldChunkManager.cs` | Реестр чанков по `WorldData`, `GetChunkAtPosition`, `GetChunksInRadius`, `GenerateCloudSeed` | `ChunkSize = 2000` (`WorldChunkManager.cs:83`) |
| `WorldStreamingManager.cs` | Координатор: `LoadChunksAroundPlayer`, preload-очередь, memory budget, scene-filter | `loadRadius = 2`, `unloadRadius = 3`, `updateInterval = 0.5f` (`WorldStreamingManager.cs:53-64`); `preloadLayers/delay/interval`, `maxLoadedChunks` (`WorldStreamingManager.cs:76-91`) |
| `ChunkLoader.cs` | `LoadChunk/UnloadChunk`, `CreateChunkRoot`, fade-in/out 1.5с, `ChunksContainer` | `fadeDuration = 1.5f`, `globalSeed` (`ChunkLoader.cs:28-36`); события `OnChunkLoaded/Unloaded` (`ChunkLoader.cs:43-46`) |
| `ProceduralChunkGenerator.cs` | Генерация гор (`MountainMeshGenerator`), облаков (`CumulonimbusCloud`), ферм (префаб/плейсхолдер) | `lodLevel` 0–2, сегменты 64/32/16, кольца 24/12/8 (`ProceduralChunkGenerator.cs:137-150`) |
| `PlayerChunkTracker.cs` | Серверный per-client трекинг + `LoadChunkClientRpc/UnloadChunkClientRpc` | `loadRadius = 2`, `unloadRadius = 3` (`PlayerChunkTracker.cs:22-27`); словари client→chunk/loaded/scene/transform (`PlayerChunkTracker.cs:40-62`) |
| `ChunkNetworkSpawner.cs` | Серверный спавн сундуков/NPC при загрузке чанка | `chestPrefab/npcPrefab`, `_spawnedObjects` (`ChunkNetworkSpawner.cs:22-52`) |
| `FloatingOriginMP.cs` | Legacy эвристический сдвиг мира (предшественник FO-слайса) | `threshold = 150000f`, `shiftRounding = 10000f` (`FloatingOriginMP.cs:92-96`); режимы Local/ServerSynced/ServerAuthority (`FloatingOriginMP.cs:62-70`) |
| `StreamingTest.cs`, `StreamingTest_AutoRun.cs` | Тестовые harness'ы | ссылки на `WorldStreamingManager` |
| `WorldSceneManager.cs` (см. §3) | Сцена→чанк координация | гистерезис FO 90K/70K |

Факты неиспользования (проверено 2026-09-27):

1. **Ни одна `.unity`-сцена не ссылается** на `WorldStreamingManager` / `WorldChunkManager` / `ChunkLoader` /
   `ProceduralChunkGenerator` / `PlayerChunkTracker` / `ChunkNetworkSpawner` / `FloatingOriginMP`
   (поиск по `*.unity` — 0 совпадений). Прототипы лежат кодом на диске, в сценах отсутствуют.
2. `FloatingOriginMigrationCensus.cs:49` относит `FloatingOriginMP | WorldSceneManager | WorldStreamingManager`
   к `legacy-origin` правилам — т.е. сама кодовая база уже считает их легаси.
3. Прототипы конфликтуют с тремя действующими решениями: authored `Terrain_0_0` (vs процедурная генерация гор),
   GlobalPilot/каталог 151 (vs бесконтрольный рантайм-спавн), NGO 2.13 `[Rpc(SendTo.X)]` (vs старые `[ClientRpc]` без `SendTo`
   в `PlayerChunkTracker.cs:334,357`).
4. Детальный разбор дефектов — в `02_DO_WE_NEED_CLIENT_CHUNK_LOADER.md` §5. Вывод: **не воскрешать, не чинить,
   не дорабатывать**; при необходимости чанков в будущем — проектировать заново от FO-контракта (условия — в `06_*`).

## 7. Floating Origin (действующий слайс — опора для §5 и 05_*.md)

- Транзакция `GlobalMotionControlledRebaseSlice`: F8 вручную, авто — отлёт >256 м от origin (опрос 5 с,
  кулдаун 35 с, галка `Auto Rebase Enabled`), F9 — откат. Корни сцен + игроки едут `position += T`
  с серверным `NetworkTransform.Teleport` (`NetworkPublished`); локальные кэши сдвигаются тем же `T` под флагами;
  вторым клиентам — broadcast `FO06_REBASE_SHIFT` (±T); маркеры `runtimeRebase.Completed` / `ActorRebound(ok=True)`.
  Источник: `docs/world/floatingorigin/README.md:9-17`.
- Две независимые подсистемы (ключ к пайплайну): **A — сдвиг мира**, **B — допуск при старте**
  (гейт `GlobalSceneNativeExecutor.BuildPreparation`: неизвестный scene-placed `NetworkObject` без
  `GlobalSceneSourceMarker` + записи каталога + сходящегося digest → Host fail-closed
  `uncontrolled_network_source_before_native_sweep`). Источник: `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md:22-45`.
- Каталог пилота: 151 запись (прецедент парома `ParomRoute_01`, `T-PAROM-01`), тулза
  `ProjectC/World/Floating Origin/Bake Pilot Catalog (Preview + Apply)`
  (`Assets/_Project/Editor/FloatingOrigin/BakePilotSceneCatalog.cs`), алгоритмы
  `AuditGlobalSceneCatalog.LayoutHash` (`09S_NEW_CONTENT_PIPELINE.md:137-160`).
- Контракт транзакции: closed participant set, фазы REQUEST → FREEZE → PREFLIGHT → CAPTURE → APPLY →
  REBUILD → VALIDATE → PUBLISH → RELEASE, инварианты (в т.ч. `FloatingOriginMP` не включается,
  общий `SetParent` для NO/ship/city запрещён) — `docs/world/floatingorigin/06N_REBASE_TRANSACTION_CONTRACT.md:22-80`.
- Мультиплеер v2 (топология, late join, owner-rules) — dormant, `06DH`
  (`docs/world/floatingorigin/README.md:43-48`). Данный ресёрч не проектирует v2, но учитывает его границы в `04_*`.

## 8. Что из этого следует (мостик к остальным документам)

- Стриминг **сцен** есть и работает (ClientSceneLoader + серверная координация); стриминга **чанков** нет
  (и не нужен в legacy-виде) — путать их нельзя (`02_*`).
- Картинка хорошая благодаря **монолитному террейну + туману + ViewDistance**, а не чанкам;
  дальние города — это задача **импосторов/LOD**, а не выгрузки (`03_*`).
- Сеть уже имеет per-client зачатки (сцены + Hide/Show), но чанк-RPC legacy-схемы масштабировать нельзя (`04_*`).
- Любой новый контент с мировыми координатами — по `09S`-пайплайну; чанки не исключение (`05_*`).
