import json, subprocess
# Final verify T-Q28: load each quest and dump itemTradeItemId.
code = r'''
var sb = new System.Text.StringBuilder();
string[] questPaths = new string[] {
    "Assets/_Project/Quests/Data/Quests/CollectCopperOre.asset",
    "Assets/_Project/Quests/Data/Quests/StageMultiDemo.asset",
    "Assets/_Project/Quests/Data/Quests/StageIntroDemo.asset",
    "Assets/_Project/Quests/Data/Quests/FindArtifact.asset"
};
foreach (var path in questPaths) {
    var def = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDefinition>(path);
    if (def == null) continue;
    sb.AppendLine("=== " + def.questId + " ===");
    if (def.stages != null) {
        foreach (var stage in def.stages) {
            if (stage == null) continue;
            sb.AppendLine("  stage=" + stage.stageId);
            if (stage.objectives != null) {
                foreach (var obj in stage.objectives) {
                    if (obj == null) continue;
                    sb.AppendLine("    obj=" + obj.objectiveId + " itemTradeItemId='" + obj.itemTradeItemId + "' qty=" + obj.requiredQuantity);
                }
            }
        }
    }
}
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:3000])
