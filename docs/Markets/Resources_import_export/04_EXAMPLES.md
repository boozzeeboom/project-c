# Resources Import/Export — Примеры

> **Дата:** 2026-06-11
> **Статус:** Анализ (код не написан)
> **Цель:** конкретные примеры CSV для каждого случая использования + acceptance-сценарии.

---

## TL;DR

5 примеров CSV, каждый — реальный сценарий из существующих ассетов проекта.
Все примеры — копи-паст в файл `Resources_Import.csv`.

| # | Сценарий | Что внутри |
|---|----------|------------|
| 1 | Минимальный (3 строки) | Smoke test «импорт работает» |
| 2 | Полный (47+5+8+4+5 = 69 строк) | Текущее состояние проекта (эталон) |
| 3 | Добавление 1 ресурса | User flow «как добавить кирку» |
| 4 | Round-trip dump | После Export → Import без изменений |
| 5 | Ошибочный (rejected) | Что **не** импортируется |

---

## 1. Минимальный CSV (smoke test)

Минимально валидный CSV: 1 строка в каждой секции.

```csv
# Project C: The Clouds — Resources Database (smoke test)
# DO NOT edit header. Add rows below. Save as UTF-8 with BOM.

# block=inventory
itemName,itemType,description,maxStack,weightKg
ТестКирка,Equipment,Тестовая кирка для smoke-теста импорта.,1,2.5

# block=tradeItems
tradeItemId,displayName,basePrice,weight,volume,slots,isDangerous,isFragile,isContraband,requiredFaction
test_pickaxe_box,Ящик тестовых кирок,500,15,1.5,2,n,y,n,None

# block=marketItems
tradeItemId,locationId,basePrice,initialStock,regenPerTick,factionRestriction,allowBuy,allowSell
test_pickaxe_box,primium,550,5,0.01,None,y,y

# block=exchangeRates
tradeItemId,inventoryItemName,warehouseQty,inventoryQty,displayName
test_pickaxe_box,ТестКирка,1,10,Ящик тестовых кирок

# block=recipes
recipeName,displayName,category,craftSeconds,requiredSkillLevel,requiredSkill,ingredient1Name,ingredient1Qty,ingredient2Name,ingredient2Qty,output1Name,output1Qty
Recipe_TestPickaxe,ТестКирка,Material,30,0,None,Железная руда,2,Древесина,1,ТестКирка,1
```

**Acceptance:**
- [ ] Preview: 1+1+1+1+1 = 5 rows, 0 errors (НО: `Железная руда` и `Древесина` должны быть
  в inventory — иначе ошибка cross-validate).
- [ ] Если `Железная руда` и `Древесина` ещё не импортированы — добавить строки в inventory.
- [ ] Import → "Created 1 ItemData, 1 TradeItem, 1 MarketItem, 1 ExchangeRate, 1 Recipe".
- [ ] Проверить файлы:
  - `Resources/Items/Item_Equipment_ТестКирка.asset`
  - `Trade/Data/Items/TradeItem_test_pickaxe_box.asset`
  - `Trade/Data/Markets/MarketConfig_primium.asset` (создан)
  - `Resources/Crafting/Recipes/Recipe_Recipe_TestPickaxe.asset` (с `_displayName="ТестКирка"`)

---

## 2. Полный CSV (эталон, текущее состояние)

Собран из существующих 47 ItemData + 2 TradeItem + ... **НЕ тестировался** (нужен Export,
которого ещё нет). Приведён для справки — что должно быть в файле.

