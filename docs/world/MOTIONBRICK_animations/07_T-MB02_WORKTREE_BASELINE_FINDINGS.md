# T-MB02 — Аудит worktree и baseline (первый прогон)

> **Дата:** 2026-11-15
> **Ветка:** `feature/motionbricks` (worktree `C:\Projects\ProjectC_client_motionbricks`)
> **HEAD:** `dc038728` (`T-MB01B: add commit index to handoff`)
> **Статус:** worktree создан, baseline проверен; обнаружен **разрыв в плане T-MB01B** (см. §3). Код интеграции не писался.

---

## 1. Что сделано

| Шаг | Команда | Результат |
|---|---|---|
| 1 | `git worktree add C:\Projects\ProjectC_client_motionbricks -b feature/motionbricks` | ✅ worktree создан, 5285 файлов, HEAD `dc038728`, рабочее дерево чистое |
| 2 | `check_baseline.ps1 -Root <worktree> -SkipPackages` | ❌ **6 из 10 файлов MISSING** |
| 3 | `check_baseline.ps1 -Root C:\Projects\ProjectC_client -SkipPackages` | ✅ **BASELINE OK: 10 files match** |
| 4 | Проверка локальных ресурсов MotionBricks | ✅ всё на месте (см. §4) |
| 5 | Состояние редактора Unity | проект `ProjectC_client` (main), открыта `WorldScene_0_0.unity`, `hasCompilationErrors=false`, Play Mode выключен |

---

## 2. Факт №1: контент проекта не хранится в git

`.gitignore` (строки 151–182, коммит `dc038728`):

```
# Content assets — tracked via Lore VCS, not git
*.unity
*.prefab
*.fbx
...
# Animation assets
*.anim
*.controller
*.overrideController
# Unity metadata
*.meta
```

`git check-ignore -v` подтверждает источник каждого «пропавшего» файла:

| Файл | Правило |
|---|---|
| `Assets/_Project/Animations/PlayerAnimation.controller` | `.gitignore:168 *.controller` |
| `Assets/_Project/Animations/PlayerAnimation_Default.overrideController` | `.gitignore:169 *.overrideController` |
| `Assets/_Project/Animations/PlayerAnimation_Female.overrideController` | `.gitignore:169` |
| `Assets/_Project/Prefabs/NetworkPlayer.prefab` | `.gitignore:153 *.prefab` |
| `Assets/_Project/Prefabs/AI/Npc_Goblin.prefab` | `.gitignore:153` |
| `Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController` | `.gitignore:169` |

Скриптовые файлы (`NetworkPlayer.cs`, `CharacterCustomisationApplier.cs`, `SkillAnimationPlayer.cs`, `NpcBrain.cs`) — в git есть, поэтому в worktree они `OK`.

---

## 3. Факт №2: baseline и T-MB02 в чистом worktree не воспроизводимы

`BASELINE_MANIFEST.md` §3.1 оговаривает только пакеты («в чистом worktree используйте `-SkipPackages`»). Про контент оговорки нет — предполагалось, что worktree из HEAD содержит анимационные ассеты. **Не содержит.**

Следствия, которые ломают документированный порядок §7.2 `06_SAFE_ROLLOUT_AND_ROLLBACK.md`:

1. **`check_baseline.ps1` в worktree всегда FAIL** на 6 файлах — это не регрессия, а отсутствие файлов в VCS. Baseline как guard имеет смысл только против рабочего дерева `C:\Projects\ProjectC_client` (Lore-контент), где он сейчас честно показывает `10 files match`.
2. **T-MB02 (шаги 3–5: импорт `.anim`, `MotionBricksBakedPlayback` на scene-объекте, playback на текущем Animator) в worktree физически невозможен**: в нём нет ни одной сцены (`*.unity` игнорируется), ни префаба игрока, ни контроллера, ни модели. Открыть worktree в Unity — значит получить пустой проект без `BootstrapScene.unity` и `WorldScene_0_0.unity` (единственные рабочие сцены проекта).
3. Git-изоляция защищает **код и документацию** (они в VCS), но не защищает и не воспроизводит **ассеты** — их откат/изоляция обеспечиваются только `check_baseline.ps1` + отсутствием записи в baseline-файлы.

### 3.1. Что это меняет

Разделение уровней:

| Слой | Где живёт | Как изолируется |
|---|---|---|
| C#-код, docs, отчёты | git | ветка `feature/motionbricks` в worktree |
| Сцены, префабы, `.anim`, контроллеры | Lore (main working tree) | только `check_baseline.ps1` + правило «не писать в baseline-файлы» |

То есть **git-worktree остаётся правильным контейнером для кода**, но работу с ассетами Unity-редактор может делать только в `C:\Projects\ProjectC_client`.

---

## 4. Локальные ресурсы (проверено)

| Ресурс | Путь | Статус |
|---|---|---|
| CPU venv (`onnxruntime 1.32`) | `C:\Projects\motion_bricks\tools\cpu_baker\.venv\Scripts\python.exe` | ✅ есть |
| TensorRT venv | `C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe` | ✅ есть |
| CLI `sa-inbetween` | `...\sa-inbetween\releases\v0.9.4\tools-win-x64\SaInbetween\cli\sa-inbetween.exe` | ✅ есть |
| SOMA FP32 ONNX (758 MB) | `...\motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp32.onnx` | ✅ есть |
| Unity | `6000.5.2f1` | ✅ |

Worktree не содержит `Library/` — при открытии его в Unity потребуется полный реимпорт.

---

## 5. Текущее состояние main (не трогали)

```
## main...origin/main [ahead 7]
 M Assets/_Project/Editor/World/SplineFenceTool.cs
 M Assets/_Project/Scenes/World/WorldScene_0_0.unity
 D Assets/_Project/UIv2/Tools/Agent/UIv2RiveBinder.cs
 M Packages/manifest.json
 M Packages/packages-lock.json
 M docs/world/spline_assets/00_DESIGN_SplineFence.md
?? Assets/_Project/Generated/Roads/Торговые улочки-3/
```

Пользовательские изменения сохранены, `git reset/checkout` не выполнялись.

---

## 6. Открытый вопрос для T-MB02

Нужно решение: где выполнять Unity-часть T-MB02 (импорт `.anim` + playback на scene-объекте), учитывая §3.

Кандидаты:

- **A.** В main (`ProjectC_client`), добавляя **только новые** файлы (`Assets/_Project/Animations/Generated/MotionBricks/…`, новый компонент), с `check_baseline.ps1` до/после; код и отчёты — коммитами в `feature/motionbricks`.
- **B.** В main, но изолировать сцену: playback вешать на отдельный probe-объект в `WorldScene_0_0` и не сохранять сцену до подтверждения (объект живёт только в сессии Play Mode — проверку делает пользователь).
- **C.** Скопировать Lore-контент в worktree и открыть второй Unity (полный реимпорт, ~2 проекта на диске, риск двух редакторов).
- **D.** Ограничить T-MB02 офлайн-частью (испечь клип + отчёт, без импорта в Unity); Unity-часть делает пользователь.

До ответа Unity-ассеты не изменялись.

---

## 7. Next

1. Получить решение по §6.
2. Baseline check → PASS перед изменениями.
3. Испечь один клип (CPU CLI `sa-inbetween` / ONNX CPU) → отчёт с метриками.
4. Импорт + `MotionBricksBakedPlayback` на scene-объекте — без правок prefab/controller.
5. Baseline check после → отчёт.

---

*Связанные: [HANDOFF.md](HANDOFF.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [baseline/BASELINE_MANIFEST.md](baseline/BASELINE_MANIFEST.md)*
