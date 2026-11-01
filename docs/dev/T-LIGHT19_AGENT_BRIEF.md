# T-LIGHT19 — brief: конус/веер (L4)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD:** `27d84557`
**Scope:** только отбор кандидатов в `PrimumLightPool.CheckCore` по направлению камеры (веер + тыловой радиус + score).
Без fade (L6/T-LIGHT21), без тюнинга радиуса/капа (L5/T-LIGHT20), без LOD/HLOD/URP.
**Play Mode:** только с разрешения пользователя (сейчас **не** разрешён). Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/world/lights/T-LIGHT18_REPORT.md` §4, §8 (разворот 180°: `probeTurnSetChanged=0`)
- `docs/world/lights/LIGHT_CULLING_PLAN.md` §L4, §0 (инертные свойства), §1 п.7–8
- `docs/world/lights/LIGHT_CULLING_TIMING_RESEARCH.md` §5.4 «Вариант A», §6 (числа/риски)
- `Assets/_Project/Scripts/World/PrimumLightPool.cs` — `CheckCore` (504–663), `TryGetFocusPosition`/`EnsureFocusCamera`
- `Assets/_Project/Editor/Lighting/TLight16BaselineBench.cs` — `RunMode.L3`, `FrustumCensus`, проба фокуса (фазы 0/1/2)

---

## 0. Факт (после L3/T-LIGHT18)

- Пул фильтрует **только по дистанции**: `onDist = ActiveRadius` (100 м, L2), `offDist = onDist·1.1`
  (гистерезис 0.1); кандидаты и удержание сортируются по `_sqr` (евклидова дистанция от камеры).
  **Направление камеры в критерии отбора не входит.**
- Прямое следствие: разворот 180° не меняет набор — `probeTurnSetChanged = 0` во всех 11 кейсах L3;
  после разворота в кадре **0 из 96** (P1), 23 (STREET_h18), 11 (ADMIN_h18), 25 (FARM_h18) активных.
  `setVersion` при развороте не растёт. Это и есть дефект «не горит там, куда смотрю».
- Фокус уже стабилен: `ThirdPersonCamera_0` 11/11, реакция 1–4 кадра, `focusSearchMs` 2.44–3.57 мс
  (поиск редкий). Следующий рычаг — **угол**, не камера.
- Лампа `range`: обычные 3.5–14 м, у `PRIMUM_INLIGHT` override **48.3 м**; вынос third-person камеры
  от персонажа — единицы метров. Отсюда нижняя граница `R_rear`.

## 1. Задачи

1. **Веер из камеры (FOV-derived, не хардкод).** Считать половину вертикального угла из
   `_focusCamera.fieldOfView * 0.5f`, половину горизонтального — из аспекта:
   `halfH = atan(tan(halfV) · aspect)` (в градусах). При `focus`-override (Transform без камеры) —
   fallback-константа из сериализованного поля с клампом, в лог писать `coneSource=camera|fallback`.
   Forward брать живьём из `transform.forward` (FO-safe, мировых кэшей не хранить).
2. **Критерий кандидата на включение:**
   `d ≤ R_on² И (угол_до_forward ≤ halfH И угол_по_вертикали ≤ halfV)` **ИЛИ** `d ≤ R_rear²`.
   Тыловой радиус — «чтобы за спиной не было чёрной дыры», но его мало: вплотную к камере
   лампы должны оставаться. Стартовое `R_rear = 25–30 м` (не 20 из ресерча: 20 меньше, чем
   вынос + 2·range у INLIGHT с range 48.3 м). Верхняя граница — `min(R_on, 30)`.
3. **Гистерезис по углу и радиусу (не ломая существующий):** включение — `onCone`, удержание —
   `offCone = onCone + coneMarginDeg` (например +5–8°) и `offRadius = R_on·(1+hysteresis)`.
   Уже горящие сохраняют приоритет приёма (как сейчас: pass 0 `_keep`, pass 1 `_cand`).
4. **Score при cap:** сортировка не по чистой дистанции, а по `score = d + anglePenaltyPerDeg · угол`.
   Цель — при заполненном cap фронтальные лампы вытесняют боковые/тыловые. Дефолт штрафа подобрать
   так, чтобы при `d ≈ 100 м` и `угол 60°` score был хуже, чем у фронтальной лампы на 100 м.
5. **Pre-activation против pop-in:** включать набор с запасом (расширенный конус/радиус), чтобы при
   развороте камеры лампа уже горела к моменту попадания в кадр. Настоящий fade — L6, здесь только запас.
6. **Диагностика (обязательна для гейта):**
   - публичные счётчики: `ConeCandidates`, `RearCandidates`, `ActiveBehindOutsideRear`,
     `ActiveInFrustum` (через `FrustumCensus` в бенче), `ConeHalfHDeg`, `ConeHalfVDeg`, `RearRadius`;
   - лог `[PRIMUM/Lights] ... cone=on halfH=.. halfV=.. rear=.. cand/keep=..`;
   - колонки CSV: `coneEnabled, coneHalfH, coneHalfV, rearRadius, coneCand, rearCand, activeBehind,
     activeInFrustum, probeTurnSetChanged, probeTurnFrames, probeTurnSec`.
7. **Сериализованные поля с инициализаторами** (сцена не пересохраняется): `coneEnabled=true`,
   `coneMarginDeg`, `rearRadius`, `anglePenaltyPerDeg`, `coneHalfAngleFallback`. Не менять
   `dayGate/split/outdoorMinSlots/activeRadius*/maxActive(96)/checkInterval/hysteresis`.
8. `Light.enabled` по-прежнему пишет только пул; `intensity` не трогать (владелец — ALIC);
   `ActiveLightChanged` продолжает зваться на каждой смене набора.

## 2. Замер и gate

- Play Mode Host; точки P1 / STREET / ADMIN / FARM; 12 ч и 18 ч; режимы **A/B**:
  `cone off` (контроль = текущие L2+L3) vs `cone on`. Конфиг пула закреплён на принятом L2
  (dayGate on, eps 0.002, split on, outMin 40, radius L2/100, cap 96, hysteresis 0.10).
- Проба фокуса: фаза 0 baseline, фаза 1 телепорт 5 м, фаза 2 разворот 180°; добавить фазу 3 —
  разворот 90° (боковой веер) и фазу 4 — «лампа за спиной в R_rear» (проверка, что тыл не выжжен).
- Метрики: `probeTurnSetChanged` (**ожидается 1**), `activeInFrustum` после разворота
  (контроль 0–25), `activeBehindOutsideRear` (ожидается → 0), `setVersion`, `activeTotal` и сплит
  OUT/IN/FARM, `dc`, `checkMarkerMsAvg`, `poolCheckMs`, `intensityZeroActive`.
- **Gate:**
  - после 180° набор меняется (`setVersion` растёт), `activeInFrustum` ≥ 2× контроля и ≥ 50 % активных;
  - лампы вне веера и вне `R_rear` гаснут, за спиной чёрного провала нет (в `R_rear` свет остаётся);
  - `dc` и `active` не хуже контроля; cap 96 не превышен; `intensityZeroActive = 0` на 12 ч;
  - CPU пула: `checkMarkerMsAvg ≤ 0.5 мс`, `poolCheckMs` без роста > 2×;
  - F8/F9 PASS (`worldRootShift` = predicted), `0 errors`, сцена не сохранена;
  - commit `T-LIGHT19`, отчёт `T-LIGHT19_REPORT.md` + CSV `T-LIGHT19_<date>.csv`, план §1.4.
- **Визуальный sign-off пользователя:** веер при беге/развороте, лампы за спиной, отсутствие
  pop-in и «чёрных провалов», интерьеры не потемнели, вечер 17–20 ч.

## 3. Жёсткие правила

1. Только отбор кандидатов/score в `PrimumLightPool`; fade, LOD/HLOD, URP, `ProjectSettings`,
   `VisibilityDirector`, корабли и двери не трогать.
2. `Light.enabled` — пул; `Light.intensity` — ALIC; пул `intensity` только читает (dayGate).
3. Не менять `maxActive=96`, `activeRadius=100`, `checkInterval=0.25`, dayGate/split — это L5.
4. FO-safe: никаких кэшированных мировых `Vector3`; `forward`/позиции читаются живьём; после F8/F9
   поведение идемпотентно.
5. Play Mode — только по разрешению; сцену не сохранять; push не делать; `.meta`/`.asmdef` руками не писать.
6. Известные артефакты бенча: `NOTE;farm_cluster` пишется каждый кадр (растягивает FARM-кейс);
   `probeTurnSec` при просадке fps нерепрезентативен — опираться на кадры.

**Отчёт:** `docs/world/lights/T-LIGHT19_REPORT.md` + CSV `docs/world/lights/T-LIGHT19_<date>.csv`.