```csv
# Project C: The Clouds — Resources Database
# Generated 2026-06-11. Source of truth for ItemData, TradeItemDefinition,
# MarketItemConfig, ExchangeRateEntry, RecipeData.

# block=inventory
# (47 строк — все ItemData из Resources/Items/)
itemName,itemType,description,maxStack,weightKg
Железная руда,Resources,Обычная железная руда. Добывается в шахтах.,20,2.0
Медная руда,Resources,Красноватая медная руда. Используется в проводке.,20,1.5
Кристаллическая пыль,Resources,Мерцающие кристаллы. Редкий ресурс.,10,0.5
Древесина,Resources,Обычная древесина. Строительный материал.,50,1.0
Верёвка 10м,Equipment,Прочная верёвка длиной 10 метров. Нужна для восхождений.,5,0.8
Карабин,Equipment,Металлический карабин для страховки.,10,0.1
Фонарь,Equipment,Ручной фонарь. Освещает тёмные места.,1,0.3
Сухпаёк,Food,Военный сухой паёк. Восстанавливает силы.,10,0.4
Консервы,Food,Мясные консервы. Долго хранятся.,10,0.5
Бутыль воды,Food,Чистая питьевая вода. 0.5 литра.,5,0.6
Антигравитационное топливо,Fuel,Жидкое топливо для антиграв-двигателей. Высокая плотность.,5,3.0
Угольные брикеты,Fuel,Спрессованный уголь. Дешёвое топливо.,20,1.0
Газовый баллон,Fuel,Сжатый газ для нагревателей. Взрывоопасен.,3,2.5
Антиграв-камень малый,Antigrav,Маленький левитирующий камень. Подходит для малых платформ.,3,1.0
Антиграв-камень большой,Antigrav,Большой левитирующий камень. Подходит для тяжёлых кораблей.,1,5.0
Стабилизатор поля,Antigrav,Устройство для стабилизации антиграв-поля. Расходник.,5,0.2
Мезий-крошка,Meziy,Маленький осколок мезия. Слабо светится.,20,0.05
Мезий-кристалл,Meziy,Целый кристалл мезия. Источник энергии.,5,0.3
Мезий-сердцевина,Meziy,Чистая сердцевина мезия. Редчайший компонент.,1,1.0
Бинт,Medical,Обычный марлевый бинт. Перевязка ран.,20,0.05
Антисептик,Medical,Дезинфицирующий раствор. Предотвращает заражение.,10,0.1
Стимулятор,Medical,Медицинский стимулятор. Восстанавливает силы.,5,0.1
Батарея,Tech,Стандартная электрическая батарея.,20,0.1
Микросхема,Tech,Печатная плата. Нужна для ремонта техники.,10,0.05
Кабель,Tech,Электрический кабель 1м. Универсальный.,20,0.05
Антигравий (осколок),Antigrav,Осколок антигравия. 100 осколков можно спрессовать в слиток на рынке.,99,0.1
ShipLight ключ,Equipment,Ключ-стержень для запуска корабля. Без него нельзя сесть за штурвал.,1,0.05
ShipMedium ключ,Equipment,Ключ для корабля среднего класса.,1,0.05
ShipHeavy ключ,Equipment,Ключ для тяжёлого корабля.,1,0.05
Зелёный ключ,Equipment,Универсальный зелёный ключ от двери.,1,0.05
Красный ключ,Equipment,Универсальный красный ключ от двери.,1,0.05
Синий ключ,Equipment,Универсальный синий ключ от двери.,1,0.05
ТестовыйStageItem,Resources,Тестовый предмет для проверки pickup'а.,5,1.0
МеднаяРуда (тест),Resources,Тестовая медная руда с другим именем (проверка кросс-валидации).,10,1.0
Железный слиток,Resources,Слиток железа. Результат переплавки.,50,5.0
Медный слиток,Resources,Слиток меди. Результат переплавки.,50,4.0
TestPickaxe,Equipment,Тестовая кирка для майнинга.,1,2.0
# ... ещё 10 строк (PickupItem-* тестовые, ResourceNode_*, и т.д.)
# Полный список — смотри Assets/_Project/Resources/Items/ (47 файлов)

# block=tradeItems
tradeItemId,displayName,basePrice,weight,volume,slots,isDangerous,isFragile,isContraband,requiredFaction
mesium_canister_v01,Мезий (канистра),200,10,0.5,1,y,n,n,None
antigrav_ingot_v01,Слиток антигравия,500,5,0.2,1,n,y,n,None
resource_iron_box,Ящик железной руды,100,50,5,4,n,n,n,None
resource_copper_box,Ящик медной руды,80,40,4,4,n,n,n,None
resource_wood_box,Ящик древесины,40,30,8,4,n,n,n,None

# block=marketItems
# (5 товаров × 2 locationId = 10 строк; некоторые только на primium)
tradeItemId,locationId,basePrice,initialStock,regenPerTick,factionRestriction,allowBuy,allowSell
mesium_canister_v01,primium,250,5,0.01,None,y,n
antigrav_ingot_v01,primium,600,10,0.005,None,y,y
antigrav_ingot_v01,secundus,550,5,0.003,None,y,y
resource_iron_box,primium,120,50,0.02,None,y,y
resource_iron_box,secundus,100,30,0.015,None,y,y
resource_copper_box,primium,90,40,0.02,None,y,y
resource_wood_box,primium,50,40,0.02,None,y,y
antigrav_ingot_v01,tertius,700,3,0.002,FreeTraders,n,y

# block=exchangeRates
tradeItemId,inventoryItemName,warehouseQty,inventoryQty,displayName
resource_iron_box,Железная руда,1,100,Ящик железной руды
resource_copper_box,Медная руда,1,100,Ящик медной руды
resource_wood_box,Древесина,1,100,Ящик древесины
antigrav_ingot_v01,Антигравий (осколок),1,100,Слиток антигравия

# block=recipes
recipeName,displayName,category,craftSeconds,requiredSkillLevel,requiredSkill,ingredient1Name,ingredient1Qty,ingredient2Name,ingredient2Qty,ingredient3Name,ingredient3Qty,output1Name,output1Qty,output2Name,output2Qty
Recipe_IronIngot,Железный слиток,Material,10,0,None,Железная руда,3,,,Железный слиток,1,,
Recipe_CopperIngot,Медный слиток,Material,10,0,None,Медная руда,3,,,Медный слиток,1,,
Recipe_ShipKeyLight,ShipLight ключ,Ship,30,0,None,Железный слиток,1,Медный слиток,1,ShipLight ключ,1,,
Recipe_ShipKeyMedium,ShipMedium ключ,Ship,60,0,None,Железный слиток,2,Микросхема,1,ShipMedium ключ,1,,
Recipe_ShipKeyHeavy,ShipHeavy ключ,Ship,120,0,None,Железный слиток,3,Микросхема,2,Батарея,1,ShipHeavy ключ,1,,
```

