# T-MB01 — Пайплайн интеграции MotionBricks в Project C

> **Дата:** 2026-11-15
> **Статус:** design proposal; код не писался
> **Область:** Unity 6.5 (`6000.5.2f1`), Windows x64, URP 17.5, NGO 2.13
> **Главный принцип:** neural-анимация — **косметика и authoring**, не gameplay-authority. Всё, что влияет на бой, сеть и FO, остаётся детерминированным и authored.
> **Offline:** все команды ниже используют локальный пакет `C:\Projects\motion_bricks`; ссылки на скачивание оставлены как происхождение. См. [05_LOCAL_RESOURCES.md](05_LOCAL_RESOURCES.md).
> **Safety:** перед любыми изменениями — [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md): worktree, baseline check, feature flag, откат.

---

## 0. Принципы

1. **Не тащить модель в MMO-клиент.** 758 MB FP32 / 380 MB FP16 + 200M params — не тот бюджет.
2. **Baked clips — основной ship-путь.** Генерация offline, playback через существующий Animator/Animancer.
3. **Runtime-генерация — только для одного игрока, за feature flag, с fallback.** Никогда для NPC/толпы.
4. **Combat не трогаем.** `T-INP-08`: урон завязан на Animation Event `OnAttackImpact`; neural-позы не гарантируют тайминг.
5. **Сеть не реплицирует кости.** Реплицируем состояние; клиент играет клип.
6. **FO:** target-позы — в local space; мировые координаты не кэшируем.
7. **Никаких ручных `.meta`/`.asmdef`.** Если нужен asmdef — отдельный план с одобрения.
8. **Baseline неприкосновенен:** `PlayerAnimation.controller`, `NetworkPlayer.prefab`, NPC-контроллеры, `SkillAnimationPlayer` не меняем на MVP; работа — в worktree; `check_baseline.ps1` до/после каждого этапа.

---

## 1. Целевая архитектура

### 1.1. Offline (ship)

```
┌─────────────────────────────┐
│ MotionBricksPrimitiveAuthoring │  (scene: entry/targets/exit/box)
└──────────────┬──────────────┘
               │ Bake
               ▼
┌─────────────────────────────┐
│ MotionBricksBaker (Editor)  │
│  - HTTP → Python/TensorRT   │
│  - или ONNX Runtime CPU     │
└──────────────┬──────────────┘
               │ SOMA poses (JSON)
               ▼
┌─────────────────────────────┐
│ SomaHumanoidRetargeter      │
│  - mirror, rest delta, IK   │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ .anim + metadata + report   │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ Animator / Animancer        │
│  - PlayerAnimation.controller│
│  - NPC controller           │
│  - AnimatorOverrideController│
└─────────────────────────────┘
```

### 1.2. Runtime experiment (optional, dev only)

```
┌──────────────┐   targets    ┌────────────────────┐
│ Gameplay     │─────────────▶│ MotionBricksRuntime│
│ (player only)│              │  scheduler/state   │
└──────────────┘              └─────────┬──────────┘
                                        │ IMotionBricksBackend
                          ┌─────────────┼─────────────┐
                          ▼             ▼             ▼
                       Sentis       ONNX Runtime   HTTP (dev)
                          │             │             │
                          └─────────────┴─────────────┘
                                        │ SOMA frames
                                        ▼
                              ┌────────────────────┐
                              │ PoseApplier        │
                              │ HumanPose/Playable │
                              │ + foot IK          │
                              └─────────┬──────────┘
                                        ▼
                              Animator / bones / CC
```

---

## 2. Фаза 0 — аудит (1–2 дня)

**Цель:** понять, что вообще генерировать. Без этого baker — игрушка.

| Задача | Результат |
|---|---|
| Инвентаризация клипов | Таблица: какие состояния покрыты, какие переходы отсутствуют |
| Инвентаризация контроллеров | `PlayerAnimation.controller`, NPC controller, `AnimatorOverrideController` |
| Бюджет | Max build size, max memory, min GPU, target FPS |
| Список болей | «нет turn-in-place», «нет injured walk», «нет vault», «нет sit/stand» и т.п. |
| Решение | Стоит ли вообще baker; какие 3–5 клипов дадут эффект |

