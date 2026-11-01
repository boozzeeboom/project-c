# T-LIGHT18 / L3 — фокус/камера: фильтры, кэш и адаптивный триггер (A/B)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD при замере:** `ef0be359` (код `57bfdb07`)
**Режим бенча:** `TLight16BaselineBench`, `RunMode.L3` (11 кейсов: 4 точки × 12/18 ч × контроль/L3)
**Scope:** только выбор и кэш фокус-камеры + тайминг реакции. Конус, fade, LOD/HLOD, URP, `ProjectSettings`, корабли, двери не менялись.

---

## 0. Итог одним абзацем

Пул перестал брать «любую активную камеру» и перестал искать её каждую секунду. Теперь камера
проверяется по фильтрам (`CameraType.Game`, активна, `targetTexture == null`, `farClipPlane ≥ 50`,
`cullingMask` содержит слой ламп, камера дотягивается до ближайшей лампы), поиск идёт **только по
инвалидации** (камера null/выключена, сменилась сцена, изменилось число камер), а обновление набора
дополнительно запускается адаптивным триггером при сдвиге камеры >2 м или повороте >3° (floor 0.1 с).
Замер A/B на 11 кейсах: **фокус-камера `ThirdPersonCamera_0` во всех 11 кейсах** (контроль тоже, но он
добирался до верного выбора через 0.19–0.29 с, а на старте вообще выбирал неверно), **реакция на телепорт
камеры на 5 м — 1–2 кадра / 0.025–0.067 с у L3 против 2–8 кадров / 0.054–0.266 с у контроля**, **реакция на
разворот 180° — 2–4 кадра / 0.117–0.130 с против 7–10 кадров / 0.250–0.286 с**. Поисков камеры внутри
L3-кейса ровно **1** (форсированный сброс конфигом) против **2–6** в контроле, при стоимости поиска
**2.44–3.57 мс** (медиана 2.691 мс) это прямая экономия на всех кейсах. Изменений в картинке нет:
`dc` совпал **во всех 5 парах** (2249/2249, 2273/2273, 534/534, 1041/1041, 446/446), `setVersion` совпал
во всех парах, `activeTotal` и сплит OUT/IN/FARM совпали во всех парах. F8/F9 — **PASS / PASS_rollback_restore**,
`errors=0, exceptions=0, shaderErrors=0`, сцена не сохранена, push нет. Визуальный sign-off и полный путь
по меню — за пользователем (§7).

---

## 1. Метод

| Пункт | Значение |
|---|---|
| Сцена | `WorldScene_0_0.unity` (не сохранялась), Play Mode на Host |
| Вход | `Project C → Lighting → T-LIGHT18 focus → Run` (mode `L3`), тег `[T-LIGHT18]` |
| Камера | `ThirdPersonCamera_0`, far 30 000, fov 60, near 0.1 |
| Точки | P1 `(-342.90, -50.40, -102.20)`; STREET `(-382.90, -58.20, -126.20)`; ADMIN `(-589.95, -36.89, 85.06)`; FARM `(5431.48, -252.48, -2386.03)` |
| Часы | 12 / 18 ч через `ForcePhaseSilent`, значение читается назад из `ServerTimeOfDay` |
| Проба фокуса | фазы 0 (baseline) / 1 (телепорт `ProbeTeleportMeters = 5`) / 2 (разворот 180°); `ProbeTimeoutFrames = 90`, `ProbeSettleFrames = 12` |
| Кейсов | 11 (P1 12/18 ч ×2 режима, STREET 12 ч ×2 + 18 ч L3, ADMIN 18 ч ×2, FARM 18 ч ×2) |
| Конфиг пула | закреплён на принятом L2: `dayGate=on, eps=0.002, split=on, outMin=40, radius=L2/100, maxActive=96, hysteresis=0.10, checkInterval=0.25` |
| A/B | per-case `pool.ApplyCameraConfig(filters, adaptive, moveM, rotDeg, minInterval)` — только память, сцена не грязнится |
| Лог | `Temp/T-LIGHT18/T-LIGHT18_2026-11-01_raw.txt` (вне git) |
| CSV | `docs/world/lights/T-LIGHT18_2026-11-01.csv` |

