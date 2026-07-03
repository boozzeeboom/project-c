// =====================================================================================
// ResourcesCsvImporter.cs — импорт CSV в ScriptableObject'ы (inventory, tradeItems, marketItems, exchangeRates)
// =====================================================================================
// Документация:
//   • docs/Markets/Resources_import_export/02_DESIGN.md §3.5
//   • docs/Markets/Resources_import_export/03_TICKETS.md T-IE03, T-IE04
//
// Назначение: создаёт/обновляет SO-ассеты в проекте на основе распарсенного CSV.
// Ключевое правило: НЕ пересоздавать существующие .asset (GUID stable!), обновлять
// поля через EditorUtility.SetDirty. Только если ассета нет — CreateAsset.
//
// MVP scope (T-IE03): ProcessInventory (ItemData + ItemRegistry).
// T-IE04 добавит: ProcessTradeItems, ProcessMarketItems, ProcessExchangeRates.
//
// recipes (Phase 2) — warning + skip.
//
// Прецедент: QuestCsvImporter (Assets/_Project/Quests/Editor/QuestCsvImporter.cs).
//
// T-IE03 (2026-06-11): MVP. ~250 LOC.
// =====================================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectC.Trade;
using ProjectC.Trade.Config;
using ProjectC.Crafting;
using ProjectC.ResourceNode;

namespace ProjectC.Items.Editor
{
    public class ResourcesCsvImporter
    {
        // ============================================================
        // Asset paths
        // ============================================================

        public const string ITEMS_FOLDER = "Assets/_Project/Resources/Items";
        public const string ITEM_REGISTRY_PATH = "Assets/_Project/Items/Data/ItemRegistry.asset";

        // T-IE04
        public const string TRADE_ITEMS_FOLDER = "Assets/_Project/Trade/Data/Items";
        public const string TRADE_ITEM_DATABASE_PATH = "Assets/_Project/Trade/Data/TradeItemDatabase.asset";
        public const string EXCHANGE_RATE_CONFIG_PATH = "Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset";
        public const string MARKET_CONFIGS_FOLDER = "Assets/_Project/Trade/Data/Markets";

        // ============================================================
        // ImportResult
        // ============================================================

        public class ImportResult
        {
            public int created, updated, skipped;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            public int TotalChanges => created + updated;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Total: created={created}, updated={updated}, skipped={skipped}, errors={errors.Count}, warnings={warnings.Count}");
                if (errors.Count > 0)
                {
                    sb.AppendLine("Errors:");
                    foreach (var e in errors) sb.AppendLine("  [E] " + e);
                }
                if (warnings.Count > 0)
                {
                    sb.AppendLine("Warnings:");
                    foreach (var w in warnings) sb.AppendLine("  [W] " + w);
                }
                return sb.ToString();
            }
        }

        // ============================================================
        // Entry point: Apply
        // ============================================================

        /// <summary>
        /// Применяет blocks (из ResourcesCsvParser) к SO-ассетам. Возвращает ImportResult.
        /// T-IE03: inventory + ItemRegistry.
        /// T-IE04: tradeItems + TradeItemDatabase + marketItems + MarketConfig + exchangeRates + ExchangeRateConfig.
        /// T-IE08: prune block — optional cleanup (none/orphan/replace).
        /// </summary>
        public static ImportResult Apply(Dictionary<string, List<ResourcesCsvRow>> blocks)
        {
            var result = new ImportResult();

            // 1. Process inventory → ItemData + ItemRegistry.
            ProcessInventory(blocks.GetValueOrDefault("inventory", new List<ResourcesCsvRow>()), result);

            // 2. Process tradeItems → TradeItemDefinition + TradeItemDatabase.
            ProcessTradeItems(blocks.GetValueOrDefault("tradeItems", new List<ResourcesCsvRow>()), result);

            // 3. Process marketItems → MarketConfig.items[].
            ProcessMarketItems(blocks.GetValueOrDefault("marketItems", new List<ResourcesCsvRow>()), result);

            // 4. Process exchangeRates → ExchangeRateConfig.rates[].
            ProcessExchangeRates(blocks.GetValueOrDefault("exchangeRates", new List<ResourcesCsvRow>()), result);

            // 5. recipes — Phase 2 placeholder (skip with warning).
            if (blocks.TryGetValue("recipes", out var recipes) && recipes.Count > 0)
                result.warnings.Add($"recipes: {recipes.Count} rows skipped (Phase 2, see Crafting roadmap)");

            // 6. T-IE08: prune (cleanup).
            ApplyPrune(blocks, result);

            // Save all dirty assets.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        // ============================================================
        // ProcessInventory — ItemData + ItemRegistry (T-IE03)
        // ============================================================

        private static void ProcessInventory(List<ResourcesCsvRow> rows, ImportResult result)
        {
            if (rows.Count == 0) return;

            // 1. Load ItemRegistry (don't recreate).
            var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(ITEM_REGISTRY_PATH);
            if (registry == null)
            {
                result.errors.Add($"ItemRegistry not found at {ITEM_REGISTRY_PATH}. " +
                                   "Create one via menu 'ProjectC/Items/Item Registry'.");
                return;
            }

            // 2. Use SerializedObject to edit the private 'entries' list (Unity Editor pattern).
            var so = new SerializedObject(registry);
            var entriesProp = so.FindProperty("entries");
            if (entriesProp == null)
            {
                result.errors.Add($"ItemRegistry: 'entries' SerializedProperty not found. " +
                                   $"Check that ItemRegistry has a [SerializeField] List<Entry> entries field.");
                return;
            }

            int maxId = 0;
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var elem = entriesProp.GetArrayElementAtIndex(i);
                int eid = elem.FindPropertyRelative("id").intValue;
                if (eid > maxId) maxId = eid;
            }

            // Build name→ItemData index from registry (fast path).
            var byName = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var elem = entriesProp.GetArrayElementAtIndex(i);
                var itemRef = elem.FindPropertyRelative("item").objectReferenceValue as ItemData;
                if (itemRef != null && !string.IsNullOrEmpty(itemRef.itemName))
                    byName[itemRef.itemName] = itemRef;
            }

