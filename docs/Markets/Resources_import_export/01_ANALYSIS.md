# Resources Import/Export — Глубокий анализ систем

> **Дата:** 2026-06-11
> **Статус:** Анализ (код не написан)
> **Цель:** дать полную картину всех 5 типов SO, их ID-пространств, конвенций и текущего состояния,
> чтобы спроектировать CSV-схему без сюрпризов.

---

## TL;DR

Проект хранит «предмет» в 5 разных SO с разными ID-пространствами (int / string / AssetPath).
Все они должны **согласованно** описывать один и тот же ресурс: 1 руда = 1 ItemData + 1 TradeItemDefinition
(ящик 100 руды) + 1 MarketItemConfig (цена/сток на рынке) + N RecipeData (любые рецепты
с этой рудой) + 1 ExchangeRateEntry (как упаковать/распаковать). CSV должен связать всё это
в одной таблице через `itemName` (человекочитаемое имя) и/или `assetKey` (стабильный латинский
ключ для кросс-системных ссылок).

---

## 1. Пять систем предметов (полная инвентаризация)

### 1.1 `ItemData` — пикабельный предмет (Inventory)

| Поле | Тип | Описание |
|------|-----|----------|
| `itemName` | `string` | **Человекочитаемое имя, уникальное**. "Железная руда", "Антигравий (осколок)" |
| `itemType` | `ItemType` enum | Resources / Equipment / Food / Fuel / Antigrav / Meziy / Medical / Tech |
| `description` | `string` | UI/tooltip |
| `icon` | `Sprite` | Ссылка (можно null на старте) |
| `maxStack` | `int` | Сколько в 1 слоте (1 = non-stackable). Phase 6, дефолт 1 |
| `weightKg` | `float` | Вес (для будущей cargo) |

**ID:** `int` (1..N) через `ItemRegistry.asset` → `[ItemRegistry.Entry] entries: { id, item }`.
Fallback при отсутствии — `Resources.LoadAll<ItemData>("Items")` + авто-инкремент id.

**Где живут:** `Assets/_Project/Resources/Items/*.asset` (47 файлов на 2026-06-11).
Имя файла: `Item_{ItemType}_{Sanitize(itemName)}.asset` (конвенция из `ItemDatasetGenerator`).

**Потребители:**
- `InventoryWorld._itemDatabase` (server POCO, key = int)
- `PickupItem.itemData` (scene-prefab, поле `[SerializeField]`)
- `RecipeData._ingredients[].item`, `_outputs[].item` (Editor-сериализованные ссылки)
- `ResourceNodeConfig._resultItem`, `_requiredTool`
- `LootTable.entries[].item`, `.guaranteedItems[]`
- `QuestObjective.itemTradeItemId` (строковый, lookup через `ItemRegistry.TryGetIdByName`)

**Ключевая особенность:** `ItemRegistry.asset` сейчас **НЕПОЛНЫЙ** (id 1..32, а в `Resources/Items/`
**47 файлов**, включая антгравий-осколок, ключи, рецепты, тестовые заглушки). Ассеты
существуют, но не зарегистрированы ⇒ при pickup через `PickupItem.Collect()` сервер вызовет
`GetOrRegisterItemId` и авто-добавит с id=33+, но `ItemRegistry.TryGetIdByName("Антигравий
(осколок)")` вернёт 0, и квесты его не найдут. **Это известный долг, CSV-импорт его починит.**

### 1.2 `TradeItemDefinition` — рыночный товар (ящик/канистра)

| Поле | Тип | Описание |
|------|-----|----------|
| `itemId` | `string` | **Уникальный стабильный ключ** (латиница, snake_case, +version). "mesium_canister_v01" |
| `displayName` | `string` | "Мезий (канистра)" |
| `icon` | `Sprite` | UI |
| `basePrice` | `float` | Базовая цена CR |
| `weight` | `float` | Вес единицы (кг) |
| `volume` | `float` | Объём единицы (м³) |
| `slots` | `int` | Cargo-slots за единицу |
| `isDangerous` | `bool` | (Мезий — протечка при столкновении) |
| `isFragile` | `bool` | (Двигатели, МНП) |
| `isContraband` | `bool` | (Нелегальный товар) |
| `requiredFaction` | `Faction` enum | None/NP/Aurora/Titan/Hermes/Prometheus/FreeTraders/Military |

