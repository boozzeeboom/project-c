# T-FO07H — F8 при пилотировании: честный отказ рестарта потока (диагноз + маркер статуса)

Date: 2026-09-13.

## Факты из ф8_15.txt (F8 в полёте, `inShip=True` все 717 записей)

- Цепочка транзакции полная до `Completed`; `ActorDrained(frame=1)` →
  `WorldFrameShifted(id=1,ok=True)` → `ActorRebound(ok=False:stream_refused)`.
- После: `adapter=WaitingForControl, baselinePlaced=False` (все 119 записей),
  игрок летит (`playerPos` меняется), телепортов 0, ошибок 0, визуально ок.
- Пилотирование, мир, камера не пострадали: поток пилота остановлен drain,
  рестарт отклонён preflight — игра продолжается локально (движение кораблём
  не зависит от motion-потока).

## Почему preflight отказывает посаженному пилоту (механизм, кандидаты)

Пилот parented к `ShipRoot` (worldPositionStays). `StartWorldStream` идёт через
`ActivateWorldServer` + `CanPrepareControl`: World-ветка `TryPlanHierarchy`
запрещает скрытого сетевого предка (`desiredParent=null`, `changeParent=true`),
а план с отсоединением от корабля в момент пилотирования не проходит
валидацию записи baseline. Точная ветвь без инструментирования не называется —
поэтому в маркер добавлен `status=` адаптера (следующий прогон F8 в полёте
сразу покажет gate: `DriverBlocked`/`WaitingForParent`/др.).

## Решение в тикете

Только диагностика: `ActorRebound(...;status=<Status>)` при `stream_refused`.
Поведение не менялось (отказ и так был честным и безопасным). Полноценный
rebind посаженного пилота (parent-local поток через ShipRoot либо отложенный
рестарт после выхода с корабля) — отдельный gate, когда будет потребитель
потока (сейчас его нет даже в host-only).

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): F8 в полёте → `ActorRebound(ok=False:stream_refused;status=...)`,
   полёт продолжается, ошибок 0.
