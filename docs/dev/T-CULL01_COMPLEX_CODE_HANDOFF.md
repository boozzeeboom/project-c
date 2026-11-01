# T-CULL01 — Culling/visibility: сложные части кода и handoff другому агенту

**Дата:** 2026-10-31 · **Базовый коммит:** `6b49ac51` (docs: culling research)
**Родительские документы:**
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\README.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\09_BEAM_COMPARISON_AND_DECISIONS.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md`

**Статус:** ticket ready; код не начат. **Владелец:** Lead. **Исполнитель сложных пакетов:** отдельный агент,
по одному этапу (правило Stage gate).
**Смысл тикета:** здесь собраны **только сложные/рискованные части кода** с абсолютными путями и
приёмочными критериями. Простые правки (слой дверей, настройки, baseline) в тикет не входят — они
делаются маленькими коммитами по `10_EXECUTION_PLAN.md`.

> ⚠️ Line numbers указаны на базовом коммите `6b49ac51`. Перед правкой агент обязан перечитать файл
> (строки могли сдвинуться) и приложить актуальные ссылки в отчёте.

---

## 0. Правила для агента-исполнителя (HARD)

1. Делать **только один пакет H** за задачу; не трогать соседние этапы.
2. Не менять сцены/префабы вне разрешённого пакета; **не использовать scene-overrides** для мержа.
3. Не запускать Play Mode, не делать скриншоты, не push; коммит — только по разрешению Lead.
4. Не трогать: `Terrain`, `PRIMUM_COLLIDERS`, `DoorController`, `PrimumLightPool`, `NetworkManager`,
   `ClientSceneLoader`, `ScenePlacedObjectSpawner`, `GlobalMotion*` контракты без явного пункта пакета.
5. Все новые mesh-ассеты — **`.asset` в git**; prefab/FBX — Lore VCS (`.gitignore:151–154`).
6. После работы — отчёт: изменённые файлы (absolute paths), `git diff --stat`, замеры до/после,
   проверки F8/F9, reimport, rollback-план.

---

## 1. Матрица сложности

| ID | Этап | Пакет | Сложность | Почему сложно | Срок |
|---|---|---|---|---|---|
| **H1** | 1 | `ShipPrefabRepair` | **высокая** | 238 коллайдеров/9 дверей внутри nested FBX; reimport; prefab internals | 0.5–1 д |
| **H2** | 3 | `ShipVisualMerger` + LOD | **высокая** | 973 MR, 10 материалов, интерактив/фрактуры, `UInt32`, lightprobes | 3–5 д |
| **H3** | 2 | `DoorVisibilityBudget` + `ShadowBudget` | **средняя** | мультикамера, FO, гистерезис, тени/слои | 3–5 д |
| **H4** | 5 | `PrimumCityRebatch` + manifest | **очень высокая** | 174 001 scene-override, prefab вне git, B2, детерминизм | 2–4 нед |
| **H5** | 4 | GRD + DOTS-шейдеры | **высокая** | 20 Shader Graph, `KeepAll`, тени/створки, откат | 2–3 нед |
| **H6** | 6 | `VisibilityDirector` | **высокая** | мультикамера, FO, бюджеты, ≤2 мс | 3–4 нед |
| **H7** | 7 | HLOD/L2/PVS | **очень высокая** | offline bake, rebake, FO-локальные данные | 6–10 нед |

---

## 2. H1 — `ShipPrefabRepair` (Stage 1, 0.5–1 день)

### 2.1. Цель
Вынести **250 BoxCollider** и **9 `DoorController`** из инстанса тяжёлого FBX в обычные дети
prefab-корня, чтобы реимпорт модели их не сносил. **Визуал не склеивать.**

### 2.2. Входы (absolute)
- Префаб: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab`
- FBX: `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx`
- Скрипты:
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\DoorController.cs` — класс **32**, `[RequireComponent]` **30**; 9 инстансов на узлах FBX.
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipRootReference.cs`, `ShipComponentLocator.cs`.
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\ShipController.cs` — **39–41, 100–106, 461–462**.
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipDeckNav.cs` — **26–30, 62, 67**.
- Тулы-референсы:
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\BuildSettlementColliders.cs` — shipMode **34–35, 89, 160, 268–269, 1438**.
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\ShipPresetCreator.cs` — паттерн **40, 43, 181, 526, 578–589**.
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\CombineMeshesToCollider.cs` — **НЕ использовать как есть**: `convex=false` **172**, один меш **114**.

### 2.3. Что сделать
1. Dry-run: построить отчёт «что лежит внутри FBX-инстанса» (пути, типы, локальные позы).
   Факт: **238 из 250** боксов лежат внутри инстанса FBX (10 — под корнем FBX, 2 — под корнем prefab);
   9 `DoorController` — на узлах FBX. Переносим все 250 боксов и 9 дверей в обычные дети prefab-корня.
2. Создать Editor-скрипт `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipPrefabRepair.cs`
   (новая папка `Editor\Ships`, .meta создаст Unity).
3. Перенести 250 `BoxCollider` в `SHIP_COLLIDERS` дети prefab-корня с сохранением world/local поз.
4. Перенести 9 `DoorController` + их объекты в `Doors/` дети prefab-корня; сохранить ссылки
   `ShipRootReference`; не дублировать.
5. Не менять source FBX; работать с prefab instance через `PrefabUtility`/`GetComponentsInChildren`.
6. Dry-run/apply меню `ProjectC/Ships/Repair Heavy Prefab`; результат — отчёт `08_DATA/H1_prefab_repair.csv`.

### 2.4. Приёмка
- После reimport FBX: 250 боксов и 9 дверей **на месте**, нет дублей, 0 missing refs.
- `DoorController` открывается; `ShipRootReference`/`ModuleSlot`/`PilotSeat` не потеряны.
- `git diff` по сцене — **пусто**; изменения только в prefab/Editor-скрипте.
- F8/F9: 0 errors, корабль на месте.
- Rollback: backup prefab; revert одного файла.

---

## 3. H2 — `ShipVisualMerger` + LOD (Stage 3, 3–5 дней)

### 3.1. Цель
**Только тяжёлый `Ship_havyII_base`.** Остальные корабли (`Ship_medium_base`, `Ship_Light_root_fbx_test`,
20 NPC) в H2 **не трогать**: они выносятся в future scale-out после зелёного пилота.
P1/P2: greeble-мерж (973 → ~620 slot), затем LODGroup; P3 (позже) — «8 ячеек разрушения × материал»
(973 → ~92). Пилот должен доказать пайплайн; масштабирование на остальные — отдельный этап.

### 3.2. Входы (absolute)
- Heavy prefab: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab`
- Heavy FBX: `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx`
- Данные: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\I_ships_compartments.csv`
  (936 объектов), `I_ships_dc_model.csv`, `I_ships_prefabs.csv`.
- Паттерн сборки: `C:\Projects\ProjectC_client\Assets\_Project\Editor\ShipPresetCreator.cs` — **40, 526, 578–589**.
- API-референс: `C:\Projects\ProjectC_client\Assets\_Project\Editor\CombineMeshesToCollider.cs` — **31, 65–183, 114, 172** (нельзя `convex=false`; можно `CombineMeshes`).
- **Вне scope H2:** `Ship_Light_root_fbx_test.prefab`, `Ship_medium_base.prefab`, 20 NPC — future scale-out.

### 3.3. Что клеить / что нельзя
**Клеить:** статик-визуал корпуса/интерьера/двигателей. В префабе **17 material assets** (10 корпусных
из FBX + материалы двигателей/роторов/PDTT), но **группировать по ассету материала, не по имени** —
в FBX есть дубли имён (`Metal_Brass` ×2, `Metal_Steel_Dark` ×2 и т.п.). P1 — только greeble:
`Bolt_*`, `UnderHull_*`, `Interior_*`, `Contour_*`, `Mess_*`, `Det_*` (359 объектов, ~8 % тр.)
в 10 сабмешей по материалам корпуса.
**Нельзя:** 9 дверей + `Ramp_Cargo`, `PilotSeat`, 11 `ModuleSlot`, `CargoVisual`,
`EngineThrusterVisual`/`ShipPartShake`, `ContrailVFX`, 250 коллайдеров, будущие фрактуры.

### 3.4. Что сделать
1. Новый `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipVisualMerger.cs`:
   - обход только визуальных детей; whitelist интерактива (по именам/компонентам);
   - группировка по `(cell, material)`; `CombineInstance`; `mergeSubMeshes=false`;
   - `indexFormat = UInt32` при сумме вершин > 65 535;
   - `RecalculateBounds()`; сохранить UV/нормали (не пересчитывать нормали без нужды);
   - выгрузить `C:\Projects\ProjectC_client\Assets\_Project\Generated\Ships\<Class>_VIS_<Cell>_<Mat>.asset`
     (новая папка; `.asset` в git);
   - пересобрать prefab-ассет, **не scene-overrides**; `ShipPresetCreator`-паттерн.
2. P2: `LODGroup` на корабль (LOD0 полный, LOD1 без bolt/mess/underhull, LOD2 1 mesh/3–5 материалов;
   пороги 300/900/2500 м). LOD-меши — отдельные `.asset`.
3. P3 (отдельной задачей после плана фрактуры): 8 ячеек × материал → ~92 slot; каждый кусок —
   отдельный GameObject/меш + выпуклый коллайдер (для будущего pre-fractured).

### 3.5. Приёмка
- **P1 — PASS:** heavy 973 → **616 MR**, 20 merged meshes; Σtris не изменились; двери/слоты/cargo/VFX целы.
- **P2 — PASS (conditional):** `ShipShadowBudget` (весь LODGroup, поштучно) закрыл гейт:
  **d200 1 170 / repeat 1 161 / d300 1 236** при ≤1 300; d400 122 / d1200 104.
  Визуальная цена: у корабля дальше ~150 м пропадает тень статики — нужен sign-off пользователя.
- **Stage 3c** закрыт; см. `T-CULL01_STAGE3C_REPORT.md`; visual/F8/F9 — открыты.
- **Другие корабли — 0 изменений**; scene-diff пустой; H1-R1 сохранён.
- P2 зелёный (после visual sign-off) разблокирует **Stage 3b**; P3/фрактура — отдельно.

### 3.6. Stage 3b — future scale-out (⛔ НЕ СЕЙЧАС)
Решение пользователя 2026-10-31: medium/light/NPC **сейчас не трогать**; они типовые и пойдут по
пайплайну HeavyII позже. Если мешают в кадре — выключить их `MeshRenderer` runtime-ом, prefab не менять.
Отдельный бриф `T-CULL01_STAGE3B_AGENT_BRIEF.md` помечен «отменено/не выполнять»; к нему вернуться
только по отдельному решению.

---

## 4. H3 — `DoorVisibilityBudget` + `ShadowBudget` (Stage 2, 3–5 дней)

### 4.1. Цель
Убрать 84 % DC дверей и лишние тени: слой + дистанция + per-object shadow budget, без переделки
геометрии и без потери `DoorController`/коллайдеров.

### 4.2. Входы (absolute)
- 12 дверных префабов: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\*.prefab`
  (`PM_DOOR_SINGLE_PLANK_A`, `PANEL_B`, `ARCH_C`, `DOUBLE_A`, `PALACE_DOUBLE_A/B/C`, `GATE_A/B`,
  `WIDE_A`, `NARROW_A`, `PM_ADM_ENTRANCE_PORTAL`); **11 используются в сцене** (NARROW_A — 0 инстансов);
  2 598 инстансов / 5 493 MR / 5.19 slot.
