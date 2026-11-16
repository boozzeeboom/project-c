# 17 — T-MB06: официальный MotionBricks runtime на owner-персонаже

> **Дата:** 2026-11-16
> **Статус:** runtime-код, standalone TensorRT и UDP smoke проверены; **визуальная приёмка на живом персонаже ещё не проведена**.
> **Цель:** проверить непрерывную генерацию как в preview NVIDIA, а не новые offline-переходы. Движение, бой, сеть и префабы игрока не меняются.
> **Design:** [T-MB06_RUNTIME_SIDECAR.md](../../dev/T-MB06_RUNTIME_SIDECAR.md)

## 1. Что реализовано

```text
NetworkPlayer + CharacterController
    ↓ фактические position / rotation / velocity / dt
Python sidecar: SOMACharacter + StateMachine + Smart Primitives
    ↓ GeneratedMotionPlayer + TensorRT + MotionRecorder
77 SOMA joint transforms + 4 foot contacts
    ↓ UDP loopback, latest complete frame
SOMA → Project C rig в LateUpdate
```

Это не воспроизведение заранее запечённого `.anim`: sidecar использует официальные
`SOMACharacter`, `StateMachine`, `GeneratedMotionPlayer`, `MotionRecorder`, Smart Primitive
clips и `MotionBricksSOMAEngine`. При locomotion возникают новые TensorRT-генерации;
`playback_source` различает `generated`, `clip_transition` и `clip`.

`CharacterController` остаётся единственным источником перемещения. Runtime не вызывает
`Move`, не меняет `NetworkPlayer`, не реплицирует кости и не определяет hitbox/урон.
Root генерации связывается с фактическим положением и yaw игрока; вертикальный bob
генерации сохраняется. История recorder получает уже скорректированную показанную позу.

Файлы:

- `tools/runtime_sidecar.py` — официальный runtime, внешний root, UDP, self-test и метрики.
- `MotionBricksRuntimeClient.cs` — запуск процесса, async logs, background receive,
  latest-frame, RTT/inference/process diagnostics и shutdown.
- `MotionBricksSomaRetarget.cs` — SOMA bind/rest → 22 кости Project C, world-delta rotations,
  ограничения ключиц, вертикальная поправка таза; `B-jaw` необязателен для female-ригов.
- `MotionBricksRuntimePlayback.cs` — owner-only playback, A/B, fallback и body-swap guard.
- `MotionBricksRuntimeReviewMenu.cs` — ручной Attach/Start/Stop/Detach/Diagnostics.

Attach выбирает rest template из фактических serialized-моделей `CharacterCustomisationApplier`
(`_maleModel`/`_femaleModel` по `CurrentBodyType`); HumanM/HumanF остаются fallback для KL-аватаров.
Это поддерживает текущий male `testing.fbx` с аватаром `testingAvatar` и female-после body swap.

## 2. Изоляция и переключение

Runtime находится под `#if UNITY_EDITOR`, добавляется только вручную в Play Mode и не
сохраняется в prefab/сцену. Python, TensorRT, ONNX и sample motions не копируются в `Assets`
и не попадают в build. Обычный Animator Controller не заменяется.

- **Attach + start** запускает neural preview на локальном owner.
- **Ctrl+F10** переключает neural preview и authored Animator.
- **Stop / restore Animator** оставляет sidecar живым для повторного A/B.
- **Detach** останавливает sidecar и удаляет runtime-компонент.
- Потеря owner, отключённый CharacterController, каст, stale pose, ошибка протокола или
  завершение sidecar возвращают authored Animator.
- Body swap останавливает neural preview и обновляет ссылку Animator; автоматический
  повторный запуск после свапа не выполняется.

Старый baked playback/root motion на том же игроке отключается при запуске neural preview.
Это защита эксперимента, а не production-rollout.

## 3. Протокол

UDP IPv4 loopback, little-endian, magic `MBR1`, version 1.

| Пакет | Размер | Содержимое |
|---|---:|---|
| Unity → sidecar request | 64 байта | sequence, flags, dt, walk/run speeds, position, rotation, velocity |
| Bind response | 2204 байта | header 48 + 77×7 float |
| Pose response | 2220 байт | header 48 + 77×7 float + 4 contacts |
| Error response | header + UTF-8 | диагностика исключения sidecar |

