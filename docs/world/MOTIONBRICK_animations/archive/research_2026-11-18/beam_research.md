# BEAM_RESEARCH — Unity + MotionBricks: как MotionBricks встраивают в игры и что это даёт Project C

> **Дата:** 2026-11-18  
> **Тип:** только анализ / ресерч. Никаких изменений кода, сцен, ассетов и пакетов.  
> **Область:** Project C, Stage 2.5, host-client co-op до 4 игроков; Unity 6.5 (`6000.5.2f1`), URP, Windows x64.  
> **Тема:** NVIDIA MotionBricks (`NVlabs/motionbricks`, SIGGRAPH 2026, arXiv:2604.24833) и способы его встраивания в игры/Unity.  
> **Связанные документы:** `README.md`, `01_RESEARCH.md`, `03_ALTERNATIVES.md`, `14_OFFICIAL_VERTICAL_SLICE.md`, `16_OFFLINE_REJECTED_RUNTIME_NEXT.md`, `17_T-MB06_RUNTIME_SIDECAR.md`.

---

## 0. Короткий ответ

1. **MotionBricks — не «новый Animator».** Это нейросетевой генератор коротких кинематических переходов: на вход даются 4 последние позы + 1–4 целевые позы, на выходе получается 0.6–1.9 с движения на SOMA-скелете (30 суставов, 30 Hz). Высокоуровневое поведение, состояния, переходы, бой, equipment, сеть и hitbox-логика остаются снаружи.
2. **В играх его используют как слой motion generation, а не как замену всей анимационной системы.** Официальный UE5-демо: `StateTree` с тремя режимами (locomotion / target pose sequence / falling), Smart Primitives генерируют целевые ключевые позы, нейросеть заполняет движение между ними.
3. **В Unity есть три реальных пути интеграции:**
   - **Sentis / Unity Inference Engine** через пакет `tools.doji.motionbricks` и `.sentis`-модели (757/733 MB);
   - **ONNX Runtime Unity plugin** (пример `asus4/onnxruntime-unity-examples` — click-to-move на SOMA);
   - **TensorRT sidecar** (официальный Python runtime, NVIDIA-only) — у нас уже есть T-MB06.
4. **Для Project C главный полезный сценарий — offline motion factory / baker**, а не runtime-основа. Генерировать недостающие переходы, взаимодействия с объектами, стили locomotion, запекать в `.anim` и проигрывать через существующий `Animator` / `AnimatorOverrideController`.
5. **Если первичная цель — «более живые анимации персонажа»**, то MotionBricks **может помочь**, но не как «включить и сразу красиво». Он даёт возможность синтезировать inbetween-движение между авторскими/контекстными позами и целевыми позами. Качество, плавность и «жизнь» зависят от авторинга target-поз, IK, ретаргета, скелета и постобработки.
6. **Не стоит переводить проект на runtime MotionBricks как основу.** Модель 758 MB FP32 / 380 MB FP16, ~818 MiB VRAM, NVIDIA/TensorRT или тяжёлый ONNX, недетерминизм, SOMA 30 суставов без полноценных пальцев/мимики, foot skating, проблемы с сетью, боем и хитбоксами.
7. **Практическая рекомендация:** гибрид:
   - production-анимация и gameplay — через текущий `Animator` + authored clips + `AnimatorOverrideController` + IK;
   - MotionBricks — как генератор контента для конкретных недостающих переходов/взаимодействий, с запеканием и валидацией;
   - runtime sidecar — только как opt-in эксперимент для owner-игрока, с fallback, без влияния на бой/сеть.

---

## 1. Что именно такое MotionBricks

### 1.1. Суть

MotionBricks — проект NVIDIA Research (SIGGRAPH 2026), открытый репозиторий `NVlabs/motionbricks`. Это модульная латентная генеративная модель для real-time motion:

- обучена на большом корпусе motion capture;
- генерирует движение как inbetweening: по контекстным позам и целевым позам заполняет промежуточные кадры;
- использует Smart Primitives — небольшие авторские/процедурные целевые ключевые позы/правила, по которым модель понимает, куда и как двигаться;
- поддерживает навигацию, взаимодействия с объектами, падение и другие сценарии;
- работает в real time на NVIDIA GPU.

Ключевая идея: вместо огромного графа состояний с тысячами клипов и ручных переходов модель синтезирует движение между заданными позами.

