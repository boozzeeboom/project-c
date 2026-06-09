import json, subprocess
# T-Q28: migrate itemTradeItemId from string name → int id using ItemRegistry.
# Iterates through quest assets, for each objective.itemTradeItemId that is non-empty:
#   - Try int.TryParse (already int, skip)
#   - Else lookup via ItemRegistry.TryGetIdByName → write int
code = r'''
var sb = new System.Text.StringBuilder();
string[] questPaths = new string[] {
    "Assets/_Project/Quests/Data/Quests/CollectCopperOre.asset",
    "Assets/_Project/Quests/Data/Quests/StageMultiDemo.asset",
    "Assets/_Project/Quests/Data/Quests/StageIntroDemo.asset",
    "Assets/_Project/Quests/Data/Quests/FindArtifact.asset",
    "Assets/_Project/Quests/Data/Quests/EventDrivenQuest.asset"
};

// Ensure ItemRegistry.Instance is set
var regType = System.Type.GetType("ProjectC.Items.ItemRegistry, Assembly-CSharp");
if (regType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        regType = asm.GetType("ProjectC.Items.ItemRegistry");
        if (regType != null) break;
    }
}
var reg = UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/_Project/Items/Data/ItemRegistry.asset", regType);
var setInstance = regType.GetMethod("SetInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
setInstance.Invoke(null, new object[] { reg });

int totalMigrated = 0;
int totalSkipped = 0;
foreach (var path in questPaths) {
    var def = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDefinition>(path);
    if (def == null) { sb.AppendLine("SKIP (not found): " + path); continue; }
    int migrated = 0;
    int skipped = 0;
    if (def.stages != null) {
        foreach (var stage in def.stages) {
            if (stage == null || stage.objectives == null) continue;
            foreach (var obj in stage.objectives) {
                if (obj == null) continue;
                string val = obj.itemTradeItemId;
                if (string.IsNullOrEmpty(val)) { skipped++; continue; }
                // If already int, skip.
                if (int.TryParse(val, out _)) { skipped++; continue; }
                // Lookup via ItemRegistry.
                int newId = ProjectC.Quests.QuestWorld.ResolveItemId(val);
                if (newId > 0) {
                    sb.AppendLine("  " + def.questId + " / " + obj.objectiveId + ": '" + val + "' -> " + newId);
                    obj.itemTradeItemId = newId.ToString();
                    migrated++;
                } else {
                    sb.AppendLine("  " + def.questId + " / " + obj.objectiveId + ": '" + val + "' -> NOT FOUND");
                }
            }
        }
    }
    if (migrated > 0) {
        UnityEditor.EditorUtility.SetDirty(def);
        sb.AppendLine("MIGRATED " + migrated + " in " + def.questId);
        totalMigrated += migrated;
    } else {
        sb.AppendLine("no changes in " + def.questId);
    }
    totalSkipped += skipped;
}
UnityEditor.AssetDatabase.SaveAssets();
sb.AppendLine("=== TOTAL: migrated=" + totalMigrated + " skipped(already int/empty)=" + totalSkipped + " ===");
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:3000])
