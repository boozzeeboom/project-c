# hy4_research — MotionBricks: внешний ресерч и ответ на стратегический вопрос

> **Дата:** 2026-11-18
> **Тип:** research only. Код не менялся, Play Mode не запускался, тесты не запускались, коммит не создавался.
> **Рамка:** Unity 6000.5.2f1, StandaloneWindows64, host-client co-op до 4 игроков (не MMO — прежние MMO-аргументы считать исторической гипотезой).
> **Чем отличается от [01_RESEARCH.md](01_RESEARCH.md):** там технический fact-check, ONNX-аудит и Sentis-совместимость; здесь — **как MotionBricks реально встраивают в игры** (официальный путь, Unity-спільнота, offline-продакшен) и прямой ответ на вопросы «зачем он нам», «что даст нового», «нужно ли печь переходы».
> **Локальный контекст:** [README.md](README.md), [HANDOFF.md](HANDOFF.md), [03_ALTERNATIVES.md](03_ALTERNATIVES.md), [16_OFFLINE_REJECTED_RUNTIME_NEXT.md](16_OFFLINE_REJECTED_RUNTIME_NEXT.md), [17_T-MB06_RUNTIME_SIDECAR.md](17_T-MB06_RUNTIME_SIDECAR.md).

---

## 0. Короткий ответ на вопросы пользователя

**1. Для чего нам MotionBricks?**
Не как замена Animator, а как **generative motion factory**: модель умеет достраивать движение между «последними 4 позами» и «1–4 целями» там, где нет готового клипа. Плюс zero-shot стили (zombie / injured / skipping / strafe / crouch) и object-взаимодействия (сесть, перелезть, поднять) как «кирпичики» target-поз. Это не «больше анимаций» — это **синтез недостающих переходов и реакций под текущий ввод**.

**2. Что он даст, чего у нас нет?**
- генерацию перехода **без заранее авторингованного transition-clip** и без ручной настройки кроссфейдов;
- вариативность от прогона к прогону (одна и та же поза не повторяется mechanically);
- адаптацию стиля/скорости/направления живьём (runtime-ветка), включая поворот «на месте» без отдельного turn-клипа;
- object-интеракции, авторинг которых по словам авторов занимает **<10 минут** без animation graph;
- неявное планирование момента/импульса и foot placement (но **не гарантию** контакта).

**3. Первичная цель — «более живые анимации». Что в итоге?**
«Живость» в нашем проекте сейчас упирается не в количество клипов (их ~1344 FBX), а в **выбор/смешивание/реактивность/контакты/вариативность**. MotionBricks runtime решает реактивность и вариативность, но ценой 380–758 MB модели, NVIDIA/TensorRT, недетерминизма и ~5–32 мс на генерацию. **Дешёвый и индустриально проверенный ответ на «живость» — Motion Matching + Animation Rigging IK + процедурные additively-слои**, а MotionBricks — точечный инструмент для контента, которого нет. Рекомендация: **не заменять стек, а добавить MM/IK как основной путь; MB оставить как (а) offline-фабрику для узкого списка клипов и (б) уже написанный runtime-эксперимент T-MB06 за feature flag.**

**4. «Донастраивать наши анимации, генерируя и запекая переходы?»**
**Как общую стратегию — нет, это уже проверено и отклонено.** Offline pack v1: 22 перехода, **0/22** валидатор, **9/22** slide ≤2 см, максимум **11.32 см/кадр**, в Animator не внедрялся, пользователь: «ничуть не лучше того, что было, и требует 100500 отладки» ([16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)). Причины системные: MB — генератор по позам, а не библиотека клипов; `NetworkPlayer` задаёт скорость отдельно от клипа → нужен motion coupling; матрица состояний взрывается; запекание теряет главное преимущество — генерацию под текущий ввод. bake допустим **только для закрытого перечня отсутствующих клипов** с жёсткими гейтами (см. §5).

---

## 1. Что MotionBricks из себя представляет по первоисточникам

