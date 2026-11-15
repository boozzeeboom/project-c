# T-MB02 — Ретаргет baked-клипа: диагностика и замеры

> **Дата:** 2026-11-15
> **Ветка:** `feature/motionbricks` (worktree `C:\Projects\ProjectC_client_motionbricks`)
> **HEAD до коммита:** `342b95a9` (`T-MB02: audit worktree baseline, record content-asset VCS gap`)
> **Статус:** ✅ цель спайка достигнута — `bake → import → playback на scene-объекте` работает, ретаргет **корректен по замерам**. Качество анимации — не цель T-MB02 (см. §6).
> **Unity-часть выполнялась в main** (`C:\Projects\ProjectC_client`), см. [07](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) §3 — worktree не содержит ассетов.

---

## 1. Что сделано

| Шаг | Действие | Результат |
|---|---|---|
| 1 | Извлечение bind-позы SOMA из USD (`extract_soma_bind.py`, pxr/UsdSkel) | `soma_bind.f32`, 2 156 B = 77×7 float32, `metersPerUnit` 0.01 применён |
| 2 | CPU-бейк `walk_to_jog` (sa-inbetween v0.9.4, ONNX Runtime, fp32) | `WalkToJog_Male_30fps.f32`, 107 800 B = 50×77×7, 30 fps |
| 3 | `MotionBricksSpikeBaker` — ретаргет SOMA → Humanoid-риг | `WalkToJog_Male.anim`, 22 кости / 91 кривая, 50 кадров, **1,633 с** |
| 4 | Импорт клипа в Unity, `MotionBricksBakedPlayback` на probe-объекте | PlayableGraph поверх `Animator`, без правок prefab/controller |
| 5 | Независимый замер клипа (`MotionBricksClipProbe`) | таблица §4, воспроизводимо через menu item |
| 6 | Baseline-guard | `BASELINE OK: 10 files match` (см. §7) |

Ассеты, созданные **только как новые файлы**, ничего из baseline не изменено:

```
Assets/_Project/Animations/Generated/MotionBricks/walk_to_jog/Male/   (генерация)
Assets/_Project/Settings/MotionBricks/MotionBricksRuntimeSettings.asset
Assets/_Project/Scripts/Animation/MotionBricks/*.cs
Assets/_Project/Scripts/Editor/Animation/*.cs
docs/world/MOTIONBRICK_animations/tools/extract_soma_bind.py
```

---

## 2. Симптом и корневая причина

**Симптом.** Первый ретаргет переносил локальную дельту источника в локальный фрейм рига:

```
restWorld * inverse(bindLocal) * poseLocal
```

Левая рука уезжала **вверх** (`upperArm.L · down = −0,44…−0,91`), правая оставалась внизу, но с меньшей амплитудой (`+0,26…+0,66`). Зеркальная симметрия терялась на всех парных конечностях.

**Диагностика (замеры, временный `MB_MirrorDiag.cs`, удалён).**

| Величина | Значение | Вывод |
|---|---|---|
| `rigMirror` (8 пар L/R, rest-поза FBX) | `0,0°` | rest-поза рига **идеально зеркальна** — дело не в риге |
| `srcMirror` / `Fmirror` (руки, ноги) | `178–180°` | источник зеркален «как положено», но в **своих** локальных осях |
| `srcMirror` плечо `11→12` | `139,5°` | плечевая цепочка ведёт себя иначе (см. §5) |
| `u_s` — смещение ребёнка в локальном фрейме кости **источника** | локальная **±X** (`−X` слева, `+X` справа) | |
| `u_t` — то же в локальном фрейме кости **рига** | локальная **+Z** с обеих сторон | |
| `uAngle` между ними | **90,0° на каждой цепочке** | корневая причина |

**Корневая причина.** Локальные оси костей у источника и у рига не совпадают: у SOMA дочерняя кость лежит вдоль локальной `±X` (знак разный слева/справа), у рига Kevin Iglesias — вдоль локальной `+Z` (знак одинаковый). Перенос локальной дельты внутри чужого фрейма даёт ошибку 90° на каждую кость, причём **со сменой знака по сторонам** — отсюда «одна рука вверх, другая асимметрично вниз».

