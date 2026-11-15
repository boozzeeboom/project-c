# T-MB01B — Безопасная интеграция и откат MotionBricks

> **Дата:** 2026-11-15
> **Цель:** интегрировать MotionBricks так, чтобы **текущий Animator-пайплайн остался дефолтным**, а откат занимал минуты, а не дни.
> **Связанные:** [02_PIPELINE.md](02_PIPELINE.md) · [05_LOCAL_RESOURCES.md](05_LOCAL_RESOURCES.md) · [baseline/BASELINE_MANIFEST.md](baseline/BASELINE_MANIFEST.md) · [07](07_T-MB02_WORKTREE_BASELINE_FINDINGS.md) · [08](08_T-MB02_RETARGET_FINDINGS.md) · [09](09_T-MB02_ANALYSIS_AND_NEXT.md) · [HANDOFF.md](HANDOFF.md)

---

## 0. TL;DR

1. **Базовая линия — текущий Animator.** Пока `MotionBricksRuntimeSettings` отсутствует или `Mode=Disabled`, ни один новый компонент не трогает `Animator`, кости, контроллеры и префабы.
2. **Три режима:** `Baseline` (дефолт), `BakedClips` (рекомендуемый ship-путь), `NeuralExperimental` (только игрок, только dev, только Windows+NVIDIA).
3. **Никаких правок существующих ассетов на MVP.** Baked-клипы играются через отдельный `PlayableGraph`/`Animancer`, без изменения `PlayerAnimation.controller` и `NetworkPlayer.prefab`.
4. **Один выключатель.** `Mode=Disabled` → компонент останавливает playback, уничтожает граф, вызывает `Animator.Rebind()`; дальше работает штатный Animator.
5. **Baseline-хэши.** `baseline/check_baseline.ps1` проверяет 12 ключевых файлов; до/после каждого этапа — PASS.
6. **Откат по уровням:** выключить режим → удалить компонент → удалить сгенерированные клипы → `git revert` → выбросить worktree. Main не трогаем.
7. **Никаких изменений** в `Packages/manifest.json`, `NpcBrain`, `Combat`, `SkillAnimationPlayer`, `NetworkTransform` без отдельного решения.
8. **Body swap M↔F** — обязательный тест: новый компонент должен подписаться на `CharacterCustomisationApplier.BodySwapped`, иначе female-модель получит T-pose/старый Animator.
9. **Combat не трогаем:** `OnAttackImpact` и hit window остаются authored; neural-позы не используются для хитбоксов.
10. **Merge только после go/no-go** и подтверждения пользователя; до этого — отдельная ветка/worktree.

---

## 1. Гарантии, которые мы даём

| Гарантия | Как обеспечивается |
|---|---|
| Текущая анимация работает как раньше | `Mode=Disabled` по умолчанию; новые компоненты не активны; baseline-хэши |
| Нет модели в билде | Baked-путь; модель только в `C:\Projects\motion_bricks` |
| Нет изменений `manifest.json` | Sentis не устанавливается в main; tarball лежит отдельно |
| Нет изменений боевой логики | `SkillAnimationPlayer`, `NpcBrain`, `CombatServer` не редактируются |
| Нет изменений сети | Кости не реплицируются; `NetworkTransform` не трогаем |
| Нет изменений FO | Новые компоненты работают в local space; target-позы не в мировых координатах |
| Откат без миграций | Нет изменений save-формата/сцен/префабов на MVP; удаление компонента возвращает baseline |
| Проверяемость | `check_baseline.ps1` + `git diff` + A/B test matrix |

### 1.1. Факты из кода (проверено 2026-11-15)

- Runtime-контроллер игрока: `PlayerAnimation_Default.overrideController` → база `PlayerAnimation.controller`; female-вариант — `PlayerAnimation_Female.overrideController`.
- `CharacterCustomisationApplier` делает M↔F body swap и вызывает `BodySwapped(Animator)`; `SkillAnimationPlayer` уже подписан на это событие и кэширует `AnimatorOverrideController`.
- `NetworkPlayer`: `CharacterController`, owner-authority `NetworkTransform` (`Interpolate=false`), `applyRootMotion=0`; Animator — child `Visual_Model`.
- `NpcBrain`: server-side FSM, `NavMeshAgent`, `NetworkTransform`; Animator params `Speed`/`IsAttacking`/`IsGrounded`/`Attack`/`Skill`/`Death`/`Surrender`/`Work`/`IsSleeping`.
- `SkillAnimationPlayer`: state `Skill` + `SkillPlay` trigger + `OnAttackImpact` ~60%; нельзя ломать.
- В проекте **нет** общего feature-flag/settings-паттерна; `MotionBricksRuntimeSettings` создаётся отдельно.
- `com.unity.ai.inference` в `Packages/manifest.json` **не установлен**.

