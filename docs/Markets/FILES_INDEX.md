# Markets — Files Index

Полный каталог 53 .cs файлов + UI assets + ScriptableObject assets + scene-placed prefabs.

## `Assets/_Project/Trade/Scripts/` — корневые скрипты (LEGACY + Editor)

| Файл | Строк | Статус | Назначение |
|------|------:|--------|------------|
| `TradeDatabase.cs` | 78 | **АКТИВЕН** (новый путь) | ScriptableObject базы всех TradeItemDefinition; индексирует по itemId и displayName |
| `TradeItemDefinition.cs` | 46 | **АКТИВЕН** (новый путь) | ScriptableObject одного товара (id, name, basePrice, weight, volume, slots, флаги) + enum Faction |
| `TradeMarketServer.cs` | 1002 | **LEGACY** | Старый NetworkBehaviour (CSV-сериализация, локальные vs RPC paths). Не используется новой подсистемой. К удалению после cleanup |
| `TradeUI.cs` | 1143 | **LEGACY** | Старый MonoBehaviour с хардкод-UGUI. Не используется. К удалению |
| `PlayerTradeStorage.cs` | 278 | **LEGACY** | Старый MonoBehaviour на NetworkPlayer (клиент-сайд мутации). Не используется. К удалению |
| `PlayerDataStore.cs` | 131 | **LEGACY** | Старый singleton с PlayerPrefs. Не используется. К удалению |
| `LocationMarket.cs` | 180 | **LEGACY** | Старый ScriptableObject (mutable state). Не используется. К удалению |
| `MarketItem.cs` | 173 | **LEGACY** | Старый Serializable. Не используется. К удалению |
| `MarketEvent.cs` | 255 | **LEGACY** (есть новый в `Core/`) | Старая версия с tick-based duration. Не используется |
| `NPCTrader.cs` | 138 | **LEGACY** (есть новый в `Core/`) | Старая версия. Не используется |
| `CargoSystem.cs` | 253 | **АКТИВЕН** (частично) | NetworkBehaviour/MonoBehaviour на корабле, держит `shipClass` + cargo list. `MarketServer.ResolveShipClass()` читает `cargoComp.shipClass` |
| `AutoTradeZone.cs` | 170 | **LEGACY** | Старая логика. Не используется |
| `TradeTrigger.cs` | 93 | **LEGACY** | Старый триггер. Не используется (заменён на `Network/MarketZone.cs`) |
| `ContractSystem.cs` | 720 | **АКТИВЕН** (отдельно) | Контракты — отдельная подсистема, ссылается на старый `TradeMarketServer`. Будет мигрирована отдельно |
| `ContractBoardUI.cs` | 462 | **АКТИВЕН** (отдельно) | UI контрактов |
| `ContractData.cs` | 223 | **АКТИВЕН** (отдельно) | POCO контракта |
| `ContractTrigger.cs` | 109 | **АКТИВЕН** (отдельно) | Триггер контракта |
| `PlayerCreditsManager.cs` | 50 | **LEGACY** | Старый manager кредитов. Не используется |
| `PlayerDebt.cs` | 175 | **LEGACY** | Старый долг. Не используется |
| `TradeSetup.cs` | 44 | **LEGACY** | Старый setup-helper. Не используется |
| `TradeSceneSetup.cs` | 86 | **LEGACY** | Старый editor setup. Не используется |
| `TradeDebugTest.cs` | 269 | **DEBUG/LEGACY** | Старый test scene. Не используется |
| `TradeDebugTools.cs` | 318 | **DEBUG/LEGACY** | Старый debug UI. Не используется |

## `Assets/_Project/Trade/Scripts/Config/` — конфиги (SO, READ-ONLY)

| Файл | Назначение |
|------|------------|
| `MarketConfig.cs` | ScriptableObject рынка (locationId, displayName, items: List<MarketItemConfig>) |
| `MarketItemConfig.cs` | [Serializable] struct: itemId, basePrice, initialStock, regenPerTick, allowBuy/allowSell, factionRestriction, definition (ссылка на TradeItemDefinition) |

