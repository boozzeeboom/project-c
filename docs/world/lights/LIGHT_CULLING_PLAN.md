# LIGHT CULLING — план исполнения (T-LIGHT16…T-LIGHT23)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **Unity:** 6000.5.2f1 · **URP:** 17.5.0
**Основание:** `docs/world/lights/LIGHT_CULLING_TIMING_RESEARCH.md` + подотчёты A–E.
**Статус:** ресерч проверен (3 независимых прохода); обязательные корректировки внесены.
**L1/T-LIGHT16 — PASS**; **L2/T-LIGHT17 — PASS**; **L2b/T-LIGHT17b — PASS** (ALIC 6.81 → 0.16 мс/кадр, −97.6 %).
Следующий — **L3/T-LIGHT18 (фокус/камера)**.
**Важно:** T-LIGHT24/25 **удалены**. Финал T-CULL01 — после закрытия светового трека.

---

## 0. Проверенное состояние

| Факт | Значение | Источник |
|---|---|---|
| Источники под `PRIMUM_LIGHTS` | **2 238**: `OUT` 753 / `IN` 1 334 (ADMIN 530) + **151 фермерских** (`PRIMUM_FARMLIGHT_*`) | сцена/верификатор cc3ad |
| Включено в edit mode | **151 ферм-ламп** (`m_Enabled:1` override); `Rebuild()` гасит их при загрузке | сцена |
| Пул | `maxActive=96`, `activeRadius=140`, `checkInterval=0.25`, `hysteresis=0.1`, `focus=null` | `PrimumLightPool.cs:18-34`; сцена |
| Фокус | одна камера, перепоиск 1 с, без `cullingMask/targetTexture` | B |
| Владение `Light.enabled` | `PrimumLightPool` | A/E, директор не пишет |
| Владение `Light.intensity` | `AdditionalLightIntensityController` (ALIC); flicker INLIGHT → запись каждый кадр | ALIC:440–478 |
| Рендер-путь | **Forward+** (`ProjectC_URP_Renderer.asset:189 m_RenderingMode:2`); GRD/SRP/occlusion включены | asset / T-LIGHT12 |
| Лимит камеры | **256 всего** (255 additional + main), per-object 4 в Forward+ **не применяется** | URP source |
| Тени | 400 м / 4 каскада / soft; additional shadows **off** | URP asset |
| HDR | **off**; bloom/emissive >1 клипается; GI/probes нет | URP asset / T-LIGHT12 |
| HLOD | L1 46–48 DC, L2 2 DC; L2 night atlas уже запечён | Stage 7 |

**Ключевое:** проблема не в «снятом лимите», а в логике пула. Но **угол — не единственная и, возможно, не главная причина**: 64 % источников интерьерные, `PRIMUM_OUTLIGHT` днём имеет intensity 0 при 96 включённых, а `range override 48.3 м` у INLIGHT. Сначала — измерение, не конус.

---

## 1. Обязательные корректировки ресерча

1. **Forward+ уже включён** — T-LIGHT24 «переключить Forward+/GRD» **удалён**.
2. **«Лимит снят» — только per-object.** Пул всё ещё `maxActive=96`.
3. **Counts:** под `PRIMUM_LIGHTS` — **2 238** (`OUT` 753 + `IN` 1 334 + фермы 151); 2 087 — только `OUT/IN` имена; 151 ферм-ламп включены в edit mode (`m_Enabled:1`) и гасятся `Rebuild()`. Комментарий пула «1 900» устарел.
4. **MD2_Lamp_\*** удалены (`a7cc6db9`); группа `CityLamps` в ALIC инертна — не учитывать.
4. **Fade нельзя писать в `Light.intensity`:** intensity принадлежит ALIC, flicker пишет каждый кадр; fade — только через контракт с ALIC/общий множитель.
5. **Фокус — камера, не Transform игрока:** в режиме корабля камера до ~350 м от игрока; player-focus даст регресс. Anchor = камера, локальный игрок — только override/исключение.
6. **`VisibilityDirector.WorldCameras`** пересобирается 1 с и пуст на старте; для L1 использовать кэш камер/фолбэк, union — только с per-camera бюджетом.
7. **Углы конуса — из `Camera.fieldOfView`/аспекта**, не хардкод 70/40; `R_rear ≥ вынос камеры + 2·maxRange` (~25–30 м).
8. **`cullingMask`, `renderingLayerMask`, `LightLayer`, `Light.shadows` — инертны** в этой конфигурации; работают `enabled` и `range`.
9. **День/ночь-гейт:** 96 `enabled` с intensity≈0 днём — впустую; добавить.
10. **HLOD L2 night** уже есть в импостере/шейдере; не строить отдельный квад из `placement_manifest`.
11. **LOD/emissive:** HDR off + нет GI/probes → гашение Light оставит тёмные фасады; emissive не заменит свет на 60 м без проверки.
12. **maxActive:** 96 оставить базой; 128/160/192 — только после GPU-гейта (≤+2 мс) и замера lights-per-tile; тихое превышение 256 опасно.

