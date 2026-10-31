# 10. План исполнения culling/visibility (Project C)

**Дата:** 2026-10-31 · **Основание:** `10_CULLING_RESEARCH` (A–J), коммит `6b49ac51`
**Статус:** Stage 0 принят **с оговорками** 2026-10-31 (`T-CULL01/0`); Stage 1 — готов к передаче,
Stage 2 можно вести параллельно. **Тикет сложного кода:** `T-CULL01` —
[`docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md`](../../../dev/T-CULL01_COMPLEX_CODE_HANDOFF.md).
**Правило:** сложные куски кода не делает Lead; они выносятся в `T-CULL01` и передаются отдельному
агенту по одному этапу, когда до этапа дошли. Простые правки — отдельными маленькими коммитами.

---

## 0.1. TL;DR для заказчика

1. План — **8 этапов** (Stage 0–7). Первые четыре дают основной эффект за ~4–6 недель.
2. **Stage 0** — baseline + два фикса; **Stage 1** — корабли P0 (0.5–1 день); **Stage 2** — двери
   (2–4 дня) + бюджет теней; **Stage 2b** — фермы (1 день); **Stage 3** — мерж/LOD кораблей (3–5 дней).
3. **Stage 4** — GRD + GPU occlusion (2–3 недели); **Stage 5** — переклейка города B2 (2–4 недели,
   только при зелёном GRD); **Stage 6/7** — VisibilityDirector и HLOD/PVS (опционально).
4. Сложный код — **7 пакетов H1–H7** в [`T-CULL01`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md);
   простые правки (сцена/слой/настройки/двери) — отдельными коммитами по этому плану.
5. Каждый этап заканчивается замером по gate и **маленьким коммитом**; без gate следующий этап не
   начинается. Play Mode/скриншоты — только пользователь.

### 0.2. Трекер этапов

| Stage | Статус | Зависит от | Gate (кратко) | Owner | Артефакт |
|---|---|---|---|---|---|
| 0 | 🟡 done with caveats (R0.1–R0.3) | — | baseline CSV ±5 %, фиксы, F8/F9 | Lead/пользователь | `08_DATA/K_baseline_2026-10-31.csv`, `K_stage0_report.md` |
| 1 | 🟡 ready for handoff | Stage 0 | 250 боксов/9 дверей после reimport; 0 missing; scene-diff пуст | агент T-CULL01/H1 | prefab + `H1_prefab_repair.csv` |
| 2 | ⬜ planned | Stage 0 | 26 981 → 5–9k DC; F8/F9; raycast/физика ок | Lead/H3 | слой + 2 скрипта |
| 2b | ⬜ planned | Stage 2 (слой) | −270 дверей ферм/≈−640 слотов; тени off | Lead/пользователь | настройки/слой |
| 3 | ⬜ planned | Stage 1 | корабли ≤2 500 DC (P1/P2); 973→~620; двери работают | агент T-CULL01/H2 | `.asset` + LODGroup |
| 4 | ⬜ planned | Stage 2 | DC ≤2 500; Hybrid Batch Group; 0 shader errors; FO | агент T-CULL01/H5 | настройки + шейдеры |
| 5 | ⬜ planned | Stage 4 зелёный + окклюзия ≥35 % | цвет ±10 %, тени ≤+10 %, Σtris ±0.1 %, DC не растёт | агент T-CULL01/H4 | rebatch-тул + prefab-ассеты |
| 6 | ⬜ optional | Stage 4/5 | culling ≤2 мс; −60…−80 % DC без окклюзии | агент T-CULL01/H6 | VisibilityDirector |
| 7 | ⬜ optional | Stage 5/6 | L1 ≤500, L2 ≤10; FO | агент T-CULL01/H7 | HLOD/impostor/PVS |

Статусы обновлять здесь после каждого gate; коммит этапа — с префиксом `T-CULL01/<H>`.