**Критерий выхода:** есть список 3–5 конкретных клипов, которые нельзя быстро сделать из существующих ассетов.

---

## 3. Фаза 1 — offline baker (рекомендуется)

### 3.1. Компоненты

| Компонент | Где | Назначение |
|---|---|---|
| `MotionBricksPrimitiveAuthoring` | Runtime/MonoBehaviour | Авторинг: entry, targets, exit, box, style, body type |
| `MotionBricksBakeSettings` | Editor/ScriptableObject | URL/CLI, модель, precision, output, curve mode, root motion, thresholds |
| `MotionBricksBakerWindow` | Editor | Список примитивов, выбор, Bake, прогресс, отчёт |
| `MotionBricksBaker` | Editor | Оркестрация: собрать позы → backend → ретаргет → кривые → метаданные |
| `IMotionBricksBackend` | Runtime/Editor | HTTP / ONNX Runtime / CLI |
| `SomaHumanoidRetargeter` | Runtime | SOMA → Humanoid, mirror, rest delta, height |
| `MotionBricksClipValidator` | Editor | Длина, стыки, foot slide, penetration, quaternion continuity |
| `MotionBricksClipMetadata` | ScriptableObject | Модель, hash, precision, backend, дата, validation |
| `MotionBricksClipPlayer` | Runtime | Playback baked-клипа, root motion, fade |
| `BakedRootMotionDriver` | Runtime | Hips delta → `CharacterController.Move` |

### 3.2. Workflow аниматора

1. Поставить на сцену `MotionBricksPrimitiveAuthoring`.
2. Задать `primitiveId`, тип, стиль, body type.
3. Расставить `entry`, 1–4 `target`, `exit`; настроить `interactionBox`.
4. Нажать **Bake** в `MotionBricksBakerWindow`.
5. Baker:
   - снимает контекст (4 позы) с текущего персонажа/клипа;
   - отправляет target-позы в backend;
   - получает 18–58 кадров;
   - ретаргетит на Humanoid;
   - пишет `.anim` + metadata + validation report;
   - показывает отчёт (pass/fail, метрики).
6. Аниматор проигрывает клип в Preview, правит target-позы, перепекает.
7. Клип попадает в контроллер/`AnimatorOverrideController`/`SkillNodeConfig`.

### 3.3. Данные авторинга

```csharp
namespace ProjectC.Animation.MotionBricks
{
    public enum MotionBricksBodyType { Male, Female, Neutral }

    public enum MotionBricksPrimitiveKind
    {
        Locomotion,        // walk/run/strafe/stop/turn
        Traversal,         // vault, jump, climb, fall
        ObjectInteraction, // sit, stand, pick-up, use
        IdleVariant        // injured, stealth, zombie, social
    }

    public enum MotionBricksStyle
    {
        Normal, Injured, Stealth, Zombie, Strafing, Custom
    }
}
```

```csharp
using UnityEngine;

namespace ProjectC.Animation.MotionBricks
{
    /// <summary>
    /// Сцена-авторинг одного Smart Primitive. Не хранит мировые координаты:
    /// entry/targets/exit — Transform-якоря под корнем сцены.
    /// </summary>
    public sealed class MotionBricksPrimitiveAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        public string primitiveId = "new_primitive";
        public MotionBricksPrimitiveKind kind = MotionBricksPrimitiveKind.ObjectInteraction;
        public MotionBricksStyle style = MotionBricksStyle.Normal;
        public MotionBricksBodyType bodyType = MotionBricksBodyType.Male;

        [Header("Poses (1-4 targets)")]
        public Transform entry;
        public Transform[] targets = new Transform[4];
        public Transform exit;

        [Header("Interaction")]
        public Vector3 interactionBoxCenter;
        public Vector3 interactionBoxSize = new Vector3(1.5f, 2f, 1.5f);

        [Header("Bake")]
        [Tooltip("Убрать горизонтальное смещение Hips; движение — кодом/NavMesh.")]
        public bool bakeInPlace = true;

        [Tooltip("Добавлять Animation Event OnAttackImpact. Для combat НЕ включать.")]
        public bool addAttackImpactEvent;
    }
}
```

