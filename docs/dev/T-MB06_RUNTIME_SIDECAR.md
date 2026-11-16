# T-MB06 — runtime MotionBricks на живом персонаже (design)

> **Дата:** 2026-11-16
> **Статус:** реализация dev-only эксперимента.
> **Цель:** проверить непрерывную генерацию NVIDIA на настоящем owner-персонаже, а не новые запечённые переходы. Движение, бой и сеть остаются прежними.

## 1. Контур эксперимента

```text
NetworkPlayer + CharacterController (без изменений)
        ↓ position / rotation / velocity / dt, UDP localhost
Python: официальный SOMACharacter + StateMachine + Smart Primitives
        ↓ GeneratedMotionPlayer + TensorRT + MotionRecorder
77 локальных SOMA transforms + foot contacts + metrics
        ↓ UDP localhost, latest frame, без ожидания inference в Unity
SOMA → Project C rig, LateUpdate, только визуальный слой
```

Используется официальный `SOMACharacter`, включая idle/locomotion states, blend spaces,
раннюю регенерацию и clip transitions. Свой параллельный планировщик не создаётся.
Sample motions и Smart Primitives читаются из официального репозитория только как
evaluation/dev preview; в `Assets` они не копируются.

## 2. Движение и координаты

`CharacterController` — единственный источник перемещения. Runtime не вызывает `Move`,
не меняет `NetworkPlayer`, не реплицирует кости. Unity отправляет фактическую
горизонтальную скорость контроллера; sidecar строит из неё `LocomotionCommand`.
Скорости walk/run режима — 5/10 м/с, как у игрока.

App world NVIDIA: right-handed Z-up, forward +X. Unity → app position:
`(z, -x, y)`. Поворот конвертируется сменой basis и отражением handedness.

Важный контракт официального `SOMACharacter.current_pose`: root **XY** — app-ground
персонажа, root **Z** — высота уже относительно ground. Поэтому перед отправкой в Unity
вычитается только app-ground XY; Z не трогается. C# получает model-space root с локальной
высотой и использует существующее преобразование baker'а: position `(-x,y,z)`,
quaternion `(x,-y,-z,w)`. C# дополнительно гардит аномальный вертикальный offset (>5 м или non-finite) и уходит
в fallback, не двигая модель. Для facing численно подтверждён постоянный basis offset:
при движении вперёд root-forward пакета `−X`, а rest-forward `testing` — `+Z`; поэтому
мировые SOMA-delta поворачиваются на +90° до применения к Project C rig. Для плеч/предплечий
twist отдельно ограничен ±90° — runtime-аналог фикса «руки наизнанку» из T-MB05.

Внешний root корректируется **до** `player.record_pose()`: recorder получает именно
показанную позу. Горизонталь и yaw берутся от игрока, вертикальный bob генерации
сохраняется относительно высоты земли. При rebase/телепорте клиент отправляет reset.

## 3. Файлы и изоляция

- `tools/runtime_sidecar.py` — официальный runtime, внешний root, UDP server, self-test.
- `MotionBricksRuntimeClient.cs` — процесс, background receive, latest-frame buffer,
  timeout/failure budget, диагностика.
- `MotionBricksSomaRetarget.cs` — bind-поза из UDP packet + rest из FBX, полный world-rotation
  retarget по проверенной схеме T-MB03/05; `soma_bind.f32` не требуется, `B-jaw` опционален.
- `MotionBricksRuntimePlayback.cs` — gate `NeuralExperimental`, owner-only, переключение
  `Ctrl+F10`, combat/body-swap/airborne guard, восстановление Animator.
- `MotionBricksRuntimeReviewMenu.cs` — явный Attach/Start только в Play Mode; prefab,
  controller и сцены не меняются.

Компонент не добавляется в prefab. Python/TensorRT и модели не включаются в build.
Обычный Animator не удаляется и его controller не заменяется. Остановка, timeout,
combat и body swap возвращают authored Animator через `Rebind()+Update(0)`; это защита
эксперимента, а не отдельный production rollout.

## 4. Протокол и нагрузка

UDP IPv4 loopback, little-endian, versioned binary packets. Request — 64 байта: sequence,
flags, dt, position, rotation и velocity. Bind response — 2204 байта, pose response — 2220
байт: header 48 + 77×7 float + 4 contacts у pose. Bind передаётся отдельным packet из
официального skeleton. Ошибка передаётся отдельным packet и пишется в stderr.

Получение работает в background thread. Unity никогда не ждёт TensorRT: применяется
последний целый кадр; при stale timeout генерация отключается. Логируются engine init,
wall inference p50/p95/max, официальные stage timings, IPC RTT/age, sent/received FPS,
fallback count и причина остановки. FP32 — первый тест, FP16 — отдельная опция.

## 5. Проверка и границы результата

1. Синтаксис Python без `py_compile`-артефактов.
2. Standalone TensorRT self-test: idle → 5 м/с → 10 м/с → turn → stop; finite poses,
   `generation_count > 0`, реальные timings.
3. Реальный UDP smoke test, включая shutdown и error packet.
4. Unity compilation: 0 errors. Play Mode, тесты MCP, build и скриншоты агент не запускает.
5. Ручная приёмка: Host/Client → `ProjectC/MotionBricks/Runtime/Attach + start on my player`
   → WASD/Shift/повороты → `Ctrl+F10` для A/B → `Log diagnostics`.

Численный self-test не доказывает визуальное качество, контакт стоп, equipment/body-swap
или FPS в Unity. Эти пункты остаются ручной приёмкой; shipping-решение по результатам
замеров отдельно.
