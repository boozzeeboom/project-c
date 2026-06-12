// M19-T7: DialogTree CSV parser.
// Формат: один row = один edge. Узлы создаются автоматически из fromNodeId/toNodeId.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Dialogue;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    public static class DialogCsvImporter
    {
        private const string DIALOGS_FOLDER = "Assets/_Project/Quests/Data/Dialogs";

        public class ImportResult
        {
            public int treesCreated;
            public int treesUpdated;
            public int nodesCreated;
            public int edgesCreated;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        /// <summary>Import dialogs.csv. Returns map of treeId -> DialogTree.</summary>
        public static Dictionary<string, DialogTree> Import(string csvPath, ImportResult result = null)
        {
            if (result == null) result = new ImportResult();
            var dialogs = new Dictionary<string, DialogTree>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(csvPath))
            {
                result.errors.Add($"dialogs.csv not found: {csvPath}");
                return dialogs;
            }

            try
            {
                var text = File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                return ImportText(text, result, csvPath);
            }
            catch (Exception e)
            {
                result.errors.Add($"Error reading dialogs.csv: {e.Message}");
                return dialogs;
            }
        }

        public static Dictionary<string, DialogTree> ImportText(string csvText, ImportResult result, string sourcePath = "<inline>")
        {
            var dialogs = new Dictionary<string, DialogTree>(StringComparer.OrdinalIgnoreCase);
            if (result == null) result = new ImportResult();

            // Parse CSV with independent parser (no questId requirement)
            var (rows, header, parseErrors) = ParseDialogCsv(csvText, sourcePath);
            result.errors.AddRange(parseErrors);
            if (rows.Count == 0) return dialogs;

            // Group by treeId
            var treeGroups = rows
                .Where(r => !r.HasError && !string.IsNullOrEmpty(r.Get("treeId")))
                .GroupBy(r => r.Get("treeId").Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var treeGroup in treeGroups)
            {
                try
                {
                    var tree = BuildTree(treeGroup.Key, treeGroup.ToList(), result);
                    if (tree != null)
                    {
                        dialogs[treeGroup.Key] = tree;
                        result.edgesCreated += CountEdges(tree);
                        result.nodesCreated += tree.nodes.Length;
                    }
                }
                catch (Exception e)
                {
                    result.errors.Add($"Error building tree '{treeGroup.Key}': {e.Message}");
                }
            }

            // Save created/updated trees to assets
            foreach (var (treeId, tree) in dialogs)
            {
                SaveDialogAsset(treeId, tree, result);
            }

            return dialogs;
        }

        private static DialogTree BuildTree(string treeId, List<QuestCsvRow> rows, ImportResult result)
        {
            // Ensure folder
            if (!AssetDatabase.IsValidFolder(DIALOGS_FOLDER))
            {
                var parent = "Assets/_Project/Quests/Data";
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets/_Project/Quests", "Data");
                AssetDatabase.CreateFolder(parent, "Dialogs");
            }

            string assetPath = $"{DIALOGS_FOLDER}/{treeId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogTree>(assetPath);
            bool isNew = existing == null;

            var tree = existing ?? ScriptableObject.CreateInstance<DialogTree>();
            tree.treeId = treeId;
            // Auto-generate displayName from treeId
            tree.displayName = treeId.Replace('_', ' ').Trim();

            // Group rows by fromNodeId (each node = first row + all edges)
            var nodeGroups = rows.GroupBy(r => r.Get("fromNodeId").Trim(), StringComparer.OrdinalIgnoreCase);

            var nodesList = new List<DialogueNode>();
            var nodeMap = new Dictionary<string, DialogueNode>(StringComparer.OrdinalIgnoreCase);
            string firstNodeId = null;

            // Find root (default: "greeting", or first node)
            tree.rootNodeId = "greeting";
            if (nodeGroups.Any())
            {
                firstNodeId = nodeGroups.First().Key;
                // If greeting exists in CSV, use it; otherwise use first
                if (nodeGroups.Any(g => g.Key.Equals("greeting", StringComparison.OrdinalIgnoreCase)))
                    tree.rootNodeId = "greeting";
                else
                    tree.rootNodeId = firstNodeId;
            }

            // First pass: create all nodes
            foreach (var ng in nodeGroups)
            {
                var firstRow = ng.First();
                var node = new DialogueNode
                {
                    nodeId = ng.Key,
                    text = firstRow.Get("fromText"),
                    speaker = ParseSpeaker(firstRow.Get("fromSpeaker")),
                    edges = new DialogueEdge[ng.Count()]
                };
                // Add to list and map
                nodesList.Add(node);
                nodeMap[ng.Key] = node;
            }

            // Second pass: build edges
            foreach (var ng in nodeGroups)
            {
                var parentNode = nodeMap[ng.Key];
                int edgeIdx = 0;
                foreach (var row in ng)
                {
                    var edge = new DialogueEdge
                    {
                        label = row.Get("edgeLabel"),
                        targetNodeId = row.Get("toNodeId"),
                        hideIfUnavailable = row.GetBool("hideIfUnavailable"),
                    };

                    // Condition (single, "shortcut" form)
                    var condType = row.Get("conditionType");
                    if (!string.IsNullOrEmpty(condType))
                    {
                        edge.condition = new DialogueCondition
                        {
                            type = ParseConditionType(condType, out var err),
                            stringParam = row.Get("conditionStringParam"),
                            intParam = row.GetInt("conditionIntParam", 0),
                            factionParam = ParseFaction(row.Get("conditionFactionParam")),
                        };
                        if (err != null) result.warnings.Add($"tree '{treeId}': {err}");
                    }

                    // Action
                    var actType = row.Get("actionType");
                    if (!string.IsNullOrEmpty(actType))
                    {
                        edge.action = new DialogueAction
                        {
                            type = ParseActionType(actType, out var err),
                            stringParam = row.Get("actionStringParam"),
                            intParam = row.GetInt("actionIntParam", 0),
                            factionParam = ParseFaction(row.Get("actionFactionParam")),
                        };
                        if (err != null) result.warnings.Add($"tree '{treeId}': {err}");
                    }

                    parentNode.edges[edgeIdx++] = edge;
                }
            }

            tree.nodes = nodesList.ToArray();
            return tree;
        }

        private static void SaveDialogAsset(string treeId, DialogTree tree, ImportResult result)
        {
            string assetPath = $"{DIALOGS_FOLDER}/{treeId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogTree>(assetPath);
            bool isNew = existing == null;
            if (isNew)
            {
                AssetDatabase.CreateAsset(tree, assetPath);
                result.treesCreated++;
                Debug.Log($"[DialogCsvImporter] Created dialog '{treeId}'");
            }
            else
            {
                EditorUtility.SetDirty(tree);
                result.treesUpdated++;
            }
        }

        private static int CountEdges(DialogTree tree)
        {
            int count = 0;
            if (tree?.nodes != null)
                foreach (var n in tree.nodes) count += n.edges?.Length ?? 0;
            return count;
        }

        // ============================================================
        // Type parsers
        // ============================================================

        private static SpeakerRef ParseSpeaker(string raw)
        {
            var def = new SpeakerRef();
            if (string.IsNullOrEmpty(raw)) return def;
            // Format: "Npc: npcId" or "Player" or "Narrator"
            if (raw.StartsWith("Npc:", StringComparison.OrdinalIgnoreCase))
            {
                def.speakerKind = SpeakerRef.Kind.Npc;
                def.refId = raw.Substring(4).Trim();
            }
            else if (raw.Equals("Player", StringComparison.OrdinalIgnoreCase))
            {
                def.speakerKind = SpeakerRef.Kind.Player;
            }
            else if (raw.Equals("Narrator", StringComparison.OrdinalIgnoreCase))
            {
                def.speakerKind = SpeakerRef.Kind.Narrator;
            }
            else
            {
                // Default: treat as NPC id
                def.speakerKind = SpeakerRef.Kind.Npc;
                def.refId = raw;
            }
            return def;
        }

        private static DialogueConditionType ParseConditionType(string raw, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(raw)) return DialogueConditionType.HasItem;
            if (Enum.TryParse<DialogueConditionType>(raw, true, out var result)) return result;
            error = $"Unknown conditionType '{raw}'";
            return DialogueConditionType.HasItem;
        }

        private static DialogueActionType ParseActionType(string raw, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(raw)) return DialogueActionType.EndConversation;
            if (Enum.TryParse<DialogueActionType>(raw, true, out var result)) return result;
            error = $"Unknown actionType '{raw}'";
            return DialogueActionType.EndConversation;
        }

        private static FactionId ParseFaction(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return FactionId.None;
            return Enum.TryParse<FactionId>(raw, true, out var f) ? f : FactionId.None;
        }

        // ============================================================
        // Lightweight CSV parser for dialogs.csv (independent of quest schema)
        // ============================================================

        private static (List<QuestCsvRow> rows, string[] header, List<string> errors) ParseDialogCsv(string csvText, string sourcePath)
        {
            var errors = new List<string>();
            var rows = new List<QuestCsvRow>();

            var lines = SplitCsvLines(csvText);
            if (lines.Count < 2)
            {
                errors.Add($"dialogs.csv must have at least a header row and one data row. Found {lines.Count} lines.");
                return (rows, new string[0], errors);
            }

            // Parse header — all columns allowed (no required columns)
            var rawHeader = ParseCsvLine(lines[0]);
            var header = new string[rawHeader.Length];
            for (int i = 0; i < rawHeader.Length; i++)
            {
                header[i] = rawHeader[i].Trim();
            }

            // Data rows
            for (int lineIdx = 1; lineIdx < lines.Count; lineIdx++)
            {
                var line = lines[lineIdx];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                var row = new QuestCsvRow { lineNumber = lineIdx + 1 };

                for (int i = 0; i < fields.Length && i < header.Length; i++)
                {
                    row.values[header[i]] = fields[i].Trim();
                }

                // Validate ONLY required for dialogs: treeId, fromNodeId, edgeLabel, toNodeId
                if (string.IsNullOrEmpty(row.Get("treeId")))
                    row.errors.Add($"Line {row.lineNumber}: 'treeId' is required");
                if (string.IsNullOrEmpty(row.Get("fromNodeId")))
                    row.errors.Add($"Line {row.lineNumber}: 'fromNodeId' is required");
                if (string.IsNullOrEmpty(row.Get("edgeLabel")))
                    row.errors.Add($"Line {row.lineNumber}: 'edgeLabel' is required");

                rows.Add(row);
            }

            return (rows, header, errors);
        }

        private static List<string> SplitCsvLines(string text)
        {
            var lines = new List<string>();
            var currentLine = new System.Text.StringBuilder();
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
            var current = new System.Text.StringBuilder();
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