- Слои: `C:\Projects\ProjectC_client\ProjectSettings\TagManager.asset` — **9–41**; свободно 21 слот.
- Камера/FO:
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DetailDistanceCuller.cs` — **13–107** (гистерезис, 0.5 с), **87** (`Camera.main` — не повторять).
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\PrimumLightPool.cs` — **33, 159–219** (выбор камеры).
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ViewDistanceApplier.cs` — **75, 78, 82, 93, 111**.
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\FloatingOrigin\Network\GlobalMotionControlledRebaseSlice.cs` — **265–418, 326, 1345, 1349–1354**.
- Настройки: `ProjectC_URP.asset` **58–59, 73, 86–88**; `ProjectC_URP_Renderer.asset` **189–190**.

### 4.3. Что сделать
1. Слой `PRIMUM_DOORS` (слот 10); перенести 11 префабов; `layerCullDistances[10]=120–150`,
   `layerCullSpherical=false` для геймплейной камеры.
2. Новый `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DoorVisibilityBudget.cs`:
   группы дверей (дом/чанк), гистерезис 10–15 %, обновление 2–4 Гц, `Renderer.enabled`/`forceRenderingOff`,
   `shadowCastingMode=Off` дальше 40–80 м; не трогать `DoorController`/коллайдеры.
