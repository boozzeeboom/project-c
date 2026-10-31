# T-CULL01 / Stage 2 — brief для отдельного агента (двери + бюджет теней)

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `870ff534` (H1) · **План:** `10_EXECUTION_PLAN.md` §4
**Статус:** Stage 2 — следующий шаг; Stage 1 (H1) принят частично, Stage 0 baseline — P1 **27 131 DC** при Midday.
**Play Mode:** нужен для замеров; запускать только с разрешением пользователя. Не сохранять сцену, не делать push.

## 0. Цель

Снять **84 % draw calls** дверного кадра без переделки геометрии:
- новый слой `PRIMUM_DOORS` (слот 10) + `Camera.layerCullDistances` (120–150 м);
- отдельный **`shadowCastingMode`-бюджет** (слой дистанции тени не режет);
- при необходимости — `DoorVisibilityBudget` (Renderer.enabled по группам/дистанции, гистерезис).
- Stage 2b: двери ферм (270 MR) на тот же слой + тени геометрии ферм off.

**Контроль (Stage 0, Midday):** P1 **27 131 DC**; P1 без городских дверей **4 607**; без всех дверей **2 730**.
Цель Stage 2: **5 000–9 000 DC** на P1, двери в кадре — сотни, без заметных дефектов.

> ⚠️ **Нужно разрешение на изменение `ProjectSettings/TagManager.asset`** (новый слой 10). Без него
> `layerCullDistances` невозможен; fallback — только per-object distance-менеджер (меньший эффект).
> Перед правкой — получить явное «да» от пользователя/Lead.

---

## 1. Абсолютные входы

- План: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md`
- Тикет H3: `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §4
- Baseline: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\K_baseline_2026-10-31.csv`,
  `K_stage0_report.md`
- Дверные префабы (12, 11 используются):
  `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\*.prefab`
  (`PM_DOOR_SINGLE_PLANK_A`, `PANEL_B`, `ARCH_C`, `DOUBLE_A`, `PALACE_DOUBLE_A/B/C`, `GATE_A/B`,
  `WIDE_A`, `NARROW_A` (0 инстансов), `PM_ADM_ENTRANCE_PORTAL`)
- Двери ферм: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Farms_Merged\*` + сцена
- Слои: `C:\Projects\ProjectC_client\ProjectSettings\TagManager.asset` (свободен слот 10)
- Геймплейные камеры: `Assets\_Project\Scripts\Core\SpringArmCamera.cs`, `WorldCamera.cs`;
  `ThirdPersonCamera` — runtime-риг
- Эталоны кода:
  - `Assets\_Project\Scripts\World\DetailDistanceCuller.cs` — гистерезис/группы/`Renderer.enabled`
  - `Assets\_Project\Scripts\World\PrimumLightPool.cs` — выбор камеры без `Camera.main` (33, 159–219)
  - `Assets\_Project\Scripts\World\FloatingOrigin\Network\GlobalMotionControlledRebaseSlice.cs` — FO-контракт
- Новый скрипт (если нужен):
  `Assets\_Project\Scripts\World\DoorVisibilityBudget.cs` + при необходимости `ShadowBudget.cs`
- CSV/отчёт: `docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\L_stage2_<date>.csv`

---

## 2. Что сделать по шагам

### 2a. Слой + `layerCullDistances` (сначала без нового кода, если хватает)

1. С разрешения — добавить в `TagManager.asset` слой **`PRIMUM_DOORS` = 10**.
2. Назначить слой **дверным префабам** (11 используемых) — инстансы наследуют.
   **Проверить физику/raycast:** `GameObject.layer` влияет и на коллайдеры; убедиться, что матрица
   `Physics` и все `LayerMask` для игрока/кораблей/дверей не сломались. Если сломались — остановиться
   и отчитаться (не «чинить» маски по всему проекту без согласования).
3. На геймплейной камере: `Camera.layerCullDistances = new float[32]`,
   `[10] = 120–150f`, `layerCullSpherical = false`. Не включать `AtmosphereCamera` (far 10, слой 9).
4. Замерить P1/улица/крыша при **Midday**, 3 замера, равный `frameCount`; сравнить с 27 131.
5. Если `layerCullDistances` не даёт ожидаемого (двери всё равно рисуются) — проверить, что камера
   действительно геймплейная, слой назначен, массив применён; зафиксировать.

### 2b. `shadowCastingMode`-бюджет (обязательно; слой тени не режет)

