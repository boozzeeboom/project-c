# Trade System V2 — Design & Refactor Plan

**Проект:** Project C: The Clouds
**Автор:** Mavis (агент-напарник)
**Дата:** 2026-06-02
**Статус:** ⏳ На согласовании с пользователем

> Этот документ — **источник истины** для рефакторинга торговли/склада. Заменяет `docs/TRADE_SYSTEM_RAG.md` как RAG (после имплементации).
> Старый код остаётся как референс до конца миграции, потом удаляется.

---

## 0. TL;DR

- **Полностью переписать** серверный слой (`TradeMarketServer` + `PlayerDataStore` + `PlayerTradeStorage`) с чистой серверной моделью
- **Полностью переписать** клиентский UI на UI Toolkit (UXML/USS), сценарно управляемый (не хардкод)
- **Добавить две новые фичи** (явно запрошенные пользователем):
  - Time multiplier для рыночного тика (с привязкой к `ServerWeatherController`)
  - Multi-ship selection в зоне рынка
- **Сохранить** математику цены/спроса/предложения, GDD-формулы, NPC-трейдеров, события
- **Сохранить** `TradeItemDefinition`, `TradeDatabase` (содержат только данные, ОК)
- **Сохранить** `CargoSystem` (после небольшой сетевой доработки)
- **Удалить** `TradeUI.cs` (1200 строк), старый `PlayerTradeStorage` (компонент на NetworkPlayer), `PlayerDataStore` (PlayerPrefs-storage), `LocationMarket` (split: config + state), `TradeMarketServer` (1100+ строк), `NetworkPlayer.TradeBuyServerRpc/SellServerRpc/TradeResultClientRpc` (часть кода)
- **Удалить** `docs/TRADE_SYSTEM_RAG.md`, `docs/TRADE_DEBUG_GUIDE.md` (заменены этим документом)

---

## 1. Анализ старой системы (что было сломано)

### 1.1. По файлам

| Файл | Строк | Состояние | Главные проблемы |
|------|-------|-----------|-----------------|
| `TradeMarketServer.cs` | 1140 | Работает, но fragile | CSV-сериализация, локальные vs RPC paths, `FindObjectsByType` fallback, не учитывает позицию игрока |
| `TradeUI.cs` | 1200 | Работает, но | Хардкод Canvas, `_tradeLocked` + `_lastTradeTime` + ручные ClientRpc фильтры — многоуровневая защита от двойных RPC указывает на то, что базовая причина не устранена |
| `PlayerTradeStorage.cs` | 325 | MonoBehaviour на NetworkPlayer | Клиент-сайд `LoadToShip/UnloadFromShip` без RPC → dedicated server не увидит изменений |
| `PlayerDataStore.cs` | 151 | singleton, PlayerPrefs | **Сломан в dedicated server**: каждый процесс пишет в свой PlayerPrefs, синхронизации нет |
| `MarketItem.cs` | 198 | Serializable в SO | Хранение состояния (цены, сток) внутри ScriptableObject → теряется при рестарте сцены |
| `LocationMarket.cs` | 198 | ScriptableObject | То же: mutable state в SO + загрузка через `Resources.Load` (P1: не работает с Addressables) |
| `NetworkPlayer.cs` (trade часть) | ~80 RPC | RPC proxy | Фильтрация по `IsOwner` + workaround по `targetClientId` (8L) — оба хрупкие |
| `CargoSystem.cs` | 287 | Local MonoBehaviour | Не сетевой, нет авторитетности |

### 1.2. По симптомам из задания