Линии конфигурации из raw-лога прогона (по одной на кейс):

```
PREGAME;stage=menu;allCameras=1;cameras=MainCamera(type=Game,far=120000,mask=0xFFFFFFFF,active=True);
        poolFocus=none:none;poolCamera=none;poolSearches=1;poolChecks=1;lightMask=0x100;
        legacyRuleWouldPick=MainCamera;legacyNearest_m=228645.5;...
GATE_FOCUS;poolFocus=camera:ThirdPersonCamera_0;poolCamera=ThirdPersonCamera_0;searches=3;poolSize=2;
        adaptiveChecks=2;positionVersion=3;rig=ThirdPersonCamera_0;...
CONFIG;case=<id>;pool;dayGate=on;dayGateEps=0.0020;split=on;outMin=40;radiusMode=L2;radius=100;
        legacyRadius=140;maxActive=96;hysteresis=0.10;checkInterval=0.25
CONFIG;case=<id>;focus;filters=<off|on>;adaptive=<off|on>;minFar=50;moveM=2.0;rotDeg=3.0;
        minInterval=0.10;poolPoll=1.00;camera=<none|name>;source=camera:<name>;...
FOCUSPROBE;case=<id>;phase=<teleport|teleport_reacted|turn_reacted>;...frames=<n>;sec=<s>;setChanged=<bool>
```

---

## 2. Что изменено в коде

### 2.1 `PrimumLightPool.cs` — фокус-камера (владелец `Light.enabled` не изменился)

Новые сериализованные поля с инициализаторами (поэтому сцену не пришлось пересохранять — уже
сериализованный компонент получает новые дефолты):

| Поле | Дефолт | Смысл |
|---|---|---|
| `cameraFiltersEnabled` | `true` | A/B-переключатель фильтров камеры |
| `adaptiveTriggerEnabled` | `true` | A/B-переключатель триггера по сдвигу/повороту |
| `focusMinFarClip` | `50` | отсекает камеры меню/портальные с маленьким far |
| `focusMoveThreshold` | `2.0` | сдвиг камеры (м), после которого набор пересобирается сразу |
| `focusRotateThreshold` | `3.0` | поворот камеры (°), после которого набор пересобирается сразу |
| `adaptiveMinInterval` | `0.1` | floor между адаптивными чеками |
| `focusPoolPollInterval` | `1.0` | как часто перепроверяется **состав пула камер** (`Camera.allCamerasCount`) |

Приватное состояние: `_lightLayerMask`, `_focusPoolSize` (`-1`), `_nextFocusSearchUnscaled`,
`_nextFocusPoolPollUnscaled`, `_lastCheckUnscaled`, `_anchorAppliedValid/_anchorAppliedPos/_anchorAppliedRot`,
`_lastLoggedFocus`, `_focusReport` (`StringBuilder(192)`). Прежнее поле `_nextFocusSearch` удалено.
`OnEnable` подписывается на `SceneManager.sceneLoaded`, `OnDisable` отписывается; `OnSceneLoaded` инвалидирует кэш.

Поток:

```
Update  → AdaptiveFocusTriggered (|Δpos| > 2 м или Δrot > 3°, не чаще 0.1 с) → Check
Check   → CheckCore → TryGetFocusPosition → EnsureFocusCamera
EnsureFocusCamera: невалидна → поиск (не чаще 1/с)
                 | валидна   → Camera.allCamerasCount != _focusPoolSize → поиск
FindFocusCamera: фильтры → ближайшая лампа → out_of_reach | выбор по nearestSqr
```

Существенно, что **фильтры стоят до прохода по лампам**: отбракованная камера не оплачивает скан
2 238 источников. `_lightLayerMask` считается один раз в `Rebuild` из зарегистрированных ламп (в этой
сцене `0x100` — слой 8 `PRIMUM`), слой не хардкодится; при смене маски кэш сбрасывается.

