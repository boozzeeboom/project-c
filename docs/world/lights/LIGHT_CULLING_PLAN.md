# LIGHT CULLING — план исполнения (T-LIGHT16…T-LIGHT23)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **Unity:** 6000.5.2f1 · **URP:** 17.5.0
**Основание:** `docs/world/lights/LIGHT_CULLING_TIMING_RESEARCH.md` + подотчёты A–E.
**Статус:** ресерч проверен (3 независимых прохода); обязательные корректировки внесены.
**L1/T-LIGHT16 — PASS**; **L2/T-LIGHT17 — PASS**; **L2b/T-LIGHT17b — PASS** (ALIC 6.81 → 0.16 мс/кадр, −97.6 %);
**L3/T-LIGHT18 — PASS** (фокус-камера `ThirdPersonCamera_0` в игре 11/11, реакция 1–4 кадра вместо 5–10).
**L4/T-LIGHT19 — PASS** (веер: разворот 180° меняет набор 6/6, `activeBehindOutsideRear` 26–77 → 0–1, доля активных в кадре ×1.1…×5.5, DC без изменений).
**L5/T-LIGHT20 — PASS** (A/B по 8 осям от базы L4; финальный конфиг **совпал с L4**, дефолты в коде не менялись; `coneTurnSetChanged` 111/111, `intensityZeroActive=0`, F8/F9 PASS).
**L6/T-LIGHT21 — PASS по fade-in** (`t90` 0.107–0.132 с, скачок 21–24 % против 100 %, ALIC 0.035–0.117 мс, DC без изменений; fade-out отклонён по гейту удержаний, `fadeOutSeconds=0`).
**Световой трек L1–L6 закрыт и принят пользователем** (визуальный sign-off 2026-11-01: «визуально всё ок»).
Следующий — **финальная приёмка T-CULL01** ([brief](../dev/T-CULL01_FINAL_ACCEPTANCE_AGENT_BRIEF.md)) —
**выполнена 2026-11-01: PASS, T-CULL01 = CLOSED** ([отчёт](../dev/T-CULL01_FINAL_ACCEPTANCE_REPORT.md), §4).
**Важно:** T-LIGHT24/25 **удалены**, L7/T-LIGHT22–23 **отложены** (реальной второй камеры нет; emissive-ветка — отдельная тема).

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

### 1.3. Результат L3 (T-LIGHT18, 2026-11-01) — PASS

Отчёт: [`T-LIGHT18_REPORT.md`](T-LIGHT18_REPORT.md); CSV: `T-LIGHT18_2026-11-01.csv` (11 кейсов A/B).
Сцена не сохранялась, `maxActive` 96 и политика `dayGate`/`split`/`radius` без изменений.

| Рычаг | Числа |
|---|---|
| фильтры камер | **11/11** `focusSource=camera:ThirdPersonCamera_0`; `MainCamera-out_of_reach` (лампа 228 645.5 м при far 120 000), `AtmosphereCamera-mask_no_lights`; в меню `FocusCamera=none` вместо камеры меню |
| кэш вместо перепоиска 1 с | поисков за кейс **2–6 → 1** при стоимости 2.44–3.57 мс за вызов; `setVersion` и `dc` совпали во всех парах |
| адаптивный триггер (>2 м / >3° / floor 0.1 с) | телепорт 5 м: кадры **5 → 1** (0.188 → 0.035 с); разворот 180°: кадры **8 → 4** (0.262 → 0.124 с); гейт ≤0.15 с пройден |
| `checkMarkerMsAvg` | 0.2690–0.4615 → **0.2277–0.3832** мс/кадр, то есть чаще чеков не стало |
| DC / active | **совпали**: 2249/2249, 2273/2273, 534/534, 1041/1041, 446/446; сплит OUT/IN/FARM тот же |
| F8/F9 | **PASS** (`worldRootShift 256.000 = predicted`) / **PASS_rollback_restore**; `0 errors`, сцена не сохранена |
| разворот 180° | `probeTurnSetChanged=0` — направление камеры в критерии не входит; 0 из 96 в кадре. Это **не** L3, а L4/T-LIGHT19 |