**ID:** `string itemId` (стабильный, versioned, snake_case).

**Где живут:** `Assets/_Project/Trade/Data/Items/*.asset` (2 файла: Mesium, Antigrav).
Имя файла: `TradeItem_{itemId}.asset` (конвенция из `TradeAssetGenerator`).

**Список:** `Assets/_Project/Trade/Data/TradeItemDatabase.asset` → `TradeDatabase.allItems: List<TradeItemDefinition>`.
Используется только для UI (`GetItemByDisplayName`, `GetItemsByFaction`).
**Серверная логика (`TradeWorld`, `Warehouse`) НЕ использует `TradeDatabase`** — она
берёт `TradeItemDefinitionResolver` (инжектится в `TradeWorld.Resolver`), который сейчас
строится по `Resources.Load<TradeDatabase>("Trade/TradeItemDatabase")`.

**Потребители:**
- `MarketItemConfig.definition` (Editor-сериализованная ссылка)
- `Warehouse.GetQuantity(itemId)` (runtime state)
- `MarketServer.Buy/Sell` (по itemId)
- `ExchangeRateEntry.warehouseItemId` (ссылка через строку)

**Ключевая особенность:** `itemId` — это **версионированный** ключ ("v01", "v02"), и
это **отдельное** от `itemName` пространство. Ящик железной руды `resource_iron_box`
связан с пикаблом "Железная руда" только через `ExchangeRateEntry`.

### 1.3 `MarketItemConfig` — экономика рынка

| Поле | Тип | Описание |
|------|-----|----------|
| `itemId` | `string` | (= `TradeItemDefinition.itemId`) |
| `definition` | `TradeItemDefinition` | Ссылка на SO (для UI: icon, weight, volume, slots) |
| `basePrice` | `float` | Базовая цена (CR). Multiplier: `currentPrice = basePrice * (1 + demand - supply) * event` |
| `initialStock` | `int` | Стартовый сток |
| `regenPerTick` | `float [0..0.5]` | Регенерация (0.02 = +2% от initialStock за тик) |
| `factionRestriction` | `Faction` | None = всем |
| `allowBuy` | `bool` | Можно ли покупать у рынка |
| `allowSell` | `bool` | Можно ли продавать рынку |

**Где живёт:** НЕ отдельные ассеты. Хранится в `MarketConfig.asset.items: List<MarketItemConfig>`.

**Структура:**
- `Assets/_Project/Trade/Data/.../MarketConfig_{locationId}.asset` (Primium, Secund, Tertius, Quartus).
- `MarketConfig.locationId` = "primium" / "secundus" / etc.
- `MarketConfig.items` — список конфигов.

**Потребители:** `MarketTrader` (server POCO), `MarketState` (runtime), UI MarketWindow.

**Ключевая особенность:** один `TradeItemDefinition` может продаваться **на нескольких рынках**
с разной ценой (Primium vs Secund). Но сейчас `MarketConfig` — один ассет, и в нём
`items[]` — все товары этого рынка. **В CSV одна строка = одна пара (itemId, locationId)**.

### 1.4 `RecipeData` — рецепт крафта

| Поле | Тип | Описание |
|------|-----|----------|
| `_displayName` | `string` | "Железный слиток" |
| `_icon` | `Sprite` | UI |
| `_description` | `string` | Tooltip |
| `_category` | `RecipeCategory` enum | Module/Consumable/Ship/Material/Misc |
| `_ingredients[]` | `RecipeIngredient[]` | [{ item: ItemData, quantity: int }] |
| `_outputs[]` | `RecipeOutput[]` | [{ item: ItemData, quantity: int }] |
| `_craftSeconds` | `float` | Базовое время |
| `_requiredSkillLevel` | `int` | Phase 2 (MVP: 0) |
| `_requiredSkill` | `SkillType` enum | Phase 2 (MVP: None) |

