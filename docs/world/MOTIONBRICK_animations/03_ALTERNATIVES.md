# T-MB01 — Альтернативы MotionBricks для Project C

> **Дата:** 2026-11-15
> **Связанные документы:** [README.md](README.md) · [01_RESEARCH.md](01_RESEARCH.md) · [02_PIPELINE.md](02_PIPELINE.md) · [04_SOURCES.md](04_SOURCES.md)
> **Цель:** выбрать не «самую крутую технологию», а наименьший риск для MMO-клиента с server-authoritative боем и ограниченным бюджетом.

---

## 1. Критерии

| Критерий | Что значит |
|---|---|
| Runtime cost | CPU/GPU/память на клиенте |
| MMO scale | Работает ли для десятков/сотен NPC |
| Quality | Насколько хорошо выглядит и не ломает ноги/руки |
| Coverage | Locomotion / traversal / interactions / combat / fingers / face |
| Determinism | Одинаково ли у сервера/клиентов |
| Authoring speed | Сколько времени у аниматора/программиста |
| Integration risk | Сколько кода/багов/зависимостей |
| License | Можно ли шипить коммерчески |
| Build size | Сколько MB добавляет |
| Unity 6.5 fit | Совместимость с 6000.5.2f1 |

---

## 2. Варианты

### 2.1. Baseline: Animator + клипы + `AnimatorOverrideController`

**Что это:** текущий пайплайн Project C. `PlayerAnimation.controller`, NPC controller, `SkillAnimationPlayer`, `AnimatorOverrideController`, Animation Events.

**Плюсы:**
- Уже работает; ноль новых зависимостей.
- Детерминизм; события; IK; equipment.
- Аниматоры понимают.

**Минусы:**
- Много состояний/переходов; сложно поддерживать.
- Нет адаптации под скорость/стиль за пределами blend tree.
- Переходы нужно делать руками.

**Когда:** всегда как база; не заменять целиком.

---

### 2.2. Animancer (Playable API)

**Что это:** платный Unity-плагин, играет клипы «по требованию» через Playables, без жёсткого Animator Controller.

**Плюсы:**
- Гибкое управление состояними/переходами/слоями.
- Меньше «магии» Animator Controller.
- Хорошо для NPC/скиллов; можно комбинировать с baked-клипами.
- Не требует neural/GPU.

**Минусы:**
- Платный; ещё одна зависимость.
- Не генерирует клипы; покрытие то же.
- Нужно переписать текущие состояния/переходы.

**Когда:** если Animator Controller станет тормозом/сложным; для NPC с разнообразными состояниями.

---

### 2.3. Motion Matching

**Что это:** data-driven поиск позы/окна клипа по текущему состоянию и траектории ввода; альтернатива state machine для locomotion.

**Варианты:**
- **Motion Matching for Unity (MxM)** — Asset Store, **free**, v2.3.2 (Mar 2025), original Unity 2021.3.45; community fork `Frost-Blade-Studios/Motion-Matching-for-Unity`. Проверить совместимость с Unity 6.5.
- **JLPM22/MotionMatching** — open-source research package (`com.jlpm.motionmatching`), master's thesis; менее production-ready.
- **Custom** — свой поиск/BVH; дорого.

**Плюсы:**
- Отличный locomotion: ходьба/бег/стопы/повороты без ручных переходов.
- Детерминизм (CPU, данные).
- Нет 758 MB модели; работает на всех клиентах.
- Хорошо сочетается с IK и baked-клипами.

**Минусы:**
- Нужна большая качественная база locomotion-клипов.
- Не решает object interactions/combat.
- Настройка/отладка; CPU cost на персонажа; для толпы — LOD.
- MxM официально 2021.3; на Unity 6.5 может потребоваться fork/фиксы.

**Когда:** для игрока и ближних NPC; если locomotion — главная боль.

---

### 2.4. Animation Rigging / IK

**Что это:** `com.unity.animation.rigging 1.4.1` уже в проекте. TwoBoneIK, MultiAim, LookAt, ChainIK.

**Плюсы:**
- Дешёво; детерминизм; работает с любыми клипами.
- Foot IK, look-at, aim, hand pins.
- Уже установлено.

**Минусы:**
- Не создаёт движение; только корректирует.
- Может конфликтовать с neural bone writes/порядком обновления.

**Когда:** всегда для качества контакта стоп/рук; обязательный слой поверх baked-клипов.

---

### 2.5. MotionBricks offline baker

**Что это:** генерация коротких клипов offline, запекание в `.anim`, playback через Animator/Animancer.

**Плюсы:**
- Закрывает переходы/стили/взаимодействия, которых нет в ассетах.
- Нет модели в билде; нет NVIDIA у игроков.
- Детерминизм; события; equipment.
- Вписывается в `T-INP-08`.

