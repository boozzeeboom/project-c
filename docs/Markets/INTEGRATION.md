# Markets — Integration

Связи рыночной подсистемы с остальным проектом. Что импортирует `Trade`, что импортируют из `Trade`, какие сцены содержат компоненты.

## 1. Кто вызывает `Trade` код (Trade — downstream)

### `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

**Импорты:**
- `using ProjectC.Trade;` (для `TradeMarketServer` — мёртвый, см. ниже)
- `using ProjectC.Trade.Dto;` (для `MarketSnapshotDto`, `TradeResultDto`)
- `using ProjectC.Trade.Network;` (для `MarketServer`)

**Использования (НОВЫЕ):**
- `NetworkPlayer.cs:307` — `ProjectC.Trade.Client.MarketInteractor.TryOpenMarket()` в E-handler (Update пешего режима)
- `NetworkPlayer.cs:835-840` — `ReceiveMarketSnapshotTargetRpc` (target для `MarketServer.ReceiveMarketSnapshotClientRpc`)
- `NetworkPlayer.cs:842-846` — `ReceiveTradeResultTargetRpc` (target для `MarketServer.ReceiveTradeResultClientRpc`)
- `NetworkPlayer.cs:851-857` — `RequestSetMarketTimeMultiplier` (debug: клиент → MarketServer.RequestSetTimeMultiplierRpc)

**Использования (МЁРТВЫЕ, к удалению):**
- `NetworkPlayer.cs:588-601` — `TradeBuyServerRpc` (SendTo.Server, проксирует в `TradeMarketServer.Instance.BuyItemServerRpc`)
- `NetworkPlayer.cs:606-617` — `TradeSellServerRpc` (аналогично)
- `NetworkPlayer.cs:626-662` — `TradeResultClientRpc` (SendTo.Owner, делегирует в `TradeUI.Instance.OnTradeResult`)
- `NetworkPlayer.cs:672-731` — `ContractRequestServerRpc`/`Accept/Complete/Fail` (проксируют в `ContractSystem.Instance.*`)
- `NetworkPlayer.cs:736-747` — `ContractListClientRpc` (SendTo.Owner, делегирует в `ContractBoardUI.Instance.OnContractsReceived`)
- `NetworkPlayer.cs:752-763` — `ContractResultClientRpc`

**Поток E (пеший режим):**
```csharp
// NetworkPlayer.Update, line 296-312
if (Keyboard.current.eKey.wasPressedThisFrame) {
    FindNearestInteractable();
    if (_nearestChest != null || _nearestNetworkChest != null) {
        TryPickup();                                              // сундук — приоритет
    } else {
        if (!ProjectC.Trade.Client.MarketInteractor.TryOpenMarket()) {
            TryPickup();                                          // не в зоне → pickup
        }
    }
}
```

### `Assets/_Project/Scripts/Core/NetworkManagerController.cs`

**Использование (FIX 3):**
- `Awake()` создаёт root GameObject `[MarketClientState]` с компонентом `MarketClientState`, `DontDestroyOnLoad`. Гарантирует наличие singleton до `NetworkManager.StartHost/StartClient`.

**Точный код (нужно проверить — не смотрел файл целиком):**
```csharp
private void Awake() {
    // ...existing logic...
    
    // FIX 3 (2026-06-04): гарантировать наличие MarketClientState singleton.
    // Без этого MarketClientState.Instance == null в NetworkPlayer.ReceiveMarketSnapshotTargetRpc
    // → NRE → snapshot не доходит до клиента.
    if (ProjectC.Trade.Client.MarketClientState.Instance == null) {
        var go = new GameObject("[MarketClientState]");
        go.AddComponent<ProjectC.Trade.Client.MarketClientState>();
        // DontDestroyOnLoad выставляется в MarketClientState.Awake (singleton pattern)
    }
}
```

### `Assets/_Project/Scripts/Player/ShipController.cs` (вне Trade)

**Использование (через `CargoSystem`):**
- `MarketServer.ResolveShipClass(shipNetworkObjectId)` (MarketServer.cs:401-407):
  ```csharp
  var no = NetworkManager.Singleton.SpawnManager.SpawnedObjects[shipNetworkObjectId];
  var cargo = no.GetComponent<CargoSystem>();
  return cargo != null ? cargo.shipClass : ShipClass.Light;
  ```
