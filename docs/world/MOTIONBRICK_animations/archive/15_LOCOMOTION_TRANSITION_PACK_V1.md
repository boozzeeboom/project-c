# 15 — Locomotion Transition Pack v1 (22 перехода одним batch)

> **Дата:** 2026-11-16
> **Ветка:** `main`
> **Статус:** ❌ **отклонён пользователем**: ощущается не лучше исходного Animator и требует сопоставимого объёма ручной отладки. Пакет был сгенерирован и просмотрен; в Animator не внедрялся. Решение и следующий runtime-этап — [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md).
> **Область:** host-client co-op до 4 игроков; прежняя формулировка «MMO запрещает runtime» больше не используется как архитектурный аргумент.
> **Связанные:** [14_OFFICIAL_VERTICAL_SLICE.md](14_OFFICIAL_VERTICAL_SLICE.md) · [Locomotion pack design](../dev/MOTIONBRICKS_LOCOMOTION_PACK_DESIGN.md) · [HANDOFF.md](HANDOFF.md)

---

## 1. Почему это не четыре отдельных клипа

Инвентаризация `PlayerAnimation.controller` показала, что cycling-клипы уже есть:

- `Walk_BlendTree` — 8 направлений;
- `Run_BlendTree` — 8 направлений;
- `Sprint_BlendTree` — 5 направлений;
- Idle, TurnLeft/Right, Jump/Fall/Land, Combat, Gather, Mine, Lumber.

Генерировать ещё раз `Walk Left/Right/Backward` как отдельные циклы — дублировать готовый контент. Поэтому вместо этого одним batch создан **пакет переходов** между состояниями, которые сейчас склеиваются обычным crossfade.

## 2. Состав пакета

Один manifest задаёт 13 source clips и 22 задания:

- `Idle → Walk_*` для 8 направлений;
- `Walk_* → Idle` для 8 направлений;
- `WalkForward ↔ RunForward`;
- `RunForward ↔ SprintForward`;
- `WalkForward → TurnLeft`;
- `WalkForward → TurnRight`.

Manifest: `docs/world/MOTIONBRICK_animations/batches/locomotion_v1.json`.

### Пайплайн

```text
PlayerAnimation.controller inventory
  → locomotion_v1.json
  → Unity exporter: 13 source clips → 13 pose packs
  → Python: inverse KL→SOMA + target placement
  → official MotionBricksSOMAEngine / TensorRT: 22 генерации
  → MotionBricks baker: generic + Humanoid clips
  → 44 .anim + 22 отчёта
```

Source clips — проектные KL Humanoid clips. NVIDIA sample motions не использовались. Контроллер не изменялся.

## 3. Результат batch

| Параметр | Значение |
|---|---|
| Заданий | **22** |
| Generic clips | **22 × 91 curve** |
| Humanoid clips | **22 × 102 curve** |
| Длина каждого | **18 кадров / 0.567 s** |
| Tokens | 6 |
| Официальный backend | TensorRT SOMA FP32, identity model space |
| Модель SHA256 | `856419dc72aeb14e591eebf4dd0ea05734a2506ddb00bcd39dd6dac2ad60f4fb` |
| Source roundtrip | max **≤2.96×10⁻⁶°** |
| Contact hover | **0.10–0.15 см** |
| Penetration | **≤0.05 см** |
| Foot slide | **1.03–11.32 см/кадр**, 9/22 ≤2 см |
| Contact frames | 9–17 из 18 |
| Validation PASS | **0/22** — validator не является direction-aware, см. §4 |
| Humanoid size | ~10.6 МБ на 22 клипа (generic ещё ~4.8 МБ) |

### Направления

Для idle-start output root проходит около **0.857 м** за 0.567 s (~1.51 м/с):

- forward → Unity +Z;
- backward → Unity −Z;
- left/right → Unity ∓X;
- диагонали → соответствующие XZ.

Stop-переходы проходят **0.428 м** и заканчиваются в Idle. Speed transitions: ~1.5 м за 18 кадров для Walk↔Run и ~1.93 м для Run↔Sprint. Turn transitions проходят ~0.536 м с поворотом корпуса.

## 4. Почему validator даёт FAIL

Текущий `MotionBricksClipValidator` рассчитан на **forward in-place цикл**:

- `axisError` сравнивает движение опорной стопы с `-facing`;
- для backward движения корректное значение близко к **180°**;
- для left/right — к **90°**;
- для idle→walk и stop контактных кадров меньше, чем в полном цикле.

Поэтому `axis 175°` у `IdleToWalkBackward` — не баг клипа. Асимметрия также не является пороком для start/stop.

**Реальным сигналом остаётся foot slide:** только 9 из 22 клипов уложились в ≤2 см/кадр; у части lateral/run-переходов скольжение заметное. Это нужно проверять глазами и при необходимости отдельно настраивать foot-lock/clip speed. Автоматически объявлять пакет принятым нельзя.

## 5. Как проверить ощущения в игре (WASD + Play Mode)

Animation Window не проверяет отклик, инерцию и связь анимации с движением. Для этого добавлен **обратимый Play Mode-стенд**:

```text
ProjectC → MotionBricks → Review → Locomotion Transition Playtest (Play Mode)
```

Что он делает:

- создаёт **копию** `PlayerAnimation.controller` в `Generated/MotionBricks/playtest/`;
- заменяет в копии только переходы Idle↔Walk, Walk↔Run, Run↔Sprint на сгенерированные clip-states;
- во время Play Mode подменяет runtime controller **только у owner-игрока**;
- `Restore baseline` возвращает оригинальный controller;
- оригинальный `PlayerAnimation.controller`, prefab, `NetworkPlayer` и сцена **не меняются**; после выхода из Play Mode подмена исчезает.

Порядок проверки (Play Mode запускает пользователь):

1. Открыть окно `Locomotion Transition Playtest (Play Mode)`.
2. Нажать **Rebuild test controller**.
3. Запустить игру и дождаться spawn игрока.
4. Нажать **Apply test controller**.
5. Проверить:
   - W: Idle→Walk;
   - отпустить W: Walk→Idle;
   - W+Shift: Walk→Run/Sprint;
   - короткие тапы W;
   - резкое отпускание на бегу;
   - повторные старт/стоп.
6. Нажать **Restore baseline** для сравнения с текущим Animator.
7. Выйти из Play Mode.

**Важное ограничение теста:** `NetworkPlayer` не изменялся. Он по-прежнему мгновенно задаёт `Speed = 5/10` и двигает `CharacterController`; сгенерированные переходы имеют длительность 0.567 s. Стенд специально покажет, насколько анимация рассинхронизирована со скоростью персонажа и где нужен motion matching / управление скоростью по клипу. Кроме того, текущий `NetworkPlayer` всегда ставит `MoveY=1`, поэтому в этом стенде подключены forward/speed-переходы; боковые direction clips проверяются отдельно после решения по скорости.

## 5.1. Просмотр без Play Mode

Пакет также можно смотреть на персонаже без запуска игры:

Добавлены два безопасных инструмента:

1. Сцена `Assets/_Project/Scenes/MotionBricksTransitionPreview.unity` — KL-персонаж, камера, свет и пол.
2. Окно `ProjectC/MotionBricks/Review/Locomotion Transition Preview`.

Порядок:

1. Сохранить текущие сцены и открыть `MotionBricksTransitionPreview.unity` (в окне есть кнопка **Open preview scene**).
2. Открыть `ProjectC → MotionBricks → Review → Locomotion Transition Preview`.
3. Выбрать `MotionBricksPreviewCharacter` в Hierarchy и нажать **Use selected**.
4. Кликать клипы в списке или **Play / Pause**, **◀ Prev / Next ▶**, **Speed**.
5. **Stop / restore pose** возвращает исходную позу.

Окно работает через `AnimationMode` в Edit Mode: Play Mode, Animator Controller, prefab и сцена не изменяются. Clip list автоматически берёт Humanoid-клипы из `locomotion_transitions_v1/Male`.

Пакет лежит локально (в git не входит):

```text
Assets/_Project/Animations/Generated/MotionBricks/locomotion_transitions_v1/Male/
```

Рекомендуемый порядок просмотра:

1. `MB_IdleToWalkForward_Humanoid.anim`
2. `MB_WalkForwardToIdle_Humanoid.anim`
3. `MB_WalkForwardToRunForward_Humanoid.anim`
4. `MB_RunForwardToWalkForward_Humanoid.anim`
5. `MB_IdleToWalkLeft_Humanoid.anim`
6. `MB_IdleToWalkBackward_Humanoid.anim`
7. `MB_WalkForwardToTurnLeft_Humanoid.anim`
8. `MB_WalkForwardToTurnRight_Humanoid.anim`

Критерии:

- старт из Idle без T-pose/snap;
- стоп без рывка и «шарканья»;
- Walk→Run и Run→Sprint без смены ноги/скачка;
- left/right/backward визуально идут в правильную сторону;
- turn начинает поворот, а не просто проигрывает прямой шаг;
- руки не выворачиваются, ключицы не дёргаются.

Play Mode не требуется. Визуальную приёмку выполняет пользователь.

## 6. Runtime path

Зафиксирован следующий порядок:

1. принять или отклонить offline transition pack;
2. портировать полный runtime-контур NVIDIA: engine, recorder показанных поз, motion player, targets из управления;
3. замерить на host-client 4-player сценарии: p50/p95, GPU/VRAM, CPU, frame time, сетевой трафик;
4. только после измерений решать, shipping/owner-only/experimental.

Архитектура и stop conditions: `docs/dev/MOTIONBRICKS_LOCOMOTION_PACK_DESIGN.md`.

## 7. Воспроизведение

1. Unity Edit Mode:
   `ProjectC/MotionBricks/Official Slice/Export locomotion transition sources (Edit Mode)`
2. Python из официального venv:
   ```powershell
   .venv\Scripts\python.exe C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\locomotion_pack.py generate
   ```
3. Unity refresh, затем batch bake `locomotion_transitions_v1` (22 jobs).
4. Проверить:
   - `locomotion_pack_work/batch_metrics.json`
   - `locomotion_transitions_v1/Male/*.bake.report.json`.

## 8. Ограничения

- Только Male.
- 6 токенов / 18 кадров на переход; более длинные переходы не проверены.
- Визуальная приёмка не выполнена.
- Клипы **не подключены** к `PlayerAnimation.controller`.
- Foot slide у части переходов выше порога.
- Runtime-порт ещё не начат.
- Baseline 12/12; `MotionBricksRuntimeSettings.mode = 0`; Play Mode не запускался.
