# Resources_Import.csv — Справка

> **Source of truth** для 4 подсистем: `ItemData`, `TradeItemDefinition`, `MarketItemConfig`, `ExchangeRateEntry`.
> `.asset` файлы **генерируются** из этого CSV.

**Status:** ✅ MVP (T-IE01..T-IE07) — Phase 1 done. Recipes/CraftingStation — Phase 2 (отдельная итерация).

---

## 1. Как пользоваться

### Редактирование в Excel

1. Открыть `Assets/_Project/Resources/_docs/Resources_Import.csv` в **Excel** или LibreOffice Calc.
2. **Encoding**: UTF-8 with BOM (Excel обычно определяет автоматически; иначе — File → Import → UTF-8).
3. **Разделитель**: запятая (`,`). Если Excel сохраняет с `;` — сменить региональные настройки или использовать Text Import Wizard.
4. **Header row** (первая строка в каждой секции) — **не редактировать**. Колонки определены в `ResourcesCsvSchema.cs`.

### Применение через Unity Editor

1. Открыть Unity → `Tools → ProjectC → Resources → CSV Import/Export`.
2. **Browse** → выбрать CSV → **Preview** (показывает все строки с ошибками/предупреждениями).
3. Если Preview OK — **▶ Import** (зелёная кнопка). Откроется dialog с результатами (Created/Updated/Skipped/Errors).
4. После Import — **Ctrl+S** в Unity для сохранения (если автосохранение отключено).
5. Для round-trip проверки: **Export** (синяя кнопка) → сохранить в новый файл → сравнить.

### Что НЕ покрывает этот CSV

- ❌ **`icon` (Sprite)** — бинарный asset, остаётся в `.asset` (CSV не несёт бинарные ссылки).
- ❌ **Recipes / Crafting** — Phase 2 (отдельный `CraftingCsvImporter`, см. ниже).
- ❌ **LootTable, ResourceNode, NPC, Quest** — у каждой подсистемы свой CSV importer.
- ❌ **`.meta` файлы** — генерируются Unity автоматически.

---

## 2. Структура CSV (4 секции)

| Секция | Что генерирует | Колонок |
|--------|----------------|---------|
| `# block=inventory` | `ItemData` + `ItemRegistry` (id 1..N) | 5 (itemName, itemType, description, maxStack, weightKg) |
| `# block=tradeItems` | `TradeItemDefinition` + `TradeItemDatabase` | 10 (tradeItemId, displayName, basePrice, weight, volume, slots, isDangerous, isFragile, isContraband, requiredFaction) |
| `# block=marketItems` | `MarketConfig.items[]` (inline) | 8 (tradeItemId, locationId, basePrice, initialStock, regenPerTick, factionRestriction, allowBuy, allowSell) |
| `# block=exchangeRates` | `ExchangeRateConfig.rates[]` (inline) | 5 (tradeItemId, inventoryItemName, warehouseQty, inventoryQty, displayName) |

**recipes — Phase 2 (не покрывается MVP).** Парсер распознаёт секцию для forward-compat, importer выдаёт warning + skip.

### Конвенции

- **itemName** — уникальное в inventory (case-insensitive). Дубликаты → global error.
- **tradeItemId** — уникальное (snake_case + `_vNN`). `resource_iron_box`, `antigrav_ingot_v01`. Дубликаты → global error.
- **marketItems** — уникальная пара `(tradeItemId, locationId)`. Если существующая пара в CSV повторяется — update; иначе — create.
- **exchangeRates** — уникальная пара `(tradeItemId, inventoryItemName)`. То же: update или create.
- **ItemType enum** — `Resources / Equipment / Food / Fuel / Antigrav / Meziy / Medical / Tech`.
- **Faction enum** — `None / NP / Aurora / Titan / Hermes / Prometheus / FreeTraders / Military`.
- **RecipeCategory** (для recipes) — `Module / Consumable / Ship / Material / Misc`.

### Defaults (если ячейка пустая)

| Поле | Default |
|------|---------|
| `maxStack` | 1 |
| `weightKg` | 0.1 |
| `basePrice` | 10 |
| `weight` (tradeItem) | 1 |
| `volume` (tradeItem) | 0.1 |
| `slots` | 1 |
| `isDangerous/isFragile/isContraband` | n |
| `requiredFaction` | None |
| `initialStock` (marketItem) | 50 |
| `regenPerTick` | 0.02 |
| `factionRestriction` (market) | None |
| `allowBuy/allowSell` | y |
| `warehouseQty` | 1 |
| `inventoryQty` | 100 |

---

## 3. Типичные сценарии

### Добавить новый ресурс

Допустим, хотим «Медный камень» — новый тип руды, продаётся ящиками на primium.

В Excel:
1. `# block=inventory` → добавить строку:
   ```
   Медный камень,Resources,Кусок медной породы. Добывается в шахтах.,30,3.0
   ```
2. `# block=tradeItems` → добавить:
   ```
   resource_copper_stone_box,Ящик медного камня,150,60,6,4,n,n,n,None
   ```
3. `# block=marketItems` → добавить:
   ```
   resource_copper_stone_box,primium,180,30,0.02,None,y,y
   ```
4. `# block=exchangeRates` → добавить:
   ```
   resource_copper_stone_box,Медный камень,1,100,Ящик медного камня
   ```
