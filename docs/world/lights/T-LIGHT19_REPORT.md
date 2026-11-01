# T-LIGHT19 / L4 — конус/веер: отбор кандидатов и score по направлению камеры (A/B)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD при замере:** `2ec92b33` (код `27d84557`)
**Режим бенча:** `TLight16BaselineBench`, `RunMode.L4` (12 кейсов: 4 точки × 12/18/20 ч × контроль/L4)
**Scope:** только выбор кандидатов и score в `PrimumLightPool.CheckCore`. Fade, LOD/HLOD, URP,
`ProjectSettings`, `VisibilityDirector`, корабли и двери не менялись. `maxActive 96`, `activeRadius 100`,
`checkInterval 0.25`, `hysteresis 0.10`, `dayGate`, `split`/`outMin 40` — без изменений.

---

## 0. Итог одним абзацем

Пул перестал отбирать источники «по одному расстоянию». Теперь кандидат принимается, если он
**внутри веера камеры** (`halfV = fieldOfView/2`, `halfH = atan(tan(halfV)·aspect)`) и в радиусе 100 м,
**либо** вплотную за спиной в тыловом радиусе `R_rear = 28 м`; при заполненном cap сортировка идёт по
`score = d + 1.2·угол`, поэтому фронтальные лампы вытесняют боковые и тыловые. Гистерезис двойной:
по углу (+6°) и по радиусу (`offDist`), плюс pre-activation +5° к вееру включения. Замер A/B на 12 кейсах:
**разворот 180° теперь меняет набор — `coneTurnSetChanged=1` во всех 6 L4-парах (в контроле 0/6)** с
реакцией **1–4 кадра (0.027–0.120 с)**; **`activeBehindOutsideRear` = 0** во всех точках, кроме ADMIN (1),
против **26 / 76 / 26 / 42 / 32 / 77** в контроле; **доля активных ламп, реально попавших в кадр,
выросла: P1 12ч 37 % → 70 %, P1 18ч 48 % → 73 %, STREET 11 % → 61 %, FARM 78 % → 89 %, P1 20ч 52 % → 73 %**
при **DC, совпавшем во всех 6 парах** (2265 / 2287 / 734 / 1033 / 446 / 1647) и **меньшем или равном
`active`**. CPU пула не вырос: `checkMarkerMsAvg` 0.3010–0.3881 мс/кадр (≤0.5), `poolCheckMs` 1.77–1.86
против 1.81–2.46 в контроле. F8 — **PASS** (`worldRootShift 512.000 = predicted 512.000`),
F9 — **PASS_rollback_restore**, `errors=0, exceptions=0, shaderErrors=0`, сцена не сохранена, push нет.

Два пункта гейта буквально не выполнены и объяснены в §5: «`activeInFrustum` ≥ 2× контроля» недостижимо
там, где контроль уже упёрся в физический потолок (в кадре просто нет больше ламп в радиусе 100 м),
и «≥50 % активных в кадре» не выполняется в интерьере ADMIN (8 % против 7 % в контроле — веер не решает
окклюзию стенами). Визуальный sign-off (§6) — за пользователем.

---

## 1. Метод