| Факт | Значение | Источник |
|---|---|---|
| Сущность | Исследовательский проект NVIDIA, SIGGRAPH 2026, ACM TOG 45(4), DOI 10.1145/3811334; **не Unity-пакет, не игровой ассет** | GitHub README, project page |
| Состав | обученная модель + Python runtime (Warp / Newton / TensorRT) + 2 демо + 2 редактора + ONNX-экспорт | GitHub README |
| Примитив генерации | 4 context-позы + 1–4 target-позы → сегмент 0.6–1.9 с (6–16 токенов × 4 кадра @30 Гц; 18–58 валидных кадров) | runtime_api.md |
| Кто принимает решения | host-приложение решает **WHAT**, модель решает **HOW** (как дойти от текущего движения до цели) | guided_tutorial.md |
| Цепочка | autoregressive: проигрываем сегмент → `MotionRecorder` хранит реально показанные позы → последние 4 становятся context следующей генерации | runtime_api.md |
| Smart Locomotion | unit-интерфейс velocity/heading/style, zero-shot, без retraining и per-task tuning | project page |
| Smart Objects | target-позы вокруг объекта (sit / vault / pick-up / fall), pivot адаптирует последовательность под направление подхода | guided_tutorial.md |
| Обучение | BONES-SEED ~350k клипов / 700 ч; обучение на 313 526 клипах | SOMA model card |
| Размер | ~200M параметров (root 40.4M + pose 136.5M + VQ-VAE decoder 23.4M); ONNX **758 MB FP32 / 380 MB FP16** | model card, ONNX manifest |
| Скелет | SOMA, 30 суставов; **нет полных пальцев и мимики**; есть Jaw/eyes и hand-end joints | SOMA model card |
| Железо | официальный runtime требует NVIDIA GPU + TensorRT; демо тестировались на RTX 4090 | GitHub README |
| «15 000 FPS / 2 мс» | throughput модели в батче на RTX 4090, **без** input prep / decode / endpoint correction / ретаргета / рендера | paper, project page |
| Community замеры | ~2.37 мс среднее, спайки до ~57.5 мс (RTX 5070 Ti); ~818 MiB VRAM; CPU ONNX Runtime 73–77 мс N=1 на i7-8700 | community, локальный бенчмарк [01](01_RESEARCH.md) |

**Чего модель НЕ делает (важно для нашего дизайна):**
- не принимает геометрию сцены, коллизии, текст, теги оружия;
- не гарантирует физическую выполнимость, контакт стоп, отсутствие penetration (model card прямо перечисляет foot skating / penetration как ограничения);
- не предсказывает пальцы/лицо, не заменяет высокоуровневое поведение (в UE5-демо — StateTree из 3 состояний);
- не является «дообучением под нашу игру»: zero-shot работает в пределах training distribution, полного training pipeline в релизе нет.

---

## 2. Как MotionBricks реально встраивают в игры

### 2.1. Официальный путь — Unreal Engine 5, а не Unity

В демо NVIDIA (2:40 uncut, «каждое движение сгенерировано нейросетью, без foot-locking/blending/collision/hand-authored transitions»):
- ONNX-модель → **нативный C++ плагин** с TensorRT;
- **встроенный real-time retargeter** UE5 (обучение на SOMA, демо-персонажи Messenger/Guard);
- высокоуровневое поведение — **StateTree** (locomotion / target pose sequence / falling), три задачи: locomotion task actor всегда активен на персонаже, interaction task actor кладётся в сцену, falling спавнится динамически;
- авторство Smart Primitive — **<10 минут** на каждый (по paper);
- target-позы в demonstrations берутся из «горсти клипов» — то есть **модель не заменяет клипы как источник поз**, она заменяет *переходы* между ними.

Вывод: даже у автора технология — это **слой поверх движка и поверх клипов**, с собственным C++-плагином и state-машиной. «Убрать Animator/StateTree целиком» — не то, что демонстрируется.

### 2.2. Unity: готового плагина нет; что реально существует