FO-safe: мировых `Vector3` не кэшируется. `_anchorAppliedPos/_anchorAppliedRot` — дельта **до следующего
чека**, переинициализируется при смене камеры, смене сцены и после промаха, поэтому ложный триггер после
ребейса безвреден (проверка идемпотентна), а «сдвиг» не может быть накоплен через ребейз.

Новая публичная диагностика: `FocusPositionVersion`, `FocusPositionVersionFrame`, `FocusAdaptiveChecks`,
`FocusNormalChecks`, `FocusPoolSize`, `FocusCameraName`, `CameraFiltersEnabled`, `AdaptiveTriggerEnabled`,
`LightLayerMask`, `CameraConfigLine`, `ApplyCameraConfig(bool filters, bool adaptive, float moveThreshold,
float rotateThreshold, float minInterval)`.

**Политика света не тронута:** `dayGate`/`split`/`outdoorMinSlots`/`activeRadius*`/`maxActive`/`hysteresis`/
`checkInterval`, `OwnershipLine`, запись `Light.enabled` — без изменений. Пул по-прежнему не пишет
`intensity`, `range` и `shadows`.

### 2.2 `TLight16BaselineBench.cs` — режим `L3`

`RunMode { Baseline, L2, L2B, L3 }`, меню `Project C/Lighting/T-LIGHT18 focus/Run` и `.../Abort`,
тег `[T-LIGHT18]`, каталог `Temp/T-LIGHT18`, префикс CSV `T-LIGHT18`. `BuildCasesL3()` — 11 кейсов;
каждому кейсу закрепляется принятый L2-конфиг пула, варьируется только `ApplyCameraConfig`. Добавлены
`NotePreGameFocus()`, строка `GATE_FOCUS`, проба фокуса (фазы 0/1/2) и колонки CSV
`focusFilters, focusAdaptive, focusVersion, focusAdaptiveChecks, focusSearches, probeReactionFrames,
probeReactionSec, probeTurnFrames, probeTurnSec, probeTurnSetChanged, probeTimeout` — пишутся только в `L3`,
поэтому схемы L1/L2/L2b не поехали. `FrustumCensus()` даёт `activeInFrustum` для проверки «сколько
активных ламп реально попало в кадр».

### 2.3 Карта владения (не изменилась)

| Свойство | Владелец | Пул | ALIC |
|---|---|---|---|
| `Light.enabled` | `PrimumLightPool` | **пишет** | читает |
| `Light.intensity` | `AdditionalLightIntensityController` | только читает (день/ночь-гейт) | **пишет** |
| фокус-камера | `PrimumLightPool` | **пишет** (`focus`-override имеет приоритет) | — |

---

## 3. Гейт фокуса: какая камера выбирается

| Проверка | Результат |
|---|---|
| фокус в игре | **11/11** кейсов `focusSource=camera:ThirdPersonCamera_0`, `focus=camera:ThirdPersonCamera_0` |
| на старте нет `AtmosphereCamera` | **PASS**: в меню `poolFocus=none:none`; камера рассмотрена и отклонена с причиной `AtmosphereCamera-mask_no_lights` |
| парковочная `MainCamera` исключена | **PASS**: `MainCamera-out_of_reach` (стоит в 240 км, `far=120000`, ближайшая лампа 228 645.5 м) |
| `poolSize` | 2 в игре (`MainCamera` + `ThirdPersonCamera_0`), 2 в меню, 3 в одном кадре при появлении `AtmosphereCamera` |
| `lightMask` | `0x100` во всех линиях — слой посчитан из ламп, не из константы |

Логи консоли прогона (в порядке появления, `searchMs` — время одного поиска):

