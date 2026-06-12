// =====================================================================================
// ResourcesCsvSchema.cs — определение колонок и блоков CSV для Resources Import/Export
// =====================================================================================
// Документация:
//   • docs/Markets/Resources_import_export/02_DESIGN.md §1
//   • docs/Markets/Resources_import_export/03_TICKETS.md T-IE01
//
// Назначение: метаданные 4 MVP-блоков CSV (inventory, tradeItems, marketItems,
// exchangeRates). Каждый блок — набор ColumnDef с типом/required/default/aliases.
// recipes — Phase 2 (out of scope MVP), но парсер распознаёт секцию для forward-compat.
//
// Прецедент: QuestCsvSchema (Assets/_Project/Quests/Editor/QuestCsvSchema.cs) —
// копируем ColumnDef, парсер (SplitCsvLines/ParseCsvLine), pattern aliases.
//
// T-IE01 (2026-06-11): MVP. ~200 LOC.
// =====================================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectC.Items.Editor
{
    // ============================================================
    // 1. ColumnDef — описание одной колонки
    // ============================================================

    public class ColumnDef
    {
        public string name;          // каноническое имя (без пробелов, латиница)
        public string[] aliases;     // альтернативные имена (русские, с пробелами)
        public bool required;        // обязательная колонка
        public string defaultValue;  // значение по умолчанию (если ячейка пустая)
        public string type;          // "string" | "int" | "float" | "bool" | "enum"
        public string description;   // human-readable описание
        public System.Type enumType; // если type=="enum" — тип enum
    }

    // ============================================================
    // 2. BlockSchema — набор колонок для одной секции (# block=...)
    // ============================================================

    public class BlockSchema
    {
        public string blockName;     // "inventory", "tradeItems", ...
        public ColumnDef[] Columns { get; private set; }

        public BlockSchema(string blockName, ColumnDef[] columns)
        {
            this.blockName = blockName;
            Columns = columns;
        }

        public string ResolveColumnName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return null;
            var lower = rawName.Trim().ToLowerInvariant();
            foreach (var col in Columns)
            {
                if (col.name.ToLowerInvariant() == lower) return col.name;
                if (col.aliases != null)
                    foreach (var alias in col.aliases)
                        if (alias.ToLowerInvariant() == lower) return col.name;
            }
            return null;
        }

        public ColumnDef GetColumn(string name) => Columns.FirstOrDefault(c => c.name == name);
    }

    // ============================================================
    // 3. ResourcesCsvSchema — все 4 MVP-блока + Phase 2 placeholder
    // ============================================================

    public static class ResourcesCsvSchema
    {
        // -------- Block 1: inventory (ItemData) --------
        public static readonly BlockSchema BlockInventory = new BlockSchema("inventory", new[]
        {
            new ColumnDef { name = "itemName",    aliases = new[]{"item name","item_name","название","имя предмета"}, required = true,  type = "string", description = "Уникальное имя предмета (itemName в ItemData). Любой UTF-8 текст, регистр учитывается." },
            new ColumnDef { name = "itemType",    aliases = new[]{"item type","item_type","тип","тип предмета"},    required = true,  type = "enum",   enumType = typeof(ItemType), defaultValue = "Resources", description = "ItemType enum: Resources/Equipment/Food/Fuel/Antigrav/Meziy/Medical/Tech" },
            new ColumnDef { name = "description", aliases = new[]{"desc","описание","tooltip"},                       required = false, type = "string", description = "UI/tooltip текст" },
            new ColumnDef { name = "maxStack",    aliases = new[]{"max stack","max_stack","stack","макс стак"},      required = false, type = "int",    defaultValue = "1", description = "Сколько в 1 слоте. 1 = non-stackable" },
            new ColumnDef { name = "weightKg",    aliases = new[]{"weight","weight_kg","вес"},                        required = false, type = "float",  defaultValue = "0.1", description = "Вес одного предмета (кг)" },
        });

        // -------- Block 2: tradeItems (TradeItemDefinition) --------
        public static readonly BlockSchema BlockTradeItems = new BlockSchema("tradeItems", new[]
        {
            new ColumnDef { name = "tradeItemId",      aliases = new[]{"trade item id","trade_item_id","item id","itemId"}, required = true,  type = "string", description = "Уникальный id товара (snake_case + _vNN, напр. 'antigrav_ingot_v01')" },
            new ColumnDef { name = "displayName",      aliases = new[]{"display name","display_name","name"},            required = true,  type = "string", description = "Отображаемое имя (напр. 'Антигравий (слиток)')" },
            new ColumnDef { name = "basePrice",        aliases = new[]{"price","base price","цена"},                   required = false, type = "float",  defaultValue = "10", description = "Базовая цена CR" },
            new ColumnDef { name = "weight",           aliases = new[]{"вес"},                                              required = false, type = "float",  defaultValue = "1", description = "Вес единицы (кг)" },
            new ColumnDef { name = "volume",           aliases = new[]{"объём","объем","vol"},                            required = false, type = "float",  defaultValue = "0.1", description = "Объём единицы (м³)" },
            new ColumnDef { name = "slots",            aliases = new[]{"cargo slots","слоты"},                             required = false, type = "int",    defaultValue = "1", description = "Cargo-slots за единицу" },
            new ColumnDef { name = "isDangerous",      aliases = new[]{"dangerous","опасный"},                              required = false, type = "bool",   defaultValue = "n", description = "Опасный груз (y/n)" },
            new ColumnDef { name = "isFragile",        aliases = new[]{"fragile","хрупкий"},                                 required = false, type = "bool",   defaultValue = "n", description = "Хрупкий груз (y/n)" },
            new ColumnDef { name = "isContraband",     aliases = new[]{"contraband","контрабанда"},                          required = false, type = "bool",   defaultValue = "n", description = "Контрабанда (y/n)" },
            new ColumnDef { name = "requiredFaction",  aliases = new[]{"faction","фракция"},                                  required = false, type = "enum",   enumType = typeof(ProjectC.Trade.Faction), defaultValue = "None", description = "Фракция (None/NP/Aurora/Titan/Hermes/Prometheus/FreeTraders/Military)" },
        });

        // -------- Block 3: marketItems (MarketItemConfig) --------
        public static readonly BlockSchema BlockMarketItems = new BlockSchema("marketItems", new[]
        {
            new ColumnDef { name = "tradeItemId",        aliases = new[]{"item id","itemId"},                              required = true,  type = "string", description = "= TradeItemDefinition.itemId" },
            new ColumnDef { name = "locationId",         aliases = new[]{"location","market","локация","рынок"},          required = true,  type = "string", description = "MarketConfig.locationId: primium/secundus/tertius/quartus" },
            new ColumnDef { name = "basePrice",          aliases = new[]{"price","цена"},                                  required = false, type = "float",  description = "Цена на этом рынке (override TradeItem.basePrice)" },
            new ColumnDef { name = "initialStock",       aliases = new[]{"stock","начальный сток","сток"},                  required = false, type = "int",    defaultValue = "50", description = "Стартовый сток" },
            new ColumnDef { name = "regenPerTick",       aliases = new[]{"regen","регенерация"},                             required = false, type = "float",  defaultValue = "0.02", description = "Регенерация за тик (0..0.5, доля от initialStock)" },
            new ColumnDef { name = "factionRestriction", aliases = new[]{"faction restriction","фракция"},                  required = false, type = "enum",   enumType = typeof(ProjectC.Trade.Faction), defaultValue = "None", description = "Фракция (None = всем)" },
            new ColumnDef { name = "allowBuy",           aliases = new[]{"can buy","покупка"},                              required = false, type = "bool",   defaultValue = "y", description = "Разрешена покупка (y/n)" },
            new ColumnDef { name = "allowSell",          aliases = new[]{"can sell","продажа"},                             required = false, type = "bool",   defaultValue = "y", description = "Разрешена продажа (y/n)" },
        });

        // -------- Block 4: exchangeRates (ExchangeRateEntry) --------
        public static readonly BlockSchema BlockExchangeRates = new BlockSchema("exchangeRates", new[]
        {
            new ColumnDef { name = "tradeItemId",        aliases = new[]{"warehouse item","warehouse id","ящик"},       required = true,  type = "string", description = "= TradeItemDefinition.itemId (ящик/канистра/слиток)" },
            new ColumnDef { name = "inventoryItemName",  aliases = new[]{"inventory item","inventory name","предмет"},   required = true,  type = "string", description = "= ItemData.itemName (пикабл)" },
            new ColumnDef { name = "warehouseQty",       aliases = new[]{"box qty","коробок"},                             required = false, type = "int",    defaultValue = "1", description = "Сколько ящиков за операцию" },
            new ColumnDef { name = "inventoryQty",       aliases = new[]{"pickup qty","шт","штук"},                          required = false, type = "int",    defaultValue = "100", description = "Сколько пикаблов за операцию" },
            new ColumnDef { name = "displayName",        aliases = new[]{"name","отображаемое"},                            required = false, type = "string", description = "Отображаемое имя (напр. 'Ящик железной руды')" },
        });

        // -------- Block 5: recipes (Phase 2 — placeholder) --------
        // Парсер распознаёт секцию для forward-compat, importer выдаёт warning + skip.
        // Реальный импорт — отдельный CraftingCsvImporter.
        public static readonly BlockSchema BlockRecipes = new BlockSchema("recipes", new[]
        {
            new ColumnDef { name = "recipeName", required = true, type = "string", description = "[Phase 2] Уникальное имя рецепта (= asset filename)" },
        });

        // -------- Block 6: prune (T-IE08) --------
        // Управляет очисткой устаревших ItemData/TradeItemDefinition/MarketItemConfig/
        // ExchangeRateEntry. Если блок отсутствует — mode=none (backward-compat, ничего
        // не удаляется). Только 1 строка данных (mode, applyTo).
        // mode: "none" | "orphan" | "replace"
        //   - none    — ничего не удалять (default)
        //   - orphan  — удалить ассеты, которых нет в CSV И на которые нет references
        //   - replace — удалить ВСЁ в указанных applyTo секциях (двойной confirm)
        // applyTo: "all" | "inventory,tradeItems,marketItems,exchangeRates" (через запятую)
        public static readonly BlockSchema BlockPrune = new BlockSchema("prune", new[]
        {
            new ColumnDef { name = "mode",     aliases = new[]{"prune mode"}, required = true,  type = "string", defaultValue = "none",
                            description = "none | orphan | replace. Default: none. См. 06_PRUNE_DESIGN.md." },
            new ColumnDef { name = "applyTo",  aliases = new[]{"apply to","scope"},  required = false, type = "string", defaultValue = "all",
                            description = "all | inventory,tradeItems,marketItems,exchangeRates (через запятую). Default: all." },
        });

        // ============================================================
        // Lookup
        // ============================================================

        private static readonly Dictionary<string, BlockSchema> _blocks = new Dictionary<string, BlockSchema>
        {
            { BlockInventory.blockName,   BlockInventory },
            { BlockTradeItems.blockName,  BlockTradeItems },
            { BlockMarketItems.blockName, BlockMarketItems },
            { BlockExchangeRates.blockName, BlockExchangeRates },
            { BlockRecipes.blockName,     BlockRecipes },
            { BlockPrune.blockName,       BlockPrune },
        };

        public static BlockSchema GetBlockSchema(string blockName)
        {
            return _blocks.TryGetValue(blockName, out var schema) ? schema : null;
        }

        public static IEnumerable<BlockSchema> AllBlocks => _blocks.Values;
    }

    // ============================================================
    // 4. ResourcesCsvRow — одна строка после парсинга
    // ============================================================

    public class ResourcesCsvRow
    {
        /// <summary>Имя секции (inventory/tradeItems/marketItems/exchangeRates/recipes).</summary>
        public string block;

        /// <summary>Сырые значения (ключ = каноническое имя колонки).</summary>
        public Dictionary<string, string> values = new Dictionary<string, string>();

        /// <summary>Номер строки (1-based, включая header).</summary>
        public int lineNumber;

        /// <summary>Ошибки валидации этой строки.</summary>
        public List<string> errors = new List<string>();

        public string Get(string column) => values.TryGetValue(column, out var v) ? v ?? "" : "";

        public int GetInt(string column, int defaultVal = 0)
            => int.TryParse(Get(column), System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultVal;

        public float GetFloat(string column, float defaultVal = 0f)
            => float.TryParse(Get(column), System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultVal;

        public bool GetBool(string column)
        {
            var v = Get(column).ToLowerInvariant().Trim();
            return v == "y" || v == "yes" || v == "true" || v == "1";
        }

        public bool HasError => errors.Count > 0;
    }
}
#endif
