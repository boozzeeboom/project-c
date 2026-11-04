# 07 — ФЕРМА 0-3 RICE: импорт, материалы, двери, лампы, слияние — факты и числа

**Собрано:** 2026-11-04. Unity 6000.5.2f1, URP 17.5.0 Forward+, `C:\Projects\ProjectC_client`.
**Режим:** edit-mode, через `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`. Play Mode **не запускался**, сцены **не сохранялись**, в `Assets/_Project/**` (кроме папки префаба фермы) изменений нет.
**Метки:** **[F]** факт (файл/YAML/замер через API) · **[M]** выведено из кода · **[E]** оценка.

**Итог:** ферма `FARM_0_3_RICE` собрана из 962 мешей источника в 1 префаб: **90 merged-мешей + 33 двери + 24 лампы**, все двери и лампы — **настоящие вложенные prefab-instance**. Ассет верифицирован, но в `WorldScene_0_0` **не размещён** и runtime-приёмку не проходил.

---

## 0. Сводка «до / после»

| Метрика | До (backup) | После | Источник |
|---|---|---|---|
| Размер `.prefab` | 3 602 647 Б (3.44 МБ) | ≈1.10 МБ | замер файла |
| `MeshRenderer` | **962** | **167** (90 merged + 77 в дверях) | YAML / API |
| Меши: ссылок / уникальных | 962 / **962** (саб-ассеты FBX) | 167 / **106** (90 merged + 16 префабных) | YAML / API |
| Вершины | 271 346 (FBX, 962 меша) | **268 706** | API |
| Треугольники | 145 578 (FBX) | **138 018** | API |
| Материалы | 28 встроенных FBX (`MAT_*`) | **20** процедурных (`M_PC_*` + `PM_LAMP_GLOW_WARM`) | API |
| Шейдеры | FBX/`URP/Lit` | **14**, все процедурные Shader Graph + `URP/Unlit` (lamp glow) | API |
| Слоты материалов / null-слоты | 907 переразмечено | **276 / 0** | API |
| BoxCollider | 387 (пользовательские) | **387** (+82 дверей = **469** всего `Collider`) | YAML / API |
| Вложенные `PrefabInstance` (`!u!1001`) | **0** | **57** (33 двери + 24 лампы) | YAML |
| `DoorController` | 0 | **33** | API |
| `Light` | 0 | **24** (все `enabled=false`) | API |
| Слои GameObject | `[0]` | **`[0, 8, 10]`** | API |
| Static-рендереры | 0 | **0** (static только у слоя коллайдеров) | API |

Разница по треугольникам (145 578 → 138 018, −5.2 %) объясняется удалением исходной дверной геометрии (−162 рендерера / −12 312 верт) и 76 debug-маркеров; геометрия ламповых корпусов сохранена как emissive в merged-батчах.

---

## 1. Источник: `.blend` → FBX → Unity [F]

| Параметр | Значение |
|---|---|
| `.blend` | `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primumfarm_0_3\ProjectC_farm_primumfarm_0-3_1.blend` (Blender 5.2.3 LTS) |
| Корневая коллекция | `FARM_0_3_RICE` → 19 детей: `00_REFERENCE` (исключена из экспорта) + 18 рабочих блоков |
| Единицы | `METRIC`, `scale_length = 1.0`; габарит `226.6 × 130.802 × 49.19` м |
| Объекты | 971 (962 — меши вне `00_REFERENCE`) |
| FBX | `Assets/_Project/Models/Primumfarm/0-3/Project-C_primum_farm_0-3_GameReady.fbx` — 3.4 МБ, 962 меша, 271 346 верт / 145 578 тр, 28 материалов |

Флаги экспорта: `apply_scale_options='FBX_SCALE_ALL'`, `global_scale=1`, `apply_unit_scale=True`, `axis_forward='-Z'`, `axis_up='Y'`, `use_selection=True` по 962 мешам.
**Причина:** первый экспорт с `FBX_SCALE_NONE` дал масштаб 100 на всех объектах в Unity.

Флаги импорта (`ModelImporter`): `globalScale = 1.06`, `useFileScale = true`, cameras/lights/blendShapes/animation — off.
**Канон масштаба `k = 1.06`** (значение 1.07 в старых README — опечатка). После импорта `fileScale = 1`, масштаб всех объектов 1, габарит `240.20 × 52.14 × 138.65` м = ровно 1.06 от исходника.

Особенность конвертации: каждый объект стоит в `(0,0,0)` с поворотом `X≈270.02°`, геометрия запечена в мировых координатах Blender, пивоты — в мировом нуле. Поэтому коллайдеры и слияние считались по матрице объекта, а не по локальным `mesh.bounds`.

