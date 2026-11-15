# T-MB01 — MotionBricks: технический ресерч

> **Дата:** 2026-11-15
> **Связанные документы:** [README.md](README.md) · [02_PIPELINE.md](02_PIPELINE.md) · [03_ALTERNATIVES.md](03_ALTERNATIVES.md) · [04_SOURCES.md](04_SOURCES.md)
> **Метод:** чтение официальных docs/paper/ONNX-манифеста, статический аудит ONNX-графа, локальный CPU-бенчмарк ONNX Runtime, чтение кода Project C. Официальный TensorRT-движок и Sentis не запускались.

---

## 1. Что такое MotionBricks (по первоисточникам)

MotionBricks — исследовательский проект NVIDIA (SIGGRAPH 2026, ACM TOG 45(4), DOI `10.1145/3811334`). Это **не игровой ассет и не Unity-пакет**, а:
- обученная генеративная модель движения;
- Python-runtime для инференса (Warp / Newton / TensorRT);
- набор демо и редакторов Smart Primitives;
- ONNX-экспорт для интеграции в другие движки.

### 1.1. Заявленные результаты

| Метрика | Значение | Условия |
|---|---|---|
| Покрытие | ~350 000 клипов / 700 ч | корпус Bones RigPlay 1; обучение на 313 526 клипах |
| Параметры | ~200M | VQ-VAE 23.4M + pose 136.5M + root 40.4M |
| Частота | 30 Hz | вход/выход |
| Одна генерация | 24–64 сырых кадра → 18–58 валидных | 6–16 токенов по 4 кадра; ~0.6–1.9 с |
| Latency | ~2 ms | TensorRT, RTX 4090, модель без pre/post |
| Throughput | ~15 000 FPS | TensorRT, batch, синтетический benchmark |
| Скелет | SOMA, 30 суставов | плюс Unitree G1 (29 DoF) |

**Важно:** 15 000 FPS — это throughput модели на GPU в оптимизированном batch-сценарии, а не «15 000 FPS игры». В реальном приложении добавляются подготовка входов, декодирование, endpoint correction, ретаргет, IK и рендер.

### 1.2. Архитектура

Четыре уровня:

```
[Gameplay / Smart Primitive]
        │  target keyframes (1–4 позы)
        ▼
[Root module]  — предсказывает длительность и траекторию root
        │
        ▼
[Pose module]  — masked transformer, предсказывает токены позы
        │
        ▼
[Decoder / VQ-VAE] — токены → непрерывные позы, root, скорости, foot contacts
        │
        ▼
[Application] — ретаргет, IK, запись в кости, root motion
```

- **Smart Locomotion** — генерирует target-позы из velocity/heading/style; использует критически задемпфированную пружину для начальной траектории и нейронное уточнение root.
- **Smart Object** — target-позы вокруг объекта (сесть, vault, pick-up), interaction box, pivot, sequence.
- **Root module** — 40.4M параметров, transformer encoder; предсказывает число кадров и глобальную траекторию.
- **Pose module** — 136.5M параметров, 16 слоёв × 1024; masked token transformer.
- **Decoder** — 23.4M параметров, VQ-VAE с 5 головами по 16 кодов; декодирует токены в позы.
- **Tokenizer** — 4 кадра → 1 токен; 5 heads; обучение с VQ-loss.

### 1.3. Как работает генерация

1. Приложение даёт **4 последние позы** (context) и **1–4 target-позы**.
2. Root module предсказывает длительность `T` и траекторию root.
3. Pose module предсказывает токены позы вдоль траектории.
4. Decoder выдаёт непрерывные позы, root, скорости и foot contacts.
5. Приложение проигрывает сегмент, записывает показанные позы и использует последние 4 как context для следующей генерации.
6. Регенерация — при изменении ввода или когда буфер заканчивается.

Это **autoregressive chaining**, а не одна бесконечная генерация. Ошибки могут накапливаться; именно поэтому в UE5-демо есть отдельный recorder, который хранит реально показанные позы.

### 1.4. Что модель НЕ делает

- Не принимает геометрию сцены, коллизии, текст, теги, параметры оружия.
- Не гарантирует физическую осуществимость, контакт стоп, отсутствие penetration.
- Не предсказывает полные пальцы и мимику (blendshapes): только 30 суставов SOMA, включая Jaw/eyes и hand-end joints.
- Не заменяет высокоуровневое поведение: в UE5-демо есть StateTree (locomotion / target pose sequence / falling).
- Не является «обучением под вашу игру»: zero-shot работает в пределах training distribution; полноценного training pipeline в релизе нет.

