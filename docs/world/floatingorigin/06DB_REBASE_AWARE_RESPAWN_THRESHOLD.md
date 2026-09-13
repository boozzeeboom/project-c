# T-FO06DB — Rebase-aware порог падения (respawn deathY едет вместе с миром)

Date: 2026-09-13

## Диагноз из f8_3.txt

F8-транзакция полностью успешна: `Requested → ... → NetworkPublished(teleported=58,skipped=0,errors=0) →
Published → Completed`, сдвиг `(-39936, -2560, -39936)`. T-FO06DA работает.

Но дальше игрок «подпрыгивает на месте» — лог доказывает цикл с периодом ~0.5с:

- после сдвига игрок на `[56.01, -57.84, 64.00]`, `grounded=True`;
- `respawn.Update: y=-57.835 deathY=0.000` → `FallThresholdReached(elapsed≈0.55, delay=0.500)` →
  `PerformRespawn` → `TeleportRpc` к точке у кораблей;
- повтор бесконечно: `y=-56.916 <= deathY=0` всегда истинно.

Корень: `PlayerRespawnTracker._deathY = 0` — абсолютный legacy-порог. До F8 земля
на `y≈2502`, после F8 легитимная земля (палубы кораблей, `ShipDeckNav origin y≈-58`)
лежит ниже порога. Каждый тик ниже порога + 0.5с задержка = вечный ретелепорт
с телепортацией на ~1м вверх (`-56.92 → -55.95`), визуально «подпрыгивание».

Сопутствующие находки (вне scope тикета):

- `camera.LateUpdate ... collisionPos=[39991.06, 2502.79, 39999.55]` — история
  коллизий камеры осталась в досдвиговых координатах навсегда. Следующий gate
  (camera lag/collision history, план §2 п.6).
- Повторный F8 после сдвига корректно `Rejected(no_rebase_plan)` — игрок уже
  рядом с origin, порог 256 не превышен. Это штатное поведение, не баг.
- F9 (`f9_3.txt`): `RollbackCompleted`, `NetworkPublished(teleported=58)` —
  «не вижу изменений» и есть ожидаемый результат rollback: всё возвращено назад.

## Решение

`PlayerRespawnTracker.ApplyRebaseTranslation(Vector3)`:
`_deathY += translation.y` + сброс таймера падения.

Slice вызывает один раз за транзакцию на сервере:
успех — `+plan.LocalTranslation` (после `PublishNetworkTeleport`);
rollback — `-request.Plan.LocalTranslation` (корень игрока хранится
в `_rollbackPlayerRoot`, очищается в `finally`).
Маркер evidence: `runtimeRebase.RespawnShifted(dy=...)`.

## Границы

- Точки респавна (`RespawnManager`, триггеры, корабли) — мировые объекты,
  сдвигаются вместе с миром сами; правим только абсолютный порог трекера.
- `deathY` остаётся legacy-абсолютом вне транзакций; полный перевод высоты
  на frame-relative — отдельный gate (план §2 п.8: глобальная высота).
- История коллизий камеры — отдельный gate.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): F8 → в логе `RespawnShifted(dy=-2560.00)`; игрок стоит
   на палубе/земле, цикла `FallThresholdReached` каждые 0.5с нет.
3. F9: `RollbackCompleted` + `RespawnShifted(dy=+2560.00)`, deathY вернулся к 0.