FBX не хранит коллекции, поэтому иерархия 304 групп восстановлена из `_local_backups/farm_0_3_export_2026-11-04/farm_0_3_collections.tsv` (962 строки `objName<TAB>collectionPath`, 281 группа) с `worldPositionStays = true`.

---

## 2. Материалы [F]

- Переразмещено **907 слотов** на процедурную палитру `M_PC_*`; слотов `URP/Lit` в префабе не осталось.
- **19** рендереров `*_WINDOWS` (в источнике `MAT_METAL_DARK`) принудительно переведены на `M_PC_Glass`.
- **76** debug-маркеров удалено: все слоты = `MAT_ACCESS_ROUTE` / `MAT_CONTOUR_DEBUG` / `MAT_ZONE`, плиты 0.05–0.21 м толщиной, суммарно 21 389 м² по XZ, материал буквально назван DEBUG.
- **11** оставшихся смешанных слотов `MAT_ZONE` → `M_PC_Steel_v2_Dark` (разметка палуб, пожарные маркеры, тела указателей).

Соответствие (сокращённо): `MAT_WOOD→M_PC_Wood`, `MAT_METAL_DARK→M_PC_Steel_v2_Dark`, `MAT_HARDWARE→M_PC_Bronze_Dark`, `MAT_SUPPORT→M_PC_Steel_v2`, `MAT_{PLATFORM_SHEET,DECK_PLATE,PLATE}→M_PC_Plate`, `MAT_TILE→M_PC_Tiles`, `MAT_LAMP_GLOW→PM_LAMP_GLOW_WARM`, `MAT_WALL_PLASTER→M_PC_Concrete_Limewash`, `MAT_PLASTER→M_PC_Concrete`, `MAT_STONE→M_PC_Cobble_Granite`, `MAT_{PIPE,STAIR_REBAR,METAL_GRATE}→M_PC_IronRust`, `MAT_RICE→M_PC_Foliage`, `MAT_{HAY,BUND}→M_PC_Fabric_Canvas`, `MAT_{RUBBER,CABLE}→M_PC_Plastic_Rubber`, `MAT_WATER→M_PC_Glass_Blue`, `MAT_JAR→M_PC_Glass_Amber`, `MAT_BRICK→M_PC_Brick`, `MAT_{BARREL,CRATE}→M_PC_Wood_Dark`.

Итог: **20 материалов, 276 слотов, 0 null, 14 шейдеров** (`Brick`, `Bronze`, `Cobble`, `Concrete`, `Fabric`, `Foliage`, `Glass`, `IronRust`, `Plastic`, `Plate`, `Tiles`, `Wood`, `Shader Graphs/Steel_v2`, `Universal Render Pipeline/Unlit`).

---

## 3. Двери: 33 инстанса, 8 префабов [F]

Проёмы фермы существенно крупнее городских: 11 одностворчатых и 22 двустворчатых, размеры от `0.95 × 2.18` до `6.36 × 5.26` м. Выбор префаба: сначала по числу створок, затем минимизация `(ширина + высота)` после равномерного подгона `min(openW/prefabW, openH/prefabH)` с ограничением `0.5…1.8`.

| Префаб | Инстансов | Примечание |
|---|---|---|
| `PM_DOOR_PALACE_DOUBLE_C` | 15 | основной двустворчатый, масштаб 0.76 и 1.52 |
| `PM_DOOR_SINGLE_WIDE_A` | 6 | масштаб 0.64 / 0.998 / 1.04 |
| `PM_DOOR_GATE_A` | 4 | ворота склада и мельницы, масштаб **1.51 / 1.80** |
| `PM_DOOR_SINGLE_PANEL_B` | 3 | масштаб 1.00 |
| `PM_DOOR_SINGLE_ARCH_C` | 2 | 0.88 / 0.97 |
| `PM_DOOR_PALACE_DOUBLE_A` | 1 | `DOOR_SAN`, 0.99 |
| `PM_DOOR_GATE_B` | 1 | `DOOR_SEED_GATE`, 0.88 |
| `PM_DOOR_SINGLE_PLANK_A` | 1 | `DOOR_NURSERY_PUMP`, 0.99 |