---

## 2. ONNX-интерфейс

### 2.1. Файлы и размеры

| Файл | Размер | Примечание |
|---|---:|---|
| `mmm_v6_soma_fp32.onnx` | **757 864 273 B (~758 MB)** | FP32, официальный |
| `mmm_v6_soma_fp16.onnx` | **380 327 058 B (~380 MB)** | mixed FP16, входы/выходы FP32 |
| `mmm_v6_g1_fp32.onnx` | ~? | Unitree G1, 29 DoF |
| `mmm_v6_g1_fp16.onnx` | ~? | Unitree G1 |

Размеры FP32/FP16 SOMA измерены по `Content-Length` официальных LFS-файлов.

### 2.2. Входы (N = batch, обычно 1)

| Имя | Shape | Тип | Смысл |
|---|---|---|---|
| `global_root_values` | `[N, 8, 5]` | FP32 | root position XYZ + cos/sin heading |
| `has_global_root_values` | `[N, 8]` | INT32 | маска наличия |
| `local_root_values` | `[N, 8, 4]` | FP32 | heading rate, ground velocity X/Z, height |
| `has_local_root_values` | `[N, 8]` | INT32 | маска |
| `local_poses` | `[N, 8, 267]` | FP32 | 29 не-root позиций (87) + 30 6D-rotations (180) |
| `has_local_poses` | `[N, 8]` | INT32 | маска |
| `allowed_pred_num_tokens` | `[N, 11]` | INT32 | какие длины 6..16 разрешены |

Слоты 0–3 — context, 4–7 — target. Маски 0/1. Хотя бы одна длина должна быть разрешена.

### 2.3. Выходы

| Имя | Shape | Тип | Смысл |
|---|---|---|---|
| `pred_global_motions` | `[N, 64, 366]` | FP32 | на кадр: root 5 + joint positions 87 + 6D rotations 180 + velocities 90 + foot contacts 4 |
| `pred_num_tokens` | `[N]` | INT32 | выбранное число токенов |

Валидны первые `4 * pred_num_tokens` кадров. Сырой выход 24–64 кадра; официальный engine убирает по 3 boundary-кадра с каждого конца → 18–58 валидных.

### 2.4. Суставы SOMA (30)

```
Hips, Spine1, Spine2, Chest, Neck1, Neck2, Head, Jaw,
LeftEye, RightEye, LeftShoulder, LeftArm, LeftForeArm, LeftHand,
LeftHandThumbEnd, LeftHandMiddleEnd, RightShoulder, RightArm,
RightForeArm, RightHand, RightHandThumbEnd, RightHandMiddleEnd,
LeftLeg, LeftShin, LeftFoot, LeftToeBase,
RightLeg, RightShin, RightFoot, RightToeBase
```

Это **не** Unity Humanoid: нет полного набора пальцев, нет отдельной груди/ключиц в Unity-раскладке, есть Neck2 и hand-end joints. Нужен маппинг и ретаргет.

### 2.5. Пространства координат

- SOMA model space: **Y up**, XZ ground plane, meters.
- G1 model space: Z up, +X forward.
- Unity: Y up, **left-handed**.
- Community bridge использует mirror: position `z → -z`, quaternion `(x,y,z,w) → (-x,-y,z,w)`, self-inverse. Это стартовая гипотеза; проверять T-pose на каждом персонаже.

### 2.6. Foot contacts

4 значения на кадр: left heel, left toe, right heel, right toe; confidence 0..1. Это **не** IK и не гарантия контакта; это сигнал для foot-lock/IK и валидации.

---

## 3. Аудит ONNX-графа

### 3.1. Метод

- Скачан официальный `mmm_v6_soma_fp32.onnx` (758 MB).
- Разобран `onnx.ModelProto` (onnx 1.23.2).
- Посчитаны узлы, инициализаторы, типы операторов, атрибуты рисковых операторов.
- Отдельно запущен ONNX Runtime CPU (Python) на случайных/нулевых входах с валидными масками.

### 3.2. Структура

