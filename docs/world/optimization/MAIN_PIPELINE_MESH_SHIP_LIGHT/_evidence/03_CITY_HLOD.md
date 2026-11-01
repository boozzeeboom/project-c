# 03 — ГОРОД / HLOD / VisibilityDirector / сцены: факты и числа (T-CULL01 H6–H8)

**Собрано:** 2026-11-01, только чтение (Play Mode/бенчи не запускались). Unity 6000.5.2f1, URP 17.5.0, `C:\Projects\ProjectC_client`. Живые данные сцены — 3 read-only вызова MCP (`manage_scene get_hierarchy`).
**Сокращения:** `D/` = `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/`; `R6/R7/R8` = `docs/dev/T-CULL01_STAGE6/7/8_REPORT.md`; `HO` = `docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md`; `EP` = `10_CULLING_RESEARCH/10_EXECUTION_PLAN.md`; `BIT` = `09_BEAM_COMPARISON_AND_DECISIONS.md`; `BLK` = `docs/world/primum_city/01_SETUP/02_BLOCKS_AND_GEOMETRY.md`; `FO9S` = `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md`.
**Метки:** **[F]** факт из файла/живой сцены · **[M]** выведено из кода/ассета · **[E]** оценка/экстраполяция · **[U]** не подтверждено. Дополняет `06_CONTENT_PIPELINE.md` (Blender/FBX), не заменяет её.

## 0. Сводка (принятая базовая линия)

| Метрика | Значение | Источник |
|---|---|---|
| L0 (площадь) | **2 229–2 235 DC**, 174 SetPass, 11.79 M тр. Без политики дверей — 2 341 DC / 12.33 M тр. | R8 §2.1, EP §9.2 |
| L0 «исторические» 2 362–2 378 | включали камеру нового главного меню (+37 DC) — **не сравнимы** с текущими | R7 §2.1 |
| L1 (3–12 км) / L2 (12+ км) | **46–48 DC** (гейт ≤500) / **2 DC** (гейт ≤10) | R7 §5.2 |
| Двери | 2 630 `DoorController` / **5 493** рендерера слоя 10 (5 223 блочных + 270 ферм); >150 м в кадре — **0** (контроль 2 633) | R8 §2.3, §4 |
| Директор | **1 000** групп, **1 147** направляемых рендереров, union = 1 камера; тик 0.90 мс (H7 0.48 / H6 0.18), пик 4.07 мс на телепорте | R8 §2, §5 |
| HLOD-ассеты | 23 L1-меша + импостор + 2 текстуры + 2 материала + манифест = **29 файлов, 50.65 МБ** | замер папки |
| Источник L1 | 859 рендереров, **5 407 574** тр. → **266 794** тр. (−95.1 %), 571 905 верт. | манифест [F] |

## 1. Иерархия мира и сцены

### 1.1 Сетка
| Параметр | Значение | Источник |
|---|---|---|
| `BootstrapScene` (build 0) | `NetworkManager`, `NetworkPlayerSpawner`, `ClientSceneLoader`, `ScenePlacedObjectSpawner` | AGENTS.md |
| Стриминговые сцены | **24** = `WorldScene_{0..5}_{0..3}`, сетка **6 × 4** | `Assets/_Project/Data/Scene/SceneRegistry.asset:15-17` |
| `SCENE_SIZE` / overlap | **79 999 м** / 1 600 м → мир **479 994 × 319 996 м** [M] | `Scripts/World/Scene/SceneID.cs:17-19` |
| Стриминг | preload **10 000 м**, unload **10 000 м**, `maxLoadedScenes` **4** | `Scripts/World/Scene/ClientSceneLoader.cs:66-70` |
| Заполнена | только `WorldScene_0_0` — **60 МБ**, 1 538 452 строки; остальные 23 — 37–46 КБ | замер файлов |
| Город в сцене | блоки на **39.3–40.0 км** по X/Z от нуля сцены (40 км внутри одной сцены) | манифест `pivot` [F] |

