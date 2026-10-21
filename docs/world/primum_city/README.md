# PRIMUM CITY — индекс документации

**Каталог:** `docs/world/primum_city/` · **Обновлён:** 2026-10-21
**Статус:** город импортирован и работает; исходные группы дверей **выключены** (кроме 73 сборок
витрин/лавок, возвращённых в фасады); двери **расставлены — 2 161 инстанс** (2 051 одиночная +
110 двустворчатых) в `WorldRoot_0_0/PRIMUM_DOORS` — см. `03_DOORS/03_MASS_PLACEMENT.md` (ред. 2).

> 📍 **Куда идём и в каком порядке — `ROADMAP.md`** (дорожная карта: бюджеты, что закрыто,
> предложенный порядок этапов Э1…Э8, Definition of Done, открытые решения).
> Закрыто: **Э1 — коллайдеры** (7 428 боксов, `04_COLLIDERS/03_COLLIDERS_V2.md`) и **Э3 — свет**
> (`08_LIGHTING/01_LIGHTING.md`). Следующее по плану: **Э2 — остальные типы дверей**, затем перф (Э4–Э6).

> Это точка входа. Раньше документы лежали плоским списком `01…16` — теперь разложены по каталогам;
> соответствие старых и новых имён сохранено в `05_HISTORY/`.

---

## 1. Что где лежит

| Каталог | Содержимое |
|---|---|
| **`ROADMAP.md`** | 📍 **дорожная карта**: глобальная задача и бюджеты, факты на сегодня, порядок этапов Э1…Э8, Definition of Done, открытые решения |
| **`01_SETUP/`** | как город устроен и импортирован: инвентарь и чистка, блоки/геометрия, сборка в Unity, LOD и дальность |
| **`02_PIPELINE/`** | конвейер Blender→Unity: решение о масштабе k=1.07, нормали/виндинг, **правила дверей v13**, процедура подмены FBX, скрипты и бэкапы |
| **`03_DOORS/`** | **двери**: текущее состояние и числа, реестры (`registries/`), **план префабов и расстановки** |
| **`04_COLLIDERS/`** | ✅ **коллайдеры (Э1)**: `03_COLLIDERS_V2.md` — 7 428 боксов (WALK 9 / STAIR 273 / WALL 4 846 / FLOOR 1 700 / ROOF 600), правила, скрипты в `tools/`, CSV; история — `01`, `02` |
| **`05_HISTORY/`** | журнал итераций, восстановление после аварии, результаты фаз, **журнал дверей v5…v13**, постмортемы |
| **`06_REFERENCE/`** | индустриальная практика, Floating Origin/сеть/геймплей, приёмка |
| **`07_STATE/`** | актуальное состояние и точка продолжения |
| **`08_LIGHTING/`** | **свет (Э3)**: префабы `PM_LIGHT_*`, emissive, бюджет источников, `lamps_audit.csv`, `placement_manifest.csv` |
| **`data/`** | сырые данные: `doors_manifest.csv` (3 004 исходные двери), `colliders.csv` |

## 2. Актуальные документы (читать в этом порядке)

1. **`ROADMAP.md`** — 📍 что делаем и в каком порядке (начинать с него).
2. **`03_DOORS/README.md`** — состояние дверей: сколько, где, какие гейты пройдены, что осталось.
3. **`02_PIPELINE/03_DOORS_PIPELINE_V13.md`** — правила группировки дверей (v13.8), гейты, как запускать.
4. **`03_DOORS/01_PREFAB_PLAN.md`** — план: типы префабов, контракт пивота, приёмка.
   **`03_DOORS/02_SINGLE_DOOR_PREFABS.md`** — ✅ 3 одиночных префаба (спецификация, иерархия, материалы,
   коллайдер + `DoorController`).
   **`03_DOORS/04_DOUBLE_DOOR_PREFAB.md`** — ✅ двустворчатый `PM_DOOR_DOUBLE_A` + генератор префабов.
   **`03_DOORS/03_MASS_PLACEMENT.md`** — ✅ **расстановка 2 161 двери** (ред. 2): алгоритм, сопряжение
   пар створок, витрины, инструмент, проверка, ограничения.