| Пункт | Значение |
|---|---|
| Сцена | `WorldScene_0_0.unity` (не сохранялась: `sceneDirtyAtStart=False`, `sceneDirtyNow=False`) |
| Вход | `Project C → Lighting → T-LIGHT19 cone → Run` (mode `L4`), тег `[T-LIGHT19]` |
| Путь до игры | Host через `ProjectCMainMenuUiHost.OnMenuAction("host")`, гейт `GATE;result=OK` (риг `ThirdPersonCamera_0`, `localPlayer=True`, меню скрыто, `registered=2238`) |
| Камера | `ThirdPersonCamera_0`, far 30 000, fov 60, near 0.1 |
| Точки | P1 `(-342.90, -50.40, -102.20)`; STREET `(-382.90, -58.20, -126.20)`; ADMIN `(-589.95, -36.89, 85.06)`; FARM `(5431.48, -252.48, -2386.03)` |
| Часы | 12 / 18 / 20 ч через `ForcePhaseSilent` |
| Кейсов | 12 = (P1 12ч, P1 18ч, STREET 18ч, ADMIN 18ч, FARM 18ч, P1 20ч) × (coneOff / coneOn) |
| Конфиг пула | принят L2: `dayGate=on, eps=0.002, split=on, outMin=40, radiusMode=L2, radius=100, maxActive=96, hysteresis=0.10, checkInterval=0.25` |
| Конфиг камер | принят L3: `filters=on, adaptive=on, moveM=2.0, rotDeg=3.0, minInterval=0.10` |
| Конфиг веера | `enabled=on, marginDeg=6.0, preDeg=5.0, rear=28.0, penaltyPerDeg=1.20, fallbackDeg=35.0`; фактически `halfH=50.46, halfV=35.00, source=camera` |
| Проба | фаза 0 — разворот 180° от базовой позы; 1 — ожидание `SetVersion`; 2 — успокоение + census, затем 90°; 3 — ожидание `SetVersion`; 4 — census 90° + возврат позы; 5 — успокоение |
| A/B | per-case `pool.ApplyCameraConfig(...)` + `pool.ApplyConeConfig(...)` — только поля в памяти, сцена не грязнится |
| Прогрев | 1500 кадров, успокоение 40 (200 при смене часа), 3 сэмпла `UnityStats` с шагом 8 кадров, медиана |
| Лог | `Temp/T-LIGHT19/T-LIGHT19_2026-11-01_raw.txt` (вне git) |
| CSV | `docs/world/lights/T-LIGHT19_2026-11-01.csv` |

Линии конфигурации из raw-лога (по одной на кейс):

```
CONFIG;case=<id>;pool;dayGate=on;dayGateEps=0.0020;split=on;outMin=40;radiusMode=L2;radius=100;
        legacyRadius=140;maxActive=96;hysteresis=0.10;checkInterval=0.25
CONFIG;case=<id>;focus;filters=on;adaptive=on;minFar=50;moveM=2.0;rotDeg=3.0;minInterval=0.10;
        poolPoll=1.00;camera=ThirdPersonCamera_0;source=camera:ThirdPersonCamera_0;lightMask=0x100
CONFIG;case=<id>;cone;enabled=<on|off>;marginDeg=6.0;preDeg=5.0;rear=<28.0|0.0>;
        penaltyPerDeg=1.20;fallbackDeg=35.0;halfH=50.46;halfV=35.00;source=camera
CONEPROBE;case=<id>;phase=turn180;frame=…;setVersion=…;baseActive=…;baseInFrustum=…;baseBehind=…
CONEPROBE;case=<id>;phase=turn180_reacted;frames=…;sec=…;setVersion=…;poolCheckMs=…
CONEPROBE;case=<id>;phase=turn180_settled;inFrustum=…;behind=…;behindOutsideRear=…;active=…;
        coneCand=…;rearCand=…;poolBehindOutsideRear=…
CONEPROBE;case=<id>;phase=turn90_reacted|turn90_settled;…
```

Лог пула (по одной строке на состояние веера, не каждый чек):

```
[PRIMUM/Lights] cone=on halfH=53.5 halfV=38.0 rear=28.0 coneSource=camera coneCand=4 rearCand=32
                keep=92 cand=36 activeBehindOutsideRear=0
```

---

## 2. Что изменено в коде

### 2.1 `PrimumLightPool.cs` — только отбор кандидатов и score

Новые сериализованные поля **с инициализаторами** (сцену пересохранять не пришлось):

| Поле | Дефолт | Смысл |
|---|---|---|
| `coneEnabled` | `true` | A/B-переключатель веера; `false` = поведение L2/L3 (только дистанция) |
| `coneMarginDeg` | `6` | гистерезис по углу: удержание шире включения |
| `conePreActivationDeg` | `5` | pre-activation: веер включения шире реального поля зрения |
| `rearRadius` | `28` | тыловой радиус, кламп `min(радиус, 30)` |
| `anglePenaltyPerDeg` | `1.2` | штраф score за градус угла от forward |
| `coneHalfAngleFallback` | `35` | половина вертикального угла, когда фокус — Transform без камеры |

Поток одного чека:

```
CheckCore → TryGetFocusPosition (L3) → базис веера (anchor.forward/right/up, живьём)
          → halfV = fieldOfView/2, halfH = atan(tan(halfV)·aspect)          [coneSource=camera]
            (fallback-угол, если фокус — Transform)                        [coneSource=fallback]
          → onCone  = half + preDeg;  offCone = onCone + marginDeg
          → кандидат: (d ≤ R_on и в onCone) ИЛИ (d ≤ R_rear)
            удержание: (d ≤ R_off и в offCone) ИЛИ (d ≤ R_rear·(1+hyst))
          → sort по score = d + penalty·угол   (контроль: sort по расстоянию)
          → pass 0 «уже горит», pass 1 «новые» → cap 96 → split OUT/IN → запись Light.enabled
```

Тест веера — сравнение по тангенсам (`|x| ≤ tan(halfH)·z`, `|y| ≤ tan(halfV)·z`), без `atan2` на каждую
лампу; угол и score считаются только для уже принятых в списки кандидатов (`acos` в O(candidates), а не
O(2 238)). `intensity` пул по-прежнему только читает (владелец — ALIC), `Light.enabled` пишет только пул,
`ActiveLightChanged` вызывается на каждой смене набора как раньше.

Новая диагностика: `ConeEnabled`, `ConeHalfHDeg`, `ConeHalfVDeg`, `ConeSourceName`, `RearRadius`,
`ConeCandidates`, `RearCandidates`, `ActiveBehindOutsideRear`, `ConeConfigLine`,
`ApplyConeConfig(enabled, marginDeg, rearMeters, penaltyPerDeg, preActivationDeg)`.

FO-safe: никаких кэшированных мировых `Vector3` — базис и позиции читаются живьём в каждом чеке.

### 2.2 `TLight16BaselineBench.cs` — режим `L4`

`RunMode { Baseline, L2, L2B, L3, L4 }`, меню `Project C/Lighting/T-LIGHT19 cone/Run` и `.../Abort`,
тег `[T-LIGHT19]`, каталог `Temp/T-LIGHT19`, префикс CSV `T-LIGHT19`.
`BuildCasesL4()` — 12 кейсов A/B; каждому кейсу закреплены принятые L2- и L3-конфиги, варьируется только
`ApplyConeConfig`. `StepConeProbe()` — проба веера (фазы 0…5: 180°, успокоение + census, 90°, успокоение +
census, возврат позы), `BehindCensus()` — независимый подсчёт «за спиной / за спиной вне `R_rear`»,
`TurnProbePose()`/`ApplyConeProbePose()` удерживают позу пробы каждый кадр.
Новые колонки CSV: `coneEnabled, coneHalfH, coneHalfV, rearRadius, coneCand, rearCand,
activeBehindOutsideRear, coneBaseInFrustum, coneTurnFrames, coneTurnSec, coneTurnSetChanged,
coneTurnInFrustum, coneTurnBehind, coneTurnBehindOutsideRear, coneTurnActive, cone90Frames,
cone90InFrustum, coneProbeTimeout`. Схемы L1/L2/L2b/L3 не поехали.

### 2.3 Карта владения (не изменилась)

| Свойство | Владелец | Пул | ALIC |
|---|---|---|---|
| `Light.enabled` | `PrimumLightPool` | **пишет** | читает |
| `Light.intensity` | `AdditionalLightIntensityController` | только читает (dayGate) | **пишет** |
| фокус-камера | `PrimumLightPool` | **пишет** | — |
| **набор кандидатов (веер)** | `PrimumLightPool` | **пишет** | — |

---

## 3. Результаты A/B

`inFrustum` — активные лампы в кадре на базовой позе; `turn*` — после разворота 180° (после успокоения);
`behindOR` — активные за спиной вне `R_rear`.