**Правила:**
- Якоря — дети объекта под `WorldScene_*`; не хранить абсолютные мировые координаты.
- `targets` — 1–4; пустые игнорируются.
- `entry`/`exit` опциональны; если нет — берётся текущая поза/последняя target.
- `interactionBox` — локальный; гизмо в Editor.

### 3.4. Backend для baker

#### Вариант A — HTTP + официальный Python engine (лучшее качество)

- Python 3.12 + `uv`, официальный репозиторий, TensorRT 10.8, NVIDIA GPU.
- Сервер: `POST /generate` (см. community bridge) или прямой вызов `MotionBricksSOMAEngine`.
- Плюсы: официальный engine, endpoint correction, boundary trim, точное качество.
- Минусы: Python/CUDA/TensorRT на машине; первый запуск строит TensorRT plan (минуты); последовательная очередь.
- **Проверено 2026-11-15:** `generate_once.py --model soma` → 26 frames (0.87 s) + 50 frames (1.67 s), RTX 3090 24 GB, driver 610.88, CUDA 12.9.

Runbook (**offline**, всё уже скачано):
```powershell
# Официальный repo + LFS + .venv уже в C:\Projects\motion_bricks\motionbricks
cd C:\Projects\motion_bricks\motionbricks

# Проверка импорта/версии TensorRT
.venv\Scripts\python.exe -c "import motionbricks, tensorrt; print(motionbricks.__version__, tensorrt.__version__)"

# Headless-пример: 2 генерации, без viewer (план уже закэширован)
.venv\Scripts\python.exe docs\examples\generate_once.py --model soma

# Scene preview (нужен GL/viewer)
.venv\Scripts\python.exe -m app.scene_preview --scene assets/scenes/soma_gym.scene.json

# HTTP baker: community-реализация уже скачана
cd C:\Projects\motion_bricks\community\motionbricks-unity-bridge
# см. README репозитория; сервер запускается из venv official repo
```

Если официальный venv не собран/сломан — используйте CPU-путь (вариант B).

#### Вариант B — ONNX Runtime CPU (без NVIDIA)

- Модель уже локально: `C:\Projects\motion_bricks\motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp32.onnx` (758 MB), лицензия NVIDIA Open Model Agreement.
- Готовый venv: `C:\Projects\motion_bricks\tools\cpu_baker\.venv` (`onnxruntime 1.32.0`, `numpy`, `onnx`, `fastapi`).
- Наш замер: 73–77 ms N=1 на i7-8700; 252–279 ms N=8 (onnxruntime 1.32 дал 55–71 ms в повторном smoke-test).
- Плюсы: без CUDA; детерминированно; просто.
- Минусы: нет endpoint correction/boundary trim — реализовать самим; качество может отличаться от TensorRT; 758 MB модель.

Проверка:
```powershell
C:\Projects\motion_bricks\tools\cpu_baker\.venv\Scripts\python.exe `
  C:\Projects\motion_bricks\tools\cpu_baker\smoke_test.py `
  --model C:\Projects\motion_bricks\motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp32.onnx
```
Ожидаемо: `pred_global_motions (1, 64, 366)`, `pred_num_tokens [16]`, ~50–80 ms.

#### Вариант C — CLI `sa-inbetween` / .NET ORT

- Готовый CPU-инструмент, Apache 2.0 code.
- Локально: `C:\Projects\motion_bricks\community\sa-inbetween`; готовый `win-x64` + модель + `soma_rest.pose` — в `releases\v0.9.4\tools-win-x64\SaInbetween`; тестовые позы — в `releases\v0.9.4\library`.
- **Проверено:** `cli\sa-inbetween.exe generate` → 50 frames; `inspect` OK (`demo_walk_to_jog.saib`).
- Плюсы: не писать свой inference; есть `.saib`/`.pose`.
- Минусы: 77 joints, свой формат; нужен ретаргет/конвертация; неофициально; sample library — eval-only.