| Параметр | Значение |
|---|---:|
| IR version | 8 |
| Opset | 17 |
| Узлов | **6 993** |
| Initializers | 434 |
| Уникальных типов операторов | **51** |
| `Constant` | 2 480 |
| `Unsqueeze` | 872 |
| `Concat` | 447 |
| `Reshape` | 412 |
| `Shape` | 316 |
| `Gather` | 300 |
| `Mul` | 293 |
| `Add` | 259 |
| `Transpose` | 206 |
| `Slice` | 185 |
| `Cast` | 177 |
| `MatMul` | 148 |
| `ConstantOfShape` | 83 |
| `Where` | 75 |
| `Expand` | 72 |
| `Sqrt` | 72 |
| `Relu` | 69 |
| `Div` | 68 |
| `Conv` | 42 |
| `LayerNormalization` | 48 |
| `Softmax` | 24 |
| `Range` | 21 |
| `ScatterElements` | 16 |
| `ScatterND` | 10 |
| `TopK` | 1 |

Полный список 51 типа: `Abs, Add, And, ArgMax, Atan, Cast, Clip, Concat, Constant, ConstantOfShape, Conv, Cos, CumSum, Div, Equal, Expand, Flatten, Gather, GatherElements, Gemm, Greater, LayerNormalization, LeakyRelu, Less, MatMul, Max, Mod, Mul, Neg, Not, OneHot, Range, ReduceL2, ReduceSum, Relu, Reshape, Resize, ScatterElements, ScatterND, Shape, Sin, Slice, Softmax, Sqrt, Squeeze, Sub, Tile, TopK, Transpose, Unsqueeze, Where`.

### 3.3. Совместимость с Unity Sentis (статический анализ)

Проверено по [Supported ONNX operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/supported-operators.html) для Sentis 2.6.1:

| Оператор | Sentis | Комментарий |
|---|---|---|
| `Abs, Add, And, ArgMax, Atan, Cast, Clip, Concat, Constant, ConstantOfShape, Conv, Cos, CumSum, Div, Equal, Expand, Flatten, Gather, GatherElements, Gemm, Greater, LayerNormalization, LeakyRelu, Less, MatMul, Max, Mod, Mul, Neg, Not, OneHot, Range, ReduceL2, ReduceSum, Relu, Reshape, Shape, Sin, Slice, Softmax, Sqrt, Squeeze, Sub, Tile, Transpose, Unsqueeze, Where` | ✅ | Все есть в списке |
| `Resize` | ✅ | Модель использует `mode=nearest`, `coordinate_transformation_mode=asymmetric`; Sentis не поддерживает `half_pixel_symmetric`, но он не используется |
| `ScatterElements` | ✅ | Модель использует `axis=1/-1`, **без** `ScatterReductionMode`; Sentis GPU не поддерживает reduction mode, но он не нужен |
| `ScatterND` | ✅ | Поддерживается |
| `TopK` | ✅ | `axis=-1`, `largest=0`; Sentis GPUCompute поддерживает, GPUPixel — нет |
| `If`, `Loop`, `NonZero`, `Einsum`, `LSTM` | ❌ | **Не используются** — это главный риск, который снят |

**Вывод:** по типам операторов граф совместим с Sentis; блокеров уровня «оператор не поддерживается» не найдено. Но это **не** гарантия импорта: остаются динамические размерности, 6993 узла, 2480 Constant, оптимизация и производительность.

### 3.4. Риски Sentis

1. **Динамический batch.** Вход `batch_size` — dynamic. Для Sentis лучше зафиксировать `N=1` в import settings; иначе оптимизация хуже.
2. **Динамические выходные размерности.** `pred_global_motions` имеет dims `Concatpred_global_motions_dim_1/2`; Sentis должен вывести их через shape inference. Если не сможет — потребуется модификация ONNX (static shape) или functional API.
3. **6993 узла / 2480 Constant.** Импорт и оптимизация могут быть долгими; инференс может страдать от dispatch overhead. TensorRT фьюзит граф агрессивно, Sentis — нет.
4. **`MatMul`/`Gemm` на GPUCompute** могут идти через DirectML (Windows); на других платформах — fallback/медленнее. Проект Windows-only, это смягчает.
5. **FP16.** Mixed FP16 ONNX может импортироваться иначе; начинать с FP32.
6. **Память.** 758 MB weights + активации + Unity; на 6–8 GB GPU это заметно.

