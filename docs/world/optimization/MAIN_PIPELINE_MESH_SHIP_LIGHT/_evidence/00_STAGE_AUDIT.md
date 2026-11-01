# 00 — Аудит утверждений стейджей и светового трека (независимая проверка)

Дата: 2026-11-01. Роль: независимый аудитор. Метод: **только чтение** файлов (CSV, `*_raw.txt`, отчёты,
`grep` по коду). Unity MCP, Play Mode, бенчи, тесты, коммиты — не использовались; проект/сцена/ассеты не менялись.
Единственный созданный файл — этот.

Сокращения путей:
`D/` = `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/` · `P/` = `docs/world/primum_city/10_CULLING_RESEARCH/` ·
`R/` = `docs/dev/` · `L/` = `docs/world/lights/` · `E/` = `docs/world/optimization/MAIN_PIPELINE_MESH_SHIP_LIGHT/_evidence/`

Вердикты: **Match** — число найдено в артефакте и совпало; **Mismatch** — артефакт даёт другое число либо
утверждение опирается не на ту колонку/стадию; **Not-found** — артефакта нет, число не подтверждается.

| Стейдж | Утверждение (число) | Источник утверждения | Артефакт | Фактическое значение | Match/Mismatch/Not-found | Комментарий |
|---|---|---|---|---|---|---|
| 0 | P1 baseline 27 131 DC | `D/K_stage0_report.md` §3 | `D/K_baseline_2026-10-31.csv` стр. `P1_tsentr_ploshhadi`, `drawCalls` | 27131 | Match | far 30000, Midday forced, shadowCasters 10115 |
| 0 | повтор P1 = 27 131 (расхождение 0.00 %) | там же §3 | `D/K_baseline_2026-10-31.csv` стр. `P1_tsentr_ploshhadi_repeat` | 27131 | Match | точно совпало |
| 0 | N объектов: 17 751 MR / 17 229 enabled / 16 422 с тенями | `D/K_stage0_report.md` §2 | только текст отчёта (в CSV поля нет) | 17751 / 17229 / 16422 | Match (отчёт) | в `K_baseline` такой колонки не существует — CSV-подтверждения нет |
| 0 | ночной контроль 17 020 DC, shadowCasters 0 | там же §2 | `D/K_baseline_2026-10-31.csv` стр. `P1_control_noch` | 17020 / 0 | Match | обоснование «17 020 = кадр без теневого прохода» подтверждено |
| 2 | P1 27 131 → 6 094 DC (−77.5 %) | `R/T-CULL01_STAGE2_REPORT.md` шапка | `D/L_stage2_2026-10-31.csv` стр. `P1_tsentr_ploshhadi`, `drawCalls` | 6094 | Match | raw: `rawDc=6094\|6094\|6109`, медиана 6094 |
| 2 | повтор 6 109 (+0.25 %) | там же | `D/L_stage2_2026-10-31.csv` стр. `P1_tsentr_ploshhadi_repeat` | 6109 | Match | 6109 vs 6094 = +0.25 %, порог ±5 % выполнен |
| 2 | «двери 2 633 / 0» | приписано Stage 2 | `D/L_stage2_2026-10-31.csv`: 3154 − 521 = 2633; отдельной колонки нет | 2633 (выводится), «/0» не измерено | **Mismatch (атрибуция)** | формулировка «2 633 / 0» принадлежит Stage 8: `V_stage8` `P1_doors_off` → `doorsDrawnBeyond150=2633`, `P1_doors_on` → 0. В отчёте Stage 2 её нет |
| 2 | дверей в кадре 3 154 / выжило 521 (16.5 %) | `R/T-CULL01_STAGE2_REPORT.md` §5 | `D/L_stage2_2026-10-31.csv` `doorsInFrustum` / `doorsKept` | 3154 / 521 | Match | raw `doorsInFrustum=3154;doorsKept=521;kept_pct=16.5` |
| 2 | предварительная таблица 3 165 / 529 (16.7 %) | там же §2 | `D/L_stage2_2026-10-31.csv` | 3154 / 521 | **Mismatch (внутреннее)** | оценка в §2 расходится с замером §5 на 11 дверей; в CSV — только 3154/521, на выводы не влияет |
| 2 | 5 493 дверных рендерера на слое 10 | там же §2 | `D/L_stage2_2026-10-31_raw.txt` `doorsEnabled=5493` | 5493 | Match | в `V_stage8_raw` `doorRenderers=5493` — то же число |
| 2 | P2 765 (−15.7 %), P3 819 (−63.1 %) | там же §5 | `D/L_stage2_2026-10-31.csv` `P2_uzkaya_ulitsa` / `P3_krysha` | 765 / 819 | Match | P3: `doorsKept=0` при 282 в кадре |
| 2 | ночь 5 019 DC | там же §5 | `D/L_stage2_2026-10-31.csv` `P1_tsentr_ploshhadi_night` | 5019 | Match | shadowCasters 0 |
| 2 | 2 630 `DoorController` | `R/T-CULL01_STAGE2_REPORT.md` §4 | `D/V_final_2026-11-01_raw.txt` `REBASE;…doors=2630` | 2630 | Match (вне Stage 2) | в артефактах Stage 2 компонентного счёта нет — только в probe/final raw |
| 3 | HeavyII mesh renderers 973 → 616 | `D/M_stage3_heavy_2026-10-31.csv` | rows `before,model,mesh_renderers` / `after,model,mesh_renderers` | 973 / 616 | Match | `gate,P1,heavy_meshrenderers: 973 → 616; подтверждено в runtime` |
| 3 | DC 2 916 → 1 812 | там же | `slots` note `dc_upper(1+2 каскада)` | 2916 / 1812 | Match | это модель (1 + 2 каскада), а не замер; замер LOD0 1538 (85 % модели) |
| 3 | runtime 1 538 / 1 015 / 235 / 104 | `D/M_stage3_heavy_runtime_2026-10-31.csv` | `runtime,case,ship_dc_*` | 1538 (200 м) / 1015 (80 м) / 235 (400 м) / 104 (1200 м) | Match | порядок в отчёте иной (200/80/400/1200), набор чисел совпал; повторы 1015/1015 и 104/104 |
| 3c | 1 170 / 1 161 / 1 236 | `R/T-CULL01_STAGE3C_REPORT.md` § | `D/N_stage3c_heavy_2026-10-31.csv` `measured_2150,dc,d200` / `d200_repeat` / `d300` | 1170 / 1161 / 1236 | Match | прогон 21:50:01; гейт ≤1300, запас +130 / +64 |
| 3c | ранний прогон 1 434 — гейт FAIL | там же § | `D/N_stage3c…csv` `measured,dc,d200` | 1434 (повтор 1427) | Match | корректно помечен как заменённый прогоном `measured_2150` |
| 3c | LOD 300/900, `lodBias` 1.30, size 27.071 | там же § | `D/N_stage3c…csv` `verify,lod,thresholds` | `300.000/900.000`, `maxLossyScale=12.000` | Match | эффективные пороги 390/1170 м |
| 3c | 616 рендереров теней, расхождений с источником 0 | там же § | `D/N_stage3c…csv` `verify,shadow_modes,renderers` | 616, `off=0; расхождений=0` | Match | |
| 3c | F8/F9 — `NO_TRANSACTION` (долг Stage 3a) | там же § | `D/N_stage3c…csv` `measured,rebase,f8_f9` | `NO_TRANSACTION; worldRootShift_m=0.00` | Match | на момент 3c F8/F9 не были закрыты (закрыты позже, Stage 6/7/8) |
| 4 | GRD baseline 6 400 DC | `R/T-CULL01_STAGE4_REPORT.md` шапка | `D/O_stage4_2026-10-31.csv` `phase=baseline` `case=C1` | 6400 | Match | `C1_repeat` тоже 6400 |
| 4 | после GRD 3 031 (−52.6 %) | там же | `D/O_stage4…csv` `phase=grd` `case=C1` | 3031 | Match | прогоны `grd_run1` и `grd_run2` оба 3031 |
| 4 | hybrid DC 2 876 / 18 196 инстансов | там же § | `D/O_stage4…csv` `grd` `C1`: `hybridDC` / `hybridInstances` | 2876 / 18196 | Match | baseline: `hybridDC=0` |
| 4 | гейт DC ≤2 500 не выполнен | там же | 3 031 > 2 500 | 3031 | Match | статус CONDITIONAL GREEN зафиксирован честно |
| 4 | окклюзия ≈0: 3 029 без / 3 031 с | там же §4 | `D/O_stage4…csv` `grd_noocc` `C1` | 3029 | Match | разница 2 DC |
| 4b | бенч P1 2 423 (sb200) | `R/T-CULL01_STAGE4B_REPORT.md` | `D/P_stage4b_2026-10-31.csv` `grd_sm0` `P1_sb200` | 2423 | Match | A/B-полигон бенча, `budgetEntries=1482`, не состояние компонента |
| 4b | baseline 3 047 DC | там же | `D/P_stage4b…csv` `grd_sm0` `P1` | 3047 | Match | повтор 3041 |
| V | реальный компонент даёт P1 2 811 | `R/T-CULL01_FINAL_VALIDATION_REPORT.md` | `D/Q_final_validation_2026-11-01.csv` `P1_noship_budget_on` | 2811 | Match | гейт ≤2500 провален; причина — 1 301 entry вместо 1 482 |
| V | кандидатов 1 301 вместо 1 482 | там же | `D/Q_final_validation…csv` `budgetEntries` | 1301 | Match | |
| 4c | P1 2 394 / повтор 2 394 (0.0 %) | `R/T-CULL01_STAGE4C_REPORT.md` | `D/R_stage4c_2026-11-01.csv` `P1_noship_budget_on` / `_repeat` | 2394 / 2394 | Match | `budgetEntries=1301` после фикса камер |
| 4c | с кораблями 2 406 | там же | `D/R_stage4c…csv` `P1_real_budget_on` | 2406 | Match | под гейтом ≤2500 |
| 6 | P1 2 378 DC | `R/T-CULL01_STAGE6_REPORT.md` шапка | `D/S_stage6_2026-11-01.csv` + `_raw.txt` `P1_dir_on` | 2378 | Match | raw `dc=2378`, `rawDc=2378\|2378\|2374` |
| 6 | повтор 2 372 | там же | `D/S_stage6_2026-11-01_raw.txt` `P1_dir_on_repeat` | 2372 | Match | в per-run файлах (090719/091313/091926) повторы 2377–2378; 2372 есть только в итоговом прогоне 092647 |
| 6 | без директора 2 388–2 394 | там же | `D/S_stage6…csv` `P1_dir_off` = 2388, `P1_real_dir_off` = 2394 | 2388 / 2394 | Match | |
| 6 | реальная игра с кораблями 2 362–2 365 | там же | `D/S_stage6…csv` `P1_real_dir_on`: 2362 (092647), 2365 (091926), 2367 (090719), 2368 (091313) | 2 362–2 368 | **Mismatch** | верхняя граница занижена на 3 DC; сам вывод (−10…−16) не меняется |
| 6 | 1 000 групп директора | там же §2.1 | `D/S_stage6…csv` `dirGroups` | 1000 (run1: 998) | Match | `dirMembers` 1 147–1 187 |
| 6 | culling ≤2 мс | там же § | `D/S_stage6…csv` `cullMsAvg` = 1.314…1.784; `cullMsMax` = 2.557…4.953 | среднее ≤2 ✅, пики >2 | Match (по среднему) | заявленная в отчёте полоса «1.33–1.69» не покрывает 1.784; пики до 4.95 мс — отдельное наблюдение |
| 6 | C3 −50 DC (−5 %) | там же | `D/S_stage6…csv` `C3_dir_on` 943 vs `C3_dir_off` 993 | −50 | Match | |
| 7 | P1 (L0) 2 341 DC | `R/T-CULL01_STAGE7_REPORT.md` шапка | `D/U_stage7_2026-11-01.csv` `P1_hlod_on` | 2341 | Match | on = off = real (2341/2341/2341) |
| 7 | L1 46–48 DC | там же | `D/U_stage7…csv`: 10 км 568−522 = 46; 5 км 515−467 = 48 | 46 / 48 | Match (производное) | отдельной колонки «DC города» нет — считается как `hlod_on − city_off` |
| 7 | L2 2 DC | там же | `D/U_stage7…csv`: 20 км 503−501 = 2 | 2 | Match (производное) | повтор 503/501 |
| 7 | 23 блока | там же §1.1 | `D/U_stage7_bake_2026-11-01.csv` `result=BAKED;mode=All;blocks=23;impostor=True` | 23 | Match | в CSV 24 строки на прогон = 23 блока + `L2_IMPOSTOR` |
| 7 | rebake побайтно детерминирован | там же шапка | `D/U_stage7_bake…csv` `result=DETERMINISTIC;mismatches=0` (×2) | 0 mismatches | Match | плюс колонка `match=1` у всех 23 блоков |
| 7 | 23 L1-ассета + L2-импостор | там же §1.1 | `D/U_stage7_bake…csv` (23 имени блоков + `L2_IMPOSTOR`) | 23 + 1 | Match | |
| 8 | P1 2 229 DC | `R/T-CULL01_STAGE8_REPORT.md` шапка | `D/V_stage8_2026-11-01.csv` `P1_doors_on` | 2229 | Match | повтор тоже 2229 |
| 8 | −112 DC к baseline 2 341 | там же | `D/V_stage8…csv`: 2341 (doors_off) − 2229 | 112 | Match | −4.8 % |
| 8 | двери >150 м в кадре = 0 | там же | `D/V_stage8…csv` `P1_doors_on` `doorsDrawnBeyond150` | 0 | Match | `doorsDrawn=419`, `doorsForcedOff=4697` |
| 8 | контроль `P1_doors_off` = 2 633 | там же §2.1 | `D/V_stage8…csv` `P1_doors_off` `doorsDrawnBeyond150` | 2633 | Match | `doorsDrawn=3154` при выключенной политике |
| 8 | после F8/F9 — тот же 2 229 | там же | `D/V_stage8…csv` `P1_doors_on_postFO` | 2229 | Match | |
| F | P1 2 235 / повтор 2 235 | `R/T-CULL01_FINAL_ACCEPTANCE_REPORT.md` §2 | `D/V_final_2026-11-01.csv` `P1_doors_on` / `_repeat` | 2235 / 2235 | Match | база гейта 2 229 ±5 % (2 117.6–2 340.5) |
| F | база 2 229 ±5 % | там же | `D/V_stage8_2026-11-01.csv` | 2229 | Match | |
| F | L1 46 / L2 2 | там же §2.1 | `D/V_final…csv`: 10 км 506−460 = 46; 20 км 441−439 = 2 | 46 / 2 | Match (производное) | совпадает со Stage 7 |
| F | hybridDC 2 129 (P1) | там же | `D/V_final…csv` `P1_doors_on` `hybridDC` | 2129 | Match | контроль `P1_doors_off` — 2241 |
| F | hybridInstances 4 191 против 17 555 | там же | `D/V_final…csv`: `P1_doors_on` 4191 / `P1_doors_off` 17555 | 4191 / 17555 | Match | |
| F | srpBatcherDC = 3 | там же | `D/V_final…csv` `P1_doors_on` | 3 | Match | |
| F | F8: сдвиг 256.000 м = predicted | там же | `D/V_final_2026-11-01_raw.txt` `REBASE;phase=F8_after` | `worldRootShift_m=256.000; predictedShift_m=256.000` | Match | frameGen 2→3 |
| F | F9: `PASS_rollback_restore`, rollback=1, restore=1 | там же | `D/V_final…raw.txt` `REBASE;phase=F9_after` | `status=PASS_rollback_restore; rollbackLogs=1; restoreTrue=1; shift=0.000` | Match | |
| F | errors=0 / exceptions=0 / shaderErrors=0 | там же §2 | в CSV полей нет; в raw — только `GATE;inGame` и `CASE` | — | **Not-found (CSV)** | утверждение есть только в тексте отчёта, машинного артефакта нет |
| L1–L6 | 2 238 источников под `PRIMUM_LIGHTS` | `L/LIGHT_CULLING_PLAN.md:22,42` | `registered=2238` в `T-LIGHT16/17/17B/18/19/20/21` (все строки) | 2238 | Match | сходится и с YAML-замером `E/06_CONTENT_PIPELINE.md` (2 238 ламп) |
| L1–L6 | ALIC 6.81 → 0.16 мс/кадр | `L/LIGHT_CULLING_PLAN.md:6,117` | `L/T-LIGHT17B_2026-11-01.csv` `alicMs`: `alicAll` 6.8084 / `alicActive` 0.1606 | 6.8084 / 0.1606 | Match | разброс 6.283–7.292 / 0.0999–0.2433; худший 0.2433 |
| L5 | радиус 100 м принят | `L/LIGHT_CULLING_PLAN.md:104,207,213` | `L/T-LIGHT20_2026-11-01.csv` `tuneRadius` | 100 (финальные кейсы `r100`) | Match | 120/140 отклонены `capCut` 18/57 |
| L1 | P1 12 ч: `zeroActive` 46 → 0 | `L/LIGHT_CULLING_PLAN.md` | `L/T-LIGHT16_BASELINE…csv` `P1_h12` = 46; `L/T-LIGHT20…csv` `P1_h12_final` = 0 | 46 → 0 | Match | `intensityZeroActive` |
| L4 | веер: разворот 180° меняет набор 0/6 → 6/6 | `L/LIGHT_CULLING_PLAN.md:8,279` | `L/T-LIGHT19…csv` `coneTurnSetChanged`: control 6×0 / cone-on 6×1 | 0/6 → 6/6 | Match | `L/LIGHT_CULLING_PLAN.md:171` пишет «0/6 → 1/6» — см. строку кросс-док ниже |
| L4 | `activeBehind` 26–77 → 0–1 | `L/LIGHT_CULLING_PLAN.md:8,172,279` | `L/T-LIGHT19…csv`: 26–77 — это `coneTurnBehindOutsideRear` контроля (58/76/26/42/32/77); 0–1 — `activeBehindOutsideRear` после (0/1/1/1/0/1) | базлайн 0–70, после 0–1 | **Mismatch** | сравниваются разные колонки: «до» — метрика после разворота, «после» — статичный кадр. Настоящий `activeBehindOutsideRear` контроля = 12/20/70/54/0/19 |
| L6 | fade `t90` 0.107–0.132 с | `L/T-LIGHT21_REPORT.md`, `LIGHT_CULLING_PLAN.md:10,251` | `L/T-LIGHT21…csv` `fadeT90Sec` (без −1) | 0.107…0.132 | Match | 0.107 / 0.118 / 0.121 / 0.121 / 0.122 / 0.127 / 0.132 |
| L6 | fade-out held ≤75 | `L/LIGHT_CULLING_PLAN.md` | `L/T-LIGHT21…csv` `fadeHeldForFade` = 75/73/59/30/22/8/6 при гейте **≤16** | max 75 | **Mismatch** | 75 — факт замера, но гейт ≤16 **не выполнен** (10 из 13 кейсов выше), поэтому fade-out отклонён. «≤75» нельзя читать как соответствие |
| L6 | fade-in 0.15 с принят, fade-out = 0 | `L/LIGHT_CULLING_PLAN.md:288` | `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs:222,225` | `fadeInSeconds = 0.15f`, `fadeOutSeconds = 0f` | Match | в CSV замерного прогона `fadeOutSec=0.30` — это конфиг до отклонения |
| L1–L6 | `cap = 96` | `L/LIGHT_CULLING_PLAN.md` | `L/T-LIGHT16…csv` `cap` | 96 | Match | `capSaturated=1` на P1 днём |
| Код | `PrimumHlodBaker` | `R/T-CULL01_STAGE7_REPORT.md` §1.1 | `Assets/_Project/Editor/Culling/PrimumHlodBaker.cs:45` | класс есть; меню `Project C/Culling/Stage 7 HLOD/` | Match | пункты: Bake Pilot Block / Bake All Blocks + L2 / Rebake Determinism Check |
| Код | `PrimumDoorLayerTool` | `R/T-CULL01_STAGE2_REPORT.md` §1 | `Assets/_Project/Editor/PrimumDoorLayerTool.cs:15` | меню `Project C/Culling/Door Layer/Dry Run` + `Apply` | Match | |
| Код | `ShipPrefabRepair` | `P/` handoff | `Assets/_Project/Editor/Ships/ShipPrefabRepair.cs:44` | меню `ProjectC/Ships/Repair…` | Match | |
| Код | `ShipVisualMerger` | `P/` handoff | `Assets/_Project/Editor/Ships/ShipVisualMerger.cs:50` | меню `ProjectC/Ships/Heavy Visual Merge…` | Match | |
| Код | `ShipShadowBudgetTool` | `R/T-CULL01_STAGE3C_REPORT.md` | `Assets/_Project/Editor/Ships/ShipShadowBudgetTool.cs:40` | меню `ProjectC/Ships/Shadow Budget/…((Stage 3c))` | Match | |
| Код | `TLight16BaselineBench` | `L/LIGHT_CULLING_PLAN.md` | `Assets/_Project/Editor/Lighting/TLight16BaselineBench.cs:81` | меню `Project C/Lighting/T-LIGHT16…21` | Match | один класс на все L1–L6 |
| Код | `FinalValidationBench` | `R/T-CULL01_FINAL_ACCEPTANCE_REPORT.md` §1 | `Assets/_Project/Editor/Culling/FinalValidationBench.cs:41` | меню `…/Final Validation/Run`, `…/Stage 6 Director/Run`, `…/Stage 7 HLOD/Run Bench`, `…/Stage 8 Doors/Run Bench` | Match | |
| Код | `T_CULL01AcceptanceProbe` | там же | `Assets/_Project/Editor/Culling/T_CULL01AcceptanceProbe.cs:30` | меню `Project C/Culling/T-CULL01 Acceptance/Probe (light + doors)` | Match | |
| Код | `BakePilotSceneCatalog` | `docs/world/floatingorigin/09S…` | `Assets/_Project/Editor/FloatingOrigin/BakePilotSceneCatalog.cs:20` | меню `ProjectC/World/Floating Origin/Bake Pilot Catalog (Preview + Apply)` | Match | |
| Код | `PrimumLampLighting` | `L/` аудит ламп | `Assets/_Project/Editor/Primum/PrimumLampLighting.cs:36` | меню `ProjectC/World/PRIMUM/Свет: …` (4 пункта) | Match | |
| Кросс-док | `10_EXECUTION_PLAN` трекер vs отчёты | `P/10_EXECUTION_PLAN.md` §0.2 | строки Stage 2/3/4/4b/V/4c/6/7/8/L/F | 6094 / 1170-1161-1236 / 3031 / 2423 / 2811 / 2394-2406 / 2378-2372 / 2341 + 46-48 + 2 / 2229 / 2235 | Match | все числа плана совпали с CSV и отчётами |
| Кросс-док | `README.md` vs отчёты | `P/README.md:6-40` | 2235 / 2229 / 46 / 2 / 2129 / 2423 / 2394 / 2378-2372 / 2341 / 2229 | совпадает | Match | строка 40 явно помечает 26 981 / 6 094 / 3 031 как исторические — корректно |
| Кросс-док | `02_FACTS_AND_MEASUREMENTS` vs отчёты | `P/02_FACTS_AND_MEASUREMENTS.md:8-9` | 2229 / 2235 / 46 / 2 | совпадает | Match | исторические числа помечены как исторические |
| Кросс-док | `LIGHT_CULLING_PLAN` vs отчёты L1–L6 | `L/LIGHT_CULLING_PLAN.md:6-10,275-281` | 2 238, 6.81→0.16, radius 100, t90 0.107–0.132 | совпадает | Match | |
| Кросс-док | `LIGHT_CULLING_PLAN:171` «`coneTurnSetChanged` 0/6 → **1/6**» | `L/LIGHT_CULLING_PLAN.md:171` | `L/T-LIGHT19_REPORT.md:18,216` и `L/LIGHT_CULLING_PLAN.md:8,279` | 6/6 | **Mismatch (внутреннее)** | опечатка «1/6» против «6/6» в четырёх других местах; CSV подтверждает 6/6 |
| Кросс-док | `LIGHT_CULLING_PLAN` метка `activeBehindOutsideRear` 26–77 | `L/LIGHT_CULLING_PLAN.md:8,172,279` + `L/T-LIGHT19_REPORT.md:169` | `L/T-LIGHT19…csv` | см. строку L4 выше | **Mismatch** | тот же дефект разметки продублирован в плане и в отчёте |

