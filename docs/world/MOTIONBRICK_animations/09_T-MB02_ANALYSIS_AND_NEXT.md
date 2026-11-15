# T-MB02 — анализ результатов и следующие шаги

> **Дата:** 2026-11-15
> **Ветка:** `feature/motionbricks` (worktree `C:\Projects\ProjectC_client_motionbricks`)
> **Основа:** [08_T-MB02_RETARGET_FINDINGS.md](08_T-MB02_RETARGET_FINDINGS.md) + [07_T-MB02_WORKTREE_BASELINE_FINDINGS.md](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md)
> **Статус:** T-MB02 spike принят по замерам и визуалу; production-интеграции нет.

---

## 1. Что реально доказал T-MB02

| Что | Доказательство |
|---|---|
| CPU-бейк SOMA → `.anim` работает | `WalkToJog_Male_30fps.f32` 50×77×7, 30 fps; `WalkToJog_Male.anim` 91 кривая, 1.633 s |
| Humanoid-бейк работает | `WalkToJog_Male_Humanoid.anim` 102 кривые (91 мышца + 7 root), `humanMotion = True` |
| Ретаргет исправлен | полный мировой поворот `(R_pose · R_bind⁻¹) · restWorld`; мышц вне `[-1..1]` 24 → 3; расхождение Transform vs muscles ≤ 19° |
| Playback через `PlayableGraph` работает | `Play 'WalkToJog_Male_Humanoid'`, `controller=null`, `humanMotion=True`; клип играет |
| Baseline не сломан | `check_baseline.ps1 -SkipPackages` → 10/10; пакеты не менялись |
| Откат работает | `StopPlayback` → `Animator.Rebind()` + `Update(0)`, лог «baseline Animator восстановлен» |
| Пользователь подтвердил визуал | «проигрывает анимацию разворота на месте — выглядит корректно» |

**Ключевое:** это proof-of-pipeline, а не production-анимация. T-MB02 отвечает на вопрос «можно ли вообще испечь и проиграть», а не «хорошо ли это выглядит в игре».

---

## 2. Что T-MB02 не доказал (и не должен был)

1. **Качество движения.** Один демо-клип `walk_to_jog`, который в источнике разворачивает на месте на ≈197°, а не идёт вперёд. Это не баг ретаргета — это свойство сид-клипа.
2. **Покрытие locomotion.** Нет forward walk/run/strafe/stop/turn-in-place, нет blend tree, нет скорости.
3. **IK и контакт стоп.** `footRY` доходит до 0.299 при rest 0.128 — стопа «висит»; foot IK не сделан.
4. **Плечи.** Сегменты `11→12` / `39→40` исключены из-за degenerate swing; `B-shoulder.L/R` наследуют грудь. Пожимания плеч/ключиц нет.
5. **Кисти/пальцы/челюсть/голова.** Скрутка не переносится; они наследуют родителя. Полных пальцев и мимики нет.
6. **Слабые руки.** `diag` по `B-upperArm.L` не изменился, хотя muscle-кривые рук меняются на 0.26–0.30. Это открытое противоречие: либо diag смотрит не ту кость, либо мышцы рук не доходят до кости. Пока не разрешено — нельзя утверждать, что руки анимируются корректно.
7. **Root motion.** `BakeInPlace = true`: XZ-смещение hips обнулено, вертикаль и yaw сохранены. Перенос поступательного движения — отдельная задача.
8. **Сеть/FO.** Probe-объект в `WorldScene_0_0`, но network/2-client/F8-F9 не тестировались.
9. **Body swap M↔F.** Код подписки на `BodySwapped` есть, но на player-префабе/в Play Mode не проверялся.
10. **Animation Events / blending / controller.** Клип играется через `PlayableGraph`, не через state; событий и blend-переходов нет.

---

## 3. Ответы на вопросы

### 3.1. Покроем ли 100% различных бесшовных анимаций игрока через MotionBricks?

**Нет. 100% — недостижимо в текущей архитектуре и с текущей моделью.**

| Категория | Покрытие MotionBricks | Комментарий |
|---|---|---|
| Idle / walk / run / strafe / turn / stop | ⚠️ Частично | Модель умеет locomotion, но нужны сид-клипы, target-позы и валидация; текущий демо — только разворот |
| Переходы (start/stop/turn) | ✅ Потенциально сильная сторона | Это in-betweening; но качество/бесшовность надо доказывать на 3–5 клипах |
| Jump / land / vault / climb | ⚠️ Частично | Нужны target-позы, IK, физика; без них будут провалы/парения |
| Sit / pick-up / use / open | ⚠️ Потенциально | Smart Primitives + target-позы; в Unity ещё не реализовано |
| Combat / attack / hit / death | ❌ Нет | Тайминг/события/хитбоксы authored; модель не гарантирует |
| Пальцы / мимика / emotes | ❌ Нет | SOMA 30 joints, нет полных пальцев и blendshapes |
| Cinematics / камера | ❌ Нет | Не тот инструмент |
| NPC-толпа | ❌ Runtime нет | Только offline-baked пул |

