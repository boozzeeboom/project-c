# Prune Feature — T-IE08 (Pruning)

> **Прецедент:** [ResourcesCsvImporter §3.5 Apply](../01_ANALYSIS.md) + фича "вычистить устаревшие предметы перед импортом нового сезона".
> **Статус:** T-IE08 (MVP, добавляется перед релизом).
> **Цель:** безопасное удаление `ItemData`/`TradeItemDefinition`/`MarketItemConfig`/`ExchangeRateEntry`, которые **отсутствуют** в CSV, **либо** полная замена всех данных из CSV.

---

## TL;DR

Добавляем новую секцию в CSV (опц.):
```
# block=prune
mode,applyTo
orphan,all
```

Возможные значения `mode`:
- **`none`** (default) — backwards-compat. Ничего не удаляется. Только apply CSV (текущее поведение).
- **`orphan`** — удаляет ассеты/записи, которые **отсутствуют в CSV**, но **только если на них НЕТ references** в сценах/других SO.
- **`replace`** — удаляет **вСЕ** ассеты/записи в указанных `applyTo` секциях, потом apply CSV.

Возможные значения `applyTo`:
- **`all`** (default) — применить ко всем 4 секциям (inventory, tradeItems, marketItems, exchangeRates).
- **`inventory`** — только ItemData + ItemRegistry.
- **`tradeItems`** — только TradeItemDefinition + TradeItemDatabase.
- **`marketItems`** — только MarketConfig.items[].
- **`exchangeRates`** — только ExchangeRateConfig.rates[].
- Можно комбинировать через запятую: `inventory,tradeItems`.

---

## 1. Аудит — что и где

### 1.1 Куда importer пишет (то, что prune удаляет)

| CSV секция | Файл/SO | Где лежит |
|------------|---------|-----------|
| `inventory` | `ItemData` (1 файл на предмет) | `Assets/_Project/Resources/Items/Item_*.asset` |
| | `ItemRegistry` (id→item) | `Assets/_Project/Items/Data/ItemRegistry.asset` (entries[]) |
| `tradeItems` | `TradeItemDefinition` (1 файл на item) | `Assets/_Project/Trade/Data/Items/TradeItem_*.asset` |
| | `TradeItemDatabase` | `Assets/_Project/Trade/Data/TradeItemDatabase.asset` (allItems[]) |
| `marketItems` | `MarketItemConfig` (inline) | `Assets/_Project/Trade/Data/Markets/MarketConfig_*.asset` (items[]) |
| `exchangeRates` | `ExchangeRateEntry` (inline) | `Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset` (rates[]) |

### 1.2 References — где живут ссылки (blacklist source)

| Ссылается на | Где | Доступен importer'у? |
|--------------|-----|----------------------|
| `ItemData` | `PickupItem.itemData` (scene) | ✅ через `FindAssets("t:Prefab")` + `PrefabUtility.LoadPrefabContents` |
| | `LootTable.entries[].item` + `guaranteedItems[]` | ✅ через `FindAssets("t:LootTable")` |
| | `NetworkChestContainer` → `LootTable` | ✅ то же |
| | `RecipeData._ingredients[].item` + `_outputs[].item` | ✅ через `FindAssets("t:RecipeData")` |
| | `MarketItemConfig.definition` (4 MarketConfig_*.asset) | ✅ через `FindAssets("t:MarketConfig")` |
| | `ResourceNodeConfig._resultItem` + `_requiredTool` | ✅ через `FindAssets("t:ResourceNodeConfig")` |
| | **Saved `InventoryData`** (player inventories) | ❌ `Application.persistentDataPath/...` (вне Assets/) |
| `TradeItemDefinition` | `MarketItemConfig.definition` (см. выше) | ✅ |
| | **Saved `Warehouse`** (per-player per-location) | ❌ `persistentDataPath` |
| | **Saved `CargoData`** (per-ship) | ❌ `persistentDataPath` |
| | **Saved `ContractData`** | ❌ `persistentDataPath` |
| | `CraftingStationConfig` (через `RecipeData.definition`?) | нет, не ссылается |

### 1.3 Что НЕ сканируется (warning, не blocker)

**Saved runtime state** (вне `Assets/`):
- `JsonInventoryRepository` (player inventories) — `Application.persistentDataPath/...`
- `ServerFileRepository` (warehouse, cargo, contracts) — `Application.persistentDataPath/ServerData/...`

→ Если `mode=orphan` и удаляем ItemData, у которого id есть в `InventoryData` JSON, **savegame сломается**. Importer выдаст **warning** со списком id, которые могут быть затронуты (на основе ItemRegistry.asset — который сканируем). **Не блокирует** — пользователь сам решает.

---

## 2. Алгоритм PruneEngine

### 2.1 `mode=none` (default, backward-compat)