**Рекомендация:** A для качества, B для машин без NVIDIA; C — как референс.

### 3.5. Ретаргет и кривые

#### 3.5.1. Ретаргет

```csharp
public sealed class SomaHumanoidRetargeter
{
    // Настройки в инспекторе:
    // - mapping SOMA joint → HumanBodyBones
    // - rest poses (source/target)
    // - mirror axis (z→-z)
    // - height scale
    public void Apply(SomaPose pose, Animator animator);
}
```

Формула:
```
q_target_local = q_target_rest * inverse(q_source_rest) * q_source_local
```

Проверки:
- T-pose: руки/ноги/лицо;
- левая/правая сторона;
- высота: root scale;
- пальцы: остаются rest/last pose;
- лицо: Jaw/eyes — только если есть соответствующие bones.

#### 3.5.2. Кривые

| Режим | Что пишем | Плюсы | Минусы |
|---|---|---|---|
| `TransformCurves` | `localRotation`/`localPosition` каждой кости | Просто; точная поза; работает с Generic/Humanoid | Не ретаргетится между M/F; отдельный клип на body type |
| `HumanoidMuscleCurves` | `Animator` muscle curves + `RootT/RootQ` | Ретаргетится; меньше клипов | Сложнее; потеря деталей; нужен `HumanPoseHandler` |

**MVP:** `TransformCurves` для player body (M/F отдельно).
**Позже:** `HumanoidMuscleCurves` для NPC-вариантов.

#### 3.5.3. Root motion

- `bakeInPlace = true`: удалить горизонтальное смещение Hips; вертикаль оставить (прыжок).
- `bakeInPlace = false`: оставить Hips; playback через `BakedRootMotionDriver`.
- Для `CharacterController`: delta из Hips → `Move(delta)`.
- Для `NavMeshAgent`: in-place + speed match.
- Не включать `Animator.applyRootMotion` на `NetworkPlayer` (сейчас `0`).

### 3.6. Валидация

`MotionBricksClipValidator` проверяет:

| Проверка | Порог (стартовый) |
|---|---|
| Длина | 0.6–1.9 с (18–58 кадров @30 Hz) |
| Первый кадр vs context | position < 2 см, rotation < 5° |
| Последний кадр vs target | position < 5 см, rotation < 10° |
| Foot slide при contact > 0.5 | < 2 см/кадр |
| Ground penetration | < 2 см |
| Root drift (in-place) | < 5 см |
| Quaternion continuity | нет скачков > 30° между кадрами |
| Equipment sockets | Hips/Spine/Hands/Feet валидны |
| Loop seam (если loop) | position < 2 см, rotation < 5° |

Отчёт:
```csharp
[CreateAssetMenu(menuName = "ProjectC/Animation/MotionBricks Validation")]
public sealed class MotionBricksValidationReport : ScriptableObject
{
    public bool passed;
    public float durationSeconds;
    public float footSlideMaxCm;
    public float penetrationMaxCm;
    public float rootDriftCm;
    public string[] warnings;
}
```

**Важно:** пороги — стартовые; аниматор настраивает. Валидатор не заменяет глаза.

### 3.7. Выходные файлы и метаданные

```
Assets/_Project/Animations/Generated/MotionBricks/
  <PrimitiveId>/
    Male/
      <PrimitiveId>_<Style>_Male.anim
      <PrimitiveId>_<Style>_Male_Metadata.asset
    Female/
      ...
```

`MotionBricksClipMetadata`:
```csharp
[CreateAssetMenu(menuName = "ProjectC/Animation/MotionBricks Clip Metadata")]
public sealed class MotionBricksClipMetadata : ScriptableObject
{
    public string primitiveId;
    public string modelName;          // MotionBricks-SOMA-RP-v1
    public string onnxSha256;
    public string precision;          // fp32/fp16
    public string backend;            // tensorrt/onnxruntime
    public string bakeDateUtc;
    public int frameCount;
    public int sampleRate;            // 30
    public string rootMotion;         // inPlace/rootMotion
    public MotionBricksValidationReport validation;
    public string license;            // NVIDIA Open Model Agreement
}
```

