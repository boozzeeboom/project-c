# MotionBricks — official vertical slice на собственном движущемся контексте

> **Дата:** 2026-11-16
> **Тип:** read-only к production runtime; экспериментальный offline probe, без нового тикета в трекере.
> **Статус:** выполнен 2026-11-16; Humanoid-клип принят пользователем в Animation Window; результаты — [14_OFFICIAL_VERTICAL_SLICE.md](../world/MOTIONBRICK_animations/14_OFFICIAL_VERTICAL_SLICE.md).
> **Ограничения:** Play Mode не запускать; Animator/controller/NetworkPlayer/NPC/Combat/Package не менять; model/TensorRT/CUDA в build не добавлять.

## Цель

Разделить три ранее смешанных вопроса на одном воспроизводимом примере:

1. Что выдаёт **официальный** `MotionBricksSOMAEngine` на правильном контексте реального движения?
2. Отличается ли текущий `sa-inbetween` на тех же входах и конфигурации?
3. Сохраняется ли смысл исходного SOMA-движения после текущего Unity baker без IK/Humanoid?

## Вертикальный срез

- Источник: собственный для проекта KL `HumanM@Walk01_Forward [RM]`, 30 Hz, 25 кадров.
- Context: четыре последовательных кадра начала walking clip (индексы 0–3).
- Target: четыре последовательных кадра в конце того же клипа (индексы 20–23), спустя 0.567 s пути.
- Обратный ретаргет KL→SOMA: inverse текущего world-rest-delta; пропорции SOMA берутся из официального USD, root масштабируется по длине ноги.
- До генерации: roundtrip SOMA raw→KL world rotations→SOMA raw; mapped rotations должны совпасть после inverse (численный допуск), non-finite значений быть не должно.
- Backend A: официальный `MotionBricksSOMAEngine`, identity model/runtime space, **фиксированные 6 токенов** (18 кадров = 0.567 s), endpoint correction включён, четыре target poses.
- Backend B: `sa-inbetween`, те же `.pose`, `--override 6`, endpoint correction включён, четыре target poses.

Почему 6 токенов, а не дефолтные 8–14: источник проходит путь target за 0.567 s. При дефолтном выборе модель взяла 14 токенов (50 кадров, 1.63 s) и растянула те же 0.64 м до 0.39 м/с. Это отдельное наблюдение о выборе длительности, а не ошибка Unity-пайплайна; фиксированный минимум позволяет проверить именно двигательную семантику без искусственного slow motion.
- Unity import: только generic Transform clip, `groundFeet:false`, `legIk:false`, без Humanoid, IK, playback компонентов и root-motion bridge.

## Явные non-goals

- Не добиваться красивой ходьбы дополнительным IK.
- Не проверять production locomotion, M/F swap, equipment, NGO/FO, combat.
- Не генерировать production-контент из NVIDIA sample motions. Sample library в этом срезе не используется.
- Не называть результат визуально принятым: визуальный probe выполняет пользователь.

## Решение по результатам

| Наблюдение | Вывод |
|---|---|
| Плохо уже в исходном SOMA output обоих backend | Вход/модель/сценарий, а не Unity baker |
| Official PASS, community FAIL | Проблема community backend/совместимости |
| SOMA output хорош, Unity roundtrip FAIL | Наш inverse/forward retarget |
| SOMA output хорош, baked clip отличается | Текущий SOMA→KL baker |
| Всё хорошо по числам | Нужен пользовательский визуальный sign-off; production-accepted не объявлять |

## Артефакты

- Локальные входы/выходы: `Assets/_Project/Animations/Generated/MotionBricks/official_vertical_slice/Male/` (в git не добавлять).
- Воспроизводимые tools: `docs/world/MOTIONBRICK_animations/tools/`.
- Итоговый численный отчёт и runbook: новый документ рядом с `01`–`13`, обновление HANDOFF/ITERATIONS одним документирующим изменением после результата.