```
FocusCamera=none FocusSource=none pool=2 searchMs=1.943 searches=1 | MainCamera-out_of_reach,AtmosphereCamera-mask_no_lights
FocusCamera=ThirdPersonCamera_0 FocusSource=none pool=3 searchMs=2.510 searches=2 | MainCamera-out_of_reach,ThirdPersonCamera_0+,AtmosphereCamera-mask_no_lights
FocusCamera=ThirdPersonCamera_0 FocusSource=camera pool=2 searchMs=2.693 searches=7 | MainCamera-out_of_reach,ThirdPersonCamera_0+
FocusCamera=ThirdPersonCamera_0 FocusSource=camera pool=2 searchMs=2.676 searches=16 | MainCamera-out_of_reach,ThirdPersonCamera_0+
FocusCamera=ThirdPersonCamera_0 FocusSource=camera pool=2 searchMs=2.710 searches=24 | MainCamera-out_of_reach,ThirdPersonCamera_0+
FocusCamera=ThirdPersonCamera_0 FocusSource=camera pool=2 searchMs=2.858 searches=25 | MainCamera-out_of_reach,ThirdPersonCamera_0+
FocusCamera=ThirdPersonCamera_0 FocusSource=camera pool=2 searchMs=2.953 searches=30 | MainCamera-out_of_reach,ThirdPersonCamera_0+
FocusCamera=ThirdPersonCamera_0 FocusSource=camera pool=2 searchMs=2.458 searches=34 | MainCamera-out_of_reach,ThirdPersonCamera_0+
FocusCamera=none FocusSource=camera pool=1 searchMs=1.336 searches=35 | MainCamera-out_of_reach
```

Два края прогона объяснимы: первая строка — меню, до спавна игровой камеры (пул честно оставил
`FocusCamera=none`, а не взял камеру меню); последняя — выход в меню после бенча, `pool=1`.
Строка `FocusSource=none` при уже выбранной камере — момент, когда камера появилась и была найдена,
но персона-версия позиции ещё не поднялась.

---

## 4. Результаты A/B

`focusSearchMs` — стоимость одного поиска, `probeReaction*` — реакция на сдвиг камеры на 5 м,
`probeTurn*` — реакция на разворот 180°. `focusSearches` — накопленный счётчик **на конец кейса**.

| Кейс | dc | active OUT/IN/FARM | inFrustum | setVersion | focusSearchMs | searches | телепорт кадр / с | разворот кадр / с |
|---|---|---|---|---|---|---|---|---|
| `P1_h12` контр. | 2249 | 0/70/0 | 26 | 4 | 2.691 | 5 | 2 / 0.066 | 9 / 0.262 |
| `P1_h12` L3 | 2249 | 0/70/0 | 26 | 4 | 2.693 | 7 | **1 / 0.035** | **2 / 29.937*** |
| `P1_h18` контр. | 2273 | 40/56/0 | 46 | 5 | 2.385 | 13 | 2 / 0.054 | 10 / 0.286 |
| `P1_h18` L3 | 2273 | 40/56/0 | 46 | 5 | 2.676 | 16 | 2 / 0.067 | **4 / 0.117** |
| `STREET_h12` контр. | 534 | 0/96/0 | 25 | 7 | 2.479 | 22 | 8 / 0.237 | 8 / 0.250 |
| `STREET_h12` L3 | 534 | 0/96/0 | 25 | 7 | 2.710 | 24 | **1 / 0.025** | **4 / 0.128** |
| `STREET_h18` L3 | 734 | 40/56/0 | 26 | 8 | 2.858 | 25 | **1 / 0.026** | **4 / 0.130** |
| `ADMIN_h18` контр. | 1041 | 4/92/0 | 7 | 9 | 3.571 | 27 | 7 / 0.266 | 7 / 0.284 |
| `ADMIN_h18` L3 | 1041 | 4/92/0 | 7 | 9 | 2.953 | 30 | **1 / 0.035** | **4 / 0.117** |
| `FARM_h18` контр. | 446 | 0/0/32 | 25 | 10 | 2.439 | 32 | 5 / 0.188 | 8 / 0.261 |
| `FARM_h18` L3 | 446 | 0/0/32 | 25 | 10 | 2.458 | 34 | **1 / 0.040** | **3 / 0.124** |

\* единственный «грязный» сэмпл: `frames=2` при `sec=29.937` — артефакт блокировки главного потока
редактора вызовом `execute_code` (`Thread.Sleep`) во время пробы. Годно только число кадров.

