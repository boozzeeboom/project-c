# HANDOFF — MotionBricks integration

> **Для:** следующего агента/разработчика, который делает интеграцию.
> **Дата:** 2026-11-16
> **Статус:** research + offline pack + safe rollout + **T-MB02 spike** + **T-MB04b accepted** + **T-MB03 done** + **T-MB04c code + runtime (visual pending)** + **T-MB05 code + замеры (visual pending)** + **official slice visual accepted**; **offline locomotion transition pack отклонён**. **T-MB06:** официальный runtime sidecar + owner-player harness реализованы; standalone TensorRT и UDP PASS, Unity 0 errors, **визуальная приёмка pending**.
> **Продуктовая рамка:** host-client co-op до 4 игроков. Прежние MMO-аргументы против runtime считать исторической гипотезой; feasibility runtime определяется замерами.
> **Главное правило:** текущий Animator — дефолт; MotionBricks — additive, off by default, с откатом в минуты.
> **Main:** код и docs импортированы в `main` коммитом `934ecdf6`; `MotionBricksRuntimeSettings.asset` в main — `mode: 0` (Disabled). `feature/motionbricks` — backup/история, не мержить слепо (в ветке `mode: 1`).
> **T-MB02:** `bake → import → playback` работает; качество/IK/покрытие — T-MB03/04. См. [07](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) · [08](08_T-MB02_RETARGET_FINDINGS.md) · [09](09_T-MB02_ANALYSIS_AND_NEXT.md).
> **T-MB04a (историческая):** рабочая forward-walk — авторский клип KL; метрики/руки/baseline подтверждены. Runtime-тест root motion и body swap закрыты в T-MB04b; см. [10](10_T-MB04B_RUNTIME_HARNESS.md).
> **T-MB04b (вечер 15.11): ✅ принят.** Root motion на probe: **2.00 м/с** (`30,42 м / 15,21 s`), стопы не шаркают, `Stop`→baseline. Body swap Male→Female→Male на живом игроке — штатный путь 1/1; body swap при **активном playback** проверен на probe с настоящим applier — playback и root motion перепривязываются, кость двигается (18.8°/29.6°). Осталось вне тикета (в чек-лист T-MB08): 2 клиента, F8/F9 от нуля. Второй `CharacterController.Move` запрещён кодом; интеграция в `NetworkPlayer` — отдельный шаг.
> **T-MB03 (ночь 15.11): ✅ сделано** — бейкер собирает задания с диска, batch + валидация + `*.bake.report.json`; тазовый foot IK стал контактно-осознанным (размах таза 15.7 → 11.7 см при hover 4 / penetration 1 см); `applyRootMotion` guard с учётом `SkillAnimationPlayer`; GUID/ссылки при перепёке сохранены. Подробности и замеры — [11](11_T-MB03_BAKER_AND_GUARDS.md). Baseline 12/12, Play Mode не запускался.
> **T-MB04c (ночь 15.11): код + runtime-замер** — новый `MotionBricksLocomotionBridge`: `MotionBricksBakedRootMotion` на `NetworkPlayer` не вызывает `Move`, а публикует displacement; `NetworkPlayer.ProcessMovement` забирает его перед **единственным** `_controller.Move`. Baseline-файл `NetworkPlayer.cs` изменён с одобрения; хэш обновлён отдельным коммитом. Один контролируемый прогон Play Mode (по разрешению пользователя): поездка **33.13 м / 16.56 с = 2.00 м/с**, `Stop` → стоп, body swap Female→Male с перепривязкой playback/скелета, 0 errors. Визуал/экипировка — за пользователем: [12](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md) §6.1.
> **T-MB05 (16.11): код + offline-замеры + probe runtime; visual pending** — аналитический **2-bone IK ног** в бейкере: таз **15.7 → 1.7 см** (`WalkForward`) и **5.1 → 4.6 см** (`WalkToJog`, one-shot), humanoid hover/penetration **0.09/0.00** и **0.30/0.20** (в допуске). `loop:false` в sidecar + `loopTime/WrapMode`; non-cyclic решатель не тянет шов. Найдены и исправлены: **humanoid T-поза** (`HumanPoseHandler`), **stale parent в leg IK** (172° переворот бёдер), **last-frame wrap** (последний кадр = первый), **Arm Twist ±180/360** (178.8°), **RootQ guard** (корпус не поворачивался → «выворот»; на probe включён `applyRootMotion`), **ключица** (локальный delta 11/39). Baseline 12/12: [13](13_T-MB05_LEG_IK_AND_LOOP.md).
> **Official vertical slice (16.11): ✅ численный PASS + visual accepted; Play Mode pending** — собственный KL `Walk01_Forward [RM]` → inverse SOMA (roundtrip ≤2.4×10⁻⁶°) → официальный TensorRT и `sa-inbetween` на одном moving context. 18 кадров, **2.142 м/с**, yaw drift ~0; backend difference ≤13.3 мм / 2.6°. Unity generic clip сохранил **1.214 м / 0.567 с = 2.142 м/с**, humanoid без T-pose. **Пользователь принял `OfficialWalk_Male_Humanoid.anim` в Animation Window**; это не Play Mode/equipment acceptance. Contact-validator FAIL — ожидаемый артефакт `groundFeet:false`+root motion, не обратное движение: [14](14_OFFICIAL_VERTICAL_SLICE.md).
> **Locomotion transition pack v1 (16.11): ❌ отклонён** — 22 перехода, 0/22 validator PASS, 9/22 slide≤2. Пользователь: не лучше исходного Animator, требует сопоставимого объёма ручной отладки. В Animator не внедрялся. Generated-артефакты и debug-сцена удалены; причина и следующий этап — [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md).
> **T-MB06 (16.11): runtime-код + standalone PASS; повторный visual pending** — настоящий `NetworkPlayer`/`CharacterController` отправляет движение в официальный Python/TensorRT `SOMACharacter`; UDP latest-frame → SOMA retarget поверх Animator в `LateUpdate`. Ctrl+F10 — neural/legacy A/B; owner loss/cast/stale/error/body swap → authored Animator. Attach берёт rest template из фактической модели customisation (`testing`/female), HumanM/HumanF — fallback; `B-jaw` опционален. Исправлены по приёмке пользователя: улёт по вертикали (root Z относительный; guard >5 м), постоянный поворот на −90° (+90° source→target yaw; численно `−X`→`+Z`), выворачивание рук (twist ±90°), прыжок (airborne → authored Animator, auto-resume после landing). Self-test 480 кадров/14 генераций, тёплый wall inference p50/p95 **5.107/32.302 ms**; Unity 0 errors, baseline 12/12. **Повторный живой визуал, equipment/body swap, FPS/VRAM и host-client ещё не проверены.** Инструкция и метрики — [17](17_T-MB06_RUNTIME_SIDECAR.md).
> **Найдено:** фактический рантайм-игрок — `NetworkPlayer_GlobalPilot.prefab` (подставляет `GlobalMotionNetworkStartup` из профиля), baseline защищает `NetworkPlayer.prefab`. Новое тело после свапа приходит с FBX-дефолтами Animator (`applyRootMotion=true`, `CullUpdateTransforms`) — для будущих root-motion клипов нужен гард (T-MB03/05).