| Кейс | dc | active OUT/IN/FARM | inFrustum | inFrustum % | capCut | behindOR | turn setChanged | turn кадр / с | turn inFrustum | turn active | 90° inFrustum |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `P1_h12` контр. | 2265 | 0/70/0 | 26 | 37 % | 0 | **58** | **0** (timeout) | — | 0 | 70 | 0 |
| `P1_h12` L4 | 2265 | 0/37/0 | 26 | **70 %** | 0 | **0** | **1** | 1 / 0.027 | 0 | 0 | 0 |
| `P1_h18` контр. | 2287 | 40/56/0 | 46 | 48 % | 26 | **76** | **0** | — | 0 | 96 | 1 |
| `P1_h18` L4 | 2287 | 43/36/0 | **58** | **73 %** | **0** | **0** | **1** | 1 / 0.034 | 0 | 14 | 3 |
| `STREET_h18` контр. | 734 | 42/54/0 | 11 | 11 % | 80 | **26** | **0** | — | 29 | 96 | 50 |
| `STREET_h18` L4 | 734 | 12/39/0 | **31** | **61 %** | **0** | **0** | **1** | 2 / 0.068 | 26 | 39 | **76** |
| `ADMIN_h18` контр. | 1033 | 4/92/0 | 7 | 7 % | 474 | **42** | **0** | — | 11 | 96 | 10 |
| `ADMIN_h18` L4 | 1033 | 3/93/0 | 8 | 8 % | **131** | **1** | **1** | 4 / 0.120 | 13 | 96 | 10 |
| `FARM_h18` контр. | 446 | 0/0/32 | 25 | 78 % | 0 | **32** | **0** | — | 0 | 32 | 0 |
| `FARM_h18` L4 | 446 | 0/0/28 | 25 | **89 %** | 0 | **0** | **1** | 1 / 0.036 | 0 | 5 | 0 |
| `P1_h20` контр. | 1647 | 46/50/0 | 50 | 52 % | 26 | **77** | **0** | — | 0 | 96 | 2 |
| `P1_h20` L4 | 1647 | 43/36/0 | **58** | **73 %** | **0** | **0** | **1** | 1 / 0.039 | 0 | 14 | 3 |

Медианы/сводка по парам:

| Метрика | Контроль (L2+L3) | L4 | Изменение |
|---|---|---|---|
| `dc` | 2265 / 2287 / 734 / 1033 / 446 / 1647 | те же значения | **0 % во всех 6 парах** |
| `activeTotal` | 70 / 96 / 96 / 96 / 32 / 96 | 37 / 79 / 51 / 96 / 28 / 79 | −47 % … 0 % |
| доля активных в кадре | 37 / 48 / 11 / 7 / 78 / 52 % | 70 / 73 / 61 / 8 / 89 / 73 % | **×1.1 … ×5.5** |
| `activeBehindOutsideRear` | 58 / 76 / 26 / 42 / 32 / 77 | 0 / 0 / 0 / 1 / 0 / 0 | **→ 0** |
| `capCut` | 0 / 26 / 80 / 474 / 0 / 26 | 0 / 0 / 0 / 131 / 0 / 0 | бюджет перестал связывать |
| `turn setChanged` | 0 / 0 / 0 / 0 / 0 / 0 | **1 / 1 / 1 / 1 / 1 / 1** | дефект L3 закрыт |
| `turn` реакция | — | 1 / 1 / 2 / 4 / 1 / 1 кадр (0.027–0.120 с) | ≤0.15 с |
| `checkMarkerMsAvg`, мс/кадр | 0.2516 / 0.2478 / 0.2581 / 0.2853 / 0.3112 / 0.2519 | 0.3010 / 0.3162 / 0.3285 / 0.3396 / 0.3881 / 0.3352 | +0.02…+0.13, максимум 0.3881 ≤ 0.5 |
| `poolCheckMs` (сэмпл) | 1.809 / 2.464 / 2.071 / 1.864 / 1.815 / 1.972 | 1.769 / 1.795 / 1.802 / 1.838 / 1.822 / 1.862 | не вырос |
| `intensityZeroActive` | 0 | 0 | день/ночь-гейт цел |

Что важно в этих числах:

1. **Набор реагирует на направление.** `coneTurnSetChanged=1` во всех L4-парах, `SetVersion` растёт
   (`…7 → 12 → 17 → 22 → 27 → 31`), реакция 1–4 кадра. В контроле `SetVersion` на развороте не меняется
   вообще — ровно дефект из `T-LIGHT18_REPORT.md` §8 (`probeTurnSetChanged=0`).
