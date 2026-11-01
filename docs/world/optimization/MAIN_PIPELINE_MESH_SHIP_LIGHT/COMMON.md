# COMMON — общие правила контента ×100

← [README](README.md) · [CHECKLISTS](CHECKLISTS.md) · **§1–§5 и §10–§12 — здесь**; модули: §6 → [SHIPS.md](SHIPS.md), §7 → [SETTLEMENTS.md](SETTLEMENTS.md), §8 → [LIGHTS.md](LIGHTS.md), §9 → [CITY_HLOD.md](CITY_HLOD.md)

## 1. Что зафиксировано (потолки, из которых нельзя выходить)

### 1.1. Бюджеты мира (принятая база, 2026-11-01)
**Канон масштаба:** `k = 1.06` (решение 2026-11-01). 1.07 в старых README — опечатка, не использовать.

| Метрика | Факт сейчас | Гейт | Комментарий |
|---|---:|---:|---|
| **L0 P1** (площадь города, игра) | **2 229–2 235 DC** | **≤2 500** | SetPass 173, 11.79 M тр., hybridDC 2 129, srpBatcherDC 3 |
| **L1** (город 3–12 км) | **46 DC** | **≤500** | 23 блока × 2 DC; HLOD-меш на блок |
| **L2** (город 12+ км) | **2 DC** | **≤10** | 1 квад-импостор на город |
| Двери >150 м в кадре | **0** | **0** | контроль без политики — 2 633; 5 493 дверных MR на слое 10 |
| `hybridDC` / инстансов | 2 129 / 4 191 | — | 1.97 инстанса на батч — потолок, пока пары уникальны |
| Свет: источников / включено | **2 238** / ≤96 | cap 96 | radius 100 м, веер, dayGate |
| Свет: CPU | ALIC 0.16 мс/кадр, пул 0.23–0.39 мс | ALIC ≤1 мс | O(N) на чек пула |
| Материалов в городе / в сцене | **49 / 92** | ≤64 в новой сцене | 15 шейдеров, 0 текстур, 0 прозрачных |
| Сцена `WorldScene_0_0` | 60 МБ, 4 590 GO, 4 906 prefab-instance | ≤100 МБ | монолит; см. §9.5 |
| CPU | culling 0.72–0.93 мс (L0/L1), 2.16 мс (L2) | ≤2 мс средн. | пики до ~4.95 мс — норма телепорта |

### 1.2. Формула DC — главное, что определяет все правила

Проверена на 7 независимых замерах (`_evidence/05_MATERIALS_DC.md` §1.1), ошибка ≤1 DC:

```
DC(L0) ≈ 2 × P + C + O
P = число уникальных троек (mesh, material, renderer-settings) в кадре
C = число shadow-кастеров в кадре        (1 DC на кастер)
O = 106…155 DC постоянной добавки        (небо, UI, TMP, террейн, руины)
```

**Следствия (запомнить):**
- **Одна уникальная пара = 2 DC.** Уникальный материал на каждый дом/проп = `+2 DC` за объект.
- **Один сабмеш = ещё одна пара = +2 DC.** Дом 12 материалами = 24 DC; тот же дом 1 мешем/1 материалом = 2 DC.
- Число **рендереров почти не влияет** на DC (GRD инстансит одинаковые пары); влияет число **пар**. Город: 1 207 MR
  → 1 806 слотов → 1 806 пар → весь город в кадре дал бы ≈3 612 DC, поэтому и существует HLOD (23 пары → 46 DC).
- Кастеры: каждая тень = 1 DC. Кастомные бюджеты теней (`CityShadowBudget`, `ShadowBudget`, `ShipShadowBudget`,
  `DoorVisibilityBudget`) — единственный способ их ограничить.
- **Пропы = 95 % слотов, но 25 % треугольников.** Мелочь <0.25 м = 16.4 % тр. города, скрытые интерьеры = 78.6 % —
  всё это платится слотами. Поэтому «оптимизировать треугольники» ≠ «оптимизировать кадр».

### 1.3. Карта владения (кто пишет какое свойство — не перепутать)