---

## 1. Что уже решено

1. **MotionBricks не тащим в runtime MMO-клиента.** 758 MB FP32 / 380 MB FP16, 200M params, NVIDIA/TensorRT или тяжёлый ONNX, недетерминизм, нет пальцев/мимики, combat timing ломается.
2. **Основной путь — offline baker:** генерируем короткие клипы (0.6–1.9 с), запекаем в `.anim`, играем через существующий Animator/OverrideController/PlayableGraph.
3. **Runtime neural** — только experimental, owner player, Windows+NVIDIA, feature flag, fallback; не для NPC/толпы/combat/remote.
4. **Никаких изменений** `Packages/manifest.json`, `PlayerAnimation.controller`, `NetworkPlayer.prefab`, `NpcBrain`, `Combat`, `SkillAnimationPlayer` на MVP.
5. **Откат:** `Mode=Disabled` → удалить playback → удалить generated clips → `git revert` → выбросить worktree.

---

## 2. Что уже проверено

| Проверка | Результат |
|---|---|
| Официальный TensorRT `generate_once.py` | ✅ 26 frames (0.87 s) + 50 frames (1.67 s), RTX 3090, driver 610.88, CUDA 12.9 |
| CPU ONNX Runtime `smoke_test.py` | ✅ `[1,64,366]`, tokens 16, 55–71 ms N=1 (i7-8700) |
| CPU CLI `sa-inbetween` | ✅ `generate` → 50 frames; `inspect` OK (`demo_walk_to_jog.saib`) |
| T-MB02 bake → import → playback | ✅ `WalkToJog_Male_Humanoid`, `PlayableGraph`, controller=null; baseline restore |
| T-MB02 Play Mode acceptance | ✅ пользователь: поза корректная, конечности не вывернуты; это разворот на месте (свойство seed) |
| T-MB04a KL forward walk | ✅ 2.00 м/с, foot slide 0.07/0.11 см/кадр, подъём 0.036 м, руки 23.7°, baseline restore |
| T-MB04a root motion | ✅ **2.00 м/с** в рантайме (`30,42 м / 15,21 s`), стопы не шаркают, `Stop`→baseline (§5.1–5.3 [10](10_T-MB04B_RUNTIME_HARNESS.md)) |
| T-MB04a body swap | ✅ Male→Female→Male на живом игроке (1/1, штатный путь); при активном playback — проверено на probe с настоящим applier: перепривязка playback и `B-hips`, кость 18.8°/29.6° (§5.2–5.3) |
| T-MB03 baker/IK/guard | ✅ discovery с диска, batch+validate, `*.bake.report.json`; foot IK hover 4 / penetration 1 см при размахе таза 11.7 см; guard 6/6; GUID стабилен — [11](11_T-MB03_BAKER_AND_GUARDS.md) |
| T-MB05 leg IK/loop | ✅ 2-bone IK: таз 15.7 → **1.7 см**, humanoid hover/pen **0.09/0.00**; `WalkToJog` 5.1 → **4.6 см**, **0.30/0.20**; `loop:false` + non-cyclic; T-поза/stale-parent/last-frame/twist/RootQ/ключица fixes; baseline 12/12 — [13](13_T-MB05_LEG_IK_AND_LOOP.md). Probe runtime: рука идёт с корпусом |
| Baseline animation files | ✅ `check_baseline.ps1` - 12/12 match (main); worktree `-SkipContent` - 4 scripts |
| Sentis import | ❌ не проверялся; конфликт версий Burst/Collections/Newtonsoft |
| Unity Play Mode | ✅ T-MB04b: пользователь + один прогон агента по разрешению; агент сам Play Mode не запускает |

---