### 1.2 Живой состав `WorldScene_0_0` (MCP 2026-11-01): корней 61, у `WorldRoot_0_0` **22 ребёнка** [F]
| Ребёнок `WorldRoot_0_0` | Детей | Что | Слой | В HLOD |
|---|---:|---|---:|---|
| `PRIMUM` | **26** | город: **23 блока** + `ADMIN_FOR_COLLIDER_COLLIDER` (484), `ADMIN_DOOR_PREFABS` (133), `ADMIN_ENTRANCE_PORTAL` (с `DoorController`) | 8 | **да, 23** |
| `PRIMUM_DOORS` / `_LIGHTS` / `_COLLIDERS` | 21 / 21 / 27 | двери (2 597 + 133 ADMIN) / 2 238 `Light` (`PrimumLightPool`) / 1 376 объектов коллайдеров | 8,10 / 8 / 8 | нет |
| `Primum_farms` | 6 | фермы **142 MR / 150 слотов / 1.27 M тр.**, 963–1 051 м от города | — | **нет** |
| `Primum_farms_DOORS` | 2 | двери ферм (270 MR), `block = -1` | 10 | нет |
| `RuinsValleys` | **496** | руины (`DetailDistanceCuller` ведёт `enabled`) | — | нет |
| `Secund` / `Tertius` / `Quartus` | по 3 | спутники — **жёсткий контракт имени** директора | — | нет |
| `Terrain_0_0` | 0 | `Terrain` + `TerrainCollider`, **единственный `isStatic`** среди корней/детей | 0 | нет |
| `ParomRoute_01` + `Parom_A_Start/B1/B2/C_End` | 3 | паром (`NetworkObject` + `GlobalSceneSourceMarker`), якоря — сиблинги | — | нет |
| `Airways`, `WindZones`, `CompassRose_North`, `SceneLabel_0_0`, `Cube`, `Primum` (нижний регистр, **неактивный легаси** — не путать с `PRIMUM`) | 0–2 | служебное/тест/разметка | — | нет |

Вне `WorldRoot_0_0`: 23 корабля + 20 NPC-кораблей (свои корни сцены, `ShipRootReference` + `NetworkObject`), пикапы/сундуки/станции/NPC-спавнеры (`NetworkObject` + `GlobalSceneSourceMarker`), `probe-*`.

### 1.3 Floating Origin: что едет бесплатно, что требует регистрации
| Ветка | Правило | Источник |
|---|---|---|
| 🟢 Контент под корнем `WorldScene_*` | `_includeAllWorldSceneRoots = true`: **каждый активный корень** сцены — участник (`CityStatic`), сдвиг `Target.position += translation` | `GlobalMotionControlledRebaseSlice.cs:24, 1133-1156, 1367` |
| 🟢 Дети участника (в т.ч. созданные позже) | едут с корнем: HLOD-корень создаётся в рантайме под `WorldRoot_0_0`, F8 → `worldDelta=0.000` | R7 §6 |
| 🔴 Неактивный корень сцены | **исключается** из участников (`T-FO06CZ`) → включённый позже останется в старых координатах | там же `:1142-1155` |
| 🟡 Кэш мировых `Vector3` в скрипте | `ApplyRebaseTranslation` в shift-блоке (success **и** rollback) | FO9S §3.1 |
| 🔴 Рантайм-спавн вне корней сцен | регистрация участником или parent под участника | FO9S §3.1 |
| 🔴 Любой `NetworkObject` в сцене | `GlobalSceneSourceMarker` + запись в `GlobalMotionPilotSceneCatalog` + `layoutHash`/`dependencyHash` + digest, иначе Host: `uncontrolled_network_source_before_native_sweep` | FO9S §3.2 |
| 💾 Сейвы координат / 🚫 world-space bake (Umbra/PVS), статик-батчинг города | кумулятив при загрузке / запрещены — FO ломает | FO9S §3.1, BIT §7.10 |

Проверка: F8 → `frameGen` +1, сдвиг = предсказанный, объект на месте, 0 errors; F9 → `PASS_rollback_restore`. Тулза каталога: `ProjectC/World/Floating Origin/Bake Pilot Catalog (Preview + Apply)` (`Editor/FloatingOrigin/BakePilotSceneCatalog.cs:25`).

### 1.4 Как добавить контент (короткий путь)
1. Классифицируй: есть/появится `NetworkObject` в сабтри? Нет → шаг 2; да → шаги 2–4 + регистрация (FO9S §3.2).
2. Положи **под корень своей `WorldScene_*`** (город — `WorldRoot_0_0/PRIMUM/<блок>` или `Primum_farms`), активным, слой 8, **без `static`**.
3. Геометрия: мировые координаты, корень блока в (0,0,0), масштаб инстанса 1, join «блок × материал» (`BLK:39-40`).
4. Сетевой контент: физика по `ownership` → `Bake Pilot Catalog` (маркер + observation + entry + хэши + digest).
5. Контент в `PRIMUM` → **перепечь HLOD** (§3.5), иначе он живёт только в L0. Проверки: 0 errors, `AuditGlobalSceneCatalog`, F8/F9, DC по §0.

