# 01 — КОРАБЛИ: факты и числа (T-CULL01, пилот HeavyII)

**Собрано:** 2026-11-01, только чтение (Play Mode/бенчи не запускались). Unity 6000.5.2f1, URP 17.5.0, `C:\Projects\ProjectC_client`.
**Сокращения:** `D/` = `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/`; `R3` = `docs/dev/T-CULL01_STAGE3_REPORT.md`;
`PM` = `…STAGE3_PLAYMODE_2026-10-31.md`; `R3C` = `…STAGE3C_REPORT.md`; `BR3B` = `…STAGE3B_AGENT_BRIEF.md`;
`HO` = `…COMPLEX_CODE_HANDOFF.md`; `EP` = `10_CULLING_RESEARCH/10_EXECUTION_PLAN.md`.
**Метки:** **[F]** факт из файла/замера · **[M]** edit-mode модель · **[E]** оценка/экстраполяция · **[U]** не подтверждено.

## 1. Флот: префабы, классы, размеры, стоимость

| Префаб | Длина, м | `shipFlightClass` | Роль | MR сейчас | tris | уник.мат./слоты | Коллайдеры | Двери | LODGroup | DC (слоты×3) |
|---|---:|---|---|---:|---:|---|---|---:|---|---|
| `Ship_havyII_base` | **324.9** | HeavyII (3) | игрок, 1 инстанс | **616** (было 973) | 43 520 | 17 / **604** LOD0 | 250 Box, 0 Mesh | **9** | 3 уровня | 1812/171/156 **[M]**; 1170 замер |
| `Ship_medium_base` | 73.1 | Medium (1) | игрок, 1 инстанс | 118 | 23 006 | 17 / 118 | 3 Mesh + 3 Box | 1 | нет | 354 **[M]** |
| `Ship_Light_root_fbx_test` | 18.5 | Light (0) | игрок, 1 инстанс | 428 | 54 528 | 25 / 428 | 2 Mesh + 15 Box | 2 | нет | 1284 **[M]** |
| `DONT-DELETE-NPC REFERENCE SHIP` | — | Medium (1) | шаблон NPC | 6 | 820 | 1 / 6 | 5 Box + 1 Capsule + 1 Sphere | 1 | нет | 18 |
| 20 NPC (`Альбатрос`…`Шмель`) | 8–273 | 0/1/2/3 (§8.3) | NPC/декор | 6 каждый (**120**) | 820 каждый | 1 / 6 | 7 каждый | 1 | нет | 360 на 20 |
| `Prefabs/NPC_Ship_HeavyII_03.prefab` | — | HeavyII **[U]** | 0 инстансов | 2 | 24 | 1 / 2 | 3 Box | 1 | нет | 6 |

**Источники:** `D/I_ships_prefabs.csv:2-26`, `D/C_ships.csv:2-25`, `D/I_ships_scene.csv:2-24`, `D/I_ships_fbx.csv:2-7`;
счёт компонентов — прямой разбор YAML префабов (`!u!23/33/65/64/205/114`), 2026-11-01.
**Сцена `WorldScene_0_0`:** 23 инстанса, **1 639 MR** в аудите **[F на дату аудита]** → после H2 (**616** у heavy) **1 282 MR**
[арифметика]. Доля: 18.4 % рендереров сцены (8 891) при 2.0 % треугольников (`02_FACTS_AND_MEASUREMENTS.md:232`).
Модель DC всех кораблей **4 917** (16.2 % кадра 21 118, конфиг B) — `D/I_ships_dc_model.csv:6`; после H2 + бюджета **≈3 168** [E].
**Файлы heavy (факт):** 570 своих MR + 46 MR на узлах вложенного FBX (5 `PrefabInstance`); 23 `MeshFilter`
(21 сгенерированный меш, один переиспользован, 1 пустая ссылка); 1 `LODGroup`; 45 `MonoBehaviour`
(9 `DoorController`, 11 `ModuleSlot`, 2 `EngineThrusterVisual`, 2 `ShipPartShake`, 1 `ShipShadowBudget`, 1 `VisualEffect`).
Medium: 2 своих MR + 5 nested FBX = 118 flat. Light: 2 своих MR + 3 nested = 428 flat.

## 2. HeavyII-пилот