**Acceptance:**
- [ ] CSV содержит 47+5+8+4+5 = **69 строк данных** (без заголовков/комментариев).
- [ ] После Export из существующих ассетов — **идентичен** этому файлу (за исключением
  порядка строк и форматирования).

---

## 3. Добавление 1 ресурса (user flow)

Хотим добавить «Медный камень» — новый тип руды, добывается в шахтах, перерабатывается в
ящик на рынке.

**До:** в проекте нет `Медный камень`.

**Действия (в Excel, в `Resources_Import.csv`):**

1. Добавить строку в `# block=inventory`:
   ```
   Медный камень,Resources,Кусок медной породы. Добывается в шахтах.,30,3.0
   ```

2. Добавить строку в `# block=tradeItems` (хотим продавать ящиками):
   ```
   resource_copper_stone_box,Ящик медного камня,150,60,6,4,n,n,n,None
   ```

3. Добавить строку в `# block=marketItems` (на двух рынках):
   ```
   resource_copper_stone_box,primium,180,30,0.02,None,y,y
   resource_copper_stone_box,secundus,150,20,0.015,None,y,y
   ```

4. Добавить строку в `# block=exchangeRates` (pack/unpack):
   ```
   resource_copper_stone_box,Медный камень,1,100,Ящик медного камня
   ```

5. (Опц.) Добавить строку в `# block=recipes` (крафт слитка):
   ```
   Recipe_CopperStoneIngot,Слиток медного камня,Material,15,0,None,Медный камень,5,,,Медный слиток,1,,
   ```

6. Сохранить CSV.

7. `Tools → ProjectC → Resources → CSV Import/Export` → Browse → Preview → Import.

8. Console: "Created 1 ItemData (Медный камень, id 48), Created 1 TradeItem
   (resource_copper_stone_box), Created 2 MarketItem, Created 1 ExchangeRate, Created 1 Recipe.
   Total 6 changes. 0 errors."

9. Проверить файлы:
   - `Resources/Items/Item_Resources_Медный_камень.asset` создан.
   - `Items/Data/ItemRegistry.asset` — `entries[47].id=48, .item=новый`.
   - `Trade/Data/Items/TradeItem_resource_copper_stone_box.asset` создан.
   - `Trade/Data/TradeItemDatabase.asset` — `allItems` = 6 (Mesium, Antigrav, Iron, Copper, Wood, CopperStone).
   - `Trade/Data/Markets/MarketConfig_primium.asset` — `items[]` = +1 (resource_copper_stone_box).
   - `Resources/Exchange/DefaultExchangeRate.asset` — `rates[]` = 5.
   - `Resources/Crafting/Recipes/Recipe_Recipe_CopperStoneIngot.asset` создан.

10. (Опц.) Создать PickupItem в `WorldScene_0_0` с `itemData = Медный камень`.

11. Play Mode → подобрать PickupItem → Inventory → Exchanger tab → Упаковать 100 шт →
    +1 ящик на складе.