3. Новый `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ShadowBudget.cs`:
   город/фермы: `shadowCastingMode=Off` по дистанции/группам; «тени только у ближних».
4. Мультикамерный union: только геймплейные камеры; `AtmosphereCamera` (меню, far 10) и статичная
   `BootstrapScene/MainCamera` — исключить; не использовать `Camera.main`.
5. FO: живые Transform; никаких мировых кэшей; после F8/F9 — сброс/пересчёт.

### 4.4. Приёмка
- 26 981 → **5 000–9 000 DC** (A); двери в кадре — сотни.
- Нет заметного pop-in/мигания; тени — по согласованному допуску.
- F8/F9: 0 errors, видимость не «залипла»; raycast/физика после смены слоя не сломаны.
- Kill: экономия <30 % / визуальные дефекты / поломка дверей → откат.

---

## 5. H4 — `PrimumCityRebatch` + manifest (Stage 5, 2–4 недели) — ⛔ ЗАКРЫТ ДАННЫМИ

> **Не выполнять.** Stage 4b dry-run на пилоте `S9_TRD-S2`: 160 текущих пар против 532 у `J_B`;
> B2 дал бы **330** батчей (`verdict=B2_increases`), то есть ~6.3× хуже текущего слитого состояния.
> Условие «окклюзия ≥35 %» также не выполнено (0 %). H4 оставлен как историческая справка.