| Симптом | Корневая причина | Где в коде |
|---------|------------------|------------|
| "1 продавалось как 2" | Двойной RPC + клиент-сайд мутации (если RPC упал, локально уже записали) | `TradeUI.TryBuyItem` + `OnTradeResult` + `SyncWarehouseItem` / `RemoveFromWarehouse` — **три места** изменения количества |
| "У хоста работало, у клиента нет" | `TradeResultClientRpc` фильтровал по `IsOwner` (которое на хосте == true, на клиенте == true), но `ClientRpcParams` с `TargetClientIds` не доезжал до клиента | `NetworkPlayer.TradeResultClientRpc` + 8L fix через `targetClientId` |
| "Склады были глобальные" | В `PlayerTradeStorage.Save()` ключ брался из `currentLocationId` но **fallback** на `"global"`; до 8F ключ был `PD_Warehouse_{clientId}` без локации | Старый код до сессии 8F, кое-где fallback остался |
| "Погрузка/разгрузка не работала" | Клиент-сайд `PlayerTradeStorage.LoadToShip` — `cargo.AddCargo` срабатывает только локально. На dedicated server груз не двигается, но клиент думает что сдвинул | `PlayerTradeStorage.LoadToShip` + `UnloadFromShip` |
| "Цены скакали" | `RecalculatePrice` без клампов до 8D; `eventMultiplier=0` давал цену 0; decay 0.846x за тик слишком агрессивный | `MarketItem.RecalculatePrice` + `DecayFactors` |
| "UI в хардкоде" | `BuildUI()` — 50+ строк `new GameObject()` с абсолютными позициями; невозможно править без перекомпиляции | `TradeUI.BuildUI` |

### 1.3. Скрытые баги, найденные при чтении

| Баг | Описание | Последствие |
|-----|----------|-------------|
| `LocKey.ToLower()` | `PD_Warehouse_{clientId}_{locationId.ToLower()}` — если кто-то пишет `PRIMIUM` vs `primium`, разные ключи, разные склады | Молча теряются предметы |
| `Resources.LoadAll` в билде | `TradeMarketServer.LoadAllMarkets` грузит `Trade/Markets` из Resources; в Addressables-friendly проекте — не масштабируется | Не работает с будущей Addressables-миграцией |
| `FindAnyObjectByType<NetworkPlayer>` | В `TradeUI.Player` getter — может вернуть чужого, если хост+клиент на одной машине | Под вопросом в dedicated server |
| `LoadFromPlayerDataStore` с сервера пишет в серверный PlayerPrefs, клиент в свой | При dedicated server клиент видит "0 CR" пока не получит RPC с `newCredits` | Уже фиксили 8G, но корень — PlayerPrefs |
| `AddComponent<PlayerTradeStorage>` на player в `FindPlayerStorage` | На сервере создаёт компонент динамически → на клиенте другой экземпляр, разные списки warehouse | Капитальный рассинхрон UI ↔ сервер |

---

## 2. Новая архитектура

### 2.1. Принципы

1. **Server is single source of truth** — все мутации warehouse/cargo/credits только на сервере
2. **Клиент — projection layer** — показывает то, что сервер прислал, мутации только через RPC
3. **State != config** — ScriptableObject = только config (товары, базовые цены); runtime state — на сервере, в `IPlayerDataRepository`
4. **UI — declarative** — UXML+USS, не код
5. **Position-aware validation** — сервер проверяет, что игрок физически рядом с `MarketZone` (новый компонент) перед любой операцией
6. **Time as first-class signal** — `MarketTimeService` подписан на `ServerWeatherController` события и даёт `MarketTickRate` (множитель)

### 2.2. Слои

