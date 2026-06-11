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

namespace ProjectC.Items.Editor
{
    public class ResourcesCsvImporter
    {
        // ============================================================
        // Asset paths
        // ============================================================

        public const string ITEMS_FOLDER = "Assets/_Project/Resources/Items";
        public const string ITEM_REGISTRY_PATH = "Assets/_Project/Items/Data/ItemRegistry.asset";

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
        /// MVP: обрабатывает inventory (T-IE03) + recipes skip (Phase 2).
        /// T-IE04 добавит tradeItems/marketItems/exchangeRates.
        /// </summary>
        public static ImportResult Apply(Dictionary<string, List<ResourcesCsvRow>> blocks)
        {
            var result = new ImportResult();

            // 1. Process inventory → ItemData + ItemRegistry.
            ProcessInventory(blocks.GetValueOrDefault("inventory", new List<ResourcesCsvRow>()), result);

            // 2. recipes — Phase 2 placeholder (skip with warning).
            if (blocks.TryGetValue("recipes", out var recipes) && recipes.Count > 0)
                result.warnings.Add($"recipes: {recipes.Count} rows skipped (Phase 2, see Crafting roadmap)");

            // T-IE04: ProcessTradeItems, ProcessMarketItems, ProcessExchangeRates.

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