### 5.1. Цель
Сделать пере-склейку города **воспроизводимой** и, при зелёном GRD, выполнить **B2** (дом × материал,
только геометрия ≥1 м). Не повторять 174 001 scene-override.

### 5.2. Входы (absolute)
- Prefab-источник: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\PRIMUM_CITY.prefab`
  (**не в git**; 16 468 MR / 20 694 slots).
- Scene: `C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity`
  (62.9 МБ; 174 001 override / 34.3 МБ = 54.6 %).
- Геометрия: `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Primum\Rebased\**` (16 809 мешей).
- Данные J: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\J_granularity_variants.csv`,
  `J_city_chunks_variants.csv`; `C_batch_bounds.csv`, `C_primum_batches.csv`.
- Референсы: `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\PrimumMeshRebase.cs` (32,40,48,52,440–441),
  `PrimumMaterialMap.cs` (31,38,113–173,196–227), `DoorPrefabBuilder.cs` (63–70,141,219,345,514,548–560),
  `CombineMeshesToCollider.cs` (114).
- `.gitignore`: `C:\Projects\ProjectC_client\.gitignore` — **151–154, 206** (`*.prefab`, `*.fbx`, `Art/*`; `*.asset` в git).

### 5.3. Что сделать
1. Новая папка `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\09_MERGE\`
   (манифест/отчёты; **не путать с `09_BEAM_COMPARISON_AND_DECISIONS.md`**):
   `mapping.csv` (`chunk, unit, source_object, material, target_mesh`) + `report_<chunk>.csv`.
2. Новый `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\PrimumCityRebatch.cs`:
   - группировка `MeshFilter` по `(unit, material)`; unit = дом из имени (`RES_S2_344`), ADMIN/палас —
     комната, BASE-плиты — чанк;
   - `CombineInstance`; `mergeSubMeshes=false`; `UInt32` при >65 535 вершин; `RecalculateBounds()`;
   - меши: `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Primum\Rebatched\<чанк>\<unit>_<MAT>.asset`
     (новая папка; `.asset` в git);
   - prefab-ассет на чанк `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\PRIMUM_BLOCKS\<чанк>.prefab`
     с локальными координатами; **никаких scene-overrides**;
   - dry-run/apply меню; отчёт `Σtris ±0.1 %`, `Σverts ±0.5 %`, слоты = группы, max delta world pos <0.001 м.
3. B2: в группу входит только геометрия ≥1 м; мелочь/интерьеры — чанк-батчи; материалы/UV/имена не менять;
   коллайдеры/`PRIMUM_DOORS`/лампы/`PRIMUM_COLLIDERS` не трогать; static не ставить.
4. Пилот: `S9_TRD-S2` (104 рендерера, 45–46 домов → ~330 B2); затем решение.
5. Перед переклейкой — **воспроизводимость**: тул + манифест в git; `PRIMUM_CITY.prefab` — в VCS/Lore
   (согласовать с Lead; правка `.gitignore` — отдельное решение).

### 5.4. Приёмка
- Dry-run: `Σtris ±0.1 %`; слоты = числу групп; отчёт записан.
- Apply на копии сцены: prefab цел, 0 ошибок; reimport одного FBX не ломает prefab;
  Apply/Revert prefab-инстанса не рушит мерж.
- B2 пилот: цвет ±10 %, тени ≤+10 %, DC **не растёт**; F8/F9 зелёный.
- Kill: Σtris >0.1 %, scene-diff >1 000 строк, reimport ломает, DC растёт — откат к A.

---

## 6. H5 — GRD + GPU occlusion + DOTS-шейдеры (Stage 4, 2–3 недели)

### 6.1. Цель
Включить `GPU Resident Drawer = InstancedDrawing` + GPU occlusion; двери в инстансах (≤~72 пары),
цель 1 500–2 500 DC. **Сам алгоритм не писать** — использовать engine.

### 6.2. Входы (absolute)
- Настройки:
  - `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP.asset` — **58–59, 73, 86–88**.
  - `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP_Renderer.asset` — **189–190**.
  - `C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset` — **58, 79–80**.
- Shader Graph: 15 графов города + 5 дверей; список — `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\C_city_materials.csv`,
  `C_shaders.csv`; папка `C:\Projects\ProjectC_client\Assets\_Project\Materials\**` (город — 49 SG).
- Package URP 17.5 (версия в точку):
  - `C:\Projects\ProjectC_client\Library\PackageCache\com.unity.render-pipelines.core@9fe9377312a8\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs` — **18–19, 60**.
  - `C:\Projects\ProjectC_client\Library\PackageCache\com.unity.render-pipelines.universal@748d7458cc62\Runtime\UniversalRenderer.cs` — **11–23**.
  - `...\UniversalRenderPipeline.cs` — **1700, 1753**; `...\UniversalRenderPipelineAsset.cs` — **528, 1465–1468**;
    `...\UniversalRendererRenderGraph.cs` — **974–980, 1515–1524**.
- Двери/тени: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\*.prefab`,
  `ShipController`/`DoorController` (створки).

