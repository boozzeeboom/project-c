# T-FO06DI — Блокер: фрейм GlobalMotionWorld не едет вместе с миром (global-идентичность пилота прыгает на translation)

Date: 2026-09-13. Статус: диагноз + зафиксированный gate, ИЗМЕНЕНИЙ КОДА НЕТ
(осознанно, см. «Почему не фиксим точечно»).

## Механика дефекта (проверена по коду, не предположение)

1. Пилот ведёт живой global-поток: `ф8_10.txt` показывает рост
   `ngo.ControlAccepted(revision=1→2→3, sequence=0→13→37)` — Authority публикует
   через `PoseAdapter.LateUpdate → TryCaptureWorld → PublishWorld`.
2. `TryCaptureWorld`: `position = Frame.Coordinates.ToGlobal(local)`,
   где `ToGlobal = Origin.Translated(local)`.
3. F8 меняет `local` на `+T`, а `Frame.Origin` не меняется никогда:
   - `LocalCoordinateFrame` — immutable struct (`WithOrigin` возвращает новый);
   - `GlobalMotionFrame.Coordinates` — get-only;
   - нового API сдвига нет; `UnregisterFrame` отказывает, пока bound actors;
   - `PoseAdapter` кэширует `Frame` по ссылке (`ReferenceEquals` +
     `World.IsCurrent` в `ContextValid`) — подмена объекта фрейма уронит
     все bound actors в `InvalidFrame/WaitingForControl`.
4. Итог: публикуемая global-позиция пилота прыгает ровно на `T`
   (в наших прогонах `(-39936, -2560, -39936)`), хотя по дизайну плана §2.1
   global-точка от сдвига инвариантна («сдвиг не является телепортом»).
5. Потребитель: по плану §3.1 (T-FO05C) checkpoint-источник читает
   latest `_serverControl` — чекпоинт после F8 сохранит прыгнувшую global.

## Почему это пока латентно, а не пожар

В живой игре нет подключённого потребителя global-потока пилота:
вся интеграция T-FO06 (checkpoint repository/backup/restore/provider) —
« Repository к игре не подключён» (план §3.1, T-FO06B). Stock-сохранения
пишут локальный transform. Remote-реплик (второй клиент) в тестах не было.
Поэтому: single-host геймплей не страдает, будущий мультиплеер/персистентность —
да.

## Почему не фиксим точечно в этом тикете

Честные варианты требуют изменения immutable-ядра (`LocalCoordinateFrame`,
`GlobalMotionFrame`, `PoseAdapter.Frame` + `IsCurrent`/`ContextValid`) —
это фундамент, на котором стоят 600+ pure-проверок T-FO02–T-FO06 и весь
readiness-bundle. Локальный хак (напр. перезахват baseline как «новой правды»
через `ReactivateFromCurrentPose`) молча легализует прыжок — хуже болезни.
Нужен скоординированный gate уровня T-FO07: замена фрейма + перепривязка
акторов + переиздание control с тем же binding/discontinuity lineage.

## Gate acceptance (когда брать)

1. Спроектировать `TryShiftFrameOrigin` на `GlobalMotionWorld` с перепривязкой
   акторов (draft, без ломки pure-контрактов).
2. F8 → опубликованная global пилота до/после совпадает (сравнение
   `TrySampleForDisplay` или server control до/после, дельта ≈ 0).
3. Чекпоинт до/после F8 указывает на одну global-точку.
4. Только потом — мультиплеер-приёмка второго клиента.

## Проверка этого тикета

Без кода: статический аудит выше + живые revision/sequence из `ф8_10.txt`.
Компиляция не затрагивалась.