**Минусы:**
- Клипы фиксированы; нет runtime-адаптации.
- Нужен ретаргет/валидация.
- Зависимость от Python/TensorRT или ONNX Runtime на машине аниматора.
- Лицензия/attribution.

**Когда:** когда не хватает конкретных клипов; лучший fit для Project C.

---

### 2.6. Kimodo (NVIDIA, offline)

**Что это:** kinematic motion diffusion, 700 ч mocap, text + kinematic constraints, SOMA/G1; offline.

**Плюсы:**
- Богатый контроль: текст, keyframes, end-effectors, waypoints.
- Высокое качество; SOMA.
- Подходит для bespoke cinematics/emotes.

**Минусы:**
- Diffusion — медленно; не runtime.
- Нет Unity-интеграции; Python.
- 700 ч данных/модель; лицензия NVIDIA Open Model Agreement.
- Не решает MMO-scale.

**Когда:** offline authoring сложных сцен/эмоций; альтернатива MotionBricks baker для текстового контроля.

---

### 2.7. ARDY (NVIDIA, real-time research)

**Что это:** autoregressive diffusion с hybrid representation; real-time; text + long-horizon constraints; SIGGRAPH 2026.

**Плюсы:**
- Real-time; текст + waypoints/keyframes.
- Более гибкий, чем MotionBricks, по контролю.

**Минусы:**
- Research; нет Unity; тяжёлый; неясна latency на consumer GPU.
- Не для MMO-толпы.
- Лицензия/модели.

**Когда:** если нужен runtime с текстовым контролем и есть GPU; пока не для production.

---

### 2.8. Unity Muse Animate

**Что это:** Unity-пакет `com.unity.muse.animate` 1.0; prompt-based генерация анимаций в Editor.

**Плюсы:**
- В Editor; не нужно внешнее ПО.
- Быстрый прототип.

**Минусы:**
- Требует Muse/облако/подписку (проверить текущие условия).
- Ограниченный контроль; не для точных interaction/combat.
- Не evaluated в проекте; зависимость от сервиса.

**Когда:** быстрый прототип, не production.

---

### 2.9. Video mocap

**Что это:** Move.ai, Rokoko Vision, Plask, DeepMotion Animate 3D и т.п. — видео → FBX.

**Плюсы:**
- Свои движения; хорошее качество для человеческих действий.
- FBX → Unity Humanoid; привычный пайплайн.
- Нет модели в билде.

**Минусы:**
- Платно/по минутам; нужна запись/актёр.
- Ограничения по ракурсу/одежде.
- Не генерирует «из воздуха»; нужен источник.

**Когда:** bespoke анимации, emotes, cinematics, тесты.

---

### 2.10. Cascadeur

**Что это:** physics-assisted keyframing tool; FBX.

**Плюсы:**
- Быстрые акробатические/физичные движения.
- Контроль аниматора; нет neural runtime.
- Хорошо для vault/jump/fall.

**Минусы:**
- Ручной труд; лицензия; экспорт/импорт.
- Не для массовой генерации.

**Когда:** сложные traversal/акробатика, где baker даёт мусор.

---

### 2.11. Full neural runtime (MotionBricks/Sentis/ORT/TensorRT)

**Что это:** генерация в runtime на клиенте.

**Плюсы:**
- Адаптация под игрока/стиль/цели.
- «Умные» переходы без клипов.

**Минусы:**
- 758/380 MB модель; 200M params; NVIDIA/TensorRT или тяжёлый ONNX.
- Недетерминизм; сеть/хитбоксы.
- MMO-scale — нет.
- Высокий integration risk.

**Когда:** только experimental, feature flag, один игрок, Windows+NVIDIA.

---

## 3. Сравнительная матрица

Обозначения: ✅ хорошо, ⚠️ условно, ❌ плохо/нет.

| Вариант | Runtime cost | MMO scale | Quality | Coverage | Determinism | Authoring | Risk | License | Build size |
|---|---|---|---|---|---|---|---|---|---|
| Baseline Animator | ✅ | ✅ | ⚠️ | ✅ (что есть) | ✅ | ⚠️ | ✅ | ✅ | ✅ |
| Animancer | ✅ | ✅ | ⚠️ | ✅ (что есть) | ✅ | ✅ | ⚠️ | 💰 | ✅ |
| Motion Matching (MxM) | ⚠️ | ⚠️ (LOD) | ✅ locomotion | ❌ interactions | ✅ | ⚠️ | ⚠️ | ✅ free | ✅ |
| Animation Rigging | ✅ | ✅ | ✅ contact | ❌ генерация | ✅ | ✅ | ✅ | ✅ | ✅ |
| MotionBricks baker | ✅ | ✅ | ✅ transitions | ⚠️ body only | ✅ | ⚠️ | ⚠️ | ⚠️ NVIDIA | ✅ |
| Kimodo | ❌ offline | ✅ offline | ✅ | ✅ text/constraints | ✅ (baked) | ⚠️ | ⚠️ | ⚠️ NVIDIA | ✅ |
| ARDY | ❌ | ❌ | ✅ | ✅ text/constraints | ⚠️ | ❌ | ❌ | ⚠️ NVIDIA | ❌ |
| Muse Animate | ✅ editor | ✅ | ⚠️ | ⚠️ | ✅ (baked) | ✅ | ⚠️ | 💰 cloud | ✅ |
| Video mocap | ✅ | ✅ | ✅ | ✅ custom | ✅ | ⚠️ | ✅ | 💰 | ✅ |
| Cascadeur | ✅ | ✅ | ✅ | ✅ custom | ✅ | ⚠️ | ✅ | 💰 | ✅ |
| Neural runtime | ❌ | ❌ | ✅ | ⚠️ body only | ❌ | ❌ | ❌ | ⚠️ NVIDIA | ❌ |

