# T-MB05 — 2-bone IK ног и non-cyclic pelvis (loop:false)

> **Дата:** 2026-11-16
> **Ветка:** `main`
> **HEAD до работ:** `0fa9ae42` (`T-MB04c: audit notes, save side effect and plan status`)
> **Коммит:** `87102426` + follow-up (runtime fixes)
> **Статус:** код + offline-замеры готовы; baseline 12/12. Был **один контролируемый Play Mode** (по разрешению пользователя), редактор после него перезапускался; runtime-фиксы внесены offline, повторный Play Mode **не запускался**. Визуал/финальный runtime — за пользователем.
> **Связанные:** [11_T-MB03_BAKER_AND_GUARDS.md](11_T-MB03_BAKER_AND_GUARDS.md) · [12_T-MB04C_ROOT_MOTION_IN_PLAYER.md](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [baseline/BASELINE_MANIFEST.md](baseline/BASELINE_MANIFEST.md) · [HANDOFF.md](HANDOFF.md) · design: `docs/dev/T-MB05_LEG_IK_LOOP_DESIGN.md`

---

## 1. Что требовалось (acceptance)

| Пункт | Требование | Статус |
|---|---|---|
| 2-bone IK | Контакт стоп в допуске (hover ≤ 5, penetration ≤ 2), таз ближе к 2 см | ✅ `WalkForward`: таз **15.7 → 1.7 см**, humanoid **0.1 / 0.0**; `WalkToJog` (one-shot): **5.1 → 4.6 см**, **0.4 / 0.4** |
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
| `WalkForward_Male` | 15.73 см | **1.72 см** | 48.2° | 0.09 / 0.00 см | **0.09 / 0.00 см** | 22.41 см → отклонён | 1 кадр |
| `WalkToJog_Male` (one-shot) | 5.10 см | **4.57 см** | 37° | 0.09 / 0.00 см | **0.42 / 0.37 см** | 4.42 см → применён | 2 кадра |

`humanoid` — то, что видит валидатор после фикса last-frame/twist и one-shot pelvis. Контакт в допуске
(5 / 2). Таз `WalkToJog` 4.6 см, а не ~2: нециклический solver не сглаживает шов one-shot-клипа,
а сам клип — синтетический разворот (валидатор: yaw −195.6°, ось 53.6°, асимметрия 39 %).
`WalkForward` остаётся опорным по тазу (1.7 см).

### 3.2. Runtime-находки (Play Mode, один сеанс по разрешению)

Play Mode на probe выявил **несколько разных проблем**, а не одну:

1. **Stale parent в leg IK (исправлено).** `ApplyLegIkFrame` применялся после цикла кадров и
   брал мировой поворот таза из **последнего** кадра. На `WalkToJog` (разворот ~187°) локальные
   бёдра переворачивались на **172°** за кадр. Теперь `LegFrame.ParentRotation` сохраняется
   в каждом кадре. После фикса: per-frame бёдра ≤ 12°.
2. **Last-frame wrap humanoid-бейка (исправлено).** `SampleAnimation` ровно в `clip.length`
   при `loopTime=true` заворачивал последний кадр на **первый**, и humanoid-клип заканчивался
   позой старта. Теперь сэмпл идёт в `length − 1e-4`; yaw drift вернулся к истинным −195.6°
   (а не −360°).
3. **Arm Twist ±180/360 (исправлено).** `Arm Twist In-Out` держался около 2.0 (полный оборот);
   Animator клампит в `[-1..1]`, и на полюсе 2.0 рука переворачивалась на **178.8°**.
   Twist-кривые разворачиваются по 2.0, arm twist ограничен ±0.5 (90°). После фикса
   offline max arm jump 7.8°, leg 12.2°.
4. **`WalkToJog` — one-shot.** Sidecar `{"loop":false}` → `loopTime=false`/Clamp;
   `MotionBricksBakedPlayback` не зацикливает клип с `isLooping=false`; review-меню играет
   `WalkToJog` с `loop:false`. Шва «последний→первый» больше нет.

**Осталось (важно):** у `WalkToJog` **рука в мире counter-rotates**: исходная SOMA-рука имеет
постоянный **локальный** поворот (идёт с корпусом), generic-клип поворачивает руку вместе с корпусом,
а humanoid-клип держит руку почти мировой (к f49 расхождение 146–167°). Это **не 2-bone IK и не
runtime-фикс**, а ретаргет arm chain для клипов с полным разворотом (источник `WalkToJog` и так
FAIL по yaw/оси/асимметрии). Для визуальной приёмки locomotion использовать **KL Walk01**;
`WalkToJog` — диагностический клип, не go/no-go. Отдельный тикет — arm retarget для full-turn clips.

### 3.3. Почему прежний humanoid-клип был T-позой (найдено и исправлено)

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
| hover / penetration | **0.09 / 0.00** | **0.42 / 0.37** | в допуске (5 / 2) |
| таз | 1.72 см | 4.57 см | `WalkForward` — цель ~2 см; `WalkToJog` one-shot не сглаживает шов |
| foot slide | 2.88 см/кадр | 1.57 см/кадр | у `WalkForward` контент (стопа стоит больше кадров); порог не ослаблен |
| ось шага | 83.9° | 53.6° | контент (FAIL) |
| асимметрия опоры | 45 % | 39 % | контент (FAIL) |
| дрейф рыскания | 0.0° | −195.6° | `WalkToJog` в источнике разворачивается (FAIL) |
| ключицы/руки | двигаются | двигаются, но arm world counter-rotates | humanoid T-поза исправлена; full-turn arm retarget — отдельно (3.2) |

`WalkForward`/`WalkToJog` по-прежнему **FAIL** по оси/асимметрии/развороту — это качество
синтезированного контента, не пайплайна. Для go/no-go нужны 3–5 клипов с собственными seed/target;
визуальный эталон locomotion — **KL Walk01**, не `WalkToJog`.

---

## 7. Риски / что осталось

| # | Риск | Куда |
|---|---|---|
| 1 | **Финальный Play Mode не проверен.** Один сеанс был, но редактор перезапускался; runtime-фиксы (parent/last-frame/twist/loop) внесены offline и проверены Edit-Mode-диагностикой. | Пользователь: короткий probe-прогон |
| 2 | **`WalkToJog` arm counter-rotation** при полном развороте: humanoid-рука держится почти мировой, исходная — локально с корпусом (расхождение до 167°). Это arm retarget, не 2-bone IK; клип и так FAIL по yaw/оси/асимметрии. | Отдельный тикет arm retarget для full-turn clips |
| 3 | **Foot slide 2.88** у `WalkForward` — стопа стоит больше кадров; XZ клипа непостоянно. Порог не ослаблен. | Foot-lock / лучший контент |
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