- Позиция инстанса: `(bounds.center.x, bounds.min.y, bounds.center.z)`; поворот `identity`, если проём вытянут по X, иначе `Euler(0, 90, 0)`. Неравномерного масштаба нет (0 инстансов).
- Исходная дверная геометрия снята: **162 рендерера / 12 312 верт**.
- Пивоты проверены и совпадают с исходными префабами (`leftPivot = LeafL`, `rightPivot = LeafR`, `hingePivot = Leaf` у одностворчатых ворота; null-ссылки — по устройству префаба, не дефект).
- Слой всех 33 инстансов — **10 `PRIMUM_DOORS`**, `DoorController` жив (33).
- **Ограничение:** два ворот склада и два ворот мельницы посажены с масштабом 1.51–1.80 (проём 6.36 м против максимального 3.53 м у префаба). Относительный зазор до 0.31. Это цена переиспользования 11 фиксированных префабов на проёмах 0.95–6.36 м.

---

## 4. Лампы: 24 инстанса, 2 префаба [F]

- `PRIMUM_FARMLIGHT_LAMP_01…24` в группе `PRIMUM_FARMLIGHT`, позиция = центр `bounds` исходного `LAMP_xx_GLOW`.
- `PM_LIGHT_STREET_WALL` (range 9) — 11 штук при `glow.y > 2.8`; `PM_LIGHT_FACADE_WALL` (range 6) — 13 штук.
- Проверено у всех 24: `Light` присутствует (`Point`), `shadows = None`, `enabled = false`, `range ≤ 20`, слой **8**, масштаб 1.
- Корпуса и `*_GLOW`-геометрия сохранены в merged-батчах как emissive (`PM_LAMP_GLOW_WARM`), поэтому `Light` не «висит в воздухе».

---

## 5. Слияние [F]

Ключ батча: `(верхний блок, материал, rendererSettings)`, где `rendererSettings = layer | shadowCastingMode | receiveShadows | lightProbeUsage | reflectionProbeUsage | motionVectorGenerationMode | renderingLayerMask`.

- Сабмеши развёрнуты через `CombineInstance.subMeshIndex`; `indexFormat = UInt32`; `CombineMeshes(mergeSubMeshes: true, useMatrices: true)`; `RecalculateBounds()`.
- Лимит **60 000 верт/меш** соблюдён: фактический максимум **35 356** (`PM_FRM_31_PADDY_M_PC_Wood_00.asset`), мешей сверх лимита — **0**. Распределение: 83 меша < 10k верт, 6 мешей 10–20k, 1 меш 30–40k.
- Исключено из слияния: всё под `FARM_0_3_RICE_DOORS` (77 рендереров), любой объект с компонентами кроме Transform/MeshFilter/MeshRenderer, все коллайдеры и лампы.
- Результат: **90 батчей**, 253 322 верт / 135 198 тр; снято **724** исходных рендерера, уничтожено 724 пустых GameObject и дополнительно 188 пустых обёрток-групп.
- Топология префаба: 20 корневых детей = 18 блоков + `FARM_0_3_RICE_COLLIDER` (387 детей) + `FARM_0_3_RICE_DOORS` (33) + `PRIMUM_FARMLIGHT` (24).
  Рендереров по блокам: `90_CONTENT` 10, `30_NURSERY` 9, `34_DRYING_WAREHOUSE` 9, `31_PADDY` 8, `20_ROUTES` 8, `33_MILL` 7, `35_RESIDENTIAL` 7, `41_PAX_TERMINAL` 7, `40_DOCKS` 6, `32_THRESHING` 6, `61_TRANSITIONS` 3, `50_UNDERDECK` 3, `80_UTILITY` 2, `60_SUPPORTS` 2, `10_PLATFORMS` 1, `42_DECK_COVERS` 1, `70_RAILINGS` 1, `_DOORS` 77.
- Объём merged-мешей: **90 файлов / 22.72 МБ** (23 827 654 Б), от 5 736 Б до 3.30 МБ.
- DC-модель (COMMON §): `DC ≈ 2×P + C + O`, одна уникальная пара `(mesh, material, settings)` = 2 DC. 90 batчей ≈ 180–270 DC против 962 рендереров «каждый со своим мешем».

---

## 6. Верификация вложенных prefab-instance [F]

Ранее по этому префабу был сделан **ложный вывод**, что двери и лампы расплющены в копии. Прямая проверка сохранённого YAML и API это опровергает:

```
YAML PrefabInstance blocks          = 57
m_PrefabInstance refs: nonZero=57   zero=1777
  8a78c439… x15 → PM_DOOR_PALACE_DOUBLE_C.prefab
  a5f88bac… x6  → PM_DOOR_SINGLE_WIDE_A.prefab
  1ba477fc… x4  → PM_DOOR_GATE_A.prefab
  3c92f049… x3  → PM_DOOR_SINGLE_PANEL_B.prefab
  92e8d6d2… x2  → PM_DOOR_SINGLE_ARCH_C.prefab
  bb3162a0… x1  → PM_DOOR_PALACE_DOUBLE_A.prefab
  5f4f65cd… x1  → PM_DOOR_GATE_B.prefab
  b6678ab5… x1  → PM_DOOR_SINGLE_PLANK_A.prefab     = 33
  841a63d6… x13 → PM_LIGHT_FACADE_WALL.prefab
  a2dc55f8… x11 → PM_LIGHT_STREET_WALL.prefab       = 24
```