### 3.5. Локальный CPU-бенчмарк ONNX Runtime

| Параметр | Значение |
|---|---|
| CPU | Intel Core i7-8700, 6 cores / 12 threads |
| Runtime | ONNX Runtime CPU (Python), `ORT_ENABLE_ALL` |
| Модель | `mmm_v6_soma_fp32.onnx` (758 MB) |
| Входы | нулевые/rest-подобные позы, валидные маски, все длины 6..16 разрешены |
| Выход | `pred_num_tokens = 16` (максимум), `[1,64,366]` |
| N=1 | **73.4 / 77.3 / 77.3 ms** |
| N=4 | **146.3 / 156.2 / 156.9 ms** |
| N=8 | **251.5 / 255.0 / 279.3 ms** |

**Что это значит:**
- Граф **исполняется** на CPU, без NVIDIA, без TensorRT.
- 75 ms на генерацию — достаточно для offline baker (даже с запасом на ретаргет/валидацию).
- Для одного игрока в runtime 75 ms каждые ~1 с — ~7.5% CPU; теоретически возможно, но качество/память/размер модели всё равно проблемы.
- Для 100 NPC — неприемлемо.
- Это **не** оценка качества: входы случайные, без endpoint correction, без ретаргета; официальный engine не использовался.
- `sa-inbetween` сообщает ~5 с на 50 кадров CPU; наш замер быстрее. Разница может быть в железе, версии ORT, цепочке генераций, декодировании/IK. Не считать это противоречием; диапазон 75 ms–5 s зависит от условий.

---

## 4. Варианты интеграции в Unity

### 4.1. A. Offline baker (рекомендуется)

```
Unity Editor / CLI
   → HTTP/CLI → Python MotionBricks (TensorRT) или ONNX Runtime
   → SOMA-позы
   → ретаргет на Unity Humanoid
   → .anim (transform или muscle curves)
   → Animator / AnimatorOverrideController / Animancer
```

**Плюсы:** нет модели в билде; нет NVIDIA-зависимости у игроков; детерминированные клипы; работает на всех платформах; вписывается в `T-INP-08`.
**Минусы:** клипы фиксированы; нет runtime-адаптации; нужен пайплайн ретаргета/валидации.

### 4.2. B. Unity Sentis (`com.unity.ai.inference`)

```
Unity → Sentis model → GPUCompute/CPU → SOMA poses → ретаргет → кости
```

**Плюсы:** всё в Unity, без Python; кросс-платформенный backend; можно шипить.
**Минусы:** 758/380 MB модель; импорт 6993 узлов; latency неизвестна; Sentis не использует TensorRT; 200M params на compute shaders; риск, что p95 не уложится в бюджет. **Статический op-аудит — хороший, но импорт не проверен.**

### 4.3. C. Native ONNX Runtime / TensorRT plugin

```
Unity C# → P/Invoke → C++ plugin → ONNX Runtime (CPU/DirectML/CUDA) или TensorRT → SOMA poses
```

**Плюсы:** максимальная скорость (TensorRT), контроль над памятью, можно CPU/DirectML.
**Минусы:** нативные DLL под Windows x64; CUDA/TensorRT зависимости; версии драйверов; сложная сборка; 758 MB модель; не для MMO.

### 4.4. D. HTTP sidecar (только dev)

```
Unity → HTTP → Python FastAPI + MotionBricksSOMAEngine (TensorRT) → JSON poses
```

**Плюсы:** самый быстрый путь к качеству; официальный engine; не нужно портировать модель.
**Минусы:** Python + CUDA + TensorRT на машине; сеть; не ship; последовательная очередь; задержка LAN.
**Когда:** прототип, offline baker, сравнение качества.

### 4.5. E. Baked clips + Animator/Animancer (ship)

```
.anim → Animator state / AnimatorOverrideController / Animancer → кости
```

**Плюсы:** zero runtime cost, deterministic, works on all clients, network-friendly, сохраняет Animation Events.
**Минусы:** нет генерации в runtime; нужен автор/пайплайн.

### 4.6. Сравнение

