# Markets — Documentation Index

Документация рыночной подсистемы Project C: The Clouds (Stage 2.5).

Исходный код живёт в `Assets/_Project/Trade/`. Архитектурный дизайн — `docs/dev/TRADE_V2_DESIGN.md`. GDD — `docs/gdd/GDD_22_Economy_Trading.md`.

## Навигация

| Документ | Назначение |
|----------|------------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Слои системы, диаграммы server↔client, потоки данных, ownership-границы |
| [FILES_INDEX.md](FILES_INDEX.md) | Каталог всех 53 .cs файлов (Scripts/Core/Network/Client/Config/Service/Repository/Dto + Editor) с кратким описанием |
| [FLOW_TRADE.md](FLOW_TRADE.md) | Полный путь операции: zone enter → E → subscribe → snapshot → buy → load → unload → sell |
| [FIXES_HISTORY.md](FIXES_HISTORY.md) | 4 фикса верстки 2026-06-04 (MarketWindow) + 1 фикс жизненного цикла (NetworkManagerController) + 1 фикс 2026-06-05 (per-ship cargo cache) |
| [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | Мелкие недочёты, оставленные на точечную правку |
| [INTEGRATION.md](INTEGRATION.md) | Связи с остальным проектом: NetworkPlayer, NetworkManagerController, BootstrapScene, WorldScene_0_0 |

## Краткое описание системы

**Серверная авторитетность.** Все цены, сток, склад, груз корабля, кредиты — только на сервере. Клиент показывает то, что прислал сервер (`MarketSnapshotDto`).

**Слои.**
- Server POCO: `TradeWorld` (singleton) → `MarketState` (per-location) → `MarketItemState` (per-item) → `Warehouse`/`CargoData` (per-player/location/ship).
- Server MonoBehaviour: `MarketServer` (NetworkBehaviour, RPC hub), `MarketTimeService` (tick loop), `MarketZone` (scene-placed).
- Client: `MarketClientState` (singleton, projection) → `MarketWindow` (UI Toolkit) → `MarketInteractor` (E-handler helper).

**Multi-ship.** `MarketZone` имеет два радиуса — `tradeRadius` (5-30м, по инспектору; в `WorldScene_0_0` для Primium = 30м) для игрока и `shipDockRadius` (30м) для кораблей. Корабли в радиусе попадают в `nearbyShips[]` снапшота + `shipCargos[]` (cargo ВСЕХ nearby ships, с 2026-06-05); UI показывает dropdown выбора корабля только если их 2+. Per-ship client cache в `MarketClientState.CurrentShipCargos` — мгновенное переключение между кораблями без roundtrip.

**Time-based экономика.** `MarketTimeService.MarketTimeMultiplier` (0.1x..100x) управляет частотой тика. Затухание спроса/предложения — half-life в секундах (time-based, не tick-based), одинаково ведёт себя при любом multiplier.

**Repository pattern.** `IPlayerDataRepository` → `PlayerPrefsRepository` (default, host) / `ServerFileRepository` (P1 stub, dedicated).

**UI.** UI Toolkit (UXML + USS). `MarketWindow` — единственный контроллер, читает `MarketClientState.CurrentSnapshot`, шлёт команды через `MarketClientState.RequestXxx()`.

**Текущий статус (2026-06-05).** Полный цикл работает: zone enter → E → snapshot → BUY/LOAD/UNLOAD/SELL → credits/warehouse обновляются. **Per-ship cargo cache** (cargo всех кораблей в зоне кэшируется на клиенте, переключение мгновенное). Подтверждено пользователем. 4 версточных фикса + per-ship cargo fix применены, компиляция чистая.

## Что НЕ задокументировано здесь (но относится к трейду)

- **Контракты** (`ContractSystem.cs` + `ContractBoardUI.cs` + `ContractData.cs` + `ContractTrigger.cs`) — отдельная подсистема, ссылается на `TradeMarketServer` (старый). Будет мигрирована на новый `MarketServer` отдельно.
- **Старая v1 архитектура** (`TradeUI.cs`, `TradeMarketServer.cs`, `PlayerTradeStorage.cs`, `PlayerDataStore.cs`, `LocationMarket.cs`, `MarketItem.cs`, `CargoSystem.cs`, `AutoTradeZone.cs`, `TradeTrigger.cs`, `PlayerCreditsManager.cs`, `PlayerDebt.cs`, `TradeSetup.cs`, `TradeSceneSetup.cs`, `TradeDebugTest.cs`, `TradeDebugTools.cs`, `NetworkPlayer.TradeBuyServerRpc/SellServerRpc/TradeResultClientRpc`) — сохранены в `Scripts/` (root) для референса. **Не используются** новой подсистемой. Cleanup — отдельный тикет, см. `TRADE_V2_INTEGRATION.md` §8.