---

## 4. Round-trip (Export → no changes → Import)

**Цель:** убедиться, что Export → Import — идемпотентная операция (не создаёт дубликатов,
не теряет данные).

**Шаги (user):**
1. Сделать `git commit` всех текущих ассетов (для отката).
2. `Tools → ProjectC → Resources → CSV Export` → сохранить в `Resources_Import_dump.csv`.
3. **НЕ править** CSV.
4. `Tools → ProjectC → Resources → CSV Import/Export` → Browse → `Resources_Import_dump.csv` → Preview.
5. Ожидаемо: Preview показывает "Updated 0, Created 0, Skipped 0, 0 errors" (потому что
   все ассеты уже существуют, поля совпадают).
6. Click Import → dialog: "0 created, 69 updated, 0 skipped, 0 errors".
7. `git status --short` — никаких изменений в `.asset` (т.к. `SetDirty` без изменений
   не меняет `mtime`).

**Если что-то поменялось** (например, importer перезаписал `description` пустой строкой):
- Проблема: Exporter потерял поле, или Importer применил default вместо сохранения.
- Фикс: проверить, что Exporter правильно сериализует все поля, а Importer не
  перезаписывает пустыми.

---

## 5. Ошибочный CSV (rejected)

**Цель:** показать, как выглядят ошибки валидации.

```csv
# Project C: The Clouds — Resources Database (error examples)

# block=inventory
itemName,itemType,description,maxStack,weightKg
,Resources,Без имени — должно быть ошибкой.,10,1.0
Ботинки,Apparel,Несуществующий itemType 'Apparel'.,1,0.5
РесурсСМаксом,Resources,Отрицательный maxStack.,-5,1.0
РесурсСНулём,Resources,Нулевой weightKg.,10,0

# block=tradeItems
tradeItemId,displayName,basePrice,weight,volume,slots,isDangerous,isFragile,isContraband,requiredFaction
iron_box_v01,Ящик железа,-100,50,5,4,n,n,n,None
# Отрицательная цена — должно быть ошибкой.
iron_box_v01,Дубликат,100,50,5,4,n,n,n,None
# Дубликат tradeItemId в той же секции — global error.

# block=marketItems
tradeItemId,locationId,basePrice,initialStock,regenPerTick,factionRestriction,allowBuy,allowSell
unknown_box,primium,100,10,0.01,None,y,y
# unknown_box нет в tradeItems — cross-validate error.
iron_box_v01,primium,100,10,0.01,UnknownFaction,y,y
# UnknownFaction нет в enum Faction.

# block=exchangeRates
tradeItemId,inventoryItemName,warehouseQty,inventoryQty,displayName
iron_box_v01,Железная руда,1,100,Ящик железа
# ОК — но если в inventory нет "Железная руда" — cross-validate error.
iron_box_v01,НесуществующийПредмет,1,100,Ошибка
# inventoryItemName не существует — cross-validate error.

# block=recipes
recipeName,displayName,category,craftSeconds,requiredSkillLevel,requiredSkill,ingredient1Name,ingredient1Qty,ingredient2Name,ingredient2Qty,output1Name,output1Qty
,Без имени,Material,10,0,None,Железная руда,3,,,Железный слиток,1
# Пустой recipeName — required.
Recipe_Broken,Битый рецепт,Material,10,0,None,НесуществующийИнгредиент,1,,,Железный слиток,1
# ingredient1Name не существует — cross-validate error.
Recipe_Weird,Странный рецепт,InvalidCategory,10,0,None,Железная руда,3,,,Железный слиток,1
# InvalidCategory нет в enum RecipeCategory.
```

**Ожидаемые ошибки (Preview):**

| Line | Block | Severity | Error |
|------|-------|----------|-------|
| 6 | inventory | error | 'itemName' is required |
| 7 | inventory | error | 'itemType' = 'Apparel' is not valid ItemType |
| 8 | inventory | error | 'maxStack' = '-5' is not int (or out of range) |
| 15 | tradeItems | error | 'basePrice' = '-100' must be >= 0 |
| 16 | tradeItems | global | duplicate tradeItemId 'iron_box_v01' |
| 21 | marketItems | error | 'tradeItemId' = 'unknown_box' not in tradeItems |
| 22 | marketItems | error | 'factionRestriction' = 'UnknownFaction' is not valid Faction |
| 28 | exchangeRates | error | 'inventoryItemName' = 'НесуществующийПредмет' not in inventory |
| 32 | recipes | error | 'recipeName' is required |
| 33 | recipes | error | 'ingredient1Name' = 'НесуществующийИнгредиент' not in inventory |
| 34 | recipes | error | 'category' = 'InvalidCategory' is not valid RecipeCategory |