## 2.1. Коммиты

| Коммит | Что |
|---|---|
| `c6a5f57f` | `T-MB05`: restore humanoid RootQ on probe, local clavicle retarget |
| `7ac26287` | `T-MB05`: leg IK parent frame, humanoid last-frame wrap, twist flips, one-shot loop |
| `87102426` | `T-MB05`: 2-bone leg IK, non-cyclic pelvis/`loop:false`, humanoid T-поза fix, metrics/docs |
| `5425bc62` | `T-MB04c`: update the reviewed `NetworkPlayer.cs` baseline hash (12/12) |
| `e33cc216` | `T-MB04c`: baked root motion through the locomotion bridge, runtime measurement, docs |
| `90180758` | `T-MB03`: discovery baker, batch/validation/metadata, contact-aware pelvis foot IK, applyRootMotion guard, guard/IK self-tests |
| `d7235bfe` | `T-MB04b`: ticket closed — travel 2.00 m/s, body swap with active playback, findings |
| `1441e51b` | `T-MB04b`: Stop/body-swap/F8 session log + runtime player prefab finding |
| `2c35a903` | `T-MB04b`: play-mode acceptance of probe root motion (logs + status) |
| `eb4c87e8` | `T-MB04b`: bring-probe convenience and single-play ordering in the review menu |
| `a1b3f859` | `T-MB04b`: handoff index row for the runtime harness commit |
| `ddbc51f0` | `T-MB04b`: probe root motion, body-swap harness, no-duplicate-Move guard |
| `934ecdf6` | `T-MB02/T-MB04a`: import code and docs into main (settings stay Disabled) |
| `cd4133d9` | `T-MB04a`: handoff index row for the working-clip commit |
| `1d3ff3e6` | `T-MB02`: analysis, coverage limits and next-step plan |
| `0ed2e66c` | `T-MB04a`: KL walk as the working clip, stance metric off the clip ground rail, root motion |
| `10431e23` | `T-MB02`: close out - acceptance results, DoD checklist, baseline re-run |
| `b58eeaec` | `T-MB02`: handoff index rows for the playback diagnostics fixes |
| `2431aa0b` | `T-MB02`: handoff index row for the in-place clip save fix |
| `0f3ad6da` | `T-MB02`: play the muscle clip, diag moved to LateUpdate (the Update-time reading lied) |
| `0df7403e` | `T-MB02`: probe switched to the generic clip (reverted by 0f3ad6da) |
| `389ca9d8` | `T-MB02`: save baked clips in place so scene references survive a rebake |
| `68863e78` | `T-MB02`: full world-delta retarget, muscle-range guard, rebake + report fix |
| `5c24d321` | `T-MB02`: loop for baked clip + evidence the humanoid clip drives the skeleton |
| `2b840489` | `T-MB02`: Humanoid muscle bake + playback diagnostics |
| `54f80173` | `T-MB02`: record retarget findings and clip measurements |
| `7e0b5082` | `T-MB02`: baked-clip playback, runtime gate, spike baker and clip probe |
| `342b95a9` | `T-MB02`: audit worktree baseline, record content-asset VCS gap |
| `c6806dd9` | `T-MB01B`: safe rollout, baseline guard, handoff |
| `3c2bb7d4` | `T-MB01A`: offline verification results |
| `711c70e3` | `T-MB01A`: sa-inbetween CLI/poses + Sentis deps |
| `036e79bc` | `T-MB01A`: self-contained official venv (python312 + uv) |
| `cf4f8b75` | `T-MB01A`: offline resource pack + local links |
| `c3ea92aa` | `T-MB01`: research + decision |

---

## 3. С чего начать (первые 30 минут)

1. Прочитать:
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\README.md`
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\06_SAFE_ROLLOUT_AND_ROLLBACK.md`
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\02_PIPELINE.md` (§3–§7)
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\05_LOCAL_RESOURCES.md`
2. Работаем в **main**:
   ```powershell
   cd C:\Projects\ProjectC_client
   ```
   `feature/motionbricks` — backup/история; не мержить слепо (в ветке `MotionBricksRuntimeSettings.asset` `mode: 1`, в main — `mode: 0`).