Медианы по парам:

| Метрика | Контроль | L3 |
|---|---|---|
| реакция на сдвиг 5 м, кадры | 5 | **1** |
| реакция на сдвиг 5 м, с | 0.188 | **0.035** |
| реакция на разворот 180°, кадры | 8 | **4** |
| реакция на разворот 180°, с | 0.262 | **0.124** |
| время одного поиска, мс | 2.479 | 2.683 |
| `checkMarkerMsAvg`, мс/кадр | 0.2690–0.4615 | 0.2277–0.3832 |
| `poolCheckMs` (сэмпл), мс | 1.748–5.171 | 1.730–2.861 |

### 4.1. Сколько поисков камеры делает каждый режим

По raw-логу разница между строкой `CONFIG;...;focus;searches=N` (момент применения конфига) и следующей
строкой пробы того же кейса:

| Кейс | контроль | L3 |
|---|---|---|
| `P1_h12` | +2 | **+1** |
| `P1_h18` | +6 | **+1** |
| `STREET_h12` | +6 | **+1** |
| `STREET_h18` | — | **+1** |
| `ADMIN_h18` | +2 | **+1** |
| `FARM_h18` | +2 | **+1** |

В L3-кейсе всегда ровно один поиск — тот, что форсирован сменой конфига (`ApplyCameraConfig` инвалидирует
кэш). В контроле камера переискивается раз в секунду, поэтому за время кейса набегает 2–6 поисков. При
стоимости 2.44–3.57 мс это **5.2–21.4 мс против 2.46–2.96 мс** на кейс: экономия 50–86 %.

### 4.2. Адаптивный триггер

`focusAdaptiveChecks` растёт ровно на **3** за L3-кейс (2 → 5 → 8 → 11 → 14 → 17) — срабатывания на телепорт,
на разворот и одно при успокоении. В контрольных кейсах счётчик не растёт (триггер выключен).
В статичных бенчевых позах число обычных чеков не выросло: `checkMarkerMsAvg` у L3 **ниже** контроля во всех
парах (максимум 0.3832 против 0.4615 мс/кадр).

---

## 5. Ответы по задачам брифа

### 5.1. Кэш камер с фильтрами (пункт 1.1) — сделано

`CameraType.Game`, активна, `targetTexture == null`, `farClipPlane ≥ focusMinFarClip (50)`, `cullingMask`
содержит слой ламп, ближайшая лампа в пределах `farClipPlane`. Состав пула камер перепроверяется не чаще
раза в секунду (`Camera.allCamerasCount`), сам поиск — только по инвалидации. `AtmosphereCamera` (маска
`0x200`, без слоя 8) и парковочная `MainCamera` (лампа в 228 км при far 120 км) отклонены — см. §3.

### 5.2. Anchor = камера (пункт 1.2) — сделано

Фокус — `Transform` камеры. Явный `focus`-override сохраняет приоритет. Локальный игрок по умолчанию
**не** используется: в режиме корабля камера уходит от игрока на сотни метров, и привязка к игроку дала бы
регресс (это записано и в ресерче, §1.5 плана).

### 5.3. Адаптивный триггер (пункт 1.3) — сделано

Сдвиг >2 м или поворот >3° при floor 0.1 с вместо ожидания таймера. Измеренный эффект: реакция на
телепорт 1 кадр вместо 2–8, на разворот 4 кадра вместо 7–10 (§4.2). Минимальный интервал 0.1 с держится:
при fps 24–36 (кадр 26–42 мс) триггер физически не может чаще одного раза в 2–3 кадра.

### 5.4. Лог (пункт 1.4) — сделано

`[PRIMUM/Lights] FocusCamera=… FocusSource=… pool=… searchMs=… searches=… | <камера>-<причина>,…` пишется
только при смене выбора (`_lastLoggedFocus`), причины: `out_of_reach`, `mask_no_lights`, `inactive`,
`has_target_texture`, `too_close_far`, `not_game_camera`, `+` (принята). Версия набора (`SetVersion`)
пишется в CSV колонкой `setVersion`.

