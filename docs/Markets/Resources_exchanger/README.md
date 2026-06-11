# Resources Exchanger — Индекс

> **Статус:** ✅ DONE (T-E01..T-E05)
> **Дата реализации:** 2026-06-11
> **Цель:** объединить две системы предметов (pickable inventory + warehouse boxes)
> через обменник-упаковщик, не ломая существующие системы.

## Документы

| Файл | Содержание |
|------|------------|
| [`01_ANALYSIS.md`](01_ANALYSIS.md) | Полный анализ, сравнение подходов, рекомендуемая архитектура (Hybrid D), план тикетов T-E01..T-E05 |
| [`02_IMPLEMENTATION.md`](02_IMPLEMENTATION.md) | Актуальная реализация — что изменилось в коде, ключевые решения, баги и фиксы |
| [`03_FIXES_HISTORY.md`](03_FIXES_HISTORY.md) | Хронология правок и обнаруженных багов |

## Ключевые решения (после рефакторинга)

1. **Zero-touch** — ни одна существующая система не меняется
2. **4-я вкладка MarketWindow** — код UI прямо в MarketWindow.cs (не отдельный ExchangerTab.cs)
3. **ResourceExchangeResolver** — переиспользуемый слой маппинга
4. **Config-driven** — новые пары предметов = запись в SO
5. **Склад обновляется через MarketServer.PushPlayerSnapshot** — не только InventoryServer
6. **ExchangeServer не отдельный NetworkBehaviour** — использован существующий паттерн MarketServer (scene-placed, BootstrapScene)
7. **ScenePlacedObjectSpawner подписан на NetworkManager.OnServerStarted** — иначе scene-placed NetworkObject в BootstrapScene не спавнятся
8. **InventoryWorld.MAX_SLOTS** — вынесен в конфигурируемое поле через инспектор InventoryServer (по умолчанию 1000)
9. **Счётчики Pack/Unpack — единицы, не паки** — client шлёт `countToRemove = rate.inventoryQty` (100 шт), сервер валидирует кратность

## Актуальные файлы

| Файл | Тикет | Назначение |
|------|-------|------------|
| `Assets/_Project/Trade/Exchange/Config/ExchangeRateConfig.cs` | T-E01 | ScriptableObject — список курсов |
| `Assets/_Project/Trade/Exchange/Config/ExchangeRateEntry.cs` | T-E01 | struct: warehouseItemId, inventoryItemName, inventoryQty, warehouseQty |
| `Assets/_Project/Trade/Exchange/Core/ExchangeResult.cs` | T-E02 | struct результата операции |
| `Assets/_Project/Trade/Exchange/Core/ExchangeWorld.cs` | T-E02 | Серверная POCO-логика Pack/Unpack с rollback |
| `Assets/_Project/Trade/Exchange/Core/ResourceExchangeResolver.cs` | T-E01 | Lookup+кэш курсов |
| `Assets/_Project/Trade/Exchange/Network/ExchangeServer.cs` | T-E03 | NetworkBehaviour + RPC-хаб |
| `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` | T-E04 | 4-я вкладка (код UI в RefreshExchangeData/OnPackClicked/OnUnpackClicked) |
| `Assets/_Project/Trade/Scripts/Client/ExchangeClientState.cs` | T-E03 | Клиентская проекция результатов |
| `Assets/_Project/Trade/Scripts/Dto/ExchangeResultDto.cs` | T-E03 | DTO результата |
| `Assets/_Project/Scenes/BootstrapScene.unity` | T-E05 | `[ExchangeServer]` root GameObject |
| `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` | T-E05 | `OnServerStarted` подписка для спавна BootstrapScene |

## Связанные системы (не менять!)

| Система | Файлы |
|---------|-------|
| Inventory | `Assets/_Project/Items/Core/InventoryWorld.cs`, `Assets/_Project/Scripts/Core/ItemData.cs` |
| InventoryServer | `Assets/_Project/Items/Network/InventoryServer.cs` |
| Market/Trade | `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs`, `Assets/_Project/Trade/Scripts/Core/Warehouse.cs` |
| MarketServer | `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` |