## 2. `VisibilityDirector` — единственный владелец видимости

`Assets/_Project/Scripts/World/VisibilityDirector.cs` (1 347 строк). Хостинг: авто-бутстрап на скрытом `ViewDistanceRuntime` (`AutoBootstrap = true`, `:136`), **только Play Mode**; `OnDisable` снимает все `forceRenderingOff`.

### 2.1 Политики (все — одно свойство `forceRenderingOff`, тик 3 Гц = 0.33 с; ночь `_PrimumHlodNight` 0..1 по солнцу — тоже здесь)
| Политика | Порог | Гистерезис | Что гасит/включает |
|---|---|---|---|
| Под-пиксельный culling | диаметр < **2 px** во **всех** камерах union | 12 % (обратно на 2.24 px) | направляемые рендереры домов/ферм/спутников |
| HLOD L0→L1 | **3 000 м** до AABB блока | 10 % | L0-рендереры блока + L1-рендерер |
| HLOD L1→L2 | **12 000 м** до AABB города | 10 % | все блоки + импостор |
| Двери L0 под GRD | **150 м** (обратно 135 м) | 10 % | дверные рендереры слоя 10 |

Камеры union: тип `Game`, активна, без RenderTexture, far ≥ 50, рисует слой `worldCameraLayer` (= **10**, слой дверей — он же маркер мира), «дотягивается» до якоря чанка. Замер: в union **1** камера (`ThirdPersonCamera_0`); `AtmosphereCamera` — `mask_no_world`, парковочная `MainCamera` (BootstrapScene) — `out_of_reach` (R6 §4, R8 §2).

### 2.2 Как контент попадает: **только авто-обнаружение по точному имени/иерархии**, API регистрации нет (`Build():433`, `:453-466`)
| Что добавлено | Как распознаётся | Класс | Ведёт директор? |
|---|---|---|---|
| Ребёнок `PRIMUM` → его дети | чанк (район) → дом | `Chunk`→`House` | **да** (`directCityHouses`) |
| Ребёнок `Primum_farms` | ферма | `Farm` | **да** (`directFarms`) |
| Корень `Secund`/`Tertius`/`Quartus` | спутник | `Satellite` | **да** (`directSatellites`) |
| Корень `*_DOORS` / `*_LIGHTS` | двери / лампы | `Doors` / `Lamps` | наблюдает (`DoorVisibilityBudget` / `PrimumLightPool`) |
| `RuinsValleys` | руины | `Ruins` | наблюдает (`DetailDistanceCuller`) |
| **любой другой корень** | `Misc` | `Misc` | **нет** — не гасится ни политикой, ни HLOD |
| Корабль (`ShipRootReference`) | `Ship` + `Interior` (`Int_*`) | `Ship`/`Interior` | наблюдает (`ShipShadowBudget`) |

Участник политики (`IsDirectable:581`): **только `MeshRenderer`**, не слой 10, не под `DoorController`, не уже погашенный. Следствие: `SkinnedMeshRenderer`/`ParticleSystemRenderer`/`LineRenderer`/`Terrain` не трогаются.

### 2.3 API и поля
`WorldCameras`, `Groups`, `GroupCount`, `DirectedMemberCount`, `CulledCount`, `CameraCount`, `PolicyEnabled`, `MinScreenPixels`, `LastTickMs/MaxTickMs/AvgTickMs`, `TickCount`, `RebaseResets/RollbackResets/TeleportResets`, `CameraReport`, `ProviderReport`, `OwnershipConflicts`, `TryGetNearestCameraDistance`, `DescribeGroups`;
HLOD: `HlodEnabled`, `HlodBlockCount`, `HlodImpostorPresent`, `CityLevel`, `HlodHiddenCount`, `HlodDoorCount`, `HlodDoorsHidden`, `L1Distance`, `L2Distance`, `LodHysteresis`, `HlodLevelCounts`, `HlodReport`, `NightFactor`, `HlodRoot`;
двери: `DoorCullEnabled`, `DoorCullDistance`, `DoorCullTotal` (5 493), `DoorCullBlockBound` (5 223), `DoorCullHiddenByDistance`; константы `HlodRootName = "PRIMUM_HLOD"`, `NightGlobalName = "_PrimumHlodNight"` (`:250-353`).
Инспектор (`:79-133`): `policyEnabled`, `minScreenPixels 2`, `hysteresis 0.12`, `directCityHouses/directFarms/directSatellites`, `updateInterval 0.33`,
`cameraRescanInterval 1`, `rebuildInterval 2`, `hlodEnabled`, `hlodL1Distance 0` (0 = из манифеста), `hlodL2Distance 0`, `hlodHysteresis -1`,
`doorCullEnabled`, `doorCullDistance 150`, `doorCullHysteresis 0.1`, `worldCameraLayer 10`, `teleportThreshold 50`, `logOnBuild`.