**Где живут:** `Assets/_Project/Resources/Crafting/Recipes/*.asset` (5 файлов:
`Recipe_IronIngot`, `Recipe_CopperIngot`, `Recipe_ShipKeyLight`, +2 заглушки).

**Список:** `CraftingStationConfig._allowedRecipes[]` (Editor-сериализованный массив).
Нет центрального `RecipeDatabase` (в отличие от `TradeItemDatabase`).

**Потребители:** `CraftingWorld.RegisterRecipe` (server), `CraftingStationConfig` (UI),
`Station_CraftingTable` / `Station_Shipyard`.

**Ключевая особенность:** рецепт ссылается на `ItemData` через **GUID в .asset** (Editor-time).
В CSV придётся писать **либо `assetPath`** (`Resources/Items/Item_Resources_Железная_руда.asset`),
**либо `itemName`** (lookup через `ItemRegistry`).

### 1.5 `ExchangeRateEntry` — курс Pack/Unpack

| Поле | Тип | Описание |
|------|-----|----------|
| `warehouseItemId` | `string` | `TradeItemDefinition.itemId` ящика |
| `warehouseQty` | `int` | Сколько ящиков за 1 операцию |
| `inventoryItemName` | `string` | `ItemData.itemName` пикабла |
| `inventoryQty` | `int` | Сколько пикаблов за 1 операцию |
| `displayName` | `string` | UI ("Ящик железной руды") |

**Где живёт:** `Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset` →
`ExchangeRateConfig.rates: List<ExchangeRateEntry>`. Сейчас 4 записи: IronOre, CopperOre, Wood, Antigrav.

**Потребители:** `ExchangeServer.RequestPackRpc/UnpackRpc` (server),
`ResourceExchangeResolver.FindRateForItemName/FindRateForWarehouseItem` (lookup).

**Ключевая особенность:** **связывает два ID-пространства** (int ItemData ↔ string TradeItemDefinition)
через `inventoryItemName` (lookup в `InventoryWorld._itemDatabase` по `itemName`).
Никаких GUID, чистый строковый матч.

### 1.6 Сводная таблица

| Аспект | ItemData | TradeItemDefinition | MarketItemConfig | RecipeData | ExchangeRateEntry |
|--------|:--------:|:-------------------:|:----------------:|:----------:|:-----------------:|
| Файл | 1 | 1 | inline в MarketConfig | 1 | inline в ExchangeRateConfig |
| ID-тип | `int` | `string itemId` | `string itemId` + locationId | `displayName` (нет ID) | пара string'ов |
| ID-источник | `ItemRegistry.asset` | `itemId` поле | `itemId` поле | — | — |
| Сериализация ссылок | GUID в .asset | GUID в .asset | GUID в .asset | GUID в .asset | строковый матч |
| Потребители | Inventory, Quests, Crafting, Mining, Exchanger | Trade, Exchanger, Market | Market | Crafting | Exchanger |
| Список-SO | `ItemRegistry.asset` | `TradeItemDatabase.asset` | `MarketConfig.asset` | `CraftingStationConfig._allowedRecipes[]` | `ExchangeRateConfig.asset` |

---

## 2. Конвенции имён и путей (всё, что мы должны соблюдать)

### 2.1 ItemData

- **ItemType enum** (порядок важен — это `int` в .asset):
  ```
  0 Resources, 1 Equipment, 2 Food, 3 Fuel, 4 Antigrav, 5 Meziy, 6 Medical, 7 Tech
  ```
- **Имя файла:** `Item_{ItemType}_{Sanitize(itemName)}.asset`
  - Sanitize: оставить только `IsLetterOrDigit`, `_`, `-`, остальное → `_`.
  - Пример: "Железная руда" → `Item_Resources_Железная_руда.asset`
- **Папка:** `Assets/_Project/Resources/Items/`
- **Префикс MenuItem:** `Project C/Item Data`

### 2.2 TradeItemDefinition

- **Имя файла:** `TradeItem_{itemId}.asset` (itemId в snake_case).
  - Пример: "mesium_canister_v01" → `TradeItem_Mesium_v01.asset`