```
┌──────────────────────────────────────────────────────────────┐
│  КЛИЕНТ (per-client MonoBehaviour + UI Toolkit)             │
│  ┌────────────────┐ ┌────────────────┐ ┌──────────────────┐  │
│  │ MarketHud      │ │ WarehousePanel │ │ ShipSelector     │  │
│  │ (UXML/USS)     │ │ (UXML/USS)     │ │ (UXML/USS)       │  │
│  └────────────────┘ └────────────────┘ └──────────────────┘  │
│           ▲              ▲                  ▲                │
│           │ Subscribe to │                  │                │
│  ┌────────┴──────────────┴──────────────────┴──────────────┐ │
│  │  MarketClientState (singleton) — последний snapshot    │ │
│  │  от сервера + набор отправленных, но не подтверждённых  │ │
│  │  команд                                                │ │
│  └────────┬───────────────────────────────────────────────┘ │
│           │ RPC SendTo.Server                               │
└───────────┼──────────────────────────────────────────────────┘
            ▼
┌──────────────────────────────────────────────────────────────┐
│  СЕРВЕР (NetworkBehaviour + server-only state classes)      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ MarketServer (NetworkBehaviour) — RPC приёмник,       │   │
│  │ 1 штука, DontDestroyOnLoad, ставится в Bootstrap     │   │
│  │ • RequestBuyServerRpc                               │   │
│  │ • RequestSellServerRpc                              │   │
│  │ • RequestLoadToShipServerRpc (itemId, qty, shipId)  │   │
│  │ • RequestUnloadFromShipServerRpc (itemId, qty,      │   │
│  │   shipId)                                           │   │
│  │ • SendMarketSnapshotClientRpc (RPC target)          │   │
│  │ • SubscribeRequestServerRpc                          │   │
│  └────────┬─────────────────────────────────────────────┘   │
│           │ делегирует                                        │
│  ┌────────▼─────────────────────────────────────────────┐   │
│  │ TradeWorld (server-only, NOT NetworkBehaviour)       │   │
│  │ • Markets: Dictionary<locationId, MarketState>      │   │
│  │ • PlayerStorages: IPlayerDataRepository              │   │
│  │ • NPCTraders, MarketEvents                           │   │
│  │ • MarketTick(dt) — вызывается из MarketTimeService   │   │
│  │ • TryBuy / TrySell / TryLoad / TryUnload            │   │
│  │   (возвращает TradeResult enum + сообщение)         │   │
│  └────────┬─────────────────────────────────────────────┘   │
│           │ owns                                              │
│  ┌────────▼─────────────────────────────────────────────┐   │
│  │ IPlayerDataRepository (interface)                   │   │
│  │ • GetCredits / SetCredits / ModifyCredits            │   │
│  │ • GetWarehouse / SetWarehouse (по locationId)        │   │
│  │ • GetCargo (по shipNetworkObjectId)                  │   │
│  │ • Impl: PlayerPrefsRepository (default, host)        │   │
│  │ • Impl: ServerFileRepository (P1, dedicated)         │   │
│  └────────┬─────────────────────────────────────────────┘   │
│           │ timer / events                                    │
│  ┌────────▼─────────────────────────────────────────────┐   │
│  │ MarketTimeService (server-only MonoBehaviour)        │   │
│  │ • TickInterval = baseInterval / MarketTimeMultiplier │   │
│  │ • Multiplier exposed via Inspector + RPC             │   │
│  │ • Optionally подписан на ServerWeatherController     │   │
│  │   OnTimeOfDayChanged / OnTemperatureChanged          │   │
│  │ • Каждый тик: TradeWorld.MarketTick(dt)              │   │
│  │ • После: MarketServer.SendMarketSnapshotAllClientRpc │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

### 2.3. ScriptableObject split (config vs state)

**`MarketConfig` (ScriptableObject, READ-ONLY, asset):**
- `locationId`, `displayName`
- `items: List<MarketItemConfig>` где `MarketItemConfig`:
  - `itemId: string`
  - `basePrice: float`
  - `initialStock: int`
  - `regenPerTick: float` (0..1, доля от initialStock)
  - `factionRestriction: Faction` (опц.)
  - Ссылка на `TradeItemDefinition` для UI-отображения (иконка, описание, вес/объём/слоты)

**`MarketState` (POCO, server-only, in-memory):**
- `locationId: string`
- `items: Dictionary<itemId, MarketItemState>` где `MarketItemState`:
  - `config: MarketItemConfig`
  - `currentPrice: float`
  - `availableStock: int`
  - `demandFactor: float` (clamped 0..1.5)
  - `supplyFactor: float` (clamped 0..1.5)
  - `eventMultiplier: float` (1.0 default, 0.5..2.0)
  - `version: int` (инкремент на каждое изменение → клиент видит "обновление")

**`LocationMarket.cs`** — переименовать/разделить на:
- `Assets/_Project/Trade/Scripts/Config/MarketConfig.cs` (SO)
- `Assets/_Project/Trade/Scripts/Config/MarketItemConfig.cs` (serializable struct)
- Старый `LocationMarket.cs` — **удалить**
- Старый `MarketItem.cs` — **удалить**, заменить на `MarketItemState` (POCO, in-memory)

### 2.4. Player storage — server-side only

**`Warehouse` (POCO, in-memory):**
- `ownerClientId: ulong`
- `locationId: string`
- `items: List<WarehouseEntry>` где `WarehouseEntry: { itemId: string, quantity: int }`

**`Cargo` (POCO, in-memory):**
- `shipNetworkObjectId: ulong` (NetworkObjectId, не clientId)
- `shipClass: ShipClass`
- `items: List<WarehouseEntry>`

**Repository pattern:**
- `IPlayerDataRepository` — интерфейс
- `PlayerPrefsRepository` — текущая реализация, помечаем как "host only / single-process"
- `ServerFileRepository` — заглушка (TODO, P1), файлы в `ServerData/{clientId}.json`

### 2.5. Сетевой API (новый)

**Server-side (`MarketServer` NetworkBehaviour):**
```csharp
[Rpc(SendTo.Server, RequireOwnership = true)]
public void RequestBuyRpc(string locationId, string itemId, int quantity, RpcParams rpcParams = default)