Ничего не делает. Только Apply CSV как сейчас. Identical behavior.

### 2.2 `mode=orphan`

```python
def prune_orphan(applyTo_blocks, csv_blocks):
    # 1. Build whitelist from CSV (what we WANT to keep).
    csv_inventory_names = set(csv_blocks['inventory'].itemName) if 'inventory' in csv_blocks else None
    csv_tradeItem_ids = set(csv_blocks['tradeItems'].tradeItemId) if 'tradeItems' in csv_blocks else None
    csv_market_pairs = set((m.tradeItemId, m.locationId) for m in csv_blocks['marketItems']) if 'marketItems' in csv_blocks else None
    csv_exchange_pairs = set((e.tradeItemId, e.inventoryItemName) for e in csv_blocks['exchangeRates']) if 'exchangeRates' in csv_blocks else None

    # 2. Build blacklist from references.
    blacklist_inventory = scan_referenced_item_names()  # PickupItem, LootTable, RecipeData, ResourceNode, MarketConfig
    blacklist_tradeItem = scan_referenced_trade_item_ids()  # MarketItemConfig
    # Note: saved runtime state NOT included (see §1.3).

    # 3. For each asset/entry, decide KEEP vs DELETE.
    to_delete = {'inventory': [], 'tradeItems': [], 'marketItems': [], 'exchangeRates': []}
    blocked = {'inventory': [], 'tradeItems': [], 'marketItems': [], 'exchangeRates': []}

    # inventory (ItemData + ItemRegistry entries)
    if 'inventory' in applyTo_blocks:
        for item in Resources/Items/:
            if item.itemName not in csv_inventory_names:
                if item.itemName in blacklist_inventory:
                    blocked['inventory'].append((item, 'has references in scenes/SOs'))
                else:
                    to_delete['inventory'].append(item)

    # tradeItems
    if 'tradeItems' in applyTo_blocks:
        for t in TradeItemDatabase.allItems + orphan scan:
            if t.itemId not in csv_tradeItem_ids:
                if t.itemId in blacklist_tradeItem:
                    blocked['tradeItems'].append((t, 'MarketItemConfig.definition'))
                else:
                    to_delete['tradeItems'].append(t)

    # marketItems
    if 'marketItems' in applyTo_blocks:
        for mc in MarketConfig_*.asset:
            for mic in mc.items:
                if (mic.itemId, mc.locationId) not in csv_market_pairs:
                    to_delete['marketItems'].append(mic)  # no blacklist — pure UI data

    # exchangeRates
    if 'exchangeRates' in applyTo_blocks:
        for rate in DefaultExchangeRate.rates:
            if (rate.warehouseItemId, rate.inventoryItemName) not in csv_exchange_pairs:
                to_delete['exchangeRates'].append(rate)  # no blacklist

    return to_delete, blocked
```

### 2.3 `mode=replace`

**Идентично `orphan`**, НО `blacklist` игнорируется — всё, чего нет в CSV, удаляется.

Перед удалением:
- **Двойной confirm dialog**:
  1. "Будет удалено N предметов. Продолжить?" [Yes/No]
  2. "Это действие необратимо. Continue?" [Yes/No]

### 2.4 Безопасность (Pitfall #3)

- Перед удалением **диалог подтверждения** со списком:
  ```
  PRUNE PREVIEW
  ─────────────────
  Will delete (safe, no references):
    inventory: 3
      • Говно (Resources/Items/Item_Resources_Говно.asset)
      • ТестовыйКамень
      • ItemRegistryEntry id=37
    tradeItems: 0
    marketItems: 2
      • MarketConfig_Primium: «resource_wood_box»
    exchangeRates: 0

  Blocked (has references — will NOT be touched):
    inventory: 0
    tradeItems: 0
    marketItems: 0
    exchangeRates: 0

  Warnings:
    • Saved runtime state (InventoryData, Warehouse, CargoData, ContractData)
      cannot be scanned. If you delete an ItemData, saved player inventories
      that reference its id may break. Recommended: clear saves before testing.

  [Continue] [Cancel]
  ```

- **Перед удалением файлов** — `EditorUtility.DisplayDialog` с `MessageType.Warning` (а не Info).
- **Перед сохранением** — `AssetDatabase.SaveAssets()` + `Refresh()`.

---

## 3. Изменения в коде

### 3.1 `ResourcesCsvSchema.cs` (~15 LOC delta)

Добавить новый `BlockSchema`:
```csharp
public static readonly BlockSchema BlockPrune = new BlockSchema("prune", new[]
{
    new ColumnDef { name = "mode", required = true, type = "string",
                    description = "none | orphan | replace. Default: none." },
    new ColumnDef { name = "applyTo", required = false, type = "string", defaultValue = "all",
                    description = "all | inventory,tradeItems,marketItems,exchangeRates. Default: all." },
});
```