- `MarketZone.BuildNearbyShipsDtos()` (MarketZone.cs:326-367):
  ```csharp
  var cargoComp = sc.GetComponent<CargoSystem>();
  ShipClass cls = cargoComp != null ? cargoComp.shipClass : ShipClass.Light;
  var limits = ShipClassLimits.Get(cls);
  // currentWeight = cargo.ComputeTotalWeight(Resolver)
  // currentVolume = cargo.ComputeTotalVolume(Resolver)
  // currentSlots = cargo.ComputeTotalSlots(Resolver)
  ```

То есть **Trade зависит от наличия `CargoSystem` на каждом корабле**. Без `CargoSystem` — `shipClass = Light` (default), лимиты могут не совпадать с реальным кораблём. В `WorldScene_0_0` все корабли должны иметь `CargoSystem` (см. `INTEGRATION_SHIPS_TO_WORLD_0_0.md`).

### `ProjectC.World.Chest.NetworkChestContainer` (приоритет над рынком)

**Где:** `NetworkPlayer.FindNearestInteractable()` (NetworkPlayer.cs:452-482)
```csharp
// First check NEW NetworkChestContainer (higher priority)
var networkChests = FindObjectsByType<NetworkChestContainer>(...);
foreach (var chest in networkChests) {
    if (dist < chest.GetOpenRadius()) {
        _nearestNetworkChest = chest;
        return;  // chests take priority over markets
    }
}
```

Если рядом сундук — `TryOpenMarket` НЕ вызывается. Это сделано чтобы не было конфликта «что открывается по E — сундук или рынок».

### `ProjectC.Core.ServerWeatherController` (опциональная подписка)

**Где:** `MarketTimeService.cs:44, 109, 117-125`
```csharp
[SerializeField] private ProjectC.Core.ServerWeatherController weatherController;
[SerializeField] private bool useWeatherFactor = false;
// ...
private float ComputeWeatherFactor() {
    if (weatherController == null) return 1f;
    float hour = weatherController.TimeOfDay;  // 0..24
    float t = (hour - 12f) / 12f * Mathf.PI;
    return Mathf.Lerp(0.5f, 1.0f, (Mathf.Cos(t) + 1f) * 0.5f);
}
```

**По умолчанию `useWeatherFactor = false`** — рынок не зависит от погоды. Если в инспекторе включить + перетащить WeatherController, то `MarketTimeMultiplier` умножится на 0.5..1.0 (днём 1.0, ночью 0.5).

---

## 2. Что `Trade` использует из остального проекта (Trade — upstream)

| Из | Что использует |
|----|----------------|
| `ProjectC.Player.NetworkPlayer` | `ReceiveMarketSnapshotTargetRpc` target; в RPC вызовах: `FindNetworkPlayer(clientId)` через `NetworkManager.ConnectedClients[clientId].PlayerObject.GetComponent<NetworkPlayer>()` |
| `ProjectC.Player.ShipController` | `GetComponent<CargoSystem>().shipClass` (для определения класса корабля) |
| `Unity.Netcode` | `NetworkBehaviour`, `[Rpc]`, `RpcParams`, `SendTo.Server/Owner`, `RpcInvokePermission.Owner` |
| `UnityEngine.UIElements` | `UIDocument`, `VisualTreeAsset`, `StyleSheet`, `ListView`, `DropdownField`, `Button`, `Label`, `TextField`, `PickingMode` |
| `ProjectC.Items.Inventory` | (косвенно) — `Inventory` хранит `credits`? Нет, кредиты в `PlayerPrefsRepository`. Inventory хранит пикапы. Не пересекается. |

---

## 3. Сцены

### `Assets/_Project/Scenes/BootstrapScene.unity`

Содержит (в дополнение к `NetworkManager` + `NetworkManagerController` + `ClientSceneLoader` + `ScenePlacedObjectSpawner`):

| GameObject | Компонент | Назначение |
|------------|-----------|------------|
| `[MarketServer]` | `NetworkObject` + `MarketServer` | RPC hub. `Trade Database` = `TradeItemDatabase.asset`, `Market Configs` = 4 MarketConfig'а, `Max Ops Per Minute` = 30 |
| `[MarketServer]` (тот же) | `MarketTimeService` | Tick loop. `Base Tick Interval Seconds` = 300, `Market Time Multiplier` = 1.0, `Use Weather Factor` = false |
| `[MarketClientState]` | `MarketClientState` (DontDestroyOnLoad) | Singleton projection layer. **Создаётся автоматически** в `NetworkManagerController.Awake` (FIX 3) |
| `[MarketWindow]` | `UIDocument` + `MarketWindow` | UI Toolkit окно. `Panel Settings` = `MarketPanelSettings.asset`, `Source Asset` = `MarketWindow.uxml` (или Resources fallback). `Visible On Start` = false |

