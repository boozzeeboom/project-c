# T-MB01 — NVIDIA MotionBricks: ресерч, применимость и пайплайн для Project C

> **Дата:** 2026-11-15
> **Статус:** research complete; решение предложено, ждёт подтверждения
> **Область:** анимация персонажей, Unity 6.5 (`6000.5.2f1`), Windows x64, MMO-sandbox
> **Тикет:** `T-MB01` (новая серия `T-MB` — MotionBricks; заводится этим документом)
> **Документы:** [01_RESEARCH.md](01_RESEARCH.md) · [02_PIPELINE.md](02_PIPELINE.md) · [03_ALTERNATIVES.md](03_ALTERNATIVES.md) · [04_SOURCES.md](04_SOURCES.md)

---

## TL;DR

1. **MotionBricks — реальный проект NVIDIA** (SIGGRAPH 2026, `arXiv:2604.24833`), а не хайп из пересказа: 200M параметров, SOMA-скелет на 30 суставов, 30 Hz, одна генерация — 24–64 сырых кадра (18–58 валидных, ~0.6–1.9 с), обучение на ~313k клипов из корпуса ~350k / 700 ч.
2. **Это не «drop-in замена Animator»**. Официального Unity-пакета нет. Нужны ONNX Runtime / TensorRT, ретаргет SOMA → Unity Humanoid, внешняя логика Smart Primitives и свой планировщик генерации. Компонент `Animator`, `Avatar` и скиннинг никуда не исчезают — исчезает только необходимость в Animator Controller для сгенерированного участка движения.
3. **Модель большая:** ONNX весит **758 MB (FP32)** / **380 MB (FP16)**; 200M параметров; по замеру сообщества — **~818 MiB VRAM** на RTX 5070 Ti. «Крошечная модель, которая легко живёт в VRAM рядом с текстурами» — это маркетинговое преувеличение для MMO-клиента.
4. **Runtime для MMO не подходит.** На каждого персонажа нужна отдельная генерация; NVIDIA/TensorRT или тяжёлый ONNX; генерация недетерминирована между GPU/точностью; сервер не может воспроизвести позу; кости нельзя использовать для хитбоксов; передача 30 суставов × 30 Hz на сотни NPC — десятки МБ/с.
5. **Для Project C применимо как offline motion factory:** генерировать переходы, стили, взаимодействия с объектами, запекать в обычные `.anim` и проигрывать через существующий `Animator` / `AnimatorOverrideController` (уже есть пайплайн `T-INP-08`).
6. **Offline baker можно сделать без NVIDIA.** На этой машине (Intel i7-8700, 6 ядер / 12 потоков, ONNX Runtime CPU, FP32, N=1) одна генерация заняла **73–77 ms**; N=4 — **146–157 ms**; N=8 — **252–279 ms** (random input, без оценки качества). Этого достаточно для пакетного запекания клипов.
7. **Runtime-эксперимент для одного игрока** возможен только за feature flag и с fallback. Статический аудит ONNX показал: все **51 тип операторов** графа есть в списке поддерживаемых Sentis, но **6993 узла**, динамические размерности и 2480 `Constant` — риск не по «неподдерживаемым операм», а по latency/импорту. Нужен отдельный spike.
8. **Альтернативы для runtime:** Animancer (Playable API), Motion Matching (MxM / JLPM22), Animation Rigging IK, offline Kimodo/ARDY, video mocap. Рекомендация — гибрид: существующий Animator/Animancer + motion matching для игрока + IK + offline baker для недостающих переходов.
9. **Решение:** не тащить 758 MB модель в клиент MMO, не заменять Animator у NPC, не использовать neural poses для хитбоксов. Инвестировать в baker только под конкретную анимационную задачу, а не «на всякий случай».
10. **Что реально проверено:** официальные docs/paper/ONNX-манифест, статический аудит ONNX-графа, CPU-бенчмарк ONNX Runtime, чтение кода проекта. Официальный TensorRT-движок, Sentis-импорт и Play Mode **не запускались** (правило `dont-start-play`).

---

## 1. Что именно проверялось