Добавить в `_blocks` dictionary.

### 3.2 `ResourcesCsvParser.cs` (~5 LOC delta)

Парсер **уже** парсит любые блоки через `ResourcesCsvSchema.GetBlockSchema` lookup. Нужно только:
- Validate `mode` enum в `ValidateRow` (если добавить `enumType` в ColumnDef).
- Default value: `mode=none, applyTo=all`.

### 3.3 `ResourcesCsvCrossValidator.cs` (~20 LOC delta)

Добавить валидацию:
- `mode` ∈ {"none", "orphan", "replace"} (case-insensitive).
- `applyTo` tokens ∈ {"all", "inventory", "tradeItems", "marketItems", "exchangeRates"}, без дубликатов.
- Cross-validate: если mode=replace и applyTo=marketItems, но csv_marketItems пуст — warning "no items to import, will delete all existing".

### 3.4 `ResourcesCsvImporter.cs` (~250 LOC delta)

Добавить `PruneEngine`:
```csharp
public class PruneReport
{
    public int safeToDeleteInventory, safeToDeleteTradeItems, safeToDeleteMarketItems, safeToDeleteExchangeRates;
    public int blockedInventory, blockedTradeItems, blockedMarketItems, blockedExchangeRates;
    public List<string> warnings = new List<string>();
    public List<string> blockedReasons = new List<string>();  // human-readable
    public List<string> willDeleteAssetPaths = new List<string>();  // for dialog
}

private static (PruneReport report, List<ResourcesCsvRow> orphanRows) ComputePrune(
    Dictionary<string, List<ResourcesCsvRow>> csvBlocks,
    string mode, HashSet<string> applyTo)
{
    // 1. Build whitelist from CSV.
    // 2. Scan references (ItemData, TradeItemDefinition).
    // 3. Decide KEEP/DELETE/BLOCKED.
    // 4. Return report + orphan rows (for delete).
}

private static void ApplyPrune(PruneReport report, List<ResourcesCsvRow> orphanRows, ImportResult result)
{
    // For each section:
    //   - For each orphan row:
    //     - Delete .asset file (if delete_inventory/tradeItems)
    //     - Remove from registry/db/array
    //   - SetDirty + SaveAssets
}
```

В `Apply()`:
```csharp
// 1. Parse prune block.
var pruneMode = csvBlocks.ContainsKey("prune") ? GetPruneMode(csvBlocks["prune"]) : "none";
var applyTo = csvBlocks.ContainsKey("prune") ? GetApplyTo(csvBlocks["prune"]) : new HashSet<string>{ "all" };

// 2. Apply CSV (creates/updates).
// ... existing logic ...

// 3. If pruneMode != "none", run PruneEngine.
if (pruneMode != "none")
{
    var (report, orphanRows) = ComputePrune(csvBlocks, pruneMode, applyTo);
    ApplyPrune(report, orphanRows, result);
    result.warnings.AddRange(report.warnings);
}
```

Helpers (новые):
```csharp
private static HashSet<string> ScanReferencedItemNames()
{
    // 1. FindAssets("t:Prefab") → LoadPrefabContents → GetComponentsInChildren<PickupItem>
    // 2. FindAssets("t:LootTable") → entries[].item + guaranteedItems[]
    // 3. FindAssets("t:RecipeData") → _ingredients[].item + _outputs[].item
    // 4. FindAssets("t:ResourceNodeConfig") → _resultItem + _requiredTool
    // 5. FindAssets("t:MarketConfig") → items[].definition (MarketItemConfig.definition)
    // → HashSet<string> (itemName, case-insensitive)
}

private static HashSet<string> ScanReferencedTradeItemIds()
{
    // 1. FindAssets("t:MarketConfig") → items[].definition (TradeItemDefinition.itemId)
    // 2. (Phase 2) FindAssets("t:ContractData")
    // → HashSet<string> (itemId, case-insensitive)
}

private static void DeleteItemDataAsset(ItemData item)
{
    var path = AssetDatabase.GetAssetPath(item);
    if (!string.IsNullOrEmpty(path))
        AssetDatabase.DeleteAsset(path);
}

private static void DeleteTradeItemAsset(TradeItemDefinition t)
{
    var path = AssetDatabase.GetAssetPath(t);
    if (!string.IsNullOrEmpty(path))
        AssetDatabase.DeleteAsset(path);
}
```

### 3.5 `ResourcesCsvWindow.cs` (~80 LOC delta)

Добавить 3-й state group (после импорт/экспорт):
- **Prune section** (visible only после Preview):
  ```
  ── Prune mode ──
  Mode: [none ▼] (none / orphan / replace)
  Apply to: ☑ inventory  ☑ tradeItems  ☑ marketItems  ☑ exchangeRates
  ── Prune preview ──
  Safe to delete: 3 inventory, 0 tradeItems, 2 marketItems, 0 exchangeRates
  Blocked: 0
  Warnings: 2
  ```