### `Assets/_Project/Scenes/World/WorldScene_0_0.unity`

| GameObject | Компонент | Назначение |
|------------|-----------|------------|
| `MarketZone_Primium` | `SphereCollider` (isTrigger) + `MarketZone` | Триггер зоны рынка Примум. `Location Id` = "primium", `Display Name` = "Примум", `Trade Radius` = 30 (см. ниже), `Ship Dock Radius` = 30 |
| `MarketZone_Sellshittest` | `SphereCollider` (isTrigger) + `MarketZone` | Debug-зона. `Location Id` = "TEST_1" |
| `ships` (root) | 3× `ShipController` + `NetworkObject` + `CargoSystem` + `NetworkTransform` | Корабли в зоне `shipDockRadius` Примум. CargoSystem обязателен — иначе MarketServer.ResolveShipClass вернёт Light по умолчанию |

**Важно:** `tradeRadius` в `WorldScene_0_0` стоит **30** (а не 5 как в `INTEGRATION_SHIPS_TO_WORLD_0_0.md`). Это видно из логов диагностики (`dist=28,7` в `LocalPlayerZone entered`). 5 — было ошибочное значение в спеке, фактический — 30.

---

## 4. Editor-ассеты и ScriptableObject-ассеты

### Что создать вручную (если удалять/пересоздавать)

См. `docs/dev/TRADE_V2_INTEGRATION.md` §1-3 (полная инструкция). Кратко:

1. **PanelSettings asset** — `Create → UI Toolkit → Panel Settings Asset` в `Assets/_Project/Trade/Resources/UI/`. Имя: `MarketPanelSettings`.

2. **TradeItemDefinition** — 3 ассета: `TradeItem_Mesium_v01` (id=`mesium_canister_v01`, basePrice=10, weight=10, volume=0.5, slots=1), `TradeItem_Antigrav_v01` (id=`antigrav_ingot_v01`, basePrice=50, weight=5, volume=0.2, slots=1), `TradeItem_Latex_v01` (id=`latex_roll_v01`, basePrice=5, weight=8, volume=1.0, slots=1).

3. **TradeDatabase** — `TradeItemDatabase.asset` со ссылками на 3 `TradeItemDefinition`.

4. **MarketConfig** × 4 — `MarketConfig_Primium/Secundus/Tertius/Quartus.asset` в `Assets/_Project/Trade/Data/Markets/`. Каждый: `locationId`, `displayName`, `items: List<MarketItemConfig>` с ссылками на `definition` + `basePrice` + `initialStock` + `regenPerTick`.

5. **NetworkPrefabs** — НЕ присвоен `DefaultNetworkPrefabs.asset` (known issue, см. AGENTS.md). Scene-placed объекты работают по `GlobalObjectIdHash`, dynamic spawn — сломан (не критично для рынка).

---

## 5. Сетевые тонкости

### Почему `FindNetworkPlayer(clientId).ReceiveXxxTargetRpc` вместо `SendTo.Owner` напрямую

**Проблема:** В NGO 2.x `SendTo.Owner` работает ТОЛЬКО для NetworkBehaviour'ов, которые являются PlayerObject игрока. `MarketServer` — обычный `NetworkObject`, не PlayerObject. `SendTo.Owner` на нём отправит RPC ВСЕМ клиентам (или ни одному).

**Решение:** Сервер находит нужный `NetworkPlayer` через `NetworkManager.ConnectedClients[clientId].PlayerObject.GetComponent<NetworkPlayer>()` и вызывает `target.ReceiveMarketSnapshotTargetRpc(snapshot)`. У `ReceiveMarketSnapshotTargetRpc` стоит `[Rpc(SendTo.Owner)]` — теперь это работает, т.к. вызов идёт на PlayerObject.

**Код:** `MarketServer.cs:282-285, 290-293, 409-415`:
```csharp
private void SendSnapshotToClient(ulong clientId, MarketZone zone) {
    // ... build snapshot ...
    var target = FindNetworkPlayer(clientId);
    if (target == null) return;
    target.ReceiveMarketSnapshotTargetRpc(snapshot);
}

private static NetworkPlayer FindNetworkPlayer(ulong clientId) {
    var nm = NetworkManager.Singleton;
    if (nm == null) return null;
    if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return null;
    return client.PlayerObject?.GetComponent<NetworkPlayer>();
}
```