### 2.1 Исходное состояние
FBX `project-c_ship_heavyII_1_…_game-ready_1.fbx`: 2 455 612 байт, **936 узлов** (1 Null + 935 Mesh), **37 128 tris**,
10 материалов корпуса, 15 089 квадов + 880 нгонов, **470 мешей ≤24 tris**, медиана 18 tris (`D/I_ships_fbx.csv:2`).
H1 (`ShipPrefabRepair`): 250 `BoxCollider` + 9 `DoorController` вынесены из FBX-инстанса в `SHIP_COLLIDERS`;
**10 leaf-коллайдеров створок остались на узлах FBX** намеренно (`ShipPrefabRepair.cs:22-30`).
Приёмка H1: 250 боксов (240 вне FBX / 10 внутри), 9 дверей, 0 missing, позы ±1e-4 (`:62-70`).

### 2.2 P1 — greeble-мерж (6 префиксов)

| Ячейка | Префиксы | Узлов | Мешей | Σtris до → после |
|---|---|---:|---:|---:|
| `GrbFine` | `Bolt_`, `Mess_`, `UnderHull_` | 219 | 3 | 6 708 → 6 708 |
| `GrbDetail` | `Interior_`, `Contour_`, `Det_` | 159 | 7 | 9 284 → 9 284 |
| **итого** | | **378** | **10** | **15 992 → 15 992 (0.0 %)** |

Группировка `(ячейка, ассет материала, настройки рендерера)`, `mergeSubMeshes=false`, `UInt16` (≤10 556 вершин).
С узлов FBX сняты **только** `MeshRenderer`+`MeshFilter`; трансформы/коллайдеры целы. Меши:
`Assets/_Project/Generated/Ships/HeavyII_VIS_*.asset` — **21 шт** (10 P1 + 10 LOD1 + 1 LOD2), в git.
**Вне мержа — 547 статик-рендереров** = 935 − 378 − 10; разрез (`D/I_ships_compartments.csv:2-20`):
`Int_*` 83 меша/6 864 tris, `T1_/T2_` 184/3 512, `Engine_/Eng_` 97/3 664, `CB_*` 12/1 800, `Deck_*` 26/1 232,
`Stair/Ladder/Well` 16/1 044, `Bridge_*` 28/752, `Lgt/Keel/Shipyard` 18/560, `Hull_*` 18/384, `Win_*` 15/288,
`Sill_*` 15/180, `Door_*` 18/216. Причина: P1 по брифу — только greeble; интерьеры/палубы зарезервированы под P3.
**Источники:** `ShipVisualMerger.cs:16-18,77-78`, `D/M_stage3_heavy_2026-10-31.csv:14-42,83`.

### 2.3 P2 — LODGroup (значения из префаба)

| Параметр | Значение | Источник |
|---|---|---|
| Хост / `size` / `localReferencePoint` | `SHIP_VISUAL_H2` / **27.070501** / (1.140, 22.130, 0.136) | prefab:2111,2143-2144 |
| `screenRelativeTransitionHeight` | **0.9377497 / 0.3125832 / 0** | prefab:2149+ |
| Пороги (FOV 60, lodBias 1) / max lossyScale | **300 / 900 м**, LOD2 без cull / **12.000** → worldSize **324.846** | `ShipVisualMerger.cs:81-83`; `D/N_…csv:2` |
| Рендереров по уровням | **557 / 10 / 1 = 568**; слотов с 47 вне LODGroup — **604 / 57 / 52**; LOD2 = 5 сабмешей | prefab, `D/M_…csv:92-94` |
| LOD1 | 10 мешей по материалам (без `GrbFine`), **30 164 tris**, 706 источников | `D/M_…csv:31-41` |
| LOD2 | **1 меш, 5 сабмешей, 30 164 tris**, 53 666 вершин; Steel_Dark / Bronze_Dark / Glass / Lamp_Red / Lamp_Green | `D/M_…csv:41`, `ShipVisualMerger.cs:89-104` |
| Вне LODGroup | **47 слотов**: 9 дверей + `Door_AftGate_Stb` + `Ramp_Cargo`, `PilotSeat`, 2× `Slot_Engine`, `CargoVisual`, `ContrailVFX`, слоты, корневой dummy | `R3:17-18`, `D/N_…csv:4` |

**Потолок:** `d = worldSize / (2·tan(fov/2)·h)`, `h ≤ 1` ⇒ LOD0→LOD1 ближе **0.866 × worldSize = 281.3 м** задать нельзя
(`R3C:20-33`; `Retune Heavy LOD Thresholds` упал на 281.6065 ≠ 900.0001). На 200 м корабль **всегда** LOD0.

