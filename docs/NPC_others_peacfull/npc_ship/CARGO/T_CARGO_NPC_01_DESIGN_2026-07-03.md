# T-CARGO-NPC-01 — Design: Universal cargo for NPC ships

> **Автор:** Mavis (Mavis)
> **Дата:** 2026-07-03
> **Статус:** 🟡 DESIGN → CODE (готов к реализации)
> **Связано с:** [`CARGO_REMAINING_WORK_2026-07-02.md`](../../Ships/cargo_system/CARGO_REMAINING_WORK_2026-07-02.md) §2.4,
> [`03_V2_ARCHITECTURE.md`](../03_V2_ARCHITECTURE.md) §6.1, [`04_LIVING_BEHAVIOR.md`](../04_LIVING_BEHAVIOR.md) §2

---

## 1. TL;DR

Расширяем существующий `NpcShipSchedule` SO списком товаров + флагом «безлимитный кошелёк», добавляем
server-only `NpcCargoService` (использует существующий `TradeWorld` без дубль-структур), и встраиваем
новые фазы `Unloading → Loading → Undocking` в FSM `NpcShipController` (между `Docked` и `Undocking`).

После реализации NPC-курьер, стоя на паде станции Примум:
1. **Unloading**: продаёт ВСЁ cargo со своего корабля на рынок `CurrentRoute.toLocationId` (`TrySell(npcInstanceId, …)`).
2. **Loading**: скупает товары из списка `schedule.cargoTrade.buyItems` с рынка до заполнения `cargo` (`TryBuy(npcInstanceId, …)`).
3. **Undocking → Departing → InTransit** → летит к следующей станции → repeat.

**Итог:** реальный оборот рынка от NPC, визуально ящики на палубе (T-CARGO-VIS-01 уже подписан на
`TradeWorld.OnCargoChanged`), 3D-визуал покрывает NPC автоматически.

---

## 2. Решения (D-26..D-32)