## `Assets/_Project/Trade/Scripts/Core/` — server-only POCO

| Файл | Назначение |
|------|------------|
| `MarketState.cs` | Runtime состояние одного рынка (locationId, items: Dict<itemId, MarketItemState>); `ComputeVersion()` для отслеживания изменений |
| `MarketItemState.cs` | Runtime состояние одной позиции (config, currentPrice, availableStock, demandFactor, supplyFactor, eventMultiplier, version); `RecalculatePrice()` |
| `Warehouse.cs` | Склад игрока на локации (ownerClientId, locationId, items); DEFAULT_MAX_WEIGHT/VOLUME/ITEM_TYPES константы; `TryAdd/TryRemove/LoadFrom/SaveToList` |
| `CargoData.cs` | Груз корабля (shipNetworkObjectId, shipClass, items); `ComputeTotalWeight/Volume/Slots` |
| `TradeWorld.cs` | Главный серверный singleton (markets, npcTraders, events, Repository, Resolver); `TryBuy/Sell/Load/Unload`, `MarketTick(dt)`, `GetOrLoadWarehouse/Cargo` |
| `TradeResult.cs` | readonly struct: code, message, newCredits, newMarketStock, updatedWarehouse, updatedCargo; `IsSuccess`; static `Ok`/`Fail` |
| `MarketEvent.cs` | Глобальное событие рынка (time-based, duration/cooldown в секундах); TriggerType enum (Manual/DemandThreshold/Random) |
| `NPCTrader.cs` | NPC-трейдер (fromLocationId → toLocationId, itemId, minVolumePerTick..maxVolumePerTick, TradeCondition); перемещает товары между рынками каждый тик |
| `TradeItemDefinitionResolver.cs` | interface: TryGet/GetWeight/GetVolume/GetSlots/GetDisplayName |
| `DatabaseResolver.cs` | Реализация `TradeItemDefinitionResolver` поверх `TradeDatabase` |

## `Assets/_Project/Trade/Scripts/Network/` — server-side MonoBehaviour

| Файл | Назначение |
|------|------------|
| `MarketServer.cs` | NetworkBehaviour, RPC hub (Buy/Sell/Load/Unload/Subscribe/SetTimeMultiplier); валидация позиции; DTO builders; `BroadcastSnapshotsToAll`; rate limit (30 ops/min) |
| `MarketZone.cs` | scene-placed MonoBehaviour (SphereCollider, tradeRadius, shipDockRadius); `_playersInZone` (server), `LocalPlayerZone` (client), `_shipsInZone`; `BuildNearbyShipsDtos()` |
| `MarketZoneRegistry.cs` | static Dictionary<locationId, MarketZone> + static `LocalPlayerZone` |
| `MarketTimeService.cs` | server-only MonoBehaviour, Update() → tick timer → `TradeWorld.MarketTick(dt)` + `onMarketTick` UnityEvent; `MarketTimeMultiplier` (0.1x..100x); `useWeatherFactor` (опц. подписка на ServerWeatherController) |

## `Assets/_Project/Trade/Scripts/Client/` — client-side projection

| Файл | Назначение |
|------|------------|
| `MarketClientState.cs` | singleton MonoBehaviour, держит `CurrentSnapshot` (MarketSnapshotDto?) + `LastResult` (TradeResultDto?); `OnSnapshotUpdated`/`OnTradeResult` events; convenience API `RequestBuy/Sell/Load/Unload/Subscribe/SetTimeMultiplier`; static `LocalizeResultCode` |
| `MarketInteractor.cs` | static helper; `TryOpenMarket()` (E-handler) — `MarketZoneRegistry.LocalPlayerZone` + fallback `FindNearestZone`; `AutoSubscribeIfInZone()` |
| `MarketWindow.cs` | UI Toolkit контроллер; UIDocument + UXML/USS; ListView для item/warehouse/cargo; dropdown `ship-selector`; методы Show/Hide/Toggle; idempotent EnsureBuilt(); pickingMode Ignore по умолчанию, Position когда открыт |

