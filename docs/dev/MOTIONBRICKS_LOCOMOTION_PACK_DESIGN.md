# MotionBricks — locomotion transition pack + runtime path

> **Дата:** 2026-11-16
> **Статус:** ❌ pack v1 отклонён: не лучше исходного Animator и требует ручной отладки. Offline-направление остановлено; следующий этап — полноценный runtime-порт. Результат — [16](../world/MOTIONBRICK_animations/16_OFFLINE_REJECTED_RUNTIME_NEXT.md).
> **Решение пользователя:** сначала **полный пакет locomotion transitions**, затем **полный runtime-порт MotionBricks как у NVIDIA** с замером производительности.
> **Пересмотр допущения:** проект больше не оценивается как MMO на сотни клиентов. Текущая модель — host-client, реалистичная цель до **4 игроков** с host-сервером. Прежние выводы «runtime невозможен, потому что MMO» считать исторической гипотезой, а не архитектурным запретом.

## 1. Почему поштучные клипы прекращаются

Текущий `PlayerAnimation.controller` уже содержит:

- `Walk_BlendTree`: 8 направлений;
- `Run_BlendTree`: 8 направлений;
- `Sprint_BlendTree`: 5 направлений;
- Idle, Jump, Fall, Land, TurnLeft/Right, CombatIdle, Attack, Damage, Death, Skill, Mine, Lumber, Gather.

Поэтому генерировать `Walk Left/Right/Backward` как отдельные новые cycling-клипы бессмысленно: это дубли существующего контента.

MotionBricks должен закрывать **переходы**, а не повторять уже готовые циклы. Единица поставки — не клип, а **domain pack**.

## 2. Полный locomotion transition pack v1

22 перехода генерируются одним batch-прогоном:

| Группа | Переходы | Кол-во |
|---|---|---:|
| Start | `Idle → Walk_{Forward,Backward,Left,Right,FwdLeft,FwdRight,BackLeft,BackRight}` | 8 |
| Stop | `Walk_* → Idle` для тех же 8 направлений | 8 |
| Скорость | `Walk_Forward ↔ Run_Forward`, `Run_Forward ↔ Sprint_Forward` | 4 |
| Поворот | `Walk_Forward → Turn_Left/Right` | 2 |
| **Итого** | | **22** |

Source clips — проектные KL Humanoid clips, RootMotion-варианты для движущихся состояний. NVIDIA sample motions не используются.

Пакет создаётся в отдельном каталоге:

```text
Assets/_Project/Animations/Generated/MotionBricks/locomotion_transitions_v1/
  <job-id>/Male/
    <JobId>_30fps.f32
    soma_bind.f32
    <JobId>.bake.config.json
    <JobId>.anim
    <JobId>_Humanoid.anim
```

Контроллер и baseline-файлы **не меняются** до визуальной приёмки пакета.

## 3. Batch architecture

```text
docs/world/MOTIONBRICK_animations/batches/locomotion_v1.json
        │ sources + transition jobs + frame windows
        ▼
Unity Edit Mode exporter
        │ все source clips → pose packs
        ▼
locomotion_pack.py
        │ inverse SOMA + target placement + official TensorRT по всем jobs
        ▼
raw .f32 per transition + metadata
        ▼
existing MotionBricks baker: humanoid/generic + validation
        ▼
batch report + preview list
```

Длительность одного перехода: 6 токенов / 18 кадров / 0.6 s. Для стартов/стопов этого достаточно; скорость задаётся target path.

## 4. Runtime path после offline pack

Отдельная следующая фаза, не смешивать с pack:

1. перенести официальный runtime loop: recorder последних показанных поз, `GeneratedMotionPlayer`, targets из управления;
2. один owner-player, Windows+NVIDIA, за feature flag;
3. замерить на 1, 2, 4 клиентах: GPU/VRAM, p50/p95 latency, frame time, network traffic;
4. определить, можно ли применять runtime только для owner и/или для NPC-хоста;
5. решение о shipping — только по измерениям, без априорного «MMO запрещает».

## 5. Stop conditions

- Если batch pack нельзя воспроизвести одной командой — архитектура не принята.
- Если переходы визуально хуже текущего 0.2 s crossfade — пакет не интегрируется.
- Если runtime порт не укладывается в бюджет 4-player host-client — фиксируется измеренный отказ, а не гипотеза.
- Baseline 12/12, `mode=0` до явного решения о runtime.
