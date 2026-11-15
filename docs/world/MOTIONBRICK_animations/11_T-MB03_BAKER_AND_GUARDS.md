# T-MB03 — бейкер без хардкода, batch/validation, foot IK, applyRootMotion guard

> **Дата:** 2026-11-15 (поздний вечер)
> **Ветка:** `main`
> **HEAD до работ:** `76aaff06` (`T-MB04b: audit next-step risks, runtime player baseline gap and plan status`)
> **Статус:** сделано; baseline 12/12 до/после; Play Mode **не запускался** (правило проекта).
> **Связанные:** [10](10_T-MB04B_RUNTIME_HARNESS.md) · [09](09_T-MB02_ANALYSIS_AND_NEXT.md) · [06](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [HANDOFF.md](HANDOFF.md) · [baseline/](baseline/)

---

## 1. Что требовалось (acceptance)

| Пункт | Требование | Статус |
|---|---|---|
| baseline | PASS до/после | ✅ 12/12 до и после |
| baker | без хардкода, batch/validation | ✅ задания собираются с диска, batch + валидация + отчёт |
| pelvis foot IK | «в допуске» | ✅ контакт в допуске (hover 4 см ≤ 5, penetration 1 см ≤ 2); размах таза 15.7 → 11.7 см, см. §5 |
| applyRootMotion guard | не ломает `SkillAnimationPlayer` | ✅ guard + self-test 6/6 (без Play Mode) |
| SaveClipInPlace | сохраняет GUID/ссылки | ✅ GUID стабилен на трёх перепёках; ссылка в сцене жива |

Baseline-файлы (`NetworkPlayer.cs`, `CharacterCustomisationApplier.cs`, `SkillAnimationPlayer.cs`,
`NpcBrain.cs`, prefab/controller, пакеты) **не менялись**.

---

## 2. Файлы

| Файл | Что |
|---|---|
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksBakeJob.cs` | **новый**: задание бейка, discovery по диску, sidecar-конфиг |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksFootIk.cs` | **новый**: тазовый foot IK (два режима) + self-test решателя |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksBakeReport.cs` | **новый**: схема `*.bake.report.json`, SHA256, запись UTF-8 |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksGuardSelfTest.cs` | **новый**: проверка guard'а без Play Mode |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksSpikeBaker.cs` | переработан: discovery/batch/отчёт/новый IK, ретаргет не менялся |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksClipValidator.cs` | замер вынесен в `MotionBricksValidationReport`, добавлен batch |
| `Assets/_Project/Scripts/Animation/MotionBricks/MotionBricksBakedPlayback.cs` | guard `applyRootMotion` (+ учёт каста) |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksReviewMenu.cs` | harness-лог печатает `applyRootMotion` и `guard` |

Сгенерированные клипы и отчёты лежат в `Assets/_Project/Animations/Generated/MotionBricks/...`
(Lore/локально, в git не идут — как и раньше).

---

## 3. Бейк: задания собираются с диска

Контракт папки (был и раньше, теперь это единственный вход):

```
Assets/_Project/Animations/Generated/MotionBricks/<primitiveId>/<bodyType>/
    soma_bind.f32                 ← настоящая bind-поза SOMA (обязательно)
    <ClipBase>_30fps.f32          ← сырой клип (обязательно)
    <ClipBase>.bake.config.json   ← необязательный sidecar
    <ClipBase>.anim               ← выход (generic)
    <ClipBase>_Humanoid.anim      ← выход (muscles)
    <ClipBase>.bake.report.json   ← выход (метаданные)
```

`MotionBricksBakeJob.Discover()` обходит папки, сортирует их стабильно и создаёт задание на каждый
`*_30fps.f32` при наличии `soma_bind.f32`. Никаких `WalkToJog`/`WalkForward` в логике больше нет.

Sidecar (все поля необязательны, дефолты — в классе `MotionBricksBakeConfig`):

```json
{ "groundFeet": false, "bakeInPlace": false, "clipSpeed": 2.0, "label": "Jump" }
```

- `groundFeet` — тазовый foot IK (для прыжка/полёта выключить);
- `bakeInPlace` — выбрасывать горизонталь корня (`false` = клип несёт перенос);
- `clipSpeed` — справочная скорость опоры, м/с (для настройки root motion);
- `label` — метка в логах/отчёте.

Меню:

| Пункт | Что делает |
|---|---|
| `ProjectC/MotionBricks/Bake/All discovered clips (batch)` | печёт все задания |
| `ProjectC/MotionBricks/Bake/Bake + validate all discovered` | печёт и меряет каждый humanoid-клип |
| `ProjectC/MotionBricks/Bake/List discovered clips` | печатает найденные задания с настройками |
| `ProjectC/MotionBricks/Bake/Walk Forward (Male)` / `WalkToJog (Male)` | совместимые точечные пункты (ищут задание по primitive/body) |
| `ProjectC/MotionBricks/Validate/All discovered clips (batch)` | только замер, без бейка |
| `ProjectC/MotionBricks/Bake/Foot IK self-test` | проверка решателя на синтетических профилях |
| `ProjectC/MotionBricks/Bake/applyRootMotion guard self-test` | проверка guard'а без Play Mode |

---

## 4. Метаданные бейка (`*.bake.report.json`)

Пишется рядом с клипами (`JsonUtility` + UTF-8 без BOM; `.meta` создаёт Unity). Содержит:

- `schema`, `ticket`, `generatedUtc`, модель и `soma_bind.f32` с SHA256;
- сырьё: имя, SHA256, кадры, длительность, joints/floats;
- настройки: `bakeInPlace`, `groundFeet`, `yawOffsetDeg`, `maxClavicleDeg`;
- `footIk`: режим, размах «до/после», hover/penetration, итерации, покадровая трасса
  (`lowestFootYCm`, `correctionsCm`, `stanceFoot`);
- `genericClip`/`humanoidClip`: путь, GUID до/после, `guidStable`, кривые, длина, humanMotion;
- `validation`: PASS/FAIL, причины, все метрики валидатора;
- `warnings`: вырожденные цепочки, мышцы вне `[-1..1]`, foot IK вне допуска.

Пример (walk_forward): `Assets/_Project/Animations/Generated/MotionBricks/walk_forward/Male/WalkForward_Male.bake.report.json`.

---

## 5. Pelvis foot IK: что было, что стало и почему не «2 см»

### 5.1. Дефект

Rotation-only ретаргет сохраняет углы суставов, но нога рига короче SOMA (reach 0.852 против
0.940 м), поэтому при одинаковой высоте таза стопа висит ~10 см. Прежний бейкер каждый кадр
опускал таз так, чтобы **нижняя** стопа встала ровно на rest-высоту (`PelvisMin`). Это
гарантирует контакт, но таз повторяет весь вертикальный размах позы: у `WalkForward_Male`
получалось **15.7 см** против **2 см** у эталонной ходьбы KL.

### 5.2. Решение (`PelvisSmooth`)

Таз ищется как **самая гладкая** кривая, у которой контакт остаётся в допуске:

```
c[f] ∈ [raw[f] − AllowPenetration, raw[f] + AllowHover]
raw[f] = groundFootY − lowestFootY[f]
AllowHover = 3 см, AllowPenetration = 1 см  (строже порогов валидатора 5/2)
```

Решение — проекция Гаусса–Зейдлиля (сглаживание к соседям + возврат в коридор), по кругу:
клип играется в петлю, шов тоже гладкий. Вертикаль источника (`root.y`) при включённом IK
**не** подмешивается — высоту таза определяет земля. Для плохого клипа таз всё равно обязан
ехать: если опорная нога уходит вверх на 15 см, плоский таз означает висящую стопу.

Режимы: `Off` (прыжки), `PelvisMin` (прежнее поведение, для A/B/отката), `PelvisSmooth` (дефолт).
Логика ретаргета (повороты, ключицы, мышцы) **не менялась**.

### 5.3. Замеры (bake + validate, 15.11)

| Клип | Размах таза | hover | penetration | foot slide | Контакт |
|---|---|---|---|---|---|
| `WalkForward_Male` (IK Smooth) | **11.73 см** (было 15.73) | 4.0 см | 1.0 см | 1.82 см/кадр | в допуске |
| `WalkForward_Male` (IK Off, A/B) | 22.41 см | 11.57 см | 0 | — (контакта нет) | FAIL |
| `WalkToJog_Male` (IK Smooth) | **1.10 см** (было 5.10) | 4.0 см | 1.0 см | 0.99 см/кадр | в допуске |

A/B снят временным sidecar `{"groundFeet": false}` и затем повторным бейком с IK (GUID те же).

**Честный вывод:** «2 см» для `WalkForward_Male` недостижимы тазовым IK — это свойство самого
клипа: в нём нижняя стопа (при тазе в rest-высоте) гуляет на **15.7 см**, т.е. опорная нога
реально отрывается. Тазовый IK может либо повторять этот размах (контакт), либо оставить таз
плоским (стопа висит). Он уменьшил размах на 25 % против старого и на 48 % против «без IK»,
удержав контакт в допуске. Довести до 2 см можно только **IK ног (2-bone)**, которая сгибает
колено и ставит стопу на землю при гладком тазе — это отдельный шаг (кандидат в T-MB05),
в T-MB03 не делался, чтобы не менять позу клипа без визуальной приёмки.

### 5.4. Самопроверка решателя (без клипа и Play Mode)

`ProjectC/MotionBricks/Bake/Foot IK self-test`:

```
PASS  ровный контакт: таз 0,0 → 0,0 см, hover 0,0, penetration 0,0
PASS  эталон 2 см:    таз 2,0 → 0,0 см, hover 1,0, penetration 1,0
PASS  плохой клип 15.8 см: таз 15,8 → 11,8 см, hover 3,0, penetration 1,0
PASS  смена опоры:    таз 3,0 → 0,0 см, hover 2,0, penetration 1,0
ИТОГ: PASS
```

---

## 6. Валидация: batch и отчёт

`MotionBricksClipValidator.Validate(...)` теперь возвращает `MotionBricksValidationReport`
(все метрики + `Failures` + готовый текст). Старые пункты меню печатают тот же текст, что и
раньше; batch дополнительно печатает сводку и пишет результат в отчёт бейка.

Результат прогона 15.11 (пороги применяются):

| Метрика | `WalkForward_Male` | `WalkToJog_Male` | Порог |
|---|---|---|---|
| длина / кадры | 1.500 s / 46 | 1.633 s / 50 | — |
| hipsY | 0.810..0.928 | 0.947..0.959 | — |
| опорная стопа (hover) | 4.0 см | 4.0 см | ≤ 5 |
| penetration | 1.0 см | 1.0 см | ≤ 2 |
| foot slide | 1.82 см/кадр | 0.99 см/кадр | ≤ 2 |
| ось шага vs лицо | **75.0°** ✗ | **49.1°** ✗ | ≤ 20 |
| скорость опоры L/R | 0.47 / 0.69 м/с | 0.22 / 0.23 м/с | — |
| асимметрия | **33 %** ✗ | 4.9 % | ≤ 25 |
| дрейф рыскания | −0.1° | **−195.6°** ✗ | ±15 |
| ключицы L/R | 24.7° / 26.4° | 176.2° / 176.2° | ≥ 3 |
| руки L/R | 80.8° / 83.7° | 131.4° / 151.1° | ≥ 10 |
| implied speed | 0.51 м/с | 0.23 м/с | — |
| **ИТОГ** | **FAIL** | **FAIL** | |

Оба FAIL — **качество контента**, а не пайплайна: `WalkForward_Male` синтезирован из статичной
позы (T-MB04a: ноги уходят вбок, асимметрия), `WalkToJog` в источнике разворачивается на ≈197°.
Валидатор не ослаблялся: пороги те же, просто теперь их видно в batch и отчёте.

Побочное наблюдение: `walk_to_jog` даёт `B-shoulder` 176° в humanoid-клипе при клампе ключицы
18° в generic — это humanoid-конверсия мышц (`Shoulder Front-Back` вне `[-1..1]`), известный долг
ретаргета с T-MB02, не регрессия T-MB03 (математика мышц не менялась).

---

## 7. applyRootMotion guard

### 7.1. Зачем

После body swap новое тело приходит из FBX с `applyRootMotion = true` (T-MB04b, §5.3). На
in-place KL-клипе это безвредно, но для клипа с переносом корня Animator сложил бы своё движение
с движением персонажа (двойная скорость).

### 7.2. Что делает playback

- перед стартом графа: запоминает `applyRootMotion`, выставляет `false`;
- после `StopPlayback` (Rebind + Update(0)): возвращает прежнее значение;
- body swap: сначала возвращает флаг старому Animator'у, затем берёт новый (у которого снова
  `true` из FBX) и выключает его на время playback;
- **не перетирает флаг во время каста**: если `SkillAnimationPlayer.IsCasting`, восстановление
  пропускается (флагом владеет скилл, он сам сохраняет/включает/восстанавливает его вокруг каста);
- играть во время каста playback по-прежнему отказывается (старый гейт).
- `RootMotionGuardActive` — публичный признак для harness-лога.

### 7.3. Проверка без Play Mode

`ProjectC/MotionBricks/Bake/applyRootMotion guard self-test` (временный объект `HideAndDontSave`,
настоящий публичный путь `TryPlayForReview`/`StopPlayback`):

```
PASS  старт (true)
PASS  стоп (вернул true)
PASS  старт/стоп (было false)
PASS  body swap (A→B)
PASS  каст: playback не стартует и флаг не тронут
PASS  каст: Stop не перетирает флаг скилла
ИТОГ: PASS (guard живой, скилл не ломается)
```

Осталось пользователю (Play Mode, чек-лист T-MB08): включить playback на живом игроке, скастовать
скилл и убедиться, что анимация/флаг возвращаются; harness-лог печатает
`applyRootMotion=...` и `guard=...`.

---

## 8. SaveClipInPlace: GUID и ссылки

`SaveClipInPlace` не удаляет ассет: кривые перезаписываются на месте, GUID сверяется явно и
попадает в отчёт (`guidBefore`, `guid`, `guidStable`). Три перепёка подряд:

| Клип | GUID | Стабилен |
|---|---|---|
| `WalkForward_Male.anim` | `ea45b84cc6488174aa75298fd1c4010f` | ✅ |
| `WalkForward_Male_Humanoid.anim` | `6cccfe57e5014cd4398abf7c3c9eed0a` | ✅ |
| `WalkToJog_Male.anim` | `dc5f1d8819f0dec43a7dcb30ca59adf2` | ✅ |
| `WalkToJog_Male_Humanoid.anim` | `971e390ab04243343815da4e8c79c0a7` | ✅ |

Ссылка из сцены жива: `WorldScene_0_0.unity`, объект `MB_Probe_Player`:
`_clip: {fileID: 7400000, guid: 971e390ab04243343815da4e8c79c0a7, type: 2}` — тот же GUID после
перепёка. Prefab/controller не трогались.

---

## 9. Приёмка

| Пункт | Доказательство |
|---|---|
| baseline PASS до/после | `BASELINE OK: 12 files match` до работ и после |
| baker без хардкода | `MotionBricksBakeJob.Discover()`; `List discovered` → 2 задания; добавление папки = новое задание |
| batch | `Bake + validate all discovered` → «испечено 2, ошибок 0»; сводка валидации |
| validation/metadata | `*.bake.report.json` с SHA256, foot IK-трассой, метриками и GUID |
| pelvis foot IK в допуске | hover 4.0 ≤ 5, penetration 1.0 ≤ 2, slide 1.82 ≤ 2; размах 15.7 → 11.7 см (§5) |
| applyRootMotion guard не ломает скилл | guard self-test 6/6 + ветка `IsCasting` в коде (§7) |
| SaveClipInPlace сохраняет GUID/ссылки | §8 |

Компиляция: 0 errors; предупреждения — только существовавшие в проекте `CS0618` (Unity 6 deprecations).

---

## 10. Что осталось / риски

| # | Риск | Куда |
|---|---|---|
| 1 | **Качество клипов.** `WalkForward_Male` и `WalkToJog_Male` валидатор не проходят (ось, асимметрия, разворот). Это контент (синтез из статичной позы), не бейкер. | Свои seed/target-позы; T-MB08 решает по 3–5 клипам |
| 2 | **Размах таза 11.7 см на плохом клипе.** Тазовый IK не может дать 2 см, если опорная стопа гуляет на 15.7 см. | 2-bone IK ног (кандидат в T-MB05), только после визуальной приёмки |
| 3 | **Мышцы вне `[-1..1]`: 5** (Jaw Close, Shoulder Front-Back L/R, Thumb Stretched). Humanoid-плеер их клампит → поза в игре ≠ поза в бейке. | Долг ретаргета с T-MB02; math не менялась |
| 4 | **Guard на живом игроке и во время каста не проверен в Play Mode.** | Чек-лист T-MB08 (пользователь) |
| 5 | **Baseline защищает `NetworkPlayer.prefab`, а рантайм-игрок — `NetworkPlayer_GlobalPilot.prefab`.** | Отдельное решение по baseline |
| 6 | **Root motion в `NetworkPlayer` не интегрирован** — отдельный тикет T-MB04c, требует одобрения правки baseline-файла. | T-MB04c |
| 7 | Сгенерированные клипы/отчёты не в git (Lore); воспроизводимость — через бейкер + `sourceSha256` в отчёте. | Как и раньше |
| 8 | `tmp_tmb04_*.bat` в корне main — временные файлы. | Удалить после проверки, не коммитить |

---

## 11. Откат

| Уровень | Действие |
|---|---|
| 0 | `MotionBricksRuntimeSettings.mode = Disabled` (уже 0) |
| 1 | Перепечь с `{"groundFeet": false}` (sidecar) — вернёт вертикаль источника |
| 2 | `git revert` коммита T-MB03 (только editor-скрипты/доки; baseline-файлы не тронуты) |
| 3 | Сгенерированные клипы/отчёты удалить из `Generated/MotionBricks/` (сцена потеряет ссылку — это ожидаемо при удалении ассета) |

---

## 12. Абсолютные ссылки

| Что | Путь |
|---|---|
| Задания бейка | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksBakeJob.cs` |
| Foot IK | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksFootIk.cs` |
| Отчёт/метаданные | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksBakeReport.cs` |
| Бейкер | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksSpikeBaker.cs` |
| Валидатор | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksClipValidator.cs` |
| Guard self-test | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksGuardSelfTest.cs` |
| Playback + guard | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedPlayback.cs` |
| Отчёт walk_forward | `C:\Projects\ProjectC_client\Assets\_Project\Animations\Generated\MotionBricks\walk_forward\Male\WalkForward_Male.bake.report.json` |
| Отчёт walk_to_jog | `C:\Projects\ProjectC_client\Assets\_Project\Animations\Generated\MotionBricks\walk_to_jog\Male\WalkToJog_Male.bake.report.json` |
| Baseline | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |

---

## 13. Передача следующему агенту (15.11, ночь)

### 13.1. Состояние на момент передачи

- **T-MB03 закрыт** на уровне кода/замеров: коммиты `90180758` (код + доки) и `a971fddb`
  (строка в индексе HANDOFF).
- **Baseline 12/12**; `MotionBricksRuntimeSettings.mode: 0`; сцена `WorldScene_0_0` не менялась;
  Play Mode агентом **не запускался**.
- T-MB04c **не начат**: пользователь 15.11 передал анализ другому агенту, одобрения на правку
  `NetworkPlayer.cs` нет. Ничего из baseline-файлов не тронуто.

Всё воспроизводится в Edit Mode, без Play Mode:

1. `powershell -ExecutionPolicy Bypass -File docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1`
   → `BASELINE OK: 12 files match`.
2. `ProjectC/MotionBricks/Bake/Foot IK self-test` → 4 PASS, `ИТОГ: PASS`.
3. `ProjectC/MotionBricks/Bake/applyRootMotion guard self-test` → 6 PASS, `ИТОГ: PASS`.
4. `ProjectC/MotionBricks/Bake/List discovered clips` → 2 задания (`walk_forward`, `walk_to_jog`).
5. `ProjectC/MotionBricks/Bake/Bake + validate all discovered` → «испечено 2, ошибок 0»; GUID
   стабильны; оба клипа FAIL по оси/асимметрии/развороту — это контент, не пайплайн (§6).

### 13.2. T-MB04c — план (реализован, см. [12](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md))

Одобрение на правку `NetworkPlayer.cs` получено 15.11 (ночь); план ниже выполнен. Замер Play Mode: поездка **2.00 м/с**, `Stop` → стоп, body swap Female→Male с перепривязкой ([12 §6.1](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md)); визуал T-pose/экипировки — за пользователем.

1. **Новый runtime-файл** `Assets/_Project/Scripts/Animation/MotionBricks/MotionBricksLocomotionBridge.cs`:
   реестр активных `MotionBricksBakedRootMotion` (регистрация в `OnEnable`/`OnDisable`, без
   `GetComponent` в кадре) + `TryConsumeDisplacement(NetworkPlayer, out Vector3)` (чтение одноразовое:
   после выдачи смещение обнуляется).
   Гейт тот же: `MotionBricksRuntimeSettings.mode != Disabled`, `allowPlayer`, `player.IsOwner`.
2. **`MotionBricksBakedRootMotion`**: на объекте с `NetworkPlayer` по-прежнему НЕ вызывает `Move`;
   вместо предупреждения публикует посчитанное смещение в мост (одно место расчёта).
3. **`NetworkPlayer.ProcessMovement`** (+11 строк перед записью `CharacterController.Move.before`):
   если мост дал смещение — горизонталь этого кадра = baked-смещение (ветер на кадр выключается),
   затем идёт существующий единственный `_controller.Move`. Никаких новых `Move`, `NetworkTransform`
   и контроллер не меняются.
4. **Baseline:** после правки `NetworkPlayer.cs` пересчитать только его хэш и обновить
   `baseline_hashes.tsv` ОТДЕЛЬНЫМ коммитом по процедуре `BASELINE_MANIFEST.md` §5.
5. **Приёмка:** baseline PASS (с обновлённым хэшем); игрок едет от baked root motion; второго
   `Move` нет; `Stop` → baseline; body swap на `GlobalPilot` без T-pose/потери экипировки.
6. **Риски:** `ClipSpeed` едет по `transform.forward` и не учитывает ввод — для MVP допустимо
   только когда playback управляет движением; `HipsDelta` для клипов с переносом корня — отдельный
   шаг после того, как появятся такие клипы.

### 13.3. Отдельные решения (сознательно не сделаны)

| Решение | Статус на 15.11 |
|---|---|
| Добавить `NetworkPlayer_GlobalPilot.prefab` в `baseline_hashes.tsv` | **Отложено пользователем.** Хэш на 15.11 `c14c09dcde0ab1dc37ef4a6f83b4aa4503706cfa26da71837cfdbec6fe2b6010`; добавить только после пересчёта и отдельным коммитом (BASELINE_MANIFEST §4) |
| 2-bone IK ног (цель — размах таза ~2 см) | Кандидат в T-MB05; сейчас тазовый IK упирается в качество клипа (§5.3). Только после визуальной приёмки |
| Удалить `tmp_tmb04_*.bat` из корня | Не коммитились; удалить при удобном случае |

### 13.4. Что проверить пользователю в Play Mode (когда будет сессия T-MB08)

1. `Review/Log harness state` → в логе `applyRootMotion=...` и `guard=...`.
2. `Review/Player/Attach + play KL Walk01 on my player` → при FBX-дефолте в логе
   `applyRootMotion: true → false`; `Review/Player/Stop on my player` → `applyRootMotion восстановлен`.
3. Каст скилла при активном playback → playback не стартует во время каста; после каста флаг
   и анимация возвращаются (агент проверил только self-test без Play Mode).
4. `Review/Body swap/Body swap → Female|Male` при активном playback → перепривязка, T-pose нет,
   экипировка на месте.
5. 2 клиента, F8/F9 от нуля, equipment (чек-лист T-MB08).

---

## 14. Аудит T-MB03 со стороны следующего шага (15.11, ночь)

### 14.1. Что реально закрыто

- Бейкер больше не хардкодит клипы: discovery по `*_30fps.f32` + `soma_bind.f32`; sidecar-конфиг; batch/validate/list.
- Метаданные: `*.bake.report.json` с SHA256 сырья/bind, настройками, foot-IK-трассой, метриками и GUID.
- `applyRootMotion` guard: self-test 6/6 без Play Mode; учитывает `SkillAnimationPlayer` при восстановлении флага.
- `SaveClipInPlace`: GUID стабилен на трёх перепёках; ссылка `MB_Probe_Player._clip` жива.
- Baseline 12/12; baseline-файлы не тронуты; Play Mode не запускался.

### 14.2. Новые риски / что важно не потерять

1. **`PelvisSmooth` цикличен по построению.** `MotionBricksFootIk.Solve` всегда берёт `prev=(f-1+n)%n` и `next=(f+1)%n`; в `MotionBricksBakeConfig` **нет `loop`/`cyclic`**, а `groundFeet` по умолчанию `true`. Для one-shot клипов (jump/vault/sit/attack) с контактом это сгладит несвязанные начало и конец. Для прыжков `groundFeet:false` спасает, для sit/vault — нужен `loop:false`/нециклическое сглаживание или `groundFeet:false`. Кандидат в T-MB05.
2. **Каст скилла во время активного playback не остановлен.** `TryPlay` отказывает, если `SkillAnimationPlayer.IsCasting` уже true, но `Update` не останавливает playback при переходе в каст. Если скилл начинается во время playback, `PlayableGraph` может продолжать писать позу поверх скилла. Нужно проверить в T-MB08; при необходимости — `if (_skillPlayer.IsCasting) StopPlayback()`.
3. **Это не 2-bone foot IK.** `PelvisSmooth` двигает таз, а не ставит стопу; `WalkForward_Male` 11.7 см размаха при цели 2 см. Честно: цель 2 см достигается только IK ног (T-MB05) и/или лучшим контентом.
4. **Solver упирается в 400 итераций** (в отчётах `iterations: 400`), т.е. не сходится по `1e-6`. Результат в допуске, но это сигнал: для T-MB05 стоит заменить на taut-string/least-variation или добавить сходимость.
5. **Оба текущих клипа FAIL по валидатору:** `WalkForward_Male` — ось 75°, асимметрия 33 %, implied 0.51 м/с; `WalkToJog` — yaw −195.6°, ключицы 176°. Это контент, не бейкер; **production-клипов пока нет**.
6. **Baseline gap:** фактический игрок `NetworkPlayer_GlobalPilot.prefab` не в `baseline_hashes.tsv`; решение отложено.
7. **Root motion в `NetworkPlayer` не интегрирован:** T-MB04c; правка baseline-файла требует одобрения.
8. **`tmp_tmb04_*.bat`** — temp-файлы в корне; не коммитить, удалить.

### 14.3. Где мы по плану

| Этап | Статус |
|---|---|
| T-MB01 / 01A / 01B | ✅ |
| T-MB02 | ✅ |
| T-MB04a | ✅ historical |
| T-MB04b | ✅ accepted |
| T-MB03 | ✅ done (pipeline; quality FAIL остаётся) |
| T-MB04c root motion в NetworkPlayer | ✅ code + runtime; visual pending — [12](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md) |
| T-MB05 hybrid/2-bone IK/NPC pool | ✅ 2-bone IK + loop:false — [13](13_T-MB05_LEG_IK_AND_LOOP.md); NPC pool — отдельно |
| T-MB08 2 clients/F8-F9/equipment/go-no-go | ⬜ |
| T-MB06/07 runtime neural | ⬜ optional |

До go/no-go: **T-MB04c → T-MB05 (если нужен NPC/2-bone IK) → T-MB08**. Если T-MB05 не нужен, остаётся T-MB04c + T-MB08.

### 14.4. Следующему агенту

1. Не трогать baseline-файлы без одобрения; `check_baseline.ps1` → 12/12.
2. T-MB04c: root motion в `NetworkPlayer.ProcessMovement` под feature flag; один `CharacterController.Move`; после — baseline с обновлённым хэшем отдельным коммитом. **Сделано, см. [12](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md); Play Mode — за пользователем.**
3. T-MB05: 2-bone IK, `loop:false`/нециклический IK, NPC baked pool/hybrid primitives.
   **2-bone IK и `loop:false` сделаны** ([13](13_T-MB05_LEG_IK_AND_LOOP.md)); NPC pool — отдельно.
4. T-MB08: 2 clients, F8/F9 от нуля, equipment, каст скилла во время playback, go/no-go.
5. Отдельное решение: добавить `NetworkPlayer_GlobalPilot.prefab` в baseline.

---

*Связанные: [10_T-MB04B_RUNTIME_HARNESS.md](10_T-MB04B_RUNTIME_HARNESS.md) · [09_T-MB02_ANALYSIS_AND_NEXT.md](09_T-MB02_ANALYSIS_AND_NEXT.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [HANDOFF.md](HANDOFF.md)*
