# T-FO06DG — Сдвиг кэша платформы игрока вместе с миром

Date: 2026-09-13

## Аудит (read-only, без нового лога)

`NetworkPlayer.ApplyPlatformCarry` (строки 1217–1297):

- `deltaPos = platform.position - _platformLastPos` — верхнего клампа нет
  (есть только фильтр шума снизу `_platformMinDelta`);
- дельта уходит в единый `CharacterController.Move` (строка 1159).

Сценарий: игрок стоит на палубе корабля в момент F8. Мир и платформа
сдвигаются на translation, а `_platformLastPos` остаётся досдвиговым.
Первый кадр после транзакции: `deltaPos ≈ translation` (десятки км) →
игрока швыряет одним Move (сквозь геометрию / за борт), затем кэш
обновляется и всё «нормализуется». Один кадр достаточно для поломки.

Во всех пользовательских логах `onPlatform=False`, поэтому баг пока
не наблюдался — чиним заранее (тот же класс, что DF: молчаливая дыра).

Замороженный контроллер во время транзакции не спасает: `ApplyPlatformCarry`
возвращается при выключенном контроллере, а после `ReleaseController`
первый же кадр считает по stale-кэшу.

## Решение

- `NetworkPlayer.ApplyRebaseTranslation`: `_platformLastPos += translation`
  + `_platformDelta = Vector3.zero`. Ротация translation-only планом
  не меняется, `_platformLastRot` не трогаем.
- `ShiftPlayerRespawnReference` расширен и переименован
  в `ShiftPlayerFrameReferences`: за один вызов сдвигает и трекер deathY,
  и кэш платформы (оба живут на префабе игрока). Маркер прежний
  (`RespawnShifted`), путь/флаг `_respawnShiftApplied` без изменений.

## Границы

- `_currentPlatform` (ссылка на объект) переживает сдвиг сама.
- Yaw-перенос (`_carryYaw`, орбитальное смещение) translation не затрагивает.
- Проверка возможна только на палубе в момент F8 (user-controlled).

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): встать на палубу корабля (`onPlatform=True`) → F8 →
   игрок остаётся на палубе, флинга нет, `PlatformCarry` дельты в логе
   обычные (метры, не километры).

## Верификация 2026-09-13 (ф8_9.txt) — PASS

F8 на палубе: было `[40210.81, 2504.69, 40075.14]` → стало
`[18.81, -55.32, -116.86]` стабильно (повторы, без дрейфа).
`onPlatform=True` во всех 583 записях после `Completed` — остался на палубе.
`PlatformCarry` дельты: `0.00/-0.02/-0.06` — сантиметры, флинга на translation нет.
Телепортов после — 0, ошибок — 0. T-FO06DG закрыт полностью.