Bind-поза передаётся отдельным packet из официального skeleton; файл `soma_bind.f32` для
этого runtime не нужен. Флаги request: `reset=1`, `shutdown=2`.

Unity не ждёт inference: receive работает в background thread, визуальный слой берёт
последний целый кадр. При stale timeout происходит fallback. Метрики включают engine init,
wall inference p50/p95/max, официальные CUDA stages, IPC RTT/age, sent/received FPS,
generation count, RAM процесса, fallback count и причину остановки.

## 4. Проверенные результаты

### 4.1. Standalone TensorRT

Команда:

```powershell
C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe `
  C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\runtime_sidecar.py --self-test
```

Сценарий: idle → 5 м/с → 10 м/с → поворот на 10 м/с → stop, 480 кадров, 14 генераций.
Все позы и contacts finite; источники: `clip=60`, `generated=330`, `clip_transition=90`.

| Прогон | Init | Wall inference mean | p50 | p95 | max |
|---|---:|---:|---:|---:|---:|
| Первый холодный | 2916.840 ms | — | 24.856 ms | 147.745 ms | 327.978 ms |
| Последний тёплый | 3356.041 ms | 10.777 ms | 5.220 ms | 32.478 ms | 82.011 ms |

Последние CUDA stages: encode **0.283 ms**, inference **3.634 ms**, decode **0.106 ms**,
post-process **0.019 ms**. Это числа standalone sidecar, не Unity frame time и не
end-to-end игровой FPS.

### 4.2. Реальный UDP smoke

Отдельный subprocess + datagrams: **PASS**. Получен finite bind, затем 150 finite pose
packets, 6 генераций; shutdown завершился с кодом 0. `ENGINE_READY init_ms=2849.1 ms`.

RTT p50 **1.567 ms**, p95 **4.752 ms**, max **5897.773 ms**. Max включает ожидание
холодной инициализации/генерации и не является steady-state latency.

### 4.3. Unity и неизменность базовой анимации

- Python syntax: **PASS**.
- Unity `6000.5.2f1`: compilation **0 errors**, `isCompiling=false`.
- `check_baseline.ps1`: **BASELINE OK: 12 files match**.
- `git diff -- Packages`: пусто.
- `NetworkPlayer.cs`, combat/skills, player prefabs и `PlayerAnimation.controller` не менялись.

## 5. Ручная приёмка

1. Войти в Play Mode → Host/Client, дождаться появления персонажа.
2. `ProjectC/MotionBricks/Runtime/Attach + start on my player`.
3. Проверить WASD, Shift, резкие повороты, stop и движение по неровной поверхности.
4. `Ctrl+F10` — сравнить neural preview с обычным Animator; повторить несколько раз.
5. `ProjectC/MotionBricks/Runtime/Log diagnostics` — сохранить строки `[MB-Runtime]`
   и `[MB-Sidecar]`, включая p95, generations, source, fallbacks и RAM.
6. Отдельно проверить стопы/контакты, equipment, body swap, каст, F8/F9 и выход из Play Mode.
7. После body swap повторить Attach вручную. Для завершения — `Detach`.

Для оценки нагрузки дополнительно нужны Unity CPU/GPU frame time, FPS, VRAM и длительный
прогон; затем host-client 1/2/4 игрока. Сейчас эти данные не собраны.

## 6. Что ещё не доказано

- **Play Mode и визуальный retarget на живом персонаже не запускались агентом.**
- Нет оснований заранее заявлять отсутствие T-pose, skating, penetration, pop или проблем
  с пропорциями/ключицами.
- Runtime IK ног в Unity не добавлен; модель выдаёт contacts, но это не гарантирует контакт
  после ретаргета.
- Equipment, body swap, combat fallback, Floating Origin, выгрузка и выход из Play Mode
  требуют ручной проверки.
- GPU/VRAM/FPS в Unity, host-client 1/2/4 и длительная стабильность не измерены.
- Это evaluation sidecar с абсолютными dev-путями, а не нативный shipping backend.

Следующее решение принимается по визуальной приёмке и полным игровым замерам, а не по
standalone inference latency.