[Rpc(SendTo.Server, RequireOwnership = true)]
public void RequestSellRpc(string locationId, string itemId, int quantity, RpcParams rpcParams = default)

[Rpc(SendTo.Server, RequireOwnership = true)]
public void RequestLoadToShipRpc(string locationId, string itemId, int quantity, ulong shipNetworkObjectId, RpcParams rpcParams = default)

[Rpc(SendTo.Server, RequireOwnership = true)]
public void RequestUnloadFromShipRpc(string locationId, string itemId, int quantity, ulong shipNetworkObjectId, RpcParams rpcParams = default)

[Rpc(SendTo.Server, RequireOwnership = true)]
public void SubscribeMarketRpc(string locationId, RpcParams rpcParams = default)  // клиент "вошёл в зону" — сервер шлёт ему снепшот
[Rpc(SendTo.Server, RequireOwnership = true)]
public void UnsubscribeMarketRpc(string locationId, RpcParams rpcParams = default)

// Server → owner
[Rpc(SendTo.Owner)]
public void ReceiveMarketSnapshotRpc(MarketSnapshotDto snapshot, RpcParams rpcParams = default)

[Rpc(SendTo.Owner)]
public void ReceiveTradeResultRpc(TradeResultDto result, RpcParams rpcParams = default)
```

**Важно:** RPC с `RequireOwnership = true` → сервер может положиться на `rpcParams.Receive.SenderClientId` (вместо хрупкого `OwnerClientId` на стороне клиента). Это решает класс багов "хост видит, клиент нет".

### 2.6. Time multiplier — новая фича

**`MarketTimeService` (server-only MonoBehaviour, ставится в Bootstrap сцене рядом с NetworkManager):**
```csharp
[SerializeField] float baseTickIntervalSeconds = 300f;  // 5 минут
[SerializeField] float minTickIntervalSeconds = 5f;      // защита от спама
[SerializeField] float marketTimeMultiplier = 1f;        // пользовательский множитель

public float CurrentTickInterval => Mathf.Max(
    minTickIntervalSeconds,
    baseTickIntervalSeconds / marketTimeMultiplier
);