### `InvokePermission = RpcInvokePermission.Owner`

Все client→server RPC в `MarketServer` имеют этот атрибут:
```csharp
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
public void RequestBuyRpc(string locationId, string itemId, int quantity, RpcParams rpcParams = default) { ... }
```

Это означает, что ТОЛЬКО владелец NetworkObject может вызвать этот RPC. Поскольку `MarketServer` — синглтон (1 NetworkObject на весь рантайм), только **owner** этого NetworkObject может слать RPC. **Но** `MarketServer` — server-spawned, у него может не быть `OwnerClientId`. На практике NGO трактует server-spawned без owner'а как «любой может слать» в host mode и «никто не может» в dedicated. Проверить!

Workaround в коде: `MarketClientState.RequestBuy` (MarketClientState.cs:93-103) вызывает `MarketServer.Instance.RequestBuyRpc(...)` напрямую — это работает в host mode (host = server + owner). В dedicated server клиент должен слать RPC как-то иначе — НЕ ПРОВЕРЕНО.

### `RequireOwnership = true` vs `InvokePermission = RpcInvokePermission.Owner`

В дизайне `TRADE_V2_DESIGN.md` §2.5 указано `RequireOwnership = true`, но в коде используется `InvokePermission = RpcInvokePermission.Owner` (новый API NGO 2.x). Эти атрибуты эквивалентны в NGO 2.x.

---

## 6. Тестирование

### Ручное (по GDD_22 §14)

- Compile: `0 errors`
- Manual: `BootstrapScene` → Play → host появится → подойти к `MarketZone_Primium` → E → окно открылось → BUY/SELL/LOAD/UNLOAD работают
- Multiplayer: 2 инстанса (Editor + Standalone) → оба видят одинаковые цены, оба могут торговать

### Автоматическое

**Не реализовано.** `Assets/_Project/Tests/` пуст, asmdef нет. Добавление:
- EditMode тест для `PriceFormula` (pure function, без Unity)
- EditMode тест для `Warehouse.TryAdd/TryRemove` (лимиты веса/объёма/типов)
- EditMode тест для `MarketState` (decay, recalculation)
- PlayMode тест для `MarketServer` RPC flow (нужен NetworkManager + mock client) — сложно

План: P1, после стабилизации. См. AGENTS.md "Don't touch .asmdef" — нужно user approval для создания Tests asmdef.

---

## 7. Что НЕ интегрировано (out of scope Stage 2.5)

- `ContractSystem` — миграция на новый `MarketServer` — отдельный тикет
- `DefaultNetworkPrefabs.asset` — known issue, см. AGENTS.md
- `WorldSceneManager` / `ServerSceneManager` — стриминг инфраструктура, не развёрнута
- `FloatingOriginMP` — удалён из проекта (см. `NetworkPlayer.cs:95-96, 127, 176, 569-580` — комментарии "removed, scene-based doesn't need")
- `.asmdef` для Trade — запрещено auto-create (AGENTS.md)
- SQLite/Addressables для repository — P1
- `IPlayerReputationRepository` — Stage 5+

---

## 8. Cleanup checklist (когда пользователь одобрит)

См. `TRADE_V2_INTEGRATION.md` §8 + [KNOWN_ISSUES.md §3](KNOWN_ISSUES.md#3-старая-v1-архитектура-не-удалена). Краткий список:

1. **Сначала мигрировать `ContractSystem`** (он ссылается на старый `TradeMarketServer.Instance`)
2. **Удалить 16 legacy файлов** из `Assets/_Project/Trade/Scripts/`
3. **Удалить `NetworkPlayer.TradeBuy/SellServerRpc/TradeResultClientRpc`** (lines 583-662)
4. **Удалить 4 `Market_*.asset`** из `Assets/_Project/Trade/Data/Markets/`
5. **Удалить `docs/TRADE_SYSTEM_RAG.md`, `docs/TRADE_DEBUG_GUIDE.md`** (заменены на `docs/Markets/`)
6. **Запустить cleanup-сборку** → должно компилироваться с 0 errors и 0 warnings
7. **Полный тест** в host + multiplayer