| Источник | Что взято |
|---|---|
| [Project page](https://nvlabs.github.io/motionbricks/) + [paper](https://arxiv.org/abs/2604.24833) | Архитектура, Smart Primitives, 15k FPS / 2 ms, 350k клипов, UE5-демо |
| [GitHub NVlabs/motionbricks](https://github.com/NVlabs/motionbricks) | Runtime API, ONNX, TensorRT, лицензии, ограничения |
| [HF nvidia/MotionBricks](https://huggingface.co/nvidia/MotionBricks) | Веса SOMA/G1, model cards, лицензия |
| [SOMA model card](https://github.com/NVlabs/motionbricks/blob/main/docs/soma_model.md) | 30 суставов, 200M параметров, входы/выходы, 30 Hz, ограничения |
| [ONNX manifest](https://github.com/NVlabs/motionbricks/blob/main/motionbricks/assets/onnx/soma/soma_n1.json) | Точные имена/размеры тензоров, порядок суставов |
| [NVIDIA Open Model Agreement](https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-open-model-agreement/) | Коммерческое использование, распространение, права на output |
| Unity Sentis docs / package registry | Opset 7–25, список операторов, версия 2.6.1, Unity 6000.0+ |
| Community Unity bridges | Реальные попытки интеграции, замеры, ограничения |
| Код Project C | Текущий Animator-пайплайн, CharacterController, NavMeshAgent, FO-контракты |
| Локальный аудит | ONNX-граф (6993 узла) + ONNX Runtime CPU benchmark |

Подробный список — в [04_SOURCES.md](04_SOURCES.md).

---

## 2. Fact-check исходного запроса

Исходный текст — это смесь верных фактов, пересказа демо и выдуманных деталей. Ниже — по пунктам.

| # | Утверждение из запроса | Вердикт | Как на самом деле |
|---|---|---|---|
| 1 | «Больше нет компонентов Animator, окон Animator Controller и стейт-машин» | ⚠️ Полуправда | Нет необходимости в **Animator Controller** для сгенерированного движения. Но `Animator` + `Avatar` + `SkinnedMeshRenderer` остаются; высокоуровневое поведение всё равно нужно — в UE5-демо это **StateTree** с тремя состояниями (locomotion / target pose sequence / falling). «Стейт-машины исчезли» — неверно. |
| 2 | «Оригинальный MotionBricks написан на Python с TensorRT и PyTorch» | ✅ Верно | Официальный runtime — Python + NVIDIA Warp + Newton + TensorRT 10.8; обучение/чекпоинты — PyTorch. |
| 3 | «Внутри Unity пайплайн из четырёх C#-менеджеров: SmartPrimitivesManager, NeuralRootPredictor, TransformerPoseInference, VQVAEDecoder» | ❌ Выдумка | Официального Unity-кода нет. Реальная декомпозиция: **Smart Primitives** (app-side), **root module**, **pose module**, **decoder (VQ-VAE)**. Имена классов в запросе не существуют; community-бридж использует `SomaHumanoidMapper`, `MotionBricksClient`, `MotionBricksPlayer` и т.п. |
| 4 | «ONNX/TensorRT-модель встраивается непосредственно в Unity» | ⚠️ Полуправда | ONNX — да. TensorRT — нативная NVIDIA-библиотека, не Unity-пакет; для неё нужен C++ plugin / sidecar / CUDA. Unity Sentis TensorRT не использует (compute shaders / DirectML). |
| 5 | «Humanoid Rig, ИИ управляет глобальными углами суставов относительно Root» | ⚠️ Приблизительно | Модель работает на **SOMA** (30 суставов), выдаёт локальные трансформы суставов + глобальную траекторию root. Для Unity Humanoid нужен ретаргет; «просто Humanoid Rig» недостаточно. |
| 6 | «Не нужно настраивать IK для ног — ИИ генерирует позы без foot sliding по умолчанию» | ❌ Неверно как гарантия | Модель предсказывает **foot contacts** (heel/toe L/R), но это кинематика без физики и без foot-locking. Model card прямо перечисляет foot skating/penetration как ограничения; после ретаргета скольжение усиливается. |
| 7 | «Модель весит очень мало и легко помещается в VRAM параллельно с текстурами» | ❌ Преувеличение | 200M параметров; **758 MB FP32 / 380 MB FP16** ONNX; ~818 MiB VRAM по замеру community. Для 6–8 GB GPU это заметная доля. |
| 8 | «Размер билда уменьшается на гигабайты, можно удалить все анимации» | ⚠️ Только для узкого сценария | Для single-player AAA с полным покрытием — возможно. Для Project C — нет: модель не даёт полные пальцы и мимику (blendshapes), не заменяет authored combat timing, emotes, hit reactions; плюс сама модель 380–758 MB. Гибрид остаётся. |
| 9 | «Идеальный инбетвининг, без рывков» | ⚠️ Сильно оптимистично | Качество высокое, но генерация — короткие сегменты 0.6–1.9 с, склеиваемые autoregressive; ошибки накапливаются, FP16/FP32 расходятся, при ретаргете возможны артефакты. |
| 10 | «Debug-режим: три призрачных меша (синий/фиолетовый/финальный)» | ⚠️ Демо-техника | Это приём из демо/порта, не продуктовая фича. Реализуемо, но не «из коробки». |
| 11 | «Готового ассета нет, собирают из preview GR00T-WholeBodyControl» | ⚠️ Устарело | Уже есть официальный репозиторий `NVlabs/motionbricks`, ONNX SOMA + G1, runtime, редакторы. GR00T preview — это G1-робот, не SOMA-человек. Community Unity-бриджи существуют, но неофициальны. |
| 12 | «350 000 движений упакованы в одну математическую модель» | ✅ Почти | Обучение на **313 526** клипах из корпуса ~350k / 700 ч; полный датасет не поставляется. |
| 13 | «15 000 FPS, 2 ms latency» | ⚠️ Только модель | TensorRT на RTX 4090, batch, без input prep/decode/endpoint correction. Community: ~2.37 ms average, но spike до ~57.5 ms на RTX 5070 Ti. |
| 14 | «Unity 6 — особенно актуально» | ⚠️ Частично | Unity 6.5 совместим с Sentis 2.6.1 (Unity 6000.0+), opset 17 поддерживается. Но Sentis в проекте не установлен, и импорт 6993-узлового графа не проверен. |

---

## 3. Применимость к Project C

### 3.1. Что есть в проекте сейчас

- **Игрок:** `NetworkPlayer` — `CharacterController`, owner-authority `NetworkTransform` (`Interpolate = false`), `applyRootMotion = 0`; Humanoid `Avatar`; `PlayerAnimation.controller` с состояниями Idle / Walk_BlendTree / Sprint / Jump / Land / Attack1H / Skill / Gather / Lumber / TurnLeft/Right / Damage.
- **NPC:** `NpcBrain` — server-side FSM, `NavMeshAgent`, репликация через `NetworkTransform`; Animator-параметры `Speed`, `IsAttacking`, `IsGrounded`, `Attack`, `Skill`, `Death`, `Surrender`, `Work`, `IsSleeping`; скиллы — через `AnimatorOverrideController`.
- **Бой:** `T-INP-08` — `AnimationClip` + `AnimatorOverrideController` + Animation Event `OnAttackImpact` на ~60% клипа; тайминг удара завязан на клип. Это критично.
- **FO:** персонажи — `IGlobalMotionActorParticipant`; NPC/игрок уже под контрактом floating origin.
- **Анимационный контент:** 1344 FBX (~6.3 GB, включая меши), 13 `.anim` (~20.5 MB), 78 `.controller`; основная библиотека — Kevin Iglesias Human Animations.
- **Пакеты:** `com.unity.animation.rigging 1.4.1`, Cinemachine 3.1.7, Timeline 1.8.13; **Sentis / `com.unity.ai.inference` не установлен**.

### 3.2. Матрица применимости

| Сценарий | Применимость | Почему |
|---|---|---|
| **Offline-запекание переходов/стилей/взаимодействий** | ✅ **Да, лучший fit** | Короткие клипы 0.6–1.9 с идеально ложатся в `.anim`; существующий Animator/OverrideController их проиграет; нет runtime-зависимости. |
| **Игрок: runtime-генерация locomotion** | 🟡 **Эксперимент, не сейчас** | Технически возможно на Windows+NVIDIA; нужен ретаргет, планировщик, fallback, замер Sentis/ORT. Для MMO-клиента 380–758 MB модель — дорого. |
| **NPC: runtime-генерация** | ❌ **Нет** | NPC — server-authoritative `NavMeshAgent` + `NetworkTransform`; генерация на клиенте косметическая и будет драться с репликацией; на сервере — дорого и недетерминировано. |
| **Толпа / 100+ персонажей** | ❌ **Нет** | Одна генерация на персонажа; CPU 75 ms, GPU 2–3 ms; сеть/память/детерминизм не сходятся. |
| **Combat / hit reactions / death** | ❌ **Нет** | Тайминг удара привязан к Animation Event и серверному hit window; neural-поза не гарантирует событие и не должна определять хитбокс. |
| **Полные пальцы / мимика / emotes** | ❌ **Нет** | SOMA 30 суставов: есть только thumb/middle end, Jaw/eyes — суставы; blendshapes и полные пальцы не предсказываются. |
| **Сетевой gameplay** | ❌ **Нет для runtime; ✅ для baked** | Кости не реплицируются; baked-клип детерминирован и играется по состоянию. |
| **Floating Origin** | ✅ **Совместимо с оговорками** | Персонаж под корнем сцены; root motion — локальный; авторские target-позы хранить в local space, не в мировых координатах. |
| **Уменьшение размера билда** | ❌ **Нет** | Модель 380–758 MB; authored-клипы всё равно нужны. |

---

## 4. Вердикт

**MotionBricks не является заменой Animator в Project C.**

Это генератор коротких кинематических переходов для SOMA-скелета, требующий NVIDIA GPU + TensorRT (или тяжёлого ONNX-инференса), внешней логики Smart Primitives и ретаргета на Unity Humanoid. Для MMO-клиента с сотнями NPC, server-authoritative боем и Windows-сборкой он не годится как runtime-основа.

**Но он применим как offline-инструмент:**

- генерировать недостающие переходы (idle → walk → run → stop, turn-in-place, подъём/спуск, injured/stealth/strafing);
- генерировать взаимодействия с объектами (сесть, встать, vault, pick-up) по target-позам;
- быстро прототипировать стилизованную locomotion для NPC;
- запекать всё в `.anim` и играть через существующий Animator/Animancer, без модели в билде.

**Runtime-эксперимент** для одного игрока имеет смысл только если появится продуктовая фича, которую нельзя решить клипами (например, уникальная генерация под состояние игрока). Тогда — отдельный timeboxed spike, feature flag, fallback, и только на Windows+NVIDIA.

---

## 5. Рекомендованный план

| Фаза | Что | Срок (оценка) | Результат |
|---|---|---|---|
| **0. Audit** | Инвентаризация анимаций, бюджет, целевое железо, список недостающих переходов | 1–2 дня | Таблица «что генерировать» |
| **1. Offline baker MVP** | Python/ONNX Runtime baker + Unity Editor window + импорт `.anim` + метаданные + валидация | 2–3 недели | 3–5 реальных клипов, встроенных в `PlayerAnimation.controller` / NPC controller |
| **2. Retarget/IK** | SOMA→Humanoid ретаргет, foot IK, проверка T-pose, оборудование на костях | 1–2 недели | Клипы не «ломают» equipment и не скользят |
| **3. Hybrid Smart Primitives** | Авторинг target-поз в Unity, playback baked-клипов по триггеру, без нейросети | 2–3 недели | «Умные» взаимодействия без runtime-модели |
| **4. Runtime spike (optional)** | Sentis или ONNX Runtime plugin, один игрок, feature flag, замер latency/VRAM/quality | 2–4 недели, timebox | Go/No-go по критериям из [02_PIPELINE.md](02_PIPELINE.md) |
| **5. Production** | Только если фаза 4 прошла go; иначе — стоп на фазе 1–3 | — | Осознанное решение |

**Что делать сейчас:** не устанавливать Sentis, не качать 758 MB модель в проект, не менять `manifest.json`. Начать с фазы 0–1, если есть конкретная задача аниматора.

---

## 6. Ключевые числа

| Параметр | Значение | Источник |
|---|---|---|
| Параметры модели | ~200M (VQ-VAE 23.4M, pose 136.5M, root 40.4M) | SOMA model card |
| ONNX FP32 / FP16 | 758 MB / 380 MB | локальный `Content-Length` |
| VRAM (community) | ~818 MiB (RTX 5070 Ti) | Unity6_motionbricks_test |
| TensorRT latency | 2.50 ms FP32 / 1.28 ms FP16, N=1 (RTX 4090) | ONNX inference docs |
| TensorRT throughput | 15 000 FPS / 2 ms (paper) | paper abstract |
| Community runtime | 2.37 ms avg, spike ~57.5 ms (RTX 5070 Ti) | Unity6_motionbricks_test |
| CPU ONNX Runtime (наш замер) | 73–77 ms N=1; 146–157 ms N=4; 252–279 ms N=8 (i7-8700) | локальный benchmark |
| Скелет | SOMA, 30 суставов, 30 Hz | SOMA model card |
| Одна генерация | 24–64 сырых кадра → 18–58 валидных (~0.6–1.9 с) | runtime API |
| Контекст / цели | 4 контекстные позы + 1–4 target-позы | runtime API |
| Обучение | 313 526 клипов; корпус ~350k / 700 ч | SOMA model card |
| ONNX-граф | opset 17, 6993 узла, 434 initializer, 51 тип операторов | локальный аудит |
| Sentis | 2.6.1, Unity 6000.0+, opset 7–25 | Unity registry/docs |

---

## 7. Риски и открытые вопросы

| Риск | Влияние | Митигация |
|---|---|---|
| 758 MB модель в клиенте | Размер билда/патча, память | Не шипить; baked-клипы |
| NVIDIA/TensorRT зависимость | Отсечение игроков без RTX | Offline baker; runtime — только optional |
| Sentis импорт 6993 узлов | Долгий импорт, высокий latency | Spike; если плохо — ONNX Runtime plugin или отказаться |
| Ретаргет SOMA→Humanoid | Пропорции, foot sliding, penetration | T-pose тест, IK, ограничение target-поз |
| Недетерминизм FP16/FP32 | Разные позы у клиентов | Только косметика; baked для gameplay |
| Animation Event timing | Хитбоксы/урон | Combat оставить authored; neural не использовать в бою |
| Лицензия | Юридические риски | NVIDIA Open Model Agreement; attribution; sample data — eval-only |
| FO | Сдвиг мира | Local-space target-позы; регистрация участников |
| Качество вне training distribution | Странные позы | Ограничить стили/переходы; валидация аниматора |

**Открытые вопросы:**
1. Импортируется ли ONNX в Sentis 2.6.1 без ошибок и сколько занимает импорт?
2. Какой p95 latency у Sentis на RTX 3060/4070/4090?
3. Достаточно ли качества SOMA→Unity Humanoid для наших пропорций (включая гоблинов/капитанов)?
4. Нужен ли runtime вообще, или хватает baked-клипов?
5. Есть ли продуктовая фича, которая требует именно генерации, а не клипов?

---

## 8. Состав документации

| Файл | Содержание |
|---|---|
| **README.md** (этот) | Вердикт, fact-check, матрица применимости, план |
| [01_RESEARCH.md](01_RESEARCH.md) | Глубокий технический разбор: архитектура, ONNX, граф, runtime-варианты, Unity-реалии, ретаргет, сеть, FO, лицензии |
| [02_PIPELINE.md](02_PIPELINE.md) | Пайплайн интеграции: фазы, C#-контракты, baker, runtime, QA, тикеты |
| [03_ALTERNATIVES.md](03_ALTERNATIVES.md) | Альтернативы и сравнительная матрица |
| [04_SOURCES.md](04_SOURCES.md) | Аннотированные источники и методология замеров |

---

## 9. Как проверить (для пользователя)

Агент **не запускает Play Mode** и не делает скриншоты. Проверка — вручную:

1. **Compile:** открыть проект, дождаться компиляции, Console → 0 errors.
2. **Docs:** прочитать README + 02_PIPELINE, подтвердить/отклонить решение.
3. **Baker (если одобрен):** запустить внешний baker на одном тестовом переходе, импортировать `.anim`, проиграть в Preview/Play Mode, проверить:
   - первый кадр совпадает с контекстом;
   - последний — с target-позой;
   - нет скольжения стоп > 2 см/кадр;
   - equipment не отрывается;
   - Animation Event `OnAttackImpact` не затрагивается (combat-клипы не генерируем).
4. **FO:** F8 → персонаж на месте, 0 errors; F9 → возврат.
5. **Network:** 2 клиента, NPC/игрок не дёргаются, позиции совпадают.

---

*Документ создан по правилам проекта: русский язык, тикет в заголовке, ссылки на код/доки, без запуска Play Mode.*