### 6.3. Что сделать
1. **Корабли не оптимизировать:** `Ship_havyII_base` не трогать; medium/light/NPC — не менять.
   Если корабли мешают city-замеру — выключить их `MeshRenderer` runtime-ом (без сохранения сцены).
2. Audit 15+5 `.shadergraph`: включить DOTS/instancing-варианты; проверить 0 `DOTS_INSTANCING_ON`
   у рукописных шейдеров; не ломать процедурные world-space шумы.
3. `GraphicsSettings.asset: m_BrgStripping = 2 (KeepAll)`; URP: `m_GPUResidentDrawerMode = 1`,
   `m_GPUResidentDrawerEnableOcclusionCullingInCameras = 1`; Forward+ уже включён.
4. Пилот на одном чанке → вся сцена: Frame Debugger `Hybrid Batch Group`; DC/SetPass/tris.
5. Проверить: двери (створки), тени, материал/эмиссию, F8/F9, 0 shader errors.
6. Fallback: исключить двери из GRD (`GPUDrivenProcessor`/opt-out) или откатить к 18.10-состоянию
   (`m_GPUResidentDrawerMode=0`, `m_BrgStripping=0`).

### 6.4. Приёмка
- **Факт Stage 4 (2026-10-31):** P1 **6 400 → 3 031 DC (−52.6 %)**, ночь 1 767, PILOT 1 178;
  `hybridDC=2 876 / 18 196 instances`, `srpBatcherDC=8`; 0 shader errors; визуальный sign-off пользователя есть.
- **Факт Stage 4b (2026-11-01):** городской `CityShadowBudget` 200 м дал **P1 3 047 → 2 423 DC**
  (−20.5 %, гейт ≤2 500 взят, запас 3.1 %); visual sign-off пользователя 01.11; 150 м = 2 302.
  `smallMeshScreenPercentage` оставлен 0; atlas gain 0; **B2 dry-run = `B2_increases`**.
  Отчёт: `T-CULL01_STAGE4B_REPORT.md`, CSV `P_stage4b_*`, компонент `CityShadowBudget.cs`.
- **Stage V — FAIL:** реальный `CityShadowBudget` дал **P1 2 811** (entries 1 301, ожидалось 1 482/2 423);
  gate 2 500 не взят. F8 PASS (256 м, 0 errors); F9 — rollback `restore=True`, но bench ждал frameGen
  и записал `FAILED_no_transaction` → критерий неверный. Отчёт: `T-CULL01_FINAL_VALIDATION_REPORT.md`.
- **Stage 4c — PASS:** причина 388 DC — `AtmosphereCamera` (origin, mask атмосферы) в union камер;
  фикс камер → реальный **P1 2 394 DC** (корабли 2 406), повтор 0.0 %, F8/F9 PASS. Отчёт:
  `T-CULL01_STAGE4C_REPORT.md`; **Stage 6 → 7 утверждены и следующие**.
