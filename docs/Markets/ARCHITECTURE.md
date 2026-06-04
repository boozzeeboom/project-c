# Markets — Architecture

## Слои

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  КЛИЕНТ (per-client MonoBehaviour + UI Toolkit)                            │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketWindow (UIDocument, UI Toolkit)                              │    │
│  │   • читает MarketClientState.CurrentSnapshot                      │    │
│  │   • дергает MarketClientState.RequestXxx()                         │    │
│  │   • Esc = Hide, остальное = через callbacks UXML/USS               │    │
│  └────────────────────────────────────────────────────────────────────┘    │
│           ▲ OnSnapshotUpdated / OnTradeResult events                       │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ MarketClientState (singleton MonoBehaviour, DontDestroyOnLoad)    │    │
│  │   • держит последний MarketSnapshotDto + последний TradeResultDto  │    │
│  │   • forwardит NetworkPlayer.ReceiveMarketSnapshotTargetRpc/Rpc     │    │
│  │   • Convenience API: RequestBuy/Sell/Load/Unload/Subscribe         │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │ RPC: FindNetworkPlayer(clientId).ReceiveXxxTargetRpc(...)      │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ NetworkPlayer (NetworkBehaviour)                                   │    │
│  │   • [Rpc(SendTo.Owner)] ReceiveMarketSnapshotTargetRpc(...)        │    │
│  │   • [Rpc(SendTo.Owner)] ReceiveTradeResultTargetRpc(...)           │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │ NetworkObject.Netcode (server→owner transport)                 │
└───────────┼─────────────────────────────────────────────────────────────────┘
            │
            ▼  SendTo.Server
┌─────────────────────────────────────────────────────────────────────────────┐
│  СЕРВЕР (NetworkBehaviour + server-only state)                             │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketServer (NetworkBehaviour, 1 шт, DontDestroyOnLoad)           │    │
│  │   • [Rpc(SendTo.Server, Owner)] RequestBuy/Sell/Load/UnloadRpc     │    │
│  │   • [Rpc(SendTo.Server, Owner)] SubscribeMarketRpc                 │    │
│  │   • Rate limit (maxOpsPerMinute, default 30)                       │    │
│  │   • Position validation через MarketZoneRegistry                   │    │
│  │   • Делегирует в TradeWorld.TryXxx()                                │    │
│  │   • SendSnapshotToClient() / BroadcastSnapshotsToAll()             │    │
│  │   • BuildItemPriceDtos / BuildWarehouseDtos / BuildTradeResultDto  │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │                                                                 │
│  ┌────────▼───────────────────────────────────────────────────────────┐    │
│  │ TradeWorld (POCO singleton, server-only)                           │    │
│  │   • Markets: Dictionary<locationId, MarketState>                  │    │
│  │   • _npcTraders: List<NPCTrader> (ГосКонвой, Ветер, Караванщик...) │    │
│  │   • _activeEvents: List<MarketEvent> (Мезиевая лихорадка)          │    │
│  │   • Repository: IPlayerDataRepository (PlayerPrefsRepository)      │    │
│  │   • Resolver: TradeItemDefinitionResolver (DatabaseResolver)       │    │
│  │   • TryBuy / TrySell / TryLoadToShip / TryUnloadFromShip           │    │
│  │   • MarketTick(dtSeconds) — NPC, events, decay, regen              │    │
│  │   • GetOrLoadWarehouse / GetOrLoadCargo (in-memory cache)          │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │ owns                                                            │
│  ┌────────▼───────────────────────────────────────────────────────────┐    │
│  │ POCO State: MarketState, MarketItemState, Warehouse, CargoData     │    │
│  │   • in-memory, не MonoBehaviour, не сериализуются в сцену          │    │
│  │   • создаются в TradeWorld.Initialize()                            │    │
│  └────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketTimeService (server-only MonoBehaviour)                      │    │
│  │   • Update() → tick timer → TradeWorld.MarketTick(dt)              │    │
│  │   • OnMarketTick event → MarketServer.BroadcastSnapshotsToAll()    │    │
│  │   • MarketTimeMultiplier (0.1x..100x, Range attribute)              │    │
│  │   • Опционально подписан на ServerWeatherController                 │    │
│  └────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketZone (scene-placed MonoBehaviour, ×N в WorldScene_X_Z)      │    │
│  │   • SphereCollider (radius = tradeRadius) для player detection     │    │
│  │   • OverlapSphere (radius = shipDockRadius) для ship detection     │    │
│  │   • _playersInZone: HashSet<ulong> (server)                        │    │
│  │   • _shipsInZone: HashSet<ulong> (server)                          │    │
│  │   • Регистрирует себя в MarketZoneRegistry по locationId           │    │
│  │   • BuildNearbyShipsDtos() — для снапшота                          │    │
│  └────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Поток данных: открытие рынка и покупка