- **Папка:** `Assets/_Project/Trade/Data/Items/`
- **Префикс MenuItem:** `ProjectC/Trade Item`
- **`itemId` конвенция** (по существующим):
  - `mesium_canister_v01` — `noun_material_form_version`
  - `antigrav_ingot_v01`
  - `resource_iron_box`, `resource_copper_box`, `resource_wood_box` (из Exchanger)
  - Версия (`_v01`) — для будущих ребалансов (если меняется состав — новая версия).

### 2.3 MarketItemConfig (inline)

- Внутри `MarketConfig.asset.items[]` — не отдельные файлы.
- **Имя файла MarketConfig:** `MarketConfig_{locationId}.asset` → `MarketConfig_primium.asset`.
- **Папка:** `Assets/_Project/Trade/Data/Markets/` (предположительно).
- **locationId конвенция:** `primium`, `secundus`, `tertius`, `quartus` (лат. порядковые).

### 2.4 RecipeData

- **Имя файла:** `Recipe_{displayName}.asset` (Sanitize не применяется).
  - Пример: "Железный слиток" → `Recipe_IronIngot.asset` (транслит вручную).
- **Папка:** `Assets/_Project/Resources/Crafting/Recipes/`
- **Префикс MenuItem:** `Project C/Crafting/Recipe`
- **CraftingStationConfig** (где AllowedRecipes): `Assets/_Project/Resources/Crafting/Stations/Station_*.asset`

### 2.5 ExchangeRateEntry (inline)

- Внутри `Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset` → `ExchangeRateConfig.rates[]`.
- **Сейчас один ассет на проект**. T-E01 планировал `MarketConfig.locationId → свой ExchangeRateConfig`
  (пока не сделано, см. `02_IMPLEMENTATION.md` §7 "Ограничения" п.3).

---

## 3. Текущее состояние — что неполно/сломано

| Проблема | Файл | Влияние |
|----------|------|---------|
| `ItemRegistry.asset` содержит только 32 записи (id 1..32) | `Assets/_Project/Items/Data/ItemRegistry.asset` | Антгравий-осколок, ключи, рецепты, тестовые заглушки — НЕ зарегистрированы |
| `TradeItemDatabase.asset` содержит только 2 записи (Mesium, Antigrav) | `Assets/_Project/Trade/Data/TradeItemDatabase.asset` | Нет box'ов для Iron/Copper/Wood, нет других товаров |
| `DefaultExchangeRate.asset` содержит 4 записи | `Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset` | ОК для MVP, но не расширяется до локаций |
| Антгравий: ItemData "Антигравий (осколок)" есть в `Resources/Items/`, но НЕ в `ItemRegistry` | `Assets/_Project/Resources/Items/Item_Antigrav_Антигравий_осколок.asset` | Серверный `ExchangeWorld.Unpack` не сможет resolveInventoryItemId по "Антигравий (осколок)" — NRE/fail |
| `ItemDatasetGenerator` создаёт 24 предмета, но идемпотентно — не перезаписывает уже созданные | `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs:112` | Невозможно «обновить» через регенерацию — надо удалять вручную |
| `TradeAssetGenerator` создаёт только Mesium + Antigrav | `Assets/_Project/Trade/Scripts/Editor/TradeAssetGenerator.cs` | То же ограничение |
| LootTable ссылается на ItemData по GUID (`{fileID: 11400000, guid: ...}`) | `LootTable_TestCommon.asset` | Удалить .asset = сломать LootTable (orphan reference) |
| `MarketConfig` — все 4 есть в `Trade/Data/Markets/`: Primium, Secundus, Tertius, Quartus | `Assets/_Project/Trade/Data/Markets/MarketConfig_*.asset` | OK. На каждом — `mesium_canister_v01` + `antigrav_ingot_v01` (кроме Tertius — только Mesium) |
| `LootTable.entries[].chance` хранится как `float` (0..1) | `Assets/_Project/Scripts/Core/LootTable.cs` | CSV-колонка `lootChance` должна быть `float` |
| `ResourceNode` использует `ItemType`-поле + ссылку на `ItemData` (только 1 результат) | `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs` | Не покрывается общей БД — отдельный `ResourceNodeConfig` (T-G01) |

