# T-SHIP-FIX10 — консоль груза: стаки + отложенный refresh

> Тикет: `T-SHIP-FIX10` (P1, фаза C). Дата: 2026-09-18. Статус: **ИСПРАВЛЕНО (код + дока)**.
> Тесты — за пользователем (кейсы также в `docs/dev/global_needtotest/SHIP_TESTS.md`).

## Перепроверка (до фикса — по пунктам)

1. ✅ **Стаки: ПОДТВЕРЖДЕНО.** `RefreshInventory:417-418` — `grouped[inv.itemId]++` считает записи,
   а `InventoryItemDto.quantity` (стак 1..maxStack, `InventoryItemDto.cs:31`) игнорируется →
   `entry.count` занижен → `OnStoreClicked:546` хронически «Недостаточно»,
   `GetInvQtyMax:600` врёт. Чиню: суммировать `quantity`.
2. ❌ **Курс клиент/сервер: ОПРОВЕРГНУТО.** Клиент — `ResourceExchangeResolver.Default`
   (`Resources/Exchange/DefaultExchangeRate`), сервер — `_exchangeRateConfig` из сцены.
   Сверка GUID: поле сцены указывает на тот же `DefaultExchangeRate.asset`
   (`guid d10a814f...` совпал). Один SO — расхождения нет. Код не трогаем; риск только если
   кто-то переназначит поле в сцене (зафиксировано здесь).
3. ✅ **Refresh-мигание: ПОДТВЕРЖДЕНО.** `HandleResult:635` делает `RefreshData()` сразу,
   телеметрия отстаёт ~200мс+ → stale-мигание. Чиню: отложенный refresh 0.5с корутиной
   (статус-текст результата показываем сразу, данные перечитываем после sync).
4. ➖ **Retrieve O(n) `AddItemDirect` (`ShipCargoServer:308`): корректен, оставляем.**
   Батч-переписывание серверного обмена — риск без выигрыша для тикета (perf-note, не баг).
5. ❌ **Утечка `_opTimestamps`: ОПРОВЕРГНУТО.** `CheckRateLimitOrResult:377` уже чистит
   записи старше 60с (`RemoveAll`); на клиента — одна маленькая запись. Не трогаем.
6. ➖ **Нет IsDocked-гейта на cargo-RPC: оставляем как дизайн.** Рыночный путь покрыт
   `MarketZone`, консольный — клиентской proximity; серверный гейт — смена поведения,
   отдельным решением (кандидат в будущий тикет, не этот).

## Изменения (только `ShipCargoConsoleWindow.cs`)

- Группировка по `quantity` (`+= Max(1, qty)`) — чинит `count`, `OnStoreClicked`, `GetInvQtyMax`.
- `HandleResult` → статус сразу + `DelayedRefreshData(0.5s)`; `+ using System.Collections`.

## Проверка

- Compile: Console → 0 errors/warnings (MCP; см. секцию ITERATIONS).
- Tests: Test Runner — за пользователем.
- Manual (за пользователем, также в реестре): стак 50 шт → `count` показывает 50, упаковка
  всего стака проходит с первого раза; max-кнопка даёт верный предел; после store/retrieve
  UI не мигает stale (данные догоняют за ~0.5с); клиент/сервер курсы совпадают (packable везде).
