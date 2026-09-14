# T-FO07G — Аудит посадки/parenting при сдвиге (без кода)

Date: 2026-09-13.

## Проверенные пути рантайм-парентинга (не FO-контракты)

- `NetworkPlayer`: посадка `transform.SetParent(_currentShip.ShipRoot, true)`
  (строка ~1445), выход `SetParent(null)` + телепорт на `GetExitPosition()`
  (live от корабля). Cargo boxes, thruster/contrail visuals, equipment —
  children соответствующих корней.
- `NpcBrain.BeginRide`: `TrySetParent(shipNo)` + parented-ветка только
  перечитывает кэш (`_rideLastPos = platform.position`, без carry-add).

## Вердикт: действий не требуется

Посаженный игрок — child корня-корабля: участник slice двигает корень,
игрок едет вместе (отдельным участником не регистрируется —
`IsContainedByRegisteredRoot`). Сдвиг deathY/платформы slice ему всё равно
положен (мир уехал) и безвреден (`inShip`, carry сброшен при посадке).
Rebind читает мировую позицию — корректна после сдвига. Выход —
`GetExitPosition()` live. Ссылки (`_currentShip`/`_lastShip`) — object refs,
сдвиг их не затрагивает. Выход во время замороженной транзакции —
пользовательский ввод в окно ~мс, отдельного гарда не требует.

## Проверка

Без кода: статический аудит выше. Компиляция не затрагивалась.