| Свойство | Владелец | Новому коду писать? |
|---|---|---|
| `Light.enabled` | `PrimumLightPool` (на `PRIMUM_LIGHTS`) | ❌ нет |
| `Light.intensity`, `range`, `shadows` у ламп | `AdditionalLightIntensityController` (ALIC, на `DayNightController`) | ❌ нет |
| `Renderer.forceRenderingOff` | **только `VisibilityDirector`** | ❌ никогда |
| `Renderer.shadowCastingMode` | `CityShadowBudget` / `ShadowBudget` / `DoorVisibilityBudget` / `ShipShadowBudget` | ❌ нет (только через свой бюджет) |
| `Renderer.enabled` | `DetailDistanceCuller` (руины) | ❌ нет |
| `layerCullDistances[10]`, `camera.far`, `lodBias` | `DoorVisibilityBudget` / `ViewDistanceApplier` | ❌ нет |
| `DoorController`, коллайдеры, `SetActive` | геймплей | ⚠️ только осознанно, не из куллинга |
| HLOD-корень `PRIMUM_HLOD` | `VisibilityDirector` (runtime, `HideFlags.DontSave`) | ❌ нет |

---

## 2. Двенадцать золотых правил (нарушение = регресс)

1. **Всё новое — под активный корень своей `WorldScene_*`.** Дети корней едут с Floating Origin бесплатно;
   объекты вне корней/неактивные корни — ломают F8/F9 (`_evidence/03_CITY_HLOD.md` §1.3).
2. **`NetworkObject` в сцене → обязательна регистрация** через `ProjectC → World → Floating Origin → Bake Pilot
   Catalog (Preview + Apply)` (маркер + observation + entry + `layoutHash`/`dependencyHash` + digest + непустой
   `reviewNote`). Без неё Host падает: `uncontrolled_network_source_before_native_sweep`.
3. **Никаких static-флагов, Batching, ContributeGI, Occluder** — мир двигается под FO, baked world-space ломается.
   Static-флаг есть только у `Terrain_0_0`.
4. **Не писать чужие свойства** (§1.3): `forceRenderingOff`, `shadowCastingMode`, `enabled`, `intensity`.
5. **Мержить только статику и только edit-time**, результат — коммитируемые `.asset` в git. Runtime `CombineMeshes`
   запрещён (хитч + недетерминизм). Мерж через scene-overrides — запрещён (хрупко: reimport/Apply сносит).
6. **Ключ мержа** = `(ячейка/юнит, материал, renderer-settings)`. Настройки рендерера (`layer, shadows, receive,
   lightProbes, reflectionProbes, motionVectors, renderingLayerMask`) должны быть одинаковы у копий, иначе группы
   дробятся (`ShipVisualMerger.AddToGroups`).
7. **Двери, створки, интерактив, коллайдеры, VFX — никогда не мержить.** `DoorController` требует `Collider`;
   створка едет с узлом модели; коллайдеры — compound из примитивов.
8. **Уникальный материал = +2 DC.** Палитра: 49 `M_PC_*`/`PM_*`; 1 материал на проп; ≤12 на дом; ≤24 на статик-визуал
   корабля; сабмешей ≤4. Никаких `MaterialPropertyBlock`/`.material` на массовом контенте.
9. **Интерьеры — отдельно от экстерьера.** 78.6 % тр. города — скрытые интерьеры; не класть их в общий батч,
   не моделить мелочь <0.25 м (16.4 % тр.).
10. **Свет — только на реальные приборы.** Point, `range` 3.5–14 м (никогда >20), `shadows=None`, `enabled=false`
    в префабе, имя с префиксом группы (`PRIMUM_OUTLIGHT_/INLIGHT_/FARMLIGHT_`), точка — центр стекла прибора.
11. **Новый блок в `PRIMUM` → перепечь HLOD** (`Stage 7 HLOD → Bake All Blocks + L2` + `Rebake Determinism Check`).
    Не перепечь = новый контент живёт только <3 км.
