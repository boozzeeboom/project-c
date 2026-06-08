import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();

// --- 1. Remove trigger zone from BootstrapScene ---
var bootScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/BootstrapScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
var oldTrigger = GameObject.Find("TriggerZone_DiscoverQuest");
if (oldTrigger != null) {
    UnityEngine.Object.DestroyImmediate(oldTrigger);
    sb.AppendLine("Removed old trigger from BootstrapScene");
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bootScene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(bootScene);
sb.AppendLine("BootstrapScene saved");

// --- 2. Open WorldScene_0_0 ---
var wsScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/World/WorldScene_0_0.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
sb.AppendLine("Opened WorldScene_0_0");

// --- 3. Simplify quest to 1 stage ---
string questPath = "Assets/_Project/Quests/Data/Quests/CollectCopperOre.asset";
var quest = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDefinition>(questPath);
if (quest != null) {
    var stage = new ProjectC.Quests.QuestStage();
    stage.stageId = "collect";
    stage.description = "Собери 3 медных руды.";
    var obj = new ProjectC.Quests.QuestObjective();
    obj.objectiveId = "gather_copper";
    obj.objectiveType = ProjectC.Quests.QuestObjectiveType.HaveItem;
    obj.description = "Медная руда";
    obj.itemTradeItemId = "Медная руда";
    obj.requiredQuantity = 3;
    obj.required = true;
    obj.optional = false;
    stage.objectives = new ProjectC.Quests.QuestObjective[] { obj };
    var giveCredits = new ProjectC.Dialogue.DialogueAction();
    giveCredits.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
    giveCredits.intParam = 200;
    var addRep = new ProjectC.Dialogue.DialogueAction();
    addRep.type = ProjectC.Dialogue.DialogueActionType.AddReputation;
    addRep.factionParam = ProjectC.Factions.FactionId.GuildOfThoughts;
    addRep.intParam = 25;
    stage.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { giveCredits, addRep };
    stage.nextStageId = "";
    quest.stages = new ProjectC.Quests.QuestStage[] { stage };
    UnityEditor.EditorUtility.SetDirty(quest);
    UnityEditor.AssetDatabase.SaveAssets();
    sb.AppendLine("Quest simplified to 1 stage, onComplete: GiveCredits(200) + AddReputation(25)");
}

// --- 4. Attach trigger zone component (use Type.GetType to bypass Roslyn cache) ---
var triggerGo = GameObject.Find("TriggerZone_DiscoverQuest");
if (triggerGo != null) {
    var componentType = System.Type.GetType("ProjectC.Quests.Testing.M13QuestTriggerZone, Assembly-CSharp");
    if (componentType == null) {
        sb.AppendLine("WARN: M13QuestTriggerZone type NOT loaded yet — domain reload needed");
    } else {
        var existing = triggerGo.GetComponent(componentType);
        if (existing == null) {
            triggerGo.AddComponent(componentType);
            sb.AppendLine("Added M13QuestTriggerZone via reflection");
        } else {
            sb.AppendLine("TriggerZone already has component");
        }
    }
    // Move trigger 12m west of Mira (clear of pickup ring at 5m)
    var mira = GameObject.Find("[Mira]");
    if (mira != null) {
        var pos = mira.transform.position;
        triggerGo.transform.position = new UnityEngine.Vector3(pos.x - 12f, pos.y, pos.z);
        sb.AppendLine("Trigger moved to: " + triggerGo.transform.position);
    }
} else {
    sb.AppendLine("WARN: TriggerZone_DiscoverQuest NOT FOUND");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wsScene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(wsScene);
sb.AppendLine("WorldScene_0_0 saved");

return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=60)
print(r.stdout[:3000])
