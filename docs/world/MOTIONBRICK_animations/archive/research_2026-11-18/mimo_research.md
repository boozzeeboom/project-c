# mimo_research — MotionBricks в играх: как его реально встраивают и что это даёт Project C

> **Дата:** 2026-11-18
> **Тикет:** `T-MB01` (серия `T-MB`)
> **Формат:** только анализ и ресерч. Код, сцены, Play Mode, коммиты ассетов — не затрагиваются.
> **Вопрос заказчика:** как Unity/игры встраивают MotionBricks вообще; для чего он нам; что он дало того, чего не было; стоило ли через него донастраивать анимации генерацией и запеканием переходов.
> **Основа:** внешние источники (см. §7) + собственные доки серии T-MB ([01](01_RESEARCH.md), [09](09_T-MB02_ANALYSIS_AND_NEXT.md), [15](15_LOCOMOTION_TRANSITION_PACK_V1.md), [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md), [17](17_T-MB06_RUNTIME_SIDECAR.md)).

---

## 1. TL;DR

1. **Внешний ландшафт:** официального Unity/UE-плагина нет ни у кого. NVIDIA выпустила Python-runtime + ONNX; UE5-плагин из paper существует, но не вышел. В индустрии **нет ни одной шипинг-игры с MotionBricks** (i-scoop, окт. 2026). Всё, что есть, — это 5 способов интеграции, которые делают сами студии/хоббисты (§2).
2. **Для Unity экосистема предлагает:** (а) in-process через Unity Inference Engine/Sentis — готовые `.sentis`-модели на HuggingFace + коммерческий пакет `tools.doji.motionbricks`; (б) sidecar-процесс (Python/HTTP/UDP) — путь, который уже реализован у нас в T-MB06; (в) offline-bake в `.anim` — путь, который у нас **уже отклонён**.
3. **Что MotionBricks дал Project C реально:** не «живые анимации», а **инфраструктуру и знание**: проверенный пайплайн ретаргета SOMA→Humanoid, бейкер с валидатором и метриками, root motion в `NetworkPlayer`, foot IK, runtime sidecar с A/B-переключателем. Плюс — **отрицательный результат по offline-переходам**, который стоит денег: 22 перехода, 0/22 validator PASS, вердикт пользователя «ничуть не лучше исходного Animator».
4. **«Донастраивать наши анимации, генерируя и запекая переходы» — НЕТ.** Это уже сделано и отклонено нашими же замерами ([15](15_LOCOMOTION_TRANSITION_PACK_V1.md)/[16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)). Повторный заход = повторение отклонённого пути с теми же причинами: матрица состояний, speed coupling с `NetworkPlayer`, ручная отладка сравнима с ручным авторингом.
5. **Единственный незакрытый вопрос по MotionBricks** — runtime (T-MB06): код и standalone-проверки есть, живой визуал/FPS/VRAM/1-2-4 клиента **не приняты**. Go/no-go по уже записанным критериям ([16 §4.4](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)) — вот что стоит закрыть, а не изобретать новые offline-паки.
6. **Если цель №1 — «более живые анимации персонажа» сегодня**, дешевле и надёжнее: additive/lean/turn-in-place слои в текущем Animator, Animation Rigging (уже стоит), больше authored-клипов из библиотеки, и — как отдельный спайк — motion matching (MxM бесплатен). MotionBricks даёт живость **только через runtime-генерацию под ввод**, и только при условии, что приёмка T-MB06 пройдёт.

---

## 2. Как MotionBricks встраивают в игры/движки: 5 найденных паттернов

Ресерч внешних реализаций (ноябрь 2026). Паттерны не взаимоисключающие — комбинируются.

### 2.1. Паттерн A — «NVIDIA-оригинал»: Python-runtime + движок-обёртка (так сделан официальный UE5-демо)

Архитектура из paper, Appendix G («UE5 Demo Implementation»):

