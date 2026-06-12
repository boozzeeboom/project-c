// =====================================================================================
// ResourcesCsvCrossValidator.cs — кросс-валидация между блоками Resources CSV
// =====================================================================================
// Документация:
//   • docs/Markets/Resources_import_export/02_DESIGN.md §3.4
//   • docs/Markets/Resources_import_export/03_TICKETS.md T-IE02
//
// Назначение: проверяет кросс-ссылки между блоками ПОСЛЕ парсинга (T-IE01).
// Per-row errors накапливаются в row.errors — импортёр skip'ает плохие строки.
// Все ошибки теперь per-row, globalErrors НЕ используются (больше не блокируют).
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
        /// Проверяет кросс-ссылки между блоками. Все ошибки — per-row (row.errors).
        /// globalErrors больше не пополняется — импорт НЕ блокируется.
        /// </summary>
        public static void Validate(
            Dictionary<string, List<ResourcesCsvRow>> blocks,
            List<string> globalErrors)  // kept for backward-compat with Window, but not added to
        {
            // 1. Build indices (skip rows that already have errors — no point cascading).
            var invNames = new Dictionary<string, ResourcesCsvRow>(StringComparer.OrdinalIgnoreCase);
            var tradeIds = new Dictionary<string, ResourcesCsvRow>(StringComparer.OrdinalIgnoreCase);

            // 2. inventory: duplicate itemName → per-row error on BOTH occurrences (keep first wins).
            if (blocks.TryGetValue("inventory", out var inv))
            {
                var seen = new Dictionary<string, ResourcesCsvRow>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in inv)
                {
                    if (r.HasError) continue;
                    var name = r.Get("itemName");
                    if (string.IsNullOrEmpty(name)) continue;
                    if (seen.TryGetValue(name, out var first))
                    {
                        r.errors.Add($"Duplicate itemName '{name}' (first at line {first.lineNumber}). Skipping this row.");
                        // Don't mark first row as error — it's the canonical entry.
                    }
                    else
                    {
                        seen[name] = r;
                        invNames[name] = r;
                    }
                }
            }

            // 3. tradeItems: duplicate tradeItemId → per-row error.
            if (blocks.TryGetValue("tradeItems", out var trd))
            {
                var seen = new Dictionary<string, ResourcesCsvRow>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in trd)
                {
                    if (r.HasError) continue;
                    var id = r.Get("tradeItemId");
                    if (string.IsNullOrEmpty(id)) continue;
                    if (seen.TryGetValue(id, out var first))
                    {
                        r.errors.Add($"Duplicate tradeItemId '{id}' (first at line {first.lineNumber}). Skipping this row.");
                    }
                    else
                    {
                        seen[id] = r;
                        tradeIds[id] = r;
                    }
                }
            }

            // 4. marketItems: tradeItemId must exist in tradeItems.
            if (blocks.TryGetValue("marketItems", out var mkt))
            {
                foreach (var r in mkt)
                {
                    if (r.HasError) continue;
                    var tid = r.Get("tradeItemId");
                    if (!string.IsNullOrEmpty(tid) && !tradeIds.ContainsKey(tid))
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

                    if (!string.IsNullOrEmpty(tid) && !tradeIds.ContainsKey(tid))
                        r.errors.Add($"Line {r.lineNumber} (exchangeRates): tradeItemId '{tid}' not in tradeItems block");

                    if (!string.IsNullOrEmpty(inm) && !invNames.ContainsKey(inm))
                        r.errors.Add($"Line {r.lineNumber} (exchangeRates): inventoryItemName '{inm}' not in inventory block");
                }
            }

            // 6. recipes — Phase 2 placeholder.
            //    Парсер уже выдал global error "Unknown column" если есть лишние колонки.
            //    Здесь только фиксируем факт наличия секции (для UI в T-IE06 Window).
            //    Реальная валидация ингредиентов/outputs — CraftingCsvValidator.

            // 7. T-IE08: prune block — validate mode + applyTo (per-row, non-blocking).
            if (blocks.TryGetValue("prune", out var pruneRows) && pruneRows.Count > 0)
            {
                var firstRow = pruneRows[0];
                if (!firstRow.HasError)
                {
                    var mode = (firstRow.Get("mode") ?? "").Trim().ToLowerInvariant();
                    if (mode != "none" && mode != "orphan" && mode != "replace")
                        firstRow.errors.Add($"Invalid prune mode '{firstRow.Get("mode")}'. Valid: none, orphan, replace.");

                    var applyTo = (firstRow.Get("applyTo") ?? "all").Trim().ToLowerInvariant();
                    var tokens = applyTo.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var validTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        { "all", "inventory", "tradeItems", "marketItems", "exchangeRates" };
                    var seenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var t in tokens)
                    {
                        var tk = t.Trim();
                        if (tk.Length == 0) continue;
                        if (!seenTokens.Add(tk))
                            firstRow.errors.Add($"Duplicate prune applyTo token '{tk}'.");
                        else if (!validTokens.Contains(tk))
                            firstRow.errors.Add($"Invalid prune applyTo token '{tk}'. Valid: all, inventory, tradeItems, marketItems, exchangeRates.");
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
            int totalErrors = 0;
            foreach (var name in new[] { "inventory", "tradeItems", "marketItems", "exchangeRates", "recipes" })
            {
                if (!blocks.TryGetValue(name, out var rows)) continue;
                int errs = rows.Count(r => r.HasError);
                totalErrors += errs;
                string suffix = errs > 0 ? $" ({errs} errors)" : "";
                sb.AppendLine($"{name}: {rows.Count} rows{suffix}");
            }
            if (totalErrors > 0)
                sb.AppendLine($"⚠ {totalErrors} row(s) with errors — will be SKIPPED during import.");
            return sb.ToString();
        }
    }
}
#endif
