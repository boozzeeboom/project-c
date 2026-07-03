// T-CARGO-NPC-01: Editor helper для быстрого заполнения NpcShipSchedule.cargoTrade.
// Запускается через меню: Tools/ProjectC/PeacefulShip/Fill NpcShipSchedule Cargo Trade
// Заполняет cargoTrade.buyItems в существующих .asset'ах реалистичными itemId из MarketConfig_Primium.
//
// Pattern: unity-serializedobject-csv-data-tool (legacy Resources I/E pattern).
// D30: itemId — строки, валидация через TradeDatabase (load allItems + check membership).

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectC.PeacefulShip.Core;
using ProjectC.PeacefulShip.Stations;
using ProjectC.Trade; // TradeItemDefinition

namespace ProjectC.PeacefulShip.EditorTools
{
    public static class NpcShipScheduleCargoHelper
    {
        // ============================== PRESETS ==============================
        // ItemId взяты из MarketConfig_Primium.asset (проверено 2026-07-03).
        // См. docs/NPC_others_peacfull/npc_ship/CARGO/T_CARGO_NPC_01_DESIGN_2026-07-03.md §5.

        private static readonly (string scheduleFile, NpcCargoTradeConfig[] items, int maxLoadSlots, float maxLoadWeightKg)[] PRESETS =
        {
            ("NpcShipSchedule_Courier", new NpcCargoTradeConfig[]
            {
                new NpcCargoTradeConfig { itemId = "resource_mezium_box",       desiredQuantity = 3, sellOnArrival = true, maxKeepQuantity = 0 },
                new NpcCargoTradeConfig { itemId = "resource_antigrav_box",     desiredQuantity = 2, sellOnArrival = true, maxKeepQuantity = 0 },
            }, 8, 200f),

            ("NpcShipSchedule_Trader", new NpcCargoTradeConfig[]
            {
                new NpcCargoTradeConfig { itemId = "resource_copper_wire_box",   desiredQuantity = 5, sellOnArrival = true, maxKeepQuantity = 0 },
                new NpcCargoTradeConfig { itemId = "resource_brass_sheet_box",   desiredQuantity = 4, sellOnArrival = true, maxKeepQuantity = 0 },
            }, 10, 400f),
        };

        [MenuItem("Tools/ProjectC/PeacefulShip/Fill NpcShipSchedule Cargo Trade")]
        public static void FillAllSchedules()
        {
            int totalChanged = 0;
            foreach (var (filename, items, maxLoadSlots, maxLoadWeightKg) in PRESETS)
            {
                // Ищем .asset по имени (в Resources/PeacefulShip)
                string[] guids = AssetDatabase.FindAssets($"t:NpcShipSchedule {Path.GetFileNameWithoutExtension(filename)}");
                if (guids == null || guids.Length == 0)
                {
                    Debug.LogWarning($"[NpcShipScheduleCargoHelper] asset not found: {filename}");
                    continue;
                }

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var so = AssetDatabase.LoadAssetAtPath<NpcShipSchedule>(path);
                    if (so == null) continue;

                    Undo.RecordObject(so, "Fill NpcShipSchedule Cargo Trade");

                    if (so.cargoTrade == null)
                        so.cargoTrade = new NpcCargoTradeListConfig();

                    so.cargoTrade.useUnlimitedCredits = true;
                    so.cargoTrade.sellAllOnArrival = true;
                    so.cargoTrade.buyConfiguredItemsAfterSell = true;
                    so.cargoTrade.maxLoadSlots = maxLoadSlots;
                    so.cargoTrade.maxLoadWeightKg = maxLoadWeightKg;
                    so.cargoTrade.buyItems = items;

                    EditorUtility.SetDirty(so);
                    totalChanged++;
                    Debug.Log($"[NpcShipScheduleCargoHelper] Filled '{so.name}' at {path}: " +
                              $"items={items.Length}, maxLoadSlots={maxLoadSlots}, maxLoadWeightKg={maxLoadWeightKg}kg");
                }
            }

            if (totalChanged > 0)
                AssetDatabase.SaveAssets();

            Debug.Log($"[NpcShipScheduleCargoHelper] Done. {totalChanged} asset(s) updated.");
        }

        [MenuItem("Tools/ProjectC/PeacefulShip/Validate NpcShipSchedule Cargo Trade")]
        public static void ValidateSchedules()
        {
            var tradeDb = LoadTradeDatabaseOrNull();
            var validItemIds = new HashSet<string>();
            if (tradeDb != null && tradeDb.allItems != null)
            {
                foreach (var def in tradeDb.allItems)
                {
                    if (def != null && !string.IsNullOrEmpty(def.itemId))
                        validItemIds.Add(def.itemId);
                }
            }
            else
            {
                Debug.LogWarning("[NpcShipScheduleCargoHelper] TradeDatabase not found — skipping itemId validation. " +
                                 "Use Assets > Create > ProjectC > Trade Database if not present.");
            }

            string[] guids = AssetDatabase.FindAssets($"t:NpcShipSchedule");
            int issues = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<NpcShipSchedule>(path);
                if (so == null || so.cargoTrade == null) continue;
                if (so.cargoTrade.buyItems == null) continue;

                foreach (var item in so.cargoTrade.buyItems)
                {
                    if (string.IsNullOrEmpty(item.itemId))
                    {
                        Debug.LogError($"[NpcShipScheduleCargoHelper] {so.name}.cargoTrade.buyItems: empty itemId", so);
                        issues++;
                        continue;
                    }
                    if (validItemIds.Count > 0 && !validItemIds.Contains(item.itemId))
                    {
                        Debug.LogError($"[NpcShipScheduleCargoHelper] {so.name}.cargoTrade.buyItems: " +
                                       $"itemId '{item.itemId}' not in TradeDatabase", so);
                        issues++;
                    }
                }
            }

            Debug.Log($"[NpcShipScheduleCargoHelper] Validation done. {issues} issue(s) found.");
        }

        private static TradeDatabase LoadTradeDatabaseOrNull()
        {
            // TradeDatabase не имеет статического singleton. Ищем в Resources/ через AssetDatabase.
            string[] guids = AssetDatabase.FindAssets("t:TradeDatabase");
            if (guids == null || guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TradeDatabase>(path);
        }
    }
}
#endif