```text
Gameplay (StateTree: locomotion / target pose sequence / falling)
    ↓ task actor (blueprint) генерирует target keyframes
Pose generator node в animation graph
    ↓ вызывает backbone только при новых target; между генерациями — чтение motion buffer
MotionBricks backbone (root → pose → VQ-decoder)
    ↓ 77 SOMA-трансформов + foot contacts
retarget → кости персонажа
    ↓ recorder сохраняет реально показанные позы → последние 4 = context следующей генерации
```

Ключевые находки, важные для нас:
- **StateTree/стейт-машина никуда не делась** — высокий уровень поведения остаётся ручным (3 состояния в демо). Нейросеть заменяет **только слой переходов/инбетвина**.
- **Инференс не каждый кадр** — только при смене target; остальное время играется буфер. Это сильно удешевляет CPU/GPU-бюджет относительно «генерация каждый кадр».
- **Двусторонняя связь gameplay↔animation:** gameplay может прервать движение в любой момент новыми кейфреймами; animation сообщает, что буфер кончается.
- В demo используются box traces для детекта интерактива, сокеты, pivot-якоря ключей — то есть MotionBricks **встраивается в штатные средства движка**, а не заменяет их.

### 2.2. Паттерн B — in-process inference в Unity через Sentis / Unity Inference Engine

Самая «готовая» Unity-интеграция, найденная в ресерче:

- **`julienkay/MotionBricks-inference-engine`** (HuggingFace): конвертированные `motionbricks_soma_fp32.sentis` (757 MB) и `motionbricks_unitree_g1_fp32.sentis` (733 MB), сохранены Inference Engine 2.6.1, загружаются без конвертации; ONNX — источник (opset 17).
- **Коммерческий Unity-пакет `tools.doji.motionbricks`** (Doji Technologies): скачивает модель через AI Model Hub (`tools.doji.model-hub`), API — `MotionBricksModels.LoadAsync()`. Это первый найденный «нажал-и-работает»-путь для Unity.
- Нюансы, которые пакет подтверждает: weight quantization Float16 «вдвое меньше, та же скорость на GPU, но ~×4 медленнее на CPU»; Uint8 «слишком меняет выход»; FP16-графы upstream в Sentis не несут выигрыша (конвертация в FP32 при импорте).

**Оценка для нас:** наш статический аудит ([01 §3.3](01_RESEARCH.md)) уже показал, что все 51 тип операторов графа совместимы с Sentis — блокеров «оператор не поддерживается» нет. Остаётся то, что не проверялось: импорт 6993 узлов, latency, VRAM. Пакет doji снимает работу по конвертации, но **не снимает** вопросы производительности и 758 MB модели.

### 2.3. Паттерн C — sidecar-процесс (внешний runtime → IPC/UDP → Unity)

- Community-мосты (`motionbricks-unity-bridge` и аналоги): FastAPI/CLI сервер на официальном Python runtime + Unity C# клиент + Clip Baker.
- **Наш T-MB06** ([17](17_T-MB06_RUNTIME_SIDECAR.md)): официальный `SOMACharacter` + StateMachine + Smart Primitives + TensorRT в Python-процессе, UDP loopback (порт 39360), `MotionBricksRuntimePlayback` в Unity, Ctrl+F10 A/B. Это ровно этот паттерн — и, по ресерчу, он считается **самым быстрым способом честно проверить runtime** (§4.2 в [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md) рассуждал так же).
- Внешние данные по этому пути: self-test 480 кадров PASS, тёплый inference p50/p95 **5.107/32.302 ms** (наш замер, [17](17_T-MB06_RUNTIME_SIDECAR.md)); у community — ~2.37 ms avg со спайком до ~57.5 ms на RTX 5070 Ti.

### 2.4. Паттерн D — offline-bake в `.anim` → обычный Animator

Путь «сгенерировать офлайн → ретаргет → запечь → играть через существующий Animator» — рекомендованный в [01](01_RESEARCH.md) «лучший fit» и **фактически испробованный нами**: T-MB02 (baker/retarget/playback), T-MB03 (batch/валидатор/IK), transition pack v1 (22 клипа). Итог — отклонён ([16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)). См. §5 этого документа.