12. **Имя = контракт.** `PRIMUM`, `Primum_farms`, `Secund/Tertius/Quartus`, `WorldRoot_0_0`, `PRIMUM_DOORS/<блок>`,
    `PRIMUM_LIGHTS/<чанк>`, `PRIMUM_HLOD` — переименование молча ломает директор/HLOD/FO.

---

## 3. Пайплайн ассета: Blender → Unity → сцена → проверка

| # | Шаг | Где / чем | Критерий приёмки |
|---|---|---|---|
| 0 | **Класс ассета**: корабль (S/M/L/XL), дом/ферма, проп, свет | §6, §7, §8 | выбран бюджет из §6.1 / §7.2 |
| 1 | **Blender: коллекции и имена** | коллекции `60_BUILDINGS/<зона>/<дом>/{05_FLOOR,10_WALLS,…,90_MARKER}`; объекты `<ДОМ>_<КАТЕГОРИЯ>_…`; `organize_buildings()` | нет объектов «в куче», 1 дом = 1 префикс |
| 2 | **Blender: масштаб и геометрия** | 1 unit = 1 m; масштаб правится **только в Blender и только у мешей** (k=1.06 применён к городу, `01_SCALE_DECISION.md`); нормали по связным островам (0 вывернутых); толщина у плит; UV `smart_project` **после join**; ≤65 535 верт./меш; ≤80 k тр./блок | 0 вывернутых, нет вырожденных, нет прошиваний (clash-audit) |
| 3 | **Blender: join** | блок × материал; не склеивать стекло/эмиссию/воду; флэтнить иерархию; ≤4 сабмеша | таблица «блок → меши → тр. → габарит» |
| 4 | **Экспорт FBX** | `axis_forward=-Z, axis_up=Y, FBX_SCALE_NONE, global_scale=1, use_mesh_modifiers=True, object_types={MESH,EMPTY}`; поставка в `_deliver_*/`, бэкап в `_versions/` | в Unity приходит X≈270° и ×100 — это норма, компенсируется `useFileScale` |
| 5 | **Импорт Unity** | подмена **по тому же пути/имени** (GUID сохраняется); `.meta` не трогать; `globalScale=1`, `useFileScale=1`, `meshCompression=Off`, `isReadable=Off`, `addColliders=Off`, `generateSecondaryUV=Off`, `bakeAxisConversion=Off`, `indexFormat=UInt16` | габариты Δ ≤0.02 м; 0 missing |
| 6 | **Y-up rebase мешей** (только город/чанки) | `PrimumMeshRebase`, меню 388 (`ProjectC → World → PRIMUM → …`) | verify «1:1 PASS» |
| 7 | **Материалы** | палитра `M_PC_*`; подмена по имени (`PrimumMaterialMap`, `material_map.csv`); emissive — `PM_LAMP_GLOW_WARM` | 0 слотов `URP/Lit` после verify; `m_ValidKeywords: []` |
| 8 | **Префаб** | root scale 1; визуал ребёнком (`MainVisual` для корабля, `SHIP_VISUAL_H2` для heavy); коллайдеры — **отдельные дети** (`SHIP_COLLIDERS`), не внутри FBX; двери — на узлах модели (`DoorController`+`Collider`); свет — отдельные `PM_LIGHT_*` | reimport FBX не сносит коллайдеры/двери |
| 9 | **Слой и родитель** | город/свет — слой **8** `PRIMUM`; двери — слой **10** `PRIMUM_DOORS`; корабль — корень сцены + `ShipRootReference` + tag `Ship`; фермы/станции — `WorldRoot_0_0/Primum_farms` | слой назначен инструментом, не руками |
| 10 | **Сцена** | инстанс под нужным корнем (§9.1); **не** держать город разложенным; модель не править внутри FBX-инстанса | сцена `isDirty=false` |
| 11 | **Сеть/FO** | есть `NetworkObject` → шаг 12; иначе (декор/статика) — ничего | Host стартует |
| 12 | **FO-каталог** | `Bake Pilot Catalog (Preview + Apply)` | `AuditGlobalSceneCatalog` без новых расхождений; digest сходится |
| 13 | **HLOD** (если под `PRIMUM`) | `Project C → Culling → Stage 7 HLOD → Bake All Blocks + L2` → `Rebake Determinism Check` | 23+N блоков, `impostor=yes`, `DETERMINISTIC; mismatches=0` |
| 14 | **Проверка** | edit-mode verify инструментов + `CHECKLISTS.md`; runtime — пользователь | 0 errors; бюджеты §1.1; F8/F9 PASS |