---

## 2. Режимы

```csharp
public enum MotionBricksMode
{
    Disabled = 0,          // baseline: только текущий Animator
    BakedClips = 1,        // играем запечённые .anim через отдельный playback
    NeuralExperimental = 2 // runtime-генерация, только owner player, dev
}
```

| Режим | Что активно | Что in build | Кому |
|---|---|---|---|
| `Disabled` | Штатный Animator/OverrideController | Ничего нового | Все, default |
| `BakedClips` | `MotionBricksBakedPlayback` + `.anim` | Только `.anim` | Все, после валидации |
| `NeuralExperimental` | `MotionBricksRuntime` + backend | Модель/плагин только в dev | Только owner player, Windows+NVIDIA |

**Переключение:** один ScriptableObject `MotionBricksRuntimeSettings` (или отсутствие ассета = `Disabled`). Никаких `#define` в коде, никаких пересборок.

---

## 3. Feature flag и gate

```csharp
[CreateAssetMenu(menuName = "ProjectC/Animation/MotionBricks Runtime Settings")]
public sealed class MotionBricksRuntimeSettings : ScriptableObject
{
    public MotionBricksMode mode = MotionBricksMode.Disabled;

    [Header("Scope")]
    public bool allowPlayer = true;
    public bool allowNpc = false;              // всегда false для MMO
    public bool allowRemotePlayers = false;    // всегда false
    public bool allowInCombat = false;         // всегда false на MVP

    [Header("Runtime")]
    public int maxInferenceMs = 100;
    public int failureBudget = 3;              // после N ошибок — circuit breaker
    public RuntimeAnimatorController fallbackController; // null = текущий
}
```

```csharp
public static class MotionBricksRuntimeGate
{
    public static bool IsNeuralAllowed(MotionBricksRuntimeSettings s, NetworkPlayer player)
    {
        if (s == null || s.mode != MotionBricksMode.NeuralExperimental) return false;
        if (!s.allowPlayer || player == null || !player.IsOwner) return false;
        if (Application.platform != RuntimePlatform.WindowsPlayer) return false;
        if (!SystemInfo.graphicsDeviceVendor.ToLowerInvariant().Contains("nvidia")) return false;
        // if (!s.allowInCombat && IsInCombat(player)) return false; // подключить ваш combat-check
        return true;
    }
}
```

**Правила:**
- Нет ассета настроек → `Disabled` (fail-closed).
- `NeuralExperimental` не включается в build по умолчанию; только dev/cheat.
- `allowNpc = false`, `allowRemotePlayers = false` — жёстко.
- При `maxInferenceMs`/`failureBudget` превышении — `Disabled` на сессию + лог.

---

## 4. Изоляция ассетов

### 4.1. Новые папки (не трогают существующие)

```
Assets/_Project/Animations/Generated/MotionBricks/
  <PrimitiveId>/<BodyType>/*.anim
  <PrimitiveId>/<BodyType>/*Metadata.asset
Assets/_Project/Animations/Generated/MotionBricks/Authoring/
  MotionBricksPrimitiveAuthoring.cs (если нужен MonoBehaviour)
Assets/_Project/Settings/MotionBricks/
  MotionBricksRuntimeSettings.asset (создаётся в Unity, не руками)
```

- `.meta` создаёт Unity; **не писать руками**.
- Новые `.anim` не перезаписывают существующие.
- `PlayerAnimation.controller` и override-контроллеры **не трогаем** на MVP.
- `NetworkPlayer.prefab` **не трогаем** на MVP; тест — на variant/scene-объекте.

### 4.2. Как играть baked-клип, не ломая контроллер

**Важно:** `Animator.CrossFade("clipName")` не умеет играть произвольный `AnimationClip` — только состояние контроллера. Варианты:

| Вариант | Что делает | Риск | Когда |
|---|---|---|---|
| **A. `PlayableGraph` + `AnimationClipPlayable`** | Отдельный граф, `AnimationPlayableOutput` на Animator; играет клип поверх; при остановке `graph.Destroy()` + `Animator.Rebind()` | Средний: нужно правильно остановить/восстановить | **MVP** |
| **B. `Animancer`** | Play-on-demand, без правок контроллера | Платный пакет; ещё одна зависимость | Если уже есть/одобрен |
| **C. `AnimatorOverrideController` + отдельный state** | Один state `MotionBricks` + override клипа | Требует правки/дублирования контроллера | После MVP |
| **D. Дублированный контроллер** | `PlayerAnimation_MotionBricks.controller` + M/F overrides | Дублирование, body swap | После MVP, для A/B |

**Рекомендация MVP:** **A. `PlayableGraph`** в отдельном компоненте:
```csharp
_graph = PlayableGraph.Create("MotionBricksBaked");
var clipPlayable = AnimationClipPlayable.Create(_graph, clip);
var output = AnimationPlayableOutput.Create(_graph, "MotionBricks", _animator);
output.SetSourcePlayable(clipPlayable);
_graph.Play();
// stop:
_graph.Stop(); _graph.Destroy(); _animator.Rebind();
```
- Не менять `runtimeAnimatorController`.
- Не вызывать `Animator.Update` вручную без необходимости.
- На `OnDisable`/`BodySwapped` — stop + destroy + rebind.
- Если `SkillAnimationPlayer` активен — не запускать playback (gate).

### 4.3. Если всё-таки нужен state в контроллере

Только после T-MB02/03:
1. Дублировать `PlayerAnimation.controller` → `PlayerAnimation_MotionBricks.controller`.
2. Дублировать M/F override controllers.
3. Добавить **один** state `MotionBricks` + параметр/триггер; не менять существующие состояния/переходы.
4. Переключать набор контроллеров через `MotionBricksRuntimeSettings`; body swap должен выбирать M/F вариант.
5. Baseline-контроллеры оставить нетронутыми; откат — переключить настройку/удалить дубликаты.

---

## 5. Изоляция кода

| Новый файл | Назначение |
|---|---|
| `MotionBricksRuntimeSettings.cs` | Feature flag/режим |
| `MotionBricksBakedPlayback.cs` | Baked playback (PlayableGraph) |
| `MotionBricksBakedRootMotion.cs` | Hips delta → `CharacterController.Move` (если root motion) |
| `MotionBricksRuntime.cs` | Только experimental: scheduler/state |
| `MotionBricksPoseApplier.cs` | Только experimental: HumanPose/AnimationScriptPlayable |
| `SomaHumanoidRetargeter.cs` | Editor/runtime retarget |
| `Editor/MotionBricksBakerWindow.cs` | Editor baker |

**Правила:**
- Namespace `ProjectC.Animation.MotionBricks`.
- **Не редактировать** `NetworkPlayer.cs`, `NpcBrain.cs`, `SkillAnimationPlayer.cs`, `CharacterCustomisationApplier.cs` на MVP.
- Если нужен hook — сначала отдельный компонент, который находит `Animator`/`CharacterCustomisationApplier` через `GetComponent`/`GetComponentInChildren`.
- Body swap:
  ```csharp
  var applier = GetComponent<CharacterCustomisationApplier>();
  if (applier != null) applier.BodySwapped += OnBodySwapped;
  // OnBodySwapped: StopPlayback(); _animator = newAnimator; RebuildBoneMap();
  //                  ReapplyCurrentClipOrFallback();
  ```
- FO: если компонент кэширует мировые позиции — `IGlobalMotionActorParticipant`/`GlobalMotionActorLink` + `ApplyRebaseTranslation`; для local-space playback не нужно.
- Не писать `.asmdef` руками; если нужен — отдельный план.

---

## 6. Lifecycle и fallback

### 6.1. Включение

```
Awake/OnEnable:
  if (settings == null || settings.mode == Disabled) { enabled = false; return; }
  if (!gate) { enabled = false; return; }
  resolve Animator, applier, controller
  subscribe BodySwapped
  start playback/generation
```

### 6.2. Выключение (`OnDisable`/`OnDestroy`)

```
stop requests (CancellationToken)
stop generation loop
if (_graph.IsValid()) { _graph.Stop(); _graph.Destroy(); }
_animator.Rebind();
_animator.Update(0f);
unsubscribe BodySwapped
log once: "MotionBricks disabled; baseline Animator restored."
```

### 6.3. Ошибки/таймауты

- Backend timeout/exception → `FallbackToBaseline()`:
  - stop graph;
  - `_animator.Rebind()`;
  - crossfade to baseline state/clip;
  - increment failure counter; after `failureBudget` → mode `Disabled` for session.
