# 06 — Пайплайн контента: Blender → FBX → Unity-импорт → префаб → сцена

Дата 2026-11-01. Метод: только чтение (доки, `.fbx.meta`, YAML сцены, Editor-скрипты); проект/сцена/Blender не менялись. `[UNVERIFIED]` — не подтверждено файлом. Смежное: `05_*` (меши), `04_*` (свет), `03_*` (корабли).

## 0. Факт со сцены `WorldScene_0_0` (замер YAML)

| Метрика | Значение |
|---|---|
| GameObject / Transform / корней сцены | **4 590** / 4 590 / **28** |
| `WorldRoot_0_0` / PrefabInstance-блоков (`!u!1001`) | **3 513** объекта, 20 детей / **4 906** |
| Двери `PM_DOOR_*` / лампы `PM_LIGHT_*` | **2 597** в `PRIMUM_DOORS/<чанк>/` + 133 в `ADMIN_DOOR_PREFABS` / **2 238** в `PRIMUM_LIGHTS/<чанк>/` |
| `PRIMUM_COLLIDERS` / `PRIMUM_CITY.prefab` | 27 узлов-чанков, 1 376 объектов, слой **8** / **1** инстанс под `WorldRoot_0_0` |

Дети `WorldRoot_0_0`: `PRIMUM_COLLIDERS`, `PRIMUM_DOORS`, `PRIMUM_LIGHTS`, `Primum` (MarketZone_Primium + RepairManager), `Primum_farms` (**1 450** объектов), `Primum_farms_DOORS`, `Terrain_0_0`, `Quartus`/`Secund`/`Tertius` (по 37), `RuinsValleys` (497), `ParomRoute_01` + якоря, `Airways`, `CompassRose_North`, `SceneLabel_0_0`, `WindZones`. Дома/двери/лампы/коллайдеры живут **внутри `PRIMUM_CITY.prefab`** (узлы `PRIMUM_*`), а не детьми сцены; **префабов домов нет** — дом лишь батч внутри FBX чанка.

## 1. Blender-часть

### 1.1 Коллекции и имена (HARD)
`.opencode/rules/blender-mcp.md:10–27`: `CITY/{00_TERRAIN,20_PLATFORMS,30_PLAZA,40_ADMIN,50_CIRCULATION,70_INFRA_TECH,99_REFERENCE}`; дом = `60_BUILDINGS/<ЗОНА-АЛЛЕЯ>/<ДОМ>/{05_FLOOR,10_WALLS,20_ENTRANCE,30_WINDOWS,40_INTERIOR,50_ROOF,60_DECOR,80_PASSAGE,90_MARKER}`; кварталы — `55_QUARTERS/…`. Под-коллекция `<ДОМ> - <NN_ГРУППА>` (имена коллекций глобально уникальны), объект `<ДОМ>_<КАТЕГОРИЯ>_…`, 1 дом = 1 префикс. После батча — `organize_buildings()`; префикс зоны матчится **по границе `_`** (иначе `RES_NE_01` уезжает в зону `RES_N`).

### 1.2 Единицы и масштаб
**1 unit = 1 m**: двери 2.10 × 0.95 м = жилая норма, герой 1.96 м, `CC.height` 1.80–2.00 (`02_PIPELINE/01_SCALE_DECISION.md:10–31`). Масштаб правится **только в Blender** и только у мешей (пустышки не масштабировать); масштаб на инстансе в сцене запрещён (`README.md:111–117`). Применён **k = 1.06** (`01_SCALE_DECISION.md:75–104`: 1605×97×1024 → 1701×103×1085 м, двери 2.10→2.23 м), но `README.md:73,114` пишет **1.07** → §7.1. Корень города: `(41 000, 2 474.56, 40 000)`, rotY 180°, **scale 1** (`01_SCALE_DECISION.md:101`).