**Импорт сейчас неоднороден:** `materialImportMode=1` (город) vs `2` (фермы/корабли), `animationType=0` vs `2`.
Для нового контента бери вариант **как у ферм/кораблей (2)** и фиксируй в отчёте; городские чанки не трогай.

---

## 4. Мердж: что можно и что нельзя (матрица)

### 4.1. Ключ мержа (реализован в `ShipVisualMerger`)

```
key = cell/unit + "|" + assetPath#materialName + "|" + rendererSettings
rendererSettings = layer|shadows|receiveShadows|lightProbes|reflectionProbes|motionVectors|renderingLayerMask
```
Сабмеши исходника разворачиваются: на каждый `subMeshIndex` — свой `CombineInstance` (лечит разные материалы в меше).

### 4.2. Матрица

| Категория | Мердж? | Ключ / как | Факт и почему |
|---|---|---|---|
| **Городской экстерьер** | ✅ **MERGE** | `(блок × материал)` | Уже сделано: 1 016 батчей / 1 059 слотов, AABB 130 м; **это оптимум** |
| Город — дробление по дому (B2) | ❌ **НЕ ДЕЛАТЬ** | — | `B2_increases`: 330 батчей против 160, DC растёт; окклюзия ≈0 % |
| Город — ячейки 10–30 м | ❌ **НЕ ДЕЛАТЬ** | — | режет дома и швы, `+16…+47 %` видимых слотов |
| Интерьеры | ⚠️ **ОТДЕЛЬНО** | отдельный меш/слой, не в общий батч | 78.6 % тр. города не видны снаружи |
| Мелочь <0.25 м | ❌ **не моделить / вырезать** | — | 16.4 % тр. города = 888 284 тр. |
| Ферма | ✅ **MERGE** по `(ферма × группа × материал)` | **99 merged + 43 = 145 MR / 150 слотов** | «1 меш на ферму» экономит ≤280 DC, но тащит **1.27 M тр. всегда** → ❌ |
| Корабль — greebles | ✅ **MERGE** | 6 префиксов: `Bolt_*, Mess_*, UnderHull_*` → `GrbFine`; `Interior_*, Contour_*, Det_*` → `GrbDetail`; ключ `(ячейка, материал, settings)` | HeavyII: **378 узлов → 10 мешей**, Σtris **0.0 %** потерь, 973 → 616 MR |
| Корабль — LOD1/LOD2 | ✅ **MERGE** | LOD1: 1 меш на материал (10); LOD2: 1 меш, ≤5 сабмешей | HeavyII LOD2 = 30 164 тр., 53 666 верт. |
| **Двери / створки / рампы** | ❌ **НИКОГДА** | — | `DoorController` требует `Collider`, створка едет с узлом; дубли дали бы 259 боксов |
| **Интерактив** (`PilotSeat`, `ModuleSlot`, `CargoVisual`, двигатели с `EngineThrusterVisual`/`ShipPartShake`, `ContrailVFX`) | ❌ **НИКОГДА** | — | динамика/сеть/VFX; `ShipPartShake` двигает трансформы |
| **Коллайдеры** | ❌ **НИКОГДА** | compound из примитивов | на heavy 250 боксов + 10 leaf дверей; мерж геометрии не должен менять физику |
| **VFX** | ❌ **НИКОГДА** | — | после мержа `ShipContrailVfx.UseShipBounds=false` + `ManualBoundsSize` |
| **Зеркальные трансформы** (`det < 0`) | ❌ **исключить** | — | инвертируется обход треугольников |
| **Пропы/декор** | ✅ **MERGE по типу** | общий меш + общий материал | сейчас проп = 143 тр./0.4 м/1 слот; 15 452 объектов = 19 635 слотов (95 % слотов!) |
| **Runtime `CombineMeshes`** | ❌ **ЗАПРЕЩЕНО** | — | хитч и недетерминизм; только edit-time `.asset` |
| HLOD-рендереры, `PRIMUM_HLOD` | ❌ **не трогать** | — | runtime-корень директора |
| Выключенные рендереры | ❌ **не включать в мерж** | — | попадут в кадр |