```
NetworkPlayer.Update (пеший режим, E нажата)
  │
  │ 1. FindNearestInteractable() — есть ли рядом сундук?
  │    ├─ сундук есть → TryPickup() (НЕ открывать рынок)
  │    └─ сундука нет → MarketInteractor.TryOpenMarket()
  │
  ▼
MarketInteractor.TryOpenMarket()
  │ 2. Берёт MarketZoneRegistry.LocalPlayerZone (может быть stale)
  │ 3. Если null — FindNearestZone() с логом дистанций ко всем зонам
  │ 4. MarketClientState.RequestSubscribeMarket(locationId)
  │     │
  │     ▼
  │     MarketClientState
  │       └─ MarketServer.Instance.SubscribeMarketRpc(locationId)
  │          │
  │          ▼ (RPC: SendTo.Server, Owner)
  │          MarketServer.SubscribeMarketRpc()
  │            ├─ MarketZoneRegistry.Get(locationId) → валидация зоны
  │            ├─ zone.IsPlayerInZone(clientId) → валидация позиции
  │            └─ SendSnapshotToClient(clientId, zone)
  │               │
  │               ▼ (RPC: target.ReceiveMarketSnapshotTargetRpc)
  │               NetworkPlayer.ReceiveMarketSnapshotTargetRpc(snapshot)
  │                 └─ MarketClientState.OnSnapshotReceived(snapshot)
  │                    ├─ CurrentSnapshot = snapshot
  │                    └─ OnSnapshotUpdated event → MarketWindow.HandleSnapshot
  │                                              └─ UI отрисовывает списки, лейблы
  │
  │ 5. MarketWindow.Instance.Show()  ← вызывается ПАРАЛЛЕЛЬНО с подпиской
  │      └─ если snapshot ещё не пришёл → "Загрузка рынка..." (placeholder)
  │
  ▼
[игрок видит окно, выбирает товар, жмёт КУПИТЬ]
  │
  ▼
MarketWindow.OnBuyClicked
  │ 6. ParseQty() (clamp 1..9999)
  │ 7. _state.RequestBuy(locationId, itemId, qty)
  │    └─ MarketServer.Instance.RequestBuyRpc(...)
  │       │
  │       ▼ (RPC: SendTo.Server, Owner)
  │       MarketServer.RequestBuyRpc()
  │         ├─ CheckRateLimit(clientId) (sliding 60s window, 30 ops)
  │         ├─ ValidateInZone(clientId, locationId) → zone
  │         └─ TradeWorld.Instance.TryBuy(clientId, locationId, itemId, qty)
  │            │
  │            ▼ (sync, server-only)
  │            TradeWorld.TryBuy()
  │              ├─ валидация itemId/qty/locationId/market/item/price/stock/credits
  │              ├─ warehouse.TryAdd(itemId, qty, Resolver) ← лимиты
  │              ├─ Repository.TryModifyCredits(clientId, -totalCost)
  │              ├─ item.availableStock -= qty
  │              ├─ PriceFormula.ApplyBuy(item, qty) ← demand_factor ↑, RecalculatePrice
  │              ├─ Repository.SetWarehouse(clientId, locationId, ...) ← persist
  │              └─ return TradeResult.Ok(...)
  │         │
  │         ├─ BuildTradeResultDto(r, Buy, ...) → TradeResultDto
  │         ├─ SendTradeResultToOwner(clientId, dto) → target.ReceiveTradeResultTargetRpc
  │         │   └─ NetworkPlayer.ReceiveTradeResultTargetRpc → MarketClientState.OnTradeResultReceived
  │         │      └─ OnTradeResult event → MarketWindow.HandleTradeResult
  │         │         └─ зелёное сообщение "Куплено: mesium x1"
  │         └─ SendSnapshotToClient(clientId, zone) → обновлённый снапшот с новыми ценами
  │            └─ MarketWindow.HandleSnapshot → UI перерисовывается
  │
  ▼
[игрок переключается на вкладку СКЛАД]
  │
  ▼
MarketWindow._warehouseList уже заполнен в HandleSnapshot
  (warehouse приходит в каждом MarketSnapshotDto, обновляется автоматически)
```

