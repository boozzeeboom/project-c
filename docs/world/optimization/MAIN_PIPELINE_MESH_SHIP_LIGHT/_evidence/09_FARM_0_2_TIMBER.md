# 09 — ФЕРМА 0-2 TIMBER: материалы, двери, лампы, слияние, слои — факты и числа

**Собрано:** 2026-11-05. Unity 6000.5.2f1, URP 17.5.0 Forward+, `C:\Projects\ProjectC_client`.
**Режим:** Blender 5.2.3 LTS (правка ориентации граней и экспорт) + Unity edit-mode через
`PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`.
Play Mode **не запускался**, сцены **не сохранялись**.
**Метки:** **[F]** факт (файл/API/замер) · **[M]** выведено из кода · **[E]** оценка.

**Итог:** ферма `FARM_0_2_TIMBER` собрана из 4 940 мешей источника в 1 префаб:
**79 merged-меш + 30 дверей + 43 лампы**; двери и лампы — настоящие вложенные prefab-instance,
визуал и лампы на слое 8 (`PRIMUM`), двери — на 10 (`PRIMUM_DOORS`).
Ассет верифицирован edit-mode проверками; в `WorldScene_0_0` **не размещён**, runtime-приёмку не проходил.

---

## 0. Сводка «до / после» [F]

| Метрика | До (бэкап) | После | Источник |
|---|---|---|---|
| Размер `.prefab` | 14 700 541 Б | **1 714 380 Б** (−88.3 %) | файл |
| `Transform` | 6 154 | **1 394** | API |
| `MeshRenderer` | **4 939** | **179** (79 merged + 23 подвижных + 77 дверных) | API |
| Вершины | 320 116 | **330 196** | API |
| Треугольники | 172 112 (FBX-меши) | **176 588** | API |
| Материалы | 13 `MF_*` (FBX) | **15** процедурных `M_PC_*` | API |
| Слоты / null-слоты | 4 939 / 0 | **285 / 0** | API |
| `Collider` | 288 (пользовательские) | **365** (288 + 77 дверных) | API |
| Вложенные prefab-instance | 0 | **73** (30 дверных + 43 ламповых) | API |
| `DoorController` | 0 | **30** | API |
| `Light` | 0 | **43** (все `enabled=false`) | API |
| Слои GameObject | `[0]` | **`[0, 8, 10]`** | API |
| Static-рендереры | 0 | **0** (static только у слоя коллайдеров) | API |
| Габарит | 262.99 × 53.05 × 142.09 | **262.99 × 53.05 × 142.09** (не изменился) | API |

Баланс вершин сходится точно [F]:

```
пул слияния   4 713 рендереров / 313 972 верт  →  79 батчей / 313 972 верт   (разница 0)
контур        15 мешей / 1 080 верт            →  удалён
исходные двери 188 рендереров / 4 512 верт     →  сняты, вместо них 30 prefab-instance / 15 672 верт
подвижные      23 рендерера / 552 верт         →  сохранены без слияния
итого в префабе: 313 972 + 15 672 + 552 = 330 196 верт
```

---

## 1. Источник и подготовка (Blender → Unity) [F]

| Параметр | Значение |
|---|---|
| `.blend` | `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primumfarm_0_2\ProjectC_farm_primumfarm_0-2_1.blend` (2 470 679 Б, сохранён после правки) |
| Корневая коллекция | `FARM_0_2_TIMBER`, 713 коллекций, 12 верхних блоков |
| Гигиена имён | 0 суффиксов `.NNN` (у коллекций и объектов), 0 дублей имён, 0 пустых коллекций, 0 объектов в нескольких коллекциях |
| Единицы | `METRIC`, `scale_length = 1.0`, не-единичного масштаба — 0 |
| FBX | `Assets/_Project/Models/Primumfarm/0-2/Project-C_primum_farm_0-2_GameReady.fbx` (10 755 548 Б, 4 940 мешей + 43 Empty-якоря) |
| Импорт | `globalScale = 1.06`, `useFileScale = true`, `fileScale = 1`, без камер/света/анимаций, `meshCompression = Off` |
| Результат импорта | 4 940 мешей / 320 884 верт / 172 112 тр / 13 `MF_*`; габарит 262.99 × 53.05 × 142.09 (= 248.10 × 134.05 × 50.05 × 1.06) |
| Карта коллекций | `_local_backups/farm_0_2_export_2026-11-04/farm_0_2_collections.tsv` (4 983 строки) |
| Иерархия префаба | 710 групп восстановлены из TSV, 4 983 объекта привязаны, несовпадений 0 |

