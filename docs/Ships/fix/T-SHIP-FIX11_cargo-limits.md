# T-SHIP-FIX11 — store-путь на effective-лимитах (бонусы модулей)

> Тикет: `T-SHIP-FIX11` (P1, фаза C). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса, чтением кода — ПОДТВЕРЖДЕНО, настоящий баг)

- `ShipCargoServer.RequestStoreToCargoRpc` шаг 2 (`:177-178`): `cargo.TryAdd(...)` напрямую.
  `CargoData.TryAdd` берёт `_limitsOverride ?? ShipClassLimits.Get(shipClass)` (`CargoData.cs:87`),
  а override ставит только `TradeWorld.TryLoadToShipCore` через приватный
  `TryCheckEffectiveCargoLimits` (`TradeWorld.cs:770-775,803-849`). Итог: путь
  инвентарь→трюм (консоль) проверял **статические** лимиты, а HUD/телеметрия показывали
  **эффективные** (`GetEffectiveCargoLimits`, base + бонусы модулей) и рыночный путь их же
  enforced. Корабль с `MODULE_CARGO_BAY_01`: HUD показывает свободное место, store отвечает
  «Трюм полон» (или наоборот при Light-фолбэке).
- Retrieve-путь (`TryRemove`) лимитов не касается — не тронут. `TryAddContractOwned` —
  контрактный путь, вне скоупа (кандидат в отдельный тикет).
- Курс обмена: перепроверен в FIX10 (один SO) — здесь не дублируем.

## Решение (минимальный дифф, 2 файла, зеркало рыночного пути)

- `TradeWorld.TryCheckEffectiveCargoLimits`: `private` → `public` + коммент
  (точка входа для серверных вызывающих; force-register + override как раньше).
- `ShipCargoServer.RequestStoreToCargoRpc` шаг 2: перед `TryAdd` — тот же pre-check;
  при непроходе — rollback в инвентарь (`RollbackReturnItems`, как ниже по коду) +
  отказ «Трюм полон: причина». Поведение при совпадении лимитов — побайтово как было.

## Изменения

- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs`: видимость метода + коммент.
- `Assets/_Project/Trade/Exchange/Network/ShipCargoServer.cs`: pre-check + rollback в store.
- `docs/Ships/ITERATIONS.md` — секция тикета.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): корабль с расширителем трюма — store
  принимает сверх статического max (до effective); HUD `cargoMax` = серверному решению;
  переполнение сверх effective — отказ «Трюм полон» + предметы возвращены в инвентарь
  целиком (rollback); без модулей — поведение как раньше; retrieve — без изменений.