5. **`02_PIPELINE/04_REIMPORT_SWAP_AND_POSTPASS.md`** — как подменять FBX 1:1 и что при этом ломается.
6. **`02_PIPELINE/05_SCRIPTS_AND_BACKUPS.md`** — где лежат скрипты, логи, бэкапы; как запускать воркеры.
7. **`08_LIGHTING/01_LIGHTING.md`** — ✅ **свет (Э3)**: префабы `PM_LIGHT_*`, emissive, `PrimumLightPool`,
   бюджет источников, что пропущено и почему; данные — `lamps_audit.csv`, `placement_manifest.csv`.
8. **`04_COLLIDERS/03_COLLIDERS_V2.md`** — ✅ **коллайдеры (Э1)**: 7 428 боксов из исходной геометрии
   (WALK 9 / STAIR 273 / WALL 4 846 / FLOOR 1 700 / ROOF 600), правила классов, скрипты, проверки, откат.

## 3. Сводка текущего состояния (на 2026-10-21)

| Параметр | Значение |
|---|---|
| Чанков города | **23** (18 исходных + 2 SE + 3 торговых), `Assets/_Project/Art/Models/Primum/Chunks/*.fbx` |
| Корень города | `WorldRoot_0_0/PRIMUM` — инстанс `Assets/_Project/Prefabs/City/PRIMUM_CITY.prefab` |
| Позиция/поворот корня | (39 460.0, 2 475.67, 40 000.0), rotY 180°, scale 1, слой `PRIMUM` (8) |
| Масштаб моделей | k = **1.07** относительно RAW (применён в Blender к мешам) |
| Двери (исходные группы) | **3 038** в узлах `DOORS` каждого чанка, **все выключены**; пивот = низ по центру |
| Двери (расстановка) | **2 161** префаб-инстанс в `WorldRoot_0_0/PRIMUM_DOORS/<чанк>/` (PLANK_A 930, PANEL_B 750, ARCH_C 371, DOUBLE_A 110); `static = false` |
| Светильники | **2 045** кластеров в узлах `LAMPS` → **1 453 световых префаба** в `WorldRoot_0_0/PRIMUM_LIGHTS`; горят ≤ 96 одновременно (пул), корпуса — emissive `PM_LAMP_GLOW_WARM` |
| Конструктив | остаётся в батчах `<чанк>_M_<материал>` / `<чанк>_INT_<материал>`; в двери не вклеивается |
| Непривязанные детали | узел `DOOR_PARTS_UNASSIGNED` (включён, видим) |
| Улетевшие объекты | узел `OUTLIERS` (выключен): 75 объектов + 20 гигантских колец ADMIN |
| Реестры дверей | `03_DOORS/registries/doors_registry_<чанк>.csv` — 23 файла |
| Мешей/рендереров города | **16 468** (было 147 715); на чанк 6 … 6 388 (среднее 716) — регресс, цель 150–400 (`ROADMAP.md` Э4) |
| **Физика** | ✅ **7 428 боксов** в `WorldRoot_0_0/PRIMUM_COLLIDERS` (WALK 9 — низ города 8+1, STAIR 273 — наклонные, WALL 4 846 — окна залиты, FLOOR 1 700, ROOF 600) + `TerrainCollider` + 4 432 коллайдера дверей; `PAVING` MeshCollider **выключен**; `static = false`. Детали — `04_COLLIDERS/03_COLLIDERS_V2.md` |
| Рендер-путь | SRP Batcher вкл; **GPU Resident Drawer выкл** (откат из-за шейдеров, `ROADMAP.md` Э5); Static Batching off |
| LOD | **0** `LODGroup` — L0/L1/L2 не начаты (`ROADMAP.md` Э6) |
| Материалы | 100 имён-заглушек `Universal Render Pipeline/Lit`; подмена пулом не сделана (`ROADMAP.md` Э4) |

## 4. Где мы сейчас и что дальше

**Порядок работ и критерии — в `ROADMAP.md`** (этапы Э1…Э8). Кратко на сегодня:

| Этап | Что | Статус |
|---|---|---|
| Э1 | **Коллайдеры: минимальные боксы** (8 боксов низа + диагональные лестницы + стены/полы/крыши) | ✅ сделано — `04_COLLIDERS/03_COLLIDERS_V2.md` |
| Э2 | Остальные типы дверей (админ/дворцы 288, ворота 40, витрины 22+73, узкие 63) | ⏳ |
| Э3 | **Свет**: 7 префабов `PM_LIGHT_*`, emissive, бюджет 96 источников (ADMIN/дворцы не тронуты) | ✅ сделано — `08_LIGHTING/01_LIGHTING.md` |
| Э4 | Зернистость пасс 2 (`TRD-S2` 6 388 мешей) + пул материалов + облегчение префабов дверей | ⏳ |
| Э5 | GPU-путь: шейдеры → DOTS-инстансинг → GPU Resident Drawer | ⏳ |
| Э6 | LOD L1/HLOD + L2-импостор (решить: по чанкам или сначала нарезать блоки) | ⏳ |
| Э7 | Интерьеры отдельными префабами (вариант B) | ⏳ |
| Э8 | Чистка: `OUTLIERS`, заглушки дверей, легаси `Primum`, `NetworkPrefabsList`, NavMesh, порядок в docs | ⏳ |

Открытые вопросы к заказчику — `ROADMAP.md` §6.

## 5. Жёсткие правила работы (не нарушать)

* **RAW не менять**: `Q:\…\New Folder\PRIMUM_chunks_RAW\` — только чтение; рабочие копии — в новый каталог версии.
* **Атрибуты файлов не снимать** — делать копию содержимым в новый подкаталог.
* Перед подменой FBX — бэкап в `_versions/`.
* Масштаб k = 1.07 применять **только к меш-объектам** (пустышки не масштабировать).
* Двери: пивот = низ по центру; имя = `<исходное имя>_<W>x<H>_X`.
* Любая подмена FBX **стирает** ручные правки внутри объектов модели (коллайдеры, скрипты, выключение) —
  только скриптовый пост-проход или отдельные объекты вне модели.

## 6. Соответствие старым именам

| Старое имя (до реорганизации) | Где теперь |
|---|---|
| 01_INVENTORY_AND_CLEANUP.md | `01_SETUP/01_INVENTORY_AND_CLEANUP.md` |
| 02_BLOCKS_AND_GEOMETRY.md | `01_SETUP/02_BLOCKS_AND_GEOMETRY.md` |
| 03_UNITY_ASSEMBLY.md | `01_SETUP/03_UNITY_ASSEMBLY.md` |
| 04_LOD_AND_DISTANCE.md | `01_SETUP/04_LOD_AND_DISTANCE.md` |
| 05_FO_NETWORK_AND_GAMEPLAY.md | `06_REFERENCE/02_FO_NETWORK_AND_GAMEPLAY.md` |
| 06_ACCEPTANCE.md | `06_REFERENCE/03_ACCEPTANCE.md` |
| 07_INDUSTRY_PRACTICE.md | `06_REFERENCE/01_INDUSTRY_PRACTICE.md` |
| 08_GRANULARITY_COLLIDERS_INTERIORS.md | `04_COLLIDERS/02_GRANULARITY_COLLIDERS_INTERIORS.md` |
| 09_PHASE2_RESULTS.md | `05_HISTORY/03_PHASE2_RESULTS.md` |
| 10_PHASE3_RESULTS.md | `05_HISTORY/04_PHASE3_RESULTS.md` |
| 11_SCALE_DECISION.md | `02_PIPELINE/01_SCALE_DECISION.md` |
| 12_COLLIDERS.md | `04_COLLIDERS/01_COLLIDERS.md` |
| 13_RECOVERY_RESEARCH.md | `05_HISTORY/02_RECOVERY_RESEARCH.md` |
| 14_NORMALS_POSTMORTEM.md | `02_PIPELINE/02_NORMALS_POSTMORTEM.md` |
| 15_DOORS_REIMPORT_PLAN.md | `05_HISTORY/05_DOORS_JOURNAL.md` (журнал v5…v13) |
| 16_RESUME_STATE.md | `07_STATE/01_RESUME_STATE.md` |
| ITERATIONS.md | `05_HISTORY/01_ITERATIONS.md` |
| colliders.csv, doors_manifest.csv | `data/` |
| doors_registry_<чанк>.csv | `03_DOORS/registries/doors_registry_<чанк>.csv` |
| doors_registry_HOUSES_<чанк>.csv (дубликаты), doors_registry_RING_pilot.csv | удалены (устарели; история в git) |


