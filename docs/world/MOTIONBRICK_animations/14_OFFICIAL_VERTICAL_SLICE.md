# 14 — Official vertical slice: собственный moving context → TensorRT → Unity

> **Дата:** 2026-11-16
> **Ветка:** `main`
> **Статус:** численный offline probe выполнен; Unity generic/humanoid clips испечены; **Play Mode и визуальный preview не запускались**. Это не production acceptance.
> **Связанные:** [13_T-MB05_LEG_IK_AND_LOOP.md](13_T-MB05_LEG_IK_AND_LOOP.md) · [09_T-MB02_ANALYSIS_AND_NEXT.md](09_T-MB02_ANALYSIS_AND_NEXT.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [design](../dev/MOTIONBRICKS_OFFICIAL_VERTICAL_SLICE_DESIGN.md) · [HANDOFF.md](HANDOFF.md)

---

## 1. Зачем этот прогон

Предыдущая интеграция много раз проверяла обработку уже полученного motion, но не проверила главный вопрос:

> что выдаёт **официальный** MotionBricks, если дать ему не четыре кадра одной статичной позы, а настоящий движущийся контекст?

Срез разделяет четыре независимых уровня:

1. inverse-ретаргет проектного KL clip → SOMA;
2. официальный `MotionBricksSOMAEngine` / TensorRT;
3. тот же вход через `sa-inbetween` / ONNX Runtime CPU;
4. текущий Unity baker без IK, Humanoid override и runtime playback.

NVIDIA sample motions **не использованы**. Источник — проектный KL `HumanM@Walk01_Forward [RM]`, 25 кадров @30 Hz, root speed **2.142 м/с**.

---

## 2. Вход

| Параметр | Значение |
|---|---|
| Source | `HumanM@Walk01_Forward [RM]` (Kevin Iglesias asset проекта) |
| Context | кадры **0–3** (0.00–0.10 s), последовательное движение |
| Target (4 poses) | кадры **20–23** (0.667–0.767 s) |
| Путь до target | **1.214 м** за **0.567 s** |
| Expected speed | **2.142 м/с** |
| SOMA input | 77 joints, Y-up, meters, root translation scale `1.071142` по длине ноги |
| Token setting | **6** (18 валидных кадров = 0.567 s) |
| Endpoint correction | включён |
| Model / runtime space | `SOMA FP32`, `model_to_runtime = identity` |

Roundtrip проверка inverse-ретаргета до запуска модели:

- mapped world rotations: **max 2.41×10⁻⁶°**, mean **2.36×10⁻⁷°**;
- non-finite: нет;
- source SHA256: `e8575bfe36e0dec76b2f8c73a2485252a0f105bd3c8fb74468f5b371a6eee6c6`.

То есть вход в модель не содержит ошибки конверсии KL↔SOMA.

### 2.1. Важный побочный результат про длительность

Первый exploratory-прогон ставил target на кадры 12–15 (слишком близко для минимального 0.6 s) и разрешал 8–14 токенов. Официальная модель выбрала **14 токенов / 50 кадров / 1.633 s** и растянула путь **0.643 м** до **0.393 м/с**. Community backend дал практически тот же результат.

Это не «ошибка Unity» и не доказательство плохого качества модели: приложение обязано согласовать длительность генерации с target. Наблюдение сохраняется как отдельный риск автоматического выбора `allowed_token_counts`.

---

## 3. Результаты raw SOMA

| Метрика | Source KL | Official TensorRT | sa-inbetween CPU |
|---|---:|---:|---:|
| Кадров | 25 | **18** | **18** |
| Длительность | 0.800 s | 0.567 s | 0.567 s |
| Root ground travel | 1.714 м | **1.214 м** | **1.214 м** |
| Root speed | 2.142 м/с | **2.142 м/с** | **2.142 м/с** |
| Yaw drift | ~0° | **−1.7×10⁻⁶°** | **~0°** |
| Root Y range | 2.17 см | 6.68 см | 6.83 см |
| Foot Y min / max | 0.110 / 0.285 м | 0.082 / 0.317 м | 0.080 / 0.313 м |
| Foot contact counts LH/LT/RH/RT | n/a | **3 / 8 / 3 / 6** | **3 / 8 / 3 / 6** |

**Backend A/B:** frame count, endpoints и ground trajectory совпадают. Разница по всем joint world transforms:

- max position **13.26 мм**;
- RMS position **4.13 мм**;
- max rotation **2.60°**.

Это не bit-exact результат, но семантика движения одна. Гипотеза «community backend выдаёт принципиально другой motion» в этом срезе **не подтвердилась**.

### 3.1. Производительность официального backend

После прогрева TensorRT/CUDA:

- encode **11.54 ms**;
- inference **6.23 ms**;
- decode **0.65 ms**;
- endpoint post-process **0.30 ms**.

Wall time процесса — 4.11 s, но это импорт Python, загрузка engine, plan cache и I/O, а не стоимость одного кадра. Холодный первый encode в предыдущем запуске был 2.35 s из-за компиляции Warp-ядра.

---

## 4. Unity import без обвязки

Bake настройки:

```json
{
  "groundFeet": false,
  "legIk": false,
  "loop": false,
  "bakeInPlace": false,
  "clipSpeed": 2.1423
}
```

Получено:

| Артефакт | Результат |
|---|---|
| Generic Transform clip | **91 curve**, 18 кадров, 0.567 s, `humanMotion=false` |
| Humanoid clip | **102 curve**, 18 кадров, 0.567 s, `humanMotion=true` |
| Generic root delta | `(0.006, 0.017, 1.214)`, flat **1.2138 м**, **2.142 м/с** |
| Направление | root **+Z**, pelvis facing ≈ **+Z** |
| Движение рук | local amplitude L **55.2°**, R **42.1°** |
| Колено L | **87.9–162.6°** за клип |
| T-pose | **не обнаружена**; руки в рабочем опущенном диапазоне |
| GUID generic/humanoid | стабильны при перепёке |

Текущий `MotionBricksClipValidator` даёт FAIL:

```text
hover 6.4 см > 5
foot slide 0.00 см/кадр > 2
ось шага 180.0° > 20°
```

**Это ожидаемые артефакты выбранного среза, а не доказательство обратного движения:**

- IK намеренно выключен (`groundFeet:false`, `legIk:false`) — контакт не корректируется;
- validator не нашёл ни одного contact frame (`0`), потому что запись сделана с root motion и без foot IK;
- `axisError = 180°` — fallback `hasTravel=false`, когда нет contact runs (`MotionBricksClipValidator.cs:442,452`), а не измеренное направление.

При этом generic root реально прошёл **+1.214 м по +Z** за 0.567 s, а limbs двигаются. Значит Unity import сохранил движение; текущий contact-validator к такому клипу неприменим без отдельного root-motion режима.

---

## 5. Что доказано и что нет

### Доказано

- Официальный TensorRT SOMA engine запускается и выдаёт **направленное вперёд locomotion** на собственном moving context.
- Выход имеет ожидаемую скорость **2.142 м/с**, yaw drift практически нулевой, finite poses.
- Community `sa-inbetween` на идентичном входе семантически совпадает с официальным; разница мала и численная.
- Inverse-ретаргет KL→SOMA точен до микроградусов по mapped joints.
- Текущий Unity baker сохраняет root trajectory и угловое движение в generic clip.
- Humanoid conversion больше не даёт T-pose; 102 muscle curves и движение рук/ног присутствуют.
- Baseline **12/12**, settings `mode=0`, runtime-файлы не менялись.

### Не доказано

- Визуальное качество походки: Play Mode/Animation preview пользователем не выполнялся.
- Foot contact/hover/slide на игровом риге без IK.
- Переходы, повороты, stop/start, chaining нескольких генераций.
- Поведение на Female/body swap/equipment/NGO/FO/combat.
- Production-качество и cinematic polish.
- Разница official vs community на объёме больше одного 18-кадрового сегмента.

---

## 6. Вывод для плана

**Основная гипотеза аудита подтверждена:** ранее наблюдавшаяся «плохая ходьба» была следствием неправильного входа и последующей полировки, а не доказанной неспособности MotionBricks генерировать forward locomotion.

Поэтому:

1. новые IK/guard/NPC-фичи пока **не наращивать**;
2. следующий шаг — визуальный просмотр двух клипов пользователем и **3–5 коротких клипов с собственным движущимся контекстом** (walk, turn, stop, vault/interaction);
3. переходить к T-MB08 следует после визуального подтверждения и хотя бы одного перехода (start/stop), а не после одного 18-кадрового сегмента;
4. `sa-inbetween` допустим как быстрый CPU-инструмент A/B, но production-генерация должна быть воспроизводима официальным engine и фиксировать model SHA.

---

## 7. Воспроизведение

### 7.1. Unity Edit Mode

`ProjectC/MotionBricks/Official Slice/Export KL walk seed (Edit Mode)` создаёт:

`Assets/_Project/Animations/Generated/MotionBricks/official_vertical_slice/Male/unity_pose_pack.json`

### 7.2. Python

Из `C:\Projects\motion_bricks\motionbricks`:

```powershell
.venv\Scripts\python.exe C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\official_vertical_slice.py prepare
.venv\Scripts\python.exe C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\official_vertical_slice.py official
.venv\Scripts\python.exe C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\official_vertical_slice.py community
.venv\Scripts\python.exe C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\official_vertical_slice.py analyze
```

Или одной командой: `... official_vertical_slice.py all`.

### 7.3. Unity bake

1. Скопировать `OfficialWalk_Male_30fps.f32` и `soma_bind.f32` в `official_slice_bake/Male/`.
2. Sidecar из §4.
3. `refresh_unity` (assets only) → `MotionBricksSpikeBaker.Bake`.

### 7.4. Откат

- Runtime: ничего не включалось, `MotionBricksRuntimeSettings.mode = 0`.
- Удалить локальные `official_vertical_slice/` и `official_slice_bake/` (в git не входят).
- `git revert` документационного/tool-коммита.
- Baseline-файлы не тронуты.

---

## 8. Локальные артефакты (не коммитить)

```text
Assets/_Project/Animations/Generated/MotionBricks/official_vertical_slice/Male/
  unity_pose_pack.json
  OwnedKlWalk_Source_30fps.f32
  OfficialWalk_Male_30fps.f32
  CommunityWalk_Male_30fps.f32
  official_generation.json
  community_generation.json
  raw_metrics.json
  seed_report.json
  OfficialWalk_Default14_30fps.f32      (exploratory)
  raw_metrics_default14.json            (exploratory)

Assets/_Project/Animations/Generated/MotionBricks/official_slice_bake/Male/
  OfficialWalk_Male.anim                (generic Transform)
  OfficialWalk_Male_Humanoid.anim
  OfficialWalk_Male.bake.report.json
```

NVIDIA model weights и TensorRT **не добавлялись** в проект, Packages или build.