**Скрутка (roll) вдоль оси кости из T-позы принципиально не определена** (цепочка руки коллинеарна), поэтому любая схема «перенос локальной дельты» обязана её угадывать — и угадывает по-разному для левой и правой стороны.

---

## 3. Итоговая схема ретаргета (полный мировой поворот кости)

```csharp
// 1. SOMA: parent-local -> global (в осях Unity)
var localRot = ToUnityRot(new Quaternion(f[o+3], f[o+4], f[o+5], f[o+6]));
var localPos = ToUnityPos(f[o], f[o+1], f[o+2]);
rot[j] = parent < 0 ? localRot : rot[parent] * localRot;
pos[j] = parent < 0 ? localPos : pos[parent] + rot[parent] * localPos;

// 2. Мировая дельта кости относительно bind-позы.
//    (R_pose · R_bind⁻¹) переносит и направление, и качение источника.
//    FromToRotation(restDir, poseDir) качение теряет — каждому суставу достаётся
//    своя случайная скрутка, мышцы уходят за [-1..1], в игре конечности выворачивает.
Quaternion delta = frameRot[seg.Bone] * Quaternion.Inverse(restRot[seg.Bone]);
deltaByPose[seg.Bone] = yaw * delta * yawInv;            // BakeInPlace

// 3. Применение к ригу: rest-поза рига сохраняется, поверх неё кладётся мировая дельта
animWorld = delta * restWorld[t];
locals[t][frame] = Quaternion.Inverse(animParentWorld) * animWorld;
```

Пересчёт осей: `ToUnityPos = (-x, y, z)`, `ToUnityRot = (x, -y, -z, w)` — сопряжение матрицей `M = diag(-1,1,1)`, внесено один раз на входе.

**Сегменты (14):** `(0,1) (1,3) (3,4) (4,6) (12,13) (13,14) (40,41) (41,42) (67,68) (68,69) (69,70) (72,73) (73,74) (74,75)`.
Кости без своей цепочки (`B-hand.L/R`, `B-toe.L/R`, `B-jaw`, `B-head`, `B-shoulder.L/R`) наследуют поворот родителя: `animWorld = animParentWorld * t.localRotation`. Нулевой кватернион `Quaternion(0,0,0,0)` в клип не попадает ни при каких условиях.

**Карта костей (22):** `0 B-hips · 1 B-spine · 3 B-chest · 4 B-neck · 6 B-head · 8 B-jaw · 11 B-shoulder.L · 12 B-upperArm.L · 13 B-forearm.L · 14 B-hand.L · 39-42 R · 67-70 thigh/shin/foot/toe.L · 72-75 R`. `spine2` (2) и `neck2` (5) пропущены.

**Что попадает в лог:** `сегментов 14`, `swing: средний 46,8°, максимум 141,7°`, деградировавшие сегменты. Swing теперь считается **только как контроль направления** и в позу не идёт — поза строится из мировой дельты. Отдельно печатается число мышц вне `[-1..1]` при каждом humanoid-перепёке.

## 4. Замеры клипа (независимая проверка)

`MotionBricksClipProbe` читает кривые готового `.anim` через `AnimationUtility.GetCurveBindings` + `GetEditorCurve`, применяет их к свежему инстансу FBX (все `Animator` выключены) и измеряет **мировые** направления. Play Mode не нужен.

Опорные значения rest-позы: `hipsY = 0,980  headY = 1,607  footLY = footRY = 0,128`, `upperArmL = (−1,00, −0,07, 0,02)`, `upperArmR = (1,00, −0,07, 0,02)`.

```
t     hipsY  headY  footLY footRY | armL·dn armR·dn legL·dn legR·dn toeL·dn toeR·dn
0,00  0,952  1,574  0,164  0,299 |   0,93    0,90    0,97    0,89    0,68    0,95
0,40  0,932  1,550  0,146  0,124 |   0,99    0,83    0,96    0,94    0,65    0,51
1,20  0,959  1,567  0,115  0,108 |   0,92    0,89    1,00    0,99    0,18    0,16
1,60  0,960  1,575  0,111  0,109 |   0,92    0,93    1,00    1,00    0,11    0,10
armLen L = 0,234  R = 0,234  legLen L = 0,412  R = 0,412  handWorldY L = 1,031  R = 1,028
```

