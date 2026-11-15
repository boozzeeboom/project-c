# T-MB05 — 2-bone leg IK и non-cyclic pelvis (design)

> **Дата:** 2026-11-16
> **Статус:** реализовано; замеры — в [13_T-MB05_LEG_IK_AND_LOOP.md](../world/MOTIONBRICK_animations/13_T-MB05_LEG_IK_AND_LOOP.md). Scope: 2-bone IK + loop flag; NPC pool — отдельно.
> **Связанные:** `docs/world/MOTIONBRICK_animations/11_T-MB03_BAKER_AND_GUARDS.md` §5/§14, `12_T-MB04C_ROOT_MOTION_IN_PLAYER.md`, `HANDOFF.md`.

## 1. Проблема

`PelvisSmooth` держит контакт, опуская таз вслед за нижней стопой. Поэтому размах таза
равен размаху «нижней стопы» клипа: `WalkForward_Male` 15.7 → 11.7 см при цели ~2 см.
Колено при этом не участвует — нога рига короче SOMA, и таз компенсирует всю разницу.

`MotionBricksFootIk.Solve` всегда цикличен (`(f±1+n)%n`). Для one-shot (jump/vault/sit)
это сглаживает несвязанные начало и конец.

## 2. Что делаем

### 2.1. 2-bone IK ног (bake-time)

После ретаргета и до записи кривых:

1. Для каждого кадра снимаем мировые позиции/повороты `B-thigh`, `B-shin`, `B-foot` и
   определяем опорную стопу (нижняя, как в валидаторе).
2. Считаем **достижимость**: при тазе в rest-высоте какой максимальный сдвиг таза вверх
   ещё позволяет опорной ноге дотянуться до земли (полное выпрямление). Это верхняя
   граница `hi[f] ≤ 0`: выше rest таз не поднимаем.
3. Кривая таза = `min(sourceBob[f], hi[f])` + сглаживание внутри границ. `sourceBob`
   берётся из вертикали корня источника и применяется только если её размах ≤ 6 см
   (иначе клип сам «прыгает», и таз повторять это не должен). Для `WalkForward_Male`
   (22 см) bob = 0; для `WalkToJog` (4.4 см) сохраняется.
4. Для каждой ноги:
   - опорная: цель `foot.y = groundY` (rest-высота кости стопы);
   - маховая: цель `foot.y = max(currentY + pelvisOffset, groundY)` — только чтобы не
     проваливаться под землю;
   - аналитический 2-bone solve (закон косинусов), плоскость сгиба — текущее направление
     колена, fallback — forward персонажа; мировой поворот стопы сохраняется.
5. `locals` для thigh/shin/foot пересчитываются от новых мировых поворотов; generic и
   humanoid клипы пекутся из одного и того же `locals`, поэтому поза совпадает.
6. В отчёт пишем: `mode=LegIk`, `bobBeforeCm` (наивный), `bobAfterCm` (финальный таз),
   `hoverCm`/`penetrationCm` (post-IK), `reachLimitedFrames`, `maxResidualCm`,
   `sourceBobCm`, `sourceBobApplied`, `kneeBendMaxDeg`, `loop`.

Откат: sidecar `{ "legIk": false }` → прежний `PelvisSmooth` (код остаётся).

### 2.2. `loop` / cyclic

- `MotionBricksBakeConfig.loop` (default `true`) и `MotionBricksBakeJob.Loop`.
- `MotionBricksFootIk.Solve(..., bool cyclic)`; нециклический вариант не заворачивает
  соседей через шов (края берут сами себя).
- Новый `SolveCurve(target, upperBound, cyclic)` — та же проекция, но с верхней границей
  и целевой кривой; используется для таза.
- В `AnimationClipSettings` запечённого клипа ставим `loopTime = job.Loop`;
  `wrapMode = Loop/Clamp`. Runtime `MotionBricksBakedPlayback._loop` уже управляет
  ручной намоткой; для one-shot его выставляют `false` (или `ConfigureForReview(loop:false)`).
- Self-test: профиль-«рампа» с разными началом и концом; cyclic тянет край к шву,
  non-cyclic — нет, край остаётся в коридоре.

### 2.3. Приёмка

| Пункт | Как проверяем |
|---|---|
| контакт | `hover ≤ 5 см`, `penetration ≤ 2 см` на перепечённых клипах |
| таз | `bobAfterCm` на хорошем клипе ≈ 2 см; на текущих синтетических — насколько получится, честно в отчёте |
| хороший клип | self-test на KL `HumanM@Walk01_Forward`: таз не раздувается, контакт не ломается |
| loop:false | self-test решателя + `loopTime=false` в клипе; визуал one-shot — за пользователем |
| baseline | 12/12 до/после |

Play Mode не запускается. Визуальная приёмка (стопы/колени/T-pose) — пользователь.

---

## 3. Итог (16.11)

| Клип | Таз до | Таз после | Humanoid hover/pen | Контакт |
|---|---|---|---|---|
| `WalkForward_Male` | 15.73 см | **1.72 см** | 4.70 / 1.92 см | в допуске (5/2) |
| `WalkToJog_Male` | 5.10 см | **2.81 см** | 1.70 / 0.90 см | в допуске (5/2) |

Raw pose (до humanoid-конверсии): hover 0.09 / penetration 0.00 у обоих.
`loop:false` проверен self-test'ом (sidecar + clip settings + non-cyclic шов).
Отклонения: humanoid-кламп `Lower Leg Stretch` (0.00..1.03) даёт 4.7 см у `WalkForward`;
foot slide `WalkForward` 2.36 см/кадр (стопа стоит больше кадров) — контент/foot-lock.