## Поток данных: погрузка в корабль

```
[игрок на вкладке СКЛАД, выбирает мезий, выбирает корабль в dropdown, жмёт ПОГРУЗИТЬ]
  │
  ▼
MarketWindow.OnLoadClicked
  │ 8. GetSelectedShipId() → ulong shipNetworkObjectId из ShipSummaryDto
  │ 9. _state.RequestLoadToShip(locationId, itemId, qty, shipId)
  │    └─ MarketServer.Instance.RequestLoadToShipRpc(...)
  │       │
  │       ▼
  │       MarketServer.RequestLoadToShipRpc()
  │         ├─ CheckRateLimit
  │         ├─ ValidateInZone(clientId, locationId) → zone
  │         ├─ zone.IsShipInZone(shipId) — корабль в shipDockRadius?
  │         │   └─ если нет → TradeResultCode.ShipNotInZone
  │         ├─ ResolveShipClass(shipId) → читает CargoSystem.shipClass через SpawnManager
  │         └─ TradeWorld.Instance.TryLoadToShip(clientId, locationId, itemId, qty, shipId, shipClass)
  │            │
  │            ▼
  │            TradeWorld.TryLoadToShip()
  │              ├─ warehouse.TryRemove(itemId, qty)  ← снять со склада
  │              ├─ cargo.TryAdd(itemId, qty, Resolver) ← лимиты (вес, объём, слоты)
  │              │   └─ fail → warehouse.TryAdd (откат) → return Fail
  │              ├─ Repository.SetWarehouse(clientId, locationId, ...) ← persist
  │              ├─ Repository.SetCargo(shipId, ...) ← persist
  │              └─ return TradeResult.Ok(... updatedCargo)
  │         │
  │         ├─ SendTradeResultToOwner → MarketWindow видит updatedCargoSnapshot
  │         │   └─ MarketWindow._cargoCache = updatedCargoSnapshot → cargo list перерисовывается
  │         └─ SendSnapshotToClient → склад обновился в UI
```

## Поток данных: тик рынка (server)

```
MarketTimeService.Update() каждый кадр (server only)
  │
  │ _tickTimer += Time.deltaTime
  │ if _tickTimer >= CurrentTickInterval (= 300 / multiplier, clamped [1, 3600]):
  │
  ▼
TradeWorld.Instance.MarketTick(dt = CurrentTickInterval)
  │
  ├─ 1. NPC-трейдеры
  │    foreach trader in _npcTraders:
  │      if trader.ShouldTrade(fromMarket, toMarket):
  │        trader.ExecuteTrade(_markets)
  │          ├─ fromItem.availableStock -= volume
  │          ├─ fromItem.demandFactor += volume * DEMAND_PER_UNIT_BOUGHT
  │          ├─ toItem.availableStock += volume
  │          ├─ toItem.supplyFactor += volume * SUPPLY_PER_UNIT_SOLD
  │          └─ RecalculatePrice()
  │
  ├─ 2. Events: trigger check
  │    foreach evt in _activeEvents:
  │      if not active and not in cooldown:
  │        if evt.ShouldTrigger(_markets): evt.Activate()
  │          └─ foreach market: evt.ApplyToMarket(market) ← demand/supply × multiplier
  │
  ├─ 3. Time-based decay
  │    foreach market, foreach item:
  │      item.demandFactor = PriceFormula.DecayFactor(item.demandFactor, dt)
  │        (half-life 1800s, factor *= exp(-ln(2) * dt / 1800))
  │      item.supplyFactor = PriceFormula.DecayFactor(item.supplyFactor, dt)
  │      PriceFormula.RegenerateStock(item) ← initialStock * regenPerTick
  │      item.RecalculatePrice()
  │
  └─ 4. Event tick
       foreach evt in _activeEvents:
         evt.Tick(dt)  ← remainingSeconds -= dt, deactivate if <= 0
         if was active and now inactive:
           foreach market: evt.RemoveFromMarket(market) ← divide factors back

MarketTimeService.onMarketTick event
  └─ MarketServer.BroadcastSnapshotsToAll()
       foreach zone in MarketZoneRegistry.All:
         foreach clientId in zone.PlayersInZone:
           SendSnapshotToClient(clientId, zone)
             └─ MarketSnapshotDto с обновлёнными ценами → клиент
```