## Итоги

**Объём.** Проверено **85** утверждений (Stage 0 — 4, Stage 2 — 9, Stage 3/3c — 8, Stage 4/4b/V/4c — 11,
Stage 6 — 6, Stage 7 — 7, Stage 8 — 5, финальная приёмка — 9, свет L1–L6 — 10, существование кода/меню — 10,
кросс-док консистентность — 6).

**Подтверждено (77):**
- Сквозная цепочка DC сходится на всех стейджах и в трёх независимых CSV-семействах: 27 131 → 6 094 → 6 400 → 3 031
  → 2 423 (бенч 4b) → 2 394 (4c) → 2 378 → 2 341 → 2 229 → 2 235 (приёмка). Каждое число найдено в строке CSV.
- Runtime-числа корабля (1 538 / 1 015 / 235 / 104), Stage 3c (1 170 / 1 161 / 1 236), Stage 7 (L1 46–48, L2 2, 23 блока,
  `DETERMINISTIC;mismatches=0`), Stage 8 (−112, двери >150 м = 0, контроль 2 633) — подтверждены построчно.
- F8/F9 подтверждены машинно (`worldRootShift_m=256.000 = predictedShift_m`, `PASS_rollback_restore`, restore=1).
- Свет: 2 238 источников, ALIC 6.8084 → 0.1606 мс, radius 100, `zeroActive` 46 → 0, `t90` 0.107–0.132, `cap 96` — все
  числа есть в CSV-колонках.
