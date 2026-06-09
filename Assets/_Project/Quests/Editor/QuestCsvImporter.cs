// M19-T2: QuestCsvImporter — CSV → QuestDefinition.asset
// Парсит строки, группирует по questId, создаёт/обновляет SO.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;
using ProjectC.Dialogue;
using ProjectC.Items;

namespace ProjectC.Quests.Editor
{
    public static class QuestCsvImporter
    {
        private const string QUESTS_FOLDER = "Assets/_Project/Quests/Data/Quests";

        public class ImportResult
        {
            public int created;
            public int updated;
            public int skipped;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        /// <summary>Import CSV file → QuestDefinition.asset.</summary>
        public static ImportResult Import(string csvPath)
        {
            var result = new ImportResult();

            // 1. Parse CSV
            var (rows, header, parseErrors) = QuestCsvParser.ParseFile(csvPath);
            result.errors.AddRange(parseErrors);
            if (rows.Count == 0) return result;

            // 2. Cross-validate (optional, not blocking)
            QuestCsvValidator.CrossValidate(rows, result.errors);

            // 3. Group by questId
            var questGroups = rows
                .Where(r => !r.HasError && !string.IsNullOrEmpty(r.Get("questId")))
                .GroupBy(r => r.Get("questId").Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in questGroups)
            {
                try
                {
                    ProcessQuest(group.ToList(), result);
                }
                catch (Exception e)
                {
                    result.errors.Add($"Error processing quest '{group.Key}': {e.Message}");
                }
            }

            // 4. Trigger database rescan
            QuestDatabaseAutoDiscover.Rescan();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        private static void ProcessQuest(List<QuestCsvRow> rows, ImportResult result)
        {
            var firstRow = rows[0];
            var questId = firstRow.Get("questId").Trim();

            // Check if already exists
            string assetPath = $"{QUESTS_FOLDER}/{questId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<QuestDefinition>(assetPath);
            bool isNew = existing == null;

            // Create or get SO
            var quest = existing ?? ScriptableObject.CreateInstance<QuestDefinition>();
            if (isNew)
            {
                // Ensure folder exists
                if (!AssetDatabase.IsValidFolder(QUESTS_FOLDER))
                {
                    var parent = "Assets/_Project/Quests/Data";
                    if (!AssetDatabase.IsValidFolder(parent))
                        AssetDatabase.CreateFolder("Assets/_Project/Quests", "Data");
                    AssetDatabase.CreateFolder(parent, "Quests");
                }
                AssetDatabase.CreateAsset(quest, assetPath);
            }

            // --- Quest-level fields ---
            quest.questId = questId;
            quest.displayName = firstRow.Get("displayName");
            quest.description = firstRow.Get("description");

            // Faction
            var factionStr = firstRow.Get("faction");
            if (!string.IsNullOrEmpty(factionStr))
            {
                if (Enum.TryParse<FactionId>(factionStr, true, out var faction))
                    quest.faction = faction;
                else
                    result.warnings.Add($"Quest '{questId}': unknown faction '{factionStr}', using Neutral");
            }

            quest.oneShot = firstRow.GetBool("oneShot");
            quest.discoverable = true; // default true for CSV imports

            // Prerequisites
            var prereqStr = firstRow.Get("prereqQuest");
            if (!string.IsNullOrEmpty(prereqStr))
            {
                var prereqs = new List<QuestPrerequisite>();
                foreach (var pid in prereqStr.Split(new[]{';',','}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = pid.Trim();
                    if (!string.IsNullOrEmpty(p))
                    {
                        prereqs.Add(new QuestPrerequisite
                        {
                            type = QuestPrerequisiteType.QuestCompleted,
                            stringParam = p
                        });
                    }
                }
                quest.prerequisites = prereqs.ToArray();
            }
            else
            {
                quest.prerequisites = Array.Empty<QuestPrerequisite>();
            }

            // --- Stages ---
            var stageGroups = rows.GroupBy(r => r.GetInt("stageNum"));
            var stages = new List<QuestStage>();

            foreach (var sg in stageGroups.OrderBy(g => g.Key))
            {
                var stageRows = sg.ToList();
                var firstStageRow = stageRows[0];
                var stage = new QuestStage
                {
                    stageId = firstStageRow.Get("stageId"),
                    description = firstStageRow.Get("stageDescription"),
                    nextStageId = "", // auto-computed
                    objectives = new QuestObjective[stageRows.Count]
                };

                // If stageId is empty, auto-generate
                if (string.IsNullOrEmpty(stage.stageId))
                    stage.stageId = $"stage_{sg.Key}";

                // Objectives
                for (int i = 0; i < stageRows.Count; i++)
                {
                    var row = stageRows[i];
                    var obj = new QuestObjective
                    {
                        objectiveId = row.Get("objectiveId"),
                        requiredQuantity = row.GetInt("qty", 1),
                        itemTradeItemId = row.Get("itemName"),
                        targetNpcId = row.Get("npcId"),
                    };
                    // Parse objectiveType
                    var typeStr = row.Get("objectiveType");
                    if (!string.IsNullOrEmpty(typeStr))
                    {
                        if (Enum.TryParse<QuestObjectiveType>(typeStr, true, out var ot))
                            obj.objectiveType = ot;
                        else
                            result.warnings.Add($"Quest '{questId}', stage {sg.Key}: unknown objective type '{typeStr}', using HaveItem");
                    }
                    // Auto-generate objectiveId if empty
                    if (string.IsNullOrEmpty(obj.objectiveId))
                        obj.objectiveId = $"obj_{sg.Key}_{i}";

                    stage.objectives[i] = obj;
                }

                // OnEnter actions (from first row of stage)
                stage.onEnterActions = ParseActions(firstStageRow.Get("onEnterActions"), questId, sg.Key, result);

                // OnComplete actions (from first row of stage)
                stage.onCompleteActions = ParseActions(firstStageRow.Get("onCompleteActions"), questId, sg.Key, result);

                stages.Add(stage);
            }

            // Link stages: nextStageId
            for (int i = 0; i < stages.Count; i++)
            {
                if (i < stages.Count - 1)
                    stages[i].nextStageId = stages[i + 1].stageId;
                else
                    stages[i].nextStageId = ""; // terminal
            }
            quest.stages = stages.ToArray();

            // --- Rewards (from last row of quest) ---
            var lastRow = rows[rows.Count - 1];
            quest.rewards = new QuestReward
            {
                credits = lastRow.GetInt("rewardCR", 0),
                items = ParseRewardItems(lastRow.Get("rewardItem")),
                reputation = ParseRewardReputation(lastRow.Get("rewardRep")),
            };

            // Save
            EditorUtility.SetDirty(quest);

            if (isNew)
            {
                result.created++;
                Debug.Log($"[QuestCsvImporter] Created quest '{questId}' as {assetPath}");
            }
            else
            {
                result.updated++;
                Debug.Log($"[QuestCsvImporter] Updated quest '{questId}'");
            }

            // Also update quest database reference
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>("Assets/_Project/Quests/Data/QuestDatabase.asset");
            if (db != null)
            {
                var currentQuests = db.quests?.ToList() ?? new List<QuestDefinition>();
                if (!currentQuests.Any(q => q != null && q.questId == questId))
                {
                    var list = currentQuests.ToList();
                    list.Add(quest);
                    db.quests = list.ToArray();
                    EditorUtility.SetDirty(db);
                }
            }
        }

        // ============================================================
        // Action string parser
        // ============================================================

        /// <summary>Parse "GiveCredits::200" or "AddNpcAttitude:mira_01:5" → DialogueAction[].</summary>
        private static DialogueAction[] ParseActions(string raw, string questId, int stageNum, ImportResult result)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<DialogueAction>();

            var actions = new List<DialogueAction>();
            foreach (var token in raw.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(new[]{':'}, 5); // max 5 parts
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) continue;

                var typeName = parts[0].Trim();
                var action = new DialogueAction();
                bool found = MapActionType(typeName, action);
                if (!found)
                {
                    result.warnings.Add($"Quest '{questId}', stage {stageNum}: unknown action type '{typeName}', skipping");
                    continue;
                }

                // Parse params based on action type
                string param1 = parts.Length > 1 ? parts[1].Trim() : "";
                string param2 = parts.Length > 2 ? parts[2].Trim() : "";
                string param3 = parts.Length > 3 ? parts[3].Trim() : "";
                // param4 unused for now

                switch (action.type)
                {
                    case DialogueActionType.GiveCredits:
                        action.intParam = ParseInt(param2, 0);
                        break;
                    case DialogueActionType.AddReputation:
                        action.intParam = ParseInt(param2, 0);
                        if (Enum.TryParse<FactionId>(param1, true, out var repFaction))
                            action.factionParam = repFaction;
                        break;
                    case DialogueActionType.AddNpcAttitude:
                        action.stringParam = param1; // npcId
                        action.intParam = ParseInt(param2, 0);
                        break;
                    case DialogueActionType.GiveItem:
                    case DialogueActionType.TakeItem:
                        action.stringParam = param1; // itemName
                        action.intParam = ParseInt(param2, 1);
                        // itemType as param3
                        if (!string.IsNullOrEmpty(param3) && Enum.TryParse<ItemType>(param3, true, out var it))
                            action.itemType = it;
                        break;
                    case DialogueActionType.OfferQuest:
                    case DialogueActionType.AcceptQuest:
                        action.stringParam = param1; // questId
                        break;
                    case DialogueActionType.CompleteObjective:
                        action.stringParam = param1; // objectiveId
                        break;
                    default:
                        action.stringParam = param1;
                        action.intParam = ParseInt(param2, 0);
                        break;
                }

                actions.Add(action);
            }

            return actions.ToArray();
        }

