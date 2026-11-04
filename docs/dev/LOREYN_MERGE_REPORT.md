# «Лорейн» — материалы палитры и мерж статики (класс Light, метрический класс S)

**Дата:** 2026-11-04 · **Проект:** `C:\Projects\ProjectC_client` · Unity 6000.5.2f1 / URP 17.5.0 Forward+
**Правила:** [`MAIN_PIPELINE_MESH_SHIP_LIGHT`](../world/optimization/MAIN_PIPELINE_MESH_SHIP_LIGHT/README.md) → `SHIPS.md` §6.1 и §6.3 шаги 4–5, `COMMON.md` §3 шаг 7, §4, §5
**Инструмент:** `Assets/_Project/Editor/Ships/LoreynVisualMerger.cs` (меню `ProjectC → Ships → Loreyn (Light) → 1…9`)
**Данные:** `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_loreyn_merge_2026-11-04.csv`
**Бэкап (вне Assets, не в git):** `_local_backups/MAIN_PIPELINE/Loreyn/Лорейн.prefab.pre-merge.bak` (до материалов и мержа)

---

## 1. Состояние до

| Параметр | Значение |
|---|---|
| Префаб | `Assets/_Project/Prefabs/Ships/Лорейн.prefab`, root scale 1, tag `Ship`, `ShipController.shipFlightClass = Light` |
| Визуал | `MainVisual/project-c_ship_light_prem_1_detail_1_ship` — плоская копия модели, scale 1.06 |
| Модель FBX | `Assets/_Project/Art/Models/Ships/Light_prem_1/project-c_ship_light_prem_1_detail_1_ship.fbx` (импорт 1:1 с `Heavy_base`) |
| Габарит | **5.164 × 2.557 × 5.147 м** → метрический класс **S** (полоса <30 м) |
| MeshRenderer | **383** (379 визуал + 4 интерактив) |
| Материалы визуала | 10 слотов `URP/Lit` из FBX (ни одного палитрового) |
| Треугольники / вершины | 20 504 / 38 856 |
| Коллайдеры / двери | 25 (23 `BoxCollider` + Sphere + Capsule) / 1 `DoorController` (`Door_Gangway`) |
| LODGroup / ShadowBudget | нет / нет (для класса S не требуются) |
| Размер .prefab | 1 142 366 Б |

Коллайдеры и дверь сделаны пользователем скриптом: 19 узлов `BC_*` в 8 группах `<узел>_Colliders`, створка `Door_Gangway_HingeT → Door_Gangway_HingeB → Door_Gangway` (`BoxCollider` + `DoorController` + `ShipRootReference`).

---

## 2. Что сделано

### 2.1. Материалы палитры (`COMMON.md` §3 шаг 7)

Подмена по имени материала, `sharedMaterials` в тот же индекс слота (никакого `.material`):

| Исходный материал FBX | Материал палитры | Шейдер | Рендереров |
|---|---|---|---:|
| `Metal_Bronze_Polished` | `M_PC_Bronze` | Bronze | 70 |
| `Metal_Brass` | `M_PC_Bronze_Brass` | Bronze | 214 |
| `Wood_Light_Lacquer` | `M_PC_Wood` | Wood | 24 |
| `Leather_Light` | `M_PC_Fabric_Cream` | Fabric | 45 |
| `Carpet_Dark` | `M_PC_Fabric` | Fabric | 11 |
| `Lamp_Amber` | `M_PC_Lantern` | Lantern | 1 |
| `Lamp_White` | `M_PC_Lantern_Cool` | Lantern | 1 |
| `Glass_Green_Matte` | `M_PC_Glass_Green` | Glass | 11 |
| `Nav_Red` | `M_PC_Lantern_Red` | Lantern | 1 |
| `Nav_Green` | `M_PC_Lantern_Green` | Lantern | 1 |

Стекло и свет разведены по требованию: **на стёклах только Glass**, Lantern — только на реальных приборах.
Проверено по узлам: `Canopy_Glass`, `Port_Glass`, `Skylight_Glass`, `Skylight_GlassTop`, `Wall_FrontGlass`,
`GaugeGlass_*` → `M_PC_Glass_Green`; `CabinLamp` → `M_PC_Lantern_Cool`, `NavLight_Port` → `M_PC_Lantern_Red`,
`NavLight_Stbd` → `M_PC_Lantern_Green`, `Antenna_Tip` → `M_PC_Lantern`.

Назначений: **379**, слотов `URP/Lit` в визуале после подмены — **0**.

### 2.2. Мерж статики (`COMMON.md` §4, `SHIPS.md` §6.3 шаг 4)

Ключ мержа — `(ячейка, материал, renderer-settings)`. Ячеек две:

| Ячейка | Что входит | Рендереров | Куда ставится результат |
|---|---|---:|---|
| `Hull` | 360 рендереров корпуса/интерьера | 360 | `MainVisual/SHIP_VISUAL_LOR` |
| `Door` | поддерево створки `Door_Gangway` | 19 | дети самого `Door_Gangway` (движется с `hingePanel`) |

Защищено от мержа: **0** — все 379 рендереров статичны, зеркальных трансформов нет, у меш-узлов нет
коллайдеров (коллайдеры пользователя живут на отдельных узлах `BC_*`), выключенных рендереров нет.

Результат: **379 рендереров → 13 мешей** (10 `Hull` + 3 `Door`), Σtris **20 504 → 20 504 (Δ = 0)**.
С исходных узлов сняты только `MeshRenderer` + `MeshFilter`; `Transform`, иерархия, группы `*_Colliders`
и все компоненты остались (повторный мерж и правки модели предсказуемы).