**Реалистичная оценка:** после T-MB03/04 можно покрыть **часть body-locomotion и traversal** (условно 40–70% типовых переходов), но не 100%; combat/face/fingers/emotes остаются authored. Любая цифра «100%» — маркетинг.

Почему «бесшовность» не гарантирована:
- одна генерация 0.6–1.9 с; длинное движение — цепочка, ошибки накапливаются;
- модель не видит геометрию/коллизии;
- ретаргет на 22 кости + пропущенные плечи/кисти/пальцы;
- нет IK; стопа может висеть/скользить;
- качество зависит от seed/target-поз и training distribution (BONES RP1), а не от нашей игры.

---

### 3.2. Какие ограничения будут с запеканием?

| Ограничение | Почему / что делать |
|---|---|
| **Длина клипа 0.6–1.9 с** | 18–58 кадров @30 Hz; длинное движение — несколько генераций/склеек |
| **SOMA 30 → Unity Humanoid 22 кости** | В спайке 22 кости / 91 кривая; `spine2`/`neck2` пропущены |
| **Плечи не переносятся** | `11→12`/`39→40` degenerate; нужен отдельный источник направления ключицы |
| **Кисти/пальцы/челюсть/голова без скрутки** | Нет смаппленной дочерней кости; наследуют родителя |
| **3 мышцы вне `[-1..1]`** | `Jaw Close`, `Thumb Stretched`; Humanoid clamp; глазами/челюстью не торговать |
| **Расхождение ≤ 19°** | Остаток лимитов аватара (Twist/Stretch), не арифметики; для качества нужно проверять |
| **`BakeInPlace`** | XZ hips обнулены; root yaw сохранён; для root motion — отдельный режим |
| **Нет foot IK** | `footRY` 0.299 vs rest 0.128; стопа висит; нужен IK-слой |
| **Нет collision/penetration** | Модель кинематическая; проверять вручную/валидатором |
| **Seed/target quality** | Демо `walk_to_jog` — разворот на 197°, не ходьба; нужен forward-walk seed |
| **Нет Animation Events** | Combat/trigger-события не появляются сами; для locomotion не критично, для боя — нельзя |
| **`.anim`/`.meta` не в git** | Контент в Lore; `SaveClipInPlace`, не Delete+Create; иначе ссылки из сцены рвутся |
| **Лицензия** | `walk_*`/`jog_*` — eval-only sample data; производные `.f32`/`.saib` не коммитить; для релиза — свои позы |
| **Нет batch/validation** | Спайк хардкодит `WalkToJog`; T-MB03 должен обобщить и добавить метрики |
| **Playback без controller** | `PlayableGraph` играет клип, но нет blend/state/events; для игры нужен слой состояний |

**Вывод:** запекание даёт **детерминированный, дешёвый в runtime** клип, но качество и покрытие ограничены ретаргетом, IK, seed-клипами и лицензией. Это не «нажал кнопку — получил идеальную анимацию».

---

### 3.3. НПС получат пул «живых» анимаций?

**В текущей архитектуре — нет runtime-«живого» пула.** `allowNpc = false`; NPC остаются на `NpcBrain` + `NavMeshAgent` + `NetworkTransform` + Animator.

Что возможно:
- **Offline-baked пул**: испечь N вариантов (idle/walk/run/turn/work/sit) и играть их через NPC controller/OverrideController. Это **пул клипов**, а не генерация.
- **Рандомизация вариантов**: выбрать случайный baked-клип из пула для idle/work; дёшево, детерминированно, работает на всех клиентах.
- **Runtime neural для NPC**: не рекомендуется. Server-authoritative `NavMeshAgent`/`NetworkTransform` будет драться с root motion; генерация на клиенте косметическая; на сервере — дорого/недетерминировано; 100+ NPC — CPU/bandwidth/GPU не сходятся.
- Если продукт требует именно «живых» NPC — это отдельная архитектура (server-side animation service или клиентский GPU-бюджет), не текущий MMO-клиент.

**Итог:** NPC могут получить **пул baked-анимаций** (контентное разнообразие), но не бесконечную runtime-генерацию. Для MMO это правильный компромисс.

---

### 3.4. В чём выигрыш против текущего Animator + locomotion?