**Главный gap:** нельзя добавить 1 ресурс в проект без ручной работы в 5+ местах.
CSV-импорт покрывает 4 из 5 (ItemData, TradeItemDefinition, MarketItemConfig, RecipeData, ExchangeRateEntry);
ResourceNodeConfig и LootTable — **вне scope** первой итерации (но могут быть добавлены позже).

---

## 4. ID-пространства и их связь

```
                    itemName (string, уник.)
                          │
                          ▼
                    ┌────────────┐
                    │  ItemData  │ ← int id (ItemRegistry)
                    └────────────┘
                          │
              ┌───────────┼────────────┐
              │           │            │
              ▼           ▼            ▼
       ResourceNode    Recipe      Exchanger
       ._resultItem   ._inputs[]   .inventoryItemName
              │           │            │
              └─────────┬─┘            │
                        ▼              ▼
                    Warehouse.TryAdd(rate.warehouseItemId, ...)
                        ▲              │
                        │              ▼
                  itemId (string)  ExchangeRateEntry
                        ▲              │
                        │              ▼
                  TradeItemDefinition  (warehouseItemId = itemId)
                        ▲
                        │
                  MarketItemConfig
                  .itemId + .definition
                        ▲
                        │
                  MarketConfig.items[]
                  (per locationId)
```

**Главный вывод:** `itemName` (ItemData) и `itemId` (TradeItemDefinition) — **два
независимых ID-пространства**, и они могут иметь **разное число записей**:
- 47 ItemData (включая ключи, еду, медикаменты — то, что не торгуется).
- 2 TradeItemDefinition (только то, что продаётся на рынке).
- 4 ExchangeRateEntry (только пары, для которых есть обмен).

CSV должен это уважать: одна строка = один **ресурсный item** (ItemData), а колонки
`tradeItemId, marketLocationId, exchangeRate` — опциональные (пустые для ключей, еды и т.д.).

---

## 5. Кросс-ссылки — что валидировать

При импорте CSV надо проверить, что:

1. **ItemRegistry.entries уникальны по id** (без дубликатов).
2. **ItemRegistry.entries уникальны по itemName** (без дубликатов).
3. **ItemData.itemName уникальны** (между всеми ассетами в `Resources/Items/`).
4. **TradeItemDefinition.itemId уникальны** (между всеми ассетами в `Trade/Data/Items/`).
5. **MarketItemConfig.itemId существует** как `TradeItemDefinition.itemId`.
6. **MarketItemConfig.definition** (GUID) указывает на правильный `TradeItemDefinition`.
7. **RecipeData._ingredients[].item** (GUID) указывает на существующий `ItemData`.
8. **RecipeData._outputs[].item** (GUID) указывает на существующий `ItemData`.
9. **ExchangeRateEntry.warehouseItemId** существует как `TradeItemDefinition.itemId`.
10. **ExchangeRateEntry.inventoryItemName** существует как `ItemData.itemName`.
11. **CSV-блок `recipes`**: `recipeName` уникален; все `ingredientItemName` и `outputItemName` есть в ItemRegistry.
12. **CSV-блок `marketItems`**: `itemId` есть в TradeDatabase; `locationId` есть в MarketConfig.
13. **CSV-блок `exchangeRates`**: пара (warehouseItemId, inventoryItemName) уникальна.

---

## 6. Известные ограничения, которые CSV-импорт не решает (или решает частично)

1. **Round-trip потеря не-полей:** если вручную добавить `icon`, `description` в `.asset`,
   экспорт в CSV их **включит**, но **обратный импорт перезапишет** по CSV-данным.
   Решение: CSV = source of truth (всё в CSV), `.asset` генерируется; ручные правки
   в `.asset` после импорта — не поддерживаются (откатываются при следующем импорте).
