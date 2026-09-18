# T-SHIP-FIX02 — server-authoritative состав пилотов

> Тикет: `T-SHIP-FIX02` (P0, фаза B). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Clamp входных float закрыт в FIX01; здесь — только membership. Тесты — за пользователем
> (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО, с уточнением)

- `AddPilotRpc` / `RemovePilotRpc` (`ShipController.cs`, `SendTo.Everyone`): любой клиент мог
  вызвать напрямую с любым `clientId` — вписать «призрака» (корабль никогда не freeze'ится,
  `Count>0` блокирует IDLE) или выкинуть чужого пилота из списка.
- Живые вызывающие (проверено grep): `AddPilot` — только `NetworkPlayer:1574`
  (ветка посадки `SubmitSwitchModeRpc`, выполняется везде т.к. Everyone);
  `RemovePilot` — `NetworkPlayer:807` (OnNetworkDespawn), `:1531` (ветка выхода),
  `PilotSeatController.Exit:122` — **0 вызывающих** (мёртвый compat-путь, НЕ трогаем, снос в FIX13).
- Посадка уже ownership-gated выше: `SubmitSwitchModeRpc:1482-1500` (T-KEY-06,
  `MetaRequirementRegistry.CanPlayerUse`) — сервер отказывает без ключа до `AddPilot`.
  Мой фикс делает authoritative сам список (второй уровень).
- `NetworkManager.ServerClientId` — устоявшийся паттерн в проекте (30+ мест);
  чтение `Receive.SenderClientId` в Everyone-RPC — прецедент (`SubmitSwitchModeRpc:1484`).

## Решение (минимальный дифф, только `ShipController.cs`)

- `AddPilot` / `RemovePilot`: `if (!IsServer) return;` — мутирует только сервер;
  broadcast (`SendTo.Everyone`) сходится на всех (как было, минус прямые вызовы).
- `AddPilotRpc` / `RemovePilotRpc`: sender-guard —
  `SenderClientId != ServerClientId → Warning + return` (прямые клиентские вызовы/мод-клиент).
- Поведение честных клиентов не меняется: клиент жмёт F → сервер проверяет ключ →
  добавляет + рассылает; локальный `_pilots` клиента обновляется бродкастом
  (отправка ввода от этого не зависит — guard в `SubmitShipInputRpc` серверный).

## Изменения

- `Assets/_Project/Scripts/Player/ShipController.cs`: 4 гарда (2 entry + 2 RPC).
- `docs/Ships/ITERATIONS.md` — секция тикета.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): посадка/выход F (соло + кооп: второй пилот садится,
  оба в списке); выход последнего пилота с ENGINE ON → freeze; мод-клиент дергает
  `AddPilotRpc(chужойId)` / `RemovePilotRpc(пилот)` напрямую → игнор + Warning у сервера,
  состав не меняется; деспавн игрока в корабле → снят со списка везде.