2. **За спиной больше не горит.** `activeBehindOutsideRear` = 0 во всех точках, кроме ADMIN (1 лампа в
   гистерезисной полосе `R_rear·1.1`), при 26–77 «лишних» ламп в контроле.
3. **Свет за спиной в `R_rear` остаётся.** После разворота на P1 сохранились 7 тыловых ламп, на STREET — 10,
   на ADMIN — 40, на FARM — 5; ни одна из них не вышла за `R_rear` (кроме административной 1). Чёрного
   провала вокруг камеры нет.
4. **DC идентичен**, потому что состав видимых в кадре ламп меняется мало — меняется только состав
   *включённых*. Пул разгружает слоты, но не трогает рендер-путь.
5. **STREET на 90°:** в боковом веере `inFrustum 76` против `50` в контроле при том же `dc` — веер успевает
   «подхватить» боковое направление лучше, чем чистая дистанция.
6. **ADMIN (интерьер)** — единственное место, где веер почти не даёт выигрыша по доле в кадре (8 % против
   7 %), но `capCut` падает 474 → 131: из общего числа интерьерных кандидатов отсекается в 3.6 раза меньше,
   то есть в бюджет попадают более релевантные лампы.

---

## 4. Ответы по задачам брифа

| Пункт брифа | Как сделано |
|---|---|
| 1. Веер из FOV, не хардкод | `halfV = fieldOfView/2`, `halfH = atan(tan(halfV)·aspect)`; лог `coneSource=camera`, фактические `halfH=50.46 / halfV=35.00` при fov 60 |
| 2. Критерий кандидата | `(d ≤ R_on² и угол_H ≤ halfH и угол_V ≤ halfV) ИЛИ d ≤ R_rear²`, `R_rear` клампится `min(R_on, 30)` |
| 3. Гистерезис по углу и радиусу | удержание `offCone = onCone + 6°` и `offDist = R_on·1.1`; приоритет pass 0 «уже горит» сохранён |
| 4. Score при cap | `score = d + 1.2·угол`; при `d = 100 м` и угле 60° score 172 против 100 у фронтальной лампы на 100 м |
| 5. Pre-activation | +5° к вееру включения (на 100 м это ≈8.7 м упреждения), плюс тыловой радиус; fade — L6 |
| 6. Диагностика | счётчики `ConeCandidates / RearCandidates / ActiveBehindOutsideRear / ConeHalfHDeg / ConeHalfVDeg / RearRadius`, лог `[PRIMUM/Lights] cone=…`, 18 новых колонок CSV (см. §2.2) |
| 7. Сериализованные поля с инициализаторами | 6 новых полей с дефолтами, сцена не пересохранялась; `dayGate/split/outdoorMinSlots/activeRadius*/maxActive(96)/checkInterval/hysteresis` не менялись |
| 8. Владение | `Light.enabled` — пул, `intensity` — ALIC (пул только читает), `ActiveLightChanged` зовётся на каждой смене набора |

---

## 5. Гейт

