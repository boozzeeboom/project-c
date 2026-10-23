# Э2 — ДВЕРИ: ПЕРЕДАЧА СЛЕДУЮЩЕМУ АГЕНТУ

**Создан:** 2026-10-23 · **Статус:** рабочая передача (этап Э2 открыт)
**Родительский план:** `../ROADMAP.md` §4 (этап Э2) и §6 (открытые решения)
**Точка входа в проект:** `../README.md`

> Все числа ниже — замер по живой сцене и по `placement_manifest.csv` на 23.10.2026.
> Play Mode и скриншоты **не выполнялись** (NOT RUN): это работа пользователя
> (правила `dont-start-play` / `manual-playtest-only`).

---

## 0. Состояние на 23.10.2026

| Что | Число |
|---|---|
| Префабов дверей в `PRIMUM_DOORS` (21 чанк) | **2 161** — `PLANK_A` 930, `PANEL_B` 750, `ARCH_C` 371, `DOUBLE_A` 110 |
| Рендереров / `BoxCollider` на них | 11 726 / **4 432** (= 2 на дверь: блокирующее полотно + триггер рамы — это и есть быстрая проверка целостности) |
| Дверей в ADMIN (`PRIMUM/ADMIN_DOOR_PREFABS`) | **133** (поставлены разовым проходом, **своего меню нет**) |
| Отчёт последней расстановки | `placement_manifest.csv`: 3 118 строк = **place 2 161** + **skip 884** + **keep_original 73** |

Состояние в git: `7521019f` (этот отчёт по Э1) поверх `e472a129` (сама сцена).

---

## 1. Абсолютные пути

### 1.1 Сцена и объекты

| Что | Где |
|---|---|
| Сцена | `C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity` |
| Корень мира | `WorldRoot_0_0` |
| Город (инстанс префаба) | `WorldRoot_0_0/PRIMUM` → `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\PRIMUM_CITY.prefab` |
| Источник дверей (исходные группы, **выключены**) | `WorldRoot_0_0/PRIMUM/PRIMUM_HOUSES_<чанк>/DOORS` |
| **Куда ставить городские двери** | `WorldRoot_0_0/PRIMUM_DOORS/<чанк>` (сосед `PRIMUM`, **не** ребёнок префаба) |
| ADMIN-двери | `WorldRoot_0_0/PRIMUM/ADMIN_DOOR_PREFABS` |
| ADMIN главный вход | `WorldRoot_0_0/PRIMUM/ADMIN_ENTRANCE_PORTAL` (8 объектов) |
| ADMIN-эталон геометрии 1:1 | `WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER` (2 758 мешей) |
| Коллайдеры | `WorldRoot_0_0/PRIMUM_COLLIDERS/<чанк>/` → `HOUSE_WALL_BOX`, `HOUSE_FLOOR_BOX`, `HOUSE_ROOF_BOX`, `HOUSE_STAIR_BOX` |
| Свет | `WorldRoot_0_0/PRIMUM_LIGHTS` (+ `PrimumLightPool`) |

### 1.2 Ассеты