2. **GUID-ы в RecipeData:** сейчас в `_ingredients[].item` хранится `{guid: X, fileID: 11400000, type: 2}`.
   При пересоздании `.asset` GUID **может измениться** (Unity назначает новый при `CreateAsset`).
   ⇒ Если есть другие `.asset`, ссылающиеся на этот ItemData по GUID, они **сломаются**.
   **Решение:** НЕ пересоздавать существующие `.asset` без нужды — обновлять поля через
   `EditorUtility.SetDirty` (см. `QuestCsvImporter.cs:213`). Если ассета нет — создать.
3. **Stacking в InventoryWorld:** каждый id = 1 слот. 100 осколков = 100 слотов, требует
   `maxSlots=1000` (см. `InventoryWorld.cs:128`). CSV-импорт не меняет это.
4. **Несколько MarketZone:** T-E01 в плане — `MarketConfig.locationId → свой ExchangeRateConfig`.
   Не сделано. CSV-импорт должен **поддержать** это (одна строка exchange rate с
   колонкой `locationId`), даже если пока один ассет на проект.
5. **ItemData.icon, TradeItemDefinition.icon** — Sprite-ссылки. CSV не может их
   нести (бинарный asset). **Решение:** оставить в .asset (или null), CSV не трогает.
   Importer **не обновляет** `icon` (только остальные поля).
6. **Нет merge двух CSV:** если пользователь держит `Resources_Import_2026_06.csv` и
   `Resources_Import_2026_07.csv`, импортируется **только один** — второй полностью
   перезаписывает. Частичный merge (по itemName) — Phase 2.
7. **ResourceNode и LootTable — вне scope первой итерации.** Они не покрываются
   общей БД. Импортёр может **сообщить warning** если встретит `resourceNodeItem` в CSV
   (пока не реализовано), но **не создаёт** ResourceNodeConfig.

---

## 7. Соответствие существующему коду — что НЕ сломать

CSV-импорт **не должен** менять:
- `InventoryWorld.RegisterAllItems()` — fallback на `ItemRegistry.Instance` остаётся.
- `ItemRegistry.TryGetIdByName` — нужен для совместимости с QuestWorld.
- `ResourceExchangeResolver.FindRateForItemName` — ищет по `inventoryItemName` (== `ItemData.itemName`).
- `TradeWorld.Resolver` (TradeItemDefinitionResolver) — резолвит itemId → weight/volume через
  `TradeDatabase`. CSV-импортёр должен **обновлять** `TradeDatabase.allItems` (это и есть
  список).
- `MarketItemConfig.definition` — Editor-сериализованная ссылка. **MUST be set** при
  создании/обновлении MarketItemConfig, иначе UI не покажет иконку.
- `RecipeData._ingredients[].item` — Editor-сериализованная ссылка. **MUST be set**
  при создании/обновлении, иначе `CraftingWorld.RegisterRecipe` упадёт.
- `.meta` файлы — НЕ пересоздавать, чтобы не сломать обратные ссылки.

---

## 8. Аналоги в проекте (повторить паттерн)

### 8.1 QuestCsvSchema (`Assets/_Project/Quests/Editor/QuestCsvSchema.cs`)

| Класс | Назначение | Что копируем |
|-------|------------|--------------|
| `QuestCsvSchema.Columns[]` | Метаданные колонок (name, aliases, required, type, defaultValue) | То же: `ResourcesCsvSchema.Columns[]` |
| `QuestCsvRow` | `Dictionary<string, string> values`, `lineNumber`, `List<string> errors` | То же |
| `QuestCsvParser` | `ParseFile/ParseText`, `SplitCsvLines` (поддержка quoted), `ParseCsvLine` (escaped quotes) | Скопировать **как есть** (общий утилитарный класс вынести) |
| `QuestCsvValidator` | Cross-валидация (кросс-ссылки) | Скопировать паттерн |
| `QuestCsvImporter` | `Import(string csvPath) → ImportResult` | Адаптировать под 5 типов SO |
| `QuestCsvExporter` | `Export(string csvPath, QuestDefinition[]) → int rowsWritten` | Адаптировать |
| `QuestCsvWindow` | `EditorWindow` с UI Toolkit: TextField + Browse + Preview ListView + кнопки | Скопировать структуру, заменить `QuestCsv*` на `ResourcesCsv*` |