public void SetMultiplier(float mult);  // серверный вызов
[Rpc(SendTo.Server, RequireOwnership = true)]
public void SetMultiplierServerRpc(float mult, RpcParams rpcParams = default);  // для отладки
public float GetMultiplier();
public float GetTimeUntilNextTick();
```

**Подписка на `ServerWeatherController` (опционально, в инспекторе):**
- `bool useTimeOfDayMultiplier` — если true, дневной tick быстрее ночного (`Multiplier = 1.0` днём, `0.5` ночью, через lerp)
- `bool useTemperatureMultiplier` — холоднее → меньше товаров → меньше тиков

**Tick loop:**
```csharp
void Update() {
    if (!IsServer) return;
    _tickTimer += Time.deltaTime;
    if (_tickTimer >= CurrentTickInterval) {
        _tickTimer = 0;
        TradeWorld.Instance.MarketTick(_tickInterval);  // dt — для time-based decay
        MarketServer.Instance.BroadcastSnapshotsToAll();
    }
}
```

`MarketTick(dt)` — теперь **time-based** decay, а не tick-based:
```csharp
// вместо factor *= 0.92 (per tick)
float halfLifeSeconds = 1800f;  // 30 минут
float k = Mathf.Log(2f) / halfLifeSeconds;
factor *= Mathf.Exp(-k * dt);
```
Это автоматически работает с любым multiplier (быстрее тики → меньше dt каждый → та же физика).

### 2.7. Multi-ship selection — новая фича

**`MarketZone` (scene-placed MonoBehaviour, ставится в `WorldScene_X_Z` рядом с городом):**
```csharp
[SerializeField] string locationId;       // "primium", "secundus", ...
[SerializeField] float tradeRadius = 5f;  // игрок должен быть в этой зоне
[SerializeField] float shipDockRadius = 30f;  // корабли в этой зоне считаются "у причала"
[SerializeField] Transform interactionPoint;  // где игрок должен стоять чтобы открыть меню
```

**Серверный companion (`MarketZoneServer`, на том же GameObject, server-only):**
- `OnTriggerEnter(Collider)` для игроков → server добавляет clientId в `_playersInZone`
- `OnTriggerEnter(Collider)` для кораблей → server добавляет NetworkObjectId корабля в `_shipsInZone`
- Каждый frame проверяет «свежесть» (если корабль/игрок вышел — удаляет из списка)
- При `SubscribeMarketRpc` от клиента — сервер шлёт снепшот **включая** список кораблей в зоне

**Клиентский flow:**
1. Игрок входит в `MarketZone.trigger` → `NetworkPlayer` детектит → посылает `SubscribeMarketRpc(locationId)`
2. Сервер отвечает `ReceiveMarketSnapshotRpc` с:
   - `marketState` (текущие цены, сток)
   - `warehouse` (его склад на этой локации)
   - `nearbyShips: List<ShipSummary>` (id, name, cargoSummary)
3. UI показывает панель "Рынок" (можно открыть вручную через E)
4. Когда игрок хочет погрузить/разгрузить — UI сначала показывает `ShipSelector.uxml` (dropdown со списком кораблей)
5. Выбор корабля → `RequestLoadToShipRpc(itemId, qty, selectedShipId)`
6. Сервер валидирует: игрок в зоне? корабль в зоне? груз помещается?
7. Ответ → `ReceiveTradeResultRpc` + обновлённый `ReceiveMarketSnapshotRpc`

### 2.8. UI на UI Toolkit (замена хардкода)

**Новые файлы:**
- `Assets/_Project/Trade/UI/MarketWindow.uxml` — корневой макет панели
- `Assets/_Project/Trade/UI/MarketWindow.uss` — стили
- `Assets/_Project/Trade/UI/MarketItemRow.uxml` — переиспользуемая строка товара
- `Assets/_Project/Trade/UI/WarehouseRow.uxml` — переиспользуемая строка склада
- `Assets/_Project/Trade/UI/ShipSelector.uxml` — выбор корабля
- `Assets/_Project/Trade/UI/ShipSelector.uss` — стили
- `Assets/_Project/Trade/UI/Theme_Trade.uss` — переменные темы (ссылка на основной Theme)

**`MarketClientState` (singleton, MonoBehaviour):**
- `MarketSnapshotDto currentSnapshot` (последний снепшот)
- `event Action<MarketSnapshotDto> OnSnapshotUpdated`
- `event Action<TradeResultDto> OnTradeResult`
- `event Action<MarketTimeInfo> OnTimeInfoUpdated` (multiplier, время до тика)
- `event Action OnDisconnected` (для retry)

**`MarketWindow` (UIDocument + controller):**
- `VisualElement root` — ссылка на root из UXML
- подписывается на `MarketClientState` события
- методы: `Show()`, `Hide()`, `SelectItem(itemId)`, `SelectShip(shipId)`
- НЕ имеет собственного state, только projection

### 2.9. Cargo system — сетевой

**`CargoSystem` остаётся, но:**
- `NetworkObject` добавляется автоматически (RequireComponent)
- Поля `cargo: List<CargoItem>` становятся **серверными** (на клиенте читаются через `NetworkVariable<>` или `ServerRpc` + `ClientRpc`)
- Добавляется `NetworkObjectId` (server-assigned), используется как ключ в репозитории
- Клиент **не мутирует** `cargo.AddCargo` напрямую, только через `MarketServer.RequestLoadToShipRpc`

**Сервер-сайд `CargoService` (server-only, static helper):**
- `static Cargo GetOrCreate(NetworkObject ship)` — авторитетный
- `static bool TryAddItem(NetworkObject ship, itemId, qty, out reason)` — валидация лимитов

### 2.10. Клиентский input (E для взаимодействия)

- `NetworkPlayer.Update()` детектит `E` нажатие + `_nearestMarketZone` (через `InteractableManager`)
- Если есть зона → посылает `SubscribeMarketRpc(locationId)` (идемпотентно)
- `MarketClientState.OnSnapshotUpdated` → `MarketWindow.Show()` (если ещё не показан)
- `Esc` → `MarketWindow.Hide()` (НЕ отписка от зоны — игрок всё ещё в зоне, окно скрыто)

---

## 3. Соответствие старых и новых компонентов

| Старый | Новый | Что меняется |
|--------|-------|--------------|
| `TradeMarketServer.cs` (1140 строк) | `MarketServer.cs` (RPC ~200 строк) + `TradeWorld.cs` (~600 строк) | Расщепление: RPC приёмник отдельно от бизнес-логики |
| `PlayerDataStore.cs` (PlayerPrefs) | `IPlayerDataRepository` + `PlayerPrefsRepository` | Интерфейс, не singleton с статикой |
| `PlayerTradeStorage.cs` (MonoBehaviour на Player) | `Warehouse` POCO + repository | Никаких компонентов на NetworkPlayer; всё на сервере |
| `LocationMarket.cs` (SO со state) | `MarketConfig` (SO, read-only) + `MarketState` (POCO) | Разделение config/state |
| `MarketItem.cs` | `MarketItemConfig` (struct) + `MarketItemState` (POCO) | То же |
| `TradeUI.cs` (1200 строк) | `MarketWindow.uxml/uss` + `MarketClientState.cs` + `MarketWindow.cs` (контроллер ~250 строк) | UI Toolkit, declarative |
| `NetworkPlayer.TradeBuyServerRpc` (proxy) | `MarketServer.RequestBuyRpc` + прямой `TradeWorld.TryBuy` | Без proxy-слоя, server-only singleton |
| `TradeResultClientRpc` через `IsOwner` | `ReceiveTradeResultRpc` через `SendTo.Owner` | Стандартный NGO 2.x паттерн |
| `SendMarketUpdateClientRpc` (CSV Split) | `ReceiveMarketSnapshotRpc(MarketSnapshotDto)` через JSON/byte[] | Без CSV, с DTO |
| `TradeMarketServer.FixedUpdate` для tick | `MarketTimeService.Update` | Выделено в отдельный сервис с multiplier |
| `CargoSystem` (local) | `CargoSystem` (NetworkObject, server-authoritative) + `CargoService` | NetworkObject + сервер |
| — | `MarketZone` (scene-placed) | НОВОЕ |
| — | `MarketClientState` (singleton) | НОВОЕ |
| — | `MarketTimeService` (server-only) | НОВОЕ |
| — | `ShipSelector.uxml` | НОВОЕ |

---

## 4. Этапы реализации (порядок)

1. **Этап 0: Подготовка** — сохранить старые файлы в `Assets/_Project/Trade/_archive/` (опционально), описать в `CHANGELOG.md`
2. **Этап 1: Core types** — `MarketConfig`, `MarketItemConfig`, `MarketState`, `MarketItemState`, `Warehouse`, `Cargo`, `IPlayerDataRepository`, DTOs (`MarketSnapshotDto`, `TradeResultDto`, `ShipSummary`)
3. **Этап 2: Repository** — `PlayerPrefsRepository` (портировать логику, исправить баги), `ServerFileRepository` (заглушка)
4. **Этап 3: TradeWorld** — серверная логика (TryBuy/TrySell/TryLoad/TryUnload/Tick), перенести формулы, NPC traders, events
5. **Этап 4: MarketTimeService** — tick loop с multiplier, опциональная подписка на weather
6. **Этап 5: MarketServer (RPC)** — NetworkBehaviour, RPC API, валидация позиции через `MarketZone`
7. **Этап 6: MarketZone (scene-placed)** — trigger + server-side список игроков/кораблей
8. **Этап 7: Cargo system network** — CargoSystem становится NetworkObject, CargoService
9. **Этап 8: Client state + UI** — `MarketClientState` + UXML/USS + `MarketWindow` controller
10. **Этап 9: Multi-ship selection UI** — `ShipSelector.uxml` + flow
11. **Этап 10: Демо** — добавить 1 `MarketZone` в `WorldScene_0_0`, 2 тестовых корабля, проверить полный flow
12. **Этап 11: Чистка** — удалить старые файлы, обновить `docs/`, пометить CHANGELOG
13. **Этап 12: Документация** — обновить GDD_22 (если формула времени изменилась), написать `docs/dev/TRADE_V2_INTEGRATION.md`

Каждый этап → компиляция + ручная проверка (ты делаешь, я — нет, по AGENTS.md).

---

## 5. Открытые вопросы (нужны твои решения)

### 5.1. Хранение данных

**Q: Какую реализацию `IPlayerDataRepository` использовать по умолчанию?**

- **A) `PlayerPrefsRepository`** — работает сразу, как сейчас, но в dedicated server сломается при перезапуске
- **B) `ServerFileRepository`** — JSON-файлы на сервере, надёжнее, но нужно решить где (Application.persistentDataPath в dedicated, на той же машине)

**Моя рекомендация:** A на этом этапе (Stage 2.5), B как P1 задача с TODO. Так быстрее проверим flow. Согласен?

### 5.2. Time multiplier — источник скорости

**Q: Как тикать рынок?**

- **A) Только наш multiplier** — полностью контроль пользователя, weather controller не влияет
- **B) Weather + наш multiplier** — `effectiveMultiplier = userMultiplier * weatherFactor`, weather автоматически замедляет ночью
- **C) Сначала A, через флаг в инспекторе — потом можно переключить на B**

**Моя рекомендация:** C. По умолчанию A (выкл weather), флаг `useWeatherFactor` в инспекторе. Чтобы не ломать текущий баланс.

### 5.3. Multi-ship UI

**Q: Как показывать выбор корабля?**

- **A) Dropdown в верхней части окна** (один видимый корабль, переключение)
- **B) Sub-panel "Выберите корабль"** перед каждой операцией Load/Unload
- **C) Всегда список кораблей слева, вкладки "Рынок/Склад/Корабль"**

**Моя рекомендация:** A для v2.0. Минимально меняет привычный flow. Если в зоне 1 корабль — dropdown скрыт, действует автоматически.

### 5.4. Time-based decay vs tick-based

**Q: Как моделировать затухание спроса/предложения?**

- **A) Tick-based** (как сейчас): `factor *= 0.92` раз в 5 минут, multiplier ускоряет частоту тиков
- **B) Time-based**: `factor *= exp(-k*dt)` каждый кадр, multiplier ускоряет `k` (или dt perception)

**Моя рекомендация:** B. Лучше отвечает на "ускорить рынок" — ускорение означает "больше движения цен за минуту", а не "то же движение чаще".

### 5.5. UXML или всё-таки код-построение?

**Q: UI Toolkit полностью или частично?**

- **A) Полный UXML** (всё в `.uxml` файлах, тема через USS variables)
- **B) Гибрид** (структура в UXML, списки товаров строятся в коде через `ListView.itemsSource`)
- **C) Полный код** (но с разделением, без хардкода позиций)

**Моя рекомендация:** A для статических частей (заголовок, кнопки, лейаут), `ListView` с `bindItem` для динамических списков. Это стандартный паттерн UI Toolkit.

### 5.6. Совместимость с GDD

GDD_22 в текущей версии не упоминает time multiplier и multi-ship. Предлагаю:

- Дополнить GDD_22 разделом 4.5 "Time-based economy" (multiplier, подписка на weather)
- Дополнить GDD_22 разделом 5.5 "Multi-ship trading" (зона, выбор)
- Я НЕ буду править GDD сам (по AGENTS.md) — ты ревью и коммитишь, я подготовлю patch

---

## 6. План минимизации рисков

| Риск | Митигация |
|------|-----------|
| Регресс: что-то работало в старой системе, я сломал | Старые файлы остаются до Этапа 11. Если что-то критичное — откат через git. |
| PlayerPrefs на dedicated server | По умолчанию A, но в коде стоит TODO + Assert(IsServer) на мутации |
| Двойные RPC в новой системе | Все RPC с `RequireOwnership = true`, `_tradeLocked` на клиенте, на сервере — server-side idempotency counter |
| NetworkVariable капа на 64KB | `MarketSnapshotDto` не должен превышать 32KB даже для 100 товаров × 5 полей × 4 байта = 2KB. ОК. |
| TradeUI в Bootstrap → DontDestroyOnLoad → memory leak | `MarketClientState` singleton, не NetworkObject, живёт в client-side сцене |
| UXML не подгружается в hot reload | `Resources.Load<VisualTreeAsset>` через `AddressableAsset`/Resources или прямая ссылка в инспекторе |
| Old `PlayerTradeStorage` где-то вызывается | Grep → compile → fix call-sites |

---

## 7. Definition of Done (что считать "готово")

- [ ] Компилируется, 0 errors, 0 warnings по новым файлам
- [ ] В `WorldScene_0_0` добавлена 1 `MarketZone` (Primium), 2 `CargoSystem` (тестовых)
- [ ] Host: открыть рынок → купить 1 мезий → выбрать корабль → погрузить → продать на той же станции → кредиты изменились корректно (1 CR цена - 0.8 sell tax = 0.8, **не** 0.8 + 0.8 = 1.6)
- [ ] Client 2: подключиться → открыть рынок → цены совпадают с host → купить 1 мезий → **видит** 1 мезий на своём складе
- [ ] Tick multiplier = 10x → цены заметно двигаются за 30 секунд (несколько тиков)
- [ ] Tick multiplier = 0.1x → цены НЕ скатываются в 0, decay не агрессивнее
- [ ] Multi-ship: в зоне 2 корабля → UI dropdown работает, Load/Unload идёт в выбранный
- [ ] Multi-ship: в зоне 1 корабль → dropdown скрыт, действует автоматически
- [ ] Позиция: игрок вне `MarketZone.trigger` → RPC возвращает ошибку "Не в зоне", клиент не показывает окно
- [ ] CargoSystem — добавить NetworkTransform (как корабль), проверить что груз синхронизируется при посадке в корабль
- [ ] Документация обновлена: GDD_22 патч + `docs/dev/TRADE_V2_INTEGRATION.md`

---

## 8. Чего НЕ делаем (вне scope)

- ❌ Контракты (`ContractSystem.cs`) — оставляем как есть, они работают; если сломаются из-за удаления `PlayerTradeStorage` — фикс отдельным тикетом
- ❌ Чёрный рынок, контрабанда (GDD секция 5.4) — этап 5+
- ❌ P2P торговля между игроками — этап 3 (GDD)
- ❌ Репутация (Faction) влияние на цены — этап 5+ (GDD)
- ❌ `DefaultNetworkPrefabs.asset` — known issue, отдельный тикет
- ❌ `WorldSceneManager` / `ServerSceneManager` — стриминг, отдельная большая сессия
- ❌ `.asmdef` для Trade — отдельная задача с user approval (AGENTS.md запрещает auto-create)
- ❌ Замена PlayerPrefs на SQLite — P1, после стабилизации v2

---

**Готово к ревью.** Подтверди дизайн (и ответь на 6 открытых вопросов выше) — приступаю к Этапу 1.
