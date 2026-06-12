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
        private const string NPCS_FOLDER = "Assets/_Project/Quests/Data/Npcs";
        private const string DIALOGS_FOLDER = "Assets/_Project/Quests/Data/Dialogs";

        /// <summary>Опции импорта — пользователь выбирает через checkbox'ы.</summary>
        public class ImportOptions
        {
            public bool importQuests = true;
            public bool autoCreateMissingNpcs = true;
            public bool autoCreateMissingDialogs = true;  // создаёт минимальный dialog
            public string dialogsCsvPath;                // опционально: путь к dialogs.csv
        }

        public class ImportResult
        {
            public int created;
            public int updated;
            public int npcsCreated;
            public int dialogsCreated;
            public int skipped;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        /// <summary>Import CSV file → QuestDefinition.asset.</summary>
        public static ImportResult Import(string csvPath, ImportOptions options = null)
        {
            var result = new ImportResult();
            if (options == null) options = new ImportOptions();

            // 1. Parse CSV
            var (rows, header, parseErrors) = QuestCsvParser.ParseFile(csvPath);
            result.errors.AddRange(parseErrors);
            if (rows.Count == 0) return result;

            // 2. Cross-validate (warnings only)
            QuestCsvValidator.CrossValidate(rows, result.errors);

            // 3. Pre-load dialogs.csv (if path provided) — needed for defaultDialogTree linking in step 4.3
            Dictionary<string, DialogTree> customDialogs = null;
            if (options.autoCreateMissingDialogs && !string.IsNullOrEmpty(options.dialogsCsvPath) && File.Exists(options.dialogsCsvPath))
            {
                customDialogs = ImportDialogsCsv(options.dialogsCsvPath, result);
            }

            // 4. Collect unique NPC ids referenced in CSV
            var npcIds = CollectNpcIds(rows);
            result.warnings.Add($"Found {npcIds.Count} unique NPC(s) referenced in CSV: {string.Join(", ", npcIds.Take(10))}");

            // 5. Auto-create missing NPCs (if checkbox)
            if (options.autoCreateMissingNpcs)
            {
                var npcOverrides = CollectNpcOverrides(rows);
                foreach (var npcId in npcIds)
                {
                    bool created;
                    var (displayName, faction) = npcOverrides.TryGetValue(npcId, out var ov) ? ov : (null, FactionId.Neutral);
                    EnsureNpc(npcId, out created, displayName, faction);
                    if (created) result.npcsCreated++;
                }

                // 5.1. T-Q19: populate questOffers for each NPC based on quests where they are the giver
                var npcQuestMap = CollectNpcQuestOffers(rows);
                foreach (var (npcId, questSet) in npcQuestMap)
                {
                    UpdateNpcQuestOffers(npcId, questSet);
                }

                // 5.2. T-Q19.1: populate questTurnIns (last stage + TalkToNpc = who accepts the quest)
                var npcTurnInMap = CollectNpcQuestTurnIns(rows);
                foreach (var (npcId, questSet) in npcTurnInMap)
                {
                    UpdateNpcQuestTurnIns(npcId, questSet);
                }

                // 5.3. T-Q19.2: auto-link defaultDialogTree for each NPC if matching dialog exists
                if (customDialogs != null && customDialogs.Count > 0)
                {
                    foreach (var npcId in npcIds)
                    {
                        UpdateNpcDefaultDialog(npcId, customDialogs);
                    }
                }
            }

            // 6. Group by questId, process
            var questGroups = options.importQuests
                ? rows
                    .Where(r => !r.HasError && !string.IsNullOrEmpty(r.Get("questId")))
                    .GroupBy(r => r.Get("questId").Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<IGrouping<string, QuestCsvRow>>();

            if (options.importQuests)
            {
                foreach (var group in questGroups)
                {
                    try
                    {
                        ProcessQuest(group.ToList(), result, customDialogs);
                    }
                    catch (Exception e)
                    {
                        result.errors.Add($"Error processing quest '{group.Key}': {e.Message}");
                    }
                }
            }

            // 7. Auto-create default dialogs for each quest NPC (if checkbox, no customDialogs provided)
            if (options.autoCreateMissingDialogs)
            {
                foreach (var group in questGroups)
                {
                    var quest = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestDefinition>($"{QUESTS_FOLDER}/{group.Key.Trim()}.asset");
                    if (quest == null) continue;
                    foreach (var prereq in quest.prerequisites)
                    {
                        // For each prereq NPC, ensure it has a default dialog (if not exists)
                    }
                }
            }

            // 8. Trigger database rescan
            QuestDatabaseAutoDiscover.Rescan();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        private static HashSet<string> CollectNpcIds(List<QuestCsvRow> rows)
        {
            var npcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var npcId = row.Get("npcId");
                if (!string.IsNullOrEmpty(npcId)) npcIds.Add(npcId);
                var prereq = row.Get("prereqQuest");
                if (!string.IsNullOrEmpty(prereq))
                {
                    // prereq is questId, not npcId — skip
                }
            }
            return npcIds;
        }

        /// <summary>
        /// Build a map of npcId → set of questIds where this NPC is the **first TalkToNpc** in the quest.
        /// T-Q19: NPC owns the quest if their npcId is referenced in stage 0 of the quest.
        /// </summary>
        private static Dictionary<string, HashSet<string>> CollectNpcQuestOffers(List<QuestCsvRow> rows)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            // Group rows by questId
            var questGroups = rows
                .Where(r => !string.IsNullOrEmpty(r.Get("questId")))
                .GroupBy(r => r.Get("questId").Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in questGroups)
            {
                var questId = group.Key;
                // Get all rows for this quest, ordered by stageNum
                var questRows = group.OrderBy(r => r.GetInt("stageNum", 0)).ToList();

                // First stage: find which NPC is offered
                var firstStageRows = questRows.Where(r => r.GetInt("stageNum", 0) == 0).ToList();
                foreach (var row in firstStageRows)
                {
                    var npcId = row.Get("npcId");
                    var objType = row.Get("objectiveType");
                    // Only TalkToNpc objectives count as "quest offer" — that's the NPC who gives the quest
                    if (string.IsNullOrEmpty(npcId) || objType != "TalkToNpc") continue;
                    if (!map.ContainsKey(npcId)) map[npcId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[npcId].Add(questId);
                }
            }

            return map;
        }

        /// <summary>
        /// T-Q19.1: Build a map of npcId → set of questIds where this NPC is the **turn-in target**.
        /// Rule: last stage + TalkToNpc objective = NPC who accepts the quest.
        /// </summary>
        private static Dictionary<string, HashSet<string>> CollectNpcQuestTurnIns(List<QuestCsvRow> rows)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            var questGroups = rows
                .Where(r => !string.IsNullOrEmpty(r.Get("questId")))
                .GroupBy(r => r.Get("questId").Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in questGroups)
            {
                var questId = group.Key;
                var questRows = group.ToList();
                if (questRows.Count == 0) continue;

                // Find max stageNum for this quest
                int maxStage = questRows.Max(r => r.GetInt("stageNum", 0));
                // Get rows from last stage
                var lastStageRows = questRows.Where(r => r.GetInt("stageNum", 0) == maxStage).ToList();
                foreach (var row in lastStageRows)
                {
                    var npcId = row.Get("npcId");
                    var objType = row.Get("objectiveType");
                    if (string.IsNullOrEmpty(npcId) || objType != "TalkToNpc") continue;
                    if (!map.ContainsKey(npcId)) map[npcId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[npcId].Add(questId);
                }
            }

            return map;
        }

        /// <summary>
        /// Build a map of npcId → (displayName, faction) from CSV.
        /// First row wins (per npcId). If column missing — empty/Neutral.
        /// </summary>
        private static Dictionary<string, (string displayName, FactionId faction)> CollectNpcOverrides(List<QuestCsvRow> rows)
        {
            var map = new Dictionary<string, (string, FactionId)>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var npcId = row.Get("npcId");
                if (string.IsNullOrEmpty(npcId)) continue;
                if (map.ContainsKey(npcId)) continue; // first row wins

                var name = row.Get("npcDisplayName");
                if (string.IsNullOrEmpty(name))
                {
                    // Fallback: keep npcId as-is (user can re-import with explicit displayName)
                    name = npcId;
                }
                var factionStr = row.Get("npcFaction");
                FactionId faction = FactionId.Neutral;
                if (!string.IsNullOrEmpty(factionStr))
                {
                    if (Enum.TryParse<FactionId>(factionStr, true, out var f)) faction = f;
                    else if (factionStr.Equals("npcId", StringComparison.OrdinalIgnoreCase))
                        faction = FactionId.Neutral; // special: "fallback to npcId" marker
                }
                map[npcId] = (name, faction);
            }
            return map;
        }

        /// <summary>
        /// Update NPC's questOffers array with all questIds where this NPC is the quest giver.
        /// Preserves existing questOffers that aren't in the new set (in case quest was removed).
        /// </summary>
        private static void UpdateNpcQuestOffers(string npcId, HashSet<string> newOffers)
        {
            if (newOffers == null || newOffers.Count == 0) return;
            string assetPath = $"{NPCS_FOLDER}/{npcId}.asset";
            var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(assetPath);
            if (npc == null) return;

            var currentOffers = npc.questOffers?.ToList() ?? new List<string>();
            var beforeCount = currentOffers.Count;
            foreach (var qid in newOffers)
            {
                if (!currentOffers.Any(q => string.Equals(q, qid, StringComparison.OrdinalIgnoreCase)))
                    currentOffers.Add(qid);
            }
            if (currentOffers.Count != beforeCount)
            {
                npc.questOffers = currentOffers.ToArray();
                EditorUtility.SetDirty(npc);
                Debug.Log($"[QuestCsvImporter] Updated NPC '{npcId}' questOffers: added {currentOffers.Count - beforeCount} new (total {currentOffers.Count})");
            }
        }

        /// <summary>
        /// T-Q19.1: Update NPC's questTurnIns array — quests that this NPC accepts (player turns them in).
        /// </summary>
        private static void UpdateNpcQuestTurnIns(string npcId, HashSet<string> newTurnIns)
        {
            if (newTurnIns == null || newTurnIns.Count == 0) return;
            string assetPath = $"{NPCS_FOLDER}/{npcId}.asset";
            var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(assetPath);
            if (npc == null) return;

            var currentTurnIns = npc.questTurnIns?.ToList() ?? new List<string>();
            var beforeCount = currentTurnIns.Count;
            foreach (var qid in newTurnIns)
            {
                if (!currentTurnIns.Any(q => string.Equals(q, qid, StringComparison.OrdinalIgnoreCase)))
                    currentTurnIns.Add(qid);
            }
            if (currentTurnIns.Count != beforeCount)
            {
                npc.questTurnIns = currentTurnIns.ToArray();
                EditorUtility.SetDirty(npc);
                Debug.Log($"[QuestCsvImporter] Updated NPC '{npcId}' questTurnIns: added {currentTurnIns.Count - beforeCount} new (total {currentTurnIns.Count})");
            }
        }

        /// <summary>
        /// T-Q19.2: Auto-link NPC's defaultDialogTree if a matching dialog exists.
        /// Convention: dialog treeId = "{npcId}_default" (e.g. npc_002_default).
        /// </summary>
        private static void UpdateNpcDefaultDialog(string npcId, Dictionary<string, DialogTree> availableDialogs)
        {
            if (availableDialogs == null || availableDialogs.Count == 0) return;
            string dialogKey = $"{npcId}_default";
            if (!availableDialogs.TryGetValue(dialogKey, out var dialog)) return;
            if (dialog == null) return;

            string assetPath = $"{NPCS_FOLDER}/{npcId}.asset";
            var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(assetPath);
            if (npc == null) return;

            if (npc.defaultDialogTree != dialog)
            {
                npc.defaultDialogTree = dialog;
                EditorUtility.SetDirty(npc);
                Debug.Log($"[QuestCsvImporter] Linked NPC '{npcId}' → defaultDialogTree = '{dialogKey}'");
            }
        }

        /// <summary>Ensure NpcDefinition asset exists. Returns whether it was newly created.</summary>
        private static void EnsureNpc(string npcId, out bool created, string displayNameOverride = null, FactionId factionOverride = FactionId.Neutral)
        {
            created = false;
            string assetPath = $"{NPCS_FOLDER}/{npcId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NpcDefinition>(assetPath);
            if (existing != null)
            {
                // Update existing asset if we have better metadata (don't clobber portraits/prefabs)
                bool updated = false;
                if (!string.IsNullOrEmpty(displayNameOverride) && existing.displayName != displayNameOverride)
                {
                    existing.displayName = displayNameOverride;
                    updated = true;
                }
                if (factionOverride != FactionId.Neutral && existing.faction != factionOverride)
                {
                    // Only update if explicit override
                    existing.faction = factionOverride;
                    updated = true;
                }
                if (updated)
                {
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[QuestCsvImporter] Updated NPC '{npcId}' metadata (displayName/faction)");
                }
                return;
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(NPCS_FOLDER))
            {
                var parent = "Assets/_Project/Quests/Data";
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets/_Project/Quests", "Data");
                AssetDatabase.CreateFolder(parent, "Npcs");
            }

            // Create default NPC
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            npc.npcId = npcId;
            npc.displayName = !string.IsNullOrEmpty(displayNameOverride)
                ? displayNameOverride
                : npcId.Replace('_', ' ').Trim();
            npc.faction = factionOverride;
            npc.questOffers = new string[0];
            AssetDatabase.CreateAsset(npc, assetPath);
            created = true;
            Debug.Log($"[QuestCsvImporter] Auto-created NPC '{npcId}' as '{npc.displayName}' (faction: {npc.faction}) at {assetPath}");
        }

        /// <summary>Parse dialogs.csv. Returns dialog by treeId.</summary>
        private static Dictionary<string, DialogTree> ImportDialogsCsv(string path, ImportResult result)
        {
            var dialogResult = new DialogCsvImporter.ImportResult();
            var dialogs = DialogCsvImporter.Import(path, dialogResult);
            // Propagate dialog result to our result
            result.dialogsCreated = dialogResult.treesCreated + dialogResult.treesUpdated;
            foreach (var e in dialogResult.errors) result.errors.Add($"[dialogs.csv] {e}");
            foreach (var w in dialogResult.warnings) result.warnings.Add($"[dialogs.csv] {w}");
            return dialogs;
        }

        private static void ProcessQuest(List<QuestCsvRow> rows, ImportResult result, Dictionary<string, DialogTree> customDialogs = null)
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