### 2.4 Ownership map и что нельзя ломать
| Свойство | Владелец | Замечание |
|---|---|---|
| `forceRenderingOff` | **только `VisibilityDirector`** | grep по `Assets/_Project`: других писателей нет (бейкер/пробы читают) |
| `shadowCastingMode` | `CityShadowBudget` (город, 200/180 м, 1 301 entry), `ShadowBudget` (фермы 60/50), `DoorVisibilityBudget` (двери 40–80), `ShipShadowBudget` | директор **не пишет** |
| `Renderer.enabled` | `DetailDistanceCuller` (руины) | директор **не пишет** (H8: `enabled=false` — 0 раз) |
| `layerCullDistances[10]`, `camera.far`, `lodBias`, `Light.enabled` | `DoorVisibilityBudget`, `ViewDistanceApplier`, `PrimumLightPool` | не трогать |
| `DoorController`, коллайдеры, `SetActive` | никто из куллинга | H8: open/close 0→1→0 вживую |

Правила: (1) не писать `forceRenderingOff` из нового кода; (2) не переименовывать `PRIMUM`, `Primum_farms`, `Secund/Tertius/Quartus`,
`WorldRoot_0_0`, `PRIMUM_DOORS`, `*_DOORS`, `*_LIGHTS`, `PRIMUM_HLOD`; (3) не гасить контент через `SetActive` (директор не восстановит,
а неактивные корни выпадают из FO); (4) не ставить `forceRenderingOff` руками до старта — рендерер не попадёт в участники (`:586`);
(5) не добавлять второй HLOD-корень и scene-оверрайды.

## 3. HLOD L1 (3–12 км)

### 3.1 Единица = блок = **ребёнок `PRIMUM`**
Живая сцена: у `PRIMUM` **26 детей** → бейк даёт **23** блока (`ADMIN_FOR_COLLIDER_COLLIDER`, `ADMIN_DOOR_PREFABS`, `ADMIN_ENTRANCE_PORTAL` не бейкаются: нет прямых `MeshRenderer` вне слоя дверей).
Имя блока = имя ребёнка; ребёнок ↔ чанк директора связываются **по имени** (`_chunkByName:1010`). Футпринты (J): **26×154 м** (`S7_BIG`) … **228×156 м** (`S9_TRD-S2`), BASE-плита **685×1 037 м**; город целиком **1 466 × 73 × 1 059 м**, центр `(39 593, 2 510, 39 834)` в пространстве `WorldRoot_0_0`.

### 3.2 Правила бейка (`Editor/Culling/PrimumHlodBaker.cs`, константы `:67-89`)
| Шаг | Правило | Значение |
|---|---|---|
| 1 | Отбор (`CollectRenderers:499`) | `MeshRenderer`, `enabled`, `activeInHierarchy`, не слой 10, не `DoorController`, не `forceRenderingOff` |
| 2 | Склейка | по сабмешам через `CombineMeshes` без матриц, вершины → `WorldRoot_0_0`, зеркальные трансформы — переворот обхода |
| 3 | Цвет/PBR в вершину | `_Wood_Color/_Bronze_Color/_Stone_Color…` → color (sRGB); `_Metallic/_Smoothness` → UV0 |
| 4 | Эмиссия | класс в альфу: `opaque 0 / windowDark 85 / windowLit 170 / lamp 255`; окно горит по хэшу центра — **55 %**; лампы всегда |
| 5 | Мелочь | связные компоненты (сварка 1 мм), компонента **< 0.25 м** удаляется (окна/лампы — нет) |
| 6 | Интерьеры | внешняя видимость лучами PhysX: **41 направление** (5 высот × 8 азимутов + зенит), сетка **0.75 м**, ≤250 000 лучей/напр. |
| 7 | Формат | вершина **24 байта** (pos f32×3, normal SNorm8×4, color UNorm8×4, PBR UV0 UNorm8×4), UInt16 (макс. 42 464 верт., UInt32 авто >65 535); один сабмеш, pivot = центр AABB, **один материал на весь город** |