### 2.4 До / после (edit-mode) и бюджет теней

| Состояние | MR | слоты | DC upper (1 + 2 каскада) |
|---|---:|---:|---:|
| до H1/H2 | 973 | 972 | **2 916** |
| после LOD0 (≤300 м) | 616 | **604** | **1 812** · LOD1: 57 / 171 · LOD2: 52 / 156 |

`D/M_…csv:43-44,91-94`; `R3:26-33`. Σtris не изменились. Валидатор H2: 3 уровня, 300 ±3 м / 900 ±9 м,
`LOD2 height == 0`, все статики FBX в LOD0, Σtris greeble = FBX, `UseShipBounds == false`, 9/250/11/1/1/2/2/1
(`ShipVisualMerger.cs:798-883`).
**`ShipShadowBudget`:** `on=140`, `off=150`, `smallPartMaxAxis=8`, `coverWholeLodGroup=true`, тик 0.33 с
(`ShipShadowBudget.cs:40-60`). Режим «только мелочь» — **314 кандидатов/314 слота**, 0 `Off` в префабе (`R3C:86-95`);
`coverWholeLodGroup` покрывает **568 рендереров / 572 слота** и намеренно **не** фильтрует `renderer.enabled`
(`R3C:357-368`). Только Play Mode, `RestoreShadows()` на `OnDisable`, `Camera.main` не используется, мировых кэшей нет
(FO-safe), `SetActive`/`enabled`/коллайдеры/`DoorController` не трогает, `budgetEnabled=false` — откат (`R3C:64-83,381-382`).
Владелец `shadowCastingMode` корабля — `ShipShadowBudget` (`T-CULL01_STAGE6_REPORT.md:20`).

### 2.5 Runtime (Host, Midday, far 30 000, FOV 60, `lodBias 1.30`, shadow 400/4/soft)

| Точка | Stage 3a | Stage 3c (21:50) | Гейт ≤1300 |
|---|---:|---:|---|
| 80 м (LOD0, срез фрустумом) | 1 015 | **971** | — |
| **200 м (весь корабль, LOD0)** | **1 538** | **1 170** | **PASS** (+130, 10.0 %) |
| 200 м повтор | 1 538 | **1 161** | PASS (+139; Δ 0.77 %) |
| **300 м (худшая поза, LOD0)** | 1 611 | **1236** | **PASS** (+64, 4.9 %) |
| 400 м (LOD1) | 235 | **122** | — |
| 1200 м (LOD2) | 104 | **104** | — |

`D/M_stage3_heavy_runtime_2026-10-31.csv:18-23`, `D/N_stage3c_heavy_2026-10-31.csv:47-56`, `R3C:391-407`.
Кастеров корабля: 225 (200 м) / **44** (300 м) — остаётся 47 слотов интерактива вне LODGroup (`D/N_…csv:55`).
Повторы: 0.49–0.77 % при допуске ±5 %. На 200 м ~64 % DC — теневой проход (983 из 1538; 1.658 draw/кастер, `R3C:37-47`).
`ship_tris` в A−B **непригоден** (−477 396 / −37 616 / −26 498) — выводить только `ship_dc` (`D/N_…csv:28,56`).

## 3. Инструменты (Editor)