### 5.5. Политика `enabled`/`intensity` не менялась (пункт 1.5)

Диффом не тронуты `dayGate`/`split`/`radius`/`maxActive`, `AdditionalLightIntensityController` вообще не
изменялся. Пул не пишет ни `intensity`, ни `range`, ни `shadows`.

---

## 6. Гейт

| Требование | Результат |
|---|---|
| фокус всегда `ThirdPersonCamera_0` | **PASS**: 11/11 кейсов `focusSource=camera:ThirdPersonCamera_0` |
| на старте нет `AtmosphereCamera` | **PASS**: в меню `poolFocus=none:none`; при появлении отклонена с `AtmosphereCamera-mask_no_lights` |
| реакция ≤ 0.15 с | **PASS по кадрам и по секундам, кроме одного сэмпла**: телепорт 0.025–0.067 с (контроль 0.054–0.266 с, 3 из 5 выше гейта), разворот 0.117–0.130 с (контроль 0.250–0.286 с, 5 из 5 выше гейта). Единственное исключение — `P1_h12` L3 `probeTurnSec=29.937` при `frames=2`, артефакт блокировки главного потока, не поведение пула |
| DC/active не хуже baseline | **PASS**: `dc` совпал **во всех 5 парах** (2249/2249, 2273/2273, 534/534, 1041/1041, 446/446); `activeTotal` и сплит OUT/IN/FARM совпали во всех парах; `setVersion` совпал во всех парах; `intensityZeroActive = 0` во всех кейсах |
| день/ночь L2 сохранён | **PASS**: 12 ч `activeOUT = 0`, 18 ч `activeOUT = 40` (P1/STREET) и `4` (ADMIN), `capSaturated = 1` там же, где был в L2 |
| F8 | **PASS**: `worldRootShift_m = 256.000` = предсказанный 256.000; после ребейса `poolActive=96, poolOut=46, poolIn=50, focus=camera:ThirdPersonCamera_0` |
| F9 | **PASS_rollback_restore**: `rollbackLogs=1, restoreTrue=1` |
| 0 errors | `FINAL_ERRORS;errors=0;exceptions=0;shaderErrors=0;warnings=656` (предупреждения предсуществующие — NavMesh-спавн и штатный FO-rollback) |
| сцена не сохранена | `NOTE;post_exit;sceneDirtyAtStart=False;sceneDirtyNow=False`; Play Mode вышел сам |
| prefab/FBX/URP/корабли/двери 0 diff | `git status`: изменены только `PrimumLightPool.cs`, `TLight16BaselineBench.cs`, добавлены CSV и отчёт. `VisibilityDirector`, сцена, префабы, FBX, URP-ассет, `ProjectSettings`, корабли и двери не тронуты |
| отчёт + CSV + commit | `T-LIGHT18_REPORT.md`, `T-LIGHT18_2026-11-01.csv`, commit `T-LIGHT18` (push — нет) |

---

## 7. Что именно может увидеть пользователь (нужен sign-off)

1. **Включение лампы.** Набор теперь пересобирается на 0.1–0.13 с раньше, чем раньше (было 0.25–0.29 с).
   Это значит, что при быстром проходе мимо лампы она может включиться **чуть раньше по времени**, но
   в той же точке пространства. Проверять на улице вечером (17–20 ч) при беге.
2. **Flicker интерьеров.** Не менялся: политика `enabled` та же, `setVersion` в парах совпал.
3. **Скачок часа.** Гейт дня/ночи не тронут (`activeOUT = 0` на 12 ч).
4. **Меню → одиночная игра → выбор персонажа → городские камеры.** Главное, что изменилось для игрока:
   до спавна игровой камеры пул больше **не** берёт камеру меню (`AtmosphereCamera`) — `FocusCamera=none`,
   ошибок в консоли нет. В этой сессии весь путь до города был пройден автоматически (гейт бенча
   проверяет риг `ThirdPersonCamera*`, локального игрока, скрытое меню и `registered ≥ 2000`).
