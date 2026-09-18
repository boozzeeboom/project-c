# T-SHIP-FIX07 — cargoDetail в отдельный NetworkVariable (по событиям, не 5 Гц)

> Тикет: `T-SHIP-FIX07` (P0, фаза B). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО)

- `NetworkVariable` шлёт **весь struct** при любом отличии (`Equals=false`).
  `cargoDetail[]` (до 32 записей со строками) лежал в 5-Гц снапшоте → летящий корабль
  (позиция/топливо меняются каждый тик) тащил весь детализированный груз 5 раз/сек.
  Стоящие корабли и так молчали (Equals=true) — шторм только в движении.
- Потребители `cargoDetail` (проверено grep, всего 2): `MyShipsTab:393` (+сравнение `:549-578`,
  читает `sc.TelemetryState` напрямую) и `ShipCargoConsoleWindow:474-487` (через агрегатор).
  `position` из телеметрии — только подпись `MyShipsTab:400` (F1). `cargoUsed/cargoMax` (дешёвые
  int) читают `ShipCargoVisual` + бары — остаются в быстром снапшоте.
- Оба UI обновляются по `OnShipStateChanged` (`MyShipsTab:163`, консоль `:331`) — то же событие
  поднимаем и на cargo-апдейтах, подписки не меняем.
- Попутно закрыты замечания ревью: `GetHashCode` теперь покрывает все поля (cargoDetail уехал
  в свой struct со своим Equals/Hash); `lastUpdateServerTime` — debug-поле, сознательно вне
  Equals и Hash (зафиксировано здесь).

## Решение (сплит, аддитивно; 6 файлов)

- `ShipTelemetryState.cs`: `cargoDetail` УБРАН из быстрого struct (+сериализация/Equals);
  новый `ShipCargoDetailState { shipNetworkObjectId, cargoDetail[] }` (сериализация + Equals +
  Hash по id+длине, массивы поэлементно не хешируем — задокументировано).
- `ShipController.cs`: `+ _telemetryCargoState` NV (Everyone/Server) + геттер + событие +
  `BuildCargoDetailDto()` (вынесено из `UpdateTelemetryState`) + `PublishCargoDetail()` (сервер);
  вызовы: конец `RegisterCargoWhenReady` (первичная публикация) + `RecalculateCargoPenalty`
  (все мутации груза идут через `OnCargoChanged`: load/unload/wipe).
- `ShipTelemetryClientState.cs`: `+ _cargoByShip` + `GetShipCargoDetail()` + подписка на
  `OnTelemetryCargoChanged` в `SubscribeToShip` (+seed initial) + чистка в `UnsubscribeFromShip`;
  на cargo-апдейте поднимается существующий `OnShipStateChanged(shipNetId)`.
- `MyShipsTab.cs`: детали из `sc.TelemetryCargoState.cargoDetail`; сравнение разделено
  (fast-approx + `CargoDetailEquals` по кешу `_lastCargoDetail`).
- `ShipCargoConsoleWindow.cs`: детали из `telemetry.GetShipCargoDetail()`, счётчики из быстрого.
- Позиция/топливо/HP — без изменений (5 Гц как были; квантование не дало бы выигрыша
  в движении и тронуло бы отображаемую точность — сознательно не делаем).

## Изменения

- См. diffstat в ITERATIONS. Быстрый снапшот похудел на весь `cargoDetail[]`;
  cargo-трафик теперь только по событиям смены груза.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): открыть трюм/«Мои корабли» → детали груза видны;
  load/unload → детали обновляются без реоткрытия; полёт с полным трюмом — UI жив;
  `ShipCargoVisual` (ящики) без изменений; регресс баров груз/топливо/HP.
