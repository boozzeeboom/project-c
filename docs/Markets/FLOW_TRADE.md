# Markets — Trade Flow

Полный путь типовой операции. Один проход = BUY + LOAD + UNLOAD + SELL в одном `MarketZone_Primium`.

## 0. Предусловия

- Хост запущен (`NetworkManager.IsListening == true`).
- `[MarketServer]` в `BootstrapScene` имеет `IsSpawned = true`, `OnNetworkSpawn` отработал:
  - `_repository = PlayerPrefsRepository`
  - `_resolver = DatabaseResolver(tradeDatabase)`
  - `TradeWorld.CreateAndInitialize(marketConfigs, ...)` — markets=4, npcTraders=4, events=1
  - `_timeService.onMarketTick.AddListener(BroadcastSnapshotsToAll)`
- `[MarketClientState]` в `BootstrapScene` жив (`Instance != null`).
- `[MarketWindow]` в `BootstrapScene` жив, UXML загружен, layout valid.
- `MarketZone_Primium` в `WorldScene_0_0` заспавнен (scene-placed), `MarketZoneRegistry._zones["primium"] = this`.
- Игрок (host) управляет `NetworkPlayer`, `OwnerClientId = 0`.
- У игрока `credits = 1000` (стартовые из `PlayerPrefsRepository`).
- 2 корабля в `shipDockRadius` зоны Примум (например, в `WorldScene_0_0` их 3).

## 1. Zone enter (client + server)

### 1.1. SphereCollider триггерит (быстрый путь)

```
Frame N (вход в tradeRadius):
  MarketZone.OnTriggerEnter(other = player collider)
    ├─ other.GetComponentInParent<NetworkPlayer>() → np
    ├─ dist = Vector3.Distance(zonePos, np.pos) ≤ tradeRadius? → yes
    ├─ if _isServer: _playersInZone.Add(np.OwnerClientId = 0)
    │     └─ Debug.Log "[MarketZone:primium] server detected player in zone: clientId=0"
    └─ if np.IsOwner: MarketZoneRegistry.LocalPlayerZone = this
          └─ Debug.Log "[MarketZone:primium] client: local player entered zone (dist=28,7)"
```

### 1.2. Polling (fallback + cleanup)

Каждые `POLL_INTERVAL = 0.25s` в `MarketZone.Update`:

**Server:**
- `PollPlayersInRadius()` — `Physics.OverlapSphere(zonePos, tradeRadius, ~0, Ignore)`. Из найденных коллайдеров `GetComponentInParent<NetworkPlayer>()` → `_playersInZone.Add(clientId)`. Debounce: игрок удаляется только после `MISS_THRESHOLD = 3` подряд пропусков (~0.75s).
- `PollShipsInRadius()` — `Physics.OverlapSphere(zonePos, shipDockRadius)`. Из найденных `ShipController` → `_shipsInZone.Add(NetworkObjectId)`. Без debounce (корабли статичнее).

**Client:**
- `PollLocalPlayerZone()` — ищет `NetworkPlayer` с `IsOwner = true`, считает `dist`. Если `dist ≤ tradeRadius` → `LocalPlayerZone = this`, иначе `LocalPlayerZone = null`.

## 2. E нажата (NetworkPlayer.Update)

```csharp
// NetworkPlayer.cs:296-312
if (Keyboard.current.eKey.wasPressedThisFrame) {
    FindNearestInteractable();
    if (_nearestChest != null || _nearestNetworkChest != null) {
        TryPickup();  // сундук — приоритет
    } else {
        if (!ProjectC.Trade.Client.MarketInteractor.TryOpenMarket()) {
            TryPickup();  // не в рыночной зоне — pickup
        }
    }
}
```

Сундуков рядом нет → вызов `MarketInteractor.TryOpenMarket()`.

## 3. TryOpenMarket (client)

```csharp
// MarketInteractor.cs:23-62
var zone = MarketZoneRegistry.LocalPlayerZone;  // = MarketZone_Primium
// DIAG log: "[MarketInteractor] TryOpenMarket: enter — LocalPlayerZone=primium, Registry.All.Count=1"
if (zone == null) {
    zone = FindNearestZone();  // fallback, log дистанций ко всем зонам
    if (zone == null) return false;
    MarketZoneRegistry.LocalPlayerZone = zone;
}
var state = MarketClientState.Instance;
state.RequestSubscribeMarket(zone.LocationId);  // "primium"
var window = MarketWindow.Instance;
window.Show();
return true;
```