### 8.2 Ключевые нюансы паттерна

- **CSV в UTF-8** с BOM (Excel открывает по дефолту).
- **Header row** — обязателен, canonical names + aliases (поддержка русских имён колонок).
- **Required columns** — проверка перед импортом (глобальный error, не row error).
- **Quoted/escaped fields** — поддержка `"`, `""`, multiline.
- **Defaults** — если колонка пуста, берётся `defaultValue` из ColumnDef.
- **Per-row errors** — собираются, но **не блокируют** импорт других строк.
- **Per-row warnings** — отложенные (если уже существует — обновляем молча).
- **`AssetDatabase.LoadAssetAtPath<T>`** — сначала пробуем загрузить, потом создаём.
- **`EditorUtility.SetDirty(so)`** + `AssetDatabase.SaveAssets()` + `AssetDatabase.Refresh()` в конце.
- **`AssetDatabase.IsValidFolder` / `CreateFolder`** — создаём папки рекурсивно.
- **Preview в Window** — `ListView` с колонками: line, key, name, errors.
- **Browse button** — `EditorUtility.OpenFilePanel("Select CSV", "", "csv")`.
- **Result dialog** — `EditorUtility.DisplayDialog("Title", "Message", "OK")` с итогами.

### 8.3 Что **дополнительно** нужно для Resources

- **Несколько блоков в одном CSV:** `inventory`, `tradeItems`, `marketItems`, `recipes`, `exchangeRates`.
  Разделитель блоков — пустая строка + строка-маркер `# block=recipes` (или секции в одном файле).
  Альтернатива: 5 отдельных CSV. **Рекомендация: один CSV, секции** (как в INI/CSV-SDF).
- **Поиск ItemData по itemName:** `Resources.FindObjectsOfTypeAll<ItemData>()` или
  `AssetDatabase.FindAssets("t:ItemData")` + `LoadAssetAtPath<ItemData>(path)`.
  Кэшировать `Dictionary<string, ItemData>` на время импорта.
- **Поиск TradeItemDefinition по itemId:** `AssetDatabase.FindAssets("t:TradeItemDefinition")`.
- **Поиск/создание MarketConfig по locationId:** `Resources.FindAssets` или
  `LoadAssetAtPath<MarketConfig>("Assets/_Project/Trade/Data/Markets/MarketConfig_{id}.asset")`.
- **Дозапись в ItemRegistry** — `Entry { id, item }` где `id` = max(existing) + 1.
  **Проблема:** существующий ItemRegistry неполон ⇒ при импорте не добавлять
  «уже зарегистрированные» id; добавлять только новые.
- **GUID ассета для кросс-ссылок:** `AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item))`
  — потом использовать в YAML-полях ссылок (при `AssetDatabase.CreateAsset`/`SaveAssets`
  Unity сама сериализует ссылки на другие SO через GUID).

---

## 9. Что **точно** будет в CSV (предпросмотр колонок)

> Финальная схема — в `02_DESIGN.md`. Здесь — sanity-check полноты.

### Блок 1: inventory (ItemData) — основной блок, 1 строка = 1 пикабл
- `itemName` (required, unique), `itemType` (enum), `description`, `maxStack`, `weightKg`

### Блок 2: tradeItems (TradeItemDefinition) — 1 строка = 1 рыночный товар
- `itemId` (required, snake_case + _vNN), `displayName`, `basePrice`, `weight`, `volume`, `slots`,
  `isDangerous`, `isFragile`, `isContraband`, `requiredFaction`

### Блок 3: marketItems (MarketItemConfig) — 1 строка = 1 пара (itemId, locationId)
- `itemId` (= TradeItem), `locationId` (= primium/secundus), `basePrice` (override?),
  `initialStock`, `regenPerTick`, `factionRestriction`, `allowBuy`, `allowSell`

### Блок 4: exchangeRates (ExchangeRateEntry) — 1 строка = 1 пара
- `warehouseItemId` (= TradeItem), `warehouseQty`, `inventoryItemName` (= ItemData),
  `inventoryQty`, `displayName`, опц. `locationId` (пока None)

