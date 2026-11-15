# HANDOFF — MotionBricks integration

> **Для:** следующего агента/разработчика, который делает интеграцию.
> **Дата:** 2026-11-15
> **Статус:** research + offline pack + safe rollout + **T-MB02 spike** + **T-MB04b accepted**; production-интеграции ещё нет.
> **Главное правило:** текущий Animator — дефолт; MotionBricks — additive, off by default, с откатом в минуты.
> **Main:** код и docs импортированы в `main` коммитом `934ecdf6`; `MotionBricksRuntimeSettings.asset` в main — `mode: 0` (Disabled). `feature/motionbricks` — backup/история, не мержить слепо (в ветке `mode: 1`).
> **T-MB02:** `bake → import → playback` работает; качество/IK/покрытие — T-MB03/04. См. [07](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) · [08](08_T-MB02_RETARGET_FINDINGS.md) · [09](09_T-MB02_ANALYSIS_AND_NEXT.md).
> **T-MB04a (историческая):** рабочая forward-walk — авторский клип KL; метрики/руки/baseline подтверждены. Runtime-тест root motion и body swap закрыты в T-MB04b; см. [10](10_T-MB04B_RUNTIME_HARNESS.md).
> **T-MB04b (вечер 15.11): ✅ принят.** Root motion на probe: **2.00 м/с** (`30,42 м / 15,21 s`), стопы не шаркают, `Stop`→baseline. Body swap Male→Female→Male на живом игроке — штатный путь 1/1; body swap при **активном playback** проверен на probe с настоящим applier — playback и root motion перепривязываются, кость двигается (18.8°/29.6°). Осталось вне тикета (в чек-лист T-MB08): 2 клиента, F8/F9 от нуля. Второй `CharacterController.Move` запрещён кодом; интеграция в `NetworkPlayer` — отдельный шаг.
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
| Baseline animation files | ✅ `check_baseline.ps1` - 12/12 match (main); worktree `-SkipContent` - 4 scripts |
| Sentis import | ❌ не проверялся; конфликт версий Burst/Collections/Newtonsoft |
| Unity Play Mode | ✅ T-MB04b: пользователь + один прогон агента по разрешению; агент сам Play Mode не запускает |

---

## 2.1. Коммиты

| Коммит | Что |
|---|---|
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
5. Начать `T-MB03` (baker/pelvis IK/applyRootMotion guard) и `T-MB08` (2 clients/F8-F9/equipment) — код и Unity-ассеты в main; baseline до/после.

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
| `T-MB04c` | ⬜ next | Интеграция root motion в `NetworkPlayer.ProcessMovement` под feature flag; правка baseline-файла — только с одобрения |
| `T-MB03` | ⬜ next | Baker MVP: generalize, batch, metadata, validation; фикс pelvis foot IK; guard `applyRootMotion` после body swap |
| `T-MB05` | ⬜ | Hybrid primitives: authoring/registry/playback |
| `T-MB06` | ⬜ optional | Sentis import + benchmark (timebox) |
| `T-MB07` | ⬜ optional | Runtime experiment (only after go) |
| `T-MB08` | ⬜ | Go/no-go review |
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
4. **Foot IK / pelvis.** Для KL метрики в допуске (slide 0.07/0.11 см/кадр, подъём 0.036 м), но тазовый foot IK в бейкере пережимает стопу (размах таза 15.8 см против 2 см) — T-MB03.
5. **`MotionBricksRuntimeSettings.mode`** в main — `0` (Disabled) ✓; probe `MB_Probe_Player` остаётся в `WorldScene_0_0` как dev-объект (разрешённая ветка).
6. **Body swap M↔F — стенд готов.** Playback добавляется в рантайме на живого игрока; свап идёт через настоящий `CharacterCustomisationApplier` (штатный путь `CustomisationClientState`, при неподписанном applier — прямой вызов `ApplyBodyType` из меню). Визуал — за пользователем; `NetworkPlayer.prefab` не тронут.
7. **Root motion — на probe, не на игроке.** `MotionBricksBakedRootMotion` стоит на `MB_Probe_Player`: `_clipSpeed = 2.00`, `_acceleration = 0`, `_allowTransformFallback = true`. На сетевом персонаже компонент **запрещён кодом** (второй `CharacterController.Move` невозможен); полноценная интеграция в `NetworkPlayer.ProcessMovement` — отдельный шаг, требует правки baseline-файла `NetworkPlayer.cs`.
8. **Network/FO** (2 clients, F8/F9) не тестировались.
9. **Sentis 2.6.1 конфликт версий:** Burst 1.8.17 / Collections 2.4.3 / Newtonsoft 3.2.1 vs проект 1.8.29 / 6.5.0 / 3.2.2. Не ставить в main.
10. **Sentis import** 6993-узлового ONNX не проверен; нужен T-MB06.
11. **`Animator.CrossFade` не играет произвольный клип:** для MVP использовать `PlayableGraph`/`AnimationClipPlayable` или Animancer.
12. **Play Mode/визуал** — только пользователь.
13. **Лицензии:** sample motions eval-only; модели — NVIDIA Open Model Agreement; attribution.
14. **`tmp_tmb04_*.bat`** в корне main — temp-файлы; удалить после проверки, не коммитить.

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