### 1.2. Технические факты

| Параметр | Значение |
|---|---|
| Параметры модели | ~200M |
| Скелет | SOMA, 30 суставов |
| Частота | 30 Hz |
| Одна генерация | 24–64 сырых кадра → 18–58 валидных (~0.6–1.9 с) |
| Контекст | 4 позы |
| Цели | 1–4 позы |
| Вес ONNX FP32 / FP16 | 758 MB / 380 MB |
| VRAM (community) | ~818 MiB на RTX 5070 Ti |
| TensorRT latency | ~2.50 ms FP32 / ~1.28 ms FP16, N=1, RTX 4090 |
| CPU ONNX Runtime (наш замер) | 73–77 ms N=1; 146–157 ms N=4; 252–279 ms N=8 (i7-8700) |
| Лицензия | NVIDIA Open Model Agreement |
| Обучение | 313 526 клипов из корпуса ~350k / 700 ч |

### 1.3. Что модель предсказывает, а что нет

Предсказывает:

- локальные трансформы суставов;
- глобальную траекторию root;
- foot contacts (heel/toe L/R) как предсказание, но не физический foot lock.

Не предсказывает надёжно:

- полные пальцы;
- blendshapes / мимику;
- авторский тайминг удара;
- физику, коллизии, хитбоксы;
- детерминированный результат между разными GPU/точностями.

Model card прямо перечисляет foot skating/penetration как ограничения.

### 1.4. Лицензионные ограничения

- модели под NVIDIA Open Model Agreement;
- коммерческое использование возможно, но с условиями;
- sample data / demo motions могут быть eval-only;
- если шипить модель в клиент, нужно учитывать размер, распространение и attribution.

---

## 2. Как MotionBricks встраивают в игры

### 2.1. Официальная модель интеграции (UE5 demo)

Официальный демо-проект в Unreal Engine 5 показывает не «нейросеть вместо анимации», а архитектуру с разделением ответственности:

- **High-level behavior controller** — `StateTree` с тремя режимами:
  1. `locomotion` — навигационный примитив постоянно подаёт целевые ключевые позы на основе velocity, orientation, replanning frequency;
  2. `target pose sequence` — примитив взаимодействия с объектом подаёт целевые позы для выполнения действия;
  3. `falling` — динамически создаётся task actor во время выполнения.
- **Smart Primitives** — генерируют целевые keyframes: позицию, ориентацию, скорость, target poses.
- **Neural model** — заполняет движение между контекстом и целями.
- **Скелет / рендер** — обычный skeletal mesh, риг, скиннинг.

Важно: в демо заявлено «no foot-locking, no blending, no collision detection, no hand-authored transitions», но это демонстрационная техника, а не гарантия production-ready качества. В реальном проекте обычно нужен гибрид: IK, collision, gameplay events, authoring.

### 2.2. Общая схема интеграции в любую игру

Независимо от движка, интеграция выглядит так:

```text
Gameplay / AI / Input
        ↓
High-level state (locomotion / interaction / fall / combat)
        ↓
Smart Primitive / authoring layer
  - context poses (4 последних)
  - target poses (1-4)
  - flags: targetHasPose, min/max tokens
        ↓
MotionBricks inference
        ↓
Generated segment 0.6-1.9 s
        ↓
Pose application / retarget / IK / foot correction
        ↓
Skeletal mesh / animation system
```

То есть MotionBricks заменяет не всю анимационную систему, а **участок генерации движения между позами**.

### 2.3. Unity-пути интеграции

#### A. Unity Sentis / Unity Inference Engine + Doji package

Есть community/package-подход:

- HuggingFace `julienkay/MotionBricks-inference-engine` содержит `.sentis` и `.onnx` модели:
  - `motionbricks_soma_fp32.sentis` — 757 MB;
  - `motionbricks_unitree_g1_fp32.sentis` — 733 MB.
- Пакет `tools.doji.motionbricks` запускает модели через Unity Inference Engine.
- Модели скачиваются через AI Model Hub, не нужно вручную тащить их в проект.
- Использование в коде:

```csharp
using Doji.AI.MotionBricks;
using Unity.InferenceEngine;

Model model = await MotionBricksModels.LoadAsync(); // SOMA
```

Плюсы:

- самый «Unity-native» путь;
- модель импортируется как Sentis;
- GPU backend возможен;
- проще распространять как runtime-фичу, чем Python sidecar.

Минусы:

- модель очень большая;
- нужен кастомный mapper SOMA → Unity skeleton;
- нужен код применения поз к `SkinnedMeshRenderer` / bones;
- CPU backend медленнее;
- не решает ретаргет, IK, equipment, сеть, gameplay events.

#### B. ONNX Runtime Unity plugin (community example)

Репозиторий `asus4/onnxruntime-unity-examples` содержит пример `Assets/Samples/MotionBricks`:

- click-to-move демо;
- модель скачивается из pinned commit `NVlabs/motionbricks`;
- используется `InferenceSession` и ручной pre/post-processing;
- SOMA skeleton описан в `SomaSkeleton.cs`;
- генерация запускается на worker thread;
- есть context poses, target poses, walk/idle goals;
- есть ghost preview и scheduling следующей генерации.

Это важный пример того, как MotionBricks реально встраивают в Unity **без Sentis**: как runtime-генератор locomotion на SOMA-скелете. Но это демо, а не production-архитектура: нет боя, сети, NPC, equipment, hit events.

#### C. TensorRT sidecar / official runtime

Официальный runtime — Python + NVIDIA Warp + Newton + TensorRT. Для Unity это обычно:

- Python sidecar генерирует позы;
- Unity получает их по UDP/shared memory;
- Unity применяет позы к скелету.

Плюсы:

- максимальная производительность TensorRT;
- ближе к официальному API;
- меньше риска несовместимости операторов.

Минусы:

- NVIDIA-only;
- отдельный процесс;
- сложность деплоя;
- синхронизация, fallback, отладка;
- не подходит для массовых NPC.

У нас это уже реализовано как T-MB06 runtime sidecar для owner-игрока.

#### D. Offline baker / motion factory

Самый прагматичный путь:

- запускать модель вне Unity (Python/ONNX Runtime/TensorRT);
- генерировать переходы/взаимодействия;
- конвертировать SOMA-позы в Unity Humanoid `.anim`;
- импортировать в проект;
- проигрывать через `Animator` / `AnimatorOverrideController`;
- валидировать foot sliding, endpoint matching, equipment.

Плюсы:

- нет модели в билде;
- детерминированно;
- совместимо с gameplay, боем, сетью, Animation Events;
- работает на любом железе.

Минусы:

- не даёт «живую» генерацию под состояние игрока;
- требует ретаргет и валидацию;
- всё равно нужен authored content.

---

## 3. Что MotionBricks даст Project C, чего у нас не было

### 3.1. Потенциальная польза

| Что хотим | Как MotionBricks может помочь | Насколько реально для Project C |
|---|---|---|
| Более живые переходы locomotion | Генерация inbetween между idle/walk/run/turn/stop | ✅ Хорошо как offline-контент |
| Взаимодействия с объектами | Target pose sequence: sit/stand/vault/pick-up | ✅ Хорошо как offline или opt-in runtime |
| Стилизованные позы | Aim-позы + генерация движения к ним | ✅ Хорошо для прототипов/emotes |
| Меньше ручных клипов | Синтез недостающих переходов | 🟡 Частично; всё равно нужна валидация |
| Runtime-«живость» игрока | Непрерывная генерация locomotion из поз | 🟡 Только owner-player, opt-in, NVIDIA |
| Уникальные движения под состояние | Генерация не из библиотеки, а из target-поз | 🟡 Только если появится продуктовая фича |
| Уменьшение размера билда | Теоретически меньше клипов | ❌ Нет: модель 380–758 MB + authored clips всё равно нужны |

### 3.2. Что у нас уже есть и почему MotionBricks не заменяет это

Текущая система уже решает много задач:

- `PlayerAnimation.controller`: Idle / Walk_BlendTree / Sprint / Jump / Land / Attack1H / Skill / Gather / Lumber / TurnLeft/Right / Damage;
- blend trees для 8-направленной locomotion;
- `AnimatorOverrideController` для male/female и скиллов;
- `SkillAnimationPlayer` через `AnimatorOverrideController` + Animation Event `OnAttackImpact`;
- NPC через `NpcBrain` + `NavMeshAgent` + `NetworkTransform`;
- body swap и floating origin уже интегрированы в текущий Animator-пайплайн.

MotionBricks не даёт:

- Animation Events с точным таймингом;
- серверную авторитетность;
- детерминированность;
- совместимость с `NavMeshAgent` / `CharacterController` без дополнительной логики;
- поддержку equipment, пальцев, мимики;
- готовый gameplay-driven state machine.

---

## 4. MotionBricks vs наша цель: «более живые анимации»

### 4.1. Прямой ответ на вопрос

**Первичная цель — более живые анимации персонажа.** MotionBricks может помочь, но не автоматически.

Он подходит, если «живость» означает:

- более плавные переходы между позами;
- генерация движения к целевой позе;
- синтез interaction-движений (сесть, встать, vault, reach);
- меньше «мёртвых» стыков между клипами.

Он не подходит, если «живость» означает:

- улучшение качества authored combat;
- более точные hit reactions;
- лучшую мимику/пальцы;
- простое drop-in улучшение всех NPC;
- уменьшение ручной работы без авторинга target-поз.

### 4.2. Стоит ли «донастраивать наши анимации, генерируя и запекая переходы»?

**Да, но только как прицельный инструмент, а не как общая стратегия.**

Рекомендуемая позиция:

- **Не использовать MotionBricks для массовой генерации всех переходов.** У нас уже есть базовая locomotion-библиотека и blend trees; автоматическая генерация 22 переходов ранее была отклонена как не лучше исходного Animator и требующая ручной отладки.
- **Использовать MotionBricks для конкретных недостающих кусков:**
  - injured/stealth/strafing locomotion;
  - turn-in-place варианты;
  - подъём/спуск;
  - взаимодействия с объектами (vault, sit, pick-up);
  - emotes/pose-driven движения;
  - прототипирование стилей.
- **Запекать результат в `.anim`**, проигрывать через существующий Animator.
- **Не менять combat-клипы и Animation Events.** Боевой тайминг остаётся authored.

То есть ответ: **MotionBricks стоит использовать для генерации и запекания отдельных переходов/взаимодействий, но не как замену нашей анимационной системы и не как runtime-основу.**

---

## 5. Сравнение вариантов для Project C

| Вариант | Польза | Стоимость/риск | Вердикт |
|---|---|---|---|
| Текущий Animator + authored clips | Стабильно, gameplay-safe, Animation Events | Ручной авторинг, меньше «живости» | Основа production |
| Motion Matching (MxM / JLPM22 / Unity Motion Matching) | Хорошая runtime locomotion, без нейросети в клиенте | Нужна большая motion DB, настройка | Сильный кандидат для игрока |
| Animation Rigging IK | Улучшает контакты, aim, foot placement | Ограниченный эффект | Обязательно добавлять |
| Animancer (Playable API) | Более гибкий runtime control | Рефакторинг | Опционально |
| MotionBricks offline baker | Синтез недостающих переходов/взаимодействий | Ретаргет, валидация, ручная проверка | ✅ Рекомендуется как инструмент |
| MotionBricks runtime sidecar | «Живая» генерация для owner-player | NVIDIA-only, sidecar, fallback, недетерминизм | 🟡 Только opt-in эксперимент |
| MotionBricks runtime в клиенте (Sentis/ORT) | Runtime-генерация без sidecar | 758 MB, VRAM, CPU latency, кастомный mapper | ❌ Не для production сейчас |
| MotionBricks для NPC | Косметическая генерация | Server-auth mismatch, стоимость, память | ❌ Нет |

---

## 6. Что нужно, чтобы MotionBricks реально улучшил анимации

Минимальные условия:

1. **Авторинг target-поз.** Без хороших целевых поз модель не даст «живость», а даст случайные движения.
2. **Ретаргет SOMA → Unity Humanoid.** Проверить пропорции, T-pose, equipment attachment.
3. **Foot IK / ground correction.** Модель не гарантирует отсутствие foot sliding.
4. **Endpoint correction.** Начальный и конечный кадры должны совпадать с authored/context-позами.
5. **Валидация.** Foot sliding, penetration, equipment offset, root motion velocity.
6. **Fallback.** Если генерация не удалась — вернуться к authored Animator.
7. **Изоляция gameplay.** Боевые клипы, Animation Events, hit windows не генерировать.

---

## 7. Рекомендованный план для Project C

### Сейчас