| Критерий | Текущий Animator + клипы | MotionBricks baked | MotionBricks runtime |
|---|---|---|---|
| Детерминизм | ✅ | ✅ (клип) | ❌ (GPU/FP) |
| Runtime cost | ✅ низкий | ✅ 0 neural | ❌ высокий |
| Покрытие locomotion | ✅ есть, но ручные переходы | ⚠️ можно закрыть gaps | ⚠️ body-only |
| Необычные переходы | ❌ ручная работа | ✅ сильная сторона | ✅ |
| Стили (injured/stealth/zombie) | ❌ нужны клипы | ✅ можно испечь | ✅ |
| Object interactions | ❌ ручная работа | ✅ target-позы | ✅ |
| Combat/events | ✅ authored | ❌ не трогаем | ❌ |
| Пальцы/лицо | ✅ authored | ❌ | ❌ |
| Foot contact/IK | ⚠️ зависит от клипов | ❌ пока нет | ❌ |
| MMO/NPC scale | ✅ | ✅ | ❌ |
| Content/licensing | ✅ свои клипы | ⚠️ eval-only sample | ⚠️ |
| Build size | ✅ | ✅ | ❌ 380–758 MB |

**Реальный выигрыш сейчас — authoring velocity и закрытие дыр в контенте**, а не «лучше текущего Animator»:
- быстро испечь переход, которого нет в ассетах (turn-in-place, injured walk, vault, sit);
- получить варианты стиля без ручной анимации;
- добавить NPC-пул без runtime-модели.

**Где текущий Animator лучше:** стандартный locomotion, combat, facial/fingers, детерминизм, нулевой runtime-риск. Поэтому не заменять его целиком.

---

## 4. Риски и safety debt (важно до следующего этапа)

| Риск | Статус | Что сделать |
|---|---|---|
| `MotionBricksRuntimeSettings.mode = 1` (BakedClips) | В main asset сейчас **включён** | Перед завершением/мержем поставить `Disabled` или удалить probe из сцены; иначе любой `MotionBricksBakedPlayback` начнёт играть |
| Probe `WorldRoot_0_0/MB_Probe_Player` в `WorldScene_0_0` | Есть, с клипом | Оставить только в dev-сцене или удалить после приёмки; не тащить в production |
| `B-upperArm.L` diag не меняется | Открыто | Проверить на forward-walk клипе; возможно, diag смотрит не ту кость |
| Плечи/кисти/пальцы/челюсть | Ограничение | T-MB04: решение (ключица/additive/отказ) |
| Foot IK | Нет | T-MB04: IK-слой, проверка `footRY` |
| Worktree не содержит Lore-контент | Подтверждено 07 | Код/docs — worktree; Unity-ассеты — main; baseline check в main; `-SkipContent` в worktree |
| Sample data eval-only | Подтверждено | Для релиза — свои seed/target-позы; `.f32`/`.saib` не коммитить |
| `.anim`/`.meta` ignored | Подтверждено | `SaveClipInPlace`; не Delete+Create; ссылки из сцены |
| Body swap M↔F не тестирован | Код есть | T-MB04: Play Mode M→F→M на player-префабе |
| Network/FO не тестированы | Нет | T-MB04/05: 2 clients + F8/F9 |

---

## 5. Что дальше (приоритет)

### Шаг 0 — безопасность (сразу)

1. Поставить `MotionBricksRuntimeSettings.mode = Disabled` **или** убрать probe-объект из `WorldScene_0_0` до следующего этапа.
2. `check_baseline.ps1` в main → PASS.
3. Зафиксировать решение: probe — dev-only, не в production-сцене.

### Шаг 1 — T-MB04a: retarget/IK (главный блокер качества)

- **Forward-walk seed** вместо turn-in-place: испечь walk/run/strafe, проверить направление и скорость.
- **Плечи:** решить — отдельный источник направления ключицы, additive-слой или осознанно не анимировать.
- **Руки:** проверить `B-upperArm.L`; добиться, чтобы muscle-кривые реально двигали кость.
- **Foot IK:** прижать стопу к земле; порог foot slide ≤ 2 см/кадр, penetration ≤ 2 см.
- **Root motion:** решить in-place vs root motion; для `CharacterController` — delta hips.
- **Body swap:** M→F→M на `NetworkPlayer`; проверить `BodySwapped`.
- **Equipment:** оружие/плащ на месте.
- **Валидация:** `MotionBricksClipProbe` + визуал; 3–5 клипов.

### Шаг 2 — T-MB03: generalize baker

- Убрать хардкод `WalkToJog`; параметризовать primitive id, body type, seed/target.
- Batch: несколько клипов за прогон; metadata; validation report.
- `SaveClipInPlace` для всех клипов; GUID stable.
- Не трогать controller/prefab; playback на probe/scene.
- Добавить `MotionBricksClipProbe` в CI-подобную проверку (Editor menu / headless).

### Шаг 3 — T-MB05: hybrid primitives / NPC pool

- Interaction boxes + target poses + baked clips.
- NPC: baked pool (idle/walk/work/sit) через existing Animator/OverrideController; `allowNpc=false` для runtime.
- Проверить 2 clients + F8/F9.

### Шаг 4 — T-MB06: runtime (optional, not now)

- Только если есть продуктовая необходимость.
- Sentis import 6993 nodes / VRAM / p95; конфликт версий.
- Если нет — закрыть ветку и остаться на baked.

### Шаг 5 — T-MB08: go/no-go