## `Assets/_Project/Trade/Scripts/Dto/` — INetworkSerializable

| Файл | Назначение |
|------|------------|
| `MarketSnapshotDto.cs` | RPC payload: locationId, displayName, items[], warehouse[], credits, warehouseMaxWeight/Volume/Types, nearbyShips[], marketTimeMultiplier, secondsUntilNextTick, marketVersion |
| `TradeResultDto.cs` | RPC payload: code, op, locationId, itemId, quantity, newCredits, newStock, shipNetworkObjectId, updatedWarehouseSnapshot, updatedCargoSnapshot |
| `ShipSummaryDto.cs` | shipNetworkObjectId, displayName, shipClassName, currentWeight/maxWeight, currentVolume/maxVolume, currentSlots/maxSlots, uniqueItemCount |
| `TradeResultCode.cs` | enum: Ok, InvalidArgs, InternalError, NotInZone, RateLimited, MarketNotFound, ItemNotInMarket, InsufficientStock, ItemBuyDisabled, ItemSellDisabled, PriceInvalid, FactionRestricted, ItemNotInWarehouse, WarehouseFullWeight/Volume/Types, ShipNotFound, ShipNotInZone, ItemNotInCargo, CargoFullWeight/Volume/Slots, InsufficientCredits |
| `ItemPriceDto.cs` | itemId, displayName, currentPrice, availableStock, version |
| `WarehouseEntryDto.cs` | itemId, displayName, quantity |

## `Assets/_Project/Trade/Scripts/Service/`

| Файл | Назначение |
|------|------------|
| `PriceFormula.cs` | static helpers: `CalculatePrice`, `ApplyBuy` (demand ↑), `ApplySell` (supply ↑), `DecayFactor` (half-life в секундах), `RegenerateStock`; константы DEMAND_MIN/MAX, SUPPLY_MIN/MAX, DEMAND_PER_UNIT_BOUGHT, SUPPLY_PER_UNIT_SOLD |

## `Assets/_Project/Trade/Scripts/Repository/` — персистентность

| Файл | Назначение |
|------|------------|
| `IPlayerDataRepository.cs` | interface: GetCredits/TryModifyCredits/SetCredits, TryGetWarehouse/SetWarehouse, TryGetCargo/SetCargo |
| `PlayerPrefsRepository.cs` | Реализация через PlayerPrefs (host, single-process); ключи: `PD_Credits_{clientId}`, `PD_Warehouse_{clientId}_{locationId.ToLower}`, `PD_Cargo_{shipId}` |
| `ServerFileRepository.cs` | P1 stub — JSON-файлы для dedicated server (не реализовано, см. TODO) |

## `Assets/_Project/Trade/Scripts/Editor/`

| Файл | Назначение |
|------|------------|
| `MarketAssetGenerator.cs` | Editor утилита для генерации MarketConfig ассетов |
| `MarketItemIDInitializer.cs` | Editor утилита для инициализации itemId |
| `TradeAssetGenerator.cs` | Editor утилита для генерации TradeItemDefinition ассетов |
| `TradeSceneSetupTool.cs` | Editor утилита для настройки сцены (старый) |

## UI Assets (UI Toolkit)

| Файл | Назначение |
|------|------------|
| `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml` | Структура окна: tabs (market/warehouse), ListView (items/warehouse/cargo), кнопки (buy/sell/load/unload/close), labels (location/credits/warehouse-info/time-info), qty-field, message-label, ship-selector (DropdownField) |
| `Assets/_Project/Trade/Resources/UI/MarketWindow.uss` | Стили: .market-window (absolute, top:5%, left:50%, translate -50% 0, w=640, max-w=90%, max-h=90%, border-radius=8, padding=12, color=0.863, bg=0.078/0.098/0.137 a=0.95); .market-row / .warehouse-row / .cargo-row (flex-direction=row, padding, hover/selected); .list-section (flex-grow=1, min-height=0) |
| `MarketPanelSettings.asset` (в Resources/UI/ или _Project/UI/) | PanelSettings для UIDocument; создаётся вручную через `Create → UI Toolkit → Panel Settings Asset` |