**До исправления** было `armL·dn = −0,44…−0,91` (рука вверх), `armR·dn = +0,26…+0,66`. Замеры корпуса (hips/head) практически не изменились — то есть починка затронула именно перенос ориентаций, а не общую позу.

**Что подтверждено замером:**

- обе руки опущены и качаются (`armL·dn` 0,90–0,99, `armR·dn` 0,83–0,93);
- ноги опущены (`legL·dn` / `legR·dn` 0,89–1,00), стопы поочерёдно поднимаются к rest-высоте;
- осанка, вертикальное качание таза (~4 см) и стабильность головы сохранены;
- длины костей рига не изменились (`armLen 0,234` с обеих сторон, `legLen 0,412` с обеих сторон) — ретаргет не «растягивает» скелет.

---

## 5. Ограничения спайка (осознанные, не регрессии)

| Ограничение | Причина / статус |
|---|---|
| Плечи не поднимаются (нет shrug) | `11→12` даёт swing 173° на f46, `39→40` — 179° на f39: суставы почти совпадают, направление шумит и **переворачивается в середине цикла**. Прицеливание выбросило бы плечевой сустав на противоположную сторону грудной клетки. Сегменты убраны из `Segments`; `B-shoulder.L/R` наследуют поворот груди. |
| Кисти, пальцы, челюсть, голова — без переноса скрутки | Для них нет смаппленной дочерней кости в SOMA-сегментах, они наследуют родителя. Для спайка допустимо. |
| Нет IK, стопа не прижата к земле | `footRY` поднимается до `0,299` (0,171 выше rest 0,128) — нога «висит» на кадре 0. Нужен foot-IK слой (вне T-MB02). |
| Модель переноса rotation-only | Корневая трансляция обнуляется (`BakeInPlace = true`): X/Z дельты hips занулены, вертикаль сохранена. Перенос поступательного движения — отдельная задача. |
| Клип не в `*.controller` | По правилу T-MB02 контроллер не трогали: воспроизведение идёт через `PlayableGraph` (`06_SAFE_ROLLOUT §4.2`), т.к. `Animator.CrossFade` не умеет играть произвольный `AnimationClip`. |

---

## 6. Что НЕ входит в T-MB02 и требует решения

1. **Качество анимации** (плавность, контакт стопы, инерция) — это T-MB04. Спайк доказывает только техническую цепочку.
2. **Плечи.** Нужен либо отдельный источник направления ключицы (например, вектор от `spine2` к `upperArm` с фильтрацией по времени), либо отказ от анимации ключиц в пользу additive-слоя.
3. **IK стоп** — слой поверх PlayableGraph.
4. **Лицензия.** Библиотека поз `walk_*` / `jog_*` — eval-only sample data NVIDIA Open Model Agreement. Все производные бинарники (`*.f32`, `*.saib`) в git **не** коммитятся, см. §7.

---

## 7. Git-гигиена этого этапа

| Что | Куда попало |
|---|---|
| `MotionBricksBakedPlayback.cs`, `MotionBricksRuntimeSettings.cs`, `MotionBricksClipProbe.cs`, `MotionBricksSpikeBaker.cs`, `MotionBricksRuntimeSettings.asset`, `WalkToJog_Male.bake.json` | коммит в `feature/motionbricks` (авторские файлы) |
| `tools/extract_soma_bind.py` | коммит в `feature/motionbricks` |
| этот отчёт | коммит в `feature/motionbricks` |
| `*.anim`, `*.f32`, `*.saib` | **не коммитятся**: `*.anim` игнорируется `.gitignore:167`; `.f32`/`.saib` — производные NVIDIA eval-only данных (лицензия §6.4). Воспроизводятся: `extract_soma_bind.py` → CPU-бейк → menu item бейка |
| `.meta` файлы | игнорируются (`.gitignore:182`) — известный разрыв T-MB01B, см. [07](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) §2 |
| `WorldScene_0_0.unity` | игнорируется (`.gitignore:152`), в коммит попасть не может |

Baseline после работ: **`BASELINE OK: 10 files match`** (`check_baseline.ps1 -SkipPackages` против `C:\Projects\ProjectC_client`). Пакеты в main не менялись; ровно 10 защищённых файлов на месте.