Требования:
- `onnxSha256` — чтобы понимать, какой моделью спекли.
- `bakeDateUtc` — для воспроизводимости.
- `validation` — pass/fail.
- `license` — attribution.

### 3.8. Runtime playback

```csharp
public sealed class MotionBricksClipPlayer : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private bool _useRootMotion;

    public void Play(AnimationClip clip, float fadeSeconds = 0.2f)
    {
        // Animator.CrossFadeInFixedTime(clip.name, fadeSeconds);
        // или Animancer: _animancer.Play(clip, fadeSeconds);
    }
}
```

Для `CharacterController`:
```csharp
public sealed class BakedRootMotionDriver : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private string _hipsBone = "Hips";

    // В Update, после Animator:
    // delta = hips.position - lastHipsPosition; delta.y = 0;
    // _controller.Move(delta);
}
```

**Порядок:**
- `Animator` обновляется в `Update` (или `FixedUpdate` при `AnimatePhysics`);
- `BakedRootMotionDriver` — после Animator, до/вместе с `CharacterController.Move`;
- `Animation Rigging` — после Animator, но до финального рендера; следить, чтобы IK не перезаписывал root motion.

### 3.9. Бюджеты

| Параметр | Бюджет |
|---|---|
| Один клип | 18–58 кадров, 30 Hz; ~50 KB raw |
| 1000 клипов | ~50 MB raw; `.anim` меньше; приемлемо |
| Bake time | 75 ms–2.5 s на клип (CPU/GPU); 100 клипов — минуты |
| Runtime | 0 ms neural; только Animator/IK |
| Build | без модели; только `.anim` |

---

## 4. Фаза 2 — Hybrid Smart Primitives (без нейросети)

**Идея:** сохранить UX Smart Primitives (target-позы вокруг объекта, interaction box, sequence), но вместо генерации в runtime играть заранее испечённый клип.

### 4.1. Компоненты

| Компонент | Назначение |
|---|---|
| `MotionBricksPrimitiveAuthoring` | Уже есть; авторские якоря |
| `MotionBricksPrimitiveDatabase` | ScriptableObject: список примитивов, clip, box, style |
| `MotionBricksPrimitiveRegistry` | Сцена: находит примитивы рядом |
| `MotionBricksPrimitivePlayer` | Триггер: box overlap/interact → play clip |
| `MotionBricksIkPins` | Animation Rigging: кисти/стопы к якорям |

### 4.2. Runtime flow

1. Игрок/NPC входит в `interactionBox`.
2. `Registry` выбирает лучший примитив по углу/дистанции/стилю.
3. `Player` проверяет cooldown/state; запускает `Animator.CrossFade`/Animancer.
4. `IkPins` подтягивает кисти/стопы к target-якорям (опционально).
5. По окончании — возврат в locomotion.

### 4.3. Плюсы

- Детерминизм; работает на всех клиентах.
- Нет 758 MB модели.
- Можно использовать для NPC.
- Совместимо с `T-INP-08` (клип — обычный `AnimationClip`).
- UX «как Smart Primitives», но без neural.

### 4.4. Ограничения

- Клипы фиксированы; нет адаптации под угол/скорость.
- Для вариантов — печь несколько клипов (approach angle, style) или блендить.
- Всё равно нужен аниматор/валидация.

---

## 5. Фаза 3 — runtime experiment (optional, timebox)

### 5.1. Backends

| Backend | Плюсы | Минусы | Когда |
|---|---|---|---|
| `SentisMotionBricksBackend` | Всё в Unity; кросс-платформа; без Python | 758/380 MB; 6993 узла; latency неизвестна; импорт | Если нужен ship на Windows |
| `OnnxRuntimeMotionBricksBackend` | CPU/DirectML; быстрый CPU; контроль | Нативный plugin; DLL; 758 MB | Если Sentis плох, а runtime нужен |
| `HttpMotionBricksBackend` | Официальный engine; quality | Python/CUDA; сеть; не ship | Dev/сравнение |