- Baked clip missing → baseline state; log.
- Body swap during playback → stop, rebind new animator, reapply clip if valid, else baseline.
- Combat starts → if `allowInCombat=false`, stop neural, baseline; baked playback can continue if clip is non-combat? Gate by state.

### 6.4. «Kill switch» в рантайме

- `MotionBricksRuntimeSettings.mode = Disabled` (Editor/cheat) → все компоненты на следующем `OnEnable`/update cycle останавливаются.
- Дополнительно: `MotionBricksRuntime.KillSwitch()` — статический флаг, который вызывает `Stop` у всех экземпляров; сбрасывается только рестартом.
- В build `Disabled` по умолчанию; включение — только dev/cheat.

---

## 7. Безопасный порядок работ

### 7.1. Worktree / branch

```powershell
cd C:\Projects\ProjectC_client
git worktree add C:\Projects\ProjectC_client_motionbricks -b feature/motionbricks
cd C:\Projects\ProjectC_client_motionbricks
```

- Main/worktree с пользовательскими изменениями не трогаем.
- **Worktree — только код и docs.** Контент (`*.prefab`, `*.controller`, `*.anim`, сцены, FBX) в git **не хранится** (Lore VCS), поэтому в worktree его нет; открывать Unity и работать с ассетами нужно в `C:\Projects\ProjectC_client` (main).
- Baseline-хэши анимационных файлов в worktree проверить нельзя — используйте `check_baseline.ps1 -SkipPackages -SkipContent` (проверяются только скрипты); контентный baseline проверяйте в main.
- Пакеты (`Packages/*`) в worktree могут отличаться от working-tree snapshot в main — `-SkipPackages`; пакеты проверяйте в main отдельно.
- Если работа идёт в main — **стоп**; сначала worktree для кода/docs.

### 7.2. T-MB02 (spike, без изменений prefab/controller)

1. `check_baseline.ps1` → PASS (в чистом worktree — с `-SkipPackages`).
2. Испечь **один** клип через CPU CLI/ONNX (см. [05](05_LOCAL_RESOURCES.md)).
3. Импортировать `.anim` в `Generated/MotionBricks/`.
4. На **scene-объекте** (не prefab) добавить `MotionBricksBakedPlayback`; `Animator` — текущий.
5. В Play Mode (пользователь) проиграть клип; проверить:
   - клип играет;
   - после stop — baseline Animator;

### 7.3. T-MB03/04/05

- Только после PASS T-MB02.
- Baked clips — в `Generated/`; контроллеры/префабы не менять, пока не подтверждено.
- Hybrid primitives — отдельные scene-объекты/якоря; не трогать существующие.
- При необходимости controller state — только дублированный контроллер (см. §4.3).

### 7.4. T-MB06/07 (runtime)

- Только при явном go.
- `MotionBricksRuntimeSettings` default `Disabled`.
- Sentis — в отдельной ветке/копии проекта; конфликт версий Burst/Collections/Newtonsoft.
- Runtime — только owner player, не NPC, не combat, не remote.
- Перед включением — baseline check + A/B.

---

### 7.5. Важное про `Animator.CrossFade`

Не пытаться играть произвольный `.anim` через `Animator.CrossFade("clip")`: это состояние контроллера, а не клип. Для MVP — `PlayableGraph`/`AnimationClipPlayable` (см. §4.2). Это предотвращает необходимость правок контроллера.

---

## 8. Уровни отката

| Уровень | Действие | Время | Риск | Что остаётся |
|---|---|---|---|---|
| **0. Выключатель** | `MotionBricksRuntimeSettings.mode = Disabled` | секунды | Низкий | Компоненты есть, но не активны; baseline работает |
| **1. Удалить playback** | Убрать `MotionBricksBakedPlayback`/`MotionBricksRuntime` со scene/prefab | минуты | Низкий | Сгенерированные клипы лежат, не используются |
| **2. Удалить ассеты** | Удалить `Assets/_Project/Animations/Generated/MotionBricks/` | минуты | Низкий | Контроллеры/префабы не тронуты |
| **3. Revert кода** | `git revert` коммитов интеграции в feature-ветке | <1 ч | Средний | Baseline-ассеты не менялись; revert чистый |
| **4. Выбросить worktree** | Удалить worktree/ветку; main не тронут | минуты | Низкий | Всё как до интеграции |