**Stage 0 (2026-10-31):** baseline — `08_DATA/K_baseline_2026-10-31.csv`, разбор — `08_DATA/K_stage0_report.md`.
Baseline зафиксирован как **Midday + тени URP 1000/2**: в фазе `Night (Noch)`/`Twilight (Sumerki)` главный свет
выключен и теневого прохода нет, тот же кадр даёт 17 020 DC вместо 27 131. Оба бага (`Camera.main` в куллере,
no-op `QualitySettings.shadowDistance`) исправлены. F8/F9 прошли без фактического сдвига мира (фокус уже в кванте),
поэтому реальный ребейз за порогом `256 м` остаётся открытым — см. `K_stage0_report.md` §6.

---

## 0. Что считаем результатом

Цель — стабильные **60 fps** в L0-городе и порту, без переделки геймплея/сети/FO:

| Метрика | Сейчас | Цель после этапов |
|---|---|---|
| Draw calls, конфиг A (shadow 1000/2) | 26 981 | **≤ 2 500–3 000** |
| Draw calls, конфиг B (shadow 250/1) | 21 118 | ≤ 2 000–2 500 |
| Двери в кадре | до ~17 766 DC | сотни DC |
| Корабли (все 23) | ~4 917 DC (модель) | **≤ 480 DC** |
| Город (не двери) | 2 292 / 1 647 DC (модель J) | не хуже +10 % |
| Culling CPU | не измерено | **≤ 2 мс** |
| FO F8/F9 | работает | 0 errors, без сдвига/pop |
| Визуал | — | без заметных pop-in; тени/двери/эмиссия по согласованию |

Бюджет «L0 ≤ 120 DC» **отменён**: это бюджет L1/L2. L1 — ≤500, L2 — ≤10.

---

## 1. Порядок этапов (сверху вниз, не перескакивать)

```
Stage 0  Baseline + фиксы (2–3 дня)                     — простой код/ручные проверки
Stage 1  P0 корабли: вынос коллайдеров/дверей (0.5–1 день) — СЛОЖНЫЙ КОД → T-CULL01/H1
Stage 2  Ф0 двери + бюджет теней (3–5 дней)              — средний код → T-CULL01/H3
Stage 2b Фермы: двери на слой, тени off (1 день)          — простые настройки
Stage 3  Корабли P1/P2: greeble-мерж + LOD (3–5 дней)    — СЛОЖНЫЙ КОД → T-CULL01/H2
Stage 4  GRD + GPU occlusion (2–3 недели)                — СЛОЖНЫЙ КОД/ШЕЙДЕРЫ → T-CULL01/H5
Stage 5  Воспроизводимость мержа + B2 города (2–4 нед)   — СЛОЖНЫЙ КОД → T-CULL01/H4
Stage 6  VisibilityDirector (3–4 недели, опционально)    — СЛОЖНЫЙ КОД → T-CULL01/H6
Stage 7  HLOD/L1 + L2-импостор / PVS (опционально)       — СЛОЖНЫЙ КОД → T-CULL01/H7
```

Stage 0–3 можно частично параллелить: Stage 1 не зависит от Stage 2; Stage 3 — после Stage 1.
Stage 2b — после Stage 2 (тот же слой дверей). Stage 4 — после Stage 2 (нужны измерения и шиммер-настройки).
Stage 5 — только после зелёного Stage 4. Stage 6/7 — по необходимости.

---

## 2. Stage 0 — Baseline + фиксы (2–3 дня)