- Не устанавливать Sentis/Inference Engine, если нет конкретной задачи.
- Не менять `Packages/manifest.json`.
- Не менять `PlayerAnimation.controller`, `NetworkPlayer.prefab`, `NpcBrain`, `SkillAnimationPlayer`.
- Использовать MotionBricks как внешний генератор контента, а не runtime-основу.

### Следующий шаг (если нужна «живость»)

1. Составить список из 5–10 конкретных анимационных задач, которые реально мешают:
   - отсутствующие переходы;
   - vault/sit/pick-up;
   - injured/stealth locomotion;
   - emotes.
2. Для каждой задачи решить: authored clip, motion matching, IK или MotionBricks.
3. Если выбран MotionBricks — сделать один offline vertical slice:
   - сгенерировать 1–3 клипа;
   - импортировать `.anim`;
   - проверить в Animation Window;
   - проверить foot sliding и equipment;
   - только потом подключать к controller.

### Runtime-эксперимент

Оставлять только как отдельный timeboxed spike:

- feature flag;
- fallback;
- только owner-player;
- без влияния на бой/сеть;
- замер latency, VRAM, FPS, качество.

---

## 8. Ограничения этого ресерча

- Play Mode не запускался; визуальное качество не оценивалось в рантайме.
- Источники — официальные docs/paper/repo, community-репозитории, локальные документы проекта.
- Некоторые community-интеграции не являются production-ready и могут быть несовместимы с Unity 6.5.
- Оценка «более живые анимации» субъективна; требуется ручная проверка пользователем.

---

## 9. Источники

### Официальные

- Project page: https://nvlabs.github.io/motionbricks/
- Paper: https://arxiv.org/abs/2604.24833
- PDF: https://research.nvidia.com/labs/gear/motionbricks/pdfs/motionbricks_siggraph_2026.pdf
- GitHub: https://github.com/NVlabs/motionbricks
- Hugging Face: https://huggingface.co/nvidia/MotionBricks
- SOMA model card: https://github.com/NVlabs/motionbricks/blob/main/docs/soma_model.md
- Runtime API: https://github.com/NVlabs/motionbricks/blob/main/docs/runtime_api.md
- ONNX manifest: https://github.com/NVlabs/motionbricks/blob/main/motionbricks/assets/onnx/soma/soma_n1.json
- NVIDIA Open Model Agreement: https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-open-model-agreement/

### Unity / community

- MotionBricks for Unity Inference Engine: https://huggingface.co/julienkay/MotionBricks-inference-engine
- ONNX Runtime Unity examples: https://github.com/asus4/onnxruntime-unity-examples
- MotionBricks sample in asus4 repo: `Assets/Samples/MotionBricks`
- Unity Inference Engine docs: https://docs.unity3d.com/Packages/com.unity.ai.inference@latest/
- TriPo3D article: https://www.tripo3d.ai/blog/ai-animation-unity-third-person-controller
- i-Scoop overview: https://www.i-scoop.eu/nvidia-motionbricks/

### Локальные документы проекта

- `docs/world/MOTIONBRICK_animations/README.md`
- `docs/world/MOTIONBRICK_animations/01_RESEARCH.md`
- `docs/world/MOTIONBRICK_animations/03_ALTERNATIVES.md`
- `docs/world/MOTIONBRICK_animations/14_OFFICIAL_VERTICAL_SLICE.md`
- `docs/world/MOTIONBRICK_animations/16_OFFLINE_REJECTED_RUNTIME_NEXT.md`
- `docs/world/MOTIONBRICK_animations/17_T-MB06_RUNTIME_SIDECAR.md`

---

## 10. Финальный вывод

MotionBricks — мощный генератор движения, но не магическая замена анимационной системы. В играх его правильно использовать как **motion generation layer** поверх authored/state-driven архитектуры, а не вместо неё.

Для Project C:

- **Для более живых анимаций:** использовать как offline-генератор конкретных переходов и взаимодействий, запекать в `.anim`, играть через существующий Animator.
- **Для runtime-эксперимента:** только owner-player, feature flag, fallback, без влияния на бой/сеть.
- **Не использовать:** как основу NPC, combat, hitboxes, массовой толпы или полной замены Animator.

Таким образом, ответ на исходный вопрос: **да, через MotionBricks можно донастраивать анимации, генерируя и запекая переходы, но это должен быть прицельный инструмент для конкретных задач, а не стратегия замены всей анимационной системы.**