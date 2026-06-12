// T-Q19.3: NpcCsvImporter — CSV → NpcDefinition updates (services, attitude, greeting, voice).
// 1 строка = 1 NPC. Колонки: npcId обязательно, остальные опционально.
// Пропускает поля, которые не указаны (не перезаписывает).

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    public static class NpcCsvImporter
    {
        private const string NPCS_FOLDER = "Assets/_Project/Quests/Data/Npcs";

        public class ImportResult
        {
            public int npcsUpdated;
            public int npcsSkipped;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        // ============================================================
        // Schema
        // ============================================================

        private class ColumnDef
        {
            public string name;
            public string[] aliases;
            public bool required;
            public string description;
        }

        private static readonly ColumnDef[] Columns = new ColumnDef[]
        {
            new ColumnDef { name = "npcId",            aliases = new[]{"npc id","npc_id","id","npc"},     required = true,  description = "NPC ID (must exist in NpcDefinition assets)" },
            new ColumnDef { name = "services",         aliases = new[]{"service","сервисы"},             required = false, description = "Битовая маска: Trade, Repair, Refuel, Restock, Banking, Healing (через ;)" },
            new ColumnDef { name = "attitudeLinks",    aliases = new[]{"attitude_links","attitudes","отношения"}, required = false, description = "Faction:delta;Faction:delta (например Pirates:-15;Underground:5)" },
            new ColumnDef { name = "attitudeMin",      aliases = new[]{"attitude_min","att_min"},         required = false, description = "Минимальное значение NpcAttitude (-100..200)" },
            new ColumnDef { name = "attitudeMax",      aliases = new[]{"attitude_max","att_max"},         required = false, description = "Максимальное значение NpcAttitude (-100..200)" },
            new ColumnDef { name = "greetingText",     aliases = new[]{"greeting_text","greeting","приветствие"}, required = false, description = "Текст при подходе к NPC" },
            new ColumnDef { name = "voicePrefix",      aliases = new[]{"voice_prefix","voice"},          required = false, description = "Префикс для voice lines (audio)" },
            new ColumnDef { name = "interactionRadius",aliases = new[]{"interaction_radius","radius","радиус"}, required = false, description = "Радиус interact (метры)" },
            new ColumnDef { name = "showGreeting",     aliases = new[]{"show_greeting","show greeting"}, required = false, description = "Показывать ли greeting (y/n)" },
        };

        private static string ResolveColumnName(string rawName)
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

        // ============================================================
        // Row data
        // ============================================================

        private class NpcCsvRow
        {
            public int lineNumber;
            public Dictionary<string, string> values = new Dictionary<string, string>();
            public List<string> errors = new List<string>();
            public bool HasError => errors.Count > 0;
            public string Get(string col) => values.TryGetValue(col, out var v) ? v ?? "" : "";
        }

        // ============================================================
        // Public API
        // ============================================================

        public static ImportResult Import(string csvPath)
        {
            var result = new ImportResult();
            if (!File.Exists(csvPath))
            {
                result.errors.Add($"npcs.csv not found: {csvPath}");
                return result;
            }
            try
            {
                var text = File.ReadAllText(csvPath, Encoding.UTF8);
                return ImportText(text);
            }
            catch (Exception e)
            {
                result.errors.Add($"Error reading npcs.csv: {e.Message}");
                return result;
            }
        }

        public static ImportResult ImportText(string csvText)
        {
            var result = new ImportResult();
            var (rows, header, parseErrors) = ParseNpcCsv(csvText);
            result.errors.AddRange(parseErrors);
            if (rows.Count == 0) return result;

            foreach (var row in rows)
            {
                if (row.HasError) { result.npcsSkipped++; continue; }
                if (ApplyRow(row, result)) result.npcsUpdated++;
                else result.npcsSkipped++;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        // ============================================================
        // Apply to NPC asset
        // ============================================================

        private static bool ApplyRow(NpcCsvRow row, ImportResult result)
        {
            var npcId = row.Get("npcId");
            string assetPath = $"{NPCS_FOLDER}/{npcId}.asset";
            var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(assetPath);
            if (npc == null)
            {
                result.warnings.Add($"NPC '{npcId}' not found at {assetPath} (create via quests.csv first). Row skipped.");
                return false;
            }

            bool changed = false;

            // services
            var servicesStr = row.Get("services");
            if (!string.IsNullOrEmpty(servicesStr))
            {
                var newServices = ParseServices(servicesStr, out var parseErr);
                if (parseErr != null) result.warnings.Add($"Line {row.lineNumber}: {parseErr}");
                if (newServices != npc.services)
                {
                    npc.services = newServices;
                    changed = true;
                }
            }

            // attitudeLinks
            var linksStr = row.Get("attitudeLinks");
            if (!string.IsNullOrEmpty(linksStr))
            {
                var newLinks = ParseAttitudeLinks(linksStr, out var parseErr);
                if (parseErr != null) result.warnings.Add($"Line {row.lineNumber}: {parseErr}");
                if (!AttitudeLinksEqual(newLinks, npc.attitudeLinks))
                {
                    npc.attitudeLinks = newLinks;
                    changed = true;
                }
            }

            // attitudeMin/Max
            var minStr = row.Get("attitudeMin");
            if (!string.IsNullOrEmpty(minStr) && int.TryParse(minStr, out var minVal))
            {
                if (npc.personalAttitudeMin != minVal)
                {
                    npc.personalAttitudeMin = minVal;
                    changed = true;
                }
            }
            var maxStr = row.Get("attitudeMax");
            if (!string.IsNullOrEmpty(maxStr) && int.TryParse(maxStr, out var maxVal))
            {
                if (npc.personalAttitudeMax != maxVal)
                {
                    npc.personalAttitudeMax = maxVal;
                    changed = true;
                }
            }

            // greetingText
            var greetingStr = row.Get("greetingText");
            if (!string.IsNullOrEmpty(greetingStr) && npc.greetingText != greetingStr)
            {
                npc.greetingText = greetingStr;
                changed = true;
            }

            // voicePrefix
            var voiceStr = row.Get("voicePrefix");
            if (!string.IsNullOrEmpty(voiceStr) && npc.voicePrefix != voiceStr)
            {
                npc.voicePrefix = voiceStr;
                changed = true;
            }

            // interactionRadius
            var radStr = row.Get("interactionRadius");
            if (!string.IsNullOrEmpty(radStr) && float.TryParse(radStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rad))
            {
                if (Math.Abs(npc.interactionRadius - rad) > 0.001f)
                {
                    npc.interactionRadius = rad;
                    changed = true;
                }
            }

            // showGreeting
            var showStr = row.Get("showGreeting");
            if (!string.IsNullOrEmpty(showStr))
            {
                bool show = showStr.ToLowerInvariant() is "y" or "yes" or "true" or "1";
                if (npc.showGreeting != show)
                {
                    npc.showGreeting = show;
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(npc);
                Debug.Log($"[NpcCsvImporter] Updated NPC '{npcId}' (services/attitude/greeting/...)");
            }

            return true;
        }

        // ============================================================
        // Parsers
        // ============================================================

        private static NpcService ParseServices(string raw, out string error)
        {
            error = null;
            NpcService result = NpcService.None;
            var validNames = new[] { "Trade", "Repair", "Refuel", "Restock", "Banking", "Healing" };
            foreach (var token in raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                bool matched = false;
                foreach (var name in validNames)
                {
                    if (string.Equals(t, name, StringComparison.OrdinalIgnoreCase))
                    {
                        result |= (NpcService)Enum.Parse(typeof(NpcService), name);
                        matched = true;
                        break;
                    }
                }
                if (!matched) error = $"Unknown service '{t}'. Valid: {string.Join(", ", validNames)}";
            }
            return result;
        }

        private static AttitudeLink[] ParseAttitudeLinks(string raw, out string error)
        {
            error = null;
            var links = new List<AttitudeLink>();
            foreach (var token in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(':');
                if (parts.Length < 2)
                {
                    error = $"Invalid attitude link '{token}' (expected FactionId:delta)";
                    continue;
                }
                if (!Enum.TryParse<FactionId>(parts[0].Trim(), true, out var faction))
                {
                    error = $"Unknown faction '{parts[0]}' in attitude link";
                    continue;
                }
                if (!int.TryParse(parts[1].Trim(), out var delta))
                {
                    error = $"Invalid delta '{parts[1]}' in attitude link";
                    continue;
                }
                links.Add(new AttitudeLink { targetFaction = faction, deltaOnLike = delta, deltaOnDislike = -delta });
            }
            return links.ToArray();
        }

        private static bool AttitudeLinksEqual(AttitudeLink[] a, AttitudeLink[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].targetFaction != b[i].targetFaction) return false;
                if (a[i].deltaOnLike != b[i].deltaOnLike) return false;
            }
            return true;
        }

        // ============================================================
        // CSV parser (independent from QuestCsvParser)
        // ============================================================

        private static (List<NpcCsvRow> rows, string[] header, List<string> errors) ParseNpcCsv(string csvText)
        {
            var errors = new List<string>();
            var rows = new List<NpcCsvRow>();
            var lines = SplitCsvLines(csvText);
            if (lines.Count < 2)
            {
                errors.Add("npcs.csv must have at least a header row and one data row.");
                return (rows, new string[0], errors);
            }

            // Parse header
            var rawHeader = ParseCsvLine(lines[0]);
            var header = new string[rawHeader.Length];
            for (int i = 0; i < rawHeader.Length; i++)
            {
                var canonical = ResolveColumnName(rawHeader[i]);
                if (canonical == null) errors.Add($"Line 1 (header): Unknown column '{rawHeader[i]}'. Ignored.");
                else header[i] = canonical;
            }

            // Data rows
            for (int lineIdx = 1; lineIdx < lines.Count; lineIdx++)
            {
                var line = lines[lineIdx];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var fields = ParseCsvLine(line);
                var row = new NpcCsvRow { lineNumber = lineIdx + 1 };
                for (int i = 0; i < fields.Length && i < header.Length; i++)
                {
                    if (header[i] != null) row.values[header[i]] = fields[i].Trim();
                }
                if (string.IsNullOrEmpty(row.Get("npcId")))
                    row.errors.Add($"Line {row.lineNumber}: 'npcId' is required");
                rows.Add(row);
            }
            return (rows, header, errors);
        }

        private static List<string> SplitCsvLines(string text)
        {
            var lines = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            foreach (char c in text)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (c == '\n' && !inQuotes)
                {
                    lines.Add(current.ToString().TrimEnd('\r'));
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) lines.Add(current.ToString().TrimEnd('\r'));
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
                        current.Append('"');
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
