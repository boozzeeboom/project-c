// =====================================================================================
// ResourcesCsvCrossValidator.cs — кросс-валидация между блоками Resources CSV
// =====================================================================================
// Документация:
//   • docs/Markets/Resources_import_export/02_DESIGN.md §3.4
//   • docs/Markets/Resources_import_export/03_TICKETS.md T-IE02
//
// Назначение: проверяет кросс-ссылки между блоками ПОСЛЕ парсинга (T-IE01).
// Per-row errors накапливаются в row.errors (импортёр их прочитает и skip).
// Global errors — блокируют импорт (например, missing required column).
//
// MVP проверки (4 шт.):
//   1. inventory: уникальность itemName (case-insensitive)
//   2. tradeItems: уникальность tradeItemId (case-insensitive)
//   3. marketItems: tradeItemId существует в tradeItems
//   4. exchangeRates: tradeItemId существует в tradeItems AND inventoryItemName в inventory
//
// recipes — Phase 2 placeholder: парсер распознаёт секцию, но валидация не выполняется.
// В Phase 2 будет отдельный CraftingCsvValidator.
//
// Прецедент: QuestCsvValidator.CrossValidate (Assets/_Project/Quests/Editor/QuestCsvSchema.cs:347).
//
// T-IE02 (2026-06-11): MVP. ~130 LOC.
// =====================================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectC.Items.Editor
{
    public static class ResourcesCsvCrossValidator
    {
        /// <summary>
        /// Проверяет кросс-ссылки между блоками. Per-row errors → row.errors;
        /// global errors → globalErrors list.
        /// </summary>
        public static void Validate(
            Dictionary<string, List<ResourcesCsvRow>> blocks,
            List<string> globalErrors)
        {
            // 1. Build indices (skip rows that already have errors — no point cascading).
            var invNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tradeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (blocks.TryGetValue("inventory", out var inv))
                foreach (var r in inv) if (!r.HasError) invNames.Add(r.Get("itemName"));

            if (blocks.TryGetValue("tradeItems", out var trd))
                foreach (var r in trd) if (!r.HasError) tradeIds.Add(r.Get("tradeItemId"));

            // 2. inventory: duplicate itemName.
            if (blocks.TryGetValue("inventory", out var inv2))
            {
                var dupNames = inv2
                    .Where(r => !r.HasError)
                    .GroupBy(r => r.Get("itemName"), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
                    .Select(g => g.Key);
                foreach (var d in dupNames)
                    globalErrors.Add($"inventory: duplicate itemName '{d}'");
            }

            // 3. tradeItems: duplicate tradeItemId.
            if (blocks.TryGetValue("tradeItems", out var trd2))
            {
                var dupIds = trd2
                    .Where(r => !r.HasError)
                    .GroupBy(r => r.Get("tradeItemId"), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
                    .Select(g => g.Key);
                foreach (var d in dupIds)
                    globalErrors.Add($"tradeItems: duplicate tradeItemId '{d}'");
            }

            // 4. marketItems: tradeItemId must exist in tradeItems.
            if (blocks.TryGetValue("marketItems", out var mkt))
            {
                foreach (var r in mkt)
                {
                    if (r.HasError) continue;
                    var tid = r.Get("tradeItemId");
                    if (!string.IsNullOrEmpty(tid) && !tradeIds.Contains(tid))
                        r.errors.Add($"Line {r.lineNumber} (marketItems): tradeItemId '{tid}' not in tradeItems block");
                }
            }

            // 5. exchangeRates: tradeItemId ∈ tradeItems AND inventoryItemName ∈ inventory.
            if (blocks.TryGetValue("exchangeRates", out var xch))
            {
                foreach (var r in xch)
                {
                    if (r.HasError) continue;
                    var tid = r.Get("tradeItemId");
                    var inm = r.Get("inventoryItemName");

                    if (!string.IsNullOrEmpty(tid) && !tradeIds.Contains(tid))
                        r.errors.Add($"Line {r.lineNumber} (exchangeRates): tradeItemId '{tid}' not in tradeItems block");

                    if (!string.IsNullOrEmpty(inm) && !invNames.Contains(inm))
                        r.errors.Add($"Line {r.lineNumber} (exchangeRates): inventoryItemName '{inm}' not in inventory block");
                }
            }

            // 6. recipes — Phase 2 placeholder.
            //    Парсер уже выдал global error "Unknown column" если есть лишние колонки.
            //    Здесь только фиксируем факт наличия секции (для UI в T-IE06 Window).
            //    Реальная валидация ингредиентов/outputs — CraftingCsvValidator.

            // 7. T-IE08: prune block — validate mode + applyTo.
            if (blocks.TryGetValue("prune", out var pruneRows) && pruneRows.Count > 0)
            {
                var firstRow = pruneRows[0];
                if (!firstRow.HasError)
                {
                    var mode = (firstRow.Get("mode") ?? "").Trim().ToLowerInvariant();
                    if (mode != "none" && mode != "orphan" && mode != "replace")
                        globalErrors.Add($"prune: invalid mode '{firstRow.Get("mode")}'. Valid: none, orphan, replace.");

                    var applyTo = (firstRow.Get("applyTo") ?? "all").Trim().ToLowerInvariant();
                    var tokens = applyTo.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                    var validTokens = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                        { "all", "inventory", "tradeItems", "marketItems", "exchangeRates" };
                    var seenTokens = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    foreach (var t in tokens)
                    {
                        var tk = t.Trim();
                        if (tk.Length == 0) continue;
                        if (!seenTokens.Add(tk))
                        {
                            globalErrors.Add($"prune: duplicate applyTo token '{tk}'");
                            continue;
                        }
                        if (!validTokens.Contains(tk))
                            globalErrors.Add($"prune: invalid applyTo token '{tk}'. Valid: all, inventory, tradeItems, marketItems, exchangeRates.");
                    }
                }
            }
        }

        /// <summary>
        /// Считает статистику для Preview UI: rows per block + errors per row.
        /// </summary>
        public static string Summary(Dictionary<string, List<ResourcesCsvRow>> blocks)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var name in new[] { "inventory", "tradeItems", "marketItems", "exchangeRates", "recipes" })
            {
                if (!blocks.TryGetValue(name, out var rows)) continue;
                int errs = rows.Count(r => r.HasError);
                string suffix = errs > 0 ? $" ({errs} errors)" : "";
                sb.AppendLine($"{name}: {rows.Count} rows{suffix}");
            }
            return sb.ToString();
        }
    }
}
#endif