`Show()` сразу показывает окно, **не дожидаясь** snapshot (race fix — см. KNOWN_ISSUES §1). Если snapshot ещё не пришёл, в `_messageLabel` пишется "Загрузка рынка...".

## 4. SubscribeMarketRpc (RPC client→server)

```csharp
// MarketClientState.RequestSubscribeMarket → MarketServer.Instance.SubscribeMarketRpc
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void SubscribeMarketRpc(string locationId, RpcParams rpcParams = default) {
    ulong clientId = rpcParams.Receive.SenderClientId;  // = 0
    var zone = MarketZoneRegistry.Get(locationId);     // = MarketZone_Primium
    if (zone == null) {
        Debug.LogWarning($"[MarketServer] Subscribe from {clientId} rejected: zone '{locationId}' not found");
        return;
    }
    if (!zone.IsPlayerInZone(clientId)) {
        Debug.LogWarning($"[MarketServer] Subscribe from {clientId} rejected: player not in zone. PlayersInZone count={zone.PlayersInZone.Count}");
        return;
    }
    Debug.Log($"[MarketServer] Subscribe OK from {clientId} for zone 'primium' → sending snapshot");
    SendSnapshotToClient(clientId, zone);
}
```

## 5. SendSnapshotToClient (server)

```csharp
// MarketServer.cs:256-286
private void SendSnapshotToClient(ulong clientId, MarketZone zone) {
    var market = TradeWorld.Instance.GetMarket(zone.LocationId);  // MarketState{locationId="primium"}
    var itemDtos = BuildItemPriceDtos(market);  // [ItemPriceDto(mesium, 10, 80, v=0), ItemPriceDto(antigrav, 50, 40, v=0)]
    Debug.Log($"[MarketServer] SendSnapshotToClient: client=0 loc=primium items=2");

    var snapshot = new MarketSnapshotDto {
        locationId = "primium",
        displayName = "Примум",
        items = itemDtos,
        marketVersion = market.ComputeVersion(),
        warehouse = BuildWarehouseDtos(TradeWorld.Instance.GetWarehouseSnapshot(0, "primium")),
            // = null или [] (у игрока пока пусто)
        credits = TradeWorld.Instance.Repository.GetCredits(0),  // = 1000
        warehouseMaxWeight = Warehouse.DEFAULT_MAX_WEIGHT,
        warehouseMaxVolume = Warehouse.DEFAULT_MAX_VOLUME,
        warehouseMaxTypes = Warehouse.DEFAULT_MAX_ITEM_TYPES,
        nearbyShips = zone.BuildNearbyShipsDtos().ToArray(),  // 3 корабля
        marketTimeMultiplier = 1f,
        secondsUntilNextTick = 300f,
    };
    var target = FindNetworkPlayer(0);  // NetworkPlayer клиента 0
    target.ReceiveMarketSnapshotTargetRpc(snapshot);  // RPC: SendTo.Owner
}
```

## 6. ReceiveMarketSnapshotTargetRpc (client)

```csharp
// NetworkPlayer.cs:835-840
[Rpc(SendTo.Owner)]
public void ReceiveMarketSnapshotTargetRpc(MarketSnapshotDto snapshot, RpcParams rpcParams = default) {
    Debug.Log($"[NetworkPlayer:0] ReceiveMarketSnapshotTargetRpc: loc=primium items=2");
    ProjectC.Trade.Client.MarketClientState.Instance?.OnSnapshotReceived(snapshot);
}
```

```csharp
// MarketClientState.cs:56-61
public void OnSnapshotReceived(MarketSnapshotDto snapshot) {
    Debug.Log($"[MarketClientState] OnSnapshotReceived: loc=primium items=2 wh=0 ships=3 credits=1000");
    CurrentSnapshot = snapshot;
    OnSnapshotUpdated?.Invoke(snapshot);
}
```

## 7. UI projection (client)

```csharp
// MarketWindow.HandleSnapshot(snap)
_locationLabel.text = "Рынок: Примум";
_creditsLabel.text = "Кредиты: 1000 CR";
_warehouseInfoLabel.text = "Склад: 0 типов / 16";
_timeInfoLabel.text = "Скорость рынка: x1.0 | Тик через: 300с";
_itemList.itemsSource = snap.items;  // [mesium, antigrav]
_warehouseList.itemsSource = Array.Empty<WarehouseEntryDto>();
_cargoList.itemsSource = _cargoCache ?? Array.Empty<WarehouseEntryDto>();
_shipSelector.choices = ["Корабль #6", "Корабль #7", "Корабль #8"];  // 3 корабля
_shipSelectorContainer.style.display = (snap.nearbyShips.Length > 1) ? Flex : None;  // = Flex
// _selectedMarketItem = -1 (player не выбрал)
```