**Ориентация граней.** Все 4 940 мешей замкнутые (открытых оболочек и рёбер с >2 гранями нет),
поэтому признак однозначный — знак signed volume. Вывернутых было **1 557**, развёрнуты
`bmesh.ops.recalc_face_normals`; дополнительно проверены меши со **смешанной** ориентацией
(сравнение `|V|` до/после recalc по всем 4 940) — таких **0**. Повторная проверка: вывернутых **0**. [F]

**Якоря ламп.** В исходнике есть 43 authored-якоря `LAMP_*` (Empty) — готовые позиции светильников.
В первый экспорт (только `object_types={'MESH'}`) они не попали, поэтому экспорт пересобран с
`object_types={'MESH','EMPTY'}`; якоря сохранены в префабе как точки для светового этапа. [F]

---

## 2. Материалы [F]

- `MF_*`-слотов в префабе **0**, null-слотов **0**, всего **285** слотов на **15** материалов
  (79 слотов у merged-батчей, 183 у дверных префабов, 23 у подвижных).
- Новых материалов не создавалось, только существующая процедурная палитра:

| Исходный `MF_*` | Слотов до | Назначено | Шейдер |
|---|---:|---|---|
| `MF_BRASS` | 300 | `M_PC_Bronze_Brass` | Bronze |
| `MF_CANVAS` | 313 | `M_PC_Fabric_Canvas` | Fabric |
| `MF_EMISSIVE` | 43 | `M_PC_Lantern` | Lantern |
| `MF_GLASS` | 62 | `M_PC_Glass` | Glass |
| `MF_METAL_DECK` | 815 | `M_PC_Plate` | Plate |
| `MF_METAL_RUST` | 570 | `M_PC_IronRust` | IronRust |
| `MF_METAL_STEEL` | 871 | `M_PC_Steel_v2` | Shader Graphs/Steel_v2 |
| `MF_SOIL` | 157 | `M_PC_Terrain` | Terrain |
| `MF_TILE_ROOF` | 238 | `M_PC_Tiles` | Tiles |
| `MF_WOOD_BEAM` | 686 | `M_PC_Wood` | Wood |
| `MF_WOOD_DARK` | 241 | `M_PC_Wood_Dark` | Wood |
| `MF_WOOD_PLANK` | 566 | `M_PC_Wood_Ochre` | Wood |
| `MF_WOOD_TRUSS` | 77 | `M_PC_Wood_Red` | Wood |

- Распределение после прохода (285 слотов): `M_PC_IronRust` 65, `M_PC_Bronze` 47, `M_PC_Wood_Dark` 46,
  `M_PC_Concrete` 36, `M_PC_Wood` 20, `M_PC_Steel_v2` 15, `M_PC_Wood_Ochre` 12, `M_PC_Bronze_Brass` 7,
  `M_PC_Plate` 7, `M_PC_Fabric_Canvas` 6, `M_PC_Tiles` 6, `M_PC_Wood_Red` 6, `M_PC_Lantern` 5,
  `M_PC_Terrain` 4, `M_PC_Glass` 3.
  Разбивка по категориям [F]: merged — 79 слотов, 13 материалов (без `Bronze`/`Concrete`);
  двери — 183 слота, 5 материалов (`M_PC_Bronze` 47, `M_PC_IronRust` 47, `M_PC_Wood_Dark` 40,
  `M_PC_Concrete` 36, `M_PC_Wood` 13) — `M_PC_Bronze` и `M_PC_Concrete` приходят только с дверных префабов;
  подвижные — 23 слота, 3 материала (`M_PC_IronRust` 10, `M_PC_Steel_v2` 7, `M_PC_Wood_Ochre` 6).