---

## 8. Воспроизведение

```
1) C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe ^
     docs\world\MOTIONBRICK_animations\tools\extract_soma_bind.py ^
     --usd "...\motionbricks\assets\human\soma_base_skel_minimal_no_root.usd" ^
     --out "Assets\_Project\Animations\Generated\MotionBricks\walk_to_jog\Male\soma_bind.f32"

2) CPU-бейк sa-inbetween v0.9.4  ->  WalkToJog_Male_30fps.f32   (50×77×7, 30 fps)

3) Unity: ProjectC/MotionBricks/T-MB02 Bake WalkToJog (Male)
   Unity: ProjectC/MotionBricks/T-MB02 Probe Baked Clip      (замер, Play Mode не нужен)

4) Playback: probe-объект WorldRoot_0_0/MB_Probe_Player
   (39839.0, 2533.0, 40007.0) -> HumanM_Model (Animator) + MotionBricksBakedPlayback
```

**Порядок перед каждым бейком:** `refresh_unity(compile: request)` → `check_compile_errors` → menu item. Иначе `execute_menu_item` запускает **предыдущую** скомпилированную версию бейкера.

---

## 9. Открытый пункт

Визуальное подтверждение в Play Mode не выполнено (по правилу проекта Play Mode запускает пользователь). `HANDOFF.md §9` требует «Пользователь подтвердил визуально». До этого пункта T-MB02 остаётся закрытым **по замерам**, но не по визуалу.

---



## Поправка 2 (Play Mode): «куклу выворачивает наизнанку»

Наблюдение пользователя: humanoid-клип кости двигает (это уже подтверждено), но конечности выглядят выломанными.

### Найденные дефекты бейкера

1. **Нулевые кватернионы.** `EvalFrame` писал `locals[t][frame]` только костям из `Segments`. Остальные 8 костей (`B-head`, `B-jaw`, `B-shoulder.L/R`, `B-hand.L/R`, `B-toe.L/R`) оставались с `Quaternion(0,0,0,0)` — невалидный поворот. Замер по клипу: `|q| = 0,0000` у этих 8, `|q| = 1,0000` у остальных 14.
2. **Скрутка на каждом суставе от `swing * restWorld`.** Кость получала минимальный поворот только по направлению — качение не переносилось, и каждому суставу доставалась своя случайная скрутка. Замер мышц: **24 из 95 вне [-1..1]**, в т.ч. `Left Foot Twist In-Out -4,63..4,79`, `Right Upper Leg Twist In-Out -2,42..-0,01`, `Left Arm Down-Up -1,91..-0,50`, `Left Shoulder Down-Up 2,98`, `Jaw Close 10,03`.
3. Humanoid-плеер клампит мышцы по лимитам аватара, поэтому в игре поза не совпадала с запечённой. Замер несовпадения (поза из Transform-кривых vs поза, восстановленная из мышц через `HumanPoseHandler.SetHumanPose`): **до 90°** (`B-shin.L` 87,8° при t=0,8).

### Исправление (`MotionBricksSpikeBaker.cs`)

- Переносится **полный мировой поворот кости**: `animWorld = (R_pose · R_bind⁻¹) · restWorld_рига`. Качение источника сохраняется, суставных скруток не появляется. Направления костей не изменились (`armL·dn` 0,92–0,99, `legL·dn` 0,94–1,00).
- Кости без своей цепочки наследуют поворот родителя со своим rest-локалом вместо нулевого кватерниона.
- В humanoid-бейк добавлен контроль: количество мышц вне [-1..1] печатается в лог при каждом перепёке.

### Результат

| Метрика | было | стало |
|---|---|---|
| мышц вне [-1..1] | 24 | **3** (`Jaw Close` 1,03, `Thumb 1 Stretched` −1,48 — статические, пальцы/челюсть) |
| угол «Transform-кривые vs мышцы» | до 90° | **≤ 19°** (большинство < 10°) |
| невалидных кватернионов в клипе | 8 | **0** |

Поза t = 0,4 с проверена визуально на временном инстансе в сцене: руки вдоль корпуса с лёгким сгибом, ноги в шаге, стопы ориентированы правильно, вывернутых конечностей нет.