### Блок 5: recipes (RecipeData) — 1 строка = 1 рецепт
- `recipeName` (required, unique), `displayName`, `category`, `craftSeconds`,
  `requiredSkillLevel`, `requiredSkill`, `ingredient1Name`+`ingredient1Qty`, `ingredient2Name`+`ingredient2Qty`, …
  `output1Name`+`output1Qty`, `output2Name`+`output2Qty`, …

Поскольку рецепты имеют переменное число ингредиентов, **либо** используем
`ingredientNames`+`ingredientQtys` (semicolon-separated, как в `QuestCsvExporter`),
**либо** фиксируем максимум (например, до 5 ингредиентов и 3 outputs) с
`ingredient1Name..ingredient5Name` колонками. **Рекомендация: semicolon-separated**
(как у квестов), проще для content writer'а, парсер один и тот же.

---

## 10. Что НЕ нужно в CSV (out of scope)

- `icon` (Sprite) — бинарный asset, остаётся в `.asset`.
- `displayName` overrides на UI — текущая конвенция `displayName` в TradeItemDefinition,
  для ItemData — `itemName`. CSV использует эти же поля.
- Транслитерация имени файла — генерится автоматически по конвенции.
- `.meta` — генерится Unity, нельзя писать вручную (см. AGENTS.md "Hard Rules").
- Кастомные скриптыые классы (`Station_Shipyard`, `Station_CraftingTable`) — не покрываются
  общей БД.

---

## 11. Ключевые риски (для обсуждения)

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| GUID-ы ItemData меняются при пересоздании `.asset` → ломают LootTable/Recipe/_allowedRecipes | Средняя | Обновлять существующие `.asset` через `SetDirty`, не пересоздавать |
| ItemRegistry.asset при импорте получит дубль id | Средняя | Валидация на max(id)+1, проверка `entries.All(e => e.id != newId)` |
| CSV в Excel откроется криво (BOM/кодировка) | Высокая | UTF-8 BOM, тест открытия в Excel/LibreOffice |
| Контент-райтер ошибётся в `itemName` (опечатка в "Железная руда" vs "Железная руды") | Высокая | CrossValidate перед импортом, показать diff в Preview |
| Несколько человек редактируют CSV в git → merge-конфликты | Средняя | Документировать, что CSV = source of truth, merge вручную |
| `IngredientItems` (semicolon-list) парсятся неправильно | Низкая | Скопировать `ParseActions` из `QuestCsvImporter` |
| ExchangeRateEntry добавляется, но `ExchangeServer` не подхватывает (cache не обновлён) | Низкая | После импорта `EditorUtility.SetDirty(exchangeRateConfig)` + Refresh, restart в PlayMode |

---

## 12. Что после импорта надо проверить (acceptance)

1. `Assets/_Project/Resources/Items/` — все ItemData-ассеты созданы/обновлены.
2. `Assets/_Project/Items/Data/ItemRegistry.asset` — содержит **все** ItemData (id 1..N без пропусков).
3. `Assets/_Project/Trade/Data/Items/` — все TradeItemDefinition-ассеты.
4. `Assets/_Project/Trade/Data/TradeItemDatabase.asset` — `allItems` содержит все TradeItemDefinition.
5. `Assets/_Project/Trade/Data/Markets/MarketConfig_*.asset` — все locationId.
6. `Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset` — все курсы.
7. `Assets/_Project/Resources/Crafting/Recipes/` — все RecipeData-ассеты.
8. `Assets/_Project/Resources/Crafting/Stations/Station_*.asset` — `_allowedRecipes` обновлены
   (если новые рецепты добавлены в `recipeStation` колонку — но это уже отдельная фича).
9. Unity Editor **компилируется без ошибок** (все ссылки на ItemData резолвятся).
10. Play Mode: Host → подойти к MarketZone → Exchanger tab → все 4 ящика отображаются
    + Антгравий (T-E05) — осколок регистрируется, Pack 100 осколков → +1 ящик.

---

Дальше: [`02_DESIGN.md`](02_DESIGN.md) — конкретная схема CSV-колонок, алгоритм импорта/экспорта,
макет Editor Window.
