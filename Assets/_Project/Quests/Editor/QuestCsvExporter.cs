// M19-T3: QuestCsvExporter — QuestDefinition.asset → CSV строки
// Обратная операция импорту. Один квест → несколько строк (по числу objectives).

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Dialogue;
using ProjectC.Items;

namespace ProjectC.Quests.Editor
{
    public static class QuestCsvExporter
    {
        /// <summary>Export quests to CSV file. Returns number of rows written.</summary>
        public static int Export(string csvPath, QuestDefinition[] quests)
        {
            if (quests == null || quests.Length == 0 || string.IsNullOrEmpty(csvPath)) return 0;

            var sb = new StringBuilder();

            // Header
            var columns = QuestCsvSchema.Columns;
            sb.AppendLine(string.Join(",", columns.Select(c => QuoteCsv(c.name))));

            // Data rows
            int rowCount = 0;
            foreach (var quest in quests)
            {
                if (quest == null) continue;

                if (quest.stages == null || quest.stages.Length == 0)
                {
                    // Quest with no stages — one row with empty objective
                    sb.AppendLine(MakeRow(quest, null, -1));
                    rowCount++;
                }
                else
                {
                    for (int si = 0; si < quest.stages.Length; si++)
                    {
                        var stage = quest.stages[si];
                        if (stage == null) continue;

                        if (stage.objectives == null || stage.objectives.Length == 0)
                        {
                            // Stage with no objectives — one row
                            sb.AppendLine(MakeRow(quest, stage, si, null, 0));
                            rowCount++;
                        }
                        else
                        {
                            for (int oi = 0; oi < stage.objectives.Length; oi++)
                            {
                                var obj = stage.objectives[oi];
                                sb.AppendLine(MakeRow(quest, stage, si, obj, oi));
                                rowCount++;
                            }
                        }
                    }
                }
            }

            // Write file
            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            return rowCount;
        }

        private static string MakeRow(QuestDefinition quest, QuestStage stage, int stageIndex, QuestObjective obj, int objIndex)
        {
            var questId = EscapeCsv(quest.questId);
            var displayName = EscapeCsv(quest.displayName);
            var description = EscapeCsv(quest.description ?? "");
            var faction = quest.faction.ToString();
            var oneShot = quest.oneShot ? "y" : "n";
            var prereq = quest.prerequisites != null
                ? EscapeCsv(string.Join(";", quest.prerequisites
                    .Where(p => p.type == QuestPrerequisiteType.QuestCompleted)
                    .Select(p => p.stringParam)))
                : "";

            // Stage data
            var stageNum = stageIndex.ToString();
            var stageId = EscapeCsv(stage?.stageId ?? "");
            var stageDesc = EscapeCsv(stage?.description ?? "");
            var onEnter = stage != null ? EscapeCsv(SerializeActions(stage.onEnterActions)) : "";
            var onComplete = stage != null ? EscapeCsv(SerializeActions(stage.onCompleteActions)) : "";

            // Objective data
            var objType = obj != null ? obj.objectiveType.ToString() : "";
            var objId = EscapeCsv(obj?.objectiveId ?? "");
            var itemName = EscapeCsv(obj?.itemTradeItemId ?? "");
            var npcId = EscapeCsv(obj?.targetNpcId ?? "");
            var qty = (obj?.requiredQuantity ?? 1).ToString();

            // Rewards (from quest level)
            var rewardCR = (quest.rewards?.credits ?? 0).ToString();
            var rewardRep = quest.rewards?.reputation != null
                ? EscapeCsv(string.Join(";", quest.rewards.reputation.Select(r => $"{r.faction}:{r.value}")))
                : "";
            var rewardItem = quest.rewards?.items != null
                ? EscapeCsv(string.Join(";", quest.rewards.items.Select(i => $"{i.tradeItemId}:{i.count}")))
                : "";

            return $"{questId},{displayName},{description},{faction},{oneShot},{prereq},{stageNum},{stageId},{stageDesc},{onEnter},{objType},{objId},{itemName},{npcId},{qty},{onComplete},{rewardCR},{rewardRep},{rewardItem}";
        }

        // Overload for quest without stage context
        private static string MakeRow(QuestDefinition quest, QuestStage stage, int stageIndex, object unused, int unused2)
        {
            return MakeRow(quest, stage, stageIndex, null, 0);
        }

        private static string MakeRow(QuestDefinition quest, object unused1, int unused2)
        {
            return MakeRow(quest, null, -1, null, 0);
        }

        // ============================================================
        // Action → string serialization
        // ============================================================

        private static string SerializeActions(DialogueAction[] actions)
        {
            if (actions == null || actions.Length == 0) return "";
            return string.Join(";", actions.Select(a => SerializeAction(a)));
        }

        private static string SerializeAction(DialogueAction a)
        {
            // Map type to short name
            var typeName = a.type switch
            {
                DialogueActionType.GiveCredits => "GiveCredits",
                DialogueActionType.AddReputation => "AddReputation",
                DialogueActionType.AddNpcAttitude => "AddNpcAttitude",
                DialogueActionType.GiveItem => "GiveItem",
                DialogueActionType.TakeItem => "TakeItem",
                DialogueActionType.OfferQuest => "OfferQuest",
                DialogueActionType.AcceptQuest => "AcceptQuest",
                DialogueActionType.CompleteObjective => "CompleteObjective",
                _ => a.type.ToString()
            };

            switch (a.type)
            {
                case DialogueActionType.GiveCredits:
                    return $"{typeName}::{a.intParam}";
                case DialogueActionType.AddReputation:
                    return $"{typeName}:{a.factionParam}:{a.intParam}";
                case DialogueActionType.AddNpcAttitude:
                    return $"{typeName}:{a.stringParam}:{a.intParam}";
                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                    return $"{typeName}:{a.stringParam}:{a.intParam}:{a.itemType}";
                case DialogueActionType.OfferQuest:
                case DialogueActionType.AcceptQuest:
                case DialogueActionType.CompleteObjective:
                    return $"{typeName}:{a.stringParam}";
                default:
                    return $"{typeName}:{a.stringParam}:{a.intParam}";
            }
        }

        // ============================================================
        // CSV helpers
        // ============================================================

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private static string QuoteCsv(string value)
        {
            // Simple header quoting — always quoted for safety
            if (value.Contains(',') || value.Contains('"') || value.Contains(' '))
                return $"\"{value}\"";
            return value;
        }
    }
}
#endif
