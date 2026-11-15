# T-MB01B — Baseline Manifest (animation safety net)

> **Дата:** 2026-11-15
> **Назначение:** зафиксировать состояние текущего Animator-пайплайна, чтобы интеграция MotionBricks не сломала то, что уже работает, и чтобы откат был проверяемым.
> **HEAD на момент снимка:** `3c2bb7d48743abf20aebf941538a2201605e637b`
> **Важно:** это снимок **рабочего дерева**, а не коммита. В рабочем дереве уже есть пользовательские изменения (`WorldScene_0_0.unity`, `SplineFenceTool.cs`, `Packages/*`, удалён `UIv2RiveBinder.cs`, untracked `Generated/Roads/...`) — они не входят в baseline-хэши, кроме `Packages/manifest.json` / `packages-lock.json`, которые перечислены явно.

---

## 1. Зачем это

Любая интеграция анимации рискует:
- изменить `AnimatorController` / `AnimatorOverrideController` и сломать состояния/переходы;
- изменить `NetworkPlayer.prefab` и потерять ссылки/слои;
- незаметно поменять `Packages/manifest.json` (Sentis/Burst/Collections) и сломать сборку;
- изменить `SkillAnimationPlayer`/`NetworkPlayer` и сломать `OnAttackImpact`/respawn/body swap.

`baseline_hashes.tsv` + `check_baseline.ps1` дают **дешёвую проверку**: до и после интеграции агент запускает скрипт и видит, какие файлы изменились.

---

## 2. Что зафиксировано

| Файл | Роль |
|---|---|
| `Assets/_Project/Animations/PlayerAnimation.controller` | Базовый контроллер игрока (Idle/Walk/Sprint/Jump/Land/Attack1H/Skill/Gather/Lumber/Turn/Damage) |
| `Assets/_Project/Animations/PlayerAnimation_Default.overrideController` | Male override (runtime controller игрока) |
| `Assets/_Project/Animations/PlayerAnimation_Female.overrideController` | Female override (body swap) |
| `Assets/_Project/Prefabs/NetworkPlayer.prefab` | Префаб игрока: Animator, `applyRootMotion=0`, `CharacterController`, `NetworkTransform` |
| `Assets/_Project/Prefabs/AI/Npc_Goblin.prefab` | Базовый NPC-префаб |
| `Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController` | NPC override controller |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | Движение/анимация/бой/FO |
| `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` | M↔F body swap + `BodySwapped` event |
| `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs` | `T-INP-08`: `AnimatorOverrideController`, `OnAttackImpact`, body swap/FO |
| `Assets/_Project/Scripts/AI/NpcBrain.cs` | Server FSM, `NavMeshAgent`, Animator params |
| `Packages/manifest.json` | Пакеты проекта (Sentis **не** установлен) |
| `Packages/packages-lock.json` | Lock-версии |

Полные SHA256 — в [`baseline_hashes.tsv`](baseline_hashes.tsv). Скрипт — [`check_baseline.ps1`](check_baseline.ps1).

---

## 3. Как пользоваться

### 3.1. Проверить baseline

```powershell
powershell -ExecutionPolicy Bypass -File `
  C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1
```

Ожидаемо:
```
OK       Assets/_Project/Animations/PlayerAnimation.controller
...
BASELINE OK: 12 files match.
```

**Если работа идёт в чистом worktree** (создан из HEAD, без пользовательских изменений `Packages/*`), используйте:
```powershell
powershell -ExecutionPolicy Bypass -File `
  C:\Projects\ProjectC_client_motionbricks\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1 `
  -SkipPackages
```
Пакетные хэши в TSV сняты с **текущего рабочего дерева** (там уже удалён Rive); в чистом worktree они могут отличаться. Для пакетов проверяйте `git diff` в main отдельно.

Если файл изменён — скрипт покажет `CHANGED`/`MISSING` и вернёт exit code 1. Это **не автоматически** означает ошибку: возможно, изменение намеренное. Но его нужно осознанно принять и обновить baseline отдельным коммитом.

### 3.2. Правило для агента интеграции

1. **До начала работы** — запустить скрипт, сохранить вывод в отчёт.
2. **После каждого этапа** (T-MB02/03/04/05/06) — запустить снова.
3. Если изменился файл из baseline:
   - остановиться;
   - объяснить, зачем;
   - проверить `git diff` именно этого файла;
   - получить подтверждение пользователя;
   - только потом обновлять `baseline_hashes.tsv` отдельным коммитом.
4. **Не** перезаписывать baseline-хэши «чтобы скрипт прошёл» without review.

---

## 4. Ограничения

- Хэши считаются по **рабочему дереву**, включая пользовательские изменения на 2026-11-15. Если пользователь продолжит редактировать эти файлы, скрипт будет показывать `CHANGED` — это ожидаемо; нужно синхронизировать baseline с пользователем.
- `Packages/manifest.json` и `packages-lock.json` — тоже working-tree snapshot (в main уже удалён Rive). В чистом worktree из HEAD они могут отличаться; используйте `-SkipPackages` и проверяйте пакеты в main отдельно.
- Хэши не покрывают `.meta`-файлы. Если Unity пересоздаст `.meta` (например, из-за перемещения ассета), GUID может измениться — это отдельная проверка (`git status`, `git diff --stat`).
- Хэши не покрывают сцены (`WorldScene_0_0.unity`) — там уже есть пользовательские изменения; для сцен использовать `git diff` и визуальную проверку.
- Baseline не заменяет коммит/ветку: перед интеграцией всё равно нужен отдельный worktree/branch.
- Проверка не запускает Unity и не валидирует анимацию; она только ловит случайные изменения файлов.

---

## 5. Если baseline нужно обновить

Только по явному решению пользователя:

```powershell
# 1. Убедиться, что изменение намеренное
git diff -- Assets/_Project/Animations/PlayerAnimation.controller

# 2. Пересчитать один файл
(Get-FileHash Assets/_Project/Animations/PlayerAnimation.controller -Algorithm SHA256).Hash.ToLower()

# 3. Обновить строку в baseline_hashes.tsv
# 4. Запустить check_baseline.ps1
# 5. Закоммитить отдельно: "T-MB01B: update baseline after reviewed change"
```

---

*Связанные: [06_SAFE_ROLLOUT_AND_ROLLBACK.md](../06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [HANDOFF.md](../HANDOFF.md) · [02_PIPELINE.md](../02_PIPELINE.md)*
