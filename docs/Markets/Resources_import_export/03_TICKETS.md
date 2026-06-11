# Resources Import/Export — План тикетов

> **Дата:** 2026-06-11
> **Статус:** Анализ (код не написан)
> **Прецедент:** `Resources_exchanger` T-E01..T-E05 (5 тикетов, ~1.5 сессии).
> **Цель:** декомпозиция CSV-импорта на тикеты с чёткими границами и проверками.

---

## TL;DR

7 тикетов, оценочно 3-4 сессии. Каждый тикет — компилируемое улучшение. **User проверяет
сам в Editor, тесты не запускаем** (см. AGENTS.md / `project-c-bootstrap` "Mavis
invokes MCP").

| ID | Название | Сложность | Зависит от |
|----|----------|-----------|------------|
| **T-IE01** | CSV Schema + Parser | M | — |
| **T-IE02** | Cross-Validator | S | T-IE01 |
| **T-IE03** | Importer (inventory + ItemRegistry) | M | T-IE01, T-IE02 |
| **T-IE04** | Importer (tradeItems + marketItems + exchangeRates) | M | T-IE03 |
| **T-IE05** | Exporter | S | T-IE03, T-IE04 |
| **T-IE06** | Editor Window | M | T-IE01..T-IE05 |
| **T-IE07** | Sample CSV + smoke test (антигравий) | S | T-IE06 |
| **⏭️ Phase 2** | Recipes / CraftingStation — отдельная итерация | L | T-IE01..T-IE07 |

> **Out of scope MVP (Phase 2):** импорт `RecipeData` и обновление
> `CraftingStationConfig._allowedRecipes[]`. Своя схема (semicolon-ingredient list или
> фиксированные колонки), ссылается на `itemName` (из Phase 1) и `stationType`. См.
> `Crafting_system/` roadmap.
>
> Парсер **распознаёт** секцию `recipes` (skip + warning), importer не обрабатывает.
> В CSV можно держать `recipes` для Phase 2, не убирать.

После T-IE07: импортёр работает end-to-end для 4 из 5 подсистем, ItemRegistry полон,
антигравий осколок регистрируется, можно добавлять новые ресурсы через CSV.

---

## T-IE01 — CSV Schema + Parser

**Цель:** определить схему всех 5 секций + парсер CSV (без импорта).

**Файлы (новые):**
- `Assets/_Project/Items/Editor/ResourcesCsvSchema.cs` (~250 LOC)
  - Класс `ColumnDef` (копия из `QuestCsvSchema`).
  - `BlockSchema` — набор колонок для одной секции (`BlockInventory`, `BlockTradeItems`,
    `BlockMarketItems`, `BlockExchangeRates`, `BlockRecipes`).
  - `ResourcesCsvSchema.GetBlockSchema(string blockName) → BlockSchema`.
  - `ResourcesCsvRow` (копия, но с `string block`).
  - Утилиты: `ResolveColumnName`, `IsBool`, `EscapeCsv`, `QuoteCsv`.
- `Assets/_Project/Items/Editor/ResourcesCsvParser.cs` (~200 LOC)
  - `ParseFile(string path) → (blocks, globalErrors)`.
  - `SplitCsvLines` + `ParseCsvLine` — **копия** из `QuestCsvParser`
    (можно вынести в общий utility `CsvUtils`, но это отдельный рефакторинг — не делаем).
  - `ApplyDefaults` — пустые ячейки заполняются `ColumnDef.defaultValue`.
  - `ValidateRow` (тип/required/range).

**Тест (user):**
1. Открыть Unity, Console: 0 errors.
2. Создать `Assets/_Project/Resources/_docs/Resources_Import.csv` вручную (или скопировать
   из `04_EXAMPLES.md`).
3. Запустить из Editor (через временный `[MenuItem]` или `EditorApplication.ExecuteMenuItem` —
   нет, лучше добавить `[MenuItem("Tools/ProjectC/Resources/CSV Parser Test")]` в
   `ResourcesCsvParser` на время T-IE01).
4. Console должен показать: "Parsed: inventory=47, tradeItems=5, marketItems=8, exchangeRates=4,
   recipes=5. 0 errors."

**Что НЕ делаем:** импорт в SO, валидацию между секциями (это T-IE02).

---

## T-IE02 — Cross-Validator

**Цель:** проверить кросс-ссылки между секциями до импорта.

**Файлы (новые):**
- `Assets/_Project/Items/Editor/ResourcesCsvCrossValidator.cs` (~150 LOC)
  - `Validate(blocks, globalErrors)` — собирает все per-row errors и global errors.
  - Проверки: §3.4 `02_DESIGN.md` (уникальность itemName, существование tradeItemId
    в marketItems/exchangeRates/recipes, и т.д.).

**Тест (user):**
1. В CSV добавить строку: `BogusItem,Resources,...,...` (нет такого) — в `# block=recipes`
   `ingredient1Name=BogusItem`.
2. Запустить парсер + cross-validator.
3. Console: "Row 6 (recipes): ingredient1Name 'BogusItem' not in inventory".
4. Убрать строку, добавить дубликат: `Железная руда,Resources,...,...` (уже есть).
5. Console: "inventory: duplicate itemName 'Железная руда'".

**Что НЕ делаем:** окно, импорт — T-IE07 / T-IE03+.

---

## T-IE03 — Importer (inventory + ItemRegistry)

**Цель:** импорт секции `inventory` в `ItemData` + `ItemRegistry.asset`.

**Файлы (изменения):**
- `Assets/_Project/Items/Editor/ResourcesCsvImporter.cs` (новый, ~250 LOC)
  - `ImportResult` (created, updated, skipped, errors, warnings).
  - `Apply(blocks, result)` — entry point, вызывает ProcessXxx.
  - `ProcessInventory(rows, result)` — детально в `02_DESIGN.md` §3.5.
  - `FindItemDataByName(string name) → ItemData` — `AssetDatabase.FindAssets` + filter.
  - `EnsureFolder(string path)`, `EnsureUniqueAssetPath(ref path)`, `SanitizeFileName`.
  - `GetInt/GetFloat/GetBool` extensions для `ResourcesCsvRow`.

**Тест (user):**
1. Удалить `Item_Antigrav_Антигравий_осколок.asset` (резервная копия!).
2. Запустить `Tools → ProjectC → Resources → CSV Apply (inventory only)`.
3. Console: "Created 1 ItemData (Антигравий (осколок), id 33)".
4. Проверить: `Resources/Items/Item_Antigrav_Антигравий_осколок.asset` создан,
   `ItemRegistry.asset` содержит 33 записи (id 1..33), `entries[32].item` = новый ассет.
5. Play Mode → PickupItem с этим itemData подбирается без NRE.

**Что НЕ делаем:** tradeItems, marketItems, exchangeRates — T-IE04.
**Что НЕ делаем:** recipes — Phase 2 (out of scope MVP).

---

## T-IE04 — Importer (tradeItems + marketItems + exchangeRates)

**Цель:** импорт оставшихся подсистем trade/exchange.

**Файлы (изменения):**
- `Assets/_Project/Items/Editor/ResourcesCsvImporter.cs` (расширение, +300 LOC)
  - `ProcessTradeItems(rows, result)` — `TradeItemDefinition` + `TradeItemDatabase.allItems`.
  - `ProcessMarketItems(rows, result)` — `MarketConfig_{locationId}.asset` (создать если нет).
  - `ProcessExchangeRates(rows, result)` — `ExchangeRateConfig.rates`.
  - `FindTradeItemById`, `FindMarketConfig`, `GetOrCreateMarketConfig`.

**Тест (user):**
1. Запустить `Tools → ProjectC → Resources → CSV Apply (all)`.
2. Console: "Updated 0 / Created 5 TradeItem, 0 / Created 8 MarketItem, 0 / Updated 4
   ExchangeRate, 0 / Created 0 ItemData. Total 17 changes."
3. Проверить:
   - `Trade/Data/Items/TradeItem_*.asset` — 5 файлов.
   - `Trade/Data/TradeItemDatabase.asset` — `allItems` = 5.
   - `Trade/Data/Markets/MarketConfig_primium.asset`, `MarketConfig_secundus.asset` — созданы.
   - `Resources/Exchange/DefaultExchangeRate.asset` — 4 rates.
4. Play Mode → MarketZone → Exchanger tab → видит 4 ящика (resource_iron_box, etc).

**Что НЕ делаем:** recipes — T-IE05.

---

## T-IE05 — Exporter

**Цель:** обратная операция — собрать CSV из всех SO (4 блока: inventory, tradeItems, marketItems, exchangeRates).

**Файлы (новые):**
- `Assets/_Project/Items/Editor/ResourcesCsvExporter.cs` (~200 LOC)
  - `Export(string csvPath) → int rowsWritten`.
  - `InventoryBlock` — `Resources.FindObjectsOfTypeAll<ItemData>()` filtered to
    `Resources/Items/`, sort by name.
  - `TradeItemsBlock` — `Resources.FindObjectsOfTypeAll<TradeItemDefinition>()` filtered
    to `Trade/Data/Items/`, sort by itemId.
  - `MarketItemsBlock` — `Resources.FindObjectsOfTypeAll<MarketConfig>()`,
    flatten `items[]` per locationId.
  - `ExchangeRatesBlock` — `Resources.Load<ExchangeRateConfig>("Exchange/DefaultExchangeRate")`.
  - **Recipes block не пишется** (Phase 2 — отдельный `CraftingCsvExporter`).

**Тест (user):**
1. `Tools → ProjectC → Resources → CSV Export` → сохранить в `Resources_Import_dump.csv`.
2. Открыть в Excel — **4 секции** (без recipes), ~55 строк.
3. Сравнить с оригинальным CSV (если был) — должны совпадать (за исключением BOM).

---

## T-IE06 — Editor Window

**Цель:** UI окно с Preview + кнопками Import/Export.

**Файлы (новые):**
- `Assets/_Project/Items/Editor/ResourcesCsvWindow.cs` (~280 LOC)
  - `[MenuItem("Tools/ProjectC/Resources/CSV Import/Export", priority = 200)]`.
  - `OnEnable` → `BuildUI()` — UI Toolkit (копия `QuestCsvWindow`).
  - **Browse** button → `EditorUtility.OpenFilePanel`.
  - **Preview** button → парсит CSV, заполняет ListView.
  - **Import** button → вызывает `ResourcesCsvImporter.Apply(blocks)`.
  - **Export** button → `EditorUtility.SaveFilePanel` + `ResourcesCsvExporter.Export`.
  - ListView с колонками: Block, Line, Key, Name, Issues.
  - Status bar: "X created, Y updated, Z skipped, N errors, M warnings".
  - **Recipes warning**: если в CSV есть секция `recipes` — показать warning "skipped (Phase 2)".

**Тест (user):**
1. `Tools → ProjectC → Resources → CSV Import/Export` → окно открывается.
2. Browse → выбрать `Resources_Import.csv` → Preview → ListView заполнен, 0 errors.
3. Import → dialog: "Created 1, Updated 0, Skipped 0, 0 errors".
4. Export → сохранить в `dump.csv` → проверить в Excel.

---

## T-IE07 — Sample CSV + Smoke Test (антигравий)

**Цель:** зафиксировать эталонный CSV и проверить end-to-end на реальных данных.

**Файлы (новые):**
- `Assets/_Project/Resources/_docs/Resources_Import.csv` — собранный из текущих 47 ItemData +
  5 TradeItem + 7 MarketItem + 4 ExchangeRate (см. `04_EXAMPLES.md`). **Без recipes** (Phase 2).
- `Assets/_Project/Resources/_docs/Resources_Import_Schema.md` — короткая справка
  (что есть в CSV, как пользоваться).

**Действия:**
1. Сгенерировать `Resources_Import.csv` из текущих ассетов (T-IE05 — Exporter).
2. Добавить антгравий-осколок (itemName="Антигравий (осколок)") в `inventory` (он уже есть,
   но в `ItemRegistry` не зарегистрирован).
3. Добавить `antigrav_ingot_v01` в `tradeItems` (уже есть `TradeItem_Antigrav_v01.asset`),
   но в `TradeItemDatabase` его нет (проверить).
4. Добавить `resource_iron_box`, `resource_copper_box`, `resource_wood_box` в `tradeItems` —
   сейчас их нет (только Mesium + Antigrav в `TradeItemDatabase.asset`).
5. Добавить 4 `exchangeRates` (все уже есть в `DefaultExchangeRate.asset`, копируем).
6. **Сделать backup всех `.asset`** в `Assets/_Project/Resources/Items/` и
   `Assets/_Project/Trade/Data/Items/` (на случай отката).
7. Запустить Import → проверить acceptance из §12 `01_ANALYSIS.md`.

**Acceptance:**
- [ ] `ItemRegistry.asset` содержит **все** ItemData (id 1..N).
- [ ] `TradeItemDatabase.asset` содержит **все** TradeItemDefinition.
- [ ] Антгравий осколок Pack 100 → +1 ingot, Unpack 1 → +100 осколков.
- [ ] Iron/Copper/Wood Pack 100 → +1 box, обратно.
- [ ] Все 5 рецептов работают в CraftingStation.
- [ ] MarketWindow показывает 5 товаров (Mesium, Antigrav, Iron box, Copper box, Wood box).
- [ ] Unity Editor: 0 errors в Console после реимпорта.

---

## Зависимости между тикетами

```
T-IE01 ──┬──> T-IE02 ──┐
         │            │
         └──> T-IE03 ──┴──> T-IE04 ──┐
                       │              │
                       └──> T-IE05 ───┤
                                      │
                        T-IE06 ────────┤
                                      │
                        T-IE07 ◀──────┘

⏭️ Phase 2 (Recipes):
  T-IE01..T-IE07 ──> CraftingCsvSchema ──> CraftingCsvImporter ──> CraftingCsvExporter
```

**T-IE02 можно параллелить с T-IE03** (cross-validator не зависит от импорта, но
включается в UI только в T-IE06).

---

## Оценка времени (по опыту T-E01..T-E05)

| Тикет | Сессий | LOC | Файлов |
|-------|--------|-----|--------|
| T-IE01 | 1 | ~200 | 2 (Schema + Parser) |
| T-IE02 | 0.5 (вместе с T-IE01) | ~110 | 1 (CrossValidator) |
| T-IE03 | 1 | ~150 | 1 (Importer: inventory) |
| T-IE04 | 1 | ~200 | 0 (расширение Importer) |
| T-IE05 | 0.5 | ~200 | 1 (Exporter) |
| T-IE06 | 1 | ~280 | 1 (Window) |
| T-IE07 | 0.5 (с user validation) | ~50 | 2 (CSV sample + Schema doc) |
| **ИТОГО** | **~3.5 сессии** | **~1190 LOC** | **~8 файлов** |

Первая сессия может объединить T-IE01 + T-IE02 (как в Exchanger roadmap).
Финальная сессия (T-IE07) — пользовательская валидация на реальных данных.

⏭️ **Phase 2 (Recipes / CraftingStation):** ~3-4 сессии дополнительно, ~400 LOC.
Отдельный roadmap, начинается после принятия MVP-импорта.

---

## Сравнение с Exchanger roadmap

| Аспект | Exchanger (T-E01..T-E05) | Resources I/E (T-IE01..T-IE07 + Phase 2) |
|--------|--------------------------|---------------------------------|
| Тикетов | 5 | 7 (MVP) + Phase 2 (Recipes) |
| Сессий | ~1.5 | ~3.5 (MVP) |
| Паттерн | POCO + NetworkBehaviour + Tab | Editor-only (POCO + EditorWindow) |
| Runtime-влияние | Да (ExchangeServer, ExchangeWorld) | Нет (только Editor) |
| Меняет runtime-классы | Нет (zero-touch) | Нет (только `ItemRegistry` обновляется, что и так уже было) |
| Артефакты пользователя | — | 1 CSV + 1 schema doc |
| Acceptance | Manual в Play Mode | Manual в Editor + 1 Play Mode smoke |

---

## Что НЕ делаем (границы)

- ❌ Импорт LootTable, ResourceNodeConfig, NPC, Faction — отдельные подсистемы с собственной БД.
  В `01_ANALYSIS.md` §6.7 упомянуто как out of scope.
- ❌ Backup .asset файлов перед импортом — пользователь делает сам через git.
- ❌ Web sync, БД, JSON — Stage 3+.
- ❌ Сложные pack/unpack с комиссией — flat rate.
- ❌ Multiple .csv merge — Phase 2.
- ❌ Иконки (Sprite) — отдельный пайплайн.
- ❌ Поехавшие GUID-ы при пересоздании ассетов — решаем через `SetDirty` вместо `CreateAsset`.

---

## Что МОЖЕТ пойти не так (риски, обсудить с пользователем до T-IE01)

1. **Конфликт `Resources/Items/` и `Items/` (где лежит `ItemRegistry.asset`):**
   - `Resources/Items/Item_*.asset` — 47 файлов ItemData.
   - `Items/Data/ItemRegistry.asset` — список.
   - `Items/Editor/*.cs` — скрипты.
   - `Items/Core/*.cs` — runtime.
   - `Items/Network/*.cs` — server.
   - **Всё корректно**, но в Editor скрипте надо делать `using ProjectC.Items;` —
     конфликт имён `Items/Editor` ↔ `Items` (folder) — **решаемо**, проверим при
     имплементации.
2. **MarketConfig.asset в неожиданном месте:** я не нашёл существующих
   `MarketConfig_primium.asset` в `Trade/Data/Markets/`. Возможно, они лежат в
   `Resources/Trade/Markets/` или ещё где. **Перед T-IE04** — `find` по проекту.
3. **ItemRegistry.asset может быть под git-lfs или read-only** — проверить права.
4. **CSV в Excel портит кириллицу** — митигация: UTF-8 BOM, тест открытия в Excel.
5. **.meta у существующих ассетов не должен меняться** — если importer пересоздаёт ассет
   (например, `AssetDatabase.DeleteAsset` + `CreateAsset`), GUID сменится ⇒ сломаются
   LootTable, Recipe. **Решено:** НЕ удалять, только обновлять поля через `SetDirty`.
6. **`Station_*.asset._allowedRecipes`** — обновлять ли? Если да — пользователь должен
   явно указать в CSV колонку `recipeStation`. Сейчас **не делаем**.

---

Дальше: [`04_EXAMPLES.md`](04_EXAMPLES.md) — конкретные примеры CSV-файлов и acceptance-сценариев.
