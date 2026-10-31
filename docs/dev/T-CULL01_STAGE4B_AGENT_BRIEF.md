# T-CULL01 / Stage 4b — brief для отдельного агента (gap close 3 031 → ≤2 500)

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `2c88e366`
**Scope:** только город/двери. **Корабли не оптимизировать:** heavy не трогать; medium/light/NPC
не менять; если мешают в кадре — выключить их `MeshRenderer` runtime-ом, без сохранения сцены.
**Play Mode:** нужен для замеров; запускать только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE4_REPORT.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` §6/§7
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §6.5
- `C:\Projects\ProjectC_client\docs\dev\T-LIGHT01_SHADOW_CONFIG.md`

---

## 0. Где мы сейчас

Stage 4 дал **P1 6 400 → 3 031 DC (−52.6 %)**, `hybridDC = 2 876 / 18 196 instances`, `srpBatcherDC = 8`,
0 shader errors, визуально пользователь принял. Но:
- гейт **DC ≤2 500 не взят**: 3 031 (+531, +17.5 %);
- **GPU occlusion = 0 %** (3 029 без occlusion vs 3 031 с ней);
- **Stage 5/B2 не запускать автоматически**: условие «окклюзия ≥35 %» не выполнено, а данные J
  показывают, что B2 увеличивает units/slots (A 698/1 656 → B2 915/3 182; DC-модель 2 292 → 3 904)
  и под GRD может **увеличить** unique (mesh,material)-пары;
- `lodBias = 0.70` при quality Medium; числа Stage 3 при 1.30 несравнимы;
- старые Stage 0/2/3 числа при тенях 1000/2 — исторические.

**Цель 4b:** найти безопасный путь к **≤2 500** или доказать, что его нет без Stage 5. B2 apply — запрещён.

---

## 1. Порядок работы (по возрастанию риска)

### 1.1. Baseline (0.5 дня)

- Play Mode Host, Midday, far 30 000, fov 60, текущие тени 400/4/soft, `lodBias` фактический.
- Корабли выключены; C1/площадь, C2/улица, C3/крыша; `Stage4GrBench` уже умеет это.
- Зафиксировать DC/SetPass/tris/shadowCasters/hybridDC/hybridInstances; CSV `P_stage4b_<date>.csv`.
- Старые 3 031 / 6 400 — контроль; повтор ±5 %.

### 1.2. `m_SmallMeshScreenPercentage` (сначала, 0.5–1 день)

- Сейчас в `ProjectC_URP.asset:87` **0**. Это встроенный GRD small-mesh culling; включение требует
  разрешения на правку URP-ассета.
- Свип: **0.5 / 1 / 2 %** (или доступные единицы поля), каждый — замер C1/C2/C3 + **визуальная проверка
  на pop-in** (мелкие детали, двери, крыши, окна).
- Если pop-in заметен — откатить значение; не «дожимать» ценой визуала.
- Записать `hybridDC`, `hybridInstances`, DC; ожидание: часть мелких мешей перестаёт рисоваться.

### 1.3. Городской shadow budget (0.5–1 день, средний риск)

- В проекте уже есть `Assets\_Project\Scripts\World\ShadowBudget.cs` (по именам корней, сейчас
  `Primum_farms`, `shadowOffDistance=60`). Для города нужен отдельный экземпляр/конфиг:
  корни чанков `PRIMUM_*` / `WorldRoot_0_0`, дистанция **150–250 м**, группа = чанк.
- **Не менять** URP `shadowDistance=400/4` и не трогать `ShipShadowBudget`/двери.
- Замер: C1 день, C3; визуал теней города (силуэты домов, крыши, двери); ночь — контроль (теней нет).
- Если визуально плохо — откат; это не обязательный рычаг.

### 1.4. Material/submesh dry-run (0.5 дня)

- По данным `C_primum_batches.csv` / `C_primum_chunks.csv` посчитать: сколько unique
  (mesh,material)-пар и submesh-слотов дают hybridDC; сколько даст объединение материалов в атлас
  (только оценка, **без переделки**).
- Если потенциал <~500 DC — не идти в атлас; если >500 — отдельное решение.

### 1.5. B2 dry-run (0.5 дня) — **не apply**

- На пилоте `PRIMUM_HOUSES_S9_TRD-S2_P1_45H` (кейс `PILOT`) оценить, сколько hybrid-батчей/DC даст B2:
  merge house×material ≥1 м; **сравнить с текущим A**.
- Если B2 **не уменьшает** hybridDC — закрыть вопрос, Stage 5 не делать.
- Если уменьшает — отчёт Lead/пользователю с числами; apply только отдельным решением.

---

## 2. Абсолютные входы

- `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP.asset` — 86–88 (`m_GPUResidentDrawerMode`,
  `m_SmallMeshScreenPercentage`, occlusion), 58–59 тени.
- `C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset` — 58 (KeepAll).
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\Stage4GrBench.cs` — bench; расширить при необходимости.
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ShadowBudget.cs` — городской бюджет.
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DoorVisibilityBudget.cs` — не ломать.
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\`:
  `O_stage4_*`, `J_granularity_variants.csv`, `J_city_chunks_variants.csv`, `C_primum_chunks.csv`,
  `C_primum_batches.csv`.
- Сцена: `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — не сохранять.
- Backup настроек: `_local_backups\T-CULL01\Stage4b\` (вне git).

---

## 3. Приёмка Stage 4b

- [ ] baseline воспроизведён (3 031 ±5 %); новый CSV;
- [ ] хотя бы один безопасный рычаг даёт **P1 ≤2 500** без заметного визуального регресса,
      **или** доказано, что без Stage 5 этого не достичь;
- [ ] B2 dry-run: есть число, уменьшает ли B2 hybridDC; apply не делался;
- [ ] 0 shader errors; Hybrid Batch Group; двери/ствени/тени/FO без регрессий;
- [ ] корабли не изменены; сцена не сохранена; commit `T-CULL01/H5b` + CSV/отчёт.

**Kill / stop:**
- small-mesh culling даёт заметный pop-in → откат;
- городской shadow budget визуально плох → откат;
- B2 dry-run не показывает снижения → Stage 5 не делать, зафиксировать;
- нет разрешения на URP-ассет → только dry-run/оценка, без записи.

---

## 4. Жёсткие правила

1. Только город/двери; корабли (heavy/medium/light/NPC) не трогать; при помехах — runtime-выключение.
2. Не менять prefab/FBX/сцену; не сохранять сцену; не push.
3. `ProjectSettings`/URP-ассет — только с явным разрешением пользователя.
4. Не `SetActive`, не `Camera.main`, не scene-overrides; FO-safe.
5. **B2 apply запрещён**; Stage 5 не начинать; P3/фрактуру не делать.