---

## 4. Рекомендация по сценариям

| Задача | Рекомендация | Почему |
|---|---|---|
| **Locomotion игрока** | Motion Matching (MxM/custom) + Animation Rigging | Лучшее качество на клипах; детерминизм; без модели |
| **NPC locomotion** | Animator/Animancer + LOD + baked clips | Дешево; server-authoritative; не нужен neural |
| **Переходы/стили, которых нет** | MotionBricks offline baker | Точечно; без runtime-риска |
| **Vault/sit/pick-up** | MotionBricks baker + IK pins | Target-позы; клип; детерминизм |
| **Combat/attack/hit** | Authored clips + `T-INP-08` | Тайминг/события/хитбоксы |
| **Facial/fingers** | Authored/face rig | Модель не покрывает |
| **Bespoke cinematics/emotes** | Kimodo / video mocap / Cascadeur | Контроль качества |
| **Runtime-адаптация под игрока** | Только experimental neural runtime | Нет дешёвой альтернативы, но высокий риск |

---

## 5. Гибридный стек (рекомендуется)

```
┌─────────────────────────────────────────────────────────┐
│ 1. Locomotion: Motion Matching / Animator blend trees   │
│ 2. Contact: Animation Rigging (foot/hand IK)            │
│ 3. Missing transitions/styles: MotionBricks baker       │
│ 4. Combat/emotes/face: authored clips / mocap           │
│ 5. Network: state replication + baked clips             │
└─────────────────────────────────────────────────────────┘
```

**Почему не «всё через MotionBricks»:**
- MMO-scale; 758 MB; NVIDIA; non-determinism; no fingers/face; combat timing.
- Baker даёт 80% пользы за 20% риска.

---

## 6. Дерево решений

```
Нужна анимация, которой нет в клипах?
├─ Нет → Baseline Animator/Animancer + IK. Стоп.
└─ Да
   ├─ Можно сделать вручную/мокапом за разумное время?
   │  ├─ Да → Video mocap / Cascadeur / аниматор.
   │  └─ Нет
   │     ├─ Нужна адаптация в runtime под игрока/стиль?
   │     │  ├─ Нет → MotionBricks offline baker. ✅
   │     │  └─ Да
   │     │     ├─ Motion Matching решает? → MxM/custom.
   │     │     └─ Нет → Runtime neural spike (Sentis/ORT).
   │     │        ├─ p95/VRAM/quality OK? → Experimental feature.
   │     │        └─ Нет → Отказ; baker/мокап.
   └─ Есть ли NVIDIA GPU у всех игроков?
      ├─ Нет → Runtime neural нельзя.
      └─ Да → всё равно проверить build size/лицензию.
```

---

## 7. Оценка трудозатрат (грубо)

| Работа | Оценка | Комментарий |
|---|---|---|
| Baker spike (1 клип) | 3–5 дней | HTTP/ORT + импорт |
| Baker MVP | 2–3 недели | Editor window, metadata, validation |
| Retarget/IK | 1–2 недели | T-pose, foot IK, equipment |
| Hybrid primitives | 2–3 недели | Authoring, registry, playback |
| Sentis spike | 3–5 дней | Импорт + benchmark |
| Runtime experiment | 3–4 недели | Scheduler, applier, flag, QA |
| Motion Matching (MxM) | 3–6 недель | База клипов, настройка, отладка |
| Animancer migration | 1–2 недели | Переписать состояния |
| Kimodo/video mocap | по клипу | Зависит от объёма |

Оценки — для одного программиста + аниматор part-time; не обязательство.

---

## 8. Итог

- **Не** заменять Animator целиком.
- **Не** тащить neural runtime в MMO-клиент.
- **Да** — MotionBricks offline baker для точечных переходов/стилей/взаимодействий.
- **Да** — Motion Matching/Animancer/IK для runtime-качества.
- **Да** — authored clips для combat/face/fingers.
- Решение о runtime neural — только после Sentis spike и продуктовой необходимости.

---

*Источники и оговорки — в [04_SOURCES.md](04_SOURCES.md).*