Игрок видит окно: "Рынок: Примум | 1000 CR | 2 товара (мезий 10 CR, антигравий 50 CR) | 3 корабля в зоне".

## 8. Игрок выбирает мезий, жмёт КУПИТЬ (qty=1)

### 8.1. UI

```csharp
// MarketWindow.OnBuyClicked
var snap = _state.CurrentSnapshot.Value;
int qty = ParseQty();  // 1
var it = snap.items[_selectedMarketItem = 0];  // mesium
_state.RequestBuy(snap.locationId, "mesium_canister_v01", 1);
```

### 8.2. RPC

```csharp
// MarketClientState.RequestBuy → MarketServer.Instance.RequestBuyRpc
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestBuyRpc(string locationId, string itemId, int quantity, RpcParams rpcParams = default) {
    ulong clientId = rpcParams.Receive.SenderClientId;  // 0
    if (!CheckRateLimit(clientId)) return;              // OK
    if (!ValidateInZone(clientId, locationId, out var zone)) {
        SendTradeResultToOwner(clientId, TradeResultDto_Fail(NotInZone, Buy, ...));
        return;
    }
    var r = TradeWorld.Instance.TryBuy(0, "primium", "mesium_canister_v01", 1);
    var dto = BuildTradeResultDto(r, Buy, "primium", "mesium_canister_v01", 1, 0, 0);
    SendTradeResultToOwner(0, dto);
    if (r.IsSuccess) SendSnapshotToClient(0, zone);
}
```

### 8.3. TradeWorld.TryBuy

```csharp
// TradeWorld.cs:169-221
public TradeResult TryBuy(ulong clientId, string locationId, string itemId, int quantity) {
    // 1. Validation
    if (itemId == null || quantity <= 0) return Fail(InvalidArgs, ...);
    if (!MarketExists("primium")) return Fail(MarketNotFound, ...);
    var market = _markets["primium"];
    var item = market.GetItem("mesium_canister_v01");
    if (item == null || item.config == null) return Fail(ItemNotInMarket, ...);
    if (!item.config.allowBuy) return Fail(ItemBuyDisabled, ...);
    item.RecalculatePrice();
    if (item.currentPrice <= 0f) return Fail(PriceInvalid, ...);
    if (item.availableStock < 1) return Fail(InsufficientStock, ...);  // stock=80, OK

    // 2. Credits
    float totalCost = item.currentPrice * quantity;  // 10 * 1 = 10
    float currentCredits = Repository.GetCredits(0);  // 1000
    if (currentCredits < totalCost) return Fail(InsufficientCredits, ...);  // OK

    // 3. Warehouse
    var warehouse = GetOrLoadWarehouse(0, "primium");  // cached, empty
    if (!warehouse.TryAdd("mesium_canister_v01", 1, Resolver, out var whFail)) return Fail(WarehouseFull*, ...);
    // OK, weight=10 < DEFAULT_MAX_WEIGHT

    // 4. Charge
    if (!Repository.TryModifyCredits(0, -10f, out var newCredits, out _)) {
        warehouse.TryRemove(...);  // rollback
        return Fail(InsufficientCredits, ...);
    }
    // newCredits = 990

    // 5. Market update
    item.availableStock -= 1;  // 79
    PriceFormula.ApplyBuy(item, 1);
    //   demandFactor += 1 * DEMAND_PER_UNIT_BOUGHT (e.g. 0.02), clamp [0..1.5]
    //   item.RecalculatePrice() — price может измениться незначительно
    Repository.SetWarehouse(0, "primium", warehouse.SaveToList());
    //   Persists [mesium_canister_v01 x1] to PlayerPrefs key PD_Warehouse_0_primium

    Debug.Log($"[TradeWorld] BUY client=0 loc=primium item=mesium_canister_v01 qty=1 cost=10 newCredits=990");
    return TradeResult.Ok(990, 79, warehouse, null);
}
```

### 8.4. Server → client