Остаточные расхождения ≤ 19° дают лимиты мышц аватара (Twist/Stretch), а не арифметика ретаргета.

**Статус:** перепёк сделан **после** этого наблюдения. Повторный Play Mode ещё не запускался — визуальная приёмка за пользователем (§9 выше остаётся открытым до неё). Раздел §3 этого отчёта переписан под новую схему: прежняя (direction/swing, без качения) была источником дефекта.

### Побочно: «нет камеры» в Play Mode

Play запускался с открытой только `WorldScene_0_0`: в рантайме было `sceneCount = 1`, камер — 0. Камера (`MainCamera`, tag MainCamera) и игрок живут в `BootstrapScene`; открыть её аддитивно рядом с миром — камера появляется (проверено: `cameras = 1`).


## Поправка 3 (Play Mode): «кукла заморожена»

Симптом: в Play Mode кукла стоит в статичной позе. В логе сессии **нет ни одного** сообщения
`[MotionBricksBakedPlayback]` — playback не запускался вообще, а не «играл не ту позу».

### Причина

`MotionBricksBakedPlayback.OnEnable` играет клип только при `_clip != null`. На объекте
`WorldRoot_0_0/MB_Probe_Player` поле `_clip` оказалось **null в загруженной сцене**: бейкер сохранял клип
через `AssetDatabase.DeleteAsset` + `AssetDatabase.CreateAsset`, а сцена держит ссылку по GUID — после
пересоздания ассета in-memory ссылка в сцене обнуляется. В YAML сцены ссылка при этом остаётся валидной
(`_clip: {fileID: 7400000, guid: 971e390ab04243343815da4e8c79c0a7}`), поэтому в редакторе поломка не видна —
только в Play Mode.

### Исправление

- `MotionBricksSpikeBaker.SaveClipInPlace(clip, path)`: если ассет существует — `ClearCurves()` + перенос
  биндингов (`AnimationUtility.GetCurveBindings`, object-reference curves) + clip settings, `SetDirty`,
  `AssetDatabase.SaveAssets()`. GUID и fileID сохраняются, ссылки из сцен/префабов не рвутся; новый ассет
  создаётся только если его нет.
- Через этот метод теперь сохраняются **оба** клипа (generic и humanoid).
- Сцена: `_clip` переустановлен на `WalkToJog_Male_Humanoid` (`humanMotion = True`, 102 кривые),
  `_playOnEnable = 1`, `_loop = 1`, `_logVerbose = 1`; `WorldScene_0_0` сохранена.

### Проверки после перепёка (14:12)

| Проверка | Значение |
|---|---|
| `WalkToJog_Male.anim` | guid `dc5f1d88…`, 91 кривая, `humanMotion = False`, длина 1,633 s |
| `WalkToJog_Male_Humanoid.anim` | guid `971e390a…` (**не изменился** после перепёка), 102 кривые, `humanMotion = True` |
| мышц вне `[-1..1]` | 3 (как и до перепёка) |
| гейт | `MotionBricksRuntimeSettings.mode = 1` (BakedClips), `allowPlayer = 1` |
| сцена | `_clip` = `WalkToJog_Male_Humanoid`, `dirty = False` |

**Правило на будущее:** клип, на который ссылаются сцена/префаб, нельзя пересоздавать через
Delete+Create — только перезаписывать на месте. Иначе получаем «замороженную куклу» без внятной ошибки.

Play Mode после этой правки ещё не запускался.


## Поправка 4: какой клип играет probe

После Поправки 3 `_clip` был выставлен на `WalkToJog_Male_Humanoid` (muscle-клип). Решение пересмотрено
в пользу **generic** `WalkToJog_Male`:

- Именно generic-клип был **доказанно проигран в Play Mode** (15.11, 13:13–13:39): в логе
  `[MotionBricksBakedPlayback] Play 'WalkToJog_Male'`, а одноразовый `diag` показал изменение
  `B-upperArm.L` на ~93° за время работы. То есть Animator с Humanoid-аватаром и
  `runtimeAnimatorController = null` играет клип с Transform-кривыми через `PlayableGraph`.
- Для muscle-клипа такого замера в Play Mode нет.
- Прежнее утверждение «на Humanoid-аватаре клип с Transform-кривыми скелет не двигает» этим замером
  **опровергается** — в бейкере оно оставлено как гипотеза, но в деле не подтверждалось.