**Offline Sentis:** tarball `C:\Projects\motion_bricks\unity\packages\com.unity.ai.inference-2.6.1.tgz` + зависимости (`burst 1.8.17`, `collections 2.4.3`, `newtonsoft 3.2.1`, `app-ui 1.3.6`). В проекте уже Burst 1.8.29 / Collections 6.5.0 / Newtonsoft 3.2.2 — возможен downgrade-конфликт; ставить только в отдельной ветке/копии проекта, не в main без решения. Подробно — [05_LOCAL_RESOURCES.md](05_LOCAL_RESOURCES.md) §5.

### 5.2. Scheduler

```csharp
public sealed class MotionBricksRuntime : MonoBehaviour
{
    // Состояния: Disabled, Locomotion, TargetSequence, Falling.
    // Один in-flight запрос; отмена устаревших.
    // Генерация при:
    //  - timeToTransition <= lookahead
    //  - target изменился (velocity/heading/style/primitive)
    //  - buffer low
    // Результат: кадры в буфер; sample по времени.
}
```

Правила:
- Не генерировать каждый кадр; только по событию/порогу.
- Не блокировать main thread; `Task`/coroutine/`ScheduleIterable`.
- Отменять устаревшие запросы (`CancellationToken`).
- Fallback: `Animator`/Animancer + clip, crossfade.
- Логировать p50/p95/fallback/VRAM.

### 5.3. Pose applier

- `HumanPoseHandler.SetHumanPose` — проще, Humanoid.
- `AnimationScriptPlayable` + `IAnimationJob` — чище, но сложнее.
- **Не** писать напрямую в `LateUpdate` поверх Animation Rigging: порядок/IK конфликтуют.
- Root delta — в `CharacterController.Move`; для NPC не применять.
- Foot IK — после позы, по contacts.

### 5.4. Feature flag

```csharp
[CreateAssetMenu(menuName = "ProjectC/Animation/MotionBricks Runtime Settings")]
public sealed class MotionBricksRuntimeSettings : ScriptableObject
{
    public bool enabled;
    public BackendKind backend;      // Off, Sentis, OnnxRuntime, Http
    public Object modelAsset;
    public int maxInferenceMs = 100;
    public bool allowInCombat;
    public bool allowForNpc;         // всегда false в MMO
    public bool allowForRemotePlayers; // false
}
```

> Каноническая схема режимов/полей — [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) §3; здесь — эскиз.

`MotionBricksRuntimeGate`:
- Windows x64;
- NVIDIA GPU (для TensorRT) или backend CPU;
- VRAM >= 8 GB (для Sentis/FP32);
- `IsOwner` player;
- not in combat (если `allowInCombat=false`);
- feature flag enabled;
- model loaded;
- and no fallback active.

### 5.5. Go/No-go критерии

| Критерий | Порог |
|---|---|
| Импорт Sentis | Успешно, без unsupported ops |
| p95 latency | ≤ 33 ms (30 FPS) или ≤ 50 ms (20 FPS) на целевом GPU |
| VRAM | ≤ 1.5 GB |
| Качество | Не хуже baked-клипов на 10 сценариях |
| Foot slide | ≤ 2 см/кадр |
| Penetration | ≤ 2 см |
| Стабильность | 30 мин soak, 0 errors/leaks |
| GC | 0 B/frame в steady state |
| Build size | Приемлемо для политики патчей |
| Legal | Attribution/NOTICE |

Если любой hard-fail → **stop**; остаёмся на baker.

---

## 6. Интеграция с существующими системами

### 6.1. Игрок

- `NetworkPlayer`: `CharacterController`, owner-authority `NetworkTransform` (`Interpolate=false`), `applyRootMotion=0`.
- Neural/baked root motion:
  - delta из Hips → `CharacterController.Move` в том же месте, где сейчас движение;
  - не дублировать `NetworkTransform`/`CharacterController`;
  - не включать `applyRootMotion`.