**Brief для отдельного агента (Play Mode разрешён пользователем):**
[`T-CULL01_STAGE0_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE0_AGENT_BRIEF.md).

**Цель:** иметь сравнимые «до/после» и убрать два известных бага.

### 0.1. Методика и точки
- Камера: геймплейный риг, **far 30 км** (не 1000), 3 точки: центр площади / узкая улица / крыша;
  плюс 1 точка у порта (корабли).
- `UnityStats` (DC/SetPass/tris), Frame Debugger, Profiler; равный `frameCount`; FPS после ~1500 кадров.
- `AtmosphereCamera` (меню, far 10) — вне замера; `Camera.main` не использовать.
- FO-регресс: F8 → F9, 0 errors, без сдвига.

**Артефакт:** `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/K_baseline_<дата>.csv`
(колонки: сцена, точка, far, shadow, DC, SetPass, tris, FPS, frameCount).

### 0.2. Фиксы (простые, отдельный маленький коммит)
1. **`ViewDistanceApplier` shadowDistance — no-op.** URP читает `UniversalRenderPipelineAsset.shadowDistance`,
   а не `QualitySettings`. Файл:
   [`C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ViewDistanceApplier.cs`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scripts/World/ViewDistanceApplier.cs) — строки **75, 82**.
   Решение: перенести shadow-пресет в URP-ассет (правка `ProjectC_URP.asset` — согласовать) **или**
   убрать из `ViewDistanceConfig` и управлять через `shadowCastingMode`-бюджет.
2. **`Camera.main` в `DetailDistanceCuller`.** Файл:
   [`C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DetailDistanceCuller.cs`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scripts/World/DetailDistanceCuller.cs) — строка **87**.
   В проекте **две** камеры с тегом `MainCamera`; `Camera.main` может вернуть статичную в 240 км.
   Эталон выбора камеры — `PrimumLightPool`:
   [`C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\PrimumLightPool.cs`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scripts/World/PrimumLightPool.cs) — строки **33, 159–219**.
3. **`AtmosphereCamera` (far 10)** не включать в union/камеры куллинга — это меню-камера.
4. Проверить `GraphicsSettings`: `m_CameraRelativeLightCulling/ShadowCulling` уже 1/1
   ([`C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset`](file:///C:/Projects/ProjectC_client/ProjectSettings/GraphicsSettings.asset) — строки **58, 79–80**).

**Gate Stage 0:** baseline CSV заполнен; повторный замер ±5 %; оба бага воспроизведены/исправлены;
F8/F9 зелёный.

### 2.4. Stage 0 review (Lead, 2026-10-31)

**Принято:**
- P1 baseline **27 131 DC** против ориентира 26 981 (**+0.56 %**), повтор P1 — **0.00 %**;
  P1 без городских дверей **4 607** против 4 954 (**−7.0 %**, в допуске ±10 %).
- Зафиксировано условие: **Midday forced + URP 1000/2**; ночью/в сумерках главный свет выключен,
  теневого прохода нет: тот же кадр **17 020 DC** (−37 %). Будущие замеры — только при Midday
  (или явно подписывать фазу).
- `DetailDistanceCuller` больше не использует `Camera.main`; проверено: в центре хэмлетов
  21/496 руин включены, при камере в 300 км — 0/496.
- `ViewDistanceApplier` больше не присваивает `QualitySettings.shadowDistance`; поле в конфиге
  помечено устаревшим; URP-ассет не менялся.

**Открытые пункты (не блокируют Stage 1–2, блокируют Stage 3/5):**

| ID | Что | Почему важно | Как закрыть |
|---|---|---|---|
| **R0.1** | F8/F9 прошли **без фактического сдвига** (фокус уже в кванте) | реальный ребейз за 256 м не проверен; нужен перед Stage 5/6 | вывести фокус за порог и повторить F8→F9; 0 errors, без сдвига |
| **R0.2** | Корабли **не у порта**: тяжёлый `Ship_havyII_base` (976 MR) на `(79953, -34884, 79367)`, Y менялся на ~17 км — падает; P4 = порт без кораблей | **блокирует baseline/приёмку Stage 3 (H2)**; H1 можно делать | отдельная задача: стабилизировать/заспавнить корабль, снять ship-baseline (DC/MR) |
| **R0.3** | SetPass **316 vs 232** (+36 % при совпавших DC); инвентарь «17 751 MR» расходится с live 8 891 | не влияет на P1 DC, но нельзя публиковать как точный инвентарь | Frame Debugger/повторный подсчёт сцены; зафиксировать методику |
| **R0.4** | Baseline снят **до** фикса куллера; P1 как контроль сохраняется | сравнение Stage 2 делать на пост-фикс P1 27 131 | использовать P1 27 131 как новый контроль; руины — отдельно |

**Вердикт:** Stage 0 — **частичный pass**: baseline и оба фикса приняты; Stage 1/2 можно начинать;
Stage 3 (H2) — только после R0.2; Stage 5/6 — после R0.1.

**Brief для Stage 1:** [`T-CULL01_STAGE1_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE1_AGENT_BRIEF.md).