### 2.5. Паттерн E — экзотика: C++/GGML-порты и batch для толпы

- **`motion-bricks.cpp` / `kimodo.cpp`** (localai-org и др.): перенос весов в GGUF/GGML, CPU/Vulkan backend, стабильный C ABI → из любого движка (демо-выход HTTP JSON → Three.js). Появился вариант «без Python и без NVIDIA-стека» — но это ещё один runtime-стек, который надо поддерживать.
- **`patch-gg/crowdbricks`**: **батчинг** — движение сотен персонажей за один вызов модели (~10k generations/s на RTX 4090 по заявлению автора, batch 26× быстрее поштучного). Это единственный найденный ответ на «а как же много NPC» — и он меняет экономику для толпы... но остаётся вне Unity-контуром (TensorRT-сервис) и не проверен нами.
- **Reddit r/Unity3D** — рабочий third-person controller на MotionBricks в Unity (keyframes → realtime, «tiny model on your PC» — маркетинговая формулировка; модель 380–758 MB). Доказывает осуществимость, не доказывает production-пригодность.

### 2.6. Сводная таблица паттернов

| Паттерн | Где модель | Детерминизм | Модель в билде | Наш статус | Внешний статус |
|---|---|---|---|---|---|
| A. Python runtime + движок (как UE5 demo) | вне процесса | ⚠️ | ❌ | реализован как C (sidecar) | официальный демо-путь |
| B. Sentis in-process | в Unity | ⚠️ | ✅ 757 MB | op-аудит есть, импорт не проверен | пакет doji готов к использованию |
| C. Sidecar IPC/UDP | вне процесса | ⚠️ | ❌ | **T-MB06, код готов, визуал pending** | community-мосты |
| D. Offline bake → `.anim` | не нужна | ✅ | ❌ | **сделан и ОТКЛОНЁН** | основной «продуктовый» совет в статьях |
| E. GGML-порт / batch-толпа | свой runtime | ⚠️ | ⚠️ | не рассматривался | ранний stage, вне Unity |

---

## 3. Что говорит внешний рынок (важно для решений)

- **Ни одна шипинг-игра не использует MotionBricks** (i-scoop, 29.10.2026). Runtime в релизе — «Python reference, а не engine-плагин». Вывод статьи: годится для прототипирования, previs, crowd/NPC-поведения, **не как drop-in замена hero-персонажу**.
- **NVIDIA не выпустила UE5-плагин**, которым пользовалась в paper, — только ONNX + Python. Unity-плагина нет вообще.
- **Устоявшаяся альтернатива — motion matching**, он уже в UE5 из коробки (бесплатный Game Animation Sample на 500+ клипов). Для Unity — MxM (Asset Store, free) / JLPM22 (OSS). Motion matching **выбирает** готовый фрагмент из базы; MotionBricks **генерирует** новый. Генерация умеет то, чего нет в базе (переходы, которых не снимали), но и наследует дыры training-данных.
- **Ценность, которую статья называет реальной** для средней студии: не 2 ms, а **стоимость авторинга** — меньше переходных клипов снимать/чистить/зашивать в граф. Paper цитирует Assassin’s Creed: >15 000 анимаций, 5 000 состояний, графы до 12 уровней вложенности.
- **Лицензия:** код Apache 2.0, модели — NVIDIA Open Model Agreement (коммерция разрешена), **bundled sample motions — eval-only**, для релиза нужны свои позы. Наш пайплайн это уже учитывает (рабочая ходьба — авторский клип KL).

---

## 4. Что MotionBricks дал Project C — честный актив-лист

За серию T-MB01…T-MB06 получено **реально** (по нашим докам):