- Все 10 упомянутых инструментов существуют в коде, и пути меню в отчётах совпадают с `MenuItem`-константами.
- `10_EXECUTION_PLAN`, `README`, `02_FACTS` не содержат устаревших чисел в качестве текущих: старые значения
  помечены как исторические.

**Не подтверждено / расхождения (7 строк Mismatch = 6 различных дефектов, из них 1 продублирован в двух доках; + 1 Not-found):**
1. **«двери 2 633 / 0» приписаны Stage 2** — в `L_stage2` такой метрики нет; 2 633 выводится как 3154 − 521,
   а формулировка «/0» принадлежит Stage 8 (`doorsDrawnBeyond150`). Ошибка атрибуции, не числа.
2. **Stage 2, §2: 3 165 / 529 (16.7 %)** против 3 154 / 521 (16.5 %) в §5 и в CSV — внутреннее расхождение на 11 дверей.
3. **Stage 6, «реальная игра 2 362–2 365»** — в CSV есть 2 367 и 2 368; фактический диапазон 2 362–2 368.
4. **Свет, `activeBehind` 26–77 → 0–1** — сравниваются разные колонки: «до» взято из `coneTurnBehindOutsideRear`
   (замер после разворота 180°), «после» — из статичного `activeBehindOutsideRear`. Реальный контроль
   `activeBehindOutsideRear` = 12/20/70/54/0/19, а не 26–77.