| Инструмент | Файл / меню | Что делает / когда | Инварианты | Риски |
|---|---|---|---|---|
| **Repair Heavy Prefab** | `Editor/Ships/ShipPrefabRepair.cs` · `ProjectC/Ships/Repair Heavy Prefab (dry-run / apply)`, `…Verify Heavy Prefab (FBX reimport)`, `…Rollback (from backup)` | Выносит 250 боксов в `SHIP_COLLIDERS` и 9 дверей в дети prefab-корня; **визуал не клеит**. После **каждого** reimport FBX, перед мержем | 250/9/973 MR/973 MF/1 452 Transform/0 missing, позы ±1e-4 (`:62-70`). При `SHIP_VISUAL_H2` абсолютная проверка 973 MR заменяется инвариантами H2 (`R3:21`) | Абсолютные ожидания ломаются на любом другом корабле; жёстко зашитый `PrefabPath` |
| **Visual Merge** | `Editor/Ships/ShipVisualMerger.cs` · `ProjectC/Ships/Heavy Visual Merge (dry-run / apply P1+P2 / Verify / Rollback)` | P1: 6 greeble-префиксов → 10 мешей; P2: `LODGroup` 300/900 + LOD1/LOD2. Один раз на класс, до бюджета теней | `ValidateMergedState` + `ValidateInteractive` (§2.4); Σtris склейки = Σtris FBX | Зашит на heavy (PrefabPath/FbxPath/MeshPrefix/отчёт/меню) — параметризации нет (`BR3B:59-66`) |
| **Shadow Budget** | `Editor/Ships/ShipShadowBudgetTool.cs` + `Scripts/Ship/ShipShadowBudget.cs` · `ProjectC/Ships/Shadow Budget/Apply to Ship_havyII_base (Stage 3c) / Verify / Rollback` | Вешает/проверяет/снимает runtime-компонент; Verify = параметры + инвентарь по габариту + аудит `shadowCastingMode` + инварианты H2 + проекция ΔDC. После мержа и LOD, если корабль мешает в гейте | 9 / 250 / 11 / **616** MR / LOD 300/900 / `Off` в префабе = 0 / `GateDc = 1300` (`:59-73`). Гейт **не подтверждает** — только prefab-side | Зашит на heavy; повторный `Apply` запрещён (нужен Rollback); префаб в `.gitignore` → на чистом клоне меню обязательно |
| **Ship Summary** | `Editor/ShipSummaryWindow.cs` · `Tools/Project C/Ship Summary` | Таблица префабов `Prefabs/Ships` — **13 колонок**: Name, Class, Thrust, MaxSpd, Yaw, Pitch, Vert, Mass×, FuelMax, Fuel/s, HP, Power, WindExp; batch-правка; detail-панели 6 подсистем. Пред созданием/правкой парка | Скрывает `DONT-DELETE*`, отдельно `Ship_Light_root` (`:25-28`), сортировка по всем колонкам | **Геометрии и DC не показывает вообще**; cargo-лимитов нет → не приёмочный инструмент |
| **Create Ship Preset** | `Editor/ShipPresetCreator.cs` · `Tools/Project C/Create Ship Preset` | Генерит заглушку: Rigidbody 1000 кг, `ShipController`, 9 `ModuleSlot` + `Slot_Engine` + `ShipCargoVisual` + `PilotSeatController` + `DoorController`, key-item/damage/schedule, NavMesh-бейк палубы, **6 кубов-примитивов** | `visualScale` по классам: Light (6,1,12) … HeavyII (13.3,1,22) (`:137-176`) | Пресеты расходятся с живыми префабами (§8.5/8.6); 6 кубов ≠ корабельный контент |

## 4. ПРАВИЛА: что мерджим / что не мерджим