```csharp
// MarketServer.BuildTradeResultDto → MarketServer.SendTradeResultToOwner
var dto = new TradeResultDto {
    code = Ok,
    op = Buy,
    locationId = "primium",
    itemId = "mesium_canister_v01",
    quantity = 1,
    newCredits = 990,
    newStock = 79,
    shipNetworkObjectId = 0,
    updatedWarehouseSnapshot = BuildWarehouseDtos([{itemId="mesium_canister_v01", quantity=1}]),
    updatedCargoSnapshot = null,  // Buy не меняет cargo
};
target.ReceiveTradeResultTargetRpc(dto);  // → MarketClientState.OnTradeResultReceived
// + SendSnapshotToClient(0, zone) → обновлённый снапшот
```

### 8.5. UI feedback

```csharp
// MarketWindow.HandleTradeResult(result)
_messageLabel.text = "Куплено: mesium_canister_v01 x1";  // зелёный
_warehouseInfoLabel.text = "Склад: 1 типов / 16";
// (snapshot также обновит списки через HandleSnapshot)
```

## 9. Игрок переключается на вкладку СКЛАД, выбирает мезий, выбирает корабль, жмёт ПОГРУЗИТЬ

### 9.1. UI

```csharp
// MarketWindow.OnLoadClicked
var snap = _state.CurrentSnapshot.Value;
int qty = 1;
var wh = snap.warehouse[_selectedWarehouseItem = 0];  // mesium x1
ulong shipId = GetSelectedShipId();  // 6 (первый корабль в dropdown)
_state.RequestLoadToShip(snap.locationId, "mesium_canister_v01", 1, 6);
```

### 9.2. RPC + Server validation

```csharp
// MarketServer.RequestLoadToShipRpc
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestLoadToShipRpc(string locationId, string itemId, int quantity, ulong shipNetworkObjectId, RpcParams rpcParams = default) {
    ulong clientId = 0;
    if (!CheckRateLimit(clientId)) return;
    if (!ValidateInZone(clientId, locationId, out var zone)) { /* NotInZone */ return; }
    if (!zone.IsShipInZone(shipNetworkObjectId)) {  // ship 6 in shipDockRadius?
        SendTradeResultToOwner(clientId, TradeResultDto_Fail(ShipNotInZone, LoadToShip, ...));
        return;
    }
    var shipClass = ResolveShipClass(6);  // Light, Medium, HeavyI, HeavyII
    var r = TradeWorld.Instance.TryLoadToShip(0, "primium", "mesium_canister_v01", 1, 6, ShipClass.Light);
    var dto = BuildTradeResultDto(r, LoadToShip, "primium", "mesium_canister_v01", 1, 6, 0);
    SendTradeResultToOwner(0, dto);
    if (r.IsSuccess) SendSnapshotToClient(0, zone);
}
```

### 9.3. TradeWorld.TryLoadToShip

```csharp
// TradeWorld.cs:270-295
public TradeResult TryLoadToShip(ulong clientId, string locationId, string itemId, int quantity, ulong shipId, ShipClass shipClass) {
    if (itemId == null || quantity <= 0 || shipId == 0) return Fail(InvalidArgs, ...);

    var warehouse = GetOrLoadWarehouse(0, "primium");  // [{mesium, 1}]
    var cargo = GetOrLoadCargo(6, ShipClass.Light);     // []

    if (!warehouse.TryRemove("mesium_canister_v01", 1, out var whFail)) return Fail(WarehouseFull*, ...);
    // OK, warehouse now empty
    if (!cargo.TryAdd("mesium_canister_v01", 1, Resolver, out var cargoFail)) {
        warehouse.TryAdd(...);  // rollback
        return Fail(CargoFull*, ...);
    }
    // OK, cargo now [{mesium, 1}], weight=10 ≤ maxWeight (100 for Light), volume=0.5 ≤ 3, slots=1 ≤ 4

    Repository.SetWarehouse(0, "primium", warehouse.SaveToList());  // [] → PlayerPrefs
    Repository.SetCargo(6, cargo.SaveToList());  // [{mesium, 1}] → PlayerPrefs

    Debug.Log($"[TradeWorld] LOAD client=0 loc=primium ship=6 item=mesium_canister_v01 qty=1");
    return TradeResult.Ok(990, 0, warehouse, cargo);  // updatedCargo != null
}
```

### 9.4. UI

```csharp
// MarketWindow.HandleTradeResult(result)
_messageLabel.text = "Погрузка: mesium_canister_v01 x1";
// result.op == LoadToShip && result.updatedCargoSnapshot != null:
_cargoCache = result.updatedCargoSnapshot;  // [{mesium, 1}]
_cargoList.itemsSource = _cargoCache;
_cargoList.Rebuild();
// HandleSnapshot затем обновит _warehouseList → пустой
```

