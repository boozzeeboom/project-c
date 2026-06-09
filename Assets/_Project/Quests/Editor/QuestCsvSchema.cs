// M19-T1: QuestCsvSchema + QuestCsvParser + QuestCsvValidator
// Single-file CSV для content writer'ов (нетекнари).
// 1 строка = 1 objective (flat, denormalized).
// Все колонки в одном CSV. Обязательные: questId, displayName, stageNum, objectiveType, qty.
// Остальные — опционально.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ProjectC.Quests.Editor
{
    // ============================================================
    // 1. QuestCsvSchema — определение колонок
    // ============================================================

    public static class QuestCsvSchema
    {
        /// <summary>Описание одной колонки CSV.</summary>
        public class ColumnDef
        {
            public string name;          // каноническое имя (без пробелов)
            public string[] aliases;     // альтернативные имена (русские, с пробелами)
            public bool required;        // обязательная колонка
            public string defaultValue;  // значение по умолчанию
            public string type;          // "string", "int", "bool", "enum"
            public string description;   // human-readable описание
        }

        public static readonly ColumnDef[] Columns = new ColumnDef[]
        {
            new ColumnDef { name = "questId",         aliases = new[]{"quest id","quest_id","id","квест","квест id"},          required = true,  type = "string", description = "Уникальный ID квеста (латиница, без пробелов)" },
            new ColumnDef { name = "displayName",     aliases = new[]{"display name","display_name","name","название","имя"},   required = true,  type = "string", description = "Название квеста (игрок видит это)" },
            new ColumnDef { name = "description",     aliases = new[]{"desc","описание"},                                      required = false, type = "string", description = "Описание квеста (текст в журнале)" },
            new ColumnDef { name = "faction",         aliases = new[]{"фракция","factionid"},                                   required = false, type = "enum",  description = "Фракция (GuildOfThoughts, Neutral...)" },
            new ColumnDef { name = "oneShot",         aliases = new[]{"one shot","one_shot","одноразовый"},                    required = false, type = "bool",  defaultValue = "n", description = "Одноразовый квест (y/n)" },
            new ColumnDef { name = "prereqQuest",     aliases = new[]{"prerequisite","prereq","требует","нужен"},              required = false, type = "string", description = "Какой квест нужно завершить сначала (questId)" },
            new ColumnDef { name = "stageNum",        aliases = new[]{"stage num","stage_num","stage","этап","номер этапа"},   required = true,  type = "int",   description = "Номер этапа (0, 1, 2...)" },
            new ColumnDef { name = "stageId",         aliases = new[]{"stage id","stage_id","этап id"},                        required = false, type = "string", description = "ID этапа (латиница, опционально)" },
            new ColumnDef { name = "stageDescription",aliases = new[]{"stage description","stage_desc","описание этапа"},       required = false, type = "string", description = "Описание этапа" },
            new ColumnDef { name = "onEnterActions",  aliases = new[]{"on enter","on_enter","onEnter","при входе"},               required = false, type = "string", description = "Действия при входе в этап (ActionType:p1:p2;...)" },
            new ColumnDef { name = "objectiveType",   aliases = new[]{"objective type","objective_type","obj type","тип цели","тип"}, required = true, type = "string", description = "Тип цели: HaveItem, TalkToNpc, StandOnTrigger" },
            new ColumnDef { name = "objectiveId",     aliases = new[]{"objective id","objective_id","obj id","obj_id","цель id","id цели"}, required = false, type = "string", description = "ID цели" },
            new ColumnDef { name = "itemName",        aliases = new[]{"item name","item_name","item","предмет"},               required = false, type = "string", description = "Название предмета (для HaveItem)" },
            new ColumnDef { name = "npcId",           aliases = new[]{"npc id","npc_id","npc","NPC","персонаж"},              required = false, type = "string", description = "ID NPC (для TalkToNpc)" },
            new ColumnDef { name = "qty",             aliases = new[]{"quantity","кол-во","количество","count"},               required = true,  type = "int",   defaultValue = "1", description = "Сколько нужно (3 руды, 1 поговорить)" },
            new ColumnDef { name = "onCompleteActions",aliases = new[]{"on complete","on_complete","onComplete","при завершении"},  required = false, type = "string", description = "Действия при завершении этапа" },
            new ColumnDef { name = "rewardCR",        aliases = new[]{"reward cr","reward_cr","reward","credits","награда cr","кредиты"}, required = false, type = "int",   defaultValue = "0", description = "Награда кредитами" },
            new ColumnDef { name = "rewardRep",       aliases = new[]{"reward rep","reward_rep","reward reputation","награда реп","репутация"}, required = false, type = "string", description = "Награда репутацией (FactionId:value)" },
            new ColumnDef { name = "rewardItem",      aliases = new[]{"reward item","reward_item","награда предмет"},          required = false, type = "string", description = "Награда предметом (itemName:count)" },
        };

        /// <summary>Найти каноническое имя колонки по любому алиасу (case-insensitive).</summary>
        public static string ResolveColumnName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return null;
            var lower = rawName.Trim().ToLowerInvariant();
            foreach (var col in Columns)
            {
                if (col.name.ToLowerInvariant() == lower) return col.name;
                foreach (var alias in col.aliases)
                    if (alias.ToLowerInvariant() == lower) return col.name;
            }
            return null;
        }

        public static ColumnDef GetColumn(string name) => Columns.FirstOrDefault(c => c.name == name);
    }

    // ============================================================
    // 2. QuestCsvRow — одна строка после парсинга
    // ============================================================

    public class QuestCsvRow
    {
        /// <summary>Сырые значения (ключ = каноническое имя колонки).</summary>
        public Dictionary<string, string> values = new Dictionary<string, string>();

        /// <summary>Номер строки (1-based, включая header).</summary>
        public int lineNumber;

        /// <summary>Ошибки валидации этой строки.</summary>
        public List<string> errors = new List<string>();

        public string Get(string column) => values.TryGetValue(column, out var v) ? v ?? "" : "";
        public int GetInt(string column, int defaultVal = 0) => int.TryParse(Get(column), out var v) ? v : defaultVal;
        public bool GetBool(string column) { var v = Get(column).ToLowerInvariant(); return v == "y" || v == "yes" || v == "true" || v == "1"; }

        public bool HasError => errors.Count > 0;
    }

    // ============================================================
    // 3. QuestCsvParser — парсинг CSV файла
    // ============================================================

    public static class QuestCsvParser
    {
        /// <summary>Парсит CSV файл. Возвращает строки + заголовок.</summary>
        public static (List<QuestCsvRow> rows, string[] header, List<string> globalErrors) ParseFile(string path)
        {
            var globalErrors = new List<string>();
            if (!File.Exists(path))
            {
                globalErrors.Add($"File not found: {path}");
                return (new List<QuestCsvRow>(), new string[0], globalErrors);
            }

            try
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                return ParseText(text, path);
            }
            catch (Exception e)
            {
                globalErrors.Add($"Error reading file: {e.Message}");
                return (new List<QuestCsvRow>(), new string[0], globalErrors);
            }
        }

        /// <summary>Парсит CSV текст (полный парсинг с поддержкой quoted fields).</summary>
        public static (List<QuestCsvRow> rows, string[] header, List<string> globalErrors) ParseText(string csvText, string sourceName = "<inline>")
        {
            var globalErrors = new List<string>();
            var rows = new List<QuestCsvRow>();

            // Split into lines, handling quoted multiline fields
            var lines = SplitCsvLines(csvText);
            if (lines.Count < 2)
            {
                globalErrors.Add($"CSV must have at least a header row and one data row. Found {lines.Count} lines.");
                return (rows, new string[0], globalErrors);
            }

            // Parse header
            var rawHeader = ParseCsvLine(lines[0]);
            var header = new string[rawHeader.Length];
            var columnMap = new Dictionary<string, int>(); // canonical name → index

            for (int i = 0; i < rawHeader.Length; i++)
            {
                var canonical = QuestCsvSchema.ResolveColumnName(rawHeader[i]);
                if (canonical == null)
                {
                    globalErrors.Add($"Line 1 (header): Unknown column '{rawHeader[i]}'. Ignored.");
                    header[i] = rawHeader[i]; // keep as-is
                }
                else
                {
                    header[i] = canonical;
                    if (!columnMap.ContainsKey(canonical))
                        columnMap[canonical] = i;
                }
            }

            // Check required columns
            foreach (var col in QuestCsvSchema.Columns)
            {
                if (col.required && !columnMap.ContainsKey(col.name))
                    globalErrors.Add($"Missing required column: '{col.name}' ({col.description})");
            }

            // Parse data rows
            for (int lineIdx = 1; lineIdx < lines.Count; lineIdx++)
            {
                var line = lines[lineIdx];
                if (string.IsNullOrWhiteSpace(line)) continue; // skip empty

                var fields = ParseCsvLine(line);
                var row = new QuestCsvRow { lineNumber = lineIdx + 1 };

                // Fill values
                for (int i = 0; i < fields.Length && i < header.Length; i++)
                {
                    var colName = header[i];
                    if (colName != null && QuestCsvSchema.GetColumn(colName) != null)
                    {
                        row.values[colName] = fields[i].Trim();
                    }
                }

                // Apply defaults for empty fields
                foreach (var col in QuestCsvSchema.Columns)
                {
                    if (!string.IsNullOrEmpty(col.defaultValue) && string.IsNullOrEmpty(row.Get(col.name)))
                        row.values[col.name] = col.defaultValue;
                }

                // Basic validation
                ValidateRow(row);

                rows.Add(row);
            }

            // Validate quest structure
            if (rows.Count > 0)
                ValidateStructure(rows, globalErrors);

            return (rows, header, globalErrors);
        }

        private static void ValidateRow(QuestCsvRow row)
        {
            // Required fields not empty
            var requiredChecks = new (string col, string label)[]
            {
                ("questId", "Quest ID"),
                ("displayName", "Name"),
                ("stageNum", "Stage number"),
                ("objectiveType", "Objective type"),
            };
            foreach (var (col, label) in requiredChecks)
            {
                if (string.IsNullOrWhiteSpace(row.Get(col)))
                    row.errors.Add($"Line {row.lineNumber}: '{label}' is required");
            }

            // stageNum must be int >= 0
            var stageNum = row.Get("stageNum");
            if (!string.IsNullOrEmpty(stageNum) && (!int.TryParse(stageNum, out var sn) || sn < 0))
                row.errors.Add($"Line {row.lineNumber}: Stage number '{stageNum}' is not a valid non-negative integer");

            // qty must be int > 0
            var qty = row.Get("qty");
            if (!string.IsNullOrEmpty(qty) && (!int.TryParse(qty, out var qv) || qv <= 0))
                row.errors.Add($"Line {row.lineNumber}: Quantity '{qty}' is not a valid positive integer");

            // rewardCR must be int >= 0
            var cr = row.Get("rewardCR");
            if (!string.IsNullOrEmpty(cr) && (!int.TryParse(cr, out var cv) || cv < 0))
                row.errors.Add($"Line {row.lineNumber}: Rewards CR '{cr}' is not a valid non-negative integer");

            // objectiveType must be a valid enum (case-insensitive)
            var objType = row.Get("objectiveType");
            if (!string.IsNullOrEmpty(objType))
            {
                var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "haveitem", "talktonpc", "standontrigger", "completeobjective",
                    "killedentity", "eventdriven", "cargohasitem"
                };
                if (!validTypes.Contains(objType.Trim()))
                    row.errors.Add($"Line {row.lineNumber}: Unknown objective type '{objType}'. Valid: HaveItem, TalkToNpc, StandOnTrigger, CompleteObjective, KilledEntity, EventDriven");
            }
        }

        /// <summary>Групповая валидация: questId уникальность, stageNum последовательность.</summary>
        private static void ValidateStructure(List<QuestCsvRow> rows, List<string> globalErrors)
        {
            // Multi-stage quests use the same questId across rows — that's expected.
            // Only flag if same questId appears with DIFFERENT displayNames (true duplicate intent).
            var questNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var qid = row.Get("questId");
                if (string.IsNullOrEmpty(qid)) continue;
                var name = row.Get("displayName");
                if (questNames.TryGetValue(qid, out var prevName) && prevName != name)
                {
                    globalErrors.Add($"Duplicate questId '{qid}' with different displayNames: '{prevName}' vs '{name}'. Did you mean two different quests?");
                }
                else if (!questNames.ContainsKey(qid))
                {
                    questNames[qid] = name;
                }
            }

            // per-quest: stage numbers should be sequential
            var questGroups = rows.Where(r => !string.IsNullOrEmpty(r.Get("questId")))
                                  .GroupBy(r => r.Get("questId"), StringComparer.OrdinalIgnoreCase);
            foreach (var group in questGroups)
            {
                var stages = group.Select(r => r.GetInt("stageNum")).Distinct().OrderBy(x => x).ToList();
                if (stages.Count > 1)
                {
                    for (int i = 1; i < stages.Count; i++)
                    {
                        if (stages[i] != stages[i - 1] + 1)
                            globalErrors.Add($"Quest '{group.Key}': stage numbers are not sequential ({string.Join(", ", stages)}). Expected {stages[i-1]+1} after stage {stages[i-1]}.");
                    }
                }
            }
        }

        // ============================================================
        // CSV line parser (handles quoted fields, escaped quotes)
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

    // ============================================================
    // 4. QuestCsvValidator — пост-парсинг: кросс-ссылки, структура
    // ============================================================

    public static class QuestCsvValidator
    {
        /// <summary>Пост-валидация: проверка ссылок на NPC, ItemRegistry, FactionId.</summary>
        public static void CrossValidate(List<QuestCsvRow> rows, List<string> globalErrors)
        {
            foreach (var row in rows)
            {
                if (row.HasError) continue; // skip rows with basic errors

                // itemName → ItemRegistry lookup
                var itemName = row.Get("itemName");
                if (!string.IsNullOrEmpty(itemName))
                {
                    if (ResolveItem(itemName) == 0)
                        row.errors.Add($"Item '{itemName}' not found in ItemRegistry. Line {row.lineNumber}");
                }

                // npcId → NpcDefinition lookup  
                var npcId = row.Get("npcId");
                if (!string.IsNullOrEmpty(npcId))
                {
                    if (!ResolveNpc(npcId))
                        row.errors.Add($"NPC '{npcId}' not found in NpcDefinition. Line {row.lineNumber}");
                }

                // rewardRep → FactionId lookup
                var rep = row.Get("rewardRep");
                if (!string.IsNullOrEmpty(rep))
                {
                    var parts = rep.Split(':');
                    if (parts.Length >= 1 && !ResolveFaction(parts[0].Trim()))
                        row.errors.Add($"Faction '{parts[0]}' not found in FactionDefinition. Line {row.lineNumber}");
                }
            }
        }

        // Resolve using the current project context (via reflection-safe calls)
        private static int ResolveItem(string name) => ProjectC.Quests.QuestWorld.ResolveItemId(name);
        private static bool ResolveNpc(string npcId) { /* stubs — runtime context */ return true; }
        private static bool ResolveFaction(string faction) { /* stubs */ return true; }
    }
}
#endif