- **Удалён `01_MASTER_CONTOUR`** — 15 мешей / 1 080 верт: разметочная/отладочная геометрия,
  снята по тому же правилу, что на 0-3 (6 плит) и 0-4 (13 плит). [F]
- Попиксельное соответствие `MF_* → M_PC_*` по объектам из слитого артефакта не восстанавливается
  (исходные рендереры уничтожены слиянием); таблица соответствия выше — это фактическое исполнение прохода. [M]

---

## 3. Двери [F]

Правило пайплайна: сначала по числу створок, затем минимум `|openW − w·s| + |openH − h·s|`
при равномерном `s = clamp(min(openW/w, openH/h), 0.5 … 1.8)`; позиция `(bounds.center.x, bounds.min.y, bounds.center.z)`;
поворот `identity`, если проём вытянут по X, иначе `Euler(0, 90, 0)`; неравномерный масштаб запрещён.
Снимаются только створки и фурнитура (`LEAF*`, `HANDLE`, `HINGE_*`, `ROLLER_*`).

**Заменено 30 сборок** (13 одностворчатых + 17 двустворчатых), снято 188 рендереров / 4 512 верт. [F]

### 3.1 Одностворчатые — 13

| Сборка | Префаб | s | Поставлено | Проём | Заливка W |
|---|---|---:|---|---|---:|
| `SAWMILL_DOOR_W` | `PM_DOOR_SINGLE_WIDE_A` | 1.060 | 2.12 × 2.33 | 2.12 × 2.33 | 1.000 |
| `WAREHOUSE_PORTAL_DOOR_W` | `PM_DOOR_SINGLE_WIDE_A` | 1.060 | 2.12 × 2.33 | 2.12 × 2.33 | 1.000 |
| `NURSERY_GREENHOUSE_01_DOOR_E` | `PM_DOOR_SINGLE_WIDE_A` | 1.060 | 2.12 × 2.33 | 2.12 × 2.33 | 1.000 |
| `NURSERY_GREENHOUSE_01_DOOR_W` | `PM_DOOR_SINGLE_WIDE_A` | 1.060 | 2.12 × 2.33 | 2.12 × 2.33 | 1.000 |
| `NURSERY_GREENHOUSE_02_DOOR_E` | `PM_DOOR_SINGLE_WIDE_A` | 1.060 | 2.12 × 2.33 | 2.12 × 2.33 | 1.000 |
| `NURSERY_GREENHOUSE_02_DOOR_W` | `PM_DOOR_SINGLE_WIDE_A` | 1.060 | 2.12 × 2.33 | 2.12 × 2.33 | 1.000 |
| `DRYING_BOILER_DOOR_S` | `PM_DOOR_SINGLE_ARCH_C` | 0.988 | 1.19 × 2.33 | 1.27 × 2.33 | 0.932 |
| `NURSERY_SEED_SHED_DOOR_S` | `PM_DOOR_SINGLE_ARCH_C` | 0.988 | 1.19 × 2.33 | 1.27 × 2.33 | 0.932 |
| `RES_COMMON_DOOR_S` | `PM_DOOR_SINGLE_ARCH_C` | 0.988 | 1.19 × 2.33 | 1.48 × 2.33 | 0.799 |
| `RES_WC_DOOR_N` | `PM_DOOR_SINGLE_ARCH_C` | 0.943 | 1.13 × 2.23 | 1.27 × 2.23 | 0.890 |
| `UNDERDECK_PUMP_DOOR_S` | `PM_DOOR_SINGLE_ARCH_C` | 0.943 | 1.13 × 2.23 | 1.27 × 2.23 | 0.890 |
| `NURSERY_PUMPS_DOOR_S` | `PM_DOOR_SINGLE_ARCH_C` | 0.898 | 1.08 × 2.12 | 1.17 × 2.12 | 0.924 |
| `PAX_BOOTH_DOOR_S` | `PM_DOOR_SINGLE_ARCH_C` | 0.898 | 1.08 × 2.12 | 1.17 × 2.12 | 0.924 |

### 3.2 Двустворчатые — 17