## 10. Игрок выбирает мезий на вкладке ГРУЗ КОРАБЛЯ, жмёт РАЗГРУЗИТЬ

```csharp
// MarketWindow.OnUnloadClicked
var cargo = SnapCargo(snap.Value);  // = _cargoCache = [{mesium, 1}]
var it = cargo[_selectedCargoItem = 0];
_state.RequestUnloadFromShip("primium", "mesium_canister_v01", 1, 6);

// → MarketServer.RequestUnloadFromShipRpc
// → TradeWorld.TryUnloadFromShip
//   ├─ cargo.TryRemove → OK, cargo=[]
//   ├─ warehouse.TryAdd → OK, warehouse=[{mesium, 1}]
//   ├─ Persist both
//   └─ return Ok(990, 0, warehouse, cargo) — updatedCargo=empty, updatedWarehouse=[{mesium,1}]
// → TradeResultDto
// → MarketWindow._cargoCache = [] (empty cargo)
```

## 11. Игрок выбирает мезий на вкладке СКЛАД, жмёт ПРОДАТЬ (qty=1)

```csharp
// MarketWindow.OnSellClicked
var it = snap.items[_selectedMarketItem = 0];  // mesium, currentPrice=10
_state.RequestSell("primium", "mesium_canister_v01", 1);

// → MarketServer.RequestSellRpc
// → TradeWorld.TrySell
//   ├─ warehouse.TryRemove → OK
//   ├─ item.RecalculatePrice → 10
//   ├─ revenue = 10 * 1 * 0.8f = 8  ← NPC-маржа 80% от цены покупки
//   ├─ Repository.TryModifyCredits(0, 8) → newCredits = 998
//   ├─ item.availableStock += 1 → 80 (восстановление)
//   ├─ PriceFormula.ApplySell(item, 1) → supplyFactor += 0.02
//   └─ return Ok(998, 80, warehouse, null)
// → TradeResultDto
// → MarketWindow: "Продано: mesium_canister_v01 x1", credits=998
```

**Итог одного прохода:** credits: 1000 → 990 (BUY) → 990 (LOAD) → 990 (UNLOAD) → 998 (SELL), warehouse: [] → [mesium,1] → [] → [mesium,1] → [], ship cargo: [] → [mesium,1] → [], stock на рынке: 80 → 79 (BUY) → 80 (SELL восстановил), цена: 10 → ~10 (supply + demand почти в балансе после round-trip).

## 12. Esc — закрыть окно

```csharp
// MarketWindow.Update
if (kb.escapeKey.wasPressedThisFrame && IsVisible()) Hide();
// Hide(): _root.pickingMode = Ignore, SetVisible(false) — main-container display:None
// При этом LocalPlayerZone остаётся = MarketZone_Primium, подписка активна,
// следующий E опять вызовет TryOpenMarket → подписка идемпотентна → snapshot шлётся повторно
```

## 13. Тик (каждые 300с при multiplier=1x, или 30с при 10x)

```
MarketTimeService.Update (server)
  → TradeWorld.MarketTick(dt = 300f)
    → 4 NPC-трейдера обменивают товары между primium/secundus/tertius/quartus
    → events.ShouldTrigger проверяются (мезиевая лихорадка при demandFactor ≥ 0.8)
    → demandFactor/supplyFactor: factor *= exp(-ln(2) * 300 / 1800) ≈ 0.892 (30-мин half-life)
    → item.availableStock += initialStock * regenPerTick
    → RecalculatePrice
  → onMarketTick event → MarketServer.BroadcastSnapshotsToAll
    → foreach zone, foreach clientId in PlayersInZone: SendSnapshotToClient
    → клиенты получают обновлённый snapshot
```

## 14. Out-of-zone edge case

Если игрок отошёл на 100м от зоны (вне tradeRadius), `MarketZone.PollPlayersInRadius` через 3 тика удалит его из `_playersInZone`. Следующий `RequestBuyRpc` → `ValidateInZone` → false → `TradeResultCode.NotInZone` → красное сообщение "Вы должны быть в зоне рынка".

Если корабль уплыл за shipDockRadius, `IsShipInZone(shipId) = false` → `TradeResultCode.ShipNotInZone` → "Корабль не в зоне причала".