| Проект | Что делает | Урок для нас |
|---|---|---|
| **NVlabs/motionbricks** | SPECIAL official repo: ONNX SOMA/G1, Python runtime, Inbetweener, Scene Preview, Smart Primitive/Scene Editor | «Референс-архитектура», не ship- готовый код |
| **motionbricks-unity-bridge** (community) | FastAPI sidecar + Unity C# client + Clip Baker | Быстрый путь к качеству и offline-клипам; dev-only, не ship |
| **Unity6_motionbricks_test** (community) | Sentis / ONNX Runtime в Unity 6 | ~818 MiB VRAM, 2.37 мс среднее / спайк 57.5 мс — «2 мс» в игре не гарантированы |
| **motionbricks-studio** (limeflash, MIT) | Offline-фабрика для **реальной игры Build a Freak (Roblox)**: MB — hit reactions ~0.3 с и transitions ~4 мс/шаг; Kimodo — vault/limp/locomotion; Blender-ретаргет; на выходе clip JSON | **«Not for attacks: generated punches didn't hold up»**; «our own poses only», sample motions eval-only. Прямое подтверждение нашего вывода про combat |
| **tide-motion** (Tide Engine) | `connect`: последние 4 кадра source + первые 4 кадра target → MB генерирует мост, endpoint correction сохраняет позы точно; выход — GLB без модели | MB требует **канонических SOMA имён/пропорций**; «no foot-contact cleanup, obstacle avoidance or physical validation is guaranteed» |
| **KimodoUnityBridge** | Unity-пакет (git URL), prompt → generate & bake → Humanoid clips, Timeline, runtime playback, CUDA + CPU fallback | Для «живых» *новых клипов* по тексту это практичнее, чем MB |
| **promptimate** | Unity 6, Kimodo через Vulkan (AMD/NVIDIA/Intel), ~2 GB VRAM, ~2.5 с на 5 с клипа, Humanoid AnimationClip + Foot IK + пальцы из prompt | Body-only; но стандартный `.anim` — вписывается в наш пайплайн без нейросети в билде |
| **kimodo-practical** | end-to-end: сертификация рига → bake на каноническом скелете → integration; **zero neural code in runtime**; валидировано боёвкой на two-player fighting game | «Не делай ретаргет всегда правильным — сделай невозможным отправить неправильное»: гейты + сертификация |
| **motion-bricks.cpp** | C++/Vulkan порт, F32→F16/квантизация | Ранний этап; альтернатива CUDA-зависимости |

**Общий паттерн встраивания (无论 Unity или UE):**
```
gameplay/FSM      → выбирает target-позы и «что дальше»
MotionBricks      → генерирует «как дойти» (context → target)
application       → ретаргет, IK, root motion, events, recorder
animation system  → Animator / PlayableGraph / baked clip
```
То есть **MotionBricks не заменяет анимационный рантайм, а вставляется в него как генератор сегментов**.

### 2.3. Почему «15 000 FPS, 2 мс, можно удалить все анимации» — не про наш случай

- 15 000 FPS — модельный throughput в батче, а не FPS игры и не end-to-end latency.
- Модель 380–758 MB сама по себе больше, чем типичный выигрыш от «удаления анимаций».
- Те, кто реально делает продакшен (motionbricks-studio, tide-motion, kimodo-practical), **генерируют офлайн и шипят обычные клипы**; runtime neural почти нигде не уходит в shipping.
- Даже официальный UE5-плагин помечен как research/experimental; API и данные могут ломаться.

---

## 3. Что он даст нового / чего НЕ даст

| Даёт (новое относительно текущего стека) | Не даёт (остаётся нашим же кодом/контентом) |
|---|---|
| Переход между произвольными позами **без authored transition-clip** | Детерминизма: FP16/FP32, GPU/драйвер, autoregressive feedback расходятся → сервер не повторит позу |
| Zero-shot стили: injured / zombie / skipping / strafe / crouch без новых клипов | Полных пальцев и мимики (SOMA 30 суставов, нет face blendshapes) |
| Object-интеракции по target-позам + pivot под любой подход | Combat timing / Animation Event `OnAttackImpact` / серверные хитбоксы (`T-INP-08`) |
| Вариативность между прогонами («не один и тот же шаг») | Понимания сцены/коллизий: obstacle avoidance и physical validation не гарантированы |
| Реактивную перегенерацию при изменении ввода (runtime) | Гарантированного foot-locking: foot contacts — сигнал для нашего IK, не IK сам по себе |
| Неявный план импульса/момента и placement стоп | Сетевой репликации костей: реплицируем состояние, не позы |
| Компактный авторский интерфейс «кирпичиков» (~10 мин/примитив) | Готового Unity-пакета, лёгкого билда и кросс-платформенности (CUDA/TensorRT) |

**Практическая формула:** MB закрывает «как перейти/как вариацию», но не закрывает «что делать в бою, как выглядит лицо, кто авторитетен, как не скользят стопы, как это шиппить».

---

## 4. Цель «более живые анимации» — диагноз и варианты

