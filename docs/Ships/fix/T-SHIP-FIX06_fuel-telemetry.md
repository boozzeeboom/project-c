# T-SHIP-FIX06 — топливо в HUD из телеметрии (не из локальной копии)

> Тикет: `T-SHIP-FIX06` (P0, фаза B). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО, с уточнением скоупа)

- `ShipFuelSystem` — обычный `MonoBehaviour`, `currentFuel` — plain float (`:33`).
  Мутирует только сервер; у клиента копия стоит на `Initialize`-значении.
- Сервер УЖЕ пишет топливо в телеметрию (`ShipController.UpdateTelemetryState:1054-1061,1136-1137`:
  `fuelNormalized/fuelMax`, 5 Гц) — отдельный `NetworkVariable` НЕ нужен.
- Баг — в потребителе: `ShipHudController` (`UpdateSpeedColumn:481-508`) читает
  **локальный** `ship.FuelSystem` (`FuelPercent`, `CurrentFuel`, `isRefueling`).
  HUD трекает пилотируемый корабль (`Update:141-159`), на хосте цифры верные (общая копия),
  на удалённом клиенте — stale (бар стоит, REFUEL-индикатор врёт).
- `MyShipsTab:368,375` уже читает `telemetry.fuelNormalized/fuelMax` — правильный паттерн, копируем его.
- Серверные чтения (`ShipController:304,1311,1372`, `MeziyModuleActivator:156`) — корректны (серверная копия),
  не тронуты. Неймсинг `isRefueling/thrustPenaltyMult/speedPenaltyMult` — не тронут (стиль, FIX13).

## Решение (минимальный дифф, 3 файла)

- `ShipTelemetryState`: `+ const FlagRefueling` + `+ byte flags` (bit0 = идёт дозаправка);
  сериализация + `Equals` (включён — иначе дельта не уйдёт) + `GetHashCode`.
- `ShipController.UpdateTelemetryState`: `flags` из `fuelSystem.isRefueling` (сервер).
- `ShipHudController` FUEL-блок: сначала телеметрия (`ShipTelemetryClientState.GetShipState(netId)`:
  `fuelPct = fuelNormalized`, `cur = pct*max`, `isRefuel = flags&bit`), fallback — локальный `fs`
  (host-edge / телеметрия ещё не пришла). Число `AtmosphericRefuelRate` для подписи — из локального
  конфига (это конфиг, не состояние).

## Изменения

- `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs`: flags-поле.
- `Assets/_Project/Scripts/Player/ShipController.cs`: 1 строка (flags в снапшот).
- `Assets/_Project/Scripts/Ship/UI/ShipHudController.cs`: FUEL-блок telemetry-first.
- `docs/Ships/ITERATIONS.md` — секция тикета.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): удалённый клиент-пилот жжёт топливо —
  HUD-бар едет синхронно с сервером; L на месте → REFUEL-индикатор; цифры `FUEL cur/max`
  совпадают с серверными; хост — без изменений; `MyShipsTab` — без изменений.