`PrefabUtility.GetPrefabInstanceStatus` по всем узлам префаба: **183 `Connected`**, `NotAPrefab` — 605 (merged-меши, коллайдеры, пустые группы). То есть связь с исходными префабами сохранена: правка `PM_DOOR_*` / `PM_LIGHT_*` распространится на ферму, «переиспользование готовых префабов» выполнено фактически, а не номинально. Переделка через Prefab Stage не требуется.

---

## 7. Коллайдеры и static [F]

- **387** пользовательских `BoxCollider` под `FARM_0_3_RICE_COLLIDER` сохранены без изменений; всего в префабе **469** `Collider` (387 + 82 дверных).
- Всего **476** GameObject с `isStatic` из **788** Transform: 378 — сами боксы коллайдеров, 98 — пустые узлы-держатели. **Static-рендереров — 0.**
- Флаги `isStatic` на слое коллайдеров **намеренно не тронуты**: слой принадлежит пользователю, а правило «не ставить static на мировой контент» относится к визуалу под Floating Origin.

---

## 8. Что НЕ сделано / открытые пункты

- **Префаб не размещён в `WorldScene_0_0`** под `WorldRoot_0_0/Primum_farms/<имя>` (SETTLEMENTS §7.7 шаг 1, §3 шаги 9–10). Решение о размещении и сохранении сцены — за пользователем.
- **Не выполнены** гейты после размещения: `Project C → Culling → Door Layer → Apply`, Probe, `Stage 8 Doors → Run Bench` (`P1 2 229 ±5 %`, `L1 46`, `L2 2`, дверей >150 м = 0, F8/F9). В текущем прогоне двери фермы не участвовали.
- **Нет визуальной Play Mode-приёмки** дверей (посадка в проём, открытие/закрытие) и ламп (посадка света, ночной режим).
- Ворота склада и мельницы сидят на масштабе 1.51–1.80 (см. §3) — визуально требует отдельного подтверждения.
- **99 orphan merged-ассетов / 117.36 МБ** (`PM_FRM_X12_*`, `PM_FRM_SP1_*`, `MF_*`) в `Farms_Merged` — не от этого прохода и новым префабом не используются; не удалялись.
- **HLOD для ферм не применим:** бейкер знает только `PRIMUM`, своей строки для ферм нет → контент целиком остаётся в L0.
- Имена в Blender требуют гигиены: автосуффиксы коллекций дверей `BOLTS.001…034` / `HANDLE.001…034`, дубли объектов `BEACON_A.001/.002/.003`, `GREENHOUSE_TRUSS.001`, `DOOR_MILL_GATE_W_BOLTS_A.001`, пустые коллекции `PACK_HOUSE/PACK_DOORS`, `HEAT_UNIT/HEAT_DOORS`.

---

## 9. Артефакты, бэкапы, git

| Что | Где |
|---|---|
| Префаб | `Assets/_Project/Prefabs/City/Farms/FARM_0_3_RICE.prefab` (1.10 МБ) |
| Merged-меши | `Assets/_Project/Prefabs/City/Farms_Merged/PM_FRM_<GROUP>_<MAT>_NN.asset` — 90 шт., 22.72 МБ |
| Бэкап «до» | `_local_backups/farm_0_3_opt_2026-11-04/FARM_0_3_RICE.prefab` (+ `.meta`) |
| Карта коллекций | `_local_backups/farm_0_3_export_2026-11-04/farm_0_3_collections.tsv` |
| FBX | `Assets/_Project/Models/Primumfarm/0-3/Project-C_primum_farm_0-3_GameReady.fbx` |

**Git-политика проекта:** `.gitignore:153` — `*.prefab`, `.gitignore:182` — `*.meta`. Префабы и `.meta` **вне git по устройству репозитория** (так же лежат все `PM_DOOR_*`, `PM_LIGHT_*` и `PRIMUM_CITY.prefab`). Поэтому в коммит попадают только **90 merged `.asset`** (как ранее `PM_FRM_SP1_*` и `PM_FRM_X12_*`) и этот отчёт; сам префаб воспроизводится инструментом, а не из git.