Что реально создаёт ощущение «живости» в играх:
1. **Реактивность** — персонаж меняет позу/фазу под ввод и траекторию без задержки.
2. **Отсутствие «резиновых» переходов** — правильный выбор кадра/смешивание, а не только crosfade.
3. **Контакты** — стопы не скользят, руки цепляются, вес передаётся.
4. **Вариативность** — повторы не заметны.
5. **Вторичное движение / намерение** — lean, look-at, aim offset, дыхание.

| Подход | Реактивность | Переходы | Контакты | Вариативность | Детерминизм/сеть | Риск |
|---|---|---|---|---|---|---|
| Текущий Animator + blend tree | средняя | ручные crossfade | IK частично | низкая | ✅ | ✅ низкий |
| **Motion Matching (MxM / custom)** | ✅ высокая | ✅ данные вместо графа | ⚠️ + IK | ✅ из базы клипов | ✅ CPU/deterministic | ⚠️ нужна база, Unity 6.5 совместимость |
| Animation Rigging (уже стоит 1.4.1) | — | — | ✅ foot/hand IK | — | ✅ | ✅ уже установлен |
| Процедурные additively-слои (lean/look-at) | ✅ | — | ⚠️ | ✅ | ✅ | ✅ дёшево |
| **MB offline (baker)** | нет (клип фиксирован) | ✅ точечно | ⚠️ гейты | ⚠️ по клипу | ✅ baked | ⚠️ ретаргет/валидация |
| **MB runtime (T-MB06)** | ✅ | ✅ | ⚠️ + наш IK | ✅ | ❌ | ❌ 380–758 MB, CUDA, p95 |
| Kimodo / prompt-to-motion (offline) | нет | ✅ | ⚠️ | ✅ | ✅ baked | ⚠️ body-only |

**Вывод:** если цель сформулирована как «живые персонажи», а не «нейросеть в проекте», — **MotionBricks не является ни первым, ни вторым выбором**. Первый выбор — Motion Matching для locomotion игрока (он же индустриальный стандарт: UE 5.4+ Pose Search, Unity deprecated Kinematica → third-party MxM), второй — Animation Rigging + процедурные слои. MotionBricks/KimodoOffline имеет смысл как **источник недостающих клипов**, а не как замена системы.

Отдельно: в нашем проекте уже есть 1344 FBX (~6.3 GB) и рабочий KL Walk01 — то есть **проблема coverage не в дефиците**, проблема selection/blending/reactivity. Это делает MM ещё более предпочтительным и bake ещё менее оправданным.

---

## 5. Прямой ответ: «печь переходы через MB?»

**Как системный пайплайн — нет.** Локальный эксперимент уже дал отрицательный результат ([15](15_LOCOMOTION_TRANSITION_PACK_V1.md), [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)): 22 запрошенных перехода, 0/22 PASS валидатора, 9/22 с slide ≤2 см, максимум 11.32 см/кадр; пользователь отклонил как «не лучше исходного Animator».

Почему это не «допилить IK ещё чуть-чуть», а системное ограничение:
1. MB — генератор по context/target-позам, а не каталог готовых `.anim`. Каждый клип требует контекста, целей, решения о длительности.
2. `NetworkPlayer` ставит `Speed=5/10` и двигает `CharacterController` независимо от клипа → нужен motion coupling, которого baked-клип не имеет.
3. 8 направлений × start/stop × walk/run/sprint × turn = десятки переходов, каждый со своей проверкой — возврат к тому «ковырянию», от которого уходили.
4. Текущий crosfade Animator уже приемлем; offline-переход меняет локальную позу, но не решает управление скоростью и фазой шага.
5. Bake теряет главное преимущество MB — генерацию под текущий ввод.

**Когда bake Всё-таки уместен** (узкий список, не «на всякий случай»):
- клипа нет вообще и мокап/аниматор дороже (редкий vault, sit, pick-up, нестандартный turn, hit reaction — но **не attack**);
- переход в стиле, которого нет в библиотеке (injured/stealth/crouch-strafe) под конкретную сцену;
- нужна вариативность для фоновых NPC, где достаточно 3–5 запечённых вариантов.

**Гейты приёма клипа:** валидатор PASS; foot slide ≤2 см/кадр; нет T-pose; equipment на месте; `OnAttackImpact` и боевые клипы не трогаем; root motion через `MotionBricksLocomotionBridge` и единственный `_controller.Move` (T-MB04c); target-позы в local space (Floating Origin). Если гейты не проходят — клип выбрасывается, а не «допилывается неделю».