Оговорки: `focusSearchMs` не уменьшился (поиск редкий, но по-прежнему O(2 238)); в FARM-кейсе
`NOTE;farm_cluster` пишется каждый кадр и растягивает кейс; один сэмпл `probeTurnSec=29.937` при `frames=2` —
артефакт блокировки главного потока редактора, опираться на кадры. Визуальный sign-off — за пользователем.

### L4 — T-LIGHT19: конус/веер

- FOV-derived H/V, `R_rear` ≥ вынос + 2·range, score distance+angle, pre-activation; cap 96 (128 только после GPU-гейта).
- Gate: свет в веере, лампы сзади гаснут, нет pop-in; визуал пользователя.
- Brief: [`T-LIGHT19_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-LIGHT19_AGENT_BRIEF.md)
  (веер из `fieldOfView`/аспекта, `R_rear` 25–30 м, `score = d + штраф за угол`, гистерезис по углу и радиусу,
  pre-activation; `probeTurnSetChanged` должен стать 1, `activeInFrustum` ≥ 2× контроля).

### 1.4. Результат L4 (T-LIGHT19, 2026-11-01) — PASS

Отчёт: [`T-LIGHT19_REPORT.md`](T-LIGHT19_REPORT.md); CSV: `T-LIGHT19_2026-11-01.csv` (12 кейсов A/B).
Сцена не сохранялась, `maxActive` 96 и политика `dayGate`/`split`/`radius` без изменений;
`Light.intensity` по-прежнему не пишется пулом.

| Рычаг | Числа |
|---|---|
| веер из FOV (`halfH=50.46`, `halfV=35.00` при fov 60) + `R_rear 28` + score `d + 1.2·угол` | отбор кандидатов по направлению камеры; `false` = точный контроль L2+L3 |
| разворот 180° | `coneTurnSetChanged` **0/6 → 1/6** (реакция 1–4 кадра, 0.027–0.120 с); `SetVersion` растёт на каждом развороте |
| «горит за спиной» | `activeBehindOutsideRear` **58/76/26/42/32/77 → 0/0/0/1/0/0**; тыловые лампы в `R_rear` остаются (7/10/40/5) |
| свет в кадре | доля активных в кадре 37 %→70 % (P1 12ч), 48 %→73 % (P1 18ч), 52 %→73 % (P1 20ч), 11 %→61 % (STREET), 78 %→89 % (FARM); в ADMIN 7 %→8 % (окклюзия стенами — вне L4) |
| бюджет | `active` 70/96/96/96/32/96 → 37/79/51/96/28/79; `capCut` P1 18ч 26→0, STREET 80→0, ADMIN 474→131 |
| DC | **совпал во всех 6 парах**: 2265 / 2287 / 734 / 1033 / 446 / 1647 |
| CPU | `checkMarkerMsAvg` 0.3010–0.3881 мс/кадр (гейт ≤0.5), `poolCheckMs` 1.77–1.86 против 1.81–2.46 в контроле |
| F8/F9 | **PASS** (`worldRootShift 512.000 = predicted 512.000`) / **PASS_rollback_restore**; `errors=0`, сцена не сохранена |

Оговорки: «`activeInFrustum` ≥ 2× контроля» достигнуто только там, где в кадре есть куда расти
(STREET ×2.8); на P1/ADMIN/FARM контроль уже упирался в потолок числа ламп в кадре — сравнивать нужно
долю активных в кадре (см. выше). В интерьере ADMIN доля в кадре почти не изменилась (веер не решает
окклюзию). `coneSource=fallback` в прогоне не встретился — фокус всегда `camera`.
**Визуальный sign-off получен от пользователя 2026-11-01: «визуально всё ок»** (веер при развороте,
лампы за спиной, интерьеры, вечер) — L4 принят полностью.

### L5 — T-LIGHT20: тюнинг/A-B

- Гистерезис, activeRadius 140/100/70, cap 96/128, range 48.3; GPU ms ≤+2; FPS без регресса.
- Gate: выбранные значения + отчёт; откат конфигом.
- Brief: [`T-LIGHT20_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-LIGHT20_AGENT_BRIEF.md)
  (A/B по одной оси от базы L4: radius 100/120/140, cap 96/128 только через GPU-гейт ≤+2 мс, hysteresis
  0.10/0.20, margin 6/10°, pre 0/5/8°, rear 20/28/35, penalty 0.5/1.2/2.0; `range` 48.3 м — только
  измерение, сцену/префабы не менять; финал фиксируется дефолтами в коде).