3. Запустить baseline check в main:
   ```powershell
   powershell -ExecutionPolicy Bypass -File `
     C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1
   ```
   Ожидаемо `BASELINE OK: 12 files match`; пакеты проверить `git diff -- Packages`.
4. Убедиться, что локальные ресурсы на месте:
   ```powershell
   Test-Path C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe
   Test-Path C:\Projects\motion_bricks\tools\cpu_baker\.venv\Scripts\python.exe
   ```
5. T-MB04c/T-MB05 закрыты по коду/замерам; official slice visual accepted; offline transition pack v1 отклонён. **T-MB06 runtime-код готов**, standalone TensorRT/UDP и Unity compile проверены. Дальше — ручная визуальная приёмка и замеры по [17](17_T-MB06_RUNTIME_SIDECAR.md), затем host-client до 4 игроков; только после этого решать про gameplay/shipping. `T-MB08` с 2 client/F8-F9/equipment/каст — после runtime-этапа. Очистка старых generated/MB debug-файлов выполнена, правила — [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md).

**Main vs worktree:** main теперь содержит код/docs и Lore-контент; worktree не содержит контента и оставлен только как backup. Не открывать два Unity на одном проекте; не копировать контент в worktree.

---

## 4. Тикеты и порядок

| Тикет | Статус | Что делать |
|---|---|---|
| `T-MB01` | ✅ done | Research/decision |
| `T-MB01A` | ✅ done | Offline resource pack + local links |
| `T-MB01B` | ✅ done | Safe rollout/rollback + baseline manifest + handoff |
| `T-MB02` | ✅ done | Испечь **один** клип, импортировать, проиграть на scene-объекте (приёмка 15.11 - `HANDOFF §9`) |
| `T-MB04a` | ✅ historical | KL walk/метрики/руки/root motion code; закрыт T-MB04b |
| `T-MB04b` | ✅ accepted | root motion **2.00 м/с**, `Stop`→baseline, body swap (в т.ч. при активном playback) — приняты. Вне тикета → T-MB08: 2 клиента, F8/F9 от нуля, экипировка отдельным глазом |
| `T-MB04c` | ✅ code + runtime; visual pending | Root motion встроен в движение игрока: `MotionBricksLocomotionBridge` + публикация из `MotionBricksBakedRootMotion`; `ProcessMovement` забирает displacement перед единственным `Move`. Замер 2.00 м/с, Stop→стоп, body swap с перепривязкой; визуал — [12 §6](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md). |
| `T-MB03` | ✅ done | Baker MVP: discovery/batch/validation/metadata; контактный pelvis foot IK; guard `applyRootMotion`; `SaveClipInPlace` с проверкой GUID — [11](11_T-MB03_BAKER_AND_GUARDS.md) |
| `T-MB05` | ✅ code + metrics; visual pending | 2-bone IK ног + `loop:false`: таз 15.7 → 1.7 см / 5.1 → 4.6 см, humanoid contact 0.09/0.00 и 0.30/0.20; T-поза/stale-parent/last-frame/twist/RootQ/ключица fixes — [13](13_T-MB05_LEG_IK_AND_LOOP.md). NPC pool/culling — отдельно |
| Official slice (вне тикета) | ✅ numeric + Animation Window accepted; Play Mode pending | Собственный moving context → TensorRT/CPU A/B → generic/humanoid без IK: **2.142 м/с**, 1.214 м, без T-pose; пользователь принял Humanoid-клип в Animation Window — [14](14_OFFICIAL_VERTICAL_SLICE.md) |
| Locomotion pack v1 (вне тикета) | ❌ rejected | Не лучше исходного Animator; 0/22 validator, 9/22 slide≤2; generated-артефакты удалены — [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md) |
| `T-MB06` | 🟡 code + standalone/UDP PASS; visual pending | Official SOMA runtime через Python/UDP на owner-персонаже, Ctrl+F10 A/B/fallback; Unity 0 errors, baseline 12/12. Дальше живой визуал, FPS/VRAM, host-client 1/2/4 — [17](17_T-MB06_RUNTIME_SIDECAR.md) |
| `T-MB07` | ⬜ optional | Runtime gameplay/cosmetic integration после измерений |
| `T-MB08` | ⬜ | Go/no-go review: сначала preview + 3–5 moving-context клипов, затем 2 клиента/F8-F9/equipment/каст |
| `T-MB09` | ⬜ | Docs/rollout |

**Правило:** не создавать тикеты в трекере без одобрения; это план.

---

## 5. Абсолютные ссылки

### 5.1. Project C docs

| Документ | Абсолютный путь |
|---|---|
| Research README | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\README.md` |
| Technical research | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\01_RESEARCH.md` |
| Pipeline | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\02_PIPELINE.md` |
| Alternatives | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\03_ALTERNATIVES.md` |
| Sources | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\04_SOURCES.md` |
| Offline resources | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\05_LOCAL_RESOURCES.md` |
| Safe rollout/rollback | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\06_SAFE_ROLLOUT_AND_ROLLBACK.md` |
| T-MB05 leg IK/loop | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\13_T-MB05_LEG_IK_AND_LOOP.md` |
| Baseline manifest | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\BASELINE_MANIFEST.md` |
| Baseline hashes | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\baseline_hashes.tsv` |
| Baseline check | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |

### 5.2. Project C code/assets

| Что | Абсолютный путь |
|---|---|
| Player prefab | `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\NetworkPlayer.prefab` |
| Player controller | `C:\Projects\ProjectC_client\Assets\_Project\Animations\PlayerAnimation.controller` |
| Male override | `C:\Projects\ProjectC_client\Assets\_Project\Animations\PlayerAnimation_Default.overrideController` |
| Female override | `C:\Projects\ProjectC_client\Assets\_Project\Animations\PlayerAnimation_Female.overrideController` |
| Player script | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\NetworkPlayer.cs` |
| Body swap | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\CharacterCustomisationApplier.cs` |
| Skill animation | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Skills\SkillAnimationPlayer.cs` |
| NPC brain | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\AI\NpcBrain.cs` |
| NPC prefab | `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\AI\Npc_Goblin.prefab` |
| NPC controller | `C:\Projects\ProjectC_client\Assets\_Project\Animation\AI\NpcAnimator_Goblin.overrideController` |
| Packages | `C:\Projects\ProjectC_client\Packages\manifest.json` |

### 5.3. MotionBricks offline pack

| Что | Абсолютный путь |
|---|---|
| Root README | `C:\Projects\motion_bricks\README.md` |
| Official repo | `C:\Projects\motion_bricks\motionbricks` |
| Runtime API | `C:\Projects\motion_bricks\motionbricks\docs\runtime_api.md` |
| ONNX inference | `C:\Projects\motion_bricks\motionbricks\docs\onnx_inference.md` |
| SOMA model card | `C:\Projects\motion_bricks\motionbricks\docs\soma_model.md` |
| SOMA ONNX FP32 | `C:\Projects\motion_bricks\motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp32.onnx` |
| HF SafeTensors | `C:\Projects\motion_bricks\models\huggingface\MotionBricks` |
| Community bridge | `C:\Projects\motion_bricks\community\motionbricks-unity-bridge` |
| CPU CLI `sa-inbetween` | `C:\Projects\motion_bricks\community\sa-inbetween\releases\v0.9.4\tools-win-x64\SaInbetween` |
| CPU CLI test clip | `C:\Projects\motion_bricks\community\sa-inbetween\releases\v0.9.4\library\demo_walk_to_jog.saib` |
| CPU venv | `C:\Projects\motion_bricks\tools\cpu_baker\.venv` |
| TensorRT venv | `C:\Projects\motion_bricks\motionbricks\.venv` |
| Unity Sentis tarball | `C:\Projects\motion_bricks\unity\packages\com.unity.ai.inference-2.6.1.tgz` |
| Checksums | `C:\Projects\motion_bricks\checksums\SHA256SUMS.txt` |
| Paper | `C:\Projects\motion_bricks\papers\motionbricks_siggraph_2026.pdf` |
| License | `C:\Projects\motion_bricks\papers\nvidia-open-model-agreement-2026-04-02.pdf` |

---

## 6. Guardrails (HARD)

- ❌ Не менять `Packages/manifest.json` в main.
- ❌ Не менять `PlayerAnimation.controller`, `NetworkPlayer.prefab`, `NpcBrain`, `SkillAnimationPlayer`, `Combat` на MVP.
- ❌ Не тащить TensorRT/CUDA/758 MB модель в shipping build.
- ❌ Не использовать neural-позы для хитбоксов/урона.
- ❌ Не генерировать combat-клипы без пересчёта Animation Events.
- ❌ Не хранить target-позы в абсолютных мировых координатах.
- ❌ Не писать `.meta`/`.asmdef` руками.
- ❌ Не запускать Play Mode/тесты/скриншоты агентом — это делает пользователь.
- ❌ Не делать `git reset --hard`/`git checkout -- .` в main: там есть пользовательские изменения.
- ❌ Не мержить в main до go/no-go и подтверждения пользователя.

---

## 7. Fallback / rollback (кратко)

| Уровень | Действие | Время |
|---|---|---|
| 0 | `MotionBricksRuntimeSettings.mode = Disabled` | секунды |
| 1 | Убрать `MotionBricksBakedPlayback`/`MotionBricksRuntime` со scene/prefab | минуты |
| 2 | Удалить `Assets/_Project/Animations/Generated/MotionBricks/` | минуты |
| 3 | `git revert` коммитов в `feature/motionbricks` | <1 ч |
| 4 | Удалить worktree/ветку; main не тронут | минуты |

**Проверка:** `baseline/check_baseline.ps1` до/после каждого этапа; `CHANGED` → остановиться и разобраться.

---

## 8. Открытые вопросы / блокеры

1. **Сид-клип `walk_to_jog` — разворот на месте.** Для рабочей ходьбы используется авторский KL `HumanM@Walk01_Forward`; синтезированный `WalkForward_Male` (0.54 м/с) пока не motion-consistent.
2. **Плечи не анимируются.** `B-shoulder.L/R` наследуют грудь; `11→12`/`39→40` degenerate swing.
3. **Руки — закрыто для KL.** `B-upperArm.L` двигается на 23.7° в LateUpdate при `controller = null`; для синтезированных клипов проверить отдельно.
4. **Foot IK / pelvis.** Для KL метрики в допуске (slide 0.07/0.11 см/кадр, подъём 0.036 м). T-MB03: тазовый IK стал контактно-осознанным (`PelvisSmooth`): на `WalkForward_Male` размах таза 15.7 → 11.7 см при hover 4 / penetration 1 см. **T-MB05:** 2-bone IK ног снял зависимость таза от стопы — `WalkForward` 15.7 → **1.7 см**, `WalkToJog` 5.1 → **4.6 см** (one-shot); humanoid contact **0.09/0.00** и **0.30/0.20** (в допуске). Остаток: старт `WalkToJog` в повёрнутой позе (snap, контент), foot slide 2.88 у `WalkForward`; turn-клип на живом игроке требует RootQ/RootT-разделения — см. [13](13_T-MB05_LEG_IK_AND_LOOP.md).
5. **`MotionBricksRuntimeSettings.mode`** в main — `0` (Disabled) ✓; probe `MB_Probe_Player` остаётся в `WorldScene_0_0` как dev-объект (разрешённая ветка).
6. **Body swap M↔F — стенд готов.** Playback добавляется в рантайме на живого игрока; свап идёт через настоящий `CharacterCustomisationApplier` (штатный путь `CustomisationClientState`, при неподписанном applier — прямой вызов `ApplyBodyType` из меню). Визуал — за пользователем; `NetworkPlayer.prefab` не тронут.
7. **Root motion — probe и игрок.** На probe `MotionBricksBakedRootMotion` двигает transform через `_allowTransformFallback` (2.00 м/с, T-MB04b). На сетевом персонаже он Move не вызывает: публикует смещение в `MotionBricksLocomotionBridge`, а `NetworkPlayer.ProcessMovement` применяет его в своём единственном `_controller.Move` (T-MB04c, [12](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md)). Контролируемый прогон агента по разрешению: 2.00 м/с, Stop→стоп, body swap Female→Male с перепривязкой; визуал/T-pose/экипировка — за пользователем.
8. **Network/FO** (2 clients, F8/F9) не тестировались.
9. **Sentis 2.6.1 конфликт версий:** Burst 1.8.17 / Collections 2.4.3 / Newtonsoft 3.2.1 vs проект 1.8.29 / 6.5.0 / 3.2.2. Не ставить в main.
10. **Sentis import** 6993-узлового ONNX не проверен; нужен T-MB06.
11. **`Animator.CrossFade` не играет произвольный клип:** для MVP использовать `PlayableGraph`/`AnimationClipPlayable` или Animancer.
12. **Play Mode/визуал** — только пользователь.
13. **Лицензии:** sample motions eval-only; модели — NVIDIA Open Model Agreement; attribution.
14. **`tmp_tmb04_*.bat`** в корне main — temp-файлы; удалить после проверки, не коммитить.
15. **Побочный эффект T-MB04c Play Mode:** сессия сохранила позицию в `ShipPositions.json` (`players[0] ≈ (-95.9, -99.0, -16.1)`); если не нужна — MainMenu → debug «Delete Character Position Saves» или respawn. Сейв не коммитить.

**Обновление 15.11, вечер (T-MB04a):**
- main содержит код/docs (`934ecdf6`); `MotionBricksRuntimeSettings.asset` — `mode: 0`; baseline 10/10.
- KL Walk01: ось vs лицо 0.1°, опора 2.00 / 2.00 м/с, асимметрия 0 %, slide 0.07 / 0.11 см/кадр,
  подъём 0.036 м; `B-upperArm.L` 23.7° в LateUpdate.
- `WalkToJog_Male_Humanoid` остаётся на пробе; для ходьбы использовать `ProjectC/MotionBricks/Review/Play KL Walk01 Forward on probe`.
- `MotionBricksBakedRootMotion` — новый файл, не подключён; `_clipSpeed = 0` (инертен). Runtime-тест — пользователь.
- `feature/motionbricks` — backup; в ветке settings `mode: 1`, в main `mode: 0`; не мержить слепо.

**Обновление 15.11, вечер (T-MB04b):**
- Probe: `MotionBricksBakedRootMotion` с `_clipSpeed = 2.00`, `_acceleration = 0`, `_allowTransformFallback = true`;
  Animator пробы — `applyRootMotion = false`, `cullingMode = AlwaysAnimate` (раньше root motion был включён и мог
  складываться с нашим движением). Проверено: лицо модели = +Z = `transform.forward`.
- Меню: `Review/Play KL Walk01 Forward on probe`, `Review/Probe/Start|Log travel measurement`,
  `Review/Player/Attach + play KL Walk01 on my player`, `Review/Body swap/Body swap → Female|Male`,
  `Review/Focus probe in Scene view`, `Review/Log harness state`.
- Body swap — на живом игроке: playback добавляется `AddComponent` в сессии Play Mode (префаб не меняется),
  свап идёт через настоящий `CharacterCustomisationApplier`; при неподписанном applier меню вызывает его
  `ApplyBodyType` напрямую (только редактор).
- `MotionBricksBakedRootMotion` перепривязывается на `BodySwapped` и читает таз в `LateUpdate`.
- `NetworkPlayer.cs` **не менялся**: интеграция root motion в movement — следующий шаг после подтверждения.
- **Приёмка 15.11 (Play Mode, пользователь), сеанс 1:** `Play 'HumanM@Walk01_Forward'` на `MB_Probe_Player`,
  `B-upperArm.L` 24.3°, `controller=null`, `humanMotion=True`, петля сработала (`t=0,81/0,80`);
  визуально «идёт нормально, кукла не шаркает»; выход из Play Mode → «baseline Animator восстановлен».
- **Сеанс 2:** `Stop on probe` нажат — baseline восстановлен; `B-upperArm.L` 14.5°;
  body swap Male→Female→Male на живом игроке — `Applied bodyType=` и `отреагировало applier'ов 1/1`
  (штатный путь, без reflection); модели — из `NetworkPlayer_GlobalPilot.prefab`
  (male `testing`, female `PC_Char_Adjudicator_Female_APOSE_v01_Rigged_Humanoid`).
  Ходьба после свапа визуально та же — ожидаемо (humanoid-ретаргет, тот же контроллер), T-pose нет.
  F8 ×7 у нуля → ожидаемый `no_rebase_plan`, ошибок нет.