- **Import button** — если mode != "none" → confirm dialog "Will delete N items. Continue?".

### 3.6 `ResourcesCsvExporter.cs` (~10 LOC delta)

При экспорте — добавить комментарий-блок в начало CSV:
```
# block=prune (default config — replace before re-import if needed)
# mode,none
# applyTo,all
```
(Не реальный блок с данными, просто reminder. Реальный config — у пользователя в отдельном файле или решении.)

---

## 4. Acceptance-критерии (smoke-тесты)

### Test 1 — `mode=none` (backward-compat)
- CSV без `# block=prune` → поведение идентично T-IE07.
- ItemRegistry, TradeItemDatabase, MarketConfig, ExchangeRateConfig — только update/create, **без удаления**.

### Test 2 — `mode=orphan, applyTo=inventory`
- В проекте: 47 ItemData (включая 5 orphan: Говно, ТестовыйКамень, ...).
- В CSV: 37 строк (без Говно/ТестовыйКамень).
- После Apply+Prune:
  - Создано: 0
  - Обновлено: 37
  - Удалено (ItemData): ~5 (orphan)
  - Заблокировано: 0 (нет references)
  - ItemRegistry: 47 - 5 = 42 entries.

### Test 3 — `mode=orphan, applyTo=inventory` с references
- В проекте: ItemData «Медная руда» (id=26, orphan — без duplicates) и «Медная руда» (id=29, в LootTable).
- В CSV: «Медная руда» одна строка.
- После Apply+Prune:
  - Один из дубликатов (id=26) удалён.
  - Другой (id=29) — **заблокирован** (есть reference в LootTable).
  - LootTable продолжает работать.

### Test 4 — `mode=replace, applyTo=marketItems`
- В проекте: 7 market items (Primium=2, Secundus=2, Tertius=1, Quartus=2).
- В CSV: только «antigrav_ingot_v01,primium,500,10,0.005,None,y,y» (1 строка).
- После Apply+Prune:
  - Все 7 существующих market items удалены.
  - 1 новый создан.
  - Primium=1, Secundus=0, Tertius=0, Quartus=0.

### Test 5 — safety dialogs
- `mode=replace` → dialog "Will delete N items. Continue?" [Yes/No/Cancel].
- Пользователь нажимает [No] → prune отменяется, **НО apply CSV уже выполнен** (потому что Apply() делает apply до prune). Это **intentional** — пользователь может вручную откатить через git.

### Test 6 — saved runtime state warning
- Игрок имеет в `InventoryData` (saved) item с id=37.
- CSV: id=37 удаляется.
- Mode=orphan → ItemData удаляется, ItemRegistry entry удаляется.
- Warning в ImportResult: "ItemData with id=37 deleted but may be referenced in saved InventoryData (Application.persistentDataPath/...). Recommend clearing saves."

---

## 5. Оценка объёма

| Файл | LOC delta | Сложность |
|------|-----------|-----------|
| `ResourcesCsvSchema.cs` | +15 | XS |
| `ResourcesCsvParser.cs` | +5 | XS |
| `ResourcesCsvCrossValidator.cs` | +20 | S |
| `ResourcesCsvImporter.cs` | +250 | L |
| `ResourcesCsvWindow.cs` | +80 | M |
| `ResourcesCsvExporter.cs` | +10 | XS |
| `Resources_Import.csv` (sample) | +5 (comment) | XS |
| **ИТОГО** | **~385 LOC** | M |

Smoke-тесты (6 сценариев) — отдельная сессия.

---

## 6. Что НЕ делает MVP

- ❌ **Сканирование saved runtime state** (InventoryData/Warehouse/CargoData/ContractData в `persistentDataPath`) — вне `Assets/`, importer не видит. **Только warning**.
- ❌ **Драг-н-дроп** (что удалить) — buttons only.
- ❌ **Undo support** — пользователь делает `git checkout` если передумал.
- ❌ **Per-asset "keep this" override** — в Phase 2 (comment в CSV `# keep=itemName`).
- ❌ **Delete protection через .keep файл** — overkill для MVP.

---

## 7. План имплементации

**Один тикет: T-IE08 Pruning** (~3-4 часа).

Шаги:
1. Schema + Parser + CrossValidator (small, 1 sub-ticket).
2. Importer `PruneEngine` — scan references + compute + apply.
3. Window — UI + confirm dialogs.
4. 6 smoke-тестов.
5. Обновить `05_USER_GUIDE.md` (новый раздел "Pruning").
6. Sample CSV — добавить комментарий `# block=prune example`.

**Когда начнём?** Жду ваш сигнал.
