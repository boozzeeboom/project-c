# T-LIGHT16 — brief: baseline light-culling/timing instrument (L1, без изменения поведения)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD:** `c5e18532`
**Unity:** 6000.5.2f1 · **URP:** 17.5.0 (Forward+ уже включён)
**Scope:** только инструментация/замер. Никаких конусов, fade, focus-смен, day/night-гейтов, LOD.
**Play Mode:** разрешён пользователем для этой задачи. Сцену не сохранять, push не делать; свет не трогать.

**Прочитать:**
- `C:\Projects\ProjectC_client\docs\world\lights\LIGHT_CULLING_PLAN.md` (L1 = этот этап)
- `C:\Projects\ProjectC_client\docs\world\lights\LIGHT_CULLING_TIMING_RESEARCH.md` (§0–§3)
- Подотчёты: `research/A_LIGHT_SYSTEM_AUDIT.md`, `B_FOCUS_CAMERA_TIMING.md`
- Код: `Assets\_Project\Scripts\World\PrimumLightPool.cs`, `Assets\_Project\Scripts\Core\Lighting\AdditionalLightIntensityController.cs`, `Assets\_Project\Scripts\World\VisibilityDirector.cs`

---

## 0. Проверенное состояние (не пересчитывать, использовать как ожидание)

| Факт | Значение |
|---|---|
| Источники под `PRIMUM_LIGHTS` | **2 238**: `OUT` 753 + `IN` 1 334 (ADMIN 530) + **151 ферм** (`PRIMUM_FARMLIGHT_*`) |
| Edit-mode enabled | **151 ферм-ламп** (`m_Enabled:1`); `Rebuild()` гасит их при загрузке |
| MD2_Lamp_* | **удалены** (`a7cc6db9`); группа `CityLamps` инертна |
| Пул | `maxActive=96`, `activeRadius=140`, `checkInterval=0.25`, `hysteresis=0.1`, `focus=null` |
| Фокус | `Camera.allCameras`, перепоиск 1 с, без `cullingMask/targetTexture` фильтра |
| Владение | `Light.enabled` — пул; `Light.intensity` — ALIC (flicker INLIGHT → **запись каждый кадр**) |
| URP | Forward+, per-object 4 не применяется, per-camera 256; тени 400/4, additional shadows off; HDR off |

**Важно:** N = **2 238** (не 2 087/1 900). ALIC ApplyIntensity может стоить дороже пула — замерить отдельно.

---

## 1. Что сделать

### 1.1. Диагностика (edit-mode код, без смены поведения)

В `PrimumLightPool.cs` добавить (желательно за `#if UNITY_EDITOR`/дебаг-флагом):
- счётчики: `RegisteredCount`, `ActiveCount`, `_cand.Count`, `_desired.Count`/cap binding;
- разбивка out/in/farm: сколько активных `OUT`/`IN`/`FARMLIGHT`;
- radius buckets активных (0–70, 70–100, 100–140, 140–154 м) и сколько кандидатов отсеяно по cap;
- сколько активных имеют `intensity ≈ 0` (день);
- маркер Profiler `PRIMUM/LightPool.Check`; длительность Check, длительность `FindFocusCamera`;
- источник фокуса (`ThirdPersonCamera_*` / другая камера).

В `AdditionalLightIntensityController`:
- маркер `ALIC.ApplyIntensity`; длительность и число вызовов/кадр; сколько tracked lights участвуют в Apply.

**Никаких изменений `Light.enabled`, `intensity`, `focus`, `maxActive`, `activeRadius` в этом этапе.**

### 1.2. Play Mode baseline (пользователь запускает; агент собирает данные)

Процедура:
1. Play Mode → Host; Midday; затем установить время **17–20 ч** (жалоба пользователя) и повторить в **день (12 ч)**.
2. Точки: P1 (площадь), P2 (улица), ADMIN (интерьер), фермы (кластер).
3. На каждой точке: warmup 300–600 кадров; записать median UnityStats (DC, SetPass, GPU ms/FPS); `visibleLights.Length` (если доступен API); счётчики пула; ALIC ms; Check ms.
4. Движение: разворот камеры на 180° у P1; быстрый полёт/разгон через 3–4 улицы; пауза 1 с; повтор.
5. Отдельно проверить: гаснут ли 151 ферм после `Rebuild` (лог/счётчики); сколько активных имеют intensity≈0 днём; какая камера выбрана фокусом.
6. F8/F9 реальным вводом: 0 errors, лампы/корпуса на месте (после ребейза).

Выход: `docs/world/lights/T-LIGHT16_BASELINE_<date>.csv` + короткий отчёт `T-CULL01_LIGHT_BASELINE_REPORT.md`.
Рекомендуемые колонки: `point, timeOfDay, activeTotal, activeOUT, activeIN, activeFarm, candidateCount, capSaturation(0/1), intensityZeroActive, checkMs, focusSearchMs, alIcMs, dc, setPass, gpuMs, fps, notes`.

### 1.3. Вопросы, на которые обязан ответить отчёт

1. Связывает ли `cap=96` кандидатов на типовых точках? Какой процент радиусных кандидатов отсекается?
2. Сколько активных ламп днём имеют intensity≈0 (пустая трата слотов)?
3. Гаснут ли 151 ферм после Rebuild, и верно ли, что они входят в бюджет 96?
4. Сколько активных — интерьерные vs уличные; как это меняется с позицией камеры?
5. Какая камера реально используется как фокус (`FindFocusCamera`) в P1/улица/ADMIN/фермы?
6. Реальная задержка реакции (повернуть 180° → сколько кадров до обновления набора).
7. ALIC.ApplyIntensity vs PrimumLightPool.Check: что доминирует по CPU; GPU ms с cap 96.
8. Есть ли зависимость симптома от времени суток (17–20 ч vs 12 ч)?

---

## 2. Gate

- CSV заполнен по всем точкам/фазам; отчёт содержит ответы на 8 вопросов;
- **Поведение света не изменилось**: те же DC/активные на контрольной точке до/после инструментации (повтор ±1 %);
- 0 compile errors; 0 Play Mode exceptions; сцена не сохранена; prefab/FBX/корабли/URP 0 diff;
- Коммит `T-LIGHT16: light pool baseline instrumentation`; без push.

**Stop/kill:** инструментация меняет поведение → откат; маркер Profiler дороже 0.1 мс средн. → упростить; сцена dirty → не сохранять, отчёт.

---

## 3. Жёсткие правила

1. Только `PrimumLightPool` + `AdditionalLightIntensityController` (маркеры/счётчики); не трогать `VisibilityDirector`, HLOD, двери, корабли, URP/ProjectSettings.
2. Не менять конус/fade/focus/day-night-логику/LOD — это T-LIGHT17+.
3. Play Mode — только по разрешению пользователя; сцену не сохранять; push нет.
4. FO-safe: позиции читать живьём; новые поля — float/счётчики, не мировые Vector3.
5. Отчёт/CSV — в git; бэкапы/логи — не в git.