| Что | Где |
|---|---|
| Префабы дверей | `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\` — `PM_DOOR_SINGLE_PLANK_A.prefab`, `PM_DOOR_SINGLE_PANEL_B.prefab`, `PM_DOOR_SINGLE_ARCH_C.prefab`, `PM_DOOR_DOUBLE_A.prefab` |
| Меши дверей | `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\Meshes\` |
| Материалы | `C:\Projects\ProjectC_client\Assets\_Project\Materials\Primum\Doors\` — `PM_DOOR_WOOD_DARK.mat`, `PM_DOOR_WOOD_MID.mat`, `PM_DOOR_IRON.mat`, `PM_DOOR_BRONZE.mat`, `PM_DOOR_STONE.mat` |
| Рантайм-логика | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\DoorController.cs` |
| Инспектор `DoorController` | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\Editor\DoorControllerEditor.cs` |

### 1.3 Инструменты (Editor, меню `ProjectC → World → PRIMUM`)

| Файл | Пункт меню (приоритет) | Что делает |
|---|---|---|
| `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\PrimumDoorPlacement.cs` | `Двери: аудит проёмов (CSV)` (300) | анализ узлов `DOORS` → `placement_manifest.csv`, сцену не меняет |
| то же | `Двери: расставить одиночные (префабы)` (301) | массовая расстановка, идемпотентно пересобирает `PRIMUM_DOORS` |
| то же | `Двери: очистить расстановку` (302) | снести `PRIMUM_DOORS` |
| то же | `Двери: проверить посадку (verify)` (303) | проверка посадки и наложений |
| `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\DoorPrefabBuilder.cs` | `Двери: собрать двустворчатый префаб` (320), `Двери: пересобрать 3 одиночных (восстановление)` (321) | генерация префабов и мешей |
| `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\PrimumHouseColliders.cs` | `Коллайдеры домов: проверить проёмы и плиты` (363), `Коллайдеры домов: проходимость проёмов капсулой` (364) | проверка, что проёмы не завалены боксами |
| то же | `Коллайдеры ADMIN: проверить проёмы и плиты` (367), `Коллайдеры ADMIN: проходимость проёмов капсулой` (368) | то же для ADMIN |

> ⚠️ **Приоритеты меню конфликтуют:** `...\Editor\Primum\PrimumLampLighting.cs` тоже занимает 360–365.
> Ориентироваться по полному пути пункта меню, а не по цифре.

### 1.4 Данные и документы

| Что | Где |
|---|---|
| Реестры по чанкам (23 файла) | `C:\Projects\ProjectC_client\docs\world\primum_city\03_DOORS\registries\doors_registry_<чанк>.csv` (колонки `chunk,door,pos_x,pos_y,pos_z,rot_y,w,h,active,verts,tris`) |
| Отчёт расстановки | `C:\Projects\ProjectC_client\docs\world\primum_city\03_DOORS\placement_manifest.csv` |
| Исходный манифест модели | `C:\Projects\ProjectC_client\docs\world\primum_city\data\doors_manifest.csv` |
| Правила конвейера (v13.8) | `C:\Projects\ProjectC_client\docs\world\primum_city\02_PIPELINE\03_DOORS_PIPELINE_V13.md` |
| План префабов и типов | `...\03_DOORS\01_PREFAB_PLAN.md` |
| Одиночные префабы | `...\03_DOORS\02_SINGLE_DOOR_PREFABS.md` |
| Массовая расстановка | `...\03_DOORS\03_MASS_PLACEMENT.md` |
| Двустворчатый префаб | `...\03_DOORS\04_DOUBLE_DOOR_PREFAB.md` |
| ADMIN-двери: история и алгоритм | `C:\Projects\ProjectC_client\docs\world\primum_city\17_ADMIN_DOORS_WORK.md` |
| Дорожная карта (Э2, открытые решения) | `C:\Projects\ProjectC_client\docs\world\primum_city\ROADMAP.md` |
| Коллайдеры домов и ADMIN | `...\04_COLLIDERS\05_HOUSES_BOXES.md` (§9 — ADMIN), `...\04_COLLIDERS\06_ADMIN_COLLIDERS.md` |
| Свет (если задеты узлы `LAMPS`) | `...\08_LIGHTING\01_LIGHTING.md` |

---

## 2. Контракт префаба (менять только осознанно)

Габарит — **с рамой**, глубина по Z = 0.22 м:

| Id | Префаб | W | H |
|---|---|---|---|
| `PLANK_A` | `PM_DOOR_SINGLE_PLANK_A` | 0.96 | 2.10 |
| `PANEL_B` | `PM_DOOR_SINGLE_PANEL_B` | 1.06 | 2.15 |
| `ARCH_C` | `PM_DOOR_SINGLE_ARCH_C` | 1.20 | 2.36 |
| `DOUBLE_A` | `PM_DOOR_DOUBLE_A` | 1.36 (створка 0.56) | 2.50 |

Иерархия (на примере `PM_DOOR_SINGLE_PLANK_A`):

* корень `PM_DOOR_SINGLE_PLANK_A` — **триггер** `BoxCollider isTrigger=true` (0.90 × 2.10 × 0.20) + `DoorController`
  (`HingedSingle`, `hingePivot = Leaf`, `hingePanel = Leaf/LeafWood`, `hingeSide = Left`, `openAngle = 90`, `openDuration = 1.2`);
* ребёнок `Leaf` — **блокирующий** `BoxCollider isTrigger=false`, едет с петлёй;
* в `Leaf`: `LeafWood`, `LeafIron`, `LeafBronze`, (`LeafPanels` у панельных); отдельно `Frame`, `Sill`;
* `DOUBLE_A` — `LeafL` / `LeafR`, `DoorController` типа `HingedDouble` с `leftPivot` / `rightPivot`,
  `leftHingeSide = Left`, `rightHingeSide = Right`, `doubleOpenAngle = 90`.

**Пивот = низ по центру.** Слой `PRIMUM` (8), `static = false` (Floating Origin), `SetStaticEditorFlags(0)`.

Фильтры отбора (`PrimumDoorPlacement`, менять только с обоснованием):
`MinWidth 0.60`, `MaxWidth 1.38`, `MinHeight 1.80`, `MaxHeight 2.90`,
`DoubleMinW 1.15`, `DoubleMaxW 1.78`, `MaxThicknessRatio 0.45`,
`LeafMinHeightRatio 0.80`, `LeafMinWidthRatio 0.30`, `LeafSingleWidthRatio 0.55`, `LeafMinThicknessRatio 0.30`;
сопряжение пары створок: `PairMaxGap 0.12`, `PairMaxOverlap 0.30`, `PairMaxBottomDelta 0.10`, `PairMaxHeightDelta 0.30`.

Поворот — **0 или 90** по оси толщины AABB (не по `rot_y` реестра).
Масштаб считается от **общего** габарита с рамой; в ADMIN принято
`min((W_рамы − 0.26) / W_префаба, (H_рамы − 0.13) / H_префаба)`.

---

## 3. Что осталось (точные числа из `placement_manifest.csv`)

| Причина `skip` | Кол-во | Где именно |
|---|---|---|
| «двустворчатая единой группой — ждёт набор дворцов/администрации» | **288** | `S1_GLD_P1_25H` — **153** (доминанта `w = 1.50` — 137 строк), `ADMIN_S0_ADM_P1_01H` — **135** |
| ворота `GATE` | **40** | `S3_TRD-EN_P1` 10, `S8_GW_P1` 8, `S5_NE_P3` 6, `S4_TRD-EN_P2` 5, `S3_TRD-ES_P1` 4, `S4_TRD-ES_P2` 4, `S5_NE_P2` 3. Габариты: 2.89 × 2.78 — 34, 3.26 × 2.78 — 2, **3.53 × 3.57 — 4** |
| широкий проём > 1.78 | **22** | `S2_TRD_P1` 8, `S9_TRD-S2` 8, `S5_NE_P1` 5, `S9_TRD-N` 1 |
| узкий проём < 0.60 («пара») | **63** | `S2_TRD_P1` 28, `S4_TRD_P3` 26, `S3_TRD_P2` 9 |
| «не одностворчатая» (створок 2, доля 0.47) | 45 | разбросано — кандидаты в двустворчатые |
| «не одностворчатая» (створок 0, доля 0.00) | 8 | разбросано |
| не дверь (толщина/ширина) | 14 | игнорировать |
| мебель/наличник — `FURN` 241, `DOORCASE` 66 | 307 | **не трогать**: это не двери |
| `keep_original` (витрины/лавки оставлены исходными) | **73** | `S9_TRD-S2` 37, `S6_NQ` 18, `S9_TRD-N` 10, `S5_NE_P1` 8 — исходные группы **обязаны остаться включёнными** |
| объединена в двустворчатую пару | 97 | уже стоит как `DOUBLE_A` |

**Приоритет работ**

1. Дворцовый набор `S1_GLD` (153 двери ~1.5 м) — нужен новый вариант префаба «дворцовая двустворчатая».
2. ADMIN — см. предупреждение ниже.
3. Ворота `GATE` — нужны префабы ворот 2.89 × 2.78 и 3.53 × 3.57.
4. Узкие 0.56 и широкие > 1.78 — отдельные типы, не «сжимать» в существующие.

> ⚠️ **По ADMIN числа расходятся.** Реестр `doors_registry_ADMIN_S0_ADM_P1_01H.csv` = 135 строк,
> из них `_FRAME_` = 123; в сцене стоит 133 (123 из реестра + 10 крыльевых `ADM_CONN_*`/`ADM_WING_*`).
> При этом манифест показывает 135 `skip` по ADMIN. Перед работой по ADMIN **пересчитать реестр и сцену заново** —
> манифесту в этой части доверять нельзя. Алгоритм ADMIN (без меню, разовым проходом) описан в
> `C:\Projects\ProjectC_client\docs\world\primum_city\17_ADMIN_DOORS_WORK.md`.

---

## 4. Порядок работы

1. Правки — только в `PrimumDoorPlacement.cs` (и `DoorPrefabBuilder.cs`, если нужен новый тип).
   Меню 300 — аудит, 301 — расстановка, 303 — verify.
2. Новый тип двери = новый `Variant` (`Id`, `Path`, `W`, `H`, `D`) + правило классификации.
   Существующие 4 префаба **не пересобирать** — на них уже стоят 2 161 инстанс.
3. Ставить в `WorldRoot_0_0/PRIMUM_DOORS/<чанк>` (сосед `PRIMUM`). **Никогда** внутрь
   `PRIMUM_CITY.prefab` — оттуда всё теряется при обновлении префаба.
4. После прогона 301/303 обновить `placement_manifest.csv` (пишется автоматически), дописать
   этот файл и `../ROADMAP.md`, затем коммит только относящихся файлов (без `push`).

---

## 5. Что проверять и чем

| Проверка | Инструмент | Норма |
|---|---|---|
| Посадка дверей | меню `Двери: проверить посадку (verify)` (303) | 0 ошибок; ровно 2 `BoxCollider` на дверь |
| Дверь не завалена коллайдерами домов | `Коллайдеры домов: проверить проёмы и плиты` (363) | перекрытых проёмов **0** |
| Проход через проём | `Коллайдеры домов: проходимость проёмов капсулой` (364) | не регрессировать известный долг `FLOOR` (2 158 из 3 459 проходов) |
| ADMIN | меню 367 / 368 | перекрыто 0, проходов **86/86** — текущий эталон |
| Легаси-заглушки | осмотр сцены | группы `DOORS/` выключены, кроме 73 витрин (`keep_original`) |
| Ходьба, `E`, F8/F9 | **Play Mode — только пользователь** (NOT RUN) | двери открываются, физика едет с городом, 0 errors |

---

## 6. Ловушки, на которых уже обжигались

* **Подмена/реимпорт FBX стирает** ручные правки внутри модели (коллайдеры, скрипты, выключение) —
  только скриптовый пост-проход или отдельные объекты вне модели.
* **`static` нельзя:** мир живёт под Floating Origin (F8/F9), статик-запекание отрывает двери от города.
* **Пивот — не центр AABB**, а низ по центру; ошибка пивота даёт «двери висят в воздухе».
* **`rot_y` из реестра не различает ориентацию меша** — ориентацию брать по тонкой стороне рамы.
* **ADMIN: размеры и ориентация — только из `PRIMUM/ADMIN_FOR_COLLIDER`**, не из bbox рам чанка.
* **Приоритеты меню дублируются** между `PrimumLampLighting` и `PrimumHouseColliders`.
* **73 витрины** обязаны оставаться исходными включёнными — иначе дыры в фасадах.
* Правило `PORTAL` (полная высота объекта вместо `DOOR_MAX`) уже покрывает парадный проём ADMIN 12.63 × 4.17 —
  расширять `DOOR_MAX` не нужно.