| Категория | Объектов / tris | Вердикт | Причина |
|---|---:|---|---|
| `Bolt_*` (крепёж, болты) | 90 / 1 800 | **MERGE** → `GrbFine`, нет в LOD1+ | медиана 18 tris/меш — чистый greeble |
| `Mess_*` (камбузная утварь) | 8 / 280 | **MERGE** → `GrbFine` | мелочь <100 tris, статична |
| `UnderHull_*` (трубы/хомуты под днищем) | 121 / 4 628 | **MERGE** → `GrbFine`; первый в LOD1 | с земли/сверху не видно |
| `Interior_*` (потолочные лампы/панели) | 58 / 1 624 | **MERGE** → `GrbDetail` | 52 меша с `Lamp_White` — один бакет |
| `Contour_*` (обводы, рёбра, нав-огни) | 80 / 1 668 | **MERGE** → `GrbDetail` | статично; нав-огни на `Lamp_*` |
| `Det_*` (разметка, рым-болты, кнехты) | 21 / 5 992 | **MERGE** → `GrbDetail` | «никогда не двигается» |
| `Int_*` (каюты, камбуз, медблок, мостик, трюм) | 83 / 6 864 (18.5 %) | **NO-MERGE сейчас**; позже «по комнате» | нужна по-комнатная видимость + задел под разрушаемость |
| `T1_/T2_` (переборки, полы, потолки) | 184 / 3 512 (9.5 %) | **NO-MERGE сейчас**; позже «по палубе» | лучший разрез и для LOD, и для разрушаемости |
| `Engine_/Eng_*` (трубы, консоли, щиты) | 97 / 3 664 (9.9 %) | **NO-MERGE сейчас** | сами двигатели — отдельные nested FBX |
| `CB_*`, `Deck_*`, `Stair/Ladder/Well`, `Bridge_*` | 12+26+16+28 / 1 800+1 232+1 044+752 | MERGE (не сделано) | статичный такелаж/палуба/трапы/мостик |
| `Hull_*`, `Win_*`, `Sill_*`, `Lgt_/Keel_/Shipyard_` | 18+15+15+18 / 384+288+180+560 | MERGE (не сделано), Win — в glass-бакет | мало tris, но это лишние рендереры |
| **`Door_*` / `Ramp_*` / `AftGate_*` / створки** | 9 дверей + 2 створки | **NEVER** | на узле `DoorController` + `Collider`; `[RequireComponent(Collider)]`, `AddComponent<DoorController>()` без коллайдера → `null`; дубли дали бы 259 боксов (`ShipPrefabRepair.cs:22-30`) |
| **Коллайдеры (250 Box + 10 leaf)** | 250 | **NEVER** | мерж геометрии не должен менять физику; leaf-боксы створок обязаны ехать с узлом |
| **`ModuleSlot`(11) / `PilotSeat` / `CargoVisual` / `EngineThrusterVisual`+`ShipPartShake`(2) / `ContrailVFX`** | 47 слотов | **NEVER**, вне LODGroup | динамика/интерактив/сеть; `ShipPartShake` двигает трансформы |
| **VFX (`VisualEffect`, `VFXRenderer`, след)** | 1+1 | **NEVER** | `ShipContrailVfx` берёт размер наибольшего MR: после мержа закреплены `UseShipBounds=false` + `ManualBoundsSize=(72.216, 216.360, 9.720)` (`R3:19-20`, prefab:11446-11447) |
| **Лампы (`Lamp_*`)** | в greeble | MERGE по материалу; `Lamp_Red`/`Lamp_Green` **остаются в LOD2** | навигационные огни читаемы на дистанции; `Lamp_White` уходит в стекло (`ShipVisualMerger.cs:97-104`) |

## 5. Рекомендации по классам (метрических классов в проекте НЕТ, §8.4)

Классы в коде только геймплейные: `ShipFlightClass {Light, Medium, Heavy, HeavyII}` (`ShipController.cs:22-28`),
`ShipClass {Light, Medium, HeavyI, HeavyII}` для груза (`Trade/Scripts/Core/ShipClass.cs:21-27`). Полосы ниже — предложение [E].

| Полоса | Длина | Пример | Шаги | Бюджет [E] | LODGroup | ShadowBudget | Двери/коллайдеры [E] |
|---|---|---|---:|---|---|---|---|
| **S** | **< 30 м** | light 18.5 м | H1-аналог → мерж по материалу (1–3 меша) → 1 уровень | ≤10 слотов → **≤30 DC** | не нужен | не нужен | 0–1 / 5–15 |
| **M** | **30–80 м** | medium 73.1 м | H1-аналог → мерж → LODGroup 2–3 ур. (пороги от габарита!) | ≤25 слотов → **≤75 DC** | да | если в кадре 150–300 м | 1–2 / 15–40 |
| **L** | **80–200 м** | NPC 80–128 м | то же + LOD1 без greeble, LOD2 = 1 меш × ≤5 материалов | ≤60 слотов → **≤180 DC** | да, 3 ур. | да, если попадает в гейт | 2–5 / 40–120 |
| **XL** | **> 200 м** | heavy 324.9 м | H1 → P1 (6 префиксов) → P2 (300/900, LOD2 без cull) → `ShipShadowBudget(140/150, coverWholeLodGroup)` | **604 слота / 1 170 DC** [F] при гейте ≤1300 | да, но LOD0 < 281 м недостижим | **обязателен** | 9 / 250 [F] |

**Счёт бюджета до замера:** в LOD0 `DC ≈ слоты × 3` — совпало на 85 % (1 538 факт vs 1 812 модели); **в LOD1/LOD2 неверно**
(137 % и 67 %) — `PM:102-104`. Слоты после мержа ≈ **1.08 × MR** (604 на 557), до мержа ≈ 1:1.
**Пороги:** `LODUtility.CalculateDistance(worldSize, h, fov)` при `h ≤ 0.999`, FOV 60; `d = worldSize / (1.1547 × h)`,
нижняя граница `0.866 × worldSize` (`R3C:20-33`). **Гейты:** P1 heavy ≤1800 — PASS (1538); P2 ≤1300 на 200 **и** 300 м —
PASS (1170/1236); «все корабли ≤480» — модель после **P3** (8 ячеек × материал), P3 не реализован (`EP:75`).

