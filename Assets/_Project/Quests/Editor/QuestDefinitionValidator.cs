// T-Q08: QuestDefinitionValidator — editor-only static class.
// Validates QuestDefinition assets: stage connectivity, objective targets, reward items,
// prerequisite quests. Logs warnings в console + цветной report.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.7 (validation).
//
// Usage:
//   - [MenuItem("Tools/ProjectC/Validate All Quests")] — scan all QuestDefinition assets.
//   - [MenuItem("Tools/ProjectC/Validate Selected Quest")] — validate single selected asset.
//   - QuestDefinition.OnValidate() (добавлен в T-Q08) — auto-validate on Inspector change.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    /// <summary>
    /// Editor-only validator. Finds all QuestDefinition assets, checks integrity, reports errors.
    /// </summary>
    public static class QuestDefinitionValidator
    {
        // ============================================================
        // Menu actions
        // ============================================================

        [MenuItem("Tools/ProjectC/Validate All Quests", priority = 100)]
        public static void ValidateAll()
        {
            var guids = AssetDatabase.FindAssets("t:QuestDefinition");
            int totalErrors = 0;
            int totalWarnings = 0;
            int brokenQuests = 0;
            var report = new StringBuilder();
            report.AppendLine($"=== QuestDefinition validation ({guids.Length} assets) ===");

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var def = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (def == null) continue;
                var r = Validate(def);
                totalErrors += r.errors;
                totalWarnings += r.warnings;
                if (!r.IsOk) brokenQuests++;
                if (!r.IsOk || r.warnings > 0)
                {
                    report.AppendLine($"\n--- {def.questId} ({def.name}) [{r.issues.Count} issues] ---");
                    foreach (var issue in r.issues)
                    {
                        report.AppendLine($"  [{issue.severity}] {issue.message}");
                    }
                }
            }

            report.AppendLine($"\n=== Summary: {guids.Length} quests, {brokenQuests} broken, {totalErrors} errors, {totalWarnings} warnings ===");
            if (brokenQuests == 0 && totalErrors == 0)
            {
                report.AppendLine("OK: all quests valid.");
            }
            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/ProjectC/Validate Selected Quest", priority = 101)]
        public static void ValidateSelected()
        {
            var def = Selection.activeObject as QuestDefinition;
            if (def == null)
            {
                Debug.LogWarning("[QuestValidator] Selected object is not a QuestDefinition.");
                return;
            }
            var r = Validate(def);
            if (r.IsOk && r.warnings == 0)
            {
                Debug.Log($"[QuestValidator] {def.questId} OK (0 issues)");
                return;
            }
            var report = new StringBuilder();
            report.AppendLine($"=== {def.questId} ({def.name}) ===");
            foreach (var issue in r.issues)
            {
                report.AppendLine($"  [{issue.severity}] {issue.message}");
            }
            if (r.IsOk) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        // ============================================================
        // Core validation logic
        // ============================================================

        public static ValidationResult Validate(QuestDefinition def)
        {
            var result = new ValidationResult();
            if (def == null) return result;

            if (string.IsNullOrEmpty(def.questId))
                result.Add(Severity.Error, "questId is empty");

            if (def.stages == null || def.stages.Length == 0)
            {
                result.Add(Severity.Error, "stages[] is empty (quest has no stages)");
                return result;
            }

            // ---- 1. Stage connectivity ----
            ValidateStageConnectivity(def, result);

            // ---- 2. Objective targets ----
            ValidateObjectiveTargets(def, result);

            // ---- 3. Reward items ----
            ValidateRewardItems(def, result);

            // ---- 4. Prerequisites ----
            ValidatePrerequisites(def, result);

            // ---- 5. Entry stage exists ----
            if (def.GetEntryStage() == null)
                result.Add(Severity.Error, "entry stage is null (first stage has no stageId or stageId is empty)");

            return result;
        }

        private static void ValidateStageConnectivity(QuestDefinition def, ValidationResult result)
        {
            // Build set of referenced stageIds (via nextStageId in stages)
            var referenced = new HashSet<string>();
            for (int i = 0; i < def.stages.Length; i++)
            {
                var s = def.stages[i];
                if (s == null) continue;
                if (!string.IsNullOrEmpty(s.nextStageId))
                    referenced.Add(s.nextStageId);
            }

            // Find unreachable stages (BFS from first stage)
            var stageById = new Dictionary<string, QuestStage>();
            for (int i = 0; i < def.stages.Length; i++)
            {
                var s = def.stages[i];
                if (s == null || string.IsNullOrEmpty(s.stageId)) continue;
                if (stageById.ContainsKey(s.stageId))
                    result.Add(Severity.Error, $"duplicate stageId: '{s.stageId}'");
                else
                    stageById[s.stageId] = s;
            }

            var reachable = new HashSet<string>();
            if (def.stages.Length > 0 && def.stages[0] != null && !string.IsNullOrEmpty(def.stages[0].stageId))
            {
                var queue = new Queue<string>();
                queue.Enqueue(def.stages[0].stageId);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (!reachable.Add(id)) continue;
                    if (stageById.TryGetValue(id, out var st) && !string.IsNullOrEmpty(st.nextStageId))
                    {
                        queue.Enqueue(st.nextStageId);
                    }
                }
            }

            foreach (var kvp in stageById)
            {
                if (!reachable.Contains(kvp.Key))
                    result.Add(Severity.Warning, $"stage '{kvp.Key}' is unreachable from entry stage");
            }

            // Check for broken nextStageId references
            for (int i = 0; i < def.stages.Length; i++)
            {
                var s = def.stages[i];
                if (s == null) continue;
                if (!string.IsNullOrEmpty(s.nextStageId) && !stageById.ContainsKey(s.nextStageId))
                    result.Add(Severity.Error, $"stage '{s.stageId}' references missing nextStageId: '{s.nextStageId}'");
            }
        }

        private static void ValidateObjectiveTargets(QuestDefinition def, ValidationResult result)
        {
            for (int i = 0; i < def.stages.Length; i++)
            {
                var st = def.stages[i];
                if (st == null || st.objectives == null) continue;
                for (int j = 0; j < st.objectives.Length; j++)
                {
                    var obj = st.objectives[j];
                    if (obj == null) continue;
                    if (string.IsNullOrEmpty(obj.objectiveId))
                        result.Add(Severity.Error, $"stage '{st.stageId}' objective[{j}]: objectiveId is empty");

                    switch (obj.objectiveType)
                    {
                        case QuestObjectiveType.TalkToNpc:
                            if (string.IsNullOrEmpty(obj.targetNpcId))
                                result.Add(Severity.Error, $"stage '{st.stageId}' obj '{obj.objectiveId}' (TalkToNpc): targetNpcId is empty");
                            break;
                        case QuestObjectiveType.DeliverItem:
                            if (string.IsNullOrEmpty(obj.targetNpcId))
                                result.Add(Severity.Error, $"stage '{st.stageId}' obj '{obj.objectiveId}' (DeliverItem): targetNpcId is empty");
                            if (string.IsNullOrEmpty(obj.itemTradeItemId))
                                result.Add(Severity.Warning, $"stage '{st.stageId}' obj '{obj.objectiveId}' (DeliverItem): itemTradeItemId is empty");
                            break;
                        case QuestObjectiveType.ReachLocation:
                            if (string.IsNullOrEmpty(obj.targetSceneId))
                                result.Add(Severity.Warning, $"stage '{st.stageId}' obj '{obj.objectiveId}' (ReachLocation): targetSceneId is empty (will use player position only)");
                            break;
                        case QuestObjectiveType.ReputationAtLeast:
                            if (obj.targetFaction == Factions.FactionId.None)
                                result.Add(Severity.Error, $"stage '{st.stageId}' obj '{obj.objectiveId}' (ReputationAtLeast): targetFaction is None");
                            break;
                        case QuestObjectiveType.EventDriven:
                            if (string.IsNullOrEmpty(obj.eventId))
                                result.Add(Severity.Error, $"stage '{st.stageId}' obj '{obj.objectiveId}' (EventDriven): eventId is empty");
                            break;
                    }
                }
            }
        }

        private static void ValidateRewardItems(QuestDefinition def, ValidationResult result)
        {
            if (def.rewards == null) return;

            // Credits
            if (def.rewards.credits < 0)
                result.Add(Severity.Error, $"rewards.credits is negative: {def.rewards.credits}");

            // Items
            if (def.rewards.items != null)
            {
                for (int i = 0; i < def.rewards.items.Length; i++)
                {
                    var ri = def.rewards.items[i];
                    if (ri == null) continue;
                    if (string.IsNullOrEmpty(ri.tradeItemId))
                        result.Add(Severity.Warning, $"rewards.items[{i}]: tradeItemId is empty");
                    if (ri.count <= 0)
                        result.Add(Severity.Error, $"rewards.items[{i}]: count is {ri.count} (must be > 0)");
                }
            }

            // Reputation
            if (def.rewards.reputation != null)
            {
                for (int i = 0; i < def.rewards.reputation.Length; i++)
                {
                    var rep = def.rewards.reputation[i];
                    if (rep == null) continue;
                    if (rep.faction == FactionId.None)
                        result.Add(Severity.Error, $"rewards.reputation[{i}]: faction is None");
                }
            }

            // Unlocks
            if (def.rewards.unlocks != null)
            {
                for (int i = 0; i < def.rewards.unlocks.Length; i++)
                {
                    if (def.rewards.unlocks[i] == null) continue;
                    if (string.IsNullOrEmpty(def.rewards.unlocks[i].unlockId))
                        result.Add(Severity.Warning, $"rewards.unlocks[{i}]: unlockId is empty");
                }
            }
        }

        private static void ValidatePrerequisites(QuestDefinition def, ValidationResult result)
        {
            if (def.prerequisites == null) return;
            for (int i = 0; i < def.prerequisites.Length; i++)
            {
                var p = def.prerequisites[i];
                if (p == null) continue;
                // For QuestCompleted type, questId is in stringParam
                if (p.type == QuestPrerequisiteType.QuestCompleted && string.IsNullOrEmpty(p.stringParam))
                    result.Add(Severity.Error, $"prerequisites[{i}] (QuestCompleted): stringParam (questId) is empty");
                if (p.type == QuestPrerequisiteType.QuestCompleted && p.stringParam == def.questId)
                    result.Add(Severity.Error, $"prerequisites[{i}]: self-reference (quest references itself as prerequisite)");
            }
        }

        // ============================================================
        // Result types
        // ============================================================

        public enum Severity { Info, Warning, Error }

        public struct Issue
        {
            public Severity severity;
            public string message;
        }

        public class ValidationResult
        {
            public List<Issue> issues = new List<Issue>();
            public int errors => CountOf(Severity.Error);
            public int warnings => CountOf(Severity.Warning);
            public bool IsOk => errors == 0;

            private int CountOf(Severity s)
            {
                int n = 0;
                for (int i = 0; i < issues.Count; i++)
                    if (issues[i].severity == s) n++;
                return n;
            }

            public void Add(Severity s, string msg)
            {
                issues.Add(new Issue { severity = s, message = msg });
            }
        }
    }
}
#endif
