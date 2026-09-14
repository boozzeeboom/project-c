# T-FO07J — Точная ветвь отказа rebind в полёте (анализ, без кода)

Date: 2026-09-13. По маркеру `status=WaitingForControl` из ф8_16 (добавлен в 07I).

## Цепочка отказа (проверена по коду построчно)

`StartWorldStream(Owner)` → `ActivateWorldServer` → `ActivateServer` →
`baselinePreflight(candidate)` = `PoseAdapter.CanPrepareControl`:

1. `TryPlanHierarchy(World-baseline, allowChange=true, reportFailure=false, role)`:
   World-ветка видит сетевого предка (`CurrentNetworkAncestor()` = ShipRoot NO)
   → `desiredParent=null`, `changeParent=true`.
2. Дальше проверка прежнего родителя:
   `World.TryGetActor(shipNetworkObjectId, ...)` — корабль НЕ registered actor
   (адаптеры есть только у игроков) → `PlanningFailure(DriverBlocked)`
   с `reportFailure=false` — статус НЕ меняется (остаётся `WaitingForControl`
   после `Bind`), возврат `false`.
3. `ActivateServer` → `false` → `StartWorldStream` → `PrepareBaseline`
   (та же стена) → `stream_refused`.

Итого: `status=WaitingForControl` в маркере — это leftover после `Bind`,
а не fresh-диагноз; сама ветвь — **незарегистрированный ship-родитель
в World-потоке**. Silent-false by design (preflight не должен мусорить
в статус до настоящей попытки записи).

## Почему не чиним здесь

Легальные пути оба вне scope best-effort slice:

- **ParentLocal-поток через корабль** (`StartParentStream`): требует
  registered parent-actor с полным lifecycle (ship adapter — T-FO08).
- **Отпаренить → World-rebind → припаренить назад**: мутация иерархии
  пилотирования вне baseline-транзакции (`SetParent` + `worldPositionStays`
  + controller состояние + `RemovePilot` инварианты) — отдельный
  reviewed gate, не однострочник.

Текущее поведение безопасно и стабильно: поток down, полёт идёт,
повторный F8 в полёте повторяет тот же цикл без деградации
(drain на отвязанном — no-op по `World==null`, shift проходит, rebind
снова честно отказывает). Потребителя потока по-прежнему нет.

## Что дальше (gates)

- T-FO08: ship adapter (parent-actor) → `StartParentStream` для посаженного пилота.
- Альтернатива: reviewed unparent-rebind-reparent процедура.
- Маркер `status=` оставить: при будущих отказах отличит
  silent-preflight (`WaitingForControl`) от `DriverBlocked`/`Faulted`.

## Проверка

Без кода: построчный аудит выше + `status=WaitingForControl` из ф8_16
(стабильно воспроизводится: ф8_15 и ф8_16). Компиляция не затрагивалась.