5. Сохранить CSV (Ctrl+S).
6. В Unity: `Tools → ProjectC → Resources → CSV Import/Export` → Browse → Preview → Import.
7. Проверить файлы:
   - `Resources/Items/Item_Resources_Медный_камень.asset` создан
   - `Items/Data/ItemRegistry.asset` — добавлена запись (следующий id)
   - `Trade/Data/Items/TradeItem_resource_copper_stone_box.asset` создан
   - `Trade/Data/TradeItemDatabase.asset` — добавлен в `allItems`
   - `Trade/Data/Markets/MarketConfig_Primium.asset` — добавлен в `items[]`
   - `Resources/Exchange/DefaultExchangeRate.asset` — добавлен в `rates[]`
8. (Опц.) Создать PickupItem в `WorldScene_0_0` с этим `itemData`.
9. Play Mode → подобрать → Inventory → Exchanger tab → Упаковать 100 шт → +1 ящик.

### Обновить существующий

Изменить `weightKg`, `maxStack`, `description` в строке inventory → Save → Import. Importer найдёт по `itemName` и обновит **поля** через `EditorUtility.SetDirty` (**GUID не меняется** — LootTable/Recipe остаются валидными).

### Round-trip проверка

1. **Export** → сохранить в `/tmp/dump.csv`.
2. **Import** тот же файл → должен быть `Created=0, Updated=N, Skipped=0, Errors=0` (idempotency).

---

## 4. Кросс-валидация (импорт отвергнет если…)

| Ошибка | Блокирует импорт? |
|--------|:--:|
| `inventory: duplicate itemName` | ❌ global error |
| `tradeItems: duplicate tradeItemId` | ❌ global error |
| `marketItems: tradeItemId not in tradeItems` | ⚠️ row skip |
| `exchangeRates: tradeItemId not in tradeItems` | ⚠️ row skip |
| `exchangeRates: inventoryItemName not in inventory` | ⚠️ row skip |
| Type mismatch (int, float, bool, enum) | ⚠️ row skip |
| Required field empty | ⚠️ row skip |

**Глобальные ошибки** блокируют импорт (preview → red ✗). **Per-row ошибки** пропускают строку, остальные применяются.

---

## 5. Файлы, которые importer создаёт/обновляет

| Asset path | Создаёт? | Обновляет? |
|------------|:--------:|:----------:|
| `Assets/_Project/Resources/Items/Item_{Type}_{Name}.asset` | ✅ | ✅ (поля через SetDirty, GUID stable) |
| `Assets/_Project/Items/Data/ItemRegistry.asset` | — | ✅ (добавляет Entry) |
| `Assets/_Project/Trade/Data/Items/TradeItem_{itemId}.asset` | ✅ | ✅ |
| `Assets/_Project/Trade/Data/TradeItemDatabase.asset` | — | ✅ (добавляет в allItems) |
| `Assets/_Project/Trade/Data/Markets/MarketConfig_{TitleCase}.asset` | ✅ (если нет) | ✅ (добавляет в items[]) |
| `Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset` | — | ✅ (добавляет в rates[]) |

**GUID existing ассетов НЕ меняется** — ссылки из LootTable/Recipe остаются валидными.

---

## 6. Phase 2 (вне MVP)

- ❌ **`# block=recipes`** — рецепты крафта + CraftingStation `_allowedRecipes[]`. Импортируется отдельным `CraftingCsvImporter`. См. `docs/Crafting_system/`.
- ❌ **Per-location ExchangeRateConfig** — `MarketConfig.locationId → свой ExchangeRateConfig`. Сейчас один глобальный `DefaultExchangeRate.asset`.
- ❌ **Backup перед импортом** — пользователь делает сам через git.
- ❌ **Иконки (Sprite)** — отдельный пайплайн PNG → Texture2D → Sprite.

---

## 7. Troubleshooting

| Проблема | Решение |
|----------|---------|
| Excel сохранил в `;` вместо `,` | File → Options → Advanced → Decimal separator = `.`, List separator = `,` |
| Кириллица — крякозябры | Убедиться что сохранение в UTF-8 with BOM (Excel: Save As → Tools → Web Options → Encoding: UTF-8) |
| `Error: ItemRegistry not found` | Не должно быть — ассет существует. Если удалён — создать через `ProjectC/Items/Item Registry` меню |
| `MarketConfig_Primium not found` | Не должно быть (файл есть). Если удалён — importer создаст новый с auto-generated `displayName` |
| После Import — некоторые предметы не подбираются в Play Mode | `ItemRegistry.asset` не сохранился. Проверить `Items/Data/ItemRegistry.asset` — там ли новая запись. Если нет — перезапустить Import |
| Дубликат `itemName` в моих `.asset` файлах | **Legacy data** — Exporter покажет дубликаты. Cleanup вне scope importer'а. Можно переименовать одну из копий в CSV и re-import |

---

**См. также:**
- `docs/Markets/Resources_import_export/02_DESIGN.md` — полный design с примерами кода
- `docs/Markets/Resources_import_export/03_TICKETS.md` — история тикетов T-IE01..T-IE07
- `docs/Markets/Resources_exchanger/` — обменник (Pack/Unpack), использует этот CSV для конфигурации
- `docs/Markets/Resources_import_export/04_EXAMPLES.md` — больше примеров и acceptance-сценариев