| Сборка | Префаб | s | Поставлено | Проём | Заливка W |
|---|---|---:|---|---|---:|
| `RES_HOUSE_A1_DOOR_FRONT` | `PM_DOOR_PALACE_DOUBLE_C` | 0.741 | 2.07 × 2.12 | 3.39 × 2.12 | 0.610 |
| `RES_HOUSE_A2_DOOR_FRONT` | `PM_DOOR_PALACE_DOUBLE_C` | 0.741 | 2.07 × 2.12 | 3.39 × 2.12 | 0.610 |
| `RES_HOUSE_A3_DOOR_FRONT` | `PM_DOOR_PALACE_DOUBLE_C` | 0.741 | 2.07 × 2.12 | 3.39 × 2.12 | 0.610 |
| `RES_HOUSE_A4_DOOR_FRONT` | `PM_DOOR_PALACE_DOUBLE_C` | 0.741 | 2.07 × 2.12 | 3.39 × 2.12 | 0.610 |
| `RES_HOUSE_B1_DOOR_FRONT` | `PM_DOOR_PALACE_DOUBLE_C` | 0.741 | 2.07 × 2.12 | 3.39 × 2.12 | 0.610 |
| `RES_HOUSE_B2_DOOR_FRONT` | `PM_DOOR_PALACE_DOUBLE_C` | 0.741 | 2.07 × 2.12 | 3.39 × 2.12 | 0.610 |
| `RES_HOUSE_B3_DOOR_FRONT` | `PM_DOOR_PALACE_DOUBLE_C` | 0.741 | 2.07 × 2.12 | 3.39 × 2.12 | 0.610 |
| `NURSERY_SEED_SHED_GATE_E` | `PM_DOOR_PALACE_DOUBLE_C` | 1.112 | 3.10 × 3.18 | 6.36 × 3.18 | 0.488 |
| `DRYING_CHAMBER_1_GATE_S` | `PM_DOOR_PALACE_DOUBLE_C` | 1.668 | 4.65 × 4.77 | 8.48 × 4.77 | 0.549 |
| `DRYING_CHAMBER_2_GATE_S` | `PM_DOOR_PALACE_DOUBLE_C` | 1.668 | 4.65 × 4.77 | 8.48 × 4.77 | 0.549 |
| `DRYING_CHAMBER_3_GATE_S` | `PM_DOOR_PALACE_DOUBLE_C` | 1.668 | 4.65 × 4.77 | 8.48 × 4.77 | 0.549 |
| `WAREHOUSE_PORTAL_GATE_S1` | `PM_DOOR_PALACE_DOUBLE_C` | 1.186 | 3.31 × 3.39 | 7.21 × 3.39 | 0.459 |
| `WAREHOUSE_PORTAL_GATE_S2` | `PM_DOOR_PALACE_DOUBLE_C` | 1.186 | 3.31 × 3.39 | 7.21 × 3.39 | 0.459 |
| `SAWMILL_GATE_E` | `PM_DOOR_PALACE_DOUBLE_C` | 1.800 | 5.02 × 5.15 | 12.72 × 5.30 | 0.395 |
| `SAWMILL_GATE_W` | `PM_DOOR_PALACE_DOUBLE_C` | 1.800 | 5.02 × 5.15 | 12.72 × 5.30 | 0.395 |
| `WAREHOUSE_PORTAL_GATE_E` | `PM_DOOR_PALACE_DOUBLE_C` | 1.800 | 5.02 × 5.15 | 12.72 × 5.30 | 0.395 |
| `WAREHOUSE_PORTAL_GATE_W` | `PM_DOOR_PALACE_DOUBLE_C` | 1.800 | 5.02 × 5.15 | 12.72 × 5.30 | 0.395 |

- Итог: 30 экземпляров под контейнером `FARM_0_2_TIMBER_DOORS`, **30 `DoorController`** живы,
  77 рендереров, 77 коллайдеров, все объекты дверей — слой **10**. Неравномерного масштаба нет; s ≤ 1.8. [F]
- **Оставлено как механизм/негабаритная геометрия (5 сборок):** 3 люка `UNDERDECK_SPINE_HATCH_0{1,2,3}_LID`
  (горизонтальные крышки 1.27 × 1.91, не створки) и 2 створки карго-лифта
  `CARGO_LIFT_L_HEAVY_TO_LOGGING_GATE_{TOP,BOTTOM}`. Вместе с ними сохранены 6 треков лифта
  `..._GATE_TRACK_*` и 7 статических треков `RES_HOUSE_*_DOOR_FRONT_TRACK`. [F]