### 4.3. Как прикинуть DC нового ассета (до замера)

```
DC ≈ 2 × (число различных (mesh,материал,настройки)-слотов в кадре) + (сколько из них кастует тень)
```
- Дом 12 материалов, 1 меш на материал: `2×12 + 12 = 36 DC`. Тот же дом 1 мешем/1 материалом: `3 DC`.
- HeavyII в кадре 555 слотов: `2×555 + 60 = 1 170 DC` — совпало с замером.
- Весь город (1 806 слотов) дал бы 3 612 DC — поэтому нужен HLOD (23 пары = 46 DC).

---

## 5. Материалы

### 5.1. Разрешено / запрещено

| Разрешено | Условие |
|---|---|
| **Project Shader Graph** (Wood, Bronze, Glass, Cobble, Brick, Concrete, Fabric, Foliage, IronRust, Lantern, Marble, Plate, Steel_v2, Tiles) | у всех есть вариант `DOTS_INSTANCING_ON` (GRD-совместимы) |
| **`URP/Lit`** | одиночные объекты; SRP-batcher-совместим |
| **`URP/Unlit`** | только эмиссивные линзы/лампы (`PM_LAMP_GLOW_WARM`) |
| `ProjectC/PrimumHlod`, `ProjectC/PrimumImpostor` | **только** HLOD L1/L2 |
| Один материал на семейство, цвет — в палитре | сейчас 49 материалов на 20 955 слотов = 428 слотов/материал |

| ❌ Запрещено | Почему |
|---|---|
| Уникальный материал на каждый объект | +2 DC за объект; именно так устроены 16 125 «пропов» prefab-ассета |
| `MaterialPropertyBlock` на массовом контенте | выводит рендерер из SRP Batcher/инстансинга (MPB есть только на геймплейных одиночках) |
| `.material` (инстанс) вместо `.sharedMaterial` | утечка + уникальный материал |
| Standard (Built-in) шейдер / шейдер без `DOTS_INSTANCING_ON` | GRD не инстансит |
| Per-material keywords (`_EMISSION`, `_ALPHATEST_ON`, `_SURFACE_TYPE_TRANSPARENT`) рядом с близнецами без них | материал выпадает из группы вариантов → новый батч |
| Полупрозрачность «для красоты» массово | ломает depth-проход и инстансинг (в городе 0 прозрачных — норма) |
| HDR-emissive | `m_SupportsHDR: 0`: значения >1 клипаются; свечение даёт bloom, а не материал |
| Текстура >2k на проп; атлас «на материал» | `atlasGain = 0` — атлас не рычаг DC |

### 5.2. Бюджеты

| Параметр | Лимит |
|---|---|
| Материалов в новой сцене | **≤64** (сейчас в сцене 92: город 49 + фермы 4 + прочее) |
| Слотов на ассет: дом / проп / корабль | **≤12 / 1 / ≤24** (статик-визуал) |
| Сабмешей на меш | **≤4** (каждый = +2 DC) |
| Эмиссия | `_BaseColor ≤1.0` RGB; «огонь» — отдельный материал + bloom |
| Текстуры нового контента | albedo sRGB, маски linear, BC7/BC5 (desktop), ≤1024 проп / ≤2048 герой, мипы on, Streaming on, атлас на семейство |
| Имена материалов | **контракт** подмены (`material_map.csv`); не переименовывать |

### 5.3. Гигиена и известные риски