| Критерий | A. Offline baker | B. Sentis | C. Native plugin | D. Sidecar | E. Baked + Animator |
|---|---|---|---|---|---|
| Модель в билде | ❌ | ✅ 380–758 MB | ✅ 758 MB | ❌ | ❌ |
| NVIDIA обязательна | ❌ (можно CPU) | ❌ | ⚠️ для TensorRT; ❌ для ORT CPU | ✅ | ❌ |
| Runtime-адаптация | ❌ | ✅ | ✅ | ✅ | ❌ |
| MMO scale | ✅ | ⚠️ 1 игрок | ⚠️ 1 игрок | ⚠️ dev | ✅ |
| Детерминизм | ✅ | ⚠️ | ⚠️ | ⚠️ | ✅ |
| Сложность | Средняя | Высокая | Очень высокая | Низкая | Низкая |
| Риск | Низкий | Высокий | Высокий | Низкий (dev) | Низкий |

---

## 5. Unity-реалии: что значит «без Animator»

### 5.1. Компоненты, которые остаются

- `Animator` — контейнер `Avatar`, `HumanDescription`, bone mapping; нужен для `GetBoneTransform`, Humanoid retarget, Animation Events, IK.
- `SkinnedMeshRenderer` — скиннинг; читает transforms костей.
- `Avatar` — описание Humanoid-скелета; без него нет muscle space/retarget.
- `AnimatorController` — **не обязателен** для сгенерированного движения; можно оставить пустым или не использовать.

### 5.2. Способы записи позы

| Способ | Плюсы | Минусы |
|---|---|---|
| Прямая запись `Transform.localRotation/localPosition` в `LateUpdate` | Просто; community bridge так делает | Может драться с Animator/IK; нет root motion/events; порядок с Animation Rigging; equipment/кости надо мапить вручную |
| `HumanPoseHandler.SetHumanPose` | Humanoid muscle space; ретаргет; учитывает Avatar | Дороже; теряет некоторые детали; требует Avatar; не для Generic |
| `PlayableGraph` + `AnimationScriptPlayable` / `IAnimationJob` | «Правильный» путь; работает с Animator, root motion, IK; контроль порядка | Сложнее; нужно писать job; конфликт с текущим решением «не писать свой PlayableGraph» (T-INP-08) |
| `AnimationClip` + `Animator`/`Animancer` | Детерминизм; events; IK; привычный пайплайн | Нет runtime-генерации |

**Рекомендация для Project C:** baked clips + существующий Animator. Для runtime-эксперимента — `HumanPoseHandler` или `AnimationScriptPlayable`, но не прямая запись в `LateUpdate` поверх Animation Rigging.

### 5.3. Root motion

- `NetworkPlayer` использует `CharacterController` и `applyRootMotion = 0`.
- Если у клипа есть root motion, его нельзя просто включить: `CharacterController.Move` и `NetworkTransform` уже управляют позицией.
- Для игрока: извлекать delta из Hips/RootT и вызывать `CharacterController.Move(delta)`; для NPC: игнорировать root motion, двигать `NavMeshAgent`, а клип играть in-place и speed-match.
- Для baked-клипов: либо `in-place` (удалить горизонтальное смещение), либо `with displacement` (оставить Hips, но проигрывать через драйвер).
- Floating origin: delta локальный, мировые координаты не кэшировать.

### 5.4. Animation Events и бой

`T-INP-08` завязывает урон на `Animation Event OnAttackImpact` на ~60% клипа. Это значит:
- нельзя заменять combat-клипы neural-генерацией без пересчёта тайминга;
- baked-клипы для locomotion/traversal можно, для атак — нет;
- если генерировать attack, событие нужно расставить вручную и проверить hit window;
- хитбоксы должны быть серверными коллайдерами, а не костями.

### 5.5. Equipment

`EquipSlotToBone` использует `Animator.GetBoneTransform(HumanBodyBones...)`.
- Пока Animator включён и Avatar валиден — работает.
- Если отключать Animator и писать кости напрямую — `GetBoneTransform` может не работать; нужно кэшировать transforms/рефакторить.
- При ретаргете SOMA→Humanoid кости Hips/Spine/Hands/Feet должны остаться валидными; иначе оружие/одежда отвалятся.

---

## 6. Ретаргет SOMA → Unity Humanoid

### 6.1. Маппинг (стартовая версия)