- Equipment: `Animator.GetBoneTransform` — Animator оставить включённым.
- Camera: Cinemachine не зависит от Animator; но jitter-толерантность проверить (T-JITTER).

### 6.2. NPC

- `NpcBrain`: server FSM, `NavMeshAgent`, `NetworkTransform`; клиентская анимация косметическая.
- Baked clips: добавить состояния/триггеры в NPC controller; `NpcBrain.UpdateAnimator` уже шлёт `Speed`/`IsAttacking`/`IsGrounded`.
- Runtime neural: **не использовать**; иначе root motion будет драться с `NavMeshAgent`/`NetworkTransform`.
- Если очень нужно — только in-place поза без root motion и speed match; но это не рекомендуется.

### 6.3. Combat

- `T-INP-08`: `SkillAnimationPlayer`, `AnimatorOverrideController`, Animation Event `OnAttackImpact` ~60%.
- Baked MotionBricks clips can be assigned to `SkillNodeConfig.attackClip` **только если**:
  - тайминг события расставлен вручную;
  - hit window проверен;
  - аниматор подтвердил.
- По умолчанию: **не генерировать combat**; использовать authored clips.
- Хитбоксы — серверные коллайдеры/капсулы, не кости.

### 6.4. Equipment

- `EquipSlotToBone` — `Animator.GetBoneTransform`; Animator включён.
- При ретаргете проверить Hips/Spine/Hands/Feet; иначе оружие/плащ отвалятся.
- Baked-клипы не должны менять rest pose/bone lengths.

### 6.5. Network

- Не реплицировать кости.
- Реплицировать: скорость, heading, style, primitive id, start time, phase.
- Remote players: интерполяция root + baked clip/Animator.
- Server: `NpcBrain`/`CombatServer` не знают о neural.
- Bandwidth: см. [01_RESEARCH.md](01_RESEARCH.md) §7.2.

### 6.6. Floating Origin

- Персонаж под `WorldScene_*`; root motion локальный.
- `MotionBricksPrimitiveAuthoring` якоря — под корнем сцены; local space.
- Не хранить абсолютные мировые координаты.
- Если runtime target-позы кэшируются — `ApplyRebaseTranslation` + регистрация.
- `NpcBrain`/`NetworkPlayer` уже `IGlobalMotionActorParticipant`; новые компоненты не ломают контракт.
- Проверка: F8 → объект на месте, 0 errors; F9 → возврат.

### 6.7. Debug

- `MotionBricksDebugView`:
  - ghost meshes: context (серый), target (синий), generated (зелёный), final (обычный);
  - gizmos: entry/targets/exit/box;
  - stats: backend, ms, tokens, frames, fallback, VRAM;
  - toggle in `MotionBricksRuntimeSettings`.
- Не включать в build по умолчанию.

### 6.8. Безопасность и откат

- `MotionBricksRuntimeSettings.mode = Disabled` по умолчанию; нет ассета — тоже `Disabled`.
- Baked playback — отдельный `PlayableGraph`/`AnimationClipPlayable`, **без правок** `PlayerAnimation.controller`.
- `OnDisable` → stop graph + `Animator.Rebind()`; baseline Animator продолжает.
- Body swap: подписка на `CharacterCustomisationApplier.BodySwapped`, пересборка bone map.
- Baseline check: `baseline/check_baseline.ps1` (12 файлов).
- Уровни отката: см. [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) §8.
- **Не мержить в main** до go/no-go; работать в `feature/motionbricks` worktree.

---

## 7. QA

### 7.1. Compile / tests

- Console → 0 errors, 0 warnings (или осознанные).
- EditMode tests:
  - retarget math (rest delta, mirror);
  - curve baking (transform vs muscle);
  - metadata hash;
  - validator thresholds;
  - root motion extraction.
- Тесты не запускает агент; пользователь — Test Runner.

### 7.2. Manual matrix (пользователь)