1. Решение по визуалу: можно ли выключать тени дверей дальше **40–80 м**?
   Если да — `Renderer.shadowCastingMode = Off` для дверей за порогом; если нет — оставить тени,
   зафиксировать, что выигрыш будет меньше.
2. Реализовать группами (не per-door каждый кадр): обновление 2–4 Гц, гистерезис 10–15 %,
   `Renderer.enabled`/`shadowCastingMode` (не `SetActive`), FO-safe — позиции читать живьём.
3. Мультикамерный union: `Renderer.enabled` глобален; учитывать только реальные геймплейные камеры,
   `AtmosphereCamera` исключить.
4. Замерить день + ночь (ночью тени уже выключены — контроль).

### 2c. Stage 2b — фермы (1 день, после 2a)

- `Primum_farms_DOORS` (101 инстанс / **270 MR**) → слой `PRIMUM_DOORS`, та же дистанция.
- `shadowCastingMode=Off` геометрии `Primum_farms` (142 MR / 150 slots / 116.6 МБ / 1.27 млн тр)
  — фермы в 963–1 051 м, тени не читаются; **не клеить**.
- Свет 151 `Light` — проверить, что фермы не попадают в `PrimumLightPool` (≤96).
- Коллайдеры/3 mesh collider не трогать.

---

## 3. Методика замеров (обязательно)

- Play Mode → **Host**, прогрев ~1500 кадров; фаза **Midday forced** (как Stage 0) — иначе сравнение
  с 27 131 некорректно; ночью отдельный контроль.
- Камера геймплейная, far **30 000**; `AtmosphereCamera` вне замера; `Camera.main` не использовать.
- P1 центра `(39593.1, 2509.6, 39833.8)`, yaw 337.5; улица; крыша; повтор P1 для ±5 %.
- Метрики: `UnityStats.drawCalls/setPassCalls/triangles/shadowCasters`, Frame Debugger (Camera/Shadow),
  Profiler Culling; FPS — только после прогрева (в Editor не показатель).
- F8/F9: 0 errors; проверить, что после ребейза дистанции/видимость не «залипли».
- CSV: `L_stage2_<date>.csv` (point, dayPhase, layerDist, doorShadows, DC, SetPass, tris, shadowCasters, fps, frame).

---

## 4. Приёмка Stage 2

- [ ] слой `PRIMUM_DOORS` существует и назначен 11 используемым префабам;
- [ ] физика/raycast дверей не сломаны (проверить `Physics`-матрицу и игрока/корабли);
- [ ] P1 **27 131 → 5 000–9 000 DC**; двери в кадре — сотни;
- [ ] тени по согласованному визуалу; ночь не хуже;
- [ ] F8/F9: 0 errors, без «залипшей» видимости;
- [ ] фермы: −270 дверных рендереров, тени off; визуал ворот ок;
- [ ] сцена не сохранена; prefab/FBX не в git; `git status` чистый (кроме разрешённых правок);
- [ ] отчёт Lead: commit hash, 4 строки DC до/после, CSV, проверка физики/raycast, F8/F9.

**Kill / stop:**
- правка `TagManager` не разрешена → делать только per-object distance-бюджет без слоя, отчитаться;
- ломаются коллизии/raycast дверей → стоп, откат слоя;
- визуальные дефекты/тени хуже согласованного → откат, оставить только безопасную часть;
- сцена получает diff → не сохранять, отчёт.

---

## 5. Жёсткие правила

1. Только Stage 2/2b; не начинать Stage 3 (корабли), пока не закрыт R0.2.
2. Не менять `ProjectC_URP.asset`, `ProjectSettings` (кроме разрешённого `TagManager`), `Packages`.
3. Не использовать `Camera.main`; не менять `DoorController`, коллайдеры, `PrimumLightPool`.
4. Не `SetActive` для куллинга — только `Renderer.enabled`/`shadowCastingMode`.
5. Никаких мировых `Vector3` между кадрами; FO-safe.
6. Не сохранять сцену; prefab/FBX — вне git; commit только разрешённые файлы; без push.

---

## 6. Что сдать Lead

- commit hash (если был);
- до/после P1: DC/SetPass/tris/shadowCasters; CSV path;
- что изменено (absolute paths) и почему;
- физика/raycast: чем проверено;
- F8/F9, Console; что осталось непроверенным;
- подтверждение, что сцена не сохранена, `git status` чистый.