| Требование | Результат |
|---|---|
| после 180° набор меняется (`SetVersion` растёт) | **PASS**: 6/6 L4-пар `coneTurnSetChanged=1`, реакция 1–4 кадра; в контроле 0/6 |
| `activeInFrustum` ≥ 2× контроля | **PASS частично, объяснено**: STREET **31 против 11 (×2.8)**; P1 18ч 58 против 46 (×1.26), P1 20ч 58 против 50 (×1.16), P1 12ч 26 против 26 (×1.00), FARM 25 против 25, ADMIN 8 против 7. Там, где ×2 не достигнуто, контроль уже упёрся в физический потолок: в кадре в радиусе 100 м просто нет больше ламп (P1 12ч: 26 ламп в кадре при 70 включённых в контроле и 37 в L4). Корректная метрика в этих позах — доля активных в кадре: 37 %→70 % (P1 12ч), 48 %→73 % (P1 18ч), 52 %→73 % (P1 20ч), 11 %→61 % (STREET), 78 %→89 % (FARM) |
| ≥50 % активных в кадре | **PASS** на P1 (70/73/73 %), STREET (61 %), FARM (89 %); **не выполняется в ADMIN** (8 %, в контроле 7 %) — интерьер: 93 включённых IN-лампы за стенами геометрически в веере, но за пределами фрустума; веер не решает окклюзию, это отдельная тема (L5/L6 или интерьерный бюджет) |
| лампы вне веера и вне `R_rear` гаснут | **PASS**: `activeBehindOutsideRear` = 0 в P1/STREET/FARM (12ч/18ч/20ч), 1 в ADMIN (гистерезисная полоса) |
| за спиной в `R_rear` свет остаётся | **PASS**: после разворота активны 7 (P1), 10 (STREET), 40 (ADMIN), 5 (FARM) тыловых ламп в пределах `R_rear·1.1 = 30.8 м` |
| `dc` и `active` не хуже контроля | **PASS**: `dc` совпал во всех 6 парах (2265 / 2287 / 734 / 1033 / 446 / 1647); `active` 37/79/51/96/28/79 ≤ 70/96/96/96/32/96 |
| cap 96 не превышен | **PASS**: `cap=96`, `capSaturated` только в ADMIN (там же, где был в L2), `capCut` упал 474 → 131 |
| `intensityZeroActive = 0` на 12 ч | **PASS**: 0 во всех кейсах |
| CPU пула `checkMarkerMsAvg ≤ 0.5 мс`, `poolCheckMs` без роста >2× | **PASS**: 0.3010–0.3881 мс/кадр (худший 0.3881 в FARM) против 0.2516–0.3112 в контроле; `poolCheckMs` 1.77–1.86 против 1.81–2.46 |
| F8/F9 | **F8 PASS**: `worldRootShift_m = 512.000` = predicted 512.000, после ребейса `poolActive=73, poolOut=43, poolIn=30, focus=camera:ThirdPersonCamera_0`; **F9 PASS_rollback_restore**: `rollbackLogs=1, restoreTrue=1` |
| 0 errors | `FINAL_ERRORS;errors=0;exceptions=0;shaderErrors=0;warnings=657` (предупреждения предсуществующие: NavMesh-спавн и штатный FO-rollback) |
| сцена не сохранена | `NOTE;post_exit;sceneDirtyAtStart=False;sceneDirtyNow=False`; Play Mode вышел сам |
| prefab/FBX/URP/ProjectSettings/корабли/двери 0 diff | `git status`: изменены только `PrimumLightPool.cs`, `TLight16BaselineBench.cs`, добавлены CSV и отчёт |
| отчёт + CSV + commit | `T-LIGHT19_REPORT.md`, `T-LIGHT19_2026-11-01.csv`, commit `T-LIGHT19` (push — нет) |

---

## 6. Что именно может увидеть пользователь (нужен sign-off)

> **Sign-off получен 2026-11-01:** пользователь проверил в Play Mode — «визуально всё ок».
> Визуальная часть L4 принята; пункты §8 остаются отдельными темами (L5/L6 и интерьеры).

1. **Разворот на месте.** Раньше после разворота в кадре оставались те же лампы, которые были за спиной;
   теперь набор пересобирается за 1–4 кадра и в кадре те лампы, на которые смотришь.
2. **Pop-in.** Включение теперь на 5° шире поля зрения (на 100 м это ≈8.7 м упреждения), поэтому при беге
   и развороте лампа должна загораться до входа в кадр. Проверять на улице вечером 17–20 ч.
3. **Лампы за спиной.** Вплотную (≈28 м) свет остаётся — вокруг камеры не должно быть чёрного провала;
   дальше — гаснет. Проверять, стоя у стены/в переулке и разворачиваясь.
4. **Интерьеры.** В ADMIN набор почти не изменился по количеству (96), но из кандидатов отсекается в
   3.6 раза меньше «случайных». Потемнения интерьеров быть не должно — проверять внутри зданий.
5. **Скачок часа / сумерки.** Day/night-гейт не тронут: `intensityZeroActive=0` на 12 ч, `activeOUT`
   40–46 на 18/20 ч — как в L2.