- После 3–5 валидированных клипов, не после одного демо.
- Критерии: baseline PASS, foot IK, body swap, equipment, network/FO, A/B, лицензия.
- Если нет — не мержить; оставить docs/код в feature-ветке.

---

## 6. Acceptance criteria для следующего этапа

- [ ] `check_baseline.ps1` в main — PASS до/после.
- [ ] `MotionBricksRuntimeSettings.mode = Disabled` по умолчанию; probe не в production-сцене.
- [ ] Forward-walk клип: направление/скорость соответствуют seed; нет разворота на 197°.
- [ ] Плечи/руки: визуально двигаются; `B-upperArm.L` меняется в `LateUpdate`.
- [ ] Foot IK: `footRY` ≤ 0.15 при rest 0.128; foot slide ≤ 2 см/кадр.
- [ ] Body swap M→F→M: нет T-pose, equipment на месте.
- [ ] 2 clients + F8/F9: 0 errors, позиции/анимации совпадают.
- [ ] Лицензия: только свои seed/target-позы; sample data не в релизе.

---

## 7. Готовый текст для следующего агента

> T-MB02 spike принят: CPU-бейк SOMA→Humanoid, `PlayableGraph` playback и baseline restore работают; визуал подтверждён. Но это один демо-клип `walk_to_jog`, который разворачивает на месте, с 22 костями, без плеч/кистей/пальцев, без foot IK и с открытым вопросом по рукам. 100% покрытия нет; combat/face/fingers остаются authored. Дальше: (0) выключить `MotionBricksRuntimeSettings.mode`/убрать probe из production-сцены; (1) T-MB04a — forward-walk seed, плечи, руки, foot IK, root motion, body swap; (2) T-MB03 — обобщить baker и batch; (3) T-MB05 — baked pool для NPC/hybrid primitives; (4) T-MB06 runtime — только при продуктовой необходимости. Worktree = код/docs; Unity-ассеты = main (Lore); baseline check в main; в worktree `-SkipPackages -SkipContent`.

---

## 8. Абсолютные ссылки

| Что | Путь |
|---|---|
| T-MB02 retarget findings | `C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\08_T-MB02_RETARGET_FINDINGS.md` |
| Worktree/content gap | `C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\07_T-MB02_WORKTREE_BASELINE_FINDINGS.md` |
| This analysis | `C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\09_T-MB02_ANALYSIS_AND_NEXT.md` |
| Handoff | `C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\HANDOFF.md` |
| Safe rollout | `C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\06_SAFE_ROLLOUT_AND_ROLLBACK.md` |
| Baseline check | `C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |
| Baked playback | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedPlayback.cs` |
| Runtime settings | `C:\Projects\ProjectC_client\Assets\_Project\Settings\MotionBricks\MotionBricksRuntimeSettings.asset` |
| Spike baker | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksSpikeBaker.cs` |
| Clip probe | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksClipProbe.cs` |
| Generated clip | `C:\Projects\ProjectC_client\Assets\_Project\Animations\Generated\MotionBricks\walk_to_jog\Male\WalkToJog_Male_Humanoid.anim` |
| Probe object | `WorldScene_0_0 / WorldRoot_0_0 / MB_Probe_Player` |
| Offline pack | `C:\Projects\motion_bricks\README.md` |

---

*Связанные: [07](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) · [08](08_T-MB02_RETARGET_FINDINGS.md) · [HANDOFF.md](HANDOFF.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md)*


---

## T-MB04a, 15.11.2026 — синтез forward-walk из статичной позы: замеренный отказ и выбор источника

### Что проверено

Инструмент (валидатор) на этот момент откалиброван по авторской ходьбе KL и различает клипы:
эталон `HumanM@Walk01_Forward` — ось шага vs лицо **0.1°**, слайд **0.07 / 0.11 см/кадр**,
скорость опоры **2.00 / 2.00 м/с** (асимметрия 0 %), опорная стопа 0.164 м.
Прежний наш клип `WalkForward_Male` — 83.8°, 2.78 / 1.56 см/кадр, 43 % асимметрии.

### Гипотеза и её проверка

План: контакт-L = библиотечная поза `walk_1_0`; контакт-R = её сагиттальное зеркало;
сгенерировать цикл, перепечь, перемерить.

Зеркальная поза собрана корректно (это проверено, а не предположено):
мировые позиции совпадают с зеркалом исходных с точностью **0.00000 м**, колени
меняются местами (6° / 45° → 45° / 6°), лицо сохранено (0.0°), det(M) = −1.
Зеркало бралось по сагиттальной плоскости **персонажа** (нормаль из направления носков),
а не по мировому X: позы библиотеки стоят под рысканием ≈ 19°.

Затем две генерации CLI (контекст — 4 позы `walk_1_0..3`, цель — зеркало), склейка кадров:

| Опыт | Кадров | Стопа по Y | Рыскание | Смещение опорной стопы |
|---|---|---|---|---|
| бюджет 8 токенов | 26 | L до **1.053 м** (выше таза) | дрейф **56°** | 35.5 см/кадр |
| бюджет модели (14) | 50 | обе до **1.55 м** | дрейф **113°** | — (опоры почти нет) |

Шов между половинами идеален (0.0000 м), замыкание цикла 0.176 м, длина цикла 1.70 s —
то есть труба (генерация → кадры → .f32) работает, не работает **содержание**.

### Причина (замер, не мнение)

`walk_1_0..3` — четыре **последовательных кадра одного мгновения** (различие 1.7–4.3° на кость),
а не фазы шага. Контекст из 4 кадров у модели — это окно 4×33 мс, в нём нет каденции.
Цель «зеркальный контакт» требует переноса ног через корпус, и без движения в контексте
модель выполняет это в воздухе: поднимает обе стопы и разворачивает таз.

Побочный замер: нормализация знаков кватернионов зеркальной позы (7 суставов имели dot < 0,
в т.ч. бёдра −0.943) дала **побитово тот же** выход модели — она канонизирует кватернионы сама.
Знаки не были причиной; версия закрыта.

### Решение (принято 15.11)

Рабочая forward-walk для приёмки T-MB04a — **авторский клип KL** `HumanM@Walk01_Forward`
(0.80 s, 30 fps, 25 кадров): он уже проходит все метрики с запасом и лицензионно «свой» для проекта.
Синтез ходьбы MotionBricks выносится из T-MB04a в отдельную задачу с **контекстом-движением**
(реальная каденция в 4 кадрах), а не статичной позой. Ограничение лицензии при этом сохраняется:
позы комплекта — eval-only, в релизный seed не идут.

### Приёмка в редакторе без ослабления гейта

`mode = Disabled` остаётся (Шаг 0). Для визуального просмотра добавлен редакторный обход:
`MotionBricksBakedPlayback.ReviewOverride` + `TryPlayForReview()` под `#if UNITY_EDITOR`
(в билд не попадает) и меню `ProjectC/MotionBricks/Review/...`:
- `Play KL Walk01 Forward on probe (рабочая ходьба)` — рабочий клип;
- `Play Forward Walk on probe` / `(generic clip)` / `WalkToJog` — наш синтез;
- `Stop on probe` — вернуть baseline Animator.

Клип грузится по пути (в т.ч. из FBX через `path.fbx::ClipName`), ссылка в сцене не нужна.


### Приёмка 15.11, вечер — что закрыто замером и логом

Рабочий клип — авторский KL `HumanM@Walk01_Forward` (0.80 s, 25 кадров), играется через тот же
рантайм-путь (`MotionBricksBakedPlayback` + PlayableGraph, `controller = null`).

Лог сеанса 18:07 (плейтест пользователя):
- `[MB-Review] Играю KL Walk01 Forward ... на 'MB_Probe_Player' → Animator 'HumanM_Model'` — клип взят
  из FBX по спецификации `path.fbx::ClipName`, гейт `mode = Disabled` не менялся;
- `diag: t=0,77/0,80 s, кость B-upperArm.L: было (19.0, 265.3, 73.2), стало (10.3, 241.9, 71.2),
  controller=null, humanMotion=True` — локальный поворот кости изменился на **23.7°** ⇒
  пункт «B-upperArm.L меняется в LateUpdate» закрыт;
- `Playback остановлен, baseline Animator восстановлен` ⇒ возврат baseline в живом сеансе подтверждён;
- ошибок компиляции после 17:41 нет, исключений за сеанс нет.

Замеры валидатора по рабочему клипу (эталонные цифры, пороги не применяются, но все в допуске):

| Метрика | KL Walk01 | Порог |
|---|---|---|
| ось шага vs лицо | **0.1°** | ≤ 20 |
| foot slide L / R | **0.07 / 0.11 см/кадр** | ≤ 2 |
| скорость опоры L / R | **2.00 / 2.00 м/с**, асимметрия 0 % | ≤ 25 % |
| stride за контакт | 0.33 м (контроллер должен давать 2.00 м/с) | — |
| подъём стопы над землёй клипа | 0.036 м | ≤ 0.05 |
| penetration | 0.000 м | ≤ 0.02 |
| дрейф рыскания | 0.0° | ≤ ±15 |
| ключицы / плечо | 13.5° / 76.5° | ≥ 3 / ≥ 10 |

### Порог высоты опоры переопределён (и почему это не подгонка)

Старый порог `опорная стопа ≤ 0.15` мерился **абсолютно от rest-позы** рига (0.128), то есть от позы
T/A, а не от контакта шага. Валидатор при этом брал минимум по обеим стопам. Следствие: **эталонная
авторская ходьба KL давала 0.164 и «падала»**, потому что у неё таз ходит по вертикали 0.927..0.947
и опорная кость законно поднимается над rest.