## 6. Что НЕ делать (антипаттерны из отчётов)

1. **Абсолютные пороги MR**: `ExpectedMeshRenderers = 973` (`ShipPrefabRepair.cs:64`) уже потребовало ветки
   «если есть `SHIP_VISUAL_H2`» (`R3:21`) — любой новый мерж ломает проверку без своего инварианта.
2. **Reimport FBX без Verify**: 250 боксов/9 дверей жили user-override'ами на узлах модели, переименование узла их сносит
   (`ShipPrefabRepair.cs:14-21`).
3. **`ShipPositions.json`**: сохранённая позиция падения телепортировала корабль на −41 570 м (`py=-42311.10`,
   `groundDist=-1`), каждая сессия перезаписывала её снова (`PM:30-52`). Правит только пользователь.
4. **`ShipContrailVfx.UseShipBounds=true` после мержа** — след прилипает к новому крупному bounds.
5. **Мерж дверей/створок/коллайдеров** — `DoorController` требует `Collider`; створка едет с узлом.
6. **Правка medium/light/NPC «на всякий случай»**: Stage 3b отменён — «они типовые; если мешают в кадре —
   выключать `MeshRenderer` runtime-ом, префабы не трогать» (`BR3B:3-6`, `HO:139-143`).
7. **Edit-mode мутация `shadowCastingMode`**: дефект Stage 2 оставил 5 346 `m_CastShadows: 0`; бюджет обязан иметь
   `Application.isPlaying`-guard + `RestoreShadows()` (`R3C:78-80`).
8. **Верить модели `слоты × 3` вне LOD0** и **метрике `ship_tris` A−B** (отрицательные значения — мусор).
9. **`Camera.main`** в мультикамерных бюджетах (правильно: `CameraType.Game`, без `targetTexture`, `far ≥ 50`).
10. **«Просто уменьшить порог LOD»** — упирается в `screenRelativeTransitionHeight ≤ 1.0` (281.6 м), ретайн удалён (`R3C:31-33`).
11. **Бюджет теней без визуального sign-off** — дальше 150 м у корабля пропадает тень статики (на 300 м остаётся 47 слотов);
    приёмка **PENDING** (`R3C:438-441`).

## 7. Чек-лист приёмки нового корабля

**Edit mode (без Play Mode, сцена не сохраняется).** 1) `Repair … (dry-run)` → `(apply)`: боксы и двери вынесены, 0 missing,
позы ±1e-4. 2) `Repair … (Verify, FBX reimport)` после **принудительного** reimport → 250/9/0 missing.
3) `Heavy Visual Merge (dry-run)`: сколько greeble-узлов и мешей, Σtris источника = Σtris склейки.
4) `(apply P1+P2)` → `Verify`: 3 уровня, пороги ±3/±9 м, `LOD2 height == 0`, все статики в LOD0, `UseShipBounds == false`,
интерактив 9/250/11/1/1/2/2/1, 0 missing script. 5) `Shadow Budget → Apply → Verify`: параметры, инвентарь, `Off` в префабе = **0**,
расхождений `shadowCastingMode` с FBX = **0**, инварианты H2 целы. 6) Дифы: только префаб + новые `.asset`;
**mtime других кораблей не изменился**; сцена `isDirty == false`.

**Runtime (Play Mode, разрешение пользователя; бенч сам останавливает игру).** 7) `Project C → Culling → Stage 3 Ship Bench → Run`:
точки 80/120/200/200-repeat/300/400/1200 м, `Medium`, `lodBias 1.30`, shadow 400/4/soft. 8) Гейты: 200 м **≤1300**, 300 м **≤1300**,
повтор **±5 %**; зафиксировать `slots`, `shadowCasters`, `shadowVisible`. 9) Целостность: 9 `DoorController` (open →
`progress=1.00`, `localMove_m≈0.15`; close → `0.00`), 250 `BoxCollider`, 11 `ModuleSlot`, `PilotSeat`, `CargoVisual`,
`ContrailVFX`, 2 `EngineThrusterVisual`. 10) **F8 → F9** реальным вводом: объект на месте, 0 errors, `transactionsSeen=True`
(сейчас долг: `False`). 11) Глазами: переходы LOD 80/200/400/1200, пропадание тени статики на 140/150 м, ночной режим.

