# T-FO09O — Полный ресерч и код-ревью FO-интеграции (закрытие хост-версии)

Date: 2026-09-14. Метод: Deep Subsystem Audit (skill code-review, Mode 2).
Прочитаны полностью: `GlobalMotionControlledRebaseSlice` (1243 строки),
`OriginRebasePlan` (+Recover), `GlobalSceneNativeExecutor` (RequireIdentity/
FaultSteadyState), `GlobalMotionPilotSpawnSource` (spawn-пути),
`GlobalMotionRebaseCoordinator.TryAdd` (дубли ID),
`AltitudeCorridorSystem` + `AltitudeCorridorData` (тип),
`WindManager` (ZoneRuntimeState), `NpcBrain.ApplyRebaseTranslation`,
`ShipPositionServer` (save/restore), `PlayerPositionServer.RestorePlayer`,
`RespawnManager/RespawnPointData`, `NetworkPlayer` (Update-гейт, restore),
`StormCellDirector`, `PickupDeckRide`, sibling `ShiftAltitudeCorridors`.
Остальные ~90 файлов каталога — dormant pure-контракты T-FO01–06
(в рантайм-пути хоста не участвуют, своими тикетами ревью пройдены).

## CRITICAL — найдено и исправлено

1. **Rollback −T без +T.** При apply/rebuild/validate-refused success-сдвиги
   не выполнялись, но Rollback безусловно применял −T к carry/NPC/storms/
   wind/corridors/fallbacks + broadcast −T клиентам + (с 09K) книгам.
   Отравление кэшей, ассетов и клиентов. Фикс: флаг `_localStateShifted`
   (ставится после блока сдвигов, сброс в начале запроса и после revert),
   маркер `LocalRevertSkipped(restored;shifted)`. Respawn/camera оставили
   свои тонкие флаги. Particles/decks/teleport — безвредны, без флага.
2. **Мутация SO-ассетов коридоров.** `AltitudeCorridorData :
   ScriptableObject`, сдвиг писал в `.asset` на диске (Editor Play =
   cumulative corruption; комментарий «runtime-only» был неверен).
   Спасала только ±T-симметрия в сессии. Фикс: `EnsureRuntimeCorridors`
   (одноразовые runtime-копии, `_globalCorridor` перепривязывается,
   `SetCorridors` сбрасывает флаг — внешних вызовов нет, setup-only).
3. **Дубли ID участников.** `NPC_RUNTIME/<name>` — спавнерные клоны делят
   имена → `duplicate_participant_id` валит ВСЮ транзакцию на подготовке.
   Ветка dormant (пока 0 срабатываний), но это латентный killer. Фикс:
   суффикс-индекс вызова (GetInstanceID deprecated CS0619).

## MEDIUM — исправлено

4. **Trails у клиента.** Сервер гасит Trail/Line (09B-батч), handler —
   только ParticleSystem. Зеркальный clear добавлен.

## Поправка после коммита (sibling 09N)

- Sibling тикетом `09N_RESCUE_TO_DEFAULT_SPAWN` вернул РУЧНОЕ Esc-спасение
  строго на дефолтный городской спавн (откат моего 09C-маршрута через
  ship-цепочку). Авто-респавн остался на общей цепочке
  (`TryResolveShipRescuePosition`, строка ~117 — проверено, на месте).
  Описание 09C выше читать с этой поправкой; решение sibling — не мое.

## Наблюдения — без изменений, зафиксировано

5. `FaultSteadyState` перманентен (drift → `_faulted`+закрытие пиров
   навсегда, без автовосстановления). Ни разу не срабатывал в серии;
   трогать fail-closed blind — риск. Оставить.
6. Вложенные NPC: порядок `FindObjectsByType` vs containment — теор. edge,
   префабы не вложены. Оставить.
7. Handler без флагов/ordering (deathY, камера, дубликаты сообщений) —
   deferred с мультиплеером (топологии нет).
8. `position += T` на Rigidbody-кораблях — доказано визуально, оставить.
9. Кулдаун ставится и на `RollbackFaulted` — safe-side, оставить.
10. Sibling `ShiftAltitudeCorridors`: синглтон, вне циклов — класс 09G-бага
    отсутствует. OK.

## Незакрытое (хост-скоп, не код)

- Far-repeat кулдауна 30с (отход >256м + F8 в окне) — тест пользователя.
- Посадка/выход в момент сдвига — тест пользователя.
- Шторм-тест (шторма нет), docking/recall/autopilot/торговля, chest,
  замеры spikes/трафика, видео-доказательства.
- Recover-ветка вне фрейма в дикой природе не тестирована (код покрыт тем
  же путём).
- Декларация jitter-fixed запрещена до приёмки (§4): `NOT READY` остаётся.

## Deferred (не хост-скоп)

- Мультиплеер v2 (топология, late join, owner-rules, handler ordering).
- Ship-adapter v2 (миграция репликации NT→Replicator).
- Initial-rebase при загрузке (origin между сессиями; 09J покрывает 1M).
- Spawn-from-save для рантайм-кораблей; global-double wire; физрегионы.

## Вердикт

Хост-одиночка FO: код закрыт полностью (все известные дефекты исправлены
в этом тикете), приёмка — за пользовательскими тестами выше. После них —
формальное снятие `NOT READY` для хост-скопа отдельным решением.

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS; скобки slice 182/182, corridors 30/30, wind 37/37.
2. Play Mode retest — NOT RUN (user): штатный F8/F9-цикл + маркеры
   (`LocalRevertSkipped` в норме отсутствует; ассеты коридоров в
   `git status` чистые после сессии).

## Приложение: свёрнутые безкодовые аудиты (2026-09-14)

- **07D (Rigidbody/пулы):** чинить нечего. Slice двигает Transform +
  `Physics.SyncTransforms()`, velocities/sleep/constraints не трогает
  (план §2.4 по построению); интерполяция Rigidbody даёт ≤1–2 fixed-кадра
  доглэйда, сетевая сторона закрыта телепортом NT (06DA). Пулы
  (`VfxObjectPool`, `DamageNumberService`, `ShipCargoVisual`) позицию задают
  при выдаче — протухших мировых кэшей нет. Вне gate: physics-handoff с
  пассажирами в момент сдвига (трек 07/08), снаряды в полёте (transient).
- **07G (посадка/parenting):** действий нет. Посаженный игрок — child
  корня-корабля (`SetParent`, ~1445), едет с участником, отдельно не
  регистрируется (`IsContainedByRegisteredRoot`); rebind читает мировую
  позицию; выход — live `GetExitPosition()`; ссылки — object refs.
  Выход в окно замороженной транзакции (~мс) гарда не требует.