---

## 2. Этапы

### L1 — T-LIGHT16: baseline-инструментация (гейт, без изменения поведения)

- Добавить временные/дебаг-счётчики в `PrimumLightPool` (и маркер Profiler): registered/active/candidates, out/in split, **фермы (151) отдельно**, cap binding, radius buckets, check ms; отдельно ALIC ApplyIntensity ms.
- Play Mode (пользователь): время суток **17–20 ч** (жалоба) + день/ночь; разворот 180°; 140/100/70 м; cap 96; замер `visibleLights.Length`, GPU ms, FPS.
- Определить: сколько active имеют intensity≈0; сколько ближайших — интерьерные; гаснут ли 151 ферм после `Rebuild`; связывает ли cap; какова реальная задержка; какая камера выбрана.
- **Gate:** отчёт с числами и top-причиной; никаких изменений поведения; сцена чистая; commit T-LIGHT16.
- **Только после этого** выбираем L2/L3.

### 1.1. Результат L1 (T-LIGHT16, 2026-11-01) — PASS

Отчёт: [`T-CULL01_LIGHT_BASELINE_REPORT.md`](T-CULL01_LIGHT_BASELINE_REPORT.md);
CSV: `T-LIGHT16_BASELINE_2026-11-01.csv`. N = **2 238**, registered/active 96, farms 151.

| Вывод | Числа |
|---|---|
| cap 96 **связывает** | P1 174/174 отсечено, STREET 177/177, ADMIN 540/540 |
| днём слоты заняты нулём | P1 12ч: **46 из 96** active имеют intensity≈0 (все OUT); глобально 753 OUT |
| интерьеры едят бюджет | P1/STREET: 50 IN + 46 OUT; ADMIN: **96/96 IN** |
| Фермы | 151 зарегистрированы, **43** активны у ферм; отдельной квоты нет |
| Реакция на 180° | **нет**: SetVersion=1, 0 из 96 active в фрустуме после разворота |
| CPU | **ALIC 5.4–7.1 мс/кадр** против пула 0.22–0.36 мс/кадр (пик 9 мс) |
| Фокус | ThirdPersonCamera_0; на старте до спавна — AtmosphereCamera @(0,0,0) |
| F8/F9 | PASS; DC P1 2254 vs Stage8 2229 (+1.12%, шум сессии) |

Вывод: **угол — третий рычаг**. Сначала day/night-гейт, split out/in и стоимость ALIC; конус — после.

### L2 — T-LIGHT17: ownership + дешёвые победы

1. **Day/night-гейт пула:** не держать/не включать источники с `intensity ≈ 0`; при 12 ч освобождается ~46 слотов P1. Проверить переходы 17–21 ч, чтобы свет включался вовремя.
2. **Раздельный/приоритетный бюджет OUT/IN:** интерьеры не должны занимать все 96 (ADMIN 96/96, P1 50/96). Ввести квоты/score; измерить визуал улицы/интерьера.
3. **`activeRadius` 140 → 100/70** (конфиг/A-B); range ламп 3.5–14 м — это и есть LOD света без нового кода.
4. **Карта владения + A/B-флаг + строка отчёта** (`enabled` — pool, `intensity` — ALIC); не менять `intensity` в пуле.
5. **ALIC — отдельный T-LIGHT17b (обязателен, самый большой CPU):** 6 мс/кадр на 2 238 лампах; ограничить Apply активными/нулевыми/группами, обновлять intensity при включении. В L2 только измерить/подготовить контракт, не ломать flicker.
6. **Gate:** day activeZero=0; P1 cap saturation заметно ниже; визуал не хуже; CPU/GPU без регресса; F8/F9; commit T-LIGHT17.
- **Только после L2** — L3 фокус и L4 конус.

### 1.2. Результат L2 (T-LIGHT17, 2026-11-01) — PASS

Отчёт: [`T-LIGHT17_REPORT.md`](T-LIGHT17_REPORT.md); CSV: `T-LIGHT17_2026-11-01.csv` (22 кейса A/B).
N = 2 238, `maxActive` 96 без изменений, сцена не сохранялась.

| Рычаг | Числа |
|---|---|
| day/night-гейт | P1 12 ч `zeroActive` **46 → 0**, `activeOUT` 46 → 0, `activeIN` 50 → **96**; то же на STREET; DC 2242 без изменений, ALIC 5.89 → 5.59 мс |
| возврат вечером | `l2_100` 18 ч `activeOUT` **46**, 20 ч **46**, `zero` 0 — погашенное днём возвращается вместе с кривой 17–21 ч |
| резерв OUT/IN | ADMIN `activeOUT` **0 → 27** (`split140`), `quotaDeferred 553`; на площади состав не меняется (46/50) |
| радиус 100 | P1 12 ч `capSaturated 1 → 0`, `capCut` 174 → 0, `active` 96 → 70; ночью P1/ADMIN набор тот же |
| радиус 70 | **отклонён**: 96 → 49 ночью, FARM 43 → 21, выигрыша в DC/GPU нет |
| F8/F9 | **PASS** / **PASS_rollback_restore**, `0 errors`, сцена не сохранена |