**Если всё-таки правили controller/prefab:**
- `check_baseline.ps1` покажет `CHANGED`;
- `git diff` — вручную;
- восстановить из baseline-коммита/хэшей **только** эти файлы:
  ```powershell
  git checkout HEAD -- Assets/_Project/Animations/PlayerAnimation.controller
  # и т.д. — только после проверки, что нет пользовательских правок в этих файлах
  ```
- **Не** делать `git checkout -- .` / `git reset --hard` — в main есть пользовательские изменения.

---

## 9. Тест-матрица

### 9.1. Baseline regression (до и после каждого этапа)

| # | Сценарий | Ожидание |
|---|---|---|
| 1 | Idle → Walk → Run → Sprint → Stop | Плавно, без T-pose |
| 2 | Jump / Land | Без рывков, приземление |
| 3 | TurnLeft / TurnRight | Корректный разворот |
| 4 | Attack1H / Skill | `OnAttackImpact` срабатывает; hit window не изменён |
| 5 | Gather / Lumber / Mining | Триггеры/state работают |
| 6 | Damage / Death / Respawn | Без залипания Animator |
| 7 | M→F→M body swap | Модель/контроллер меняются, анимация не ломается |
| 8 | Equipment (weapon/cloak) | Кости/сокеты на месте |
| 9 | NPC Goblin: idle/chase/attack/death/surrender/work/sleep | Параметры Animator работают |
| 10 | Network 2 clients | Позиции/анимации совпадают |
| 11 | F8 / F9 | Объект на месте; 0 errors |
| 12 | 30 min soak | Нет утечек/деградации |

### 9.2. A/B (baseline vs baked vs neural)

- Один сценарий/сейв/ввод; запись видео; сравнение:
  - foot slide ≤ 2 см/кадр;
  - penetration ≤ 2 см;
  - нет T-pose/попа;
  - equipment на месте;
  - `OnAttackImpact` не сдвинут;
  - frame time не хуже baseline + 2 ms;
  - GC 0 B/frame в steady state.

### 9.3. Abort criteria

Остановить интеграцию, если:
- любой baseline regression;
- T-pose после disable/body swap;
- `OnAttackImpact` не срабатывает/сдвинут;
- foot slide/penetration выше порогов;
- FPS/GC/память деградировали;
- Sentis import/package conflict не решён;
- пользователь не подтвердил визуально.

---

## 10. Merge / go-no-go

- До go/no-go — **не мержить** в main; работать в `feature/motionbricks`.
- Merge только после:
  1. baseline check PASS;
  2. T-MB02/03/04/05 PASS;
  3. A/B и abort criteria;
  4. пользователь подтвердил визуально;
  5. `Mode=Disabled` по умолчанию в build.
- После merge — обновить `baseline_hashes.tsv` **только** для намеренно изменённых файлов, отдельным коммитом.

---

## 11. Абсолютные ссылки

| Что | Путь |
|---|---|
| Baseline manifest | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\BASELINE_MANIFEST.md` |
| Baseline hashes | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\baseline_hashes.tsv` |
| Baseline check | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |
| Pipeline | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\02_PIPELINE.md` |
| Offline resources | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\05_LOCAL_RESOURCES.md` |
| Handoff | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\HANDOFF.md` |
| Animator controller | `C:\Projects\ProjectC_client\Assets\_Project\Animations\PlayerAnimation.controller` |
| Player prefab | `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\NetworkPlayer.prefab` |
| Player script | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\NetworkPlayer.cs` |
| Body swap | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\CharacterCustomisationApplier.cs` |
| Skill animation | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Skills\SkillAnimationPlayer.cs` |
| NPC brain | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\AI\NpcBrain.cs` |
| MotionBricks repo | `C:\Projects\motion_bricks\motionbricks` |
| CPU CLI | `C:\Projects\motion_bricks\community\sa-inbetween\releases\v0.9.4\tools-win-x64\SaInbetween` |
| CPU venv | `C:\Projects\motion_bricks\tools\cpu_baker\.venv` |
| TensorRT venv | `C:\Projects\motion_bricks\motionbricks\.venv` |

---

*Связанные: [02_PIPELINE.md](02_PIPELINE.md) · [05_LOCAL_RESOURCES.md](05_LOCAL_RESOURCES.md) · [baseline/BASELINE_MANIFEST.md](baseline/BASELINE_MANIFEST.md) · [HANDOFF.md](HANDOFF.md)*