- Kill: нет Hybrid Batch Group / DC не падает / створки «плывут» → откат.

### 6.6. H5c — actual CityShadowBudget gate + F9 (Stage 4c) — ✅ PASS
- Причина 2 811: `AtmosphereCamera` (origin, mask только атмосферы) удерживал тени 105 рендереров.
- Фикс: камера участвует только если рисует слой мира и дотягивается до города; `RefreshCameras` в `Build()`.
- **P1 2 394 / repeat 2 394 (0.0 %) / с кораблями 2 406**; F8/F9 PASS. Отчёт `T-CULL01_STAGE4C_REPORT.md`.

### 6.5. Stage 5 (B2) — ЗАКРЫТ
Dry-run: B2 **увеличивает** hybridDC (330 против 160 пар на пилоте); условие окклюзии 0 %.
**H4 не делать; Stage 5 закрыт.** **Stage 6/7 — утверждены, не опциональны.**

---

## 7. H6 — `VisibilityDirector` (Stage 6, 3–4 недели) — ✅ УТВЕРЖДЁН

> **Решение пользователя 01.11:** Stage 6 — фундамент будущего, **не опциональный**. Идёт после Stage 4c.
> **Brief:** [`T-CULL01_STAGE6_AGENT_BRIEF.md`](T-CULL01_STAGE6_AGENT_BRIEF.md); текущий baseline
> **P1 2 394 DC**, F8/F9 PASS; Stage 6 не должен ухудшить его.

### 7.1. Цель
Тонкий слой поверх engine-куллинга: группы/ячейки, мультикамерный union, бюджеты теней, корабли,
гистерезис, FO-safe, ≤0.3–0.5 мс (бюджет culling ≤2 мс).

### 7.2. Входы (absolute)
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DetailDistanceCuller.cs` — **13–107**.
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\PrimumLightPool.cs` — **33, 159–219**.
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\FloatingOrigin\Network\GlobalMotionControlledRebaseSlice.cs` — **265–418, 326, 1345, 1349–1354**.
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\FloatingOrigin\Network\GlobalMotionActorContract.cs` — **10–15**.
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ViewDistanceApplier.cs` — **75, 78, 82, 93, 111**.

### 7.3. Что сделать
1. Новый `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\VisibilityDirector.cs` (+ `CullingGroup`/группы).
2. Иерархия: мир → чанк → улица/дом → группа дверей; группы для кораблей/интерьеров/ламп.
3. Мультикамерный union: `MainCamera`-риг + `WorldCamera` (far 1e6) — только реальные; `AtmosphereCamera` (far 10) исключить.
4. `Renderer.enabled`/`forceRenderingOff`, `shadowCastingMode`, гистерезис 10–15 %, 2–4 Гц.
5. FO: живые Transform; после F8/F9 — пересчёт bounds/сброс кэшей (success **и** rollback); без мировых кэшей.
6. Не трогать `DoorController`, коллайдеры, `PrimumLightPool`, сеть/физику/VFX.

### 7.3. Приёмка
- **Foundation:** мультикамерный union работает; группы/FO-safe; существующие бюджеты не дублируются;
  нет артефактов; F8/F9 PASS.
- **Performance:** culling ≤2 мс; DC не хуже **2 394**; измеримый выигрыш — фиксировать, но «−60…−80 %»
  считать stretch-целью, не ломать визуал ради неё.
- Kill: >0.7 мс на MVP / артефакты → урезать гранулярность (сначала двери/руины/интерьеры).

### 7.4. Факт Stage 6 (H6) — ✅ PASS
- `VisibilityDirector`: 1 000 групп, union = `ThirdPersonCamera_0` (Atmosphere/парковочная MainCamera исключены),
  политика — `forceRenderingOff` < 2 px (гистерезис 12 %, 3 Гц) только для домов/ферм/спутников.
- P1 **2 378 / 2 372** (без директора 2 388–2 394), реальная игра 2 362–2 365, C3 −50; culling ≈1.3–1.7 мс,
  директор 0.02 мс/кадр; F8/F9 PASS; 0 errors. Отчёт `T-CULL01_STAGE6_REPORT.md`.
- Визуально принято пользователем. Открыто: pre-existing двойное владение тенями ферм, рефакторинг
  бюджетов на единый `WorldCameras` — только после согласования; Stage 7 — следующий.

---

## 8. H7 — HLOD/L1 + L2-импостор / PVS (Stage 7, 6–10 недель) — ✅ УТВЕРЖДЁН

> **Решение пользователя 01.11:** Stage 7 — фундамент дальнего мира/L2, **не опциональный**.
> **Brief:** [`T-CULL01_STAGE7_AGENT_BRIEF.md`](T-CULL01_STAGE7_AGENT_BRIEF.md); стартовый baseline
> **P1 2 362–2 378 DC**, L0 не ухудшать.

### 8.1. Цель
L1 (3–12 км) — 1 меш на блок/квартал; L2 (12–80 км) — 1–2 вызова; PVS — только если L0 <1k обязателен.

### 8.2. Входы (absolute)
- `C:\Projects\ProjectC_client\docs\world\primum_city\01_SETUP\04_LOD_AND_DISTANCE.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\ROADMAP.md` (Э6)
- `C:\Projects\ProjectC_client\docs\world\distantfocus\README.md`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\` (новый HLOD tool)
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\09_BEAM_COMPARISON_AND_DECISIONS.md` (PVS-вердикт).