- **Сеанс 3 (закрытие):** поездка — `30,42 м / 15,21 s = 2,00 м/с`; body swap при **активном playback**
  проверен на probe с настоящим `CharacterCustomisationApplier` (временный компонент, в сцену не сохранён):
  после `ApplyBodyType(Female/Male)` — новый `Play 'HumanM@Walk01_Forward'`, `MotionBricksBakedRootMotion`
  перепривязался к новой `B-hips`, кость двигалась на 18.8°/29.6°. Play Mode остановлен, сцена не изменена.
- **Вне тикета (чек-лист T-MB08):** 2 клиента, F8/F9 от нуля, отдельная визуальная проверка экипировки.
- **Найдено:** `check_baseline.ps1` защищает `NetworkPlayer.prefab`, но рантайм-игрок —
  `NetworkPlayer_GlobalPilot.prefab` (подставляется `GlobalMotionNetworkStartup` из профиля,
  роль `Spatial`). Для будущей интеграции playback/root motion в игрока целиться в GlobalPilot
  и, вероятно, добавить его в baseline отдельным решением.
- **Найдено (для T-MB03/05):** новое тело после свапа приходит с FBX-дефолтами Animator
  (`applyRootMotion = true`, `cullingMode = CullUpdateTransforms`), а не с override'ами префаба.
  На текущем in-place KL-клипе дрейфа нет (`Body.localPosition = (0,0,0)` за 16 с), но для клипа
  с переносом корня это даст двойное движение — нужен гард на playback (выключить/восстановить
  `applyRootMotion` с учётом `SkillAnimationPlayer`).