- Открытие/закрытие в runtime не проверялось: Play Mode не запускался.

---

## 4. Лампы [F]

- 43 authored-якоря `LAMP_*` → 43 prefab-instance под контейнером `PRIMUM_FARMLIGHT`.
- Подбор по типу светильника: `PM_LIGHT_STREET_POST` ×27, `PM_LIGHT_INTERIOR_PENDANT` ×9
  (8 купольных `DOME_0x` с плафонами `*_HANG_*` + `WH_OFFICE`), `PM_LIGHT_FACADE_WALL` ×7 (`RES_A1…A4`, `RES_B1…B3`).
- Посадка: расстояние от якоря до ближайшего плафона (bounds-дистанция) — **min 0.00 / median 0.00 / max 0.00**,
  покрыто **43 из 43** различных плафонов (`M_PC_Lantern`), то есть на каждый якорь ровно свой светильник.
- Все `Light`: `Point`, `shadows = None`, `enabled = false`, слой 8. Ни одного `Light` вне светового контейнера.
- Материал плафонов `MF_EMISSIVE` переведён на существующий `M_PC_Lantern` — эмиссия сохраняется,
  новые материалы не создавались.

---

## 5. Слияние [F]

- Ключ группы: `(верхний блок, материал, rendererSettings)`, где `rendererSettings = layer | shadowCastingMode |
  receiveShadows | lightProbeUsage | reflectionProbeUsage | motionVectorGenerationMode | renderingLayerMask`.
- `indexFormat = UInt32`, `CombineMeshes(mergeSubMeshes: true, useMatrices: true)`, `RecalculateBounds()`,
  потолок 60 000 верт на батч.
- Результат: **79 батчей**, 28.08 МБ, 313 972 верт; максимальный батч **49 512** верт
  (`PM_FRM_02_10_PLATFORMS_M_PC_Steel_v2_00`), батчей сверх потолка — **0**.
- Распределение батчей по блокам: `30_INDUSTRY` 13, `60_RESIDENTIAL` 13, `90_DECOR` 10, `40_DOCKS` 9,
  `50_UNDERDECK` 9, `80_UTILITY` 9, `20_ROUTES` 7, `10_PLATFORMS` 4, `51_SUPPORTS` 4, `70_RAILINGS` 1.
- **Не сливались:** двери (контейнер `FARM_0_2_TIMBER_DOORS`), лампы (`PRIMUM_FARMLIGHT`),
  коллайдеры (`FARM_0_2_TIMBER_COLLIDER`), 7 коллекций `_MOVING`/`_MOVABLE`
  (`CARGO_LIFT_L_HEAVY_TO_LOGGING_MOVING` 9, `CRANE_GANTRY_LOGGING_MOVING` 3, `CRANE_GANTRY_STACK_MOVING` 3,
  `PAD_DOCK_{L_HEAVY,M1,M2,S_RESERVE}_MOVABLE` по 2) — всего 23 рендерера / 552 верт сохранены как есть.
- Слияние сняло только `MeshRenderer` + `MeshFilter`; объекты с коллайдером сохранены как оболочки
  (в `30_INDUSTRY` так сохранены 4 пользовательских коллайдера). Static-флаги на новый визуал не ставились.

---

## 6. Слой PRIMUM [F]

| Слой | Transform | Что это |
|---:|---:|---|
| 8 `PRIMUM` | **951** | весь визуал (79 merged + 23 подвижных), 43 лампы, все группы коллекций и 43 якоря `LAMP_*` |
| 10 `PRIMUM_DOORS` | **155** | 30 дверных instance целиком, включая их коллайдеры и створки |
| 0 `Default` | **288** | только объекты пользовательских коллайдеров (284 в `FARM_0_2_TIMBER_COLLIDER` + 4 в `30_INDUSTRY`) |

