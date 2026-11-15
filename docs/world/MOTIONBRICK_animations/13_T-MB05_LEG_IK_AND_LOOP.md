# T-MB05 — 2-bone IK ног и non-cyclic pelvis (loop:false)

> **Дата:** 2026-11-16
> **Ветка:** `main`
> **HEAD до работ:** `0fa9ae42` (`T-MB04c: audit notes, save side effect and plan status`)
> **Коммит:** `87102426`
> **Статус:** код + замеры готовы; baseline 12/12; Play Mode **не запускался**. Визуал (стопы/колени/T-pose) — за пользователем.
> **Связанные:** [11_T-MB03_BAKER_AND_GUARDS.md](11_T-MB03_BAKER_AND_GUARDS.md) · [12_T-MB04C_ROOT_MOTION_IN_PLAYER.md](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [baseline/BASELINE_MANIFEST.md](baseline/BASELINE_MANIFEST.md) · [HANDOFF.md](HANDOFF.md) · design: `docs/dev/T-MB05_LEG_IK_LOOP_DESIGN.md`

---

## 1. Что требовалось (acceptance)

| Пункт | Требование | Статус |
|---|---|---|
| 2-bone IK | Контакт стоп в допуске (hover ≤ 5, penetration ≤ 2), таз ближе к 2 см | ✅ `WalkForward`: таз **15.7 → 1.7 см**, humanoid hover **4.7** / penetration **1.9**; `WalkToJog`: **5.1 → 2.8 см**, **1.7 / 0.9** |
| loop:false | One-shot не портит начало/конец | ✅ sidecar `loop:false` → `loopTime=false`, `WrapMode.Clamp`; нециклический решатель не тянет шов (self-test) |
| NPC baked pool | Без runtime neural, работает на всех клиентах | ⏭ **вне этой сессии** по решению пользователя; `allowNpc=false` в settings не менялся. NPC pool — отдельный шаг |
| baseline | PASS до/после | ✅ 12/12 до и после |
| culling после body swap | `AlwaysAnimate` для локального игрока | ⏭ отдельное решение (не в T-MB05) |

Правка baseline-файлов (`NetworkPlayer.cs`, префабы, контроллеры, `NpcBrain`, `SkillAnimationPlayer`, `Combat`, `Packages/*`) **не выполнялась**.

---

## 2. Файлы

| Файл | Что | Baseline |
|---|---|---|
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksLegIk.cs` | **новый**: аналитический 2-bone IK + self-test | — |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksLoopSelfTest.cs` | **новый**: self-test sidecar `loop` + `AnimationClipSettings` | — |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksBakeJob.cs` | `legIk`, `loop` в sidecar/job; `ReportSchema=3` | — |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksBakeReport.cs` | поля leg IK/loop/raw-vs-humanoid | — |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksFootIk.cs` | `cyclic` в `PelvisSmooth`, `SolveCurve` (верхняя граница + bob), self-test шва | — |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksSpikeBaker.cs` | leg IK в бейке, `loopTime`, humanoid-бейк через сэмпл generic-клипа без Animator | — |
| `docs/dev/T-MB05_LEG_IK_LOOP_DESIGN.md` | design до кода | — |

Сгенерированные `.anim`/отчёты — Lore, в git не идут. GUID клипов **стабильны** (`ea45…`, `6ccc…`, `dc5f…`, `971e…`).

---

## 3. 2-bone IK: что и почему

`PelvisSmooth` (T-MB03) держит контакт, опуская таз вслед за нижней стопой. Поэтому размах таза
повторяет размах стопы: `WalkForward` 15.7 → 11.7 см при цели ~2 см. Колено не участвует.

T-MB05 добавляет обратную задачу для опорной ноги:

1. Для каждого кадра снимаются мировые позиции/повороты `B-thigh/B-shin/B-foot`, опорная стопа — нижняя.
2. Считается **достижимость**: насколько высоко можно держать таз, чтобы опорная нога дотянулась до земли полным выпрямлением. Это верхняя граница `hi[f] ≤ 0` (выше rest таз не поднимаем).
3. Кривая таза = `min(sourceBob[f], hi[f])` + сглаживание. `sourceBob` берётся из вертикали корня источника и применяется только если её размах ≤ **6 см** (иначе это «прыжок» синтеза, а не походка). Для `WalkForward` (22.4 см) bob отклонён; для `WalkToJog` (4.4 см) сохранён.
4. Опорная стопа ставится на `groundY` (rest-высота кости стопы); маховая — только поднимается, если провалилась под землю. Решение — закон косинусов, плоскость сгиба — текущее колено (fallback — forward персонажа), мировой поворот стопы сохраняется.
5. `locals` для thigh/shin/foot пересчитываются; generic и humanoid клипы пекутся из одного и того же набора.

Откат: sidecar `{ "legIk": false }` → прежний `PelvisSmooth` (код не удалён). Гейт `groundFeet:false` (прыжки/полёт) по-прежнему выключает весь foot IK.

### 3.1. Замеры (bake + validate, 16.11)

| Клип | Таз до | Таз после | knee max | raw hover/pen | humanoid hover/pen | source bob | reach-limited |
|---|---|---|---|---|---|---|---|
| `WalkForward_Male` | 15.73 см | **1.72 см** | 48.2° | 0.09 / 0.00 см | **4.70 / 1.92 см** (в допуске) | 22.41 см → отклонён | 1 кадр |
| `WalkToJog_Male` | 5.10 см | **2.81 см** | 38.5° | 0.09 / 0.00 см | **1.70 / 0.90 см** (в допуске) | 4.42 см → применён | 5 кадров |

`humanoid` — то, что видит игрок (валидатор). `raw pose` — поза ретаргета до humanoid-конверсии.
Разница 4.7 см у `WalkForward` — известное ограничение humanoid-аватара: мышцы `Lower Leg Stretch`
выходят за `[-1..1]` (0.00..1.03) и клампятся, поэтому humanoid-клип не повторяет контакт generic-клипа
один-в-один. Контакт всё равно в допуске валидатора; это кандидат в отдельный humanoid post-pass.

### 3.2. Почему прежний humanoid-клип был T-позой (найдено и исправлено)

`HumanPoseHandler.GetHumanPose` на инстансе **с Animator** читает внутреннюю T-позу, а не
трансформы, которые бейкер выставил напрямую. Симптом: `Shoulder Front-Back=0`, `Arm Down-Up=0.384`,
`Stretch=1.0` при реальной ходьбе; humanoid-клип двигал руки/ключицы неправильно.
Исправление: humanoid-бейк сэмплирует **готовый generic-клип** через `AnimationClip.SampleAnimation`
на временном инстансе, у которого **удалён Animator** (Avatar остаётся для `HumanPoseHandler`).
После этого `bodyPosition.y` и мышцы соответствуют позе; `B-shoulder/upperArm` снова двигаются.

Важно: при активном `Animator` с Humanoid-аватаром generic-кривые **не применяются** к скелету
(`SampleAnimation`/`AnimationMode` молча ничего не делают) — это и был источник T-позы.

---

## 4. `loop` / non-cyclic

- Sidecar: `{ "loop": false }` (default `true`).
- `MotionBricksFootIk.Solve(..., cyclic)` и `SolveCurve(..., cyclic)` не заворачивают соседей через шов; края берут сами себя.
- `ApplyLoopSettings(clip, loop)`: `WrapMode.Loop` + `loopTime=true` либо `WrapMode.Clamp` + `loopTime=false`. Это попадает в ассет и переживает `SaveClipInPlace` (GUID стабилен).
- Runtime `MotionBricksBakedPlayback._loop` уже управляет ручной намоткой; для one-shot его выставляют `false` (`ConfigureForReview(loop:false)`).
- Self-test: профиль «все кадры на земле, последний +10 см» — cyclic тянет первый кадр на −1.0 см, non-cyclic оставляет 0.0 см; конец корректируется на −7.0 см (hover 3.0 / penetration 1.0 — в допуске). `SolveCurve` с верхней границей: cyclic −9.6 см на старте, non-cyclic 0.0 см.

---

## 5. Самопроверки (без Play Mode)

| Меню | Результат |
|---|---|
| `Bake/Foot IK self-test` | **6/6 PASS** (плоский контакт, эталон 2 см, плохой клип 15.8 → 11.8, смена опоры, one-shot шов, кривая таза) |
| `Bake/Leg IK self-test` | **6/6 PASS** (достижимая цель, сгиб колена, ориентация стопы, прямая нога без NaN, недостижимая цель, no-op) |
| `Bake/Loop settings self-test` | **4/4 PASS** (дефолты sidecar, `loop:false`, clip one-shot, clip loop) |
| `Bake/applyRootMotion guard self-test` | **6/6 PASS** (T-MB03 guard не сломан) |
| `Bake/Bake + validate all discovered` | 2 испечено, 0 ошибок; оба клипа FAIL по контенту (ось/асимметрия/разворот), контакт PASS |

---

## 6. Валидатор: что изменилось и что осталось контентом

| Метрика | `WalkForward` | `WalkToJog` | Комментарий |
|---|---|---|---|
| hover / penetration | **4.70 / 1.92** | **1.70 / 0.90** | в допуске (5 / 2) |
| таз | 1.72 см | 2.81 см | цель ~2 см достигнута |
| foot slide | 2.36 см/кадр | 62.78 см/кадр | **выросло** у WalkForward: стопа теперь реально стоит больше кадров, и XZ-движение клипа внутри контакта непостоянно. Это контент, порог не ослаблялся; кандидат на foot-lock post-pass |
| ось шага | 78.4° | 101.0° | контент (FAIL) |
| асимметрия опоры | 32 % | 53 % | контент (FAIL) |
| дрейф рыскания | 0.0° | −360.0° | `WalkToJog` в источнике разворачивается (FAIL) |
| ключицы/руки | двигаются | двигаются | humanoid T-поза исправлена |

`WalkForward`/`WalkToJog` по-прежнему **FAIL** по оси/асимметрии/развороту — это качество
синтезированного контента, не пайплайна. Для go/no-go нужны 3–5 клипов с собственными seed/target.

---

## 7. Риски / что осталось

| # | Риск | Куда |
|---|---|---|
| 1 | **Визуал не проверен.** Сгиб колена до 48° и новая кривая таза — глазами пользователя. | T-MB08 / пользователь |
| 2 | **Humanoid vs generic контакт:** 4.7 см hover у `WalkForward` при 0.1 см в generic. Причина — кламп `Lower Leg Stretch` (0.00..1.03). В допуске, но запас маленький. | Отдельный humanoid post-pass (feedback по стопам) |
| 3 | **Foot slide 2.36** у `WalkForward` — стопа стоит больше кадров; XZ клипа непостоянно. Порог не ослаблен. | Foot-lock / лучший контент |
| 4 | **Solver 400 итераций** (не сходится по `1e-6`). Результат в допуске; для продакшена — taut-string/least-variation. | T-MB05 follow-up |
| 5 | **NPC baked pool** не делался (решение пользователя). `allowNpc=false` не менялся, runtime neural не используется. | Отдельный шаг T-MB05b/T-MB08 |
| 6 | **Culling после body swap** (`CullUpdateTransforms` у нового тела) не менялся. | Отдельное решение |
| 7 | **`NetworkPlayer_GlobalPilot.prefab` вне baseline.** | Отдельное решение |
| 8 | **`tmp_tmb04_*.bat`** в корне — temp, не коммитить. | Удалить |

---

## 8. Откат

| Уровень | Действие |
|---|---|
| 0 | `MotionBricksRuntimeSettings.mode = Disabled` (уже 0) — runtime не затронут |
| 1 | Sidecar `{ "legIk": false }` + перепёк → прежний `PelvisSmooth`; `{ "loop": true }` для петли |
| 2 | `git revert` коммита T-MB05 (editor-скрипты + доки); baseline-файлы не тронуты |
| 3 | Перепёк клипов заново (GUID стабильны) или удалить `Generated/MotionBricks/*` (сцена потеряет ссылки — ожидаемо) |

---

## 9. Абсолютные ссылки

| Что | Путь |
|---|---|
| Leg IK | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksLegIk.cs` |
| Loop self-test | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksLoopSelfTest.cs` |
| Pelvis curve | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksFootIk.cs` |
| Baker | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksSpikeBaker.cs` |
| Отчёт `WalkForward` | `C:\Projects\ProjectC_client\Assets\_Project\Animations\Generated\MotionBricks\walk_forward\Male\WalkForward_Male.bake.report.json` |
| Отчёт `WalkToJog` | `C:\Projects\ProjectC_client\Assets\_Project\Animations\Generated\MotionBricks\walk_to_jog\Male\WalkToJog_Male.bake.report.json` |
| Baseline | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |
| Design | `C:\Projects\ProjectC_client\docs\dev\T-MB05_LEG_IK_LOOP_DESIGN.md` |