---

## 3. Stage 1 — P0 корабли: вынос коллайдеров/дверей (0.5–1 день)

**Цель:** убрать хрупкость: **238 из 250 BoxCollider** и **9 `DoorController`** лежат внутри
инстанса FBX. Реимпорт модели их сносит. Это **не** мерж визуала — только перестройка префаба.

**Сложный код:** `T-CULL01 / H1` (`ShipPrefabRepair`).
**Файлы:**
- Префаб: [`C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab`](file:///C:/Projects/ProjectC_client/Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab)
- FBX: [`C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx`](file:///C:/Projects/ProjectC_client/Assets/_Project/Art/Models/Ships/HeavyII_base/project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx)
- Скрипты: `ShipController.cs` (39–41, 100–106, 461–462), `DoorController.cs` (30–32),
  `ShipRootReference.cs`, `ShipComponentLocator.cs`, `ShipDeckNav.cs` (26–30, 62, 67).
- Тул-референс: `BuildSettlementColliders.cs` (shipMode: 34–35, 89, 160, 268–269, 1438) и
  `ShipPresetCreator.cs` (40, 43, 526, 578–589).

**Действия:** вынести 250 боксов и 9 дверей в обычные дети корня префаба; узлы FBX не трогать;
сохранить локальные трансформы; двери/коллайдеры — вне инстанса FBX; проверить `DoorController`,
`ShipRootReference`, `ModuleSlot`, `PilotSeat`, `CargoVisual`, `ContrailVFX`.

**Gate:** реимпорт FBX не теряет коллайдеры/двери; `DoorController` работает; 0 missing refs;
scene-diff ≤ нескольких строк (правки только в префабе); F8/F9 зелёный.

---

## 4. Stage 2 — Ф0 двери + бюджет теней (3–5 дней)

**Цель:** снять 84 % DC без переделки геометрии.

### 2.1. Слой дверей
- Создать слой `PRIMUM_DOORS` (свободен, слот **10**; см. `TagManager.asset`).
- Перенести 11 дверных префабов (`PM_DOOR_*`, `PM_ADM_ENTRANCE_PORTAL`) на слой; инстансы наследуют.
- `Camera.layerCullDistances[10] = 120–150 м`, `layerCullSpherical = false`.
- **`layerCullDistances` не влияет на тени** — отдельный `shadowCastingMode`-бюджет.

### 2.2. Бюджет теней/видимости (средний код → T-CULL01/H3)
- `DoorVisibilityBudget` + `ShadowBudget`: группы, гистерезис 10–15 %, 2–4 Гц, мультикамерный union,
  только `Renderer.enabled`/`shadowCastingMode`, без `SetActive`.
- Файлы-референсы: `DetailDistanceCuller.cs` (гистерезис, 0.5 с), `PrimumLightPool.cs` (выбор камеры),
  `GlobalMotionControlledRebaseSlice.cs` (FO, `TryRebuild`).

**Gate:** 26 981 → **5 000–9 000 DC**; двери в кадре — сотни; тени по согласованному визуалу;
F8/F9 зелёный; 0 errors.

---

## 4.1. Stage 2b — Фермы: двери + тени (1 день, простые настройки)

**Цель:** убрать 270 дверных рендереров ферм из города и тени 1.27 млн тр.

- `Primum_farms_DOORS` (101 инстанс / **270 MR**) → слой `PRIMUM_DOORS` (тот же, что двери города);
  дистанция дверей 120–150 м.
- `shadowCastingMode = Off` геометрии ферм (`Primum_farms`: 142 MR / 150 slots / 99 ассетов / 116.6 МБ)
  — фермы в 963–1 051 м, их тени не читаются; **не клеить**: гранулярность уже правильная.
- Свет: 151 `Light` вне пула — проверить, что фермы не попадают в `PrimumLightPool` (≤96).
- Коллайдеры/3 mesh collider не трогать; HLOD L1 — позже (Stage 7).
**Gate:** −270 рендереров/≈−640 слотов из города, −1.27 млн тр в shadow pass; визуал ворот/теней ок;
F8/F9 зелёный.

---

## 5. Stage 3 — Корабли P1/P2: greeble-мерж + LOD (3–5 дней)

**Цель:** тяжёлый 973 MR → ~620 (P1), затем LOD; все корабли ~4 917 → ≤2 500 DC.

**Сложный код:** `T-CULL01 / H2` (`ShipVisualMerger` + LODGroup builder).
**Файлы:**
- Heavy prefab (abs), light/medium prefabs (abs), FBX heavy (936 объектов, 10 материалов корпуса;
  в префабе **17 material assets** — группировать по ассету, не по имени).
- Выход: `C:\Projects\ProjectC_client\Assets\_Project\Generated\Ships\<Class>_VIS_<Cell>_<Mat>.asset`
  (новый каталог; `.asset` **в git**, prefab/FBX — Lore).
- Референсы: `ShipPresetCreator.cs` (40, 526, 578–589), `CombineMeshesToCollider.cs`
  (31, 65–183, 114, 172 — **не использовать как есть**: `convex=false` и один меш).
- Нельзя клеить: 9 дверей, аппарель, `PilotSeat`, 11 `ModuleSlot`, `CargoVisual`,
  `EngineThrusterVisual`/`ShipPartShake`, `ContrailVFX`, 250 коллайдеров, будущие фрактуры.

**Действия:** P1 — только greeble (`Bolt_*`, `UnderHull_*`, `Interior_*`, `Contour_*`, `Mess_*`,
`Det_*`: 359 объектов) в 10 сабмешей; P2 — `LODGroup` (300/900/2500 м), LOD1/LOD2.
P3 (позже, отдельно) — «8 ячеек разрушения × материал» → ~92 slot.

**Gate:** Frame Debugger на одном тяжёлом инстансе (40064, 2512, 39449); reimport не ломает;
двери/слоты работают; FO/сеть ок; DC кораблей ≤2 500 (P1/P2).

---

## 6. Stage 4 — GRD + GPU occlusion (2–3 недели)

**Цель:** выйти на 1 500–2 500 DC, включить Hi-Z.

**Сложный код/шейдеры:** `T-CULL01 / H5`.
**Файлы:**
- [`C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP.asset`](file:///C:/Projects/ProjectC_client/Assets/_Project/Settings/ProjectC_URP.asset)
  (строки 58–59, 73, 86–88) и `ProjectC_URP_Renderer.asset` (189–190).
- [`C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset`](file:///C:/Projects/ProjectC_client/ProjectSettings/GraphicsSettings.asset) — строка **58**.
- Shader Graph: 15 графов города + 5 дверей; список — `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\C_city_materials.csv`
  и `C_shaders.csv`; папка `C:\Projects\ProjectC_client\Assets\_Project\Materials\**`.
- Package-источники URP 17.5:
  `C:\Projects\ProjectC_client\Library\PackageCache\com.unity.render-pipelines.core@9fe9377312a8\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs`
  (18–19, 60),
  `...\universal@748d7458cc62\Runtime\UniversalRenderer.cs` (11–23),
  `...\UniversalRenderPipeline.cs` (1700, 1753),
  `...\UniversalRenderPipelineAsset.cs` (528, 1465–1468),
  `...\UniversalRendererRenderGraph.cs` (974–980, 1515–1524).

**Действия:** `BrgStripping=2 (KeepAll)`, `GPUResidentDrawerMode=InstancedDrawing`,
`GPUResidentDrawerEnableOcclusionCullingInCameras=1`; DOTS-варианты в ~20 графах; пилот на одном
чанке → вся сцена; проверить Hybrid Batch Group, тени, створки дверей, F8/F9.
**Gate:** DC ≤2 500; 0 shader errors; Hybrid Batch Group; двери/тени/FO ок. Иначе откат
(как 18.10) и Stage 6 вместо GRD.

---

## 7. Stage 5 — Воспроизводимость мержа + B2 города (2–4 недели, условно)

**Порог:** только если Stage 4 зелёный **и** окклюзия снимает **≥35 %** видимых слотов B2,
**и** решён отдельный бюджет теней. Иначе — не делать; остаётся A.

**Сложный код:** `T-CULL01 / H4` (`PrimumCityRebatch` + `mapping.csv` + prefab-ассеты).
**Почему сложно:** 174 001 scene-override / 34.3 МБ (54.6 % сцены); prefab-источник
`PRIMUM_CITY.prefab` не в git; входные FBX/prefab — Lore; нельзя повторить scene-overrides.
**Файлы:**
- [`C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\PRIMUM_CITY.prefab`](file:///C:/Projects/ProjectC_client/Assets/_Project/Prefabs/City/PRIMUM_CITY.prefab)
- [`C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scenes/World/WorldScene_0_0.unity)
- `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Primum\Rebased\**` (16 809 мешей)
- Референсы: `PrimumMeshRebase.cs` (32, 40, 48, 440–441), `PrimumMaterialMap.cs` (31, 38, 113–173),
  `DoorPrefabBuilder.cs` (63–70, 141, 219, 345, 514), `CombineMeshesToCollider.cs` (114).
- Данные J: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\J_granularity_variants.csv`,
  `J_city_chunks_variants.csv`.

**Действия:** тул + манифест в git; dry-run (`Σtris ±0.1 %`, слоты = группы); prefab-ассет на чанк;
сцена подменяет child; пилот `S9_TRD-S2` (104 → ~330 B2); B2 = дом × материал только ≥1 м,
мелочь/интерьеры — чанк-батчи; **не** ячейки 10–30 м и не «по слоям».
**Gate:** цвет ±10 %, тени ≤+10 %, DC не растёт, Σtris ±0.1 %, F8/F9, reimport одного FBX;
при провале — откат к A.

---

## 8. Stage 6 — VisibilityDirector (3–4 недели, опционально)

**Когда:** если нужен мультикамерный union/бюджеты/корабли/интерьеры вне GRD и если Stage 4 не
покрывает; либо как fallback, если GRD красный.

**Сложный код:** `T-CULL01 / H6`.
**Файлы:** новый `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\VisibilityDirector.cs`
(+ группы/FO-адаптер); референсы — `DetailDistanceCuller.cs` (13–107), `PrimumLightPool.cs` (33, 159–219),
`GlobalMotionControlledRebaseSlice.cs` (265–418, 1345, 1349–1354), `GlobalMotionActorContract.cs` (10–15).
**Требования:** ≤0.3–0.5 мс, 2–4 Гц, гистерезис, union только «настоящих» камер, FO-safe,
не трогать `DoorController`/коллайдеры/свет/сеть/VFX.
**Gate:** culling ≤2 мс; −60…−80 % DC без окклюзии; F8/F9 зелёный.

---

## 9. Stage 7 — HLOD/L1 + L2-импостор / PVS (опционально)

**Когда:** нужен L0 <1k или L1/L2; город заморожен; есть бюджет 6–10 недель.
**Сложный код:** `T-CULL01 / H7` (HLOD-baker/impostor bake/portal-PVS).
**Файлы:** `C:\Projects\ProjectC_client\Assets\_Project\Editor\` (новый HLOD tool),
`C:\Projects\ProjectC_client\docs\world\primum_city\01_SETUP\04_LOD_AND_DISTANCE.md`,
`C:\Projects\ProjectC_client\docs\world\distantfocus\README.md`.
**Gate:** L1 ≤500 DC, L2 ≤10; силуэт узнаваем; FO; без rebake-катастрофы.

---

## 10. Роли и как передавать сложный код

| Этап | Кто | Что передаём |
|---|---|---|
| Stage 0 | Lead/пользователь | простые фиксы, baseline |
| Stage 1 | **другой агент** по `T-CULL01/H1` | `ShipPrefabRepair` |
| Stage 2 | Lead/другой агент | дверной слой — простые правки; бюджет — `T-CULL01/H3` |
| Stage 2b | Lead/пользователь | двери ферм на слой, тени off — простые настройки (1 день) |
| Stage 3 | **другой агент** по `T-CULL01/H2` | `ShipVisualMerger` + LOD |
| Stage 4 | **другой агент** по `T-CULL01/H5` | GRD/шейдеры |
| Stage 5 | **другой агент** по `T-CULL01/H4` | `PrimumCityRebatch` |
| Stage 6 | **другой агент** по `T-CULL01/H6` | VisibilityDirector |
| Stage 7 | **другой агент** по `T-CULL01/H7` | HLOD/PVS |

**Протокол передачи:** перед этапом открыть `T-CULL01`, взять соответствующий раздел `H*`,
приложить актуальный baseline/ветку и попросить агента:
1. прочитать `T-CULL01` + указанные absolute paths;
2. сделать только этот этап (не трогать соседние);
3. сдать diff + отчёт с замерами по gate;
4. не запускать Play Mode/не коммитить без разрешения;
5. при провале gate — откат по чек-листу.

---

## 11. Общие правила (для всех этапов)

- **Research-only документы не дают права менять сцену.** Перед кодом — отдельная задача/одобрение.
- Никаких **scene-overrides** для мержа; только prefab-ассеты/`.asset` в git.
- Никаких **static-флагов** на ездящем контенте; 2 076 static GO (коллайдеры/холдеры) не трогать.
- Не трогать `Terrain`, `PRIMUM_COLLIDERS`, `DoorController`, `PrimumLightPool`, `NetworkManager`,
  `ClientSceneLoader`, `ScenePlacedObjectSpawner`.
- FO: только frame-local данные или локальные координаты ячеек; после F8/F9 — сброс кэшей;
  публичного события ребейза нет (хук `TryRebuild`/поллинг `CumulativeRebaseOffset`).
- Бюджет culling ≤2 мс; всё дороже — off by default.
- Каждый этап — отдельный маленький коммит с тикетом `T-CULL01`; push не делать.

---

## 12. Ссылки

- План/обоснование: [`09_BEAM_COMPARISON_AND_DECISIONS.md`](09_BEAM_COMPARISON_AND_DECISIONS.md),
  [`05_RECOMMENDED_ARCHITECTURE.md`](05_RECOMMENDED_ARCHITECTURE.md),
  [`06_EXPERIMENTS_AND_ROADMAP.md`](06_EXPERIMENTS_AND_ROADMAP.md).
- Тикет сложного кода: [`C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md).
- Отчёты: [`07_SUBAGENT_REPORTS/`](07_SUBAGENT_REPORTS/) — A–J; ключевые H/I/J.
- Данные: [`08_DATA/`](08_DATA/).