            // Also scan Resources/Items/ for items NOT in registry (orphans).
            // We don't auto-register them here — that's the CSV importer's job.
            var existingAssets = AssetDatabase.FindAssets("t:ItemData", new[] { ITEMS_FOLDER });
            foreach (var guid in existingAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && !string.IsNullOrEmpty(item.itemName) && !byName.ContainsKey(item.itemName))
                    byName[item.itemName] = item;
            }

            EnsureFolder(ITEMS_FOLDER);

            // 3. Process each row.
            foreach (var row in rows)
            {
                if (row.HasError) { result.skipped++; continue; }

                var name = row.Get("itemName");
                if (string.IsNullOrEmpty(name)) { result.skipped++; continue; }

                var typeStr = row.Get("itemType");
                if (!Enum.TryParse<ItemType>(typeStr, true, out var itemType))
                {
                    row.errors.Add($"Line {row.lineNumber}: itemType '{typeStr}' is not valid ItemType");
                    result.skipped++;
                    continue;
                }

                if (byName.TryGetValue(name, out var existing))
                {
                    // UPDATE: existing asset.
                    UpdateItemData(existing, itemType, row, result);
                    // Note: registry entry already references existing, no change needed.
                }
                else
                {
                    // CREATE: new asset + registry entry.
                    var newItem = CreateItemData(name, itemType, row);
                    int newId = ++maxId;

                    // Append new Entry to SerializedProperty array.
                    int insertIdx = entriesProp.arraySize;
                    entriesProp.InsertArrayElementAtIndex(insertIdx);
                    var newElem = entriesProp.GetArrayElementAtIndex(insertIdx);
                    newElem.FindPropertyRelative("id").intValue = newId;
                    newElem.FindPropertyRelative("item").objectReferenceValue = newItem;

                    byName[name] = newItem;
                    result.created++;
                }
            }

            // 4. Apply SerializedObject changes (writes entries back to ItemRegistry.asset).
            so.ApplyModifiedProperties();
        }

        // ============================================================
        // ProcessTradeItems — TradeItemDefinition + TradeItemDatabase (T-IE04)
        // ============================================================

        private static void ProcessTradeItems(List<ResourcesCsvRow> rows, ImportResult result)
        {
            if (rows.Count == 0) return;

            EnsureFolder(TRADE_ITEMS_FOLDER);

            // 1. Load TradeItemDatabase.
            var db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(TRADE_ITEM_DATABASE_PATH);
            if (db == null)
            {
                result.errors.Add($"TradeItemDatabase not found at {TRADE_ITEM_DATABASE_PATH}");
                return;
            }

            // 2. Build byId index from db + orphan scan.
            var byId = new Dictionary<string, TradeItemDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in db.allItems)
            {
                if (t != null && !string.IsNullOrEmpty(t.itemId))
                    byId[t.itemId] = t;
            }
            var existingTradeAssets = AssetDatabase.FindAssets("t:TradeItemDefinition", new[] { TRADE_ITEMS_FOLDER });
            foreach (var guid in existingTradeAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetDatabase.LoadAssetAtPath<TradeItemDefinition>(path);
                if (t != null && !string.IsNullOrEmpty(t.itemId) && !byId.ContainsKey(t.itemId))
                    byId[t.itemId] = t;
            }

            foreach (var row in rows)
            {
                if (row.HasError) { result.skipped++; continue; }

                var itemId = row.Get("tradeItemId");
                if (string.IsNullOrEmpty(itemId)) { result.skipped++; continue; }

                if (byId.TryGetValue(itemId, out var existing))
                {
                    UpdateTradeItem(existing, row, result);
                }
                else
                {
                    var newItem = CreateTradeItem(itemId, row);
                    db.allItems.Add(newItem);
                    byId[itemId] = newItem;
                    result.created++;
                }
            }

            EditorUtility.SetDirty(db);
        }

        private static void UpdateTradeItem(TradeItemDefinition item, ResourcesCsvRow row, ImportResult result)
        {
            // itemId is the key — don't overwrite.
            item.displayName = row.Get("displayName");
            if (string.IsNullOrEmpty(item.displayName)) item.displayName = item.itemId;
            item.basePrice = row.GetFloat("basePrice", item.basePrice);
            item.weight = row.GetFloat("weight", item.weight);
            item.volume = row.GetFloat("volume", item.volume);
            item.slots = Mathf.Max(1, row.GetInt("slots", item.slots));
            item.isDangerous = row.GetBool("isDangerous");
            item.isFragile = row.GetBool("isFragile");
            item.isContraband = row.GetBool("isContraband");
            var facStr = row.Get("requiredFaction");
            if (!string.IsNullOrEmpty(facStr) && Enum.TryParse<Faction>(facStr, true, out var fac))
                item.requiredFaction = fac;

            EditorUtility.SetDirty(item);
            result.updated++;
        }

        private static TradeItemDefinition CreateTradeItem(string itemId, ResourcesCsvRow row)
        {
            var item = ScriptableObject.CreateInstance<TradeItemDefinition>();
            item.itemId = itemId;
            item.displayName = row.Get("displayName");
            if (string.IsNullOrEmpty(item.displayName)) item.displayName = itemId;
            item.basePrice = row.GetFloat("basePrice", 10f);
            item.weight = row.GetFloat("weight", 1f);
            item.volume = row.GetFloat("volume", 0.1f);
            item.slots = Mathf.Max(1, row.GetInt("slots", 1));
            item.isDangerous = row.GetBool("isDangerous");
            item.isFragile = row.GetBool("isFragile");
            item.isContraband = row.GetBool("isContraband");
            var facStr = row.Get("requiredFaction");
            if (!string.IsNullOrEmpty(facStr) && Enum.TryParse<Faction>(facStr, true, out var fac))
                item.requiredFaction = fac;
            // icon: CSV doesn't carry Sprite refs — leave null.

            var path = $"{TRADE_ITEMS_FOLDER}/TradeItem_{itemId}.asset";
            EnsureUniqueAssetPath(ref path);
            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"[ResourcesCsvImporter] Created TradeItem: '{itemId}' → {path}");
            return item;
        }

        // ============================================================
        // ProcessMarketItems — MarketConfig.items[] (T-IE04)
        // ============================================================

        private static void ProcessMarketItems(List<ResourcesCsvRow> rows, ImportResult result)
        {
            if (rows.Count == 0) return;

            // 1. Group by locationId (CSV has one row per (itemId, locationId) pair).
            var byLocation = new Dictionary<string, List<ResourcesCsvRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (row.HasError) { result.skipped++; continue; }
                var loc = row.Get("locationId");
                if (string.IsNullOrEmpty(loc)) continue;
                if (!byLocation.TryGetValue(loc, out var list))
                {
                    list = new List<ResourcesCsvRow>();
                    byLocation[loc] = list;
                }
                list.Add(row);
            }

            // 2. For each location, load/create MarketConfig and process its rows.
            foreach (var kv in byLocation)
            {
                var mc = GetOrCreateMarketConfig(kv.Key, result);
                if (mc == null) continue;

                foreach (var row in kv.Value)
                {
                    var itemId = row.Get("tradeItemId");
                    if (string.IsNullOrEmpty(itemId)) { result.skipped++; continue; }

                    // Find definition by itemId (needed for MarketItemConfig.definition field).
                    var def = FindTradeItemById(itemId);
                    if (def == null)
                    {
                        // Cross-validate should have caught this. Skip with warning.
                        result.warnings.Add($"Line {row.lineNumber} (marketItems): tradeItemId '{itemId}' has no TradeItemDefinition — skipping");
                        result.skipped++;
                        continue;
                    }

                    // Find existing MarketItemConfig in this MarketConfig (by itemId).
                    MarketItemConfig existingMic = null;
                    foreach (var mic in mc.items)
                    {
                        if (mic != null && mic.itemId == itemId) { existingMic = mic; break; }
                    }

                    if (existingMic != null)
                    {
                        UpdateMarketItemConfig(existingMic, def, row, result);
                    }
                    else
                    {
                        var newMic = CreateMarketItemConfig(def, row);
                        mc.items.Add(newMic);
                        result.created++;
                    }
                }

                EditorUtility.SetDirty(mc);
            }
        }

        private static MarketConfig GetOrCreateMarketConfig(string locationId, ImportResult result)
        {
            // Search existing MarketConfig by locationId.
            var existingGuids = AssetDatabase.FindAssets("t:MarketConfig", new[] { MARKET_CONFIGS_FOLDER });
            foreach (var g in existingGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var mc = AssetDatabase.LoadAssetAtPath<MarketConfig>(p);
                if (mc != null && mc.locationId == locationId) return mc;
            }

            // Not found — create new.
            EnsureFolder(MARKET_CONFIGS_FOLDER);
            var newMc = ScriptableObject.CreateInstance<MarketConfig>();
            newMc.locationId = locationId;
            newMc.displayName = Capitalize(locationId);
            newMc.description = $"Auto-created by ResourcesCsvImporter (locationId={locationId})";
            newMc.items = new List<MarketItemConfig>();

            var path = $"{MARKET_CONFIGS_FOLDER}/MarketConfig_{Capitalize(locationId)}.asset";
            EnsureUniqueAssetPath(ref path);
            AssetDatabase.CreateAsset(newMc, path);
            result.warnings.Add($"Created new MarketConfig: {path}");
            Debug.Log($"[ResourcesCsvImporter] Created MarketConfig: locationId='{locationId}' → {path}");
            return newMc;
        }

        private static MarketItemConfig CreateMarketItemConfig(TradeItemDefinition def, ResourcesCsvRow row)
        {
            var mic = new MarketItemConfig
            {
                itemId = def.itemId,
                definition = def,
            };
            UpdateMarketItemConfig(mic, def, row, null);
            return mic;
        }

        private static void UpdateMarketItemConfig(MarketItemConfig mic, TradeItemDefinition def, ResourcesCsvRow row, ImportResult result)
        {
            mic.itemId = def.itemId;
            mic.definition = def;
            var basePriceStr = row.Get("basePrice");
            if (!string.IsNullOrEmpty(basePriceStr)) mic.basePrice = row.GetFloat("basePrice", mic.basePrice);
            int newStock = row.GetInt("initialStock", mic.initialStock);
            if (newStock > 0) mic.initialStock = newStock;
            float newRegen = row.GetFloat("regenPerTick", mic.regenPerTick);
            if (newRegen >= 0f) mic.regenPerTick = newRegen;
            var facStr = row.Get("factionRestriction");
            if (!string.IsNullOrEmpty(facStr) && Enum.TryParse<Faction>(facStr, true, out var fac))
                mic.factionRestriction = fac;
            // For bool, always apply (default is "y"/"n").
            mic.allowBuy = row.GetBool("allowBuy");
            mic.allowSell = row.GetBool("allowSell");

            if (result != null) result.updated++;
        }

        /// <summary>TitleCase: "primium" → "Primium", "tertius" → "Tertius".</summary>
        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length == 1) return s.ToUpper();
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        // ============================================================
        // ProcessExchangeRates — ExchangeRateConfig.rates[] (T-IE04)
        // ============================================================

        private static void ProcessExchangeRates(List<ResourcesCsvRow> rows, ImportResult result)
        {
            if (rows.Count == 0) return;

            var config = AssetDatabase.LoadAssetAtPath<ExchangeRateConfig>(EXCHANGE_RATE_CONFIG_PATH);
            if (config == null)
            {
                result.errors.Add($"ExchangeRateConfig not found at {EXCHANGE_RATE_CONFIG_PATH}");
                return;
            }

            // Build byPair index for upsert.
            var byPair = new Dictionary<(string, string), int>();
            for (int i = 0; i < config.rates.Count; i++)
            {
                var r = config.rates[i];
                // ExchangeRateEntry is a struct — can't be null. Use string.IsNullOrEmpty as a proxy.
                if (string.IsNullOrEmpty(r.warehouseItemId) || string.IsNullOrEmpty(r.inventoryItemName)) continue;
                byPair[(r.warehouseItemId, r.inventoryItemName)] = i;
            }

            foreach (var row in rows)
            {
                if (row.HasError) { result.skipped++; continue; }

                var whId = row.Get("tradeItemId");
                var invName = row.Get("inventoryItemName");
                if (string.IsNullOrEmpty(whId) || string.IsNullOrEmpty(invName)) { result.skipped++; continue; }

                if (byPair.TryGetValue((whId, invName), out var idx))
                {
                    UpdateExchangeRateEntry(config.rates[idx], row, result);
                }
                else
                {
                    var newEntry = CreateExchangeRateEntry(whId, invName, row);
                    config.rates.Add(newEntry);
                    result.created++;
                }
            }

            EditorUtility.SetDirty(config);
        }

        private static void UpdateExchangeRateEntry(ExchangeRateEntry entry, ResourcesCsvRow row, ImportResult result)
        {
            entry.warehouseItemId = row.Get("tradeItemId");
            entry.inventoryItemName = row.Get("inventoryItemName");
            entry.warehouseQty = Mathf.Max(1, row.GetInt("warehouseQty", entry.warehouseQty));
            entry.inventoryQty = Mathf.Max(1, row.GetInt("inventoryQty", entry.inventoryQty));
            var display = row.Get("displayName");
            if (!string.IsNullOrEmpty(display)) entry.displayName = display;

            if (result != null) result.updated++;
        }

        private static ExchangeRateEntry CreateExchangeRateEntry(string whId, string invName, ResourcesCsvRow row)
        {
            return new ExchangeRateEntry
            {
                warehouseItemId = whId,
                inventoryItemName = invName,
                warehouseQty = Mathf.Max(1, row.GetInt("warehouseQty", 1)),
                inventoryQty = Mathf.Max(1, row.GetInt("inventoryQty", 100)),
                displayName = row.Get("displayName"),
            };
        }

        // ============================================================
        // Find helpers
        // ============================================================

        private static TradeItemDefinition FindTradeItemById(string itemId)
        {
            // 1. Try TradeItemDatabase.allItems (fast).
            var db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(TRADE_ITEM_DATABASE_PATH);
            if (db != null)
            {
                foreach (var t in db.allItems)
                    if (t != null && t.itemId == itemId) return t;
            }
            // 2. Scan assets (fallback for orphans).
            var guids = AssetDatabase.FindAssets("t:TradeItemDefinition", new[] { TRADE_ITEMS_FOLDER });
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var t = AssetDatabase.LoadAssetAtPath<TradeItemDefinition>(p);
                if (t != null && t.itemId == itemId) return t;
            }
            return null;
        }

        private static void UpdateItemData(ItemData item, ItemType itemType, ResourcesCsvRow row, ImportResult result)
        {
            // Don't overwrite itemName (it's the registry key).
            // Update other fields.
            item.itemType = itemType;
            var desc = row.Get("description");
            if (!string.IsNullOrEmpty(desc)) item.description = desc;
            // For maxStack/weightKg, use GetInt/GetFloat which return default on empty.
            int newMaxStack = row.GetInt("maxStack", item.maxStack);
            if (newMaxStack >= 1) item.maxStack = newMaxStack;
            float newWeightKg = row.GetFloat("weightKg", item.weightKg);
            if (newWeightKg >= 0f) item.weightKg = newWeightKg;

            EditorUtility.SetDirty(item);
            result.updated++;
        }

        private static ItemData CreateItemData(string name, ItemType itemType, ResourcesCsvRow row)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = name;
            item.itemType = itemType;
            item.description = row.Get("description");
            item.icon = null; // CSV не несёт Sprite refs.
            item.maxStack = Mathf.Max(1, row.GetInt("maxStack", 1));
            item.weightKg = Mathf.Max(0f, row.GetFloat("weightKg", 0.1f));

            var path = $"{ITEMS_FOLDER}/Item_{itemType}_{SanitizeFileName(name)}.asset";
            EnsureUniqueAssetPath(ref path);
            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"[ResourcesCsvImporter] Created ItemData: '{name}' (id will be assigned in registry) → {path}");
            return item;
        }

        // ============================================================
        // PruneEngine (T-IE08)
        // ============================================================

        public class PruneReport
        {
            public int safeToDeleteInventory;
            public int safeToDeleteTradeItems;
            public int safeToDeleteMarketItems;
            public int safeToDeleteExchangeRates;
            public int blockedInventory;
            public int blockedTradeItems;
            public int blockedMarketItems;
            public int blockedExchangeRates;
            public List<string> warnings = new List<string>();
            public List<string> blockedReasons = new List<string>();
            public List<string> willDeleteAssetPaths = new List<string>();
        }

        /// <summary>
        /// T-IE08: применяет prune-логику (после apply CSV). Если блок prune отсутствует
        /// или mode=none — no-op.
        /// </summary>
        private static void ApplyPrune(Dictionary<string, List<ResourcesCsvRow>> blocks, ImportResult result)
        {
            if (!blocks.TryGetValue("prune", out var pruneRows) || pruneRows.Count == 0) return;
            var firstRow = pruneRows[0];
            if (firstRow.HasError)
            {
                result.errors.Add("prune block has errors, skipping prune");
                return;
            }

            var mode = (firstRow.Get("mode") ?? "none").Trim().ToLowerInvariant();
            if (mode == "none") return;

            var applyToStr = (firstRow.Get("applyTo") ?? "all").Trim().ToLowerInvariant();
            var applyTo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in applyToStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var tk = t.Trim();
                if (tk.Length == 0) continue;
                if (tk == "all") { applyTo.UnionWith(new[] { "inventory", "tradeItems", "marketItems", "exchangeRates" }); }
                else { applyTo.Add(tk); }
            }
            if (applyTo.Count == 0) { result.errors.Add("prune: applyTo is empty"); return; }

            // 1. Build references blacklist.
            var referencedItemNames = ScanReferencedItemNames();
            var referencedTradeItemIds = ScanReferencedTradeItemIds();

            // 2. Build CSV whitelist.
            var csvInvNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (blocks.TryGetValue("inventory", out var invRows))
                foreach (var r in invRows) if (!r.HasError) csvInvNames.Add(r.Get("itemName"));

            var csvTradeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (blocks.TryGetValue("tradeItems", out var trdRows))
                foreach (var r in trdRows) if (!r.HasError) csvTradeIds.Add(r.Get("tradeItemId"));

            var csvMarketPairs = new HashSet<(string, string)>();
            if (blocks.TryGetValue("marketItems", out var mktRows))
                foreach (var r in mktRows) if (!r.HasError)
                    csvMarketPairs.Add((r.Get("tradeItemId"), r.Get("locationId")));

            var csvExchPairs = new HashSet<(string, string)>();
            if (blocks.TryGetValue("exchangeRates", out var xchRows))
                foreach (var r in xchRows) if (!r.HasError)
                    csvExchPairs.Add((r.Get("tradeItemId"), r.Get("inventoryItemName")));

            // 3. Compute decisions.
            var report = new PruneReport();

            // 3a. inventory: ItemData + ItemRegistry entries.
            if (applyTo.Contains("inventory"))
            {
                var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(ITEM_REGISTRY_PATH);
                if (registry != null)
                {
                    var so = new SerializedObject(registry);
                    var entriesProp = so.FindProperty("entries");
                    var guids = AssetDatabase.FindAssets("t:ItemData", new[] { ITEMS_FOLDER });

                    // Collect all ItemData by name.
                    var allItems = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);
                    foreach (var g in guids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(g);
                        var it = AssetDatabase.LoadAssetAtPath<ItemData>(p);
                        if (it != null && !string.IsNullOrEmpty(it.itemName))
                            allItems[it.itemName] = it;
                    }
                    for (int i = entriesProp.arraySize - 1; i >= 0; i--)
                    {
                        var elem = entriesProp.GetArrayElementAtIndex(i);
                        var itemRef = elem.FindPropertyRelative("item").objectReferenceValue as ItemData;
                        if (itemRef != null && csvInvNames.Contains(itemRef.itemName)) continue;

                        // Decide: SKIP / DELETE.
                        string reason = null;
                        bool shouldDelete = false;
                        if (itemRef == null)
                        {
                            // Orphan entry (objectReferenceValue is null) — always delete.
                            shouldDelete = true;
                        }
                        else if (mode == "orphan" && referencedItemNames.Contains(itemRef.itemName))
                        {
                            // Blocked: has references in scenes/SOs.
                            report.blockedInventory++;
                            report.blockedReasons.Add($"inventory: '{itemRef.itemName}' not in CSV but has references (PickupItem/LootTable/RecipeData/etc)");
                            continue;
                        }
                        else
                        {
                            // Safe to delete.
                            shouldDelete = true;
                            reason = "not in CSV";
                        }

                        if (shouldDelete)
                        {
                            report.safeToDeleteInventory++;
                            if (itemRef != null)
                            {
                                var assetPath = AssetDatabase.GetAssetPath(itemRef);
                                if (!string.IsNullOrEmpty(assetPath)) report.willDeleteAssetPaths.Add(assetPath);
                            }
                            else
                            {
                                report.willDeleteAssetPaths.Add($"ItemRegistry entry #{i} (orphan, no ItemData)");
                            }
                            entriesProp.DeleteArrayElementAtIndex(i);
                        }
                    }
                    so.ApplyModifiedProperties();

                    // Delete .asset files (not in registry now).
                    foreach (var name in allItems.Keys)
                    {
                        if (csvInvNames.Contains(name)) continue;
                        if (mode == "orphan" && referencedItemNames.Contains(name)) continue;
                        var it = allItems[name];
                        var p = AssetDatabase.GetAssetPath(it);
                        if (!string.IsNullOrEmpty(p) && AssetDatabase.DeleteAsset(p))
                            Debug.Log($"[ResourcesCsvImporter] Pruned ItemData: '{name}' ({p})");
                    }
                }
            }

            // 3b. tradeItems: TradeItemDefinition + TradeItemDatabase.allItems.
            if (applyTo.Contains("tradeItems"))
            {
                var db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(TRADE_ITEM_DATABASE_PATH);
                if (db != null)
                {
                    // Build byId map (db + orphan scan).
                    var byId = new Dictionary<string, TradeItemDefinition>(StringComparer.OrdinalIgnoreCase);
                    foreach (var t in db.allItems)
                        if (t != null && !string.IsNullOrEmpty(t.itemId)) byId[t.itemId] = t;
                    var existingGuids = AssetDatabase.FindAssets("t:TradeItemDefinition", new[] { TRADE_ITEMS_FOLDER });
                    foreach (var g in existingGuids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(g);
                        var t = AssetDatabase.LoadAssetAtPath<TradeItemDefinition>(p);
                        if (t != null && !string.IsNullOrEmpty(t.itemId) && !byId.ContainsKey(t.itemId))
                            byId[t.itemId] = t;
                    }

                    foreach (var kv in byId)
                    {
                        if (csvTradeIds.Contains(kv.Key)) continue;
                        if (mode == "orphan" && referencedTradeItemIds.Contains(kv.Key))
                        {
                            report.blockedTradeItems++;
                            report.blockedReasons.Add($"tradeItems: '{kv.Key}' not in CSV but referenced by MarketItemConfig.definition");
                            continue;
                        }
                        report.safeToDeleteTradeItems++;
                        var assetPath = AssetDatabase.GetAssetPath(kv.Value);
                        if (!string.IsNullOrEmpty(assetPath)) report.willDeleteAssetPaths.Add(assetPath);
                        // Remove from TradeItemDatabase.
                        db.allItems.Remove(kv.Value);
                        // Delete .asset.
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            if (AssetDatabase.DeleteAsset(assetPath))
                                Debug.Log($"[ResourcesCsvImporter] Pruned TradeItem: '{kv.Key}' ({assetPath})");
                        }
                    }
                    EditorUtility.SetDirty(db);
                }
            }

            // 3c. marketItems: MarketConfig.items[].
            if (applyTo.Contains("marketItems"))
            {
                var mcGuids = AssetDatabase.FindAssets("t:MarketConfig", new[] { MARKET_CONFIGS_FOLDER });
                foreach (var g in mcGuids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var mc = AssetDatabase.LoadAssetAtPath<MarketConfig>(p);
                    if (mc == null || mc.items == null) continue;
                    int before = mc.items.Count;
                    mc.items.RemoveAll(mic => mic != null && !csvMarketPairs.Contains((mic.itemId, mc.locationId)));
                    int removed = before - mc.items.Count;
                    report.safeToDeleteMarketItems += removed;
                    EditorUtility.SetDirty(mc);
                }
            }

            // 3d. exchangeRates: ExchangeRateConfig.rates[].
            if (applyTo.Contains("exchangeRates"))
            {
                var config = AssetDatabase.LoadAssetAtPath<ExchangeRateConfig>(EXCHANGE_RATE_CONFIG_PATH);
                if (config != null && config.rates != null)
                {
                    int before = config.rates.Count;
                    config.rates.RemoveAll(r =>
                        string.IsNullOrEmpty(r.warehouseItemId) || string.IsNullOrEmpty(r.inventoryItemName) ||
                        !csvExchPairs.Contains((r.warehouseItemId, r.inventoryItemName)));
                    int removed = before - config.rates.Count;
                    report.safeToDeleteExchangeRates += removed;
                    EditorUtility.SetDirty(config);
                }
            }

            // 4. Warnings.
            report.warnings.Add(
                "prune: applied mode='" + mode + "', applyTo='" + applyToStr + "'. " +
                "Deleted: inventory=" + report.safeToDeleteInventory + ", tradeItems=" + report.safeToDeleteTradeItems +
                ", marketItems=" + report.safeToDeleteMarketItems + ", exchangeRates=" + report.safeToDeleteExchangeRates + ". " +
                "Blocked: inventory=" + report.blockedInventory + ", tradeItems=" + report.blockedTradeItems +
                ", marketItems=" + report.blockedMarketItems + ", exchangeRates=" + report.blockedExchangeRates + ".");
            // Saved runtime state warning (cannot be scanned).
            report.warnings.Add("prune: saved runtime state (InventoryData, Warehouse, CargoData, ContractData) lives in Application.persistentDataPath/ and cannot be scanned. If you deleted ItemData, saved player inventories that reference its id may break. Recommend clearing saves before testing.");
            result.warnings.AddRange(report.warnings);
        }

        /// <summary>Сканирует scenes + SO, ссылающиеся на ItemData. Возвращает HashSet itemName (case-insensitive).</summary>
        private static HashSet<string> ScanReferencedItemNames()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. PickupItem.itemData (prefab-scene).
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var g in prefabGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(p) || !p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject root = null;
                try { root = PrefabUtility.LoadPrefabContents(p); }
                catch { continue; }
                if (root == null) continue;
                try
                {
                    foreach (var pickup in root.GetComponentsInChildren<PickupItem>(true))
                    {
                        if (pickup != null && pickup.itemData != null && !string.IsNullOrEmpty(pickup.itemData.itemName))
                            result.Add(pickup.itemData.itemName);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // 2. LootTable (entries + guaranteedItems).
            foreach (var g in AssetDatabase.FindAssets("t:LootTable"))
            {
                var lt = AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(g));
                if (lt == null) continue;
                if (lt.entries != null) foreach (var e in lt.entries) if (e?.item != null) result.Add(e.item.itemName);
                if (lt.guaranteedItems != null) foreach (var it in lt.guaranteedItems) if (it != null) result.Add(it.itemName);
            }

            // 3. RecipeData (_ingredients + _outputs).
            foreach (var g in AssetDatabase.FindAssets("t:RecipeData"))
            {
                var r = AssetDatabase.LoadAssetAtPath<RecipeData>(AssetDatabase.GUIDToAssetPath(g));
                if (r == null) continue;
                if (r.Ingredients != null) foreach (var ing in r.Ingredients) if (ing.item != null) result.Add(ing.item.itemName);
                if (r.Outputs != null) foreach (var outp in r.Outputs) if (outp.item != null) result.Add(outp.item.itemName);
            }

            // 4. ResourceNodeConfig (_resultItem + _requiredTool).
            foreach (var g in AssetDatabase.FindAssets("t:ResourceNodeConfig"))
            {
                var rn = AssetDatabase.LoadAssetAtPath<ResourceNodeConfig>(AssetDatabase.GUIDToAssetPath(g));
                if (rn == null) continue;
                if (rn.ResultItem != null) result.Add(rn.ResultItem.itemName);
                if (rn.RequiredTool != null) result.Add(rn.RequiredTool.itemName);
            }

            // 5. MarketItemConfig.definition (MarketConfig_*.asset).
            foreach (var g in AssetDatabase.FindAssets("t:MarketConfig"))
            {
                var mc = AssetDatabase.LoadAssetAtPath<MarketConfig>(AssetDatabase.GUIDToAssetPath(g));
                if (mc?.items == null) continue;
                foreach (var mic in mc.items)
                    if (mic?.definition != null && !string.IsNullOrEmpty(mic.definition.itemId)) result.Add(mic.definition.itemId);
            }

            return result;
        }

        /// <summary>Сканирует SO, ссылающиеся на TradeItemDefinition. Возвращает HashSet itemId.</summary>
        private static HashSet<string> ScanReferencedTradeItemIds()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. MarketItemConfig.definition.
            foreach (var g in AssetDatabase.FindAssets("t:MarketConfig"))
            {
                var mc = AssetDatabase.LoadAssetAtPath<MarketConfig>(AssetDatabase.GUIDToAssetPath(g));
                if (mc?.items == null) continue;
                foreach (var mic in mc.items)
                    if (mic?.definition != null && !string.IsNullOrEmpty(mic.definition.itemId))
                        result.Add(mic.definition.itemId);
            }

            // 2. TradeItemDatabase.allItems (itself — keep all that are listed in db).
            // (Handled implicitly — not added to delete set.)

            return result;
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            var parts = folderPath.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void EnsureUniqueAssetPath(ref string path)
        {
            if (!File.Exists(path)) return;
            var dir = Path.GetDirectoryName(path).Replace('\\', '/');
            var nameNoExt = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (int i = 2; i < 1000; i++)
            {
                var candidate = $"{dir}/{nameNoExt}_{i}{ext}";
                if (!File.Exists(candidate))
                {
                    path = candidate;
                    return;
                }
            }
            throw new InvalidOperationException($"Could not find unique path for {path}");
        }

        /// <summary>Очищает имя файла: оставляет буквы/цифры/_/- , остальное заменяет на _.</summary>
        private static string SanitizeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "_";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
#endif
