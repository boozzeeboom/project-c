# T-CULL01 / финальная приёмка — отчёт (закрытие тикета)

**Дата:** 2026-11-01 · **HEAD:** `bf4de0a5` · **Unity:** 6000.5.2f1 · **URP:** 17.5.0 (Forward+)
**Сцена:** `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — **не сохранялась** (`sceneDirtyAtStart=False`,
`sceneDirtyNow=False` в обоих прогонах) · **Push:** нет
**Scope:** только приёмка и закрытие. Новых оптимизаций нет, Stage 6/7/8 не переделывались, свет-код
(`Light.enabled` — пул, `Light.intensity` — ALIC) не откатывался и не переписывался, URP/ProjectSettings
не менялись.

**Вердикт: численные гейты PASS, T-CULL01 = CLOSED.**
Одна находка среды (остаточные A/B-значения света в памяти редактора, §6) и два открытых наблюдения
(тик culling 2.16 мс на 20 км, F9-флейк ввода) — вынесены отдельно, закрытию не мешают.

---

## 1. Как проверялось

Два Play Mode-прогона подряд, оба — **обычный вход в игру** через кнопку «ОДИНОЧНАЯ ИГРА»
(`ProjectCMainMenuUiHost.OnMenuAction("host")`), с гейтом «мы действительно в игре»:

| Прогон | Инструмент | Кейсов | Что даёт |
|---|---|---:|---|
| 1 | `Project C → Culling → T-CULL01 Acceptance → Probe` (`T_CULL01AcceptanceProbe`) | 2 фазы (12 ч / 18 ч) | свет-инварианты + census дверей по дистанции + DC/hybridDC |
| 2 | `Project C → Culling → Stage 8 Doors → Run Bench` (`FinalValidationBench`, режим Stage 8) | **19/19** | P1, лестница дистанций L0/L1/L2, атрибуция города, ночь, F8/F9 реальным вводом |

Гейт «в игре» (прогон 2, строка `GATE;inGame`):
`rig=ThirdPersonCamera_0; localPlayer=True; liveSession=True; pcmmMenuShown=False;
pcmmAtmosphereCamera=False; cursorLock=Locked`. То есть нового главного меню на экране нет,
локальный игрок заспавнен, камера — игровой риг в городе (проба отдельно зафиксировала
`camPos=(-342.9, -50.4, -102.2)`, `cameraToCityM=0.0` — площадь города).
Проба **не запускалась бы**, если бы меню оставалось на экране: вход ждёт `pcmmMenuShown=False`
и появления рига.

Методика замера не менялась с Stage 4c/6/7/8: Host, Midday (12 ч), `ThirdPersonCamera_0`,
far 30 000, FOV 60, near 0.1, двигатели рига отключены, прогрев 1 500 кадров, успокоение,
медиана 3 сэмплов `UnityStats`; F8/F9 — реальным вводом через `InputSystem.QueueStateEvent`.

---

## 2. Численные гейты

| Гейт брифа | Ожидание | Измерено (2026-11-01, прогон 2) | Вердикт |
|---|---|---:|---|
| L0 P1 (площадь, меню скрыто) | **2 229 ±5 %** (2 117.6–2 340.5) | **2 235**, повтор **2 235** (Δ 0.0 %) | **PASS** (+0.27 %) |
| P1 после F8/F9 | 2 229 | **2 229** | **PASS** |
| P1 с кораблями (реальная игра) | — | 2 245 | — |
| Контроль «политика дверей off» | ~2 341 | 2 347 | −4 за счёт дверей дальше 150 м |
| L1, город, 10 км | **46 ±5 %** (43.7–48.3) | 506 − 460 = **46** | **PASS** |
| L2, город, 20 км | **2** | 441 − 439 = **2** | **PASS** |
| Двери > 150 м в кадре | **0** | **0** во всех кейсах политики on; контроль `P1_doors_off` — **2 633** | **PASS** |
| Двери < 130 м видны | видны | **748** нарисовано / **0** погашено (проба) | **PASS** |
| GRD `hybridDC` | > 0 | **2 129** (P1), `hybridInstances` 4 191 против 17 555 в контроле | **PASS** |
| `srpBatcherDC` | мал | **3** | **PASS** |
| L1/L2 при 3 / 12 км | уровни переключаются | D3k: `L0=5/L1=18`; D5k и D10k: `L1=23`; D20k: `L2=23` | **PASS** |
| F8 | реальный ввод, сдвиг = предсказанный | **прошло с 1-го нажатия**: frameGen 2→3, сдвиг **256.000 м** = predicted | **PASS** |
| F9 | `PASS_rollback_restore`, restore=True | **`PASS_rollback_restore`**, `rollbackLogs=1`, `restoreTrue=1`, сдвиг 0.000 | **PASS** (2-е нажатие, §6) |
| Ошибки | 0 | `errors=0; exceptions=0; shaderErrors=0` | **PASS** |
| Сцена / ассеты | не сохранена, 0 diff | `sceneDirtyNow=False`; `git status` — только новый файл пробы | **PASS** |

### 2.1. Лестница дистанций (день, `dc`)

| Точка | Уровень | DC (`doors_on`) | DC (`city_off`) | город | двери в кадре / > 150 м |
|---|---|---:|---:|---:|---|
| P1 (0 км) | L0 | **2 235** | — | — | 419 / **0** |
| 0.5 км | L0 | 2 182 | 233 | 1 949 | 0 / 0 |
| 3 км | смешанный | 504 | — | — | 0 / 0 |
| 5 км | L1 | 467 | — | — | 0 / 0 |
| 10 км | L1 | 506 (повтор 506) | 460 | **46** | 0 / 0 |
| 20 км | L2 | 441 | 439 | **2** | 0 / 0 |
| 10 км после F8/F9 | L1 | 502 | — | — | 0 / 0 |

Ночь: `N0.5k` 2 176, `N5k` 463 — двери и тени ведут себя как днём.
`cullMsAvg`: 0.72–0.93 мс на L0/L1; **2.16 мс на 20 км** (§6, наблюдение).

### 2.2. Двери (проба, поза P1, 12 ч)

| Проверка | Значение |
|---|---:|
| рендереров слоя 10 в сцене | 5 493 (все `enabled`) |
| погашено политикой (`forceRenderingOff`) | 4 697 |
| < 130 м: нарисовано / погашено | **748 / 0** |
| полоса 130–165 м (гистерезис): нарисовано / погашено | 48 / 145 |
| > 150 м: нарисовано / погашено | **0 / 4 552** |
| `DoorController` / из них с коллайдером | **2 630 / 2 630** |

`Renderer.enabled` политикой не трогается (5 493 включённых рендерера при 4 697 погашенных через
`forceRenderingOff`).

---

## 3. Свет-инварианты (без световых бенчей T-LIGHT16…21)

Проба читает только публичную диагностику `PrimumLightPool` и `AdditionalLightIntensityController`;
`Light.enabled` пишет пул, `Light.intensity` — ALIC, проба не пишет ничего.

| Инвариант | Ожидание брифа | 12 ч (день) | 18 ч (вечер) | Вердикт |
|---|---|---|---:|---|
| `FocusCamera` | `ThirdPersonCamera_0` | `ThirdPersonCamera_0` | `ThirdPersonCamera_0` | **PASS** |
| `FocusSource` | `camera` | `camera` | `camera` | **PASS** |
| `intensityZeroActive` | 0 днём | **0** | 0 | **PASS** |
| `activeOUT` | 40–46 вечером | 0 | **43** | **PASS** |
| `activeIN` / `active` | `active` 30–72 (поза зависит) | 33 / 33 | 33 / 76 | **PASS** |
| `HeldForFade` (установившийся) | 0 | **0** | **0** | **PASS** |
| `GateGraceHeld` (установившийся) | 0 | **0** | **0** | **PASS** |
| `capSaturated` / `capCut` | — | 0 / 0 | 0 / 0 | — |
| fade | enabled, fade-in 0.15 с | `fadeEnabled=True`, `in=0.15`, `fadeInCount=156`, `fadeActive=0` | то же | **PASS** (структурно + L6 `t90` 0.107–0.132 с) |
| `dc` относительно базы Stage 8 | не изменился | 2 245 | 2 265 | **PASS** (2 229 ±1 %) |
| Зарегистрировано источников | 2 238 | 2 238 | 2 238 | — |

Фактический конфиг в прогоне (`CONFIG_POOL`): `dayGate=on (eps 0.002)`, `split=on (outMin 40)`,
`radiusMode=L2 radius=100` (legacy 140 не используется), `maxActive=96`, `hysteresis 0.10`,
`checkInterval 0.25`, `gateGrace 0.40`, веер `margin 6° / rear 28 / penalty 1.2`, `source=camera`.
Расхождение по `preDeg` (8° вместо принятых 5°) — среда, а не проект: см. §6.

---

## 4. Defaults / bootstrap: что поднимается само, а что требует инструментов

| Компонент / настройка | Источник | Авто при старте? | Проверено |
|---|---|---|---|
| GRD `GPUResidentDrawerMode=1`, occlusion=1 | `ProjectC_URP.asset` (git) | **да** (проект) | SerializedObject: `1 / 1` |
| `BrgStripping=2` (KeepAll) | `ProjectSettings/GraphicsSettings.asset` (git) | **да** | SerializedObject: `2` |
| URP: Forward+ (2), shadow 400 м / 4 каскада / soft, HDR off | `ProjectC_URP*.asset` (git) | **да** | `m_RenderingMode=2`, `m_ShadowDistance=400`, `m_ShadowCascadeCount=4` |
| `CityShadowBudget` (off 200 / on 180 м, 1 301 entry) | компонент с `AutoBootstrap` + `RuntimeInitializeOnLoadMethod` | **да**, без ссылки в сцене | `BUDGET;entries=1301;onM=180;offM=200`, `budget_steady;entries=1301` |
| `VisibilityDirector` (1 000 групп, 1 147 направляемых, двери 150 м, гистерезис 0.10) | компонент сцены | **да** | `DIRECTOR;groups=1000;dirMembers=1147;doorCull=True;doorDistM=150` |
| HLOD L1 (23 блока) + L2 импостор | `Resources/HLOD/PRIMUM_HLOD.asset` + `Generated/HLOD/*` (git) | **да** (читает манифест, строит runtime-корень `WorldRoot_0_0/PRIMUM_HLOD`) | `hlodBlocks=23; impostor=True; L1 3000 м / L2 12000 м; hlodRoot worldDelta=0.000` |
| Свет: пул (dayGate/split/radius 100/cap 96/веер/фокус-камера) и ALIC (fade-in 0.15) | код + компоненты сцен | **да** | §3 |
| Слой 10 `PRIMUM_DOORS` | `ProjectSettings/TagManager.asset` (git) | **да** | `layer10 = PRIMUM_DOORS`; в сцене 5 493 дверных рендерера на слое 10 |
| Дверные префабы **на слое 10** | `Prefabs/City/Doors/*.prefab` — **вне git (Lore)** | **нет** | нужен инструмент, см. runbook |
| `Ship_havyII_base.prefab` (коллайдеры/двери/мёрж/LOD) | `Prefabs/Ships/` — **вне git (Lore)** | **нет** | инструменты ремонта/бейка, см. runbook |
| HLOD перезапекание после изменения города | `PrimumHlodBaker` | **нет** | `Stage 7 HLOD → Bake All Blocks + L2` |

---

## 5. Runbook (чистый клон / новый контент)

### 5.1. После клонирования

1. Открыть `Assets/_Project/Scenes/BootstrapScene.unity`, затем `WorldScene_0_0.unity`.
2. **Двери на слой 10** (префабы вне git): меню **Project C → Culling → Door Layer → Apply**.
   Проверка: в сцене 5 493 рендерера на слое 10, 0 на слое 8.
3. **Корабль** (вне git): **Project C → Ships → Repair Heavy Prefab** (после любого reimport FBX
   повторить: 250 `BoxCollider`, 9 `DoorController`, `PilotSeat`/`ModuleSlot`/`CargoVisual` на месте).
4. Проверить, что настройки пришли из git: GRD `mode=1` + occlusion=1 (`ProjectC_URP.asset`),
   `BrgStripping=2` (`GraphicsSettings.asset`), слой 10 (`TagManager.asset`),
   HLOD-манифест `Resources/HLOD/PRIMUM_HLOD` (23 блока + импостор).
5. `CityShadowBudget`, `VisibilityDirector`, свет-пул/ALIC — **auto-bootstrap**, ручных действий нет.

### 5.2. Быстрый regression (≈7 минут)

1. `Project C → Culling → T-CULL01 Acceptance → Probe` — свет-инварианты + двери
   (<130 м видны, >150 м не рисуются), пишет `Temp/T-CULL01-final/acceptance_probe_*.txt`.
2. `Project C → Culling → Stage 8 Doors → Run Bench` — 19 кейсов, пишет
   `08_DATA/V_stage8_2026-11-01.csv`/`_raw.txt` + скриншоты.
3. Приёмка: P1 **2 229 ±5 %**, двери >150 м = **0**, город L1 **46 ±5 %**, L2 **2**,
   `hybridDC > 0`, `srpBatcherDC` ≈ единицы, F8/F9 PASS, 0 errors.

### 5.3. Если P1 > 2 341 или двери появились дальше 150 м

1. **Не отключать системы.** Сначала проверить, что вход был «в игре», а не в меню:
   строка `GATE;inGame` должна содержать `pcmmMenuShown=False`, `localPlayer=True`,
   `rig=ThirdPersonCamera_0` (камера нового меню добавляла +37 DC).
2. Сверить `budgetEntries` (должно быть 1 301) и `doorsForcedOff` (≈4 697 при камере на площади):
   нулевые значения означают, что `CityShadowBudget`/`VisibilityDirector` не поднялись.
3. Проверить слой дверей (`PRIMUM_DOORS` в TagManager, префабы на слое 10) и что GRD не выключен
   (`m_GPUResidentDrawerMode = 1`).
4. Только после этого профилировать Frame Debugger/UnityStats; `layerCullDistances[10]` под GRD
   не действует — дистанция дверей живёт в `VisibilityDirector`, второй владелец не добавляется.

---

## 6. Находки, отклонения и открытые пункты

### 6.1. Остаточные A/B-значения света в памяти редактора (среда, не проект)

В прогоне живые компоненты показывали `conePreActivationDeg = 8` (принято 5) и
`fadeOutSeconds = 0.30` (принято 0, мгновенное гашение). Это **не состояние проекта**:

- в `WorldScene_0_0.unity` и `BootstrapScene.unity` **нет** ни одного из полей светового конфига
  (`dayGateEnabled`, `coneEnabled`, `coneMarginDeg`, `conePreActivationDeg`, `gateGraceSeconds`,
  `fadeEnabled`, `fadeInSeconds`, `fadeOutSeconds`) — проверено чтением YAML;
  у пула в сцене сериализованы только `checkInterval/maxActive/activeRadius/hysteresis/focus`;
- в коде принятые дефолты: `conePreActivationDeg = 5f`, `fadeInSeconds = 0.15f`, `fadeOutSeconds = 0f`
  (коммит `2b734a22`, T-LIGHT21);
- значения в памяти — след световых бенчей L5/L6 (`ApplyTuneConfig`/`ApplyFadeConfig` пишут поля
  компонента в Play Mode). Вход в Play Mode идёт без перезагрузки сцены, поэтому значения остаются
  в живых объектах редактора, сцену не dirty и на диск не попадают.

**Следствие:** новая сессия редактора/перезагрузка сцены даёт принятые 5° и fade-out 0.
Отдельного действия по проекту не требуется; на DC, двери, HLOD, тени и FO это не влияет
(`dc` в прогоне 2 245/2 265 при базе Stage 8 2 229). Пункт вынесен как «гигиена стенда»:
после световых бенчей перезагружать сцену перед приёмкой.

### 6.2. F9-флейк ввода воспроизвёлся

F9 потребовал **2 нажатия** (`pressAttempts=2`), после чего дал `PASS_rollback_restore`,
`rollbackLogs=1`, `restoreTrue=1`, сдвиг 0.000. Это тот же класс отказа (`no_rebase_plan`),
что в Stage 6/7/8; к куллингу и дверям отношения не имеет. По правилу брифа (§3.5) закрытие
не блокирует, но тикет на ввод/F9 остаётся открытым.

### 6.3. Тик culling на L2 вышел за 2 мс

`cullMsAvg` в кейсе 20 км — **2.164 мс** (L0/L1: 0.72–0.93 мс; Stage 8 сообщал 0.66–0.91 мс).
Это единственный кейс выше дизайн-бюджета ≤2 мс; на DC не влияет, на L0 не проявляется.
Наблюдение для отдельного пункта «ранжировать L2-проход/разнести проверки по кадрам».

### 6.4. Открытые пункты (вынесены отдельными тикетами)

- **F9 / input-flake** (`no_rebase_plan`, иногда 2 нажатия) — подсистема ввода, не куллинг.
- **PVS не нужен** — L0 держится на 2 229 DC (> 1 000), отказ от baked PVS подтверждён.
- **Двойное владение тенями ферм** — `Primum_farms.shadowCastingMode` пишут `CityShadowBudget`
  и `ShadowBudget` одновременно (pre-existing, зафиксировано в `DIRECTOR;ownershipConflicts`
  ещё с Stage 6; не исправлялось).
- **Свет вне T-CULL01**: fade-out с лимитом одновременно удерживаемых источников (гейт ≤16),
  интерьерный бюджет/окклюзия ADMIN, `range` INLIGHT 48.3 м (правка сцены требует одобрения),
  пространственный индекс для `FindFocusCamera` (O(2 238)), L7 (multi-camera/emissive) — отложен.
- **Визуальный sign-off** полосы 130–150 м и ночи — получен пользователем ранее (Stage 7/8,
  L1–L6); приёмка его не перепроверяет, поп-ин остаётся на глазах пользователя.

---

## 7. Артефакты

| Файл | Что |
|---|---|
| `Assets/_Project/Editor/Culling/T_CULL01AcceptanceProbe.cs` | **новый, только чтение**: свет-инварианты + census дверей по дистанции, меню `Project C → Culling → T-CULL01 Acceptance` |
| `08_DATA/V_final_2026-11-01.csv` / `_raw.txt` | приёмочный прогон Stage 8-бенча (19/19, F8/F9); принятые артефакты `V_stage8_*` не перезаписаны |
| `Temp/T-CULL01-final/acceptance_probe_182654.txt` | сырые строки пробы света/дверей (в git не идёт) |
| `docs/dev/T-CULL01_STAGE8_REPORT.md`, `T-CULL01_STAGE7_REPORT.md` | предыдущие этапы (без изменений) |

---

## 8. Приёмка брифа

- [x] обычный запуск игры (меню → «Одиночная игра», гейт `pcmmMenuShown=False` + живой риг):
      P1 **2 235** (2 229 ±5 %), повтор 2 235, двери >150 м = **0**, L1 **46**, L2 **2**;
- [x] GRD `hybridDC=2 129` / `srpBatcherDC=3`; `CityShadowBudget` 1 301 entry (200/180);
      `VisibilityDirector` 1 000 групп, двери 150 м; HLOD 23 блока + импостор — **auto-bootstrap**;
- [x] F8 PASS (256.000 = predicted), F9 `PASS_rollback_restore` (2 нажатия), объекты/двери/HLOD на месте;
- [x] свет-инварианты: `FocusCamera=ThirdPersonCamera_0`, `FocusSource=camera`,
      `intensityZeroActive=0` днём, `activeOUT=43` вечером, `HeldForFade=0`, `GateGraceHeld=0`,
      fade-in 0.15 с включён, `dc` не изменился;
- [x] 0 errors / 0 exceptions / 0 shader errors; сцена `isDirty=False`; prefab/FBX/корабли — 0 diff;
- [x] runbook для чистого клона/нового контента записан (§5);
- [x] T-CULL01 закрыт в `10_EXECUTION_PLAN.md` и `T-CULL01_COMPLEX_CODE_HANDOFF.md`;
      README/02 обновлены финальными числами; отчёт и коммит есть;
- [x] открытые пункты вынесены отдельно (§6.4).