**Behavior при Import:**
- Per-row errors → строка skipped, остальные обрабатываются.
- Global errors (дубликаты, missing required column) → импорт блокируется.
- Диалог: "Skipped 11 rows. 0 created, 0 updated. 11 errors."

**Что проверить:** что при `Skipped > 50%` кнопка Import **disabled** (heuristic —
«слишком много ошибок, что-то фундаментально не так»).

---

## 6. Quick reference — текущее состояние (июнь 2026)

Из существующих ассетов (на 2026-06-11):

| Категория | Сейчас | После импорта | Расхождения |
|-----------|--------|---------------|-------------|
| `ItemData` (ассеты в `Resources/Items/`) | 47 | 47 | — |
| `ItemRegistry.entries` (id 1..N) | 32 | 47 | **15 пропущено** (ключи, рецепты, тест. заглушки) |
| `TradeItemDefinition` (в `Trade/Data/Items/`) | 2 | 5 | **3 пропущено** (iron/copper/wood box) |
| `TradeItemDatabase.allItems` | 2 | 5 | **3 пропущено** |
| `MarketConfig` (locationId) | 4 (primium/secundus/tertius/quartus) | 4 | OK |
| `MarketConfig[locationId].items[]` | 7 (Primium=2, Secundus=2, Tertius=1, Quartus=2) | 8 | **1 отсутствует** (resource_wood_box — хотим добавить) |
| `ExchangeRateConfig.rates` | 4 | 4 | OK |
| `RecipeData` (в `Resources/Crafting/Recipes/`) | 5 (3 активных + 2 тест.) | 5 | OK (названия немного не совпадают, см. `02_DESIGN.md`) |
| `CraftingStationConfig._allowedRecipes[]` | 3 (CraftingTable) | 3 | OK (T-IE05 не обновляет stations) |

**Главный вывод:** основной gap — `ItemRegistry` (15 пропущено) и `TradeItemDatabase`
(3 пропущено). После импорта эти SO-контейнеры будут полными, и `InventoryWorld.GetOrRegisterItemId`
перестанет авто-добавлять с дублями.

---

## 7. Acceptance-сценарии (по тикетам)

### T-IE01 (Schema + Parser)

```powershell
# 1. Скопировать `Resources_Import_minimal.csv` (пример 1) в
#    Assets/_Project/Resources/_docs/Resources_Import.csv

# 2. Открыть Unity, Console: 0 errors

# 3. Tools → ProjectC → Resources → CSV Parser Test (временный menu, удалить после T-IE01)

# 4. Console:
#    [ResourcesCsvParser] Parsed 5 blocks: inventory=1, tradeItems=1, marketItems=1, exchangeRates=1, recipes=1
#    [ResourcesCsvParser] 0 errors
```

### T-IE02 (Cross-Validator)

```powershell
# 1. Добавить в CSV строку с опечаткой (пример 5, строки 6, 7, ...)

# 2. Tools → ProjectC → Resources → CSV Validate (временный menu)

# 3. Console:
#    [ResourcesCsvCrossValidator] Row 6: 'itemName' is required
#    [ResourcesCsvCrossValidator] Row 7: 'itemType' = 'Apparel' is not valid ItemType
#    ...
```

### T-IE03 (Inventory Import)

```powershell
# 1. Backup ItemRegistry.asset (cp .bak)
# 2. Запустить Tools → ProjectC → Resources → CSV Apply → inventory
# 3. Console:
#    [ResourcesCsvImporter] inventory: created 15, updated 32, skipped 0
# 4. ItemRegistry.asset теперь содержит 47 entries (id 1..47)
# 5. Play Mode → PickupItem с itemData "Антигравий (осколок)" → подбирается
```

### T-IE04 (Trade/Exchange Import)

```powershell
# 1. Backup TradeItemDatabase.asset, ExchangeRateConfig.asset
# 2. Запустить Tools → ProjectC → Resources → CSV Apply → all
# 3. Console:
#    [ResourcesCsvImporter] inventory: 15 created, 32 updated
#    [ResourcesCsvImporter] tradeItems: 3 created, 2 updated
#    [ResourcesCsvImporter] marketItems: 6 created, 2 updated
#    [ResourcesCsvImporter] exchangeRates: 0 created, 4 updated
#    [ResourcesCsvImporter] recipes: 0 created, 5 updated
#    Total: 24 created, 45 updated. 0 errors.
# 4. Play Mode → MarketZone → Exchanger tab → 4 ящика видны
# 5. Pack 100 осколков антигравия → +1 ящик. Unpack → -1 ящик, +100 осколков.
```