Итог: **859 рендереров / 5 407 574 тр. → 266 794 тр. (−95.1 %)**, 571 905 верт.; удалено мелочи **888 284** тр., скрытых **4 252 496** тр.; время **≈28 с** (23 блока + L2).
Тяжёлые: `ADMIN` 631 694 → 10 104 (купол сохранён), `S6_NQ` 504 496 → 12 577, `S5_NE_P2` 405 132 → 9 905, `S7_SE_P1` 382 472 → 18 582; `BASE` 18 466 — без фильтра видимости. Контроль потерь (16 видов): покрытие **0–0.53 %**, цвет **0–5.0 %** (пилот `S9_TRD-S2`: 0.105 % / 2.5 %). Предел **19 033 тр./блок**, минимум 1 852.

### 3.3 Ассеты, материалы, шейдеры
| Объект | Путь/имя | Факт |
|---|---|---|
| 23 L1-меша | `Assets/_Project/Generated/HLOD/<блок>_L1.asset` | **29.31 МБ**, **1.27 МБ** в среднем (текст YAML, `m_SerializationMode: 2`; бинарь ≈0.54×) [E] |
| Материалы | `PRIMUM_HLOD_L1.mat` (`ProjectC/PrimumHlod`), `PRIMUM_HLOD_L2.mat` (`ProjectC/PrimumImpostor`) | L1: `_LampIntensity 2.4`, `_WindowDayDim 0.55`, `_WindowColor (2.2,1.45,0.75)`; L2: `_HalfW 919.14`, `_Metallic 0.002`, `_Smoothness 0.248`, `_LightsFogKeep 0.6` |
| Импостор | `PRIMUM_L2_Impostor.asset` | квад 4 верт., bounds-сфера radius ≈1 150 м [M] |
| Текстуры L2 | `PRIMUM_L2_Albedo.asset`, `PRIMUM_L2_Night.asset` | по **10.66 МБ** (DXT5, 4096×1024 + 6 мипов) |
| Манифест | `Assets/_Project/Resources/HLOD/PRIMUM_HLOD.asset` | 304 стр.; `format 1`, `bakeHash 5253f70a…`, 23 блока, `l1Distance 3000`, `l2Distance 12000`, `hysteresis 0.1` |
| Шейдеры | `Shaders/HLOD/PrimumHlod.shader`, `PrimumImpostor.shader` | L1: `UniversalFragmentPBR`, проходы `ForwardLit/DepthOnly/DepthNormals` + 2 bake, **теней нет**, лампы сдвиг 1.5 м к камере. L2: 4 верт., выбор кадра в вершинном шейдере, alpha-test, ночные огни поверх тумана |

Рантайм: директор читает манифест и создаёт `WorldRoot_0_0/PRIMUM_HLOD` (`HideFlags.DontSave`, не static); рендерер — `shadowCastingMode Off`, `receiveShadows false`, probes off, `ForceNoMotion`, `forceRenderingOff=true` до первого тика (`:1198-1215`). В сцену/префабы ничего не пишется (`PRIMUM_HLOD` в YAML — **0** совпадений); страховка — `PrimumHlodEditModeCleanup` (`:1653`).

### 3.4 Сколько DC и что входит
**46 DC на город в L1** = ≈**2 DC/блок** (ForwardLit + depth/depth-normals) × 23. Порог — **до AABB блока**, от `lodBias` не зависит (пресеты far 30/60/120 км). Входит: **только город**. **Не входит:** фермы (142 MR / 1.27 M тр.), двери ферм, спутники, `RuinsValleys`, корабли, интерьеры (вырезаны лучами), двери. Фермы — «идеальный первый кандидат» на L1 (BIT §6.2).

