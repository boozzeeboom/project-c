// =====================================================================================
// ResourcesCsvParser.cs — парсер CSV в 4 MVP-блока (inventory/tradeItems/marketItems/exchangeRates)
// =====================================================================================
// Документация:
//   • docs/Markets/Resources_import_export/02_DESIGN.md §1, §3
//   • docs/Markets/Resources_import_export/03_TICKETS.md T-IE01
//
// Назначение: разбирает CSV-файл с маркерами "# block=<name>" на Dictionary<block, rows>.
// Поддерживает:
//   • UTF-8 with BOM (BOM strip'ается автоматически)
//   • Quoted fields ("...")
//   • Escaped quotes ("")
//   • Multiline fields (если внутри кавычек)
//   • # comments (строки начинающиеся с #, кроме "# block=..." маркеров)
//   • Empty lines (между секциями)
//
// recipes (Phase 2) — секция распознаётся, но rows не валидируются.
// Импорт recipes — отдельный CraftingCsvImporter.
//
// Прецедент: QuestCsvParser (Assets/_Project/Quests/Editor/QuestCsvSchema.cs) —
// SplitCsvLines и ParseCsvLine скопированы без изменений.
//
// T-IE01 (2026-06-11): MVP. ~200 LOC.
// =====================================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ProjectC.Items.Editor
{
    public static class ResourcesCsvParser
    {
        /// <summary>
        /// Парсит CSV-файл. Возвращает dictionary: blockName → list of ResourcesCsvRow.
        /// Также возвращает global errors (отсутствующие required columns, etc).
        /// </summary>
        public static (Dictionary<string, List<ResourcesCsvRow>> blocks,
                       List<string> globalErrors)
            ParseFile(string path)
        {
            var globalErrors = new List<string>();
            if (!File.Exists(path))
            {
                globalErrors.Add($"File not found: {path}");
                return (new Dictionary<string, List<ResourcesCsvRow>>(), globalErrors);
            }

            try
            {
                // UTF-8 with BOM (default for Excel exports).
                var text = File.ReadAllText(path, new UTF8Encoding(true));
                return ParseText(text, path);
            }
            catch (System.Exception e)
            {
                globalErrors.Add($"Error reading file: {e.Message}");
                return (new Dictionary<string, List<ResourcesCsvRow>>(), globalErrors);
            }
        }

        /// <summary>
        /// Парсит CSV-текст напрямую (для тестов или round-trip).
        /// </summary>
        public static (Dictionary<string, List<ResourcesCsvRow>> blocks,
                       List<string> globalErrors)
            ParseText(string csvText, string sourceName = "<inline>")
        {
            var globalErrors = new List<string>();
            var blocks = new Dictionary<string, List<ResourcesCsvRow>>();

            // Strip UTF-8 BOM if present.
            if (!string.IsNullOrEmpty(csvText) && csvText[0] == '\uFEFF')
                csvText = csvText.Substring(1);

            var lines = SplitCsvLines(csvText);
            if (lines.Count == 0)
            {
                globalErrors.Add("CSV file is empty.");
                return (blocks, globalErrors);
            }

            string currentBlock = null;
            int blockLine = 0;  // line index within current block (0 = header)
            string[] header = null;
            var columnMap = new Dictionary<string, int>();
            BlockSchema currentSchema = null;

            for (int i = 0; i < lines.Count; i++)
            {
                var rawLine = lines[i];
                var line = rawLine.Trim();

                // Skip empty lines.
                if (string.IsNullOrEmpty(line)) continue;

                // Skip pure comments (lines starting with # but NOT "# block=...").
                if (line.StartsWith("#") && !line.StartsWith("# block=", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                // Block marker: "# block=<name>".
                if (line.StartsWith("# block=", System.StringComparison.OrdinalIgnoreCase))
                {
                    currentBlock = line.Substring("# block=".Length).Trim();
                    if (!blocks.ContainsKey(currentBlock))
                        blocks[currentBlock] = new List<ResourcesCsvRow>();

                    blockLine = 0;
                    header = null;
                    columnMap.Clear();
                    currentSchema = ResourcesCsvSchema.GetBlockSchema(currentBlock);
                    if (currentSchema == null)
                    {
                        globalErrors.Add($"Line {i + 1}: Unknown block '{currentBlock}'. " +
                                         "Valid blocks: inventory, tradeItems, marketItems, exchangeRates (recipes — Phase 2).");
                    }
                    continue;
                }

                if (currentBlock == null)
                {
                    globalErrors.Add($"Line {i + 1}: data before first '# block=' marker. Ignored.");
                    continue;
                }

                if (currentSchema == null)
                {
                    // Unknown block — skip its body (already warned above).
                    continue;
                }

                var fields = ParseCsvLine(line);

                // Header (first non-empty line after # block=).
                if (blockLine == 0)
                {
                    header = new string[fields.Length];
                    for (int j = 0; j < fields.Length; j++)
                    {
                        var canonical = currentSchema.ResolveColumnName(fields[j]);
                        if (canonical == null)
                        {
                            globalErrors.Add($"Line {i + 1} (header of '{currentBlock}'): " +
                                              $"Unknown column '{fields[j]}'. Ignored.");
                            header[j] = fields[j]; // keep as-is for error reporting
                        }
                        else
                        {
                            header[j] = canonical;
                            if (!columnMap.ContainsKey(canonical))
                                columnMap[canonical] = j;
                        }
                    }

                    // Check required columns.
                    foreach (var col in currentSchema.Columns.Where(c => c.required))
                    {
                        if (!columnMap.ContainsKey(col.name))
                            globalErrors.Add($"Block '{currentBlock}': missing required column " +
                                             $"'{col.name}' ({col.description})");
                    }
                }
                else
                {
                    // Data row.
                    var row = new ResourcesCsvRow
                    {
                        block = currentBlock,
                        lineNumber = i + 1,
                    };

                    for (int j = 0; j < fields.Length && j < header.Length; j++)
                    {
                        var colName = header[j];
                        if (colName != null && currentSchema.GetColumn(colName) != null)
                        {
                            row.values[colName] = fields[j].Trim();
                        }
                    }

                    // Apply defaults for empty fields.
                    foreach (var col in currentSchema.Columns)
                    {
                        if (!string.IsNullOrEmpty(col.defaultValue) && string.IsNullOrEmpty(row.Get(col.name)))
                        {
                            row.values[col.name] = col.defaultValue;
                        }
                    }

                    // Basic type/required validation.
                    ValidateRow(row, currentSchema);

                    blocks[currentBlock].Add(row);
                }

                blockLine++;
            }

            return (blocks, globalErrors);
        }

        // ============================================================
        // Per-row validation
        // ============================================================

        private static void ValidateRow(ResourcesCsvRow row, BlockSchema schema)
        {
            // Required fields not empty.
            foreach (var col in schema.Columns.Where(c => c.required))
            {
                if (string.IsNullOrWhiteSpace(row.Get(col.name)))
                    row.errors.Add($"Line {row.lineNumber}: '{col.name}' is required ({col.description})");
            }

            // Type checks (only on non-empty values).
            foreach (var col in schema.Columns)
            {
                var v = row.Get(col.name);
                if (string.IsNullOrEmpty(v)) continue;

                switch (col.type)
                {
                    case "int":
                        if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv) || iv < 0)
                            row.errors.Add($"Line {row.lineNumber}: '{col.name}' = '{v}' is not a non-negative int");
                        break;

                    case "float":
                        if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv) || fv < 0f)
                            row.errors.Add($"Line {row.lineNumber}: '{col.name}' = '{v}' is not a non-negative float");
                        break;

                    case "bool":
                        var lv = v.ToLowerInvariant().Trim();
                        if (lv != "y" && lv != "n" && lv != "yes" && lv != "no" && lv != "true" && lv != "false" && lv != "1" && lv != "0")
                            row.errors.Add($"Line {row.lineNumber}: '{col.name}' = '{v}' is not y/n/yes/no/true/false/1/0");
                        break;

                    case "enum":
                        if (col.enumType != null)
                        {
                            if (!System.Enum.TryParse(col.enumType, v, true, out _))
                                row.errors.Add($"Line {row.lineNumber}: '{col.name}' = '{v}' is not valid {col.enumType.Name}");
                        }
                        break;
                }
            }
        }

        // ============================================================
        // CSV line parser (handles quoted fields, escaped quotes)
        // Копия из QuestCsvParser — общий utility. В будущем — вынести в
        // Assets/_Project/Editor/Utility/CsvUtils.cs (Phase 2 refactor).
        // ============================================================

        private static List<string> SplitCsvLines(string text)
        {
            var lines = new List<string>();
            var currentLine = new StringBuilder();
            bool inQuotes = false;
            foreach (char c in text)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (c == '\n' && !inQuotes)
                {
                    lines.Add(currentLine.ToString().TrimEnd('\r'));
                    currentLine.Clear();
                    continue;
                }
                currentLine.Append(c);
            }
            if (currentLine.Length > 0) lines.Add(currentLine.ToString().TrimEnd('\r'));
            return lines;
        }

        private static string[] ParseCsvLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return new string[0];
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"'); // escaped quote
                        i++;
                    }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }
    }
}
#endif
