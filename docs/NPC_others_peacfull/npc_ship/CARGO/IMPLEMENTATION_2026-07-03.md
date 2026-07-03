# T-CARGO-NPC-01 — Implementation Report

> **Дата:** 2026-07-03
> **Статус:** ✅ ЗАВЕРШЁН
> **Связано:** [T_CARGO_NPC_01_DESIGN_2026-07-03.md](T_CARGO_NPC_01_DESIGN_2026-07-03.md), [MARKET_ID_REFACTOR_DESIGN.md](../../../Markets/MARKET_ID_REFACTOR_DESIGN.md)

---

## 1. Что реализовано

### 1.1 NpcCargoService (server-only singleton)

**Файл:** `Assets/_Project/Scripts/PeacefulShip/Network/NpcCargoService.cs`

Создаётся в `NpcShipServer.OnNetworkSpawn`, выполняет dwell-trade для NPC:

- **Phase 1 (Unload):** cargo → market.stock через `TradeWorld.TryNpcSell()`. Уважает `maxKeepQuantity`.
- **Phase 2 (Load):** market.stock → cargo через `TradeWorld.TryNpcBuy()`. Pre-check по `maxLoadSlots` / `maxLoadWeightKg` из конфига.
- **BuildManifest():** читает cargo из `TradeWorld._cargoCache` и строит `NpcShipCargoManifest`.

### 1.2 TradeWorld.TryNpcBuy / TryNpcSell

**Файл:** `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs`

Server-only API для NPC-торговли:
- `TryNpcBuy(npcClientId, locationId, itemId, qty, shipId, shipClass, useUnlimitedCredits)` — market.stock → cargo, минуя warehouse.
- `TryNpcSell(npcClientId, locationId, itemId, qty, shipId, shipClass, useUnlimitedCredits)` — cargo → market.stock.
- `useUnlimitedCredits=true` — скипает проверку кредитов (GDD: безлимитный кошелёк на время тестов).
- Guard: `IsNpcDockedAtStation()` — defense-in-depth проверка что NPC физически на паде.

### 1.3 Интеграция в NpcShipController.NavTick

**Файл:** `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs`

- Флаг `_cargoTradeDone` — dwell-trade выполняется ОДИН раз за docking.
- Сбрасывается в `SetMode(Docked)`.
- Вызывается в Docked-блоке через ~1с после touchdown.
- `locationId` = `state.CurrentRoute.fromLocationId` (текущая станция).

### 1.4 NpcCargoTradeListConfig

**Файл:** `Assets/_Project/Scripts/PeacefulShip/Core/NpcCargoTradeConfig.cs`

```csharp
public struct NpcCargoTradeConfig {
    public string itemId;
    public int desiredQuantity;
    public bool sellOnArrival;
    public int maxKeepQuantity;
}

public class NpcCargoTradeListConfig {
    public bool useUnlimitedCredits = true;
    public int maxLoadSlots = 8;
    public float maxLoadWeightKg = 200f;
    public bool sellAllOnArrival = true;
    public bool buyConfiguredItemsAfterSell = true;
    public NpcCargoTradeConfig[] buyItems;
}
```

### 1.5 NpcShipSchedule.GetOrInitCargoTrade()

**Файл:** `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipSchedule.cs`

Авто-заполнение `buyItems` из пресетов по `scheduleId`:
- `"SCH-NPC-001"` (Courier) → `resource_mezium_box` ×3 + `resource_antigrav_box` ×2

---

## 2. MARKET-ID-REFACTOR (сопутствующий)

См. полный диздок: [MARKET_ID_REFACTOR_DESIGN.md](../../../Markets/MARKET_ID_REFACTOR_DESIGN.md)

Кратко:
- `MarketZone` — `_marketConfig` (MarketConfig SO) вместо строки `locationId`
- `MarketConfigCollector` — авто-сбор из сцен + нормализация `ToUpperInvariant()`
- Все реестры нормализуют ключи: `TradeWorld`, `MarketZoneRegistry`, `DockingZoneRegistry`
- `MarketServer` авто-собирает MarketConfig из MarketZone в загруженных сценах
- `MarketConfig.OnValidate()` — авто-UPPERCASE `locationId`

