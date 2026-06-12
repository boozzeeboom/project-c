# Resources Import/Export — Индекс

> **Дата:** 2026-06-11
> **Статус:** Анализ (код не написан)
> **Задача:** спроектировать CSV-импорт/экспорт для объединённой БД ресурсов
> всех подсистем (Inventory, Trade/Market, Exchanger, Crafting, Quests).

## Контекст

В проекте есть **5 типов SO**, описывающих «предметы» в разных подсистемах:

| # | SO | Где живёт | Зачем | ID |
|---|---|---|---|---|
| 1 | `ItemData` | `Assets/_Project/Resources/Items/*.asset` | Pickable, инвентарь игрока | `int` (1..34) из `ItemRegistry.asset` |
| 2 | `TradeItemDefinition` | `Assets/_Project/Trade/Data/Items/*.asset` | Рыночный товар (ящик/канистра/слиток) | `string itemId` ("mesium_canister_v01") |
| 3 | `MarketItemConfig` | вложен в `MarketConfig.asset.items[]` | Экономика рынка (price/stock/regen) | по `itemId` |
| 4 | `RecipeData` | `Assets/_Project/Resources/Crafting/Recipes/*.asset` | Крафт (ingredients → outputs) | по `displayName` + AssetPath |
| 5 | `ExchangeRateEntry` | вложен в `ExchangeRateConfig.asset.rates[]` | Pack/Unpack (inventory ↔ warehouse) | по паре имён |

Плюс служебные SO-списки: `ItemRegistry.asset` (32 id), `TradeItemDatabase.asset` (2 шт),
`ExchangeRateConfig.asset` (4 курса), `MarketConfig.asset` (locationId → items[]).

**Проблема:** все эти данные сейчас **разрозненны** и правятся вручную.
Чтобы добавить 1 новый ресурс, надо создать 5-6 ассетов и согласовать GUID-ы.
Антигравий (T-E05) уже показал это: осколок не зарегистрирован в `ItemRegistry.asset` (там 32, а
последний 33 — сам антгравий), и при попытке Pack сервер не нашёл `inventoryItemId`.

**Решение:** единый CSV-файл (`Resources_Import.csv`) с одной строкой на ресурс,
содержащей ВСЕ поля (inventory + trade + economy + recipes + exchange). Editor tool
`Tools → ProjectC → Resources → CSV Import/Export` импортирует CSV, создаёт/обновляет
все связанные SO, пересобирает `ItemRegistry` и `ExchangeRateConfig`.

## Документы

| Файл | Содержание |
|------|------------|
| [`README.md`](README.md) | Этот файл — индекс, контекст, прецеденты |
| [`01_ANALYSIS.md`](01_ANALYSIS.md) | Глубокий разбор 5 SO-систем |
| [`02_DESIGN.md`](02_DESIGN.md) | Схема CSV, алгоритм, макет окна |
| [`03_TICKETS.md`](03_TICKETS.md) | T-IE01..T-IE07 (декомпозиция) |
| [`04_EXAMPLES.md`](04_EXAMPLES.md) | Примеры CSV + acceptance |
| [`05_USER_GUIDE.md`](05_USER_GUIDE.md) | **Start-to-end: от CSV до ящиков в обменнике в Play Mode** |
| [`06_PRUNE_DESIGN.md`](06_PRUNE_DESIGN.md) | T-IE08 Pruning: режимы none/orphan/replace для очистки устаревших предметов |

## Прецеденты в проекте

Импортируем не «с нуля» — есть полностью работающий паттерн, повторяем 1-в-1:

- `Assets/_Project/Quests/Editor/QuestCsvSchema.cs` — 4 класса: `QuestCsvSchema.Columns[]`,
  `QuestCsvRow`, `QuestCsvParser` (с поддержкой quoted/escaped), `QuestCsvValidator`.
- `Assets/_Project/Quests/Editor/QuestCsvImporter.cs` — статический класс, разбирает CSV,
  группирует, создаёт/обновляет SO через `AssetDatabase.CreateAsset`/`LoadAssetAtPath`.
- `Assets/_Project/Quests/Editor/QuestCsvExporter.cs` — обратная операция, SO → CSV.
- `Assets/_Project/Quests/Editor/QuestCsvWindow.cs` — `EditorWindow` с UI Toolkit:
  TextField + Browse + Preview ListView + кнопки Import/Export.

**Адаптация для ресурсов** = то же самое, но:
1. Schema шире (~30 колонок вместо 19).
2. Importer создаёт не один тип SO, а 4-5 (ItemData, TradeItemDefinition, MarketItemConfig, RecipeData, ExchangeRateEntry).
3. Главное отличие — **импорт нескольких RecipeData с ингредиентами/outputs из одной строки
   невозможен** ⇒ рецепты живут в ОТДЕЛЬНОМ блоке строк CSV (с разделителем `block`).
4. Валидатор должен проверить ВСЕ кросс-ссылки: itemName → ItemRegistry, warehouseItemId →
   TradeItemDefinition, ingredient/output → ItemData по имени.

## Что НЕ входит (границы MVP)

- ❌ Экспорт в JSON/Excel/YAML — только CSV (как у квестов).
- ❌ Синхронизация с Google Sheets / БД — нет в Stage 2.5, приоритет низкий.
- ❌ Сложные pack/unpack с комиссиями (T-E01 в плане Exchanger, но не сделано) — flat rate.
- ❌ Драг-н-дроп в Window — buttons-only (как `QuestCsvWindow`).
- ❌ Импорт старых `.asset`-ов в CSV — exporter создаст новый CSV, но **merge с уже существующими
  ассетами** вне области (если экспортируем → правим → импортируем, **теряем не в CSV
  изменения**, например вручную добавленные maxStack/description — это известное
  ограничение, см. `01_ANALYSIS.md` §6).

## Связанные документы

- `docs/Markets/Resources_exchanger/` — обменник (Exchanger, T-E01..T-E05, ✅ DONE).
  CSV-импорт должен **включать** курсы Exchanger'а, чтобы они не «отвалились».
- `docs/STEP_BY_STEP_DEVELOPMENT.md` — общий roadmap проекта.
- `docs/Crafting_system/`, `docs/Mining/`, `docs/Quests/` — подсистемы, чьи данные
  импортируются (recipes, resourceNodes, quest tasks).
