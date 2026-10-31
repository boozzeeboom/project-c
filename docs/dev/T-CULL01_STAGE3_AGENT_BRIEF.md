# T-CULL01 / Stage 3a — brief для отдельного агента (H2: только `Ship_havyII_base`)

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `79f26fdf` (Stage 2)
**Scope:** **только тяжёлый `Ship_havyII_base`**. `Ship_medium_base`, `Ship_Light_root_fbx_test`
и 20 NPC-заглушек **не трогать**; для них — future scale-out после зелёного пилота.
**Play Mode:** нужен для приёмки; запускать только с явным разрешением пользователя. Не сохранять сцену, не push.

**Прочитать до работы:**
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §3 (H2) и §0.
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` §5 (Stage 3).
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\I_ships_compartments.csv`,
  `I_ships_dc_model.csv`, `I_ships_prefabs.csv`.
- Stage 2 отчёт: `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE2_REPORT.md` (сцена/двери не ломать).

---

## 0. Контекст и предусловия

- H1 для heavy выполнен частично: **240/250** коллайдеров вынесены, **9 `DoorController` + 10 leaf-коллайдеров**
  остались в nested FBX; reimport проверен. H1-R1: после любого reimport FBX повторно прогнать
  `Project C → Ships → Repair Heavy Prefab`.
- Stage 2 (двери) завершён: P1 **27 131 → 6 094 DC**; слой `PRIMUM_DOORS` (10), `DoorVisibilityBudget`.
  **Сцена должна быть чистой (HEAD `52e4be48`)** — в Stage 2 были unintended 5 346 override `m_CastShadows: 0`;
  Lead откатил их. Перед работой проверить `git status` и переоткрыть `WorldScene_0_0`, если Unity держит старую копию.
- `Ship_havyII_base` по правке пользователя стоит **у города в сцене и должен быть на платформе**.
  Нужно подтвердить в Play Mode, что он стабилен (не падает), и снять **ship-baseline**:
  973 MR / ~2 919 DC upper (3 прохода), при Midday/far 30 000.
- Если корабль всё ещё нестабилен — **остановиться** и отчитаться (это R0.2, не H2).

---

## 1. Цель H2 (только HeavyII)

Доказать пайплайн оптимизации на одном тяжёлом корабле:

1. **P1 — greeble-мерж:** `Bolt_*`, `UnderHull_*`, `Interior_*`, `Contour_*`, `Mess_*`, `Det_*`
   (359 объектов, ~8 % тр.) → **973 → ~620 slot**.
2. **P2 — LODGroup:** LOD0 полный, LOD1 без bolt/mess/underhull, LOD2 1 меш / 3–5 материалов;
   пороги **300 / 900 / 2500 м**; LOD-меши — отдельные `.asset`.
3. **P3 (фрактура) — НЕ входит**: только после отдельного плана геймдизайна.

**Ожидание:** heavy **973 slot → ~620 (P1)**, DC ~**2 919 → ~1 800**; P2 — на дистанции **40/5 slot**.
**Другие корабли — 0 изменений.**

---

## 2. Абсолютные входы

- Префаб: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab`
- FBX: `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx`
- Данные: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\I_ships_compartments.csv`
  (936 объектов; merge/keep verdicts), `I_ships_dc_model.csv`, `I_ships_prefabs.csv`.
- Скрипты:
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\DoorController.cs` (9 дверей; класс 32)
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipRootReference.cs`, `ShipComponentLocator.cs`
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\ShipController.cs` (39–41, 100–106, 461–462)
- Референсы тулов:
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\ShipPresetCreator.cs` — 40, 526, 578–589
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\CombineMeshesToCollider.cs` — 31, 65–183, 114, **172** (`convex=false` — **не использовать как есть**)
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipPrefabRepair.cs` (H1; не ломать)
- Выход:
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipVisualMerger.cs`
  - mesh-ассеты: `C:\Projects\ProjectC_client\Assets\_Project\Generated\Ships\HeavyII_VIS_<Cell>_<Mat>.asset`
    (`.asset` в git; prefab/FBX — Lore)
  - отчёт/CSV: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\M_stage3_heavy_<date>.csv`