Теперь метрика — **подъём опорной стопы над землёй самого клипа** (`groundRail` = минимум по всем
кадрам), порог ≤ 0.05 м. Числа: эталон **0.036**, наш клип **0.000**. Абсолютная величина печатается
справочно, чтобы ничего не прятать. Класс ошибки тот же, что был со slide (мера от анатомической
константы вместо контакта самого клипа).

Побочное наблюдение по нашему клипу: подъём 0.000 при размахе таза **0.780..0.938 (15.8 см)** против
2 см у эталона — тазовый foot IK прижимает стопу к полу слишком жёстко и «утягивает» таз вниз.
Это отдельный дефект бейкера (не влияет на приёмку рабочего клипа, но остаётся в T-MB03).

### Baseline

`check_baseline.ps1 -SkipPackages -SkipContent` в main: **BASELINE OK: 10 files match** —
`PlayerAnimation.controller`, оба override-контроллера, `NetworkPlayer.prefab`, `Npc_Goblin.prefab`,
`NpcAnimator_Goblin.overrideController`, `NetworkPlayer.cs`, `CharacterCustomisationApplier.cs`,
`SkillAnimationPlayer.cs`, `NpcBrain.cs`.


---

## T-MB04a, вечер 15.11 — root motion написан, состояние для следующего агента

### `MotionBricksBakedRootMotion.cs` (новый файл, в рантайме ещё не проверялся)

`Assets/_Project/Scripts/Animation/MotionBricks/MotionBricksBakedRootMotion.cs`,
namespace `ProjectC.Animation.MotionBricks`. Реализует §5 документа [06](06_SAFE_ROLLOUT_AND_ROLLBACK.md).

Зачем вообще: клипы пекутся с `BakeInPlace = true`, горизонталь корня выброшена. Значит персонажа везёт
код — и с той скоростью, которую подразумевает клип, иначе стопы скользят. Замеренные скорости:
KL `HumanM@Walk01_Forward` — **2.00 м/с**, наш синтезированный `WalkForward_Male` — **0.54 м/с**.

Два источника (`_source`):

| Режим | Что делает | Для чего |
|---|---|---|
| `ClipSpeed` | едет вперёд по лицу корня с постоянной скоростью, разгон `_acceleration` (по умолчанию 8 м/с²) | in-place клипы (текущие все) |
| `HipsDelta` | мировая дельта кости таза между кадрами, вертикаль выброшена, скачок > `_maxDeltaPerFrame` (0.5 м) игнорируется | будущие клипы с переносом корня (`BakeInPlace = false`) |

Безопасность — по тем же правилам, что playback:

- гейт тот же ассет `MotionBricksRuntimeSettings`; нет ассета или `Mode=Disabled` → `enabled = false`;
- **`_clipSpeed = 0` по умолчанию** → компонент физически ничего не двигает, пока скорость не задана явно;
- двигает только при `MotionBricksBakedPlayback.IsPlaying` (нет клипа — нет движения);
- без `CharacterController` не двигает transform, если не включён `_allowTransformFallback`
  (сетевой персонаж двигается `CharacterController` + `NetworkTransform`, подмена недопустима);
- не кэширует мировые позиции дольше одного кадра → rebase Floating Origin не превращается в рывок;
  скачок таза больше порога прямо логируется и отбрасывается;
- в префабы и сцены **не подключён намеренно**: проба по-прежнему несёт только playback, поведение
  текущего визуала не изменилось.

Чтобы это проверить, нужно (делает пользователь в Play Mode): добавить компонент на `MB_Probe_Player`,
задать `_settings` = тот же ассет, `_clipSpeed = 2.00`, играть KL Walk01 через Review-меню — кукла должна
ехать вперёд без skating. Сейчас проба стоит на месте, потому что клип in-place, а скорость не задана.

### Пункты приёмки T-MB04a — где стоим

| Пункт | Статус | Чем подтверждён |
|---|---|---|
| baseline PASS до/после | **ЗАКРЫТ** | `check_baseline.ps1 -SkipPackages -SkipContent`: `BASELINE OK: 10 files match` |
| mode=Disabled / проба не в production | **ЗАКРЫТ по допустимой ветке** | `mode = Disabled`; `MB_Probe_Player` остаётся в `WorldScene_0_0` — это разрешённая ветка брифа |
| forward-walk направление и скорость | **ЗАКРЫТ** (на KL) | ось vs лицо **0.1°**, опора **2.00 / 2.00 м/с**, асимметрия **0 %**, stride 0.33 м |
| плечи/руки двигаются, `B-upperArm.L` в LateUpdate | **ЗАКРЫТ** | лог 18:07:17: кость ушла из `(19.0, 265.3, 73.2)` в `(10.3, 241.9, 71.2)` = **23.7°**, `controller = null` |
| `footRY ≤ 0.15`, slide ≤ 2 см/кадр | **ЗАКРЫТ** после переопределения метрики | подъём над землёй клипа **0.036** (порог ≤ 0.05), slide L/R **0.07 / 0.11** |
| body swap M→F→M без T-pose, equipment | **НЕ ПРОВЕРЕН, есть блокер** | см. ниже |
| 2 клиента + F8/F9 без errors | **НЕ ПРОВЕРЕН** | нужен второй клиент; правило «тесты делает пользователь» |
| root motion | **КОД НАПИСАН, рантайм не проверен** | в сцену/префаб не подключён |
| только свои/авторские позы | **ЗАКРЫТ** | рабочая ходьба — авторский клип KL (локальный пакет), sample poses использованы только для eval |