- Рендереры: 102 на слое 8, 77 на слое 10 — визуальной геометрии на `Default` нет.
- Слой пользовательских коллайдеров **не менялся** (как и на 0-3/0-4): он инертен для рендера и culling,
  а `OuterCommZone.pollLayerMask = ~0`, `Physics.GetIgnoreLayerCollision` для пар 0/8/10/31 — без игнора.
  Изменение слоя коллайдеров в этот проход не входит.
- Static-флаги: **440** объектов в `FARM_0_2_TIMBER_COLLIDER` (поставлены пользователем вместе с коллайдерами),
  **0** static-рендереров и **0** static-флагов в визуальной части.

---

## 7. Коллайдеры и целостность [F]

| Проверка | Результат |
|---|---|
| Пользовательские коллайдеры | 288 (284 под `FARM_0_2_TIMBER_COLLIDER`, 4 в `30_INDUSTRY`) — не тронуты |
| Дверные коллайдеры | 77 (из префабов дверей) |
| Всего коллайдеров | 365 |
| `DoorController` | 30 (все живые, ссылки на створки внутри префабов) |
| `Light` | 43, включённых 0 |
| Missing mesh / null-материалы | 0 / 0 |
| Габарит | 262.99 × 53.05 × 142.09 — совпадает с исходным |

---

## 8. Открытые вопросы [E]

1. **Заполнение проёмов.** Ворота-«створки» шириной 12.72 м (SAWMILL_GATE_E/W, WAREHOUSE_PORTAL_GATE_E/W)
   закрываются префабом на 0.395 ширины, `DRYING_CHAMBER_*` — 0.549, `RES_HOUSE_*_DOOR_FRONT` — 0.610.
   Это следствие фиксированного набора из 11 префабов и запрета на неравномерный масштаб.
   Следующий по размеру кандидат (`PM_DOOR_GATE_B`, 3.53 × 3.57) при `s = 1.49` даёт высоту 5.30 м, но это
   одностворчатый префаб в двустворчатом проёме; при `s = 1.8` ширина 6.35 м против 12.72 м — то есть
   по ширине он лучше, по числу створок хуже. Решение оставлено за пользователем.
2. **Нет HLOD.** Бейкер знает только `PRIMUM`, поэтому вся ферма живёт в L0 на всех дистанциях.
3. **Не размещено в мире.** Префаб не инстанциирован в `WorldRoot_0_0/Primum_farms/<name>`, поэтому
   `Project C → Culling → Door Layer → Apply` и замеры Stage 8 по этой ферме ещё не выполнялись.
4. **Runtime-приёмка не пройдена:** открытие/закрытие дверей, посадка створок, ночной свет,
   culling дверей за 150 м — проверяются только в Play Mode пользователем.
5. **Дубли от повторного запуска.** Первый прогон слияния оборвался по таймауту транспорта и был повторён
   Unity; повторно слитые объекты остались с `mesh == null` и удалены точечной чисткой (79 объектов).
   Итоговые числа сверены с бэкапом и сходятся точно, но операция должна запускаться однократно.
   Тогда же `PrefabUtility.InstantiatePrefab` оставил в активной сцене один временный корневой объект
   (`BootstrapScene` стала dirty); лишний корень отброшен перезагрузкой сцены с диска, сцена снова чистая
   (`dirty = false`, 68 корневых объектов). Сцена не сохранялась.

---

## 9. Артефакты

| Что | Путь |
|---|---|
| FBX | `Assets/_Project/Models/Primumfarm/0-2/Project-C_primum_farm_0-2_GameReady.fbx` |
| Префаб | `Assets/_Project/Prefabs/City/Farms/FARM_0_2_TIMBER.prefab` (1 714 380 Б) |
| Бэкап до прохода | `_local_backups/farm_0_2_opt_2026-11-05/FARM_0_2_TIMBER.prefab` (14 700 541 Б) |
| Карта коллекций | `_local_backups/farm_0_2_export_2026-11-04/farm_0_2_collections.tsv` |
| Слитые меши | `Assets/_Project/Prefabs/City/Farms_Merged/PM_FRM_02_*.asset` — 79 файлов, 28.08 МБ |
| `.blend` | `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primumfarm_0_2\ProjectC_farm_primumfarm_0-2_1.blend` |
