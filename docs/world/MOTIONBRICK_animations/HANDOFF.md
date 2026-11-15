# HANDOFF — MotionBricks integration

> **Для:** следующего агента/разработчика, который делает интеграцию.
> **Дата:** 2026-11-15
> **Статус:** research + offline pack + safe rollout + **T-MB02 spike** готовы; production-интеграции ещё нет.
> **Главное правило:** текущий Animator — дефолт; MotionBricks — additive, off by default, с откатом в минуты.
> **T-MB02:** `bake → import → playback` работает; качество/IK/покрытие — T-MB03/04. См. [07](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) · [08](08_T-MB02_RETARGET_FINDINGS.md) · [09](09_T-MB02_ANALYSIS_AND_NEXT.md).
> **T-MB04a (вечер 15.11):** рабочая forward-walk — авторский клип KL; все метрики опоры в допуске, `B-upperArm.L` двигается (23.7°), baseline восстанавливается; root motion написан (`MotionBricksBakedRootMotion`, в сцену намеренно не подключён). Открыто: body swap при активном playback, 2 клиента / F8 / F9. Подробности — [09](09_T-MB02_ANALYSIS_AND_NEXT.md).

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
| Baseline animation files | ✅ `check_baseline.ps1` — 12/12 match (main); worktree `-SkipContent` — 4 scripts |
| Sentis import | ❌ не проверялся; конфликт версий Burst/Collections/Newtonsoft |
| Unity Play Mode | ❌ не запускался (правило `dont-start-play`) |

---

## 2.1. Коммиты

| Коммит | Что |
|---|---|
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
2. Создать worktree:
   ```powershell
   cd C:\Projects\ProjectC_client
   git worktree add C:\Projects\ProjectC_client_motionbricks -b feature/motionbricks
   ```
3. Запустить baseline check:
   ```powershell
   # worktree = только код/docs; контент (prefab/controller/anim/сцены) живёт в Lore/main.
   powershell -ExecutionPolicy Bypass -File `
     C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1 `
     -SkipPackages -SkipContent
   ```
   Контентный baseline и пакеты проверяйте в `C:\Projects\ProjectC_client` (main).
4. Убедиться, что локальные ресурсы на месте:
   ```powershell
   Test-Path C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe
   Test-Path C:\Projects\motion_bricks\tools\cpu_baker\.venv\Scripts\python.exe
   ```
5. Начать `T-MB03/04` (код/docs — в worktree; Unity-ассеты — в main).

**Worktree vs main:** worktree не содержит Lore-контента — в нём нет сцен, префабов, контроллеров, `.anim`, FBX. Открывать Unity и делать импорт/playback нужно в `C:\Projects\ProjectC_client`; код и docs коммитить в `feature/motionbricks`. Не копировать контент во второй worktree и не открывать два Unity на одном проекте.

---

## 4. Тикеты и порядок

| Тикет | Статус | Что делать |
|---|---|---|
| `T-MB01` | ✅ done | Research/decision |
| `T-MB01A` | ✅ done | Offline resource pack + local links |
| `T-MB01B` | ✅ done | Safe rollout/rollback + baseline manifest + handoff |
| `T-MB02` | ✅ done | Испечь **один** клип, импортировать, проиграть на scene-объекте (приёмка 15.11 — `HANDOFF §9`) |
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

1. **Сид-клип — разворот на месте.** `walk_to_jog` в источнике даёт root yaw ≈197° и XZ 0.06 м; для ходьбы нужен forward-walk seed (T-MB04).
2. **Плечи не анимируются.** `B-shoulder.L/R` наследуют грудь; `11→12`/`39→40` degenerate swing.
3. **Руки под вопросом.** `diag` по `B-upperArm.L` не меняется, хотя muscle-кривые рук меняются на 0.26–0.30; проверить на forward-walk.
4. **Foot IK нет.** `footRY` до 0.299 при rest 0.128; стопа висит; нужен IK-слой.
5. **`MotionBricksRuntimeSettings.mode = 1`** и probe `MB_Probe_Player` в `WorldScene_0_0`; перед завершением/мержем — `Disabled`/удалить probe из production-сцены.
6. **Body swap M↔F** на player-префабе не тестирован; подписка на `BodySwapped` есть.
7. **Network/FO** (2 clients, F8/F9) не тестировались.
8. **Sentis 2.6.1 конфликт версий:** Burst 1.8.17 / Collections 2.4.3 / Newtonsoft 3.2.1 vs проект 1.8.29 / 6.5.0 / 3.2.2. Не ставить в main.
9. **Sentis import** 6993-узлового ONNX не проверен; нужен T-MB06.
10. **`Animator.CrossFade` не играет произвольный клип:** для MVP использовать `PlayableGraph`/`AnimationClipPlayable` или Animancer.
11. **Play Mode/визуал** — только пользователь.
12. **Лицензии:** sample motions eval-only; модели — NVIDIA Open Model Agreement; attribution.

**Обновление 15.11, вечер (T-MB04a):**
- пп. 1–4 закрыты для рабочего клипа: ходьба — авторский KL (ось vs лицо 0.1°, опора 2.00 / 2.00 м/с,
  асимметрия 0 %, slide 0.07 / 0.11 см/кадр, подъём стопы над землёй клипа 0.036); `B-upperArm.L`
  двигается на 23.7° в LateUpdate при `controller = null`;
- п. 5 актуален частично: `MotionBricksRuntimeSettings.mode = Disabled` ✓; `MB_Probe_Player` остаётся
  в `WorldScene_0_0` — это разрешённая брифом ветка;
- **п. 6 стал блокером:** `CharacterCustomisationApplier` есть только у настоящего персонажа, а
  `MotionBricksBakedPlayback` — только у пробы. Сценарий «свап при активном playback» нельзя проверить
  без правки `NetworkPlayer.prefab` (запрещено) либо без applier на пробе (материалы + сокеты).
  Обработчик `OnBodySwapped` подтверждён только чтением кода;
- п. 7 по-прежнему проверяет только пользователь;
- новое: `MotionBricksBakedRootMotion` написан, никуда не подключён, в рантайме не проверялся.
  Замеренные скорости для настройки: KL Walk01 — 2.00 м/с, наш синтезированный WalkForward — 0.54 м/с;
- новое: тазовый foot IK пережимает стопу (подъём 0.000 при размахе таза 0.780..0.938 = 15.8 см против
  2 см у эталона) — таз утягивается вниз, это T-MB03.

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