Створка двери: `Door_Gangway` сохранил `BoxCollider`, `DoorController`, `ShipRootReference`;
его визуал переехал в трёх детей `Loreyn_VIS_Door_*`, поэтому створка по-прежнему вращается одним
трансформом вокруг `Door_Gangway_HingeB` (`hingePivot`).

### 2.3. LODGroup и ShadowBudget — по классу

`SHIPS.md` §6.1 для полосы **S** (<30 м): «мерж по материалу → 1 уровень», **LODGroup не нужен**,
**ShadowBudget не нужен**. Ни то, ни другое не добавлялось: `LODGroup` в префабе — 0, `ShipShadowBudget` — нет.

---

## 3. Состояние после

| Параметр | До | После |
|---|---:|---:|
| MeshRenderer (весь префаб) | 383 | **17** |
| — визуал | 379 | 13 |
| — интерактив (`PilotSeat`, `Exchanger`, `Slot_Engine`) | 4 | 4 |
| Слотов материала в кадре (весь корабль) | 383 | **17** |
| DC-модель `2×P + C` | ≈1 149 | **≈51** |
| Σtris | 20 504 | 20 504 |
| Материалов в палитре | 10 (URP/Lit) | 10 (`M_PC_*`), ≤24 по §5.2 |
| Сабмешей на меш | 1 | 1 |
| Коллайдеры / двери | 25 / 1 | **25 / 1** |
| Размер .prefab | 1 142 366 Б | **512 801 Б** (−55 %) |
| Mesh-ассеты | 0 | 13 в `Assets/_Project/Generated/Ships/Loreyn_VIS_*.asset` (4 117 690 Б) |

Гейт полосы S — **≤30 DC (≤10 слотов)** — численно **не пройден** (17 слотов): корабль несёт 13
материальных групп (10 палитры × 2 ячейки) и 4 интерактивных рендерера. Это следствие детализации модели
(379 деталей, 10 исходных материалов), а не потери правил: 379 → 13 слотов (−96.6 %).
Если гейт нужно закрыть точно, есть два обратимых шага без правки инструмента: слить палитру до 6
(`Bronze`+`Bronze_Brass`, `Fabric`+`Fabric_Cream`, `Lantern`+`Lantern_Cool`) и/или перевести 4 интерактивных
рендерера на палитру — это меняет визуальное решение и требует отдельного согласования.

---

## 4. Проверки

| Проверка | Результат |
|---|---|
| `1. Materials (dry-run)` | 379 назначений, 379 слотов `URP/Lit` до → 0 после |
| `3. Merge Visual (dry-run)` | 379 к склейке / 0 защищено, 13 групп (10 Hull + 3 Door) |
| Σtris источник = склейка | **20 504 = 20 504** (Δ = 0) |
| Мировой AABB | 5.164 × 2.557 × 5.147 → **5.164 × 2.557 × 5.092**; склейка **внутри** исходного AABB, сжатие **1.1 %** |
| Причина сжатия | `Renderer.bounds` — AABB меша, развёрнутого трансформом (у повёрнутых деталей раздувается); склейка даёт точный AABB по вершинам — поэтому сравнение одностороннее |
| Нормали склеенных мешей | `normals.Length == vertexCount` у всех 13 |
| `5. Verify` | **OK**, 0 проблем |
| Инварианты | 1 `DoorController`, 23 `BoxCollider`, 25 коллайдеров всего, 19 узлов `BC_*`, 8 групп `*_Colliders`, 10 узлов `Slot_*`, 0 missing script |
| Ссылки двери | `hingePanel = Door_Gangway`, `hingePivot = Door_Gangway_HingeB` — живые |
| Визуал створки | 3 склеенных меша — дети `Door_Gangway` (проверено `Verify`) |
| Материалы | все 13 мешей — `M_PC_*`, по одному материалу и одному сабмешу |
| Компиляция / консоль | `No compile errors`, 0 errors, 0 shader errors |

### Что не входит в счёт `Verify`

4 интерактивных рендерера (`PilotSeat`, `Exchanger`, `Cube`/`Cylinder` в `Slot_Engine/EngineVisuals`) остаются
на `URP/Lit` — для одиночных объектов это разрешено `COMMON.md` §5.1. Мерж их не касается.

---

## 5. Что осталось

- **Визуальный sign-off в Play Mode за пользователем**: посадка в корабль, вид кабины, стекло/лампы,
  открытие-закрытие `Door_Gangway`, полёт. Числа и инварианты — не замена этой проверке (`CHECKLISTS.md` §1).
- Посадка `PilotSeat`, `Door`, `Exchanger`, `ShipCargoVisual`, `Slot_*`, `DeckNavSurface` относительно новой
  модели — авторская зона пользователя, мерж её не менял.
- Runtime-замер DC для «Лорейна» не проводился: `FinalValidationBench` зашит на `Ship_havyII_base`.
- FO-каталог не трогался: префаб в сцене не инстанцировался.

---

## 6. Откат и повтор

```
ProjectC → Ships → Loreyn (Light) → 9. Rollback (from backup)   // префаб из бэкапа + удаление Loreyn_VIS_*
```

Повтор для следующего корабля класса S: скопировать `LoreynVisualMerger.cs`, заменить `PrefabPath`,
`ModelRootName`, `MergeRootName`, `MeshPrefix`, `MaterialMap`, `BackupRelativePath`, `ReportRelativePath`
и блок инвариантов (двери / коллайдеры / `BC_*` / `Slot_*`). Абсолютные числа «Альбатроса» и «Лорейна»
переносить нельзя (`SHIPS.md` §6.5).