        private static bool MapActionType(string name, DialogueAction action)
        {
            var map = new Dictionary<string, DialogueActionType>(StringComparer.OrdinalIgnoreCase)
            {
                {"givecredits", DialogueActionType.GiveCredits},
                {"addreputation", DialogueActionType.AddReputation},
                {"addnpcattitude", DialogueActionType.AddNpcAttitude},
                {"giveitem", DialogueActionType.GiveItem},
                {"takeitem", DialogueActionType.TakeItem},
                {"offerquest", DialogueActionType.OfferQuest},
                {"acceptquest", DialogueActionType.AcceptQuest},
                {"completeobjective", DialogueActionType.CompleteObjective},
            };
            if (map.TryGetValue(name.Replace(" ", "").Replace("-", ""), out var t))
            {
                action.type = t;
                return true;
            }
            return false;
        }

        private static int ParseInt(string s, int defaultVal)
        {
            if (string.IsNullOrEmpty(s)) return defaultVal;
            return int.TryParse(s, out var v) ? v : defaultVal;
        }

        // ============================================================
        // Reward parsers
        // ============================================================

        private static QuestRewardItem[] ParseRewardItems(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<QuestRewardItem>();
            var items = new List<QuestRewardItem>();
            foreach (var token in raw.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(':');
                if (parts.Length == 0) continue;
                var itemName = parts[0].Trim();
                int count = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 1;
                items.Add(new QuestRewardItem { tradeItemId = itemName, count = count });
            }
            return items.ToArray();
        }

        private static QuestRewardReputation[] ParseRewardReputation(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<QuestRewardReputation>();
            var reps = new List<QuestRewardReputation>();
            foreach (var token in raw.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(':');
                if (parts.Length == 0) continue;
                if (Enum.TryParse<FactionId>(parts[0].Trim(), true, out var faction))
                {
                    int value = parts.Length > 1 && int.TryParse(parts[1], out var v) ? v : 0;
                    reps.Add(new QuestRewardReputation { faction = faction, value = value });
                }
            }
            return reps.ToArray();
        }
    }
}
#endif