- Backup префаба вне Assets: `C:\Projects\ProjectC_client\_local_backups\T-CULL01\Stage3\Ship_havyII_base.prefab.bak`

---

## 3. Что клеить / что нельзя

**Клеить:** статик-визуал корпуса/интерьера/двигателей. В префабе **17 material assets** (10 корпусных
из FBX + двигатели/роторы/PDTT); группировать **по ассету материала, не по имени** (в FBX есть дубли имён).
P1 — только greeble-набор из §1.

**Нельзя:** 9 дверей + `Ramp_Cargo`, `PilotSeat`, 11 `ModuleSlot`, `CargoVisual`,
`EngineThrusterVisual`/`ShipPartShake`, `ContrailVFX`, 250 коллайдеров, будущие фрактуры;
не менять source FBX; не трогать сцену; не использовать scene-overrides; не делать runtime `CombineMeshes`.

---

## 4. Порядок работы

1. **Sanity:** `git status` чистый (кроме разрешённых файлов); сцена = HEAD; heavy на платформе;
   снять baseline (973 MR / DC / shadowCasters) при Midday, far 30 000.
2. **P1:** новый `ShipVisualMerger` — обход визуальных детей heavy prefab; whitelist интерактива;
   группировка по `(cell, material)`; `CombineInstance`; `mergeSubMeshes=false`;
   `indexFormat = UInt32` при >65 535 вершин; `RecalculateBounds`; сохранить UV/нормали;
   выгрузить `.asset`; пересобрать prefab-ассет, **не scene-overrides**.
3. **P2:** `LODGroup` на heavy; LOD0/1/2; отдельные `.asset`; пороги 300/900/2500 м.
4. **Проверка:** Frame Debugger на стабильном инстансе; двери/10 leaf/cargo/слоты/VFX целы;
   reimport FBX не ломает (H1-R1); F8→F9; **другие корабли 0 diff** (prefab-файлы medium/light/NPC не менялись).
5. **Коммит** только `.asset` + Editor-скрипт + CSV/отчёт: `T-CULL01/H2: heavy visual merge + LOD`; без push.

---

## 5. Приёмка

- [ ] heavy: **973 → ~620 slot** (P1); LODGroup с LOD1/LOD2; Frame Debugger подтверждает;
- [ ] heavy DC ≤~1 800 (P1), ≤~1 300 (P2) на стабильном инстансе;
- [ ] 9 дверей/10 leaf-коллайдеров, `PilotSeat`, 11 `ModuleSlot`, `CargoVisual`, VFX целы;
- [ ] reimport FBX не ломает; F8/F9: 0 errors, без «залипшей» видимости;
- [ ] сцена не сохранена; **medium/light/NPC: 0 изменений**; `git status` чистый;
- [ ] CSV/отчёт Lead: commit hash, до/после MR/slot/DC, Frame Debugger, reimport, F8/F9.

**Kill / stop:**
- корабль нестабилен/не на платформе → stop (R0.2);
- двери/слоты/VFX ломаются → rollback;
- reimport ломает prefab → stop;
- Σtris расходится >0.1 % → stop;
- пилот heavy не уложился в gate → future scale-out не начинать.

---

## 6. Жёсткие правила

1. Только HeavyII; другие корабли не трогать.
2. Не менять сцену, `TagManager`, URP/ProjectSettings; не сохранять Play Mode сцену.
3. Prefab/FBX не в git; backup вне Assets; в коммит — `.asset` + скрипт + CSV.
4. Не `SetActive`, не `Camera.main`, не менять `DoorController`/коллайдеры; мир FO-safe.
5. Не делать P3/фрактуру без отдельного плана.
