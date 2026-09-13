# T-FO06DA — Публикация сети после controlled rebase (NetworkTransform.Teleport)

Date: 2026-09-13

## Проблема

F8-транзакция (T-FO06CY) завершается `Completed`, мир сдвигается на `LocalTranslation`
(~56 км в capture f8_2), но дальше сеть работает против сдвинутого мира:

- Корабли, станции и сценовые объекты реплицируются stock NGO `NetworkTransform`
  (по классификации T-FO06D: Spatial-ready=0, мигрирован только пилот).
- После серверного сдвига корней их NT-буферы интерполяции на клиентах хранят
  досдвиговые позиции. Клиент интерполирует 56 км как обычное движение:
  объекты «улетают»/джиттерят после F8. В f8_2 при этом 0 errors — разнос
  визуально-сетевой, не эксепшн.
- Пилотный игрок (`GlobalMotionReplicator`) ведёт global-поток: глобальная позиция
  от сдвига не меняется, его трогать не нужно.

Это gap, прямо зафиксированный в `06CY_CONTROLLED_REBASE_VERTICAL_SLICE.md`:
«NGO baseline publication after rebase: NOT integrated».

## Решение

Новая фаза транзакции `PublishNetworkTeleport` между `Validated` и `Published`
(и симметрично в `Rollback` после восстановления снапшотов):

- Только на сервере (`NetworkManager.Singleton.IsServer`), иначе маркер
  `NetworkPublished(skipped:no_server_authority)`.
- Для каждого участника обход `GetComponentsInChildren<NetworkTransform>(false)`
  (только активные; неактивные корни и так исключены из scope по T-FO06CZ).
- Для каждого заспавненного NT с `CanCommitToTransform == true`:
  `nt.Teleport(t.position, t.rotation, t.localScale)` — значения не меняются,
  сбрасывается только флаг интерполяции (`shouldTeleport` реплицируется клиентам).
- Вложенные NT телепортировать тоже: их world-позиция изменилась вместе с родителем.
- Ошибки/отсутствие authority — в счётчики, не в fault транзакции: фаза
  best-effort, мир уже валиден.
- Маркер evidence: `runtimeRebase.NetworkPublished(teleported=N;skipped=M;errors=K)`.

## Границы (что НЕ делаем)

- Не меняем потоки `GlobalMotionReplicator` (пилот): global-координаты от сдвига
  инвариантны.
- Не трогаем Rigidbody velocities/sleep (план §2, п.4: сохранить).
- Не трогаем NavMesh rebuild (отдельный gate T-FO06BH и далее).
- Rollback F9 получает тот же телепорт назад — иначе «пролёт» при возврате.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors.
2. Play Mode (user): старт из BootstrapScene → F8 → в логе `NetworkPublished`
   с `teleported>0`; корабли/станции стоят на месте (нет 56 км пролёта).
3. F9 на свежем прогоне: `RollbackCompleted` + `NetworkPublished`, позиции вернулись.