| # | Сценарий | Ожидание |
|---|---|---|
| 1 | Idle → walk → run → stop | Нет рывков; стопы не скользят |
| 2 | 180° turn | Нет разворота «спиной вперёд» |
| 3 | Slope/stairs | Стопы на поверхности; нет penetration |
| 4 | Vault/sit | Руки/стопы на якорях; возврат в locomotion |
| 5 | Equipment | Оружие/плащ на месте |
| 6 | Combat | `OnAttackImpact` срабатывает; хитбокс не зависит от позы |
| 7 | Network 2 clients | Позиции/анимации совпадают |
| 8 | F8/F9 | Объект на месте; 0 errors |
| 9 | 30 min soak | Нет утечек/деградации |
| 10 | Fallback | Отключение backend → Animator без T-pose |

### 7.3. Performance

- Profiler: `Animator`, `Animation Rigging`, `MotionBricksRuntime`, `CharacterController`.
- Память: AnimatorController, clips, model (если runtime), texture/VRAM.
- GC: 0 B/frame в steady state.
- Frame time: не хуже baseline + 2 ms (apply/IK).

---

## 8. Тикеты

| Тикет | Что | Зависимости |
|---|---|---|
| `T-MB01` | Research + decision (этот документ) | — |
| `T-MB01A` | Offline resource pack: repo/ONNX/HF/community/Sentis/venv в `C:\Projects\motion_bricks` | T-MB01 |
| `T-MB01B` | Safe rollout/rollback + baseline manifest + handoff | T-MB01A |
| `T-MB02` | Baker spike: один клип через HTTP/ORT, импорт, playback | T-MB01B |
| `T-MB03` | Baker MVP: Editor window, settings, metadata, validation | T-MB02 |
| `T-MB04` | Retarget/IK: SOMA→Humanoid, T-pose, foot IK, equipment | T-MB03 |
| `T-MB05` | Hybrid primitives: authoring, registry, playback | T-MB04 |
| `T-MB06` | Runtime spike: Sentis import + benchmark (timebox) | T-MB01 |
| `T-MB07` | Runtime experiment: scheduler/applier/flag (optional) | T-MB06 go |
| `T-MB08` | Go/No-go review | T-MB04/06/07 |
| `T-MB09` | Docs/rollout: обновить README, IMPLEMENTATION_PLAN/ITERATIONS | T-MB08 |

**Правило:** тикеты не создавать в трекере без одобрения; это план, не факт.

---

## 9. Чего НЕ делать

- ❌ Не добавлять `com.unity.ai.inference`/758 MB модель в `manifest.json` без одобрения.
- ❌ Не тащить TensorRT/CUDA в shipping build.
- ❌ Не заменять Animator у NPC.
- ❌ Не использовать neural-позы для хитбоксов/урона.
- ❌ Не генерировать combat-клипы без пересчёта Animation Events.
- ❌ Не хранить target-позы в абсолютных мировых координатах.
- ❌ Не писать `.meta`/`.asmdef` руками.
- ❌ Не запускать Play Mode/тесты агентом; это делает пользователь.

---

## 10. Rollout

0. **T-MB01B** — worktree + baseline check; без этого не начинать.
1. **T-MB02** — доказать, что один клип можно испечь и проиграть. Если нет — стоп.
2. **T-MB03/04** — tool + retarget; 3–5 клипов; аниматор подтверждает.
3. **T-MB05** — hybrid primitives; использовать в сцене.
4. **T-MB06** — Sentis spike; если p95/VRAM/качество плохие — закрыть runtime-ветку.
5. **T-MB07** — только при go; dev-build, feature flag, fallback.
6. **T-MB08/09** — решение, документация, коммит.

**Критерий успеха Phase 1:** аниматор делает переход/взаимодействие за < 30 минут и получает клип, который проходит валидацию и не ломает equipment/бой.

**Критерий отказа:** если baker не даёт качества лучше ручного клипа или требует больше времени, чем ручная анимация — не внедрять.

---

*Связанные: [README.md](README.md) · [01_RESEARCH.md](01_RESEARCH.md) · [03_ALTERNATIVES.md](03_ALTERNATIVES.md) · [04_SOURCES.md](04_SOURCES.md)*