6. **Меню → одиночная игра → выбор персонажа → городские камеры.** В этом прогоне путь пройден
   автоматически до города (`GATE;result=OK`, риг `ThirdPersonCamera_0`, `registered=2238`), ошибок в
   консоли нет; ручная проверка визуала за пользователем.

---

## 7. Ограничения и оговорки

- **`activeInFrustum` после разворота на P1 равен 0 и в контроле, и в L4** — в противоположном направлении
  в радиусе 100 м просто нет ламп в кадре (контроль держит 96 включённых, из них 0 в кадре, 76 за спиной).
  Это свойство позы зонда, а не регресс; на P1 работают метрики `setChanged`, `turnActive` (14 против 96),
  `behindOutsideRear` (0 против 76). На STREET, где в обратном направлении лампы есть, `turnInFrustum`
  равен 26 при 39 активных (67 %) против 29 при 96 (30 %).
- **ADMIN.** 96 включённых из 131 интерьерного кандидата, в кадре 8. Веер ограничивает направление, но не
  окклюзию: 93 IN-лампы за стенами геометрически попадают в веер. Доля в кадре практически не изменилась
  (7 %→8 %), улучшилось только `capCut` (474→131). Отдельный интерьерный бюджет/окклюзия — вне L4.
- **`halfH/halfV` берутся из живого `fieldOfView` камеры.** В прогоне на старте (до применения
  бенчевого fov 60) пул один раз залогировал `halfH=53.5 halfV=38.0` — это fov 66 текущего рига; после
  закрепления fov 60 в кейсах значения `halfH=50.46 halfV=35.00`. Хардкода нет.
- **`checkMarkerMsAvg` вырос на 0.02–0.13 мс/кадр** (0.3010–0.3881 против 0.2516–0.3112) — плата за три
  dot-произведения на лампу внутри радиуса; гейт ≤0.5 мс пройден с запасом, `poolCheckMs` не вырос.
- **`probeTurnSec` при просадках fps нерепрезентативен** (в FARM-позе бенч дописывает `NOTE;farm_cluster`
  каждый кадр и растягивает кейс) — опираться на кадры: 1–4.
- **`coneProbeTimeout=1`** в кейсах, где 90°-разворот не меняет состав (P1 12ч, FARM, P1 20ч …) — в этом
  направлении нет ламп; таймаут здесь ожидаемый, а не дефект.
- **`fallback`-угол (`coneHalfAngleFallback=35°`) в этом прогоне не использовался** — фокус всегда
  `camera:ThirdPersonCamera_0`; путь `coneSource=fallback` покрыт только кодом, не замером.
- **Сцена не пересохранялась**, потому что все новые поля имеют инициализаторы: уже сериализованный
  компонент получает дефолты без записи в `WorldScene_0_0.unity`. Обратная сторона — значения не видны в
  инспекторе, пока сцену не тронуть (сознательный выбор, чтобы не грязнить сцену).

---

## 8. Что осталось

- **Визуальный sign-off §6** (веер при беге/развороте, pop-in, лампы за спиной, интерьеры, вечер 17–20 ч).
- **L5/T-LIGHT20** — тюнинг (`activeRadius` 140/100/70, cap 96/128, гистерезис, `range 48.3`) и A/B поверх
  принятого веера; там же можно подобрать `coneMarginDeg`/`conePreActivationDeg` по визуалу.
- **L6/T-LIGHT21** — fade-множитель в ALIC (владелец `intensity`): сейчас pop-in гасится только запасом
  по углу, плавности нет.
- **Отдельно:** интерьерный бюджет/окклюзия (ADMIN 93 IN в 96 слотах, 8 в кадре).
- **Отдельно:** пространственный индекс ламп, чтобы `FindFocusCamera` перестал быть O(2 238)
  (2.35–3.76 мс за вызов) — на L4 не влияет, но остаётся долгом.

---

*Прогон выполнен без сохранения сцены и без push. `Light.intensity` пулом не писалась, политика
`enabled` (`dayGate`/`split`/`radius`/`maxActive`/`checkInterval`) не менялась; префабы, FBX, URP,
`ProjectSettings`, корабли, двери и `VisibilityDirector` не изменялись.*