> **Историческая таблица T-MB04a.** Пункты «body swap», «2 клиента + F8/F9» и «root motion» закрыты
> позже, в T-MB04b: root motion — 2.00 м/с и `Stop`→baseline; body swap — на живом игроке и при активном
> playback; 2 клиента и F8/F9 от нуля вынесены в чек-лист T-MB08. Актуальный статус — [10 §5](10_T-MB04B_RUNTIME_HARNESS.md).

**Блокер по body swap.** `CharacterCustomisationApplier` (и событие `BodySwapped`) есть только на настоящем
персонаже, а `MotionBricksBakedPlayback` — только на пробе. Значит сценарий «свап при активном playback»
сейчас проверить нечем: чтобы это сделать, нужно либо вешать playback на `NetworkPlayer.prefab`
(**запрещено**), либо вешать applier на пробу (тянет материалы и сокеты — отдельная работа). Пока
обработчик `OnBodySwapped` в playback подтверждён только чтением кода. Решение о способе проверки —
за следующим этапом, это не техническая мелочь, а выбор объёма работ.

### Порядок для следующего агента

1. **Root motion в рантайме** — пользователь добавляет компонент на пробу, ставит 2.00 м/с, играет KL;
   критерий: кукла едет, стопы не скользят, на `Stop` — baseline.
2. **Body swap** — сначала решить способ проверки (см. блокер), потом проверять.
3. **2 клиента + F8/F9** — только пользователь.
4. **Бейкер, дефект T-MB03:** тазовый foot IK пережимает стопу (подъём 0.000 при размахе таза
   0.780..0.938 = 15.8 см против 2 см у эталона) — таз утягивается вниз.
5. **Синтез собственной ходьбы** отложен: нужен motion-consistent контекст (4 кадра реальной каденции),
   библиотечные позы — четыре соседних кадра одного мгновения.

---

## Ревью T-MB04a и статус ветки (анализ, не код)

### Что сделано хорошо

- `MotionBricksBakedRootMotion` изолирован: `_clipSpeed = 0` по умолчанию, гейт тот же, без `CharacterController` transform не двигает, в префабы/сцены не подключён.
- `ClipSpeed` для in-place клипов и `HipsDelta` для будущих root-motion клипов — правильное разделение.
- KL `HumanM@Walk01_Forward` как рабочая ходьба: 2.00 м/с, foot slide 0.07/0.11 см/кадр, подъём 0.036 м, руки 23.7° — это уже похоже на валидируемый клип, а не демо-разворот.
- Review-меню обходит гейт только в Editor (`ReviewOverride`), не меняя `mode=Disabled`; это безопасно.
- Baseline 10/10; пакеты не менялись.
- Код и docs импортированы в main коммитом `934ecdf6`; `MotionBricksRuntimeSettings.asset` в main — `mode: 0` (Disabled), probe остаётся в `WorldScene_0_0` как dev-объект.

### Риски, которые ещё не закрыты

1. **Root motion не проверен в рантайме.** Компонент ни к чему не подключён; проба стоит. Это не баг, но и не доказательство.
2. **Двойное движение, если просто повесить компонент на `NetworkPlayer`.** `NetworkPlayer` уже вызывает `CharacterController.Move`; `MotionBricksBakedRootMotion` вызывает его ещё раз. Для игрока root motion нужно **встраивать в существующий movement**, а не добавлять отдельный компонент. Для пробы — можно.
3. **Body swap — блокер, а не задача.** `MotionBricksBakedPlayback` подписан на `BodySwapped`, но `MotionBricksBakedRootMotion` кэширует `_hips` и не перепривязывается. После M↔F старый hips/Animator может остаться. Нужен test harness (вариант prefab, не baseline) или applier на пробе.
4. **`ClipSpeed` едет по `transform.forward`.** Для камеро-относительного движения/поворота это может не совпадать с направлением клипа. Нужно кормить фактическую скорость/heading или использовать `HipsDelta`.
5. **`HipsDelta` читается в `Update`.** Animator обновляет позу в `Update`/humanoid pass; возможен лаг на кадр/джиттер. Проверять в `LateUpdate`/после Animator.
6. **2 клиента + F8/F9** не проверены; `_maxDeltaPerFrame` защищает от rebase, но это не тест.
7. **Пельвис-foot IK** пережимает стопу (размах таза 15.8 см против 2 см) — T-MB03.
8. **На пробе висит `WalkToJog_Male_Humanoid`.** Без Review-меню кукла играет демо-разворот; следующий агент должен использовать `ProjectC/MotionBricks/Review/Play KL Walk01 Forward on probe`.
9. **`tmp_tmb04_*.bat`** в корне main — временные файлы; удалить после проверки (не коммитить).