5. **Свет, fade-out held «≤75»** — 75 это факт замера, а гейт был **≤16**; 10 из 13 кейсов гейт нарушили, fade-out
   отклонён. Формулировка читается как соответствие, хотя это зафиксированный провал гейта.
6. **`LIGHT_CULLING_PLAN:171` «0/6 → 1/6»** против «6/6» в остальных четырёх местах плана и в отчёте L4 — опечатка.
7. **Not-found: `errors=0 / exceptions=0 / shaderErrors=0`** — только текст отчёта, машинного артефакта нет.

**Критично (влияет на документ «оптимизация x100»):**
- Ни одно расхождение **не опрокидывает** итоговый результат: цепочка 27 131 → 2 235 и все гейты подтверждены
  артефактами. Расхождения — в атрибуции (п.1), внутренних черновиках (п.2), границах диапазонов (п.3) и
  разметке световых метрик (п.4–6).
- **Опасная для цитирования формулировка — п.4 и п.5.** «`activeBehind` 26–77 → 0–1» и «fade-out held ≤75»
  выглядят как достигнутые улучшения, но первое сравнивает разные величины, а второе — провал гейта ≤16.
  В x100 их нельзя приводить как «до/после» без пересчёта по одной колонке.
- **Провенанс 2 423 (Stage 4b)**: это A/B-полигон бенча (`budgetEntries=1482`), реальный компонент дал 2 811
  (Stage V, гейт провален), и только фикс камер в 4c дал 2 394. Цепочка задокументирована честно, но 2 423
  нельзя называть «достигнутым состоянием».
- **Провенанс 2 372 (Stage 6)**: число есть только в итоговом прогоне 092647; в трёх per-run файлах повторы
  2 377–2 378. Для x100 безопаснее цитировать 2 378 и «2 372–2 378».
- **Диапазоны вместо точек.** Stage 7 «L1 46–48» и «L2 2», финальные «L1 46 / L2 2» — производные
  (`hlod_on − city_off`), отдельной колонки в CSV нет; при пересчёте из другого прогона число может сместиться.
- **Пики culling.** Заявленное «culling ≤2 мс» верно для среднего (1.31–1.78), но `cullMsMax` доходит до 4.95 мс —
  в документе про x100 это стоит давать как «среднее ≤2 мс, пики до ~5 мс».