### T-IE05 (Recipes Import)

```powershell
# 1. Запустить Tools → ProjectC → Resources → CSV Apply → all
# 2. Console: recipes: 0 created, 5 updated.
# 3. Play Mode → подойти к CraftingTable → открыть UI → видит 3 рецепта (Iron, Copper, ShipKeyLight)
# 4. Запустить крафт "Железный слиток" — 3 руды → 1 слиток
```

### T-IE06 (Export)

```powershell
# 1. Tools → ProjectC → Resources → CSV Export → /tmp/dump.csv
# 2. Открыть dump.csv в Excel — 5 секций, ~69 строк
# 3. Сравнить с исходным CSV — порядок строк может отличаться, данные идентичны
```

### T-IE07 (Editor Window)

```powershell
# 1. Tools → ProjectC → Resources → CSV Import/Export → окно
# 2. Browse → выбрать CSV → Preview → ListView с ~69 строками
# 3. Errors column показывает ошибки (если есть)
# 4. Status: "Ready / 0 errors / 0 warnings"
# 5. Import → dialog с итогами
# 6. Export → save dialog → файл сохранён
```

### T-IE08 (Smoke Test)

```powershell
# 1. Запустить полный Apply на эталонном CSV
# 2. Все 12 acceptance из §12 01_ANALYSIS.md выполнены
# 3. Play Mode smoke:
#    - Start Host
#    - F на PickupItem с "Железная руда" → подобрана (id=1)
#    - F на PickupItem с "Антигравий (осколок)" → подобрана (id=26)
#    - F на MarketZone → MarketWindow → Exchanger tab
#    - Pack 100 железной руды → +1 iron_box
#    - Unpack 1 iron_box → +100 железной руды
#    - Pack 100 антигравий осколков → +1 antigrav_ingot
#    - Unpack 1 antigrav_ingot → +100 осколков
#    - Закрыть MarketWindow → всё работает
```

---

## 8. Что НЕ покрыто примерами (Phase 2)

- ❌ Иконки (Sprite) — отдельный пайплайн.
- ❌ LootTable, ResourceNodeConfig — out of scope (см. `01_ANALYSIS.md` §6).
- ❌ CraftingStationConfig._allowedRecipes — обновление вручную.
- ❌ Multiple-location ExchangeRateConfig (T-E01 follow-up).
- ❌ Per-recipe station mapping (CSV-колонка `recipeStation`).
- ❌ Импорт старых данных в новый CSV (миграция).

---

## 9. Шпаргалка по ID-пространствам

| ID-тип | Где | Пример | Кто его знает |
|--------|-----|--------|---------------|
| `int id` | `ItemRegistry.entries[].id` | 1..47 | `InventoryWorld._itemDatabase` (key) |
| `string itemName` | `ItemData.itemName` | "Железная руда" | UI, `QuestWorld.TryGetIdByName`, `ExchangeRateEntry.inventoryItemName` |
| `string itemId` | `TradeItemDefinition.itemId` | "antigrav_ingot_v01" | `TradeWorld.Resolver`, `MarketItemConfig.itemId`, `Warehouse` |
| `string locationId` | `MarketConfig.locationId` | "primium" | `MarketZoneRegistry` |
| `string pairKey` | `ExchangeRateConfig.rates[]` | (warehouseItemId, inventoryItemName) | `ExchangeServer` |
| `string recipeName` | `RecipeData.assetPath` (имя файла) | "Recipe_IronIngot" | `CraftingStationConfig._allowedRecipes[]` (GUID-ссылки) |
| `GUID` | `.asset.meta` | 380bf68bc67431b41b3e838e908f9eef | `LootTable.entries[].item`, `RecipeData._ingredients[].item` |

**При импорте CSV:**
- Новые `ItemData` → новый `id` = `max(existing) + 1` (auto-increment).
- Новые `TradeItemDefinition` → берём `tradeItemId` из CSV (стабильный, versioned).
- Существующие ассеты → обновляем поля через `SetDirty`, GUID не меняется.

---

Дальше: вернуться к [`README.md`](README.md) для индекс-обзора.