- Generic-клип проверен точнее: его кривые — это `localRotation` самого рига (round-trip на трансформах,
  `armLen 0,234`, `legLen 0,412`, направления костей), тогда как muscle-клип восстанавливается через
  `HumanPoseHandler` с остатком ≤ 19°.

Итог: на probe стоит `WalkToJog_Male` (`humanMotion = False`, 91 кривая), `_playOnEnable = 1`,
`_loop = 1`, `_logVerbose = 1`; `WorldScene_0_0` сохранена. Muscle-клип остаётся на диске как второй
вариант для сравнения.

### Почему Edit Mode здесь не судья

Контрольный опыт в preview-сцене (`PlayableGraph` + `graph.Evaluate(0.2f)` × 8, риг `HumanM_Model`):
**ни наш muscle-клип, ни штатный `HumanM@Walk01_Forward`** кости не двигают (0,0° по `B-upperArm.L`),
двигается только `hips` у нашего клипа. Значит `graph.Evaluate` в Edit Mode не прогоняет humanoid-пасс
Animator'а — скелет обновляет только `Animator.Update` в Play Mode. Оценивать позу этим способом нельзя,
и в отчёт как «проверено» это не идёт.


## Поправка 5: почему «двигается по замеру, а в кадре стоит» (отмена Поправки 4)

Замер из Play Mode (после Поправки 4, `_clip = WalkToJog_Male`, `humanMotion = False`):

```
[MotionBricksBakedPlayback] Play 'WalkToJog_Male' (1,63 s, speed 1,00) на 'HumanM_Model'
[MotionBricksBakedPlayback] diag: t=0,35/1,63 s, кость B-upperArm.L:
   было (14.0, 200.2, 338.1), стало (34.2, 281.6, 48.3), controller=null, humanMotion=False
```

Кость «повернулась», а кукла в кадре не двигалась. Причина — **где читается замер**: `diag` снимался в
`Update()`, то есть **до** того, как Animator применил свою позу. Animator с Humanoid-аватаром прогоняет
humanoid-пасс и **перезаписывает** кости из внутренней muscle-позы; клип с одними Transform-кривыми
(`humanMotion = False`) muscle-позу не задаёт, поэтому в кадре остаётся bind-поза, а в `Update` читается
сырой выход графа. Прежние «~93°» из замера 13:13 — тот же артефакт, а не доказательство движения.

**Вывод (подтверждает изначальную гипотезу бейкера):** на Humanoid-аватаре играть нужно **muscle-клип**.
Поправка 4 неверна и отменена.

### Исправлено

- `_clip` = `WalkToJog_Male_Humanoid` (`humanMotion = True`, 102 кривые, 91 кривая мышц + 7 root;
  мышц вне `[-1..1]` — 3 статические). `WorldScene_0_0` сохранена.
- `MotionBricksBakedPlayback`: диагностика перенесена в `LateUpdate()` — читается после Animator'а,
  то есть ровно то, что видит игрок. В `Update()` остался только `KeepLooping()`.
- `StopPlayback()` больше не зовёт `Rebind()`/`Update(0)` на неактивном Animator — убрана ошибка
  `Can't call Animator.Update on inactive object` при выгрузке сцены.

### Что подтвердит следующий плейтест

`diag` теперь в `LateUpdate`: если в нём кость меняется — меняется и кадр. Если кость в `LateUpdate`
стоит — humanoid-пасс не получает позу, и тогда причина в muscle-кривых, а не в playback.


## Итог T-MB02: приёмка Play Mode (15.11.2026)

### Лог приёмки (пользователь, Play Mode)

```
[MotionBricksBakedPlayback] Play 'WalkToJog_Male_Humanoid' (1,63 s, speed 1,00) на 'HumanM_Model'. Controller не менялся.
[MotionBricksBakedPlayback] diag: t=0,64/1,63 s, кость B-upperArm.L: было (30.6, 254.8, 62.9),
   стало (30.6, 254.8, 62.9), controller=null, humanMotion=True
[MotionBricksBakedPlayback] Playback остановлен, baseline Animator восстановлен.
```

Визуально: поза корректная, конечности не вывернуты, клип проигрывается как **разворот на месте**.