---

## 3. Исправленные баги

### 3.1 MarketNotFound (PRIMIUM_TEST_ZONE)

**Причина:** `MarketConfig_Primium_test` не был в `MarketServer.marketConfigs` (BootstrapScene).

**Исправление:** Добавлен в список + реализован авто-сбор из сцен.

### 3.2 Case mismatch (primium vs PRIMIUM)

**Причина:** `MarketConfig_Primium.locationId = "primium"` (lowercase), маршрут NPC: `"PRIMIUM"`.

**Исправление:** `locationId` → `"PRIMIUM"` + нормализация `ToUpperInvariant()` во всех реестрах.

### 3.3 CargoFullVolume (cargo_max_volume)

**Причина:** NPC-корабль HeavyII имел `baseMaxCargoVolume=3` (дефолт Light) в префабе. `ShipCargoRegistry.GetEffectiveLimits()` возвращал `maxVolume=3` → `newVolume=8 > 3` → fail.

**Исправление:** `TryCheckEffectiveCargoLimits` — если effective limits ≤ Light-дефолт, а shipClass тяжелее → игнорируем, берём `ShipClassLimits.Get(shipClass)`.

---

## 4. Файлы

### Новые (5)

| Файл | Назначение |
|------|-----------|
| `Trade/Scripts/Config/MarketConfigCollector.cs` | Нормализация + авто-сбор |
| `Trade/Scripts/Editor/MarketZoneMigrationTool.cs` | Editor-мигратор |
| `docs/Markets/MARKET_ID_REFACTOR_DESIGN.md` | Диздок рефакторинга |
| `docs/NPC_others_peacfull/npc_ship/CARGO/IMPLEMENTATION_2026-07-03.md` | Этот документ |

### Изменённые (9)

| Файл | Изменение |
|------|-----------|
| `Trade/Scripts/Network/MarketZone.cs` | `_marketConfig` поле, derived `LocationId` |
| `Trade/Scripts/Network/MarketZoneRegistry.cs` | Нормализация ключей |
| `Trade/Scripts/Network/MarketServer.cs` | Авто-сбор конфигов из сцен |
| `Trade/Scripts/Core/TradeWorld.cs` | Нормализация + TryNpcBuy/Sell + фикс CargoFullVolume |
| `Scripts/Docking/Network/DockingZoneRegistry.cs` | Нормализация locationId |
| `Scripts/PeacefulShip/Stations/NpcShipController.cs` | RunDwellCargoTrade() + нормализация |
| `Trade/Scripts/Config/MarketConfig.cs` | OnValidate() авто-UPPERCASE |
| `Trade/Data/Markets/MarketConfig_Primium.asset` | `locationId`: `"primium"` → `"PRIMIUM"` |
| `Scenes/BootstrapScene.unity` | +MarketConfig_Primium_test в marketConfigs |

---

## 5. Editor-инструменты

- **Tools → ProjectC → Trade → Migrate MarketZones to MarketConfig refs**
  Читает старые `locationId` из сериализованных данных MarketZone, находит MarketConfig, назначает в `_marketConfig`.
- **Tools → ProjectC → Trade → Add MarketConfig_Primium_test to MarketServer**
  Одноразовый хотфикс для BootstrapScene.

---

## 6. Как теперь добавлять рынок

1. `Assets > Create > ProjectC > Trade > Market Config` → `locationId = "MY_ZONE"`, items
2. В WorldScene: GO с `MarketZone`, перетащить MarketConfig SO → готово
3. BootstrapScene не трогаем — MarketServer сам собирает MarketConfig из сцен

---

## 7. Открытые вопросы

- **UI cargo NPC:** клиентский `MarketClientState` показывает cargo только nearby ships (через MarketZone). NPC-корабли не видны в UI трейда игрока. Нужен отдельный UI для просмотра NPC-cargo.
- **3D визуал:** `ShipCargoVisual` должен подписаться на `TradeWorld.OnCargoChanged` для NPC-кораблей (сейчас подписан на `ShipTelemetryClientState` — только для player-кораблей).
- **Баланс:** `useUnlimitedCredits=true` — временно для тестов. Нужна экономика NPC.