### 1.3 Пивот, ориентация, экспорт
| Параметр | Значение | Источник |
|---|---|---|
| Пивот двери / меша чанка | низ по центру / у дома-чанка; ставить **после** масштаба | `README.md:115`, `01_SCALE_DECISION.md:106–109` |
| Оси FBX | `axis_forward='-Z'`, `axis_up='Y'`, `FBX_SCALE_NONE`, `global_scale=1.0`, `use_mesh_modifiers=True`, `object_types={'MESH','EMPTY'}` | `05_SCRIPTS_AND_BACKUPS.md:57` |
| Как приходит в Unity / координаты | поворот **X≈270°**, масштаб **×100** (компенсирует `useFileScale`); мировые Blender, корень блока в (0,0,0) | `120_RES-ALLEY_REIMPORT_UV.md:101`, `02_BLOCKS_AND_GEOMETRY.md:39–40` |

### 1.4 Требования к мешу
1. **Нормали:** знаковый объём по **связным островам** (`V=Σdet(a,b,c)/6`), `V<0` → `reverse_faces` по острову; гейт — 0 вывернутых (`02_NORMALS_POSTMORTEM.md:32–41,62–77`; прецедент — 542 043 развёрнутых грани).
2. **Толщина:** вырожденных мешей нет — плита/планка имеет толщину по нормали (`blender-mcp.md:31–33`).
3. **UV/вершины:** `smart_project(angle_limit=66°, island_margin=0.02)` **после join**, слой `UVMap` (`120_…:53–73`); ≤ **65 535** вершин на меш, иначе резать или UInt32 (`02_BLOCKS_AND_GEOMETRY.md:29`).
4. **Join:** блок × материал; не склеивать стекло/эмиссию/воду; флэтнить иерархию; ориентир ≤ 80k тр./блок.
5. **Материалы/пересечения:** имя = ключ подмены, пустых слотов нет, 16–38 материалов на чанк (`03_UNITY_ASSEMBLY.md:110–119`); вывески/навесы/окна не проходят сквозь кровлю и соседей (clash-audit).