### 3.5 Как бейкать и детерминизм
**Project C → Culling → Stage 7 HLOD**: `Bake Pilot Block` (только `S9_TRD-S2`) · `Bake All Blocks + L2` · `Rebake Determinism Check`.
Порядок: открыть `WorldScene_0_0` (не Play) → `Bake All Blocks + L2` → Console/`D/U_stage7_bake_2026-11-01.csv` → `Rebake Determinism Check`
→ закоммитить `Generated/HLOD/**` + `.meta` + манифест. Пишутся PNG-превью (`U_stage7_L1_pilot_preview`, `_admin_preview`, `_L2_atlas_preview`). Детерминизм: `bakeHash=5253f70a…`, `mismatches=0`; повторный Bake All → SHA1 всех **29** файлов `FILES_IDENTICAL 29` (GUID сохранены). Обеспечен: хэш центра окна, порядок детей `PRIMUM`, только свойства материалов.

## 4. HLOD L2 — импостор (12+ км)

| Параметр | Значение |
|---|---|
| Кадры | **8 азимутов × 4 высоты** (`RowElevations 0/8/20/45°`), кадр **512×256**, суперсэмплинг **2×** |
| Атлас | **4096×1024** (8×4 кадра), DXT5, **6 мипов** с сохранением покрытия alpha-test |
| Каналы | albedo+покрытие, ночные огни (+нормаль.y), `_NightScale 4`, `_Cutoff 0.5`; PBR по средним города |
| Метрика | `_HalfW 919.14 м`; `_RowHalfH` **40.75 / 164.96 / 354.80 / 691.97 м** |
| Бейк | ортокамера по L1-мешам через `CommandBuffer` в RT; **в сцене ничего не создаётся** |
| Стоимость | **2 DC**; полный кадр на 20–80 км — 464–513 DC; состояния нет → FO 🟢 (только скаляры) |

**При добавлении контента:** всё, что попало в `PRIMUM`, требует **полной перепечки L1+L2** (`Bake All Blocks + L2`): обновляются 23(+N) мешей, `cityCenter/citySize`, `_HalfW/_RowHalfH`, атлас, `impostorHash`, `bakeHash`. Не перепечь = молчаливая деградация: L2 и L1 блока показывают старый силуэт, новый квартал виден только в L0 (<3 км).
**Риски:** один атлас на город (4096 px на 1.47 км → ≈0.36 м/px в кадре, ≈3.6 м/px на экране с 12 км [E]); футпринт ×10 → детализация ×1/10 (нужны тайлы/каскады); HLOD нельзя выгрузить (Resources держит 29 мешей + 2 текстуры всегда, ≈25 МБ бинаря [E]).

## 5. Двери L0 под GRD (H8)

**Почему `layerCullDistances` мёртв:** `InstanceCuller` GPU Resident Drawer проверяет только `cullingLayerMask` (`Library/PackageCache/com.unity.render-pipelines.core@9fe9377312a8/Runtime/GPUDriven/Culling/InstanceCuller.cs:359-363`, цит. по R7 §2.2) → после Stage 4 ≈25 тыс. инстансов / ≈137 DC / ≈1 млн тр. дверей рисовались на любой дистанции.
**Как теперь:** в директоре, `doorCullDistance = 150 м`, возврат на `150·(1−0.1) = 135 м`, тик 3 Гц, то же единственное свойство (`UpdateDoors:1147`). Два независимых повода: (1) блок на L1/L2 — `PRIMUM_DOORS/<блок>` + `ADMIN_*` → блок ADMIN; (2) дистанция — для **всех** дверей слоя 10, включая фермы (`block = -1`, 270). Ортокамера → дистанционная политика выключается (`useDistance` требует `!_snapOrtho`).
**Замер (R8 §2.3, §4):** на площади гасятся **4 697** из 5 493; `<130 м` — 913 видимых / **0** погашенных; `>165 м` — 4 006 погашено / **0** нарисованных; 130–165 м — 574 рендерера (гистерезис). Δ = −112 DC, −0.54 M тр., −13 364 hybrid-инстанса; `enabled=false` — **0**.
**Проверять при добавлении дверей:** слой **10** (иначе попадёт в L1-бейк и под-пиксельную политику); `DoorController` + коллайдер; корень `PRIMUM_DOORS/<имя>`, **равное имени блока** (иначе дверь не привяжется к блоку — только дистанция), либо `*_DOORS` для ферм. Поп-ин 130–150 м бенчем не доказывается — нужен глаз пользователя; при заметном поп-ине поднимать `doorCullHysteresis`/дистанцию.

## 6. Масштаб: если контента станет ×100