| SOMA | Unity HumanBodyBones |
|---|---|
| Hips | Hips |
| Spine1 | Spine |
| Spine2 | Chest |
| Chest | UpperChest |
| Neck1 | Neck |
| Neck2 | (merge в Neck) |
| Head | Head |
| Jaw | Jaw |
| LeftEye / RightEye | LeftEye / RightEye |
| LeftShoulder / RightShoulder | LeftShoulder / RightShoulder |
| LeftArm / RightArm | LeftUpperArm / RightUpperArm |
| LeftForeArm / RightForeArm | LeftLowerArm / RightLowerArm |
| LeftHand / RightHand | LeftHand / RightHand |
| LeftHandThumbEnd / RightHandThumbEnd | LeftThumbDistal / RightThumbDistal |
| LeftHandMiddleEnd / RightHandMiddleEnd | LeftMiddleDistal / RightMiddleDistal |
| LeftLeg / RightLeg | LeftUpperLeg / RightUpperLeg |
| LeftShin / RightShin | LeftLowerLeg / RightLowerLeg |
| LeftFoot / RightFoot | LeftFoot / RightFoot |
| LeftToeBase / RightToeBase | LeftToes / RightToes |

Это маппинг community bridge; он приблизительный. Для наших персонажей (включая гоблинов) пропорции и rest pose могут отличаться.

### 6.2. Формула ретаргета

Для локальных ротаций:

```
q_target_local = q_target_rest * inverse(q_source_rest) * q_source_local
```

- `q_source_rest` — rest-поза SOMA (или bind pose);
- `q_target_rest` — rest-поза Unity-персонажа;
- `q_source_local` — локальная ротация из модели;
- результат — локальная ротация для Unity-кости.

Для root/positions:
- позиция root масштабируется по росту (`height_target / height_source`);
- позиции остальных суставов не переносятся напрямую — используется скелет Unity и локальные ротации; иначе пропорции сломаются.

### 6.3. Mirror и ориентация

- SOMA: right-handed Y-up; Unity: left-handed Y-up.
- Mirror: `z → -z`, quaternion `(x,y,z,w) → (-x,-y,z,w)`.
- Проверка: T-pose, левая/правая рука, лицо вперёд; если персонаж смотрит назад — инвертировать ось.
- Для каждого FBX может быть своя authoring orientation; не полагаться на один глобальный mirror.

### 6.4. Пропорции и ограничения

- SOMA — параметрическая модель человека; гоблины/капитаны/толстые NPC могут выглядеть неестественно.
- Высота и длина конечностей масштабируются приблизительно; для сильных отличий нужен proper retarget (например, Unity Animation Rigging / внешний retarget).
- Пальцы: только thumb/middle end; остальные пальцы остаются в rest/последней позе.
- Лицо: Jaw/eyes — суставы, не blendshapes; мимика не генерируется.

### 6.5. Foot IK

- Использовать predicted foot contacts как веса для `TwoBoneIK` / foot-lock.
- Проверять: при contact > 0.5 стопа не должна двигаться > 2 см/кадр.
- При penetration — поднимать/смещать; но это уже постобработка, не модель.
- Для NPC на NavMesh — IK по земле/палубе; для игрока — по `CharacterController` ground.

---

## 7. Сеть и MMO

### 7.1. Детерминизм

- Одинаковый вход + одинаковый backend/точность → скорее всего одинаковый выход.
- FP16 vs FP32 — разные токены; GPU/драйвер/ORT версии — разные; autoregressive feedback усиливает расхождения.
- Сервер не может воспроизвести позу клиента; значит, neural-анимация не может быть gameplay-authoritative.

### 7.2. Bandwidth

Одна поза: 30 суставов × (3 position + 4 quaternion) × 4 B = **840 B**.
- 30 Hz → **25.2 KB/s** на персонажа.
- 100 персонажей → **2.52 MB/s (~20 Mbps)**.
- 1000 → **25.2 MB/s**.
- Только ротации (4 floats): 480 B/кадр → 14.4 KB/s; 100 → 1.44 MB/s.
- Выход модели: 64 × 366 × 4 B = **93.7 KB** на генерацию; при 1 Hz — 93.7 KB/s на персонажа; 100 → 9.37 MB/s.

Для MMO это слишком дорого; baked clips/state replication дешевле.

### 7.3. Server authority и хитбоксы