### 8.3. Что сделать
- HLOD bake по блокам/чанкам в редакторе; LODGroup на дистанции; импостор/билборд L2.
- PVS — только если город заморожен; данные в **локальных координатах** ячеек; rebake-процесс.
- Приёмка: L1 ≤500 DC, L2 ≤10 DC; силуэт узнаваем; FO.

---

## 9. Простые задачи (не входят в этот тикет)

| Задача | Файл | Почему просто |
|---|---|---|
| Baseline-замеры | новые CSV в `08_DATA/` | без правок проекта |
| `ViewDistanceApplier` shadow no-op | `...\Scripts\World\ViewDistanceApplier.cs:75,82` | одна правка/решение |
| `Camera.main` в `DetailDistanceCuller` | `...\DetailDistanceCuller.cs:87` | замена на rig-камеру по эталону `PrimumLightPool` |
| Слой дверей / перенос 12 префабов | `TagManager.asset`, `Prefabs/City/Doors/*.prefab` | ручная/скриптовая операция, не алгоритм |
| **Фермы: двери на слой + тени off (Stage 2b)** | `Primum_farms_DOORS`, `Farms_Merged`, `Primum_farms` | настройки/слой, 1 день; не клеить |
| Настройки URP/BRG | ассеты | конфиг + пилот |
| Отчёт/индексы | docs | документация |

---

## 10. Absolute reference map (для копирования в задачу агенту)