### 6.1 Пределы (измерено/выведено)
| Метрика | Сейчас (×1) | Формула/предел | ×10 | ×100 |
|---|---:|---|---:|---:|
| Блоков HLOD | 23 | = детей `PRIMUM`; жёсткого потолка в коде **нет** | 230 | 2 300 |
| L1 DC | **46** | **≈2 DC/блок**; гейт ≤500 → **потолок ≈250 блоков** | 460 (на грани) | **4 600 — провал в 9×** |
| L1 тр. | 266 794 | ≈11.6 k тр./блок | 2.7 M | 26.7 M (без GRD-инстансинга) |
| L1-ассеты | 29.31 МБ | **1.27 МБ/блок** (YAML) | 293 МБ | **2.9 ГБ в git** |
| L2 | 2 DC | 1 квад + атлас на весь город | атлас на 14.7 км | нужны тайлы/каскады |
| Бейк | ≈28 с | ≈1.1 с/блок + L2 | ≈4.5 мин | ≈45 мин |
| Сцена | 60 МБ / 1.54 M строк / 174 001 override (34.3 МБ) | один `WorldScene_0_0` | ≈600 МБ | ≈6 ГБ — **не грузится** |
| Память HLOD | ≈25 МБ [E] | всё в `Resources`, не выгружается | ≈250 МБ | ≈2.5 ГБ резидентно |
| FO-участники | 61 корень | все активные корни | ≤100 | ≤100 |

**Вывод:** ×100 упирается не в L0-геометрию, а в три вещи: (1) `2 DC × блоки` (L1-гейт живёт до ~250 блоков); (2) один `WorldScene_0_0` как монолит (60 МБ → 6 ГБ YAML, `maxLoadedScenes 4`, preload 10 км); (3) `Resources`-манифест, тянущий всё HLOD всегда.

### 6.2 Правила «новый квартал = …»
1. **Новый квартал = новый ребёнок `PRIMUM`** (= блок = чанк директора), имя `PRIMUM_<TYPE>_S<n>_<ZONE>_P<m>_<NN>H`, совпадающее с `PRIMUM_DOORS/<то же имя>`. Один блок: 26–228 × 156 м, ≤**80 k тр.** (BLK §2), ≤65 535 верт./меш, join «блок × материал».
2. **1 блок ≈ 2 DC в L1 ≈ 1.27 МБ ассетов.** Бюджет мира: **≤200–250 блоков**, иначе L1 ≤500 DC не выполняется → на ×10 нужен промежуточный уровень (L1-агломерация по 5–10 блоков) или L1-каскад, а не 230 мешей.
3. **≤~100 корней сцены** (FO-участники = корни) и **≤4 загруженных `WorldScene_*`**: кварталы раскладывать по сценам сетки 6×4 (79 999 м), а не в один `WorldScene_0_0`.
4. **Регион больше сцены = отдельная сцена + свой HLOD-манифест.** Сейчас манифест один и жёстко привязан к `PRIMUM`/`WorldRoot_0_0`; до этого масштабировать нельзя.
5. **Фермы/спутники/руины в HLOD не входят**: при ×100 они дают L0-DC на всех дистанциях. Прежде чем множить фермы — сделать им L1 по тому же шаблону (бейкер знает только `PRIMUM`).
6. **L2 пересматривать при футпринте >≈5 км** (один квад+атлас перестаёт читаться): 2–4 региональных импостора или тайловый атлас.
7. **Каждый новый блок = перепечь L1+L2** + F8/F9 + `Rebake Determinism Check`.

## 7. Чек-лист «новый квартал / ферма в мир» и инструменты

