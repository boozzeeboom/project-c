# T-FO06DD — Сдвиг истории камеры вместе с миром

Date: 2026-09-13

## Проблема

После F8 `camera.LateUpdate ... collisionPos=[39991.06, 2502.79, 39999.55]`
навсегда остаётся в досдвиговых координатах (видно в f8_3/f8_4/f8_5).

Анализ `SpringArmCamera`:

- `_lastCollisionPos` потребляется только в anti-pop окне
  (`_wasColliding && now - _collisionExitTime < antiPopTime`, строки 614/634):
  камера, столкнувшаяся в момент F8, следующим кадром возвращается
  к досдвиговой точке коллизии — визуальный поп на десятки километров.
- Dormant API `TryApplyGlobalMotionCameraTranslation` (позиция + lag +
  collision при `_wasColliding`) существует со времён T-FO06AX, но slice
  его никогда не вызывал — камера догоняла мир только через snap
  (`snapDist`, строка 464).
- Stale `collisionPos` при `colliding=False` функционально безвреден
  (только лог), но маркер вводит в заблуждение при разборе.

## Решение

Slice вызывает dormant API в транзакции (best-effort, маркер
`runtimeRebase.CameraShifted(ok=True/False;error=...)`):

- success-путь после сдвига deathY: `ShiftCameraHistory(+translation)` →
  флаг `_cameraShiftApplied`;
- rollback: сдвиг назад только при флаге (тот же паттерн, что T-FO06DC).

Камера ищется через `FindAnyObjectByType<SpringArmCamera>()`
(активная ThirdPersonCamera). Неуспех (камера не найдена / не инициализирована /
нефинитный сдвиг) — в маркер, транзакцию не валит.

## Границы

- `lagSpeed` (скаляр) и `collisionExitTime` (время) сдвига не требуют.
- Полный camera ownership/history evidence и rollback через snapshot
  (`TryCapture/TryRestoreGlobalMotionCameraHistory`) — отдельный gate,
  здесь только сдвиг вместе с миром (план §2 п.6, первая часть).

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): F8 → в логе `CameraShifted(ok=True)`; `collisionPos`
   в следующих кадрах — в новых координатах (около игрока, не 39991/2502/39999).
3. F9: `RollbackCompleted` без `CameraShifted` (флаг не ставился).

## Верификация 2026-09-13 (ф8_7.txt) — PASS

Полная цепочка `Requested → ... → CameraShifted(ok=True) → Completed`,
`NetworkPublished(teleported=58)`, `RespawnShifted(dy=-2560.00)`.
`collisionPos` после `Completed` — `(59.875, -57.215, 63.664)`, т.е. рядом
с игроком в новых координатах (раньше навсегда `(39994.99, 2502.79, 40002.47)`).
Телепортов после — 0, ошибок — 0, «визуально всё ок» подтверждено.
T-FO06DE закрыт полностью.
