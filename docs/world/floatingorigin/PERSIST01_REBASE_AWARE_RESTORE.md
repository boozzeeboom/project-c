# T-FO-PERSIST: персистенция персонажа после сдвига (диагноз и план)

> Тикет-группа: **T-FO-PERSIST01/02/03**. Дата: 2026-09-13.
> Симптом: каждый заход — со спавна. Билд до ввода FO — персистенция работала (T-PERSIST01/02, 2026-08-16).

## 1. Диагноз (факты, не гипотезы)

1. **Сейв пишется, restore-код активен.** `ф8_21.txt:8728` — `Saved 22 ships + 1 players`
   каждые 5с. `ShipPositionServer.RestoreCoroutine` завершается, `PlayerPositionServer`
   загружает, `NetworkPlayer.RestorePlayerPositionCoroutine` ждёт оба флага.
2. **В логах F8-тестов restore нечего применять:** `ф8_21:4673`, `ф8_18`, `ф8_19` —
   `No save file ... Returning empty wrapper` → `Loaded: 0 ships, 0 players`.
   (Тесты идут с вайпом сейвов ради детерминизма — restore-путь давно не упражнялся
   с непустым файлом.)
3. **Корневая причина (код, проверен построчно):**
   F8 сдвигает мир И игрока на `plan.LocalTranslation` (участники slice).
   `ShipPositionServer.SaveCurrentState` пишет уже сдвинутые локальные координаты.
   Свежий старт грузит мир в исходном origin — записи о сдвиге нигде нет.
   Restore кладёт игрока/корабли со смещением на полный вектор сдвига (десятки км)
   в пустоту → `PlayerRespawnTracker.Update` (`y <= _deathY`, 0.5с) → респавн на спавн.
   Пример из живого файла: игрок сохранён на `(183, −15.6, 150)` — ниже свежего
   `_deathY = 0` даже безотносительно пустоты.
4. **FO-валидация legacy не при чём:** `CountLegacyPositionServices` /
   `ValidateStart` относятся к pilot-сессии (`GlobalMotionSessionCoordinator`),
   живая игра идёт legacy-хостом (в `ф8_21` только пассивный `PeerConnected queued`),
   `ShipPositionServer`/`PlayerPositionServer` на месте и работают. Пилот-префаб
   (`UsesGlobalCoordinates`) в живой игре не заспавнен.
5. **Вторичка:** свежий `_deathY = 0` убивает даже валидные низкие сейвы
   (пост-F8 легитимная земля — отрицательные y). `ResetFallTimer` в restore-пути
   даёт только 0.5с отсрочки.

## 2. Решение (Option B: корректировать сейв при загрузке, мир не трогать)

Альтернатива (при старте досдвигать мир под сейв) отвергнута: хирургия сцен
на старте, гонки с readiness/executor. Вместо этого сейв хранит суммарный сдвиг,
загрузка вычитает его из координат — мир остаётся в исходном origin, всё консистентно.

## 3. Тикеты

- **T-FO-PERSIST01** — суммарный сдвиг в файле + коррекция при загрузке:
  `ShipPositionListWrapper.rbx/rby/rbz` (суммарный `LocalTranslation` на момент сейва)
  + `rbFrame`; slice ведёт статический кумулятив (success `+= T`, rollback при
  `restored` `-= T`); `SaveCurrentState` пишет; `RestoreCoroutine` вычитает из
  позиций кораблей (включая `pxCruise/liftStartY`) и игроков до применения.
  Маркер `RebaseCorrected`. Старые файлы (нули) — без коррекции, обратно совместимо.
- **T-FO-PERSIST02** — `PlayerRespawnTracker.EnsureDeathBelow(y, margin)`:
  вызов из `RestorePlayerPositionCoroutine` после успешного restore — порог
  опускается под восстановленную точку, мгновенного респавна нет.
- **T-FO-PERSIST03 (verify, за пользователем):** F8 → ходьба → выход в меню →
  новый заход → игрок на довидовой точке; корабли на местах; в логе
  `RebaseCorrected` + `restored to position`, без `FallThresholdReached`.

## 5. Поправка по итогам т-фо-персис_1.txt (T-FO-PERSIST03, 2026-09-13)

Пункт 4 раздела 1 был неточен для живой игры. Факты из лога:
- `Restored 22/22 ships` + `RebaseCorrected offset=(-39936,-2560,-40192) frame=2` —
  T-FO-PERSIST01 для кораблей работает.
- Но игрок спавнится как **`NetworkPlayer_GlobalPilot[Clone]`** через pilot-путь
  (`SpawnAsPlayerObject`, `InitialGateReleased`, позиция `Respawn_Default + 1м`):
  `GlobalMotionPilotSpawnSource` всегда отдаёт авторскую точку спавна, сейв не читает
  (прямо заявлено в docstring: «intentionally not a production persistence source»).
- `RestorePlayerPositionCoroutine` для него молча выходит (`UsesGlobalCoordinates`
  → `yield break`), legacy restore by design не выполняется.
- Дальше игрок падает (`grounded=False`, y: 1 → −6.3) → `FallThresholdReached` →
  `TeleportRpc(target=[39992, 2502.77, 40000])` = спавн. Полная цепочка
  «каждый заход со спавна» доказана строками 2888–3618 лога.
- FO checkpoint-сессия при этом не работает (legacy-сервисы на месте → fail-closed),
  чекпоинты никогда не писались — ни одна из двух персистенций игрока не вела.

Фикс T-FO-PERSIST03 (мост, не смена архитектуры):
- `ShipPositionServer.TryLoadCorrectedPlayer` (static) + `ShiftWrapperByOffset`
  (единая точка коррекции; `ApplyRebaseCorrection` делегирует ей).
- `GlobalMotionPilotSpawnSource.TryGetLegacySpawn`: однократный резолв на клиента
  (кэш `_legacySpawns`/`_legacyMissed`), проекция проверяется до выдачи плана,
  любой провал = тихий fallback на `Respawn_Default`. Маркер `PilotSpawnRestored`.
- `NetworkPlayer`: в global-ветке coroutine перед `yield break` —
  `EnsureDeathBelow(transform.position.y)` (placement уже применён).
- Граница: inShip-записи используются как есть (корабль ресторится в matching-точку
  тем же сейвом); протухших сейвов не истекаем (как legacy).

## 6. Границы

- Не трогаем: BootstrapScene, pilot-сессию, checkpoint-систему FO, схему DTO
  (только аддитивные поля), `inShip`-ветку restore (выход из корабля live).
- Мультиплеер: второй клиент — DEFERRED (решение T-FO08E в силе).