| Актив | Где задокументировано | Стоимость для «живости» персонажа |
|---|---|---|
| Полный research по модели (ONNX-аудит, CPU-бенч, лицензии) | [01](01_RESEARCH.md), [04](04_SOURCES.md) | косвенно — сняты риски |
| Ретаргет SOMA→Humanoid + валидатор с метриками (slide/axis/asymmetry/penetration) | [08](08_T-MB02_RETARGET_FINDINGS.md), [09](09_T-MB02_ANALYSIS_AND_NEXT.md) | инфраструктура; валидатор полезен и для обычных клипов |
| Baker batch + metadata + GUID-stability | [11](11_T-MB03_BAKER_AND_GUARDS.md) | инфраструктура |
| Foot IK (pelvis) + 2-bone IK ног | [11](11_T-MB03_BAKER_AND_GUARDS.md), [13](13_T-MB05_LEG_IK_AND_LOOP.md) | **да** — качество контакта |
| Root motion, встроенный в `NetworkPlayer.ProcessMovement` (2.00 м/с, замерено) | [12](12_T-MB04C_ROOT_MOTION_IN_PLAYER.md), [10](10_T-MB04B_RUNTIME_HARNESS.md) | да, если пойдём по runtime/root-пути |
| Runtime sidecar: TensorRT + UDP + A/B Ctrl+F10 + fallback | [17](17_T-MB06_RUNTIME_SIDECAR.md) | **единственный реальный кандидат на «живость»** — но не принят визуально |
| Отрицательный результат offline-переходов (0/22 PASS) | [15](15_LOCOMOTION_TRANSITION_PACK_V1.md), [16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md) | **экономит месяцы** — вопрос закрыт замерами |

**Чего НЕ получено:** ни один сгенерированный клип не попал в шипинг-путь. `PlayerAnimation.controller` не менялся. Дефолт — исходный Animator. То есть **исходная цель «более живые анимации персонажа» на текущий момент не выполнена** — получена инфраструктура для проверки гипотезы.

---

## 5. Главный вопрос: «стоит ли донастраивать наши анимации, генерируя и запекая переходы?»

### 5.1. Ответ: **нет — это уже проверено и отклонено**

Мы не гадаем: у нас есть эксперимент с замерами ([15](15_LOCOMOTION_TRANSITION_PACK_V1.md)/[16](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)):

| Факт | Значение |
|---|---|
| Сгенерировано переходов | 22 |
| Прошли валидатор | **0/22** |
| Foot slide ≤ 2 см/кадр | 9/22 (максимум 11.32 см/кадр) |
| Подключены к боевому Animator | нет |
| Вердикт пользователя | «ничуть не лучше того, что было, и требует 100500 отладки» |

Причины отказа — **системные**, их не вылечить «ещё одной итерацией пака»:

1. **MotionBricks — генератор по target-позам, а не библиотека Unity-клипов.** Каждый переход = контекст + target + решение о длительности/скорости + ручная валидация.
2. **Скорость управляется не клипом.** `NetworkPlayer` мгновенно ставит `Speed=5/10` и двигает `CharacterController`; клип идёт со своей кривой → нужен motion coupling, которого у offline-клипа по определению нет. Это не баг пака, а свойство offline-подхода.
3. **Матрица состояний.** 8 направлений × start/stop × walk/run/sprint × turn = десятки переходов, каждый с отладкой. Мы вернёмся ровно к тому «ковырянию», от которого хотели уйти — с дополнительным слоем ретаргета.
4. **Crossfade текущего Animator уже приемлем** по нашим же замерам — локальное улучшение позы перехода не решает системную задачу.
5. **Offline-bake убивает главное свойство модели** — генерацию под текущий ввод. То есть платим цену нейросети (ретаргет, валидация, лицензия), а получаем просто «ещё один клип», который можно было бы сделать в Blender/взять из библиотеки.

Вывод: **не возобновлять offline transition packs, не полировать IK под них, не подключать их к `PlayerAnimation.controller`.** Пайплайн (baker/валидатор/retarget) хранить как инструмент на случай конкретного одиночного запроса (§5.3).