**ShipSummaryWindow (`Tools/Project C/Ship Summary`)** — смотреть строку корабля (Class, Mass×, HP, Power, WindExp) и batch-правку.
**Чего там нет:** MR, слотов, DC, коллайдеров, дверей, LOD — для геометрии только `… → Verify` и CSV-отчёты.

## 8. Расхождения / риски

1. **`coverWholeLodGroup` не сериализован в префабе** — 0 вхождений в `Ship_havyII_base.prefab` (grep 2026-11-01), поле живёт
   на дефолте C# (`= true`, `ShipShadowBudget.cs:49`). «Откат одним флагом» (`R3C:381-382`) = правка в инспекторе, не значение в ассете.
2. **`I_ships_*` / `C_ships.csv` — до-H2**: heavy 973 MR, сцена 1 639 MR / 4 917 DC. Актуально **616 MR** и **1 282 MR**.
   Любой документ, цитирующий 973 как «сейчас», устарел.
3. **Метка «NPC light (20 шт.)» в `I_ships_prefabs.csv:7-26` неверна**: классы 3 Light / 6 Medium / 7 Heavy / 4 HeavyII
   (плюс базы: 4/8/7/5). Длина заглушек 8–273 м **пересекается** между классами: Light 18–48, Medium 28.8–73.1,
   Heavy 49.3–127.8, HeavyII 133–324.9 — имена («Гигант» 273 м, «Мастодонт» 133 м) ничего не значат.
4. **Метрических классов размеров нет** — только геймплейные enum'ы; полосы S/M/L/XL из §5 это предложение [E].
5. **`massMultiplier` расходится**: префаб heavy = **15** (2000×15 = 30 000, `PM:34`), пресет HeavyII `ShipPresetCreator.cs:171` = **25**;
   medium: 15 в префабе против 10 в пресете.
6. **Три источника грузовых лимитов**: `ShipClass.cs:23-26` и `ShipClassLimits` (`CargoData.cs:295-312`) — Light 4/100/3 …
   HeavyII 30/5000/80; `ShipPresetCreator` (`cargoSlots` 20/2 000/100 000/**10 000 000**, вес 400/3 000/28 657/121 393),
   которые пишутся в `ShipController.baseMaxCargoSlots`. Разброс в 6 порядков.
7. **Дрейф «slot»:** в `I_ships_*` slot ≈ MR (973/428/118); в `M_/N_` slot = Σ`sharedMaterials` (604 на 557 рендереров);
   `02_FACTS_AND_MEASUREMENTS.md:229-230` даёт light 429 / medium 123. Сравнивать «слоты» между документами нельзя.
8. **Цель по DC не одна:** `EP:75` «все 23 ≤480» (модель после P3), `BR3B:45-46` «все ≤2500 (P1/P2), после P3 — ≤480».
   P3 не реализован; фактически один тяжёлый даёт 1 170 DC.
9. **Статус P2 противоречив по времени:** `R3:34` и `R3C:174-175,234-235` — FAIL (1538/1434/1361), PASS только в `R3C:391-407`
   (прогон 21:50) и `D/N_…csv:49-54`. Ссылаться нужно на 21:50.
10. **H1-арифметика разная:** «238 из 250 внутри FBX» (`HO:71`) vs «240 вне FBX / 10 leaf» (`D/M_…csv:101`); итог 250 сходится.
11. **Долг F8/F9 не закрыт** (`transactionsSeen=False`, `worldRootShift=0.00` на success и rollback) — новый корабль получает
    тот же непроверенный ребейз (`R3C:440-441`).
12. **Визуальный sign-off бюджета отсутствует** — гейт закрыт по числу, но не принят глазами; это блокер раскатки (`BR3B:24-32`).
13. **Чего не хватает:** нет runtime-замера medium/light/NPC при конфиге 400/4/soft; нет ни одного замера DC после мержа для
    корабля < 200 м; нет метрики треугольников на инстансе (A−B мусорен); HLOD в проекте только для города
    (`T-CULL01_STAGE7_REPORT.md:5,41-43`); нет данных о стоимости анимации дверей в кадре; нет решения по `ShipPositions.json`.