### L5 — T-LIGHT20: тюнинг/A-B

- Гистерезис, activeRadius 140/100/70, cap 96/128, range 48.3; GPU ms ≤+2; FPS без регресса.
- Gate: выбранные значения + отчёт; откат конфигом.

### 1.5. Результат L5 (T-LIGHT20, 2026-11-01) — PASS

Отчёт: [`T-LIGHT20_REPORT.md`](T-LIGHT20_REPORT.md); CSV: `T-LIGHT20_2026-11-01.csv`
(run `172434` — 54 кейса, run `173217` — 57 кейсов). Сцена не сохранялась, `maxActive` 96,
политика `dayGate`/`split`/`outMin`/`checkInterval` и логика отбора L4 не менялись;
пул по-прежнему не пишет `Light.intensity`, `Light.range`, `shadows`.

База = принятый L4: `radius 100`, `cap 96`, `hysteresis 0.10`, `margin 6°`, `pre 5°`, `rear 28 м`,
`penalty 1.2`. База воспроизвелась в обоих прогонах (P1 18 ч: активных 72, доля в кадре 81 %,
`capCut` 0, `gpuMs` 9.28/9.06, DC расходятся ≤0.4 %).

| Ось | Варианты | Числа | Итог |
|---|---|---|---|
| `activeRadius` | 120 / 140 | P1 18 ч: активных 96, доля в кадре 94/96 %, `capCut` 18/57, `capSaturated` 1, `gpuMs` +1.2…1.5 мс | **100 остаётся** |
| `maxActive` | 128 | P1/P1-20 ч без изменений (72/58); ADMIN 128 vs 96 → доля в кадре **73 %** vs 80 %, `ΔgpuMs` ≤0.2 мс, FPS не ниже | GPU-гейт PASS, вариант отклонён — **96 остаётся** |
| `hysteresis` | 0.20 | P1 `behindOutsideRear` 0→1, STREET 1→2, доля в кадре 81→79 % | **0.10 остаётся** |
| `coneMarginDeg` | 10° | метрики идентичны базе | **6° остаётся** |
| `conePreActivationDeg` | 0 / 8 (`halfH` 45.5/50.5/53.5) | pre 0: P1 67, STREET 43; pre 8: P1 77, STREET 51; доля в кадре **не меняется** (58/31) | **5° остаётся** |
| `rearRadius` | 20 / 35 | rear 20: 69/42, `behindOutsideRear` 0; rear 35 → эффективный **30.0** (`min(radius,30)`), STREET `behindOutsideRear` 2 | **28 остаётся** |
| `anglePenaltyPerDeg` | 0.5 / 1.2 / 2.0 | P1/P1-20 ч идентичны (cap не связывает, `capCut` 0) | **1.2 остаётся** |
| `range` INLIGHT 48.3 м | только измерение | `inActiveRangeGt20 / inActiveTotal` = **100 %** во всех кейсах (30/30, 95/95, 127/127) | сцена/префабы не менялись, решение — отдельным тикетом |

**Финальный конфиг совпал с L4 → дефолты в коде не менялись** (`ApplyTuneConfig` добавлен только для A/B).
F8/F9 шли под финальным конфигом (confirm-группа оставляет его в памяти пула).

Гейт прогонов: `coneTurnSetChanged = 1` в **111/111** кейсах (реакция 1–4 кадра); `intensityZeroActive = 0`
везде, включая 12 ч; `activeOUT` на 18/20 ч = 42 (P1); `checkMarkerMsAvg` 0.2353–0.3806 мс (≤0.5);
`poolCheckMs` макс 2.785; F8 **PASS** (`worldRootShift 256.000 = predicted 256.000`); F9
**PASS_rollback_restore**; `errors=0, exceptions=0, shaderErrors=0`; `sceneDirtyAtStart/Now = False`;
`BENCH_DONE … casesCompleted=54/54` и `57/57`, `hold=no`; вход из меню бенча проверен (`GATE;result=OK`,
риг `ThirdPersonCamera_0`, `localPlayer=True`, `registered=2238`).