**Обновление 15.11, ночь (T-MB03):**
- Бейкер больше не знает конкретных клипов: `MotionBricksBakeJob.Discover()` собирает задания из
  `Generated/MotionBricks/<primitive>/<body>/*_30fps.f32` (+ `soma_bind.f32`); необязательный
  sidecar `<ClipBase>.bake.config.json` задаёт `groundFeet`/`bakeInPlace`/`clipSpeed`/`label`.
- Меню: `Bake/All discovered clips (batch)`, `Bake/Bake + validate all discovered`,
  `Bake/List discovered clips`, `Validate/All discovered clips (batch)`, `Bake/Foot IK self-test`,
  `Bake/applyRootMotion guard self-test`. Старые точечные пункты сохранены.
- Метаданные: `<ClipBase>.bake.report.json` — SHA256 сырья/bind-позы, настройки, покадровая
  foot-IK трасса, метрики валидатора, GUID до/после (`guidStable`).
- Foot IK: `PelvisSmooth` вместо «прижать каждый кадр». `WalkForward_Male` 15.73 → 11.73 см
  (hover 4.0 / penetration 1.0 при порогах 5/2), `WalkToJog_Male` 5.10 → 1.10 см. A/B без IK:
  22.41 см. Валидатор не ослаблялся; оба синтезированных клипа по-прежнему FAIL по оси/асимметрии —
  это контент, не пайплайн.