### 5.2. Тогда для чего MotionBricks вообще — три честных сценария

| # | Сценарий | Даёт живость? | Цена | Статус |
|---|---|---|---|---|
| **1** | **Runtime-генерация locomotion для owner-игрока** (T-MB06): персонаж в реальном времени получает позы, адаптированные под ввод/скорость/повороты, без ручной матрицы переходов | **Да — единственный**, кто это даёт | NVIDIA GPU + sidecar/модель, недетерминизм, owner-only, нужен fallback | код готов; **живой визуал, FPS/VRAM, 1/2/4 клиента не приняты** |
| **2** | **Одиночные интеракции, которых нет в библиотеке** (vault, sit, pick-up, подъём/спуск) через Smart Object target-позы, запечь в `.anim` | точечно, да | ретаргет/валидация на каждый случай | отложено; возможно только по продуктовому запросу |
| **3** | **Донастройка существующих locomotion-переходов генерацией** | нет — проверено | все недостатки §5.1 | ❌ отклонено |

Отдельно: **для NPC/runtime толпы** наш MMO-аргумент исторически «нет», но внешний ресерч показал переменную — `crowdbricks`-батчинг (сотни персонажей за вызов). Это не снимает недетерминизм/сетевые вопросы и не проверено нами; фиксируем как «позже посмотреть», не как план.

### 5.3. Если всё же понадобится один конкретный интерактив (vault/sit)

Порог входа низкий, если задача **одиночная и продуктовая**: бейкер и валидатор уже есть, target-позы авторятся в Unity, клип играет через существующий override-controller. Это не «система переходов», а один клип — и стоимость оправдана только если без него никак. Без продуктового запроса — не начинать.

---

## 6. Рекомендация: что делать с MotionBricks дальше и чем заменить «живость»

### 6.1. По MotionBricks — закрыть вопрос замерами, а не новыми экспериментами

1. **Довести T-MB06 до честного go/no-go** по уже записанным критериям ([16 §4.4](16_OFFLINE_REJECTED_RUNTIME_NEXT.md)): живой визуал после fix 17.11, p50/p95 end-to-end, FPS/VRAM, 1/2/4 клиента host-client, fallback без T-pose. Это **дёшево** (код есть) и закрывает тему: либо runtime — фича, либо MotionBricks уходит в «R&D/архив» с задокументированным пайплайном.
2. **Не начинать** новые offline-паки, не дорабатывать rejected pack, не генерировать переходы «на всякий случай».
3. **Не устанавливать Sentis** в проект, пока runtime-путь не доказан: паттерн B (§2.2) — это потенциально более чистый вариант ТОГО ЖЕ runtime (без Python-sidecar), но 757 MB + импорт 6993 узлов — отдельный timeboxed spike, только если T-MB06 покажет, что runtime нужен, а sidecar мешает.
4. Пакет doji (`tools.doji.motionbricks`) и `.sentis`-модели **зафиксировать в источниках** ([04](04_SOURCES.md)) как самый дешёвый путь к in-process инференсу, если до этого дойдём.

### 6.2. Если цель — «более живые анимации персонажа» прямо сейчас (приоритет по стоимости)

| Шаг | Что даёт | Цена/риск |
|---|---|---|
| **1. Полировка текущего Animator** | lean в поворотах, turn-in-place, phase-matched crossfade, better idle-варианты, additive breathing/weapon layers | дёшево, детерминировано, ничего не ломает в сети/бою |
| **2. Animation Rigging (уже в проекте)** | foot IK на реальных клипах, look-at, hand pins — убирает «мёртвость» позы | дёшево; наш IK-код частично уже есть |
| **3. Больше authored-контента** | у библиотеки Kevin Iglesias есть неиспользованные клипы/переходы; `AnimatorOverrideController` уже в пайплайне `T-INP-08` | средняя цена, всегда работает |
| **4. Motion matching спайк (MxM, free)** | бесшовная locomotion без ручной матрицы — ровно то, чего мы хотели от offline-пака, но **из готовых клипов**, детерминированно, без модели | средняя; проверить совместимость MxM с Unity 6.5; нужна база locomotion-клипов |
| **5. MotionBricks runtime (T-MB06)** | уникальная адаптация под ввод + стили + интеракции «под угол объекта» | высокая; **только после приёмки замеров** |

