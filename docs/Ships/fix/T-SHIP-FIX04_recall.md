# T-SHIP-FIX04 — серверный авторитет Recall (владение + цена + пад)

> Тикет: `T-SHIP-FIX04` (P0, фаза B). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО)

`ShipController.RecallShipToPadServerRpc:2381-2422` (`InvokePermission.Everyone`):
- `padPosition` + `cost` — от клиента, без проверок → `cost=0` + телепорт **любого**
  корабля в **любую** точку (клиент выбирает объект вызова `sc` сам:
  `RepairManagerWindow.OnRecallShipClicked:626-690` шлёт RPC на `sc` из своего словаря).
- Проверки владения нет (в отличие от модулей/карго). Дропдаун «свой корабль» — клиентский фильтр.
- Связи: `ShipPositionServer.Update` сейвит живые трансформы каждые 5 сек (`:13,:117`) →
  телепорт подхватывается автосейвом (хук не нужен, риск — только рестарт сервера <5 сек после recall).
  `ExitDocked()` ставит `_lastUndockTime` (`:162`) → grace урона (Damage) покрыт существующим вызовом (`:2404`).

## Решение (минимальный дифф, только `ShipController.cs`, сигнатура RPC и клиент не тронуты)

1. **Владение:** `KeyRodInstanceWorld.IsOwnerOfShip(clientId, NetworkObjectId)` — ключ от клиента
   не нужен, shipNetId сервер знает сам. Чужой → `Warning` + return (без списаний/телепорта).
2. **Цена:** `+ [SerializeField] _serverRecallCost = 500` (дефолт = `RepairManager._shipRecallCost`);
   клиентский `cost` игнорируется (mismatch → Warning). Списание до телепорта (порядок сохранён).
3. **Пад:** сервер ищет `DockingPadTriggerBox` (`FindObjectsByType`, редкая операция — только по RPC),
   сверяет клиентскую точку с ближайшим **свободным** падом (толерантность 15 м);
   телепорт — на **серверную** позицию пада, не на клиентскую. Нет свободного пада рядом → отказ.
   Замечание: `Player → Docking.Stations` — новая using-зависимость; цикла сборок нет
   (одна сборка), но архитектурно замостовано — кандидат на вынос в `DockingWorld`-lookup позже.
4. **Чистка:** не требовалась (мёртвого кода в зоне recall нет; `GetCurrentPitchInput/YawInput` —
   кандидат в FIX01/13, не здесь).

## Изменения

- `Assets/_Project/Scripts/Player/ShipController.cs`: `_serverRecallCost`, ownership-guard,
  серверная цена, валидация пада (толерантность 15 м, телепорт на серверную позицию).
- `docs/Ships/ITERATIONS.md` — секция тикета.
- `docs/dev/global_needtotest/SHIP_TESTS.md` — кейсы FIX04.

## Проверка

- Compile: Console → 0 CS-ошибок (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): свой recall за 500 (пад свободен рядом);
  чужой recall → отказ без списаний; `cost=0` → списаны 500; точка в чистом поле →
  отказ (нет пада рядом); recall на занятый пад → отказ; кредиты после recall = до − 500;
  рестарт сервера сразу после recall (<5 сек) — известный риск позиции (зафиксировать факт).