- `applyRootMotion` guard: playback выключает флаг на время графа и возвращает после; при body swap
  перепривязывается; во время каста `SkillAnimationPlayer` флаг не перетирает. Self-test 6/6 без
  Play Mode.
- `SaveClipInPlace`: GUID всех четырёх клипов стабилен на трёх перепёках; ссылка `MB_Probe_Player._clip`
  в `WorldScene_0_0` жива.
- Baseline 12/12 до/после; Play Mode не запускался. Подробности — [11](11_T-MB03_BAKER_AND_GUARDS.md).
- **Передача 15.11:** T-MB04c **не начат** — одобрения на правку `NetworkPlayer.cs` нет (пользователь
  отдаёт анализ другому агенту); готовый план «мост + один Move» — [11 §13.2](11_T-MB03_BAKER_AND_GUARDS.md).
  Добавление `NetworkPlayer_GlobalPilot.prefab` в baseline — решение отложено (см. [11 §13.3](11_T-MB03_BAKER_AND_GUARDS.md)).

**Обновление 15.11, ночь (T-MB04c):**
- `MotionBricksLocomotionBridge` — статический реестр активных `MotionBricksBakedRootMotion` и
  `TryConsumeDisplacement(NetworkPlayer, out Vector3)`. Гейт: `mode != Disabled` (или `ReviewOverride`),
  `allowPlayer`, `player.IsOwner`, `playback.IsPlaying`; иначе `false` → baseline-движение.
- `MotionBricksBakedRootMotion` на объекте с `NetworkPlayer` больше не пишет warning «нельзя», а публикует
  смещение в мост; `[DefaultExecutionOrder(-100)]` гарантирует, что смещение готово до `ProcessMovement`.
  На probe путь не изменился (`CharacterController`/transform fallback).
- `NetworkPlayer.ProcessMovement`: +11 строк — если мост дал displacement, горизонталь кадра = baked
  displacement + `_platformDelta`; вертикаль/гравитация/платформа сохраняются. `_controller.Move` остался один.
- Меню: `Player/Attach + play KL Walk01 on my player` теперь ставит и root motion (2.00 м/с, ускорение 0);
  добавлены `Player/Start|Log travel measurement`; `Log harness state` печатает `player root motion` и
  `bridgeSources`. `Detach` убирает оба компонента.
- Baseline: `NetworkPlayer.cs` — **CHANGED** (намеренно, с одобрения), хэш `9cc44bf7…`; `baseline_hashes.tsv`
  обновлён **отдельным коммитом**; до/после обновления — 12/12.
- **Runtime (один прогон агента по разрешению пользователя):** Host через штатный
  `GlobalMotionPilotRuntime.StartPilotHost()`; attach → поездка **33.13 м / 16.56 с = 2.00 м/с**,
  `isGrounded`, forward +Z; `Stop` → позиция не меняется 20 с; body swap Female→Male при активном
  playback — `PlayerAnimation_Female/Default`, playback/`B-hips` перепривязаны, 0 errors. Визуал
  T-pose/экипировки — за пользователем. Детали и оговорка про culling — [12 §6.1](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md).
- Приёмка пользователем — [12](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md) §6 (в первую очередь глазами:
  стопы не шаркают, T-pose при свапе нет, экипировка на месте).

