# 16 — Offline transition pack отклонён; следующий этап — полноценный runtime MotionBricks

> **Дата:** 2026-11-16
> **Ветка:** `main`
> **Решение:** offline-путь «генерировать переходы в отдельные `.anim` и подключать к Animator» остановлен. Следующий этап — **полноценный runtime-порт MotionBricks как у NVIDIA, с измерениями на host-client до 4 игроков**.
> **Код runtime в этом шаге не писался.** Изменены только документация, ignore-правила и очистка локальных артефактов.
> **Связанные:** [15_LOCOMOTION_TRANSITION_PACK_V1.md](15_LOCOMOTION_TRANSITION_PACK_V1.md) · [MOTIONBRICKS_LOCOMOTION_PACK_DESIGN.md](../dev/MOTIONBRICKS_LOCOMOTION_PACK_DESIGN.md) · [HANDOFF.md](HANDOFF.md)

---

## 1. Вердикт

Пользователь проверил результат offline transition pack:

> «Эта хрень что получилась — ничуть не лучше того, что было, и требует 100500 отладки, как и раньше».

Это принимается как **отказ от offline-направления**, а не как «ещё один повод допилить IK/переходы».

Причины отказа подтверждаются накопленными данными:

| Факт | Значение |
|---|---|
| Сгенерировано переходов | 22 |
| Прошли текущий валидатор | **0/22** |
| Foot slide ≤ 2 см/кадр | **9/22** |
| Foot slide максимум | **11.32 см/кадр** |
| Contact hover / penetration | 0.10–0.15 / ≤0.05 см |
| Клипы подключены к боевому Animator | нет |
| Требуется ручная настройка | speed coupling, state routing, direction-aware validation, controller transitions |

Высокое качество контакта не компенсирует ручную интеграцию и рассинхрон скорости/анимации.

## 2. Почему offline-путь не дал ожидаемого

1. **MotionBricks — генератор по target-позам, а не библиотека готовых Unity-клипов.**
   Каждый переход требует контекста, target poses и решения о длительности/скорости.

2. **Animator и `NetworkPlayer` управляют скоростью отдельно от клипа.**
   `NetworkPlayer` мгновенно задаёт `Speed=5/10` и двигает `CharacterController`. Клип перехода идёт 0.567 s со своей кривой. В результате нужен motion coupling, которого в offline-клипе нет.

3. **Количество состояний быстро превращается в матрицу.**
   8 направлений × start/stop × walk/run/sprint × turn даёт десятки переходов; каждый требует своей проверки и настройки. Это возвращает то самое «ковыряние», от которого хотели уйти.

4. **Crossfade текущего Animator уже работает приемлемо.**
   Offline-переходы дают локально другую позу, но не решают системную задачу управления скоростью и фазой шага.

5. **Offline-bake теряет главное преимущество MotionBricks — генерацию под текущий ввод.**
   Ровно это и нужно проверить в runtime.

## 3. Решение

Остановить и не развивать:

- новые offline transition packs;
- дальнейшую полировку foot IK для этих клипов;
- подключение сгенерированных переходов к `PlayerAnimation.controller`;
- ручные state-transition эксперименты вокруг rejected pack.

**Следующий этап: T-MB06 — полноценный runtime-порт NVIDIA.** Не отдельный клип, не preview, а рабочий контур: модель → контекст → target → playback → персонаж → измерения.

## 4. План runtime-порта

### 4.1. Что значит «нормально делаем»

Не «ещё один smoke-test TensorRT». Нужен полный runtime loop:

```text
управление игрока / состояние движения
        ↓
target poses (позиция, направление, скорость, фаза)
        ↓
context: последние 4 реально показанные позы
        ↓
MotionBricks runtime engine (TensorRT)
        ↓
endpoint correction + foot contacts
        ↓
выдача позы в Unity Humanoid
        ↓
recorder сохраняет именно показанные позы
        ↓
следующая генерация
```

Обязательные компоненты:

1. Официальный SOMA engine и TensorRT-план.
2. `MotionRecorder` / история 4×30 Hz.
3. `GeneratedMotionPlayer` и target-pose generator.
4. Связь с реальным движением персонажа.
5. Fallback на текущий Animator при ошибке/перегрузке.
6. Замеры end-to-end, а не только времени inference.

### 4.2. Первый runtimeMVP

- Windows x64 + NVIDIA, один owner-игрок.
- Только визуальный слой; authoritative movement/combat не меняются.
- Отдельный процесс **официального Python runtime** через локальный IPC — самый быстрый способ faithfully проверить runtime и получить честные цифры.
- Feature flag; по умолчанию выключен; оригинальный Animator остаётся fallback.
- Модель/TensorRT/CUDA не попадают в shipping build на этом этапе.

Если latency/качество пройдут — отдельно решить, нужен ли нативный C#/TensorRT plugin и как переносить runtime в клиент.

### 4.3. Что мерить на host-client

Не «MMO-аргументы», а конкретные замеры:

| Сценарий | Метрики |
|---|---|
| 1 клиент | p50/p95 end-to-end, GPU/VRAM, CPU, frame time |
| 2 клиента host-client | то же + network traffic, host load |
| 4 клиента host-client | p95, стабильность, память, деградация |
| 10–30 минут | утечки/перегрев/деградация |
| старт/стоп/поворот | отклик, pop, фут-контакт, визуальная приемлемость |
| fallback | отключение runtime → Animator без T-pose |

Обязательные числа: model build/load time, engine init, p50/p95 generation, playback latency, GPU memory, CPU %, FPS impact, net bytes/s, fallback count.

### 4.4. Критерии допуска

- Ввод WASD ощущается не хуже текущего Animator.
- Нет систематического foot skating/T-pose/pop.
- p95 end-to-end укладывается в согласованный бюджет после замеров.
- Runtime можно выключить одной настройкой.
- Для 4-player host-client есть измеренная стоимость.

До этих критериев runtime не становится shipping-путём.

## 5. Что сделано с мусором

Удалены локальные артефакты отклонённого offline-эксперимента (в git они не входили):

- `locomotion_pack_work/` — 92 файла;
- `locomotion_sources/` — 52 файла;
- `locomotion_transitions_v1/` — 223 файла, ~15.7 МБ;
- `official_slice_bake/` — 13 файлов;
- `official_vertical_slice/` — 152 файла;
- `playtest/` — тестовый controller;
- `walk_forward/` — старые T-MB03 debug-клипы;
- старые `walk_to_jog` `.anim/.f32/.saib/.report/.config` (кроме tracked исторического `WalkToJog_Male.bake.json`);
- `MotionBricksTransitionPreview.unity`;
- `tmp_tmb04_*.bat`.

Суммарно освобождено около **25+ МБ** локальных данных.

`.gitignore` дополнен общими правилами:

```gitignore
/Assets/_Project/Animations/Generated/MotionBricks/
/Assets/_Project/Settings/MotionBricks/
/Assets/_Project/Scenes/MotionBricksTransitionPreview.unity*
/tmp_tmb04_*.bat
```

Оставлены:

- `MotionBricksRuntimeSettings.asset` — локальный Lore-ассет, нужен runtime, но не коммитится;
- исходный код/документация MotionBricks;
- tracked `WalkToJog_Male.bake.json` — маленький исторический manifest T-MB02;
- пользовательские изменения сцен (погодный цикл/позиция корабля) — не откатывались.

Baseline-файлы не менялись: **12/12**.

## 6. Текущий статус

| Пункт | Статус |
|---|---|
| Offline locomotion transition pack | ❌ rejected |
| Генерация новых offline clips | ⏹ остановлена |
| Runtime MotionBricks (T-MB06) | ⬜ следующий этап, код ещё не начат |
| Исходный Animator | ✅ остаётся дефолтом |
| Play Mode | не запускался агентом |
| Baseline | 12/12 |