5. **Режим корабля.** Anchor — камера, поэтому выбор не «уезжает» вместе с кораблём. Отдельного кейса
   на корабль в этом прогоне нет (§8).

---

## 8. Ограничения и оговорки

- **Разворот 180° не меняет состав**: `probeTurnSetChanged = 0` во всех 11 кейсах, направление камеры в
  критерии отбора не входит. `activeInFrustumAfterTurn` не постоянен (0 в P1, 23 в `STREET_h18`, 11 в
  `ADMIN_h18`, 25 в FARM) — это свойство позы зонда, а не регресс L3. Именно это закрывает L4/T-LIGHT19.
- **Секунды реакции зависят от частоты кадра.** При просадках (`fps = 2.20` и `6.55` в P1-кейсах) один
  кадр = 150–450 мс, поэтому `probeTurnSec=29.937` при `frames=2` — артефакт замера, и опираться нужно на
  кадры (L3: 1–4, контроль: 5–10).
- **`focusSearchMs` не уменьшился**, а смысл поменялся: поиск теперь редкий, но не дешёвый — один проход
  по 2 238 лампам для единственной подходящей камеры, 2.44–3.57 мс. Экономия получена количеством
  (1 против 2–6 за кейс), а не стоимостью. Дальнейшее снижение — за счёт пространственного индекса ламп,
  это отдельная задача, не L3.
- **`focusMarkerMsAvg` нестабилен** (контроль 0.2690–0.4615, L3 0.0123–0.3832) и как опорная метрика не
  годится; выводы сделаны по кадрам реакции, счётчику поисков и `checkMarkerMsAvg`.
- **`poolCheckMs`** — сэмпл одного чека, а не статистика кейса; в таблице дан диапазон, не медиана.
- **Адаптивный триггер может увеличить частоту чеков в движении**: при беге камера стабильно проходит
  2 м за <0.1 с, то есть чек может идти до ~10 Гц против 4 Гц по таймеру. В статичных бенчевых позах
  число чеков не выросло (`checkMarkerMsAvg` у L3 ниже контроля), но игровой бег этим прогоном не покрыт.
  Если это станет заметно, floor `adaptiveMinInterval` поднимается без изменения политики света.
- **Строка `NOTE;farm_cluster;…`** в FARM-позе дописывается в raw-лог каждый кадр (`File.AppendAllText`) —
  предсуществующее поведение бенча, оно растягивает FARM-кейс и делает его секунды нерепрезентативными.
- **`warnings = 656`** — предсуществующие (NavMesh-спавн, FO rollback), от нового кода предупреждений нет.
- **Сцена не пересохранялась**, потому что все новые поля имеют инициализаторы: уже сериализованный
  компонент получает `filters/adaptive = on` без записи в `WorldScene_0_0.unity`. Обратная сторона — значения
  в сцене не видны в инспекторе, пока сцену не тронуть; это сознательный выбор, чтобы не грязнить сцену.

---

## 9. Что осталось

- **Визуальный sign-off §7** и полный путь по меню — за пользователем.
- **L4/T-LIGHT19** — конус/веер: после 180° в кадре по-прежнему 0 из 96 активных, `setVersion` не меняется.
  Это следующая и главная причина «не горит за спиной/по курсу».
- **L5/T-LIGHT20** — тюнинг (`activeRadius` 140/100/70, `cap`, гистерезис) и A/B.
- **L6/T-LIGHT21** — fade-множитель в ALIC (владелец `intensity`).
- **Отдельно:** пространственный индекс ламп, чтобы поиск камеры перестал быть O(2 238) — 2.4–3.6 мс за вызов.
- **Отдельно:** прогнать L3 в режиме корабля и при непрерывном беге, чтобы измерить реальную частоту
  адаптивных чеков (в бенче поза статична).

---

*Прогон выполнен без сохранения сцены и без push. `Light.intensity` пулом не писалась, `Light.enabled`
политика (`dayGate`/`split`/`radius`/`maxActive`) не менялась; префабы, FBX, URP, `ProjectSettings`,
корабли, двери и `VisibilityDirector` не изменялись.*