| # | Шаг | Инструмент/меню | Критерий |
|---|---|---|---|
| 1 | Blender: нарезка, join «блок × материал», ≤80 k тр., UV/нормали | `BLK §3`, `.opencode/rules/blender-mcp.md` | таблица «блок → меши → тр. → габарит» |
| 2 | FBX-экспорт каноном (`FBX_SCALE_NONE`, Y-up, Apply Transform OFF) | `115_CITY_FBX_EXPORT.md` | те же числа (±1 % тр., ≤0.02 м) |
| 3 | Unity: блок под `PRIMUM/<имя>` (или `Primum_farms/<имя>`), слой 8, **не static** | — | имя = имя будущего L1-блока |
| 4 | Двери: префаб на слой 10, `PRIMUM_DOORS/<имя блока>` | `Door Layer → Apply` (EP §9.2) | `DoorController` + коллайдер |
| 5 | `NetworkObject`? → регистрация | `ProjectC/World/Floating Origin/Bake Pilot Catalog (Preview + Apply)` | Host стартует, аудит без расхождений |
| 6 | HLOD | `Project C/Culling/Stage 7 HLOD/Bake All Blocks + L2` | 23+N блоков, `impostor=yes` |
| 7 | Детерминизм | `…/Rebake Determinism Check` | `DETERMINISTIC; mismatches=0` |
| 8 | Игра (делает пользователь): F8/F9, полёт 0.5→80 км | — | L0<3 км, L1 3–12, L2 >12; двери >150 м = 0; 0 errors |
| 9 | Коммит / диагностика | git; `Project C → Culling → Stage 6/7/8` (`FinalValidationBench.cs`), `T_CULL01AcceptanceProbe.cs` | `Generated/HLOD/**` + `.meta` + манифест + docs; DC, счётчики, камеры union |

## 8. Расхождения / риски

1. **«23 блока» — не константа пайплайна, а число детей `PRIMUM`.** В коде нет ни массива, ни предела: манифест — список, директор матчит по имени. Переименование ребёнка `PRIMUM` **молча** убивает его L1/L2 (`missing++` только в логе); `2 DC/блок` упирается в гейт L1 ≤500 на ~250 блоках.
2. **Документ нарезки расходится с фактом.** `BLK:10-31` проектировал «блок = квартал **100–300 м**, 20–60 блоков, 40–120 DC»; факт — 23 блока-чанка с футпринтами **26×154 … 228×156 м**, а BIT:177 фиксирует: «по блоку 100–300 м — no-op, чанк и есть блок».
3. **Комментарий манифеста врёт про L2:** `PrimumHlodManifest.cs:10` — «8 азимутов × **3** высоты», факт — **4** ряда (`_Grid=(8,4)`, `bakeSettings rows=0/8/20/45`). Правы код и ассет, не комментарий.
4. **`l1Distance/l2Distance/hysteresis` бейкер не пишет** — берутся из инициализаторов класса (3000/12000/0.1). При смене дефолтов в классе старый ассет сохранит старые числа → расхождение кода и ассета.
5. **Манифест в `Resources` = всё HLOD резидентно** (29 мешей + 2 атласа: 50.65 МБ на диске, ≈25 МБ в памяти [E]), выгрузки нет — потолок для ×10/×100 без Addressables/сцен.
6. **L1-бейк исключает `!r.enabled`** (`CollectRenderers:506`) и `forceRenderingOff`: контент, спрятанный в редакторе, в L1 не попадёт и «исчезнет» на 3–12 км (в L0 виден). Правило: всё, что должно жить в L1, включено в редакторе.
7. **Двери ферм (`block = -1`) режутся только по дистанции**, а `layerCullDistances` под GRD мёртв: другой порог для ферм = новый параметр, но не новый владелец свойства.
8. **Порог 3 000 м — до AABB блока, не до центра города:** на 3 км уровень смешанный (5 блоков L0 + 18 L1, R7 §5.2). При мелких блоках смешанная зона расширяется, pop-in на переходах заметнее.
9. **Неактивные корни сцены выпадают из FO** (`T-FO06CZ`): включённый позже контент останется в старых координатах после F8.
10. **`Primum` (нижний регистр) vs `PRIMUM`** живут одновременно; легаси-корень неактивен и директором не распознаётся (точное сравнение имён).
11. **Числа «до» из ранних отчётов не сравнимы с текущими:** 2 362–2 378 включали камеру нового меню (+37 DC); честный baseline 2 341, текущий 2 229–2 235 (R7 §2.1, R8, EP §9.2).
12. **`RuinsValleys` — 496 детей в сцене**, а BIT:201 говорит о «60 хамлетах»: для директора это **одна** наблюдаемая группа (ведёт `DetailDistanceCuller`), т.е. гранулярность руин = 1.
13. **[U] Силуэт L1/L2 в игре не доказан бенчем** (розовая дымка/облака, R7 §7): выводы §3–§4 — из bake-превью, не из кадра; переходы 3 км и 12 км требуют глаза пользователя. **[U]** `06_CONTENT_PIPELINE.md:11` считает у `WorldRoot_0_0` 20 детей, живой MCP — **22** — счётчики сверять при правках.