### 1.5 `.blend`, поставка, бэкапы (`02_PIPELINE/05_SCRIPTS_AND_BACKUPS.md:3–59`)
`Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder\`: `PRIMUM_chunks_RAW/` (**только чтение**, 23 чанка), рабочие бленды `PRIMUM_chunks_reimport_doors/` (актуальные) и `PRIMUM_chunks/`, поставка FBX `_deliver_doors/` (v13; `_deliver/` — 1-е поколение, не использовать), бэкапы `_versions/` (`*_before_*_swap_<дата>.fbx`, `*_pre_fix_normals_*`, terrain), скрипты/логи `_work/` (`run_all_doors_v13.py`, `fix_normals2.py`, `uv_unwrap_export.py`), Blender `W:\SteamLibrary\...\blender.exe` 5.2.2 LTS headless `-b --factory-startup --python`. Процесс: RAW не менять, атрибуты не снимать, бэкап-версия после каждого батча; перестройки — батчами 15–30 домов; `CITY_CHECK_V4` перед сдачей.

## 2. Unity-импорт

### 2.1 ModelImporter — замер по `.fbx.meta` (чанк города, BASE, ферма ×2, корабль Light)
| Поле `.meta` | Город | Фермы/корабли | Комментарий |
|---|---|---|---|
| `globalScale` / `useFileScale` / `useFileUnits` | **1 / 1 / 1** | 1 / 1 / 1 | без Convert Units модель ×100 (`03_UNITY_ASSEMBLY.md:15`) |
| `meshCompression` / `isReadable` / `addColliders` | **0 / 0 / 0** | 0 / 0 / 0 | без компрессии; вершины не читаем; коллайдеры строим осознанно (§2.3) |
| `materialImportMode` / `materialLocation` / `materialName` / `materialSearch` | **1** (Standard) / 1 / 0 / 1 | **2** (ImportViaMaterialDescription) / 1 / 0 / 1 | режимы разные; материалы остаются в модели |
| `animationType` / `importAnimation` | **0** (None) / 1 | **2** (Generic) / 1 | у статики Generic |
| `generateSecondaryUV` / `swapUVChannels` / `keepQuads` / `weldVertices` | **0** / 0 / 0 / 1 | 0 / 0 / 0 / 1 | лайтмапов нет → lightmap UV не генерим |
| `normalImportMode` / `tangentImportMode` / `indexFormat` | 0 / 3 / 0 | 0 / 3 / 0 | тангенты считаются; UInt16 |
| `bakeAxisConversion` / `preserveHierarchy` / `sortHierarchyByName` / `optimizeMesh` / `useSRGBMaterialColor` | **0** / 0 / 1 / on `[UNVERIFIED]` / 1 | 0 / 0 / 1 / on / 1 | ось печёт меню 388, не импортёр |

### 2.2 Материалы при импорте
Импорт даёт материалы **из FBX** (`URP/Lit`); после Э3.1 в городе+фермах **0 слотов `URP/Lit`**, 53 материала. Подмена **по имени** источника: `09_MATERIALS/material_map.csv` (105 имён → 45 целей, 18 семейств) + `Editor/Primum/PrimumMaterialMap.cs`, меню 380–385 (отчёт/применить/verify/варианты/по чанку). Цели — `M_PC_*`, эмиссия — `PM_LAMP_GLOW_WARM`.

### 2.3 Коллайдеры
Дома — **боксы по конструктиву**, не меши: 10 355 `BoxCollider` (7 715 стен / 1 652 плит / 760 крыш / 228 маршей) + BASE 74 + ADMIN 382 = **10 817** в `PRIMUM_COLLIDERS`; мебель/окна/декор в физику не идут, проём двери **вырезается** из бокса (лево/право/над) — `04_COLLIDERS/05_HOUSES_BOXES.md:3–40`. Данные — `04_COLLIDERS/houses_boxes.json` (Blender `tools/houses_boxes.py`), сборка `PrimumHouseColliders.cs` (меню 360/365), группы `HOUSE_{WALL,FLOOR,ROOF,STAIR}_BOX`, идемпотентно. Корабли: 250 боксов + 9 `DoorController` **выносятся из FBX-инстанса** в дети префаба, иначе реимпорт снесёт (`10_CULLING_RESEARCH/09_…:124–127`).

### 2.4 Папки (факт)
`Art/Models/Primum/Chunks/*.fbx` — 23 чанка (GUID стабильны); `Art/Models/Primum/Rebased/<чанк>/` — Y-up меши (меню 388); `Art/Models/{Primumfarm_0_0, Ships/}` — FBX ферм/кораблей; `Prefabs/City/{PRIMUM_CITY.prefab, Doors/, Lights/, Colliders/, Farms_Merged/}`; `Prefabs/Ships/*.prefab` — 24 префаба; `Generated/<Root>_Combined.asset`.

## 3. Сборка префаба

1. FBX в проект (`useFileScale` on, `globalScale` 1) → сверка габаритов Δ ≤ 0.02 м (`03_UNITY_ASSEMBLY.md:22`).
2. **Пост-проход `PRIMUM_CITY.prefab`** (`LoadPrefabContents → SaveAsPrefabAsset`): слой `PRIMUM` (8) рекурсивно; `DOORS` выключить, `LAMPS` включить, `DOOR_PARTS_UNASSIGNED` включить, `OUTLIERS` выключить (`04_REIMPORT_SWAP_AND_POSTPASS.md:38–50`).
3. **Y-up rebase** (меню 388) — обязателен после подмены FBX: вершины/нормали/тангенты в пространство родителя, пивот в центр, scale 1; ассеты в `Art/Models/Primum/Rebased/<чанк>/`, verify «1:1 PASS» (`PrimumMeshRebase.cs:14–31`).
4. Материалы — меню 384 → 385 (идемпотентно). Новое ставить префаб-инстансом, не правкой модели.

### 3.1 Слои (`ProjectSettings/TagManager.asset`) и флаги
| Слот | Имя | Что |
|---|---|---|
| 8 / 9 | **PRIMUM** / PCMM_Atmosphere | городские батчи, коллайдеры, световые префабы / атмосфера |
| **10** / 31 | **PRIMUM_DOORS** / ZoneCollider | все `PM_DOOR_*` (корень и дети) — `PrimumDoorLayerTool.cs:18–19` / зоны |
| 11–14 | PRIMUM_PROPS, INTERIOR, FARMS, RUINS | **план** `09_…:194–206` (не созданы) |

Теги: `Ship`, `HUDCanvas`. **Static-флаги = 0 везде** (`m_StaticEditorFlags: 0`), Static Batching off; ContributeGI/Occluder **не включать** — FO двигает мир, baked world-space ломается (`01_SETUP/04_LOD_AND_DISTANCE.md:50–52`).

### 3.2 Имена (факт) и компоненты
`PM_DOOR_SINGLE_{PLANK_A,PANEL_B,ARCH_C,NARROW_A,WIDE_A}`, `PM_DOOR_DOUBLE_A`, `PM_DOOR_PALACE_DOUBLE_{A,B,C}`, `PM_DOOR_GATE_{A,B}`, `PM_ADM_ENTRANCE_PORTAL`, `PM_LIGHT_{STREET_POST,STREET_WALL,FACADE_WALL,FACADE_SCONCE,INTERIOR_PENDANT,INTERIOR_SCONCE,INTERIOR_TABLE}`, `PRIMUM_BC_<чанк>` (коллайдеры), `PM_FRM_*` (фермы), чанки `PRIMUM_{HOUSES,ADMIN,BASE}_*`; узлы модели `DOORS/DOORS_BIG/LAMPS/DOOR_PARTS_UNASSIGNED/OUTLIERS`. ⚠️ Префиксов `GATE_*`, `SHIP_*` **нет** (есть лишь файлы `Ship_havyII_base`, `Ship_medium_base`, `Ship_Light_root_fbx_test`). Дверь = `DoorController` + `BoxCollider` + дети `Leaf`/`LeafWood`/`Frame`, слой 10; свет = только `Light` (+`PrimumLightPool`), корпус уже в модели; корабль (`ShipPresetCreator`): `Rigidbody`, `NetworkObject`, `NetworkTransform`, `ShipController/FuelSystem/ModuleManager/Hull/RootReference/InputReader`, `NpcShipController`, `ShipDeckNav`, `Slot_*`, `MainVisual` (scale 1 на корне), tag `Ship`. HLOD — манифест `Assets/_Project/Resources/HLOD/PRIMUM_HLOD.asset` (`PrimumHlodBaker`).

## 4. Размещение в сцене

| Что | Как/чем |
|---|---|
| Двери | префаб-инстансы по `03_DOORS/registries/doors_registry_<чанк>.csv`; `PrimumDoorPlacement` меню 301 (**сама сохраняет сцену**), 300/302/303 аудит/очистка/verify |
| Лампы / коллайдеры | из узлов `LAMPS` модели (`PrimumLampLighting`, меню 361, verify 364) / из `houses_boxes*.json` (меню 360/365, BASE 350/340) |
| Город (батчи) / корабли | инстанс `PRIMUM_CITY.prefab` под `WorldRoot_0_0` (**не** держать разложенным) / префаб-инстанс **в корне сцены** (`Ship_*` — `m_TransformParent: 0`) |
| Ферма/порт | `Tools → ProjectC → Create Port Station…` (`PortStationCreator`); эталон `WorldRoot_0_0/Primum_farms/Средняя 0_0` |

### 4.1 Floating Origin — два независимых гейта (`floatingorigin/09S_NEW_CONTENT_PIPELINE.md`)
| # | Подсистема | Правило |
|---|---|---|
| **A** сдвиг | едут дети корней `WorldScene_*` | 🟢 под `WorldRoot_*` бесплатно; 🟡 кэш мировых `Vector3` → `ApplyRebaseTranslation`; 🔴 рантайм-спавн вне корней → регистрация участником; 💾 сейвы → кумулятив |
| **B** допуск при старте | цензит **только `NetworkObject`** | маркер `GlobalSceneSourceMarker` (`sourceId = sceneGuid:objectId:prefabId`, руками не выдумывать) + observation + entry в `GlobalMotionPilotSceneCatalog` + сходящийся digest, иначе Host падает `uncontrolled_network_source_before_native_sweep` |

Без `NetworkObject` (декор, статик, лампы, двери) — только A, каталог не трогаем; корабли/паром — ветка B.

### 4.2 Стриминг, сейв, bake
`SceneRegistry.asset`: `GridColumns 6`, `GridRows 4`, `SceneNamePrefix WorldScene_`, `UseAdditiveLoading 1` (две копии: `Data/Scene/` и `Resources/Scene/`). `WorldScene_0_0` — 61.4 МБ (весь контент), остальные 23 — шеллы 36–45 КБ (`WorldRoot_<x>_<z>`, `GroundPlane_`, `Boundaries_`, `SceneLabel_`). ⚠️ Имя файла и корень **переставлены**: `WorldScene_0_1` = `WorldRoot_1_0`, `WorldScene_5_3` = `WorldRoot_3_5`. Новый контент — в `WorldScene_0_0`.

Сейв: после пост-прохода + rebase + материалов, когда `dirty` снят и 0 errors; подмена FBX делает сцену dirty → `MarkSceneDirty`+`SaveScene` (`04_…:32`); меню 301 сохраняет сцену само. Bake-тулза `Editor/FloatingOrigin/BakePilotSceneCatalog.cs`, меню `ProjectC/World/Floating Origin/Bake Pilot Catalog (Preview + Apply)`: скан NO/маркеров → treatment/ownership → превью diff → apply **только после непустого `reviewNote`** → пересчёт `layoutHash`/`dependencyHash`/digest → ре-верификация (`DryRun()` — read-only). Ассеты `Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset` (**152** поля `reviewNote` ≈151 запись) + `…Profile.asset`. Структурный дрейф тулза не чинит; каталог — снапшот (сейв после пересчёта делает `dependencyHash` stale).

## 5. Чек-лист «ассет готов»

- [ ] Blender: коллекции по стандарту, имена `<ДОМ>_<КАТЕГОРИЯ>_…`, `organize_buildings()` прогнан
- [ ] нормали по островам → **0** вывернутых; нет вырожденных мешей и прошиваний (clash-audit)
- [ ] UV `smart_project` **после** join; ≤65 535 вершин/меш; материалы без пустых слотов, имена = ключи
- [ ] k применён к мешам **в Blender**, пивот выставлен после масштаба
- [ ] экспорт `FBX_SCALE_NONE / -Z / Y / global_scale 1 / MESH+EMPTY`; файл в `_deliver_doors/`, бэкап в `_versions/`
- [ ] Unity: подмена по тому же пути/имени (GUID цел), `.meta` не трогать, MD5 стейджа = MD5 в проекте
- [ ] импорт: `useFileScale` on, `globalScale` 1, `meshCompression` off, `isReadable` off, `addColliders` off, `generateSecondaryUV` off
- [ ] пост-проход `PRIMUM_CITY.prefab`: слой `PRIMUM`/`PRIMUM_DOORS`, `DOORS` off, `LAMPS` on, `OUTLIERS` off
- [ ] Y-up rebase (388) → verify 1:1 PASS; материалы 384→385 → verify 0 `URP/Lit`
- [ ] размещение префаб-инстансом под корнем; есть `NetworkObject` → маркер + observation + entry + digest
- [ ] сцена: 0 errors, `dirty` снят, `AuditGlobalSceneCatalog` без новых расхождений
- [ ] руками (пользователь): F8 → объект на месте + `runtimeRebase.Completed`; F9 → возврат

## 6. Антипаттерны

1. **Править `.meta`/`.asmdef` руками** — ломает GUID; при подмене FBX `.meta` не трогать вообще.
2. **Масштаб на инстансе** (1.06 на корне) и **`globalScale` ≠ 1** пофайлово (историческое 0.36…10.64) — «скрытый масштаб», наследуется дверями/коллайдерами.
3. **Держать город разложенным в сцене** (167 969 → 20 254 рендерера) — только `PRIMUM_CITY.prefab`.
4. **Ручные правки внутри FBX-инстанса** (коллайдеры, компоненты, `SetActive`) — реимпорт стирает.
5. **Static-флаги / Batching / ContributeGI / Occluder** на контенте — FO двигает мир; Umbra/PVS bake в world-space и static-рендеры запрещены (`09_…:238`).
6. **Scene-overrides как мерж** (174 001 override / 34.3 МБ / 54.6 % сцены, невоспроизводим) и **runtime `CombineMeshes`** — мерж только edit-time в коммитируемые `.asset`.
7. **Километровые координаты в локальных полях**, кэш мировых `Vector3` без `ApplyRebaseTranslation`.
8. **Объекты вне корней сцен** без регистрации; новый scene-placed `NetworkObject` без маркера/каталога.
9. **Слой вместо гранулярности** (21 слой ≠ 577 домов) и **дробление города на ячейки 10–30 м** (+16…+47 % слотов).
10. **Клеить фермы в 1 меш** (−280 DC за +1.27 млн тр/кадр) и **«всё в 1 меш»** на корабле (убивает разрушаемость, lightprobes, покомнатную видимость).
11. **FBX с 900+ объектами как есть** — приёмка: ≤24 слота статик-визуала, ≤40 GO вне визуала, ≤120 примитивных коллайдеров, ≤60k тр. LOD0.
12. **Менять** `Terrain`, `PRIMUM_COLLIDERS`, `DoorController`, `PrimumLightPool`, `NetworkManager`, `ClientSceneLoader`, `ScenePlacedObjectSpawner` в контентных чейнжах.

## 7. Расхождения / риски

| # | Риск | Факт |
|---|---|---|
| 1 | **k = 1.06 vs 1.07** | `01_SCALE_DECISION.md:75` фиксирует применённый **1.06** (1701×103×1085 м), `README.md:73,114` — **1.07**; для x100 нужен один канон (двери 2.23 vs 2.25 м + пересчёт коллайдеров) |
| 2 | **Корень города в доках ≠ в сцене** | доки — `WorldRoot_0_0/PRIMUM` (`README.md:71`); факт — инстанс виден как `Primum` (слой 0, дети `MarketZone_Primium` + `RepairManager`), `PRIMUM_COLLIDERS/DOORS/LIGHTS` — отдельные дети `WorldRoot_0_0` (слой 8). Инструменты ищут `PRIMUM` с фолбэком `WorldRoot_0_0` (`PrimumDoorPlacement.cs:34–35`) → **имя корня — контракт** |
| 3 | **Счётчики в доках устарели** | двери 2 161 vs **2 597** (+133 ADMIN), свет 2 087 vs **2 238**; для x100 считать по сцене |
| 4 | **Префабов домов нет** | дом = батч `<чанк>_M_<материал>` в FBX чанка (23 чанка, 26–228 м); «дома x100» требуют **нового** пайплайна (префаб на дом + инстансинг), иначе рост только чанками — главный риск тома |
| 5 | **Слой ферм не назначен** | `FARMS` (9) зарезервирован (`03_UNITY_ASSEMBLY.md:29`), фактически фермы на слое 0, план culling предлагает `PRIMUM_FARMS` = **13** (`09_…:194–206`) — три источника истины |
| 6 | **FBX/префабы, вероятно, вне git** | `120_…:141`: `Art/*`, `*.prefab` в `.gitignore` → «мерж невоспроизводим» (`09_…:186–190`); нужен бинарный VCS (Lore/LFS) или исключение |
| 7 | **Импорт неоднороден** | `materialImportMode` 1 vs 2, `animationType` Generic у статики `[UNVERIFIED]`; **Y-up rebase — ручной шаг** после каждой подмены (забыть = материалы в чужом пространстве) |
| 8 | **Масштаб сцены** | 61 МБ, 4 906 prefab-instance; ×100 ≈ 490k инстансов при бюджетах L0 1.5–3k DC, L1 ≤500, L2 ≤10 (`09_…:221`); `LODGroup` в городе фактически **0** — L1/L2 не начаты |
| 9 | **Каталог пилота — снапшот** | сейв после пересчёта делает digest stale; массовый контент = массовый bake + `reviewNote` на каждую запись (не автоматизируется) |

## 8. Первоисточники

`docs/world/primum_city/` (`README.md`, `01_SETUP/*`, `02_PIPELINE/*`, `03_DOORS/README.md`, `04_COLLIDERS/05_HOUSES_BOXES.md`, `08_LIGHTING/01_LIGHTING.md`, `09_MATERIALS/material_map.csv`, `10_CULLING_RESEARCH/{09_…,10_…}.md`, `120_RES-ALLEY_REIMPORT_UV.md`); `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md`; `docs/world/PLACEMENT_SCRIPTS/{FARMS_PREFAB,Ships}/README.md`; `.opencode/rules/blender-mcp.md`; `Assets/_Project/Editor/{Primum/*.cs, PrimumDoorLayerTool.cs, Culling/PrimumHlodBaker.cs, FloatingOrigin/BakePilotSceneCatalog.cs}`; `ProjectSettings/TagManager.asset`; `Assets/_Project/Scenes/World/WorldScene_0_0.unity`; `Assets/_Project/Data/Scene/SceneRegistry.asset`.