## Ownership-границы

| Данные | Кто пишет | Кто читает |
|--------|-----------|-----------|
| `MarketState` (цены, сток, factors) | `TradeWorld.TryXxx()` (server) | `MarketServer.SendSnapshotToClient` (server) |
| `MarketSnapshotDto` | `MarketServer` (server) | `MarketClientState` (client) |
| `Warehouse` (per clientId, locationId) | `TradeWorld.TryBuy/TryLoad/TryUnload` (server) | `MarketServer` (server) для снапшота |
| `CargoData` (per shipNetworkObjectId) | `TradeWorld.TryLoadToShip/TryUnloadFromShip` (server) | `MarketServer.SendSnapshotToClient` (через ShipSummaryDto) + `MarketZone.BuildNearbyShipsDtos` |
| `TradeResultDto` | `MarketServer` (server) | `MarketClientState` (client) |
| `credits` | `Repository.TryModifyCredits` (server) | `MarketServer` для снапшота |
| `currentCredits` на клиенте | ТОЛЬКО из `MarketSnapshotDto.credits` (не из DTO) | `MarketWindow._creditsLabel` |
| `LocalPlayerZone` | `MarketZone.PollLocalPlayerZone` (client) | `MarketInteractor.TryOpenMarket` (client) |
| `_playersInZone`, `_shipsInZone` | `MarketZone.PollPlayersInRadius / PollShipsInRadius` (server) | `MarketServer.ValidateInZone` (server) |

## Server-only state vs Client-only state

**Server-only** (POCO, in-memory, не NetworkBehaviour):
- `TradeWorld` (singleton) — markets, traders, events
- `MarketState`, `MarketItemState`
- `Warehouse`, `CargoData`
- `IPlayerDataRepository` — `PlayerPrefsRepository` (для host; для dedicated — P1)
- `TradeItemDefinitionResolver` — `DatabaseResolver`
- `PriceFormula` (static)
- `MarketEvent`, `NPCTrader` (POCO, [Serializable])

**Server-only** (MonoBehaviour, scene-placed или DontDestroyOnLoad):
- `MarketServer` (NetworkBehaviour) — RPC hub, валидация, DTO builders
- `MarketTimeService` (MonoBehaviour) — tick loop
- `MarketZone` (MonoBehaviour, scene-placed × N) — триггер, _playersInZone, _shipsInZone

**Client-only** (MonoBehaviour, singleton, DontDestroyOnLoad):
- `MarketClientState` (MonoBehaviour) — projection layer
- `MarketWindow` (MonoBehaviour) — UI Toolkit контроллер

**Static helpers** (используются на обеих сторонах):
- `MarketZoneRegistry` (static) — реестр зон + LocalPlayerZone
- `MarketInteractor` (static) — E-handler helper
- `NetworkingUtils` (static) — IsServerSafe/IsClientSafe

## Trade-off: почему POCO + DI в TradeWorld, а не NetworkBehaviour

1. **Testability** — `TradeWorld` можно создать в EditMode-тесте без `NetworkManager`, без `NetworkObject`. Достаточно мокнуть `IPlayerDataRepository` и `TradeItemDefinitionResolver`.
2. **No NGO overhead** — мутации `_markets` / `_playerStorages` не проходят через NGO. NetworkObject = 1 штука (MarketServer), а не 4 (по одному на локацию).
3. **Single source of truth** — все мутации в одном `TradeWorld`, не размазаны по нескольким NetworkBehaviour.
4. **Singleton lifecycle** — `TradeWorld` создаётся в `MarketServer.OnNetworkSpawn()` и умирает в `OnNetworkDespawn()`. Не DontDestroyOnLoad (POCO), не scene-placed. Живёт ровно столько, сколько сервер.

Компромисс: `MarketServer` обязан слать все RPC на `target = FindNetworkPlayer(clientId).ReceiveXxxTargetRpc(...)` (а не `SendTo.Owner`), потому что в NGO 2.x `SendTo.Owner` не работает с NetworkObject, отличным от PlayerObject. Это и есть главный work-around в новой архитектуре.