## ScriptableObject Assets (data)

| Путь | Назначение |
|------|------------|
| `Assets/_Project/Trade/Data/TradeItemDatabase.asset` | База всех TradeItemDefinition (мезий, антигравий) |
| `Assets/_Project/Trade/Data/Items/TradeItem_Mesium_v01.asset` | Мезий (itemId=`mesium_canister_v01`, basePrice=10, weight=10, volume=0.5, slots=1) |
| `Assets/_Project/Trade/Data/Items/TradeItem_Antigrav_v01.asset` | Антигравий (itemId=`antigrav_ingot_v01`, basePrice=50, weight=5, volume=0.2, slots=1) |
| `Assets/_Project/Trade/Data/Items/TradeItem_Latex_v01.asset` | Латекс (itemId=`latex_roll_v01`, basePrice=5, weight=8, volume=1.0, slots=1) |
| `Assets/_Project/Trade/Data/Markets/MarketConfig_Primium.asset` | locationId=`primium`, displayName="Примум", items: мезий (80), антигравий (40) |
| `Assets/_Project/Trade/Data/Markets/MarketConfig_Secundus.asset` | locationId=`secundus`, displayName="Секунд" |
| `Assets/_Project/Trade/Data/Markets/MarketConfig_Tertius.asset` | locationId=`tertius`, displayName="Тертиус" |
| `Assets/_Project/Trade/Data/Markets/MarketConfig_Quartus.asset` | locationId=`quartus`, displayName="Квартус" |
| `Assets/_Project/Trade/Data/Markets/Market_Primium_v01.asset` (×4 LEGACY) | Старые `LocationMarket` SO со state. **Не используются** новой подсистемой. К удалению |

## Scene-placed

| Сцена | GameObject | Назначение |
|-------|-----------|------------|
| `Assets/_Project/Scenes/BootstrapScene.unity` | `[MarketServer]` | NetworkObject + MarketServer + MarketTimeService; tradeDatabase + 4 MarketConfig в инспекторе |
| `Assets/_Project/Scenes/BootstrapScene.unity` | `[MarketClientState]` | MonoBehaviour singleton, DontDestroyOnLoad; создаётся в Awake из `NetworkManagerController` (см. FIX 3) |
| `Assets/_Project/Scenes/BootstrapScene.unity` | `[MarketWindow]` | UIDocument + MarketWindow контроллер |
| `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | `MarketZone_Primium` | SphereCollider, locationId=`primium`, tradeRadius=30, shipDockRadius=30 (см. INTEGRATION) |
| `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | `MarketZone_Sellshittest` | SphereCollider, locationId=`TEST_1`, debug-зона (видимо тест) |

## Связи с не-Trade кодом

| Файл вне Trade | Что импортирует из Trade |
|----------------|--------------------------|
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | `ProjectC.Trade.Client.MarketInteractor.TryOpenMarket()` (line 307); `NetworkPlayer.ReceiveMarketSnapshotTargetRpc` / `ReceiveTradeResultTargetRpc` (target для `MarketServer` RPC); `NetworkPlayer.RequestSetMarketTimeMultiplier` (line 851-857) |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | В `Awake()` создаёт `[MarketClientState]` GameObject как root (FIX 3) |
| `Assets/_Project/Scripts/Player/ShipController.cs` (вне Trade) | Имеет `CargoSystem` компонент, который `MarketServer.ResolveShipClass` читает через SpawnManager |

## Что лежит рядом (НЕ Trade, но пересекается)

- `ProjectC.Items` (Inventory, InventoryUI) — пикапы, сундуки; не пересекается с рынком
- `ProjectC.UI` (UIFactory, UIManager) — общее управление UI; не пересекается с MarketWindow (UIDocument свой)
- `ProjectC.World.Streaming` (PlayerChunkTracker, WorldSceneManager) — стриминг сцен; не пересекается
- `ProjectC.World.Chest` (NetworkChestContainer, ChestContainer) — сундуки; **приоритет над рынком** при нажатии E (см. `NetworkPlayer.cs:300-311`)