### Почему «разворот на месте» — это верно

Замер исходных кадров `WalkToJog_Male_30fps.f32` (корневой джойнт, раскрутка по непрерывности):

| кадр | rootYaw | позиция XZ |
|---|---|---|
| f0 | −71,0° | (0,00, 0,01) |
| f10 | −127,3° | (0,09, 0,23) |
| f20 | −197,3° | (0,13, 0,29) |
| f30 | −238,3° | (0,06, 0,18) |
| f49 | −267,7° | (0,03, −0,04) |

Корень поворачивается на **≈ −197°**, смещение по XZ — **0,06 м**. То есть демо-клип сам по себе
разворачивает персонажа на месте, и ретаргет переносит его верно. Ни `BakeInPlace`, ни перенос рыскания
трогать не нужно — для «шага на месте» нужен другой **сид-клип**, а не правка ретаргета (это T-MB04).

### Мышцы в клипе

102 root-кривые: 32 меняются больше чем на 0,1 (`Left Upper Leg Front-Back` 0,35..0,93,
`Right Lower Leg Stretch` 0,12..0,86, `Right Upper Leg Front-Back` −0,08..0,53), руки заметно слабее
(`Left Arm Down-Up` −0,62..−0,36, `Right Arm Down-Up` −0,79..−0,49).

**Открытый пункт (перенесён в T-MB04):** `diag` по `B-upperArm.L` показал неизменную позу
(30.6, 254.8, 62.9 до и после), хотя muscle-кривые рук меняются на 0,26–0,30. Либо кость в `diag`
не та, что двигает humanoid-пасс, либо мышцы рук до кости не доходят. Проверять на клипе с ходьбой
вперёд, где амплитуда рук больше.

### Baseline

```
check_baseline.ps1 -SkipPackages
  OK  PlayerAnimation.controller, PlayerAnimation_Default/Female.overrideController,
      NetworkPlayer.prefab, Npc_Goblin.prefab, NpcAnimator_Goblin.overrideController,
      NetworkPlayer.cs, CharacterCustomisationApplier.cs, SkillAnimationPlayer.cs, NpcBrain.cs
  SKIP Packages/manifest.json, Packages/packages-lock.json (package guard disabled)
BASELINE OK: 10 files match.
```

`git diff -- Packages` пуст и в worktree, и в main — пакеты не менялись.

### Definition of Done T-MB02

| Пункт | Статус | Чем подтверждён |
|---|---|---|
| Baseline check PASS до и после | ✅ | `BASELINE OK: 10 files match` (до — `342b95a9`, после — 15.11) |
| Один baked-клип импортирован в `Generated/MotionBricks/` | ✅ | `WalkToJog_Male` (91 кривая) + `WalkToJog_Male_Humanoid` (102) |
| Проигрывается на scene-объекте через `PlayableGraph`, без правок prefab/controller | ✅ | лог `Play 'WalkToJog_Male_Humanoid'`, `controller=null` |
| После остановки — baseline Animator (нет T-pose) | ✅ | `StopPlayback` → `Rebind()` + `Update(0)`, лог «baseline Animator восстановлен» |
| Нет изменений в baseline-файлах | ✅ | 10/10 `OK`, `Packages` SKIP |
| Пользователь подтвердил визуально | ✅ | «проигрывает анимацию разворота на месте — выглядит корректно» |
| Отчёт: что сгенерировали, чем, метрики, проблемы | ✅ | этот документ, §1–§9 + Поправки 2–5 |

### Что вынесено за пределы T-MB02

1. Сид-клип с ходьбой вперёд (сейчас демо разворачивает на месте).
2. Foot IK: `footRY` доходит до 0,299 при rest 0,128 — ступня «висит» (зафиксировано в §5).
3. Проверка слабого движения рук (см. открытый пункт выше).
4. Всё остальное — по плану `HANDOFF §4` (T-MB03 → T-MB04 → T-MB05 → T-MB08 go/no-go).

*Связанные: [HANDOFF.md](HANDOFF.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [07_T-MB02_WORKTREE_BASELINE_FINDINGS.md](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) · [baseline/BASELINE_MANIFEST.md](baseline/BASELINE_MANIFEST.md)*
