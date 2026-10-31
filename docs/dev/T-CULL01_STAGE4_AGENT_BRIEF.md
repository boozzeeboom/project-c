# T-CULL01 / Stage 4 — brief для отдельного агента (GRD + GPU occlusion + DOTS-шейдеры)

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `2a986147`
**Scope:** включить **GPU Resident Drawer + GPU occlusion** и DOTS-варианты шейдеров для **города/дверей**.
**Корабли сейчас не оптимизируем.** `Ship_havyII_base` остаётся как есть; medium/light/NPC —
**не трогать**; если мешают в кадре/замерах — выключить их `MeshRenderer` runtime-ом, без правки prefab/сцены.
**Play Mode:** нужен для замеров; запускать только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` §6
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §6
- `C:\Projects\ProjectC_client\docs\dev\T-LIGHT01_SHADOW_CONFIG.md` (текущие тени `400/4/soft`)
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE2_REPORT.md` / `T-CULL01_STAGE3C_REPORT.md`

---

## 0. Предусловия

- Stage 2/3 готовы: двери на слое 10, `DoorVisibilityBudget`; heavy P2 закрыт; другие корабли не трогаем.
- **Текущие тени — `400 м / 4 каскада / soft on`** (T-LIGHT01). Старые P1 6 094 / heavy 1 538 —
  исторические; **сначала новый baseline**, потом GRD.
- Нужны явные разрешения пользователя на:
  - `ProjectSettings/GraphicsSettings.asset` (`m_BrgStripping`),
  - `Assets/_Project/Settings/ProjectC_URP.asset` (GRD/occlusion),
  - Play Mode.
  Без разрешения — только dry-run/аудит, без записи настроек.
- Сцена чистая; prefab/FBX не менять; `ShipShadowBudget`/двери не ломать.

---

## 1. Цель Stage 4

1. Включить `GPUResidentDrawerMode = InstancedDrawing` + GPU occlusion.
2. `m_BrgStripping = 2 (KeepAll)`.
3. DOTS/instancing-варианты в ~20 Shader Graph (15 город + 5 дверей), без поломки world-space шумов.
4. Пилот на одном чанке → вся сцена; Frame Debugger `Hybrid Batch Group`; DC/SetPass/GPU.
5. Цель: **DC ≤2 500**; 0 shader errors; двери/тени/эмиссия/FO без регрессий.

**Не писать свой алгоритм** — использовать engine. **Не начинать B2/переклейку города** (Stage 5) —
только если Stage 4 зелёный и окклюзия снимет ≥35 %.

---

## 2. Абсолютные входы

- `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP.asset` — строки 58–59, 73, 86–88
- `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP_Renderer.asset` — 189–190
- `C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset` — 58, 79–80
- Shader Graph:
  - `C:\Projects\ProjectC_client\Assets\_Project\Materials\**` (город — 49 SG / ~15 графов + 5 дверей)
  - `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\C_city_materials.csv`
  - `...\C_shaders.csv`
- URP 17.5 package:
  - `...\com.unity.render-pipelines.core@9fe9377312a8\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs` — 18–19, 60
  - `...\com.unity.render-pipelines.universal@748d7458cc62\Runtime\UniversalRenderer.cs` — 11–23
  - `...\UniversalRenderPipeline.cs` — 1700, 1753
  - `...\UniversalRenderPipelineAsset.cs` — 528, 1465–1468
  - `...\UniversalRendererRenderGraph.cs` — 974–980, 1515–1524
- Двери: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\*.prefab`,
  `DoorController.cs` / `DoorVisibilityBudget.cs`
- Корабли: `Ship_havyII_base.prefab` — **не менять**; medium/light/NPC — **не менять**.
- Отчёт/CSV: `08_DATA\O_stage4_<date>.csv` + `docs/dev/T-CULL01_STAGE4_REPORT.md`
- Backup настроек: `_local_backups\T-CULL01\Stage4\` (URP/GraphicsSettings копии, вне git)

---

## 3. Порядок работы

### 3.1. Baseline (0.5–1 день)

- Play Mode Host; Midday; far 30 000; fov 60; текущие тени 400/4/soft; quality Medium.
- **Отключить все корабли** (heavy + medium + light + NPC) на время city-baseline: `MeshRenderer.enabled=false`
  runtime-ом; не сохранять сцену. Записать P1/улица/крыша: DC, SetPass, tris, shadowCasters,
  Frame Debugger (opaque/prepass/shadow), culling ms.
- Отдельно — контроль с `Ship_havyII_base` включённым (без medium/light/NPC).
- Это **новый baseline**; старый P1 6 094 не использовать.

### 3.2. Shader audit (2–4 дня)

- 15 городских + 5 дверных Shader Graph: включить DOTS/instancing-варианты, проверить, что
  procedural world-space шумы/эмиссия не ломаются.
- Найти рукописные HLSL/Unlit без `DOTS_INSTANCING_ON`; для них — fallback/вариант.
- 0 shader errors после импорта; проверить `Hybrid Batch Group` в Frame Debugger.

### 3.3. Включение GRD (по разрешению)

- `GraphicsSettings.asset: m_BrgStripping = 2 (KeepAll)`.
- URP asset: `m_GPUResidentDrawerMode = 1` (InstancedDrawing),
  `m_GPUResidentDrawerEnableOcclusionCullingInCameras = 1`; Forward+ уже есть.
- Пилот на одном чанке → вся сцена; Frame Debugger: `Hybrid Batch Group`; DC/SetPass/GPU.
- Проверить: двери/створки, тени, эмиссию, стекло, terrain, F8/F9.
- Если двери «плывут»/ломаются — исключить их из GRD (opt-out), не откатывать весь GRD сразу.

### 3.4. Замер и решение

- P1/улица/крыша: DC/SetPass/tris; culling ms; GPU time (если доступно).
- Gate: **DC ≤2 500**; `Hybrid Batch Group` есть; 0 shader errors; двери/тени/FO ок.
- Если нет — откат настроек и отчёт; Stage 5/B2 не начинать, рассмотреть Stage 6 fallback.

---

## 4. Приёмка

- [ ] новый baseline при тенях 400/4 снят; старые числа не используются;
- [ ] GRD + GPU occlusion включены; `Hybrid Batch Group` подтверждён Frame Debugger;
- [ ] DC ≤2 500; 0 shader errors; двери/тени/эмиссия/FO без регрессий;
- [ ] корабли: heavy не изменён; medium/light/NPC не изменены (в отчёте — 0 diff);
- [ ] сцена не сохранена; commit `T-CULL01/H5`; отчёт Lead.

**Kill / stop:** нет Hybrid Batch Group; DC не падает; створки/тени ломаются; шейдеры сыпят ошибками;
нет разрешения на ProjectSettings/URP — остановиться и отчитаться.

---

## 5. Жёсткие правила

1. Не оптимизировать корабли; heavy не трогать; другие — только runtime-выключение рендереров для замера.
2. Не менять prefab/FBX/сцену; сцену не сохранять.
3. `ProjectSettings`/URP — только с явным разрешением пользователя.
4. Не `SetActive`, не `Camera.main`, не scene-overrides; FO-safe.
5. Не начинать B2/Stage 5 и не делать P3 фрактуру.
6. Prefab/FBX — Lore; `.asset`/скрипты/CSV — в git; push не делать.