Пункты 1–3 не конкурируют с MotionBricks — они закрывают «живость» в шипинг-пути. Пункт 4 закрывает ту же боль (переходы), но штатными средствами. Пункт 5 — единственное, что MotionBricks делает принципиально иначе всех остальных.

### 6.3. Одна строка для решения

> MotionBricks не сделал наши анимации живее — он сделал нас знающими (пайплайн, валидатор, ретаргет, runtime-контур) и дал измеренный отказ от offline-переходов. Живость персонажа сейчас решается классическими слоями (Animator polish + Rigging + контент, при желании — motion matching); в MotionBricks имеет смысл вкладываться только в одну ветку — принять или закрыть runtime T-MB06 по замерам.

---

## 7. Источники (внешние)

| Источник | Что взято |
|---|---|
| [Project page NVlabs](https://nvlabs.github.io/motionbricks/) | обещания (15k FPS / 2 ms / 350k), code release, smart primitives |
| [GitHub NVlabs/motionbricks](https://github.com/NVlabs/motionbricks) | runtime API, ONNX, demos, редакторы, лицензии |
| [Paper arXiv:2604.24833](https://arxiv.org/abs/2604.24833) (HTML) | **Appendix G: архитектура UE5-демо** — StateTree, task actors, pose generator node, инференс по событию, box traces, bidirectional gameplay↔animation; §6.1/6.2 Smart Locomotion/Object |
| [i-scoop: NVIDIA MotionBricks…](https://www.i-scoop.eu/nvidia-motionbricks/) (29.10.2026) | **нет шипинг-игр**; UE5-плагин не выпущен; motion matching как устоявшаяся альтернатива; лицензионная таблица; «прототипирование/previs/crowd, не drop-in hero»; слабые места (редкие скиллы, нет физики, ретаргет) |
| [HuggingFace julienkay/MotionBricks-inference-engine](https://huggingface.co/julienkay/MotionBricks-inference-engine) | `.sentis`-модели (757/733 MB), Inference Engine 2.6.1, пакет `tools.doji.motionbricks`, API `MotionBricksModels.LoadAsync()`, поведение FP16/квантизации |
| [Reddit r/Unity3D: third-person controller](https://www.reddit.com/r/Unity3D/comments/1w6xetg/) | факт рабочей Unity-интеграции community; маркетинг «tiny model» |
| [Tripo: MotionBricks in Unity](https://www.tripo3d.ai/blog/ai-animation-unity-third-person-controller) | рекомендованный слоёный подход интеграции: валидация рига → отдельный runtime bridge → controller → fallback; «не изобретать API по видео» |
| [patch-gg/crowdbricks](https://github.com/patch-gg/crowdbricks) | batch-генерация для сотен персоней (TensorRT), заявление ~10k gen/s на 4090 |
| [motion-bricks.cpp](https://github.com/localai-org/motion-bricks.cpp/blob/main/docs/IMPLEMENTATION.md) | GGML/C++-порт, C ABI, HTTP→Three.js; вариант runtime без Python/NVIDIA-стека |
| [nvidia/MotionBricks (HF)](https://huggingface.co/nvidia/MotionBricks) | модели SOMA/G1, model cards, terms |
| Unity Sentis docs (пакет `com.unity.ai.inference` 2.6.1) | opset/операторы — наш аудит в [01 §3.3](01_RESEARCH.md) |

Локальные измерения и вердикты проекта — в доках серии T-MB (§1 этого документа); они не дублируются здесь.

---

*Формат: mimo_research. Только анализ/ресерч; код и ассеты не менялись; Play Mode не запускался.*