- `PM_LAMP_GLOW_WARM` — пакетный `URP/Unlit` на **2 131 слот (10.2 % города)**, вне палитры Shader Graph,
  с мёртвыми Lit-свойствами; эмиссия 2.4 при HDR off → клипается. Не тиражировать как «эталон эмиссии».
- GPU Instancing включён лишь у 7/49 материалов — под GRD неважно, но при откате GRD палитра не готова.
- Интерьеры (78.6 % тр.) и мелочь <0.25 м (16.4 %) — главный «мусор» по треугольникам; бюджеты тр. без разделения
  экстерьер/интерьер бессмысленны.

---


---

## 10. Проверка и приёмка

### 10.1. Быстрый регресс (перед коммитом контента)

| Проверка | Как | Что должно быть |
|---|---|---|
| Компиляция / консоль | Unity Console | 0 errors / 0 exceptions / 0 shader errors |
| Сцена | `git status`, `isDirty` | сцена не сохранена; изменены только ожидаемые файлы |
| Edit-mode verify | инструменты §11 (`Repair/Verify`, `Visual Merge/Verify`, `Shadow Budget/Verify`, `Свет: проверить`, `Door Layer/Dry Run`, `HLOD/Rebake Determinism Check`) | все инварианты PASS |
| Свет/двери | `Project C → Culling → T-CULL01 Acceptance → Probe` (≈7 мин) | `FocusCamera=ThirdPersonCamera_0`, `intensityZeroActive=0` днём, `HeldForFade=0`, `GateGraceHeld=0`, двери <130 м видны, >150 м = 0 |
| DC/HLOD/FO | `Project C → Culling → Stage 8 Doors → Run Bench` (19 кейсов) | P1 **2 229 ±5 %**, L1 **46 ±5 %**, L2 **2**, двери >150 м = **0**, `hybridDC > 0`, `srpBatcherDC` мал, F8/F9 PASS |
| FO-каталог (если `NetworkObject`) | `ProjectC → World → Floating Origin → Audit` | без новых расхождений, digest сходится |

### 10.2. Красные флаги (остановиться и разобраться)

| Симптом | Вероятная причина |
|---|---|
| P1 > 2 341 DC (гейт 2 500) | новые уникальные материал-пары/сабмеши; не перепечён HLOD; двери не на слое 10 |
| L1 > 500 DC | блоков >~250; блок не попал в бейк (переименован ребёнок `PRIMUM`) |
| L2 > 10 DC | импостор не перепечён / два импостора |
| Двери >150 м в кадре ≠ 0 | двери не на слое 10, или корень `PRIMUM_DOORS/<имя>` не совпал с блоком |
| Свет: `activeIN=96` днём | известный баг кривой `PRIMUM_INLIGHT` (0…1 vs 0…24) — не новый контент |
| Свет: чек пула > 5–7 мс | ламп >5 тыс.; нужен чанк-локальный пул/индекс |
| Мигание теней ферм в 60–200 м | двойное владение: `ShadowBudget` (60/50) и `CityShadowBudget` (200/180) пишут одно свойство; не добавлять третьего писателя, выбрать одного владельца |
| Host не стартует: `uncontrolled_network_source...` | новый `NetworkObject` без маркера/каталога |
| Объекты «уехали» после F8 | объект вне корней сцены / неактивный корень / static-флаг |
| Контент «исчез» на 3–12 км | спрятан в редакторе (`enabled=false`) → не попал в L1-бейк; перепечь HLOD |
| Сцена >100 МБ | город разложен в сцене, а не инстансом `PRIMUM_CITY.prefab` |

---

## 11. Ссылки: инструменты, первоисточники, доказательства

### 11.1. Инструменты (меню Unity)