---

## 6. Итоговая рекомендация

1. **Оставить текущий Animator дефолтом.** MB — аддитивный слой за feature flag, откат в минуты. Это уже реализовано (`mode = 0` в main).
2. **Не возобновлять offline transition pack как линию разработки.** Он отклонён по контентным причинам, а не из-за нехватки IK-правок.
3. **Если цель — «живые анимации», вложиться в Motion Matching + Animation Rigging + процедурные слои** для игрока и ближних NPC. Это детерминированно, дёшево, сетево дружелюбно и прямо бьёт по реактивности/переходам/контактам.
4. **MB использовать точечно как offline-фабрику** по закрытому списку недостающих клипов с гейтами из §5 — вместе с Kimodo/prompt-to-motion там, где нужен контроль через keyframes/текст. Обратный пример из motionbricks-studio показателен: MB для реакций/переходов, Kimodo для поз, **руки для атак**.
5. **T-MB06 (runtime neural) оставить как эксперимент** и не делать из него shipping-путь до прохождения уже сформулированных критериев [17]: живой визуал A/B Ctrl+F10, equipment/body swap, FPS/VRAM, p95 end-to-end и host-client 1/2/4 игрока. Если визуал не выигрывает у Animator+MM заметно — stop, а не «ещё немного подкрутить».
6. **Не ставить Sentis/модель в `manifest.json`/билд** без отдельного одобрения: 380–758 MB, конфликт версий Burst/Collections/Newtonsoft, недетерминизм.
7. **Юридически:** код Apache 2.0, docs/media CC-BY-4.0, модели NVIDIA Open Model Agreement (коммерческое использование разрешено с условиями), sample motions — eval-only, выход модели не принадлежит NVIDIA. Нужен NOTICE/attribution; sample-позы не использовать как production-контент.

**Одной фразой:** MotionBricks нужен не «чтобы анимировать персонажа», а чтобы **дозаполнить дыры в анимационном контенте и, в эксперименте, проверить реактивную генерацию**; «живость» же в основном покупается Motion Matching + IK, а массовое запекание переходов мы уже пробовали и отвергли.

---

## 7. Источники

**Первичные (веб, ресерч 18.11.2026):**
- https://github.com/NVlabs/motionbricks (README, лицензии, структура)
- https://raw.githubusercontent.com/NVlabs/motionbricks/main/docs/guided_tutorial.md
- https://raw.githubusercontent.com/NVlabs/motionbricks/main/docs/runtime_api.md
- https://nvlabs.github.io/motionbricks/ (Smart Locomotion / Smart Objects / Uncut demo / code release)
- https://arxiv.org/abs/2604.24833 · https://research.nvidia.com/labs/gear/motionbricks/pdfs/motionbricks_siggraph_2026.pdf
- https://github.com/limeflash/motionbricks-studio (offline-фабрика для Roblox-игры)
- https://github.com/Tide-Engine/tide-motion (connect clips, GLB, endpoint correction)
- https://github.com/OneYoungMean/KimodoUnityBridge · https://github.com/shahwaizse/promptimate · https://github.com/htdt/kimodo-practical
- https://github.com/localai-org/motion-bricks.cpp (C++/Vulkan порт)
- https://discussions.unity.com/t/released-motion-matching-for-unity-advanced-character-animation/744955 (MxM)
- https://mocaponline.com/blogs/mocap-news/unreal-vs-unity-animation-comparison (UE Pose Search vs Unity, Kinematica discontinued)
- https://dev.epicgames.com/documentation/unreal-engine/motion-matching-in-unreal-engine (Motion Matching + Animation Warping)

**Локальные (уже в проекте):**
- [README.md](README.md) · [01_RESEARCH.md](01_RESEARCH.md) · [03_ALTERNATIVES.md](03_ALTERNATIVES.md)
- [15_LOCOMOTION_TRANSITION_PACK_V1.md](15_LOCOMOTION_TRANSITION_PACK_V1.md) · [16_OFFLINE_REJECTED_RUNTIME_NEXT.md](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)
- [17_T-MB06_RUNTIME_SIDECAR.md](17_T-MB06_RUNTIME_SIDECAR.md) · [HANDOFF.md](HANDOFF.md)

---

*Документ создан по правилам проекта: русский язык, без правок кода, без запуска Play Mode/тестов, без коммита.*
