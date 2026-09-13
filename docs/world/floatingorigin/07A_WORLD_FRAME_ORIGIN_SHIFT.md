# T-FO07A — Сдвиг origin фрейма мира вместе с контентом (первый шаг T-FO07)

Date: 2026-09-13. Этап плана: T-FO07 (серверные регионы и physics routing),
первый executable шаг к блокеру T-FO06DI.

## Инвариант (знак проверен по `Translated`)

Контент сдвинут на `+T` (`local_new = local_old + T`).
`ToGlobal = Origin + local` → для сохранения global:
`Origin_new = Origin_old.Translated(-T)` (вычитание в double).

## Что делается в этом тикете

1. `GlobalMotionWorld.RegisteredFrameIds()` — снимок id (для итерации без
   мутации во время перечисления) + `TryShiftFrameOrigin(id, newOrigin, error)`:
   fail-closed (running, фрейм есть, origin finite+valid; maxLocal и physics
   наследуются; `RunGeneration` сохраняется — это не рестарт).
   При bound actors — отказ `frame_has_bound_actors=N` (честный, в маркер).
2. `GlobalMotionPlayerBootstrap.TryUpdateFrameDefinition(id, coordinates, error)`:
   явное обновление definition + lease (иначе `EnsureFrames` уронит сессию
   `no implicit rebase`). Без этого вызова шаг 1 ломает живую игру.
3. Slice после `Completed` (только success-путь, best-effort): для каждого
   world frame — сдвиг origin на `-T`, затем sync definition в bootstrap.
   Маркер на фрейм: `WorldFrameShifted(id=..;ok=True/False:<reason>)`.
   Порядок: сначала world (отказ = пропуск definition), затем bootstrap.
   Rollback фреймы не трогает (отката origin нет — отдельный gate,
   т.к. отмена потребовала бы той же actor-координации).

## Ожидаемое поведение сегодня (честно)

Пилот bound к фрейму → `TryShiftFrameOrigin` откажет
`frame_has_bound_actors=1`, definition не тронется, игра не изменится.
Маркер в логе зафиксирует состояние. Полный сдвиг с живым актором
(drain → shift → rebind → re-issue control с discontinuity lineage) —
следующий слайс T-FO07B.

## Почему не трогаем immutable-ядро

`LocalCoordinateFrame`/`GlobalMotionFrame` остаются immutable;
замена объекта фрейма — через реестр с явными проверками, акторы при отказе
не инвалидируются. 600+ pure-проверок не затрагиваются (новых pure-валидаторов
не добавляем, меняем только runtime-классы; compile-контроль через refresh).

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): F8 → `WorldFrameShifted(id=1;ok=False:frame_has_bound_actors=1)`
   (id может отличаться — смотреть фактический), игра без изменений,
   ошибок 0.