Владелец `Light.enabled` — пул, владелец `Light.intensity` — ALIC; пул `intensity` только читает.
Карта владения и `ConfigLine` пишутся в raw-лог прогона. DC/GPU L2 не меняет — реальный CPU-рычаг это ALIC (T-LIGHT17b).

### 1.2b. Результат L2b (T-LIGHT17b, 2026-11-01) — PASS

Отчёт: [`T-LIGHT17B_REPORT.md`](T-LIGHT17B_REPORT.md); CSV: `T-LIGHT17B_2026-11-01.csv` (26 кейсов A/B).

| Рычаг | Числа |
|---|---|
| ALIC self-time | **6.81 → 0.16 мс/кадр** (медиана), **−97.6 %** (гейт ≤1.0 мс / −70 %) |
| Записи/кадр | **≈2 239 → ≈4** в установившемся режиме (writes), iterated 2 238 → 96–136 |
| Кадр целиком | медиана **−13.5 мс**, 12/13 пар в плюс (нативный cost записей вне stopwatch) |
| DC | совпал во всех 13 парах; P1 12ч 2251/2251, 18ч 2262/2262 |
| Day/night gate | сохранён: `activeOUT` 0 на 12ч, 40–46 на 18/20ч; `intensityZeroActive` 0 |
| F8/F9 | PASS / PASS_rollback_restore; 0 errors; сцена не сохранялась |
| Визуал | sign-off за пользователем: включение лампы без тёмного кадра; flicker интерьеров; скачок часа |

Оговорки: `ZeroIntensityTracked` в active-режиме сменил семантику (диагностика, не поведение);
`poolCheckMaxMs=8.730` — lifetime max, не per-case; контроль 6.28–7.29 мс vs L2 5.40–6.08 — разные сессии.

### L3 — T-LIGHT18: фокус/камера

- Кэш камер, фильтры (Game, no RT, far≥50, слой мира; Atmosphere/парковочная MainCamera исключены), fallback.
- Anchor = камера; локальный игрок — только явный override; сетевой `LocalClient.PlayerObject`.
- Адаптивный триггер: сдвиг >2 м / поворот >3°, минимум 0.1 с.
- Gate: реакция ≤0.15 с, нет «чужой камеры», нет регресса в режиме корабля.

### L4 — T-LIGHT19: конус/веер

- FOV-derived H/V, `R_rear` ≥ вынос + 2·range, score distance+angle, pre-activation; cap 96 (128 только после GPU-гейта).
- Gate: свет в веере, лампы сзади гаснут, нет pop-in; визуал пользователя.

### L5 — T-LIGHT20: тюнинг/A-B

- Гистерезис, activeRadius 140/100/70, cap 96/128, range 48.3; GPU ms ≤+2; FPS без регресса.
- Gate: выбранные значения + отчёт; откат конфигом.

### L6 — T-LIGHT21: fade в ALIC

- Fade-множитель живёт в ALIC (владелец intensity); pool передаёт вес/состояние; 0.15/0.30 с.
- Gate: нет «щелчков», flicker/день-ночь не сломаны.

### L7 — T-LIGHT22/23: multi-camera + day/night emissive (условно)

- Union только при реальной второй камере; per-camera бюджет; fallback. Day/night emissive `PM_LAMP_GLOW_WARM` — отдельная ветка с knob/перепечкой HLOD ClassLamp.
- T-LIGHT24/25 — **не делать** (Forward+/GRD уже; порталов/PVS нет).

---

## 3. Приёмка трека

- Реакция ≤0.15 с; свет в веере; активных ≤ cap; CPU пула ≤0.5 мс; ALIC не дороже baseline.
- Нет «щелчков»; flicker/день-ночь; HDR-off артефакты не хуже.
- F8/F9; L1/L2 HLOD, двери, корабли не сломаны; 0 errors; сцена не сохранена.
- После L1–L6 — финальная приёмка T-CULL01.

---

## 4. Следующий шаг

**L3 / T-LIGHT18 — фокус/камера:** кэш камер, фильтры, anchor камера, адаптивный триггер;
после него L4/T-LIGHT19 (конус), L5/T-LIGHT20 (тюнинг), L6/T-LIGHT21 (fade в ALIC).
Brief: [`T-LIGHT18_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-LIGHT18_AGENT_BRIEF.md).
Отчёты: [`T-LIGHT17B_REPORT.md`](T-LIGHT17B_REPORT.md) · [`T-LIGHT17_REPORT.md`](T-LIGHT17_REPORT.md) · [`T-CULL01_LIGHT_BASELINE_REPORT.md`](T-CULL01_LIGHT_BASELINE_REPORT.md).
