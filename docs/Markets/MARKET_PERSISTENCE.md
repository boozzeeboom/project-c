# Market State Persistence (v1)

## Overview

После рестарта сервера runtime-состояние рынков (stock, demand/supply factors, активные события) сохраняется через `IPlayerDataRepository` — тот же паттерн, что используется для контрактов.

## Что сохраняется

| Данные | Persist? | Причина |
|---|---|---|
| `availableStock`, `demandFactor`, `supplyFactor`, `eventMultiplier`, `version` per item | ✅ | Игровое состояние. Потеря = эксплойт (сток восстанавливается до initial) |
| `MarketEvent` runtime (`isActive`, `remainingSeconds`, `cooldownRemaining`) | ✅ | Активный ивент не должен обрываться на рестарте |
| `MarketTrader` (NPC) | ❌ | Хардкод в `InitDefaultNPCTraders`, нет runtime-мутаций для сохранения |
| `currentPrice` | ❌ | Пересчитывается из factors через `RecalculatePrice` |
| config-ссылки / basePrice | ❌ | Из ScriptableObject, не runtime |

## Файлы

| Файл | Назначение |
|---|---|
| `Assets/_Project/Trade/Scripts/Dto/MarketSaveData.cs` | `[Serializable]` DTO: `MarketSaveData`, `MarketLocationSaveEntry`, `MarketItemSaveEntry`, `MarketEventSaveEntry` |
| `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` | Интерфейс: `+ SaveMarkets` / `+ TryLoadMarkets` с `RepositoryLoadStatus` |
| `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` | JSON: `ServerData/markets.json` |
| `Assets/_Project/Trade/Scripts/Repository/PlayerPrefsRepository.cs` | PlayerPrefs keys: `PD2_Markets`, `PD2_Markets_bak`, `PD2_Markets_tmp` (best-effort recovery) |
| `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` | `SaveAll()` / `LoadAll()`; save после buy/sell/npc/events/Shutdown |

## DTO Shape

```
MarketSaveData
  markets: List<MarketLocationSaveEntry>
    locationId: string
    items: List<MarketItemSaveEntry>
      itemId, availableStock, demandFactor, supplyFactor, eventMultiplier, version
  events: List<MarketEventSaveEntry>
    eventId, isActive, remainingSeconds, cooldownRemaining, startTimeUnscaled
```

## Load Strategy

1. `TradeWorld.Initialize()` создаёт markets из `MarketConfig` (все items + config refs)
2. `InitDefaultMarketEvents()` создаёт дефолтные события
3. `LoadAll()` получает `RepositoryLoadStatus`:
   - `NoSaveFound` — overlay отсутствует
   - `Loaded` — применяются сохранённые runtime-поля
   - `ValidEmptySave` — состояние считается валидным пустым, без ошибочной regeneration policy
   - `CorruptSave` / `UnsupportedSchema` — overlay отклоняется и последующие `SaveAll()` блокируются
4. Для `Loaded` и `ValidEmptySave` `LoadAll()` делает overlay:
   - **Market items**: матчит по `(locationId, itemId)`. Items, которых нет в config — игнорируются
   - **Events**: матчит по `eventId`. Если saved `isActive == true` — переприменяет эффект на рынки

## Save Triggers

| Trigger | Метод |
|---|---|
| Игрок купил товар | `TryBuy` → `SaveAll()` |
| Игрок продал товар | `TrySell` → `SaveAll()` |
| NPC купил товар | `TryNpcBuy` → `SaveAll()` |
| NPC продал товар | `TryNpcSell` → `SaveAll()` |
| Событие активировано (manual/trigger) | `ActivateEvent` → `SaveAll()` |
| Событие деактивировалось (истекло время) | `MarketTick` → dirty flag → `SaveAll()` |
| Сервер остановлен | `Shutdown` → `SaveAll()` |

## Verification

- Купить товар (сток падает) → рестарт сервера → сток остаётся сниженным
- Demand/supply factors не сбрасываются в 0 после рестарта
- Активный market event переживает рестарт (remaining timer сохраняется)
- Логи: `[TradeWorld] Loaded markets from repository`
- Compile clean
- Empty snapshot и отсутствие snapshot различаются (`ValidEmptySave` / `NoSaveFound`)