```
PROJECT = C:\Projects\ProjectC_client

# Сцена/префабы
PROJECT\Assets\_Project\Scenes\World\WorldScene_0_0.unity
PROJECT\Assets\_Project\Prefabs\City\PRIMUM_CITY.prefab
PROJECT\Assets\_Project\Prefabs\City\Doors\*.prefab
PROJECT\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab
PROJECT\Assets\_Project\Prefabs\Ships\Ship_Light_root_fbx_test.prefab
PROJECT\Assets\_Project\Prefabs\Ships\Ship_medium_base.prefab
PROJECT\Assets\_Project\Art\Models\Primum\Rebased\**
PROJECT\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx
PROJECT\Assets\_Project\Generated\** (mesh-ассеты в git)

# Код
PROJECT\Assets\_Project\Editor\CombineMeshesToCollider.cs
PROJECT\Assets\_Project\Editor\BuildSettlementColliders.cs
PROJECT\Assets\_Project\Editor\ShipPresetCreator.cs
PROJECT\Assets\_Project\Editor\Primum\PrimumMeshRebase.cs
PROJECT\Assets\_Project\Editor\Primum\PrimumMaterialMap.cs
PROJECT\Assets\_Project\Editor\Primum\DoorPrefabBuilder.cs
PROJECT\Assets\_Project\Scripts\Player\ShipController.cs
PROJECT\Assets\_Project\Scripts\Ship\DoorController.cs
PROJECT\Assets\_Project\Scripts\Ship\ShipRootReference.cs
PROJECT\Assets\_Project\Scripts\Ship\ShipComponentLocator.cs
PROJECT\Assets\_Project\Scripts\Ship\ShipDeckNav.cs
PROJECT\Assets\_Project\Scripts\World\DetailDistanceCuller.cs
PROJECT\Assets\_Project\Scripts\World\PrimumLightPool.cs
PROJECT\Assets\_Project\Scripts\World\ViewDistanceApplier.cs
PROJECT\Assets\_Project\Scripts\World\FloatingOrigin\Network\GlobalMotionControlledRebaseSlice.cs
PROJECT\Assets\_Project\Scripts\World\FloatingOrigin\Network\GlobalMotionActorContract.cs

# Настройки
PROJECT\ProjectSettings\TagManager.asset
PROJECT\ProjectSettings\GraphicsSettings.asset
PROJECT\ProjectSettings\ProjectSettings.asset
PROJECT\Assets\_Project\Settings\ProjectC_URP.asset
PROJECT\Assets\_Project\Settings\ProjectC_URP_Renderer.asset
PROJECT\Packages\manifest.json
PROJECT\.gitignore

# Пакеты (URP 17.5, версия в точку)
PROJECT\Library\PackageCache\com.unity.render-pipelines.core@9fe9377312a8\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs
PROJECT\Library\PackageCache\com.unity.render-pipelines.universal@748d7458cc62\Runtime\UniversalRenderer.cs
PROJECT\Library\PackageCache\com.unity.render-pipelines.universal@748d7458cc62\Runtime\UniversalRenderPipeline.cs
PROJECT\Library\PackageCache\com.unity.render-pipelines.universal@748d7458cc62\Runtime\Data\UniversalRenderPipelineAsset.cs
PROJECT\Library\PackageCache\com.unity.render-pipelines.universal@748d7458cc62\Runtime\UniversalRendererRenderGraph.cs

# Документация/данные
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\README.md
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\09_BEAM_COMPARISON_AND_DECISIONS.md
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\07_SUBAGENT_REPORTS\H_BEAM_VS_OURS.md
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\07_SUBAGENT_REPORTS\I_SHIP_MERGE_AUDIT.md
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\07_SUBAGENT_REPORTS\J_CITY_FARMS_MERGE_RECIPE.md
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\I_ships_*.csv
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\J_*.csv
PROJECT\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\C_*.csv
```

---

## 11. Шаблон постановки агенту (копировать в задачу)

```
Ты — исполнитель этапа <H1|H2|...> тикета T-CULL01 проекта Project C.
Проект: C:\Projects\ProjectC_client. Базовый коммит: 6b49ac51.
Прочитай:
  C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md
  C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md
  <absolute paths из раздела H*>
Разрешено менять: <exact scope пакета>.
Запрещено: сцены вне scope, scene-overrides, static-флаги, Play Mode, push, правки чужих этапов.
Сделай: <действия из H*>.
Сдай: git diff --stat, отчёт с absolute путями, замеры до/после по gate, проверки F8/F9,
reimport, rollback-план. Если gate не выполнен — остановись и приложи замеры.
```

---

## 12. Чек-лист ревью после каждого H

- [ ] Изменены только файлы из scope; `Assets/`/`ProjectSettings` вне scope не тронуты.
- [ ] Нет новых scene-overrides; scene-diff минимален/пуст.
- [ ] Новые `.asset` в git; prefab/FBX не «закоммичены» мимо Lore.
- [ ] `git diff --stat` и список absolute paths в отчёте.
- [ ] Замеры: до/после, равный `frameCount`, far 30 км, 3 точки; gate выполнен.
- [ ] F8 → F9: 0 errors, объекты на месте, видимость не «залипла».
- [ ] Reimport исходного FBX/Apply prefab не ломает результат.
- [ ] Rollback выполнен/описан и проверен.
- [ ] `T-CULL01`/docs обновлены; нет open questions без owner.

---

## 13. Открытые вопросы (решить до соответствующего этапа)

1. **H1/H2:** доступен ли агент к Lore VCS для prefab/FBX; иначе правки только в локальном рабочем дереве.
2. **H2:** план фрактуры (8 ячеек) — нужен вход геймдизайна; без него P3 не начинать.
3. **H3:** визуальный допуск по теням дверей (можно ли off >40–80 м) — решение заказчика.
4. **H4:** можно ли менять `.gitignore`/включать `PRIMUM_CITY.prefab` в git — **спросить Lead/заказчика**.
5. **H5:** подтвердить, что Shader Graph 17.5 генерирует DOTS-варианты для 20 графов; иначе — ветка HLSL.
6. **H6:** включать ли `WorldCamera` (far 1e6) в union; нужен ли `ShipObservationCamera` (far 1e6).
7. **H7:** нужен ли L0 <1k (иначе PVS не оправдан).