**Обновление 16.11 (T-MB05):**
- **2-bone IK ног** в бейкере (`MotionBricksLegIk`): таз идёт по достижимости + естественному bob,
  стопу ставит аналитический IK (закон косинусов, плоскость колена из позы, мировой поворот стопы
  сохраняется). Откат — sidecar `{ "legIk": false }` → `PelvisSmooth`.
- Замеры (bake + validate, offline): `WalkForward_Male` таз **15.7 → 1.7 см**, humanoid
  hover/pen **0.09/0.00**; `WalkToJog_Male` (one-shot) **5.1 → 4.6 см**, **0.30/0.20**.
  Raw pose hover/pen 0.1/0.0. Оба контакта — в допуске валидатора (5/2).
  Ось/асимметрия/разворот по-прежнему FAIL — контент.
- **`loop:false`:** sidecar + `loopTime=false`/`WrapMode.Clamp`; `PelvisSmooth`/`SolveCurve` получили
  `cyclic`; self-test «one-shot шов» PASS (cyclic −1.0 см на старте, non-cyclic 0.0 см).
- **Humanoid T-поза bug fix:** `HumanPoseHandler.GetHumanPose` на инстансе с Animator читал T-позу;
  humanoid-клип теперь сэмплирует generic-клип через `SampleAnimation` на инстансе **без Animator**
  (Avatar остаётся). После фикса `B-shoulder/upperArm` двигаются, мышцы соответствуют позе.
- **Runtime-фиксы (после Play Mode на probe):**
  - **stale parent в leg IK** — IK после цикла кадров брал поворот таза из последнего кадра;
    на `WalkToJog` это давало 172° переворот бёдер. Теперь `LegFrame.ParentRotation` на кадр.
  - **last-frame wrap humanoid-бейка** — сэмпл ровно в `clip.length` при `loopTime=true`
    заворачивал последний кадр на первый; теперь `length − 1e-4`, yaw drift вернулся к −195.6°.
  - **Arm Twist ±180/360** — `Arm Twist In-Out` ~2.0, Animator клампит и рука переворачивалась
    на 178.8°; twist разворачивается, arm twist ограничен ±0.5.
  - **one-shot** — playback не зацикливает клип с `isLooping=false`; review-меню `WalkToJog`
    играет с `loop:false`.
- Self-tests: Foot IK 6/6, Leg IK 6/6, Loop 4/4, applyRootMotion guard 6/6. Baseline 12/12.
- **Главная причина «выворота» — `applyRootMotion` guard** глушил humanoid **RootQ**: корпус
  не поворачивался (`hipsYaw≈5`), а руки/ноги проигрывали разворот (`armLocalYaw≈280`).
  Review-меню на probe после старта humanoid-клипа возвращает `applyRootMotion=true`
  (RootT XZ in-place, двойного движения нет). Runtime-замер: `hipsYaw 2→313→173`,
  `armWorldYaw 340→249→172` — рука идёт с корпусом. На **игроке** guard пока оставлен:
  turn-клип на player требует разделения RootQ/RootT — отдельное решение.
- **Ключица:** `B-shoulder` теперь получает локальный delta source-джойнтов 11/39 (клэмп 45°),
  а не только swing chest→shoulder; arm local перестал компенсировать недо-поворот.
- **Остаётся:** `WalkToJog` начинается уже в повёрнутой позе — с rest-позы виден snap первых
  ~0.6 с (контент/clip start). Клип по-прежнему FAIL по yaw/оси/асимметрии; визуальный
  эталон locomotion — **KL Walk01**. `WalkForward` foot slide 2.88 — контент.
- **Не делалось (по решению):** NPC baked pool, culling `AlwaysAnimate` после body swap,
  `NetworkPlayer_GlobalPilot.prefab` в baseline.
- Подробности — [13](13_T-MB05_LEG_IK_AND_LOOP.md); design — `docs/dev/T-MB05_LEG_IK_LOOP_DESIGN.md`.

---

## 9. Definition of Done для T-MB02

- [x] Baseline check PASS до и после.
- [x] Один baked-клип импортирован в `Generated/MotionBricks/`.
- [x] Проигрывается на scene-объекте через `PlayableGraph` (без правок prefab/controller).
- [x] После остановки — baseline Animator (нет T-pose).
- [x] Нет изменений в baseline-файлах (или изменения объяснены/одобрены).
- [x] Пользователь подтвердил визуально.
- [x] Отчёт: что сгенерировали, чем, метрики, проблемы.


**Приёмка 15.11.2026 (Play Mode, пользователь).** `Play 'WalkToJog_Male_Humanoid' (1,63 s, speed 1,00) на 'HumanM_Model'. Controller не менялся.`; `diag` (уже из `LateUpdate`): `t=0,64 s`, `humanMotion=True`, поза корректная, конечности не вывернуты. Клип воспроизводится как разворот на месте — так и в источнике: root yaw −71,0° → −267,7° (≈ −197°), смещение по XZ 0,06 м (`WalkToJog_Male_30fps.f32`).

**Baseline после работ:** `check_baseline.ps1 -SkipPackages` → `BASELINE OK: 10 files match`; `git diff -- Packages` пуст в worktree и в main.

**Перенесено в T-MB04:** сид-клип с ходьбой вперёд (текущий разворачивает на месте), foot IK (`footRY` до 0,299 при rest 0,128), и проверка слабого движения рук — `diag` по `B-upperArm.L` не менялся, хотя muscle-кривые рук меняются на 0,26–0,30.

---

*Удачи. Если сомневаешься — сначала baseline check, потом изменения; лучше остановиться на baked-клипах, чем ломать Animator.*
