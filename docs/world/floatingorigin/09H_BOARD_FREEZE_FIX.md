# T-FO09H-fix — Мёртвое управление после посадки: recover-план вне фрейма

Date: 2026-09-13. Жалоба (ф8_29): после `boarded restored ship` персонаж
неуправляем (бег в лупе/идл), F не сажает. До 09H такого не было.

## Диагноз по ф8_29 (точечно)

- `boarded restored ship` один раз (09H сработал, игрок на exit).
  Дальше: `player.Update.begin` идёт (1924), но `beforeProcessMovement`
  — 1 раз (до посадки 186/186 1:1). Управление мертво при
  `inputEnabled=True`, `controllerEnabled=True`, `grounded=True`.
- Гейт: `Update` строка ~853: `if (!CanSimulateInCurrentCoordinates
  && !_inShip) return;` — посадка на exit оставляет `_inShip=false`,
  а прямой телепорт вынес игрока за фрейм адаптера
  (local 119871 при maxLocal 100000) → `CanSimulate=false` → ранний
  return до движения и до F-посадки. Аниматор (отдельный луп) продолжает
  «бежать» — визуально «бег в лупе».
- Почему навсегда: `TryCreate` возвращает false вне `ContainsLocal` —
  и автотриггер, и транзакция молча отказываются. В логе после посадки
  ноль rebase-маркеров. `AdapterReady(status=Ready)` вводит в заблуждение
  (это baseline-готовность, не `IsReadyForSimulation` с ролью Authority).

## Решение

1. `OriginRebasePlan.TryCreateRecover` (новый, аддитивный): та же
   квантованная математика без гейтов ContainsLocal/порога; финальная
   проверка представимости сохранена. Старый `TryCreate` не тронут.
2. Slice `ExecuteTransaction`: fallback на recover ТОЛЬКО вне
   `ContainsLocal` (внутри в пределах порога — честный `no_rebase_plan`
   как раньше); маркер `Requested` несёт `;recovered=true`.
3. `TryAutoRebase`: предпроверка `TryCreate OR TryCreateRecover`.
4. `WaitForShipAndBoard`: после телепорта сразу
   `RequestControlledRebase(false, "reconnect_board")` (best-effort) —
   вне фрейма сработает recover, внутри — честный отказ; авто подхватит
   через 5с в любом случае.

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS; скобки 364/364, 9/9, 167/167.
2. Play Mode (user): выход на корабле → перезаход → `boarded` →
   `Requested(reason=reconnect_board;recovered=true)` → `Completed` →
   управление есть, F сажает. Лог — мне.