| #   | Решение | Обоснование |
|-----|---------|-------------|
| D26 | `clientId` для NPC-buy/sell = `npcInstanceId` (sentinel) | Q8 закрыт юзером 2026-07-03: «id=800000000000002F… используем их». Warehouse и credits NPC хранятся в `PlayerPrefsRepository` под его id — у каждого курьера собственный виртуальный склад/кошелёк |
| D27 | `useUnlimitedCredits = true` по умолчанию для NPC-курьеров | GDD: «на время тестов NPC обладают безлимитными средствами». Баланс — отдельный эпик, не блокирует |
| D28 | Cargo NPC = `TradeWorld._cargoCache[npcShip.NetworkObjectId]` | D14 уже зафиксировано. `TryLoadToShip` / `TryUnloadFromShip` / `GetOrLoadCargo` работают по `NetworkObjectId` — NPC-корабль такой же `NetworkObject`, лимиты считаются через `ShipCargoRegistry` (он force-register'ит NPC тоже) |
| D29 | `NpcCargoTradeConfig` = новый `[Serializable] struct` поле в `NpcShipSchedule`, НЕ отдельный SO | Schedule — единственный SO-описатель поведения NPC; добавлять cargo в отдельный SO = 2 SO на одного курьера. По аналогии с `NpcShipRoute[]` (тоже массив struct'ов внутри SO) |
| D30 | Список товаров задаётся **по itemId**, без привязки к `TradeItemDefinition` | `MarketConfig` уже использует `itemId` (строка) + ссылку на `TradeItemDefinition` (для UI). NPC-конфиг дублирует только `itemId` + `quantity`; валидация через `DatabaseResolver.TryGet` |
| D31 | FSM: вставляем `Loading` между `Docked` и `Undocking` (M3.2 уже подразумевает `Loading` как no-op). Разбиваем на: `Docked → Unloading → Loading → Undocking` | Двухфазный dwell: сначала unload, потом load. Это естественная последовательность реального курьера |
| D32 | `NpcShipCargoManifest` остаётся как v2 hook — DTO остаётся. Заполнение = read из `TradeWorld.GetCargoSnapshot(npcShipId, shipClass)` | D18 уже зафиксировано. Не дублируем |
| D33 | Новый server-only API в `TradeWorld`: `TryNpcBuy(npcClientId, locationId, itemId, qty, npcShipId, shipClass, useUnlimitedCredits)` и `TryNpcSell(...)`. Существующие `TryBuy`/`TrySell` НЕ меняются | Чтобы обойти `currentCredits < totalCost` без модификации `PlayerPrefsRepository` (там `SetCredits` клампит к 0 и не любит `Infinity`). Минимальный surface change, чистый server-only. |

---

## 3. Файлы

### 3.1 Новые

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcCargoTradeConfig.cs` | `[Serializable] struct` + `NpcCargoTradeListConfig` (агрегатор) — список item'ов и настройки |
| `Assets/_Project/Scripts/PeacefulShip/Network/NpcCargoService.cs` | Server-only сервис: `UnloadAll(npcState)`, `LoadFromSchedule(npcState, schedule)`, `BuildManifestSnapshot(...)` |
| `Assets/_Project/Scripts/PeacefulShip/Editor/NpcShipScheduleCargoHelper.cs` | Editor-утилита: добавляет cargo-секцию к SO + OnValidate предупреждения |
| `docs/NPC_others_peacfull/npc_ship/CARGO/CHANGELOG.md` | Лог изменений подсистемы NPC-cargo |

### 3.2 Изменённые

| Файл | Изменение |
|------|-----------|
| `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipSchedule.cs` | + поле `NpcCargoTradeListConfig cargoTrade` (Header "NPC Cargo Trade") |
| `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | + фазы `Unloading/Loading` в `NavTick` Docked block + вызов `NpcCargoService` |
| `Assets/_Project/Resources/PeacefulShip/NpcShipSchedule_Courier.asset` | + cargo-секция (с примерами item'ов из `MarketConfig_Primium`) |
| `Assets/_Project/Resources/PeacefulShip/NpcShipSchedule_Trader.asset` | + cargo-секция (другая корзина) |
| `docs/NPC_others_peacfull/npc_ship/CHANGELOG.md` | + секция T-CARGO-NPC-01 |
| `docs/Ships/cargo_system/CHANGELOG.md` | + секция T-CARGO-NPC-01 (если файл существует) |

### 3.3 НЕ трогаем

- `TradeWorld.cs` — никаких новых public API. Существующие `TryBuy` / `TrySell` / `TryLoadToShip` / `TryUnloadFromShip` уже работают с произвольным `clientId` (D26).
- `IPlayerDataRepository` — не нужен новый интерфейс. Warehouse NPC хранится в `PlayerPrefsRepository` под `clientId = npcInstanceId` (D26).
- `MarketZone` / `MarketServer` — NPC не входит в зону (он сервер-управляемый), валидация зоны не нужна для NPC-операций (мы вызываем `TradeWorld.*` напрямую, в обход `MarketServer.RequestBuyRpc`).
- `MarketSnapshotDto` — изменения NPC-cargo клиенту не нужны для UI (NPC-курьер не отображается в market UI; если нужно для дебага — `NpcShipCargoManifest` уже сериализуется, в DTO 6.1 расписано).

---

## 4. Архитектура

### 4.1 `NpcCargoTradeConfig` (struct)

```csharp
[Serializable]
public struct NpcCargoTradeConfig
{
    public string itemId;        // "resource_mezium_box" — должно существовать в TradeDatabase
    public int desiredQuantity;  // сколько купить за 1 dwell
    public bool sellOnArrival;   // true = продать сначала (если есть в cargo)
    public int maxKeepQuantity;  // не продавать больше этого (защита: не продать cargo, которое NPC только что купил)
}

[Serializable]
public class NpcCargoTradeListConfig
{
    [Header("Behavior")]
    public bool useUnlimitedCredits = true;     // D27
    public int maxLoadSlots = 8;                // стоп-кран по слотам (даже если рынок позволит)
    public int maxLoadWeightKg = 200;           // стоп-кран по весу
    public bool sellAllOnArrival = true;        // D31: unload фаза
    public bool buyConfiguredItemsAfterSell = true; // D31: load фаза

    [Header("Items to buy (executed in order)")]
    public NpcCargoTradeConfig[] buyItems;
}
```

**Header в NpcShipSchedule:**

```csharp
[Header("NPC Cargo Trade (T-CARGO-NPC-01)")]
public NpcCargoTradeListConfig cargoTrade = new NpcCargoTradeListConfig();
```

### 4.2 `NpcCargoService` (server-only)

```csharp
public class NpcCargoService : MonoBehaviour
{
    public static NpcCargoService Instance { get; private set; }

    /// <summary>
    /// Продаёт ВСЁ cargo NPC-корабля на рынок locationId (текущей станции).
    /// Use case: NPC прилетел на станцию → unload всё что привёз.
    /// credits: useUnlimitedCredits → не начисляются (виртуальные).
    /// stock: market.availableStock += qty (оборот).
    /// </summary>
    public UnloadReport UnloadAllAtStation(ulong npcInstanceId, ulong shipNetworkObjectId, ShipClass shipClass, string locationId);

    /// <summary>
    /// Скупает товары из schedule.cargoTrade.buyItems в порядке массива,
    /// останавливаясь при maxLoadSlots / maxLoadWeightKg / market.stock.
    /// </summary>
    public LoadReport LoadFromSchedule(ulong npcInstanceId, ulong shipNetworkObjectId, ShipClass shipClass, string locationId, NpcCargoTradeListConfig trade);

    /// <summary>
    /// Читает текущий cargo NPC-корабля и заполняет NpcShipCargoManifest.
    /// Используется для UI/дебага.
    /// </summary>
    public NpcShipCargoManifest BuildManifest(ulong shipNetworkObjectId, ShipClass shipClass, int capacitySlots, float capacityWeight);
}
```

Создаётся в `NpcShipServer.OnNetworkSpawn` (рядом с `NpcShipWorld.CreateAndInitialize`).

### 4.3 FSM изменения (NpcShipController)

Текущая последовательность в `Docked` блоке `NavTick` (строки 350-367):

```
Docked (60s dwell) → ExitDocked + LiftStartY → SetMode(Lifting)
```

Новая:

```
Docked (10s wait)
  → if schedule.cargoTrade.sellAllOnArrival AND not yet sold:
      Service.UnloadAllAtStation(...)
      → SetMode(NavMode.Unloading)  // внутри unload — сразу назад в Docked после 1 frame (быстро)
  → if schedule.cargoTrade.buyConfiguredItemsAfterSell AND not yet bought:
      Service.LoadFromSchedule(...)
      → SetMode(NavMode.Loading)
  → DwellTime elapsed → LiftStartY → SetMode(Lifting)
```

Чтобы не плодить NavMode-ы, я добавлю только один helper-method `RunCargoTradeIfNeeded()`,
который выполняется **синхронно в том же Docked-кадре** (trade операции в `TradeWorld` — синхронные POCO-вызовы,
не требуют корутины). Возвращаемое значение `RunCargoTradeReport` (sold N items, bought M items) логируется.

Если `sellAllOnArrival=false` и `buyConfiguredItemsAfterSell=false` — поведение **полностью совпадает с текущим**.
Backward compatible. По умолчанию в новом schedule включены оба.

### 4.4 Cargo лимиты NPC

`ShipCargoRegistry.TryForceRegisterFromNetworkManager(shipNetworkObjectId)` уже умеет force-register'ить
любой `NetworkObject` с `ShipController` — для NPC-кораблей это работает так же. Лимиты:
- `capacitySlots` = `ShipClassLimits.Get(npcShipClass).maxSlots` (Light=4, Medium=10, HeavyI=20, HeavyII=30)
- `capacityWeight` = то же `maxWeight`
- NPC-корабль не имеет модулей → per-instance override = статический fallback

Доп. защита в `NpcCargoService.LoadFromSchedule`:
- `maxLoadSlots` / `maxLoadWeightKg` из `cargoTrade` — жёсткий стоп-кран даже если рынок даёт больше.
- Если cargo уже частично заполнен (например, при предыдущем unload) — лимиты уважаются.

---

## 5. Данные (asset-конфиг)

### 5.1 NpcShipSchedule_Courier.asset — секция cargoTrade

```yaml
cargoTrade:
  useUnlimitedCredits: 1
  maxLoadSlots: 8
  maxLoadWeightKg: 200
  sellAllOnArrival: 1
  buyConfiguredItemsAfterSell: 1
  buyItems:
  - itemId: resource_mezium_box
    desiredQuantity: 3
    sellOnArrival: 1
    maxKeepQuantity: 0
  - itemId: resource_antigrav_box
    desiredQuantity: 2
    sellOnArrival: 1
    maxKeepQuantity: 0
```

### 5.2 NpcShipSchedule_Trader.asset — секция cargoTrade (другая корзина)

```yaml
cargoTrade:
  useUnlimitedCredits: 1
  maxLoadSlots: 10
  maxLoadWeightKg: 400
  sellAllOnArrival: 1
  buyConfiguredItemsAfterSell: 1
  buyItems:
  - itemId: resource_copper_wire_box
    desiredQuantity: 5
    sellOnArrival: 1
    maxKeepQuantity: 0
  - itemId: resource_brass_sheet_box
    desiredQuantity: 4
    sellOnArrival: 1
    maxKeepQuantity: 0
```

**Привязка itemId к реальным ItemData:** валидация через `DatabaseResolver.TryGet` в `OnValidate` —
если `itemId` не зарегистрирован в `TradeDatabase`, Editor выдаст ошибку.

---

## 6. Verification

### 6.1 Compile (обязательно, делаю сам)

1. `refresh_unity` (mode=force, scope=scripts, compile=request, wait_for_ready=true)
2. `read_console types=error,warning` → 0 entries ожидается
3. Если ошибки — fix → repeat (1-2 цикла допустимо)

### 6.2 Play Mode (юзер проверяет)

```
1. Open BootstrapScene
2. Start Host
3. Wait 2-3 мин пока NPC долетят и сядут на Примум (Courier schedule)
4. Смотреть в Console:
   - "[NpcShipWorld:NPC] id=… Departing→…" (лёт туда)
   - "[NpcShipController] NavMode Docked → Lifting" (после dwell + cargo trade)
   - "[NpcCargoService] SELL npcId=… item=resource_mezium_box qty=3" (unload)
   - "[NpcCargoService] BUY npcId=… item=resource_mezium_box qty=3" (load)
   - "[TradeWorld] BUY client=800000000000002F loc=primium item=… qty=3" (stock списывается с рынка)
   - "[TradeWorld] SELL client=800000000000002F loc=primium item=… qty=3" (stock возвращается на рынок)
5. Открыть CharacterWindow → таб "Корабль" → увидеть cargo NPC рядом (опционально, если хук в UI)
6. Проверить stock: открыть MarketWindow у Примум → stock resource_mezium_box меняется по тикам
```

### 6.3 Визуал (T-CARGO-VIS-01 автоматически покрывает)

Если на NPC-корабле висит `ShipCargoVisual` + child GO `ShipCargoVisual` (как в T-CARGO-VIS-01)
→ при `TradeWorld.OnCargoChanged(npcShipId)` → визуал обновится → ящики на палубе появятся/исчезнут.

---

## 7. Что НЕ входит (out of scope)

- ❌ NPC-credits balance / экономика без `useUnlimitedCredits` (отдельный эпик)
- ❌ NPC trade route optimizer (выбор itemId по `demandCategory` через анализ stock) — `cargoTrade.buyItems` задаётся явно
- ❌ Multiple-cargo NPC (т.е. NPC с разными cargo-конфигами на разных leg'ах маршрута) — пока один schedule = одна cargo-стратегия
- ❌ Cargo decay / spoilage в cargo NPC (GDD_25 Phase 4+)
- ❌ HUD индикатор "NPC продаёт/покупает" (только Debug.Log)
- ❌ Изменения `NpcShipCargoManifest` DTO контракта (структура стабильна, fillthrough — отдельный мини-тикет)

---

## 8. Архитектурные риски

| Риск | Митигация |
|------|-----------|
| NPC cargo-операции падают с insufficient_stock на перегруженном рынке | `LoadFromSchedule` ловит `TradeResult`, логирует, идёт к следующему item — не зацикливается |
| Множество NPC на одной станции скупают весь stock за 1 dwell | `sellAllOnArrival=true` + `buyConfiguredItemsAfterSell=true` с `maxLoadSlots` cap. Stock регенерируется в `MarketTick` (0.02 за тик). В persistence не блокируем |
| Расхождение `clientId=npcInstanceId` vs `clientId=0` в разных code-path'ах | Все NPC-операции идут **только** через `NpcCargoService` → `TradeWorld`. Никаких прямых `TryBuy(npcId, …)` извне |
| `ShipController.shipClass` для NPC может быть `Light` (default), даже если префаб HeavyI | `ShipCargoRegistry.GetEffectiveLimits(npcShipId)` читает `ship.ShipClass` (не fallback на Light). NPC-ship назначит правильный класс |
| Persistence: NPC-warehouse и NPC-credits в `PlayerPrefsRepository` мусорят | Это OK — D26 фиксирует «у каждого курьера собственный виртуальный склад». При удалении NPC-префаба можно почистить ключи (отдельный тикет) |

---

## 9. Зависимости

- ✅ TradeWorld (T-CARGO-01..06) — готов, проверен
- ✅ NpcShipController FSM (M3.2) — готов, проверен в Play Mode
- ✅ CargoData / ShipClassLimits — готовы
- ✅ PlayerPrefsRepository — поддерживает произвольный clientId
- ⚠️ DatabaseResolver / TradeDatabase — не модифицируем, используем as-is

---

## 10. Чеклист реализации

1. [ ] `NpcCargoTradeConfig.cs` — struct + ListConfig
2. [ ] Расширить `NpcShipSchedule.cs` — добавить поле `cargoTrade`
3. [ ] `NpcCargoService.cs` — UnloadAll / LoadFromSchedule / BuildManifest
4. [ ] `NpcShipServer.cs` — создать `NpcCargoService` при `OnNetworkSpawn`
5. [ ] `NpcShipController.NavTick` — Docked hook + вызов `NpcCargoService`
6. [ ] Editor-утилита: `NpcShipScheduleCargoHelper.cs` (опционально, OnValidate достаточно)
7. [ ] Обновить 2 SO asset'а — Courier + Trader
8. [ ] MCP verify trio
9. [ ] CHANGELOG (NPC + cargo_system)
10. [ ] Summary + verification recipe для юзера