### Ветка / main

- **Main теперь основной:** `934ecdf6` содержит MotionBricks code/docs; `MotionBricksRuntimeSettings.asset` в main — `mode: 0`; baseline 10/10.
- **`feature/motionbricks` — backup/история**, не мержить слепо: в ветке `MotionBricksRuntimeSettings.asset` имеет `mode: 1`, а docs/код уже импортированы в main. Если ветка понадобится — только точечно, с сохранением main `mode: 0`.
- Worktree больше не нужен для кода; Unity-контент — в main (Lore), baseline check — в main. Worktree можно оставить как backup или удалить после подтверждения.

### Следующий порядок

1. **Пользователь:** root motion на пробе — добавить `MotionBricksBakedRootMotion`, `_settings` = main asset, `_clipSpeed = 2.00`, Review KL Walk01; критерий: кукла едет, стопы не скользят, `Stop` → baseline.
2. **Решение по body swap:** variant prefab `NetworkPlayer_MB_Test` (не baseline) с playback+root motion+applier, либо applier на пробе. Без этого T-MB04a не закрыт.
3. **Интеграция root motion в `NetworkPlayer`** — только после теста на пробе; не добавлять второй `CharacterController.Move`.
4. **T-MB03:** убрать хардкод `WalkToJog`, batch/validation, исправить pelvis foot IK (15.8 см).
5. **2 клиента + F8/F9** — пользователь.
6. **T-MB08 go/no-go** — после 3–5 клипов, не после одного KL.

### Абсолютные ссылки (main)

| Что | Путь |
|---|---|
| Root motion | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedRootMotion.cs` |
| Playback | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedPlayback.cs` |
| Settings (mode=0) | `C:\Projects\ProjectC_client\Assets\_Project\Settings\MotionBricks\MotionBricksRuntimeSettings.asset` |
| Review menu | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksReviewMenu.cs` |
| Validator | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksClipValidator.cs` |
| Spike baker | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksSpikeBaker.cs` |
| T-MB04a section | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\09_T-MB02_ANALYSIS_AND_NEXT.md` |
| Handoff | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\HANDOFF.md` |
| Baseline | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |
| Offline pack | `C:\Projects\motion_bricks\README.md` |

---

## T-MB04b, вечер 15.11 — стенд приёмки собран

Подробности и порядок приёмки — [10_T-MB04B_RUNTIME_HARNESS.md](10_T-MB04B_RUNTIME_HARNESS.md).
Коротко, что изменилось против состояния выше:

1. **Root motion подключён к probe**: `_clipSpeed = 2.00`, `_acceleration = 0`, `_allowTransformFallback = true`.
   Направление проверено замером: «носок−стопа» = `(0.028, 0, 1.000)`, `dot` с `transform.forward` = 1.000.
2. **У Animator пробы выключен `applyRootMotion`** (был включён) и поставлен `AlwaysAnimate`: иначе Animator
   сам применял root-кривые клипа и складывал движение с нашим; на `NetworkPlayer` в baseline `applyRootMotion = 0`.
3. **Body swap проверяется на живом игроке**, а не на пробе: playback добавляется `AddComponent` в сессии
   Play Mode, свап идёт через настоящий `CharacterCustomisationApplier`. Префаб и baseline-скрипты не менялись.
4. **Второй `CharacterController.Move` запрещён кодом**: на объекте/в родителях есть `NetworkPlayer` → компонент
   не двигает ничего и пишет предупреждение. Интеграция в `NetworkPlayer.ProcessMovement` — следующий шаг.
5. **Baseline 12/12 до и после**; `mode: 0` не менялся.

Открыто: body swap M→F→M, цифровой замер поездки, `Stop on probe` в сессии, 2 клиента + F8/F9,
интеграция root motion в игрока, T-MB03 (baker/IK).
Принято 15.11 (Play Mode): root motion на probe — **2.00 м/с** (`30,42 м / 15,21 s`), стопы не шаркают,
`Stop on probe` восстанавливает baseline; body swap Male→Female→Male — штатный путь 1/1; body swap при
активном playback — playback и `B-hips` перепривязываются (кость 18.8°/29.6°). Логи — [10 §5.1–5.3](10_T-MB04B_RUNTIME_HARNESS.md).
**T-MB04b закрыт.** Вне тикета (чек-лист T-MB08): 2 клиента, F8/F9 от нуля, отдельная проверка экипировки.
**Важно:** рантайм-игрок — `NetworkPlayer_GlobalPilot.prefab`, baseline защищает `NetworkPlayer.prefab`;
новое тело после свапа несёт FBX-дефолты Animator (`applyRootMotion=true`) — гард на T-MB03/05.
