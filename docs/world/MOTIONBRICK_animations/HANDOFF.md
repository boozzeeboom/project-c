# HANDOFF — MotionBricks integration

> **Для:** следующего агента/разработчика, который делает интеграцию.
> **Дата:** 2026-11-15
> **Статус:** research + offline resource pack + safe rollout plan готовы; код интеграции **не писался**.
> **Главное правило:** текущий Animator — дефолт; MotionBricks — additive, off by default, с откатом в минуты.

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
| Baseline animation files | ✅ `check_baseline.ps1` — 12/12 match |
| Sentis import | ❌ не проверялся; конфликт версий Burst/Collections/Newtonsoft |
| Unity Play Mode | ❌ не запускался (правило `dont-start-play`) |

---

## 3. С чего начать (первые 30 минут)

1. Прочитать:
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\README.md`
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\06_SAFE_ROLLOUT_AND_ROLLBACK.md`
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\02_PIPELINE.md` (§3–§7)
   - `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\05_LOCAL_RESOURCES.md`
2. Создать worktree:
   ```powershell
   cd C:\Projects\ProjectC_client
   git worktree add C:\Projects\ProjectC_client_motionbricks -b feature/motionbricks
   ```
3. Запустить baseline check:
   ```powershell
   # в чистом worktree пакеты могут отличаться от working-tree snapshot в main:
   powershell -ExecutionPolicy Bypass -File `
     C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1 `
     -SkipPackages
   ```
   Пакеты (`Packages/*`) проверяйте в main отдельно (`git diff -- Packages`).
4. Убедиться, что локальные ресурсы на месте:
   ```powershell
   Test-Path C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe
   Test-Path C:\Projects\motion_bricks\tools\cpu_baker\.venv\Scripts\python.exe
   ```
5. Начать `T-MB02` (один клип; без prefab/controller).

---

## 4. Тикеты и порядок

| Тикет | Статус | Что делать |
|---|---|---|
| `T-MB01` | ✅ done | Research/decision |
| `T-MB01A` | ✅ done | Offline resource pack + local links |
| `T-MB01B` | ✅ done | Safe rollout/rollback + baseline manifest + handoff |
| `T-MB02` | ⬜ next | Испечь **один** клип, импортировать, проиграть на scene-объекте |
| `T-MB03` | ⬜ | Baker MVP: Editor window, metadata, validation |
| `T-MB04` | ⬜ | Retarget/IK: SOMA→Humanoid, T-pose, foot IK, equipment |
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

1. **Sentis 2.6.1 конфликт версий:** просит Burst 1.8.17 / Collections 2.4.3 / Newtonsoft 3.2.1; в проекте Burst 1.8.29 / Collections 6.5.0 / Newtonsoft 3.2.2. Не ставить в main; тестировать в отдельной копии.
2. **Sentis import** 6993-узлового ONNX не проверен; нужен T-MB06.
3. **Body swap M↔F:** новый playback/retarget должен подписаться на `CharacterCustomisationApplier.BodySwapped`; иначе T-pose.
4. **`Animator.CrossFade` не играет произвольный клип:** для MVP использовать `PlayableGraph`/`AnimationClipPlayable` или Animancer, не править контроллер.
5. **Play Mode/визуал** — только пользователь.
6. **Лицензии:** sample motions eval-only; модели — NVIDIA Open Model Agreement; attribution.

---

## 9. Definition of Done для T-MB02

- [ ] Baseline check PASS до и после.
- [ ] Один baked-клип импортирован в `Generated/MotionBricks/`.
- [ ] Проигрывается на scene-объекте через `PlayableGraph` (без правок prefab/controller).
- [ ] После остановки — baseline Animator (нет T-pose).
- [ ] Нет изменений в baseline-файлах (или изменения объяснены/одобрены).
- [ ] Пользователь подтвердил визуально.
- [ ] Отчёт: что сгенерировали, чем, метрики, проблемы.

---

*Удачи. Если сомневаешься — сначала baseline check, потом изменения; лучше остановиться на baked-клипах, чем ломать Animator.*
