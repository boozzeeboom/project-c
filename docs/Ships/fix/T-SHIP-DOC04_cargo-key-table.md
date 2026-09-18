# T-SHIP-DOC04 — перепроверка: карго-таблица «без ключа» vs P5 guard

> Тикет: `T-SHIP-DOC04`. Дата: 2026-09-18. Статус: **ПОДТВЕРЖДЕНО**.
> Scope: только перепроверка + правка строки доки. Кода нет.

## Что проверяли

Утверждение ревью (§5 несостыковок): `Key 00_OVERVIEW.md §1.3` —
«Загрузить товары … ✓ (не требуют ключа)» — ложна после P5.

## Факт на текущем main (сверено grep)

Guard на месте, 4 RPC (P5 `f4d2c9f`):
- `Trade/.../ShipCargoServer.cs:115` (`RequestStoreToCargoRpc`) и `:242` (`RequestRetrieveFromCargoRpc`)
  → `KeyRodInstanceWorld.IsOwnerOfShip` + отказ «Вы не владелец».
- `Trade/Scripts/Network/MarketServer.cs:183` (`RequestLoadToShipRpc`) и `:211`
  (`RequestUnloadFromShipRpc`) → `TradeResultCode.NotOwner (=36)` (`TradeResultCode.cs:41`).
- Бонус (вне P5): `ContractServer.cs:252,304,323` — тоже `IsOwnerOfShip`.

Строка `§1.3` «Загрузить товары с рынка на корабль (в зоне) | ✓ (Cargo-операции не требуют ключа) | ✓»
противоречит коду: без ключа сервер отказывает.

## Решение

Статус ПОДТВЕРЖДЕНО. Фикс docs-only: строка таблицы `§1.3` исправлена
(«✗ BLOCKED (P5: NotOwner=36) + toast/результат»), остальная таблица не тронута.

## Проверка

- Читать `§1.3`: строка карго — с P5-статусом.
- Compile: не требуется (docs only).