Оговорки: **ADMIN исключён из выводов по осям** — одна и та же поза даёт `inFrame` 77 (80 %) и 26 (27 %),
`capCut` 128 vs 152, `behindOutsideRear` 0 vs 21, потому что pass 0 «уже горит» удерживает набор,
принесённый предыдущим кейсом, а кандидатов в интерьере (224) больше cap; `gpuMs` шумный (8.63–10.77 мс
на одном конфиге в P1 18 ч); `fps` в FARM занижается артефактом `NOTE;farm_cluster` (известен с L3/L4);
`activeBehindOutsideRear` 0–1 на P1/STREET — лампа в гистерезисной полосе `R_rear·1.1 = 30.8 м`
(в CSV L4 для `STREET_h18_coneOn` тоже 1); `range` INLIGHT 48.3 м не менялся — правка требует
отдельного одобрения и замера lights-per-tile.
**Визуальный sign-off получен от пользователя 2026-11-01: «визуально всё ок»** — финальный конфиг совпал
с принятым L4, картинка не менялась, L5 принят полностью (открытые темы вынесены в §8 отчёта L5).

### L6 — T-LIGHT21: fade в ALIC

- Fade-множитель живёт в ALIC (владелец intensity); pool передаёт вес/состояние; 0.15/0.30 с.
- Gate: нет «щелчков», flicker/день-ночь не сломаны.
- Brief: [`T-LIGHT21_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-LIGHT21_AGENT_BRIEF.md)
  (fade-in 0.15 с обязателен; fade-out 0.30 с — только через контракт «пул спрашивает разрешение на
  выключение», иначе отклоняется; обязательный грейс-период `dayGate` 0.4 с против thrash, т.к. в первых
  кадрах fade-in `intensity ≈ 0` и пул счёл бы лампу погашенной; resync не должен перезапускать fade).
- **Результат 2026-11-01** (`21fd5a74`, отчёт [`T-LIGHT21_REPORT.md`](T-LIGHT21_REPORT.md), 26 кейсов):
  fade-in 0.15 с **принят** — `t90` 0.107–0.132 с, кадровый скачок 21–24 % против 100 % в контроле, кривая
  монотонна, `dc` совпал во всех 13 парах A/B, ALIC 0.035–0.117 мс/кадр, `gateGraceHeld = 0`, F8/F9 PASS,
  0 errors/shader errors. Fade-out 0.30 с **отклонён как дефолт**: при массовом выходе ламп из набора
  удержание `Light.enabled` доходило до 75 источников (STREET), 73, 59, 30, 22 против гейта ≤16;
  механизм оставлен в коде и выключен (`fadeOutSeconds = 0`), гашение остаётся мгновенным.
- **Визуальный sign-off получен от пользователя 2026-11-01: «визуально всё ок»** — плавное появление,
  мгновенное гашение, flicker интерьеров и переходы часа приняты; L6 закрыт. Остаточный «щелчок» на
  выключение зафиксирован как известное ограничение (не блокер).

### L7 — T-LIGHT22/23: multi-camera + day/night emissive (условно)

- Union только при реальной второй камере; per-camera бюджет; fallback. Day/night emissive `PM_LAMP_GLOW_WARM` — отдельная ветка с knob/перепечкой HLOD ClassLamp.
- T-LIGHT24/25 — **не делать** (Forward+/GRD уже; порталов/PVS нет).
- **Решение 2026-11-01: отложено.** Реальной второй камеры в игре нет (`VisibilityDirector.WorldCameras`
  даёт один риг; union нечего объединять), а day/night emissive без HDR/GI/probes требует отдельного
  визуального ресерча и правки HLOD-импостера. Возвращаться только при появлении второй боевой камеры
  или отдельного запроса на emissive.

---

## 3. Приёмка трека — итог

| Этап | Тикет | Итог | Ключевые числа |
|---|---|---|---|
| L1 | T-LIGHT16 | PASS | baseline: N=2 238, cap 96 связывает, ALIC 5.4–7.1 мс |
| L2 | T-LIGHT17 | PASS | day/night-гейт 46→0 нулевых слотов; split OUT/IN (ADMIN OUT 0→27); radius 100 |
| L2b | T-LIGHT17b | PASS | ALIC **6.81 → 0.16 мс/кадр** (−97.6 %), ~2 239 → ~4 записи/кадр |
| L3 | T-LIGHT18 | PASS | фокус-камера 11/11, поисков 2–6 → 1, реакция 1–4 кадра |
| L4 | T-LIGHT19 | PASS | веер: разворот меняет набор 6/6, «за спиной» 26–77 → 0–1, доля в кадре ×1.1…×5.5, DC без изменений |
| L5 | T-LIGHT20 | PASS | 8 осей A/B, финал = L4 (radius 100 / cap 96 / hyst 0.10 / margin 6° / pre 5° / rear 28 / penalty 1.2), 111 кейсов |
| L6 | T-LIGHT21 | PASS (fade-in) | `t90` 0.107–0.132 с, скачок 21–24 % против 100 %, ALIC 0.035–0.117 мс, fade-out отклонён |
| L7 | T-LIGHT22/23 | отложен | вторая камера отсутствует; emissive — отдельная тема |

**Итоговое состояние света (дефолты в коде, сцена не пересохранена):**
`dayGate=on (eps 0.002)`, `split=on (outMin 40)`, `radius=100`, `maxActive=96`, `hysteresis=0.10`,
`checkInterval=0.25`, фокус-камера с фильтрами и адаптивным триггером (>2 м / >3° / 0.1 с),
веер `halfH/halfV` из FOV + `R_rear 28` + `score = d + 1.2·угол` + `margin 6°` / `pre 5°`,
fade-in 0.15 с (fade-out 0), грейс `dayGate` 0.4 с, владение: `enabled` — пул, `intensity` — ALIC.

**Остаточные темы (вне трека, отдельными тикетами):**
- fade-out (нужен лимит одновременно удерживаемых источников, гейт ≤16; сейчас мгновенное гашение);
- интерьерный бюджет/окклюзия ADMIN (96 из 224 кандидатов, `inFrame` 8 %; веер не решает окклюзию);
- `range` INLIGHT 48.3 м (уменьшение освободило бы бюджет интерьера и тайлы, но правка сцены требует
  одобрения и замера lights-per-tile);
- пространственный индекс для `FindFocusCamera` (O(2 238), 2.4–3.3 мс за вызов);
- input-flake F9 (`no_rebase_plan`, иногда 2 нажатия).

**Дальше — финальная приёмка T-CULL01** (Stage 8 + свет уже приняты визуально; приёмка подтверждает
воспроизводимость метрик в обычном запуске, а не переделывает этапы).

---

## 4. Следующий шаг

**Финальная приёмка T-CULL01 — выполнена 2026-11-01, PASS; T-CULL01 = CLOSED.**
Обычный запуск игры: P1 **2 235** (2 229 ±5 %, повтор 0.0 %), двери >150 м = **0**, L1 **46** / L2 **2**,
`hybridDC` 2 129, F8/F9 PASS, 0 errors; свет-инварианты `ThirdPersonCamera_0`/`camera`,
`intensityZeroActive=0` днём, `activeOUT=43` вечером, `HeldForFade=0`, `GateGraceHeld=0`, fade-in 0.15 с,
`dc` не изменился. Отчёт и runbook —
[`T-CULL01_FINAL_ACCEPTANCE_REPORT.md`](../dev/T-CULL01_FINAL_ACCEPTANCE_REPORT.md).

> **Гигиена стенда (из приёмки):** после световых бенчей перезагружать сцену перед приёмкой.
> `ApplyTuneConfig`/`ApplyFadeConfig` пишут поля компонента в Play Mode, вход в Play Mode идёт без
> перезагрузки сцены, поэтому в живых объектах редактора остаются остаточные A/B-значения
> (`preDeg 8°`, `fadeOut 0.30`), которых нет ни в YAML, ни в дефолтах кода (5° / 0). На DC/двери/HLOD
> это не влияет, но новая сессия редактора даёт принятые значения.
Отчёты света: [`T-LIGHT21_REPORT.md`](T-LIGHT21_REPORT.md) · [`T-LIGHT20_REPORT.md`](T-LIGHT20_REPORT.md) · [`T-LIGHT19_REPORT.md`](T-LIGHT19_REPORT.md) · [`T-LIGHT18_REPORT.md`](T-LIGHT18_REPORT.md) · [`T-LIGHT17B_REPORT.md`](T-LIGHT17B_REPORT.md) · [`T-LIGHT17_REPORT.md`](T-LIGHT17_REPORT.md) · [`T-CULL01_LIGHT_BASELINE_REPORT.md`](T-CULL01_LIGHT_BASELINE_REPORT.md).