- `NpcBrain` — server-side FSM, `NavMeshAgent`, `NetworkTransform`; клиентская анимация косметическая.
- Если генерировать позы на клиенте, серверные хитбоксы не должны зависеть от костей; использовать капсулы/коллайдеры.
- Если хитбоксы bone-based — neural runtime нельзя включать в бою.
- `T-INP-08` timing удара — authored; не генерировать.

### 7.4. Что реплицировать

- Не кости.
- Состояние: скорость, heading, style id, primitive id, start time, phase.
- Клиент играет baked-клип/Animator по состоянию; remote players — интерполяция root + клип.

---

## 8. Floating Origin

- Персонажи под корнями `WorldScene_*`; rebase сдвигает корни, локальные кости не меняются.
- Baked-клипы содержат локальные transforms — FO не влияет.
- Авторские target-позы:
  - хранить в **local space** якоря под корнем сцены;
  - не хранить абсолютные мировые координаты (120 км → float precision, rebase).
- Runtime target-позы:
  - вычислять из текущего transform персонажа + локального offset;
  - не кэшировать мировые `Vector3` между кадрами;
  - если кэш неизбежен — `ApplyRebaseTranslation` + регистрация участником.
- `NpcBrain`/`NetworkPlayer` уже `IGlobalMotionActorParticipant`; новые компоненты (driver/baker runtime) не должны ломать контракт.
- Проверка: F8 → объект на месте, 0 errors; F9 → возврат.

---

## 9. Лицензии

| Компонент | Лицензия | Что можно |
|---|---|---|
| Код MotionBricks | Apache 2.0 | Использовать, модифицировать, распространять; сохранить notices |
| Модели SOMA/G1 (ONNX, SafeTensors) | NVIDIA Open Model Agreement | Коммерческое использование, распространение модифицированных моделей, outputs не принадлежат NVIDIA; соблюдать terms/attribution/export |
| Sample motions (BONES BVH/CSV) | NVIDIA Sample Data Evaluation License | Только внутренняя оценка/тест; **не шипить** и не использовать как production-контент |
| Training data (BONES RP1) | не распространяется | Нет доступа к полному датасету |
| Community bridges | Apache 2.0 (заявлено) | Проверить и сохранить attribution; неофициально |

**Практически:**
- Generated output можно использовать коммерчески (NVIDIA не претендует на ownership), но нужно соблюсти условия agreement.
- Не использовать bundled sample motions как target/context для production-клипов; лучше свои позы/клипы.
- В `NOTICE` добавить: "Licensed by NVIDIA Corporation under the NVIDIA Open Model Agreement."
- Model card/weights не перепродавать как есть; при распространении — приложить лицензию.

---

## 10. Ограничения и риски (сводка)

| Ограничение | Последствие |
|---|---|
| SOMA 30 joints | Нет полных пальцев/лица; ретаргет на Humanoid приблизительный |
| Kinematic only | Нет физики/коллизий; penetration/foot sliding возможны |
| No scene input | Приложение само размещает target-позы и обходит препятствия |
| Short segments | Нужна цепочка генераций; ошибки накапливаются |
| Training distribution | Новые стили/скиллы zero-shot не гарантированы |
| 200M params / 758 MB | Память, build size, import time |
| TensorRT/NVIDIA | Не работает на AMD/Intel GPU без ORT/Sentis fallback |
| Non-determinism | Не для server-authoritative gameplay |
| No full training pipeline | Нельзя дообучить под наши стили в рамках релиза |
| License | Нужны attribution/notice; sample data — eval-only |

---

## 11. Выводы

1. MotionBricks — **сильная research-технология**, но не продукт «подключил и заменил Animator».
2. Для Project C основная ценность — **offline authoring**: короткие переходы/стили/взаимодействия, запечённые в `.anim`.
3. Runtime-путь технически возможен (Sentis/ORT/TensorRT), но экономически и архитектурно не подходит MMO-клиенту.
4. Статический op-аудит снял главный риск Sentis (нет неподдерживаемых операторов), но производительность/импорт остаются неизвестными; это задача отдельного spike, а не «давайте внедрим».
5. Решение по умолчанию: **не интегрировать runtime; использовать baker по необходимости; альтернативы — motion matching/Animancer/IK** (см. [03_ALTERNATIVES.md](03_ALTERNATIVES.md)).

---

*Все внешние факты — в [04_SOURCES.md](04_SOURCES.md). Локальные замеры — методология там же.*