| Инструмент | Меню | Когда |
|---|---|---|
| `Repair Heavy Prefab` | `ProjectC → Ships → Repair Heavy Prefab (dry-run/apply)`, `Verify (FBX reimport)`, `Rollback` | после каждого reimport FBX корабля, до мержа |
| `Heavy Visual Merge` | `ProjectC → Ships → Heavy Visual Merge (dry-run)`, `(apply P1+P2)`, `Verify`, `Rollback` | мерж greebles + LODGroup |
| `Shadow Budget` (ship) | `ProjectC → Ships → Shadow Budget → Apply/Verify/Rollback` | бюджет теней корабля L/XL |
| `Ship Summary` | `Tools → Project C → Ship Summary` | конфиг/пресеты классов (не DC-инструмент) |
| `Create Ship Preset` | `Tools → Project C → Create Ship Preset` | заготовка нового корабля |
| `Door Layer` | `Project C → Culling → Door Layer → Dry Run/Apply` | назначить слой 10 дверным префабам |
| `PrimumDoorPlacement` | `ProjectC → World → PRIMUM → Двери: аудит/расставить/очистить/verify` | расстановка дверей (301 сохраняет сцену!) |
| `PrimumLampLighting` | `ProjectC → World → PRIMUM → Свет: аудит/расставить/emissive/очистить/verify/пересобрать` | свет |
| `PrimumHlodBaker` | `Project C → Culling → Stage 7 HLOD → Bake Pilot Block / Bake All Blocks + L2 / Rebake Determinism Check` | HLOD |
| `BakePilotSceneCatalog` | `ProjectC → World → Floating Origin → Bake Pilot Catalog (Preview + Apply)` | любой новый `NetworkObject` |
| `T_CULL01AcceptanceProbe` | `Project C → Culling → T-CULL01 Acceptance → Probe` | быстрый свет/двери-регресс |
| `FinalValidationBench` | `Project C → Culling → Stage 8 Doors → Run Bench` (и Stage 6/7) | полный DC/HLOD/FO-регресс |

### 11.2. Первоисточники (обязательное чтение перед работой)

- `docs/world/primum_city/README.md`, `01_SETUP/02_BLOCKS_AND_GEOMETRY.md`, `01_SETUP/03_UNITY_ASSEMBLY.md`,
  `02_PIPELINE/01_SCALE_DECISION.md`, `02_PIPELINE/02_NORMALS_POSTMORTEM.md`, `02_PIPELINE/04_REIMPORT_SWAP_AND_POSTPASS.md`
- `docs/world/primum_city/03_DOORS/`, `04_COLLIDERS/05_HOUSES_BOXES.md`, `08_LIGHTING/01_LIGHTING.md`,
  `09_MATERIALS/01_MATERIAL_UNIFICATION.md`
- `docs/world/primum_city/10_CULLING_RESEARCH/`: `09_BEAM_COMPARISON_AND_DECISIONS.md`, `10_EXECUTION_PLAN.md`,
  `07_SUBAGENT_REPORTS/J_CITY_FARMS_MERGE_RECIPE.md` (гранулярность и рецепт ферм/города)
- `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` (два гейта FO: сдвиг и допуск `NetworkObject`)
- `docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md` (пресеты far/lodBias: 30/60/120 км, 0.7/1.0/1.3)
- `.opencode/rules/blender-mcp.md` (коллекции, масштаб, запрет прошиваний)
- `docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md` (H1–H8, сложный код), `T-CULL01_FINAL_ACCEPTANCE_REPORT.md` (итог)
- `docs/world/lights/LIGHT_CULLING_PLAN.md` (свет L1–L6, ✅ CLOSED), `T-LIGHT16…21_REPORT.md`

### 11.3. Доказательства этого документа (`_evidence/`)

| Файл | Что внутри |
|---|---|
| `_evidence/00_STAGE_AUDIT.md` | аудит 85 утверждений Stage 0–8 + свет: 77 Match, 6 дефектов, 1 Not-found |
| `_evidence/01_SHIPS.md` | флот, HeavyII пилот, инструменты, матрица merge/no-merge, классы S/M/L/XL |
| `_evidence/02_FARMS.md` | дома/фермы: двери (4.65/дом), коллайдеры (≈21/дом), тени (двойное владение), математика 5 vs 250 домов |
| `_evidence/03_CITY_HLOD.md` | иерархия мира, директор, HLOD L1/L2, двери, пределы ×100 |
| `_evidence/04_LIGHTS.md` | 2 238 ламп, группы ALIC, префабы, размещение, бюджет, ×100 |
| `_evidence/05_MATERIALS_DC.md` | формула DC, палитра, правила мешей/мержа, бюджеты тр., антипаттерны |
| `_evidence/06_CONTENT_PIPELINE.md` | Blender→FBX→Unity→префаб→сцена, ModelImporter, FO, чек-лист |

