# T-SHIP-DOC05 — перепроверка: scope cargo-guard (TradeWorld-уровень закрыт?)

> Тикет: `T-SHIP-DOC05`. Дата: 2026-09-18. Статус: **ПОДТВЕРЖДЕНО (разрыв план/факт)**.
> Scope: только перепроверка + пометка в доке. Кода нет.

## Что проверяли

Утверждение ревью (§6 несостыковок): `SHIP_REFACTOR_PLAN P5` — guard в
`ShipCargoServer + TradeWorld.TryLoad/Unload`; факт (`CARGO_OWNERSHIP_DESIGN` + P5 `f4d2c9f`) —
guard в `ShipCargoServer + MarketServer.RequestLoad/UnloadRpc`. TradeWorld-уровень не закрыт.

## Факт на текущем main (сверено чтением кода)

- Guard на RPC-слое подтверждён (см. DOC04): `ShipCargoServer.cs:115,242`,
  `MarketServer.cs:183,211` (`NotOwner=36`).
- `TradeWorld.TryLoadToShipCore (:753-790)` и `TryUnloadFromShipCore (:881+)`:
  валидация только `InvalidArgs`/`NotInZone`, затем склад/трюм напрямую.
  **`IsOwnerOfShip` в `TradeWorld.cs` нет** — любой серверный вызывающий
  (другой RPC, будущий код, тесты) обходит владение. Defense-in-depth разрыв:
  закрыт RPC-слой, открыт доменный слой.
- Попутно закрыт P1-check из ревью (лимиты): `TryLoadToShipCore:766-775` делает
  pre-check через `ShipCargoRegistry.GetEffectiveLimits()` + `cargo.SetLimitsOverride(...)`
  (`TryCheckEffectiveCargoLimits:803-849`, force-register `:856-869`).
  Сервер использует per-instance effective лимиты, а не чистую статику —
  расхождения HUD/сервер по лимитам нет (остался нюанс Light-фолбэка `:813-825`).
- `CARGO_OWNERSHIP_DESIGN.md §2` описывает 4 точки (А: ShipCargoServer, Б: MarketServer) —
  факт соответствует дизайну; расхождение только с формулировкой `SHIP_REFACTOR_PLAN P5`
  («TradeWorld-уровень»).

## Решение

Статус ПОДТВЕРЖДЕНО. Фикс docs-only: баннер в `CARGO_OWNERSHIP_DESIGN.md §2`
(«guard — RPC-слой; `TradeWorld.Try*` без guard — известный разрыв, кандидат в `T-SHIP-FIX11`»)
+ баннер-уточнение в `SHIP_REFACTOR_PLAN_2026-07-21.md` P5 (факт: MarketServer, не TradeWorld).
Кодовый guard доменного слоя — НЕ этот тикет (уйдёт в `T-SHIP-FIX11`).

## Проверка

- Читать оба дока: баннеры на месте.
- Compile: не требуется (docs only).
