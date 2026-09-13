# T-FO06DC — Флаг прямого сдвига deathY (двойной сдвиг в F9-потоке)

Date: 2026-09-13

## Диагноз из f9_4.txt

После F9-rollback игрок «неуправляем, прыгает на точку». Лог доказывает:

- `RollbackCompleted`, `NetworkPublished(teleported=58)`, `RespawnShifted(dy=+2560.00)`;
- дальше: `respawn.Update(y=2502.165, deathY=2560.000)` → условие `y <= deathY`
  всегда истинно → `TeleportRpc` к `[40118.83, 2504.05, 40001.66]` каждые ~0.5с бесконечно.

Корень — асимметрия путей T-FO06DB:

- прямой сдвиг `_deathY += translation` выполняется только на success-пути
  (после `Validated`);
- в F9-потоке apply двигает мир, затем forced failure → `Rollback` безусловно
  сдвигал назад `-request.Plan.LocalTranslation` (= +2560), хотя вперёд
  ничего не двигали. Итог: `deathY = 0 + 2560` при игроке на y=2502.

## Решение

Флаг `_respawnShiftApplied` (per-transaction, сброс в начале `RequestControlledRebase`):

- success-путь после `ShiftPlayerRespawnReference` ставит `true`;
- `Rollback` сдвигает назад только при `true`, затем сбрасывает в `false`.

Инвариант: число прямых и обратных сдвигов равно. F8→F8(reject)→F9: флаг
сброшен в начале F9-транзакции, обратного сдвига нет — deathY остаётся
корректным (-2560 при сдвинутом мире).

## Сопутствующие вердикты по трём логам

- `f8 to f9.txt`: F8 полный успех (`Completed` + `RespawnShifted(dy=-2560)`),
  затем F9 → `Rejected(no_rebase_plan)` — уже у origin. «F9 ничего не произошло»
  = штатное поведение: F9 не «отмена F8», а тест rollback-пути; откатывать нечего.
- `f8_4.txt`: полный успех F8. «Джиттер пропал, хождение нормальное, корабли
  с NPC на палубах» = T-FO06DA (телепорт NT) + T-FO06DB (deathY) работают.
- `f9_4.txt`: баг настоящего тикета, исправлен флагом.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): свежий прогон → F9 → в логе НЕТ `RespawnShifted`;
   `respawn.Update` с `deathY=0.000`, цикла телепортов нет, игрок управляем.
3. F8 → `RespawnShifted(dy=-2560.00)`; затем F9 (reject, без маркеров) —
   deathY остаётся -2560, игрок стоит.