---

## 12. Что НЕ цитировать (устаревшее / опасные формулировки)

Проверено аудитом `_evidence/00_STAGE_AUDIT.md`. В новых документах, отчётах и промптах использовать **только правую колонку**.

| Опасно / устарело | Правильно |
|---|---|
| «HeavyII 973 MR» как текущее | **616 MR** (973 — до-H2) |
| «2 087 ламп» | **2 238** (2 087 = только городские, без 151 ферм) |
| «2 161 дверь» | **≈2 598 инстансов** → 5 493 MR на слое 10; 2 630 `DoorController` |
| «P1 2 362–2 378» как baseline | **2 341** (без политики дверей), текущий **2 229–2 235**; 2 362–2 378 включали камеру меню (+37) |
| «все корабли ≤480 DC» | это модель после **P3**, который не реализован; один heavy = **1 170 DC** |
| «fade-out held ≤75» как улучшение | гейт был **≤16**, провален (10/13) → fade-out **отклонён**, `fadeOutSeconds=0` |
| «activeBehind 26–77 → 0–1» как одно «до/после» | «до» — `coneTurnBehindOutsideRear` (после разворота), «после» — статичный `activeBehindOutsideRear` (12–70 → 0–1); сравнивать одну колонку |
| «culling ≤2 мс» без оговорки | среднее ≤2 мс, **пики до ~4.95 мс** (телепорт) |
| «2 423 DC» как достигнутое состояние | 2 423 — A/B-полигон бенча; реальный компонент дал 2 811, после фикса камер — **2 394** |
| «2 372» как стабильный повтор Stage 6 | безопаснее **2 372–2 378** (в per-run файлах 2 377–2 378) |
| «~1 900 ламп» (комментарий пула, T-LIGHT13) | 2 238 |
| «ShadowDistance 1000 / 2 каскада / soft 0» (lighting-plan, T-LIGHT11) | в ассете **400 / 4 / soft 1** |
| «множитель INLIGHT 15» (T-LIGHT15) | в сцене **1.76** |
| «23 блока» как константа пайплайна | это число детей `PRIMUM`; потолок L1-гейта ≈250 блоков |
| k = 1.07 (старые README) | **канон — 1.06** (решение 2026-11-01); 1.07 не использовать |
| «дом = префаб» | **префабов домов нет**: дом = батч в FBX чанка (архитектурный риск ×100, §7.8) |
| «ферма = N домов» | **системы ферм-из-домов нет**: «Ферма Примума» — портовая станция с merged-визуалом (145 MR); оценки 5/250 домов — экстраполяция (§7.3) |
| `Colliders/PRIMUM_BC_*.prefab` и `Colliders/Houses/PM_HOUSE_*` как «пайплайн домов» | **мёртвые ассеты**: 0 ссылок (18 файлов / 12 111 боксов и 100 мешей); живые коллайдеры — inline `HOUSE_*_BOX` в сцене |
| `layerCullDistances[10] = 150` как рабочая дистанция дверей | **мёртв под GRD**; реально режет `VisibilityDirector`: off **150** / on **135 м** |
| «гистерезис дверей 130–150 м» | в коде **150 / 135**; 130–165 — измеренная смешанная полоса, не гистерезис |
| `activeRadius: 140` в YAML сцены | реально работает **100** (L2-поле `activeRadiusL2`, в сцене не сериализовано) |

---

*Детали и доказательства — в `_evidence/`; история решений — в отчётах Stage 0–8, L1–L6, финальной приёмке.
При изменении любого числа в §1 — обновить `README.md` §2 и `CHECKLISTS.md`.*
